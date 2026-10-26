// rts/src/Game/Assets/Scripts/Camera/RtsCameraController.cs
// The fixed three-quarter top-down RTS camera. Put it on the Camera object (AutoBootstrap does). It keeps a focus point on the
// ground, pans that point with edge scrolling, WASD / arrow keys and middle-mouse drag, zooms by changing the camera height along a
// fixed pitch with the mouse wheel, clamps the focus to the map read from the simulation (through SimulationHost), and places the
// camera each frame at focus - forward x distance. Part of Rts.Game (Unity side, presentation only): it never writes to the
// simulation and nothing here becomes a command.
//
// Input uses the LEGACY Input Manager (Input.GetKey, Input.GetMouseButton, Input.mousePosition, Input.mouseScrollDelta), so no
// Input System package is needed. If you install the Input System package, set Project Settings > Player > Active Input Handling to
// "Both" or these calls throw InvalidOperationException.
//
// Key conflict: A and S are also the attack-move and stop commands (SelectionController). While units are selected the letter keys
// W A S D do not pan; the arrow keys, edge scrolling and middle-mouse drag always do. With nothing selected WASD pans.
//
// Geometry: world X is simulation X, world Z is simulation Y, world Y is up. The camera looks along +Z tilted down by pitchDegrees,
// never rotated around Y, so "up on screen" is always +Z.
using Rts.Game.Bootstrap;
using Rts.Game.UnitSelection;
using UnityEngine;

namespace Rts.Game.CameraRig
{
    /// <summary>Pan, zoom and bounds for the fixed-angle RTS camera. One per scene, on the camera's GameObject.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class RtsCameraController : MonoBehaviour
    {
        /// <summary>Downward tilt of the camera, fixed for the whole match. Unit: degrees from horizontal. Range: 30 to 80 (90 would look straight down); default 55.</summary>
        [SerializeField, Range(30f, 80f)] private float pitchDegrees = 55f;

        /// <summary>Lowest camera height (most zoomed in). Unit: world units above the ground plane (1 world unit = 1 tile). Range: 3 to 100; must be at most maxHeight.</summary>
        [SerializeField, Range(3f, 100f)] private float minHeight = 10f;

        /// <summary>Highest camera height (most zoomed out). Unit: world units above the ground plane. Range: 3 to 100; must be at least minHeight. Default 45 shows roughly the whole 64 by 64 greybox map at 60 degrees field of view.</summary>
        [SerializeField, Range(3f, 100f)] private float maxHeight = 45f;

        /// <summary>Camera height at the first frame. Unit: world units. Range: minHeight to maxHeight (clamped if outside).</summary>
        [SerializeField, Range(3f, 100f)] private float startHeight = 32f;

        /// <summary>Height change for one mouse-wheel notch. Unit: world units per notch. Range: 0.5 to 10; default 3.</summary>
        [SerializeField, Range(0.5f, 10f)] private float zoomStepWorldUnits = 3f;

        /// <summary>How fast the height eases to its target after a wheel notch. Unit: 1 per second (higher is snappier). Range: 1 to 60; default 12.</summary>
        [SerializeField, Range(1f, 60f)] private float zoomSmoothing = 12f;

        /// <summary>Panning speed (keys and edge scrolling) when zoomed fully out; it scales down linearly with height so zoomed-in panning stays controllable. Unit: world units (tiles) per second. Range: 5 to 200; default 40.</summary>
        [SerializeField, Range(5f, 200f)] private float panSpeedTilesPerSecond = 40f;

        /// <summary>Pan with WASD and the arrow keys. Unit: on/off. WASD is suspended while units are selected (see the key conflict note above).</summary>
        [SerializeField] private bool keyboardPanEnabled = true;

        /// <summary>Pan when the mouse is within edgeScrollMarginPixels of a window edge. Unit: on/off. Turn off in the editor if the Game view edge scrolling is annoying.</summary>
        [SerializeField] private bool edgeScrollEnabled = true;

        /// <summary>Width of the screen-edge band that scrolls the camera. Unit: pixels. Range: 1 to 64; default 12.</summary>
        [SerializeField, Range(1f, 64f)] private float edgeScrollMarginPixels = 12f;

        /// <summary>Drag the ground with the middle mouse button. Unit: on/off.</summary>
        [SerializeField] private bool middleMouseDragEnabled = true;

        /// <summary>How far past the map edge the focus point may go. Unit: tiles. Range: 0 to 32; default 4.</summary>
        [SerializeField, Range(0f, 32f)] private float boundsMarginTiles = 4f;

        /// <summary>Map size used until the simulation reports one (or if it never does). Unit: tiles. Range: 8 to 1024; default 64 (the greybox map).</summary>
        [SerializeField, Range(8f, 1024f)] private float fallbackMapSizeTiles = 64f;

        /// <summary>The camera component this script drives. Range: non-null after Awake.</summary>
        private Camera _camera;

        /// <summary>The host that owns the simulation, for the map size. Range: null until found (found in Start and re-searched while null).</summary>
        private SimulationHost _host;

        /// <summary>Selection controller, asked whether units are selected. Range: null until found; the camera works without it.</summary>
        private SelectionController _selection;

        /// <summary>Ground point the camera looks at, as (world X, world Z). Unit: world units (tiles). Range: inside the map plus boundsMarginTiles.</summary>
        private Vector2 _focus;

        /// <summary>Camera height now. Unit: world units. Range: minHeight to maxHeight.</summary>
        private float _height;

        /// <summary>Height the wheel has asked for; _height eases towards it. Unit: world units. Range: minHeight to maxHeight.</summary>
        private float _targetHeight;

        /// <summary>True after the focus has been centred on the real map once. Unit: on/off.</summary>
        private bool _focusInitialised;

        /// <summary>True while a middle-mouse drag is in progress. Unit: on/off.</summary>
        private bool _dragging;

        /// <summary>Mouse position at the previous frame of a drag. Unit: screen pixels, origin bottom-left. Range: inside the window.</summary>
        private Vector3 _dragPrevious;

        /// <summary>The ground point the camera looks at, as (world X, world Z). Unit: tiles. Range: inside the map plus boundsMarginTiles.</summary>
        public Vector2 FocusTiles => _focus;

        /// <summary>Moves the focus to a ground point (clamped to the map on the next frame). Used by future UI such as a minimap click.</summary>
        /// <param name="x">World X (simulation X) in tiles. Range: any.</param>
        /// <param name="z">World Z (simulation Y) in tiles. Range: any.</param>
        public void CentreOn(float x, float z)
        {
            _focus = new Vector2(x, z);
        }

        /// <summary>Keeps minHeight and maxHeight in order when edited in the Inspector.</summary>
        private void OnValidate()
        {
            if (maxHeight < minHeight)
            {
                maxHeight = minHeight;
            }
        }

        /// <summary>Caches the camera and sets the starting height and focus.</summary>
        private void Awake()
        {
            _camera = GetComponent<Camera>();
            // Height to start at, forced into the allowed band.
            float start = Mathf.Clamp(startHeight, minHeight, Mathf.Max(minHeight, maxHeight));
            _height = start;
            _targetHeight = start;
            _focus = new Vector2(fallbackMapSizeTiles * 0.5f, fallbackMapSizeTiles * 0.5f);
        }

        /// <summary>Finds the host and the selection controller.</summary>
        private void Start()
        {
            FindCollaborators();
        }

        /// <summary>Reads input, moves the focus and zoom, and places the camera. Runs after all Update calls so the picture is final.</summary>
        private void LateUpdate()
        {
            if ((_host == null || _selection == null) && Time.frameCount % 60 == 0)
            {
                // Retry about once a second: an object search every frame would be wasteful when a collaborator is absent on purpose.
                FindCollaborators();
            }
            // Real seconds since the last frame, unaffected by Time.timeScale so the camera works while the match is paused.
            float dt = Time.unscaledDeltaTime;

            UpdateBoundsAndInitialFocus(out float maxX, out float maxZ);
            ApplyZoom(dt);
            ApplyMiddleMouseDrag();
            ApplyPanInput(dt);

            // Keep the focus on the map (plus the margin).
            _focus.x = Mathf.Clamp(_focus.x, -boundsMarginTiles, maxX + boundsMarginTiles);
            _focus.y = Mathf.Clamp(_focus.y, -boundsMarginTiles, maxZ + boundsMarginTiles);

            PlaceCamera();
        }

        /// <summary>Looks up the host and the selection controller if they are not known yet.</summary>
        private void FindCollaborators()
        {
            if (_host == null)
            {
                _host = FindFirstObjectByType<SimulationHost>();
            }
            if (_selection == null)
            {
                _selection = FindFirstObjectByType<SelectionController>();
            }
        }

        /// <summary>Reads the map size from the simulation and centres the focus on the map once.</summary>
        /// <param name="maxX">Map width. Unit: tiles (world units). Range: 8 to 1024.</param>
        /// <param name="maxZ">Map height. Unit: tiles (world units). Range: 8 to 1024.</param>
        private void UpdateBoundsAndInitialFocus(out float maxX, out float maxZ)
        {
            maxX = fallbackMapSizeTiles;
            maxZ = fallbackMapSizeTiles;
            if (_host != null && _host.Match != null)
            {
                maxX = _host.Match.Terrain.Width;
                maxZ = _host.Match.Terrain.Height;
                if (!_focusInitialised)
                {
                    _focus = new Vector2(maxX * 0.5f, maxZ * 0.5f);
                    _focusInitialised = true;
                }
            }
        }

        /// <summary>Wheel input changes the target height; the height eases to it.</summary>
        /// <param name="dt">Real seconds since the last frame. Range: 0 upward.</param>
        private void ApplyZoom(float dt)
        {
            // Wheel notches this frame; positive scrolls away from the user, which zooms in. Unit: notches.
            float notches = Input.mouseScrollDelta.y;
            if (notches != 0f)
            {
                _targetHeight = Mathf.Clamp(_targetHeight - notches * zoomStepWorldUnits, minHeight, maxHeight);
            }
            // Fraction of the remaining gap to close this frame (frame-rate independent exponential ease). Unit: fraction. Range: 0 to 1.
            float blend = 1f - Mathf.Exp(-zoomSmoothing * dt);
            _height = Mathf.Lerp(_height, _targetHeight, blend);
        }

        /// <summary>Middle-mouse drag grabs the ground: the point under the cursor stays under the cursor.</summary>
        private void ApplyMiddleMouseDrag()
        {
            if (!middleMouseDragEnabled)
            {
                _dragging = false;
                return;
            }
            if (Input.GetMouseButtonDown(2))
            {
                _dragging = true;
                _dragPrevious = Input.mousePosition;
            }
            if (!Input.GetMouseButton(2))
            {
                _dragging = false;
            }
            if (!_dragging)
            {
                return;
            }
            // Mouse position now. Unit: screen pixels.
            Vector3 current = Input.mousePosition;
            if (TryGroundPoint(_dragPrevious, out Vector3 before) && TryGroundPoint(current, out Vector3 after))
            {
                _focus += new Vector2(before.x - after.x, before.z - after.z);
            }
            _dragPrevious = current;
        }

        /// <summary>Keyboard and edge-scroll panning in world X/Z.</summary>
        /// <param name="dt">Real seconds since the last frame. Range: 0 upward.</param>
        private void ApplyPanInput(float dt)
        {
            if (_dragging)
            {
                return;
            }
            // Pan direction in world X (right) and Z (up the screen). Unit: unitless; each axis -1 to 1 before clamping the length to 1.
            Vector2 direction = Vector2.zero;
            if (keyboardPanEnabled)
            {
                // WASD yields to the unit commands (A attack-move, S stop) while something is selected.
                bool lettersPan = _selection == null || !_selection.HasSelection;
                if (Input.GetKey(KeyCode.RightArrow) || (lettersPan && Input.GetKey(KeyCode.D)))
                {
                    direction.x += 1f;
                }
                if (Input.GetKey(KeyCode.LeftArrow) || (lettersPan && Input.GetKey(KeyCode.A)))
                {
                    direction.x -= 1f;
                }
                if (Input.GetKey(KeyCode.UpArrow) || (lettersPan && Input.GetKey(KeyCode.W)))
                {
                    direction.y += 1f;
                }
                if (Input.GetKey(KeyCode.DownArrow) || (lettersPan && Input.GetKey(KeyCode.S)))
                {
                    direction.y -= 1f;
                }
            }
            if (edgeScrollEnabled && Application.isFocused)
            {
                // Cursor position. Unit: screen pixels, origin bottom-left.
                Vector3 mouse = Input.mousePosition;
                // True when the cursor is inside the window; outside it (the editor Game view border, a second monitor) must not scroll.
                bool inside = mouse.x >= 0f && mouse.y >= 0f && mouse.x <= Screen.width - 1f && mouse.y <= Screen.height - 1f;
                if (inside)
                {
                    if (mouse.x <= edgeScrollMarginPixels)
                    {
                        direction.x -= 1f;
                    }
                    else if (mouse.x >= Screen.width - 1f - edgeScrollMarginPixels)
                    {
                        direction.x += 1f;
                    }
                    if (mouse.y <= edgeScrollMarginPixels)
                    {
                        direction.y -= 1f;
                    }
                    else if (mouse.y >= Screen.height - 1f - edgeScrollMarginPixels)
                    {
                        direction.y += 1f;
                    }
                }
            }
            if (direction == Vector2.zero)
            {
                return;
            }
            // Speed now: full speed at maxHeight, proportionally slower when zoomed in. Unit: world units per second.
            float speed = panSpeedTilesPerSecond * (_height / Mathf.Max(0.01f, maxHeight));
            _focus += Vector2.ClampMagnitude(direction, 1f) * (speed * dt);
        }

        /// <summary>Puts the camera at focus - forward x distance with the fixed pitch.</summary>
        private void PlaceCamera()
        {
            // Camera rotation: pitched down, never turned around Y.
            Quaternion rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            // Distance along the view direction from the camera to the focus point. Unit: world units. Range: height / sin(pitch), at least height.
            float distance = _height / Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);
            transform.rotation = rotation;
            transform.position = new Vector3(_focus.x, 0f, _focus.y) - rotation * Vector3.forward * distance;
        }

        /// <summary>Intersects the ray under a screen point with the ground plane y = 0.</summary>
        /// <param name="screenPoint">Screen position. Unit: pixels, origin bottom-left.</param>
        /// <param name="point">World point on the plane when the ray hits it.</param>
        /// <returns>True when the ray points at the plane.</returns>
        private bool TryGroundPoint(Vector3 screenPoint, out Vector3 point)
        {
            Ray ray = _camera.ScreenPointToRay(screenPoint);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = Vector3.zero;
            return false;
        }
    }
}

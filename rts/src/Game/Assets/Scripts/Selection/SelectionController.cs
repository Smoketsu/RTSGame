// rts/src/Game/Assets/Scripts/Selection/SelectionController.cs
// Player input for the local player: selecting own units (click, shift-click, drag rectangle, control groups) and turning orders into
// simulation commands. This is the ONLY place in the Unity project where input reaches the simulation, and it does so only through
// Simulation.Submit (architecture rule 2: everything is a command). Part of Rts.Game (Unity side, presentation only).
//
// Controls (legacy Input Manager, so no Input System package is needed):
//   Left click            select the nearest own unit under the cursor (Shift adds); click on nothing clears
//   Left drag             draw a rectangle and select every own unit inside it (Shift adds)
//   Ctrl + 0..9           store the selection in a control group;  0..9 recalls it (Shift adds)
//   Right click           ground: MoveUnitCommand per unit;  enemy unit: AttackUnitCommand per armed unit
//   A, then left click    AttackMoveCommand per unit to the clicked ground point (Esc or right click cancels)
//   S                     StopCommand per unit
//   R / U                 RootHqCommand / UprootHqCommand for selected mobile HQs
//   G                     GatherCommand: selected workers gather from the resource field under the cursor
//
// What flows into commands: entity IDs from the simulation, the local player's ID, and a ground point. The ground point is found by
// intersecting the mouse ray with the terrain layer planes (float maths, presentation), then quantised to an INTEGER sub-tile cell
// (default quarter tiles) and converted with Fix64.FromFraction(int, int), so no float ever enters a command.
//
// Commands are submitted for the current tick (Simulation.Clock.Tick), which Submit accepts and the next Step applies, with a
// per-player sequence number kept here. Sequences start at firstSequence (default 1,000,000) so they never collide with the
// sequence numbers of a scripted command file, which the queue would reject as duplicates.
using System.Collections.Generic;
using System.Text;
using Rts.Game.Bootstrap;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Terrain;
using Rts.Sim.Units;
using UnityEngine;

namespace Rts.Game.UnitSelection
{
    /// <summary>Selection, control groups, selection rings and order commands for the local player. One per scene.</summary>
    [DisallowMultipleComponent]
    public sealed class SelectionController : MonoBehaviour
    {
        /// <summary>Which units a pick may return.</summary>
        private enum PickFilter
        {
            /// <summary>Only units owned by the local player.</summary>
            Own,

            /// <summary>Only units owned by a player on a different team than the local player.</summary>
            Enemy,
        }

        /// <summary>Player slot this machine controls. Unit: player slot. Range: 0 to 254; must exist in the match (default 0, the first player of the script).</summary>
        [SerializeField, Range(0, 254)] private int localPlayerSlot = 0;

        /// <summary>First sequence number of the commands this controller issues. Unit: sequence number. Range: 0 to 2147483647; default 1000000, above any scripted sequence.</summary>
        [SerializeField, Min(0)] private int firstSequence = 1000000;

        /// <summary>Mouse travel before a left press counts as a drag rather than a click. Unit: pixels. Range: 1 to 50; default 6.</summary>
        [SerializeField, Range(1f, 50f)] private float dragThresholdPixels = 6f;

        /// <summary>Smallest distance from the mouse ray to a unit that still picks it, so small units are clickable. Unit: tiles (world units). Range: 0.2 to 3; default 0.6.</summary>
        [SerializeField, Range(0.2f, 3f)] private float minPickRadiusTiles = 0.6f;

        /// <summary>Pick radius as a multiple of the unit's collision radius (used when larger than the minimum). Unit: ratio. Range: 0.5 to 4; default 1.25.</summary>
        [SerializeField, Range(0.5f, 4f)] private float pickRadiusPerUnitRadius = 1.25f;

        /// <summary>Order target resolution: each tile is split into this many cells per axis and orders aim at the centre of a cell. Unit: cells per tile. Range: 1 to 8; default 4 (quarter tiles).</summary>
        [SerializeField, Range(1, 8)] private int targetCellsPerTile = 4;

        /// <summary>Selection disc diameter as a multiple of the unit's cube edge. Unit: ratio. Range: 1 to 4; default 1.6.</summary>
        [SerializeField, Range(1f, 4f)] private float ringDiameterPerCubeSize = 1.6f;

        /// <summary>Smallest selection disc diameter. Unit: world units. Range: 0.3 to 4; default 0.9.</summary>
        [SerializeField, Range(0.3f, 4f)] private float ringMinDiameter = 0.9f;

        /// <summary>Thickness of the flat selection disc. Unit: world units. Range: 0.01 to 0.3; default 0.04.</summary>
        [SerializeField, Range(0.01f, 0.3f)] private float ringThickness = 0.04f;

        /// <summary>Colour of the selection disc. Unit: RGBA, each channel 0 to 1. Default bright green.</summary>
        [SerializeField] private Color ringColor = new Color(0.25f, 1f, 0.35f, 1f);

        /// <summary>Fill colour of the drag rectangle. Unit: RGBA, each channel 0 to 1; default translucent green.</summary>
        [SerializeField] private Color boxFillColor = new Color(0.25f, 1f, 0.35f, 0.15f);

        /// <summary>Outline colour of the drag rectangle. Unit: RGBA, each channel 0 to 1; default green.</summary>
        [SerializeField] private Color boxBorderColor = new Color(0.25f, 1f, 0.35f, 0.9f);

        /// <summary>Show the small selection/status text at the top left of the screen. Unit: on/off.</summary>
        [SerializeField] private bool showStatusText = true;

        /// <summary>Number of control groups, keys 0 to 9. Unit: groups.</summary>
        private const int ControlGroupCount = 10;

        /// <summary>Height of the selection disc above the terrain surface, to avoid z-fighting with it. Unit: world units. Range: 0.01 to 0.2.</summary>
        private const float RingLiftWorldUnits = 0.05f;

        /// <summary>Ground offset of the ray height test below which a ray counts as horizontal and cannot pick ground. Unit: unitless (ray direction Y). Range: tiny positive.</summary>
        private const float MinRayDirectionY = 1e-4f;

        /// <summary>The host that owns the simulation. Range: null until found.</summary>
        private SimulationHost _host;

        /// <summary>The camera used for picking and for projecting units to the screen. Range: null until found.</summary>
        private Camera _camera;

        /// <summary>The player this controller commands. Range: valid once the host is running and the slot exists.</summary>
        private PlayerId _localPlayer;

        /// <summary>Frame number of the last search for the host and camera. Unit: frames. Range: any; starts far in the past so the first search runs at once.</summary>
        private int _lastSearchFrame = -1000;

        /// <summary>True once the local player slot has been checked against the match. Unit: on/off.</summary>
        private bool _playerChecked;

        /// <summary>True when the checked slot exists in the match; commands are only issued then. Unit: on/off.</summary>
        private bool _playerValid;

        /// <summary>Next sequence number to use. Unit: sequence number. Range: firstSequence upward, wraps never in practice.</summary>
        private uint _nextSequence;

        /// <summary>Selected units in selection order. Presentation state, never hashed.</summary>
        private readonly List<EntityId> _selected = new List<EntityId>();

        /// <summary>Same IDs as _selected for fast membership tests (order never used, so HashSet is fine here).</summary>
        private readonly HashSet<EntityId> _selectedSet = new HashSet<EntityId>();

        /// <summary>Control groups 0 to 9; each holds unit IDs, possibly of units that have died since (filtered on recall).</summary>
        private readonly List<EntityId>[] _groups = new List<EntityId>[ControlGroupCount];

        /// <summary>True after A was pressed: the next left click is an attack-move target instead of a selection. Unit: on/off.</summary>
        private bool _attackMoveArmed;

        /// <summary>True while the left button is held after a press that started a click or drag. Unit: on/off.</summary>
        private bool _leftDown;

        /// <summary>True once the held left button has moved past the drag threshold. Unit: on/off.</summary>
        private bool _dragging;

        /// <summary>Where the left button went down. Unit: screen pixels, origin bottom-left.</summary>
        private Vector2 _dragStart;

        /// <summary>Selection disc per selected unit, a child of the unit's cube. Values may be destroyed (Unity null) when the unit dies.</summary>
        private readonly Dictionary<EntityId, GameObject> _rings = new Dictionary<EntityId, GameObject>();

        /// <summary>Scratch list reused by SyncRings to avoid allocating every frame.</summary>
        private readonly List<EntityId> _scratch = new List<EntityId>();

        /// <summary>Material shared by all discs, copied from a primitive's default material so it suits the render pipeline. Range: null until the first disc.</summary>
        private Material _ringMaterial;

        /// <summary>Increases whenever the selection or the armed state changes, so the status text is rebuilt only then. Unit: counter.</summary>
        private int _stateVersion;

        /// <summary>Value of _stateVersion the cached status text was built for. Unit: counter.</summary>
        private int _textVersion = -1;

        /// <summary>Cached status text. Range: any text.</summary>
        private string _statusText = "";

        /// <summary>Style for the status text, created in OnGUI (GUI.skin is only valid there). Range: null until the first OnGUI.</summary>
        private GUIStyle _statusStyle;

        /// <summary>True when at least one unit is selected. The camera uses it to give A and S to the commands.</summary>
        public bool HasSelection => _selected.Count > 0;

        /// <summary>The selected unit IDs in selection order (read-only view; do not keep it across frames).</summary>
        public IReadOnlyList<EntityId> Selected => _selected;

        /// <summary>Prepares the empty control groups and the first sequence number.</summary>
        private void Awake()
        {
            for (int i = 0; i < ControlGroupCount; i++)
            {
                _groups[i] = new List<EntityId>();
            }
            _nextSequence = (uint)firstSequence;
        }

        /// <summary>Reads input and issues commands. Does nothing until the simulation is running.</summary>
        private void Update()
        {
            if (!EnsureReady())
            {
                return;
            }
            PruneDeadSelection();
            HandleKeyboard();
            HandleMouse();
        }

        /// <summary>Keeps one selection disc under every selected unit. Runs after the host has moved the cubes.</summary>
        private void LateUpdate()
        {
            if (_host != null && _host.Match != null)
            {
                SyncRings();
            }
        }

        /// <summary>Draws the drag rectangle and the status text.</summary>
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            if (_dragging)
            {
                DrawDragRectangle();
            }
            if (showStatusText && _host != null && _host.Match != null)
            {
                DrawStatusText();
            }
        }

        /// <summary>Destroys the discs and their material.</summary>
        private void OnDestroy()
        {
            foreach (GameObject ring in _rings.Values)
            {
                if (ring != null)
                {
                    Destroy(ring);
                }
            }
            _rings.Clear();
            if (_ringMaterial != null)
            {
                Destroy(_ringMaterial);
            }
        }

        // ------------------------------------------------------------------ setup

        /// <summary>Finds the host and the camera and checks the local player once; returns true when input may be processed.</summary>
        private bool EnsureReady()
        {
            if (_host == null || _camera == null)
            {
                // Search at most about twice a second: an object search every frame would be wasteful when nothing is there to find.
                if (Time.frameCount - _lastSearchFrame < 30)
                {
                    return false;
                }
                _lastSearchFrame = Time.frameCount;
                if (_host == null)
                {
                    _host = FindFirstObjectByType<SimulationHost>();
                }
                if (_camera == null)
                {
                    _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                }
            }
            if (_host == null || _host.Match == null || _camera == null)
            {
                return false;
            }
            if (!_playerChecked)
            {
                _playerChecked = true;
                _localPlayer = new PlayerId((byte)localPlayerSlot);
                _playerValid = _host.Match.Players.Contains(_localPlayer);
                if (!_playerValid)
                {
                    Debug.LogWarning("[SelectionController] Player slot " + localPlayerSlot + " does not exist in this match, so orders are disabled. Set 'Local Player Slot' to a slot of the script.");
                }
            }
            return _playerValid;
        }

        // ------------------------------------------------------------------ keyboard

        /// <summary>Control groups, hotkeys and cancelling the attack-move mode.</summary>
        private void HandleKeyboard()
        {
            // Modifier state this frame.
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            for (int group = 0; group < ControlGroupCount; group++)
            {
                // Alpha0 .. Alpha9 are consecutive key codes.
                if (Input.GetKeyDown(KeyCode.Alpha0 + group))
                {
                    if (ctrl)
                    {
                        AssignGroup(group);
                    }
                    else
                    {
                        RecallGroup(group, shift);
                    }
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetAttackMoveArmed(false);
            }
            if (ctrl)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.A) && HasSelection)
            {
                SetAttackMoveArmed(true);
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                IssueStop();
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                IssueHqToggle(true);
            }
            if (Input.GetKeyDown(KeyCode.U))
            {
                IssueHqToggle(false);
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                IssueGather();
            }
        }

        /// <summary>Stores the current selection in a control group.</summary>
        /// <param name="group">Group number. Range: 0 to 9.</param>
        private void AssignGroup(int group)
        {
            _groups[group].Clear();
            _groups[group].AddRange(_selected);
        }

        /// <summary>Selects the living members of a control group.</summary>
        /// <param name="group">Group number. Range: 0 to 9.</param>
        /// <param name="add">True to add to the current selection, false to replace it.</param>
        private void RecallGroup(int group, bool add)
        {
            // Living members of the group, in stored order.
            List<EntityId> members = new List<EntityId>();
            foreach (EntityId id in _groups[group])
            {
                if (_host.Match.Units.TryGet(id, out Unit unit) && unit.Owner == _localPlayer)
                {
                    members.Add(id);
                }
            }
            if (members.Count == 0)
            {
                return;
            }
            if (!add)
            {
                ClearSelection();
            }
            foreach (EntityId id in members)
            {
                AddToSelection(id);
            }
        }

        // ------------------------------------------------------------------ mouse

        /// <summary>Left button: select or attack-move; right button: order.</summary>
        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (_attackMoveArmed)
                {
                    IssueAttackMove();
                    SetAttackMoveArmed(false);
                }
                else
                {
                    _leftDown = true;
                    _dragging = false;
                    _dragStart = Input.mousePosition;
                }
            }
            if (_leftDown && Input.GetMouseButton(0) && !_dragging)
            {
                if (((Vector2)Input.mousePosition - _dragStart).magnitude >= dragThresholdPixels)
                {
                    _dragging = true;
                }
            }
            if (_leftDown && Input.GetMouseButtonUp(0))
            {
                bool add = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (_dragging)
                {
                    BoxSelect(add);
                }
                else
                {
                    ClickSelect(add);
                }
                _leftDown = false;
                _dragging = false;
            }
            if (Input.GetMouseButtonDown(1))
            {
                if (_attackMoveArmed)
                {
                    SetAttackMoveArmed(false);
                }
                else
                {
                    IssueRightClickOrder();
                }
            }
        }

        // ------------------------------------------------------------------ selecting

        /// <summary>Selects the nearest own unit under the cursor, or clears the selection when there is none and Shift is up.</summary>
        /// <param name="add">True (Shift held) to add to the selection instead of replacing it.</param>
        private void ClickSelect(bool add)
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            bool found = TryPickUnit(ray, PickFilter.Own, out EntityId id);
            if (!add)
            {
                ClearSelection();
            }
            if (found)
            {
                AddToSelection(id);
            }
        }

        /// <summary>Selects every own unit whose cube is inside the dragged screen rectangle.</summary>
        /// <param name="add">True (Shift held) to add to the selection instead of replacing it.</param>
        private void BoxSelect(bool add)
        {
            // Screen rectangle of the drag, origin bottom-left.
            Rect box = CurrentDragRect();
            if (!add)
            {
                ClearSelection();
            }
            foreach (SimulationHost.UnitViewRef view in _host.UnitViews)
            {
                if (!_host.Match.Units.TryGet(view.Id, out Unit unit) || unit.Owner != _localPlayer)
                {
                    continue;
                }
                // Cube position on the screen; z is the distance in front of the camera, so z <= 0 means behind it.
                Vector3 screen = _camera.WorldToScreenPoint(view.Transform.position);
                if (screen.z > 0f && box.Contains(new Vector2(screen.x, screen.y)))
                {
                    AddToSelection(view.Id);
                }
            }
        }

        /// <summary>Adds one unit to the selection if it is not in it yet.</summary>
        /// <param name="id">A living own unit.</param>
        private void AddToSelection(EntityId id)
        {
            if (_selectedSet.Add(id))
            {
                _selected.Add(id);
                _stateVersion++;
            }
        }

        /// <summary>Empties the selection.</summary>
        private void ClearSelection()
        {
            if (_selected.Count > 0)
            {
                _selected.Clear();
                _selectedSet.Clear();
                _stateVersion++;
            }
        }

        /// <summary>Removes units that no longer exist from the selection and disarms attack-move when nothing is selected.</summary>
        private void PruneDeadSelection()
        {
            for (int i = _selected.Count - 1; i >= 0; i--)
            {
                if (!_host.Match.Units.TryGet(_selected[i], out Unit _))
                {
                    _selectedSet.Remove(_selected[i]);
                    _selected.RemoveAt(i);
                    _stateVersion++;
                }
            }
            if (_selected.Count == 0 && _attackMoveArmed)
            {
                SetAttackMoveArmed(false);
            }
        }

        /// <summary>Sets the attack-move mode flag and refreshes the status text.</summary>
        /// <param name="armed">True after A, false after use or cancel.</param>
        private void SetAttackMoveArmed(bool armed)
        {
            if (_attackMoveArmed != armed)
            {
                _attackMoveArmed = armed;
                _stateVersion++;
            }
        }

        // ------------------------------------------------------------------ picking

        /// <summary>
        /// Finds the unit whose cube is closest to the mouse ray, within its pick radius. This works for ground and air units alike
        /// (an air unit's cube floats above its shadow, so a ground-plane test would pick the wrong spot).
        /// </summary>
        /// <param name="ray">Ray from the camera through the cursor.</param>
        /// <param name="filter">Own units or enemy units.</param>
        /// <param name="id">The picked unit when found.</param>
        /// <returns>True when a unit was picked.</returns>
        private bool TryPickUnit(Ray ray, PickFilter filter, out EntityId id)
        {
            id = EntityId.None;
            // Best (smallest) distance from the ray to a cube centre found so far. Unit: world units.
            float bestDistance = float.MaxValue;
            foreach (SimulationHost.UnitViewRef view in _host.UnitViews)
            {
                if (!_host.Match.Units.TryGet(view.Id, out Unit unit))
                {
                    continue;
                }
                if (filter == PickFilter.Own)
                {
                    if (unit.Owner != _localPlayer)
                    {
                        continue;
                    }
                }
                else if (!_host.Match.Players.AreEnemies(_localPlayer, unit.Owner))
                {
                    continue;
                }
                // Vector from the ray origin to the cube centre, and how far along the ray the centre lies. Unit: world units.
                Vector3 toUnit = view.Transform.position - ray.origin;
                float along = Vector3.Dot(toUnit, ray.direction);
                if (along <= 0f)
                {
                    continue;
                }
                // Shortest distance between the ray and the cube centre. Unit: world units.
                float distance = Vector3.Cross(ray.direction, toUnit).magnitude;
                // Largest distance at which this unit still counts as under the cursor. Unit: world units.
                float radius = Mathf.Max(minPickRadiusTiles, unit.Definition.Radius.ToFloatForPresentation() * pickRadiusPerUnitRadius);
                if (distance <= radius && distance < bestDistance)
                {
                    bestDistance = distance;
                    id = view.Id;
                }
            }
            return id.IsValid;
        }

        /// <summary>
        /// Finds the terrain point under a ray by testing the plane of each elevation layer and keeping the nearest hit whose tile
        /// really has that layer. Presentation maths only (floats); the result is quantised to integers before it becomes a command.
        /// </summary>
        /// <param name="ray">Ray from the camera through the cursor.</param>
        /// <param name="point">World hit point when found. Unit: world units; X and Z are tile coordinates.</param>
        /// <returns>True when the ray hits a tile of the map.</returns>
        private bool TryPickGround(Ray ray, out Vector3 point)
        {
            point = Vector3.zero;
            if (Mathf.Abs(ray.direction.y) < MinRayDirectionY)
            {
                return false;
            }
            TerrainGrid grid = _host.Match.Terrain;
            // World height of one layer. Unit: world units per layer.
            float layerHeight = _host.LayerHeightWorldUnits;
            // Smallest ray distance of any valid hit so far. Unit: world units along the ray.
            float bestT = float.MaxValue;
            bool found = false;
            for (int layer = 0; layer <= TerrainTile.MaxHeight; layer++)
            {
                // Distance along the ray to the plane of this layer. Negative or zero means the plane is behind the camera.
                float t = (layer * layerHeight - ray.origin.y) / ray.direction.y;
                if (t <= 0f || t >= bestT)
                {
                    continue;
                }
                Vector3 hit = ray.GetPoint(t);
                // Tile under the hit.
                int tileX = Mathf.FloorToInt(hit.x);
                int tileY = Mathf.FloorToInt(hit.z);
                if (!grid.Contains(tileX, tileY) || grid.At(tileX, tileY).Height != layer)
                {
                    continue;
                }
                bestT = t;
                point = hit;
                found = true;
            }
            return found;
        }

        /// <summary>Turns a picked ground point into a simulation position through integers only (cell index, then an exact fraction).</summary>
        /// <param name="point">World point from TryPickGround.</param>
        private FixVec2 ToSimPoint(Vector3 point)
        {
            // Cell indices along X and Y. Unit: cells (1/targetCellsPerTile tile). Range: 0 to map size x targetCellsPerTile.
            int cellX = Mathf.FloorToInt(point.x * targetCellsPerTile);
            int cellY = Mathf.FloorToInt(point.z * targetCellsPerTile);
            // The centre of cell c is (2c + 1) / (2 x cells per tile) tiles.
            return new FixVec2(
                Fix64.FromFraction(2 * cellX + 1, 2 * targetCellsPerTile),
                Fix64.FromFraction(2 * cellY + 1, 2 * targetCellsPerTile));
        }

        // ------------------------------------------------------------------ orders

        /// <summary>Right click: attack an enemy under the cursor, else move to the ground point.</summary>
        private void IssueRightClickOrder()
        {
            if (_selected.Count == 0)
            {
                return;
            }
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (TryPickUnit(ray, PickFilter.Enemy, out EntityId target))
            {
                foreach (EntityId id in _selected)
                {
                    if (_host.Match.Units.TryGet(id, out Unit unit) && unit.Definition.Weapon != null)
                    {
                        Submit(new AttackUnitCommand(CurrentTick(), _localPlayer, NextSequence(), id, target));
                    }
                }
                return;
            }
            if (!TryPickGround(ray, out Vector3 point))
            {
                return;
            }
            // Target in simulation tiles.
            FixVec2 destination = ToSimPoint(point);
            foreach (EntityId id in _selected)
            {
                if (_host.Match.Units.TryGet(id, out Unit unit) && CanMove(unit))
                {
                    Submit(new MoveUnitCommand(CurrentTick(), _localPlayer, NextSequence(), id, destination));
                }
            }
        }

        /// <summary>Attack-move to the ground point under the cursor; unarmed units simply move.</summary>
        private void IssueAttackMove()
        {
            if (_selected.Count == 0)
            {
                return;
            }
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!TryPickGround(ray, out Vector3 point))
            {
                return;
            }
            FixVec2 destination = ToSimPoint(point);
            foreach (EntityId id in _selected)
            {
                if (!_host.Match.Units.TryGet(id, out Unit unit) || !CanMove(unit))
                {
                    continue;
                }
                if (unit.Definition.Weapon != null)
                {
                    Submit(new AttackMoveCommand(CurrentTick(), _localPlayer, NextSequence(), id, destination));
                }
                else
                {
                    Submit(new MoveUnitCommand(CurrentTick(), _localPlayer, NextSequence(), id, destination));
                }
            }
        }

        /// <summary>Stop order for every selected unit that can move.</summary>
        private void IssueStop()
        {
            foreach (EntityId id in _selected)
            {
                if (_host.Match.Units.TryGet(id, out Unit unit) && CanMove(unit))
                {
                    Submit(new StopCommand(CurrentTick(), _localPlayer, NextSequence(), id));
                }
            }
        }

        /// <summary>Root (R) or uproot (U) every selected mobile HQ that is in the matching state.</summary>
        /// <param name="root">True to root (HQ must be Uprooted), false to uproot (HQ must be Rooted).</param>
        private void IssueHqToggle(bool root)
        {
            foreach (EntityId id in _selected)
            {
                if (!_host.Match.Units.TryGet(id, out Unit unit) || unit.Definition.Hq == null)
                {
                    continue;
                }
                if (root && unit.HqState == HqState.Uprooted)
                {
                    Submit(new RootHqCommand(CurrentTick(), _localPlayer, NextSequence(), id));
                }
                else if (!root && unit.HqState == HqState.Rooted)
                {
                    Submit(new UprootHqCommand(CurrentTick(), _localPlayer, NextSequence(), id));
                }
            }
        }

        /// <summary>Sends every selected worker to gather from the resource field nearest the cursor (within one tile of the cursor's tile).</summary>
        private void IssueGather()
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!TryPickGround(ray, out Vector3 point))
            {
                return;
            }
            // Tile under the cursor.
            int tileX = Mathf.FloorToInt(point.x);
            int tileY = Mathf.FloorToInt(point.z);
            // Closest field found so far and its squared distance to the cursor. Unit: tiles squared.
            ResourceFieldState best = null;
            float bestDistance = float.MaxValue;
            foreach (ResourceFieldState field in _host.Match.Resources.All)
            {
                if (field.IsDepleted || Mathf.Abs(field.X - tileX) > 1 || Mathf.Abs(field.Y - tileY) > 1)
                {
                    continue;
                }
                // Offset from the field's tile centre to the cursor. Unit: tiles.
                float dx = field.X + 0.5f - point.x;
                float dy = field.Y + 0.5f - point.z;
                if (dx * dx + dy * dy < bestDistance)
                {
                    bestDistance = dx * dx + dy * dy;
                    best = field;
                }
            }
            if (best == null)
            {
                Debug.Log("[SelectionController] G: no resource field under the cursor.");
                return;
            }
            foreach (EntityId id in _selected)
            {
                if (_host.Match.Units.TryGet(id, out Unit unit) && unit.Definition.Economy.CanGather)
                {
                    Submit(new GatherCommand(CurrentTick(), _localPlayer, NextSequence(), id, best.Index));
                }
            }
        }

        /// <summary>True when an order to walk makes sense: the unit has a speed and is not a rooted or changing HQ.</summary>
        /// <param name="unit">A living unit.</param>
        private static bool CanMove(Unit unit)
        {
            return unit.Definition.SpeedTilesPerSecond > Fix64.Zero
                && (unit.Definition.Hq == null || unit.HqState == HqState.Uprooted);
        }

        /// <summary>The tick new commands are scheduled for: the current clock tick, which the next Step applies. Unit: ticks. Range: 0 upward.</summary>
        private int CurrentTick()
        {
            return _host.Match.Clock.Tick;
        }

        /// <summary>Returns the next per-player sequence number and advances it. Unit: sequence number.</summary>
        private uint NextSequence()
        {
            return _nextSequence++;
        }

        /// <summary>The single entry into the simulation: Simulation.Submit. A refusal (a host bug such as a past tick) is logged, not thrown.</summary>
        /// <param name="command">The command to queue.</param>
        private void Submit(Command command)
        {
            try
            {
                _host.Match.Submit(command);
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogWarning("[SelectionController] Submit refused " + command + ": " + exception.Message);
            }
        }

        // ------------------------------------------------------------------ rings

        /// <summary>Creates a disc under each newly selected unit and removes the discs of deselected or dead units.</summary>
        private void SyncRings()
        {
            _scratch.Clear();
            foreach (KeyValuePair<EntityId, GameObject> pair in _rings)
            {
                if (pair.Value == null || !_selectedSet.Contains(pair.Key))
                {
                    _scratch.Add(pair.Key);
                }
            }
            foreach (EntityId id in _scratch)
            {
                if (_rings[id] != null)
                {
                    Destroy(_rings[id]);
                }
                _rings.Remove(id);
            }
            foreach (EntityId id in _selected)
            {
                if (!_rings.ContainsKey(id) && _host.TryGetViewTransform(id, out Transform cube))
                {
                    _rings.Add(id, CreateRing(cube));
                }
            }
        }

        /// <summary>Builds a flat cylinder child of a unit's cube, lying just above the terrain beneath the unit.</summary>
        /// <param name="cube">The unit's cube transform (uniformly scaled, unrotated).</param>
        private GameObject CreateRing(Transform cube)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Selection Ring";
            Collider ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null)
            {
                Destroy(ringCollider);
            }
            Renderer ringRenderer = ring.GetComponent<Renderer>();
            if (_ringMaterial == null)
            {
                _ringMaterial = new Material(ringRenderer.sharedMaterial);
                _ringMaterial.name = "Selection Ring";
                _ringMaterial.color = ringColor;
            }
            ringRenderer.sharedMaterial = _ringMaterial;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Cube edge length (the cube is uniformly scaled and its parent has unit scale). Unit: world units.
            float cubeSize = Mathf.Max(0.01f, cube.localScale.x);
            // Disc diameter. Unit: world units.
            float diameter = Mathf.Max(ringMinDiameter, cubeSize * ringDiameterPerCubeSize);
            // Terrain surface height under the cube. Unit: world units.
            float groundY = GroundHeightAt(cube.position);
            // Local scale compensates the parent's scale. A Unity cylinder primitive is 1 wide and 2 tall at scale 1.
            ring.transform.SetParent(cube, false);
            ring.transform.localScale = new Vector3(diameter / cubeSize, ringThickness / (2f * cubeSize), diameter / cubeSize);
            ring.transform.localPosition = new Vector3(0f, (groundY + RingLiftWorldUnits - cube.position.y) / cubeSize, 0f);
            ring.transform.localRotation = Quaternion.identity;
            return ring;
        }

        /// <summary>World height of the terrain surface under a world position (tile layer x layer height).</summary>
        /// <param name="world">World position; only X and Z are used.</param>
        private float GroundHeightAt(Vector3 world)
        {
            TerrainGrid grid = _host.Match.Terrain;
            int tileX = Mathf.Clamp(Mathf.FloorToInt(world.x), 0, grid.Width - 1);
            int tileY = Mathf.Clamp(Mathf.FloorToInt(world.z), 0, grid.Height - 1);
            return grid.At(tileX, tileY).Height * _host.LayerHeightWorldUnits;
        }

        // ------------------------------------------------------------------ GUI

        /// <summary>The drag rectangle in screen space (origin bottom-left) between the press point and the cursor.</summary>
        private Rect CurrentDragRect()
        {
            // Cursor now. Unit: screen pixels.
            Vector2 now = Input.mousePosition;
            Vector2 min = Vector2.Min(_dragStart, now);
            Vector2 max = Vector2.Max(_dragStart, now);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>Draws the translucent selection rectangle with a thin border (IMGUI uses a top-left origin, so Y is flipped).</summary>
        private void DrawDragRectangle()
        {
            Rect box = CurrentDragRect();
            // Same rectangle in IMGUI coordinates. Unit: pixels.
            Rect gui = new Rect(box.xMin, Screen.height - box.yMax, box.width, box.height);
            // Border thickness. Unit: pixels.
            const float border = 2f;
            Color previous = GUI.color;
            GUI.color = boxFillColor;
            GUI.DrawTexture(gui, Texture2D.whiteTexture);
            GUI.color = boxBorderColor;
            GUI.DrawTexture(new Rect(gui.xMin, gui.yMin, gui.width, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gui.xMin, gui.yMax - border, gui.width, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gui.xMin, gui.yMin, border, gui.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gui.xMax - border, gui.yMin, border, gui.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Draws the selection count, the first selected unit's name and the attack-move hint at the top left.</summary>
        private void DrawStatusText()
        {
            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(GUI.skin.label);
                _statusStyle.fontSize = 14;
                _statusStyle.normal.textColor = Color.white;
            }
            if (_textVersion != _stateVersion)
            {
                _textVersion = _stateVersion;
                _statusText = BuildStatusText();
            }
            // Text box position and size. Unit: pixels from the top-left corner.
            Rect area = new Rect(10f, 8f, 520f, 80f);
            Color previous = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), _statusText, _statusStyle);
            GUI.color = previous;
            GUI.Label(area, _statusText, _statusStyle);
        }

        /// <summary>Builds the status text for the current selection.</summary>
        private string BuildStatusText()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Player ").Append(_localPlayer.Value).Append("   Selected: ").Append(_selected.Count);
            if (_selected.Count > 0 && _host.Match.Units.TryGet(_selected[0], out Unit first))
            {
                text.Append("  (").Append(first.Definition.DisplayName);
                if (_selected.Count > 1)
                {
                    text.Append(" and others");
                }
                text.Append(')');
            }
            if (_attackMoveArmed)
            {
                text.Append("\nAttack-move: left-click the target (Esc or right-click cancels)");
            }
            return text.ToString();
        }
    }
}

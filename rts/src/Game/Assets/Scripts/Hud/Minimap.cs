// rts/src/Game/Assets/Scripts/Hud/Minimap.cs
// The minimap, bottom left (player_interface_2.md UI-14): one pixel per tile, point filtered, for the local team only. Base layer,
// redrawn after each stepped tick (at most once per frame): the terrain colour TerrainView uses, at full colour where the team sees the
// tile now, dimmed where it has explored it, black where it never has (the FogView rule; F8 Show All applies). Drawn over it each frame:
// units as dots in their owner's colour (own and allied always, others only when FogView.CanSee; structures as their footprint tile,
// mobile units 2 x 2 pixels), the Core ring in the holder's colour (grey with no holder), and the camera's view as the quadrilateral
// where the four viewport corner rays meet the ground. Left click or drag moves the camera there; right click orders the selection to
// that point (SelectionController.IssueGroundOrder: moves, and rally points for producers that cannot walk); while attack-move (A) is
// armed, left click attack-moves there. No enemy picking on the minimap. Points are built from integer cells through
// SelectionController.ToSimPoint, so no float enters a command. Part of Rts.Game (presentation only).
using Rts.Game.Bootstrap;
using Rts.Game.CameraRig;
using Rts.Game.Presentation;
using Rts.Game.TerrainDisplay;
using Rts.Game.UnitSelection;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Terrain, fog, units, Core and camera frame in one small texture, with camera and order input.</summary>
    [DisallowMultipleComponent]
    public sealed class Minimap : MonoBehaviour
    {
        /// <summary>Receives pointer events on the minimap image only (a handler on the HUD root would also catch every other panel).</summary>
        private sealed class Pointer : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            /// <summary>The minimap that acts on the events. Range: set right after AddComponent.</summary>
            public Minimap Owner;

            /// <summary>Forwards a press.</summary>
            /// <param name="eventData">The pointer event.</param>
            public void OnPointerDown(PointerEventData eventData)
            {
                Owner.OnPress(eventData, false);
            }

            /// <summary>Forwards a drag (only the left button moves the camera while dragging).</summary>
            /// <param name="eventData">The pointer event.</param>
            public void OnDrag(PointerEventData eventData)
            {
                Owner.OnPress(eventData, true);
            }
        }

        /// <summary>Edge of the minimap's longer side. Unit: reference pixels (1920 x 1080). Range: 128 to 512; default 256.</summary>
        [SerializeField, Range(128f, 512f)] private float size = 256f;

        /// <summary>Brightness of explored tiles the team does not see now. Unit: fraction. Range: 0 to 1; default 0.45.</summary>
        [SerializeField, Range(0f, 1f)] private float exploredBrightness = 0.45f;

        /// <summary>Colour of the camera frame. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color32 FrameColor = new Color32(255, 255, 255, 255);

        /// <summary>Colour of the Core ring with no holder. Unit: RGBA.</summary>
        private static readonly Color32 NeutralCore = new Color32(190, 190, 190, 255);

        /// <summary>The image showing the texture. Range: non-null after Awake.</summary>
        private RawImage _image;

        /// <summary>One pixel per tile; null until the match runs.</summary>
        private Texture2D _texture;

        /// <summary>Terrain and fog per pixel, redrawn after each tick. Range: width x height once built.</summary>
        private Color32[] _base;

        /// <summary>The picture of this frame: the base plus units, Core and frame. Range: width x height once built.</summary>
        private Color32[] _frame;

        /// <summary>Tick the base layer was drawn for. Unit: ticks. Range: -1 (never) upward.</summary>
        private int _baseTick = -1;

        /// <summary>Show All setting the base was drawn with. Unit: on/off.</summary>
        private bool _baseShowAll;

        /// <summary>The local command path. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>The terrain view whose tile colours the minimap shares. Range: null until found.</summary>
        private TerrainView _terrain;

        /// <summary>The selection, for orders and ground points. Range: null until found.</summary>
        private SelectionController _selection;

        /// <summary>The camera controller, for moving the view. Range: null until found.</summary>
        private RtsCameraController _cameraController;

        /// <summary>Builds the image, bottom left, with its pointer receiver; hidden until a match runs.</summary>
        private void Awake()
        {
            _image = HudBuild.Rect(transform, "Minimap", new Vector2(0f, 0f), new Vector2(12f, 10f), new Vector2(size, size)).gameObject.AddComponent<RawImage>();
            _image.color = Color.white;
            _image.gameObject.AddComponent<Pointer>().Owner = this;
            _image.gameObject.SetActive(false);
        }

        /// <summary>Frees the texture.</summary>
        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
            }
        }

        /// <summary>Redraws the base after a tick and the overlays every frame.</summary>
        private void LateUpdate()
        {
            if (_commands == null)
            {
                _commands = FindAnyObjectByType<LocalCommands>();
            }
            if (_terrain == null)
            {
                _terrain = FindAnyObjectByType<TerrainView>();
            }
            bool ready = _commands != null && _commands.IsReady && _terrain != null && _terrain.HasTerrain;
            _image.gameObject.SetActive(ready);
            if (!ready)
            {
                return;
            }
            Simulation sim = _commands.Host.Match;
            int width = sim.Terrain.Width;
            int height = sim.Terrain.Height;
            if (_texture == null)
            {
                _texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Minimap" };
                _base = new Color32[width * height];
                _frame = new Color32[width * height];
                _image.texture = _texture;
                // Keep the map's aspect inside the square. Unit: reference pixels.
                float scale = size / Mathf.Max(width, height);
                _image.rectTransform.sizeDelta = new Vector2(width * scale, height * scale);
            }
            TeamId team = sim.Players.TeamOf(_commands.LocalPlayer);
            if (sim.Clock.Tick != _baseTick || FogView.ShowAll != _baseShowAll)
            {
                DrawBase(sim, team, width, height);
            }
            System.Array.Copy(_base, _frame, _base.Length);
            DrawUnits(sim, team, width, height);
            DrawCore(sim, width, height);
            DrawCameraFrame(width, height);
            _texture.SetPixels32(_frame);
            _texture.Apply(false);
        }

        /// <summary>Terrain colour per tile, full where visible, dimmed where explored, black where never seen.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="team">The local team.</param>
        /// <param name="width">Map width. Unit: tiles.</param>
        /// <param name="height">Map height. Unit: tiles.</param>
        private void DrawBase(Simulation sim, TeamId team, int width, int height)
        {
            _baseTick = sim.Clock.Tick;
            _baseShowAll = FogView.ShowAll;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color32 colour = _terrain.TileColour(index);
                    if (!FogView.ShowAll && !sim.Vision.IsTileVisible(team, x, y))
                    {
                        colour = sim.Vision.IsTileExplored(team, x, y)
                            ? new Color32((byte)(colour.r * exploredBrightness), (byte)(colour.g * exploredBrightness), (byte)(colour.b * exploredBrightness), 255)
                            : new Color32(0, 0, 0, 255);
                    }
                    _base[index] = colour;
                }
            }
        }

        /// <summary>A dot per unit the local team may see: structures as their tile, mobile units 2 x 2 pixels.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="team">The local team.</param>
        /// <param name="width">Map width. Unit: tiles.</param>
        /// <param name="height">Map height. Unit: tiles.</param>
        private void DrawUnits(Simulation sim, TeamId team, int width, int height)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (!FogView.CanSee(sim, team, unit))
                {
                    continue;
                }
                Color32 colour = SimulationHost.ColorOf(unit.Owner);
                int tile = sim.Terrain.TileOf(unit.Position);
                int x = tile % width;
                int y = tile / width;
                // shortcut: a footprint is one tile in Phase 1 data, so a structure is one pixel; draw the real footprint when bigger buildings arrive.
                int extent = unit.Definition.Economy.FootprintTiles > 0 ? 1 : 2;
                for (int dy = 0; dy < extent; dy++)
                {
                    for (int dx = 0; dx < extent; dx++)
                    {
                        Plot(x + dx, y + dy, colour, width, height);
                    }
                }
            }
        }

        /// <summary>The Core ring in the holder's colour (grey with no holder), when the map has a Core.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="width">Map width. Unit: tiles.</param>
        /// <param name="height">Map height. Unit: tiles.</param>
        private void DrawCore(Simulation sim, int width, int height)
        {
            CoreArea core = sim.Map.Core;
            if (core == null)
            {
                return;
            }
            Color32 colour = sim.Cycles.Holder.IsValid ? (Color32)CoreView.TeamColor(sim, sim.Cycles.Holder) : NeutralCore;
            float cx = core.Centre.X.ToFloatForPresentation();
            float cy = core.Centre.Y.ToFloatForPresentation();
            float radius = core.RadiusTiles.ToFloatForPresentation();
            // Points around the ring: enough that neighbouring points fall on neighbouring pixels. Unit: points.
            int points = Mathf.Max(16, Mathf.CeilToInt(radius * 8f));
            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points;
                Plot(Mathf.FloorToInt(cx + Mathf.Cos(angle) * radius), Mathf.FloorToInt(cy + Mathf.Sin(angle) * radius), colour, width, height);
            }
        }

        /// <summary>The camera's view: where the four viewport corner rays meet the ground plane y = 0, joined by lines.</summary>
        /// <param name="width">Map width. Unit: tiles.</param>
        /// <param name="height">Map height. Unit: tiles.</param>
        private void DrawCameraFrame(int width, int height)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            Vector2[] corners = new Vector2[4];
            Vector3[] viewport = { new Vector3(0f, 0f), new Vector3(1f, 0f), new Vector3(1f, 1f), new Vector3(0f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                Ray ray = camera.ViewportPointToRay(viewport[i]);
                // A ray that never reaches the ground (above the horizon) is clamped to a far point along the ground direction.
                float t = ray.direction.y < -1e-4f ? -ray.origin.y / ray.direction.y : 200f;
                Vector3 hit = ray.GetPoint(t);
                corners[i] = new Vector2(hit.x, hit.z);
            }
            for (int i = 0; i < 4; i++)
            {
                Line(corners[i], corners[(i + 1) % 4], width, height);
            }
        }

        /// <summary>Draws a line between two points in tiles (Bresenham on pixels, clipped to the texture).</summary>
        /// <param name="from">Start point. Unit: tiles.</param>
        /// <param name="to">End point. Unit: tiles.</param>
        /// <param name="width">Map width. Unit: tiles.</param>
        /// <param name="height">Map height. Unit: tiles.</param>
        private void Line(Vector2 from, Vector2 to, int width, int height)
        {
            int x0 = Mathf.FloorToInt(from.x);
            int y0 = Mathf.FloorToInt(from.y);
            int x1 = Mathf.FloorToInt(to.x);
            int y1 = Mathf.FloorToInt(to.y);
            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            // Step limit, so a far-off corner cannot make the loop long. Unit: pixels.
            int limit = 4 * (width + height);
            for (int step = 0; step < limit; step++)
            {
                Plot(x0, y0, FrameColor, width, height);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        /// <summary>Sets one pixel of this frame when it is on the texture.</summary>
        /// <param name="x">Pixel X (tile X). Unit: tiles.</param>
        /// <param name="y">Pixel Y (tile Y). Unit: tiles.</param>
        /// <param name="colour">Colour.</param>
        /// <param name="width">Map width. Unit: tiles.</param>
        /// <param name="height">Map height. Unit: tiles.</param>
        private void Plot(int x, int y, Color32 colour, int width, int height)
        {
            if (x >= 0 && y >= 0 && x < width && y < height)
            {
                _frame[y * width + x] = colour;
            }
        }

        /// <summary>
        /// Acts on a press or drag on the minimap: left moves the camera there (or attack-moves while A is armed, on the press only), right
        /// orders the selection there (on the press only).
        /// </summary>
        /// <param name="eventData">The pointer event.</param>
        /// <param name="drag">True for a drag event, false for the press.</param>
        private void OnPress(PointerEventData eventData, bool drag)
        {
            if (_commands == null || !_commands.IsReady)
            {
                return;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_image.rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }
            Rect rect = _image.rectTransform.rect;
            // Position across the minimap. Unit: fraction. Range: 0 to 1 (clamped).
            float u = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            float v = Mathf.Clamp01((local.y - rect.yMin) / rect.height);
            Simulation sim = _commands.Host.Match;
            // The point in tiles; it becomes a command only through ToSimPoint's integer cells.
            float x = u * sim.Terrain.Width;
            float y = v * sim.Terrain.Height;
            if (_selection == null)
            {
                _selection = FindAnyObjectByType<SelectionController>();
            }
            if (_cameraController == null)
            {
                _cameraController = FindAnyObjectByType<RtsCameraController>();
            }
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (!drag && _selection != null && _selection.AttackMoveArmed)
                {
                    _selection.IssueAttackMoveAt(_selection.ToSimPoint(new Vector3(x, 0f, y)));
                }
                else if (_cameraController != null)
                {
                    _cameraController.CentreOn(x, y);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right && !drag && _selection != null)
            {
                _selection.IssueGroundOrder(_selection.ToSimPoint(new Vector3(x, 0f, y)));
            }
        }
    }
}

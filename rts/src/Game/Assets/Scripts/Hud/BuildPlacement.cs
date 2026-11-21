// rts/src/Game/Assets/Scripts/Hud/BuildPlacement.cs
// Structure placement with a ghost (player_interface.md UI-3). Begin(definition) is called by the build menu of the command card;
// while active the mode holds PointerCapture: a ghost cube follows the tile under the cursor, a left click sends
// BuildStructureCommand(worker, definition, tile) through LocalCommands for the selected worker nearest the tile centre (ties lowest
// EntityId), holding Shift keeps the mode armed, and Esc or a right click cancels. The ghost is red with the reason when the tile
// has a placement problem (EconomyRules.PlacementProblem, the build order's own check), amber when a unit stands on the tile,
// green otherwise; these are hints only and the simulation decides. Every frame it also draws a faint ghost for every pending build site of the local
// player (active orders and queued tiles), and a short red flash on each tile of a wall line the simulation skipped.
// Walls and gates (UI-4, walls_and_tech.md D.5): the mode becomes a drag. On press and every move the line from the press tile to
// the cursor tile is WallLine.Rasterise(..., wall_line_max_tiles) with one ghost per tile (green, red with the tile problem, amber
// with a unit on it, red past the count the player can pay for) and the label "N x cost = total", red when not all valid tiles are
// affordable. On release the valid tiles are split into k = min(workers, tiles) contiguous chunks in drag order (sizes differ by at
// most one, earlier chunks larger); chunk i goes to the unassigned worker nearest its first tile (ties lowest EntityId), reversed
// when that worker is nearer its last tile, as one BuildWallLineCommand each. The simulation decides what is built and pays.
// Presentation only: nothing here changes the simulation except through the commands.
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Game.Controls;
using Rts.Game.UnitSelection;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EntityId = Rts.Sim.Core.EntityId;

namespace Rts.Game.Hud
{
    /// <summary>Ghost placement of one structure type, and faint ghosts of pending build sites.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildPlacement : MonoBehaviour
    {
        /// <summary>Ghost colour of a tile that can be ordered. Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.4f, 0.45f);

        /// <summary>Ghost colour of a tile the simulation will refuse. Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color invalidColor = new Color(1f, 0.25f, 0.2f, 0.45f);

        /// <summary>Ghost colour of a tile with a unit standing on it. Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color occupiedColor = new Color(1f, 0.75f, 0.2f, 0.45f);

        /// <summary>Colour of the faint ghosts of pending build sites. Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color pendingColor = new Color(0.6f, 0.85f, 1f, 0.25f);

        /// <summary>Ghost height. Unit: world units. Range: 0.1 to 3.</summary>
        private const float GhostHeight = 1f;

        /// <summary>Definition being placed, or null while the mode is off.</summary>
        private string _definitionId;

        /// <summary>The cursor ghost. Range: null until first used.</summary>
        private Renderer _ghost;

        /// <summary>Faint ghosts of pending sites, reused frame to frame. Range: 0 to the most pending sites seen.</summary>
        private readonly List<Renderer> _pending = new List<Renderer>();

        /// <summary>Reason text next to the cursor while the ghost is red, or the "N x cost = total" label of a wall line. Range: non-null after Awake.</summary>
        private Text _reason;

        /// <summary>How long a skipped wall tile flashes red. Unit: seconds (real time). Range: 0.2 to 5.</summary>
        private const float FlashSeconds = 1.5f;

        /// <summary>Tiles of the wall line under the cursor, in drag order. Presentation scratch list.</summary>
        private readonly List<(int X, int Y)> _line = new List<(int X, int Y)>();

        /// <summary>Valid tiles of the line (no tile problem), in drag order; what release sends. Presentation scratch list.</summary>
        private readonly List<(int X, int Y)> _validTiles = new List<(int X, int Y)>();

        /// <summary>One ghost per tile of the wall line, reused frame to frame. Range: 0 to wall_line_max_tiles.</summary>
        private readonly List<Renderer> _lineGhosts = new List<Renderer>();

        /// <summary>Skipped wall tiles still flashing, with the real time the flash ends. Unit: tiles; seconds of Time.unscaledTime.</summary>
        private readonly List<(int X, int Y, float Until)> _flashes = new List<(int X, int Y, float Until)>();

        /// <summary>True while the left button, pressed on the map, drags a wall line. Unit: on/off.</summary>
        private bool _dragging;

        /// <summary>Tile where the wall drag started. Unit: tiles.</summary>
        private int _dragStartX;

        /// <summary>Tile where the wall drag started. Unit: tiles.</summary>
        private int _dragStartY;

        /// <summary>Last tile the cursor was over during the drag, used while the cursor is off the map. Unit: tiles.</summary>
        private int _dragEndX;

        /// <summary>Last tile the cursor was over during the drag, used while the cursor is off the map. Unit: tiles.</summary>
        private int _dragEndY;

        /// <summary>True once subscribed to the host's events (for skipped tiles). Unit: on/off.</summary>
        private bool _subscribed;

        /// <summary>Selected own workers this frame, ascending EntityId. Presentation scratch list.</summary>
        private readonly List<Unit> _workers = new List<Unit>();

        /// <summary>The local command path. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>The selection source (selected units, ground picking). Range: null until found.</summary>
        private SelectionController _selection;

        /// <summary>True while a structure is being placed. Unit: on/off.</summary>
        public bool IsActive => _definitionId != null;

        /// <summary>Starts placing a structure type; replaces any placement in progress.</summary>
        /// <param name="definitionId">A structure of the local faction (from the build menu).</param>
        public void Begin(string definitionId)
        {
            _definitionId = definitionId;
            PointerCapture.Capture(this);
        }

        /// <summary>Stops placing and hides the ghost.</summary>
        public void Cancel()
        {
            _definitionId = null;
            _dragging = false;
            PointerCapture.Release(this);
            if (_ghost != null)
            {
                _ghost.gameObject.SetActive(false);
            }
            HideLineGhosts(0);
            _reason.text = "";
        }

        /// <summary>Creates the cursor reason text under the HUD canvas.</summary>
        private void Awake()
        {
            _reason = HudBuild.Text(transform, "Placement Reason", Vector2.zero, Vector2.zero, new Vector2(420f, 30f), 20, TextAnchor.LowerLeft);
        }

        /// <summary>Releases the pointer, unsubscribes and destroys the ghosts.</summary>
        private void OnDestroy()
        {
            PointerCapture.Release(this);
            if (_subscribed && _commands != null && _commands.Host != null)
            {
                _commands.Host.EventRaised -= OnSimEvent;
            }
            foreach (Renderer ghost in _lineGhosts)
            {
                if (ghost != null)
                {
                    Destroy(ghost.gameObject);
                }
            }
            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
            }
            foreach (Renderer ghost in _pending)
            {
                if (ghost != null)
                {
                    Destroy(ghost.gameObject);
                }
            }
        }

        /// <summary>Draws pending sites, and while placing: moves the ghost, places on a left click, cancels on Esc or a right click.</summary>
        private void Update()
        {
            if (_commands == null)
            {
                _commands = FindAnyObjectByType<LocalCommands>();
            }
            if (_selection == null)
            {
                _selection = FindAnyObjectByType<SelectionController>();
            }
            if (_commands == null || _selection == null || !_commands.IsReady)
            {
                return;
            }
            if (!_subscribed)
            {
                _commands.Host.EventRaised += OnSimEvent;
                _subscribed = true;
            }
            Simulation sim = _commands.Host.Match;
            DrawPendingSites(sim);
            if (!IsActive)
            {
                return;
            }
            CollectWorkers(sim);
            if (_workers.Count == 0 || GameInput.GetKeyDown(KeyCode.Escape) || GameInput.GetMouseButtonDown(1))
            {
                Cancel();
                return;
            }
            Camera camera = Camera.main;
            // Ground point under the cursor; only meaningful when picked.
            Vector3 point = Vector3.zero;
            bool picked = camera != null && _selection.TryPickGround(camera.ScreenPointToRay(GameInput.mousePosition), out point);
            if (!picked && !_dragging)
            {
                if (_ghost != null)
                {
                    _ghost.gameObject.SetActive(false);
                }
                HideLineGhosts(0);
                _reason.text = "";
                return;
            }
            // Tile under the cursor, or the last one of a drag while the cursor is off the map. Unit: tiles.
            int tileX = picked ? Mathf.FloorToInt(point.x) : _dragEndX;
            int tileY = picked ? Mathf.FloorToInt(point.z) : _dragEndY;
            // True while the cursor is over a HUD panel; a click there belongs to the HUD.
            bool overHud = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (sim.Content.Units.TryGetValue(_definitionId, out UnitDefinition armed) && armed.Economy.IsWall)
            {
                UpdateWallDrag(sim, armed, tileX, tileY, overHud);
                return;
            }
            Unit worker = NearestWorker(sim, tileX, tileY);
            string problem = TileProblem(sim, tileX, tileY);
            Color color = problem != null ? invalidColor : UnitOnTile(sim, tileX, tileY) ? occupiedColor : validColor;
            PlaceGhost(EnsureGhost(), sim, tileX, tileY, color);
            _reason.text = problem != null ? RejectionTexts.For(problem) ?? problem : "";
            _reason.color = HudBuild.TextColor;
            PlaceLabelAtCursor();
            if (GameInput.GetMouseButtonDown(0) && !overHud)
            {
                _commands.Submit(new BuildStructureCommand(_commands.Tick, _commands.LocalPlayer, _commands.NextSequence(), worker.Id, _definitionId, tileX, tileY));
                if (!GameInput.GetKey(KeyCode.LeftShift) && !GameInput.GetKey(KeyCode.RightShift))
                {
                    Cancel();
                }
            }
        }

        /// <summary>Wall mode: starts, draws and releases a wall line drag (see the file header).</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="wall">The armed wall or gate definition.</param>
        /// <param name="tileX">Tile under the cursor. Unit: tiles.</param>
        /// <param name="tileY">Tile under the cursor. Unit: tiles.</param>
        /// <param name="overHud">True while the cursor is over a HUD panel.</param>
        private void UpdateWallDrag(Simulation sim, UnitDefinition wall, int tileX, int tileY, bool overHud)
        {
            if (_ghost != null)
            {
                _ghost.gameObject.SetActive(false);
            }
            if (!_dragging && GameInput.GetMouseButtonDown(0) && !overHud)
            {
                _dragging = true;
                _dragStartX = tileX;
                _dragStartY = tileY;
            }
            _dragEndX = tileX;
            _dragEndY = tileY;
            WallLine.Rasterise(_dragging ? _dragStartX : tileX, _dragging ? _dragStartY : tileY, tileX, tileY, sim.Tuning.Economy.WallLineMaxTiles, _line);
            sim.Players.TryGet(_commands.LocalPlayer, out Player player);
            // Tiles the player can pay for at once. Unit: tiles. Range: 0 upward (int.MaxValue for a free wall).
            int affordable = Mathf.Min(wall.CostCommon > 0 ? player.Common / wall.CostCommon : int.MaxValue, wall.CostRare > 0 ? player.Rare / wall.CostRare : int.MaxValue);
            _validTiles.Clear();
            for (int i = 0; i < _line.Count; i++)
            {
                (int x, int y) = _line[i];
                string problem = EconomyRules.WallLineTileProblem(sim, _workers[0], x, y);
                Color color = problem != null ? invalidColor
                    : _validTiles.Count >= affordable ? invalidColor
                    : UnitOnTile(sim, x, y) ? occupiedColor : validColor;
                if (problem == null)
                {
                    _validTiles.Add((x, y));
                }
                PlaceGhost(LineGhost(i), sim, x, y, color);
            }
            HideLineGhosts(_line.Count);
            // Cost of the valid tiles; rare is shown only when the wall costs rare.
            string cost = wall.CostCommon + "c" + (wall.CostRare > 0 ? " " + wall.CostRare + "r" : "");
            string total = (_validTiles.Count * wall.CostCommon) + "c" + (wall.CostRare > 0 ? " " + (_validTiles.Count * wall.CostRare) + "r" : "");
            _reason.text = _validTiles.Count + " x " + cost + " = " + total;
            _reason.color = _validTiles.Count > affordable ? new Color(1f, 0.4f, 0.35f, 1f) : HudBuild.TextColor;
            PlaceLabelAtCursor();
            if (_dragging && GameInput.GetMouseButtonUp(0))
            {
                _dragging = false;
                SendLine(sim);
                if (!GameInput.GetKey(KeyCode.LeftShift) && !GameInput.GetKey(KeyCode.RightShift))
                {
                    Cancel();
                }
            }
        }

        /// <summary>
        /// Splits _validTiles into contiguous chunks, one per worker (earlier chunks larger), and sends one BuildWallLineCommand per chunk to the
        /// unassigned worker nearest its first tile (ties lowest EntityId), reversing the chunk when that worker is nearer its last tile.
        /// </summary>
        /// <param name="sim">The running match.</param>
        private void SendLine(Simulation sim)
        {
            int count = _validTiles.Count;
            if (count == 0)
            {
                return;
            }
            // Number of chunks and the base chunk size; the first (count % chunks) chunks get one more tile. Unit: tiles.
            int chunks = Mathf.Min(_workers.Count, count);
            int baseSize = count / chunks;
            int extra = count % chunks;
            // Workers not yet given a chunk, ascending EntityId.
            List<Unit> unassigned = new List<Unit>(_workers);
            int start = 0;
            for (int c = 0; c < chunks; c++)
            {
                int size = baseSize + (c < extra ? 1 : 0);
                List<(int X, int Y)> chunk = _validTiles.GetRange(start, size);
                start += size;
                Unit worker = null;
                Fix64 best = Fix64.Zero;
                foreach (Unit candidate in unassigned)
                {
                    Fix64 distance = DistanceSquared(sim, candidate, chunk[0].X, chunk[0].Y);
                    // unassigned is in ascending ID order, so a strict comparison keeps the lowest ID on a tie.
                    if (worker == null || distance < best)
                    {
                        worker = candidate;
                        best = distance;
                    }
                }
                unassigned.Remove(worker);
                if (DistanceSquared(sim, worker, chunk[size - 1].X, chunk[size - 1].Y) < best)
                {
                    chunk.Reverse();
                }
                _commands.Submit(new BuildWallLineCommand(_commands.Tick, _commands.LocalPlayer, _commands.NextSequence(), worker.Id, _definitionId, chunk));
            }
        }

        /// <summary>Squared distance from a worker to a tile centre, in fixed point (the clamped tile when outside the map). Unit: tiles squared.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="worker">A unit.</param>
        /// <param name="tileX">Tile X. Unit: tiles.</param>
        /// <param name="tileY">Tile Y. Unit: tiles.</param>
        private static Fix64 DistanceSquared(Simulation sim, Unit worker, int tileX, int tileY)
        {
            FixVec2 centre = sim.Terrain.CentreOf(sim.Terrain.IndexOf(Mathf.Clamp(tileX, 0, sim.Terrain.Width - 1), Mathf.Clamp(tileY, 0, sim.Terrain.Height - 1)));
            Fix64 dx = worker.Position.X - centre.X;
            Fix64 dy = worker.Position.Y - centre.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>Puts the reason or line label just right of and above the cursor, in canvas units.</summary>
        private void PlaceLabelAtCursor()
        {
            _reason.rectTransform.anchoredPosition = (Vector2)GameInput.mousePosition / Mathf.Max(0.01f, GetComponent<Canvas>().scaleFactor) + new Vector2(18f, 12f);
        }

        /// <summary>Wall line ghost number <paramref name="index"/>, created when the pool is too small.</summary>
        /// <param name="index">Pool index. Range: 0 to the pool size.</param>
        private Renderer LineGhost(int index)
        {
            if (index == _lineGhosts.Count)
            {
                _lineGhosts.Add(CreateGhost("Wall Line Ghost " + index));
            }
            return _lineGhosts[index];
        }

        /// <summary>Hides the wall line ghosts from <paramref name="first"/> on.</summary>
        /// <param name="first">First ghost to hide. Range: 0 upward.</param>
        private void HideLineGhosts(int first)
        {
            for (int i = first; i < _lineGhosts.Count; i++)
            {
                _lineGhosts[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Starts a red flash on each skipped tile of the local player's wall lines.</summary>
        /// <param name="simEvent">An event of the tick that just ran.</param>
        private void OnSimEvent(SimEvent simEvent)
        {
            if (simEvent is BuildLineTileSkippedEvent skipped && skipped.Owner == _commands.LocalPlayer)
            {
                _flashes.Add((skipped.TileX, skipped.TileY, Time.unscaledTime + FlashSeconds));
            }
        }

        /// <summary>
        /// The reason the simulation would refuse a structure on this tile, or null: EconomyRules.PlacementProblem (interface_support.md I1), the
        /// same check the build order makes, fog-safe for the local team. Its supply figure is from the last step, so the order may still differ;
        /// the simulation decides.
        /// </summary>
        /// <param name="sim">The running match.</param>
        /// <param name="tileX">Tile X. Unit: tiles.</param>
        /// <param name="tileY">Tile Y. Unit: tiles.</param>
        private string TileProblem(Simulation sim, int tileX, int tileY)
        {
            return EconomyRules.PlacementProblem(sim, _commands.LocalPlayer, _definitionId, tileX, tileY);
        }

        /// <summary>Fills _workers with the selected own units that can build (CanGather), ascending EntityId.</summary>
        /// <param name="sim">The running match.</param>
        private void CollectWorkers(Simulation sim)
        {
            _workers.Clear();
            foreach (EntityId id in _selection.Selected)
            {
                if (sim.Units.TryGet(id, out Unit unit) && unit.Owner == _commands.LocalPlayer && unit.Definition.Economy.CanGather)
                {
                    _workers.Add(unit);
                }
            }
            _workers.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        /// <summary>The selected worker nearest the tile centre (squared distance in fixed point), ties lowest EntityId.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="tileX">Tile X. Unit: tiles.</param>
        /// <param name="tileY">Tile Y. Unit: tiles.</param>
        private Unit NearestWorker(Simulation sim, int tileX, int tileY)
        {
            Unit best = null;
            Fix64 bestDistance = Fix64.Zero;
            foreach (Unit worker in _workers)
            {
                // Outside the map the clamped tile's centre is close enough for choosing a worker (the order is refused anyway).
                Fix64 distance = DistanceSquared(sim, worker, tileX, tileY);
                // _workers is in ascending ID order, so a strict comparison keeps the lowest ID on a tie.
                if (best == null || distance < bestDistance)
                {
                    best = worker;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>True when any living unit's position is on the tile.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="tileX">Tile X. Unit: tiles.</param>
        /// <param name="tileY">Tile Y. Unit: tiles.</param>
        private static bool UnitOnTile(Simulation sim, int tileX, int tileY)
        {
            if (!sim.Terrain.Contains(tileX, tileY))
            {
                return false;
            }
            int tile = sim.Terrain.IndexOf(tileX, tileY);
            foreach (Unit unit in sim.Units.All)
            {
                if (sim.Terrain.TileOf(unit.Position) == tile)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Shows a faint ghost on every pending site of the local player: each active order's tile and each queued tile.</summary>
        /// <param name="sim">The running match.</param>
        private void DrawPendingSites(Simulation sim)
        {
            // Ghosts used this frame. Range: 0 upward.
            int used = 0;
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner != _commands.LocalPlayer || unit.BuildState == BuildOrderState.None)
                {
                    continue;
                }
                PlaceGhost(PendingGhost(used++), sim, unit.BuildTileX, unit.BuildTileY, pendingColor);
                foreach (int tile in unit.BuildQueue)
                {
                    PlaceGhost(PendingGhost(used++), sim, tile % sim.Terrain.Width, tile / sim.Terrain.Width, pendingColor);
                }
            }
            // Real time now, for the skipped-tile flashes. Unit: seconds.
            float now = Time.unscaledTime;
            _flashes.RemoveAll(flash => flash.Until <= now);
            foreach ((int x, int y, float _) in _flashes)
            {
                PlaceGhost(PendingGhost(used++), sim, x, y, invalidColor);
            }
            for (int i = used; i < _pending.Count; i++)
            {
                _pending[i].gameObject.SetActive(false);
            }
        }

        /// <summary>The cursor ghost, created on first use.</summary>
        private Renderer EnsureGhost()
        {
            if (_ghost == null)
            {
                _ghost = CreateGhost("Placement Ghost");
            }
            return _ghost;
        }

        /// <summary>Pending-site ghost number <paramref name="index"/>, created when the pool is too small.</summary>
        /// <param name="index">Pool index. Range: 0 to the pool size.</param>
        private Renderer PendingGhost(int index)
        {
            if (index == _pending.Count)
            {
                _pending.Add(CreateGhost("Pending Site " + index));
            }
            return _pending[index];
        }

        /// <summary>Creates a translucent, collider-free cube with its own material.</summary>
        /// <param name="ghostName">Object name in the Hierarchy.</param>
        private static Renderer CreateGhost(string ghostName)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = ghostName;
            Destroy(cube.GetComponent<Collider>());
            Renderer renderer = cube.GetComponent<Renderer>();
            // Sprites/Default is always included in builds, unlit and alpha blended, so the ghost is see-through.
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            return renderer;
        }

        /// <summary>Puts a ghost on a tile at the terrain height and tints it.</summary>
        /// <param name="ghost">The ghost cube.</param>
        /// <param name="sim">The running match.</param>
        /// <param name="tileX">Tile X. Unit: tiles.</param>
        /// <param name="tileY">Tile Y. Unit: tiles.</param>
        /// <param name="color">Tint, RGBA 0 to 1.</param>
        private void PlaceGhost(Renderer ghost, Simulation sim, int tileX, int tileY, Color color)
        {
            // Elevation layer of the tile; 0 outside the map. Unit: layers.
            int layer = sim.Terrain.Contains(tileX, tileY) ? sim.Terrain.At(tileX, tileY).Height : 0;
            ghost.transform.position = new Vector3(tileX + 0.5f, layer * _commands.Host.LayerHeightWorldUnits + GhostHeight * 0.5f, tileY + 0.5f);
            ghost.transform.localScale = new Vector3(0.96f, GhostHeight, 0.96f);
            ghost.material.color = color;
            ghost.gameObject.SetActive(true);
        }
    }
}

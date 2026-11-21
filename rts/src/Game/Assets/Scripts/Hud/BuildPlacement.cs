// rts/src/Game/Assets/Scripts/Hud/BuildPlacement.cs
// Structure placement with a ghost (player_interface.md UI-3). Begin(definition) is called by the build menu of the command card;
// while active the mode holds PointerCapture: a ghost cube follows the tile under the cursor, a left click sends
// BuildStructureCommand(worker, definition, tile) through LocalCommands for the selected worker nearest the tile centre (ties lowest
// EntityId), holding Shift keeps the mode armed, and Esc or a right click cancels. The ghost is red with the reason when the tile
// has a placement problem or the structure misses a prerequisite, amber when a unit stands on the tile, green otherwise; these
// are hints only and the simulation decides. Every frame it also draws a faint ghost for every pending build site of the local
// player (active orders and queued tiles). Presentation only: nothing here changes the simulation except through the command.
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Game.Controls;
using Rts.Game.UnitSelection;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Economy;
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

        /// <summary>Reason text next to the cursor while the ghost is red. Range: non-null after Awake.</summary>
        private Text _reason;

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
            PointerCapture.Release(this);
            if (_ghost != null)
            {
                _ghost.gameObject.SetActive(false);
            }
            _reason.text = "";
        }

        /// <summary>Creates the cursor reason text under the HUD canvas.</summary>
        private void Awake()
        {
            _reason = HudBuild.Text(transform, "Placement Reason", Vector2.zero, Vector2.zero, new Vector2(420f, 30f), 20, TextAnchor.LowerLeft);
        }

        /// <summary>Releases the pointer and destroys the ghosts.</summary>
        private void OnDestroy()
        {
            PointerCapture.Release(this);
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
            if (camera == null || !_selection.TryPickGround(camera.ScreenPointToRay(GameInput.mousePosition), out Vector3 point))
            {
                if (_ghost != null)
                {
                    _ghost.gameObject.SetActive(false);
                }
                _reason.text = "";
                return;
            }
            // Tile under the cursor. Unit: tiles.
            int tileX = Mathf.FloorToInt(point.x);
            int tileY = Mathf.FloorToInt(point.z);
            Unit worker = NearestWorker(sim, tileX, tileY);
            string problem = TileProblem(sim, worker, tileX, tileY);
            Color color = problem != null ? invalidColor : UnitOnTile(sim, tileX, tileY) ? occupiedColor : validColor;
            PlaceGhost(EnsureGhost(), sim, tileX, tileY, color);
            _reason.text = problem != null ? RejectionTexts.For(problem) ?? problem : "";
            _reason.rectTransform.anchoredPosition = (Vector2)GameInput.mousePosition / Mathf.Max(0.01f, GetComponent<Canvas>().scaleFactor) + new Vector2(18f, 12f);
            // True while the cursor is over a HUD panel; a click there belongs to the HUD.
            bool overHud = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (GameInput.GetMouseButtonDown(0) && !overHud)
            {
                _commands.Submit(new BuildStructureCommand(_commands.Tick, _commands.LocalPlayer, _commands.NextSequence(), worker.Id, _definitionId, tileX, tileY));
                if (!GameInput.GetKey(KeyCode.LeftShift) && !GameInput.GetKey(KeyCode.RightShift))
                {
                    Cancel();
                }
            }
        }

        /// <summary>
        /// The reason the simulation would refuse this tile, as far as public queries know: the placement problem of the tile, else a missing
        /// prerequisite. shortcut: tile_occupied, no_base and static_defence_cap are only known from the rejection; switch to
        /// EconomyRules.PlacementProblem (interface_support.md I1) once it is on main.
        /// </summary>
        /// <param name="sim">The running match.</param>
        /// <param name="worker">The worker that would build.</param>
        /// <param name="tileX">Tile X. Unit: tiles.</param>
        /// <param name="tileY">Tile Y. Unit: tiles.</param>
        private string TileProblem(Simulation sim, Unit worker, int tileX, int tileY)
        {
            string problem = EconomyRules.WallLineTileProblem(sim, worker, tileX, tileY);
            if (problem == null && sim.Content.Units.TryGetValue(_definitionId, out UnitDefinition definition)
                && EconomyRules.FirstMissingRequirement(sim, _commands.LocalPlayer, definition) != null)
            {
                problem = "missing_prerequisite";
            }
            return problem;
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
            // Tile centre; outside the map the clamped tile's centre is close enough for choosing a worker (the order is refused anyway).
            FixVec2 centre = sim.Terrain.CentreOf(sim.Terrain.IndexOf(Mathf.Clamp(tileX, 0, sim.Terrain.Width - 1), Mathf.Clamp(tileY, 0, sim.Terrain.Height - 1)));
            Unit best = null;
            Fix64 bestDistance = Fix64.Zero;
            foreach (Unit worker in _workers)
            {
                Fix64 dx = worker.Position.X - centre.X;
                Fix64 dy = worker.Position.Y - centre.Y;
                Fix64 distance = dx * dx + dy * dy;
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

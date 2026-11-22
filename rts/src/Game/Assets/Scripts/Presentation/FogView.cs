// rts/src/Game/Assets/Scripts/Presentation/FogView.cs
// Fog of war drawing (player_interface.md UI-6). The simulation computes vision (architecture rule 7); this only draws it for the
// local player's team: one translucent quad per tile laid on the terrain, clear where the team sees the tile now
// (Vision.IsTileVisible), dimmed where it has explored it before (IsTileExplored), black where it never has; and every cube of a
// unit that is not on the local team is hidden unless the team sees it (Vision.IsUnitVisibleTo). Other presentation code asks
// CanSee before drawing or picking an enemy. F8 in the editor toggles "Show All" for testing (never in a build). Lives on the
// SimulationHost object, added by AutoBootstrap. Shortcut: only tile tops are covered, so cliff faces stay visible in black fog;
// draw fogged side faces when terrain art replaces the greybox. Part of Rts.Game (presentation only).
using Rts.Game.Bootstrap;
using Rts.Game.Controls;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Units;
using UnityEngine;

namespace Rts.Game.Presentation
{
    /// <summary>Draws the local team's fog of war and hides enemy cubes it cannot see.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimulationHost))]
    public sealed class FogView : MonoBehaviour
    {
        /// <summary>Overlay colour of explored tiles the team does not see now. Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color exploredColor = new Color(0f, 0f, 0f, 0.5f);

        /// <summary>Overlay colour of tiles the team has never seen. Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color unexploredColor = new Color(0.02f, 0.02f, 0.03f, 1f);

        /// <summary>Height of the overlay above each tile's surface. Unit: world units. Range: 0.005 to 0.2.</summary>
        private const float Lift = 0.03f;

        /// <summary>
        /// Editor-only debug switch: draw everything as if the whole map were visible. Static so every presentation script reads one switch.
        /// Unit: on/off. Always false in a build.
        /// </summary>
        public static bool ShowAll { get => _showAll || Observer; private set => _showAll = value; }

        /// <summary>The F8 switch's own state (editor only). Unit: on/off.</summary>
        private static bool _showAll;

        /// <summary>Observer view: everything visible, set by the host while a replay plays (also in builds). Unit: on/off.</summary>
        public static bool Observer { get; set; }

        /// <summary>The host on the same object. Range: non-null after Awake.</summary>
        private SimulationHost _host;

        /// <summary>The local command path on the same object (local player). Range: non-null after Awake.</summary>
        private LocalCommands _commands;

        /// <summary>The overlay mesh, one quad per tile. Range: null until the match runs.</summary>
        private Mesh _mesh;

        /// <summary>Vertex colours of the overlay, four per tile, reused each refresh. Range: 4 x tile count.</summary>
        private Color32[] _colors;

        /// <summary>Tick the overlay was last coloured for. Unit: ticks. Range: -1 (never) upward.</summary>
        private int _coloredTick = -1;

        /// <summary>Show All setting the overlay was last coloured with. Unit: on/off.</summary>
        private bool _coloredShowAll;

        /// <summary>True when terrain heights changed and the quads must be rebuilt. Unit: on/off.</summary>
        private bool _heightsDirty = true;

        /// <summary>True once subscribed to the host's events. Unit: on/off.</summary>
        private bool _subscribed;

        /// <summary>
        /// True when the local team may see this unit drawn: its own team's units always, others only while the team sees them (or Show All).
        /// </summary>
        /// <param name="sim">The running match.</param>
        /// <param name="team">The local player's team.</param>
        /// <param name="unit">Any living unit.</param>
        public static bool CanSee(Simulation sim, TeamId team, Unit unit)
        {
            return ShowAll || (unit.Owner.IsValid && sim.Players.TeamOf(unit.Owner) == team) || sim.Vision.IsUnitVisibleTo(team, unit);
        }

        /// <summary>True when the local team sees the tile now (or Show All).</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="team">The local player's team.</param>
        /// <param name="position">A point in tiles.</param>
        public static bool CanSeePoint(Simulation sim, TeamId team, FixVec2 position)
        {
            int tile = sim.Terrain.TileOf(position);
            return ShowAll || sim.Vision.IsTileVisible(team, tile % sim.Terrain.Width, tile / sim.Terrain.Width);
        }

        /// <summary>Finds the host and the command path.</summary>
        private void Awake()
        {
            _host = GetComponent<SimulationHost>();
            _commands = GetComponent<LocalCommands>();
            ShowAll = false;
        }

        /// <summary>Handles the editor F8 toggle and recolours the overlay after each tick.</summary>
        private void Update()
        {
#if UNITY_EDITOR
            if (GameInput.GetKeyDown(KeyCode.F8))
            {
                ShowAll = !_showAll;
                Debug.Log("[FogView] Show All " + (ShowAll ? "on" : "off") + " (editor only).");
            }
#endif
            if (_commands == null || !_commands.IsReady)
            {
                return;
            }
            Simulation sim = _host.Match;
            if (!_subscribed)
            {
                _host.EventRaised += OnSimEvent;
                _subscribed = true;
            }
            if (_mesh == null || _heightsDirty)
            {
                BuildMesh(sim);
                _coloredTick = -1;
            }
            if (sim.Clock.Tick != _coloredTick || ShowAll != _coloredShowAll)
            {
                Recolor(sim);
            }
        }

        /// <summary>Hides or shows each unit cube for the local team. Runs after the host has positioned the cubes.</summary>
        private void LateUpdate()
        {
            if (_commands == null || !_commands.IsReady)
            {
                return;
            }
            Simulation sim = _host.Match;
            TeamId team = sim.Players.TeamOf(_commands.LocalPlayer);
            foreach (SimulationHost.UnitViewRef view in _host.UnitViews)
            {
                if (view.Transform == null || !sim.Units.TryGet(view.Id, out Unit unit))
                {
                    continue;
                }
                Renderer cube = view.Transform.GetComponent<Renderer>();
                if (cube != null)
                {
                    cube.enabled = CanSee(sim, team, unit);
                }
            }
        }

        /// <summary>Unsubscribes and frees the mesh.</summary>
        private void OnDestroy()
        {
            if (_subscribed && _host != null)
            {
                _host.EventRaised -= OnSimEvent;
            }
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        /// <summary>Marks the quads for a rebuild when a tile's height changes.</summary>
        /// <param name="simEvent">An event of the tick that just ran.</param>
        private void OnSimEvent(SimEvent simEvent)
        {
            if (simEvent is TerrainChangedEvent)
            {
                _heightsDirty = true;
            }
        }

        /// <summary>(Re)builds the overlay: one quad per tile at its surface height, on a child object with an unlit alpha-blended material.</summary>
        /// <param name="sim">The running match.</param>
        private void BuildMesh(Simulation sim)
        {
            _heightsDirty = false;
            int width = sim.Terrain.Width;
            int height = sim.Terrain.Height;
            int tiles = width * height;
            Vector3[] vertices = new Vector3[tiles * 4];
            int[] triangles = new int[tiles * 6];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int tile = y * width + x;
                    // Overlay height of this tile. Unit: world units.
                    float top = sim.Terrain[tile].Height * _host.LayerHeightWorldUnits + Lift;
                    int v = tile * 4;
                    vertices[v] = new Vector3(x, top, y);
                    vertices[v + 1] = new Vector3(x, top, y + 1);
                    vertices[v + 2] = new Vector3(x + 1, top, y + 1);
                    vertices[v + 3] = new Vector3(x + 1, top, y);
                    int t = tile * 6;
                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 2;
                    triangles[t + 3] = v;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
                }
            }
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Fog Overlay" };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                GameObject overlay = new GameObject("Fog Overlay");
                overlay.AddComponent<MeshFilter>().sharedMesh = _mesh;
                MeshRenderer renderer = overlay.AddComponent<MeshRenderer>();
                // Sprites/Default is always included in builds, unlit, alpha blended and draws the vertex colour.
                renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _colors = new Color32[vertices.Length];
            _mesh.colors32 = _colors;
            _mesh.RecalculateBounds();
        }

        /// <summary>Colours each tile's quad from the local team's vision after the latest tick.</summary>
        /// <param name="sim">The running match.</param>
        private void Recolor(Simulation sim)
        {
            _coloredTick = sim.Clock.Tick;
            _coloredShowAll = ShowAll;
            TeamId team = sim.Players.TeamOf(_commands.LocalPlayer);
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 explored = exploredColor;
            Color32 unexplored = unexploredColor;
            int width = sim.Terrain.Width;
            int height = sim.Terrain.Height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 color = ShowAll || sim.Vision.IsTileVisible(team, x, y) ? clear
                        : sim.Vision.IsTileExplored(team, x, y) ? explored : unexplored;
                    int v = (y * width + x) * 4;
                    _colors[v] = color;
                    _colors[v + 1] = color;
                    _colors[v + 2] = color;
                    _colors[v + 3] = color;
                }
            }
            _mesh.colors32 = _colors;
        }
    }
}

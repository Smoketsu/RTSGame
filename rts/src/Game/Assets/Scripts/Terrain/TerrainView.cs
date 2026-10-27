// rts/src/Game/Assets/Scripts/Terrain/TerrainView.cs
// Draws the simulation's terrain grid as ONE generated Mesh with per-vertex colours: a flat quad per tile at world Y = elevation
// layer x SimulationHost.LayerHeightWorldUnits, plus a vertical "cliff" quad wherever a tile is higher than its south, west or east
// neighbour. Built once at start; rebuilt (or, for water/surface/blocker changes only, recoloured) when the simulation reports a
// TerrainChangedEvent. Part of Rts.Game (Unity side, presentation only): it reads TerrainGrid and events and writes nothing back.
//
// Colours: layer 0 dark green, layer 1 green, layer 2 light green (layers 3 to 7 progressively lighter), ramps grey, blocked tiles
// (walls, building footprints) dark, shallow and deep water blue blended over the ground colour by the grid's depth class (0 dry,
// 1 shallow, 2 deep). A faint checker shade makes individual tiles readable.
//
// Material: RtsMaterials.CreateVertexColorMaterial (our Rts/VertexColorUnlit shader, then Sprites/Default, then URP unlit shaders;
// the full chain is documented there). The mesh is generated far rows first so even the no-depth-write fallback sorts correctly for
// the fixed camera, which looks towards +Z. North-facing cliffs are not generated because the camera can never see them.
// Ramps are drawn flat at their own layer (units standing on them use the same height), so a ramp reads as a grey step, not a slope.
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Game.Presentation;
using Rts.Sim.Events;
using Rts.Sim.Terrain;
using UnityEngine;

namespace Rts.Game.TerrainDisplay
{
    /// <summary>Renders the terrain of the running match and keeps it up to date. One per scene.</summary>
    [DisallowMultipleComponent]
    public sealed class TerrainView : MonoBehaviour
    {
        /// <summary>Ground colour per elevation layer, index 0 to 7. Unit: RGBA, each channel 0 to 1. Range: 8 entries (missing entries reuse the last); defaults go from dark green (layer 0) through green (1) to light green (2) and paler.</summary>
        [SerializeField]
        private Color[] layerColors =
        {
            new Color(0.13f, 0.32f, 0.16f, 1f),
            new Color(0.27f, 0.52f, 0.24f, 1f),
            new Color(0.58f, 0.74f, 0.42f, 1f),
            new Color(0.72f, 0.80f, 0.55f, 1f),
            new Color(0.80f, 0.84f, 0.66f, 1f),
            new Color(0.86f, 0.88f, 0.76f, 1f),
            new Color(0.92f, 0.93f, 0.86f, 1f),
            new Color(0.97f, 0.97f, 0.94f, 1f),
        };

        /// <summary>Colour of ramp tiles. Unit: RGBA, each channel 0 to 1. Default mid grey.</summary>
        [SerializeField] private Color rampColor = new Color(0.55f, 0.55f, 0.57f, 1f);

        /// <summary>Colour of blocked tiles (walls, building footprints). Unit: RGBA, each channel 0 to 1. Default near black.</summary>
        [SerializeField] private Color blockedColor = new Color(0.09f, 0.09f, 0.10f, 1f);

        /// <summary>Colour blended over tiles with shallow water (depth class 1). Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color shallowWaterColor = new Color(0.36f, 0.62f, 0.95f, 1f);

        /// <summary>Colour blended over tiles with deep water (depth class 2). Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color deepWaterColor = new Color(0.08f, 0.22f, 0.62f, 1f);

        /// <summary>How strongly shallow water covers the ground colour. Unit: fraction. Range: 0 (invisible) to 1 (solid blue); default 0.65.</summary>
        [SerializeField, Range(0f, 1f)] private float shallowWaterOpacity = 0.65f;

        /// <summary>How strongly deep water covers the ground colour. Unit: fraction. Range: 0 to 1; default 0.9.</summary>
        [SerializeField, Range(0f, 1f)] private float deepWaterOpacity = 0.9f;

        /// <summary>Brightness lost on every second tile (checker pattern). Unit: fraction of brightness. Range: 0 (no pattern) to 0.3; default 0.05.</summary>
        [SerializeField, Range(0f, 0.3f)] private float checkerShade = 0.05f;

        /// <summary>Brightness of cliff faces relative to the tile above them. Unit: fraction of brightness. Range: 0.1 to 1; default 0.55.</summary>
        [SerializeField, Range(0.1f, 1f)] private float cliffBrightness = 0.55f;

        /// <summary>The host that owns the simulation. Range: null until found in Start.</summary>
        private SimulationHost _host;

        /// <summary>The grid being drawn. Range: null until Start succeeds.</summary>
        private TerrainGrid _grid;

        /// <summary>The generated mesh. Range: null until the first build; owned by this component and destroyed with it.</summary>
        private Mesh _mesh;

        /// <summary>The material from RtsMaterials. Range: null if no shader was found; owned by this component.</summary>
        private Material _material;

        /// <summary>Vertex colours of the whole mesh, kept so single tiles can be recoloured without a rebuild. Unit: RGBA bytes.</summary>
        private Color32[] _colors;

        /// <summary>Elevation layer each tile had when the mesh was built, by tile index. Unit: layer index. Range: 0 to 7.</summary>
        private byte[] _builtHeights;

        /// <summary>Ramp flag each tile had when the mesh was built, by tile index.</summary>
        private bool[] _builtRamps;

        /// <summary>Tiles whose colour (not shape) changed since the last frame. Presentation bookkeeping, so a HashSet is fine.</summary>
        private readonly HashSet<int> _dirtyColourTiles = new HashSet<int>();

        /// <summary>True when a tile's height or ramp flag changed and the whole mesh must be regenerated. Unit: on/off.</summary>
        private bool _needsRebuild;

        /// <summary>Finds the host, builds the mesh once and starts listening for terrain changes.</summary>
        private void Start()
        {
            _host = FindAnyObjectByType<SimulationHost>();
            if (_host == null || _host.Match == null)
            {
                Debug.LogWarning("[TerrainView] No running SimulationHost, so there is no terrain to draw.");
                enabled = false;
                return;
            }
            _grid = _host.Match.Terrain;
            _material = RtsMaterials.CreateVertexColorMaterial("Terrain Vertex Colours");
            if (_material == null)
            {
                enabled = false;
                return;
            }
            _mesh = new Mesh();
            _mesh.name = "Terrain Mesh";
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = gameObject.AddComponent<MeshFilter>();
            }
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            filter.sharedMesh = _mesh;
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            BuildMesh();
            _host.EventRaised += OnSimEvent;
        }

        /// <summary>Applies the changes collected from events since the last frame (at most one rebuild per frame).</summary>
        private void LateUpdate()
        {
            if (_mesh == null)
            {
                return;
            }
            if (_needsRebuild)
            {
                BuildMesh();
            }
            else if (_dirtyColourTiles.Count > 0)
            {
                foreach (int index in _dirtyColourTiles)
                {
                    PaintTile(index);
                }
                _dirtyColourTiles.Clear();
                _mesh.colors32 = _colors;
            }
        }

        /// <summary>Stops listening and frees the mesh and material.</summary>
        private void OnDestroy()
        {
            if (_host != null)
            {
                _host.EventRaised -= OnSimEvent;
            }
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        /// <summary>Listener for simulation events: notes which tiles changed. Reads only; nothing is sent back to the simulation.</summary>
        /// <param name="simEvent">An event of the tick that just ran.</param>
        private void OnSimEvent(SimEvent simEvent)
        {
            TerrainChangedEvent changed = simEvent as TerrainChangedEvent;
            if (changed == null || _builtHeights == null || changed.TileIndex < 0 || changed.TileIndex >= _builtHeights.Length)
            {
                return;
            }
            // The tile's new value from the event.
            TerrainTile tile = changed.Tile;
            if (tile.Height != _builtHeights[changed.TileIndex] || tile.IsRamp != _builtRamps[changed.TileIndex])
            {
                _needsRebuild = true;
            }
            else
            {
                _dirtyColourTiles.Add(changed.TileIndex);
            }
        }

        /// <summary>Generates the whole mesh from the grid: tile tops first (far rows first), then cliff faces.</summary>
        private void BuildMesh()
        {
            // Map size in tiles.
            int width = _grid.Width;
            int height = _grid.Height;
            // World height of one elevation layer. Unit: world units per layer.
            float layerHeight = _host.LayerHeightWorldUnits;
            // Vertex, colour and index buffers; tops use 4 vertices and 6 indices per tile, cliffs likewise per face.
            List<Vector3> vertices = new List<Vector3>(width * height * 4);
            List<Color32> colors = new List<Color32>(width * height * 4);
            List<int> triangles = new List<int>(width * height * 6);
            _builtHeights = new byte[width * height];
            _builtRamps = new bool[width * height];

            // Tops. Row 0 of the buffer is the northernmost row (y = height - 1), so the far rows are drawn first.
            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    int index = _grid.IndexOf(x, y);
                    TerrainTile tile = _grid[index];
                    _builtHeights[index] = tile.Height;
                    _builtRamps[index] = tile.IsRamp;
                    // World Y of the tile surface. Unit: world units.
                    float top = tile.Height * layerHeight;
                    Color32 colour = TileColour(index);
                    AddQuad(vertices, colors, triangles,
                        new Vector3(x, top, y), new Vector3(x, top, y + 1), new Vector3(x + 1, top, y + 1), new Vector3(x + 1, top, y), colour);
                }
            }

            // Cliff faces on the south, west and east edges (the camera never sees north faces).
            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    int index = _grid.IndexOf(x, y);
                    TerrainTile tile = _grid[index];
                    // World Y of this tile's surface. Unit: world units.
                    float top = tile.Height * layerHeight;
                    // Offsets of the three neighbours that get a face: south, west, east.
                    for (int side = 0; side < 3; side++)
                    {
                        int nx = side == 1 ? x - 1 : (side == 2 ? x + 1 : x);
                        int ny = side == 0 ? y - 1 : y;
                        if (!_grid.Contains(nx, ny))
                        {
                            continue;
                        }
                        int neighbourIndex = _grid.IndexOf(nx, ny);
                        TerrainTile neighbour = _grid[neighbourIndex];
                        if (neighbour.Height >= tile.Height)
                        {
                            continue;
                        }
                        // World Y of the lower neighbour's surface. Unit: world units.
                        float low = neighbour.Height * layerHeight;
                        Color32 cliff = CliffColour(tile, neighbour);
                        if (side == 0)
                        {
                            AddQuad(vertices, colors, triangles,
                                new Vector3(x, top, y), new Vector3(x + 1, top, y), new Vector3(x + 1, low, y), new Vector3(x, low, y), cliff);
                        }
                        else if (side == 1)
                        {
                            AddQuad(vertices, colors, triangles,
                                new Vector3(x, top, y), new Vector3(x, top, y + 1), new Vector3(x, low, y + 1), new Vector3(x, low, y), cliff);
                        }
                        else
                        {
                            AddQuad(vertices, colors, triangles,
                                new Vector3(x + 1, top, y), new Vector3(x + 1, top, y + 1), new Vector3(x + 1, low, y + 1), new Vector3(x + 1, low, y), cliff);
                        }
                    }
                }
            }

            _mesh.Clear();
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _mesh.SetVertices(vertices);
            _colors = colors.ToArray();
            _mesh.colors32 = _colors;
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            _needsRebuild = false;
            _dirtyColourTiles.Clear();
        }

        /// <summary>Adds one quad (two triangles) with a single colour. Corners go around the quad in order.</summary>
        /// <param name="vertices">Vertex buffer to append to.</param>
        /// <param name="colors">Colour buffer, one entry per vertex.</param>
        /// <param name="triangles">Index buffer to append to.</param>
        /// <param name="a">First corner, world units.</param>
        /// <param name="b">Second corner, world units.</param>
        /// <param name="c">Third corner, world units.</param>
        /// <param name="d">Fourth corner, world units.</param>
        /// <param name="colour">Colour of all four vertices, each channel 0 to 255.</param>
        private static void AddQuad(List<Vector3> vertices, List<Color32> colors, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 colour)
        {
            // Index of the first of the four new vertices.
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            colors.Add(colour);
            colors.Add(colour);
            colors.Add(colour);
            colors.Add(colour);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        /// <summary>Rewrites the four vertex colours of one tile's top quad in the cached colour array.</summary>
        /// <param name="index">Tile index. Range: 0 to tile count - 1.</param>
        private void PaintTile(int index)
        {
            // Tile position in tiles.
            int x = _grid.XOf(index);
            int y = _grid.YOf(index);
            // First vertex of the tile's top quad: tops are stored far row first, four vertices each.
            int first = ((_grid.Height - 1 - y) * _grid.Width + x) * 4;
            Color32 colour = TileColour(index);
            _colors[first] = colour;
            _colors[first + 1] = colour;
            _colors[first + 2] = colour;
            _colors[first + 3] = colour;
        }

        /// <summary>Ground colour of a tile's top face from its layer, ramp flag, blockers and water depth class.</summary>
        /// <param name="index">Tile index. Range: 0 to tile count - 1.</param>
        private Color32 TileColour(int index)
        {
            TerrainTile tile = _grid[index];
            // Colour before water and the checker shade.
            Color colour;
            if (tile.IsBlocked)
            {
                colour = blockedColor;
            }
            else
            {
                colour = tile.IsRamp ? rampColor : LayerColour(tile.Height);
                // Depth class from the grid: 0 dry, 1 shallow, 2 deep.
                int depthClass = _grid.DepthClass(index);
                if (depthClass == 1)
                {
                    colour = Color.Lerp(colour, shallowWaterColor, shallowWaterOpacity);
                }
                else if (depthClass == 2)
                {
                    colour = Color.Lerp(colour, deepWaterColor, deepWaterOpacity);
                }
            }
            if (((_grid.XOf(index) + _grid.YOf(index)) & 1) == 1)
            {
                // Brightness factor of the darker checker tiles. Unit: fraction. Range: 0.7 to 1.
                float shade = 1f - checkerShade;
                colour = new Color(colour.r * shade, colour.g * shade, colour.b * shade, 1f);
            }
            return colour;
        }

        /// <summary>Colour of a cliff face: the upper tile's ground (or ramp) colour, darkened.</summary>
        /// <param name="upper">The higher tile.</param>
        /// <param name="lower">The lower neighbour.</param>
        private Color32 CliffColour(TerrainTile upper, TerrainTile lower)
        {
            Color baseColour = (upper.IsRamp || lower.IsRamp) ? rampColor : LayerColour(upper.Height);
            return new Color(baseColour.r * cliffBrightness, baseColour.g * cliffBrightness, baseColour.b * cliffBrightness, 1f);
        }

        /// <summary>Ground colour for an elevation layer; layers beyond the configured list reuse its last entry.</summary>
        /// <param name="layer">Elevation layer. Range: 0 to 7.</param>
        private Color LayerColour(int layer)
        {
            if (layerColors == null || layerColors.Length == 0)
            {
                return Color.gray;
            }
            return layerColors[Mathf.Clamp(layer, 0, layerColors.Length - 1)];
        }
    }
}

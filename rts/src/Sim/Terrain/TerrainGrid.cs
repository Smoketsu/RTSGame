// rts/src/Sim/Terrain/TerrainGrid.cs
// The map as a grid of TerrainTile values with passability, speed and neighbour queries, and the only
// mutators that may change terrain during a match. Part of Rts.Sim.Terrain (ADR-006). Every change raises
// a TerrainChangedEvent and increments Version so pathfinding caches rebuild. Tiles are indexed
// y * Width + x and iterated in ascending index order.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Units;

namespace Rts.Sim.Terrain
{
    /// <summary>Tile grid of one match. Built by <see cref="Simulation"/> from a <see cref="MapDefinition"/>.</summary>
    public sealed class TerrainGrid
    {
        /// <summary>Map width. Unit: tiles. Range: 8 to 1024.</summary>
        public int Width { get; }

        /// <summary>Map height. Unit: tiles. Range: 8 to 1024.</summary>
        public int Height { get; }

        /// <summary>Number of tiles, Width × Height. Range: 64 to 1 048 576.</summary>
        public int TileCount => _tiles.Length;

        /// <summary>Increases by 1 on every terrain change that can alter passability or traversal cost (height, ramp, blockers, surface, depth class). Range: 0 upward. Pathfinding caches compare against it.</summary>
        public int Version { get; private set; }

        /// <summary>The map this grid was built from (resource fields, Core, spawns).</summary>
        public MapDefinition Map { get; }

        /// <summary>Backing tile array in index order.</summary>
        private readonly TerrainTile[] _tiles;

        /// <summary>Terrain tuning: multipliers and depth thresholds.</summary>
        private readonly TerrainTuning _tuning;

        /// <summary>Flood tuning: ground-unit water multipliers.</summary>
        private readonly FloodTuning _flood;

        /// <summary>The eight neighbour offsets in the fixed order N, E, S, W, NE, SE, SW, NW (y grows north). Index into both arrays with the same direction number.</summary>
        public static readonly int[] NeighbourDx = { 0, 1, 0, -1, 1, 1, -1, -1 };

        /// <summary>Y offsets matching <see cref="NeighbourDx"/>.</summary>
        public static readonly int[] NeighbourDy = { 1, 0, -1, 0, 1, -1, -1, 1 };

        /// <summary>Number of neighbour directions (4 orthogonal then 4 diagonal).</summary>
        public const int DirectionCount = 8;

        /// <summary>Builds the grid from map data. Water starts at the map's authored depth (0 in Phase 1 maps).</summary>
        /// <param name="map">Validated map definition.</param>
        /// <param name="tuning">Terrain tuning.</param>
        /// <param name="flood">Flood tuning.</param>
        public TerrainGrid(MapDefinition map, TerrainTuning tuning, FloodTuning flood)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _flood = flood ?? throw new ArgumentNullException(nameof(flood));
            Width = map.WidthTiles;
            Height = map.HeightTiles;
            _tiles = new TerrainTile[Width * Height];
            // Tile index being created. Unit: tile index. Range: 0 to Width * Height - 1.
            for (int index = 0; index < _tiles.Length; index++)
            {
                _tiles[index] = new TerrainTile(map.HeightAt(index), map.IsRampAt(index), Fix64.Zero, TeamId.None, SurfaceState.Dry, TileBlockers.None);
            }
            Version = 0;
        }

        /// <summary>Tile index of (x, y). Caller guarantees the coordinates are inside the map.</summary>
        public int IndexOf(int x, int y)
        {
            return y * Width + x;
        }

        /// <summary>X coordinate of a tile index. Range: 0 to Width - 1.</summary>
        public int XOf(int index)
        {
            return index % Width;
        }

        /// <summary>Y coordinate of a tile index. Range: 0 to Height - 1.</summary>
        public int YOf(int index)
        {
            return index / Width;
        }

        /// <summary>True when (x, y) lies inside the map.</summary>
        public bool Contains(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        /// <summary>The tile at an index.</summary>
        public TerrainTile this[int index] => _tiles[index];

        /// <summary>The tile at (x, y); caller guarantees it is inside the map.</summary>
        public TerrainTile At(int x, int y)
        {
            return _tiles[IndexOf(x, y)];
        }

        /// <summary>
        /// Tile index containing a position, clamped into the map so a position on the far edge or slightly outside
        /// still maps to a tile. Positions are in tiles; tile (x, y) covers [x, x+1).
        /// </summary>
        public int TileOf(FixVec2 position)
        {
            // Floor of each component, clamped to the valid tile range.
            int x = Math.Min(Math.Max(position.X.FloorToInt(), 0), Width - 1);
            // Tile row of the position, clamped. Unit: tiles. Range: 0 to Height - 1.
            int y = Math.Min(Math.Max(position.Y.FloorToInt(), 0), Height - 1);
            return IndexOf(x, y);
        }

        /// <summary>Centre point of a tile in tiles, for example (3.5, 7.5) for tile (3, 7).</summary>
        public FixVec2 CentreOf(int index)
        {
            return new FixVec2(Fix64.FromInt(XOf(index)) + Fix64.Half, Fix64.FromInt(YOf(index)) + Fix64.Half);
        }

        /// <summary>Clamps a position so it stays inside the map (just inside the far edges).</summary>
        public FixVec2 ClampToMap(FixVec2 position)
        {
            // Largest allowed coordinate: one step inside the far edge so FloorToInt stays a valid tile.
            Fix64 maxX = Fix64.FromInt(Width) - Fix64.Epsilon;
            // Largest allowed Y coordinate. Unit: tiles (fixed-point). Range: just below Height.
            Fix64 maxY = Fix64.FromInt(Height) - Fix64.Epsilon;
            return new FixVec2(Fix64.Clamp(position.X, Fix64.Zero, maxX), Fix64.Clamp(position.Y, Fix64.Zero, maxY));
        }

        /// <summary>Neighbour index in direction <paramref name="direction"/> (0 to 7), or -1 when off the map.</summary>
        public int Neighbour(int index, int direction)
        {
            // Column of the neighbour tile. Unit: tiles. Range: may lie outside 0 to Width - 1 before the Contains check.
            int x = XOf(index) + NeighbourDx[direction];
            // Row of the neighbour tile. Unit: tiles. Range: may lie outside 0 to Height - 1 before the Contains check.
            int y = YOf(index) + NeighbourDy[direction];
            return Contains(x, y) ? IndexOf(x, y) : -1;
        }

        /// <summary>
        /// True when a unit on <paramref name="layer"/> may step from tile <paramref name="from"/> directly to the
        /// edge- or corner-adjacent tile <paramref name="to"/>: air always; ground and water need both tiles unblocked and
        /// the height rule (same layer, or one layer apart with a ramp on either side). Diagonal steps also need both
        /// orthogonal neighbours passable, so units never cut corners.
        /// </summary>
        public bool IsStepPassable(int from, int to, MovementLayer layer)
        {
            if (layer == MovementLayer.Air)
            {
                return true;
            }
            if (!EdgePassable(from, to))
            {
                return false;
            }
            // Offsets between the tiles; a diagonal step has both non-zero.
            int dx = XOf(to) - XOf(from);
            // Row offset from the first tile to the second. Unit: tiles. Range: -1, 0 or 1.
            int dy = YOf(to) - YOf(from);
            if (dx != 0 && dy != 0)
            {
                // Tile reached by the horizontal half of a diagonal step. Unit: tile index. Range: 0 to tile count - 1.
                int viaX = IndexOf(XOf(from) + dx, YOf(from));
                // Tile reached by the vertical half of a diagonal step. Unit: tile index. Range: 0 to tile count - 1.
                int viaY = IndexOf(XOf(from), YOf(from) + dy);
                return EdgePassable(from, viaX) && EdgePassable(from, viaY) && EdgePassable(viaX, to) && EdgePassable(viaY, to);
            }
            return true;
        }

        /// <summary>The ground height rule between two adjacent tiles, ignoring diagonals.</summary>
        private bool EdgePassable(int a, int b)
        {
            // Tile value at a. Read-only copy.
            TerrainTile ta = _tiles[a];
            // Tile value at b. Read-only copy.
            TerrainTile tb = _tiles[b];
            if (ta.IsBlocked || tb.IsBlocked)
            {
                return false;
            }
            if (ta.Height == tb.Height)
            {
                return true;
            }
            // Height difference, 1 means one layer apart.
            int difference = ta.Height > tb.Height ? ta.Height - tb.Height : tb.Height - ta.Height;
            return difference == 1 && (ta.IsRamp || tb.IsRamp);
        }

        /// <summary>True when a unit on <paramref name="layer"/> may stand on the tile at all (used for goal validation).</summary>
        public bool IsStandable(int index, MovementLayer layer)
        {
            return layer == MovementLayer.Air || !_tiles[index].IsBlocked;
        }

        /// <summary>Depth class of a tile: 0 dry, 1 shallow, 2 deep, from the tuning threshold.</summary>
        public int DepthClass(int index)
        {
            return DepthClassOf(_tiles[index].WaterDepth);
        }

        /// <summary>
        /// Speed multiplier a unit on <paramref name="layer"/> gets on the tile, from terrain and flood tuning, ignoring who
        /// owns the water (every ground unit counts as an enemy of it). Used for flow-field costs, which are shared by all
        /// teams (ADR-007 amendment). Unit: multiplier of base speed. Range: 0.1 to 2. Air is always 1.
        /// </summary>
        public Fix64 SpeedMultiplier(int index, MovementLayer layer)
        {
            return SpeedMultiplier(index, layer, TeamId.None);
        }

        /// <summary>
        /// Speed multiplier for a unit of <paramref name="unitTeam"/> on <paramref name="layer"/>: ground units on water
        /// owned by their own team are not slowed (the design slows enemy ground units); water-layer units get the
        /// water-unit multipliers on any water. Unit: multiplier of base speed. Range: 0.1 to 2. Air is always 1.
        /// </summary>
        public Fix64 SpeedMultiplier(int index, MovementLayer layer, TeamId unitTeam)
        {
            if (layer == MovementLayer.Air)
            {
                return Fix64.One;
            }
            // 0 dry, 1 shallow, 2 deep; own-team water counts as dry for ground units.
            int depthClass = DepthClass(index);
            if (layer == MovementLayer.Ground && unitTeam.IsValid && _tiles[index].WaterTeam == unitTeam)
            {
                depthClass = 0;
            }
            if (layer == MovementLayer.Water)
            {
                if (depthClass == 2)
                {
                    return _tuning.WaterUnitDeepSpeedMultiplier;
                }
                return depthClass == 1 ? _tuning.WaterUnitShallowSpeedMultiplier : _tuning.WaterUnitDrySpeedMultiplier;
            }
            if (depthClass == 2)
            {
                return _flood.DeepWaterEnemySpeedMultiplier;
            }
            if (depthClass == 1)
            {
                return _flood.ShallowWaterEnemySpeedMultiplier;
            }
            switch (_tiles[index].Surface)
            {
                case SurfaceState.Raised:
                    return _tuning.GroundRaisedSpeedMultiplier;
                case SurfaceState.Scorched:
                    return _tuning.GroundScorchedSpeedMultiplier;
                default:
                    return _tuning.GroundDrySpeedMultiplier;
            }
        }

        /// <summary>Sets a tile's water depth keeping its current owner (or None when it dries). Internal: flood systems call it inside a step. Returns true when the depth changed.</summary>
        internal bool SetWaterDepth(Simulation sim, int index, Fix64 depth)
        {
            return SetWater(sim, index, depth, _tiles[index].WaterTeam);
        }

        /// <summary>
        /// Sets a tile's water depth and owning team, clamped to [0, max_water_depth]. Internal: flood systems call it
        /// inside a step. Returns true when depth or owner changed. Water of another team on the tile is replaced, never mixed.
        /// </summary>
        internal bool SetWater(Simulation sim, int index, Fix64 depth, TeamId team)
        {
            // Requested depth limited to 0 and the maximum water depth. Unit: depth units. Range: 0 to max_water_depth.
            Fix64 clamped = Fix64.Clamp(depth, Fix64.Zero, _tuning.MaxWaterDepth);
            // Owner after the change: None once the tile is dry.
            TeamId owner = clamped > Fix64.Zero ? team : TeamId.None;
            // Tile value before the change. Read-only copy.
            TerrainTile tile = _tiles[index];
            if (tile.WaterDepth == clamped && tile.WaterTeam == owner)
            {
                return false;
            }
            Replace(sim, index, new TerrainTile(tile.Height, tile.IsRamp, clamped, owner, tile.Surface, tile.Blockers));
            return true;
        }

        /// <summary>Changes a tile's elevation layer (earth raising and lowering, Phase 4). Internal; inside a step only.</summary>
        internal void SetHeight(Simulation sim, int index, byte height)
        {
            if (height > TerrainTile.MaxHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
            // Tile value before the change. Read-only copy.
            TerrainTile tile = _tiles[index];
            Replace(sim, index, new TerrainTile(height, tile.IsRamp, tile.WaterDepth, tile.WaterTeam, tile.Surface, tile.Blockers));
        }

        /// <summary>Changes a tile's surface state. Internal; inside a step only.</summary>
        internal void SetSurface(Simulation sim, int index, SurfaceState surface)
        {
            // Tile value before the change. Read-only copy.
            TerrainTile tile = _tiles[index];
            Replace(sim, index, new TerrainTile(tile.Height, tile.IsRamp, tile.WaterDepth, tile.WaterTeam, surface, tile.Blockers));
        }

        /// <summary>Adds blockers to a tile (walls, footprints). Internal; inside a step only.</summary>
        internal void AddBlockers(Simulation sim, int index, TileBlockers blockers)
        {
            // Tile value before the change. Read-only copy.
            TerrainTile tile = _tiles[index];
            Replace(sim, index, new TerrainTile(tile.Height, tile.IsRamp, tile.WaterDepth, tile.WaterTeam, tile.Surface, tile.Blockers | blockers));
        }

        /// <summary>Removes blockers from a tile. Internal; inside a step only.</summary>
        internal void RemoveBlockers(Simulation sim, int index, TileBlockers blockers)
        {
            // Tile value before the change. Read-only copy.
            TerrainTile tile = _tiles[index];
            Replace(sim, index, new TerrainTile(tile.Height, tile.IsRamp, tile.WaterDepth, tile.WaterTeam, tile.Surface, tile.Blockers & ~blockers));
        }

        /// <summary>
        /// Stores a new tile value and reports the change. Requires an active step. The version (which invalidates every
        /// cached flow field) only advances when the change can alter passability or traversal cost: height, ramp,
        /// blockers, surface or the water depth class. A depth change inside the same class (most flood ticks) keeps it.
        /// </summary>
        private void Replace(Simulation sim, int index, TerrainTile tile)
        {
            if (!sim.IsStepping)
            {
                throw new InvalidOperationException("Terrain can only change inside Simulation.Step.");
            }
            // The value being replaced, to decide whether pathing costs changed.
            TerrainTile old = _tiles[index];
            // True when any field the flow-field cost function reads has changed.
            bool affectsPathing = old.Height != tile.Height || old.IsRamp != tile.IsRamp || old.Blockers != tile.Blockers
                || old.Surface != tile.Surface || DepthClassOf(old.WaterDepth) != DepthClassOf(tile.WaterDepth);
            _tiles[index] = tile;
            if (affectsPathing)
            {
                Version++;
            }
            sim.Events.Add(new TerrainChangedEvent(sim.Clock.Tick, index, tile));
        }

        /// <summary>Depth class of a depth value: 0 dry, 1 shallow, 2 deep.</summary>
        private int DepthClassOf(Fix64 depth)
        {
            if (depth <= Fix64.Zero)
            {
                return 0;
            }
            return depth >= _tuning.DeepWaterDepthThreshold ? 2 : 1;
        }

        /// <summary>Feeds size, version and every tile in index order into the state hash.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Width);
            hasher.Write(Height);
            hasher.Write(Version);
            // Tile index being hashed. Unit: tile index. Range: 0 to Width * Height - 1.
            for (int index = 0; index < _tiles.Length; index++)
            {
                _tiles[index].WriteHash(hasher);
            }
        }
    }
}

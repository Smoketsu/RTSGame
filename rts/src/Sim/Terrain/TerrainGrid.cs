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

        /// <summary>Increases by 1 on every terrain change. Range: 0 upward. Pathfinding caches compare against it.</summary>
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
            for (int index = 0; index < _tiles.Length; index++)
            {
                _tiles[index] = new TerrainTile(map.HeightAt(index), map.IsRampAt(index), Fix64.Zero, SurfaceState.Dry, TileBlockers.None);
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
            Fix64 maxY = Fix64.FromInt(Height) - Fix64.Epsilon;
            return new FixVec2(Fix64.Clamp(position.X, Fix64.Zero, maxX), Fix64.Clamp(position.Y, Fix64.Zero, maxY));
        }

        /// <summary>Neighbour index in direction <paramref name="direction"/> (0 to 7), or -1 when off the map.</summary>
        public int Neighbour(int index, int direction)
        {
            int x = XOf(index) + NeighbourDx[direction];
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
            int dy = YOf(to) - YOf(from);
            if (dx != 0 && dy != 0)
            {
                int viaX = IndexOf(XOf(from) + dx, YOf(from));
                int viaY = IndexOf(XOf(from), YOf(from) + dy);
                return EdgePassable(from, viaX) && EdgePassable(from, viaY) && EdgePassable(viaX, to) && EdgePassable(viaY, to);
            }
            return true;
        }

        /// <summary>The ground height rule between two adjacent tiles, ignoring diagonals.</summary>
        private bool EdgePassable(int a, int b)
        {
            TerrainTile ta = _tiles[a];
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
            Fix64 depth = _tiles[index].WaterDepth;
            if (depth <= Fix64.Zero)
            {
                return 0;
            }
            return depth >= _tuning.DeepWaterDepthThreshold ? 2 : 1;
        }

        /// <summary>
        /// Speed multiplier a unit on <paramref name="layer"/> gets on the tile, from terrain and flood tuning. Unit:
        /// multiplier of base speed. Range: 0.1 to 2. Air is always 1.
        /// </summary>
        public Fix64 SpeedMultiplier(int index, MovementLayer layer)
        {
            if (layer == MovementLayer.Air)
            {
                return Fix64.One;
            }
            // 0 dry, 1 shallow, 2 deep.
            int depthClass = DepthClass(index);
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

        /// <summary>Sets a tile's water depth, clamped to [0, max_water_depth]. Internal: flood systems call it inside a step. Returns true when the depth changed.</summary>
        internal bool SetWaterDepth(Simulation sim, int index, Fix64 depth)
        {
            Fix64 clamped = Fix64.Clamp(depth, Fix64.Zero, _tuning.MaxWaterDepth);
            TerrainTile tile = _tiles[index];
            if (tile.WaterDepth == clamped)
            {
                return false;
            }
            Replace(sim, index, new TerrainTile(tile.Height, tile.IsRamp, clamped, tile.Surface, tile.Blockers));
            return true;
        }

        /// <summary>Changes a tile's elevation layer (earth raising and lowering, Phase 4). Internal; inside a step only.</summary>
        internal void SetHeight(Simulation sim, int index, byte height)
        {
            if (height > TerrainTile.MaxHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
            TerrainTile tile = _tiles[index];
            Replace(sim, index, new TerrainTile(height, tile.IsRamp, tile.WaterDepth, tile.Surface, tile.Blockers));
        }

        /// <summary>Changes a tile's surface state. Internal; inside a step only.</summary>
        internal void SetSurface(Simulation sim, int index, SurfaceState surface)
        {
            TerrainTile tile = _tiles[index];
            Replace(sim, index, new TerrainTile(tile.Height, tile.IsRamp, tile.WaterDepth, surface, tile.Blockers));
        }

        /// <summary>Adds blockers to a tile (walls, footprints). Internal; inside a step only.</summary>
        internal void AddBlockers(Simulation sim, int index, TileBlockers blockers)
        {
            TerrainTile tile = _tiles[index];
            Replace(sim, index, new TerrainTile(tile.Height, tile.IsRamp, tile.WaterDepth, tile.Surface, tile.Blockers | blockers));
        }

        /// <summary>Removes blockers from a tile. Internal; inside a step only.</summary>
        internal void RemoveBlockers(Simulation sim, int index, TileBlockers blockers)
        {
            TerrainTile tile = _tiles[index];
            Replace(sim, index, new TerrainTile(tile.Height, tile.IsRamp, tile.WaterDepth, tile.Surface, tile.Blockers & ~blockers));
        }

        /// <summary>Stores a new tile value, bumps the version and reports the change. Requires an active step.</summary>
        private void Replace(Simulation sim, int index, TerrainTile tile)
        {
            if (!sim.IsStepping)
            {
                throw new InvalidOperationException("Terrain can only change inside Simulation.Step.");
            }
            _tiles[index] = tile;
            Version++;
            sim.Events.Add(new TerrainChangedEvent(sim.Clock.Tick, index, tile));
        }

        /// <summary>Feeds size, version and every tile in index order into the state hash.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Width);
            hasher.Write(Height);
            hasher.Write(Version);
            for (int index = 0; index < _tiles.Length; index++)
            {
                _tiles[index].WriteHash(hasher);
            }
        }
    }
}

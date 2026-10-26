// rts/src/Sim/Factions/Water/FloodField.cs
// The working copy of the terrain that one flood update computes on: per-tile water drops, owner team, elevation layer and
// blocker flags, plus the per-team no-flood masks. Part of Rts.Sim.Factions.Water (spec: rts/docs/specs/flooding.md section 4).
// It is plain integer arrays with no link to the simulation, so FloodRules and its tests run on it without a Simulation; the
// mechanic fills it from TerrainGrid at the start of an update and writes the changes back through TerrainGrid.SetWater.
// Tiles are indexed y * Width + x; neighbours are visited in the order N, E, S, W (the TerrainGrid order).
using System;

namespace Rts.Sim.Factions.Water
{
    /// <summary>Integer snapshot of the terrain's water for one flood update.</summary>
    public sealed class FloodField
    {
        /// <summary>Marks a tile (or pending owner) with no water owner. Range: fixed -1; real teams are 0 to 254.</summary>
        public const int NoTeam = -1;

        /// <summary>Number of team slots in <see cref="NoFlood"/>. Range: fixed 256 (team IDs 0 to 254).</summary>
        public const int TeamSlots = 256;

        /// <summary>Map width. Unit: tiles. Range: 1 upward.</summary>
        public int Width { get; }

        /// <summary>Map height. Unit: tiles. Range: 1 upward.</summary>
        public int Height { get; }

        /// <summary>Number of tiles, Width times Height. Range: 1 upward.</summary>
        public int TileCount { get; }

        /// <summary>Drops that count as one height layer when comparing water levels. Unit: drops. Range: 128 upward (0.5 depth units).</summary>
        public int LayerDrops { get; }

        /// <summary>Most drops one tile may hold. Unit: drops. Range: 128 upward (max_water_depth times 256).</summary>
        public int MaxDrops { get; }

        /// <summary>Elevation layer per tile. Unit: layer index. Range: 0 to 7.</summary>
        public int[] LayerHeight { get; }

        /// <summary>True per tile that holds a wall. Walls block flow and are never filled.</summary>
        public bool[] Wall { get; }

        /// <summary>True per tile covered by a building footprint. Footprints stay dry.</summary>
        public bool[] Footprint { get; }

        /// <summary>Water per tile. Unit: drops. Range: 0 to <see cref="MaxDrops"/>.</summary>
        public int[] Drops { get; }

        /// <summary>Owner team of the water per tile, <see cref="NoTeam"/> when the tile holds none. Range: -1 or 0 to 254.</summary>
        public int[] Team { get; }

        /// <summary>
        /// No-flood mask per owner team (index = team ID): true where that team's water may not stand because the tile is within
        /// the dry gap of an enemy resource field or drop-off. A null entry means no restriction for that team.
        /// </summary>
        public bool[]?[] NoFlood { get; }

        /// <summary>Allocates an empty dry field.</summary>
        /// <param name="width">Map width in tiles, 1 upward.</param>
        /// <param name="height">Map height in tiles, 1 upward.</param>
        /// <param name="layerDrops">Drops per height layer, 1 upward.</param>
        /// <param name="maxDrops">Per-tile cap in drops, 1 upward.</param>
        public FloodField(int width, int height, int layerDrops, int maxDrops)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "A flood field needs at least one tile.");
            }
            if (layerDrops < 1 || maxDrops < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(layerDrops), "layerDrops and maxDrops must be at least 1.");
            }
            Width = width;
            Height = height;
            TileCount = width * height;
            LayerDrops = layerDrops;
            MaxDrops = maxDrops;
            LayerHeight = new int[TileCount];
            Wall = new bool[TileCount];
            Footprint = new bool[TileCount];
            Drops = new int[TileCount];
            Team = new int[TileCount];
            NoFlood = new bool[]?[TeamSlots];
            for (int i = 0; i < TileCount; i++)
            {
                Team[i] = NoTeam;
            }
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

        /// <summary>
        /// Neighbour of a tile in direction N, E, S, W (0 to 3; y grows north), or -1 when it lies off the map.
        /// </summary>
        /// <param name="index">Tile index.</param>
        /// <param name="direction">0 = N (x, y+1), 1 = E (x+1, y), 2 = S (x, y-1), 3 = W (x-1, y).</param>
        public int Neighbour(int index, int direction)
        {
            int x = index % Width;
            int y = index / Width;
            switch (direction)
            {
                case 0: return y + 1 < Height ? index + Width : -1;
                case 1: return x + 1 < Width ? index + 1 : -1;
                case 2: return y > 0 ? index - Width : -1;
                default: return x > 0 ? index - 1 : -1;
            }
        }

        /// <summary>Water level of a tile for downhill comparison: height times LayerDrops plus its drops. Unit: drops. Range: 0 to 7 times LayerDrops plus MaxDrops.</summary>
        public int Level(int index)
        {
            return LayerHeight[index] * LayerDrops + Drops[index];
        }

        /// <summary>True when the no-flood mask of <paramref name="team"/> forbids its water on the tile.</summary>
        public bool IsNoFlood(int team, int index)
        {
            if (team < 0 || team >= TeamSlots)
            {
                return false;
            }
            bool[]? mask = NoFlood[team];
            return mask != null && mask[index];
        }
    }
}

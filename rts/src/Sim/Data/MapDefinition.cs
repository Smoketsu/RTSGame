// rts/src/Sim/Data/MapDefinition.cs
// Immutable description of a map from data/maps/<id>.json: size, spawns, elevation layers, ramps, resource
// fields and the Core. Part of Rts.Sim.Data (ADR-006). TerrainGrid copies the authored heights and ramps at
// match start; everything else is read in place.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Which of the two resources a field yields.</summary>
    public enum ResourceKind : byte
    {
        /// <summary>The common resource mined at every base.</summary>
        Common = 0,

        /// <summary>The rare resource found at contested flank expansions.</summary>
        Rare = 1,
    }

    /// <summary>One resource field on the map.</summary>
    public sealed class ResourceField
    {
        /// <summary>Tile X of the field. Unit: tiles. Range: 0 to map width - 1.</summary>
        public int X { get; }

        /// <summary>Tile Y of the field. Unit: tiles. Range: 0 to map height - 1.</summary>
        public int Y { get; }

        /// <summary>Common or rare.</summary>
        public ResourceKind Kind { get; }

        /// <summary>Resource units the field starts with; fields deplete. Unit: resource units. Range: 1 to 1 000 000.</summary>
        public int Amount { get; }

        /// <summary>Builds a field; the loader validates the ranges.</summary>
        public ResourceField(int x, int y, ResourceKind kind, int amount)
        {
            X = x;
            Y = y;
            Kind = kind;
            Amount = amount;
        }
    }

    /// <summary>The Core at the map centre: holding it (Phase 2) picks the next Planetary Cycle.</summary>
    public sealed class CoreArea
    {
        /// <summary>Centre position in tiles.</summary>
        public FixVec2 Centre { get; }

        /// <summary>Capture radius. Unit: tiles. Range: 1 to 32.</summary>
        public Fix64 RadiusTiles { get; }

        /// <summary>Builds the area; the loader validates the ranges.</summary>
        public CoreArea(FixVec2 centre, Fix64 radiusTiles)
        {
            Centre = centre;
            RadiusTiles = radiusTiles;
        }
    }

    /// <summary>One map. Never changes during a match; live terrain state lives in TerrainGrid.</summary>
    public sealed class MapDefinition
    {
        /// <summary>Stable ID such as "greybox_1v1" (ADR-003).</summary>
        public string Id { get; }

        /// <summary>Name shown to players. Presentation only.</summary>
        public string DisplayName { get; }

        /// <summary>Map width. Unit: tiles. Range: 8 to 1024.</summary>
        public int WidthTiles { get; }

        /// <summary>Map height. Unit: tiles. Range: 8 to 1024.</summary>
        public int HeightTiles { get; }

        /// <summary>Fewest spawn points a map may have. Schema bound: a map needs two opponents.</summary>
        public const int MinSpawns = 2;

        /// <summary>Most spawn points a map may have. Schema bound, not a design limit: the design ships 2 to 4 player modes, and nothing in the simulation assumes fewer than this.</summary>
        public const int MaxSpawns = 16;

        /// <summary>Spawn points in slot order; spawn i is for player slot i. Unit: tiles from the map origin. Range: x 0 to WidthTiles, y 0 to HeightTiles. Count: <see cref="MinSpawns"/> to <see cref="MaxSpawns"/>.</summary>
        public IReadOnlyList<FixVec2> Spawns { get; }

        /// <summary>Elevation layer per tile in index order (y × width + x). Unit: layer. Range: 0 to 7. Length: width × height.</summary>
        private readonly byte[] _heights;

        /// <summary>Ramp flag per tile in index order. Length: width × height.</summary>
        private readonly bool[] _ramps;

        /// <summary>Resource fields in file order.</summary>
        public IReadOnlyList<ResourceField> ResourceFields { get; }

        /// <summary>The Core, or null for a map without one (test maps only; every shipped map has a Core).</summary>
        public CoreArea? Core { get; }

        /// <summary>
        /// Builds a map. <paramref name="heights"/> and <paramref name="ramps"/> may be null for a flat map with no
        /// ramps; otherwise their length must be width × height. The loader validates everything else.
        /// </summary>
        public MapDefinition(string id, string displayName, int widthTiles, int heightTiles, IReadOnlyList<FixVec2> spawns,
            byte[]? heights, bool[]? ramps, IReadOnlyList<ResourceField>? resourceFields, CoreArea? core)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Map id must not be empty.", nameof(id));
            }
            if (widthTiles < 8 || widthTiles > 1024 || heightTiles < 8 || heightTiles > 1024)
            {
                throw new ArgumentOutOfRangeException(nameof(widthTiles), "Map size must be 8 to 1024 tiles per side.");
            }
            if (spawns == null || spawns.Count < MinSpawns || spawns.Count > MaxSpawns)
            {
                throw new ArgumentException("A map needs " + MinSpawns + " to " + MaxSpawns + " spawns.", nameof(spawns));
            }
            // Tile count, for validating the row arrays.
            int tileCount = widthTiles * heightTiles;
            if (heights != null && heights.Length != tileCount)
            {
                throw new ArgumentException("heights must have one entry per tile.", nameof(heights));
            }
            if (ramps != null && ramps.Length != tileCount)
            {
                throw new ArgumentException("ramps must have one entry per tile.", nameof(ramps));
            }
            Id = id;
            DisplayName = displayName ?? id;
            WidthTiles = widthTiles;
            HeightTiles = heightTiles;
            Spawns = spawns;
            _heights = heights ?? new byte[tileCount];
            _ramps = ramps ?? new bool[tileCount];
            ResourceFields = resourceFields ?? new List<ResourceField>();
            Core = core;
        }

        /// <summary>Convenience constructor for a flat map with no ramps, fields or Core (tests and the Phase 0 harness map).</summary>
        public MapDefinition(string id, string displayName, int widthTiles, int heightTiles, IReadOnlyList<FixVec2> spawns)
            : this(id, displayName, widthTiles, heightTiles, spawns, null, null, null, null)
        {
        }

        /// <summary>Authored elevation layer of a tile index. Range: 0 to 7.</summary>
        public byte HeightAt(int index)
        {
            return _heights[index];
        }

        /// <summary>Authored ramp flag of a tile index.</summary>
        public bool IsRampAt(int index)
        {
            return _ramps[index];
        }
    }
}

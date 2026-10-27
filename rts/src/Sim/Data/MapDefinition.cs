// rts/src/Sim/Data/MapDefinition.cs
// Immutable description of a map from rts/data/maps/<id>.json. Part of Rts.Sim.Data.
// Phase 0 carries only size and spawn points; terrain, elevation and resource layout arrive with Phase 1 map work.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>One map: name, size in tiles and its player spawn points (<see cref="MinSpawns"/> to <see cref="MaxSpawns"/>).</summary>
    public sealed class MapDefinition
    {
        /// <summary>Stable ID, equal to the file name without ".json", such as "greybox_1v1". Lower-case letters, digits and underscores.</summary>
        public string Id { get; }

        /// <summary>Name shown to players. Presentation only.</summary>
        public string DisplayName { get; }

        /// <summary>Map width. Unit: tiles. Range: 8 to 1024.</summary>
        public int WidthTiles { get; }

        /// <summary>Map height. Unit: tiles. Range: 8 to 1024.</summary>
        public int HeightTiles { get; }

        /// <summary>Fewest spawn points a map may have. Schema bound: a map needs two opponents. Range: 2 (fixed).</summary>
        public const int MinSpawns = 2;

        /// <summary>Most spawn points a map may have. Schema bound, not a design limit: the design ships 2 to 4 player modes, and nothing in the simulation assumes fewer than this.</summary>
        public const int MaxSpawns = 16;

        /// <summary>Spawn points in slot order; spawn i is for player slot i. Unit: tiles from the map origin. Range: x 0 to WidthTiles, y 0 to HeightTiles. Count: <see cref="MinSpawns"/> to <see cref="MaxSpawns"/>.</summary>
        public IReadOnlyList<FixVec2> Spawns { get; }

        /// <summary>Builds a map; the loader has already validated ranges and reported them with file and field names.</summary>
        public MapDefinition(string id, string displayName, int widthTiles, int heightTiles, IReadOnlyList<FixVec2> spawns)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Map id must not be empty.", nameof(id));
            }
            Id = id;
            DisplayName = displayName ?? id;
            WidthTiles = widthTiles;
            HeightTiles = heightTiles;
            Spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
        }
    }
}

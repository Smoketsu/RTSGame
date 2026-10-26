// rts/src/Sim/Data/MapDefinition.cs
// Immutable description of a map from rts/data/maps/<id>.json. Part of Rts.Sim.Data.
// Phase 0 carries only size and spawn points; terrain, elevation and resource layout arrive with Phase 1 map work.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>One map: name, size in tiles and 2 to 4 player spawn points.</summary>
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

        /// <summary>Spawn points in slot order; spawn i is for player slot i. Unit: tiles from the map origin. Range: x 0 to WidthTiles, y 0 to HeightTiles. Count: 2 to 4.</summary>
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

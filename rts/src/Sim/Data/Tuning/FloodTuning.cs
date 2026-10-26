// rts/src/Sim/Data/Tuning/FloodTuning.cs
// Typed view of rts/data/tuning/flood.json. Part of Rts.Sim.Data. Water faction flooding limits and water depth effects. Read by the Water flooding and terrain depth simulation system. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Flood placement limits and water depth speed effects that the flooding and terrain depth simulation reads at match start. Loaded from "tuning/flood.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class FloodTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/flood.json";

        /// <summary>Distance floods must stop short of enemy resource fields and drop-offs. Unit: tiles. Range: 0 to 12. Source: Flood limits.</summary>
        public Fix64 EnemyResourceGapTiles { get; }

        /// <summary>Maximum share of a map's area that the lowest elevation layer may cover. Unit: fraction of map area (0.35 = 35%). Range: 0.05 to 0.6. Source: Maps / Layout rules.</summary>
        public Fix64 LowestLayerMaxMapFraction { get; }

        /// <summary>Multiplier on enemy ground speed in deep water (0.5 = half speed). Unit: multiplier of base ground speed. Range: 0.1 to 1.0. Source: Currents and depth.</summary>
        public Fix64 DeepWaterEnemySpeedMultiplier { get; }

        /// <summary>Multiplier on enemy ground speed in shallow water (0.85 = 85% speed). Unit: multiplier of base ground speed. Range: 0.1 to 1.0. Source: not in the design doc; placeholder, open question.</summary>
        public Fix64 ShallowWaterEnemySpeedMultiplier { get; }

        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public FloodTuning(JsonValue root)
        {
            root.RejectUnknownKeys("enemy_resource_gap_tiles", "lowest_layer_max_map_fraction", "deep_water_enemy_speed_multiplier", "shallow_water_enemy_speed_multiplier");
            EnemyResourceGapTiles = root.RequireFix64("enemy_resource_gap_tiles", "0", "12");
            LowestLayerMaxMapFraction = root.RequireFix64("lowest_layer_max_map_fraction", "0.05", "0.6");
            DeepWaterEnemySpeedMultiplier = root.RequireFix64("deep_water_enemy_speed_multiplier", "0.1", "1.0");
            ShallowWaterEnemySpeedMultiplier = root.RequireFix64("shallow_water_enemy_speed_multiplier", "0.1", "1.0");
        }
    }
}

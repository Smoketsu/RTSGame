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

        /// <summary>Depth that counts as one height layer when water levels are compared, so it sets how steeply "downhill" weighs a cliff against a depth difference. Must be a multiple of 1/256 and no more than max_water_depth in terrain.json. Unit: depth units. Range: 0.5 to max_water_depth (terrain.json). Source: not in the design doc; placeholder (rts/docs/specs/flooding.md section 2).</summary>
        public Fix64 DepthUnitsPerHeightLayer { get; }

        /// <summary>How often water emission, the tide and the downhill flow run. Unit: seconds (converted to whole ticks, at least 1). Range: 0.25 to 5. Source: not in the design doc; placeholder (flooding.md section 2).</summary>
        public Fix64 FloodUpdateIntervalSeconds { get; }

        /// <summary>Redistribution passes per flood update; more passes make the water flow faster. Unit: passes (count). Range: 1 to 16. Source: not in the design doc; placeholder (flooding.md section 2).</summary>
        public int FlowPassesPerUpdate { get; }

        /// <summary>Share of its high-tide volume a team may hold at low tide. Unit: fraction of high-tide volume (0.5 = half). Range: 0 to 1. Source: Tides ("at low tide it pulls back and exposes ground"); the value is a placeholder.</summary>
        public Fix64 LowTideVolumeFraction { get; }

        /// <summary>Floor on how fast water drains toward the sources when the tide falls, so a team with no sources still drains. Unit: depth-unit tiles per second. Range: 0 to 64. Source: not in the design doc; placeholder (flooding.md section 2).</summary>
        public Fix64 MinRetreatPerSecond { get; }

        /// <summary>How far from a flood source's own tile the release point may be searched (the source's footprint stays dry). Unit: tiles (Chebyshev distance, whole). Range: 0 to 6. Source: Flood limits (building footprints stay dry); the value is a placeholder.</summary>
        public int FloodSourceReleaseRadiusTiles { get; }

        /// <summary>A resource field this close to an enemy drop-off counts as an enemy resource field that floods must stay clear of. Unit: tiles (Chebyshev distance, whole). Range: 1 to 32. Source: Flood limits (stop short of enemy resource fields); the value is a placeholder.</summary>
        public int ResourceClaimRadiusTiles { get; }

        /// <summary>Healing of water-layer units standing on their own team's flooded tiles. Unit: health points per second. Range: 0 to 50. Source: Flooding ("on flooded ground, water units ... heal"); the value is a placeholder.</summary>
        public Fix64 WaterUnitHealPerSecond { get; }

        /// <summary>Time before the same caster can cast the wave again. Unit: seconds (converted to whole ticks). Range: 1 to 300. Source: Waves; the value is a placeholder.</summary>
        public Fix64 WaveCooldownSeconds { get; }

        /// <summary>How far a wave reaches from the caster along its direction. Unit: tiles. Range: 1 to 20. Source: Waves; the value is a placeholder.</summary>
        public int WaveLengthTiles { get; }

        /// <summary>Width of a wave across its direction. Unit: tiles (odd number). Range: 1 to 9. Source: Waves; the value is a placeholder.</summary>
        public int WaveWidthTiles { get; }

        /// <summary>Water added to each tile the wave covers. Must be a multiple of 1/256. Unit: depth units. Range: 0 to 8. Source: Waves ("briefly extend the flood onto dry ground"); the value is a placeholder.</summary>
        public Fix64 WaveWaterDepth { get; }

        /// <summary>Distance enemy non-air units are shoved by a wave. Unit: tiles (whole steps). Range: 0 to 6. Source: Waves ("shove enemy units back"); the value is a placeholder.</summary>
        public int WavePushTiles { get; }

        /// <summary>Time between two flood updates converted to whole simulation ticks (rounded to nearest, ADR-004), at least 1. Unit: ticks. Range: 1 to 5 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int FloodUpdateIntervalTicks(MatchClock clock)
        {
            // Rounded tick count; a very fast interval at a low tick rate could round to 0, which would never update.
            int ticks = clock.SecondsToTicks(FloodUpdateIntervalSeconds);
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>Wave cooldown converted to whole simulation ticks (rounded to nearest, ADR-004), at least 1. Unit: ticks. Range: 1 to 300 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int WaveCooldownTicks(MatchClock clock)
        {
            // Rounded tick count, at least 1 so a cast always costs a cooldown.
            int ticks = clock.SecondsToTicks(WaveCooldownSeconds);
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>True when a depth value is a whole number of drops, that is a multiple of 1/256 (the flooding maths unit, FloodMath.DropsPerDepthUnit).</summary>
        /// <param name="depth">A depth in depth units.</param>
        public static bool IsWholeDrops(Fix64 depth)
        {
            // Raw bits below the 1/256 step (Q32.32: the low 24 bits).
            return (depth.Raw & ((1L << 24) - 1)) == 0;
        }

        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public FloodTuning(JsonValue root)
        {
            root.RejectUnknownKeys("enemy_resource_gap_tiles", "lowest_layer_max_map_fraction", "deep_water_enemy_speed_multiplier", "shallow_water_enemy_speed_multiplier",
                "depth_units_per_height_layer", "flood_update_interval_seconds", "flow_passes_per_update", "low_tide_volume_fraction", "min_retreat_per_second",
                "flood_source_release_radius_tiles", "resource_claim_radius_tiles", "water_unit_heal_per_second", "wave_cooldown_seconds", "wave_length_tiles",
                "wave_width_tiles", "wave_water_depth", "wave_push_tiles");
            EnemyResourceGapTiles = root.RequireFix64("enemy_resource_gap_tiles", "0", "12");
            LowestLayerMaxMapFraction = root.RequireFix64("lowest_layer_max_map_fraction", "0.05", "0.6");
            DeepWaterEnemySpeedMultiplier = root.RequireFix64("deep_water_enemy_speed_multiplier", "0.1", "1.0");
            ShallowWaterEnemySpeedMultiplier = root.RequireFix64("shallow_water_enemy_speed_multiplier", "0.1", "1.0");
            DepthUnitsPerHeightLayer = root.RequireFix64("depth_units_per_height_layer", "0.5", "20");
            if (!IsWholeDrops(DepthUnitsPerHeightLayer))
            {
                throw root.Require("depth_units_per_height_layer").Fail("must be a multiple of 1/256");
            }
            FloodUpdateIntervalSeconds = root.RequireFix64("flood_update_interval_seconds", "0.25", "5");
            FlowPassesPerUpdate = root.RequireInt("flow_passes_per_update", 1, 16);
            LowTideVolumeFraction = root.RequireFix64("low_tide_volume_fraction", "0", "1");
            MinRetreatPerSecond = root.RequireFix64("min_retreat_per_second", "0", "64");
            FloodSourceReleaseRadiusTiles = root.RequireInt("flood_source_release_radius_tiles", 0, 6);
            ResourceClaimRadiusTiles = root.RequireInt("resource_claim_radius_tiles", 1, 32);
            WaterUnitHealPerSecond = root.RequireFix64("water_unit_heal_per_second", "0", "50");
            WaveCooldownSeconds = root.RequireFix64("wave_cooldown_seconds", "1", "300");
            WaveLengthTiles = root.RequireInt("wave_length_tiles", 1, 20);
            WaveWidthTiles = root.RequireInt("wave_width_tiles", 1, 9);
            if (WaveWidthTiles % 2 == 0)
            {
                throw root.Require("wave_width_tiles").Fail("must be an odd number (the wave is centred on its direction)");
            }
            WaveWaterDepth = root.RequireFix64("wave_water_depth", "0", "8");
            if (!IsWholeDrops(WaveWaterDepth))
            {
                throw root.Require("wave_water_depth").Fail("must be a multiple of 1/256");
            }
            WavePushTiles = root.RequireInt("wave_push_tiles", 0, 6);
        }
    }
}

// rts/src/Sim/Data/Tuning/TerrainTuning.cs
// Typed view of rts/data/tuning/terrain.json: per-layer speed multipliers, depth thresholds and pathfinder limits.
// Part of Rts.Sim.Data. Read by TerrainGrid (speed and depth classes) and FlowFieldCache (cache size).
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Immutable values of tuning/terrain.json, validated against the ranges in the file's comments.</summary>
    public sealed class TerrainTuning
    {
        /// <summary>Data path of the file this class reads, relative to the data root.</summary>
        public const string FilePath = "tuning/terrain.json";

        /// <summary>Depth at or above which water is deep; below (and above 0) is shallow. Unit: depth units (1.0 = one tile of water height). Range: 0.05 to 10. Source: Currents and depth; placeholder.</summary>
        public Fix64 DeepWaterDepthThreshold { get; }

        /// <summary>Most water a tile holds. Unit: depth units. Range: 0.5 to 20. Source: placeholder.</summary>
        public Fix64 MaxWaterDepth { get; }

        /// <summary>Ground-layer multiplier on dry ground. Unit: multiplier of base speed. Range: 0.1 to 2. Source: baseline.</summary>
        public Fix64 GroundDrySpeedMultiplier { get; }

        /// <summary>Ground-layer multiplier on raised ground. Unit: multiplier of base speed. Range: 0.1 to 2. Source: placeholder (Phase 4).</summary>
        public Fix64 GroundRaisedSpeedMultiplier { get; }

        /// <summary>Ground-layer multiplier on scorched ground. Unit: multiplier of base speed. Range: 0.1 to 2. Source: Fire faction / Scorching; placeholder (Phase 6).</summary>
        public Fix64 GroundScorchedSpeedMultiplier { get; }

        /// <summary>Water-layer multiplier on dry ground. Unit: multiplier of base speed. Range: 0.1 to 2. Source: Water faction (weak on dry land); placeholder.</summary>
        public Fix64 WaterUnitDrySpeedMultiplier { get; }

        /// <summary>Water-layer multiplier in shallow water. Unit: multiplier of base speed. Range: 0.1 to 2. Source: Flooding with tides and waves; placeholder.</summary>
        public Fix64 WaterUnitShallowSpeedMultiplier { get; }

        /// <summary>Water-layer multiplier in deep water. Unit: multiplier of base speed. Range: 0.1 to 2. Source: Flooding with tides and waves; placeholder.</summary>
        public Fix64 WaterUnitDeepSpeedMultiplier { get; }

        /// <summary>Distance from the goal at which a move order completes. Unit: tiles. Range: 0.05 to 2. Source: ADR-007; placeholder.</summary>
        public Fix64 ArrivalRadiusTiles { get; }

        /// <summary>Most cached flow fields before the oldest is evicted. Unit: fields. Range: 1 to 1024. Source: ADR-007; placeholder.</summary>
        public int MaxCachedFlowFields { get; }

        /// <summary>Reads and validates every key of the file; any unknown, missing or out-of-range key throws <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public TerrainTuning(JsonValue root)
        {
            root.RejectUnknownKeys("deep_water_depth_threshold", "max_water_depth", "ground_dry_speed_multiplier",
                "ground_raised_speed_multiplier", "ground_scorched_speed_multiplier", "water_unit_dry_speed_multiplier",
                "water_unit_shallow_speed_multiplier", "water_unit_deep_speed_multiplier", "arrival_radius_tiles",
                "max_cached_flow_fields");
            DeepWaterDepthThreshold = root.RequireFix64("deep_water_depth_threshold", "0.05", "10");
            MaxWaterDepth = root.RequireFix64("max_water_depth", "0.5", "20");
            if (!FloodTuning.IsWholeDrops(MaxWaterDepth))
            {
                throw root.Require("max_water_depth").Fail("must be a multiple of 1/256");
            }
            GroundDrySpeedMultiplier = root.RequireFix64("ground_dry_speed_multiplier", "0.1", "2");
            GroundRaisedSpeedMultiplier = root.RequireFix64("ground_raised_speed_multiplier", "0.1", "2");
            GroundScorchedSpeedMultiplier = root.RequireFix64("ground_scorched_speed_multiplier", "0.1", "2");
            WaterUnitDrySpeedMultiplier = root.RequireFix64("water_unit_dry_speed_multiplier", "0.1", "2");
            WaterUnitShallowSpeedMultiplier = root.RequireFix64("water_unit_shallow_speed_multiplier", "0.1", "2");
            WaterUnitDeepSpeedMultiplier = root.RequireFix64("water_unit_deep_speed_multiplier", "0.1", "2");
            ArrivalRadiusTiles = root.RequireFix64("arrival_radius_tiles", "0.05", "2");
            MaxCachedFlowFields = root.RequireInt("max_cached_flow_fields", 1, 1024);
        }
    }
}

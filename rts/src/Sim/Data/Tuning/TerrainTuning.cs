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

        /// <summary>Fraction of the overlap between two touching units removed per tick. Unit: fraction per tick. Range: 0 to 1 (0 = off). Source: P-003 row 11; placeholder.</summary>
        public Fix64 SeparationStrength { get; }

        /// <summary>Sideways ("keep right") push of a moving unit that walks into another. Unit: fraction of the overlap per tick. Range: 0 to 2 (0 = off). Source: P-003 row 11; placeholder.</summary>
        public Fix64 SidestepStrength { get; }

        /// <summary>Largest total push one unit receives per tick. Unit: tiles per tick. Range: 0.01 to 0.5. Source: P-003 row 11; placeholder.</summary>
        public Fix64 SeparationMaxPushTilesPerTick { get; }

        /// <summary>Distance from its goal within which a moving unit touching a nearer stopped unit ends its move. Unit: tiles. Range: 0 to 16 (0 = off). Source: P-003 row 11; placeholder.</summary>
        public Fix64 CrowdArrivalRadiusTiles { get; }

        /// <summary>Side of the neighbour-search grid cells; speed only, never results. Unit: tiles. Range: 1 to 16 (integer). Source: placeholder.</summary>
        public int SeparationCellTiles { get; }

        /// <summary>Whether air units push each other apart. Unit: boolean. Range: true or false. Source: P-003 row 11; placeholder.</summary>
        public bool AirSeparationEnabled { get; }

        /// <summary>Most cached flow fields before the oldest is evicted. Unit: fields. Range: 1 to 1024. Source: ADR-007; placeholder.</summary>
        public int MaxCachedFlowFields { get; }

        /// <summary>Reads and validates every key of the file; any unknown, missing or out-of-range key throws <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public TerrainTuning(JsonValue root)
        {
            root.RejectUnknownKeys("deep_water_depth_threshold", "max_water_depth", "ground_dry_speed_multiplier",
                "ground_raised_speed_multiplier", "ground_scorched_speed_multiplier", "water_unit_dry_speed_multiplier",
                "water_unit_shallow_speed_multiplier", "water_unit_deep_speed_multiplier", "arrival_radius_tiles",
                "separation_strength", "sidestep_strength", "separation_max_push_tiles_per_tick", "crowd_arrival_radius_tiles",
                "separation_cell_tiles", "air_separation_enabled", "max_cached_flow_fields");
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
            SeparationStrength = root.RequireFix64("separation_strength", "0", "1");
            SidestepStrength = root.RequireFix64("sidestep_strength", "0", "2");
            SeparationMaxPushTilesPerTick = root.RequireFix64("separation_max_push_tiles_per_tick", "0.01", "0.5");
            CrowdArrivalRadiusTiles = root.RequireFix64("crowd_arrival_radius_tiles", "0", "16");
            SeparationCellTiles = root.RequireInt("separation_cell_tiles", 1, 16);
            AirSeparationEnabled = root.RequireBool("air_separation_enabled");
            MaxCachedFlowFields = root.RequireInt("max_cached_flow_fields", 1, 1024);
        }
    }
}

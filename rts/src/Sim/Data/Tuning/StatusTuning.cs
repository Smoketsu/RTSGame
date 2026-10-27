// rts/src/Sim/Data/Tuning/StatusTuning.cs
// Typed view of rts/data/tuning/statuses.json. Part of Rts.Sim.Data. Shared statuses any faction can apply, Grounded and slowed. Read by the shared status simulation system. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Duration and application values for the shared Grounded status and the Spoutling drench rule that the status simulation reads at match start. Loaded from "tuning/statuses.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class StatusTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/statuses.json";

        /// <summary>How long the Grounded status lasts on a unit. Unit: seconds. Range: 0.5 to 15. Source: Factions / Grounded, a shared status.</summary>
        public Fix64 GroundedDurationSeconds { get; }

        /// <summary>Number of Spoutling drench hits that apply Grounded to the target. Unit: hits. Range: 1 to 10. Source: Factions / Water faction / Unit concepts (Spoutling).</summary>
        public int SpoutlingDrenchHitsToGround { get; }

        /// <summary>Source key the drench status entries carry, a label (not a faction or unit name) so the hash and events can tell a drench slow or ground from one applied by another source. Unit: text id. Range: fixed constant "drench", a valid status source key (1 to 64 characters of a-z, 0-9, underscore, dot).</summary>
        public const string DrenchSourceKey = "drench";

        /// <summary>How long a drench hit is remembered after the latest hit before the hit counter resets. Unit: seconds. Range: 0.5 to 30. Source: placeholder, not in the design doc (statuses spec).</summary>
        public Fix64 SpoutlingDrenchResetSeconds { get; }

        /// <summary>Speed multiplier each drench hit applies to a flyer for a while. Unit: multiplier of speed (0.75 = 25% slower). Range: 0.1 to 1. Source: placeholder, not in the design doc (statuses spec).</summary>
        public Fix64 DrenchSlowMultiplier { get; }

        /// <summary>How long the slow of one drench hit lasts. Unit: seconds. Range: 0.1 to 10. Source: placeholder, not in the design doc (statuses spec).</summary>
        public Fix64 DrenchSlowDurationSeconds { get; }

        /// <summary>How far a flyer grounded over a blocked tile searches for a free landing tile. Unit: tiles (Chebyshev rings around the flyer's tile). Range: 0 to 8; 0 means it never relocates. Source: placeholder, not in the design doc (statuses spec).</summary>
        public int GroundedLandingSearchRadiusTiles { get; }

        /// <summary>Most distinct slow sources one unit tracks at once; a new source beyond this replaces the entry that ends soonest. Unit: entries. Range: 1 to 8. Source: placeholder, not in the design doc (statuses spec).</summary>
        public int MaxSlowSourcesPerUnit { get; }

        /// <summary>How long the Grounded status lasts on a unit, converted from <see cref="GroundedDurationSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 0.5 to 15 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int GroundedDurationTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(GroundedDurationSeconds);
        }

        /// <summary>The drench hit memory <see cref="SpoutlingDrenchResetSeconds"/> as whole ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 0.5 to 30 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int DrenchResetTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(SpoutlingDrenchResetSeconds);
        }

        /// <summary>The drench slow length <see cref="DrenchSlowDurationSeconds"/> as whole ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 0.1 to 10 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int DrenchSlowDurationTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(DrenchSlowDurationSeconds);
        }

        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public StatusTuning(JsonValue root)
        {
            root.RejectUnknownKeys("grounded_duration_seconds", "spoutling_drench_hits_to_ground", "spoutling_drench_reset_seconds", "drench_slow_multiplier",
                "drench_slow_duration_seconds", "grounded_landing_search_radius_tiles", "max_slow_sources_per_unit");
            GroundedDurationSeconds = root.RequireFix64("grounded_duration_seconds", "0.5", "15");
            SpoutlingDrenchHitsToGround = root.RequireInt("spoutling_drench_hits_to_ground", 1, 10);
            SpoutlingDrenchResetSeconds = root.RequireFix64("spoutling_drench_reset_seconds", "0.5", "30");
            DrenchSlowMultiplier = root.RequireFix64("drench_slow_multiplier", "0.1", "1");
            DrenchSlowDurationSeconds = root.RequireFix64("drench_slow_duration_seconds", "0.1", "10");
            GroundedLandingSearchRadiusTiles = root.RequireInt("grounded_landing_search_radius_tiles", 0, 8);
            MaxSlowSourcesPerUnit = root.RequireInt("max_slow_sources_per_unit", 1, 8);
        }
    }
}

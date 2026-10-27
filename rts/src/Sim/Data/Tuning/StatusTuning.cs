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

        /// <summary>How long the Grounded status lasts on a unit, converted from <see cref="GroundedDurationSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 0.5 to 15 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int GroundedDurationTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(GroundedDurationSeconds);
        }
        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public StatusTuning(JsonValue root)
        {
            root.RejectUnknownKeys("grounded_duration_seconds", "spoutling_drench_hits_to_ground");
            GroundedDurationSeconds = root.RequireFix64("grounded_duration_seconds", "0.5", "15");
            SpoutlingDrenchHitsToGround = root.RequireInt("spoutling_drench_hits_to_ground", 1, 10);
        }
    }
}

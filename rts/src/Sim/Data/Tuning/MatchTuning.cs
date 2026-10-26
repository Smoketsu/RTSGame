// rts/src/Sim/Data/Tuning/MatchTuning.cs
// Typed view of rts/data/tuning/match.json. Part of Rts.Sim.Data. Match clock, tides, Planetary Cycles and the Core. Read by the match clock and Core-capture simulation systems. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Match clock, tide, Planetary Cycle and Core-holding timings that the match clock and Core-capture systems read at match start. Loaded from "tuning/match.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class MatchTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/match.json";

        /// <summary>Simulation ticks per second. Unit: ticks/second. Range: 10 to 60. Source: docs/decisions/ADR-004-tick-rate.md.</summary>
        public int TicksPerSecond { get; }

        /// <summary>How often the full state hash is computed. Unit: ticks. Range: 1 to 600. Source: docs/decisions/ADR-005-state-hash.md.</summary>
        public int StateHashIntervalTicks { get; }

        /// <summary>Shortest target match length. Unit: seconds. Range: 300 to 3600. Source: Core gameplay loop.</summary>
        public Fix64 MatchTargetMinSeconds { get; }

        /// <summary>Longest target match length. Unit: seconds. Range: 300 to 3600. Source: Core gameplay loop.</summary>
        public Fix64 MatchTargetMaxSeconds { get; }

        /// <summary>Duration of one full high-to-high tide cycle, shared by all water players. Unit: seconds. Range: 60 to 600. Source: Win condition and twists / Planetary Cycles, Factions / Water.</summary>
        public Fix64 TideCycleSeconds { get; }

        /// <summary>Time between Planetary Cycle shifts. Unit: seconds. Range: 60 to 900. Source: Win condition and twists / Planetary Cycles.</summary>
        public Fix64 PlanetaryCycleIntervalSeconds { get; }

        /// <summary>How far ahead of a shift the next Planetary Cycle is shown to players. Unit: seconds. Range: 0 to 120. Source: Win condition and twists / Planetary Cycles.</summary>
        public Fix64 PlanetaryCycleWarningSeconds { get; }

        /// <summary>Uncontested time a unit must spend in the Core radius to become Core holder. Unit: seconds. Range: 5 to 60. Source: Holding the Core.</summary>
        public Fix64 CoreCaptureSeconds { get; }

        /// <summary>Cumulative time a player must hold the Core to win in 1v1v1 and FFA. Unit: seconds. Range: 60 to 1500. Source: Holding the Core.</summary>
        public Fix64 FfaCoreWinCumulativeSeconds { get; }

        /// <summary>Shortest target match length, converted from <see cref="MatchTargetMinSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 300 to 3600 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int MatchTargetMinTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(MatchTargetMinSeconds);
        }

        /// <summary>Longest target match length, converted from <see cref="MatchTargetMaxSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 300 to 3600 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int MatchTargetMaxTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(MatchTargetMaxSeconds);
        }

        /// <summary>Duration of one full high-to-high tide cycle, shared by all water players, converted from <see cref="TideCycleSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 60 to 600 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int TideCycleTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(TideCycleSeconds);
        }

        /// <summary>Time between Planetary Cycle shifts, converted from <see cref="PlanetaryCycleIntervalSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 60 to 900 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int PlanetaryCycleIntervalTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(PlanetaryCycleIntervalSeconds);
        }

        /// <summary>How far ahead of a shift the next Planetary Cycle is shown to players, converted from <see cref="PlanetaryCycleWarningSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 0 to 120 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int PlanetaryCycleWarningTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(PlanetaryCycleWarningSeconds);
        }

        /// <summary>Uncontested time a unit must spend in the Core radius to become Core holder, converted from <see cref="CoreCaptureSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 5 to 60 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int CoreCaptureTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(CoreCaptureSeconds);
        }

        /// <summary>Cumulative time a player must hold the Core to win in 1v1v1 and FFA, converted from <see cref="FfaCoreWinCumulativeSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 60 to 1500 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int FfaCoreWinCumulativeTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(FfaCoreWinCumulativeSeconds);
        }
        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public MatchTuning(JsonValue root)
        {
            root.RejectUnknownKeys("ticks_per_second", "state_hash_interval_ticks", "match_target_min_seconds", "match_target_max_seconds", "tide_cycle_seconds", "planetary_cycle_interval_seconds", "planetary_cycle_warning_seconds", "core_capture_seconds", "ffa_core_win_cumulative_seconds");
            TicksPerSecond = root.RequireInt("ticks_per_second", 10, 60);
            StateHashIntervalTicks = root.RequireInt("state_hash_interval_ticks", 1, 600);
            MatchTargetMinSeconds = root.RequireFix64("match_target_min_seconds", "300", "3600");
            MatchTargetMaxSeconds = root.RequireFix64("match_target_max_seconds", "300", "3600");
            TideCycleSeconds = root.RequireFix64("tide_cycle_seconds", "60", "600");
            PlanetaryCycleIntervalSeconds = root.RequireFix64("planetary_cycle_interval_seconds", "60", "900");
            PlanetaryCycleWarningSeconds = root.RequireFix64("planetary_cycle_warning_seconds", "0", "120");
            CoreCaptureSeconds = root.RequireFix64("core_capture_seconds", "5", "60");
            FfaCoreWinCumulativeSeconds = root.RequireFix64("ffa_core_win_cumulative_seconds", "60", "1500");
            if (!(MatchTargetMinSeconds <= MatchTargetMaxSeconds))
            {
                throw root.Require("match_target_min_seconds").Fail("must not be greater than match_target_max_seconds");
            }
            if (!(PlanetaryCycleWarningSeconds <= PlanetaryCycleIntervalSeconds))
            {
                throw root.Require("planetary_cycle_warning_seconds").Fail("must not be longer than planetary_cycle_interval_seconds");
            }
        }
    }
}

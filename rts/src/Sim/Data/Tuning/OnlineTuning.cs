// rts/src/Sim/Data/Tuning/OnlineTuning.cs
// Typed view of rts/data/tuning/online.json. Part of Rts.Sim.Data. Pauses, disconnects and ranked rules enforced by the match rules layer. Read by the match rules layer. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Disconnect pause limits, per-player pause budget and ranked unlock threshold that the match rules layer enforces. Loaded from "tuning/online.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class OnlineTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/online.json";

        /// <summary>Longest a single disconnect pause may last. Unit: seconds. Range: 30 to 600. Source: Surrender and disconnects.</summary>
        public Fix64 DisconnectPauseMaxSeconds { get; }

        /// <summary>Number of disconnect pauses each player may trigger in a match. Unit: pauses per player per match (count). Range: 0 to 5. Source: Surrender and disconnects.</summary>
        public int DisconnectPausesPerPlayer { get; }

        /// <summary>Total voluntary pause time each player may use in a match; separate from the disconnect pause above, which has its own limit. Unit: seconds per player per match. Range: 0 to 600. Source: Surrender and disconnects.</summary>
        public Fix64 PauseBudgetSecondsPerPlayer { get; }

        /// <summary>Matches a player must finish before ranked mode unlocks. Unit: matches (count). Range: 0 to 100. Source: Game modes.</summary>
        public int RankedUnlockMatches { get; }

        /// <summary>Longest a single disconnect pause may last, converted from <see cref="DisconnectPauseMaxSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 30 to 600 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int DisconnectPauseMaxTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(DisconnectPauseMaxSeconds);
        }
        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public OnlineTuning(JsonValue root)
        {
            root.RejectUnknownKeys("disconnect_pause_max_seconds", "disconnect_pauses_per_player", "pause_budget_seconds_per_player", "ranked_unlock_matches");
            DisconnectPauseMaxSeconds = root.RequireFix64("disconnect_pause_max_seconds", "30", "600");
            DisconnectPausesPerPlayer = root.RequireInt("disconnect_pauses_per_player", 0, 5);
            PauseBudgetSecondsPerPlayer = root.RequireFix64("pause_budget_seconds_per_player", "0", "600");
            RankedUnlockMatches = root.RequireInt("ranked_unlock_matches", 0, 100);
        }
    }
}

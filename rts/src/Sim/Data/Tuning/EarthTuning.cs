// rts/src/Sim/Data/Tuning/EarthTuning.cs
// Typed view of rts/data/tuning/earth.json. Part of Rts.Sim.Data. Earth faction Tailoring prototype, Recasting and Attunement (Phase 4). Read by the Earth Tailoring simulation system. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Recasting and Attunement values for the Earth Tailoring prototype (Phase 4) that the Earth Tailoring simulation reads at match start. Loaded from "tuning/earth.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class EarthTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/earth.json";

        /// <summary>Time a Recasting takes to complete. Unit: seconds. Range: 1 to 60. Source: Factions / Earth / Recasting.</summary>
        public Fix64 RecastingSeconds { get; }

        /// <summary>Share of the unit's cost paid to Recast. Unit: fraction of cost (0.25 = 25%). Range: 0 to 1. Source: Factions / Earth / Recasting.</summary>
        public Fix64 RecastingCostFraction { get; }

        /// <summary>Resistance to a damage type gained each time the unit is hit by that type. Unit: fraction of incoming damage of that type resisted, per hit (0.01 = 1%). Range: 0 to 0.1. Source: Factions / Earth / Attunement.</summary>
        public Fix64 AttunementGainPerHit { get; }

        /// <summary>Maximum resistance Attunement can reach against one damage type. Unit: fraction of incoming damage resisted (0.30 = 30%). Range: 0 to 1. Source: Factions / Earth / Attunement.</summary>
        public Fix64 AttunementCap { get; }

        /// <summary>Resistance lost at each fade step while out of combat. Unit: fraction of incoming damage resisted (0.05 = 5 percentage points). Range: 0 to 0.3. Source: Factions / Earth / Attunement.</summary>
        public Fix64 AttunementFadeFraction { get; }

        /// <summary>Time between Attunement fade steps. Unit: seconds. Range: 1 to 60. Source: Factions / Earth / Attunement.</summary>
        public Fix64 AttunementFadeIntervalSeconds { get; }

        /// <summary>Time a Recasting takes to complete, converted from <see cref="RecastingSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 1 to 60 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int RecastingTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(RecastingSeconds);
        }

        /// <summary>Time between Attunement fade steps, converted from <see cref="AttunementFadeIntervalSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 1 to 60 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int AttunementFadeIntervalTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(AttunementFadeIntervalSeconds);
        }
        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public EarthTuning(JsonValue root)
        {
            root.RejectUnknownKeys("recasting_seconds", "recasting_cost_fraction", "attunement_gain_per_hit", "attunement_cap", "attunement_fade_fraction", "attunement_fade_interval_seconds");
            RecastingSeconds = root.RequireFix64("recasting_seconds", "1", "60");
            RecastingCostFraction = root.RequireFix64("recasting_cost_fraction", "0", "1");
            AttunementGainPerHit = root.RequireFix64("attunement_gain_per_hit", "0", "0.1");
            AttunementCap = root.RequireFix64("attunement_cap", "0", "1");
            AttunementFadeFraction = root.RequireFix64("attunement_fade_fraction", "0", "0.3");
            AttunementFadeIntervalSeconds = root.RequireFix64("attunement_fade_interval_seconds", "1", "60");
        }
    }
}

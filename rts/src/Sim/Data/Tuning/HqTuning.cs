// rts/src/Sim/Data/Tuning/HqTuning.cs
// Typed view of rts/data/tuning/hq.json. Part of Rts.Sim.Data. The mobile HQ unit (rooting, uprooting, survivability floor, Last Stand and the FFA reveal). Read by the HQ unit simulation system. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Rooting, uprooting, survivability and Last Stand values that the HQ unit simulation reads at match start. Loaded from "tuning/hq.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class HqTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/hq.json";

        /// <summary>Time the HQ takes to root in place. Unit: seconds. Range: 1 to 60. Source: The mobile HQ unit.</summary>
        public Fix64 RootSeconds { get; }

        /// <summary>Time the HQ takes to uproot and become mobile. Unit: seconds. Range: 1 to 60. Source: The mobile HQ unit.</summary>
        public Fix64 UprootSeconds { get; }

        /// <summary>Minimum time the HQ must survive against the strongest HQ-killing army. Checked by the balance harness. Unit: seconds. Range: 5 to 120. Source: Survivability floor.</summary>
        public Fix64 SurvivabilityFloorSeconds { get; }

        /// <summary>Supply value of the strongest HQ-killing army used in the survivability floor check. Checked by the balance harness. Unit: supply. Range: 1 to 200. Source: Survivability floor.</summary>
        public int SurvivabilityFloorSupply { get; }

        /// <summary>Switch that enables Last Stand for the prototype test. Unit: boolean (true or false). Range: true or false. Source: Last Stand.</summary>
        public bool LastStandEnabled { get; }

        /// <summary>Health fraction at or below which Last Stand triggers. Unit: fraction of max health (0.25 = 25%). Range: 0.05 to 0.5. Source: Last Stand.</summary>
        public Fix64 LastStandHealthFraction { get; }

        /// <summary>Time the HQ is invulnerable once Last Stand triggers. Unit: seconds. Range: 0 to 30. Source: Last Stand.</summary>
        public Fix64 LastStandInvulnerableSeconds { get; }

        /// <summary>How many times Last Stand can trigger in one match. Unit: uses per match (count). Range: 0 to 5. Source: Last Stand.</summary>
        public int LastStandUsesPerMatch { get; }

        /// <summary>Time an Uprooted HQ stays hidden in 1v1v1 and FFA before it is revealed to everyone. Unit: seconds. Range: 0 to 120. Source: No hiding.</summary>
        public Fix64 FfaUprootedRevealSeconds { get; }

        /// <summary>Time the HQ takes to root in place, converted from <see cref="RootSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 1 to 60 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int RootTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(RootSeconds);
        }

        /// <summary>Time the HQ takes to uproot and become mobile, converted from <see cref="UprootSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 1 to 60 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int UprootTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(UprootSeconds);
        }

        /// <summary>Minimum time the HQ must survive against the strongest HQ-killing army. Checked by the balance harness, converted from <see cref="SurvivabilityFloorSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 5 to 120 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int SurvivabilityFloorTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(SurvivabilityFloorSeconds);
        }

        /// <summary>Time the HQ is invulnerable once Last Stand triggers, converted from <see cref="LastStandInvulnerableSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 0 to 30 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int LastStandInvulnerableTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(LastStandInvulnerableSeconds);
        }

        /// <summary>Time an Uprooted HQ stays hidden in 1v1v1 and FFA before it is revealed to everyone, converted from <see cref="FfaUprootedRevealSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 0 to 120 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int FfaUprootedRevealTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(FfaUprootedRevealSeconds);
        }
        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public HqTuning(JsonValue root)
        {
            root.RejectUnknownKeys("root_seconds", "uproot_seconds", "survivability_floor_seconds", "survivability_floor_supply", "last_stand_enabled", "last_stand_health_fraction", "last_stand_invulnerable_seconds", "last_stand_uses_per_match", "ffa_uprooted_reveal_seconds");
            RootSeconds = root.RequireFix64("root_seconds", "1", "60");
            UprootSeconds = root.RequireFix64("uproot_seconds", "1", "60");
            SurvivabilityFloorSeconds = root.RequireFix64("survivability_floor_seconds", "5", "120");
            SurvivabilityFloorSupply = root.RequireInt("survivability_floor_supply", 1, 200);
            LastStandEnabled = root.RequireBool("last_stand_enabled");
            LastStandHealthFraction = root.RequireFix64("last_stand_health_fraction", "0.05", "0.5");
            LastStandInvulnerableSeconds = root.RequireFix64("last_stand_invulnerable_seconds", "0", "30");
            LastStandUsesPerMatch = root.RequireInt("last_stand_uses_per_match", 0, 5);
            FfaUprootedRevealSeconds = root.RequireFix64("ffa_uprooted_reveal_seconds", "0", "120");
        }
    }
}

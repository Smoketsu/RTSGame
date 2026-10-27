// rts/src/Sim/Data/Tuning/MomentumTuning.cs
// Typed view of rts/data/tuning/momentum.json. Part of Rts.Sim.Data. Skai Riders signature mechanic, Momentum (stack gain, bonuses, drain and loss rules). Read by the Skai Riders Momentum simulation system. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Momentum stack rates, caps, bonuses and loss rules that the Skai Riders Momentum simulation reads at match start. Loaded from "tuning/momentum.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class MomentumTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/momentum.json";

        /// <summary>Rate at which Momentum stacks are gained. Unit: stacks/second. Range: 0.1 to 5. Source: Factions / Skai Riders / Signature mechanic: Momentum.</summary>
        public Fix64 StackGainPerSecond { get; }

        /// <summary>Maximum number of Momentum stacks a unit can hold. Unit: stacks. Range: 1 to 10. Source: Factions / Skai Riders / Signature mechanic: Momentum.</summary>
        public int MaxStacks { get; }

        /// <summary>Speed bonus gained per Momentum stack. Unit: fraction of base speed per stack (0.04 = 4%). Range: 0 to 0.2. Source: Factions / Skai Riders / Signature mechanic: Momentum.</summary>
        public Fix64 SpeedBonusPerStack { get; }

        /// <summary>Damage bonus gained per Momentum stack. Unit: fraction of base damage per stack (0.04 = 4%). Range: 0 to 0.2. Source: Factions / Skai Riders / Signature mechanic: Momentum.</summary>
        public Fix64 DamageBonusPerStack { get; }

        /// <summary>Time after stack gain stops before Momentum starts to drain. Unit: seconds. Range: 0 to 10. Source: Factions / Skai Riders / Signature mechanic: Momentum.</summary>
        public Fix64 DrainDelaySeconds { get; }

        /// <summary>Rate at which Momentum stacks drain once draining has started. Unit: stacks/second. Range: 0.1 to 5. Source: not in the design doc; placeholder, open question.</summary>
        public Fix64 DrainStacksPerSecond { get; }

        /// <summary>Switch: Momentum is lost when the unit becomes Grounded. Unit: boolean (true or false). Range: true or false. Source: Factions / Skai Riders / Signature mechanic: Momentum.</summary>
        public bool LostOnGrounded { get; }

        /// <summary>Switch: Momentum is lost when the unit becomes slowed. Unit: boolean (true or false). Range: true or false. Source: Factions / Skai Riders / Signature mechanic: Momentum.</summary>
        public bool LostOnSlowed { get; }

        /// <summary>Time after stack gain stops before Momentum starts to drain, converted from <see cref="DrainDelaySeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 0 to 10 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int DrainDelayTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(DrainDelaySeconds);
        }
        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public MomentumTuning(JsonValue root)
        {
            root.RejectUnknownKeys("stack_gain_per_second", "max_stacks", "speed_bonus_per_stack", "damage_bonus_per_stack", "drain_delay_seconds", "drain_stacks_per_second", "lost_on_grounded", "lost_on_slowed");
            StackGainPerSecond = root.RequireFix64("stack_gain_per_second", "0.1", "5");
            MaxStacks = root.RequireInt("max_stacks", 1, 10);
            SpeedBonusPerStack = root.RequireFix64("speed_bonus_per_stack", "0", "0.2");
            DamageBonusPerStack = root.RequireFix64("damage_bonus_per_stack", "0", "0.2");
            DrainDelaySeconds = root.RequireFix64("drain_delay_seconds", "0", "10");
            DrainStacksPerSecond = root.RequireFix64("drain_stacks_per_second", "0.1", "5");
            LostOnGrounded = root.RequireBool("lost_on_grounded");
            LostOnSlowed = root.RequireBool("lost_on_slowed");
        }
    }
}

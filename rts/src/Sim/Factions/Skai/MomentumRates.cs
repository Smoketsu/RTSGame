// rts/src/Sim/Factions/Skai/MomentumRates.cs
// The Momentum tuning converted to whole ticks and integer stacks for one match clock. Part of Rts.Sim.Factions.Skai
// (spec: rts/docs/specs/momentum.md section 2). A pure function of tuning and clock, so nothing here is hashed.
using System;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Sim.Factions.Skai
{
    /// <summary>The Momentum constants of a match: stack cap, tick counts per gain and drain step, bonuses and loss switches.</summary>
    public readonly struct MomentumRates
    {
        /// <summary>Most stacks a unit can hold. Unit: stacks. Range: 1 to 10.</summary>
        public readonly int MaxStacks;

        /// <summary>Moving ticks needed for one stack: max(1, SecondsToTicks(1 / stack_gain_per_second)). Unit: ticks. Range: 1 or more (20 at the defaults and 20 ticks per second).</summary>
        public readonly int GainTicksPerStack;

        /// <summary>Still ticks a unit waits before stacks start to drain: SecondsToTicks(drain_delay_seconds). Unit: ticks. Range: 0 or more (30 at the defaults).</summary>
        public readonly int DrainDelayTicks;

        /// <summary>Draining ticks needed to lose one stack: max(1, SecondsToTicks(1 / drain_stacks_per_second)). Unit: ticks. Range: 1 or more (20 at the defaults).</summary>
        public readonly int DrainTicksPerStack;

        /// <summary>Speed added per stack. Unit: fraction of base speed per stack. Range: 0 to 0.2 (0.04 at the defaults).</summary>
        public readonly Fix64 SpeedBonusPerStack;

        /// <summary>Damage added per stack. Unit: fraction of base damage per stack. Range: 0 to 0.2 (0.04 at the defaults).</summary>
        public readonly Fix64 DamageBonusPerStack;

        /// <summary>True when Grounded wipes all stacks.</summary>
        public readonly bool LostOnGrounded;

        /// <summary>True when Slowed wipes all stacks.</summary>
        public readonly bool LostOnSlowed;

        /// <summary>
        /// Converts the tuning for a clock. "One / value" is a Fix64 division (truncating) followed by SecondsToTicks (round
        /// to nearest), so 0.3 stacks per second gives 67 ticks at 20 ticks per second.
        /// </summary>
        /// <param name="tuning">The parsed tuning/momentum.json.</param>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public MomentumRates(MomentumTuning tuning, MatchClock clock)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }
            MaxStacks = tuning.MaxStacks;
            GainTicksPerStack = Math.Max(1, clock.SecondsToTicks(Fix64.One / tuning.StackGainPerSecond));
            DrainDelayTicks = tuning.DrainDelayTicks(clock);
            DrainTicksPerStack = Math.Max(1, clock.SecondsToTicks(Fix64.One / tuning.DrainStacksPerSecond));
            SpeedBonusPerStack = tuning.SpeedBonusPerStack;
            DamageBonusPerStack = tuning.DamageBonusPerStack;
            LostOnGrounded = tuning.LostOnGrounded;
            LostOnSlowed = tuning.LostOnSlowed;
        }
    }
}

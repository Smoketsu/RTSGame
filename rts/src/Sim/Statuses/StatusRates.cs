// rts/src/Sim/Statuses/StatusRates.cs
// The status durations converted from seconds to whole ticks, computed from the tuning and the match clock. Part of
// Rts.Sim.Statuses (spec: rts/docs/specs/statuses.md section 2: "Tick conversions at match start"). A pure function of
// tuning and clock, so it holds no state and nothing here needs hashing; StatusSystem builds it where it needs it.
using System;
using Rts.Sim.Data;

namespace Rts.Sim.Statuses
{
    /// <summary>Whole-tick versions of the status durations for one match.</summary>
    internal readonly struct StatusRates
    {
        /// <summary>Length of Grounded when the applier does not say. Unit: ticks. Range: 1 or more (0.5 to 15 seconds times the tick rate).</summary>
        public readonly int GroundedTicks;

        /// <summary>Time after the latest drench hit at which the hit counter resets. Unit: ticks. Range: 1 or more.</summary>
        public readonly int DrenchResetTicks;

        /// <summary>Length of the slow one drench hit applies. Unit: ticks. Range: 1 or more.</summary>
        public readonly int DrenchSlowTicks;

        /// <summary>Converts the three durations; a result of 0 ticks is an error because a status must last at least one tick.</summary>
        /// <param name="tuning">The statuses tuning.</param>
        /// <param name="sim">The simulation whose clock sets the tick rate.</param>
        public StatusRates(StatusTuning tuning, Simulation sim)
        {
            GroundedTicks = tuning.GroundedDurationTicks(sim.Clock);
            DrenchResetTicks = tuning.DrenchResetTicks(sim.Clock);
            DrenchSlowTicks = tuning.DrenchSlowDurationTicks(sim.Clock);
            if (GroundedTicks < 1)
            {
                throw new InvalidOperationException(StatusTuning.FilePath + ": field 'grounded_duration_seconds' must be at least one tick at " + sim.Clock.TicksPerSecond + " ticks per second.");
            }
            if (DrenchResetTicks < 1)
            {
                throw new InvalidOperationException(StatusTuning.FilePath + ": field 'spoutling_drench_reset_seconds' must be at least one tick at " + sim.Clock.TicksPerSecond + " ticks per second.");
            }
            if (DrenchSlowTicks < 1)
            {
                throw new InvalidOperationException(StatusTuning.FilePath + ": field 'drench_slow_duration_seconds' must be at least one tick at " + sim.Clock.TicksPerSecond + " ticks per second.");
            }
        }
    }
}

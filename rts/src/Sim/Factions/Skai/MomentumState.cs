// rts/src/Sim/Factions/Skai/MomentumState.cs
// The Momentum state of one unit: integer stacks plus integer tick accumulators, so no fractional stacks exist.
// Part of Rts.Sim.Factions.Skai (spec: rts/docs/specs/momentum.md section 3). Owned by MomentumMechanic, hashed by it.
namespace Rts.Sim.Factions.Skai
{
    /// <summary>Momentum progress of one eligible unit. Only <see cref="MomentumMechanic"/> changes it.</summary>
    public sealed class MomentumState
    {
        /// <summary>Stacks held. Unit: stacks. Range: 0 to MaxStacks.</summary>
        public int Stacks { get; internal set; }

        /// <summary>Moving ticks accumulated toward the next stack. Unit: ticks. Range: 0 to GainTicksPerStack - 1.</summary>
        public int GainTicks { get; internal set; }

        /// <summary>Consecutive still ticks, saturating at the drain delay. Unit: ticks. Range: 0 to DrainDelayTicks.</summary>
        public int StillTicks { get; internal set; }

        /// <summary>Draining ticks accumulated toward losing a stack. Unit: ticks. Range: 0 to DrainTicksPerStack - 1.</summary>
        public int DrainTicks { get; internal set; }
    }
}

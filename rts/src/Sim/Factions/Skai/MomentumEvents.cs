// rts/src/Sim/Factions/Skai/MomentumEvents.cs
// The events of the Momentum mechanic: a stack was gained or drained, and all stacks were lost to a status. Part of
// Rts.Sim.Factions.Skai (spec: rts/docs/specs/momentum.md section 5). The UI shows stacks from these events.
// They live in the Rts.Sim.Events namespace like every other event so hosts find them in one place.
using Rts.Sim.Core;

namespace Rts.Sim.Events
{
    /// <summary>A unit's Momentum stacks changed by one step of gain or drain.</summary>
    public sealed class MomentumChangedEvent : SimEvent
    {
        /// <summary>The unit.</summary>
        public EntityId Unit { get; }

        /// <summary>Stacks before the change. Unit: stacks. Range: 0 to MaxStacks.</summary>
        public int PreviousStacks { get; }

        /// <summary>Stacks after the change. Unit: stacks. Range: 0 to MaxStacks; differs from <see cref="PreviousStacks"/> by one.</summary>
        public int Stacks { get; }

        /// <summary>Builds the event from its fields.</summary>
        public MomentumChangedEvent(int tick, EntityId unit, int previousStacks, int stacks) : base(tick)
        {
            Unit = unit;
            PreviousStacks = previousStacks;
            Stacks = stacks;
        }

        /// <summary>Machine-readable kind name: "momentum_changed".</summary>
        public override string Kind => "momentum_changed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " momentum " + PreviousStacks + " -> " + Stacks + ".";
    }

    /// <summary>A unit lost all its Momentum stacks because it was Grounded or Slowed.</summary>
    public sealed class MomentumLostEvent : SimEvent
    {
        /// <summary>The unit.</summary>
        public EntityId Unit { get; }

        /// <summary>Stacks that were lost. Unit: stacks. Range: 1 to MaxStacks (the event is only emitted when above 0).</summary>
        public int StacksLost { get; }

        /// <summary>Why: "grounded" or "slowed" ("grounded" wins when both apply).</summary>
        public string Reason { get; }

        /// <summary>Builds the event from its fields.</summary>
        public MomentumLostEvent(int tick, EntityId unit, int stacksLost, string reason) : base(tick)
        {
            Unit = unit;
            StacksLost = stacksLost;
            Reason = reason;
        }

        /// <summary>Machine-readable kind name: "momentum_lost".</summary>
        public override string Kind => "momentum_lost";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " lost " + StacksLost + " momentum (" + Reason + ").";
    }
}

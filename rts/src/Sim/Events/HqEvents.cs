// rts/src/Sim/Events/HqEvents.cs
// The events the HQ system emits: an HQ changed state (including the end of Rooting and Uprooting) and Last Stand began.
// Part of Rts.Sim.Events. Listeners (renderer, audio, UI) read them; they never change simulation state (rule 8).
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Sim.Events
{
    /// <summary>An HQ unit moved from one <see cref="HqState"/> to another (Rooted to Uprooting, Uprooting to Uprooted, and so on).</summary>
    public sealed class HqStateChangedEvent : SimEvent
    {
        /// <summary>The HQ unit.</summary>
        public EntityId Unit { get; }

        /// <summary>State before the change.</summary>
        public HqState From { get; }

        /// <summary>State after the change.</summary>
        public HqState To { get; }

        /// <summary>Builds the event from its fields.</summary>
        public HqStateChangedEvent(int tick, EntityId unit, HqState from, HqState to) : base(tick)
        {
            Unit = unit;
            From = from;
            To = to;
        }

        /// <summary>Machine-readable kind name: "hq_state_changed".</summary>
        public override string Kind => "hq_state_changed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " HQ " + From + " -> " + To + ".";
    }

    /// <summary>An HQ's Last Stand triggered: it ignores damage for <see cref="InvulnerableTicks"/> ticks.</summary>
    public sealed class HqLastStandEvent : SimEvent
    {
        /// <summary>The HQ unit.</summary>
        public EntityId Unit { get; }

        /// <summary>Health when it triggered. Unit: health points. Range: above 0 and at most last_stand_health_fraction of max health.</summary>
        public Fix64 Health { get; }

        /// <summary>How long the invulnerability lasts. Unit: ticks. Range: 0 to 30 seconds in ticks (tuning/hq.json).</summary>
        public int InvulnerableTicks { get; }

        /// <summary>Builds the event from its fields.</summary>
        public HqLastStandEvent(int tick, EntityId unit, Fix64 health, int invulnerableTicks) : base(tick)
        {
            Unit = unit;
            Health = health;
            InvulnerableTicks = invulnerableTicks;
        }

        /// <summary>Machine-readable kind name: "hq_last_stand".</summary>
        public override string Kind => "hq_last_stand";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " HQ Last Stand at " + Health + " health for " + InvulnerableTicks + " ticks.";
    }
}

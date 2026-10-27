// rts/src/Sim/Events/CombatEvents.cs
// The events the combat system emits: a unit was damaged, a unit was killed. Part of Rts.Sim.Events.
// Per hit the order is fixed: UnitDamagedEvent when the blow lands; for a lethal blow, UnitKilledEvent and then
// UnitRemovedEvent("killed") follow at the end of the combat step, in ascending victim ID order.
using Rts.Sim.Core;

namespace Rts.Sim.Events
{
    /// <summary>One weapon hit landed on a unit.</summary>
    public sealed class UnitDamagedEvent : SimEvent
    {
        /// <summary>The unit that fired.</summary>
        public EntityId Attacker { get; }

        /// <summary>The unit that was hit.</summary>
        public EntityId Target { get; }

        /// <summary>Damage dealt after the damage table and modifiers. Unit: health points. Range: 0 or more.</summary>
        public Fix64 Damage { get; }

        /// <summary>The target's health after the hit. Unit: health points. Range: 0 to the target's max health; 0 means it dies this tick.</summary>
        public Fix64 RemainingHealth { get; }

        /// <summary>Builds the event from its fields.</summary>
        public UnitDamagedEvent(int tick, EntityId attacker, EntityId target, Fix64 damage, Fix64 remainingHealth) : base(tick)
        {
            Attacker = attacker;
            Target = target;
            Damage = damage;
            RemainingHealth = remainingHealth;
        }

        /// <summary>Machine-readable kind name: "unit_damaged".</summary>
        public override string Kind => "unit_damaged";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Attacker + " hit " + Target + " for " + Damage + " (" + RemainingHealth + " left).";
    }

    /// <summary>A unit's health reached 0 and it is being removed from the match.</summary>
    public sealed class UnitKilledEvent : SimEvent
    {
        /// <summary>The unit that died.</summary>
        public EntityId Victim { get; }

        /// <summary>The unit whose hit was lethal.</summary>
        public EntityId Killer { get; }

        /// <summary>Owner of the victim, or <see cref="PlayerId.None"/> for a neutral unit.</summary>
        public PlayerId VictimOwner { get; }

        /// <summary>Owner of the killer, or <see cref="PlayerId.None"/> for a neutral unit.</summary>
        public PlayerId KillerOwner { get; }

        /// <summary>Builds the event from its fields.</summary>
        public UnitKilledEvent(int tick, EntityId victim, EntityId killer, PlayerId victimOwner, PlayerId killerOwner) : base(tick)
        {
            Victim = victim;
            Killer = killer;
            VictimOwner = victimOwner;
            KillerOwner = killerOwner;
        }

        /// <summary>Machine-readable kind name: "unit_killed".</summary>
        public override string Kind => "unit_killed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Killer + " (" + KillerOwner + ") killed " + Victim + " (" + VictimOwner + ").";
    }
}

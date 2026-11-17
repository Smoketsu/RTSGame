// rts/src/Sim/Events/CombatEvents.cs
// The events the combat system emits: a shot was fired, a unit was damaged, a blast fell, a unit was killed, and the dome shield events (hit, down, up). Part of Rts.Sim.Events.
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

    /// <summary>A weapon fired (every shot, at fire time, before any dome event). The hit lands on <see cref="LandTick"/> unless a dome absorbs it.</summary>
    public sealed class ShotFiredEvent : SimEvent
    {
        /// <summary>The unit that fired.</summary>
        public EntityId Attacker { get; }

        /// <summary>The unit aimed at.</summary>
        public EntityId Target { get; }

        /// <summary>Tick the hit lands. Unit: ticks. Range: the firing tick up to firing tick + 10 (equal for a zero-delay weapon).</summary>
        public int LandTick { get; }

        /// <summary>True for a melee weapon (range 1.5 tiles or less).</summary>
        public bool IsMelee { get; }

        /// <summary>Builds the event from its fields.</summary>
        public ShotFiredEvent(int tick, EntityId attacker, EntityId target, int landTick, bool isMelee) : base(tick)
        {
            Attacker = attacker;
            Target = target;
            LandTick = landTick;
            IsMelee = isMelee;
        }

        /// <summary>Machine-readable kind name: "shot_fired".</summary>
        public override string Kind => "shot_fired";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Attacker + " fired at " + Target + " (lands tick " + LandTick + (IsMelee ? ", melee)." : ").");
    }

    /// <summary>A dome shield absorbed part or all of a ranged hit.</summary>
    public sealed class ShieldDomeHitEvent : SimEvent
    {
        /// <summary>The unit whose dome absorbed.</summary>
        public EntityId Dome { get; }

        /// <summary>The unit that fired.</summary>
        public EntityId Attacker { get; }

        /// <summary>The unit the shot was aimed at.</summary>
        public EntityId Target { get; }

        /// <summary>Damage absorbed. Unit: health points. Range: above 0.</summary>
        public Fix64 Absorbed { get; }

        /// <summary>Dome pool left after absorbing. Unit: health points. Range: 0 up to the dome's pool.</summary>
        public Fix64 PoolLeft { get; }

        /// <summary>Builds the event from its fields.</summary>
        public ShieldDomeHitEvent(int tick, EntityId dome, EntityId attacker, EntityId target, Fix64 absorbed, Fix64 poolLeft) : base(tick)
        {
            Dome = dome;
            Attacker = attacker;
            Target = target;
            Absorbed = absorbed;
            PoolLeft = poolLeft;
        }

        /// <summary>Machine-readable kind name: "shield_dome_hit".</summary>
        public override string Kind => "shield_dome_hit";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Dome + " dome absorbed " + Absorbed + " of " + Attacker + "'s shot at " + Target + " (" + PoolLeft + " left).";
    }

    /// <summary>A weapon's blast landed (combat_mechanics.md 1.6). Once per blast, also with 0 victims, before the victims' UnitDamagedEvents. Presentation and tools only; nothing reads it inside the simulation.</summary>
    public sealed class BlastEvent : SimEvent
    {
        /// <summary>The unit that fired (it may be gone).</summary>
        public EntityId Attacker { get; }

        /// <summary>Owner of the attacker at fire time. Range: a valid player.</summary>
        public PlayerId AttackerOwner { get; }

        /// <summary>Where the blast fell. Unit: tiles. Range: inside the map.</summary>
        public FixVec2 Centre { get; }

        /// <summary>The weapon's blast radius. Unit: tiles. Range: above 0 to 4.</summary>
        public Fix64 RadiusTiles { get; }

        /// <summary>Enemy units inside the radius that the blast reached (before dome absorption). Unit: units. Range: 0 or more.</summary>
        public int Victims { get; }

        /// <summary>Builds the event from its fields.</summary>
        public BlastEvent(int tick, EntityId attacker, PlayerId attackerOwner, FixVec2 centre, Fix64 radiusTiles, int victims) : base(tick)
        {
            Attacker = attacker;
            AttackerOwner = attackerOwner;
            Centre = centre;
            RadiusTiles = radiusTiles;
            Victims = victims;
        }

        /// <summary>Machine-readable kind name: "blast".</summary>
        public override string Kind => "blast";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Attacker + " blast at " + Centre + " radius " + RadiusTiles + " reached " + Victims + " units.";
    }

    /// <summary>A dome shield's pool reached 0 and the dome is down.</summary>
    public sealed class ShieldDomeDownEvent : SimEvent
    {
        /// <summary>The unit whose dome went down.</summary>
        public EntityId Dome { get; }

        /// <summary>Builds the event from its field.</summary>
        public ShieldDomeDownEvent(int tick, EntityId dome) : base(tick)
        {
            Dome = dome;
        }

        /// <summary>Machine-readable kind name: "shield_dome_down".</summary>
        public override string Kind => "shield_dome_down";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Dome + " dome is down.";
    }

    /// <summary>A dome shield that was down has refilled completely and is up again.</summary>
    public sealed class ShieldDomeUpEvent : SimEvent
    {
        /// <summary>The unit whose dome came back up.</summary>
        public EntityId Dome { get; }

        /// <summary>Builds the event from its field.</summary>
        public ShieldDomeUpEvent(int tick, EntityId dome) : base(tick)
        {
            Dome = dome;
        }

        /// <summary>Machine-readable kind name: "shield_dome_up".</summary>
        public override string Kind => "shield_dome_up";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Dome + " dome is up again.";
    }
}

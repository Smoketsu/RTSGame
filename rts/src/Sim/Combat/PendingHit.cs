// rts/src/Sim/Combat/PendingHit.cs
// One ranged hit in flight: fired, bound to its target, waiting for its landing tick. Part of Rts.Sim.Combat. Stored in the
// PendingHitQueue, which is hashed (P-003 row 4, rts/docs/specs/p003_rules.md A.2).
using Rts.Sim.Core;

namespace Rts.Sim.Combat
{
    /// <summary>A hit that has been fired and will land on <see cref="LandTick"/>. Immutable; damage is fixed at fire time.</summary>
    public readonly struct PendingHit
    {
        /// <summary>Tick the hit lands. Unit: ticks. Range: fire tick + 1 to fire tick + 10.</summary>
        public readonly int LandTick;

        /// <summary>Order of firing among all hits ever queued; breaks ties between hits landing on one tick. Unit: count. Range: 0 upward.</summary>
        public readonly long Sequence;

        /// <summary>The unit that fired (it may be gone when the hit lands).</summary>
        public readonly EntityId Attacker;

        /// <summary>Owner of the attacker at fire time; None for neutral.</summary>
        public readonly PlayerId AttackerOwner;

        /// <summary>The unit hit.</summary>
        public readonly EntityId Target;

        /// <summary>Final damage (after table, uphill, modifiers and dome). Unit: health points. Range: 0 or more (0 when a table multiplier or modifier is 0).</summary>
        public readonly Fix64 Damage;

        /// <summary>True when the landed hit counts as a drench hit (Spoutling).</summary>
        public readonly bool AppliesDrench;

        /// <summary>Stores every field.</summary>
        public PendingHit(int landTick, long sequence, EntityId attacker, PlayerId attackerOwner, EntityId target, Fix64 damage, bool appliesDrench)
        {
            LandTick = landTick;
            Sequence = sequence;
            Attacker = attacker;
            AttackerOwner = attackerOwner;
            Target = target;
            Damage = damage;
            AppliesDrench = appliesDrench;
        }
    }
}

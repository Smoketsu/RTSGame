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

        /// <summary>Definition ID of the attacker, used at landing to read the weapon's damage type and blast radius. Unit: definition ID. Range: "" when the hit has no blast.</summary>
        public readonly string SourceDefinitionId;

        /// <summary>Base damage each blast victim takes before its own armour (splashBase, combat_mechanics.md 1.2). Unit: health points. Range: 0 or more; 0 without a blast.</summary>
        public readonly Fix64 SplashDamage;

        /// <summary>Layers the blast reaches, decided at fire time. Unit: bit mask. Range: bit 0 ground, bit 1 water, bit 2 air; 0 without a blast.</summary>
        public readonly byte SplashLayers;

        /// <summary>Where the blast falls when the target is no longer registered at landing: the target's position at fire time. Unit: tiles. Range: inside the map; zero vector without a blast.</summary>
        public readonly FixVec2 BlastFallback;

        /// <summary>True when the hit carries a blast.</summary>
        public bool HasBlast => SourceDefinitionId.Length > 0;

        /// <summary>Stores every field. The four blast fields default to "no blast".</summary>
        public PendingHit(int landTick, long sequence, EntityId attacker, PlayerId attackerOwner, EntityId target, Fix64 damage, bool appliesDrench,
            string sourceDefinitionId = "", Fix64 splashDamage = default, byte splashLayers = 0, FixVec2 blastFallback = default)
        {
            LandTick = landTick;
            Sequence = sequence;
            Attacker = attacker;
            AttackerOwner = attackerOwner;
            Target = target;
            Damage = damage;
            AppliesDrench = appliesDrench;
            SourceDefinitionId = sourceDefinitionId;
            SplashDamage = splashDamage;
            SplashLayers = splashLayers;
            BlastFallback = blastFallback;
        }
    }
}

// rts/src/Sim/Units/WeaponDefinition.cs
// Immutable description of a unit's one weapon, parsed from the "weapon" object of a unit file. Part of
// Rts.Sim.Units. The combat system (Rts.Sim.Combat) reads it every tick; every number comes from data (rule 3).
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>One weapon: damage per hit, damage type, range, cooldown and which movement layers it can hit. Never changes during a match.</summary>
    public sealed class WeaponDefinition
    {
        /// <summary>
        /// Largest range that counts as melee. Unit: tiles (edge to edge). Range: fixed schema constant, 1.5. A schema
        /// constant (it classifies a weapon for presentation and AI), not a tuning value.
        /// </summary>
        public static readonly Fix64 MeleeMaxRangeTiles = Fix64.Parse("1.5");

        /// <summary>Damage dealt by one hit before the damage table and modifiers. Unit: health points. Range: greater than 0.</summary>
        public Fix64 Damage { get; }

        /// <summary>Name of the damage type, a key of the combat damage table (tuning/combat.json). Range: non-empty text, checked by the loader against the table.</summary>
        public string DamageType { get; }

        /// <summary>Reach measured between the two units' edges (centre distance minus both radii). Unit: tiles (fixed-point). Range: greater than 0.</summary>
        public Fix64 RangeTiles { get; }

        /// <summary>Time between two hits. Unit: seconds (fixed-point). Range: greater than 0; converted to whole ticks (at least 1) by the combat system.</summary>
        public Fix64 CooldownSeconds { get; }

        /// <summary>True when the weapon may hit units on the ground layer.</summary>
        public bool CanTargetGround { get; }

        /// <summary>True when the weapon may hit units on the water layer.</summary>
        public bool CanTargetWater { get; }

        /// <summary>True when the weapon may hit units on the air layer.</summary>
        public bool CanTargetAir { get; }

        /// <summary>True when every landed hit counts as a drench hit on an air target (Spoutling): three hits ground it, each slows it. Range: true or false; false for every other weapon. Set by the optional applies_drench key of the weapon object.</summary>
        public bool AppliesDrench { get; }

        /// <summary>True when <see cref="RangeTiles"/> is at most <see cref="MeleeMaxRangeTiles"/>.</summary>
        public bool IsMelee => RangeTiles <= MeleeMaxRangeTiles;

        /// <summary>Builds a weapon, validating every range. The loader turns a failure into an error naming the file and field.</summary>
        /// <param name="damage">Damage per hit, greater than 0.</param>
        /// <param name="damageType">Damage type name, non-empty.</param>
        /// <param name="rangeTiles">Range in tiles, greater than 0.</param>
        /// <param name="cooldownSeconds">Seconds between hits, greater than 0.</param>
        /// <param name="canTargetGround">May hit the ground layer.</param>
        /// <param name="canTargetWater">May hit the water layer.</param>
        /// <param name="canTargetAir">May hit the air layer.</param>
        /// <param name="appliesDrench">True when each landed hit calls the status system's drench rule. Default false.</param>
        public WeaponDefinition(Fix64 damage, string damageType, Fix64 rangeTiles, Fix64 cooldownSeconds,
            bool canTargetGround, bool canTargetWater, bool canTargetAir, bool appliesDrench = false)
        {
            if (damage <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "weapon damage must be greater than 0.");
            }
            if (string.IsNullOrEmpty(damageType))
            {
                throw new ArgumentException("weapon damage_type must not be empty.", nameof(damageType));
            }
            if (rangeTiles <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(rangeTiles), "weapon range_tiles must be greater than 0.");
            }
            if (cooldownSeconds <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), "weapon cooldown_seconds must be greater than 0.");
            }
            if (!canTargetGround && !canTargetWater && !canTargetAir)
            {
                throw new ArgumentException("weapon can_target must allow at least one layer.", nameof(canTargetGround));
            }
            Damage = damage;
            DamageType = damageType;
            RangeTiles = rangeTiles;
            CooldownSeconds = cooldownSeconds;
            CanTargetGround = canTargetGround;
            CanTargetWater = canTargetWater;
            CanTargetAir = canTargetAir;
            AppliesDrench = appliesDrench;
        }

        /// <summary>True when the weapon may hit a unit on <paramref name="layer"/>.</summary>
        /// <param name="layer">The target's movement layer.</param>
        public bool CanTarget(MovementLayer layer)
        {
            switch (layer)
            {
                case MovementLayer.Ground: return CanTargetGround;
                case MovementLayer.Water: return CanTargetWater;
                case MovementLayer.Air: return CanTargetAir;
                default: return false;
            }
        }

        /// <summary>Feeds every field into a hash in a fixed order (used by the content digest).</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Damage);
            hasher.Write(DamageType);
            hasher.Write(RangeTiles);
            hasher.Write(CooldownSeconds);
            hasher.Write(CanTargetGround);
            hasher.Write(CanTargetWater);
            hasher.Write(CanTargetAir);
            hasher.Write(AppliesDrench);
        }
    }
}

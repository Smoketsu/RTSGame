// rts/src/Sim/Combat/CombatRules.cs
// The combat rules a match plays by: which damage and armour types exist, the multiplier table between them, the
// acquire range bonus and the default armour type. Part of Rts.Sim.Combat. Built from data/tuning/combat.json by
// CombatTuning.CreateRules (or by hand in tests), stored on the ContentCatalog, read by CombatSystem and hashed through
// the content digest so two peers with different combat tuning diverge at tick 0. Immutable once built.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Sim.Combat
{
    /// <summary>Damage table and acquisition tuning for one match. Immutable; no tuning number is hard-coded here.</summary>
    public sealed class CombatRules
    {
        /// <summary>
        /// Rules that accept any damage type and armour type and always answer multiplier 1, with no acquire bonus. Used by
        /// catalogs built by hand (tests, tools) that never set rules, so unarmed content needs no combat data.
        /// Range: a shared immutable instance; <see cref="IsNeutral"/> is true.
        /// </summary>
        public static readonly CombatRules Neutral = new CombatRules();

        /// <summary>Damage type names in ordinal order. Empty when <see cref="IsNeutral"/>.</summary>
        private readonly SortedSet<string> _damageTypes;

        /// <summary>Armour type names in ordinal order. Empty when <see cref="IsNeutral"/>.</summary>
        private readonly SortedSet<string> _armourTypes;

        /// <summary>Multiplier for each damage type (outer key) against each armour type (inner key). Unit: fraction. Range: 0 to 3.</summary>
        private readonly SortedDictionary<string, SortedDictionary<string, Fix64>> _table;

        /// <summary>True for the permissive rules of <see cref="Neutral"/>: nothing is validated and every multiplier is 1.</summary>
        public bool IsNeutral { get; }

        /// <summary>Extra distance beyond weapon range within which an attack-moving unit chases a target. Unit: tiles (fixed-point). Range: 0 to 20.</summary>
        public Fix64 AcquireRangeBonusTiles { get; }

        /// <summary>Armour type given to units whose data names none. Range: one of the armour type names; empty when <see cref="IsNeutral"/>.</summary>
        public string DefaultArmourType { get; }

        /// <summary>Damage type names in ordinal order.</summary>
        public IReadOnlyCollection<string> DamageTypes => _damageTypes;

        /// <summary>Armour type names in ordinal order.</summary>
        public IReadOnlyCollection<string> ArmourTypes => _armourTypes;

        /// <summary>Builds the permissive rules used by <see cref="Neutral"/>.</summary>
        private CombatRules()
        {
            _damageTypes = new SortedSet<string>(StringComparer.Ordinal);
            _armourTypes = new SortedSet<string>(StringComparer.Ordinal);
            _table = new SortedDictionary<string, SortedDictionary<string, Fix64>>(StringComparer.Ordinal);
            IsNeutral = true;
            AcquireRangeBonusTiles = Fix64.Zero;
            DefaultArmourType = string.Empty;
        }

        /// <summary>
        /// Builds validated rules. Throws <see cref="ArgumentException"/> when the table is not a complete damage-type by
        /// armour-type grid or the default armour type is unknown.
        /// </summary>
        /// <param name="multipliers">Fraction for each (damage type, armour type) pair. Range: 0 to 3; one entry per pair.</param>
        /// <param name="acquireRangeBonusTiles">Extra chase distance for attack-moving units. Unit: tiles. Range: 0 to 20.</param>
        /// <param name="defaultArmourType">Armour type for units that name none. Range: a key of the inner tables.</param>
        public CombatRules(IReadOnlyDictionary<string, IReadOnlyDictionary<string, Fix64>> multipliers,
            Fix64 acquireRangeBonusTiles, string defaultArmourType)
        {
            if (multipliers == null)
            {
                throw new ArgumentNullException(nameof(multipliers));
            }
            if (acquireRangeBonusTiles < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(acquireRangeBonusTiles), "The acquire range bonus must be 0 or more.");
            }
            _damageTypes = new SortedSet<string>(StringComparer.Ordinal);
            _armourTypes = new SortedSet<string>(StringComparer.Ordinal);
            _table = new SortedDictionary<string, SortedDictionary<string, Fix64>>(StringComparer.Ordinal);
            // Armour type names found in the first row; every later row must match them exactly.
            SortedSet<string>? firstRow = null;
            foreach (KeyValuePair<string, IReadOnlyDictionary<string, Fix64>> row in multipliers)
            {
                // Copy of this damage type's row in ordinal order.
                SortedDictionary<string, Fix64> copy = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
                // Armour type names of this row.
                SortedSet<string> names = new SortedSet<string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, Fix64> cell in row.Value)
                {
                    copy.Add(cell.Key, cell.Value);
                    names.Add(cell.Key);
                }
                if (firstRow == null)
                {
                    firstRow = names;
                }
                else if (!firstRow.SetEquals(names))
                {
                    throw new ArgumentException("Damage type '" + row.Key + "' does not list the same armour types as the other rows.", nameof(multipliers));
                }
                _damageTypes.Add(row.Key);
                _table.Add(row.Key, copy);
            }
            if (firstRow == null || !firstRow.Contains(defaultArmourType))
            {
                throw new ArgumentException("The default armour type '" + defaultArmourType + "' is not one of the armour types.", nameof(defaultArmourType));
            }
            _armourTypes.UnionWith(firstRow);
            IsNeutral = false;
            AcquireRangeBonusTiles = acquireRangeBonusTiles;
            DefaultArmourType = defaultArmourType;
        }

        /// <summary>True when <paramref name="name"/> is a damage type (always true for neutral rules).</summary>
        public bool IsDamageType(string name)
        {
            return IsNeutral || _damageTypes.Contains(name);
        }

        /// <summary>True when <paramref name="name"/> is an armour type (always true for neutral rules).</summary>
        public bool IsArmourType(string name)
        {
            return IsNeutral || _armourTypes.Contains(name);
        }

        /// <summary>
        /// Fraction of a weapon's damage that gets through. Unit: fraction. Range: 0 to 3; exactly 1 for neutral rules.
        /// Throws <see cref="ArgumentException"/> for an unknown type, which <see cref="Validate"/> rules out at match start.
        /// </summary>
        /// <param name="damageType">The weapon's damage type name.</param>
        /// <param name="armourType">The target's armour type name.</param>
        public Fix64 GetMultiplier(string damageType, string armourType)
        {
            if (IsNeutral)
            {
                return Fix64.One;
            }
            if (!_table.TryGetValue(damageType, out SortedDictionary<string, Fix64>? row) || !row.TryGetValue(armourType, out Fix64 cell))
            {
                throw new ArgumentException("No multiplier for damage type '" + damageType + "' against armour type '" + armourType + "'.");
            }
            return cell;
        }

        /// <summary>
        /// Checks that every unit's armour type and weapon damage type are known to these rules. Returns the first
        /// problem (units in ordinal ID order) or null. Called by <c>ContentCatalog.Validate</c>.
        /// </summary>
        /// <param name="units">All unit definitions in ordinal ID order.</param>
        public string? Validate(IEnumerable<UnitDefinition> units)
        {
            if (IsNeutral)
            {
                return null;
            }
            foreach (UnitDefinition unit in units)
            {
                if (!_armourTypes.Contains(unit.ArmourType))
                {
                    return "Unit '" + unit.Id + "' has unknown armour type '" + unit.ArmourType + "'.";
                }
                if (unit.Weapon != null && !_damageTypes.Contains(unit.Weapon.DamageType))
                {
                    return "Unit '" + unit.Id + "' has a weapon with unknown damage type '" + unit.Weapon.DamageType + "'.";
                }
                if (unit.Hq != null && (!_armourTypes.Contains(unit.Hq.RootedArmourType) || !_armourTypes.Contains(unit.Hq.UprootedArmourType)))
                {
                    return "Unit '" + unit.Id + "' has an HQ armour type that is unknown to the damage table.";
                }
            }
            return null;
        }

        /// <summary>Feeds the whole rule set into a hash (the content digest) in a fixed order.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(IsNeutral);
            hasher.Write(AcquireRangeBonusTiles);
            hasher.Write(DefaultArmourType);
            hasher.Write(_table.Count);
            foreach (KeyValuePair<string, SortedDictionary<string, Fix64>> row in _table)
            {
                hasher.Write(row.Key);
                hasher.Write(row.Value.Count);
                foreach (KeyValuePair<string, Fix64> cell in row.Value)
                {
                    hasher.Write(cell.Key);
                    hasher.Write(cell.Value);
                }
            }
        }
    }
}

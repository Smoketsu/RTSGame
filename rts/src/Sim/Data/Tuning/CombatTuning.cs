// rts/src/Sim/Data/Tuning/CombatTuning.cs
// Typed view of rts/data/tuning/combat.json. Part of Rts.Sim.Data. Damage types, armour types, the damage multiplier
// table, the attack-move acquire bonus and the default armour type. Read by the combat system through CombatRules.
// Every value is a placeholder starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// When a key is added to the JSON file, add the property here with the same Unit, Range and Source text, or the
// loader reports the key as unknown.
using System;
using System.Collections.Generic;
using Rts.Sim.Combat;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Damage and armour type names, the multiplier table and acquisition tuning that the combat system reads. Loaded from "tuning/combat.json". Immutable.</summary>
    public sealed class CombatTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/combat.json";

        /// <summary>Most damage types or armour types the file may declare. Unit: names. Range: fixed validation bound, 8.</summary>
        private const int MaxTypeNames = 8;

        /// <summary>Damage type names in file order. Unit: list of text IDs. Range: 1 to 8 distinct names. Source: Matchups and balance / Balance philosophy (soft counters only); placeholder.</summary>
        public IReadOnlyList<string> DamageTypes { get; }

        /// <summary>Armour type names in file order. Unit: list of text IDs. Range: 1 to 8 distinct names. Source: Matchups and balance / Balance philosophy (soft counters only); placeholder.</summary>
        public IReadOnlyList<string> ArmourTypes { get; }

        /// <summary>Extra distance beyond weapon range within which an attack-moving unit chases a target. Unit: tiles. Range: 0 to 20. Source: Matchups and balance / Balance philosophy (soft counters only); placeholder.</summary>
        public Fix64 AcquireRangeBonusTiles { get; }

        /// <summary>Armour type given to a unit whose file has no "armour_type". Unit: armour type name. Range: one of <see cref="ArmourTypes"/>. Source: Matchups and balance / Balance philosophy (soft counters only); placeholder.</summary>
        public string DefaultArmourType { get; }

        /// <summary>Multiplier per damage type then armour type, ordinal order. Unit: fraction of base damage. Range: 0 to 3 per cell. Source: Matchups and balance / Balance philosophy (soft counters only); placeholder.</summary>
        private readonly SortedDictionary<string, IReadOnlyDictionary<string, Fix64>> _multipliers;

        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public CombatTuning(JsonValue root)
        {
            root.RejectUnknownKeys("damage_types", "armour_types", "multipliers", "acquire_range_bonus_tiles", "default_armour_type");
            DamageTypes = ReadNames(root.Require("damage_types"));
            ArmourTypes = ReadNames(root.Require("armour_types"));
            // The multipliers object: exactly one row per damage type.
            JsonValue table = root.Require("multipliers");
            table.RejectUnknownKeys(ToArray(DamageTypes));
            _multipliers = new SortedDictionary<string, IReadOnlyDictionary<string, Fix64>>(StringComparer.Ordinal);
            foreach (string damageType in DamageTypes)
            {
                // The row for this damage type: exactly one fraction per armour type.
                JsonValue row = table.Require(damageType);
                row.RejectUnknownKeys(ToArray(ArmourTypes));
                // Fractions of this row by armour type, ordinal order.
                SortedDictionary<string, Fix64> cells = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
                foreach (string armourType in ArmourTypes)
                {
                    cells.Add(armourType, row.RequireFix64(armourType, "0", "3"));
                }
                _multipliers.Add(damageType, cells);
            }
            AcquireRangeBonusTiles = root.RequireFix64("acquire_range_bonus_tiles", "0", "20");
            DefaultArmourType = root.RequireNonEmptyString("default_armour_type");
            if (!ContainsName(ArmourTypes, DefaultArmourType))
            {
                throw root.Require("default_armour_type").Fail("must be one of the armour_types (found \"" + DefaultArmourType + "\")");
            }
        }

        /// <summary>The fraction of damage that gets through. Throws <see cref="ArgumentException"/> for an unknown type name.</summary>
        /// <param name="damageType">A name from <see cref="DamageTypes"/>.</param>
        /// <param name="armourType">A name from <see cref="ArmourTypes"/>.</param>
        public Fix64 GetMultiplier(string damageType, string armourType)
        {
            if (!_multipliers.TryGetValue(damageType, out IReadOnlyDictionary<string, Fix64>? row) || !row.TryGetValue(armourType, out Fix64 cell))
            {
                throw new ArgumentException("No multiplier for damage type '" + damageType + "' against armour type '" + armourType + "'.");
            }
            return cell;
        }

        /// <summary>Builds the runtime rules the combat system uses. Hosts set them on the content catalog; the data loader already does.</summary>
        public CombatRules CreateRules()
        {
            return new CombatRules(_multipliers, AcquireRangeBonusTiles, DefaultArmourType);
        }

        /// <summary>Reads a non-empty list of distinct names made of lower-case letters, digits and underscores.</summary>
        private static List<string> ReadNames(JsonValue array)
        {
            // Elements of the array node.
            IReadOnlyList<JsonValue> items = array.Items;
            if (items.Count < 1 || items.Count > MaxTypeNames)
            {
                throw array.Fail("must list 1 to " + MaxTypeNames + " names (found " + items.Count + ")");
            }
            // Names in file order.
            List<string> names = new List<string>();
            foreach (JsonValue item in items)
            {
                // One name as written.
                string name = item.AsString();
                if (name.Length == 0)
                {
                    throw item.Fail("must not be empty");
                }
                // Index into the name text. Unit: characters. Range: 0 to name length - 1.
                for (int i = 0; i < name.Length; i++)
                {
                    // Character being checked.
                    char c = name[i];
                    if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                    {
                        throw item.Fail("may only contain lower-case letters, digits and underscores (found \"" + name + "\")");
                    }
                }
                if (ContainsName(names, name))
                {
                    throw item.Fail("lists \"" + name + "\" twice");
                }
                names.Add(name);
            }
            return names;
        }

        /// <summary>True when <paramref name="name"/> is in <paramref name="names"/> (ordinal comparison).</summary>
        private static bool ContainsName(IReadOnlyList<string> names, string name)
        {
            // Index into the list. Unit: list position. Range: 0 to Count - 1.
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Copies a name list into the array form <see cref="JsonValue.RejectUnknownKeys"/> takes.</summary>
        private static string[] ToArray(IReadOnlyList<string> names)
        {
            // Copy of the names. Range: same length as the list.
            string[] copy = new string[names.Count];
            // Index into the list. Unit: list position. Range: 0 to Count - 1.
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = names[i];
            }
            return copy;
        }
    }
}

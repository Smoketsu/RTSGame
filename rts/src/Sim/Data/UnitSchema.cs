// rts/src/Sim/Data/UnitSchema.cs
// Parsing helpers for the combat-related keys of a unit file (units/<faction>/<name>.json): armour_type,
// sight_range_tiles and the weapon object. Part of Rts.Sim.Data. Called from the unit section of DataLoader so that
// file stays short. All three keys are optional, so unit files written before Phase 1 still load; damage_type and
// armour_type are validated against tuning/combat.json and every error names the unit file and the field
// (for example "units/skai/windrunner.json: field 'weapon.damage_type' must be one of ...").
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Sim.Data
{
    /// <summary>Static helpers that turn the combat keys of a unit file into definition fields.</summary>
    public static class UnitSchema
    {
        /// <summary>Sight range used when a unit file has no sight_range_tiles. Unit: tiles. Range: fixed schema default, 8 (the value fog of war will start from; stored now, unused until Phase 1 vision).</summary>
        public static readonly Fix64 DefaultSightRangeTiles = Fix64.FromInt(8);

        /// <summary>Reads the optional armour_type key, defaulting to the combat tuning's default and checking the name against its armour types.</summary>
        /// <param name="root">The unit file's root object.</param>
        /// <param name="combat">The parsed tuning/combat.json.</param>
        public static string ReadArmourType(JsonValue root, CombatTuning combat)
        {
            // The armour type as written, or the tuning default.
            string armourType = root.OptionalString("armour_type", combat.DefaultArmourType);
            if (!Contains(combat.ArmourTypes, armourType))
            {
                throw root.Require("armour_type").Fail("must be one of " + Join(combat.ArmourTypes) + " from " + CombatTuning.FilePath + " (found \"" + armourType + "\")");
            }
            return armourType;
        }

        /// <summary>Reads the optional sight_range_tiles key. Unit: tiles. Range: 1 to 64; <see cref="DefaultSightRangeTiles"/> when absent.</summary>
        /// <param name="root">The unit file's root object.</param>
        public static Fix64 ReadSightRange(JsonValue root)
        {
            if (!root.TryGetMember("sight_range_tiles", out JsonValue? node))
            {
                return DefaultSightRangeTiles;
            }
            return node.AsFix64(Fix64.One, Fix64.FromInt(64));
        }

        /// <summary>Reads the optional weapon object. Returns null when the key is absent (an unarmed unit).</summary>
        /// <param name="root">The unit file's root object.</param>
        /// <param name="combat">The parsed tuning/combat.json, for the damage type names.</param>
        public static WeaponDefinition? ReadWeapon(JsonValue root, CombatTuning combat)
        {
            if (!root.TryGetMember("weapon", out JsonValue? weapon))
            {
                return null;
            }
            weapon.RejectUnknownKeys("damage", "damage_type", "range_tiles", "cooldown_seconds", "can_target");
            // Damage per hit. Unit: health points (fixed-point). Range: 0.01 to 100000.
            Fix64 damage = weapon.RequireFix64("damage", "0.01", "100000");
            // Damage type name; must be one of the damage types in tuning/combat.json.
            string damageType = weapon.RequireNonEmptyString("damage_type");
            if (!Contains(combat.DamageTypes, damageType))
            {
                throw weapon.Require("damage_type").Fail("must be one of " + Join(combat.DamageTypes) + " from " + CombatTuning.FilePath + " (found \"" + damageType + "\")");
            }
            // Reach between unit edges. Unit: tiles (fixed-point). Range: 0.1 to 64 (melee is 1.5 or less).
            Fix64 range = weapon.RequireFix64("range_tiles", "0.1", "64");
            // Time between hits. Unit: seconds (fixed-point). Range: 0.05 to 60.
            Fix64 cooldown = weapon.RequireFix64("cooldown_seconds", "0.05", "60");
            // Layers the weapon may hit, as set by the can_target array below.
            bool ground = false;
            bool water = false;
            bool air = false;
            // Elements of the can_target array.
            IReadOnlyList<JsonValue> layers = weapon.RequireArray("can_target");
            if (layers.Count < 1 || layers.Count > 3)
            {
                throw weapon.Require("can_target").Fail("must list 1 to 3 of \"ground\", \"water\", \"air\" (found " + layers.Count + " entries)");
            }
            foreach (JsonValue item in layers)
            {
                // One layer name as written.
                string layer = item.AsString();
                // True when this layer was already listed.
                bool duplicate;
                switch (layer)
                {
                    case "ground": duplicate = ground; ground = true; break;
                    case "water": duplicate = water; water = true; break;
                    case "air": duplicate = air; air = true; break;
                    default:
                        throw item.Fail("must be \"ground\", \"water\" or \"air\" (found \"" + layer + "\")");
                }
                if (duplicate)
                {
                    throw item.Fail("lists \"" + layer + "\" twice");
                }
            }
            return new WeaponDefinition(damage, damageType, range, cooldown, ground, water, air);
        }

        /// <summary>True when <paramref name="name"/> is in <paramref name="names"/> (ordinal comparison).</summary>
        private static bool Contains(IReadOnlyList<string> names, string name)
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

        /// <summary>Joins names as "a", "b", "c" for error messages.</summary>
        private static string Join(IReadOnlyList<string> names)
        {
            // Text built so far.
            string text = string.Empty;
            // Index into the list. Unit: list position. Range: 0 to Count - 1.
            for (int i = 0; i < names.Count; i++)
            {
                text += (i == 0 ? "\"" : ", \"") + names[i] + "\"";
            }
            return text;
        }
    }
}

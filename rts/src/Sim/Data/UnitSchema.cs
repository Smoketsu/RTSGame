// rts/src/Sim/Data/UnitSchema.cs
// Parsing helpers for the combat-related and economy keys of a unit file (units/<faction>/<name>.json): armour_type,
// sight_range_tiles, the weapon object, the hq object and the economy keys (build_seconds, can_gather, is_drop_off, trains,
// is_static_defence, footprint, provides_supply). Part of Rts.Sim.Data. Called from the unit section of DataLoader so that
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
            weapon.RejectUnknownKeys("damage", "damage_type", "range_tiles", "cooldown_seconds", "can_target", "applies_drench");
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
            // Optional switch: every landed hit counts as a drench hit on air targets (Spoutling). Range: true or false; false when absent.
            bool appliesDrench = weapon.TryGetMember("applies_drench", out JsonValue? drenchNode) && drenchNode.AsBool();
            return new WeaponDefinition(damage, damageType, range, cooldown, ground, water, air, appliesDrench);
        }

        /// <summary>
        /// Reads the optional hq object: { "is_hq": true, "rooted_armour_type", "uprooted_armour_type", "worker_unit" }. Returns null
        /// when the key is absent or is_hq is false. Both armour types are checked against tuning/combat.json; the worker unit's
        /// existence is checked later, once every unit is loaded.
        /// </summary>
        /// <param name="root">The unit file's root object.</param>
        /// <param name="combat">The parsed tuning/combat.json, for the armour type names.</param>
        public static HqDefinition? ReadHq(JsonValue root, CombatTuning combat)
        {
            if (!root.TryGetMember("hq", out JsonValue? hq))
            {
                return null;
            }
            hq.RejectUnknownKeys("is_hq", "rooted_armour_type", "uprooted_armour_type", "worker_unit");
            if (!hq.RequireBool("is_hq"))
            {
                return null;
            }
            // Armour type while Rooted, and while Uprooted or switching; both must be armour types of the damage table.
            string rooted = hq.RequireNonEmptyString("rooted_armour_type");
            string uprooted = hq.RequireNonEmptyString("uprooted_armour_type");
            if (!Contains(combat.ArmourTypes, rooted))
            {
                throw hq.Require("rooted_armour_type").Fail("must be one of " + Join(combat.ArmourTypes) + " from " + CombatTuning.FilePath + " (found \"" + rooted + "\")");
            }
            if (!Contains(combat.ArmourTypes, uprooted))
            {
                throw hq.Require("uprooted_armour_type").Fail("must be one of " + Join(combat.ArmourTypes) + " from " + CombatTuning.FilePath + " (found \"" + uprooted + "\")");
            }
            return new HqDefinition(rooted, uprooted, hq.RequireNonEmptyString("worker_unit"));
        }

        /// <summary>
        /// Reads the optional economy keys of a unit file: build_seconds (0.05 to 3600, default <see cref="UnitEconomyDefinition.DefaultBuildSeconds"/>),
        /// can_gather, is_drop_off, is_static_defence (booleans, default false), trains (array of unit IDs, default empty),
        /// footprint (0 or 1, default 0) and provides_supply (0 to 100, default 0). References in trains are checked once every unit is loaded.
        /// </summary>
        /// <param name="root">The unit file's root object.</param>
        public static UnitEconomyDefinition ReadEconomy(JsonValue root)
        {
            // Train or construction time. Unit: seconds (fixed-point). Range: 0.05 to 3600.
            Fix64 buildSeconds = root.TryGetMember("build_seconds", out JsonValue? buildNode)
                ? buildNode.AsFix64(Fix64.Parse("0.05"), Fix64.FromInt(3600))
                : UnitEconomyDefinition.DefaultBuildSeconds;
            // Worker flag, drop-off flag and static-defence flag. Range: true or false; false when absent.
            bool canGather = root.TryGetMember("can_gather", out JsonValue? gatherNode) && gatherNode.AsBool();
            bool isDropOff = root.TryGetMember("is_drop_off", out JsonValue? dropNode) && dropNode.AsBool();
            bool isStaticDefence = root.TryGetMember("is_static_defence", out JsonValue? defenceNode) && defenceNode.AsBool();
            // Units this unit can train, in file order; duplicates are an error.
            List<string> trains = new List<string>();
            if (root.TryGetMember("trains", out JsonValue? trainsNode))
            {
                foreach (JsonValue item in trainsNode.Items)
                {
                    // One listed unit ID.
                    string id = item.AsString();
                    if (trains.Contains(id))
                    {
                        throw item.Fail("lists the unit '" + id + "' twice");
                    }
                    trains.Add(id);
                }
            }
            // Tiles blocked while the unit lives. Unit: tiles. Range: 0 or 1.
            int footprint = root.TryGetMember("footprint", out JsonValue? footprintNode) ? footprintNode.AsInt(0, 1) : 0;
            // Supply added to the owner's cap. Unit: supply. Range: 0 to 100.
            int providesSupply = root.TryGetMember("provides_supply", out JsonValue? supplyNode) ? supplyNode.AsInt(0, 100) : 0;
            return new UnitEconomyDefinition(buildSeconds, canGather, isDropOff, trains, isStaticDefence, footprint, providesSupply);
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

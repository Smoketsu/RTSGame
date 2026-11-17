// rts/src/Sim/Data/UnitSchema.cs
// Parsing helpers for the combat-related and economy keys of a unit file (units/<faction>/<name>.json): armour_type,
// sight_range_tiles, airborne_sight_range_tiles (required for air units), the weapon object, the hq object and the economy keys (build_seconds, can_gather, is_drop_off, trains,
// is_static_defence, footprint, provides_supply) and the flooding keys (flood_source_output_per_second, flood_source_capacity,
// can_cast_wave). Part of Rts.Sim.Data. Called from the unit section of DataLoader so that
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
        /// <summary>Sight range used when a unit file has no sight_range_tiles. Unit: tiles. Range: fixed schema default, 8 (read by fog of war, rts/src/Sim/Vision).</summary>
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

        /// <summary>
        /// Reads the airborne_sight_range_tiles key (fog_of_war.md section 2): required when the unit's layer is air, rejected
        /// for ground and water units. Returns null for non-air units (the definition then uses sight_range_tiles).
        /// Unit: tiles. Range: 1 to 64.
        /// </summary>
        /// <param name="root">The unit file's root object.</param>
        /// <param name="layer">The unit's definition layer, already parsed.</param>
        public static Fix64? ReadAirborneSightRange(JsonValue root, MovementLayer layer)
        {
            // True when the file carries the key.
            bool present = root.TryGetMember("airborne_sight_range_tiles", out JsonValue? node);
            if (layer != MovementLayer.Air)
            {
                if (present)
                {
                    throw node!.Fail("is only allowed for units whose layer is \"air\"");
                }
                return null;
            }
            // The key is mandatory for flyers: Require names the file and field when it is missing.
            return root.Require("airborne_sight_range_tiles").AsFix64(Fix64.One, Fix64.FromInt(64));
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
            weapon.RejectUnknownKeys("damage", "damage_type", "range_tiles", "cooldown_seconds", "can_target", "applies_drench", "hit_delay_ticks", "splash_radius_tiles", "structure_damage_multiplier", "can_target_air_on_water");
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
            // Ticks between firing and landing. Unit: ticks. Range: 0 to 10; 0 when absent; must be 0 for a melee weapon.
            int hitDelay = weapon.TryGetMember("hit_delay_ticks", out JsonValue? delayNode) ? delayNode.AsInt(0, WeaponDefinition.MaxHitDelayTicks) : 0;
            if (hitDelay > 0 && range <= WeaponDefinition.MeleeMaxRangeTiles)
            {
                throw delayNode!.Fail("must be 0 for a melee weapon (range_tiles 1.5 or less)");
            }
            // Blast radius. Unit: tiles (fixed-point). Range: 0 to 4; 0 (no blast) when absent.
            Fix64 splash = weapon.TryGetMember("splash_radius_tiles", out JsonValue? splashNode)
                ? splashNode.AsFix64(Fix64.Zero, Fix64.FromInt(WeaponDefinition.MaxSplashRadiusTiles)) : Fix64.Zero;
            // Multiplier on damage against structures. Unit: multiplier of damage. Range: 0.1 to 4; 1 (no bonus) when absent.
            Fix64 structureMultiplier = weapon.TryGetMember("structure_damage_multiplier", out JsonValue? structureNode)
                ? structureNode.AsFix64(WeaponDefinition.MinStructureDamageMultiplier, WeaponDefinition.MaxStructureDamageMultiplier) : Fix64.One;
            // Optional switch: the weapon also hits flyers while its unit stands on a flooded tile. Range: true or false; false when absent; must be absent or false when can_target lists "air".
            bool airOnWater = weapon.TryGetMember("can_target_air_on_water", out JsonValue? airOnWaterNode) && airOnWaterNode.AsBool();
            if (airOnWater && air)
            {
                throw airOnWaterNode!.Fail("is redundant: can_target already lists air");
            }
            return new WeaponDefinition(damage, damageType, range, cooldown, ground, water, air, appliesDrench, hitDelay, splash, structureMultiplier, airOnWater);
        }

        /// <summary>Reads the optional shield_dome object (all four keys required inside it). Returns null when absent.</summary>
        /// <param name="root">The unit file's root object.</param>
        public static ShieldDomeDefinition? ReadShieldDome(JsonValue root)
        {
            if (!root.TryGetMember("shield_dome", out JsonValue? dome))
            {
                return null;
            }
            dome.RejectUnknownKeys("radius_tiles", "pool", "regen_delay_seconds", "regen_per_second");
            return new ShieldDomeDefinition(
                dome.RequireFix64("radius_tiles", "0.5", "16"),
                dome.RequireFix64("pool", "1", "100000"),
                dome.RequireFix64("regen_delay_seconds", "0", "60"),
                dome.RequireFix64("regen_per_second", "0.01", "10000"));
        }

        /// <summary>Reads the optional grounding_ability object (all three keys required inside it). Returns null when absent.</summary>
        /// <param name="root">The unit file's root object.</param>
        public static GroundingAbilityDefinition? ReadGroundingAbility(JsonValue root)
        {
            if (!root.TryGetMember("grounding_ability", out JsonValue? ability))
            {
                return null;
            }
            ability.RejectUnknownKeys("range_tiles", "radius_tiles", "cooldown_seconds");
            return new GroundingAbilityDefinition(
                ability.RequireFix64("range_tiles", "0.5", "32"),
                ability.RequireFix64("radius_tiles", "0.5", "8"),
                ability.RequireFix64("cooldown_seconds", "1", "300"));
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
        /// can_gather, is_drop_off, is_static_defence, is_wall (booleans, default false; is_wall needs footprint 1), trains and requires (arrays of unit IDs, default empty; requires at most 8, no duplicates),
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
            // Wall flag. Range: true or false; false when absent; true needs footprint 1.
            bool isWall = root.TryGetMember("is_wall", out JsonValue? wallNode) && wallNode.AsBool();
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
            // Prerequisite building IDs, in file order. Range: 0 to 8 distinct IDs; references are checked once every unit is loaded.
            List<string> requires = new List<string>();
            if (root.TryGetMember("requires", out JsonValue? requiresNode))
            {
                if (requiresNode.Items.Count > 8)
                {
                    throw requiresNode.Fail("lists more than 8 entries");
                }
                foreach (JsonValue item in requiresNode.Items)
                {
                    // One required building ID.
                    string id = item.AsString();
                    if (requires.Contains(id))
                    {
                        throw item.Fail("lists '" + id + "' twice");
                    }
                    requires.Add(id);
                }
            }
            // Tiles blocked while the unit lives. Unit: tiles. Range: 0 or 1.
            int footprint = root.TryGetMember("footprint", out JsonValue? footprintNode) ? footprintNode.AsInt(0, 1) : 0;
            // Supply added to the owner's cap. Unit: supply. Range: 0 to 100.
            int providesSupply = root.TryGetMember("provides_supply", out JsonValue? supplyNode) ? supplyNode.AsInt(0, 100) : 0;
            if (isWall && footprint != 1)
            {
                throw root.Require("is_wall").Fail("is_wall needs footprint 1");
            }
            return new UnitEconomyDefinition(buildSeconds, canGather, isDropOff, trains, isStaticDefence, footprint, providesSupply, isWall, requires);
        }

        /// <summary>
        /// Reads the optional flooding keys of a unit file (rts/docs/specs/flooding.md section 2): flood_source_output_per_second
        /// (depth-unit tiles per second, 0 to 64, default 0; above 0 makes the unit a flood source), flood_source_capacity
        /// (depth-unit tiles, 0 to 4096, default 0; must be above 0 when the output is above 0) and can_cast_wave (boolean,
        /// default false). Returns <see cref="UnitFloodDefinition.None"/> when none of the keys is present.
        /// </summary>
        /// <param name="root">The unit file's root object.</param>
        public static UnitFloodDefinition ReadFlood(JsonValue root)
        {
            // Water released per second. Unit: depth-unit tiles per second. Range: 0 to 64; 0 when absent.
            Fix64 output = root.TryGetMember("flood_source_output_per_second", out JsonValue? outputNode)
                ? outputNode.AsFix64(Fix64.Zero, UnitFloodDefinition.MaxOutputPerSecond)
                : Fix64.Zero;
            // Share of the team's high-tide volume. Unit: depth-unit tiles. Range: 0 to 4096; 0 when absent.
            Fix64 capacity = root.TryGetMember("flood_source_capacity", out JsonValue? capacityNode)
                ? capacityNode.AsFix64(Fix64.Zero, UnitFloodDefinition.MaxCapacity)
                : Fix64.Zero;
            if (output > Fix64.Zero && capacity <= Fix64.Zero)
            {
                throw root.Require("flood_source_output_per_second").Fail("needs flood_source_capacity above 0 (a source with no capacity could never release water)");
            }
            // Wave switch (Tidecaller). Range: true or false; false when absent.
            bool canCastWave = root.TryGetMember("can_cast_wave", out JsonValue? waveNode) && waveNode.AsBool();
            if (output == Fix64.Zero && capacity == Fix64.Zero && !canCastWave)
            {
                return UnitFloodDefinition.None;
            }
            return new UnitFloodDefinition(output, capacity, canCastWave);
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

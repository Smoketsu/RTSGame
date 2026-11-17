// rts/tools/BalanceHarness/FightSettings.cs
// Reads rts/data/scripts/fights/fights.json (spec: rts/docs/specs/micro_benchmark.md section 3.1): the settings of the equal-value fight mode (--fights). The file lives
// under scripts/ so DataLoader skips it (outside the content digest). Parsed with JsonValue and its Require helpers; unknown keys, bad ranges and cross-checks against the
// data set are DataValidationExceptions naming the file and the field. Harness-only: the simulation never reads it; seconds stay Fix64 so tick conversions are exact,
// fractions are doubles because only tool-side statistics use them. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>A mixed army recipe from the settings file.</summary>
    public sealed class FightComposition
    {
        /// <summary>Composition ID: letters a-z, digits and underscores, no dot. Range: 1 to 64 characters.</summary>
        public string Id { get; }

        /// <summary>Recipe: unit ID to count, in ordinal unit ID order. Unit: units. Range: 1 to 8 entries, counts 1 to 50.</summary>
        public IReadOnlyDictionary<string, int> Units { get; }

        /// <summary>Bundles the values.</summary>
        public FightComposition(string id, IReadOnlyDictionary<string, int> units)
        {
            Id = id;
            Units = units;
        }
    }

    /// <summary>Terrain kinds of a fight case.</summary>
    public static class FightKinds
    {
        /// <summary>All height 0, both seats attack.</summary>
        public const string Flat = "flat";

        /// <summary>Plateau for seat 1 with one ramp; seat 1 holds.</summary>
        public const string HighGround = "high_ground";

        /// <summary>Flat arena flooded by seat 1's team before the armies spawn.</summary>
        public const string Flooded = "flooded";

        /// <summary>Wall with a gap between the armies.</summary>
        public const string Choke = "choke";

        /// <summary>The known kinds in documentation order.</summary>
        public static readonly string[] All = { Flat, HighGround, Flooded, Choke };
    }

    /// <summary>A terrain case with the army values it is played at.</summary>
    public sealed class FightCase
    {
        /// <summary>Case ID: letters a-z, digits and underscores. Range: 1 to 64 characters.</summary>
        public string Id { get; }

        /// <summary>One of <see cref="FightKinds.All"/>.</summary>
        public string Kind { get; }

        /// <summary>Army values played. Unit: resource units. Range: 1 to 8 entries of 50 to 20000.</summary>
        public IReadOnlyList<int> ArmyValues { get; }

        /// <summary>Bundles the values.</summary>
        public FightCase(string id, string kind, IReadOnlyList<int> armyValues)
        {
            Id = id;
            Kind = kind;
            ArmyValues = armyValues;
        }
    }

    /// <summary>A threat army of rule 5.</summary>
    public sealed class FightThreat
    {
        /// <summary>Threat name, for example "air".</summary>
        public string Id { get; }

        /// <summary>Unit or composition ID of the army that plays the threat.</summary>
        public string Side { get; }

        /// <summary>Bundles the values.</summary>
        public FightThreat(string id, string side)
        {
            Id = id;
            Side = side;
        }
    }

    /// <summary>The parsed fights.json.</summary>
    public sealed class FightSettings
    {
        /// <summary>Source name used in every error message. Range: fixed path relative to rts/data.</summary>
        public const string SourceName = "scripts/fights/fights.json";

        /// <summary>Path of the file relative to the data folder. Range: fixed.</summary>
        public const string RelativePath = "scripts/fights/fights.json";

        /// <summary>Base seed of the fight seed derivation. Unit: seed. Range: 0 to 2147483647.</summary>
        public ulong BaseSeed { get; private set; }

        /// <summary>The swept units (combat units) in ordinal ID order. Unit: unit IDs. Range: 1 to 64 entries.</summary>
        public IReadOnlyList<string> Units { get; private set; } = Array.Empty<string>();

        /// <summary>The compositions in file order. Range: 0 to 16 entries.</summary>
        public IReadOnlyList<FightComposition> Compositions { get; private set; } = Array.Empty<FightComposition>();

        /// <summary>The terrain cases in file order. Range: 1 to 16 entries.</summary>
        public IReadOnlyList<FightCase> Cases { get; private set; } = Array.Empty<FightCase>();

        /// <summary>Arena size along the line of approach. Unit: tiles. Range: 16 to 128.</summary>
        public int ArenaWidthTiles { get; private set; }

        /// <summary>Arena size across the line of approach. Unit: tiles. Range: 16 to 128.</summary>
        public int ArenaHeightTiles { get; private set; }

        /// <summary>Gap between the front ranks at spawn. Unit: tiles. Range: 2 to 64.</summary>
        public int StartDistanceTiles { get; private set; }

        /// <summary>Units per rank. Unit: units. Range: 1 to 50.</summary>
        public int FormationWidthUnits { get; private set; }

        /// <summary>Distance between neighbouring spawn positions. Unit: tiles (fixed-point). Range: 0.5 to 4.</summary>
        public Fix64 FormationSpacingTiles { get; private set; }

        /// <summary>Ramp width of the high_ground case. Unit: tiles. Range: 1 to 16.</summary>
        public int RampWidthTiles { get; private set; }

        /// <summary>Gap width of the choke case. Unit: tiles. Range: 1 to 16.</summary>
        public int ChokeWidthTiles { get; private set; }

        /// <summary>Wall thickness of the choke case. Unit: tiles. Range: 1 to 8.</summary>
        public int ChokeWallThicknessTiles { get; private set; }

        /// <summary>Unit spawned to flood the arena. Unit: unit ID. Range: a unit with flood output above 0.</summary>
        public string FloodSourceUnit { get; private set; } = string.Empty;

        /// <summary>Flood source tiles relative to the arena centre tile as (dx, dy). Unit: tiles. Range: 1 to 8 entries, inside the arena.</summary>
        public IReadOnlyList<KeyValuePair<int, int>> FloodSourceOffsets { get; private set; } = Array.Empty<KeyValuePair<int, int>>();

        /// <summary>Warm-up before the armies spawn in the flooded case. Unit: seconds (fixed-point). Range: 0 to 600.</summary>
        public Fix64 FloodWarmupSeconds { get; private set; }

        /// <summary>Least flooded share of the lane at army spawn. Unit: fraction. Range: 0 to 1.</summary>
        public double FloodMinLaneFraction { get; private set; }

        /// <summary>Interval of the idle re-targeting rule. Unit: seconds (fixed-point). Range: 0.25 to 30.</summary>
        public Fix64 ReissueSeconds { get; private set; }

        /// <summary>Confirmation time of the cannot-target stalemate. Unit: seconds (fixed-point). Range: 0.05 to 60.</summary>
        public Fix64 StalemateConfirmSeconds { get; private set; }

        /// <summary>Time without damage that ends a fight as a stalemate. Unit: seconds (fixed-point). Range: 0.05 to 600.</summary>
        public Fix64 StalemateIdleSeconds { get; private set; }

        /// <summary>Fight length cap. Unit: seconds (fixed-point). Range: 1 to 1800.</summary>
        public Fix64 MaxFightSeconds { get; private set; }

        /// <summary>Largest allowed difference of the two army values as a share of the larger. Unit: fraction. Range: 0 to 1.</summary>
        public double MaxValueMismatchFraction { get; private set; }

        /// <summary>Fights re-run in-process for the spot check. Unit: fights. Range: 0 to 16.</summary>
        public int VerifyFights { get; private set; }

        /// <summary>Fights per worker call. Unit: fights. Range: 1 to 1000.</summary>
        public int ChunkFights { get; private set; }

        /// <summary>Case the checks and the matchups file read. Unit: case ID. Range: a case of kind flat.</summary>
        public string RuleCase { get; private set; } = string.Empty;

        /// <summary>Army value the checks and the matchups file read. Unit: resource units. Range: one of the army values of <see cref="RuleCase"/>.</summary>
        public int RuleArmyValue { get; private set; }

        /// <summary>Threat armies of rule 5. Range: 0 to 16 entries.</summary>
        public IReadOnlyList<FightThreat> Threats { get; private set; } = Array.Empty<FightThreat>();

        /// <summary>Rule 1: mobile answers needed. Unit: unit types. Range: 0 to 20.</summary>
        public int Rule1MinMobileAnswers { get; private set; }

        /// <summary>Rule 1: static answers needed. Unit: unit types. Range: 0 to 20.</summary>
        public int Rule1MinStaticAnswers { get; private set; }

        /// <summary>Rule 2: lowest multiplier. Unit: fraction. Range: 0 to 3.</summary>
        public double Rule2MinMultiplier { get; private set; }

        /// <summary>Rule 2: highest multiplier. Unit: fraction. Range: 0 to 3.</summary>
        public double Rule2MaxMultiplier { get; private set; }

        /// <summary>Rule 2: highest over lowest multiplier within a damage type. Unit: ratio. Range: 1 to 100.</summary>
        public double Rule2MaxRatioWithinType { get; private set; }

        /// <summary>Rule 3: least value left of a counter. Unit: fraction. Range: 0 to 1.</summary>
        public double Rule3MinValueLeft { get; private set; }

        /// <summary>Rule 3: largest exchange ratio of a counter. Unit: ratio. Range: 1 to 100.</summary>
        public double Rule3MaxExchangeRatio { get; private set; }

        /// <summary>Rule 5: unit answers needed per threat and faction. Unit: unit types. Range: 0 to 20.</summary>
        public int Rule5MinUnitAnswers { get; private set; }

        /// <summary>Rule 5: terrain answers needed per threat and faction. Unit: cases. Range: 0 to 20.</summary>
        public int Rule5MinTerrainAnswers { get; private set; }

        /// <summary>True for a unit that is swept as a side: mobile, supplied, no HQ, no footprint, not a gatherer and armed.</summary>
        /// <param name="definition">The unit definition.</param>
        public static bool IsCombatUnit(UnitDefinition definition)
        {
            return definition.SpeedTilesPerSecond > Fix64.Zero && definition.Supply > 0 && IsArmy(definition) && definition.Weapon != null;
        }

        /// <summary>True for an army unit: mobile footprint 0, not a worker, not an HQ. The one rule shared by <see cref="ResponseObserver"/> and the fight observer.</summary>
        /// <param name="definition">The unit definition.</param>
        public static bool IsArmy(UnitDefinition definition)
        {
            return definition.Economy.FootprintTiles == 0 && !definition.Economy.CanGather && definition.Hq == null;
        }

        /// <summary>Value of one unit: cost_common + cost_rare (AiCounter.Value with counter_rare_cost_weight 1). Unit: resource units. Range: 0 upward.</summary>
        /// <param name="definition">The unit definition.</param>
        public static int ValueOf(UnitDefinition definition)
        {
            return definition.CostCommon + definition.CostRare;
        }

        /// <summary>The case with this ID; throws <see cref="HarnessException"/> when there is none.</summary>
        /// <param name="id">Case ID.</param>
        public FightCase CaseById(string id)
        {
            foreach (FightCase fightCase in Cases)
            {
                if (fightCase.Id == id)
                {
                    return fightCase;
                }
            }
            throw new HarnessException("fights.json has no case '" + id + "'");
        }

        /// <summary>Reads and parses the file from <paramref name="dataDirectory"/>; throws <see cref="HarnessException"/> when it is missing.</summary>
        /// <param name="dataDirectory">The data folder (rts/data).</param>
        /// <param name="data">The loaded data set the settings are checked against.</param>
        public static FightSettings Load(string dataDirectory, DataSet data)
        {
            // Full path of the settings file.
            string path = Path.Combine(dataDirectory, RelativePath);
            if (!File.Exists(path))
            {
                throw new HarnessException("settings file not found: " + path);
            }
            return Parse(File.ReadAllText(path), data);
        }

        /// <summary>Parses the text of fights.json and checks it against the data set.</summary>
        /// <param name="text">File text (comments allowed).</param>
        /// <param name="data">The loaded data set.</param>
        public static FightSettings Parse(string text, DataSet data)
        {
            JsonValue root = JsonReader.Parse(text, SourceName);
            root.RejectUnknownKeys("base_seed", "units", "compositions", "cases", "arena_width_tiles", "arena_height_tiles", "start_distance_tiles", "formation_width_units",
                "formation_spacing_tiles", "ramp_width_tiles", "choke_width_tiles", "choke_wall_thickness_tiles", "flood_source_unit", "flood_source_offsets",
                "flood_warmup_seconds", "flood_min_lane_fraction", "reissue_seconds", "stalemate_confirm_seconds", "stalemate_idle_seconds", "max_fight_seconds",
                "max_value_mismatch_fraction", "verify_fights", "chunk_fights", "rule_case", "rule_army_value", "threats", "rule1_min_mobile_answers",
                "rule1_min_static_answers", "rule2_min_multiplier", "rule2_max_multiplier", "rule2_max_ratio_within_type", "rule3_min_value_left",
                "rule3_max_exchange_ratio", "rule5_min_unit_answers", "rule5_min_terrain_answers");
            FightSettings s = new FightSettings();
            s.BaseSeed = (ulong)root.RequireInt("base_seed", 0, int.MaxValue);
            s.Units = ReadUnits(root.Require("units"), data);
            s.Compositions = ReadCompositions(root.Require("compositions"), data);
            s.Cases = ReadCases(root.Require("cases"));
            s.ArenaWidthTiles = root.RequireInt("arena_width_tiles", 16, 128);
            s.ArenaHeightTiles = root.RequireInt("arena_height_tiles", 16, 128);
            s.StartDistanceTiles = root.RequireInt("start_distance_tiles", 2, 64);
            s.FormationWidthUnits = root.RequireInt("formation_width_units", 1, 50);
            s.FormationSpacingTiles = root.RequireFix64("formation_spacing_tiles", "0.5", "4");
            s.RampWidthTiles = root.RequireInt("ramp_width_tiles", 1, 16);
            s.ChokeWidthTiles = root.RequireInt("choke_width_tiles", 1, 16);
            s.ChokeWallThicknessTiles = root.RequireInt("choke_wall_thickness_tiles", 1, 8);
            JsonValue floodUnit = root.Require("flood_source_unit");
            s.FloodSourceUnit = floodUnit.AsString();
            if (!data.Content.Units.TryGetValue(s.FloodSourceUnit, out UnitDefinition? floodDefinition) || !floodDefinition.Flood.IsSource)
            {
                throw floodUnit.Fail("must name a unit with flood_source_output_per_second above 0 (found '" + s.FloodSourceUnit + "')");
            }
            s.FloodSourceOffsets = ReadOffsets(root.Require("flood_source_offsets"), s.ArenaWidthTiles, s.ArenaHeightTiles);
            s.FloodWarmupSeconds = root.RequireFix64("flood_warmup_seconds", "0", "600");
            s.FloodMinLaneFraction = HarnessStats.ToDouble(root.RequireFix64("flood_min_lane_fraction", "0", "1"));
            s.ReissueSeconds = root.RequireFix64("reissue_seconds", "0.25", "30");
            s.StalemateConfirmSeconds = root.RequireFix64("stalemate_confirm_seconds", "0.05", "60");
            s.StalemateIdleSeconds = root.RequireFix64("stalemate_idle_seconds", "0.05", "600");
            s.MaxFightSeconds = root.RequireFix64("max_fight_seconds", "1", "1800");
            s.MaxValueMismatchFraction = HarnessStats.ToDouble(root.RequireFix64("max_value_mismatch_fraction", "0", "1"));
            s.VerifyFights = root.RequireInt("verify_fights", 0, 16);
            s.ChunkFights = root.RequireInt("chunk_fights", 1, 1000);
            JsonValue ruleCase = root.Require("rule_case");
            s.RuleCase = ruleCase.AsString();
            FightCase? flat = null;
            foreach (FightCase fightCase in s.Cases)
            {
                if (fightCase.Id == s.RuleCase)
                {
                    flat = fightCase;
                }
            }
            if (flat == null || flat.Kind != FightKinds.Flat)
            {
                throw ruleCase.Fail("must name a case of kind flat (found '" + s.RuleCase + "')");
            }
            JsonValue ruleValue = root.Require("rule_army_value");
            s.RuleArmyValue = ruleValue.AsInt(50, 20000);
            if (!new List<int>(flat.ArmyValues).Contains(s.RuleArmyValue))
            {
                throw ruleValue.Fail("must be one of the army_values of case '" + s.RuleCase + "' (found " + s.RuleArmyValue.ToString(CultureInfo.InvariantCulture) + ")");
            }
            s.Threats = ReadThreats(root.Require("threats"), s);
            s.Rule1MinMobileAnswers = root.RequireInt("rule1_min_mobile_answers", 0, 20);
            s.Rule1MinStaticAnswers = root.RequireInt("rule1_min_static_answers", 0, 20);
            s.Rule2MinMultiplier = HarnessStats.ToDouble(root.RequireFix64("rule2_min_multiplier", "0", "3"));
            s.Rule2MaxMultiplier = HarnessStats.ToDouble(root.RequireFix64("rule2_max_multiplier", "0", "3"));
            s.Rule2MaxRatioWithinType = HarnessStats.ToDouble(root.RequireFix64("rule2_max_ratio_within_type", "1", "100"));
            s.Rule3MinValueLeft = HarnessStats.ToDouble(root.RequireFix64("rule3_min_value_left", "0", "1"));
            s.Rule3MaxExchangeRatio = HarnessStats.ToDouble(root.RequireFix64("rule3_max_exchange_ratio", "1", "100"));
            s.Rule5MinUnitAnswers = root.RequireInt("rule5_min_unit_answers", 0, 20);
            s.Rule5MinTerrainAnswers = root.RequireInt("rule5_min_terrain_answers", 0, 20);
            return s;
        }

        /// <summary>True when <paramref name="id"/> is a unit or composition of the sweep.</summary>
        /// <param name="id">Side ID.</param>
        public bool HasSide(string id)
        {
            foreach (string unit in Units)
            {
                if (unit == id)
                {
                    return true;
                }
            }
            foreach (FightComposition composition in Compositions)
            {
                if (composition.Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The sweep units: every combat unit of the data set in ordinal order, or the listed ones (each must be a combat unit) in ordinal order.</summary>
        private static List<string> ReadUnits(JsonValue node, DataSet data)
        {
            if (node.Items.Count > 64)
            {
                throw node.Fail("must hold 0 to 64 entries (found " + node.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            // Unit IDs in ordinal order.
            SortedSet<string> ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JsonValue item in node.Items)
            {
                string id = item.AsString();
                if (!data.Content.Units.TryGetValue(id, out UnitDefinition? definition) || !IsCombatUnit(definition))
                {
                    throw item.Fail("must name a combat unit (found '" + id + "')");
                }
                if (!ids.Add(id))
                {
                    throw item.Fail("lists '" + id + "' twice");
                }
            }
            if (node.Items.Count == 0)
            {
                foreach (UnitDefinition definition in data.Content.Units.Values)
                {
                    if (IsCombatUnit(definition))
                    {
                        ids.Add(definition.Id);
                    }
                }
            }
            if (ids.Count == 0)
            {
                throw node.Fail("selects no combat unit");
            }
            return new List<string>(ids);
        }

        /// <summary>Reads the compositions: unique IDs, one faction, combat units, counts 1 to 50.</summary>
        private static List<FightComposition> ReadCompositions(JsonValue node, DataSet data)
        {
            if (node.Items.Count > 16)
            {
                throw node.Fail("must hold 0 to 16 entries (found " + node.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            List<FightComposition> list = new List<FightComposition>();
            SortedSet<string> seen = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JsonValue item in node.Items)
            {
                item.RejectUnknownKeys("id", "units");
                JsonValue idNode = item.Require("id");
                string id = idNode.AsString();
                if (!IsIdentifier(id))
                {
                    throw idNode.Fail("must be 1 to 64 characters of a-z, 0-9 and underscore, no dot (found '" + id + "')");
                }
                if (!seen.Add(id))
                {
                    throw idNode.Fail("is used twice ('" + id + "')");
                }
                JsonValue unitsNode = item.Require("units");
                if (unitsNode.Members.Count < 1 || unitsNode.Members.Count > 8)
                {
                    throw unitsNode.Fail("must hold 1 to 8 units (found " + unitsNode.Members.Count.ToString(CultureInfo.InvariantCulture) + ")");
                }
                SortedDictionary<string, int> recipe = new SortedDictionary<string, int>(StringComparer.Ordinal);
                string faction = string.Empty;
                foreach (KeyValuePair<string, JsonValue> pair in unitsNode.Members)
                {
                    if (!data.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition) || !IsCombatUnit(definition))
                    {
                        throw pair.Value.Fail("names '" + pair.Key + "', which is not a combat unit");
                    }
                    if (faction.Length > 0 && faction != definition.FactionId)
                    {
                        throw pair.Value.Fail("mixes factions ('" + pair.Key + "' is " + definition.FactionId + ", the others are " + faction + ")");
                    }
                    faction = definition.FactionId;
                    recipe.Add(pair.Key, pair.Value.AsInt(1, 50));
                }
                list.Add(new FightComposition(id, recipe));
            }
            return list;
        }

        /// <summary>Reads the cases: unique IDs, a known kind, 1 to 8 army values of 50 to 20000.</summary>
        private static List<FightCase> ReadCases(JsonValue node)
        {
            if (node.Items.Count < 1 || node.Items.Count > 16)
            {
                throw node.Fail("must hold 1 to 16 entries (found " + node.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            List<FightCase> list = new List<FightCase>();
            SortedSet<string> seen = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JsonValue item in node.Items)
            {
                item.RejectUnknownKeys("id", "kind", "army_values");
                JsonValue idNode = item.Require("id");
                string id = idNode.AsString();
                if (!IsIdentifier(id))
                {
                    throw idNode.Fail("must be 1 to 64 characters of a-z, 0-9 and underscore (found '" + id + "')");
                }
                if (!seen.Add(id))
                {
                    throw idNode.Fail("is used twice ('" + id + "')");
                }
                JsonValue kindNode = item.Require("kind");
                string kind = kindNode.AsString();
                if (Array.IndexOf(FightKinds.All, kind) < 0)
                {
                    throw kindNode.Fail("is an unknown kind '" + kind + "' (known: " + string.Join(", ", FightKinds.All) + ")");
                }
                JsonValue valuesNode = item.Require("army_values");
                if (valuesNode.Items.Count < 1 || valuesNode.Items.Count > 8)
                {
                    throw valuesNode.Fail("must hold 1 to 8 entries (found " + valuesNode.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
                }
                List<int> values = new List<int>();
                foreach (JsonValue value in valuesNode.Items)
                {
                    values.Add(value.AsInt(50, 20000));
                }
                list.Add(new FightCase(id, kind, values));
            }
            return list;
        }

        /// <summary>Reads the flood source offsets and checks that each lies inside the arena.</summary>
        private static List<KeyValuePair<int, int>> ReadOffsets(JsonValue node, int width, int height)
        {
            if (node.Items.Count < 1 || node.Items.Count > 8)
            {
                throw node.Fail("must hold 1 to 8 entries (found " + node.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>();
            foreach (JsonValue item in node.Items)
            {
                if (item.Items.Count != 2)
                {
                    throw item.Fail("must be a [dx, dy] pair");
                }
                // Offset from the arena centre tile. Unit: tiles. Range: -128 to 128.
                int dx = item.Items[0].AsInt(-128, 128);
                int dy = item.Items[1].AsInt(-128, 128);
                // Absolute tile of the source. Unit: tiles.
                int x = width / 2 + dx;
                int y = height / 2 + dy;
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    throw item.Fail("puts a flood source outside the arena (tile " + x.ToString(CultureInfo.InvariantCulture) + ", " + y.ToString(CultureInfo.InvariantCulture) + ")");
                }
                list.Add(new KeyValuePair<int, int>(dx, dy));
            }
            return list;
        }

        /// <summary>Reads the threats; each side must be a unit or composition of the sweep.</summary>
        private static List<FightThreat> ReadThreats(JsonValue node, FightSettings s)
        {
            if (node.Items.Count > 16)
            {
                throw node.Fail("must hold 0 to 16 entries (found " + node.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            List<FightThreat> list = new List<FightThreat>();
            SortedSet<string> seen = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JsonValue item in node.Items)
            {
                item.RejectUnknownKeys("id", "side");
                string id = item.RequireNonEmptyString("id");
                if (!seen.Add(id))
                {
                    throw item.Require("id").Fail("is used twice ('" + id + "')");
                }
                JsonValue sideNode = item.Require("side");
                string side = sideNode.AsString();
                if (!s.HasSide(side))
                {
                    throw sideNode.Fail("must be a unit or composition of the sweep (found '" + side + "')");
                }
                list.Add(new FightThreat(id, side));
            }
            return list;
        }

        /// <summary>True for 1 to 64 characters of a-z, 0-9 and underscore.</summary>
        private static bool IsIdentifier(string id)
        {
            if (id.Length < 1 || id.Length > 64)
            {
                return false;
            }
            foreach (char c in id)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }
            }
            return true;
        }
    }
}

// rts/src/Ai/AiProfile.cs
// A validated skirmish AI profile (rts/data/ai/<id>.json): the decision timing, economy targets, build placement, attack,
// retreat and defence numbers, and one plan per faction (worker, supply building, build order, army composition, tide window).
// Part of Rts.Ai. DataLoader routes the ai/ folder and keeps each file's parsed JsonValue in DataSet.AiProfiles (checking only
// the header and that "id" matches the file name); AiProfile.Parse validates everything else here with the same JsonValue
// Require* helpers, so every error is a DataValidationException naming the file and the field, for example
// "ai/standard.json: field 'attack_army_supply' is missing". Decimals are Fix64 (parsed exactly), never floating point.
// Differences from rts/docs/specs/skirmish_ai.md section 2 (written before the economy, HQ and vision code existed):
//   * no expansion keys (expand_*, max_bases, base_site_taken_radius_tiles, drop_off_building) and no HQ relocation key: the
//     Phase 1 roster has no drop-off structure and the HQ stays Rooted, so the AI plays one base;
//   * added keys for numbers the spec hard-coded or left implicit: field_max_distance_tiles, build_ring_min_tiles,
//     build_ring_max_tiles, field_clearance_tiles, rally_distance_tiles and damage_memory_seconds; the attack staging keys
//     stage_distance_tiles, stage_radius_tiles, stage_gather_fraction and stage_timeout_seconds (the army gathers short of the
//     target so fast and slow units arrive together instead of trickling into the defence); focus_radius_tiles (focus fire on
//     visible enemy army units); and all_in_when_mined_out
//     (Phase 1 maps hold only the main fields, so a mined-out AI attacks with what it has instead of stalling the match).
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>One build order step: keep at least <see cref="Count"/> of a structure once supply used reaches a threshold.</summary>
    public sealed class AiBuildStep
    {
        /// <summary>Definition ID of the structure, for example "skai.roost". A structure (footprint above 0) of the plan's faction.</summary>
        public string StructureId { get; }

        /// <summary>How many of the structure the AI keeps (built or under construction). Unit: structures. Range: 1 to 20.</summary>
        public int Count { get; }

        /// <summary>Supply used at or above which the step starts. Unit: supply. Range: 0 to 200.</summary>
        public int AtSupplyUsed { get; }

        /// <summary>Stores the step.</summary>
        /// <param name="structureId">Structure definition ID.</param>
        /// <param name="count">Structures to keep. Range: 1 to 20.</param>
        /// <param name="atSupplyUsed">Supply threshold. Unit: supply. Range: 0 to 200.</param>
        public AiBuildStep(string structureId, int count, int atSupplyUsed)
        {
            StructureId = structureId;
            Count = count;
            AtSupplyUsed = atSupplyUsed;
        }
    }

    /// <summary>The per-faction part of a profile.</summary>
    public sealed class AiFactionPlan
    {
        /// <summary>Faction this plan is for, for example "skai".</summary>
        public string FactionId { get; }

        /// <summary>Worker unit the HQ trains; must have can_gather. Range: a unit ID of the faction.</summary>
        public string WorkerUnitId { get; }

        /// <summary>Structure built when free supply runs low; must provide supply. Range: a structure ID of the faction.</summary>
        public string SupplyBuildingId { get; }

        /// <summary>Build order steps in priority order; only the first unmet step is considered (strict order).</summary>
        public IReadOnlyList<AiBuildStep> BuildOrder { get; }

        /// <summary>Army mix as integer weights by unit ID, in ordinal ID order. Unit: weight. Range: 0 to 100 each, at least one above 0.</summary>
        public IReadOnlyDictionary<string, int> Composition { get; }

        /// <summary>Lowest tide level at which the AI starts an attack. Unit: fraction (0 low tide, 1 high tide). Range: 0 to 1.</summary>
        public Fix64 AttackTideMin { get; }

        /// <summary>Highest tide level at which the AI starts an attack. Unit: fraction. Range: AttackTideMin to 1.</summary>
        public Fix64 AttackTideMax { get; }

        /// <summary>Army supply at or above which the tide window is ignored. Unit: supply. Range: 1 to 400.</summary>
        public int AttackTideOverrideSupply { get; }

        /// <summary>Stores the plan; <see cref="AiProfile.Parse(JsonValue, ContentCatalog)"/> validates it first.</summary>
        public AiFactionPlan(string factionId, string workerUnitId, string supplyBuildingId, IReadOnlyList<AiBuildStep> buildOrder,
            IReadOnlyDictionary<string, int> composition, Fix64 attackTideMin, Fix64 attackTideMax, int attackTideOverrideSupply)
        {
            FactionId = factionId;
            WorkerUnitId = workerUnitId;
            SupplyBuildingId = supplyBuildingId;
            BuildOrder = buildOrder;
            Composition = composition;
            AttackTideMin = attackTideMin;
            AttackTideMax = attackTideMax;
            AttackTideOverrideSupply = attackTideOverrideSupply;
        }
    }

    /// <summary>A validated AI profile. Immutable; one instance may be shared by several controllers.</summary>
    public sealed class AiProfile
    {
        /// <summary>Profile ID, equal to the file name, for example "aggressive".</summary>
        public string Id { get; }

        /// <summary>Data-root-relative file the profile came from, for error messages, for example "ai/aggressive.json".</summary>
        public string SourceName { get; }

        /// <summary>Time between decisions. Unit: seconds. Range: 0.1 to 5.</summary>
        public Fix64 ThinkIntervalSeconds { get; }

        /// <summary>Most commands one decision may submit; later intents are dropped. Unit: commands. Range: 1 to 200.</summary>
        public int MaxCommandsPerThink { get; }

        /// <summary>Workers sent to one resource field before the next field is used. Unit: workers. Range: 1 to 8.</summary>
        public int WorkersPerField { get; }

        /// <summary>Worker count the AI stops training at. Unit: workers. Range: 1 to 100.</summary>
        public int MaxWorkers { get; }

        /// <summary>Fields farther than this from the own HQ are never worked. Unit: tiles. Range: 2 to 64.</summary>
        public Fix64 FieldMaxDistanceTiles { get; }

        /// <summary>Build supply when free supply is at or below this. Unit: supply. Range: 0 to 40.</summary>
        public int SupplyBuffer { get; }

        /// <summary>Wait before re-trying a structure whose last attempt did not appear. Unit: seconds. Range: 1 to 60.</summary>
        public Fix64 BuildRetrySeconds { get; }

        /// <summary>Innermost Chebyshev ring around the HQ tile searched for a building site. Unit: tiles. Range: 1 to 20.</summary>
        public int BuildRingMinTiles { get; }

        /// <summary>Outermost Chebyshev ring around the HQ tile searched for a building site. Unit: tiles. Range: build_ring_min_tiles to 30.</summary>
        public int BuildRingMaxTiles { get; }

        /// <summary>No structure is placed within this Chebyshev distance of a resource field tile. Unit: tiles. Range: 0 to 10.</summary>
        public int FieldClearanceTiles { get; }

        /// <summary>Massing army waits this far from the HQ toward the enemy spawn. Unit: tiles. Range: 0 to 20.</summary>
        public Fix64 RallyDistanceTiles { get; }

        /// <summary>Army supply at which the AI attacks, before jitter. Unit: supply. Range: 1 to 200.</summary>
        public int AttackArmySupply { get; }

        /// <summary>Extra supply drawn 0 to this from the AI's own generator for each attack wave. Unit: supply. Range: 0 to 40.</summary>
        public int AttackSupplyJitter { get; }

        /// <summary>Retreat when army supply falls below this share of its supply at the start of the attack. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 RetreatArmyFraction { get; }

        /// <summary>Retreat when local own power is below this multiple of local visible enemy power. Unit: ratio. Range: 0 to 2.</summary>
        public Fix64 RetreatPowerRatio { get; }

        /// <summary>The attacking army first gathers this far short of the target (toward the own HQ); 0 attacks straight away. Unit: tiles. Range: 0 to 40.</summary>
        public Fix64 StageDistanceTiles { get; }

        /// <summary>Army units within this distance of the staging point count as gathered. Unit: tiles. Range: 1 to 20.</summary>
        public Fix64 StageRadiusTiles { get; }

        /// <summary>The army pushes from the staging point once this share of its supply has gathered there. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 StageGatherFraction { get; }

        /// <summary>The army pushes anyway this long after the attack started. Unit: seconds. Range: 0 to 120.</summary>
        public Fix64 StageTimeoutSeconds { get; }

        /// <summary>Focus fire reach: an army unit attacks the nearest visible enemy army unit within this distance; 0 turns focus fire off. Unit: tiles. Range: 0 to 20.</summary>
        public Fix64 FocusRadiusTiles { get; }

        /// <summary>Radius used for the retreat power test and the siege test. Unit: tiles. Range: 4 to 40.</summary>
        public Fix64 EngagementRadiusTiles { get; }

        /// <summary>Visible enemies this close to an own structure or HQ are a threat. Unit: tiles. Range: 4 to 40.</summary>
        public Fix64 DefendRadiusTiles { get; }

        /// <summary>Threat supply that recalls an attacking army home. Unit: supply. Range: 1 to 100.</summary>
        public int DefendRecallSupply { get; }

        /// <summary>How long a hit on an own unit inside the base counts as a threat. Unit: seconds. Range: 0 to 30.</summary>
        public Fix64 DamageMemorySeconds { get; }

        /// <summary>Time after a retreat before the next attack may start. Unit: seconds. Range: 0 to 120.</summary>
        public Fix64 RegroupSeconds { get; }

        /// <summary>
        /// When true and no field within field_max_distance_tiles of the HQ has resources left (the base is mined out, so no
        /// reinforcements will come), the AI goes all in: it attacks with any army regardless of the threshold and the tide window
        /// and never retreats. Unit: boolean. Range: true or false.
        /// </summary>
        public bool AllInWhenMinedOut { get; }

        /// <summary>Per-faction plans by faction ID, in ordinal order.</summary>
        public IReadOnlyDictionary<string, AiFactionPlan> Factions { get; }

        /// <summary>Builds the profile from its validated parts; see <see cref="Parse(JsonValue, ContentCatalog)"/>.</summary>
        private AiProfile(JsonValue root, string id, IReadOnlyDictionary<string, AiFactionPlan> factions)
        {
            Id = id;
            SourceName = root.SourceName;
            Factions = factions;
            ThinkIntervalSeconds = root.RequireFix64("think_interval_seconds", "0.1", "5");
            MaxCommandsPerThink = root.RequireInt("max_commands_per_think", 1, 200);
            WorkersPerField = root.RequireInt("workers_per_field", 1, 8);
            MaxWorkers = root.RequireInt("max_workers", 1, 100);
            FieldMaxDistanceTiles = root.RequireFix64("field_max_distance_tiles", "2", "64");
            SupplyBuffer = root.RequireInt("supply_buffer", 0, 40);
            BuildRetrySeconds = root.RequireFix64("build_retry_seconds", "1", "60");
            BuildRingMinTiles = root.RequireInt("build_ring_min_tiles", 1, 20);
            BuildRingMaxTiles = root.RequireInt("build_ring_max_tiles", BuildRingMinTiles, 30);
            FieldClearanceTiles = root.RequireInt("field_clearance_tiles", 0, 10);
            RallyDistanceTiles = root.RequireFix64("rally_distance_tiles", "0", "20");
            AttackArmySupply = root.RequireInt("attack_army_supply", 1, 200);
            AttackSupplyJitter = root.RequireInt("attack_supply_jitter", 0, 40);
            RetreatArmyFraction = root.RequireFix64("retreat_army_fraction", "0", "1");
            RetreatPowerRatio = root.RequireFix64("retreat_power_ratio", "0", "2");
            EngagementRadiusTiles = root.RequireFix64("engagement_radius_tiles", "4", "40");
            StageDistanceTiles = root.RequireFix64("stage_distance_tiles", "0", "40");
            FocusRadiusTiles = root.RequireFix64("focus_radius_tiles", "0", "20");
            StageRadiusTiles = root.RequireFix64("stage_radius_tiles", "1", "20");
            StageGatherFraction = root.RequireFix64("stage_gather_fraction", "0", "1");
            StageTimeoutSeconds = root.RequireFix64("stage_timeout_seconds", "0", "120");
            DefendRadiusTiles = root.RequireFix64("defend_radius_tiles", "4", "40");
            DefendRecallSupply = root.RequireInt("defend_recall_supply", 1, 100);
            DamageMemorySeconds = root.RequireFix64("damage_memory_seconds", "0", "30");
            RegroupSeconds = root.RequireFix64("regroup_seconds", "0", "120");
            AllInWhenMinedOut = root.RequireBool("all_in_when_mined_out");
        }

        /// <summary>Parses and validates profile text. Throws <see cref="DataValidationException"/> naming the file and the field.</summary>
        /// <param name="text">The profile file's text (JSON with comments).</param>
        /// <param name="sourceName">Data-root-relative path, for example "ai/aggressive.json"; the id must match its file name.</param>
        /// <param name="content">Units and factions the IDs are checked against.</param>
        public static AiProfile Parse(string text, string sourceName, ContentCatalog content)
        {
            return Parse(JsonReader.Parse(text, sourceName), content);
        }

        /// <summary>
        /// Validates a parsed profile tree (as stored in <see cref="DataSet.AiProfiles"/>). Throws <see cref="DataValidationException"/>
        /// naming the file and the field on any problem: unknown or missing keys, values out of range, and IDs that are unknown, belong
        /// to another faction or have the wrong kind.
        /// </summary>
        /// <param name="root">The profile file's root object.</param>
        /// <param name="content">Units and factions the IDs are checked against.</param>
        public static AiProfile Parse(JsonValue root, ContentCatalog content)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            if (root.Kind != JsonKind.Object)
            {
                throw root.Fail("the top level must be a JSON object");
            }
            if (root.Require(DataLoader.SchemaVersionKey).AsInt() != DataLoader.SupportedSchemaVersion)
            {
                throw root.Require(DataLoader.SchemaVersionKey).Fail("must be " + DataLoader.SupportedSchemaVersion);
            }
            root.RequireNonEmptyString(DataLoader.DocKey);
            root.RejectUnknownKeys("id", "think_interval_seconds", "max_commands_per_think", "workers_per_field", "max_workers",
                "field_max_distance_tiles", "supply_buffer", "build_retry_seconds", "build_ring_min_tiles", "build_ring_max_tiles",
                "field_clearance_tiles", "rally_distance_tiles", "attack_army_supply", "attack_supply_jitter", "retreat_army_fraction",
                "retreat_power_ratio", "engagement_radius_tiles", "stage_distance_tiles", "stage_radius_tiles", "stage_gather_fraction",
                "stage_timeout_seconds", "focus_radius_tiles", "defend_radius_tiles", "defend_recall_supply", "damage_memory_seconds",
                "regroup_seconds", "all_in_when_mined_out", "factions");
            // Profile ID as written; must equal the file name without folder and extension.
            string id = root.RequireNonEmptyString("id");
            // File name stem the id must match, from the source path.
            string expected = StemOf(root.SourceName);
            if (expected.Length > 0 && !string.Equals(id, expected, StringComparison.Ordinal))
            {
                throw root.Require("id").Fail("must be '" + expected + "' to match the file path (found '" + id + "')");
            }
            // The factions object: one plan per faction ID.
            JsonValue factionsNode = root.Require("factions");
            // Plans by faction ID in ordinal order.
            SortedDictionary<string, AiFactionPlan> plans = new SortedDictionary<string, AiFactionPlan>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in factionsNode.Members)
            {
                plans.Add(pair.Key, ParseFaction(pair.Key, pair.Value, content));
            }
            if (plans.Count == 0)
            {
                throw factionsNode.Fail("must hold a plan for at least one faction");
            }
            return new AiProfile(root, id, plans);
        }

        /// <summary>
        /// The profile <paramref name="id"/> from a loaded data set, validated against its content. Throws
        /// <see cref="DataValidationException"/> when the file is missing or invalid.
        /// </summary>
        /// <param name="data">The loaded data folder.</param>
        /// <param name="id">Profile ID, the file name under rts/data/ai without ".json".</param>
        public static AiProfile FromDataSet(DataSet data, string id)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (!data.AiProfiles.TryGetValue(id, out JsonValue? root))
            {
                throw new DataValidationException(DataLoader.AiFolder + "/" + id + ".json", string.Empty, "is not a loaded AI profile");
            }
            return Parse(root, data.Content);
        }

        /// <summary>The plan for <paramref name="factionId"/>; throws <see cref="DataValidationException"/> naming this file when it has none.</summary>
        /// <param name="factionId">A faction ID, for example "water".</param>
        public AiFactionPlan RequireFaction(string factionId)
        {
            if (!Factions.TryGetValue(factionId, out AiFactionPlan? plan))
            {
                throw new DataValidationException(SourceName, "factions." + factionId, "is missing; this profile has no plan for that faction");
            }
            return plan;
        }

        /// <summary>Validates one faction plan object.</summary>
        /// <param name="factionId">The key the plan is stored under.</param>
        /// <param name="node">The plan object.</param>
        /// <param name="content">Units and factions to check IDs against.</param>
        private static AiFactionPlan ParseFaction(string factionId, JsonValue node, ContentCatalog content)
        {
            if (!content.Factions.TryGetValue(factionId, out FactionDefinition? faction))
            {
                throw node.Fail("names an unknown faction (no factions/" + factionId + ".json)");
            }
            if (node.Kind != JsonKind.Object)
            {
                throw node.Fail("must be an object");
            }
            node.RejectUnknownKeys("worker_unit", "supply_building", "build_order", "composition", "attack_tide_min", "attack_tide_max",
                "attack_tide_override_supply");
            // Worker unit ID, checked to be a gatherer of this faction.
            string worker = node.RequireNonEmptyString("worker_unit");
            // Definition of the worker unit.
            UnitDefinition workerDefinition = RequireUnit(node.Require("worker_unit"), worker, faction.Id, content);
            if (!workerDefinition.Economy.CanGather)
            {
                throw node.Require("worker_unit").Fail("must be a unit with can_gather (found '" + worker + "')");
            }
            // Supply structure ID, checked to be a structure that provides supply.
            string supply = node.RequireNonEmptyString("supply_building");
            // Definition of the supply structure.
            UnitDefinition supplyDefinition = RequireUnit(node.Require("supply_building"), supply, faction.Id, content);
            if (supplyDefinition.Economy.FootprintTiles <= 0 || supplyDefinition.Economy.ProvidesSupply <= 0)
            {
                throw node.Require("supply_building").Fail("must be a structure with provides_supply above 0 (found '" + supply + "')");
            }
            // Build order steps in file order.
            List<AiBuildStep> steps = new List<AiBuildStep>();
            foreach (JsonValue stepNode in node.RequireArray("build_order"))
            {
                stepNode.RejectUnknownKeys("structure", "count", "at_supply_used");
                // Structure ID of this step.
                string structure = stepNode.RequireNonEmptyString("structure");
                // Definition of the step's structure.
                UnitDefinition structureDefinition = RequireUnit(stepNode.Require("structure"), structure, faction.Id, content);
                if (structureDefinition.Economy.FootprintTiles <= 0)
                {
                    throw stepNode.Require("structure").Fail("must be a structure (footprint above 0; found '" + structure + "')");
                }
                steps.Add(new AiBuildStep(structure, stepNode.RequireInt("count", 1, 20), stepNode.RequireInt("at_supply_used", 0, 200)));
            }
            // The composition object: unit ID to weight.
            JsonValue compositionNode = node.Require("composition");
            // Weights by unit ID in ordinal order.
            SortedDictionary<string, int> composition = new SortedDictionary<string, int>(StringComparer.Ordinal);
            // Sum of all weights; must end above 0. Unit: weight. Range: 0 to 100 per unit.
            int totalWeight = 0;
            foreach (KeyValuePair<string, JsonValue> pair in compositionNode.Members)
            {
                // Definition of the composition unit.
                UnitDefinition unit = RequireUnit(pair.Value, pair.Key, faction.Id, content);
                if (unit.Economy.FootprintTiles > 0 || unit.Hq != null || unit.Economy.CanGather)
                {
                    throw pair.Value.Fail("must be an army unit (mobile, not the HQ, not a worker; found '" + pair.Key + "')");
                }
                if (!IsTrainedByFaction(pair.Key, faction.Id, content))
                {
                    throw pair.Value.Fail("is not in the trains list of any " + faction.Id + " structure");
                }
                // Weight of this unit in the mix. Unit: weight. Range: 0 to 100.
                int weight = pair.Value.AsInt(0, 100);
                composition.Add(pair.Key, weight);
                totalWeight += weight;
            }
            if (totalWeight <= 0)
            {
                throw compositionNode.Fail("must give at least one unit a weight above 0");
            }
            // Tide window bounds. Unit: fraction. Range: 0 to 1, min at most max.
            Fix64 tideMin = node.RequireFix64("attack_tide_min", "0", "1");
            // Upper bound of the tide window. Unit: fraction. Range: 0 to 1.
            Fix64 tideMax = node.RequireFix64("attack_tide_max", "0", "1");
            if (tideMax < tideMin)
            {
                throw node.Require("attack_tide_max").Fail("must be at least attack_tide_min");
            }
            return new AiFactionPlan(faction.Id, worker, supply, steps.AsReadOnly(), composition, tideMin, tideMax,
                node.RequireInt("attack_tide_override_supply", 1, 400));
        }

        /// <summary>The unit definition <paramref name="id"/>; fails on <paramref name="node"/> when it is unknown or of another faction.</summary>
        private static UnitDefinition RequireUnit(JsonValue node, string id, string factionId, ContentCatalog content)
        {
            if (!content.Units.TryGetValue(id, out UnitDefinition? definition))
            {
                throw node.Fail("names an unknown unit '" + id + "'");
            }
            if (!string.Equals(definition.FactionId, factionId, StringComparison.Ordinal))
            {
                throw node.Fail("names '" + id + "', which belongs to faction '" + definition.FactionId + "', not '" + factionId + "'");
            }
            return definition;
        }

        /// <summary>True when some structure of the faction lists <paramref name="unitId"/> in its trains list.</summary>
        private static bool IsTrainedByFaction(string unitId, string factionId, ContentCatalog content)
        {
            foreach (UnitDefinition producer in content.Units.Values)
            {
                if (!string.Equals(producer.FactionId, factionId, StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (string trained in producer.Economy.Trains)
                {
                    if (string.Equals(trained, unitId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>File name of <paramref name="path"/> without folders and without ".json"; empty when the path has no .json name.</summary>
        private static string StemOf(string path)
        {
            // Index of the last folder separator, or -1.
            int slash = path.LastIndexOf('/');
            // File name part of the path.
            string name = slash >= 0 ? path.Substring(slash + 1) : path;
            return name.EndsWith(".json", StringComparison.Ordinal) ? name.Substring(0, name.Length - ".json".Length) : string.Empty;
        }
    }
}

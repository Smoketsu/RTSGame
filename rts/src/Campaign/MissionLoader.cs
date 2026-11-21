// rts/src/Campaign/MissionLoader.cs
// Parses and validates a mission file (rts/campaign/missions/<id>.json) into an immutable MissionDefinition
// (rts/docs/specs/campaign_scripting.md sections 2 and 6). Part of Rts.Campaign. It takes text, never touches the file system (the host reads
// the file, as the balance harness does), reuses Rts.Sim's JsonReader so comments, exact numbers and file/field error messages match game data,
// and parses every command object through CommandScript.ParseCommandObject so missions and command scripts accept the same commands.
// Every problem is a DataValidationException naming the mission path and a field path such as triggers[2].actions[0].commands[0].x.
// Engine-free; Fix64 and integers only; no static state.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Teams;

namespace Rts.Campaign
{
    /// <summary>Reads mission text into a <see cref="MissionDefinition"/>.</summary>
    public static class MissionLoader
    {
        /// <summary>Largest nesting of conditions (the root counts as 1). Unit: levels. Range: fixed by the spec, 3.</summary>
        private const int MaxConditionDepth = 3;

        /// <summary>Event kinds an event_count condition may name, in the order the error text lists them. Range: the nine kinds of the spec.</summary>
        private static readonly string[] EventKinds =
        {
            "unit_spawned", "unit_arrived", "resource_deposited", "unit_trained", "structure_completed", "unit_killed", "hq_state_changed", "core_captured", "cycle_started",
        };

        /// <summary>Everything a parse needs besides the node being read.</summary>
        private sealed class Context
        {
            /// <summary>The loaded game data (units, factions, maps, AI profiles).</summary>
            public readonly DataSet Data;

            /// <summary>The map the mission plays on; coordinates are checked against it.</summary>
            public readonly MapDefinition Map;

            /// <summary>Command readers for faction commands.</summary>
            public readonly IReadOnlyList<IScriptCommandParser> Parsers;

            /// <summary>Mission file path used in messages passed on to the command readers.</summary>
            public readonly string Source;

            /// <summary>Controller by player slot, in ascending slot order. Range: the mission's players.</summary>
            public readonly SortedDictionary<PlayerId, MissionPlayer> Slots = new SortedDictionary<PlayerId, MissionPlayer>();

            /// <summary>Objective IDs read so far, in file order, mapped to their index. Range: 0 to 63 entries.</summary>
            public readonly SortedDictionary<string, int> ObjectiveIndexes = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Builds the context.</summary>
            public Context(DataSet data, MapDefinition map, IReadOnlyList<IScriptCommandParser> parsers, string source)
            {
                Data = data;
                Map = map;
                Parsers = parsers;
                Source = source;
            }
        }

        /// <summary>
        /// Parses and validates mission text. Throws <see cref="DataValidationException"/> naming <paramref name="sourcePath"/> and the field on any
        /// problem (also for JSON syntax errors); throws <see cref="ArgumentNullException"/> for a null argument.
        /// </summary>
        /// <param name="text">The mission file's text (JSON with comments).</param>
        /// <param name="sourcePath">The file path; its name without folder and .json must equal the mission's id. Range: non-empty text.</param>
        /// <param name="data">The loaded game data the mission names IDs from.</param>
        /// <param name="parsers">Readers for faction commands, normally BuiltInMechanics.ScriptParsers.</param>
        public static MissionDefinition Parse(string text, string sourcePath, DataSet data, IReadOnlyList<IScriptCommandParser> parsers)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (parsers == null)
            {
                throw new ArgumentNullException(nameof(parsers));
            }
            // Path with forward slashes, as every other message shows it.
            string source = sourcePath.Replace('\\', '/');
            JsonValue root = JsonReader.Parse(text, source);
            if (root.Kind != JsonKind.Object)
            {
                throw root.Fail("the top level must be a JSON object");
            }
            root.RejectUnknownKeys("id", "display_name", "map", "seed", "max_seconds", "planetary_cycles", "players", "setup", "objectives", "triggers", "victory_objective",
                "defeat_when");
            if (root.Require(DataLoader.SchemaVersionKey).AsInt() != DataLoader.SupportedSchemaVersion)
            {
                throw root.Require(DataLoader.SchemaVersionKey).Fail("must be " + DataLoader.SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture));
            }
            root.RequireNonEmptyString(DataLoader.DocKey);

            // Mission ID and the file stem it must equal.
            string id = root.RequireNonEmptyString("id");
            // File name without folders and without the .json ending.
            string stem = source.Substring(source.LastIndexOf('/') + 1);
            if (stem.EndsWith(".json", StringComparison.Ordinal))
            {
                stem = stem.Substring(0, stem.Length - 5);
            }
            if (id != stem)
            {
                throw root.Require("id").Fail("must equal the file name without .json (\"" + stem + "\", found \"" + id + "\")");
            }
            if (!IsSnake(id))
            {
                throw root.Require("id").Fail("must be snake_case: a-z, 0-9 and '_', starting with a letter");
            }
            // Display name shown to players. Unit: characters. Range: 1 to 60.
            string displayName = RequireText(root, "display_name", 1, 60);
            // Map ID played.
            string mapId = root.RequireNonEmptyString("map");
            if (!data.Maps.TryGetValue(mapId, out MapDefinition? map))
            {
                throw root.Require("map").Fail("is not a loaded map (found \"" + mapId + "\")");
            }
            // Simulation seed. Range: 0 to 2147483647.
            int seed = root.RequireInt("seed", 0, int.MaxValue);
            // Time limit. Unit: seconds. Range: 30 to 7200.
            int maxSeconds = root.RequireInt("max_seconds", 30, 7200);
            // Whether the data set's Planetary Cycles are passed to the simulation.
            bool cycles = root.RequireBool("planetary_cycles");

            Context ctx = new Context(data, map, parsers, source);
            List<MissionPlayer> players = ReadPlayers(root.Require("players"), ctx);

            // Setup command objects, applied on tick 0 by the script issuer.
            List<JsonValue> setup = new List<JsonValue>();
            IReadOnlyList<JsonValue> setupItems = root.RequireArray("setup");
            CheckCount(root.Require("setup"), setupItems.Count, 0, 500);
            foreach (JsonValue node in setupItems)
            {
                ValidateCommand(node, ctx, PlayerId.None);
                setup.Add(node);
            }

            List<ObjectiveDefinition> objectives = ReadObjectives(root.Require("objectives"), ctx);
            List<TriggerDefinition> triggers = ReadTriggers(root.Require("triggers"), ctx);

            // Objective whose completion wins.
            string victoryId = root.RequireNonEmptyString("victory_objective");
            if (!ctx.ObjectiveIndexes.TryGetValue(victoryId, out int victory))
            {
                throw root.Require("victory_objective").Fail("names no objective (found \"" + victoryId + "\")");
            }
            ConditionDefinition? defeat = null;
            if (root.TryGetMember("defeat_when", out JsonValue? defeatNode))
            {
                defeat = ReadCondition(defeatNode, ctx, 1, 0);
            }
            return new MissionDefinition(id, displayName, mapId, (ulong)seed, maxSeconds, cycles, players, setup, objectives, triggers, victory, defeat, parsers, source);
        }

        // ---------------------------------------------------------------- players

        /// <summary>Reads the players list: 1 to 8 entries, unique slots inside the map's spawn count, known factions and controllers, at least one human.</summary>
        private static List<MissionPlayer> ReadPlayers(JsonValue array, Context ctx)
        {
            // The entries of the list.
            IReadOnlyList<JsonValue> items = array.Items;
            CheckCount(array, items.Count, 1, 8);
            // Players in file order.
            List<MissionPlayer> players = new List<MissionPlayer>();
            // True once a human slot is seen.
            bool anyHuman = false;
            foreach (JsonValue node in items)
            {
                node.RejectUnknownKeys("id", "team", "faction", "name", "controller");
                // Player slot. Unit: slot number. Range: 0 to the map's spawn count - 1 (and at most 254).
                int slot = node.RequireInt("id", 0, PlayerId.MaxValue);
                if (slot >= ctx.Map.Spawns.Count)
                {
                    throw node.Require("id").Fail("must be below the map's spawn count (" + ctx.Map.Spawns.Count.ToString(CultureInfo.InvariantCulture) + ")");
                }
                // Team number. Range: 0 to 254.
                int team = node.RequireInt("team", 0, TeamId.MaxValue);
                // Faction ID, which must exist in the data set.
                string faction = node.RequireNonEmptyString("faction");
                if (!ctx.Data.Content.Factions.ContainsKey(faction))
                {
                    throw node.Require("faction").Fail("is not a loaded faction (found \"" + faction + "\")");
                }
                // Display name. Range: any string; "Player <slot>" when absent.
                string name = node.OptionalString("name", "Player " + slot.ToString(CultureInfo.InvariantCulture));
                // Controller text. Range: "human", "script" or "ai:<profile>".
                string controller = node.RequireNonEmptyString("controller");
                // AI profile of an "ai:" controller.
                string? profile = null;
                if (controller.StartsWith("ai:", StringComparison.Ordinal))
                {
                    profile = controller.Substring(3);
                    if (!ctx.Data.AiProfiles.ContainsKey(profile))
                    {
                        throw node.Require("controller").Fail("names no loaded AI profile (found \"" + profile + "\")");
                    }
                }
                else if (controller != "human" && controller != "script")
                {
                    throw node.Require("controller").Fail("must be \"human\", \"script\" or \"ai:<profile>\" (found \"" + controller + "\")");
                }
                anyHuman |= controller == "human";
                // The slot as a player id.
                PlayerId playerId = new PlayerId((byte)slot);
                MissionPlayer player = new MissionPlayer(new PlayerSetup(playerId, new TeamId((byte)team), faction, name), controller, profile);
                if (!ctx.Slots.TryAdd(playerId, player))
                {
                    throw node.Require("id").Fail("is a duplicate player slot (" + slot.ToString(CultureInfo.InvariantCulture) + ")");
                }
                players.Add(player);
            }
            if (!anyHuman)
            {
                throw array.Fail("must list at least one player with controller \"human\"");
            }
            return players;
        }

        // ---------------------------------------------------------------- objectives

        /// <summary>Reads the objectives list: 1 to 64 entries with unique IDs; requires may only name earlier objectives.</summary>
        private static List<ObjectiveDefinition> ReadObjectives(JsonValue array, Context ctx)
        {
            // The entries of the list.
            IReadOnlyList<JsonValue> items = array.Items;
            CheckCount(array, items.Count, 1, 64);
            // Objectives in file order.
            List<ObjectiveDefinition> objectives = new List<ObjectiveDefinition>();
            foreach (JsonValue node in items)
            {
                node.RejectUnknownKeys("id", "text", "requires", "complete_when", "fail_when", "hint_area");
                // Objective ID. Range: unique snake_case.
                string id = node.RequireNonEmptyString("id");
                if (!IsSnake(id))
                {
                    throw node.Require("id").Fail("must be snake_case: a-z, 0-9 and '_', starting with a letter (found \"" + id + "\")");
                }
                // Index this objective will have.
                int index = objectives.Count;
                if (ctx.ObjectiveIndexes.ContainsKey(id))
                {
                    throw node.Require("id").Fail("is a duplicate objective ID (\"" + id + "\")");
                }
                // Panel text. Unit: characters. Range: 1 to 120.
                string text = RequireText(node, "text", 1, 120);
                // Indexes of the required objectives.
                List<int> requires = new List<int>();
                foreach (JsonValue required in node.RequireArray("requires"))
                {
                    // ID of the required objective.
                    string requiredId = required.AsString();
                    if (!ctx.ObjectiveIndexes.TryGetValue(requiredId, out int requiredIndex))
                    {
                        throw required.Fail("must name an objective that appears earlier in the file (found \"" + requiredId + "\")");
                    }
                    requires.Add(requiredIndex);
                }
                ConditionDefinition complete = ReadCondition(node.Require("complete_when"), ctx, 1, 0);
                ConditionDefinition? fail = node.TryGetMember("fail_when", out JsonValue? failNode) ? ReadCondition(failNode, ctx, 1, 0) : null;
                HintArea? hint = null;
                if (node.TryGetMember("hint_area", out JsonValue? hintNode))
                {
                    hintNode.RejectUnknownKeys("x", "y", "radius_tiles");
                    hint = new HintArea(ReadPoint(hintNode, ctx), RequireRadius(hintNode));
                }
                ctx.ObjectiveIndexes.Add(id, index);
                objectives.Add(new ObjectiveDefinition(id, index, text, requires, complete, fail, hint));
            }
            return objectives;
        }

        // ---------------------------------------------------------------- conditions

        /// <summary>
        /// Reads one condition node and its children. Nodes are numbered in preorder from <paramref name="firstIndex"/>.
        /// </summary>
        /// <param name="node">The condition object.</param>
        /// <param name="ctx">Parse context.</param>
        /// <param name="depth">Nesting level of this node; the root is 1. Range: 1 to 3.</param>
        /// <param name="firstIndex">Preorder index of this node within its tree. Range: 0 upward.</param>
        private static ConditionDefinition ReadCondition(JsonValue node, Context ctx, int depth, int firstIndex)
        {
            if (node.Kind != JsonKind.Object)
            {
                throw node.Fail("must be a condition object");
            }
            // Condition type name.
            string type = node.RequireString("type");
            // Filters and values read for the node; unused ones keep these defaults.
            string eventName = string.Empty;
            int minCount = 0;
            bool hasOwner = false;
            PlayerId owner = PlayerId.None;
            string? definition = null;
            bool hasUnit = false;
            EntityId unit = EntityId.None;
            Fix64 x = Fix64.Zero;
            Fix64 y = Fix64.Zero;
            Fix64 radius = Fix64.Zero;
            int common = 0;
            int rare = 0;
            Fix64 seconds = Fix64.Zero;
            PlayerState state = PlayerState.Active;
            List<ConditionDefinition> children = new List<ConditionDefinition>();
            // Number of nodes in this subtree so far (this node counts).
            int nodeCount = 1;
            ConditionKind kind;
            switch (type)
            {
                case "event_count":
                    kind = ConditionKind.EventCount;
                    node.RejectUnknownKeys("type", "event", "min_count", "owner", "definition", "unit");
                    eventName = node.RequireNonEmptyString("event");
                    if (Array.IndexOf(EventKinds, eventName) < 0)
                    {
                        throw node.Require("event").Fail("must be one of \"" + string.Join("\", \"", EventKinds) + "\" (found \"" + eventName + "\")");
                    }
                    minCount = node.RequireInt("min_count", 1, 1000);
                    hasOwner = node.TryGetMember("owner", out JsonValue? eventOwner);
                    if (hasOwner)
                    {
                        owner = ReadSlot(eventOwner!, ctx);
                    }
                    definition = ReadOptionalUnitDefinition(node, ctx);
                    hasUnit = node.TryGetMember("unit", out JsonValue? eventUnit);
                    if (hasUnit)
                    {
                        unit = new EntityId((uint)eventUnit!.AsInt(1, int.MaxValue));
                    }
                    CheckEventFilters(node, eventName, hasOwner, definition != null, hasUnit);
                    break;
                case "units_in_area":
                    kind = ConditionKind.UnitsInArea;
                    node.RejectUnknownKeys("type", "owner", "definition", "x", "y", "radius_tiles", "min_count");
                    hasOwner = true;
                    owner = ReadSlot(node.Require("owner"), ctx);
                    definition = ReadOptionalUnitDefinition(node, ctx);
                    FixVec2 areaCentre = ReadPoint(node, ctx);
                    x = areaCentre.X;
                    y = areaCentre.Y;
                    radius = RequireRadius(node);
                    minCount = node.RequireInt("min_count", 1, 500);
                    break;
                case "resources_at_least":
                    kind = ConditionKind.ResourcesAtLeast;
                    node.RejectUnknownKeys("type", "player", "common", "rare");
                    owner = ReadSlot(node.Require("player"), ctx);
                    common = node.RequireInt("common", 0, 1000000);
                    rare = node.RequireInt("rare", 0, 1000000);
                    break;
                case "seconds_active":
                    kind = ConditionKind.SecondsActive;
                    node.RejectUnknownKeys("type", "seconds");
                    seconds = RequireFix(node, "seconds", Fix64.Zero, Fix64.FromInt(3600));
                    break;
                case "camera_in_area":
                    kind = ConditionKind.CameraInArea;
                    node.RejectUnknownKeys("type", "x", "y", "radius_tiles");
                    FixVec2 cameraCentre = ReadPoint(node, ctx);
                    x = cameraCentre.X;
                    y = cameraCentre.Y;
                    radius = RequireRadius(node);
                    break;
                case "selection_contains":
                    kind = ConditionKind.SelectionContains;
                    node.RejectUnknownKeys("type", "owner", "definition", "min_count");
                    hasOwner = true;
                    owner = ReadSlot(node.Require("owner"), ctx);
                    definition = ReadOptionalUnitDefinition(node, ctx);
                    minCount = node.RequireInt("min_count", 1, 500);
                    break;
                case "player_state":
                    kind = ConditionKind.PlayerStateIs;
                    node.RejectUnknownKeys("type", "player", "state");
                    owner = ReadSlot(node.Require("player"), ctx);
                    state = ReadPlayerState(node.Require("state"));
                    break;
                case "all_of":
                case "any_of":
                    kind = type == "all_of" ? ConditionKind.AllOf : ConditionKind.AnyOf;
                    node.RejectUnknownKeys("type", "conditions");
                    if (depth >= MaxConditionDepth)
                    {
                        throw node.Fail("nests conditions deeper than " + MaxConditionDepth.ToString(CultureInfo.InvariantCulture) + " levels");
                    }
                    // The child list.
                    IReadOnlyList<JsonValue> childNodes = node.RequireArray("conditions");
                    CheckCount(node.Require("conditions"), childNodes.Count, 1, 8);
                    foreach (JsonValue childNode in childNodes)
                    {
                        ConditionDefinition child = ReadCondition(childNode, ctx, depth + 1, firstIndex + nodeCount);
                        nodeCount += child.NodeCount;
                        children.Add(child);
                    }
                    break;
                default:
                    throw node.Require("type").Fail("must be one of \"event_count\", \"units_in_area\", \"resources_at_least\", \"seconds_active\", \"camera_in_area\", "
                        + "\"selection_contains\", \"player_state\", \"all_of\" or \"any_of\" (found \"" + type + "\")");
            }
            return new ConditionDefinition(kind, firstIndex, nodeCount, eventName, minCount, hasOwner, owner, definition, hasUnit, unit, x, y, radius, common, rare, seconds, state, children);
        }

        /// <summary>Rejects owner, definition and unit filters that the event kind cannot be matched on (spec section 2: unit_killed has no definition filter, cycle_started no filters at all).</summary>
        private static void CheckEventFilters(JsonValue node, string eventName, bool owner, bool definition, bool unit)
        {
            // Filters the kind supports.
            bool ownerOk = eventName != "cycle_started";
            bool definitionOk = eventName != "unit_killed" && eventName != "core_captured" && eventName != "cycle_started" && eventName != "hq_state_changed";
            bool unitOk = eventName != "core_captured" && eventName != "cycle_started";
            if (owner && !ownerOk)
            {
                throw node.Require("owner").Fail("is not a filter of event \"" + eventName + "\"");
            }
            if (definition && !definitionOk)
            {
                throw node.Require("definition").Fail("is not a filter of event \"" + eventName + "\"");
            }
            if (unit && !unitOk)
            {
                throw node.Require("unit").Fail("is not a filter of event \"" + eventName + "\"");
            }
        }

        /// <summary>The optional definition key as a unit definition ID that exists, or null.</summary>
        private static string? ReadOptionalUnitDefinition(JsonValue node, Context ctx)
        {
            if (!node.TryGetMember("definition", out JsonValue? definitionNode))
            {
                return null;
            }
            // The ID as written.
            string id = definitionNode.AsString();
            if (!ctx.Data.Content.Units.ContainsKey(id))
            {
                throw definitionNode.Fail("is not a loaded unit definition (found \"" + id + "\")");
            }
            return id;
        }

        /// <summary>A player slot that is one of the mission's players.</summary>
        private static PlayerId ReadSlot(JsonValue node, Context ctx)
        {
            // The slot as written. Unit: slot number. Range: 0 to 254.
            PlayerId slot = new PlayerId((byte)node.AsInt(0, PlayerId.MaxValue));
            if (!ctx.Slots.ContainsKey(slot))
            {
                throw node.Fail("names no player of this mission (found " + slot.Value.ToString(CultureInfo.InvariantCulture) + ")");
            }
            return slot;
        }

        /// <summary>A player state name: active, surrendered, disconnected, defeated or eliminated.</summary>
        private static PlayerState ReadPlayerState(JsonValue node)
        {
            // The name as written.
            string name = node.AsString();
            switch (name)
            {
                case "active": return PlayerState.Active;
                case "surrendered": return PlayerState.Surrendered;
                case "disconnected": return PlayerState.Disconnected;
                case "defeated": return PlayerState.Defeated;
                case "eliminated": return PlayerState.Eliminated;
                default:
                    throw node.Fail("must be \"active\", \"surrendered\", \"disconnected\", \"defeated\" or \"eliminated\" (found \"" + name + "\")");
            }
        }

        // ---------------------------------------------------------------- triggers and actions

        /// <summary>Reads the triggers list: 0 to 256 entries with unique IDs.</summary>
        private static List<TriggerDefinition> ReadTriggers(JsonValue array, Context ctx)
        {
            // The entries of the list.
            IReadOnlyList<JsonValue> items = array.Items;
            CheckCount(array, items.Count, 0, 256);
            // Triggers in file order.
            List<TriggerDefinition> triggers = new List<TriggerDefinition>();
            // IDs seen so far.
            SortedSet<string> ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JsonValue node in items)
            {
                node.RejectUnknownKeys("id", "when", "actions");
                // Trigger ID. Range: unique snake_case.
                string id = node.RequireNonEmptyString("id");
                if (!IsSnake(id))
                {
                    throw node.Require("id").Fail("must be snake_case: a-z, 0-9 and '_', starting with a letter (found \"" + id + "\")");
                }
                if (!ids.Add(id))
                {
                    throw node.Require("id").Fail("is a duplicate trigger ID (\"" + id + "\")");
                }
                // The when object; exactly one key.
                JsonValue when = node.Require("when");
                if (when.Members.Count != 1)
                {
                    throw when.Fail("must have exactly one of \"mission_started\", \"objective_activated\", \"objective_completed\" or \"condition\"");
                }
                TriggerWhenKind whenKind;
                int objectiveIndex = -1;
                ConditionDefinition? condition = null;
                // The single member of when.
                JsonValue only = default!;
                string key = string.Empty;
                foreach (KeyValuePair<string, JsonValue> pair in when.Members)
                {
                    key = pair.Key;
                    only = pair.Value;
                }
                switch (key)
                {
                    case "mission_started":
                        whenKind = TriggerWhenKind.MissionStarted;
                        if (!only.AsBool())
                        {
                            throw only.Fail("must be true");
                        }
                        break;
                    case "objective_activated":
                    case "objective_completed":
                        whenKind = key == "objective_activated" ? TriggerWhenKind.ObjectiveActivated : TriggerWhenKind.ObjectiveCompleted;
                        // Objective ID named.
                        string objectiveId = only.AsString();
                        if (!ctx.ObjectiveIndexes.TryGetValue(objectiveId, out objectiveIndex))
                        {
                            throw only.Fail("names no objective (found \"" + objectiveId + "\")");
                        }
                        break;
                    case "condition":
                        whenKind = TriggerWhenKind.Condition;
                        condition = ReadCondition(only, ctx, 1, 0);
                        break;
                    default:
                        throw only.Fail("is not a known trigger form; use \"mission_started\", \"objective_activated\", \"objective_completed\" or \"condition\"");
                }
                // Action list.
                JsonValue actionArray = node.Require("actions");
                IReadOnlyList<JsonValue> actionNodes = actionArray.Items;
                CheckCount(actionArray, actionNodes.Count, 1, 32);
                // Actions in file order.
                List<MissionAction> actions = new List<MissionAction>();
                foreach (JsonValue actionNode in actionNodes)
                {
                    actions.Add(ReadAction(actionNode, ctx));
                }
                triggers.Add(new TriggerDefinition(id, whenKind, objectiveIndex, condition, actions));
            }
            return triggers;
        }

        /// <summary>Reads one trigger action.</summary>
        private static MissionAction ReadAction(JsonValue node, Context ctx)
        {
            if (node.Kind != JsonKind.Object)
            {
                throw node.Fail("must be an action object");
            }
            // Action type name.
            string type = node.RequireString("type");
            // Values read for the action; unused ones keep these defaults.
            IReadOnlyList<JsonValue> commands = new JsonValue[0];
            PlayerId issuer = PlayerId.None;
            Fix64 delay = Fix64.Zero;
            string text = string.Empty;
            Fix64 seconds = Fix64.Zero;
            Fix64 x = Fix64.Zero;
            Fix64 y = Fix64.Zero;
            Fix64 radius = Fix64.Zero;
            List<EntityId> units = new List<EntityId>();
            string reason = string.Empty;
            ActionKind kind;
            switch (type)
            {
                case "commands":
                    kind = ActionKind.Commands;
                    node.RejectUnknownKeys("type", "commands", "issuer", "delay_seconds");
                    issuer = ReadIssuer(node, ctx);
                    delay = node.TryGetMember("delay_seconds", out JsonValue? delayNode) ? ReadFix(delayNode, Fix64.Zero, Fix64.FromInt(3600)) : Fix64.Zero;
                    // The command array.
                    JsonValue commandArray = node.Require("commands");
                    commands = commandArray.Items;
                    CheckCount(commandArray, commands.Count, 1, 100);
                    foreach (JsonValue command in commands)
                    {
                        ValidateCommand(command, ctx, issuer);
                    }
                    break;
                case "show_message":
                    kind = ActionKind.ShowMessage;
                    node.RejectUnknownKeys("type", "text", "seconds");
                    text = RequireText(node, "text", 1, 300);
                    seconds = RequireFix(node, "seconds", Fix64.One, Fix64.FromInt(60));
                    break;
                case "highlight_area":
                    kind = ActionKind.HighlightArea;
                    node.RejectUnknownKeys("type", "x", "y", "radius_tiles", "seconds");
                    FixVec2 areaCentre = ReadPoint(node, ctx);
                    x = areaCentre.X;
                    y = areaCentre.Y;
                    radius = RequireRadius(node);
                    seconds = RequireFix(node, "seconds", Fix64.One, Fix64.FromInt(60));
                    break;
                case "highlight_units":
                    kind = ActionKind.HighlightUnits;
                    node.RejectUnknownKeys("type", "units", "seconds");
                    // The unit ID list.
                    JsonValue unitArray = node.Require("units");
                    CheckCount(unitArray, unitArray.Items.Count, 1, 64);
                    foreach (JsonValue unitNode in unitArray.Items)
                    {
                        units.Add(new EntityId((uint)unitNode.AsInt(1, int.MaxValue)));
                    }
                    seconds = RequireFix(node, "seconds", Fix64.One, Fix64.FromInt(60));
                    break;
                case "camera_focus":
                    kind = ActionKind.CameraFocus;
                    node.RejectUnknownKeys("type", "x", "y");
                    FixVec2 focus = ReadPoint(node, ctx);
                    x = focus.X;
                    y = focus.Y;
                    break;
                case "win_mission":
                    kind = ActionKind.WinMission;
                    node.RejectUnknownKeys("type");
                    break;
                case "lose_mission":
                    kind = ActionKind.LoseMission;
                    node.RejectUnknownKeys("type", "reason");
                    reason = node.RequireNonEmptyString("reason");
                    if (!IsSnake(reason))
                    {
                        throw node.Require("reason").Fail("must be snake_case: a-z, 0-9 and '_', starting with a letter (found \"" + reason + "\")");
                    }
                    break;
                default:
                    throw node.Require("type").Fail("must be one of \"commands\", \"show_message\", \"highlight_area\", \"highlight_units\", \"camera_focus\", \"win_mission\" "
                        + "or \"lose_mission\" (found \"" + type + "\")");
            }
            return new MissionAction(kind, commands, issuer, delay, text, seconds, x, y, radius, units, reason);
        }

        /// <summary>The issuer key of a commands action: "script" or absent (the script issuer, 255), or a slot whose controller is "script".</summary>
        private static PlayerId ReadIssuer(JsonValue node, Context ctx)
        {
            if (!node.TryGetMember("issuer", out JsonValue? issuerNode))
            {
                return PlayerId.None;
            }
            if (issuerNode.Kind == JsonKind.String)
            {
                if (issuerNode.AsString() != "script")
                {
                    throw issuerNode.Fail("must be \"script\" or the slot number of a script player");
                }
                return PlayerId.None;
            }
            // The slot as written. Unit: slot number. Range: 0 to 254.
            PlayerId slot = new PlayerId((byte)issuerNode.AsInt(0, PlayerId.MaxValue));
            if (!ctx.Slots.TryGetValue(slot, out MissionPlayer? player) || !player.IsScript)
            {
                throw issuerNode.Fail("must be \"script\" or a player whose controller is \"script\" (slot " + slot.Value.ToString(CultureInfo.InvariantCulture) + " is not)");
            }
            return slot;
        }

        // ---------------------------------------------------------------- commands

        /// <summary>
        /// Checks a command object the way the runner will use it: it parses through CommandScript.ParseCommandObject with the issuer it will
        /// carry, names a loaded unit definition (or, for train_unit, research item) when it has a definition, and keeps a written x and y inside the map.
        /// </summary>
        /// <param name="node">The command object.</param>
        /// <param name="ctx">Parse context.</param>
        /// <param name="issuer">The issuer the command will carry. Range: a script slot or PlayerId.None.</param>
        private static void ValidateCommand(JsonValue node, Context ctx, PlayerId issuer)
        {
            CommandScript.ParseCommandObject(node, 0, issuer, 0, ctx.Source, ctx.Parsers);
            CheckCommandReferences(node, ctx);
        }

        /// <summary>Checks the data references and coordinates of an already parsed command object.</summary>
        private static void CheckCommandReferences(JsonValue node, Context ctx)
        {
            if (node.TryGetMember("definition", out JsonValue? definitionNode))
            {
                // The ID as written; the parser already checked it is a string.
                string id = definitionNode.AsString();
                // True for train_unit, whose definition may also name a research item.
                bool research = node.RequireString("type") == "train_unit" && ctx.Data.Content.Researches.ContainsKey(id);
                if (!research && !ctx.Data.Content.Units.ContainsKey(id))
                {
                    throw definitionNode.Fail("is not a loaded unit definition (found \"" + id + "\")");
                }
            }
            if (node.TryGetMember("x", out JsonValue? xNode) && node.TryGetMember("y", out JsonValue? yNode))
            {
                CheckInMap(xNode, ReadCoordinate(xNode), ctx.Map.WidthTiles);
                CheckInMap(yNode, ReadCoordinate(yNode), ctx.Map.HeightTiles);
            }
        }

        // ---------------------------------------------------------------- shared readers

        /// <summary>Validates a command object for the inputs file loader, which has its own context-free needs: same checks, against a data set and map.</summary>
        /// <param name="node">The command object.</param>
        /// <param name="mission">The mission whose map, data and readers apply.</param>
        /// <param name="data">The game data.</param>
        /// <param name="issuer">The issuer the command will carry. Range: a valid slot.</param>
        internal static void ValidateInputCommand(JsonValue node, MissionDefinition mission, DataSet data, PlayerId issuer)
        {
            Context ctx = new Context(data, data.RequireMap(mission.MapId), mission.Parsers, mission.SourceName);
            CommandScript.ParseCommandObject(node, 0, issuer, 0, node.SourceName, mission.Parsers);
            CheckCommandReferences(node, ctx);
        }

        /// <summary>Reads a coordinate written as decimal text or a JSON number.</summary>
        /// <param name="value">The node.</param>
        internal static Fix64 ReadCoordinate(JsonValue value)
        {
            if (value.Kind == JsonKind.String)
            {
                // The text, parsed exactly with no floating point.
                string text = value.AsString();
                try
                {
                    return Fix64.Parse(text);
                }
                catch (FormatException)
                {
                    throw value.Fail("must be decimal text such as \"20.5\" (found \"" + text + "\")");
                }
                catch (OverflowException)
                {
                    throw value.Fail("is too large (found \"" + text + "\")");
                }
            }
            return value.AsFix64();
        }

        /// <summary>A coordinate from text or number, kept in [min, max].</summary>
        private static Fix64 ReadFix(JsonValue value, Fix64 min, Fix64 max)
        {
            // The value read.
            Fix64 number = ReadCoordinate(value);
            if (number < min || number > max)
            {
                throw value.Fail("must be from " + min + " to " + max + " (found " + number + ")");
            }
            return number;
        }

        /// <summary>Required member as text or number in [min, max].</summary>
        internal static Fix64 RequireFix(JsonValue node, string key, Fix64 min, Fix64 max)
        {
            return ReadFix(node.Require(key), min, max);
        }

        /// <summary>The radius_tiles member, 0.5 to 64.</summary>
        private static Fix64 RequireRadius(JsonValue node)
        {
            return RequireFix(node, "radius_tiles", Fix64.Half, Fix64.FromInt(64));
        }

        /// <summary>The x and y members as a point inside the map.</summary>
        private static FixVec2 ReadPoint(JsonValue node, Context ctx)
        {
            return new FixVec2(RequireFix(node, "x", Fix64.Zero, Fix64.FromInt(ctx.Map.WidthTiles)), RequireFix(node, "y", Fix64.Zero, Fix64.FromInt(ctx.Map.HeightTiles)));
        }

        /// <summary>Fails when a coordinate lies outside 0 to the map size.</summary>
        private static void CheckInMap(JsonValue node, Fix64 value, int size)
        {
            if (value < Fix64.Zero || value > Fix64.FromInt(size))
            {
                throw node.Fail("must be inside the map (0 to " + size.ToString(CultureInfo.InvariantCulture) + " tiles, found " + value + ")");
            }
        }

        /// <summary>Required text member whose length is in [min, max] characters.</summary>
        private static string RequireText(JsonValue node, string key, int min, int max)
        {
            // The text.
            string text = node.RequireString(key);
            if (text.Length < min || text.Length > max)
            {
                throw node.Require(key).Fail("must be " + min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture) + " characters long (found " + text.Length.ToString(CultureInfo.InvariantCulture) + ")");
            }
            return text;
        }

        /// <summary>Fails when a list has fewer than min or more than max entries.</summary>
        private static void CheckCount(JsonValue list, int count, int min, int max)
        {
            if (count < min || count > max)
            {
                throw list.Fail("must have " + min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture) + " entries (found " + count.ToString(CultureInfo.InvariantCulture) + ")");
            }
        }

        /// <summary>True when the text is a snake_case ID: a lower-case letter, then lower-case letters, digits and underscores.</summary>
        internal static bool IsSnake(string text)
        {
            if (text.Length == 0 || text[0] < 'a' || text[0] > 'z')
            {
                return false;
            }
            foreach (char c in text)
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

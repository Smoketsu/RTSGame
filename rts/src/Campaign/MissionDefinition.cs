// rts/src/Campaign/MissionDefinition.cs
// The immutable result of parsing a mission file: header, players, setup commands, objectives, triggers and their actions
// (rts/docs/specs/campaign_scripting.md sections 2 and 6). Part of Rts.Campaign. Built by MissionLoader, read by MissionRunner and the hosts.
// A mission is data: nothing here changes after loading, and nothing here touches a simulation except CreateSimulation, which builds a
// fresh one from the mission's header. Engine-free; Fix64 and integers only.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;

namespace Rts.Campaign
{
    /// <summary>One player slot of a mission: the usual setup plus who controls it.</summary>
    public sealed class MissionPlayer
    {
        /// <summary>Slot, team, faction and name as the simulation takes them.</summary>
        public PlayerSetup Setup { get; }

        /// <summary>Controller text as written: "human", "script" or "ai:&lt;profile&gt;". Range: one of those three shapes.</summary>
        public string Controller { get; }

        /// <summary>AI profile ID (the file name under rts/data/ai without .json) for an "ai:" controller, else null. Range: a loaded profile.</summary>
        public string? AiProfile { get; }

        /// <summary>True for a "human" slot (commands come from the host's player input, never from the runner).</summary>
        public bool IsHuman => Controller == "human";

        /// <summary>True for a "script" slot (the runner may issue commands as this slot).</summary>
        public bool IsScript => Controller == "script";

        /// <summary>Builds the entry; MissionLoader is the only caller.</summary>
        internal MissionPlayer(PlayerSetup setup, string controller, string? aiProfile)
        {
            Setup = setup;
            Controller = controller;
            AiProfile = aiProfile;
        }
    }

    /// <summary>A circle the UI marks while an objective is active.</summary>
    public sealed class HintArea
    {
        /// <summary>Circle centre. Unit: tiles. Range: inside the map.</summary>
        public FixVec2 Centre { get; }

        /// <summary>Circle radius. Unit: tiles. Range: 0.5 to 64.</summary>
        public Fix64 RadiusTiles { get; }

        /// <summary>Builds the area; MissionLoader is the only caller.</summary>
        internal HintArea(FixVec2 centre, Fix64 radiusTiles)
        {
            Centre = centre;
            RadiusTiles = radiusTiles;
        }
    }

    /// <summary>One objective: text, prerequisites and the conditions that complete or fail it.</summary>
    public sealed class ObjectiveDefinition
    {
        /// <summary>Unique snake_case ID. Range: a-z, 0-9 and underscore, starting with a letter.</summary>
        public string Id { get; }

        /// <summary>Position in file order. Unit: objectives. Range: 0 to 63.</summary>
        public int Index { get; }

        /// <summary>Text for the objective panel. Range: 1 to 120 characters.</summary>
        public string Text { get; }

        /// <summary>Indexes (file order) of the objectives that must be complete first; every one is smaller than <see cref="Index"/>, so the graph has no cycle. Range: 0 to Index - 1; empty means active from the start.</summary>
        public IReadOnlyList<int> Requires { get; }

        /// <summary>True when the objective is completed.</summary>
        public ConditionDefinition Complete { get; }

        /// <summary>True loses the mission with reason objective_failed:&lt;id&gt;; null when the objective cannot fail.</summary>
        public ConditionDefinition? Fail { get; }

        /// <summary>Area the UI marks while the objective is active, or null.</summary>
        public HintArea? Hint { get; }

        /// <summary>Builds the objective; MissionLoader is the only caller.</summary>
        internal ObjectiveDefinition(string id, int index, string text, IReadOnlyList<int> requires, ConditionDefinition complete, ConditionDefinition? fail, HintArea? hint)
        {
            Id = id;
            Index = index;
            Text = text;
            Requires = requires;
            Complete = complete;
            Fail = fail;
            Hint = hint;
        }
    }

    /// <summary>What an action does. Range: the seven action types of the spec.</summary>
    public enum ActionKind
    {
        /// <summary>Submit command objects for a later tick.</summary>
        Commands,

        /// <summary>Cue: show a message.</summary>
        ShowMessage,

        /// <summary>Cue: mark a circle.</summary>
        HighlightArea,

        /// <summary>Cue: mark units.</summary>
        HighlightUnits,

        /// <summary>Cue: the host may pan the camera.</summary>
        CameraFocus,

        /// <summary>End the mission as won.</summary>
        WinMission,

        /// <summary>End the mission as lost.</summary>
        LoseMission,
    }

    /// <summary>One trigger action. Fields that do not belong to the action's <see cref="Kind"/> keep their defaults.</summary>
    public sealed class MissionAction
    {
        /// <summary>Which action this is.</summary>
        public ActionKind Kind { get; }

        /// <summary>Command objects of a Commands action, as parsed JSON without tick, issuer and sequence; the runner parses them again with the real tick and sequence when the action fires. Range: 1 to 100 entries.</summary>
        public IReadOnlyList<JsonValue> CommandObjects { get; }

        /// <summary>Issuing slot of a Commands action: PlayerId.None (255) for the script issuer, else a slot whose controller is "script". Range: 255 or a script slot.</summary>
        public PlayerId Issuer { get; }

        /// <summary>Wait before a Commands action's tick. Unit: seconds. Range: 0 to 3600.</summary>
        public Fix64 DelaySeconds { get; }

        /// <summary>Message text of ShowMessage. Range: 1 to 300 characters.</summary>
        public string Text { get; }

        /// <summary>How long a cue stays on screen. Unit: seconds. Range: 1 to 60.</summary>
        public Fix64 Seconds { get; }

        /// <summary>Cue centre x (HighlightArea, CameraFocus). Unit: tiles. Range: inside the map.</summary>
        public Fix64 X { get; }

        /// <summary>Cue centre y. Unit: tiles. Range: inside the map.</summary>
        public Fix64 Y { get; }

        /// <summary>Cue radius (HighlightArea). Unit: tiles. Range: 0.5 to 64.</summary>
        public Fix64 RadiusTiles { get; }

        /// <summary>Entity IDs of HighlightUnits. Range: 1 to 64 IDs, each 1 upward.</summary>
        public IReadOnlyList<EntityId> Units { get; }

        /// <summary>Reason of LoseMission, snake_case. Range: a-z, 0-9 and underscore, starting with a letter.</summary>
        public string Reason { get; }

        /// <summary>Builds the action; MissionLoader is the only caller.</summary>
        internal MissionAction(ActionKind kind, IReadOnlyList<JsonValue> commandObjects, PlayerId issuer, Fix64 delaySeconds, string text, Fix64 seconds, Fix64 x, Fix64 y,
            Fix64 radiusTiles, IReadOnlyList<EntityId> units, string reason)
        {
            Kind = kind;
            CommandObjects = commandObjects;
            Issuer = issuer;
            DelaySeconds = delaySeconds;
            Text = text;
            Seconds = seconds;
            X = x;
            Y = y;
            RadiusTiles = radiusTiles;
            Units = units;
            Reason = reason;
        }
    }

    /// <summary>What makes a trigger fire. Range: the four forms of the spec.</summary>
    public enum TriggerWhenKind
    {
        /// <summary>When the mission starts.</summary>
        MissionStarted,

        /// <summary>When a named objective becomes active.</summary>
        ObjectiveActivated,

        /// <summary>When a named objective is completed.</summary>
        ObjectiveCompleted,

        /// <summary>When a condition is true (counting from tick 0).</summary>
        Condition,
    }

    /// <summary>One trigger: when it fires, once, and what it does.</summary>
    public sealed class TriggerDefinition
    {
        /// <summary>Unique snake_case ID. Range: a-z, 0-9 and underscore, starting with a letter.</summary>
        public string Id { get; }

        /// <summary>Which form of "when" this trigger uses.</summary>
        public TriggerWhenKind WhenKind { get; }

        /// <summary>Index (file order) of the objective named by ObjectiveActivated or ObjectiveCompleted, else -1. Range: -1, or 0 to 63.</summary>
        public int ObjectiveIndex { get; }

        /// <summary>The condition of a Condition trigger, else null.</summary>
        public ConditionDefinition? Condition { get; }

        /// <summary>Actions run in order when the trigger fires. Range: 1 to 32 entries.</summary>
        public IReadOnlyList<MissionAction> Actions { get; }

        /// <summary>Builds the trigger; MissionLoader is the only caller.</summary>
        internal TriggerDefinition(string id, TriggerWhenKind whenKind, int objectiveIndex, ConditionDefinition? condition, IReadOnlyList<MissionAction> actions)
        {
            Id = id;
            WhenKind = whenKind;
            ObjectiveIndex = objectiveIndex;
            Condition = condition;
            Actions = actions;
        }
    }

    /// <summary>A loaded, validated mission. Immutable.</summary>
    public sealed class MissionDefinition
    {
        /// <summary>Mission ID; equals the file name without .json. Range: snake_case text.</summary>
        public string Id { get; }

        /// <summary>Name shown to players. Range: 1 to 60 characters.</summary>
        public string DisplayName { get; }

        /// <summary>Map ID played. Range: a map in the data set.</summary>
        public string MapId { get; }

        /// <summary>Simulation seed. Range: 0 to 2147483647.</summary>
        public ulong Seed { get; }

        /// <summary>Time limit; the mission is lost with reason timeout after it. Unit: seconds. Range: 30 to 7200.</summary>
        public int MaxSeconds { get; }

        /// <summary>False passes no cycle list to the simulation (no Planetary Cycle ever shifts). Range: true or false.</summary>
        public bool PlanetaryCycles { get; }

        /// <summary>Players in file order. Range: 1 to 8 entries, at least one human.</summary>
        public IReadOnlyList<MissionPlayer> Players { get; }

        /// <summary>Setup command objects, applied on tick 0 by the script issuer in file order. Range: 0 to 500 entries.</summary>
        public IReadOnlyList<JsonValue> Setup { get; }

        /// <summary>Objectives in file order. Range: 1 to 64 entries.</summary>
        public IReadOnlyList<ObjectiveDefinition> Objectives { get; }

        /// <summary>Triggers in file order. Range: 0 to 256 entries.</summary>
        public IReadOnlyList<TriggerDefinition> Triggers { get; }

        /// <summary>Index (file order) of the objective whose completion wins the mission. Range: 0 to Objectives.Count - 1.</summary>
        public int VictoryObjective { get; }

        /// <summary>True on any tick loses the mission (reason defeat), or null.</summary>
        public ConditionDefinition? DefeatWhen { get; }

        /// <summary>Command readers used to parse command objects (core kinds plus faction commands such as cast_wave). Range: the list the loader was given.</summary>
        public IReadOnlyList<IScriptCommandParser> Parsers { get; }

        /// <summary>The file path the mission was read from, for messages. Range: any non-empty text.</summary>
        public string SourceName { get; }

        /// <summary>Builds the mission; MissionLoader is the only caller.</summary>
        internal MissionDefinition(string id, string displayName, string mapId, ulong seed, int maxSeconds, bool planetaryCycles, IReadOnlyList<MissionPlayer> players,
            IReadOnlyList<JsonValue> setup, IReadOnlyList<ObjectiveDefinition> objectives, IReadOnlyList<TriggerDefinition> triggers, int victoryObjective,
            ConditionDefinition? defeatWhen, IReadOnlyList<IScriptCommandParser> parsers, string sourceName)
        {
            Id = id;
            DisplayName = displayName;
            MapId = mapId;
            Seed = seed;
            MaxSeconds = maxSeconds;
            PlanetaryCycles = planetaryCycles;
            Players = players;
            Setup = setup;
            Objectives = objectives;
            Triggers = triggers;
            VictoryObjective = victoryObjective;
            DefeatWhen = defeatWhen;
            Parsers = parsers;
            SourceName = sourceName;
        }

        /// <summary>Index of the objective called <paramref name="id"/>, or -1. Unit: objectives. Range: -1, or 0 to Objectives.Count - 1.</summary>
        /// <param name="id">Objective ID.</param>
        public int IndexOfObjective(string id)
        {
            for (int i = 0; i < Objectives.Count; i++)
            {
                if (Objectives[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The players as simulation setups, in file order.</summary>
        public List<PlayerSetup> PlayerSetups()
        {
            // Setups in file order.
            List<PlayerSetup> setups = new List<PlayerSetup>();
            foreach (MissionPlayer player in Players)
            {
                setups.Add(player.Setup);
            }
            return setups;
        }

        /// <summary>
        /// A new, not yet started simulation for this mission: its map, seed, players, the built-in faction mechanics, the tuning, and the data
        /// set's Planetary Cycles when <see cref="PlanetaryCycles"/> is true, none otherwise. The host calls Start, then builds the runner.
        /// </summary>
        /// <param name="data">The loaded data set the mission was validated against.</param>
        public Simulation CreateSimulation(DataSet data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            // The map played.
            MapDefinition map = data.RequireMap(MapId);
            return new Simulation(new SimulationConfig(data.CreateMatchSettings(Seed), data.Content, PlayerSetups(), BuiltInMechanics.CreateRegistry(), data.Tuning, map,
                PlanetaryCycles ? data.Cycles.Values : null));
        }
    }
}

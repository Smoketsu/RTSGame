// rts/src/Campaign/MissionInputs.cs
// The headless scripted player (rts/docs/specs/campaign_scripting.md sections 2 and 4): reads rts/campaign/inputs/<mission id>.json and, before each
// step, reports the camera and selection signals and submits the human commands that have fallen due, so a mission can run without a person or Unity.
// Part of Rts.Campaign. Commands go through Simulation.Submit as the mission's human player with the inputs' own sequence counter; signals go to
// MissionRunner.ReportSignal. Nothing here changes the simulation by any other route. Takes text, never touches files. Engine-free; Fix64 and integers only.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Teams;

namespace Rts.Campaign
{
    /// <summary>One scripted input: when it is due and what it does.</summary>
    internal sealed class InputEntry
    {
        /// <summary>Index (file order) of the objective whose activation starts the delay. Range: 0 to 63.</summary>
        public readonly int ObjectiveIndex;

        /// <summary>Reaction time after the objective's first counting tick. Unit: seconds. Range: 0 to 3600.</summary>
        public readonly Fix64 DelaySeconds;

        /// <summary>The signal to report, or null for a command input.</summary>
        public readonly HostSignal? Signal;

        /// <summary>The command object to submit (without tick, issuer, sequence), or null for a signal input.</summary>
        public readonly JsonValue? Command;

        /// <summary>True once the input has been delivered; each fires once.</summary>
        public bool Fired;

        /// <summary>Builds an unfired entry.</summary>
        public InputEntry(int objectiveIndex, Fix64 delaySeconds, HostSignal? signal, JsonValue? command)
        {
            ObjectiveIndex = objectiveIndex;
            DelaySeconds = delaySeconds;
            Signal = signal;
            Command = command;
        }
    }

    /// <summary>The scripted player of a mission. One instance per run (it remembers what it has fired).</summary>
    public sealed class MissionInputs
    {
        /// <summary>Mission ID the inputs belong to.</summary>
        public string MissionId { get; }

        /// <summary>The human slot the commands are issued as. Range: a "human" player of the mission.</summary>
        public PlayerId Player { get; }

        /// <summary>Next sequence number of the human player's commands from this file. Unit: counter. Range: 0 upward.</summary>
        public uint NextSequence { get; private set; }

        /// <summary>Entries in file order.</summary>
        private readonly List<InputEntry> _entries;

        /// <summary>The mission's command readers and name, used when a command object is parsed with its real tick.</summary>
        private readonly MissionDefinition _mission;

        /// <summary>Bundles the parsed entries.</summary>
        private MissionInputs(MissionDefinition mission, PlayerId player, List<InputEntry> entries)
        {
            _mission = mission;
            MissionId = mission.Id;
            Player = player;
            _entries = entries;
        }

        /// <summary>
        /// Parses and validates an inputs file against its mission. Throws <see cref="DataValidationException"/> naming the file and field: the
        /// mission must match, the player must be a human slot, every <c>on_objective</c> must name an objective, every input has exactly one of
        /// signal and command, and every command object parses.
        /// </summary>
        /// <param name="text">The inputs file's text (JSON with comments).</param>
        /// <param name="sourcePath">The file path, for messages.</param>
        /// <param name="mission">The mission the inputs play.</param>
        /// <param name="data">The loaded game data (for map and definition checks).</param>
        public static MissionInputs Parse(string text, string sourcePath, MissionDefinition mission, DataSet data)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (mission == null)
            {
                throw new ArgumentNullException(nameof(mission));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            JsonValue root = JsonReader.Parse(text, sourcePath.Replace('\\', '/'));
            root.RejectUnknownKeys("mission", "player", "inputs");
            if (root.Require(DataLoader.SchemaVersionKey).AsInt() != DataLoader.SupportedSchemaVersion)
            {
                throw root.Require(DataLoader.SchemaVersionKey).Fail("must be " + DataLoader.SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture));
            }
            root.RequireNonEmptyString(DataLoader.DocKey);
            // Mission ID the file says it plays.
            string missionId = root.RequireNonEmptyString("mission");
            if (missionId != mission.Id)
            {
                throw root.Require("mission").Fail("must be \"" + mission.Id + "\" (found \"" + missionId + "\")");
            }
            // The human slot. Unit: slot number. Range: 0 to 254.
            PlayerId player = new PlayerId((byte)root.RequireInt("player", 0, PlayerId.MaxValue));
            // True when the slot is a human player of the mission.
            bool human = false;
            foreach (MissionPlayer candidate in mission.Players)
            {
                human |= candidate.Setup.Id == player && candidate.IsHuman;
            }
            if (!human)
            {
                throw root.Require("player").Fail("must be a player of the mission whose controller is \"human\" (found " + player.Value.ToString(CultureInfo.InvariantCulture) + ")");
            }
            // Entries in file order.
            List<InputEntry> entries = new List<InputEntry>();
            foreach (JsonValue node in root.RequireArray("inputs"))
            {
                node.RejectUnknownKeys("on_objective", "delay_seconds", "signal", "command");
                // Objective ID the delay counts from.
                string objectiveId = node.RequireNonEmptyString("on_objective");
                // Index of that objective.
                int objectiveIndex = mission.IndexOfObjective(objectiveId);
                if (objectiveIndex < 0)
                {
                    throw node.Require("on_objective").Fail("names no objective of mission " + mission.Id + " (found \"" + objectiveId + "\")");
                }
                // Reaction time. Unit: seconds. Range: 0 to 3600.
                Fix64 delay = MissionLoader.RequireFix(node, "delay_seconds", Fix64.Zero, Fix64.FromInt(3600));
                // True when each of signal and command is present.
                bool hasSignal = node.TryGetMember("signal", out JsonValue? signalNode);
                bool hasCommand = node.TryGetMember("command", out JsonValue? commandNode);
                if (hasSignal == hasCommand)
                {
                    throw node.Fail("must have exactly one of \"signal\" and \"command\"");
                }
                if (hasSignal)
                {
                    entries.Add(new InputEntry(objectiveIndex, delay, ReadSignal(signalNode!), null));
                }
                else
                {
                    MissionLoader.ValidateInputCommand(commandNode!, mission, data, player);
                    entries.Add(new InputEntry(objectiveIndex, delay, null, commandNode));
                }
            }
            return new MissionInputs(mission, player, entries);
        }

        /// <summary>Reads a signal object: camera_moved (x, y) or selection_changed (units).</summary>
        private static HostSignal ReadSignal(JsonValue node)
        {
            // Signal type name.
            string type = node.RequireString("type");
            switch (type)
            {
                case "camera_moved":
                    node.RejectUnknownKeys("type", "x", "y");
                    return new CameraMovedSignal(MissionLoader.ReadCoordinate(node.Require("x")), MissionLoader.ReadCoordinate(node.Require("y")));
                case "selection_changed":
                    node.RejectUnknownKeys("type", "units");
                    // Selected entity IDs.
                    List<EntityId> units = new List<EntityId>();
                    foreach (JsonValue unit in node.RequireArray("units"))
                    {
                        units.Add(new EntityId((uint)unit.AsInt(1, int.MaxValue)));
                    }
                    return new SelectionChangedSignal(units);
                default:
                    throw node.Require("type").Fail("must be \"camera_moved\" or \"selection_changed\" (found \"" + type + "\")");
            }
        }

        /// <summary>
        /// Call before every step: for each unfired input whose objective is active and whose due tick (the objective's first counting tick plus the
        /// delay in ticks) has come, reports its signal or submits its command at the current tick as the human player. Each input fires once.
        /// </summary>
        /// <param name="sim">The simulation between steps.</param>
        /// <param name="runner">The runner of the mission; it receives the signals and says which objectives are active.</param>
        /// <param name="submit">Receives the human commands; null means <c>sim.Submit</c>. Pass <c>ReplayRecorder.Submit</c> to record them.</param>
        public void BeforeStep(Simulation sim, MissionRunner runner, Action<Command>? submit = null)
        {
            foreach (InputEntry entry in _entries)
            {
                if (entry.Fired)
                {
                    continue;
                }
                // The objective's state.
                ObjectiveState objective = runner.Objectives[entry.ObjectiveIndex];
                if (objective.Status != ObjectiveStatus.Active)
                {
                    continue;
                }
                // Tick the input is due. Unit: ticks.
                int due = objective.FirstCountingTick + sim.Clock.SecondsToTicks(entry.DelaySeconds);
                if (sim.Clock.Tick < due)
                {
                    continue;
                }
                entry.Fired = true;
                if (entry.Signal != null)
                {
                    runner.ReportSignal(entry.Signal);
                }
                else
                {
                    (submit ?? sim.Submit)(CommandScript.ParseCommandObject(entry.Command!, sim.Clock.Tick, Player, NextSequence, _mission.SourceName, _mission.Parsers));
                    NextSequence++;
                }
            }
        }
    }
}

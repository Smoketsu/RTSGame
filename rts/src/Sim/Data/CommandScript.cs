// rts/src/Sim/Data/CommandScript.cs
// Reads a command script (JSON with comments) into players, commands, a seed and a tick count. Part of Rts.Sim.Data.
// One script file drives every host: the headless harness (rts/tools/BalanceHarness) and the Unity bootstrap
// (rts/src/Game SimulationHost) both parse the same rts/data/scripts/*.json with this class, so their state hashes
// are comparable; later campaign missions use the same format. The script format is documented in
// rts/tools/BalanceHarness/README.md and in rts/data/scripts/smoke.json. Engine-free like the rest of the simulation
// assembly: it takes text, never touches the file system (hosts do the file IO), and uses only Fix64 and integers.
// Parsing reuses JsonReader so scripts get the same comments, exact numbers and file/line/field error messages as game
// data. Errors are DataValidationException naming the script path and the field. DataLoader skips the scripts/ folder
// on purpose: scripts are parsed here, not by the loader.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Sim.Data
{
    /// <summary>A parsed command script: who plays, what commands are issued when, how long to run.</summary>
    public sealed class CommandScript
    {
        /// <summary>Largest tick count a script may request. Unit: ticks (13.9 hours at 20 ticks per second). Range: fixed tool limit.</summary>
        public const int MaxTicks = 1000000;

        /// <summary>The script's seed. Range: any ulong.</summary>
        public ulong Seed { get; }

        /// <summary>How many ticks to simulate unless the host overrides it (the harness has --ticks). Unit: ticks. Range: 0 to <see cref="MaxTicks"/>.</summary>
        public int Ticks { get; }

        /// <summary>Map ID the script expects, or null when it does not care (the host then uses the first map in ordinal ID order).</summary>
        public string? MapId { get; }

        /// <summary>Player slots in script order.</summary>
        public IReadOnlyList<PlayerSetup> Players { get; }

        /// <summary>Commands in script order; the simulation sorts them by (tick, issuer, sequence) itself.</summary>
        public IReadOnlyList<Command> Commands { get; }

        /// <summary>Bundles the parsed pieces.</summary>
        private CommandScript(ulong seed, int ticks, IReadOnlyList<PlayerSetup> players, IReadOnlyList<Command> commands, string? mapId)
        {
            Seed = seed;
            MapId = mapId;
            Ticks = ticks;
            Players = players;
            Commands = commands;
        }

        /// <summary>Parses script text. Throws <see cref="DataValidationException"/> naming <paramref name="sourceName"/> and the field on any problem.</summary>
        /// <param name="text">The script file's text.</param>
        /// <param name="sourceName">The script path (data-root-relative or full), used in error messages.</param>
        public static CommandScript Parse(string text, string sourceName)
        {
            return Parse(JsonReader.Parse(text, sourceName), sourceName);
        }

        /// <summary>Builds a script from an already parsed JSON tree. Throws <see cref="DataValidationException"/> naming <paramref name="sourceName"/> and the field on any problem.</summary>
        /// <param name="root">The parsed top-level node of the script file.</param>
        /// <param name="sourceName">The script path, used for problems with the file as a whole (field-level problems name the node's own source and path).</param>
        public static CommandScript Parse(JsonValue root, string sourceName)
        {
            if (root.Kind != JsonKind.Object)
            {
                throw new DataValidationException(sourceName, string.Empty, "the top level must be a JSON object (found " + root.Kind.ToString().ToLowerInvariant() + ")");
            }
            root.RejectUnknownKeys("seed", "ticks", "players", "commands", "map");
            // Map ID to play on, or null to let the host pick the first map in ordinal order. Range: an ID under data/maps.
            string? mapId = root.TryGetMember("map", out JsonValue? mapNode) ? mapNode.AsString() : null;
            if (root.Require(DataLoader.SchemaVersionKey).AsInt() != DataLoader.SupportedSchemaVersion)
            {
                throw root.Require(DataLoader.SchemaVersionKey).Fail("must be " + DataLoader.SupportedSchemaVersion);
            }
            // Match seed from the script. Unit: integer seed. Range: any ulong.
            ulong seed = ReadUlong(root.Require("seed"));
            // Ticks to simulate. Unit: ticks. Range: 0 to MaxTicks.
            int ticks = root.RequireInt("ticks", 0, MaxTicks);

            // Player slots, in script order.
            List<PlayerSetup> players = new List<PlayerSetup>();
            foreach (JsonValue node in root.RequireArray("players"))
            {
                node.RejectUnknownKeys("id", "team", "faction", "name");
                // Player slot index. Unit: slot number. Range: 0 to PlayerId.MaxValue.
                int id = node.RequireInt("id", 0, PlayerId.MaxValue);
                // Team index. Unit: team number. Range: 0 to TeamId.MaxValue.
                int team = node.RequireInt("team", 0, TeamId.MaxValue);
                // Faction ID, non-empty text. Range: any non-empty string.
                string faction = node.RequireNonEmptyString("faction");
                // Display name. Range: any string; defaults to "Player <id>" when absent.
                string name = node.OptionalString("name", "Player " + id.ToString(CultureInfo.InvariantCulture));
                players.Add(new PlayerSetup(new PlayerId((byte)id), new TeamId((byte)team), faction, name));
            }
            if (players.Count == 0)
            {
                throw root.Require("players").Fail("must list at least one player");
            }

            // Commands, in script order.
            List<Command> commands = new List<Command>();
            foreach (JsonValue node in root.RequireArray("commands"))
            {
                commands.Add(ReadCommand(node));
            }
            return new CommandScript(seed, ticks, players, commands, mapId);
        }

        /// <summary>Reads one command object.</summary>
        private static Command ReadCommand(JsonValue node)
        {
            // Command kind name, such as spawn_unit. Range: one of the known kinds (noop, spawn_unit, move_unit, remove_unit, attack_move, attack_unit, stop, root_hq, uproot_hq, gather, train_unit, build_structure, apply_status).
            string type = node.RequireString("type");
            // Tick the command applies at. Unit: ticks. Range: 0 to MaxTicks.
            int tick = node.RequireInt("tick", 0, MaxTicks);
            // Issuing player slot. Unit: slot number. Range: 0 to PlayerId.None.Value (none).
            PlayerId issuer = new PlayerId((byte)node.RequireInt("issuer", 0, PlayerId.None.Value));
            // Per-issuer sequence number. Unit: integer sequence. Range: 0 to int.MaxValue.
            uint sequence = (uint)node.RequireInt("sequence", 0, int.MaxValue);
            switch (type)
            {
                case "noop":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence");
                    return new NoOpCommand(tick, issuer, sequence);
                case "spawn_unit":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "definition", "owner", "x", "y");
                    return new SpawnUnitCommand(tick, issuer, sequence, node.RequireNonEmptyString("definition"),
                        new PlayerId((byte)node.RequireInt("owner", 0, PlayerId.None.Value)), ReadPoint(node));
                case "move_unit":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit", "x", "y");
                    return new MoveUnitCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)), ReadPoint(node));
                case "remove_unit":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit");
                    return new RemoveUnitCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)));
                case "attack_move":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit", "x", "y");
                    return new AttackMoveCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)), ReadPoint(node));
                case "attack_unit":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit", "target");
                    return new AttackUnitCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)),
                        new EntityId((uint)node.RequireInt("target", 1, int.MaxValue)));
                case "stop":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit");
                    return new StopCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)));
                case "root_hq":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit");
                    return new RootHqCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)));
                case "uproot_hq":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit");
                    return new UprootHqCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)));
                case "gather":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit", "field");
                    return new GatherCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)),
                        node.RequireInt("field", 0, 1023));
                case "train_unit":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "producer", "definition");
                    return new TrainUnitCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("producer", 1, int.MaxValue)),
                        node.RequireNonEmptyString("definition"));
                case "build_structure":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit", "definition", "x", "y");
                    return new BuildStructureCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)),
                        node.RequireNonEmptyString("definition"), node.RequireInt("x", 0, 1023), node.RequireInt("y", 0, 1023));
                case "apply_status":
                    node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit", "status", "multiplier", "seconds", "source");
                    return ReadApplyStatus(node, tick, issuer, sequence);
                default:
                    throw node.Require("type").Fail("must be \"noop\", \"spawn_unit\", \"move_unit\", \"remove_unit\", \"attack_move\", \"attack_unit\", \"stop\", \"root_hq\", \"uproot_hq\", \"gather\", \"train_unit\", \"build_structure\" or \"apply_status\" (found \"" + type + "\")");
            }
        }

        /// <summary>
        /// Reads the apply_status command: unit, status ("grounded" or "slowed"), optional multiplier (default 1; Slowed needs
        /// a value above 0 and at most 1, checked when applied), optional seconds (default 0 = the tuning default for Grounded,
        /// invalid for Slowed) and optional source (default "script").
        /// </summary>
        private static Command ReadApplyStatus(JsonValue node, int tick, PlayerId issuer, uint sequence)
        {
            // Status name as written. Range: "grounded" or "slowed".
            string status = node.RequireString("status");
            if (status != "grounded" && status != "slowed")
            {
                throw node.Require("status").Fail("must be \"grounded\" or \"slowed\" (found \"" + status + "\")");
            }
            // Speed multiplier of a slow. Unit: multiplier of speed. Range: above 0 and at most 1 for Slowed; default 1.
            Fix64 multiplier = node.TryGetMember("multiplier", out JsonValue? multiplierNode) ? ReadCoordinate(multiplierNode) : Fix64.One;
            // Length in seconds. Unit: seconds. Range: above 0; default 0 (Grounded: use grounded_duration_seconds).
            Fix64 seconds = node.TryGetMember("seconds", out JsonValue? secondsNode) ? ReadCoordinate(secondsNode) : Fix64.Zero;
            // Source label. Range: 1 to 64 characters of a-z, 0-9, underscore and dot; default "script".
            string source = node.OptionalString("source", ApplyStatusCommand.DefaultSourceKey);
            if (!Statuses.SlowEntry.IsValidKey(source))
            {
                throw node.Require("source").Fail("must be 1 to 64 characters of a-z, 0-9, '_' and '.' (found \"" + source + "\")");
            }
            return new ApplyStatusCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)),
                status == "grounded" ? Statuses.StatusKind.Grounded : Statuses.StatusKind.Slowed, multiplier, seconds, source);
        }

        /// <summary>Reads the x and y members, each either decimal text ("20.5") or a JSON number, as a fixed-point point in tiles.</summary>
        private static FixVec2 ReadPoint(JsonValue node)
        {
            return new FixVec2(ReadCoordinate(node.Require("x")), ReadCoordinate(node.Require("y")));
        }

        /// <summary>Reads a coordinate from a string or number node.</summary>
        private static Fix64 ReadCoordinate(JsonValue value)
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
            }
            return value.AsFix64();
        }

        /// <summary>Reads a whole number from 0 to ulong.MaxValue from its decimal text.</summary>
        private static ulong ReadUlong(JsonValue value)
        {
            // Decimal text of the number node.
            string text = value.NumberText;
            // Sequence parsed from the text. Unit: integer. Range: 0 to ulong.MaxValue.
            if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong result))
            {
                throw value.Fail("must be a whole number from 0 to 18446744073709551615 (found " + text + ")");
            }
            return result;
        }
    }
}

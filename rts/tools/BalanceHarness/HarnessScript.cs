// rts/tools/BalanceHarness/HarnessScript.cs
// Reads a harness script (JSON with comments) into players, commands, a seed and a tick count. Part of
// Rts.Tools.BalanceHarness. The script format is documented in README.md and in rts/tools/BalanceHarness/scripts/smoke.json.
// Parsing reuses the simulation's JsonReader so scripts get the same comments, exact numbers and file/line/field
// error messages as game data. Errors are DataValidationException naming the script path and the field.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Teams;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>A parsed script: who plays, what commands are issued when, how long to run.</summary>
    public sealed class HarnessScript
    {
        /// <summary>Largest tick count a script may request. Unit: ticks (13.9 hours at 20 ticks per second). Range: fixed tool limit.</summary>
        public const int MaxTicks = 1000000;

        /// <summary>The script's seed. Range: any ulong.</summary>
        public ulong Seed { get; }

        /// <summary>How many ticks to simulate unless --ticks overrides it. Unit: ticks. Range: 0 to <see cref="MaxTicks"/>.</summary>
        public int Ticks { get; }

        /// <summary>Player slots in script order.</summary>
        public IReadOnlyList<PlayerSetup> Players { get; }

        /// <summary>Commands in script order; the simulation sorts them by (tick, issuer, sequence) itself.</summary>
        public IReadOnlyList<Command> Commands { get; }

        /// <summary>Bundles the parsed pieces.</summary>
        private HarnessScript(ulong seed, int ticks, IReadOnlyList<PlayerSetup> players, IReadOnlyList<Command> commands)
        {
            Seed = seed;
            Ticks = ticks;
            Players = players;
            Commands = commands;
        }

        /// <summary>Parses script text. Throws <see cref="DataValidationException"/> naming <paramref name="sourceName"/> and the field on any problem.</summary>
        /// <param name="text">The script file's text.</param>
        /// <param name="sourceName">The script path, used in error messages.</param>
        public static HarnessScript Parse(string text, string sourceName)
        {
            JsonValue root = JsonReader.Parse(text, sourceName);
            root.RejectUnknownKeys("seed", "ticks", "players", "commands");
            if (root.Require(DataLoader.SchemaVersionKey).AsInt() != DataLoader.SupportedSchemaVersion)
            {
                throw root.Require(DataLoader.SchemaVersionKey).Fail("must be " + DataLoader.SupportedSchemaVersion);
            }
            ulong seed = ReadUlong(root.Require("seed"));
            int ticks = root.RequireInt("ticks", 0, MaxTicks);

            // Player slots, in script order.
            List<PlayerSetup> players = new List<PlayerSetup>();
            foreach (JsonValue node in root.RequireArray("players"))
            {
                node.RejectUnknownKeys("id", "team", "faction", "name");
                int id = node.RequireInt("id", 0, PlayerId.MaxValue);
                int team = node.RequireInt("team", 0, TeamId.MaxValue);
                string faction = node.RequireNonEmptyString("faction");
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
            return new HarnessScript(seed, ticks, players, commands);
        }

        /// <summary>Reads one command object.</summary>
        private static Command ReadCommand(JsonValue node)
        {
            string type = node.RequireString("type");
            int tick = node.RequireInt("tick", 0, MaxTicks);
            PlayerId issuer = new PlayerId((byte)node.RequireInt("issuer", 0, PlayerId.None.Value));
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
                default:
                    throw node.Require("type").Fail("must be \"noop\", \"spawn_unit\", \"move_unit\" or \"remove_unit\" (found \"" + type + "\")");
            }
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
            if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong result))
            {
                throw value.Fail("must be a whole number from 0 to 18446744073709551615 (found " + text + ")");
            }
            return result;
        }
    }
}

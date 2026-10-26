// rts/tests/Sim.Tests/Data/CommandScriptTests.cs
// Tests for CommandScript, the shared command-script parser used by the headless harness and the Unity bootstrap:
// the shipped rts/data/scripts/smoke.json parses to the expected seed, players and commands, the DataLoader leaves
// the scripts/ folder alone, and malformed scripts raise DataValidationException naming the file and the field.
// Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Sim.Tests.Data
{
    /// <summary>CommandScript parsing and error reporting.</summary>
    [TestFixture]
    public class CommandScriptTests
    {
        /// <summary>Data-root-relative path of the shipped smoke script.</summary>
        private const string SmokePath = "scripts/smoke.json";

        /// <summary>Source name used for in-memory scripts; errors must start with it.</summary>
        private const string Source = "test/script.json";

        /// <summary>Reads the shipped smoke script text from the real data folder.</summary>
        private static string ReadSmokeText()
        {
            return File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), SmokePath));
        }

        /// <summary>Builds a minimal valid script around one command object (JSON text, without braces around the list).</summary>
        private static string ScriptWith(string commandJson)
        {
            return "{ \"$schema_version\": 1, \"seed\": 7, \"ticks\": 10,"
                + " \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"skai\" } ],"
                + " \"commands\": [ " + commandJson + " ] }";
        }

        /// <summary>Asserts that parsing throws a validation error that starts with the source name and names the field.</summary>
        private static void AssertRejected(string text, string field, params string[] extra)
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() => CommandScript.Parse(text, Source));
            Assert.That(ex.Message, Does.StartWith(Source + ": "));
            Assert.That(ex.Message, Does.Contain("field '" + field + "'"));
            foreach (string part in extra)
            {
                Assert.That(ex.Message, Does.Contain(part));
            }
        }

        /// <summary>The shipped smoke script has the seed, tick count, players and commands the Unity host used to hard-code.</summary>
        [Test]
        public void SmokeScript_Parses_WithExpectedContent()
        {
            CommandScript script = CommandScript.Parse(ReadSmokeText(), SmokePath);
            Assert.That(script.Seed, Is.EqualTo(1234UL));
            Assert.That(script.Ticks, Is.EqualTo(600));

            Assert.That(script.Players.Count, Is.EqualTo(2));
            Assert.That(script.Players[0].Id, Is.EqualTo(new PlayerId(0)));
            Assert.That(script.Players[0].Team.Value, Is.EqualTo(0));
            Assert.That(script.Players[0].FactionId, Is.EqualTo("skai"));
            Assert.That(script.Players[1].Id, Is.EqualTo(new PlayerId(1)));
            Assert.That(script.Players[1].Team.Value, Is.EqualTo(1));
            Assert.That(script.Players[1].FactionId, Is.EqualTo("water"));

            // 4 spawns, 4 script moves, then a player move, a no-op, a rejected move, a removal and a move of the removed unit.
            Assert.That(script.Commands.Count, Is.EqualTo(13));
            int spawns = 0, moves = 0, removes = 0, noops = 0;
            foreach (Command command in script.Commands)
            {
                if (command is SpawnUnitCommand) { spawns++; }
                else if (command is MoveUnitCommand) { moves++; }
                else if (command is RemoveUnitCommand) { removes++; }
                else if (command is NoOpCommand) { noops++; }
            }
            Assert.That(spawns, Is.EqualTo(4));
            Assert.That(moves, Is.EqualTo(7));
            Assert.That(removes, Is.EqualTo(1));
            Assert.That(noops, Is.EqualTo(1));

            // The first command is the tick-0 spawn of Skai's Windrunner at (8, 8), issued by the script (255).
            SpawnUnitCommand first = (SpawnUnitCommand)script.Commands[0];
            Assert.That(first.Tick, Is.EqualTo(0));
            Assert.That(first.Issuer, Is.EqualTo(PlayerId.None));
            Assert.That(first.Sequence, Is.EqualTo(0u));
            Assert.That(first.DefinitionId, Is.EqualTo("skai.windrunner"));
            Assert.That(first.Owner, Is.EqualTo(new PlayerId(0)));
            Assert.That(first.Position, Is.EqualTo(FixVec2.FromInts(8, 8)));

            // The fifth command is the tick-20 move of unit 1 to (56, 56).
            MoveUnitCommand move = (MoveUnitCommand)script.Commands[4];
            Assert.That(move.Tick, Is.EqualTo(20));
            Assert.That(move.Sequence, Is.EqualTo(4u));
            Assert.That(move.Unit, Is.EqualTo(new EntityId(1)));
            Assert.That(move.Target, Is.EqualTo(FixVec2.FromInts(56, 56)));
        }

        /// <summary>Parsing the same text through the JsonValue overload gives the same script.</summary>
        [Test]
        public void ParseJsonValue_MatchesParseText()
        {
            string text = ReadSmokeText();
            CommandScript fromText = CommandScript.Parse(text, SmokePath);
            CommandScript fromTree = CommandScript.Parse(JsonReader.Parse(text, SmokePath), SmokePath);
            Assert.That(fromTree.Seed, Is.EqualTo(fromText.Seed));
            Assert.That(fromTree.Ticks, Is.EqualTo(fromText.Ticks));
            Assert.That(fromTree.Players.Count, Is.EqualTo(fromText.Players.Count));
            Assert.That(fromTree.Commands.Count, Is.EqualTo(fromText.Commands.Count));
        }

        /// <summary>Coordinates may be decimal text or numbers; both give exact fixed-point values.</summary>
        [Test]
        public void Coordinates_AcceptTextAndNumbers()
        {
            CommandScript script = CommandScript.Parse(ScriptWith(
                "{ \"tick\": 1, \"issuer\": 0, \"sequence\": 0, \"type\": \"move_unit\", \"unit\": 1, \"x\": \"20.5\", \"y\": 12 }"), Source);
            MoveUnitCommand move = (MoveUnitCommand)script.Commands[0];
            Assert.That(move.Target, Is.EqualTo(new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("12"))));
        }

        /// <summary>The loader ignores the scripts/ folder (the real data folder loads with the script present) and still rejects other unknown folders.</summary>
        [Test]
        public void DataLoader_IgnoresScriptsFolder_ButRejectsOthers()
        {
            List<DataSourceFile> files = TestData.ReadAll();
            Assert.That(files.Exists(f => f.Path == SmokePath), Is.True);
            Expect.DoesNotThrow(() => DataLoader.Load(files));

            // Not a valid data file at all: the loader must not look at it.
            files.Add(new DataSourceFile("scripts/anything.json", "not json, loader never parses it"));
            Expect.DoesNotThrow(() => DataLoader.Load(files));

            files.Add(new DataSourceFile("scriptz/x.json", "{}"));
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(files)).Message, Does.StartWith("scriptz/x.json: "));
        }

        /// <summary>An unknown command type names the file and the "type" field.</summary>
        [Test]
        public void UnknownCommandType_Throws()
        {
            AssertRejected(ScriptWith("{ \"tick\": 1, \"issuer\": 0, \"sequence\": 0, \"type\": \"teleport_unit\" }"),
                "commands[0].type", "teleport_unit");
        }

        /// <summary>A missing required field names the file and the field.</summary>
        [Test]
        public void MissingField_Throws()
        {
            // spawn_unit without "owner".
            AssertRejected(ScriptWith("{ \"tick\": 0, \"issuer\": 255, \"sequence\": 0, \"type\": \"spawn_unit\", \"definition\": \"skai.windrunner\", \"x\": \"1\", \"y\": \"1\" }"),
                "commands[0].owner", "missing");
            // Top-level "ticks" missing.
            AssertRejected("{ \"$schema_version\": 1, \"seed\": 1, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"skai\" } ], \"commands\": [] }",
                "ticks");
        }

        /// <summary>A coordinate that is not decimal text or a number names the file and the field.</summary>
        [Test]
        public void BadCoordinate_Throws()
        {
            AssertRejected(ScriptWith("{ \"tick\": 1, \"issuer\": 0, \"sequence\": 0, \"type\": \"move_unit\", \"unit\": 1, \"x\": \"abc\", \"y\": \"2\" }"),
                "commands[0].x", "abc");
            AssertRejected(ScriptWith("{ \"tick\": 1, \"issuer\": 0, \"sequence\": 0, \"type\": \"move_unit\", \"unit\": 1, \"x\": \"1\", \"y\": true }"),
                "commands[0].y");
        }

        /// <summary>Unknown keys, a wrong schema version and an empty player list are errors too.</summary>
        [Test]
        public void StructuralProblems_Throw()
        {
            AssertRejected(ScriptWith("{ \"tick\": 1, \"issuer\": 0, \"sequence\": 0, \"type\": \"noop\", \"colour\": 1 }"), "commands[0].colour");
            AssertRejected("{ \"$schema_version\": 2, \"seed\": 1, \"ticks\": 1, \"players\": [], \"commands\": [] }", "$schema_version");
            AssertRejected("{ \"$schema_version\": 1, \"seed\": 1, \"ticks\": 1, \"players\": [], \"commands\": [] }", "players");
            DataValidationException ex = Expect.Throws<DataValidationException>(() => CommandScript.Parse("[]", Source));
            Assert.That(ex.Message, Does.StartWith(Source + ": "));
        }
    }
}

// rts/tests/Sim.Tests/Harness/HarnessTraceTests.cs
// Tests of the --trace mode of the balance harness: tracing a game must not change it (same final state hash as the untraced game), and the
// command line mode must write the timeline and summary. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Trace mode tests.</summary>
    [TestFixture]
    public class HarnessTraceTests
    {
        /// <summary>A traced game ends in the same state hash as the same game without a trace, and the trace holds timeline lines and a summary.</summary>
        [Test]
        public void Trace_DoesNotChangeTheGame()
        {
            // The shipped data set.
            DataSet data = HarnessTestSupport.Load(TestData.FindDataDirectory());
            // Game length used. Unit: seconds. Range: 1 to 7200.
            const int seconds = 90;
            GameRecord plain = GameRunner.Play(data, "greybox_1v1", "skai_vs_water", 0, 12345UL, "aggressive", "aggressive", seconds);
            StringWriter text = new StringWriter();
            GameRecord traced = GameRunner.Play(data, "greybox_1v1", "skai_vs_water", 0, 12345UL, "aggressive", "aggressive", seconds, new GameTrace(text, 30));
            Assert.That(traced.StateHash, Is.EqualTo(plain.StateHash));
            Assert.That(traced.EndTick, Is.EqualTo(plain.EndTick));
            // Trace text of the traced game.
            string trace = text.ToString();
            Assert.That(trace, Does.Contain("t=30s P0 skai"));
            Assert.That(trace, Does.Contain("t=60s P1 water"));
            Assert.That(trace, Does.Contain("== summary =="));
            Assert.That(trace, Does.Contain("last 20 commands"));
            // Worker construction: the summary reports ordered, placed and cancelled build orders (WCA-11).
            Assert.That(trace, Does.Contain("structures ordered="));
            Assert.That(trace, Does.Contain("placed="));
            Assert.That(trace, Does.Contain("build orders cancelled, whole game="));
            Assert.That(trace, Does.Contain(" cancelled="), "the timeline lines carry the interval's cancellations");
        }

        /// <summary>A command the simulation refuses is counted by reason in the timeline line and in the summary.</summary>
        [Test]
        public void Trace_CountsRejectedCommandsByReason()
        {
            DataSet data = HarnessTestSupport.Load(TestData.FindDataDirectory());
            MapDefinition map = data.RequireMap("greybox_1v1");
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "a"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), "water", "b"),
            };
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(1UL), data.Content, players, BuiltInMechanics.CreateRegistry(), data.Tuning, map, data.Cycles.Values));
            sim.Start();
            StringWriter text = new StringWriter();
            GameTrace trace = new GameTrace(text, 1);
            trace.Begin(sim, "rejection test");
            // Player 0 orders a unit that does not exist; the simulation must refuse with unknown_unit.
            sim.Submit(new TrainUnitCommand(0, new PlayerId(0), 0, new EntityId(999), "skai.fledgling"));
            sim.Step();
            trace.AfterStep(sim);
            trace.Finish(sim, MatchOutcome.Evaluate(sim.Players), 0, 1);
            Assert.That(text.ToString(), Does.Contain("rejected=unknown_unit:1"));
            Assert.That(text.ToString(), Does.Contain("commands rejected, whole game=unknown_unit:1"));
        }

        /// <summary>The command line mode writes the timeline to the --out file and needs --map and --matchup.</summary>
        [Test]
        public void TraceMode_WritesTheFile()
        {
            // Data folder and output file.
            string data = TestData.FindDataDirectory();
            string file = Path.Combine(HarnessTestSupport.NewFolder(), "trace.txt");
            int exit = HarnessTestSupport.Run(new[] { "--data", data, "--trace", "--map", "greybox_1v1", "--matchup", "skai_vs_water",
                "--seed", "1", "--max-game-seconds", "40", "--out", file }, out string output, out string error);
            Assert.That(exit, Is.EqualTo(0), error);
            Assert.That(output, Does.Contain("\"matchup\":\"skai_vs_water\""));
            Assert.That(File.ReadAllText(file), Does.Contain("t=30s P0 skai"));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", data, "--trace" }, out _, out error), Is.EqualTo(1));
            Assert.That(error, Does.Contain("--trace needs --map and --matchup"));
        }

        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }
    }
}

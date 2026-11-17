// rts/tests/Sim.Tests/Harness/HarnessBatchTests.cs
// Process-level acceptance tests of the balance harness (spec rts/docs/specs/balance_harness.md section 7): one game is deterministic in-process and
// through a worker process (7), the batch output does not depend on --jobs (8), the HQ floor exit codes (9), a determinism failure (12) and a failing
// worker (13). They start real worker processes (this harness assembly with --worker), so they are slower than the unit tests. Part of Rts.Sim.Tests.
using System.IO;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Harness tests that run worker processes.</summary>
    [TestFixture]
    public class HarnessBatchTests
    {
        /// <summary>Deletes the temporary data and output folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>Spec test 7: the same game twice in-process gives the same record, and a worker process prints the same JSON line.</summary>
        [Test]
        public void OneGame_IsDeterministicInProcessAndThroughAWorker()
        {
            string folder = Rts.Sim.Tests.Data.TestData.FindDataDirectory();
            DataSet data = Rts.Sim.Tests.Data.TestData.LoadAll();
            ulong seed = SeedDerivation.GameSeed((ulong)data.Tuning.HarnessBatch.BaseSeed, "greybox_1v1", "skai_vs_water", 0);
            string first = GameRunner.Play(data, "greybox_1v1", "skai_vs_water", 0, seed, "aggressive", "aggressive", 300).ToJsonLine();
            string second = GameRunner.Play(data, "greybox_1v1", "skai_vs_water", 0, seed, "aggressive", "aggressive", 300).ToJsonLine();
            Assert.That(second, Is.EqualTo(first));
            WorkerResult worker = WorkerPool.RunOne(new[] { "--data", folder, "--map", "greybox_1v1", "--matchup", "skai_vs_water", "--first", "0", "--count", "1", "--max-game-seconds", "300" });
            Assert.That(worker.ExitCode, Is.EqualTo(0), worker.Error);
            Assert.That(worker.Lines, Is.EqualTo(new[] { first }));
        }

        /// <summary>Spec test 8: --jobs 1 and --jobs 3 give byte-identical stdout, report.md, results.jsonl and hq_floor.jsonl.</summary>
        [Test]
        public void Batch_OutputDoesNotDependOnJobs()
        {
            string data = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch);
            string[] outputs = new string[2];
            string[][] files = new string[2][];
            for (int run = 0; run < 2; run++)
            {
                string outDir = HarnessTestSupport.NewFolder();
                int exit = HarnessTestSupport.Run(new[] { "--data", data, "--batch", "--games", "4", "--maps", "greybox_1v1", "--matchups", "skai_vs_water",
                    "--max-game-seconds", "120", "--verify", "1", "--jobs", run == 0 ? "1" : "3", "--out", outDir, "--report-only" }, out outputs[run], out string error);
                Assert.That(exit, Is.EqualTo(0), error);
                files[run] = new[] { File.ReadAllText(Path.Combine(outDir, "report.md")), File.ReadAllText(Path.Combine(outDir, "results.jsonl")),
                    File.ReadAllText(Path.Combine(outDir, "hq_floor.jsonl")) };
            }
            Assert.That(outputs[1], Is.EqualTo(outputs[0]));
            Assert.That(outputs[0], Does.Contain("cell greybox_1v1 skai_vs_water 4/4\n"));
            Assert.That(outputs[0], Does.EndWith("HARNESS RESULT=FAIL\n").Or.EndWith("HARNESS RESULT=PASS\n"));
            for (int f = 0; f < 3; f++)
            {
                Assert.That(files[1][f], Is.EqualTo(files[0][f]));
            }
            Assert.That(files[0][1].Split('\n').Length, Is.EqualTo(5), "four game lines and the final newline");
        }

        /// <summary>Spec test 9 (exit codes): a failing floor exits 2 and prints FAIL, --report-only exits 0 and still prints FAIL, a passing floor exits 0.</summary>
        [Test]
        public void Batch_FloorExitCodes()
        {
            string failing = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch, FloorEdits(1000));
            string outDir = HarnessTestSupport.NewFolder();
            string[] args = { "--data", failing, "--batch", "--games", "1", "--maps", "greybox_1v1", "--matchups", "skai_vs_skai", "--max-game-seconds", "1", "--verify", "0", "--jobs", "2", "--out", outDir };
            Assert.That(HarnessTestSupport.Run(args, out string output, out string error), Is.EqualTo(2), error);
            Assert.That(output, Does.EndWith("HARNESS RESULT=FAIL\n"));
            Assert.That(File.ReadAllText(Path.Combine(outDir, "report.md")), Does.Contain("| 9.0 s (skai.windrunner x10 vs skai.great_aerie) | FAIL |"));
            Assert.That(HarnessTestSupport.Run(Append(args, "--report-only"), out string reportOnly, out _), Is.EqualTo(0));
            Assert.That(reportOnly, Does.EndWith("HARNESS RESULT=FAIL\n"));

            string passing = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch, FloorEdits(2500));
            args[1] = passing;
            Assert.That(HarnessTestSupport.Run(args, out string passed, out error), Is.EqualTo(0), error);
            Assert.That(passed, Does.EndWith("HARNESS RESULT=PASS\n"));
            Assert.That(File.ReadAllText(Path.Combine(outDir, "report.md")), Does.Contain("| 24.0 s (skai.windrunner x10 vs skai.great_aerie) | PASS |"));
        }

        /// <summary>Spec test 12: a re-run that plays a different seed is a determinism failure: one mismatch in the report, DETERMINISM_FAIL, exit 3.</summary>
        [Test]
        public void Batch_DeterminismFailureExitsWithThree()
        {
            string data = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch);
            string outDir = HarnessTestSupport.NewFolder();
            int exit = HarnessTestSupport.Run(new[] { "--data", data, "--batch", "--games", "1", "--maps", "greybox_1v1", "--matchups", "skai_vs_water",
                "--max-game-seconds", "120", "--verify", "1", "--jobs", "2", "--out", outDir, "--test-fault", BatchOptions.FaultPerturb }, out string output, out string error);
            Assert.That(exit, Is.EqualTo(3), error);
            Assert.That(output, Does.EndWith("HARNESS RESULT=DETERMINISM_FAIL\n"));
            Assert.That(File.ReadAllText(Path.Combine(outDir, "report.md")), Does.Contain("| greybox_1v1 | skai_vs_water | 1 | 1 |"));
        }

        /// <summary>Spec test 13: a worker that exits 1 fails the batch (exit 1) and its stderr text is in the message.</summary>
        [Test]
        public void Batch_WorkerFailureExitsWithOne()
        {
            string data = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch);
            int exit = HarnessTestSupport.Run(new[] { "--data", data, "--batch", "--games", "1", "--maps", "greybox_1v1", "--matchups", "skai_vs_water",
                "--jobs", "1", "--out", HarnessTestSupport.NewFolder(), "--test-fault", BatchOptions.FaultWorkerExit }, out _, out string error);
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error, Does.Contain("test fault: worker asked to exit with code 1"));
        }

        /// <summary>The data edits of the floor arithmetic scenario (see HarnessUnitTests.FloorData): skai against skai, 10 attackers of 3 supply.</summary>
        private static DataEdit[] FloorEdits(int hqHealth)
        {
            return new[]
            {
                new DataEdit("tuning/combat.json", "\"structure\": 0.5", "\"structure\": 1.0"),
                new DataEdit("tuning/harness_batch.json", "\"hq_floor_standoff_tiles\": 2", "\"hq_floor_standoff_tiles\": 0"),
                new DataEdit("units/skai/great_aerie.json", "\"max_health\": 1500", "\"max_health\": " + hqHealth),
                new DataEdit("units/skai/great_aerie.json", "\"damage\": 8", "\"damage\": 0.01"),
                new DataEdit("units/skai/windrunner.json", "\"supply\": 1", "\"supply\": 3"),
                new DataEdit("units/skai/windrunner.json", "\"damage\": 6", "\"damage\": 10"),
                new DataEdit("units/skai/windrunner.json", "\"cooldown_seconds\": 0.8", "\"cooldown_seconds\": 1.0"),
                new DataEdit("units/skai/windrunner.json", "\"hit_delay_ticks\": 2", "\"hit_delay_ticks\": 0"),
                new DataEdit("units/skai/windrunner.json", "\"range_tiles\": 4", "\"range_tiles\": 10"),
                new DataEdit("units/skai/fledgling.json", "\"damage\": 3", "\"damage\": 0.01"),
                new DataEdit("units/skai/roc_rider.json", "\"damage\": 14", "\"damage\": 0.01"),
                new DataEdit("units/skai/stormcaller.json", "\"damage\": 25", "\"damage\": 0.01"),
                new DataEdit("units/skai/talon_archer.json", "\"damage\": 8", "\"damage\": 0.01"),
                // The Thunderhead (specs/missing_roster.md) is a Skai attacker too; it must not be the fastest HQ killer of the arithmetic scenario.
                new DataEdit("units/skai/thunderhead.json", "\"damage\": 44", "\"damage\": 0.01"),
            };
        }

        /// <summary>The array with one more element.</summary>
        private static string[] Append(string[] args, string extra)
        {
            string[] longer = new string[args.Length + 1];
            args.CopyTo(longer, 0);
            longer[args.Length] = extra;
            return longer;
        }
    }
}

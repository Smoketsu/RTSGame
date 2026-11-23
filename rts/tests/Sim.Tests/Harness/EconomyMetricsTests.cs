// rts/tests/Sim.Tests/Harness/EconomyMetricsTests.cs
// Acceptance tests H-E1 to H-E3 of rts/docs/specs/ai_economy_calc.md (package E0): the income_by behaviour check, the two new summary fields (workers_by_bucket, spent_other_by_bucket) and the --behaviour
// output with the new check and its BEHAVIOUR_BY_OPENING lines. The check tests use hand-made summaries; the summary and command tests play real prototype_1v1 games with short caps. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Package E0 tests.</summary>
    [TestFixture]
    public class EconomyMetricsTests
    {
        /// <summary>Ticks per second of the synthetic tests. Unit: ticks per second. Range: 20.</summary>
        private const int TicksPerSecond = 20;

        /// <summary>Ticks in one 30-second income bucket of the synthetic tests. Unit: ticks. Range: 600.</summary>
        private const int BucketTicks = 30 * TicksPerSecond;

        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>An income_by check configured from <paramref name="entry"/> with 30-second buckets and rare weight 1.</summary>
        private static IBehaviourCheck IncomeCheck(string entry)
        {
            IBehaviourCheck check = BehaviourChecks.Create("income_by", new BehaviourContext(10000, BucketTicks, null, TicksPerSecond));
            check.ReadSettings(JsonReader.Parse(entry, "test"));
            return check;
        }

        /// <summary>A synthetic summary that carries only the given income buckets.</summary>
        private static GameSummary IncomeSummary(int[][] income)
        {
            return new GameSummary(0, 0, new SortedDictionary<string, int[]>(), new List<int[]?>(), income, new List<int[]>(), new SortedDictionary<string, int>(), new SortedDictionary<string, int>(),
                new SortedDictionary<string, int>());
        }

        /// <summary>A throwaway game record that ended at tick <paramref name="endTick"/>.</summary>
        private static GameRecord GameEndingAt(int endTick)
        {
            return new GameRecord("prototype_1v1", "water_vs_water", 0, 1UL, "water", "water", "a", "b", GameRecord.Undecided, endTick, 1UL, 1UL, 1UL, new List<HqKill>());
        }

        /// <summary>H-E1: buckets of 30 s with deposits [100,0], [200,50], ...: the metric at 60 s is 350 with rare weight 1; at 90 s with the game ended at 75 s it is Unjudged; at_seconds 45 with 30-s buckets is a settings error naming at_seconds.</summary>
        [Test]
        public void IncomeBy_MetricUnjudgedAndSettings()
        {
            int[][] income = { new[] { 100, 0 }, new[] { 200, 50 }, new[] { 300, 100 } };
            IBehaviourCheck at60 = IncomeCheck("{\"enabled\": true, \"at_seconds\": 60, \"min_income\": 0, \"max_violation_fraction\": 0, \"print_by_opening\": true}");
            BehaviourResult result = at60.Evaluate(IncomeSummary(income), GameEndingAt(2399));
            Assert.That(result.Outcome, Is.EqualTo(CheckOutcome.Held));
            Assert.That(PredictEvaluator.Ratio3(result.Num, result.Den), Is.EqualTo("350.000"));
            Assert.That(at60.PrintByOpening, Is.True);
            // The bar: 350 holds at min 350 and violates at min 351 (edge inclusive).
            IBehaviourCheck min350 = IncomeCheck("{\"enabled\": true, \"at_seconds\": 60, \"min_income\": 350, \"max_violation_fraction\": 0, \"print_by_opening\": false}");
            Assert.That(min350.Evaluate(IncomeSummary(income), GameEndingAt(2399)).Outcome, Is.EqualTo(CheckOutcome.Held));
            IBehaviourCheck min351 = IncomeCheck("{\"enabled\": true, \"at_seconds\": 60, \"min_income\": 351, \"max_violation_fraction\": 0, \"print_by_opening\": false}");
            Assert.That(min351.Evaluate(IncomeSummary(income), GameEndingAt(2399)).Outcome, Is.EqualTo(CheckOutcome.Violated));
            // Game ended at 75 s (last tick 1499) before 90 s: nothing to judge, although three buckets exist.
            IBehaviourCheck at90 = IncomeCheck("{\"enabled\": true, \"at_seconds\": 90, \"min_income\": 0, \"max_violation_fraction\": 0, \"print_by_opening\": true}");
            Assert.That(at90.Evaluate(IncomeSummary(income), GameEndingAt(75 * TicksPerSecond - 1)).Outcome, Is.EqualTo(CheckOutcome.Unjudged));
            Assert.That(at90.Evaluate(IncomeSummary(income), GameEndingAt(90 * TicksPerSecond - 1)).Outcome, Is.EqualTo(CheckOutcome.Held));
            DataValidationException bad = Expect.Throws<DataValidationException>(() => IncomeCheck("{\"enabled\": true, \"at_seconds\": 45, \"min_income\": 0, \"max_violation_fraction\": 0, \"print_by_opening\": true}"));
            Assert.That(bad.Message, Does.Contain("at_seconds"));
        }

        /// <summary>H-E2: a 120-s game writes four workers_by_bucket and four spent_other_by_bucket entries that read back equal; worker and drop_off spending are not in spent_other_by_bucket; a record without the fields parses with empty lists.</summary>
        [Test]
        public void Summary_BucketFields_WrittenAndReadBack()
        {
            DataSet data = TestData.LoadAll();
            ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", "skai_vs_water", 0);
            GameRecord watched = GameRunner.Play(data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 120, null, -1, -1, new SummaryObserver(30, new[] { 60 }));
            foreach (GameSummary summary in new[] { watched.Summary0!, watched.Summary1! })
            {
                Assert.That(summary.WorkersByBucket.Count, Is.EqualTo(4));
                Assert.That(summary.SpentOtherByBucket.Count, Is.EqualTo(4));
                Assert.That(summary.Income.Count, Is.EqualTo(4));
                Assert.That(summary.WorkersByBucket[0], Is.GreaterThan(0));
                // Own workers never fall to zero in a 120-s opening, and a worker is bought in it (so the exclusion below is not vacuous).
                Assert.That(summary.Spent[PlayerLedger.ClassWorker][0], Is.GreaterThan(0));
                // Net spending of the other classes: the totals less the worker and drop_off classes, summed over the buckets.
                int common = 0;
                int rare = 0;
                foreach (int[] bucket in summary.SpentOtherByBucket)
                {
                    common += bucket[0];
                    rare += bucket[1];
                }
                Assert.That(common, Is.EqualTo(summary.SpentCommon - summary.Spent[PlayerLedger.ClassWorker][0] - summary.Spent[PlayerLedger.ClassDropOff][0]));
                Assert.That(rare, Is.EqualTo(summary.SpentRare - summary.Spent[PlayerLedger.ClassWorker][1] - summary.Spent[PlayerLedger.ClassDropOff][1]));
            }
            string line = watched.ToJsonLine();
            GameRecord back = GameRecord.Parse(line);
            Assert.That(back.ToJsonLine(), Is.EqualTo(line));
            Assert.That(back.Summary0!.WorkersByBucket, Is.EqualTo(watched.Summary0!.WorkersByBucket));
            Assert.That(back.Summary1!.SpentOtherByBucket[1], Is.EqualTo(watched.Summary1!.SpentOtherByBucket[1]));
            // An old record: strip the two fields.
            string old = Regex.Replace(line, ",\"workers_by_bucket\":\\[[^\\]]*\\],\"spent_other_by_bucket\":\\[(\\[[^\\]]*\\],?)*\\]", "");
            Assert.That(old, Does.Not.Contain("workers_by_bucket").And.Not.Contain("spent_other_by_bucket"));
            GameRecord parsed = GameRecord.Parse(old);
            Assert.That(parsed.Summary0!.WorkersByBucket, Is.Empty);
            Assert.That(parsed.Summary0.SpentOtherByBucket, Is.Empty);
        }

        /// <summary>H-E3: --behaviour with the shipped checks (income_by at 60 s in a data copy) prints the income_by pair of lines each followed by its BEHAVIOUR_BY_OPENING lines in ordinal order, the four older checks print as before, BEHAVIOUR_SUMMARY counts six checks (econ_gap added by E-OPT), and behaviour.jsonl without the two new fields parses to summaries without them.</summary>
        [Test]
        public void Command_PrintsIncomeByAndOpeningLines()
        {
            string data = HarnessTestSupport.CopyData();
            string checksPath = Path.Combine(data, BehaviourSettings.RelativePath);
            // income_by judged at 60 s so a 120-s game has something to judge.
            string text = Regex.Replace(File.ReadAllText(checksPath), "\"at_seconds\": 300,(\\s*//[^\\n]*\\n\\s*)\"min_income\"", "\"at_seconds\": 60,$1\"min_income\"");
            Assert.That(text, Does.Contain("\"at_seconds\": 60,"));
            File.WriteAllText(checksPath, text);
            string folder = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            int exit = HarnessTestSupport.Run(new[] { "--data", data, "--behaviour", "--profile-a", "aggressive_s5", "--profile-b", "aggressive_s5", "--games", "4", "--maps", "prototype_1v1", "--matchups", "skai_vs_water",
                "--max-game-seconds", "120", "--jobs", "4", "--out", folder }, out string stdout, out string stderr);
            Assert.That(exit, Is.AnyOf(0, 2), stderr);
            string[] lines = stdout.TrimEnd('\n').Split('\n');
            Assert.That(lines[lines.Length - 1], Does.EndWith(" of=6"), "five checks of E0 and earlier plus econ_gap (package E-OPT)");
            // Every check has two BEHAVIOUR lines (candidate, baseline); only income_by has opening lines.
            int behaviourLines = 0;
            foreach (string line in lines)
            {
                if (line.StartsWith("BEHAVIOUR ", StringComparison.Ordinal))
                {
                    behaviourLines++;
                }
            }
            Assert.That(behaviourLines, Is.EqualTo(12));
            int incomeIndex = Array.FindIndex(lines, l => l.StartsWith("BEHAVIOUR income_by ", StringComparison.Ordinal));
            Assert.That(incomeIndex, Is.GreaterThanOrEqualTo(0));
            Regex format = new Regex(@"^BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=(skai|water) opening=(\S*) games=\d+ metric_median=\S+$");
            // The opening lines directly after the first income_by line, up to the next BEHAVIOUR line; their (faction, opening) keys ascend.
            string previous = "";
            int count = 0;
            for (int i = incomeIndex + 1; i < lines.Length && lines[i].StartsWith("BEHAVIOUR_BY_OPENING", StringComparison.Ordinal); i++)
            {
                Match match = format.Match(lines[i]);
                Assert.That(match.Success, Is.True, lines[i]);
                string key = match.Groups[1].Value + " " + match.Groups[2].Value;
                Assert.That(string.CompareOrdinal(previous, key), Is.LessThan(0), lines[i]);
                previous = key;
                count++;
            }
            Assert.That(count, Is.GreaterThan(0));
            // The record lines without the two new fields still parse, and read as empty.
            foreach (string jsonLine in File.ReadAllLines(Path.Combine(folder, "behaviour.jsonl")))
            {
                string old = Regex.Replace(jsonLine, ",\"workers_by_bucket\":\\[[^\\]]*\\],\"spent_other_by_bucket\":\\[(\\[[^\\]]*\\],?)*\\]", "");
                Assert.That(GameRecord.Parse(old).Summary0!.WorkersByBucket, Is.Empty);
            }
        }
    }
}

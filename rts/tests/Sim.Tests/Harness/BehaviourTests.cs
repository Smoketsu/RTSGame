// rts/tests/Sim.Tests/Harness/BehaviourTests.cs
// Acceptance tests B-1 to B-5 of rts/docs/specs/balance_measurement.md (package M, section M.4): the read-only summary observer, the static defence share check, the HQ move check, the registry and its data file, and
// the --behaviour command. Real games use prototype_1v1 with short caps; the check tests use synthetic summaries. Part of Rts.Sim.Tests. The command test starts worker processes.
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
    /// <summary>Summary observer and behaviour check tests.</summary>
    [TestFixture]
    public class BehaviourTests
    {
        /// <summary>Ticks in one income bucket of the synthetic tests. Unit: ticks.</summary>
        private const int BucketTicks = 600;

        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>A configured check from an entry written as JSON text (rare_weight in parts per 10 000).</summary>
        private static IBehaviourCheck Check(string id, string entry, int rareWeightPt = 10000)
        {
            IBehaviourCheck check = BehaviourChecks.Create(id, new BehaviourContext(rareWeightPt, BucketTicks));
            check.ReadSettings(JsonReader.Parse(entry, "test"));
            return check;
        }

        /// <summary>A synthetic summary: spending totals, the static defence split and income buckets and HQ moves.</summary>
        private static GameSummary Summary(int spentCommon, int spentRare, int sdCommon, int sdRare, int[][]? income = null, int[][]? moves = null)
        {
            SortedDictionary<string, int[]> spent = new SortedDictionary<string, int[]>(StringComparer.Ordinal) { [PlayerLedger.ClassStaticDefence] = new[] { sdCommon, sdRare } };
            return new GameSummary(spentCommon, spentRare, spent, new List<int[]?>(), income ?? new int[0][], moves ?? new int[0][], new SortedDictionary<string, int>(), new SortedDictionary<string, int>(),
                new SortedDictionary<string, int>());
        }

        /// <summary>A throwaway game record for checks that read only the summary.</summary>
        private static GameRecord Game()
        {
            return new GameRecord("prototype_1v1", "water_vs_water", 0, 1UL, "water", "water", "a", "b", GameRecord.Undecided, 1, 1UL, 1UL, 1UL, new List<HqKill>());
        }

        /// <summary>The "spent=Nc/Mr" totals of each player in a trace, in player order.</summary>
        private static List<int[]> TraceSpent(string trace)
        {
            List<int[]> totals = new List<int[]>();
            foreach (Match match in Regex.Matches(trace, @"gathered=\d+c/\d+r  spent=(-?\d+)c/(-?\d+)r"))
            {
                totals.Add(new[] { int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value) });
            }
            return totals;
        }

        /// <summary>B-1: one game with and without the summary observer has the same state hash and play signature; the summary's spending equals the GameTrace totals of the same game; the record line round-trips.</summary>
        [Test]
        public void Observer_IsReadOnly_AndMatchesTheTrace()
        {
            DataSet data = TestData.LoadAll();
            ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", "water_vs_water", 0);
            GameRecord plain = GameRunner.Play(data, "prototype_1v1", "water_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 120);
            GameRecord watched = GameRunner.Play(data, "prototype_1v1", "water_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 120, null, -1, -1,
                new SummaryObserver(30, new[] { 60, 100, 900 }));
            Assert.That(watched.StateHash, Is.EqualTo(plain.StateHash));
            Assert.That(watched.PlaySignature, Is.EqualTo(plain.PlaySignature));
            Assert.That(plain.Summary0, Is.Null);
            Assert.That(watched.Summary0, Is.Not.Null);
            Assert.That(watched.Summary1, Is.Not.Null);
            // The trace of the same game: spending per player.
            StringWriter text = new StringWriter();
            GameRunner.Play(data, "prototype_1v1", "water_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 120, new GameTrace(text, 30));
            List<int[]> spent = TraceSpent(text.ToString());
            Assert.That(spent.Count, Is.EqualTo(2));
            Assert.That(watched.Summary0!.SpentCommon, Is.EqualTo(spent[0][0]));
            Assert.That(watched.Summary0.SpentRare, Is.EqualTo(spent[0][1]));
            Assert.That(watched.Summary1!.SpentCommon, Is.EqualTo(spent[1][0]));
            Assert.That(watched.Summary1.SpentRare, Is.EqualTo(spent[1][1]));
            Assert.That(watched.Summary0.SpentCommon, Is.GreaterThan(0), "the AI spent something");
            // Class split adds up to the totals; the bank at 60 s and 100 s exists (the game ran 120 s), the one at 900 s does not; income covers the game.
            int classSum = 0;
            foreach (int[] split in watched.Summary0.Spent.Values)
            {
                classSum += split[0];
            }
            Assert.That(classSum, Is.EqualTo(watched.Summary0.SpentCommon));
            Assert.That(watched.Summary0.Bank[0], Is.Not.Null);
            Assert.That(watched.Summary0.Bank[1], Is.Not.Null);
            Assert.That(watched.Summary0.Bank[2], Is.Null);
            Assert.That(watched.Summary0.Income.Count, Is.EqualTo(plain.EndTick / (30 * 20) + 1));
            Assert.That(watched.Summary0.Trained.Count, Is.GreaterThan(0));
            // The line parses back to the same line.
            string line = watched.ToJsonLine();
            Assert.That(GameRecord.Parse(line).ToJsonLine(), Is.EqualTo(line));
        }

        /// <summary>B-2: 1000 common with 150 on static defence holds at 0.15 (edge inclusive); 151 violates; spending nothing holds; rare counts with rare_weight.</summary>
        [Test]
        public void StaticDefenceShare_EdgeAndWeights()
        {
            const string entry = "{\"enabled\": true, \"max_share\": 0.15, \"max_violation_fraction\": 0.05}";
            IBehaviourCheck check = Check("static_defence_share", entry);
            Assert.That(check.Evaluate(Summary(1000, 0, 150, 0), Game()).Outcome, Is.EqualTo(CheckOutcome.Held));
            Assert.That(check.Evaluate(Summary(1000, 0, 151, 0), Game()).Outcome, Is.EqualTo(CheckOutcome.Violated));
            BehaviourResult none = check.Evaluate(Summary(0, 0, 0, 0), Game());
            Assert.That(none.Outcome, Is.EqualTo(CheckOutcome.Held));
            Assert.That(none.Num, Is.EqualTo(0));
            // Rare weight 2: 500 common + 250 rare = 1000; 75 rare on defence is 150, 76 is 152.
            IBehaviourCheck weighted = Check("static_defence_share", entry, 20000);
            Assert.That(weighted.Evaluate(Summary(500, 250, 0, 75), Game()).Outcome, Is.EqualTo(CheckOutcome.Held));
            Assert.That(weighted.Evaluate(Summary(500, 250, 0, 76), Game()).Outcome, Is.EqualTo(CheckOutcome.Violated));
            // The profile passes when violated x 10 000 <= 500 x judged: 1 of 20 passes, 2 of 20 fails.
            CheckTally tally = new CheckTally("p", 0);
            for (int i = 0; i < 19; i++)
            {
                tally.Add(check.Evaluate(Summary(1000, 0, 100, 0), Game()));
            }
            tally.Add(check.Evaluate(Summary(1000, 0, 200, 0), Game()));
            Assert.That(tally.Passes(check.MaxViolationPt), Is.True);
            tally.Add(check.Evaluate(Summary(1000, 0, 200, 0), Game()));
            Assert.That(tally.Passes(check.MaxViolationPt), Is.False);
        }

        /// <summary>Income buckets b0 to b(n-1) from per-bucket common amounts (no rare).</summary>
        private static int[][] Income(params int[] common)
        {
            int[][] buckets = new int[common.Length][];
            for (int i = 0; i < common.Length; i++)
            {
                buckets[i] = new[] { common[i], 0 };
            }
            return buckets;
        }

        /// <summary>B-3: with a 10 percent gain, window 1 and settle 0: before 600 and after 700 holds, after 650 violates, after 660 holds (edge); a move with the uproot in the first window or the after window past the end is unjudged; a move under the minimum distance or with root -1 is ignored.</summary>
        [Test]
        public void HqMove_GainEdgeAndJudging()
        {
            IBehaviourCheck check = Check("hq_move_new_income", "{\"enabled\": true, \"min_distance_tiles\": 4, \"window_buckets\": 1, \"settle_buckets\": 0, \"min_gain_fraction\": 0.10, \"max_violation_fraction\": 0}");
            // Uproot in bucket 3 (tick 1800), root in bucket 4 (tick 2500), 5 tiles; before = bucket 2 = 600, after = bucket 4.
            int[][] Moves(int distanceTenths, int rootTick = 2500) => new[] { new[] { 1800, rootTick, distanceTenths } };
            Assert.That(check.Evaluate(Summary(0, 0, 0, 0, Income(0, 0, 600, 0, 700, 0), Moves(50)), Game()).Outcome, Is.EqualTo(CheckOutcome.Held));
            BehaviourResult violating = check.Evaluate(Summary(0, 0, 0, 0, Income(0, 0, 600, 0, 650, 0), Moves(50)), Game());
            Assert.That(violating.Outcome, Is.EqualTo(CheckOutcome.Violated));
            Assert.That(violating.Extras, Is.EqualTo(new long[] { 1, 1, 0 }));
            Assert.That(check.Evaluate(Summary(0, 0, 0, 0, Income(0, 0, 600, 0, 660, 0), Moves(50)), Game()).Outcome, Is.EqualTo(CheckOutcome.Held));
            // Uproot in bucket 0: fewer than K buckets before it.
            BehaviourResult early = check.Evaluate(Summary(0, 0, 0, 0, Income(600, 0, 0, 0, 700, 0), new[] { new[] { 10, 2500, 50 } }), Game());
            Assert.That(early.Outcome, Is.EqualTo(CheckOutcome.Unjudged));
            Assert.That(early.Extras, Is.EqualTo(new long[] { 1, 0, 1 }));
            // Root in the last bucket + 1: the after window runs past the end (5 buckets, root in bucket 5).
            Assert.That(check.Evaluate(Summary(0, 0, 0, 0, Income(0, 0, 600, 0, 700), Moves(50, 3100)), Game()).Outcome, Is.EqualTo(CheckOutcome.Unjudged));
            // 3 tiles is under the 4 tile minimum; root -1 never came.
            BehaviourResult shortMove = check.Evaluate(Summary(0, 0, 0, 0, Income(0, 0, 600, 0, 10, 0), Moves(30)), Game());
            Assert.That(shortMove.Outcome, Is.EqualTo(CheckOutcome.Held));
            Assert.That(shortMove.Extras[0], Is.EqualTo(0));
            Assert.That(check.Evaluate(Summary(0, 0, 0, 0, Income(0, 0, 600, 0, 10, 0), Moves(50, -1)), Game()).Outcome, Is.EqualTo(CheckOutcome.Held));
        }

        /// <summary>B-4: a data entry whose ID is not registered fails naming the entry; a registered check missing from the data is not run and is listed as not declared; an unknown --checks ID is an error; the shipped file parses.</summary>
        [Test]
        public void Registry_AndDataFile()
        {
            BehaviourSettings shipped = BehaviourSettings.Load(TestData.FindDataDirectory());
            // Four checks added by ai_reef_runner_defence.md RR-M: 9 becomes 13.
            Assert.That(shipped.Entries.Count, Is.EqualTo(13), "five older checks, econ_gap (package E-OPT), the three package S-M checks and the four package RR-M checks");
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), BehaviourSettings.RelativePath));
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => BehaviourSettings.Parse(text.Replace("\"static_defence_share\": {", "\"no_such_check\": {")));
            Assert.That(unknown.Message, Does.StartWith(BehaviourSettings.SourceName).And.Contain("no_such_check"));
            DataValidationException badKey = Expect.Throws<DataValidationException>(() => BehaviourSettings.Parse(text.Replace("\"max_share\": 0.15,", "\"max_share\": 0.15, \"extra\": 1,")));
            Assert.That(badKey.Message, Does.StartWith(BehaviourSettings.SourceName).And.Contain("extra"));
            // Only one check declared.
            BehaviourSettings one = BehaviourSettings.Parse("{ \"$schema_version\": 1, \"$doc\": \"x\", \"games_per_cell\": 4, \"rare_weight\": 1, \"checks\": { \"static_defence_share\": { \"enabled\": true, \"max_share\": 0.15, \"max_violation_fraction\": 0.05 } } }");
            List<string> notDeclared = new List<string>();
            List<IBehaviourCheck> checks = one.Checks(null, BucketTicks, notDeclared);
            Assert.That(checks.Count, Is.EqualTo(1));
            Assert.That(notDeclared, Is.EqualTo(new[] { "hq_move_new_income", "workers_at", "first_drop_off", "income_by", "econ_gap", "depot_losses_young", "harass_trade", "harass_squads" }));
            Expect.Throws<HarnessException>(() => one.Checks(new[] { "no_such_check" }, BucketTicks, new List<string>()));
        }

        /// <summary>B-5: --behaviour on one mirror cell prints one line per check and profile (candidate first) in the stated format, then BEHAVIOUR_SUMMARY, writes the files and exits 0 or 2 as the lines say; behaviour.md lists a check missing from the data as not declared.</summary>
        [Test]
        public void Command_PrintsLinesAndSummary()
        {
            string data = HarnessTestSupport.CopyData();
            // A data copy with only the static defence check declared.
            File.WriteAllText(Path.Combine(data, BehaviourSettings.RelativePath),
                "{ \"$schema_version\": 1, \"$doc\": \"x\", \"games_per_cell\": 8, \"rare_weight\": 1, \"checks\": { \"static_defence_share\": { \"enabled\": true, \"max_share\": 0.15, \"max_violation_fraction\": 0.05 } } }");
            string folder = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            int exit = HarnessTestSupport.Run(new[] { "--data", data, "--behaviour", "--profile-a", "abl_s5_no_h", "--profile-b", "aggressive_s5", "--games", "8", "--matchups", "water_vs_water",
                "--max-game-seconds", "60", "--jobs", "4", "--out", folder }, out string stdout, out string stderr);
            string[] lines = stdout.TrimEnd('\n').Split('\n');
            Assert.That(lines.Length, Is.EqualTo(3), stderr);
            Regex format = new Regex(@"^BEHAVIOUR static_defence_share profile=(\S+) games=8 held=\d+ violated=\d+ unjudged=\d+ metric_median=\S+ metric_max=\S+ bar=0\.150 (PASS|FAIL)$");
            Match candidate = format.Match(lines[0]);
            Match baseline = format.Match(lines[1]);
            Assert.That(candidate.Success, Is.True, lines[0]);
            Assert.That(baseline.Success, Is.True, lines[1]);
            Assert.That(candidate.Groups[1].Value, Is.EqualTo("aggressive_s5"));
            Assert.That(baseline.Groups[1].Value, Is.EqualTo("abl_s5_no_h"));
            Assert.That(lines[2], Is.EqualTo("BEHAVIOUR_SUMMARY candidate=aggressive_s5 passed=" + (candidate.Groups[2].Value == "PASS" ? "1" : "0") + " of=1"));
            Assert.That(exit, Is.EqualTo(candidate.Groups[2].Value == "PASS" ? 0 : 2));
            Assert.That(File.ReadAllLines(Path.Combine(folder, "behaviour.jsonl")).Length, Is.EqualTo(8));
            Assert.That(File.ReadAllText(Path.Combine(folder, "behaviour.md")), Does.Contain("not run): hq_move_new_income"));
        }
    }
}

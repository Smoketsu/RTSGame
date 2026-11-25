// rts/tests/Sim.Tests/Harness/ReefDefenceHarnessTests.cs
// Acceptance tests of package RR-M (rts/docs/specs/ai_reef_runner_defence.md 4.3): the four checks won, worker_losses_pre_overrun, army_supply_ratio_at and trained_unit, and the --paired-faction filter of the
// paired behaviour test, on hand-made records. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Tests of the RR-M checks and the faction-paired test.</summary>
    [TestFixture]
    public class ReefDefenceHarnessTests
    {
        /// <summary>Entry text of the four checks' common keys. Range: fixed.</summary>
        private const string Common = "\"enabled\": true, \"max_violation_fraction\": 1, \"paired_direction\": \"higher\"";

        /// <summary>Sample seconds of the shipped ai_eval.json. Unit: seconds. Range: 180, 300, 450.</summary>
        private static readonly int[] Samples = { 180, 300, 450 };

        /// <summary>Loaded data, shared by the tests.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>A configured check from entry text, with the sample seconds and the data set known.</summary>
        private static IBehaviourCheck Check(string id, string entry)
        {
            IBehaviourCheck check = BehaviourChecks.Create(id, new BehaviourContext(10000, 600, null, 20, Data, 2400, Samples));
            check.ReadSettings(JsonReader.Parse(entry, "test"));
            return check;
        }

        /// <summary>Production metrics with the given pre-overrun worker losses and army supply samples (180, 300, 450 s).</summary>
        private static ProductionMetrics Production(int preOverrun, params int?[] army)
        {
            return new ProductionMetrics(preOverrun, 0, 0, 0, new List<int?>(), new List<int?>(), new List<int?>(), new List<int?>(army), null, null, preOverrun);
        }

        /// <summary>A summary with the given trained counts and production metrics (null for none).</summary>
        private static GameSummary Summary(ProductionMetrics? production = null, Dictionary<string, int>? trained = null)
        {
            return new GameSummary(0, 0, new SortedDictionary<string, int[]>(StringComparer.Ordinal), new List<int[]?>(), new int[0][], new int[0][],
                new SortedDictionary<string, int>(trained ?? new Dictionary<string, int>(), StringComparer.Ordinal), new SortedDictionary<string, int>(), new SortedDictionary<string, int>(), null, null, null, production);
        }

        /// <summary>A record of one game: factions, result, candidate slot and the two summaries; pair 0 of a prototype_1v1 cell.</summary>
        private static GameRecord Game(string f0, string f1, string result, int candidateSlot, GameSummary s0, GameSummary s1, string matchup = "x", int pair = 0)
        {
            return new GameRecord("prototype_1v1", matchup, pair * 2 + candidateSlot, 1UL, f0, f1, "base", "cand", result, 1, 1UL, 1UL, 1UL, new List<HqKill>(), "", "", pair, candidateSlot, "", s0, s1);
        }

        /// <summary>A summary whose only content is the worker losses before the overrun.</summary>
        private static GameSummary Losses(int losses)
        {
            return Summary(Production(losses));
        }

        /// <summary>RR-M 1: won is 1 / 1 for the named slot and 0 / 1 for the other; a draw is 0 / 1 for both.</summary>
        [Test]
        public void Won_ReadsTheResult()
        {
            IBehaviourCheck check = Check("won", "{ " + Common + " }");
            GameSummary s0 = Summary();
            GameSummary s1 = Summary();
            GameRecord slot1 = Game("skai", "water", GameRecord.Slot1, 0, s0, s1);
            Assert.That(check.Evaluate(s1, slot1).Num, Is.EqualTo(1));
            Assert.That(check.Evaluate(s1, slot1).Den, Is.EqualTo(1));
            Assert.That(check.Evaluate(s0, slot1).Num, Is.EqualTo(0));
            Assert.That(check.Evaluate(s0, slot1).Den, Is.EqualTo(1));
            GameRecord draw = Game("skai", "water", GameRecord.Draw, 0, s0, s1);
            Assert.That(check.Evaluate(s0, draw).Num, Is.EqualTo(0));
            Assert.That(check.Evaluate(s1, draw).Num, Is.EqualTo(0));
            Assert.That(check.Evaluate(s1, draw).Outcome, Is.EqualTo(CheckOutcome.Held));
        }

        /// <summary>RR-M 2: worker_losses_pre_overrun reads the field; a summary without production metrics is Unjudged.</summary>
        [Test]
        public void WorkerLossesPreOverrun_ReadsTheField()
        {
            IBehaviourCheck check = Check("worker_losses_pre_overrun", "{ " + Common + " }");
            GameSummary with = Losses(7);
            GameSummary without = Summary();
            GameRecord game = Game("skai", "water", GameRecord.Slot0, 0, with, without);
            BehaviourResult result = check.Evaluate(with, game);
            Assert.That(result.Num, Is.EqualTo(7));
            Assert.That(result.Den, Is.EqualTo(1));
            Assert.That(check.Evaluate(without, game).Outcome, Is.EqualTo(CheckOutcome.Unjudged));
        }

        /// <summary>RR-M 3: army_supply_ratio_at at 300 s is exactly own / enemy, enemy 0 gives own / 1, a null sample is Unjudged, and at_seconds 240 fails to load naming the key.</summary>
        [Test]
        public void ArmySupplyRatioAt_ReadsTheSample()
        {
            IBehaviourCheck check = Check("army_supply_ratio_at", "{ \"enabled\": true, \"at_seconds\": 300, \"max_violation_fraction\": 1, \"paired_direction\": \"higher\" }");
            GameSummary own = Summary(Production(0, 5, 12, 20));
            GameSummary enemy = Summary(Production(0, 6, 30, 40));
            BehaviourResult ratio = check.Evaluate(own, Game("skai", "water", GameRecord.Slot0, 0, own, enemy));
            Assert.That(ratio.Num, Is.EqualTo(12));
            Assert.That(ratio.Den, Is.EqualTo(30));
            GameSummary none = Summary(Production(0, 6, 0, 40));
            BehaviourResult zero = check.Evaluate(own, Game("skai", "water", GameRecord.Slot0, 0, own, none));
            Assert.That(zero.Num, Is.EqualTo(12));
            Assert.That(zero.Den, Is.EqualTo(1));
            GameSummary ended = Summary(Production(0, 6, null, null));
            Assert.That(check.Evaluate(own, Game("skai", "water", GameRecord.Slot0, 0, own, ended)).Outcome, Is.EqualTo(CheckOutcome.Unjudged));
            Assert.That(check.Evaluate(ended, Game("skai", "water", GameRecord.Slot0, 0, own, ended)).Outcome, Is.EqualTo(CheckOutcome.Unjudged));
            Assert.That(check.Evaluate(Summary(), Game("skai", "water", GameRecord.Slot0, 0, own, Summary())).Outcome, Is.EqualTo(CheckOutcome.Unjudged), "no production metrics");
            DataValidationException bad = Expect.Throws<DataValidationException>(() => Check("army_supply_ratio_at", "{ \"enabled\": true, \"at_seconds\": 240, \"max_violation_fraction\": 1 }"));
            Assert.That(bad.Message, Does.Contain("at_seconds"));
        }

        /// <summary>RR-M 4: trained_unit reads the count (0 when absent); an unknown unit fails to load naming the key.</summary>
        [Test]
        public void TrainedUnit_ReadsTheCount()
        {
            IBehaviourCheck check = Check("trained_unit", "{ \"enabled\": true, \"unit\": \"water.reef_runner\", \"max_violation_fraction\": 1, \"paired_direction\": \"higher\" }");
            GameSummary some = Summary(null, new Dictionary<string, int> { ["water.reef_runner"] = 9, ["water.grasper"] = 2 });
            GameSummary absent = Summary(null, new Dictionary<string, int> { ["water.grasper"] = 2 });
            GameRecord game = Game("water", "skai", GameRecord.Slot0, 0, some, absent);
            Assert.That(check.Evaluate(some, game).Num, Is.EqualTo(9));
            Assert.That(check.Evaluate(absent, game).Num, Is.EqualTo(0));
            DataValidationException bad = Expect.Throws<DataValidationException>(() => Check("trained_unit", "{ \"enabled\": true, \"unit\": \"water.nope\", \"max_violation_fraction\": 1 }"));
            Assert.That(bad.Message, Does.Contain("unit"));
        }

        /// <summary>
        /// A cross pair of hand-made records, candidate "cand" against "base": game 0 has the candidate in slot 0 as Water and the baseline as Skai, game 1 the candidate in slot 1 as Skai and the baseline as Water.
        /// Worker losses before the overrun: candidate Water 3, baseline Water 6, candidate Skai 9, baseline Skai 0.
        /// </summary>
        private static List<GameRecord> CrossPair()
        {
            return new List<GameRecord>
            {
                Game("water", "skai", GameRecord.Slot0, 0, Losses(3), Losses(0), "water_vs_skai"),
                Game("water", "skai", GameRecord.Slot0, 1, Losses(6), Losses(9), "water_vs_skai"),
            };
        }

        /// <summary>The paired row of worker_losses_pre_overrun (lower is better) with an optional faction filter.</summary>
        private static PairedCheckRow Paired(List<GameRecord> records, string faction)
        {
            IBehaviourCheck check = Check("worker_losses_pre_overrun", "{ \"enabled\": true, \"max_violation_fraction\": 1, \"paired_direction\": \"lower\" }");
            return BehaviourRunner.PairedTest(check, "lower", records, "base", "cand", 500, 28016, faction);
        }

        /// <summary>RR-M 5: the faction filter judges Water's slots only (3 against 6 is better; unfiltered 12 against 6 is worse), a Skai mirror pair is skipped, and the line ends with the faction and skipped_pairs.</summary>
        [Test]
        public void PairedFaction_FiltersTheSlotGames()
        {
            // (a) Candidate Water slot 3 against baseline Water slot 6; the candidate's Skai 9 and the baseline's Skai 0 are ignored.
            List<GameRecord> cross = CrossPair();
            PairedCheckRow filtered = Paired(cross, "water");
            Assert.That(filtered.Better, Is.EqualTo(1));
            Assert.That(filtered.Worse, Is.EqualTo(0));
            Assert.That(filtered.SkippedPairs, Is.EqualTo(0));
            PairedCheckRow unfiltered = Paired(cross, string.Empty);
            Assert.That(unfiltered.Better, Is.EqualTo(0));
            Assert.That(unfiltered.Worse, Is.EqualTo(1), "3 + 9 = 12 against 6 + 0 = 6");
            Assert.That(unfiltered.Line(), Does.Not.Contain("faction="));
            // (b) A Skai mirror pair has no Water slot-game: skipped, not unjudged.
            List<GameRecord> mirror = new List<GameRecord>
            {
                Game("skai", "skai", GameRecord.Slot0, 0, Losses(1), Losses(2), "skai_vs_skai"),
                Game("skai", "skai", GameRecord.Slot0, 1, Losses(1), Losses(2), "skai_vs_skai"),
            };
            PairedCheckRow skipped = Paired(mirror, "water");
            Assert.That(skipped.SkippedPairs, Is.EqualTo(1));
            Assert.That(skipped.UnjudgedPairs, Is.EqualTo(0));
            Assert.That(skipped.Pairs, Is.EqualTo(0));
            // (c) The line and the table row end with the faction and the skipped count.
            Assert.That(skipped.Line(), Does.EndWith(" faction=water skipped_pairs=1"));
            Assert.That(skipped.TableRow(), Does.EndWith(" | water | 1 |\n"));
            Assert.That(filtered.Line(), Does.EndWith(" faction=water skipped_pairs=0"));
        }

        /// <summary>RR-M 7: --paired-faction with --ladder is an error naming the flag, as is a faction that is not in the data set.</summary>
        [Test]
        public void PairedFaction_Errors()
        {
            HarnessException ladder = Expect.Throws<HarnessException>(() => BatchOptions.Parse(new[] { "--data", "d", "--ladder", "--profile-a", "a", "--profile-b", "b", "--paired-faction", "water" }));
            Assert.That(ladder.Message, Does.Contain("--paired-faction"));
            BatchOptions ok = BatchOptions.Parse(new[] { "--data", "d", "--behaviour", "--profile-a", "a", "--profile-b", "b", "--paired-faction", "water" });
            Assert.That(ok.PairedFaction, Is.EqualTo("water"));
            string folder = System.IO.Path.Combine(HarnessTestSupport.NewFolder(), "out");
            int exit = HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--behaviour", "--profile-a", "aggressive_s5", "--profile-b", "aggressive_s5", "--games", "4", "--matchups", "water_vs_water",
                "--max-game-seconds", "60", "--jobs", "1", "--paired-faction", "earth", "--out", folder }, out string stdout, out string stderr);
            Assert.That(exit, Is.EqualTo(1), stdout);
            Assert.That(stderr, Does.Contain("--paired-faction"));
        }
    }
}

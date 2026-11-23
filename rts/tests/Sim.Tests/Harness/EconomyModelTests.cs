// rts/tests/Sim.Tests/Harness/EconomyModelTests.cs
// Acceptance tests H-E4 to H-E8 of rts/docs/specs/ai_economy_calc.md (package E-OPT): the integer economy model (hand computation, replay identity, search tie-break, withdrawals) and the econ_gap check's Unjudged
// outcome and settings errors. Every model test builds its inputs by hand (no data set), so the numbers can be recomputed on paper. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Package E-OPT tests.</summary>
    [TestFixture]
    public class EconomyModelTests
    {
        /// <summary>A common field whose centre is at (<paramref name="x"/>, <paramref name="y"/>) tiles. Unit: tiles.</summary>
        private static ModelField Common(int x, int y, int amount)
        {
            return new ModelField(new FixVec2(Fix64.FromInt(x), Fix64.FromInt(y)), false, amount);
        }

        /// <summary>
        /// Inputs for a hand-built map: spawn at the origin, speed 2.5, interact 1, HQ radius 0, depot radius 0, 20 ticks a second, 30-s buckets, 200 in the bank, 2 workers, and the data's usual economy numbers
        /// (load 5 held 40 ticks, 3 slots, worker 50 and 240 ticks, depot 100 and 400 ticks, placement 40, ring 3, reach 10).
        /// </summary>
        private static ModelInputs Inputs(IReadOnlyList<ModelField> fields, IReadOnlyList<long>? withdrawals = null, int workers = 2, int bucketSeconds = 30)
        {
            return new ModelInputs
            {
                Fields = fields,
                Spawn = new FixVec2(Fix64.Zero, Fix64.Zero),
                WorkerSpeed = Fix64.Parse("2.5"),
                StartingWorkers = workers,
                BucketSeconds = bucketSeconds,
                OtherSpending = withdrawals ?? Array.Empty<long>(),
            };
        }

        /// <summary>H-E4: one common field 5 tiles from the spawn, two workers that are never added to, no withdrawals, 60 s: income equals the hand computation (and the model's walk formula equals the AI's).</summary>
        [Test]
        public void Model_OneField_MatchesHandComputation()
        {
            // The walk: one way = 5 - 2 x 1 - 0 = 3 tiles, round trip = 2 x 3 x 20 / 2.5 = 48 ticks, so the trip cycle is 40 + 48 = 88 ticks.
            Fix64 speed = Fix64.Parse("2.5");
            Assert.That(EconomyModel.WalkTicks(Fix64.FromInt(5), Fix64.Zero, Fix64.One, speed, 20), Is.EqualTo(48));
            Assert.That(EconomyModel.WalkTicks(Fix64.Parse("7.3"), Fix64.Parse("0.5"), Fix64.One, speed, 20), Is.EqualTo(AiBrain.WalkTicks(Fix64.Parse("7.3"), Fix64.Parse("0.5"), Fix64.One, speed, 20)), "the copy equals the AI's formula");
            Assert.That(EconomyModel.WalkTicks(Fix64.FromInt(1), Fix64.Zero, Fix64.One, Fix64.Zero, 20), Is.EqualTo(0), "speed 0: no walk time");
            // Two workers use min(2 x 40, 3 x 88) = 80 ticks of slot time per 88-tick cycle, so each second adds 20 x 80 = 1600 slot-ticks; a load needs 40 x 88 = 3520. Over 60 s: 96000 / 3520 = 27 loads of 5.
            EconomyModel model = new EconomyModel(Inputs(new[] { Common(5, 0, 3000) }));
            Assert.That(model.Income(60, 2, Array.Empty<int>()), Is.EqualTo(27 * 5), "worker cap 2 equals the workers, so none is trained");
            // A field of 100 pays at most 100.
            Assert.That(new EconomyModel(Inputs(new[] { Common(5, 0, 100) })).Income(60, 2, Array.Empty<int>()), Is.EqualTo(100));
            // Workers beyond the room earn nothing: room = ceil(3 x 88 / 40) = 7, so 9 workers earn what 7 do (7 x 40 = 280 > 264 slot-ticks, so 3 slots x 88 = 264 is the limit): 20 x 264 x 60 / 3520 = 90 loads.
            Assert.That(new EconomyModel(Inputs(new[] { Common(5, 0, 3000) }, null, 9)).Income(60, 2, Array.Empty<int>()), Is.EqualTo(90 * 5));
        }

        /// <summary>The map of the replay test: three common fields near the HQ and one far common field that only a depot reaches.</summary>
        private static ModelInputs RichMap()
        {
            // 5-second buckets: the replay reads the workers of the bucket before, so its error is the growth within one bucket; with the real 30-s buckets that lag is larger (reported with the measurement).
            return Inputs(new[] { Common(5, 0, 3000), Common(0, 6, 3000), Common(-6, 0, 3000), Common(18, 0, 3000), Common(21, 3, 3000) }, null, 2, 5);
        }

        /// <summary>H-E5: the model's best schedule, fed back as the AI's schedule (workers after each bucket and depot completion ticks), gives MOD within 1 percent of OPT.</summary>
        [Test]
        public void Replay_OfTheBestSchedule_IsWithinOnePercent()
        {
            EconomyModel model = new EconomyModel(RichMap());
            ModelBest best = model.Search(600, 6, 20, 2, 4, 20, 4, 2);
            Assert.That(best.Income, Is.GreaterThan(0));
            List<int> workers = new List<int>();
            List<int> depots = new List<int>();
            Assert.That(model.Income(600, best.WorkerCap, best.Triggers, workers, depots), Is.EqualTo(best.Income), "playing the best schedule again gives its income");
            Assert.That(workers.Count, Is.EqualTo(120), "600 s in 5-s buckets");
            long replay = model.Replay(600, workers, depots);
            Assert.That(Math.Abs(replay - best.Income) * 100, Is.LessThanOrEqualTo(best.Income), "MOD " + replay + " against OPT " + best.Income);
        }

        /// <summary>H-E6: where schedules tie the smaller N wins (then fewer depots); the result is identical across two runs; where a larger N earns more it is chosen.</summary>
        [Test]
        public void Search_TieBreak_SmallerCapWins_AndIsRepeatable()
        {
            // A field of 100: every schedule earns the whole 100 inside 300 s, so all 17 caps and every depot tuple tie.
            EconomyModel small = new EconomyModel(Inputs(new[] { Common(5, 0, 100) }, null, 4));
            ModelBest first = small.Search(300, 8, 40, 2, 4, 40, 2, 3);
            ModelBest second = small.Search(300, 8, 40, 2, 4, 40, 2, 3);
            Assert.That(first.Income, Is.EqualTo(100));
            Assert.That(first.WorkerCap, Is.EqualTo(8), "the smaller N wins a tie");
            Assert.That(first.Triggers, Is.Empty, "fewer depots win a tie");
            Assert.That(second.Income, Is.EqualTo(first.Income));
            Assert.That(second.WorkerCap, Is.EqualTo(first.WorkerCap));
            Assert.That(second.Triggers, Is.EqualTo(first.Triggers));
            // A rich field: more workers earn more, so the smallest cap is not the best, and the same grid run twice agrees.
            EconomyModel rich = new EconomyModel(Inputs(new[] { Common(5, 0, 3000), Common(0, 6, 3000) }, null, 4));
            ModelBest a = rich.Search(300, 4, 14, 2, 4, 14, 2, 1);
            ModelBest b = rich.Search(300, 4, 14, 2, 4, 14, 2, 1);
            Assert.That(a.WorkerCap, Is.GreaterThan(4));
            Assert.That(a.Income, Is.GreaterThan(rich.Income(300, 4, Array.Empty<int>())));
            Assert.That(b.Income, Is.EqualTo(a.Income));
            Assert.That(b.WorkerCap, Is.EqualTo(a.WorkerCap));
            Assert.That(b.Triggers, Is.EqualTo(a.Triggers));
        }

        /// <summary>H-E7: a bucket withdrawal larger than the bank blocks every purchase until income repays it.</summary>
        [Test]
        public void Withdrawal_LargerThanTheBank_BlocksPurchasesUntilRepaid()
        {
            ModelField[] fields = { Common(5, 0, 3000), Common(0, 6, 3000) };
            // With no withdrawal the HQ trains at once: workers have grown by the end of the first bucket.
            List<int> free = new List<int>();
            new EconomyModel(Inputs(fields)).Income(600, 14, Array.Empty<int>(), free);
            Assert.That(free[0], Is.GreaterThan(2));
            // A withdrawal of 1000 at second 0 leaves the bank at 200 - 1000 = -800; two workers earn about 2 a second, so nothing is bought for the first buckets.
            List<int> blocked = new List<int>();
            new EconomyModel(Inputs(fields, new long[] { 1000 })).Income(600, 14, Array.Empty<int>(), blocked);
            Assert.That(blocked[0], Is.EqualTo(2), "the bank is negative after the first bucket: nothing bought");
            Assert.That(blocked[1], Is.EqualTo(2), "still negative after the second bucket");
            Assert.That(blocked[19], Is.GreaterThan(2), "income repays the withdrawal and purchases resume");
        }

        /// <summary>A hand-made game record that ended at tick <paramref name="endTick"/>.</summary>
        private static GameRecord GameEndingAt(int endTick)
        {
            return new GameRecord("prototype_1v1", "water_vs_water", 0, 1UL, "water", "water", "a", "b", GameRecord.Undecided, endTick, 1UL, 1UL, 1UL, new List<HqKill>());
        }

        /// <summary>An econ_gap check configured from <paramref name="entry"/> with 30-second buckets.</summary>
        private static IBehaviourCheck GapCheck(string entry)
        {
            IBehaviourCheck check = BehaviourChecks.Create("econ_gap", new BehaviourContext(10000, 30 * 20, null, 20));
            check.ReadSettings(JsonReader.Parse(entry, "test"));
            return check;
        }

        /// <summary>The econ_gap entry of the shipped file with <paramref name="key"/> replaced by <paramref name="value"/> (text of one JSON member).</summary>
        private static string Entry(string key, string value)
        {
            Dictionary<string, string> members = new Dictionary<string, string>
            {
                ["enabled"] = "true", ["at_seconds"] = "300", ["worker_cap_min"] = "8", ["worker_cap_max"] = "40", ["worker_cap_step"] = "2", ["depot_trigger_min"] = "4", ["depot_trigger_max"] = "40",
                ["depot_trigger_step"] = "2", ["max_depots"] = "3", ["max_gap_permille"] = "1000", ["max_violation_fraction"] = "0", ["print_by_opening"] = "true",
            };
            members[key] = value;
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string> pair in members)
            {
                parts.Add("\"" + pair.Key + "\": " + pair.Value);
            }
            return "{" + string.Join(", ", parts) + "}";
        }

        /// <summary>H-E8: econ_gap is Unjudged for a game that ended before at_seconds (without needing a data set); worker_cap_min above worker_cap_max and an at_seconds that is not a multiple of the bucket are settings errors naming the key.</summary>
        [Test]
        public void EconGap_UnjudgedAndSettingsErrors()
        {
            IBehaviourCheck check = GapCheck(Entry("enabled", "true"));
            GameSummary empty = new GameSummary(0, 0, new SortedDictionary<string, int[]>(), new List<int[]?>(), new List<int[]>(), new List<int[]>(), new SortedDictionary<string, int>(),
                new SortedDictionary<string, int>(), new SortedDictionary<string, int>());
            Assert.That(check.Evaluate(empty, GameEndingAt(100 * 20 - 1)).Outcome, Is.EqualTo(CheckOutcome.Unjudged), "the game ended at 100 s, before 300 s");
            Assert.That(check.Evaluate(empty, GameEndingAt(400 * 20)).Outcome, Is.EqualTo(CheckOutcome.Unjudged), "no income buckets: nothing to judge (an old record)");
            DataValidationException order = Expect.Throws<DataValidationException>(() => GapCheck(Entry("worker_cap_min", "10").Replace("\"worker_cap_max\": 40", "\"worker_cap_max\": 8")));
            Assert.That(order.Message, Does.Contain("worker_cap_min"));
            DataValidationException triggers = Expect.Throws<DataValidationException>(() => GapCheck(Entry("depot_trigger_min", "50").Replace("\"depot_trigger_max\": 40", "\"depot_trigger_max\": 8")));
            Assert.That(triggers.Message, Does.Contain("depot_trigger_min"));
            DataValidationException bucket = Expect.Throws<DataValidationException>(() => GapCheck(Entry("at_seconds", "45")));
            Assert.That(bucket.Message, Does.Contain("at_seconds"));
            DataValidationException depots = Expect.Throws<DataValidationException>(() => GapCheck(Entry("max_depots", "5")));
            Assert.That(depots.Message, Does.Contain("max_depots"));
        }

        /// <summary>The shipped econ_gap entry parses with the real data's settings (the file is read by BehaviourSettings, whose validation builds a throwaway instance of every declared check).</summary>
        [Test]
        public void EconGap_ShippedEntryParses()
        {
            DataSet data = TestData.LoadAll();
            Assert.That(BehaviourChecks.IsRegistered("econ_gap"), Is.True);
            BehaviourSettings settings = BehaviourSettings.Load(TestData.FindDataDirectory());
            List<string> notDeclared = new List<string>();
            List<IBehaviourCheck> checks = settings.Checks(new[] { "econ_gap" }, 30 * data.Tuning.Match.TicksPerSecond, notDeclared, null, data.Tuning.Match.TicksPerSecond, data);
            Assert.That(checks.Count, Is.EqualTo(1));
            Assert.That(checks[0].PrintByOpening, Is.True);
            Assert.That(checks[0].ExtraNames, Is.EqualTo(new[] { "total_gap_sum", "model_error_sum", "gap_judged" }));
        }
    }
}

// rts/tests/Sim.Tests/Harness/PredictTests.cs
// Acceptance tests C-1 to C-9 of rts/docs/specs/balance_measurement.md (package C, first half): the fight predictor scored against measured fights by --predict-fights
// (entries, uphill, classes and margin, scoring, mechanic presence, calibration bins, the committed sweep, determinism, the decision rule), plus the validation of predict.json.
// Part of Rts.Sim.Tests. Uses the shipped data and the committed sweep; C-8 starts worker processes.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Fight predictor evaluation tests.</summary>
    [TestFixture]
    public class PredictTests
    {
        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>The shipped data set. Range: loaded once per call.</summary>
        private static DataSet Data()
        {
            return TestData.LoadAll();
        }

        /// <summary>The predictor numbers of the default profile.</summary>
        private static PredictorSettings Predictor(DataSet data)
        {
            return PredictorSettings.From(AiProfile.FromDataSet(data, "aggressive_s5"));
        }

        /// <summary>A synthetic record: the two armies, a case, a result and a left fraction per seat; the start values are equal and the fight is not mismatched unless asked.</summary>
        private static FightRecord Record(string id, string fightCase, int value, Dictionary<string, int> units0, Dictionary<string, int> units1, string result, string reason = "eliminated",
            bool mismatch = false, double left0 = 0.5, double left1 = 0.0)
        {
            return new FightRecord(id, fightCase, value, "a", "b", new SortedDictionary<string, int>(units0, StringComparer.Ordinal), new SortedDictionary<string, int>(units1, StringComparer.Ordinal),
                800, 800, mismatch, result, reason, 100, "5.0", left0, left1, 1, 0, -1, 1UL, 2UL);
        }

        /// <summary>A scored fight with a given predicted class and margin text.</summary>
        private static PredictedFight Scored(FightRecord record, string cls, string margin)
        {
            return new PredictedFight(record, cls, Fix64.Parse(margin), Fix64.One, Fix64.One);
        }

        /// <summary>Predict settings with other thresholds, by editing the shipped text.</summary>
        private static PredictSettings SettingsWith(string key, string value)
        {
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), PredictSettings.RelativePath));
            return PredictSettings.Parse(FightTestSupport.SetKey(text, key, value));
        }

        /// <summary>C-1: a record with water.grasper x3 gives one entry, count 3 and health 3 x MaxHealth; compositions give entries in ordinal order.</summary>
        [Test]
        public void Entries_CountAndHealth_InOrdinalOrder()
        {
            DataSet data = Data();
            List<FightEntry> one = PredictEvaluator.Entries(new SortedDictionary<string, int> { ["water.grasper"] = 3 }, data);
            Assert.That(one.Count, Is.EqualTo(1));
            Assert.That(one[0].Count, Is.EqualTo(Fix64.FromInt(3)));
            Assert.That(one[0].Health, Is.EqualTo(Fix64.FromInt(3) * data.Content.Units["water.grasper"].MaxHealth));
            FightSettings settings = FightSettings.Load(TestData.FindDataDirectory(), data);
            IReadOnlyDictionary<string, int> recipe = settings.Compositions[0].Units;
            List<FightEntry> mix = PredictEvaluator.Entries(recipe, data);
            Assert.That(mix.Count, Is.EqualTo(recipe.Count));
            for (int i = 1; i < mix.Count; i++)
            {
                Assert.That(string.CompareOrdinal(mix[i - 1].Definition.Id, mix[i].Definition.Id), Is.LessThan(0));
            }
            HarnessException ex = Expect.Throws<HarnessException>(() => PredictEvaluator.Entries(new SortedDictionary<string, int> { ["no.such_unit"] = 1 }, data));
            Assert.That(ex.Message, Does.Contain("no.such_unit"));
        }

        /// <summary>C-2: side A is uphill only in the high_ground case.</summary>
        [Test]
        public void Uphill_OnlyForHighGround()
        {
            Assert.That(PredictEvaluator.AUphill(FightKinds.HighGround), Is.True);
            Assert.That(PredictEvaluator.AUphill(FightKinds.Flat), Is.False);
            Assert.That(PredictEvaluator.AUphill(FightKinds.Flooded), Is.False);
            Assert.That(PredictEvaluator.AUphill(FightKinds.Choke), Is.False);
            // The uphill flag reaches the predictor: a ranged ground unit of seat 0 needs longer on the plateau than on flat ground.
            DataSet data = Data();
            PredictorSettings s = Predictor(data);
            FightRecord r = Record("x", "flat", 750, new Dictionary<string, int> { ["water.ink_lobber"] = 5 }, new Dictionary<string, int> { ["water.grasper"] = 5 }, FightRecord.Seat0Won);
            PredictedFight flat = PredictEvaluator.Predict(r, FightKinds.Flat, data, s);
            PredictedFight high = PredictEvaluator.Predict(r, FightKinds.HighGround, data, s);
            Assert.That(high.TimeSeat0KillsSeat1, Is.GreaterThan(flat.TimeSeat0KillsSeat1));
            Assert.That(high.TimeSeat1KillsSeat0, Is.EqualTo(flat.TimeSeat1KillsSeat0), "seat 1 shoots from the plateau without a penalty");
        }

        /// <summary>C-3: equal times and both at the longest time give none with margin 1; a faster A gives seat0 with margin tB / tA, capped at MaxAdvantage.</summary>
        [Test]
        public void Classify_ClassesAndMargin()
        {
            PredictorSettings s = new PredictorSettings(Fix64.FromInt(600), Fix64.Parse("0.5"), Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.FromInt(10));
            string cls = PredictEvaluator.Classify(new FightPrediction(Fix64.FromInt(20), Fix64.FromInt(20), false, Fix64.One), s, out Fix64 margin);
            Assert.That(cls, Is.EqualTo("none"));
            Assert.That(margin, Is.EqualTo(Fix64.One));
            cls = PredictEvaluator.Classify(new FightPrediction(Fix64.FromInt(600), Fix64.FromInt(600), false, Fix64.One), s, out margin);
            Assert.That(cls, Is.EqualTo("none"));
            Assert.That(margin, Is.EqualTo(Fix64.One));
            cls = PredictEvaluator.Classify(new FightPrediction(Fix64.FromInt(10), Fix64.FromInt(25), true, Fix64.One), s, out margin);
            Assert.That(cls, Is.EqualTo("seat0"));
            Assert.That(margin, Is.EqualTo(Fix64.FromInt(25) / Fix64.FromInt(10)));
            cls = PredictEvaluator.Classify(new FightPrediction(Fix64.FromInt(25), Fix64.FromInt(10), false, Fix64.One), s, out margin);
            Assert.That(cls, Is.EqualTo("seat1"));
            Assert.That(margin, Is.EqualTo(Fix64.FromInt(25) / Fix64.FromInt(10)));
            cls = PredictEvaluator.Classify(new FightPrediction(Fix64.Parse("0.5"), Fix64.FromInt(500), true, Fix64.One), s, out margin);
            Assert.That(cls, Is.EqualTo("seat0"));
            Assert.That(margin, Is.EqualTo(Fix64.FromInt(10)), "capped at MaxAdvantage");
        }

        /// <summary>C-4: mismatched fights are excluded; a predicted none on a decided fight is wrong; a stalemate predicted none agrees and is not in the headline.</summary>
        [Test]
        public void Scoring_MismatchDecidedAndNoWinner()
        {
            Dictionary<string, int> u = new Dictionary<string, int> { ["water.grasper"] = 1 };
            List<PredictedFight> fights = new List<PredictedFight>
            {
                Scored(Record("flat/750/a/b", "flat", 750, u, u, FightRecord.Seat0Won), "seat0", "2"),
                Scored(Record("flat/750/c/d", "flat", 750, u, u, FightRecord.Seat1Won), "seat0", "2"),
                Scored(Record("flat/750/e/f", "flat", 750, u, u, FightRecord.Seat0Won), "none", "1"),
                Scored(Record("flat/750/g/h", "flat", 750, u, u, FightRecord.Stalemate, "no_damage"), "none", "1"),
                Scored(Record("flat/750/i/j", "flat", 750, u, u, FightRecord.Stalemate, "no_damage"), "seat0", "1.5"),
                Scored(Record("flat/750/k/l", "flat", 750, u, u, FightRecord.Seat0Won, mismatch: true), "seat0", "2"),
            };
            DataSet data = Data();
            PredictEvaluation e = PredictEvaluator.Evaluate(fights, FightSettings.Load(TestData.FindDataDirectory(), data), data, PredictSettings.Load(TestData.FindDataDirectory()));
            Assert.That(e.Mismatched, Is.EqualTo(1));
            Assert.That(e.Overall.Decided, Is.EqualTo(3));
            Assert.That(e.Overall.Correct, Is.EqualTo(1), "only the first decided fight is right; none on a decided fight is wrong");
            Assert.That(e.NoWinner, Is.EqualTo(2));
            Assert.That(e.NoWinnerAgree, Is.EqualTo(1));
            Assert.That(e.Overall.Fights, Is.EqualTo(5), "judged = decided + no winner");
            Assert.That(PredictRunner.Line(e, PredictEvaluator.Decide(PredictSettings.Load(TestData.FindDataDirectory()), e.Overall, e.Clean, e.Mechanics, e.Bins)),
                Does.StartWith("PREDICT fights=6 judged=5 decided=3 correct=1 accuracy=0.333 "));
        }

        /// <summary>C-5: the mechanic table rows from definitions and the case; windstorm_range and focus_fire are never present; "only" counts.</summary>
        [Test]
        public void MechanicPresence_EachRow()
        {
            DataSet data = Data();
            Dictionary<string, int> spoutling = new Dictionary<string, int> { ["water.spoutling"] = 1 };
            Dictionary<string, int> roc = new Dictionary<string, int> { ["skai.roc_rider"] = 1 };
            Dictionary<string, int> grasper = new Dictionary<string, int> { ["water.grasper"] = 1 };
            Dictionary<string, int> lobber = new Dictionary<string, int> { ["water.ink_lobber"] = 1 };
            Dictionary<string, int> shellback = new Dictionary<string, int> { ["water.shellback"] = 1 };
            Dictionary<string, int> windrunner = new Dictionary<string, int> { ["skai.windrunner"] = 1 };
            // Drench needs an air unit on the other side.
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, spoutling, roc, "seat0"), FightKinds.Flat, data, 0), Does.Contain("drench"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, roc, spoutling, "seat0"), FightKinds.Flat, data, 0), Does.Contain("drench"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, spoutling, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Not.Contain("drench"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, spoutling, spoutling, "seat0"), FightKinds.Flat, data, 0), Does.Not.Contain("drench"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, lobber, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Contain("splash"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, grasper, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Not.Contain("splash"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, windrunner, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Contain("momentum"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, grasper, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Not.Contain("momentum"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flooded", 1, grasper, grasper, "seat0"), FightKinds.Flooded, data, 0), Does.Contain("flooding"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, grasper, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Not.Contain("flooding"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, shellback, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Contain("regeneration"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, lobber, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Contain("hit_delay"));
            Assert.That(PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, grasper, grasper, "seat0"), FightKinds.Flat, data, 0), Does.Not.Contain("hit_delay"), "the Grasper's weapon has no hit delay");
            foreach (string kind in new[] { FightKinds.Flat, FightKinds.HighGround, FightKinds.Flooded, FightKinds.Choke })
            {
                List<string> all = PredictEvaluator.MechanicsPresent(Record("f", "flat", 1, spoutling, roc, "seat0"), kind, data, 0);
                Assert.That(all, Does.Not.Contain("windstorm_range"));
                Assert.That(all, Does.Not.Contain("focus_fire"));
            }
            // "Only": grasper against grasper has nothing present (clean); the flooded one has flooding alone.
            FightSettings fightSettings = FightSettings.Load(TestData.FindDataDirectory(), data);
            List<PredictedFight> fights = new List<PredictedFight>
            {
                Scored(Record("flat/1500/a/b", "flat", 1500, grasper, grasper, "seat0"), "seat0", "2"),
                Scored(Record("flooded/1500/a/b", "flooded", 1500, grasper, grasper, "seat0"), "seat1", "2"),
                Scored(Record("flat/1500/c/d", "flat", 1500, lobber, grasper, "seat0"), "seat0", "2"),
            };
            PredictEvaluation e = PredictEvaluator.Evaluate(fights, fightSettings, data, PredictSettings.Load(TestData.FindDataDirectory()));
            Assert.That(e.Clean.Decided, Is.EqualTo(1));
            Assert.That(e.Clean.Correct, Is.EqualTo(1));
            MechanicRow flooding = e.Mechanics.Find(row => row.Id == "flooding")!;
            Assert.That(flooding.Present.Decided, Is.EqualTo(1));
            Assert.That(flooding.Only.Decided, Is.EqualTo(1));
            Assert.That(flooding.Only.Correct, Is.EqualTo(0));
            MechanicRow splash = e.Mechanics.Find(row => row.Id == "splash")!;
            Assert.That(splash.Only.Decided, Is.EqualTo(1), "with hit_delay_ignore_ticks 2 (fight_predictor_v2.md 5.4) the lobber's 2-tick hit delay no longer counts, so splash is alone");
            Assert.That(splash.Absent.Decided, Is.EqualTo(2));
        }

        /// <summary>C-6: a margin equal to an edge goes to the upper bin; the comparison is Fix64 (1.1 is not a double).</summary>
        [Test]
        public void BinIndex_EdgeGoesToUpperBin()
        {
            List<Fix64> edges = new List<Fix64> { Fix64.One, Fix64.Parse("1.1"), Fix64.Parse("1.25"), Fix64.Parse("1.5") };
            Assert.That(PredictEvaluator.BinIndex(edges, Fix64.One), Is.EqualTo(0));
            Assert.That(PredictEvaluator.BinIndex(edges, Fix64.Parse("1.0999")), Is.EqualTo(0));
            Assert.That(PredictEvaluator.BinIndex(edges, Fix64.Parse("1.1")), Is.EqualTo(1));
            Assert.That(PredictEvaluator.BinIndex(edges, Fix64.Parse("1.25")), Is.EqualTo(2));
            Assert.That(PredictEvaluator.BinIndex(edges, Fix64.Parse("1.4999")), Is.EqualTo(2));
            Assert.That(PredictEvaluator.BinIndex(edges, Fix64.Parse("1.5")), Is.EqualTo(3));
            Assert.That(PredictEvaluator.BinIndex(edges, Fix64.FromInt(10000)), Is.EqualTo(3), "the last bin is open");
        }

        /// <summary>C-7: the committed sweep gives 780 fights, 765 decided, 15 without a winner, 0 mismatched, and prints a decision (exit 0).</summary>
        [Test]
        public void CommittedSweep_Evaluates()
        {
            string sweep = CommittedSweep();
            string outDirectory = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            int exit = HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--predict-fights", "--from", sweep, "--out", outDirectory }, out string stdout, out string error);
            Assert.That(exit, Is.EqualTo(0), error);
            string last = stdout.TrimEnd('\n').Substring(stdout.TrimEnd('\n').LastIndexOf('\n') + 1);
            Assert.That(last, Does.StartWith("PREDICT fights=780 judged=780 decided=765 correct="));
            Assert.That(last, Does.Contain(" no_winner_agree="));
            Assert.That(last, Does.Match("decision=(FIX_FORMULA|SIMULATE|DEVELOPER_CHOICE) \\(proposed thresholds\\)$"));
            Assert.That(File.ReadAllText(Path.Combine(outDirectory, "predict.md")), Does.Contain("Mismatched fights (start values too far apart, 0 here)"));
            Assert.That(File.ReadAllLines(Path.Combine(outDirectory, "predict.jsonl")).Length, Is.EqualTo(780));
            // An unknown unit is an error naming the record.
            string bad = Path.Combine(HarnessTestSupport.NewFolder(), "bad.jsonl");
            File.WriteAllText(bad, File.ReadAllLines(sweep)[0].Replace("skai.roc_rider", "skai.no_such") + "\n");
            int badExit = HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--predict-fights", "--from", bad, "--out", outDirectory }, out string badOut, out string badError);
            Assert.That(badExit, Is.EqualTo(1));
            Assert.That(badError, Does.Contain("skai.no_such").And.Contain("record 1"));
        }

        /// <summary>C-8: --from twice gives identical predict.md and predict.jsonl; sweep mode with --jobs 1 and --jobs 4 gives identical files.</summary>
        [Test]
        public void Output_IsDeterministic()
        {
            string sweep = CommittedSweep();
            string[] texts = new string[2];
            for (int i = 0; i < 2; i++)
            {
                string outDirectory = Path.Combine(HarnessTestSupport.NewFolder(), "out");
                Assert.That(HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--predict-fights", "--from", sweep, "--out", outDirectory }, out string stdout, out string error), Is.EqualTo(0), error);
                texts[i] = stdout + File.ReadAllText(Path.Combine(outDirectory, "predict.md")) + File.ReadAllText(Path.Combine(outDirectory, "predict.jsonl"));
            }
            Assert.That(texts[1], Is.EqualTo(texts[0]));
            string folder = FightTestSupport.Quick("chunk_fights", "4");
            string[] sweeps = new string[2];
            int[] jobs = { 1, 4 };
            for (int i = 0; i < 2; i++)
            {
                string outDirectory = Path.Combine(HarnessTestSupport.NewFolder(), "out");
                Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--predict-fights", "--jobs", jobs[i].ToString(), "--out", outDirectory }, out string stdout, out string error), Is.EqualTo(0), error);
                Assert.That(stdout, Does.Contain("PREDICT fights=").And.Contain("fights 6/6"));
                sweeps[i] = File.ReadAllText(Path.Combine(outDirectory, "predict.md")) + File.ReadAllText(Path.Combine(outDirectory, "predict.jsonl")) + File.ReadAllText(Path.Combine(outDirectory, "fights.jsonl"))
                    + File.ReadAllText(Path.Combine(outDirectory, "fights.md"));
            }
            Assert.That(sweeps[1], Is.EqualTo(sweeps[0]));
        }

        /// <summary>Synthetic tallies: n decided fights of which c are correct.</summary>
        private static PredictTally Tally(int correct, int decided)
        {
            PredictTally tally = new PredictTally();
            Dictionary<string, int> u = new Dictionary<string, int> { ["water.grasper"] = 1 };
            for (int i = 0; i < decided; i++)
            {
                tally.Add(Scored(Record("f" + i, "flat", 750, u, u, FightRecord.Seat0Won), i < correct ? "seat0" : "seat1", "2"));
            }
            return tally;
        }

        /// <summary>A mechanic row with a present tally.</summary>
        private static MechanicRow Mechanic(string id, int correct, int decided)
        {
            MechanicRow row = new MechanicRow(id);
            Dictionary<string, int> u = new Dictionary<string, int> { ["water.grasper"] = 1 };
            for (int i = 0; i < decided; i++)
            {
                row.Present.Add(Scored(Record("m" + i, "flat", 750, u, u, FightRecord.Seat0Won), i < correct ? "seat0" : "seat1", "2"));
            }
            return row;
        }

        /// <summary>Two calibration bins with the given decided and win counts.</summary>
        private static List<CalibrationBin> Bins(int decided0, int wins0, int decided1, int wins1)
        {
            CalibrationBin a = new CalibrationBin(Fix64.One, Fix64.Parse("2"));
            CalibrationBin b = new CalibrationBin(Fix64.Parse("2"), null);
            a.Decided = decided0;
            a.Wins = wins0;
            b.Decided = decided1;
            b.Wins = wins1;
            return new List<CalibrationBin> { a, b };
        }

        /// <summary>C-9: accuracies at each threshold edge give the three decisions (an edge value does not fire "below").</summary>
        [Test]
        public void Decide_ThresholdEdges()
        {
            PredictSettings s = PredictSettings.Load(TestData.FindDataDirectory());
            List<CalibrationBin> fine = Bins(20, 10, 20, 12);
            List<MechanicRow> noMechanics = new List<MechanicRow>();
            // Headline 75 of 100 is exactly the threshold 0.75: not below. Clean 85 of 100 is exactly 0.85: not below.
            Assert.That(PredictEvaluator.Decide(s, Tally(75, 100), Tally(85, 100), noMechanics, fine).Verdict, Is.EqualTo(PredictDecision.FixFormula));
            // One fight less: headline 74 of 100 fires rule 1.
            Assert.That(PredictEvaluator.Decide(s, Tally(74, 100), Tally(85, 100), noMechanics, fine).Verdict, Is.EqualTo(PredictDecision.Simulate));
            Assert.That(PredictEvaluator.Decide(s, Tally(80, 100), Tally(84, 100), noMechanics, fine).Verdict, Is.EqualTo(PredictDecision.Simulate), "clean 0.84 is below 0.85");
            Assert.That(PredictEvaluator.Decide(s, Tally(80, 100), Tally(0, 0), noMechanics, fine).Verdict, Is.EqualTo(PredictDecision.FixFormula), "an empty clean row cannot fire");
            // Calibration: bin 1 at 0.50 against bin 0 at 0.60 is a drop of exactly 0.10: not an inversion; at 0.45 it is.
            Assert.That(PredictEvaluator.Decide(s, Tally(80, 100), Tally(85, 100), noMechanics, Bins(20, 12, 20, 10)).Verdict, Is.EqualTo(PredictDecision.FixFormula));
            Assert.That(PredictEvaluator.Decide(s, Tally(80, 100), Tally(85, 100), noMechanics, Bins(20, 12, 20, 9)).Verdict, Is.EqualTo(PredictDecision.Simulate));
            Assert.That(PredictEvaluator.Decide(s, Tally(80, 100), Tally(85, 100), noMechanics, Bins(20, 12, 5, 0)).Verdict, Is.EqualTo(PredictDecision.FixFormula), "a bin below min_fights_per_row is ignored");
            // Mechanics: 14 of 20 is exactly 0.70 (not below the floor); the 0.85 list names it. 13 of 20 is below the floor.
            PredictDecision fix = PredictEvaluator.Decide(s, Tally(80, 100), Tally(85, 100), new List<MechanicRow> { Mechanic("splash", 14, 20), Mechanic("drench", 19, 20) }, fine);
            Assert.That(fix.Verdict, Is.EqualTo(PredictDecision.FixFormula));
            Assert.That(fix.Listed, Is.EqualTo(new[] { "splash" }));
            PredictDecision choice = PredictEvaluator.Decide(s, Tally(80, 100), Tally(85, 100), new List<MechanicRow> { Mechanic("splash", 13, 20), Mechanic("drench", 19, 20) }, fine);
            Assert.That(choice.Verdict, Is.EqualTo(PredictDecision.DeveloperChoice));
            Assert.That(choice.Listed, Is.EqualTo(new[] { "splash" }));
            Assert.That(PredictEvaluator.Decide(s, Tally(80, 100), Tally(85, 100), new List<MechanicRow> { Mechanic("splash", 1, 19) }, fine).Verdict, Is.EqualTo(PredictDecision.FixFormula),
                "a mechanic with fewer than mechanic_min_fights decided fights is not judged");
        }

        /// <summary>The ordering of table rows: accuracy ascending by cross-multiplied integers, empty rows last, ties by key.</summary>
        [Test]
        public void SortByAccuracy_UsesIntegers()
        {
            SortedDictionary<string, PredictTally> rows = new SortedDictionary<string, PredictTally>(StringComparer.Ordinal)
            {
                ["b"] = Tally(1, 3), ["a"] = Tally(2, 6), ["c"] = Tally(0, 0), ["d"] = Tally(1, 2),
            };
            List<KeyValuePair<string, PredictTally>> sorted = PredictEvaluator.SortByAccuracy(rows);
            Assert.That(sorted.ConvertAll(p => p.Key), Is.EqualTo(new[] { "a", "b", "d", "c" }));
        }

        /// <summary>predict.json: the shipped file parses; an unknown key, a bad first edge, descending edges and five decimals each fail naming the file and the field.</summary>
        [Test]
        public void Settings_AreValidated()
        {
            PredictSettings shipped = PredictSettings.Load(TestData.FindDataDirectory());
            Assert.That(shipped.SimulateBelowAccuracyPt, Is.EqualTo(7500));
            Assert.That(shipped.MarginBinEdges.Count, Is.EqualTo(6));
            Assert.That(Expect.Throws<DataValidationException>(() => SettingsWith("margin_bin_edges", "[\"1.1\", \"2\"]")).Message, Does.Contain("predict.json").And.Contain("first edge"));
            Assert.That(Expect.Throws<DataValidationException>(() => SettingsWith("margin_bin_edges", "[\"1\", \"2\", \"1.5\"]")).Message, Does.Contain("above the edge before"));
            Assert.That(Expect.Throws<DataValidationException>(() => SettingsWith("simulate_below_accuracy", "0.12345")).Message, Does.Contain("at most 4 decimals"));
            Assert.That(Expect.Throws<DataValidationException>(() => SettingsWith("min_fights_per_row", "0")).Message, Does.Contain("min_fights_per_row"));
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), PredictSettings.RelativePath));
            Assert.That(Expect.Throws<DataValidationException>(() => PredictSettings.Parse(text.Replace("\"fix_min_accuracy\"", "\"fix_min_acuracy\""))).Message, Does.Contain("fix_min_acuracy"));
        }

        /// <summary>The path of the committed sweep of the 18 Nov 2026 report (found from the data folder).</summary>
        private static string CommittedSweep()
        {
            string path = Path.GetFullPath(Path.Combine(TestData.FindDataDirectory(), "..", "docs", "reports", "fights_2026-11-18", "fights.jsonl"));
            Assert.That(File.Exists(path), Is.True, path);
            return path;
        }
    }
}

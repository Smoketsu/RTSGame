// rts/tests/Sim.Tests/Harness/HeldOutTests.cs
// Acceptance tests FP2-1-1 to FP2-1-10 of rts/docs/specs/fight_predictor_v2.md section 5.6 (package FP2-1, held-out fight set support): --fight-settings, seat1_value_ratios, the extended
// records (value ratio, lane depth class, snapshot), the subsets E, U and D with their contexts, the bars B1 to B7, the hit_delay correction and the determinism of the new output.
// Also checks that heldout.json parses and plans without playing any of its fights (that happens only in package FP2-3). Part of Rts.Sim.Tests. Some tests start worker processes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Held-out fight set support tests.</summary>
    [TestFixture]
    public class HeldOutTests
    {
        /// <summary>A single unit (a Skai Windrunner) for synthetic records.</summary>
        private const string W = "skai.windrunner";

        /// <summary>A second single unit (a Water Grasper) for synthetic records.</summary>
        private const string G = "water.grasper";

        /// <summary>The committed sweep of 18 Nov 2026, relative to the data folder parent (rts).</summary>
        private const string CommittedSweep = "docs/reports/fights_2026-11-18/fights.jsonl";

        /// <summary>Deletes the temporary data folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>Path of a file under rts/, next to the data folder.</summary>
        private static string UnderRts(string relative)
        {
            return Path.Combine(Path.GetDirectoryName(TestData.FindDataDirectory().TrimEnd('/', '\\'))!, relative);
        }

        /// <summary>The record line with its state hash removed: the hash includes the tuning digest, which changed after the committed sweep was written (reports/predict_fights README).</summary>
        private static string WithoutHash(string line)
        {
            return Regex.Replace(line, ",\"state_hash\":\"[0-9a-f]{16}\"", string.Empty);
        }

        /// <summary>Adds the two extended keys to the fights.json of a data folder (neither is in the shipped file).</summary>
        private static void AddExtended(string folder, string extended, string loss)
        {
            string path = Path.Combine(folder, FightTestSupport.SettingsPath);
            string text = File.ReadAllText(path).TrimEnd();
            Assert.That(text, Does.EndWith("}"));
            text = text.Substring(0, text.Length - 1).TrimEnd() + ",\n  \"extended_records\": " + extended + ",\n  \"snapshot_loss_fraction\": " + loss + "\n}\n";
            File.WriteAllText(path, text);
        }

        /// <summary>The quick sweep with extended records and a case list.</summary>
        private static string ExtendedQuick(string cases, string loss = "0.25", params string[] more)
        {
            string folder = FightTestSupport.Quick(more);
            FightTestSupport.Edit(folder, "cases", cases);
            AddExtended(folder, "true", loss);
            return folder;
        }

        /// <summary>FP2-1-1: without --fight-settings the plan of fights.json is the 780 committed fights in the committed order, and --fights --only gives the committed line (state hash aside).</summary>
        [Test]
        public void DefaultUnchanged_PlanAndOnlyMatchCommittedSweep()
        {
            DataSet data = TestData.LoadAll();
            FightSettings settings = FightSettings.Load(TestData.FindDataDirectory(), data);
            FightPlan plan = FightPlan.Build(data, settings);
            string[] committed = File.ReadAllLines(UnderRts(CommittedSweep));
            Assert.That(plan.Fights.Count, Is.EqualTo(780));
            Assert.That(committed.Length, Is.EqualTo(780));
            for (int i = 0; i < committed.Length; i++)
            {
                Assert.That(FightRecord.Parse(committed[i]).Fight, Is.EqualTo(plan.Fights[i].Id), "fight " + i);
            }
            Assert.That(settings.ExtendedRecords, Is.False);
            // One played fight against the committed line.
            const int Index = 99;
            int exit = HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--fights", "--only", plan.Fights[Index].Id }, out string output, out string error);
            Assert.That(exit, Is.EqualTo(0), error);
            Assert.That(WithoutHash(output.TrimEnd('\n')), Is.EqualTo(WithoutHash(committed[Index])));
        }

        /// <summary>FP2-1-2: a case with ["1", "1.5"] plans twice the fights; ratio-1 IDs are unchanged, the others end in /r15000; seat 1's value is within the multiplier rounding of 1.5 x seat 0's; mismatch is judged against the ratio.</summary>
        [Test]
        public void Ratios_PlanIdsValuesAndMismatch()
        {
            string folder = FightTestSupport.Quick("cases", "[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500], \"seat1_value_ratios\": [\"1\", \"1.5\"] } ]");
            DataSet data = HarnessTestSupport.Load(folder);
            FightSettings settings = FightSettings.Load(folder, data);
            FightPlan plan = FightPlan.Build(data, settings);
            Assert.That(plan.Fights.Count, Is.EqualTo(2 * FightTestSupport.QuickIds.Length));
            // Plan order: ratio 1 first (the quick ids), then ratio 1.5 with the suffix.
            for (int i = 0; i < FightTestSupport.QuickIds.Length; i++)
            {
                Assert.That(plan.Fights[i].Id, Is.EqualTo(FightTestSupport.QuickIds[i]));
                Assert.That(plan.Fights[FightTestSupport.QuickIds.Length + i].Id, Is.EqualTo(FightTestSupport.QuickIds[i] + "/r15000"));
            }
            foreach (FightSpec fight in plan.Fights)
            {
                if (fight.RatioPt == FightSpec.EqualRatioPt)
                {
                    continue;
                }
                // Seat 1's value against 1.5 x 1500 = 2250: off by at most half a recipe (the multiplier rounds half up).
                Assert.That(Math.Abs(fight.Value1 * 2 - 2 * 2250), Is.LessThanOrEqualTo(fight.Seat1.RecipeValue), fight.Id);
                // Mismatch against the target ratio, computed again in doubles.
                double wanted = 1.5 * fight.Value0;
                Assert.That(fight.Mismatch, Is.EqualTo(Math.Abs(fight.Value1 - wanted) > 0.15 * Math.Max(fight.Value1, wanted)), fight.Id);
            }
            // A ratio that the equal rule would call a mismatch is not one against its target, and the reverse.
            FightSpec uneven = plan.Fights[FightTestSupport.QuickIds.Length];
            Assert.That(Math.Abs(uneven.Value1 - uneven.Value0), Is.GreaterThan(0.15 * Math.Max(uneven.Value0, uneven.Value1)), "the sides differ by about 50 percent");
            Assert.That(uneven.Mismatch, Is.False, "but the target ratio is 1.5, so it is no mismatch");
            // Validation names the file and key.
            string bad = FightTestSupport.Quick("cases", "[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500], \"seat1_value_ratios\": [\"0.1\"] } ]");
            DataValidationException ex = Expect.Throws<DataValidationException>(() => FightSettings.Load(bad, HarnessTestSupport.Load(bad)));
            Assert.That(ex.Message, Does.Contain("fights.json").And.Contain("seat1_value_ratios"));
            string five = FightTestSupport.Quick("cases", "[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500], \"seat1_value_ratios\": [\"1\", \"1.1\", \"1.2\", \"1.3\", \"1.4\"] } ]");
            Assert.That(Expect.Throws<DataValidationException>(() => FightSettings.Load(five, HarnessTestSupport.Load(five))).Message, Does.Contain("seat1_value_ratios"));
        }

        /// <summary>FP2-1-3: records are the original format with extended_records false or absent; with it true the line is the same line with the extended keys appended after the last key.</summary>
        [Test]
        public void ExtendedRecordsOff_ByteIdentical_AndOnAppends()
        {
            string plain = FightTestSupport.Quick();
            string off = FightTestSupport.Quick();
            AddExtended(off, "false", "0.25");
            string on = ExtendedQuick("[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500] } ]");
            string plainLine = FightTestSupport.Play(plain, FightTestSupport.QuickFirst).Record.ToJsonLine();
            string offLine = FightTestSupport.Play(off, FightTestSupport.QuickFirst).Record.ToJsonLine();
            string onLine = FightTestSupport.Play(on, FightTestSupport.QuickFirst).Record.ToJsonLine();
            Assert.That(plainLine, Does.Not.Contain("value_ratio_pt"));
            Assert.That(offLine, Is.EqualTo(plainLine));
            Assert.That(onLine, Does.StartWith(plainLine.Substring(0, plainLine.Length - 1) + ",\"value_ratio_pt\":10000,\"lane_depth_class\":-1,\"snap_tick\":"));
            Assert.That(FightRecord.Parse(plainLine).Extension, Is.Null);
        }

        /// <summary>The record of a quick fight played with a loss fraction and optionally a cap in seconds.</summary>
        private static FightRecord Quick(string loss, string fight, string cap = "180")
        {
            string folder = ExtendedQuick("[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500] } ]", loss, "max_fight_seconds", cap);
            return FightTestSupport.Play(folder, fight).Record;
        }

        /// <summary>Value left of one seat's snapshot over its start value, in thousandths (rounded half up like the record's left fractions); the snapshot health is in thousandths of a health point.</summary>
        private static double SnapshotLeft(IReadOnlyDictionary<string, SnapGroup> snap, int startValue, DataSet data)
        {
            double sum = 0;
            foreach (KeyValuePair<string, SnapGroup> pair in snap)
            {
                Rts.Sim.Units.UnitDefinition unit = data.Content.Units[pair.Key];
                sum += FightSettings.ValueOf(unit) * (pair.Value.HealthMilli / 1000.0) / (double)unit.MaxHealth.ToFloatForPresentation();
            }
            return sum / startValue;
        }

        /// <summary>FP2-1-4: the snapshot is taken in the first step meeting the loss rule, its counts and health equal the living units' at that tick, and a fight ending first has snap_tick -1 and empty maps.</summary>
        [Test]
        public void Snapshot_FirstStepMeetingLossRule_CountsMatchLivingUnits()
        {
            DataSet data = TestData.LoadAll();
            const string Fight = "flat/1500/water.grasper/water.ink_lobber";
            FightRecord full = Quick("0.25", Fight);
            FightExtension x = full.Extension!;
            Assert.That(x.SnapTick, Is.GreaterThan(0), "a 1500-value fight loses a quarter of a side before it ends");
            Assert.That(x.SnapTick, Is.LessThan(full.EndTick));
            // The same fight stopped by the cap at the snapshot tick and at the tick before it: 20 ticks per second.
            string capAt = ((decimal)x.SnapTick / 20m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string capBefore = ((decimal)(x.SnapTick - 1) / 20m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            FightRecord at = Quick("0.25", Fight, capAt);
            Assert.That(at.EndTick, Is.EqualTo(x.SnapTick));
            int count0 = 0;
            int count1 = 0;
            foreach (SnapGroup g in x.Snap0.Values) { count0 += g.Count; }
            foreach (SnapGroup g in x.Snap1.Values) { count1 += g.Count; }
            Assert.That(at.Alive0, Is.EqualTo(count0));
            Assert.That(at.Alive1, Is.EqualTo(count1));
            Assert.That(SnapshotLeft(x.Snap0, at.Value0, data), Is.EqualTo(at.Left0).Within(0.002));
            Assert.That(SnapshotLeft(x.Snap1, at.Value1, data), Is.EqualTo(at.Left1).Within(0.002));
            Assert.That(Math.Min(at.Left0, at.Left1), Is.LessThanOrEqualTo(0.750), "the rule is met at the snapshot tick");
            FightRecord before = Quick("0.25", Fight, capBefore);
            Assert.That(before.EndTick, Is.EqualTo(x.SnapTick - 1));
            Assert.That(Math.Min(before.Left0, before.Left1), Is.GreaterThanOrEqualTo(0.750), "and not yet one tick earlier");
            // A fight that ends before the rule is met (loss 0.95 of a side while the other is wiped out in the same step): no snapshot.
            FightRecord none = Quick("0.95", Fight);
            if (none.Extension!.SnapTick < 0)
            {
                Assert.That(none.Extension.Snap0, Is.Empty);
                Assert.That(none.Extension.Snap1, Is.Empty);
            }
            // A fight capped after one second has lost nothing like 95 percent: no snapshot, empty maps.
            FightRecord capped = Quick("0.95", Fight, "1");
            Assert.That(capped.Extension!.SnapTick, Is.EqualTo(-1));
            Assert.That(capped.Extension.Snap0, Is.Empty);
            Assert.That(capped.Extension.Snap1, Is.Empty);
        }

        /// <summary>FP2-1-5: in a flooded case the lane depth class is the majority class of the lane tiles at army spawn (computed again here from a simulation); other cases give -1.</summary>
        [Test]
        public void LaneDepth_FloodedMajorityClass_OtherCasesMinusOne()
        {
            string folder = ExtendedQuick("[ { \"id\": \"flooded\", \"kind\": \"flooded\", \"army_values\": [1500] }, { \"id\": \"choke\", \"kind\": \"choke\", \"army_values\": [1500] }, { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500] } ]");
            FightRecord flooded = FightTestSupport.Play(folder, "flooded/1500/skai.windrunner/water.grasper").Record;
            FightRecord choke = FightTestSupport.Play(folder, "choke/1500/skai.windrunner/water.grasper").Record;
            Assert.That(choke.Extension!.LaneDepthClass, Is.EqualTo(-1));
            // The flooded arena stepped to the army spawn tick, then its lane tiles counted by depth class.
            DataSet data = HarnessTestSupport.Load(folder);
            FightSettings settings = FightSettings.Load(folder, data);
            FightSpec spec = FightPlan.Build(data, settings).Fights[0];
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), spec.Seat0.Faction, "seat 0"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), spec.Seat1.Faction, "seat 1"),
                new PlayerSetup(new PlayerId(2), new TeamId(1), data.Content.Units[settings.FloodSourceUnit].FactionId, "flood"),
            };
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(1), data.Content, players, BuiltInMechanics.CreateRegistry(), data.Tuning,
                FightPlan.BuildArena(settings, spec.Case), null));
            sim.Start();
            uint sequence = 0;
            foreach (KeyValuePair<int, int> offset in settings.FloodSourceOffsets)
            {
                sim.Submit(new SpawnUnitCommand(0, PlayerId.None, sequence++, settings.FloodSourceUnit, new PlayerId(2),
                    new FixVec2(Fix64.FromInt(FightPlan.CentreX(settings) + offset.Key) + Fix64.Half, Fix64.FromInt(FightPlan.CentreY(settings) + offset.Value) + Fix64.Half)));
            }
            int spawnTick = sim.Clock.SecondsToTicks(settings.FloodWarmupSeconds);
            for (int t = 0; t < spawnTick; t++)
            {
                sim.Step();
            }
            int[] perClass = new int[3];
            for (int y = 0; y < settings.ArenaHeightTiles; y++)
            {
                for (int x = 0; x < settings.ArenaWidthTiles; x++)
                {
                    if (FightPlan.IsLaneTile(settings, x, y))
                    {
                        perClass[sim.Terrain.DepthClass(sim.Terrain.IndexOf(x, y))]++;
                    }
                }
            }
            int expected = 0;
            for (int c = 1; c < 3; c++)
            {
                expected = perClass[c] > perClass[expected] ? c : expected;
            }
            Assert.That(flooded.Extension!.LaneDepthClass, Is.EqualTo(expected));
            Assert.That(flooded.Extension.LaneDepthClass, Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>FP2-1-6: a record round-trips through its JSON line with and without the extended keys, and a line with only some of them reads the rest as defaults.</summary>
        [Test]
        public void Parse_RoundTrip_WithAndWithoutEveryOptionalKey()
        {
            string folder = ExtendedQuick("[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500] } ]");
            string line = FightTestSupport.Play(folder, FightTestSupport.QuickFirst).Record.ToJsonLine();
            Assert.That(FightRecord.Parse(line).ToJsonLine(), Is.EqualTo(line));
            string plain = FightTestSupport.Play(FightTestSupport.Quick(), FightTestSupport.QuickFirst).Record.ToJsonLine();
            Assert.That(FightRecord.Parse(plain).ToJsonLine(), Is.EqualTo(plain));
            // Each optional key alone.
            string baseLine = plain.Substring(0, plain.Length - 1);
            FightExtension ratio = FightRecord.Parse(baseLine + ",\"value_ratio_pt\":12500}").Extension!;
            Assert.That(ratio.ValueRatioPt, Is.EqualTo(12500));
            Assert.That(ratio.LaneDepthClass, Is.EqualTo(-1));
            Assert.That(ratio.SnapTick, Is.EqualTo(-1));
            Assert.That(FightRecord.Parse(baseLine + ",\"lane_depth_class\":2}").Extension!.LaneDepthClass, Is.EqualTo(2));
            FightExtension snap = FightRecord.Parse(baseLine + ",\"snap_tick\":40,\"snap0\":{\"skai.windrunner\":[3,1500250]},\"snap1\":{}}").Extension!;
            Assert.That(snap.SnapTick, Is.EqualTo(40));
            Assert.That(snap.Snap0["skai.windrunner"].Count, Is.EqualTo(3));
            Assert.That(snap.Snap0["skai.windrunner"].HealthMilli, Is.EqualTo(1500250));
            Assert.That(snap.ValueRatioPt, Is.EqualTo(10000));
            Assert.That(Expect.Throws<HarnessException>(() => FightRecord.Parse(baseLine + ",\"snap0\":{\"skai.windrunner\":[3]}}")).Message, Does.Contain("malformed"));
        }

        /// <summary>A synthetic extended record of two single units (or other IDs) with a given winner; ratio, snapshot and depth class as given.</summary>
        private static FightRecord Rec(string id, string a, string b, string result, int ratioPt = 10000, int depth = -1, string fightCase = "flat", bool snapshot = false, bool mismatch = false, bool names = false)
        {
            SortedDictionary<string, int> u0 = new SortedDictionary<string, int>(StringComparer.Ordinal) { [a] = 2 };
            SortedDictionary<string, int> u1 = new SortedDictionary<string, int>(StringComparer.Ordinal) { [b] = 2 };
            SortedDictionary<string, SnapGroup> s0 = new SortedDictionary<string, SnapGroup>(StringComparer.Ordinal);
            SortedDictionary<string, SnapGroup> s1 = new SortedDictionary<string, SnapGroup>(StringComparer.Ordinal);
            if (snapshot)
            {
                s0[a] = new SnapGroup(1, 123456);
                s1[b] = new SnapGroup(2, 654321);
            }
            return new FightRecord(id, fightCase, 1000, names ? "skai_ai_mix" : a, names ? "water_ai_mix" : b, u0, u1, 800, 800, mismatch, result, "eliminated", 100, "5.0", 0.5, 0.0, 1, 0, -1, 1UL, 2UL,
                new FightExtension(ratioPt, depth, snapshot ? 40 : -1, s0, s1));
        }

        /// <summary>FP2-1-7: E, U and D are counted correctly on synthetic records; snapshot predictions use GapTiles 0 and the snapshot health; start predictions use start_distance_tiles and the water of lane_depth_class.</summary>
        [Test]
        public void Subsets_AndContexts()
        {
            DataSet data = TestData.LoadAll();
            FightSettings fs = FightSettings.Load(TestData.FindDataDirectory(), data);
            PredictSettings ps = PredictSettings.Load(TestData.FindDataDirectory());
            PredictorSettings predictor = PredictorSettings.From(AiProfile.FromDataSet(data, "aggressive_s5"));
            List<FightRecord> records = new List<FightRecord>
            {
                Rec("flat/1000/skai.windrunner/water.grasper", "skai.windrunner", "water.grasper", FightRecord.Seat0Won, snapshot: true),
                Rec("flat/1000/water.grasper/skai.windrunner", "water.grasper", "skai.windrunner", FightRecord.Seat1Won),
                Rec("flat_uneven/2000/skai.windrunner/water.grasper/r12500", "skai.windrunner", "water.grasper", FightRecord.Seat0Won, 12500, fightCase: "flat", snapshot: true),
                Rec("flat_uneven/2000/water.grasper/skai.windrunner/r15000", "water.grasper", "skai.windrunner", FightRecord.Seat1Won, 15000),
                Rec("flat/1000/a/b", "skai.windrunner", "water.grasper", FightRecord.Seat0Won, mismatch: true, snapshot: true),
            };
            List<PredictedFight> starts = new List<PredictedFight>();
            List<PredictedFight> snaps = new List<PredictedFight>();
            foreach (FightRecord r in records)
            {
                starts.Add(PredictEvaluator.Predict(r, FightKinds.Flat, data, predictor, fs, null));
                PredictedFight? s = PredictEvaluator.PredictSnapshot(r, FightKinds.Flat, data, predictor, null);
                if (s != null)
                {
                    snaps.Add(s);
                }
            }
            Assert.That(snaps.Count, Is.EqualTo(3));
            PredictSubsets subsets = PredictSubsets.Build(starts, snaps, fs, data, ps);
            Assert.That(subsets.E.Fights.Count, Is.EqualTo(3), "ratio-1 fights, the mismatched one included in the list");
            Assert.That(subsets.E.Mismatched, Is.EqualTo(1));
            Assert.That(subsets.E.Overall.Fights, Is.EqualTo(2));
            Assert.That(subsets.U.Overall.Fights, Is.EqualTo(2));
            Assert.That(subsets.EU.Overall.Fights, Is.EqualTo(4));
            Assert.That(subsets.D.Overall.Fights, Is.EqualTo(2), "the mismatched fight's snapshot is excluded");
            // A record without extended keys is in no subset.
            FightRecord oldStyle = new FightRecord("flat/1000/a/b", "flat", 1000, "a", "b", records[0].Units0, records[0].Units1, 800, 800, false, "seat0", "eliminated", 1, "0.1", 0.5, 0, 1, 0, -1, 1UL, 2UL);
            Assert.That(PredictSubsets.Build(new[] { PredictEvaluator.Predict(oldStyle, FightKinds.Flat, data, predictor, fs, null) }, new List<PredictedFight>(), fs, data, ps).EU.Fights.Count, Is.EqualTo(0));
            // Contexts.
            FightContext start = PredictEvaluator.StartContext(records[0], FightKinds.Flat, fs);
            Assert.That(start.GapTiles, Is.EqualTo(Fix64.FromInt(fs.StartDistanceTiles)));
            Assert.That(start.Water, Is.EqualTo(FightWater.None));
            Assert.That(start.WaterSide, Is.EqualTo(-1));
            Assert.That(start.AUphill, Is.False);
            Assert.That(PredictEvaluator.StartContext(records[0], FightKinds.HighGround, fs).AUphill, Is.True);
            Assert.That(PredictEvaluator.StartContext(Rec("x", "skai.windrunner", "water.grasper", "seat0", depth: 2, fightCase: "flooded"), FightKinds.Flooded, fs).Water, Is.EqualTo(FightWater.Deep));
            Assert.That(PredictEvaluator.StartContext(Rec("x", "skai.windrunner", "water.grasper", "seat0", depth: 1, fightCase: "flooded"), FightKinds.Flooded, fs).Water, Is.EqualTo(FightWater.Shallow));
            Assert.That(PredictEvaluator.StartContext(Rec("x", "skai.windrunner", "water.grasper", "seat0", depth: 0, fightCase: "flooded"), FightKinds.Flooded, fs).Water, Is.EqualTo(FightWater.None));
            FightRecord noDepth = new FightRecord("flooded/1/a/b", "flooded", 1, "a", "b", records[0].Units0, records[0].Units1, 1, 1, false, "seat0", "eliminated", 1, "0.1", 0.5, 0, 1, 0, 5, 1UL, 2UL);
            Assert.That(PredictEvaluator.StartContext(noDepth, FightKinds.Flooded, fs).Water, Is.EqualTo(FightWater.Shallow), "a record without lane_depth_class is Shallow in a flooded case");
            Assert.That(PredictEvaluator.StartContext(noDepth, FightKinds.Flat, fs).Water, Is.EqualTo(FightWater.None));
            FightContext snap = PredictEvaluator.SnapshotContext(records[0], FightKinds.Flat);
            Assert.That(snap.GapTiles, Is.EqualTo(Fix64.Zero));
            // Snapshot entries: 123456 thousandths = 123.456 health for 1 unit; 654321 = 654.321 for 2.
            List<FightEntry> entries = PredictEvaluator.SnapshotEntries(records[0].Extension!.Snap1, data);
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Count, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That((entries[0].Health - Fix64.Parse("654.321")).Abs(), Is.LessThanOrEqualTo(Fix64.FromRaw(4)));
        }

        /// <summary>A start prediction of a synthetic record with a given class and margin, as if made by the profile (records and margins are what the bars read).</summary>
        private static PredictedFight Pf(FightRecord record, string cls, string margin = "2")
        {
            return new PredictedFight(record, cls, Fix64.Parse(margin), Fix64.One, Fix64.One);
        }

        /// <summary>The bar with this ID.</summary>
        private static BarResult Bar(List<BarResult> bars, string id)
        {
            return bars.Find(b => b.Id == id)!;
        }

        /// <summary>Subsets from lists of start and snapshot predictions.</summary>
        private static PredictSubsets Sub(DataSet data, FightSettings fs, PredictSettings ps, List<PredictedFight> starts, List<PredictedFight> snaps)
        {
            return PredictSubsets.Build(starts, snaps, fs, data, ps);
        }

        /// <summary>
        /// n decided fights of E between non-unit sides ("a", "b"): <paramref name="correct"/> predicted right with the margin, the rest predicted the other seat.
        /// </summary>
        private static void AddE(List<PredictedFight> list, int correct, int wrong, string margin = "2", string a = W, string b = G, int ratioPt = 10000, bool names = false)
        {
            for (int i = 0; i < correct + wrong; i++)
            {
                FightRecord r = Rec("flat/1000/" + a + "/" + b + "/" + list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), a, b, FightRecord.Seat0Won, ratioPt, names: names);
                list.Add(Pf(r, i < correct ? "seat0" : "seat1", margin));
            }
        }

        /// <summary>FP2-1-8: synthetic predictions at each bar's edge give PASS, FAIL and UNJUDGED as section 6 states; without a baseline the comparison bars are UNJUDGED.</summary>
        [Test]
        public void Bars_PassFailUnjudgedAtTheEdges()
        {
            DataSet data = TestData.LoadAll();
            FightSettings fs = FightSettings.Load(TestData.FindDataDirectory(), data);
            PredictSettings ps = PredictSettings.Load(TestData.FindDataDirectory());
            Fix64 attackMin = Fix64.Parse("1.3");
            List<PredictedFight> none = new List<PredictedFight>();

            // B1: 80 of 100 against a baseline of 75 of 100 is exactly the gain 0.05 (PASS); 76 of 100 is 0.04 (FAIL); 79 of 100 is below 0.80 (FAIL).
            List<PredictedFight> c80 = new List<PredictedFight>();
            AddE(c80, 80, 20);
            List<PredictedFight> b75 = new List<PredictedFight>();
            AddE(b75, 75, 25);
            List<PredictedFight> b76 = new List<PredictedFight>();
            AddE(b76, 76, 24);
            List<PredictedFight> c79 = new List<PredictedFight>();
            AddE(c79, 79, 21);
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, c80, none), Sub(data, fs, ps, b75, none), data), "B1").Status, Is.EqualTo("PASS"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, c80, none), Sub(data, fs, ps, b76, none), data), "B1").Status, Is.EqualTo("FAIL"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, c79, none), Sub(data, fs, ps, b75, none), data), "B1").Status, Is.EqualTo("FAIL"));
            List<BarResult> noBase = PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, c80, none), null, data);
            Assert.That(Bar(noBase, "B1").Status, Is.EqualTo("UNJUDGED"));
            Assert.That(Bar(noBase, "B1").Reason, Does.Contain("baseline"));
            Assert.That(Bar(noBase, "B6").Status, Is.EqualTo("UNJUDGED"));
            Assert.That(Bar(noBase, "B7").Status, Is.EqualTo("UNJUDGED"));
            Assert.That(Bar(noBase, "C1").Status, Is.EqualTo("STDERR"));
            Assert.That(PredictBars.Verdict(noBase, out int passed0, out int judged0), Is.EqualTo("INCOMPLETE"));
            Assert.That(judged0, Is.LessThan(7));
            Assert.That(passed0, Is.LessThanOrEqualTo(judged0));

            // B2: bin [1, 1.1) 8 of 10, bin [1.1, 1.25) 6 of 10 is a drop of 0.2 (FAIL); 7 of 10 is a drop of exactly 0.10, which is not more than the limit (PASS).
            List<PredictedFight> inverted = new List<PredictedFight>();
            AddE(inverted, 8, 2, "1.05");
            AddE(inverted, 6, 4, "1.2");
            List<PredictedFight> edge = new List<PredictedFight>();
            AddE(edge, 8, 2, "1.05");
            AddE(edge, 7, 3, "1.2");
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, inverted, none), null, data), "B2").Status, Is.EqualTo("FAIL"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, edge, none), null, data), "B2").Status, Is.EqualTo("PASS"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, none, none), null, data), "B2").Status, Is.EqualTo("UNJUDGED"));

            // B3: margin 1.3 is inside the window, 1.5 is outside; 7 of 10 right is exactly 0.70 (PASS), 6 of 10 FAIL, 9 fights UNJUDGED.
            List<PredictedFight> b3Pass = new List<PredictedFight>();
            AddE(b3Pass, 7, 3, "1.3");
            AddE(b3Pass, 0, 10, "1.5");
            List<PredictedFight> b3Fail = new List<PredictedFight>();
            AddE(b3Fail, 6, 4, "1.4999");
            List<PredictedFight> b3Few = new List<PredictedFight>();
            AddE(b3Few, 9, 0, "1.4");
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, b3Pass, none), null, data), "B3").Status, Is.EqualTo("PASS"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, b3Fail, none), null, data), "B3").Status, Is.EqualTo("FAIL"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, b3Few, none), null, data), "B3").Status, Is.EqualTo("UNJUDGED"));

            // B4: 5 no-winner fights of which 3 are predicted none (PASS at 0.5), 2 of 5 (FAIL), 4 fights (UNJUDGED); 100 decided with 2 predicted none (PASS), 3 (FAIL).
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, NoWinner(3, 2, 100, 2), none), null, data), "B4").Status, Is.EqualTo("PASS"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, NoWinner(2, 3, 100, 2), none), null, data), "B4").Status, Is.EqualTo("FAIL"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, NoWinner(2, 2, 100, 2), none), null, data), "B4").Status, Is.EqualTo("UNJUDGED"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, NoWinner(3, 2, 100, 3), none), null, data), "B4").Status, Is.EqualTo("FAIL"));

            // B5: a pair of two single units, 8 decided fights: 4 of 10 is exactly 0.40 (PASS), 3 of 10 FAIL, 7 decided fights UNJUDGED; sides that are not units are not pairs.
            List<PredictedFight> pass5 = new List<PredictedFight>();
            AddE(pass5, 4, 6, "2", "skai.windrunner", "water.grasper");
            List<PredictedFight> fail5 = new List<PredictedFight>();
            AddE(fail5, 3, 7, "2", "skai.windrunner", "water.grasper");
            List<PredictedFight> few5 = new List<PredictedFight>();
            AddE(few5, 0, 7, "2", "skai.windrunner", "water.grasper");
            List<PredictedFight> notUnits = new List<PredictedFight>();
            AddE(notUnits, 0, 10, "2", W, G, 10000, true);
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, pass5, none), null, data), "B5").Status, Is.EqualTo("PASS"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, fail5, none), null, data), "B5").Status, Is.EqualTo("FAIL"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, few5, none), null, data), "B5").Status, Is.EqualTo("UNJUDGED"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, notUnits, none), null, data), "B5").Status, Is.EqualTo("UNJUDGED"));

            // B6: 75 of 100 on D against a baseline of 75 (PASS); 74 of 100 (FAIL); 80 of 100 against a baseline of 81 (FAIL).
            List<PredictedFight> d75 = Snaps(75, 25);
            List<PredictedFight> d74 = Snaps(74, 26);
            List<PredictedFight> d80 = Snaps(80, 20);
            List<PredictedFight> d81 = Snaps(81, 19);
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, c80, d75), Sub(data, fs, ps, b75, d75), data), "B6").Status, Is.EqualTo("PASS"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, c80, d74), Sub(data, fs, ps, b75, d74), data), "B6").Status, Is.EqualTo("FAIL"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, c80, d80), Sub(data, fs, ps, b75, d81), data), "B6").Status, Is.EqualTo("FAIL"));

            // B7: accuracy on U equal to the baseline's passes, one fight below fails.
            List<PredictedFight> u60 = new List<PredictedFight>();
            AddE(u60, 6, 4, "2", W, G, 12500);
            List<PredictedFight> u59 = new List<PredictedFight>();
            AddE(u59, 5, 5, "2", W, G, 12500);
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, u60, none), Sub(data, fs, ps, u60, none), data), "B7").Status, Is.EqualTo("PASS"));
            Assert.That(Bar(PredictBars.Judge(ps, attackMin, Sub(data, fs, ps, u59, none), Sub(data, fs, ps, u60, none), data), "B7").Status, Is.EqualTo("FAIL"));

            // Verdict: every bar judged and passing is PASS with bars=n/n; a failing one is FAIL.
            List<BarResult> all = new List<BarResult>
            {
                new BarResult("B1", "x", "y", "PASS", string.Empty), new BarResult("B2", "x", "y", "PASS", string.Empty), new BarResult("C1", "x", "y", "STDERR", string.Empty),
            };
            Assert.That(PredictBars.Verdict(all, out int passed, out int judged), Is.EqualTo("PASS"));
            Assert.That(passed, Is.EqualTo(2));
            Assert.That(judged, Is.EqualTo(2));
            all.Add(new BarResult("B3", "x", "y", "UNJUDGED", "r"));
            Assert.That(PredictBars.Verdict(all, out passed, out judged), Is.EqualTo("INCOMPLETE"));
            all.Add(new BarResult("B4", "x", "y", "FAIL", string.Empty));
            Assert.That(PredictBars.Verdict(all, out passed, out judged), Is.EqualTo("FAIL"));
            Assert.That(judged, Is.EqualTo(3));
        }

        /// <summary>No-winner fights and decided fights of E: noneOfNoWinner of noWinner are predicted none, the decided fights are predicted right except decidedNone predicted none.</summary>
        private static List<PredictedFight> NoWinner(int noneOfNoWinner, int otherNoWinner, int decided, int decidedNone)
        {
            List<PredictedFight> list = new List<PredictedFight>();
            for (int i = 0; i < noneOfNoWinner + otherNoWinner; i++)
            {
                list.Add(Pf(Rec("flat/1000/a/b/s" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), W, G, FightRecord.Stalemate), i < noneOfNoWinner ? "none" : "seat0", i < noneOfNoWinner ? "1" : "2"));
            }
            for (int i = 0; i < decided; i++)
            {
                list.Add(Pf(Rec("flat/1000/a/b/d" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), W, G, FightRecord.Seat0Won), i < decidedNone ? "none" : "seat0", i < decidedNone ? "1" : "2"));
            }
            return list;
        }

        /// <summary>Snapshot predictions: <paramref name="correct"/> right and <paramref name="wrong"/> wrong.</summary>
        private static List<PredictedFight> Snaps(int correct, int wrong)
        {
            List<PredictedFight> list = new List<PredictedFight>();
            for (int i = 0; i < correct + wrong; i++)
            {
                list.Add(Pf(Rec("flat/1000/a/b/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), W, G, FightRecord.Seat0Won, snapshot: true), i < correct ? "seat0" : "seat1"));
            }
            return list;
        }

        /// <summary>FP2-1-9: with hit_delay_ignore_ticks 2 a fight whose weapons all have 2 ticks has no hit_delay, and the committed sweep has a non-empty clean row; with 0 the original rule holds.</summary>
        [Test]
        public void HitDelayCorrection_CleanRowNotEmpty()
        {
            DataSet data = TestData.LoadAll();
            Assert.That(data.Content.Units["water.ink_lobber"].Weapon!.HitDelayTicks, Is.EqualTo(2));
            FightRecord lobbers = Rec("flat/1000/water.ink_lobber/water.ink_lobber", "water.ink_lobber", "water.ink_lobber", FightRecord.Seat0Won);
            Assert.That(PredictEvaluator.MechanicsPresent(lobbers, FightKinds.Flat, data, 2), Does.Not.Contain("hit_delay"));
            Assert.That(PredictEvaluator.MechanicsPresent(lobbers, FightKinds.Flat, data, 0), Does.Contain("hit_delay"));
            Assert.That(PredictSettings.Load(TestData.FindDataDirectory()).HitDelayIgnoreTicks, Is.EqualTo(2));
            // The committed sweep: clean decided fights exist under the corrected rule, none under the original one.
            FightSettings fs = FightSettings.Load(TestData.FindDataDirectory(), data);
            PredictorSettings predictor = PredictorSettings.From(AiProfile.FromDataSet(data, "aggressive_s5"));
            List<PredictedFight> fights = new List<PredictedFight>();
            foreach (FightRecord r in PredictRunner.ReadRecords(File.ReadAllLines(UnderRts(CommittedSweep))))
            {
                fights.Add(PredictEvaluator.Predict(r, fs.CaseById(r.Case).Kind, data, predictor));
            }
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), PredictSettings.RelativePath));
            PredictEvaluation corrected = PredictEvaluator.Evaluate(fights, fs, data, PredictSettings.Parse(text));
            PredictEvaluation original = PredictEvaluator.Evaluate(fights, fs, data, PredictSettings.Parse(FightTestSupport.SetKey(text, "hit_delay_ignore_ticks", "0")));
            Assert.That(corrected.Clean.Decided, Is.GreaterThan(0));
            Assert.That(original.Clean.Decided, Is.EqualTo(0));
        }

        /// <summary>FP2-1-10: --jobs 1 and --jobs 4 give identical fights.jsonl, predict.jsonl and predict.md on a small extended settings file given by --fight-settings; the option replaces the default file.</summary>
        [Test]
        public void Determinism_JobsAndFightSettingsOption()
        {
            string folder = ExtendedQuick("[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500], \"seat1_value_ratios\": [\"1\", \"1.25\"] } ]", "0.25", "chunk_fights", "2");
            // A custom file with two units only; the folder's own fights.json keeps three units, so the option must be read.
            string custom = Path.Combine(folder, "scripts/fights/custom_fights.json");
            File.WriteAllText(custom, FightTestSupport.SetKey(File.ReadAllText(Path.Combine(folder, FightTestSupport.SettingsPath)), "units", "[\"skai.windrunner\", \"water.grasper\"]"));
            string[] fightFiles = new string[2];
            string[] predictFiles = new string[2];
            string[] predictLines = new string[2];
            int[] jobs = { 1, 4 };
            for (int i = 0; i < 2; i++)
            {
                string outFights = Path.Combine(HarnessTestSupport.NewFolder(), "fights");
                int exit = HarnessTestSupport.Run(new[] { "--data", folder, "--fights", "--fight-settings", custom, "--jobs", jobs[i].ToString(), "--out", outFights }, out string output, out string error);
                Assert.That(exit, Is.EqualTo(0), output + error);
                fightFiles[i] = File.ReadAllText(Path.Combine(outFights, "fights.jsonl"));
                string outPredict = Path.Combine(HarnessTestSupport.NewFolder(), "predict");
                exit = HarnessTestSupport.Run(new[] { "--data", folder, "--predict-fights", "--fight-settings", custom, "--profile", "aggressive_s5", "--baseline-profile", "aggressive_s3", "--jobs", jobs[i].ToString(), "--out", outPredict },
                    out output, out error);
                Assert.That(exit, Is.EqualTo(0), output + error);
                predictFiles[i] = File.ReadAllText(Path.Combine(outPredict, "predict.jsonl")) + "\n=====\n" + File.ReadAllText(Path.Combine(outPredict, "predict.md"));
                predictLines[i] = output.TrimEnd('\n').Split('\n')[^1];
                Assert.That(error, Does.Contain("predict time profile aggressive_s5"), "timing goes to stderr");
                Assert.That(predictFiles[i], Does.Not.Contain("predict time").And.Not.Contain("microseconds)"), "and never to a file");
                Assert.That(output, Does.Not.Contain("predict time"));
            }
            Assert.That(fightFiles[0].Split('\n').Length - 1, Is.EqualTo(4), "one pair in both seat orders, at two ratios");
            Assert.That(fightFiles[0], Does.Contain("value_ratio_pt\":12500"));
            Assert.That(fightFiles[1], Is.EqualTo(fightFiles[0]));
            Assert.That(predictFiles[1], Is.EqualTo(predictFiles[0]));
            Assert.That(predictLines[0], Does.Match(@"^PREDICT fights=\d+ .* bars=\d+/\d+ verdict=(PASS|FAIL|INCOMPLETE)$"));
            Assert.That(predictLines[1], Is.EqualTo(predictLines[0]));
            Assert.That(predictFiles[0], Does.Contain("## Bars"));
        }

        /// <summary>heldout.json parses with the shipped data, plans 2394 fights (1710 equal-value, 684 uneven) with no army rejected by the arena, and the matchups file accepts it (nothing is played).</summary>
        [Test]
        public void HeldOutSettings_ParseAndPlan_NothingPlayed()
        {
            DataSet data = TestData.LoadAll();
            string path = Path.Combine(TestData.FindDataDirectory(), "scripts/fights/heldout.json");
            FightSettings settings = FightSettings.Parse(File.ReadAllText(path), data, "heldout.json");
            Assert.That(settings.BaseSeed, Is.EqualTo(20261121UL));
            Assert.That(settings.ExtendedRecords, Is.True);
            Assert.That(settings.SnapshotLossPt, Is.EqualTo(2500));
            Assert.That(settings.MaxFightSeconds, Is.EqualTo(Fix64.FromInt(300)));
            Assert.That(settings.RuleArmyValue, Is.EqualTo(1000));
            FightPlan plan = FightPlan.Build(data, settings);
            Assert.That(plan.Sides.Count, Is.EqualTo(17), "11 combat units and 6 compositions (the spec says about 19)");
            Assert.That(plan.Fights.Count, Is.EqualTo(1904), "136 pairs x 2 orders x (5 equal cells + 2 ratios)");
            int equal = 0;
            foreach (FightSpec fight in plan.Fights)
            {
                equal += fight.RatioPt == FightSpec.EqualRatioPt ? 1 : 0;
            }
            Assert.That(equal, Is.EqualTo(1360));
            Expect.DoesNotThrow(() => FightMatchups.Load(TestData.FindDataDirectory(), settings, data));
        }

        /// <summary>heldout2.json (the second held-out set, made after the first failed its gate) parses and plans with the shipped data, uses a new seed and army values no earlier set played, and nothing is played.</summary>
        [Test]
        public void HeldOut2Settings_ParseAndPlan_NewSeedAndValues()
        {
            DataSet data = TestData.LoadAll();
            FightSettings first = FightSettings.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "scripts/fights/heldout.json")), data, "heldout.json");
            FightSettings second = FightSettings.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "scripts/fights/heldout2.json")), data, "heldout2.json");
            FightSettings sweep = FightSettings.Load(TestData.FindDataDirectory(), data);
            Assert.That(second.BaseSeed, Is.Not.EqualTo(first.BaseSeed));
            Assert.That(second.BaseSeed, Is.Not.EqualTo(sweep.BaseSeed));
            // Army values of the earlier sets.
            SortedSet<int> used = new SortedSet<int>();
            foreach (FightSettings earlier in new[] { first, sweep })
            {
                foreach (FightCase fightCase in earlier.Cases)
                {
                    used.UnionWith(fightCase.ArmyValues);
                }
            }
            foreach (FightCase fightCase in second.Cases)
            {
                foreach (int value in fightCase.ArmyValues)
                {
                    Assert.That(used.Contains(value), Is.False, fightCase.Id + " " + value);
                }
            }
            Assert.That(second.ExtendedRecords, Is.True);
            Assert.That(FightPlan.Build(data, second).Fights.Count, Is.EqualTo(1904));
            Expect.DoesNotThrow(() => FightMatchups.Load(TestData.FindDataDirectory(), second, data));
        }

        /// <summary>roster_numbers_1.json (the bar set of specs/roster_numbers_1.md section 3) parses and plans with the shipped data, uses only army values no committed set played, and the matchups file accepts it; nothing is played.</summary>
        [Test]
        public void RosterNumbers1Settings_ParseAndPlan_NewValues()
        {
            DataSet data = TestData.LoadAll();
            // Data folder of the shipped files.
            string dir = TestData.FindDataDirectory();
            FightSettings bars = FightSettings.Parse(File.ReadAllText(Path.Combine(dir, "scripts/fights/roster_numbers_1.json")), data, "roster_numbers_1.json");
            // Army values the three committed sets played. Unit: resource units.
            SortedSet<int> used = new SortedSet<int>();
            foreach (string file in new[] { "fights.json", "heldout.json", "heldout2.json" })
            {
                FightSettings earlier = FightSettings.Parse(File.ReadAllText(Path.Combine(dir, "scripts/fights", file)), data, file);
                foreach (FightCase fightCase in earlier.Cases)
                {
                    used.UnionWith(fightCase.ArmyValues);
                }
            }
            foreach (FightCase fightCase in bars.Cases)
            {
                foreach (int value in fightCase.ArmyValues)
                {
                    Assert.That(used.Contains(value), Is.False, fightCase.Id + " " + value);
                }
            }
            // Planning throws when an army does not fit the arena.
            Assert.That(FightPlan.Build(data, bars).Fights.Count, Is.GreaterThan(0));
            Expect.DoesNotThrow(() => FightMatchups.Load(dir, bars, data));
        }
    }
}

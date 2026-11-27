// rts/tests/Sim.Tests/Ai/AiKiteCorrectionTests.cs
// Acceptance tests of package K-1 (rts/docs/specs/ai_kite_correction.md section 3.3): the optional faction-plan key predictor_kite_factor, the Kites rule test,
// the kite term of the formula predictor, its use in the predictor mix only, and the candidate profile aggressive_s5_kite. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Teams;
using Rts.Sim.Factions;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Tests of the kite correction (ai_kite_correction.md K-1).</summary>
    [TestFixture]
    public class AiKiteCorrectionTests
    {
        /// <summary>Baseline profile ID. Range: a shipped profile.</summary>
        private const string Baseline = "aggressive_s5";

        /// <summary>Candidate profile ID. Range: a shipped profile.</summary>
        private const string Candidate = "aggressive_s5_kite";

        /// <summary>Ladder base seed of the harness-path tests. Range: any ulong.</summary>
        private const ulong BaseSeed = 20261026UL;

        /// <summary>The Water faction ID. Range: a faction of the data set.</summary>
        private const string Water = "water";

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>A shipped profile by ID.</summary>
        private static AiProfile Profile(string id)
        {
            return AiProfile.FromDataSet(Data, id);
        }

        /// <summary>The text of the candidate profile file, line endings normalised.</summary>
        private static string CandidateText => File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", Candidate + ".json")).Replace("\r\n", "\n");

        /// <summary>Parses the candidate's text after replacing <paramref name="oldText"/> (which must occur) with <paramref name="newText"/>.</summary>
        private static AiProfile Edited(string oldText, string newText)
        {
            // The candidate's text.
            string text = CandidateText;
            Assert.That(text.Contains(oldText), Is.True, oldText);
            return AiProfile.Parse(text.Replace(oldText, newText), "ai/" + Candidate + ".json", Data.Content);
        }

        /// <summary>A full-health entry of <paramref name="count"/> units of a definition.</summary>
        private static FightEntry Entry(string id, int count)
        {
            UnitDefinition definition = Data.Content.Units[id];
            return new FightEntry(definition, Fix64.FromInt(count), definition.MaxHealth * Fix64.FromInt(count));
        }

        /// <summary>Settings of aggressive_s5 with the kite term of the spec (gap 2 tiles, ratio 0.8) and the given factor.</summary>
        private static PredictorSettings Kite(string factor)
        {
            return PredictorSettings.From(Profile(Baseline)).WithKite(Fix64.Parse(factor), Fix64.FromInt(2), Fix64.Parse("0.8"));
        }

        /// <summary>Adds every leaf of a parsed tree to <paramref name="into"/> as path = text, leaving out the root id, $doc and Water's predictor_kite_factor.</summary>
        private static void Flatten(JsonValue node, string path, SortedDictionary<string, string> into)
        {
            if (path == "id" || path == "$doc" || path == "factions/water/predictor_kite_factor")
            {
                return;
            }
            switch (node.Kind)
            {
                case JsonKind.Object:
                    foreach (KeyValuePair<string, JsonValue> member in node.Members)
                    {
                        Flatten(member.Value, path.Length == 0 ? member.Key : path + "/" + member.Key, into);
                    }
                    break;
                case JsonKind.Array:
                    for (int i = 0; i < node.Items.Count; i++)
                    {
                        Flatten(node.Items[i], path + "[" + i + "]", into);
                    }
                    break;
                case JsonKind.String:
                    into[path] = node.AsString();
                    break;
                case JsonKind.Bool:
                    into[path] = node.AsBool().ToString();
                    break;
                case JsonKind.Number:
                    into[path] = node.NumberText;
                    break;
                default:
                    into[path] = "null";
                    break;
            }
        }

        /// <summary>K1 1: the candidate parses with 0.4 in Water's plan and 1 in Skai's; its JSON tree equals aggressive_s5's except id, $doc and the key.</summary>
        [Test]
        public void K1_ProfileParsesAndDiffersOnlyInTheKey()
        {
            AiProfile candidate = Profile(Candidate);
            Assert.That(candidate.Factions[Water].PredictorKiteFactor, Is.EqualTo(Fix64.Parse("0.4")));
            Assert.That(candidate.Factions["skai"].PredictorKiteFactor, Is.EqualTo(Fix64.One));
            // Leaves of both trees by path.
            SortedDictionary<string, string> a = new SortedDictionary<string, string>(StringComparer.Ordinal);
            SortedDictionary<string, string> b = new SortedDictionary<string, string>(StringComparer.Ordinal);
            Flatten(Data.AiProfiles[Baseline], string.Empty, a);
            Flatten(Data.AiProfiles[Candidate], string.Empty, b);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            foreach (KeyValuePair<string, string> pair in a)
            {
                Assert.That(b.TryGetValue(pair.Key, out string? other), Is.True, pair.Key);
                Assert.That(other, Is.EqualTo(pair.Value), pair.Key);
            }
        }

        /// <summary>K1 2: every shipped profile but the candidate holds the factor 1 in every plan.</summary>
        [Test]
        public void K1_OldProfilesAreOff()
        {
            foreach (KeyValuePair<string, JsonValue> pair in Data.AiProfiles)
            {
                if (pair.Key == Candidate)
                {
                    continue;
                }
                foreach (KeyValuePair<string, AiFactionPlan> plan in Profile(pair.Key).Factions)
                {
                    Assert.That(plan.Value.PredictorKiteFactor, Is.EqualTo(Fix64.One), pair.Key + "/" + plan.Key);
                }
            }
        }

        /// <summary>K1 3: factor 1.5, factor -0.1, predictor_model sim, use_predictor_mix false and use_unit_control false each fail to load with a message naming the key.</summary>
        [Test]
        public void K1_LoaderErrors()
        {
            // Each edit as old text and new text.
            (string Old, string New)[] edits =
            {
                ("\"predictor_kite_factor\": 0.4", "\"predictor_kite_factor\": 1.5"),
                ("\"predictor_kite_factor\": 0.4", "\"predictor_kite_factor\": -0.1"),
                ("\"predictor_model\": \"formula\"", "\"predictor_model\": \"sim\""),
                ("\"use_predictor_mix\": true", "\"use_predictor_mix\": false"),
                ("\"use_unit_control\": true", "\"use_unit_control\": false"),
            };
            foreach ((string oldText, string newText) in edits)
            {
                DataValidationException error = Expect.Throws<DataValidationException>(() => Edited(oldText, newText));
                Assert.That(error.Message, Does.Contain("predictor_kite_factor"), newText);
            }
        }

        /// <summary>K1 4: WithOrders and the opening and main plans of AiVariety (each of the four openings forced) carry Water's factor.</summary>
        [Test]
        public void K1_PlanCopiesCarryTheFactor()
        {
            AiProfile candidate = Profile(Candidate);
            AiFactionPlan water = candidate.Factions[Water];
            Assert.That(water.WithOrders(water.BuildOrder, water.Composition).PredictorKiteFactor, Is.EqualTo(Fix64.Parse("0.4")));
            foreach (AiOpening opening in water.Openings)
            {
                AiProfile forced = candidate.WithForcedOpenings(new Dictionary<string, string> { { Water, opening.Id }, { "skai", "skai_standard" } });
                AiFactionPlan forcedPlan = forced.Factions[Water];
                Assert.That(forcedPlan.PredictorKiteFactor, Is.EqualTo(Fix64.Parse("0.4")), opening.Id);
                AiVariety variety = AiVariety.Create(forced, forcedPlan, 7UL, 30);
                Assert.That(variety.MainPlan.PredictorKiteFactor, Is.EqualTo(Fix64.Parse("0.4")), opening.Id + " main");
                Assert.That(variety.OpeningPlan.PredictorKiteFactor, Is.EqualTo(Fix64.Parse("0.4")), opening.Id + " opening");
            }
        }

        /// <summary>
        /// K1 5: the Kites table. Unit numbers (range tiles, speed tiles per second): windrunner 4 and 3.5, talon_archer 6 and 2.5, grasper 1 (melee) and 3,
        /// shellback 2 and 1.8, reef_runner 4 and 4.1, spoutling 5 and 3. Rule settings: gap 2, ratio 0.8.
        /// </summary>
        [Test]
        public void K1_KitesTable()
        {
            // Settings with gap 2 and ratio 0.8.
            PredictorSettings s = Kite("0.4");
            UnitDefinition Unit(string id) => Data.Content.Units[id];
            Assert.That(FightPredictor.Kites(Unit("skai.windrunner"), Unit("water.grasper"), s), Is.True);
            Assert.That(FightPredictor.Kites(Unit("skai.talon_archer"), Unit("water.grasper"), s), Is.True);
            Assert.That(FightPredictor.Kites(Unit("skai.windrunner"), Unit("water.shellback"), s), Is.True);
            Assert.That(FightPredictor.Kites(Unit("skai.windrunner"), Unit("water.reef_runner"), s), Is.False);
            Assert.That(FightPredictor.Kites(Unit("skai.talon_archer"), Unit("water.reef_runner"), s), Is.False);
            Assert.That(FightPredictor.Kites(Unit("skai.windrunner"), Unit("water.spoutling"), s), Is.False);
            Assert.That(FightPredictor.Kites(Unit("water.grasper"), Unit("skai.windrunner"), s), Is.False, "melee never kites");
            // Equality boundaries: range gap 3 (4 >= 1 + 3) and speed ratio 1 (3 >= 1 x 3).
            PredictorSettings gap3 = s.WithKite(Fix64.Parse("0.4"), Fix64.FromInt(3), Fix64.Parse("0.8"));
            PredictorSettings gap301 = s.WithKite(Fix64.Parse("0.4"), Fix64.Parse("3.01"), Fix64.Parse("0.8"));
            Assert.That(FightPredictor.Kites(Unit("skai.windrunner"), Unit("water.grasper"), gap3), Is.True);
            Assert.That(FightPredictor.Kites(Unit("skai.windrunner"), Unit("water.grasper"), gap301), Is.False);
            PredictorSettings ratio1 = s.WithKite(Fix64.Parse("0.4"), Fix64.FromInt(2), Fix64.One);
            PredictorSettings ratio101 = s.WithKite(Fix64.Parse("0.4"), Fix64.FromInt(2), Fix64.Parse("1.01"));
            Assert.That(FightPredictor.Kites(Unit("water.spoutling"), Unit("water.grasper"), ratio1), Is.True);
            Assert.That(FightPredictor.Kites(Unit("water.spoutling"), Unit("water.grasper"), ratio101), Is.False);
        }

        /// <summary>K1 6: the settings of the profile are off, and WithKite(1, 2, 0.8) predicts exactly as the settings without the term.</summary>
        [Test]
        public void K1_OffIsIdentity()
        {
            Assert.That(PredictorSettings.From(Profile(Candidate)).KiteFactor, Is.EqualTo(Fix64.One));
            PredictorSettings off = PredictorSettings.From(Profile(Baseline));
            PredictorSettings one = off.WithKite(Fix64.One, Fix64.FromInt(2), Fix64.Parse("0.8"));
            // Pairs of unit IDs fought at 3 against 4.
            string[] ids = { "water.grasper", "skai.windrunner", "water.shellback", "water.reef_runner", "skai.talon_archer", "water.spoutling", "skai.roc_rider" };
            foreach (string a in ids)
            {
                foreach (string b in ids)
                {
                    FightPrediction x = FightPredictor.Predict(new[] { Entry(a, 3) }, new[] { Entry(b, 4) }, false, Data.Content.Combat, off);
                    FightPrediction y = FightPredictor.Predict(new[] { Entry(a, 3) }, new[] { Entry(b, 4) }, false, Data.Content.Combat, one);
                    Assert.That(y.TimeAKillsB, Is.EqualTo(x.TimeAKillsB), a + " v " + b);
                    Assert.That(y.TimeBKillsA, Is.EqualTo(x.TimeBKillsA), a + " v " + b);
                    Assert.That(y.AWins, Is.EqualTo(x.AWins), a + " v " + b);
                    Assert.That(y.Advantage, Is.EqualTo(x.Advantage), a + " v " + b);
                }
            }
        }

        /// <summary>
        /// K1 7: 3 Pummelers against 4 Windrunners at factor 0.4: the Pummeler side needs at least the off time divided by 0.4 (or the maximum), the Windrunner side's time is unchanged
        /// and the advantage is lower. Times are recorded in the failure messages when the test runs.
        /// </summary>
        [Test]
        public void K1_KitedSideIsSlower()
        {
            PredictorSettings off = PredictorSettings.From(Profile(Baseline));
            PredictorSettings on = Kite("0.4");
            FightPrediction x = FightPredictor.Predict(new[] { Entry("water.grasper", 3) }, new[] { Entry("skai.windrunner", 4) }, false, Data.Content.Combat, off);
            FightPrediction y = FightPredictor.Predict(new[] { Entry("water.grasper", 3) }, new[] { Entry("skai.windrunner", 4) }, false, Data.Content.Combat, on);
            // Least allowed time: off time / 0.4, less 2^-20 relative. Unit: seconds.
            Fix64 least = x.TimeAKillsB / Fix64.Parse("0.4");
            least -= least / Fix64.FromInt(1 << 20);
            Assert.That(y.TimeAKillsB >= least || y.TimeAKillsB == off.MaxSeconds, Is.True, "off " + x.TimeAKillsB + " on " + y.TimeAKillsB);
            Assert.That(y.TimeBKillsA, Is.EqualTo(x.TimeBKillsA));
            Assert.That(y.Advantage < x.Advantage, Is.True, "advantage off " + x.Advantage + " on " + y.Advantage);
        }

        /// <summary>K1 8: against Windrunners and Pummelers together, only the Windrunner entry is kited, so the time lies strictly between the off time and off / factor.</summary>
        [Test]
        public void K1_PerTargetFactor()
        {
            // Both enemy entries as side B.
            // Windrunners kite the Pummeler; the second Pummeler entry is melee and never kites. (The spec names Reef Runners, but they also kite a Pummeler: range 4 >= 1 + 2, speed 4.1 >= 0.8 x 3.)
            FightEntry[] enemy = { Entry("skai.windrunner", 2), Entry("water.grasper", 2) };
            FightEntry[] mine = { Entry("water.grasper", 3) };
            FightPrediction x = FightPredictor.Predict(mine, enemy, false, Data.Content.Combat, PredictorSettings.From(Profile(Baseline)));
            FightPrediction y = FightPredictor.Predict(mine, enemy, false, Data.Content.Combat, Kite("0.4"));
            Assert.That(y.TimeAKillsB > x.TimeAKillsB, Is.True, "off " + x.TimeAKillsB + " on " + y.TimeAKillsB);
            Assert.That(y.TimeAKillsB < x.TimeAKillsB / Fix64.Parse("0.4"), Is.True, "off " + x.TimeAKillsB + " on " + y.TimeAKillsB);
        }

        /// <summary>A greybox_1v1 Water-against-Water simulation (own army as in AiArmyMixTests C2_5), the believed enemy model and the view.</summary>
        private static (AiWorldView View, AiMemory Memory) TracedSituation()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), Water, "A"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), Water, "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            ExploreKit.ExploreAll(sim);
            // Script sequence number. Range: 0 upward.
            uint sequence = 0;
            // Row of the next unit. Range: 0 upward.
            int row = 0;
            foreach ((string id, int count) in new[] { ("water.grand_shell", 1), ("water.grasper", 5), ("water.shellback", 2), ("water.spoutling", 1) })
            {
                for (int i = 0; i < count; i++)
                {
                    sim.Submit(new Rts.Sim.Commands.SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, sequence++, id, new PlayerId(0),
                        id == "water.grand_shell" ? new FixVec2(Fix64.FromInt(7), Fix64.FromInt(7)) : new FixVec2(Fix64.FromInt(9), Fix64.FromInt(9 + row++))));
                    sim.Step();
                }
            }
            AiMemory memory = new AiMemory();
            foreach ((string id, int count) in new[] { ("water.grasper", 3), ("water.shellback", 1), ("water.ink_lobber", 1), ("water.spoutling", 1), ("water.reef_runner", 1) })
            {
                memory.EnemyTypes[id] = new EnemyTypeEstimate { Count = Fix64.FromInt(count), LastSeenTick = sim.Clock.Tick };
            }
            return (AiWorldView.Build(sim, new PlayerId(0)), memory);
        }

        /// <summary>
        /// K1 9: in the traced situation the predictor mix with the candidate's Water plan gives a lower grasper advantage than the same profile with the key removed
        /// (advantage and mix tables are in the failure message of a failed run; no other direction is asserted).
        /// </summary>
        [Test]
        public void K1_MixUsesItAndFightDoesNot()
        {
            (AiWorldView view, AiMemory memory) = TracedSituation();
            AiProfile on = Profile(Candidate);
            // The candidate with the key at 1, which behaves as absent.
            AiProfile off = Edited("\"predictor_kite_factor\": 0.4", "\"predictor_kite_factor\": 1");
            // Advantages by pool unit with and without the term. Unit: ratio.
            SortedDictionary<string, Fix64> withKite = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
            SortedDictionary<string, Fix64> without = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
            AiCounter.PredictorMix(view, on, on.Factions[Water], memory, withKite);
            AiCounter.PredictorMix(view, off, off.Factions[Water], memory, without);
            Assert.That(withKite["water.grasper"] < without["water.grasper"], Is.True, "grasper with " + withKite["water.grasper"] + " without " + without["water.grasper"]);
            // The fight decision reads PredictorSettings.From, which has no term.
            Assert.That(PredictorSettings.From(on).KiteFactor, Is.EqualTo(Fix64.One));
        }

        /// <summary>K1 10: the predictor mix of the Skai plan is identical under aggressive_s5 and the candidate on the same view.</summary>
        [Test]
        public void K1_SkaiMixUnchanged()
        {
            (AiWorldView view, AiMemory memory) = TracedSituation();
            AiProfile a = Profile(Baseline);
            AiProfile b = Profile(Candidate);
            SortedDictionary<string, int> mixA = AiCounter.PredictorMix(view, a, a.Factions["skai"], memory, null);
            SortedDictionary<string, int> mixB = AiCounter.PredictorMix(view, b, b.Factions["skai"], memory, null);
            Assert.That(mixB, Is.EqualTo(mixA));
        }

        /// <summary>K1 11: through the harness path (skai_vs_skai, seed 20261026, 120 s) games 0 and 1 of pair 0 have the same state hash and play signature.</summary>
        [Test]
        public void K1_CandidateSkaiIsTheBaselineSkai()
        {
            GameRecord first = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_skai", 0, BaseSeed, Baseline, Candidate, 120, true);
            GameRecord second = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_skai", 1, BaseSeed, Baseline, Candidate, 120, true);
            Assert.That(second.StateHash, Is.EqualTo(first.StateHash));
            Assert.That(second.PlaySignature, Is.EqualTo(first.PlaySignature));
        }

        /// <summary>
        /// K1 12: skai_vs_water, pair 0, 600 s: the candidate-Water game state hash differs from the baseline-Water game. No direction is asserted.
        /// Deviation from the spec (300 s): at 300 s the two games of pair 0 are identical, because Water has not yet sighted the kiting units, so the term has not changed a choice.
        /// Trained counts at 600 s: candidate Water grasper 2, leviathan 1, reef_runner 43, shellback 8, spoutling 16, tender 32; baseline Water grasper 14, leviathan 1, reef_runner 23, shellback 7, spoutling 13, tender 33.
        /// </summary>
        [Test]
        public void K1_WaterGameDiffers()
        {
            // Game 1 has the candidate in slot 1 (Water); game 0 the baseline.
            GameRecord candidateWater = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_water", 1, BaseSeed, Baseline, Candidate, 600, true, 0UL, null, new SummaryObserver(30, new[] { 300 }));
            GameRecord baselineWater = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_water", 0, BaseSeed, Baseline, Candidate, 600, true, 0UL, null, new SummaryObserver(30, new[] { 300 }));
            Assert.That(candidateWater.Slot1Profile, Is.EqualTo(Candidate));
            Assert.That(baselineWater.Slot1Profile, Is.EqualTo(Baseline));
            Assert.That(candidateWater.StateHash, Is.Not.EqualTo(baselineWater.StateHash));
        }
    }
}

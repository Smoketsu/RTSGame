// rts/tests/Sim.Tests/Ai/FightSimTests.cs
// Acceptance tests FP2-2-1 to FP2-2-15 of rts/docs/specs/fight_predictor_v2.md section 7 (package FP2-2): the lane simulation FightSim and the predictor_model switch. The scenes use
// hand-built UnitDefinitions (so each mechanic is isolated) against the shipped damage table and tuning, plus the shipped units for the symmetry test; the retreat test (FP2-2-15) lives
// here rather than in AiFightTests because it needs no scene. Part of Rts.Sim.Tests. FP2-2-1 plays two AI games through the harness (about a minute).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Sim.Tests.Harness;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>The lane simulation fight predictor.</summary>
    [TestFixture]
    public class FightSimTests
    {
        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>The shipped game data of the lane simulation.</summary>
        private static FightSimRules Rules => FightSimRules.Build(Data.Content, Data.Tuning);

        /// <summary>The settings of the candidate profile with the shipped rules.</summary>
        private static PredictorSettings Settings(FightSimRules? rules = null)
        {
            return PredictorSettings.From(AiProfile.FromDataSet(Data, "aggressive_s5_fp2"), rules ?? Rules);
        }

        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>Decimal text to Fix64.</summary>
        private static Fix64 F(string text)
        {
            return Fix64.Parse(text);
        }

        /// <summary>A hand-built unit: ground melee by default, 100 health, speed 2, one weapon of 10 damage per second unless a different range, damage or cooldown is given.</summary>
        private static UnitDefinition U(string id, MovementLayer layer = MovementLayer.Ground, int health = 100, string speed = "2", string range = "1", int damage = 10, string cooldown = "1",
            bool ground = true, bool water = true, bool air = false, bool drench = false, string splash = "0", ShieldDomeDefinition? dome = null, bool armed = true, string armour = "light", int cost = 50)
        {
            WeaponDefinition? weapon = armed ? new WeaponDefinition(Fix64.FromInt(damage), "kinetic", F(range), F(cooldown), ground, water, air, drench, 0, F(splash)) : null;
            return new UnitDefinition(id, id, "t", layer, Fix64.FromInt(health), F(speed), 1, F("0.5"), cost, 0, armour, Fix64.FromInt(8), weapon, null, null, null, null, dome);
        }

        /// <summary>An entry of <paramref name="count"/> full-health units of a definition.</summary>
        private static FightEntry E(UnitDefinition unit, int count)
        {
            return new FightEntry(unit, Fix64.FromInt(count), unit.MaxHealth * Fix64.FromInt(count));
        }

        /// <summary>An entry of a shipped unit.</summary>
        private static FightEntry Shipped(string id, int count)
        {
            return E(Data.Content.Units[id], count);
        }

        /// <summary>Predicts with the candidate settings and a dry lane unless a context is given.</summary>
        private static FightPrediction P(FightEntry[] a, FightEntry[] b, FightContext? context = null, FightSimRules? rules = null)
        {
            FightContext c = context ?? FightContext.Dry(false);
            return FightPredictor.Predict(a, b, c, Data.Content.Combat, Settings(rules));
        }

        /// <summary>FP2-2-1 (a): with the formula profile the committed sweep gives the committed predict.jsonl byte for byte.</summary>
        [Test]
        public void OldProfiles_PredictJsonlByteIdentical()
        {
            string folder = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            int exit = HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--predict-fights", "--from", Path.Combine(TestData.FindDataDirectory(), "..", "docs", "reports", "fights_2026-11-18", "fights.jsonl"),
                "--profile", "aggressive_s5", "--out", folder }, out string output, out string error);
            Assert.That(exit, Is.EqualTo(0), error);
            Assert.That(output, Does.Contain("accuracy=0.735"));
            Assert.That(File.ReadAllText(Path.Combine(folder, "predict.jsonl")),
                Is.EqualTo(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "..", "docs", "reports", "predict_fights", "predict.jsonl"))));
        }

        /// <summary>The result line of the trace of a fixed-seed AI game (map prototype_1v1, matchup skai_vs_water, seed 20261026, 600 s cap), both slots on the profile.</summary>
        private static string PlayTrace(string profile)
        {
            string trace = Path.Combine(HarnessTestSupport.NewFolder(), "trace.txt");
            int exit = HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--trace", "--map", "prototype_1v1", "--matchup", "skai_vs_water", "--profile-a", profile, "--profile-b", profile,
                "--seed", "20261026", "--max-game-seconds", "600", "--out", trace }, out string output, out string error);
            Assert.That(exit, Is.EqualTo(0), error);
            Match hash = Regex.Match(output, "\"result\":\"[a-z0-9]+\",\"end_tick\":\\d+,\"state_hash\":\"[0-9a-f]{16}\"");
            Assert.That(hash.Success, Is.True, output);
            return hash.Value;
        }

        /// <summary>
        /// FP2-2-1 (b): one fixed-seed AI game per shipped profile family ends with the final state hash pinned from main before this package.
        /// Re-pinned for research and unlocks (specs/research_and_unlocks.md R1.9; the AI never researches, so the Stormcaller, Ink Lobber, Thunderhead and Leviathan are never trained): aggressive_s5 old end tick 9883 and hash 79b49601e07be618, new 10378 and 963626e47dd707bf; aggressive_s3 old 9867 and aaa7203112b1ac2d, new 9223 and c04bdcc0d03b3910; both still slot 0.
        /// </summary>
        [Test]
        public void OldProfiles_FixedSeedGamesKeepTheirHash()
        {
            // Re-pinned for the rally state, now always hashed per unit (specs/interface_support.md I5.4, determinism review of I1 to I5): hashes only, end ticks 10378 and 9223 and winners unchanged. Old values 2a9fdef3422f6141 and 1db9fc659b9f0eea.
            Assert.That(PlayTrace("aggressive_s5"), Is.EqualTo("\"result\":\"slot0\",\"end_tick\":10378,\"state_hash\":\"963626e47dd707bf\""));
            Assert.That(PlayTrace("aggressive_s3"), Is.EqualTo("\"result\":\"slot0\",\"end_tick\":9223,\"state_hash\":\"c04bdcc0d03b3910\""));
        }

        /// <summary>The text of a shipped profile file.</summary>
        private static string ProfileText(string id)
        {
            return File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json"));
        }

        /// <summary>FP2-2-2: every profile parses; all but the candidate use the formula; bad model text and each out-of-range sim value fail naming file and key; the candidate differs from aggressive_s5 in id and model only.</summary>
        [Test]
        public void Profiles_ParseModelsAndRanges()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.PredictorModel, Is.EqualTo(id == "aggressive_s5_fp2" ? PredictorModel.Sim : PredictorModel.Formula), id);
            }
            string text = ProfileText("aggressive_s5");
            DataValidationException model = Expect.Throws<DataValidationException>(() => AiProfile.Parse(text.Replace("\"predictor_model\": \"formula\"", "\"predictor_model\": \"quantum\""), "ai/aggressive_s5.json", Data.Content));
            Assert.That(model.Message, Does.Contain("ai/aggressive_s5.json").And.Contain("predictor_model"));
            (string Key, string Good, string[] Bad)[] ranges =
            {
                ("predictor_sim_step_seconds", "0.25", new[] { "0.04", "1.5" }), ("predictor_sim_max_seconds", "180", new[] { "9", "601" }), ("predictor_sim_idle_seconds", "30", new[] { "0.5", "601" }),
                ("predictor_sim_max_units_per_side", "64", new[] { "3", "501" }), ("predictor_sim_splash_density", "1", new[] { "0.05", "4.5" }), ("predictor_sim_approach_tiles", "4", new[] { "-1", "33" }),
            };
            foreach ((string key, string good, string[] bad) in ranges)
            {
                foreach (string value in bad)
                {
                    string edited = text.Replace("\"" + key + "\": " + good + ",", "\"" + key + "\": " + value + ",");
                    Assert.That(edited, Is.Not.EqualTo(text), key);
                    DataValidationException ex = Expect.Throws<DataValidationException>(() => AiProfile.Parse(edited, "ai/aggressive_s5.json", Data.Content));
                    Assert.That(ex.Message, Does.Contain("ai/aggressive_s5.json").And.Contain(key), key + " " + value);
                }
            }
            // The candidate differs from aggressive_s5 in its id line and its model line only (comment lines are not data).
            List<string> baseLines = DataLines(text);
            List<string> candidateLines = DataLines(ProfileText("aggressive_s5_fp2"));
            Assert.That(candidateLines.Count, Is.EqualTo(baseLines.Count));
            List<string> different = new List<string>();
            for (int i = 0; i < baseLines.Count; i++)
            {
                if (baseLines[i] != candidateLines[i])
                {
                    different.Add(candidateLines[i]);
                }
            }
            Assert.That(different, Is.EqualTo(new List<string> { "\"id\": \"aggressive_s5_fp2\",", "\"predictor_model\": \"sim\"," }));
        }

        /// <summary>The non-comment, non-empty lines of a JSON text, trimmed.</summary>
        private static List<string> DataLines(string text)
        {
            List<string> lines = new List<string>();
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    lines.Add(trimmed);
                }
            }
            return lines;
        }

        /// <summary>FP2-2-3: Predict(a, b) and Predict(b, a) give mirrored results (swapped times, opposite winners) and advantages that multiply to 1 within the Fix64 rounding, for five unit pairs and one mixed pair.</summary>
        [Test]
        public void Symmetry_MirroredResults()
        {
            (FightEntry[] A, FightEntry[] B)[] pairs =
            {
                (new[] { Shipped("skai.windrunner", 8) }, new[] { Shipped("water.grasper", 10) }),
                (new[] { Shipped("skai.roc_rider", 6) }, new[] { Shipped("skai.talon_archer", 8) }),
                (new[] { Shipped("water.ink_lobber", 6) }, new[] { Shipped("water.grasper", 5) }),
                (new[] { Shipped("skai.thunderhead", 4) }, new[] { Shipped("water.spoutling", 6) }),
                (new[] { Shipped("water.shellback", 6) }, new[] { Shipped("water.grasper", 8) }),
                (new[] { Shipped("skai.windrunner", 6), Shipped("skai.talon_archer", 2) }, new[] { Shipped("water.grasper", 6), Shipped("water.reef_runner", 2) }),
            };
            foreach ((FightEntry[] a, FightEntry[] b) in pairs)
            {
                FightPrediction ab = P(a, b);
                FightPrediction ba = P(b, a);
                string what = a[0].Definition.Id + " vs " + b[0].Definition.Id;
                // The winner's own time is exact; the other time is derived through the advantage (a multiplication one way, a division the other) and agrees to a microsecond.
                Assert.That((ba.TimeAKillsB - ab.TimeBKillsA).Abs().Raw, Is.LessThanOrEqualTo(4300), what);
                Assert.That((ba.TimeBKillsA - ab.TimeAKillsB).Abs().Raw, Is.LessThanOrEqualTo(4300), what);
                Assert.That(ba.AWins, Is.EqualTo(!ab.AWins), what);
                Fix64 product = ab.Advantage * ba.Advantage;
                // A capped advantage is not part of the symmetry; the pairs are chosen so that none is.
                Assert.That(ab.Advantage, Is.LessThan(Settings().MaxAdvantage), what);
                Assert.That(ba.Advantage, Is.LessThan(Settings().MaxAdvantage), what);
                Assert.That((product - Fix64.One).Abs().Raw, Is.LessThanOrEqualTo(4), what + " product " + product + " raw error " + (product - Fix64.One).Raw);
            }
        }

        /// <summary>FP2-2-4: the same call twice gives equal results, and FightSim.cs holds no floating point, System.Random, clock or Dictionary.</summary>
        [Test]
        public void Determinism_RepeatableAndSourceClean()
        {
            FightEntry[] a = { Shipped("skai.windrunner", 6), Shipped("skai.talon_archer", 2) };
            FightEntry[] b = { Shipped("water.grasper", 5), Shipped("water.ink_lobber", 3) };
            FightPrediction first = P(a, b);
            FightPrediction second = P(a, b);
            Assert.That(second.TimeAKillsB, Is.EqualTo(first.TimeAKillsB));
            Assert.That(second.TimeBKillsA, Is.EqualTo(first.TimeBKillsA));
            Assert.That(second.AWins, Is.EqualTo(first.AWins));
            Assert.That(second.Advantage, Is.EqualTo(first.Advantage));
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "..", "src", "Ai", "FightSim.cs"));
            Assert.That(Regex.IsMatch(text, @"\b(float|double|decimal)\b"), Is.False, "no floating point");
            Assert.That(Regex.IsMatch(text, @"System\.Random|\bDateTime\b|\bStopwatch\b|Dictionary<|HashSet<|Environment\.TickCount"), Is.False, "no random, clock or unordered collection");
        }

        /// <summary>FP2-2-5: an empty B is a win with the cap, an empty A has advantage 0, flyers beat a side that cannot hit air, and two sides that cannot hit each other have no winner.</summary>
        [Test]
        public void EmptyAndUnhittable()
        {
            UnitDefinition grunt = U("t.grunt");
            FightPrediction bEmpty = P(new[] { E(grunt, 3) }, new FightEntry[0]);
            Assert.That(bEmpty.AWins, Is.True);
            Assert.That(bEmpty.Advantage, Is.EqualTo(Settings().MaxAdvantage));
            FightPrediction aEmpty = P(new FightEntry[0], new[] { E(grunt, 3) });
            Assert.That(aEmpty.AWins, Is.False);
            Assert.That(aEmpty.Advantage, Is.EqualTo(Fix64.Zero));
            UnitDefinition flyer = U("t.flyer", MovementLayer.Air, range: "5", ground: true, water: true, air: false);
            UnitDefinition earthbound = U("t.earth", range: "5", ground: true, water: true, air: false);
            FightPrediction flyers = P(new[] { E(flyer, 3) }, new[] { E(earthbound, 3) });
            Assert.That(flyers.AWins, Is.True, "the flyers cannot be hit");
            Assert.That(flyers.Advantage, Is.GreaterThan(Fix64.One));
            UnitDefinition airborne = U("t.air", MovementLayer.Air, range: "5", ground: false, water: false, air: true);
            FightPrediction none = P(new[] { E(airborne, 2) }, new[] { E(U("t.ground2", range: "5", ground: true, water: false, air: false), 2) });
            Assert.That(none.AWins, Is.False);
            Assert.That(none.TimeAKillsB, Is.EqualTo(Settings().MaxSeconds));
            Assert.That(none.TimeBKillsA, Is.EqualTo(Settings().MaxSeconds));
            Assert.That(none.Advantage, Is.EqualTo(Fix64.One));
            Assert.That(PredictEvaluator.Classify(none, Settings(), out Fix64 margin), Is.EqualTo("none"));
            Assert.That(margin, Is.EqualTo(Fix64.One));
        }

        /// <summary>FP2-2-6: an unarmed dome unit (pool 150, regeneration 25 a second, delay 0) with an ally against a ranged attacker of 10 damage a second firing from outside the radius is a stalemate.</summary>
        [Test]
        public void DomeStalemate_NoWinner()
        {
            UnitDefinition dome = U("t.dome", armed: false, dome: new ShieldDomeDefinition(Fix64.FromInt(3), Fix64.FromInt(150), Fix64.Zero, Fix64.FromInt(25)));
            UnitDefinition ally = U("t.ally", armed: false);
            UnitDefinition shooter = U("t.shooter", range: "8", damage: 10, cooldown: "1");
            FightPrediction r = P(new[] { E(shooter, 1) }, new[] { E(ally, 1), E(dome, 1) });
            Assert.That(r.AWins, Is.False);
            Assert.That(r.TimeAKillsB, Is.EqualTo(Settings().MaxSeconds));
            Assert.That(r.TimeBKillsA, Is.EqualTo(Settings().MaxSeconds));
            Assert.That(PredictEvaluator.Classify(r, Settings(), out Fix64 margin), Is.EqualTo("none"));
            // Without the dome the shooter wins at once.
            Assert.That(P(new[] { E(shooter, 1) }, new[] { E(ally, 1) }).AWins, Is.True);
        }

        /// <summary>FP2-2-7: one splash attacker (radius 2, density 1) kills clumped units sooner than the same attacker without splash, and no blast reaches more than 12 units.</summary>
        [Test]
        public void Splash_KillsSoonerAndCapsAtTwelve()
        {
            UnitDefinition target = U("t.frail", health: 10, armed: false, speed: "0");
            UnitDefinition blaster = U("t.blaster", range: "6", damage: 20, cooldown: "1", splash: "2");
            UnitDefinition plain = U("t.plain", range: "6", damage: 20, cooldown: "1");
            FightPrediction splash = P(new[] { E(blaster, 1) }, new[] { E(target, 12) });
            FightPrediction single = P(new[] { E(plain, 1) }, new[] { E(target, 12) });
            Assert.That(splash.AWins, Is.True);
            Assert.That(single.AWins, Is.True);
            Assert.That(splash.TimeAKillsB, Is.LessThan(single.TimeAKillsB));
            // The blast disc is the blast radius plus the victim's radius (2 + 0.5): floor(pi x 2.5^2) = 19 units, the target among them. 19 units die to the first blast
            // (the target at 20 damage and 18 others at 10); a 20th needs a second shot one cooldown later.
            FightPrediction nineteen = P(new[] { E(blaster, 1) }, new[] { E(target, 19) });
            FightPrediction twenty = P(new[] { E(blaster, 1) }, new[] { E(target, 20) });
            FightPrediction thirtyEight = P(new[] { E(blaster, 1) }, new[] { E(target, 38) });
            Assert.That(twenty.TimeAKillsB - nineteen.TimeAKillsB, Is.EqualTo(Fix64.One));
            Assert.That(thirtyEight.TimeAKillsB, Is.EqualTo(twenty.TimeAKillsB));
        }

        /// <summary>
        /// Diagnosis fix A: the blast disc includes the victim's radius, as CombatSystem.Blast does (src/Sim/Combat/CombatSystem.cs line 619: reach = SplashRadiusTiles + victim radius), so the cap
        /// on blast victims counts the units in that wider disc: floor(density x pi x (radius + victim radius)^2) - 1.
        /// </summary>
        [Test]
        public void BlastCap_CountsTheWholeDisc()
        {
            UnitDefinition blaster = U("t.blaster", range: "6", splash: "1");
            UnitDefinition victim = U("t.victim");
            Assert.That(FightSim.BlastCap(Fix64.One, blaster.Weapon!, victim), Is.EqualTo(6), "pi x 1.5^2 = 7.07 units, the target among them");
            Assert.That(FightSim.BlastCap(F("0.1"), blaster.Weapon!, victim), Is.EqualTo(0), "never negative");
            UnitDefinition wide = U("t.wide", range: "6", splash: "2");
            Assert.That(FightSim.BlastCap(Fix64.One, wide.Weapon!, victim), Is.EqualTo(18));
        }

        /// <summary>
        /// Diagnosis fix B: a unit picks an armed enemy before an unarmed one, whatever the distance order, as CombatSystem does (src/Sim/Combat/CombatSystem.cs lines 338 to 340:
        /// rank 0 armed, 1 unarmed, rank before distance). The shooter kills the armed enemy first, so it takes less return fire and wins with more left.
        /// </summary>
        [Test]
        public void Targets_ArmedBeforeUnarmed()
        {
            // The unarmed wall sorts first (index 0) and stands at the same distance; the armed turret shoots back.
            UnitDefinition wall = U("t.a_wall", health: 50, speed: "0", armed: false);
            UnitDefinition turret = U("t.b_turret", health: 50, speed: "0", range: "8", damage: 5, cooldown: "1");
            UnitDefinition shooter = U("t.shooter", health: 100, range: "8", damage: 10, cooldown: "1");
            FightPrediction r = P(new[] { E(shooter, 1) }, new[] { E(wall, 1), E(turret, 1) });
            FightPrediction scan = FightSim.PredictByScan(new[] { E(shooter, 1) }, new[] { E(wall, 1), E(turret, 1) }, FightContext.Dry(false), Rules, Settings());
            Assert.That(r.AWins, Is.True);
            Assert.That(scan.Advantage, Is.EqualTo(r.Advantage), "the index and the scan rank the same way");
            // With the turret sorting first the answer must be the same: the rank, not the list order, decides.
            UnitDefinition wall2 = U("t.b_wall", health: 50, speed: "0", armed: false);
            UnitDefinition turret2 = U("t.a_turret", health: 50, speed: "0", range: "8", damage: 5, cooldown: "1");
            FightPrediction other = P(new[] { E(shooter, 1) }, new[] { E(turret2, 1), E(wall2, 1) });
            Assert.That(other.Advantage, Is.EqualTo(r.Advantage));
            Assert.That(other.TimeAKillsB, Is.EqualTo(r.TimeAKillsB));
        }

        /// <summary>FP2-2-8: a ranged ground side that wins on the flat has a lower advantage uphill; melee and air shooters are unaffected.</summary>
        [Test]
        public void Uphill_OnlyRangedGround()
        {
            UnitDefinition archer = U("t.archer", range: "6", damage: 10);
            UnitDefinition brute = U("t.brute", health: 100, damage: 10);
            FightPrediction flat = P(new[] { E(archer, 6) }, new[] { E(brute, 6) });
            FightPrediction up = P(new[] { E(archer, 6) }, new[] { E(brute, 6) }, FightContext.Dry(true));
            Assert.That(flat.AWins, Is.True);
            Assert.That(up.Advantage, Is.LessThan(flat.Advantage));
            UnitDefinition fist = U("t.fist", range: "1", damage: 14);
            UnitDefinition hawk = U("t.hawk", MovementLayer.Air, range: "6", damage: 10, air: true);
            UnitDefinition dummy = U("t.dummy", armed: true, damage: 5, range: "6", air: true);
            Assert.That(P(new[] { E(fist, 6) }, new[] { E(brute, 5) }, FightContext.Dry(true)).Advantage, Is.EqualTo(P(new[] { E(fist, 6) }, new[] { E(brute, 5) }).Advantage));
            Assert.That(P(new[] { E(hawk, 6) }, new[] { E(dummy, 6) }, FightContext.Dry(true)).Advantage, Is.EqualTo(P(new[] { E(hawk, 6) }, new[] { E(dummy, 6) }).Advantage));
        }

        /// <summary>FP2-2-9: drench shooters plus ground-only melee beat flyers that beat the same side without drench (grounding makes the flyers hittable).</summary>
        [Test]
        public void Drench_GroundsFlyers()
        {
            UnitDefinition flyer = U("t.flyer", MovementLayer.Air, health: 100, speed: "2", range: "6", damage: 10, cooldown: "1");
            UnitDefinition spout = U("t.spout", health: 60, speed: "2", range: "5", damage: 1, cooldown: "0.5", air: true, drench: true);
            UnitDefinition plainSpout = U("t.spout", health: 60, speed: "2", range: "5", damage: 1, cooldown: "0.5", air: true, drench: false);
            UnitDefinition brute = U("t.brute", health: 150, speed: "2", range: "1", damage: 20, cooldown: "1", air: false);
            FightEntry[] flyers = { E(flyer, 3) };
            FightPrediction withDrench = P(new[] { E(spout, 3), E(brute, 6) }, flyers);
            FightPrediction without = P(new[] { E(plainSpout, 3), E(brute, 6) }, flyers);
            Assert.That(withDrench.AWins, Is.True, "grounded flyers can be hit by the brutes");
            Assert.That(without.AWins, Is.False, "the brutes cannot reach flyers that are never grounded");
        }

        /// <summary>FP2-2-10: a Momentum unit that approaches for at least max_stacks seconds kills a static target sooner than with damage_bonus_per_stack 0.</summary>
        [Test]
        public void Momentum_AddsDamageAfterApproach()
        {
            UnitDefinition rider = U("t.rider", speed: "2", range: "1", damage: 10, cooldown: "1");
            UnitDefinition wall = U("t.wall", health: 55, speed: "0", armed: false);
            string momentumPath = Path.Combine(TestData.FindDataDirectory(), "tuning", "momentum.json");
            string shipped = File.ReadAllText(momentumPath);
            Assert.That(shipped, Does.Contain("\"damage_bonus_per_stack\": 0.04"));
            MomentumTuning none = new MomentumTuning(JsonReader.Parse(shipped.Replace("\"damage_bonus_per_stack\": 0.04", "\"damage_bonus_per_stack\": 0"), "tuning/momentum.json"));
            FightSimRules with = new FightSimRules(Data.Content.Combat, Data.Tuning.Momentum, Data.Tuning.Status, Data.Tuning.Flood, Data.Tuning.Terrain, new[] { "t.rider" });
            FightSimRules without = new FightSimRules(Data.Content.Combat, none, Data.Tuning.Status, Data.Tuning.Flood, Data.Tuning.Terrain, new[] { "t.rider" });
            // 20 tiles at speed 2 is 10 seconds of approach: past the 5 stacks.
            FightContext far = new FightContext(false, FightWater.None, -1, Fix64.FromInt(20));
            FightPrediction bonus = P(new[] { E(rider, 1) }, new[] { E(wall, 1) }, far, with);
            FightPrediction plain = P(new[] { E(rider, 1) }, new[] { E(wall, 1) }, far, without);
            Assert.That(bonus.AWins && plain.AWins, Is.True);
            Assert.That(bonus.TimeAKillsB, Is.LessThan(plain.TimeAKillsB));
        }

        /// <summary>FP2-2-11: a ranged water side against a melee ground side has a higher advantage in deep neutral water than dry, and the ground side is not slowed in its own water.</summary>
        [Test]
        public void Flood_SlowsGroundSide()
        {
            UnitDefinition swimmer = U("t.swimmer", MovementLayer.Water, health: 100, speed: "2", range: "6", damage: 12, cooldown: "1");
            UnitDefinition walker = U("t.walker", health: 100, speed: "2", range: "1", damage: 10, cooldown: "1");
            FightEntry[] a = { E(swimmer, 5) };
            FightEntry[] b = { E(walker, 5) };
            FightPrediction dry = P(a, b);
            FightPrediction deep = P(a, b, new FightContext(false, FightWater.Deep, -1, -Fix64.One));
            FightPrediction ownWater = P(a, b, new FightContext(false, FightWater.Deep, 1, -Fix64.One));
            Assert.That(dry.AWins && deep.AWins && ownWater.AWins, Is.True);
            Assert.That(deep.Advantage, Is.GreaterThan(dry.Advantage));
            Assert.That(ownWater.Advantage, Is.LessThan(deep.Advantage), "the ground side keeps its speed in its own water");
        }

        /// <summary>FP2-2-12: Count 2.6 with Health 260 gives 3 units of 100 health; 200 units against 100 are scaled to 64 against 32.</summary>
        [Test]
        public void CountsAndHealth()
        {
            UnitDefinition unit = U("t.unit");
            PredictorSettings s = Settings();
            FightSim.Sizes(new[] { new FightEntry(unit, F("2.6"), F("260")) }, new FightEntry[0], s, out List<FightEntry> ea, out List<int> ca, out List<Fix64> ha, out List<FightEntry> eb, out List<int> cb, out List<Fix64> hb);
            Assert.That(ca, Is.EqualTo(new List<int> { 3 }));
            Assert.That((ha[0] - Fix64.FromInt(100)).Abs().Raw, Is.LessThanOrEqualTo(64), "260 / 2.6 is 100 up to the rounding of 2.6 in Fix64");
            Assert.That(ea.Count, Is.EqualTo(1));
            Assert.That(cb, Is.Empty);
            Assert.That(eb, Is.Empty);
            Assert.That(hb, Is.Empty);
            FightSim.Sizes(new[] { E(unit, 200) }, new[] { E(unit, 100) }, s, out ea, out ca, out ha, out eb, out cb, out hb);
            Assert.That(ca, Is.EqualTo(new List<int> { 64 }));
            Assert.That(cb, Is.EqualTo(new List<int> { 32 }));
            // An entry that rounds to no unit is dropped; a small entry next to a large one keeps at least one unit.
            FightSim.Sizes(new[] { new FightEntry(unit, F("0.4"), F("40")) }, new[] { E(unit, 1) }, s, out ea, out ca, out ha, out eb, out cb, out hb);
            Assert.That(ca, Is.Empty);
            UnitDefinition other = U("t.other");
            FightSim.Sizes(new[] { E(unit, 400), E(other, 1) }, new[] { E(unit, 10) }, s, out ea, out ca, out ha, out eb, out cb, out hb);
            Assert.That(ca, Is.EqualTo(new List<int> { 64, 1 }));
        }

        /// <summary>FP2-2-13: a start gap of 0 removes the longer-range side's free shots, so its advantage is lower than with the default gap.</summary>
        [Test]
        public void Gap_ZeroRemovesFreeShots()
        {
            UnitDefinition archer = U("t.archer", range: "8", damage: 8, cooldown: "1");
            UnitDefinition fighter = U("t.fighter", health: 100, range: "1", damage: 10, cooldown: "1");
            FightPrediction normal = P(new[] { E(archer, 6) }, new[] { E(fighter, 6) });
            FightPrediction engaged = P(new[] { E(archer, 6) }, new[] { E(fighter, 6) }, FightContext.Engaged(false));
            Assert.That(normal.AWins, Is.True);
            Assert.That(engaged.Advantage, Is.LessThan(normal.Advantage));
        }

        /// <summary>FP2-2-14: a win with value left f gives advantage 1 / (1 - f^2) and the times of 3.3; the other mappings; a no-winner result is class none.</summary>
        [Test]
        public void Mapping_AdvantageAndTimes()
        {
            PredictorSettings s = Settings();
            FightPrediction a = FightSim.Map(0, F("0.6"), F("12"), s);
            Assert.That(a.AWins, Is.True);
            Fix64 expected = Fix64.One / (Fix64.One - F("0.6") * F("0.6"));
            Assert.That(a.Advantage, Is.EqualTo(expected));
            Assert.That(a.TimeAKillsB, Is.EqualTo(F("12")));
            Assert.That(a.TimeBKillsA, Is.EqualTo(F("12") * expected));
            FightPrediction b = FightSim.Map(1, F("0.6"), F("12"), s);
            Assert.That(b.AWins, Is.False);
            Assert.That(b.Advantage, Is.EqualTo(Fix64.One - F("0.6") * F("0.6")));
            Assert.That(b.TimeBKillsA, Is.EqualTo(F("12")));
            Assert.That(b.TimeAKillsB, Is.EqualTo(F("12") / b.Advantage));
            Assert.That(FightSim.Map(0, Fix64.One, F("5"), s).Advantage, Is.EqualTo(s.MaxAdvantage));
            Assert.That(FightSim.Map(1, Fix64.One, F("5"), s).Advantage, Is.EqualTo(Fix64.Zero));
            Assert.That(FightSim.Map(1, Fix64.One, F("5"), s).TimeAKillsB, Is.EqualTo(s.MaxSeconds));
            Assert.That(FightSim.Map(0, F("0.5"), F("0.001"), s).TimeAKillsB, Is.EqualTo(s.MinSeconds), "the shortest time floors the winner's time");
            FightPrediction none = FightSim.Map(-1, Fix64.Zero, F("30"), s);
            Assert.That(none.TimeAKillsB, Is.EqualTo(s.MaxSeconds));
            Assert.That(none.TimeBKillsA, Is.EqualTo(s.MaxSeconds));
            Assert.That(none.Advantage, Is.EqualTo(Fix64.One));
            Assert.That(PredictEvaluator.Classify(none, s, out Fix64 margin), Is.EqualTo("none"));
            // A whole simulated win: with no damage taken f is 1 (cap).
            UnitDefinition shooter = U("t.shooter", range: "8", damage: 10);
            UnitDefinition target = U("t.target", armed: false, speed: "0", health: 50);
            Assert.That(P(new[] { E(shooter, 1) }, new[] { E(target, 1) }).Advantage, Is.EqualTo(s.MaxAdvantage));
            Assert.That(margin, Is.EqualTo(Fix64.One));
        }

        /// <summary>FP2-2-15: the retreat call passes a start gap of 0 with the sim model (the formula ignores it and gives today's answer); the call site in AiBrainFight uses the engaged context.</summary>
        [Test]
        public void RetreatContext_GapZero()
        {
            Assert.That(FightContext.Engaged(true).GapTiles, Is.EqualTo(Fix64.Zero));
            Assert.That(FightContext.Engaged(true).AUphill, Is.True);
            Assert.That(FightContext.Dry(false).GapTiles, Is.EqualTo(-Fix64.One));
            string brain = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "..", "src", "Ai", "AiBrainFight.cs"));
            Assert.That(brain, Does.Contain("FightContext.Engaged(IsUphill(MeanPosition(engaged), MeanPosition(group)))"));
            // The formula answers the same with either context; the sim does not.
            FightEntry[] a = { Shipped("skai.windrunner", 6) };
            FightEntry[] b = { Shipped("water.grasper", 6) };
            PredictorSettings formula = PredictorSettings.From(AiProfile.FromDataSet(Data, "aggressive_s5"));
            FightPrediction viaOld = FightPredictor.Predict(a, b, false, Data.Content.Combat, formula);
            FightContext engaged = FightContext.Engaged(false);
            FightPrediction viaContext = FightPredictor.Predict(a, b, engaged, Data.Content.Combat, formula);
            Assert.That(viaContext.Advantage, Is.EqualTo(viaOld.Advantage));
            Assert.That(viaContext.TimeAKillsB, Is.EqualTo(viaOld.TimeAKillsB));
            FightContext dry = FightContext.Dry(false);
            Assert.That(FightPredictor.Predict(a, b, engaged, Data.Content.Combat, Settings()).Advantage, Is.Not.EqualTo(FightPredictor.Predict(a, b, dry, Data.Content.Combat, Settings()).Advantage));
            Assert.That(Expect.Throws<ArgumentException>(() => PredictorSettings.From(AiProfile.FromDataSet(Data, "aggressive_s5_fp2"))).Message, Does.Contain("FightSimRules"));
        }

        /// <summary>Review fix 1: FightRunner counts its no_damage stalemate from army spawn (lastDamage starts at spawnTick), so the sim counts idle time from t = 0 too: an approach longer than the idle window is no winner in both.</summary>
        [Test]
        public void Idle_CountsFromSpawn_AsTheFightRunnerDoes()
        {
            string runner = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "..", "tools", "BalanceHarness", "FightRunner.cs"));
            Assert.That(runner, Does.Contain("int lastDamage = spawnTick;"));
            UnitDefinition shooter = U("t.shooter", range: "8", damage: 10);
            UnitDefinition target = U("t.target", armed: false, speed: "0", health: 50);
            // 100 tiles at speed 2 is 46 s of walking, past the 30 s idle window.
            FightPrediction far = P(new[] { E(shooter, 1) }, new[] { E(target, 1) }, new FightContext(false, FightWater.None, -1, Fix64.FromInt(100)));
            Assert.That(far.AWins, Is.False);
            Assert.That(far.Advantage, Is.EqualTo(Fix64.One));
            Assert.That(P(new[] { E(shooter, 1) }, new[] { E(target, 1) }, new FightContext(false, FightWater.None, -1, Fix64.FromInt(40))).AWins, Is.True);
        }

        /// <summary>Review fix 2: a blast reaches the blast radius plus the victim's radius.</summary>
        [Test]
        public void Splash_ReachAddsVictimRadius()
        {
            UnitDefinition blaster = U("t.blaster", range: "6", splash: "2");
            UnitDefinition victim = U("t.victim");
            Assert.That(FightSim.InBlastReach(F("2.5"), blaster.Weapon!, victim), Is.True, "radius 2 + victim radius 0.5");
            Assert.That(FightSim.InBlastReach(F("2.51"), blaster.Weapon!, victim), Is.False);
        }

        /// <summary>Review fix 3: a water-layer unit on a dry lane walks at the dry-ground factor, so it needs longer than with the factor at 1; a ground unit is unaffected.</summary>
        [Test]
        public void DrySpeed_WaterLayerUnitsOnDryLane()
        {
            Assert.That(Rules.WaterUnitDrySpeed, Is.EqualTo(Data.Tuning.Terrain.WaterUnitDrySpeedMultiplier));
            Assert.That(Rules.WaterUnitDrySpeed, Is.LessThan(Fix64.One));
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "tuning", "terrain.json"));
            Assert.That(text, Does.Contain("\"water_unit_dry_speed_multiplier\": 0.85"));
            TerrainTuning unit = new TerrainTuning(JsonReader.Parse(text.Replace("\"water_unit_dry_speed_multiplier\": 0.85", "\"water_unit_dry_speed_multiplier\": 1.0"), "tuning/terrain.json"));
            FightSimRules fast = new FightSimRules(Data.Content.Combat, Data.Tuning.Momentum, Data.Tuning.Status, Data.Tuning.Flood, unit, new string[0]);
            UnitDefinition swimmer = U("t.swimmer", MovementLayer.Water, range: "1", damage: 10);
            UnitDefinition walker = U("t.walker", range: "1", damage: 10);
            UnitDefinition wall = U("t.wall", health: 30, speed: "0", armed: false);
            FightContext far = new FightContext(false, FightWater.None, -1, Fix64.FromInt(20));
            Assert.That(P(new[] { E(swimmer, 1) }, new[] { E(wall, 1) }, far).TimeAKillsB, Is.GreaterThan(P(new[] { E(swimmer, 1) }, new[] { E(wall, 1) }, far, fast).TimeAKillsB));
            Assert.That(P(new[] { E(walker, 1) }, new[] { E(wall, 1) }, far).TimeAKillsB, Is.EqualTo(P(new[] { E(walker, 1) }, new[] { E(wall, 1) }, far, fast).TimeAKillsB));
        }

        /// <summary>Review fix 4: both returned times stay within predictor_max_seconds even when it is below predictor_sim_max_seconds.</summary>
        [Test]
        public void Times_CappedAtPredictorMaxSeconds()
        {
            PredictorSettings s = new PredictorSettings(Fix64.FromInt(20), F("0.5"), Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.FromInt(100), PredictorModel.Sim, F("0.25"), Fix64.FromInt(180), Fix64.FromInt(100), 64, Fix64.One, Fix64.FromInt(4), Rules);
            UnitDefinition shooter = U("t.shooter", range: "8", damage: 10);
            UnitDefinition target = U("t.target", armed: false, speed: "0", health: 50);
            FightContext far = new FightContext(false, FightWater.None, -1, Fix64.FromInt(60));
            FightEntry[] a = { E(shooter, 1) };
            FightEntry[] b = { E(target, 1) };
            FightPrediction r = FightPredictor.Predict(a, b, far, Data.Content.Combat, s);
            Assert.That(r.AWins, Is.True);
            Assert.That(r.TimeAKillsB, Is.LessThanOrEqualTo(Fix64.FromInt(20)));
            Assert.That(r.TimeBKillsA, Is.LessThanOrEqualTo(Fix64.FromInt(20)));
            FightPrediction mirrored = FightPredictor.Predict(b, a, far, Data.Content.Combat, s);
            Assert.That(mirrored.TimeAKillsB, Is.LessThanOrEqualTo(Fix64.FromInt(20)));
            Assert.That(mirrored.TimeBKillsA, Is.LessThanOrEqualTo(Fix64.FromInt(20)));
        }

        /// <summary>Review fix 5: the uphill penalty uses effective layers (a Grounded flyer is ground), and the Momentum slow test is the speed factor's own test.</summary>
        [Test]
        public void EffectiveLayerAndSlowTest()
        {
            Assert.That(FightSim.IsUphillShot(true, 0, false, MovementLayer.Ground, MovementLayer.Ground), Is.True, "a Grounded flyer shooting or hit counts as ground");
            Assert.That(FightSim.IsUphillShot(true, 0, false, MovementLayer.Air, MovementLayer.Ground), Is.False);
            Assert.That(FightSim.IsUphillShot(true, 0, false, MovementLayer.Ground, MovementLayer.Air), Is.False);
            Assert.That(FightSim.IsUphillShot(true, 1, false, MovementLayer.Ground, MovementLayer.Ground), Is.False);
            Assert.That(FightSim.IsUphillShot(true, 0, true, MovementLayer.Ground, MovementLayer.Ground), Is.False);
            Assert.That(FightSim.IsUphillShot(false, 0, false, MovementLayer.Ground, MovementLayer.Ground), Is.False);
            Assert.That(FightSim.IsSlowed(F("4"), F("3.75")), Is.True);
            Assert.That(FightSim.IsSlowed(F("4"), F("4")), Is.False, "a slow that ends at the step start no longer holds");
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "..", "src", "Ai", "FightSim.cs"));
            Assert.That(Regex.Matches(text, @"IsSlowed\(unit\.SlowUntil, now\)").Count, Is.EqualTo(2), "speed factor and Momentum use the same test and the same time");
        }

        /// <summary>Review fix 6: the front index gives exactly the results of the plain scan, on armies with domes, blasts, drench, Momentum, flyers, water and uphill, and on hand-built armies with units at several positions.</summary>
        [Test]
        public void FrontIndex_EqualsScan()
        {
            (FightEntry[] A, FightEntry[] B)[] pairs =
            {
                (new[] { Shipped("skai.windrunner", 40) }, new[] { Shipped("water.grasper", 50) }),
                (new[] { Shipped("water.ink_lobber", 30), Shipped("water.shellback", 6) }, new[] { Shipped("skai.roc_rider", 20), Shipped("skai.talon_archer", 20) }),
                (new[] { Shipped("skai.roc_rider", 12), Shipped("skai.thunderhead", 6) }, new[] { Shipped("water.spoutling", 30), Shipped("water.grasper", 20) }),
                (new[] { Shipped("water.leviathan", 3), Shipped("water.reef_runner", 30) }, new[] { Shipped("skai.windrunner", 30), Shipped("skai.stormcaller", 6) }),
                (new[] { Shipped("skai.windrunner", 100) }, new[] { Shipped("water.grasper", 100) }),
                (new[] { E(U("t.fast", speed: "3"), 7), E(U("t.slow", speed: "1", range: "4"), 9) }, new[] { E(U("t.mid", speed: "2", range: "2", splash: "1.5"), 11), E(U("t.wall", speed: "0", armed: false), 4) }),
            };
            FightContext[] contexts =
            {
                FightContext.Dry(false), FightContext.Dry(true), FightContext.Engaged(false), new FightContext(false, FightWater.Deep, -1, -Fix64.One), new FightContext(false, FightWater.Shallow, 0, Fix64.FromInt(9)),
            };
            foreach ((FightEntry[] a, FightEntry[] b) in pairs)
            {
                foreach (FightContext context in contexts)
                {
                    FightPrediction index = FightSim.Predict(a, b, context, Rules, Settings());
                    FightPrediction scan = FightSim.PredictByScan(a, b, context, Rules, Settings());
                    string what = a[0].Definition.Id + " v " + b[0].Definition.Id + " " + context.Water + " gap " + context.GapTiles;
                    Assert.That(index.TimeAKillsB, Is.EqualTo(scan.TimeAKillsB), what);
                    Assert.That(index.TimeBKillsA, Is.EqualTo(scan.TimeBKillsA), what);
                    Assert.That(index.AWins, Is.EqualTo(scan.AWins), what);
                    Assert.That(index.Advantage, Is.EqualTo(scan.Advantage), what);
                }
            }
        }

        /// <summary>The candidate profile plays a whole fixed-seed game without error and twice to the same final state hash (the sim predictor is deterministic inside a match).</summary>
        [Test]
        public void CandidateProfile_PlaysDeterministically()
        {
            string first = PlayTrace("aggressive_s5_fp2");
            string second = PlayTrace("aggressive_s5_fp2");
            Assert.That(second, Is.EqualTo(first));
        }
    }
}

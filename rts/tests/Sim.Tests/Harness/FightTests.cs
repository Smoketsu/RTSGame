// rts/tests/Sim.Tests/Harness/FightTests.cs
// Acceptance tests of the equal-value fight mode (rts/docs/specs/micro_benchmark.md section 8): settings validation, the plan, the formation, a known fight, the stalemate and cap
// rules, the terrain cases, the determinism and order independence of the sweep, the design-rule checks, the digest guard and --strict. Most tests use a quick sweep (three units,
// one flat case, 6 fights); the sweep tests start worker processes. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Fight mode tests.</summary>
    [TestFixture]
    public class FightTests
    {
        /// <summary>Deletes the temporary data folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>Test 1: each bad edit is exit 1 naming the file and the field; the shipped arena with a 60-tile distance fails at plan time naming the first fight.</summary>
        [Test]
        public void SettingsValidation_NamesFileAndField()
        {
            // (edited file, find text or key, replacement, text the message must contain)
            string comp = "[ { \"id\": \"mix\", \"units\": { \"skai.windrunner\": 1, \"water.grasper\": 1 } } ]";
            string[][] cases =
            {
                new[] { "settings", "cases", "[ { \"id\": \"flat\", \"kind\": \"swamp\", \"army_values\": [1500] } ]", "kind" },
                new[] { "settings", "compositions", comp, "mixes factions" },
                new[] { "settings", "compositions", "[ { \"id\": \"a.b\", \"units\": { \"water.grasper\": 1 } } ]", "id" },
                new[] { "settings", "threats", "[ { \"id\": \"x\", \"side\": \"skai.nothing\" } ]", "side" },
                new[] { "settings", "flood_source_unit", "\"water.grasper\"", "flood_source_unit" },
                new[] { "settings", "rule_army_value", "999", "rule_army_value" },
                new[] { "settings", "rule_case", "\"choke\"", "rule_case" },
                new[] { "settings", "nonsense", "1", "nonsense" },
            };
            foreach (string[] c in cases)
            {
                string folder = FightTestSupport.Quick();
                string path = Path.Combine(folder, FightTestSupport.SettingsPath);
                string text = File.ReadAllText(path);
                if (c[1] == "nonsense")
                {
                    text = text.Replace("\"base_seed\"", "\"nonsense\": 1,\n  \"base_seed\"");
                }
                else if (c[1] == "rule_case")
                {
                    // The quick cases get a second case of kind choke so the name exists but is not flat.
                    text = FightTestSupport.SetKey(text, "cases", "[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500] }, { \"id\": \"choke\", \"kind\": \"choke\", \"army_values\": [1500] } ]");
                    text = FightTestSupport.SetKey(text, c[1], c[2]);
                }
                else
                {
                    text = FightTestSupport.SetKey(text, c[1], c[2]);
                }
                File.WriteAllText(path, text);
                int exit = FightTestSupport.Only(folder, FightTestSupport.QuickFirst, out string output, out string error);
                Assert.That(exit, Is.EqualTo(1), c[1] + ": " + error);
                Assert.That(error, Does.Contain(FightTestSupport.SettingsPath), c[1]);
                Assert.That(error, Does.Contain(c[3]), c[1] + ": " + error);
            }
            // A matchups entry naming a unit that does not exist, and one with an unknown case.
            string badUnit = FightTestSupport.Quick();
            File.WriteAllText(Path.Combine(badUnit, FightTestSupport.MatchupsPath), "{ \"water.nothing\": { \"job\": \"x\", \"wins\": null, \"loses\": null } }");
            int exitUnit = HarnessTestSupport.Run(new[] { "--data", badUnit, "--fights", "--only", FightTestSupport.QuickFirst }, out string o1, out string e1);
            Assert.That(exitUnit, Is.EqualTo(1), e1);
            Assert.That(e1, Does.Contain(FightTestSupport.MatchupsPath));
            Assert.That(e1, Does.Contain("water.nothing"));
            string badCase = FightTestSupport.Quick();
            File.WriteAllText(Path.Combine(badCase, FightTestSupport.MatchupsPath),
                "{ \"water.grasper\": { \"job\": \"x\", \"wins\": { \"against\": \"water.ink_lobber\", \"case\": \"nowhere\" }, \"loses\": null } }");
            int exitCase = HarnessTestSupport.Run(new[] { "--data", badCase, "--fights", "--only", FightTestSupport.QuickFirst }, out string o2, out string e2);
            Assert.That(exitCase, Is.EqualTo(1), e2);
            Assert.That(e2, Does.Contain(FightTestSupport.MatchupsPath));
            Assert.That(e2, Does.Contain("case"));
            // Plan-time error with the shipped arena.
            string far = FightTestSupport.DataWith("start_distance_tiles", "60");
            int exitFar = FightTestSupport.Only(far, "flat/750/skai.roc_rider/skai.stormcaller", out string o3, out string e3);
            Assert.That(exitFar, Is.EqualTo(1), e3);
            Assert.That(e3, Does.Contain("fight flat/750/skai.roc_rider/skai.stormcaller"));
        }

        /// <summary>Test 2: the shipped plan has 13 sides (11 combat units after roster R-1 and R-2, 2 compositions) and 780 fights in the spec's order, with the spec's army counts; mismatch follows max_value_mismatch_fraction.</summary>
        [Test]
        public void Plan_MatchesTheSpec()
        {
            string folder = HarnessTestSupport.CopyData();
            DataSet data = HarnessTestSupport.Load(folder);
            FightSettings settings = FightSettings.Load(folder, data);
            FightPlan plan = FightPlan.Build(data, settings);
            Assert.That(settings.Units, Is.EqualTo(new[]
            {
                "skai.roc_rider", "skai.stormcaller", "skai.talon_archer", "skai.thunderhead", "skai.windrunner", "water.grasper", "water.ink_lobber", "water.leviathan",
                "water.reef_runner", "water.shellback", "water.spoutling",
            }));
            Assert.That(plan.Sides.Count, Is.EqualTo(13));
            Assert.That(plan.Fights.Count, Is.EqualTo(780));
            Assert.That(plan.Fights[0].Id, Is.EqualTo("flat/750/skai.roc_rider/skai.stormcaller"));
            Assert.That(plan.Fights[1].Id, Is.EqualTo("flat/750/skai.stormcaller/skai.roc_rider"));
            FightSpec pummelers = Find(plan, "flat/1500/water.grasper/water.ink_lobber");
            Assert.That(pummelers.Multiplier0, Is.EqualTo(15));
            Assert.That(pummelers.Multiplier1, Is.EqualTo(8));
            FightSpec rocs = Find(plan, "flat/750/skai.roc_rider/water.grasper");
            Assert.That(rocs.Multiplier0, Is.EqualTo(4));
            Assert.That(rocs.Multiplier1, Is.EqualTo(8));
            FightSpec mix = Find(plan, "flat/1500/water_ai_mix/skai_ai_mix");
            Assert.That(mix.Multiplier1, Is.EqualTo(2), "skai_ai_mix at 1500");
            Assert.That(Find(plan, "flat/1500/water_ai_mix/water.grasper").Value0, Is.EqualTo(1580));
            Assert.That(Find(plan, "flat/750/water_ai_mix/water.grasper").Multiplier0, Is.EqualTo(1));
            foreach (FightSpec spec in plan.Fights)
            {
                Assert.That(spec.Mismatch, Is.False, spec.Id);
            }
            string strict = FightTestSupport.DataWith("max_value_mismatch_fraction", "0.01");
            FightPlan strictPlan = FightPlan.Build(HarnessTestSupport.Load(strict), FightSettings.Load(strict, HarnessTestSupport.Load(strict)));
            Assert.That(Find(strictPlan, "flat/750/skai.roc_rider/water.grasper").Mismatch, Is.False);
            Assert.That(Find(strictPlan, "flat/750/water.grasper/water.ink_lobber").Mismatch, Is.True);
        }

        /// <summary>Test 3: the formation of 15 Pummelers and 8 Brinecasters sits at the spec's coordinates.</summary>
        [Test]
        public void Formation_SitsWhereTheSpecSays()
        {
            string folder = HarnessTestSupport.CopyData();
            FightSpec spec = FightTestSupport.Spec(folder, "flat/1500/water.grasper/water.ink_lobber", out DataSet data, out FightSettings settings);
            List<FightSpawn> grasper = FightPlan.Formation(data, settings, spec, 0);
            Assert.That(grasper.Count, Is.EqualTo(15));
            for (int j = 0; j < 10; j++)
            {
                Assert.That(grasper[j].Position.X, Is.EqualTo(Fix64.Parse("16.5")));
                Assert.That(grasper[j].Position.Y, Is.EqualTo(Fix64.Parse("12") + Fix64.FromInt(j)));
            }
            for (int j = 0; j < 5; j++)
            {
                Assert.That(grasper[10 + j].Position.X, Is.EqualTo(Fix64.Parse("15.5")));
                Assert.That(grasper[10 + j].Position.Y, Is.EqualTo(Fix64.Parse("14.5") + Fix64.FromInt(j)));
            }
            List<FightSpawn> lobber = FightPlan.Formation(data, settings, spec, 1);
            Assert.That(lobber.Count, Is.EqualTo(8));
            for (int j = 0; j < 8; j++)
            {
                Assert.That(lobber[j].Position.X, Is.EqualTo(Fix64.Parse("32.5")));
                Assert.That(lobber[j].Position.Y, Is.EqualTo(Fix64.Parse("13") + Fix64.FromInt(j)));
            }
        }

        /// <summary>
        /// Test 4: the known equal-value case, 15 Pummelers against 8 Brinecasters, is decided by elimination with value left, the same side winning from either seat.
        /// Before area damage (combat_mechanics.md package 1, 18 Nov 2026) the Pummelers won; with the Brinecaster's blast (radius 1 tile, placeholder) the Brinecasters win.
        /// </summary>
        [Test]
        public void KnownCase_BrinecastersBeatPummelers()
        {
            string folder = HarnessTestSupport.CopyData();
            FightRecord a = FightTestSupport.Play(folder, "flat/1500/water.grasper/water.ink_lobber").Record;
            Assert.That(a.Result, Is.EqualTo(FightRecord.Seat1Won));
            Assert.That(a.Reason, Is.EqualTo("eliminated"));
            Assert.That(a.Left1, Is.GreaterThan(0.0));
            FightRecord b = FightTestSupport.Play(folder, "flat/1500/water.ink_lobber/water.grasper").Record;
            Assert.That(b.Result, Is.EqualTo(FightRecord.Seat0Won));
            Assert.That(b.Reason, Is.EqualTo("eliminated"));
            Assert.That(b.Left0, Is.GreaterThan(0.0));
            // A record parsed from its own line is the same line again.
            Assert.That(FightRecord.Parse(a.ToJsonLine()).ToJsonLine(), Is.EqualTo(a.ToJsonLine()));
        }

        /// <summary>Test 5: a Roc Rider that can only hit air against Pummelers that cannot hit air ends as a cannot_target stalemate after the 5 s confirmation (end tick 99).</summary>
        [Test]
        public void Stalemate_CannotTarget()
        {
            string folder = FightTestSupport.Quick("units", "[\"skai.roc_rider\", \"water.grasper\"]");
            string unitFile = Path.Combine(folder, "units/skai/roc_rider.json");
            string unitText = File.ReadAllText(unitFile);
            Assert.That(unitText, Does.Contain("\"can_target\": [\"ground\", \"water\", \"air\"]"));
            File.WriteAllText(unitFile, unitText.Replace("\"can_target\": [\"ground\", \"water\", \"air\"]", "\"can_target\": [\"air\"]"));
            FightRecord r = FightTestSupport.Play(folder, "flat/1500/skai.roc_rider/water.grasper").Record;
            Assert.That(r.Result, Is.EqualTo(FightRecord.Stalemate));
            Assert.That(r.Reason, Is.EqualTo("cannot_target"));
            Assert.That(r.EndTick, Is.EqualTo(99));
            Assert.That(r.Left0, Is.EqualTo(1.0));
            Assert.That(r.Left1, Is.EqualTo(1.0));
        }

        /// <summary>Test 6: with a 1 s idle limit the first fight ends as a no_damage stalemate at tick 20 (the armies are 16 tiles apart).</summary>
        [Test]
        public void Stalemate_NoDamage()
        {
            string folder = FightTestSupport.Quick("stalemate_idle_seconds", "1");
            FightRecord r = FightTestSupport.Play(folder, FightTestSupport.QuickFirst).Record;
            Assert.That(r.Result, Is.EqualTo(FightRecord.Stalemate));
            Assert.That(r.Reason, Is.EqualTo("no_damage"));
            Assert.That(r.EndTick, Is.EqualTo(20));
        }

        /// <summary>Test 7: with a 1 s cap and a 30 s idle limit the first fight is unresolved at tick 20.</summary>
        [Test]
        public void Cap_EndsUnresolved()
        {
            string folder = FightTestSupport.Quick("max_fight_seconds", "1", "stalemate_idle_seconds", "30");
            FightRecord r = FightTestSupport.Play(folder, FightTestSupport.QuickFirst).Record;
            Assert.That(r.Result, Is.EqualTo(FightRecord.Unresolved));
            Assert.That(r.Reason, Is.EqualTo("time_cap"));
            Assert.That(r.EndTick, Is.EqualTo(20));
        }

        /// <summary>Test 10: the shipped flood covers at least flood_min_lane_fraction of the lane and no flood source counts as value; with no warm-up the run is a settings error.</summary>
        [Test]
        public void Flooded_FloodsTheLaneAndCountsNoSource()
        {
            string folder = HarnessTestSupport.CopyData();
            FightSpec spec = FightTestSupport.Spec(folder, "flooded/1500/water.grasper/water.ink_lobber", out DataSet data, out FightSettings settings);
            FightRecord r = FightRunner.Run(data, settings, spec, settings.BaseSeed).Record;
            // Lane tiles of the arena.
            int lane = 0;
            for (int y = 0; y < settings.ArenaHeightTiles; y++)
            {
                for (int x = 0; x < settings.ArenaWidthTiles; x++)
                {
                    lane += FightPlan.IsLaneTile(settings, x, y) ? 1 : 0;
                }
            }
            Assert.That(r.FloodedLaneTiles, Is.GreaterThanOrEqualTo((int)Math.Ceiling(settings.FloodMinLaneFraction * lane)));
            Assert.That(r.Value0, Is.EqualTo(1500));
            Assert.That(r.Value1, Is.EqualTo(1520));
            Assert.That(r.Result, Is.EqualTo(FightRecord.Seat0Won));
            string dry = FightTestSupport.DataWith("flood_warmup_seconds", "0");
            int exit = FightTestSupport.Only(dry, "flooded/1500/water.grasper/water.ink_lobber", out string output, out string error);
            Assert.That(exit, Is.EqualTo(1), error);
            Assert.That(error, Does.Contain("flooded/1500/water.grasper/water.ink_lobber"));
            Assert.That(error, Does.Contain("fraction 0.000"));
        }

        /// <summary>Test 11: on high ground seat 1 issues no command, seat 0 does, and the arena has a plateau of height 1 behind a ramp of IsRamp tiles.</summary>
        [Test]
        public void HighGround_SeatOneHoldsOnThePlateau()
        {
            string folder = HarnessTestSupport.CopyData();
            FightSpec spec = FightTestSupport.Spec(folder, "high_ground/1500/water.grasper/water.ink_lobber", out DataSet data, out FightSettings settings);
            FightRun run = FightRunner.Run(data, settings, spec, settings.BaseSeed);
            Assert.That(run.Commands[1], Is.EqualTo(0));
            Assert.That(run.Commands[0], Is.GreaterThan(0));
            MapDefinition arena = FightPlan.BuildArena(settings, spec.Case);
            int f1 = FightPlan.FrontColumn1(settings);
            int cy = FightPlan.CentreY(settings);
            for (int y = 0; y < arena.HeightTiles; y++)
            {
                for (int x = 0; x < arena.WidthTiles; x++)
                {
                    int index = y * arena.WidthTiles + x;
                    Assert.That(arena.HeightAt(index), Is.EqualTo(x >= f1 - 2 ? 1 : 0), "height at " + x + "," + y);
                    bool ramp = x == f1 - 3 && y >= cy - settings.RampWidthTiles / 2 && y < cy - settings.RampWidthTiles / 2 + settings.RampWidthTiles;
                    Assert.That(arena.IsRampAt(index), Is.EqualTo(ramp), "ramp at " + x + "," + y);
                }
            }
        }

        /// <summary>Test 12: the choke wall has height 1 on its columns except the gap rows.</summary>
        [Test]
        public void Choke_WallHasAGap()
        {
            string folder = HarnessTestSupport.CopyData();
            FightSpec spec = FightTestSupport.Spec(folder, "choke/1500/water.grasper/water.ink_lobber", out DataSet data, out FightSettings settings);
            MapDefinition arena = FightPlan.BuildArena(settings, spec.Case);
            int cx = FightPlan.CentreX(settings);
            int cy = FightPlan.CentreY(settings);
            int t = settings.ChokeWallThicknessTiles;
            int g = settings.ChokeWidthTiles;
            for (int y = 0; y < arena.HeightTiles; y++)
            {
                for (int x = 0; x < arena.WidthTiles; x++)
                {
                    bool wallColumn = x >= cx - t / 2 && x < cx - t / 2 + t;
                    bool gap = y >= cy - g / 2 && y < cy - g / 2 + g;
                    Assert.That(arena.HeightAt(y * arena.WidthTiles + x), Is.EqualTo(wallColumn && !gap ? 1 : 0), "height at " + x + "," + y);
                }
            }
        }

        /// <summary>Runs the quick sweep into a new output folder; returns the exit code, stdout and the output folder.</summary>
        private static int Sweep(string folder, int jobs, out string output, out string outDirectory, params string[] more)
        {
            outDirectory = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            List<string> args = new List<string> { "--data", folder, "--fights", "--jobs", jobs.ToString(), "--out", outDirectory };
            args.AddRange(more);
            return HarnessTestSupport.Run(args.ToArray(), out output, out string error);
        }

        /// <summary>The text of the four output files, concatenated with their names, for byte comparisons.</summary>
        private static string Files(string outDirectory)
        {
            string text = string.Empty;
            foreach (string name in new[] { "fights.jsonl", "hq_floor.jsonl", "fights.md" })
            {
                text += "== " + name + "\n" + File.ReadAllText(Path.Combine(outDirectory, name));
            }
            return text;
        }

        /// <summary>Test 8: the quick sweep run twice gives byte-identical files and stdout, and the stdout has the progress lines and the FIGHTS summary.</summary>
        [Test]
        public void Sweep_IsDeterministic()
        {
            string folder = FightTestSupport.Quick("chunk_fights", "4");
            int exit1 = Sweep(folder, 2, out string out1, out string dir1);
            int exit2 = Sweep(folder, 2, out string out2, out string dir2);
            Assert.That(exit1, Is.EqualTo(0), out1);
            Assert.That(exit2, Is.EqualTo(0));
            Assert.That(out2, Is.EqualTo(out1));
            Assert.That(Files(dir2), Is.EqualTo(Files(dir1)));
            Assert.That(out1, Does.Contain("fights 4/6\nfights 6/6\n"));
            Assert.That(out1, Does.Contain("FIGHTS fights=6 decided="));
            // One fights.jsonl line per fight in plan order, and the floor trials of both factions, units and no composition.
            string[] lines = File.ReadAllLines(Path.Combine(dir1, "fights.jsonl"));
            Assert.That(lines.Length, Is.EqualTo(6));
            for (int i = 0; i < 6; i++)
            {
                Assert.That(FightRecord.Parse(lines[i]).Fight, Is.EqualTo(FightTestSupport.QuickIds[i]));
            }
            int floorTrials = HqFloorTrial.Plan(HarnessTestSupport.Load(folder), new[] { "skai_vs_skai", "skai_vs_water", "water_vs_water" }).Count;
            Assert.That(floorTrials, Is.GreaterThan(0));
            Assert.That(File.ReadAllLines(Path.Combine(dir1, "hq_floor.jsonl")).Length, Is.EqualTo(floorTrials), "every armed mobile unit, Rooted and Uprooted, and no composition");
        }

        /// <summary>Test 9: --jobs 1 and --jobs 3 give byte-identical files and stdout.</summary>
        [Test]
        public void Sweep_DoesNotDependOnJobs()
        {
            string folder = FightTestSupport.Quick("chunk_fights", "2");
            int exit1 = Sweep(folder, 1, out string out1, out string dir1);
            int exit3 = Sweep(folder, 3, out string out3, out string dir3);
            Assert.That(exit1, Is.EqualTo(0), out1);
            Assert.That(exit3, Is.EqualTo(0));
            Assert.That(out3, Is.EqualTo(out1));
            Assert.That(Files(dir3), Is.EqualTo(Files(dir1)));
        }

        /// <summary>A composition of the sweep gets HQ floor trials with k = hq_floor_supply / recipe supply (skai_ai_mix, supply 13, gives 2 recipes) and the worker places the recipe in order.</summary>
        [Test]
        public void HqFloor_CompositionsAreAttackers()
        {
            string folder = HarnessTestSupport.CopyData();
            DataSet data = HarnessTestSupport.Load(folder);
            FightSettings settings = FightSettings.Load(folder, data);
            FightPlan plan = FightPlan.Build(data, settings);
            List<string> warnings = new List<string>();
            List<HqFloorSpec> specs = HqFloorTrial.Plan(data, new[] { "skai_vs_skai", "skai_vs_water", "water_vs_water" }, plan.Sides, warnings);
            Assert.That(warnings, Is.Empty);
            Assert.That(specs.FindAll(s => s.Unit == "skai_ai_mix").Count, Is.EqualTo(4), "2 defenders, Rooted and Uprooted");
            Assert.That(specs.FindAll(s => s.Unit == "water_ai_mix").Count, Is.EqualTo(4));
            foreach (string defender in new[] { "skai", "water" })
            {
                HqFloorRecord record = HqFloorTrial.Run(data, new HqFloorSpec("skai", "skai_ai_mix", defender, true), FindRecipe(plan, "skai_ai_mix"));
                Assert.That(record.Count, Is.EqualTo(14), "k 2 of the 7-unit, 13-supply recipe against the " + defender + " HQ");
                Assert.That(record.Unit, Is.EqualTo("skai_ai_mix"));
            }
        }

        /// <summary>A hand-built flat 1500 record with equal start values: the result and the left fractions are the only things that matter to the checks.</summary>
        private static FightRecord Rec(string seat0, string seat1, string result, double left0, double left1, int value0 = 1500, int value1 = 1500)
        {
            return new FightRecord("flat/1500/" + seat0 + "/" + seat1, "flat", 1500, seat0, seat1, new Dictionary<string, int>(), new Dictionary<string, int>(), value0, value1, false, result,
                "eliminated", 200, "10.0", left0, left1, 1, 1, -1, 1UL, 1UL);
        }

        /// <summary>Both seat orders of a pair in which <paramref name="winner"/> beats <paramref name="loser"/> keeping <paramref name="left"/> of its value.</summary>
        private static List<FightRecord> Beat(string winner, string loser, double left)
        {
            return new List<FightRecord>
            {
                Rec(winner, loser, FightRecord.Seat0Won, left, 0.0),
                Rec(loser, winner, FightRecord.Seat1Won, 0.0, left),
            };
        }

        /// <summary>Shipped settings and data for the hand-built check tests.</summary>
        private static void Shipped(out DataSet data, out FightSettings settings)
        {
            string folder = HarnessTestSupport.CopyData();
            data = HarnessTestSupport.Load(folder);
            settings = FightSettings.Load(folder, data);
        }

        /// <summary>Test 13a: the shipped data fails rule 2 with the drench numbers, and rule 1 lists the Roc Rider air answer count the roster actually gives.</summary>
        [Test]
        public void Checks_DataLintsOnShippedData()
        {
            Shipped(out DataSet data, out FightSettings settings);
            CheckResult rule2 = FightChecks.Rule2MultiplierBand(new List<FightRecord>(), settings, FightMatchups.Empty, data);
            Assert.That(rule2.Pass, Is.False);
            Assert.That(rule2.Detail, Does.Contain("drench/structure 0.25 below 0.5"));
            Assert.That(rule2.Detail, Does.Contain("drench ratio 4.00 above 2"));
            // With the roster of R-1 and R-2 (Leviathan hits air) Skai's Roc Rider has two Water answers, so rule 1 passes on the shipped data; a threshold of 5 fails it (the Reef Runner and the Shellback count as conditional "(on water)" answers since combat_mechanics.md package 3, so the Roc Rider has four).
            Assert.That(FightChecks.Rule1NoImmunity(new List<FightRecord>(), settings, FightMatchups.Empty, data).Pass, Is.True);
            string strict = FightTestSupport.DataWith("rule1_min_mobile_answers", "5");
            DataSet strictData = HarnessTestSupport.Load(strict);
            CheckResult rule1 = FightChecks.Rule1NoImmunity(new List<FightRecord>(), FightSettings.Load(strict, strictData), FightMatchups.Empty, strictData);
            Assert.That(rule1.Pass, Is.False);
            Assert.That(rule1.Detail, Does.Contain("water: skai.roc_rider attacked by 4 mobile units (water.leviathan, water.reef_runner (on water), water.shellback (on water), water.spoutling), needs 5"));
            // Lowering the rule 2 thresholds to the shipped numbers makes that lint pass.
            string relaxed = FightTestSupport.DataWith("rule2_min_multiplier", "0.25", "rule2_max_ratio_within_type", "4");
            DataSet relaxedData = HarnessTestSupport.Load(relaxed);
            FightSettings relaxedSettings = FightSettings.Load(relaxed, relaxedData);
            Assert.That(FightChecks.Rule2MultiplierBand(new List<FightRecord>(), relaxedSettings, FightMatchups.Empty, relaxedData).Pass, Is.True);
        }

        /// <summary>Test 13b: rule 3 on hand-built records, rule 4a and the matchups_agree detail format.</summary>
        [Test]
        public void Checks_FightRulesOnHandBuiltRecords()
        {
            Shipped(out DataSet data, out FightSettings settings);
            // Rule 3: the Pummeler is the stated counter of the Brinecaster.
            FightMatchups counter = FightMatchups.Parse("{ \"water.ink_lobber\": { \"job\": \"x\", \"wins\": null, \"loses\": { \"against\": \"water.grasper\", \"case\": \"flat\" } } }", settings, data);
            CheckResult lopsided = FightChecks.Rule3CounterMargin(Beat("water.grasper", "water.ink_lobber", 0.90), settings, counter, data);
            Assert.That(lopsided.Pass, Is.False);
            Assert.That(FightTestSupport.Join(lopsided.Detail), Does.Contain("exchange ratio 10.0 above 3"));
            Assert.That(FightChecks.Rule3CounterMargin(Beat("water.grasper", "water.ink_lobber", 0.50), settings, counter, data).Pass, Is.True);
            Assert.That(FightChecks.Rule3CounterMargin(Beat("water.grasper", "water.ink_lobber", 0.10), settings, counter, data).Pass, Is.False, "keeps less than 0.2");
            Assert.That(FightChecks.Rule3CounterMargin(Beat("water.ink_lobber", "water.grasper", 0.50), settings, counter, data).Pass, Is.False, "the claimed counter does not beat the unit");

            // Rule 4a: a Roc Rider that beats both other Skai units of a three-unit sweep.
            string quick = FightTestSupport.Quick("units", "[\"skai.roc_rider\", \"skai.talon_archer\", \"skai.windrunner\"]", "threats", "[]");
            DataSet quickData = HarnessTestSupport.Load(quick);
            FightSettings quickSettings = FightSettings.Load(quick, quickData);
            List<FightRecord> records = Beat("skai.roc_rider", "skai.talon_archer", 0.6);
            records.AddRange(Beat("skai.roc_rider", "skai.windrunner", 0.6));
            CheckResult dominant = FightChecks.Rule4NoDominantUnit(records, quickSettings, FightMatchups.Empty, quickData);
            Assert.That(dominant.Pass, Is.False);
            Assert.That(FightTestSupport.Join(dominant.Detail), Does.Contain("skai.roc_rider beats every other skai unit"));
            Assert.That(FightChecks.Rule4NoDominantUnit(Beat("skai.roc_rider", "skai.talon_archer", 0.6), quickSettings, FightMatchups.Empty, quickData).Pass, Is.True);
            Assert.That(FightChecks.Rule4EveryUnitWins(records, quickSettings, FightMatchups.Empty, quickData).Pass, Is.False, "no other-faction unit in this sweep");

            // matchups_agree: the Pummeler claims to lose to the Brinecaster, but won both seats.
            FightMatchups wrong = FightMatchups.Parse("{ \"water.grasper\": { \"job\": \"x\", \"wins\": null, \"loses\": { \"against\": \"water.ink_lobber\", \"case\": \"flat\" } } }", settings, data);
            CheckResult agree = FightChecks.MatchupsAgree(Beat("water.grasper", "water.ink_lobber", 0.41), settings, wrong, data);
            Assert.That(agree.Pass, Is.False);
            Assert.That(agree.Detail, Does.Contain("water.grasper: claims it loses to water.ink_lobber on flat; measured seat 0 win (left 0.41), seat 1 win (left 0.41)"));
            FightMatchups right = FightMatchups.Parse("{ \"water.grasper\": { \"job\": \"x\", \"wins\": { \"against\": \"water.ink_lobber\", \"case\": \"flat\" }, \"loses\": null } }", settings, data);
            Assert.That(FightTestSupport.Join(FightChecks.MatchupsAgree(Beat("water.grasper", "water.ink_lobber", 0.41), settings, right, data).Detail), Does.Not.Contain("water.grasper: claims"));
            // A mismatch fight counts as no fight: the same claim then reads "missing or mismatch".
            Assert.That(FightChecks.MatchupsAgree(new List<FightRecord>(), settings, right, data).Detail, Does.Contain("water.grasper: claims it beats water.ink_lobber on flat; measured seat 0 missing or mismatch, seat 1 missing or mismatch"));
        }

        /// <summary>The content digest a simulation of the folder's data computes at Start (what two peers compare at tick 0).</summary>
        private static ulong ContentDigestOf(string folder, out ulong tuningDigest)
        {
            DataSet data = HarnessTestSupport.Load(folder);
            tuningDigest = data.Tuning.SourceDigest;
            List<Rts.Sim.Teams.PlayerSetup> players = new List<Rts.Sim.Teams.PlayerSetup>
            {
                new Rts.Sim.Teams.PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "a"),
                new Rts.Sim.Teams.PlayerSetup(new PlayerId(1), new TeamId(1), "water", "b"),
            };
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(1UL), data.Content, players, Rts.Sim.Factions.BuiltInMechanics.CreateRegistry(), data.Tuning,
                data.RequireMap(null), data.Cycles.Values));
            sim.Start();
            return sim.ContentDigest;
        }

        /// <summary>Test 14: editing matchups.json, fights.json or harness_batch.json changes neither digest; editing tuning/hq.json changes both (the control).</summary>
        [Test]
        public void DigestGuard_FightFilesStayOutsideTheDigests()
        {
            ulong baseContent = ContentDigestOf(HarnessTestSupport.CopyData(), out ulong baseTuning);
            foreach (DataEdit edit in new[]
            {
                new DataEdit(FightTestSupport.MatchupsPath, "No entries yet.", "Edited text."),
                new DataEdit(FightTestSupport.SettingsPath, "\"base_seed\": 20261118", "\"base_seed\": 7"),
                new DataEdit("tuning/harness_batch.json", "\"chunk_games\": 25", "\"chunk_games\": 26"),
            })
            {
                ulong content = ContentDigestOf(HarnessTestSupport.CopyData(new[] { edit }), out ulong tuning);
                Assert.That(content, Is.EqualTo(baseContent), edit.File);
                Assert.That(tuning, Is.EqualTo(baseTuning), edit.File);
            }
            ulong changedContent = ContentDigestOf(HarnessTestSupport.CopyData(new[] { new DataEdit("tuning/hq.json", "\"root_seconds\": 10", "\"root_seconds\": 11") }), out ulong changedTuning);
            Assert.That(changedTuning, Is.Not.EqualTo(baseTuning));
            Assert.That(changedContent, Is.Not.EqualTo(baseContent));
        }

        /// <summary>Test 15: a failing check is exit 0 without --strict and exit 2 with it; CHECK ... FAIL is printed either way.</summary>
        [Test]
        public void Strict_TurnsAFailedCheckIntoExit2()
        {
            // The quick sweep has no matchups entries, so matchups_agree fails.
            string folder = FightTestSupport.Quick();
            int lenient = Sweep(folder, 2, out string lenientOut, out string dirA);
            int strict = Sweep(folder, 2, out string strictOut, out string dirB, "--strict");
            Assert.That(lenient, Is.EqualTo(0), lenientOut);
            Assert.That(strict, Is.EqualTo(2), strictOut);
            Assert.That(lenientOut, Does.Contain("CHECK matchups_agree FAIL"));
            Assert.That(strictOut, Does.Contain("CHECK matchups_agree FAIL"));
            Assert.That(lenientOut, Does.Contain("CHECK rule6_hq_floor "));
            Assert.That(lenientOut, Does.Match(@"FIGHTS fights=6 decided=\d+ draws=\d+ stalemates=\d+ unresolved=\d+ mismatched=0 checks_passed=\d of=8"));
        }

        /// <summary>The recipe of a composition side.</summary>
        private static IReadOnlyDictionary<string, int> FindRecipe(FightPlan plan, string id)
        {
            foreach (FightSide side in plan.Sides)
            {
                if (side.Id == id)
                {
                    return side.Recipe;
                }
            }
            Assert.Fail("no side " + id);
            return null!;
        }

        /// <summary>The fight with this ID in a plan.</summary>
        private static FightSpec Find(FightPlan plan, string id)
        {
            foreach (FightSpec spec in plan.Fights)
            {
                if (spec.Id == id)
                {
                    return spec;
                }
            }
            Assert.Fail("no fight " + id);
            return null!;
        }
    }
}

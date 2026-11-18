// rts/tests/Sim.Tests/Harness/HarnessUnitTests.cs
// Fast, process-free acceptance tests of the balance harness (spec rts/docs/specs/balance_harness.md section 7): seed derivation (1), side swap (2),
// matchup and map ID validation (3), Wilson interval (4), percentiles (5), the HQ floor arithmetic and survived trial (9, 10 in-process part), the
// deferred and failing median cap (11) and the floor consistency check (14). Part of Rts.Sim.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Process-free harness tests.</summary>
    [TestFixture]
    public class HarnessUnitTests
    {
        /// <summary>Deletes the temporary data folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>Spec test 1: FNV-1a of known strings and the derived seeds of the spec's examples.</summary>
        [Test]
        public void SeedDerivation_MatchesTheSpecValues()
        {
            Assert.That(SeedDerivation.Fnv1a64(string.Empty), Is.EqualTo(0xCBF29CE484222325UL));
            Assert.That(SeedDerivation.Fnv1a64("a"), Is.EqualTo(0xAF63DC4C8601EC8CUL));
            Assert.That(SeedDerivation.Fnv1a64("greybox_1v1/skai_vs_water"), Is.EqualTo(0xDA7141FC5CB80F77UL));
            Assert.That(SeedDerivation.Fnv1a64("greybox_1v1/skai_vs_skai"), Is.EqualTo(0x27E9E0F647F98A9CUL));
            Assert.That(SeedDerivation.GameSeed(20261026, "greybox_1v1", "skai_vs_water", 0), Is.EqualTo(0x6CB3B0FB23ACA4E0UL));
            Assert.That(SeedDerivation.GameSeed(20261026, "greybox_1v1", "skai_vs_water", 1), Is.EqualTo(0x95F249730176B09AUL));
            Assert.That(SeedDerivation.GameSeed(20261026, "greybox_1v1", "skai_vs_water", 2), Is.EqualTo(0xA4D5B07EAD235131UL));
            Assert.That(SeedDerivation.GameSeed(20261026, "greybox_1v1", "skai_vs_water", 499), Is.EqualTo(0x80243880BB17C8C3UL));
            Assert.That(SeedDerivation.GameSeed(20261026, "greybox_1v1", "skai_vs_skai", 0), Is.EqualTo(0x542BF6F4AD48990FUL));
        }

        /// <summary>Spec test 2: even games put the first-named faction in slot 0, odd games swap; a slot-1 win in game 1 counts for the first faction.</summary>
        [Test]
        public void SideSwap_AlternatesAndScoresForTheFirstFaction()
        {
            Assert.That(GameRunner.FactionInSlot("skai_vs_water", 0, 0), Is.EqualTo("skai"));
            Assert.That(GameRunner.FactionInSlot("skai_vs_water", 0, 1), Is.EqualTo("water"));
            Assert.That(GameRunner.FactionInSlot("skai_vs_water", 1, 0), Is.EqualTo("water"));
            Assert.That(GameRunner.FactionInSlot("skai_vs_water", 1, 1), Is.EqualTo("skai"));
            // A game 1 record where slot 1 (skai) won, and one where slot 0 (water) won.
            GameRecord slot1Win = new GameRecord("m", "skai_vs_water", 1, 1, "water", "skai", "aggressive", "aggressive", GameRecord.Slot1, 10, 1, 1, new List<HqKill>());
            GameRecord slot0Win = new GameRecord("m", "skai_vs_water", 1, 1, "water", "skai", "aggressive", "aggressive", GameRecord.Slot0, 10, 1, 1, new List<HqKill>());
            Assert.That(ReportWriter.ScoreFor(slot1Win, "skai"), Is.EqualTo(1));
            Assert.That(ReportWriter.ScoreFor(slot0Win, "skai"), Is.EqualTo(-1));
        }

        /// <summary>Spec test 3: reversed, headless-faction and unknown IDs are errors (the batch exits 1) with a useful message.</summary>
        [Test]
        public void MatchupAndMapIds_AreValidated()
        {
            DataSet data = TestData_Load();
            HarnessException reversed = Expect.Throws<HarnessException>(() => BatchRunner.ResolveMatchups(data, new[] { "water_vs_skai" }));
            Assert.That(reversed.Message, Does.Contain("skai_vs_water"));
            HarnessException neutral = Expect.Throws<HarnessException>(() => BatchRunner.ResolveMatchups(data, new[] { "neutral_vs_skai" }));
            Assert.That(neutral.Message, Does.Contain("hq_unit"));
            HarnessException map = Expect.Throws<HarnessException>(() => BatchRunner.ResolveMaps(data, new[] { "no_such_map" }));
            Assert.That(map.Message, Does.Contain("no_such_map"));
            Assert.That(BatchRunner.ResolveMatchups(data, new[] { "skai_vs_water", "skai_vs_skai" }), Is.EqualTo(new[] { "skai_vs_skai", "skai_vs_water" }));
            // The same errors reach the exit code 1 through the command line.
            string folder = TestDataFolder();
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--batch", "--matchups", "water_vs_skai" }, out _, out string error), Is.EqualTo(1));
            Assert.That(error, Does.Contain("skai_vs_water"));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--batch", "--maps", "no_such_map" }, out _, out _), Is.EqualTo(1));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--batch", "--ci", "--games", "3" }, out _, out _), Is.EqualTo(1), "--ci and --games exclude each other");
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--batch", "--script", "x.json" }, out _, out _), Is.EqualTo(1), "single-run options are errors in batch mode");
        }

        /// <summary>Spec test 4: Wilson interval examples and the band flags.</summary>
        [Test]
        public void Wilson_MatchesTheSpecExamples()
        {
            WinRate half = HarnessStats.Wilson(10, 0, 10, 1.96);
            Assert.That(HarnessStats.Percent(half.P) + " " + HarnessStats.Percent(half.Low) + " " + HarnessStats.Percent(half.High), Is.EqualTo("50.0 29.9 70.1"));
            WinRate large = HarnessStats.Wilson(250, 0, 250, 1.96);
            Assert.That(HarnessStats.Percent(large.Low) + " " + HarnessStats.Percent(large.High), Is.EqualTo("45.6 54.4"));
            WinRate edge = HarnessStats.Wilson(275, 0, 225, 1.96);
            Assert.That(HarnessStats.Percent(edge.P) + " " + HarnessStats.Percent(edge.Low) + " " + HarnessStats.Percent(edge.High), Is.EqualTo("55.0 50.6 59.3"));
            Assert.That(HarnessStats.OutsideBand(edge.P, 0.45, 0.55), Is.False, "the band edge is inclusive");
            Assert.That(HarnessStats.SignificantlyOutsideBand(edge, 0.45, 0.55), Is.False);
            WinRate none = HarnessStats.Wilson(0, 0, 20, 1.96);
            Assert.That(HarnessStats.Percent(none.Low) + " " + HarnessStats.Percent(none.High), Is.EqualTo("0.0 16.1"));
            Assert.That(HarnessStats.SignificantlyOutsideBand(none, 0.45, 0.55), Is.True);
            Assert.That(HarnessStats.Wilson(9, 2, 9, 1.96).P, Is.EqualTo(0.5));
        }

        /// <summary>Spec test 5: lower median, p10 and p90 by integer indexing, one value, and no games.</summary>
        [Test]
        public void Percentiles_UseTheLowerMedianAndDashForNoGames()
        {
            Assert.That(HarnessStats.LengthSummary(new[] { 400, 100, 300, 200 }, 20), Is.EqualTo(new[] { "10.0", "5.0", "15.0" }));
            Assert.That(HarnessStats.LengthSummary(new[] { 240 }, 20), Is.EqualTo(new[] { "12.0", "12.0", "12.0" }));
            Assert.That(HarnessStats.LengthSummary(new int[0], 20), Is.EqualTo(new[] { "-", "-", "-" }));
        }

        /// <summary>Spec test 9 (in-process part): ten 3-supply attackers of 10 damage per second against a 1000-health HQ kill it in 180 ticks (9.0 s, FAIL); with 2500 health in 480 ticks (24.0 s, PASS).</summary>
        [Test]
        public void HqFloor_ArithmeticFailsAndPasses()
        {
            DataSet failing = HarnessTestSupport.Load(FloorData(1000));
            HqFloorRecord record = HqFloorTrial.Run(failing, new HqFloorSpec("skai", "skai.windrunner", "skai", true));
            Assert.That(record.Count, Is.EqualTo(10));
            Assert.That(record.TtkTicks, Is.EqualTo(180));
            LimitResult limit = ReportWriter.EvaluateFloor(InputWithFloors(failing, record));
            Assert.That(limit.Result, Is.EqualTo("FAIL"));
            Assert.That(limit.Measured, Does.StartWith("9.0 s"));

            DataSet passing = HarnessTestSupport.Load(FloorData(2500));
            HqFloorRecord longer = HqFloorTrial.Run(passing, new HqFloorSpec("skai", "skai.windrunner", "skai", true));
            Assert.That(longer.TtkTicks, Is.EqualTo(480));
            LimitResult passed = ReportWriter.EvaluateFloor(InputWithFloors(passing, longer));
            Assert.That(passed.Result, Is.EqualTo("PASS"));
            Assert.That(passed.Measured, Does.StartWith("24.0 s"));
        }

        /// <summary>Spec test 10: an attacker whose damage table entry is 0 never hurts the HQ: ttk -1, reported as "&gt; 300 s", and the limit passes.</summary>
        [Test]
        public void HqFloor_SurvivedTrialPasses()
        {
            DataSet data = HarnessTestSupport.Load(HarnessTestSupport.CopyData(new[] { new DataEdit("tuning/combat.json", "\"structure\": 0.5", "\"structure\": 0.0") }));
            HqFloorRecord record = HqFloorTrial.Run(data, new HqFloorSpec("skai", "skai.windrunner", "water", true));
            Assert.That(record.TtkTicks, Is.EqualTo(-1));
            Assert.That(record.DeathTick, Is.EqualTo(-1));
            ReportInput input = InputWithFloors(data, record);
            Assert.That(ReportWriter.EvaluateFloor(input).Result, Is.EqualTo("PASS"));
            Assert.That(ReportWriter.Build(input), Does.Contain("| Rooted | > 300 s |"));
        }

        /// <summary>Spec test 11: with the shipped data (no earth faction) the median cap is DEFERRED; with a played matchup whose median exceeds the cap it FAILS.</summary>
        [Test]
        public void MedianCap_DeferredWithoutEarthAndFailsWhenExceeded()
        {
            DataSet shipped = TestData_Load();
            Assert.That(ReportWriter.EvaluateMedianCap(InputWithGames(shipped)).Result, Is.EqualTo("DEFERRED"));
            DataSet capped = HarnessTestSupport.Load(HarnessTestSupport.CopyData(new[] { new DataEdit("tuning/harness_batch.json", "\"earth_vs_water\"", "\"skai_vs_water\"") }));
            // Cap 1500 s is 30000 ticks; a 40000-tick game is over it, a 20000-tick game under it.
            GameRecord slow = new GameRecord("greybox_1v1", "skai_vs_water", 0, 1, "skai", "water", "aggressive", "aggressive", GameRecord.Slot0, 39999, 1, 1, new List<HqKill>());
            GameRecord fast = new GameRecord("greybox_1v1", "skai_vs_water", 0, 1, "skai", "water", "aggressive", "aggressive", GameRecord.Slot0, 19999, 1, 1, new List<HqKill>());
            Assert.That(ReportWriter.EvaluateMedianCap(InputWithGames(capped, slow)).Result, Is.EqualTo("FAIL"));
            Assert.That(ReportWriter.EvaluateMedianCap(InputWithGames(capped, fast)).Result, Is.EqualTo("PASS"));
            Assert.That(ReportWriter.EvaluateMedianCap(InputWithGames(capped)).Result, Is.EqualTo("DEFERRED"), "no game played");
        }

        /// <summary>Spec test 14: hq.json and harness.json disagreeing about the floor is an error (exit 1) with the spec's message.</summary>
        [Test]
        public void FloorConsistency_MismatchExitsWithOne()
        {
            string folder = HarnessTestSupport.CopyData(new[] { new DataEdit("tuning/hq.json", "\"survivability_floor_seconds\": 15", "\"survivability_floor_seconds\": 25") });
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--batch", "--out", HarnessTestSupport.NewFolder() }, out _, out string error), Is.EqualTo(1));
            Assert.That(error, Does.Contain("tuning/hq.json survivability_floor_seconds must equal tuning/harness.json hq_floor_seconds"));
        }

        /// <summary>Spec test 15: the single-run mode prints the same hashes as before the batch mode existed.</summary>
        [Test]
        public void SingleRunMode_IsUnchanged()
        {
            // Both hashes re-pinned for worker construction (specs/worker_construction.md): new tuning key worker_placement_seconds and workers walk to build sites.
            string folder = TestDataFolder();
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder }, out string output, out _), Is.EqualTo(0));
            // Re-pinned again for the missing roster (specs/missing_roster.md R2-9): old values 03674dfb1efc68b7 and 93762ddc16910b23, outcomes unchanged.
            // Re-pinned for combat mechanics package 1 (specs/combat_mechanics.md): old value a65a7eea003009f4, outcome unchanged (undecided, tick 599). Re-pinned for combat mechanics package 2 (structure_damage_multiplier in the content digest): a65a7eea003009f4 became c399aaafed8cf945 in package 1 and is now 55643cc0cb0ce049; economy_smoke 9a71eaccaab510c5 is now e4127bf62b50cbc1, outcomes unchanged.
            // Re-pinned for F0 (specs/micro_benchmark.md 3.3, developer decision 4 of 18 Nov 2026: Rooted HQ floor 20 s to 15 s in hq.json and harness.json, both in the content digest): smoke 55643cc0cb0ce049 is now 225054de92f557a4 (undecided, tick 599), economy_smoke e4127bf62b50cbc1 is now 9cffda66d3cb0548 (undecided, tick 1499).
            // Re-pinned for combat mechanics package 3 (specs/combat_mechanics.md: can_target_air_on_water is in the weapon digest, Reef Runner and Shellback carry it): smoke 225054de92f557a4 is now a3aada433dad3915 (undecided, tick 599), economy_smoke 9cffda66d3cb0548 is now d951951064e0b735 (undecided, tick 1499).
            // Re-pinned for combat mechanics package 4 (specs/combat_mechanics.md: grounding_ability in the content digest and Unit.GroundingReadyTick in the unit hash, the Stormcaller carries the ability): smoke a3aada433dad3915 is now e1ab5e425ce8cb35 (undecided, tick 599), economy_smoke d951951064e0b735 is now 06d921a35888ec95 (undecided, tick 1499).
            // Re-pinned for walls (specs/walls_and_tech.md package W: is_wall in the content digest, two wall unit files): smoke e1ab5e425ce8cb35 is now 2c679e2fc3c0a732 and economy_smoke 06d921a35888ec95 is now b27e696890ec03ba; outcomes unchanged (undecided, ticks 599 and 1499).
            // Re-pinned for tech prerequisites (specs/walls_and_tech.md package T: requires in the content digest, two tech building files): smoke 2c679e2fc3c0a732 is now 9c985e8fc052ce36 and economy_smoke b27e696890ec03ba is now 57e92601f6755ea6; outcomes unchanged (undecided, ticks 599 and 1499).
            // Re-pinned for gates (specs/walls_and_tech.md package G: GateTeam in the tile hash, is_gate in the content digest, two gate unit files): smoke 9c985e8fc052ce36 is now 722a561a7c2ad335 and economy_smoke 57e92601f6755ea6 is now ea7a4a9d577c9b83; outcomes unchanged (undecided, ticks 599 and 1499).
            // Re-pinned for drag to place (specs/walls_and_tech.md package D: Unit.BuildQueue in the unit hash, wall_line_max_tiles in the tuning digest): smoke 722a561a7c2ad335 is now c423332711029434 and economy_smoke ea7a4a9d577c9b83 is now ce62b175ea751ba6; outcomes unchanged (undecided, ticks 599 and 1499).
            Assert.That(output, Does.Contain("STATE_HASH=c423332711029434\n"));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--script", System.IO.Path.Combine(folder, "scripts", "economy_smoke.json") }, out string economy, out _), Is.EqualTo(0));
            Assert.That(economy, Does.Contain("STATE_HASH=ce62b175ea751ba6\n"));
        }

        /// <summary>The shipped data set.</summary>
        private static DataSet TestData_Load()
        {
            return Rts.Sim.Tests.Data.TestData.LoadAll();
        }

        /// <summary>Folder of the shipped data.</summary>
        private static string TestDataFolder()
        {
            return Rts.Sim.Tests.Data.TestData.FindDataDirectory();
        }

        /// <summary>A report input with the data set, the given game records and no other records.</summary>
        private static ReportInput InputWithGames(DataSet data, params GameRecord[] games)
        {
            return new ReportInput { Data = data, BaseSeed = 1, GamesPerCell = 1, AiProfile = "aggressive", MaxGameSeconds = 1, Games = new List<GameRecord>(games) };
        }

        /// <summary>A report input with the data set and the given floor records.</summary>
        private static ReportInput InputWithFloors(DataSet data, params HqFloorRecord[] floors)
        {
            return new ReportInput { Data = data, BaseSeed = 1, GamesPerCell = 1, AiProfile = "aggressive", MaxGameSeconds = 1, Floors = new List<HqFloorRecord>(floors) };
        }

        /// <summary>
        /// Data folder for the floor arithmetic tests (spec test 9): the Great Aerie has <paramref name="hqHealth"/> health and a negligible weapon, kinetic
        /// damage counts in full against structures, the Windrunner has supply 3, damage 10, cooldown 1 s, no hit delay and 10 tiles of range, every other
        /// Skai unit deals 0.01 damage, the matchup is skai against skai and the standoff is 0 so every attacker is in reach at once.
        /// </summary>
        /// <param name="hqHealth">Health of the Great Aerie. Unit: health points. Range: 1 to 100000.</param>
        private static string FloorData(int hqHealth)
        {
            return HarnessTestSupport.CopyData(
                new[]
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
                });
        }
    }
}

// rts/tests/Sim.Tests/Cycles/CycleDataTests.cs
// Acceptance test 19 of rts/docs/specs/planetary_cycles.md section 7 (cycle files, quake_paths, tuning keys), the pick_cycle script
// type, and the line-ending rule: the loader normalises CRLF and a lone CR to LF before it hashes any text, so a Windows checkout
// gives the same ContentDigest and the same final state hash as the LF original. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;

namespace Rts.Sim.Tests.Cycles
{
    /// <summary>Loading cycles, quake paths and the new tuning keys; script parsing; line endings.</summary>
    [TestFixture]
    public class CycleDataTests
    {
        // ---------------------------------------------------------------- shipped data

        [Test]
        public void ShippedCycles_CarryTheirEffectNumbers()
        {
            DataSet data = TestData.LoadAll();
            Assert.That(data.Cycles.Count, Is.EqualTo(3));
            Assert.That(data.Cycles["windstorm"].Favours, Is.EqualTo(CycleFavour.Air));
            Assert.That(data.Cycles["windstorm"].Effects.AirSpeedMultiplier, Is.EqualTo(Fix64.Parse("1.25")));
            Assert.That(data.Cycles["windstorm"].Effects.SurfaceRangedRangeBonusTiles, Is.EqualTo(Fix64.FromInt(-1)));
            Assert.That(data.Cycles["quake"].Effects.TogglesQuakePaths, Is.True);
            Assert.That(data.Cycles["quake"].Effects.DefenceDamagePerSecondFraction, Is.EqualTo(Fix64.Parse("0.004")));
            Assert.That(data.Cycles["quake"].Effects.DefenceDamageMinHealthFraction, Is.EqualTo(Fix64.Parse("0.25")));
            Assert.That(data.Cycles["core_surge"].Effects.CoreDamageMultiplier, Is.EqualTo(Fix64.Parse("1.5")));
            Assert.That(data.Cycles["core_surge"].Effects.CoreEffectRadiusTiles, Is.EqualTo(Fix64.FromInt(8)));
        }

        [Test]
        public void ShippedTuning_HasTheNewKeys()
        {
            Tuning tuning = TestData.LoadAll().Tuning;
            Assert.That(tuning.Match.CoreWinMinTeams, Is.EqualTo(3));
            Assert.That(tuning.Match.QuakePathRelocateRadiusTiles, Is.EqualTo(3));
            Assert.That(tuning.Economy.StartingWorkerCount, Is.EqualTo(4));
            Assert.That(tuning.Match.PlanetaryCycleIntervalTicks(new MatchClock(20)), Is.EqualTo(4800));
            Assert.That(tuning.Match.PlanetaryCycleWarningTicks(new MatchClock(20)), Is.EqualTo(1200));
            Assert.That(tuning.Match.CoreCaptureTicks(new MatchClock(20)), Is.EqualTo(400));
            Assert.That(tuning.Match.FfaCoreWinCumulativeTicks(new MatchClock(20)), Is.EqualTo(9600));
        }

        // ---------------------------------------------------------------- cycle files

        [Test]
        public void CycleFile_MissingAnEffectKey_NamesFileAndField()
        {
            DataValidationException error = Expect.Throws<DataValidationException>(() => DataLoader.Load(CycleWorld.EditedFiles(
                "cycles/core_surge.json", "\"core_damage_multiplier\": 1.5,", "\"core_damage_multiplier\": 1.5",
                "cycles/core_surge.json", "\"core_effect_radius_tiles\": 8", string.Empty)));
            Assert.That(error.Message, Is.EqualTo("cycles/core_surge.json: field 'effects.core_effect_radius_tiles' is missing"));
        }

        [Test]
        public void CycleFile_EffectOutOfRange_IsAnError()
        {
            DataValidationException error = Expect.Throws<DataValidationException>(() => DataLoader.Load(CycleWorld.EditedFiles(
                "cycles/windstorm.json", "\"air_speed_multiplier\": 1.25", "\"air_speed_multiplier\": 3")));
            Assert.That(error.File, Is.EqualTo("cycles/windstorm.json"));
            Assert.That(error.Field, Is.EqualTo("effects.air_speed_multiplier"));
        }

        [Test]
        public void CycleFile_MinHealthFractionOfZero_IsAnError()
        {
            // A floor of 0 would let a quake kill a defence nothing then removes; the loader needs it strictly above 0.
            DataValidationException error = Expect.Throws<DataValidationException>(() => DataLoader.Load(CycleWorld.EditedFiles(
                "cycles/quake.json", "\"defence_damage_min_health_fraction\": 0.25", "\"defence_damage_min_health_fraction\": 0")));
            Assert.That(error.File, Is.EqualTo("cycles/quake.json"));
            Assert.That(error.Field, Is.EqualTo("effects.defence_damage_min_health_fraction"));
            // The smallest accepted value loads.
            Assert.That(DataLoader.Load(CycleWorld.EditedFiles("cycles/quake.json", "\"defence_damage_min_health_fraction\": 0.25",
                "\"defence_damage_min_health_fraction\": 0.01")).Cycles["quake"].Effects.DefenceDamageMinHealthFraction, Is.EqualTo(Fix64.Parse("0.01")));
        }

        [Test]
        public void CycleFile_UnknownEffectKey_IsAnError()
        {
            DataValidationException error = Expect.Throws<DataValidationException>(() => DataLoader.Load(CycleWorld.EditedFiles(
                "cycles/quake.json", "\"toggles_quake_paths\": true,", "\"toggles_quake_paths\": true, \"typo_key\": 1,")));
            Assert.That(error.Field, Is.EqualTo("effects.typo_key"));
        }

        [Test]
        public void Cycles_AllOfOnePlayStyle_IsAnError()
        {
            DataValidationException error = Expect.Throws<DataValidationException>(() => DataLoader.Load(CycleWorld.EditedFiles(
                "cycles/quake.json", "\"favours\": \"attackers\"", "\"favours\": \"air\"",
                "cycles/core_surge.json", "\"favours\": \"position\"", "\"favours\": \"air\"")));
            Assert.That(error.Message, Does.StartWith("cycles/core_surge.json"), "the error names the first cycle file in ordinal order");
            Assert.That(error.Message, Does.Contain("at least two play styles are needed so the no-repeat rule can always pick"));
        }

        [Test]
        public void Cycles_NoCycleFilesAtAll_IsFine()
        {
            List<DataSourceFile> files = TestData.ReadAll();
            files.RemoveAll(f => f.Path.StartsWith("cycles/"));
            Assert.That(DataLoader.Load(files).Cycles.Count, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- quake paths

        /// <summary>The greybox map file with a quake_paths array added after the Core.</summary>
        private static List<DataSourceFile> WithPaths(string pathsJson)
        {
            return CycleWorld.EditedFiles("maps/greybox_1v1.json", "\"core\": { \"x\": 32, \"y\": 32, \"radius_tiles\": 4 }",
                "\"core\": { \"x\": 32, \"y\": 32, \"radius_tiles\": 4 },\n  \"quake_paths\": " + pathsJson);
        }

        [Test]
        public void QuakePaths_LoadAndEnterTheMapHash()
        {
            DataSet plain = TestData.LoadAll();
            DataSet withPaths = DataLoader.Load(WithPaths("[ { \"id\": \"west_gap\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 20, \"y\": 40 }, { \"x\": 21, \"y\": 40 } ] } ]"));
            MapDefinition map = withPaths.Maps["greybox_1v1"];
            Assert.That(plain.Maps["greybox_1v1"].QuakePaths, Is.Empty);
            Assert.That(map.QuakePaths.Count, Is.EqualTo(1));
            Assert.That(map.QuakePaths[0].Id, Is.EqualTo("west_gap"));
            Assert.That(map.QuakePaths[0].ClosedOutsideQuake, Is.True);
            Assert.That(map.QuakePaths[0].TileIndexes, Is.EqualTo(new List<int> { 40 * 64 + 20, 40 * 64 + 21 }));
            StateHasher a = new StateHasher();
            StateHasher b = new StateHasher();
            plain.Maps["greybox_1v1"].WriteHash(a);
            map.WriteHash(b);
            Assert.That(b.Hash, Is.Not.EqualTo(a.Hash));
        }

        [Test]
        public void QuakePaths_TileOnAResourceField_NamesMapAndField()
        {
            DataValidationException error = Expect.Throws<DataValidationException>(() => DataLoader.Load(WithPaths(
                "[ { \"id\": \"bad\", \"closed_outside_quake\": false, \"tiles\": [ { \"x\": 4, \"y\": 10 } ] } ]")));
            Assert.That(error.File, Is.EqualTo("maps/greybox_1v1.json"));
            Assert.That(error.Field, Is.EqualTo("quake_paths[0].tiles[0]"));
            Assert.That(error.Message, Does.Contain("resource field"));
        }

        [Test]
        public void QuakePaths_BadShapes_AreErrors()
        {
            // Tile shared by two paths.
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(WithPaths(
                "[ { \"id\": \"a\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 20, \"y\": 40 } ] },"
                + " { \"id\": \"b\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 20, \"y\": 40 } ] } ]"))).Field, Is.EqualTo("quake_paths[1].tiles[0]"));
            // The same path ID twice.
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(WithPaths(
                "[ { \"id\": \"a\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 20, \"y\": 40 } ] },"
                + " { \"id\": \"a\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 21, \"y\": 40 } ] } ]"))).Field, Is.EqualTo("quake_paths[1].id"));
            // A tile off the map.
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(WithPaths(
                "[ { \"id\": \"a\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 64, \"y\": 40 } ] } ]"))).Field, Is.EqualTo("quake_paths[0].tiles[0].x"));
            // A path without tiles.
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(WithPaths(
                "[ { \"id\": \"a\", \"closed_outside_quake\": true, \"tiles\": [] } ]"))).Field, Is.EqualTo("quake_paths[0].tiles"));
            // A bad ID.
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(WithPaths(
                "[ { \"id\": \"Bad Id\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 20, \"y\": 40 } ] } ]"))).Field, Is.EqualTo("quake_paths[0].id"));
        }

        [Test]
        public void QuakePaths_OnASpawnOrItsWorkerRings_NameMapPathAndTile()
        {
            // greybox_1v1 spawns are (8, 8) and (56, 56); the shipped worker rings reach 3 tiles.
            DataValidationException onSpawn = Expect.Throws<DataValidationException>(() => DataLoader.Load(WithPaths(
                "[ { \"id\": \"walled_in\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 8, \"y\": 8 } ] } ]")));
            Assert.That(onSpawn.File, Is.EqualTo("maps/greybox_1v1.json"));
            Assert.That(onSpawn.Message, Does.Contain("walled_in").And.Contain("(8, 8)").And.Contain("spawn point of slot 0"));
            // A tile on the third ring of the second spawn.
            DataValidationException onRing = Expect.Throws<DataValidationException>(() => DataLoader.Load(WithPaths(
                "[ { \"id\": \"ring_three\", \"closed_outside_quake\": false, \"tiles\": [ { \"x\": 20, \"y\": 40 }, { \"x\": 53, \"y\": 56 } ] } ]")));
            Assert.That(onRing.Message, Does.Contain("ring_three").And.Contain("(53, 56)").And.Contain("slot 1"));
            // One tile further out is fine.
            Assert.That(DataLoader.Load(WithPaths(
                "[ { \"id\": \"outside\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 12, \"y\": 8 } ] } ]")).Maps["greybox_1v1"].QuakePaths.Count, Is.EqualTo(1));
            // The ring is data: widening it to 4 rejects that tile.
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(CycleWorld.EditedFiles(
                "maps/greybox_1v1.json", "\"core\": { \"x\": 32, \"y\": 32, \"radius_tiles\": 4 }",
                "\"core\": { \"x\": 32, \"y\": 32, \"radius_tiles\": 4 },\n  \"quake_paths\": [ { \"id\": \"outside\", \"closed_outside_quake\": true, \"tiles\": [ { \"x\": 12, \"y\": 8 } ] } ]",
                "tuning/economy.json", "\"starting_worker_search_radius_tiles\": 3", "\"starting_worker_search_radius_tiles\": 4"))).Message,
                Does.Contain("outside").And.Contain("(12, 8)"));
        }

        [Test]
        public void QuakePath_CopiesTheCallersTileList()
        {
            List<int> tiles = new List<int> { CycleWorld.Tile(5, 5), CycleWorld.Tile(6, 5) };
            QuakePath path = new QuakePath("gap", true, tiles);
            List<QuakePath> paths = new List<QuakePath> { path };
            MapDefinition map = CycleWorld.Map(true, paths);
            // The caller edits its lists after the map was built.
            tiles[0] = CycleWorld.Tile(1, 1);
            tiles.Add(CycleWorld.Tile(2, 2));
            paths.Clear();
            Assert.That(path.TileIndexes, Is.EqualTo(new List<int> { CycleWorld.Tile(5, 5), CycleWorld.Tile(6, 5) }));
            Assert.That(map.QuakePaths.Count, Is.EqualTo(1));
            Assert.That(map.QuakePaths[0].TileIndexes.Count, Is.EqualTo(2));
        }

        // ---------------------------------------------------------------- tuning keys

        [Test]
        public void MatchTuning_NewKeysAreRangeChecked()
        {
            Assert.That(Expect.Throws<DataValidationException>(() => CycleWorld.EditedTuning(
                "tuning/match.json", "\"core_win_min_teams\": 3", "\"core_win_min_teams\": 1")).Field, Is.EqualTo("core_win_min_teams"));
            Assert.That(Expect.Throws<DataValidationException>(() => CycleWorld.EditedTuning(
                "tuning/match.json", "\"quake_path_relocate_radius_tiles\": 3", "\"quake_path_relocate_radius_tiles\": 9")).Field, Is.EqualTo("quake_path_relocate_radius_tiles"));
            Assert.That(Expect.Throws<DataValidationException>(() => CycleWorld.EditedTuning(
                "tuning/economy.json", "\"starting_worker_count\": 4", "\"starting_worker_count\": 21")).Field, Is.EqualTo("starting_worker_count"));
        }

        // ---------------------------------------------------------------- pick_cycle in scripts

        [Test]
        public void Script_PickCycle_ParsesToACommand()
        {
            CommandScript script = CommandScript.Parse(
                "{ \"$schema_version\": 1, \"seed\": 1, \"ticks\": 10, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"skai\" } ],"
                + " \"commands\": [ { \"tick\": 100, \"issuer\": 255, \"sequence\": 3, \"type\": \"pick_cycle\", \"cycle\": \"quake\" } ] }", "scripts/pick.json");
            Assert.That(script.Commands.Count, Is.EqualTo(1));
            PickCycleCommand pick = (PickCycleCommand)script.Commands[0];
            Assert.That(pick.CycleId, Is.EqualTo("quake"));
            Assert.That(pick.Tick, Is.EqualTo(100));
            Assert.That(pick.Issuer, Is.EqualTo(PlayerId.None));
            Assert.That(pick.Sequence, Is.EqualTo(3u));
            Assert.That(pick.Kind, Is.EqualTo("pick_cycle"));
        }

        [Test]
        public void Script_PickCycle_NeedsACycleAndRejectsUnknownKeys()
        {
            string head = "{ \"$schema_version\": 1, \"seed\": 1, \"ticks\": 10, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"skai\" } ], \"commands\": [ ";
            Assert.That(Expect.Throws<DataValidationException>(() => CommandScript.Parse(
                head + "{ \"tick\": 1, \"issuer\": 255, \"sequence\": 0, \"type\": \"pick_cycle\" } ] }", "scripts/pick.json")).Message, Does.Contain("cycle"));
            Assert.That(Expect.Throws<DataValidationException>(() => CommandScript.Parse(
                head + "{ \"tick\": 1, \"issuer\": 255, \"sequence\": 0, \"type\": \"pick_cycle\", \"cycle\": \"quake\", \"x\": 1 } ] }", "scripts/pick.json")).Message, Does.Contain("not a known key"));
        }

        // ---------------------------------------------------------------- line endings

        /// <summary>The shipped files with every line break written as <paramref name="newline"/>.</summary>
        private static List<DataSourceFile> WithLineEndings(string newline)
        {
            List<DataSourceFile> converted = new List<DataSourceFile>();
            foreach (DataSourceFile file in TestData.ReadAll())
            {
                converted.Add(new DataSourceFile(file.Path, file.Text.Replace("\r\n", "\n").Replace("\n", newline)));
            }
            return converted;
        }

        /// <summary>Plays the first 300 ticks of ai_vs_ai.json's spawns (no AI) on data loaded from <paramref name="files"/>; returns the content digest and the final state hash.</summary>
        private static (ulong digest, ulong finalHash, ulong tuningDigest) Play(List<DataSourceFile> files)
        {
            DataSet data = DataLoader.Load(files);
            // The script text as these files carry it. Scripts are parsed by CommandScript, not the loader, and are not hashed, so a lone CR
            // (which would end no // comment) is turned into LF here; CRLF is read as it is.
            string text = files.Find(f => f.Path == "scripts/ai_vs_ai.json")!.Text.Replace("\r\n", "\n").Replace('\r', '\n');
            CommandScript script = CommandScript.Parse(text, "scripts/ai_vs_ai.json", BuiltInMechanics.ScriptParsers);
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(script.Seed), data.Content, script.Players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap(script.MapId), data.Cycles.Values));
            sim.Start();
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            sim.Step(300);
            return (sim.ContentDigest, sim.ComputeStateHash(), data.Tuning.SourceDigest);
        }

        [Test]
        public void LineEndings_CrlfAndLoneCrGiveTheSameDigestAndHashAsLf()
        {
            (ulong digest, ulong finalHash, ulong tuningDigest) lf = Play(WithLineEndings("\n"));
            (ulong digest, ulong finalHash, ulong tuningDigest) crlf = Play(WithLineEndings("\r\n"));
            (ulong digest, ulong finalHash, ulong tuningDigest) cr = Play(WithLineEndings("\r"));
            Assert.That(crlf.tuningDigest, Is.EqualTo(lf.tuningDigest));
            Assert.That(crlf.digest, Is.EqualTo(lf.digest), "a CRLF checkout has the same ContentDigest");
            Assert.That(crlf.finalHash, Is.EqualTo(lf.finalHash), "and the same final state hash");
            Assert.That(cr.digest, Is.EqualTo(lf.digest), "a lone CR is a line break too");
            Assert.That(cr.finalHash, Is.EqualTo(lf.finalHash));
        }

        [Test]
        public void LineEndings_ARealTextChangeStillChangesTheDigest()
        {
            List<DataSourceFile> changed = CycleWorld.EditedFiles("tuning/match.json", "\"core_capture_seconds\": 20", "\"core_capture_seconds\": 21");
            Assert.That(Play(changed).tuningDigest, Is.Not.EqualTo(Play(WithLineEndings("\n")).tuningDigest));
        }
    }
}

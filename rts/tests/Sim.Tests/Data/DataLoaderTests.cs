// rts/tests/Sim.Tests/Data/DataLoaderTests.cs
// Tests for DataLoader against the real files in rts/data: shipped values match the design doc, factions list real
// units, and each kind of broken file raises a DataValidationException that names the file and the field.
// Part of Rts.Sim.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Data
{
    /// <summary>DataLoader behaviour on the real data folder and on edited copies of it.</summary>
    [TestFixture]
    public class DataLoaderTests
    {
        /// <summary>The real data loaded once per test.</summary>
        private DataSet _data = null!;

        /// <summary>Loads the real data folder before each test.</summary>
        [SetUp]
        public void LoadRealData()
        {
            _data = TestData.LoadAll();
        }

        // ---------------------------------------------------------------- shipped values

        /// <summary>Match-level durations from the design doc (ADR-004 table).</summary>
        [Test]
        public void Tuning_Match_HasDesignDocDurations()
        {
            MatchTuning match = _data.Tuning.Match;
            Assert.That(match.TideCycleSeconds, Is.EqualTo(Fix64.FromInt(210)));
            Assert.That(match.PlanetaryCycleIntervalSeconds, Is.EqualTo(Fix64.FromInt(240)));
            Assert.That(match.PlanetaryCycleWarningSeconds, Is.EqualTo(Fix64.FromInt(60)));
            Assert.That(match.CoreCaptureSeconds, Is.EqualTo(Fix64.FromInt(20)));
            Assert.That(match.FfaCoreWinCumulativeSeconds, Is.EqualTo(Fix64.FromInt(480)));
            Assert.That(match.TicksPerSecond, Is.EqualTo(20));
            Assert.That(match.StateHashIntervalTicks, Is.EqualTo(20));
        }

        /// <summary>Durations convert to the whole ticks ADR-004 lists.</summary>
        [Test]
        public void Tuning_Durations_ConvertToAdr004Ticks()
        {
            MatchClock clock = new MatchClock(_data.Tuning.Match.TicksPerSecond);
            Assert.That(_data.Tuning.Match.TideCycleTicks(clock), Is.EqualTo(4200));
            Assert.That(_data.Tuning.Match.PlanetaryCycleIntervalTicks(clock), Is.EqualTo(4800));
            Assert.That(_data.Tuning.Match.PlanetaryCycleWarningTicks(clock), Is.EqualTo(1200));
            Assert.That(_data.Tuning.Match.CoreCaptureTicks(clock), Is.EqualTo(400));
            Assert.That(_data.Tuning.Hq.RootTicks(clock), Is.EqualTo(200));
            Assert.That(_data.Tuning.Hq.UprootTicks(clock), Is.EqualTo(100));
            Assert.That(_data.Tuning.Momentum.DrainDelayTicks(clock), Is.EqualTo(30));
            Assert.That(_data.Tuning.Status.GroundedDurationTicks(clock), Is.EqualTo(80));
        }

        /// <summary>HQ, Momentum and supply values from the design doc.</summary>
        [Test]
        public void Tuning_HqMomentumSupply_HaveDesignDocValues()
        {
            Assert.That(_data.Tuning.Hq.RootSeconds, Is.EqualTo(Fix64.FromInt(10)));
            Assert.That(_data.Tuning.Hq.UprootSeconds, Is.EqualTo(Fix64.FromInt(5)));
            Assert.That(_data.Tuning.Hq.LastStandEnabled, Is.False);
            Assert.That(_data.Tuning.Momentum.MaxStacks, Is.EqualTo(5));
            Assert.That(_data.Tuning.Momentum.SpeedBonusPerStack, Is.EqualTo(Fix64.Parse("0.04")));
            Assert.That(_data.Tuning.Momentum.DamageBonusPerStack, Is.EqualTo(Fix64.Parse("0.04")));
            Assert.That(_data.Tuning.Momentum.DrainDelaySeconds, Is.EqualTo(Fix64.Parse("1.5")));
            Assert.That(_data.Tuning.Momentum.LostOnGrounded, Is.True);
            Assert.That(_data.Tuning.Supply.SupplyCap, Is.EqualTo(200));
            Assert.That(_data.Tuning.Supply.StaticDefencesPerBaseMax, Is.EqualTo(6));
        }

        /// <summary>The remaining tuning files load and expose sane values.</summary>
        [Test]
        public void Tuning_OtherFiles_Load()
        {
            Assert.That(_data.Tuning.Flood.EnemyResourceGapTiles, Is.EqualTo(Fix64.FromInt(4)));
            Assert.That(_data.Tuning.Earth.RecastingCostFraction, Is.EqualTo(Fix64.Parse("0.25")));
            Assert.That(_data.Tuning.Online.RankedUnlockMatches, Is.EqualTo(10));
            Assert.That(_data.Tuning.Harness.GamesPerMatchupPerMap, Is.EqualTo(500));
            Assert.That(_data.Tuning.Status.SpoutlingDrenchHitsToGround, Is.EqualTo(3));
        }

        /// <summary>Match settings come from match.json.</summary>
        [Test]
        public void CreateMatchSettings_UsesMatchTuning()
        {
            MatchSettings settings = _data.CreateMatchSettings(77UL);
            Assert.That(settings.Seed, Is.EqualTo(77UL));
            Assert.That(settings.TicksPerSecond, Is.EqualTo(20));
            Assert.That(settings.StateHashIntervalTicks, Is.EqualTo(20));
        }

        // ---------------------------------------------------------------- content

        /// <summary>Both playable factions and the neutral one load with the right mechanics.</summary>
        [Test]
        public void Factions_Load_WithMechanics()
        {
            Assert.That(_data.Content.Factions.Count, Is.EqualTo(3));
            Assert.That(_data.Content.Factions["skai"].MechanicId, Is.EqualTo("momentum"));
            Assert.That(_data.Content.Factions["water"].MechanicId, Is.EqualTo("flooding"));
            Assert.That(_data.Content.Factions["neutral"].MechanicId, Is.EqualTo(FactionDefinition.NoMechanic));
            Assert.That(_data.Content.Factions["neutral"].UnitIds.Count, Is.EqualTo(0));
        }

        /// <summary>Every unit a faction lists exists, belongs to that faction, and every mechanic ID has a built-in plug-in.</summary>
        [Test]
        public void Factions_ListOnlyExistingUnits()
        {
            FactionMechanicRegistry registry = BuiltInMechanics.CreateRegistry();
            foreach (KeyValuePair<string, FactionDefinition> pair in _data.Content.Factions)
            {
                foreach (string unitId in pair.Value.UnitIds)
                {
                    Assert.That(_data.Content.Units.ContainsKey(unitId), Is.True, pair.Key + " lists " + unitId);
                    Assert.That(_data.Content.Units[unitId].FactionId, Is.EqualTo(pair.Key));
                }
                Assert.That(pair.Value.MechanicId == FactionDefinition.NoMechanic || registry.Contains(pair.Value.MechanicId), Is.True,
                    pair.Key + " names unregistered mechanic " + pair.Value.MechanicId);
            }
            Assert.That(_data.Content.Validate(), Is.Null);
            Assert.That(_data.Content.Factions["skai"].UnitIds, Is.EqualTo(new[] { "skai.fledgling", "skai.windrunner", "skai.talon_archer", "skai.stormcaller", "skai.roc_rider", "skai.great_aerie", "skai.roost", "skai.windspire", "skai.perch_tower" }));
            Assert.That(_data.Content.Factions["water"].UnitIds, Is.EqualTo(new[] { "water.tender", "water.spoutling", "water.grasper", "water.shellback", "water.ink_lobber", "water.grand_shell", "water.tidepool", "water.shell_forge", "water.coral_turret" }));
        }

        /// <summary>Example unit values and layers.</summary>
        [Test]
        public void Units_Load_WithLayersAndValues()
        {
            Assert.That(_data.Content.Units.Count, Is.EqualTo(18));
            UnitDefinition windrunner = _data.Content.Units["skai.windrunner"];
            Assert.That(windrunner.Layer, Is.EqualTo(MovementLayer.Ground));
            Assert.That(windrunner.SpeedTilesPerSecond, Is.EqualTo(Fix64.Parse("3.5")));
            Assert.That(windrunner.FactionId, Is.EqualTo("skai"));
            Assert.That(_data.Content.Units["skai.fledgling"].Layer, Is.EqualTo(MovementLayer.Ground));
            Assert.That(_data.Content.Units["skai.roc_rider"].Layer, Is.EqualTo(MovementLayer.Air));
            Assert.That(_data.Content.Units["water.spoutling"].Layer, Is.EqualTo(MovementLayer.Water));
            foreach (UnitDefinition unit in _data.Content.Units.Values)
            {
                if (unit.Hq != null || unit.Economy.FootprintTiles > 0)
                {
                    // HQs and buildings have their own placeholder ranges (large health, slow or no movement).
                    continue;
                }
                // Roster placeholder bounds (PHASE1_PLAN step 9): health 40 to 220, speed 1.8 to 5 tiles per second.
                Assert.That(unit.MaxHealth, Is.GreaterThanOrEqualTo(Fix64.FromInt(40)).And.LessThanOrEqualTo(Fix64.FromInt(220)), unit.Id);
                Assert.That(unit.SpeedTilesPerSecond, Is.GreaterThanOrEqualTo(Fix64.Parse("1.8")).And.LessThanOrEqualTo(Fix64.FromInt(5)), unit.Id);
            }
        }

        /// <summary>The grey-box map and the three cycles.</summary>
        [Test]
        public void MapsAndCycles_Load()
        {
            MapDefinition map = _data.Maps["greybox_1v1"];
            Assert.That(map.WidthTiles, Is.EqualTo(64));
            Assert.That(map.HeightTiles, Is.EqualTo(64));
            Assert.That(map.Spawns.Count, Is.EqualTo(2));
            Assert.That(map.Spawns[0], Is.EqualTo(FixVec2.FromInts(8, 8)));
            Assert.That(_data.Cycles.Count, Is.EqualTo(3));
            Assert.That(_data.Cycles["windstorm"].Favours, Is.EqualTo(CycleFavour.Air));
            Assert.That(_data.Cycles["quake"].Favours, Is.EqualTo(CycleFavour.Attackers));
            Assert.That(_data.Cycles["core_surge"].Favours, Is.EqualTo(CycleFavour.Position));
            // Iteration order is ordinal ID order.
            Assert.That(new List<string>(_data.Cycles.Keys), Is.EqualTo(new[] { "core_surge", "quake", "windstorm" }));
        }

        /// <summary>Loading is independent of the order the host lists files in.</summary>
        [Test]
        public void Load_IsIndependentOfFileOrder()
        {
            List<DataSourceFile> files = TestData.ReadAll();
            files.Reverse();
            DataSet reversed = DataLoader.Load(files);
            Assert.That(new List<string>(reversed.Content.Units.Keys), Is.EqualTo(new List<string>(_data.Content.Units.Keys)));
            Assert.That(reversed.Tuning.Match.TideCycleSeconds, Is.EqualTo(_data.Tuning.Match.TideCycleSeconds));
        }

        // ---------------------------------------------------------------- failures

        /// <summary>Asserts the loader rejects the files, with a message that starts with the file and mentions the field and the extra text.</summary>
        private static DataValidationException AssertRejected(List<DataSourceFile> files, string file, string field, string? alsoContains = null)
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(files));
            Assert.That(ex.Message, Does.StartWith(file + ": "), ex.Message);
            Assert.That(ex.Message, Does.Contain("'" + field + "'"), ex.Message);
            Assert.That(ex.File, Is.EqualTo(file));
            Assert.That(ex.Field, Is.EqualTo(field));
            if (alsoContains != null)
            {
                Assert.That(ex.Message, Does.Contain(alsoContains), ex.Message);
            }
            return ex;
        }

        /// <summary>A required unit field is missing.</summary>
        [Test]
        public void MissingField_Throws_NamingFileAndField()
        {
            List<DataSourceFile> files = TestData.WithEdit("units/skai/windrunner.json", "\"max_health\": 60,", string.Empty);
            DataValidationException ex = AssertRejected(files, "units/skai/windrunner.json", "max_health", "is missing");
            Assert.That(ex.Message, Is.EqualTo("units/skai/windrunner.json: field 'max_health' is missing"));
        }

        /// <summary>A required tuning key is missing.</summary>
        [Test]
        public void MissingTuningKey_Throws()
        {
            List<DataSourceFile> files = TestData.WithEdit("tuning/supply.json", "\"supply_cap\": 200,", string.Empty);
            AssertRejected(files, "tuning/supply.json", "supply_cap", "is missing");
        }

        /// <summary>A number where a string should be, and a string where a number should be.</summary>
        [Test]
        public void WrongType_Throws_NamingFileAndField()
        {
            List<DataSourceFile> files = TestData.WithEdit("units/skai/windrunner.json", "\"speed_tiles_per_second\": 3.5", "\"speed_tiles_per_second\": \"fast\"");
            AssertRejected(files, "units/skai/windrunner.json", "speed_tiles_per_second", "must be a number");
            files = TestData.WithEdit("units/skai/windrunner.json", "\"display_name\": \"Windrunner\"", "\"display_name\": 5");
            AssertRejected(files, "units/skai/windrunner.json", "display_name", "must be a string");
            files = TestData.WithEdit("tuning/hq.json", "\"last_stand_enabled\": false", "\"last_stand_enabled\": 0");
            AssertRejected(files, "tuning/hq.json", "last_stand_enabled", "true or false");
            files = TestData.WithEdit("units/skai/windrunner.json", "\"supply\": 1", "\"supply\": 1.5");
            AssertRejected(files, "units/skai/windrunner.json", "supply", "whole number");
        }

        /// <summary>A negative speed is out of range.</summary>
        [Test]
        public void NegativeSpeed_Throws_NamingFileAndField()
        {
            List<DataSourceFile> files = TestData.WithEdit("units/skai/windrunner.json", "\"speed_tiles_per_second\": 3.5", "\"speed_tiles_per_second\": -1");
            AssertRejected(files, "units/skai/windrunner.json", "speed_tiles_per_second", "from 0 to 100");
        }

        /// <summary>A tuning value outside the documented range.</summary>
        [Test]
        public void TuningOutOfRange_Throws()
        {
            List<DataSourceFile> files = TestData.WithEdit("tuning/match.json", "\"tide_cycle_seconds\": 210", "\"tide_cycle_seconds\": 5");
            AssertRejected(files, "tuning/match.json", "tide_cycle_seconds", "from 60 to 600");
        }

        /// <summary>A misspelled key in a tuning file or a unit file.</summary>
        [Test]
        public void UnknownKey_Throws_NamingFileAndKey()
        {
            List<DataSourceFile> files = TestData.WithEdit("tuning/hq.json", "\"root_seconds\": 10,", "\"root_seconds\": 10, \"rot_seconds\": 3,");
            AssertRejected(files, "tuning/hq.json", "rot_seconds", "not a known key");
            files = TestData.WithEdit("units/water/grasper.json", "\"cost_rare\": 0", "\"cost_rare\": 0, \"armour\": 3");
            AssertRejected(files, "units/water/grasper.json", "armour", "not a known key");
        }

        /// <summary>Schema versions other than 1 are refused, and the version key is mandatory.</summary>
        [Test]
        public void BadSchemaVersion_Throws_NamingFile()
        {
            List<DataSourceFile> files = TestData.WithEdit("tuning/match.json", "\"$schema_version\": 1", "\"$schema_version\": 2");
            DataValidationException ex = AssertRejected(files, "tuning/match.json", "$schema_version", "found 2");
            Assert.That(ex.Message, Does.Contain("must be 1"));
            files = TestData.WithEdit("units/skai/fledgling.json", "\"$schema_version\": 1", "\"$schema_version\": \"one\"");
            AssertRejected(files, "units/skai/fledgling.json", "$schema_version");
            files = TestData.WithEdit("maps/greybox_1v1.json", "\"$schema_version\": 1,", string.Empty);
            AssertRejected(files, "maps/greybox_1v1.json", "$schema_version", "is missing");
        }

        /// <summary>A syntax error names the file, line and column.</summary>
        [Test]
        public void SyntaxError_NamesFileLineAndColumn()
        {
            List<DataSourceFile> files = TestData.WithEdit("cycles/quake.json", "\"favours\": \"attackers\",", "\"favours\": ,");
            DataValidationException ex = Expect.Throws<JsonParseException>(() => DataLoader.Load(files));
            Assert.That(ex.Message, Does.StartWith("cycles/quake.json: line "));
            Assert.That(ex.Message, Does.Contain("column"));
        }

        /// <summary>A missing tuning file is a file-level error.</summary>
        [Test]
        public void MissingTuningFile_Throws()
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.Without("tuning/online.json")));
            Assert.That(ex.Message, Does.StartWith("tuning/online.json: "));
            Assert.That(ex.Message, Does.Contain("missing"));
        }

        /// <summary>A faction listing a unit that has no file.</summary>
        [Test]
        public void FactionListingUnknownUnit_Throws()
        {
            List<DataSourceFile> files = TestData.WithEdit("factions/skai.json", "\"skai.windrunner\"", "\"skai.windrunnerr\"");
            AssertRejected(files, "factions/skai.json", "units[1]", "skai.windrunnerr");
        }

        /// <summary>A unit pointing at a faction that does not exist, and a unit whose id disagrees with its path.</summary>
        [Test]
        public void BrokenReferences_Throw()
        {
            List<DataSourceFile> files = TestData.WithEdit("units/skai/windrunner.json", "\"id\": \"skai.windrunner\"", "\"id\": \"skai.wind_runner\"");
            AssertRejected(files, "units/skai/windrunner.json", "id", "skai.windrunner");
            files = TestData.WithEdit("units/skai/windrunner.json", "\"faction_id\": \"skai\"", "\"faction_id\": \"water\"");
            AssertRejected(files, "units/skai/windrunner.json", "faction_id", "folder");
            files = TestData.Without("factions/water.json");
            Expect.Throws<DataValidationException>(() => DataLoader.Load(files));
        }

        /// <summary>Layer, map and cycle value checks.</summary>
        [Test]
        public void BadEnumsAndMaps_Throw()
        {
            List<DataSourceFile> files = TestData.WithEdit("units/skai/roc_rider.json", "\"layer\": \"air\"", "\"layer\": \"sky\"");
            AssertRejected(files, "units/skai/roc_rider.json", "layer", "\"ground\", \"water\" or \"air\"");
            files = TestData.WithEdit("cycles/windstorm.json", "\"favours\": \"air\"", "\"favours\": \"everyone\"");
            AssertRejected(files, "cycles/windstorm.json", "favours");
            files = TestData.WithEdit("maps/greybox_1v1.json", "{ \"x\": 56, \"y\": 56 }", "{ \"x\": 156, \"y\": 56 }");
            AssertRejected(files, "maps/greybox_1v1.json", "spawns[1].x");
            files = TestData.WithEdit("maps/greybox_1v1.json", ",\n    { \"x\": 56, \"y\": 56 }", string.Empty);
            AssertRejected(files, "maps/greybox_1v1.json", "spawns", "2 to 16");
        }

        /// <summary>Files outside the known folders, files that are not JSON and duplicate paths are refused.</summary>
        [Test]
        public void StrayFiles_Throw()
        {
            List<DataSourceFile> files = TestData.ReadAll();
            files.Add(new DataSourceFile("misc/notes.json", "{}"));
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(files)).Message, Does.StartWith("misc/notes.json: "));
            files = TestData.ReadAll();
            files.Add(new DataSourceFile("units/skai/readme.txt", "hello"));
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(files)).Message, Does.StartWith("units/skai/readme.txt: "));
            files = TestData.ReadAll();
            files.Add(files[0]);
            Assert.That(Expect.Throws<DataValidationException>(() => DataLoader.Load(files)).Message, Does.StartWith(files[0].Path + ": "));
        }
    }
}

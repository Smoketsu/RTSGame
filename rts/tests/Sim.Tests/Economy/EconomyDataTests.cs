// rts/tests/Sim.Tests/Economy/EconomyDataTests.cs
// Tests that the shipped data files carry the HQ, economy and building values the economy system reads (placeholder numbers from
// rts/data), and that the data loader rejects bad HQ and train references with a message naming the file and field.
// Part of Rts.Sim.Tests. Loads the real rts/data folder through TestData.
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Shipped HQ, building and economy data, and loader validation of it.</summary>
    [TestFixture]
    public class EconomyDataTests
    {
        /// <summary>Both factions name an HQ unit with HQ rules; the Great Aerie flies and is faster than the swimming Grand Shell; the economy tuning is read.</summary>
        [Test]
        public void ShippedData_HqsBuildingsAndEconomyTuning()
        {
            DataSet data = TestData.LoadAll();
            Assert.That(data.Content.Factions["skai"].HqUnitId, Is.EqualTo("skai.great_aerie"));
            Assert.That(data.Content.Factions["water"].HqUnitId, Is.EqualTo("water.grand_shell"));
            Assert.That(data.Content.Factions[FactionDefinition.NeutralId].HqUnitId, Is.Null);

            UnitDefinition aerie = data.Content.Units["skai.great_aerie"];
            UnitDefinition shell = data.Content.Units["water.grand_shell"];
            Assert.That(aerie.Hq, Is.Not.Null);
            Assert.That(shell.Hq, Is.Not.Null);
            // Ground, not air: a rooted nest must be hittable by ground weapons (no base is unhittable).
            Assert.That(aerie.Layer, Is.EqualTo(MovementLayer.Ground));
            Assert.That(shell.Layer, Is.EqualTo(MovementLayer.Water));
            Assert.That(aerie.SpeedTilesPerSecond, Is.GreaterThan(shell.SpeedTilesPerSecond));
            Assert.That(aerie.Weapon, Is.Not.Null);
            Assert.That(aerie.Hq!.RootedArmourType, Is.EqualTo("structure"));
            Assert.That(aerie.Hq.UprootedArmourType, Is.EqualTo("armoured"));
            Assert.That(aerie.Hq.WorkerUnitId, Is.EqualTo("skai.fledgling"));
            Assert.That(shell.Hq!.WorkerUnitId, Is.EqualTo("water.tender"));
            Assert.That(data.Content.Units["skai.fledgling"].Economy.CanGather, Is.True);
            Assert.That(data.Content.Units["water.tender"].Economy.CanGather, Is.True);
            Assert.That(data.Content.Units["skai.windrunner"].Economy.CanGather, Is.False);

            UnitDefinition roost = data.Content.Units["skai.roost"];
            Assert.That(roost.Economy.Trains, Is.EqualTo(new[] { "skai.windrunner", "skai.roc_rider" }));
            Assert.That(roost.Economy.ProvidesSupply, Is.EqualTo(8));
            Assert.That(roost.Economy.FootprintTiles, Is.EqualTo(1));
            UnitDefinition tidepool = data.Content.Units["water.tidepool"];
            Assert.That(tidepool.Economy.Trains, Is.EqualTo(new[] { "water.spoutling", "water.grasper" }));
            Assert.That(tidepool.Economy.ProvidesSupply, Is.EqualTo(8));
            foreach (string id in new[] { "skai.perch_tower", "water.coral_turret" })
            {
                UnitDefinition tower = data.Content.Units[id];
                Assert.That(tower.Economy.IsStaticDefence, Is.True, id);
                Assert.That(tower.Economy.FootprintTiles, Is.EqualTo(1), id);
                Assert.That(tower.Weapon, Is.Not.Null, id);
            }
            foreach (UnitDefinition unit in data.Content.Units.Values)
            {
                Assert.That(unit.Economy.BuildSeconds, Is.GreaterThan(Fix64.Zero), unit.Id);
            }

            EconomyTuning economy = data.Tuning.Economy;
            Assert.That(economy.StartingCommon, Is.EqualTo(200));
            Assert.That(economy.StartingRare, Is.EqualTo(0));
            Assert.That(economy.BaseSupply, Is.EqualTo(10));
            Assert.That(economy.GatherAmountPerTrip, Is.EqualTo(5));
            Assert.That(economy.GatherSecondsPerTrip, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(economy.GatherTicksPerTrip(new MatchClock(20)), Is.EqualTo(40));
            Assert.That(economy.WorkerSpeedMultiplierWhileCarrying, Is.EqualTo(Fix64.One));
            Assert.That(economy.MaxQueueLength, Is.EqualTo(5));
            Assert.That(economy.BaseRadiusTiles, Is.EqualTo(Fix64.FromInt(12)));
            Assert.That(data.Tuning.Hq.HqRootMaxDistanceToFieldTiles, Is.EqualTo(Fix64.FromInt(6)));
        }

        /// <summary>The loader names the file and field of a bad HQ unit, a dangling train entry, a missing hq_unit and an out-of-range economy value.</summary>
        [Test]
        public void Loader_RejectsBadHqAndTrainReferences()
        {
            // hq_unit naming a unit that is not an HQ.
            DataValidationException notHq = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("factions/skai.json", "\"hq_unit\": \"skai.great_aerie\"", "\"hq_unit\": \"skai.windrunner\"")));
            Assert.That(notHq.Message, Does.Contain("factions/skai.json").And.Contain("hq_unit").And.Contain("not an HQ"));
            // hq_unit naming a unit of the other faction.
            DataValidationException wrongFaction = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("factions/skai.json", "\"hq_unit\": \"skai.great_aerie\"", "\"hq_unit\": \"water.grand_shell\"")));
            Assert.That(wrongFaction.Message, Does.Contain("factions/skai.json").And.Contain("hq_unit").And.Contain("belongs to faction"));
            // hq_unit missing for a faction that lists units.
            DataValidationException missing = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("factions/water.json", "\"hq_unit\": \"water.grand_shell\",", string.Empty)));
            Assert.That(missing.Message, Does.Contain("factions/water.json").And.Contain("hq_unit").And.Contain("is missing"));
            // trains naming a unit that does not exist.
            DataValidationException dangling = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("units/skai/roost.json", "\"trains\": [\"skai.windrunner\", \"skai.roc_rider\"]", "\"trains\": [\"skai.ghost\"]")));
            Assert.That(dangling.Message, Does.Contain("units/skai/roost.json").And.Contain("trains").And.Contain("skai.ghost"));
            // An HQ whose worker is not a worker.
            DataValidationException badWorker = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("units/skai/great_aerie.json", "\"worker_unit\": \"skai.fledgling\"", "\"worker_unit\": \"skai.windrunner\"")));
            Assert.That(badWorker.Message, Does.Contain("units/skai/great_aerie.json").And.Contain("hq.worker_unit"));
            // An out-of-range economy value names the new tuning file.
            DataValidationException range = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("tuning/economy.json", "\"base_supply\": 10", "\"base_supply\": 999")));
            Assert.That(range.Message, Does.Contain("tuning/economy.json").And.Contain("base_supply"));
            // An unknown key in the HQ tuning is still refused, and the new key is required.
            DataValidationException noRoot = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("tuning/hq.json", "\"hq_root_max_distance_to_field_tiles\": 6,", string.Empty)));
            Assert.That(noRoot.Message, Does.Contain("tuning/hq.json").And.Contain("hq_root_max_distance_to_field_tiles"));
        }

        /// <summary>A unit with a footprint and a speed above 0 is refused by the loader, naming the file and the footprint field; speed 0 loads.</summary>
        [Test]
        public void Loader_RejectsAFootprintOnAMovingUnit()
        {
            // The roost is a building (speed 0, footprint 1); giving it speed makes it a walking footprint.
            DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("units/skai/roost.json", "\"speed_tiles_per_second\": 0,", "\"speed_tiles_per_second\": 1.5,")));
            Assert.That(ex.Message, Does.Contain("units/skai/roost.json").And.Contain("footprint").And.Contain("speed_tiles_per_second"));
            Expect.DoesNotThrow(() => DataLoader.Load(TestData.ReadAll()));
        }

        /// <summary>spawn_search_radius_tiles loads (shipped value 3), is range checked and is a required key.</summary>
        [Test]
        public void SpawnSearchRadius_IsTuningData()
        {
            Assert.That(TestData.LoadAll().Tuning.Economy.SpawnSearchRadiusTiles, Is.EqualTo(3));
            DataValidationException range = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("tuning/economy.json", "\"spawn_search_radius_tiles\": 3", "\"spawn_search_radius_tiles\": 0")));
            Assert.That(range.Message, Does.Contain("tuning/economy.json").And.Contain("spawn_search_radius_tiles"));
            Assert.That(DataLoader.Load(TestData.WithEdit("tuning/economy.json", "\"spawn_search_radius_tiles\": 3", "\"spawn_search_radius_tiles\": 5")).Tuning.Economy.SpawnSearchRadiusTiles, Is.EqualTo(5));
        }

        /// <summary>Starting a match checks the status durations at the lowest tick rate and the smallest allowed durations, so a bad value cannot surface mid-step.</summary>
        [Test]
        public void Start_AcceptsTheShortestStatusDurationsAtTheSlowestTickRate()
        {
            Tuning shortest = DataLoader.Load(TestData.WithEdit("tuning/statuses.json", "\"drench_slow_duration_seconds\": 2,", "\"drench_slow_duration_seconds\": 0.1,")).Tuning;
            Assert.That(shortest.Status.DrenchSlowDurationTicks(new MatchClock(MatchSettings.MinTicksPerSecond)), Is.GreaterThanOrEqualTo(1));
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(1, MatchSettings.MinTicksPerSecond, 10), TestWorld.Catalog(), TestWorld.TwoPlayers(),
                Rts.Sim.Factions.BuiltInMechanics.CreateRegistry(), shortest, TestWorld.FlatMap()));
            Expect.DoesNotThrow(() => sim.Start());
        }
    }
}

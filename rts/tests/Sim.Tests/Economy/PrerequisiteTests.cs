// rts/tests/Sim.Tests/Economy/PrerequisiteTests.cs
// Acceptance tests T-1 to T-11 of package T of rts/docs/specs/walls_and_tech.md (tech prerequisites): a unit or structure definition may list
// `requires` (own complete buildings of the same faction); train_unit and build_structure reject with missing_prerequisite when one is
// missing, nothing is undone when a prerequisite dies, direct spawns ignore the list, and the loader checks references and cycles.
// Part of Rts.Sim.Tests. Economy setup: TestWorld's economy map, shipped tuning, worker speed 4, placement 40 ticks; a "producer" is a
// test.gate_barracks spawned complete for P0 at (12, 16).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>The prerequisite rule: commands, query, loader and shipped data.</summary>
    [TestFixture]
    public sealed class PrerequisiteTests
    {
        /// <summary>Tech building: footprint 1, 200 health, cost 50, build 5 s, no requirement.</summary>
        private const string TechId = "test.tech";

        /// <summary>Gated unit: mobile ground, speed 4, supply 1, cost 50, build 2 s (40 ticks), requires test.tech.</summary>
        private const string GatedId = "test.gated";

        /// <summary>Producer: building, trains test.gated and test.runner, cost 100, provides 8 supply.</summary>
        private const string BarracksId = "test.gate_barracks";

        /// <summary>Gated structure: footprint 1, cost 10, requires test.tech.</summary>
        private const string GatedTowerId = "test.gated_tower";

        /// <summary>Unit that requires both the tech building and the producer (file order: tech first).</summary>
        private const string DoubleId = "test.double";

        /// <summary>The ally of P0 in T-3 (team 0).</summary>
        private static readonly PlayerId P2 = new PlayerId(2);

        /// <summary>The catalog of the tests: TestWorld's plus the prerequisite units.</summary>
        private static ContentCatalog Catalog()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(new UnitDefinition(TechId, "Tech", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(200), Fix64.Zero, 0, Fix64.Half, 50, 0,
                "armoured", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(5), false, false, new List<string>(), false, 1, 0)));
            catalog.AddUnit(new UnitDefinition(GatedId, "Gated", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 50, 0,
                "light", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(2), false, false, new List<string>(), false, 0, 0, false, new List<string> { TechId })));
            catalog.AddUnit(new UnitDefinition(BarracksId, "Gate barracks", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(200), Fix64.Zero, 0, Fix64.Half, 100, 0,
                "armoured", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(5), false, false, new List<string> { GatedId, TestWorld.RunnerId }, false, 1, 8)));
            catalog.AddUnit(new UnitDefinition(GatedTowerId, "Gated tower", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.Zero, 0, Fix64.Half, 10, 0,
                "armoured", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(2), false, false, new List<string>(), false, 1, 0, false, new List<string> { TechId })));
            catalog.AddUnit(new UnitDefinition(DoubleId, "Double", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 50, 0,
                "light", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(2), false, false, new List<string>(), false, 0, 0, false, new List<string> { TechId, BarracksId })));
            return catalog;
        }

        /// <summary>A started economy simulation with this fixture's catalog; <paramref name="withAlly"/> adds P2 on team 0.</summary>
        private static Simulation Sim(bool withAlly = false)
        {
            List<PlayerSetup> players = TestWorld.TwoPlayers();
            if (withAlly)
            {
                players.Add(new PlayerSetup(P2, new TeamId(0), TestWorld.TestFactionId, "Ally"));
            }
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), Catalog(), players,
                BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.EconomyMap()));
            sim.Start();
            return sim;
        }

        /// <summary>Starts a simulation with a producer for P0 at (12, 16).</summary>
        private static Simulation WithProducer(out EntityId producer, bool withAlly = false)
        {
            Simulation sim = Sim(withAlly);
            producer = Spawn(sim, BarracksId, P0, 12, 16);
            return sim;
        }

        /// <summary>A train command for the current tick.</summary>
        private static TrainUnitCommand Train(Simulation sim, EntityId producer, string definition)
        {
            return new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), producer, definition);
        }

        // ------------------------------------------------------------------ T-1 to T-5

        /// <summary>T-1: rejected without the building (nothing paid, nothing queued); accepted the tick after a complete one is spawned.</summary>
        [Test]
        public void T1_RejectedWithoutTheBuilding()
        {
            Simulation sim = WithProducer(out EntityId producer);
            AssertRejected(sim, Train(sim, producer, GatedId), "missing_prerequisite");
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            Assert.That(Get(sim, producer).ProductionQueue.Count, Is.EqualTo(0));
            Spawn(sim, TechId, P0, 16, 16);
            AssertAccepted(sim, Train(sim, producer, GatedId));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(150));
        }

        /// <summary>T-2: a tech building under construction does not count: the train command on the completion tick is rejected, the next tick's is accepted.</summary>
        [Test]
        public void T2_UnderConstructionDoesNotCount()
        {
            Simulation sim = WithProducer(out EntityId producer);
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            int t0 = sim.Clock.Tick;
            AssertAccepted(sim, new BuildStructureCommand(t0, P0, Seq(), worker, TechId, 20, 14));
            List<SimEvent> events = StepCollect(sim, 161);
            Assert.That(Of<StructurePlacedEvent>(events).Single().Tick, Is.EqualTo(t0 + 62));
            Assert.That(sim.Clock.Tick, Is.EqualTo(t0 + 162));
            AssertRejected(sim, Train(sim, producer, GatedId), "missing_prerequisite");
            Assert.That(Of<StructureCompletedEvent>(new List<SimEvent>(sim.Events.Current)).Single().Tick, Is.EqualTo(t0 + 162));
            AssertAccepted(sim, Train(sim, producer, GatedId));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(100));
        }

        /// <summary>T-3: an enemy's or an ally's tech building does not count.</summary>
        [Test]
        public void T3_OnlyOwnBuildingsCount()
        {
            Simulation enemy = WithProducer(out EntityId producer);
            Spawn(enemy, TechId, P1, 16, 16);
            AssertRejected(enemy, Train(enemy, producer, GatedId), "missing_prerequisite");
            Simulation ally = WithProducer(out EntityId second, true);
            Spawn(ally, TechId, P2, 16, 16);
            AssertRejected(ally, Train(ally, second, GatedId), "missing_prerequisite");
        }

        /// <summary>T-4 and T-5: when the prerequisite dies the queue finishes (T + 39 and T + 79), new orders are refused, ungated units are not, and built units are untouched.</summary>
        [Test]
        public void T4_PrerequisiteDies_QueueFinishes()
        {
            Simulation sim = WithProducer(out EntityId producer);
            EntityId tech = Spawn(sim, TechId, P0, 16, 16);
            int t = sim.Clock.Tick;
            sim.Submit(Train(sim, producer, GatedId));
            sim.Submit(Train(sim, producer, GatedId));
            sim.Step();
            Assert.That(Reasons(new List<SimEvent>(sim.Events.Current)), Is.Empty);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(100));
            int supplyAfter = PlayerOf(sim, P0).SupplyUsed;
            // Trained units by tick.
            List<UnitTrainedEvent> trained = new List<UnitTrainedEvent>();
            // Tick being run, T + 1 to T + 85.
            for (int offset = 1; offset <= 85; offset++)
            {
                if (offset == 10)
                {
                    sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), tech));
                }
                if (offset == 11 || offset == 80)
                {
                    sim.Submit(Train(sim, producer, GatedId));
                }
                if (offset == 80)
                {
                    sim.Submit(Train(sim, producer, TestWorld.RunnerId));
                }
                sim.Step();
                List<SimEvent> events = new List<SimEvent>(sim.Events.Current);
                trained.AddRange(Of<UnitTrainedEvent>(events));
                if (offset == 11)
                {
                    Assert.That(Reasons(events), Is.EqualTo(new List<string> { "missing_prerequisite" }));
                }
                if (offset == 80)
                {
                    Assert.That(Reasons(events), Is.EqualTo(new List<string> { "missing_prerequisite" }), "the gated one is rejected, the runner accepted");
                }
            }
            Assert.That(supplyAfter, Is.EqualTo(PlayerOf(sim, P0).SupplyUsed - 1 + 0), "two gated units plus one runner now");
            List<int> gatedTicks = trained.FindAll(e => e.DefinitionId == GatedId).ConvertAll(e => e.Tick);
            Assert.That(gatedTicks, Is.EqualTo(new List<int> { t + 39, t + 79 }));
            // T-5: both built units take move orders and keep their health.
            foreach (UnitTrainedEvent unit in trained.FindAll(e => e.DefinitionId == GatedId))
            {
                AssertAccepted(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), unit.Unit, At(30, 30)));
                Assert.That(Get(sim, unit.Unit).Health, Is.EqualTo(Fix64.FromInt(100)));
            }
        }

        /// <summary>T-6: a gated structure is rejected before out_of_bounds and site_unexplored; with the tech complete it is accepted and still placed after the tech is removed, with no refund.</summary>
        [Test]
        public void T6_GatedStructure_AndRejectionOrder()
        {
            Simulation sim = Sim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, GatedTowerId, -1, 5), "missing_prerequisite");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, GatedTowerId, 60, 60), "missing_prerequisite");
            EntityId tech = Spawn(sim, TechId, P0, 16, 16);
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, GatedTowerId, 20, 14));
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), tech));
            List<SimEvent> events = StepCollect(sim, 80);
            Assert.That(Of<StructurePlacedEvent>(events).Count, Is.EqualTo(1));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(190), "10 paid, nothing refunded");
        }

        /// <summary>T-7: spawn_unit ignores requires.</summary>
        [Test]
        public void T7_DirectSpawnsIgnoreRequires()
        {
            Simulation sim = Sim();
            EntityId gated = Spawn(sim, GatedId, P0, 20, 20);
            Assert.That(Get(sim, gated).Definition.Id, Is.EqualTo(GatedId));
        }

        /// <summary>T-8: FirstMissingRequirement answers the first unmet entry in file order, and null once all are met.</summary>
        [Test]
        public void T8_Query()
        {
            Simulation sim = Sim();
            UnitDefinition gated = sim.Content.Units[GatedId];
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, gated), Is.EqualTo(TechId));
            Spawn(sim, TechId, P0, 16, 16);
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, gated), Is.Null);
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, sim.Content.Units[TestWorld.RunnerId]), Is.Null);
            Simulation other = Sim();
            UnitDefinition both = other.Content.Units[DoubleId];
            Assert.That(EconomyRules.FirstMissingRequirement(other, P0, both), Is.EqualTo(TechId), "neither");
            Spawn(other, BarracksId, P0, 12, 16);
            Assert.That(EconomyRules.FirstMissingRequirement(other, P0, both), Is.EqualTo(TechId), "only the producer");
            Spawn(other, TechId, P0, 16, 16);
            Assert.That(EconomyRules.FirstMissingRequirement(other, P0, both), Is.Null);
        }

        // ------------------------------------------------------------------ T-9: loader

        /// <summary>The shipped files with <c>"requires": [...]</c> inserted before the footprint line of each named unit file (path, list text).</summary>
        private static List<DataSourceFile> WithRequires(params (string Path, string List)[] edits)
        {
            List<DataSourceFile> files = TestData.ReadAll();
            foreach ((string path, string list) in edits)
            {
                int at = files.FindIndex(f => f.Path == path);
                Assert.That(at, Is.GreaterThanOrEqualTo(0), path);
                Assert.That(files[at].Text, Does.Contain("\"footprint\": 1,"), path);
                files[at] = new DataSourceFile(path, files[at].Text.Replace("\"footprint\": 1,", "\"requires\": " + list + ",\n  \"footprint\": 1,"));
            }
            return files;
        }

        /// <summary>The thunderhead file with its requires list replaced.</summary>
        private static List<DataSourceFile> Thunderhead(string list)
        {
            return TestData.WithEdit("units/skai/thunderhead.json", "\"requires\": [\"skai.storm_spire\"]", "\"requires\": " + list);
        }

        /// <summary>T-9: every bad list is rejected naming the file and the field; a chain loads.</summary>
        [Test]
        public void T9_Loader()
        {
            (string List, string Field, string Text)[] bad =
            {
                ("[\"skai.nope\"]", "requires[0]", "no file in units/"),
                ("[\"skai.storm_spire\", \"skai.storm_spire\"]", "requires", "twice"),
                ("[\"skai.thunderhead\"]", "requires[0]", "requires itself"),
                ("[\"skai.windrunner\"]", "requires[0]", "not a building"),
                ("[\"water.reef_workshop\"]", "requires[0]", "same faction"),
                ("[\"a1\", \"a2\", \"a3\", \"a4\", \"a5\", \"a6\", \"a7\", \"a8\", \"a9\"]", "requires", "more than 8"),
            };
            foreach ((string list, string field, string text) in bad)
            {
                DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(Thunderhead(list)));
                Assert.That(ex.Message, Does.Contain("thunderhead.json"), list);
                Assert.That(ex.Message, Does.Contain(text), list);
                Assert.That(ex.Message, Does.Contain(field), list);
            }
            // Two-cycle: the lower ID (storm_spire) is named and starts the path.
            DataValidationException two = Expect.Throws<DataValidationException>(() => DataLoader.Load(WithRequires(
                ("units/skai/storm_spire.json", "[\"skai.wind_baffle\"]"), ("units/skai/wind_baffle.json", "[\"skai.storm_spire\"]"))));
            Assert.That(two.Message, Does.Contain("storm_spire.json"));
            Assert.That(two.Message, Does.Contain("requirement cycle: skai.storm_spire -> skai.wind_baffle -> skai.storm_spire"));
            // Three-cycle: perch_tower requires storm_spire requires wind_baffle requires perch_tower; written from the lowest ID.
            DataValidationException three = Expect.Throws<DataValidationException>(() => DataLoader.Load(WithRequires(
                ("units/skai/perch_tower.json", "[\"skai.storm_spire\"]"), ("units/skai/storm_spire.json", "[\"skai.wind_baffle\"]"), ("units/skai/wind_baffle.json", "[\"skai.perch_tower\"]"))));
            Assert.That(three.Message, Does.Contain("perch_tower.json"));
            Assert.That(three.Message, Does.Contain("requirement cycle: skai.perch_tower -> skai.storm_spire -> skai.wind_baffle -> skai.perch_tower"));
            // A chain without a cycle loads.
            DataSet chain = DataLoader.Load(WithRequires(("units/skai/storm_spire.json", "[\"skai.wind_baffle\"]"), ("units/skai/wind_baffle.json", "[\"skai.perch_tower\"]")));
            Assert.That(chain.Content.Units["skai.storm_spire"].Economy.Requires, Is.EqualTo(new[] { "skai.wind_baffle" }));
        }

        /// <summary>T-10: shipped data: exactly the Thunderhead and the Leviathan have requires, and the tech buildings have the spec values.</summary>
        [Test]
        public void T10_ShippedData()
        {
            DataSet data = TestData.LoadAll();
            List<string> gated = data.Content.Units.Values.Where(u => u.Economy.Requires.Count > 0).Select(u => u.Id).ToList();
            Assert.That(gated, Is.EqualTo(new List<string> { "skai.thunderhead", "water.leviathan" }));
            Assert.That(data.Content.Units["skai.thunderhead"].Economy.Requires, Is.EqualTo(new[] { "skai.storm_spire" }));
            Assert.That(data.Content.Units["water.leviathan"].Economy.Requires, Is.EqualTo(new[] { "water.reef_workshop" }));
            foreach ((string faction, string id) in new[] { ("skai", "skai.storm_spire"), ("water", "water.reef_workshop") })
            {
                Assert.That(data.Content.Factions[faction].UnitIds, Does.Contain(id));
                UnitDefinition tech = data.Content.Units[id];
                Assert.That((tech.MaxHealth, tech.CostCommon, tech.CostRare), Is.EqualTo((Fix64.FromInt(400), 150, 50)));
                Assert.That((tech.Economy.BuildSeconds, tech.Economy.FootprintTiles, tech.Supply), Is.EqualTo((Fix64.FromInt(30), 1, 0)));
                Assert.That((tech.ArmourType, tech.SightRangeTiles), Is.EqualTo(("structure", Fix64.FromInt(8))));
                Assert.That(tech.Economy.Trains, Is.Empty);
                Assert.That(tech.Economy.Requires, Is.Empty);
                Assert.That(tech.Weapon, Is.Null);
            }
        }

        /// <summary>T-11: a changed requires entry changes the content digest.</summary>
        [Test]
        public void T11_DigestMoves()
        {
            ulong one = DataLoader.Load(Thunderhead("[\"skai.storm_spire\"]")).Content.ComputeDigest();
            ulong two = DataLoader.Load(Thunderhead("[\"skai.perch_tower\"]")).Content.ComputeDigest();
            Assert.That(two, Is.Not.EqualTo(one));
        }
    }
}

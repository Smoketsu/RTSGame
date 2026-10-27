// rts/tests/Sim.Tests/Cycles/StandardMatchStartTests.cs
// Tests for Rts.Sim.Data.StandardMatchStart (rts/docs/specs/balance_harness.md section 2): each player's faction HQ on the map's spawn
// point for its slot, then starting_worker_count workers on the first free tiles of rings 1 to 3 around it, as script-issued
// spawn_unit commands at tick 0. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;

namespace Rts.Sim.Tests.Cycles
{
    /// <summary>The standard start of a skirmish match.</summary>
    [TestFixture]
    public class StandardMatchStartTests
    {
        /// <summary>Skai in slot 0 against Water in slot 1.</summary>
        private static List<PlayerSetup> SkaiVsWater()
        {
            return new List<PlayerSetup>
            {
                new PlayerSetup(CycleWorld.P0, CycleWorld.T0, "skai", "Skai"),
                new PlayerSetup(CycleWorld.P1, CycleWorld.T1, "water", "Water"),
            };
        }

        [Test]
        public void Commands_PlaceTheHqOnTheSpawnPointAndTheWorkersAroundIt()
        {
            DataSet data = TestData.LoadAll();
            MapDefinition map = data.Maps["greybox_1v1"];
            List<Command> commands = StandardMatchStart.Commands(data, map, SkaiVsWater());
            Assert.That(commands.Count, Is.EqualTo(10), "two HQs and four workers each");
            for (int i = 0; i < commands.Count; i++)
            {
                SpawnUnitCommand spawn = (SpawnUnitCommand)commands[i];
                Assert.That(spawn.Tick, Is.EqualTo(0));
                Assert.That(spawn.Issuer, Is.EqualTo(PlayerId.None));
                Assert.That(spawn.Sequence, Is.EqualTo((uint)i));
            }
            SpawnUnitCommand skaiHq = (SpawnUnitCommand)commands[0];
            Assert.That(skaiHq.DefinitionId, Is.EqualTo("skai.great_aerie"));
            Assert.That(skaiHq.Owner, Is.EqualTo(CycleWorld.P0));
            Assert.That(skaiHq.Position, Is.EqualTo(map.Spawns[0]), "exactly the spawn point the map gives");
            SpawnUnitCommand waterHq = (SpawnUnitCommand)commands[5];
            Assert.That(waterHq.DefinitionId, Is.EqualTo("water.grand_shell"));
            Assert.That(waterHq.Owner, Is.EqualTo(CycleWorld.P1));
            Assert.That(waterHq.Position, Is.EqualTo(map.Spawns[1]));
            // The Skai workers: the first four free tiles of ring 1 around the spawn tile, ascending tile index, at tile centres.
            int cx = map.Spawns[0].X.FloorToInt();
            int cy = map.Spawns[0].Y.FloorToInt();
            List<FixVec2> expected = new List<FixVec2>();
            for (int y = cy - 1; y <= cy + 1 && expected.Count < 4; y++)
            {
                for (int x = cx - 1; x <= cx + 1 && expected.Count < 4; x++)
                {
                    if ((x != cx || y != cy) && map.HeightAt(y * map.WidthTiles + x) == map.HeightAt(cy * map.WidthTiles + cx) && !map.IsRampAt(y * map.WidthTiles + x))
                    {
                        expected.Add(new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half));
                    }
                }
            }
            Assert.That(expected.Count, Is.EqualTo(4), "the greybox spawn has four free tiles on ring 1");
            for (int i = 0; i < 4; i++)
            {
                SpawnUnitCommand worker = (SpawnUnitCommand)commands[1 + i];
                Assert.That(worker.DefinitionId, Is.EqualTo("skai.fledgling"));
                Assert.That(worker.Owner, Is.EqualTo(CycleWorld.P0));
                Assert.That(worker.Position, Is.EqualTo(expected[i]));
            }
        }

        [Test]
        public void Commands_AreTheSameWhateverOrderThePlayersAreListedIn()
        {
            DataSet data = TestData.LoadAll();
            MapDefinition map = data.Maps["greybox_1v1"];
            List<PlayerSetup> reversed = SkaiVsWater();
            reversed.Reverse();
            List<Command> a = StandardMatchStart.Commands(data, map, SkaiVsWater());
            List<Command> b = StandardMatchStart.Commands(data, map, reversed);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].ToString(), Is.EqualTo(a[i].ToString()));
                Assert.That(((SpawnUnitCommand)b[i]).Position, Is.EqualTo(((SpawnUnitCommand)a[i]).Position));
                Assert.That(((SpawnUnitCommand)b[i]).DefinitionId, Is.EqualTo(((SpawnUnitCommand)a[i]).DefinitionId));
            }
        }

        [Test]
        public void Commands_RunInASimulationWithoutRejections()
        {
            DataSet data = TestData.LoadAll();
            MapDefinition map = data.Maps["greybox_1v1"];
            List<PlayerSetup> players = SkaiVsWater();
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(1234), data.Content, players, BuiltInMechanics.CreateRegistry(),
                data.Tuning, map, data.Cycles.Values));
            sim.Start();
            foreach (Command command in StandardMatchStart.Commands(data, map, players))
            {
                sim.Submit(command);
            }
            sim.Step();
            Assert.That(sim.Units.Count, Is.EqualTo(10));
            foreach (SimEvent evt in sim.Events.Current)
            {
                Assert.That(evt, Is.Not.InstanceOf<CommandRejectedEvent>(), evt.ToString());
            }
        }

        [Test]
        public void Commands_WorkerCountComesFromTuning()
        {
            DataSet none = DataLoader.Load(CycleWorld.EditedFiles("tuning/economy.json", "\"starting_worker_count\": 4", "\"starting_worker_count\": 0"));
            Assert.That(StandardMatchStart.Commands(none, none.Maps["greybox_1v1"], SkaiVsWater()).Count, Is.EqualTo(2), "only the HQs");
        }

        [Test]
        public void Commands_TooFewWorkerTiles_NamesMapAndSlot()
        {
            DataSet many = DataLoader.Load(CycleWorld.EditedFiles("tuning/economy.json", "\"starting_worker_count\": 4", "\"starting_worker_count\": 20"));
            // A tiny map whose slot-0 spawn sits in a corner: rings 1 to 3 hold only 3 + 5 + 7 = 15 tiles there.
            MapDefinition corner = new MapDefinition("corner_map", "Corner", 8, 8, new List<FixVec2> { new FixVec2(Fix64.Half, Fix64.Half), FixVec2.FromInts(5, 5) });
            InvalidOperationException error = Expect.Throws<InvalidOperationException>(() => StandardMatchStart.Commands(many, corner, SkaiVsWater()));
            Assert.That(error.Message, Does.Contain("corner_map"));
            Assert.That(error.Message, Does.Contain("slot 0"));
        }

        [Test]
        public void Commands_SlotWithoutASpawnPoint_IsAnError()
        {
            DataSet data = TestData.LoadAll();
            List<PlayerSetup> three = new List<PlayerSetup>(SkaiVsWater())
            {
                new PlayerSetup(new PlayerId(5), new TeamId(2), "skai", "Far"),
            };
            InvalidOperationException error = Expect.Throws<InvalidOperationException>(() => StandardMatchStart.Commands(data, data.Maps["greybox_1v1"], three));
            Assert.That(error.Message, Does.Contain("slot 5"));
        }
    }
}

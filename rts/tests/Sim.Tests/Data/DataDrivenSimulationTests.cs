// rts/tests/Sim.Tests/Data/DataDrivenSimulationTests.cs
// End-to-end test: load the real rts/data folder, build a Simulation from it with the built-in mechanics and two
// players (Skai versus Water), spawn a data-defined unit by command, move it, and check it travels at the speed the
// data file declares. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Data
{
    /// <summary>Data loader plus simulation together.</summary>
    [TestFixture]
    public class DataDrivenSimulationTests
    {
        /// <summary>Player 0, Skai, team 0.</summary>
        private static readonly PlayerId Skai = new PlayerId(0);

        /// <summary>Player 1, Water, team 1.</summary>
        private static readonly PlayerId Water = new PlayerId(1);

        /// <summary>Builds and starts a Skai-versus-Water simulation from the real data folder.</summary>
        private static Simulation Build(DataSet data, ulong seed)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(Skai, new TeamId(0), "skai", "Skai player"),
                new PlayerSetup(Water, new TeamId(1), "water", "Water player"),
            };
            SimulationConfig config = new SimulationConfig(data.CreateMatchSettings(seed), data.Content, players, BuiltInMechanics.CreateRegistry());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>Distance a spawned Windrunner covers after one second of movement equals its data-defined speed.</summary>
        [Test]
        public void Windrunner_MovesAtDataDefinedSpeed()
        {
            DataSet data = TestData.LoadAll();
            Simulation sim = Build(data, 1234UL);
            Fix64 speed = data.Content.Units["skai.windrunner"].SpeedTilesPerSecond;
            Assert.That(speed, Is.EqualTo(Fix64.Parse("3.5")));

            sim.Submit(new SpawnUnitCommand(0, Skai, 0, "skai.windrunner", Skai, FixVec2.FromInts(4, 4)));
            sim.Step();
            Unit? unit = null;
            foreach (Unit candidate in sim.Units.All)
            {
                unit = candidate;
            }
            Assert.That(unit, Is.Not.Null);
            Assert.That(unit!.Definition.Id, Is.EqualTo("skai.windrunner"));
            Assert.That(unit.Position, Is.EqualTo(FixVec2.FromInts(4, 4)));

            // Move 35 tiles east, starting on tick 1; after exactly one second (20 ticks) it has covered 'speed' tiles.
            sim.Submit(new MoveUnitCommand(1, Skai, 1, unit.Id, FixVec2.FromInts(39, 4)));
            sim.Step(sim.Clock.TicksPerSecond);
            Fix64 travelled = unit.Position.X - Fix64.FromInt(4);
            Fix64 error = (travelled - speed).Abs();
            // Per-tick division and the vector length truncate by a few raw units (2^-32 tiles) each tick; 0.000001 tiles is far above that and far below any real speed difference.
            Assert.That(error, Is.LessThan(Fix64.Parse("0.000001")));
            Assert.That(unit.Position.Y, Is.EqualTo(Fix64.FromInt(4)));
        }

        /// <summary>A short move arrives after distance / speed seconds (within one tick of truncation).</summary>
        [Test]
        public void Windrunner_ArrivesAfterDistanceOverSpeed()
        {
            DataSet data = TestData.LoadAll();
            Simulation sim = Build(data, 5UL);
            sim.Submit(new SpawnUnitCommand(0, Skai, 0, "skai.windrunner", Skai, FixVec2.FromInts(4, 4)));
            sim.Step();
            // 7 tiles at 3.5 tiles per second is 2 seconds, 40 ticks at 20 ticks per second.
            sim.Submit(new MoveUnitCommand(1, Skai, 1, new EntityId(1), FixVec2.FromInts(11, 4)));
            int arrivedTick = -1;
            for (int i = 0; i < 60 && arrivedTick < 0; i++)
            {
                sim.Step();
                foreach (SimEvent e in sim.Events.Current)
                {
                    if (e is UnitArrivedEvent arrived)
                    {
                        arrivedTick = arrived.Tick;
                    }
                }
            }
            Assert.That(arrivedTick, Is.InRange(40, 42));
        }

        /// <summary>Units of both factions spawn from data, and the same data and seed give the same hash.</summary>
        [Test]
        public void SameDataAndSeed_GiveSameHash_AndBothFactionsSpawn()
        {
            DataSet data = TestData.LoadAll();
            ulong[] hashes = new ulong[2];
            for (int run = 0; run < 2; run++)
            {
                Simulation sim = Build(data, 99UL);
                sim.Submit(new SpawnUnitCommand(0, Skai, 0, "skai.roc_rider", Skai, FixVec2.FromInts(8, 8)));
                sim.Submit(new SpawnUnitCommand(0, Water, 0, "water.grasper", Water, FixVec2.FromInts(56, 56)));
                sim.Step(40);
                Assert.That(sim.Units.Count, Is.EqualTo(2));
                hashes[run] = sim.ComputeStateHash();
            }
            Assert.That(hashes[0], Is.EqualTo(hashes[1]));
        }
    }
}

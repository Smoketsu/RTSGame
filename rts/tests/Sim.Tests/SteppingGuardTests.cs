// rts/tests/Sim.Tests/SteppingGuardTests.cs
// Tests that state-changing simulation entry points refuse to run outside a step (architecture rule 8: events out, never
// in; mutation methods throw outside a step). Covers the mechanics' OnTick, the economy step and the supply recompute.
// Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Skai;
using Rts.Sim.Factions.Water;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests
{
    /// <summary>Guards against mutation from outside Simulation.Step.</summary>
    [TestFixture]
    public class SteppingGuardTests
    {
        /// <summary>A started Skai-versus-Water match from the real data, so both signature mechanics exist.</summary>
        private static Simulation Build()
        {
            // The shipped data, loaded from rts/data.
            DataSet data = TestData.LoadAll();
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "A"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), "water", "B"),
            };
            SimulationConfig config = new SimulationConfig(data.CreateMatchSettings(1), data.Content, players, BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap("greybox_1v1"));
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>The Momentum and Flooding mechanics throw from OnTick when called between steps, and still run inside Step.</summary>
        [Test]
        public void MechanicOnTick_ThrowsOutsideAStep()
        {
            Simulation sim = Build();
            IFactionMechanic momentum = sim.Mechanics[MomentumMechanic.MechanicId];
            IFactionMechanic flooding = sim.Mechanics[FloodingMechanic.MechanicId];
            Expect.Throws<InvalidOperationException>(() => momentum.OnTick(sim));
            Expect.Throws<InvalidOperationException>(() => flooding.OnTick(sim));
            // Inside a step both run (Step calls them), so the guard does not block the normal path.
            Expect.DoesNotThrow(() => sim.Step(3));
        }

        /// <summary>The economy step and the supply recompute throw between steps.</summary>
        [Test]
        public void EconomySystem_ThrowsOutsideAStep()
        {
            Simulation sim = Build();
            Expect.Throws<InvalidOperationException>(() => EconomySystem.Step(sim));
            Expect.Throws<InvalidOperationException>(() => EconomySystem.RecomputeSupply(sim));
            Expect.DoesNotThrow(() => sim.Step(2));
        }

        /// <summary>Wave casting is also guarded (the coordinator's guard): calling the mechanic directly between steps throws.</summary>
        [Test]
        public void CastWave_ThrowsOutsideAStep()
        {
            Simulation sim = Build();
            FloodingMechanic flooding = (FloodingMechanic)sim.Mechanics[FloodingMechanic.MechanicId];
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, "skai.windrunner", new PlayerId(0), FixVec2.FromInts(20, 4)));
            sim.Step();
            // Any live unit will do: the guard is the first statement.
            Unit unit = null!;
            foreach (Unit candidate in sim.Units.All)
            {
                unit = candidate;
            }
            Assert.That(unit, Is.Not.Null);
            Expect.Throws<InvalidOperationException>(() => flooding.CastWave(sim, unit, 1, 0));
        }
    }
}

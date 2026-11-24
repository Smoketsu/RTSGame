// rts/tests/Sim.Tests/Data/ReefRunnerTests.cs
// Package A tests A-8 and A-9 of rts/docs/specs/skirmish_ai_v3.md: the Reef Runner (water scout) loads with the spec's values, the Tidepool trains it and the water faction
// lists it second, and a Reef Runner crosses the dry greybox_1v1 map on the water layer in the time the path needs. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Data
{
    /// <summary>Reef Runner data and movement.</summary>
    [TestFixture]
    public class ReefRunnerTests
    {
        /// <summary>Goal tile of the crossing test (centre-ish of the far plateau). Unit: tiles. Range: inside greybox_1v1 (64 by 64).</summary>
        private const int GoalX = 55;

        /// <summary>Second coordinate of the goal. Unit: tiles. Range: inside greybox_1v1.</summary>
        private const int GoalY = 54;

        /// <summary>A-8: layer, targets, cost, supply, producer and faction list entry.</summary>
        [Test]
        public void ReefRunner_Loads_WithTheSpecValues()
        {
            DataSet data = TestData.LoadAll();
            UnitDefinition unit = data.Content.Units["water.reef_runner"];
            Assert.That(unit.DisplayName, Is.EqualTo("Reef Runner"));
            Assert.That(unit.Layer, Is.EqualTo(MovementLayer.Water));
            Assert.That(unit.Weapon, Is.Not.Null);
            Assert.That(unit.Weapon!.CanTargetGround, Is.True);
            Assert.That(unit.Weapon.CanTargetWater, Is.True);
            Assert.That(unit.Weapon.CanTargetAir, Is.False, "open question Q2 default: the Reef Runner does not hit air");
            // roster_numbers_1 (specs/roster_numbers_1.md 2.1): health 66 (was 60); every other number of the unit is unchanged.
            Assert.That(unit.MaxHealth, Is.EqualTo(Fix64.FromInt(66)));
            Assert.That(unit.SpeedTilesPerSecond, Is.EqualTo(Fix64.Parse("4.1")));
            Assert.That(unit.Weapon.Damage, Is.EqualTo(Fix64.FromInt(6)));
            Assert.That(unit.Weapon.CooldownSeconds, Is.EqualTo(Fix64.Parse("0.8")));
            Assert.That(unit.Weapon.RangeTiles, Is.EqualTo(Fix64.FromInt(4)));
            Assert.That(unit.CostCommon, Is.EqualTo(75));
            Assert.That(unit.CostRare, Is.EqualTo(0));
            Assert.That(unit.Supply, Is.EqualTo(1));
            Assert.That(data.Content.Units["water.tidepool"].Economy.Trains, Does.Contain("water.reef_runner"));
            Assert.That(data.Content.Factions["water"].UnitIds[1], Is.EqualTo("water.reef_runner"));
        }

        /// <summary>A-9: spawned at (8, 9) with a move order to (55, 54), the unit stands within 1 tile of the goal inside the time limit (measured 407 ticks plus 2 s).</summary>
        [Test]
        public void ReefRunner_CrossesTheDryMap()
        {
            DataSet data = TestData.LoadAll();
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), "water", "Water"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), "skai", "Skai"),
            };
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(1234UL), data.Content, players, Rts.Sim.Factions.BuiltInMechanics.CreateRegistry(),
                data.Tuning, data.RequireMap("greybox_1v1"), data.Cycles.Values));
            sim.Start();
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, "water.reef_runner", new PlayerId(0), new FixVec2(Fix64.FromInt(8), Fix64.FromInt(9))));
            sim.Submit(new MoveUnitCommand(1, new PlayerId(0), 0, new EntityId(1), new FixVec2(Fix64.FromInt(GoalX), Fix64.FromInt(GoalY))));
            // Time allowed: measured arrival at tick 407 (20.35 s; the spec estimated 19.3 s, the real path round the ramps is longer) plus 2 s, at 20 ticks per second. Unit: ticks. Range: 450.
            const int limitTicks = 450;
            // Squared distance to the goal after the last step. Unit: tiles squared.
            Fix64 distanceSquared = Fix64.FromInt(1000000);
            // Tick the unit first stood within 1 tile of the goal, or -1.
            int arrived = -1;
            for (int i = 0; i < limitTicks && arrived < 0; i++)
            {
                sim.Step();
                Assert.That(sim.Units.TryGet(new EntityId(1), out Unit? unit), Is.True);
                FixVec2 offset = new FixVec2(unit!.Position.X - Fix64.FromInt(GoalX), unit.Position.Y - Fix64.FromInt(GoalY));
                distanceSquared = offset.X * offset.X + offset.Y * offset.Y;
                if (distanceSquared <= Fix64.One)
                {
                    arrived = sim.Clock.Tick;
                }
            }
            Assert.That(arrived, Is.GreaterThan(0), "within 22.5 s the unit must stand within 1 tile of the goal (squared distance " + distanceSquared + ")");
        }
    }
}

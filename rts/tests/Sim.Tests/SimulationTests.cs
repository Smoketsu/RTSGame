// rts/tests/Sim.Tests/SimulationTests.cs
// Tests for the tick loop: spawning from a definition, movement at the defined speed, arrival events,
// hash reporting on the interval, and the rule that nothing mutates state outside a step. Part of Rts.Sim.Tests.
using System;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Units;

namespace Rts.Sim.Tests
{
    [TestFixture]
    public sealed class SimulationTests
    {
        [Test]
        public void Start_EmitsMatchStartedAndCreatesMechanics()
        {
            Simulation sim = TestWorld.Started();
            Assert.That(sim.IsStarted, Is.True);
            Assert.That(sim.Events.Current.Count, Is.EqualTo(1));
            Assert.That(sim.Events.Current[0], Is.InstanceOf<MatchStartedEvent>());
            // P0 uses "none", P1 uses "momentum".
            Assert.That(sim.Mechanics.Count, Is.EqualTo(2));
            Assert.That(sim.Mechanics.ContainsKey("momentum"), Is.True);
            Assert.That(sim.Mechanics.ContainsKey("none"), Is.True);
            Expect.Throws<InvalidOperationException>(() => sim.Start());
        }

        [Test]
        public void SpawnedUnit_MovesAtDefinedSpeed()
        {
            Simulation sim = TestWorld.Started();
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
            sim.Step();
            Assert.That(sim.Units.Count, Is.EqualTo(1));
            Assert.That(sim.Units.TryGet(new EntityId(1), out Unit? unit), Is.True);
            Assert.That(unit!.Health, Is.EqualTo(Fix64.FromInt(100)));

            sim.Submit(new MoveUnitCommand(1, new PlayerId(0), 1, unit.Id, FixVec2.FromInts(10, 0)));
            // 2 tiles per second at 20 ticks per second = 0.1 tiles per tick; after 20 ticks the unit is at x = 2.
            sim.Step(20);
            Assert.That(unit.Position.X.Raw, Is.EqualTo(Fix64.FromInt(2).Raw).Within(40));
            Assert.That(unit.HasMoveTarget, Is.True);
            // 10 tiles takes about 100 movement steps; the order completes once the unit is within
            // arrival_radius_tiles (ADR-007), so step tick by tick and watch for the arrival event.
            bool arrived = false;
            for (int i = 0; i < 81 && !arrived; i++)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is UnitArrivedEvent)
                    {
                        arrived = true;
                    }
                }
            }
            Assert.That(arrived, Is.True, "expected a unit_arrived event");
            Assert.That((unit.Position - FixVec2.FromInts(10, 0)).Length() <= sim.Tuning.Terrain.ArrivalRadiusTiles, Is.True);
            Assert.That(unit.HasMoveTarget, Is.False);
        }

        [Test]
        public void EntityIds_AreNeverReused()
        {
            Simulation sim = TestWorld.Started();
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
            sim.Step();
            sim.Submit(new RemoveUnitCommand(1, PlayerId.None, 0, new EntityId(1)));
            sim.Submit(new SpawnUnitCommand(1, PlayerId.None, 1, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
            sim.Step();
            Assert.That(sim.Units.Count, Is.EqualTo(1));
            Assert.That(sim.Units.Contains(new EntityId(1)), Is.False);
            Assert.That(sim.Units.Contains(new EntityId(2)), Is.True);
        }

        [Test]
        public void StateHash_IsReportedOnTheInterval()
        {
            Simulation sim = TestWorld.Started();
            int reports = 0;
            for (int tick = 0; tick < 25; tick++)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is StateHashedEvent hashed)
                    {
                        reports++;
                        Assert.That(hashed.Tick % TestWorld.HashInterval, Is.EqualTo(0));
                        Assert.That(hashed.Hash, Is.Not.EqualTo(0UL));
                    }
                }
            }
            // Ticks 0, 10 and 20.
            Assert.That(reports, Is.EqualTo(3));
            Assert.That(sim.LastReportedHash, Is.Not.EqualTo(0UL));
        }

        [Test]
        public void StateHash_ChangesWhenStateChanges()
        {
            Simulation sim = TestWorld.Started();
            ulong empty = sim.ComputeStateHash();
            sim.Step();
            ulong afterTick = sim.ComputeStateHash();
            Assert.That(afterTick, Is.Not.EqualTo(empty), "advancing the clock must change the hash");
            sim.Submit(new SpawnUnitCommand(1, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
            sim.Step();
            ulong withUnit = sim.ComputeStateHash();
            Assert.That(withUnit, Is.Not.EqualTo(afterTick), "spawning a unit must change the hash");
            Assert.That(sim.ComputeStateHash(), Is.EqualTo(withUnit), "hashing is repeatable");
        }

        [Test]
        public void Mutation_OutsideStep_Throws()
        {
            Simulation sim = TestWorld.Started();
            UnitDefinition definition = sim.Content.Units[TestWorld.RunnerId];
            Expect.Throws<InvalidOperationException>(() => { sim.SpawnUnit(definition, new PlayerId(0), FixVec2.Zero); });
            Expect.Throws<InvalidOperationException>(() => { sim.RemoveUnit(new EntityId(1), "test"); });
        }

        [Test]
        public void Rng_IsLockedOutsideStep()
        {
            Simulation sim = TestWorld.Started();
            Assert.That(sim.Rng.IsLocked, Is.True);
            Expect.Throws<InvalidOperationException>(() => { sim.Rng.NextUInt(); });
            // A command may draw during its step; the draw changes the RNG state and therefore the hash.
            ulong before = sim.ComputeStateHash();
            sim.Submit(new DrawRandomCommand(0, PlayerId.None, 0));
            sim.Step();
            Assert.That(sim.Rng.DrawCount, Is.EqualTo(1UL));
            Assert.That(sim.ComputeStateHash(), Is.Not.EqualTo(before));
            Assert.That(sim.Rng.IsLocked, Is.True);
        }

        [Test]
        public void Content_IsFrozenAfterStart()
        {
            Simulation sim = TestWorld.Started();
            Assert.That(sim.Content.IsFrozen, Is.True);
            Expect.Throws<InvalidOperationException>(() => { sim.Content.AddUnit(sim.Content.Units[TestWorld.RunnerId]); });
            Assert.That(sim.ContentDigest, Is.Not.EqualTo(0UL));
            Assert.That(TestWorld.Catalog().ComputeDigest(), Is.EqualTo(sim.ContentDigest));
        }

        [Test]
        public void StateHash_CoversEveryUnitField()
        {
            // Each hashed unit field, changed on its own, must change the hash (ADR-005).
            Simulation sim = TestWorld.Started();
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
            sim.Step();
            ulong baseline = sim.ComputeStateHash();
            Unit unit = sim.Units.TryGet(new EntityId(1), out Unit? found) ? found! : throw new InvalidOperationException();
            unit.SetPosition(FixVec2.FromInts(1, 0));
            ulong moved = sim.ComputeStateHash();
            Assert.That(moved, Is.Not.EqualTo(baseline), "position");
            unit.SetHealth(Fix64.FromInt(50));
            ulong hurt = sim.ComputeStateHash();
            Assert.That(hurt, Is.Not.EqualTo(moved), "health");
            unit.SetMoveTarget(FixVec2.FromInts(3, 3));
            ulong ordered = sim.ComputeStateHash();
            Assert.That(ordered, Is.Not.EqualTo(hurt), "move target");
            unit.ClearMoveTarget();
            Assert.That(sim.ComputeStateHash(), Is.EqualTo(hurt), "clearing restores the earlier hash");
        }

        /// <summary>Test-only command that draws one random number, to prove draws are legal inside a step.</summary>
        private sealed class DrawRandomCommand : Command
        {
            public DrawRandomCommand(int tick, PlayerId issuer, uint sequence) : base(tick, issuer, sequence)
            {
            }

            public override string Kind => "test_draw_random";

            public override CommandResult Apply(Simulation sim)
            {
                sim.Rng.NextUInt();
                return CommandResult.Ok;
            }

            public override void WriteHash(StateHasher hasher)
            {
                hasher.Write(Kind);
            }
        }

        [Test]
        public void UnknownFaction_FailsAtStart()
        {
            SimulationConfig config = new SimulationConfig(
                new MatchSettings(1, 20, 20),
                TestWorld.Catalog(),
                new[] { new Rts.Sim.Teams.PlayerSetup(new PlayerId(0), new TeamId(0), "fire", "Zed") },
                Rts.Sim.Factions.BuiltInMechanics.CreateRegistry(),
                TestWorld.Tuning,
                TestWorld.FlatMap());
            Simulation sim = new Simulation(config);
            Expect.Throws<InvalidOperationException>(() => sim.Start());
        }

        [Test]
        public void MatchClock_ConvertsDesignDurationsExactly()
        {
            MatchClock clock = new MatchClock(20);
            Assert.That(clock.SecondsToTicks(Fix64.Parse("210")), Is.EqualTo(4200));
            Assert.That(clock.SecondsToTicks(Fix64.Parse("240")), Is.EqualTo(4800));
            Assert.That(clock.SecondsToTicks(Fix64.Parse("1.5")), Is.EqualTo(30));
            Assert.That(clock.SecondsToTicks(Fix64.Parse("0.04")), Is.EqualTo(1));
            Assert.That(clock.SecondsToTicks(Fix64.Zero), Is.EqualTo(0));
            Assert.That(clock.TicksToSeconds(4200), Is.EqualTo(Fix64.FromInt(210)));
            Expect.Throws<ArgumentOutOfRangeException>(() => { clock.SecondsToTicks(Fix64.FromInt(-1)); });
            Expect.Throws<ArgumentOutOfRangeException>(() => { new MatchClock(5); });
        }
    }
}

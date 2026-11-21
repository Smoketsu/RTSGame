// rts/tests/Sim.Tests/Economy/RallyPointTests.cs
// Acceptance tests IS-21 to IS-26 of package I5 of rts/docs/specs/interface_support.md: set_rally_point stores a point on a producer; a unit trained there gets a plain
// move order to it (a worker too, even onto a resource field), the point survives the producer, and it enters the state hash only while set. Part of Rts.Sim.Tests.
// Economy setup: TestWorld.StartedEconomy (20 ticks/s, P0 200 common), a complete barracks of P0 at (12, 16) that trains test.worker (100 ticks); the rally point is (18, 16)
// unless a test says otherwise. T is the tick the first train_unit is applied on.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Commands.Codec;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Statuses;
using Rts.Sim.Units;
using Rts.Sim.Economy;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Rally points on producers.</summary>
    [TestFixture]
    public sealed class RallyPointTests
    {
        /// <summary>The rally point of most tests, 6 tiles east of the barracks. Unit: tiles.</summary>
        private static readonly FixVec2 Point = At(18, 16);

        /// <summary>What one run records: the worker's position after each of ticks T + 100 to T + 400, the unreachable events seen, and the worker.</summary>
        private sealed class RunResult
        {
            /// <summary>Worker position after each tick T + 100 to T + 400. Unit: tiles.</summary>
            public List<FixVec2> Positions = new List<FixVec2>();

            /// <summary>Description of every MoveUnreachableEvent seen from tick T + 99 on.</summary>
            public List<string> Unreachable = new List<string>();

            /// <summary>The worker, as it was right after it spawned on T + 99.</summary>
            public bool SpawnedWithMoveTarget;

            /// <summary>Move target of the worker right after it spawned on T + 99.</summary>
            public FixVec2 SpawnedMoveTarget;

            /// <summary>The worker at the end.</summary>
            public Unit Worker = null!;
        }

        /// <summary>A started economy simulation with a complete barracks of P0.</summary>
        private static Simulation WithBarracks(out EntityId barracks)
        {
            Simulation sim = TestWorld.StartedEconomy();
            barracks = Spawn(sim, TestWorld.BarracksId, P0, 12, 16);
            return sim;
        }

        /// <summary>A set (or clear) command for the current tick, issued by P0.</summary>
        private static SetRallyPointCommand Rally(Simulation sim, EntityId producer, FixVec2 point, bool clear = false)
        {
            return new SetRallyPointCommand(sim.Clock.Tick, P0, Seq(), producer, clear, point);
        }

        /// <summary>
        /// Runs the IS-21 timeline: optional preparation, the rally point (when <paramref name="rally"/>) on tick T - 1 or an idle tick in the control run, a worker trained on T,
        /// then ticks T + 100 to T + 400 recorded; the control run (no rally) gives the worker a move_unit to the point on T + 100 instead.
        /// </summary>
        private static RunResult Run(bool rally, FixVec2 point, System.Action<Simulation>? prepare = null)
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            prepare?.Invoke(sim);
            if (rally)
            {
                AssertAccepted(sim, Rally(sim, barracks, point));
            }
            else
            {
                sim.Step();
            }
            int t = sim.Clock.Tick;
            AssertAccepted(sim, new TrainUnitCommand(t, P0, Seq(), barracks, TestWorld.WorkerId));
            RunResult result = new RunResult();
            // The worker, found from the event on tick T + 99.
            EntityId worker = EntityId.None;
            while (sim.Clock.Tick < t + 100)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is UnitTrainedEvent trained)
                    {
                        worker = trained.Unit;
                    }
                    if (evt is MoveUnreachableEvent)
                    {
                        result.Unreachable.Add(evt.Describe());
                    }
                }
            }
            Assert.That(worker.IsValid, Is.True, "the worker appears on T + 99");
            result.SpawnedWithMoveTarget = Get(sim, worker).HasMoveTarget;
            result.SpawnedMoveTarget = Get(sim, worker).MoveTarget;
            if (!rally)
            {
                sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), worker, point));
            }
            while (sim.Clock.Tick <= t + 400)
            {
                sim.Step();
                result.Positions.Add(Get(sim, worker).Position);
                result.Unreachable.AddRange(sim.Events.Current.OfType<MoveUnreachableEvent>().Select(e => e.Describe()));
            }
            result.Worker = Get(sim, worker);
            return result;
        }

        /// <summary>IS-21: the trained worker gets the move target on T + 99 and then moves exactly as if move_unit had been given on T + 100.</summary>
        [Test]
        public void Is21_RallyThenTrain()
        {
            RunResult rallied = Run(true, Point);
            RunResult control = Run(false, Point);
            Assert.That(rallied.SpawnedWithMoveTarget, Is.True);
            Assert.That(rallied.SpawnedMoveTarget, Is.EqualTo(Point));
            Assert.That(control.SpawnedWithMoveTarget, Is.False);
            Assert.That(rallied.Positions.Count, Is.EqualTo(301));
            Assert.That(rallied.Positions, Is.EqualTo(control.Positions));
            Assert.That((rallied.Worker.Position - Point).LengthSquared() <= Fix64.One, Is.True, "ends within one tile of the point");
        }

        /// <summary>IS-22: with no rally point, and with one set and cleared before the spawn, the new unit has no move target.</summary>
        [Test]
        public void Is22_NoneAndClear()
        {
            Simulation none = WithBarracks(out EntityId b1);
            AssertAccepted(none, new TrainUnitCommand(none.Clock.Tick, P0, Seq(), b1, TestWorld.WorkerId));
            StepCollect(none, 99);
            Unit first = none.Units.All.Single(u => u.Definition.Id == TestWorld.WorkerId);
            Assert.That(first.HasMoveTarget, Is.False);

            Simulation cleared = WithBarracks(out EntityId b2);
            AssertAccepted(cleared, Rally(cleared, b2, Point));
            AssertAccepted(cleared, new TrainUnitCommand(cleared.Clock.Tick, P0, Seq(), b2, TestWorld.WorkerId));
            AssertAccepted(cleared, Rally(cleared, b2, FixVec2.Zero, true));
            Assert.That(Get(cleared, b2).HasRallyPoint, Is.False);
            StepCollect(cleared, 98);
            Unit second = cleared.Units.All.Single(u => u.Definition.Id == TestWorld.WorkerId);
            Assert.That(second.HasMoveTarget, Is.False);
        }

        /// <summary>IS-23: every rejection reason; clearing needs no valid point; a building under construction accepts a rally point.</summary>
        [Test]
        public void Is23_Rejections()
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            EntityId tower = Spawn(sim, TestWorld.TowerId, P0, 20, 20);
            FixVec2 far = new FixVec2(Fix64.FromInt(20000), Fix64.Zero);
            AssertRejected(sim, new SetRallyPointCommand(sim.Clock.Tick, P0, Seq(), new EntityId(999), false, Point), "unknown_unit");
            AssertRejected(sim, new SetRallyPointCommand(sim.Clock.Tick, P1, Seq(), barracks, false, Point), "not_owner");
            AssertRejected(sim, Rally(sim, tower, Point), "not_a_producer");
            AssertRejected(sim, Rally(sim, barracks, far), "out_of_bounds");
            AssertAccepted(sim, Rally(sim, barracks, far, true));
            Assert.That(Get(sim, barracks).HasRallyPoint, Is.False);
            Assert.That(Get(sim, barracks).RallyPoint, Is.EqualTo(FixVec2.Zero));
            AssertAccepted(sim, Rally(sim, barracks, Point));
            Assert.That(Get(sim, barracks).RallyPoint, Is.EqualTo(Point));

            // A barracks still under construction.
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            EntityId site = PlaceStructure(sim, worker, TestWorld.BarracksId, 20, 14);
            Assert.That(Get(sim, site).IsUnderConstruction, Is.True);
            AssertAccepted(sim, Rally(sim, site, Point));
            Assert.That(Get(sim, site).HasRallyPoint, Is.True);
        }

        /// <summary>IS-24: a point on a blocked tile is accepted; the worker gets the same move order and the same unreachable events as with move_unit.</summary>
        [Test]
        public void Is24_Unreachable()
        {
            System.Action<Simulation> wall = sim =>
            {
                StatusTestWorld.Run(sim, sim.Clock.Tick, 7201, s => s.Terrain.AddBlockers(s, s.Terrain.IndexOf(18, 16), TileBlockers.Wall));
                sim.Step();
            };
            RunResult rallied = Run(true, Point, wall);
            RunResult control = Run(false, Point, wall);
            Assert.That(rallied.SpawnedMoveTarget, Is.EqualTo(Point));
            Assert.That(rallied.Unreachable, Is.EqualTo(control.Unreachable), "same unreachable events (none or more: a blocked goal in open ground ends beside it)");
            Assert.That(rallied.Positions, Is.EqualTo(control.Positions));
        }

        /// <summary>IS-25: removing the producer leaves the units already sent with their order.</summary>
        [Test]
        public void Is25_ProducerLost()
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            // A point 38 tiles away, so neither worker arrives before the barracks goes.
            FixVec2 far = At(50, 16);
            AssertAccepted(sim, Rally(sim, barracks, far));
            int t = sim.Clock.Tick;
            sim.Submit(new TrainUnitCommand(t, P0, Seq(), barracks, TestWorld.WorkerId));
            sim.Submit(new TrainUnitCommand(t, P0, Seq(), barracks, TestWorld.WorkerId));
            // The two workers, found from their events.
            List<EntityId> workers = new List<EntityId>();
            while (sim.Clock.Tick <= t + 199)
            {
                sim.Step();
                workers.AddRange(sim.Events.Current.OfType<UnitTrainedEvent>().Select(e => e.Unit));
            }
            Assert.That(workers.Count, Is.EqualTo(2));
            AssertAccepted(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), barracks));
            Assert.That(sim.Units.TryGet(barracks, out Unit? _), Is.False);
            foreach (EntityId id in workers)
            {
                Assert.That(Get(sim, id).HasMoveTarget, Is.True);
                Assert.That(Get(sim, id).MoveTarget, Is.EqualTo(far));
            }
            StepCollect(sim, 50);
        }

        /// <summary>IS-26: the state hash moves while a point is set and returns when it is cleared; codec, script and wire-format checks.</summary>
        [Test]
        public void Is26_HashCodecAndScript()
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            ulong plain = sim.ComputeStateHash();
            AssertAccepted(sim, Rally(sim, barracks, Point));
            ulong withPoint = sim.ComputeStateHash();
            Assert.That(withPoint, Is.Not.EqualTo(plain));
            AssertAccepted(sim, Rally(sim, barracks, Point, true));
            // Equal to a run that never set one and stepped the same ticks.
            Simulation never = WithBarracks(out EntityId _);
            never.Step();
            never.Step();
            Assert.That(sim.ComputeStateHash(), Is.EqualTo(never.ComputeStateHash()));

            CommandCodecRegistry registry = BuiltInMechanics.CreateCodecRegistry();
            foreach (SetRallyPointCommand original in new[]
            {
                new SetRallyPointCommand(40, P1, 3, new EntityId(77), false, new FixVec2(Fix64.Parse("12.75"), Fix64.Parse("-3.5"))),
                new SetRallyPointCommand(41, P0, 4, new EntityId(78), true, new FixVec2(Fix64.One, Fix64.One)),
            })
            {
                byte[] payload = registry.EncodePayload(original);
                SetRallyPointCommand decoded = (SetRallyPointCommand)registry.DecodePayload(original.Kind, original.Tick, original.Issuer, original.Sequence, payload);
                Assert.That((decoded.Producer, decoded.Clear, decoded.Point), Is.EqualTo((original.Producer, original.Clear, original.Point)));
                Assert.That(decoded.Point, Is.EqualTo(original.Clear ? FixVec2.Zero : original.Point), "a cleared command stores Zero");
                // The flag byte sits before the 16 bytes of the point; any value but 0 or 1 is refused.
                byte[] bad = (byte[])payload.Clone();
                bad[bad.Length - 17] = 2;
                Expect.Throws<CommandCodecException>(() => registry.DecodePayload(original.Kind, original.Tick, original.Issuer, original.Sequence, bad));
            }

            string head = "{ \"$schema_version\": 1, \"seed\": 7, \"ticks\": 40, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"skai\" } ], \"commands\": [ ";
            string common = "\"type\": \"set_rally_point\", \"tick\": 30, \"issuer\": 1, \"sequence\": 9, \"producer\": 77";
            SetRallyPointCommand set = (SetRallyPointCommand)CommandScript.Parse(head + "{ " + common + ", \"x\": 20.5, \"y\": \"7\" } ] }", "test/rally.json").Commands.Single();
            Assert.That((set.Clear, set.Producer, set.Point), Is.EqualTo((false, new EntityId(77), new FixVec2(Fix64.Parse("20.5"), Fix64.FromInt(7)))));
            SetRallyPointCommand clear = (SetRallyPointCommand)CommandScript.Parse(head + "{ " + common + ", \"clear\": true } ] }", "test/rally.json").Commands.Single();
            Assert.That((clear.Clear, clear.Point), Is.EqualTo((true, FixVec2.Zero)));
            Expect.Throws<DataValidationException>(() => CommandScript.Parse(head + "{ " + common + ", \"clear\": true, \"x\": 1 } ] }", "test/rally.json"));
            Expect.Throws<DataValidationException>(() => CommandScript.Parse(head + "{ " + common + " } ] }", "test/rally.json"));
        }

        /// <summary>S4: a trained unit that is movement-locked (a Rooted HQ) gets no rally move order. Test-local catalog: a building that trains the test HQ.</summary>
        [Test]
        public void Is27_MovementLockedUnitGetsNoRallyOrder()
        {
            const string hqFactory = "test.hq_factory";
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(new UnitDefinition(hqFactory, "HQ factory", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(200), Fix64.Zero, 0, Fix64.Half, 0, 0,
                "armoured", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(5), false, false, new List<string> { TestWorld.HqId }, false, 1, 0)));
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), catalog, TestWorld.TwoPlayers(),
                BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.EconomyMap()));
            sim.Start();
            EntityId factory = Spawn(sim, hqFactory, P0, 12, 16);
            AssertAccepted(sim, Rally(sim, factory, Point));
            AssertAccepted(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), factory, TestWorld.HqId));
            // Ticks stepped while waiting for the HQ to appear. Unit: ticks. Range: 0 to 2000.
            Unit? hq = null;
            for (int i = 0; i < 2000 && hq == null; i++)
            {
                sim.Step();
                hq = sim.Units.All.FirstOrDefault(u => u.Definition.Id == TestWorld.HqId);
            }
            Assert.That(hq, Is.Not.Null);
            Assert.That(HqSystem.IsMovementLocked(hq!), Is.True);
            Assert.That(hq!.HasMoveTarget, Is.False);
        }
    }
}

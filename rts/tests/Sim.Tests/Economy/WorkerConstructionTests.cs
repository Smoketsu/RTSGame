// rts/tests/Sim.Tests/Economy/WorkerConstructionTests.cs
// Acceptance tests WC-1 to WC-23 of rts/docs/specs/worker_construction.md (package WC-1, the Sim part): a build order names a worker,
// the worker walks to the site, spends worker_placement_seconds there, the structure appears and finishes alone. Covers walk and
// placement timing, refunds on every cancellation reason, the fog rule, pending orders in the static defence cap and in supply, the
// gather loop resuming, hash coverage, the unchanged wire format, separation, and the economy_smoke script. Part of Rts.Sim.Tests.
// Layout unless stated otherwise ("WC layout"): the economy map and the shipped tuning (placement 2 s = 40 ticks, reach 2.0 tiles to
// the test barracks); a P0 worker at (14, 14) is ordered to build a barracks on tile (20, 14) on tick t0.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Commands.Codec;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Tests.Harness;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>The worker construction rule in the simulation.</summary>
    [TestFixture]
    public class WorkerConstructionTests
    {
        /// <summary>One run of the WC layout: the simulation, the worker and the tick the order was applied on.</summary>
        private sealed class Layout
        {
            /// <summary>The simulation.</summary>
            public Simulation Sim { get; }

            /// <summary>The ordered worker.</summary>
            public EntityId Worker { get; }

            /// <summary>Tick the build command was applied on. Unit: ticks. Range: 1 upward.</summary>
            public int T0 { get; }

            /// <summary>Events of tick t0 (the command tick).</summary>
            public List<SimEvent> CommandEvents { get; }

            /// <summary>Builds the layout: worker at (<paramref name="x"/>, 14), barracks ordered on (<paramref name="siteX"/>, 14).</summary>
            public Layout(int x = 14, int siteX = 20, Simulation? sim = null)
            {
                Sim = sim ?? TestWorld.StartedEconomy();
                Worker = Spawn(Sim, TestWorld.WorkerId, P0, x, 14);
                T0 = Sim.Clock.Tick;
                CommandEvents = Do(Sim, new BuildStructureCommand(T0, P0, Seq(), Worker, TestWorld.BarracksId, siteX, 14));
            }

            /// <summary>The worker's unit record.</summary>
            public Unit WorkerUnit => Get(Sim, Worker);

            /// <summary>Steps until tick <paramref name="tick"/> has been processed (the clock then reads tick + 1) and returns the events of those ticks.</summary>
            public List<SimEvent> StepThrough(int tick)
            {
                // Events gathered. Unit: events.
                List<SimEvent> all = new List<SimEvent>();
                while (Sim.Clock.Tick <= tick)
                {
                    Sim.Step();
                    all.AddRange(Sim.Events.Current);
                }
                return all;
            }
        }

        /// <summary>Number of live units of a definition.</summary>
        private static int CountOf(Simulation sim, string definition)
        {
            // Units found. Unit: count.
            int count = 0;
            foreach (Unit unit in sim.Units.All)
            {
                count += unit.Definition.Id == definition ? 1 : 0;
            }
            return count;
        }

        /// <summary>Asserts the worker has every build field at its None value.</summary>
        private static void AssertNoOrder(Unit worker)
        {
            Assert.That(worker.BuildState, Is.EqualTo(BuildOrderState.None));
            Assert.That(worker.BuildDefinitionId, Is.EqualTo(string.Empty));
            Assert.That(worker.BuildTileX, Is.EqualTo(-1));
            Assert.That(worker.BuildTileY, Is.EqualTo(-1));
            Assert.That(worker.BuildTicksRemaining, Is.EqualTo(0));
            Assert.That(worker.BuildResumeFieldIndex, Is.EqualTo(-1));
        }

        /// <summary>The first event of type T, or fails.</summary>
        private static T First<T>(List<SimEvent> events) where T : SimEvent
        {
            List<T> found = Of<T>(events);
            Assert.That(found, Is.Not.Empty, typeof(T).Name);
            return found[0];
        }

        /// <summary>WC-1: the walk takes as long as distance and speed say; the order is paid on t0 and the placement starts on arrival.</summary>
        [Test]
        public void Wc01_WalkTime_FromDistanceAndSpeed()
        {
            Layout w = new Layout();
            BuildOrderAcceptedEvent accepted = First<BuildOrderAcceptedEvent>(w.CommandEvents);
            Assert.That(accepted.Tick, Is.EqualTo(w.T0));
            Assert.That(accepted.CostCommon, Is.EqualTo(100));
            Assert.That(accepted.CostRare, Is.EqualTo(0));
            Assert.That(PlayerOf(w.Sim, P0).Common, Is.EqualTo(100));
            Assert.That(Of<BuildOrderStartedEvent>(w.CommandEvents), Is.Empty);
            // Walk (no placement start yet) up to and including t0 + 21.
            List<SimEvent> early = w.StepThrough(w.T0 + 21);
            Assert.That(Of<BuildOrderStartedEvent>(early), Is.Empty, "not within reach at x = 18.4");
            Assert.That(w.WorkerUnit.BuildState, Is.EqualTo(BuildOrderState.ToSite));
            List<SimEvent> arrival = w.StepThrough(w.T0 + 22);
            BuildOrderStartedEvent started = First<BuildOrderStartedEvent>(arrival);
            Assert.That(started.Tick, Is.EqualTo(w.T0 + 22));
            Assert.That(started.PlacementTicks, Is.EqualTo(40));
            Assert.That(w.WorkerUnit.HasMoveTarget, Is.False);
            Assert.That(w.WorkerUnit.BuildState, Is.EqualTo(BuildOrderState.Placing));
        }

        /// <summary>WC-2: placement takes 40 ticks; the structure appears under construction and finishes 100 ticks later; no refund; supply follows completion.</summary>
        [Test]
        public void Wc02_PlacementTime_InTicks()
        {
            Layout w = new Layout();
            w.StepThrough(w.T0 + 61);
            Assert.That(CountOf(w.Sim, TestWorld.BarracksId), Is.EqualTo(0));
            Assert.That(w.WorkerUnit.BuildState, Is.EqualTo(BuildOrderState.Placing));
            Assert.That(w.WorkerUnit.BuildTicksRemaining, Is.EqualTo(1));
            List<SimEvent> placing = w.StepThrough(w.T0 + 62);
            // The structure's spawn event comes first, then the placed event.
            int spawnedAt = placing.FindIndex(e => e is UnitSpawnedEvent);
            int placedAt = placing.FindIndex(e => e is StructurePlacedEvent);
            Assert.That(spawnedAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(placedAt, Is.GreaterThan(spawnedAt));
            StructurePlacedEvent placed = First<StructurePlacedEvent>(placing);
            Assert.That(placed.Worker, Is.EqualTo(w.Worker));
            Assert.That(placed.TileX, Is.EqualTo(20));
            Unit barracks = Get(w.Sim, placed.Structure);
            Assert.That(barracks.Position, Is.EqualTo(new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("14.5"))));
            Assert.That(barracks.Health, Is.GreaterThanOrEqualTo(Fix64.Parse("19.9")).And.LessThanOrEqualTo(Fix64.Parse("20.1")));
            Assert.That(barracks.ConstructionTicksRemaining, Is.EqualTo(100));
            Assert.That(w.Sim.Terrain[w.Sim.Terrain.IndexOf(20, 14)].IsBlocked, Is.True);
            AssertNoOrder(w.WorkerUnit);
            Assert.That(PlayerOf(w.Sim, P0).SupplyCap, Is.EqualTo(10));
            // Completion on t0 + 162: the structure's countdown first runs on t0 + 63 (it was not in this step's snapshot).
            List<SimEvent> before = w.StepThrough(w.T0 + 161);
            Assert.That(Of<StructureCompletedEvent>(before), Is.Empty);
            Assert.That(PlayerOf(w.Sim, P0).SupplyCap, Is.EqualTo(10));
            List<SimEvent> done = w.StepThrough(w.T0 + 162);
            Assert.That(First<StructureCompletedEvent>(done).Tick, Is.EqualTo(w.T0 + 162));
            Assert.That(PlayerOf(w.Sim, P0).SupplyCap, Is.EqualTo(18));
            Assert.That(PlayerOf(w.Sim, P0).Common, Is.EqualTo(100), "no refund once the structure exists");
        }

        /// <summary>WC-3: the placement ticks follow the tuning; out-of-range and missing values are load errors.</summary>
        [Test]
        public void Wc03_PlacementTicks_FollowTheTuning()
        {
            const string original = "\"worker_placement_seconds\": 2.0";
            Tuning half = TuningWithEdit("tuning/economy.json", original, "\"worker_placement_seconds\": 0.5");
            Layout w = new Layout(sim: TestWorld.StartedEconomy(tuning: half));
            List<SimEvent> events = w.StepThrough(w.T0 + 32);
            Assert.That(First<StructurePlacedEvent>(events).Tick, Is.EqualTo(w.T0 + 32));
            Tuning tiny = TuningWithEdit("tuning/economy.json", original, "\"worker_placement_seconds\": 0.05");
            Assert.That(tiny.Economy.WorkerPlacementTicks(new MatchClock(20)), Is.EqualTo(1));
            Assert.That(half.Economy.WorkerPlacementTicks(new MatchClock(20)), Is.EqualTo(10));
            Assert.That(TestWorld.Tuning.Economy.WorkerPlacementTicks(new MatchClock(20)), Is.EqualTo(40));
            foreach (string bad in new[] { "0.01", "31" })
            {
                DataValidationException ex = Expect.Throws<DataValidationException>(() => TuningWithEdit("tuning/economy.json", original, "\"worker_placement_seconds\": " + bad));
                Assert.That(ex.Message, Does.Contain("tuning/economy.json").And.Contain("worker_placement_seconds"));
            }
            DataValidationException missing = Expect.Throws<DataValidationException>(() => TuningWithEdit("tuning/economy.json", original + ",", string.Empty));
            Assert.That(missing.Message, Does.Contain("tuning/economy.json").And.Contain("worker_placement_seconds"));
        }

        /// <summary>WC-4: a worker already within reach starts placing on the command tick.</summary>
        [Test]
        public void Wc04_ArrivalOnTheCommandTick()
        {
            Layout w = new Layout(x: 19);
            Assert.That(First<BuildOrderStartedEvent>(w.CommandEvents).Tick, Is.EqualTo(w.T0));
            List<SimEvent> events = w.StepThrough(w.T0 + 40);
            Assert.That(First<StructurePlacedEvent>(events).Tick, Is.EqualTo(w.T0 + 40));
        }

        /// <summary>WC-5: a new order refunds; a rejected one does not cancel.</summary>
        [Test]
        public void Wc05_RefundWhenANewOrderArrives()
        {
            // Each new order kind and its command factory, given the sim and the worker.
            string[] kinds = { "move", "stop", "attack_move", "gather" };
            foreach (string kind in kinds)
            {
                Layout w = new Layout();
                w.StepThrough(w.T0 + 4);
                Command order;
                switch (kind)
                {
                    case "move":
                        order = new MoveUnitCommand(w.Sim.Clock.Tick, P0, Seq(), w.Worker, At(14, 30));
                        break;
                    case "stop":
                        order = new StopCommand(w.Sim.Clock.Tick, P0, Seq(), w.Worker);
                        break;
                    case "attack_move":
                        order = new AttackMoveCommand(w.Sim.Clock.Tick, P0, Seq(), w.Worker, At(14, 30));
                        break;
                    default:
                        order = new GatherCommand(w.Sim.Clock.Tick, P0, Seq(), w.Worker, 2);
                        break;
                }
                List<SimEvent> events = Do(w.Sim, order);
                Assert.That(Reasons(events), Is.Empty, kind);
                BuildOrderCancelledEvent cancelled = First<BuildOrderCancelledEvent>(events);
                Assert.That(cancelled.Reason, Is.EqualTo("new_order"), kind);
                Assert.That(cancelled.RefundCommon, Is.EqualTo(100));
                Assert.That(PlayerOf(w.Sim, P0).Common, Is.EqualTo(200), kind);
                AssertNoOrder(w.WorkerUnit);
                if (kind == "move")
                {
                    Assert.That(w.WorkerUnit.HasMoveTarget, Is.True);
                    Assert.That(w.WorkerUnit.MoveTarget, Is.EqualTo(At(14, 30)));
                }
                w.StepThrough(w.T0 + 105);
                Assert.That(CountOf(w.Sim, TestWorld.BarracksId), Is.EqualTo(0), kind);
                Assert.That(w.Sim.Terrain[w.Sim.Terrain.IndexOf(20, 14)].IsBlocked, Is.False, kind);
            }

            // An attack order from an unarmed worker is rejected (no_weapon) and cancels nothing; so is a move out of bounds.
            Layout rejected = new Layout();
            EntityId enemy = Spawn(rejected.Sim, TestWorld.TankId, P1, 40, 40);
            AssertRejected(rejected.Sim, new AttackUnitCommand(rejected.Sim.Clock.Tick, P0, Seq(), rejected.Worker, enemy), "no_weapon");
            AssertRejected(rejected.Sim, new MoveUnitCommand(rejected.Sim.Clock.Tick, P0, Seq(), rejected.Worker, At(20000, 5)), "out_of_bounds");
            Assert.That(rejected.WorkerUnit.BuildState, Is.EqualTo(BuildOrderState.ToSite));
            Assert.That(PlayerOf(rejected.Sim, P0).Common, Is.EqualTo(100));
        }

        /// <summary>WC-5 (armed worker): an attack order from an armed worker (shipped workers are armed since P-003) cancels the build with new_order and a full refund.</summary>
        [Test]
        public void Wc05b_AttackOrderFromAnArmedWorker_CancelsTheBuild()
        {
            DataSet data = TestData.LoadAll();
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(1234), data.Content,
                new List<PlayerSetup> { new PlayerSetup(P0, new TeamId(0), "skai", "Skai"), new PlayerSetup(P1, new TeamId(1), "water", "Water") },
                BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap("greybox_1v1")));
            sim.Start();
            EntityId worker = Spawn(sim, "skai.fledgling", P0, 20, 9);
            EntityId enemy = Spawn(sim, "water.tender", P1, 24, 9);
            Assert.That(Get(sim, worker).Definition.Weapon, Is.Not.Null, "the shipped worker is armed");
            // Common stock before the order. Unit: resource units.
            int before = PlayerOf(sim, P0).Common;
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, "skai.perch_depot", 21, 7));
            Assert.That(PlayerOf(sim, P0).Common, Is.LessThan(before));
            List<SimEvent> events = Do(sim, new AttackUnitCommand(sim.Clock.Tick, P0, Seq(), worker, enemy));
            Assert.That(Reasons(events), Is.Empty);
            BuildOrderCancelledEvent cancelled = First<BuildOrderCancelledEvent>(events);
            Assert.That(cancelled.Reason, Is.EqualTo("new_order"));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(before), "full refund");
            AssertNoOrder(Get(sim, worker));
        }

        /// <summary>A blocked site approached diagonally: the worker stops just outside reach and the order is still cancelled as site_blocked, not unreachable.</summary>
        [Test]
        public void Wc07b_BlockedSiteApproachedDiagonally_IsSiteBlocked()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 13, 7);
            // A flying spotter explores the site (flyers never occupy a tile).
            Spawn(sim, TestWorld.FlyerId, P0, 20, 14);
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, TestWorld.BarracksId, 20, 14));
            Do(sim, new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.TowerId, P1, At(20, 14)));
            List<SimEvent> events = StepCollect(sim, 120);
            Assert.That(First<BuildOrderCancelledEvent>(events).Reason, Is.EqualTo("site_blocked"));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
        }

        /// <summary>WC-6: a worker removed or killed mid-placement refunds its order and no structure appears.</summary>
        [Test]
        public void Wc06_WorkerKilledMidPlacement()
        {
            Layout w = new Layout();
            w.StepThrough(w.T0 + 29);
            List<SimEvent> events = Do(w.Sim, new RemoveUnitCommand(w.Sim.Clock.Tick, PlayerId.None, Seq(), w.Worker));
            Assert.That(w.Sim.Clock.Tick, Is.EqualTo(w.T0 + 31));
            int removedAt = events.FindIndex(e => e is UnitRemovedEvent);
            int cancelledAt = events.FindIndex(e => e is BuildOrderCancelledEvent);
            Assert.That(removedAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(cancelledAt, Is.GreaterThan(removedAt));
            BuildOrderCancelledEvent cancelled = First<BuildOrderCancelledEvent>(events);
            Assert.That(cancelled.Reason, Is.EqualTo("worker_lost"));
            Assert.That(cancelled.RefundCommon, Is.EqualTo(100));
            Assert.That(cancelled.Worker, Is.EqualTo(w.Worker));
            Assert.That(PlayerOf(w.Sim, P0).Common, Is.EqualTo(200));
            w.StepThrough(w.T0 + 131);
            Assert.That(CountOf(w.Sim, TestWorld.BarracksId), Is.EqualTo(0));
            Assert.That(w.Sim.Terrain[w.Sim.Terrain.IndexOf(20, 14)].IsBlocked, Is.False);

            // Combat variant: an enemy shooter kills the worker; the refund comes on the death tick.
            Layout c = new Layout();
            Spawn(c.Sim, TestWorld.ShooterId, P1, 21, 17);
            List<SimEvent> fight = c.StepThrough(c.T0 + 160);
            UnitKilledEvent killed = First<UnitKilledEvent>(fight);
            Assert.That(killed.Victim, Is.EqualTo(c.Worker));
            BuildOrderCancelledEvent lost = First<BuildOrderCancelledEvent>(fight);
            Assert.That(lost.Reason, Is.EqualTo("worker_lost"));
            Assert.That(lost.Tick, Is.EqualTo(killed.Tick));
            Assert.That(CountOf(c.Sim, TestWorld.BarracksId), Is.EqualTo(0));
            Assert.That(PlayerOf(c.Sim, P0).Common, Is.EqualTo(200));
        }

        /// <summary>WC-7: a structure on the site before the worker arrives cancels the order as site_blocked, with a refund.</summary>
        [Test]
        public void Wc07_SiteBlockedOnArrival()
        {
            Layout w = new Layout();
            w.StepThrough(w.T0);
            Do(w.Sim, new SpawnUnitCommand(w.Sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.TowerId, P1, At(20, 14)));
            List<SimEvent> events = w.StepThrough(w.T0 + 30);
            Assert.That(Of<BuildOrderStartedEvent>(events), Is.Empty);
            BuildOrderCancelledEvent cancelled = First<BuildOrderCancelledEvent>(events);
            Assert.That(cancelled.Reason, Is.EqualTo("site_blocked"));
            Assert.That(cancelled.Tick, Is.EqualTo(w.T0 + 22));
            Assert.That(cancelled.RefundCommon, Is.EqualTo(100));
            Assert.That(PlayerOf(w.Sim, P0).Common, Is.EqualTo(200));
            Assert.That(w.WorkerUnit.GatherState, Is.EqualTo(GatherState.Idle));
            Assert.That(w.WorkerUnit.HasMoveTarget, Is.False);
        }

        /// <summary>WC-8: a unit standing on the site cancels the order as site_occupied, on arrival and again on the placement tick.</summary>
        [Test]
        public void Wc08_SiteOccupied_OnArrivalAndDuringPlacement()
        {
            Layout a = new Layout();
            Do(a.Sim, new SpawnUnitCommand(a.Sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.TankId, P1, At(20, 14)));
            List<SimEvent> early = a.StepThrough(a.T0 + 30);
            BuildOrderCancelledEvent onArrival = First<BuildOrderCancelledEvent>(early);
            Assert.That(onArrival.Reason, Is.EqualTo("site_occupied"));
            Assert.That(onArrival.Tick, Is.EqualTo(a.T0 + 22));

            Layout b = new Layout();
            b.StepThrough(b.T0 + 49);
            Do(b.Sim, new SpawnUnitCommand(b.Sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.TankId, P1, At(20, 14)));
            List<SimEvent> late = b.StepThrough(b.T0 + 70);
            BuildOrderCancelledEvent atPlacement = First<BuildOrderCancelledEvent>(late);
            Assert.That(atPlacement.Reason, Is.EqualTo("site_occupied"));
            Assert.That(atPlacement.Tick, Is.EqualTo(b.T0 + 62));
            Assert.That(atPlacement.RefundCommon, Is.EqualTo(100));
            Assert.That(CountOf(b.Sim, TestWorld.BarracksId), Is.EqualTo(0));
        }

        /// <summary>WC-9: a site the team never saw is rejected, and the worker's gather job is untouched.</summary>
        [Test]
        public void Wc09_UnexploredSiteRejected()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            AssertAccepted(sim, new GatherCommand(sim.Clock.Tick, P0, Seq(), worker, 2));
            List<SimEvent> events = Do(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, TestWorld.BarracksId, 40, 14));
            Assert.That(Reasons(events), Is.EqualTo(new List<string> { "site_unexplored" }));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            Assert.That(Of<BuildOrderAcceptedEvent>(events), Is.Empty);
            Assert.That(Get(sim, worker).GatherState, Is.EqualTo(GatherState.ToField));
            Assert.That(Get(sim, worker).GatherFieldIndex, Is.EqualTo(2));
        }

        /// <summary>WC-10: the fog check comes before tile_blocked, so a rejection never reveals what stands on an unseen tile.</summary>
        [Test]
        public void Wc10_FogIsCheckedBeforeBlocked()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            Spawn(sim, TestWorld.TowerId, P1, 40, 40);
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, TestWorld.BarracksId, 40, 40), "site_unexplored");
        }

        /// <summary>WC-11: a tile seen once is enough, even when nobody sees it now.</summary>
        [Test]
        public void Wc11_ExploredButNotVisibleIsEnough()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            EntityId spotter = Spawn(sim, TestWorld.RunnerId, P0, 40, 14);
            TeamId team = sim.Players.TeamOf(P0);
            Assert.That(sim.Vision.IsTileVisible(team, 40, 14), Is.True);
            Do(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), spotter, At(60, 60)));
            // Ticks stepped while waiting for the tile to go dark. Unit: ticks. Range: 0 to 400.
            for (int i = 0; i < 400 && sim.Vision.IsTileVisible(team, 40, 14); i++)
            {
                sim.Step();
            }
            Assert.That(sim.Vision.IsTileVisible(team, 40, 14), Is.False);
            Assert.That(sim.Vision.IsTileExplored(team, 40, 14), Is.True);
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, TestWorld.BarracksId, 40, 14));
        }

        /// <summary>WC-12: two workers share one static defence cap, pending orders count, and cancelling one frees its place.</summary>
        [Test]
        public void Wc12_TwoWorkers_OneStaticDefenceCap()
        {
            Simulation sim = TestWorld.StartedEconomy();
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            for (int x = 16; x <= 20; x++)
            {
                Spawn(sim, TestWorld.TowerId, P0, x, 10);
            }
            EntityId w1 = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            EntityId w2 = Spawn(sim, TestWorld.WorkerId, P0, 15, 15);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            int tick = sim.Clock.Tick;
            sim.Submit(new BuildStructureCommand(tick, P0, 1000, w1, TestWorld.TowerId, 21, 10));
            sim.Submit(new BuildStructureCommand(tick, P0, 1001, w2, TestWorld.TowerId, 22, 10));
            sim.Step();
            List<SimEvent> events = new List<SimEvent>(sim.Events.Current);
            Assert.That(Reasons(events), Is.EqualTo(new List<string> { "static_defence_cap" }));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(190));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(8), "5 towers, 1 pending tower, 2 workers");
            List<SimEvent> stopped = Do(sim, new StopCommand(sim.Clock.Tick, P0, Seq(), w1));
            Assert.That(First<BuildOrderCancelledEvent>(stopped).Reason, Is.EqualTo("new_order"));
            Assert.That(First<BuildOrderCancelledEvent>(stopped).RefundCommon, Is.EqualTo(10));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(7));
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), w2, TestWorld.TowerId, 22, 10));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(190));
        }

        /// <summary>WC-13: pending orders count as supply used, so two workers cannot both take the last supply; the order's supply carries over to the structure.</summary>
        [Test]
        public void Wc13_TwoWorkers_OneSupply()
        {
            Simulation sim = TestWorld.StartedEconomy();
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            // Nine workers; the test HQ provides no supply and base_supply is 10.
            List<EntityId> workers = new List<EntityId>();
            for (int i = 0; i < 9; i++)
            {
                workers.Add(Spawn(sim, TestWorld.WorkerId, P0, 10 + i, 15));
            }
            Player owner = PlayerOf(sim, P0);
            Assert.That(owner.SupplyUsed, Is.EqualTo(9));
            Assert.That(owner.SupplyCap, Is.EqualTo(10));
            int tick = sim.Clock.Tick;
            sim.Submit(new BuildStructureCommand(tick, P0, 2000, workers[3], TestWorld.TowerId, 16, 10));
            sim.Submit(new BuildStructureCommand(tick, P0, 2001, workers[4], TestWorld.TowerId, 17, 10));
            sim.Step();
            Assert.That(Reasons(new List<SimEvent>(sim.Events.Current)), Is.EqualTo(new List<string> { "supply_cap" }));
            Assert.That(owner.SupplyUsed, Is.EqualTo(10));
            // The supply stays at 10 on every tick up to and including the tick the tower appears.
            bool placed = false;
            for (int i = 0; i < 200 && !placed; i++)
            {
                sim.Step();
                placed = Of<StructurePlacedEvent>(new List<SimEvent>(sim.Events.Current)).Count == 1;
                Assert.That(owner.SupplyUsed, Is.EqualTo(10));
            }
            Assert.That(placed, Is.True);
        }

        /// <summary>WC-14: the same site cannot be ordered twice; re-ordering a worker to its own site is rejected and its order goes on.</summary>
        [Test]
        public void Wc14_SameSiteTwice()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId w1 = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            EntityId w2 = Spawn(sim, TestWorld.WorkerId, P0, 15, 15);
            int tick = sim.Clock.Tick;
            sim.Submit(new BuildStructureCommand(tick, P0, 3000, w1, TestWorld.BarracksId, 20, 14));
            sim.Submit(new BuildStructureCommand(tick, P0, 3001, w2, TestWorld.BarracksId, 20, 14));
            sim.Step();
            Assert.That(Reasons(new List<SimEvent>(sim.Events.Current)), Is.EqualTo(new List<string> { "site_pending" }));
            List<SimEvent> again = Do(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), w1, TestWorld.BarracksId, 20, 14));
            Assert.That(Reasons(again), Is.EqualTo(new List<string> { "site_pending" }));
            Assert.That(Of<BuildOrderCancelledEvent>(again), Is.Empty);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(100));
            Assert.That(Get(sim, w1).BuildState, Is.EqualTo(BuildOrderState.ToSite));
        }

        /// <summary>WC-15: a new build order replaces the pending one: refund first, then the new payment.</summary>
        [Test]
        public void Wc15_ReplacingAnOrder()
        {
            // A tower needs a drop-off within the base radius, so the layout gets an HQ.
            Simulation sim = TestWorld.StartedEconomy();
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            Layout w = new Layout(sim: sim);
            List<SimEvent> events = Do(w.Sim, new BuildStructureCommand(w.Sim.Clock.Tick, P0, Seq(), w.Worker, TestWorld.TowerId, 16, 16));
            int cancelledAt = events.FindIndex(e => e is BuildOrderCancelledEvent);
            int acceptedAt = events.FindIndex(e => e is BuildOrderAcceptedEvent);
            Assert.That(cancelledAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(acceptedAt, Is.GreaterThan(cancelledAt));
            BuildOrderCancelledEvent cancelled = First<BuildOrderCancelledEvent>(events);
            Assert.That(cancelled.Reason, Is.EqualTo("new_order"));
            Assert.That(cancelled.RefundCommon, Is.EqualTo(100));
            Assert.That(First<BuildOrderAcceptedEvent>(events).CostCommon, Is.EqualTo(10));
            Assert.That(PlayerOf(w.Sim, P0).Common, Is.EqualTo(190));
            Assert.That(w.WorkerUnit.BuildTileX, Is.EqualTo(16));
            Assert.That(w.WorkerUnit.BuildDefinitionId, Is.EqualTo(TestWorld.TowerId));
        }

        /// <summary>WC-16: after placing, or after a site failure, the worker goes back to the field it left, carrying its load to a drop-off first.</summary>
        [Test]
        public void Wc16_BackToGathering()
        {
            // Plain variant: gathering, then order a barracks at (16, 12).
            Simulation sim = TestWorld.StartedEconomy();
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 12, 14);
            AssertAccepted(sim, new GatherCommand(sim.Clock.Tick, P0, Seq(), worker, 0));
            // Ticks stepped while waiting for the worker to start gathering. Unit: ticks. Range: 0 to 200.
            for (int i = 0; i < 200 && Get(sim, worker).GatherState != GatherState.Gathering; i++)
            {
                sim.Step();
            }
            Assert.That(Get(sim, worker).GatherState, Is.EqualTo(GatherState.Gathering));
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, TestWorld.BarracksId, 16, 12));
            Assert.That(Get(sim, worker).BuildResumeFieldIndex, Is.EqualTo(0));
            Assert.That(Get(sim, worker).GatherState, Is.EqualTo(GatherState.Idle));
            List<SimEvent> events = new List<SimEvent>();
            for (int i = 0; i < 200 && Of<StructurePlacedEvent>(events).Count == 0; i++)
            {
                sim.Step();
                events = new List<SimEvent>(sim.Events.Current);
            }
            Assert.That(Of<StructurePlacedEvent>(events).Count, Is.EqualTo(1));
            Assert.That(Get(sim, worker).GatherState, Is.EqualTo(GatherState.ToField));
            Assert.That(Get(sim, worker).GatherFieldIndex, Is.EqualTo(0));

            // Carrying variant: the load stays carried and goes to the drop-off after the placement.
            Simulation carrying = TestWorld.StartedEconomy();
            Spawn(carrying, TestWorld.HqId, P0, 12, 12);
            EntityId carrier = Spawn(carrying, TestWorld.WorkerId, P0, 12, 14);
            AssertAccepted(carrying, new GatherCommand(carrying.Clock.Tick, P0, Seq(), carrier, 0));
            for (int i = 0; i < 300 && Get(carrying, carrier).CarriedAmount == 0; i++)
            {
                carrying.Step();
            }
            Assert.That(Get(carrying, carrier).CarriedAmount, Is.EqualTo(5));
            AssertAccepted(carrying, new BuildStructureCommand(carrying.Clock.Tick, P0, Seq(), carrier, TestWorld.BarracksId, 16, 12));
            Assert.That(Get(carrying, carrier).CarriedAmount, Is.EqualTo(5));
            events = new List<SimEvent>();
            for (int i = 0; i < 200 && Of<StructurePlacedEvent>(events).Count == 0; i++)
            {
                carrying.Step();
                events = new List<SimEvent>(carrying.Events.Current);
            }
            Assert.That(Get(carrying, carrier).GatherState, Is.EqualTo(GatherState.ToDropOff));
            Assert.That(Get(carrying, carrier).CarriedAmount, Is.EqualTo(5));

            // Cancelled variant: a structure blocks the site; the worker resumes field 0.
            Simulation blocked = TestWorld.StartedEconomy();
            Spawn(blocked, TestWorld.HqId, P0, 12, 12);
            EntityId blockedWorker = Spawn(blocked, TestWorld.WorkerId, P0, 12, 14);
            AssertAccepted(blocked, new GatherCommand(blocked.Clock.Tick, P0, Seq(), blockedWorker, 0));
            for (int i = 0; i < 200 && Get(blocked, blockedWorker).GatherState != GatherState.Gathering; i++)
            {
                blocked.Step();
            }
            AssertAccepted(blocked, new BuildStructureCommand(blocked.Clock.Tick, P0, Seq(), blockedWorker, TestWorld.BarracksId, 16, 12));
            Do(blocked, new SpawnUnitCommand(blocked.Clock.Tick, PlayerId.None, Seq(), TestWorld.TowerId, P1, At(16, 12)));
            events = new List<SimEvent>();
            for (int i = 0; i < 200 && Of<BuildOrderCancelledEvent>(events).Count == 0; i++)
            {
                blocked.Step();
                events = new List<SimEvent>(blocked.Events.Current);
            }
            Assert.That(First<BuildOrderCancelledEvent>(events).Reason, Is.EqualTo("site_blocked"));
            Assert.That(Get(blocked, blockedWorker).GatherState, Is.EqualTo(GatherState.ToField));
            Assert.That(Get(blocked, blockedWorker).GatherFieldIndex, Is.EqualTo(0));

            // Depleted variant: the field runs dry before the placement; the worker ends Idle.
            Simulation dry = TestWorld.StartedEconomy();
            Spawn(dry, TestWorld.HqId, P0, 12, 12);
            EntityId dryWorker = Spawn(dry, TestWorld.WorkerId, P0, 12, 14);
            AssertAccepted(dry, new GatherCommand(dry.Clock.Tick, P0, Seq(), dryWorker, 0));
            for (int i = 0; i < 200 && Get(dry, dryWorker).GatherState != GatherState.Gathering; i++)
            {
                dry.Step();
            }
            AssertAccepted(dry, new BuildStructureCommand(dry.Clock.Tick, P0, Seq(), dryWorker, TestWorld.BarracksId, 16, 12));
            dry.Resources[0].Take(1000);
            events = new List<SimEvent>();
            for (int i = 0; i < 200 && Of<StructurePlacedEvent>(events).Count == 0; i++)
            {
                dry.Step();
                events = new List<SimEvent>(dry.Events.Current);
            }
            Assert.That(Of<StructurePlacedEvent>(events).Count, Is.EqualTo(1));
            Assert.That(Get(dry, dryWorker).GatherState, Is.EqualTo(GatherState.Idle));
        }

        /// <summary>WC-17: a site on a terrace with no ramp cannot be reached; the order is cancelled on the tick movement gives up.</summary>
        [Test]
        public void Wc17_UnreachableSite()
        {
            // A 64 by 64 map: high terrace for x of 32 or more, no ramp. Heights row-major. Range: 0 or 1.
            byte[] heights = new byte[TestWorld.FlatMapSize * TestWorld.FlatMapSize];
            for (int y = 0; y < TestWorld.FlatMapSize; y++)
            {
                for (int x = 32; x < TestWorld.FlatMapSize; x++)
                {
                    heights[y * TestWorld.FlatMapSize + x] = 1;
                }
            }
            MapDefinition map = new MapDefinition("test_no_ramp", "Test No Ramp", TestWorld.FlatMapSize, TestWorld.FlatMapSize,
                new List<FixVec2> { At(4, 4), At(60, 60) }, heights, new bool[heights.Length], null, null);
            Simulation sim = TestWorld.StartedEconomy(map: map);
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 10, 30);
            Spawn(sim, TestWorld.FlyerId, P0, 40, 30);
            int t0 = sim.Clock.Tick;
            List<SimEvent> events = Do(sim, new BuildStructureCommand(t0, P0, Seq(), worker, TestWorld.BarracksId, 40, 30));
            events.AddRange(StepCollect(sim, 5));
            Assert.That(Of<MoveUnreachableEvent>(events), Is.Not.Empty);
            BuildOrderCancelledEvent cancelled = First<BuildOrderCancelledEvent>(events);
            Assert.That(cancelled.Reason, Is.EqualTo("unreachable"));
            Assert.That(cancelled.RefundCommon, Is.EqualTo(100));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
        }

        /// <summary>A state hash taken at tick t0 + <paramref name="steps"/> of a WC layout after <paramref name="mutate"/> changed the worker.</summary>
        private static ulong HashAfter(int steps, System.Action<Unit>? mutate)
        {
            Layout w = new Layout();
            w.StepThrough(w.T0 + steps);
            mutate?.Invoke(w.WorkerUnit);
            return w.Sim.ComputeStateHash();
        }

        /// <summary>WC-18: every new unit field is in the hash; identical runs hash alike.</summary>
        [Test]
        public void Wc18_HashCoversTheNewFields()
        {
            // Placing at t0 + 30.
            ulong baseline = HashAfter(30, null);
            Assert.That(HashAfter(30, null), Is.EqualTo(baseline));
            // Remaining placement time, tile, definition and resume field, each changed alone (state back to Placing with the same ticks).
            Assert.That(HashAfter(30, u => u.StartPlacing(u.BuildTicksRemaining + 1)), Is.Not.EqualTo(baseline), "ticks");
            Assert.That(HashAfter(30, u => { int t = u.BuildTicksRemaining; u.SetBuildOrder(u.BuildDefinitionId, u.BuildTileX + 1, u.BuildTileY, u.BuildResumeFieldIndex); u.StartPlacing(t); }), Is.Not.EqualTo(baseline), "tile x");
            Assert.That(HashAfter(30, u => { int t = u.BuildTicksRemaining; u.SetBuildOrder(u.BuildDefinitionId, u.BuildTileX, u.BuildTileY + 1, u.BuildResumeFieldIndex); u.StartPlacing(t); }), Is.Not.EqualTo(baseline), "tile y");
            Assert.That(HashAfter(30, u => { int t = u.BuildTicksRemaining; u.SetBuildOrder(TestWorld.TowerId, u.BuildTileX, u.BuildTileY, u.BuildResumeFieldIndex); u.StartPlacing(t); }), Is.Not.EqualTo(baseline), "definition");
            Assert.That(HashAfter(30, u => { int t = u.BuildTicksRemaining; u.SetBuildOrder(u.BuildDefinitionId, u.BuildTileX, u.BuildTileY, 1); u.StartPlacing(t); }), Is.Not.EqualTo(baseline), "resume field");
            // State alone: walking (ToSite, 0 ticks) against the same moment with Placing and 0 ticks.
            ulong walking = HashAfter(5, null);
            Assert.That(HashAfter(5, u => u.StartPlacing(0)), Is.Not.EqualTo(walking), "state");
        }

        /// <summary>WC-19: the build command's payload bytes are unchanged and round-trip.</summary>
        [Test]
        public void Wc19_WireFormatUnchanged()
        {
            CommandCodecRegistry registry = BuiltInMechanics.CreateCodecRegistry();
            Command command = new BuildStructureCommand(0, P0, 0, new EntityId(42), "ab", 3, 4);
            byte[] payload = registry.EncodePayload(command);
            Assert.That(System.BitConverter.ToString(payload).Replace("-", string.Empty).ToLowerInvariant(),
                Is.EqualTo("2a000000" + "0200" + "6162" + "03000000" + "04000000"));
            BuildStructureCommand decoded = (BuildStructureCommand)registry.DecodePayload(command.Kind, 0, P0, 0, payload);
            Assert.That(decoded.Worker, Is.EqualTo(new EntityId(42)));
            Assert.That(decoded.DefinitionId, Is.EqualTo("ab"));
            Assert.That(decoded.TileX, Is.EqualTo(3));
            Assert.That(decoded.TileY, Is.EqualTo(4));
        }

        /// <summary>WC-20: a placing worker is not pushed by separation; a builder walking past a stopped unit still arrives on time.</summary>
        [Test]
        public void Wc20_BuildersDoNotSeparate()
        {
            Layout w = new Layout();
            w.StepThrough(w.T0 + 30);
            FixVec2 where = w.WorkerUnit.Position;
            Do(w.Sim, new SpawnUnitCommand(w.Sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.RunnerId, P0, where + new FixVec2(Fix64.Parse("0.2"), Fix64.Zero)));
            w.StepThrough(w.T0 + 40);
            Assert.That(w.WorkerUnit.Position, Is.EqualTo(where));
            Assert.That(w.WorkerUnit.BuildState, Is.EqualTo(BuildOrderState.Placing));

            Layout walk = new Layout();
            // The layout stepped one tick already with the order; a stopped runner goes next to the goal on the following tick.
            Do(walk.Sim, new SpawnUnitCommand(walk.Sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.RunnerId, P0, At(19, 16)));
            List<SimEvent> events = walk.StepThrough(walk.T0 + 22);
            Assert.That(First<BuildOrderStartedEvent>(events).Tick, Is.EqualTo(walk.T0 + 22));
        }

        /// <summary>WC-21: structures spawned directly by a script are complete at once and raise no build events.</summary>
        [Test]
        public void Wc21_DirectSpawnsUnchanged()
        {
            Simulation sim = TestWorld.StartedEconomy();
            List<SimEvent> events = Do(sim, new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.BarracksId, P0, At(20, 14)));
            Unit barracks = Get(sim, First<UnitSpawnedEvent>(events).Unit);
            Assert.That(barracks.ConstructionTicksRemaining, Is.EqualTo(0));
            Assert.That(Of<BuildOrderAcceptedEvent>(events), Is.Empty);
            Assert.That(Of<StructurePlacedEvent>(events), Is.Empty);
        }

        /// <summary>Runs economy_smoke.json in process and returns every event of the run.</summary>
        private static List<SimEvent> RunEconomySmoke()
        {
            DataSet data = TestData.LoadAll();
            string path = Path.Combine(TestData.FindDataDirectory(), "scripts", "economy_smoke.json");
            CommandScript script = CommandScript.Parse(File.ReadAllText(path), "economy_smoke.json", BuiltInMechanics.ScriptParsers);
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(script.Seed), data.Content, script.Players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap(script.MapId), data.Cycles.Values));
            sim.Start();
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            return StepCollect(sim, script.Ticks);
        }

        /// <summary>WC-22: economy_smoke gives identical output twice, its three orders are placed, nothing is cancelled and the only rejection is the scripted hq_rooted move.</summary>
        [Test]
        public void Wc22_EconomySmoke()
        {
            List<SimEvent> events = RunEconomySmoke();
            Assert.That(Of<StructurePlacedEvent>(events).Count, Is.EqualTo(3));
            Assert.That(Of<BuildOrderCancelledEvent>(events), Is.Empty);
            Assert.That(Reasons(events), Is.EqualTo(new List<string> { "hq_rooted" }), "the script's one intended rejection (tick 1290)");
            string folder = TestData.FindDataDirectory();
            string script = Path.Combine(folder, "scripts", "economy_smoke.json");
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--script", script }, out string first, out string firstError), Is.EqualTo(0));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--script", script }, out string second, out string secondError), Is.EqualTo(0));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(secondError, Is.EqualTo(firstError));
            Assert.That(firstError.Split('\n').Length, Is.LessThanOrEqualTo(2), "only the scripted hq_rooted warning");
            // Re-pinned for the missing roster (specs/missing_roster.md R2-9): the content digest now includes the three new unit files. Old value 93762ddc16910b23; outcome unchanged.
            // Re-pinned for combat mechanics package 1 (specs/combat_mechanics.md): old value aff851b30f63e4f8, outcome unchanged (undecided, tick 1499). Re-pinned again for the slot queue tie-break (`GatherSlotGrants` hashed after `GatherQueuedTick`): old 706626f97350b663, new 12259d6626becbc0; outcome unchanged. Re-pinned for combat mechanics package 2 (structure_damage_multiplier in the content digest): 9a71eaccaab510c5 became e4127bf62b50cbc1, outcome unchanged.
            // Re-pinned for F0 (specs/micro_benchmark.md 3.3, developer decision 4 of 18 Nov 2026: Rooted HQ floor 20 s to 15 s in hq.json and harness.json, both in the content digest): old value e4127bf62b50cbc1, outcome unchanged (undecided, tick 1499).
            // Re-pinned for combat mechanics package 3 (specs/combat_mechanics.md: can_target_air_on_water is in the weapon digest, Reef Runner and Shellback carry it): old value 9cffda66d3cb0548, outcome unchanged (undecided, tick 1499).
            // Re-pinned for combat mechanics package 4 (specs/combat_mechanics.md: grounding_ability in the content digest and Unit.GroundingReadyTick in the unit hash, the Stormcaller carries the ability): old value d951951064e0b735, outcome unchanged (undecided, tick 1499).
            // Re-pinned for gates (specs/walls_and_tech.md package G: GateTeam in the tile hash, is_gate in the content digest, two gate unit files): old value 57e92601f6755ea6, outcome unchanged (undecided, tick 1499).
            // Re-pinned for drag to place (specs/walls_and_tech.md package D: Unit.BuildQueue in the unit hash, wall_line_max_tiles in the tuning digest): old value ea7a4a9d577c9b83, outcome unchanged (undecided, tick 1499).
            // Re-pinned for field saturation (specs/field_saturation.md): gather slot keys in the tuning digest, `GatherQueuedTick` in the unit hash. Old value ce62b175ea751ba6, outcome unchanged (undecided, tick 1499).
            Assert.That(first, Does.Contain("STATE_HASH=72128f50a116322b\n"));
        }
    }
}

// rts/tests/Sim.Tests/Economy/WallLineTests.cs
// Acceptance tests D-1 to D-14 of package D of rts/docs/specs/walls_and_tech.md (drag to place): build_wall_line pays for a dragged line up front,
// stores the tiles on the worker (Unit.BuildQueue), builds them one after another, refunds what is not built, and WallLine.Rasterise draws
// lines with no walkable gap. D-12 (flood) lives in FloodingTests; the codec and script parts of D-13 live in CommandCodecTests,
// the Net and Replay codec tests and CommandScriptTests. Part of Rts.Sim.Tests.
// Economy setup: TestWorld's economy map and shipped tuning, test.wall cost 25 and build 5 s (100 ticks), worker spawned at (14, 14) before t0.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;
using CombatRig = Rts.Sim.Tests.Combat.CombatRig;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Dragged wall lines.</summary>
    [TestFixture]
    public sealed class WallLineTests
    {
        /// <summary>Wall definition of the tests (200 health, cost 25, build 5 s, footprint 1, is_wall).</summary>
        private const string WallId = "test.wall";

        /// <summary>Wall of D-9 that costs 1 supply.</summary>
        private const string SupplyWallId = "test.wall_s1";

        /// <summary>Mover of D-11: ground, speed 4, radius 0.5.</summary>
        private const string MoverId = "test.mover4";

        /// <summary>Wall (or supply wall) definition in the economy setup (armour "armoured").</summary>
        private static UnitDefinition Wall(string id, int supply, string armour)
        {
            return new UnitDefinition(id, "Wall", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(200), Fix64.Zero, supply, Fix64.Half, 25, 0,
                armour, Fix64.FromInt(3), null, null, new UnitEconomyDefinition(Fix64.FromInt(5), false, false, new List<string>(), false, 1, 0, true));
        }

        /// <summary>Economy simulation with the test walls in the catalog.</summary>
        private static Simulation EconomySim()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(Wall(WallId, 0, "armoured"));
            catalog.AddUnit(Wall(SupplyWallId, 1, "armoured"));
            SimulationConfig config = new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), catalog,
                TestWorld.TwoPlayers(), BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.EconomyMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>The rasterised line from (x0, y0) to (x1, y1), at most 16 tiles.</summary>
        private static List<(int X, int Y)> Line(int x0, int y0, int x1, int y1, int max = 16)
        {
            List<(int X, int Y)> tiles = new List<(int X, int Y)>();
            WallLine.Rasterise(x0, y0, x1, y1, max, tiles);
            return tiles;
        }

        /// <summary>The wall-line command of P0 for the tick the simulation is at.</summary>
        private static BuildWallLineCommand LineOrder(Simulation sim, EntityId worker, IEnumerable<(int X, int Y)> tiles, string definition = WallId)
        {
            return new BuildWallLineCommand(sim.Clock.Tick, P0, Seq(), worker, definition, tiles);
        }

        /// <summary>Steps <paramref name="ticks"/> ticks collecting events, failing when a unit has queued tiles but no build order (D.2 rule 10).</summary>
        private static List<SimEvent> StepChecked(Simulation sim, int ticks)
        {
            List<SimEvent> all = new List<SimEvent>();
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
                all.AddRange(sim.Events.Current);
                foreach (Unit unit in sim.Units.All)
                {
                    Assert.That(unit.BuildQueue.Count == 0 || unit.BuildState != BuildOrderState.None, Is.True, "queue invariant, tick " + sim.Clock.Tick);
                }
            }
            return all;
        }

        /// <summary>Asserts every listed tile is explored by P0's team (the precondition of the line tests).</summary>
        private static void AssertExplored(Simulation sim, IEnumerable<(int X, int Y)> tiles)
        {
            foreach ((int x, int y) in tiles)
            {
                Assert.That(sim.Vision.IsTileExplored(sim.Players.TeamOf(P0), x, y), Is.True, "explored " + x + "," + y);
            }
        }

        /// <summary>Events on tick <paramref name="tick"/> only.</summary>
        private static List<T> On<T>(List<SimEvent> events, int tick) where T : SimEvent
        {
            return Of<T>(events).FindAll(e => e.Tick == tick);
        }

        /// <summary>Runs D-1's line from t0 for 260 ticks; returns the start tick, the events (including t0's) and the simulation through <paramref name="extra"/>.</summary>
        private static (Simulation Sim, EntityId Worker, int T0, List<SimEvent> Events) RunThreeWalls(int ticksAfter = 260)
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            List<(int X, int Y)> tiles = new List<(int X, int Y)> { (20, 14), (20, 15), (20, 16) };
            AssertExplored(sim, tiles);
            int t0 = sim.Clock.Tick;
            List<SimEvent> events = Do(sim, LineOrder(sim, worker, tiles));
            events.AddRange(StepChecked(sim, ticksAfter));
            return (sim, worker, t0, events);
        }

        // ------------------------------------------------------------------ D-1

        /// <summary>D-1: three walls in a row by one worker: accepted t0 (cost 75), then 0 on t0 + 62 and t0 + 105; placed t0 + 62, 105, 149; completed t0 + 162, 205, 249; common 125.</summary>
        [Test]
        public void D1_ThreeWallsInARow()
        {
            (Simulation sim, EntityId worker, int t0, List<SimEvent> events) = RunThreeWalls();
            List<BuildOrderAcceptedEvent> accepted = Of<BuildOrderAcceptedEvent>(events);
            Assert.That(accepted.Select(e => (e.Tick - t0, e.TileX, e.TileY, e.CostCommon)).ToList(),
                Is.EqualTo(new List<(int, int, int, int)> { (0, 20, 14, 75), (62, 20, 15, 0), (105, 20, 16, 0) }));
            Assert.That(Of<BuildOrderStartedEvent>(events).Select(e => e.Tick - t0).ToList(), Is.EqualTo(new List<int> { 22, 65, 109 }));
            Assert.That(Of<StructurePlacedEvent>(events).Select(e => e.Tick - t0).ToList(), Is.EqualTo(new List<int> { 62, 105, 149 }));
            Assert.That(Of<StructureCompletedEvent>(events).Select(e => e.Tick - t0).ToList(), Is.EqualTo(new List<int> { 162, 205, 249 }));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(125));
            Assert.That(Get(sim, worker).BuildState, Is.EqualTo(BuildOrderState.None));
            Assert.That(Get(sim, worker).BuildQueue, Is.Empty);
            Assert.That(Of<BuildOrderCancelledEvent>(events), Is.Empty);
        }

        /// <summary>D-1 (never on a line tile): the worker never stands on a tile of the line.</summary>
        [Test]
        public void D1_WorkerNeverStandsOnTheLine()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            AssertAccepted(sim, LineOrder(sim, worker, new List<(int X, int Y)> { (20, 14), (20, 15), (20, 16) }));
            for (int i = 0; i < 260; i++)
            {
                sim.Step();
                Assert.That(sim.Terrain[sim.Terrain.TileOf(Get(sim, worker).Position)].Blockers, Is.EqualTo(TileBlockers.None), "tick " + sim.Clock.Tick);
            }
        }

        // ------------------------------------------------------------------ D-2, D-3

        /// <summary>D-2: ten tiles with 200 common keep eight (common 0), skip two with insufficient_resources and queue seven; with 20 common the order is rejected.</summary>
        [Test]
        public void D2_MoneyRunsOut()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            List<(int X, int Y)> tiles = Line(17, 10, 17, 19);
            AssertExplored(sim, tiles);
            List<SimEvent> events = Do(sim, LineOrder(sim, worker, tiles));
            Assert.That(Reasons(events), Is.Empty);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(0));
            Assert.That(Of<BuildOrderAcceptedEvent>(events).Single().CostCommon, Is.EqualTo(200));
            List<BuildLineTileSkippedEvent> skipped = Of<BuildLineTileSkippedEvent>(events);
            Assert.That(skipped.Select(e => (e.TileX, e.TileY, e.Reason, e.RefundCommon)).ToList(),
                Is.EqualTo(new List<(int, int, string, int)> { (17, 18, "insufficient_resources", 0), (17, 19, "insufficient_resources", 0) }));
            Assert.That(Get(sim, worker).BuildQueue.Count, Is.EqualTo(7));
            Simulation poor = EconomySim();
            EntityId poorWorker = Spawn(poor, TestWorld.WorkerId, P0, 14, 14);
            PlayerOf(poor, P0).Spend(180, 0);
            AssertRejected(poor, LineOrder(poor, poorWorker, tiles), "insufficient_resources");
            Assert.That(PlayerOf(poor, P0).Common, Is.EqualTo(20));
            Assert.That(Get(poor, poorWorker).BuildState, Is.EqualTo(BuildOrderState.None));
        }

        /// <summary>D-3: an enemy wall in the middle is skipped tile_blocked (paid 100); a repeated tile is skipped duplicate_tile (paid 50); a line wholly off the map is rejected out_of_bounds.</summary>
        [Test]
        public void D3_BadTilesInTheMiddle()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            Spawn(sim, WallId, P1, 17, 14);
            List<SimEvent> events = Do(sim, LineOrder(sim, worker, Line(17, 12, 17, 16)));
            Assert.That(Of<BuildLineTileSkippedEvent>(events).Select(e => (e.TileX, e.TileY, e.Reason)).ToList(), Is.EqualTo(new List<(int, int, string)> { (17, 14, "tile_blocked") }));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(100));
            Assert.That(Get(sim, worker).BuildQueue, Is.EqualTo(new List<int> { sim.Terrain.IndexOf(17, 13), sim.Terrain.IndexOf(17, 15), sim.Terrain.IndexOf(17, 16) }));
            Simulation dup = EconomySim();
            EntityId dupWorker = Spawn(dup, TestWorld.WorkerId, P0, 14, 14);
            List<SimEvent> dupEvents = Do(dup, LineOrder(dup, dupWorker, new List<(int X, int Y)> { (17, 12), (17, 13), (17, 12) }));
            Assert.That(Of<BuildLineTileSkippedEvent>(dupEvents).Select(e => (e.TileX, e.TileY, e.Reason)).ToList(), Is.EqualTo(new List<(int, int, string)> { (17, 12, "duplicate_tile") }));
            Assert.That(PlayerOf(dup, P0).Common, Is.EqualTo(150));
            Simulation off = EconomySim();
            EntityId offWorker = Spawn(off, TestWorld.WorkerId, P0, 14, 14);
            AssertRejected(off, LineOrder(off, offWorker, new List<(int X, int Y)> { (-1, 5), (-2, 5), (500, 500) }), "out_of_bounds");
            Assert.That(PlayerOf(off, P0).Common, Is.EqualTo(200));
        }

        /// <summary>A wall line across the field tile (10, 10) skips that tile on_resource_field, pays for the others and queues the rest.</summary>
        [Test]
        public void D3b_FieldTileIsSkipped()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            List<SimEvent> events = Do(sim, LineOrder(sim, worker, Line(10, 9, 10, 11)));
            Assert.That(Reasons(events), Is.Empty);
            Assert.That(Of<BuildLineTileSkippedEvent>(events).Select(e => (e.TileX, e.TileY, e.Reason)).ToList(), Is.EqualTo(new List<(int, int, string)> { (10, 10, "on_resource_field") }));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(150));
            Assert.That(Get(sim, worker).BuildQueue, Is.EqualTo(new List<int> { sim.Terrain.IndexOf(10, 11) }));
        }

        // ------------------------------------------------------------------ D-4

        /// <summary>D-4: an enemy unit on the middle tile leaves a hole: (20, 15) is cancelled site_occupied with a refund on t0 + 65, (20, 16) goes on and is placed t0 + 109; common 150; the same line again fills only the hole.</summary>
        [Test]
        public void D4_UnitInTheWayLeavesAHole()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            // The tank stands at the tile centre (20.5, 15.5), so separation from the walking worker cannot push it onto a neighbouring tile.
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.TankId, P1, new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("15.5"))));
            sim.Step();
            EntityId tank = sim.Events.Current.OfType<UnitSpawnedEvent>().Single().Unit;
            int t0 = sim.Clock.Tick;
            List<(int X, int Y)> tiles = new List<(int X, int Y)> { (20, 14), (20, 15), (20, 16) };
            List<SimEvent> events = Do(sim, LineOrder(sim, worker, tiles));
            Assert.That(Reasons(events), Is.Empty);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(125));
            events.AddRange(StepChecked(sim, 200));
            Assert.That(Of<BuildOrderCancelledEvent>(events).Count, Is.EqualTo(1), string.Join("\n", events.Where(e => e is BuildOrderAcceptedEvent || e is BuildOrderStartedEvent || e is StructurePlacedEvent || e is BuildLineTileSkippedEvent || e is UnitRemovedEvent).Select(e => (e.Tick - t0) + " " + e.Describe())));
            BuildOrderCancelledEvent cancelled = Of<BuildOrderCancelledEvent>(events).Single();
            Assert.That((cancelled.Tick - t0, cancelled.TileX, cancelled.TileY, cancelled.Reason, cancelled.RefundCommon), Is.EqualTo((65, 20, 15, "site_occupied", 25)));
            Assert.That(On<BuildOrderAcceptedEvent>(events, cancelled.Tick).Select(e => (e.TileX, e.TileY, e.CostCommon)).ToList(), Is.EqualTo(new List<(int, int, int)> { (20, 16, 0) }));
            Assert.That(Of<StructurePlacedEvent>(events).Select(e => (e.Tick - t0, e.TileX, e.TileY)).ToList(), Is.EqualTo(new List<(int, int, int)> { (62, 20, 14), (109, 20, 16) }));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(150));
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), tank));
            List<SimEvent> again = Do(sim, LineOrder(sim, worker, tiles));
            Assert.That(Of<BuildLineTileSkippedEvent>(again).Select(e => (e.TileX, e.TileY, e.Reason)).ToList(),
                Is.EqualTo(new List<(int, int, string)> { (20, 14, "tile_blocked"), (20, 16, "tile_blocked") }));
            Assert.That(Get(sim, worker).BuildQueue, Is.Empty);
            Assert.That((Get(sim, worker).BuildTileX, Get(sim, worker).BuildTileY), Is.EqualTo((20, 15)));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(125));
        }

        // ------------------------------------------------------------------ D-5, D-6, D-9

        /// <summary>D-5: the worker removed on t0 + 70 refunds the active tile (worker_lost) and the queued one (worker_lost): common 175, one wall stands.</summary>
        [Test]
        public void D5_WorkerDiesMidLine()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            int t0 = sim.Clock.Tick;
            List<SimEvent> events = Do(sim, LineOrder(sim, worker, new List<(int X, int Y)> { (20, 14), (20, 15), (20, 16) }));
            events.AddRange(StepChecked(sim, 69));
            List<SimEvent> death = Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), worker));
            Assert.That(sim.Clock.Tick, Is.EqualTo(t0 + 71));
            BuildOrderCancelledEvent cancelled = Of<BuildOrderCancelledEvent>(death).Single();
            Assert.That((cancelled.TileX, cancelled.TileY, cancelled.Reason, cancelled.RefundCommon), Is.EqualTo((20, 15, "worker_lost", 25)));
            BuildLineTileSkippedEvent skipped = Of<BuildLineTileSkippedEvent>(death).Single();
            Assert.That((skipped.TileX, skipped.TileY, skipped.Reason, skipped.RefundCommon), Is.EqualTo((20, 16, "worker_lost", 25)));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(175));
            events.AddRange(StepChecked(sim, 200));
            Assert.That(Of<StructurePlacedEvent>(events).Select(e => (e.TileX, e.TileY)).ToList(), Is.EqualTo(new List<(int, int)> { (20, 14) }));
        }

        /// <summary>D-6: a move order on t0 + 70 refunds like D-5 with reason new_order and leaves an empty queue; a second line order refunds the same and then pays the new line.</summary>
        [Test]
        public void D6_NewOrderMidLine()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            Do(sim, LineOrder(sim, worker, new List<(int X, int Y)> { (20, 14), (20, 15), (20, 16) }));
            StepChecked(sim, 69);
            List<SimEvent> moved = Do(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), worker, At(16, 14)));
            Assert.That(Of<BuildOrderCancelledEvent>(moved).Single().Reason, Is.EqualTo("new_order"));
            Assert.That(Of<BuildLineTileSkippedEvent>(moved).Single().Reason, Is.EqualTo("new_order"));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(175));
            Assert.That(Get(sim, worker).BuildQueue, Is.Empty);
            Assert.That(Get(sim, worker).HasMoveTarget, Is.True);

            Simulation second = EconomySim();
            EntityId other = Spawn(second, TestWorld.WorkerId, P0, 14, 14);
            Do(second, LineOrder(second, other, new List<(int X, int Y)> { (20, 14), (20, 15), (20, 16) }));
            StepChecked(second, 69);
            List<SimEvent> replaced = Do(second, LineOrder(second, other, new List<(int X, int Y)> { (17, 12), (17, 13) }));
            Assert.That(Of<BuildOrderCancelledEvent>(replaced).Single().Reason, Is.EqualTo("new_order"));
            Assert.That(Of<BuildLineTileSkippedEvent>(replaced).Count(e => e.Reason == "new_order"), Is.EqualTo(1));
            Assert.That(PlayerOf(second, P0).Common, Is.EqualTo(175 - 50));
            Assert.That(Get(second, other).BuildQueue.Count, Is.EqualTo(1));
        }

        /// <summary>D-9: with a wall that costs 1 supply, a 3-tile line raises SupplyUsed by 3; after the worker is removed on t0 + 70 only the placed wall counts (1).</summary>
        [Test]
        public void D9_Supply()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            int before = PlayerOf(sim, P0).SupplyUsed;
            // Supply the worker itself uses; it leaves with the worker. Unit: supply.
            int workerSupply = Get(sim, worker).Definition.Supply;
            Do(sim, LineOrder(sim, worker, new List<(int X, int Y)> { (20, 14), (20, 15), (20, 16) }, SupplyWallId));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(before + 3));
            StepChecked(sim, 69);
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), worker));
            // The worker's own supply left with it; the placed wall's 1 stays.
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(before - workerSupply + 1));
        }

        // ------------------------------------------------------------------ D-7, D-8

        /// <summary>D-7: two workers with disjoint lines are both accepted; a third line that names a tile in the other's queue skips it site_pending; a single build order for that tile is rejected site_pending.</summary>
        [Test]
        public void D7_TwoWorkers()
        {
            Simulation sim = EconomySim();
            EntityId a = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            EntityId b = Spawn(sim, TestWorld.WorkerId, P0, 14, 16);
            sim.Submit(new BuildWallLineCommand(sim.Clock.Tick, P0, Seq(), a, WallId, new List<(int X, int Y)> { (20, 14) }));
            sim.Submit(new BuildWallLineCommand(sim.Clock.Tick, P0, Seq(), b, WallId, new List<(int X, int Y)> { (20, 15), (20, 16) }));
            sim.Step();
            Assert.That(Reasons(new List<SimEvent>(sim.Events.Current)), Is.Empty);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(125));
            List<SimEvent> events = Do(sim, new BuildWallLineCommand(sim.Clock.Tick, P0, Seq(), a, WallId, new List<(int X, int Y)> { (20, 16), (20, 17) }));
            Assert.That(Of<BuildLineTileSkippedEvent>(events).Select(e => (e.TileX, e.TileY, e.Reason)).ToList(), Is.EqualTo(new List<(int, int, string)> { (20, 16, "site_pending") }));
            Assert.That(Of<BuildOrderCancelledEvent>(events).Single().Reason, Is.EqualTo("new_order"));
            Assert.That((Get(sim, a).BuildTileX, Get(sim, a).BuildTileY), Is.EqualTo((20, 17)));
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), a, WallId, 20, 16), "site_pending");
            StepChecked(sim, 300);
        }

        /// <summary>D-8: an empty list, 17 tiles, a non-wall and another player's worker are each rejected with stock and orders unchanged.</summary>
        [Test]
        public void D8_Limits()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            EntityId enemy = Spawn(sim, TestWorld.WorkerId, P1, 50, 50);
            AssertRejected(sim, LineOrder(sim, worker, new List<(int X, int Y)>()), "no_tiles");
            List<(int X, int Y)> seventeen = Enumerable.Range(0, 17).Select(i => (10 + i % 8, 10 + i / 8)).ToList();
            AssertRejected(sim, LineOrder(sim, worker, seventeen), "too_many_tiles");
            AssertRejected(sim, LineOrder(sim, worker, Line(17, 12, 17, 14), TestWorld.TowerId), "not_a_wall");
            AssertRejected(sim, LineOrder(sim, enemy, Line(17, 12, 17, 14)), "not_owner");
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            Assert.That(Get(sim, worker).BuildState, Is.EqualTo(BuildOrderState.None));
            Assert.That(Get(sim, enemy).BuildState, Is.EqualTo(BuildOrderState.None));
        }

        // ------------------------------------------------------------------ D-10

        /// <summary>D-10: the rasteriser on the spec's examples, both directions, a single tile, a short line, a capped line; the return value is the count.</summary>
        [Test]
        public void D10_Rasteriser()
        {
            Assert.That(Line(0, 0, 3, 1), Is.EqualTo(new List<(int, int)> { (0, 0), (1, 0), (2, 1), (3, 1) }));
            Assert.That(Line(3, 1, 0, 0), Is.EqualTo(new List<(int, int)> { (3, 1), (2, 1), (1, 0), (0, 0) }));
            Assert.That(Line(0, 0, 2, 2), Is.EqualTo(new List<(int, int)> { (0, 0), (1, 1), (2, 2) }));
            Assert.That(Line(5, 5, 5, 5), Is.EqualTo(new List<(int, int)> { (5, 5) }));
            Assert.That(Line(0, 0, 4, 0).Count, Is.EqualTo(5));
            List<(int X, int Y)> capped = Line(0, 0, 20, 0, 16);
            Assert.That(capped.Count, Is.EqualTo(16));
            Assert.That(capped[0], Is.EqualTo((0, 0)));
            Assert.That(capped[15], Is.EqualTo((15, 0)));
            List<(int X, int Y)> output = new List<(int X, int Y)> { (9, 9) };
            Assert.That(WallLine.Rasterise(1, 1, 1, 4, 16, output), Is.EqualTo(output.Count));
            Assert.That(output, Is.EqualTo(new List<(int, int)> { (1, 1), (1, 2), (1, 3), (1, 4) }));
            Assert.That(WallLine.Rasterise(0, 0, 3, 3, 0, output), Is.EqualTo(0));
            // Every line is 8-connected and has min(max(|dx|, |dy|) + 1, max) tiles.
            for (int dx = -9; dx <= 9; dx++)
            {
                for (int dy = -9; dy <= 9; dy++)
                {
                    List<(int X, int Y)> tiles = Line(0, 0, dx, dy, 64);
                    Assert.That(tiles.Count, Is.EqualTo(System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) + 1));
                    for (int i = 1; i < tiles.Count; i++)
                    {
                        Assert.That(System.Math.Max(System.Math.Abs(tiles[i].X - tiles[i - 1].X), System.Math.Abs(tiles[i].Y - tiles[i - 1].Y)), Is.EqualTo(1));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ D-11

        /// <summary>The combat rig with a mover of P1 at (<paramref name="mx"/>, <paramref name="my"/>) ordered to (<paramref name="gx"/>, <paramref name="gy"/>) on tick 0 and P2 walls on every tile of the line except column <paramref name="skipColumn"/> (-1 for none).</summary>
        private static (CombatRig Rig, EntityId Mover) DiagonalRig(int x1, int y1, string mx, string my, string gx, string gy, int skipColumn)
        {
            ContentCatalog catalog = CombatRig.NewCatalog();
            catalog.AddUnit(Wall(WallId, 0, "structure"));
            catalog.AddUnit(new UnitDefinition(MoverId, "Mover", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 0, 0));
            CombatRig rig = new CombatRig(catalog);
            EntityId mover = rig.Add(MoverId, CombatRig.P1, mx, my);
            foreach ((int x, int y) in Line(0, 0, x1, y1, 24))
            {
                if (x != skipColumn)
                {
                    rig.Add(WallId, CombatRig.P2, x + ".5", y + ".5");
                }
            }
            rig.Sim.Submit(new MoveUnitCommand(0, PlayerId.None, 7000, mover, new FixVec2(CombatRig.F(gx), CombatRig.F(gy))));
            return (rig, mover);
        }

        /// <summary>D-11: a P2 line from (0, 0) to (23, 11) or (23, 23) has no walkable gap: the mover's order ends move_unreachable on tick 0; without a tile of column 12 it arrives before tick 300.</summary>
        [Test]
        public void D11_DiagonalLinesHaveNoWalkableGap()
        {
            (CombatRig shallow, EntityId a) = DiagonalRig(23, 11, "20.5", "2.5", "2.5", "20.5", -1);
            shallow.Step(1);
            Assert.That(shallow.Of<MoveUnreachableEvent>().Any(e => e.Tick == 0 && e.Unit == a), Is.True);
            (CombatRig steep, EntityId b) = DiagonalRig(23, 23, "15.5", "5.5", "5.5", "15.5", -1);
            steep.Step(1);
            Assert.That(steep.Of<MoveUnreachableEvent>().Any(e => e.Tick == 0 && e.Unit == b), Is.True);
            (CombatRig control, EntityId c) = DiagonalRig(23, 11, "20.5", "2.5", "2.5", "20.5", 12);
            control.Step(300);
            Assert.That(control.Of<UnitArrivedEvent>().Any(e => e.Unit == c && e.Tick < 300), Is.True);
        }

        // ------------------------------------------------------------------ D-14

        /// <summary>State hashes of D-1's run with the second tile at (20, <paramref name="secondY"/>), one per hash tick.</summary>
        private static List<ulong> Hashes(int secondY)
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            List<SimEvent> events = Do(sim, LineOrder(sim, worker, new List<(int X, int Y)> { (20, 14), (20, secondY), (20, 16) }));
            events.AddRange(StepChecked(sim, 120));
            return Of<StateHashedEvent>(events).Select(e => e.Hash).ToList();
        }

        /// <summary>D-14: two identical runs hash equal at every hash tick; a changed second tile differs at the first hash tick after t0; the queue is part of the unit hash.</summary>
        [Test]
        public void D14_Determinism()
        {
            List<ulong> first = Hashes(15);
            Assert.That(Hashes(15), Is.EqualTo(first));
            List<ulong> other = Hashes(13);
            Assert.That(other[0], Is.Not.EqualTo(first[0]));
        }
    }
}

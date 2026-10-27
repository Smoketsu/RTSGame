// rts/tests/Sim.Tests/Economy/EconomyTests.cs
// Tests for the Phase 1 economy (step 7): the worker gather loop and depletion, training with cost, build time and supply, the supply
// cap, structure construction, the static defence cap per base, build rejections, footprints that block pathing and clear on death,
// and determinism of a scripted economy. Part of Rts.Sim.Tests. Uses the in-code fixtures of TestWorld (test.hq, test.worker,
// test.barracks, test.tower) on the economy map: common field 0 at (10, 10) with 12 units, rare field 1 at (30, 10) with 50,
// common field 2 at (50, 50) with 1500. Shipped tuning: 200 common at the start, base supply 10, 5 per trip, 2 s per trip.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Gathering, production, supply and construction.</summary>
    [TestFixture]
    public class EconomyTests
    {
        /// <summary>Number of live units of a definition owned by a player.</summary>
        private static int CountOf(Simulation sim, PlayerId owner, string definition)
        {
            // Units found. Unit: count.
            int count = 0;
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == owner && unit.Definition.Id == definition)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>A worker walks to the field, gathers 5 per 40 ticks, deposits at the Rooted HQ, repeats, and stops when the 12-unit field is empty.</summary>
        [Test]
        public void Gather_RoundTrip_DepositsRightAmounts_AndDepletesField()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            EntityId workerId = Spawn(sim, TestWorld.WorkerId, P0, 14, 12);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            int commandTick = sim.Clock.Tick;
            List<SimEvent> events = Do(sim, new GatherCommand(commandTick, P0, Seq(), workerId, 0));
            Unit worker = Get(sim, workerId);
            Assert.That(worker.GatherState, Is.EqualTo(GatherState.ToField));
            Assert.That(worker.GatherFieldIndex, Is.EqualTo(0));
            events.AddRange(StepCollect(sim, 700));

            List<ResourceDepositedEvent> deposits = Of<ResourceDepositedEvent>(events);
            Assert.That(deposits.Count, Is.EqualTo(3));
            Assert.That(new[] { deposits[0].Amount, deposits[1].Amount, deposits[2].Amount }, Is.EqualTo(new[] { 5, 5, 2 }));
            foreach (ResourceDepositedEvent deposit in deposits)
            {
                Assert.That(deposit.Resource, Is.EqualTo(ResourceKind.Common));
                Assert.That(deposit.DropOff, Is.EqualTo(hqId));
                Assert.That(deposit.Owner, Is.EqualTo(P0));
                Assert.That(deposit.Worker, Is.EqualTo(workerId));
            }
            // Each trip waits gather_seconds_per_trip (2 s = 40 ticks) at the field.
            Assert.That(deposits[0].Tick - commandTick, Is.GreaterThanOrEqualTo(40));
            Assert.That(deposits[1].Tick - deposits[0].Tick, Is.GreaterThanOrEqualTo(40));
            Assert.That(deposits[2].Tick - deposits[1].Tick, Is.GreaterThanOrEqualTo(40));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(212));
            Assert.That(PlayerOf(sim, P0).Rare, Is.EqualTo(0));
            Assert.That(sim.Resources[0].Remaining, Is.EqualTo(0));
            Assert.That(sim.Resources[0].IsDepleted, Is.True);
            List<FieldDepletedEvent> depleted = Of<FieldDepletedEvent>(events);
            Assert.That(depleted.Count, Is.EqualTo(1));
            Assert.That(depleted[0].FieldIndex, Is.EqualTo(0));
            Assert.That(depleted[0].Tick, Is.LessThan(deposits[2].Tick), "the field empties when the last load is taken, before it is delivered");
            Assert.That(worker.GatherState, Is.EqualTo(GatherState.Idle));
            Assert.That(worker.CarriedAmount, Is.EqualTo(0));
            Assert.That(worker.HasMoveTarget, Is.False);
            // The other fields were not touched, and a depleted field refuses new orders.
            Assert.That(sim.Resources[1].Remaining, Is.EqualTo(50));
            Assert.That(sim.Resources[2].Remaining, Is.EqualTo(1500));
            AssertRejected(sim, new GatherCommand(sim.Clock.Tick, P0, Seq(), workerId, 0), "field_depleted");
        }

        /// <summary>Rare fields pay into the rare stock; bad gather orders are refused; a move order cancels the loop.</summary>
        [Test]
        public void Gather_RareField_RejectionsAndCancel()
        {
            Simulation sim = TestWorld.StartedEconomy();
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            EntityId workerId = Spawn(sim, TestWorld.WorkerId, P0, 29, 10);
            EntityId runner = Spawn(sim, TestWorld.RunnerId, P0, 40, 40);
            AssertRejected(sim, new GatherCommand(sim.Clock.Tick, P0, Seq(), runner, 1), "cannot_gather");
            AssertRejected(sim, new GatherCommand(sim.Clock.Tick, P0, Seq(), workerId, 7), "unknown_field");
            AssertRejected(sim, new GatherCommand(sim.Clock.Tick, P0, Seq(), workerId, -1), "unknown_field");
            AssertRejected(sim, new GatherCommand(sim.Clock.Tick, P1, Seq(), workerId, 1), "not_owner");

            AssertAccepted(sim, new GatherCommand(sim.Clock.Tick, P0, Seq(), workerId, 1));
            // Run until the first load reaches the HQ 18 tiles away.
            ResourceDepositedEvent? first = null;
            for (int i = 0; i < 800 && first == null; i++)
            {
                sim.Step();
                foreach (ResourceDepositedEvent deposit in Of<ResourceDepositedEvent>(new List<SimEvent>(sim.Events.Current)))
                {
                    first = deposit;
                }
            }
            Assert.That(first, Is.Not.Null);
            Assert.That(first!.Resource, Is.EqualTo(ResourceKind.Rare));
            Assert.That(first.Amount, Is.EqualTo(5));
            Assert.That(PlayerOf(sim, P0).Rare, Is.EqualTo(5));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            Assert.That(sim.Resources[1].Remaining, Is.EqualTo(45));

            // The worker heads back to the field; a move order cancels the loop and sends it elsewhere.
            Unit worker = Get(sim, workerId);
            Assert.That(worker.GatherState, Is.EqualTo(GatherState.ToField));
            AssertAccepted(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), workerId, At(20, 30)));
            Assert.That(worker.GatherState, Is.EqualTo(GatherState.Idle));
            Assert.That(worker.GatherFieldIndex, Is.EqualTo(-1));
            StepCollect(sim, 200);
            Assert.That(sim.Resources[1].Remaining, Is.EqualTo(45), "an idle worker takes nothing");
        }

        /// <summary>Training pays at once, reserves supply, and the unit appears build_seconds (100 ticks) later on the first free tile next to the producer.</summary>
        [Test]
        public void Train_DeductsCost_SpawnsAfterBuildSeconds()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            Unit hq = Get(sim, hqId);
            Player owner = PlayerOf(sim, P0);
            List<SimEvent> events = Do(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId));
            Assert.That(Reasons(events), Is.Empty);
            Assert.That(owner.Common, Is.EqualTo(150));
            Assert.That(owner.SupplyUsed, Is.EqualTo(1), "a queued worker reserves its supply");
            Assert.That(hq.ProductionQueue.Count, Is.EqualTo(1));
            // The command's own step counts as the first of the 100.
            for (int step = 2; step <= 100; step++)
            {
                sim.Step();
                events.AddRange(sim.Events.Current);
                Assert.That(CountOf(sim, P0, TestWorld.WorkerId), Is.EqualTo(step < 100 ? 0 : 1), "after step " + step);
            }
            List<UnitTrainedEvent> trained = Of<UnitTrainedEvent>(events);
            Assert.That(trained.Count, Is.EqualTo(1));
            Assert.That(trained[0].Producer, Is.EqualTo(hqId));
            Assert.That(trained[0].DefinitionId, Is.EqualTo(TestWorld.WorkerId));
            // First free tile in ascending (row, column) order around tile (12, 12): tile (11, 11).
            Assert.That(Get(sim, trained[0].Unit).Position, Is.EqualTo(new FixVec2(Fix64.Parse("11.5"), Fix64.Parse("11.5"))));
            Assert.That(Get(sim, trained[0].Unit).Owner, Is.EqualTo(P0));
            Assert.That(hq.ProductionQueue.Count, Is.EqualTo(0));
            Assert.That(owner.SupplyUsed, Is.EqualTo(1));

            // Two queued: the second starts when the first finishes, 100 ticks later.
            sim.Submit(new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId));
            sim.Submit(new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId));
            StepCollect(sim, 99);
            Assert.That(CountOf(sim, P0, TestWorld.WorkerId), Is.EqualTo(1));
            Assert.That(owner.Common, Is.EqualTo(50));
            StepCollect(sim, 1);
            Assert.That(CountOf(sim, P0, TestWorld.WorkerId), Is.EqualTo(2));
            StepCollect(sim, 99);
            Assert.That(CountOf(sim, P0, TestWorld.WorkerId), Is.EqualTo(2));
            StepCollect(sim, 1);
            Assert.That(CountOf(sim, P0, TestWorld.WorkerId), Is.EqualTo(3));
        }

        /// <summary>Every train rejection reason.</summary>
        [Test]
        public void Train_Rejections()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            EntityId farHq = Spawn(sim, TestWorld.HqId, P1, 45, 20);
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), new EntityId(999), TestWorld.WorkerId), "unknown_unit");
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P1, Seq(), hqId, TestWorld.WorkerId), "not_owner");
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, "test.nope"), "unknown_definition");
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.RunnerId), "cannot_train");
            // A far, Uprooted HQ cannot train; neither can one that is switching.
            Assert.That(Get(sim, farHq).HqState, Is.EqualTo(HqState.Uprooted));
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P1, Seq(), farHq, TestWorld.WorkerId), "hq_not_rooted");
            AssertAccepted(sim, new UprootHqCommand(sim.Clock.Tick, P0, Seq(), hqId));
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId), "hq_not_rooted");
            StepCollect(sim, 100);
            AssertAccepted(sim, new RootHqCommand(sim.Clock.Tick, P0, Seq(), hqId));
            StepCollect(sim, 200);

            // Queue length: with plenty of resources the sixth entry is refused (max_queue_length 5).
            Player owner = PlayerOf(sim, P0);
            owner.AddResource(ResourceKind.Common, 1000);
            for (int i = 0; i < 5; i++)
            {
                AssertAccepted(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId));
            }
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId), "queue_full");
            Assert.That(Get(sim, hqId).ProductionQueue.Count, Is.EqualTo(5));

            // Money: with 20 common left a 50-common worker cannot be queued.
            Simulation poor = TestWorld.StartedEconomy();
            EntityId poorHq = Spawn(poor, TestWorld.HqId, P0, 12, 12);
            PlayerOf(poor, P0).Spend(180, 0);
            AssertRejected(poor, new TrainUnitCommand(poor.Clock.Tick, P0, Seq(), poorHq, TestWorld.WorkerId), "insufficient_resources");
            Assert.That(PlayerOf(poor, P0).Common, Is.EqualTo(20));

            // A structure that is still being built trains nothing.
            EntityId workerId = Spawn(poor, TestWorld.WorkerId, P0, 14, 14);
            PlayerOf(poor, P0).AddResource(ResourceKind.Common, 500);
            List<SimEvent> built = Do(poor, new BuildStructureCommand(poor.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 20, 20));
            EntityId barracks = Of<UnitSpawnedEvent>(built)[0].Unit;
            AssertRejected(poor, new TrainUnitCommand(poor.Clock.Tick, P0, Seq(), barracks, TestWorld.RunnerId), "under_construction");
        }

        /// <summary>A full supply bar blocks training until a supply building is complete; a building under construction gives no supply; construction ramps health to the maximum.</summary>
        [Test]
        public void SupplyCap_BlocksTraining_UntilBuildingCompletes()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            EntityId workerId = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            for (int i = 0; i < 9; i++)
            {
                Spawn(sim, TestWorld.RunnerId, P0, 30 + i, 40);
            }
            Player owner = PlayerOf(sim, P0);
            // 9 runners and 1 worker use all 10 base supply.
            Assert.That(owner.SupplyUsed, Is.EqualTo(10));
            Assert.That(owner.SupplyCap, Is.EqualTo(10));
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId), "supply_cap");
            Assert.That(owner.Common, Is.EqualTo(200), "a refused order costs nothing");

            // Place a barracks (provides 8 supply once complete): it starts at 10% health.
            List<SimEvent> events = Do(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 20, 20));
            Assert.That(Reasons(events), Is.Empty);
            Assert.That(owner.Common, Is.EqualTo(100));
            EntityId barracksId = Of<UnitSpawnedEvent>(events)[0].Unit;
            Unit barracks = Get(sim, barracksId);
            Assert.That(barracks.IsUnderConstruction, Is.True);
            Fix64 previousHealth = barracks.Health;
            Assert.That(previousHealth, Is.GreaterThanOrEqualTo(Fix64.FromInt(20)).And.LessThan(Fix64.FromInt(23)));
            AssertRejected(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId), "supply_cap");
            Assert.That(owner.SupplyCap, Is.EqualTo(10), "an unfinished building gives no supply");

            // Construction takes build_seconds (5 s = 100 ticks) counted from the placing step; two steps have run (the placing step and the refused order above).
            int remaining = barracks.ConstructionTicksRemaining;
            Assert.That(remaining, Is.EqualTo(98));
            for (int i = 0; i < remaining; i++)
            {
                Assert.That(barracks.IsUnderConstruction, Is.True);
                sim.Step();
                events = new List<SimEvent>(sim.Events.Current);
                Assert.That(barracks.Health, Is.GreaterThan(previousHealth));
                previousHealth = barracks.Health;
            }
            Assert.That(barracks.IsUnderConstruction, Is.False);
            Assert.That(Of<StructureCompletedEvent>(events).Count, Is.EqualTo(1));
            Assert.That(barracks.Health, Is.EqualTo(barracks.Definition.MaxHealth));
            Assert.That(owner.SupplyCap, Is.EqualTo(18));
            AssertAccepted(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, TestWorld.WorkerId));
            Assert.That(owner.SupplyUsed, Is.EqualTo(11));
        }

        /// <summary>A base holds at most 6 static defences; a seventh in the same base is refused, and a site outside any base has no base to count in.</summary>
        [Test]
        public void StaticDefenceCap_SixPerBase()
        {
            Simulation sim = TestWorld.StartedEconomy();
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            EntityId workerId = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            Assert.That(sim.Tuning.Supply.StaticDefencesPerBaseMax, Is.EqualTo(6));
            for (int x = 16; x <= 21; x++)
            {
                AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.TowerId, x, 10));
            }
            Player owner = PlayerOf(sim, P0);
            Assert.That(CountOf(sim, P0, TestWorld.TowerId), Is.EqualTo(6));
            Assert.That(owner.Common, Is.EqualTo(140));
            Assert.That(owner.SupplyUsed, Is.EqualTo(7), "the worker plus six defences at 1 supply each");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.TowerId, 22, 10), "static_defence_cap");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.TowerId, 40, 40), "no_base");
            Assert.That(CountOf(sim, P0, TestWorld.TowerId), Is.EqualTo(6));
            Assert.That(owner.Common, Is.EqualTo(140), "refused builds cost nothing");
            // Other buildings are not static defences: a barracks still fits in the same base.
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 22, 10));
        }

        /// <summary>Every build rejection reason.</summary>
        [Test]
        public void Build_Rejections_BlockedOccupiedOutOfBoundsAndMore()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId workerId = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            EntityId runner = Spawn(sim, TestWorld.RunnerId, P0, 30, 30);
            EntityId enemyWorker = Spawn(sim, TestWorld.WorkerId, P1, 40, 40);
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 20, 20));
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 20, 20), "tile_blocked");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 14, 14), "tile_occupied");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 64, 3), "out_of_bounds");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, -1, 3), "out_of_bounds");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), runner, TestWorld.BarracksId, 25, 25), "cannot_build");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, "test.nope", 25, 25), "unknown_definition");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.RunnerId, 25, 25), "not_a_structure");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P1, Seq(), workerId, TestWorld.BarracksId, 25, 25), "not_owner");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P1, Seq(), enemyWorker, TestWorld.BarracksId, 25, 25), "wrong_faction");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), new EntityId(999), TestWorld.BarracksId, 25, 25), "unknown_unit");
            // 200 start - 100 paid = 100: one more barracks fits, a third does not.
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 22, 20));
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 24, 20), "insufficient_resources");
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(0));
        }

        /// <summary>A structure's footprint blocks its tile and the flow field to it while it stands, and the block clears when it dies in combat.</summary>
        [Test]
        public void Footprint_BlocksPathing_WhileAlive_ClearsOnDeath()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId workerId = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            EntityId runner = Spawn(sim, TestWorld.RunnerId, P0, 24, 20);
            int tile = sim.Terrain.IndexOf(20, 20);
            int beside = sim.Terrain.IndexOf(21, 20);
            Assert.That(sim.Terrain[tile].IsBlocked, Is.False);
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 20, 20));
            Assert.That(sim.Terrain[tile].IsBlocked, Is.True);
            Assert.That(sim.Terrain[tile].Blockers & TileBlockers.Footprint, Is.EqualTo(TileBlockers.Footprint));
            // The blocked tile itself is unreachable, but the field leads to its standable neighbours (ADR-007 follow-up),
            // so a ground unit ordered onto the footprint walks up and stops beside it instead of failing.
            Assert.That(sim.Pathing.Get(MovementLayer.Ground, tile).IsReachable(tile), Is.False);
            Assert.That(sim.Pathing.Get(MovementLayer.Ground, tile).IsReachable(beside), Is.True);
            // Air units ignore the footprint.
            Assert.That(sim.Terrain.IsStandable(tile, MovementLayer.Air), Is.True);
            Assert.That(sim.Terrain.IsStandable(tile, MovementLayer.Ground), Is.False);

            // An enemy mortar (blast 20, x2 against the armoured barracks) one-shots the unfinished building.
            int versionBefore = sim.Terrain.Version;
            List<SimEvent> events = Do(sim, new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.MortarId, P1, At(20, 25)));
            events.AddRange(StepCollect(sim, 5));
            Assert.That(Of<UnitKilledEvent>(events).Count, Is.EqualTo(1));
            Assert.That(sim.Terrain[tile].IsBlocked, Is.False);
            Assert.That(sim.Terrain.Version, Is.GreaterThan(versionBefore));
            Assert.That(sim.Pathing.Get(MovementLayer.Ground, tile).IsReachable(beside), Is.True);

            // A second structure, out of the mortar's reach: a ground unit ordered onto its footprint walks up and
            // stops beside it (the blocked goal seeds its standable neighbours), never on it and never "unreachable".
            int farTile = sim.Terrain.IndexOf(30, 10);
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), workerId, TestWorld.BarracksId, 30, 10));
            events = Do(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), runner, new FixVec2(Fix64.Parse("30.5"), Fix64.Parse("10.5"))));
            events.AddRange(StepCollect(sim, 160));
            Assert.That(Of<MoveUnreachableEvent>(events).Count, Is.EqualTo(0));
            Assert.That(Of<UnitArrivedEvent>(events).Count, Is.EqualTo(1));
            Assert.That(sim.Units.TryGet(runner, out Unit? walker) && sim.Terrain.TileOf(walker!.Position) != farTile, Is.True, "the runner stops beside the footprint, never on it");
        }

        /// <summary>The same economy script gives the same hashes twice; changing one command changes the result; the five new command kinds parse from a script.</summary>
        [Test]
        public void EconomyScript_ParsesAndReplaysIdentically()
        {
            const string text = @"{
  ""$schema_version"": 1,
  ""seed"": 99,
  ""ticks"": 400,
  ""players"": [ { ""id"": 0, ""team"": 0, ""faction"": ""test"" }, { ""id"": 1, ""team"": 1, ""faction"": ""skai"" } ],
  ""commands"": [
    { ""tick"": 0, ""issuer"": 255, ""sequence"": 0, ""type"": ""spawn_unit"", ""definition"": ""test.hq"", ""owner"": 0, ""x"": 12, ""y"": 12 },
    { ""tick"": 0, ""issuer"": 255, ""sequence"": 1, ""type"": ""spawn_unit"", ""definition"": ""test.worker"", ""owner"": 0, ""x"": 14, ""y"": 12 },
    { ""tick"": 1, ""issuer"": 0, ""sequence"": 0, ""type"": ""gather"", ""unit"": 2, ""field"": 0 },
    { ""tick"": 1, ""issuer"": 0, ""sequence"": 1, ""type"": ""train_unit"", ""producer"": 1, ""definition"": ""test.worker"" },
    { ""tick"": 2, ""issuer"": 0, ""sequence"": 2, ""type"": ""build_structure"", ""unit"": 2, ""definition"": ""test.barracks"", ""x"": 20, ""y"": 20 },
    { ""tick"": 150, ""issuer"": 0, ""sequence"": 3, ""type"": ""uproot_hq"", ""unit"": 1 },
    { ""tick"": 300, ""issuer"": 0, ""sequence"": 4, ""type"": ""root_hq"", ""unit"": 1 }
  ]
}";
            CommandScript script = CommandScript.Parse(text, "economy_test.json");
            List<string> kinds = new List<string>();
            foreach (Command command in script.Commands)
            {
                kinds.Add(command.Kind);
            }
            Assert.That(kinds, Is.EqualTo(new List<string> { "spawn_unit", "spawn_unit", "gather", "train_unit", "build_structure", "uproot_hq", "root_hq" }));

            List<ulong> first = RunScript(script);
            List<ulong> second = RunScript(CommandScript.Parse(text, "economy_test.json"));
            Assert.That(first.Count, Is.EqualTo(400 / TestWorld.HashInterval + 1));
            Assert.That(second, Is.EqualTo(first));
            List<ulong> changed = RunScript(CommandScript.Parse(text.Replace("\"field\": 0", "\"field\": 1"), "economy_test.json"));
            Assert.That(changed[changed.Count - 1], Is.Not.EqualTo(first[first.Count - 1]));

            // Unknown fields are rejected by the parser.
            Expect.Throws<DataValidationException>(() => CommandScript.Parse(text.Replace("\"field\": 0", "\"fields\": 0"), "economy_test.json"));
        }

        /// <summary>Runs a parsed script on the economy map and returns every reported hash followed by the final hash.</summary>
        private static List<ulong> RunScript(CommandScript script)
        {
            Simulation sim = TestWorld.StartedEconomy(script.Seed);
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            // Hashes in the order they were reported.
            List<ulong> hashes = new List<ulong>();
            for (int tick = 0; tick < script.Ticks; tick++)
            {
                sim.Step();
                foreach (StateHashedEvent hashed in Of<StateHashedEvent>(new List<SimEvent>(sim.Events.Current)))
                {
                    hashes.Add(hashed.Hash);
                }
            }
            hashes.Add(sim.ComputeStateHash());
            return hashes;
        }
    }
}

// rts/tests/Sim.Tests/Economy/WallTests.cs
// Acceptance tests W-1 to W-16 of package W of rts/docs/specs/walls_and_tech.md (walls): a structure with is_wall blocks its tile with the
// BuiltWall flag, is placed and paid like any structure, stops and re-routes ground movement, keeps flood water already on it (W-5 lives in
// FloodingTests), takes combat and Quake damage, is hidden in fog for the AI view, loads from data and is hashed. Part of Rts.Sim.Tests.
// Economy setup (W-1, W-10, W-13, W-14): TestWorld's economy map and shipped tuning. Combat setup (the rest): CombatRig's 24 x 24 arena,
// separation off, shipped damage table; walls are spawned complete by script on tile centres (x + 0.5, y + 0.5).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Ai;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Cycles;
using Rts.Sim.Tests.Data;
using Rts.Sim.Tests.Statuses;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;
using CombatRig = Rts.Sim.Tests.Combat.CombatRig;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Player-built walls.</summary>
    [TestFixture]
    public sealed class WallTests
    {
        /// <summary>Wall definition of the tests: ground, 200 health, radius 0.5, cost 25, build 5 s, footprint 1, is_wall, sight 3.</summary>
        private const string WallId = "test.wall";

        /// <summary>Mover of the movement tests: ground, speed 4 (0.2 tiles per tick), radius 0.5.</summary>
        private const string MoverId = "test.mover4";

        /// <summary>Flyer of W-8: air, speed 4, radius 0.5.</summary>
        private const string Flyer4Id = "test.flyer4";

        /// <summary>Owner of the victims in the combat setup (player 2, team 2); player 1 (<c>P1</c>) is the attacker side.</summary>
        private static readonly PlayerId P2 = CombatRig.P2;

        /// <summary>Next script sequence number for commands this fixture submits itself. Unit: counter. Range: 5000 upward (above CombatRig's own).</summary>
        private uint _seq = 5000;

        /// <summary>The wall definition; armour name as the setup's damage table knows it.</summary>
        private static UnitDefinition WallDefinition(string armour, int footprint = 1)
        {
            return new UnitDefinition(WallId, "Wall", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(200), Fix64.Zero, 0, Fix64.Half, 25, 0,
                armour, Fix64.FromInt(3), null, null, new UnitEconomyDefinition(Fix64.FromInt(5), false, false, new List<string>(), false, footprint, 0, footprint == 1));
        }

        /// <summary>Economy simulation with the test wall in the catalog.</summary>
        private static Simulation EconomySim()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(WallDefinition("armoured"));
            SimulationConfig config = new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), catalog,
                TestWorld.TwoPlayers(), BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.EconomyMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>Combat catalog with the wall, the mover and the flyer.</summary>
        private static ContentCatalog ArenaCatalog()
        {
            ContentCatalog catalog = CombatRig.NewCatalog();
            catalog.AddUnit(WallDefinition("structure"));
            catalog.AddUnit(new UnitDefinition(MoverId, "Mover", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 0, 0));
            catalog.AddUnit(new UnitDefinition(Flyer4Id, "Flyer", TestWorld.TestFactionId, MovementLayer.Air, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 0, 0,
                "light", Fix64.FromInt(8), null, null, null, Fix64.FromInt(4)));
            return catalog;
        }

        /// <summary>Queues a move order for tick <paramref name="tick"/>.</summary>
        private void Move(CombatRig rig, int tick, EntityId unit, string x, string y)
        {
            rig.Sim.Submit(new MoveUnitCommand(tick, PlayerId.None, _seq++, unit, new FixVec2(CombatRig.F(x), CombatRig.F(y))));
        }

        /// <summary>Queues a wall spawn on tile (x, y) for tick <paramref name="tick"/>.</summary>
        private void SpawnWall(CombatRig rig, int tick, int x, int y, PlayerId owner)
        {
            rig.Sim.Submit(new SpawnUnitCommand(tick, PlayerId.None, _seq++, WallId, owner, new FixVec2(CombatRig.F(x + ".5"), CombatRig.F(y + ".5"))));
        }

        /// <summary>Queues walls on the whole column x = 10 except the rows in <paramref name="gaps"/>, for tick <paramref name="tick"/>; returns nothing, IDs follow spawn order.</summary>
        private void Column(CombatRig rig, int tick, params int[] gaps)
        {
            for (int y = 0; y < 24; y++)
            {
                if (!gaps.Contains(y))
                {
                    SpawnWall(rig, tick, 10, y, P2);
                }
            }
        }

        /// <summary>Asserts a position coordinate is within 0.001 tiles of a decimal (the 0.2 tiles per tick step is not exact in fixed point).</summary>
        private static void Near(Fix64 actual, string expected, string message = "")
        {
            Assert.That((actual - CombatRig.F(expected)).Abs() <= CombatRig.F("0.001"), Is.True, message + " expected " + expected + " but was " + actual);
        }

        /// <summary>Events of a type on one tick of a rig's log.</summary>
        private static List<T> On<T>(CombatRig rig, int tick) where T : SimEvent
        {
            return rig.Of<T>().FindAll(e => e.Tick == tick);
        }

        // ------------------------------------------------------------------ W-1

        /// <summary>W-1: a worker places a wall on t0 + 62 with the BuiltWall flag, health 20, and it completes on t0 + 162.</summary>
        [Test]
        public void W1_PlacedByAWorker()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            int t0 = sim.Clock.Tick;
            int supply = PlayerOf(sim, P0).SupplyUsed;
            AssertAccepted(sim, new BuildStructureCommand(t0, P0, Seq(), worker, WallId, 20, 14));
            List<SimEvent> all = new List<SimEvent> { };
            all.AddRange(StepCollect(sim, 170));
            Assert.That(Of<BuildOrderStartedEvent>(all).Single().Tick, Is.EqualTo(t0 + 22));
            StructurePlacedEvent placed = Of<StructurePlacedEvent>(all).Single();
            Assert.That(placed.Tick, Is.EqualTo(t0 + 62));
            Assert.That(Of<StructureCompletedEvent>(all).Single().Tick, Is.EqualTo(t0 + 162));
            int index = sim.Terrain.IndexOf(20, 14);
            Assert.That(Of<TerrainChangedEvent>(all).Any(e => e.Tick == t0 + 62 && e.TileIndex == index), Is.True);
            Assert.That(sim.Terrain.At(20, 14).Blockers, Is.EqualTo(TileBlockers.BuiltWall));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(175));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(supply));
        }

        // ------------------------------------------------------------------ W-2 to W-4: movement

        /// <summary>W-2: walls closing the arena on tick 10 stop a walking unit at once: x stays 7.5, the move order ends with move_unreachable.</summary>
        [Test]
        public void W2_PlacedMidWalk_Stops()
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            EntityId mover = rig.Add(MoverId, P1, "5.5", "10.5");
            Move(rig, 0, mover, "15.5", "10.5");
            Column(rig, 10);
            rig.Step(10);
            Near(rig.Get(mover).Position.X, "7.5", "after tick 9");
            rig.Step(1);
            Assert.That(On<MoveUnreachableEvent>(rig, 10).Count(e => e.Unit == mover), Is.EqualTo(1));
            Near(rig.Get(mover).Position.X, "7.5", "after tick 10");
            Assert.That(rig.Get(mover).HasMoveTarget, Is.False);
        }

        /// <summary>W-3: with a gap at (10, 20) the unit re-routes through it and arrives, never standing on a wall tile.</summary>
        [Test]
        public void W3_Gap_Reroutes()
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            EntityId mover = rig.Add(MoverId, P1, "5.5", "10.5");
            Column(rig, 0, 20);
            Move(rig, 0, mover, "15.5", "10.5");
            bool throughGap = false;
            for (int tick = 0; tick < 300; tick++)
            {
                rig.Step();
                int tile = rig.Sim.Terrain.TileOf(rig.Get(mover).Position);
                Assert.That(rig.Sim.Terrain[tile].Blockers, Is.EqualTo(TileBlockers.None), "tick " + tick);
                throughGap |= tile == rig.Sim.Terrain.IndexOf(10, 20);
            }
            Assert.That(throughGap, Is.True);
            Assert.That(rig.Of<UnitArrivedEvent>().Any(e => e.Unit == mover && e.Tick < 300), Is.True);
        }

        /// <summary>W-4: killing the wall at (10, 10) on tick 20 opens the tile (TerrainChangedEvent, no blocker), the unit does not resume, a new order on 21 walks through and arrives.</summary>
        [Test]
        public void W4_WallDies_Opens()
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            EntityId mover = rig.Add(MoverId, P1, "5.5", "10.5");
            Move(rig, 0, mover, "15.5", "10.5");
            Column(rig, 10);
            // Walls are IDs 2 to 25 in row order, so row 10 is ID 12.
            rig.Remove(20, new EntityId(12));
            Move(rig, 21, mover, "15.5", "10.5");
            rig.Step(20);
            rig.Step(1);
            Assert.That(On<TerrainChangedEvent>(rig, 20).Any(e => e.TileIndex == 250), Is.True);
            Assert.That(rig.Sim.Terrain.At(10, 10).Blockers, Is.EqualTo(TileBlockers.None));
            Near(rig.Get(mover).Position.X, "7.5", "no auto-resume");
            rig.Step(15);
            Near(rig.Get(mover).Position.X, "10.5", "after tick 35");
            rig.Step(60);
            Assert.That(rig.Of<UnitArrivedEvent>().Any(e => e.Unit == mover && e.Tick < 70), Is.True);
        }

        /// <summary>W-4 combat variant: a wall killed by an enemy shooter loses its blocker in 6d of the death tick.</summary>
        [Test]
        public void W4_KilledInCombat_BlockerGoesOnTheDeathTick()
        {
            ContentCatalog catalog = ArenaCatalog();
            CombatRig.AddUnit(catalog, "t.gun", MovementLayer.Ground, 100, "light", new WeaponDefinition(Fix64.FromInt(500), "kinetic", Fix64.FromInt(7), Fix64.FromInt(10), true, true, false));
            CombatRig rig = new CombatRig(catalog);
            EntityId gun = rig.Add("t.gun", P1, "2.5", "10.5");
            EntityId wall = rig.Add(WallId, P2, "9.5", "10.5");
            rig.Attack(gun, wall);
            rig.Step(1);
            Assert.That(rig.Of<UnitRemovedEvent>().Any(e => e.Unit == wall && e.Tick == 0), Is.True, "500 x 0.5 kills 200 health at once");
            Assert.That(rig.Sim.Terrain.At(9, 10).Blockers, Is.EqualTo(TileBlockers.None));
            Assert.That(rig.Of<TerrainChangedEvent>().Any(e => e.Tick == 0 && e.TileIndex == rig.Sim.Terrain.IndexOf(9, 10) && e.Tile.Blockers == TileBlockers.None), Is.True);
        }

        // ------------------------------------------------------------------ W-6, W-7: combat

        /// <summary>W-6: a blast against a wall uses the structure row and the structure multiplier (200 - 12 x 1.25 x 1.5 = 177.5); a kinetic hit uses 0.5 (196).</summary>
        [Test]
        public void W6_ArmourAndStructureMultiplier()
        {
            ContentCatalog catalog = ArenaCatalog();
            CombatRig.AddUnit(catalog, "t.blast", MovementLayer.Ground, 100, "light", new WeaponDefinition(Fix64.FromInt(12), "blast", Fix64.FromInt(7), Fix64.FromInt(10), true, true, false, false, 0, default, CombatRig.F("1.5")));
            CombatRig.AddUnit(catalog, "t.kin", MovementLayer.Ground, 100, "light", new WeaponDefinition(Fix64.FromInt(8), "kinetic", Fix64.FromInt(7), Fix64.FromInt(10), true, true, false));
            CombatRig rig = new CombatRig(catalog);
            EntityId blast = rig.Add("t.blast", P1, "2.5", "10.5");
            EntityId kin = rig.Add("t.kin", P1, "2.5", "12.5");
            EntityId w1 = rig.Add(WallId, P2, "9.5", "10.5");
            EntityId w2 = rig.Add(WallId, P2, "9.5", "12.5");
            rig.Attack(blast, w1);
            rig.Attack(kin, w2);
            rig.Step(1);
            Assert.That(rig.Health(w1), Is.EqualTo(CombatRig.F("177.5")));
            Assert.That(rig.Health(w2), Is.EqualTo(Fix64.FromInt(196)));
        }

        /// <summary>W-7: splash hits an enemy wall (188.75) but not the attacker's own (200); one victim.</summary>
        [Test]
        public void W7_SplashHitsEnemyWallsOnly()
        {
            ContentCatalog catalog = ArenaCatalog();
            CombatRig.AddUnit(catalog, "t.blast", MovementLayer.Ground, 100, "light", new WeaponDefinition(Fix64.FromInt(12), "blast", Fix64.FromInt(7), Fix64.FromInt(10), true, true, false, false, 0, CombatRig.F("1"), CombatRig.F("1.5")));
            CombatRig.AddUnit(catalog, "t.light", MovementLayer.Ground, 100, "light");
            CombatRig rig = new CombatRig(catalog);
            EntityId a = rig.Add("t.blast", P1, "2.5", "10.5");
            EntityId t = rig.Add("t.light", P2, "8.5", "10.5");
            EntityId enemyWall = rig.Add(WallId, P2, "9.5", "10.5");
            EntityId ownWall = rig.Add(WallId, P1, "8.5", "11.5");
            rig.Attack(a, t);
            rig.Step(1);
            Assert.That(rig.Health(t), Is.EqualTo(Fix64.FromInt(91)));
            Assert.That(rig.Health(enemyWall), Is.EqualTo(CombatRig.F("188.75")));
            Assert.That(rig.Health(ownWall), Is.EqualTo(Fix64.FromInt(200)));
            Assert.That(rig.Of<BlastEvent>().Single().Victims, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ W-8, W-9, W-11

        /// <summary>W-8: a flyer passes over the column (x 10.5 after tick 24); grounded on tick 24 over a wall tile it lands at (9.5, 9.5) and its order ends with move_unreachable.</summary>
        [Test]
        public void W8_FlyersPass_GroundedFlyersLandBeside()
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            EntityId flyer = rig.Add(Flyer4Id, P1, "5.5", "10.5");
            Column(rig, 0);
            Move(rig, 0, flyer, "15.5", "10.5");
            rig.Step(25);
            Near(rig.Get(flyer).Position.X, "10.5", "after tick 24");

            CombatRig grounded = new CombatRig(ArenaCatalog());
            EntityId second = grounded.Add(Flyer4Id, P1, "5.5", "10.5");
            Column(grounded, 0);
            Move(grounded, 0, second, "15.5", "10.5");
            grounded.Sim.Submit(new ApplyStatusCommand(24, PlayerId.None, _seq++, second, StatusKind.Grounded, Fix64.One, Fix64.Zero, "script"));
            grounded.Step(25);
            Near(grounded.Get(second).Position.X, "9.5", "landed x");
            Near(grounded.Get(second).Position.Y, "9.5", "landed y");
            Assert.That(On<MoveUnreachableEvent>(grounded, 24).Any(e => e.Unit == second), Is.True);
        }

        /// <summary>W-9: walls do not block sight; tile (12, 10) behind the column is visible to the mover's team.</summary>
        [Test]
        public void W9_VisionNotBlocked()
        {
            ContentCatalog catalog = ArenaCatalog();
            CombatRig.AddUnit(catalog, "t.watcher", MovementLayer.Ground, 100, "light");
            CombatRig rig = new CombatRig(catalog);
            rig.Add("t.watcher", P1, "5.5", "10.5");
            Column(rig, 0);
            rig.Step(2);
            Assert.That(rig.Sim.Vision.IsTileVisible(rig.Sim.Players.TryGet(P1, out Player? p) ? p!.Team : TeamId.None, 12, 10), Is.True);
        }

        /// <summary>W-11: walls on all eight neighbours are accepted and the enclosed unit's move ends at once with move_unreachable.</summary>
        [Test]
        public void W11_SealingAllowed()
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            EntityId mover = rig.Add(MoverId, P2, "5.5", "5.5");
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        SpawnWall(rig, 0, 5 + dx, 5 + dy, P1);
                    }
                }
            }
            Move(rig, 1, mover, "15.5", "5.5");
            rig.Step(2);
            Assert.That(rig.Of<CommandRejectedEvent>(), Is.Empty);
            Assert.That(On<MoveUnreachableEvent>(rig, 1).Any(e => e.Unit == mover), Is.True);
        }

        // ------------------------------------------------------------------ W-10

        /// <summary>W-10: placement rules: occupied and blocked tiles are rejected, a wall needs no base and ignores the defence cap, and water under it stays.</summary>
        [Test]
        public void W10_PlacementRules()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            Spawn(sim, TestWorld.TankId, P1, 20, 14);
            Spawn(sim, WallId, P1, 18, 14);
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, WallId, 20, 14), "tile_occupied");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, WallId, 18, 14), "tile_blocked");
            // No base at all: a wall is accepted, a tower is not.
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, TestWorld.TowerId, 16, 14), "no_base");
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, WallId, 16, 14));

            Simulation based = EconomySim();
            Spawn(based, TestWorld.HqId, P0, 12, 12);
            for (int x = 16; x <= 21; x++)
            {
                Spawn(based, TestWorld.TowerId, P0, x, 10);
            }
            EntityId builder = Spawn(based, TestWorld.WorkerId, P0, 14, 14);
            AssertAccepted(based, new BuildStructureCommand(based.Clock.Tick, P0, Seq(), builder, WallId, 22, 14));
            EntityId second = Spawn(based, TestWorld.WorkerId, P0, 15, 15);
            AssertRejected(based, new BuildStructureCommand(based.Clock.Tick, P0, Seq(), second, TestWorld.TowerId, 22, 10), "static_defence_cap");

            // Water on the tile stays after placement.
            Simulation wet = EconomySim();
            EntityId swimmer = Spawn(wet, TestWorld.WorkerId, P0, 14, 14);
            StatusTestWorld.Run(wet, wet.Clock.Tick, 7000, s => s.Terrain.SetWater(s, s.Terrain.IndexOf(18, 14), Fix64.One, new TeamId(0)));
            wet.Step();
            AssertAccepted(wet, new BuildStructureCommand(wet.Clock.Tick, P0, Seq(), swimmer, WallId, 18, 14));
            StepCollect(wet, 80);
            Assert.That(wet.Terrain.At(18, 14).Blockers, Is.EqualTo(TileBlockers.BuiltWall));
            Assert.That(wet.Terrain.At(18, 14).WaterDepth, Is.EqualTo(Fix64.One));
        }

        // ------------------------------------------------------------------ W-12

        /// <summary>W-12: a Quake damages a wall like a defence: 200 x 0.004 each second from tick 4800 down to 25 percent (50).</summary>
        [Test]
        public void W12_QuakeDamagesWalls()
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles());
            rig.ScriptPick(0, "quake");
            EntityId wall = rig.Spawn(0, CycleWorld.BuiltWallId, CycleWorld.P0, "5.5", "5.5");
            rig.RunThrough(9500);
            List<UnitDamagedEvent> hits = rig.Of<UnitDamagedEvent>().FindAll(e => e.Target == wall);
            Fix64 step = Fix64.FromInt(200) * Fix64.Parse("0.004");
            Assert.That(hits.Count, Is.EqualTo(188));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(hits[i].Tick, Is.EqualTo(4800 + 20 * i));
                Assert.That(hits[i].Damage, Is.EqualTo(step));
                Assert.That(hits[i].Attacker.IsValid, Is.False);
            }
            Assert.That(rig.UnitOf(wall).Health, Is.EqualTo(Fix64.FromInt(50)));
        }

        // ------------------------------------------------------------------ W-13

        /// <summary>W-13: a wall and a tower on one tile keep their own flags: removing the tower leaves BuiltWall, removing the wall clears the tile.</summary>
        [Test]
        public void W13_BlockerKindOnRemoval()
        {
            Simulation sim = EconomySim();
            EntityId wall = Spawn(sim, WallId, P0, 30, 30);
            EntityId tower = Spawn(sim, TestWorld.TowerId, P0, 30, 30);
            Assert.That(sim.Terrain.At(30, 30).Blockers, Is.EqualTo(TileBlockers.BuiltWall | TileBlockers.Footprint));
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), tower));
            Assert.That(sim.Terrain.At(30, 30).Blockers, Is.EqualTo(TileBlockers.BuiltWall));
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), wall));
            Assert.That(sim.Terrain.At(30, 30).Blockers, Is.EqualTo(TileBlockers.None));
        }

        // ------------------------------------------------------------------ W-14

        /// <summary>W-14: the AI view shows an enemy wall only where the team sees the tile, and a map Wall flag everywhere.</summary>
        [Test]
        public void W14_AiViewHidesWallsInFog()
        {
            Simulation sim = EconomySim();
            Spawn(sim, WallId, P1, 50, 40);
            StatusTestWorld.Run(sim, sim.Clock.Tick, 7001, s => s.Terrain.AddBlockers(s, s.Terrain.IndexOf(52, 40), TileBlockers.Wall));
            sim.Step();
            Assert.That(AiWorldView.Build(sim, P0).IsBlocked(50, 40), Is.False, "enemy wall in fog");
            Assert.That(AiWorldView.Build(sim, P0).IsBlocked(52, 40), Is.True, "map wall known everywhere");
            Spawn(sim, TestWorld.RunnerId, P0, 48, 40);
            Assert.That(AiWorldView.Build(sim, P0).IsBlocked(50, 40), Is.True, "enemy wall in sight");
        }

        // ------------------------------------------------------------------ W-15

        /// <summary>W-15: is_wall with footprint 0 is rejected naming the file and is_wall; exactly the two shipped walls carry it, with the spec values.</summary>
        [Test]
        public void W15_Loader()
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() =>
                DataLoader.Load(TestData.WithEdit("units/skai/wind_baffle.json", "\"footprint\": 1", "\"footprint\": 0")));
            Assert.That(ex.Message, Does.Contain("wind_baffle.json"));
            Assert.That(ex.Message, Does.Contain("is_wall"));
            DataSet data = TestData.LoadAll();
            List<string> walls = data.Content.Units.Values.Where(u => u.Economy.IsWall).Select(u => u.Id).ToList();
            Assert.That(walls, Is.EqualTo(new List<string> { "skai.wind_baffle", "water.coral_wall" }));
            foreach (string id in walls)
            {
                UnitDefinition wall = data.Content.Units[id];
                Assert.That(wall.MaxHealth, Is.EqualTo(Fix64.FromInt(200)));
                Assert.That(wall.CostCommon, Is.EqualTo(25));
                Assert.That(wall.Economy.BuildSeconds, Is.EqualTo(Fix64.FromInt(5)));
                Assert.That(wall.Economy.FootprintTiles, Is.EqualTo(1));
                Assert.That(wall.ArmourType, Is.EqualTo("structure"));
                Assert.That(wall.Supply, Is.EqualTo(0));
            }
        }

        // ------------------------------------------------------------------ W-16

        /// <summary>The state hashes of W-4's scene with the wall on tile (10, <paramref name="row"/>), one per hash tick.</summary>
        private static List<ulong> Hashes(int row)
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            rig.Add(WallId, P2, "10.5", row + ".5");
            rig.Step(30);
            return rig.Of<StateHashedEvent>().Select(e => e.Hash).ToList();
        }

        /// <summary>W-16: two identical runs hash equal on every tick, a wall one tile over differs from the spawn tick, and a wall tile hashes Blockers 4.</summary>
        [Test]
        public void W16_Determinism()
        {
            List<ulong> a = Hashes(10);
            Assert.That(Hashes(10), Is.EqualTo(a));
            List<ulong> b = Hashes(11);
            Assert.That(b[0], Is.Not.EqualTo(a[0]));
            TerrainTile tile = new TerrainTile(0, false, Fix64.Zero, TeamId.None, SurfaceState.Dry, TileBlockers.BuiltWall);
            StateHasher one = new StateHasher();
            tile.WriteHash(one);
            StateHasher two = new StateHasher();
            new TerrainTile(0, false, Fix64.Zero, TeamId.None, SurfaceState.Dry, (TileBlockers)4).WriteHash(two);
            Assert.That(one.Hash, Is.EqualTo(two.Hash));
            Assert.That((byte)TileBlockers.BuiltWall, Is.EqualTo(4));
        }
    }
}

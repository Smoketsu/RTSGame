// rts/tests/Sim.Tests/Economy/GateTests.cs
// Acceptance tests G-1 to G-16 of package G of rts/docs/specs/walls_and_tech.md (gates): a structure with is_gate blocks its tile with the Gate
// flag and a GateTeam, units of that team (allies included) path through it while everyone else treats it as a wall, the flow-field cache keys
// by team only while a gate stands, and the tile hash covers GateTeam. G-10 and G-11 (flood and wave) live in FloodingTests. Part of Rts.Sim.Tests.
// Economy setup (G-1, G-8, G-9, G-14): TestWorld's economy map. Combat setup (the rest): CombatRig's 24 x 24 arena, separation off.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Pathing;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Cycles;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;
using CombatRig = Rts.Sim.Tests.Combat.CombatRig;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Player-built gates.</summary>
    [TestFixture]
    public sealed class GateTests
    {
        /// <summary>Wall definition of the tests (as WallTests: 200 health, cost 25, build 5 s).</summary>
        private const string WallId = "test.wall";

        /// <summary>Gate definition of the tests: the wall with cost 50, build 10 s (200 ticks) and is_gate.</summary>
        private const string GateId = "test.gate";

        /// <summary>Mover: ground, speed 4 (0.2 tiles per tick), radius 0.5.</summary>
        private const string MoverId = "test.mover4";

        /// <summary>Flyer: air, speed 4, radius 0.5.</summary>
        private const string Flyer4Id = "test.flyer4";

        /// <summary>Owner of the gate in the combat setup (team 1).</summary>
        private static readonly PlayerId CombatP1 = CombatRig.P1;

        /// <summary>Enemy of the gate owner (team 2).</summary>
        private static readonly PlayerId CombatP2 = CombatRig.P2;

        /// <summary>Next script sequence number for commands this fixture submits itself. Unit: counter. Range: 6000 upward (above CombatRig's own and WallTests').</summary>
        private uint _seq = 6000;

        /// <summary>A gate or wall definition: 200 health, radius 0.5, footprint 1, is_wall; gate when <paramref name="gate"/>.</summary>
        private static UnitDefinition Structure(string id, bool gate, string armour)
        {
            return new UnitDefinition(id, id, TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(200), Fix64.Zero, 0, Fix64.Half, gate ? 50 : 25, 0,
                armour, Fix64.FromInt(3), null, null, new UnitEconomyDefinition(Fix64.FromInt(gate ? 10 : 5), false, false, new List<string>(), false, 1, 0, true, null, gate));
        }

        /// <summary>Economy simulation with the test gate and wall in the catalog.</summary>
        private static Simulation EconomySim()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(Structure(WallId, false, "armoured"));
            catalog.AddUnit(Structure(GateId, true, "armoured"));
            SimulationConfig config = new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), catalog,
                TestWorld.TwoPlayers(), BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.EconomyMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>Combat catalog with the wall, the gate, the mover and the flyer.</summary>
        private static ContentCatalog ArenaCatalog()
        {
            ContentCatalog catalog = CombatRig.NewCatalog();
            catalog.AddUnit(Structure(WallId, false, "structure"));
            catalog.AddUnit(Structure(GateId, true, "structure"));
            catalog.AddUnit(new UnitDefinition(MoverId, "Mover", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 0, 0));
            catalog.AddUnit(new UnitDefinition(Flyer4Id, "Flyer", TestWorld.TestFactionId, MovementLayer.Air, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 0, 0,
                "light", Fix64.FromInt(8), null, null, null, Fix64.FromInt(4)));
            return catalog;
        }

        /// <summary>The gated column of the spec: P1 walls on (10, 0) to (10, 23) except row 10, and a P1 gate on (10, 10), all spawned on tick 0. Returns the gate's ID.</summary>
        private static EntityId GatedColumn(CombatRig rig)
        {
            for (int y = 0; y < 24; y++)
            {
                if (y != 10)
                {
                    rig.Add(WallId, CombatP1, "10.5", y + ".5");
                }
            }
            return rig.Add(GateId, CombatP1, "10.5", "10.5");
        }

        /// <summary>Queues a move order for tick <paramref name="tick"/>.</summary>
        private void Move(CombatRig rig, int tick, EntityId unit, string x, string y)
        {
            rig.Sim.Submit(new MoveUnitCommand(tick, PlayerId.None, _seq++, unit, new FixVec2(CombatRig.F(x), CombatRig.F(y))));
        }

        /// <summary>Asserts a position coordinate is within 0.001 tiles of a decimal.</summary>
        private static void Near(Fix64 actual, string expected, string message = "")
        {
            Assert.That((actual - CombatRig.F(expected)).Abs() <= CombatRig.F("0.001"), Is.True, message + " expected " + expected + " but was " + actual);
        }

        /// <summary>Events of a type on one tick of a rig's log.</summary>
        private static List<T> On<T>(CombatRig rig, int tick) where T : SimEvent
        {
            return rig.Of<T>().FindAll(e => e.Tick == tick);
        }

        /// <summary>Builds the gated column with a mover of <paramref name="owner"/> at (5.5, 10.5) ordered to (<paramref name="goalX"/>, 10.5) on tick 0.</summary>
        private (CombatRig Rig, EntityId Mover) CrossingRig(PlayerId owner, bool ally = false, string goalX = "15.5")
        {
            CombatRig rig = new CombatRig(ArenaCatalog(), allyOfP1: ally);
            EntityId mover = rig.Add(MoverId, owner, "5.5", "10.5");
            GatedColumn(rig);
            Move(rig, 0, mover, goalX, "10.5");
            return (rig, mover);
        }

        /// <summary>Runs a crossing: on tile (10, 10) with x 10.1 after tick 22, x 10.5 after tick 24, arrival on tick 48, never on a wall tile.</summary>
        private void AssertCrosses(PlayerId owner, bool ally)
        {
            (CombatRig rig, EntityId mover) = CrossingRig(owner, ally);
            int gate = rig.Sim.Terrain.IndexOf(10, 10);
            for (int tick = 0; tick < 60; tick++)
            {
                rig.Step();
                int tile = rig.Sim.Terrain.TileOf(rig.Get(mover).Position);
                Assert.That((rig.Sim.Terrain[tile].Blockers & TileBlockers.BuiltWall) != 0, Is.False, "tick " + tick);
                if (tick == 22)
                {
                    Assert.That(tile, Is.EqualTo(gate));
                    Near(rig.Get(mover).Position.X, "10.1", "after tick 22");
                }
                if (tick == 24)
                {
                    Near(rig.Get(mover).Position.X, "10.5", "after tick 24");
                }
            }
            Assert.That(rig.Of<UnitArrivedEvent>().Single(e => e.Unit == mover).Tick, Is.EqualTo(48));
        }

        // ------------------------------------------------------------------ G-1

        /// <summary>G-1: a worker places a gate on t0 + 62 with Blockers == Gate and GateTeam = the owner's team, health 20, and it completes on t0 + 262; cost 50.</summary>
        [Test]
        public void G1_PlacedByAWorker()
        {
            Simulation sim = EconomySim();
            EntityId worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            int t0 = sim.Clock.Tick;
            AssertAccepted(sim, new BuildStructureCommand(t0, P0, Seq(), worker, GateId, 20, 14));
            List<SimEvent> all = StepCollect(sim, 270);
            Assert.That(Of<BuildOrderStartedEvent>(all).Single().Tick, Is.EqualTo(t0 + 22));
            StructurePlacedEvent placed = Of<StructurePlacedEvent>(all).Single();
            Assert.That(placed.Tick, Is.EqualTo(t0 + 62));
            Assert.That(Of<StructureCompletedEvent>(all).Single().Tick, Is.EqualTo(t0 + 262));
            Assert.That(Of<TerrainChangedEvent>(all).Any(e => e.Tick == t0 + 62 && e.TileIndex == sim.Terrain.IndexOf(20, 14) && e.Tile.Blockers == TileBlockers.Gate), Is.True);
            Assert.That(sim.Terrain.At(20, 14).Blockers, Is.EqualTo(TileBlockers.Gate));
            Assert.That(sim.Terrain.At(20, 14).GateTeam, Is.EqualTo(sim.Players.TeamOf(P0)));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(150));
        }

        // ------------------------------------------------------------------ G-2 to G-5, G-7

        /// <summary>G-2: the owner's mover crosses the gated column through the gate (on tile (10, 10) after tick 22, arrives on tick 48).</summary>
        [Test]
        public void G2_OwnTeamPasses()
        {
            AssertCrosses(CombatP1, false);
        }

        /// <summary>G-3: an enemy mover gets move_unreachable on tick 0 and stays at x 5.5.</summary>
        [Test]
        public void G3_EnemyBlocked()
        {
            (CombatRig rig, EntityId mover) = CrossingRig(CombatP2);
            rig.Step(5);
            Assert.That(On<MoveUnreachableEvent>(rig, 0).Any(e => e.Unit == mover), Is.True);
            Near(rig.Get(mover).Position.X, "5.5");
        }

        /// <summary>G-4: an ally of the owner (same team) passes like the owner.</summary>
        [Test]
        public void G4_AllyPasses()
        {
            AssertCrosses(CombatRig.P3, true);
        }

        /// <summary>G-5: the owner's mover ordered to the gate tile arrives standing on it (tick 23, x 10.3); an enemy stops beside it (tick 18, x 9.1, tile (9, 10)).</summary>
        [Test]
        public void G5_GateAsAGoal()
        {
            (CombatRig own, EntityId ownMover) = CrossingRig(CombatP1, false, "10.5");
            own.Step(40);
            UnitArrivedEvent arrived = own.Of<UnitArrivedEvent>().Single(e => e.Unit == ownMover);
            Assert.That(arrived.Tick, Is.EqualTo(23));
            Near(arrived.Position.X, "10.3");
            Assert.That(own.Sim.Terrain.TileOf(arrived.Position), Is.EqualTo(own.Sim.Terrain.IndexOf(10, 10)));
            (CombatRig foe, EntityId foeMover) = CrossingRig(CombatP2, false, "10.5");
            foe.Step(40);
            UnitArrivedEvent stopped = foe.Of<UnitArrivedEvent>().Single(e => e.Unit == foeMover);
            Assert.That(stopped.Tick, Is.EqualTo(18));
            Near(stopped.Position.X, "9.1");
            Assert.That(foe.Sim.Terrain.TileOf(stopped.Position), Is.EqualTo(foe.Sim.Terrain.IndexOf(9, 10)));
        }

        /// <summary>G-7: removing the gate on tick 20 clears the tile (no blocker, GateTeam None) and a new enemy order on tick 21 passes and arrives before tick 80.</summary>
        [Test]
        public void G7_GateDies()
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            EntityId mover = rig.Add(MoverId, CombatP2, "5.5", "10.5");
            EntityId gate = GatedColumn(rig);
            Move(rig, 0, mover, "15.5", "10.5");
            rig.Remove(20, gate);
            Move(rig, 21, mover, "15.5", "10.5");
            rig.Step(21);
            Assert.That(rig.Sim.Terrain.At(10, 10).Blockers, Is.EqualTo(TileBlockers.None));
            Assert.That(rig.Sim.Terrain.At(10, 10).GateTeam, Is.EqualTo(TeamId.None));
            rig.Step(59);
            Assert.That(rig.Of<UnitArrivedEvent>().Any(e => e.Unit == mover && e.Tick < 80), Is.True);
        }

        // ------------------------------------------------------------------ G-6

        /// <summary>Flow fields built when a P1 and a P2 mover are ordered to one goal on tick 0, with a P1 gate at (20, 20) when asked.</summary>
        private int BuildsForTwoTeams(bool withGate)
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            EntityId a = rig.Add(MoverId, CombatP1, "3.5", "3.5");
            EntityId b = rig.Add(MoverId, CombatP2, "3.5", "5.5");
            if (withGate)
            {
                rig.Add(GateId, CombatP1, "20.5", "20.5");
            }
            Move(rig, 0, a, "15.5", "15.5");
            Move(rig, 0, b, "15.5", "15.5");
            long before = rig.Sim.Pathing.BuildCount;
            rig.Step();
            return (int)(rig.Sim.Pathing.BuildCount - before);
        }

        /// <summary>G-6: with no gate two teams share one field (1 build); with a gate anywhere each team has its own (2 builds).</summary>
        [Test]
        public void G6_CacheCost()
        {
            Assert.That(BuildsForTwoTeams(false), Is.EqualTo(1));
            Assert.That(BuildsForTwoTeams(true), Is.EqualTo(2));
        }

        /// <summary>The cache-key rule: with no gate on the map a team argument is ignored, so every team gets the very same field object as the team-less call.</summary>
        [Test]
        public void G6_NoGate_PathingIsTheTeamlessField()
        {
            Simulation sim = EconomySim();
            int goal = sim.Terrain.IndexOf(30, 30);
            FlowField none = sim.Pathing.Get(MovementLayer.Ground, goal);
            Assert.That(sim.Pathing.Get(MovementLayer.Ground, goal, new TeamId(0)), Is.SameAs(none));
            Assert.That(sim.Pathing.Get(MovementLayer.Ground, goal, new TeamId(1)), Is.SameAs(none));
            Assert.That(none.PassTeam, Is.EqualTo(TeamId.None));
            Assert.That(sim.Terrain.GateTileCount, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ G-8

        /// <summary>G-8: stacked gates keep the first team; removing the first hands the tile to the second; removing both clears it; a wall under a gate keeps BuiltWall.</summary>
        [Test]
        public void G8_StackedGates()
        {
            Simulation sim = EconomySim();
            TeamId team0 = sim.Players.TeamOf(P0);
            TeamId team1 = sim.Players.TeamOf(P1);
            EntityId first = Spawn(sim, GateId, P0, 30, 30);
            EntityId second = Spawn(sim, GateId, P1, 30, 30);
            Assert.That(sim.Terrain.At(30, 30).GateTeam, Is.EqualTo(team0));
            Assert.That(sim.Terrain.GateTileCount, Is.EqualTo(1));
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), first));
            Assert.That(sim.Terrain.At(30, 30).GateTeam, Is.EqualTo(team1));
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), second));
            Assert.That(sim.Terrain.At(30, 30).Blockers, Is.EqualTo(TileBlockers.None));
            Assert.That(sim.Terrain.At(30, 30).GateTeam, Is.EqualTo(TeamId.None));
            Assert.That(sim.Terrain.GateTileCount, Is.EqualTo(0));
            Spawn(sim, WallId, P0, 32, 30);
            EntityId gate = Spawn(sim, GateId, P0, 32, 30);
            Assert.That(sim.Terrain.At(32, 30).Blockers, Is.EqualTo(TileBlockers.BuiltWall | TileBlockers.Gate));
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), gate));
            Assert.That(sim.Terrain.At(32, 30).Blockers, Is.EqualTo(TileBlockers.BuiltWall));
            Assert.That(sim.Terrain.At(32, 30).GateTeam, Is.EqualTo(TeamId.None));
        }

        // ------------------------------------------------------------------ G-9

        /// <summary>G-9: the corner rule by team: the diagonal (9, 10) to (10, 11) past a P0 gate at (10, 10) is passable for team 0 only, and the gate tile is standable for team 0 and air only.</summary>
        [Test]
        public void G9_CornerRuleByTeam()
        {
            Simulation sim = EconomySim();
            Spawn(sim, GateId, P0, 10, 10);
            TerrainGrid grid = sim.Terrain;
            TeamId own = sim.Players.TeamOf(P0);
            TeamId other = sim.Players.TeamOf(P1);
            int from = grid.IndexOf(9, 10);
            int to = grid.IndexOf(10, 11);
            Assert.That(grid.IsStepPassable(from, to, MovementLayer.Ground, own), Is.True);
            Assert.That(grid.IsStepPassable(from, to, MovementLayer.Ground, other), Is.False);
            Assert.That(grid.IsStepPassable(from, to, MovementLayer.Ground, TeamId.None), Is.False);
            Assert.That(grid.IsStepPassable(from, to, MovementLayer.Ground), Is.False);
            int gate = grid.IndexOf(10, 10);
            Assert.That(grid.IsStandable(gate, MovementLayer.Ground, own), Is.True);
            Assert.That(grid.IsStandable(gate, MovementLayer.Ground, other), Is.False);
            Assert.That(grid.IsStandable(gate, MovementLayer.Air, own), Is.True);
            Assert.That(grid.IsStandable(gate, MovementLayer.Air, other), Is.True);
            Assert.That(grid.IsStandable(gate, MovementLayer.Ground), Is.False);
            Assert.That(grid[gate].IsBlocked, Is.True);
        }

        // ------------------------------------------------------------------ G-12, G-13

        /// <summary>G-12: a P1 flyer grounded over the gate tile (10, 10) lands on tile (9, 9), never in its own gate.</summary>
        [Test]
        public void G12_GroundedFlyerLandsBesideTheGate()
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            EntityId flyer = rig.Add(Flyer4Id, CombatP1, "5.5", "10.5");
            GatedColumn(rig);
            Move(rig, 0, flyer, "15.5", "10.5");
            rig.Sim.Submit(new ApplyStatusCommand(24, PlayerId.None, _seq++, flyer, StatusKind.Grounded, Fix64.One, Fix64.Zero, "script"));
            rig.Step(25);
            // It lands at (9.5, 9.5), then on the same tick walks on toward the gate (its own team's), 0.2 tiles north; never in the gate tile.
            Near(rig.Get(flyer).Position.X, "9.5", "landed x");
            Near(rig.Get(flyer).Position.Y, "9.7", "landed y, after one step");
            Assert.That(rig.Sim.Terrain.TileOf(rig.Get(flyer).Position), Is.EqualTo(rig.Sim.Terrain.IndexOf(9, 9)));
        }

        /// <summary>G-13a: a blast hits an enemy gate (188.75) but not the attacker's own (200), as W-7 with gates.</summary>
        [Test]
        public void G13_SplashHitsEnemyGatesOnly()
        {
            ContentCatalog catalog = ArenaCatalog();
            CombatRig.AddUnit(catalog, "t.blast", MovementLayer.Ground, 100, "light", new WeaponDefinition(Fix64.FromInt(12), "blast", Fix64.FromInt(7), Fix64.FromInt(10), true, true, false, false, 0, CombatRig.F("1"), CombatRig.F("1.5")));
            CombatRig.AddUnit(catalog, "t.light", MovementLayer.Ground, 100, "light");
            CombatRig rig = new CombatRig(catalog);
            EntityId a = rig.Add("t.blast", CombatP1, "2.5", "10.5");
            EntityId t = rig.Add("t.light", CombatP2, "8.5", "10.5");
            EntityId enemyGate = rig.Add(GateId, CombatP2, "9.5", "10.5");
            EntityId ownGate = rig.Add(GateId, CombatP1, "8.5", "11.5");
            rig.Attack(a, t);
            rig.Step(1);
            Assert.That(rig.Health(t), Is.EqualTo(Fix64.FromInt(91)));
            Assert.That(rig.Health(enemyGate), Is.EqualTo(CombatRig.F("188.75")));
            Assert.That(rig.Health(ownGate), Is.EqualTo(Fix64.FromInt(200)));
            Assert.That(rig.Of<BlastEvent>().Single().Victims, Is.EqualTo(1));
        }

        /// <summary>G-13b: a Quake damages a gate like a wall: 200 x 0.004 each second from tick 4800 down to 50.</summary>
        [Test]
        public void G13_QuakeDamagesGates()
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles());
            rig.ScriptPick(0, "quake");
            EntityId gate = rig.Spawn(0, CycleWorld.BuiltGateId, CycleWorld.P0, "5.5", "5.5");
            rig.RunThrough(9500);
            List<UnitDamagedEvent> hits = rig.Of<UnitDamagedEvent>().FindAll(e => e.Target == gate);
            Assert.That(hits.Count, Is.EqualTo(188));
            Assert.That(hits[0].Tick, Is.EqualTo(4800));
            Assert.That(hits[0].Damage, Is.EqualTo(Fix64.FromInt(200) * Fix64.Parse("0.004")));
            Assert.That(rig.UnitOf(gate).Health, Is.EqualTo(Fix64.FromInt(50)));
        }

        // ------------------------------------------------------------------ G-14

        /// <summary>G-14: the AI view shows an enemy gate only where the team sees the tile.</summary>
        [Test]
        public void G14_AiViewHidesGatesInFog()
        {
            Simulation sim = EconomySim();
            Spawn(sim, GateId, P1, 50, 40);
            Assert.That(AiWorldView.Build(sim, P0).IsBlocked(50, 40), Is.False, "enemy gate in fog");
            Spawn(sim, TestWorld.RunnerId, P0, 48, 40);
            Assert.That(AiWorldView.Build(sim, P0).IsBlocked(50, 40), Is.True, "enemy gate in sight");
        }

        // ------------------------------------------------------------------ G-15

        /// <summary>G-15: is_gate without is_wall is rejected naming the file and is_gate; exactly the two shipped gates carry it, with the spec values.</summary>
        [Test]
        public void G15_Loader()
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() =>
                DataLoader.Load(TestData.WithEdit("units/skai/wind_gate.json", "\"is_wall\": true", "\"is_wall\": false")));
            Assert.That(ex.Message, Does.Contain("wind_gate.json"));
            Assert.That(ex.Message, Does.Contain("is_gate"));
            DataSet data = TestData.LoadAll();
            List<string> gates = data.Content.Units.Values.Where(u => u.Economy.IsGate).Select(u => u.Id).ToList();
            Assert.That(gates, Is.EqualTo(new List<string> { "skai.wind_gate", "water.coral_gate" }));
            foreach (string id in gates)
            {
                UnitDefinition gate = data.Content.Units[id];
                Assert.That(gate.Economy.IsWall, Is.True);
                Assert.That(gate.MaxHealth, Is.EqualTo(Fix64.FromInt(200)));
                Assert.That(gate.CostCommon, Is.EqualTo(50));
                Assert.That(gate.CostRare, Is.EqualTo(0));
                Assert.That(gate.Economy.BuildSeconds, Is.EqualTo(Fix64.FromInt(10)));
                Assert.That(gate.Economy.FootprintTiles, Is.EqualTo(1));
                Assert.That(gate.ArmourType, Is.EqualTo("structure"));
                Assert.That(gate.Supply, Is.EqualTo(0));
            }
        }

        // ------------------------------------------------------------------ G-16

        /// <summary>The state hashes of a scene with one gate owned by <paramref name="owner"/> on tile (10, 10), one per hash tick.</summary>
        private static List<ulong> Hashes(PlayerId owner)
        {
            CombatRig rig = new CombatRig(ArenaCatalog());
            rig.Add(GateId, owner, "10.5", "10.5");
            rig.Step(30);
            return rig.Of<StateHashedEvent>().Select(e => e.Hash).ToList();
        }

        /// <summary>G-16: GateTeam changes the tile hash; the same gate owned by another team differs from the spawn tick; identical runs match at every hash tick.</summary>
        [Test]
        public void G16_Hash()
        {
            StateHasher one = new StateHasher();
            new TerrainTile(0, false, Fix64.Zero, TeamId.None, SurfaceState.Dry, TileBlockers.Gate, new TeamId(1)).WriteHash(one);
            StateHasher two = new StateHasher();
            new TerrainTile(0, false, Fix64.Zero, TeamId.None, SurfaceState.Dry, TileBlockers.Gate, new TeamId(2)).WriteHash(two);
            Assert.That(one.Hash, Is.Not.EqualTo(two.Hash));
            Assert.That((byte)TileBlockers.Gate, Is.EqualTo(8));
            Assert.That(new TerrainTile(0, false, Fix64.Zero, TeamId.None, SurfaceState.Dry, TileBlockers.None, new TeamId(1)).GateTeam, Is.EqualTo(TeamId.None), "normalised without the flag");
            List<ulong> a = Hashes(CombatP1);
            Assert.That(Hashes(CombatP1), Is.EqualTo(a));
            Assert.That(Hashes(CombatP2)[0], Is.Not.EqualTo(a[0]));
        }
    }
}

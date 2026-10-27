// rts/tests/Sim.Tests/Vision/VisionTests.cs
// Tests for fog of war (rts/docs/specs/fog_of_war.md section 7): the integer sight circle, per-team visible and explored
// grids, shared ally vision, the high-ground rule with spotters and ramps, short airborne sight and Grounded flyers,
// neutral units, the enemy list, state hashing, vision at match start, the combat hook (automatic acquisition skips unseen
// units, forced orders do not) and the airborne_sight_range_tiles data field. Part of Rts.Sim.Tests. Uses small in-code
// maps and test unit definitions; only the data-field tests read rts/data.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Sim.Vision;

namespace Rts.Sim.Tests.Vision
{
    /// <summary>Acceptance tests of the fog-of-war spec plus the combat hook and the data field.</summary>
    [TestFixture]
    public sealed class VisionTests
    {
        /// <summary>Side length of the test maps. Unit: tiles. Range: fixed 16.</summary>
        private const int Size = 16;

        /// <summary>Ground unit with sight 2 (stamp of 13 tiles).</summary>
        private const string Sight2Id = "test.sight2";

        /// <summary>Ground unit with sight 3 (stamp of 29 tiles).</summary>
        private const string Sight3Id = "test.sight3";

        /// <summary>Ground unit with sight 4.</summary>
        private const string Sight4Id = "test.sight4";

        /// <summary>Air unit with sight_range_tiles 9 and airborne_sight_range_tiles 6.</summary>
        private const string ScoutId = "test.scout";

        /// <summary>ID of the faction whose mechanic spawns a unit at match start (acceptance test 12).</summary>
        private const string StarterFactionId = "starter";

        /// <summary>ID of the mechanic that spawns a unit at match start.</summary>
        private const string StarterMechanicId = "test_start_spawn";

        /// <summary>Team 0.</summary>
        private static readonly TeamId Team0 = new TeamId(0);

        /// <summary>Team 1.</summary>
        private static readonly TeamId Team1 = new TeamId(1);

        /// <summary>Player 0 (team 0).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1 (team 1 in the default two-player world).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Next script sequence number for spawn and move commands. Unit: counter. Range: 0 upward; unique per run of this fixture.</summary>
        private static uint _sequence;

        /// <summary>Mechanic that spawns one P0 unit at match start, to prove Start computes vision before the first step.</summary>
        private sealed class StarterMechanic : IFactionMechanic
        {
            /// <summary>Mechanic ID.</summary>
            public string Id => StarterMechanicId;

            /// <summary>Spawns a sight-3 unit for player 0 at (4.5, 4.5).</summary>
            public void OnMatchStart(Simulation sim)
            {
                sim.SpawnUnit(sim.Content.Units[Sight3Id], P0, Pos("4.5", "4.5"));
            }

            /// <summary>Does nothing.</summary>
            public void OnTick(Simulation sim)
            {
            }

            /// <summary>Has no state.</summary>
            public void WriteHash(StateHasher hasher)
            {
            }
        }

        /// <summary>Position from decimal text, in tiles.</summary>
        private static FixVec2 Pos(string x, string y)
        {
            return new FixVec2(Fix64.Parse(x), Fix64.Parse(y));
        }

        /// <summary>A 16 by 16 map, every tile height 0, no ramps.</summary>
        private static MapDefinition FlatMap()
        {
            return new MapDefinition("vision_flat", "Vision Flat", Size, Size,
                new List<FixVec2> { FixVec2.FromInts(1, 1), FixVec2.FromInts(14, 14) });
        }

        /// <summary>A 16 by 16 map whose columns x >= 8 are height 1, with an optional ramp tile at height 0.</summary>
        /// <param name="rampX">Column of the ramp tile, or -1 for none.</param>
        /// <param name="rampY">Row of the ramp tile.</param>
        private static MapDefinition HighEastMap(int rampX = -1, int rampY = -1)
        {
            byte[] heights = new byte[Size * Size];
            bool[] ramps = new bool[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 8; x < Size; x++)
                {
                    heights[y * Size + x] = 1;
                }
            }
            if (rampX >= 0)
            {
                ramps[rampY * Size + rampX] = true;
            }
            return new MapDefinition("vision_high_east", "Vision High East", Size, Size,
                new List<FixVec2> { FixVec2.FromInts(1, 1), FixVec2.FromInts(14, 14) }, heights, ramps, null, null);
        }

        /// <summary>The shared test catalog plus the sight test units and the starter faction.</summary>
        private static ContentCatalog Catalog()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            foreach (KeyValuePair<string, int> sight in new[]
            {
                new KeyValuePair<string, int>(Sight2Id, 2), new KeyValuePair<string, int>(Sight3Id, 3), new KeyValuePair<string, int>(Sight4Id, 4),
            })
            {
                catalog.AddUnit(new UnitDefinition(sight.Key, sight.Key, TestWorld.TestFactionId, MovementLayer.Ground,
                    Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0, "light", Fix64.FromInt(sight.Value), null));
            }
            catalog.AddUnit(new UnitDefinition(ScoutId, ScoutId, TestWorld.TestFactionId, MovementLayer.Air,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0, "light", Fix64.FromInt(9), null, null, null, Fix64.FromInt(6)));
            catalog.AddFaction(new FactionDefinition(StarterFactionId, "Starter", StarterMechanicId, new List<string> { TestWorld.RunnerId }));
            return catalog;
        }

        /// <summary>Players 0 (team 0) and 1 (team 1), both on the no-mechanic test faction.</summary>
        private static List<PlayerSetup> TwoTeams()
        {
            return new List<PlayerSetup>
            {
                new PlayerSetup(P0, Team0, TestWorld.TestFactionId, "A"),
                new PlayerSetup(P1, Team1, TestWorld.TestFactionId, "B"),
            };
        }

        /// <summary>A started simulation with the vision catalog, the shipped tuning and the given map and players.</summary>
        private static Simulation Build(MapDefinition map, List<PlayerSetup>? players = null, ulong seed = 7)
        {
            FactionMechanicRegistry registry = BuiltInMechanics.CreateRegistry();
            registry.Register(StarterMechanicId, () => new StarterMechanic());
            SimulationConfig config = new SimulationConfig(
                new MatchSettings(seed, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                Catalog(), players ?? TwoTeams(), registry, TestWorld.Tuning, map);
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>Spawns a unit through a script command and steps one tick. Returns the new unit.</summary>
        private static Unit Spawn(Simulation sim, string definition, PlayerId owner, string x, string y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, Pos(x, y)));
            sim.Step();
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is UnitSpawnedEvent spawned)
                {
                    Assert.That(sim.Units.TryGet(spawned.Unit, out Unit? unit), Is.True);
                    return unit!;
                }
            }
            throw new AssertionException("Spawn produced no UnitSpawnedEvent.");
        }

        /// <summary>Orders a move and steps until the unit has no move target (at most 400 ticks).</summary>
        private static void MoveAndArrive(Simulation sim, Unit unit, string x, string y)
        {
            sim.Submit(new MoveUnitCommand(sim.Clock.Tick, unit.Owner, _sequence++, unit.Id, Pos(x, y)));
            sim.Step();
            // Ticks stepped so far. Unit: ticks. Range: 0 to 400.
            for (int i = 0; i < 400 && unit.HasMoveTarget; i++)
            {
                sim.Step();
            }
            Assert.That(unit.HasMoveTarget, Is.False, "the unit should have arrived");
        }

        /// <summary>1. Stamp counts: R 3 gives 29 tiles, 2.5 gives 21, 1 gives 5, 0 gives 1; the viewer's own tile comes first in its row and is always included.</summary>
        [Test]
        public void Stamp_HasTheSpecifiedTileCounts()
        {
            Assert.That(VisionStamp.Offsets(Fix64.FromInt(3)).Length, Is.EqualTo(29));
            Assert.That(VisionStamp.Offsets(Fix64.Parse("2.5")).Length, Is.EqualTo(21));
            Assert.That(VisionStamp.Offsets(Fix64.One).Length, Is.EqualTo(5));
            Assert.That(VisionStamp.Offsets(Fix64.Zero).Length, Is.EqualTo(1));
            // Scan order: dy from -r to r, then dx from -r to r, so radius 1 starts at (0, -1) and ends at (0, 1).
            VisionOffset[] one = VisionStamp.Offsets(Fix64.One);
            Assert.That(one[0].Dx, Is.EqualTo(0));
            Assert.That(one[0].Dy, Is.EqualTo(-1));
            Assert.That(one[4].Dx, Is.EqualTo(0));
            Assert.That(one[4].Dy, Is.EqualTo(1));
            VisionOffset only = VisionStamp.Offsets(Fix64.Zero)[0];
            Assert.That(only.Dx, Is.EqualTo(0));
            Assert.That(only.Dy, Is.EqualTo(0));
        }

        /// <summary>2. Circle edge: with R 3 at (4.5, 4.5) the tiles (7,4), (6,6), (4,1) are seen and (8,4), (7,5), (7,7) are not.</summary>
        [Test]
        public void CircleEdge_IsInclusiveAtTheRadius()
        {
            Simulation sim = Build(FlatMap());
            Spawn(sim, Sight3Id, P0, "4.5", "4.5");
            VisionSystem vision = sim.Vision;
            Assert.That(vision.IsTileVisible(Team0, 7, 4), Is.True);
            Assert.That(vision.IsTileVisible(Team0, 6, 6), Is.True);
            Assert.That(vision.IsTileVisible(Team0, 4, 1), Is.True);
            Assert.That(vision.IsTileVisible(Team0, 8, 4), Is.False);
            Assert.That(vision.IsTileVisible(Team0, 7, 5), Is.False);
            Assert.That(vision.IsTileVisible(Team0, 7, 7), Is.False);
            Assert.That(vision.GridsOf(Team0)!.CountVisible(), Is.EqualTo(29));
            // Off the map and unknown teams answer false instead of throwing.
            Assert.That(vision.IsTileVisible(Team0, -1, 4), Is.False);
            Assert.That(vision.IsTileVisible(Team0, 4, Size), Is.False);
            Assert.That(vision.IsTileVisible(new TeamId(9), 4, 4), Is.False);
            Assert.That(vision.IsTileExplored(TeamId.None, 4, 4), Is.False);
        }

        /// <summary>3. Explored persists: after the unit walks from (4.5, 4.5) to (12.5, 4.5), (4,4) is explored but not visible, (12,4) is both, and team 1 explored nothing.</summary>
        [Test]
        public void Explored_PersistsAfterTheUnitLeaves()
        {
            Simulation sim = Build(FlatMap());
            Unit unit = Spawn(sim, Sight3Id, P0, "4.5", "4.5");
            Assert.That(sim.Vision.IsTileVisible(Team0, 4, 4), Is.True);
            MoveAndArrive(sim, unit, "12.5", "4.5");
            Assert.That(sim.Vision.IsTileVisible(Team0, 4, 4), Is.False);
            Assert.That(sim.Vision.IsTileExplored(Team0, 4, 4), Is.True);
            Assert.That(sim.Vision.IsTileVisible(Team0, 12, 4), Is.True);
            Assert.That(sim.Vision.IsTileExplored(Team0, 12, 4), Is.True);
            Assert.That(sim.Vision.GridsOf(Team1)!.CountExplored(), Is.EqualTo(0));
            Assert.That(sim.Vision.IsTileExplored(Team1, 4, 4), Is.False);
            // The tiles walked past in between stay explored too.
            Assert.That(sim.Vision.IsTileExplored(Team0, 8, 4), Is.True);
        }

        /// <summary>4. Allies share: a unit of player 2 (team 0) lights tile (10,12) for player 0's team but not for team 1.</summary>
        [Test]
        public void Allies_ShareOneGrid()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, Team0, TestWorld.TestFactionId, "A"),
                new PlayerSetup(P1, Team1, TestWorld.TestFactionId, "B"),
                new PlayerSetup(new PlayerId(2), Team0, TestWorld.TestFactionId, "C"),
            };
            Simulation sim = Build(FlatMap(), players);
            Spawn(sim, Sight2Id, new PlayerId(2), "10.5", "10.5");
            Assert.That(sim.Vision.IsTileVisible(Team0, 10, 12), Is.True);
            Assert.That(sim.Vision.IsTileVisible(Team1, 10, 12), Is.False);
            Assert.That(sim.Vision.Teams.Count, Is.EqualTo(2), "one grid pair per team, not per player");
            Assert.That(sim.Vision.TeamOfPlayer(new PlayerId(2)), Is.EqualTo(Team0));
        }

        /// <summary>5. High ground hidden: a ground unit at (6.5, 6.5) with R 4 sees (5,6) but not the height-1 tile (9,6).</summary>
        [Test]
        public void HighGround_IsHiddenFromTheLowGround()
        {
            Simulation sim = Build(HighEastMap());
            Spawn(sim, Sight4Id, P0, "6.5", "6.5");
            Assert.That(sim.Vision.IsTileVisible(Team0, 9, 6), Is.False);
            Assert.That(sim.Vision.IsTileVisible(Team0, 5, 6), Is.True);
            Assert.That(sim.Vision.IsTileVisible(Team0, 7, 6), Is.True, "the last low tile before the cliff is still seen");
            Assert.That(sim.Vision.IsTileVisible(Team0, 8, 6), Is.False, "the first high tile is hidden");
        }

        /// <summary>6. Spotter: an allied unit on the high ground at (9.5, 2.5) makes (9,6) visible to the whole team, so an enemy at (9.5, 6.5) is seen.</summary>
        [Test]
        public void Spotter_OnTheHighGround_RevealsTheEnemyToTheTeam()
        {
            Simulation sim = Build(HighEastMap());
            Spawn(sim, Sight4Id, P0, "6.5", "6.5");
            Unit enemy = Spawn(sim, TestWorld.RunnerId, P1, "9.5", "6.5");
            Assert.That(sim.Vision.IsUnitVisibleTo(Team0, enemy), Is.False);
            Spawn(sim, Sight4Id, P0, "9.5", "2.5");
            Assert.That(sim.Vision.IsTileVisible(Team0, 9, 6), Is.True);
            Assert.That(sim.Vision.IsUnitVisibleTo(Team0, enemy), Is.True);
            Assert.That(sim.Vision.IsUnitVisibleTo(Team1, enemy), Is.True, "a unit is always visible to its own team");
            Assert.That(sim.Vision.CanTeamSeeUnit(Team0, enemy), Is.True);
        }

        /// <summary>7. Ramp eye: standing on the height-0 ramp tile (7,6) lifts the eye by one layer, so (9,6) becomes visible.</summary>
        [Test]
        public void RampTile_LiftsTheEyeByOneLayer()
        {
            Simulation sim = Build(HighEastMap(7, 6));
            Unit unit = Spawn(sim, Sight4Id, P0, "6.5", "6.5");
            Assert.That(sim.Vision.IsTileVisible(Team0, 9, 6), Is.False);
            MoveAndArrive(sim, unit, "7.5", "6.5");
            Assert.That(unit.Position.X.FloorToInt(), Is.EqualTo(7));
            Assert.That(sim.Terrain.At(7, 6).IsRamp, Is.True);
            Assert.That(sim.Vision.IsTileVisible(Team0, 9, 6), Is.True);
            Assert.That(sim.Vision.IsTileVisible(Team0, 11, 6), Is.True, "dx 4 is on the circle edge and the eye is high enough");
            Assert.That(sim.Vision.IsTileVisible(Team0, 12, 6), Is.False, "dx 5 is outside R 4");
        }

        /// <summary>8. Airborne sight: a flyer sees 6 tiles and every height; Grounded it sees 9 tiles but is bound by the high-ground rule.</summary>
        [Test]
        public void AirborneSight_IsShortAndIgnoresHeight_GroundedSightFollowsTheRule()
        {
            Simulation sim = Build(HighEastMap());
            Unit scout = Spawn(sim, ScoutId, P0, "3.5", "3.5");
            Assert.That(scout.Definition.SightRangeTiles, Is.EqualTo(Fix64.FromInt(9)));
            Assert.That(scout.Definition.AirborneSightRangeTiles, Is.EqualTo(Fix64.FromInt(6)));
            Assert.That(sim.Vision.IsTileVisible(Team0, 9, 3), Is.True, "distance 6, height 1, flyers see every height");
            Assert.That(sim.Vision.IsTileVisible(Team0, 3, 10), Is.False, "distance 7 is beyond the airborne radius");
            // Ground it for 5 seconds with the script status command.
            sim.Submit(new ApplyStatusCommand(sim.Clock.Tick, PlayerId.None, _sequence++, scout.Id, StatusKind.Grounded, Fix64.One, Fix64.FromInt(5), "test"));
            sim.Step();
            Assert.That(scout.EffectiveLayer, Is.EqualTo(MovementLayer.Ground));
            Assert.That(sim.Vision.IsTileVisible(Team0, 9, 3), Is.False, "Grounded: bound by the high-ground rule");
            Assert.That(sim.Vision.IsTileVisible(Team0, 3, 10), Is.True, "Grounded: ground sight range 9 reaches distance 7");
        }

        /// <summary>9. Neutral units give no vision to any team.</summary>
        [Test]
        public void NeutralUnits_GiveNoVision()
        {
            Simulation sim = Build(FlatMap());
            Spawn(sim, Sight4Id, PlayerId.None, "8.5", "8.5");
            Assert.That(sim.Vision.GridsOf(Team0)!.CountVisible(), Is.EqualTo(0));
            Assert.That(sim.Vision.GridsOf(Team1)!.CountVisible(), Is.EqualTo(0));
            Assert.That(sim.Vision.GridsOf(Team0)!.CountExplored(), Is.EqualTo(0));
        }

        /// <summary>10. Enemy list: enemies 5 and 3 inside team 0's sight and 4 outside give [3, 5]; own units are left out and the list is cleared first; a visible neutral unit is included.</summary>
        [Test]
        public void CollectVisibleEnemies_ReturnsVisibleEnemiesInIdOrder()
        {
            Simulation sim = Build(FlatMap());
            Unit viewer = Spawn(sim, Sight3Id, P0, "4.5", "4.5");
            Unit ownOther = Spawn(sim, TestWorld.RunnerId, P0, "4.5", "5.5");
            Unit enemy3 = Spawn(sim, TestWorld.RunnerId, P1, "5.5", "4.5");
            Unit enemy4 = Spawn(sim, TestWorld.RunnerId, P1, "12.5", "12.5");
            Unit enemy5 = Spawn(sim, TestWorld.RunnerId, P1, "4.5", "6.5");
            Assert.That(new[] { viewer.Id.Value, ownOther.Id.Value, enemy3.Id.Value, enemy4.Id.Value, enemy5.Id.Value }, Is.EqualTo(new uint[] { 1, 2, 3, 4, 5 }));
            List<EntityId> found = new List<EntityId> { new EntityId(99) };
            sim.Vision.CollectVisibleEnemies(Team0, found);
            Assert.That(found, Is.EqualTo(new List<EntityId> { enemy3.Id, enemy5.Id }));
            // A neutral unit inside the circle is reported too (ID 6); one outside is not.
            Unit rock = Spawn(sim, TestWorld.RunnerId, PlayerId.None, "6.5", "4.5");
            Spawn(sim, TestWorld.RunnerId, PlayerId.None, "14.5", "1.5");
            sim.Vision.CollectVisibleEnemies(Team0, found);
            Assert.That(found, Is.EqualTo(new List<EntityId> { enemy3.Id, enemy5.Id, rock.Id }));
        }

        /// <summary>Hash of the vision grids alone (the part of the state hash this system owns).</summary>
        private static ulong VisionHash(Simulation sim)
        {
            StateHasher hasher = new StateHasher();
            sim.Vision.WriteHash(hasher);
            return hasher.Hash;
        }

        /// <summary>11. Hash: identical runs agree; moving a unit one tile changes the hash; an extra explored tile with the same visible set changes it too.</summary>
        [Test]
        public void StateHash_CoversVisibleAndExploredGrids()
        {
            // Two identical runs: unit at (4.5, 4.5), 30 ticks.
            Simulation a = Build(FlatMap());
            Simulation b = Build(FlatMap());
            Spawn(a, Sight3Id, P0, "4.5", "4.5");
            Spawn(b, Sight3Id, P0, "4.5", "4.5");
            a.Step(30);
            b.Step(30);
            Assert.That(a.ComputeStateHash(), Is.EqualTo(b.ComputeStateHash()));
            Assert.That(VisionHash(a), Is.EqualTo(VisionHash(b)));

            // One unit one tile further east: both the vision hash and the full hash change.
            Simulation moved = Build(FlatMap());
            Spawn(moved, Sight3Id, P0, "5.5", "4.5");
            moved.Step(30);
            Assert.That(VisionHash(moved), Is.Not.EqualTo(VisionHash(a)));
            Assert.That(moved.ComputeStateHash(), Is.Not.EqualTo(a.ComputeStateHash()));

            // Same visible set now, different history: one run walked here from (4.5, 4.5), the other spawned here.
            Simulation walked = Build(FlatMap());
            Unit walker = Spawn(walked, Sight3Id, P0, "4.5", "4.5");
            MoveAndArrive(walked, walker, "12.5", "4.5");
            Simulation direct = Build(FlatMap());
            Spawn(direct, Sight3Id, P0, "12.5", "4.5");
            direct.Step(5);
            walked.Step(5);
            Assert.That(walked.Vision.GridsOf(Team0)!.CountVisible(), Is.EqualTo(direct.Vision.GridsOf(Team0)!.CountVisible()));
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Assert.That(walked.Vision.IsTileVisible(Team0, x, y), Is.EqualTo(direct.Vision.IsTileVisible(Team0, x, y)));
                }
            }
            Assert.That(walked.Vision.GridsOf(Team0)!.CountExplored(), Is.GreaterThan(direct.Vision.GridsOf(Team0)!.CountExplored()));
            Assert.That(VisionHash(walked), Is.Not.EqualTo(VisionHash(direct)), "explored history is hashed");
        }

        /// <summary>The hash also reflects a different team count and is stable across a replay of the same commands (determinism of the recompute).</summary>
        [Test]
        public void Replay_OfTheSameCommands_GivesTheSameVisionHashAtEveryTick()
        {
            // Hash of every tick for one scripted run.
            List<ulong> Run()
            {
                Simulation sim = Build(HighEastMap(7, 6));
                List<ulong> hashes = new List<ulong>();
                Unit unit = Spawn(sim, Sight4Id, P0, "2.5", "6.5");
                Spawn(sim, ScoutId, P1, "12.5", "12.5");
                sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P0, 500, unit.Id, Pos("7.5", "6.5")));
                for (int i = 0; i < 80; i++)
                {
                    sim.Step();
                    hashes.Add(VisionHash(sim));
                }
                return hashes;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        /// <summary>12. Start vision: right after Start (before the first step) team 0 sees and has explored the tiles around a unit placed at match start.</summary>
        [Test]
        public void Start_ComputesVisionBeforeTheFirstStep()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, Team0, StarterFactionId, "A"),
                new PlayerSetup(P1, Team1, TestWorld.TestFactionId, "B"),
            };
            Simulation sim = Build(FlatMap(), players);
            Assert.That(sim.Clock.Tick, Is.EqualTo(0));
            Assert.That(sim.Units.Count, Is.EqualTo(1));
            Assert.That(sim.Vision.IsTileVisible(Team0, 4, 4), Is.True);
            Assert.That(sim.Vision.IsTileVisible(Team0, 7, 4), Is.True);
            Assert.That(sim.Vision.IsTileVisible(Team0, 8, 4), Is.False);
            Assert.That(sim.Vision.IsTileExplored(Team0, 4, 4), Is.True);
            Assert.That(sim.Vision.GridsOf(Team1)!.CountVisible(), Is.EqualTo(0));
        }

        /// <summary>A unit that is removed stops giving vision on the next recompute, while its explored tiles remain.</summary>
        [Test]
        public void RemovedUnit_StopsGivingVision_ButExploredStays()
        {
            Simulation sim = Build(FlatMap());
            Unit unit = Spawn(sim, Sight3Id, P0, "4.5", "4.5");
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, unit.Id));
            sim.Step();
            Assert.That(sim.Vision.IsTileVisible(Team0, 4, 4), Is.False);
            Assert.That(sim.Vision.IsTileExplored(Team0, 4, 4), Is.True);
        }

        /// <summary>Builds the combat-hook world: shooter (sight 8, weapon range 5) at (6.5, 6.5), unarmed enemy tank on the high ground at (9.5, 6.5), 3 tiles away.</summary>
        private static Simulation HighGroundDuel(out Unit shooter, out Unit tank)
        {
            Simulation sim = Build(HighEastMap());
            shooter = Spawn(sim, TestWorld.ShooterId, P0, "6.5", "6.5");
            tank = Spawn(sim, TestWorld.TankId, P1, "9.5", "6.5");
            return sim;
        }

        /// <summary>Combat hook: an enemy in weapon range but hidden by the high-ground rule is not acquired; once a spotter reveals it, it is.</summary>
        [Test]
        public void Combat_AutomaticAcquisition_SkipsUnitsTheTeamCannotSee()
        {
            Simulation sim = HighGroundDuel(out Unit shooter, out Unit tank);
            sim.Step(60);
            Assert.That(sim.Vision.IsUnitVisibleTo(Team0, tank), Is.False);
            Assert.That(tank.Health, Is.EqualTo(Fix64.FromInt(100)), "no shot at a unit the team cannot see");
            Assert.That(shooter.AttackTarget.IsValid, Is.False);
            Spawn(sim, TestWorld.RunnerId, P0, "9.5", "4.5");
            sim.Step(20);
            Assert.That(sim.Vision.IsUnitVisibleTo(Team0, tank), Is.True);
            Assert.That(tank.Health < Fix64.FromInt(100), Is.True, "the spotter makes the tank a valid automatic target");
            Assert.That(shooter.AttackTarget, Is.EqualTo(tank.Id));
        }

        /// <summary>Combat hook: a target that stops being visible is dropped, an unforced shooter does not keep firing at it.</summary>
        [Test]
        public void Combat_UnforcedTarget_IsDroppedWhenItLeavesVision()
        {
            Simulation sim = HighGroundDuel(out Unit shooter, out Unit tank);
            Unit spotter = Spawn(sim, TestWorld.RunnerId, P0, "9.5", "4.5");
            sim.Step(20);
            Assert.That(shooter.AttackTarget, Is.EqualTo(tank.Id));
            // Removing the spotter hides the tank again.
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, spotter.Id));
            sim.Step(2);
            Assert.That(shooter.AttackTarget.IsValid, Is.False);
            // Health after the drop; it must not fall further while the tank stays hidden.
            Fix64 health = tank.Health;
            sim.Step(60);
            Assert.That(tank.Health, Is.EqualTo(health));
        }

        /// <summary>Combat hook: an explicit attack_unit order still works on a unit the team cannot see (commands are not yet filtered by vision).</summary>
        [Test]
        public void Combat_ForcedAttackOrder_WorksOnAnUnseenUnit()
        {
            Simulation sim = HighGroundDuel(out Unit shooter, out Unit tank);
            Assert.That(sim.Vision.IsUnitVisibleTo(Team0, tank), Is.False);
            sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P0, _sequence++, shooter.Id, tank.Id));
            sim.Step(20);
            Assert.That(tank.Health < Fix64.FromInt(100), Is.True);
        }

        /// <summary>The airborne sight defaults to the ground sight for hand-built definitions and rejects zero.</summary>
        [Test]
        public void Definition_AirborneSight_DefaultsToGroundSight()
        {
            ContentCatalog catalog = Catalog();
            Assert.That(catalog.Units[TestWorld.FlyerId].AirborneSightRangeTiles, Is.EqualTo(catalog.Units[TestWorld.FlyerId].SightRangeTiles));
            Assert.That(catalog.Units[Sight3Id].AirborneSightRangeTiles, Is.EqualTo(Fix64.FromInt(3)));
            Expect.Throws<ArgumentOutOfRangeException>(() => new UnitDefinition("x.y", "X", "test", MovementLayer.Air, Fix64.One, Fix64.One, 0, Fix64.Half, 0, 0,
                "light", Fix64.FromInt(5), null, null, null, Fix64.Zero));
        }

        /// <summary>Shipped data: the Roc Rider sees 10 on the ground and 6 in the air; ground units carry no airborne value of their own.</summary>
        [Test]
        public void ShippedData_RocRiderHasShortAirborneSight()
        {
            DataSet data = TestData.LoadAll();
            UnitDefinition roc = data.Content.Units["skai.roc_rider"];
            Assert.That(roc.Layer, Is.EqualTo(MovementLayer.Air));
            Assert.That(roc.AirborneSightRangeTiles, Is.EqualTo(Fix64.FromInt(6)));
            Assert.That(roc.AirborneSightRangeTiles < roc.SightRangeTiles, Is.True);
            UnitDefinition windrunner = data.Content.Units["skai.windrunner"];
            Assert.That(windrunner.AirborneSightRangeTiles, Is.EqualTo(windrunner.SightRangeTiles));
        }

        /// <summary>Loader: airborne_sight_range_tiles is required for an air unit and rejected for a ground unit, and each error names the file and field.</summary>
        [Test]
        public void Loader_RequiresAirborneSightForFlyersAndRejectsItElsewhere()
        {
            DataValidationException missing = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("units/skai/roc_rider.json", "\"airborne_sight_range_tiles\": 6,", string.Empty)));
            Assert.That(missing.Message, Does.Contain("units/skai/roc_rider.json"));
            Assert.That(missing.Message, Does.Contain("airborne_sight_range_tiles"));
            DataValidationException range = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("units/skai/roc_rider.json", "\"airborne_sight_range_tiles\": 6,", "\"airborne_sight_range_tiles\": 65,")));
            Assert.That(range.Message, Does.Contain("airborne_sight_range_tiles"));
            DataValidationException ground = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("units/skai/windrunner.json", "\"sight_range_tiles\": 9,", "\"sight_range_tiles\": 9, \"airborne_sight_range_tiles\": 5,")));
            Assert.That(ground.Message, Does.Contain("units/skai/windrunner.json"));
            Assert.That(ground.Message, Does.Contain("airborne_sight_range_tiles"));
        }

        /// <summary>The content digest covers the airborne sight, so two peers with different values diverge at tick 0.</summary>
        [Test]
        public void ContentDigest_ChangesWithAirborneSight()
        {
            ulong original = DataLoader.Load(TestData.ReadAll()).Content.ComputeDigest();
            ulong edited = DataLoader.Load(TestData.WithEdit("units/skai/roc_rider.json", "\"airborne_sight_range_tiles\": 6,", "\"airborne_sight_range_tiles\": 5,")).Content.ComputeDigest();
            Assert.That(edited, Is.Not.EqualTo(original));
        }
    }
}

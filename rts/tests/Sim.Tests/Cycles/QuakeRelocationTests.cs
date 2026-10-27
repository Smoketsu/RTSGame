// rts/tests/Sim.Tests/Cycles/QuakeRelocationTests.cs
// Tests for what a Quake does to the units standing on a quake-path tile that closes (planetary_cycles.md section 4, SetPath): a unit
// is moved only to a tile it can walk to under the ADR-006 rules (same height layer or a ramp, no cliff, never through or onto a tile
// that is closing in the same call), legal for its movement layer, chosen by nearest distance with a documented tie-break that leans
// toward the unit's own spawn point; units that cannot move (speed 0, Rooted HQs) keep the tile open; units re-path after the toggle;
// and the damage floor of a quake never lets a defence reach 0 health. Part of Rts.Sim.Tests. Cycles shift every 60 seconds with no
// warning, the shift is scripted to be a Quake, and the first shift is on tick 1200.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Cycles
{
    /// <summary>Unit relocation and re-pathing when quake paths close.</summary>
    [TestFixture]
    public class QuakeRelocationTests
    {
        /// <summary>A rig on the given map whose first shift (tick 1200) is a scripted Quake.</summary>
        private static CycleRig QuakeRig(MapDefinition map, List<CycleDefinition>? cycles = null, string scripted = "quake")
        {
            CycleRig rig = new CycleRig(map: map, tuning: CycleWorld.EditedTuning(CycleWorld.ShortCycleEdits()), cycles: cycles ?? CycleWorld.ShippedCycles());
            rig.ScriptPick(0, scripted);
            return rig;
        }

        /// <summary>A map whose single path "bridge" (open outside a Quake, closed during one) covers tile (9, 9).</summary>
        private static MapDefinition BridgeMap(byte[]? heights = null, bool[]? ramps = null, List<ResourceField>? fields = null)
        {
            return CycleWorld.Map(true, new List<QuakePath> { new QuakePath("bridge", false, new List<int> { CycleWorld.Tile(9, 9) }) }, heights, ramps, fields);
        }

        /// <summary>Sets the height of one tile of a 32 by 32 height array.</summary>
        private static void SetHeight(byte[] heights, int x, int y, byte height)
        {
            heights[CycleWorld.Tile(x, y)] = height;
        }

        /// <summary>The centre of tile (x, y) as a position. Unit: tiles.</summary>
        private static FixVec2 Centre(int x, int y)
        {
            return new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half);
        }

        /// <summary>The TilesChanged figure of the "bridge" path event of tick 1200.</summary>
        private static int BridgeTilesChanged(CycleRig rig)
        {
            return rig.Of<QuakePathChangedEvent>().FindAll(e => e.Tick == 1200 && e.PathId == "bridge")[0].TilesChanged;
        }

        // ---------------------------------------------------------------- height and reachability

        [Test]
        public void Relocation_NeverCrossesACliff_AndKeepsTheTileOpenWhenWalledIn()
        {
            // Every neighbour of tile (9, 9) is two layers up: nothing is reachable.
            byte[] heights = new byte[1024];
            for (int y = 8; y <= 10; y++)
            {
                for (int x = 8; x <= 10; x++)
                {
                    if (x != 9 || y != 9)
                    {
                        SetHeight(heights, x, y, 2);
                    }
                }
            }
            CycleRig rig = QuakeRig(BridgeMap(heights));
            EntityId walker = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.5", "9.5");
            rig.RunThrough(1200);
            Assert.That(rig.At<CycleStartedEvent>(1200)!.CycleId, Is.EqualTo("quake"));
            Assert.That(rig.UnitOf(walker).Position, Is.EqualTo(Centre(9, 9)), "the unit has nowhere legal to go");
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(9, 9)].IsBlocked, Is.False, "so the tile stays open");
            Assert.That(BridgeTilesChanged(rig), Is.EqualTo(0));
            Assert.That(rig.Of<UnitRelocatedEvent>(), Is.Empty);
        }

        [Test]
        public void Relocation_PrefersAReachableTileOverANearerOneAcrossACliff()
        {
            // North, east and south of (9, 9) are cliffs, so the three nearest tiles are not reachable; only the west one is.
            byte[] heights = new byte[1024];
            SetHeight(heights, 9, 8, 2);
            SetHeight(heights, 10, 9, 2);
            SetHeight(heights, 9, 10, 2);
            CycleRig rig = QuakeRig(BridgeMap(heights));
            EntityId walker = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.5", "9.5");
            rig.RunThrough(1200);
            Assert.That(rig.UnitOf(walker).Position, Is.EqualTo(Centre(8, 9)));
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(9, 9)].Blockers, Is.EqualTo(TileBlockers.Wall));
            Assert.That(BridgeTilesChanged(rig), Is.EqualTo(1));
        }

        [Test]
        public void Relocation_UsesARampToLeaveACliffedTile()
        {
            // Neighbours are cliffs (layer 3) except (10, 9), a ramp on layer 1: connected by a passable step, and layer 3 is two above the ramp.
            byte[] heights = new byte[1024];
            bool[] ramps = new bool[1024];
            for (int y = 8; y <= 10; y++)
            {
                for (int x = 8; x <= 10; x++)
                {
                    if (x != 9 || y != 9)
                    {
                        SetHeight(heights, x, y, 3);
                    }
                }
            }
            SetHeight(heights, 10, 9, 1);
            ramps[CycleWorld.Tile(10, 9)] = true;
            CycleRig rig = QuakeRig(BridgeMap(heights, ramps));
            EntityId walker = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.5", "9.5");
            rig.RunThrough(1200);
            Assert.That(rig.UnitOf(walker).Position, Is.EqualTo(Centre(10, 9)));
            Assert.That(rig.At<UnitRelocatedEvent>(1200)!.Unit, Is.EqualTo(walker));
        }

        [Test]
        public void Relocation_NeverLandsOnATileClosingInTheSameFlip()
        {
            // Path "pair" closes (9, 8) and (9, 9) together; the unit on (9, 9) may not move onto (9, 8).
            MapDefinition map = CycleWorld.Map(true, new List<QuakePath>
            {
                new QuakePath("pair", false, new List<int> { CycleWorld.Tile(9, 8), CycleWorld.Tile(9, 9) }),
            });
            CycleRig rig = QuakeRig(map);
            EntityId walker = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.5", "9.5");
            rig.RunThrough(1200);
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(9, 8)].Blockers, Is.EqualTo(TileBlockers.Wall));
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(9, 9)].Blockers, Is.EqualTo(TileBlockers.Wall));
            Assert.That(rig.UnitOf(walker).Position, Is.EqualTo(Centre(8, 9)), "nearest legal tile, leaning toward the owner's spawn");
            Assert.That(rig.Sim.Terrain[rig.Sim.Terrain.TileOf(rig.UnitOf(walker).Position)].IsBlocked, Is.False);
        }

        // ---------------------------------------------------------------- the tie-break

        [Test]
        public void Relocation_TieBreakLeansTowardTheUnitsOwnSpawn()
        {
            // Four tiles tie at distance 1 on open ground. Player 0 starts at (4, 4), player 1 at (28, 28).
            CycleRig first = QuakeRig(BridgeMap());
            EntityId fromPlayer0 = first.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.5", "9.5");
            first.RunThrough(1200);
            Assert.That(first.UnitOf(fromPlayer0).Position, Is.EqualTo(Centre(9, 8)), "player 0's unit steps toward its own start");
            CycleRig second = QuakeRig(BridgeMap());
            EntityId fromPlayer1 = second.Spawn(0, CycleWorld.ScoutId, CycleWorld.P1, "9.5", "9.5");
            second.RunThrough(1200);
            Assert.That(second.UnitOf(fromPlayer1).Position, Is.EqualTo(Centre(10, 9)), "player 1's unit steps toward the other side of the map");
        }

        [Test]
        public void Relocation_NearestByDistanceFromTheUnitsActualPosition()
        {
            // The unit stands in the east part of its tile, so the east neighbour is nearer than the west one whichever spawn it favours.
            CycleRig rig = QuakeRig(BridgeMap());
            EntityId walker = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.9", "9.5");
            rig.RunThrough(1200);
            Assert.That(rig.UnitOf(walker).Position, Is.EqualTo(Centre(10, 9)));
        }

        // ---------------------------------------------------------------- layers and immobile units

        [Test]
        public void Relocation_WaterLayerUnitMovesToATileLegalForIt()
        {
            CycleRig rig = QuakeRig(BridgeMap());
            EntityId boat = rig.Spawn(0, CycleWorld.BoatId, CycleWorld.P0, "9.5", "9.5");
            rig.RunThrough(1200);
            // Where the boat ended up.
            FixVec2 position = rig.UnitOf(boat).Position;
            Assert.That(position, Is.EqualTo(Centre(9, 8)));
            // Tile it stands on.
            int tile = rig.Sim.Terrain.TileOf(position);
            Assert.That(rig.Sim.Terrain.IsStandable(tile, MovementLayer.Water), Is.True);
            Assert.That(rig.Sim.Terrain.SpeedMultiplier(tile, MovementLayer.Water), Is.GreaterThan(Fix64.Zero));
        }

        [Test]
        public void Relocation_AFlyerIsNotMoved()
        {
            CycleRig rig = QuakeRig(BridgeMap());
            EntityId flyer = rig.Spawn(0, CycleWorld.SkyScoutId, CycleWorld.P0, "9.5", "9.5");
            rig.RunThrough(1200);
            Assert.That(rig.UnitOf(flyer).Position, Is.EqualTo(Centre(9, 9)));
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(9, 9)].Blockers, Is.EqualTo(TileBlockers.Wall));
        }

        [Test]
        public void Relocation_ASpeedZeroUnitKeepsTheTileOpen()
        {
            CycleRig rig = QuakeRig(BridgeMap());
            EntityId dummy = rig.Spawn(0, CycleWorld.DummyId, CycleWorld.P0, "9.5", "9.5");
            EntityId scout = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.5", "9.5");
            rig.RunThrough(1200);
            Assert.That(rig.UnitOf(dummy).Position, Is.EqualTo(Centre(9, 9)), "an immobile unit is never moved");
            Assert.That(rig.UnitOf(scout).Position, Is.EqualTo(Centre(9, 9)), "nobody on the tile moves when the tile stays open");
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(9, 9)].IsBlocked, Is.False, "the path tile does not close this cycle");
            Assert.That(BridgeTilesChanged(rig), Is.EqualTo(0));
            Assert.That(rig.Of<UnitRelocatedEvent>(), Is.Empty);
        }

        [Test]
        public void Relocation_ARootedHqKeepsTheTileOpen_AnUprootedOneMoves()
        {
            // A resource field close to (9.5, 9.5) makes an HQ spawned there Rooted.
            List<ResourceField> fields = new List<ResourceField> { new ResourceField(12, 9, ResourceKind.Common, 1000) };
            CycleRig rooted = QuakeRig(BridgeMap(null, null, fields));
            EntityId hq = rooted.Spawn(0, TestWorld.HqId, CycleWorld.P0, "9.5", "9.5");
            rooted.RunThrough(1200);
            Assert.That(rooted.UnitOf(hq).HqState, Is.EqualTo(HqState.Rooted));
            Assert.That(rooted.UnitOf(hq).Position, Is.EqualTo(Centre(9, 9)));
            Assert.That(rooted.Sim.Terrain[CycleWorld.Tile(9, 9)].IsBlocked, Is.False, "a Rooted HQ is never moved, so its tile does not close");
            Assert.That(BridgeTilesChanged(rooted), Is.EqualTo(0));

            // No field on the map: the HQ spawns Uprooted and is mobile, so it is relocated like any ground unit.
            CycleRig uprooted = QuakeRig(BridgeMap());
            EntityId mobile = uprooted.Spawn(0, TestWorld.HqId, CycleWorld.P0, "9.5", "9.5");
            uprooted.RunThrough(1200);
            Assert.That(uprooted.UnitOf(mobile).HqState, Is.EqualTo(HqState.Uprooted));
            Assert.That(uprooted.UnitOf(mobile).Position, Is.EqualTo(Centre(9, 8)));
            Assert.That(uprooted.Sim.Terrain[CycleWorld.Tile(9, 9)].Blockers, Is.EqualTo(TileBlockers.Wall));
        }

        // ---------------------------------------------------------------- re-pathing after a toggle

        [Test]
        public void MovingUnit_RepathsAroundAGateThatClosesWhileANorthWallOpens()
        {
            // A wall down column 16: the south gate (rows 20 to 31) is open outside a Quake, the north wall (rows 0 to 19) is closed.
            List<int> southGate = new List<int>();
            for (int y = 20; y <= 31; y++)
            {
                southGate.Add(CycleWorld.Tile(16, y));
            }
            List<int> northWall = new List<int>();
            for (int y = 0; y <= 19; y++)
            {
                northWall.Add(CycleWorld.Tile(16, y));
            }
            MapDefinition map = CycleWorld.Map(true, new List<QuakePath>
            {
                new QuakePath("south_gate", false, southGate),
                new QuakePath("north_wall", true, northWall),
            });
            CycleRig rig = QuakeRig(map);
            EntityId walker = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "10.5", "25.5");
            rig.Move(1150, walker, "22.5", "25.5");
            rig.RunThrough(1199);
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(16, 25)].IsBlocked, Is.False, "the gate is open before the Quake");
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(16, 5)].IsBlocked, Is.True, "the north wall is closed before the Quake");
            Assert.That(rig.UnitOf(walker).Position.X, Is.LessThan(Fix64.FromInt(16)), "still west of the wall when the Quake hits");
            // Terrain version before the flip, which invalidates every cached flow field when it changes.
            int versionBefore = rig.Sim.Terrain.Version;
            // Smallest Y the walker reaches after the flip. Unit: tiles.
            Fix64 minY = rig.UnitOf(walker).Position.Y;
            // Tick being run. Unit: ticks. Range: 1200 to 2000.
            for (int tick = 1200; tick <= 2000; tick++)
            {
                rig.RunThrough(tick);
                Fix64 y = rig.UnitOf(walker).Position.Y;
                if (y < minY)
                {
                    minY = y;
                }
            }
            Assert.That(rig.Sim.Terrain.Version, Is.GreaterThan(versionBefore), "the toggle bumped the terrain version");
            Assert.That(minY, Is.LessThan(Fix64.FromInt(20)), "the route changed: the walker went north to the opened gap");
            // Distance from the goal at the end. Unit: tiles.
            Fix64 left = (rig.UnitOf(walker).Position - new FixVec2(Fix64.Parse("22.5"), Fix64.Parse("25.5"))).Length();
            Assert.That(left, Is.LessThan(Fix64.One), "and it still arrived");
        }

        // ---------------------------------------------------------------- the damage floor

        [Test]
        public void QuakeDamage_NeverTakesADefenceToZero_EvenWithAFloorFractionOfZero()
        {
            // Hand-built effects: 5 percent per second and a floor fraction of 0 (the loader would refuse it; the system must still hold).
            CycleEffects harsh = new CycleEffects(Fix64.One, Fix64.Zero, Fix64.Parse("0.05"), Fix64.Zero, false, Fix64.One, Fix64.Zero);
            List<CycleDefinition> cycles = new List<CycleDefinition>
            {
                new CycleDefinition("bruise", "Bruise", CycleFavour.Attackers, "Test.", harsh),
                new CycleDefinition("calm", "Calm", CycleFavour.Air, "Test."),
            };
            CycleRig rig = QuakeRig(CycleWorld.Map(), cycles, "bruise");
            EntityId wall = rig.Spawn(0, CycleWorld.WallId, CycleWorld.P1, "20.5", "20.5");
            // 40 applications of 15 health would take 300 health to 0 twenty times over.
            rig.RunThrough(2300);
            Unit unit = rig.UnitOf(wall);
            Assert.That(unit.Health, Is.EqualTo(Fix64.Epsilon), "health stops one raw unit above 0");
            Assert.That(rig.Sim.Units.TryGet(wall, out _), Is.True, "the defence is still there");
            Assert.That(rig.Of<UnitKilledEvent>(), Is.Empty);
        }
    }
}

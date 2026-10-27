// rts/tests/Sim.Tests/Cycles/CycleEffectsTests.cs
// Acceptance tests 14 to 17 of rts/docs/specs/planetary_cycles.md section 7 (and the Core surge half of 20): Windstorm changes the speed
// of flyers and the range of surface ranged units from the tick after the shift, Quake damages static defences once a second down to a
// floor and flips quake paths (relocating ground units off tiles that close), Core surge multiplies the damage of attackers near the Core.
// Part of Rts.Sim.Tests. Cycles shift every 60 seconds with no warning here (a shift on tick 1200), except the Quake damage test, which
// uses the shipped 240 and 60 seconds so a whole 240-application cycle fits.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Cycles
{
    /// <summary>The effects of the three shipped cycles.</summary>
    [TestFixture]
    public class CycleEffectsTests
    {
        /// <summary>Two raised to the minus 30, the tolerance of the spec for a speed measured through vector maths. Unit: tiles (raw Q32.32 value 4).</summary>
        private static readonly Fix64 Tolerance = Fix64.FromRaw(4);

        /// <summary>A rig with cycles shifting on tick 1200 and the given cycle scripted for that shift.</summary>
        private static CycleRig ShortRig(string scriptedCycle, MapDefinition? map = null, Tuning? tuning = null)
        {
            CycleRig rig = new CycleRig(map: map, tuning: tuning ?? CycleWorld.EditedTuning(CycleWorld.ShortCycleEdits()), cycles: CycleWorld.ShippedCycles());
            rig.ScriptPick(0, scriptedCycle);
            return rig;
        }

        // ---------------------------------------------------------------- 14. Windstorm

        [Test]
        public void Windstorm_FlyersGainSpeedFromTheTickAfterTheShift()
        {
            CycleRig rig = ShortRig("windstorm");
            EntityId flyer = rig.Spawn(0, CycleWorld.SkyScoutId, CycleWorld.P0, "5.5", "5.5");
            rig.Move(1198, flyer, "30.5", "5.5");
            rig.RunThrough(1199);
            Fix64 x1199 = rig.UnitOf(flyer).Position.X;
            rig.RunThrough(1200);
            Fix64 x1200 = rig.UnitOf(flyer).Position.X;
            rig.RunThrough(1201);
            Fix64 x1201 = rig.UnitOf(flyer).Position.X;
            Assert.That(rig.At<CycleStartedEvent>(1200)!.CycleId, Is.EqualTo("windstorm"));
            Assert.That((x1200 - x1199 - Fix64.Parse("0.2")).Abs(), Is.LessThanOrEqualTo(Tolerance), "tick 1200 still moves at the normal 0.2 tiles");
            Assert.That((x1201 - x1200 - Fix64.Parse("0.25")).Abs(), Is.LessThanOrEqualTo(Tolerance), "tick 1201 moves at 1.25 times the speed");
        }

        [Test]
        public void Windstorm_GroundRangedUnitsLoseRange()
        {
            CycleRig rig = ShortRig("windstorm");
            EntityId archer = rig.Spawn(0, CycleWorld.ArcherId, CycleWorld.P0, "5.5", "20.5");
            // An idle enemy 5.75 tiles east: 4.75 tiles edge to edge, inside range 5 and outside range 4.
            rig.Spawn(0, CycleWorld.DummyId, CycleWorld.P1, "11.25", "20.5");
            rig.RunThrough(1300);
            List<int> hitTicks = rig.TicksOf<UnitDamagedEvent>(e => e.Attacker == archer);
            Assert.That(hitTicks, Does.Contain(0), "the archer hits on its spawn tick");
            Assert.That(hitTicks, Does.Contain(1200), "range is unchanged on the shift tick itself");
            Assert.That(hitTicks.FindAll(t => t >= 1201 && t <= 1300), Is.Empty, "no hit once the windstorm is active");
        }

        [Test]
        public void Windstorm_EffectiveRangeTiles()
        {
            CycleRig rig = ShortRig("windstorm");
            EntityId range2 = rig.Spawn(0, CycleWorld.Range2Id, CycleWorld.P0, "2.5", "2.5");
            EntityId melee = rig.Spawn(0, CycleWorld.MeleeId, CycleWorld.P0, "2.5", "4.5");
            EntityId skyShooter = rig.Spawn(0, CycleWorld.SkyShooterId, CycleWorld.P0, "2.5", "6.5");
            EntityId hq = rig.Spawn(0, TestWorld.HqId, CycleWorld.P0, "2.5", "8.5");
            rig.RunThrough(1199);
            Assert.That(rig.Sim.Cycles.EffectiveRangeTiles(rig.UnitOf(range2), rig.UnitOf(range2).Definition.Weapon!), Is.EqualTo(Fix64.FromInt(2)), "the weapon range before the shift");
            rig.RunThrough(1201);
            Assert.That(rig.Sim.Cycles.EffectiveRangeTiles(rig.UnitOf(range2), rig.UnitOf(range2).Definition.Weapon!), Is.EqualTo(Fix64.Parse("1.5")), "range 2 shrinks to melee range, not below");
            Assert.That(rig.Sim.Cycles.EffectiveRangeTiles(rig.UnitOf(melee), rig.UnitOf(melee).Definition.Weapon!), Is.EqualTo(Fix64.One));
            Assert.That(rig.Sim.Cycles.EffectiveRangeTiles(rig.UnitOf(skyShooter), rig.UnitOf(skyShooter).Definition.Weapon!), Is.EqualTo(Fix64.FromInt(5)));
            Assert.That(rig.Sim.Cycles.EffectiveRangeTiles(rig.UnitOf(hq), rig.UnitOf(hq).Definition.Weapon!), Is.EqualTo(Fix64.FromInt(5)));
        }

        // ---------------------------------------------------------------- 15. Quake damage

        [Test]
        public void Quake_DamagesStaticDefencesOncePerSecondDownToTheFloor()
        {
            // Shipped timings: the scripted quake is locked on tick 3600, starts on 4800 and ends on 9600.
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles());
            rig.ScriptPick(0, "quake");
            EntityId wall = rig.Spawn(0, CycleWorld.WallId, CycleWorld.P0, "5.5", "5.5");
            EntityId unbuilt = rig.Spawn(0, CycleWorld.WallId, CycleWorld.P0, "8.5", "5.5");
            EntityId hq = rig.Spawn(0, TestWorld.HqId, CycleWorld.P1, "11.5", "5.5");
            EntityId scout = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P1, "14.5", "5.5");
            rig.RunThrough(0);
            rig.UnitOf(unbuilt).SetConstructionTicks(1000000);
            rig.RunThrough(9500);
            Assert.That(rig.At<CycleStartedEvent>(4800)!.CycleId, Is.EqualTo("quake"));
            List<UnitDamagedEvent> hits = rig.Of<UnitDamagedEvent>().FindAll(e => e.Target == wall);
            Assert.That(hits.Count, Is.EqualTo(188), "188 applications reach the floor, then no more");
            // Damage of one application: 300 x 0.004 in Fix64, as the system computes it.
            Fix64 step = Fix64.FromInt(300) * Fix64.Parse("0.004");
            for (int i = 0; i < hits.Count; i++)
            {
                Assert.That(hits[i].Tick, Is.EqualTo(4800 + 20 * i), "application " + (i + 1));
                Assert.That(hits[i].Attacker.IsValid, Is.False, "quake damage comes from the environment");
                if (i < 187)
                {
                    Assert.That(hits[i].Damage, Is.EqualTo(step));
                }
            }
            Assert.That(hits[187].RemainingHealth, Is.EqualTo(Fix64.FromInt(75)), "the floor is 25 percent of 300");
            Assert.That(rig.UnitOf(wall).Health, Is.EqualTo(Fix64.FromInt(75)));
            Assert.That(rig.UnitOf(unbuilt).Health, Is.EqualTo(Fix64.FromInt(300)), "a defence under construction is untouched");
            Assert.That(rig.UnitOf(hq).Health, Is.EqualTo(rig.UnitOf(hq).Definition.MaxHealth), "an HQ is not a defence");
            Assert.That(rig.UnitOf(scout).Health, Is.EqualTo(Fix64.FromInt(100)), "a mobile unit is untouched");
            Assert.That(rig.Of<UnitDamagedEvent>().Count, Is.EqualTo(188), "nothing else was damaged");
        }

        // ---------------------------------------------------------------- 16. Core surge

        /// <summary>Hits by one attacker, as (tick, damage) pairs.</summary>
        private static List<UnitDamagedEvent> HitsBy(CycleRig rig, EntityId attacker)
        {
            return rig.Of<UnitDamagedEvent>().FindAll(e => e.Attacker == attacker);
        }

        [Test]
        public void CoreSurge_AttackersNearTheCoreHitHarder()
        {
            CycleRig rig = ShortRig("core_surge");
            // Core centre (16, 16). Attacker A stands 8 tiles away, B 8.5 tiles away; each has its own dummy 4 tiles further out.
            EntityId a = rig.Spawn(0, CycleWorld.StrikerId, CycleWorld.P0, "24", "16");
            rig.Spawn(0, CycleWorld.DummyId, CycleWorld.P1, "28", "16");
            EntityId b = rig.Spawn(0, CycleWorld.StrikerId, CycleWorld.P0, "16", "24.5");
            rig.Spawn(0, CycleWorld.DummyId, CycleWorld.P1, "16", "28.5");
            rig.RunThrough(1210);
            Assert.That(rig.At<CycleStartedEvent>(1200)!.CycleId, Is.EqualTo("core_surge"));
            foreach (EntityId attacker in new[] { a, b })
            {
                foreach (UnitDamagedEvent hit in HitsBy(rig, attacker))
                {
                    Fix64 expected = attacker == a && hit.Tick >= 1201 ? Fix64.FromInt(15) : Fix64.FromInt(10);
                    Assert.That(hit.Damage, Is.EqualTo(expected), attacker + " at tick " + hit.Tick);
                }
            }
            Assert.That(HitsBy(rig, a).FindAll(h => h.Tick >= 1201).Count, Is.GreaterThan(0));
            Assert.That(HitsBy(rig, b).FindAll(h => h.Tick >= 1201).Count, Is.GreaterThan(0));
        }

        [Test]
        public void CoreSurge_OnAMapWithoutACoreDoesNothing()
        {
            CycleRig rig = ShortRig("core_surge", CycleWorld.Map(withCore: false));
            EntityId a = rig.Spawn(0, CycleWorld.StrikerId, CycleWorld.P0, "17", "16");
            rig.Spawn(0, CycleWorld.DummyId, CycleWorld.P1, "21", "16");
            rig.RunThrough(1210);
            Assert.That(rig.At<CycleStartedEvent>(1200)!.CycleId, Is.EqualTo("core_surge"));
            Assert.That(HitsBy(rig, a).FindAll(h => h.Tick >= 1201).Count, Is.GreaterThan(0));
            foreach (UnitDamagedEvent hit in HitsBy(rig, a))
            {
                Assert.That(hit.Damage, Is.EqualTo(Fix64.FromInt(10)), "tick " + hit.Tick);
            }
        }

        // ---------------------------------------------------------------- 17. quake paths

        /// <summary>The map of test 17: path gap (closed outside a Quake), path bridge (open outside a Quake) and path depot (open, with a footprint on its tile).</summary>
        private static MapDefinition PathMap()
        {
            return CycleWorld.Map(true, new List<QuakePath>
            {
                new QuakePath("gap", true, new List<int> { CycleWorld.Tile(5, 5), CycleWorld.Tile(6, 5) }),
                new QuakePath("bridge", false, new List<int> { CycleWorld.Tile(9, 9) }),
                new QuakePath("depot", false, new List<int> { CycleWorld.Tile(12, 12) }),
            });
        }

        [Test]
        public void QuakePaths_FlipWithTheQuakeAndRelocateGroundUnits()
        {
            CycleRig rig = ShortRig("quake", PathMap());
            TerrainGrid terrain = rig.Sim.Terrain;
            Assert.That(terrain[CycleWorld.Tile(5, 5)].Blockers, Is.EqualTo(TileBlockers.Wall), "a path closed outside a Quake is walled at start");
            Assert.That(terrain[CycleWorld.Tile(6, 5)].Blockers, Is.EqualTo(TileBlockers.Wall));
            Assert.That(terrain[CycleWorld.Tile(9, 9)].Blockers, Is.EqualTo(TileBlockers.None));
            EntityId walker = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.5", "9.5");
            EntityId flyer = rig.Spawn(0, CycleWorld.SkyScoutId, CycleWorld.P0, "9.5", "9.5");
            rig.Spawn(0, CycleWorld.WallId, CycleWorld.P1, "12.5", "12.5");
            rig.RunThrough(1199);
            Assert.That(terrain[CycleWorld.Tile(5, 5)].IsBlocked, Is.True);
            Assert.That(terrain[CycleWorld.Tile(9, 9)].IsBlocked, Is.False);
            rig.RunThrough(1200);
            Assert.That(rig.At<CycleStartedEvent>(1200)!.CycleId, Is.EqualTo("quake"));
            Assert.That(terrain[CycleWorld.Tile(5, 5)].IsBlocked, Is.False, "the gap opens in a Quake");
            Assert.That(terrain[CycleWorld.Tile(6, 5)].IsBlocked, Is.False);
            Assert.That(terrain[CycleWorld.Tile(9, 9)].Blockers, Is.EqualTo(TileBlockers.Wall), "the bridge closes in a Quake");
            Assert.That(rig.UnitOf(walker).Position, Is.EqualTo(new FixVec2(Fix64.Parse("9.5"), Fix64.Parse("8.5"))), "nearest reachable tile centre (distance 1), tie to the spawn side, then lowest index");
            Assert.That(rig.UnitOf(flyer).Position, Is.EqualTo(new FixVec2(Fix64.Parse("9.5"), Fix64.Parse("9.5"))), "a flyer is not moved");
            UnitRelocatedEvent? moved = rig.At<UnitRelocatedEvent>(1200);
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved!.Unit, Is.EqualTo(walker));
            Assert.That(moved.Reason, Is.EqualTo("quake_path"));
            Assert.That(moved.From, Is.EqualTo(new FixVec2(Fix64.Parse("9.5"), Fix64.Parse("9.5"))));
            // The path events, in path order.
            List<QuakePathChangedEvent> changes = rig.Of<QuakePathChangedEvent>().FindAll(e => e.Tick == 1200);
            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(changes[0].PathId, Is.EqualTo("gap"));
            Assert.That(changes[0].Closed, Is.False);
            Assert.That(changes[0].TilesChanged, Is.EqualTo(2));
            Assert.That(changes[1].PathId, Is.EqualTo("bridge"));
            Assert.That(changes[1].Closed, Is.True);
            Assert.That(changes[1].TilesChanged, Is.EqualTo(1));
            Assert.That(changes[2].PathId, Is.EqualTo("depot"));
            Assert.That(changes[2].TilesChanged, Is.EqualTo(0), "a tile with a footprint is skipped");
            Assert.That(terrain[CycleWorld.Tile(12, 12)].Blockers, Is.EqualTo(TileBlockers.Footprint));
            // The next shift is not a Quake: both flip back.
            rig.RunThrough(2400);
            Assert.That(rig.At<CycleStartedEvent>(2400)!.CycleId, Is.Not.EqualTo("quake"));
            Assert.That(terrain[CycleWorld.Tile(5, 5)].Blockers, Is.EqualTo(TileBlockers.Wall));
            Assert.That(terrain[CycleWorld.Tile(6, 5)].Blockers, Is.EqualTo(TileBlockers.Wall));
            Assert.That(terrain[CycleWorld.Tile(9, 9)].Blockers, Is.EqualTo(TileBlockers.None));
            Assert.That(terrain[CycleWorld.Tile(12, 12)].Blockers, Is.EqualTo(TileBlockers.Footprint), "the footprint outlives the path toggles");
        }

        [Test]
        public void QuakePaths_ATileWithNoRoomForItsUnitsStaysOpen()
        {
            // Short cycles plus a relocation radius of 0, so a unit on a closing tile has nowhere to go.
            Tuning noRelocation = CycleWorld.EditedTuning(Concat(CycleWorld.ShortCycleEdits(),
                "tuning/match.json", "\"quake_path_relocate_radius_tiles\": 3", "\"quake_path_relocate_radius_tiles\": 0"));
            CycleRig rig = ShortRig("quake", PathMap(), noRelocation);
            EntityId walker = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "9.5", "9.5");
            rig.RunThrough(1200);
            Assert.That(rig.Sim.Terrain[CycleWorld.Tile(9, 9)].IsBlocked, Is.False, "the unit could not be moved, so the tile stays open");
            Assert.That(rig.UnitOf(walker).Position, Is.EqualTo(new FixVec2(Fix64.Parse("9.5"), Fix64.Parse("9.5"))));
            Assert.That(rig.Of<QuakePathChangedEvent>().FindAll(e => e.Tick == 1200 && e.PathId == "bridge")[0].TilesChanged, Is.EqualTo(0));
            Assert.That(rig.Of<UnitRelocatedEvent>(), Is.Empty);
        }

        /// <summary>The edits followed by one more triple.</summary>
        private static string[] Concat(string[] edits, params string[] more)
        {
            // Combined list of edit strings.
            List<string> all = new List<string>(edits);
            all.AddRange(more);
            return all.ToArray();
        }
    }
}

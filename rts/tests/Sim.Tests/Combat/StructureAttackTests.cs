// rts/tests/Sim.Tests/Combat/StructureAttackTests.cs
// Tests that units can walk to and fight structures: a structure's tile is blocked for ground movement (a Footprint blocker),
// and the flow field for a blocked goal leads to the standable neighbours, so units stop beside the structure instead of
// getting MoveUnreachableEvent. Covers attack_unit on an enemy structure far away and attack_move onto a structure's tile.
// Part of Rts.Sim.Tests. Uses the in-code fixtures of TestWorld (test.shooter, test.barracks) on the flat 64 by 64 map.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Terrain;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Combat
{
    /// <summary>Walking to, attacking and arriving beside structures.</summary>
    [TestFixture]
    public class StructureAttackTests
    {
        /// <summary>A shooter ordered to attack an enemy barracks 20 tiles away walks there, stops in weapon range and destroys it; the walk never reports an unreachable goal.</summary>
        [Test]
        public void AttackUnit_OnFarEnemyStructure_WalksThereAndKillsIt()
        {
            Simulation sim = TestWorld.Started();
            // Enemy barracks (200 health, armoured, footprint 1) 20 tiles east of the shooter.
            EntityId barracks = Spawn(sim, TestWorld.BarracksId, P1, 40, 10);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 20, 10);
            Assert.That(sim.Terrain.IsStandable(sim.Terrain.TileOf(At(40, 10)), MovementLayer.Ground), Is.False, "the structure blocks its tile");
            List<SimEvent> events = new List<SimEvent>();
            events.AddRange(Do(sim, new AttackUnitCommand(sim.Clock.Tick, P0, Seq(), shooter, barracks)));
            // 20 tiles at 2 tiles per second is about 14 s (280 ticks) to get in range, then 40 hits of 5 damage at one per 16 ticks.
            events.AddRange(StepCollect(sim, 1200));

            Assert.That(Of<MoveUnreachableEvent>(events), Is.Empty, "a blocked goal is approached, not refused");
            Assert.That(Reasons(events), Is.Empty);
            List<UnitKilledEvent> kills = Of<UnitKilledEvent>(events);
            Assert.That(kills.Count, Is.EqualTo(1));
            Assert.That(kills[0].Victim, Is.EqualTo(barracks));
            Assert.That(kills[0].Killer, Is.EqualTo(shooter));
            Assert.That(sim.Units.TryGet(barracks, out Unit? _), Is.False);
            // The shooter walked most of the way east of its start (it stops about 6 tiles short of the barracks).
            Assert.That(Get(sim, shooter).Position.X, Is.GreaterThan(Fix64.FromInt(30)));
        }

        /// <summary>An attack-move whose goal is the tile of a structure ends beside it with UnitArrivedEvent, not MoveUnreachableEvent.</summary>
        [Test]
        public void AttackMove_OntoStructureTile_StopsBesideItWithArrival()
        {
            Simulation sim = TestWorld.Started();
            // An allied barracks (never a target) 20 tiles east of the shooter.
            EntityId barracks = Spawn(sim, TestWorld.BarracksId, P0, 40, 10);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 20, 10);
            Unit structure = Get(sim, barracks);
            List<SimEvent> events = new List<SimEvent>();
            events.AddRange(Do(sim, new AttackMoveCommand(sim.Clock.Tick, P0, Seq(), shooter, structure.Position)));
            events.AddRange(StepCollect(sim, 400));

            Assert.That(Of<MoveUnreachableEvent>(events), Is.Empty);
            List<UnitArrivedEvent> arrived = Of<UnitArrivedEvent>(events);
            Assert.That(arrived.Count, Is.EqualTo(1));
            Assert.That(arrived[0].Unit, Is.EqualTo(shooter));
            Unit unit = Get(sim, shooter);
            Assert.That(unit.HasMoveTarget, Is.False);
            Assert.That(unit.IsAttackMoving, Is.False, "the order ends once the unit has stopped beside the structure");
            // Beside the structure: the tile next to the barracks tile, not on it.
            int unitTile = sim.Terrain.TileOf(unit.Position);
            int structureTile = sim.Terrain.TileOf(structure.Position);
            Assert.That(unitTile, Is.Not.EqualTo(structureTile));
            Assert.That(System.Math.Abs(sim.Terrain.XOf(unitTile) - sim.Terrain.XOf(structureTile)), Is.LessThanOrEqualTo(1));
            Assert.That(System.Math.Abs(sim.Terrain.YOf(unitTile) - sim.Terrain.YOf(structureTile)), Is.LessThanOrEqualTo(1));
        }
    }
}

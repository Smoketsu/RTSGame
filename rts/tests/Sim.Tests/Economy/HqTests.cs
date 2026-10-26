// rts/tests/Sim.Tests/Economy/HqTests.cs
// Tests for the mobile HQ unit (Phase 1 step 6): root and uproot timings in ticks, the "next to a resource field" rule, movement
// refused while Rooted, the weapon and armour following the state, Last Stand (switched off by default, once per HQ, exact length)
// and player defeat when the HQ dies. Part of Rts.Sim.Tests. Uses the in-code fixtures of TestWorld (test.hq, test.tank,
// test.shooter, test.mortar) on the economy map, whose common field at tile (10, 10) is the "resource field" the HQ roots beside.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>The mobile HQ: rooting, weapon, Last Stand and defeat.</summary>
    [TestFixture]
    public class HqTests
    {
        /// <summary>Root, uproot and root again take root_seconds and uproot_seconds from tuning/hq.json, counted in steps from the command's own tick.</summary>
        [Test]
        public void UprootThenRoot_TakeTheTuningTimes()
        {
            Simulation sim = TestWorld.StartedEconomy();
            // An HQ spawned beside the field at (10, 10) starts Rooted.
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            Unit hq = Get(sim, hqId);
            Assert.That(hq.HqState, Is.EqualTo(HqState.Rooted));
            // Ticks for each switch: 5 s and 10 s at 20 ticks per second.
            int uprootTicks = sim.Tuning.Hq.UprootTicks(sim.Clock);
            int rootTicks = sim.Tuning.Hq.RootTicks(sim.Clock);
            Assert.That(uprootTicks, Is.EqualTo(100));
            Assert.That(rootTicks, Is.EqualTo(200));

            sim.Submit(new UprootHqCommand(sim.Clock.Tick, P0, Seq(), hqId));
            // Events of the whole switch, in order.
            List<SimEvent> events = new List<SimEvent>();
            for (int step = 1; step <= uprootTicks; step++)
            {
                sim.Step();
                events.AddRange(sim.Events.Current);
                Assert.That(hq.HqState, Is.EqualTo(step < uprootTicks ? HqState.Uprooting : HqState.Uprooted), "after step " + step);
            }
            List<HqStateChangedEvent> changes = Of<HqStateChangedEvent>(events);
            Assert.That(changes.Count, Is.EqualTo(2));
            Assert.That(changes[0].From, Is.EqualTo(HqState.Rooted));
            Assert.That(changes[0].To, Is.EqualTo(HqState.Uprooting));
            Assert.That(changes[1].To, Is.EqualTo(HqState.Uprooted));
            Assert.That(changes[1].Tick - changes[0].Tick, Is.EqualTo(uprootTicks - 1));
            Assert.That(hq.HqTransitionTicksRemaining, Is.EqualTo(0));

            sim.Submit(new RootHqCommand(sim.Clock.Tick, P0, Seq(), hqId));
            for (int step = 1; step <= rootTicks; step++)
            {
                sim.Step();
                Assert.That(hq.HqState, Is.EqualTo(step < rootTicks ? HqState.Rooting : HqState.Rooted), "after step " + step);
            }
        }

        /// <summary>Rooting needs a field with resources within hq_root_max_distance_to_field_tiles; the other rejection reasons are stable.</summary>
        [Test]
        public void Root_RejectedAwayFromFields_AndOtherRejections()
        {
            Simulation sim = TestWorld.StartedEconomy();
            // Spawned 9.5 tiles from the nearest field centre (placeholder limit 6): starts Uprooted.
            EntityId far = Spawn(sim, TestWorld.HqId, P0, 20, 10);
            Assert.That(Get(sim, far).HqState, Is.EqualTo(HqState.Uprooted));
            AssertRejected(sim, new RootHqCommand(sim.Clock.Tick, P0, Seq(), far), "not_near_resources");
            Assert.That(Get(sim, far).HqState, Is.EqualTo(HqState.Uprooted));

            // Walk it next to the field (3.5 tiles away), then rooting is accepted.
            AssertAccepted(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), far, At(14, 10)));
            StepCollect(sim, 80);
            Assert.That(Get(sim, far).HasMoveTarget, Is.False);
            AssertAccepted(sim, new RootHqCommand(sim.Clock.Tick, P0, Seq(), far));
            Assert.That(Get(sim, far).HqState, Is.EqualTo(HqState.Rooting));
            AssertRejected(sim, new RootHqCommand(sim.Clock.Tick, P0, Seq(), far), "transition_in_progress");
            AssertRejected(sim, new UprootHqCommand(sim.Clock.Tick, P0, Seq(), far), "transition_in_progress");
            StepCollect(sim, 200);
            Assert.That(Get(sim, far).HqState, Is.EqualTo(HqState.Rooted));
            AssertRejected(sim, new RootHqCommand(sim.Clock.Tick, P0, Seq(), far), "already_rooted");

            // Not an HQ, not the owner.
            EntityId runner = Spawn(sim, TestWorld.RunnerId, P0, 30, 30);
            AssertRejected(sim, new RootHqCommand(sim.Clock.Tick, P0, Seq(), runner), "not_hq");
            AssertRejected(sim, new UprootHqCommand(sim.Clock.Tick, P0, Seq(), runner), "not_hq");
            AssertRejected(sim, new RootHqCommand(sim.Clock.Tick, P1, Seq(), far), "not_owner");
            AssertRejected(sim, new RootHqCommand(sim.Clock.Tick, P0, Seq(), new EntityId(999)), "unknown_unit");

            // Uproot, then uprooting an Uprooted HQ is refused.
            AssertAccepted(sim, new UprootHqCommand(sim.Clock.Tick, P0, Seq(), far));
            StepCollect(sim, 100);
            AssertRejected(sim, new UprootHqCommand(sim.Clock.Tick, P0, Seq(), far), "already_uprooted");
        }

        /// <summary>A Rooted, Rooting or Uprooting HQ refuses move, attack-move and attack orders with hq_rooted; an Uprooted one walks.</summary>
        [Test]
        public void RootedHq_RejectsMoves_UprootedHqMoves()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            EntityId tank = Spawn(sim, TestWorld.TankId, P1, 40, 40);
            AssertRejected(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, At(20, 12)), "hq_rooted");
            AssertRejected(sim, new AttackMoveCommand(sim.Clock.Tick, P0, Seq(), hqId, At(20, 12)), "hq_rooted");
            AssertRejected(sim, new AttackUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, tank), "hq_rooted");
            Assert.That(Get(sim, hqId).HasMoveTarget, Is.False);
            // Stop is always fine.
            AssertAccepted(sim, new StopCommand(sim.Clock.Tick, P0, Seq(), hqId));

            AssertAccepted(sim, new UprootHqCommand(sim.Clock.Tick, P0, Seq(), hqId));
            AssertRejected(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, At(20, 12)), "hq_rooted");
            StepCollect(sim, 100);
            Assert.That(Get(sim, hqId).HqState, Is.EqualTo(HqState.Uprooted));
            // Uprooted: the move is accepted and the HQ walks (speed 2 tiles per second, so 0.1 tile per tick).
            FixVec2 before = Get(sim, hqId).Position;
            AssertAccepted(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), hqId, At(20, 12)));
            StepCollect(sim, 20);
            Assert.That(Get(sim, hqId).Position.X, Is.GreaterThan(before.X));
        }

        /// <summary>The weapon fires only while Rooted; the armour type is the rooted one only while Rooted.</summary>
        [Test]
        public void HqWeapon_OnlyWhileRooted_ArmourFollowsState()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            // An unarmed enemy tank 3 tiles away, inside the HQ's range of 5.
            EntityId tank = Spawn(sim, TestWorld.TankId, P1, 15, 12);
            Unit hq = Get(sim, hqId);
            Assert.That(hq.ArmourType, Is.EqualTo("armoured"));

            // Rooted: the HQ shoots the tank.
            List<SimEvent> rooted = StepCollect(sim, 40);
            Assert.That(HqHits(rooted, hqId).Count, Is.GreaterThanOrEqualTo(2));
            Fix64 tankHealth = Get(sim, tank).Health;
            Assert.That(tankHealth, Is.LessThan(Fix64.FromInt(100)));

            // Uprooting and Uprooted: silent, and the tank is left alone.
            List<SimEvent> switching = Do(sim, new UprootHqCommand(sim.Clock.Tick, P0, Seq(), hqId));
            Assert.That(hq.ArmourType, Is.EqualTo("light"));
            switching.AddRange(StepCollect(sim, 120));
            Assert.That(hq.HqState, Is.EqualTo(HqState.Uprooted));
            Assert.That(HqHits(switching, hqId).Count, Is.EqualTo(0));
            Assert.That(Get(sim, tank).Health, Is.EqualTo(tankHealth));

            // Rooting again: still silent until Rooted, then the weapon returns.
            List<SimEvent> back = Do(sim, new RootHqCommand(sim.Clock.Tick, P0, Seq(), hqId));
            Assert.That(hq.ArmourType, Is.EqualTo("light"));
            back.AddRange(StepCollect(sim, 230));
            Assert.That(hq.HqState, Is.EqualTo(HqState.Rooted));
            int rootedTick = -1;
            foreach (HqStateChangedEvent change in Of<HqStateChangedEvent>(back))
            {
                if (change.To == HqState.Rooted)
                {
                    rootedTick = change.Tick;
                }
            }
            Assert.That(rootedTick, Is.GreaterThan(0));
            List<UnitDamagedEvent> hits = HqHits(back, hqId);
            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(1));
            foreach (UnitDamagedEvent hit in hits)
            {
                Assert.That(hit.Tick, Is.GreaterThanOrEqualTo(rootedTick));
            }

            // Armour decides the damage of the same shot: kinetic 10 does 5 to the Rooted "armoured" HQ and 10 to the Uprooted "light" HQ.
            Simulation armour = TestWorld.StartedEconomy();
            EntityId hq2 = Spawn(armour, TestWorld.HqId, P0, 12, 12);
            List<SimEvent> shot = Do(armour, new SpawnUnitCommand(armour.Clock.Tick, PlayerId.None, Seq(), TestWorld.ShooterId, P1, At(12, 16)));
            List<UnitDamagedEvent> onHq = HitsOn(shot, hq2);
            Assert.That(onHq.Count, Is.EqualTo(1));
            Assert.That(onHq[0].Damage, Is.EqualTo(Fix64.FromInt(5)));
            List<SimEvent> uprooting = Do(armour, new UprootHqCommand(armour.Clock.Tick, P0, Seq(), hq2));
            uprooting.AddRange(StepCollect(armour, 40));
            onHq = HitsOn(uprooting, hq2);
            Assert.That(onHq.Count, Is.GreaterThanOrEqualTo(2));
            foreach (UnitDamagedEvent hit in onHq)
            {
                Assert.That(hit.Damage, Is.EqualTo(Fix64.FromInt(10)));
            }
        }

        /// <summary>Hits fired by the HQ in <paramref name="events"/>.</summary>
        private static List<UnitDamagedEvent> HqHits(List<SimEvent> events, EntityId hq)
        {
            // Matching hits.
            List<UnitDamagedEvent> hits = new List<UnitDamagedEvent>();
            foreach (UnitDamagedEvent hit in Of<UnitDamagedEvent>(events))
            {
                if (hit.Attacker == hq)
                {
                    hits.Add(hit);
                }
            }
            return hits;
        }

        /// <summary>Hits that landed on <paramref name="target"/> in <paramref name="events"/>.</summary>
        private static List<UnitDamagedEvent> HitsOn(List<SimEvent> events, EntityId target)
        {
            // Matching hits.
            List<UnitDamagedEvent> hits = new List<UnitDamagedEvent>();
            foreach (UnitDamagedEvent hit in Of<UnitDamagedEvent>(events))
            {
                if (hit.Target == target)
                {
                    hits.Add(hit);
                }
            }
            return hits;
        }

        /// <summary>Last Stand is off in the shipped tuning: an HQ pushed under 25% keeps taking damage, dies, and its player is Defeated, which decides a 1v1.</summary>
        [Test]
        public void LastStand_OffByDefault_HqDeathDefeatsOwnerAndDecidesOneVersusOne()
        {
            Assert.That(TestWorld.Tuning.Hq.LastStandEnabled, Is.False);
            Simulation sim = TestWorld.StartedEconomy();
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            Spawn(sim, TestWorld.HqId, P1, 50, 48);
            Assert.That(MatchOutcome.Evaluate(sim.Players).IsDecided, Is.False);
            // Leave the HQ at 60 of 200 health; one mortar hit does 40 to the Rooted "armoured" HQ (blast x2).
            Get(sim, hqId).SetHealth(Fix64.FromInt(60));
            List<SimEvent> events = Do(sim, new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.MortarId, P1, At(12, 16)));
            events.AddRange(StepCollect(sim, 60));
            Assert.That(Of<HqLastStandEvent>(events), Is.Empty);
            Assert.That(sim.Units.Contains(hqId), Is.False, "the HQ died to the second mortar hit");
            List<PlayerStateChangedEvent> changes = Of<PlayerStateChangedEvent>(events);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Player, Is.EqualTo(P0));
            Assert.That(changes[0].Previous, Is.EqualTo(PlayerState.Active));
            Assert.That(changes[0].Current, Is.EqualTo(PlayerState.Defeated));
            Assert.That(changes[0].Cause, Is.EqualTo("hq_destroyed"));
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            Assert.That(outcome.IsDecided, Is.True);
            Assert.That(outcome.Winner, Is.EqualTo(new TeamId(1)));
            // The kill, the removal and the defeat are reported in that order within the tick.
            int killed = events.FindIndex(e => e is UnitKilledEvent k && k.Victim == hqId);
            int removed = events.FindIndex(e => e is UnitRemovedEvent r && r.Unit == hqId);
            int defeated = events.FindIndex(e => e is PlayerStateChangedEvent);
            Assert.That(killed, Is.GreaterThanOrEqualTo(0));
            Assert.That(removed, Is.GreaterThan(killed));
            Assert.That(defeated, Is.GreaterThan(removed));
            Assert.That(PlayerOf(sim, P1).IsActive, Is.True);
        }

        /// <summary>With Last Stand on, it triggers once at the threshold; later hits below the threshold do not trigger it again.</summary>
        [Test]
        public void LastStand_TriggersOnce_ThenWearsOff()
        {
            Tuning enabled = TuningWithEdit("tuning/hq.json", "\"last_stand_enabled\": false", "\"last_stand_enabled\": true");
            Simulation sim = TestWorld.StartedEconomy(1234, enabled);
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            Unit hq = Get(sim, hqId);
            // 60 of 200 health; the shooter does 5 per hit to the Rooted HQ, so the second hit reaches 50 = 25% and triggers.
            hq.SetHealth(Fix64.FromInt(60));
            List<SimEvent> events = Do(sim, new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.ShooterId, P1, At(12, 16)));
            events.AddRange(StepCollect(sim, 260));
            List<HqLastStandEvent> triggers = Of<HqLastStandEvent>(events);
            Assert.That(triggers.Count, Is.EqualTo(1));
            Assert.That(triggers[0].Unit, Is.EqualTo(hqId));
            Assert.That(triggers[0].Health, Is.EqualTo(Fix64.FromInt(50)));
            Assert.That(triggers[0].InvulnerableTicks, Is.EqualTo(100));
            int start = triggers[0].Tick;
            foreach (UnitDamagedEvent hit in HitsOn(events, hqId))
            {
                if (hit.Tick > start && hit.Tick < start + 100)
                {
                    Assert.That(hit.Damage, Is.EqualTo(Fix64.Zero), "hit at tick " + hit.Tick + " is inside Last Stand");
                }
                else if (hit.Tick >= start + 100)
                {
                    Assert.That(hit.Damage, Is.EqualTo(Fix64.FromInt(5)), "hit at tick " + hit.Tick + " is after Last Stand");
                }
            }
            Assert.That(hq.LastStandUsed, Is.True);
            Assert.That(hq.IsInvulnerable, Is.False);
            // Below the 25% threshold after Last Stand and still alive: it did not trigger a second time.
            Assert.That(hq.Health, Is.LessThan(Fix64.FromInt(50)));
            Assert.That(hq.Health, Is.GreaterThan(Fix64.Zero));
        }

        /// <summary>Last Stand protects for exactly last_stand_invulnerable_seconds of ticks; the first real hit after it kills the weakened HQ and defeats its owner.</summary>
        [Test]
        public void LastStand_HoldsExactlyConfiguredTicks_ThenHqDies()
        {
            Tuning enabled = TuningWithEdit("tuning/hq.json", "\"last_stand_enabled\": false", "\"last_stand_enabled\": true");
            Simulation sim = TestWorld.StartedEconomy(1234, enabled);
            EntityId hqId = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            Spawn(sim, TestWorld.HqId, P1, 50, 48);
            Get(sim, hqId).SetHealth(Fix64.FromInt(60));
            // The mortar fires on its spawn tick and then every 20 ticks: 40 damage to the Rooted HQ, leaving 20 and triggering Last Stand.
            List<SimEvent> events = Do(sim, new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), TestWorld.MortarId, P1, At(12, 16)));
            events.AddRange(StepCollect(sim, 130));
            HqLastStandEvent trigger = Of<HqLastStandEvent>(events)[0];
            int start = trigger.Tick;
            Assert.That(trigger.Health, Is.EqualTo(Fix64.FromInt(20)));
            List<UnitDamagedEvent> hits = HitsOn(events, hqId);
            // Hits at start, +20, +40, +60, +80 (the first one triggered it; the other four are ignored) and +100 (kills).
            Assert.That(hits.Count, Is.EqualTo(6));
            Assert.That(hits[0].Tick, Is.EqualTo(start));
            Assert.That(hits[0].Damage, Is.EqualTo(Fix64.FromInt(40)));
            for (int i = 1; i <= 4; i++)
            {
                Assert.That(hits[i].Tick, Is.EqualTo(start + 20 * i));
                Assert.That(hits[i].Damage, Is.EqualTo(Fix64.Zero));
                Assert.That(hits[i].RemainingHealth, Is.EqualTo(Fix64.FromInt(20)));
            }
            Assert.That(hits[5].Tick, Is.EqualTo(start + 100));
            Assert.That(hits[5].Damage, Is.EqualTo(Fix64.FromInt(40)));
            Assert.That(sim.Units.Contains(hqId), Is.False);
            Assert.That(Of<HqLastStandEvent>(events).Count, Is.EqualTo(1));
            Assert.That(PlayerOf(sim, P0).State, Is.EqualTo(PlayerState.Defeated));
        }
    }
}

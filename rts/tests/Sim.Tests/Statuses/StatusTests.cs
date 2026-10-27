// rts/tests/Sim.Tests/Statuses/StatusTests.cs
// Acceptance tests of the shared status system (rts/docs/specs/statuses.md section 7, tests 1 to 12) plus checks of the
// apply_status rejections, the script parser, hashing, the data keys and the Spoutling's applies_drench flag.
// Part of Rts.Sim.Tests. Ticks in comments are simulation ticks at 20 ticks per second.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Statuses
{
    /// <summary>Grounded, Slowed, drench, landing, wall blocking, commands, hashing and data.</summary>
    [TestFixture]
    public sealed class StatusTests
    {
        /// <summary>Flyer start used by the simple lifecycle tests. Unit: tiles.</summary>
        private static readonly FixVec2 Start = StatusTestWorld.Centre(10, 10);

        /// <summary>1. Grounded at tick 10: Ground and overridden through tick 89, until tick 90; tick 90 expires it with status_expired then unit_layer_changed.</summary>
        [Test]
        public void Grounded_Lifecycle()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.FlyerId, StatusTestWorld.P0, Start);
            // Duration omitted (0): grounded_duration_seconds = 4 s = 80 ticks.
            StatusTestWorld.Apply(sim, 10, 0, 1, StatusKind.Grounded, "1", "0");
            Unit flyer = null!;
            StatusTestWorld.StepThrough(sim, 9);
            flyer = StatusTestWorld.UnitOf(sim, 1);
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Air));
            StatusTestWorld.StepThrough(sim, 10);
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Ground));
            Assert.That(flyer.HasLayerOverride, Is.True);
            Assert.That(flyer.Statuses.GroundedUntilTick, Is.EqualTo(90));
            Assert.That(flyer.Statuses.IsGrounded, Is.True);
            StatusTestWorld.StepThrough(sim, 89);
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Ground));
            sim.Step(); // tick 90
            List<SimEvent> events = new List<SimEvent>(sim.Events.Current);
            int expired = events.FindIndex(e => e is StatusExpiredEvent);
            int changed = events.FindIndex(e => e is UnitLayerChangedEvent);
            Assert.That(expired, Is.GreaterThanOrEqualTo(0));
            Assert.That(changed, Is.GreaterThan(expired), "status_expired comes first, then unit_layer_changed");
            UnitLayerChangedEvent layer = (UnitLayerChangedEvent)events[changed];
            Assert.That(layer.From, Is.EqualTo(MovementLayer.Ground));
            Assert.That(layer.To, Is.EqualTo(MovementLayer.Air));
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Air));
            Assert.That(flyer.HasLayerOverride, Is.False);
            Assert.That(flyer.Statuses.IsGrounded, Is.False);
            Assert.That(flyer.Statuses.GroundedSourceKey, Is.EqualTo(string.Empty));
        }

        /// <summary>2. Grounded again one tick later refreshes to the later end (91) instead of adding durations, keeps the first key, and reports Refreshed.</summary>
        [Test]
        public void Grounded_RefreshesInsteadOfExtending()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.FlyerId, StatusTestWorld.P0, Start);
            StatusTestWorld.Apply(sim, 10, 0, 1, StatusKind.Grounded, "1", "0", "first");
            StatusTestWorld.Apply(sim, 11, 0, 1, StatusKind.Grounded, "1", "0", "second");
            StatusTestWorld.StepThrough(sim, 10);
            Assert.That(StatusTestWorld.EventsOf<StatusAppliedEvent>(sim)[0].Refreshed, Is.False);
            sim.Step();
            Unit flyer = StatusTestWorld.UnitOf(sim, 1);
            Assert.That(flyer.Statuses.GroundedUntilTick, Is.EqualTo(91));
            Assert.That(flyer.Statuses.GroundedSourceKey, Is.EqualTo("first"));
            List<StatusAppliedEvent> applied = StatusTestWorld.EventsOf<StatusAppliedEvent>(sim);
            Assert.That(applied.Count, Is.EqualTo(1));
            Assert.That(applied[0].Refreshed, Is.True);
            Assert.That(applied[0].UntilTick, Is.EqualTo(91));
        }

        /// <summary>3. Grounded on a ground unit is rejected not_a_flyer; ApplyGrounded returns false; no status and no event.</summary>
        [Test]
        public void Grounded_OnlyFlyers()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
            StatusTestWorld.Apply(sim, 5, 0, 1, StatusKind.Grounded, "1", "0");
            bool direct = true;
            StatusTestWorld.Run(sim, 6, 0, s => direct = StatusSystem.ApplyGrounded(s, StatusTestWorld.UnitOf(s, 1), 10, "direct", PlayerId.None));
            StatusTestWorld.StepThrough(sim, 4);
            StatusTestWorld.StepThrough(sim, 5);
            List<CommandRejectedEvent> rejected = StatusTestWorld.EventsOf<CommandRejectedEvent>(sim);
            Assert.That(rejected.Count, Is.EqualTo(1));
            Assert.That(rejected[0].Reason, Is.EqualTo("not_a_flyer"));
            sim.Step(); // tick 6
            Assert.That(direct, Is.False);
            Unit runner = StatusTestWorld.UnitOf(sim, 1);
            Assert.That(runner.Statuses.IsGrounded, Is.False);
            Assert.That(runner.HasLayerOverride, Is.False);
            Assert.That(StatusTestWorld.EventsOf<StatusAppliedEvent>(sim), Is.Empty);
            Assert.That(StatusTestWorld.EventsOf<UnitLayerChangedEvent>(sim), Is.Empty);
        }

        /// <summary>4. A ground unit of speed 2 slowed 0.5 for 2 s at tick 0 advances 0.05 per tick on ticks 0 to 39 and 0.1 from tick 40, and status_expired is emitted at tick 40.</summary>
        [Test]
        public void Slow_ChangesSpeedAndExpires()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
            StatusTestWorld.Move(sim, 0, 1, 1, StatusTestWorld.Centre(50, 10));
            StatusTestWorld.Apply(sim, 0, 2, 1, StatusKind.Slowed, "0.5", "2", "chill");
            // Per-tick advance at speed 1 (2 times 0.5) and at speed 2, computed with the same clock conversion the movement system uses.
            Fix64 slowStep = sim.Clock.PerSecondToPerTick(Fix64.One);
            Fix64 fullStep = sim.Clock.PerSecondToPerTick(Fix64.FromInt(2));
            Unit unit = null!;
            Fix64 previousX = Start.X;
            for (int tick = 0; tick < 60; tick++)
            {
                sim.Step();
                unit = StatusTestWorld.UnitOf(sim, 1);
                Fix64 advance = unit.Position.X - previousX;
                previousX = unit.Position.X;
                Assert.That(advance, Is.EqualTo(tick < 40 ? slowStep : fullStep), "advance on tick " + tick);
                List<StatusExpiredEvent> expired = StatusTestWorld.EventsOf<StatusExpiredEvent>(sim);
                Assert.That(expired.Count, Is.EqualTo(tick == 40 ? 1 : 0), "status_expired count on tick " + tick);
                if (tick == 40)
                {
                    Assert.That(expired[0].Status, Is.EqualTo(StatusKind.Slowed));
                    Assert.That(expired[0].SourceKey, Is.EqualTo("chill"));
                }
            }
            Assert.That(slowStep, Is.EqualTo(Fix64.One / Fix64.FromInt(20)));
        }

        /// <summary>5. Slows never multiply: key a 0.75 for 100 ticks and key b 0.5 for 20 ticks give 0.5 on ticks 0 to 19, 0.75 on 20 to 99, One from 100.</summary>
        [Test]
        public void Slow_StrongestWins()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
            StatusTestWorld.Apply(sim, 0, 1, 1, StatusKind.Slowed, "0.75", "5", "a");
            StatusTestWorld.Apply(sim, 0, 2, 1, StatusKind.Slowed, "0.5", "1", "b");
            Unit unit = null!;
            for (int tick = 0; tick < 110; tick++)
            {
                sim.Step();
                unit = StatusTestWorld.UnitOf(sim, 1);
                Fix64 expected = tick < 20 ? Fix64.Parse("0.5") : tick < 100 ? Fix64.Parse("0.75") : Fix64.One;
                Assert.That(unit.Statuses.SpeedMultiplier, Is.EqualTo(expected), "SpeedMultiplier on tick " + tick);
                Assert.That(unit.StatusSpeedMultiplier, Is.EqualTo(expected), "Unit.StatusSpeedMultiplier on tick " + tick);
            }
            Assert.That(unit.Statuses.IsSlowed, Is.False);
        }

        /// <summary>6. The same key refreshes: one entry, the new multiplier, and the later of the two end ticks.</summary>
        [Test]
        public void Slow_SameKeyRefreshes()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
            StatusTestWorld.Apply(sim, 0, 1, 1, StatusKind.Slowed, "0.5", "2", "a");
            StatusTestWorld.Apply(sim, 10, 0, 1, StatusKind.Slowed, "0.75", "0.5", "a");
            StatusTestWorld.StepThrough(sim, 10);
            Unit unit = StatusTestWorld.UnitOf(sim, 1);
            Assert.That(unit.Statuses.Slows.Count, Is.EqualTo(1));
            Assert.That(unit.Statuses.Slows[0].Multiplier, Is.EqualTo(Fix64.Parse("0.75")));
            Assert.That(unit.Statuses.Slows[0].UntilTick, Is.EqualTo(40));
            Assert.That(unit.StatusSpeedMultiplier, Is.EqualTo(Fix64.Parse("0.75")));
            Assert.That(StatusTestWorld.EventsOf<StatusAppliedEvent>(sim)[0].Refreshed, Is.True);
        }

        /// <summary>7. With a cap of 4, a fifth key evicts the entry that ends soonest (a), leaving b, c, d, e in key order.</summary>
        [Test]
        public void Slow_CapEvictsTheEarliestEnding()
        {
            Simulation sim = StatusTestWorld.Build();
            Assert.That(sim.Tuning.Status.MaxSlowSourcesPerUnit, Is.EqualTo(4));
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
            // Durations 0.5, 1, 1.5, 2 and 2.5 s give UntilTick 10, 20, 30, 40, 50.
            StatusTestWorld.Apply(sim, 0, 1, 1, StatusKind.Slowed, "0.9", "0.5", "a");
            StatusTestWorld.Apply(sim, 0, 2, 1, StatusKind.Slowed, "0.9", "1", "b");
            StatusTestWorld.Apply(sim, 0, 3, 1, StatusKind.Slowed, "0.9", "1.5", "c");
            StatusTestWorld.Apply(sim, 0, 4, 1, StatusKind.Slowed, "0.9", "2", "d");
            StatusTestWorld.Apply(sim, 0, 5, 1, StatusKind.Slowed, "0.9", "2.5", "e");
            sim.Step();
            Unit unit = StatusTestWorld.UnitOf(sim, 1);
            List<string> keys = new List<string>();
            foreach (SlowEntry entry in unit.Statuses.Slows)
            {
                keys.Add(entry.SourceKey);
            }
            Assert.That(keys, Is.EqualTo(new List<string> { "b", "c", "d", "e" }));
            Assert.That(unit.Statuses.Slows[3].UntilTick, Is.EqualTo(50));
        }

        /// <summary>8. A drench weapon hitting an air unit on ticks 0, 1 and 2 slows it and grounds it on the third hit (until 2 + 80), resetting the counter.</summary>
        [Test]
        public void Drench_GroundsOnTheThirdHit()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, StatusTestWorld.DrencherId, StatusTestWorld.P0, Start);
            StatusTestWorld.Spawn(sim, 0, 1, TestWorld.FlyerId, StatusTestWorld.P1, StatusTestWorld.Centre(12, 10));
            sim.Step();
            Unit flyer = StatusTestWorld.UnitOf(sim, 2);
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(1));
            sim.Step();
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(2));
            sim.Step();
            Assert.That(flyer.Statuses.GroundedUntilTick, Is.EqualTo(82));
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(0));
            Assert.That(flyer.Statuses.Slows.Count, Is.EqualTo(1));
            Assert.That(flyer.Statuses.Slows[0].SourceKey, Is.EqualTo("drench"));
            Assert.That(flyer.Statuses.Slows[0].Multiplier, Is.EqualTo(Fix64.Parse("0.75")));
            Assert.That(flyer.Statuses.Slows[0].UntilTick, Is.EqualTo(42));
            Assert.That(flyer.Statuses.Slows[0].SourcePlayer, Is.EqualTo(StatusTestWorld.P0));
            List<DrenchHitEvent> hits = StatusTestWorld.EventsOf<DrenchHitEvent>(sim);
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Hits, Is.EqualTo(3));
            Assert.That(hits[0].Grounded, Is.True);
            Assert.That(hits[0].Attacker, Is.EqualTo(new EntityId(1)));
            Assert.That(hits[0].Target, Is.EqualTo(new EntityId(2)));
            // The drencher keeps shooting the now grounded flyer, which is no longer a drench target: no more drench hits, no re-grounding.
            for (int tick = 3; tick < 20; tick++)
            {
                sim.Step();
                Assert.That(StatusTestWorld.EventsOf<DrenchHitEvent>(sim), Is.Empty, "tick " + tick);
            }
            Assert.That(flyer.Statuses.GroundedUntilTick, Is.EqualTo(82));
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(0));
        }

        /// <summary>9. Hits on ticks 0, 50 and 160: the counter resets at the start of tick 150 (100 ticks after the hit on 50), so tick 160 counts 1 and nothing grounds.</summary>
        [Test]
        public void Drench_CounterTimesOut()
        {
            ScriptedDrenchMechanic scripted = new ScriptedDrenchMechanic().Hit(0, 1, 2).Hit(50, 1, 2).Hit(160, 1, 2);
            Simulation sim = StatusTestWorld.Build(players: DriverPlayers(), scripted: scripted);
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
            StatusTestWorld.Spawn(sim, 0, 1, TestWorld.FlyerId, StatusTestWorld.P1, StatusTestWorld.Centre(12, 10));
            Unit flyer = null!;
            StatusTestWorld.StepThrough(sim, 49);
            flyer = StatusTestWorld.UnitOf(sim, 2);
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(1));
            StatusTestWorld.StepThrough(sim, 50);
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(2));
            StatusTestWorld.StepThrough(sim, 149);
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(2), "still remembered on tick 149");
            StatusTestWorld.StepThrough(sim, 150);
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(0), "reset at the start of tick 150");
            Assert.That(flyer.Statuses.LastDrenchTick, Is.EqualTo(-1));
            StatusTestWorld.StepThrough(sim, 160);
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(1));
            Assert.That(flyer.Statuses.IsGrounded, Is.False);
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Air));
        }

        /// <summary>10. A drench hit on a ground unit, or on an already Grounded flyer, changes nothing and emits nothing.</summary>
        [Test]
        public void Drench_NeedsAFlyer()
        {
            ScriptedDrenchMechanic scripted = new ScriptedDrenchMechanic().Hit(5, 1, 2).Hit(20, 1, 3);
            Simulation sim = StatusTestWorld.Build(players: DriverPlayers(), scripted: scripted);
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
            StatusTestWorld.Spawn(sim, 0, 1, TestWorld.RunnerId, StatusTestWorld.P1, StatusTestWorld.Centre(12, 10));
            StatusTestWorld.Spawn(sim, 0, 2, TestWorld.FlyerId, StatusTestWorld.P1, StatusTestWorld.Centre(14, 10));
            StatusTestWorld.Apply(sim, 10, 0, 3, StatusKind.Grounded, "1", "0");
            StatusTestWorld.StepThrough(sim, 4);
            StatusTestWorld.StepThrough(sim, 5); // hit on the ground unit
            Unit ground = StatusTestWorld.UnitOf(sim, 2);
            Assert.That(ground.Statuses.DrenchHits, Is.EqualTo(0));
            Assert.That(ground.Statuses.LastDrenchTick, Is.EqualTo(-1));
            Assert.That(ground.Statuses.IsSlowed, Is.False);
            Assert.That(StatusTestWorld.EventsOf<DrenchHitEvent>(sim), Is.Empty);
            Assert.That(StatusTestWorld.EventsOf<StatusAppliedEvent>(sim), Is.Empty);
            StatusTestWorld.StepThrough(sim, 19);
            StatusTestWorld.StepThrough(sim, 20); // hit on the grounded flyer
            Unit flyer = StatusTestWorld.UnitOf(sim, 3);
            Assert.That(flyer.Statuses.IsGrounded, Is.True);
            Assert.That(flyer.Statuses.DrenchHits, Is.EqualTo(0));
            Assert.That(flyer.Statuses.IsSlowed, Is.False);
            Assert.That(StatusTestWorld.EventsOf<DrenchHitEvent>(sim), Is.Empty);
            Assert.That(StatusTestWorld.EventsOf<StatusAppliedEvent>(sim), Is.Empty);
        }

        /// <summary>11. A flyer grounded over a Wall tile lands on the first free tile of ring 1 in ascending index: (4.5, 4.5); with that and its row neighbours walled it takes the next lowest index.</summary>
        [Test]
        public void Landing_RelocatesOffABlockedTile()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Run(sim, 0, 0, s => s.Terrain.AddBlockers(s, s.Terrain.IndexOf(5, 5), TileBlockers.Wall));
            StatusTestWorld.Spawn(sim, 0, 1, TestWorld.FlyerId, StatusTestWorld.P0, StatusTestWorld.Centre(5, 5));
            StatusTestWorld.Apply(sim, 0, 2, 1, StatusKind.Grounded, "1", "0");
            sim.Step();
            Unit flyer = StatusTestWorld.UnitOf(sim, 1);
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Ground));
            Assert.That(flyer.Position, Is.EqualTo(StatusTestWorld.Centre(4, 4)));
            UnitLayerChangedEvent changed = StatusTestWorld.EventsOf<UnitLayerChangedEvent>(sim)[0];
            Assert.That(changed.From, Is.EqualTo(MovementLayer.Air));
            Assert.That(changed.To, Is.EqualTo(MovementLayer.Ground));
            Assert.That(changed.Position, Is.EqualTo(StatusTestWorld.Centre(4, 4)));

            // Second flyer: (4,4), (5,4) and (6,4) also walled, so the first free ring-1 tile in ascending index is (4,5).
            Simulation second = StatusTestWorld.Build();
            StatusTestWorld.Run(second, 0, 0, s =>
            {
                foreach (int x in new[] { 4, 5, 6 })
                {
                    s.Terrain.AddBlockers(s, s.Terrain.IndexOf(x, 4), TileBlockers.Wall);
                }
                s.Terrain.AddBlockers(s, s.Terrain.IndexOf(5, 5), TileBlockers.Wall);
            });
            StatusTestWorld.Spawn(second, 0, 1, TestWorld.FlyerId, StatusTestWorld.P0, StatusTestWorld.Centre(5, 5));
            StatusTestWorld.Apply(second, 0, 2, 1, StatusKind.Grounded, "1", "0");
            second.Step();
            Assert.That(StatusTestWorld.UnitOf(second, 1).Position, Is.EqualTo(StatusTestWorld.Centre(4, 5)));
        }

        /// <summary>Landing leaves the position alone when the flyer is over free ground, and when no tile inside the search radius is free.</summary>
        [Test]
        public void Landing_StaysPutOverFreeGroundOrWhenNothingIsFree()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.FlyerId, StatusTestWorld.P0, StatusTestWorld.Centre(5, 5));
            StatusTestWorld.Apply(sim, 0, 1, 1, StatusKind.Grounded, "1", "0");
            sim.Step();
            Assert.That(StatusTestWorld.UnitOf(sim, 1).Position, Is.EqualTo(StatusTestWorld.Centre(5, 5)));

            // Wall every tile within 3 tiles of (30, 30), the default search radius: no landing tile exists.
            Simulation walled = StatusTestWorld.Build();
            Assert.That(walled.Tuning.Status.GroundedLandingSearchRadiusTiles, Is.EqualTo(3));
            StatusTestWorld.Run(walled, 0, 0, s =>
            {
                for (int y = 27; y <= 33; y++)
                {
                    for (int x = 27; x <= 33; x++)
                    {
                        s.Terrain.AddBlockers(s, s.Terrain.IndexOf(x, y), TileBlockers.Wall);
                    }
                }
            });
            StatusTestWorld.Spawn(walled, 0, 1, TestWorld.FlyerId, StatusTestWorld.P0, StatusTestWorld.Centre(30, 30));
            StatusTestWorld.Apply(walled, 0, 2, 1, StatusKind.Grounded, "1", "0");
            walled.Step();
            Unit flyer = StatusTestWorld.UnitOf(walled, 1);
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Ground));
            Assert.That(flyer.Position, Is.EqualTo(StatusTestWorld.Centre(30, 30)));
        }

        /// <summary>
        /// 12. A wall across a 16 by 16 map blocks a Grounded flyer (X stays below 8 and a move_unreachable event is emitted
        /// while grounded); the ungrounded flyer crosses; the two runs hash differently and two identical runs hash equally.
        /// </summary>
        [Test]
        public void Walls_BlockAGroundedFlyer()
        {
            ulong groundedHash = RunWallScenario(true, out bool unreachable, out Fix64 maxX);
            Assert.That(unreachable, Is.True, "a MoveUnreachableEvent is emitted for the grounded flyer");
            Assert.That(maxX, Is.LessThan(Fix64.FromInt(8)), "the grounded flyer never reaches the wall column");
            ulong freeHash = RunWallScenario(false, out bool freeUnreachable, out Fix64 freeMaxX);
            Assert.That(freeUnreachable, Is.False);
            Assert.That(freeMaxX, Is.GreaterThan(Fix64.FromInt(8)), "without the status the flyer crosses the wall column");
            Assert.That(freeHash, Is.Not.EqualTo(groundedHash));
            Assert.That(RunWallScenario(true, out _, out _), Is.EqualTo(groundedHash), "identical runs hash identically");
        }

        /// <summary>Runs the wall scenario for 80 ticks (0 to 79) and returns the final state hash.</summary>
        private static ulong RunWallScenario(bool grounded, out bool unreachable, out Fix64 maxX)
        {
            MapDefinition map = new MapDefinition("test_wall", "Test Wall", 16, 16,
                new List<FixVec2> { FixVec2.FromInts(1, 1), FixVec2.FromInts(14, 14) });
            Simulation sim = StatusTestWorld.Build(map: map);
            StatusTestWorld.Run(sim, 0, 0, s =>
            {
                for (int y = 0; y < 16; y++)
                {
                    s.Terrain.AddBlockers(s, s.Terrain.IndexOf(8, y), TileBlockers.Wall);
                }
            });
            StatusTestWorld.Spawn(sim, 0, 1, TestWorld.FlyerId, StatusTestWorld.P0, StatusTestWorld.Centre(4, 8));
            StatusTestWorld.Move(sim, 0, 2, 1, StatusTestWorld.Centre(12, 8));
            if (grounded)
            {
                StatusTestWorld.Apply(sim, 0, 3, 1, StatusKind.Grounded, "1", "0");
            }
            unreachable = false;
            maxX = Fix64.Zero;
            for (int tick = 0; tick < 80; tick++)
            {
                sim.Step();
                unreachable |= StatusTestWorld.EventsOf<MoveUnreachableEvent>(sim).Count > 0;
                maxX = Fix64.Max(maxX, StatusTestWorld.UnitOf(sim, 1).Position.X);
            }
            return sim.ComputeStateHash();
        }

        /// <summary>Statuses are part of the state hash: a slow changes it, and two identical runs agree.</summary>
        [Test]
        public void Statuses_AreHashed()
        {
            ulong Run(bool slowed)
            {
                Simulation sim = StatusTestWorld.Build();
                StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
                if (slowed)
                {
                    StatusTestWorld.Apply(sim, 3, 0, 1, StatusKind.Slowed, "0.9", "1");
                }
                StatusTestWorld.StepThrough(sim, 5);
                return sim.ComputeStateHash();
            }
            Assert.That(Run(true), Is.Not.EqualTo(Run(false)));
            Assert.That(Run(true), Is.EqualTo(Run(true)));
        }

        /// <summary>apply_status rejections: script issuer only, known unit, valid duration and multiplier (in that order).</summary>
        [Test]
        public void ApplyStatusCommand_Rejections()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Start);
            sim.Submit(new ApplyStatusCommand(1, StatusTestWorld.P0, 0, new EntityId(1), StatusKind.Slowed, Fix64.Half, Fix64.One, "x"));
            sim.Submit(new ApplyStatusCommand(2, PlayerId.None, 0, new EntityId(99), StatusKind.Slowed, Fix64.Half, Fix64.One, "x"));
            sim.Submit(new ApplyStatusCommand(3, PlayerId.None, 0, new EntityId(1), StatusKind.Slowed, Fix64.Half, Fix64.Zero, "x"));
            sim.Submit(new ApplyStatusCommand(4, PlayerId.None, 0, new EntityId(1), StatusKind.Slowed, Fix64.Half, Fix64.Parse("0.01"), "x"));
            sim.Submit(new ApplyStatusCommand(5, PlayerId.None, 0, new EntityId(1), StatusKind.Slowed, Fix64.Zero, Fix64.One, "x"));
            sim.Submit(new ApplyStatusCommand(6, PlayerId.None, 0, new EntityId(1), StatusKind.Slowed, Fix64.Parse("1.5"), Fix64.One, "x"));
            sim.Submit(new ApplyStatusCommand(7, PlayerId.None, 0, new EntityId(1), StatusKind.Grounded, Fix64.One, Fix64.Parse("-1"), "x"));
            string[] expected = { "status_requires_script_issuer", "unknown_unit", "invalid_duration", "invalid_duration", "invalid_multiplier", "invalid_multiplier", "invalid_duration" };
            sim.Step();
            for (int tick = 1; tick <= 7; tick++)
            {
                sim.Step();
                List<CommandRejectedEvent> rejected = StatusTestWorld.EventsOf<CommandRejectedEvent>(sim);
                Assert.That(rejected.Count, Is.EqualTo(1), "tick " + tick);
                Assert.That(rejected[0].Reason, Is.EqualTo(expected[tick - 1]), "tick " + tick);
            }
            Assert.That(StatusTestWorld.UnitOf(sim, 1).Statuses.HasAnyState, Is.False);
            // A malformed source key never reaches a step.
            Expect.Throws<System.ArgumentException>(() => new ApplyStatusCommand(0, PlayerId.None, 0, new EntityId(1), StatusKind.Slowed, Fix64.Half, Fix64.One, "Bad Key"));
        }

        /// <summary>The apply methods refuse to run outside a step and reject out-of-range arguments.</summary>
        [Test]
        public void StatusSystem_ValidatesArguments()
        {
            Simulation sim = StatusTestWorld.Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.FlyerId, StatusTestWorld.P0, Start);
            sim.Step();
            Unit flyer = StatusTestWorld.UnitOf(sim, 1);
            Expect.Throws<System.InvalidOperationException>(() => StatusSystem.ApplySlow(sim, flyer, "a", Fix64.Half, 5, PlayerId.None));
            Expect.Throws<System.InvalidOperationException>(() => StatusSystem.ApplyGrounded(sim, flyer, 5, "a", PlayerId.None));
            int caught = 0;
            StatusTestWorld.Run(sim, 1, 0, s =>
            {
                try { StatusSystem.ApplySlow(s, flyer, "a", Fix64.Zero, 5, PlayerId.None); } catch (System.ArgumentOutOfRangeException) { caught++; }
                try { StatusSystem.ApplySlow(s, flyer, "a", Fix64.FromInt(2), 5, PlayerId.None); } catch (System.ArgumentOutOfRangeException) { caught++; }
                try { StatusSystem.ApplySlow(s, flyer, "a", Fix64.Half, 0, PlayerId.None); } catch (System.ArgumentOutOfRangeException) { caught++; }
                try { StatusSystem.ApplySlow(s, flyer, "BAD", Fix64.Half, 5, PlayerId.None); } catch (System.ArgumentException) { caught++; }
            });
            sim.Step();
            Assert.That(caught, Is.EqualTo(4));
            Assert.That(flyer.Statuses.IsSlowed, Is.False);
        }

        /// <summary>The apply_status script command parses into an ApplyStatusCommand with defaults for the optional fields, and bad values name the field.</summary>
        [Test]
        public void Script_ParsesApplyStatus()
        {
            const string Source = "test/status_script.json";
            string Wrap(string command) => "{ \"$schema_version\": 1, \"seed\": 7, \"ticks\": 10, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"skai\" } ], \"commands\": [ " + command + " ] }";
            CommandScript script = CommandScript.Parse(Wrap(
                "{ \"type\": \"apply_status\", \"tick\": 3, \"issuer\": 255, \"sequence\": 1, \"unit\": 2, \"status\": \"slowed\", \"multiplier\": \"0.5\", \"seconds\": 2, \"source\": \"bog\" },"
                + " { \"type\": \"apply_status\", \"tick\": 4, \"issuer\": 255, \"sequence\": 2, \"unit\": 2, \"status\": \"grounded\" }"), Source);
            ApplyStatusCommand slow = (ApplyStatusCommand)script.Commands[0];
            Assert.That(slow.Status, Is.EqualTo(StatusKind.Slowed));
            Assert.That(slow.Multiplier, Is.EqualTo(Fix64.Half));
            Assert.That(slow.DurationSeconds, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(slow.SourceKey, Is.EqualTo("bog"));
            Assert.That(slow.Unit, Is.EqualTo(new EntityId(2)));
            ApplyStatusCommand ground = (ApplyStatusCommand)script.Commands[1];
            Assert.That(ground.Status, Is.EqualTo(StatusKind.Grounded));
            Assert.That(ground.DurationSeconds, Is.EqualTo(Fix64.Zero));
            Assert.That(ground.SourceKey, Is.EqualTo("script"));
            DataValidationException bad = Expect.Throws<DataValidationException>(() => CommandScript.Parse(Wrap(
                "{ \"type\": \"apply_status\", \"tick\": 3, \"issuer\": 255, \"sequence\": 1, \"unit\": 2, \"status\": \"frozen\" }"), Source));
            Assert.That(bad.Message, Does.StartWith(Source + ": "));
            Assert.That(bad.Message, Does.Contain("status"));
        }

        /// <summary>The five new statuses.json keys load with the documented defaults and are range checked with the file and field named.</summary>
        [Test]
        public void Data_NewStatusKeysLoadAndValidate()
        {
            StatusTuning status = TestData.LoadAll().Tuning.Status;
            Assert.That(status.SpoutlingDrenchResetSeconds, Is.EqualTo(Fix64.FromInt(5)));
            Assert.That(status.DrenchSlowMultiplier, Is.EqualTo(Fix64.Parse("0.75")));
            Assert.That(status.DrenchSlowDurationSeconds, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(status.GroundedLandingSearchRadiusTiles, Is.EqualTo(3));
            Assert.That(status.MaxSlowSourcesPerUnit, Is.EqualTo(4));
            MatchClock clock = new MatchClock(20);
            Assert.That(status.GroundedDurationTicks(clock), Is.EqualTo(80));
            Assert.That(status.DrenchResetTicks(clock), Is.EqualTo(100));
            Assert.That(status.DrenchSlowDurationTicks(clock), Is.EqualTo(40));

            AssertRejected("\"drench_slow_multiplier\": 0.75", "\"drench_slow_multiplier\": 0", "drench_slow_multiplier");
            AssertRejected("\"drench_slow_multiplier\": 0.75", "\"drench_slow_multiplier\": 1.5", "drench_slow_multiplier");
            AssertRejected("\"spoutling_drench_reset_seconds\": 5", "\"spoutling_drench_reset_seconds\": 0.1", "spoutling_drench_reset_seconds");
            AssertRejected("\"drench_slow_duration_seconds\": 2", "\"drench_slow_duration_seconds\": 11", "drench_slow_duration_seconds");
            AssertRejected("\"grounded_landing_search_radius_tiles\": 3", "\"grounded_landing_search_radius_tiles\": 9", "grounded_landing_search_radius_tiles");
            AssertRejected("\"max_slow_sources_per_unit\": 4", "\"max_slow_sources_per_unit\": 0", "max_slow_sources_per_unit");
        }

        /// <summary>Loads the data with <paramref name="oldText"/> replaced in statuses.json and asserts the loader names the file and field.</summary>
        private static void AssertRejected(string oldText, string newText, string field)
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit(StatusTuning.FilePath, oldText, newText)));
            Assert.That(ex.Message, Does.Contain(StatusTuning.FilePath));
            Assert.That(ex.Message, Does.Contain("field '" + field + "'"));
        }

        /// <summary>The shipped Spoutling has applies_drench; the other shipped weapons do not.</summary>
        [Test]
        public void Data_OnlyTheSpoutlingAppliesDrench()
        {
            ContentCatalog content = TestData.LoadAll().Content;
            Assert.That(content.Units["water.spoutling"].Weapon!.AppliesDrench, Is.True);
            foreach (KeyValuePair<string, UnitDefinition> pair in content.Units)
            {
                if (pair.Key != "water.spoutling" && pair.Value.Weapon != null)
                {
                    Assert.That(pair.Value.Weapon.AppliesDrench, Is.False, pair.Key);
                }
            }
        }

        /// <summary>Players for tests that need the scripted drench mechanic: P0 drives it, P1 is Skai (enemy of P0).</summary>
        private static List<PlayerSetup> DriverPlayers()
        {
            return new List<PlayerSetup>
            {
                new PlayerSetup(StatusTestWorld.P0, new TeamId(0), StatusTestWorld.DriverFactionId, "Driver"),
                new PlayerSetup(StatusTestWorld.P1, new TeamId(1), TestWorld.SkaiFactionId, "Skai"),
            };
        }
    }
}

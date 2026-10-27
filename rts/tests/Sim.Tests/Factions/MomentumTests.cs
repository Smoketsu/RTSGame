// rts/tests/Sim.Tests/Factions/MomentumTests.cs
// Acceptance tests of the Skai Momentum mechanic (rts/docs/specs/momentum.md section 7, tests 1 to 11) plus checks that
// the modifiers are registered, entries follow units, and terrain slows do not count as Slowed. Part of Rts.Sim.Tests.
// Setup: the flat 64 by 64 map, P0 = Skai (team 0) and P1 = a water-faction player (team 1), a ground test unit of speed 2
// spawned at (2.5, 10.5) and ordered at tick 0 to (60.5, 10.5) unless a test says otherwise. 20 ticks per second.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Combat;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Skai;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Tests.Statuses;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Factions
{
    /// <summary>Momentum gain, bonuses, drain, loss and determinism.</summary>
    [TestFixture]
    public sealed class MomentumTests
    {
        /// <summary>Where the test unit starts. Unit: tiles.</summary>
        private static readonly FixVec2 Spawn = new FixVec2(Fix64.FromInt(2) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half);

        /// <summary>Where the test unit is ordered to walk. Unit: tiles.</summary>
        private static readonly FixVec2 Goal = new FixVec2(Fix64.FromInt(60) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half);

        /// <summary>Speed bonus per stack of the shipped tuning, 4%.</summary>
        private static readonly Fix64 Bonus = Fix64.Parse("0.04");

        /// <summary>P0 is Skai, P1 is the water faction (mechanic "flooding").</summary>
        private static List<PlayerSetup> Players()
        {
            return new List<PlayerSetup>
            {
                new PlayerSetup(StatusTestWorld.P0, new TeamId(0), TestWorld.SkaiFactionId, "Skai"),
                new PlayerSetup(StatusTestWorld.P1, new TeamId(1), StatusTestWorld.WaterFactionId, "Water"),
            };
        }

        /// <summary>A started match with the test players and optionally custom tuning.</summary>
        private static Simulation Build(Tuning? tuning = null)
        {
            return StatusTestWorld.Build(tuning: tuning, players: Players());
        }

        /// <summary>Tuning loaded from the real files with one text replaced in momentum.json.</summary>
        private static Tuning TuningWith(string oldText, string newText)
        {
            return DataLoader.Load(TestData.WithEdit(MomentumTuning.FilePath, oldText, newText)).Tuning;
        }

        /// <summary>Spawns the walker for P0 (unit 1) and orders it to the goal at tick 0.</summary>
        private static void SpawnWalker(Simulation sim, string definition = TestWorld.RunnerId)
        {
            StatusTestWorld.Spawn(sim, 0, 0, definition, StatusTestWorld.P0, Spawn);
            StatusTestWorld.Move(sim, 0, 1, 1, Goal);
        }

        /// <summary>The Momentum mechanic of a match.</summary>
        private static MomentumMechanic Mechanic(Simulation sim)
        {
            return (MomentumMechanic)sim.Mechanics[MomentumMechanic.MechanicId];
        }

        /// <summary>Stacks of a unit now (fails the test when it has no entry).</summary>
        private static int Stacks(Simulation sim, uint unit)
        {
            Assert.That(Mechanic(sim).TryGetState(new EntityId(unit), out MomentumState? state), Is.True, "unit " + unit + " should have a Momentum entry");
            return state!.Stacks;
        }

        /// <summary>1. Tick constants: 20, 30 and 20 at the defaults; 15 for 2 stacks per second and 45 for a 1.5 s delay at 30 ticks per second.</summary>
        [Test]
        public void Conversion_FromSecondsToTicks()
        {
            MomentumRates defaults = new MomentumRates(TestWorld.Tuning.Momentum, new MatchClock(20));
            Assert.That(defaults.GainTicksPerStack, Is.EqualTo(20));
            Assert.That(defaults.DrainDelayTicks, Is.EqualTo(30));
            Assert.That(defaults.DrainTicksPerStack, Is.EqualTo(20));
            Assert.That(defaults.MaxStacks, Is.EqualTo(5));
            Assert.That(defaults.SpeedBonusPerStack, Is.EqualTo(Bonus));
            Assert.That(defaults.LostOnGrounded, Is.True);
            Assert.That(defaults.LostOnSlowed, Is.True);

            Tuning twoPerSecond = TuningWith("\"stack_gain_per_second\": 1", "\"stack_gain_per_second\": 2");
            Assert.That(new MomentumRates(twoPerSecond.Momentum, new MatchClock(30)).GainTicksPerStack, Is.EqualTo(15));
            Assert.That(new MomentumRates(TestWorld.Tuning.Momentum, new MatchClock(30)).DrainDelayTicks, Is.EqualTo(45));

            // 0.3 stacks per second: One / 0.3 is truncated, then rounded to the nearest tick: 67 at 20 ticks per second.
            Tuning slow = TuningWith("\"stack_gain_per_second\": 1", "\"stack_gain_per_second\": 0.3");
            Assert.That(new MomentumRates(slow.Momentum, new MatchClock(20)).GainTicksPerStack, Is.EqualTo(67));

            // The running mechanic exposes the same constants.
            Assert.That(Mechanic(Build()).Rates.GainTicksPerStack, Is.EqualTo(20));
        }

        /// <summary>2. Stacks: 0 after 19 steps, 1 after 20, 4 after 80, 5 after 100 and 5 after 200, with exactly five momentum_changed events of increasing stacks.</summary>
        [Test]
        public void Gain_OneStackPerSecondUpToTheCap()
        {
            Simulation sim = Build();
            SpawnWalker(sim);
            List<MomentumChangedEvent> changes = new List<MomentumChangedEvent>();
            for (int steps = 1; steps <= 200; steps++)
            {
                sim.Step();
                changes.AddRange(StatusTestWorld.EventsOf<MomentumChangedEvent>(sim));
                if (steps == 19) { Assert.That(Stacks(sim, 1), Is.EqualTo(0)); }
                if (steps == 20) { Assert.That(Stacks(sim, 1), Is.EqualTo(1)); }
                if (steps == 80) { Assert.That(Stacks(sim, 1), Is.EqualTo(4)); }
                if (steps == 99) { Assert.That(Stacks(sim, 1), Is.EqualTo(4)); }
                if (steps == 100) { Assert.That(Stacks(sim, 1), Is.EqualTo(5)); }
            }
            Assert.That(Stacks(sim, 1), Is.EqualTo(5));
            Assert.That(changes.Count, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(changes[i].PreviousStacks, Is.EqualTo(i));
                Assert.That(changes[i].Stacks, Is.EqualTo(i + 1));
                Assert.That(changes[i].Unit, Is.EqualTo(new EntityId(1)));
            }
            Assert.That(changes[0].Tick, Is.EqualTo(19), "the first stack is gained on the 20th moving tick (tick 19)");
        }

        /// <summary>3. The advance on tick 20 uses one stack's bonus and the advance at five stacks uses five; tick 19 still advances at base speed.</summary>
        [Test]
        public void SpeedBonus_FollowsTheStacks()
        {
            Simulation sim = Build();
            SpawnWalker(sim);
            Fix64 previousX = Spawn.X;
            Fix64 advanceOn19 = Fix64.Zero;
            Fix64 advanceOn20 = Fix64.Zero;
            Fix64 advanceOn100 = Fix64.Zero;
            for (int tick = 0; tick <= 100; tick++)
            {
                sim.Step();
                Fix64 x = StatusTestWorld.UnitOf(sim, 1).Position.X;
                if (tick == 19) { advanceOn19 = x - previousX; }
                if (tick == 20) { advanceOn20 = x - previousX; }
                if (tick == 100) { advanceOn100 = x - previousX; }
                previousX = x;
            }
            Assert.That(advanceOn19, Is.EqualTo(sim.Clock.PerSecondToPerTick(Fix64.FromInt(2))));
            Assert.That(advanceOn20, Is.EqualTo(sim.Clock.PerSecondToPerTick(Fix64.FromInt(2) * (Fix64.One + Bonus * 1))));
            Assert.That(advanceOn100, Is.EqualTo(sim.Clock.PerSecondToPerTick(Fix64.FromInt(2) * (Fix64.One + Bonus * 5))));
        }

        /// <summary>4. A unit at 5 stacks that arrives on tick a keeps 5 through a+49, has 4 at a+50, 3 at a+70 and 0 at a+130.</summary>
        [Test]
        public void Drain_WaitsThenLosesOneStackPerSecond()
        {
            Simulation sim = Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Spawn);
            StatusTestWorld.Move(sim, 0, 1, 1, StatusTestWorld.Centre(40, 10));
            int arrival = -1;
            Dictionary<int, int> stacksAt = new Dictionary<int, int>();
            for (int tick = 0; tick < 700 && (arrival < 0 || tick <= arrival + 135); tick++)
            {
                sim.Step();
                if (arrival < 0 && StatusTestWorld.EventsOf<UnitArrivedEvent>(sim).Count > 0)
                {
                    arrival = tick;
                }
                stacksAt[tick] = Stacks(sim, 1);
            }
            Assert.That(arrival, Is.GreaterThan(100), "the unit has 5 stacks before it arrives");
            Assert.That(stacksAt[arrival], Is.EqualTo(5));
            Assert.That(stacksAt[arrival + 30], Is.EqualTo(5), "the 1.5 s delay: ticks a+1 to a+30 keep every stack");
            Assert.That(stacksAt[arrival + 49], Is.EqualTo(5));
            Assert.That(stacksAt[arrival + 50], Is.EqualTo(4));
            Assert.That(stacksAt[arrival + 69], Is.EqualTo(4));
            Assert.That(stacksAt[arrival + 70], Is.EqualTo(3));
            Assert.That(stacksAt[arrival + 129], Is.EqualTo(1));
            Assert.That(stacksAt[arrival + 130], Is.EqualTo(0));
            Assert.That(stacksAt[arrival + 135], Is.EqualTo(0));
        }

        /// <summary>5. Ten moving ticks, a five tick stop, then moving again: the first stack arrives on the 20th moving tick (tick 24), not 20 ticks after the stop.</summary>
        [Test]
        public void ShortStops_KeepProgress()
        {
            Simulation sim = Build();
            SpawnWalker(sim);
            sim.Submit(new StopCommand(10, PlayerId.None, 0, new EntityId(1)));
            StatusTestWorld.Move(sim, 15, 0, 1, Goal);
            for (int tick = 0; tick <= 24; tick++)
            {
                sim.Step();
                MomentumState state = StateOf(sim, 1);
                if (tick == 9) { Assert.That(state.GainTicks, Is.EqualTo(10)); }
                if (tick == 14)
                {
                    Assert.That(state.GainTicks, Is.EqualTo(10), "progress is kept while standing still");
                    Assert.That(state.StillTicks, Is.EqualTo(5));
                }
                if (tick == 23) { Assert.That(state.Stacks, Is.EqualTo(0)); }
                if (tick == 24) { Assert.That(state.Stacks, Is.EqualTo(1)); }
            }
        }

        /// <summary>6. A flyer at 5 stacks grounded at tick t loses them at once (one momentum_lost of 5, "grounded"), stays at 0 through t+79 while still moving, and holds 1 stack at the end of t+99.</summary>
        [Test]
        public void Lost_OnGrounded()
        {
            const int T = 120;
            Simulation sim = Build();
            SpawnWalker(sim, TestWorld.FlyerId);
            StatusTestWorld.Apply(sim, T, 0, 1, StatusKind.Grounded, "1", "0");
            StatusTestWorld.StepThrough(sim, T - 1);
            Assert.That(Stacks(sim, 1), Is.EqualTo(5));
            for (int tick = T; tick <= T + 99; tick++)
            {
                sim.Step();
                List<MomentumLostEvent> lost = StatusTestWorld.EventsOf<MomentumLostEvent>(sim);
                if (tick == T)
                {
                    Assert.That(lost.Count, Is.EqualTo(1));
                    Assert.That(lost[0].StacksLost, Is.EqualTo(5));
                    Assert.That(lost[0].Reason, Is.EqualTo("grounded"));
                }
                else
                {
                    Assert.That(lost, Is.Empty, "tick " + tick);
                }
                Assert.That(Stacks(sim, 1), Is.EqualTo(tick == T + 99 ? 1 : 0), "stacks at the end of tick " + tick);
            }
            Assert.That(StatusTestWorld.UnitOf(sim, 1).Statuses.IsGrounded, Is.False, "the status expired at tick t+80");
        }

        /// <summary>7. Slowed 0.75 at 5 stacks wipes them with reason "slowed"; with lost_on_slowed false the stacks stay and the movement chain multiplies status by Momentum.</summary>
        [Test]
        public void Lost_OnSlowed_SwitchRespected()
        {
            const int T = 120;
            Simulation sim = Build();
            SpawnWalker(sim);
            // Slowed for 2 s: active on ticks 120 to 159.
            StatusTestWorld.Apply(sim, T, 0, 1, StatusKind.Slowed, "0.75", "2", "bog");
            StatusTestWorld.StepThrough(sim, T);
            Assert.That(Stacks(sim, 1), Is.EqualTo(0));
            List<MomentumLostEvent> lost = StatusTestWorld.EventsOf<MomentumLostEvent>(sim);
            Assert.That(lost.Count, Is.EqualTo(1));
            Assert.That(lost[0].StacksLost, Is.EqualTo(5));
            Assert.That(lost[0].Reason, Is.EqualTo("slowed"));
            StatusTestWorld.StepThrough(sim, T + 39);
            Assert.That(Stacks(sim, 1), Is.EqualTo(0), "no gain while Slowed");
            StatusTestWorld.StepThrough(sim, T + 40 + 18);
            Assert.That(Stacks(sim, 1), Is.EqualTo(0));
            StatusTestWorld.StepThrough(sim, T + 40 + 19);
            Assert.That(Stacks(sim, 1), Is.EqualTo(1), "gain restarts when the slow expires on tick t+40");

            Simulation kept = Build(TuningWith("\"lost_on_slowed\": true", "\"lost_on_slowed\": false"));
            SpawnWalker(kept);
            StatusTestWorld.Apply(kept, T, 0, 1, StatusKind.Slowed, "0.75", "2", "bog");
            Fix64 previousX = Fix64.Zero;
            for (int tick = 0; tick <= T + 40; tick++)
            {
                kept.Step();
                Assert.That(StatusTestWorld.EventsOf<MomentumLostEvent>(kept), Is.Empty);
                if (tick == T + 1)
                {
                    // Chain order: ((base * terrain) * status) * momentum.
                    Fix64 expected = kept.Clock.PerSecondToPerTick(Fix64.FromInt(2) * Fix64.One * Fix64.Parse("0.75") * (Fix64.One + Bonus * 5));
                    Assert.That(StatusTestWorld.UnitOf(kept, 1).Position.X - previousX, Is.EqualTo(expected));
                }
                previousX = StatusTestWorld.UnitOf(kept, 1).Position.X;
            }
            Assert.That(Stacks(kept, 1), Is.EqualTo(5), "stacks survive the slow when lost_on_slowed is false");
            Assert.That(StateOf(kept, 1).GainTicks, Is.EqualTo(0));
        }

        /// <summary>8. Units of a non-Momentum faction, neutral units and speed-0 Skai units get no entry; their multipliers are One; entries follow live units.</summary>
        [Test]
        public void Eligibility_OnlyMobileUnitsOfTheMomentumFaction()
        {
            Simulation sim = Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Spawn);                  // unit 1: Skai, mobile
            StatusTestWorld.Spawn(sim, 0, 1, TestWorld.RunnerId, StatusTestWorld.P1, StatusTestWorld.Centre(2, 20)); // unit 2: water player
            StatusTestWorld.Spawn(sim, 0, 2, StatusTestWorld.StatueId, StatusTestWorld.P0, StatusTestWorld.Centre(2, 30)); // unit 3: Skai, speed 0
            StatusTestWorld.Spawn(sim, 0, 3, TestWorld.RunnerId, PlayerId.None, StatusTestWorld.Centre(2, 40));    // unit 4: neutral
            StatusTestWorld.Move(sim, 0, 4, 1, Goal);
            StatusTestWorld.Move(sim, 0, 5, 2, StatusTestWorld.Centre(60, 20));
            for (int steps = 0; steps < 120; steps++)
            {
                sim.Step();
            }
            MomentumMechanic mechanic = Mechanic(sim);
            Assert.That(mechanic.TryGetState(new EntityId(1), out _), Is.True);
            Assert.That(mechanic.TryGetState(new EntityId(2), out _), Is.False, "a water player's unit has no entry");
            Assert.That(mechanic.TryGetState(new EntityId(3), out _), Is.False, "a Skai unit with speed 0 has no entry");
            Assert.That(mechanic.TryGetState(new EntityId(4), out _), Is.False, "a neutral unit has no entry");
            Assert.That(mechanic.EntryCount, Is.EqualTo(1));
            foreach (uint id in new uint[] { 2, 3, 4 })
            {
                Unit unit = StatusTestWorld.UnitOf(sim, id);
                Assert.That(mechanic.SpeedMultiplier(sim, unit), Is.EqualTo(Fix64.One));
                Assert.That(mechanic.OutgoingDamageMultiplier(sim, unit), Is.EqualTo(Fix64.One));
            }
            Assert.That(StatusTestWorld.UnitOf(sim, 2).Position.X, Is.GreaterThan(Spawn.X + Fix64.FromInt(10)), "the water unit did move, it just earns nothing");
            // The mobile Skai unit has the bonus.
            Assert.That(mechanic.SpeedMultiplier(sim, StatusTestWorld.UnitOf(sim, 1)), Is.EqualTo(Fix64.One + Bonus * 5));
            // Removing the unit drops its entry on the next tick.
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, 0, new EntityId(1)));
            sim.Step();
            Assert.That(mechanic.EntryCount, Is.EqualTo(0));
        }

        /// <summary>The mechanic registers both modifiers under its own ID; a match without a Skai player registers none.</summary>
        [Test]
        public void RegistersItsModifiers_OnlyWhenTheMechanicIsInTheMatch()
        {
            Simulation sim = Build();
            Assert.That(SpeedIds(sim), Does.Contain("momentum"));
            Assert.That(DamageIds(sim), Does.Contain("momentum"));
            Assert.That(sim.SpeedModifiers[SpeedIds(sim).IndexOf("momentum")], Is.SameAs(Mechanic(sim)));
            Simulation noSkai = StatusTestWorld.Build(players: new List<PlayerSetup>
            {
                new PlayerSetup(StatusTestWorld.P0, new TeamId(0), TestWorld.TestFactionId, "A"),
                new PlayerSetup(StatusTestWorld.P1, new TeamId(1), StatusTestWorld.WaterFactionId, "B"),
            });
            Assert.That(SpeedIds(noSkai), Does.Not.Contain("momentum"));
            Assert.That(DamageIds(noSkai), Does.Not.Contain("momentum"));
        }

        /// <summary>IDs of the registered speed modifiers, in application order.</summary>
        private static List<string> SpeedIds(Simulation sim)
        {
            // IDs collected in order.
            List<string> ids = new List<string>();
            foreach (ISpeedModifier modifier in sim.SpeedModifiers)
            {
                ids.Add(modifier.Id);
            }
            return ids;
        }

        /// <summary>IDs of the registered damage modifiers, in application order.</summary>
        private static List<string> DamageIds(Simulation sim)
        {
            // IDs collected in order.
            List<string> ids = new List<string>();
            foreach (IDamageModifier modifier in sim.DamageModifiers)
            {
                ids.Add(modifier.Id);
            }
            return ids;
        }

        /// <summary>9. A Skai unit pushed around without a move order keeps counting still ticks and gains nothing; a unit with 3 stacks keeps them and its gain progress through pushes.</summary>
        [Test]
        public void Pushes_AreNotMoving()
        {
            Simulation sim = Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.RunnerId, StatusTestWorld.P0, Spawn);
            for (int tick = 1; tick < 25; tick++)
            {
                // Each tick after the spawn a "wave" shoves the unit half a tile east.
                StatusTestWorld.Run(sim, tick, 0, s => StatusTestWorld.UnitOf(s, 1).SetPosition(StatusTestWorld.UnitOf(s, 1).Position + new FixVec2(Fix64.Half, Fix64.Zero)));
            }
            StatusTestWorld.StepThrough(sim, 24);
            MomentumState state = StateOf(sim, 1);
            Assert.That(StatusTestWorld.UnitOf(sim, 1).Position.X, Is.GreaterThan(Spawn.X + Fix64.FromInt(10)));
            Assert.That(StatusTestWorld.UnitOf(sim, 1).LastSelfMoveTick, Is.EqualTo(-1));
            Assert.That(state.Stacks, Is.EqualTo(0));
            Assert.That(state.GainTicks, Is.EqualTo(0));
            Assert.That(state.StillTicks, Is.EqualTo(25), "ticks 0 to 24 all count as still");

            // 3 stacks earned by walking 60 ticks, then stopped and shoved: stacks and drain delay run as if the unit stood still.
            Simulation walker = Build();
            SpawnWalker(walker);
            walker.Submit(new StopCommand(60, PlayerId.None, 0, new EntityId(1)));
            for (int tick = 61; tick <= 80; tick++)
            {
                StatusTestWorld.Run(walker, tick, 0, s => StatusTestWorld.UnitOf(s, 1).SetPosition(StatusTestWorld.UnitOf(s, 1).Position + new FixVec2(Fix64.Half, Fix64.Zero)));
            }
            StatusTestWorld.StepThrough(walker, 80);
            MomentumState walkerState = StateOf(walker, 1);
            Assert.That(walkerState.Stacks, Is.EqualTo(3));
            Assert.That(walkerState.GainTicks, Is.EqualTo(0));
            Assert.That(walkerState.StillTicks, Is.EqualTo(21), "ticks 60 to 80 are still ticks");
        }

        /// <summary>10. At 3 stacks the damage multiplier is One + 0.04 * 3, and a hit of damage 10 against a 1.0 armour multiplier removes exactly 10 times that.</summary>
        [Test]
        public void DamageMultiplier_AppliesToHits()
        {
            Simulation sim = Build();
            StatusTestWorld.Spawn(sim, 0, 0, TestWorld.ShooterId, StatusTestWorld.P0, Spawn);
            StatusTestWorld.Move(sim, 0, 1, 1, Goal);
            sim.Submit(new StopCommand(60, PlayerId.None, 0, new EntityId(1)));
            StatusTestWorld.StepThrough(sim, 69);
            Unit shooter = StatusTestWorld.UnitOf(sim, 1);
            Assert.That(Stacks(sim, 1), Is.EqualTo(3), "60 moving ticks give 3 stacks, held through the drain delay");
            Fix64 multiplier = Fix64.One + Bonus * 3;
            Assert.That(Mechanic(sim).OutgoingDamageMultiplier(sim, shooter), Is.EqualTo(multiplier));
            // A light-armoured enemy appears 3 tiles in front of the shooter on tick 70 and is hit on that tick.
            StatusTestWorld.Spawn(sim, 70, 0, TestWorld.RunnerId, StatusTestWorld.P1, shooter.Position + new FixVec2(Fix64.FromInt(3), Fix64.Zero));
            sim.Step();
            List<UnitDamagedEvent> hits = StatusTestWorld.EventsOf<UnitDamagedEvent>(sim);
            Assert.That(hits.Count, Is.EqualTo(1));
            Fix64 expected = Fix64.FromInt(10) * multiplier;
            Assert.That(hits[0].Damage, Is.EqualTo(expected));
            Assert.That(StatusTestWorld.UnitOf(sim, 2).Health, Is.EqualTo(Fix64.FromInt(100) - expected));
            Assert.That(expected, Is.GreaterThan(Fix64.Parse("11.19")).And.LessThan(Fix64.Parse("11.21")));
        }

        /// <summary>11. Two identical runs hash equally at every hash tick; a run where one unit stops one tick earlier differs at the next hash tick.</summary>
        [Test]
        public void Determinism_AndHash()
        {
            List<ulong> a = HashRun(60);
            List<ulong> b = HashRun(60);
            List<ulong> earlier = HashRun(59);
            Assert.That(a.Count, Is.EqualTo(12));
            Assert.That(b, Is.EqualTo(a));
            // Hash ticks are 0, 10, 20, ...; list index i holds tick 10 * i. The stop on tick 59 instead of 60 first shows at tick 60.
            for (int i = 0; i < 6; i++)
            {
                Assert.That(earlier[i], Is.EqualTo(a[i]), "tick " + (10 * i));
            }
            Assert.That(earlier[6], Is.Not.EqualTo(a[6]), "tick 60");
        }

        /// <summary>Terrain slows (water) do not count as Slowed: a Skai unit walking through enemy water still gains stacks and has no Slowed status.</summary>
        [Test]
        public void TerrainSlow_IsNotSlowed()
        {
            Simulation sim = Build();
            StatusTestWorld.Run(sim, 0, 0, s =>
            {
                for (int y = 5; y <= 15; y++)
                {
                    for (int x = 0; x <= 30; x++)
                    {
                        s.Terrain.SetWater(s, s.Terrain.IndexOf(x, y), Fix64.One, new TeamId(1));
                    }
                }
            });
            StatusTestWorld.Spawn(sim, 0, 1, TestWorld.RunnerId, StatusTestWorld.P0, Spawn);
            StatusTestWorld.Move(sim, 0, 2, 1, Goal);
            sim.Step();
            Assert.That(sim.Terrain.SpeedMultiplier(sim.Terrain.IndexOf(2, 10), MovementLayer.Ground, new TeamId(0)), Is.LessThan(Fix64.One), "enemy water slows ground units");
            for (int steps = 1; steps < 100; steps++)
            {
                sim.Step();
            }
            Unit unit = StatusTestWorld.UnitOf(sim, 1);
            Assert.That(unit.Statuses.IsSlowed, Is.False);
            Assert.That(Stacks(sim, 1), Is.EqualTo(5), "the unit moved on all 100 ticks, so water's terrain slow does not stop the gain");
        }

        /// <summary>The mechanic state of a unit (fails the test when it has none).</summary>
        private static MomentumState StateOf(Simulation sim, uint unit)
        {
            Assert.That(Mechanic(sim).TryGetState(new EntityId(unit), out MomentumState? state), Is.True);
            return state!;
        }

        /// <summary>Runs 120 ticks with two walkers; unit 1 stops on <paramref name="stopTick"/>. Returns the state hash of every hash tick.</summary>
        private static List<ulong> HashRun(int stopTick)
        {
            Simulation sim = Build();
            SpawnWalker(sim);
            StatusTestWorld.Spawn(sim, 0, 2, TestWorld.RunnerId, StatusTestWorld.P0, StatusTestWorld.Centre(2, 20));
            StatusTestWorld.Move(sim, 0, 3, 2, StatusTestWorld.Centre(60, 20));
            sim.Submit(new StopCommand(stopTick, PlayerId.None, 0, new EntityId(1)));
            List<ulong> hashes = new List<ulong>();
            for (int tick = 0; tick < 120; tick++)
            {
                sim.Step();
                foreach (StateHashedEvent hashed in StatusTestWorld.EventsOf<StateHashedEvent>(sim))
                {
                    hashes.Add(hashed.Hash);
                }
            }
            return hashes;
        }
    }
}

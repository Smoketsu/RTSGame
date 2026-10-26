// rts/tests/Sim.Tests/Combat/CombatTests.cs
// Tests for the Phase 1 combat system: damage table, target acquisition, cooldown timing, death and its events,
// allies, weapon layers, attack-move, attack-unit and stop, plain moves, the damage modifier hook, determinism and the
// shipped unit data. Part of Rts.Sim.Tests. Uses the in-code fixtures of TestWorld (test.shooter and friends) except
// where a test says it loads rts/data.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Combat;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Combat
{
    /// <summary>Combat behaviour on small hand-built battles.</summary>
    [TestFixture]
    public class CombatTests
    {
        /// <summary>Player 0, team 0.</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1, team 1 (enemy of P0 in the default two-player world).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Next script sequence number for spawn commands. Unit: counter. Range: 0 upward; unique across the test run.</summary>
        private static uint _sequence;

        /// <summary>Offset added to every test coordinate so battles sit inside the 64 by 64 test map. Unit: tiles. Range: fixed 10.</summary>
        private const int Offset = 10;

        /// <summary>Point (x, y) shifted by <see cref="Offset"/> on both axes, in tiles.</summary>
        private static FixVec2 At(int x, int y)
        {
            return FixVec2.FromInts(x + Offset, y + Offset);
        }

        /// <summary>Spawns a unit through a script command and steps one tick (combat already runs on that tick). Returns the new unit's ID.</summary>
        private static EntityId Spawn(Simulation sim, string definition, PlayerId owner, int x, int y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, At(x, y)));
            sim.Step();
            // Spawn event of this tick, found by scanning the events.
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is UnitSpawnedEvent spawned)
                {
                    return spawned.Unit;
                }
            }
            throw new AssertionException("Spawn command produced no UnitSpawnedEvent.");
        }

        /// <summary>Steps <paramref name="ticks"/> ticks and returns every event of those ticks in order.</summary>
        private static List<SimEvent> StepCollect(Simulation sim, int ticks)
        {
            // Events gathered across the ticks.
            List<SimEvent> all = new List<SimEvent>();
            // Loop counter over the ticks. Unit: ticks. Range: 0 to ticks - 1.
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
                all.AddRange(sim.Events.Current);
            }
            return all;
        }

        /// <summary>Returns the damage events in <paramref name="events"/>.</summary>
        private static List<UnitDamagedEvent> Damage(List<SimEvent> events)
        {
            // Matching events in order.
            List<UnitDamagedEvent> found = new List<UnitDamagedEvent>();
            foreach (SimEvent evt in events)
            {
                if (evt is UnitDamagedEvent damaged)
                {
                    found.Add(damaged);
                }
            }
            return found;
        }

        /// <summary>The unit with this ID; fails the test when it is gone.</summary>
        private static Unit Get(Simulation sim, EntityId id)
        {
            Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True, id + " should be alive");
            return unit!;
        }

        /// <summary>The damage table decides the damage: kinetic is halved by armoured, blast is doubled; light takes 1 and 0.5.</summary>
        [Test]
        public void DamageTable_ScalesByDamageAndArmourType()
        {
            Simulation sim = TestWorld.Started();
            EntityId tank = Spawn(sim, TestWorld.TankId, P1, 10, 0);
            EntityId glassLike = Spawn(sim, TestWorld.FlyerId, P1, 10, 2);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 8, 0);
            // The shooter fires on its spawn tick; the events of that tick are still readable.
            List<SimEvent> events = new List<SimEvent>(sim.Events.Current);
            // The shooter (kinetic 10) hits the nearest enemy, the tank at distance 2 versus the flyer at 2.83; armoured halves it.
            List<UnitDamagedEvent> hits = Damage(events);
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Attacker, Is.EqualTo(shooter));
            Assert.That(hits[0].Target, Is.EqualTo(tank));
            Assert.That(hits[0].Damage, Is.EqualTo(Fix64.FromInt(5)));
            Assert.That(Get(sim, tank).Health, Is.EqualTo(Fix64.FromInt(95)));

            // A mortar (blast 20) hits the same tank for double and a light unit for half.
            Simulation second = TestWorld.Started();
            EntityId tank2 = Spawn(second, TestWorld.TankId, P1, 10, 0);
            Spawn(second, TestWorld.MortarId, P0, 8, 0);
            Assert.That(Get(second, tank2).Health, Is.EqualTo(Fix64.FromInt(60)));
            Simulation third = TestWorld.Started();
            EntityId light = Spawn(third, TestWorld.FlyerId, P1, 10, 0);
            Spawn(third, TestWorld.MortarId, P0, 8, 0);
            Assert.That(Get(third, light).Health, Is.EqualTo(Fix64.FromInt(90)));
            Assert.That(glassLike.IsValid, Is.True);
            // The shipped rules object answers the same lookups.
            Assert.That(TestWorld.Rules().GetMultiplier("blast", "armoured"), Is.EqualTo(Fix64.FromInt(2)));
            Expect.Throws<System.ArgumentException>(() => TestWorld.Rules().GetMultiplier("blast", "plasma"));
        }

        /// <summary>The nearest enemy in range is chosen, and equal distances go to the lowest entity ID.</summary>
        [Test]
        public void Acquisition_PicksNearestThenLowestId()
        {
            Simulation sim = TestWorld.Started();
            // Two enemies the same distance (4 tiles) from the shooter, one nearer enemy added later with a higher ID.
            EntityId first = Spawn(sim, TestWorld.TankId, P1, 4, 0);
            EntityId second = Spawn(sim, TestWorld.TankId, P1, -4, 0);
            Assert.That(first.Value, Is.LessThan(second.Value));
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            Assert.That(Get(sim, shooter).AttackTarget, Is.EqualTo(first), "tie goes to the lowest ID");

            Simulation other = TestWorld.Started();
            EntityId far = Spawn(other, TestWorld.TankId, P1, 4, 0);
            EntityId near = Spawn(other, TestWorld.TankId, P1, 0, 3);
            EntityId shooter2 = Spawn(other, TestWorld.ShooterId, P0, 0, 0);
            Assert.That(near.Value, Is.GreaterThan(far.Value));
            Assert.That(Get(other, shooter2).AttackTarget, Is.EqualTo(near), "the nearer enemy wins over a lower ID");

            // Out of reach: range 5 plus both radii (1.0) is 6; an enemy 6.5 away is ignored while idle.
            Simulation idle = TestWorld.Started();
            Spawn(idle, TestWorld.TankId, P1, 7, 0);
            EntityId lone = Spawn(idle, TestWorld.ShooterId, P0, 0, 0);
            StepCollect(idle, 5);
            Assert.That(Get(idle, lone).AttackTarget, Is.EqualTo(EntityId.None));
        }

        /// <summary>A 0.8 s cooldown is 16 ticks at 20 ticks per second: hits land exactly 16 ticks apart.</summary>
        [Test]
        public void Cooldown_SpacesHitsBySixteenTicks()
        {
            Simulation sim = TestWorld.Started();
            Spawn(sim, TestWorld.TankId, P1, 3, 0);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            // First hit happens on the spawn tick of the shooter.
            Assert.That(Get(sim, shooter).CooldownTicksRemaining, Is.EqualTo(16));
            // Ticks on which the shooter hit, gathered over 50 ticks.
            List<int> hitTicks = new List<int>();
            foreach (UnitDamagedEvent hit in Damage(StepCollect(sim, 50)))
            {
                hitTicks.Add(hit.Tick);
            }
            Assert.That(hitTicks.Count, Is.EqualTo(3));
            Assert.That(hitTicks[1] - hitTicks[0], Is.EqualTo(16));
            Assert.That(hitTicks[2] - hitTicks[1], Is.EqualTo(16));
            // The first of these hits is 16 ticks after the spawn-tick hit.
            Assert.That(hitTicks[0], Is.EqualTo(sim.Clock.Tick - 50 + 15));
        }

        /// <summary>A lethal hit emits damaged, killed, removed in that order, removes the unit, and names both owners.</summary>
        [Test]
        public void Kill_RemovesUnitAndEmitsEventsInOrder()
        {
            Simulation sim = TestWorld.Started();
            EntityId glass = Spawn(sim, TestWorld.GlassId, P1, 3, 0);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            Assert.That(Get(sim, glass).Health, Is.EqualTo(Fix64.FromInt(5)));
            List<SimEvent> events = StepCollect(sim, 16);
            Assert.That(sim.Units.Contains(glass), Is.False);
            // Kinds of the three events of the lethal hit, in order of appearance.
            List<string> kinds = new List<string>();
            foreach (SimEvent evt in events)
            {
                if (evt is UnitDamagedEvent || evt is UnitKilledEvent || evt is UnitRemovedEvent)
                {
                    kinds.Add(evt.Kind);
                }
            }
            Assert.That(kinds, Is.EqualTo(new List<string> { "unit_damaged", "unit_killed", "unit_removed" }));
            UnitKilledEvent? killed = null;
            UnitRemovedEvent? removed = null;
            foreach (SimEvent evt in events)
            {
                killed = evt as UnitKilledEvent ?? killed;
                removed = evt as UnitRemovedEvent ?? removed;
            }
            Assert.That(killed!.Victim, Is.EqualTo(glass));
            Assert.That(killed.Killer, Is.EqualTo(shooter));
            Assert.That(killed.VictimOwner, Is.EqualTo(P1));
            Assert.That(killed.KillerOwner, Is.EqualTo(P0));
            Assert.That(removed!.Reason, Is.EqualTo("killed"));
            // The shooter has no target left once the victim is gone.
            sim.Step();
            Assert.That(Get(sim, shooter).AttackTarget, Is.EqualTo(EntityId.None));
        }

        /// <summary>Allied units are never targeted, even when they are the nearest unit in range.</summary>
        [Test]
        public void Allies_AreNeverTargeted()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), TestWorld.TestFactionId, "A"),
                new PlayerSetup(P1, new TeamId(0), TestWorld.TestFactionId, "B"),
                new PlayerSetup(new PlayerId(2), new TeamId(1), TestWorld.TestFactionId, "C"),
            };
            Simulation sim = TestWorld.StartedWith(players);
            EntityId ally = Spawn(sim, TestWorld.TankId, P1, 1, 0);
            EntityId own = Spawn(sim, TestWorld.TankId, P0, 0, 1);
            EntityId enemy = Spawn(sim, TestWorld.TankId, new PlayerId(2), 5, 0);
            Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            List<SimEvent> events = StepCollect(sim, 40);
            Assert.That(Damage(events).Count, Is.GreaterThan(0));
            foreach (UnitDamagedEvent hit in Damage(events))
            {
                Assert.That(hit.Target, Is.EqualTo(enemy));
            }
            Assert.That(Get(sim, ally).Health, Is.EqualTo(Fix64.FromInt(100)));
            Assert.That(Get(sim, own).Health, Is.EqualTo(Fix64.FromInt(100)));
            // Neutral units are nobody's enemy either.
            Simulation neutral = TestWorld.Started();
            EntityId rock = Spawn(neutral, TestWorld.TankId, PlayerId.None, 2, 0);
            Spawn(neutral, TestWorld.ShooterId, P0, 0, 0);
            StepCollect(neutral, 20);
            Assert.That(Get(neutral, rock).Health, Is.EqualTo(Fix64.FromInt(100)));
        }

        /// <summary>can_target decides which layers a weapon hits: the ground-and-water shooter ignores a flyer, the all-layer mortar does not.</summary>
        [Test]
        public void CanTarget_RespectsLayers()
        {
            Simulation sim = TestWorld.Started();
            EntityId flyer = Spawn(sim, TestWorld.FlyerId, P1, 3, 0);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            StepCollect(sim, 40);
            Assert.That(Get(sim, flyer).Health, Is.EqualTo(Fix64.FromInt(100)));
            Assert.That(Get(sim, shooter).AttackTarget, Is.EqualTo(EntityId.None));
            // An explicit attack order on a layer the weapon cannot hit is rejected.
            sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P0, 0, shooter, flyer));
            sim.Step();
            Assert.That(sim.Events.Current[0], Is.InstanceOf<CommandRejectedEvent>());
            Assert.That(((CommandRejectedEvent)sim.Events.Current[0]).Reason, Is.EqualTo("cannot_target_layer"));

            Simulation withMortar = TestWorld.Started();
            EntityId flyer2 = Spawn(withMortar, TestWorld.FlyerId, P1, 3, 0);
            Spawn(withMortar, TestWorld.MortarId, P0, 0, 0);
            Assert.That(Get(withMortar, flyer2).Health, Is.EqualTo(Fix64.FromInt(90)));
        }

        /// <summary>An attack-moving unit chases an enemy inside the acquire range, kills it, then resumes to its goal; a far enemy is never chased.</summary>
        [Test]
        public void AttackMove_ChasesWithinAcquireRangeThenResumes()
        {
            Simulation sim = TestWorld.Started();
            EntityId near = Spawn(sim, TestWorld.GlassId, P1, 10, 0);
            EntityId far = Spawn(sim, TestWorld.TankId, P1, 15, 30);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            sim.Submit(new AttackMoveCommand(sim.Clock.Tick, P0, 0, shooter, At(30, 0)));
            // Weapon reach is 5 + 1.0 radii = 6, acquire reach 8: the enemy at x=10 is noticed once the shooter is at x=2.
            Unit unit = Get(sim, shooter);
            // Events gathered while the shooter walks up and stops to fight.
            List<SimEvent> events = new List<SimEvent>();
            // Loop counter over the ticks. Unit: ticks. Range: 0 to 199; the shooter stops within a few seconds.
            for (int i = 0; i < 200 && (unit.AttackTarget != near || unit.HasMoveTarget); i++)
            {
                events.AddRange(StepCollect(sim, 1));
            }
            Assert.That(unit.IsAttackMoving, Is.True);
            Assert.That(unit.AttackTarget, Is.EqualTo(near));
            Assert.That(unit.HasMoveTarget, Is.False, "it stopped to shoot once in weapon range");
            events.AddRange(StepCollect(sim, 1));
            Assert.That((Get(sim, near).Position - unit.Position).Length() <= Fix64.FromInt(6), Is.True);
            Assert.That(Damage(events).Count, Is.GreaterThan(0));
            // After the kill it resumes to the goal and the order ends on arrival.
            StepCollect(sim, 600);
            Assert.That(sim.Units.Contains(near), Is.False);
            Assert.That((unit.Position - At(30, 0)).LengthSquared() <= TestWorld.Tuning.Terrain.ArrivalRadiusTiles * TestWorld.Tuning.Terrain.ArrivalRadiusTiles, Is.True, "arrived at the goal");
            Assert.That(unit.IsAttackMoving, Is.False);
            Assert.That(unit.HasMoveTarget, Is.False);
            Assert.That(Get(sim, far).Health, Is.EqualTo(Fix64.FromInt(100)), "the far enemy was never chased");
        }

        /// <summary>A plain move order never stops or chases, but the unit still shoots what is in range while it walks.</summary>
        [Test]
        public void PlainMove_ShootsInRangeButDoesNotChase()
        {
            Simulation sim = TestWorld.Started();
            // Enemy right beside the path (in range at the start) and a second enemy 7 tiles off the path (outside weapon range, inside no chase).
            EntityId close = Spawn(sim, TestWorld.TankId, P1, 2, 0);
            EntityId offPath = Spawn(sim, TestWorld.TankId, P1, 10, 7);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P0, 0, shooter, At(20, 0)));
            // 20 ticks: the first shot fired on the spawn tick, the second is due 16 ticks later while the unit has walked about 3 tiles.
            List<SimEvent> events = StepCollect(sim, 20);
            Unit unit = Get(sim, shooter);
            Assert.That(unit.HasMoveTarget, Is.True, "a move order is not interrupted by targets");
            Assert.That(unit.Position.X, Is.GreaterThan(Fix64.Zero));
            Assert.That(Damage(events).Count, Is.GreaterThan(0), "it fired at the enemy in range while walking");
            Assert.That(Get(sim, close).Health < Fix64.FromInt(100), Is.True);
            StepCollect(sim, 300);
            Assert.That((unit.Position - At(20, 0)).Length() <= TestWorld.Tuning.Terrain.ArrivalRadiusTiles, Is.True, "arrived at the move target");
            Assert.That(unit.IsAttackMoving, Is.False);
            Assert.That(Get(sim, offPath).Health, Is.EqualTo(Fix64.FromInt(100)), "the off-path enemy was never chased");
        }

        /// <summary>attack_unit chases its target at any distance and rejects bad orders; stop clears the move, attack-move and target.</summary>
        [Test]
        public void AttackUnit_ChasesAnyDistance_AndStopClearsState()
        {
            Simulation sim = TestWorld.Started();
            EntityId victim = Spawn(sim, TestWorld.TankId, P1, 40, 0);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            EntityId ownTank = Spawn(sim, TestWorld.TankId, P0, 0, 5);
            // Rejections: allied target, unarmed attacker, wrong owner.
            sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P0, 0, shooter, ownTank));
            sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P0, 1, ownTank, victim));
            sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P1, 0, shooter, victim));
            sim.Step();
            // Reasons of the three rejections in command order (issuer P0 seq 0, P0 seq 1, P1 seq 0).
            List<string> reasons = new List<string>();
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is CommandRejectedEvent rejected)
                {
                    reasons.Add(rejected.Reason);
                }
            }
            Assert.That(reasons, Is.EqualTo(new List<string> { "target_not_enemy", "no_weapon", "not_owner" }));

            // A valid order walks 40 tiles to the victim and starts hitting it; the forced target survives the distance.
            sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P0, 2, shooter, victim));
            StepCollect(sim, 20);
            Unit unit = Get(sim, shooter);
            Assert.That(unit.AttackTarget, Is.EqualTo(victim), "kept although 40 tiles away");
            Assert.That(unit.IsAttackTargetForced, Is.True);
            Assert.That(unit.HasMoveTarget, Is.True);
            StepCollect(sim, 500);
            Assert.That(Get(sim, victim).Health < Fix64.FromInt(100), Is.True, "it arrived and shot");

            // Stop clears everything.
            sim.Submit(new AttackMoveCommand(sim.Clock.Tick, P0, 3, shooter, At(0, 0)));
            sim.Step();
            Assert.That(unit.IsAttackMoving, Is.True);
            sim.Submit(new StopCommand(sim.Clock.Tick, P0, 4, shooter));
            sim.Step();
            Assert.That(unit.HasMoveTarget, Is.False);
            Assert.That(unit.IsAttackMoving, Is.False);
            Assert.That(unit.AttackMoveGoal, Is.EqualTo(FixVec2.Zero));
            Assert.That(unit.IsAttackTargetForced, Is.False);
        }

        /// <summary>A mechanic that registers two damage modifiers, one doubling and one adding half again, with IDs deliberately out of order.</summary>
        private sealed class ModifierMechanic : IFactionMechanic
        {
            /// <summary>Mechanic ID used in the test faction.</summary>
            public string Id => "test_modifier";

            /// <summary>Registers modifiers "z_half_more" (x1.5) and "a_double" (x2).</summary>
            public void OnMatchStart(Simulation sim)
            {
                sim.RegisterDamageModifier(new FixedModifier("z_half_more", Fix64.Parse("1.5")));
                sim.RegisterDamageModifier(new FixedModifier("a_double", Fix64.FromInt(2)));
            }

            /// <summary>Does nothing.</summary>
            public void OnTick(Simulation sim)
            {
            }

            /// <summary>No state to hash.</summary>
            public void WriteHash(StateHasher hasher)
            {
            }
        }

        /// <summary>A modifier that always answers the same multiplier.</summary>
        private sealed class FixedModifier : IDamageModifier
        {
            /// <summary>Stable ID deciding application order.</summary>
            public string Id { get; }

            /// <summary>The multiplier answered for every hit. Unit: fraction. Range: 0 or more.</summary>
            private readonly Fix64 _factor;

            /// <summary>Stores the ID and factor.</summary>
            public FixedModifier(string id, Fix64 factor)
            {
                Id = id;
                _factor = factor;
            }

            /// <summary>Answers the stored factor.</summary>
            public Fix64 OutgoingDamageMultiplier(Simulation sim, Unit attacker)
            {
                return _factor;
            }
        }

        /// <summary>Registered damage modifiers multiply a hit (10 x 2 x 1.5 = 30), are kept sorted by ID, and can only register at match start.</summary>
        [Test]
        public void DamageModifier_MultipliesAndIsSortedById()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddFaction(new FactionDefinition("modded", "Modded", "test_modifier", new List<string> { TestWorld.ShooterId }));
            FactionMechanicRegistry registry = BuiltInMechanics.CreateRegistry();
            registry.Register("test_modifier", () => new ModifierMechanic());
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "modded", "A"),
                new PlayerSetup(P1, new TeamId(1), TestWorld.TestFactionId, "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(1, TestWorld.TicksPerSecond, TestWorld.HashInterval), catalog, players, registry, TestWorld.Tuning, TestWorld.FlatMap()));
            sim.Start();
            Assert.That(sim.DamageModifiers.Count, Is.EqualTo(2));
            Assert.That(sim.DamageModifiers[0].Id, Is.EqualTo("a_double"));
            Assert.That(sim.DamageModifiers[1].Id, Is.EqualTo("z_half_more"));
            EntityId victim = Spawn(sim, TestWorld.GlassId, P1, 3, 0);
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
            // Glass has 15 health and light armour: 10 x 2 x 1.5 = 30 kills it in one hit.
            Assert.That(sim.Units.Contains(victim), Is.False);
            Assert.That(sim.Units.Contains(shooter), Is.True);
            Expect.Throws<System.InvalidOperationException>(() => sim.RegisterDamageModifier(new FixedModifier("late", Fix64.One)));
        }

        /// <summary>The same small battle run twice gives identical state hashes and event streams; a different command gives a different hash.</summary>
        [Test]
        public void Battle_IsDeterministic()
        {
            // Runs a 6-unit battle for 200 ticks and returns the hashes every 10 ticks plus the total event count.
            System.Func<bool, (List<ulong>, long)> run = changed =>
            {
                Simulation sim = TestWorld.Started(99);
                EntityId a1 = Spawn(sim, TestWorld.ShooterId, P0, 0, 0);
                EntityId a2 = Spawn(sim, TestWorld.MortarId, P0, 0, 3);
                Spawn(sim, TestWorld.TankId, P0, 0, 6);
                EntityId b1 = Spawn(sim, TestWorld.ShooterId, P1, 14, 0);
                Spawn(sim, TestWorld.GlassId, P1, 14, 3);
                Spawn(sim, TestWorld.TankId, P1, 14, 6);
                sim.Submit(new AttackMoveCommand(sim.Clock.Tick, P0, 0, a1, At(20, 0)));
                sim.Submit(new AttackMoveCommand(sim.Clock.Tick, P0, 1, a2, At(20, changed ? 5 : 3)));
                sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P1, 0, b1, a1));
                // Hashes collected every tick.
                List<ulong> hashes = new List<ulong>();
                // Loop counter over the ticks. Unit: ticks. Range: 0 to 199.
                for (int i = 0; i < 200; i++)
                {
                    sim.Step();
                    hashes.Add(sim.ComputeStateHash());
                }
                return (hashes, sim.Events.TotalCount);
            };
            (List<ulong> first, long firstEvents) = run(false);
            (List<ulong> second, long secondEvents) = run(false);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(secondEvents, Is.EqualTo(firstEvents));
            (List<ulong> altered, long _) = run(true);
            Assert.That(altered[altered.Count - 1], Is.Not.EqualTo(first[first.Count - 1]));
        }

        /// <summary>The shipped data: weapons and armour load from unit files, the damage table comes from tuning/combat.json, and a bad damage type names its file and field.</summary>
        [Test]
        public void ShippedData_LoadsWeaponsArmourAndTable()
        {
            DataSet data = TestData.LoadAll();
            UnitDefinition windrunner = data.Content.Units["skai.windrunner"];
            Assert.That(windrunner.Weapon, Is.Not.Null);
            Assert.That(windrunner.Weapon!.Damage, Is.EqualTo(Fix64.FromInt(6)));
            Assert.That(windrunner.Weapon.DamageType, Is.EqualTo("kinetic"));
            Assert.That(windrunner.Weapon.RangeTiles, Is.EqualTo(Fix64.FromInt(4)));
            Assert.That(windrunner.Weapon.CooldownSeconds, Is.EqualTo(Fix64.Parse("0.8")));
            Assert.That(windrunner.Weapon.CanTarget(MovementLayer.Air), Is.True);
            Assert.That(data.Content.Units["water.grasper"].Weapon!.IsMelee, Is.True);
            Assert.That(data.Content.Units["water.grasper"].Weapon!.CanTarget(MovementLayer.Air), Is.False);
            Assert.That(data.Content.Units["skai.roc_rider"].ArmourType, Is.EqualTo("armoured"));
            Assert.That(data.Content.Units["skai.fledgling"].Weapon, Is.Null, "workers are unarmed");
            Assert.That(data.Content.Units["skai.fledgling"].SightRangeTiles, Is.EqualTo(Fix64.FromInt(8)));
            // Table lookup through the rules the loader put on the catalog.
            Assert.That(data.Content.Combat.IsNeutral, Is.False);
            Assert.That(data.Content.Combat.GetMultiplier("kinetic", "armoured"), Is.EqualTo(Fix64.Parse("0.75")));
            Assert.That(data.Tuning.Combat.GetMultiplier("drench", "structure"), Is.EqualTo(Fix64.Parse("0.25")));
            Assert.That(data.Tuning.Combat.AcquireRangeBonusTiles, Is.EqualTo(Fix64.FromInt(2)));
            // Windrunner (6 kinetic) against a Roc Rider (armoured) does 4.5 per hit when a match plays on the shipped data.
            Assert.That(windrunner.Weapon.Damage * data.Content.Combat.GetMultiplier("kinetic", "armoured"), Is.EqualTo(Fix64.Parse("4.5")));

            // A damage type missing from the table is rejected naming the unit file and the field.
            List<DataSourceFile> files = TestData.WithEdit("units/skai/windrunner.json", "\"damage_type\": \"kinetic\"", "\"damage_type\": \"plasma\"");
            DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(files));
            Assert.That(ex.Message, Does.StartWith("units/skai/windrunner.json: field 'weapon.damage_type'"));
            files = TestData.WithEdit("units/skai/windrunner.json", "\"armour_type\": \"light\"", "\"armour_type\": \"paper\"");
            ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(files));
            Assert.That(ex.Message, Does.StartWith("units/skai/windrunner.json: field 'armour_type'"));
            ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.Without("tuning/combat.json")));
            Assert.That(ex.Message, Does.StartWith("tuning/combat.json: "));
        }
    }
}

// rts/tests/Sim.Tests/Combat/P003CombatTests.cs
// Acceptance tests of P-003 package A (rts/docs/specs/p003_rules.md A.7): uphill ranged multiplier, armed workers and armed-first
// targeting (no switching), hit_delay_ticks with the hashed PendingHitQueue, and the always-on dome shield. Part of Rts.Sim.Tests.
// Builds its own small catalog on top of TestWorld.Catalog() (test units are placeholders, as in the spec); the shipped data is loaded
// where a test checks the loader.
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Combat rule changes of P-003 package A on small hand-built battles.</summary>
    [TestFixture]
    public class P003CombatTests
    {
        /// <summary>Player 0, team 0, faction "modded" (carries the test damage modifier).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1, team 1 (enemy of P0).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Ranged test unit with a 2-tick hit delay: kinetic 10, range 5, cooldown 1 s (20 ticks), ground and water.</summary>
        private const string LobberId = "test.lobber";

        /// <summary>Same as <see cref="LobberId"/> with a 1-tick delay.</summary>
        private const string Lobber1Id = "test.lobber1";

        /// <summary>Same as <see cref="LobberId"/> with a 10-tick delay.</summary>
        private const string Lobber10Id = "test.lobber10";

        /// <summary>Melee test unit: kinetic 10, range 1, cooldown 1 s, delay 0, ground and water.</summary>
        private const string MeleeId = "test.melee";

        /// <summary>Unarmed dome unit: ground, 100 health, radius 0.5, dome radius 3, pool 150, delay 4 s, regen 25 per second.</summary>
        private const string DomeId = "test.dome";

        /// <summary>Unarmed ground unit with speed 100 tiles per second (5 tiles per tick), light armour.</summary>
        private const string FastId = "test.fast";

        /// <summary>Worker-like unit with a weak melee weapon: kinetic 3, range 0.3, cooldown 1 s, delay 0, ground and water.</summary>
        private const string BrawlerId = "test.brawler";

        /// <summary>Armed air unit: kinetic 10, range 5, cooldown 1 s, delay 0, hits ground, water and air.</summary>
        private const string SkyShotId = "test.skyshot";

        /// <summary>Damage modifier whose factor a test may change between steps.</summary>
        private sealed class VarModifier : IDamageModifier
        {
            /// <summary>Stable ID deciding application order.</summary>
            public string Id => "a_var";

            /// <summary>Multiplier answered for every hit. Unit: fraction. Range: 0 or more; starts at 1.</summary>
            public Fix64 Factor = Fix64.One;

            /// <summary>Answers <see cref="Factor"/>.</summary>
            public Fix64 OutgoingDamageMultiplier(Simulation sim, Unit attacker)
            {
                return Factor;
            }
        }

        /// <summary>Mechanic that registers one <see cref="VarModifier"/>.</summary>
        private sealed class VarMechanic : IFactionMechanic
        {
            /// <summary>The modifier to register.</summary>
            private readonly VarModifier _modifier;

            /// <summary>Stores the modifier.</summary>
            public VarMechanic(VarModifier modifier)
            {
                _modifier = modifier;
            }

            /// <summary>Stable mechanic ID.</summary>
            public string Id => "test_modifier";

            /// <summary>Registers the modifier.</summary>
            public void OnMatchStart(Simulation sim)
            {
                sim.RegisterDamageModifier(_modifier);
            }

            /// <summary>Nothing per tick.</summary>
            public void OnTick(Simulation sim)
            {
            }

            /// <summary>No state.</summary>
            public void WriteHash(StateHasher hasher)
            {
            }
        }

        /// <summary>A simulation plus the events of every step so far and a command sequence counter.</summary>
        private sealed class W
        {
            /// <summary>The started simulation.</summary>
            public Simulation Sim = null!;

            /// <summary>The modifier registered for P0's faction.</summary>
            public VarModifier Mod = null!;

            /// <summary>Every event of every step, in order.</summary>
            public readonly List<SimEvent> Log = new List<SimEvent>();

            /// <summary>Next command sequence number. Unit: counter. Range: 0 upward.</summary>
            private uint _seq;

            /// <summary>Tick of the step that ran last. Unit: ticks. Range: 0 upward; -1 before any step.</summary>
            public int LastTick => Sim.Clock.Tick - 1;

            /// <summary>Steps <paramref name="n"/> ticks, appending each tick's events to <see cref="Log"/>.</summary>
            public void Step(int n = 1)
            {
                for (int i = 0; i < n; i++)
                {
                    Sim.Step();
                    Log.AddRange(Sim.Events.Current);
                }
            }

            /// <summary>Spawns a unit with a script command, steps one tick and returns the new unit's ID.</summary>
            public EntityId Spawn(string definition, PlayerId owner, int x, int y)
            {
                Sim.Submit(new SpawnUnitCommand(Sim.Clock.Tick, PlayerId.None, _seq++, definition, owner, FixVec2.FromInts(x, y)));
                int before = Log.Count;
                Step();
                for (int i = before; i < Log.Count; i++)
                {
                    if (Log[i] is UnitSpawnedEvent spawned)
                    {
                        return spawned.Unit;
                    }
                }
                throw new AssertionException("Spawn produced no UnitSpawnedEvent.");
            }

            /// <summary>Submits an attack_unit order for the next step and steps one tick.</summary>
            public void Attack(EntityId unit, EntityId target)
            {
                Sim.Submit(new AttackUnitCommand(Sim.Clock.Tick, PlayerId.None, _seq++, unit, target));
                Step();
            }

            /// <summary>Submits a move order for the next step and steps one tick.</summary>
            public void Move(EntityId unit, int x, int y)
            {
                Sim.Submit(new MoveUnitCommand(Sim.Clock.Tick, PlayerId.None, _seq++, unit, FixVec2.FromInts(x, y)));
                Step();
            }

            /// <summary>Submits a remove order for the next step and steps one tick.</summary>
            public void Remove(EntityId unit)
            {
                Sim.Submit(new RemoveUnitCommand(Sim.Clock.Tick, PlayerId.None, _seq++, unit));
                Step();
            }

            /// <summary>The live unit with this ID.</summary>
            public Unit Get(EntityId id)
            {
                Assert.That(Sim.Units.TryGet(id, out Unit? unit), Is.True, id + " should be alive");
                return unit!;
            }

            /// <summary>All logged events of type <typeparamref name="T"/>, in order.</summary>
            public List<T> Of<T>() where T : SimEvent
            {
                return Log.OfType<T>().ToList();
            }
        }

        /// <summary>Test combat rules: the TestWorld table with uphill multiplier 0.75.</summary>
        private static CombatRules Rules()
        {
            SortedDictionary<string, IReadOnlyDictionary<string, Fix64>> table = new SortedDictionary<string, IReadOnlyDictionary<string, Fix64>>();
            table.Add("kinetic", new SortedDictionary<string, Fix64> { { "light", Fix64.One }, { "armoured", Fix64.Half } });
            table.Add("blast", new SortedDictionary<string, Fix64> { { "light", Fix64.Half }, { "armoured", Fix64.FromInt(2) } });
            return new CombatRules(table, Fix64.FromInt(2), "light", Fix64.Parse("0.75"));
        }

        /// <summary>Adds a ground weapon unit (light armour, speed 2, radius 0.5, sight 8) with the given weapon to a catalog.</summary>
        private static void AddArmed(ContentCatalog catalog, string id, MovementLayer layer, Fix64 speed, Fix64 radius, WeaponDefinition weapon)
        {
            catalog.AddUnit(new UnitDefinition(id, id, TestWorld.TestFactionId, layer, Fix64.FromInt(100), speed, 1, radius, 50, 0,
                "light", Fix64.FromInt(8), weapon));
        }

        /// <summary>Builds the catalog: TestWorld's units plus the lobbers, melee, dome, fast, brawler and sky shooter.</summary>
        private static ContentCatalog Catalog()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            Fix64 ten = Fix64.FromInt(10);
            Fix64 five = Fix64.FromInt(5);
            AddArmed(catalog, LobberId, MovementLayer.Ground, Fix64.FromInt(2), Fix64.Half, new WeaponDefinition(ten, "kinetic", five, Fix64.One, true, true, false, false, 2));
            AddArmed(catalog, Lobber1Id, MovementLayer.Ground, Fix64.FromInt(2), Fix64.Half, new WeaponDefinition(ten, "kinetic", five, Fix64.One, true, true, false, false, 1));
            AddArmed(catalog, Lobber10Id, MovementLayer.Ground, Fix64.FromInt(2), Fix64.Half, new WeaponDefinition(ten, "kinetic", five, Fix64.One, true, true, false, false, 10));
            AddArmed(catalog, MeleeId, MovementLayer.Ground, Fix64.FromInt(2), Fix64.Half, new WeaponDefinition(ten, "kinetic", Fix64.One, Fix64.One, true, true, false));
            AddArmed(catalog, BrawlerId, MovementLayer.Ground, Fix64.FromInt(2), Fix64.Parse("0.3"), new WeaponDefinition(Fix64.FromInt(3), "kinetic", Fix64.Parse("0.3"), Fix64.One, true, true, false));
            AddArmed(catalog, SkyShotId, MovementLayer.Air, Fix64.FromInt(2), Fix64.Half, new WeaponDefinition(ten, "kinetic", five, Fix64.One, true, true, true));
            catalog.AddUnit(new UnitDefinition(FastId, "Fast", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(100), 1, Fix64.Half, 50, 0));
            catalog.AddUnit(new UnitDefinition(DomeId, "Dome", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0,
                "light", Fix64.FromInt(8), null, null, null, null, null,
                new ShieldDomeDefinition(Fix64.FromInt(3), Fix64.FromInt(150), Fix64.FromInt(4), Fix64.FromInt(25))));
            catalog.SetCombatRules(Rules());
            catalog.AddFaction(new FactionDefinition("modded", "Modded", "test_modifier", new List<string> { TestWorld.RunnerId }));
            return catalog;
        }

        /// <summary>Starts a match on <paramref name="map"/> (flat when null) with P0 (modded) against P1 (test).</summary>
        private static W Make(MapDefinition? map = null, ContentCatalog? catalog = null)
        {
            W w = new W();
            w.Mod = new VarModifier();
            FactionMechanicRegistry registry = BuiltInMechanics.CreateRegistry();
            VarModifier mod = w.Mod;
            registry.Register("test_modifier", () => new VarMechanic(mod));
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "modded", "A"),
                new PlayerSetup(P1, new TeamId(1), TestWorld.TestFactionId, "B"),
            };
            w.Sim = new Simulation(new SimulationConfig(new MatchSettings(1, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                catalog ?? Catalog(), players, registry, TestWorld.Tuning, map ?? TestWorld.FlatMap()));
            w.Sim.Start();
            return w;
        }

        /// <summary>Fix64 from text.</summary>
        private static Fix64 F(string text)
        {
            return Fix64.Parse(text);
        }

        /// <summary>Damage of the first hit in the log. Unit: health points.</summary>
        private static Fix64 FirstHit(W w)
        {
            List<UnitDamagedEvent> hits = w.Of<UnitDamagedEvent>();
            Assert.That(hits.Count, Is.GreaterThan(0), "a hit was expected");
            return hits[0].Damage;
        }

        /// <summary>1: the shipped tuning has the key (0.75); a missing key and 1.5 fail naming it.</summary>
        [Test]
        public void Loader_UphillMultiplier()
        {
            Assert.That(TestData.LoadAll().Tuning.Combat.UphillRangedDamageMultiplier, Is.EqualTo(F("0.75")));
            const string path = "tuning/combat.json";
            const string line = "\"uphill_ranged_damage_multiplier\": 0.75,";
            DataValidationException missing = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit(path, line, string.Empty)));
            Assert.That(missing.Message, Does.Contain("uphill_ranged_damage_multiplier"));
            DataValidationException tooBig = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit(path, line, "\"uphill_ranged_damage_multiplier\": 1.5,")));
            Assert.That(tooBig.Message, Does.Contain("uphill_ranged_damage_multiplier"));
        }

        /// <summary>2: uphill is multiplied by 0.75 after the table; downhill and level are not. Forced attack orders bypass fog (a low unit cannot see a terrace above).</summary>
        [Test]
        public void Uphill_AppliesOnlyToHigherTargets()
        {
            // Tank (armoured, kinetic table 0.5) on the high terrace, shooter below it.
            W up = Make(TestWorld.TerracedMap());
            EntityId tank = up.Spawn(TestWorld.TankId, P1, 33, 10);
            EntityId shooter = up.Spawn(TestWorld.ShooterId, P0, 29, 10);
            up.Attack(shooter, tank);
            Assert.That(FirstHit(up), Is.EqualTo(Fix64.FromInt(10) * F("0.5") * F("0.75")));
            // Shooter on the terrace, tank below: table only.
            W down = Make(TestWorld.TerracedMap());
            EntityId lowTank = down.Spawn(TestWorld.TankId, P1, 29, 10);
            EntityId high = down.Spawn(TestWorld.ShooterId, P0, 33, 10);
            down.Attack(high, lowTank);
            Assert.That(FirstHit(down), Is.EqualTo(Fix64.FromInt(5)));
            // Same height.
            W level = Make(TestWorld.TerracedMap());
            EntityId flatTank = level.Spawn(TestWorld.TankId, P1, 20, 10);
            EntityId flatShooter = level.Spawn(TestWorld.ShooterId, P0, 16, 10);
            level.Attack(flatShooter, flatTank);
            Assert.That(FirstHit(level), Is.EqualTo(Fix64.FromInt(5)));
        }

        /// <summary>3: a shooter on the ramp tile (31, 20) sees one level up, so no penalty.</summary>
        [Test]
        public void Uphill_RampLiftsTheEye()
        {
            W w = Make(TestWorld.TerracedMap());
            EntityId tank = w.Spawn(TestWorld.TankId, P1, 33, 20);
            EntityId shooter = w.Spawn(TestWorld.ShooterId, P0, 31, 20);
            w.Attack(shooter, tank);
            Assert.That(FirstHit(w), Is.EqualTo(Fix64.FromInt(5)));
        }

        /// <summary>4: no penalty for a melee attacker, an air attacker or an air target.</summary>
        [Test]
        public void Uphill_ExemptionsMeleeAndAir()
        {
            W melee = Make(TestWorld.TerracedMap());
            EntityId tank = melee.Spawn(TestWorld.TankId, P1, 32, 10);
            EntityId fighter = melee.Spawn(MeleeId, P0, 31, 10);
            melee.Attack(fighter, tank);
            Assert.That(FirstHit(melee), Is.EqualTo(Fix64.FromInt(5)), "melee");

            W air = Make(TestWorld.TerracedMap());
            EntityId tank2 = air.Spawn(TestWorld.TankId, P1, 33, 10);
            EntityId sky = air.Spawn(SkyShotId, P0, 29, 10);
            air.Attack(sky, tank2);
            Assert.That(FirstHit(air), Is.EqualTo(Fix64.FromInt(5)), "air attacker");

            // Mortar (blast 20, hits air) at a flyer above the terrace: light armour blast 0.5 = 10, no 0.75.
            W target = Make(TestWorld.TerracedMap());
            EntityId flyer = target.Spawn(TestWorld.FlyerId, P1, 33, 10);
            EntityId mortar = target.Spawn(TestWorld.MortarId, P0, 29, 10);
            target.Attack(mortar, flyer);
            Assert.That(FirstHit(target), Is.EqualTo(Fix64.FromInt(10)), "air target");
        }

        /// <summary>5: order is table, then uphill, then modifiers: ((10 x 0.5) x 0.75) x 2.</summary>
        [Test]
        public void Uphill_IsAppliedBeforeModifiers()
        {
            W w = Make(TestWorld.TerracedMap());
            w.Mod.Factor = Fix64.FromInt(2);
            EntityId tank = w.Spawn(TestWorld.TankId, P1, 33, 10);
            EntityId shooter = w.Spawn(TestWorld.ShooterId, P0, 29, 10);
            w.Attack(shooter, tank);
            Assert.That(FirstHit(w), Is.EqualTo(((Fix64.FromInt(10) * F("0.5")) * F("0.75")) * Fix64.FromInt(2)));
        }

        /// <summary>6: both shipped workers carry a melee weapon of 3 kinetic with no hit delay.</summary>
        [Test]
        public void ShippedWorkers_HaveAWeakMeleeWeapon()
        {
            DataSet data = TestData.LoadAll();
            foreach (string id in new[] { "skai.fledgling", "water.tender" })
            {
                WeaponDefinition? weapon = data.Content.Units[id].Weapon;
                Assert.That(weapon, Is.Not.Null, id);
                Assert.That(weapon!.IsMelee, Is.True, id);
                Assert.That(weapon.HitDelayTicks, Is.EqualTo(0), id);
                Assert.That(weapon.Damage, Is.EqualTo(Fix64.FromInt(3)), id);
                Assert.That(weapon.DamageType, Is.EqualTo("kinetic"), id);
            }
        }

        /// <summary>7: armed enemies are picked before nearer unarmed ones; equal armed distances go to the lowest ID.</summary>
        [Test]
        public void ArmedFirst_PicksArmedOverNearerUnarmed()
        {
            W w = Make();
            EntityId runner = w.Spawn(TestWorld.RunnerId, P1, 22, 20);
            EntityId armed = w.Spawn(TestWorld.ShooterId, P1, 24, 20);
            EntityId mine = w.Spawn(TestWorld.ShooterId, P0, 20, 20);
            Assert.That(runner.IsValid, Is.True);
            Assert.That(w.Get(mine).AttackTarget, Is.EqualTo(armed));
            // Two armed at equal distance: lowest ID.
            W tie = Make();
            EntityId a = tie.Spawn(TestWorld.ShooterId, P1, 24, 20);
            EntityId b = tie.Spawn(TestWorld.ShooterId, P1, 16, 20);
            EntityId me = tie.Spawn(TestWorld.ShooterId, P0, 20, 20);
            Assert.That(a.Value, Is.LessThan(b.Value));
            Assert.That(tie.Get(me).AttackTarget, Is.EqualTo(a));
        }

        /// <summary>8: units never switch on their own: an unarmed auto target is kept when an armed enemy comes into reach; after it is gone the next pick is the armed one.</summary>
        [Test]
        public void NoSwitching_KeptTargetStaysUntilGone()
        {
            W w = Make();
            EntityId runner = w.Spawn(TestWorld.RunnerId, P1, 22, 20);
            EntityId mine = w.Spawn(TestWorld.ShooterId, P0, 20, 20);
            Assert.That(w.Get(mine).AttackTarget, Is.EqualTo(runner));
            EntityId armed = w.Spawn(TestWorld.ShooterId, P1, 24, 20);
            w.Step();
            Assert.That(w.Get(mine).AttackTarget, Is.EqualTo(runner), "kept");
            w.Remove(runner);
            w.Step();
            Assert.That(w.Get(mine).AttackTarget, Is.EqualTo(armed), "next pick is the armed unit");
        }

        /// <summary>9: an Uprooted HQ and a structure under construction rank as unarmed.</summary>
        [Test]
        public void ArmedFirst_UprootedHqAndConstructionCountAsUnarmed()
        {
            W w = Make();
            EntityId hq = w.Spawn(TestWorld.HqId, P1, 22, 20);
            w.Get(hq).SetHqState(HqState.Uprooted, 0);
            EntityId tower = w.Spawn(TestWorld.TowerId, P1, 23, 20);
            w.Get(tower).SetConstructionTicks(500);
            EntityId armed = w.Spawn(TestWorld.ShooterId, P1, 25, 20);
            EntityId mine = w.Spawn(TestWorld.ShooterId, P0, 20, 20);
            Assert.That(w.Get(mine).AttackTarget, Is.EqualTo(armed));
        }

        /// <summary>10: a worker-like brawler beside an enemy tank hits for 3 x table (1.5) every 20 ticks without orders.</summary>
        [Test]
        public void WorkerWeapon_HitsEveryCooldown()
        {
            W w = Make();
            EntityId tank = w.Spawn(TestWorld.TankId, P1, 20, 20);
            w.Spawn(BrawlerId, P0, 21, 20);
            w.Step(60);
            List<UnitDamagedEvent> hits = w.Of<UnitDamagedEvent>();
            Assert.That(hits.Count, Is.EqualTo(4));
            foreach (UnitDamagedEvent hit in hits)
            {
                Assert.That(hit.Target, Is.EqualTo(tank));
                Assert.That(hit.Damage, Is.EqualTo(F("1.5")));
            }
            Assert.That(hits[1].Tick - hits[0].Tick, Is.EqualTo(20));
        }

        /// <summary>11: loader hit_delay_ticks: absent is 0, 11 fails, 2 on a melee weapon fails; shipped ranged weapons have 2 and melee 0.</summary>
        [Test]
        public void Loader_HitDelayTicks()
        {
            const string shell = "units/water/shellback.json";
            Assert.That(DataLoader.Load(TestData.WithEdit(shell, "\"hit_delay_ticks\": 2,", string.Empty)).Content.Units["water.shellback"].Weapon!.HitDelayTicks, Is.EqualTo(0));
            DataValidationException tooBig = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit(shell, "\"hit_delay_ticks\": 2,", "\"hit_delay_ticks\": 11,")));
            Assert.That(tooBig.Message, Does.Contain("hit_delay_ticks"));
            DataValidationException melee = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit("units/water/grasper.json", "\"hit_delay_ticks\": 0,", "\"hit_delay_ticks\": 2,")));
            Assert.That(melee.Message, Does.Contain("must be 0 for a melee weapon"));
            foreach (UnitDefinition unit in TestData.LoadAll().Content.Units.Values)
            {
                if (unit.Weapon != null)
                {
                    Assert.That(unit.Weapon.HitDelayTicks, Is.EqualTo(unit.Weapon.IsMelee ? 0 : 2), unit.Id);
                }
            }
        }

        /// <summary>12: a delayed shot reports its landing tick at fire time and damages exactly on that tick (delays 1, 2 and 10).</summary>
        [Test]
        public void Delay_HitLandsOnItsTick()
        {
            foreach ((string unit, int delay) in new[] { (Lobber1Id, 1), (LobberId, 2), (Lobber10Id, 10) })
            {
                W w = Make();
                EntityId target = w.Spawn(TestWorld.RunnerId, P1, 22, 20);
                w.Spawn(unit, P0, 20, 20);
                int t = w.LastTick;
                w.Step(delay + 1);
                List<ShotFiredEvent> shots = w.Of<ShotFiredEvent>();
                Assert.That(shots.Count, Is.EqualTo(1), unit);
                Assert.That(shots[0].Tick, Is.EqualTo(t), unit);
                Assert.That(shots[0].LandTick, Is.EqualTo(t + delay), unit);
                List<UnitDamagedEvent> hits = w.Of<UnitDamagedEvent>();
                Assert.That(hits.Count, Is.EqualTo(1), unit);
                Assert.That(hits[0].Tick, Is.EqualTo(t + delay), unit);
                Assert.That(hits[0].Target, Is.EqualTo(target), unit);
            }
        }

        /// <summary>13: a zero-delay shot reports ShotFired then UnitDamaged on one tick and queues nothing.</summary>
        [Test]
        public void ZeroDelay_LandsAtOnce()
        {
            W w = Make();
            w.Spawn(TestWorld.RunnerId, P1, 22, 20);
            w.Spawn(TestWorld.ShooterId, P0, 20, 20);
            int shot = w.Log.FindIndex(e => e is ShotFiredEvent);
            int hit = w.Log.FindIndex(e => e is UnitDamagedEvent);
            Assert.That(shot, Is.GreaterThanOrEqualTo(0));
            Assert.That(hit, Is.GreaterThan(shot));
            Assert.That(w.Log[shot].Tick, Is.EqualTo(w.Log[hit].Tick));
            Assert.That(w.Sim.PendingHitCount, Is.EqualTo(0));
        }

        /// <summary>14: damage is fixed at fire time: a modifier changed after firing does not change the landing hit.</summary>
        [Test]
        public void Delay_DamageIsFixedAtFireTime()
        {
            W w = Make();
            w.Spawn(TestWorld.RunnerId, P1, 22, 20);
            w.Spawn(LobberId, P0, 20, 20);
            w.Mod.Factor = Fix64.FromInt(2);
            w.Step(2);
            Assert.That(FirstHit(w), Is.EqualTo(Fix64.FromInt(10)));
        }

        /// <summary>15: a fast target ordered away after the lobber fires is out of range and sight on the landing tick and still takes the hit.</summary>
        [Test]
        public void Delay_HitCannotBeDodged()
        {
            W w = Make();
            EntityId target = w.Spawn(FastId, P1, 24, 20);
            w.Spawn(Lobber10Id, P0, 20, 20);
            int t = w.LastTick;
            w.Move(target, 62, 20);
            w.Step(t + 10 - w.LastTick);
            Assert.That(w.LastTick, Is.EqualTo(t + 10));
            Assert.That(w.Get(target).Position.X, Is.GreaterThan(Fix64.FromInt(40)));
            List<UnitDamagedEvent> hits = w.Of<UnitDamagedEvent>();
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Tick, Is.EqualTo(t + 10));
            Assert.That(hits[0].Damage, Is.EqualTo(Fix64.FromInt(10)));
        }

        /// <summary>16: a target killed before the landing tick drops the hit silently.</summary>
        [Test]
        public void Delay_TargetGoneDropsTheHit()
        {
            W w = Make();
            EntityId glass = w.Spawn(TestWorld.GlassId, P1, 20, 20);
            w.Spawn(MeleeId, P0, 21, 20);
            EntityId lobber = w.Spawn(LobberId, P0, 16, 20);
            w.Spawn(MeleeId, P0, 19, 20);
            Assert.That(w.Sim.Units.Contains(glass), Is.False, "the second melee hit killed it");
            Assert.That(w.Sim.PendingHitCount, Is.EqualTo(1));
            w.Step(3);
            Assert.That(w.Of<UnitDamagedEvent>().Any(h => h.Attacker == lobber), Is.False);
            Assert.That(w.Sim.PendingHitCount, Is.EqualTo(0));
        }

        /// <summary>17: a lobber removed after firing still lands a lethal hit and is credited with owner.</summary>
        [Test]
        public void Delay_AttackerGoneStillKills()
        {
            W w = Make();
            EntityId victim = w.Spawn(TestWorld.RunnerId, P1, 22, 20);
            w.Get(victim).SetHealth(Fix64.FromInt(5));
            EntityId lobber = w.Spawn(LobberId, P0, 20, 20);
            w.Remove(lobber);
            w.Step();
            UnitKilledEvent kill = w.Of<UnitKilledEvent>().Single();
            Assert.That(kill.Victim, Is.EqualTo(victim));
            Assert.That(kill.Killer, Is.EqualTo(lobber));
            Assert.That(kill.KillerOwner, Is.EqualTo(P0));
            Assert.That(w.Sim.Units.Contains(lobber), Is.False);
        }

        /// <summary>18: hits landing on one tick land in firing order (lower sequence first).</summary>
        [Test]
        public void Delay_LandOrderFollowsSequence()
        {
            W w = Make();
            EntityId a = w.Spawn(Lobber1Id, P0, 20, 20);
            EntityId b = w.Spawn(LobberId, P0, 20, 22);
            w.Get(a).SetCooldownTicks(2);
            EntityId target = w.Spawn(TestWorld.RunnerId, P1, 24, 21);
            int t = w.LastTick;
            w.Step(2);
            List<UnitDamagedEvent> hits = w.Of<UnitDamagedEvent>().Where(h => h.Target == target).ToList();
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits[0].Tick, Is.EqualTo(t + 2));
            Assert.That(hits[1].Tick, Is.EqualTo(t + 2));
            Assert.That(a.Value, Is.LessThan(b.Value));
            Assert.That(hits[0].Attacker, Is.EqualTo(b), "b fired first (lower sequence)");
            Assert.That(hits[1].Attacker, Is.EqualTo(a));
        }

        /// <summary>19: a unit hit to 0 health by a landing is not targeted in the same combat step, is removed at its end, and still fires its own ready shot.</summary>
        [Test]
        public void Landing_DeadUnitIsNotTargetedButStillFires()
        {
            W w = Make();
            EntityId r = w.Spawn(TestWorld.ShooterId, P1, 26, 20);
            EntityId s2 = w.Spawn(TestWorld.ShooterId, P0, 22, 20);
            w.Spawn(LobberId, P0, 21, 20);
            w.Get(r).SetHealth(Fix64.FromInt(10));
            w.Get(r).SetCooldownTicks(2);
            w.Get(s2).SetCooldownTicks(2);
            int t = w.LastTick;
            w.Step(2);
            List<SimEvent> landTick = w.Log.Where(e => e.Tick == t + 2).ToList();
            Assert.That(landTick.OfType<UnitDamagedEvent>().Any(h => h.Attacker == r), Is.True, "the dying unit still fires");
            Assert.That(landTick.OfType<UnitDamagedEvent>().Any(h => h.Attacker == s2), Is.False, "no one targets it after the landing");
            Assert.That(landTick.OfType<UnitKilledEvent>().Any(k => k.Victim == r), Is.True);
            Assert.That(w.Sim.Units.Contains(r), Is.False);
        }

        /// <summary>20: the pending queue and the dome fields are in the state hash; two identical runs agree while a hit is in flight.</summary>
        [Test]
        public void Hash_CoversPendingHitsAndDomeFields()
        {
            W w = Make();
            EntityId target = w.Spawn(TestWorld.RunnerId, P1, 22, 20);
            EntityId other = w.Spawn(TestWorld.RunnerId, P1, 23, 20);
            ulong empty = w.Sim.ComputeStateHash();
            w.Spawn(Lobber10Id, P0, 20, 20);
            Assert.That(w.Sim.PendingHitCount, Is.EqualTo(1));
            ulong baseline = w.Sim.ComputeStateHash();
            Assert.That(baseline, Is.Not.EqualTo(empty));
            PendingHitQueue queue = w.Sim.PendingHits;
            int land = w.Sim.Clock.Tick - 1 + 10;
            queue.Rewrite(0, land, target, Fix64.FromInt(11));
            Assert.That(w.Sim.ComputeStateHash(), Is.Not.EqualTo(baseline), "damage");
            queue.Rewrite(0, land, other, Fix64.FromInt(10));
            Assert.That(w.Sim.ComputeStateHash(), Is.Not.EqualTo(baseline), "target");
            queue.Rewrite(0, land + 1, target, Fix64.FromInt(10));
            Assert.That(w.Sim.ComputeStateHash(), Is.Not.EqualTo(baseline), "land tick");
            queue.Rewrite(0, land, target, Fix64.FromInt(10));
            Assert.That(w.Sim.ComputeStateHash(), Is.EqualTo(baseline), "restored");
            // NextSequence: enqueue and immediately take a hit; the count is unchanged, the sequence is not.
            queue.Enqueue(w.Sim.Clock.Tick, new EntityId(1), P0, target, Fix64.One, false);
            queue.TakeDue(w.Sim.Clock.Tick, new List<PendingHit>());
            Assert.That(w.Sim.ComputeStateHash(), Is.Not.EqualTo(baseline), "next sequence");

            // Dome fields, each alone.
            W d = Make();
            Unit dome = d.Get(d.Spawn(DomeId, P0, 20, 20));
            ulong domeBase = d.Sim.ComputeStateHash();
            dome.SetShield(Fix64.FromInt(149), false, -1);
            Assert.That(d.Sim.ComputeStateHash(), Is.Not.EqualTo(domeBase), "pool");
            dome.SetShield(Fix64.FromInt(150), true, -1);
            Assert.That(d.Sim.ComputeStateHash(), Is.Not.EqualTo(domeBase), "down");
            dome.SetShield(Fix64.FromInt(150), false, 3);
            Assert.That(d.Sim.ComputeStateHash(), Is.Not.EqualTo(domeBase), "last absorb");
            dome.SetShield(Fix64.FromInt(150), false, -1);
            Assert.That(d.Sim.ComputeStateHash(), Is.EqualTo(domeBase));

            // Two identical runs agree on every tick, including ticks with a hit in flight.
            bool sawFlight = false;
            W x = Make();
            W y = Make();
            foreach (W run in new[] { x, y })
            {
                run.Spawn(TestWorld.RunnerId, P1, 22, 20);
                run.Spawn(Lobber10Id, P0, 20, 20);
            }
            for (int i = 0; i < 14; i++)
            {
                x.Step();
                y.Step();
                sawFlight = sawFlight || x.Sim.PendingHitCount > 0;
                Assert.That(x.Sim.ComputeStateHash(), Is.EqualTo(y.Sim.ComputeStateHash()));
            }
            Assert.That(sawFlight, Is.True);
        }

        /// <summary>21: the shipped Shellback dome is 3 / 150 / 4 / 25; an unknown key and pool 0 fail; a dome refill below 0.01 per second is refused by the definition.</summary>
        [Test]
        public void Loader_ShieldDome()
        {
            ShieldDomeDefinition dome = TestData.LoadAll().Content.Units["water.shellback"].ShieldDome!;
            Assert.That(dome.RadiusTiles, Is.EqualTo(Fix64.FromInt(3)));
            Assert.That(dome.Pool, Is.EqualTo(Fix64.FromInt(150)));
            Assert.That(dome.RegenDelaySeconds, Is.EqualTo(Fix64.FromInt(4)));
            Assert.That(dome.RegenPerSecond, Is.EqualTo(Fix64.FromInt(25)));
            const string shell = "units/water/shellback.json";
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit(shell, "\"pool\": 150,", "\"bogus\": 1, \"pool\": 150,")));
            Assert.That(unknown.Message, Does.Contain("bogus"));
            DataValidationException zero = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit(shell, "\"pool\": 150,", "\"pool\": 0,")));
            Assert.That(zero.Message, Does.Contain("pool"));
            // The Start check (regen_per_second converting to 0 per tick) cannot be reached: the schema minimum 0.01 stays above 0 at every allowed tick rate (10 to 60).
            Expect.Throws<System.ArgumentOutOfRangeException>(() => new ShieldDomeDefinition(Fix64.FromInt(3), Fix64.FromInt(150), Fix64.FromInt(4), F("0.001")));
        }

        /// <summary>Builds the dome scene: dome D (P0) at (20, 20), ally runner T (P0) at (22, 20). Returns the world with IDs in the out parameters.</summary>
        private static W DomeScene(out EntityId dome, out EntityId t)
        {
            W w = Make();
            dome = w.Spawn(DomeId, P0, 20, 20);
            t = w.Spawn(TestWorld.RunnerId, P0, 22, 20);
            return w;
        }

        /// <summary>22: an enemy lobber outside the dome is absorbed: no damage, a dome-hit event, nothing queued.</summary>
        [Test]
        public void Dome_AbsorbsRangedShotFromOutside()
        {
            W w = DomeScene(out EntityId dome, out EntityId t);
            EntityId lobber = w.Spawn(LobberId, P1, 27, 20);
            w.Attack(lobber, t);
            w.Step(3);
            ShieldDomeHitEvent hit = w.Of<ShieldDomeHitEvent>().First();
            Assert.That(hit.Dome, Is.EqualTo(dome));
            Assert.That(hit.Absorbed, Is.EqualTo(Fix64.FromInt(10)));
            Assert.That(hit.PoolLeft, Is.EqualTo(Fix64.FromInt(140)));
            Assert.That(w.Of<UnitDamagedEvent>().Any(h => h.Target == t), Is.False);
            Assert.That(w.Get(t).Health, Is.EqualTo(Fix64.FromInt(100)));
            Assert.That(w.Sim.PendingHitCount, Is.EqualTo(0));
        }

        /// <summary>23: an enemy melee unit next to T hurts it; the dome is untouched.</summary>
        [Test]
        public void Dome_MeleePasses()
        {
            W w = DomeScene(out EntityId dome, out EntityId t);
            w.Spawn(MeleeId, P1, 23, 20);
            Assert.That(w.Of<UnitDamagedEvent>().Any(h => h.Target == t), Is.True);
            Assert.That(w.Get(dome).ShieldPool, Is.EqualTo(Fix64.FromInt(150)));
        }

        /// <summary>24: an enemy lobber inside the dome is not absorbed.</summary>
        [Test]
        public void Dome_ShotFromInsidePasses()
        {
            W w = DomeScene(out EntityId dome, out EntityId t);
            w.Spawn(LobberId, P1, 22, 21);
            w.Step(3);
            Assert.That(w.Of<UnitDamagedEvent>().Any(h => h.Target == t), Is.True);
            Assert.That(w.Get(dome).ShieldPool, Is.EqualTo(Fix64.FromInt(150)));
            Assert.That(w.Of<ShieldDomeHitEvent>().Count, Is.EqualTo(0));
        }

        /// <summary>25: the dome does not shield enemies standing inside it and does not block its own side's shots.</summary>
        [Test]
        public void Dome_ProtectsOnlyAllies_AndDoesNotBlockOwnFire()
        {
            W a = Make();
            a.Spawn(DomeId, P0, 20, 20);
            a.Spawn(TestWorld.RunnerId, P1, 25, 20);
            a.Spawn(LobberId, P0, 21, 20);
            a.Step(3);
            Assert.That(a.Of<UnitDamagedEvent>().Count, Is.GreaterThan(0), "ally lobber inside hits an enemy outside");
            Assert.That(a.Of<ShieldDomeHitEvent>().Count, Is.EqualTo(0));

            W b = Make();
            EntityId dome = b.Spawn(DomeId, P0, 20, 20);
            b.Spawn(TestWorld.RunnerId, P1, 21, 20);
            b.Spawn(LobberId, P0, 26, 20);
            b.Step(3);
            Assert.That(b.Of<UnitDamagedEvent>().Count, Is.GreaterThan(0), "enemy inside the dome is hit normally");
            Assert.That(b.Get(dome).ShieldPool, Is.EqualTo(Fix64.FromInt(150)));
        }

        /// <summary>26: a breaking hit overflows to the target, the dome goes down, and later hits pass in full.</summary>
        [Test]
        public void Dome_BreaksWithOverflowThenPasses()
        {
            W w = DomeScene(out EntityId dome, out EntityId t);
            // A recent absorb tick keeps the refill from running before the first shot.
            w.Get(dome).SetShield(Fix64.FromInt(15), false, w.Sim.Clock.Tick);
            EntityId lobber = w.Spawn(LobberId, P1, 27, 20);
            w.Attack(lobber, t);
            w.Step(45);
            List<ShieldDomeHitEvent> hits = w.Of<ShieldDomeHitEvent>();
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits[0].Absorbed, Is.EqualTo(Fix64.FromInt(10)));
            Assert.That(hits[0].PoolLeft, Is.EqualTo(Fix64.FromInt(5)));
            Assert.That(hits[1].Absorbed, Is.EqualTo(Fix64.FromInt(5)));
            Assert.That(w.Of<ShieldDomeDownEvent>().Count, Is.EqualTo(1));
            List<Fix64> damage = w.Of<UnitDamagedEvent>().Where(h => h.Target == t).Select(h => h.Damage).ToList();
            Assert.That(damage, Is.EqualTo(new List<Fix64> { Fix64.FromInt(5), Fix64.FromInt(10) }));
        }

        /// <summary>27: refill starts 80 ticks after the last absorb at 1.25 per tick; a down dome returns exactly when full and absorbs again.</summary>
        [Test]
        public void Dome_RegenAndUp()
        {
            W w = DomeScene(out EntityId dome, out EntityId t);
            EntityId lobber = w.Spawn(LobberId, P1, 27, 20);
            int a = w.LastTick;
            Assert.That(w.Of<ShieldDomeHitEvent>().Count, Is.EqualTo(1));
            w.Remove(lobber);
            w.Step(a + 79 - w.LastTick);
            Assert.That(w.Get(dome).ShieldPool, Is.EqualTo(Fix64.FromInt(140)));
            w.Step();
            Assert.That(w.Get(dome).ShieldPool, Is.EqualTo(F("141.25")));
            // A broken dome two refills short of full.
            w.Get(dome).SetShield(F("147.5"), true, w.Sim.Clock.Tick - 100);
            w.Step();
            Assert.That(w.Of<ShieldDomeUpEvent>().Count, Is.EqualTo(0));
            w.Step();
            Assert.That(w.Of<ShieldDomeUpEvent>().Count, Is.EqualTo(1));
            Assert.That(w.Get(dome).ShieldDown, Is.False);
            int before = w.Of<ShieldDomeHitEvent>().Count;
            w.Spawn(LobberId, P1, 27, 20);
            Assert.That(w.Of<ShieldDomeHitEvent>().Count, Is.EqualTo(before + 1));
            Assert.That(w.Get(t).Health, Is.EqualTo(Fix64.FromInt(100)));
        }

        /// <summary>28: overlapping allied domes chain, lowest unit ID first.</summary>
        [Test]
        public void Dome_OverlapChainsLowestIdFirst()
        {
            W w = Make();
            EntityId d1 = w.Spawn(DomeId, P0, 20, 20);
            EntityId d2 = w.Spawn(DomeId, P0, 20, 21);
            EntityId t = w.Spawn(TestWorld.RunnerId, P0, 22, 20);
            w.Get(d1).SetShield(Fix64.FromInt(4), false, w.Sim.Clock.Tick);
            w.Spawn(LobberId, P1, 27, 20);
            List<ShieldDomeHitEvent> hits = w.Of<ShieldDomeHitEvent>();
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits[0].Dome, Is.EqualTo(d1));
            Assert.That(hits[0].Absorbed, Is.EqualTo(Fix64.FromInt(4)));
            Assert.That(hits[1].Dome, Is.EqualTo(d2));
            Assert.That(hits[1].Absorbed, Is.EqualTo(Fix64.FromInt(6)));
            Assert.That(hits[1].PoolLeft, Is.EqualTo(Fix64.FromInt(144)));
            w.Step(3);
            Assert.That(w.Of<UnitDamagedEvent>().Any(h => h.Target == t), Is.False);
        }

        /// <summary>29: the combat smoke script on shipped data gives identical hash lists twice.</summary>
        [Test]
        public void CombatSmoke_IsDeterministic()
        {
            DataSet data = TestData.LoadAll();
            List<ulong> first = RunSmoke(data);
            List<ulong> second = RunSmoke(data);
            Assert.That(first.Count, Is.GreaterThan(0));
            Assert.That(first, Is.EqualTo(second));
        }

        /// <summary>Plays rts/data/scripts/combat_smoke.json through the script parser and returns the hashes reported.</summary>
        private static List<ulong> RunSmoke(DataSet data)
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestData.FindDataDirectory(), "scripts", "combat_smoke.json"));
            CommandScript script = CommandScript.Parse(text, "scripts/combat_smoke.json");
            SimulationConfig config = new SimulationConfig(new MatchSettings(script.Seed, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                data.Content, script.Players, BuiltInMechanics.CreateRegistry(), data.Tuning, data.Maps[script.MapId ?? "greybox_1v1"]);
            Simulation sim = new Simulation(config);
            sim.Start();
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            List<ulong> hashes = new List<ulong>();
            for (int i = 0; i < script.Ticks; i++)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is StateHashedEvent hashed)
                    {
                        hashes.Add(hashed.Hash);
                    }
                }
            }
            return hashes;
        }
    }
}

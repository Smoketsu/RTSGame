// rts/tests/Sim.Tests/Combat/SplashTests.cs
// Acceptance tests of combat mechanics package 1, area damage (rts/docs/specs/combat_mechanics.md 1.7, A1 to A13), plus the hash coverage of the
// four new pending-hit fields and a pin of a no-splash fight. Part of Rts.Sim.Tests. Uses CombatRig: a 24 x 24 flat arena, attacker A (player 1)
// at (2.5, 10.5), primary T (player 2) at (8.5, 10.5), shipped damage table, splash fraction 0.5, so splashBase = 12 x 0.5 = 6.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Combat;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Combat.CombatRig;

namespace Rts.Sim.Tests.Combat
{
    /// <summary>Blast damage on small hand-built scenes.</summary>
    [TestFixture]
    public class SplashTests
    {
        /// <summary>Definition ID of the attacker (weapon B).</summary>
        private const string Att = "t.att";

        /// <summary>Light, ground, 100 health.</summary>
        private const string Light = "t.light";

        /// <summary>Armoured, ground, 100 health.</summary>
        private const string Armoured = "t.armoured";

        /// <summary>Light, air, 100 health.</summary>
        private const string Flyer = "t.flyer";

        /// <summary>Structure armour, footprint 1, 100 health.</summary>
        private const string Struct = "t.struct";

        /// <summary>Light, ground, 4 health.</summary>
        private const string Frail = "t.frail";

        /// <summary>Dome unit: radius 3, pool 50, regeneration too slow to matter.</summary>
        private const string DomeBig = "t.dome50";

        /// <summary>Dome unit: radius 3, pool 4.</summary>
        private const string DomeSmall = "t.dome4";

        /// <summary>Builds a catalog with weapon B (delay and splash as given) on the attacker and the plain victim types.</summary>
        private static ContentCatalog Catalog(int delay = 0, string splash = "1", bool air = false, bool drench = false, MovementLayer attackerLayer = MovementLayer.Ground)
        {
            ContentCatalog catalog = NewCatalog();
            AddUnit(catalog, Att, attackerLayer, 100, "light", Weapon(12, delay, splash, air, drench));
            AddUnit(catalog, Light, MovementLayer.Ground, 100, "light");
            AddUnit(catalog, Armoured, MovementLayer.Ground, 100, "armoured");
            AddUnit(catalog, Flyer, MovementLayer.Air, 100, "light");
            AddUnit(catalog, Struct, MovementLayer.Ground, 100, "structure", null, 1);
            AddUnit(catalog, Frail, MovementLayer.Ground, 4, "light");
            AddUnit(catalog, DomeBig, MovementLayer.Ground, 100, "light", null, 0, new ShieldDomeDefinition(Fix64.FromInt(3), Fix64.FromInt(50), Fix64.FromInt(60), Fix64.One));
            AddUnit(catalog, DomeSmall, MovementLayer.Ground, 100, "light", null, 0, new ShieldDomeDefinition(Fix64.FromInt(3), Fix64.FromInt(4), Fix64.FromInt(60), Fix64.One));
            return catalog;
        }

        /// <summary>Scene of A1: returns the rig and the IDs [A, T, V1, V2, V3, V4, V5, V6, V7].</summary>
        private static (CombatRig Rig, EntityId[] Ids) SceneA1(ContentCatalog catalog, bool reverseVictims = false)
        {
            CombatRig rig = new CombatRig(catalog);
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            // Victim specs in the spec's order: definition, owner, x, y.
            List<(string Def, PlayerId Owner, string X, string Y)> specs = new List<(string, PlayerId, string, string)>
            {
                (Light, P2, "9.5", "10.5"),
                (Light, P2, "10.0", "10.5"),
                (Light, P2, "10.25", "10.5"),
                (Armoured, P2, "8.5", "11.5"),
                (Light, P1, "8.5", "9.5"),
                (Flyer, P2, "8.5", "12.0"),
                (Struct, P2, "7.5", "10.5"),
            };
            if (reverseVictims)
            {
                specs.Reverse();
            }
            // IDs in the spec's order, filled as victims are spawned.
            EntityId[] ids = new EntityId[9];
            ids[0] = a;
            ids[1] = t;
            for (int i = 0; i < specs.Count; i++)
            {
                (string def, PlayerId owner, string x, string y) = specs[i];
                ids[reverseVictims ? 8 - i : 2 + i] = rig.Add(def, owner, x, y);
            }
            rig.Attack(a, t);
            return (rig, ids);
        }

        /// <summary>A1: one blast, every rule of 1.1 in one scene.</summary>
        [Test]
        public void A1_BasicBlast()
        {
            (CombatRig rig, EntityId[] id) = SceneA1(Catalog());
            rig.Step();
            Assert.That(rig.Health(id[1]), Is.EqualTo(F("91")), "primary 12 x 0.75");
            Assert.That(rig.Health(id[2]), Is.EqualTo(F("95.5")), "V1 at 1.0");
            Assert.That(rig.Health(id[3]), Is.EqualTo(F("95.5")), "V2 exactly on the edge");
            Assert.That(rig.Health(id[4]), Is.EqualTo(F("100")), "V3 at 1.75 is outside");
            Assert.That(rig.Health(id[5]), Is.EqualTo(F("94")), "V4 armoured 6 x 1.0");
            Assert.That(rig.Health(id[6]), Is.EqualTo(F("100")), "V5 is an ally");
            Assert.That(rig.Health(id[7]), Is.EqualTo(F("100")), "V6 flies and the weapon cannot target air");
            Assert.That(rig.Health(id[8]), Is.EqualTo(F("92.5")), "V7 structure 6 x 1.25");
            // Event order after the spawn events.
            List<SimEvent> combat = rig.Log.Where(e => e is ShotFiredEvent || e is UnitDamagedEvent || e is BlastEvent).ToList();
            Assert.That(combat.Count, Is.EqualTo(7));
            Assert.That(combat[0], Is.InstanceOf<ShotFiredEvent>());
            Assert.That(((UnitDamagedEvent)combat[1]).Target, Is.EqualTo(id[1]));
            BlastEvent blast = (BlastEvent)combat[2];
            Assert.That(blast.Victims, Is.EqualTo(4));
            Assert.That(blast.Centre, Is.EqualTo(new FixVec2(F("8.5"), F("10.5"))));
            Assert.That(blast.RadiusTiles, Is.EqualTo(F("1")));
            Assert.That(blast.Attacker, Is.EqualTo(id[0]));
            Assert.That(combat.Skip(3).Select(e => ((UnitDamagedEvent)e).Target).ToList(), Is.EqualTo(new List<EntityId> { id[2], id[3], id[5], id[8] }));
        }

        /// <summary>A2: without splash_radius_tiles only the target is hit, no blast event, and the pending hit carries the empty blast fields.</summary>
        [Test]
        public void A2_NoSplashWeaponUnchanged()
        {
            (CombatRig rig, EntityId[] id) = SceneA1(Catalog(delay: 0, splash: "0"));
            rig.Step();
            Assert.That(rig.Health(id[1]), Is.EqualTo(F("91")));
            foreach (int i in new[] { 2, 3, 4, 5, 6, 7, 8 })
            {
                Assert.That(rig.Health(id[i]), Is.EqualTo(F("100")), "unit " + i);
            }
            Assert.That(rig.Of<BlastEvent>(), Is.Empty);
            // The same scene with a delay keeps the hit queued: its blast fields are the defaults.
            (CombatRig delayed, EntityId[] _) = SceneA1(Catalog(delay: 2, splash: "0"));
            delayed.Step();
            List<PendingHit> due = new List<PendingHit>();
            delayed.Sim.PendingHits.TakeDue(2, due);
            Assert.That(due.Count, Is.EqualTo(1));
            Assert.That(due[0].SourceDefinitionId, Is.EqualTo(string.Empty));
            Assert.That(due[0].SplashDamage, Is.EqualTo(Fix64.Zero));
            Assert.That(due[0].SplashLayers, Is.EqualTo((byte)0));
            Assert.That(due[0].BlastFallback, Is.EqualTo(FixVec2.Zero));
        }

        /// <summary>A3: the target is removed in flight; the blast still lands on tick 2 at the fire-time position.</summary>
        [Test]
        public void A3_DelayedHit_TargetGone()
        {
            CombatRig rig = new CombatRig(Catalog(delay: 2));
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            EntityId v1 = rig.Add(Light, P2, "9.5", "10.5");
            rig.Attack(a, t);
            rig.Remove(1, t);
            rig.Step(2);
            Assert.That(rig.Of<BlastEvent>(), Is.Empty, "nothing lands before tick 2");
            Assert.That(rig.Health(v1), Is.EqualTo(F("100")));
            rig.Step();
            Assert.That(rig.Health(v1), Is.EqualTo(F("95.5")));
            BlastEvent blast = rig.Of<BlastEvent>().Single();
            Assert.That(blast.Tick, Is.EqualTo(2));
            Assert.That(blast.Centre, Is.EqualTo(new FixVec2(F("8.5"), F("10.5"))));
        }

        /// <summary>
        /// A4: the target is moved by a test hook (SetPosition) on tick 1; the blast centres on its tick-2 position. The unit at (9.5, 14.5) is hit,
        /// V1 at (9.5, 10.5) is not.
        /// </summary>
        [Test]
        public void A4_DelayedHit_FollowsTheTarget()
        {
            CombatRig rig = new CombatRig(Catalog(delay: 2));
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            EntityId v1 = rig.Add(Light, P2, "9.5", "10.5");
            EntityId near = rig.Add(Light, P2, "9.5", "14.5");
            rig.Attack(a, t);
            rig.At(1, sim => rig.Get(t).SetPosition(new FixVec2(F("8.5"), F("14.5"))));
            rig.Step(3);
            Assert.That(rig.Health(v1), Is.EqualTo(F("100")));
            Assert.That(rig.Health(near), Is.EqualTo(F("95.5")));
            Assert.That(rig.Of<BlastEvent>().Single().Centre, Is.EqualTo(new FixVec2(F("8.5"), F("14.5"))));
        }

        /// <summary>A5: a weapon that targets air blasts flyers too.</summary>
        [Test]
        public void A5_AirBlastReachesAir()
        {
            (CombatRig rig, EntityId[] id) = SceneA1(Catalog(air: true));
            rig.Step();
            Assert.That(rig.Health(id[7]), Is.EqualTo(F("95.5")));
        }

        /// <summary>A6: an air attacker with a ground and water weapon blasts ground units, not flyers.</summary>
        [Test]
        public void A6_AirAttackerGroundBlast()
        {
            (CombatRig rig, EntityId[] id) = SceneA1(Catalog(attackerLayer: MovementLayer.Air));
            rig.Step();
            Assert.That(rig.Health(id[2]), Is.EqualTo(F("95.5")));
            Assert.That(rig.Health(id[7]), Is.EqualTo(F("100")));
        }

        /// <summary>A7: a blast kill is credited to the attacker; with two attackers the lower ID gets the credit.</summary>
        [Test]
        public void A7_KillCredit()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId a2 = rig.Add(Att, P1, "2.5", "11.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            EntityId v1 = rig.Add(Frail, P2, "9.5", "10.5");
            rig.Attack(a, t);
            rig.Attack(a2, t);
            rig.Step();
            UnitKilledEvent killed = rig.Of<UnitKilledEvent>().Single(k => k.Victim == v1);
            Assert.That(killed.Killer, Is.EqualTo(a));
            Assert.That(rig.Log.OfType<UnitRemovedEvent>().Any(r => r.Unit == v1), Is.True);
            Assert.That(rig.Log.FindIndex(e => e is UnitKilledEvent), Is.LessThan(rig.Log.FindIndex(e => e is UnitRemovedEvent)));
            Assert.That(rig.Sim.Units.Contains(v1), Is.False);
        }

        /// <summary>A8: domes absorb a victim's share only when the blast centre is outside the dome.</summary>
        [Test]
        public void A8_Domes()
        {
            // Part 1: target outside the dome, victim inside it.
            CombatRig rig = new CombatRig(Catalog());
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            EntityId d = rig.Add(DomeBig, P2, "8.5", "14.0");
            EntityId v8 = rig.Add(Light, P2, "8.5", "11.5");
            rig.Attack(a, t);
            rig.Step();
            Assert.That(rig.Health(v8), Is.EqualTo(F("100")));
            Assert.That(rig.Get(d).ShieldPool, Is.EqualTo(F("45.5")));
            ShieldDomeHitEvent hit = rig.Of<ShieldDomeHitEvent>().Single();
            Assert.That(hit.Dome, Is.EqualTo(d));
            Assert.That(hit.Attacker, Is.EqualTo(a));
            Assert.That(hit.Target, Is.EqualTo(v8));
            Assert.That(hit.Absorbed, Is.EqualTo(F("4.5")));
            Assert.That(hit.PoolLeft, Is.EqualTo(F("45.5")));
            // Part 2: the target inside the dome, the shot fired from outside: absorbed whole, no blast.
            CombatRig inside = new CombatRig(Catalog());
            EntityId a2 = inside.Add(Att, P1, "2.5", "10.5");
            EntityId t2 = inside.Add(Light, P2, "7.0", "12.5");
            EntityId d2 = inside.Add(DomeBig, P2, "8.5", "14.0");
            EntityId v82 = inside.Add(Light, P2, "6.0", "12.5");
            inside.Attack(a2, t2);
            inside.Step();
            Assert.That(inside.Get(d2).ShieldPool, Is.EqualTo(F("41")));
            Assert.That(inside.Of<BlastEvent>(), Is.Empty, "a fully absorbed shell makes no blast");
            Assert.That(inside.Health(v82), Is.EqualTo(F("100")));
            Assert.That(inside.Health(t2), Is.EqualTo(F("100")));
            // Part 3: pool 4: the primary is partly absorbed (9 - 4 = 5), the blast falls inside the dome and is not absorbed.
            CombatRig partly = new CombatRig(Catalog());
            EntityId a3 = partly.Add(Att, P1, "2.5", "10.5");
            EntityId t3 = partly.Add(Light, P2, "7.0", "12.5");
            partly.Add(DomeSmall, P2, "8.5", "14.0");
            EntityId v83 = partly.Add(Light, P2, "6.0", "12.5");
            partly.Attack(a3, t3);
            partly.Step();
            Assert.That(partly.Health(t3), Is.EqualTo(F("95")));
            Assert.That(partly.Health(v83), Is.EqualTo(F("95.5")), "the blast centre is inside the dome: no absorption");
            Assert.That(partly.Of<BlastEvent>().Count, Is.EqualTo(1));
        }

        /// <summary>
        /// Pins the current boundary rule of AbsorbByDomes: a victim exactly on a dome's edge (distance 3.0 from its centre, compared squared with
        /// "greater than" to leave) counts as inside, so the dome absorbs its blast share. Not in the spec's A1 to A13.
        /// </summary>
        [Test]
        public void A8b_VictimExactlyOnDomeEdge_IsAbsorbed()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            EntityId d = rig.Add(DomeBig, P2, "8.5", "14.0");
            // Victim 0.5 from the primary and exactly 3.0 from the dome centre (radius 3).
            EntityId v = rig.Add(Light, P2, "8.5", "11.0");
            rig.Attack(a, t);
            rig.Step();
            Assert.That(rig.Health(v), Is.EqualTo(F("100")), "on the edge counts as inside the dome");
            Assert.That(rig.Get(d).ShieldPool, Is.EqualTo(F("45.5")), "50 - 6 x 0.75");
        }

        /// <summary>
        /// Pins the current boundary rule for the blast centre: a centre exactly on a dome's edge (3.0 from its centre) counts as inside, so the
        /// dome does not absorb the blast share of a victim inside it. The attacker stands inside the dome so the primary is not absorbed at fire time.
        /// </summary>
        [Test]
        public void A8c_BlastCentreExactlyOnDomeEdge_IsNotAbsorbed()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId a = rig.Add(Att, P1, "8.5", "15.5");
            EntityId t = rig.Add(Light, P2, "8.5", "11.0");
            EntityId d = rig.Add(DomeBig, P2, "8.5", "14.0");
            // Victim 1.0 from the blast centre and 2.0 from the dome centre.
            EntityId v = rig.Add(Light, P2, "8.5", "12.0");
            rig.Attack(a, t);
            rig.Step();
            Assert.That(rig.Health(t), Is.EqualTo(F("91")), "primary not absorbed: the shot starts inside the dome");
            Assert.That(rig.Health(v), Is.EqualTo(F("95.5")), "blast centre on the edge counts as inside: no absorption");
            Assert.That(rig.Get(d).ShieldPool, Is.EqualTo(F("50")));
            Assert.That(rig.Of<ShieldDomeHitEvent>(), Is.Empty);
        }

        /// <summary>A9: drench applies to the primary only.</summary>
        [Test]
        public void A9_DrenchIsPrimaryOnly()
        {
            ContentCatalog catalog = NewCatalog();
            AddUnit(catalog, Att, MovementLayer.Ground, 100, "light",
                new WeaponDefinition(Fix64.FromInt(12), "blast", Fix64.FromInt(7), Fix64.FromInt(10), false, false, true, true, 0, F("1")));
            AddUnit(catalog, Flyer, MovementLayer.Air, 100, "light");
            CombatRig rig = new CombatRig(catalog);
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId f1 = rig.Add(Flyer, P2, "8.5", "10.5");
            EntityId f2 = rig.Add(Flyer, P2, "8.5", "11.5");
            rig.Attack(a, f1);
            rig.Step();
            Assert.That(rig.Get(f1).Statuses.DrenchHits, Is.EqualTo(1));
            Assert.That(rig.Get(f2).Statuses.DrenchHits, Is.EqualTo(0));
            Assert.That(rig.Health(f2), Is.EqualTo(F("95.5")), "the second flyer still takes splash damage");
            Assert.That(rig.Of<DrenchHitEvent>().Count, Is.EqualTo(1));
        }

        /// <summary>A10: an invulnerable HQ inside the blast takes 0 and is reported with damage 0.</summary>
        [Test]
        public void A10_LastStand()
        {
            ContentCatalog catalog = Catalog();
            CombatRig rig = new CombatRig(catalog);
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            EntityId hq = rig.Add(TestWorld.HqId, P2, "9.5", "10.5");
            rig.At(0, sim => rig.Get(hq).StartLastStand(100));
            rig.Attack(a, t);
            rig.Step();
            Assert.That(rig.Health(hq), Is.EqualTo(F("200")));
            UnitDamagedEvent reported = rig.Of<UnitDamagedEvent>().Single(e => e.Target == hq);
            Assert.That(reported.Damage, Is.EqualTo(Fix64.Zero));
        }

        /// <summary>A11: a damage modifier multiplies the splash base before the victim's armour: V1 takes (6 x 1.5) x 0.75.</summary>
        [Test]
        public void A11_ModifierMultipliesSplashBase()
        {
            CombatRig rig = new CombatRig(Catalog(), F("1.5"));
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            EntityId v1 = rig.Add(Light, P2, "9.5", "10.5");
            rig.Attack(a, t);
            rig.Step();
            Assert.That(rig.Health(t), Is.EqualTo(F("86.5")), "12 x 0.75 x 1.5 = 13.5");
            Assert.That(rig.Health(v1), Is.EqualTo(F("93.25")), "6.75");
        }

        /// <summary>A12: loader bounds and the shipped Stormcaller and Brinecaster.</summary>
        [Test]
        public void A12_Loader()
        {
            const string storm = "units/skai/stormcaller.json";
            foreach (string bad in new[] { "-0.5", "4.5" })
            {
                DataValidationException ex = Expect.Throws<DataValidationException>(() =>
                    DataLoader.Load(TestData.WithEdit(storm, "\"splash_radius_tiles\": 1.0,", "\"splash_radius_tiles\": " + bad + ",")));
                Assert.That(ex.Message, Does.Contain("stormcaller.json"));
                Assert.That(ex.Message, Does.Contain("splash_radius_tiles"));
            }
            foreach (string bad in new[] { "0", "1.5" })
            {
                DataValidationException ex = Expect.Throws<DataValidationException>(() =>
                    DataLoader.Load(TestData.WithEdit("tuning/combat.json", "\"splash_damage_fraction\": 0.5,", "\"splash_damage_fraction\": " + bad + ",")));
                Assert.That(ex.Message, Does.Contain("tuning/combat.json"));
                Assert.That(ex.Message, Does.Contain("splash_damage_fraction"));
            }
            DataSet shipped = TestData.LoadAll();
            Assert.That(shipped.Content.Units["skai.stormcaller"].Weapon!.SplashRadiusTiles, Is.EqualTo(F("1")));
            Assert.That(shipped.Content.Units["water.ink_lobber"].Weapon!.SplashRadiusTiles, Is.EqualTo(F("1")));
            Assert.That(shipped.Content.Units["skai.thunderhead"].Weapon!.SplashRadiusTiles, Is.EqualTo(F("1.25")));
            Assert.That(shipped.Content.Units["water.leviathan"].Weapon!.SplashRadiusTiles, Is.EqualTo(F("2")));
            Assert.That(shipped.Content.Combat.SplashDamageFraction, Is.EqualTo(F("0.5")));
            // Every other shipped weapon has no blast.
            foreach (KeyValuePair<string, UnitDefinition> unit in shipped.Content.Units)
            {
                if (unit.Value.Weapon != null && !new[] { "skai.stormcaller", "water.ink_lobber", "skai.thunderhead", "water.leviathan" }.Contains(unit.Key))
                {
                    Assert.That(unit.Value.Weapon.SplashRadiusTiles, Is.EqualTo(Fix64.Zero), unit.Key);
                }
            }
        }

        /// <summary>A13: spawn order (so IDs) changes nothing but the order of the events; identical runs hash equal; a moved victim changes the hash.</summary>
        [Test]
        public void A13_Determinism()
        {
            (CombatRig forward, EntityId[] f) = SceneA1(Catalog());
            (CombatRig reversed, EntityId[] r) = SceneA1(Catalog(), true);
            forward.Step();
            reversed.Step();
            for (int i = 2; i <= 8; i++)
            {
                Assert.That(reversed.Health(r[i]), Is.EqualTo(forward.Health(f[i])), "position slot " + i);
            }
            List<EntityId> order = reversed.Of<UnitDamagedEvent>().Skip(1).Select(e => e.Target).ToList();
            Assert.That(order, Is.EqualTo(order.OrderBy(x => x).ToList()), "damage events in ascending ID order");
            // Two identical runs, every tick.
            (CombatRig one, EntityId[] _) = SceneA1(Catalog());
            (CombatRig two, EntityId[] _) = SceneA1(Catalog());
            (CombatRig moved, EntityId[] _) = SceneA1(Catalog());
            moved.At(0, sim => moved.Get(new EntityId(3)).SetPosition(new FixVec2(F("9.75"), F("10.5"))));
            // Index of the first tick whose hash differs from the A1 run; -1 when none.
            int firstDifference = -1;
            for (int tick = 0; tick < 12; tick++)
            {
                one.Step();
                two.Step();
                moved.Step();
                Assert.That(two.Sim.ComputeStateHash(), Is.EqualTo(one.Sim.ComputeStateHash()), "tick " + tick);
                if (firstDifference < 0 && moved.Sim.ComputeStateHash() != one.Sim.ComputeStateHash())
                {
                    firstDifference = tick;
                }
            }
            Assert.That(firstDifference, Is.EqualTo(0), "the moved victim shows in the first hash after the shot");
        }

        /// <summary>Each of the four new pending-hit fields alone changes the state hash; the same values give the same hash.</summary>
        [Test]
        public void Hash_CoversTheFourBlastFields()
        {
            // Hash of a fresh match with one queued hit carrying the given blast fields.
            ulong HashWith(string source, string damage, byte layers, string x)
            {
                Simulation sim = TestWorld.Started();
                sim.PendingHits.Enqueue(5, new EntityId(1), new PlayerId(0), new EntityId(2), Fix64.FromInt(10), false, source, F(damage), layers,
                    new FixVec2(F(x), F("3")));
                return sim.ComputeStateHash();
            }
            ulong baseline = HashWith("a.b", "6", 3, "4");
            Assert.That(HashWith("a.b", "6", 3, "4"), Is.EqualTo(baseline));
            Assert.That(HashWith("a.c", "6", 3, "4"), Is.Not.EqualTo(baseline), "SourceDefinitionId");
            Assert.That(HashWith("a.b", "7", 3, "4"), Is.Not.EqualTo(baseline), "SplashDamage");
            Assert.That(HashWith("a.b", "6", 5, "4"), Is.Not.EqualTo(baseline), "SplashLayers");
            Assert.That(HashWith("a.b", "6", 3, "5"), Is.Not.EqualTo(baseline), "BlastFallback");
        }

        /// <summary>The splash fraction and the blast radius reach the content digest (so two peers with different values diverge at tick 0).</summary>
        [Test]
        public void ContentDigest_CoversSplashFractionAndRadius()
        {
            ulong digestFraction = TestData.LoadAll().Content.ComputeDigest();
            ulong other = DataLoader.Load(TestData.WithEdit("tuning/combat.json", "\"splash_damage_fraction\": 0.5,", "\"splash_damage_fraction\": 0.25,")).Content.ComputeDigest();
            Assert.That(other, Is.Not.EqualTo(digestFraction));
            ulong radius = DataLoader.Load(TestData.WithEdit("units/skai/stormcaller.json", "\"splash_radius_tiles\": 1.0,", "\"splash_radius_tiles\": 1.5,")).Content.ComputeDigest();
            Assert.That(radius, Is.Not.EqualTo(digestFraction));
        }
    }
}

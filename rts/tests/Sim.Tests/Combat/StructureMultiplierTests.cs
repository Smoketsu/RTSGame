// rts/tests/Sim.Tests/Combat/StructureMultiplierTests.cs
// Acceptance tests of combat mechanics package 2, the per-weapon structure damage multiplier (rts/docs/specs/combat_mechanics.md 2.4, B1 to B7).
// Part of Rts.Sim.Tests. Uses CombatRig: weapon damage 12 (blast), delay 0, structure_damage_multiplier 1.5; shipped table (blast 0.75 / 1.0 / 1.25
// against light / armoured / structure), attacker A (player 1) at (2.5, 10.5) unless a test moves it.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Combat.CombatRig;

namespace Rts.Sim.Tests.Combat
{
    /// <summary>Structure multiplier on the primary hit and on blast victims.</summary>
    [TestFixture]
    public class StructureMultiplierTests
    {
        /// <summary>Attacker definition ID.</summary>
        private const string Att = "t.att";

        /// <summary>Light ground unit.</summary>
        private const string Light = "t.light";

        /// <summary>Structure armour, footprint 1.</summary>
        private const string Struct = "t.struct";

        /// <summary>HQ with Rooted armour "structure" and Uprooted armour "armoured".</summary>
        private const string Hq = "t.hq";

        /// <summary>Builds the catalog: attacker with the given multiplier (null leaves the key out) and splash radius, light unit, structure, HQ.</summary>
        private static ContentCatalog Catalog(string? multiplier = "1.5", string splash = "0")
        {
            ContentCatalog catalog = NewCatalog();
            AddUnit(catalog, Att, MovementLayer.Ground, 100, "light",
                new WeaponDefinition(Fix64.FromInt(12), "blast", Fix64.FromInt(7), Fix64.FromInt(10), true, true, false, false, 0, F(splash),
                    multiplier == null ? (Fix64?)null : F(multiplier)));
            AddUnit(catalog, Light, MovementLayer.Ground, 100, "light");
            AddUnit(catalog, Struct, MovementLayer.Ground, 100, "structure", null, 1);
            catalog.AddUnit(new UnitDefinition(Hq, Hq, TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(2), 0, Fix64.Half, 0, 0,
                "light", Fix64.FromInt(8), null, new HqDefinition("structure", "armoured", TestWorld.WorkerId), null, null, null));
            return catalog;
        }

        /// <summary>One shot at a target of the given definition at (8.5, 10.5); returns the damage the target took. Unit: health points.</summary>
        private static Fix64 Shoot(ContentCatalog catalog, string target, System.Action<CombatRig, EntityId>? prepare = null)
        {
            CombatRig rig = new CombatRig(catalog);
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(target, P2, "8.5", "10.5");
            prepare?.Invoke(rig, t);
            rig.Attack(a, t);
            rig.Step();
            return F("100") - rig.Health(t);
        }

        /// <summary>B1: against a structure, 12 x 1.25 x 1.5.</summary>
        [Test]
        public void B1_Structure()
        {
            Assert.That(Shoot(Catalog(), Struct), Is.EqualTo(F("22.5")));
        }

        /// <summary>B2: a Rooted HQ in structure armour takes the bonus; the same HQ Uprooted (armoured armour) does not.</summary>
        [Test]
        public void B2_RootedHq()
        {
            // An HQ spawned away from every resource field starts Uprooted, so the test sets the state it wants inside step 2 of tick 0.
            Assert.That(Shoot(Catalog(), Hq, (rig, hq) =>
            {
                rig.At(0, sim => rig.Get(hq).SetHqState(HqState.Rooted, 0));
            }), Is.EqualTo(F("22.5")), "Rooted: structure armour row 1.25 and the bonus");
            Assert.That(Shoot(Catalog(), Hq, (rig, hq) =>
            {
                rig.At(0, sim => rig.Get(hq).SetHqState(HqState.Uprooted, 0));
            }), Is.EqualTo(F("12")), "Uprooted: armoured row 1.0, no bonus");
        }

        /// <summary>B3: a light unit takes the table only.</summary>
        [Test]
        public void B3_LightUnit()
        {
            Assert.That(Shoot(Catalog(), Light), Is.EqualTo(F("9")));
        }

        /// <summary>B4: uphill comes after the structure factor: 12 x 1.25 x 1.5 x 0.75.</summary>
        [Test]
        public void B4_Uphill()
        {
            // Heights, row-major: the east terrace (x of 16 or more) is height 1. No ramps.
            byte[] heights = new byte[24 * 24];
            for (int y = 0; y < 24; y++)
            {
                for (int x = 16; x < 24; x++)
                {
                    heights[y * 24 + x] = 1;
                }
            }
            MapDefinition map = new MapDefinition("arena_terrace", "Arena Terrace", 24, 24, new List<FixVec2> { FixVec2.FromInts(2, 2), FixVec2.FromInts(22, 22) },
                heights, new bool[24 * 24], null, null);
            CombatRig rig = new CombatRig(Catalog(), null, null, map);
            EntityId a = rig.Add(Att, P1, "12.5", "10.5");
            EntityId s = rig.Add(Struct, P2, "17.5", "10.5");
            rig.Attack(a, s);
            rig.Step();
            Assert.That(F("100") - rig.Health(s), Is.EqualTo(F("16.875")));
        }

        /// <summary>B5: blast victims get the factor too: (12 x 0.5) x 1.25 x 1.5 on a structure next to a light primary.</summary>
        [Test]
        public void B5_Splash()
        {
            CombatRig rig = new CombatRig(Catalog(splash: "1"));
            EntityId a = rig.Add(Att, P1, "2.5", "10.5");
            EntityId t = rig.Add(Light, P2, "8.5", "10.5");
            EntityId s = rig.Add(Struct, P2, "7.5", "10.5");
            rig.Attack(a, t);
            rig.Step();
            Assert.That(rig.Health(t), Is.EqualTo(F("91")));
            Assert.That(rig.Health(s), Is.EqualTo(F("88.75")));
        }

        /// <summary>B6: with the key absent the structure takes 15; every shipped weapon loads with 1 except the Leviathan's 1.5.</summary>
        [Test]
        public void B6_Default()
        {
            Assert.That(Shoot(Catalog(null), Struct), Is.EqualTo(F("15")));
            foreach (KeyValuePair<string, UnitDefinition> unit in TestData.LoadAll().Content.Units)
            {
                if (unit.Value.Weapon != null)
                {
                    Assert.That(unit.Value.Weapon.StructureDamageMultiplier, Is.EqualTo(unit.Key == "water.leviathan" ? F("1.5") : Fix64.One), unit.Key);
                }
            }
        }

        /// <summary>B7: 0.05 and 4.5 are rejected naming the key; the value reaches the content digest.</summary>
        [Test]
        public void B7_Loader()
        {
            const string file = "units/water/leviathan.json";
            foreach (string bad in new[] { "0.05", "4.5" })
            {
                DataValidationException ex = Expect.Throws<DataValidationException>(() =>
                    DataLoader.Load(TestData.WithEdit(file, "\"structure_damage_multiplier\": 1.5,", "\"structure_damage_multiplier\": " + bad + ",")));
                Assert.That(ex.Message, Does.Contain("leviathan.json"));
                Assert.That(ex.Message, Does.Contain("structure_damage_multiplier"));
            }
            ulong shipped = TestData.LoadAll().Content.ComputeDigest();
            ulong other = DataLoader.Load(TestData.WithEdit(file, "\"structure_damage_multiplier\": 1.5,", "\"structure_damage_multiplier\": 2,")).Content.ComputeDigest();
            Assert.That(other, Is.Not.EqualTo(shipped));
        }
    }
}

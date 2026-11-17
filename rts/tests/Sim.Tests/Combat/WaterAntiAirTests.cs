// rts/tests/Sim.Tests/Combat/WaterAntiAirTests.cs
// Acceptance tests of combat mechanics package 3, anti-air on water (rts/docs/specs/combat_mechanics.md 3.5, C1 to C10). Part of Rts.Sim.Tests.
// Uses CombatRig: water attacker W (player 1) at (5.5, 5.5) with a kinetic weapon (damage 6, range 4, cooldown 1 s) that has
// can_target_air_on_water, and an enemy flyer F (player 2) at (8.5, 5.5). Water is set on W's tile from a step hook (TerrainGrid.SetWater),
// the way FloodingTests does. Every number is an exact binary fraction, so Fix64 results are exact.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Ai;
using Rts.Sim.Combat;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions.Water;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Combat.CombatRig;

namespace Rts.Sim.Tests.Combat
{
    /// <summary>Tests of the can_target_air_on_water weapon rule.</summary>
    [TestFixture]
    public class WaterAntiAirTests
    {
        /// <summary>Definition ID of the water attacker W.</summary>
        private const string Runner = "t.runner";

        /// <summary>Definition ID of the plain flyer (light, air, 100 health).</summary>
        private const string Flyer = "t.flyer";

        /// <summary>Definition ID of a plain ground enemy (light, 100 health).</summary>
        private const string Ground = "t.ground";

        /// <summary>Tile index of W's tile (5, 5) on the 24 x 24 map. Unit: tile index. Range: 0 to 575.</summary>
        private const int WTile = 5 * 24 + 5;

        /// <summary>Builds the catalog: W with the conditional anti-air weapon (hit delay and blast as given), a flyer and a ground enemy.</summary>
        private static ContentCatalog Catalog(int delay = 0, string splash = "0")
        {
            ContentCatalog catalog = NewCatalog();
            WeaponDefinition weapon = new WeaponDefinition(Fix64.FromInt(6), "kinetic", Fix64.FromInt(4), Fix64.One, true, true, false, false, delay,
                Fix64.Parse(splash), null, true);
            AddUnit(catalog, Runner, MovementLayer.Water, 100, "light", weapon);
            AddUnit(catalog, Flyer, MovementLayer.Air, 100, "light");
            AddUnit(catalog, Ground, MovementLayer.Ground, 100, "light");
            return catalog;
        }

        /// <summary>Sets the water on W's tile to <paramref name="drops"/> drops of <paramref name="team"/> (0 dries it). Runs inside a step.</summary>
        private static void SetWater(Simulation sim, int drops, TeamId team)
        {
            sim.Terrain.SetWater(sim, WTile, FloodMath.ToDepth(drops), team);
        }

        /// <summary>Queues the water on W's tile at the start of <paramref name="tick"/> (a step hook, before combat).</summary>
        private static void WaterAt(CombatRig rig, int tick, int drops, TeamId team)
        {
            rig.At(tick, sim => SetWater(sim, drops, team));
        }

        /// <summary>Shots fired by <paramref name="shooter"/> so far.</summary>
        private static List<ShotFiredEvent> Shots(CombatRig rig, EntityId shooter)
        {
            return rig.Of<ShotFiredEvent>().Where(s => s.Attacker == shooter).ToList();
        }

        /// <summary>C1: on dry ground the order is rejected and the weapon never fires at the flyer, even on automatic acquisition.</summary>
        [Test]
        public void C1_Dry_NoShot()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId w = rig.Add(Runner, P1, "5.5", "5.5");
            EntityId f = rig.Add(Flyer, P2, "8.5", "5.5");
            rig.Attack(w, f);
            rig.Step(20);
            Assert.That(Shots(rig, w), Is.Empty);
            Assert.That(rig.Health(f), Is.EqualTo(F("100")));
            Assert.That(rig.Of<CommandRejectedEvent>().Single().Reason, Is.EqualTo("cannot_target_layer"));
            Assert.That(rig.Get(w).AttackTarget, Is.EqualTo(EntityId.None));
        }

        /// <summary>C2: with water under W the flyer is hit: 6 x 1.0 (kinetic against light) leaves 94.</summary>
        [Test]
        public void C2_Wet_Shoots()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId w = rig.Add(Runner, P1, "5.5", "5.5");
            EntityId f = rig.Add(Flyer, P2, "8.5", "5.5");
            WaterAt(rig, 0, 64, new TeamId(1));
            rig.Attack(w, f);
            rig.Step();
            Assert.That(Shots(rig, w).Count, Is.EqualTo(1));
            Assert.That(rig.Health(f), Is.EqualTo(F("94")));
        }

        /// <summary>C3: an enemy team's water, W's own team's water and ownerless water all count.</summary>
        [Test]
        public void C3_AnyOwnersWaterCounts()
        {
            foreach (TeamId team in new[] { new TeamId(1), new TeamId(2), TeamId.None })
            {
                CombatRig rig = new CombatRig(Catalog());
                EntityId w = rig.Add(Runner, P1, "5.5", "5.5");
                EntityId f = rig.Add(Flyer, P2, "8.5", "5.5");
                // Ownerless water is written as depth with owner None (SetWater keeps None for a depth above 0 only when asked).
                WaterAt(rig, 0, 64, team);
                rig.Attack(w, f);
                rig.Step();
                Assert.That(rig.Health(f), Is.EqualTo(F("94")), "team " + team.Value);
            }
        }

        /// <summary>C4: the tile dries on tick 20 (before combat): W fired once on tick 0, the target is dropped (forced too) and no second shot follows.</summary>
        [Test]
        public void C4_Drying_DropsTheTarget()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId w = rig.Add(Runner, P1, "5.5", "5.5");
            EntityId f = rig.Add(Flyer, P2, "8.5", "5.5");
            WaterAt(rig, 0, 64, new TeamId(1));
            WaterAt(rig, 20, 0, TeamId.None);
            rig.Attack(w, f);
            rig.Step(21);
            Assert.That(Shots(rig, w).Select(s => s.Tick).ToList(), Is.EqualTo(new List<int> { 0 }));
            Assert.That(rig.Get(w).AttackTarget, Is.EqualTo(EntityId.None));
            Assert.That(rig.Health(f), Is.EqualTo(F("94")));
        }

        /// <summary>C5: a hit already in flight lands after the tile dries; no further shot is fired.</summary>
        [Test]
        public void C5_InFlightHitLands()
        {
            CombatRig rig = new CombatRig(Catalog(delay: 2));
            EntityId w = rig.Add(Runner, P1, "5.5", "5.5");
            EntityId f = rig.Add(Flyer, P2, "8.5", "5.5");
            WaterAt(rig, 0, 64, new TeamId(1));
            WaterAt(rig, 1, 0, TeamId.None);
            rig.Attack(w, f);
            rig.Step(2);
            Assert.That(rig.Health(f), Is.EqualTo(F("100")), "not landed before tick 2");
            rig.Step();
            Assert.That(rig.Health(f), Is.EqualTo(F("94")));
            rig.Step(30);
            Assert.That(Shots(rig, w).Count, Is.EqualTo(1));
            Assert.That(rig.Health(f), Is.EqualTo(F("94")));
        }

        /// <summary>C6: attack_unit on a flyer is rejected cannot_target_layer while dry, accepted while wet, and the forced target is dropped when the tile dries.</summary>
        [Test]
        public void C6_Command()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId w = rig.Add(Runner, P1, "5.5", "5.5");
            EntityId f = rig.Add(Flyer, P2, "8.5", "5.5");
            // Dry: rejected on tick 1.
            rig.Sim.Submit(new AttackUnitCommand(1, PlayerId.None, 1000, w, f));
            WaterAt(rig, 2, 64, new TeamId(1));
            // Wet: accepted on tick 3.
            rig.Sim.Submit(new AttackUnitCommand(3, PlayerId.None, 1001, w, f));
            WaterAt(rig, 5, 0, TeamId.None);
            rig.Step(2);
            Assert.That(rig.Of<CommandRejectedEvent>().Select(r => r.Reason).ToList(), Is.EqualTo(new List<string> { "cannot_target_layer" }));
            rig.Step(2);
            Assert.That(rig.Of<CommandRejectedEvent>().Count, Is.EqualTo(1), "accepted while wet");
            Assert.That(rig.Get(w).AttackTarget, Is.EqualTo(f));
            Assert.That(rig.Get(w).IsAttackTargetForced, Is.True);
            rig.Step(2);
            Assert.That(rig.Get(w).AttackTarget, Is.EqualTo(EntityId.None), "dropped on the tick the tile dried");
        }

        /// <summary>C7: a grounded flyer is a ground target: W fires at it on dry ground.</summary>
        [Test]
        public void C7_GroundedFlyer()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId w = rig.Add(Runner, P1, "5.5", "5.5");
            EntityId f = rig.Add(Flyer, P2, "8.5", "5.5");
            rig.At(0, sim => StatusSystem.ApplyGrounded(sim, rig.Get(f), 80, "script", PlayerId.None));
            rig.Attack(w, f);
            rig.Step();
            Assert.That(rig.Get(f).EffectiveLayer, Is.EqualTo(MovementLayer.Ground));
            Assert.That(Shots(rig, w).Count, Is.EqualTo(1));
            Assert.That(rig.Health(f), Is.EqualTo(F("94")));
        }

        /// <summary>C8: the blast's air bit is decided at fire time: wet then dry before landing still hits the flyer (3); dry at fire time does not.</summary>
        [Test]
        public void C8_SplashAirBitDecidedAtFireTime()
        {
            // Wet on tick 0, dried on tick 1; the shot at the ground enemy lands on tick 2.
            CombatRig wet = new CombatRig(Catalog(delay: 2, splash: "1"));
            EntityId w = wet.Add(Runner, P1, "5.5", "5.5");
            EntityId g = wet.Add(Ground, P2, "8.5", "5.5");
            EntityId f2 = wet.Add(Flyer, P2, "9.5", "5.5");
            WaterAt(wet, 0, 64, new TeamId(1));
            WaterAt(wet, 1, 0, TeamId.None);
            wet.Attack(w, g);
            wet.Step(3);
            Assert.That(wet.Health(f2), Is.EqualTo(F("97")), "6 x 0.5 x 1.0");
            Assert.That(wet.Health(g), Is.EqualTo(F("94")));
            // Dry at fire time: the flyer is not in the blast.
            CombatRig dry = new CombatRig(Catalog(delay: 2, splash: "1"));
            EntityId w2 = dry.Add(Runner, P1, "5.5", "5.5");
            EntityId g2 = dry.Add(Ground, P2, "8.5", "5.5");
            EntityId f3 = dry.Add(Flyer, P2, "9.5", "5.5");
            dry.Attack(w2, g2);
            dry.Step(3);
            Assert.That(dry.Health(f3), Is.EqualTo(F("100")));
            Assert.That(dry.Health(g2), Is.EqualTo(F("94")));
            Assert.That(dry.Of<BlastEvent>().Single().Victims, Is.EqualTo(0));
        }

        /// <summary>C9: the loader rejects the flag next to "air" in can_target, and only the Reef Runner and the Shellback carry it.</summary>
        [Test]
        public void C9_Loader()
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() =>
                DataLoader.Load(TestData.WithEdit("units/water/reef_runner.json", "\"can_target\": [\"ground\", \"water\"],", "\"can_target\": [\"air\", \"ground\"],")));
            Assert.That(ex.Message, Does.Contain("reef_runner.json"));
            Assert.That(ex.Message, Does.Contain("can_target_air_on_water"));
            Assert.That(ex.Message, Does.Contain("redundant: can_target already lists air"));
            DataSet shipped = TestData.LoadAll();
            foreach (KeyValuePair<string, UnitDefinition> unit in shipped.Content.Units)
            {
                if (unit.Value.Weapon != null)
                {
                    Assert.That(unit.Value.Weapon.CanTargetAirOnWater, Is.EqualTo(unit.Key == "water.reef_runner" || unit.Key == "water.shellback"), unit.Key);
                }
            }
            // The constructor rejects the redundant flag too.
            Assert.That(Expect.Throws<System.ArgumentException>(() => new WeaponDefinition(Fix64.One, "kinetic", Fix64.One, Fix64.One, true, true, true, false, 0, default, null, true)).Message,
                Does.Contain("redundant"));
        }

        /// <summary>C10: the AI's predictor still reads the static layer list: a Reef Runner does no damage per second to a Roc Rider.</summary>
        [Test]
        public void C10_AiUnchanged()
        {
            DataSet data = TestData.LoadAll();
            Assert.That(FightPredictor.Dps(data.Content.Units["water.reef_runner"], data.Content.Units["skai.roc_rider"], false, data.Content.Combat),
                Is.EqualTo(Fix64.Zero));
            Assert.That(FightPredictor.Dps(data.Content.Units["water.shellback"], data.Content.Units["skai.roc_rider"], false, data.Content.Combat),
                Is.EqualTo(Fix64.Zero));
        }

        /// <summary>The flag changes the content digest (a build without the rule cannot share a match), and the public CanAttack helper follows the water.</summary>
        [Test]
        public void ContentDigestAndCanAttack()
        {
            CombatRig rig = new CombatRig(Catalog());
            EntityId w = rig.Add(Runner, P1, "5.5", "5.5");
            EntityId f = rig.Add(Flyer, P2, "8.5", "5.5");
            WaterAt(rig, 1, 64, new TeamId(1));
            rig.Step();
            Assert.That(CombatSystem.CanAttack(rig.Sim, rig.Get(w), rig.Get(f)), Is.False);
            rig.Step();
            Assert.That(CombatSystem.CanAttack(rig.Sim, rig.Get(w), rig.Get(f)), Is.True);
            Assert.That(CombatSystem.CanAttack(rig.Sim, rig.Get(f), rig.Get(w)), Is.False, "an unarmed unit attacks nothing");
            // Digest with and without the flag differ.
            ContentCatalog on = Catalog();
            ContentCatalog off = NewCatalog();
            AddUnit(off, Runner, MovementLayer.Water, 100, "light", new WeaponDefinition(Fix64.FromInt(6), "kinetic", Fix64.FromInt(4), Fix64.One, true, true, false));
            AddUnit(off, Flyer, MovementLayer.Air, 100, "light");
            AddUnit(off, Ground, MovementLayer.Ground, 100, "light");
            Assert.That(on.ComputeDigest(), Is.Not.EqualTo(off.ComputeDigest()));
        }
    }
}

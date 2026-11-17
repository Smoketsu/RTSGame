// rts/tests/Sim.Tests/Combat/NoSplashFightTests.cs
// Regression pin for a fight with no splash weapons (a delayed lobber, direct shooters, a mortar, a dome, kills; rts/docs/specs/combat_mechanics.md
// 1.1 step 7 and section 5): over 300 ticks the per-tick events and state hashes must keep today's values. The test pins those values only; it cannot
// repeat the before/after comparison. That comparison (the area-damage refactor against the code before it, with the content digest and the four new
// pending-hit fields left out of the hash) was a one-off check: every one of the 300 tick hashes and all events matched, confirmed by the
// reviewer on 18 Nov 2026. Part of Rts.Sim.Tests.
using System.Text;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Combat
{
    /// <summary>A no-splash fight with a pinned hash.</summary>
    [TestFixture]
    public class NoSplashFightTests
    {
        /// <summary>
        /// Runs the scene and folds every tick's events (tick, kind, damage, target) and state hash into one hash. Hits, dome hits and kills must all occur.
        /// Unit: hash value. Range: any ulong.
        /// </summary>
        private static ulong RunScene(out int hits, out int domeHits, out int kills)
        {
            ContentCatalog catalog = TestWorld.Catalog();
            Fix64 ten = Fix64.FromInt(10);
            Fix64 five = Fix64.FromInt(5);
            catalog.AddUnit(new UnitDefinition("t.lob", "L", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0,
                "light", Fix64.FromInt(8), new WeaponDefinition(ten, "kinetic", five, Fix64.One, true, true, false, false, 2)));
            catalog.AddUnit(new UnitDefinition("t.dome", "D", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0,
                "light", Fix64.FromInt(8), null, null, null, null, null,
                new ShieldDomeDefinition(Fix64.FromInt(3), Fix64.FromInt(40), Fix64.FromInt(4), Fix64.FromInt(25))));
            catalog.SetCombatRules(TestWorld.Rules());
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(7, TestWorld.TicksPerSecond, 10), catalog,
                TestWorld.TwoPlayers(), BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.FlatMap()));
            sim.Start();
            string[] firstSide = { "t.lob", "t.lob", TestWorld.ShooterId, TestWorld.ShooterId, TestWorld.MortarId };
            string[] secondSide = { "t.dome", TestWorld.GlassId, TestWorld.TankId, "t.lob", TestWorld.ShooterId, TestWorld.RunnerId };
            // Next command sequence number. Unit: counter. Range: 0 upward.
            uint sequence = 0;
            for (int i = 0; i < firstSide.Length; i++)
            {
                sim.Submit(new SpawnUnitCommand(0, PlayerId.None, sequence++, firstSide[i], new PlayerId(0), FixVec2.FromInts(10, 10 + i)));
            }
            for (int i = 0; i < secondSide.Length; i++)
            {
                sim.Submit(new SpawnUnitCommand(0, PlayerId.None, sequence++, secondSide[i], new PlayerId(1), FixVec2.FromInts(14 + (i % 2), 10 + i)));
            }
            StateHasher folded = new StateHasher();
            hits = 0;
            domeHits = 0;
            kills = 0;
            for (int tick = 0; tick < 300; tick++)
            {
                sim.Step();
                foreach (SimEvent e in sim.Events.Current)
                {
                    folded.Write(e.Tick);
                    folded.Write(e.Kind);
                    if (e is UnitDamagedEvent damaged)
                    {
                        folded.Write(damaged.Damage);
                        folded.Write(damaged.Target);
                        hits++;
                    }
                    if (e is ShieldDomeHitEvent)
                    {
                        domeHits++;
                    }
                    if (e is UnitKilledEvent)
                    {
                        kills++;
                    }
                }
                folded.Write(sim.ComputeStateHash());
            }
            return folded.Hash;
        }

        /// <summary>The scene hashes to the pinned value and exercises hits, the dome and kills.</summary>
        [Test]
        public void NoSplashFight_HashIsPinned()
        {
            ulong first = RunScene(out int hits, out int domeHits, out int kills);
            Assert.That(hits, Is.GreaterThan(10));
            Assert.That(domeHits, Is.GreaterThan(0));
            Assert.That(kills, Is.GreaterThan(0));
            Assert.That(RunScene(out int _, out int _, out int _), Is.EqualTo(first), "two runs agree");
            // Pinned 18 Nov 2026 for combat_mechanics.md package 1 (digest includes splash_damage_fraction and SplashRadiusTiles); re-pinned for package 2 (digest includes StructureDamageMultiplier), old value 3521365824097976314.
            Assert.That(first, Is.EqualTo(18165753124669140004UL), "pin");
        }
    }
}

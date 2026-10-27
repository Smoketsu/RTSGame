// rts/tests/Sim.Tests/Cycles/CycleHashTests.cs
// Acceptance test 18 of rts/docs/specs/planetary_cycles.md section 7 and the hash coverage of section 3: two identical runs match at every
// state_hashed event, a run without the holder's pick differs from the pick on, and every hashed field of the cycle system, changed alone,
// changes the state hash (ADR-005). Part of Rts.Sim.Tests. The per-field cases set the private state through reflection, because the
// system itself only ever changes it inside a step; nothing steps between a change and the hash, so each case isolates one field.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Cycles;
using Rts.Sim.Data;
using Rts.Sim.Events;

namespace Rts.Sim.Tests.Cycles
{
    /// <summary>Cycle and Core state is part of the state hash.</summary>
    [TestFixture]
    public class CycleHashTests
    {
        /// <summary>Hash checkpoints (tick and hash) of the test 9 scenario, optionally without the holder's pick, run for 5000 ticks.</summary>
        private static List<KeyValuePair<int, ulong>> Run(bool withPick)
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles(), hashInterval: 100);
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            if (withPick)
            {
                rig.Pick(1000, CycleWorld.P0, "quake");
            }
            rig.RunThrough(5000);
            List<KeyValuePair<int, ulong>> hashes = new List<KeyValuePair<int, ulong>>();
            foreach (StateHashedEvent evt in rig.Of<StateHashedEvent>())
            {
                hashes.Add(new KeyValuePair<int, ulong>(evt.Tick, evt.Hash));
            }
            return hashes;
        }

        [Test]
        public void TwoIdenticalRuns_MatchAtEveryCheckpoint()
        {
            List<KeyValuePair<int, ulong>> first = Run(true);
            List<KeyValuePair<int, ulong>> second = Run(true);
            Assert.That(first.Count, Is.GreaterThan(40));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ARunWithoutThePick_DiffersFromThePickOn()
        {
            List<KeyValuePair<int, ulong>> with = Run(true);
            List<KeyValuePair<int, ulong>> without = Run(false);
            Assert.That(without.Count, Is.EqualTo(with.Count));
            for (int i = 0; i < with.Count; i++)
            {
                if (with[i].Key < 1000)
                {
                    Assert.That(without[i].Value, Is.EqualTo(with[i].Value), "tick " + with[i].Key + " is before the pick");
                }
                else
                {
                    Assert.That(without[i].Value, Is.Not.EqualTo(with[i].Value), "tick " + with[i].Key + " is after the pick (the pending pick, then the random draw, are hashed)");
                }
            }
        }

        // ---------------------------------------------------------------- per-field coverage

        /// <summary>A started three-team simulation with cycles, the Core and two quake paths, still at tick 0.</summary>
        private static Simulation Build()
        {
            List<QuakePath> paths = new List<QuakePath>
            {
                new QuakePath("one", true, new List<int> { CycleWorld.Tile(5, 5), CycleWorld.Tile(6, 5) }),
                new QuakePath("two", false, new List<int> { CycleWorld.Tile(9, 9) }),
            };
            return new CycleRig(CycleWorld.Players(3), CycleWorld.Map(true, paths), cycles: CycleWorld.ShippedCycles()).Sim;
        }

        /// <summary>Sets a property, including one with a private setter, on the cycle system.</summary>
        private static void SetProperty(object target, string name, object value)
        {
            MethodInfo setter = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetSetMethod(true)!;
            setter.Invoke(target, new[] { value });
        }

        /// <summary>Changes one field of the boxed <see cref="CoreState"/> held in the system's private field and stores it back.</summary>
        private static void SetCoreField(CycleSystem cycles, string field, object value)
        {
            FieldInfo core = typeof(CycleSystem).GetField("_core", BindingFlags.NonPublic | BindingFlags.Instance)!;
            // The state as a box, so the field setter below changes it in place.
            object boxed = core.GetValue(cycles)!;
            boxed.GetType().GetField(field)!.SetValue(boxed, value);
            core.SetValue(cycles, boxed);
        }

        /// <summary>Each case names one field of the hashed list in the spec (section 3) and changes only it.</summary>
        private static List<KeyValuePair<string, Action<CycleSystem>>> Cases()
        {
            return new List<KeyValuePair<string, Action<CycleSystem>>>
            {
                Case("Enabled", c => typeof(CycleSystem).GetField("<IsEnabled>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, false)),
                Case("ActiveCycleId", c => SetProperty(c, "ActiveCycleId", "quake")),
                Case("ActiveSinceTick", c => SetProperty(c, "ActiveSinceTick", 4800)),
                Case("NextCycleId", c => SetProperty(c, "NextCycleId", "quake")),
                Case("NextLocked", c => SetProperty(c, "IsNextLocked", true)),
                Case("PendingPickId", c => c.SetPendingPick("quake", TeamId.None)),
                Case("PendingPickTeam", c => c.SetPendingPick(string.Empty, CycleWorld.T1)),
                Case("Holder", c => SetCoreField(c, "Holder", CycleWorld.T1)),
                Case("CaptureTeam", c => SetCoreField(c, "CaptureTeam", CycleWorld.T2)),
                Case("CaptureProgressTicks", c => SetCoreField(c, "CaptureProgressTicks", 7)),
                Case("IsContested", c => SetProperty(c, "IsContested", true)),
                Case("HoldTicks (team 0)", c => HoldTicks(c)[CycleWorld.T0] = 5),
                Case("HoldTicks (team 2)", c => HoldTicks(c)[CycleWorld.T2] = 9),
                Case("CoreWinner", c => SetProperty(c, "CoreWinner", CycleWorld.T0)),
                Case("quake path IsClosed", c => PathClosed(c)[1] = true),
                Case("quake path tile ClosedByCycle", c => TileClosed(c)[1][0] = true),
            };
        }

        /// <summary>The system's per-team hold tick dictionary.</summary>
        private static SortedDictionary<TeamId, int> HoldTicks(CycleSystem cycles)
        {
            return (SortedDictionary<TeamId, int>)typeof(CycleSystem).GetField("_holdTicks", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cycles)!;
        }

        /// <summary>The system's per-path closed flags.</summary>
        private static bool[] PathClosed(CycleSystem cycles)
        {
            return (bool[])typeof(CycleSystem).GetField("_pathClosed", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cycles)!;
        }

        /// <summary>The system's per-path, per-tile wall flags.</summary>
        private static bool[][] TileClosed(CycleSystem cycles)
        {
            return (bool[][])typeof(CycleSystem).GetField("_tileClosedByCycle", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cycles)!;
        }

        /// <summary>Pairs a case name with its change.</summary>
        private static KeyValuePair<string, Action<CycleSystem>> Case(string name, Action<CycleSystem> change)
        {
            return new KeyValuePair<string, Action<CycleSystem>>(name, change);
        }

        [Test]
        public void TwoFreshWorlds_HashTheSame()
        {
            Assert.That(Build().ComputeStateHash(), Is.EqualTo(Build().ComputeStateHash()));
        }

        [Test]
        public void EveryHashedCycleField_AloneChangesTheStateHash()
        {
            // Names of the cases whose change left the hash unchanged.
            List<string> unhashed = new List<string>();
            foreach (KeyValuePair<string, Action<CycleSystem>> test in Cases())
            {
                Simulation sim = Build();
                ulong before = sim.ComputeStateHash();
                test.Value(sim.Cycles);
                if (sim.ComputeStateHash() == before)
                {
                    unhashed.Add(test.Key);
                }
            }
            Assert.That(unhashed, Is.Empty, "these cycle fields are not in the state hash: " + string.Join(", ", unhashed));
        }

        [Test]
        public void TheModifierIdsAreHashedWhetherOrNotCyclesExist()
        {
            Simulation with = new CycleRig(cycles: CycleWorld.ShippedCycles()).Sim;
            Simulation without = new CycleRig().Sim;
            Assert.That(with.SpeedModifiers[0].Id, Is.EqualTo("cycle"));
            Assert.That(without.SpeedModifiers[0].Id, Is.EqualTo("cycle"), "registered even without cycles, so the hashed lists do not depend on data");
            Assert.That(without.DamageModifiers[0].Id, Is.EqualTo("cycle"));
        }
    }
}

// rts/tests/Sim.Tests/Economy/EconomyTestKit.cs
// Shared helpers for the HQ and economy tests: spawn a unit through a script command, step and collect events, read
// rejection reasons, look units up and load tuning with one value edited. Part of Rts.Sim.Tests. Test code only.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Small building blocks for the economy and HQ test fixtures.</summary>
    internal static class EconomyTestKit
    {
        /// <summary>Player 0, team 0, test faction.</summary>
        public static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1, team 1, Skai faction (enemy of P0).</summary>
        public static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Next command sequence number; unique across the run so no two commands of one issuer collide. Unit: counter. Range: 0 upward.</summary>
        private static uint _sequence;

        /// <summary>A fresh sequence number for a command.</summary>
        public static uint Seq()
        {
            return _sequence++;
        }

        /// <summary>Point at the centre-ish position (x, y) in tiles.</summary>
        public static FixVec2 At(int x, int y)
        {
            return FixVec2.FromInts(x, y);
        }

        /// <summary>Spawns a unit through a script command, steps one tick and returns the new unit's ID.</summary>
        public static EntityId Spawn(Simulation sim, string definition, PlayerId owner, int x, int y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), definition, owner, At(x, y)));
            sim.Step();
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is UnitSpawnedEvent spawned)
                {
                    return spawned.Unit;
                }
            }
            throw new AssertionException("Spawn command produced no UnitSpawnedEvent.");
        }

        /// <summary>Submits one command for the current tick and steps once. Returns the events of that tick.</summary>
        public static List<SimEvent> Do(Simulation sim, Command command)
        {
            sim.Submit(command);
            sim.Step();
            return new List<SimEvent>(sim.Events.Current);
        }

        /// <summary>Steps <paramref name="ticks"/> ticks and returns every event of those ticks in order.</summary>
        public static List<SimEvent> StepCollect(Simulation sim, int ticks)
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

        /// <summary>The rejection reasons among <paramref name="events"/>, in order.</summary>
        public static List<string> Reasons(List<SimEvent> events)
        {
            // Reasons found, in event order.
            List<string> reasons = new List<string>();
            foreach (SimEvent evt in events)
            {
                if (evt is CommandRejectedEvent rejected)
                {
                    reasons.Add(rejected.Reason);
                }
            }
            return reasons;
        }

        /// <summary>Asserts the command was rejected with exactly this one reason.</summary>
        public static void AssertRejected(Simulation sim, Command command, string reason)
        {
            List<string> reasons = Reasons(Do(sim, command));
            Assert.That(reasons, Is.EqualTo(new List<string> { reason }), command.ToString());
        }

        /// <summary>Asserts the command was accepted (no rejection event).</summary>
        public static void AssertAccepted(Simulation sim, Command command)
        {
            List<string> reasons = Reasons(Do(sim, command));
            Assert.That(reasons, Is.Empty, command.ToString());
        }

        /// <summary>The unit with this ID; fails the test when it is gone.</summary>
        public static Unit Get(Simulation sim, EntityId id)
        {
            Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True, id + " should be alive");
            return unit!;
        }

        /// <summary>The live player.</summary>
        public static Rts.Sim.Teams.Player PlayerOf(Simulation sim, PlayerId id)
        {
            Assert.That(sim.Players.TryGet(id, out Rts.Sim.Teams.Player? player), Is.True);
            return player!;
        }

        /// <summary>The shipped tuning with the first occurrence of <paramref name="oldText"/> in <paramref name="path"/> replaced.</summary>
        public static Tuning TuningWithEdit(string path, string oldText, string newText)
        {
            return DataLoader.Load(TestData.WithEdit(path, oldText, newText)).Tuning;
        }

        /// <summary>The events of type T among <paramref name="events"/>, in order.</summary>
        public static List<T> Of<T>(List<SimEvent> events) where T : SimEvent
        {
            // Matching events in order.
            List<T> found = new List<T>();
            foreach (SimEvent evt in events)
            {
                if (evt is T match)
                {
                    found.Add(match);
                }
            }
            return found;
        }
    }
}

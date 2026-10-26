// rts/tests/Sim.Tests/CommandQueueTests.cs
// Tests that commands apply in (tick, issuer, sequence) order regardless of submission order, that late
// and duplicate commands are handled, and that gameplay rejections surface as events. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;

namespace Rts.Sim.Tests
{
    [TestFixture]
    public sealed class CommandQueueTests
    {
        [Test]
        public void TakeForTick_SortsByIssuerThenSequence()
        {
            CommandQueue queue = new CommandQueue();
            queue.Enqueue(new NoOpCommand(5, new PlayerId(1), 2));
            queue.Enqueue(new NoOpCommand(5, new PlayerId(0), 9));
            queue.Enqueue(new NoOpCommand(5, new PlayerId(1), 0));
            queue.Enqueue(new NoOpCommand(6, new PlayerId(0), 0));
            queue.Enqueue(new NoOpCommand(5, new PlayerId(0), 1));
            Assert.That(queue.PendingCount, Is.EqualTo(5));
            Assert.That(queue.EarliestPendingTick(), Is.EqualTo(5));

            List<Command> tick5 = queue.TakeForTick(5);
            string[] order = new string[tick5.Count];
            for (int i = 0; i < tick5.Count; i++)
            {
                order[i] = tick5[i].Issuer + "/" + tick5[i].Sequence;
            }
            Assert.That(order, Is.EqualTo(new[] { "P0/1", "P0/9", "P1/0", "P1/2" }));
            Assert.That(queue.PendingCount, Is.EqualTo(1));
            Assert.That(queue.TakeForTick(5).Count, Is.EqualTo(0));
            Assert.That(queue.TakeForTick(6).Count, Is.EqualTo(1));
            Assert.That(queue.EarliestPendingTick(), Is.EqualTo(-1));
        }

        [Test]
        public void Submit_RejectsPastTicks()
        {
            Simulation sim = TestWorld.Started();
            sim.Step(3);
            Expect.Throws<InvalidOperationException>(() => sim.Submit(new NoOpCommand(2, new PlayerId(0), 0)));
            Expect.DoesNotThrow(() => sim.Submit(new NoOpCommand(3, new PlayerId(0), 0)));
        }

        [Test]
        public void DuplicateSequence_IsRejectedWithEvent()
        {
            Simulation sim = TestWorld.Started();
            // Two different spawns with the same (issuer, sequence) key: both are rejected, whichever arrived first.
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.FromInts(1, 1)));
            sim.Step();
            Assert.That(sim.Units.Count, Is.EqualTo(0));
            Assert.That(CountEvents(sim, "command_rejected"), Is.EqualTo(2));
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is CommandRejectedEvent rejection)
                {
                    Assert.That(rejection.Reason, Is.EqualTo("duplicate_sequence"));
                }
            }
        }

        [Test]
        public void DuplicateSequence_OutcomeDoesNotDependOnSubmissionOrder()
        {
            Simulation forward = TestWorld.Started();
            Simulation reversed = TestWorld.Started();
            SpawnUnitCommand a = new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero);
            SpawnUnitCommand b = new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(1), FixVec2.FromInts(5, 5));
            forward.Submit(a);
            forward.Submit(b);
            reversed.Submit(b);
            reversed.Submit(a);
            forward.Step();
            reversed.Step();
            Assert.That(forward.ComputeStateHash(), Is.EqualTo(reversed.ComputeStateHash()));
            Assert.That(forward.Units.Count, Is.EqualTo(0));
        }

        [Test]
        public void MoveByNonOwner_IsRejected()
        {
            Simulation sim = TestWorld.Started();
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
            sim.Step();
            EntityId unit = new EntityId(1);
            sim.Submit(new MoveUnitCommand(1, new PlayerId(1), 0, unit, FixVec2.FromInts(5, 5)));
            sim.Step();
            Assert.That(FindRejection(sim).Reason, Is.EqualTo("not_owner"));
            sim.Submit(new MoveUnitCommand(2, new PlayerId(0), 0, new EntityId(99), FixVec2.FromInts(5, 5)));
            sim.Step();
            Assert.That(FindRejection(sim).Reason, Is.EqualTo("unknown_unit"));
            sim.Submit(new SpawnUnitCommand(3, PlayerId.None, 1, "nope", new PlayerId(0), FixVec2.Zero));
            sim.Step();
            Assert.That(FindRejection(sim).Reason, Is.EqualTo("unknown_definition"));
            sim.Submit(new SpawnUnitCommand(4, new PlayerId(0), 1, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
            sim.Step();
            Assert.That(FindRejection(sim).Reason, Is.EqualTo("spawn_requires_script_issuer"));
            sim.Submit(new MoveUnitCommand(5, new PlayerId(0), 2, unit, FixVec2.FromInts(20000, 0)));
            sim.Step();
            Assert.That(FindRejection(sim).Reason, Is.EqualTo("out_of_bounds"));
            sim.Submit(new SpawnUnitCommand(6, PlayerId.None, 2, TestWorld.RunnerId, new PlayerId(0), FixVec2.FromInts(0, -20000)));
            sim.Step();
            Assert.That(FindRejection(sim).Reason, Is.EqualTo("out_of_bounds"));
            sim.Submit(new NoOpCommand(7, new PlayerId(7), 0));
            sim.Step();
            Assert.That(FindRejection(sim).Reason, Is.EqualTo("unknown_issuer"));
        }

        [Test]
        public void SubmissionOrder_DoesNotChangeOutcome()
        {
            // Two moves for the same unit on the same tick: the higher sequence wins regardless of submission order.
            Simulation forward = TestWorld.Started();
            Simulation reversed = TestWorld.Started();
            foreach (Simulation sim in new[] { forward, reversed })
            {
                sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.Zero));
                sim.Step();
            }
            MoveUnitCommand first = new MoveUnitCommand(1, new PlayerId(0), 0, new EntityId(1), FixVec2.FromInts(10, 0));
            MoveUnitCommand second = new MoveUnitCommand(1, new PlayerId(0), 1, new EntityId(1), FixVec2.FromInts(0, 10));
            forward.Submit(first);
            forward.Submit(second);
            reversed.Submit(second);
            reversed.Submit(first);
            forward.Step(30);
            reversed.Step(30);
            Assert.That(forward.ComputeStateHash(), Is.EqualTo(reversed.ComputeStateHash()));
            Assert.That(forward.Units.TryGet(new EntityId(1), out var unit), Is.True);
            Assert.That(unit!.MoveTarget, Is.EqualTo(FixVec2.FromInts(0, 10)));
        }

        /// <summary>Counts events of a kind in the last stepped tick.</summary>
        private static int CountEvents(Simulation sim, string kind)
        {
            int count = 0;
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt.Kind == kind)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Returns the first rejection event of the last stepped tick or fails the test.</summary>
        private static CommandRejectedEvent FindRejection(Simulation sim)
        {
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is CommandRejectedEvent rejection)
                {
                    return rejection;
                }
            }
            Assert.Fail("No rejection event this tick.");
            throw new InvalidOperationException();
        }
    }
}

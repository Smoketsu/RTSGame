// rts/tests/Sim.Tests/DeterminismTests.cs
// The Phase 0 exit test: the same scripted command stream run twice gives identical state hashes at every
// hash interval and at the end, and a different seed or different commands give a different hash.
// Part of Rts.Sim.Tests. CI runs this on every push.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;

namespace Rts.Sim.Tests
{
    [TestFixture]
    public sealed class DeterminismTests
    {
        /// <summary>Ticks to run in each scripted match. Range: enough to cover several hash intervals and arrivals.</summary>
        private const int MatchTicks = 400;

        /// <summary>Builds the scripted command stream: script-issued spawns, player-issued moves and a removal.</summary>
        private static List<Command> Script()
        {
            List<Command> script = new List<Command>();
            PlayerId p0 = new PlayerId(0);
            PlayerId p1 = new PlayerId(1);
            // Spawns come from the script issuer (None) with sequences 0 to 9: P0's units get IDs 1 to 5, P1's 6 to 10.
            for (int i = 0; i < 5; i++)
            {
                script.Add(new SpawnUnitCommand(0, PlayerId.None, (uint)i, TestWorld.RunnerId, p0, FixVec2.FromInts(i, 0)));
                script.Add(new SpawnUnitCommand(0, PlayerId.None, (uint)(5 + i), TestWorld.RunnerId, p1, FixVec2.FromInts(i, 20)));
            }
            for (int i = 0; i < 5; i++)
            {
                script.Add(new MoveUnitCommand(1, p0, (uint)i, new EntityId((uint)(i + 1)), FixVec2.FromInts(10 + i, 10)));
                script.Add(new MoveUnitCommand(1, p1, (uint)i, new EntityId((uint)(i + 6)), FixVec2.FromInts(10 + i, 10)));
            }
            script.Add(new RemoveUnitCommand(50, p1, 10, new EntityId(7)));
            script.Add(new MoveUnitCommand(120, p0, 10, new EntityId(1), FixVec2.FromInts(0, 0)));
            return script;
        }

        /// <summary>Runs the script on a fresh simulation and returns every reported hash followed by the final hash.</summary>
        private static List<ulong> Run(ulong seed, List<Command> script)
        {
            Simulation sim = TestWorld.Started(seed);
            foreach (Command command in script)
            {
                sim.Submit(command);
            }
            List<ulong> hashes = new List<ulong>();
            for (int tick = 0; tick < MatchTicks; tick++)
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
            hashes.Add(sim.ComputeStateHash());
            return hashes;
        }

        [Test]
        public void SameScriptTwice_IdenticalHashes()
        {
            List<ulong> first = Run(777, Script());
            List<ulong> second = Run(777, Script());
            Assert.That(first.Count, Is.EqualTo(MatchTicks / TestWorld.HashInterval + 1));
            Assert.That(second, Is.EqualTo(first));
        }

        /// <summary>
        /// Final hash of <see cref="Script"/> with seed 777 after <see cref="MatchTicks"/> ticks, re-pinned on 26 Oct 2026 (Phase 2 step 1) when tuning/online.json gained the lockstep keys input_delay_ticks and catch_up_max_ticks_per_update: the content digest covers the tuning text, so the hash moved without any behaviour change.
        /// A change here means the simulation no longer produces the same match from the same inputs: either a
        /// determinism bug or an intended change to hashing or behaviour. If intended, record why in the changelog and
        /// update the value; replays recorded before the change will no longer re-simulate.
        /// </summary>
        private const ulong GoldenFinalHash = 0xe9d313745512cd15UL;

        [Test]
        public void GoldenHash_IsPinned()
        {
            // Unlike SameScriptTwice, this catches drift between processes, machines and builds, not just within one run.
            List<ulong> hashes = Run(777, Script());
            Assert.That(hashes[hashes.Count - 1], Is.EqualTo(GoldenFinalHash));
        }

        [Test]
        public void DifferentSeed_DifferentHash()
        {
            List<ulong> a = Run(1, Script());
            List<ulong> b = Run(2, Script());
            Assert.That(a[a.Count - 1], Is.Not.EqualTo(b[b.Count - 1]));
        }

        [Test]
        public void DifferentCommands_DifferentHash()
        {
            List<Command> altered = Script();
            altered.Add(new MoveUnitCommand(200, new PlayerId(0), 11, new EntityId(3), FixVec2.FromInts(1, 1)));
            List<ulong> a = Run(5, Script());
            List<ulong> b = Run(5, altered);
            Assert.That(a[a.Count - 1], Is.Not.EqualTo(b[b.Count - 1]));
        }
    }
}

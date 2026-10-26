// rts/tests/Sim.Tests/DeterministicRandomTests.cs
// Tests for the PCG32 generator: reproducibility, range handling and golden values that pin the
// sequence so an accidental change to the algorithm is caught. Part of Rts.Sim.Tests.
using System;
using NUnit.Framework;
using Rts.Sim.Core;

namespace Rts.Sim.Tests
{
    [TestFixture]
    public sealed class DeterministicRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            DeterministicRandom a = new DeterministicRandom(42);
            DeterministicRandom b = new DeterministicRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
            }
            Assert.That(a.State, Is.EqualTo(b.State));
            Assert.That(a.DrawCount, Is.EqualTo(1000UL));
        }

        [Test]
        public void DifferentSeeds_DifferentSequences()
        {
            DeterministicRandom a = new DeterministicRandom(1);
            DeterministicRandom b = new DeterministicRandom(2);
            // Count how many of the first 100 draws coincide; for a good generator this is essentially zero.
            int same = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    same++;
                }
            }
            Assert.That(same, Is.LessThan(3));
        }

        [Test]
        public void GoldenSequence_IsStable()
        {
            // Golden values cross-checked against an independent PCG32 implementation (Python) on 26 Oct 2026. If they change, replays break:
            // treat a failure here as a determinism bug unless the algorithm change is intentional and recorded in an ADR.
            DeterministicRandom rng = new DeterministicRandom(42);
            uint[] expected = { 0xC2F57BD6u, 0x6B07C4A9u, 0x72B7B29Bu, 0x44215383u, 0xF5AF5EADu };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(rng.NextUInt(), Is.EqualTo(expected[i]), "draw " + i);
            }
        }

        [Test]
        public void NextInt_StaysInRange()
        {
            DeterministicRandom rng = new DeterministicRandom(7);
            for (int i = 0; i < 10000; i++)
            {
                int value = rng.NextInt(6);
                Assert.That(value, Is.InRange(0, 5));
            }
            Assert.That(rng.NextInt(1), Is.EqualTo(0));
            Expect.Throws<ArgumentOutOfRangeException>(() => { rng.NextInt(0); });
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(rng.NextIntInclusive(-3, 3), Is.InRange(-3, 3));
            }
            Assert.That(rng.NextIntInclusive(int.MinValue, int.MaxValue), Is.InRange(int.MinValue, int.MaxValue));
        }

        [Test]
        public void NextFix64Unit_IsInHalfOpenUnitInterval()
        {
            DeterministicRandom rng = new DeterministicRandom(99);
            for (int i = 0; i < 10000; i++)
            {
                Fix64 value = rng.NextFix64Unit();
                Assert.That(value >= Fix64.Zero && value < Fix64.One, Is.True);
            }
        }

        [Test]
        public void Restore_ResumesTheSequence()
        {
            DeterministicRandom a = new DeterministicRandom(5);
            a.NextUInt();
            a.NextUInt();
            DeterministicRandom b = new DeterministicRandom(0);
            b.Restore(a.State, a.DrawCount);
            Assert.That(b.NextUInt(), Is.EqualTo(a.NextUInt()));
            Assert.That(b.DrawCount, Is.EqualTo(3UL));
        }
    }
}

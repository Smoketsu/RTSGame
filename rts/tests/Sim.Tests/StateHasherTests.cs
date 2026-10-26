// rts/tests/Sim.Tests/StateHasherTests.cs
// Tests for the FNV-1a state hasher: known vectors, order sensitivity and type encodings.
// Part of Rts.Sim.Tests.
using NUnit.Framework;
using Rts.Sim.Core;

namespace Rts.Sim.Tests
{
    [TestFixture]
    public sealed class StateHasherTests
    {
        [Test]
        public void EmptyStream_IsOffsetBasis()
        {
            StateHasher hasher = new StateHasher();
            Assert.That(hasher.Hash, Is.EqualTo(0xcbf29ce484222325UL));
            Assert.That(hasher.BytesWritten, Is.EqualTo(0L));
        }

        [Test]
        public void KnownVector_SingleByteA()
        {
            // FNV-1a 64 of the single byte 'a' is 0xaf63dc4c8601ec8c (published test vector).
            StateHasher hasher = new StateHasher();
            hasher.Write((byte)'a');
            Assert.That(hasher.Hash, Is.EqualTo(0xaf63dc4c8601ec8cUL));
        }

        [Test]
        public void KnownVector_Foobar()
        {
            // FNV-1a 64 of "foobar" as bytes is 0x85944171f73967e8 (published test vector).
            StateHasher hasher = new StateHasher();
            foreach (char c in "foobar")
            {
                hasher.Write((byte)c);
            }
            Assert.That(hasher.Hash, Is.EqualTo(0x85944171f73967e8UL));
        }

        [Test]
        public void Order_Matters()
        {
            StateHasher a = new StateHasher();
            a.Write(1);
            a.Write(2);
            StateHasher b = new StateHasher();
            b.Write(2);
            b.Write(1);
            Assert.That(a.Hash, Is.Not.EqualTo(b.Hash));
            Assert.That(a.BytesWritten, Is.EqualTo(8L));
        }

        [Test]
        public void Encodings_AreLittleEndianAndTyped()
        {
            StateHasher viaInt = new StateHasher();
            viaInt.Write(0x04030201);
            StateHasher viaBytes = new StateHasher();
            viaBytes.Write((byte)1);
            viaBytes.Write((byte)2);
            viaBytes.Write((byte)3);
            viaBytes.Write((byte)4);
            Assert.That(viaInt.Hash, Is.EqualTo(viaBytes.Hash));

            StateHasher viaFix = new StateHasher();
            viaFix.Write(Fix64.One);
            StateHasher viaLong = new StateHasher();
            viaLong.Write(1L << 32);
            Assert.That(viaFix.Hash, Is.EqualTo(viaLong.Hash));

            StateHasher nullString = new StateHasher();
            nullString.Write((string?)null);
            StateHasher emptyString = new StateHasher();
            emptyString.Write(string.Empty);
            Assert.That(nullString.Hash, Is.Not.EqualTo(emptyString.Hash));
        }

        [Test]
        public void Reset_ReturnsToEmptyState()
        {
            StateHasher hasher = new StateHasher();
            hasher.Write(123UL);
            hasher.Reset();
            Assert.That(hasher.Hash, Is.EqualTo(StateHasher.OffsetBasis));
            Assert.That(hasher.BytesWritten, Is.EqualTo(0L));
        }
    }
}

// rts/tests/Sim.Tests/Fix64Tests.cs
// Unit tests for the Q32.32 fixed-point type: exact arithmetic, saturation, parsing, square root.
// Part of Rts.Sim.Tests. Expected raw values are computed by hand from the Q32.32 definition.
using System;
using NUnit.Framework;
using Rts.Sim.Core;

namespace Rts.Sim.Tests
{
    [TestFixture]
    public sealed class Fix64Tests
    {
        /// <summary>Raw value of 1.0, for building expectations.</summary>
        private const long One = 1L << 32;

        [Test]
        public void FromInt_IsExact()
        {
            Assert.That(Fix64.FromInt(3).Raw, Is.EqualTo(3 * One));
            Assert.That(Fix64.FromInt(-3).Raw, Is.EqualTo(-3 * One));
            Assert.That(Fix64.FromInt(int.MinValue).FloorToInt(), Is.EqualTo(int.MinValue));
        }

        [Test]
        public void Parse_ReadsDecimalsExactly()
        {
            Assert.That(Fix64.Parse("210").Raw, Is.EqualTo(210 * One));
            Assert.That(Fix64.Parse("1.5").Raw, Is.EqualTo(One + One / 2));
            Assert.That(Fix64.Parse("-1.5").Raw, Is.EqualTo(-(One + One / 2)));
            Assert.That(Fix64.Parse("0.25").Raw, Is.EqualTo(One / 4));
            // 0.04 × 2^32 = 171798691.84, rounded half up to 171798692.
            Assert.That(Fix64.Parse("0.04").Raw, Is.EqualTo(171798692L));
            Assert.That(Fix64.Parse("0.0").Raw, Is.EqualTo(0L));
        }

        [Test]
        public void Parse_RejectsBadText()
        {
            Expect.Throws<FormatException>(() => Fix64.Parse(""));
            Expect.Throws<FormatException>(() => Fix64.Parse("1e5"));
            Expect.Throws<FormatException>(() => Fix64.Parse("+1"));
            Expect.Throws<FormatException>(() => Fix64.Parse("1."));
            Expect.Throws<FormatException>(() => Fix64.Parse(".5"));
            Expect.Throws<FormatException>(() => Fix64.Parse("1.0000000001"));
            Expect.Throws<FormatException>(() => Fix64.Parse("2147483648"));
            Expect.DoesNotThrow(() => Fix64.Parse("-2147483648"));
        }

        [Test]
        public void ToString_RoundTripsThroughParse()
        {
            string[] samples = { "0", "1", "-1", "1.5", "-0.25", "210", "0.04", "123456.789" };
            foreach (string sample in samples)
            {
                Fix64 value = Fix64.Parse(sample);
                Assert.That(Fix64.Parse(value.ToString()).Raw, Is.EqualTo(value.Raw).Within(1), sample);
            }
            Assert.That(Fix64.FromInt(7).ToString(), Is.EqualTo("7"));
            Assert.That(Fix64.Parse("1.5").ToString(), Is.EqualTo("1.5"));
        }

        [Test]
        public void Addition_SaturatesInsteadOfWrapping()
        {
            Assert.That((Fix64.MaxValue + Fix64.One).Raw, Is.EqualTo(long.MaxValue));
            Assert.That((Fix64.MinValue - Fix64.One).Raw, Is.EqualTo(long.MinValue));
            Assert.That((Fix64.MinValue + Fix64.MaxValue).Raw, Is.EqualTo(-1L));
            Assert.That((-Fix64.MinValue).Raw, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void Multiplication_IsExactForRepresentableProducts()
        {
            Assert.That((Fix64.Parse("1.5") * Fix64.FromInt(2)).Raw, Is.EqualTo(3 * One));
            Assert.That((Fix64.Parse("-1.5") * Fix64.FromInt(2)).Raw, Is.EqualTo(-3 * One));
            Assert.That((Fix64.Parse("-1.5") * Fix64.FromInt(-2)).Raw, Is.EqualTo(3 * One));
            Assert.That((Fix64.Half * Fix64.Half).Raw, Is.EqualTo(One / 4));
            Assert.That((Fix64.FromInt(46340) * Fix64.FromInt(46340)).Raw, Is.EqualTo(2147395600L * One));
            // 0.1 × 0.1 truncates toward zero: parse(0.1)=429496730 raw; product raw = floor(429496730^2 / 2^32) = 42949673.
            Assert.That((Fix64.Parse("0.1") * Fix64.Parse("0.1")).Raw, Is.EqualTo(42949673L));
        }

        [Test]
        public void Multiplication_SaturatesOnOverflow()
        {
            Assert.That((Fix64.FromInt(65536) * Fix64.FromInt(65536)).Raw, Is.EqualTo(long.MaxValue));
            Assert.That((Fix64.FromInt(-65536) * Fix64.FromInt(65536)).Raw, Is.EqualTo(long.MinValue));
            Assert.That((Fix64.MaxValue * Fix64.FromInt(2)).Raw, Is.EqualTo(long.MaxValue));
            Assert.That((Fix64.MinValue * Fix64.MinValue).Raw, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void Division_TruncatesTowardZero()
        {
            Assert.That((Fix64.FromInt(1) / Fix64.FromInt(3)).Raw, Is.EqualTo(One / 3));
            Assert.That((Fix64.FromInt(-1) / Fix64.FromInt(3)).Raw, Is.EqualTo(-(One / 3)));
            Assert.That((Fix64.FromInt(6) / Fix64.FromInt(3)).Raw, Is.EqualTo(2 * One));
            Assert.That((Fix64.FromInt(1) / Fix64.FromInt(20)).Raw, Is.EqualTo(One / 20));
            Assert.That((Fix64.FromInt(1) / Fix64.Parse("0.5")).Raw, Is.EqualTo(2 * One));
            Expect.Throws<DivideByZeroException>(() => { Fix64 unused = Fix64.One / Fix64.Zero; Assert.That(unused, Is.Not.EqualTo(Fix64.Zero)); });
            Assert.That((Fix64.MaxValue / Fix64.Parse("0.5")).Raw, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void Sqrt_IsExactToTheLastBit()
        {
            Assert.That(Fix64.Sqrt(Fix64.FromInt(4)).Raw, Is.EqualTo(2 * One));
            Assert.That(Fix64.Sqrt(Fix64.FromInt(0)).Raw, Is.EqualTo(0L));
            Assert.That(Fix64.Sqrt(Fix64.Parse("0.25")).Raw, Is.EqualTo(One / 2));
            // floor(sqrt(2) × 2^32) = 6074000999.
            Assert.That(Fix64.Sqrt(Fix64.FromInt(2)).Raw, Is.EqualTo(6074000999L));
            // floor(sqrt(2147483647.0000000002) × 2^32) = 199032864720089 and floor(sqrt(MaxValue) × 2^32) = 199032864766430, cross-checked with Python isqrt.
            Assert.That(Fix64.Sqrt(Fix64.FromInt(int.MaxValue) + Fix64.Epsilon).Raw, Is.EqualTo(199032864720089L));
            Assert.That(Fix64.Sqrt(Fix64.MaxValue).Raw, Is.EqualTo(199032864766430L));
            Expect.Throws<ArgumentOutOfRangeException>(() => { Fix64.Sqrt(Fix64.FromInt(-1)); });
        }

        [Test]
        public void Rounding_FollowsDocumentedRules()
        {
            Assert.That(Fix64.Parse("1.5").FloorToInt(), Is.EqualTo(1));
            Assert.That(Fix64.Parse("-1.5").FloorToInt(), Is.EqualTo(-2));
            Assert.That(Fix64.Parse("1.5").CeilingToInt(), Is.EqualTo(2));
            Assert.That(Fix64.Parse("1.5").RoundToInt(), Is.EqualTo(2));
            Assert.That(Fix64.Parse("1.25").RoundToInt(), Is.EqualTo(1));
            Assert.That(Fix64.Parse("-1.5").RoundToInt(), Is.EqualTo(-1));
            Assert.That(Fix64.MaxValue.RoundToInt(), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void MinMaxClampAbsSign()
        {
            Assert.That(Fix64.Min(Fix64.One, Fix64.Zero), Is.EqualTo(Fix64.Zero));
            Assert.That(Fix64.Max(Fix64.One, Fix64.Zero), Is.EqualTo(Fix64.One));
            Assert.That(Fix64.Clamp(Fix64.FromInt(5), Fix64.Zero, Fix64.One), Is.EqualTo(Fix64.One));
            Expect.Throws<ArgumentException>(() => { Fix64.Clamp(Fix64.Zero, Fix64.One, Fix64.Zero); });
            Assert.That(Fix64.FromInt(-3).Abs(), Is.EqualTo(Fix64.FromInt(3)));
            Assert.That(Fix64.MinValue.Abs(), Is.EqualTo(Fix64.MaxValue));
            Assert.That(Fix64.FromInt(-3).Sign(), Is.EqualTo(-1));
            Assert.That(Fix64.Zero.Sign(), Is.EqualTo(0));
        }

        [Test]
        public void FixVec2_MoveTowards_ReachesTargetExactly()
        {
            FixVec2 start = FixVec2.Zero;
            FixVec2 target = FixVec2.FromInts(3, 4);
            FixVec2 step = start.MoveTowards(target, Fix64.One);
            // Length 5, so one unit along the direction is (0.6, 0.8).
            Assert.That(step.X.Raw, Is.EqualTo(Fix64.Parse("0.6").Raw).Within(2));
            Assert.That(step.Y.Raw, Is.EqualTo(Fix64.Parse("0.8").Raw).Within(2));
            Assert.That(start.MoveTowards(target, Fix64.FromInt(5)), Is.EqualTo(target));
            Assert.That(start.MoveTowards(target, Fix64.FromInt(50)), Is.EqualTo(target));
            Assert.That(target.Length(), Is.EqualTo(Fix64.FromInt(5)));
        }
    }
}

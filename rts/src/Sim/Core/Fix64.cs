// rts/src/Sim/Core/Fix64.cs
// Q32.32 fixed-point number: the only non-integer number type allowed in the simulation.
// Part of Rts.Sim.Core. Chosen in docs/decisions/ADR-001-fixed-point.md. Every position, speed,
// health value and tuning number in the simulation is a Fix64 so that results are bit-identical
// on every machine. Floating point never enters: data is parsed from decimal text (Parse) and
// the only float conversion is for presentation (ToFloatForPresentation).
using System;

namespace Rts.Sim.Core
{
    /// <summary>
    /// Signed fixed-point number with 32 integer bits and 32 fraction bits stored in one <see cref="long"/>.
    /// Range: about -2 147 483 648 to +2 147 483 647.999 999 999 8. Resolution: 2^-32 (about 2.3e-10).
    /// Add, subtract and multiply saturate at <see cref="MaxValue"/> / <see cref="MinValue"/>; they never wrap.
    /// Multiply and divide truncate toward zero. Division by zero throws.
    /// </summary>
    public readonly struct Fix64 : IEquatable<Fix64>, IComparable<Fix64>
    {
        /// <summary>Number of fraction bits. Raw value = real value × 2^FractionBits. Unit: bits. Range: fixed at 32.</summary>
        public const int FractionBits = 32;

        /// <summary>Raw representation of 1.0: 2^32. Range: fixed at 2^32.</summary>
        private const long OneRaw = 1L << FractionBits;

        /// <summary>Mask selecting the fraction bits of a raw value. Range: 0 to 2^32 - 1 (raw units).</summary>
        private const long FractionMask = OneRaw - 1;

        /// <summary>Most digits after the decimal point that <see cref="Parse"/> accepts. 9 digits keep the scaled numerator below 2^62. Range: fixed at 9.</summary>
        private const int MaxParseFractionDigits = 9;

        /// <summary>The raw scaled value: real value × 2^32. Full <see cref="long"/> range.</summary>
        public readonly long Raw;

        /// <summary>Zero (raw 0). Range: exactly 0.</summary>
        public static readonly Fix64 Zero = new Fix64(0L);

        /// <summary>One (raw 2^32). Range: exactly 1.0.</summary>
        public static readonly Fix64 One = new Fix64(OneRaw);

        /// <summary>One half (raw 2^31). Range: exactly 0.5.</summary>
        public static readonly Fix64 Half = new Fix64(OneRaw >> 1);

        /// <summary>Largest representable value, about +2 147 483 647.999 999 999 8. Range: raw long.MaxValue.</summary>
        public static readonly Fix64 MaxValue = new Fix64(long.MaxValue);

        /// <summary>Smallest representable value, exactly -2 147 483 648. Range: raw long.MinValue.</summary>
        public static readonly Fix64 MinValue = new Fix64(long.MinValue);

        /// <summary>Smallest positive value, 2^-32 (raw 1). Range: exactly 2^-32.</summary>
        public static readonly Fix64 Epsilon = new Fix64(1L);

        /// <summary>Builds a value from its raw scaled representation. Private: callers use <see cref="FromRaw"/> to make the intent visible.</summary>
        /// <param name="raw">Real value × 2^32. Any <see cref="long"/>.</param>
        private Fix64(long raw)
        {
            Raw = raw;
        }

        /// <summary>Wraps a raw scaled value (real value × 2^32) without any checks. Used by hashing, serialisation and tests.</summary>
        /// <param name="raw">Real value × 2^32. Any <see cref="long"/>.</param>
        public static Fix64 FromRaw(long raw)
        {
            return new Fix64(raw);
        }

        /// <summary>Converts a whole number exactly.</summary>
        /// <param name="value">Any <see cref="int"/>; every int is representable.</param>
        public static Fix64 FromInt(int value)
        {
            return new Fix64((long)value << FractionBits);
        }

        /// <summary>
        /// Builds the exact ratio <paramref name="numerator"/> / <paramref name="denominator"/> truncated toward zero, without passing through floating point.
        /// </summary>
        /// <param name="numerator">Any int.</param>
        /// <param name="denominator">Any non-zero int. Zero throws <see cref="DivideByZeroException"/>.</param>
        public static Fix64 FromFraction(int numerator, int denominator)
        {
            return FromInt(numerator) / FromInt(denominator);
        }

        /// <summary>The whole part, truncated toward negative infinity (floor). Range: -2^31 to 2^31-1.</summary>
        public int FloorToInt()
        {
            return (int)(Raw >> FractionBits);
        }

        /// <summary>The whole part rounded toward positive infinity (ceiling). Saturates at <see cref="int.MaxValue"/> for the top value.</summary>
        public int CeilingToInt()
        {
            // Floor of the value, plus one when any fraction bit is set.
            long floor = Raw >> FractionBits;
            // Whole part rounded up. Unit: whole units. Range: any long before the int clamp.
            long result = (Raw & FractionMask) != 0 ? floor + 1 : floor;
            return result > int.MaxValue ? int.MaxValue : (int)result;
        }

        /// <summary>The nearest whole number, halves rounded toward positive infinity. Saturates at <see cref="int.MaxValue"/>.</summary>
        public int RoundToInt()
        {
            // Add one half then floor; saturate instead of wrapping near the top of the range.
            long shifted = Raw > long.MaxValue - Half.Raw ? long.MaxValue : Raw + Half.Raw;
            // Whole part of value plus one half. Unit: whole units. Range: any long before the int clamp.
            long result = shifted >> FractionBits;
            return result > int.MaxValue ? int.MaxValue : (int)result;
        }

        /// <summary>Largest whole value not greater than this one, as a <see cref="Fix64"/>.</summary>
        public Fix64 Floor()
        {
            return new Fix64(Raw & ~FractionMask);
        }

        /// <summary>Absolute value. <see cref="MinValue"/> saturates to <see cref="MaxValue"/>.</summary>
        public Fix64 Abs()
        {
            if (Raw == long.MinValue)
            {
                return MaxValue;
            }
            return Raw < 0 ? new Fix64(-Raw) : this;
        }

        /// <summary>-1, 0 or +1 depending on the sign of the value.</summary>
        public int Sign()
        {
            return Raw < 0 ? -1 : (Raw > 0 ? 1 : 0);
        }

        /// <summary>The smaller of two values.</summary>
        public static Fix64 Min(Fix64 a, Fix64 b)
        {
            return a.Raw < b.Raw ? a : b;
        }

        /// <summary>The larger of two values.</summary>
        public static Fix64 Max(Fix64 a, Fix64 b)
        {
            return a.Raw > b.Raw ? a : b;
        }

        /// <summary>Clamps <paramref name="value"/> into [<paramref name="min"/>, <paramref name="max"/>]. Throws if min is greater than max.</summary>
        public static Fix64 Clamp(Fix64 value, Fix64 min, Fix64 max)
        {
            if (min.Raw > max.Raw)
            {
                throw new ArgumentException("Fix64.Clamp: min must not exceed max.");
            }
            if (value.Raw < min.Raw)
            {
                return min;
            }
            return value.Raw > max.Raw ? max : value;
        }

        /// <summary>Saturating addition: the result never wraps past <see cref="MaxValue"/> or <see cref="MinValue"/>.</summary>
        public static Fix64 operator +(Fix64 a, Fix64 b)
        {
            // Sum computed without overflow checks, then the sign of the operands reveals a wrap.
            long sum = unchecked(a.Raw + b.Raw);
            // Overflow happened when both operands share a sign and the sum has the other sign.
            bool overflow = ((a.Raw ^ sum) & (b.Raw ^ sum)) < 0;
            if (!overflow)
            {
                return new Fix64(sum);
            }
            return a.Raw < 0 ? MinValue : MaxValue;
        }

        /// <summary>Saturating subtraction.</summary>
        public static Fix64 operator -(Fix64 a, Fix64 b)
        {
            // Raw difference a.Raw - b.Raw. Unit: raw units (2^-32 each). Range: wraps on overflow; checked below.
            long diff = unchecked(a.Raw - b.Raw);
            // Overflow happened when the operands differ in sign and the result's sign differs from a's.
            bool overflow = ((a.Raw ^ b.Raw) & (a.Raw ^ diff)) < 0;
            if (!overflow)
            {
                return new Fix64(diff);
            }
            return a.Raw < 0 ? MinValue : MaxValue;
        }

        /// <summary>Negation. <see cref="MinValue"/> saturates to <see cref="MaxValue"/>.</summary>
        public static Fix64 operator -(Fix64 a)
        {
            return a.Raw == long.MinValue ? MaxValue : new Fix64(-a.Raw);
        }

        /// <summary>
        /// Saturating multiplication, truncated toward zero. Uses a full 128-bit intermediate product on the
        /// magnitudes, so no precision is lost before the final shift.
        /// </summary>
        public static Fix64 operator *(Fix64 a, Fix64 b)
        {
            // Sign of the result: negative when exactly one operand is negative.
            bool negative = (a.Raw < 0) != (b.Raw < 0);
            // Magnitudes as unsigned 64-bit values; unchecked negation maps long.MinValue to 2^63 correctly.
            ulong am = a.Raw < 0 ? unchecked((ulong)(-a.Raw)) : (ulong)a.Raw;
            // Magnitude of b as unsigned raw units. Unit: raw units (2^-32). Range: 0 to 2^63.
            ulong bm = b.Raw < 0 ? unchecked((ulong)(-b.Raw)) : (ulong)b.Raw;

            // Split each magnitude into 32-bit halves: m = m1 × 2^32 + m0.
            ulong a0 = am & 0xFFFFFFFFUL;
            // High 32 bits of the a magnitude. Unit: 32-bit word. Range: 0 to 2^31.
            ulong a1 = am >> 32;
            // Low 32 bits of the b magnitude. Unit: 32-bit word. Range: 0 to 2^32 - 1.
            ulong b0 = bm & 0xFFFFFFFFUL;
            // High 32 bits of the b magnitude. Unit: 32-bit word. Range: 0 to 2^31.
            ulong b1 = bm >> 32;

            // Partial products; each fits in 64 bits because every half is below 2^32.
            ulong p00 = a0 * b0;
            // Partial product a0 × b1 of two 32-bit halves. Unit: raw product. Range: below 2^64.
            ulong p01 = a0 * b1;
            // Partial product a1 × b0 of two 32-bit halves. Unit: raw product. Range: below 2^64.
            ulong p10 = a1 * b0;
            // Partial product a1 × b1 of the high halves. Unit: raw product. Range: below 2^62.
            ulong p11 = a1 * b1;

            // Middle column: carries from p00 plus the low halves of the cross products. Below 2^34, so no overflow.
            ulong mid = (p00 >> 32) + (p01 & 0xFFFFFFFFUL) + (p10 & 0xFFFFFFFFUL);
            // Low 64 bits of the 128-bit product.
            ulong lo = (p00 & 0xFFFFFFFFUL) | (mid << 32);
            // High 64 bits of the 128-bit product. p11 is below 2^62 because a1 and b1 are at most 2^31, so this cannot overflow.
            ulong hi = p11 + (p01 >> 32) + (p10 >> 32) + (mid >> 32);

            // The fixed-point result is the product shifted right by 32 bits: take bits 32..95.
            if ((hi >> 31) != 0)
            {
                // Bits above 95 are set: the magnitude does not fit in 63 bits.
                return negative ? MinValue : MaxValue;
            }
            // Product bits 32 to 95 as an unsigned raw magnitude. Unit: raw units. Range: 0 to 2^64 - 1, checked below.
            ulong magnitude = (hi << 32) | (lo >> 32);
            if (magnitude > (ulong)long.MaxValue)
            {
                return negative ? MinValue : MaxValue;
            }
            // Signed raw product before the sign is applied. Unit: raw units. Range: 0 to long.MaxValue.
            long raw = (long)magnitude;
            return new Fix64(negative ? -raw : raw);
        }

        /// <summary>
        /// Division truncated toward zero. Saturates when the quotient does not fit. Throws <see cref="DivideByZeroException"/> for a zero divisor.
        /// </summary>
        public static Fix64 operator /(Fix64 a, Fix64 b)
        {
            if (b.Raw == 0)
            {
                throw new DivideByZeroException("Fix64 division by zero.");
            }
            // True when exactly one operand is negative, so the quotient is negative.
            bool negative = (a.Raw < 0) != (b.Raw < 0);
            // Magnitude of a as unsigned raw units. Unit: raw units (2^-32). Range: 0 to 2^63.
            ulong am = a.Raw < 0 ? unchecked((ulong)(-a.Raw)) : (ulong)a.Raw;
            // Magnitude of b as unsigned raw units. Unit: raw units (2^-32). Range: 1 to 2^63 (b is not zero).
            ulong bm = b.Raw < 0 ? unchecked((ulong)(-b.Raw)) : (ulong)b.Raw;

            // Whole part of the quotient in raw units is (am / bm) << 32; it must fit in 31 bits.
            ulong whole = am / bm;
            if (whole > 0x7FFFFFFFUL)
            {
                return negative ? MinValue : MaxValue;
            }
            // Remainder, always below bm and therefore below 2^63, so one left shift cannot overflow.
            ulong remainder = am % bm;
            // Fraction bits produced one at a time by long division. 32 iterations give the 32 fraction bits.
            ulong fraction = 0;
            // Index of the fraction bit being produced. Unit: bits. Range: 0 to 31.
            for (int bit = 0; bit < FractionBits; bit++)
            {
                remainder <<= 1;
                fraction <<= 1;
                if (remainder >= bm)
                {
                    remainder -= bm;
                    fraction |= 1;
                }
            }
            // Quotient as unsigned raw units. Unit: raw units (2^-32). Range: 0 to 2^64 - 1, checked below.
            ulong magnitude = (whole << FractionBits) | fraction;
            if (magnitude > (ulong)long.MaxValue)
            {
                return negative ? MinValue : MaxValue;
            }
            // Signed raw quotient before the sign is applied. Unit: raw units. Range: 0 to long.MaxValue.
            long raw = (long)magnitude;
            return new Fix64(negative ? -raw : raw);
        }

        /// <summary>Multiplies by a whole number with saturation. Cheaper than converting the int first.</summary>
        public static Fix64 operator *(Fix64 a, int b)
        {
            return a * FromInt(b);
        }

        /// <summary>Divides by a whole number, truncated toward zero. Zero throws.</summary>
        public static Fix64 operator /(Fix64 a, int b)
        {
            return a / FromInt(b);
        }

        /// <summary>
        /// Square root, truncated toward zero, exact to the last fraction bit. Negative input throws
        /// <see cref="ArgumentOutOfRangeException"/> because the simulation has no use for an undefined result.
        /// </summary>
        public static Fix64 Sqrt(Fix64 value)
        {
            if (value.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Fix64.Sqrt of a negative value.");
            }
            // The answer in raw units is floor(sqrt(raw × 2^32)). Phase one: digit-by-digit integer square root of raw.
            ulong radicand = (ulong)value.Raw;
            // Running root being built up, two bits of the radicand at a time.
            ulong root = 0;
            // Current bit pair being tested, starting at the highest even bit position.
            ulong bit = 1UL << 62;
            while (bit > radicand)
            {
                bit >>= 2;
            }
            while (bit != 0)
            {
                if (radicand >= root + bit)
                {
                    radicand -= root + bit;
                    root = (root >> 1) + bit;
                }
                else
                {
                    root >>= 1;
                }
                bit >>= 2;
            }
            // Phase two: 16 more digit steps over the 32 implicit zero bits appended by the × 2^32 scaling.
            // Invariant: radicand (the remainder) is below 2 × root + 1, so the shifts below never overflow.
            for (int step = 0; step < FractionBits / 2; step++)
            {
                radicand <<= 2;
                root <<= 1;
                // Trial value for the next square-root digit. Unit: raw square-root bits. Range: below 2^64.
                ulong candidate = (root << 1) | 1UL;
                if (radicand >= candidate)
                {
                    radicand -= candidate;
                    root += 1;
                }
            }
            return new Fix64((long)root);
        }

        /// <summary>
        /// Parses decimal text such as "210", "-1.5" or "0.04" exactly, with no floating point. Accepts an optional
        /// leading minus, digits, and an optional fraction of at most 9 digits. The fraction is rounded half up to the
        /// nearest 2^-32. Anything else (exponents, spaces, plus signs) throws <see cref="FormatException"/>.
        /// </summary>
        /// <param name="text">The decimal text. Must not be null or empty.</param>
        public static Fix64 Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException("Fix64.Parse: empty text.");
            }
            // Position of the next unread character.
            int index = 0;
            // True when the text starts with a minus sign.
            bool negative = false;
            if (text[0] == '-')
            {
                negative = true;
                index = 1;
            }
            // Whole part accumulated as an unsigned integer; must stay at or below 2^31 (the magnitude of MinValue).
            ulong whole = 0;
            // Count of whole-part digits read, to reject an empty whole part.
            int wholeDigits = 0;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9')
            {
                whole = whole * 10 + (ulong)(text[index] - '0');
                if (whole > 0x80000000UL)
                {
                    throw new FormatException("Fix64.Parse: whole part out of range in '" + text + "'.");
                }
                wholeDigits++;
                index++;
            }
            if (wholeDigits == 0)
            {
                throw new FormatException("Fix64.Parse: expected a digit in '" + text + "'.");
            }
            // Fraction digits accumulated as an integer numerator over 10^fractionDigits.
            ulong fractionNumerator = 0;
            // Count of fraction digits read; capped at MaxParseFractionDigits.
            int fractionDigits = 0;
            if (index < text.Length && text[index] == '.')
            {
                index++;
                while (index < text.Length && text[index] >= '0' && text[index] <= '9')
                {
                    if (fractionDigits == MaxParseFractionDigits)
                    {
                        throw new FormatException("Fix64.Parse: more than " + MaxParseFractionDigits + " fraction digits in '" + text + "'.");
                    }
                    fractionNumerator = fractionNumerator * 10 + (ulong)(text[index] - '0');
                    fractionDigits++;
                    index++;
                }
                if (fractionDigits == 0)
                {
                    throw new FormatException("Fix64.Parse: expected a digit after '.' in '" + text + "'.");
                }
            }
            if (index != text.Length)
            {
                throw new FormatException("Fix64.Parse: unexpected character '" + text[index] + "' in '" + text + "'.");
            }
            // Denominator 10^fractionDigits, at most 10^9.
            ulong denominator = 1;
            // Loop counter over the fraction digits. Unit: digits. Range: 0 to 8.
            for (int i = 0; i < fractionDigits; i++)
            {
                denominator *= 10;
            }
            // Fraction scaled to 2^32 and rounded half up: (numerator × 2^32 + denominator / 2) / denominator. Below 2^63.
            ulong fractionRaw = ((fractionNumerator << FractionBits) + (denominator >> 1)) / denominator;
            // Rounding can carry into the whole part (e.g. 0.9999999999 rounds to 1.0); that is handled by plain addition below.
            ulong magnitude = (whole << FractionBits) + fractionRaw;
            if (negative)
            {
                if (magnitude > 0x8000000000000000UL)
                {
                    throw new FormatException("Fix64.Parse: value below MinValue in '" + text + "'.");
                }
                return new Fix64(unchecked(-(long)magnitude));
            }
            if (magnitude > (ulong)long.MaxValue)
            {
                throw new FormatException("Fix64.Parse: value above MaxValue in '" + text + "'.");
            }
            return new Fix64((long)magnitude);
        }

        /// <summary>
        /// Decimal text with up to 9 fraction digits and trailing zeros removed, for logs, errors and tests.
        /// Round-trips through <see cref="Parse"/> to within one 2^-32 step; it is not an exact representation.
        /// </summary>
        public override string ToString()
        {
            // Work on the magnitude so the sign is printed once.
            bool negative = Raw < 0;
            // Magnitude of Raw as unsigned raw units. Unit: raw units (2^-32). Range: 0 to 2^63.
            ulong magnitude = negative ? unchecked((ulong)(-Raw)) : (ulong)Raw;
            // Whole part of the magnitude. Unit: whole units. Range: 0 to 2^31.
            ulong whole = magnitude >> FractionBits;
            // Fraction bits of the magnitude. Unit: raw units (2^-32). Range: 0 to 2^32 - 1.
            ulong fractionRaw = magnitude & (ulong)FractionMask;
            // Fraction scaled to 9 decimal digits, rounded half up: (fractionRaw × 10^9 + 2^31) / 2^32. Below 2^62.
            ulong fractionDecimal = ((fractionRaw * 1000000000UL) + (1UL << (FractionBits - 1))) >> FractionBits;
            if (fractionDecimal >= 1000000000UL)
            {
                // Rounding carried into the whole part.
                whole += 1;
                fractionDecimal = 0;
            }
            // Decimal text of the whole part, digits only. Range: 0 to 2^31 as text.
            string text = whole.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (fractionDecimal != 0)
            {
                // Fraction as nine zero-padded digits with trailing zeros removed. Unit: decimal digits. Range: 1 to 9 characters.
                string digits = fractionDecimal.ToString("D9", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0');
                text = text + "." + digits;
            }
            return negative ? "-" + text : text;
        }

        /// <summary>
        /// Converts to <see cref="float"/> for rendering only. Never call this from simulation code: the result is
        /// not deterministic across machines and must never flow back into simulation state.
        /// </summary>
        public float ToFloatForPresentation()
        {
            return (float)Raw / (float)OneRaw;
        }

        /// <summary>True when both raw values are equal.</summary>
        public bool Equals(Fix64 other)
        {
            return Raw == other.Raw;
        }

        /// <summary>True when <paramref name="obj"/> is a <see cref="Fix64"/> with the same raw value.</summary>
        public override bool Equals(object? obj)
        {
            return obj is Fix64 other && Raw == other.Raw;
        }

        /// <summary>Hash of the raw value. Only for use in dictionaries outside the simulation hot path; state hashing uses <see cref="StateHasher"/>.</summary>
        public override int GetHashCode()
        {
            return Raw.GetHashCode();
        }

        /// <summary>Orders by raw value, which is the same as ordering by real value.</summary>
        public int CompareTo(Fix64 other)
        {
            return Raw.CompareTo(other.Raw);
        }

        /// <summary>True when the raw values are equal.</summary>
        public static bool operator ==(Fix64 a, Fix64 b) => a.Raw == b.Raw;
        /// <summary>True when the raw values differ.</summary>
        public static bool operator !=(Fix64 a, Fix64 b) => a.Raw != b.Raw;
        /// <summary>True when a is below b; orders by real value.</summary>
        public static bool operator <(Fix64 a, Fix64 b) => a.Raw < b.Raw;
        /// <summary>True when a is above b; orders by real value.</summary>
        public static bool operator >(Fix64 a, Fix64 b) => a.Raw > b.Raw;
        /// <summary>True when a is at most b; orders by real value.</summary>
        public static bool operator <=(Fix64 a, Fix64 b) => a.Raw <= b.Raw;
        /// <summary>True when a is at least b; orders by real value.</summary>
        public static bool operator >=(Fix64 a, Fix64 b) => a.Raw >= b.Raw;
    }
}

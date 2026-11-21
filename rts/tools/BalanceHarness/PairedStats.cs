// rts/tools/BalanceHarness/PairedStats.cs
// The exact statistics of the paired ladder and of the stated power (spec: rts/docs/specs/balance_measurement.md M.1.5 and M.2): the two-sided exact sign test, the Clopper-Pearson interval
// of the candidate's share of discordant pairs on a grid of 1/1000, the candidate's advantage in tenths of a point of game win rate, the minimum detectable effect (MDE) and the verdict.
// Every number a verdict depends on is an integer or a System.Numerics.BigInteger: no floating point, so results are exact and the same on every platform. Fractions come in as parts per
// 10 000 ("pt", exact for any value written with at most 4 decimals). "Tenths" are tenths of a point (0.1 percentage point of game win rate). Pure functions, no state. Part of Rts.Tools.BalanceHarness.
using System;
using System.Globalization;
using System.Numerics;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The result of an exact sign test on W candidate-both pairs against L baseline-both pairs.</summary>
    public sealed class SignTestResult
    {
        /// <summary>Pairs in which the profile decided (candidate both plus baseline both). Unit: pairs. Range: 0 upward.</summary>
        public int D { get; }

        /// <summary>Pairs the candidate won twice. Unit: pairs. Range: 0 to D.</summary>
        public int W { get; }

        /// <summary>Pairs the baseline won twice. Unit: pairs. Range: 0 to D.</summary>
        public int L { get; }

        /// <summary>The two-sided exact p-value in parts per 10 000, rounded half up, at most 10000. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int PPt { get; }

        /// <summary>True when the test is significant at the alpha it was run with (2 x tail x 10000 not above alpha_pt x 2^D, exact).</summary>
        public bool Significant { get; }

        /// <summary>Stores the result.</summary>
        public SignTestResult(int d, int w, int l, int pPt, bool significant)
        {
            D = d;
            W = w;
            L = l;
            PPt = pPt;
            Significant = significant;
        }
    }

    /// <summary>Exact integer statistics for the paired ladder and the stated power.</summary>
    public static class PairedStats
    {
        /// <summary>Verdict: significant and the candidate wins more.</summary>
        public const string Pass = "PASS";

        /// <summary>Verdict: significant and the baseline wins more.</summary>
        public const string Fail = "FAIL";

        /// <summary>Verdict: not significant; effects below the MDE could not have been seen.</summary>
        public const string NoMeasurableEffect = "NO_MEASURABLE_EFFECT";

        /// <summary>Verdict: too few complete pairs to judge.</summary>
        public const string Incomplete = "INCOMPLETE";

        /// <summary>Process exit code of a verdict: 0 PASS, 2 FAIL or INCOMPLETE, 4 NO_MEASURABLE_EFFECT (3 for a failed spot check is decided by the caller).</summary>
        /// <param name="verdict">One of the four verdict texts.</param>
        public static int ExitCode(string verdict)
        {
            return verdict == Pass ? 0 : verdict == NoMeasurableEffect ? 4 : 2;
        }

        /// <summary>The binomial coefficient C(n, k) as a BigInteger.</summary>
        /// <param name="n">Items. Unit: items. Range: 0 upward.</param>
        /// <param name="k">Chosen. Unit: items. Range: 0 to n.</param>
        public static BigInteger Choose(int n, int k)
        {
            // Running product, exact at every step: after step i the value is C(n - k + i, i).
            BigInteger result = BigInteger.One;
            for (int i = 1; i <= k; i++)
            {
                result = result * (n - k + i) / i;
            }
            return result;
        }

        /// <summary>Sum of C(d, k) for k = 0 to w.</summary>
        private static BigInteger LowSum(int d, int w)
        {
            BigInteger sum = BigInteger.Zero;
            for (int k = 0; k <= w; k++)
            {
                sum += Choose(d, k);
            }
            return sum;
        }

        /// <summary>The two-sided exact sign test: W is binomial(D, 1/2) under "no difference"; p = min(1, 2 x sum over k = 0..min(W, L) of C(D, k) / 2^D); D = 0 gives p = 1.</summary>
        /// <param name="w">Pairs the candidate won twice. Unit: pairs. Range: 0 upward.</param>
        /// <param name="l">Pairs the baseline won twice. Unit: pairs. Range: 0 upward.</param>
        /// <param name="alphaPt">Two-sided level in parts per 10 000. Unit: parts per 10 000. Range: 1 to 5000.</param>
        /// <param name="alphaDivisor">The level is divided by this (a Bonferroni correction over this many tests; exact, the comparison is multiplied up). Unit: tests. Range: 1 upward.</param>
        public static SignTestResult SignTest(int w, int l, int alphaPt, int alphaDivisor = 1)
        {
            // Discordant pairs and the 2^D denominator.
            int d = w + l;
            BigInteger denominator = BigInteger.Pow(2, d);
            // Twice the tail sum (the two-sided numerator), over 2^D.
            BigInteger twice = 2 * LowSum(d, Math.Min(w, l));
            // p x 10 000 rounded half up, capped at 10 000.
            BigInteger rounded = (twice * 20000 + denominator) / (denominator * 2);
            int pPt = rounded > 10000 ? 10000 : (int)rounded;
            return new SignTestResult(d, w, l, pPt, twice * 10000 * alphaDivisor <= alphaPt * denominator);
        }

        /// <summary>
        /// The exact Clopper-Pearson interval of q = W / D on a grid of 1/1000, rounded outward, by bisection on j in 0..1000: jL is the largest j with
        /// 2 x 10000 x sum over k = W..D of C(D,k) j^k (1000-j)^(D-k) at most alpha_pt x 1000^D (0 when W = 0); jH is the smallest j with the same inequality for the sum over k = 0..W (1000 when W = D).
        /// </summary>
        /// <param name="w">Pairs the candidate won twice. Unit: pairs. Range: 0 to d.</param>
        /// <param name="d">Discordant pairs. Unit: pairs. Range: 0 upward.</param>
        /// <param name="alphaPt">Two-sided level in parts per 10 000. Unit: parts per 10 000. Range: 1 to 5000.</param>
        /// <returns>jL and jH in thousandths.</returns>
        public static int[] Interval(int w, int d, int alphaPt)
        {
            // Right-hand side alpha_pt x 1000^D, shared by both bounds.
            BigInteger bound = alphaPt * BigInteger.Pow(1000, d);
            // Lower bound: predicate "upper tail small enough" holds for small j; find the largest j where it holds.
            int jl = 0;
            if (w > 0)
            {
                int lo = 0;
                int hi = 1000;
                // Invariant: the predicate holds at lo (true at j = 0 for W >= 1) and the answer is in [lo, hi].
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    if (Tail(d, mid, w, d) * 20000 <= bound)
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }
                jl = lo;
            }
            // Upper bound: the predicate "lower tail small enough" holds for large j; find the smallest j where it holds.
            int jh = 1000;
            if (w < d)
            {
                int lo = 0;
                int hi = 1000;
                // Invariant: the predicate holds at hi (true at j = 1000 for W < D) and the answer is in [lo, hi].
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (Tail(d, mid, 0, w) * 20000 <= bound)
                    {
                        hi = mid;
                    }
                    else
                    {
                        lo = mid + 1;
                    }
                }
                jh = hi;
            }
            return new[] { jl, jh };
        }

        /// <summary>Sum over k = from..to of C(d, k) j^k (1000 - j)^(d - k): the binomial tail of probability j/1000 scaled by 1000^d.</summary>
        private static BigInteger Tail(int d, int j, int from, int to)
        {
            BigInteger sum = BigInteger.Zero;
            for (int k = from; k <= to; k++)
            {
                sum += Choose(d, k) * BigInteger.Pow(j, k) * BigInteger.Pow(1000 - j, d - k);
            }
            return sum;
        }

        /// <summary>
        /// The candidate's advantage in tenths of a point of game win rate: the game win fraction is 1/2 + (W - L) / (2n) with draws excluded, so the advantage is
        /// 1000 x (W - L) / (2n) tenths, rounded half away from zero. 0 when n is 0.
        /// </summary>
        /// <param name="w">Pairs the candidate won twice. Unit: pairs. Range: 0 upward.</param>
        /// <param name="l">Pairs the baseline won twice. Unit: pairs. Range: 0 upward.</param>
        /// <param name="n">Distinct complete pairs. Unit: pairs. Range: 0 upward (at least W + L).</param>
        public static int AdvantageTenths(int w, int l, int n)
        {
            return n == 0 ? 0 : DivRoundAway(1000L * (w - l), 2L * n);
        }

        /// <summary>The advantage interval in tenths: [floor((2 jL - 1000) x D / (2n)), ceil((2 jH - 1000) x D / (2n))]; [0, 0] when n is 0.</summary>
        /// <param name="jl">Lower grid numerator from <see cref="Interval"/>. Unit: thousandths. Range: 0 to 1000.</param>
        /// <param name="jh">Upper grid numerator. Unit: thousandths. Range: 0 to 1000.</param>
        /// <param name="d">Discordant pairs. Unit: pairs. Range: 0 upward.</param>
        /// <param name="n">Distinct complete pairs. Unit: pairs. Range: 0 upward.</param>
        public static int[] AdvantageInterval(int jl, int jh, int d, int n)
        {
            if (n == 0)
            {
                return new[] { 0, 0 };
            }
            return new[] { (int)FloorDiv((2L * jl - 1000) * d, 2L * n), (int)(-FloorDiv(-((2L * jh - 1000) * d), 2L * n)) };
        }

        /// <summary>The unpaired form of the advantage interval: [jL - 500, jH - 500] tenths (n games, W = wins, D = n).</summary>
        /// <param name="jl">Lower grid numerator. Unit: thousandths.</param>
        /// <param name="jh">Upper grid numerator. Unit: thousandths.</param>
        public static int[] UnpairedInterval(int jl, int jh)
        {
            return new[] { jl - 500, jh - 500 };
        }

        /// <summary>The unpaired advantage in tenths: 1000 x (w - n/2) / n rounded half away from zero; 0 when n is 0.</summary>
        /// <param name="w">Games the candidate won (distinct). Unit: games. Range: 0 to n.</param>
        /// <param name="n">Games with a winner (distinct). Unit: games. Range: 0 upward.</param>
        public static int UnpairedAdvantageTenths(int w, int n)
        {
            return n == 0 ? 0 : DivRoundAway(500L * (2L * w - n), n);
        }

        /// <summary>Integer division rounded half away from zero.</summary>
        private static int DivRoundAway(long numerator, long denominator)
        {
            // Absolute values (the denominator is positive).
            long abs = Math.Abs(numerator);
            long q = (2 * abs + denominator) / (2 * denominator);
            return (int)(numerator < 0 ? -q : q);
        }

        /// <summary>Floor of a division by a positive number.</summary>
        private static long FloorDiv(long numerator, long denominator)
        {
            long q = numerator / denominator;
            return numerator % denominator != 0 && numerator < 0 ? q - 1 : q;
        }

        /// <summary>Z = round(10000 x (z_alpha + z_power)): the integer both MDE formulas use (28016 for 1.96 and 0.8416).</summary>
        /// <param name="zAlpha">z-score of the two-sided level. Unit: z-score. Range: 0 to 6.</param>
        /// <param name="zPower">z-score of the power. Unit: z-score. Range: 0 to 6.</param>
        public static int Z(Rts.Sim.Core.Fix64 zAlpha, Rts.Sim.Core.Fix64 zPower)
        {
            return ((zAlpha + zPower) * 10000).RoundToInt();
        }

        /// <summary>D_min: the smallest D for which an all-won sign test can be significant (smallest D with 2 x 10000 at most alpha_pt x 2^D); 6 at alpha 0.05.</summary>
        /// <param name="alphaPt">Two-sided level in parts per 10 000. Unit: parts per 10 000. Range: 1 to 5000.</param>
        public static int MinDiscordant(int alphaPt)
        {
            int d = 0;
            while (20000 > alphaPt * BigInteger.Pow(2, d))
            {
                d++;
            }
            return d;
        }

        /// <summary>Unpaired MDE: the smallest integer m (tenths of a point) with 400 x n x m^2 at least Z^2; -1 when n is 0 (nothing could be seen).</summary>
        /// <param name="n">Independent games with a winner. Unit: games. Range: 0 upward.</param>
        /// <param name="z">The integer Z from <see cref="Z"/>. Range: 1 upward.</param>
        public static int MdeUnpaired(int n, int z)
        {
            return n == 0 ? -1 : SmallestM(400 * (BigInteger)n, (BigInteger)z * z);
        }

        /// <summary>Paired MDE: with D' = max(D, D_min), the smallest m with 400 x n^2 x m^2 at least Z^2 x D'; -1 when n is 0.</summary>
        /// <param name="n">Distinct complete pairs. Unit: pairs. Range: 0 upward.</param>
        /// <param name="d">Discordant pairs. Unit: pairs. Range: 0 to n.</param>
        /// <param name="z">The integer Z. Range: 1 upward.</param>
        /// <param name="alphaPt">Two-sided level in parts per 10 000, for D_min. Unit: parts per 10 000. Range: 1 to 5000.</param>
        public static int MdePaired(int n, int d, int z, int alphaPt)
        {
            if (n == 0)
            {
                return -1;
            }
            return SmallestM(400 * (BigInteger)n * n, (BigInteger)z * z * Math.Max(d, MinDiscordant(alphaPt)));
        }

        /// <summary>The smallest m of 0 upward with coefficient x m^2 at least target (binary search; coefficient above 0).</summary>
        private static int SmallestM(BigInteger coefficient, BigInteger target)
        {
            long lo = 0;
            long hi = 1;
            while (coefficient * hi * hi < target)
            {
                hi *= 2;
            }
            while (lo < hi)
            {
                long mid = (lo + hi) / 2;
                if (coefficient * mid * mid >= target)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }
            return (int)lo;
        }

        /// <summary>Pairs needed for a target MDE of m tenths at discordant share r (parts per 10 000): n at least Z^2 x r / (400 m^2), rounded up; the games are twice that.</summary>
        /// <param name="mTenths">Target MDE. Unit: tenths of a point. Range: 1 upward.</param>
        /// <param name="rPt">Discordant share. Unit: parts per 10 000. Range: 0 to 10000.</param>
        /// <param name="z">The integer Z. Range: 1 upward.</param>
        public static int PairsNeeded(int mTenths, int rPt, int z)
        {
            BigInteger numerator = (BigInteger)z * z * rPt;
            BigInteger denominator = 400 * (BigInteger)mTenths * mTenths * 10000;
            return (int)((numerator + denominator - 1) / denominator);
        }

        /// <summary>The verdict of a sign test (M.2): PASS when significant and W above L, FAIL when significant and W below L, else NO_MEASURABLE_EFFECT.</summary>
        /// <param name="test">The sign test.</param>
        public static string Verdict(SignTestResult test)
        {
            return !test.Significant ? NoMeasurableEffect : test.W > test.L ? Pass : Fail;
        }

        /// <summary>A p-value in parts per 10 000 as "0.0000".</summary>
        /// <param name="pPt">Parts per 10 000. Range: 0 to 10000.</param>
        public static string P4(int pPt)
        {
            return (pPt / 10000).ToString(CultureInfo.InvariantCulture) + "." + (pPt % 10000).ToString("D4", CultureInfo.InvariantCulture);
        }

        /// <summary>Tenths of a point with a sign and one decimal ("+14.5", "-3.0", "+0.0").</summary>
        /// <param name="tenths">Value. Unit: tenths of a point.</param>
        public static string Signed(int tenths)
        {
            int abs = Math.Abs(tenths);
            return (tenths < 0 ? "-" : "+") + (abs / 10).ToString(CultureInfo.InvariantCulture) + "." + (abs % 10).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Tenths of a point without a sign and one decimal ("9.6"); "-" for a negative value (nothing could be seen).</summary>
        /// <param name="tenths">Value. Unit: tenths of a point. Range: -1 (none) or 0 upward.</param>
        public static string Plain(int tenths)
        {
            return tenths < 0 ? "-" : (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A share num / den as "0.000" (half up, integers); "-" when den is 0.</summary>
        /// <param name="num">Numerator. Range: 0 upward.</param>
        /// <param name="den">Denominator. Range: 0 upward.</param>
        public static string Share3(long num, long den)
        {
            return PredictEvaluator.Ratio3(num, den);
        }
    }
}

// rts/tools/BalanceHarness/HarnessStats.cs
// Pure statistics for the balance report: Wilson score interval, win rate, lower-median and percentile of match lengths, and
// the invariant-culture number formats (spec: rts/docs/specs/balance_harness.md section 4). This is the one place besides ReportWriter where
// the harness uses double; nothing computed here ever feeds back into a simulation. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>A win rate with its Wilson interval, all as fractions of 1.</summary>
    public readonly struct WinRate
    {
        /// <summary>Games counted (decided games plus draws). Unit: games. Range: 0 upward.</summary>
        public int Games { get; }

        /// <summary>Win fraction with draws counting half. Unit: fraction. Range: 0 to 1 (0 when Games is 0).</summary>
        public double P { get; }

        /// <summary>Lower end of the Wilson interval. Unit: fraction. Range: 0 to 1.</summary>
        public double Low { get; }

        /// <summary>Upper end of the Wilson interval. Unit: fraction. Range: 0 to 1.</summary>
        public double High { get; }

        /// <summary>Bundles the values.</summary>
        public WinRate(int games, double p, double low, double high)
        {
            Games = games;
            P = p;
            Low = low;
            High = high;
        }
    }

    /// <summary>Pure statistics helpers.</summary>
    public static class HarnessStats
    {
        /// <summary>Slack for comparing a computed win rate with a band edge, so 275 of 500 equals 0.55 whatever the rounding. Unit: fraction. Range: fixed, 1e-9.</summary>
        public const double BandTolerance = 1e-9;

        /// <summary>
        /// Win rate of one faction: n = <paramref name="wins"/> + <paramref name="draws"/> + <paramref name="losses"/>, score = wins + 0.5 draws,
        /// Wilson interval at <paramref name="z"/>, clamped to [0, 1]. With n = 0 everything is 0.
        /// </summary>
        /// <param name="wins">Games won. Unit: games. Range: 0 upward.</param>
        /// <param name="draws">Games drawn. Unit: games. Range: 0 upward.</param>
        /// <param name="losses">Games lost. Unit: games. Range: 0 upward.</param>
        /// <param name="z">Z-score of the interval (1.96 is 95 percent). Range: 1 to 4.</param>
        public static WinRate Wilson(int wins, int draws, int losses, double z)
        {
            // Games counted. Unit: games.
            int n = wins + draws + losses;
            if (n == 0)
            {
                return new WinRate(0, 0, 0, 0);
            }
            // Score and win fraction.
            double p = (wins + 0.5 * draws) / n;
            // z squared, the interval's centre shift and half width, and the denominator (all in fractions).
            double z2 = z * z;
            double centre = p + z2 / (2.0 * n);
            double half = z * Math.Sqrt(p * (1.0 - p) / n + z2 / (4.0 * n * (double)n));
            double scale = 1.0 + z2 / n;
            return new WinRate(n, p, Math.Min(1.0, Math.Max(0.0, (centre - half) / scale)), Math.Min(1.0, Math.Max(0.0, (centre + half) / scale)));
        }

        /// <summary>True when <paramref name="p"/> is outside [<paramref name="bandMin"/>, <paramref name="bandMax"/>], edges inclusive (within <see cref="BandTolerance"/>).</summary>
        public static bool OutsideBand(double p, double bandMin, double bandMax)
        {
            return p < bandMin - BandTolerance || p > bandMax + BandTolerance;
        }

        /// <summary>True when the whole interval lies outside the band (below its minimum or above its maximum).</summary>
        public static bool SignificantlyOutsideBand(WinRate rate, double bandMin, double bandMax)
        {
            return rate.High < bandMin - BandTolerance || rate.Low > bandMax + BandTolerance;
        }

        /// <summary>Element (count - 1) * percent / 100 of an ascending list (integer division; 50 is the lower median).</summary>
        /// <param name="sorted">Values in ascending order; not empty.</param>
        /// <param name="percent">Percentile. Unit: percent. Range: 0 to 100.</param>
        public static int Percentile(IReadOnlyList<int> sorted, int percent)
        {
            return sorted[(sorted.Count - 1) * percent / 100];
        }

        /// <summary>Ticks as seconds with one decimal ("10.0"), invariant culture.</summary>
        /// <param name="ticks">Duration. Unit: ticks. Range: 0 upward.</param>
        /// <param name="ticksPerSecond">Tick rate. Unit: ticks per second. Range: 1 to 1000.</param>
        public static string Seconds(int ticks, int ticksPerSecond)
        {
            return ((double)ticks / ticksPerSecond).ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>A fraction as a percentage with one decimal ("52.6").</summary>
        public static string Percent(double fraction)
        {
            return (fraction * 100.0).ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>Match length summary of a cell: "median | p10 | p90" cells in seconds, or "-" three times for no games.</summary>
        /// <param name="ticks">Match lengths. Unit: ticks. Any order; may be empty.</param>
        /// <param name="ticksPerSecond">Tick rate. Unit: ticks per second.</param>
        public static string[] LengthSummary(IReadOnlyList<int> ticks, int ticksPerSecond)
        {
            if (ticks.Count == 0)
            {
                return new[] { "-", "-", "-" };
            }
            // Ascending copy.
            List<int> sorted = new List<int>(ticks);
            sorted.Sort();
            return new[]
            {
                Seconds(Percentile(sorted, 50), ticksPerSecond),
                Seconds(Percentile(sorted, 10), ticksPerSecond),
                Seconds(Percentile(sorted, 90), ticksPerSecond),
            };
        }

        /// <summary>Converts a Fix64 to a double for statistics only (raw value over 2^32).</summary>
        public static double ToDouble(Rts.Sim.Core.Fix64 value)
        {
            return value.Raw / 4294967296.0;
        }
    }
}

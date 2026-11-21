// rts/tools/BalanceHarness/PredictBars.cs
// The bars of the fight predictor v2 (spec: rts/docs/specs/fight_predictor_v2.md section 6) judged on extended fight records: the three subsets E (start predictions of equal-value fights),
// U (start predictions of uneven fights) and D (snapshot predictions of damaged armies), and the verdict of bars B1 to B7 as PASS, FAIL or UNJUDGED with the reason. Every comparison is
// integer arithmetic on counts and parts per 10 000 (pt); no floating point. Bar C1 (call time) is listed but is not a code verdict: its measurement goes to stderr only.
// Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The evaluations of one profile's predictions on subsets E, U, D and E plus U.</summary>
    public sealed class PredictSubsets
    {
        /// <summary>Start predictions of ratio-1 (equal-value) fights of extended records.</summary>
        public PredictEvaluation E { get; }

        /// <summary>Start predictions of fights with another ratio.</summary>
        public PredictEvaluation U { get; }

        /// <summary>Snapshot predictions (damaged armies).</summary>
        public PredictEvaluation D { get; }

        /// <summary>Start predictions of E and U together (bars B2 to B4).</summary>
        public PredictEvaluation EU { get; }

        /// <summary>Stores the four evaluations.</summary>
        public PredictSubsets(PredictEvaluation e, PredictEvaluation u, PredictEvaluation d, PredictEvaluation eu)
        {
            E = e;
            U = u;
            D = d;
            EU = eu;
        }

        /// <summary>Splits start predictions into E and U by the record's value ratio (records without extended keys belong to neither) and evaluates the four sets.</summary>
        /// <param name="starts">One start prediction per record.</param>
        /// <param name="snapshots">One snapshot prediction per record that has a snapshot.</param>
        /// <param name="fightSettings">Case lookup.</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="p">The predict.json settings.</param>
        public static PredictSubsets Build(IReadOnlyList<PredictedFight> starts, IReadOnlyList<PredictedFight> snapshots, FightSettings fightSettings, DataSet data, PredictSettings p)
        {
            List<PredictedFight> e = new List<PredictedFight>();
            List<PredictedFight> u = new List<PredictedFight>();
            List<PredictedFight> eu = new List<PredictedFight>();
            foreach (PredictedFight fight in starts)
            {
                FightExtension? extension = fight.Record.Extension;
                if (extension == null)
                {
                    continue;
                }
                (extension.ValueRatioPt == FightSpec.EqualRatioPt ? e : u).Add(fight);
                eu.Add(fight);
            }
            return new PredictSubsets(PredictEvaluator.Evaluate(e, fightSettings, data, p), PredictEvaluator.Evaluate(u, fightSettings, data, p),
                PredictEvaluator.Evaluate(snapshots, fightSettings, data, p), PredictEvaluator.Evaluate(eu, fightSettings, data, p));
        }
    }

    /// <summary>One row of the bar table.</summary>
    public sealed class BarResult
    {
        /// <summary>Verdict: the bar holds.</summary>
        public const string Pass = "PASS";

        /// <summary>Verdict: the bar does not hold.</summary>
        public const string Fail = "FAIL";

        /// <summary>Verdict: the data or the options do not allow a verdict (the reason says why).</summary>
        public const string Unjudged = "UNJUDGED";

        /// <summary>Status of the cost bar C1, which is read from stderr by the coordinator and is not counted.</summary>
        public const string Stderr = "STDERR";

        /// <summary>Bar ID: B1 to B7 or C1.</summary>
        public string Id { get; }

        /// <summary>The bar as the spec states it, with the values of predict.json.</summary>
        public string Bar { get; }

        /// <summary>What was measured, as text.</summary>
        public string Measured { get; }

        /// <summary>One of <see cref="Pass"/>, <see cref="Fail"/>, <see cref="Unjudged"/>, <see cref="Stderr"/>.</summary>
        public string Status { get; }

        /// <summary>Why the status is what it is (empty for a plain PASS).</summary>
        public string Reason { get; }

        /// <summary>Bundles the values.</summary>
        public BarResult(string id, string bar, string measured, string status, string reason)
        {
            Id = id;
            Bar = bar;
            Measured = measured;
            Status = status;
            Reason = reason;
        }
    }

    /// <summary>Judges the bars.</summary>
    public static class PredictBars
    {
        /// <summary>True when correct / decided is at least thresholdPt / 10 000, in integers; false with no decided fight.</summary>
        /// <param name="correct">Correct fights. Range: 0 to decided.</param>
        /// <param name="decided">Decided fights. Range: 0 upward.</param>
        /// <param name="thresholdPt">Threshold in parts per 10 000. Range: 0 to 10000.</param>
        public static bool AtLeast(int correct, int decided, int thresholdPt)
        {
            return decided > 0 && (long)correct * 10000 >= (long)thresholdPt * decided;
        }

        /// <summary>True when x / xDecided is at least y / yDecided plus gainPt / 10 000, multiplied out in integers; false when either side has no decided fight.</summary>
        /// <param name="x">Correct fights of the first tally. Range: 0 to xDecided.</param>
        /// <param name="xDecided">Decided fights of the first tally. Range: 0 upward.</param>
        /// <param name="y">Correct fights of the second tally. Range: 0 to yDecided.</param>
        /// <param name="yDecided">Decided fights of the second tally. Range: 0 upward.</param>
        /// <param name="gainPt">Required gain in parts per 10 000. Range: 0 to 10000.</param>
        public static bool BeatsBy(int x, int xDecided, int y, int yDecided, int gainPt)
        {
            return xDecided > 0 && yDecided > 0 && (long)x * yDecided * 10000 >= (long)y * xDecided * 10000 + (long)gainPt * xDecided * yDecided;
        }

        /// <summary>Counts of a tally as text: "0.857 (6/7)".</summary>
        private static string Text(PredictTally t)
        {
            return t.AccuracyText() + " (" + t.Correct.ToString(CultureInfo.InvariantCulture) + "/" + t.Decided.ToString(CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>Judges B1 to B7 and lists C1.</summary>
        /// <param name="p">The bar values.</param>
        /// <param name="attackMin">attack_min_advantage of the candidate profile (lower edge of the B3 window). Unit: ratio.</param>
        /// <param name="cand">The candidate's subsets.</param>
        /// <param name="baseline">The baseline's subsets, or null without --baseline-profile (the comparison bars are then UNJUDGED).</param>
        /// <param name="data">The loaded data set (which sides are single units).</param>
        public static List<BarResult> Judge(PredictSettings p, Fix64 attackMin, PredictSubsets cand, PredictSubsets? baseline, DataSet data)
        {
            List<BarResult> bars = new List<BarResult>();
            const string noBaseline = "no --baseline-profile given";

            // B1: accuracy on E at least the value and at least the baseline's plus the gain.
            PredictTally e = cand.E.Overall;
            string b1Bar = "accuracy on E >= " + PredictEvaluator.Pt(p.V2MinAccuracyPt) + " and >= baseline + " + PredictEvaluator.Pt(p.V2MinGainPt);
            if (e.Decided == 0)
            {
                bars.Add(new BarResult("B1", b1Bar, "no decided fight in E", BarResult.Unjudged, "E is empty"));
            }
            else if (baseline == null || baseline.E.Overall.Decided == 0)
            {
                bars.Add(new BarResult("B1", b1Bar, Text(e), BarResult.Unjudged, baseline == null ? noBaseline : "the baseline has no decided fight in E"));
            }
            else
            {
                PredictTally be = baseline.E.Overall;
                bool ok = AtLeast(e.Correct, e.Decided, p.V2MinAccuracyPt) && BeatsBy(e.Correct, e.Decided, be.Correct, be.Decided, p.V2MinGainPt);
                bars.Add(new BarResult("B1", b1Bar, Text(e) + ", baseline " + Text(be), ok ? BarResult.Pass : BarResult.Fail, string.Empty));
            }

            // B2: calibration not inverted on E and U together.
            string b2Bar = "no bin with >= " + p.MinFightsPerRow.ToString(CultureInfo.InvariantCulture) + " decided fights wins less than a lower bin by more than " + PredictEvaluator.Pt(p.CalibrationMaxInversionPt);
            if (cand.EU.Overall.Decided == 0)
            {
                bars.Add(new BarResult("B2", b2Bar, "no decided fight in E and U", BarResult.Unjudged, "E and U are empty"));
            }
            else
            {
                int[]? inversion = PredictEvaluator.FindInversion(cand.EU.Bins, p.MinFightsPerRow, p.CalibrationMaxInversionPt);
                bars.Add(inversion == null
                    ? new BarResult("B2", b2Bar, "no inversion", BarResult.Pass, string.Empty)
                    : new BarResult("B2", b2Bar, "bin " + inversion[1].ToString(CultureInfo.InvariantCulture) + " wins " + PredictEvaluator.Ratio3(cand.EU.Bins[inversion[1]].Wins, cand.EU.Bins[inversion[1]].Decided)
                        + ", below bin " + inversion[0].ToString(CultureInfo.InvariantCulture) + " at " + PredictEvaluator.Ratio3(cand.EU.Bins[inversion[0]].Wins, cand.EU.Bins[inversion[0]].Decided), BarResult.Fail, string.Empty));
            }

            // B3: the predicted winner wins at least the share when the margin is in [attackMin, upper).
            int edgeDecided = 0;
            int edgeWins = 0;
            foreach (PredictedFight fight in cand.EU.Fights)
            {
                if (fight.Kind == PredictedFight.KindDecided && fight.Class != PredictedFight.ClassNone && fight.Margin >= attackMin && fight.Margin < p.V2AttackEdgeUpper)
                {
                    edgeDecided++;
                    edgeWins += fight.Correct ? 1 : 0;
                }
            }
            string b3Bar = "predicted winner wins >= " + PredictEvaluator.Pt(p.V2AttackEdgeMinSharePt) + " of decided fights with margin in [" + attackMin + ", " + p.V2AttackEdgeUpper + ")";
            string b3Measured = PredictEvaluator.Ratio3(edgeWins, edgeDecided) + " (" + edgeWins.ToString(CultureInfo.InvariantCulture) + "/" + edgeDecided.ToString(CultureInfo.InvariantCulture) + ")";
            if (edgeDecided < p.MinFightsPerRow)
            {
                bars.Add(new BarResult("B3", b3Bar, b3Measured, BarResult.Unjudged, "fewer than min_fights_per_row (" + p.MinFightsPerRow.ToString(CultureInfo.InvariantCulture) + ") fights in the window"));
            }
            else
            {
                bars.Add(new BarResult("B3", b3Bar, b3Measured, AtLeast(edgeWins, edgeDecided, p.V2AttackEdgeMinSharePt) ? BarResult.Pass : BarResult.Fail, string.Empty));
            }

            // B4: stalemates predicted none (a failing half fails the bar; an unjudged half leaves it unjudged).
            PredictEvaluation eu = cand.EU;
            string b4Bar = "no-winner fights predicted none >= " + PredictEvaluator.Pt(p.V2MinNoWinnerAgreePt) + " (judged from " + p.V2MinNoWinnerFights.ToString(CultureInfo.InvariantCulture)
                + " fights); decided fights predicted none <= " + PredictEvaluator.Pt(p.V2MaxFalseNonePt);
            string b4Measured = "none predicted for " + eu.NoWinnerAgree.ToString(CultureInfo.InvariantCulture) + "/" + eu.NoWinner.ToString(CultureInfo.InvariantCulture) + " no-winner fights; "
                + eu.DecidedPredictedNone.ToString(CultureInfo.InvariantCulture) + "/" + eu.Overall.Decided.ToString(CultureInfo.InvariantCulture) + " decided fights";
            // The false-none half: decided predicted none over decided, at most the share.
            bool falseNoneOk = (long)eu.DecidedPredictedNone * 10000 <= (long)p.V2MaxFalseNonePt * eu.Overall.Decided;
            bool agreeJudged = eu.NoWinner >= p.V2MinNoWinnerFights;
            bool agreeOk = (long)eu.NoWinnerAgree * 10000 >= (long)p.V2MinNoWinnerAgreePt * eu.NoWinner;
            if (!falseNoneOk || (agreeJudged && !agreeOk))
            {
                bars.Add(new BarResult("B4", b4Bar, b4Measured, BarResult.Fail, !falseNoneOk ? "too many decided fights predicted none" : "too few no-winner fights predicted none"));
            }
            else if (!agreeJudged)
            {
                bars.Add(new BarResult("B4", b4Bar, b4Measured, BarResult.Unjudged, "fewer than v2_min_no_winner_fights (" + p.V2MinNoWinnerFights.ToString(CultureInfo.InvariantCulture) + ") no-winner fights"));
            }
            else
            {
                bars.Add(new BarResult("B4", b4Bar, b4Measured, BarResult.Pass, string.Empty));
            }

            // B5: every pair of two single units in E with enough decided fights.
            string worstPair = string.Empty;
            PredictTally? worst = null;
            int pairsJudged = 0;
            int pairsFailed = 0;
            foreach (KeyValuePair<string, PredictTally> pair in cand.E.ByPair)
            {
                // The two side IDs of the key "a vs b".
                int split = pair.Key.IndexOf(" vs ", StringComparison.Ordinal);
                if (split < 0 || !data.Content.Units.ContainsKey(pair.Key.Substring(0, split)) || !data.Content.Units.ContainsKey(pair.Key.Substring(split + 4))
                    || pair.Value.Decided < p.V2PairMinFights)
                {
                    continue;
                }
                pairsJudged++;
                pairsFailed += AtLeast(pair.Value.Correct, pair.Value.Decided, p.V2MinPairAccuracyPt) ? 0 : 1;
                if (worst == null || (long)pair.Value.Correct * worst.Decided < (long)worst.Correct * pair.Value.Decided)
                {
                    worst = pair.Value;
                    worstPair = pair.Key;
                }
            }
            string b5Bar = "every pair of two single units in E with >= " + p.V2PairMinFights.ToString(CultureInfo.InvariantCulture) + " decided fights has accuracy >= " + PredictEvaluator.Pt(p.V2MinPairAccuracyPt);
            if (pairsJudged == 0)
            {
                bars.Add(new BarResult("B5", b5Bar, "no pair with enough decided fights", BarResult.Unjudged, "no single-unit pair has v2_pair_min_fights decided fights in E"));
            }
            else
            {
                bars.Add(new BarResult("B5", b5Bar, pairsJudged.ToString(CultureInfo.InvariantCulture) + " pairs judged, " + pairsFailed.ToString(CultureInfo.InvariantCulture) + " below; worst " + worstPair + " " + Text(worst!),
                    pairsFailed == 0 ? BarResult.Pass : BarResult.Fail, string.Empty));
            }

            // B6: accuracy on D at least the value and not below the baseline's.
            PredictTally d = cand.D.Overall;
            string b6Bar = "accuracy on D >= " + PredictEvaluator.Pt(p.V2MinDamagedAccuracyPt) + " and >= baseline's";
            if (d.Decided == 0)
            {
                bars.Add(new BarResult("B6", b6Bar, "no decided fight in D", BarResult.Unjudged, "D is empty"));
            }
            else if (baseline == null || baseline.D.Overall.Decided == 0)
            {
                bars.Add(new BarResult("B6", b6Bar, Text(d), BarResult.Unjudged, baseline == null ? noBaseline : "the baseline has no decided fight in D"));
            }
            else
            {
                PredictTally bd = baseline.D.Overall;
                bool ok = AtLeast(d.Correct, d.Decided, p.V2MinDamagedAccuracyPt) && BeatsBy(d.Correct, d.Decided, bd.Correct, bd.Decided, 0);
                bars.Add(new BarResult("B6", b6Bar, Text(d) + ", baseline " + Text(bd), ok ? BarResult.Pass : BarResult.Fail, string.Empty));
            }

            // B7: accuracy on U not below the baseline's.
            PredictTally u = cand.U.Overall;
            string b7Bar = "accuracy on U >= baseline's";
            if (u.Decided == 0)
            {
                bars.Add(new BarResult("B7", b7Bar, "no decided fight in U", BarResult.Unjudged, "U is empty"));
            }
            else if (baseline == null || baseline.U.Overall.Decided == 0)
            {
                bars.Add(new BarResult("B7", b7Bar, Text(u), BarResult.Unjudged, baseline == null ? noBaseline : "the baseline has no decided fight in U"));
            }
            else
            {
                PredictTally bu = baseline.U.Overall;
                bars.Add(new BarResult("B7", b7Bar, Text(u) + ", baseline " + Text(bu), BeatsBy(u.Correct, u.Decided, bu.Correct, bu.Decided, 0) ? BarResult.Pass : BarResult.Fail, string.Empty));
            }

            // C1: not a code verdict; the measurement is on stderr.
            bars.Add(new BarResult("C1", "mean wall time per Predict call <= " + p.V2MaxMeanCallMicroseconds.ToString(CultureInfo.InvariantCulture) + " microseconds", "stderr only (timing is never written to a file)",
                BarResult.Stderr, "the coordinator reads the stderr line; not counted in bars="));
            return bars;
        }

        /// <summary>Counts the bars: passed and judged (every bar but C1 that is not UNJUDGED), and the verdict: FAIL when any bar failed, INCOMPLETE when none failed but one is unjudged, else PASS.</summary>
        /// <param name="bars">The judged bars.</param>
        /// <param name="passed">Bars with status PASS. Unit: bars.</param>
        /// <param name="judged">Bars with status PASS or FAIL. Unit: bars.</param>
        public static string Verdict(IReadOnlyList<BarResult> bars, out int passed, out int judged)
        {
            passed = 0;
            judged = 0;
            // Any failed bar, and any unjudged bar.
            bool failed = false;
            bool unjudged = false;
            foreach (BarResult bar in bars)
            {
                if (bar.Status == BarResult.Pass)
                {
                    passed++;
                    judged++;
                }
                else if (bar.Status == BarResult.Fail)
                {
                    judged++;
                    failed = true;
                }
                else if (bar.Status == BarResult.Unjudged)
                {
                    unjudged = true;
                }
            }
            return failed ? "FAIL" : unjudged ? "INCOMPLETE" : "PASS";
        }
    }
}

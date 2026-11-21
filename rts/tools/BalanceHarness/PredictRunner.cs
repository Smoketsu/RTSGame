// rts/tools/BalanceHarness/PredictRunner.cs
// Command --predict-fights (spec: rts/docs/specs/balance_measurement.md C.2): reads fight records (from --from <fights.jsonl>, or by running the --fights sweep exactly as --fights does),
// predicts every fight with FightPredictor, scores the predictions (PredictEvaluator), writes predict.jsonl and predict.md and prints the last line
// "PREDICT fights=.. judged=.. decided=.. correct=.. accuracy=.. clean=../.. no_winner_agree=../.. decision=.. (proposed thresholds)". Nothing here changes the predictor, the sweep or its records.
// Output depends only on data, options and records, never on --jobs or the clock. Exit codes: 0 done (any decision), 3 the sweep's spot check failed, 1 error. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rts.Ai;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Coordinator of --predict-fights and the writers of its two files.</summary>
    public static class PredictRunner
    {
        /// <summary>Runs the command for parsed options.</summary>
        /// <param name="options">Parsed options (--predict-fights, --from, --profile, --jobs, --seed, --out).</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="output">stdout: the PREDICT line last.</param>
        /// <param name="error">stderr: elapsed time of a sweep.</param>
        public static int Run(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            PredictSettings settings = PredictSettings.Load(options.DataDirectory);
            FightSettings fightSettings = FightSettings.Load(options.DataDirectory, data);
            // Profile whose predictor numbers are used: --profile, else the settings' default.
            string profileId = options.Profile.Length > 0 ? options.Profile : settings.PredictorProfile;
            AiProfile profile = AiProfile.FromDataSet(data, profileId);
            // Fight records, and the exit code of the sweep (0 when read from a file).
            List<FightRecord> records = new List<FightRecord>();
            int sweepCode = 0;
            if (options.From.Length > 0)
            {
                if (!File.Exists(options.From))
                {
                    throw new HarnessException("--from: file not found: " + options.From);
                }
                records = ReadRecords(File.ReadAllLines(options.From));
            }
            else
            {
                sweepCode = FightCoordinator.RunSweepFor(options, data, output, error, records);
            }
            PredictorSettings predictor = PredictorSettings.From(profile);
            // Every record predicted and scored; a failing record is reported with its line number.
            List<PredictedFight> fights = new List<PredictedFight>();
            for (int i = 0; i < records.Count; i++)
            {
                try
                {
                    fights.Add(PredictEvaluator.Predict(records[i], fightSettings.CaseById(records[i].Case).Kind, data, predictor));
                }
                catch (HarnessException ex)
                {
                    throw new HarnessException("record " + (i + 1).ToString(CultureInfo.InvariantCulture) + " (" + records[i].Fight + "): " + ex.Message);
                }
            }
            PredictEvaluation evaluation = PredictEvaluator.Evaluate(fights, fightSettings, data, settings);
            PredictDecision decision = PredictEvaluator.Decide(settings, evaluation.Overall, evaluation.Clean, evaluation.Mechanics, evaluation.Bins);
            // Output folder: --out, or the fights subfolder of the default harness folder (where --fights writes).
            string outDirectory = options.OutDirectory == BatchOptions.DefaultOutDirectory ? Path.Combine(BatchOptions.DefaultOutDirectory, "fights") : options.OutDirectory;
            Directory.CreateDirectory(outDirectory);
            File.WriteAllText(Path.Combine(outDirectory, "predict.jsonl"), BuildJsonl(evaluation), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDirectory, "predict.md"), BuildReport(data, fightSettings, settings, profileId, profile, options, evaluation, decision), new UTF8Encoding(false));
            output.Write(Line(evaluation, decision) + "\n");
            output.Flush();
            return sweepCode != 0 ? sweepCode : 0;
        }

        /// <summary>Parses fight records, one per non-empty line; a bad line is an error naming its number.</summary>
        /// <param name="lines">Lines of a fights.jsonl.</param>
        public static List<FightRecord> ReadRecords(IReadOnlyList<string> lines)
        {
            List<FightRecord> records = new List<FightRecord>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Length == 0)
                {
                    continue;
                }
                try
                {
                    records.Add(FightRecord.Parse(lines[i]));
                }
                catch (HarnessException ex)
                {
                    throw new HarnessException("--from line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
                }
            }
            return records;
        }

        /// <summary>The last stdout line (C.2).</summary>
        /// <param name="e">The evaluation.</param>
        /// <param name="decision">The decision.</param>
        public static string Line(PredictEvaluation e, PredictDecision decision)
        {
            return "PREDICT fights=" + e.Fights.Count.ToString(CultureInfo.InvariantCulture) + " judged=" + e.Overall.Fights.ToString(CultureInfo.InvariantCulture)
                + " decided=" + e.Overall.Decided.ToString(CultureInfo.InvariantCulture) + " correct=" + e.Overall.Correct.ToString(CultureInfo.InvariantCulture)
                + " accuracy=" + e.Overall.AccuracyText() + " clean=" + e.Clean.Correct.ToString(CultureInfo.InvariantCulture) + "/" + e.Clean.Decided.ToString(CultureInfo.InvariantCulture)
                + " no_winner_agree=" + e.NoWinnerAgree.ToString(CultureInfo.InvariantCulture) + "/" + e.NoWinner.ToString(CultureInfo.InvariantCulture)
                + " decision=" + decision.Verdict + " (proposed thresholds)";
        }

        /// <summary>predict.jsonl: one line per fight with the prediction in integers (milli = thousandths).</summary>
        private static string BuildJsonl(PredictEvaluation e)
        {
            StringBuilder sb = new StringBuilder();
            foreach (PredictedFight f in e.Fights)
            {
                sb.Append("{\"fight\":").Append(GameRecord.Quote(f.Record.Fight)).Append(",\"kind\":").Append(GameRecord.Quote(f.Kind))
                    .Append(",\"result\":").Append(GameRecord.Quote(f.Record.Result)).Append(",\"predicted\":").Append(GameRecord.Quote(f.Class))
                    .Append(",\"margin_milli\":").Append(Milli(f.Margin)).Append(",\"time_seat0_milli\":").Append(Milli(f.TimeSeat0KillsSeat1))
                    .Append(",\"time_seat1_milli\":").Append(Milli(f.TimeSeat1KillsSeat0)).Append(",\"correct\":").Append(f.Correct ? "true" : "false").Append("}\n");
            }
            return sb.ToString();
        }

        /// <summary>A Fix64 in thousandths, rounded, as text (integer arithmetic).</summary>
        private static string Milli(Fix64 value)
        {
            return (value * 1000).RoundToInt().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>One table row: label, fights, decided, correct, accuracy, Wilson interval, small marker.</summary>
        private static void Row(StringBuilder sb, string label, PredictTally t, double z, int minFights)
        {
            // Wilson interval over the decided fights.
            WinRate rate = HarnessStats.Wilson(t.Correct, 0, t.Decided - t.Correct, z);
            sb.Append("| ").Append(label).Append(" | ").Append(t.Fights.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(t.Decided.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(t.Correct.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(t.AccuracyText()).Append(" | ")
                .Append(rate.Games > 0 ? "[" + HarnessStats.Percent(rate.Low) + ", " + HarnessStats.Percent(rate.High) + "]" : "-")
                .Append(t.Decided < minFights ? " | small |\n" : " | |\n");
        }

        /// <summary>The table header of <see cref="Row"/>.</summary>
        private static void Header(StringBuilder sb, string first)
        {
            sb.Append("| ").Append(first).Append(" | fights | decided | correct | accuracy | Wilson interval (%) | |\n| --- | ---: | ---: | ---: | ---: | --- | --- |\n");
        }

        /// <summary>A table of tallies keyed by a map, in key order.</summary>
        private static void Table(StringBuilder sb, string first, IEnumerable<KeyValuePair<string, PredictTally>> rows, double z, int minFights)
        {
            Header(sb, first);
            foreach (KeyValuePair<string, PredictTally> pair in rows)
            {
                Row(sb, pair.Key, pair.Value, z, minFights);
            }
            sb.Append('\n');
        }

        /// <summary>Builds predict.md.</summary>
        private static string BuildReport(DataSet data, FightSettings fightSettings, PredictSettings s, string profileId, AiProfile profile, BatchOptions options, PredictEvaluation e, PredictDecision d)
        {
            // Wilson z-score (tuning/harness_batch.json). Range: 1 to 4.
            double z = HarnessStats.ToDouble(data.Tuning.HarnessBatch.ConfidenceZ);
            StringBuilder sb = new StringBuilder();
            sb.Append("<!-- predict.md: written by the balance harness --predict-fights (rts/tools/BalanceHarness/PredictRunner.cs); not a source file. -->\n");
            sb.Append("# Fight predictor against measured fights\n\n");
            sb.Append("- Predictor: FightPredictor.Predict with the predictor_* numbers of profile `").Append(profileId).Append("`; AI thresholds of that profile: attack_min_advantage ")
                .Append(profile.AttackMinAdvantage.ToString()).Append(", retreat_max_advantage ").Append(profile.RetreatMaxAdvantage.ToString()).Append(".\n");
            sb.Append("- Fights: ").Append(options.From.Length > 0 ? "read from `" + options.From.Replace('\\', '/') + "` (the records may come from other data than the data below)" : "played by this run's --fights sweep")
                .Append("; current data digest: tuning ").Append(data.Tuning.SourceDigest.ToString("x16", CultureInfo.InvariantCulture)).Append(", content ")
                .Append(data.Content.ComputeDigest().ToString("x16", CultureInfo.InvariantCulture)).Append(".\n");
            sb.Append("- Side A is seat 0; A shoots uphill only in the high_ground case. Predicted class none = both times equal (this includes both at the longest time). Margin = longer time / max(shorter time, min seconds), capped.\n");
            sb.Append("- Decided fights (result seat0 or seat1) give the accuracy; a predicted none on a decided fight is wrong. Stalemates, unresolved fights and draws are not in the accuracy: they agree when the predictor says none.\n");
            sb.Append("- Mismatched fights (start values too far apart, ").Append(e.Mismatched.ToString(CultureInfo.InvariantCulture)).Append(" here) are left out of every table.\n");
            sb.Append("- Intervals are Wilson at z = ").Append(z.ToString("0.00", CultureInfo.InvariantCulture)).Append(" over decided fights; a row with fewer than ")
                .Append(s.MinFightsPerRow.ToString(CultureInfo.InvariantCulture)).Append(" decided fights is marked small.\n\n");
            sb.Append("**").Append(Line(e, d)).Append("**\n\n");

            sb.Append("## Decision (proposed rule, C.7)\n\n");
            sb.Append("| Threshold | Proposed | Measured | Fired |\n| --- | ---: | ---: | --- |\n");
            sb.Append("| simulate_below_clean_accuracy | ").Append(PredictEvaluator.Pt(s.SimulateBelowCleanAccuracyPt)).Append(" | ").Append(e.Clean.AccuracyText()).Append(" (").Append(e.Clean.Correct.ToString(CultureInfo.InvariantCulture))
                .Append("/").Append(e.Clean.Decided.ToString(CultureInfo.InvariantCulture)).Append(") | ").Append(PredictEvaluator.Below(e.Clean.Correct, e.Clean.Decided, s.SimulateBelowCleanAccuracyPt) ? "yes" : "no").Append(" |\n");
            sb.Append("| simulate_below_accuracy | ").Append(PredictEvaluator.Pt(s.SimulateBelowAccuracyPt)).Append(" | ").Append(e.Overall.AccuracyText()).Append(" (").Append(e.Overall.Correct.ToString(CultureInfo.InvariantCulture))
                .Append("/").Append(e.Overall.Decided.ToString(CultureInfo.InvariantCulture)).Append(") | ").Append(PredictEvaluator.Below(e.Overall.Correct, e.Overall.Decided, s.SimulateBelowAccuracyPt) ? "yes" : "no").Append(" |\n");
            int[]? inversion = PredictEvaluator.FindInversion(e.Bins, s.MinFightsPerRow, s.CalibrationMaxInversionPt);
            sb.Append("| calibration_max_inversion | ").Append(PredictEvaluator.Pt(s.CalibrationMaxInversionPt)).Append(" | ")
                .Append(inversion == null ? "no inversion among bins with at least " + s.MinFightsPerRow.ToString(CultureInfo.InvariantCulture) + " fights" : "bin " + inversion[1].ToString(CultureInfo.InvariantCulture) + " below bin " + inversion[0].ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(inversion != null ? "yes" : "no").Append(" |\n");
            sb.Append("| mechanic_min_fights | ").Append(s.MechanicMinFights.ToString(CultureInfo.InvariantCulture)).Append(" | decided fights a mechanic needs to be judged | - |\n");
            sb.Append("| fix_min_mechanic_accuracy | ").Append(PredictEvaluator.Pt(s.FixMinMechanicAccuracyPt)).Append(" | lowest judged mechanic: ").Append(LowestMechanic(e, s)).Append(" | - |\n");
            sb.Append("| fix_min_accuracy | ").Append(PredictEvaluator.Pt(s.FixMinAccuracyPt)).Append(" | - | - |\n\n");
            sb.Append("**Decision: ").Append(d.Verdict).Append("** (all thresholds are proposed placeholders for the developer to accept or change)\n\n");
            foreach (string reason in d.Reasons)
            {
                sb.Append("- ").Append(reason).Append('\n');
            }
            if (d.Listed.Count > 0)
            {
                sb.Append("- Listed mechanics: ").Append(string.Join(", ", d.Listed)).Append('\n');
            }
            sb.Append('\n');

            sb.Append("## Overall\n\n");
            Header(sb, "group");
            Row(sb, "all scored fights", e.Overall, z, s.MinFightsPerRow);
            Row(sb, "clean (no unmodelled mechanic present)", e.Clean, z, s.MinFightsPerRow);
            sb.Append("\nNo-winner fights: ").Append(e.NoWinner.ToString(CultureInfo.InvariantCulture)).Append(", of which the predictor said none for ").Append(e.NoWinnerAgree.ToString(CultureInfo.InvariantCulture))
                .Append(". Decided fights predicted none (counted wrong): ").Append(e.DecidedPredictedNone.ToString(CultureInfo.InvariantCulture)).Append(" (seat 0 won ")
                .Append(e.DecidedPredictedNoneSeat0.ToString(CultureInfo.InvariantCulture)).Append(").\n\n");
            sb.Append("Seat order: ").Append(e.SeatPairs.ToString(CultureInfo.InvariantCulture)).Append(" pairs of decided fights differ only in seat order; in ").Append(e.SeatPairsMirrored.ToString(CultureInfo.InvariantCulture))
                .Append(" of them the predictor names the same side in both orders (classes are mirror images), and in ").Append(e.SeatSplits.ToString(CultureInfo.InvariantCulture))
                .Append(" of those the measured winner is a different side in the two orders (a seat split the predictor cannot see).\n\n");

            sb.Append("## By unordered unit pair (accuracy ascending)\n\n");
            Table(sb, "pair", PredictEvaluator.SortByAccuracy(e.ByPair), z, s.MinFightsPerRow);
            sb.Append("## By side (a fight counts for both sides; accuracy ascending)\n\n");
            Table(sb, "side", PredictEvaluator.SortByAccuracy(e.BySide), z, s.MinFightsPerRow);
            sb.Append("## By terrain case\n\n");
            Table(sb, "case", e.ByCase, z, s.MinFightsPerRow);
            sb.Append("## By army value\n\n");
            Table(sb, "army value (zero padded)", e.ByValue, z, s.MinFightsPerRow);
            sb.Append("## By case and army value\n\nValue and case are confounded: in the committed sweep only flat is played at 750, so the 750 row is a flat row.\n\n");
            Table(sb, "case / army value", e.ByCaseValue, z, s.MinFightsPerRow);

            sb.Append("## By unmodelled mechanic\n\n");
            sb.Append("Present means the mechanic occurs in the fight by its definitions and case (windstorm_range and focus_fire never do: fights run without cycles and with attack-move control). \"Only\" = it is the only mechanic present.\n\n");
            sb.Append("| mechanic | present: decided | present: accuracy | absent: accuracy | only: decided | only: accuracy |\n| --- | ---: | ---: | ---: | ---: | ---: |\n");
            foreach (MechanicRow row in e.Mechanics)
            {
                sb.Append("| ").Append(row.Id).Append(" | ").Append(row.Present.Decided.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.Present.AccuracyText()).Append(" | ")
                    .Append(row.Absent.AccuracyText()).Append(" | ").Append(row.Only.Decided.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.Only.AccuracyText()).Append(" |\n");
            }
            sb.Append("| clean | ").Append(e.Clean.Decided.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(e.Clean.AccuracyText()).Append(" | - | - | - |\n\n");
            foreach (MechanicRow row in e.Mechanics)
            {
                if (row.Present.Decided > 0 && row.Absent.Decided == 0)
                {
                    sb.Append("`").Append(row.Id).Append("` is present in every decided fight, so no fight is clean and its absent column is empty.\n\n");
                }
            }

            sb.Append("## Calibration of the predicted margin\n\n");
            sb.Append("Decided fights only. \"Winner left\" is the lower median of the measured winner's value left. Read: when the predictor names a winner by a ratio in this bin, it wins this often.\n\n");
            sb.Append("| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) |\n| --- | ---: | ---: | ---: | --- | ---: |\n");
            sb.Append("| predicted none | ").Append(e.DecidedPredictedNone.ToString(CultureInfo.InvariantCulture)).Append(" | - | - | - | - |\n");
            foreach (CalibrationBin bin in e.Bins)
            {
                WinRate rate = HarnessStats.Wilson(bin.Wins, 0, bin.Decided - bin.Wins, z);
                sb.Append("| ").Append(bin.Low.ToString()).Append(bin.High.HasValue ? " to below " + bin.High.Value.ToString() : " and above").Append(" | ").Append(bin.Decided.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(bin.Wins.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(PredictEvaluator.Ratio3(bin.Wins, bin.Decided)).Append(" | ")
                    .Append(rate.Games > 0 ? "[" + HarnessStats.Percent(rate.Low) + ", " + HarnessStats.Percent(rate.High) + "]" : "-").Append(" | ")
                    .Append(bin.WinnerLeftMilli.Count > 0 ? PredictEvaluator.Ratio3(HarnessStats.Percentile(bin.WinnerLeftMilli, 50), 1000) : "-").Append(" |\n");
            }

            sb.Append("\n## No-winner fights\n\n");
            if (e.NoWinner == 0)
            {
                sb.Append("None.\n");
            }
            else
            {
                sb.Append("| fight | result | reason | predicted | value left leader | predicted winner |\n| --- | --- | --- | --- | --- | --- |\n");
                foreach (PredictedFight f in e.Fights)
                {
                    if (f.Kind != PredictedFight.KindNoWinner)
                    {
                        continue;
                    }
                    // Value left in thousandths, to compare without floating-point equality.
                    int left0 = (int)Math.Round(f.Record.Left0 * 1000.0, MidpointRounding.AwayFromZero);
                    int left1 = (int)Math.Round(f.Record.Left1 * 1000.0, MidpointRounding.AwayFromZero);
                    sb.Append("| ").Append(f.Record.Fight).Append(" | ").Append(f.Record.Result).Append(" | ").Append(f.Record.Reason).Append(" | ").Append(f.Class).Append(" | ")
                        .Append(left0 == left1 ? "even" : left0 > left1 ? "left0" : "left1").Append(" | ").Append(f.Class == PredictedFight.ClassNone ? "none" : f.Class == PredictedFight.ClassSeat0 ? "left0" : "left1").Append(" |\n");
                }
            }
            return sb.ToString();
        }

        /// <summary>Text naming the judged mechanic with the lowest accuracy (cross-multiplied), or "none judged".</summary>
        private static string LowestMechanic(PredictEvaluation e, PredictSettings s)
        {
            // Lowest row so far.
            MechanicRow? lowest = null;
            foreach (MechanicRow row in e.Mechanics)
            {
                if (row.Present.Decided >= s.MechanicMinFights
                    && (lowest == null || (long)row.Present.Correct * lowest.Present.Decided < (long)lowest.Present.Correct * row.Present.Decided))
                {
                    lowest = row;
                }
            }
            return lowest == null ? "none judged" : lowest.Id + " " + lowest.Present.AccuracyText() + " (" + lowest.Present.Correct.ToString(CultureInfo.InvariantCulture) + "/" + lowest.Present.Decided.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }
}

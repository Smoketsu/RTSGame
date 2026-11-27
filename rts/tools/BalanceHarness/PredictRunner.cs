// rts/tools/BalanceHarness/PredictRunner.cs
// Command --predict-fights (spec: rts/docs/specs/balance_measurement.md C.2): reads fight records (from --from <fights.jsonl>, or by running the --fights sweep exactly as --fights does),
// predicts every fight with FightPredictor, scores the predictions (PredictEvaluator), writes predict.jsonl and predict.md and prints the last line
// "PREDICT fights=.. judged=.. decided=.. correct=.. accuracy=.. clean=../.. no_winner_agree=../.. decision=.. (proposed thresholds)". Nothing here changes the predictor, the sweep or its records.
// With extended records (fight_predictor_v2.md 5.4) it also writes the subsets E, U and D and the bar table (PredictBars), appends " bars=<passed>/<judged> verdict=<..>" to the last line and prints the wall time per Predict call on stderr only; --baseline-profile adds baseline columns. Output depends only on data, options and records, never on --jobs or the clock. Exit codes: 0 done (any decision), 3 the sweep's spot check failed, 1 error. Part of Rts.Tools.BalanceHarness.
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
            FightSettings fightSettings = FightSettings.ForOptions(options, data);
            // Profile whose predictor numbers are used: --profile, else the settings' default.
            string profileId = options.Profile.Length > 0 ? options.Profile : settings.PredictorProfile;
            AiProfile profile = AiProfile.FromDataSet(data, profileId);
            // The baseline profile of --baseline-profile, or null.
            AiProfile? baselineProfile = options.BaselineProfile.Length > 0 ? AiProfile.FromDataSet(data, options.BaselineProfile) : null;
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
            // True when any record carries the extended keys: subsets E, U and D and the bars are then reported.
            bool extended = records.Exists(r => r.Extension != null);
            // Start and snapshot predictions of the candidate and, when given, of the baseline, with the wall time of their calls (stderr only).
            PredictTiming timing = new PredictTiming();
            List<PredictedFight> fights = new List<PredictedFight>();
            List<PredictedFight> snapshots = new List<PredictedFight>();
            // The lane simulation's game data from the loaded data, used by every profile whose predictor_model is sim (ignored by the others).
            FightSimRules simRules = FightSimRules.Build(data.Content, data.Tuning);
            // The candidate's predictor numbers, with the kite term of --predict-kite-factor when given (both seats corrected, as in the game).
            PredictorSettings predictor = PredictorSettings.From(profile, simRules);
            if (options.PredictKiteFactor != null)
            {
                predictor = predictor.WithKite(options.PredictKiteFactor.Value, profile.KiteMinRangeGapTiles, profile.KiteSpeedRatioMin);
            }
            Predictions(records, fightSettings, data, predictor, timing, fights, snapshots);
            PredictTiming baselineTiming = new PredictTiming();
            List<PredictedFight> baselineFights = new List<PredictedFight>();
            List<PredictedFight> baselineSnapshots = new List<PredictedFight>();
            if (baselineProfile != null)
            {
                Predictions(records, fightSettings, data, PredictorSettings.From(baselineProfile, simRules), baselineTiming, baselineFights, baselineSnapshots);
            }
            PredictEvaluation evaluation = PredictEvaluator.Evaluate(fights, fightSettings, data, settings);
            PredictDecision decision = PredictEvaluator.Decide(settings, evaluation.Overall, evaluation.Clean, evaluation.Mechanics, evaluation.Bins);
            // Subsets and bars for extended records.
            PredictSubsets? subsets = null;
            PredictSubsets? baselineSubsets = null;
            List<BarResult> bars = new List<BarResult>();
            string barsSuffix = string.Empty;
            if (extended)
            {
                subsets = PredictSubsets.Build(fights, snapshots, fightSettings, data, settings);
                baselineSubsets = baselineProfile == null ? null : PredictSubsets.Build(baselineFights, baselineSnapshots, fightSettings, data, settings);
                bars = PredictBars.Judge(settings, profile.AttackMinAdvantage, subsets, baselineSubsets, data);
                string verdict = PredictBars.Verdict(bars, out int passed, out int judged);
                barsSuffix = " bars=" + passed.ToString(CultureInfo.InvariantCulture) + "/" + judged.ToString(CultureInfo.InvariantCulture) + " verdict=" + verdict;
            }
            // Output folder: --out, or the fights subfolder of the default harness folder (where --fights writes).
            string outDirectory = options.OutDirectory == BatchOptions.DefaultOutDirectory ? Path.Combine(BatchOptions.DefaultOutDirectory, "fights") : options.OutDirectory;
            Directory.CreateDirectory(outDirectory);
            // Kite settings for the kite_pair field, or null without the flag (output then unchanged).
            PredictorSettings? kiteSettings = options.PredictKiteFactor == null ? null : predictor;
            File.WriteAllText(Path.Combine(outDirectory, "predict.jsonl"), BuildJsonl(evaluation, snapshots, kiteSettings, data), new UTF8Encoding(false));
            string report = BuildReport(data, fightSettings, settings, profileId, profile, options, evaluation, decision);
            if (subsets != null)
            {
                StringBuilder tail = new StringBuilder();
                AppendExtended(tail, subsets, baselineSubsets, bars, options.BaselineProfile, HarnessStats.ToDouble(data.Tuning.HarnessBatch.ConfidenceZ), settings);
                report += tail.ToString();
            }
            // Suffix of the PREDICT line with the kite factor and the number of kite-pair fights; empty without the flag.
            string kiteSuffix = string.Empty;
            if (kiteSettings != null)
            {
                report += KiteTable(evaluation, kiteSettings, data, out int kitePairFights);
                kiteSuffix = " kite_factor=" + options.PredictKiteFactorText + " kite_pair_fights=" + kitePairFights.ToString(CultureInfo.InvariantCulture);
            }
            File.WriteAllText(Path.Combine(outDirectory, "predict.md"), report, new UTF8Encoding(false));
            output.Write(Line(evaluation, decision) + barsSuffix + kiteSuffix + "\n");
            output.Flush();
            // Wall time per Predict call, stderr only (never in a file or on stdout): the C1 bar is read from here.
            error.Write("predict time profile " + profileId + ": mean " + timing.MeanMicroseconds().ToString(CultureInfo.InvariantCulture) + " us, largest " + timing.MaxMicroseconds().ToString(CultureInfo.InvariantCulture)
                + " us over " + timing.Calls.ToString(CultureInfo.InvariantCulture) + " calls\n");
            if (baselineProfile != null)
            {
                error.Write("predict time profile " + options.BaselineProfile + ": mean " + baselineTiming.MeanMicroseconds().ToString(CultureInfo.InvariantCulture) + " us, largest "
                    + baselineTiming.MaxMicroseconds().ToString(CultureInfo.InvariantCulture) + " us over " + baselineTiming.Calls.ToString(CultureInfo.InvariantCulture) + " calls\n");
            }
            return sweepCode != 0 ? sweepCode : 0;
        }

        /// <summary>Predicts and scores every record's start and, when it has a snapshot, its snapshot; a failing record is reported with its line number.</summary>
        private static void Predictions(List<FightRecord> records, FightSettings fightSettings, DataSet data, PredictorSettings predictor, PredictTiming timing, List<PredictedFight> fights, List<PredictedFight> snapshots)
        {
            for (int i = 0; i < records.Count; i++)
            {
                try
                {
                    // Terrain kind of the record's case.
                    string kind = fightSettings.CaseById(records[i].Case).Kind;
                    fights.Add(PredictEvaluator.Predict(records[i], kind, data, predictor, fightSettings, timing));
                    PredictedFight? snapshot = PredictEvaluator.PredictSnapshot(records[i], kind, data, predictor, timing);
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
                catch (HarnessException ex)
                {
                    throw new HarnessException("record " + (i + 1).ToString(CultureInfo.InvariantCulture) + " (" + records[i].Fight + "): " + ex.Message);
                }
            }
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
        /// <param name="e">The evaluation.</param>
        /// <param name="snapshots">The snapshot predictions (subset D).</param>
        /// <param name="kite">Settings with the kite term (adds the last field kite_pair to every line), or null for the original format.</param>
        /// <param name="data">The loaded data set.</param>
        private static string BuildJsonl(PredictEvaluation e, IReadOnlyList<PredictedFight> snapshots, PredictorSettings? kite, DataSet data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (PredictedFight f in e.Fights)
            {
                AppendLine(sb, f, string.Empty, kite == null ? string.Empty : KitePairField(f, kite, data));
            }
            // Snapshot predictions (subset D) follow the start predictions, each marked with its subset.
            foreach (PredictedFight f in snapshots)
            {
                AppendLine(sb, f, ",\"subset\":\"D\"", kite == null ? string.Empty : KitePairField(f, kite, data));
            }
            return sb.ToString();
        }

        /// <summary>One predict.jsonl line; <paramref name="extra"/> is JSON text starting with a comma, inserted after the fight ID (empty for the original format).</summary>
        /// <param name="sb">Output.</param>
        /// <param name="f">The fight.</param>
        /// <param name="extra">Text after the fight ID.</param>
        /// <param name="tail">JSON text starting with a comma, inserted before the closing brace (empty = none).</param>
        private static void AppendLine(StringBuilder sb, PredictedFight f, string extra, string tail)
        {
            sb.Append("{\"fight\":").Append(GameRecord.Quote(f.Record.Fight)).Append(extra).Append(",\"kind\":").Append(GameRecord.Quote(f.Kind))
                .Append(",\"result\":").Append(GameRecord.Quote(f.Record.Result)).Append(",\"predicted\":").Append(GameRecord.Quote(f.Class))
                .Append(",\"margin_milli\":").Append(Milli(f.Margin)).Append(",\"time_seat0_milli\":").Append(Milli(f.TimeSeat0KillsSeat1))
                .Append(",\"time_seat1_milli\":").Append(Milli(f.TimeSeat1KillsSeat0)).Append(",\"correct\":").Append(f.Correct ? "true" : "false").Append(tail).Append("}\n");
        }

        /// <summary>True when some entry x of one seat and some entry y of the other have Kites(y, x) and Dps(x, y) above 0 (ai_kite_correction.md 4.1); the start armies of the record decide.</summary>
        /// <param name="record">The measured fight.</param>
        /// <param name="s">Settings with the kite rule's gap and ratio.</param>
        /// <param name="data">The loaded data set.</param>
        private static bool IsKitePair(FightRecord record, PredictorSettings s, DataSet data)
        {
            // Entries of both seats.
            List<FightEntry> seat0 = PredictEvaluator.Entries(record.Units0, data);
            List<FightEntry> seat1 = PredictEvaluator.Entries(record.Units1, data);
            return OneWayKitePair(seat0, seat1, s, data) || OneWayKitePair(seat1, seat0, s, data);
        }

        /// <summary>True when some x of <paramref name="xs"/> is kited by some y of <paramref name="ys"/> and can hit it.</summary>
        /// <param name="xs">Entries of the seat that may be kited (the kited side). Unit: unit groups (count per entry). Range: any list, may be empty.</param>
        /// <param name="ys">Entries of the other seat (the possible kiter). Unit: unit groups (count per entry). Range: any list, may be empty.</param>
        /// <param name="s">Settings with the kite rule's range gap (tiles) and speed ratio (ratio). Range: the values from WithKite.</param>
        /// <param name="data">The loaded data set, for the combat rules. Unit: none (object). Range: non-null.</param>
        private static bool OneWayKitePair(List<FightEntry> xs, List<FightEntry> ys, PredictorSettings s, DataSet data)
        {
            foreach (FightEntry x in xs)
            {
                foreach (FightEntry y in ys)
                {
                    if (FightPredictor.Kites(y.Definition, x.Definition, s) && FightPredictor.Dps(x.Definition, y.Definition, false, data.Content.Combat) > Fix64.Zero)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>The kite_pair JSON field (with its leading comma) of a fight.</summary>
        /// <param name="f">The predicted fight, whose measured record is tested. Unit: none (object). Range: non-null.</param>
        /// <param name="s">Settings with the kite term. Unit: factor (fraction), tiles (range gap), ratio (speed). Range: the values from WithKite.</param>
        /// <param name="data">The loaded data set, for the combat rules. Unit: none (object). Range: non-null.</param>
        private static string KitePairField(PredictedFight f, PredictorSettings s, DataSet data)
        {
            return ",\"kite_pair\":" + (IsKitePair(f.Record, s, data) ? "true" : "false");
        }

        /// <summary>The predict.md table of accuracy on decided fights split by kite_pair; <paramref name="kitePairFights"/> receives the number of kite-pair fights (start predictions).</summary>
        /// <param name="e">The evaluation whose start predictions are split. Unit: none (object). Range: non-null; may hold no fights.</param>
        /// <param name="s">Settings with the kite term. Unit: factor (fraction), tiles (range gap), ratio (speed). Range: the values from WithKite.</param>
        /// <param name="data">The loaded data set, for the combat rules and the Wilson z-score. Unit: none (object). Range: non-null.</param>
        /// <param name="kitePairFights">Set by this method to the number of kite-pair fights. Unit: fights (count). Range: 0 up to e.Fights.Count.</param>
        private static string KiteTable(PredictEvaluation e, PredictorSettings s, DataSet data, out int kitePairFights)
        {
            // Tallies of the fights with and without a kite pair.
            PredictTally with = new PredictTally();
            PredictTally without = new PredictTally();
            kitePairFights = 0;
            foreach (PredictedFight f in e.Fights)
            {
                if (IsKitePair(f.Record, s, data))
                {
                    kitePairFights++;
                    with.Add(f);
                }
                else
                {
                    without.Add(f);
                }
            }
            // Wilson z-score (tuning/harness_batch.json). Range: 1 to 4.
            double z = HarnessStats.ToDouble(data.Tuning.HarnessBatch.ConfidenceZ);
            StringBuilder sb = new StringBuilder();
            sb.Append("\n## Accuracy by kite pair (--predict-kite-factor)\n\nA kite pair: some unit of one seat is kited (rule D.6.2 keys of the profile) by a unit of the other and can hit it. The recorded fights were played with plain attack-move, so a fall on kite-pair fights is the intended belief, not an error.\n\n");
            Header(sb, "kite_pair");
            Row(sb, "true", with, z, 0);
            Row(sb, "false", without, z, 0);
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
            CalibrationTable(sb, e, z, null);

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

        /// <summary>The calibration table (C.6) of one evaluation; with a baseline, a column of the baseline's win share in the same bin.</summary>
        private static void CalibrationTable(StringBuilder sb, PredictEvaluation e, double z, PredictEvaluation? baseline)
        {
            sb.Append("| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) |").Append(baseline == null ? string.Empty : " baseline share |")
                .Append("\n| --- | ---: | ---: | ---: | --- | ---: |").Append(baseline == null ? string.Empty : " ---: |").Append('\n');
            sb.Append("| predicted none | ").Append(e.DecidedPredictedNone.ToString(CultureInfo.InvariantCulture)).Append(" | - | - | - | - |").Append(baseline == null ? string.Empty : " - |").Append('\n');
            for (int i = 0; i < e.Bins.Count; i++)
            {
                CalibrationBin bin = e.Bins[i];
                WinRate rate = HarnessStats.Wilson(bin.Wins, 0, bin.Decided - bin.Wins, z);
                sb.Append("| ").Append(bin.Low.ToString()).Append(bin.High.HasValue ? " to below " + bin.High.Value.ToString() : " and above").Append(" | ").Append(bin.Decided.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(bin.Wins.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(PredictEvaluator.Ratio3(bin.Wins, bin.Decided)).Append(" | ")
                    .Append(rate.Games > 0 ? "[" + HarnessStats.Percent(rate.Low) + ", " + HarnessStats.Percent(rate.High) + "]" : "-").Append(" | ")
                    .Append(bin.WinnerLeftMilli.Count > 0 ? PredictEvaluator.Ratio3(HarnessStats.Percentile(bin.WinnerLeftMilli, 50), 1000) : "-").Append(" |")
                    .Append(baseline == null ? string.Empty : " " + PredictEvaluator.Ratio3(baseline.Bins[i].Wins, baseline.Bins[i].Decided) + " (" + baseline.Bins[i].Wins.ToString(CultureInfo.InvariantCulture) + "/"
                        + baseline.Bins[i].Decided.ToString(CultureInfo.InvariantCulture) + ") |").Append('\n');
            }
        }

        /// <summary>A table of tallies with a baseline accuracy column (empty when there is no baseline), in the order given.</summary>
        private static void SubsetTable(StringBuilder sb, string first, IEnumerable<KeyValuePair<string, PredictTally>> rows, SortedDictionary<string, PredictTally>? baseline, double z, int minFights)
        {
            sb.Append("| ").Append(first).Append(" | fights | decided | correct | accuracy | Wilson interval (%) |").Append(baseline == null ? string.Empty : " baseline accuracy |").Append(" |\n| --- | ---: | ---: | ---: | ---: | --- |")
                .Append(baseline == null ? string.Empty : " ---: |").Append(" --- |\n");
            foreach (KeyValuePair<string, PredictTally> pair in rows)
            {
                PredictTally t = pair.Value;
                WinRate rate = HarnessStats.Wilson(t.Correct, 0, t.Decided - t.Correct, z);
                sb.Append("| ").Append(pair.Key).Append(" | ").Append(t.Fights.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(t.Decided.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(t.Correct.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(t.AccuracyText()).Append(" | ")
                    .Append(rate.Games > 0 ? "[" + HarnessStats.Percent(rate.Low) + ", " + HarnessStats.Percent(rate.High) + "]" : "-").Append(" |");
                if (baseline != null)
                {
                    sb.Append(' ').Append(baseline.TryGetValue(pair.Key, out PredictTally? other) ? other.AccuracyText() : "-").Append(" |");
                }
                sb.Append(t.Decided < minFights ? " small |\n" : "  |\n");
            }
            sb.Append('\n');
        }

        /// <summary>The section of one subset (E, U or D): headline row, the breakdowns of C.5 2 to 4 (unit pair, side, case, army value) and the calibration table, with baseline columns when given.</summary>
        private static void SubsetSection(StringBuilder sb, string title, string what, PredictEvaluation e, PredictEvaluation? baseline, double z, int minFights)
        {
            sb.Append("## ").Append(title).Append("\n\n").Append(what).Append("\n\n");
            sb.Append("Headline: ").Append(e.Overall.Correct.ToString(CultureInfo.InvariantCulture)).Append(" of ").Append(e.Overall.Decided.ToString(CultureInfo.InvariantCulture)).Append(" decided fights = ")
                .Append(e.Overall.AccuracyText()).Append(baseline == null ? string.Empty : "; baseline " + baseline.Overall.AccuracyText()).Append(". No-winner fights ").Append(e.NoWinner.ToString(CultureInfo.InvariantCulture))
                .Append(", predicted none for ").Append(e.NoWinnerAgree.ToString(CultureInfo.InvariantCulture)).Append("; decided fights predicted none ").Append(e.DecidedPredictedNone.ToString(CultureInfo.InvariantCulture)).Append(".\n\n");
            SubsetTable(sb, "unordered unit pair (accuracy ascending)", PredictEvaluator.SortByAccuracy(e.ByPair), baseline?.ByPair, z, minFights);
            SubsetTable(sb, "side (accuracy ascending)", PredictEvaluator.SortByAccuracy(e.BySide), baseline?.BySide, z, minFights);
            SubsetTable(sb, "case", e.ByCase, baseline?.ByCase, z, minFights);
            SubsetTable(sb, "army value (zero padded)", e.ByValue, baseline?.ByValue, z, minFights);
            sb.Append("Calibration (decided fights, predicted winner by margin bin):\n\n");
            CalibrationTable(sb, e, z, baseline);
            sb.Append('\n');
        }

        /// <summary>The subset sections and the bar table appended to predict.md for extended records (fight_predictor_v2.md 5.4).</summary>
        private static void AppendExtended(StringBuilder sb, PredictSubsets cand, PredictSubsets? baseline, IReadOnlyList<BarResult> bars, string baselineId, double z, PredictSettings s)
        {
            sb.Append("\n# Subsets of extended records (fight_predictor_v2.md 5.4)\n\n");
            sb.Append("Start predictions use GapTiles = start_distance_tiles, the water of lane_depth_class (Shallow in a flooded case without one) and neutral water; snapshot predictions use GapTiles 0 and the health of the snapshot. ")
                .Append(baseline == null ? "No --baseline-profile: the comparison bars are UNJUDGED.\n\n" : "Baseline columns: profile `" + baselineId + "`.\n\n");
            SubsetSection(sb, "Subset E", "Start predictions of fights with equal target values (ratio 1).", cand.E, baseline?.E, z, s.MinFightsPerRow);
            SubsetSection(sb, "Subset U", "Start predictions of fights where seat 1's target value differs (seat1_value_ratios).", cand.U, baseline?.U, z, s.MinFightsPerRow);
            SubsetSection(sb, "Subset D", "Snapshot predictions of damaged armies: the armies when either seat had lost snapshot_loss_fraction of its value, scored against the fight's measured winner.", cand.D, baseline?.D, z, s.MinFightsPerRow);
            sb.Append("## Bars\n\n| ID | bar | measured | result | reason |\n| --- | --- | --- | --- | --- |\n");
            foreach (BarResult bar in bars)
            {
                sb.Append("| ").Append(bar.Id).Append(" | ").Append(bar.Bar.Replace("|", "\\|")).Append(" | ").Append(bar.Measured.Replace("|", "\\|")).Append(" | ").Append(bar.Status).Append(" | ").Append(bar.Reason).Append(" |\n");
            }
            sb.Append('\n');
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

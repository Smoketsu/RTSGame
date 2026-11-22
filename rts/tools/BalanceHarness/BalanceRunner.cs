// rts/tools/BalanceHarness/BalanceRunner.cs
// Command --balance (spec: rts/docs/specs/balance_measurement.md M.5): self-play of one profile in both slots with the summary observer (the batch seat rule, seeds from base_seed), the predictor accuracy of package C
// (the --fights sweep, or --fights-from a fights.jsonl) and the report of BalanceReport. Writes balance.jsonl (a meta line, one line per game, a predictor line) and balance.md; --from re-renders the report from a written
// balance.jsonl without playing, byte for byte. stdout: one CHECK line per trust check and the last line BALANCE. Exit codes: 0 (failed checks included), 2 a check failed and --strict was given, 3 the determinism spot check
// failed, 1 error. Output depends only on data, options and records, never on --jobs or the clock. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rts.Ai;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Coordinator of --balance.</summary>
    public static class BalanceRunner
    {
        /// <summary>The predictor accuracy over decided fights, from package C: every record predicted with the predictor of predict.json's profile and scored.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="dataDirectory">The data folder (for predict.json and fights.json).</param>
        /// <param name="fights">Fight records.</param>
        public static PredictorResult Score(DataSet data, string dataDirectory, IReadOnlyList<FightRecord> fights)
        {
            PredictSettings predictSettings = PredictSettings.Load(dataDirectory);
            FightSettings fightSettings = FightSettings.Load(dataDirectory, data);
            PredictorSettings predictor = PredictorSettings.From(AiProfile.FromDataSet(data, predictSettings.PredictorProfile));
            List<PredictedFight> predicted = new List<PredictedFight>();
            for (int i = 0; i < fights.Count; i++)
            {
                try
                {
                    predicted.Add(PredictEvaluator.Predict(fights[i], fightSettings.CaseById(fights[i].Case).Kind, data, predictor));
                }
                catch (HarnessException ex)
                {
                    throw new HarnessException("fight record " + (i + 1).ToString(CultureInfo.InvariantCulture) + " (" + fights[i].Fight + "): " + ex.Message);
                }
            }
            PredictEvaluation evaluation = PredictEvaluator.Evaluate(predicted, fightSettings, data, predictSettings);
            return new PredictorResult(evaluation.Overall.Decided, evaluation.Overall.Correct);
        }

        /// <summary>Runs the command for parsed options.</summary>
        /// <param name="options">Parsed options (--balance, --profile, --from, --fights-from, --strict and the overrides).</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="output">stdout: the CHECK lines and the BALANCE line last.</param>
        /// <param name="error">stderr: progress and elapsed time.</param>
        public static int Run(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            // Wall clock for the elapsed line on stderr only (tool code, never fed to a simulation).
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            BalanceSettings settings = BalanceSettings.Load(options.DataDirectory);
            AiEvalSettings eval = AiEvalSettings.Load(options.DataDirectory);
            // The records, what the run was played with, the predictor result and the spot check result.
            List<GameRecord> records = new List<GameRecord>();
            BalanceMeta meta;
            PredictorResult? predictor = null;
            int mismatches = 0;
            if (options.From.Length > 0)
            {
                if (!File.Exists(options.From))
                {
                    throw new HarnessException("--from: file not found: " + options.From);
                }
                meta = ReadJsonl(File.ReadAllLines(options.From), records, out predictor);
            }
            else
            {
                string profile = options.Profile.Length > 0 ? options.Profile : settings.Profile;
                int games = options.Games ?? settings.GamesPerCell;
                if (games < 2 || games % 2 != 0)
                {
                    throw new HarnessException("--games for --balance must be a multiple of 2 (found " + games.ToString(CultureInfo.InvariantCulture) + ")");
                }
                int maxSeconds = options.MaxGameSeconds ?? settings.MaxGameSeconds;
                if (maxSeconds < 60)
                {
                    throw new HarnessException("--max-game-seconds for --balance must be 60 to 7200 (found " + maxSeconds.ToString(CultureInfo.InvariantCulture) + ")");
                }
                ulong baseSeed = options.Seed ?? settings.BaseSeed;
                List<string> matchups = BatchRunner.ResolveMatchups(data, options.Matchups ?? settings.Matchups);
                List<string> maps = BatchRunner.ResolveMaps(data, options.Maps ?? settings.Maps);
                int jobs = options.Jobs ?? Math.Min(64, Math.Max(1, Environment.ProcessorCount));
                // Fails early (exit 1) when the profile file is missing.
                AiProfile.FromDataSet(data, profile);
                meta = new BalanceMeta(profile, baseSeed, games, maxSeconds);
                List<string> common = new List<string> { "--data", options.DataDirectory, "--seed", baseSeed.ToString(CultureInfo.InvariantCulture), "--max-game-seconds", maxSeconds.ToString(CultureInfo.InvariantCulture),
                    "--games", games.ToString(CultureInfo.InvariantCulture), "--profile", profile, "--summary" };
                if (options.TestFault.Length > 0)
                {
                    common.Add("--test-fault");
                    common.Add(options.TestFault);
                }
                records = BehaviourRunner.PlayCells(maps, matchups, games, jobs, data.Tuning.HarnessBatch.ChunkGames, common, error);
                // Determinism spot check: the first games of the first cell run again in this process, summaries included.
                int verify = Math.Min(eval.LadderVerifyGames, games);
                mismatches = BehaviourRunner.SpotCheck(records, g => GameRunner.Play(data, maps[0], matchups[0], g, SeedDerivation.GameSeed(baseSeed, maps[0], matchups[0], g), profile, profile, maxSeconds, null, -1, -1,
                    eval.NewObserver()), verify, maps[0], matchups[0]);
                if (options.FightsFrom.Length == 0)
                {
                    // The fight sweep of package C (about 22 s at 4 jobs); its own files go to a subfolder of the output folder.
                    string sweepOut = Path.Combine(OutDirectory(options), "fights");
                    BatchOptions sweep = BatchOptions.Parse(new[] { "--data", options.DataDirectory, "--fights", "--jobs", jobs.ToString(CultureInfo.InvariantCulture), "--out", sweepOut });
                    List<FightRecord> fightRecords = new List<FightRecord>();
                    int sweepCode = FightCoordinator.RunSweepFor(sweep, data, new StringWriter(), error, fightRecords);
                    if (sweepCode == 3)
                    {
                        mismatches++;
                    }
                    predictor = Score(data, options.DataDirectory, fightRecords);
                }
            }
            if (options.FightsFrom.Length > 0)
            {
                if (!File.Exists(options.FightsFrom))
                {
                    throw new HarnessException("--fights-from: file not found: " + options.FightsFrom);
                }
                predictor = Score(data, options.DataDirectory, PredictRunner.ReadRecords(File.ReadAllLines(options.FightsFrom)));
            }

            // The report and the four checks.
            List<BalanceCheck> checks = BalanceReport.Checks(records, data, settings, eval.SummaryBankTimesSeconds, predictor);
            LadderTally all = new LadderTally();
            foreach (GameRecord record in records)
            {
                all.Add(record);
            }
            int passed = 0;
            bool failed = false;
            foreach (BalanceCheck check in checks)
            {
                passed += check.Status == BalanceCheck.Pass ? 1 : 0;
                failed |= check.Status == BalanceCheck.Fail;
            }
            string outDirectory = OutDirectory(options);
            Directory.CreateDirectory(outDirectory);
            StringBuilder jsonl = new StringBuilder();
            jsonl.Append(meta.ToJsonLine()).Append('\n');
            foreach (GameRecord record in records)
            {
                jsonl.Append(record.ToJsonLine()).Append('\n');
            }
            jsonl.Append(predictor == null ? "{\"predictor\":null}" : "{\"predictor\":{\"decided\":" + predictor.Decided.ToString(CultureInfo.InvariantCulture) + ",\"correct\":" + predictor.Correct.ToString(CultureInfo.InvariantCulture) + "}}").Append('\n');
            File.WriteAllText(Path.Combine(outDirectory, "balance.jsonl"), jsonl.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDirectory, "balance.md"), BalanceReport.Build(records, data, settings, meta, eval.SummaryBankTimesSeconds, checks, eval), new UTF8Encoding(false));
            foreach (BalanceCheck check in checks)
            {
                output.Write(check.Line + "\n");
            }
            output.Write("BALANCE profile=" + meta.Profile + " games=" + records.Count.ToString(CultureInfo.InvariantCulture) + " decided=" + all.Decided.ToString(CultureInfo.InvariantCulture)
                + " distinct=" + all.Distinct.ToString(CultureInfo.InvariantCulture) + " checks_passed=" + passed.ToString(CultureInfo.InvariantCulture) + " of=" + checks.Count.ToString(CultureInfo.InvariantCulture) + "\n");
            output.Flush();
            error.Write("elapsed " + clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s\n");
            return mismatches > 0 ? 3 : failed && options.Strict ? 2 : 0;
        }

        /// <summary>The output folder: --out, or the balance subfolder of the default harness folder.</summary>
        private static string OutDirectory(BatchOptions options)
        {
            return options.OutDirectory == BatchOptions.DefaultOutDirectory ? Path.Combine(BatchOptions.DefaultOutDirectory, "balance") : options.OutDirectory;
        }

        /// <summary>Reads a balance.jsonl: the meta line, the game records (appended to <paramref name="records"/>) and the predictor line; a missing meta line is an error.</summary>
        /// <param name="lines">The lines of the file.</param>
        /// <param name="records">Receives the game records in file order.</param>
        /// <param name="predictor">Receives the predictor result, or null when the line says null or is absent.</param>
        public static BalanceMeta ReadJsonl(IReadOnlyList<string> lines, List<GameRecord> records, out PredictorResult? predictor)
        {
            BalanceMeta? meta = null;
            predictor = null;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Length == 0)
                {
                    continue;
                }
                try
                {
                    JsonValue root = JsonReader.Parse(lines[i], "balance.jsonl line " + (i + 1).ToString(CultureInfo.InvariantCulture));
                    if (root.TryGetMember("balance", out JsonValue? node))
                    {
                        meta = new BalanceMeta(node.RequireString("profile"), ulong.Parse(node.RequireString("base_seed"), NumberStyles.None, CultureInfo.InvariantCulture), node.RequireInt("games_per_cell", 2, 10000),
                            node.RequireInt("max_game_seconds", 60, 7200));
                    }
                    else if (root.TryGetMember("predictor", out JsonValue? predictorNode))
                    {
                        predictor = predictorNode.Kind == JsonKind.Null ? null : new PredictorResult(predictorNode.RequireInt("decided", 0, int.MaxValue), predictorNode.RequireInt("correct", 0, int.MaxValue));
                    }
                    else
                    {
                        records.Add(GameRecord.Parse(lines[i]));
                    }
                }
                catch (Exception ex) when (ex is DataValidationException || ex is FormatException || ex is OverflowException)
                {
                    throw new HarnessException("balance.jsonl line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
                }
            }
            if (meta == null)
            {
                throw new HarnessException("balance.jsonl has no meta line");
            }
            return meta;
        }
    }
}

// rts/tools/BalanceHarness/BehaviourRunner.cs
// Command --behaviour (spec: rts/docs/specs/balance_measurement.md M.4.5): plays the paired layout of M.1 with the summary observer (games_per_cell of behaviour_checks.json), evaluates every enabled behaviour check
// (or those of --checks) for both profiles over their slot-games, writes behaviour.jsonl and behaviour.md and prints one BEHAVIOUR line per check and profile (candidate first) and BEHAVIOUR_SUMMARY. A profile passes a
// check when violated x 10 000 is at most max_violation_fraction x judged (integers). Exit codes: 0 every candidate check passes, 2 any fails, 3 the determinism spot check failed, 1 error. Baseline lines are information.
// Also holds PlayCells, the cell and chunk loop shared with --balance. Output depends only on data, options and records, never on --jobs or the clock. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Tally of one check over the slot-games of one profile.</summary>
    public sealed class CheckTally
    {
        /// <summary>The profile ID.</summary>
        public string Profile { get; }

        /// <summary>Slot-games judged held. Unit: slot-games. Range: 0 upward.</summary>
        public int Held { get; private set; }

        /// <summary>Slot-games judged violated. Unit: slot-games. Range: 0 upward.</summary>
        public int Violated { get; private set; }

        /// <summary>Slot-games with nothing to judge. Unit: slot-games. Range: 0 upward.</summary>
        public int Unjudged { get; private set; }

        /// <summary>Metric of every judged slot-game as (numerator, denominator). Unit: as the check.</summary>
        public List<KeyValuePair<long, long>> Metrics { get; } = new List<KeyValuePair<long, long>>();

        /// <summary>Extra counters summed over all slot-games. Unit: as the check.</summary>
        public long[] Extras { get; }

        /// <summary>Creates an empty tally.</summary>
        /// <param name="profile">Profile ID.</param>
        /// <param name="extras">Number of extra counters of the check. Range: 0 upward.</param>
        public CheckTally(string profile, int extras)
        {
            Profile = profile;
            Extras = new long[extras];
        }

        /// <summary>Slot-games judged (held plus violated). Unit: slot-games.</summary>
        public int Judged => Held + Violated;

        /// <summary>Adds one slot-game's result.</summary>
        /// <param name="result">The check's result.</param>
        public void Add(BehaviourResult result)
        {
            if (result.Outcome == CheckOutcome.Held)
            {
                Held++;
            }
            else if (result.Outcome == CheckOutcome.Violated)
            {
                Violated++;
            }
            else
            {
                Unjudged++;
            }
            if (result.Outcome != CheckOutcome.Unjudged)
            {
                Metrics.Add(new KeyValuePair<long, long>(result.Num, result.Den));
            }
            for (int i = 0; i < Extras.Length && i < result.Extras.Count; i++)
            {
                Extras[i] += result.Extras[i];
            }
        }

        /// <summary>The profile passes when violated x 10 000 is at most the allowed share (parts per 10 000) of the judged slot-games.</summary>
        /// <param name="maxViolationPt">The check's max_violation_fraction. Unit: parts per 10 000. Range: 0 to 10000.</param>
        public bool Passes(int maxViolationPt)
        {
            return (long)Violated * 10000 <= (long)maxViolationPt * Judged;
        }

        /// <summary>The lower median metric (element (n - 1) / 2 of the ascending list) as "0.000", or "-" with no judged slot-game.</summary>
        public string MedianText()
        {
            if (Metrics.Count == 0)
            {
                return "-";
            }
            List<KeyValuePair<long, long>> sorted = new List<KeyValuePair<long, long>>(Metrics);
            sorted.Sort((a, b) => (a.Key * b.Value).CompareTo(b.Key * a.Value));
            KeyValuePair<long, long> median = sorted[(sorted.Count - 1) / 2];
            return PredictEvaluator.Ratio3(median.Key, median.Value);
        }

        /// <summary>The largest metric as "0.000", or "-" with no judged slot-game.</summary>
        public string MaxText()
        {
            if (Metrics.Count == 0)
            {
                return "-";
            }
            KeyValuePair<long, long> max = Metrics[0];
            foreach (KeyValuePair<long, long> metric in Metrics)
            {
                if (metric.Key * max.Value > max.Key * metric.Value)
                {
                    max = metric;
                }
            }
            return PredictEvaluator.Ratio3(max.Key, max.Value);
        }
    }

    /// <summary>Coordinator of --behaviour.</summary>
    public static class BehaviourRunner
    {
        /// <summary>
        /// Plays every game of the cells in worker processes and returns the records sorted by (map, matchup, game). The workers get <paramref name="common"/> plus the cell and chunk; chunks are sized by
        /// harness_batch.json chunk_games. Progress goes to stderr (it depends on scheduling).
        /// </summary>
        /// <param name="maps">Map IDs, ordinal order.</param>
        /// <param name="matchups">Matchup IDs, ordinal order.</param>
        /// <param name="games">Games per cell. Unit: games. Range: 1 upward.</param>
        /// <param name="jobs">Worker processes at once. Unit: processes. Range: 1 to 64.</param>
        /// <param name="chunkGames">Games per worker call. Unit: games. Range: 1 upward.</param>
        /// <param name="common">Worker arguments shared by every chunk (data folder, seed, cap, profiles and flags).</param>
        /// <param name="error">stderr for progress lines.</param>
        internal static List<GameRecord> PlayCells(IReadOnlyList<string> maps, IReadOnlyList<string> matchups, int games, int jobs, int chunkGames, IReadOnlyList<string> common, TextWriter error)
        {
            List<BatchRunner.Chunk> chunks = new List<BatchRunner.Chunk>();
            List<IReadOnlyList<string>> jobList = new List<IReadOnlyList<string>>();
            foreach (string map in maps)
            {
                foreach (string matchup in matchups)
                {
                    for (int first = 0; first < games; first += chunkGames)
                    {
                        BatchRunner.Chunk chunk = new BatchRunner.Chunk(map, matchup, first, Math.Min(chunkGames, games - first));
                        chunks.Add(chunk);
                        jobList.Add(new List<string>(common) { "--map", map, "--matchup", matchup, "--first", first.ToString(CultureInfo.InvariantCulture), "--count", chunk.Count.ToString(CultureInfo.InvariantCulture) });
                    }
                }
            }
            // Chunks finished so far, for the progress line.
            int done = 0;
            WorkerResult[] results = WorkerPool.RunAll(jobList, jobs, (index, all) =>
            {
                done++;
                error.Write("chunk " + done.ToString(CultureInfo.InvariantCulture) + "/" + jobList.Count.ToString(CultureInfo.InvariantCulture) + " " + chunks[index].Map + " " + chunks[index].Matchup + " "
                    + chunks[index].DoneAfter.ToString(CultureInfo.InvariantCulture) + "/" + games.ToString(CultureInfo.InvariantCulture) + "\n");
            });
            List<GameRecord> records = new List<GameRecord>();
            for (int c = 0; c < chunks.Count; c++)
            {
                records.AddRange(BatchRunner.ReadGames(results[c], chunks[c].Map, chunks[c].Matchup, chunks[c].First, chunks[c].Count));
            }
            records.Sort(BatchRunner.CompareGames);
            return records;
        }

        /// <summary>Re-plays the first games of the first cell in this process with a fresh observer and counts the records whose JSON line differs from the original (the determinism spot check).</summary>
        /// <param name="records">The sorted records of the run.</param>
        /// <param name="play">Plays game g of the first cell with the given observer.</param>
        /// <param name="verify">Games to re-run. Unit: games. Range: 0 to 16.</param>
        /// <param name="map">First cell's map.</param>
        /// <param name="matchup">First cell's matchup.</param>
        internal static int SpotCheck(IReadOnlyList<GameRecord> records, Func<int, GameRecord> play, int verify, string map, string matchup)
        {
            // Games whose second run differed from the first. Unit: games.
            int mismatches = 0;
            for (int g = 0; g < verify; g++)
            {
                GameRecord original = records.First(r => r.Map == map && r.Matchup == matchup && r.Game == g);
                if (original.ToJsonLine() != play(g).ToJsonLine())
                {
                    mismatches++;
                }
            }
            return mismatches;
        }

        /// <summary>The slot-games of profile <paramref name="profile"/>: one summary and record per slot that played it.</summary>
        /// <param name="records">Records with summaries.</param>
        /// <param name="profile">Profile ID.</param>
        public static List<KeyValuePair<GameSummary, GameRecord>> SlotGames(IReadOnlyList<GameRecord> records, string profile)
        {
            List<KeyValuePair<GameSummary, GameRecord>> list = new List<KeyValuePair<GameSummary, GameRecord>>();
            foreach (GameRecord record in records)
            {
                if (record.Summary0 != null && record.Slot0Profile == profile)
                {
                    list.Add(new KeyValuePair<GameSummary, GameRecord>(record.Summary0, record));
                }
                if (record.Summary1 != null && record.Slot1Profile == profile)
                {
                    list.Add(new KeyValuePair<GameSummary, GameRecord>(record.Summary1, record));
                }
            }
            return list;
        }

        /// <summary>The tally of one check over the slot-games of a profile.</summary>
        /// <param name="check">The configured check.</param>
        /// <param name="records">Records with summaries.</param>
        /// <param name="profile">Profile ID.</param>
        public static CheckTally Tally(IBehaviourCheck check, IReadOnlyList<GameRecord> records, string profile)
        {
            CheckTally tally = new CheckTally(profile, check.ExtraNames.Count);
            foreach (KeyValuePair<GameSummary, GameRecord> slotGame in SlotGames(records, profile))
            {
                tally.Add(check.Evaluate(slotGame.Key, slotGame.Value));
            }
            return tally;
        }

        /// <summary>The BEHAVIOUR line of one check and profile (M.4.5).</summary>
        /// <param name="check">The check.</param>
        /// <param name="tally">Its tally for the profile.</param>
        public static string Line(IBehaviourCheck check, CheckTally tally)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("BEHAVIOUR ").Append(check.Id).Append(" profile=").Append(tally.Profile).Append(" games=").Append((tally.Judged + tally.Unjudged).ToString(CultureInfo.InvariantCulture))
                .Append(" held=").Append(tally.Held.ToString(CultureInfo.InvariantCulture)).Append(" violated=").Append(tally.Violated.ToString(CultureInfo.InvariantCulture))
                .Append(" unjudged=").Append(tally.Unjudged.ToString(CultureInfo.InvariantCulture)).Append(" metric_median=").Append(tally.MedianText()).Append(" metric_max=").Append(tally.MaxText())
                .Append(" bar=").Append(check.BarText).Append(tally.Passes(check.MaxViolationPt) ? " PASS" : " FAIL");
            for (int i = 0; i < check.ExtraNames.Count; i++)
            {
                sb.Append(' ').Append(check.ExtraNames[i]).Append('=').Append(tally.Extras[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>
        /// The BEHAVIOUR_BY_OPENING lines of one check and profile (ai_economy_calc.md 3.1): one per (faction, opening) of the profile's slot-games, factions then openings in ordinal order, with the judged
        /// count and the median metric as the BEHAVIOUR line computes it. Empty when the check does not print by opening.
        /// </summary>
        /// <param name="check">The check.</param>
        /// <param name="records">Records with summaries.</param>
        /// <param name="profile">Profile ID.</param>
        public static List<string> ByOpeningLines(IBehaviourCheck check, IReadOnlyList<GameRecord> records, string profile)
        {
            // The BEHAVIOUR_BY_OPENING lines, one per (faction, opening). Unit: lines. Range: 0 upward.
            List<string> result = new List<string>();
            if (!check.PrintByOpening)
            {
                return result;
            }
            // Tallies by "faction opening" key; a space sorts before every letter, digit and underscore, so ordinal key order is faction then opening.
            SortedDictionary<string, CheckTally> groups = new SortedDictionary<string, CheckTally>(StringComparer.Ordinal);
            foreach (GameRecord record in records)
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    GameSummary? summary = slot == 0 ? record.Summary0 : record.Summary1;
                    if (summary == null || (slot == 0 ? record.Slot0Profile : record.Slot1Profile) != profile)
                    {
                        continue;
                    }
                    // Faction and opening of the slot. Range: IDs, the opening possibly empty.
                    string faction = slot == 0 ? record.Slot0Faction : record.Slot1Faction;
                    string opening = slot == 0 ? record.Opening0 : record.Opening1;
                    // Group key, "<faction> <opening>" (opening possibly empty). Range: IDs joined by one space.
                    string key = faction + " " + opening;
                    if (!groups.TryGetValue(key, out CheckTally? tally))
                    {
                        tally = new CheckTally(profile, check.ExtraNames.Count);
                        groups.Add(key, tally);
                    }
                    tally.Add(check.Evaluate(summary, record));
                }
            }
            foreach (KeyValuePair<string, CheckTally> group in groups)
            {
                // Position of the separator between faction and opening. Range: 0 upward.
                int split = group.Key.IndexOf(' ');
                result.Add("BEHAVIOUR_BY_OPENING check=" + check.Id + " profile=" + profile + " faction=" + group.Key.Substring(0, split) + " opening=" + group.Key.Substring(split + 1)
                    + " games=" + group.Value.Judged.ToString(CultureInfo.InvariantCulture) + " metric_median=" + group.Value.MedianText());
            }
            return result;
        }

        /// <summary>Runs the command for parsed options.</summary>
        /// <param name="options">Parsed options (--behaviour, --profile-a, --profile-b, --checks and the overrides).</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="output">stdout: the BEHAVIOUR lines and BEHAVIOUR_SUMMARY last.</param>
        /// <param name="error">stderr: progress and elapsed time.</param>
        public static int Run(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            // Wall clock for the elapsed line on stderr only (tool code, never fed to a simulation).
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            AiEvalSettings settings = AiEvalSettings.Load(options.DataDirectory);
            BehaviourSettings behaviour = BehaviourSettings.Load(options.DataDirectory);
            int games = options.Games ?? behaviour.GamesPerCell;
            if (games < 4 || games % 4 != 0)
            {
                throw new HarnessException("--games for --behaviour must be a multiple of 4 (found " + games.ToString(CultureInfo.InvariantCulture) + ")");
            }
            int maxSeconds = options.MaxGameSeconds ?? settings.LadderMaxGameSeconds;
            if (maxSeconds < 60)
            {
                throw new HarnessException("--max-game-seconds for --behaviour must be 60 to 7200 (found " + maxSeconds.ToString(CultureInfo.InvariantCulture) + ")");
            }
            ulong baseSeed = options.Seed ?? settings.LadderBaseSeed;
            List<string> matchups = BatchRunner.ResolveMatchups(data, options.Matchups ?? settings.LadderMatchups);
            List<string> maps = BatchRunner.ResolveMaps(data, options.Maps ?? settings.LadderMaps);
            int jobs = options.Jobs ?? Math.Min(64, Math.Max(1, Environment.ProcessorCount));
            // Fails early (exit 1) when a profile file is missing.
            AiProfileCheck(data, options.ProfileA);
            AiProfileCheck(data, options.ProfileB);
            // Income bucket length in ticks, and the checks to run.
            int bucketTicks = settings.SummaryBucketSeconds * data.Tuning.Match.TicksPerSecond;
            List<string> notDeclared = new List<string>();
            List<IBehaviourCheck> checks = behaviour.Checks(options.Checks, bucketTicks, notDeclared, settings.SummaryBankTimesSeconds, data.Tuning.Match.TicksPerSecond);

            List<string> common = new List<string> { "--data", options.DataDirectory, "--seed", baseSeed.ToString(CultureInfo.InvariantCulture), "--max-game-seconds", maxSeconds.ToString(CultureInfo.InvariantCulture),
                "--games", games.ToString(CultureInfo.InvariantCulture), "--profile-a", options.ProfileA, "--profile-b", options.ProfileB, "--paired", "--summary" };
            if (options.TestFault.Length > 0)
            {
                common.Add("--test-fault");
                common.Add(options.TestFault);
            }
            List<GameRecord> records = PlayCells(maps, matchups, games, jobs, data.Tuning.HarnessBatch.ChunkGames, common, error);

            // Determinism spot check: the first games of the first cell run again in this process, summaries included.
            int verify = Math.Min(settings.LadderVerifyGames, games);
            int mismatches = SpotCheck(records, g => GameRunner.PlayLadder(data, maps[0], matchups[0], g, baseSeed, options.ProfileA, options.ProfileB, maxSeconds, true, 0UL, null,
                settings.NewObserver()), verify, maps[0], matchups[0]);

            // Lines: per check the candidate then the baseline.
            List<string> lines = new List<string>();
            int passed = 0;
            StringBuilder table = new StringBuilder();
            foreach (IBehaviourCheck check in checks)
            {
                CheckTally candidate = Tally(check, records, options.ProfileB);
                CheckTally baseline = Tally(check, records, options.ProfileA);
                lines.Add(Line(check, candidate));
                lines.AddRange(ByOpeningLines(check, records, options.ProfileB));
                lines.Add(Line(check, baseline));
                lines.AddRange(ByOpeningLines(check, records, options.ProfileA));
                passed += candidate.Passes(check.MaxViolationPt) ? 1 : 0;
            }
            string summaryLine = "BEHAVIOUR_SUMMARY candidate=" + options.ProfileB + " passed=" + passed.ToString(CultureInfo.InvariantCulture) + " of=" + checks.Count.ToString(CultureInfo.InvariantCulture);

            // Output folder: --out, or the behaviour subfolder of the default harness folder.
            string outDirectory = options.OutDirectory == BatchOptions.DefaultOutDirectory ? Path.Combine(BatchOptions.DefaultOutDirectory, "behaviour") : options.OutDirectory;
            Directory.CreateDirectory(outDirectory);
            StringBuilder jsonl = new StringBuilder();
            foreach (GameRecord record in records)
            {
                jsonl.Append(record.ToJsonLine()).Append('\n');
            }
            File.WriteAllText(Path.Combine(outDirectory, "behaviour.jsonl"), jsonl.ToString(), new UTF8Encoding(false));
            table.Append("<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->\n");
            table.Append("# Behaviour checks: candidate ").Append(options.ProfileB).Append(" against baseline ").Append(options.ProfileA).Append("\n\n");
            table.Append("- Base seed ").Append(baseSeed.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(games.ToString(CultureInfo.InvariantCulture)).Append(" games per cell in the paired layout (balance_measurement.md M.1.2), game cap ")
                .Append(maxSeconds.ToString(CultureInfo.InvariantCulture)).Append(" s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.\n");
            table.Append("- Determinism spot check: ").Append(verify.ToString(CultureInfo.InvariantCulture)).Append(" games re-run in-process with the observer, ").Append(mismatches.ToString(CultureInfo.InvariantCulture)).Append(" mismatches.\n");
            table.Append("- Checks not declared in behaviour_checks.json (not run): ").Append(notDeclared.Count == 0 ? "none" : string.Join(", ", notDeclared)).Append(".\n\n");
            table.Append("**").Append(summaryLine).Append("**\n\n");
            foreach (string line in lines)
            {
                table.Append("    ").Append(line).Append('\n');
            }
            File.WriteAllText(Path.Combine(outDirectory, "behaviour.md"), table.ToString(), new UTF8Encoding(false));
            foreach (string line in lines)
            {
                output.Write(line + "\n");
            }
            output.Write(summaryLine + "\n");
            output.Flush();
            error.Write("elapsed " + clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s\n");
            return mismatches > 0 ? 3 : passed < checks.Count ? 2 : 0;
        }

        /// <summary>Throws when the profile file is missing or invalid (loading it validates it).</summary>
        private static void AiProfileCheck(DataSet data, string id)
        {
            Rts.Ai.AiProfile.FromDataSet(data, id);
        }
    }
}

// rts/tools/BalanceHarness/LadderRunner.cs
// Ladder mode of the balance harness (spec: rts/docs/specs/skirmish_ai_v3.md A.1): profile A (the baseline) plays profile B (the candidate) on every selected map and
// matchup, in both seats, and the candidate passes when it wins enough of the decided games. The coordinator splits the cells into chunks and runs them in the same worker
// processes as --batch (WorkerPool, BatchRunner's chunk and record helpers, SeedDerivation with ladder_base_seed), sorts the records, re-runs a few games in-process as a
// determinism spot check, and writes ladder.jsonl and ladder.md. Exit codes: 0 pass, 1 error, 2 bar missed or too few decided games, 3 spot check failed.
// Tool-side only: double is used for the fractions and the Wilson interval, nothing here feeds back into a simulation. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Game counts of a group of ladder games, from the candidate's point of view.</summary>
    public sealed class LadderTally
    {
        /// <summary>Games played. Unit: games. Range: 0 upward.</summary>
        public int Games { get; private set; }

        /// <summary>Games the candidate won. Unit: games. Range: 0 to Games.</summary>
        public int CandidateWins { get; private set; }

        /// <summary>Games the baseline won. Unit: games. Range: 0 to Games.</summary>
        public int BaselineWins { get; private set; }

        /// <summary>Drawn games (no team left standing). Unit: games. Range: 0 to Games.</summary>
        public int Draws { get; private set; }

        /// <summary>Games still undecided at the tick limit. Unit: games. Range: 0 to Games.</summary>
        public int Undecided { get; private set; }

        /// <summary>Decided games: wins of either side plus draws. Unit: games. Range: 0 to Games.</summary>
        public int Decided => CandidateWins + BaselineWins + Draws;

        /// <summary>Play signatures of the decided games (skirmish_ai_v4.md S.1); equal signatures are the same game. Unit: signatures. Range: 0 to Decided entries.</summary>
        private readonly SortedSet<ulong> _signatures = new SortedSet<ulong>();

        /// <summary>Play signatures of the decided games the candidate won. Unit: signatures. Range: 0 to CandidateWins entries.</summary>
        private readonly SortedSet<ulong> _winSignatures = new SortedSet<ulong>();

        /// <summary>Distinct play signatures among the decided games. Unit: games. Range: 0 to Decided.</summary>
        public int Distinct => _signatures.Count;

        /// <summary>Distinct play signatures among the candidate's wins. Unit: games. Range: 0 to Distinct.</summary>
        public int DistinctWins => _winSignatures.Count;

        /// <summary>The slot the candidate (profile B) plays in game <paramref name="game"/>: the one profile A does not play (see <see cref="GameRunner.LadderProfileInSlot"/>).</summary>
        /// <param name="game">Game index. Unit: games. Range: 0 upward.</param>
        public static int CandidateSlot(int game)
        {
            return (game / 2) % 2 == 0 ? 1 : 0;
        }

        /// <summary>Adds one game.</summary>
        /// <param name="record">A ladder game record.</param>
        public void Add(GameRecord record)
        {
            Games++;
            if (record.Result == GameRecord.Undecided)
            {
                Undecided++;
            }
            else if (record.Result == GameRecord.Draw)
            {
                Draws++;
            }
            else if ((record.Result == GameRecord.Slot0 ? 0 : 1) == CandidateSlot(record.Game))
            {
                CandidateWins++;
                _winSignatures.Add(record.PlaySignature);
            }
            else
            {
                BaselineWins++;
            }
            if (record.Result != GameRecord.Undecided)
            {
                _signatures.Add(record.PlaySignature);
            }
        }
    }

    /// <summary>The pass or fail verdict of a ladder run.</summary>
    public sealed class LadderVerdict
    {
        /// <summary>Decided games. Unit: games. Range: 0 upward.</summary>
        public int Decided { get; }

        /// <summary>Candidate wins. Unit: games. Range: 0 to Decided.</summary>
        public int Wins { get; }

        /// <summary>Candidate wins divided by decided games (draws count as decided, not as wins); 0 with no decided game. Unit: fraction. Range: 0 to 1.</summary>
        public double Fraction { get; }

        /// <summary>The bar the fraction had to reach. Unit: fraction. Range: 0 to 1.</summary>
        public double Bar { get; }

        /// <summary>True when enough games were decided and the fraction reached the bar.</summary>
        public bool Pass { get; }

        /// <summary>Evaluates a run.</summary>
        /// <param name="decided">Decided games. Unit: games. Range: 0 upward.</param>
        /// <param name="wins">Candidate wins. Unit: games. Range: 0 to decided.</param>
        /// <param name="minDecided">Decided games needed. Unit: games. Range: 1 upward.</param>
        /// <param name="bar">Fraction needed. Unit: fraction. Range: 0 to 1.</param>
        public LadderVerdict(int decided, int wins, int minDecided, double bar)
        {
            Decided = decided;
            Wins = wins;
            Fraction = decided > 0 ? (double)wins / decided : 0;
            Bar = bar;
            Pass = decided >= minDecided && Fraction >= bar - HarnessStats.BandTolerance;
        }

        /// <summary>Process exit code: 0 on a pass, 2 otherwise (3 for a failed determinism check is decided by the caller).</summary>
        public int ExitCode => Pass ? 0 : 2;

        /// <summary>The last stdout line: "LADDER candidate=&lt;id&gt; baseline=&lt;id&gt; decided=&lt;n&gt; wins=&lt;n&gt; fraction=&lt;0.000&gt; bar=&lt;0.000&gt; PASS|FAIL".</summary>
        /// <param name="candidate">Candidate profile ID.</param>
        /// <param name="baseline">Baseline profile ID.</param>
        /// <param name="extra">Text put before "bar=" (skirmish_ai_v4.md S.3: "distinct=.. mirror=.. cross=.."); empty for none.</param>
        public string Line(string candidate, string baseline, string extra = "")
        {
            return "LADDER candidate=" + candidate + " baseline=" + baseline + " decided=" + Decided.ToString(CultureInfo.InvariantCulture)
                + " wins=" + Wins.ToString(CultureInfo.InvariantCulture) + " fraction=" + Fraction.ToString("0.000", CultureInfo.InvariantCulture)
                + (extra.Length > 0 ? " " + extra : "") + " bar=" + Bar.ToString("0.000", CultureInfo.InvariantCulture) + (Pass ? " PASS" : " FAIL");
        }
    }

    /// <summary>The ladder coordinator.</summary>
    public static class LadderRunner
    {
        /// <summary>Tallies for the rows "mirror", "cross" and "cross, candidate as &lt;faction&gt;" (skirmish_ai_v4.md S.3), keys in ordinal order; a row with no game is absent.</summary>
        /// <param name="records">Ladder game records.</param>
        public static SortedDictionary<string, LadderTally> MirrorCross(IReadOnlyList<GameRecord> records)
        {
            SortedDictionary<string, LadderTally> rows = new SortedDictionary<string, LadderTally>(StringComparer.Ordinal);
            foreach (GameRecord record in records)
            {
                // Faction the candidate plays in this game (ID), and the row keys the game belongs to.
                string candidateFaction = LadderTally.CandidateSlot(record.Game) == 0 ? record.Slot0Faction : record.Slot1Faction;
                string[] keys = record.Slot0Faction == record.Slot1Faction ? new[] { "mirror" } : new[] { "cross", "cross, candidate as " + candidateFaction };
                foreach (string key in keys)
                {
                    if (!rows.TryGetValue(key, out LadderTally? tally))
                    {
                        tally = new LadderTally();
                        rows.Add(key, tally);
                    }
                    tally.Add(record);
                }
            }
            return rows;
        }

        /// <summary>"distinct=&lt;n&gt; mirror=&lt;wins&gt;/&lt;decided&gt; cross=&lt;wins&gt;/&lt;decided&gt;" for the LADDER line (skirmish_ai_v4.md S.3); distinct counts over all decided games.</summary>
        /// <param name="records">Ladder game records.</param>
        public static string StatsText(IReadOnlyList<GameRecord> records)
        {
            SortedDictionary<string, LadderTally> rows = MirrorCross(records);
            LadderTally all = new LadderTally();
            foreach (GameRecord record in records)
            {
                all.Add(record);
            }
            // Wins and decided games of a row; 0 and 0 when the row is absent.
            string Share(string key) => rows.TryGetValue(key, out LadderTally? t) ? t.CandidateWins.ToString(CultureInfo.InvariantCulture) + "/" + t.Decided.ToString(CultureInfo.InvariantCulture) : "0/0";
            return "distinct=" + all.Distinct.ToString(CultureInfo.InvariantCulture) + " mirror=" + Share("mirror") + " cross=" + Share("cross");
        }

        /// <summary>One warning line per (map, matchup) cell whose distinct/decided is below <paramref name="minFraction"/> (skirmish_ai_v4.md S.2); empty when none. Never changes an exit code.</summary>
        /// <param name="records">Ladder game records.</param>
        /// <param name="minFraction">Smallest acceptable share of distinct games among decided games. Unit: fraction. Range: 0 to 1.</param>
        public static List<string> DistinctWarnings(IReadOnlyList<GameRecord> records, double minFraction)
        {
            SortedDictionary<string, LadderTally> cells = new SortedDictionary<string, LadderTally>(StringComparer.Ordinal);
            foreach (GameRecord record in records)
            {
                string key = record.Map + "/" + record.Matchup;
                if (!cells.TryGetValue(key, out LadderTally? tally))
                {
                    tally = new LadderTally();
                    cells.Add(key, tally);
                }
                tally.Add(record);
            }
            List<string> warnings = new List<string>();
            foreach (KeyValuePair<string, LadderTally> cell in cells)
            {
                // Distinct share of the cell's decided games. Unit: fraction. Range: 0 to 1.
                double share = cell.Value.Decided > 0 ? (double)cell.Value.Distinct / cell.Value.Decided : 1.0;
                if (share < minFraction - HarnessStats.BandTolerance)
                {
                    warnings.Add("WARNING " + cell.Key + ": " + cell.Value.Distinct.ToString(CultureInfo.InvariantCulture) + " distinct of " + cell.Value.Decided.ToString(CultureInfo.InvariantCulture)
                        + " decided games (" + share.ToString("0.000", CultureInfo.InvariantCulture) + "); seeds barely vary games, do not read these win rates as balance (P-005 decision 11).");
                }
            }
            return warnings;
        }

        /// <summary>Runs the ladder for parsed options and writes ladder.jsonl and ladder.md to the output folder.</summary>
        /// <param name="options">Parsed options (--ladder, --profile-a, --profile-b and the overrides).</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="output">stdout: progress lines and the final LADDER line.</param>
        /// <param name="error">stderr: elapsed wall time.</param>
        public static int Run(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            // Wall clock for the elapsed line on stderr only (tool code, never fed to a simulation).
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            AiEvalSettings settings = AiEvalSettings.Load(options.DataDirectory);
            // Settings after the command-line overrides (same validation as the file).
            int games = options.Games ?? settings.LadderGamesPerCell;
            if (games < 4 || games % 4 != 0)
            {
                throw new HarnessException("--games for a ladder must be a multiple of 4 (found " + games.ToString(CultureInfo.InvariantCulture) + ")");
            }
            int maxSeconds = options.MaxGameSeconds ?? settings.LadderMaxGameSeconds;
            if (maxSeconds < 60)
            {
                throw new HarnessException("--max-game-seconds for a ladder must be 60 to 7200 (found " + maxSeconds.ToString(CultureInfo.InvariantCulture) + ")");
            }
            ulong baseSeed = options.Seed ?? settings.LadderBaseSeed;
            List<string> matchups = BatchRunner.ResolveMatchups(data, options.Matchups ?? settings.LadderMatchups);
            List<string> maps = BatchRunner.ResolveMaps(data, options.Maps ?? settings.LadderMaps);
            int jobs = options.Jobs ?? Math.Min(64, Math.Max(1, Environment.ProcessorCount));
            int chunkGames = data.Tuning.HarnessBatch.ChunkGames;
            // Fails early (exit 1) when a profile file is missing.
            Rts.Ai.AiProfile.FromDataSet(data, options.ProfileA);
            Rts.Ai.AiProfile.FromDataSet(data, options.ProfileB);

            List<string> common = new List<string> { "--data", options.DataDirectory, "--seed", baseSeed.ToString(CultureInfo.InvariantCulture),
                "--max-game-seconds", maxSeconds.ToString(CultureInfo.InvariantCulture), "--games", games.ToString(CultureInfo.InvariantCulture),
                "--profile-a", options.ProfileA, "--profile-b", options.ProfileB };
            if (options.TestFault.Length > 0)
            {
                common.Add("--test-fault");
                common.Add(options.TestFault);
            }
            // Chunks in (map, matchup, first game) order and their worker argument lists.
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
                        jobList.Add(new List<string>(common) { "--map", map, "--matchup", matchup, "--first", first.ToString(CultureInfo.InvariantCulture),
                            "--count", chunk.Count.ToString(CultureInfo.InvariantCulture) });
                    }
                }
            }
            // Progress lines are printed in chunk order once the chunk and every chunk before it are done, so stdout never depends on scheduling.
            bool[] finished = new bool[jobList.Count];
            int printed = 0;
            WorkerResult[] results = WorkerPool.RunAll(jobList, jobs, (index, all) =>
            {
                finished[index] = true;
                while (printed < chunks.Count && finished[printed])
                {
                    output.Write("cell " + chunks[printed].Map + " " + chunks[printed].Matchup + " " + chunks[printed].DoneAfter.ToString(CultureInfo.InvariantCulture)
                        + "/" + games.ToString(CultureInfo.InvariantCulture) + "\n");
                    printed++;
                }
            });
            List<GameRecord> records = new List<GameRecord>();
            for (int c = 0; c < chunks.Count; c++)
            {
                records.AddRange(BatchRunner.ReadGames(results[c], chunks[c].Map, chunks[c].Matchup, chunks[c].First, chunks[c].Count));
            }
            records.Sort(BatchRunner.CompareGames);

            // Determinism spot check: the first games of the first cell run again in this process; the final hash must match.
            int verify = Math.Min(settings.LadderVerifyGames, games);
            int mismatches = 0;
            for (int g = 0; g < verify; g++)
            {
                GameRecord original = records.Find(r => r.Map == maps[0] && r.Matchup == matchups[0] && r.Game == g)!;
                GameRecord again = GameRunner.Play(data, maps[0], matchups[0], g, SeedDerivation.GameSeed(baseSeed, maps[0], matchups[0], g),
                    GameRunner.LadderProfileInSlot(g, 0, options.ProfileA, options.ProfileB), GameRunner.LadderProfileInSlot(g, 1, options.ProfileA, options.ProfileB), maxSeconds);
                if (original.ToJsonLine() != again.ToJsonLine())
                {
                    mismatches++;
                }
            }

            // Tally of all games, and the verdict against the right bar.
            LadderTally total = new LadderTally();
            foreach (GameRecord record in records)
            {
                total.Add(record);
            }
            double bar = options.NonRegression ? settings.LadderNonRegressionFractionMin : settings.LadderCandidateWinFractionMin;
            LadderVerdict verdict = new LadderVerdict(total.Decided, total.CandidateWins, settings.LadderMinDecided, bar);
            // Distinct, mirror and cross text of the LADDER line, and the distinct-share warnings (reported only, never gating).
            string stats = StatsText(records);
            List<string> warnings = DistinctWarnings(records, HarnessStats.ToDouble(data.Tuning.HarnessBatch.MinDistinctFraction));

            // Output folder: --out, or the ladder subfolder of the default batch folder.
            string outDirectory = options.OutDirectory == BatchOptions.DefaultOutDirectory ? Path.Combine(BatchOptions.DefaultOutDirectory, "ladder") : options.OutDirectory;
            Directory.CreateDirectory(outDirectory);
            StringBuilder lines = new StringBuilder();
            foreach (GameRecord record in records)
            {
                lines.Append(record.ToJsonLine()).Append('\n');
            }
            File.WriteAllText(Path.Combine(outDirectory, "ladder.jsonl"), lines.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDirectory, "ladder.md"),
                BuildReport(data, options, records, verdict, stats, warnings, baseSeed, games, maxSeconds, verify, mismatches), new UTF8Encoding(false));
            // Warnings first so the LADDER line stays the last stdout line.
            foreach (string warning in warnings)
            {
                output.Write(warning + "\n");
            }
            output.Write(verdict.Line(options.ProfileB, options.ProfileA, stats) + "\n");
            output.Flush();
            error.Write("elapsed " + clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s\n");
            return mismatches > 0 ? 3 : verdict.ExitCode;
        }

        /// <summary>Builds ladder.md: the settings, the verdict and the tables per cell, per map, per seat and overall.</summary>
        private static string BuildReport(DataSet data, BatchOptions options, IReadOnlyList<GameRecord> records, LadderVerdict verdict, string stats, IReadOnlyList<string> warnings, ulong baseSeed, int games,
            int maxSeconds, int verify, int mismatches)
        {
            // Confidence z-score of the Wilson interval (tuning/harness_batch.json). Range: 1 to 4.
            double z = HarnessStats.ToDouble(data.Tuning.HarnessBatch.ConfidenceZ);
            StringBuilder sb = new StringBuilder();
            sb.Append("<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->\n");
            sb.Append("# Ladder: candidate ").Append(options.ProfileB).Append(" against baseline ").Append(options.ProfileA).Append("\n\n");
            sb.Append("- Base seed ").Append(baseSeed.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(games.ToString(CultureInfo.InvariantCulture))
                .Append(" games per cell, game cap ").Append(maxSeconds.ToString(CultureInfo.InvariantCulture)).Append(" s, ")
                .Append(options.NonRegression ? "non-regression bar" : "win bar").Append(".\n");
            sb.Append("- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.\n");
            sb.Append("- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = ")
                .Append(z.ToString("0.00", CultureInfo.InvariantCulture)).Append(".\n");
            sb.Append("- Determinism spot check: ").Append(verify.ToString(CultureInfo.InvariantCulture)).Append(" games re-run in-process, ")
                .Append(mismatches.ToString(CultureInfo.InvariantCulture)).Append(" mismatches.\n\n");
            sb.Append("**").Append(verdict.Line(options.ProfileB, options.ProfileA, stats)).Append("**\n\n");
            foreach (string warning in warnings)
            {
                sb.Append(warning).Append("\n\n");
            }
            sb.Append("Intervals count each distinct game once (play signature, skirmish_ai_v4.md S.1); games with the same signature are the same game.\n\n");

            // Groupings: (heading, key of a record) in report order.
            sb.Append("## Per cell\n\n");
            AppendTable(sb, "map / matchup", records, r => r.Map + " / " + r.Matchup, z);
            sb.Append("\n## Per map\n\n");
            AppendTable(sb, "map", records, r => r.Map, z);
            sb.Append("\n## Per seat\n\n");
            AppendTable(sb, "candidate seat", records, r => "slot " + LadderTally.CandidateSlot(r.Game).ToString(CultureInfo.InvariantCulture), z);
            sb.Append("\n## Mirror and cross-faction\n\n");
            AppendRows(sb, "row", MirrorCross(records), z);
            sb.Append("\n## Overall\n\n");
            AppendTable(sb, "all", records, r => "all games", z);
            return sb.ToString();
        }

        /// <summary>Appends a Markdown table with one row per distinct key (ordinal order).</summary>
        private static void AppendTable(StringBuilder sb, string heading, IReadOnlyList<GameRecord> records, Func<GameRecord, string> key, double z)
        {
            // Tallies by group key in ordinal order.
            SortedDictionary<string, LadderTally> groups = new SortedDictionary<string, LadderTally>(StringComparer.Ordinal);
            foreach (GameRecord record in records)
            {
                string k = key(record);
                if (!groups.TryGetValue(k, out LadderTally? tally))
                {
                    tally = new LadderTally();
                    groups.Add(k, tally);
                }
                tally.Add(record);
            }
            AppendRows(sb, heading, groups, z);
        }

        /// <summary>Appends the table header and one row per tally; the Wilson interval is over distinct games (skirmish_ai_v4.md S.2).</summary>
        private static void AppendRows(StringBuilder sb, string heading, SortedDictionary<string, LadderTally> groups, double z)
        {
            sb.Append("| ").Append(heading).Append(" | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |\n");
            sb.Append("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |\n");
            foreach (KeyValuePair<string, LadderTally> pair in groups)
            {
                LadderTally t = pair.Value;
                // Plain fraction over all decided games, and the interval over distinct games (wins = distinct winning signatures).
                WinRate plain = HarnessStats.Wilson(t.CandidateWins, 0, t.Decided - t.CandidateWins, z);
                WinRate rate = HarnessStats.Wilson(t.DistinctWins, 0, t.Distinct - t.DistinctWins, z);
                sb.Append("| ").Append(pair.Key).Append(" | ").Append(t.Games.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(t.Decided.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(t.Distinct.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(rate.Games > 0 ? rate.P.ToString("0.000", CultureInfo.InvariantCulture) : "-")
                    .Append(" | ").Append(t.CandidateWins.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(t.BaselineWins.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(t.Draws.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(t.Undecided.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(plain.Games > 0 ? plain.P.ToString("0.000", CultureInfo.InvariantCulture) : "-").Append(" | ")
                    .Append(rate.Games > 0 ? "[" + HarnessStats.Percent(rate.Low) + ", " + HarnessStats.Percent(rate.High) + "]" : "-").Append(" |\n");
            }
        }
    }
}

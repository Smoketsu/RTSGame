// rts/tools/BalanceHarness/PoolRunner.cs
// Command --pool (spec: rts/docs/specs/balance_measurement.md M.3): the candidate (--profile-b) plays a paired ladder (M.1) against every member of the opponent pool (pool.json) of the chosen stage, all chunks of
// all members in one WorkerPool.RunAll call. Each member's "all" row is gated (PoolGate: worse, better with a Bonferroni level, incomplete or no effect) and the pool has one verdict. Writes pool.jsonl and pool.md and
// prints one MEMBER line per member and the last line POOL. A member that is the candidate's own profile without forced openings is skipped. Output depends only on data, options and records, never on --jobs or the
// clock (elapsed time goes to stderr). Exit codes as M.1.6: 0 PASS, 2 FAIL or INCOMPLETE, 3 spot check failed, 4 NO_MEASURABLE_EFFECT, 1 error. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rts.Ai;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The gate of the opponent pool (M.3.4): classifies each member from its "all" row and gives the pool verdict.</summary>
    public static class PoolGate
    {
        /// <summary>Member status: significantly worse than the member at effect_alpha (W below L).</summary>
        public const string Worse = "worse";

        /// <summary>Member status: its pairs were too incomplete to read (and it is not worse).</summary>
        public const string Incomplete = "incomplete";

        /// <summary>Member status: significantly better at effect_alpha, divided by the number of members run when Bonferroni is on (W above L).</summary>
        public const string Better = "better";

        /// <summary>Member status: none of the above (not significant).</summary>
        public const string NoEffect = "no_effect";

        /// <summary>
        /// The status of one member from its "all" row. Precedence: worse, then incomplete (complete pairs below paired_min_complete_fraction), then better, then no_effect, so a member counts in exactly one status.
        /// </summary>
        /// <param name="all">The member's "all" row.</param>
        /// <param name="members">Members run (the Bonferroni divisor). Unit: members. Range: 1 upward.</param>
        /// <param name="bonferroni">True to test "better" at effect_alpha / members.</param>
        /// <param name="settings">effect_alpha and paired_min_complete_fraction.</param>
        public static string Classify(PairRow all, int members, bool bonferroni, AiEvalSettings settings)
        {
            if (all.W < all.L && all.Test.Significant)
            {
                return Worse;
            }
            if (PairedLadder.Verdict(all, settings) == PairedStats.Incomplete)
            {
                return Incomplete;
            }
            if (all.W > all.L && PairedStats.SignTest(all.W, all.L, settings.EffectAlphaPt, bonferroni ? members : 1).Significant)
            {
                return Better;
            }
            return NoEffect;
        }

        /// <summary>The pool verdict: FAIL when any member is worse, else INCOMPLETE when any is incomplete, else PASS when at least one is better, else NO_MEASURABLE_EFFECT.</summary>
        /// <param name="statuses">The members' statuses from <see cref="Classify"/>.</param>
        public static string Verdict(IReadOnlyList<string> statuses)
        {
            if (statuses.Contains(Worse))
            {
                return PairedStats.Fail;
            }
            if (statuses.Contains(Incomplete))
            {
                return PairedStats.Incomplete;
            }
            return statuses.Contains(Better) ? PairedStats.Pass : PairedStats.NoMeasurableEffect;
        }
    }

    /// <summary>What a pool run found for one member.</summary>
    internal sealed class PoolMemberResult
    {
        /// <summary>The member.</summary>
        public PoolMember Member { get; }

        /// <summary>The member's games, sorted by (map, matchup, game). Empty for a skipped member.</summary>
        public List<GameRecord> Records { get; } = new List<GameRecord>();

        /// <summary>The Pairs-table rows (cells, mirror, cross, all); null for a skipped member.</summary>
        public List<PairRow>? Rows { get; set; }

        /// <summary>Pairing kind text per faction; empty for a skipped member.</summary>
        public string Pairing { get; set; } = string.Empty;

        /// <summary>The member's gate status; empty for a skipped member.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>The member's paired verdict from its "all" row; empty for a skipped member.</summary>
        public string Verdict { get; set; } = string.Empty;

        /// <summary>Bundles the member.</summary>
        public PoolMemberResult(PoolMember member)
        {
            Member = member;
        }

        /// <summary>The MEMBER line of stdout: the PAIRED line's fields from complete= on, or "skipped (candidate)".</summary>
        public string Line()
        {
            if (Rows == null)
            {
                return "MEMBER " + Member.Id + " skipped (candidate)";
            }
            // The PAIRED line of the member's "all" row, cut at "complete=".
            string paired = PairedLadder.Line(string.Empty, Member.Id, Rows[Rows.Count - 1], Pairing, Verdict);
            return "MEMBER " + Member.Id + " " + paired.Substring(paired.IndexOf("complete=", StringComparison.Ordinal));
        }
    }

    /// <summary>Coordinator of --pool.</summary>
    public static class PoolRunner
    {
        /// <summary>Runs the command for parsed options.</summary>
        /// <param name="options">Parsed options (--pool, --profile-b, --stage, --pairs and the overrides).</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="output">stdout: the MEMBER lines and the POOL line last.</param>
        /// <param name="error">stderr: progress and elapsed time.</param>
        public static int Run(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            // Wall clock for the elapsed line on stderr only (tool code, never fed to a simulation).
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            AiEvalSettings settings = AiEvalSettings.Load(options.DataDirectory);
            PoolSettings pool = PoolSettings.Load(options.DataDirectory, data);
            // Stage text and the stage numbers it selects.
            string stage = options.Stage.Length > 0 ? options.Stage : "1";
            string candidateId = options.ProfileB;
            AiProfile candidate = AiProfile.FromDataSet(data, candidateId);
            int maxSeconds = options.MaxGameSeconds ?? settings.LadderMaxGameSeconds;
            if (maxSeconds < 60)
            {
                throw new HarnessException("--max-game-seconds for a pool must be 60 to 7200 (found " + maxSeconds.ToString(CultureInfo.InvariantCulture) + ")");
            }
            ulong baseSeed = options.Seed ?? settings.LadderBaseSeed;
            List<string> matchups = BatchRunner.ResolveMatchups(data, options.Matchups ?? pool.PoolMatchups);
            List<string> maps = BatchRunner.ResolveMaps(data, options.Maps ?? (pool.PoolMaps.Count > 0 ? pool.PoolMaps : settings.LadderMaps));
            int jobs = options.Jobs ?? Math.Min(64, Math.Max(1, Environment.ProcessorCount));
            int chunkGames = data.Tuning.HarnessBatch.ChunkGames;

            // The members of the chosen stage in file order; those that are the candidate itself are skipped.
            List<PoolMemberResult> results = new List<PoolMemberResult>();
            foreach (PoolMember member in pool.Members)
            {
                if (stage != "all" && member.Stage.ToString(CultureInfo.InvariantCulture) != stage)
                {
                    continue;
                }
                results.Add(new PoolMemberResult(member));
            }
            // The members that play, in file order, and their profiles.
            List<PoolMemberResult> playing = new List<PoolMemberResult>();
            SortedDictionary<string, AiProfile> profiles = new SortedDictionary<string, AiProfile>(StringComparer.Ordinal);
            foreach (PoolMemberResult result in results)
            {
                if (result.Member.Profile == candidateId && result.Member.ForceOpening.Count == 0)
                {
                    continue;
                }
                playing.Add(result);
                profiles[result.Member.Id] = result.Member.Resolve(data);
            }
            if (playing.Count == 0)
            {
                throw new HarnessException("no pool member to play for stage " + stage + " and candidate " + candidateId);
            }

            // Chunks per member, in (member, map, matchup, first game) order, and the worker argument lists.
            List<BatchRunner.Chunk> chunks = new List<BatchRunner.Chunk>();
            List<PoolMemberResult> chunkMembers = new List<PoolMemberResult>();
            List<IReadOnlyList<string>> jobList = new List<IReadOnlyList<string>>();
            foreach (PoolMemberResult result in playing)
            {
                // Games per cell of this member: two per pair, pairs from --pairs or the stage's value.
                int games = 2 * (options.Pairs ?? (result.Member.Stage == 1 ? pool.Stage1Pairs : pool.Stage2Pairs));
                List<string> common = new List<string> { "--data", options.DataDirectory, "--seed", baseSeed.ToString(CultureInfo.InvariantCulture),
                    "--max-game-seconds", maxSeconds.ToString(CultureInfo.InvariantCulture), "--games", games.ToString(CultureInfo.InvariantCulture),
                    "--profile-a", result.Member.Profile, "--profile-b", candidateId, "--paired", "--pool-member", result.Member.Id };
                if (options.TestFault.Length > 0)
                {
                    common.Add("--test-fault");
                    common.Add(options.TestFault);
                }
                foreach (string map in maps)
                {
                    foreach (string matchup in matchups)
                    {
                        for (int first = 0; first < games; first += chunkGames)
                        {
                            BatchRunner.Chunk chunk = new BatchRunner.Chunk(map, matchup, first, Math.Min(chunkGames, games - first));
                            chunks.Add(chunk);
                            chunkMembers.Add(result);
                            jobList.Add(new List<string>(common) { "--map", map, "--matchup", matchup, "--first", first.ToString(CultureInfo.InvariantCulture),
                                "--count", chunk.Count.ToString(CultureInfo.InvariantCulture) });
                        }
                    }
                }
            }
            // Progress goes to stderr (it depends on scheduling); stdout stays deterministic.
            int done = 0;
            WorkerResult[] workerResults = WorkerPool.RunAll(jobList, jobs, (index, all) =>
            {
                done++;
                error.Write("pool " + done.ToString(CultureInfo.InvariantCulture) + "/" + jobList.Count.ToString(CultureInfo.InvariantCulture) + " chunks, " + chunkMembers[index].Member.Id + " "
                    + chunks[index].Map + " " + chunks[index].Matchup + " " + chunks[index].DoneAfter.ToString(CultureInfo.InvariantCulture) + "\n");
            });
            for (int c = 0; c < chunks.Count; c++)
            {
                chunkMembers[c].Records.AddRange(BatchRunner.ReadGames(workerResults[c], chunks[c].Map, chunks[c].Matchup, chunks[c].First, chunks[c].Count));
            }
            foreach (PoolMemberResult result in playing)
            {
                result.Records.Sort(BatchRunner.CompareGames);
            }

            // Determinism spot check: the first games of the first member's first cell run again in this process.
            PoolMemberResult first0 = playing[0];
            int firstGames = first0.Records.FindAll(r => r.Map == maps[0] && r.Matchup == matchups[0]).Count;
            int verify = Math.Min(settings.LadderVerifyGames, firstGames);
            int mismatches = 0;
            for (int g = 0; g < verify; g++)
            {
                GameRecord original = first0.Records.Find(r => r.Map == maps[0] && r.Matchup == matchups[0] && r.Game == g)!;
                GameRecord again = GameRunner.PlayPool(data, maps[0], matchups[0], g, baseSeed, profiles[first0.Member.Id], first0.Member.Id, candidate, candidateId, maxSeconds);
                if (original.ToJsonLine() != again.ToJsonLine())
                {
                    mismatches++;
                }
            }

            // Factions of the selected matchups, for the pairing kinds.
            List<string> factions = new List<string>();
            foreach (string matchup in matchups)
            {
                factions.AddRange(GameRunner.SplitMatchup(matchup));
            }
            // Rows, pairing and gate of every playing member.
            List<string> statuses = new List<string>();
            int totalGames = 0;
            foreach (PoolMemberResult result in playing)
            {
                result.Rows = PairedLadder.Rows(PairedLadder.Outcomes(result.Records), settings);
                result.Pairing = PairedLadder.PairingText(profiles[result.Member.Id], candidate, factions);
                PairRow all = result.Rows[result.Rows.Count - 1];
                result.Verdict = PairedLadder.Verdict(all, settings);
                result.Status = PoolGate.Classify(all, playing.Count, pool.BetterBonferroni, settings);
                statuses.Add(result.Status);
                totalGames += result.Records.Count;
            }
            string verdict = PoolGate.Verdict(statuses);

            // Output folder: --out, or the pool subfolder of the default harness folder.
            string outDirectory = options.OutDirectory == BatchOptions.DefaultOutDirectory ? Path.Combine(BatchOptions.DefaultOutDirectory, "pool") : options.OutDirectory;
            Directory.CreateDirectory(outDirectory);
            StringBuilder lines = new StringBuilder();
            foreach (PoolMemberResult result in playing)
            {
                foreach (GameRecord record in result.Records)
                {
                    lines.Append(record.ToJsonLine()).Append('\n');
                }
            }
            File.WriteAllText(Path.Combine(outDirectory, "pool.jsonl"), lines.ToString(), new UTF8Encoding(false));
            // POOL line: counts of each status, the games written and the verdict.
            string poolLine = "POOL candidate=" + candidateId + " stage=" + stage + " members=" + playing.Count.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + (results.Count - playing.Count).ToString(CultureInfo.InvariantCulture) + " better=" + Count(statuses, PoolGate.Better) + " worse=" + Count(statuses, PoolGate.Worse)
                + " no_effect=" + Count(statuses, PoolGate.NoEffect) + " incomplete=" + Count(statuses, PoolGate.Incomplete) + " games=" + totalGames.ToString(CultureInfo.InvariantCulture) + " verdict=" + verdict;
            File.WriteAllText(Path.Combine(outDirectory, "pool.md"), BuildReport(settings, pool, results, candidateId, stage, baseSeed, maxSeconds, verify, mismatches, poolLine), new UTF8Encoding(false));
            foreach (PoolMemberResult result in results)
            {
                output.Write(result.Line() + "\n");
            }
            output.Write(poolLine + "\n");
            output.Flush();
            error.Write("elapsed " + clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s\n");
            return mismatches > 0 ? 3 : PairedStats.ExitCode(verdict);
        }

        /// <summary>How many of the statuses equal <paramref name="status"/>, as text.</summary>
        private static string Count(IReadOnlyList<string> statuses, string status)
        {
            // Matches counted so far. Unit: members. Range: 0 upward.
            int n = 0;
            foreach (string s in statuses)
            {
                n += s == status ? 1 : 0;
            }
            return n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Builds pool.md: the settings, one Pairs table per member and the gate table.</summary>
        private static string BuildReport(AiEvalSettings settings, PoolSettings pool, IReadOnlyList<PoolMemberResult> results, string candidateId, string stage, ulong baseSeed, int maxSeconds,
            int verify, int mismatches, string poolLine)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<!-- pool.md: written by the balance harness --pool (rts/tools/BalanceHarness/PoolRunner.cs); not a source file. -->\n");
            sb.Append("# Opponent pool: candidate ").Append(candidateId).Append(", stage ").Append(stage).Append("\n\n");
            sb.Append("- Base seed ").Append(baseSeed.ToString(CultureInfo.InvariantCulture)).Append(", game cap ").Append(maxSeconds.ToString(CultureInfo.InvariantCulture))
                .Append(" s. Each member is the baseline of a paired ladder (balance_measurement.md M.1): pair p = g / 2, the candidate plays slot g % 2, factions swap on odd pairs, both games of a pair share the seed.\n");
            sb.Append("- Sign test and interval at two-sided alpha ").Append(PairedStats.P4(settings.EffectAlphaPt)).Append(", exact (integer arithmetic). \"worse\" is tested at alpha; \"better\" at alpha divided by the number of members run when better_bonferroni is on (")
                .Append(pool.BetterBonferroni ? "on" : "off").Append("), so \"better\" needs a larger effect than the printed MDE, which is stated at alpha.\n");
            sb.Append("- Determinism spot check: ").Append(verify.ToString(CultureInfo.InvariantCulture)).Append(" games of the first member re-run in-process, ").Append(mismatches.ToString(CultureInfo.InvariantCulture)).Append(" mismatches.\n\n");
            sb.Append("**").Append(poolLine).Append("**\n\n");
            foreach (PoolMemberResult result in results)
            {
                sb.Append("# Member ").Append(result.Member.Id).Append("\n\n");
                sb.Append("Profile ").Append(result.Member.Profile);
                foreach (KeyValuePair<string, string> forced in result.Member.ForceOpening)
                {
                    sb.Append(", forced ").Append(forced.Key).Append(" opening ").Append(forced.Value);
                }
                sb.Append(", stage ").Append(result.Member.Stage.ToString(CultureInfo.InvariantCulture)).Append(".\n\n");
                if (result.Rows == null)
                {
                    sb.Append("Skipped: this member is the candidate's own profile.\n\n");
                    continue;
                }
                sb.Append("Pairing kind per faction: ").Append(result.Pairing).Append(".\n\n**").Append(result.Line()).Append("**\n");
                PairedLadder.AppendPairs(sb, result.Rows);
                sb.Append('\n');
            }
            sb.Append("# Gate\n\n| member | status | verdict of its Pairs table | candidate both (W) | baseline both (L) | p | advantage [interval] | MDE |\n| --- | --- | --- | ---: | ---: | ---: | --- | ---: |\n");
            foreach (PoolMemberResult result in results)
            {
                if (result.Rows == null)
                {
                    sb.Append("| ").Append(result.Member.Id).Append(" | skipped | - | - | - | - | - | - |\n");
                    continue;
                }
                PairRow all = result.Rows[result.Rows.Count - 1];
                sb.Append("| ").Append(result.Member.Id).Append(" | ").Append(result.Status).Append(" | ").Append(result.Verdict).Append(" | ").Append(all.W.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(all.L.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(PairedStats.P4(all.Test.PPt)).Append(" | ").Append(PairedStats.Signed(all.AdvantageTenths)).Append(" [")
                    .Append(PairedStats.Signed(all.AdvantageInterval[0])).Append(", ").Append(PairedStats.Signed(all.AdvantageInterval[1])).Append("] | ").Append(PairedStats.Plain(all.Mde)).Append(" |\n");
            }
            return sb.ToString();
        }
    }
}

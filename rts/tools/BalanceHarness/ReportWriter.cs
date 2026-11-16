// rts/tools/BalanceHarness/ReportWriter.cs
// Turns the sorted game and HQ floor records of a batch into report.md (exact headings of spec rts/docs/specs/balance_harness.md section 5) and
// evaluates the two hard limits: the HQ survivability floor and the water-versus-earth median match length (DEFERRED until an earth faction
// exists). Pure: text and numbers in, text out, invariant culture, no timestamps, so the report is byte-identical between runs. The only place
// besides HarnessStats that uses double. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Determinism spot-check result of one cell.</summary>
    public sealed class VerifyRow
    {
        /// <summary>Map ID of the cell.</summary>
        public string Map { get; }

        /// <summary>Matchup ID of the cell.</summary>
        public string Matchup { get; }

        /// <summary>Games re-run. Unit: games. Range: 0 upward.</summary>
        public int Rerun { get; }

        /// <summary>Re-run games whose result, end tick or state hash differed. Unit: games. Range: 0 to Rerun.</summary>
        public int Mismatches { get; }

        /// <summary>Bundles the values.</summary>
        public VerifyRow(string map, string matchup, int rerun, int mismatches)
        {
            Map = map;
            Matchup = matchup;
            Rerun = rerun;
            Mismatches = mismatches;
        }
    }

    /// <summary>Everything the report is built from.</summary>
    public sealed class ReportInput
    {
        /// <summary>The loaded data set (tick rate, tuning, factions).</summary>
        public DataSet Data { get; set; } = null!;

        /// <summary>Base seed used. Range: any ulong.</summary>
        public ulong BaseSeed { get; set; }

        /// <summary>Games per matchup per map. Unit: games. Range: 1 to 5000.</summary>
        public int GamesPerCell { get; set; }

        /// <summary>AI profile played.</summary>
        public string AiProfile { get; set; } = string.Empty;

        /// <summary>Game length limit used. Unit: seconds. Range: 1 to 7200.</summary>
        public int MaxGameSeconds { get; set; }

        /// <summary>Game records sorted by (map, matchup, game), all ordinal.</summary>
        public IReadOnlyList<GameRecord> Games { get; set; } = new List<GameRecord>();

        /// <summary>HQ floor records sorted by attacker, unit, defender, state.</summary>
        public IReadOnlyList<HqFloorRecord> Floors { get; set; } = new List<HqFloorRecord>();

        /// <summary>Determinism spot-check rows sorted by (map, matchup).</summary>
        public IReadOnlyList<VerifyRow> Verify { get; set; } = new List<VerifyRow>();
    }

    /// <summary>One hard limit's row.</summary>
    public sealed class LimitResult
    {
        /// <summary>The requirement text.</summary>
        public string Requirement { get; }

        /// <summary>The measured value text.</summary>
        public string Measured { get; }

        /// <summary>"PASS", "FAIL" or "DEFERRED".</summary>
        public string Result { get; }

        /// <summary>Bundles the values.</summary>
        public LimitResult(string requirement, string measured, string result)
        {
            Requirement = requirement;
            Measured = measured;
            Result = result;
        }
    }

    /// <summary>Builds the report and evaluates the hard limits.</summary>
    public static class ReportWriter
    {
        /// <summary>Result text of a limit that holds.</summary>
        public const string Pass = "PASS";

        /// <summary>Result text of a limit that is broken.</summary>
        public const string Fail = "FAIL";

        /// <summary>Result text of a limit that cannot be checked yet.</summary>
        public const string Deferred = "DEFERRED";

        /// <summary>Wins of the faction <paramref name="first"/> in game <paramref name="record"/>: 1 win, 0 draw, -1 loss, null when undecided.</summary>
        /// <param name="record">The game.</param>
        /// <param name="first">The first-named faction of the matchup. For a mirror the slot 0 side counts as the first.</param>
        public static int? ScoreFor(GameRecord record, string first)
        {
            if (record.Result == GameRecord.Undecided)
            {
                return null;
            }
            if (record.Result == GameRecord.Draw)
            {
                return 0;
            }
            // True when the first-named faction played slot 0 in this game.
            bool firstIsSlot0 = record.Slot0Faction == first;
            return (record.Result == GameRecord.Slot0) == firstIsSlot0 ? 1 : -1;
        }

        /// <summary>
        /// The HQ survivability floor limit: the smallest time to kill over all Rooted trials (a trial the HQ survived counts as infinite) against
        /// hq_floor_seconds. DEFERRED when there are no Rooted trials.
        /// </summary>
        public static LimitResult EvaluateFloor(ReportInput input)
        {
            // Tick rate and the requirement in ticks. Unit: ticks.
            int tps = input.Data.Tuning.Match.TicksPerSecond;
            int floorTicks = input.Data.Tuning.Harness.HqFloorTicks(new MatchClock(tps));
            string requirement = ">= " + HarnessStats.Seconds(floorTicks, tps) + " s against " + input.Data.Tuning.Harness.HqFloorSupply.ToString(CultureInfo.InvariantCulture) + " supply";
            // Weakest Rooted trial so far (smallest finite time to kill), and whether any Rooted trial ran.
            HqFloorRecord? worst = null;
            bool anyRooted = false;
            foreach (HqFloorRecord record in input.Floors)
            {
                if (record.HqState != "rooted")
                {
                    continue;
                }
                anyRooted = true;
                if (record.TtkTicks >= 0 && (worst == null || record.TtkTicks < worst.TtkTicks))
                {
                    worst = record;
                }
            }
            if (!anyRooted)
            {
                return new LimitResult(requirement, "no trials", Deferred);
            }
            if (worst == null)
            {
                return new LimitResult(requirement, "> " + input.Data.Tuning.HarnessBatch.HqFloorMaxTestSeconds.ToString(CultureInfo.InvariantCulture) + " s (no Rooted HQ died)", Pass);
            }
            return new LimitResult(requirement, HarnessStats.Seconds(worst.TtkTicks, tps) + " s (" + worst.Unit + " x" + worst.Count.ToString(CultureInfo.InvariantCulture)
                + " vs " + worst.DefenderHq + ")", worst.TtkTicks >= floorTicks ? Pass : Fail);
        }

        /// <summary>
        /// The median match length limit for <c>median_cap_matchup</c>: DEFERRED while either faction of the matchup has no faction file or no game of it
        /// was played; otherwise the longest per-map median must be at most water_vs_earth_median_max_seconds.
        /// </summary>
        public static LimitResult EvaluateMedianCap(ReportInput input)
        {
            int tps = input.Data.Tuning.Match.TicksPerSecond;
            int capTicks = input.Data.Tuning.Harness.WaterVsEarthMedianMaxTicks(new MatchClock(tps));
            string requirement = "<= " + HarnessStats.Seconds(capTicks, tps) + " s";
            string matchup = input.Data.Tuning.HarnessBatch.MedianCapMatchup;
            string[] sides = GameRunner.SplitMatchup(matchup);
            foreach (string side in sides)
            {
                if (!input.Data.Content.Factions.ContainsKey(side))
                {
                    return new LimitResult(requirement, "no " + side + " faction", Deferred);
                }
            }
            // Lengths per map in ticks (end tick + 1) over games that ended, maps in ordinal order.
            SortedDictionary<string, List<int>> byMap = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
            foreach (GameRecord record in input.Games)
            {
                if (record.Matchup == matchup && record.Result != GameRecord.Undecided)
                {
                    if (!byMap.TryGetValue(record.Map, out List<int>? list))
                    {
                        list = new List<int>();
                        byMap.Add(record.Map, list);
                    }
                    list.Add(record.EndTick + 1);
                }
            }
            if (byMap.Count == 0)
            {
                return new LimitResult(requirement, "no games played", Deferred);
            }
            // Longest median so far and its map.
            int worstMedian = -1;
            string worstMap = string.Empty;
            foreach (KeyValuePair<string, List<int>> pair in byMap)
            {
                pair.Value.Sort();
                int median = HarnessStats.Percentile(pair.Value, 50);
                if (median > worstMedian)
                {
                    worstMedian = median;
                    worstMap = pair.Key;
                }
            }
            return new LimitResult(requirement, HarnessStats.Seconds(worstMedian, tps) + " s (" + worstMap + ")", worstMedian <= capTicks ? Pass : Fail);
        }

        /// <summary>Builds the whole report text (LF line endings, ends with a newline).</summary>
        /// <param name="input">The batch's records and settings.</param>
        public static string Build(ReportInput input)
        {
            HarnessBatchTuning batch = input.Data.Tuning.HarnessBatch;
            int tps = input.Data.Tuning.Match.TicksPerSecond;
            double bandMin = HarnessStats.ToDouble(batch.WinRateBandMin);
            double bandMax = HarnessStats.ToDouble(batch.WinRateBandMax);
            double z = HarnessStats.ToDouble(batch.ConfidenceZ);
            double maxUndecided = HarnessStats.ToDouble(batch.MaxUndecidedFraction);
            // Z label such as "95%": the two-sided coverage of the interval, rounded to a whole percent.
            string coverage = Math.Round(100.0 * Erf(z / Math.Sqrt(2.0))).ToString("0", CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder();
            // Content digest of the first game, or zero when no game ran.
            ulong digest = input.Games.Count > 0 ? input.Games[0].ContentDigest : 0UL;
            sb.Append("# Balance report\n");
            sb.Append("Data digest `").Append(digest.ToString("x16", CultureInfo.InvariantCulture)).Append("` · base seed ").Append(input.BaseSeed.ToString(CultureInfo.InvariantCulture))
                .Append(" · ").Append(input.GamesPerCell.ToString(CultureInfo.InvariantCulture)).Append(" games per matchup per map · AI profile `").Append(input.AiProfile)
                .Append("` · max game ").Append(input.MaxGameSeconds.ToString(CultureInfo.InvariantCulture)).Append(" s\n\n");

            sb.Append("## Hard limits\n| Limit | Requirement | Measured | Result |\n| --- | --- | --- | --- |\n");
            LimitResult floor = EvaluateFloor(input);
            LimitResult cap = EvaluateMedianCap(input);
            sb.Append("| HQ survivability floor (Rooted) | ").Append(floor.Requirement).Append(" | ").Append(floor.Measured).Append(" | ").Append(floor.Result).Append(" |\n");
            sb.Append("| Water vs earth median length | ").Append(cap.Requirement).Append(" | ").Append(cap.Measured).Append(" | ").Append(cap.Result).Append(" |\n\n");

            // Cells in sorted order: (map, matchup) keys with their games.
            SortedDictionary<string, List<GameRecord>> cells = new SortedDictionary<string, List<GameRecord>>(StringComparer.Ordinal);
            foreach (GameRecord record in input.Games)
            {
                string key = record.Map + "\n" + record.Matchup;
                if (!cells.TryGetValue(key, out List<GameRecord>? list))
                {
                    list = new List<GameRecord>();
                    cells.Add(key, list);
                }
                list.Add(record);
            }
            List<string> warnings = new List<string>();

            sb.Append("## Win rates\n| Map | Matchup | Games | Decided | Draws | Undecided | First faction wins | ").Append(coverage).Append("% interval | Flags |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
            foreach (List<GameRecord> cell in cells.Values)
            {
                string first = GameRunner.SplitMatchup(cell[0].Matchup)[0];
                int wins = 0;
                int losses = 0;
                int draws = 0;
                int undecided = 0;
                foreach (GameRecord record in cell)
                {
                    int? score = ScoreFor(record, first);
                    if (score == null)
                    {
                        undecided++;
                    }
                    else if (score == 0)
                    {
                        draws++;
                    }
                    else if (score > 0)
                    {
                        wins++;
                    }
                    else
                    {
                        losses++;
                    }
                }
                WinRate rate = HarnessStats.Wilson(wins, draws, losses, z);
                string flags = string.Empty;
                if (rate.Games > 0)
                {
                    if (HarnessStats.SignificantlyOutsideBand(rate, bandMin, bandMax))
                    {
                        flags = "outside band, significant";
                    }
                    else if (HarnessStats.OutsideBand(rate.P, bandMin, bandMax))
                    {
                        flags = "outside band";
                    }
                }
                sb.Append("| ").Append(cell[0].Map).Append(" | ").Append(cell[0].Matchup).Append(" | ").Append(cell.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append((wins + losses).ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(draws.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(undecided.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(rate.Games > 0 ? HarnessStats.Percent(rate.P) + "%" : "-").Append(" | ")
                    .Append(rate.Games > 0 ? "[" + HarnessStats.Percent(rate.Low) + ", " + HarnessStats.Percent(rate.High) + "]" : "-").Append(" | ").Append(flags).Append(" |\n");
                if (undecided > maxUndecided * cell.Count + HarnessStats.BandTolerance)
                {
                    warnings.Add(cell[0].Map + " " + cell[0].Matchup + ": " + undecided.ToString(CultureInfo.InvariantCulture) + " of " + cell.Count.ToString(CultureInfo.InvariantCulture)
                        + " games undecided (above " + HarnessStats.Percent(maxUndecided) + "%).");
                }
            }

            sb.Append("\n## Match length (decided games)\n| Map | Matchup | Median | p10 | p90 |\n| --- | --- | --- | --- | --- |\n");
            foreach (List<GameRecord> cell in cells.Values)
            {
                // Lengths in ticks of the games that ended (end tick + 1).
                List<int> lengths = new List<int>();
                foreach (GameRecord record in cell)
                {
                    if (record.Result != GameRecord.Undecided)
                    {
                        lengths.Add(record.EndTick + 1);
                    }
                }
                string[] summary = HarnessStats.LengthSummary(lengths, tps);
                sb.Append("| ").Append(cell[0].Map).Append(" | ").Append(cell[0].Matchup).Append(" | ").Append(summary[0]).Append(" | ").Append(summary[1]).Append(" | ").Append(summary[2]).Append(" |\n");
            }

            sb.Append("\n## HQ time-to-kill\n### Floor trials\n| Attacker | Unit | Count | Defender HQ | State | Time to kill |\n| --- | --- | --- | --- | --- | --- |\n");
            foreach (HqFloorRecord record in input.Floors)
            {
                sb.Append("| ").Append(record.AttackerFaction).Append(" | ").Append(record.Unit).Append(" | ").Append(record.Count.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(record.DefenderHq).Append(" | ").Append(record.HqState == "rooted" ? "Rooted" : "Uprooted").Append(" | ")
                    .Append(record.TtkTicks >= 0 ? HarnessStats.Seconds(record.TtkTicks, tps) + " s" : "> " + batch.HqFloorMaxTestSeconds.ToString(CultureInfo.InvariantCulture) + " s").Append(" |\n");
            }
            sb.Append("\n### In games (median, information only)\n| Map | Matchup | Killed HQ | Kills | Median |\n| --- | --- | --- | --- | --- |\n");
            foreach (List<GameRecord> cell in cells.Values)
            {
                // Time to kill in ticks per killed faction, factions in ordinal order.
                SortedDictionary<string, List<int>> kills = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
                foreach (GameRecord record in cell)
                {
                    foreach (HqKill kill in record.HqKills)
                    {
                        if (!kills.TryGetValue(kill.Faction, out List<int>? list))
                        {
                            list = new List<int>();
                            kills.Add(kill.Faction, list);
                        }
                        list.Add(kill.DeathTick - kill.FirstDamageTick);
                    }
                }
                foreach (KeyValuePair<string, List<int>> pair in kills)
                {
                    pair.Value.Sort();
                    sb.Append("| ").Append(cell[0].Map).Append(" | ").Append(cell[0].Matchup).Append(" | ").Append(pair.Key).Append(" | ").Append(pair.Value.Count.ToString(CultureInfo.InvariantCulture))
                        .Append(" | ").Append(HarnessStats.Seconds(HarnessStats.Percentile(pair.Value, 50), tps)).Append(" s |\n");
                }
            }

            sb.Append("\n## Determinism spot check\n| Map | Matchup | Games re-run | Mismatches |\n| --- | --- | --- | --- |\n");
            foreach (VerifyRow row in input.Verify)
            {
                sb.Append("| ").Append(row.Map).Append(" | ").Append(row.Matchup).Append(" | ").Append(row.Rerun.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(row.Mismatches.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
                if (row.Mismatches > 0)
                {
                    warnings.Add(row.Map + " " + row.Matchup + ": DETERMINISM FAILURE, " + row.Mismatches.ToString(CultureInfo.InvariantCulture) + " of " + row.Rerun.ToString(CultureInfo.InvariantCulture) + " re-run games differ.");
                }
            }

            sb.Append("\n## Warnings\n");
            if (warnings.Count == 0)
            {
                sb.Append("- none\n");
            }
            foreach (string warning in warnings)
            {
                sb.Append("- ").Append(warning).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Error function (Abramowitz and Stegun 7.1.26, absolute error below 1.5e-7), used only to print the interval's coverage.</summary>
        private static double Erf(double x)
        {
            // Polynomial of the approximation in t = 1 / (1 + p x).
            double t = 1.0 / (1.0 + 0.3275911 * x);
            double poly = ((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592;
            return 1.0 - poly * t * Math.Exp(-x * x);
        }
    }
}

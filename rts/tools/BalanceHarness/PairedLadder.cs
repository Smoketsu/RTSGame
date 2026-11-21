// rts/tools/BalanceHarness/PairedLadder.cs
// The paired ladder's reading of game records (spec: rts/docs/specs/balance_measurement.md M.1.3, M.1.5, M.1.6 and M.2): turns the two games of each pair into one outcome (candidate won both = W,
// baseline won both = L, one each = S "concordant", any draw or undecided game = incomplete), counts distinct complete pairs by a pair signature, builds the "## Pairs" rows (per cell, mirror, cross, all),
// computes the pairing kind of a profile pair (full or seed), the verdict and the PAIRED stdout line; also the EFFECT line of an unpaired ladder. Every verdict is integer arithmetic (PairedStats).
// The candidate's slot is read from the record (CandidateSlot), never derived from the game index. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Ai;
using Rts.Sim.Core;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The outcome of one pair of ladder games. Immutable.</summary>
    public sealed class PairOutcome
    {
        /// <summary>Class: the candidate won both games.</summary>
        public const char CandidateBoth = 'W';

        /// <summary>Class: the baseline won both games.</summary>
        public const char BaselineBoth = 'L';

        /// <summary>Class: one win each (the same slot won both): concordant.</summary>
        public const char Concordant = 'S';

        /// <summary>Map ID of the cell.</summary>
        public string Map { get; }

        /// <summary>Matchup ID of the cell.</summary>
        public string Matchup { get; }

        /// <summary>Pair index in the cell. Unit: pairs. Range: 0 upward.</summary>
        public int Pair { get; }

        /// <summary>True when both slots play the same faction (a mirror cell).</summary>
        public bool Mirror { get; }

        /// <summary>True when both games have a winner (no draw, no undecided game).</summary>
        public bool Complete { get; }

        /// <summary>One of <see cref="CandidateBoth"/>, <see cref="BaselineBoth"/>, <see cref="Concordant"/> for a complete pair; a space for an incomplete one.</summary>
        public char Class { get; }

        /// <summary>FNV-1a over the play signature of the candidate-in-slot-0 game, then the other game's; equal values are the same experiment. Range: any ulong.</summary>
        public ulong Signature { get; }

        /// <summary>True when opening0 and opening1 are equal in both games of the pair.</summary>
        public bool OpeningsHeld { get; }

        /// <summary>Bundles the values.</summary>
        public PairOutcome(string map, string matchup, int pair, bool mirror, bool complete, char cls, ulong signature, bool openingsHeld)
        {
            Map = map;
            Matchup = matchup;
            Pair = pair;
            Mirror = mirror;
            Complete = complete;
            Class = cls;
            Signature = signature;
            OpeningsHeld = openingsHeld;
        }
    }

    /// <summary>One row of the "## Pairs" table with its statistics.</summary>
    public sealed class PairRow
    {
        /// <summary>Row label: "map / matchup", "mirror", "cross" or "all".</summary>
        public string Label { get; }

        /// <summary>Pairs played. Unit: pairs. Range: 0 upward.</summary>
        public int Pairs { get; set; }

        /// <summary>Complete pairs (plain count). Unit: pairs. Range: 0 to Pairs.</summary>
        public int Complete { get; set; }

        /// <summary>Complete pairs whose openings were held (plain count). Unit: pairs. Range: 0 to Complete.</summary>
        public int OpeningsHeld { get; set; }

        /// <summary>Plain concordant, candidate-both and baseline-both counts over all complete pairs. Unit: pairs.</summary>
        public int PlainS { get; set; }

        /// <summary>See <see cref="PlainS"/>.</summary>
        public int PlainW { get; set; }

        /// <summary>See <see cref="PlainS"/>.</summary>
        public int PlainL { get; set; }

        /// <summary>Distinct complete pairs (n). Unit: pairs. Range: 0 to Complete.</summary>
        public int N { get; set; }

        /// <summary>Distinct concordant pairs. Unit: pairs.</summary>
        public int S { get; set; }

        /// <summary>Distinct candidate-both pairs. Unit: pairs.</summary>
        public int W { get; set; }

        /// <summary>Distinct baseline-both pairs. Unit: pairs.</summary>
        public int L { get; set; }

        /// <summary>Distinct discordant pairs, W + L. Unit: pairs.</summary>
        public int D => W + L;

        /// <summary>The sign test of W against L at effect_alpha.</summary>
        public SignTestResult Test { get; set; } = new SignTestResult(0, 0, 0, 10000, false);

        /// <summary>Clopper-Pearson interval of W / D on the grid of 1/1000 (lower, upper). Unit: thousandths.</summary>
        public int[] Grid { get; set; } = { 0, 1000 };

        /// <summary>The candidate's advantage in tenths of a point of game win rate. Unit: tenths of a point.</summary>
        public int AdvantageTenths { get; set; }

        /// <summary>The advantage interval (lower, upper) in tenths of a point.</summary>
        public int[] AdvantageInterval { get; set; } = { 0, 0 };

        /// <summary>Minimum detectable effect in tenths of a point, or -1 when n is 0. Unit: tenths of a point.</summary>
        public int Mde { get; set; }

        /// <summary>Creates an empty row.</summary>
        /// <param name="label">Row label.</param>
        public PairRow(string label)
        {
            Label = label;
        }
    }

    /// <summary>Pair outcomes, rows, pairing kinds, verdicts and the stdout lines of the paired and unpaired ladders.</summary>
    public static class PairedLadder
    {
        /// <summary>Pairing kind: both profiles draw the same opening, band steps and expansion offset from one stream.</summary>
        public const string Full = "full";

        /// <summary>Pairing kind: only the random streams are shared; each profile maps them through its own library.</summary>
        public const string Seed = "seed";

        /// <summary>The outcomes of the pairs of sorted paired records (BatchRunner.CompareGames order); throws <see cref="HarnessException"/> for a record without a pair.</summary>
        /// <param name="records">Paired ladder records sorted by (map, matchup, game).</param>
        public static List<PairOutcome> Outcomes(IReadOnlyList<GameRecord> records)
        {
            List<PairOutcome> outcomes = new List<PairOutcome>();
            for (int i = 0; i < records.Count; i++)
            {
                GameRecord first = records[i];
                if (first.Pair < 0)
                {
                    throw new HarnessException("a paired report needs paired records (record of " + first.Map + " " + first.Matchup + " game " + first.Game.ToString(CultureInfo.InvariantCulture) + " has no pair)");
                }
                // The other game of the pair, when it follows directly in sorted order.
                GameRecord? second = i + 1 < records.Count && records[i + 1].Map == first.Map && records[i + 1].Matchup == first.Matchup && records[i + 1].Pair == first.Pair ? records[i + 1] : null;
                bool mirror = first.Slot0Faction == first.Slot1Faction;
                if (second == null)
                {
                    outcomes.Add(new PairOutcome(first.Map, first.Matchup, first.Pair, mirror, false, ' ', 0UL, false));
                    continue;
                }
                i++;
                // The two games ordered with the candidate-in-slot-0 game first (for the signature).
                GameRecord slot0Game = first.CandidateSlot == 0 ? first : second;
                GameRecord slot1Game = first.CandidateSlot == 0 ? second : first;
                bool complete = HasWinner(first) && HasWinner(second);
                char cls = ' ';
                if (complete)
                {
                    // Games the candidate won (0, 1 or 2).
                    int wins = (Won(first) ? 1 : 0) + (Won(second) ? 1 : 0);
                    cls = wins == 2 ? PairOutcome.CandidateBoth : wins == 0 ? PairOutcome.BaselineBoth : PairOutcome.Concordant;
                }
                StateHasher hasher = new StateHasher();
                hasher.Write(slot0Game.PlaySignature);
                hasher.Write(slot1Game.PlaySignature);
                outcomes.Add(new PairOutcome(first.Map, first.Matchup, first.Pair, mirror, complete, cls, hasher.Hash, first.Opening0 == second.Opening0 && first.Opening1 == second.Opening1));
            }
            return outcomes;
        }

        /// <summary>True when the game has a winner (slot0 or slot1).</summary>
        private static bool HasWinner(GameRecord record)
        {
            return record.Result == GameRecord.Slot0 || record.Result == GameRecord.Slot1;
        }

        /// <summary>True when the candidate (in the record's CandidateSlot) won the game.</summary>
        private static bool Won(GameRecord record)
        {
            return (record.Result == GameRecord.Slot0 ? 0 : 1) == record.CandidateSlot;
        }

        /// <summary>One row from a group of pairs: plain counts, distinct counts (a signature counts once), the sign test, the interval, the advantage and the MDE.</summary>
        /// <param name="label">Row label.</param>
        /// <param name="outcomes">The pairs of the row, in record order.</param>
        /// <param name="settings">effect_alpha and the z-scores.</param>
        public static PairRow Row(string label, IEnumerable<PairOutcome> outcomes, AiEvalSettings settings)
        {
            PairRow row = new PairRow(label);
            // Signatures of the complete pairs counted so far.
            SortedSet<ulong> seen = new SortedSet<ulong>();
            foreach (PairOutcome pair in outcomes)
            {
                row.Pairs++;
                if (!pair.Complete)
                {
                    continue;
                }
                row.Complete++;
                row.OpeningsHeld += pair.OpeningsHeld ? 1 : 0;
                row.PlainS += pair.Class == PairOutcome.Concordant ? 1 : 0;
                row.PlainW += pair.Class == PairOutcome.CandidateBoth ? 1 : 0;
                row.PlainL += pair.Class == PairOutcome.BaselineBoth ? 1 : 0;
                if (!seen.Add(pair.Signature))
                {
                    continue;
                }
                row.N++;
                row.S += pair.Class == PairOutcome.Concordant ? 1 : 0;
                row.W += pair.Class == PairOutcome.CandidateBoth ? 1 : 0;
                row.L += pair.Class == PairOutcome.BaselineBoth ? 1 : 0;
            }
            row.Test = PairedStats.SignTest(row.W, row.L, settings.EffectAlphaPt);
            row.Grid = PairedStats.Interval(row.W, row.D, settings.EffectAlphaPt);
            row.AdvantageTenths = PairedStats.AdvantageTenths(row.W, row.L, row.N);
            row.AdvantageInterval = PairedStats.AdvantageInterval(row.Grid[0], row.Grid[1], row.D, row.N);
            row.Mde = PairedStats.MdePaired(row.N, row.D, PairedStats.Z(settings.EffectZAlpha, settings.EffectZPower), settings.EffectAlphaPt);
            return row;
        }

        /// <summary>The rows of the Pairs table: one per cell (ordinal order), then mirror (when present), cross (when present) and all.</summary>
        /// <param name="outcomes">Every pair, in sorted record order.</param>
        /// <param name="settings">The effect settings.</param>
        public static List<PairRow> Rows(IReadOnlyList<PairOutcome> outcomes, AiEvalSettings settings)
        {
            // Pairs by cell key, in ordinal key order.
            SortedDictionary<string, List<PairOutcome>> cells = new SortedDictionary<string, List<PairOutcome>>(StringComparer.Ordinal);
            List<PairOutcome> mirror = new List<PairOutcome>();
            List<PairOutcome> cross = new List<PairOutcome>();
            foreach (PairOutcome pair in outcomes)
            {
                string key = pair.Map + " / " + pair.Matchup;
                if (!cells.TryGetValue(key, out List<PairOutcome>? list))
                {
                    list = new List<PairOutcome>();
                    cells.Add(key, list);
                }
                list.Add(pair);
                (pair.Mirror ? mirror : cross).Add(pair);
            }
            List<PairRow> rows = new List<PairRow>();
            foreach (KeyValuePair<string, List<PairOutcome>> cell in cells)
            {
                rows.Add(Row(cell.Key, cell.Value, settings));
            }
            if (mirror.Count > 0)
            {
                rows.Add(Row("mirror", mirror, settings));
            }
            if (cross.Count > 0)
            {
                rows.Add(Row("cross", cross, settings));
            }
            rows.Add(Row("all", outcomes, settings));
            return rows;
        }

        /// <summary>The verdict (M.2): INCOMPLETE when complete pairs are fewer than paired_min_complete_fraction of all pairs, else PASS, FAIL or NO_MEASURABLE_EFFECT from the sign test of the "all" row.</summary>
        /// <param name="all">The "all" row.</param>
        /// <param name="settings">paired_min_complete_fraction.</param>
        public static string Verdict(PairRow all, AiEvalSettings settings)
        {
            if ((long)all.Complete * 10000 < (long)settings.PairedMinCompletePt * all.Pairs)
            {
                return PairedStats.Incomplete;
            }
            return PairedStats.Verdict(all.Test);
        }

        /// <summary>
        /// The pairing kind of a profile pair for one faction (M.1.3): full when both plans have the same openings (IDs, weights, file order), every opening and the plan composition have the same set of
        /// unit IDs with weight above 0 in both, and the profiles have equal composition_jitter_fraction, expand_jitter_seconds and attack_advantage_jitter; seed otherwise.
        /// </summary>
        /// <param name="a">Baseline profile.</param>
        /// <param name="b">Candidate profile.</param>
        /// <param name="faction">Faction ID; both profiles need a plan for it.</param>
        public static string PairingKind(AiProfile a, AiProfile b, string faction)
        {
            if (a.CompositionJitterFraction != b.CompositionJitterFraction || a.ExpandJitterSeconds != b.ExpandJitterSeconds || a.AttackAdvantageJitter != b.AttackAdvantageJitter)
            {
                return Seed;
            }
            AiFactionPlan planA = a.RequireFaction(faction);
            AiFactionPlan planB = b.RequireFaction(faction);
            if (planA.Openings.Count != planB.Openings.Count || !SameActiveUnits(planA.Composition, planB.Composition))
            {
                return Seed;
            }
            for (int i = 0; i < planA.Openings.Count; i++)
            {
                AiOpening x = planA.Openings[i];
                AiOpening y = planB.Openings[i];
                if (x.Id != y.Id || x.Weight != y.Weight || !SameActiveUnits(x.Composition, y.Composition))
                {
                    return Seed;
                }
            }
            return Full;
        }

        /// <summary>True when the two compositions have the same set of unit IDs with weight above 0.</summary>
        private static bool SameActiveUnits(IReadOnlyDictionary<string, int> x, IReadOnlyDictionary<string, int> y)
        {
            // Unit IDs with weight above 0 on each side, in ordinal order.
            SortedSet<string> setX = new SortedSet<string>(StringComparer.Ordinal);
            SortedSet<string> setY = new SortedSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in x)
            {
                if (pair.Value > 0)
                {
                    setX.Add(pair.Key);
                }
            }
            foreach (KeyValuePair<string, int> pair in y)
            {
                if (pair.Value > 0)
                {
                    setY.Add(pair.Key);
                }
            }
            return setX.SetEquals(setY);
        }

        /// <summary>"faction:kind,faction:kind" for the factions in ordinal order (the last field of the PAIRED line).</summary>
        /// <param name="a">Baseline profile.</param>
        /// <param name="b">Candidate profile.</param>
        /// <param name="factions">Faction IDs of the selected matchups.</param>
        public static string PairingText(AiProfile a, AiProfile b, IEnumerable<string> factions)
        {
            SortedSet<string> sorted = new SortedSet<string>(factions, StringComparer.Ordinal);
            List<string> parts = new List<string>();
            foreach (string faction in sorted)
            {
                parts.Add(faction + ":" + PairingKind(a, b, faction));
            }
            return string.Join(",", parts);
        }

        /// <summary>The last stdout line of a paired ladder (M.1.6).</summary>
        /// <param name="candidate">Candidate profile ID.</param>
        /// <param name="baseline">Baseline profile ID.</param>
        /// <param name="all">The "all" row.</param>
        /// <param name="pairing">The text from <see cref="PairingText"/>.</param>
        /// <param name="verdict">The verdict.</param>
        public static string Line(string candidate, string baseline, PairRow all, string pairing, string verdict)
        {
            return "PAIRED candidate=" + candidate + " baseline=" + baseline + " pairs=" + all.Pairs.ToString(CultureInfo.InvariantCulture) + " complete=" + all.Complete.ToString(CultureInfo.InvariantCulture)
                + " distinct=" + all.N.ToString(CultureInfo.InvariantCulture) + " concordant=" + all.S.ToString(CultureInfo.InvariantCulture) + " candidate_both=" + all.W.ToString(CultureInfo.InvariantCulture)
                + " baseline_both=" + all.L.ToString(CultureInfo.InvariantCulture) + " p=" + PairedStats.P4(all.Test.PPt) + " advantage=" + PairedStats.Signed(all.AdvantageTenths)
                + " interval=[" + PairedStats.Signed(all.AdvantageInterval[0]) + "," + PairedStats.Signed(all.AdvantageInterval[1]) + "] mde=" + PairedStats.Plain(all.Mde) + " pairing=" + pairing + " verdict=" + verdict;
        }

        /// <summary>The EFFECT line of an unpaired ladder (M.2): n distinct games with a winner, w distinct candidate wins, the sign test with D = n and W = w, the advantage, its interval and the MDE.</summary>
        /// <param name="candidate">Candidate profile ID.</param>
        /// <param name="baseline">Baseline profile ID.</param>
        /// <param name="tally">Tally of every game.</param>
        /// <param name="settings">The effect settings.</param>
        public static string EffectLine(string candidate, string baseline, LadderTally tally, AiEvalSettings settings)
        {
            // Distinct games with a winner, and distinct candidate wins.
            int n = tally.DistinctWinners;
            int w = tally.DistinctWins;
            SignTestResult test = PairedStats.SignTest(w, n - w, settings.EffectAlphaPt);
            int[] grid = PairedStats.Interval(w, n, settings.EffectAlphaPt);
            int[] interval = PairedStats.UnpairedInterval(grid[0], grid[1]);
            return "EFFECT candidate=" + candidate + " baseline=" + baseline + " games=" + n.ToString(CultureInfo.InvariantCulture) + " wins=" + w.ToString(CultureInfo.InvariantCulture)
                + " p=" + PairedStats.P4(test.PPt) + " advantage=" + PairedStats.Signed(PairedStats.UnpairedAdvantageTenths(w, n)) + " interval=[" + PairedStats.Signed(interval[0]) + "," + PairedStats.Signed(interval[1])
                + "] mde=" + PairedStats.Plain(PairedStats.MdeUnpaired(n, PairedStats.Z(settings.EffectZAlpha, settings.EffectZPower))) + " verdict=" + PairedStats.Verdict(test);
        }

        /// <summary>Appends the "## Pairs" section of ladder.md.</summary>
        /// <param name="sb">The report being built.</param>
        /// <param name="rows">The rows from <see cref="Rows"/>.</param>
        public static void AppendPairs(StringBuilder sb, IReadOnlyList<PairRow> rows)
        {
            sb.Append("\n## Pairs\n\n");
            sb.Append("Counts S, W, L and n are over distinct complete pairs (pair signature); the plain counts over all complete pairs are in the next three columns. r = (W + L) / n. ")
                .Append("The advantage is in points of game win rate with the exact Clopper-Pearson interval on the discordant share; MDE is the smallest effect that would have been detected with the stated power. The verdict uses the `all` row only.\n\n");
            sb.Append("| row | pairs | complete | distinct complete (n) | concordant (S) | candidate both (W) | baseline both (L) | plain S / W / L | r | openings held | p | advantage [interval] | MDE |\n");
            sb.Append("| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | ---: |\n");
            foreach (PairRow row in rows)
            {
                sb.Append("| ").Append(row.Label).Append(" | ").Append(row.Pairs.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.Complete.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(row.N.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.S.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.W.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(row.L.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.PlainS.ToString(CultureInfo.InvariantCulture)).Append(" / ").Append(row.PlainW.ToString(CultureInfo.InvariantCulture))
                    .Append(" / ").Append(row.PlainL.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(PairedStats.Share3(row.D, row.N)).Append(" | ").Append(row.OpeningsHeld.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(PairedStats.P4(row.Test.PPt)).Append(" | ").Append(PairedStats.Signed(row.AdvantageTenths)).Append(" [").Append(PairedStats.Signed(row.AdvantageInterval[0])).Append(", ")
                    .Append(PairedStats.Signed(row.AdvantageInterval[1])).Append("] | ").Append(PairedStats.Plain(row.Mde)).Append(" |\n");
            }
        }
    }
}

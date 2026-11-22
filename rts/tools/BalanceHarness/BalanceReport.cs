// rts/tools/BalanceHarness/BalanceReport.cs
// The standing balance report (spec: rts/docs/specs/balance_measurement.md M.5.2 and M.5.5): everything --balance computes from game records with summaries, as pure functions of records, data and settings so a
// report can be re-rendered from balance.jsonl byte for byte. The faction matrix and the opening matrices (wins over decided games, Wilson intervals over distinct games), unit use and mechanic use shares over the
// slot-games of the owning faction, the unspent resources at the bank times, and the four trust checks of P-006 section 6. Every check verdict is integer arithmetic. Tool-side only (double only for the Wilson
// interval, as in HarnessStats). Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Wins over decided games of one matrix cell, plain and by distinct play signature.</summary>
    public sealed class WinCount
    {
        /// <summary>Decided games (a winner or a draw). Unit: games. Range: 0 upward.</summary>
        public int Decided { get; private set; }

        /// <summary>Games the row side won. Unit: games. Range: 0 to Decided.</summary>
        public int Wins { get; private set; }

        /// <summary>Play signatures of the decided games.</summary>
        private readonly SortedSet<ulong> _signatures = new SortedSet<ulong>();

        /// <summary>Play signatures of the games the row side won.</summary>
        private readonly SortedSet<ulong> _winSignatures = new SortedSet<ulong>();

        /// <summary>Distinct decided games (one per play signature). Unit: games. Range: 0 to Decided.</summary>
        public int Distinct => _signatures.Count;

        /// <summary>Distinct games the row side won. Unit: games. Range: 0 to Distinct.</summary>
        public int DistinctWins => _winSignatures.Count;

        /// <summary>Adds one game; an undecided game is ignored.</summary>
        /// <param name="record">The game.</param>
        /// <param name="won">True when the row side won it.</param>
        public void Add(GameRecord record, bool won)
        {
            if (record.Result == GameRecord.Undecided)
            {
                return;
            }
            Decided++;
            _signatures.Add(record.PlaySignature);
            if (won)
            {
                Wins++;
                _winSignatures.Add(record.PlaySignature);
            }
        }

        /// <summary>The Wilson interval of the distinct wins over the distinct games (draws count as losses, as in the ladder).</summary>
        /// <param name="z">Wilson z-score. Range: 1 to 4.</param>
        public WinRate Wilson(double z)
        {
            return HarnessStats.Wilson(DistinctWins, 0, Distinct - DistinctWins, z);
        }

        /// <summary>"wins/decided [low, high] (distinct n)" for a table cell; "-" without a game.</summary>
        /// <param name="z">Wilson z-score. Range: 1 to 4.</param>
        public string Text(double z)
        {
            if (Decided == 0)
            {
                return "-";
            }
            WinRate rate = Wilson(z);
            return Wins.ToString(CultureInfo.InvariantCulture) + "/" + Decided.ToString(CultureInfo.InvariantCulture) + " [" + HarnessStats.Percent(rate.Low) + ", " + HarnessStats.Percent(rate.High) + "] (distinct "
                + Distinct.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }

    /// <summary>One line of the use tables: a unit or a mechanic of one faction.</summary>
    public sealed class UseRow
    {
        /// <summary>"unit", "research" or "mechanic".</summary>
        public string Kind { get; }

        /// <summary>Faction ID.</summary>
        public string Faction { get; }

        /// <summary>Definition ID or mechanic ID.</summary>
        public string Id { get; }

        /// <summary>Slot-games that used it (trained or completed at least once; mechanic count at least 1). Unit: slot-games. Range: 0 to SlotGames.</summary>
        public int Used { get; set; }

        /// <summary>Slot-games of the faction the row applies to. Unit: slot-games. Range: 0 upward.</summary>
        public int SlotGames { get; set; }

        /// <summary>Bundles the identity.</summary>
        public UseRow(string kind, string faction, string id)
        {
            Kind = kind;
            Faction = faction;
            Id = id;
        }

        /// <summary>Used / SlotGames as "0.000".</summary>
        public string ShareText => PredictEvaluator.Ratio3(Used, SlotGames);

        /// <summary>True when Used / SlotGames is below <paramref name="minPt"/> parts per 10 000 (integers).</summary>
        /// <param name="minPt">Least share. Unit: parts per 10 000. Range: 0 to 10000.</param>
        public bool Below(int minPt)
        {
            return (long)Used * 10000 < (long)minPt * SlotGames;
        }
    }

    /// <summary>The lower median of the bank of one faction at one bank time.</summary>
    public sealed class UnspentRow
    {
        /// <summary>Faction ID.</summary>
        public string Faction { get; }

        /// <summary>Bank time. Unit: seconds. Range: 1 to 7200.</summary>
        public int Seconds { get; }

        /// <summary>Slot-games still running at that time. Unit: slot-games. Range: 0 upward.</summary>
        public int N { get; }

        /// <summary>Lower median of the common bank, or 0 with no slot-game. Unit: resource units.</summary>
        public int MedianCommon { get; }

        /// <summary>Lower median of the rare bank, or 0 with no slot-game. Unit: resource units.</summary>
        public int MedianRare { get; }

        /// <summary>Bundles the values.</summary>
        public UnspentRow(string faction, int seconds, int n, int medianCommon, int medianRare)
        {
            Faction = faction;
            Seconds = seconds;
            N = n;
            MedianCommon = medianCommon;
            MedianRare = medianRare;
        }
    }

    /// <summary>One trust check of the report.</summary>
    public sealed class BalanceCheck
    {
        /// <summary>Status: the check holds.</summary>
        public const string Pass = "PASS";

        /// <summary>Status: the check does not hold.</summary>
        public const string Fail = "FAIL";

        /// <summary>Status: the data the check needs was not measured.</summary>
        public const string NotMeasured = "NOT_MEASURED";

        /// <summary>Check name, "balance_use" and so on.</summary>
        public string Name { get; }

        /// <summary>PASS, FAIL or NOT_MEASURED.</summary>
        public string Status { get; }

        /// <summary>Values and bars in one line of text.</summary>
        public string Detail { get; }

        /// <summary>Bundles the values.</summary>
        public BalanceCheck(string name, string status, string detail)
        {
            Name = name;
            Status = status;
            Detail = detail;
        }

        /// <summary>The stdout line: "CHECK &lt;name&gt; &lt;status&gt; &lt;detail&gt;".</summary>
        public string Line => "CHECK " + Name + " " + Status + " " + Detail;
    }

    /// <summary>Predictor accuracy over decided fights, from package C.</summary>
    public sealed class PredictorResult
    {
        /// <summary>Decided fights. Unit: fights. Range: 0 upward.</summary>
        public int Decided { get; }

        /// <summary>Decided fights the predictor named the winner of. Unit: fights. Range: 0 to Decided.</summary>
        public int Correct { get; }

        /// <summary>Bundles the values.</summary>
        public PredictorResult(int decided, int correct)
        {
            Decided = decided;
            Correct = correct;
        }
    }

    /// <summary>The computations and the text of the balance report.</summary>
    public static class BalanceReport
    {
        /// <summary>Mechanics the report cannot measure from events or state, with the reason (M.5.3); listed, never blocking (Q-7).</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Unmeasurable = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("focus fire, kiting and retreats", "AI micro; only its commands show it, no event or state does"),
            new KeyValuePair<string, string>("regeneration and Windstorm range", "passive or global, not a choice"),
            new KeyValuePair<string, string>("tech choices as such", "covered by the unit use table: each tech structure is a definition"),
        };

        /// <summary>The factions of the records, ordinal order.</summary>
        public static List<string> Factions(IReadOnlyList<GameRecord> records)
        {
            SortedSet<string> set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (GameRecord record in records)
            {
                set.Add(record.Slot0Faction);
                set.Add(record.Slot1Faction);
            }
            return new List<string>(set);
        }

        /// <summary>The faction matrix cell (F, G): F's wins over decided games between F and G (F not G). For F equal to G, the "seat" cell: slot 0's wins over decided games of the F mirror.</summary>
        /// <param name="records">Game records.</param>
        /// <param name="f">Row faction ID.</param>
        /// <param name="g">Column faction ID.</param>
        public static WinCount FactionCell(IReadOnlyList<GameRecord> records, string f, string g)
        {
            WinCount count = new WinCount();
            foreach (GameRecord record in records)
            {
                if (f == g)
                {
                    if (record.Slot0Faction == f && record.Slot1Faction == f)
                    {
                        count.Add(record, record.Result == GameRecord.Slot0);
                    }
                    continue;
                }
                if (record.Slot0Faction == f && record.Slot1Faction == g)
                {
                    count.Add(record, record.Result == GameRecord.Slot0);
                }
                else if (record.Slot1Faction == f && record.Slot0Faction == g)
                {
                    count.Add(record, record.Result == GameRecord.Slot1);
                }
            }
            return count;
        }

        /// <summary>The opening matrix cell: wins of a slot of faction F playing opening X over decided games against a slot of faction G playing opening Y; a mirror game with X equal to Y counts once and has no winner.</summary>
        /// <param name="records">Game records.</param>
        /// <param name="f">Row faction ID.</param>
        /// <param name="x">Row opening ID.</param>
        /// <param name="g">Column faction ID.</param>
        /// <param name="y">Column opening ID.</param>
        public static WinCount OpeningCell(IReadOnlyList<GameRecord> records, string f, string x, string g, string y)
        {
            WinCount count = new WinCount();
            foreach (GameRecord record in records)
            {
                // Slot number playing the row side. Range: 0 or 1.
                for (int s = 0; s < 2; s++)
                {
                    string rowFaction = s == 0 ? record.Slot0Faction : record.Slot1Faction;
                    string rowOpening = s == 0 ? record.Opening0 : record.Opening1;
                    string colFaction = s == 0 ? record.Slot1Faction : record.Slot0Faction;
                    string colOpening = s == 0 ? record.Opening1 : record.Opening0;
                    if (rowFaction != f || rowOpening != x || colFaction != g || colOpening != y || (f == g && x == y && s == 1))
                    {
                        continue;
                    }
                    count.Add(record, f == g && x == y ? false : record.Result == (s == 0 ? GameRecord.Slot0 : GameRecord.Slot1));
                }
            }
            return count;
        }

        /// <summary>Opening IDs seen for a faction, ordinal order.</summary>
        public static List<string> OpeningsOf(IReadOnlyList<GameRecord> records, string faction)
        {
            SortedSet<string> set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (GameRecord record in records)
            {
                if (record.Slot0Faction == faction && record.Opening0.Length > 0)
                {
                    set.Add(record.Opening0);
                }
                if (record.Slot1Faction == faction && record.Opening1.Length > 0)
                {
                    set.Add(record.Opening1);
                }
            }
            return new List<string>(set);
        }

        /// <summary>Faction of the slot, its summary and the game, for every slot-game that has a summary.</summary>
        private static List<KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>>> SlotGames(IReadOnlyList<GameRecord> records)
        {
            List<KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>>> list = new List<KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>>>();
            foreach (GameRecord record in records)
            {
                if (record.Summary0 != null)
                {
                    list.Add(new KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>>(record.Slot0Faction, new KeyValuePair<GameSummary, GameRecord>(record.Summary0, record)));
                }
                if (record.Summary1 != null)
                {
                    list.Add(new KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>>(record.Slot1Faction, new KeyValuePair<GameSummary, GameRecord>(record.Summary1, record)));
                }
            }
            return list;
        }

        /// <summary>The unit use rows: every definition of a faction that is trainable (in some Trains list of that faction) or buildable (footprint above 0, no HQ), in ordinal ID order, with the share of that faction's slot-games that trained or completed it.</summary>
        /// <param name="records">Records with summaries.</param>
        /// <param name="data">The data set (definitions).</param>
        public static List<UseRow> UnitUse(IReadOnlyList<GameRecord> records, DataSet data)
        {
            List<UseRow> rows = new List<UseRow>();
            List<KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>>> slotGames = SlotGames(records);
            foreach (string faction in Factions(records))
            {
                // IDs trainable by a structure or unit of this faction.
                SortedSet<string> trainable = new SortedSet<string>(StringComparer.Ordinal);
                foreach (UnitDefinition definition in data.Content.Units.Values)
                {
                    if (definition.FactionId == faction)
                    {
                        foreach (string id in definition.Economy.Trains)
                        {
                            trainable.Add(id);
                        }
                    }
                }
                // This faction's definitions that qualify, in ordinal ID order.
                SortedDictionary<string, UnitDefinition> wanted = new SortedDictionary<string, UnitDefinition>(StringComparer.Ordinal);
                foreach (UnitDefinition definition in data.Content.Units.Values)
                {
                    if (definition.FactionId == faction && (trainable.Contains(definition.Id) || (definition.Economy.FootprintTiles > 0 && definition.Hq == null)))
                    {
                        wanted[definition.Id] = definition;
                    }
                }
                // Slot-games of this faction.
                int total = 0;
                foreach (KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>> slotGame in slotGames)
                {
                    total += slotGame.Key == faction ? 1 : 0;
                }
                foreach (string id in wanted.Keys)
                {
                    UseRow row = new UseRow("unit", faction, id) { SlotGames = total };
                    foreach (KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>> slotGame in slotGames)
                    {
                        GameSummary summary = slotGame.Value.Key;
                        if (slotGame.Key == faction && ((summary.Trained.TryGetValue(id, out int trained) && trained >= 1) || (summary.Completed.TryGetValue(id, out int completed) && completed >= 1)))
                        {
                            row.Used++;
                        }
                    }
                    rows.Add(row);
                }
                // Research items of this faction, in ordinal ID order (research_and_unlocks.md R2.3); used when completed at least once.
                foreach (ResearchDefinition research in data.Content.Researches.Values)
                {
                    if (research.FactionId != faction)
                    {
                        continue;
                    }
                    UseRow row = new UseRow("research", faction, research.Id) { SlotGames = total };
                    foreach (KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>> slotGame in slotGames)
                    {
                        if (slotGame.Key == faction && slotGame.Value.Key.Completed.TryGetValue(research.Id, out int done) && done >= 1)
                        {
                            row.Used++;
                        }
                    }
                    rows.Add(row);
                }
            }
            return rows;
        }

        /// <summary>The mechanic use rows: for every detector and faction with at least one applicable slot-game, the share of those slot-games with a count of at least 1.</summary>
        /// <param name="records">Records with summaries.</param>
        /// <param name="data">The data set (definitions and maps).</param>
        public static List<UseRow> MechanicUse(IReadOnlyList<GameRecord> records, DataSet data)
        {
            List<UseRow> rows = new List<UseRow>();
            List<KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>>> slotGames = SlotGames(records);
            foreach (MechanicDetector detector in MechanicDetectors.All)
            {
                foreach (string faction in Factions(records))
                {
                    UseRow row = new UseRow("mechanic", faction, detector.Id);
                    foreach (KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>> slotGame in slotGames)
                    {
                        if (slotGame.Key != faction || !detector.Applicable(data.Content, faction, data.Maps.TryGetValue(slotGame.Value.Value.Map, out MapDefinition? map) && map.Core != null))
                        {
                            continue;
                        }
                        row.SlotGames++;
                        row.Used += slotGame.Value.Key.Mechanics.TryGetValue(detector.Id, out int uses) && uses >= 1 ? 1 : 0;
                    }
                    if (row.SlotGames > 0)
                    {
                        rows.Add(row);
                    }
                }
            }
            return rows;
        }

        /// <summary>The lower median (element (n - 1) / 2 of the ascending list) of a non-empty list of integers.</summary>
        private static int LowerMedian(List<int> values)
        {
            values.Sort();
            return values[(values.Count - 1) / 2];
        }

        /// <summary>The unspent rows: for every faction and bank time, the lower medians of the bank over the slot-games still running at that time.</summary>
        /// <param name="records">Records with summaries.</param>
        /// <param name="bankSeconds">The bank times of the summaries. Unit: seconds.</param>
        public static List<UnspentRow> Unspent(IReadOnlyList<GameRecord> records, IReadOnlyList<int> bankSeconds)
        {
            List<UnspentRow> rows = new List<UnspentRow>();
            List<KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>>> slotGames = SlotGames(records);
            foreach (string faction in Factions(records))
            {
                for (int i = 0; i < bankSeconds.Count; i++)
                {
                    List<int> common = new List<int>();
                    List<int> rare = new List<int>();
                    foreach (KeyValuePair<string, KeyValuePair<GameSummary, GameRecord>> slotGame in slotGames)
                    {
                        if (slotGame.Key == faction && i < slotGame.Value.Key.Bank.Count && slotGame.Value.Key.Bank[i] != null)
                        {
                            common.Add(slotGame.Value.Key.Bank[i]![0]);
                            rare.Add(slotGame.Value.Key.Bank[i]![1]);
                        }
                    }
                    rows.Add(new UnspentRow(faction, bankSeconds[i], common.Count, common.Count > 0 ? LowerMedian(common) : 0, rare.Count > 0 ? LowerMedian(rare) : 0));
                }
            }
            return rows;
        }

        /// <summary>The four trust checks of M.5.5, in order; every comparison is integer arithmetic.</summary>
        /// <param name="records">Records with summaries.</param>
        /// <param name="data">The data set.</param>
        /// <param name="settings">The bars.</param>
        /// <param name="bankSeconds">The bank times of the summaries. Unit: seconds.</param>
        /// <param name="predictor">Predictor accuracy, or null when package C's evaluation was not available.</param>
        public static List<BalanceCheck> Checks(IReadOnlyList<GameRecord> records, DataSet data, BalanceSettings settings, IReadOnlyList<int> bankSeconds, PredictorResult? predictor)
        {
            List<BalanceCheck> checks = new List<BalanceCheck>();
            // 1. use.
            List<UseRow> use = UnitUse(records, data);
            use.AddRange(MechanicUse(records, data));
            // Items below the bar and the items judged (rows with at least one slot-game).
            List<string> below = new List<string>();
            int judged = 0;
            foreach (UseRow row in use)
            {
                if (row.SlotGames == 0)
                {
                    continue;
                }
                judged++;
                if (row.Below(settings.MinUseSharePt))
                {
                    below.Add(row.Faction + "/" + row.Id + " " + row.ShareText);
                }
            }
            string bar = PredictEvaluator.Ratio3(settings.MinUseSharePt, 10000);
            checks.Add(new BalanceCheck("balance_use", below.Count == 0 ? BalanceCheck.Pass : BalanceCheck.Fail, below.Count == 0
                ? "items=" + judged.ToString(CultureInfo.InvariantCulture) + " all at least " + bar
                : below.Count.ToString(CultureInfo.InvariantCulture) + " of " + judged.ToString(CultureInfo.InvariantCulture) + " items below " + bar + ": " + string.Join(", ", below)));
            // 2. predictor.
            if (predictor == null || predictor.Decided == 0)
            {
                checks.Add(new BalanceCheck("balance_predictor", BalanceCheck.NotMeasured, "no fight data (run without --from, or give --fights-from)"));
            }
            else
            {
                checks.Add(new BalanceCheck("balance_predictor", (long)predictor.Correct * 10000 >= (long)settings.PredictorMinAccuracyPt * predictor.Decided ? BalanceCheck.Pass : BalanceCheck.Fail,
                    "accuracy=" + PredictEvaluator.Ratio3(predictor.Correct, predictor.Decided) + " correct=" + predictor.Correct.ToString(CultureInfo.InvariantCulture) + " decided="
                    + predictor.Decided.ToString(CultureInfo.InvariantCulture) + " bar=" + PredictEvaluator.Ratio3(settings.PredictorMinAccuracyPt, 10000)));
            }
            // 3. unspent.
            List<string> over = new List<string>();
            foreach (UnspentRow row in Unspent(records, bankSeconds))
            {
                if (row.N > 0 && (row.MedianCommon > settings.MaxMedianUnspentCommon || row.MedianRare > settings.MaxMedianUnspentRare))
                {
                    over.Add(row.Faction + "@" + row.Seconds.ToString(CultureInfo.InvariantCulture) + "s common=" + row.MedianCommon.ToString(CultureInfo.InvariantCulture) + " rare=" + row.MedianRare.ToString(CultureInfo.InvariantCulture));
                }
            }
            checks.Add(new BalanceCheck("balance_unspent", over.Count == 0 ? BalanceCheck.Pass : BalanceCheck.Fail, (over.Count == 0 ? "all medians within " : "over ") + "bars common="
                + settings.MaxMedianUnspentCommon.ToString(CultureInfo.InvariantCulture) + " rare=" + settings.MaxMedianUnspentRare.ToString(CultureInfo.InvariantCulture) + (over.Count == 0 ? string.Empty : ": " + string.Join(", ", over))));
            // 4. openings.
            List<string> extreme = new List<string>();
            int openingsJudged = 0;
            foreach (OpeningRow row in LadderRunner.OpeningRows(records))
            {
                int n = row.MirrorSignatures.Count;
                int w = row.MirrorWinSignatures.Count;
                if (n < settings.OpeningMinGames)
                {
                    continue;
                }
                openingsJudged++;
                if ((long)w * 10000 < (long)settings.OpeningMirrorMinPt * n || (long)w * 10000 > (long)settings.OpeningMirrorMaxPt * n)
                {
                    extreme.Add(row.Faction + "/" + row.Opening + " " + w.ToString(CultureInfo.InvariantCulture) + "/" + n.ToString(CultureInfo.InvariantCulture));
                }
            }
            checks.Add(new BalanceCheck("balance_openings", extreme.Count == 0 ? BalanceCheck.Pass : BalanceCheck.Fail, (extreme.Count == 0 ? "openings judged=" + openingsJudged.ToString(CultureInfo.InvariantCulture) + " all within " : "outside ")
                + PredictEvaluator.Ratio3(settings.OpeningMirrorMinPt, 10000) + " to " + PredictEvaluator.Ratio3(settings.OpeningMirrorMaxPt, 10000) + " of distinct mirror games (at least "
                + settings.OpeningMinGames.ToString(CultureInfo.InvariantCulture) + ")" + (extreme.Count == 0 ? string.Empty : ": " + string.Join(", ", extreme))));
            return checks;
        }

        /// <summary>Builds balance.md: the seven sections of M.5.2 in order.</summary>
        /// <param name="records">Records with summaries, sorted.</param>
        /// <param name="data">The data set.</param>
        /// <param name="settings">The bars.</param>
        /// <param name="meta">Profile, base seed, games per cell and cap the run used.</param>
        /// <param name="bankSeconds">The bank times of the summaries. Unit: seconds.</param>
        /// <param name="checks">The checks from <see cref="Checks"/>.</param>
        public static string Build(IReadOnlyList<GameRecord> records, DataSet data, BalanceSettings settings, BalanceMeta meta, IReadOnlyList<int> bankSeconds, IReadOnlyList<BalanceCheck> checks)
        {
            // Wilson z-score (tuning/harness_batch.json confidence_z). Range: 1 to 4.
            double z = HarnessStats.ToDouble(data.Tuning.HarnessBatch.ConfidenceZ);
            // Counts for the settings bullets.
            LadderTally all = new LadderTally();
            foreach (GameRecord record in records)
            {
                all.Add(record);
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<!-- balance.md: written by the balance harness --balance (rts/tools/BalanceHarness/BalanceRunner.cs); not a source file. -->\n");
            sb.Append("# Balance report: ").Append(meta.Profile).Append("\n\n");
            sb.Append("- Profile ").Append(meta.Profile).Append(" plays both slots; base seed ").Append(meta.BaseSeed.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(meta.GamesPerCell.ToString(CultureInfo.InvariantCulture))
                .Append(" games per cell, game cap ").Append(meta.MaxGameSeconds.ToString(CultureInfo.InvariantCulture)).Append(" s; factions swap on odd games.\n");
            sb.Append("- Content digest ").Append(records.Count > 0 ? records[0].ContentDigest.ToString("x16", CultureInfo.InvariantCulture) : "-").Append("; ").Append(records.Count.ToString(CultureInfo.InvariantCulture)).Append(" games, ")
                .Append(all.Distinct.ToString(CultureInfo.InvariantCulture)).Append(" distinct decided games, ").Append(all.Undecided.ToString(CultureInfo.InvariantCulture)).Append(" undecided, ")
                .Append(all.Draws.ToString(CultureInfo.InvariantCulture)).Append(" draws.\n");
            sb.Append("- Intervals are Wilson at z = ").Append(z.ToString("0.00", CultureInfo.InvariantCulture)).Append(" over distinct games (play signature); a cell shows wins/decided games.\n\n");
            // 2. faction matrix.
            List<string> factions = Factions(records);
            sb.Append("## Faction matrix\n\nRow faction's wins against the column faction; the diagonal is slot 0's win share in the mirror (seat).\n\n| |");
            foreach (string g in factions)
            {
                sb.Append(' ').Append(g).Append(" |");
            }
            sb.Append("\n| --- |");
            foreach (string g in factions)
            {
                sb.Append(" --- |");
            }
            sb.Append('\n');
            foreach (string f in factions)
            {
                sb.Append("| ").Append(f).Append(" |");
                foreach (string g in factions)
                {
                    sb.Append(' ').Append(f == g ? "seat " : string.Empty).Append(FactionCell(records, f, g).Text(z)).Append(" |");
                }
                sb.Append('\n');
            }
            // 3. opening matrices.
            sb.Append("\n## Opening matrix\n\n");
            bool anyOpening = false;
            foreach (string f in factions)
            {
                foreach (string g in factions)
                {
                    if (string.CompareOrdinal(f, g) > 0)
                    {
                        continue;
                    }
                    List<string> rowsOf = OpeningsOf(records, f);
                    List<string> colsOf = OpeningsOf(records, g);
                    if (rowsOf.Count == 0 || colsOf.Count == 0)
                    {
                        continue;
                    }
                    anyOpening = true;
                    sb.Append("### ").Append(f).Append(" (rows) against ").Append(g).Append(" (columns)\n\n| |");
                    foreach (string y in colsOf)
                    {
                        sb.Append(' ').Append(y).Append(" |");
                    }
                    sb.Append("\n| --- |");
                    foreach (string y in colsOf)
                    {
                        sb.Append(" --- |");
                    }
                    sb.Append('\n');
                    foreach (string x in rowsOf)
                    {
                        sb.Append("| ").Append(x).Append(" |");
                        foreach (string y in colsOf)
                        {
                            WinCount cell = OpeningCell(records, f, x, g, y);
                            sb.Append(' ').Append(f == g && x == y ? (cell.Decided == 0 ? "-" : "games " + cell.Decided.ToString(CultureInfo.InvariantCulture) + " (distinct " + cell.Distinct.ToString(CultureInfo.InvariantCulture) + ")") : cell.Text(z)).Append(" |");
                        }
                        sb.Append('\n');
                    }
                    sb.Append('\n');
                }
            }
            if (!anyOpening)
            {
                sb.Append("The profile has no openings.\n\n");
            }
            else
            {
                sb.Append("Opening rows (draws, use share, mirror games against other openings, wins, distinct games, interval, cross games):\n\n| faction | opening | draws | use share | mirror games | distinct | wins | cross games | cross wins |\n| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |\n");
                foreach (OpeningRow row in LadderRunner.OpeningRows(records))
                {
                    sb.Append("| ").Append(row.Faction).Append(" | ").Append(row.Opening).Append(" | ").Append(row.Draws.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.UseShare.ToString("0.000", CultureInfo.InvariantCulture))
                        .Append(" | ").Append(row.MirrorGames.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.MirrorSignatures.Count.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                        .Append(row.MirrorWins.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.CrossGames.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.CrossWins.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
                }
                sb.Append('\n');
            }
            // 4. unit use.
            sb.Append("## Unit use\n\nShare of the faction's slot-games that trained (or completed) the definition at least once.\n\n| faction | definition | used | slot-games | share |\n| --- | --- | ---: | ---: | ---: |\n");
            foreach (UseRow row in UnitUse(records, data))
            {
                sb.Append("| ").Append(row.Faction).Append(" | ").Append(row.Id).Append(" | ").Append(row.Used.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.SlotGames.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.ShareText).Append(" |\n");
            }
            // 5. mechanic use.
            sb.Append("\n## Mechanic use\n\nShare of the faction's applicable slot-games with at least one use.\n\n| faction | mechanic | used | applicable slot-games | share |\n| --- | --- | ---: | ---: | ---: |\n");
            foreach (UseRow row in MechanicUse(records, data))
            {
                sb.Append("| ").Append(row.Faction).Append(" | ").Append(row.Id).Append(" | ").Append(row.Used.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.SlotGames.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.ShareText).Append(" |\n");
            }
            sb.Append("\nNot measurable as \"used\" from events or state today:\n\n");
            foreach (KeyValuePair<string, string> item in Unmeasurable)
            {
                sb.Append("- ").Append(item.Key).Append(": ").Append(item.Value).Append(".\n");
            }
            // 6. unspent.
            sb.Append("\n## Unspent resources\n\nLower median of the bank over the slot-games still running at the time (n).\n\n| faction | time (s) | n | common | rare |\n| --- | ---: | ---: | ---: | ---: |\n");
            foreach (UnspentRow row in Unspent(records, bankSeconds))
            {
                sb.Append("| ").Append(row.Faction).Append(" | ").Append(row.Seconds.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(row.N.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(row.N > 0 ? row.MedianCommon.ToString(CultureInfo.InvariantCulture) : "-").Append(" | ").Append(row.N > 0 ? row.MedianRare.ToString(CultureInfo.InvariantCulture) : "-").Append(" |\n");
            }
            // 7. trust checks.
            sb.Append("\n## Trust checks\n\nBars are placeholders for the developer (balance_report.json).\n\n");
            foreach (BalanceCheck check in checks)
            {
                sb.Append("    ").Append(check.Line).Append('\n');
            }
            return sb.ToString();
        }
    }

    /// <summary>What a balance run was played with, written to the first line of balance.jsonl so the report can be re-rendered.</summary>
    public sealed class BalanceMeta
    {
        /// <summary>Profile that played both slots.</summary>
        public string Profile { get; }

        /// <summary>Base seed. Range: any ulong.</summary>
        public ulong BaseSeed { get; }

        /// <summary>Games per cell. Unit: games. Range: 2 to 10000.</summary>
        public int GamesPerCell { get; }

        /// <summary>Game cap. Unit: seconds. Range: 60 to 7200.</summary>
        public int MaxGameSeconds { get; }

        /// <summary>Bundles the values.</summary>
        public BalanceMeta(string profile, ulong baseSeed, int gamesPerCell, int maxGameSeconds)
        {
            Profile = profile;
            BaseSeed = baseSeed;
            GamesPerCell = gamesPerCell;
            MaxGameSeconds = maxGameSeconds;
        }

        /// <summary>The meta line of balance.jsonl.</summary>
        public string ToJsonLine()
        {
            return "{\"balance\":{\"profile\":" + GameRecord.Quote(Profile) + ",\"base_seed\":" + GameRecord.Quote(BaseSeed.ToString(CultureInfo.InvariantCulture)) + ",\"games_per_cell\":" + GamesPerCell.ToString(CultureInfo.InvariantCulture)
                + ",\"max_game_seconds\":" + MaxGameSeconds.ToString(CultureInfo.InvariantCulture) + "}}";
        }
    }
}

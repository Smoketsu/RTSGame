// rts/tools/BalanceHarness/GameSummary.cs
// The per-slot summary of one game (spec: rts/docs/specs/balance_measurement.md M.4.2): spending in total and by class, the bank at fixed times, income per time bucket, every HQ move, the units trained (package E0 adds workers_by_bucket and spent_other_by_bucket: workers per completed income bucket and non-worker, non-drop-off spending per bucket), the structures
// completed and the mechanic uses counted. SummaryObserver fills it; it is written to a game record as "summary0" and "summary1" (keys in the order of the spec, maps in ordinal key order) and parsed back by the
// coordinator, so behaviour checks and the balance report read records only. With ai_eval.json metric settings it also carries the ProductionMetrics keys (spec ai_production_defence.md section 7). Immutable. Tool-side only. Part of Rts.Tools.BalanceHarness.
// Package S-M (ai_threat_map.md 7.1) adds drop_off_life and harass; a record without them parses as empty lives and zero counters.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>What one slot did in one game, in integers.</summary>
    public sealed class GameSummary
    {
        /// <summary>Common resource spent, less refunds. Unit: resource units. Range: 0 upward.</summary>
        public int SpentCommon { get; }

        /// <summary>Rare resource spent, less refunds. Unit: resource units. Range: 0 upward.</summary>
        public int SpentRare { get; }

        /// <summary>Net spending by class (hq, worker, army, static_defence, wall, drop_off, structure) as [common, rare], ordinal key order. Unit: resource units.</summary>
        public IReadOnlyDictionary<string, int[]> Spent { get; }

        /// <summary>The player's [common, rare] bank after the step at each of the bank times, or null when the game ended earlier. Unit: resource units. Range: 0 upward each; one entry per bank time.</summary>
        public IReadOnlyList<int[]?> Bank { get; }

        /// <summary>[common, rare] deposited per bucket; bucket k covers ticks [k B, (k + 1) B). Unit: resource units per bucket. Range: 0 upward each; one entry per bucket that started.</summary>
        public IReadOnlyList<int[]> Income { get; }

        /// <summary>Each HQ move as [uproot tick, root tick or -1, distance in tenths of a tile (0 when never rooted)]. Unit: ticks, ticks, tenths of a tile.</summary>
        public IReadOnlyList<int[]> HqMoves { get; }

        /// <summary>Units trained by definition ID, ordinal order. Unit: units.</summary>
        public IReadOnlyDictionary<string, int> Trained { get; }

        /// <summary>Structures completed by definition ID, ordinal order. Unit: structures.</summary>
        public IReadOnlyDictionary<string, int> Completed { get; }

        /// <summary>Mechanic uses by mechanic ID (MechanicDetectors), ordinal order; an absent key is 0. Unit: uses.</summary>
        public IReadOnlyDictionary<string, int> Mechanics { get; }

        /// <summary>Own workers (units with CanGather and no footprint) after the step at each bank time, or null when the game ended earlier; empty for a record written before this field existed. Unit: workers. Range: 0 upward each; one entry per bank time, or none.</summary>
        public IReadOnlyList<int?> Workers { get; }

        /// <summary>Completion tick of each own non-HQ drop-off structure, ascending. Unit: ticks. Range: 0 upward; empty for none or for an old record.</summary>
        public IReadOnlyList<int> DropOffTicks { get; }

        /// <summary>Tick of the first unit trained or research completed, by definition ID, ordinal order (research_and_unlocks.md R2.5: the time of the first Thunderhead and each research item); empty for a record from before this field. Unit: ticks. Range: 0 upward.</summary>
        public IReadOnlyDictionary<string, int> FirstTick { get; }

        /// <summary>Own workers (units with CanGather and no footprint) after the last step of each income bucket that ended (ai_economy_calc.md 3.1); empty for a record from before package E0. Unit: workers. Range: 0 upward each; one entry per completed bucket.</summary>
        public IReadOnlyList<int> WorkersByBucket { get; }

        /// <summary>[common, rare] net spending per bucket on every class except worker and drop_off (ai_economy_calc.md 3.1); empty for a record from before package E0. Unit: resource units per bucket. Range: 0 upward each (a refund can make one negative); one entry per bucket that started.</summary>
        public IReadOnlyList<int[]> SpentOtherByBucket { get; }

        /// <summary>Package S-M (ai_threat_map.md 7.1): one [placed tick, death tick] per own non-HQ drop-off structure in placement order; the death tick is the tick of an enemy kill, else -1; empty for a record from before S-M. Unit: ticks. Range: 0 upward, -1 for alive.</summary>
        public IReadOnlyList<int[]> DropOffLife { get; }

        /// <summary>Package S-M (ai_threat_map.md 7.1): the harass counters of the slot (squads, worker kills, value killed, value lost); all 0 for a record from before S-M.</summary>
        public HarassMetrics Harass { get; }

        /// <summary>The production and defence metrics (spec ai_production_defence.md section 7), or null for a summary collected without them (every older record and every observer built without settings). Their keys come after the older ones.</summary>
        public ProductionMetrics? Production { get; }

        /// <summary>Bundles the values; the last four default to empty (a record from before package B0, or before the first-tick map).</summary>
        public GameSummary(int spentCommon, int spentRare, IReadOnlyDictionary<string, int[]> spent, IReadOnlyList<int[]?> bank, IReadOnlyList<int[]> income, IReadOnlyList<int[]> hqMoves,
            IReadOnlyDictionary<string, int> trained, IReadOnlyDictionary<string, int> completed, IReadOnlyDictionary<string, int> mechanics,
            IReadOnlyList<int?>? workers = null, IReadOnlyList<int>? dropOffTicks = null,
            IReadOnlyDictionary<string, int>? firstTick = null, ProductionMetrics? production = null,
            IReadOnlyList<int>? workersByBucket = null, IReadOnlyList<int[]>? spentOtherByBucket = null, IReadOnlyList<int[]>? dropOffLife = null, HarassMetrics? harass = null)
        {
            DropOffLife = dropOffLife ?? Array.Empty<int[]>();
            Harass = harass ?? HarassMetrics.Empty;
            WorkersByBucket = workersByBucket ?? Array.Empty<int>();
            SpentOtherByBucket = spentOtherByBucket ?? Array.Empty<int[]>();
            Production = production;
            FirstTick = firstTick ?? new SortedDictionary<string, int>(StringComparer.Ordinal);
            Workers = workers ?? Array.Empty<int?>();
            DropOffTicks = dropOffTicks ?? Array.Empty<int>();
            SpentCommon = spentCommon;
            SpentRare = spentRare;
            Spent = spent;
            Bank = bank;
            Income = income;
            HqMoves = hqMoves;
            Trained = trained;
            Completed = completed;
            Mechanics = mechanics;
        }

        /// <summary>The summary as one JSON object (no spaces), keys in the fixed order of the spec.</summary>
        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"spent_common\":").Append(Int(SpentCommon)).Append(",\"spent_rare\":").Append(Int(SpentRare)).Append(",\"spent\":{");
            // Separator state for the class map.
            bool firstClass = true;
            foreach (KeyValuePair<string, int[]> pair in Spent)
            {
                sb.Append(firstClass ? "" : ",").Append(GameRecord.Quote(pair.Key)).Append(":[").Append(Int(pair.Value[0])).Append(',').Append(Int(pair.Value[1])).Append(']');
                firstClass = false;
            }
            sb.Append("},\"bank\":[");
            for (int i = 0; i < Bank.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append(Bank[i] == null ? "null" : "[" + Int(Bank[i]![0]) + "," + Int(Bank[i]![1]) + "]");
            }
            sb.Append("],\"income\":[");
            for (int i = 0; i < Income.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append('[').Append(Int(Income[i][0])).Append(',').Append(Int(Income[i][1])).Append(']');
            }
            sb.Append("],\"hq_moves\":[");
            for (int i = 0; i < HqMoves.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append('[').Append(Int(HqMoves[i][0])).Append(',').Append(Int(HqMoves[i][1])).Append(',').Append(Int(HqMoves[i][2])).Append(']');
            }
            sb.Append("],\"trained\":").Append(Counts(Trained)).Append(",\"completed\":").Append(Counts(Completed)).Append(",\"mechanics\":").Append(Counts(Mechanics)).Append(",\"workers\":[");
            for (int i = 0; i < Workers.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append(Workers[i] == null ? "null" : Int(Workers[i]!.Value));
            }
            sb.Append("],\"drop_off_ticks\":[");
            for (int i = 0; i < DropOffTicks.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append(Int(DropOffTicks[i]));
            }
            sb.Append("],\"workers_by_bucket\":[");
            for (int i = 0; i < WorkersByBucket.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append(Int(WorkersByBucket[i]));
            }
            sb.Append("],\"spent_other_by_bucket\":[");
            for (int i = 0; i < SpentOtherByBucket.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append('[').Append(Int(SpentOtherByBucket[i][0])).Append(',').Append(Int(SpentOtherByBucket[i][1])).Append(']');
            }
            sb.Append("],\"drop_off_life\":[");
            for (int i = 0; i < DropOffLife.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append('[').Append(Int(DropOffLife[i][0])).Append(',').Append(Int(DropOffLife[i][1])).Append(']');
            }
            sb.Append("],\"harass\":[").Append(Int(Harass.Squads)).Append(',').Append(Int(Harass.WorkerKills)).Append(',').Append(Int(Harass.KillsValue)).Append(',').Append(Int(Harass.LossesValue)).Append(']');
            if (FirstTick.Count > 0)
            {
                sb.Append(",\"first_tick\":").Append(Counts(FirstTick));
            }
            if (Production != null)
            {
                sb.Append(Production.ToJsonMembers());
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Reads a summary written by <see cref="ToJson"/>; throws <see cref="DataValidationException"/> naming the field when it is malformed.</summary>
        /// <param name="node">The "summary0" or "summary1" object of a game record.</param>
        public static GameSummary Parse(JsonValue node)
        {
            // Spending by class.
            SortedDictionary<string, int[]> spent = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in node.Require("spent").Members)
            {
                spent[pair.Key] = Pair(pair.Value);
            }
            List<int[]?> bank = new List<int[]?>();
            foreach (JsonValue item in node.RequireArray("bank"))
            {
                bank.Add(item.Kind == JsonKind.Null ? null : Pair(item));
            }
            List<int[]> income = new List<int[]>();
            foreach (JsonValue item in node.RequireArray("income"))
            {
                income.Add(Pair(item));
            }
            List<int[]> moves = new List<int[]>();
            foreach (JsonValue item in node.RequireArray("hq_moves"))
            {
                moves.Add(new[] { item.Items[0].AsInt(), item.Items[1].AsInt(), item.Items[2].AsInt() });
            }
            // Package B0 fields; a record without them reads as empty.
            List<int?> workers = new List<int?>();
            List<int> dropOffs = new List<int>();
            if (node.TryGetMember("workers", out JsonValue? workerNode))
            {
                foreach (JsonValue item in workerNode.Items)
                {
                    workers.Add(item.Kind == JsonKind.Null ? (int?)null : item.AsInt());
                }
            }
            if (node.TryGetMember("drop_off_ticks", out JsonValue? dropNode))
            {
                foreach (JsonValue item in dropNode.Items)
                {
                    dropOffs.Add(item.AsInt());
                }
            }
            // Package E0 fields; a record without them reads as empty.
            // Own workers after the last step of each ended income bucket. Unit: workers. Range: 0 upward; one entry per ended bucket.
            List<int> workersByBucket = new List<int>();
            // Per bucket [common, rare] net spending outside worker and drop_off classes. Unit: resource units. Range: any; one entry per started bucket.
            List<int[]> spentOther = new List<int[]>();
            if (node.TryGetMember("workers_by_bucket", out JsonValue? byBucketNode))
            {
                foreach (JsonValue item in byBucketNode.Items)
                {
                    workersByBucket.Add(item.AsInt());
                }
            }
            if (node.TryGetMember("spent_other_by_bucket", out JsonValue? otherNode))
            {
                foreach (JsonValue item in otherNode.Items)
                {
                    spentOther.Add(Pair(item));
                }
            }
            // Package S-M fields; a record without them reads as empty drop-off lives and zero harass counters.
            // Per drop-off [placed tick, death tick or -1]. Unit: ticks. Range: 0 upward, -1 for alive.
            List<int[]> dropOffLife = new List<int[]>();
            if (node.TryGetMember("drop_off_life", out JsonValue? lifeNode))
            {
                foreach (JsonValue item in lifeNode.Items)
                {
                    dropOffLife.Add(Pair(item));
                }
            }
            // The harass counters [squads, worker kills, kills value, losses value]; zeros when the key is absent.
            HarassMetrics harass = HarassMetrics.Empty;
            if (node.TryGetMember("harass", out JsonValue? harassNode))
            {
                harass = new HarassMetrics(harassNode.Items[0].AsInt(), harassNode.Items[1].AsInt(), harassNode.Items[2].AsInt(), harassNode.Items[3].AsInt());
            }
            // First-tick map; a record without it reads as empty.
            IReadOnlyDictionary<string, int>? firstTick = node.TryGetMember("first_tick", out JsonValue? firstNode) ? ReadCounts(firstNode) : null;
            return new GameSummary(node.RequireInt("spent_common", int.MinValue, int.MaxValue), node.RequireInt("spent_rare", int.MinValue, int.MaxValue), spent, bank, income, moves,
                ReadCounts(node.Require("trained")), ReadCounts(node.Require("completed")), ReadCounts(node.Require("mechanics")), workers, dropOffs, firstTick,
                node.TryGetMember(ProductionMetrics.FirstKey, out _) ? ProductionMetrics.Parse(node) : null, workersByBucket, spentOther, dropOffLife, harass);
        }

        /// <summary>An integer as invariant text.</summary>
        private static string Int(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A count map as a JSON object in the map's (ordinal) key order.</summary>
        private static string Counts(IReadOnlyDictionary<string, int> counts)
        {
            StringBuilder sb = new StringBuilder("{");
            // Separator state.
            bool first = true;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                sb.Append(first ? "" : ",").Append(GameRecord.Quote(pair.Key)).Append(':').Append(Int(pair.Value));
                first = false;
            }
            return sb.Append('}').ToString();
        }

        /// <summary>Reads a two-entry integer array.</summary>
        private static int[] Pair(JsonValue node)
        {
            return new[] { node.Items[0].AsInt(), node.Items[1].AsInt() };
        }

        /// <summary>Reads an object of integer counts into an ordinal sorted map.</summary>
        private static SortedDictionary<string, int> ReadCounts(JsonValue node)
        {
            SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in node.Members)
            {
                counts[pair.Key] = pair.Value.AsInt();
            }
            return counts;
        }
    }
}

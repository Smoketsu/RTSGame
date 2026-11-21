// rts/tools/BalanceHarness/GameSummary.cs
// The per-slot summary of one game (spec: rts/docs/specs/balance_measurement.md M.4.2): spending in total and by class, the bank at fixed times, income per time bucket, every HQ move, the units trained, the structures
// completed and the mechanic uses counted. SummaryObserver fills it; it is written to a game record as "summary0" and "summary1" (keys in the order of the spec, maps in ordinal key order) and parsed back by the
// coordinator, so behaviour checks and the balance report read records only. Immutable. Tool-side only. Part of Rts.Tools.BalanceHarness.
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

        /// <summary>Bundles the values; the last two default to empty (a record from before package B0).</summary>
        public GameSummary(int spentCommon, int spentRare, IReadOnlyDictionary<string, int[]> spent, IReadOnlyList<int[]?> bank, IReadOnlyList<int[]> income, IReadOnlyList<int[]> hqMoves,
            IReadOnlyDictionary<string, int> trained, IReadOnlyDictionary<string, int> completed, IReadOnlyDictionary<string, int> mechanics,
            IReadOnlyList<int?>? workers = null, IReadOnlyList<int>? dropOffTicks = null)
        {
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
            sb.Append("]}");
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
            return new GameSummary(node.RequireInt("spent_common", int.MinValue, int.MaxValue), node.RequireInt("spent_rare", int.MinValue, int.MaxValue), spent, bank, income, moves,
                ReadCounts(node.Require("trained")), ReadCounts(node.Require("completed")), ReadCounts(node.Require("mechanics")), workers, dropOffs);
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

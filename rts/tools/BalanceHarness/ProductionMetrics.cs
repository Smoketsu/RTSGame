// rts/tools/BalanceHarness/ProductionMetrics.cs
// The production and defence metrics of one slot of one game (spec: rts/docs/specs/ai_production_defence.md section 7): worker losses, raid seconds, and the producer, capacity, income and army samples.
// ProductionMetricsCollector fills it; GameSummary writes it as extra keys of "summary0" / "summary1" (appended after the older keys, so old records still parse) and reads it back. Immutable.
// Measurement only: it never reaches a controller. Tool-side only. Part of Rts.Tools.BalanceHarness.
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>What the production and defence metrics measured for one slot.</summary>
    public sealed class ProductionMetrics
    {
        /// <summary>JSON key of <see cref="WorkerLosses"/>; its presence marks a summary that carries metrics. Range: fixed.</summary>
        public const string FirstKey = "worker_losses";

        /// <summary>Own workers (can gather, no footprint) killed by an enemy up to the window end. Unit: workers. Range: 0 upward.</summary>
        public int WorkerLosses { get; }

        /// <summary>Of <see cref="WorkerLosses"/>, workers killed within fed_window_seconds of their own training. Unit: workers. Range: 0 to WorkerLosses.</summary>
        public int WorkerFedLosses { get; }

        /// <summary>Whole seconds in which an own worker, structure or HQ near an own drop-off took a hit from an enemy. Unit: seconds. Range: 0 to the window.</summary>
        public int RaidSeconds { get; }

        /// <summary>Raid seconds with no armed own army unit (or goal) near the newest hit while own army supply was at least the raiders'. Unit: seconds. Range: 0 to RaidSeconds.</summary>
        public int RaidUnansweredEdgeSeconds { get; }

        /// <summary>Own complete producers (structures training a non-worker) at each sample time, null when the game ended earlier. Unit: structures. Range: 0 upward; one entry per sample time.</summary>
        public IReadOnlyList<int?> Producers { get; }

        /// <summary>Producer capacity at each sample time, null when the game ended earlier. Unit: common per minute. Range: 0 upward; one entry per sample time.</summary>
        public IReadOnlyList<int?> Capacity { get; }

        /// <summary>Common deposited in the 60 s before each sample time, null when the game ended earlier. Unit: common. Range: 0 upward; one entry per sample time.</summary>
        public IReadOnlyList<int?> Income60 { get; }

        /// <summary>Supply of the own army units at each sample time, null when the game ended earlier. Unit: supply. Range: 0 upward; one entry per sample time.</summary>
        public IReadOnlyList<int?> ArmySupply { get; }

        /// <summary>Integer mean (floor) of the common bank over every whole second from bank_mean_from_seconds to the window end, or null when no such second was reached. Unit: common. Range: 0 upward.</summary>
        public int? BankMean { get; }

        /// <summary>Revision 2 (R2.2), reported only: the first whole second up to the window at which enemy army supply within raid_radius_tiles of the slot's HQ or of any own drop-off was at least overrun_supply (once reached, every later second counts); null when never. Unit: seconds. Range: 1 to the window, or null.</summary>
        public int? OverrunSecond { get; }

        /// <summary>Revision 2, reported only: <see cref="WorkerLosses"/> with a kill tick before <see cref="OverrunSecond"/> (all of them when it is null). Unit: workers. Range: 0 to WorkerLosses.</summary>
        public int WorkerLossesPreOverrun { get; }

        /// <summary>Revision 2, reported only: <see cref="WorkerFedLosses"/> with a kill tick before <see cref="OverrunSecond"/> (all of them when it is null). Unit: workers. Range: 0 to WorkerFedLosses.</summary>
        public int WorkerFedLossesPreOverrun { get; }

        /// <summary>Bundles the values. The overrun values default to "never reached" (the pre-overrun counts to the totals), which is what a record written before revision 2 means.</summary>
        public ProductionMetrics(int workerLosses, int workerFedLosses, int raidSeconds, int raidUnansweredEdgeSeconds, IReadOnlyList<int?> producers, IReadOnlyList<int?> capacity,
            IReadOnlyList<int?> income60, IReadOnlyList<int?> armySupply, int? bankMean, int? overrunSecond = null, int? workerLossesPreOverrun = null, int? workerFedLossesPreOverrun = null)
        {
            OverrunSecond = overrunSecond;
            WorkerLossesPreOverrun = workerLossesPreOverrun ?? workerLosses;
            WorkerFedLossesPreOverrun = workerFedLossesPreOverrun ?? workerFedLosses;
            WorkerLosses = workerLosses;
            WorkerFedLosses = workerFedLosses;
            RaidSeconds = raidSeconds;
            RaidUnansweredEdgeSeconds = raidUnansweredEdgeSeconds;
            Producers = producers;
            Capacity = capacity;
            Income60 = income60;
            ArmySupply = armySupply;
            BankMean = bankMean;
        }

        /// <summary>The metrics as JSON members (no braces, leading comma), keys in the order of the spec table.</summary>
        public string ToJsonMembers()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(",\"worker_losses\":").Append(Int(WorkerLosses)).Append(",\"worker_fed_losses\":").Append(Int(WorkerFedLosses)).Append(",\"raid_seconds\":").Append(Int(RaidSeconds))
                .Append(",\"raid_unanswered_edge_seconds\":").Append(Int(RaidUnansweredEdgeSeconds));
            sb.Append(",\"producers\":").Append(Array(Producers)).Append(",\"capacity\":").Append(Array(Capacity)).Append(",\"income_60\":").Append(Array(Income60)).Append(",\"army_supply\":").Append(Array(ArmySupply));
            sb.Append(",\"bank_mean\":").Append(BankMean == null ? "null" : Int(BankMean.Value));
            sb.Append(",\"overrun_second\":").Append(OverrunSecond == null ? "null" : Int(OverrunSecond.Value)).Append(",\"worker_losses_pre_overrun\":").Append(Int(WorkerLossesPreOverrun))
                .Append(",\"worker_fed_losses_pre_overrun\":").Append(Int(WorkerFedLossesPreOverrun));
            return sb.ToString();
        }

        /// <summary>Reads the metrics of a summary object; throws <see cref="DataValidationException"/> naming the key when one is missing or malformed.</summary>
        /// <param name="node">The "summary0" or "summary1" object, which holds <see cref="FirstKey"/>.</param>
        public static ProductionMetrics Parse(JsonValue node)
        {
            JsonValue bank = node.Require("bank_mean");
            return new ProductionMetrics(node.RequireInt("worker_losses", 0, int.MaxValue), node.RequireInt("worker_fed_losses", 0, int.MaxValue), node.RequireInt("raid_seconds", 0, int.MaxValue),
                node.RequireInt("raid_unanswered_edge_seconds", 0, int.MaxValue), Read(node, "producers"), Read(node, "capacity"), Read(node, "income_60"), Read(node, "army_supply"),
                bank.Kind == JsonKind.Null ? (int?)null : bank.AsInt(),
                // The overrun_second member when present and not null (the first second of the overrun). Unit: seconds. Range: 0 to the summary window; null when never reached.
                node.TryGetMember("overrun_second", out JsonValue? overrun) && overrun.Kind != JsonKind.Null ? overrun.AsInt() : (int?)null,
                // The worker_losses_pre_overrun member when present. Unit: workers. Range: 0 upward; null when the key is absent.
                node.TryGetMember("worker_losses_pre_overrun", out JsonValue? pre) ? pre.AsInt() : (int?)null,
                // The worker_fed_losses_pre_overrun member when present. Unit: workers. Range: 0 upward; null when the key is absent.
                node.TryGetMember("worker_fed_losses_pre_overrun", out JsonValue? fedPre) ? fedPre.AsInt() : (int?)null);
        }

        /// <summary>A sample array as JSON, null entries as null.</summary>
        private static string Array(IReadOnlyList<int?> values)
        {
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                sb.Append(i > 0 ? "," : "").Append(values[i] == null ? "null" : Int(values[i]!.Value));
            }
            return sb.Append(']').ToString();
        }

        /// <summary>Reads a sample array.</summary>
        private static List<int?> Read(JsonValue node, string key)
        {
            List<int?> list = new List<int?>();
            foreach (JsonValue item in node.RequireArray(key))
            {
                list.Add(item.Kind == JsonKind.Null ? (int?)null : item.AsInt());
            }
            return list;
        }

        /// <summary>An integer as invariant text.</summary>
        private static string Int(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

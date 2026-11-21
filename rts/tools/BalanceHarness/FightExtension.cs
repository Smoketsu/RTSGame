// rts/tools/BalanceHarness/FightExtension.cs
// The extended part of a fight record (spec: rts/docs/specs/fight_predictor_v2.md 5.3): the value ratio of the fight, the depth class of the lane at army spawn and the snapshot of both
// armies taken when either seat had lost the configured share of its value. Written after the last original key of the record only when the fight settings have extended_records true; a
// record without these keys is exactly the original format. Immutable, tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The living units of one definition in a snapshot.</summary>
    public readonly struct SnapGroup
    {
        /// <summary>Living units of the definition. Unit: units. Range: 1 upward.</summary>
        public int Count { get; }

        /// <summary>Summed health of those units, in thousandths of a health point, rounded down. Unit: thousandths of health points. Range: 1 upward.</summary>
        public long HealthMilli { get; }

        /// <summary>Stores the group.</summary>
        /// <param name="count">Living units. Unit: units. Range: 1 upward.</param>
        /// <param name="healthMilli">Summed health. Unit: thousandths of health points. Range: 1 upward.</param>
        public SnapGroup(int count, long healthMilli)
        {
            Count = count;
            HealthMilli = healthMilli;
        }
    }

    /// <summary>The extended keys of a fight record. Immutable.</summary>
    public sealed class FightExtension
    {
        /// <summary>Seat 1's target value over seat 0's in parts per 10 000 (10000 for an ordinary fight). Unit: parts per 10 000. Range: 2000 to 50000.</summary>
        public int ValueRatioPt { get; }

        /// <summary>Depth class (0 dry, 1 shallow, 2 deep) held by most lane tiles at army spawn (ties to the lower class) in a flooded case; -1 for other cases. Unit: depth class. Range: -1 to 2.</summary>
        public int LaneDepthClass { get; }

        /// <summary>Ticks from army spawn to the end of the step that took the snapshot, or -1 when the fight ended first. Unit: ticks. Range: -1 or 0 upward.</summary>
        public int SnapTick { get; }

        /// <summary>Seat 0's living army units at the snapshot by definition ID (ordinal order); empty when <see cref="SnapTick"/> is -1.</summary>
        public IReadOnlyDictionary<string, SnapGroup> Snap0 { get; }

        /// <summary>Seat 1's living army units at the snapshot by definition ID (ordinal order); empty when <see cref="SnapTick"/> is -1.</summary>
        public IReadOnlyDictionary<string, SnapGroup> Snap1 { get; }

        /// <summary>Bundles the values.</summary>
        public FightExtension(int valueRatioPt, int laneDepthClass, int snapTick, IReadOnlyDictionary<string, SnapGroup> snap0, IReadOnlyDictionary<string, SnapGroup> snap1)
        {
            ValueRatioPt = valueRatioPt;
            LaneDepthClass = laneDepthClass;
            SnapTick = snapTick;
            Snap0 = snap0;
            Snap1 = snap1;
        }

        /// <summary>The snapshot of <paramref name="seat"/> (0 or 1).</summary>
        /// <param name="seat">Seat number. Unit: seat. Range: 0 or 1.</param>
        public IReadOnlyDictionary<string, SnapGroup> Snap(int seat)
        {
            return seat == 0 ? Snap0 : Snap1;
        }

        /// <summary>The keys as JSON text starting with a comma, to append to a record line.</summary>
        public string ToJsonTail()
        {
            return new StringBuilder().Append(",\"value_ratio_pt\":").Append(ValueRatioPt.ToString(CultureInfo.InvariantCulture))
                .Append(",\"lane_depth_class\":").Append(LaneDepthClass.ToString(CultureInfo.InvariantCulture))
                .Append(",\"snap_tick\":").Append(SnapTick.ToString(CultureInfo.InvariantCulture))
                .Append(",\"snap0\":").Append(SnapJson(Snap0)).Append(",\"snap1\":").Append(SnapJson(Snap1)).ToString();
        }

        /// <summary>Reads the extended keys of a record; null when the record has none of them. A missing key takes its default (ratio 10000, depth class -1, snapshot absent).</summary>
        /// <param name="root">The record object.</param>
        public static FightExtension? Read(JsonValue root)
        {
            bool hasRatio = root.TryGetMember("value_ratio_pt", out JsonValue? ratio);
            bool hasDepth = root.TryGetMember("lane_depth_class", out JsonValue? depth);
            bool hasTick = root.TryGetMember("snap_tick", out JsonValue? tick);
            bool hasSnap0 = root.TryGetMember("snap0", out JsonValue? snap0);
            bool hasSnap1 = root.TryGetMember("snap1", out JsonValue? snap1);
            if (!hasRatio && !hasDepth && !hasTick && !hasSnap0 && !hasSnap1)
            {
                return null;
            }
            return new FightExtension(hasRatio ? ratio!.AsInt(1, int.MaxValue) : FightSpec.EqualRatioPt, hasDepth ? depth!.AsInt(-1, 2) : -1, hasTick ? tick!.AsInt(-1, int.MaxValue) : -1,
                ReadSnap(hasSnap0 ? snap0 : null), ReadSnap(hasSnap1 ? snap1 : null));
        }

        /// <summary>JSON object text of a snapshot map: "id":[count,health milli] in ordinal order.</summary>
        private static string SnapJson(IReadOnlyDictionary<string, SnapGroup> snap)
        {
            StringBuilder sb = new StringBuilder("{");
            // True before the first entry is written.
            bool first = true;
            foreach (KeyValuePair<string, SnapGroup> pair in snap)
            {
                sb.Append(first ? string.Empty : ",").Append(GameRecord.Quote(pair.Key)).Append(":[").Append(pair.Value.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(pair.Value.HealthMilli.ToString(CultureInfo.InvariantCulture)).Append(']');
                first = false;
            }
            return sb.Append('}').ToString();
        }

        /// <summary>Reads a snapshot object into an ordinal-sorted map; an absent object is an empty map.</summary>
        private static IReadOnlyDictionary<string, SnapGroup> ReadSnap(JsonValue? node)
        {
            SortedDictionary<string, SnapGroup> map = new SortedDictionary<string, SnapGroup>(StringComparer.Ordinal);
            if (node == null)
            {
                return map;
            }
            foreach (KeyValuePair<string, JsonValue> pair in node.Members)
            {
                if (pair.Value.Items.Count != 2)
                {
                    throw pair.Value.Fail("must be [count, health in thousandths]");
                }
                map.Add(pair.Key, new SnapGroup(pair.Value.Items[0].AsInt(1, int.MaxValue), long.Parse(pair.Value.Items[1].NumberText, NumberStyles.None, CultureInfo.InvariantCulture)));
            }
            return map;
        }
    }
}

// rts/tools/BalanceHarness/FightRecord.cs
// The result of one equal-value fight (spec: rts/docs/specs/micro_benchmark.md section 5): the record a worker prints as one JSON line (keys in a fixed order, invariant culture)
// and the coordinator parses back. Immutable. Tool-side only: the left fractions are doubles rounded to three decimals, so a record parsed from its own line is identical.
// Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>One fight's outcome. Immutable.</summary>
    public sealed class FightRecord
    {
        /// <summary>Result: seat 0 won.</summary>
        public const string Seat0Won = "seat0";

        /// <summary>Result: seat 1 won.</summary>
        public const string Seat1Won = "seat1";

        /// <summary>Result: both armies were eliminated in the same tick.</summary>
        public const string Draw = "draw";

        /// <summary>Result: the fight stopped because neither side could hurt the other.</summary>
        public const string Stalemate = "stalemate";

        /// <summary>Result: the fight was still running at the cap.</summary>
        public const string Unresolved = "unresolved";

        /// <summary>Fight ID "case/value/seat0/seat1".</summary>
        public string Fight { get; }

        /// <summary>Case ID.</summary>
        public string Case { get; }

        /// <summary>Target army value. Unit: resource units. Range: 50 to 20000.</summary>
        public int ArmyValue { get; }

        /// <summary>Side ID in seat 0.</summary>
        public string Seat0 { get; }

        /// <summary>Side ID in seat 1.</summary>
        public string Seat1 { get; }

        /// <summary>Seat 0's army: unit ID to count in ordinal order. Unit: units.</summary>
        public IReadOnlyDictionary<string, int> Units0 { get; }

        /// <summary>Seat 1's army: unit ID to count in ordinal order. Unit: units.</summary>
        public IReadOnlyDictionary<string, int> Units1 { get; }

        /// <summary>Start value of seat 0. Unit: resource units. Range: 1 upward.</summary>
        public int Value0 { get; }

        /// <summary>Start value of seat 1. Unit: resource units. Range: 1 upward.</summary>
        public int Value1 { get; }

        /// <summary>True when the start values differ by more than the allowed share of the larger; such a fight is left out of the checks.</summary>
        public bool Mismatch { get; }

        /// <summary>One of <see cref="Seat0Won"/>, <see cref="Seat1Won"/>, <see cref="Draw"/>, <see cref="Stalemate"/>, <see cref="Unresolved"/>.</summary>
        public string Result { get; }

        /// <summary>Why the fight ended: both_eliminated, eliminated, cannot_target, dome_absorbed, no_damage or time_cap.</summary>
        public string Reason { get; }

        /// <summary>Ticks from army spawn to the end. Unit: ticks. Range: 0 upward.</summary>
        public int EndTick { get; }

        /// <summary><see cref="EndTick"/> in seconds with one decimal, as text. Unit: seconds.</summary>
        public string Seconds { get; }

        /// <summary>Value left of seat 0: sum of unit value times health fraction over the start value. Unit: fraction. Range: 0 to 1, three decimals.</summary>
        public double Left0 { get; }

        /// <summary>Value left of seat 1. Unit: fraction. Range: 0 to 1, three decimals.</summary>
        public double Left1 { get; }

        /// <summary>Live army units of seat 0 at the end. Unit: units. Range: 0 upward.</summary>
        public int Alive0 { get; }

        /// <summary>Live army units of seat 1 at the end. Unit: units. Range: 0 upward.</summary>
        public int Alive1 { get; }

        /// <summary>Flooded lane tiles when the armies spawned, or -1 outside the flooded kind. Unit: tiles. Range: -1 upward.</summary>
        public int FloodedLaneTiles { get; }

        /// <summary>The fight's simulation seed. Range: any ulong.</summary>
        public ulong Seed { get; }

        /// <summary>State hash of the simulation after the last step. Range: any ulong.</summary>
        public ulong StateHash { get; }

        /// <summary>Bundles the values.</summary>
        public FightRecord(string fight, string fightCase, int armyValue, string seat0, string seat1, IReadOnlyDictionary<string, int> units0, IReadOnlyDictionary<string, int> units1,
            int value0, int value1, bool mismatch, string result, string reason, int endTick, string seconds, double left0, double left1, int alive0, int alive1,
            int floodedLaneTiles, ulong seed, ulong stateHash)
        {
            Fight = fight;
            Case = fightCase;
            ArmyValue = armyValue;
            Seat0 = seat0;
            Seat1 = seat1;
            Units0 = units0;
            Units1 = units1;
            Value0 = value0;
            Value1 = value1;
            Mismatch = mismatch;
            Result = result;
            Reason = reason;
            EndTick = endTick;
            Seconds = seconds;
            Left0 = left0;
            Left1 = left1;
            Alive0 = alive0;
            Alive1 = alive1;
            FloodedLaneTiles = floodedLaneTiles;
            Seed = seed;
            StateHash = stateHash;
        }

        /// <summary>The side ID in <paramref name="seat"/>.</summary>
        /// <param name="seat">Seat number. Unit: seat. Range: 0 or 1.</param>
        public string Side(int seat)
        {
            return seat == 0 ? Seat0 : Seat1;
        }

        /// <summary>Value left of <paramref name="seat"/>. Unit: fraction. Range: 0 to 1.</summary>
        /// <param name="seat">Seat number. Unit: seat. Range: 0 or 1.</param>
        public double Left(int seat)
        {
            return seat == 0 ? Left0 : Left1;
        }

        /// <summary>Start value of <paramref name="seat"/>. Unit: resource units.</summary>
        /// <param name="seat">Seat number. Unit: seat. Range: 0 or 1.</param>
        public int Value(int seat)
        {
            return seat == 0 ? Value0 : Value1;
        }

        /// <summary>The winning seat (0 or 1), or -1 for a draw, a stalemate or an unresolved fight.</summary>
        public int WinnerSeat => Result == Seat0Won ? 0 : Result == Seat1Won ? 1 : -1;

        /// <summary>
        /// Exchange ratio of the winning seat: enemy start value over (own start value times own value lost); positive infinity when the winner lost nothing.
        /// Tool-side double. Unit: ratio. Range: 0 to infinity. Only meaningful when <see cref="WinnerSeat"/> is 0 or 1.
        /// </summary>
        public double WinnerExchangeRatio()
        {
            // Winner's seat and the value it lost. Unit: fraction of its start value.
            int winner = WinnerSeat;
            double lost = 1.0 - Left(winner);
            return lost <= 0.0 ? double.PositiveInfinity : Value(1 - winner) / (Value(winner) * lost);
        }

        /// <summary>The record as one JSON line (no trailing newline), keys in the order of the spec.</summary>
        public string ToJsonLine()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"fight\":").Append(GameRecord.Quote(Fight)).Append(",\"case\":").Append(GameRecord.Quote(Case))
                .Append(",\"army_value\":").Append(ArmyValue.ToString(CultureInfo.InvariantCulture))
                .Append(",\"seat0\":").Append(GameRecord.Quote(Seat0)).Append(",\"seat1\":").Append(GameRecord.Quote(Seat1))
                .Append(",\"units0\":").Append(UnitsJson(Units0)).Append(",\"units1\":").Append(UnitsJson(Units1))
                .Append(",\"value0\":").Append(Value0.ToString(CultureInfo.InvariantCulture)).Append(",\"value1\":").Append(Value1.ToString(CultureInfo.InvariantCulture))
                .Append(",\"mismatch\":").Append(Mismatch ? "true" : "false")
                .Append(",\"result\":").Append(GameRecord.Quote(Result)).Append(",\"reason\":").Append(GameRecord.Quote(Reason))
                .Append(",\"end_tick\":").Append(EndTick.ToString(CultureInfo.InvariantCulture)).Append(",\"seconds\":").Append(Seconds)
                .Append(",\"left0\":").Append(Left0.ToString("0.000", CultureInfo.InvariantCulture)).Append(",\"left1\":").Append(Left1.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(",\"alive0\":").Append(Alive0.ToString(CultureInfo.InvariantCulture)).Append(",\"alive1\":").Append(Alive1.ToString(CultureInfo.InvariantCulture))
                .Append(",\"flooded_lane_tiles\":").Append(FloodedLaneTiles.ToString(CultureInfo.InvariantCulture))
                .Append(",\"seed\":").Append(GameRecord.Quote(Seed.ToString(CultureInfo.InvariantCulture)))
                .Append(",\"state_hash\":").Append(GameRecord.Quote(StateHash.ToString("x16", CultureInfo.InvariantCulture))).Append('}');
            return sb.ToString();
        }

        /// <summary>Parses a line written by <see cref="ToJsonLine"/>; throws <see cref="HarnessException"/> when it is malformed.</summary>
        /// <param name="line">One JSON line.</param>
        public static FightRecord Parse(string line)
        {
            try
            {
                JsonValue root = JsonReader.Parse(line, "worker output");
                string result = root.RequireNonEmptyString("result");
                if (result != Seat0Won && result != Seat1Won && result != Draw && result != Stalemate && result != Unresolved)
                {
                    throw new HarnessException("malformed worker line (unknown result '" + result + "'): " + line);
                }
                return new FightRecord(root.RequireNonEmptyString("fight"), root.RequireNonEmptyString("case"), root.RequireInt("army_value", 0, int.MaxValue),
                    root.RequireNonEmptyString("seat0"), root.RequireNonEmptyString("seat1"), ReadUnits(root.Require("units0")), ReadUnits(root.Require("units1")),
                    root.RequireInt("value0", 0, int.MaxValue), root.RequireInt("value1", 0, int.MaxValue), root.RequireBool("mismatch"), result,
                    root.RequireNonEmptyString("reason"), root.RequireInt("end_tick", 0, int.MaxValue), root.Require("seconds").NumberText,
                    double.Parse(root.Require("left0").NumberText, CultureInfo.InvariantCulture), double.Parse(root.Require("left1").NumberText, CultureInfo.InvariantCulture),
                    root.RequireInt("alive0", 0, int.MaxValue), root.RequireInt("alive1", 0, int.MaxValue), root.RequireInt("flooded_lane_tiles", -1, int.MaxValue),
                    ulong.Parse(root.RequireString("seed"), NumberStyles.None, CultureInfo.InvariantCulture),
                    ulong.Parse(root.RequireString("state_hash"), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is DataValidationException || ex is FormatException || ex is OverflowException)
            {
                throw new HarnessException("malformed worker line (" + ex.Message + "): " + line);
            }
        }

        /// <summary>JSON object text of a unit-count map in its (ordinal) order.</summary>
        private static string UnitsJson(IReadOnlyDictionary<string, int> units)
        {
            StringBuilder sb = new StringBuilder("{");
            // True before the first entry is written.
            bool first = true;
            foreach (KeyValuePair<string, int> pair in units)
            {
                sb.Append(first ? string.Empty : ",").Append(GameRecord.Quote(pair.Key)).Append(':').Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                first = false;
            }
            return sb.Append('}').ToString();
        }

        /// <summary>Reads a unit-count object into an ordinal-sorted map.</summary>
        private static IReadOnlyDictionary<string, int> ReadUnits(JsonValue node)
        {
            SortedDictionary<string, int> map = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in node.Members)
            {
                map.Add(pair.Key, pair.Value.AsInt(0, int.MaxValue));
            }
            return map;
        }
    }
}

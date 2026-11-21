// rts/tools/BalanceHarness/PredictEvaluator.cs
// The fight predictor checked against measured fights (spec: rts/docs/specs/balance_measurement.md C.3 to C.7): turns one FightRecord into a prediction (FightPredictor.Predict on the
// units of the record, entries in ordinal ID order), scores it (decided fights give the headline accuracy, no-winner fights are counted apart), builds the breakdown tables (unit pair, side,
// terrain case, army value, unmodelled mechanic, calibration bins, seat order) and applies the proposed decision rule (SIMULATE, FIX_FORMULA or DEVELOPER_CHOICE).
// Measurement only: FightPredictor, the fight sweep and its records are not changed. Every verdict is integer arithmetic (parts per 10 000, long products); Fix64 is used for the
// predictor numbers and the margin bins, double only inside HarnessStats.Wilson for display. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Rts.Ai;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions.Skai;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Counts of a group of fights: all scored fights, the decided ones and the correctly predicted ones.</summary>
    public sealed class PredictTally
    {
        /// <summary>Fights counted (decided and no-winner; mismatched fights are never counted). Unit: fights. Range: 0 upward.</summary>
        public int Fights { get; private set; }

        /// <summary>Decided fights (result seat0 or seat1). Unit: fights. Range: 0 to Fights.</summary>
        public int Decided { get; private set; }

        /// <summary>Decided fights whose predicted class equals the result. Unit: fights. Range: 0 to Decided.</summary>
        public int Correct { get; private set; }

        /// <summary>Adds one scored fight; a mismatched fight is ignored.</summary>
        /// <param name="fight">The predicted and scored fight.</param>
        public void Add(PredictedFight fight)
        {
            if (fight.Kind == PredictedFight.KindMismatch)
            {
                return;
            }
            Fights++;
            if (fight.Kind == PredictedFight.KindDecided)
            {
                Decided++;
                Correct += fight.Correct ? 1 : 0;
            }
        }

        /// <summary>Accuracy as text with three decimals, rounded half up in integers ("0.857"); "-" with no decided fight.</summary>
        public string AccuracyText()
        {
            return PredictEvaluator.Ratio3(Correct, Decided);
        }
    }

    /// <summary>Wall time of Predict calls for the stderr line of --predict-fights (fight_predictor_v2.md 5.4); never written to a file or stdout.</summary>
    public sealed class PredictTiming
    {
        /// <summary>Calls timed. Unit: calls. Range: 0 upward.</summary>
        public long Calls { get; private set; }

        /// <summary>Sum of the call times. Unit: stopwatch ticks. Range: 0 upward.</summary>
        public long TotalTicks { get; private set; }

        /// <summary>Longest call. Unit: stopwatch ticks. Range: 0 upward.</summary>
        public long MaxTicks { get; private set; }

        /// <summary>Adds one call.</summary>
        /// <param name="ticks">Time of the call. Unit: stopwatch ticks. Range: 0 upward.</param>
        public void Add(long ticks)
        {
            Calls++;
            TotalTicks += ticks;
            MaxTicks = Math.Max(MaxTicks, ticks);
        }

        /// <summary>Mean call time in microseconds, in integers; 0 with no call. Unit: microseconds. Range: 0 upward.</summary>
        public long MeanMicroseconds()
        {
            return Calls == 0 ? 0 : TotalTicks * 1000000 / Stopwatch.Frequency / Calls;
        }

        /// <summary>Longest call time in microseconds. Unit: microseconds. Range: 0 upward.</summary>
        public long MaxMicroseconds()
        {
            return MaxTicks * 1000000 / Stopwatch.Frequency;
        }
    }

    /// <summary>One fight with its prediction and its score. Immutable.</summary>
    public sealed class PredictedFight
    {
        /// <summary>Kind: the fight is left out (start values too far apart).</summary>
        public const string KindMismatch = "mismatch";

        /// <summary>Kind: the fight has a winner (seat0 or seat1).</summary>
        public const string KindDecided = "decided";

        /// <summary>Kind: stalemate, unresolved or draw.</summary>
        public const string KindNoWinner = "no_winner";

        /// <summary>Predicted class: seat 0 wins.</summary>
        public const string ClassSeat0 = "seat0";

        /// <summary>Predicted class: seat 1 wins.</summary>
        public const string ClassSeat1 = "seat1";

        /// <summary>Predicted class: no winner (equal times, or both sides at the longest time).</summary>
        public const string ClassNone = "none";

        /// <summary>The measured fight.</summary>
        public FightRecord Record { get; }

        /// <summary>One of <see cref="ClassSeat0"/>, <see cref="ClassSeat1"/>, <see cref="ClassNone"/>.</summary>
        public string Class { get; }

        /// <summary>Predicted margin: longer time over shorter time, capped; 1 for class none. Unit: ratio. Range: 1 to predictor_max_advantage.</summary>
        public Fix64 Margin { get; }

        /// <summary>Seconds seat 0 needs to kill seat 1. Unit: seconds. Range: predictor_min_seconds to predictor_max_seconds.</summary>
        public Fix64 TimeSeat0KillsSeat1 { get; }

        /// <summary>Seconds seat 1 needs to kill seat 0. Unit: seconds. Range: predictor_min_seconds to predictor_max_seconds.</summary>
        public Fix64 TimeSeat1KillsSeat0 { get; }

        /// <summary>One of <see cref="KindMismatch"/>, <see cref="KindDecided"/>, <see cref="KindNoWinner"/>.</summary>
        public string Kind { get; }

        /// <summary>True for a decided fight whose predicted class equals the result; false otherwise.</summary>
        public bool Correct { get; }

        /// <summary>Scores a prediction against its record.</summary>
        /// <param name="record">The measured fight.</param>
        /// <param name="predictedClass">Predicted class.</param>
        /// <param name="margin">Predicted margin. Unit: ratio.</param>
        /// <param name="time0">Seconds seat 0 needs. Unit: seconds.</param>
        /// <param name="time1">Seconds seat 1 needs. Unit: seconds.</param>
        public PredictedFight(FightRecord record, string predictedClass, Fix64 margin, Fix64 time0, Fix64 time1)
        {
            Record = record;
            Class = predictedClass;
            Margin = margin;
            TimeSeat0KillsSeat1 = time0;
            TimeSeat1KillsSeat0 = time1;
            Kind = record.Mismatch ? KindMismatch : record.WinnerSeat >= 0 ? KindDecided : KindNoWinner;
            Correct = Kind == KindDecided && predictedClass == record.Result;
        }
    }

    /// <summary>One row of the mechanic table.</summary>
    public sealed class MechanicRow
    {
        /// <summary>Mechanic ID. Range: one of <see cref="PredictEvaluator.MechanicIds"/>.</summary>
        public string Id { get; }

        /// <summary>Fights where the mechanic is present.</summary>
        public PredictTally Present { get; } = new PredictTally();

        /// <summary>Fights where it is absent.</summary>
        public PredictTally Absent { get; } = new PredictTally();

        /// <summary>Fights where it is the only mechanic present.</summary>
        public PredictTally Only { get; } = new PredictTally();

        /// <summary>Creates an empty row.</summary>
        /// <param name="id">Mechanic ID.</param>
        public MechanicRow(string id)
        {
            Id = id;
        }
    }

    /// <summary>One calibration bin: decided fights whose predicted winner (class not none) has a margin in the bin.</summary>
    public sealed class CalibrationBin
    {
        /// <summary>Lower edge of the bin (inclusive). Unit: ratio. Range: 1 to 10000.</summary>
        public Fix64 Low { get; }

        /// <summary>Upper edge (exclusive), or null for the open last bin. Unit: ratio.</summary>
        public Fix64? High { get; }

        /// <summary>Decided fights in the bin. Unit: fights. Range: 0 upward.</summary>
        public int Decided { get; set; }

        /// <summary>Of those, fights the predicted winner won. Unit: fights. Range: 0 to Decided.</summary>
        public int Wins { get; set; }

        /// <summary>Value left of the measured winner of each fight, in thousandths, ascending after <see cref="PredictEvaluator.Evaluate"/>. Unit: thousandths of the start value. Range: 0 to 1000.</summary>
        public List<int> WinnerLeftMilli { get; } = new List<int>();

        /// <summary>Creates an empty bin.</summary>
        /// <param name="low">Lower edge. Unit: ratio.</param>
        /// <param name="high">Upper edge or null. Unit: ratio.</param>
        public CalibrationBin(Fix64 low, Fix64? high)
        {
            Low = low;
            High = high;
        }
    }

    /// <summary>The decision of the proposed rule and why.</summary>
    public sealed class PredictDecision
    {
        /// <summary>Verdict: the fights are better simulated than predicted.</summary>
        public const string Simulate = "SIMULATE";

        /// <summary>Verdict: keep the formula and add the listed mechanics.</summary>
        public const string FixFormula = "FIX_FORMULA";

        /// <summary>Verdict: the developer chooses between fixing the listed mechanics and simulating.</summary>
        public const string DeveloperChoice = "DEVELOPER_CHOICE";

        /// <summary>One of <see cref="Simulate"/>, <see cref="FixFormula"/>, <see cref="DeveloperChoice"/>.</summary>
        public string Verdict { get; }

        /// <summary>Plain-text lines naming the rule that fired and the numbers that fired it. Range: 1 upward entries.</summary>
        public IReadOnlyList<string> Reasons { get; }

        /// <summary>Mechanic IDs listed under the verdict (to add under FIX_FORMULA, badly predicted under DEVELOPER_CHOICE); empty under SIMULATE.</summary>
        public IReadOnlyList<string> Listed { get; }

        /// <summary>Bundles the values.</summary>
        public PredictDecision(string verdict, IReadOnlyList<string> reasons, IReadOnlyList<string> listed)
        {
            Verdict = verdict;
            Reasons = reasons;
            Listed = listed;
        }
    }

    /// <summary>Everything <see cref="PredictEvaluator.Evaluate"/> computes from a set of fight records.</summary>
    public sealed class PredictEvaluation
    {
        /// <summary>Every fight in the order given.</summary>
        public List<PredictedFight> Fights { get; } = new List<PredictedFight>();

        /// <summary>All scored fights.</summary>
        public PredictTally Overall { get; } = new PredictTally();

        /// <summary>Scored fights with no unmodelled mechanic present.</summary>
        public PredictTally Clean { get; } = new PredictTally();

        /// <summary>Mismatched fights, left out. Unit: fights.</summary>
        public int Mismatched { get; set; }

        /// <summary>No-winner fights (not mismatched). Unit: fights.</summary>
        public int NoWinner { get; set; }

        /// <summary>No-winner fights whose predicted class is none. Unit: fights.</summary>
        public int NoWinnerAgree { get; set; }

        /// <summary>Unordered unit pair "a vs b" (ordinal) to tally.</summary>
        public SortedDictionary<string, PredictTally> ByPair { get; } = new SortedDictionary<string, PredictTally>(StringComparer.Ordinal);

        /// <summary>Side ID to tally (a fight counts for both sides).</summary>
        public SortedDictionary<string, PredictTally> BySide { get; } = new SortedDictionary<string, PredictTally>(StringComparer.Ordinal);

        /// <summary>Case ID to tally.</summary>
        public SortedDictionary<string, PredictTally> ByCase { get; } = new SortedDictionary<string, PredictTally>(StringComparer.Ordinal);

        /// <summary>Army value text (zero padded to 6 digits) to tally.</summary>
        public SortedDictionary<string, PredictTally> ByValue { get; } = new SortedDictionary<string, PredictTally>(StringComparer.Ordinal);

        /// <summary>"case / value" (value zero padded) to tally.</summary>
        public SortedDictionary<string, PredictTally> ByCaseValue { get; } = new SortedDictionary<string, PredictTally>(StringComparer.Ordinal);

        /// <summary>One row per mechanic of <see cref="PredictEvaluator.MechanicIds"/>, in that order.</summary>
        public List<MechanicRow> Mechanics { get; } = new List<MechanicRow>();

        /// <summary>Calibration bins in edge order.</summary>
        public List<CalibrationBin> Bins { get; } = new List<CalibrationBin>();

        /// <summary>Decided fights predicted none (a predicted none on a decided fight is wrong). Unit: fights.</summary>
        public int DecidedPredictedNone { get; set; }

        /// <summary>Of those, how many had seat 0 as the winner. Unit: fights.</summary>
        public int DecidedPredictedNoneSeat0 { get; set; }

        /// <summary>Pairs of fights that differ only in seat order and are both decided. Unit: pairs.</summary>
        public int SeatPairs { get; set; }

        /// <summary>Of those, pairs where the predictor names the same side in both orders (classes are mirror images). Unit: pairs.</summary>
        public int SeatPairsMirrored { get; set; }

        /// <summary>Of the mirrored pairs, those where the measured winner is not the same side in both orders (a seat split). Unit: pairs.</summary>
        public int SeatSplits { get; set; }
    }

    /// <summary>Prediction, scoring, breakdowns and the decision rule.</summary>
    public static class PredictEvaluator
    {
        /// <summary>The unmodelled mechanics of the FightPredictor header, in report order.</summary>
        public static readonly string[] MechanicIds = { "drench", "splash", "momentum", "flooding", "windstorm_range", "regeneration", "hit_delay", "focus_fire" };

        /// <summary>Integer ratio num / den as text with three decimals, rounded half up; "-" when den is 0.</summary>
        /// <param name="num">Numerator. Range: 0 upward.</param>
        /// <param name="den">Denominator. Range: 0 upward.</param>
        public static string Ratio3(long num, long den)
        {
            if (den <= 0)
            {
                return "-";
            }
            // Thousandths, rounded half up. Range: 0 upward.
            long milli = (num * 2000 + den) / (den * 2);
            return (milli / 1000).ToString(CultureInfo.InvariantCulture) + "." + (milli % 1000).ToString("D3", CultureInfo.InvariantCulture);
        }

        /// <summary>Entries of one side: one per unit ID in ordinal order, count and count x MaxHealth (C.3).</summary>
        /// <param name="units">Unit ID to count (ordinal order, as in the record).</param>
        /// <param name="data">The loaded data set.</param>
        public static List<FightEntry> Entries(IReadOnlyDictionary<string, int> units, DataSet data)
        {
            // Entries in the order of the map. Range: 0 upward entries.
            List<FightEntry> entries = new List<FightEntry>();
            foreach (KeyValuePair<string, int> pair in units)
            {
                if (!data.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition))
                {
                    throw new HarnessException("unknown unit '" + pair.Key + "' in the fight record (the records may come from other data)");
                }
                entries.Add(new FightEntry(definition, Fix64.FromInt(pair.Value), Fix64.FromInt(pair.Value) * definition.MaxHealth));
            }
            return entries;
        }

        /// <summary>True when side A (seat 0) shoots uphill: only the high_ground case (C.2: seat 1 holds the plateau).</summary>
        /// <param name="caseKind">Kind of the fight's case (a <see cref="FightKinds"/> value).</param>
        public static bool AUphill(string caseKind)
        {
            return caseKind == FightKinds.HighGround;
        }

        /// <summary>Class and margin of a prediction (C.3): none when both times are equal; seat0 when A wins, else seat1; margin = min(MaxAdvantage, longer / max(shorter, MinSeconds)), 1 for none.</summary>
        /// <param name="p">The predictor's answer.</param>
        /// <param name="s">The predictor numbers.</param>
        /// <param name="margin">The margin. Unit: ratio. Range: 1 to MaxAdvantage.</param>
        public static string Classify(FightPrediction p, PredictorSettings s, out Fix64 margin)
        {
            if (p.TimeAKillsB == p.TimeBKillsA)
            {
                margin = Fix64.One;
                return PredictedFight.ClassNone;
            }
            // The two times ordered.
            Fix64 longer = Fix64.Max(p.TimeAKillsB, p.TimeBKillsA);
            Fix64 shorter = Fix64.Min(p.TimeAKillsB, p.TimeBKillsA);
            margin = Fix64.Min(s.MaxAdvantage, longer / Fix64.Max(shorter, s.MinSeconds));
            return p.AWins ? PredictedFight.ClassSeat0 : PredictedFight.ClassSeat1;
        }

        /// <summary>Predicts and scores one record.</summary>
        /// <param name="record">The measured fight.</param>
        /// <param name="caseKind">Kind of the record's case.</param>
        /// <param name="data">The loaded data set (units and damage table).</param>
        /// <param name="s">The predictor numbers.</param>
        public static PredictedFight Predict(FightRecord record, string caseKind, DataSet data, PredictorSettings s)
        {
            return Predict(record, caseKind, data, s, null, null);
        }

        /// <summary>
        /// Predicts and scores one record's start (fight_predictor_v2.md 5.4): the context carries the sweep's start distance as the gap and the water of the record's lane depth class;
        /// the wall time of the Predict call alone goes to <paramref name="timing"/>.
        /// </summary>
        /// <param name="record">The measured fight.</param>
        /// <param name="caseKind">Kind of the record's case.</param>
        /// <param name="data">The loaded data set (units and damage table).</param>
        /// <param name="s">The predictor numbers.</param>
        /// <param name="fightSettings">The fight settings that played the fights (start distance), or null for the default gap.</param>
        /// <param name="timing">Receives the wall time of the call, or null.</param>
        public static PredictedFight Predict(FightRecord record, string caseKind, DataSet data, PredictorSettings s, FightSettings? fightSettings, PredictTiming? timing)
        {
            List<FightEntry> a = Entries(record.Units0, data);
            List<FightEntry> b = Entries(record.Units1, data);
            FightContext context = StartContext(record, caseKind, fightSettings);
            // Stopwatch ticks around the call alone. Unit: stopwatch ticks.
            long begin = Stopwatch.GetTimestamp();
            FightPrediction p = FightPredictor.Predict(a, b, context, data.Content.Combat, s);
            timing?.Add(Stopwatch.GetTimestamp() - begin);
            string cls = Classify(p, s, out Fix64 margin);
            return new PredictedFight(record, cls, margin, p.TimeAKillsB, p.TimeBKillsA);
        }

        /// <summary>The water of a record's lane: its lane_depth_class (1 shallow, 2 deep, 0 none); a record without one is Shallow in a flooded case and None otherwise (5.4).</summary>
        /// <param name="record">The measured fight.</param>
        /// <param name="caseKind">Kind of the record's case.</param>
        public static FightWater WaterOf(FightRecord record, string caseKind)
        {
            // Depth class of the lane, or -1 when the record has none.
            int depth = record.Extension == null ? -1 : record.Extension.LaneDepthClass;
            if (depth < 0)
            {
                return caseKind == FightKinds.Flooded ? FightWater.Shallow : FightWater.None;
            }
            return depth == 0 ? FightWater.None : depth == 1 ? FightWater.Shallow : FightWater.Deep;
        }

        /// <summary>Context of a start prediction (5.4): AUphill as C.3, water of the lane, neutral water (-1), the sweep's start distance as the gap (-1 without settings).</summary>
        /// <param name="record">The measured fight.</param>
        /// <param name="caseKind">Kind of the record's case.</param>
        /// <param name="fightSettings">The fight settings that played the fights, or null.</param>
        public static FightContext StartContext(FightRecord record, string caseKind, FightSettings? fightSettings)
        {
            return new FightContext(AUphill(caseKind), WaterOf(record, caseKind), -1, fightSettings == null ? -Fix64.One : Fix64.FromInt(fightSettings.StartDistanceTiles));
        }

        /// <summary>Context of a snapshot prediction (5.4): as the start context but the armies are already engaged, so the gap is 0.</summary>
        /// <param name="record">The measured fight.</param>
        /// <param name="caseKind">Kind of the record's case.</param>
        public static FightContext SnapshotContext(FightRecord record, string caseKind)
        {
            return new FightContext(AUphill(caseKind), WaterOf(record, caseKind), -1, Fix64.Zero);
        }

        /// <summary>Entries of one seat's snapshot: one per definition ID in ordinal order, the living count and their summed health (thousandths of a health point to Fix64).</summary>
        /// <param name="snap">The snapshot map of the record.</param>
        /// <param name="data">The loaded data set.</param>
        public static List<FightEntry> SnapshotEntries(IReadOnlyDictionary<string, SnapGroup> snap, DataSet data)
        {
            List<FightEntry> entries = new List<FightEntry>();
            foreach (KeyValuePair<string, SnapGroup> pair in snap)
            {
                if (!data.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition))
                {
                    throw new HarnessException("unknown unit '" + pair.Key + "' in the fight record (the records may come from other data)");
                }
                // Health in health points: whole part plus the thousandths. Unit: health points. Range: 0 upward.
                Fix64 health = Fix64.FromInt((int)(pair.Value.HealthMilli / 1000)) + Fix64.FromFraction((int)(pair.Value.HealthMilli % 1000), 1000);
                entries.Add(new FightEntry(definition, Fix64.FromInt(pair.Value.Count), health));
            }
            return entries;
        }

        /// <summary>Predicts one record's snapshot (subset D) and scores it against the measured result of the fight; null when the record has no snapshot.</summary>
        /// <param name="record">The measured fight.</param>
        /// <param name="caseKind">Kind of the record's case.</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="s">The predictor numbers.</param>
        /// <param name="timing">Receives the wall time of the call, or null.</param>
        public static PredictedFight? PredictSnapshot(FightRecord record, string caseKind, DataSet data, PredictorSettings s, PredictTiming? timing)
        {
            if (record.Extension == null || record.Extension.SnapTick < 0)
            {
                return null;
            }
            List<FightEntry> a = SnapshotEntries(record.Extension.Snap0, data);
            List<FightEntry> b = SnapshotEntries(record.Extension.Snap1, data);
            FightContext context = SnapshotContext(record, caseKind);
            // Stopwatch ticks around the call alone. Unit: stopwatch ticks.
            long begin = Stopwatch.GetTimestamp();
            FightPrediction p = FightPredictor.Predict(a, b, context, data.Content.Combat, s);
            timing?.Add(Stopwatch.GetTimestamp() - begin);
            string cls = Classify(p, s, out Fix64 margin);
            return new PredictedFight(record, cls, margin, p.TimeAKillsB, p.TimeBKillsA);
        }

        /// <summary>The unmodelled mechanics present in a fight (C.5 table), from definitions and the case only; IDs in <see cref="MechanicIds"/> order.</summary>
        /// <param name="record">The fight.</param>
        /// <param name="caseKind">Kind of the fight's case.</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="hitDelayIgnoreTicks">hit_delay counts as present only for a weapon whose HitDelayTicks is above this (predict.json hit_delay_ignore_ticks; 0 is the original rule). Unit: ticks. Range: 0 to 20.</param>
        public static List<string> MechanicsPresent(FightRecord record, string caseKind, DataSet data, int hitDelayIgnoreTicks)
        {
            // Definitions of each seat's units.
            List<UnitDefinition> side0 = Definitions(record.Units0, data);
            List<UnitDefinition> side1 = Definitions(record.Units1, data);
            // Presence flags per mechanic.
            bool drench = (HasDrench(side0) && HasAir(side1)) || (HasDrench(side1) && HasAir(side0));
            bool splash = false;
            bool momentum = false;
            bool regeneration = false;
            bool hitDelay = false;
            foreach (UnitDefinition unit in side0.Concat2(side1))
            {
                splash |= unit.Weapon != null && unit.Weapon.SplashRadiusTiles > Fix64.Zero;
                hitDelay |= unit.Weapon != null && unit.Weapon.HitDelayTicks > hitDelayIgnoreTicks;
                regeneration |= unit.ShieldDome != null;
                momentum |= unit.SpeedTilesPerSecond > Fix64.Zero && data.Content.Factions.TryGetValue(unit.FactionId, out Rts.Sim.Factions.FactionDefinition? faction)
                    && faction.MechanicId == MomentumMechanic.MechanicId;
            }
            // Result in fixed order; windstorm_range and focus_fire never occur in fights (no cycles, attack-move control).
            List<string> present = new List<string>();
            if (drench) { present.Add("drench"); }
            if (splash) { present.Add("splash"); }
            if (momentum) { present.Add("momentum"); }
            if (caseKind == FightKinds.Flooded) { present.Add("flooding"); }
            if (regeneration) { present.Add("regeneration"); }
            if (hitDelay) { present.Add("hit_delay"); }
            return present;
        }

        /// <summary>Concatenation helper so the two sides can be walked in one loop (no LINQ allocation beyond one list).</summary>
        private static List<UnitDefinition> Concat2(this List<UnitDefinition> a, List<UnitDefinition> b)
        {
            List<UnitDefinition> all = new List<UnitDefinition>(a);
            all.AddRange(b);
            return all;
        }

        /// <summary>The definitions of a side's units, in the map's order.</summary>
        private static List<UnitDefinition> Definitions(IReadOnlyDictionary<string, int> units, DataSet data)
        {
            List<UnitDefinition> list = new List<UnitDefinition>();
            foreach (KeyValuePair<string, int> pair in units)
            {
                if (!data.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition))
                {
                    throw new HarnessException("unknown unit '" + pair.Key + "' in the fight record (the records may come from other data)");
                }
                list.Add(definition);
            }
            return list;
        }

        /// <summary>True when some unit's weapon applies drench.</summary>
        private static bool HasDrench(List<UnitDefinition> side)
        {
            foreach (UnitDefinition unit in side)
            {
                if (unit.Weapon != null && unit.Weapon.AppliesDrench)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when some unit is on the Air layer.</summary>
        private static bool HasAir(List<UnitDefinition> side)
        {
            foreach (UnitDefinition unit in side)
            {
                if (unit.Layer == MovementLayer.Air)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Index of the calibration bin of a margin: the last bin whose lower edge is at or below it (an edge value goes to the upper bin). Fix64 comparison only.</summary>
        /// <param name="edges">Lower bin edges, ascending, the first at or below every margin. Unit: ratio.</param>
        /// <param name="margin">Predicted margin. Unit: ratio. Range: 1 upward.</param>
        public static int BinIndex(IReadOnlyList<Fix64> edges, Fix64 margin)
        {
            // Bin found so far.
            int bin = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                if (margin >= edges[i])
                {
                    bin = i;
                }
            }
            return bin;
        }

        /// <summary>Evaluates fights already predicted: tallies, breakdowns, calibration and seat-order pairs.</summary>
        /// <param name="fights">Predicted fights, any order.</param>
        /// <param name="settings">Case lookup for terrain kinds (fights.json).</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="p">The predict.json settings.</param>
        public static PredictEvaluation Evaluate(IReadOnlyList<PredictedFight> fights, FightSettings settings, DataSet data, PredictSettings p)
        {
            PredictEvaluation e = new PredictEvaluation();
            foreach (string id in MechanicIds)
            {
                e.Mechanics.Add(new MechanicRow(id));
            }
            for (int i = 0; i < p.MarginBinEdges.Count; i++)
            {
                e.Bins.Add(new CalibrationBin(p.MarginBinEdges[i], i + 1 < p.MarginBinEdges.Count ? p.MarginBinEdges[i + 1] : (Fix64?)null));
            }
            // Fight by ID, for the seat-order partner lookup.
            SortedDictionary<string, PredictedFight> byId = new SortedDictionary<string, PredictedFight>(StringComparer.Ordinal);
            foreach (PredictedFight fight in fights)
            {
                e.Fights.Add(fight);
                byId[fight.Record.Fight] = fight;
                if (fight.Kind == PredictedFight.KindMismatch)
                {
                    e.Mismatched++;
                    continue;
                }
                FightRecord r = fight.Record;
                // Terrain kind and mechanics of this fight.
                string kind = settings.CaseById(r.Case).Kind;
                List<string> present = MechanicsPresent(r, kind, data, p.HitDelayIgnoreTicks);
                e.Overall.Add(fight);
                Tally(e.ByCase, r.Case).Add(fight);
                Tally(e.ByValue, r.ArmyValue.ToString("D6", CultureInfo.InvariantCulture)).Add(fight);
                Tally(e.ByCaseValue, r.Case + " / " + r.ArmyValue.ToString("D6", CultureInfo.InvariantCulture)).Add(fight);
                Tally(e.ByPair, string.CompareOrdinal(r.Seat0, r.Seat1) <= 0 ? r.Seat0 + " vs " + r.Seat1 : r.Seat1 + " vs " + r.Seat0).Add(fight);
                Tally(e.BySide, r.Seat0).Add(fight);
                Tally(e.BySide, r.Seat1).Add(fight);
                if (present.Count == 0)
                {
                    e.Clean.Add(fight);
                }
                foreach (MechanicRow row in e.Mechanics)
                {
                    if (present.Contains(row.Id))
                    {
                        row.Present.Add(fight);
                        if (present.Count == 1)
                        {
                            row.Only.Add(fight);
                        }
                    }
                    else
                    {
                        row.Absent.Add(fight);
                    }
                }
                if (fight.Kind == PredictedFight.KindNoWinner)
                {
                    e.NoWinner++;
                    e.NoWinnerAgree += fight.Class == PredictedFight.ClassNone ? 1 : 0;
                    continue;
                }
                if (fight.Class == PredictedFight.ClassNone)
                {
                    e.DecidedPredictedNone++;
                    e.DecidedPredictedNoneSeat0 += r.WinnerSeat == 0 ? 1 : 0;
                    continue;
                }
                CalibrationBin bin = e.Bins[BinIndex(p.MarginBinEdges, fight.Margin)];
                bin.Decided++;
                bin.Wins += fight.Correct ? 1 : 0;
                bin.WinnerLeftMilli.Add((int)Math.Round(r.Left(r.WinnerSeat) * 1000.0, MidpointRounding.AwayFromZero));
            }
            foreach (CalibrationBin bin in e.Bins)
            {
                bin.WinnerLeftMilli.Sort();
            }
            // Seat-order pairs: the same two sides, same case and value, swapped seats; each pair counted once.
            foreach (PredictedFight fight in e.Fights)
            {
                FightRecord r = fight.Record;
                // The part of the fight ID after the two sides: empty for an equal-value fight, "/r<ratio in pt>" for an uneven one (the swapped fight has the same ratio).
                string baseId = r.Case + "/" + r.ArmyValue.ToString(CultureInfo.InvariantCulture) + "/" + r.Seat0 + "/" + r.Seat1;
                string ratioSuffix = r.Fight.StartsWith(baseId, StringComparison.Ordinal) ? r.Fight.Substring(baseId.Length) : string.Empty;
                if (string.CompareOrdinal(r.Seat0, r.Seat1) >= 0 || fight.Kind != PredictedFight.KindDecided
                    || !byId.TryGetValue(r.Case + "/" + r.ArmyValue.ToString(CultureInfo.InvariantCulture) + "/" + r.Seat1 + "/" + r.Seat0 + ratioSuffix, out PredictedFight? swapped)
                    || swapped.Kind != PredictedFight.KindDecided)
                {
                    continue;
                }
                e.SeatPairs++;
                // Predicted winning seat in each order (0, 1 or -1 for none), and the measured one.
                int predicted = SeatOf(fight.Class);
                int predictedSwapped = SeatOf(swapped.Class);
                if (predicted < 0 || predictedSwapped < 0 || predicted == predictedSwapped)
                {
                    continue;
                }
                e.SeatPairsMirrored++;
                // The same side wins in both orders exactly when the winning seats differ.
                e.SeatSplits += r.WinnerSeat == swapped.Record.WinnerSeat ? 1 : 0;
            }
            return e;
        }

        /// <summary>The seat number of a predicted class, or -1 for none.</summary>
        private static int SeatOf(string predictedClass)
        {
            return predictedClass == PredictedFight.ClassSeat0 ? 0 : predictedClass == PredictedFight.ClassSeat1 ? 1 : -1;
        }

        /// <summary>The tally under a key, created on first use.</summary>
        private static PredictTally Tally(SortedDictionary<string, PredictTally> map, string key)
        {
            if (!map.TryGetValue(key, out PredictTally? tally))
            {
                tally = new PredictTally();
                map.Add(key, tally);
            }
            return tally;
        }

        /// <summary>Orders rows by accuracy ascending (cross-multiplied integers; rows without a decided fight last), ties by key ordinal.</summary>
        /// <param name="rows">Key and tally pairs.</param>
        public static List<KeyValuePair<string, PredictTally>> SortByAccuracy(IEnumerable<KeyValuePair<string, PredictTally>> rows)
        {
            List<KeyValuePair<string, PredictTally>> list = new List<KeyValuePair<string, PredictTally>>(rows);
            list.Sort((x, y) =>
            {
                if (x.Value.Decided == 0 || y.Value.Decided == 0)
                {
                    int byEmpty = (x.Value.Decided == 0 ? 1 : 0) - (y.Value.Decided == 0 ? 1 : 0);
                    return byEmpty != 0 ? byEmpty : string.CompareOrdinal(x.Key, y.Key);
                }
                // correct_1 x decided_2 against correct_2 x decided_1.
                int c = ((long)x.Value.Correct * y.Value.Decided).CompareTo((long)y.Value.Correct * x.Value.Decided);
                return c != 0 ? c : string.CompareOrdinal(x.Key, y.Key);
            });
            return list;
        }

        /// <summary>True when correct / decided is below thresholdPt / 10 000, in integers; false with no decided fight.</summary>
        /// <param name="correct">Correct fights. Range: 0 to decided.</param>
        /// <param name="decided">Decided fights. Range: 0 upward.</param>
        /// <param name="thresholdPt">Threshold in parts per 10 000. Range: 0 to 10000.</param>
        public static bool Below(int correct, int decided, int thresholdPt)
        {
            return decided > 0 && (long)correct * 10000 < (long)thresholdPt * decided;
        }

        /// <summary>The first calibration inversion (C.7 rule 1): a bin with at least minFights decided fights whose win share is below a lower such bin's by more than maxInversionPt; null when none.</summary>
        /// <param name="bins">Calibration bins in edge order.</param>
        /// <param name="minFights">Decided fights a bin needs. Unit: fights.</param>
        /// <param name="maxInversionPt">Largest allowed drop. Unit: parts per 10 000.</param>
        /// <returns>Indices (lower bin, higher bin), or null.</returns>
        public static int[]? FindInversion(IReadOnlyList<CalibrationBin> bins, int minFights, int maxInversionPt)
        {
            for (int high = 1; high < bins.Count; high++)
            {
                for (int low = 0; low < high; low++)
                {
                    CalibrationBin lo = bins[low];
                    CalibrationBin hi = bins[high];
                    if (lo.Decided < minFights || hi.Decided < minFights)
                    {
                        continue;
                    }
                    // hi.Wins / hi.Decided < lo.Wins / lo.Decided - maxInversion, multiplied out: all terms are integers.
                    long left = (long)hi.Wins * lo.Decided * 10000;
                    long right = (long)lo.Wins * hi.Decided * 10000 - (long)maxInversionPt * hi.Decided * lo.Decided;
                    if (left < right)
                    {
                        return new[] { low, high };
                    }
                }
            }
            return null;
        }

        /// <summary>Applies the proposed decision rule of C.7.</summary>
        /// <param name="s">The thresholds.</param>
        /// <param name="overall">Headline tally.</param>
        /// <param name="clean">Tally of fights with no mechanic present.</param>
        /// <param name="mechanics">The mechanic rows.</param>
        /// <param name="bins">The calibration bins.</param>
        public static PredictDecision Decide(PredictSettings s, PredictTally overall, PredictTally clean, IReadOnlyList<MechanicRow> mechanics, IReadOnlyList<CalibrationBin> bins)
        {
            // Reasons for rule 1, one line per condition that holds.
            List<string> simulate = new List<string>();
            if (Below(clean.Correct, clean.Decided, s.SimulateBelowCleanAccuracyPt))
            {
                simulate.Add("rule 1: clean accuracy " + clean.Correct.ToString(CultureInfo.InvariantCulture) + "/" + clean.Decided.ToString(CultureInfo.InvariantCulture) + " = " + clean.AccuracyText()
                    + " is below " + Pt(s.SimulateBelowCleanAccuracyPt) + " (the formula is wrong even where it claims to model everything)");
            }
            if (Below(overall.Correct, overall.Decided, s.SimulateBelowAccuracyPt))
            {
                simulate.Add("rule 1: headline accuracy " + overall.Correct.ToString(CultureInfo.InvariantCulture) + "/" + overall.Decided.ToString(CultureInfo.InvariantCulture) + " = " + overall.AccuracyText()
                    + " is below " + Pt(s.SimulateBelowAccuracyPt));
            }
            int[]? inversion = FindInversion(bins, s.MinFightsPerRow, s.CalibrationMaxInversionPt);
            if (inversion != null)
            {
                CalibrationBin lo = bins[inversion[0]];
                CalibrationBin hi = bins[inversion[1]];
                simulate.Add("rule 1: calibration is inverted: bin " + inversion[1].ToString(CultureInfo.InvariantCulture) + " wins " + hi.Wins.ToString(CultureInfo.InvariantCulture) + "/"
                    + hi.Decided.ToString(CultureInfo.InvariantCulture) + " = " + Ratio3(hi.Wins, hi.Decided) + ", below bin " + inversion[0].ToString(CultureInfo.InvariantCulture) + " at "
                    + lo.Wins.ToString(CultureInfo.InvariantCulture) + "/" + lo.Decided.ToString(CultureInfo.InvariantCulture) + " = " + Ratio3(lo.Wins, lo.Decided)
                    + " by more than " + Pt(s.CalibrationMaxInversionPt));
            }
            if (simulate.Count > 0)
            {
                return new PredictDecision(PredictDecision.Simulate, simulate, new List<string>());
            }
            // Mechanics judged (enough decided fights present) and the two lists the rules need.
            List<string> belowFloor = new List<string>();
            List<string> toAdd = new List<string>();
            foreach (MechanicRow row in mechanics)
            {
                if (row.Present.Decided < s.MechanicMinFights)
                {
                    continue;
                }
                if (Below(row.Present.Correct, row.Present.Decided, s.FixMinMechanicAccuracyPt))
                {
                    belowFloor.Add(row.Id);
                }
                if (Below(row.Present.Correct, row.Present.Decided, s.FixMinAccuracyPt))
                {
                    toAdd.Add(row.Id);
                }
            }
            if (belowFloor.Count == 0)
            {
                return new PredictDecision(PredictDecision.FixFormula, new List<string> { "rule 2: every judged mechanic is at or above " + Pt(s.FixMinMechanicAccuracyPt)
                    + "; the mechanics below " + Pt(s.FixMinAccuracyPt) + " are listed to add to the formula" }, toAdd);
            }
            return new PredictDecision(PredictDecision.DeveloperChoice, new List<string> { "rule 3: a judged mechanic is below " + Pt(s.FixMinMechanicAccuracyPt)
                + " accuracy, so the formula is not abandoned but the listed rows decide between fixing them and simulating" }, belowFloor);
        }

        /// <summary>Parts per 10 000 as a fraction text with up to four decimals ("0.75").</summary>
        /// <param name="pt">Parts per 10 000. Range: 0 to 10000.</param>
        public static string Pt(int pt)
        {
            return (pt / 10000).ToString(CultureInfo.InvariantCulture) + "." + (pt % 10000).ToString("D4", CultureInfo.InvariantCulture).TrimEnd('0').PadRight(2, '0');
        }
    }
}

// rts/tools/BalanceHarness/BehaviourChecks.cs
// The behaviour checks (spec: rts/docs/specs/balance_measurement.md M.4): a check is a named predicate over one slot's game summary (and its game record), registered by ID in <see cref="BehaviourChecks"/> (the only
// composition root for checks, as BuiltInMechanics is for mechanics) and declared with its bar values in rts/data/scripts/ai_eval/behaviour_checks.json. A later package adds a check by adding one registry entry and
// one data entry. Every bar comparison is integer arithmetic (fractions are parts per 10 000, "pt"), so a verdict is exact and platform independent. Tool-side only. Part of Rts.Tools.BalanceHarness.
// Package S-M (ai_threat_map.md 7.2) adds depot_losses_young, harass_trade and harass_squads (always Held; the bar is the paired test) and the optional paired_direction key of first_drop_off and income_by.
// Package RR-M (rts/docs/specs/ai_reef_runner_defence.md 4.2) adds won, worker_losses_pre_overrun, army_supply_ratio_at and trained_unit (always Held; the bar is the faction-paired test).
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>What a check says about one slot-game.</summary>
    public enum CheckOutcome
    {
        /// <summary>The rule held.</summary>
        Held = 0,

        /// <summary>The rule was broken.</summary>
        Violated = 1,

        /// <summary>The game offered nothing to judge (not counted in held or violated).</summary>
        Unjudged = 2,
    }

    /// <summary>The result of one check on one slot-game: the outcome, the metric as a fraction Num / Den and optional extra counters.</summary>
    public readonly struct BehaviourResult
    {
        /// <summary>Held, Violated or Unjudged.</summary>
        public CheckOutcome Outcome { get; }

        /// <summary>Numerator of the metric. Unit: as the check states. Range: 0 upward.</summary>
        public long Num { get; }

        /// <summary>Denominator of the metric. Unit: as the check states. Range: 1 upward.</summary>
        public long Den { get; }

        /// <summary>Extra counters summed over slot-games and printed after the bar (the check names them in <see cref="IBehaviourCheck.ExtraNames"/>); empty for none.</summary>
        public IReadOnlyList<long> Extras { get; }

        /// <summary>Bundles the values.</summary>
        public BehaviourResult(CheckOutcome outcome, long num, long den, IReadOnlyList<long>? extras = null)
        {
            Outcome = outcome;
            Num = num;
            Den = den;
            Extras = extras ?? Array.Empty<long>();
        }
    }

    /// <summary>The numbers every check may need that do not come from its own data entry.</summary>
    public sealed class BehaviourContext
    {
        /// <summary>Weight of one rare unit against one common unit, as parts per 10 000 (rare_weight). Unit: parts per 10 000. Range: 0 to 200000.</summary>
        public int RareWeightPt { get; }

        /// <summary>Length of one income bucket of the summary. Unit: ticks. Range: 1 upward.</summary>
        public int BucketTicks { get; }

        /// <summary>Seconds of the summary's bank snapshots (summary_bank_times_seconds), or empty when not known (settings validation without a run). Unit: seconds. Range: 0 to 8 entries of 1 to 7200.</summary>
        public IReadOnlyList<int> BankSeconds { get; }

        /// <summary>Simulation ticks per second (the match tuning), or 1 when not known. Unit: ticks per second. Range: 1 upward.</summary>
        public int TicksPerSecond { get; }

        /// <summary>The loaded data set (maps, units, tuning, AI profiles) for a check that models the game (econ_gap), or null when not known (settings validation without a run).</summary>
        public DataSet? Data { get; }

        /// <summary>Last tick the summary counts (summary_window_seconds x ticks per second), or int.MaxValue when not known. Unit: ticks. Range: 1 upward.</summary>
        public int WindowTicks { get; }

        /// <summary>Seconds of the summary's army samples (summary_sample_seconds), or empty when not known (settings validation without a run). Unit: seconds. Range: entries of 1 to 7200.</summary>
        public IReadOnlyList<int> SampleSeconds { get; }

        /// <summary>Bundles the values; the last five are optional (unknown while only validating settings).</summary>
        /// <param name="rareWeightPt">Rare weight. Unit: parts per 10 000.</param>
        /// <param name="bucketTicks">Income bucket. Unit: ticks.</param>
        /// <param name="bankSeconds">Bank snapshot seconds, or null.</param>
        /// <param name="ticksPerSecond">Ticks per second, or 1 when not known.</param>
        /// <param name="data">The data set, or null.</param>
        /// <param name="windowSeconds">The summary window, or int.MaxValue when not known. Unit: seconds. Range: 60 to 2400, or int.MaxValue.</param>
        /// <param name="sampleSeconds">Seconds of the army samples, or null when not known. Unit: seconds.</param>
        public BehaviourContext(int rareWeightPt, int bucketTicks, IReadOnlyList<int>? bankSeconds = null, int ticksPerSecond = 1, DataSet? data = null, int windowSeconds = int.MaxValue,
            IReadOnlyList<int>? sampleSeconds = null)
        {
            SampleSeconds = sampleSeconds ?? Array.Empty<int>();
            WindowTicks = windowSeconds == int.MaxValue ? int.MaxValue : windowSeconds * ticksPerSecond;
            Data = data;
            RareWeightPt = rareWeightPt;
            BucketTicks = bucketTicks;
            BankSeconds = bankSeconds ?? Array.Empty<int>();
            TicksPerSecond = ticksPerSecond;
        }

        /// <summary>common x 10 000 + rare weight x rare, one amount in the weighted unit (parts per 10 000 of a common unit).</summary>
        /// <param name="common">Common resource units. Range: 0 upward.</param>
        /// <param name="rare">Rare resource units. Range: 0 upward.</param>
        public long Weighted(long common, long rare)
        {
            return common * 10000 + RareWeightPt * rare;
        }
    }

    /// <summary>A behaviour check.</summary>
    public interface IBehaviourCheck
    {
        /// <summary>Registry ID. Range: lower-case letters, digits and underscores.</summary>
        string Id { get; }

        /// <summary>Reads this check's own keys from its entry of behaviour_checks.json; throws <see cref="DataValidationException"/> naming the file and the field.</summary>
        /// <param name="entry">The check's object under "checks".</param>
        void ReadSettings(JsonValue entry);

        /// <summary>Judges one slot-game.</summary>
        /// <param name="slot">The slot's summary.</param>
        /// <param name="game">The game record (the slot's faction, profile, result).</param>
        BehaviourResult Evaluate(GameSummary slot, GameRecord game);

        /// <summary>True when the check is switched on in its entry ("enabled").</summary>
        bool Enabled { get; }

        /// <summary>Most slot-games that may violate for the profile to pass, as parts per 10 000 of the judged slot-games.</summary>
        int MaxViolationPt { get; }

        /// <summary>The bar of the metric, printed as bar=; a slot-game's metric above it violates. Unit: as the metric.</summary>
        string BarText { get; }

        /// <summary>Names of the extra counters of <see cref="BehaviourResult.Extras"/>, in order; empty for none.</summary>
        IReadOnlyList<string> ExtraNames { get; }

        /// <summary>True when the runner also prints one BEHAVIOUR_BY_OPENING line per (faction, opening) after the check's BEHAVIOUR line (the print_by_opening key; ai_economy_calc.md 3.1). Default false for a check without the key.</summary>
        bool PrintByOpening => false;
    }

    /// <summary>The registry of behaviour checks, the only place a check is declared.</summary>
    public static class BehaviourChecks
    {
        /// <summary>The registered check IDs in report order, each with the factory that makes a fresh instance.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>> Registry = new List<KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>>
        {
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(StaticDefenceShare.CheckId, c => new StaticDefenceShare(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(HqMoveNewIncome.CheckId, c => new HqMoveNewIncome(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(WorkersAt.CheckId, c => new WorkersAt(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(FirstDropOff.CheckId, c => new FirstDropOff(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(IncomeBy.CheckId, c => new IncomeBy(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(EconGap.CheckId, c => new EconGap(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(DepotLossesYoung.CheckId, c => new DepotLossesYoung(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(HarassTrade.CheckId, c => new HarassTrade(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(HarassSquads.CheckId, c => new HarassSquads(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(Won.CheckId, c => new Won(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(WorkerLossesPreOverrun.CheckId, c => new WorkerLossesPreOverrun(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(ArmySupplyRatioAt.CheckId, c => new ArmySupplyRatioAt(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(TrainedUnit.CheckId, c => new TrainedUnit(c)),
        };

        /// <summary>True when <paramref name="id"/> is a registered check.</summary>
        /// <param name="id">Check ID.</param>
        public static bool IsRegistered(string id)
        {
            foreach (KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>> pair in Registry)
            {
                if (pair.Key == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A fresh instance of the check with ID <paramref name="id"/>; throws <see cref="HarnessException"/> when it is not registered.</summary>
        /// <param name="id">Check ID.</param>
        /// <param name="context">The shared numbers.</param>
        public static IBehaviourCheck Create(string id, BehaviourContext context)
        {
            foreach (KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>> pair in Registry)
            {
                if (pair.Key == id)
                {
                    return pair.Value(context);
                }
            }
            throw new HarnessException("unknown behaviour check '" + id + "'");
        }

        /// <summary>A fraction member with at most 4 decimals as parts per 10 000, range-checked.</summary>
        internal static int Pt(JsonValue entry, string key, string min, string max)
        {
            return PredictSettings.ReadPt(entry, key, min, max);
        }

        /// <summary>
        /// M.4.3: the share of a slot-game's spending that went to static defence, (static_defence common + w x rare) / (spent common + w x rare) with w = rare_weight; held when the share is at most max_share
        /// (edge inclusive, integers); a game that spent nothing has share 0 and holds; never unjudged.
        /// </summary>
        private sealed class StaticDefenceShare : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "static_defence_share";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Largest allowed share. Unit: parts per 10 000. Range: 0 to 10000.</summary>
            private int _maxSharePt;

            /// <summary>Bundles the context.</summary>
            public StaticDefenceShare(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => PredictEvaluator.Ratio3(_maxSharePt, 10000);

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "max_share", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                _maxSharePt = Pt(entry, "max_share", "0", "1");
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                // Static defence spending and all spending in the weighted unit.
                long numerator = 0;
                if (slot.Spent.TryGetValue(PlayerLedger.ClassStaticDefence, out int[]? split))
                {
                    numerator = _context.Weighted(split[0], split[1]);
                }
                long denominator = _context.Weighted(slot.SpentCommon, slot.SpentRare);
                if (denominator <= 0)
                {
                    return new BehaviourResult(CheckOutcome.Held, 0, 1);
                }
                return new BehaviourResult(numerator * 10000 <= _maxSharePt * denominator ? CheckOutcome.Held : CheckOutcome.Violated, numerator, denominator);
            }
        }

        /// <summary>
        /// M.4.4: no HQ move without new income. For each move that rooted (root tick at least 0) and was at least min_distance_tiles long: before = income over the window_buckets buckets before the uproot bucket,
        /// after = income over window_buckets buckets starting settle_buckets after the root bucket. A move is unjudged when fewer than window_buckets buckets precede the uproot or the after window runs past the last
        /// bucket, and violates when after x 10 000 is below before x (10 000 + min_gain). A slot-game is Violated when a judged move violates, Unjudged when it has moves and none is judged, else Held. Metric: violating moves.
        /// </summary>
        private sealed class HqMoveNewIncome : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "hq_move_new_income";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Shortest move that counts. Unit: tenths of a tile. Range: 0 to 2000.</summary>
            private int _minDistanceTenths;

            /// <summary>Buckets in each window. Unit: buckets. Range: 1 to 20.</summary>
            private int _window;

            /// <summary>Buckets skipped after the root. Unit: buckets. Range: 0 to 20.</summary>
            private int _settle;

            /// <summary>Smallest gain. Unit: parts per 10 000. Range: 0 to 100000.</summary>
            private int _minGainPt;

            /// <summary>Bundles the context.</summary>
            public HqMoveNewIncome(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => "0.000";

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames { get; } = new[] { "moves", "violating_moves", "unjudged_moves" };

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "min_distance_tiles", "window_buckets", "settle_buckets", "min_gain_fraction", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                _minDistanceTenths = entry.RequireInt("min_distance_tiles", 0, 200) * 10;
                _window = entry.RequireInt("window_buckets", 1, 20);
                _settle = entry.RequireInt("settle_buckets", 0, 20);
                _minGainPt = Pt(entry, "min_gain_fraction", "0", "10");
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <summary>Income of buckets [first, first + count) in the weighted unit.</summary>
            private long Income(GameSummary slot, int first, int count)
            {
                long sum = 0;
                for (int b = first; b < first + count; b++)
                {
                    sum += _context.Weighted(slot.Income[b][0], slot.Income[b][1]);
                }
                return sum;
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                // Moves that count (rooted and long enough), judged ones, violating ones.
                long moves = 0, judged = 0, violating = 0;
                foreach (int[] move in slot.HqMoves)
                {
                    if (move[1] < 0 || move[2] < _minDistanceTenths)
                    {
                        continue;
                    }
                    moves++;
                    // Bucket of the uproot and of the root. Unit: buckets. Range: 0 upward.
                    int u = move[0] / _context.BucketTicks;
                    int t = move[1] / _context.BucketTicks;
                    if (u < _window || t + _settle + _window > slot.Income.Count)
                    {
                        continue;
                    }
                    judged++;
                    long before = Income(slot, u - _window, _window);
                    long after = Income(slot, t + _settle, _window);
                    if (after * 10000 < before * (10000 + _minGainPt))
                    {
                        violating++;
                    }
                }
                long unjudged = moves - judged;
                CheckOutcome outcome = violating > 0 ? CheckOutcome.Violated : moves > 0 && judged == 0 ? CheckOutcome.Unjudged : CheckOutcome.Held;
                return new BehaviourResult(outcome, violating, 1, new[] { moves, violating, unjudged });
            }
        }

        /// <summary>
        /// Package B (ai_budget_queues.md B.4.1): own workers at one bank time. Metric = the workers the summary recorded at at_seconds; Unjudged when the game ended before it (or the record has no workers field);
        /// Held when at least min_workers, else Violated. at_seconds must be one of summary_bank_times_seconds (checked when the context knows them).
        /// </summary>
        private sealed class WorkersAt : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "workers_at";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Index of at_seconds in the bank times. Unit: index. Range: 0 to 7; 0 when the bank times are not known.</summary>
            private int _index;

            /// <summary>Fewest workers that hold. Unit: workers. Range: 0 to 200.</summary>
            private int _minWorkers;

            /// <summary>Bundles the context.</summary>
            public WorkersAt(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => PredictEvaluator.Ratio3(_minWorkers, 1);

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "at_seconds", "min_workers", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                JsonValue at = entry.Require("at_seconds");
                // Seconds of the snapshot. Unit: seconds. Range: 1 to 7200.
                int seconds = at.AsInt(1, 7200);
                if (_context.BankSeconds.Count > 0)
                {
                    _index = -1;
                    for (int i = 0; i < _context.BankSeconds.Count; i++)
                    {
                        if (_context.BankSeconds[i] == seconds)
                        {
                            _index = i;
                        }
                    }
                    if (_index < 0)
                    {
                        throw at.Fail("must be one of summary_bank_times_seconds (found " + seconds.ToString(CultureInfo.InvariantCulture) + ")");
                    }
                }
                _minWorkers = entry.RequireInt("min_workers", 0, 200);
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                if (_index >= slot.Workers.Count || slot.Workers[_index] == null)
                {
                    return new BehaviourResult(CheckOutcome.Unjudged, 0, 1);
                }
                // Workers alive at the snapshot. Unit: workers. Range: 0 upward.
                int workers = slot.Workers[_index]!.Value;
                return new BehaviourResult(workers >= _minWorkers ? CheckOutcome.Held : CheckOutcome.Violated, workers, 1);
            }
        }

        /// <summary>
        /// Package B (ai_budget_queues.md B.4.1): when the first own non-HQ drop-off completed. Metric = seconds of the first entry of drop_off_ticks (integer division by the ticks per second);
        /// Unjudged when there is none; Held when at most max_seconds.
        /// </summary>
        private sealed class FirstDropOff : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "first_drop_off";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Latest second that holds. Unit: seconds. Range: 0 to 7200.</summary>
            private int _maxSeconds;

            /// <summary>Bundles the context.</summary>
            public FirstDropOff(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => PredictEvaluator.Ratio3(_maxSeconds, 1);

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "max_seconds", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                _maxSeconds = entry.RequireInt("max_seconds", 0, 7200);
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                if (slot.DropOffTicks.Count == 0)
                {
                    return new BehaviourResult(CheckOutcome.Unjudged, 0, 1);
                }
                // Second of the first drop-off, integer division. Unit: seconds. Range: 0 upward.
                int seconds = slot.DropOffTicks[0] / _context.TicksPerSecond;
                return new BehaviourResult(seconds <= _maxSeconds ? CheckOutcome.Held : CheckOutcome.Violated, seconds, 1);
            }
        }

        /// <summary>
        /// Package E0 (ai_economy_calc.md 3.1): income by a time. Metric = common plus rare_weight x rare deposited in the buckets that end at or before at_seconds; Unjudged when the game ended before at_seconds;
        /// Held when the metric is at least min_income. at_seconds must be a multiple of the bucket length (checked when the context knows it).
        /// </summary>
        private sealed class IncomeBy : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "income_by";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Second the income is summed up to. Unit: seconds. Range: 1 to 7200, a multiple of the bucket length.</summary>
            private int _atSeconds;

            /// <summary>Least income that holds. Unit: resource units. Range: 0 to 1000000.</summary>
            private int _minIncome;

            /// <summary>Bundles the context.</summary>
            public IncomeBy(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public bool PrintByOpening { get; private set; }

            /// <inheritdoc />
            public string BarText => PredictEvaluator.Ratio3(_minIncome, 1);

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "at_seconds", "min_income", "max_violation_fraction", "print_by_opening", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                // The at_seconds entry (kept to name the key in a settings error). Unit: seconds. Range: 1 to 7200.
                JsonValue at = entry.Require("at_seconds");
                _atSeconds = at.AsInt(1, 7200);
                // A bucket length of 1 tick means the context does not know it (settings validation without a run).
                if (_context.BucketTicks > 1 && (_atSeconds * _context.TicksPerSecond) % _context.BucketTicks != 0)
                {
                    throw at.Fail("must be a multiple of summary_bucket_seconds (found " + _atSeconds.ToString(CultureInfo.InvariantCulture) + ")");
                }
                _minIncome = entry.RequireInt("min_income", 0, 1000000);
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
                PrintByOpening = entry.RequireBool("print_by_opening");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                // Tick at which at_seconds ends, and the buckets that end at or before it. Unit: ticks, buckets. Range: 1 upward.
                int atTicks = _atSeconds * _context.TicksPerSecond;
                int buckets = atTicks / _context.BucketTicks;
                if (game.EndTick + 1 < atTicks || slot.Income.Count < buckets)
                {
                    return new BehaviourResult(CheckOutcome.Unjudged, 0, 1);
                }
                // Income of those buckets in the weighted unit (parts per 10 000 of a common unit).
                long weighted = 0;
                for (int b = 0; b < buckets; b++)
                {
                    weighted += _context.Weighted(slot.Income[b][0], slot.Income[b][1]);
                }
                return new BehaviourResult(weighted >= (long)_minIncome * 10000 ? CheckOutcome.Held : CheckOutcome.Violated, weighted, 10000);
            }
        }

        /// <summary>
        /// Package S-M (ai_threat_map.md 7.2): own drop-offs lost young. Metric = the entries of drop_off_life whose death_tick is at least 0, at most young_seconds after placed_tick, not after the summary window and
        /// before the slot's overrun_second when it has one. Always Held (max_violation_fraction 1; the bar is the paired test); never Unjudged.
        /// </summary>
        private sealed class DepotLossesYoung : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "depot_losses_young";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Longest life that counts as young. Unit: seconds. Range: 1 to 1800.</summary>
            private int _youngSeconds;

            /// <summary>Bundles the context.</summary>
            public DepotLossesYoung(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => "none";

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "young_seconds", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                _youngSeconds = entry.RequireInt("young_seconds", 1, 1800);
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                // Longest young life, the window end and the overrun tick. Unit: ticks.
                long youngTicks = (long)_youngSeconds * _context.TicksPerSecond;
                int? overrunSecond = slot.Production?.OverrunSecond;
                long overrunTick = overrunSecond.HasValue ? (long)overrunSecond.Value * _context.TicksPerSecond : long.MaxValue;
                // Young drop-offs lost. Unit: structures. Range: 0 upward.
                long lost = 0;
                foreach (int[] life in slot.DropOffLife)
                {
                    if (life[1] >= 0 && life[1] - life[0] <= youngTicks && life[1] <= _context.WindowTicks && life[1] < overrunTick)
                    {
                        lost++;
                    }
                }
                return new BehaviourResult(CheckOutcome.Held, lost, 1);
            }
        }

        /// <summary>Package S-M (ai_threat_map.md 7.2): value killed less value lost by the slot's harass squad units (may be negative). Always Held; never Unjudged.</summary>
        private sealed class HarassTrade : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "harass_trade";

            /// <summary>Bundles the context (unused: the check reads only the summary).</summary>
            public HarassTrade(BehaviourContext context)
            {
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => "none";

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                return new BehaviourResult(CheckOutcome.Held, slot.Harass.KillsValue - slot.Harass.LossesValue, 1);
            }
        }

        /// <summary>Package S-M (ai_threat_map.md 7.2): harass squads started by the slot. Always Held; never Unjudged.</summary>
        private sealed class HarassSquads : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "harass_squads";

            /// <summary>Bundles the context (unused: the check reads only the summary).</summary>
            public HarassSquads(BehaviourContext context)
            {
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => "none";

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                return new BehaviourResult(CheckOutcome.Held, slot.Harass.Squads, 1);
            }
        }

        /// <summary>Package RR-M (ai_reef_runner_defence.md 4.2): 1 / 1 when the game's result names this slot, else 0 / 1 (a draw or an undecided game is 0). Always Held; never Unjudged.</summary>
        private sealed class Won : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "won";

            /// <summary>Bundles the context.</summary>
            public Won(BehaviourContext context)
            {
                // The context is not needed (the check reads the record).
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => "none";

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                // Index of this slot in the record, found by reference. Range: 0, 1 or -1 (not a slot of the record).
                int index = ReferenceEquals(slot, game.Summary0) ? 0 : ReferenceEquals(slot, game.Summary1) ? 1 : -1;
                // True when the result names this slot. Range: true or false.
                bool won = (index == 0 && game.Result == GameRecord.Slot0) || (index == 1 && game.Result == GameRecord.Slot1);
                return new BehaviourResult(CheckOutcome.Held, won ? 1 : 0, 1);
            }
        }

        /// <summary>Package RR-M (ai_reef_runner_defence.md 4.2): own workers killed before the slot's overrun second (ProductionMetrics.WorkerLossesPreOverrun). Unjudged when the summary has no production metrics; otherwise always Held.</summary>
        private sealed class WorkerLossesPreOverrun : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "worker_losses_pre_overrun";

            /// <summary>Bundles the context.</summary>
            public WorkerLossesPreOverrun(BehaviourContext context)
            {
                // The context is not needed (the check reads the summary).
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => "none";

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                return slot.Production == null ? new BehaviourResult(CheckOutcome.Unjudged, 0, 1) : new BehaviourResult(CheckOutcome.Held, slot.Production.WorkerLossesPreOverrun, 1);
            }
        }

        /// <summary>Package RR-M (ai_reef_runner_defence.md 4.2): own army supply over the enemy's (at least 1) at the sample second at_seconds. Unjudged when either sample is null (the game ended earlier) or either summary has no production metrics; otherwise always Held.</summary>
        private sealed class ArmySupplyRatioAt : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "army_supply_ratio_at";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Index of at_seconds in the sample seconds. Unit: index. Range: 0 to 7; 0 when the sample seconds are not known.</summary>
            private int _index;

            /// <summary>Bundles the context.</summary>
            public ArmySupplyRatioAt(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => "none";

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "at_seconds", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                // The at_seconds entry (kept to name the key in an error). Unit: seconds. Range: 1 to 7200.
                JsonValue at = entry.Require("at_seconds");
                // Seconds of the sample. Unit: seconds. Range: 1 to 7200.
                int seconds = at.AsInt(1, 7200);
                if (_context.SampleSeconds.Count > 0)
                {
                    _index = -1;
                    for (int i = 0; i < _context.SampleSeconds.Count; i++)
                    {
                        if (_context.SampleSeconds[i] == seconds)
                        {
                            _index = i;
                        }
                    }
                    if (_index < 0)
                    {
                        throw at.Fail("must be one of summary_sample_seconds (found " + seconds.ToString(CultureInfo.InvariantCulture) + ")");
                    }
                }
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                // The enemy's summary: the other slot of the record, found by reference. Null when this slot is not in the record.
                GameSummary? enemy = ReferenceEquals(slot, game.Summary0) ? game.Summary1 : ReferenceEquals(slot, game.Summary1) ? game.Summary0 : null;
                if (slot.Production == null || enemy?.Production == null || _index >= slot.Production.ArmySupply.Count || _index >= enemy.Production.ArmySupply.Count
                    || slot.Production.ArmySupply[_index] == null || enemy.Production.ArmySupply[_index] == null)
                {
                    return new BehaviourResult(CheckOutcome.Unjudged, 0, 1);
                }
                // Enemy army supply, at least 1 so the ratio is defined. Unit: supply. Range: 1 upward.
                int enemySupply = Math.Max(1, enemy.Production.ArmySupply[_index]!.Value);
                return new BehaviourResult(CheckOutcome.Held, slot.Production.ArmySupply[_index]!.Value, enemySupply);
            }
        }

        /// <summary>Package RR-M (ai_reef_runner_defence.md 4.2): how many units of the unit ID the slot trained over the whole game (GameSummary.Trained; 0 when absent). Always Held; never Unjudged.</summary>
        private sealed class TrainedUnit : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "trained_unit";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Unit ID counted. Range: a unit of the data set (checked when the context knows the data).</summary>
            private string _unit = string.Empty;

            /// <summary>Bundles the context.</summary>
            public TrainedUnit(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public string BarText => "none";

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames => Array.Empty<string>();

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "unit", "max_violation_fraction", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                // The unit entry (kept to name the key in an error).
                JsonValue unit = entry.Require("unit");
                _unit = unit.AsString();
                if (_context.Data != null && !_context.Data.Content.Units.ContainsKey(_unit))
                {
                    throw unit.Fail("must be a unit ID of the data set (found \"" + _unit + "\")");
                }
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                return new BehaviourResult(CheckOutcome.Held, slot.Trained.TryGetValue(_unit, out int count) ? count : 0, 1);
            }
        }

        /// <summary>
        /// Package E-OPT (ai_economy_calc.md 3.2): how far the AI's economic choices are from the best schedule in an integer economy model. Per slot-game it builds the model of that player on that map from the data
        /// set and the profile's faction plan, searches the best worker and depot schedule up to at_seconds (OPT), replays the AI's own workers per bucket and drop-off times (MOD) and reads the AI's deposits (REAL).
        /// Metric = decision gap, 1000 x (OPT - MOD) / OPT floored (permille, may be negative: the AI is not held to the grid); extras are the sums of the total gap 1000 x (OPT - REAL) / OPT and the model error
        /// 1000 x (MOD - REAL) / MOD over the judged slot-games and their count. Unjudged when the game ended before at_seconds, a summary field is missing, or OPT or MOD is 0. Results are cached per slot-game.
        /// </summary>
        private sealed class EconGap : IBehaviourCheck
        {
            /// <summary>Registry ID.</summary>
            public const string CheckId = "econ_gap";

            /// <summary>The shared numbers.</summary>
            private readonly BehaviourContext _context;

            /// <summary>Second the income is compared up to. Unit: seconds. Range: 1 to 7200, a multiple of the bucket length.</summary>
            private int _atSeconds;

            /// <summary>Smallest worker cap N searched. Unit: workers. Range: 1 to 200, at most the largest.</summary>
            private int _capMin;

            /// <summary>Largest worker cap N searched. Unit: workers. Range: 1 to 200.</summary>
            private int _capMax;

            /// <summary>Step of the worker cap. Unit: workers. Range: 1 to 200.</summary>
            private int _capStep;

            /// <summary>Smallest depot trigger searched. Unit: workers. Range: 1 to 200, at most the largest.</summary>
            private int _triggerMin;

            /// <summary>Largest depot trigger searched. Unit: workers. Range: 1 to 200.</summary>
            private int _triggerMax;

            /// <summary>Step of the depot trigger. Unit: workers. Range: 1 to 200.</summary>
            private int _triggerStep;

            /// <summary>Most depots in a searched schedule. Unit: depots. Range: 0 to 4.</summary>
            private int _maxDepots;

            /// <summary>Largest decision gap that holds. Unit: permille. Range: 0 to 1000.</summary>
            private int _maxGapPermille;

            /// <summary>Results already computed, by slot-game (map, matchup, game, seed, slot, profile). Not shared between check instances.</summary>
            private readonly Dictionary<string, BehaviourResult> _cache = new Dictionary<string, BehaviourResult>(StringComparer.Ordinal);

            /// <summary>AI profiles already loaded, by profile ID.</summary>
            private readonly Dictionary<string, Rts.Ai.AiProfile> _profiles = new Dictionary<string, Rts.Ai.AiProfile>(StringComparer.Ordinal);

            /// <summary>Bundles the context.</summary>
            public EconGap(BehaviourContext context)
            {
                _context = context;
            }

            /// <inheritdoc />
            public string Id => CheckId;

            /// <inheritdoc />
            public bool Enabled { get; private set; }

            /// <inheritdoc />
            public int MaxViolationPt { get; private set; }

            /// <inheritdoc />
            public bool PrintByOpening { get; private set; }

            /// <inheritdoc />
            public string BarText => PredictEvaluator.Ratio3(_maxGapPermille, 1);

            /// <inheritdoc />
            public IReadOnlyList<string> ExtraNames { get; } = new[] { "total_gap_sum", "model_error_sum", "gap_judged" };

            /// <inheritdoc />
            public void ReadSettings(JsonValue entry)
            {
                entry.RejectUnknownKeys("enabled", "at_seconds", "worker_cap_min", "worker_cap_max", "worker_cap_step", "depot_trigger_min", "depot_trigger_max", "depot_trigger_step", "max_depots",
                    "max_gap_permille", "max_violation_fraction", "print_by_opening", "paired_direction");
                Enabled = entry.RequireBool("enabled");
                // The at_seconds entry (kept to name the key in a settings error). Unit: seconds. Range: 1 to 7200.
                JsonValue at = entry.Require("at_seconds");
                _atSeconds = at.AsInt(1, 7200);
                if (_context.BucketTicks > 1 && (_atSeconds * _context.TicksPerSecond) % _context.BucketTicks != 0)
                {
                    throw at.Fail("must be a multiple of summary_bucket_seconds (found " + _atSeconds.ToString(CultureInfo.InvariantCulture) + ")");
                }
                // The worker_cap_min entry (kept to name the key in a settings error). Unit: workers. Range: 1 to 200.
                JsonValue capMin = entry.Require("worker_cap_min");
                _capMin = capMin.AsInt(1, 200);
                _capMax = entry.RequireInt("worker_cap_max", 1, 200);
                _capStep = entry.RequireInt("worker_cap_step", 1, 200);
                if (_capMin > _capMax)
                {
                    throw capMin.Fail("must be at most worker_cap_max (found " + _capMin.ToString(CultureInfo.InvariantCulture) + " above " + _capMax.ToString(CultureInfo.InvariantCulture) + ")");
                }
                // The depot_trigger_min entry (kept to name the key in a settings error). Unit: workers. Range: 1 to 200.
                JsonValue triggerMin = entry.Require("depot_trigger_min");
                _triggerMin = triggerMin.AsInt(1, 200);
                _triggerMax = entry.RequireInt("depot_trigger_max", 1, 200);
                _triggerStep = entry.RequireInt("depot_trigger_step", 1, 200);
                if (_triggerMin > _triggerMax)
                {
                    throw triggerMin.Fail("must be at most depot_trigger_max (found " + _triggerMin.ToString(CultureInfo.InvariantCulture) + " above " + _triggerMax.ToString(CultureInfo.InvariantCulture) + ")");
                }
                _maxDepots = entry.RequireInt("max_depots", 0, 4);
                _maxGapPermille = entry.RequireInt("max_gap_permille", 0, 1000);
                MaxViolationPt = Pt(entry, "max_violation_fraction", "0", "1");
                PrintByOpening = entry.RequireBool("print_by_opening");
            }

            /// <inheritdoc />
            public BehaviourResult Evaluate(GameSummary slot, GameRecord game)
            {
                // Slot of this summary (0 unless it is the record's second summary). Range: 0 or 1.
                int slotIndex = ReferenceEquals(slot, game.Summary1) ? 1 : 0;
                // Cache key of this slot-game.
                string key = game.Map + "|" + game.Matchup + "|" + game.Game.ToString(CultureInfo.InvariantCulture) + "|" + game.Seed.ToString(CultureInfo.InvariantCulture) + "|" + slotIndex.ToString(CultureInfo.InvariantCulture)
                    + "|" + (slotIndex == 0 ? game.Slot0Profile : game.Slot1Profile);
                if (_cache.TryGetValue(key, out BehaviourResult cached))
                {
                    return cached;
                }
                BehaviourResult result = Compute(slot, game, slotIndex);
                _cache[key] = result;
                return result;
            }

            /// <summary>Builds the model, searches, replays and compares (the uncached part of <see cref="Evaluate"/>).</summary>
            /// <param name="slot">The slot's summary.</param>
            /// <param name="game">The game record.</param>
            /// <param name="slotIndex">Slot of the summary. Range: 0 or 1.</param>
            private BehaviourResult Compute(GameSummary slot, GameRecord game, int slotIndex)
            {
                // Result for a slot-game with nothing to judge.
                BehaviourResult unjudged = new BehaviourResult(CheckOutcome.Unjudged, 0, 1);
                // Tick at which at_seconds ends, and the buckets that end at or before it. Unit: ticks, buckets. Range: 1 upward.
                int atTicks = _atSeconds * _context.TicksPerSecond;
                int buckets = atTicks / _context.BucketTicks;
                if (game.EndTick + 1 < atTicks || slot.Income.Count < buckets || slot.WorkersByBucket.Count < buckets - 1 || slot.SpentOtherByBucket.Count < buckets)
                {
                    return unjudged;
                }
                DataSet data = _context.Data ?? throw new HarnessException("the econ_gap check needs the data set (BehaviourContext.Data)");
                // Faction and profile of the slot.
                string factionId = slotIndex == 0 ? game.Slot0Faction : game.Slot1Faction;
                string profileId = slotIndex == 0 ? game.Slot0Profile : game.Slot1Profile;
                if (!_profiles.TryGetValue(profileId, out Rts.Ai.AiProfile? profile))
                {
                    profile = Rts.Ai.AiProfile.FromDataSet(data, profileId);
                    _profiles[profileId] = profile;
                }
                // The profile's plan for the slot's faction, the map, and the definitions the model reads.
                Rts.Ai.AiFactionPlan plan = profile.RequireFaction(factionId);
                MapDefinition map = data.RequireMap(game.Map);
                UnitDefinition worker = data.Content.Units[plan.WorkerUnitId];
                UnitDefinition hq = data.Content.Units[data.Content.Factions[factionId].HqUnitId!];
                // The plan's depot, or null when it builds none.
                UnitDefinition? depot = plan.DropOffBuildingId.Length > 0 ? data.Content.Units[plan.DropOffBuildingId] : null;
                // Clock that converts seconds to ticks as the match does.
                MatchClock clock = new MatchClock(_context.TicksPerSecond);
                // The map's fields as the model reads them: the tile centre, the kind and the start amount. Unit: tiles, kind, resource units.
                List<ModelField> fields = new List<ModelField>();
                foreach (ResourceField field in map.ResourceFields)
                {
                    fields.Add(new ModelField(new FixVec2(Fix64.FromInt(field.X) + Fix64.Half, Fix64.FromInt(field.Y) + Fix64.Half), field.Kind == ResourceKind.Rare, field.Amount));
                }
                // What the game spent in each bucket outside workers and drop-offs, common plus rare. Unit: resource units.
                List<long> other = new List<long>();
                foreach (int[] pair in slot.SpentOtherByBucket)
                {
                    other.Add((long)pair[0] + pair[1]);
                }
                ModelInputs inputs = new ModelInputs
                {
                    TicksPerSecond = _context.TicksPerSecond,
                    BucketSeconds = _context.BucketTicks / _context.TicksPerSecond,
                    Fields = fields,
                    Spawn = map.Spawns[slotIndex],
                    WorkerSpeed = worker.SpeedTilesPerSecond,
                    WorkerCost = worker.CostCommon + worker.CostRare,
                    WorkerBuildTicks = Math.Max(1, clock.SecondsToTicks(worker.Economy.BuildSeconds)),
                    HasDepot = depot != null,
                    DepotCost = depot == null ? 0 : depot.CostCommon + depot.CostRare,
                    DepotBuildTicks = depot == null ? 1 : Math.Max(1, clock.SecondsToTicks(depot.Economy.BuildSeconds)),
                    DepotRadius = depot == null ? Fix64.Zero : depot.Radius,
                    HqRadius = hq.Radius,
                    AmountPerTrip = data.Tuning.Economy.GatherAmountPerTrip,
                    HoldTicks = data.Tuning.Economy.GatherTicksPerTrip(clock),
                    SlotsCommon = data.Tuning.Economy.GatherSlotsCommon,
                    SlotsRare = data.Tuning.Economy.GatherSlotsRare,
                    Interact = data.Tuning.Economy.InteractRangeTiles,
                    PlacementTicks = data.Tuning.Economy.WorkerPlacementTicks(clock),
                    StartingBank = data.Tuning.Economy.StartingCommon,
                    StartingWorkers = data.Tuning.Economy.StartingWorkerCount,
                    Reach = profile.FieldMaxDistanceTiles,
                    RingMin = profile.DepotRingMinTiles,
                    OtherSpending = other,
                };
                EconomyModel model = new EconomyModel(inputs);
                // The best schedule's income, the AI schedule's income in the model, and the AI's real deposits. Unit: resource units. Range: 0 upward.
                long opt = model.Search(_atSeconds, _capMin, _capMax, _capStep, _triggerMin, _triggerMax, _triggerStep, _maxDepots).Income;
                long mod = model.Replay(_atSeconds, slot.WorkersByBucket, slot.DropOffTicks);
                long real = 0;
                for (int b = 0; b < buckets; b++)
                {
                    real += (long)slot.Income[b][0] + slot.Income[b][1];
                }
                if (opt <= 0 || mod <= 0)
                {
                    return unjudged;
                }
                // The three gaps. Unit: permille. Range: any (negative when the AI beats the grid or the model).
                long decision = FloorDiv(1000 * (opt - mod), opt);
                long total = FloorDiv(1000 * (opt - real), opt);
                long error = FloorDiv(1000 * (mod - real), mod);
                return new BehaviourResult(decision <= _maxGapPermille ? CheckOutcome.Held : CheckOutcome.Violated, decision, 1, new[] { total, error, 1L });
            }

            /// <summary>Integer division rounded toward negative infinity. Range: any.</summary>
            /// <param name="num">Numerator. Range: any.</param>
            /// <param name="den">Denominator. Range: above 0.</param>
            private static long FloorDiv(long num, long den)
            {
                // Truncated quotient. Range: any.
                long quotient = num / den;
                return num % den < 0 ? quotient - 1 : quotient;
            }
        }
    }
}

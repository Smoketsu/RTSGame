// rts/tools/BalanceHarness/BehaviourChecks.cs
// The behaviour checks (spec: rts/docs/specs/balance_measurement.md M.4): a check is a named predicate over one slot's game summary (and its game record), registered by ID in <see cref="BehaviourChecks"/> (the only
// composition root for checks, as BuiltInMechanics is for mechanics) and declared with its bar values in rts/data/scripts/ai_eval/behaviour_checks.json. A later package adds a check by adding one registry entry and
// one data entry. Every bar comparison is integer arithmetic (fractions are parts per 10 000, "pt"), so a verdict is exact and platform independent. Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Data;

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

        /// <summary>Bundles the values.</summary>
        public BehaviourContext(int rareWeightPt, int bucketTicks)
        {
            RareWeightPt = rareWeightPt;
            BucketTicks = bucketTicks;
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
    }

    /// <summary>The registry of behaviour checks, the only place a check is declared.</summary>
    public static class BehaviourChecks
    {
        /// <summary>The registered check IDs in report order, each with the factory that makes a fresh instance.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>> Registry = new List<KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>>
        {
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(StaticDefenceShare.CheckId, c => new StaticDefenceShare(c)),
            new KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>>(HqMoveNewIncome.CheckId, c => new HqMoveNewIncome(c)),
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
                entry.RejectUnknownKeys("enabled", "max_share", "max_violation_fraction");
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
                entry.RejectUnknownKeys("enabled", "min_distance_tiles", "window_buckets", "settle_buckets", "min_gain_fraction", "max_violation_fraction");
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
    }
}

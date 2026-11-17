// rts/tools/BalanceHarness/ResponseChecks.cs
// The five response checks of the AI evaluation (spec: rts/docs/specs/skirmish_ai_v3.md A.4), one pure method per kind: each takes the recorded ResponseSeries, the check's
// parameters from ai_eval.json and, where it needs unit data, the data set and the profile of the AI under test, and returns the numbered parts that passed or failed with
// the measured values. A check passes when every part passes; the parts are reported separately because the spec names the first package that must pass each part.
// Harness-only (double is allowed); no simulation access. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Ai;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>One numbered part of a check.</summary>
    public sealed class CheckPart
    {
        /// <summary>Part number as in the spec (1, 2, ...). Range: 1 upward.</summary>
        public int Number { get; }

        /// <summary>True when the part passed.</summary>
        public bool Passed { get; }

        /// <summary>Measured value text, "name=value" (for example "aa_units=0").</summary>
        public string Metric { get; }

        /// <summary>Bundles the values.</summary>
        public CheckPart(int number, bool passed, string metric)
        {
            Number = number;
            Passed = passed;
            Metric = metric;
        }
    }

    /// <summary>The check methods.</summary>
    public static class ResponseChecks
    {
        /// <summary>Evaluates the check named by <paramref name="test"/>.</summary>
        /// <param name="test">The scenario entry.</param>
        /// <param name="series">What the run recorded.</param>
        /// <param name="data">The data set (unit definitions and combat rules).</param>
        /// <param name="profile">The profile of the AI under test and the faction it played (for the base composition).</param>
        /// <param name="faction">Faction ID of the AI under test.</param>
        public static List<CheckPart> Evaluate(ResponseTest test, ResponseSeries series, DataSet data, AiProfile profile, string faction)
        {
            switch (test.Check)
            {
                case "air_answer":
                    return AirAnswer(test, series);
                case "armoured_answer":
                    return ArmouredAnswer(test, series, data, profile, faction);
                case "trade":
                    return Trade(test, series);
                case "raid":
                    return Raid(test, series);
                case "turtle":
                    return Turtle(test, series);
                default:
                    throw new HarnessException("unknown check kind '" + test.Check + "'");
            }
        }

        /// <summary>
        /// air_answer: (1) the enemy model knew of air within known_within_seconds of the first visible enemy air unit; (2) at by_seconds the AI owns at least min_aa_units complete
        /// air-capable non-worker non-HQ units (static defences included); (3) its HQ stands at survive_seconds.
        /// </summary>
        public static List<CheckPart> AirAnswer(ResponseTest test, ResponseSeries series)
        {
            // Window the model has to react in. Unit: ticks.
            int window = (int)test.Number("known_within_seconds") * series.TicksPerSecond;
            bool known = series.FirstEnemyAirVisibleTick >= 0 && series.ModelAirKnownTick >= 0 && series.ModelAirKnownTick <= series.FirstEnemyAirVisibleTick + window;
            // Air-capable units at by_seconds.
            ResponseSample? sample = series.SampleAt((int)test.Number("by_seconds"));
            int aa = sample == null ? 0 : sample.AaUnits;
            return new List<CheckPart>
            {
                new CheckPart(1, known, "air_first_seen_s=" + Seconds(series.FirstEnemyAirVisibleTick, series) + " model_air_known_s="
                    + (series.ModelAirKnownTick < 0 ? "none" : Seconds(series.ModelAirKnownTick, series))),
                new CheckPart(2, aa >= (int)test.Number("min_aa_units"), "aa_units=" + Int(aa)),
                new CheckPart(3, series.HqAliveAt((int)test.Number("survive_seconds")), "hq_alive=" + series.HqAliveAt((int)test.Number("survive_seconds")).ToString().ToLowerInvariant()),
            };
        }

        /// <summary>
        /// armoured_answer: (1) the share of answer units in the army supply trained between first sighting + delay_seconds and until_seconds, minus the same share of the plan's
        /// base composition, is at least min_share_gain; (2) the HQ stands at survive_seconds. An answer unit has the highest damage multiplier against the most-supplied seen enemy
        /// armour type in the faction's pool, or no seen enemy type can target its layer.
        /// </summary>
        public static List<CheckPart> ArmouredAnswer(ResponseTest test, ResponseSeries series, DataSet data, AiProfile profile, string faction)
        {
            int survive = (int)test.Number("survive_seconds");
            List<UnitDefinition> pool = Pool(data, faction);
            // Most-supplied seen armour type (ties: first in ordinal order), or empty when nothing was seen.
            string armour = string.Empty;
            int best = 0;
            foreach (KeyValuePair<string, int> pair in series.SeenArmourSupply)
            {
                if (pair.Value > best)
                {
                    best = pair.Value;
                    armour = pair.Key;
                }
            }
            // Highest multiplier in the pool against that armour type. Unit: ratio.
            double top = 0;
            foreach (UnitDefinition unit in pool)
            {
                top = Math.Max(top, Multiplier(data, unit, armour));
            }
            SortedSet<string> answers = new SortedSet<string>(StringComparer.Ordinal);
            foreach (UnitDefinition unit in pool)
            {
                if (armour.Length > 0 && (Multiplier(data, unit, armour) >= top - 1e-9 && top > 0 || !AnySeenCanTarget(data, series, unit)))
                {
                    answers.Add(unit.Id);
                }
            }
            // Window of the measurement. Unit: ticks.
            int from = series.FirstEnemyArmyVisibleTick < 0 ? int.MaxValue : series.FirstEnemyArmyVisibleTick + (int)test.Number("delay_seconds") * series.TicksPerSecond;
            int until = (int)test.Number("until_seconds") * series.TicksPerSecond;
            double trainedSupply = 0;
            double answerSupply = 0;
            foreach (TimedAmount trained in series.Trained)
            {
                if (trained.Tick >= from && trained.Tick <= until)
                {
                    trainedSupply += trained.Supply;
                    answerSupply += answers.Contains(trained.Definition) ? trained.Supply : 0;
                }
            }
            // Share S of answer supply in what was trained, and the same share S0 of the base composition (weight x supply).
            double share = trainedSupply > 0 ? answerSupply / trainedSupply : 0;
            double baseTotal = 0;
            double baseAnswer = 0;
            foreach (KeyValuePair<string, int> pair in profile.RequireFaction(faction).Composition)
            {
                double weighted = pair.Value * (double)data.Content.Units[pair.Key].Supply;
                baseTotal += weighted;
                baseAnswer += answers.Contains(pair.Key) ? weighted : 0;
            }
            double baseShare = baseTotal > 0 ? baseAnswer / baseTotal : 0;
            double gain = share - baseShare;
            return new List<CheckPart>
            {
                new CheckPart(1, series.FirstEnemyArmyVisibleTick >= 0 && trainedSupply > 0 && gain >= test.Number("min_share_gain") - HarnessStats.BandTolerance,
                    "share_gain=" + gain.ToString("0.000", CultureInfo.InvariantCulture) + " share=" + share.ToString("0.000", CultureInfo.InvariantCulture)
                    + " base_share=" + baseShare.ToString("0.000", CultureInfo.InvariantCulture) + " armour=" + (armour.Length == 0 ? "none" : armour)),
                new CheckPart(2, series.HqAliveAt(survive), "hq_alive=" + series.HqAliveAt(survive).ToString().ToLowerInvariant()),
            };
        }

        /// <summary>trade: enemy army supply killed over max(1, own army supply lost) within until_seconds is at least min_trade_ratio, and the HQ stands at until_seconds.</summary>
        public static List<CheckPart> Trade(ResponseTest test, ResponseSeries series)
        {
            int until = (int)test.Number("until_seconds");
            int limit = until * series.TicksPerSecond;
            int killed = SumUpTo(series.EnemyArmyKilled, limit);
            int lost = SumUpTo(series.OwnArmyLost, limit);
            double ratio = (double)killed / Math.Max(1, lost);
            bool alive = series.HqAliveAt(until);
            return new List<CheckPart>
            {
                new CheckPart(1, ratio >= test.Number("min_trade_ratio") - HarnessStats.BandTolerance && alive,
                    "trade_ratio=" + ratio.ToString("0.000", CultureInfo.InvariantCulture) + " killed=" + Int(killed) + " lost=" + Int(lost) + " hq_alive=" + alive.ToString().ToLowerInvariant()),
            };
        }

        /// <summary>
        /// raid: (1) by defence_by_seconds the AI owns a static defence (any state) within defence_radius_tiles of its drop-off nearest the wave spawn point; (2) in each wave window
        /// [w, w + window_seconds] it loses at most max_workers_lost workers.
        /// </summary>
        public static List<CheckPart> Raid(ResponseTest test, ResponseSeries series)
        {
            ResponseSample? sample = series.SampleAt((int)test.Number("defence_by_seconds"));
            bool defended = false;
            if (sample != null && series.WaveSpawnPoint.HasValue && sample.DropOffs.Count > 0)
            {
                // Drop-off nearest the wave spawn point (ties: first listed, the lowest entity ID).
                TilePoint nearest = sample.DropOffs[0];
                foreach (TilePoint dropOff in sample.DropOffs)
                {
                    if (dropOff.DistanceSquared(series.WaveSpawnPoint.Value) < nearest.DistanceSquared(series.WaveSpawnPoint.Value))
                    {
                        nearest = dropOff;
                    }
                }
                double radius = test.Number("defence_radius_tiles");
                foreach (TilePoint defence in sample.StaticDefences)
                {
                    defended |= defence.DistanceSquared(nearest) <= radius * radius;
                }
            }
            // Most workers lost in any wave window.
            int worst = 0;
            foreach (int wave in test.Waves)
            {
                int from = wave * series.TicksPerSecond;
                int to = (wave + (int)test.Number("window_seconds")) * series.TicksPerSecond;
                int count = 0;
                foreach (int tick in series.WorkerLossTicks)
                {
                    count += tick >= from && tick <= to ? 1 : 0;
                }
                worst = Math.Max(worst, count);
            }
            return new List<CheckPart>
            {
                new CheckPart(1, defended, "static_defence_near_raided_base=" + defended.ToString().ToLowerInvariant()),
                new CheckPart(2, worst <= (int)test.Number("max_workers_lost"), "max_workers_lost_in_wave=" + Int(worst)),
            };
        }

        /// <summary>turtle: (1) army supply lost before the first enemy structure is destroyed is at most max_supply_lost_before_first_kill; (2) the AI wins by until_seconds.</summary>
        public static List<CheckPart> Turtle(ResponseTest test, ResponseSeries series)
        {
            // Own army supply lost before the first enemy structure fell (all of it when none fell).
            int lost = 0;
            foreach (TimedAmount loss in series.OwnArmyLost)
            {
                if (series.FirstEnemyStructureKillTick < 0 || loss.Tick < series.FirstEnemyStructureKillTick)
                {
                    lost += loss.Supply;
                }
            }
            bool won = series.AiWon && series.EndTick <= (int)test.Number("until_seconds") * series.TicksPerSecond;
            return new List<CheckPart>
            {
                new CheckPart(1, series.FirstEnemyStructureKillTick >= 0 && lost <= test.Number("max_supply_lost_before_first_kill"),
                    "supply_lost_before_first_kill=" + Int(lost) + " first_structure_kill_s=" + (series.FirstEnemyStructureKillTick < 0 ? "none" : Seconds(series.FirstEnemyStructureKillTick, series))),
                new CheckPart(2, won, "won_by_s=" + (won ? Seconds(series.EndTick, series) : "no")),
            };
        }

        /// <summary>
        /// The pool of a faction (spec C.3): units of the faction with footprint 0, not a worker, not an HQ, listed in the trains of some structure of the faction, in ordinal ID order.
        /// </summary>
        public static List<UnitDefinition> Pool(DataSet data, string faction)
        {
            SortedSet<string> trained = new SortedSet<string>(StringComparer.Ordinal);
            foreach (UnitDefinition unit in data.Content.Units.Values)
            {
                if (unit.FactionId == faction)
                {
                    foreach (string id in unit.Economy.Trains)
                    {
                        trained.Add(id);
                    }
                }
            }
            List<UnitDefinition> pool = new List<UnitDefinition>();
            foreach (string id in trained)
            {
                UnitDefinition unit = data.Content.Units[id];
                if (unit.FactionId == faction && unit.Economy.FootprintTiles == 0 && !unit.Economy.CanGather && unit.Hq == null)
                {
                    pool.Add(unit);
                }
            }
            return pool;
        }

        /// <summary>Damage multiplier of the unit's weapon against an armour type; 0 for an unarmed unit or an empty armour type. Unit: ratio.</summary>
        private static double Multiplier(DataSet data, UnitDefinition unit, string armour)
        {
            if (unit.Weapon == null || armour.Length == 0)
            {
                return 0;
            }
            return HarnessStats.ToDouble(data.Content.Combat.GetMultiplier(unit.Weapon.DamageType, armour));
        }

        /// <summary>True when some seen enemy unit type has a weapon that can target the unit's layer.</summary>
        private static bool AnySeenCanTarget(DataSet data, ResponseSeries series, UnitDefinition unit)
        {
            foreach (string id in series.SeenEnemyDefinitions)
            {
                UnitDefinition enemy = data.Content.Units[id];
                if (enemy.Weapon != null && enemy.Weapon.CanTarget(unit.Layer))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Total supply of the entries at or before <paramref name="limitTick"/>. Unit: supply.</summary>
        private static int SumUpTo(IReadOnlyList<TimedAmount> entries, int limitTick)
        {
            int total = 0;
            foreach (TimedAmount entry in entries)
            {
                total += entry.Tick <= limitTick ? entry.Supply : 0;
            }
            return total;
        }

        /// <summary>A tick as game seconds with one decimal, or "none" for a negative tick.</summary>
        private static string Seconds(int tick, ResponseSeries series)
        {
            return tick < 0 ? "none" : HarnessStats.Seconds(tick, series.TicksPerSecond);
        }

        /// <summary>An integer in invariant culture.</summary>
        private static string Int(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

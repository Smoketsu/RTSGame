// rts/src/Ai/FightPredictor.cs
// The fight predictor of skirmish_ai_v3.md C.1: a static, pure function that says which of two groups of units wins a fight and how
// fast, computed only from unit definitions (weapon, armour, layer, speed, shield dome) and the damage table (ContentCatalog.Combat).
// Part of Rts.Ai. It names no faction or unit, uses Fix64 and integers only, walks entries in the order given (callers pass ordinal
// definition-ID order; Group sorts that way), draws no random number and keeps no static state. Package C's counter-mix scores use
// the same per-unit damage helpers (Dps, AverageDps). Not modelled on purpose: drench slow and grounding, splash, momentum, flooding,
// Windstorm range, regeneration, hit delay and focus fire.
using System;
using System.Collections.Generic;
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>One group of units of a single definition in a predicted fight.</summary>
    public readonly struct FightEntry
    {
        /// <summary>The unit type. Range: any unit definition of the data set.</summary>
        public UnitDefinition Definition { get; }

        /// <summary>How many units the group holds. Unit: units. Range: 0 upward (fractions allowed: the enemy model holds decayed counts).</summary>
        public Fix64 Count { get; }

        /// <summary>Total health of the group. Unit: health points. Range: 0 to Count x the definition's MaxHealth.</summary>
        public Fix64 Health { get; }

        /// <summary>Stores the entry.</summary>
        /// <param name="definition">The unit type.</param>
        /// <param name="count">Units in the group. Unit: units. Range: 0 upward.</param>
        /// <param name="health">Total health of the group. Unit: health points. Range: 0 upward.</param>
        public FightEntry(UnitDefinition definition, Fix64 count, Fix64 health)
        {
            Definition = definition;
            Count = count;
            Health = health;
        }
    }

    /// <summary>The outcome of <see cref="FightPredictor.Predict"/>.</summary>
    public readonly struct FightPrediction
    {
        /// <summary>Seconds side A needs to kill side B. Unit: seconds. Range: predictor_min_seconds to predictor_max_seconds.</summary>
        public Fix64 TimeAKillsB { get; }

        /// <summary>Seconds side B needs to kill side A. Unit: seconds. Range: predictor_min_seconds to predictor_max_seconds.</summary>
        public Fix64 TimeBKillsA { get; }

        /// <summary>True when A kills B strictly sooner than B kills A (a tie goes to the defender, so false). Unit: boolean. Range: true or false.</summary>
        public bool AWins { get; }

        /// <summary>TimeBKillsA divided by TimeAKillsB (both floored at predictor_min_seconds), capped at predictor_max_advantage; 0 when A is empty. Unit: ratio. Range: 0 to predictor_max_advantage.</summary>
        public Fix64 Advantage { get; }

        /// <summary>Stores the result.</summary>
        /// <param name="timeAKillsB">Seconds for A to kill B. Unit: seconds.</param>
        /// <param name="timeBKillsA">Seconds for B to kill A. Unit: seconds.</param>
        /// <param name="aWins">True when A wins.</param>
        /// <param name="advantage">Advantage ratio. Unit: ratio.</param>
        public FightPrediction(Fix64 timeAKillsB, Fix64 timeBKillsA, bool aWins, Fix64 advantage)
        {
            TimeAKillsB = timeAKillsB;
            TimeBKillsA = timeBKillsA;
            AWins = aWins;
            Advantage = advantage;
        }
    }

    /// <summary>The predictor_* profile numbers (skirmish_ai_v3.md C.1), read once from the profile.</summary>
    public sealed class PredictorSettings
    {
        /// <summary>Longest time a side needs (also "never"). Unit: seconds. Range: 10 to 36000.</summary>
        public Fix64 MaxSeconds { get; }

        /// <summary>Shortest time a side needs. Unit: seconds. Range: 0.01 to 10.</summary>
        public Fix64 MinSeconds { get; }

        /// <summary>Share of the range head start that is subtracted from a time. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 RangeWeight { get; }

        /// <summary>Longest range head start. Unit: seconds. Range: 0 to 60.</summary>
        public Fix64 RangeHeadstartMaxSeconds { get; }

        /// <summary>Share of the shield domes that counts as extra health against ranged damage. Unit: fraction. Range: 0 to 2.</summary>
        public Fix64 DomeWeight { get; }

        /// <summary>Cap of the advantage ratio. Unit: ratio. Range: 1 to 10000.</summary>
        public Fix64 MaxAdvantage { get; }

        /// <summary>Stores the settings.</summary>
        /// <param name="maxSeconds">Longest time. Unit: seconds.</param>
        /// <param name="minSeconds">Shortest time. Unit: seconds.</param>
        /// <param name="rangeWeight">Range head start weight. Unit: fraction.</param>
        /// <param name="rangeHeadstartMaxSeconds">Longest head start. Unit: seconds.</param>
        /// <param name="domeWeight">Dome weight. Unit: fraction.</param>
        /// <param name="maxAdvantage">Advantage cap. Unit: ratio.</param>
        public PredictorSettings(Fix64 maxSeconds, Fix64 minSeconds, Fix64 rangeWeight, Fix64 rangeHeadstartMaxSeconds, Fix64 domeWeight, Fix64 maxAdvantage)
        {
            MaxSeconds = maxSeconds;
            MinSeconds = minSeconds;
            RangeWeight = rangeWeight;
            RangeHeadstartMaxSeconds = rangeHeadstartMaxSeconds;
            DomeWeight = domeWeight;
            MaxAdvantage = maxAdvantage;
        }

        /// <summary>The settings a profile carries.</summary>
        /// <param name="profile">The profile.</param>
        public static PredictorSettings From(AiProfile profile)
        {
            return new PredictorSettings(profile.PredictorMaxSeconds, profile.PredictorMinSeconds, profile.PredictorRangeWeight,
                profile.PredictorRangeHeadstartMaxSeconds, profile.PredictorDomeWeight, profile.PredictorMaxAdvantage);
        }
    }

    /// <summary>The fight predictor (skirmish_ai_v3.md C.1). Pure and stateless.</summary>
    public static class FightPredictor
    {
        /// <summary>
        /// Predicts the fight of side A against side B. Entries must be in ordinal definition-ID order (all sums run in that order);
        /// entries with a count of 0 or less are ignored.
        /// </summary>
        /// <param name="a">Side A (the AI's own side in most callers).</param>
        /// <param name="b">Side B.</param>
        /// <param name="aUphill">True when side A's ranged ground units shoot uphill (the damage table's uphill multiplier applies to A's attacks).</param>
        /// <param name="rules">The damage table (ContentCatalog.Combat).</param>
        /// <param name="settings">The predictor numbers.</param>
        public static FightPrediction Predict(IReadOnlyList<FightEntry> a, IReadOnlyList<FightEntry> b, bool aUphill, CombatRules rules, PredictorSettings settings)
        {
            // The two sides without empty entries, order kept.
            List<FightEntry> sideA = Live(a);
            List<FightEntry> sideB = Live(b);
            // Seconds for A to kill B (A shoots uphill when aUphill).
            Fix64 timeAKillsB = TimeToKill(sideA, sideB, aUphill, false, rules, settings);
            // Seconds for B to kill A (A's units are the ones that shoot uphill, but B's shots are not).
            Fix64 timeBKillsA = TimeToKill(sideB, sideA, false, aUphill, rules, settings);
            if (sideA.Count == 0)
            {
                return new FightPrediction(timeAKillsB, timeBKillsA, false, Fix64.Zero);
            }
            // The advantage before the cap. Unit: ratio.
            Fix64 ratio = timeBKillsA / Fix64.Max(timeAKillsB, settings.MinSeconds);
            return new FightPrediction(timeAKillsB, timeBKillsA, timeAKillsB < timeBKillsA, Fix64.Min(settings.MaxAdvantage, ratio));
        }

        /// <summary>Groups units by definition ID (ordinal order): Count = number of units, Health = their summed health.</summary>
        /// <param name="units">The units (own units or visible enemies).</param>
        public static List<FightEntry> Group(IEnumerable<AiUnitView> units)
        {
            // Definition, count and health per definition ID.
            SortedDictionary<string, (UnitDefinition Definition, int Count, Fix64 Health)> groups =
                new SortedDictionary<string, (UnitDefinition, int, Fix64)>(StringComparer.Ordinal);
            foreach (AiUnitView unit in units)
            {
                if (groups.TryGetValue(unit.Definition.Id, out (UnitDefinition Definition, int Count, Fix64 Health) before))
                {
                    groups[unit.Definition.Id] = (before.Definition, before.Count + 1, before.Health + unit.Health);
                }
                else
                {
                    groups[unit.Definition.Id] = (unit.Definition, 1, unit.Health);
                }
            }
            // The entries in ordinal order.
            List<FightEntry> entries = new List<FightEntry>();
            foreach ((UnitDefinition Definition, int Count, Fix64 Health) group in groups.Values)
            {
                entries.Add(new FightEntry(group.Definition, Fix64.FromInt(group.Count), group.Health));
            }
            return entries;
        }

        /// <summary>
        /// Damage per second one <paramref name="attacker"/> deals to one <paramref name="target"/>: damage x table multiplier x uphill factor
        /// / cooldown, evaluated left to right; 0 when the attacker is unarmed or its weapon cannot hit the target's layer.
        /// </summary>
        /// <param name="attacker">The shooter's type.</param>
        /// <param name="target">The target's type.</param>
        /// <param name="uphill">True when the attacker's side shoots uphill.</param>
        /// <param name="rules">The damage table.</param>
        /// <returns>Damage per second. Unit: health points per second. Range: 0 upward.</returns>
        internal static Fix64 Dps(UnitDefinition attacker, UnitDefinition target, bool uphill, CombatRules rules)
        {
            WeaponDefinition? weapon = attacker.Weapon;
            if (weapon == null || !weapon.CanTarget(target.Layer))
            {
                return Fix64.Zero;
            }
            // The uphill factor: only ranged ground-or-water shots lose damage uphill.
            Fix64 up = uphill && !weapon.IsMelee && attacker.Layer != MovementLayer.Air && target.Layer != MovementLayer.Air
                ? rules.UphillRangedDamageMultiplier : Fix64.One;
            return weapon.Damage * rules.GetMultiplier(weapon.DamageType, target.ArmourType) * up / weapon.CooldownSeconds;
        }

        /// <summary>
        /// D(x) of C.1 step 1: the damage per second one <paramref name="attacker"/> deals against a group, averaged over the entries it can
        /// hit, weighted by their health; 0 when it is unarmed or nothing is hittable.
        /// </summary>
        /// <param name="attacker">The shooter's type.</param>
        /// <param name="targets">The target entries (Count above 0).</param>
        /// <param name="uphill">True when the attacker's side shoots uphill.</param>
        /// <param name="rules">The damage table.</param>
        /// <returns>Damage per second. Unit: health points per second. Range: 0 upward.</returns>
        internal static Fix64 AverageDps(UnitDefinition attacker, IReadOnlyList<FightEntry> targets, bool uphill, CombatRules rules)
        {
            // Sum of health x dps over hittable entries, and the health of those entries.
            Fix64 weighted = Fix64.Zero;
            Fix64 health = Fix64.Zero;
            foreach (FightEntry target in targets)
            {
                if (attacker.Weapon != null && attacker.Weapon.CanTarget(target.Definition.Layer))
                {
                    weighted += target.Health * Dps(attacker, target.Definition, uphill, rules);
                    health += target.Health;
                }
            }
            return health <= Fix64.Zero ? Fix64.Zero : weighted / health;
        }

        /// <summary>The entries with a count above 0, order kept.</summary>
        private static List<FightEntry> Live(IReadOnlyList<FightEntry> entries)
        {
            // The live entries.
            List<FightEntry> live = new List<FightEntry>();
            foreach (FightEntry entry in entries)
            {
                if (entry.Count > Fix64.Zero)
                {
                    live.Add(entry);
                }
            }
            return live;
        }

        /// <summary>T(X, Y) of C.1: seconds for side X to kill side Y.</summary>
        private static Fix64 TimeToKill(List<FightEntry> xs, List<FightEntry> ys, bool xUphill, bool yUphill, CombatRules rules, PredictorSettings s)
        {
            if (ys.Count == 0)
            {
                return s.MinSeconds;
            }
            if (xs.Count == 0)
            {
                // Nothing on this side: it never kills anything (C-6: an empty B leaves A the winner).
                return s.MaxSeconds;
            }
            // Entries of Y that some armed X entry can hit.
            List<FightEntry> rest = new List<FightEntry>();
            foreach (FightEntry y in ys)
            {
                // True when some armed x can target y's layer.
                bool hittable = false;
                foreach (FightEntry x in xs)
                {
                    hittable |= x.Definition.Weapon != null && x.Definition.Weapon.CanTarget(y.Definition.Layer);
                }
                if (hittable)
                {
                    rest.Add(y);
                    continue;
                }
                // Step 3: an unhittable entry that can hurt X makes the fight unwinnable for X.
                foreach (FightEntry x in xs)
                {
                    if (Dps(y.Definition, x.Definition, yUphill, rules) > Fix64.Zero)
                    {
                        return s.MaxSeconds;
                    }
                }
            }
            if (rest.Count == 0)
            {
                return s.MinSeconds;
            }
            // Step 2: total and ranged damage per second of X against the hittable rest of Y.
            Fix64 dps = Fix64.Zero;
            Fix64 ranged = Fix64.Zero;
            foreach (FightEntry x in xs)
            {
                // Count x D(x) for this entry.
                Fix64 contribution = x.Count * AverageDps(x.Definition, rest, xUphill, rules);
                dps += contribution;
                if (x.Definition.Weapon != null && !x.Definition.Weapon.IsMelee)
                {
                    ranged += contribution;
                }
            }
            // Step 4 and 5: health of the rest, and the shield domes that add to it against ranged damage.
            Fix64 hp = Fix64.Zero;
            Fix64 pools = Fix64.Zero;
            foreach (FightEntry y in rest)
            {
                hp += y.Health;
                if (y.Definition.ShieldDome != null)
                {
                    pools += y.Count * y.Definition.ShieldDome.Pool;
                }
            }
            // Shield dome pools of Y counted as extra health against ranged damage. Unit: health points. Range: 0 upward.
            Fix64 dome = s.DomeWeight * pools;
            if (dps <= Fix64.Zero)
            {
                return s.MaxSeconds;
            }
            // Step 6: time to kill, capped.
            // Seconds X needs, before the range head start. Unit: seconds. Range: 0 to predictor_max_seconds.
            Fix64 time = Fix64.Min(s.MaxSeconds, (hp + dome * ranged / dps) / dps);
            if (time >= s.MaxSeconds)
            {
                return time;
            }
            // Step 7: the side with the longer reach shoots for a while before the other can answer.
            // X's damage-weighted weapon reach against the hittable rest of Y. Unit: tiles. Range: 0 upward.
            Fix64 reachX = ReachOf(xs, rest, xUphill, rules, dps);
            // Total damage per second Y deals to X (count x D(y) summed). Unit: health points per second. Range: 0 upward.
            Fix64 dpsY = Fix64.Zero;
            foreach (FightEntry y in ys)
            {
                dpsY += y.Count * AverageDps(y.Definition, xs, yUphill, rules);
            }
            // Deviation from C.1 step 7 (worked example C-2): when Y cannot hurt X at all, Y's reach is the count-weighted mean range of its
            // armed entries (not 0), so C-2's head start is (5 - 1) / 3 = 4/3 as the table says.
            // Y's weapon reach against X. Unit: tiles. Range: 0 upward.
            Fix64 reachY = dpsY > Fix64.Zero ? ReachOf(ys, xs, yUphill, rules, dpsY) : MeanRange(ys);
            if (reachX > reachY)
            {
                // Count-weighted mean speed of Y's armed entries, and their count.
                Fix64 speedSum = Fix64.Zero;
                Fix64 armed = Fix64.Zero;
                foreach (FightEntry y in ys)
                {
                    if (y.Definition.Weapon != null)
                    {
                        speedSum += y.Count * y.Definition.SpeedTilesPerSecond;
                        armed += y.Count;
                    }
                }
                // Count-weighted mean speed of Y's armed entries. Unit: tiles per second. Range: 0 upward.
                Fix64 speedY = armed <= Fix64.Zero ? Fix64.Zero : speedSum / armed;
                // Seconds of free shooting X gets from its longer reach, capped. Unit: seconds. Range: 0 to predictor_range_headstart_max_seconds.
                Fix64 head = speedY <= Fix64.Zero ? s.RangeHeadstartMaxSeconds : Fix64.Min((reachX - reachY) / speedY, s.RangeHeadstartMaxSeconds);
                time = Fix64.Max(s.MinSeconds, time - s.RangeWeight * head);
            }
            return time;
        }

        /// <summary>Count-weighted mean weapon range of a side's armed entries. Unit: tiles. Range: 0 upward; 0 when none is armed.</summary>
        private static Fix64 MeanRange(List<FightEntry> side)
        {
            // Sum of Count x range, and the armed count.
            Fix64 sum = Fix64.Zero;
            Fix64 armed = Fix64.Zero;
            foreach (FightEntry y in side)
            {
                if (y.Definition.Weapon != null)
                {
                    sum += y.Count * y.Definition.Weapon.RangeTiles;
                    armed += y.Count;
                }
            }
            return armed <= Fix64.Zero ? Fix64.Zero : sum / armed;
        }

        /// <summary>Damage-weighted mean weapon range of a side against a target group: sum of Count x D(x) x range, divided by the side's total damage per second.</summary>
        private static Fix64 ReachOf(List<FightEntry> side, IReadOnlyList<FightEntry> targets, bool uphill, CombatRules rules, Fix64 totalDps)
        {
            // Sum of Count x D(x) x range. Unit: health points per second x tiles.
            Fix64 sum = Fix64.Zero;
            foreach (FightEntry x in side)
            {
                if (x.Definition.Weapon != null)
                {
                    sum += x.Count * AverageDps(x.Definition, targets, uphill, rules) * x.Definition.Weapon.RangeTiles;
                }
            }
            return sum / totalDps;
        }
    }
}

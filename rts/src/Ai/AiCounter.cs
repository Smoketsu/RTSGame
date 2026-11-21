// rts/src/Ai/AiCounter.cs
// The counter-mix of skirmish_ai_v3.md C.3 and C.4: the unit pool of a faction (armed units only) computed from data, the Lanchester score of every pool
// unit (and static defence) against the enemy model, and the integer mix weights that replace the plan's composition when
// use_counter_mix is on. Part of Rts.Ai. Everything is computed from UnitDefinition and the damage table (FightPredictor helpers); no
// faction or unit ID is named here. Package C2 (skirmish_ai_v3.md C2.2, use_predictor_mix) adds PredictorMix: the pool units are scored by
// the fight predictor's advantage of the own army plus a purchase against the enemy model at equal value, and the confidence comes from
// sighted units only; Mix calls it when the switch is on and Score then only chooses static defences. Fix64 and integers only, ordinal ID order everywhere, no random draws, no static state.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>Counter-mix helpers (skirmish_ai_v3.md C.3 and C.4). Stateless.</summary>
    internal static class AiCounter
    {
        /// <summary>Rounding tolerance added before flooring a mix weight, so exact products that Fix64 truncation leaves a hair short still floor to the exact integer (2^-20 of a weight unit). Unit: weight units. Fixed tolerance, not tuning.</summary>
        private static readonly Fix64 MixTolerance = Fix64.FromRaw(1L << 12);

        /// <summary>
        /// The unit pool of a faction (C.3): mobile (footprint 0) non-worker non-HQ armed (has a weapon) units of the faction that some structure of the faction trains.
        /// </summary>
        /// <param name="view">The view (for the unit catalogue).</param>
        /// <param name="factionId">The plan's faction ID.</param>
        /// <returns>Definitions in ordinal ID order. Range: 0 or more units.</returns>
        internal static List<UnitDefinition> Pool(AiWorldView view, string factionId)
        {
            // Unit IDs some faction structure trains, ordinal.
            SortedSet<string> trained = new SortedSet<string>(StringComparer.Ordinal);
            foreach (UnitDefinition structure in view.Content.Units.Values)
            {
                if (string.Equals(structure.FactionId, factionId, StringComparison.Ordinal))
                {
                    foreach (string id in structure.Economy.Trains)
                    {
                        trained.Add(id);
                    }
                }
            }
            // The pool being built.
            List<UnitDefinition> pool = new List<UnitDefinition>();
            foreach (string id in trained)
            {
                if (view.Content.Units.TryGetValue(id, out UnitDefinition? unit) && string.Equals(unit.FactionId, factionId, StringComparison.Ordinal)
                    && AiIntel.IsArmyType(unit) && unit.Weapon != null)
                {
                    pool.Add(unit);
                }
            }
            return pool;
        }

        /// <summary>The faction's static defences with a weapon (C.3), ordinal ID order.</summary>
        /// <param name="view">The view (for the unit catalogue).</param>
        /// <param name="factionId">The plan's faction ID.</param>
        internal static List<UnitDefinition> StaticDefences(AiWorldView view, string factionId)
        {
            // The defences found.
            List<UnitDefinition> found = new List<UnitDefinition>();
            foreach (UnitDefinition unit in view.Content.Units.Values)
            {
                if (string.Equals(unit.FactionId, factionId, StringComparison.Ordinal) && unit.Economy.FootprintTiles > 0 && unit.Economy.IsStaticDefence && unit.Weapon != null)
                {
                    found.Add(unit);
                }
            }
            return found;
        }

        /// <summary>The enemy model as predictor entries (B.3, C.1): (definition, M(t), M(t) x MaxHealth) for every type with M(t) above 0, ordinal order.</summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="counts">The model counts M(t) from <see cref="AiIntel.ModelCounts"/>.</param>
        internal static List<FightEntry> Entries(AiWorldView view, SortedDictionary<string, Fix64> counts)
        {
            // The entries.
            List<FightEntry> entries = new List<FightEntry>();
            foreach (KeyValuePair<string, Fix64> pair in counts)
            {
                if (view.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition) && pair.Value > Fix64.Zero)
                {
                    entries.Add(new FightEntry(definition, pair.Value, pair.Value * definition.MaxHealth));
                }
            }
            return entries;
        }

        /// <summary>
        /// The score of one unit against the enemy entries (C.4 step 1): Dout x MaxHealth / (max(Din, counter_min_incoming_dps) x cost^2),
        /// the strength per budget squared, where cost = cost_common + counter_rare_cost_weight x cost_rare.
        /// </summary>
        /// <param name="unit">A pool unit or a static defence.</param>
        /// <param name="enemies">The enemy entries E.</param>
        /// <param name="profile">The profile (rare cost weight, minimum incoming damage).</param>
        /// <param name="view">The view (damage table).</param>
        /// <returns>The score. Unit: health points per second x health points per squared cost unit. Range: 0 upward.</returns>
        internal static Fix64 Score(UnitDefinition unit, IReadOnlyList<FightEntry> enemies, AiProfile profile, AiWorldView view)
        {
            // Damage per second the unit deals to the enemy group.
            Fix64 outgoing = FightPredictor.AverageDps(unit, enemies, false, view.Content.Combat);
            // Count-weighted mean damage per second the enemies deal to the unit.
            Fix64 incomingSum = Fix64.Zero;
            Fix64 enemyCount = Fix64.Zero;
            foreach (FightEntry enemy in enemies)
            {
                incomingSum += enemy.Count * FightPredictor.Dps(enemy.Definition, unit, false, view.Content.Combat);
                enemyCount += enemy.Count;
            }
            // Count-weighted mean damage per second the enemies deal to the unit. Unit: health points per second. Range: 0 upward.
            Fix64 incoming = enemyCount <= Fix64.Zero ? Fix64.Zero : incomingSum / enemyCount;
            // Cost with the rare resource weighted. Unit: resource units. Range: 1 upward.
            Fix64 cost = Fix64.Max(Fix64.One, Fix64.FromInt(unit.CostCommon) + profile.CounterRareCostWeight * Fix64.FromInt(unit.CostRare));
            return outgoing * unit.MaxHealth / (Fix64.Max(incoming, profile.CounterMinIncomingDps) * cost * cost);
        }

        /// <summary>
        /// The mix weights (C.4): for every pool unit and every base-composition unit, floor(mix_resolution x ((1 - k) x base share + k x
        /// counter share)). With nothing known (k = 0) this is the base composition scaled to mix_resolution.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="plan">The plan (faction and base composition).</param>
        /// <param name="memory">The memory (the enemy model; read only).</param>
        /// <returns>Weight per unit ID in ordinal order. Unit: weight units. Range: 0 to mix_resolution each.</returns>
        internal static SortedDictionary<string, int> Mix(AiWorldView view, AiProfile profile, AiFactionPlan plan, AiMemory memory)
        {
            if (profile.UsePredictorMix)
            {
                return PredictorMix(view, profile, plan, memory);
            }
            // The enemy model, as counts and as entries.
            SortedDictionary<string, Fix64> counts = AiIntel.ModelCounts(view, profile, memory);
            List<FightEntry> enemies = Entries(view, counts);
            // Known enemy army supply S_E. Unit: supply.
            Fix64 known = Fix64.Zero;
            foreach (FightEntry enemy in enemies)
            {
                known += enemy.Count * Fix64.FromInt(view.SupplyOf(enemy.Definition));
            }
            // Pool and its scores (C.4 step 1), then the kept share (step 2).
            List<UnitDefinition> pool = Pool(view, plan.FactionId);
            SortedDictionary<string, Fix64> scores = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
            Fix64 top = Fix64.Zero;
            foreach (UnitDefinition unit in pool)
            {
                // Counter score of this pool unit. Unit: score (see Score). Range: 0 upward.
                Fix64 score = enemies.Count == 0 ? Fix64.Zero : Score(unit, enemies, profile, view);
                scores[unit.Id] = score;
                top = Fix64.Max(top, score);
            }
            // Sum of the kept scores, and the kept scores (others 0).
            Fix64 keptSum = Fix64.Zero;
            SortedDictionary<string, Fix64> kept = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Fix64> pair in scores)
            {
                if (pair.Value > Fix64.Zero && pair.Value >= profile.CounterKeepFraction * top)
                {
                    kept[pair.Key] = pair.Value;
                    keptSum += pair.Value;
                }
            }
            // Step 3: confidence. Unit: fraction. Range: 0 to counter_weight.
            Fix64 k = enemies.Count == 0 || keptSum <= Fix64.Zero
                ? Fix64.Zero
                : profile.CounterWeight * Fix64.Min(Fix64.One, known / profile.CounterFullConfidenceSupply);
            return Integers(profile, plan, scores.Keys, k, kept, keptSum);
        }

        /// <summary>
        /// The integer mix of C.4 step 4 (also C2.2 step 7): floor(mix_resolution x ((1 - k) x base share + k x kept share)) for every
        /// pool unit and every base-composition unit.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <param name="plan">The plan (base composition).</param>
        /// <param name="poolIds">Pool unit IDs.</param>
        /// <param name="k">Confidence. Unit: fraction. Range: 0 to counter_weight.</param>
        /// <param name="kept">Kept weight per unit ID (the others are absent). Unit: score or advantage weight. Range: above 0.</param>
        /// <param name="keptSum">Sum of the kept weights. Range: 0 upward.</param>
        private static SortedDictionary<string, int> Integers(AiProfile profile, AiFactionPlan plan, IEnumerable<string> poolIds, Fix64 k,
            SortedDictionary<string, Fix64> kept, Fix64 keptSum)
        {
            // Total of the base weights. Unit: weight units. Range: 1 upward.
            int baseTotal = 0;
            foreach (int weight in plan.Composition.Values)
            {
                baseTotal += weight;
            }
            // Step 4: the integer mix over the pool and the base keys.
            SortedSet<string> ids = new SortedSet<string>(poolIds, StringComparer.Ordinal);
            ids.UnionWith(plan.Composition.Keys);
            // (1 - k) x mix_resolution, the share of the base composition. Unit: weight units.
            Fix64 baseShare = (Fix64.One - k) * profile.MixResolution;
            SortedDictionary<string, int> mix = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                plan.Composition.TryGetValue(id, out int baseWeight);
                // Mix weight before flooring: base share first, counter share added below. Unit: weight units. Range: 0 to mix_resolution.
                Fix64 value = baseShare * baseWeight / baseTotal;
                if (kept.TryGetValue(id, out Fix64 keptScore))
                {
                    value += k * profile.MixResolution * keptScore / keptSum;
                }
                mix[id] = (value + MixTolerance).FloorToInt();
            }
            return mix;
        }

        /// <summary>The value of one unit (C2.2 step 1): cost_common + counter_rare_cost_weight x cost_rare. Unit: resource units. Range: 0 upward.</summary>
        /// <param name="unit">A unit or structure definition.</param>
        /// <param name="profile">The profile (rare cost weight).</param>
        internal static Fix64 Value(UnitDefinition unit, AiProfile profile)
        {
            return Fix64.FromInt(unit.CostCommon) + profile.CounterRareCostWeight * Fix64.FromInt(unit.CostRare);
        }

        /// <summary>
        /// The predictor mix (C2.2): each pool unit is scored by the fight predictor's advantage of the own army plus a purchase of that
        /// unit against the enemy model scaled to equal value; units within counter_keep_fraction of the best share the counter weight;
        /// confidence comes from sighted supply only.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="plan">The plan (faction and base composition).</param>
        /// <param name="memory">The memory (the enemy model; read only).</param>
        /// <param name="advantagesOut">Test hook: when not null, receives the predictor advantage of every pool unit (step 4). Unit: ratio. Range: 0 to predictor_max_advantage.</param>
        internal static SortedDictionary<string, int> PredictorMix(AiWorldView view, AiProfile profile, AiFactionPlan plan, AiMemory memory,
            SortedDictionary<string, Fix64>? advantagesOut = null)
        {
            // Pool, and the model entries with a value above 0 (a zero-cost definition is skipped, step 2).
            List<UnitDefinition> pool = Pool(view, plan.FactionId);
            List<FightEntry> enemies = new List<FightEntry>();
            // V(E): value of the enemy model. Unit: resource units. Range: above 0 once any entry is kept.
            Fix64 enemyValue = Fix64.Zero;
            foreach (FightEntry entry in Entries(view, AiIntel.ModelCounts(view, profile, memory)))
            {
                // Value of one unit of this entry. Unit: resource units.
                Fix64 each = Value(entry.Definition, profile);
                if (each > Fix64.Zero)
                {
                    enemies.Add(entry);
                    enemyValue += entry.Count * each;
                }
            }
            // The kept advantage weights and their sum (step 5); stay empty when nothing is scored.
            SortedDictionary<string, Fix64> kept = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
            Fix64 keptSum = Fix64.Zero;
            if (enemies.Count > 0)
            {
                // O: own complete army units except the scout, with their real health.
                List<AiUnitView> own = new List<AiUnitView>();
                // V(O): value of that army. Unit: resource units.
                Fix64 ownValue = Fix64.Zero;
                foreach (AiUnitView unit in view.OwnUnits)
                {
                    if (unit.IsArmy && !unit.IsUnderConstruction && unit.Id != memory.ScoutId)
                    {
                        own.Add(unit);
                        ownValue += Value(unit.Definition, profile);
                    }
                }
                // O as predictor entries, ordinal order (Group sorts). Unit: units and health points.
                List<FightEntry> ownEntries = FightPredictor.Group(own);
                // Budget B and the equal-value factor f of step 3. Unit: resource units; ratio.
                Fix64 budget = Fix64.FromInt(profile.MixEvalBudgetCommon);
                Fix64 factor = (ownValue + budget) / enemyValue;
                // E_f: the enemy model with Count and Health multiplied by f. Unit: units and health points.
                List<FightEntry> scaled = new List<FightEntry>();
                foreach (FightEntry entry in enemies)
                {
                    scaled.Add(new FightEntry(entry.Definition, entry.Count * factor, entry.Health * factor));
                }
                // The predictor settings of the profile (C.1 keys).
                PredictorSettings settings = PredictorSettings.From(profile, memory.FightSimRules);
                // Advantage per pool unit (step 4) and the best of them. Unit: ratio.
                SortedDictionary<string, Fix64> advantages = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
                Fix64 top = Fix64.Zero;
                foreach (UnitDefinition unit in pool)
                {
                    // Units of this type the budget buys: the common limit, then the smaller of the limits. Unit: units. Range: 0 upward.
                    Fix64 buy = Fix64.Zero;
                    // Units the rare bank plus allowance buys. Unit: units. Range: 0 upward.
                    Fix64 rareBuy = Fix64.Zero;
                    // True once some cost limits the purchase (a unit costing nothing is not scored). Unit: boolean. Range: true or false.
                    bool limited = false;
                    if (unit.CostCommon > 0)
                    {
                        buy = budget / Fix64.FromInt(unit.CostCommon);
                        limited = true;
                    }
                    if (unit.CostRare > 0)
                    {
                        rareBuy = Fix64.FromInt(view.Rare + profile.MixRareAllowance) / Fix64.FromInt(unit.CostRare);
                        buy = limited ? Fix64.Min(buy, rareBuy) : rareBuy;
                        limited = true;
                    }
                    // Advantage of the own army plus the purchase. 0 when the unit costs nothing or nothing is bought.
                    Fix64 advantage = Fix64.Zero;
                    if (limited && buy > Fix64.Zero)
                    {
                        advantage = FightPredictor.Predict(WithPurchase(ownEntries, unit, buy), scaled, false, view.Content.Combat, settings).Advantage;
                    }
                    advantages[unit.Id] = advantage;
                    advantagesOut?.Add(unit.Id, advantage);
                    top = Fix64.Max(top, advantage);
                }
                // Step 5: kept units and weights.
                // Advantage a unit must reach to be kept. Unit: ratio.
                Fix64 floor = profile.CounterKeepFraction * top;
                foreach (KeyValuePair<string, Fix64> pair in advantages)
                {
                    if (top > Fix64.Zero && pair.Value > Fix64.Zero && pair.Value >= floor)
                    {
                        kept[pair.Key] = pair.Value - floor;
                        keptSum += pair.Value - floor;
                    }
                }
                if (kept.Count > 0 && keptSum <= Fix64.Zero)
                {
                    // Only possible with counter_keep_fraction 1: every kept unit weighs 1.
                    foreach (string id in new List<string>(kept.Keys))
                    {
                        kept[id] = Fix64.One;
                    }
                    keptSum = Fix64.FromInt(kept.Count);
                }
            }
            // Step 6: confidence from sighted supply only. Unit: supply.
            Fix64 seen = Fix64.Zero;
            foreach (KeyValuePair<string, Fix64> pair in AiIntel.SeenCounts(view, profile, memory))
            {
                if (view.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition))
                {
                    seen += pair.Value * Fix64.FromInt(view.SupplyOf(definition));
                }
            }
            // Confidence. Unit: fraction. Range: 0 to counter_weight.
            Fix64 k = kept.Count == 0 ? Fix64.Zero : profile.CounterWeight * Fix64.Min(Fix64.One, seen / profile.CounterFullConfidenceSupply);
            return Integers(profile, plan, pool.ConvertAll(u => u.Id), k, kept, keptSum);
        }

        /// <summary>The own army entries with <paramref name="count"/> more units of <paramref name="unit"/> at full health (C2.2 step 4), ordinal order kept.</summary>
        private static List<FightEntry> WithPurchase(List<FightEntry> army, UnitDefinition unit, Fix64 count)
        {
            // The new list; the unit's entry is replaced or inserted at its ordinal place.
            List<FightEntry> result = new List<FightEntry>();
            // True once the unit's entry has been put in the result. Unit: boolean. Range: true or false.
            bool placed = false;
            foreach (FightEntry entry in army)
            {
                if (!placed && string.CompareOrdinal(entry.Definition.Id, unit.Id) >= 0)
                {
                    placed = true;
                    if (string.Equals(entry.Definition.Id, unit.Id, StringComparison.Ordinal))
                    {
                        result.Add(new FightEntry(unit, entry.Count + count, entry.Health + count * unit.MaxHealth));
                        continue;
                    }
                    result.Add(new FightEntry(unit, count, count * unit.MaxHealth));
                }
                result.Add(entry);
            }
            if (!placed)
            {
                result.Add(new FightEntry(unit, count, count * unit.MaxHealth));
            }
            return result;
        }
    }
}

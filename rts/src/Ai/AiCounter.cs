// rts/src/Ai/AiCounter.cs
// The counter-mix of skirmish_ai_v3.md C.3 and C.4: the unit pool of a faction computed from data, the Lanchester score of every pool
// unit (and static defence) against the enemy model, and the integer mix weights that replace the plan's composition when
// use_counter_mix is on. Part of Rts.Ai. Everything is computed from UnitDefinition and the damage table (FightPredictor helpers); no
// faction or unit ID is named here. Fix64 and integers only, ordinal ID order everywhere, no random draws, no static state.
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
        /// The unit pool of a faction (C.3): mobile (footprint 0) non-worker non-HQ units of the faction that some structure of the faction trains.
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
                    && AiIntel.IsArmyType(unit))
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
            // Total of the base weights. Unit: weight units. Range: 1 upward.
            int baseTotal = 0;
            foreach (int weight in plan.Composition.Values)
            {
                baseTotal += weight;
            }
            // Step 4: the integer mix over the pool and the base keys.
            SortedSet<string> ids = new SortedSet<string>(scores.Keys, StringComparer.Ordinal);
            ids.UnionWith(plan.Composition.Keys);
            // (1 - k) x mix_resolution, the share of the base composition. Unit: weight units.
            Fix64 baseShare = (Fix64.One - k) * profile.MixResolution;
            SortedDictionary<string, int> mix = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                plan.Composition.TryGetValue(id, out int baseWeight);
                Fix64 value = baseShare * baseWeight / baseTotal;
                if (kept.TryGetValue(id, out Fix64 keptScore))
                {
                    value += k * profile.MixResolution * keptScore / keptSum;
                }
                mix[id] = (value + MixTolerance).FloorToInt();
            }
            return mix;
        }
    }
}

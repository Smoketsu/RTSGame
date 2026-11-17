// rts/src/Ai/AiFight.cs
// Small pure helpers for the fight choices of skirmish_ai_v3.md Package D: merging, scaling and building the predictor entries
// (FightEntry lists, ordinal definition-ID order) the attack, retreat, harass and defence tests compare. Part of Rts.Ai. Fix64 and
// integers only, no static state, no random draws, no faction or unit named; the rules that use them are in AiBrainFight.cs.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>Predictor-entry helpers for package D. Stateless.</summary>
    internal static class AiFight
    {
        /// <summary>
        /// The entries of <paramref name="a"/> and <paramref name="b"/> as one list with one entry per definition (counts and health
        /// summed), in ordinal definition-ID order, as FightPredictor.Predict expects.
        /// </summary>
        /// <param name="a">First entry list (any order).</param>
        /// <param name="b">Second entry list (any order).</param>
        /// <returns>The merged entries. Unit: units and health points. Range: 0 or more entries.</returns>
        internal static List<FightEntry> Merge(IEnumerable<FightEntry> a, IEnumerable<FightEntry> b)
        {
            // Count and health per definition ID, ordinal order.
            SortedDictionary<string, FightEntry> sums = new SortedDictionary<string, FightEntry>(StringComparer.Ordinal);
            foreach (IEnumerable<FightEntry> side in new[] { a, b })
            {
                foreach (FightEntry entry in side)
                {
                    sums[entry.Definition.Id] = sums.TryGetValue(entry.Definition.Id, out FightEntry before)
                        ? new FightEntry(entry.Definition, before.Count + entry.Count, before.Health + entry.Health)
                        : entry;
                }
            }
            return new List<FightEntry>(sums.Values);
        }

        /// <summary>The entries with every count and health multiplied by <paramref name="share"/> (order kept).</summary>
        /// <param name="entries">The entries (for example the enemy model).</param>
        /// <param name="share">The factor. Unit: fraction. Range: 0 to 1.</param>
        /// <returns>The scaled entries. Unit: units and health points.</returns>
        internal static List<FightEntry> Scaled(IReadOnlyList<FightEntry> entries, Fix64 share)
        {
            // The scaled copy.
            List<FightEntry> result = new List<FightEntry>();
            foreach (FightEntry entry in entries)
            {
                result.Add(new FightEntry(entry.Definition, entry.Count * share, entry.Health * share));
            }
            return result;
        }

        /// <summary>One structure as a predictor entry: count 1 and the health it is believed to have.</summary>
        /// <param name="definition">The structure's definition (a static defence with a weapon).</param>
        /// <param name="healthFraction">Health share when last seen. Unit: fraction. Range: 0 to 1.</param>
        /// <returns>The entry. Unit: one unit, health points.</returns>
        internal static FightEntry StructureEntry(UnitDefinition definition, Fix64 healthFraction)
        {
            return new FightEntry(definition, Fix64.One, healthFraction * definition.MaxHealth);
        }
    }
}

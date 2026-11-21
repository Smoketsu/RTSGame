// rts/src/Ai/AiBudget.cs
// The budget queues of specs/ai_budget_queues.md B.1 (P-006 package B), as pure integer arithmetic over arrays so the allocation can be unit-tested without a world: six named queues, each with an account A and a want N
// per resource (common, rare), laid out as index queue x 2 + resource. Open clamps the accounts to the bank, releases the paused queues' money and distributes the free money in proportion to the weights; Close
// releases what a queue no longer wants; DefenceAllowed is the defence spending ceiling. AiBrain's Turn calls these; nothing here reads a world view, a clock or a random number. Integers only (64-bit products),
// ties always to the lower queue index. Part of Rts.Ai.
using System;
using System.Collections.Generic;

namespace Rts.Ai
{
    /// <summary>The budget queue arithmetic of package B. Static and stateless; the state lives in <see cref="AiMemory"/>.</summary>
    public static class AiBudget
    {
        /// <summary>Number of queues. Unit: queues. Range: fixed 6.</summary>
        public const int QueueCount = 6;

        /// <summary>Queue index of the economy queue (supply structure, workers). Range: fixed 0.</summary>
        public const int Economy = 0;

        /// <summary>Queue index of the expansion queue (depots). Range: fixed 1.</summary>
        public const int Expansion = 1;

        /// <summary>Queue index of the production queue (build order steps, extra producers). Range: fixed 2.</summary>
        public const int Production = 2;

        /// <summary>Queue index of the army queue (scout, rule P units). Range: fixed 3.</summary>
        public const int Army = 3;

        /// <summary>Queue index of the tech queue (rule T and T2). Range: fixed 4.</summary>
        public const int Tech = 4;

        /// <summary>Queue index of the defence queue (static defences). Range: fixed 5.</summary>
        public const int Defence = 5;

        /// <summary>Index of the common resource in the per-resource arrays. Range: fixed 0.</summary>
        public const int Common = 0;

        /// <summary>Index of the rare resource in the per-resource arrays. Range: fixed 1.</summary>
        public const int Rare = 1;

        /// <summary>Length of the per-queue per-resource arrays (queue x 2 + resource). Unit: entries. Range: fixed 12.</summary>
        public const int Cells = QueueCount * 2;

        /// <summary>Largest number of proportional passes of one distribution. Unit: passes. Range: fixed 6 (specs/ai_budget_queues.md B.1.2 step 4).</summary>
        public const int MaxPasses = 6;

        /// <summary>The queue IDs of the data files, in queue order (the tie-break order everywhere).</summary>
        public static readonly string[] QueueIds = { "economy", "expansion", "production", "army", "tech", "defence" };

        /// <summary>Index of an account or want of <paramref name="queue"/> in <paramref name="resource"/>.</summary>
        /// <param name="queue">Queue index. Range: 0 to 5.</param>
        /// <param name="resource">0 common, 1 rare.</param>
        public static int Cell(int queue, int resource)
        {
            return queue * 2 + resource;
        }

        /// <summary>Sum of the accounts of one resource over all queues. Unit: resource units. Range: 0 upward.</summary>
        /// <param name="accounts">The 12 accounts.</param>
        /// <param name="resource">0 common, 1 rare.</param>
        public static long Sum(int[] accounts, int resource)
        {
            // Running total. Unit: resource units. Range: 0 upward.
            long total = 0;
            for (int q = 0; q < QueueCount; q++)
            {
                total += accounts[Cell(q, resource)];
            }
            return total;
        }

        /// <summary>
        /// The effective weight per queue and resource (B.1.2 step 3): the profile weights, the production weights times the opening factor while the drawn opening has not handed over, the defence weights times
        /// the threat factor while a threat is inside the base.
        /// </summary>
        /// <param name="common">Profile weights of free common in queue order. Unit: weight. Range: 0 to 1000 each; 6 entries.</param>
        /// <param name="rare">Profile weights of free rare in queue order. Unit: weight. Range: 0 to 1000 each; 6 entries.</param>
        /// <param name="productionFactor">budget_opening_production_factor. Unit: factor. Range: 1 to 20.</param>
        /// <param name="defenceFactor">budget_threat_defence_factor. Unit: factor. Range: 1 to 20.</param>
        /// <param name="inOpening">True before the drawn opening's handover (never without an opening).</param>
        /// <param name="underThreat">True while a threat is inside the base.</param>
        public static int[] EffectiveWeights(IReadOnlyList<int> common, IReadOnlyList<int> rare, int productionFactor, int defenceFactor, bool inOpening, bool underThreat)
        {
            // Weights per queue and resource. Unit: weight. Range: 0 to 20000.
            int[] weights = new int[Cells];
            for (int q = 0; q < QueueCount; q++)
            {
                // Multiplier of this queue. Unit: factor. Range: 1 to 20.
                int factor = q == Production && inOpening ? productionFactor : q == Defence && underThreat ? defenceFactor : 1;
                weights[Cell(q, Common)] = common[q] * factor;
                weights[Cell(q, Rare)] = rare[q] * factor;
            }
            return weights;
        }

        /// <summary>The rare-surplus flag after one think (B.1.2 step 5): set at or above <paramref name="on"/>, cleared below <paramref name="off"/> (or always when <paramref name="on"/> is 0), else unchanged.</summary>
        /// <param name="active">The flag as it was. Unit: boolean.</param>
        /// <param name="freeRare">Rare in no account. Unit: resource units. Range: 0 upward.</param>
        /// <param name="on">budget_rare_surplus_on. Unit: resource units. Range: 0 to 100000.</param>
        /// <param name="off">budget_rare_surplus_off. Unit: resource units. Range: 0 to on.</param>
        public static bool NextSurplus(bool active, long freeRare, int on, int off)
        {
            if (on <= 0)
            {
                return false;
            }
            return freeRare >= on || (active && freeRare >= off);
        }

        /// <summary>A copy of a composition in which every unit that costs rare has its weight multiplied by <paramref name="factor"/> (ordinal ID order).</summary>
        /// <param name="mix">The composition. Unit: weight. Range: 0 upward each.</param>
        /// <param name="costsRare">True for a unit ID whose definition has a rare cost.</param>
        /// <param name="factor">budget_rare_surplus_weight_factor. Unit: factor. Range: 1 to 20.</param>
        public static SortedDictionary<string, int> BoostRare(IReadOnlyDictionary<string, int> mix, Func<string, bool> costsRare, int factor)
        {
            // The boosted copy.
            SortedDictionary<string, int> boosted = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in mix)
            {
                boosted[pair.Key] = costsRare(pair.Key) ? pair.Value * factor : pair.Value;
            }
            return boosted;
        }

        /// <summary>
        /// Open steps 2 to 4 for both resources (step 1, no income, and step 5, the rare surplus, belong to the caller): clamps the accounts to the bank, releases the accounts of paused queues, then
        /// distributes the free money to the queues that want more. Changes <paramref name="accounts"/> only.
        /// </summary>
        /// <param name="accounts">The 12 accounts, carried from the last Close; updated. Unit: resource units. Range: 0 upward each.</param>
        /// <param name="wants">The 12 wants carried from the last Close. Unit: resource units. Range: 0 upward each.</param>
        /// <param name="bank">The stock now, [common, rare]. Unit: resource units. Range: 0 upward each.</param>
        /// <param name="paused">True for a queue that gets nothing and buys nothing this think; 6 entries.</param>
        /// <param name="weights">Effective weight per queue and resource (queue x 2 + resource). Unit: weight. Range: 0 upward each.</param>
        public static void Open(int[] accounts, int[] wants, int[] bank, bool[] paused, int[] weights)
        {
            for (int r = 0; r < 2; r++)
            {
                // Step 2: the accounts may not exceed the bank; the excess is taken from the last queue first. Unit: resource units. Range: 0 upward.
                long excess = Sum(accounts, r) - bank[r];
                for (int q = QueueCount - 1; q >= 0 && excess > 0; q--)
                {
                    // Taken from this queue. Unit: resource units. Range: 0 to the account.
                    int take = (int)Math.Min(excess, accounts[Cell(q, r)]);
                    accounts[Cell(q, r)] -= take;
                    excess -= take;
                }
                // Step 3: a paused queue releases its money.
                for (int q = 0; q < QueueCount; q++)
                {
                    if (paused[q])
                    {
                        accounts[Cell(q, r)] = 0;
                    }
                }
                // Step 4: what each unpaused queue still needs, then the proportional split of the free money.
                // Still needed per queue. Unit: resource units. Range: 0 upward.
                int[] need = new int[QueueCount];
                // Weight per queue for this resource. Unit: weight. Range: 0 upward.
                int[] weight = new int[QueueCount];
                for (int q = 0; q < QueueCount; q++)
                {
                    need[q] = !paused[q] && wants[Cell(q, r)] > accounts[Cell(q, r)] ? wants[Cell(q, r)] - accounts[Cell(q, r)] : 0;
                    weight[q] = weights[Cell(q, r)];
                }
                // Free money. Unit: resource units. Range: 0 upward.
                long free = Math.Max(0, bank[r] - Sum(accounts, r));
                // Granted per queue. Unit: resource units. Range: 0 upward.
                int[] grant = new int[QueueCount];
                Distribute(free, need, weight, grant);
                for (int q = 0; q < QueueCount; q++)
                {
                    accounts[Cell(q, r)] += grant[q];
                }
            }
        }

        /// <summary>
        /// Open step 4 for one resource: up to <see cref="MaxPasses"/> proportional passes over the queues that still need money and have a weight above 0 (each gets min(need, floor(base x weight / W)) with base the
        /// free money at the start of the pass and W the weight sum of those queues), then the leftover in queue order to any queue that still needs money, weight 0 included. Returns the money no queue wanted.
        /// </summary>
        /// <param name="free">Free money. Unit: resource units. Range: 0 upward.</param>
        /// <param name="need">What each queue still needs (0 for none, paused or satisfied); 6 entries; updated to what is still missing. Unit: resource units. Range: 0 upward.</param>
        /// <param name="weight">Weight per queue; 6 entries. Unit: weight. Range: 0 upward.</param>
        /// <param name="grant">Receives the money given per queue (added to); 6 entries. Unit: resource units. Range: 0 upward.</param>
        public static long Distribute(long free, int[] need, int[] weight, int[] grant)
        {
            // Money still free. Unit: resource units. Range: 0 upward.
            long remaining = free;
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                // Weight sum of the queues that still need money. Unit: weight. Range: 0 upward.
                long sum = 0;
                for (int q = 0; q < QueueCount; q++)
                {
                    if (need[q] > 0 && weight[q] > 0)
                    {
                        sum += weight[q];
                    }
                }
                if (sum == 0)
                {
                    break;
                }
                // Free money at the start of this pass. Unit: resource units. Range: 0 upward.
                long baseAmount = remaining;
                // Money given in this pass. Unit: resource units. Range: 0 upward.
                long given = 0;
                for (int q = 0; q < QueueCount; q++)
                {
                    if (need[q] <= 0 || weight[q] <= 0)
                    {
                        continue;
                    }
                    // This queue's proportional share, capped by its need. Unit: resource units. Range: 0 to need.
                    int g = (int)Math.Min(need[q], baseAmount * weight[q] / sum);
                    grant[q] += g;
                    need[q] -= g;
                    remaining -= g;
                    given += g;
                }
                if (given == 0)
                {
                    break;
                }
            }
            for (int q = 0; q < QueueCount; q++)
            {
                if (need[q] > 0 && remaining > 0)
                {
                    // Leftover given to this queue. Unit: resource units. Range: 0 to need.
                    int g = (int)Math.Min(need[q], remaining);
                    grant[q] += g;
                    need[q] -= g;
                    remaining -= g;
                }
            }
            return remaining;
        }

        /// <summary>
        /// Close: a queue with no want this think (it bought its head, had none or was paused) holds nothing; a queue with a want holds at most that want. The wants of this think replace the stored ones.
        /// </summary>
        /// <param name="accounts">The 12 accounts; updated. Unit: resource units. Range: 0 upward each.</param>
        /// <param name="storedWants">The 12 stored wants; replaced by <paramref name="thisWants"/>. Unit: resource units.</param>
        /// <param name="thisWants">The wants recorded this think (the head's cost per resource); 12 entries. Unit: resource units. Range: 0 upward each.</param>
        /// <param name="hasWant">True for a queue whose head failed on money this think; 6 entries.</param>
        public static void Close(int[] accounts, int[] storedWants, int[] thisWants, bool[] hasWant)
        {
            for (int q = 0; q < QueueCount; q++)
            {
                for (int r = 0; r < 2; r++)
                {
                    // Cell of this queue and resource. Range: 0 to 11.
                    int cell = Cell(q, r);
                    storedWants[cell] = hasWant[q] ? thisWants[cell] : 0;
                    accounts[cell] = hasWant[q] ? Math.Min(accounts[cell], thisWants[cell]) : 0;
                }
            }
        }

        /// <summary>
        /// The defence ceiling (B.1.2): false when <paramref name="sharePt"/> is above 0 and (defence spent + c) x 10000 is above sharePt x (all spent + c), so the defence share of all spending stays at or under the share.
        /// </summary>
        /// <param name="defenceSpent">Common plus rare spent by the defence queue so far. Unit: resource units. Range: 0 upward.</param>
        /// <param name="totalSpent">Common plus rare spent by all queues so far. Unit: resource units. Range: 0 upward.</param>
        /// <param name="cost">Common plus rare cost of the item. Unit: resource units. Range: 0 upward.</param>
        /// <param name="sharePt">budget_defence_max_spend_share as parts per 10 000 (0 = no ceiling). Unit: parts per 10 000. Range: 0 to 10000.</param>
        public static bool DefenceAllowed(long defenceSpent, long totalSpent, long cost, int sharePt)
        {
            return sharePt <= 0 || (defenceSpent + cost) * 10000 <= sharePt * (totalSpent + cost);
        }
    }
}

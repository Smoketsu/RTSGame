// rts/src/Ai/AiBrainBudget.cs
// Package B of the skirmish AI (specs/ai_budget_queues.md B.1): the budget steps of one think and the buying helpers of the six queues. BudgetOpen runs after rule 2 (Gather) and before rule 3 (the first rule that buys): it
// clamps, pauses and funds the queue accounts from the free money (AiBudget.Open). BudgetClose runs after rule 5b (the last rule that buys): it releases what the queues no longer want. In between the buying rules
// call BqCanPay, BqWant, BqBlocked and DefenceAllows instead of rule R's reservations. The rare-surplus decision (step 5) runs in the Turn constructor (BudgetComposition) because the composition is built there.
// Everything is a no-op while use_budget_queues is false, and for a think without income (rule R's no-deadlock branch); state is in AiMemory. Integers only. Part of Rts.Ai.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    public static partial class AiBrain
    {
        private sealed partial class Turn
        {
            /// <summary>True when this think buys through the budget queues: use_budget_queues is on and some own worker is earning (without income the bank is spent as before).</summary>
            private bool _bq;

            /// <summary>The wants recorded this think, one per queue and resource (queue x 2 + resource): the cost of the head item that failed on money. Unit: resource units. Range: 0 upward.</summary>
            private readonly int[] _thisWant = new int[AiBudget.Cells];

            /// <summary>True for a queue whose head failed on money this think (nothing more is bought for it this think).</summary>
            private readonly bool[] _hasWant = new bool[AiBudget.QueueCount];

            /// <summary>True for a queue that is paused this think (workers below the floor): it gets nothing and buys nothing.</summary>
            private readonly bool[] _paused = new bool[AiBudget.QueueCount];

            /// <summary>Common in no queue's account after this think's purchases so far (the free money F). Unit: resource units. Range: 0 upward.</summary>
            private int FreeCommon()
            {
                return _common - (int)AiBudget.Sum(_memory.BudgetAccounts, AiBudget.Common);
            }

            /// <summary>Rare in no queue's account after this think's purchases so far (the free money F). Unit: resource units. Range: 0 upward.</summary>
            private int FreeRare()
            {
                return _rare - (int)AiBudget.Sum(_memory.BudgetAccounts, AiBudget.Rare);
            }

            /// <summary>
            /// Open (B.1.2): with no income every account and want is cleared and this think spends as before; otherwise the paused queues, the effective weights (the opening's production factor, the threat's defence
            /// factor) and the distribution of the free money are worked out (AiBudget.Open). Does nothing while use_budget_queues is false.
            /// </summary>
            public void BudgetOpen()
            {
                if (!_profile.UseBudgetQueues)
                {
                    return;
                }
                if (!_bq)
                {
                    Array.Clear(_memory.BudgetAccounts, 0, AiBudget.Cells);
                    Array.Clear(_memory.BudgetWants, 0, AiBudget.Cells);
                    return;
                }
                // Own workers alive, queued ones not counted. Unit: workers. Range: 0 upward.
                int workers = 0;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    workers += unit.IsWorker ? 1 : 0;
                }
                if (_profile.BudgetWorkerFloor > 0 && workers < _profile.BudgetWorkerFloor)
                {
                    _paused[AiBudget.Army] = true;
                    _paused[AiBudget.Defence] = true;
                }
                // Effective weight per queue and resource. Unit: weight. Range: 0 to 20000.
                int[] weights = AiBudget.EffectiveWeights(_profile.BudgetCommonWeights, _profile.BudgetRareWeights, _profile.BudgetOpeningProductionFactor, _profile.BudgetThreatDefenceFactor,
                    _memory.Variety != null && _memory.Variety.InOpening(_view.Tick), UnderThreat);
                AiBudget.Open(_memory.BudgetAccounts, _memory.BudgetWants, new[] { _common, _rare }, _paused, weights);
            }

            /// <summary>Close (B.1.2): a queue without a want holds nothing, one with a want at most that want; this think's wants are stored. Does nothing without budget queues or income.</summary>
            public void BudgetClose()
            {
                if (_bq)
                {
                    AiBudget.Close(_memory.BudgetAccounts, _memory.BudgetWants, _thisWant, _hasWant);
                }
            }

            /// <summary>
            /// Step 5 of Open (the rare surplus) and its effect on the composition: from the accounts as closed last think, free rare at or above budget_rare_surplus_on sets the flag and below
            /// budget_rare_surplus_off clears it (on 0: always clear); while it is set a copy of <paramref name="mix"/> has every unit that costs rare weighted by budget_rare_surplus_weight_factor. A think without
            /// income leaves the flag as it was. Returns <paramref name="mix"/> itself while the switch is off or the flag is clear.
            /// </summary>
            /// <param name="mix">The composition of this think (counter-mix or the plan's). Unit: weight. Range: 0 upward each.</param>
            private IReadOnlyDictionary<string, int> BudgetComposition(IReadOnlyDictionary<string, int> mix)
            {
                if (!_profile.UseBudgetQueues)
                {
                    return mix;
                }
                if (_income)
                {
                    // Rare in no account, from the accounts as closed last think. Unit: resource units. Range: 0 upward.
                    long freeRare = Math.Max(0, _view.Rare - AiBudget.Sum(_memory.BudgetAccounts, AiBudget.Rare));
                    _memory.RareSurplusActive = AiBudget.NextSurplus(_memory.RareSurplusActive, freeRare, _profile.BudgetRareSurplusOn, _profile.BudgetRareSurplusOff);
                }
                if (!_memory.RareSurplusActive)
                {
                    return mix;
                }
                return AiBudget.BoostRare(mix, id => _view.Content.Units.TryGetValue(id, out UnitDefinition? unit) && unit.CostRare > 0, _profile.BudgetRareSurplusWeightFactor);
            }

            /// <summary>True when queue <paramref name="queue"/> may buy nothing more this think: it is paused or its head already failed on money.</summary>
            /// <param name="queue">Queue index. Range: 0 to 5.</param>
            private bool BqBlocked(int queue)
            {
                return _paused[queue] || _hasWant[queue];
            }

            /// <summary>True when the account of <paramref name="queue"/> covers each resource <paramref name="definition"/> costs (a resource it does not cost is not checked).</summary>
            /// <param name="queue">Queue index. Range: 0 to 5.</param>
            /// <param name="definition">The item to buy.</param>
            private bool BqCanPay(int queue, UnitDefinition definition)
            {
                return (definition.CostCommon == 0 || _memory.BudgetAccounts[AiBudget.Cell(queue, AiBudget.Common)] >= definition.CostCommon)
                    && (definition.CostRare == 0 || _memory.BudgetAccounts[AiBudget.Cell(queue, AiBudget.Rare)] >= definition.CostRare);
            }

            /// <summary>Records <paramref name="definition"/> as the head of <paramref name="queue"/> that failed only on money this think: its cost becomes the queue's want and the queue buys nothing more this think.</summary>
            /// <param name="queue">Queue index. Range: 0 to 5.</param>
            /// <param name="definition">The head item.</param>
            private void BqWant(int queue, UnitDefinition definition)
            {
                _hasWant[queue] = true;
                _thisWant[AiBudget.Cell(queue, AiBudget.Common)] = definition.CostCommon;
                _thisWant[AiBudget.Cell(queue, AiBudget.Rare)] = definition.CostRare;
            }

            /// <summary>Takes the cost of a bought item from the queue's account and adds it to the queue's cumulative spending.</summary>
            /// <param name="queue">Queue index. Range: 0 to 5.</param>
            /// <param name="definition">The item bought.</param>
            private void BqPay(int queue, UnitDefinition definition)
            {
                _memory.BudgetAccounts[AiBudget.Cell(queue, AiBudget.Common)] -= definition.CostCommon;
                _memory.BudgetAccounts[AiBudget.Cell(queue, AiBudget.Rare)] -= definition.CostRare;
                _memory.BudgetSpent[AiBudget.Cell(queue, AiBudget.Common)] += definition.CostCommon;
                _memory.BudgetSpent[AiBudget.Cell(queue, AiBudget.Rare)] += definition.CostRare;
            }

            /// <summary>The defence ceiling (B.1.2): false when buying <paramref name="definition"/> would take the defence queue's share of all spending above budget_defence_max_spend_share.</summary>
            /// <param name="definition">The static defence to buy.</param>
            private bool DefenceAllows(UnitDefinition definition)
            {
                // Cumulative spending of the defence queue and of all queues, common plus rare. Unit: resource units. Range: 0 upward.
                long defence = _memory.BudgetSpent[AiBudget.Cell(AiBudget.Defence, AiBudget.Common)] + _memory.BudgetSpent[AiBudget.Cell(AiBudget.Defence, AiBudget.Rare)];
                long total = AiBudget.Sum(_memory.BudgetSpent, AiBudget.Common) + AiBudget.Sum(_memory.BudgetSpent, AiBudget.Rare);
                // The ceiling as parts per 10 000. Unit: parts per 10 000. Range: 0 to 10000.
                int sharePt = (_profile.BudgetDefenceMaxSpendShare * Fix64.FromInt(10000)).RoundToInt();
                return AiBudget.DefenceAllowed(defence, total, definition.CostCommon + definition.CostRare, sharePt);
            }
        }
    }
}

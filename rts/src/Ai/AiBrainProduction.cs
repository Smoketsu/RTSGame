// rts/src/Ai/AiBrainProduction.cs
// Production capacity and supply rules of the skirmish AI (rts/docs/specs/ai_production_defence.md packages PD-1, only with use_production_capacity, and PD-2, only with use_supply_ahead): the section 2 helpers (Producible, RateOf, CapOfProducer,
// IncomePerMinute, WorkerRate), the deposit log pruning, rule RR's money test, rule PC (build a producer while producer capacity is below army income; replaces rule X) and rule SA (order a supply structure ahead of what the producers can use). Part of Rts.Ai (a partial of AiBrain's Turn).
// Reads only the view, the profile and memory (the deposit log holds the player's own deposits). No random draw. Integers and Fix64 only; own structures walked in the ascending ID order of the planned list.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    public static partial class AiBrain
    {
        /// <summary>
        /// Drops the deposit-log entries at or below now minus income_window_seconds (spec section 2); entries stay in tick order.
        /// </summary>
        /// <param name="view">The view of this think; only its tick is read.</param>
        /// <param name="profile">The profile; income_window_seconds is read.</param>
        /// <param name="memory">The controller's memory; its deposit log is pruned.</param>
        private static void PruneDepositLog(AiWorldView view, AiProfile profile, AiMemory memory)
        {
            // Oldest tick that still counts (exclusive). Unit: ticks. Range: any int (negative early in the match).
            int oldest = view.Tick - view.SecondsToTicks(Fix64.FromInt(profile.IncomeWindowSeconds));
            memory.DepositLog.RemoveAll(entry => entry.Tick <= oldest);
        }

        /// <summary>
        /// Test hook (rules RR and PC): runs one think's intel and a Turn, then either rule 3 (<paramref name="structures"/>) or rule P followed by rule PC's slot (rule X without the switch), and reports the reservations.
        /// Returns null when the player has no HQ. Reservations are in resource units, 0 upward; "before" is read just before the rule PC slot (rule 3 has none: before is 0).
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The AI profile.</param>
        /// <param name="plan">The faction plan.</param>
        /// <param name="memory">The controller's memory; updated.</param>
        /// <param name="structures">True to run rule 3 only; false to run Produce and then ExtraProducer.</param>
        internal static (int CommonBefore, int RareBefore, int Common, int Rare, List<AiIntent> Intents)? ProbeReserves(AiWorldView view, AiProfile profile, AiFactionPlan plan, AiMemory memory, bool structures)
        {
            // The own HQ (lowest ID), or null when the player has none.
            AiUnitView? hq = FindHq(view);
            UpdateIntel(view, profile, memory, hq);
            if (hq == null)
            {
                return null;
            }
            // Intents of the probed rules.
            List<AiIntent> intents = new List<AiIntent>();
            // This think's working state.
            Turn turn = new Turn(view, profile, plan, memory, new DeterministicRandom(1), hq, intents);
            if (structures)
            {
                turn.Structures();
                (int common, int rare) = turn.Reserves();
                return (0, 0, common, rare, intents);
            }
            turn.Produce();
            (int commonBefore, int rareBefore) = turn.Reserves();
            turn.ExtraProducer();
            (int commonAfter, int rareAfter) = turn.Reserves();
            return (commonBefore, rareBefore, commonAfter, rareAfter, intents);
        }

        private sealed partial class Turn
        {
            /// <summary>Test hook: the common and rare held back so far this think. Unit: resource units. Range: 0 upward.</summary>
            public (int Common, int Rare) Reserves()
            {
                return (_reserveCommon, _reserveRare);
            }

            /// <summary>Rule RR: true when the switch is on and the common part of a cost can be paid after the reservations, so only the rare part is missing. Unit: boolean.</summary>
            /// <param name="costCommon">Common cost of the item. Unit: resource units. Range: 0 upward.</param>
            private bool CommonPaidUnderRr(int costCommon)
            {
                return _profile.UseProductionCapacity && (costCommon == 0 || _common - _reserveCommon >= costCommon);
            }

            /// <summary>Ticks a unit takes to build, the AI-side copy of EconomyRules.BuildTicks: at least 1. Unit: ticks. Range: 1 upward.</summary>
            /// <param name="definition">The unit or structure.</param>
            private int BuildTicks(UnitDefinition definition)
            {
                return Math.Max(1, _view.SecondsToTicks(definition.Economy.BuildSeconds));
            }

            /// <summary>
            /// Spec section 2 Producible: a composition unit (weight above 0) whose requirements are met and whose rare cost is 0, or is paid by a served rare field, or fits the rare stock minus reservations.
            /// </summary>
            /// <param name="unitId">Unit ID. Range: any ID of the catalogue; a unit that is not a composition unit is not producible.</param>
            private bool Producible(string unitId)
            {
                // Composition weight of the unit. Unit: weight. Range: 0 to 100.
                if (!_composition.TryGetValue(unitId, out int weight) || weight <= 0)
                {
                    return false;
                }
                // Definition of the unit.
                UnitDefinition unit = _view.Content.Units[unitId];
                return RequirementsMet(unit) && (unit.CostRare == 0 || RareServed() || unit.CostRare <= _rare - _reserveRare);
            }

            /// <summary>Common per minute one producer turns into this unit: cost x 60 x ticks per second / build ticks, integer division. Unit: common per minute. Range: 0 upward.</summary>
            /// <param name="unit">The unit.</param>
            private int RateOf(UnitDefinition unit)
            {
                return unit.CostCommon * 60 * _view.TicksPerSecond / BuildTicks(unit);
            }

            /// <summary>The largest <see cref="RateOf"/> over the producible units the structure trains; 0 when none. Unit: common per minute. Range: 0 upward.</summary>
            /// <param name="producer">An own structure (complete, under construction or a pending order).</param>
            private int CapOfProducer(AiUnitView producer)
            {
                // Largest rate so far. Unit: common per minute. Range: 0 upward.
                int best = 0;
                foreach (string trained in producer.Definition.Economy.Trains)
                {
                    if (Producible(trained))
                    {
                        best = Math.Max(best, RateOf(_view.Content.Units[trained]));
                    }
                }
                return best;
            }

            /// <summary>Common deposited in the last income_window_seconds, per minute: the sum times 60 over the window. Unit: common per minute. Range: 0 upward.</summary>
            private int IncomePerMinute()
            {
                // Oldest tick that still counts (exclusive). Unit: ticks.
                int oldest = _view.Tick - _view.SecondsToTicks(Fix64.FromInt(_profile.IncomeWindowSeconds));
                // Common deposited inside the window. Unit: resource units. Range: 0 upward.
                long sum = 0;
                foreach ((int Tick, int Common) entry in _memory.DepositLog)
                {
                    if (entry.Tick > oldest)
                    {
                        sum += entry.Common;
                    }
                }
                return (int)(sum * 60 / _profile.IncomeWindowSeconds);
            }

            /// <summary>The rate at which the Rooted HQ turns common into workers while workers are still wanted, else 0. Unit: common per minute. Range: 0 upward.</summary>
            private int WorkerRate()
            {
                if (_hq.HqState != HqState.Rooted || _hq.IsUnderConstruction || WorkersIncludingQueued() >= WantedWorkers())
                {
                    return 0;
                }
                return RateOf(_view.Content.Units[_plan.WorkerUnitId]);
            }

            /// <summary>
            /// Rule PC (spec section 3.2): builds at most one producer when the producers' capacity is below the army income. Skips on any failed step and never reserves money.
            /// </summary>
            private void ProductionCapacity()
            {
                // Ticks of one full income window. Unit: ticks. Range: 200 to 6000 at 20 ticks per second.
                int windowTicks = _view.SecondsToTicks(Fix64.FromInt(_profile.IncomeWindowSeconds));
                if (_view.Tick < windowTicks)
                {
                    return;
                }
                // Army income: measured income less what the workers' own training takes. Unit: common per minute. Range: 0 upward.
                int income = Math.Max(0, IncomePerMinute() - WorkerRate());
                // Capacity of the own producers, built or planned. Unit: common per minute. Range: 0 upward.
                int capacity = 0;
                // Own structures that train a composition unit. Unit: structures. Range: 0 upward.
                int producers = 0;
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure)
                    {
                        capacity += CapOfProducer(unit);
                        if (TrainsCompositionUnit(unit.Definition))
                        {
                            producers++;
                        }
                    }
                }
                if (Fix64.FromInt(capacity) >= Fix64.FromInt(income) * _profile.ProductionCapacityTarget)
                {
                    return;
                }
                // Squared defend radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.DefendRadiusTiles * _profile.DefendRadiusTiles;
                foreach (AiUnitView enemy in _view.VisibleEnemies)
                {
                    if (enemy.IsArmy && (enemy.Position - _hq.Position).LengthSquared() <= radiusSquared)
                    {
                        // The main is under attack: money goes to the army.
                        return;
                    }
                }
                if (producers >= _profile.MaxProducers)
                {
                    return;
                }
                // The producible composition unit with the largest deficit that no free producer can train.
                string? choice = PickByDeficit(_composition, _counts, id => Producible(id) && FindFreeProducer(id) == null);
                if (choice == null)
                {
                    return;
                }
                // The structure that trains it.
                UnitDefinition? structure = ProducerStructureFor(choice);
                if (structure == null)
                {
                    return;
                }
                if (PayableNow(structure, AiBudget.Production))
                {
                    TryBuild(structure, null, true, AiBudget.Production);
                }
            }

            /// <summary>
            /// True when <paramref name="definition"/> can be paid now, so nothing needs reserving (rule PC step 8, rule SA step 4): the stock minus reservations (<see cref="CanPay"/>), or with budget queues the queue's account plus the free money.
            /// A resource the definition does not cost is not checked.
            /// </summary>
            /// <param name="definition">The structure.</param>
            /// <param name="queue">Budget queue that would pay (used only with budget queues). Range: 0 to 5.</param>
            private bool PayableNow(UnitDefinition definition, int queue)
            {
                if (!_bq)
                {
                    return CanPay(definition);
                }
                return (definition.CostCommon == 0 || _memory.BudgetAccounts[AiBudget.Cell(queue, AiBudget.Common)] + FreeCommon() >= definition.CostCommon)
                    && (definition.CostRare == 0 || _memory.BudgetAccounts[AiBudget.Cell(queue, AiBudget.Rare)] + FreeRare() >= definition.CostRare);
            }

            /// <summary>
            /// Rule SA (spec section 4), after rule 3a: orders one more supply structure when the supply the producers can use during one supply build would not fit under the cap. Reserves nothing.
            /// </summary>
            private void SupplyAhead()
            {
                // Supply of the supply structures under construction or ordered, and their number. Unit: supply and structures. Range: 0 upward.
                int pendingSupply = 0;
                int pending = 0;
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure && unit.IsUnderConstruction && unit.Definition.Economy.ProvidesSupply > 0)
                    {
                        pendingSupply += unit.Definition.Economy.ProvidesSupply;
                        pending++;
                    }
                }
                if (pending >= _profile.SupplyAheadMaxPending || _view.SupplyCap + pendingSupply >= _view.SupplyCapLimit)
                {
                    return;
                }
                // The supply structure and the ticks one takes to build. Unit: definition and ticks (1 upward).
                UnitDefinition supplyBuilding = _view.Content.Units[_plan.SupplyBuildingId];
                int buildTicks = BuildTicks(supplyBuilding);
                // Supply the producers can add during one supply build (N). Unit: supply. Range: 0 upward.
                int need = 0;
                foreach (AiUnitView producer in _view.OwnUnits)
                {
                    if (!producer.IsStructure || producer.IsUnderConstruction)
                    {
                        continue;
                    }
                    // Largest supply this producer can add during one supply build, over its producible units. Unit: supply. Range: 0 upward.
                    int best = 0;
                    foreach (string trained in producer.Definition.Economy.Trains)
                    {
                        if (Producible(trained))
                        {
                            UnitDefinition unit = _view.Content.Units[trained];
                            best = Math.Max(best, (_view.SupplyOf(unit) * buildTicks + BuildTicks(unit) - 1) / BuildTicks(unit));
                        }
                    }
                    need += best;
                }
                if (WorkerRate() > 0)
                {
                    UnitDefinition worker = _view.Content.Units[_plan.WorkerUnitId];
                    need += (buildTicks + BuildTicks(worker) - 1) / BuildTicks(worker) * _view.SupplyOf(worker);
                }
                if (_view.SupplyCap + pendingSupply - _view.SupplyUsed >= need + _profile.SupplyBuffer || !PayableNow(supplyBuilding, AiBudget.Economy))
                {
                    return;
                }
                TryBuild(supplyBuilding, null, false, AiBudget.Economy);
            }
        }
    }
}

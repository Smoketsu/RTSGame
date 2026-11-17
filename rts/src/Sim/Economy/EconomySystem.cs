// rts/src/Sim/Economy/EconomySystem.cs
// The Phase 1 economy: the worker gather loop, production queues, structure construction, footprints and supply. Part of
// Rts.Sim.Economy. Runs once per tick inside Simulation.Step after combat (step order in Simulation.cs). All work walks units in
// ascending entity ID order over a snapshot of the IDs, so units spawned this tick (trained units) first act next tick.
//
// Worker loop (GatherState): ToField -> Gathering -> ToDropOff -> deposit -> ToField ... until the field is empty.
//   * ToField: the worker walks to the field's tile centre. It counts as arrived within interact_range_tiles (never less than
//     the movement arrival radius). A worker that stops short of the field (no move target left and not in range: unreachable)
//     gives up and goes Idle.
//   * Gathering: waits gather_seconds_per_trip, then takes min(gather_amount_per_trip, remaining) from the field. Any number of
//     workers may share a field (Phase 1 placeholder: no slot limit). If that empties the field, FieldDepletedEvent is emitted.
//   * ToDropOff: walks to the owner's nearest drop-off (a Rooted HQ or a complete is_drop_off structure; re-evaluated every tick,
//     ties to the lowest ID) and deposits when within interact_range_tiles of its edge; ResourceDepositedEvent follows. With no
//     drop-off the worker goes Idle and keeps its load. After the deposit the worker returns to the field, or goes Idle when
//     the field is empty.
//   * Moves, attack orders and stop cancel the loop (the load stays carried; a later gather order deposits it first).
// Production: each producer has one queue, head first. The head counts down build_seconds (in ticks) while the producer is
//   complete (and, for an HQ, Rooted); at 0 the unit appears on the first free tile next to the producer (EconomyRules.
//   TryFindSpawnPosition) and UnitTrainedEvent is emitted; with no free tile it waits and retries next tick. A queued unit
//   reserves its supply and its cost is paid when queued; there is no refund (placeholder).
// Build orders (BuildOrderState; specs/worker_construction.md): BuildStructureCommand pays the cost, records the order on the
//   worker and sends it to the site (ToSite). StepBuild runs after StepGather and before StepProduction, when the worker has
//   BuildState != None.
//   * ToSite: within reach (interact range plus the structure's radius, to the site centre) the worker stops, the site is
//     checked (blocked by a structure, or a non-air unit stands on the tile) and the placement countdown of
//     worker_placement_seconds starts (BuildOrderStartedEvent). A site failure cancels the order (site_blocked or
//     site_occupied); a walk that ends short cancels it (unreachable). Both refund the cost and send the worker back to the
//     gather field it left.
//   * Placing: counts down one tick per step; at 0 the site is checked again and the structure is spawned under construction
//     (StructurePlacedEvent). The worker is free again at once. A countdown set on arrival tick A ends on tick A + P.
//   * The structure starts at construction_start_health_fraction of its health and gains the rest evenly over build_seconds
//     (StepConstruction). Its countdown first runs on the tick AFTER it is placed, because the spawned unit is not in this
//     step's snapshot, so it completes on A + P + build_ticks, one tick later than the old rule (a structure placed by command
//     on tick T completed on T + build_ticks - 1). While under construction it is no drop-off, producer or supply source and
//     its weapon is off. StructureCompletedEvent is emitted at the end.
//   * CancelBuild refunds the definition's cost and emits BuildOrderCancelledEvent. Accepted move, attack, stop, gather and build
//     orders cancel with new_order; a removed worker cancels with worker_lost (OnUnitRemoved, so deaths in step 6d).
// Footprint: a unit whose definition has footprint 1 adds a Footprint blocker to its tile at spawn and removes it when it
//   is removed (OnUnitRemoved), unless another footprint unit shares the tile.
// Supply: RecomputeSupply sums, per player, the supply of every living unit plus every queued unit plus every pending build
//   order (replaced by the structure itself when it appears), and the supply cap. It
//   scans all units, O(n) per call; it runs at the end of every economy step and before each train or build command (so two
//   commands in one tick see each other). Cheap at Phase 1 sizes; the planned replacement is incremental counters once unit
//   counts need it.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Economy
{
    /// <summary>Gathering, production, construction and supply for every player.</summary>
    public static class EconomySystem
    {
        /// <summary>Slows a worker that carries resources by worker_speed_multiplier_while_carrying (tuning/economy.json). Registered with the simulation at match start.</summary>
        private sealed class CarryingSpeedModifier : ISpeedModifier
        {
            /// <summary>Stable modifier ID; fixes the multiplication order. Range: fixed text "economy.carrying".</summary>
            public string Id => "economy.carrying";

            /// <summary>The tuning multiplier while the unit carries anything, else 1. Unit: multiplier of base speed. Range: 0.1 to 2.</summary>
            public Fix64 SpeedMultiplier(Simulation sim, Unit unit)
            {
                return unit.CarriedAmount > 0 ? sim.Tuning.Economy.WorkerSpeedMultiplierWhileCarrying : Fix64.One;
            }
        }

        /// <summary>Gives every player the starting stock and works out the first supply figures. Internal: <see cref="Simulation.Start"/> only.</summary>
        internal static void OnMatchStart(Simulation sim)
        {
            foreach (Player player in sim.Players.All)
            {
                player.SetStock(sim.Tuning.Economy.StartingCommon, sim.Tuning.Economy.StartingRare);
            }
            RecomputeSupply(sim);
        }

        /// <summary>Registers the carrying speed modifier. Internal: <see cref="Simulation.Start"/> only, inside the window where modifiers may be registered.</summary>
        internal static void RegisterModifiers(Simulation sim)
        {
            sim.RegisterSpeedModifier(new CarryingSpeedModifier());
        }

        /// <summary>Adds the footprint blocker of a new structure. Internal: <see cref="Simulation.SpawnUnit"/> only.</summary>
        internal static void OnUnitSpawned(Simulation sim, Unit unit)
        {
            if (unit.Definition.Economy.FootprintTiles > 0)
            {
                sim.Terrain.AddBlockers(sim, sim.Terrain.TileOf(unit.Position), TileBlockers.Footprint);
            }
        }

        /// <summary>Removes the footprint blocker of a removed structure unless another footprint unit shares its tile. Internal: <see cref="Simulation.RemoveUnit"/> only.</summary>
        internal static void OnUnitRemoved(Simulation sim, Unit removed)
        {
            // A removed worker's pending build order is refunded; the unit is gone, so there is nothing to resume.
            CancelBuild(sim, removed, "worker_lost", false);
            if (removed.Definition.Economy.FootprintTiles <= 0)
            {
                return;
            }
            // Tile the structure covered.
            int tile = sim.Terrain.TileOf(removed.Position);
            foreach (Unit other in sim.Units.All)
            {
                if (other.Definition.Economy.FootprintTiles > 0 && sim.Terrain.TileOf(other.Position) == tile)
                {
                    return;
                }
            }
            sim.Terrain.RemoveBlockers(sim, tile, TileBlockers.Footprint);
        }

        /// <summary>
        /// Recomputes every player's supply used and cap from the units that exist and the queues. A unit owned by nobody or by an
        /// unknown player counts for nobody. Unit: supply. See the file header for the cost.
        /// </summary>
        internal static void RecomputeSupply(Simulation sim)
        {
            if (!sim.IsStepping)
            {
                throw new InvalidOperationException("EconomySystem.RecomputeSupply can only run inside Simulation.Start or Simulation.Step.");
            }
            // Supply used and supply provided per player, in ascending player ID order.
            SortedDictionary<PlayerId, int> used = new SortedDictionary<PlayerId, int>();
            SortedDictionary<PlayerId, int> provided = new SortedDictionary<PlayerId, int>();
            foreach (Player player in sim.Players.All)
            {
                used[player.Id] = 0;
                provided[player.Id] = 0;
            }
            foreach (Unit unit in sim.Units.All)
            {
                if (!used.ContainsKey(unit.Owner))
                {
                    continue;
                }
                used[unit.Owner] += EconomyRules.SupplyOf(sim, unit.Definition);
                if (unit.BuildState != BuildOrderState.None && sim.Content.Units.TryGetValue(unit.BuildDefinitionId, out UnitDefinition? pendingDefinition))
                {
                    used[unit.Owner] += EconomyRules.SupplyOf(sim, pendingDefinition);
                }
                foreach (string queued in unit.ProductionQueue)
                {
                    if (sim.Content.Units.TryGetValue(queued, out UnitDefinition? queuedDefinition))
                    {
                        used[unit.Owner] += EconomyRules.SupplyOf(sim, queuedDefinition);
                    }
                }
                if (!unit.IsUnderConstruction)
                {
                    provided[unit.Owner] += unit.Definition.Economy.ProvidesSupply;
                }
            }
            foreach (Player player in sim.Players.All)
            {
                // Cap before the global limit. Unit: supply.
                int cap = sim.Tuning.Economy.BaseSupply + provided[player.Id];
                player.SetSupply(used[player.Id], Math.Min(sim.Tuning.Supply.SupplyCap, cap));
            }
        }

        /// <summary>Runs one tick of gathering, production and construction, then refreshes supply. Internal: only <see cref="Simulation.Step"/> calls it.</summary>
        internal static void Step(Simulation sim)
        {
            if (!sim.IsStepping)
            {
                throw new InvalidOperationException("EconomySystem.Step can only run inside Simulation.Step.");
            }
            // Snapshot of live unit IDs in ascending order; units spawned during the loop are not visited this tick.
            List<EntityId> ids = sim.Units.IdsSnapshot();
            foreach (EntityId id in ids)
            {
                if (!sim.Units.TryGet(id, out Unit? unit))
                {
                    continue;
                }
                if (unit.IsUnderConstruction)
                {
                    StepConstruction(sim, unit);
                }
                if (unit.GatherState != GatherState.Idle)
                {
                    StepGather(sim, unit);
                }
                if (unit.BuildState != BuildOrderState.None)
                {
                    StepBuild(sim, unit);
                }
                if (unit.ProductionQueue.Count > 0)
                {
                    StepProduction(sim, unit);
                }
            }
            RecomputeSupply(sim);
        }

        /// <summary>Distance a worker must come within to gather or deposit: interact_range_tiles, never less than the movement arrival radius. Unit: tiles. Range: 0.5 to 4.</summary>
        private static Fix64 InteractRange(Simulation sim)
        {
            return Fix64.Max(sim.Tuning.Economy.InteractRangeTiles, sim.Tuning.Terrain.ArrivalRadiusTiles);
        }

        /// <summary>True when the centre distance from <paramref name="a"/> to <paramref name="b"/> is at most <paramref name="reach"/> (compared squared).</summary>
        private static bool Within(FixVec2 a, FixVec2 b, Fix64 reach)
        {
            // Offset between the points. Unit: tiles.
            FixVec2 delta = b - a;
            return delta.LengthSquared() <= reach * reach;
        }

        /// <summary>
        /// Puts a worker onto a gather loop toward a field, as the gather command does after its checks. The unit stays Idle when the
        /// field index is out of range or the field is depleted, or when it carries a load and the owner has no drop-off (the load is kept).
        /// Internal: the gather command and the build order code only.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="unit">The worker.</param>
        /// <param name="fieldIndex">Field to work. Unit: field index. Range: 0 to field count - 1, else the unit stays Idle.</param>
        internal static void StartGatherLoop(Simulation sim, Unit unit, int fieldIndex)
        {
            if (fieldIndex < 0 || fieldIndex >= sim.Resources.Count || sim.Resources[fieldIndex].IsDepleted)
            {
                return;
            }
            // The chosen field.
            ResourceFieldState field = sim.Resources[fieldIndex];
            if (unit.CarriedAmount > 0)
            {
                // Nearest drop-off for the load the worker still carries.
                Unit? dropOff = EconomyRules.NearestDropOff(sim, unit.Owner, unit.Position);
                if (dropOff == null)
                {
                    return;
                }
                unit.SetGather(GatherState.ToDropOff, fieldIndex, 0);
                unit.SetMoveTarget(EconomyRules.StandablePointNear(sim, dropOff.Position, unit.EffectiveLayer));
                return;
            }
            unit.SetGather(GatherState.ToField, fieldIndex, 0);
            unit.SetMoveTarget(EconomyRules.StandablePointNear(sim, sim.Terrain.CentreOf(sim.Terrain.IndexOf(field.X, field.Y)), unit.EffectiveLayer));
        }

        /// <summary>
        /// Ends a worker's build order with a refund of the structure's cost and a BuildOrderCancelledEvent. Does nothing when the
        /// worker has no order. Does not recompute supply: the callers do (command path before its supply check, economy step at its end).
        /// Internal: commands, the removal hook and the economy step.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="worker">The worker (it may already have left the registry).</param>
        /// <param name="reason">One of new_order, worker_lost, site_blocked, site_occupied, unreachable.</param>
        /// <param name="resume">True to send the worker back to the gather field it left, when there is one.</param>
        internal static void CancelBuild(Simulation sim, Unit worker, string reason, bool resume)
        {
            if (worker.BuildState == BuildOrderState.None)
            {
                return;
            }
            // Refund amounts. Unit: resource units. Range: 0 upward.
            int refundCommon = 0;
            int refundRare = 0;
            if (sim.Content.Units.TryGetValue(worker.BuildDefinitionId, out UnitDefinition? definition))
            {
                refundCommon = definition.CostCommon;
                refundRare = definition.CostRare;
            }
            if (sim.Players.TryGet(worker.Owner, out Player? owner))
            {
                owner.AddResource(ResourceKind.Common, refundCommon);
                owner.AddResource(ResourceKind.Rare, refundRare);
            }
            sim.Events.Add(new BuildOrderCancelledEvent(sim.Clock.Tick, worker.Id, worker.Owner, worker.BuildDefinitionId, worker.BuildTileX, worker.BuildTileY, reason, refundCommon, refundRare));
            // Gather field to go back to. Unit: field index. Range: -1 or more.
            int resumeField = worker.BuildResumeFieldIndex;
            worker.ClearBuildOrder();
            if (resume && resumeField >= 0)
            {
                StartGatherLoop(sim, worker, resumeField);
            }
        }

        /// <summary>
        /// Site check for a build order: "site_blocked" when the tile is blocked, "site_occupied" when a non-air unit stands on it
        /// (the builder included), else null.
        /// </summary>
        private static string? SiteProblem(Simulation sim, int tile)
        {
            if (sim.Terrain[tile].IsBlocked)
            {
                return "site_blocked";
            }
            foreach (Unit other in sim.Units.All)
            {
                if (other.EffectiveLayer != MovementLayer.Air && sim.Terrain.TileOf(other.Position) == tile)
                {
                    return "site_occupied";
                }
            }
            return null;
        }

        /// <summary>Advances one worker's build order by one tick (specs/worker_construction.md section 3.2).</summary>
        private static void StepBuild(Simulation sim, Unit worker)
        {
            if (!sim.Content.Units.TryGetValue(worker.BuildDefinitionId, out UnitDefinition? definition))
            {
                worker.ClearBuildOrder();
                return;
            }
            // Site tile index and centre. Unit: tile index; tiles.
            int tile = sim.Terrain.IndexOf(worker.BuildTileX, worker.BuildTileY);
            FixVec2 siteCentre = sim.Terrain.CentreOf(tile);
            if (worker.BuildState == BuildOrderState.ToSite)
            {
                // Reach to the site centre: interact range plus the structure's radius. Unit: tiles.
                Fix64 reach = InteractRange(sim) + definition.Radius;
                if (Within(worker.Position, siteCentre, reach))
                {
                    worker.ClearMoveTarget();
                    // Result of the site check; null means the site is free.
                    string? problem = SiteProblem(sim, tile);
                    if (problem != null)
                    {
                        CancelBuild(sim, worker, problem, true);
                        return;
                    }
                    // Placement time. Unit: ticks. Range: 1 upward.
                    int placementTicks = sim.Tuning.Economy.WorkerPlacementTicks(sim.Clock);
                    worker.StartPlacing(placementTicks);
                    sim.Events.Add(new BuildOrderStartedEvent(sim.Clock.Tick, worker.Id, definition.Id, worker.BuildTileX, worker.BuildTileY, placementTicks));
                }
                else if (!worker.HasMoveTarget)
                {
                    // Movement gave up or something cleared the order: the site cannot be reached.
                    CancelBuild(sim, worker, "unreachable", true);
                }
                return;
            }
            worker.TickPlacing();
            if (worker.BuildTicksRemaining > 0)
            {
                return;
            }
            // Result of the second site check, on the tick the structure would appear.
            string? lateProblem = SiteProblem(sim, tile);
            if (lateProblem != null)
            {
                CancelBuild(sim, worker, lateProblem, true);
                return;
            }
            // The new structure; the footprint blocker is added by the spawn path. It is not in this step's snapshot, so its
            // construction countdown first runs next tick.
            Unit structure = sim.SpawnUnit(definition, worker.Owner, siteCentre);
            structure.SetHealth(definition.MaxHealth * sim.Tuning.Economy.ConstructionStartHealthFraction);
            structure.SetConstructionTicks(EconomyRules.BuildTicks(sim, definition));
            sim.Events.Add(new StructurePlacedEvent(sim.Clock.Tick, worker.Id, structure.Id, definition.Id, worker.BuildTileX, worker.BuildTileY));
            // Gather field to go back to. Unit: field index. Range: -1 or more.
            int resume = worker.BuildResumeFieldIndex;
            worker.ClearBuildOrder();
            if (resume >= 0)
            {
                StartGatherLoop(sim, worker, resume);
            }
        }

        /// <summary>Counts a structure's construction down, adds its health share, and completes it at 0.</summary>
        private static void StepConstruction(Simulation sim, Unit unit)
        {
            // Total construction time of this structure. Unit: ticks. Range: 1 upward.
            int total = EconomyRules.BuildTicks(sim, unit.Definition);
            // Health gained over the whole build. Unit: health points. Range: 0 to the definition's max health.
            Fix64 span = unit.Definition.MaxHealth - unit.Definition.MaxHealth * sim.Tuning.Economy.ConstructionStartHealthFraction;
            // Health gained this tick: an even share, truncated.
            Fix64 gain = span / total;
            unit.SetHealth(unit.Health + gain);
            unit.TickConstruction();
            if (unit.ConstructionTicksRemaining > 0)
            {
                return;
            }
            // Rounding left over by the truncated shares; added once at the end so an undamaged structure finishes at full health.
            Fix64 residue = span - gain * total;
            unit.SetHealth(unit.Health + residue);
            sim.Events.Add(new StructureCompletedEvent(sim.Clock.Tick, unit.Id, unit.Definition.Id));
        }

        /// <summary>Advances one worker's gather loop by one tick.</summary>
        private static void StepGather(Simulation sim, Unit worker)
        {
            if (worker.GatherFieldIndex < 0 || worker.GatherFieldIndex >= sim.Resources.Count)
            {
                worker.ClearGather();
                return;
            }
            // The field being worked.
            ResourceFieldState field = sim.Resources[worker.GatherFieldIndex];
            // Centre of the field's tile, where the worker stands to gather. Unit: tiles.
            FixVec2 fieldPoint = sim.Terrain.CentreOf(sim.Terrain.IndexOf(field.X, field.Y));
            switch (worker.GatherState)
            {
                case GatherState.ToField:
                    if (field.IsDepleted)
                    {
                        EndOrReturn(sim, worker);
                    }
                    else if (Within(worker.Position, fieldPoint, InteractRange(sim)))
                    {
                        worker.ClearMoveTarget();
                        worker.SetGather(GatherState.Gathering, field.Index, sim.Tuning.Economy.GatherTicksPerTrip(sim.Clock));
                    }
                    else if (!worker.HasMoveTarget)
                    {
                        // Stopped short of the field: it cannot be reached.
                        worker.ClearGather();
                    }
                    break;
                case GatherState.Gathering:
                    if (field.IsDepleted)
                    {
                        worker.ClearGather();
                        break;
                    }
                    if (worker.GatherTicksRemaining > 0)
                    {
                        worker.SetGather(GatherState.Gathering, field.Index, worker.GatherTicksRemaining - 1);
                    }
                    if (worker.GatherTicksRemaining == 0)
                    {
                        // Load taken from the field. Unit: resource units. Range: 0 to gather_amount_per_trip.
                        int taken = field.Take(sim.Tuning.Economy.GatherAmountPerTrip);
                        if (taken > 0)
                        {
                            worker.SetCarried(field.Kind, taken);
                        }
                        if (taken > 0 && field.IsDepleted)
                        {
                            sim.Events.Add(new FieldDepletedEvent(sim.Clock.Tick, field.Index, field.Kind));
                        }
                        EndOrReturn(sim, worker);
                    }
                    break;
                case GatherState.ToDropOff:
                    StepToDropOff(sim, worker, field);
                    break;
            }
        }

        /// <summary>After a load is taken (or the field is found empty): head for a drop-off if carrying, else stop.</summary>
        private static void EndOrReturn(Simulation sim, Unit worker)
        {
            if (worker.CarriedAmount <= 0)
            {
                worker.ClearGather();
                worker.ClearMoveTarget();
                return;
            }
            // Nearest drop-off of the owner, or null.
            Unit? dropOff = EconomyRules.NearestDropOff(sim, worker.Owner, worker.Position);
            if (dropOff == null)
            {
                worker.ClearGather();
                worker.ClearMoveTarget();
                return;
            }
            worker.SetGather(GatherState.ToDropOff, worker.GatherFieldIndex, 0);
            worker.SetMoveTarget(EconomyRules.StandablePointNear(sim, dropOff.Position, worker.EffectiveLayer));
        }

        /// <summary>Walks a loaded worker to its nearest drop-off and deposits there.</summary>
        private static void StepToDropOff(Simulation sim, Unit worker, ResourceFieldState field)
        {
            // Nearest drop-off of the owner now, or null when the last one is gone.
            Unit? dropOff = EconomyRules.NearestDropOff(sim, worker.Owner, worker.Position);
            if (dropOff == null)
            {
                worker.ClearGather();
                worker.ClearMoveTarget();
                return;
            }
            // Reach for depositing: interact range from the drop-off's edge. Unit: tiles.
            Fix64 reach = InteractRange(sim) + dropOff.Definition.Radius;
            if (Within(worker.Position, dropOff.Position, reach))
            {
                // Player who receives the load.
                Player owner = sim.Players.TryGet(worker.Owner, out Player? found) ? found : throw new InvalidOperationException("Worker owner is not a player.");
                // Amount and kind being deposited.
                int amount = worker.CarriedAmount;
                ResourceKind kind = worker.CarriedKind;
                owner.AddResource(kind, amount);
                worker.ClearCarried();
                sim.Events.Add(new ResourceDepositedEvent(sim.Clock.Tick, worker.Id, dropOff.Id, worker.Owner, kind, amount));
                if (field.IsDepleted)
                {
                    worker.ClearGather();
                    worker.ClearMoveTarget();
                }
                else
                {
                    worker.SetGather(GatherState.ToField, field.Index, 0);
                    worker.SetMoveTarget(EconomyRules.StandablePointNear(sim, sim.Terrain.CentreOf(sim.Terrain.IndexOf(field.X, field.Y)), worker.EffectiveLayer));
                }
                return;
            }
            // Where to walk: the drop-off, or a free tile beside it when its own tile is blocked.
            FixVec2 approach = EconomyRules.StandablePointNear(sim, dropOff.Position, worker.EffectiveLayer);
            if (!worker.HasMoveTarget)
            {
                // Stopped short of the drop-off: it cannot be reached.
                worker.ClearGather();
                return;
            }
            if (worker.MoveTarget != approach)
            {
                // The nearest drop-off changed (for example an HQ rooted): head for the new one.
                worker.SetMoveTarget(approach);
            }
        }

        /// <summary>Counts a producer's head-of-queue time down and releases the unit when it is done.</summary>
        private static void StepProduction(Simulation sim, Unit producer)
        {
            if (producer.IsUnderConstruction || (producer.Definition.Hq != null && producer.HqState != HqState.Rooted))
            {
                return;
            }
            producer.TickProduction();
            if (producer.ProductionTicksRemaining > 0)
            {
                return;
            }
            // Definition ID at the head of the queue and its definition.
            string headId = producer.ProductionQueue[0];
            if (!sim.Content.Units.TryGetValue(headId, out UnitDefinition? definition))
            {
                producer.FinishProductionHead(0);
                return;
            }
            if (!EconomyRules.TryFindSpawnPosition(sim, producer.Position, definition.Layer, out FixVec2 position))
            {
                // No free tile around the producer: wait and retry next tick.
                return;
            }
            // The new unit, owned by the producer's owner.
            Unit created = sim.SpawnUnit(definition, producer.Owner, position);
            sim.Events.Add(new UnitTrainedEvent(sim.Clock.Tick, producer.Id, created.Id, definition.Id));
            // Build time of the next queue entry in ticks, or 0 when none is waiting.
            int nextTicks = 0;
            if (producer.ProductionQueue.Count > 1 && sim.Content.Units.TryGetValue(producer.ProductionQueue[1], out UnitDefinition? next))
            {
                nextTicks = EconomyRules.BuildTicks(sim, next);
            }
            producer.FinishProductionHead(nextTicks);
        }
    }
}

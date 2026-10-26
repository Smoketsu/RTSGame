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
// Construction: a structure placed by BuildStructureCommand starts at construction_start_health_fraction of its health
//   and gains the rest evenly over build_seconds; while under construction it is no drop-off, producer or supply source and
//   its weapon is off. StructureCompletedEvent is emitted at the end.
// Footprint: a unit whose definition has footprint 1 adds a Footprint blocker to its tile at spawn and removes it when it
//   is removed (OnUnitRemoved), unless another footprint unit shares the tile.
// Supply: RecomputeSupply sums, per player, the supply of every living unit plus every queued unit, and the supply cap. It
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
        public static void RecomputeSupply(Simulation sim)
        {
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

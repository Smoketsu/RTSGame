// rts/src/Campaign/ConditionEvaluator.cs
// Evaluates mission conditions (rts/docs/specs/campaign_scripting.md sections 2 and 4). Part of Rts.Campaign. Two pure steps over a condition
// tree and its per-node state: Count adds one step's events and signals to the counters and latches, IsTrue answers the condition now. Both read
// the simulation (events, units, players) and never call anything that changes it. Engine-free; Fix64 and integers only; no static state.
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Campaign
{
    /// <summary>Mutable memory of one condition node: how many matching events were seen and whether a matching signal arrived. Lives in the runner, outside the simulation, and is not hashed.</summary>
    public sealed class ConditionState
    {
        /// <summary>Matching events seen since counting started. Unit: events. Range: 0 upward.</summary>
        public int Count { get; internal set; }

        /// <summary>True once a matching camera or selection signal arrived (never reset).</summary>
        public bool Latched { get; internal set; }
    }

    /// <summary>Counts and evaluates condition trees against a simulation.</summary>
    public static class ConditionEvaluator
    {
        /// <summary>A fresh state array for a condition tree: one entry per node, indexed by <see cref="ConditionDefinition.Index"/>. Unit: nodes.</summary>
        /// <param name="root">The root of the tree, or null for no condition (an empty array).</param>
        public static ConditionState[] NewStates(ConditionDefinition? root)
        {
            // One state per node of the tree.
            ConditionState[] states = new ConditionState[root == null ? 0 : root.NodeCount];
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = new ConditionState();
            }
            return states;
        }

        /// <summary>
        /// Adds one step's events and the signals stamped for that step to the counters and latches of every node of the tree. Call once per
        /// step for each tree that is counting.
        /// </summary>
        /// <param name="node">Root of the tree (or a subtree).</param>
        /// <param name="states">State array of the whole tree, indexed by node index.</param>
        /// <param name="sim">The simulation after the step (read only).</param>
        /// <param name="signals">Signals stamped with the step's tick, in arrival order.</param>
        public static void Count(ConditionDefinition node, ConditionState[] states, Simulation sim, IReadOnlyList<HostSignal> signals)
        {
            // Memory of this node.
            ConditionState state = states[node.Index];
            switch (node.Kind)
            {
                case ConditionKind.EventCount:
                    foreach (SimEvent simEvent in sim.Events.Current)
                    {
                        if (Matches(node, simEvent, sim))
                        {
                            state.Count++;
                        }
                    }
                    break;
                case ConditionKind.CameraInArea:
                    foreach (HostSignal signal in signals)
                    {
                        if (signal is CameraMovedSignal camera && Inside(camera.X, camera.Y, node))
                        {
                            state.Latched = true;
                        }
                    }
                    break;
                case ConditionKind.SelectionContains:
                    foreach (HostSignal signal in signals)
                    {
                        if (signal is SelectionChangedSignal selection && CountSelected(node, selection, sim) >= node.MinCount)
                        {
                            state.Latched = true;
                        }
                    }
                    break;
                case ConditionKind.AllOf:
                case ConditionKind.AnyOf:
                    foreach (ConditionDefinition child in node.Children)
                    {
                        Count(child, states, sim, signals);
                    }
                    break;
            }
        }

        /// <summary>True when the condition holds now.</summary>
        /// <param name="node">Root of the tree (or a subtree).</param>
        /// <param name="states">State array of the whole tree, indexed by node index.</param>
        /// <param name="sim">The simulation after the step (read only).</param>
        /// <param name="elapsedTicks">Ticks the owning objective (or, for a trigger or the defeat condition, the mission) has been counting, this tick included. Unit: ticks. Range: 0 upward.</param>
        public static bool IsTrue(ConditionDefinition node, ConditionState[] states, Simulation sim, int elapsedTicks)
        {
            switch (node.Kind)
            {
                case ConditionKind.EventCount:
                    return states[node.Index].Count >= node.MinCount;
                case ConditionKind.UnitsInArea:
                    return CountInArea(node, sim) >= node.MinCount;
                case ConditionKind.ResourcesAtLeast:
                    return sim.Players.TryGet(node.Owner, out Player? stock) && stock.Common >= node.Common && stock.Rare >= node.Rare;
                case ConditionKind.SecondsActive:
                    return elapsedTicks >= sim.Clock.SecondsToTicks(node.Seconds);
                case ConditionKind.CameraInArea:
                case ConditionKind.SelectionContains:
                    return states[node.Index].Latched;
                case ConditionKind.PlayerStateIs:
                    return sim.Players.TryGet(node.Owner, out Player? player) && player.State == node.State;
                case ConditionKind.AllOf:
                    foreach (ConditionDefinition child in node.Children)
                    {
                        if (!IsTrue(child, states, sim, elapsedTicks))
                        {
                            return false;
                        }
                    }
                    return true;
                default:
                    foreach (ConditionDefinition child in node.Children)
                    {
                        if (IsTrue(child, states, sim, elapsedTicks))
                        {
                            return true;
                        }
                    }
                    return false;
            }
        }

        /// <summary>True when a point lies inside (or on) the node's circle. Squared distances in fixed point; map coordinates keep them far below the Fix64 limit.</summary>
        private static bool Inside(Fix64 x, Fix64 y, ConditionDefinition node)
        {
            // Offsets from the circle centre. Unit: tiles.
            Fix64 dx = x - node.X;
            Fix64 dy = y - node.Y;
            return dx * dx + dy * dy <= node.RadiusTiles * node.RadiusTiles;
        }

        /// <summary>Live units of the node's owner (and definition) whose centre is inside the node's circle.</summary>
        private static int CountInArea(ConditionDefinition node, Simulation sim)
        {
            // Matching units so far. Unit: units.
            int count = 0;
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == node.Owner && (node.Definition == null || unit.Definition.Id == node.Definition) && Inside(unit.Position.X, unit.Position.Y, node))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Units of a selection signal that exist now and match the node's owner and definition.</summary>
        private static int CountSelected(ConditionDefinition node, SelectionChangedSignal selection, Simulation sim)
        {
            // Matching units so far. Unit: units.
            int count = 0;
            foreach (EntityId id in selection.Units)
            {
                if (sim.Units.TryGet(id, out Unit? unit) && unit.Owner == node.Owner && (node.Definition == null || unit.Definition.Id == node.Definition))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>True when an event is of the node's kind and passes its owner, definition and unit filters (spec section 2: how each kind is matched; a live lookup that finds no unit never matches).</summary>
        private static bool Matches(ConditionDefinition node, SimEvent simEvent, Simulation sim)
        {
            if (simEvent.Kind != node.EventName)
            {
                return false;
            }
            switch (simEvent)
            {
                case UnitSpawnedEvent spawned:
                    return Passes(node, spawned.Owner, spawned.DefinitionId, spawned.Unit);
                case UnitArrivedEvent arrived:
                    return PassesLive(node, sim, arrived.Unit, null);
                case ResourceDepositedEvent deposited:
                    return Passes(node, deposited.Owner, WorkerDefinition(sim, deposited.Worker), deposited.Worker) && sim.Units.Contains(deposited.Worker);
                case UnitTrainedEvent trained:
                    return PassesLive(node, sim, trained.Unit, trained.DefinitionId);
                case StructureCompletedEvent completed:
                    return PassesLive(node, sim, completed.Structure, completed.DefinitionId);
                case UnitKilledEvent killed:
                    return Passes(node, killed.VictimOwner, null, killed.Victim);
                case HqStateChangedEvent changed:
                    return PassesLive(node, sim, changed.Unit, null);
                case CoreCapturedEvent captured:
                    return !node.HasOwner || (sim.Players.TryGet(node.Owner, out Player? holder) && holder.Team == captured.Team);
                case CycleStartedEvent _:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The definition ID of a live unit, or null when it is gone (then a definition filter cannot match).</summary>
        private static string? WorkerDefinition(Simulation sim, EntityId id)
        {
            return sim.Units.TryGet(id, out Unit? unit) ? unit.Definition.Id : null;
        }

        /// <summary>Applies the owner, definition and unit filters to values the event carries itself. A null definition passes only when the node has no definition filter.</summary>
        private static bool Passes(ConditionDefinition node, PlayerId owner, string? definition, EntityId unit)
        {
            return (!node.HasOwner || node.Owner == owner)
                && (node.Definition == null || node.Definition == definition)
                && (!node.HasUnit || node.Unit == unit);
        }

        /// <summary>Applies the filters through a live lookup of <paramref name="id"/>: owner from the unit, definition from the event when given else from the unit. No unit, no match.</summary>
        private static bool PassesLive(ConditionDefinition node, Simulation sim, EntityId id, string? eventDefinition)
        {
            if (!sim.Units.TryGet(id, out Unit? unit))
            {
                return false;
            }
            return Passes(node, unit.Owner, eventDefinition ?? unit.Definition.Id, id);
        }
    }
}

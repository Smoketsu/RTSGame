// rts/src/Sim/Units/HqSystem.cs
// The mobile HQ unit rules (Game Design Proposal, "The mobile HQ unit"): the Rooting and Uprooting countdowns, Last Stand,
// the "next to a resource field" test, and player defeat when the HQ dies. Part of Rts.Sim.Units. Called from Simulation:
//   * Step, once per tick after the commands and before movement and combat (step order in Simulation.cs): counts the
//     Last Stand invulnerability and the Rooting or Uprooting countdown down and finishes a switch.
//   * OnUnitSpawned, from Simulation.SpawnUnit: a new HQ starts Rooted when it stands within
//     hq_root_max_distance_to_field_tiles of a field that still holds resources, otherwise Uprooted. This is how a script
//     (spawn_unit) places a starting HQ: spawn it beside a field to start Rooted.
//   * AfterDamage, from CombatSystem.Fire right after a hit lands: triggers Last Stand.
//   * OnUnitRemoved, from Simulation.RemoveUnit (the one path every death and removal takes): defeats the owner.
// Timing convention: a countdown set by a command on tick T is counted down by this system on tick T itself, so a switch of
// N ticks finishes on tick T + N - 1 (the N-th step including the command's own) and the new state applies from tick T + N.
// Last Stand: when enabled, an HQ at or below last_stand_health_fraction of its maximum becomes immune to damage for
// last_stand_invulnerable_seconds, at most last_stand_uses_per_match times per HQ. The countdown is set during combat on
// tick T and reduced by this system at the start of every later tick, so immunity covers the rest of tick T and ticks
// T + 1 to T + N - 1 (N combat phases in all, N = last_stand_invulnerable_seconds in ticks). A hit that takes an HQ from
// above the threshold straight to 0 kills it: Last Stand reacts to health that remains. Defeat: removing the last HQ a
// player owns, by any means (a kill, a remove_unit command), moves the player to Defeated with cause "hq_destroyed".
using System;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Teams;

namespace Rts.Sim.Units
{
    /// <summary>Rooting, Last Stand and defeat for HQ units.</summary>
    public static class HqSystem
    {
        /// <summary>Rejection reason for a movement or attack order given to an HQ that is not Uprooted. Range: fixed text "hq_rooted".</summary>
        public const string ReasonHqRooted = "hq_rooted";

        /// <summary>Cause written into the PlayerStateChangedEvent when an HQ's death defeats its owner. Range: fixed text "hq_destroyed".</summary>
        public const string CauseHqDestroyed = "hq_destroyed";

        /// <summary>True when <paramref name="unit"/> is an HQ that cannot walk: Rooted, Rooting or Uprooting.</summary>
        /// <param name="unit">Any unit.</param>
        public static bool IsMovementLocked(Unit unit)
        {
            return unit.Definition.Hq != null && unit.HqState != HqState.Uprooted;
        }

        /// <summary>
        /// True when some resource field that still holds resources has its tile centre within
        /// hq_root_max_distance_to_field_tiles (tuning/hq.json) of <paramref name="position"/>. The distance is compared squared in Fix64.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="position">Where the HQ stands. Unit: tiles.</param>
        public static bool IsNearResources(Simulation sim, FixVec2 position)
        {
            // Largest allowed centre distance. Unit: tiles. Range: 0.5 to 64.
            Fix64 maxDistance = sim.Tuning.Hq.HqRootMaxDistanceToFieldTiles;
            // The same distance squared, to avoid a square root. Unit: squared tiles.
            Fix64 maxSquared = maxDistance * maxDistance;
            foreach (ResourceFieldState field in sim.Resources.All)
            {
                if (field.IsDepleted)
                {
                    continue;
                }
                // Offset from the HQ to the field tile's centre. Unit: tiles.
                FixVec2 delta = sim.Terrain.CentreOf(sim.Terrain.IndexOf(field.X, field.Y)) - position;
                if (delta.LengthSquared() <= maxSquared)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Sets the starting state of a freshly spawned HQ (Rooted next to a field, else Uprooted). No event is emitted.</summary>
        /// <param name="sim">The simulation, inside a step.</param>
        /// <param name="unit">The new unit.</param>
        internal static void OnUnitSpawned(Simulation sim, Unit unit)
        {
            if (unit.Definition.Hq == null)
            {
                return;
            }
            unit.SetHqState(IsNearResources(sim, unit.Position) ? HqState.Rooted : HqState.Uprooted, 0);
        }

        /// <summary>Runs one tick of HQ timers for every HQ in ascending ID order. Internal: only <see cref="Simulation.Step"/> calls it.</summary>
        internal static void Step(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Definition.Hq == null)
                {
                    continue;
                }
                unit.TickLastStand();
                if (unit.HqState != HqState.Rooting && unit.HqState != HqState.Uprooting)
                {
                    continue;
                }
                unit.TickHqTransition();
                if (unit.HqTransitionTicksRemaining > 0)
                {
                    continue;
                }
                // State the switch ends in: Rooting ends Rooted, Uprooting ends Uprooted.
                HqState from = unit.HqState;
                HqState to = from == HqState.Rooting ? HqState.Rooted : HqState.Uprooted;
                unit.SetHqState(to, 0);
                sim.Events.Add(new HqStateChangedEvent(sim.Clock.Tick, unit.Id, from, to));
            }
        }

        /// <summary>
        /// Starts a switch: sets Rooting or Uprooting with its countdown and reports it. Internal: the root and uproot commands call it
        /// after their checks.
        /// </summary>
        /// <param name="sim">The simulation, inside a step.</param>
        /// <param name="unit">The HQ.</param>
        /// <param name="to">Rooting or Uprooting.</param>
        internal static void BeginSwitch(Simulation sim, Unit unit, HqState to)
        {
            // Ticks the switch takes: root_seconds or uproot_seconds from tuning/hq.json, at least 1.
            int ticks = Math.Max(1, to == HqState.Rooting ? sim.Tuning.Hq.RootTicks(sim.Clock) : sim.Tuning.Hq.UprootTicks(sim.Clock));
            // State before the switch, for the event.
            HqState from = unit.HqState;
            unit.SetHqState(to, ticks);
            sim.Events.Add(new HqStateChangedEvent(sim.Clock.Tick, unit.Id, from, to));
        }

        /// <summary>Triggers Last Stand when the hit that just landed left the HQ alive at or below the threshold. Internal: <c>CombatSystem.Fire</c> only.</summary>
        /// <param name="sim">The simulation, inside a step.</param>
        /// <param name="target">The unit that was just hit.</param>
        internal static void AfterDamage(Simulation sim, Unit target)
        {
            if (target.Definition.Hq == null || !sim.Tuning.Hq.LastStandEnabled)
            {
                return;
            }
            if (target.Health <= Fix64.Zero || target.IsInvulnerable || target.LastStandUses >= sim.Tuning.Hq.LastStandUsesPerMatch)
            {
                return;
            }
            // Health at or below which Last Stand triggers. Unit: health points. Range: 5% to 50% of max health.
            Fix64 threshold = target.Definition.MaxHealth * sim.Tuning.Hq.LastStandHealthFraction;
            if (target.Health > threshold)
            {
                return;
            }
            // Invulnerability length. Unit: ticks. Range: 0 to 30 seconds in ticks.
            int ticks = sim.Tuning.Hq.LastStandInvulnerableTicks(sim.Clock);
            target.StartLastStand(ticks);
            sim.Events.Add(new HqLastStandEvent(sim.Clock.Tick, target.Id, target.Health, ticks));
        }

        /// <summary>
        /// Defeats the owner of a removed HQ unless the owner still has another HQ. Internal: <see cref="Simulation.RemoveUnit"/> only.
        /// The player transition is refused (and ignored) when the player is already out of the match.
        /// </summary>
        /// <param name="sim">The simulation, inside a step.</param>
        /// <param name="removed">The unit that was just removed.</param>
        internal static void OnUnitRemoved(Simulation sim, Unit removed)
        {
            if (removed.Definition.Hq == null || !removed.Owner.IsValid)
            {
                return;
            }
            foreach (Unit other in sim.Units.All)
            {
                if (other.Owner == removed.Owner && other.Definition.Hq != null)
                {
                    return;
                }
            }
            PlayerTransitions.TryChange(sim, removed.Owner, PlayerState.Defeated, CauseHqDestroyed, out _);
        }
    }
}

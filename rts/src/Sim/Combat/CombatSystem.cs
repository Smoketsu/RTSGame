// rts/src/Sim/Combat/CombatSystem.cs
// Phase 1 combat: target acquisition, attack-move steering, weapon cooldowns, damage and death. Part of Rts.Sim.Combat.
// Runs once per tick inside Simulation.Step, AFTER movement and BEFORE faction mechanics (see the step order in
// Simulation.cs). Everything is deterministic: units are visited in ascending ID order, distances are compared as
// squares in Fix64 (no square root, no floating point), ties go to the lowest EntityId, and the only data read is the
// unit definitions and CombatRules (rts/data/tuning/combat.json).
//
// Rules in one place:
//   * Order of work per unit: tick the weapon cooldown down, resolve a target, fire if in range and ready, then
//     steer an attack-move order.
//   * Reach: a target is in range when centre distance minus both radii is at most the range, i.e. the centre
//     distance is at most range + attackerRadius + targetRadius (compared squared).
//   * Targets: an alive unit of an enemy player (PlayerRegistry.AreEnemies; neutral units are never enemies) on a
//     movement layer the weapon can hit. Allies are never targeted.
//   * Acquisition: an unforced target is kept while it stays valid and within range (plus the acquire bonus for
//     attack-moving units); otherwise the nearest valid enemy within that reach is taken, ties to the lowest ID.
//   * Attack-move (attack_move, attack_unit): the unit stops to fight enemies within weapon range, chases targets
//     within range + acquire bonus (an attack_unit target is chased at any distance), and resumes walking to the
//     goal when no target is left. Arrival (within the movement arrival radius) clears the order. A chase toward a target
//     the unit cannot walk to (flow field says unreachable) is abandoned instead of retried every tick.
//   * Plain move orders: the unit does NOT stop or chase, but it still fires at an enemy that is within weapon range
//     while it walks, whenever the cooldown allows. This keeps "retreat" orders meaningful (running units shoot back
//     at what is already in range, but never turn around).
//   * Damage = weapon damage x table multiplier x product of the registered IDamageModifier multipliers (ascending
//     modifier ID). A hit that takes health to 0 does not remove the target at once: all units act within the same
//     tick (so two units that would kill each other both fire), then the dead are removed in ascending ID order,
//     each as UnitKilledEvent followed by UnitRemovedEvent("killed"). A unit at 0 health can no longer be targeted.
//   * Cost: acquisition scans all units per armed unit, O(n^2) per tick; fine for Phase 1 sizes, a spatial grid is
//     the planned replacement once unit counts require it.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Units;

namespace Rts.Sim.Combat
{
    /// <summary>Resolves combat for every unit once per tick.</summary>
    public static class CombatSystem
    {
        /// <summary>A lethal hit waiting for end-of-step removal.</summary>
        private readonly struct PendingKill
        {
            /// <summary>The unit whose health reached 0.</summary>
            public readonly EntityId Victim;

            /// <summary>The unit whose hit was lethal.</summary>
            public readonly EntityId Killer;

            /// <summary>Stores the pair.</summary>
            public PendingKill(EntityId victim, EntityId killer)
            {
                Victim = victim;
                Killer = killer;
            }
        }

        /// <summary>Runs one tick of combat for every unit in <paramref name="sim"/>. Internal: only <see cref="Simulation.Step"/> calls it.</summary>
        internal static void Step(Simulation sim)
        {
            // Damage table and acquire bonus for this match.
            CombatRules rules = sim.Content.Combat;
            // Snapshot of live unit IDs in ascending order (removal happens after the loop, but the snapshot keeps the loop independent of it).
            List<EntityId> ids = sim.Units.IdsSnapshot();
            // Lethal hits of this tick in the order they happened.
            List<PendingKill> kills = new List<PendingKill>();
            foreach (EntityId id in ids)
            {
                if (sim.Units.TryGet(id, out Unit? unit))
                {
                    StepUnit(sim, rules, unit, kills);
                }
            }
            kills.Sort((a, b) => a.Victim.CompareTo(b.Victim));
            foreach (PendingKill kill in kills)
            {
                // The dead unit, still registered until removed below.
                if (!sim.Units.TryGet(kill.Victim, out Unit? victim))
                {
                    continue;
                }
                // Owner of the killer; None when the killer was removed earlier (cannot happen within one step, kept defensive).
                PlayerId killerOwner = sim.Units.TryGet(kill.Killer, out Unit? killer) ? killer.Owner : PlayerId.None;
                sim.Events.Add(new UnitKilledEvent(sim.Clock.Tick, kill.Victim, kill.Killer, victim.Owner, killerOwner));
                sim.RemoveUnit(kill.Victim, "killed");
            }
        }

        /// <summary>Cooldown, targeting, firing and attack-move steering for one unit.</summary>
        private static void StepUnit(Simulation sim, CombatRules rules, Unit unit, List<PendingKill> kills)
        {
            if (unit.CooldownTicksRemaining > 0)
            {
                unit.SetCooldownTicks(unit.CooldownTicksRemaining - 1);
            }
            // The unit's weapon; null for workers and other unarmed units.
            WeaponDefinition? weapon = unit.Definition.Weapon;
            // True when the unit already had a target at the start of this tick (it may have just died or gone out of reach).
            bool hadTarget = unit.AttackTarget.IsValid;
            // The unit being fought this tick, or null.
            Unit? target = null;
            if (weapon != null)
            {
                // Extra reach for noticing and keeping targets. Unit: tiles. Range: 0 or the acquire bonus.
                Fix64 bonus = unit.IsAttackMoving ? rules.AcquireRangeBonusTiles : Fix64.Zero;
                target = ResolveTarget(sim, unit, weapon, bonus);
                if (target != null)
                {
                    if (InReach(unit, target, weapon.RangeTiles))
                    {
                        if (unit.IsAttackMoving && unit.HasMoveTarget)
                        {
                            // Stop walking to fight; a plain move order is never interrupted.
                            unit.ClearMoveTarget();
                        }
                        if (unit.CooldownTicksRemaining == 0)
                        {
                            Fire(sim, rules, unit, weapon, target, kills);
                        }
                    }
                    else if (unit.IsAttackMoving)
                    {
                        if (CanReach(sim, unit, target.Position))
                        {
                            // Close the gap; the movement system walks the unit next tick.
                            unit.SetMoveTarget(target.Position);
                        }
                        else
                        {
                            // The target stands where this unit cannot walk (a flyer over a cliff, say): give it up and keep going.
                            unit.SetAttackTarget(EntityId.None, false);
                            target = null;
                        }
                    }
                }
            }
            if (target == null && unit.IsAttackMoving)
            {
                SteerAttackMove(sim, unit, hadTarget);
            }
        }

        /// <summary>
        /// Keeps an attack-moving unit with no target heading for its goal. Near the goal (within the movement arrival
        /// radius) the order ends. After a fight or a chase (<paramref name="hadTarget"/>, or a move target that is not
        /// the goal) the unit is sent back to the goal. A unit that has no move target and did not just fight was
        /// stopped by the movement system (the goal is unreachable), so the order ends instead of retrying every tick.
        /// </summary>
        /// <param name="hadTarget">True when the unit had a target at the start of this tick.</param>
        private static void SteerAttackMove(Simulation sim, Unit unit, bool hadTarget)
        {
            // Arrival radius shared with the movement system. Unit: tiles. Range: 0.05 to 2 (tuning/terrain.json).
            Fix64 arrival = sim.Tuning.Terrain.ArrivalRadiusTiles;
            if ((unit.AttackMoveGoal - unit.Position).LengthSquared() <= arrival * arrival)
            {
                unit.ClearMoveTarget();
                unit.ClearAttackMove();
            }
            else if (hadTarget || (unit.HasMoveTarget && unit.MoveTarget != unit.AttackMoveGoal))
            {
                unit.SetMoveTarget(unit.AttackMoveGoal);
            }
            else if (!unit.HasMoveTarget)
            {
                unit.ClearAttackMove();
            }
        }

        /// <summary>
        /// True when the unit can walk to <paramref name="point"/>: always for flyers, otherwise when the flow field for
        /// the point's tile reaches the unit's tile. Uses the same cached fields as the movement system.
        /// </summary>
        /// <param name="point">Destination in tiles.</param>
        private static bool CanReach(Simulation sim, Unit unit, FixVec2 point)
        {
            // Layer the unit walks on; flyers need no path.
            MovementLayer layer = unit.EffectiveLayer;
            if (layer == MovementLayer.Air)
            {
                return true;
            }
            // Tile holding the destination (clamped into the map like the movement system does).
            int goalTile = sim.Terrain.TileOf(sim.Terrain.ClampToMap(point));
            return sim.Pathing.Get(layer, goalTile).IsReachable(sim.Terrain.TileOf(unit.Position));
        }

        /// <summary>
        /// Keeps the current target while it stays valid and in reach, otherwise acquires the nearest valid enemy
        /// within reach (ties to the lowest ID). Updates <see cref="Unit.AttackTarget"/> and returns the target or null.
        /// </summary>
        /// <param name="bonus">Extra reach beyond weapon range. Unit: tiles. Range: 0 or the acquire bonus.</param>
        private static Unit? ResolveTarget(Simulation sim, Unit unit, WeaponDefinition weapon, Fix64 bonus)
        {
            // Largest centre-gap-minus-radii distance at which a target is kept or taken. Unit: tiles. Range: weapon range plus bonus.
            Fix64 acquireRange = weapon.RangeTiles + bonus;
            if (unit.AttackTarget.IsValid)
            {
                if (sim.Units.TryGet(unit.AttackTarget, out Unit? current) && IsValidTarget(sim, unit, weapon, current)
                    && (unit.IsAttackTargetForced || InReach(unit, current, acquireRange)))
                {
                    return current;
                }
                unit.SetAttackTarget(EntityId.None, false);
            }
            // Best candidate so far and its squared centre distance. Unit: squared tiles. Range: 0 or more.
            Unit? best = null;
            Fix64 bestDistanceSquared = Fix64.Zero;
            foreach (Unit candidate in sim.Units.All)
            {
                if (!IsValidTarget(sim, unit, weapon, candidate) || !InReach(unit, candidate, acquireRange))
                {
                    continue;
                }
                // Squared centre distance to this candidate. Unit: squared tiles. Range: 0 or more.
                Fix64 distanceSquared = (candidate.Position - unit.Position).LengthSquared();
                // Ascending-ID iteration plus a strict comparison gives the lowest ID on ties.
                if (best == null || distanceSquared < bestDistanceSquared)
                {
                    best = candidate;
                    bestDistanceSquared = distanceSquared;
                }
            }
            if (best != null)
            {
                unit.SetAttackTarget(best.Id, false);
            }
            return best;
        }

        /// <summary>True when <paramref name="target"/> may be shot by <paramref name="attacker"/>: another, alive, enemy unit on a layer the weapon hits.</summary>
        private static bool IsValidTarget(Simulation sim, Unit attacker, WeaponDefinition weapon, Unit target)
        {
            return target.Id != attacker.Id
                && target.Health > Fix64.Zero
                && attacker.Owner.IsValid
                && target.Owner.IsValid
                && sim.Players.AreEnemies(attacker.Owner, target.Owner)
                && weapon.CanTarget(target.EffectiveLayer);
        }

        /// <summary>
        /// True when the edge-to-edge distance between the two units is at most <paramref name="range"/>: the centre
        /// distance is compared, squared, with range plus both radii. A per-axis check first keeps the squares small.
        /// </summary>
        /// <param name="range">Edge-to-edge reach. Unit: tiles. Range: 0 or more.</param>
        private static bool InReach(Unit attacker, Unit target, Fix64 range)
        {
            // Largest allowed centre distance. Unit: tiles. Range: small (range plus two radii).
            Fix64 reach = range + attacker.Definition.Radius + target.Definition.Radius;
            // Centre offset from attacker to target. Unit: tiles.
            FixVec2 delta = target.Position - attacker.Position;
            if (delta.X.Abs() > reach || delta.Y.Abs() > reach)
            {
                return false;
            }
            return delta.LengthSquared() <= reach * reach;
        }

        /// <summary>Applies one hit: damage, event, cooldown, and a pending kill when health reaches 0.</summary>
        private static void Fire(Simulation sim, CombatRules rules, Unit attacker, WeaponDefinition weapon, Unit target, List<PendingKill> kills)
        {
            // Damage after the table multiplier. Unit: health points. Range: 0 or more.
            Fix64 damage = weapon.Damage * rules.GetMultiplier(weapon.DamageType, target.Definition.ArmourType);
            foreach (IDamageModifier modifier in sim.DamageModifiers)
            {
                // Mechanic multiplier for this hit; a negative answer counts as 0 so damage never heals.
                Fix64 factor = Fix64.Max(Fix64.Zero, modifier.OutgoingDamageMultiplier(sim, attacker));
                damage = damage * factor;
            }
            target.SetHealth(target.Health - damage);
            sim.Events.Add(new UnitDamagedEvent(sim.Clock.Tick, attacker.Id, target.Id, damage, target.Health));
            // Ticks until the next shot, at least 1 so a very short cooldown still spaces the hits. Unit: ticks. Range: 1 or more.
            attacker.SetCooldownTicks(Math.Max(1, sim.Clock.SecondsToTicks(weapon.CooldownSeconds)));
            if (target.Health <= Fix64.Zero)
            {
                kills.Add(new PendingKill(target.Id, attacker.Id));
            }
        }
    }
}

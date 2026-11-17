// rts/src/Sim/Combat/CombatSystem.cs
// Phase 1 combat: target acquisition, attack-move steering, weapon cooldowns, damage and death. Part of Rts.Sim.Combat.
// Runs once per tick inside Simulation.Step, AFTER movement and BEFORE faction mechanics (see the step order in
// Simulation.cs). Everything is deterministic: units are visited in ascending ID order, distances are compared as
// squares in Fix64 (no square root, no floating point), ties go to the lowest EntityId, and the only data read is the
// unit definitions and CombatRules (rts/data/tuning/combat.json).
//
// Rules in one place:
//   * Order within the step (P-003, rts/docs/specs/p003_rules.md A.3): 6a dome regen (units with a shield_dome, ascending ID; also
//     lists the active domes), 6b land the hits that fall due this tick (PendingHitQueue, in (land tick, sequence) order), 6c the
//     unit loop, 6d deaths. Units hit to 0 health in 6b still act in 6c (all units act within the tick) but cannot be targeted.
//   * Order of work per unit (6c): tick the weapon cooldown down, resolve a target, fire if in range and ready, then
//     steer an attack-move order.
//   * Armed first (P-003 row 3): a unit is armed when EffectiveWeapon is not null (workers carry a weak melee weapon; an Uprooted
//     HQ and a structure under construction count as unarmed). A new pick takes the minimum of (armed first, squared distance,
//     EntityId). Armed-first applies only to a new pick: a kept target is never swapped for an armed one (units never switch on their own).
//   * Uphill (P-003 row 1): a ranged hit from a surface unit on a target standing on higher ground (target tile height above the
//     attacker's tile height, plus 1 when the attacker is on a ramp) is multiplied by UphillRangedDamageMultiplier, after the damage
//     table and before the damage modifiers. Melee, air attackers and air targets are exempt.
//   * Hit delay (P-003 row 4): damage is fixed at fire time. With hit_delay_ticks 0 the hit lands at once; otherwise it is queued
//     (hashed) and lands on its tick wherever the target is, even after the attacker died. A target that is gone by then drops it.
//   * Dome (P-003 row 6): at fire time a ranged shot at a unit that stands inside an allied (or own) dome, fired from outside it, is
//     absorbed by the dome pool, lowest dome unit ID first; overflow passes on. Absorbed shots are never queued. Melee and shots
//     fired from inside the dome pass.
//   * Blast (combat_mechanics.md package 1): a weapon with splash_radius_tiles above 0 also damages, when its hit lands, every other enemy unit
//     on a layer the weapon can target whose edge is inside the radius around the target's position (or its fire-time position if it is gone):
//     weapon damage x combat.json splash_damage_fraction x the damage modifiers (fixed at fire time), then each victim's own armour row; no uphill
//     multiplier, no drench, no friendly fire. Victims are collected in ascending ID order before any is damaged. Domes absorb a victim's share when
//     the blast centre is outside the dome (AbsorbByDomes, shared with fire time). A shell fully absorbed at fire time makes no blast.
//   * Anti-air on water (combat_mechanics.md package 3): a weapon with can_target_air_on_water may also target a unit whose effective layer is Air
//     while the attacker's own tile is flooded (any depth above 0, any team's water), read whenever the check runs (CanHitLayer: acquisition, keeping
//     a target, forced or not, and the attack_unit command). A target that stops being valid when the tile dries is dropped on the next 6c; hits already
//     fired still land, and their blast keeps the air bit decided at fire time. CanTarget stays the static layer list (AI and harness read it).
//   * Structure multiplier (combat_mechanics.md package 2): a weapon's structure_damage_multiplier (default 1) multiplies damage against a structure
//     target (a unit with a footprint, or an HQ that is Rooted) after the damage table and before the uphill multiplier and the modifiers. At fire
//     time the target's state decides for the primary; at landing the victim's state decides for blast damage.
//   * Reach: a target is in range when centre distance minus both radii is at most the range, i.e. the centre
//     distance is at most range + attackerRadius + targetRadius (compared squared). The range is the weapon's, changed by
//     the active Planetary Cycle for surface ranged units (CycleSystem.EffectiveRangeTiles, Windstorm), for both fire and acquire reach.
//   * Targets: an alive unit of an enemy player (PlayerRegistry.AreEnemies; neutral units are never enemies) on a
//     movement layer the weapon can hit. Allies are never targeted.
//   * Acquisition: an unforced target is kept while it stays valid, within range (plus the acquire bonus for
//     attack-moving units) and visible to the attacker's team; otherwise the nearest valid, visible enemy within that reach is
//     taken, ties to the lowest ID. Visibility is the fog of war of the previous tick's end (sim.Vision, one-tick delay).
//     A forced target (attack_unit) is exempt: an explicit order still works on a unit the team cannot see (commands are not
//     yet filtered by vision; the Phase 2 anti-cheat plan is in rts/docs/specs/fog_of_war.md section 5).
//   * Attack-move (attack_move, attack_unit): the unit stops to fight enemies within weapon range, chases targets
//     within range + acquire bonus (an attack_unit target is chased at any distance), and resumes walking to the
//     goal when no target is left. Arrival (within the movement arrival radius) clears the order. A chase toward a target
//     the unit cannot walk to (flow field says unreachable) is abandoned instead of retried every tick.
//   * Plain move orders: the unit does NOT stop or chase, but it still fires at an enemy that is within weapon range
//     while it walks, whenever the cooldown allows. This keeps "retreat" orders meaningful (running units shoot back
//     at what is already in range, but never turn around).
//   * Damage = weapon damage x table multiplier (x uphill multiplier) x product of the registered IDamageModifier multipliers (ascending
//     modifier ID). A hit that takes health to 0 does not remove the target at once: all units act within the same
//     tick (so two units that would kill each other both fire), then the dead are removed in ascending ID order,
//     each as UnitKilledEvent followed by UnitRemovedEvent("killed"). A unit at 0 health can no longer be targeted.
//   * Drench: after a hit lands (UnitDamagedEvent), a weapon with applies_drench calls StatusSystem.ApplyDrenchHit, which
//     slows and eventually grounds air targets (rts/docs/specs/statuses.md); the hook adds no state to this class.
//   * HQs and structures: an HQ unit has no weapon while it is not Rooted, and a structure under construction has none
//     either (the weapon is read as null for the tick). Damage uses Unit.ArmourType (an HQ's armour follows its state).
//     A target with Unit.IsInvulnerable (HQ Last Stand) takes no damage: the hit is still reported, with damage 0. After a
//     landed hit HqSystem.AfterDamage may start Last Stand. Deaths go through Simulation.RemoveUnit, which defeats the owner
//     of a removed HQ and clears a structure's footprint.
//   * Cost: acquisition scans all units per armed unit, O(n^2) per tick; fine for Phase 1 sizes, a spatial grid is
//     the planned replacement once unit counts require it.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Statuses;
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

            /// <summary>Owner of the killer at fire time; None for neutral. The killer may be gone when a delayed hit lands.</summary>
            public readonly PlayerId KillerOwner;

            /// <summary>Stores the triple.</summary>
            public PendingKill(EntityId victim, EntityId killer, PlayerId killerOwner)
            {
                Victim = victim;
                Killer = killer;
                KillerOwner = killerOwner;
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
            // 6a: IDs of the dome units that can absorb this tick, ascending. Per-tick scratch, not state.
            List<EntityId> domes = RegenDomes(sim);
            // 6b: hits falling due this tick, in (land tick, sequence) order.
            List<PendingHit> due = new List<PendingHit>();
            sim.PendingHits.TakeDue(sim.Clock.Tick, due);
            foreach (PendingHit hit in due)
            {
                Land(sim, hit, kills, domes);
            }
            // 6c: the unit loop.
            foreach (EntityId id in ids)
            {
                if (sim.Units.TryGet(id, out Unit? unit))
                {
                    StepUnit(sim, rules, unit, kills, domes);
                }
            }
            // Total order: victim ID, then killer ID. Two lethal hits on one victim in a tick are possible; List.Sort is not stable,
            // so without the killer tie-break the credited killer would depend on the sort's internals. The lowest killer ID (the
            // attacker visited first) gets the credit and the second entry finds its victim already removed.
            kills.Sort((a, b) =>
            {
                // Comparison of the victims, then of the killers when the victims are equal. Range: negative, 0 or positive.
                int byVictim = a.Victim.CompareTo(b.Victim);
                return byVictim != 0 ? byVictim : a.Killer.CompareTo(b.Killer);
            });
            foreach (PendingKill kill in kills)
            {
                // The dead unit, still registered until removed below.
                if (!sim.Units.TryGet(kill.Victim, out Unit? victim))
                {
                    continue;
                }
                sim.Events.Add(new UnitKilledEvent(sim.Clock.Tick, kill.Victim, kill.Killer, victim.Owner, kill.KillerOwner));
                sim.RemoveUnit(kill.Victim, "killed");
            }
        }

        /// <summary>
        /// The weapon the unit can use now: its definition's, or null while it is under construction or an HQ that is not Rooted
        /// (or when it has none). A unit is armed when this is not null.
        /// </summary>
        internal static WeaponDefinition? EffectiveWeapon(Unit u)
        {
            WeaponDefinition? weapon = u.Definition.Weapon;
            if (weapon != null && (u.IsUnderConstruction || (u.Definition.Hq != null && u.HqState != HqState.Rooted)))
            {
                return null;
            }
            return weapon;
        }

        /// <summary>
        /// 6a: refills dome pools and brings back domes that are full again (ShieldDomeUpEvent), units in ascending ID order.
        /// Returns the IDs of the active domes (alive, up, built, owned), ascending.
        /// </summary>
        private static List<EntityId> RegenDomes(Simulation sim)
        {
            // Active dome IDs, ascending.
            List<EntityId> active = new List<EntityId>();
            // Current tick. Unit: ticks.
            int tick = sim.Clock.Tick;
            foreach (Unit unit in sim.Units.All)
            {
                // The unit's dome definition, or null for an ordinary unit.
                ShieldDomeDefinition? dome = unit.Definition.ShieldDome;
                if (dome == null)
                {
                    continue;
                }
                // Ticks without absorbing before the refill starts. Unit: ticks. Range: 0 to 1200.
                int delay = sim.Clock.SecondsToTicks(dome.RegenDelaySeconds);
                // Refill per tick. Unit: health points per tick. Range: above 0 (checked at Start).
                Fix64 rate = sim.Clock.PerSecondToPerTick(dome.RegenPerSecond);
                if (unit.ShieldPool < dome.Pool && (unit.ShieldLastAbsorbTick < 0 || tick - unit.ShieldLastAbsorbTick >= delay))
                {
                    unit.SetShield(Fix64.Min(dome.Pool, unit.ShieldPool + rate), unit.ShieldDown, unit.ShieldLastAbsorbTick);
                }
                if (unit.ShieldDown && unit.ShieldPool == dome.Pool)
                {
                    unit.SetShield(unit.ShieldPool, false, unit.ShieldLastAbsorbTick);
                    sim.Events.Add(new ShieldDomeUpEvent(tick, unit.Id));
                }
                if (unit.Health > Fix64.Zero && !unit.ShieldDown && !unit.IsUnderConstruction && unit.Owner.IsValid)
                {
                    active.Add(unit.Id);
                }
            }
            return active;
        }

        /// <summary>Cooldown, targeting, firing and attack-move steering for one unit.</summary>
        private static void StepUnit(Simulation sim, CombatRules rules, Unit unit, List<PendingKill> kills, List<EntityId> domes)
        {
            if (unit.CooldownTicksRemaining > 0)
            {
                unit.SetCooldownTicks(unit.CooldownTicksRemaining - 1);
            }
            // The unit's weapon; null for workers and other unarmed units.
            // An HQ fires only while Rooted, and a structure only once it is built (EffectiveWeapon).
            WeaponDefinition? weapon = EffectiveWeapon(unit);
            // True when the unit already had a target at the start of this tick (it may have just died or gone out of reach).
            bool hadTarget = unit.AttackTarget.IsValid;
            // The unit being fought this tick, or null.
            Unit? target = null;
            if (weapon != null)
            {
                // Extra reach for noticing and keeping targets. Unit: tiles. Range: 0 or the acquire bonus.
                Fix64 bonus = unit.IsAttackMoving ? rules.AcquireRangeBonusTiles : Fix64.Zero;
                // Weapon range after the active cycle's effect (Windstorm). Unit: tiles. Range: 1.5 or more for a ranged weapon.
                Fix64 range = sim.Cycles.EffectiveRangeTiles(unit, weapon);
                target = ResolveTarget(sim, unit, weapon, range, bonus);
                if (target != null)
                {
                    if (InReach(unit, target, range))
                    {
                        if (unit.IsAttackMoving && unit.HasMoveTarget)
                        {
                            // Stop walking to fight; a plain move order is never interrupted.
                            unit.ClearMoveTarget();
                        }
                        if (unit.CooldownTicksRemaining == 0)
                        {
                            Fire(sim, rules, unit, weapon, target, kills, domes);
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
        /// <param name="range">The weapon's effective range this tick (after the cycle effect). Unit: tiles. Range: above 0.</param>
        /// <param name="bonus">Extra reach beyond weapon range. Unit: tiles. Range: 0 or the acquire bonus.</param>
        private static Unit? ResolveTarget(Simulation sim, Unit unit, WeaponDefinition weapon, Fix64 range, Fix64 bonus)
        {
            // Largest centre-gap-minus-radii distance at which a target is kept or taken. Unit: tiles. Range: effective range plus bonus.
            Fix64 acquireRange = range + bonus;
            if (unit.AttackTarget.IsValid)
            {
                if (sim.Units.TryGet(unit.AttackTarget, out Unit? current) && IsValidTarget(sim, unit, weapon, current)
                    && (unit.IsAttackTargetForced
                        || (InReach(unit, current, acquireRange) && sim.Vision.IsUnitVisibleTo(sim.Players.TeamOf(unit.Owner), current))))
                {
                    return current;
                }
                unit.SetAttackTarget(EntityId.None, false);
            }
            // Best candidate so far and its squared centre distance. Unit: squared tiles. Range: 0 or more.
            Unit? best = null;
            Fix64 bestDistanceSquared = Fix64.Zero;
            // Rank of the best candidate: 0 armed, 1 unarmed. Range: 0 or 1.
            int bestRank = 1;
            // Team whose fog of war limits automatic acquisition (TeamId.None for a neutral attacker, which targets nothing anyway).
            TeamId attackerTeam = sim.Players.TeamOf(unit.Owner);
            foreach (Unit candidate in sim.Units.All)
            {
                if (!IsValidTarget(sim, unit, weapon, candidate) || !InReach(unit, candidate, acquireRange)
                    || !sim.Vision.IsUnitVisibleTo(attackerTeam, candidate))
                {
                    continue;
                }
                // Squared centre distance to this candidate. Unit: squared tiles. Range: 0 or more.
                Fix64 distanceSquared = (candidate.Position - unit.Position).LengthSquared();
                // Rank of this candidate: 0 armed, 1 unarmed. Range: 0 or 1.
                int rank = EffectiveWeapon(candidate) != null ? 0 : 1;
                // Armed first, then nearer; ascending-ID iteration plus strict comparisons give the lowest ID on ties.
                if (best == null || rank < bestRank || (rank == bestRank && distanceSquared < bestDistanceSquared))
                {
                    best = candidate;
                    bestDistanceSquared = distanceSquared;
                    bestRank = rank;
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
                && CanHitLayer(sim, attacker, weapon, target.EffectiveLayer);
        }

        /// <summary>True when the tile under <paramref name="unit"/>'s centre has water above 0 (any depth, any owner; combat_mechanics.md 3.1, Q8).</summary>
        internal static bool IsOnWater(Simulation sim, Unit unit)
        {
            return sim.Terrain[sim.Terrain.TileOf(unit.Position)].IsFlooded;
        }

        /// <summary>
        /// True when <paramref name="weapon"/> may hit a unit on <paramref name="layer"/> right now: the static layer list, or Air when the weapon has
        /// can_target_air_on_water and <paramref name="attacker"/> stands on water (combat_mechanics.md 3.1). The target's own tile does not matter.
        /// </summary>
        internal static bool CanHitLayer(Simulation sim, Unit attacker, WeaponDefinition weapon, MovementLayer layer)
        {
            return weapon.CanTarget(layer) || (layer == MovementLayer.Air && weapon.CanTargetAirOnWater && IsOnWater(sim, attacker));
        }

        /// <summary>
        /// True when <paramref name="attacker"/>'s weapon can hit <paramref name="target"/>'s effective layer in the current state of the match (the static
        /// layer list, or flyers while it stands on water). Layer only: range, vision, ownership and health are not read. For hosts and tools that must
        /// not declare a fight undecidable because of a conditional target rule. Read-only.
        /// </summary>
        public static bool CanAttack(Simulation sim, Unit attacker, Unit target)
        {
            WeaponDefinition? weapon = attacker.Definition.Weapon;
            return weapon != null && CanHitLayer(sim, attacker, weapon, target.EffectiveLayer);
        }

        /// <summary>
        /// True when the edge-to-edge distance between the two units is at most <paramref name="range"/>: the centre
        /// distance is compared, squared, with range plus both radii. A per-axis check first keeps the squares small.
        /// </summary>
        /// <param name="range">Edge-to-edge reach. Unit: tiles. Range: 0 or more.</param>
        internal static bool InReach(Unit attacker, Unit target, Fix64 range)
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

        /// <summary>
        /// True when <paramref name="u"/> counts as a structure for the weapon's structure_damage_multiplier (combat_mechanics.md section 0): a unit with a
        /// footprint (the rule AiWorldView.IsStructure uses) or an HQ in state Rooted.
        /// </summary>
        internal static bool IsStructureTarget(Unit u)
        {
            return u.Definition.Economy.FootprintTiles > 0 || (u.Definition.Hq != null && u.HqState == HqState.Rooted);
        }

        /// <summary>True when a ranged hit from <paramref name="attacker"/> on <paramref name="target"/> is uphill (A.4.1): neither is airborne and the target's tile is higher than the attacker's eye.</summary>
        private static bool IsUphill(Simulation sim, Unit attacker, Unit target, WeaponDefinition weapon)
        {
            if (weapon.IsMelee || attacker.EffectiveLayer == MovementLayer.Air || target.EffectiveLayer == MovementLayer.Air)
            {
                return false;
            }
            // Tile the attacker stands on; a ramp lifts its eye by one level (the vision rule).
            Rts.Sim.Terrain.TerrainTile from = sim.Terrain[sim.Terrain.TileOf(attacker.Position)];
            // Tile the target stands on.
            Rts.Sim.Terrain.TerrainTile to = sim.Terrain[sim.Terrain.TileOf(target.Position)];
            return to.Height > from.Height + (from.IsRamp ? 1 : 0);
        }

        /// <summary>Fires one shot: damage fixed now, shot event, cooldown, dome absorption, then the hit lands at once or is queued.</summary>
        private static void Fire(Simulation sim, CombatRules rules, Unit attacker, WeaponDefinition weapon, Unit target, List<PendingKill> kills, List<EntityId> domes)
        {
            // Current tick. Unit: ticks.
            int tick = sim.Clock.Tick;
            // Damage after the table multiplier. Unit: health points. Range: 0 or more.
            Fix64 damage = weapon.Damage * rules.GetMultiplier(weapon.DamageType, target.ArmourType);
            if (IsStructureTarget(target))
            {
                damage = damage * weapon.StructureDamageMultiplier;
            }
            if (IsUphill(sim, attacker, target, weapon))
            {
                damage = damage * rules.UphillRangedDamageMultiplier;
            }
            // True when this weapon's hit carries a blast.
            bool hasBlast = weapon.SplashRadiusTiles > Fix64.Zero;
            // Base damage each blast victim takes before its own armour. Unit: health points. Range: 0 or more; 0 without a blast.
            Fix64 splashBase = hasBlast ? weapon.Damage * rules.SplashDamageFraction : Fix64.Zero;
            foreach (IDamageModifier modifier in sim.DamageModifiers)
            {
                // Mechanic multiplier for this hit; a negative answer counts as 0 so damage never heals.
                Fix64 factor = Fix64.Max(Fix64.Zero, modifier.OutgoingDamageMultiplier(sim, attacker));
                damage = damage * factor;
                if (hasBlast)
                {
                    splashBase = splashBase * factor;
                }
            }
            // Tick the hit lands. Unit: ticks. Range: the current tick to current tick + 10.
            int landTick = tick + weapon.HitDelayTicks;
            sim.Events.Add(new ShotFiredEvent(tick, attacker.Id, target.Id, landTick, weapon.IsMelee));
            // Ticks until the next shot, at least 1 so a very short cooldown still spaces the hits. Unit: ticks. Range: 1 or more.
            attacker.SetCooldownTicks(Math.Max(1, sim.Clock.SecondsToTicks(weapon.CooldownSeconds)));
            if (!weapon.IsMelee && damage > Fix64.Zero && target.Owner.IsValid)
            {
                // Damage still to place after the domes. Unit: health points. Range: 0 up to the shot's damage.
                Fix64 remaining = AbsorbByDomes(sim, domes, attacker.Id, attacker.Position, target, damage);
                if (remaining <= Fix64.Zero)
                {
                    // The dome caught the whole shell: no hit, no blast (combat_mechanics.md 1.1 step 8).
                    return;
                }
                damage = remaining;
            }
            // Attacker definition ID when the hit has a blast, else "". Range: a unit definition ID or "".
            string sourceId = hasBlast ? attacker.Definition.Id : string.Empty;
            // Layers the blast reaches, from the weapon (air also when the attacker stands on water now). Unit: bit mask. Range: 0 to 7 (bit 0 ground, bit 1 water, bit 2 air); 0 without a blast.
            byte splashLayers = hasBlast ? (byte)((weapon.CanTargetGround ? 1 : 0) | (weapon.CanTargetWater ? 2 : 0) | ((weapon.CanTargetAir || (weapon.CanTargetAirOnWater && IsOnWater(sim, attacker))) ? 4 : 0)) : (byte)0;
            // Blast centre if the target is gone at landing. Unit: tiles. Range: inside the map; zero without a blast.
            FixVec2 fallback = hasBlast ? target.Position : FixVec2.Zero;
            if (weapon.HitDelayTicks == 0)
            {
                Land(sim, new PendingHit(landTick, 0, attacker.Id, attacker.Owner, target.Id, damage, weapon.AppliesDrench, sourceId, splashBase, splashLayers, fallback), kills, domes);
            }
            else
            {
                sim.PendingHits.Enqueue(landTick, attacker.Id, attacker.Owner, target.Id, damage, weapon.AppliesDrench, sourceId, splashBase, splashLayers, fallback);
            }
        }

        /// <summary>
        /// Lets the allied domes that cover <paramref name="victim"/> absorb <paramref name="damage"/>, lowest dome ID first (combat_mechanics.md 1.1 step 7).
        /// A dome absorbs when it is up, the victim's centre is inside its radius and <paramref name="from"/> is outside it. Updates pools and
        /// emits ShieldDomeHitEvent and ShieldDomeDownEvent. Used at fire time (from = attacker position) and for blast victims (from = blast centre).
        /// Returns the damage left after all domes.
        /// </summary>
        /// <param name="domes">Active dome unit IDs, ascending (this tick's list).</param>
        /// <param name="attackerId">Unit credited in the events.</param>
        /// <param name="from">Where the damage comes from. Unit: tiles.</param>
        /// <param name="victim">The unit that would take the damage.</param>
        /// <param name="damage">Damage before the domes. Unit: health points. Range: above 0.</param>
        private static Fix64 AbsorbByDomes(Simulation sim, List<EntityId> domes, EntityId attackerId, FixVec2 from, Unit victim, Fix64 damage)
        {
            // Current tick. Unit: ticks.
            int tick = sim.Clock.Tick;
            // Damage still to place after the domes. Unit: health points. Range: 0 up to damage.
            Fix64 remaining = damage;
            foreach (EntityId domeId in domes)
            {
                if (remaining <= Fix64.Zero)
                {
                    break;
                }
                if (!sim.Units.TryGet(domeId, out Unit? dome) || dome.Health <= Fix64.Zero || dome.ShieldDown || dome.Definition.ShieldDome == null
                    || !sim.Players.AreAllied(dome.Owner, victim.Owner))
                {
                    continue;
                }
                // Dome radius and its square. Unit: tiles and squared tiles. Range: 0.5 to 16.
                Fix64 radius = dome.Definition.ShieldDome.RadiusTiles;
                Fix64 radiusSquared = radius * radius;
                if ((victim.Position - dome.Position).LengthSquared() > radiusSquared || (from - dome.Position).LengthSquared() <= radiusSquared)
                {
                    continue;
                }
                // Damage this dome takes. Unit: health points. Range: 0 to the dome's pool.
                Fix64 absorbed = Fix64.Min(remaining, dome.ShieldPool);
                if (absorbed <= Fix64.Zero)
                {
                    continue;
                }
                remaining = remaining - absorbed;
                dome.SetShield(dome.ShieldPool - absorbed, false, tick);
                sim.Events.Add(new ShieldDomeHitEvent(tick, dome.Id, attackerId, victim.Id, absorbed, dome.ShieldPool));
                if (dome.ShieldPool == Fix64.Zero)
                {
                    dome.SetShield(Fix64.Zero, true, tick);
                    sim.Events.Add(new ShieldDomeDownEvent(tick, dome.Id));
                }
            }
            return remaining;
        }

        /// <summary>
        /// Lands one hit (A.4.5): the primary target takes it unless it is gone or dead (damage, 0 on an invulnerable target, event, drench, pending kill);
        /// then, when the hit carries a blast, the blast falls even if the primary is gone (combat_mechanics.md 1.1, 1.3).
        /// </summary>
        /// <param name="domes">Active dome unit IDs of this tick, ascending.</param>
        private static void Land(Simulation sim, PendingHit hit, List<PendingKill> kills, List<EntityId> domes)
        {
            // True when the primary target is still registered (alive or at 0 health this tick).
            bool registered = sim.Units.TryGet(hit.Target, out Unit? target);
            if (registered && target!.Health > Fix64.Zero)
            {
                // Damage dealt. Unit: health points. Range: 0 or more.
                Fix64 damage = hit.Damage;
                if (target.IsInvulnerable)
                {
                    // Last Stand: the hit lands but does nothing (reported with damage 0).
                    damage = Fix64.Zero;
                }
                else
                {
                    target.SetHealth(target.Health - damage);
                    HqSystem.AfterDamage(sim, target);
                }
                sim.Events.Add(new UnitDamagedEvent(sim.Clock.Tick, hit.Attacker, target.Id, damage, target.Health));
                if (hit.AppliesDrench)
                {
                    // Spoutling drench: the status system counts the hit, slows the flyer and grounds it at the threshold (statuses.md 4). It ignores targets that are not in the air.
                    StatusSystem.ApplyDrenchHit(sim, hit.Attacker, hit.AttackerOwner, target);
                }
                if (target.Health <= Fix64.Zero)
                {
                    kills.Add(new PendingKill(target.Id, hit.Attacker, hit.AttackerOwner));
                }
            }
            if (hit.HasBlast)
            {
                // Blast centre: the target's position now if it is still registered, else where it stood at fire time. Unit: tiles.
                FixVec2 centre = registered ? target!.Position : hit.BlastFallback;
                Blast(sim, hit, centre, kills, domes);
            }
        }

        /// <summary>
        /// Resolves a blast (combat_mechanics.md 1.1 steps 3 to 11): collects the enemy victims on the weapon's layers within the radius
        /// (ascending ID, before anyone is damaged), reports it, then damages each victim with its own armour, domes, Last Stand and kill credit.
        /// </summary>
        /// <param name="centre">Blast centre. Unit: tiles.</param>
        private static void Blast(Simulation sim, PendingHit hit, FixVec2 centre, List<PendingKill> kills, List<EntityId> domes)
        {
            if (!sim.Content.Units.TryGetValue(hit.SourceDefinitionId, out UnitDefinition? source) || source.Weapon == null)
            {
                return;
            }
            // Weapon whose blast this is (frozen catalogue). Gives the damage type and radius.
            WeaponDefinition weapon = source.Weapon;
            // Damage table of this match.
            CombatRules rules = sim.Content.Combat;
            // Victims in ascending ID order. Scratch, not state.
            List<Unit> victims = new List<Unit>();
            foreach (Unit v in sim.Units.All)
            {
                if (v.Id == hit.Target || v.Health <= Fix64.Zero || !v.Owner.IsValid || !hit.AttackerOwner.IsValid
                    || !sim.Players.AreEnemies(hit.AttackerOwner, v.Owner))
                {
                    continue;
                }
                // Bit of the victim's layer in the layer mask. Range: 1, 2 or 4.
                int layerBit = 1 << (int)v.EffectiveLayer;
                if ((hit.SplashLayers & layerBit) == 0)
                {
                    continue;
                }
                // Largest centre distance that still counts: radius plus the victim's own radius. Unit: tiles. Range: up to 4 plus a unit radius.
                Fix64 reach = weapon.SplashRadiusTiles + v.Definition.Radius;
                // Offset from the blast centre to the victim. Unit: tiles.
                FixVec2 delta = v.Position - centre;
                if (delta.X.Abs() > reach || delta.Y.Abs() > reach || delta.LengthSquared() > reach * reach)
                {
                    continue;
                }
                victims.Add(v);
            }
            sim.Events.Add(new BlastEvent(sim.Clock.Tick, hit.Attacker, hit.AttackerOwner, centre, weapon.SplashRadiusTiles, victims.Count));
            foreach (Unit v in victims)
            {
                // Damage after the victim's own armour row. Unit: health points. Range: 0 or more.
                Fix64 damage = hit.SplashDamage * rules.GetMultiplier(weapon.DamageType, v.ArmourType);
                if (IsStructureTarget(v))
                {
                    // The victim's state now decides (an HQ that Uprooted while the shell flew is no structure).
                    damage = damage * weapon.StructureDamageMultiplier;
                }
                if (damage > Fix64.Zero)
                {
                    // Damage left after domes covering the victim, fired from the blast centre.
                    damage = AbsorbByDomes(sim, domes, hit.Attacker, centre, v, damage);
                    if (damage <= Fix64.Zero)
                    {
                        continue;
                    }
                }
                if (v.IsInvulnerable)
                {
                    damage = Fix64.Zero;
                }
                else
                {
                    v.SetHealth(v.Health - damage);
                    HqSystem.AfterDamage(sim, v);
                }
                sim.Events.Add(new UnitDamagedEvent(sim.Clock.Tick, hit.Attacker, v.Id, damage, v.Health));
                if (v.Health <= Fix64.Zero)
                {
                    kills.Add(new PendingKill(v.Id, hit.Attacker, hit.AttackerOwner));
                }
            }
        }
    }
}

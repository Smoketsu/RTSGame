// rts/src/Sim/Statuses/StatusSystem.cs
// The shared status system: Grounded, Slowed and the Spoutling drench counter. Part of Rts.Sim.Statuses
// (spec: rts/docs/specs/statuses.md). Runs as step 3 of Simulation.Step (after commands, before movement) through
// BeginTick, and exposes the Apply methods that commands and combat call inside a step.
//
// This is the ONLY code that sets or clears Unit.LayerOverride and Unit.StatusSpeedMultiplier. UnitStatusSet is the
// source of truth; after every change the two derived values are pushed into the Unit, which hashes them.
// A status is active on tick k when k is below its UntilTick. Slows never multiply together: the strongest (smallest
// multiplier) wins. No faction or unit ID is named here; the "drench" key is a source label from StatusTuning.
// Determinism: units are visited in ascending ID order, slows in ascending key order, no random numbers are drawn.
using System;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Statuses
{
    /// <summary>Per-tick expiry plus the operations that start statuses.</summary>
    public static class StatusSystem
    {
        /// <summary>
        /// Step 3 of the simulation step. For each unit in ascending ID order: (1) end an expired Grounded (clear the
        /// override, emit status_expired then unit_layer_changed Ground to Air), (2) remove expired slows in ascending key
        /// order (emit status_expired each) and refresh the status speed multiplier, (3) reset the drench counter when the
        /// latest hit is old enough (no event).
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        internal static void BeginTick(Simulation sim)
        {
            // Current tick. Unit: ticks. Range: 0 upward.
            int tick = sim.Clock.Tick;
            // Durations in ticks for this match.
            StatusRates rates = new StatusRates(sim.Tuning.Status, sim);
            foreach (Unit unit in sim.Units.All)
            {
                // The unit's status container.
                UnitStatusSet set = unit.Statuses;
                if (!set.HasAnyState)
                {
                    continue;
                }
                if (set.GroundedUntilTick != 0 && tick >= set.GroundedUntilTick)
                {
                    // Source key of the status that ends, kept for the event.
                    string key = set.GroundedSourceKey;
                    set.ClearGrounded();
                    unit.ClearLayerOverride();
                    sim.Events.Add(new StatusExpiredEvent(tick, unit.Id, StatusKind.Grounded, key));
                    sim.Events.Add(new UnitLayerChangedEvent(tick, unit.Id, MovementLayer.Ground, unit.Definition.Layer, unit.Position));
                }
                // Position in the sorted slow list. Unit: list position. Range: 0 to Count.
                int index = 0;
                while (index < set.Slows.Count)
                {
                    // The entry under test.
                    SlowEntry entry = set.Slows[index];
                    if (tick >= entry.UntilTick)
                    {
                        set.RemoveSlowAt(index);
                        sim.Events.Add(new StatusExpiredEvent(tick, unit.Id, StatusKind.Slowed, entry.SourceKey));
                    }
                    else
                    {
                        index++;
                    }
                }
                unit.SetStatusSpeedMultiplier(set.SpeedMultiplier);
                if (set.LastDrenchTick >= 0 && tick - set.LastDrenchTick >= rates.DrenchResetTicks)
                {
                    set.SetDrench(0, -1);
                }
            }
        }

        /// <summary>
        /// Grounds a flyer for <paramref name="durationTicks"/>. Returns false (nothing happens) unless the target's
        /// definition layer is Air. Already Grounded: refresh to the later end tick and keep the old key (never extend by
        /// adding durations). Otherwise start it, set the layer override, relocate off a blocked tile (landing) and emit
        /// unit_layer_changed Air to Ground. Always resets the drench counter and emits status_applied.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="target">The unit to ground.</param>
        /// <param name="durationTicks">Length from now. Unit: ticks. Range: 1 or more.</param>
        /// <param name="sourceKey">Source label, 1 to 64 characters of a-z, 0-9, underscore, dot.</param>
        /// <param name="sourcePlayer">Player responsible, for attribution only; not stored for Grounded. Range: any player, None for scripts.</param>
        internal static bool ApplyGrounded(Simulation sim, Unit target, int durationTicks, string sourceKey, PlayerId sourcePlayer)
        {
            RequireStepping(sim);
            if (durationTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks), "A status lasts at least one tick.");
            }
            SlowEntry.RequireValidKey(sourceKey, nameof(sourceKey));
            if (target.Definition.Layer != MovementLayer.Air)
            {
                return false;
            }
            // Current tick and the first tick the new Grounded would no longer be active.
            int now = sim.Clock.Tick;
            int until = now + durationTicks;
            UnitStatusSet set = target.Statuses;
            // True when the unit was already Grounded.
            bool refreshed = set.IsGrounded;
            if (refreshed)
            {
                set.SetGrounded(Math.Max(set.GroundedUntilTick, until), set.GroundedSourceKey);
            }
            else
            {
                set.SetGrounded(until, sourceKey);
                target.SetLayerOverride(MovementLayer.Ground);
                Land(sim, target);
                sim.Events.Add(new UnitLayerChangedEvent(now, target.Id, MovementLayer.Air, MovementLayer.Ground, target.Position));
            }
            set.SetDrench(0, -1);
            sim.Events.Add(new StatusAppliedEvent(now, target.Id, StatusKind.Grounded, set.GroundedSourceKey, Fix64.One, set.GroundedUntilTick, refreshed));
            return true;
        }

        /// <summary>
        /// Slows a unit of any layer. The same key refreshes (new multiplier, later of the two end ticks). A new key is
        /// inserted in key order while below max_slow_sources_per_unit; at the cap it replaces the entry that ends soonest
        /// (ties: smallest key). Refreshes the status speed multiplier and emits status_applied.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="target">The unit to slow.</param>
        /// <param name="sourceKey">Source label, 1 to 64 characters of a-z, 0-9, underscore, dot.</param>
        /// <param name="multiplier">Speed multiplier. Unit: multiplier of speed. Range: above 0 and at most 1; otherwise ArgumentOutOfRangeException.</param>
        /// <param name="durationTicks">Length from now. Unit: ticks. Range: 1 or more; otherwise ArgumentOutOfRangeException.</param>
        /// <param name="sourcePlayer">Player responsible. Range: any player, None for scripts.</param>
        internal static void ApplySlow(Simulation sim, Unit target, string sourceKey, Fix64 multiplier, int durationTicks, PlayerId sourcePlayer)
        {
            RequireStepping(sim);
            if (multiplier <= Fix64.Zero || multiplier > Fix64.One)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), "A slow multiplier must be above 0 and at most 1.");
            }
            if (durationTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks), "A status lasts at least one tick.");
            }
            SlowEntry.RequireValidKey(sourceKey, nameof(sourceKey));
            // Current tick and the first tick the new slow would no longer be active.
            int now = sim.Clock.Tick;
            int until = now + durationTicks;
            UnitStatusSet set = target.Statuses;
            // Position of an existing entry with this key, or -1.
            int existing = set.IndexOfSlow(sourceKey);
            // True when an active entry with this key was updated.
            bool refreshed = existing >= 0;
            // The entry as stored after this call.
            SlowEntry stored;
            if (refreshed)
            {
                stored = new SlowEntry(sourceKey, multiplier, Math.Max(set.Slows[existing].UntilTick, until), sourcePlayer);
                set.ReplaceSlowAt(existing, stored);
            }
            else
            {
                stored = new SlowEntry(sourceKey, multiplier, until, sourcePlayer);
                if (set.Slows.Count >= sim.Tuning.Status.MaxSlowSourcesPerUnit)
                {
                    // Position of the entry that ends soonest; the list is in key order, so a strict comparison keeps the smallest key on ties.
                    int victim = 0;
                    for (int i = 1; i < set.Slows.Count; i++)
                    {
                        if (set.Slows[i].UntilTick < set.Slows[victim].UntilTick)
                        {
                            victim = i;
                        }
                    }
                    set.RemoveSlowAt(victim);
                }
                set.InsertSlow(stored);
            }
            target.SetStatusSpeedMultiplier(set.SpeedMultiplier);
            sim.Events.Add(new StatusAppliedEvent(now, target.Id, StatusKind.Slowed, sourceKey, multiplier, stored.UntilTick, refreshed));
        }

        /// <summary>
        /// Called by combat once per landed hit of a weapon with applies_drench. Nothing happens unless the target's
        /// effective layer is Air (a ground unit or an already Grounded flyer is ignored, with no event). Otherwise: count
        /// the hit, remember its tick, apply the drench slow, and at the hit threshold ground the target (which resets the
        /// counter). Emits drench_hit with the count reached and whether it grounded.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="attacker">The unit that fired.</param>
        /// <param name="target">The unit that was hit.</param>
        internal static void ApplyDrenchHit(Simulation sim, Unit attacker, Unit target)
        {
            RequireStepping(sim);
            if (target.EffectiveLayer != MovementLayer.Air)
            {
                return;
            }
            // Tuning and durations for the drench rule.
            StatusTuning tuning = sim.Tuning.Status;
            StatusRates rates = new StatusRates(tuning, sim);
            UnitStatusSet set = target.Statuses;
            // Current tick.
            int now = sim.Clock.Tick;
            set.SetDrench(set.DrenchHits + 1, now);
            // Hits counted including this one. Unit: hits. Range: 1 to spoutling_drench_hits_to_ground.
            int hits = set.DrenchHits;
            ApplySlow(sim, target, StatusTuning.DrenchSourceKey, tuning.DrenchSlowMultiplier, rates.DrenchSlowTicks, attacker.Owner);
            // True when this hit grounded the target.
            bool grounded = false;
            if (hits >= tuning.SpoutlingDrenchHitsToGround)
            {
                grounded = ApplyGrounded(sim, target, rates.GroundedTicks, StatusTuning.DrenchSourceKey, attacker.Owner);
            }
            sim.Events.Add(new DrenchHitEvent(now, target.Id, attacker.Id, hits, grounded));
        }

        /// <summary>
        /// Landing: if the flyer's tile is blocked for the ground layer (a wall or footprint), scan Chebyshev rings 1 to
        /// grounded_landing_search_radius_tiles around it, tiles in ascending index within a ring, and move the unit to the
        /// centre of the first in-map standable tile. If none is found the position is unchanged.
        /// </summary>
        private static void Land(Simulation sim, Unit unit)
        {
            TerrainGrid terrain = sim.Terrain;
            // Tile the unit hovers over.
            int home = terrain.TileOf(unit.Position);
            if (terrain.IsStandable(home, MovementLayer.Ground))
            {
                return;
            }
            // Tile coordinates of the home tile. Unit: tiles.
            int homeX = terrain.XOf(home);
            int homeY = terrain.YOf(home);
            // Largest ring to search. Unit: tiles. Range: 0 to 8.
            int radius = sim.Tuning.Status.GroundedLandingSearchRadiusTiles;
            for (int ring = 1; ring <= radius; ring++)
            {
                // Rows from the lowest index (smallest y) up, then columns left to right: ascending tile index.
                for (int y = homeY - ring; y <= homeY + ring; y++)
                {
                    for (int x = homeX - ring; x <= homeX + ring; x++)
                    {
                        // Chebyshev distance from the home tile; only tiles exactly on this ring count.
                        int distance = Math.Max(Math.Abs(x - homeX), Math.Abs(y - homeY));
                        if (distance != ring || !terrain.Contains(x, y))
                        {
                            continue;
                        }
                        // Candidate landing tile.
                        int candidate = terrain.IndexOf(x, y);
                        if (terrain.IsStandable(candidate, MovementLayer.Ground))
                        {
                            unit.SetPosition(terrain.CentreOf(candidate));
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>Throws unless called inside a simulation step, so statuses only change through commands, combat and mechanics.</summary>
        private static void RequireStepping(Simulation sim)
        {
            if (!sim.IsStepping)
            {
                throw new InvalidOperationException("Statuses can only change inside Simulation.Step.");
            }
        }
    }
}

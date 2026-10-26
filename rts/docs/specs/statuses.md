<!-- rts/docs/specs/statuses.md -->
<!-- Implementation spec for the shared statuses Grounded and Slowed, the Spoutling drench counter, the per-unit status container, the effective movement layer and the movement speed chain (terrain x status x mechanics). Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by sim-reviewer. -->
# Spec: Shared statuses (Grounded, Slowed, drench)

## 1. Purpose and design-doc source

- **Grounded** (Factions / Grounded, a shared status): "Every faction can force flyers to land. A Grounded flyer lands for 4 seconds and counts as a ground unit: walls block it, melee can hit it and floods slow it." Sources: Spoutling Drench, Gravity Spire, Stormcaller Downdraft.
- **Drench** (Water / Unit concepts): "Spoutling (anti-air: water jets that Drench flyers, grounding them after 3 hits ...)".
- **Slowed** (Skai / Momentum): Momentum "is lost entirely when the unit is Grounded or slowed ... opponents answer by grounding or slowing them." Development plan: "Grounded and slowed are core statuses any faction can apply."
- No status belongs to a faction; the core never names one (architecture rule 4).

## 2. Data

Existing keys in `tuning/statuses.json`: `grounded_duration_seconds` (4), `spoutling_drench_hits_to_ground` (3).

New keys in `tuning/statuses.json` (add to `StatusTuning`; all **placeholders**):

| Key | Unit | Range | Default | Controls |
| --- | --- | --- | --- | --- |
| `spoutling_drench_reset_seconds` | seconds | 0.5 to 30 | 5 | A drench hit is forgotten this long after the latest hit. |
| `drench_slow_multiplier` | multiplier of speed | 0.1 to 1 | 0.75 | Slow each drench hit applies to a flyer. |
| `drench_slow_duration_seconds` | seconds | 0.1 to 10 | 2 | Duration of that slow. |
| `grounded_landing_search_radius_tiles` | tiles | 0 to 8 | 3 | How far a flyer grounded over a blocked tile searches for a landing tile. |
| `max_slow_sources_per_unit` | entries | 1 to 8 | 4 | Distinct slow sources a unit tracks at once. |

Unit data: the Spoutling's weapon carries `applies_drench: true` (bool, default false). The combat spec owns the weapon schema; if combat has no on-hit list yet, the field sits on the unit and combat reads it there.

Tick conversions at match start (`SecondsToTicks`, round to nearest): Grounded 80, drench reset 100, drench slow 40 ticks at 20 tps. A conversion giving 0 is a load error ("must be at least one tick").

## 3. State

New `UnitStatusSet` (`rts/src/Sim/Statuses/UnitStatusSet.cs`), one per unit as `Unit.Statuses`, created with the unit and dropped with it:

| Field | Type | Unit | Range |
| --- | --- | --- | --- |
| `GroundedUntilTick` | int | ticks | 0 = not Grounded; else the first tick it is no longer active |
| `GroundedSourceKey` | string | id | "" when not Grounded |
| `Slows` | list of `SlowEntry`, sorted by `SourceKey` ordinal | | 0 to `max_slow_sources_per_unit` entries |
| `SlowEntry.SourceKey` | string | id such as `"drench"` | 1 to 64 chars, `[a-z0-9_.]` |
| `SlowEntry.Multiplier` | Fix64 | multiplier of speed | above 0, at most 1 |
| `SlowEntry.UntilTick` | int | ticks | above the apply tick |
| `SlowEntry.SourcePlayer` | PlayerId | | any, None for scripts |
| `DrenchHits` | int | hits | 0 to hits_to_ground - 1 |
| `LastDrenchTick` | int | ticks | -1 = none |

A status is **active** on tick `k` when `k < UntilTick`. Hash order, appended at the end of `Unit.WriteHash` (after `SpawnTick` and momentum.md's `LastSelfMoveTick`): `GroundedUntilTick`, `GroundedSourceKey`, slow count, then per entry `SourceKey`, `Multiplier`, `UntilTick`, `SourcePlayer`; then `DrenchHits`, `LastDrenchTick`.

Queries (pure, any time): `IsGrounded => GroundedUntilTick != 0`; `IsSlowed => Slows.Count > 0`; `SpeedMultiplier` = minimum `Multiplier` over entries, `One` when empty (the strongest slow wins; slows never multiply together); `StatusRules.EffectiveLayer(unit)` = `Ground` when `Definition.Layer == Air` and `IsGrounded`, otherwise `Definition.Layer`.

## 4. Per-tick algorithm

New step **3, statuses**, between commands and movement (Simulation header updated; see README): `StatusSystem.BeginTick(sim)` visits units in ascending ID and, per unit, in this fixed order:
1. Grounded: if `GroundedUntilTick != 0 && tick >= GroundedUntilTick`: clear it, emit `StatusExpiredEvent(Grounded)` then `UnitLayerChangedEvent(Ground -> Air)`.
2. Slows, ascending key: remove each entry with `tick >= UntilTick`, emitting `StatusExpiredEvent(Slowed, key)`.
3. Drench: if `LastDrenchTick >= 0 && tick - LastDrenchTick >= resetTicks`: `DrenchHits = 0`, `LastDrenchTick = -1` (no event).

Apply methods (`StatusSystem`, internal, throw outside a step; `now = sim.Clock.Tick`):
- `ApplyGrounded(sim, target, durationTicks, sourceKey, sourcePlayer) -> bool`: false (nothing happens) unless `target.Definition.Layer == Air`. `until = now + durationTicks`. Already Grounded: **refresh**, `GroundedUntilTick = max(old, until)`, keep the old key, `refreshed = true`. Otherwise set both, run **landing**, emit `UnitLayerChangedEvent(Air -> Ground)`. Always: `DrenchHits = 0`, `LastDrenchTick = -1`, emit `StatusAppliedEvent`. Refresh, never extend: re-applying cannot add durations together.
- Landing: if the unit's tile is off-map or has a `Wall` or `Footprint` blocker, scan Chebyshev rings `r = 1 .. grounded_landing_search_radius_tiles`, tiles in ascending index within a ring; the first in-map tile without those blockers becomes the position `(x + 1/2, y + 1/2)`. None found: position unchanged. The move order is kept; movement now follows ground fields (ADR-007 layer switching).
- `ApplySlow(sim, target, sourceKey, multiplier, durationTicks, sourcePlayer)`: throws `ArgumentOutOfRangeException` unless `0 < multiplier <= 1` and `durationTicks >= 1`. Any layer. Same key present: replace `Multiplier`, `UntilTick = max(old, now + durationTicks)`, `refreshed = true`. Else if below the cap: insert in key order. Else: replace the entry with the smallest `UntilTick` (ties: smallest key). Emit `StatusAppliedEvent`.
- `ApplyDrenchHit(sim, attacker, target)`: called by combat once per landed hit of an `applies_drench` weapon. Nothing happens unless `EffectiveLayer(target) == Air`. Then: `DrenchHits += 1`, `LastDrenchTick = now`; `ApplySlow(target, "drench", drench_slow_multiplier, drenchSlowTicks, attacker.Owner)`; if `DrenchHits >= spoutling_drench_hits_to_ground`, `ApplyGrounded(target, groundedTicks, "drench", attacker.Owner)` (which resets the counter). Emit `DrenchHitEvent` with the count reached and whether it grounded.

### Speed chain (owned here, wired into movement by the coordinator)

`rts/src/Sim/Units/SpeedChain.cs`, the only place a unit's speed is computed. Evaluate exactly in this order, each `*` a Fix64 multiply (truncating, so order matters):
```
s = unit.Definition.SpeedTilesPerSecond
s = s * TerrainSpeedTable.Multiplier(sim, unit, tileIndex)   // ADR-006: water table (flooding.md 4) then surface table
s = s * unit.Statuses.SpeedMultiplier                         // this spec
m = One
foreach mechanic in sim.Mechanics.Values (ascending ordinal ID)
    if mechanic is IUnitModifierProvider p: m = m * p.SpeedMultiplier(sim, unit)   // momentum.md
s = s * m
return Fix64.Max(s, Zero)        // tiles per second; the movement step is Clock.PerSecondToPerTick(s)
```
`TerrainSpeedTable` uses `EffectiveLayer`, so a Grounded flyer is slowed by enemy water. Flow-field cost fields (ADR-007) use only the terrain factor.

## 5. Commands and events

- `ApplyStatusCommand(tick, issuer, sequence, EntityId unit, StatusKind kind, Fix64 multiplier, Fix64 durationSeconds, string sourceKey)`, script name `apply_status` (fields `unit`, `status`: `"grounded"`/`"slowed"`, `multiplier`, `seconds`, `source`). For scripts, tests and campaign triggers. Rejections in order: `status_requires_script_issuer` (issuer is not `PlayerId.None`), `unknown_unit`, `invalid_duration` (seconds of 0 or below, or 0 ticks; for Grounded an omitted duration means `grounded_duration_seconds`), `invalid_multiplier` (Slowed outside (0, 1]), `not_a_flyer` (Grounded on a non-air definition).
- `enum StatusKind : byte { Grounded = 0, Slowed = 1 }` (stable values; evaluation order).
- `StatusAppliedEvent(tick, unit, kind, sourceKey, multiplier, untilTick, refreshed)` (`"status_applied"`); `StatusExpiredEvent(tick, unit, kind, sourceKey)` (`"status_expired"`); `UnitLayerChangedEvent(tick, unit, MovementLayer from, MovementLayer to, FixVec2 position)` (`"unit_layer_changed"`); `DrenchHitEvent(tick, target, attacker, int hits, bool grounded)` (`"drench_hit"`). Unit removal emits no status events.

## 6. Interfaces

- `Unit` gains `Statuses` (constructed in the `Unit` constructor) and the hash fields above; targeted edit only.
- `Simulation.Step` calls `StatusSystem.BeginTick` after `ApplyCommands` and before `MovementSystem.Step`.
- `IUnitModifierProvider` (`rts/src/Sim/Units/IUnitModifierProvider.cs`): `Fix64 SpeedMultiplier(Simulation, Unit)` and `Fix64 DamageMultiplier(Simulation, Unit)`, both `One` for units the provider does not affect. The statuses builder creates the interface and `SpeedChain`; the coordinator (Fable, owner of `MovementSystem`) switches movement to `SpeedChain` and to `EffectiveLayer` for field selection.
- Consumers: movement (layer and speed), combat (`EffectiveLayer` decides whether melee and ground-only weapons may target a flyer), vision (airborne sight only while the effective layer is Air), Momentum (`IsGrounded`, `IsSlowed`), flooding (speed table).
- Must NOT: name a faction or unit ID in code (the `"drench"` key is a source label, set in `StatusTuning` as a constant documented there), draw RNG, or stack slows multiplicatively.

## 7. Acceptance tests (`rts/tests/Sim.Tests/Statuses/`)

1. **Grounded lifecycle.** Air unit at tick 0; `apply_status grounded` at tick 10: after step 10 `EffectiveLayer == Ground`, `GroundedUntilTick == 90`; still Ground after step 89; step 90's events contain `status_expired` then `unit_layer_changed` (Ground to Air), and the layer is Air.
2. **Refresh, not extend.** Grounded at tick 10 and again at tick 11: `GroundedUntilTick == 91`; the second `StatusAppliedEvent.Refreshed` is true.
3. **Only flyers.** Grounded on a ground unit is rejected `not_a_flyer`; `ApplyGrounded` returns false; no status, no event.
4. **Slow speed.** Ground unit speed 2, flat open ground heading east, slowed 0.5 for 2 s at tick 0: X advances 0.05 per tick on ticks 0 to 39 and 0.1 from tick 40; `status_expired` at tick 40.
5. **Strongest wins.** At tick 0, key `a` 0.75 for 100 ticks and key `b` 0.5 for 20 ticks: `SpeedMultiplier` is 0.5 on ticks 0 to 19, 0.75 on 20 to 99, `One` from 100.
6. **Same key refreshes.** Key `a` 0.5 for 40 ticks at tick 0, then key `a` 0.75 for 10 ticks at tick 10: one entry, multiplier 0.75, `UntilTick == 40`.
7. **Cap.** Cap 4; keys `a` to `d` with `UntilTick` 10, 20, 30, 40, then `e` until 50: keys are `b, c, d, e`.
8. **Drench grounds on the third hit.** Spoutling hits an air unit at ticks 0, 1, 2: after tick 2 `GroundedUntilTick == 82`, `DrenchHits == 0`, slow `drench` 0.75 with `UntilTick == 42`, last `DrenchHitEvent` has hits 3 and grounded true.
9. **Drench timeout.** Hits at ticks 0, 50 and 160: `DrenchHits == 1` after tick 160 (reset at the start of tick 150), no Grounded.
10. **Drench needs a flyer.** A hit on a ground unit, or on an already Grounded flyer, changes nothing and emits nothing.
11. **Landing.** Flyer at (5.5, 5.5) over a `Wall` tile, all neighbours free: grounded, it moves to (4.5, 4.5) (ring 1, lowest index).
12. **Walls block a Grounded flyer** (needs Fable's movement). 16x16 map, wall on every tile of column x = 8; flyer at (4.5, 8.5) ordered to (12.5, 8.5) and grounded at tick 0: X stays below 8 through tick 79 and a `MoveUnreachableEvent` is emitted. Runs with and without the status produce different state hashes; two identical runs produce equal hashes.

## 8. Open questions and Phase 2+ follow-ups

- Spoutling "stronger on flooded ground": proposal, a hit from a Spoutling on its own team's water counts 2 hits (new key `drench_hits_on_flooded`). Not in Phase 1.
- Gravity Spire (earth, Phase 4) and Downdraft (Skai Stormcaller) call `ApplyGrounded` with their own keys; their radii and cooldowns are their own specs.
- Does a Grounded flyer keep its move order through lift-off? Phase 1: yes.
- Status immunity or diminishing returns on repeated grounding: playtest first.

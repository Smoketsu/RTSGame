<!-- rts/docs/specs/README.md -->
<!-- Index of the Phase 1 implementation specs (plan step 3) and the cross-spec decisions they share: step order, state-hash order, the speed chain, the effective movement layer and the small edits each spec asks the coordinator to make in code it owns. Part of rts/docs; read with docs/PHASE1_PLAN.md and ADR-006/007. -->
# Phase 1 implementation specs

Written by the Opus designer ("plan heavy, build light"); Sonnet builders implement each one without further design help. Every spec has the same eight sections: purpose and design-doc source, data, state (with hash order), per-tick algorithm, commands and events, interfaces, acceptance tests, open questions. Numbers the design doc does not give are marked **placeholder** and live in data. The specs build on the wave A code (terrain grid, flow fields, combat, `ISpeedModifier`, `IDamageModifier`, `Unit.LayerOverride`, `Unit.StatusSpeedMultiplier`).

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [statuses.md](statuses.md) | Grounded, Slowed, drench counter; drives `Unit.EffectiveLayer` and `Unit.StatusSpeedMultiplier` | 5 | movement, combat (built) |
| [momentum.md](momentum.md) | Skai Momentum mechanic | 5 | statuses, `LastSelfMoveTick` |
| [flooding.md](flooding.md) | Water flooding, tides, waves | 8 | terrain (built), `WaterTeam` amendment |
| [fog_of_war.md](fog_of_war.md) | Per-team vision with the high-ground rule | 10 | terrain (built), statuses |
| [skirmish_ai.md](skirmish_ai.md) | Skirmish AI player controller | 11 | economy, HQ, vision |

Build statuses first: Momentum, flooding's speed rows and vision all read what it sets.

## Shared decisions (resolved across specs)

**Step order** (extends the current `Simulation.cs` header; the coordinator makes the edit): 1. clear events; 2. apply commands in (issuer, sequence) order; 3. **statuses** (`StatusSystem.BeginTick`: expiries, drench reset); 4. movement; 5. combat; 6. faction mechanics in ascending ordinal ID (`flooding` before `momentum`); 7. **vision** recompute; 8. state hash every interval; 9. advance the clock. A status applied by combat on tick t wipes Momentum on tick t and changes movement from tick t+1; the hash and the AI read end-of-tick vision.

**State-hash order** (current order plus vision): content digest, clock, RNG, ID allocator, players, units (each unit writes `... SpawnTick, HasLayerOverride, LayerOverride, StatusSpeedMultiplier, LastSelfMoveTick, CooldownTicksRemaining, AttackTarget, IsAttackTargetForced, IsAttackMoving, AttackMoveGoal, HqState ..., gather and production fields ...` and ends with `UnitStatusSet`; players also write resources and supply), terrain (each tile gains `WaterTeam` after `WaterDepth`), **vision** (per team: visible then explored bits), **resource fields** (remaining amount per field, between vision and mechanics), mechanics in ID order (flooding wave cooldowns, Momentum stacks), damage-modifier IDs, speed-modifier IDs (right after the damage-modifier IDs, both ascending Id), hand-over policy. AI memory is not simulation state; AI determinism is proven by harness hashes.

**Speed chain**: `MovementSystem.StepLength` (built) is the only place speed is computed: `((base * terrain) * status) * speedModifiers...`, left to right in Fix64 because truncating multiplication is not associative. Terrain = `TerrainGrid.SpeedMultiplier` (flooding owns the water rows and asks for a team parameter so own-team water does not slow own ground units); status = `Unit.StatusSpeedMultiplier`, the strongest active slow (statuses own it); modifiers = registered `ISpeedModifier`s in ascending ID (Momentum). Damage follows the built `CombatSystem` chain with `IDamageModifier` (Momentum). Flow-field costs use the terrain factor only.

**Effective movement layer**: the built `Unit.EffectiveLayer`; only `StatusSystem` sets or clears `LayerOverride`. Movement, combat targeting, vision (airborne sight only while Air) and the water speed rows use it; flooding's healing uses the definition layer.

**What counts as "moving"** for Momentum: `Unit.LastSelfMoveTick`, written only by `MovementSystem` when it actually moves a unit toward its `MoveTarget` (move, attack-move or combat chase). Wave pushes and landing relocation do not count, so Momentum never compares positions.

**What counts as "slowed"** for Momentum: only the Slowed status; water terrain slows do not wipe stacks (kept open for playtests in momentum.md 8).

**Water ownership**: `TerrainTile` gains `WaterTeam` (`TeamId`, `None` when dry) so the terrain speed table can tell enemy water from own water without naming the flooding mechanic. Water of different teams never mixes; allies share water as they share vision.

**Durations and rates**: seconds become ticks once at match start with `MatchClock.SecondsToTicks` (round to nearest); 0 ticks is a load error where a duration must be positive. Per-second rates become per-update amounts by the formula each spec gives, in the bracket order given.

## Requests to the coordinator (code it owns)

1. `Simulation.cs`: steps 3 and 7, `sim.Vision`, the hash order above.
2. `MovementSystem`: set `Unit.LastSelfMoveTick` when a unit moves under its own order; pass the owner's team to the terrain speed query.
3. ADR-006 amendment: `TerrainTile.WaterTeam` (hashed after `WaterDepth`), `SetWaterDepth` extended to `SetWater(sim, index, depth, team)`, and `SpeedMultiplier(index, layer, unitTeam)` treating own-team water as dry for ground units. Optional: bump `Version` only when depth class, blockers or height change.
4. `CombatSystem`: call `StatusSystem.ApplyDrenchHit` for `applies_drench` weapons; skip automatic targets the attacker's team cannot see (fog_of_war.md 6). Both changes get a sim-reviewer pass.
5. Decide whether flow-field costs should ever see own-team water as dry (flooding.md 8).

## Changelog

Builders do not edit `docs/CHANGELOG.md`; they hand their bullets to the coordinator.

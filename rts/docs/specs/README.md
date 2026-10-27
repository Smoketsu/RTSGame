<!-- rts/docs/specs/README.md -->
<!-- Index of the implementation specs (Phase 1 plan step 3 and Phase 2 plan step 3) and the cross-spec decisions they share: step order, state-hash order, the speed chain, the effective movement layer and the small edits each spec asks the coordinator to make in code it owns. Part of rts/docs; read with docs/PHASE1_PLAN.md, docs/PHASE2_PLAN.md and ADR-006/007. -->
# Implementation specs

Written by the Opus designer ("plan heavy, build light"); Sonnet builders implement each one without further design help. Every spec has the same sections: purpose and design-doc source, data, state (with hash order), per-tick algorithm, commands and events, interfaces, acceptance tests, open questions (the campaign spec adds the tutorial's data as its own section). Numbers the design doc does not give are marked **placeholder** and live in data.

## Phase 1 (wave B, built)

The specs build on the wave A code (terrain grid, flow fields, combat, `ISpeedModifier`, `IDamageModifier`, `Unit.LayerOverride`, `Unit.StatusSpeedMultiplier`).

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [statuses.md](statuses.md) | Grounded, Slowed, drench counter; drives `Unit.EffectiveLayer` and `Unit.StatusSpeedMultiplier` | 5 | movement, combat (built) |
| [momentum.md](momentum.md) | Skai Momentum mechanic | 5 | statuses, `LastSelfMoveTick` |
| [flooding.md](flooding.md) | Water flooding, tides, waves | 8 | terrain (built), `WaterTeam` amendment |
| [fog_of_war.md](fog_of_war.md) | Per-team vision with the high-ground rule | 10 | terrain (built), statuses |
| [skirmish_ai.md](skirmish_ai.md) | Skirmish AI player controller | 11 | economy, HQ, vision |

## Phase 2 (PHASE2_PLAN.md step 3)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [planetary_cycles.md](planetary_cycles.md) | Planetary Cycles (240 s, 60 s warning, Windstorm, Quake, Core surge), Core capture, holder's pick, no repeated play style, the 8-minute Core win | 4 | combat, HQ, teams, AI (built) |
| [balance_harness.md](balance_harness.md) | Batch harness: N games per matchup per map in parallel processes, win rate with interval, match length, HQ time-to-kill, hard limits, Markdown report, exit codes | 5 | AI (built), cycles (step 4) |
| [campaign_scripting.md](campaign_scripting.md) | Missions as data (objectives, conditions, triggers, scripted commands), loader, runner, host signals, headless MissionRunner, tutorial 1 | 8 | cycles' `pick_cycle` and optional cycle list (step 4) |

Build order for Phase 2: planetary_cycles first (it changes the step order, the hash order, `SimulationConfig` and the content digest that the other two rely on); then balance_harness and campaign_scripting in either order or in parallel (they touch different folders; both need the `CommandScript` / `SimulationConfig` edits from step 4).

## Shared decisions (resolved across specs)

**Step order** (the `Simulation.cs` header is authoritative; Phase 2 adds step 9, the coordinator makes the edit): 1. clear events; 2. apply commands in (issuer, sequence) order; 3. statuses (`StatusSystem.BeginTick`); 4. HQ timers (`HqSystem.Step`); 5. movement; 6. combat; 7. economy; 8. faction mechanics in ascending ordinal ID (`flooding` before `momentum`); 9. **cycles and Core** (`CycleSystem.Step`: out-of-match sides, presence, capture, hold accrual, Core win, lock, shift and quake paths, quake damage); 10. vision recompute; 11. state hash every interval; 12. advance the clock. A status applied by combat on tick t wipes Momentum on tick t and changes movement from tick t+1; a cycle that starts on tick S changes movement and combat from tick S+1; the hash and the AI read end-of-tick vision and Core state.

**State-hash order**: content digest (definitions, tuning texts except `harness.json` once the balance_harness request is done, the map including its quake paths, then the cycle definitions), clock, RNG, ID allocator, players, units (each unit writes `... SpawnTick, HasLayerOverride, LayerOverride, StatusSpeedMultiplier, LastSelfMoveTick, CooldownTicksRemaining, AttackTarget, IsAttackTargetForced, IsAttackMoving, AttackMoveGoal, HqState ..., gather and production fields ...` and ends with `UnitStatusSet`; players also write resources and supply), terrain (each tile writes `WaterTeam` after `WaterDepth`), vision (per team: visible then explored bits), resource fields (remaining amount per field), **cycles and Core** (`CycleSystem.WriteHash`, planetary_cycles.md section 3), mechanics in ID order (flooding wave cooldowns, Momentum stacks), damage-modifier IDs, speed-modifier IDs (both ascending Id; Phase 2 adds `cycle` to both lists), hand-over policy. AI memory and mission state are not simulation state; their determinism is proven by harness and mission-runner hashes.

**Speed chain**: `MovementSystem.StepLength` (built) is the only place speed is computed: `((base * terrain) * status) * speedModifiers...`, left to right in Fix64 because truncating multiplication is not associative. Terrain = `TerrainGrid.SpeedMultiplier` with the owner's team (own-team water does not slow own ground units); status = `Unit.StatusSpeedMultiplier`, the strongest active slow; modifiers = registered `ISpeedModifier`s in ascending ordinal ID (`cycle` for Windstorm, `economy.carrying`, `momentum`). Damage follows the built `CombatSystem` chain with `IDamageModifier`s in ascending ID (`cycle` for Core surge, then `momentum`). Weapon range has one hook: `CycleSystem.EffectiveRangeTiles` (Windstorm), used for both fire reach and acquire reach. Flow-field costs use the terrain factor only.

**Effective movement layer**: the built `Unit.EffectiveLayer`; only `StatusSystem` sets or clears `LayerOverride`. Movement, combat targeting, vision (airborne sight only while Air), the water speed rows and every cycle effect (Windstorm speed and range, quake relocation) use it; flooding's healing uses the definition layer.

**What counts as "moving"** for Momentum: `Unit.LastSelfMoveTick`, written only by `MovementSystem` when it moves a unit toward its `MoveTarget`. Wave pushes, landing relocation and quake-path relocation do not count.

**What counts as "slowed"** for Momentum: only the Slowed status; water terrain slows do not wipe stacks.

**Water ownership**: `TerrainTile.WaterTeam` (`TeamId`, `None` when dry). Water of different teams never mixes; allies share water as they share vision.

**Teams, not players, hold the Core**: presence, capture, holding and the Core win are per team; any Active player of the holding team may pick the next cycle; the Core win defeats every in-match player of the other teams with cause `core_held`.

**Durations and rates**: seconds become ticks once at match start with `MatchClock.SecondsToTicks` (round to nearest); 0 ticks is a load error where a duration must be positive. Per-second rates become per-update amounts by the formula each spec gives, in the bracket order given.

**Hosts and the command pipeline**: AI controllers, mission runners, the balance harness and test inputs all run between steps, read events and read-only state, and submit commands for the current tick or later. Each owns its own sequence counters and only issues as the slots it controls (the script issuer 255 for setup and mission scripts).

## Requests to the coordinator (code it owns)

Phase 1 (handled during Phase 1): `Simulation.cs` steps and vision; `MovementSystem.LastSelfMoveTick`; ADR-006 `WaterTeam`; `CombatSystem` drench and vision gating; flow fields and own-team water.

Phase 2 (from the specs above; each gets a sim-reviewer pass):
1. `Simulation`: `Cycles` field, `CycleSystem.RegisterModifiers` and `OnMatchStart` in `Start`, step 9, hash after resource fields, cycle definitions in the content digest; update `DeterminismTests.GoldenFinalHash` once (planetary_cycles.md 2).
2. `SimulationConfig`: optional cycle list (null or empty disables Planetary Cycles).
3. `CombatSystem`: `CycleSystem.EffectiveRangeTiles` at the two range reads.
4. `DataLoader`: cycle `effects`, map `quake_paths`, the two-play-style check; `MatchTuning`: `core_win_min_teams`, `quake_path_relocate_radius_tiles`, strict warning-shorter-than-interval check; `EconomyTuning`: `starting_worker_count`; `HarnessTuning`: the batch keys (balance_harness.md 2).
5. `Tuning.SourceDigest`: leave `harness.json` out (no simulation system reads it).
6. `CommandScript`: `pick_cycle` in the core kinds and the public `ParseCommandObject` entry point (campaign_scripting.md 2).
7. New `Rts.Sim.Data.StandardMatchStart` (balance_harness.md 2).
8. CLAUDE.md folder layout and `RtsGame.sln`: `rts/src/Campaign`, `rts/tools/MissionRunner`; CI: `balance-batch` job, tutorial double run, JSON validation of `rts/campaign`.

## Changelog

Builders do not edit `docs/CHANGELOG.md`; they hand their bullets to the coordinator.

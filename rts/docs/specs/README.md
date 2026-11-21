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

## Rule changes (P-003, 16 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [p003_rules.md](p003_rules.md) | Package A: uphill ranged penalty, armed workers with armed-first targeting, ranged hit delay (pending-hit queue), Shellback dome shield. Package B: unit separation with keep-right sidestep and crowd arrival | P-003 | combat, movement (built); build A before B |

## Skirmish AI v2 (16 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [skirmish_ai_v2.md](skirmish_ai_v2.md) | A: Perch and Pearl Depots, doubled common fields, expansion fields on all three maps, greybox 180-degree fix. B: AI expansion by depot and by HQ relocation, anti-freeze rules. C: production mix fix (failing test first), home rally, attack groups, Defend hysteresis, finishing low HQs. D: `--ai-check` exit criteria on the 18 review scenarios | after the AI trace review | skirmish AI, economy, HQ (built); build A, B, C, D in order |

## Skirmish AI v3 (P-004 stages 1 to 3, 17 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [skirmish_ai_v3.md](skirmish_ai_v3.md) | A: harness `--ladder` (profile A vs B, both seats, 65 percent of at least 200 decided games) and `--response` scenarios, Reef Runner water scout. B: scouting and the enemy model. C: fight predictor, counter-mix from unit data, tech on demand, static defence, rare priority. D: predictor-driven attacks and retreats, target choice and worker harass, measured defence, unit control. Versions coexist by profile switches (`aggressive_s1` to `_s3`); no `rts/src/Sim` change | P-004 | skirmish AI v2 packages A to C (built); build A, B, C, D in order |

## Skirmish AI v4 (P-005 part 2, the AI as a balance tool)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [skirmish_ai_v4.md](skirmish_ai_v4.md) | F: raid response (workers leave dangerous fields, defence holds at home when outmatched with the HQ counted, worker losses earn a raid tower), forgetting depleted fields and unreachable sites. S: play signature per game, distinct-game counts and intervals over distinct games, mirror and cross-faction rows, `dome_absorbed` stalemates in `--fights`. A1 to A5: `requires` hygiene and chains, waves, Downdraft, Tidecaller support, standing on water against flyers, blast aim, structure multiplier in the predictor, wall breach and the `wall_in` scenario, `--fights` cast control and ablation ladders. R+L in its own spec (next row). Profiles `aggressive_s4f`, `aggressive_s4`; no `rts/src/Sim` change | P-005 decisions 10, 11 | skirmish AI v3, combat mechanics, walls and tech (built); build F and S (either order), then A1 to A5 |
| [skirmish_ai_v4_rl.md](skirmish_ai_v4_rl.md) | R: a second seeded generator per AI (independent per seat), base army mix within a band, expansion clock and attack edge jitter, all from data and off in old profiles. L: named openings per faction plan (build order, mix, timings, handover), drawn by weight at match start, four per faction. H: `opening0`/`opening1` in game records, an Openings table and warnings in `ladder.md`. Gate: 80 percent distinct games per cell, not weaker than `aggressive_s3` in mirrors, no opening above 40 percent use or outside 35 to 65 percent in mirrors. Also how package F's options are re-measured. L-R: the two rush openings reworked into pressure openings that expand at once (data only; evidence in reports/ai_v4_rush). Profile `aggressive_s4r`; no `rts/src/Sim` change | P-005 option R and L | skirmish AI v4 package S (built); build R-1, L-1, then L-2 and H-1, then L-M, then L-R |

## Missing roster (18 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [missing_roster.md](missing_roster.md) | Thunderhead, Tidecaller and Leviathan unit files (placeholder stats from stated rules), AI pool guard for unarmed units, re-measure of WC-3; no `rts/src/Sim` change | after WC-3 | skirmish AI v3, worker construction (built); build R-1, R-2, R-3 in order |

## Combat mechanics (P-005 decisions 3, 6, 7, 18 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [combat_mechanics.md](combat_mechanics.md) | 1: area damage (`weapon.splash_radius_tiles`, flat, enemies only, blast at landing, dome-aware). 2: per-weapon `structure_damage_multiplier`. 3: `can_target_air_on_water` (hits flyers only while standing on a flooded tile). 4: Downdraft as a core `grounding_ability` with the `cast_grounding` command. All numbers are placeholders; no new step | P-005 | combat, statuses, flooding (built); build 1, 2, 3, 4 in order, sim-reviewer after each |

## Equal-value fights (P-005 option M, 18 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [micro_benchmark.md](micro_benchmark.md) | Harness `--fights`: equal-value army fights without strategic AI (attack-move, both seats, flat, high ground, flooded and choke arenas), value-exchange matrix, design-rule checks 1 to 6 with the 15 s Rooted HQ floor, the matchups file (rule 7) outside the content digest; no `rts/src/Sim` change | P-005 decision 9 (built first) | balance harness (built); build F0 to F4 in order |

## Shared decisions (resolved across specs)

**Step order** (the `Simulation.cs` header is authoritative; Phase 2 adds step 9, the coordinator makes the edit): 1. clear events; 2. apply commands in (issuer, sequence) order; 3. statuses (`StatusSystem.BeginTick`); 4. HQ timers (`HqSystem.Step`); 5. movement, then separation (`UnitSeparation`); 6. combat; 7. economy; 8. faction mechanics in ascending ordinal ID (`flooding` before `momentum`); 9. **cycles and Core** (`CycleSystem.Step`: out-of-match sides, presence, capture, hold accrual, Core win, lock, shift and quake paths, quake damage); 10. vision recompute; 11. state hash every interval; 12. advance the clock. A status applied by combat on tick t wipes Momentum on tick t and changes movement from tick t+1; a cycle that starts on tick S changes movement and combat from tick S+1; the hash and the AI read end-of-tick vision and Core state.

**State-hash order**: content digest (definitions, tuning texts except `harness.json` once the balance_harness request is done, the map including its quake paths, then the cycle definitions), clock, RNG, ID allocator, players, units (each unit writes `... SpawnTick, HasLayerOverride, LayerOverride, StatusSpeedMultiplier, LastSelfMoveTick, CooldownTicksRemaining, AttackTarget, IsAttackTargetForced, IsAttackMoving, AttackMoveGoal, HqState ..., gather and production fields ...` and ends with `ConstructionTicksRemaining, ShieldPool, ShieldDown, ShieldLastAbsorbTick, GroundingReadyTick, UnitStatusSet`; players also write resources and supply), **pending hits** (`PendingHitQueue.WriteHash`, P-003 package A: count, next sequence, then per entry in (land tick, sequence) order land tick, sequence, attacker, attacker owner, target, damage, applies-drench, source definition ID, splash damage, splash layers, blast fallback position), terrain (each tile writes `WaterTeam` after `WaterDepth`), vision (per team: visible then explored bits), resource fields (remaining amount per field), **cycles and Core** (`CycleSystem.WriteHash`, planetary_cycles.md section 3), mechanics in ID order (flooding wave cooldowns, Momentum stacks), damage-modifier IDs, speed-modifier IDs (both ascending Id; Phase 2 adds `cycle` to both lists), hand-over policy. AI memory and mission state are not simulation state; their determinism is proven by harness and mission-runner hashes.

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

## Rule changes (worker construction, 18 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [worker_construction.md](worker_construction.md) | A build order names a worker; it walks to an explored site, spends `worker_placement_seconds` there, then the structure appears and builds alone. Refunds on every cancellation, pending orders count in supply and the static defence cap. WC-1: Sim, scripts, Sim tests, re-pinned hashes. WC-2: AI, harness. WC-3: AI re-baseline (report only) | worker construction | economy, HQ, vision (built); build WC-1, WC-2 on one branch, then WC-3 |

## Walls and tech prerequisites (P-005 decisions 5 and 8, 18 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [walls_and_tech.md](walls_and_tech.md) | W: worker-built walls (`is_wall`, new `TileBlockers.BuiltWall`: blocks ground and water movement and flood flow, keeps water already there, hidden in fog for the AI, Quake damages it), Wind Baffle and Coral Wall. T: `requires` list on any definition (`missing_prerequisite` on train and build orders only), Storm Spire gates the Thunderhead, Reef Workshop the Leviathan, AI rule T2. G: gates (`is_gate`, `TileBlockers.Gate` with a hashed `GateTeam` per tile, per-team flow fields while any gate stands). D: drag to place (`build_wall_line` command with a tile list, per-worker hashed tile queue paid up front, `WallLine.Rasterise`, `BuildLineTileSkippedEvent`). F: flood-bound water flyer, option only, **not approved for build**. All numbers placeholders; no new step; W, T and G add no command | P-005; G and D developer request | worker construction, combat mechanics (built); build W, then T (built), then G, then D, sim-reviewer after each |

## Field saturation (developer decision, 18 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [field_saturation.md](field_saturation.md) | SAT: gather slots per field (`gather_slots_per_common_field` 3, `gather_slots_per_rare_field` 2), `GatherState.Waiting` with a hashed first-come queue tick; every hash moves. SAT-M: re-baseline. AI-E: worker caps, depots and rare cap from the slots (`aggressive_s5`). AI-P: producers at expansions, static defence by threat. AI-H: harass speed relative to the enemy's chasers. AI-HQ: relocation to the best field cluster | developer decision 18 Nov 2026 | economy (built); build SAT, SAT-M, then L-R and the AI packages in the order of its section 10 |

## Balance measurement (P-006 packages M and C first half, 21 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [balance_measurement.md](balance_measurement.md) | M: paired ladder games with an exact sign test, stated power (MDE) and the verdict NO_MEASURABLE_EFFECT, an opponent pool with forced openings (`AiProfile.WithForcedOpenings`, the only `rts/src/Ai` change), behaviour checks over per-game summaries, the standing `--balance` report. C (first half): `--predict-fights` scores `FightPredictor` against the `--fights` sweep, with a proposed fix-or-simulate rule. No `rts/src/Sim` change, no change to AI play | P-006 | balance harness, fights, opening library (built); build M1, M3, M4, M5 in order, C in parallel before M5 |

## Fight predictor v2 (P-006 package C second half, 21 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [fight_predictor_v2.md](fight_predictor_v2.md) | A deterministic lane simulation (`FightSim`, `rts/src/Ai`) behind the profile switch `predictor_model` (`formula` in every existing profile; candidate `aggressive_s5_fp2`, ablation `aggressive_s5`); held-out fight set (`heldout.json`: new army values, AI mixes, uneven sizes, damaged-army snapshots) with bars B1 to B7 and C1 stated before results; attack ledger and mirror-only paired ladder. No `rts/src/Sim` change, no number changes | P-006 | `--predict-fights`, `--ladder --paired` (built); FP2-1 and FP2-2 in parallel, then FP2-3, then FP2-4; sim-reviewer on `FightSim` |

## Budget queues (P-006 package B, 21 Nov 2026)

| Spec | System | Plan step | Depends on |
| --- | --- | --- | --- |
| [ai_budget_queues.md](ai_budget_queues.md) | Six AI spending queues (economy, expansion, production, army, tech, defence) with per-resource weights; accounts capped at the head item and released when a queue has nothing to buy; a defence spending ceiling; a rare-surplus boost of rare-cost units. Replaces rule R and the defence value-share check behind `use_budget_queues` (off in every existing profile; candidate `aggressive_s5_b`, ablation `abl_s5b_no_b`). Two harness checks (`workers_at`, `first_drop_off`); behaviour bars, then the stage-1 pool gate. No `rts/src/Sim` change, no number changes | P-006 | behaviour checks, `--balance`, `--pool` (built); build B0 and B1 in parallel, then B2; sim-reviewer on B1 |

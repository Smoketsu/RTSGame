<!-- rts/docs/CHANGELOG.md: project changelog in Keep a Changelog style; one entry per change, grouped by version or phase. -->
# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Unreleased changes go under the [Unreleased] heading, grouped as Added, Changed, Deprecated, Removed, Fixed or Security.

## [Unreleased] - Phase 1 Prototype (Skai versus water)

### Added

- `docs/PHASE1_PLAN.md` build order; ADR-006 (terrain as a tile grid of layered ground states) and ADR-007 (flow-field pathfinding per movement layer).
- Terrain grid (`rts/src/Sim/Terrain`): per-tile height layer 0 to 7, ramp flag, water depth, surface state (dry, raised, scorched) and blockers (wall, footprint); passability with the cliff and ramp rule and no corner cutting; speed multipliers by layer and depth class from `tuning/terrain.json` and `tuning/flood.json`; mutators only inside a step, each raising `TerrainChangedEvent` and bumping `Version`; every tile hashed.
- Map format: `height_rows`, `ramp_rows`, `resource_fields` (common or rare, amount) and `core`; `greybox_1v1` rebuilt with two layer-2 start plateaus, ramps, a layer-1 ring and a layer-0 basin around the Core (lowest layer 11% of the map).
- Flow-field pathfinding (`rts/src/Sim/Pathing`): integer-cost Dijkstra with a (cost, index) heap, flow directions in fixed neighbour order, cache keyed by (layer, goal) invalidated by terrain version with deterministic eviction. `MovementSystem` moves ground and water units along the field's direction vectors, air units straight; arrival within `arrival_radius_tiles`; `MoveUnreachableEvent` when the goal cannot be reached.
- `Unit.EffectiveLayer` (status override for Grounded flyers), `StatusSpeedMultiplier` and the `ISpeedModifier` hook (Momentum) registered at `OnMatchStart`; `SimulationConfig` now takes the `Tuning` and the `MapDefinition`; command scripts may name a `map`.
- Combat system (`rts/src/Sim/Combat`): runs after movement and before mechanics; target acquisition (enemies on a layer the weapon can hit, nearest with lowest-ID tie-break), cooldowns in ticks, damage through the `tuning/combat.json` damage-type by armour-type table, lethal hits resolved after all units act, `attack_move`, `attack_unit` and `stop` commands, `UnitDamagedEvent` and `UnitKilledEvent`, the `IDamageModifier` hook. Unit files gain optional `armour_type`, `sight_range_tiles` and `weapon`; Windrunner, Roc Rider, Spoutling and Grasper have placeholder weapons. `combat_smoke.json` script.
- Tests: 22 terrain, pathing, movement and combat tests (157 in total); golden final hash re-pinned.
- Implementation specs in `docs/specs/` (flooding, statuses, Momentum, fog of war, skirmish AI) with a shared-decisions index: step order, hash order, the speed chain, effective layer, what counts as moving and slowed, team-owned water. ADR-006 and ADR-007 amendments: `TerrainTile.WaterTeam`, a team-aware speed query (own-team water does not slow own ground units), team-agnostic flow-field costs; `Unit.LastSelfMoveTick`.
- The mobile HQ (`rts/src/Sim/Hq` and HQ unit data): Rooted, Uprooted and transition states with root and uproot timings from `hq.json`, rooting only near resource fields, movement refused while rooted, weapon and drop-off only while rooted, Last Stand behind its switch, and HQ death defeating the player through `PlayerTransitions`. Commands `root_hq` and `uproot_hq`.
- Economy (`rts/src/Sim/Economy` and `tuning/economy.json`): per-player common and rare resources, supply used and cap, live resource fields from map data that deplete, worker gathering round trips to drop-offs, `train_unit` with per-producer queues, `build_structure` with footprints, build time and the static-defence cap per base. Buildings and static defences for both factions in unit data; `economy_smoke.json` script.
- Shared statuses (`rts/src/Sim/Statuses`): Grounded, slows and the Spoutling drench counter per unit, `StatusSystem.BeginTick` as step 3; the only code that sets `Unit.LayerOverride` and `StatusSpeedMultiplier`. Weapons gain `applies_drench`; the Spoutling grounds a flyer after three hits. Command `apply_status`; five placeholder keys in `statuses.json`.
- Skai Momentum implemented in the plug-in: integer stacks gained while self-moving, drained after the still delay, wiped by Grounded or Slowed, feeding movement and damage through `ISpeedModifier` and `IDamageModifier`; stacks hashed in ascending unit ID.
- Tests: 207 in total; golden final hash re-pinned.
- Fog of war (`rts/src/Sim/Vision`, `sim.Vision`): per-team visible-now and explored-ever grids, integer sight circles, the high-ground rule with ramp eye height and allied spotters, airborne sight (`airborne_sight_range_tiles`, required for air units), vision recompute as the step before the state hash with both grids hashed per team; automatic target acquisition skips unseen enemies (explicit `attack_unit` still works); commands are not yet filtered by vision (Phase 2 anti-cheat plan).
- Water flooding (`rts/src/Sim/Factions/Water`): a shared tide on the match clock (`TideClock`, 210 s), flood sources from unit data (Tidepool, Grand Shell while Rooted) releasing integer drops redistributed downhill in ascending tile order, limits (enemy resource gap, dry footprints, walls), low-tide retreat, healing of water-layer units on own water, the first-pass `cast_wave` command with cooldown; 13 new `flood.json` keys; `flood_smoke.json` script.
- Phase 1 rosters: Talon Archer, Stormcaller and Windspire for Skai; Shellback, Ink Lobber and Shell Forge for water (placeholders, commented); the Roost also trains the Roc Rider; the Great Aerie is a ground-layer unit so a rooted nest can always be hit by ground weapons.
- Unity: fixed three-quarter camera controller, vertex-coloured terrain mesh updated from terrain events, click, box and control-group selection with move, attack, attack-move, stop, root, uproot and gather orders through `Simulation.Submit`; `economy_smoke.json` plays by default and the match keeps stepping after the script ends. Unverified in the editor.
- Terrain version now advances only on changes that affect passability or traversal cost (height, ramp, blockers, surface, depth class), so flood ticks within a depth class no longer invalidate every flow field.
- Tests: 269 in total; golden final hash re-pinned.

## [Unreleased] - Phase 0 Foundations

### Added

- Architecture decision records ADR-001 to ADR-005 (fixed-point, data format, entity IDs, tick rate, state hash).
- Engine-free simulation skeleton: Fix64/FixVec2, PCG32 RNG, entity/player/team IDs, FNV-1a state hasher, match clock at 20 ticks per second, command queue ordered by (tick, issuer, sequence), per-tick event log, data-defined units with straight-line movement, faction mechanic plug-in interface with Skai and water shells, Simulation tick loop.
- NUnit tests for Fix64, the RNG, the hasher, command ordering, the tick loop and the determinism exit test (40 tests).
- Tuning data files under rts/data/tuning with every value documented.
- Asset Store shortlist in docs/ASSET_STORE.md.
- GitHub Actions CI (`.github/workflows/ci.yml`) on every push and pull request with three jobs: `build-and-test` (Release build with warnings as errors, all NUnit tests, TRX artifact), `determinism` (headless smoke script run twice, fails if `STATE_HASH=` lines differ or are missing, then the DeterminismTests by filter) and `data-files` (JSON-with-comments validation).
- `.github/scripts/validate_jsonc.py` checks every data and script JSON file parses once comments are stripped and opens with a `//` header.
- `docs/CI.md`: badge, what each job checks, how to read a determinism failure, local commands.
- Repository documentation: root README.md, CLAUDE.md (architecture and determinism rules, documentation standard, model tiers), `.claude/agents` definitions for docs-writer (Haiku), builder (Sonnet) and sim-reviewer (Opus), README stubs for every folder reserved for later phases, docs index and data folder README.
- Player and team model for any player count: `Player` with a `PlayerState` state machine (Active, Surrendered, Disconnected, Defeated, Eliminated), `PlayerRegistry` alliance queries (`AreAllied`, `AreEnemies`, `AlliesOf`, `TeamOf`, `PlayersOnTeam`, `Teams`) in ascending ID order, `MatchOutcome` (standing teams, decided, winner, draw) that works for 1v1, 2v2, 1v1v1 and FFA.
- `SurrenderCommand` and `SetConnectionStateCommand`, `PlayerStateChangedEvent`; player states change only through `PlayerTransitions` inside `Simulation.Step`.
- `IHandOverPolicy` extension point with the Phase 0 `StateOnlyHandOverPolicy`, plus disconnect and pause counters tied to the `tuning/online.json` keys (Phase 2 pause and Phase 3 hand-over are TODOs).
- The state hash now covers each player's state, state-since tick and disconnect and pause counters; display names are not hashed. 17 team-model tests.
- Unity bridge: `rts/src/Sim` is a local Unity package `com.rtsgame.sim` (package.json, `Rts.Sim.asmdef` with No Engine References, `Rts.Sim.rsp` enabling nullable, package README). New Unity 6 project at `rts/src/Game` (manifest, ProjectVersion 6000.0.58f1, `Rts.Game` and editor asmdefs) with `SimulationHost`, which loads `rts/data`, runs a scripted match on a fixed accumulator (max 10 ticks per frame), draws interpolated placeholder cubes and logs `tick=N hash=...`. `AutoBootstrap` makes Play work in any scene; the menu item RTS > Create Bootstrap Scene saves `Assets/Scenes/Bootstrap.unity`.
- `docs/UNITY_SETUP.md`: setup steps, what to expect, the determinism check against the headless runner, limitations and troubleshooting. `.gitignore` gained Unity entries.
- Data loader in `rts/src/Sim/Data`: comment-aware `JsonReader`, `JsonValue`, `DataLoader`, `DataSet` and nine typed tuning classes. Numbers go to `Fix64` or `int` with no floating point; every error names the file and field; ranges are enforced at load.
- Phase 0 content: six example units (skai and water), three factions (skai, water, neutral), the grey-box 1v1 map and the three Planetary Cycles, every number a documented placeholder.
- Headless runner `rts/tools/BalanceHarness` plays a JSON command script through the real data and prints per-interval state hashes plus `STATE_HASH=`; `scripts/smoke.json` and a README describing the Phase 2 balance harness.
- 51 new NUnit tests for the JSON reader, loader validation errors, the shipped design-doc values and an end-to-end load, spawn and move through `Simulation` (121 tests in total).
- Command scripts are shared: `CommandScript` (`rts/src/Sim/Data/CommandScript.cs`) parses them inside the simulation assembly, and both the Unity `SimulationHost` and the headless harness play `rts/data/scripts/smoke.json`, so the Unity-versus-headless hash comparison in `docs/UNITY_SETUP.md` is meaningful. `DataLoader` skips the `scripts/` folder; other unknown folders are still rejected.
- `SimulationHost` takes players, seed and commands from the script (Inspector fields `scriptRelativePath` and `matchSeed`, -1 meaning the script's seed), submits every command before the first tick and stops at the script's tick count with a final-hash log line.
- The harness `--script` option defaults to `<data dir>/scripts/smoke.json`; 8 new `CommandScriptTests` (129 tests in total).

### Changed (after the Opus simulation review)

- Commands that share an (issuer, sequence) key on one tick are now all rejected as `duplicate_sequence`, so the outcome never depends on arrival order (the old tie-break was not a total order).
- The simulation RNG is locked outside `Simulation.Step` (`DeterministicRandom.IsLocked`); `MatchClock.Advance`, `EntityIdAllocator.Allocate`, `CommandQueue.Enqueue`/`TakeForTick`, `MovementSystem.Step` and the RNG reseed/restore methods are internal, and `ContentCatalog` freezes at `Start`, so nothing can mutate state outside a step.
- Move and spawn positions must lie within ±16384 tiles (`Simulation.WorldBoundTiles`, rejected as `out_of_bounds`), and `FixVec2.MoveTowards` pre-scales far offsets so squared distances can never saturate.
- `SpawnUnitCommand` is accepted only from the script issuer (`PlayerId.None`); a player issuer is rejected as `spawn_requires_script_issuer`. Its constructor rejects an empty definition ID.
- The state hash now also covers a digest of every unit and faction definition (`Simulation.ContentDigest`) and the hand-over policy (`IHandOverPolicy.Id` and `WriteHash`), so peers with different data or policies diverge at tick 0.
- `DeterministicRandom.NextIntInclusive` is unbiased for every span; tick-rate and hash-interval bounds are shared constants on `MatchSettings`; the neutral faction ID is `FactionDefinition.NeutralId`; `DataSet` copies maps and cycles into ordinal-ordered dictionaries; maps may list 2 to 16 spawns.
- ADR-001 wording corrected to match the code (multiply and divide truncate toward zero; digit-by-digit square root).
- New tests: pinned golden final hash for the determinism script, duplicate rejection independent of submission order, RNG lock, content freeze, per-field hash coverage, far-target movement, bounds rejection (135 tests).
- Documentation pass: summaries on every `Kind` override, mechanic `Id` and comparison operator, unit and range comments on 109 locals, "Range:" on 28 numeric field summaries; the JSON reader's byte-order-mark check uses the `\uFEFF` escape instead of an invisible literal.

### Known follow-ups

- Faction-specific tuning classes (Momentum, Flood, Earth, Spoutling, harness matchup limits) live in the shared `Tuning` type. Before Phase 1 mechanics read tuning, each mechanic plug-in should declare and parse its own tuning file so adding a faction never edits the core loader.
- The pending command queue is input, not state, and is not hashed; replay snapshots (Phase 2) must serialise it.

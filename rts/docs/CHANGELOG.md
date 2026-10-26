<!-- rts/docs/CHANGELOG.md: project changelog in Keep a Changelog style; one entry per change, grouped by version or phase. -->
# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Unreleased changes go under the [Unreleased] heading, grouped as Added, Changed, Deprecated, Removed, Fixed or Security.

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

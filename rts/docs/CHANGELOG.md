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

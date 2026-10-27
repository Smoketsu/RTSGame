<!-- CLAUDE.md: standing instructions for Claude Code in this repository: architecture and determinism rules, documentation standard, layout, model tiers, build and test, and the subagents in .claude/agents. -->
# CLAUDE.md

Standing instructions for Claude Code sessions in this repository. The game design is in the Game Design Proposal (26 Oct 2026). The architecture decisions are in `rts/docs/decisions/`. Before changing simulation code, read the header of `rts/src/Sim/Simulation.cs`.

## Architecture rules (non-negotiable)

1. The simulation is an engine-free C# library (`rts/src/Sim`, assembly `Rts.Sim`). It targets `netstandard2.1` so it compiles both inside Unity and in a plain .NET test project. Its Unity assembly definition has "No Engine References" turned on. Unity only renders, plays audio and runs the UI.
2. Everything is a command. Player input, AI, network peers, replays and campaign scripts all produce the same command objects, applied on a set tick (`Simulation.Submit`, applied inside `Simulation.Step`).
3. Data-driven. Units, buildings, factions, maps, cycles and every tuning number live in data files under `rts/data`, never in code.
4. Faction mechanics are plug-ins behind one shared interface (`IFactionMechanic`), registered by ID. The core never names a faction; `Factions/BuiltInMechanics.cs` is the only composition root.
5. Players and teams: any player count from day one. Nothing assumes 1v1.
6. One match clock on the simulation tick (`MatchClock`) drives tides, cycles, warnings and Core capture.
7. Fog of war is computed in the simulation (Phase 1), not the renderer.
8. Events out, never in. Listeners read what the simulation reports (`EventLog`) and never change its state. Mutation methods throw outside a step.
9. Headless by default. The simulation must run a full match without Unity (`rts/tools/BalanceHarness`).
10. Asset Store packages are presentation only. No Unity physics or ECS in game logic.

## Determinism rules (simulation code only)

- No floating point: no `float`, `double` or `decimal`; use `Fix64` (Q32.32, ADR-001) and integers. Parse decimals with `Fix64.Parse`.
- One seeded random number generator owned by the simulation (`DeterministicRandom`, PCG32), drawn only inside a step. Never `System.Random`.
- Never read the system clock: no `DateTime`, `Stopwatch` or `Environment.TickCount`.
- Iterate collections in sorted-ID order (`SortedDictionary`, `SortedSet`, sorted lists), never `Dictionary` or `HashSet` order.
- No threads, file or network access, or engine calls inside `rts/src/Sim`.
- Hash the full state at regular ticks (`StateHasher`, ADR-005): everything that affects future ticks is written to the hash in a fixed order; anything purely presentational is not.
- Step order is fixed and documented in the header of `rts/src/Sim/Simulation.cs`: clear events, apply commands in (issuer, sequence) order, movement, faction mechanics in ascending ID order, state hash at each hash interval, advance the clock.
- Simulation and network code always gets a determinism review by an Opus or Fable reviewer before merge (the `sim-reviewer` subagent is the Opus review).

## Documentation standard

- Every file opens with a header comment: the path on line 1, then what the file does and where it fits. Use `// path` for C# and JSON, `<!-- path -->` for Markdown and `# path` for YAML and shell. Markdown agent files in `.claude/agents` keep their YAML front matter first, so the path comment follows it.
- Every class, method, field, property, constant and local variable has a comment saying what it holds, its unit (ticks, tiles, seconds, fixed-point, fraction) and its valid range. Members use `/// <summary>`; locals use `//`.
- Every tuning value in a data file has a comment line above it: `// <what it controls>. Unit: <unit>. Range: <range>. Source: <design-doc section>.`
- A change with an undocumented variable is not done.

## Folder layout

```
rts/src/Sim/       engine-free simulation (Rts.Sim)
rts/src/Game/      Unity 6 project
rts/src/Ai/        skirmish AI
rts/src/Net/       lockstep networking over Steam
rts/src/Replay/    replays
rts/tools/         BalanceHarness, ReplayCheck
rts/data/          tuning, units, factions, maps, cycles (JSON)
rts/campaign/      campaign scripting
rts/tests/         NUnit tests (Sim.Tests)
rts/docs/          ADRs, CHANGELOG.md, ASSET_STORE.md, UNITY_SETUP.md, CI.md
```

## Model tiers

- Fable: architecture, the simulation skeleton, determinism, and final review of simulation code.
- Opus: designing the team and its subagents, and reviewing subagent output in `rts/src/Sim` before merge.
- Sonnet: project setup, the data loader, the headless runner, Unity bridge scripts and CI.
- Haiku: tuning data, file headers, README stubs, changelog entries and documentation passes.
- Plan heavy, build light: the top tier writes the plan, lower tiers build it.
- If a subagent fails the same task twice, move the task up one tier and attach the failing test.
- Never use Fable or Opus for chores such as headers, stubs, tuning comments or changelog entries.

## Changelog and review

- Every change updates `rts/docs/CHANGELOG.md`, under the `[Unreleased]` heading.
- Subagents do not edit the changelog while others are working; they give their changelog bullets to the coordinator in their report.

## Build and test

- Requires the .NET 8 SDK. Commands, run from the repository root:

```
dotnet build rts/RtsGame.sln
dotnet test rts/RtsGame.sln
dotnet run --project rts/tools/BalanceHarness -- --data rts/data
```

- `rts/build/` is the output folder (set in `rts/Directory.Build.props`). It is git-ignored; never commit it.
- Warnings are errors (`TreatWarningsAsErrors`), and the code uses C# 9 with nullable reference types on. Fix every warning rather than suppressing it.
- The harness reads its command script from `rts/data/scripts/smoke.json` unless `--script <path>` is given; the Unity `SimulationHost` plays the same file (parsed by `Rts.Sim.Data.CommandScript`), so their state hashes are comparable. If the script file is missing the run fails with a file error.

## Subagents (`.claude/agents`)

- `docs-writer` (Haiku): file headers, READMEs, changelog entries and tuning comments. Never changes logic.
- `builder` (Sonnet): implements a written spec with tests, runs the test suite. Must not edit `rts/src/Sim` without a `sim-reviewer` pass afterwards.
- `sim-reviewer` (Opus): reviews `rts/src/Sim` and `rts/src/Net` changes for determinism. Reports findings with file and line; does not rewrite code unless asked.

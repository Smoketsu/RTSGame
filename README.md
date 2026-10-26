<!-- README.md: repository overview for the RTS game: what it is, folder layout, how to build, test and open it, and where the design records live. -->
# RTS

A competitive 3D real-time strategy game built in Unity 6. Three asymmetric factions at launch, Skai Riders, water and earth, with fire to follow later. Each player controls one mobile HQ that can be rooted in place and uprooted again. Matches are shaped by Planetary Cycles, timed events on the match clock, and by the Core, which players capture. Early access, planned for late summer 2027, offers Skai Riders against water.

## Folder layout

All code and data sit under `rts/`:

```
rts/src/Sim/     engine-free simulation (Rts.Sim): no engine references, no floating point
rts/src/Game/    Unity 6 project: rendering, input, UI, bridge to the simulation
rts/src/Ai/      skirmish AI; produces commands like any player
rts/src/Net/     lockstep networking over Steam
rts/src/Replay/  versioned replays built from recorded command streams
rts/tools/       headless tools (BalanceHarness, ReplayCheck)
rts/data/        game data: JSON with // comments
rts/campaign/    campaign scripting and missions
rts/tests/       NUnit tests (Sim.Tests)
rts/docs/        ADRs, changelog, Asset Store shortlist, setup notes
```

## Build and test

You need the .NET 8 SDK.

```
dotnet build rts/RtsGame.sln
dotnet test rts/RtsGame.sln
dotnet run --project rts/tools/BalanceHarness -- --data rts/data
```

Build output goes to `rts/build/`, which git ignores.

## Open in Unity

The project targets Unity 6. Setup steps are in [rts/docs/UNITY_SETUP.md](rts/docs/UNITY_SETUP.md).

## Documentation

- [Architecture decision records](rts/docs/decisions/README.md): [ADR-001](rts/docs/decisions/ADR-001-fixed-point.md) fixed-point maths, [ADR-002](rts/docs/decisions/ADR-002-data-format.md) data format, [ADR-003](rts/docs/decisions/ADR-003-entity-ids.md) entity IDs, [ADR-004](rts/docs/decisions/ADR-004-tick-rate.md) tick rate, [ADR-005](rts/docs/decisions/ADR-005-state-hash.md) state hash.
- [Documentation index](rts/docs/README.md) and [changelog](rts/docs/CHANGELOG.md).
- [CLAUDE.md](CLAUDE.md): architecture and determinism rules, documentation standard, model tiers and subagents for Claude Code sessions.

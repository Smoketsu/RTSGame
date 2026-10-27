<!-- rts/docs/PHASE2_PLAN.md: the Phase 2 (Vertical slice) build order, who builds what, what needs the developer's machine, and the exit test. -->
# Phase 2 plan: Vertical slice

Goal (development plan): two players on different machines finish an online 1v1 with no desync, outside playtesters can learn the game from the tutorial, the balance harness passes its hard limits, and the Skai sensitivity read is done. Everything that can be built and tested headless is built first; the Steam transport, the HUD and the art pipeline need the Unity editor and the Steamworks SDK on the developer's machine.

## Build order and ownership

| Step | What | Depends on | Tier | Headless here? |
| --- | --- | --- | --- | --- |
| 1 | Lockstep protocol (`rts/src/Net`): transport-agnostic peer model, input delay, command relay, per-tick hash exchange, desync detection, disconnect pause (5 min once per player), pause budget (120 s), reconnect by replaying the command log; a loopback transport for tests (ADR-008) | Phase 1 | Fable | yes |
| 2 | Versioned replays (`rts/src/Replay`): record seed, map, data digests and the command stream; binary format with a version number; `tools/ReplayCheck` re-simulates and compares hashes (ADR-009) | Phase 1 | Sonnet | yes |
| 3 | Specs with acceptance tests for Planetary Cycles and Core capture, the full-scale balance harness and the campaign scripting layer (`rts/docs/specs/`) | design doc | Opus designs | yes |
| 4 | Planetary Cycles (every 240 s, 60 s warning, three cycles with effects from data) and Core capture (20 s uncontested, holder picks the next cycle, no repeat of the same play style twice, FFA 8-minute win) | 3 | Sonnet builds | yes |
| 5 | Balance harness at full scale: N games per matchup per map in parallel processes, win rate, match length, HQ time-to-kill, the hard limits (HQ floor 20 s vs 30 supply; water vs earth median once earth exists), a Markdown report | 3, AI | Sonnet builds | yes (smaller N locally) |
| 6 | Map checklist test: lowest layer at most 35%, two ground routes between bases, Skai cliff spots in range of ground fire, rare resource on the flanks and outside the Core | Phase 1 terrain | Sonnet | yes |
| 7 | Two more 1v1 maps (three in total) with the checklist passing | 6 | Haiku data, Sonnet fixes | yes |
| 8 | Campaign scripting layer (`rts/campaign`): missions as data with triggers, objectives and scripted commands; the first tutorial mission (camera, selection, movement, gathering, building) | 3 | Sonnet builds | yes (headless run), UI on the machine |
| 9 | Unity: shared timeline bar for tide and cycles, HUD, menus, lobby, surrender; Steam transport adapter over the lockstep protocol | 1, 4 | Sonnet; Steam needs the developer | no |
| 10 | Unity compile and tests in GitHub Actions through GameCI (needs a `UNITY_LICENSE` secret from the developer) | developer | Sonnet | CI only |
| 11 | Review of every simulation, net and replay change (sim-reviewer, then Fable) | all | Opus, Fable | yes |

Art pipeline for one faction at final quality and the Skai sensitivity read are the developer's own work items for this phase.

## Exit test (headless part)

- Two simulated peers on the loopback transport play `ai_vs_ai.json` through the lockstep protocol to a decided outcome with every exchanged hash matching; an injected divergence is reported as a desync at the right tick; a disconnect pauses and resumes; a reconnecting peer catches up from the command log.
- A recorded replay of that match re-simulates to the same final hash through `ReplayCheck`.
- The balance harness runs a reduced batch (for example 20 games per matchup on each map) in CI and the full 500 locally, and the report shows every hard limit.
- Every map passes the checklist test; the tutorial mission runs headless to its last objective.

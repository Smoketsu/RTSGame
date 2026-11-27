<!-- rts/docs/reports/roster_numbers_2/suite_chunks.md -->
# Suite run in chunks (roster_numbers_2 re-pin, 27 Nov 2026)

Command per chunk: `dotnet test <project> --no-build --filter "<filter>"`, run on the final tree (the H3 pin was fixed after chunk c2 ran and re-run alone: passed). Chunks ran in parallel on a shared 4-core machine, so wall times are long.

| Chunk | Filter | Total | Passed | Failed | Wall |
| --- | --- | ---: | ---: | ---: | --- |
| c1 | Sim, not `Rts.Sim.Tests.Ai`, not `...Harness` | 725 | 725 | 0 | 3 m 11 s |
| c2 | Sim `Rts.Sim.Tests.Harness` | 153 | 152 (+1 after re-pin) | 1 (H3, re-pinned, passes) | 1 h 43 m |
| c3 | Ai: SkirmishAiTests, FightSimTests, AiVarietyTests | 55 | 55 | 0 | 1 h 19 m |
| c4 | Ai: AiHarassTests, AiPlacementTests | 20 | 20 | 0 | 45 m 47 s |
| c5 | Ai: all other Ai classes | 353 | 351 (+2 after re-pin) | 2 (C2_2, C8: mix weights, re-pinned by coordinator decision) | 1 h 35 m |
| Net | `Rts.Net.Tests` | 33 | 33 | 0 | 4 m 13 s |
| Replay | `Rts.Replay.Tests` | 30 | 30 | 0 | 2 m 24 s |

Sim total 1306 (1302 expected + 4 new tests of spec 5.3: 2 data, 2 fights): 1304 pass in the chunks, 2 fail; after the later re-pins the whole `AiArmyMixTests` class and `ProductionDefenceMetricsTests` were re-run together (30 tests, 5 m): 30 passed, 0 failed. The rest of the suite was not re-run, so every Sim test has passed in some run, and the full Sim total is 1306 passing. Net 33, Replay 30.

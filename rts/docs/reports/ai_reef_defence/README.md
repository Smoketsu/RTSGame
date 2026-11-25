<!-- rts/docs/reports/ai_reef_defence/README.md -->
<!-- Report of package RR-B of rts/docs/specs/ai_reef_runner_defence.md: the measurement of the candidate profile aggressive_s5_rr against aggressive_s5 on the spec's base seed, every bar and guard with its numbers, the reported medians, the traces and the suite totals. Part of rts/docs/reports. -->
# Report: Reef Runner defence candidate (RR-B)

Measured on branch `review/ai-reef-defence-build` (Release build; RR-M and RR-1 included). Every bar is as written in the spec, run once, nothing tuned, no second seed.

## Runs (as the spec 5.1 states, `--paired-faction water`, no `--seed`, so the ladder base seed 20261117)

```
BalanceHarness --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_rr --maps prototype_1v1 --matchups skai_vs_water --games 144 --paired-faction water --jobs 4 --out main
BalanceHarness --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_rr --maps prototype_1v1 --matchups skai_vs_skai,water_vs_water --games 48 --paired-faction water --jobs 4 --out mirrors
BalanceHarness --response --profile aggressive_s5      > response_s5.txt
BalanceHarness --response --profile aggressive_s5_rr   > response_rr.txt
BalanceHarness --trace --paired --profile-a aggressive_s5 --profile-b aggressive_s5_rr --map prototype_1v1 --seed 20261117 --max-game-seconds 600 --matchup <m> --game <g> --out traces/<m>_game<g>.txt
```

Resume support (tool-side, `RTS_CHUNK_GAMES=1` and `RTS_WORKER_CACHE=<folder>`, documented in the harness README) was used so a container restart loses little: each game was played by its own worker call and its record kept in `cache_main/` and `cache_mirrors/`. The games are independent and seeded per pair, so the records are those of an ordinary run. The determinism spot check (in-process re-run of the first two games, summaries included) read 0 mismatches in both runs.

Main run header line: `pairing kind: skai:full,water:seed` (`main_stderr.txt` line 145; `main/behaviour.md` line 5). As the spec requires.

## Bars (Water point of view, exact two-sided sign test at 0.05)

| ID | Check | D (decisive pairs) | Better / worse / ties | p | Verdict | Spec pass condition |
| --- | --- | --- | --- | --- | --- | --- |
| RR-B1 Reef Runners fielded | `trained_unit` water.reef_runner (higher) | 72 | 72 / 0 / 0 | 0.0000 | BETTER | BETTER: met |
| RR-B2 Fields defended | `worker_losses_pre_overrun` (lower) | 65 | 40 / 25 / 7 | 0.0817 | NO_MEASURABLE_EFFECT | BETTER: **not met** |
| RR-B3 Army kept | `army_supply_ratio_at` 300 s (higher) | 71 | 46 / 25 / 1 | 0.0170 | BETTER | not WORSE: met |
| RR-B4 Water wins against Skai | `won` (higher) | 0 | 0 / 0 / 72 | 1.0000 | NO_MEASURABLE_EFFECT | not WORSE: met |

RR-B4 wins: **candidate Water 0 of 72, baseline Water 0 of 72** (`stats_main.md`). Skai won every pair; the expectation of at least 69 of 72 was met (72 of 72).

For RR-B2 the stated power at D 66 was 0.65 / 0.90 / 0.99 for a true better-share of 0.65 / 0.70 / 0.75. The measured share is 40 of 65 = 0.615, below the 0.636 that D 66 needs. The designer's trial read 25 of 33 (0.76); this seed does not reproduce it.

| ID | Run | Result | Verdict |
| --- | --- | --- | --- |
| RR-G1 Skai mirror unchanged | mirrors | 24 of 24 `skai_vs_skai` pairs with the same `state_hash` and `play_signature` in both games (`stats_mirrors.md`) | holds |
| RR-G2 Water mirror | mirrors | `won` over the 24 Water-mirror pairs: 19 better / 0 worse / 5 ties, p 0.0000, BETTER (candidate slot won 43 of 48 games, baseline slot 5). The spec's power note stands: 24 pairs see only a large regression. | holds (not WORSE) |
| RR-G3 Response suite | response | `aggressive_s5` 5 of 10, `aggressive_s5_rr` 6 of 10. Every scenario passing for `aggressive_s5` passes for `aggressive_s5_rr`. The only change of verdict is `response_turtle_water` FAIL (26 supply lost) to PASS (8). Identical to the designer's run R. Per the spec 5.6 this is one deterministic game with a changed AI and not a fix of the regression's cause. Full lines in `response_s5.txt` and `response_rr.txt`. | holds |
| RR-G4 Clean commands | traces | No sample line of any of the four traces lists a rejection reason (every `rejected=` field is `-`), so no candidate trace has a reason or a larger count than its baseline trace (`traces/summary.md`). | holds |
| RR-G5 Determinism and suite | main | Spot check 0 mismatches (main and mirrors): holds. Suite: **not green**, see below. No pin moved (no pin test failed). | spot check holds; **suite part fails on one test not touched here** |

### Suite (RR-G5), Release, run in chunks (`suite_chunks.md`)

| Chunk | Passed / total |
| --- | --- |
| Sim, Ai (4 big classes) | 138 / 138 |
| Sim, Ai (rest) | 277 / 278 (1 failed) |
| Sim, Harness | 147 / 148 (1 failed, re-pinned and re-run, see below) |
| Sim, Economy and Combat | 237 / 237 |
| Sim, Data, Cycles, Campaign, Factions, Statuses, Terrain, Units, Vision | 373 / 373 |
| Sim, the rest | 113 / 113 |
| Net | 33 / 33 |
| Replay | 30 / 30 |

Summed: Sim 1287 run (1285 passed first time), Net 33, Replay 30, 1350 in all. `dotnet test --list-tests` for Sim lists 1289 names: the 2 extra are `AiHarassMeasure.PrintRaids` and `AiPlacementMeasure.PrintPlacements`, classes marked `[Explicit("measurement, not a test")]`, which a normal run does not run. 1287 + 2 = 1289, so no test was missed.

Two failures:

1. `EconomyMetricsTests.Command_PrintsIncomeByAndOpeningLines`: its count of `BEHAVIOUR` lines (18) was a re-pin RR-M missed (four new checks, two lines each: 26). This is the spec 4.3 item 8 kind of change, so it was re-pinned (18 to 26, with the comment) and the test re-run alone: passed.
2. `AiFightTests.DProfiles_SwitchesAndValidation` (`rts/tests/Sim.Tests/Ai/AiFightTests.cs` line 1068): the test walks every shipped profile and requires the package D switches off in all but a list of named s5 profiles; `aggressive_s5_rr` is a copy of `aggressive_s5` and has them on, and it is not in the list. The spec (RR-1 acceptance test 3) says that a walking test that fails is reported and not loosened, so **it was not changed**. Adding `id == "aggressive_s5_rr"` to the list, as every earlier s5-derived profile was added, is a one-token test change for the coordinator to approve. With it the suite should be green.

## Verdict (spec 5.2)

- Bars: RR-B1 BETTER, RR-B2 not significant, RR-B3 not WORSE (BETTER), RR-B4 not WORSE, nothing WORSE, RR-G1 to RR-G4 hold.
- That is **NO_MEASURABLE_EFFECT**: the AI fields Reef Runners (25.5 per Water game against 3.0 in the median, in all 72 pairs), but no effect on field defence was seen at this sample size (40 better, 25 worse, p 0.0817).
- Condition: RR-G5 requires a green suite. As the tree stands, one test (item 2 above) fails because the new profile is missing from a test's list. Read literally that makes RR-G5 fail and the verdict FAIL; with the list entry (a test-only change, no data or code) the verdict is NO_MEASURABLE_EFFECT. The coordinator decides. `aggressive_s5_rr` stays a candidate in either case, nothing is retuned.

## Reported, not judged (`stats_main.md`, `traces/summary.md`)

Medians over the 72 Water slot-games of the main run (baseline / candidate):

| Measure | aggressive_s5 | aggressive_s5_rr |
| --- | --- | --- |
| Reef Runners trained | 3.0 | 25.5 |
| Worker losses before the overrun | 6.0 | 5.0 |
| Worker losses in the 600 s window | 35.5 | 34.0 |
| Overrun second | 323 | 331 (71 of 72 games had one; baseline 72 of 72) |
| Army supply at 180 s, own / Skai | 12 / 12 | 11 / 12 |
| Army supply at 300 s, own / Skai | 20 / 33 | 26 / 33 |
| Army supply at 450 s, own / Skai | 7.5 / 69.5 | 13 / 68 |
| Game end tick | 10257.5 | 10625.5 |

- Water opening draws: identical in all 72 pairs (`water_standard` 20, `water_grasper_pressure` 20, `water_fast_expand` 15, `water_leviathan_tech` 17). The unit mix per opening is in `stats_main.md`.
- Traces (`traces/`): in the candidate-Water game the second Reef Runner (the first beyond the scout) is trained by the t=120 s sample, in the baseline-Water game by t=210 s; the army at 180 s is `water.reef_runner:2, water.shellback:1` (candidate) against `water.grasper:1, water.shellback:2` (baseline).
- Other paired lines of the main run, for information (no bar): `income_by` BETTER (51 / 20), `first_drop_off` BETTER (14 / 0), `harass_trade` BETTER (36 / 16), `harass_squads` BETTER (64 / 1), `depot_losses_young` no effect (17 / 14). The run's exit code is 2 because the existing check `hq_move_new_income` fails for both profiles alike (candidate 61 violating moves of 72, baseline 58 of 73); it is not a bar of this spec.

## Files

`main/`, `mirrors/` (`behaviour.jsonl`, `behaviour.md`), `main_stdout.txt`, `mirrors_stdout.txt` and the `*_stderr.txt` files, `response_s5.txt`, `response_rr.txt`, `traces/`, `stats_main.md`, `stats_mirrors.md`, `suite_chunks.md`, and the resume caches `cache_main/` and `cache_mirrors/` (one worker output per game).

<!-- rts/docs/reports/ai_kite/README.md -->
<!-- Report of package K-B of rts/docs/specs/ai_kite_correction.md: the measurement of the candidate profile aggressive_s5_kite against aggressive_s5 on base seed 20261201, every bar and guard with its numbers, the reported statistics, the traces, the suite totals and the verdict. Part of rts/docs/reports (the spec names the folder ai_kite_correction; the coordinator's task named ai_kite, which is used). -->
# Report: kite correction candidate (K-B)

Measured on branch `review/ai-kite-build` (Release build of the K-1 and K-H commits). Bars, thresholds, seed (20261201, run once), factor (0.4) and key are as the spec fixed them; nothing was tuned after a result.

**Verdict: FAIL**, because guard K-G3 does not hold: `response_turtle_water` passes for `aggressive_s5` and fails for `aggressive_s5_kite`. Every other bar and guard is as the spec requires (K-B1 is not significant, which on its own would have given NO_MEASURABLE_EFFECT). `aggressive_s5_kite` stays a candidate; nothing is retuned.

## Runs (spec 5.1, `RTS_CHUNK_GAMES=1`, `RTS_WORKER_CACHE=<folder>/cache_main` and `cache_mirrors`, Release, `--no-build`, `--jobs 4`)

```
--behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_kite --maps prototype_1v1 --matchups skai_vs_water --games 144 --seed 20261201 --paired-faction water --jobs 4 --out main
--behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_kite --maps prototype_1v1 --matchups skai_vs_skai,water_vs_water --games 48 --seed 20261201 --paired-faction water --jobs 4 --out mirrors
--response --profile aggressive_s5 > response_s5.txt          (and aggressive_s5_kite > response_kite.txt)
--predict-fights --from rts/docs/reports/fights_2026-11-18/fights.jsonl --profile aggressive_s5 --out predict_off
--predict-fights --from ... --profile aggressive_s5 --predict-kite-factor 0.4 --out predict_kite
--trace --paired --profile-a aggressive_s5 --profile-b aggressive_s5_kite --map prototype_1v1 --seed 20261201 --max-game-seconds 600 --matchup <m> --game <g> --out traces/<m>_game<g>.txt   (skai_vs_water and water_vs_water, games 0 and 1)
```

Main run header: `pairing kind: skai:full,water:full` (`main/behaviour.md` line 6, `main_stderr.txt`), as the spec requires. Exit codes of the behaviour runs were 2: the existing check `hq_move_new_income` fails for both profiles alike (known; not a bar). The response runs exit 2 because scenarios fail (see K-G3), as in RR-B. The determinism spot check (2 games re-run in-process) read 0 mismatches in both runs.

## Bars (Water point of view, exact two-sided sign test at 0.05, 72 cross pairs)

| ID | Check | D (decisive pairs) | Better / worse / ties | p | Verdict | Spec pass condition |
| --- | --- | --- | --- | --- | --- | --- |
| K-B1 Water wins against Skai | `won` (higher) | 4 | 3 / 1 / 68 | 0.6250 | NO_MEASURABLE_EFFECT | BETTER: **not met** |
| K-B2 Reef Runners fielded | `trained_unit` water.reef_runner (higher) | 72 | 71 / 1 / 0 | 0.0000 | BETTER | BETTER: met |
| K-B3 Fields | `worker_losses_pre_overrun` (lower) | 32 | 18 / 14 / 40 | 0.5966 | NO_MEASURABLE_EFFECT | not WORSE: met |
| K-B4 Army kept | `army_supply_ratio_at` 300 s (higher) | 43 | 21 / 22 / 29 | 1.0000 | NO_MEASURABLE_EFFECT | not WORSE: met |

D is better plus worse. K-B1: Water won 3 of 72 games with the candidate and 1 of 72 with the baseline (3 candidate-only, 1 baseline-only). The spec's stated detectable effect was about 8 of 72 (5.3), so the result is below it.

## Guards

| ID | Run | Result | Holds |
| --- | --- | --- | --- |
| K-G1 Skai mirror identical | mirrors | 24 of 24 `skai_vs_skai` pairs have the same state hash and play signature (`stats_mirrors.md`) | holds |
| K-G2 Water mirror | mirrors | `won`: 20 better / 0 worse / 4 ties, p 0.0000, BETTER (`mirrors/behaviour.md` line 68) | holds (not WORSE) |
| K-G3 Response suite | response | `aggressive_s5` 6 of 10, `aggressive_s5_kite` 5 of 10. **`response_turtle_water` passes for `aggressive_s5` (supply lost before first kill 8) and fails for `aggressive_s5_kite` (26).** Other differences: `response_worker_raid_water` p2 FAIL to PASS (workers lost 5 to 3), `response_mass_pummeler_water` still FAIL both (share_gain -0.063 to -0.047), `response_mass_windrunner_water` trade ratio 1.477 to 1.435. Full lines in `response_s5.txt` and `response_kite.txt`. | **fails** |
| K-G4 Clean commands | traces | Every sample line of all four traces has `rejected=-` (the only other `rejected=` text is the format line in the header), so no candidate trace has a rejection reason or a larger count than its baseline trace | holds |
| K-G5 Determinism, suite, pins | main, suite | Spot check 0 mismatches (main and mirrors). Suite green on the measured commit (table below). No pin moved: the pin tests are in the suite and none failed | holds |
| K-G6 Predictor unchanged on un-kited fights | predict | All 550 `predict_kite` lines with `"kite_pair":false` equal the `predict_off` line of the same fight with the field removed (checked by script over `predict.jsonl`) | holds |

K-G3 note: this is one deterministic scenario game with a changed AI, as the RR-B report said of the same scenario for `aggressive_s5_rr` (there it went the other way: FAIL with 26 lost for `aggressive_s5`, PASS with 8 for `aggressive_s5_rr`, on the earlier build and seed). The scenario is sensitive to small changes of the AI. It is reported as measured and not argued away.

K-H accuracy (`predict_off/predict.md`, `predict_kite/predict.md`): `PREDICT ... accuracy=0.735 correct=562 decided=765` off, `accuracy=0.610 correct=467 decided=765` with factor 0.4 (the designer's P1 and P2 numbers, reproduced exactly). Split by `kite_pair` with the term on: true 230 fights, 221 decided, 90 correct (0.407); false 550 fights, 544 decided, 377 correct (0.693). The recorded fights were played with plain attack-move, so a fall on kite-pair fights is the intended belief and is not barred. Deviation from the spec text: the spec counted 570 records without a kite pair and 210 changed records; the flag's definition (spec 4.1) marks 230 records as kite pairs, of which 210 change and 20 do not (a kite pair whose damage term does not alter the prediction). The 550 un-kited records are all byte-identical, which is what G6 asks.

## Verdict (spec 5.2)

- K-B2 BETTER (71 of 72), K-B1 not significant (3 / 1, p 0.625), K-B3 and K-B4 not WORSE, nothing WORSE.
- K-G1, K-G2, K-G4, K-G5, K-G6 hold. **K-G3 fails.**
- The spec: FAIL is "K-B2 not BETTER, or anything WORSE, or a guard fails". A guard fails, so the verdict is **FAIL**. Had K-G3 held, it would have been NO_MEASURABLE_EFFECT.
- `aggressive_s5_kite` stays a candidate. A new factor, or the fight-decision extension (Q-4), would be a new revision with its bars restated. No `aggressive_s5` pin moves.

## Reported, not judged (`stats_main.md`; Water slot-games, medians, baseline then candidate)

| Measure | aggressive_s5 | aggressive_s5_kite |
| --- | --- | --- |
| Pummeler (`water.grasper`) trained | 28.0 | 17.5 |
| Reef Runner trained | 24.0 | 40.0 |
| Pummeler share of trained army units | 0.400 | 0.241 |
| Reef Runner share of trained army units | 0.336 | 0.508 |
| Worker losses before the overrun | 5.0 | 4.0 |
| Army supply at 180 / 300 / 450 s, own | 10 / 23 / 9 | 10 / 23 / 10.5 |
| Army supply at 180 / 300 / 450 s, Skai | 13 / 33 / 68 | 13 / 33 / 65 |
| Overrun second (72 of 72 games have one) | 313.5 | 320.0 |
| Game end tick | 10321 | 11191.5 |

- Candidate-Water wins 3 of 72, baseline-Water wins 1 of 72.
- The candidate trained fewer Pummelers in 64 of 72 pairs and still trained at least one in 66 of 72 games.
- The candidate game ended later in 45 pairs, earlier in 27.
- The Water opening draws were identical in all 72 pairs.
- Unit mix per Water opening (median trained, baseline / candidate) is in `stats_main.md`. The openings stay distinct, and the Pummeler is still fielded in every opening (`water_leviathan_tech` drops from 20.5 to 2.0 median Pummelers; the other three drop by a fifth to a third).
- Traces (army at 180 and 300 s, `traces/`): in `skai_vs_water` pair 0 the two Water armies are identical (180 s: grasper 6, reef_runner 3; 300 s: grasper 9, reef_runner 8), which matches the finding that the term has not changed a choice by 300 s. In `water_vs_water` pair 0 the candidate Water holds grasper 8, reef_runner 8, shellback 3, spoutling 1 at 300 s against the baseline's grasper 13, reef_runner 4, shellback 1 (game 0), and grasper 11, reef_runner 7, shellback 2 against grasper 7, reef_runner 2, shellback 2, spoutling 2 (game 1, candidate is P1).
- Expected results of 5.5 against measured: K-B2 BETTER as expected; K-B1 NO_MEASURABLE_EFFECT as expected (3 wins); K-B3 no effect as expected; K-B4 not WORSE as expected; Pummeler count about half (17.5 against 28.0, the spec expected 16 against 29); games longer as expected; guards expected to hold, but K-G3 did not.

## Suite (K-G5), Debug build of the measured commit, run in chunks (`suite_chunks.md`)

| Chunk | Passed / total |
| --- | --- |
| Sim, Ai (five groups: ai_a, ai_b1, ai_b2, ai_b3a, ai_b3b) | 162 + 96 + 67 + 34 + 69 = 428 / 428 |
| Sim, everything outside Ai and Harness | 723 / 723 |
| Sim, Harness (four groups) | 47 + 58 + 29 + 17 = 151 / 151 |
| Net | 33 / 33 |
| Replay | 30 / 30 |

Sim in all: 1302 = 1287 before this work + 12 (`AiKiteCorrectionTests`) + 3 (`PredictKiteTests`). Net 33, Replay 30. No failure. The first two attempts to run the larger groups as one chunk were stopped by the tool's two-hour limit, so those groups were split further (the lines of the stopped attempts were never written).

## Deviations from the spec

- Acceptance test K1_PerTargetFactor (spec 3.3 item 8) names Reef Runners as the second, un-kited enemy entry. A Reef Runner also kites a Pummeler (range 4 >= 1 + 2, speed 4.1 >= 0.8 x 3), so both entries are kited and the test premise is false. The test uses a second Pummeler entry (melee, never kites) instead.
- Acceptance test K1_WaterGameDiffers (item 12) names a 300 s cap. At 300 s pair 0's two Water games are identical (no kiting Skai unit has been sighted by then), so the hash assertion cannot hold. The test uses 600 s, where the hashes differ; the trained counts at 600 s are in its comment.
- Report folder is `ai_kite` (the task), not `ai_kite_correction` (the spec).
- Kite-pair counts in the K-H accuracy section differ from the spec's prose (see above).

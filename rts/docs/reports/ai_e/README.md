<!-- rts/docs/reports/ai_e/README.md -->
# AI-E: the AI economy follows the saturation rule

Package AI-E of `rts/docs/specs/field_saturation.md`: worker caps from gather slots plus walk cover, the rare cap yielding to saturation, depots at 80 percent saturation, up to 4 live depots, a threat blocks a depot only from 4 supply. Branch `review/ai-e`, built on `origin/main` at 0fc7fa5. `prototype_1v1`, `-c Release`, `--jobs 4`, base seed 20261117, 72 games per cell. Baseline: `aggressive_s4r`; `abl_s5_no_e` (aggressive_s5 with only AI-E at its old values) plays it bit-identically (same state hash `e289ebedbf0ddc26` for water_vs_water game 2 over the whole game, and the 180 s hash test E-7), so the ladder baseline is `aggressive_s4r` as the spec's section 9 says. No threshold, weight, scenario or bar was changed after seeing results. `rts/build` is git-ignored; the numbers below were copied out of it as each run finished.

## After the review fixes

Review of the first build found a flip-flop (finding 1): with `rare_cap_yields_to_saturation` on, a common worker's death closed the rare field, rule G4 force-drained the rare workers onto a full common field, the common field then ran over its cap, the rare cap reopened and rebalancing sent a worker back. Fixed in `AiBrain.Rebalance`: with the yielding cap on, a closed rare field's workers move only to a served field below its cap; they are no longer forced onto a full common field, and new workers are kept off the closed field as before. Old profiles (switch off) keep the forced move, so they play as before (`abl_s5_no_e` against `aggressive_s4r` was re-checked, see below). The rest of the review items are tests, comments and the parser tests (listed in the commit). The first numbers are kept unchanged below under "First build"; this section has the numbers after the fixes.

**Flip count (finding 1).** Reproduced in a targeted test, ruled out in a whole game:
- Test `E8_KillingACommonWorkerDoesNotFlipRareWorkers` (saturated `aggressive_s5` economy on greybox_1v1, rare bank 5000, both common fields at their cap, two rare workers, one common worker removed, 10 thinks): before the fix it failed with a rare worker going common then back to rare (field-kind trails `CR` and `C`: 1 flip, 2 rare-to-common moves); after the fix it passes (0 flips, at most 1 rare-to-common move, the worker that fills the gap).
- Test `E9_MirrorRun_CountsRareCommonFlips` (two `aggressive_s5` Skai AIs, 600 s, prototype_1v1, seed 1234; counts a worker whose field kind changes and changes back within 3 thinks): 0 flips and 0 kind changes of any kind before the fix, and 0 and 0 after. So the loop needs a worker death with the other common fields exactly full, and does not occur in that game; the ladder numbers move only through other games' details (below).

### Run 1 again: strength ladder (`aggressive_s5` against `aggressive_s4r`, `--non-regression`), 216 of 216 decided, 2867.4 s

Same command as run 1 below with `--out rts/build/ladder_e2_vs_s4r`.

```
WARNING opening water/water_grasper_pressure: wins 12 of 18 mirror games against other openings (0.667, above 0.65).
WARNING opening water/water_standard: wins 10 of 32 mirror games against other openings (0.313, below 0.35).
LADDER candidate=aggressive_s5 baseline=aggressive_s4r decided=216 wins=167 fraction=0.773 distinct=216 mirror=130/144 cross=37/72 bar=0.450 PASS
```

Determinism spot check 2 games, 0 mismatches. Beside the first build (first build in brackets):

| cell | decided | distinct | distinct / decided | candidate wins / decided | fraction | Wilson |
| --- | ---: | ---: | ---: | --- | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 67/72 (67/72) | 0.931 | [84.8, 97.0] |
| skai_vs_water | 72 | 72 | 1.000 | 37/72 (37/72) | 0.514 | [40.1, 62.6] |
| water_vs_water | 72 | 72 | 1.000 | 63/72 (60/72) | 0.875 | [77.9, 93.3] |

| row | decided | candidate wins (first build) | fraction | Wilson |
| --- | ---: | --- | ---: | --- |
| mirror | 144 | 130 (127) | 0.903 | [84.3, 94.1] |
| cross | 72 | 37 (37) | 0.514 | [40.1, 62.6] |
| cross, candidate as skai / water | 36 / 36 | 36 / 1 (36 / 1) | 1.000 / 0.028 | [90.4, 100.0] / [0.5, 14.2] |
| all | 216 | 167 (164) | 0.773 | [71.3, 82.4] |
| slot 0 / slot 1 | 108 / 108 | 83 / 84 | 0.769 / 0.778 | [68.1, 83.8] / [69.1, 84.6] |

Median game length: all 620.1 s (first build 619.3 s; min 350.5, max 1162.8); skai_vs_skai 633.0 s; skai_vs_water 480.2 s; water_vs_water 680.5 s. Undecided 0, draws 0.

Openings of the candidate (win fraction in mirror games, first build in brackets; draws unchanged 57, 48, 76, 35, 52, 42, 62, 60): skai_air 9/22 = 0.409 (0.409), skai_fast_expand 13/20 = 0.650 (0.650), skai_standard 11/28 = 0.393 (0.393), skai_windrunner_pressure 12/20 = 0.600 (0.600), water_fast_expand 16/25 = 0.640 (0.600), water_grasper_pressure 12/18 = **0.667** (0.500), water_leviathan_tech 15/31 = 0.484 (0.613), water_standard 10/32 = **0.313** (0.313).

### Run 3 again: traces after the fixes

The same 12 traces (games 0 to 3 of each matchup, `aggressive_s5` in both seats); the `aggressive_s4r` figures are the ones of the first build's run (unchanged code). Per-trace lines in `trace_summary_after_fixes.txt`. First build in brackets.

| sample | workers | waiting | completed depots | common income per 30 s |
| --- | ---: | ---: | ---: | ---: |
| 180 s | 17.0 (17.0) | 0.5, max 2 (0.5, 2) | 2.0 (2.0) | 740 (740) |
| 300 s | 25.0 (25.0) | 1.0, max 3 (1.0, 3) | 3.0 (3.0) | 990 (990) |
| 480 s | 24.5 (24.0) | 1.0, max 5 (1.5, 5) | 4.0 (4.0) | 910 (905) |

`aggressive_s4r` for comparison (unchanged): 14.0 workers, 1.0 depots and 667.5 income at 180 s; 14.0, 1.0 and 662.5 at 300 s; 17.0, 2.0 and 885 at 480 s. First depot placed median 90 s, complete 120 s, second complete 180 s (as before; the traces differ from the first build only at the 480 s sample, in 8 of 24 player-lines, mostly by a few units of income; the 180 s and 300 s samples are identical).

<!-- AFTER_END -->

## First build (before the review fixes)

## Run 1: strength ladder (`aggressive_s5` candidate against `aggressive_s4r`, `--non-regression`), 216 of 216 decided, 2990.4 s

```
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s4r --profile-b aggressive_s5 --non-regression --jobs 4 --out rts/build/ladder_e_vs_s4r
```

```
WARNING opening water/water_standard: wins 10 of 32 mirror games against other openings (0.313, below 0.35).
LADDER candidate=aggressive_s5 baseline=aggressive_s4r decided=216 wins=164 fraction=0.759 distinct=216 mirror=127/144 cross=37/72 bar=0.450 PASS
```

The determinism spot check re-ran 2 games, 0 mismatches. Distinct games per cell, computed from the play signatures of decided games in `ladder.jsonl` (the `ladder.md` "distinct fraction" column is a win share, not used):

| cell | decided | distinct | distinct / decided | candidate wins / decided | fraction | Wilson |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 67/72 | 0.931 | [84.8, 97.0] |
| skai_vs_water | 72 | 72 | 1.000 | 37/72 | 0.514 | [40.1, 62.6] |
| water_vs_water | 72 | 72 | 1.000 | 60/72 | 0.833 | [73.1, 90.2] |

Undecided 0, draws 0; slot 0 wins 108, slot 1 wins 108. Median game length (20 ticks per second): all 619.3 s (min 350.5, max 1156.5); skai_vs_skai 641.2 s; skai_vs_water 479.2 s; water_vs_water 693.8 s. No game reached the 2400 s cap (the SAT-M self-ladder of `aggressive_s4r` had a median of 736.4 s).

| row | decided | candidate wins | fraction | Wilson |
| --- | ---: | ---: | ---: | --- |
| mirror | 144 | 127 | 0.882 | [81.9, 92.5] |
| cross | 72 | 37 | 0.514 | [40.1, 62.6] |
| cross, candidate as skai | 36 | 36 | 1.000 | [90.4, 100.0] |
| cross, candidate as water | 36 | 1 | 0.028 | [0.5, 14.2] |
| all | 216 | 164 | 0.759 | [69.8, 81.1] |
| candidate in slot 0 / slot 1 | 108 / 108 | 82 / 82 | 0.759 / 0.759 | [67.1, 83.0] each |

Openings of the candidate (draws = times drawn; mirror = decided games against other openings of the same faction; the draws equal the SAT-M ones because the opening draw does not depend on the economy):

| faction | opening | draws | use share | mirror games | wins | win fraction | Wilson | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 57 | 0.264 | 22 | 9 | 0.409 | [23.3, 61.3] | 19 | 19 |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 13 | 0.650 | [43.3, 81.9] | 16 | 16 |
| skai | skai_standard | 76 | 0.352 | 28 | 11 | 0.393 | [23.6, 57.6] | 24 | 23 |
| skai | skai_windrunner_pressure | 35 | 0.162 | 20 | 12 | 0.600 | [38.7, 78.1] | 13 | 13 |
| water | water_fast_expand | 52 | 0.241 | 25 | 15 | 0.600 | [40.7, 76.6] | 15 | 0 |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 9 | 0.500 | [29.0, 71.0] | 20 | 1 |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 19 | 0.613 | [43.8, 76.3] | 17 | 0 |
| water | water_standard | 60 | 0.278 | 32 | 10 | 0.313 | [18.0, 48.6] | 20 | 0 |

## Run 3: traces (workers, waiting, depots, income) beside SAT-M

Twelve traces per profile: games 0 to 3 of each matchup, seed 20261117, the profile in both seats, `--max-game-seconds 2400`. Command per profile, matchup and game: `--trace --map prototype_1v1 --matchup <m> --game g --seed 20261117 --max-game-seconds 2400 --profile-a <p> --profile-b <p>`. `aggressive_s4r` was re-run here, because its SAT-M traces were not kept apart from the table; its 180 s and 300 s values are the SAT-M ones (14 workers, 0 waiting, 1 depot for nearly every player). Cells are workers / waiting / completed depots / common income in the 30 s before the sample; `-` means the game was over. Full per-trace lines and the summaries are in `trace_summary.txt` in this folder.

Medians over the player-samples (24 at 180 s and 300 s, 20 and 16 at 480 s because short games had ended):

| sample | profile | workers | waiting | completed depots | common income per 30 s |
| --- | --- | ---: | ---: | ---: | ---: |
| 180 s | aggressive_s5 | 17.0 (16 to 18) | 0.5 (max 2) | 2.0 (1 to 2) | 740 |
| 180 s | aggressive_s4r | 14.0 (11 to 14) | 0.0 (max 0) | 1.0 | 667.5 |
| 300 s | aggressive_s5 | 25.0 (12 to 26) | 1.0 (max 3) | 3.0 (1 to 4) | 990 |
| 300 s | aggressive_s4r | 14.0 (6 to 15) | 0.0 (max 2) | 1.0 (0 to 2) | 662.5 |
| 480 s | aggressive_s5 | 24.0 (0 to 34) | 1.5 (max 5) | 4.0 (0 to 4) | 905 |
| 480 s | aggressive_s4r | 17.0 (13 to 19) | 1.0 (max 3) | 2.0 | 885 |

First depot (placed or complete), median over 24 players: s5 90 s placed, 120 s complete; s4r 60 s placed, 90 s complete (the s4r figure is the 30 s sample at which it first shows, so it is coarse). Second depot complete: s5 180 s (24 of 24 players); s4r 330 s (20 of 24 players had one).

## Run 2: self-ladder (`aggressive_s5` against itself), 216 of 216 decided, 3552.6 s

```
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s5 --profile-b aggressive_s5 --jobs 4 --out rts/build/ladder_e_self
```

```
WARNING opening skai/skai_standard: wins 19 of 28 mirror games against other openings (0.679, above 0.65).
WARNING opening water/water_fast_expand: wins 8 of 25 mirror games against other openings (0.320, below 0.35).
LADDER candidate=aggressive_s5 baseline=aggressive_s5 decided=216 wins=111 fraction=0.514 distinct=216 mirror=75/144 cross=36/72 bar=0.650 FAIL
```

(The FAIL and exit code are meaningless in a self-ladder; the gate items are variety and openings.) SAT-M run 1 for `aggressive_s4r`: `fraction=0.523`, mirror 75/144, cross 38/72. This run's trace of game 0 (skai_vs_skai, `result slot1 end_tick 13865`) is the same game as the ladder's record.

| cell | decided | distinct | distinct / decided | SAT-M (s4r) | candidate wins / decided | Wilson |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 1.000 | 41/72 (0.569) | [45.4, 67.7] |
| skai_vs_water | 72 | 72 | 1.000 | 1.000 | 36/72 (0.500) | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 1.000 | 34/72 (0.472) | [36.1, 58.6] |

Undecided 0, draws 0; slot 0 wins 117, slot 1 wins 99. Median game length: all 563.2 s (min 388.1, max 1069.1); skai_vs_skai 542.6 s; skai_vs_water 512.0 s; water_vs_water 674.9 s (SAT-M `aggressive_s4r`: 736.4 s overall, 799.7, 447.7 and 1027.1 s). No game reached the cap.

| row | decided | candidate wins | fraction | Wilson | SAT-M (s4r) |
| --- | ---: | ---: | ---: | --- | --- |
| mirror | 144 | 75 | 0.521 | [44.0, 60.1] | 75/144 = 0.521 |
| cross | 72 | 36 | 0.500 | [38.7, 61.3] | 38/72 = 0.528 |
| cross, candidate as skai | 36 | 36 | 1.000 | [90.4, 100.0] | 35/36 |
| cross, candidate as water | 36 | 0 | 0.000 | [0.0, 9.6] | 3/36 |
| all | 216 | 111 | 0.514 | [44.8, 58.0] | 113/216 = 0.523 |
| seat 0 / seat 1 | 108 / 108 | 60 / 51 | 0.556 / 0.472 | [46.2, 64.6] / [38.1, 56.6] | 52 / 61 |

Openings (draws are identical to SAT-M: 57, 48, 76, 35, 52, 42, 62, 60):

| faction | opening | draws | use share | mirror games | wins | win fraction | Wilson | SAT-M fraction | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| skai | skai_air | 57 | 0.264 | 22 | 2 | **0.091** | [2.5, 27.8] | 0.591 | 19 | 19 |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 15 | 0.750 | [53.1, 88.8] | 0.500 | 16 | 16 |
| skai | skai_standard | 76 | 0.352 | 28 | 19 | **0.679** | [49.3, 82.1] | 0.393 | 24 | 24 |
| skai | skai_windrunner_pressure | 35 | 0.162 | 20 | 9 | 0.450 | [25.8, 65.8] | 0.550 | 13 | 13 |
| water | water_fast_expand | 52 | 0.241 | 25 | 8 | **0.320** | [17.2, 51.6] | 0.480 | 15 | 0 |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 10 | 0.556 | [33.7, 75.4] | 0.167 | 20 | 0 |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 18 | 0.581 | [40.8, 73.6] | 0.742 | 17 | 0 |
| water | water_standard | 60 | 0.278 | 32 | 17 | 0.531 | [36.4, 69.1] | 0.469 | 20 | 0 |

Skai fast_expand is at 0.750 (the band is 0.35 to 0.65; the 0.75 is outside too, so four of eight openings are outside: skai_air 0.091, skai_fast_expand 0.750, skai_standard 0.679, water_fast_expand 0.320; SAT-M had two of eight outside). Openings are report only in the spec's gate.

## Run 4: response scenarios (`--response --profile aggressive_s5`, report only)

```
RESPONSE response_roc_rush_skai.json air_answer PASS p1=PASS air_first_seen_s=163.3 model_air_known_s=94.0 p2=PASS aa_units=16 p3=PASS hq_alive=true
RESPONSE response_roc_rush_water.json air_answer PASS p1=PASS air_first_seen_s=167.9 model_air_known_s=98.0 p2=PASS aa_units=4 p3=PASS hq_alive=true
RESPONSE response_mass_pummeler_skai.json armoured_answer FAIL p1=FAIL share_gain=-0.050 share=0.412 base_share=0.462 armour=armoured p2=PASS hq_alive=true
RESPONSE response_mass_pummeler_water.json armoured_answer FAIL p1=FAIL share_gain=-0.062 share=0.246 base_share=0.308 armour=armoured p2=PASS hq_alive=true
RESPONSE response_mass_windrunner_skai.json trade PASS p1=PASS trade_ratio=1.560 killed=39 lost=25 hq_alive=true
RESPONSE response_mass_windrunner_water.json trade PASS p1=PASS trade_ratio=1.529 killed=78 lost=51 hq_alive=true
RESPONSE response_worker_raid_skai.json raid FAIL p1=FAIL static_defence_near_raided_base=false p2=FAIL max_workers_lost_in_wave=6
RESPONSE response_worker_raid_water.json raid FAIL p1=FAIL static_defence_near_raided_base=false p2=PASS max_workers_lost_in_wave=2
RESPONSE response_turtle_skai.json turtle FAIL p1=FAIL supply_lost_before_first_kill=26 first_structure_kill_s=515.4 p2=PASS won_by_s=538.8
RESPONSE response_turtle_water.json turtle PASS p1=PASS supply_lost_before_first_kill=8 first_structure_kill_s=414.1 p2=PASS won_by_s=444.3
RESPONSE_SUMMARY passed=5 of=10
```

`aggressive_s4r` (SAT-M run 3): `passed=5 of=10`, the same five pass (both roc rushes, both mass-Windrunner trades, water turtle). Not fewer. Within the failing scenarios the worker-raid Water line moved from p1 PASS (static defence near the raided base) to p1 FAIL and p2 from FAIL (7 workers lost) to PASS (2 lost); the Skai raid lost 6 workers (before 7).

## Acceptance (spec section 9, AI-E row, and the section 9 bars)

| item | bar | result | verdict |
| --- | --- | --- | --- |
| ladder, candidate `aggressive_s5` against `aggressive_s4r` (`--non-regression`) | fraction >= 0.45 | 0.759 (164/216) | PASS |
| mirror-row Wilson upper bound | >= 0.50 | 0.925 (127/144 = 0.882) | PASS |
| variety, every self-ladder cell | distinct / decided >= 0.8 | 1.000, 1.000, 1.000 | PASS |
| variety, strength-ladder cells | >= 0.8 | 1.000, 1.000, 1.000 | PASS |
| opening use share | none above 0.40 | 0.162 to 0.352 | PASS |
| response scenarios | not fewer passes than the baseline (5 of 10) | 5 of 10, same five | PASS |
| median common income at 300 s not below `aggressive_s4r`'s | 662.5 per 30 s | 990 per 30 s (24 player-samples each) | PASS |
| opening win fractions 0.35 to 0.65 in mirrors (report only) | all in band | 4 of 8 outside (skai_air 0.091, skai_fast_expand 0.750, skai_standard 0.679, water_fast_expand 0.320); strength ladder: 1 outside (water_standard 0.313) | report only; worse than SAT-M (2 of 8) |
| L-R-A2 pressure openings, self-ladder (report only) | 0.35 to 0.65 with at least 10 mirror games | skai_windrunner_pressure 0.450 (20 games), water_grasper_pressure 0.556 (18 games) | in band (SAT-M: water_grasper_pressure 0.167 failed) |

Section 9 "fails when": ladder or variety bar missed (no), fewer response passes (no), median income at 300 s below the baseline's (no). The AI-E package passes; no E switch was changed.

Reading the run (nothing tuned): the new economy is much stronger against `aggressive_s4r` in the mirror (127 of 144) and level in the cross-faction cells, where the faction split (Skai wins every cross game as candidate, Water as candidate loses all but one) is unchanged in kind. The trace table shows why: 17 workers and 2 depots at 180 s against 14 and 1, 25 workers and 3 depots at 300 s against 14 and 1, and 990 against 662.5 common income per 30 s at 300 s. The first depot is placed later than `aggressive_s4r` places it (median 90 s against 60 s seen in the 30 s samples) because the larger worker target keeps the bank low early; its second depot is complete by 180 s. The self-ladder games are shorter (median 563 s against 736 s). Queues stay small: median waiting 0.5, 1.0 and 1.5 workers at 180, 300 and 480 s (largest 5).

## Not checked

- No ladder against `abl_s5_no_e` itself: it plays bit-identically to `aggressive_s4r` (full-game state hash equal on one game, 180 s hash test), so it would duplicate run 1.
- The traces cover games 0 to 3 of each matchup (12 per profile), not every ladder game; the `aggressive_s4r` traces were re-run here.
- The response scenario lines are one run each; the pass set for `aggressive_s4r` is quoted from SAT-M, not re-run.
- `rts/build` outputs (`ladder.jsonl`, `ladder.md`, traces) are not committed apart from `trace_summary.txt`; the numbers above are their content.
- The test-first order was not followed for the AI-E tests (written together with the code; the old-value paths were checked by E-7 and the unchanged pins).

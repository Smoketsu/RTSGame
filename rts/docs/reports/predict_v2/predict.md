<!-- predict.md: written by the balance harness --predict-fights (rts/tools/BalanceHarness/PredictRunner.cs); not a source file. -->
# Fight predictor against measured fights

- Predictor: FightPredictor.Predict with the predictor_* numbers of profile `aggressive_s5_fp2`; AI thresholds of that profile: attack_min_advantage 1.3, retreat_max_advantage 0.7.
- Fights: read from `rts/docs/reports/predict_v2/heldout/fights.jsonl` (the records may come from other data than the data below); current data digest: tuning a020a25c7d7c2eae, content 9cb28f0929df518c.
- Side A is seat 0; A shoots uphill only in the high_ground case. Predicted class none = both times equal (this includes both at the longest time). Margin = longer time / max(shorter time, min seconds), capped.
- Decided fights (result seat0 or seat1) give the accuracy; a predicted none on a decided fight is wrong. Stalemates, unresolved fights and draws are not in the accuracy: they agree when the predictor says none.
- Mismatched fights (start values too far apart, 271 here) are left out of every table.
- Intervals are Wilson at z = 1.96 over decided fights; a row with fewer than 10 decided fights is marked small.

**PREDICT fights=1904 judged=1633 decided=1603 correct=1293 accuracy=0.807 clean=47/60 no_winner_agree=15/30 decision=SIMULATE (proposed thresholds)**

## Decision (proposed rule, C.7)

| Threshold | Proposed | Measured | Fired |
| --- | ---: | ---: | --- |
| simulate_below_clean_accuracy | 0.85 | 0.783 (47/60) | yes |
| simulate_below_accuracy | 0.75 | 0.807 (1293/1603) | no |
| calibration_max_inversion | 0.10 | no inversion among bins with at least 10 fights | no |
| mechanic_min_fights | 20 | decided fights a mechanic needs to be judged | - |
| fix_min_mechanic_accuracy | 0.70 | lowest judged mechanic: flooding 0.737 (199/270) | - |
| fix_min_accuracy | 0.85 | - | - |

**Decision: SIMULATE** (all thresholds are proposed placeholders for the developer to accept or change)

- rule 1: clean accuracy 47/60 = 0.783 is below 0.85 (the formula is wrong even where it claims to model everything)

## Overall

| group | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| all scored fights | 1633 | 1603 | 1293 | 0.807 | [78.7, 82.5] | |
| clean (no unmodelled mechanic present) | 60 | 60 | 47 | 0.783 | [66.4, 86.9] | |

No-winner fights: 30, of which the predictor said none for 15. Decided fights predicted none (counted wrong): 27 (seat 0 won 10).

Seat order: 779 pairs of decided fights differ only in seat order; in 673 of them the predictor names the same side in both orders (classes are mirror images), and in 117 of those the measured winner is a different side in the two orders (a seat split the predictor cannot see).

## By unordered unit pair (accuracy ascending)

| pair | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| skai_pressure_mix vs water_pressure_mix | 9 | 9 | 1 | 0.111 | [2.0, 43.5] | small |
| water.ink_lobber vs water.reef_runner | 14 | 14 | 2 | 0.143 | [4.0, 39.9] | |
| skai.windrunner vs water.ink_lobber | 14 | 14 | 3 | 0.214 | [7.6, 47.6] | |
| skai_air_mix vs water.shellback | 14 | 8 | 2 | 0.250 | [7.1, 59.1] | small |
| skai.roc_rider vs water.shellback | 14 | 7 | 2 | 0.286 | [8.2, 64.1] | small |
| skai.windrunner vs water.reef_runner | 14 | 14 | 4 | 0.286 | [11.7, 54.6] | |
| water.ink_lobber vs water_pressure_mix | 10 | 10 | 3 | 0.300 | [10.8, 60.3] | |
| skai.thunderhead vs water.shellback | 14 | 12 | 4 | 0.333 | [13.8, 60.9] | |
| water.grasper vs water.ink_lobber | 14 | 14 | 5 | 0.357 | [16.3, 61.2] | |
| skai.thunderhead vs skai.windrunner | 14 | 14 | 6 | 0.429 | [21.4, 67.4] | |
| skai_air_mix vs skai_pressure_mix | 14 | 14 | 6 | 0.429 | [21.4, 67.4] | |
| skai.windrunner vs water.shellback | 14 | 13 | 6 | 0.462 | [23.2, 70.9] | |
| water.reef_runner vs water.shellback | 14 | 13 | 6 | 0.462 | [23.2, 70.9] | |
| skai.stormcaller vs water.reef_runner | 14 | 14 | 7 | 0.500 | [26.8, 73.2] | |
| skai.talon_archer vs skai.windrunner | 14 | 14 | 7 | 0.500 | [26.8, 73.2] | |
| skai.windrunner vs water.grasper | 14 | 14 | 7 | 0.500 | [26.8, 73.2] | |
| skai.stormcaller vs water.grasper | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| skai.talon_archer vs skai_pressure_mix | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| skai.talon_archer vs water.reef_runner | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| skai_pressure_mix vs water.grasper | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| skai_pressure_mix vs water.ink_lobber | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| water.leviathan vs water.shellback | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| skai.stormcaller vs water_pressure_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| skai.windrunner vs water_pressure_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| skai_air_mix vs skai_expand_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| water.reef_runner vs water_pressure_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| skai_air_mix vs water_expand_mix | 13 | 13 | 8 | 0.615 | [35.5, 82.3] | |
| skai.stormcaller vs skai.talon_archer | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | |
| skai.stormcaller vs skai.windrunner | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | |
| skai.stormcaller vs skai_pressure_mix | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | |
| skai.talon_archer vs water.ink_lobber | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | |
| skai.thunderhead vs skai_pressure_mix | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | |
| skai.windrunner vs water_expand_mix | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | |
| water.grasper vs water.reef_runner | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | |
| water.reef_runner vs water_expand_mix | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | |
| water.leviathan vs water_expand_mix | 9 | 9 | 6 | 0.667 | [35.4, 87.9] | small |
| skai.roc_rider vs skai.windrunner | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai.roc_rider vs skai_pressure_mix | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai_pressure_mix vs water.shellback | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai_expand_mix vs skai_pressure_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | small |
| skai_pressure_mix vs water_expand_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | small |
| water_expand_mix vs water_leviathan_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | small |
| skai.talon_archer vs water.shellback | 14 | 13 | 10 | 0.769 | [49.7, 91.8] | |
| skai.thunderhead vs water_expand_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | small |
| skai.thunderhead vs water_leviathan_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | small |
| water_expand_mix vs water_pressure_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | small |
| skai.windrunner vs skai_air_mix | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | |
| skai.roc_rider vs skai_expand_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.talon_archer vs skai_expand_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.windrunner vs skai_expand_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| water.grasper vs water_leviathan_mix | 5 | 5 | 4 | 0.800 | [37.6, 96.4] | small |
| water.shellback vs water_expand_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| water.shellback vs water_pressure_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| water.leviathan vs water_leviathan_mix | 11 | 11 | 9 | 0.818 | [52.3, 94.9] | |
| skai.roc_rider vs water_expand_mix | 13 | 12 | 10 | 0.833 | [55.2, 95.3] | |
| water.ink_lobber vs water_expand_mix | 13 | 13 | 11 | 0.846 | [57.8, 95.7] | |
| skai.roc_rider vs skai_air_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.roc_rider vs water.spoutling | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.stormcaller vs water.ink_lobber | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.stormcaller vs water.spoutling | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.thunderhead vs water.leviathan | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.thunderhead vs water.reef_runner | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.windrunner vs skai_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| water_leviathan_mix vs water_pressure_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | small |
| skai.talon_archer vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai_expand_mix vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai_expand_mix vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.grasper vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.spoutling vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.stormcaller vs water_expand_mix | 13 | 13 | 12 | 0.923 | [66.7, 98.6] | |
| skai.roc_rider vs skai.talon_archer | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.roc_rider vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.talon_archer vs skai.thunderhead | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.talon_archer vs skai_air_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.talon_archer vs water.grasper | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.talon_archer vs water_expand_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.windrunner vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai_air_mix vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai_pressure_mix vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.grasper vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.grasper vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.grasper vs water_expand_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.ink_lobber vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.ink_lobber vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.reef_runner vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.roc_rider vs skai.stormcaller | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs skai.thunderhead | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | small |
| skai.roc_rider vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.stormcaller vs skai.thunderhead | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.stormcaller vs skai_air_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.stormcaller vs skai_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.stormcaller vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.stormcaller vs water.shellback | 14 | 13 | 13 | 1.000 | [77.2, 100.0] | |
| skai.stormcaller vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | small |
| skai.talon_archer vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.talon_archer vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.talon_archer vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | small |
| skai.thunderhead vs skai_air_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.thunderhead vs skai_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.thunderhead vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.thunderhead vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.thunderhead vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.thunderhead vs water_pressure_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| skai.windrunner vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.windrunner vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | small |
| skai_air_mix vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_air_mix vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_air_mix vs water.leviathan | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| skai_air_mix vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_air_mix vs water_leviathan_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| skai_air_mix vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai_expand_mix vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai_expand_mix vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai_expand_mix vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_expand_mix vs water.shellback | 10 | 2 | 2 | 1.000 | [34.2, 100.0] | small |
| skai_expand_mix vs water_expand_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | small |
| skai_expand_mix vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai_expand_mix vs water_pressure_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | small |
| skai_pressure_mix vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_pressure_mix vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_pressure_mix vs water_leviathan_mix | 12 | 12 | 12 | 1.000 | [75.7, 100.0] | |
| water.grasper vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.ink_lobber vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.ink_lobber vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | small |
| water.leviathan vs water.reef_runner | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.leviathan vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.leviathan vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.reef_runner vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | small |
| water.shellback vs water.spoutling | 14 | 13 | 13 | 1.000 | [77.2, 100.0] | |
| water.shellback vs water_leviathan_mix | 10 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| water.spoutling vs water_expand_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.spoutling vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | small |

## By side (a fight counts for both sides; accuracy ascending)

| side | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| skai.windrunner | 207 | 206 | 130 | 0.631 | [56.3, 69.4] | |
| water.reef_runner | 207 | 206 | 143 | 0.694 | [62.8, 75.3] | |
| water.shellback | 208 | 179 | 127 | 0.709 | [63.9, 77.1] | |
| skai_pressure_mix | 205 | 205 | 146 | 0.712 | [64.7, 77.0] | |
| water.ink_lobber | 202 | 202 | 146 | 0.723 | [65.7, 78.0] | |
| water_pressure_mix | 153 | 153 | 118 | 0.771 | [69.9, 83.1] | |
| water_expand_mix | 181 | 180 | 145 | 0.806 | [74.2, 85.7] | |
| skai.talon_archer | 207 | 206 | 166 | 0.806 | [74.6, 85.4] | |
| water.grasper | 207 | 207 | 168 | 0.812 | [75.3, 85.9] | |
| skai.stormcaller | 202 | 201 | 164 | 0.816 | [75.7, 86.3] | |
| skai_air_mix | 201 | 195 | 165 | 0.846 | [78.9, 89.0] | |
| skai.thunderhead | 201 | 199 | 169 | 0.849 | [79.3, 89.2] | |
| skai.roc_rider | 206 | 198 | 175 | 0.884 | [83.2, 92.1] | |
| skai_expand_mix | 156 | 148 | 134 | 0.905 | [84.7, 94.3] | |
| water_leviathan_mix | 117 | 116 | 108 | 0.931 | [87.0, 96.5] | |
| water.leviathan | 199 | 199 | 186 | 0.935 | [89.1, 96.1] | |
| water.spoutling | 207 | 206 | 196 | 0.951 | [91.3, 97.3] | |

## By terrain case

| case | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| choke | 180 | 176 | 160 | 0.909 | [85.7, 94.3] | |
| flat | 422 | 414 | 339 | 0.819 | [77.9, 85.3] | |
| flat_uneven | 491 | 482 | 415 | 0.861 | [82.7, 88.9] | |
| flooded | 270 | 270 | 199 | 0.737 | [68.1, 78.6] | |
| high_ground | 270 | 261 | 180 | 0.690 | [63.1, 74.3] | |

## By army value

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| 001000 | 360 | 352 | 320 | 0.909 | [87.4, 93.5] | |
| 002000 | 491 | 482 | 415 | 0.861 | [82.7, 88.9] | |
| 002500 | 540 | 531 | 379 | 0.714 | [67.4, 75.1] | |
| 003000 | 242 | 238 | 179 | 0.752 | [69.4, 80.3] | |

## By case and army value

Value and case are confounded: in the committed sweep only flat is played at 750, so the 750 row is a flat row.

| case / army value | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| choke / 001000 | 180 | 176 | 160 | 0.909 | [85.7, 94.3] | |
| flat / 001000 | 180 | 176 | 160 | 0.909 | [85.7, 94.3] | |
| flat / 003000 | 242 | 238 | 179 | 0.752 | [69.4, 80.3] | |
| flat_uneven / 002000 | 491 | 482 | 415 | 0.861 | [82.7, 88.9] | |
| flooded / 002500 | 270 | 270 | 199 | 0.737 | [68.1, 78.6] | |
| high_ground / 002500 | 270 | 261 | 180 | 0.690 | [63.1, 74.3] | |

## By unmodelled mechanic

Present means the mechanic occurs in the fight by its definitions and case (windstorm_range and focus_fire never do: fights run without cycles and with attack-move control). "Only" = it is the only mechanic present.

| mechanic | present: decided | present: accuracy | absent: accuracy | only: decided | only: accuracy |
| --- | ---: | ---: | ---: | ---: | ---: |
| drench | 125 | 0.888 | 0.800 | 0 | - |
| splash | 1065 | 0.825 | 0.770 | 96 | 0.729 |
| momentum | 1200 | 0.808 | 0.801 | 287 | 0.801 |
| flooding | 270 | 0.737 | 0.821 | 12 | 1.000 |
| windstorm_range | 0 | - | 0.807 | 0 | - |
| regeneration | 448 | 0.797 | 0.810 | 42 | 0.810 |
| hit_delay | 0 | - | 0.807 | 0 | - |
| focus_fire | 0 | - | 0.807 | 0 | - |
| clean | 60 | 0.783 | - | - | - |

## Calibration of the predicted margin

Decided fights only. "Winner left" is the lower median of the measured winner's value left. Read: when the predictor names a winner by a ratio in this bin, it wins this often.

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) |
| --- | ---: | ---: | ---: | --- | ---: |
| predicted none | 27 | - | - | - | - |
| 1 to below 1.1 | 178 | 105 | 0.590 | [51.6, 66.0] | 0.579 |
| 1.1 to below 1.25 | 209 | 137 | 0.656 | [58.9, 71.7] | 0.572 |
| 1.25 to below 1.5 | 227 | 164 | 0.722 | [66.1, 77.7] | 0.592 |
| 1.5 to below 2 | 247 | 197 | 0.798 | [74.3, 84.3] | 0.647 |
| 2 to below 3 | 159 | 149 | 0.937 | [88.8, 96.5] | 0.814 |
| 3 and above | 556 | 541 | 0.973 | [95.6, 98.4] | 0.947 |

## No-winner fights

| fight | result | reason | predicted | value left leader | predicted winner |
| --- | --- | --- | --- | --- | --- |
| flat/1000/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat/1000/water.shellback/skai.roc_rider | stalemate | dome_absorbed | none | left1 | none |
| flat/1000/water.shellback/skai_air_mix | stalemate | dome_absorbed | none | left0 | none |
| flat/1000/skai_air_mix/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat/3000/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat/3000/skai.thunderhead/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat/3000/water.shellback/skai_expand_mix | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat/3000/skai_expand_mix/water.shellback | stalemate | dome_absorbed | seat0 | left1 | left0 |
| high_ground/2500/skai.roc_rider/water_expand_mix | stalemate | dome_absorbed | seat1 | left1 | left1 |
| high_ground/2500/skai.stormcaller/water.shellback | stalemate | dome_absorbed | seat1 | even | left1 |
| high_ground/2500/skai.talon_archer/water.shellback | stalemate | dome_absorbed | seat1 | left1 | left1 |
| high_ground/2500/skai.windrunner/water.shellback | stalemate | dome_absorbed | seat1 | left1 | left1 |
| high_ground/2500/water.reef_runner/water.shellback | stalemate | dome_absorbed | seat1 | left1 | left1 |
| high_ground/2500/water.spoutling/water.shellback | stalemate | dome_absorbed | seat1 | left1 | left1 |
| high_ground/2500/water.shellback/skai_expand_mix | stalemate | dome_absorbed | seat1 | left0 | left1 |
| high_ground/2500/skai_expand_mix/water.shellback | stalemate | dome_absorbed | seat0 | even | left0 |
| high_ground/2500/water_leviathan_mix/water.shellback | unresolved | time_cap | seat1 | left1 | left1 |
| choke/1000/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| choke/1000/water.shellback/skai.roc_rider | stalemate | dome_absorbed | none | left1 | none |
| choke/1000/water.shellback/skai_air_mix | stalemate | dome_absorbed | none | left0 | none |
| choke/1000/skai_air_mix/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat_uneven/2000/skai.roc_rider/water.shellback/r12500 | stalemate | dome_absorbed | none | left0 | none |
| flat_uneven/2000/water.shellback/skai_expand_mix/r12500 | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat_uneven/2000/skai_expand_mix/water.shellback/r12500 | stalemate | dome_absorbed | seat0 | left1 | left0 |
| flat_uneven/2000/skai_air_mix/water.shellback/r12500 | stalemate | dome_absorbed | none | left1 | none |
| flat_uneven/2000/skai.roc_rider/water.shellback/r15000 | stalemate | dome_absorbed | none | left0 | none |
| flat_uneven/2000/skai.thunderhead/water.shellback/r15000 | stalemate | dome_absorbed | none | left0 | none |
| flat_uneven/2000/water.shellback/skai_expand_mix/r15000 | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat_uneven/2000/skai_expand_mix/water.shellback/r15000 | stalemate | dome_absorbed | seat0 | left1 | left0 |
| flat_uneven/2000/skai_air_mix/water.shellback/r15000 | stalemate | dome_absorbed | none | left1 | none |

# Subsets of extended records (fight_predictor_v2.md 5.4)

Start predictions use GapTiles = start_distance_tiles, the water of lane_depth_class (Shallow in a flooded case without one) and neutral water; snapshot predictions use GapTiles 0 and the health of the snapshot. Baseline columns: profile `aggressive_s5`.

## Subset E

Start predictions of fights with equal target values (ratio 1).

Headline: 878 of 1121 decided fights = 0.783; baseline 0.708. No-winner fights 21, predicted none for 10; decided fights predicted none 22.

| unordered unit pair (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| skai.windrunner vs water.reef_runner | 10 | 10 | 0 | 0.000 | [0.0, 27.8] | 0.000 |  |
| skai_pressure_mix vs water_pressure_mix | 6 | 6 | 0 | 0.000 | [0.0, 39.0] | 0.833 | small |
| skai.windrunner vs water.ink_lobber | 10 | 10 | 1 | 0.100 | [1.8, 40.4] | 0.100 |  |
| water.ink_lobber vs water.reef_runner | 10 | 10 | 1 | 0.100 | [1.8, 40.4] | 0.100 |  |
| water.ink_lobber vs water_pressure_mix | 6 | 6 | 1 | 0.167 | [3.0, 56.4] | 0.167 | small |
| skai_air_mix vs skai_pressure_mix | 10 | 10 | 2 | 0.200 | [5.7, 51.0] | 0.200 |  |
| skai.thunderhead vs water.shellback | 10 | 9 | 2 | 0.222 | [6.3, 54.7] | 0.778 | small |
| water.grasper vs water.ink_lobber | 10 | 10 | 3 | 0.300 | [10.8, 60.3] | 0.300 |  |
| skai_air_mix vs skai_expand_mix | 6 | 6 | 2 | 0.333 | [9.7, 70.0] | 0.167 | small |
| skai_air_mix vs water.shellback | 10 | 6 | 2 | 0.333 | [9.7, 70.0] | 0.667 | small |
| skai.roc_rider vs water.shellback | 10 | 5 | 2 | 0.400 | [11.8, 76.9] | 0.600 | small |
| skai.talon_archer vs skai.windrunner | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.500 |  |
| skai.thunderhead vs skai.windrunner | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.000 |  |
| skai.windrunner vs water.grasper | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.900 |  |
| water.leviathan vs water.shellback | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.500 |  |
| skai.windrunner vs water.shellback | 10 | 9 | 4 | 0.444 | [18.9, 73.3] | 1.000 | small |
| water.reef_runner vs water.shellback | 10 | 9 | 4 | 0.444 | [18.9, 73.3] | 1.000 | small |
| skai.stormcaller vs water.grasper | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | 0.500 |  |
| skai.stormcaller vs water.reef_runner | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | 0.500 |  |
| skai.stormcaller vs water_pressure_mix | 6 | 6 | 3 | 0.500 | [18.8, 81.2] | 0.500 | small |
| skai.talon_archer vs skai_pressure_mix | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | 0.800 |  |
| skai.talon_archer vs water.reef_runner | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | 0.600 |  |
| skai.windrunner vs water_pressure_mix | 6 | 6 | 3 | 0.500 | [18.8, 81.2] | 0.833 | small |
| skai_air_mix vs water_expand_mix | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | 0.500 |  |
| skai_expand_mix vs skai_pressure_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.250 | small |
| water.reef_runner vs water_pressure_mix | 6 | 6 | 3 | 0.500 | [18.8, 81.2] | 0.833 | small |
| skai.stormcaller vs skai.talon_archer | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.600 |  |
| skai.talon_archer vs water.ink_lobber | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.600 |  |
| skai.windrunner vs water_expand_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.800 |  |
| skai_pressure_mix vs water.grasper | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.900 |  |
| skai_pressure_mix vs water.ink_lobber | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.600 |  |
| water.grasper vs water.reef_runner | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.900 |  |
| water.reef_runner vs water_expand_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.800 |  |
| skai.roc_rider vs skai_expand_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.667 | small |
| skai.talon_archer vs skai_expand_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.833 | small |
| skai.windrunner vs skai_expand_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.667 | small |
| skai_pressure_mix vs water_expand_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 1.000 | small |
| water.shellback vs water_expand_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.500 | small |
| water_expand_mix vs water_leviathan_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.167 | small |
| water_expand_mix vs water_pressure_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.667 | small |
| skai.stormcaller vs skai.windrunner | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.700 |  |
| skai.stormcaller vs skai_pressure_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.700 |  |
| skai.thunderhead vs skai_pressure_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.100 |  |
| skai.windrunner vs skai_air_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.200 |  |
| skai.thunderhead vs water_leviathan_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | 0.250 | small |
| water.grasper vs water_leviathan_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.250 | small |
| skai.roc_rider vs water_expand_mix | 10 | 9 | 7 | 0.778 | [45.3, 93.7] | 0.222 | small |
| skai.roc_rider vs skai.windrunner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.roc_rider vs skai_air_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.roc_rider vs skai_pressure_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.stormcaller vs water.ink_lobber | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.thunderhead vs water.leviathan | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.thunderhead vs water.reef_runner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 1.000 |  |
| skai.windrunner vs skai_pressure_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.700 |  |
| skai_pressure_mix vs water.shellback | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.900 |  |
| skai.talon_archer vs water_pressure_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.833 | small |
| skai.thunderhead vs water_expand_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.833 | small |
| skai_expand_mix vs water.reef_runner | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.833 | small |
| skai_expand_mix vs water.spoutling | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.833 | small |
| water.grasper vs water_pressure_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.833 | small |
| water.leviathan vs water_expand_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.500 | small |
| water.shellback vs water_pressure_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.833 | small |
| water.spoutling vs water_pressure_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.833 | small |
| water_leviathan_mix vs water_pressure_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.167 | small |
| water.leviathan vs water_leviathan_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 0.875 | small |
| skai.talon_archer vs water.shellback | 10 | 9 | 8 | 0.889 | [56.5, 98.0] | 1.000 | small |
| skai.roc_rider vs skai.talon_archer | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.roc_rider vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.roc_rider vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.stormcaller vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.200 |  |
| skai.talon_archer vs skai_air_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.200 |  |
| skai.talon_archer vs water.grasper | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.windrunner vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai_air_mix vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai_pressure_mix vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.800 |  |
| water.grasper vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.grasper vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.grasper vs water_expand_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.ink_lobber vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.ink_lobber vs water_expand_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.reef_runner vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.roc_rider vs skai.stormcaller | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs skai.thunderhead | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.000 | small |
| skai.roc_rider vs water_pressure_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai.thunderhead | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs skai_air_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs skai_expand_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.stormcaller vs water.leviathan | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.stormcaller vs water.shellback | 10 | 9 | 9 | 1.000 | [70.1, 100.0] | 1.000 | small |
| skai.stormcaller vs water_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs skai.thunderhead | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.000 |  |
| skai.talon_archer vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.700 |  |
| skai.talon_archer vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.talon_archer vs water_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.talon_archer vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.250 | small |
| skai.thunderhead vs skai_air_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.thunderhead vs skai_expand_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs water_pressure_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.windrunner vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.500 |  |
| skai.windrunner vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.250 | small |
| skai_air_mix vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_air_mix vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_air_mix vs water.leviathan | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_air_mix vs water_leviathan_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 0.833 | small |
| skai_air_mix vs water_pressure_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.grasper | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.ink_lobber | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.300 |  |
| skai_expand_mix vs water.shellback | 6 | 2 | 2 | 1.000 | [34.2, 100.0] | 0.000 | small |
| skai_expand_mix vs water_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 0.125 | small |
| skai_expand_mix vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai_pressure_mix vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.300 |  |
| skai_pressure_mix vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_pressure_mix vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.400 |  |
| water.grasper vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.000 |  |
| water.ink_lobber vs water.leviathan | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| water.ink_lobber vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.900 |  |
| water.ink_lobber vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.leviathan vs water.reef_runner | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.500 |  |
| water.leviathan vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| water.leviathan vs water_pressure_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 0.000 | small |
| water.reef_runner vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.250 | small |
| water.shellback vs water.spoutling | 10 | 9 | 9 | 1.000 | [70.1, 100.0] | 1.000 | small |
| water.shellback vs water_leviathan_mix | 8 | 7 | 7 | 1.000 | [64.6, 100.0] | 1.000 | small |
| water.spoutling vs water_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| water.spoutling vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |

| side (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| skai.windrunner | 146 | 145 | 83 | 0.572 | [49.1, 65.0] | 0.552 |  |
| water.reef_runner | 146 | 145 | 93 | 0.641 | [56.1, 71.5] | 0.683 |  |
| water.shellback | 146 | 126 | 88 | 0.698 | [61.3, 77.2] | 0.841 |  |
| skai_pressure_mix | 146 | 146 | 102 | 0.699 | [62.0, 76.7] | 0.644 |  |
| water.ink_lobber | 142 | 142 | 100 | 0.704 | [62.5, 77.3] | 0.697 |  |
| water_pressure_mix | 94 | 94 | 67 | 0.713 | [61.4, 79.4] | 0.681 |  |
| water.grasper | 146 | 146 | 114 | 0.781 | [70.7, 84.0] | 0.774 |  |
| skai.talon_archer | 146 | 145 | 114 | 0.786 | [71.3, 84.5] | 0.676 |  |
| water_expand_mix | 130 | 129 | 102 | 0.791 | [71.3, 85.2] | 0.752 |  |
| skai_air_mix | 140 | 136 | 108 | 0.794 | [71.9, 85.4] | 0.706 |  |
| skai.stormcaller | 142 | 141 | 115 | 0.816 | [74.4, 87.1] | 0.766 |  |
| skai.thunderhead | 142 | 141 | 118 | 0.837 | [76.7, 88.9] | 0.716 |  |
| skai_expand_mix | 96 | 92 | 78 | 0.848 | [76.1, 90.7] | 0.641 |  |
| skai.roc_rider | 146 | 140 | 124 | 0.886 | [82.2, 92.8] | 0.829 |  |
| water_leviathan_mix | 92 | 91 | 84 | 0.923 | [85.0, 96.2] | 0.484 |  |
| water.leviathan | 138 | 138 | 128 | 0.928 | [87.2, 96.0] | 0.609 |  |
| water.spoutling | 146 | 145 | 138 | 0.952 | [90.4, 97.6] | 0.897 |  |

| case | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| choke | 180 | 176 | 160 | 0.909 | [85.7, 94.3] | 0.795 |  |
| flat | 422 | 414 | 339 | 0.819 | [77.9, 85.3] | 0.756 |  |
| flooded | 270 | 270 | 199 | 0.737 | [68.1, 78.6] | 0.663 |  |
| high_ground | 270 | 261 | 180 | 0.690 | [63.1, 74.3] | 0.621 |  |

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| 001000 | 360 | 352 | 320 | 0.909 | [87.4, 93.5] | 0.801 |  |
| 002500 | 540 | 531 | 379 | 0.714 | [67.4, 75.1] | 0.642 |  |
| 003000 | 242 | 238 | 179 | 0.752 | [69.4, 80.3] | 0.718 |  |

Calibration (decided fights, predicted winner by margin bin):

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) | baseline share |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| predicted none | 22 | - | - | - | - | - |
| 1 to below 1.1 | 151 | 87 | 0.576 | [49.6, 65.2] | 0.589 | 0.542 (58/107) |
| 1.1 to below 1.25 | 133 | 81 | 0.609 | [52.4, 68.8] | 0.591 | 0.629 (83/132) |
| 1.25 to below 1.5 | 167 | 122 | 0.731 | [65.9, 79.2] | 0.612 | 0.488 (63/129) |
| 1.5 to below 2 | 166 | 129 | 0.777 | [70.8, 83.4] | 0.655 | 0.753 (119/158) |
| 2 to below 3 | 98 | 89 | 0.908 | [83.5, 95.1] | 0.797 | 0.764 (123/161) |
| 3 and above | 384 | 370 | 0.964 | [94.0, 97.8] | 0.944 | 0.819 (348/425) |

## Subset U

Start predictions of fights where seat 1's target value differs (seat1_value_ratios).

Headline: 415 of 482 decided fights = 0.861; baseline 0.807. No-winner fights 9, predicted none for 5; decided fights predicted none 5.

| unordered unit pair (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| skai.roc_rider vs water.shellback | 4 | 2 | 0 | 0.000 | [0.0, 65.8] | 1.000 | small |
| skai_air_mix vs water.shellback | 4 | 2 | 0 | 0.000 | [0.0, 65.8] | 1.000 | small |
| water.ink_lobber vs water.reef_runner | 4 | 4 | 1 | 0.250 | [4.6, 69.9] | 0.250 | small |
| skai_pressure_mix vs water_pressure_mix | 3 | 3 | 1 | 0.333 | [6.1, 79.2] | 1.000 | small |
| water.leviathan vs water_expand_mix | 3 | 3 | 1 | 0.333 | [6.1, 79.2] | 0.333 | small |
| skai.roc_rider vs skai.windrunner | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 1.000 | small |
| skai.roc_rider vs skai_pressure_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 1.000 | small |
| skai.stormcaller vs skai.windrunner | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| skai.stormcaller vs skai_pressure_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| skai.stormcaller vs water.reef_runner | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| skai.talon_archer vs water.shellback | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.750 | small |
| skai.thunderhead vs skai.windrunner | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.250 | small |
| skai.thunderhead vs skai_pressure_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.250 | small |
| skai.windrunner vs water.ink_lobber | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| skai.windrunner vs water.shellback | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.750 | small |
| skai_pressure_mix vs water.grasper | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 1.000 | small |
| skai_pressure_mix vs water.ink_lobber | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| skai_pressure_mix vs water.shellback | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| water.grasper vs water.ink_lobber | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.250 | small |
| water.ink_lobber vs water_pressure_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| water.reef_runner vs water.shellback | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.750 | small |
| skai.stormcaller vs water_expand_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 0.667 | small |
| skai.thunderhead vs water.shellback | 4 | 3 | 2 | 0.667 | [20.8, 93.9] | 1.000 | small |
| skai.thunderhead vs water_expand_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 0.667 | small |
| water.ink_lobber vs water_expand_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 0.667 | small |
| water.leviathan vs water_leviathan_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 1.000 | small |
| skai.roc_rider vs water.spoutling | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| skai.stormcaller vs skai.talon_archer | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.stormcaller vs water.grasper | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.stormcaller vs water.spoutling | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.stormcaller vs water_pressure_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.talon_archer vs skai.thunderhead | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.250 | small |
| skai.talon_archer vs skai.windrunner | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai.talon_archer vs skai_pressure_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai.talon_archer vs water.ink_lobber | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.talon_archer vs water.reef_runner | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai.talon_archer vs water_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| skai.windrunner vs water.grasper | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai.windrunner vs water_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| skai.windrunner vs water_pressure_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| water.grasper vs water.reef_runner | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| water.ink_lobber vs water.spoutling | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| water.reef_runner vs water_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| water.reef_runner vs water_pressure_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| water.shellback vs water_pressure_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| skai.roc_rider vs skai.stormcaller | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs skai.talon_archer | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs skai.thunderhead | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.roc_rider vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs water_expand_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 0.333 | small |
| skai.roc_rider vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| skai.roc_rider vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai.thunderhead | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| skai.stormcaller vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| skai.talon_archer vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.talon_archer vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| skai.talon_archer vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.thunderhead vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| skai.thunderhead vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 0.000 | small |
| skai.thunderhead vs water_pressure_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai.windrunner vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.windrunner vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| skai_air_mix vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.leviathan | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 0.667 | small |
| skai_air_mix vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| skai_air_mix vs water_expand_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai_air_mix vs water_leviathan_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai_air_mix vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai_expand_mix vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water_expand_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai_expand_mix vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 0.500 | small |
| skai_expand_mix vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_pressure_mix vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai_pressure_mix vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_pressure_mix vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_pressure_mix vs water_expand_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai_pressure_mix vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 0.500 | small |
| water.grasper vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.grasper vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.grasper vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.grasper vs water_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.grasper vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 0.000 | small |
| water.grasper vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.ink_lobber vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.ink_lobber vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.ink_lobber vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| water.leviathan vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.leviathan vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.leviathan vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| water.leviathan vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.reef_runner vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.reef_runner vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| water.shellback vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.shellback vs water_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.shellback vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 0.000 | small |
| water.spoutling vs water_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.spoutling vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| water.spoutling vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water_expand_mix vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| water_expand_mix vs water_pressure_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 0.333 | small |
| water_leviathan_mix vs water_pressure_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 0.000 | small |
| skai_expand_mix vs water.shellback | 4 | 0 | 0 | - | - | - | small |

| side (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| water.shellback | 62 | 53 | 39 | 0.736 | [60.4, 83.6] | 0.849 |  |
| skai_pressure_mix | 59 | 59 | 44 | 0.746 | [62.2, 83.9] | 0.797 |  |
| water.ink_lobber | 60 | 60 | 46 | 0.767 | [64.6, 85.6] | 0.683 |  |
| skai.windrunner | 61 | 61 | 47 | 0.770 | [65.1, 85.8] | 0.820 |  |
| skai.stormcaller | 60 | 60 | 49 | 0.817 | [70.1, 89.4] | 0.733 |  |
| water.reef_runner | 61 | 61 | 50 | 0.820 | [70.5, 89.6] | 0.852 |  |
| water_expand_mix | 51 | 51 | 43 | 0.843 | [72.0, 91.8] | 0.725 |  |
| skai.talon_archer | 61 | 61 | 52 | 0.852 | [74.3, 92.0] | 0.820 |  |
| water_pressure_mix | 59 | 59 | 51 | 0.864 | [75.5, 93.0] | 0.814 |  |
| skai.roc_rider | 60 | 58 | 51 | 0.879 | [77.1, 94.0] | 0.914 |  |
| skai.thunderhead | 59 | 58 | 51 | 0.879 | [77.1, 94.0] | 0.759 |  |
| water.grasper | 61 | 61 | 54 | 0.885 | [78.2, 94.3] | 0.836 |  |
| water.leviathan | 61 | 61 | 58 | 0.951 | [86.5, 98.3] | 0.590 |  |
| water.spoutling | 61 | 61 | 58 | 0.951 | [86.5, 98.3] | 0.869 |  |
| water_leviathan_mix | 25 | 25 | 24 | 0.960 | [80.5, 99.3] | 0.680 |  |
| skai_air_mix | 61 | 59 | 57 | 0.966 | [88.5, 99.1] | 0.966 |  |
| skai_expand_mix | 60 | 56 | 56 | 1.000 | [93.6, 100.0] | 0.946 |  |

| case | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| flat_uneven | 491 | 482 | 415 | 0.861 | [82.7, 88.9] | 0.807 |  |

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| 002000 | 491 | 482 | 415 | 0.861 | [82.7, 88.9] | 0.807 |  |

Calibration (decided fights, predicted winner by margin bin):

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) | baseline share |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| predicted none | 5 | - | - | - | - | - |
| 1 to below 1.1 | 27 | 18 | 0.667 | [47.8, 81.4] | 0.523 | 0.389 (7/18) |
| 1.1 to below 1.25 | 76 | 56 | 0.737 | [62.8, 82.3] | 0.542 | 0.636 (21/33) |
| 1.25 to below 1.5 | 60 | 42 | 0.700 | [57.5, 80.1] | 0.588 | 0.741 (40/54) |
| 1.5 to below 2 | 81 | 68 | 0.840 | [74.5, 90.4] | 0.647 | 0.764 (55/72) |
| 2 to below 3 | 61 | 60 | 0.984 | [91.3, 99.7] | 0.821 | 0.702 (66/94) |
| 3 and above | 172 | 171 | 0.994 | [96.8, 99.9] | 0.947 | 0.948 (200/211) |

## Subset D

Snapshot predictions of damaged armies: the armies when either seat had lost snapshot_loss_fraction of its value, scored against the fight's measured winner.

Headline: 1371 of 1602 decided fights = 0.856; baseline 0.844. No-winner fights 9, predicted none for 0; decided fights predicted none 0.

| unordered unit pair (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| water.shellback vs water_leviathan_mix | 10 | 9 | 0 | 0.000 | [0.0, 29.9] | 1.000 | small |
| water.ink_lobber vs water.reef_runner | 14 | 14 | 3 | 0.214 | [7.6, 47.6] | 0.214 |  |
| water.leviathan vs water_leviathan_mix | 11 | 11 | 3 | 0.273 | [9.7, 56.6] | 1.000 |  |
| skai.windrunner vs water.ink_lobber | 14 | 14 | 5 | 0.357 | [16.3, 61.2] | 0.357 |  |
| water.grasper vs water.ink_lobber | 14 | 14 | 5 | 0.357 | [16.3, 61.2] | 0.357 |  |
| skai_pressure_mix vs water_expand_mix | 8 | 8 | 3 | 0.375 | [13.7, 69.4] | 0.875 | small |
| water.ink_lobber vs water_pressure_mix | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.400 |  |
| skai.windrunner vs water_expand_mix | 14 | 14 | 7 | 0.500 | [26.8, 73.2] | 1.000 |  |
| skai.stormcaller vs water.grasper | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | 0.500 |  |
| skai.stormcaller vs water.reef_runner | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | 0.571 |  |
| water.leviathan vs water.shellback | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | 0.643 |  |
| skai.stormcaller vs water_pressure_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.500 |  |
| skai.windrunner vs water_pressure_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.900 |  |
| water.reef_runner vs water_pressure_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.900 |  |
| skai_air_mix vs water_expand_mix | 13 | 13 | 8 | 0.615 | [35.5, 82.3] | 0.538 |  |
| skai.stormcaller vs skai.talon_archer | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | 0.571 |  |
| skai.talon_archer vs skai.windrunner | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | 0.571 |  |
| skai_pressure_mix vs water.ink_lobber | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | 0.643 |  |
| water.reef_runner vs water_expand_mix | 14 | 14 | 9 | 0.643 | [38.8, 83.7] | 0.929 |  |
| skai.roc_rider vs water_expand_mix | 13 | 12 | 8 | 0.667 | [39.1, 86.2] | 0.250 |  |
| skai_pressure_mix vs water_pressure_mix | 9 | 9 | 6 | 0.667 | [35.4, 87.9] | 0.667 | small |
| skai.stormcaller vs water_expand_mix | 13 | 13 | 9 | 0.692 | [42.4, 87.3] | 1.000 |  |
| water.shellback vs water_expand_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.800 |  |
| skai.stormcaller vs skai.windrunner | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | 0.643 |  |
| skai.stormcaller vs skai_pressure_mix | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | 0.643 |  |
| skai.talon_archer vs skai_pressure_mix | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | 0.929 |  |
| skai.talon_archer vs water.reef_runner | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | 0.643 |  |
| skai.talon_archer vs water_expand_mix | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | 0.929 |  |
| skai_air_mix vs water.shellback | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | 0.750 | small |
| skai_expand_mix vs skai_pressure_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | 0.750 | small |
| water.ink_lobber vs water_expand_mix | 13 | 13 | 10 | 0.769 | [49.7, 91.8] | 0.923 |  |
| skai.thunderhead vs water_expand_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | 0.778 | small |
| skai.thunderhead vs water_leviathan_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | 0.889 | small |
| water_expand_mix vs water_pressure_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | 0.778 | small |
| skai.roc_rider vs skai.windrunner | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | 0.929 |  |
| skai.roc_rider vs skai_pressure_mix | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | 0.929 |  |
| skai.talon_archer vs water.ink_lobber | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | 0.643 |  |
| skai_pressure_mix vs water.grasper | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | 0.786 |  |
| skai_pressure_mix vs water.shellback | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | 0.929 |  |
| skai.windrunner vs skai_expand_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 1.000 |  |
| skai_air_mix vs skai_expand_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.700 |  |
| water.grasper vs water_leviathan_mix | 5 | 5 | 4 | 0.800 | [37.6, 96.4] | 0.400 | small |
| water.shellback vs water_pressure_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.thunderhead vs water.shellback | 12 | 12 | 10 | 0.833 | [55.2, 95.3] | 0.833 |  |
| skai_expand_mix vs water_expand_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 1.000 | small |
| skai.roc_rider vs water.spoutling | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| skai.stormcaller vs water.ink_lobber | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.857 |  |
| skai.talon_archer vs water.grasper | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| skai.thunderhead vs skai.windrunner | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.500 |  |
| skai.thunderhead vs water.leviathan | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.786 |  |
| skai.windrunner vs water.grasper | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| skai.windrunner vs water.reef_runner | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.857 |  |
| water.grasper vs water.reef_runner | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| water_expand_mix vs water_leviathan_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 0.875 | small |
| water_leviathan_mix vs water_pressure_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 0.250 | small |
| skai.roc_rider vs skai_expand_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 1.000 |  |
| skai.talon_archer vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai_expand_mix vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.grasper vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.spoutling vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.stormcaller vs water.shellback | 13 | 13 | 12 | 0.923 | [66.7, 98.6] | 1.000 |  |
| skai.talon_archer vs water.shellback | 13 | 13 | 12 | 0.923 | [66.7, 98.6] | 1.000 |  |
| skai.windrunner vs water.shellback | 13 | 13 | 12 | 0.923 | [66.7, 98.6] | 1.000 |  |
| water.reef_runner vs water.shellback | 13 | 13 | 12 | 0.923 | [66.7, 98.6] | 1.000 |  |
| skai.roc_rider vs skai_air_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.roc_rider vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.talon_archer vs skai.thunderhead | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.357 |  |
| skai.thunderhead vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 1.000 |  |
| skai.windrunner vs skai_pressure_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.windrunner vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai_air_mix vs skai_pressure_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.786 |  |
| water.grasper vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.grasper vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.grasper vs water_expand_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.ink_lobber vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.reef_runner vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.spoutling vs water_expand_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 1.000 |  |
| skai.roc_rider vs skai.stormcaller | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs skai.talon_archer | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs skai.thunderhead | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs water.shellback | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | 0.714 | small |
| skai.roc_rider vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | 0.800 | small |
| skai.roc_rider vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs skai.thunderhead | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.stormcaller vs skai_air_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.stormcaller vs skai_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| skai.stormcaller vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | 1.000 | small |
| skai.talon_archer vs skai_air_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.571 |  |
| skai.talon_archer vs skai_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.talon_archer vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.786 |  |
| skai.talon_archer vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.talon_archer vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | 1.000 | small |
| skai.thunderhead vs skai_air_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs skai_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs skai_pressure_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.357 |  |
| skai.thunderhead vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.thunderhead vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.thunderhead vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.thunderhead vs water_pressure_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | 1.000 | small |
| skai.windrunner vs skai_air_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.786 |  |
| skai.windrunner vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.857 |  |
| skai.windrunner vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | 0.800 | small |
| skai_air_mix vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai_air_mix vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai_air_mix vs water.leviathan | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | 0.778 | small |
| skai_air_mix vs water.reef_runner | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| skai_air_mix vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai_air_mix vs water_leviathan_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | 0.889 | small |
| skai_air_mix vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_expand_mix vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_expand_mix vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_expand_mix vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| skai_expand_mix vs water.reef_runner | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.900 |  |
| skai_expand_mix vs water.shellback | 9 | 2 | 2 | 1.000 | [34.2, 100.0] | 0.000 | small |
| skai_expand_mix vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_expand_mix vs water_pressure_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 0.750 | small |
| skai_pressure_mix vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| skai_pressure_mix vs water.reef_runner | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai_pressure_mix vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai_pressure_mix vs water_leviathan_mix | 12 | 12 | 12 | 1.000 | [75.7, 100.0] | 0.917 |  |
| water.grasper vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.857 |  |
| water.ink_lobber vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.900 |  |
| water.ink_lobber vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| water.ink_lobber vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | 1.000 | small |
| water.leviathan vs water.reef_runner | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.857 |  |
| water.leviathan vs water.spoutling | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | 1.000 |  |
| water.leviathan vs water_expand_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | 0.556 | small |
| water.leviathan vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.800 |  |
| water.reef_runner vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | 0.800 | small |
| water.shellback vs water.spoutling | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | 1.000 |  |
| water.spoutling vs water_leviathan_mix | 5 | 5 | 5 | 1.000 | [56.6, 100.0] | 1.000 | small |

| side (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| water_expand_mix | 181 | 180 | 132 | 0.733 | [66.4, 79.3] | 0.828 |  |
| water.ink_lobber | 202 | 202 | 153 | 0.757 | [69.4, 81.1] | 0.748 |  |
| skai.windrunner | 206 | 206 | 163 | 0.791 | [73.1, 84.1] | 0.806 |  |
| water_leviathan_mix | 117 | 116 | 94 | 0.810 | [73.0, 87.1] | 0.862 |  |
| water_pressure_mix | 153 | 153 | 124 | 0.810 | [74.1, 86.5] | 0.784 |  |
| water.reef_runner | 206 | 206 | 168 | 0.816 | [75.7, 86.3] | 0.835 |  |
| water.shellback | 187 | 179 | 146 | 0.816 | [75.2, 86.6] | 0.888 |  |
| skai.stormcaller | 201 | 201 | 165 | 0.821 | [76.2, 86.8] | 0.816 |  |
| skai_pressure_mix | 205 | 205 | 171 | 0.834 | [77.7, 87.9] | 0.820 |  |
| skai.talon_archer | 206 | 206 | 176 | 0.854 | [80.0, 89.6] | 0.786 |  |
| water.grasper | 207 | 207 | 178 | 0.860 | [80.6, 90.1] | 0.850 |  |
| water.leviathan | 198 | 198 | 182 | 0.919 | [87.3, 95.0] | 0.859 |  |
| skai.roc_rider | 199 | 198 | 183 | 0.924 | [87.9, 95.4] | 0.914 |  |
| skai_expand_mix | 155 | 148 | 139 | 0.939 | [88.8, 96.8] | 0.919 |  |
| skai.thunderhead | 199 | 199 | 187 | 0.940 | [89.8, 96.5] | 0.834 |  |
| skai_air_mix | 195 | 195 | 184 | 0.944 | [90.2, 96.8] | 0.856 |  |
| water.spoutling | 205 | 205 | 197 | 0.961 | [92.5, 98.0] | 0.961 |  |

| case | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| choke | 176 | 176 | 159 | 0.903 | [85.1, 93.9] | 0.898 |  |
| flat | 416 | 414 | 351 | 0.848 | [81.0, 87.9] | 0.853 |  |
| flat_uneven | 486 | 482 | 435 | 0.902 | [87.3, 92.6] | 0.892 |  |
| flooded | 269 | 269 | 225 | 0.836 | [78.8, 87.6] | 0.822 |  |
| high_ground | 264 | 261 | 201 | 0.770 | [71.5, 81.7] | 0.728 |  |

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| 001000 | 352 | 352 | 318 | 0.903 | [86.8, 93.0] | 0.898 |  |
| 002000 | 486 | 482 | 435 | 0.902 | [87.3, 92.6] | 0.892 |  |
| 002500 | 533 | 530 | 426 | 0.804 | [76.8, 83.5] | 0.775 |  |
| 003000 | 240 | 238 | 192 | 0.807 | [75.2, 85.2] | 0.819 |  |

Calibration (decided fights, predicted winner by margin bin):

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) | baseline share |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| predicted none | 0 | - | - | - | - | - |
| 1 to below 1.1 | 208 | 114 | 0.548 | [48.0, 61.4] | 0.478 | 0.634 (52/82) |
| 1.1 to below 1.25 | 263 | 187 | 0.711 | [65.4, 76.2] | 0.521 | 0.567 (68/120) |
| 1.25 to below 1.5 | 263 | 215 | 0.817 | [76.6, 85.9] | 0.594 | 0.741 (106/143) |
| 1.5 to below 2 | 268 | 258 | 0.963 | [93.3, 98.0] | 0.737 | 0.780 (192/246) |
| 2 to below 3 | 180 | 177 | 0.983 | [95.2, 99.4] | 0.857 | 0.844 (232/275) |
| 3 and above | 420 | 420 | 1.000 | [99.1, 100.0] | 0.998 | 0.954 (702/736) |

## Bars

| ID | bar | measured | result | reason |
| --- | --- | --- | --- | --- |
| B1 | accuracy on E >= 0.80 and >= baseline + 0.05 | 0.783 (878/1121), baseline 0.708 (794/1121) | FAIL |  |
| B2 | no bin with >= 10 decided fights wins less than a lower bin by more than 0.10 | no inversion | PASS |  |
| B3 | predicted winner wins >= 0.70 of decided fights with margin in [1.3, 1.5) | 0.752 (124/165) | PASS |  |
| B4 | no-winner fights predicted none >= 0.50 (judged from 5 fights); decided fights predicted none <= 0.02 | none predicted for 15/30 no-winner fights; 27/1603 decided fights | PASS |  |
| B5 | every pair of two single units in E with >= 8 decided fights has accuracy >= 0.40 | 52 pairs judged, 5 below; worst skai.windrunner vs water.reef_runner 0.000 (0/10) | FAIL |  |
| B6 | accuracy on D >= 0.75 and >= baseline's | 0.856 (1371/1602), baseline 0.844 (1352/1602) | PASS |  |
| B7 | accuracy on U >= baseline's | 0.861 (415/482), baseline 0.807 (389/482) | PASS |  |
| C1 | mean wall time per Predict call <= 1000 microseconds | stderr only (timing is never written to a file) | STDERR | the coordinator reads the stderr line; not counted in bars= |


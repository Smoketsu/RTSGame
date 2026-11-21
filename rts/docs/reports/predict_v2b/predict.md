<!-- predict.md: written by the balance harness --predict-fights (rts/tools/BalanceHarness/PredictRunner.cs); not a source file. -->
# Fight predictor against measured fights

- Predictor: FightPredictor.Predict with the predictor_* numbers of profile `aggressive_s5_fp2`; AI thresholds of that profile: attack_min_advantage 1.3, retreat_max_advantage 0.7.
- Fights: read from `rts/docs/reports/predict_v2b/heldout/fights.jsonl` (the records may come from other data than the data below); current data digest: tuning a020a25c7d7c2eae, content 9cb28f0929df518c.
- Side A is seat 0; A shoots uphill only in the high_ground case. Predicted class none = both times equal (this includes both at the longest time). Margin = longer time / max(shorter time, min seconds), capped.
- Decided fights (result seat0 or seat1) give the accuracy; a predicted none on a decided fight is wrong. Stalemates, unresolved fights and draws are not in the accuracy: they agree when the predictor says none.
- Mismatched fights (start values too far apart, 283 here) are left out of every table.
- Intervals are Wilson at z = 1.96 over decided fights; a row with fewer than 10 decided fights is marked small.

**PREDICT fights=1904 judged=1621 decided=1595 correct=1353 accuracy=0.848 clean=60/72 no_winner_agree=15/26 decision=SIMULATE (proposed thresholds)**

## Decision (proposed rule, C.7)

| Threshold | Proposed | Measured | Fired |
| --- | ---: | ---: | --- |
| simulate_below_clean_accuracy | 0.85 | 0.833 (60/72) | yes |
| simulate_below_accuracy | 0.75 | 0.848 (1353/1595) | no |
| calibration_max_inversion | 0.10 | no inversion among bins with at least 10 fights | no |
| mechanic_min_fights | 20 | decided fights a mechanic needs to be judged | - |
| fix_min_mechanic_accuracy | 0.70 | lowest judged mechanic: flooding 0.754 (175/232) | - |
| fix_min_accuracy | 0.85 | - | - |

**Decision: SIMULATE** (all thresholds are proposed placeholders for the developer to accept or change)

- rule 1: clean accuracy 60/72 = 0.833 is below 0.85 (the formula is wrong even where it claims to model everything)

## Overall

| group | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| all scored fights | 1621 | 1595 | 1353 | 0.848 | [83.0, 86.5] | |
| clean (no unmodelled mechanic present) | 72 | 72 | 60 | 0.833 | [73.1, 90.2] | |

No-winner fights: 26, of which the predictor said none for 15. Decided fights predicted none (counted wrong): 22 (seat 0 won 9).

Seat order: 771 pairs of decided fights differ only in seat order; in 651 of them the predictor names the same side in both orders (classes are mirror images), and in 99 of those the measured winner is a different side in the two orders (a seat split the predictor cannot see).

## By unordered unit pair (accuracy ascending)

| pair | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| skai_air_mix vs water.shellback | 8 | 4 | 1 | 0.250 | [4.6, 69.9] | small |
| skai_pressure_mix vs water_pressure_mix | 8 | 8 | 2 | 0.250 | [7.1, 59.1] | small |
| skai.windrunner vs water.reef_runner | 14 | 14 | 4 | 0.286 | [11.7, 54.6] | |
| skai.roc_rider vs water.shellback | 14 | 6 | 2 | 0.333 | [9.7, 70.0] | small |
| skai.thunderhead vs water.shellback | 14 | 11 | 4 | 0.364 | [15.2, 64.6] | |
| skai.windrunner vs water.shellback | 14 | 14 | 6 | 0.429 | [21.4, 67.4] | |
| water.reef_runner vs water.shellback | 14 | 14 | 6 | 0.429 | [21.4, 67.4] | |
| skai.stormcaller vs skai.windrunner | 14 | 14 | 7 | 0.500 | [26.8, 73.2] | |
| skai.stormcaller vs water_pressure_mix | 12 | 12 | 6 | 0.500 | [25.4, 74.6] | |
| skai.windrunner vs water_pressure_mix | 14 | 14 | 7 | 0.500 | [26.8, 73.2] | |
| water.grasper vs water_expand_mix | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | |
| water.reef_runner vs water_pressure_mix | 14 | 14 | 7 | 0.500 | [26.8, 73.2] | |
| skai.roc_rider vs skai_pressure_mix | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| skai.stormcaller vs water.grasper | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| skai.stormcaller vs water.reef_runner | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | |
| water.ink_lobber vs water_pressure_mix | 12 | 12 | 7 | 0.583 | [32.0, 80.7] | |
| skai.talon_archer vs skai_pressure_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| skai.thunderhead vs water_leviathan_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| skai_pressure_mix vs water.grasper | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| skai_pressure_mix vs water.ink_lobber | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| water.leviathan vs water_expand_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| water_expand_mix vs water_pressure_mix | 8 | 8 | 5 | 0.625 | [30.6, 86.3] | small |
| water.leviathan vs water_leviathan_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | |
| skai.roc_rider vs skai_expand_mix | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai.stormcaller vs water.ink_lobber | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai.talon_archer vs water.shellback | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai.windrunner vs skai_expand_mix | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai.windrunner vs water.grasper | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai.windrunner vs water.ink_lobber | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| water.grasper vs water.reef_runner | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| water.ink_lobber vs water.reef_runner | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| water.leviathan vs water.shellback | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | |
| skai.roc_rider vs water_expand_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | small |
| skai.thunderhead vs water_expand_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | small |
| skai_air_mix vs skai_expand_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | small |
| skai_air_mix vs water_expand_mix | 12 | 12 | 9 | 0.750 | [46.8, 91.1] | |
| skai_expand_mix vs water_expand_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | small |
| skai.roc_rider vs skai_air_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | small |
| water.ink_lobber vs water_expand_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | small |
| water.shellback vs water_expand_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | small |
| skai.roc_rider vs skai.windrunner | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | |
| skai.talon_archer vs water.reef_runner | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | |
| skai.windrunner vs skai_pressure_mix | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | |
| water.grasper vs water.ink_lobber | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | |
| water.shellback vs water_leviathan_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.talon_archer vs skai_expand_mix | 12 | 12 | 10 | 0.833 | [55.2, 95.3] | |
| water_expand_mix vs water_leviathan_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | small |
| skai.roc_rider vs skai.talon_archer | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.roc_rider vs water.spoutling | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.talon_archer vs skai.windrunner | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.talon_archer vs skai_air_mix | 7 | 7 | 6 | 0.857 | [48.7, 97.4] | small |
| skai.talon_archer vs water.ink_lobber | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.talon_archer vs water_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai.thunderhead vs water.leviathan | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai_air_mix vs skai_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai_pressure_mix vs water.reef_runner | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai_pressure_mix vs water.shellback | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | |
| skai_pressure_mix vs water_expand_mix | 7 | 7 | 6 | 0.857 | [48.7, 97.4] | small |
| skai.windrunner vs skai_air_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | small |
| skai_expand_mix vs skai_pressure_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | small |
| skai.stormcaller vs water_expand_mix | 9 | 9 | 8 | 0.889 | [56.5, 98.0] | small |
| water.reef_runner vs water_expand_mix | 9 | 9 | 8 | 0.889 | [56.5, 98.0] | small |
| skai.roc_rider vs water_leviathan_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.stormcaller vs skai_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.talon_archer vs water_expand_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.thunderhead vs skai_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.grasper vs water_leviathan_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.roc_rider vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.stormcaller vs skai.talon_archer | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.stormcaller vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.stormcaller vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.talon_archer vs skai.thunderhead | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.talon_archer vs water.grasper | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.thunderhead vs skai.windrunner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.thunderhead vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai_expand_mix vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai_expand_mix vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.grasper vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.grasper vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.grasper vs water_pressure_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.ink_lobber vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.shellback vs water_pressure_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| water.spoutling vs water_pressure_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | |
| skai.roc_rider vs skai.stormcaller | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs skai.thunderhead | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.roc_rider vs water_pressure_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.stormcaller vs skai.thunderhead | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.stormcaller vs skai_air_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | small |
| skai.stormcaller vs skai_expand_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.stormcaller vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.stormcaller vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.talon_archer vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.talon_archer vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.talon_archer vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.thunderhead vs skai_air_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| skai.thunderhead vs skai_expand_mix | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | |
| skai.thunderhead vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.thunderhead vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.thunderhead vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.thunderhead vs water_pressure_mix | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | |
| skai.windrunner vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.windrunner vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai.windrunner vs water_expand_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| skai.windrunner vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai_air_mix vs water.grasper | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | small |
| skai_air_mix vs water.ink_lobber | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | small |
| skai_air_mix vs water.leviathan | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | small |
| skai_air_mix vs water.reef_runner | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | small |
| skai_air_mix vs water.spoutling | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | small |
| skai_air_mix vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | small |
| skai_air_mix vs water_pressure_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| skai_expand_mix vs water.grasper | 12 | 12 | 12 | 1.000 | [75.7, 100.0] | |
| skai_expand_mix vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_expand_mix vs water.leviathan | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | |
| skai_expand_mix vs water.shellback | 14 | 4 | 4 | 1.000 | [51.0, 100.0] | small |
| skai_expand_mix vs water_leviathan_mix | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | small |
| skai_expand_mix vs water_pressure_mix | 12 | 12 | 12 | 1.000 | [75.7, 100.0] | |
| skai_pressure_mix vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_pressure_mix vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| skai_pressure_mix vs water_leviathan_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| water.grasper vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.ink_lobber vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.ink_lobber vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.ink_lobber vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.leviathan vs water.reef_runner | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.leviathan vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.leviathan vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.reef_runner vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | |
| water.reef_runner vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.shellback vs water.spoutling | 14 | 13 | 13 | 1.000 | [77.2, 100.0] | |
| water.spoutling vs water_expand_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| water.spoutling vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water_leviathan_mix vs water_pressure_mix | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | small |

## By side (a fight counts for both sides; accuracy ascending)

| side | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| water.shellback | 209 | 183 | 135 | 0.738 | [67.0, 79.6] | |
| skai.windrunner | 209 | 209 | 155 | 0.742 | [67.8, 79.6] | |
| water.reef_runner | 209 | 209 | 161 | 0.770 | [70.9, 82.2] | |
| water_expand_mix | 141 | 141 | 111 | 0.787 | [71.3, 84.7] | |
| water_pressure_mix | 189 | 189 | 150 | 0.794 | [73.0, 84.5] | |
| skai_pressure_mix | 180 | 180 | 143 | 0.794 | [73.0, 84.7] | |
| skai.stormcaller | 203 | 203 | 169 | 0.833 | [77.5, 87.8] | |
| water.grasper | 203 | 203 | 172 | 0.847 | [79.1, 89.0] | |
| water.ink_lobber | 203 | 203 | 174 | 0.857 | [80.2, 89.9] | |
| skai.roc_rider | 209 | 201 | 174 | 0.866 | [81.2, 90.6] | |
| skai.talon_archer | 203 | 203 | 177 | 0.872 | [81.9, 91.1] | |
| skai_air_mix | 134 | 130 | 116 | 0.892 | [82.7, 93.5] | |
| skai.thunderhead | 203 | 200 | 181 | 0.905 | [85.6, 93.8] | |
| skai_expand_mix | 191 | 181 | 164 | 0.906 | [85.5, 94.1] | |
| water_leviathan_mix | 143 | 143 | 131 | 0.916 | [85.9, 95.1] | |
| water.leviathan | 204 | 204 | 191 | 0.936 | [89.4, 96.2] | |
| water.spoutling | 209 | 208 | 202 | 0.971 | [93.9, 98.7] | |

## By terrain case

| case | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| choke | 198 | 194 | 171 | 0.881 | [82.8, 92.0] | |
| flat | 470 | 460 | 399 | 0.867 | [83.3, 89.5] | |
| flat_uneven | 489 | 478 | 438 | 0.916 | [88.8, 93.8] | |
| flooded | 232 | 232 | 175 | 0.754 | [69.5, 80.5] | |
| high_ground | 232 | 231 | 170 | 0.736 | [67.6, 78.9] | |

## By army value

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| 001250 | 396 | 388 | 344 | 0.887 | [85.1, 91.4] | |
| 001750 | 721 | 709 | 608 | 0.858 | [83.0, 88.1] | |
| 002250 | 232 | 232 | 175 | 0.754 | [69.5, 80.5] | |
| 002750 | 272 | 266 | 226 | 0.850 | [80.2, 88.8] | |

## By case and army value

Value and case are confounded: in the committed sweep only flat is played at 750, so the 750 row is a flat row.

| case / army value | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| choke / 001250 | 198 | 194 | 171 | 0.881 | [82.8, 92.0] | |
| flat / 001250 | 198 | 194 | 173 | 0.892 | [84.0, 92.8] | |
| flat / 002750 | 272 | 266 | 226 | 0.850 | [80.2, 88.8] | |
| flat_uneven / 001750 | 489 | 478 | 438 | 0.916 | [88.8, 93.8] | |
| flooded / 002250 | 232 | 232 | 175 | 0.754 | [69.5, 80.5] | |
| high_ground / 001750 | 232 | 231 | 170 | 0.736 | [67.6, 78.9] | |

## By unmodelled mechanic

Present means the mechanic occurs in the fight by its definitions and case (windstorm_range and focus_fire never do: fights run without cycles and with attack-move control). "Only" = it is the only mechanic present.

| mechanic | present: decided | present: accuracy | absent: accuracy | only: decided | only: accuracy |
| --- | ---: | ---: | ---: | ---: | ---: |
| drench | 117 | 0.855 | 0.848 | 0 | - |
| splash | 1010 | 0.876 | 0.800 | 102 | 0.912 |
| momentum | 1171 | 0.849 | 0.847 | 314 | 0.831 |
| flooding | 232 | 0.754 | 0.864 | 12 | 0.833 |
| windstorm_range | 0 | - | 0.848 | 0 | - |
| regeneration | 442 | 0.808 | 0.864 | 47 | 0.830 |
| hit_delay | 0 | - | 0.848 | 0 | - |
| focus_fire | 0 | - | 0.848 | 0 | - |
| clean | 72 | 0.833 | - | - | - |

## Calibration of the predicted margin

Decided fights only. "Winner left" is the lower median of the measured winner's value left. Read: when the predictor names a winner by a ratio in this bin, it wins this often.

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) |
| --- | ---: | ---: | ---: | --- | ---: |
| predicted none | 22 | - | - | - | - |
| 1 to below 1.1 | 180 | 95 | 0.528 | [45.5, 59.9] | 0.368 |
| 1.1 to below 1.25 | 213 | 170 | 0.798 | [73.9, 84.7] | 0.384 |
| 1.25 to below 1.5 | 201 | 161 | 0.801 | [74.0, 85.0] | 0.606 |
| 1.5 to below 2 | 208 | 183 | 0.880 | [82.9, 91.7] | 0.650 |
| 2 to below 3 | 125 | 116 | 0.928 | [86.9, 96.2] | 0.807 |
| 3 and above | 646 | 628 | 0.972 | [95.6, 98.2] | 0.945 |

## No-winner fights

| fight | result | reason | predicted | value left leader | predicted winner |
| --- | --- | --- | --- | --- | --- |
| flat/1250/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat/1250/water.shellback/skai.roc_rider | stalemate | dome_absorbed | none | left1 | none |
| flat/1250/water.shellback/skai_expand_mix | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat/1250/skai_expand_mix/water.shellback | stalemate | dome_absorbed | seat0 | left1 | left0 |
| flat/2750/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat/2750/water.shellback/skai.roc_rider | stalemate | dome_absorbed | none | left1 | none |
| flat/2750/water.shellback/skai.thunderhead | stalemate | dome_absorbed | none | left1 | none |
| flat/2750/water.shellback/skai_expand_mix | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat/2750/skai_expand_mix/water.shellback | stalemate | dome_absorbed | seat0 | left1 | left0 |
| flat/2750/skai_air_mix/water.shellback | stalemate | dome_absorbed | none | left1 | none |
| high_ground/1750/water.spoutling/water.shellback | stalemate | dome_absorbed | seat1 | even | left1 |
| choke/1250/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| choke/1250/water.shellback/skai.roc_rider | stalemate | dome_absorbed | none | left1 | none |
| choke/1250/water.shellback/skai_expand_mix | stalemate | dome_absorbed | seat1 | left0 | left1 |
| choke/1250/skai_expand_mix/water.shellback | stalemate | dome_absorbed | seat0 | left1 | left0 |
| flat_uneven/1750/skai.roc_rider/water.shellback/r12500 | stalemate | dome_absorbed | none | left0 | none |
| flat_uneven/1750/skai.thunderhead/water.shellback/r12500 | stalemate | dome_absorbed | none | left0 | none |
| flat_uneven/1750/water.shellback/skai_expand_mix/r12500 | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat_uneven/1750/skai_expand_mix/water.shellback/r12500 | stalemate | dome_absorbed | seat0 | left1 | left0 |
| flat_uneven/1750/water.shellback/skai_air_mix/r12500 | stalemate | dome_absorbed | none | left1 | none |
| flat_uneven/1750/skai_air_mix/water.shellback/r12500 | stalemate | dome_absorbed | none | left1 | none |
| flat_uneven/1750/skai.roc_rider/water.shellback/r15000 | stalemate | dome_absorbed | none | left0 | none |
| flat_uneven/1750/skai.thunderhead/water.shellback/r15000 | stalemate | dome_absorbed | none | left0 | none |
| flat_uneven/1750/water.shellback/skai_expand_mix/r15000 | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat_uneven/1750/skai_expand_mix/water.shellback/r15000 | stalemate | dome_absorbed | seat0 | left1 | left0 |
| flat_uneven/1750/skai_air_mix/water.shellback/r15000 | stalemate | dome_absorbed | none | left1 | none |

# Subsets of extended records (fight_predictor_v2.md 5.4)

Start predictions use GapTiles = start_distance_tiles, the water of lane_depth_class (Shallow in a flooded case without one) and neutral water; snapshot predictions use GapTiles 0 and the health of the snapshot. Baseline columns: profile `aggressive_s5`.

## Subset E

Start predictions of fights with equal target values (ratio 1).

Headline: 915 of 1117 decided fights = 0.819; baseline 0.705. No-winner fights 15, predicted none for 8; decided fights predicted none 20.

| unordered unit pair (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| skai.windrunner vs water.reef_runner | 10 | 10 | 0 | 0.000 | [0.0, 27.8] | 0.000 |  |
| skai_air_mix vs water.shellback | 4 | 3 | 0 | 0.000 | [0.0, 56.2] | 1.000 | small |
| skai_pressure_mix vs water_pressure_mix | 4 | 4 | 0 | 0.000 | [0.0, 49.0] | 1.000 | small |
| water.grasper vs water_expand_mix | 6 | 6 | 1 | 0.167 | [3.0, 56.4] | 0.667 | small |
| skai.thunderhead vs water.shellback | 10 | 9 | 2 | 0.222 | [6.3, 54.7] | 0.778 | small |
| water_expand_mix vs water_pressure_mix | 4 | 4 | 1 | 0.250 | [4.6, 69.9] | 0.750 | small |
| skai.stormcaller vs skai.windrunner | 10 | 10 | 3 | 0.300 | [10.8, 60.3] | 0.800 |  |
| skai.talon_archer vs skai_pressure_mix | 6 | 6 | 2 | 0.333 | [9.7, 70.0] | 0.333 | small |
| skai_pressure_mix vs water.ink_lobber | 6 | 6 | 2 | 0.333 | [9.7, 70.0] | 0.333 | small |
| skai.stormcaller vs water_pressure_mix | 8 | 8 | 3 | 0.375 | [13.7, 69.4] | 0.625 | small |
| water.ink_lobber vs water_pressure_mix | 8 | 8 | 3 | 0.375 | [13.7, 69.4] | 0.125 | small |
| skai.roc_rider vs skai_pressure_mix | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.400 |  |
| skai.stormcaller vs water.reef_runner | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.600 |  |
| skai.windrunner vs water.shellback | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.900 |  |
| water.reef_runner vs water.shellback | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | 0.900 |  |
| skai.roc_rider vs water.shellback | 10 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| skai.stormcaller vs water.grasper | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | 0.500 |  |
| skai.thunderhead vs water_leviathan_mix | 8 | 8 | 4 | 0.500 | [21.5, 78.5] | 0.000 | small |
| skai.windrunner vs water_pressure_mix | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | 0.900 |  |
| skai_pressure_mix vs water.grasper | 6 | 6 | 3 | 0.500 | [18.8, 81.2] | 0.833 | small |
| water.reef_runner vs water_pressure_mix | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | 0.900 |  |
| skai.stormcaller vs water.ink_lobber | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.800 |  |
| skai.windrunner vs skai_expand_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.600 |  |
| skai.windrunner vs water.ink_lobber | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.100 |  |
| water.ink_lobber vs water.reef_runner | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.100 |  |
| water.leviathan vs water.shellback | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.700 |  |
| skai_air_mix vs water_expand_mix | 8 | 8 | 5 | 0.625 | [30.6, 86.3] | 0.625 | small |
| water.leviathan vs water_leviathan_mix | 8 | 8 | 5 | 0.625 | [30.6, 86.3] | 0.875 | small |
| skai.roc_rider vs skai_air_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.667 | small |
| skai_air_mix vs skai_expand_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.667 | small |
| water.leviathan vs water_expand_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.333 | small |
| water.shellback vs water_expand_mix | 6 | 6 | 4 | 0.667 | [30.0, 90.3] | 0.667 | small |
| skai.roc_rider vs skai_expand_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.700 |  |
| skai.talon_archer vs water.reef_runner | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.700 |  |
| skai.windrunner vs skai_pressure_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.700 |  |
| skai.windrunner vs water.grasper | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.900 |  |
| water.grasper vs water.reef_runner | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.900 |  |
| skai.roc_rider vs water_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.250 | small |
| skai.talon_archer vs skai_expand_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | 0.625 | small |
| skai_expand_mix vs skai_pressure_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| skai_expand_mix vs water_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| water_expand_mix vs water_leviathan_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.250 | small |
| skai.roc_rider vs skai.talon_archer | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.roc_rider vs skai.windrunner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.talon_archer vs skai.windrunner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai.talon_archer vs water.ink_lobber | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.200 |  |
| skai.talon_archer vs water.shellback | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.900 |  |
| skai.thunderhead vs water.leviathan | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| skai_air_mix vs skai_pressure_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.300 |  |
| skai_pressure_mix vs water.reef_runner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.800 |  |
| water.grasper vs water.ink_lobber | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | 0.200 |  |
| skai.stormcaller vs skai_pressure_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.667 | small |
| skai.thunderhead vs skai_pressure_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.167 | small |
| water.ink_lobber vs water_expand_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 0.833 | small |
| skai.roc_rider vs water_leviathan_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 0.125 | small |
| water.grasper vs water_leviathan_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 0.125 | small |
| skai.roc_rider vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.roc_rider vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.stormcaller vs skai.talon_archer | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.800 |  |
| skai.stormcaller vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.stormcaller vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.200 |  |
| skai.talon_archer vs skai.thunderhead | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.100 |  |
| skai.talon_archer vs water.grasper | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.talon_archer vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.thunderhead vs skai.windrunner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.100 |  |
| skai.thunderhead vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai_expand_mix vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai_expand_mix vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai_pressure_mix vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.grasper vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.grasper vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.grasper vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.ink_lobber vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.shellback vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.spoutling vs water_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| skai.roc_rider vs skai.stormcaller | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs skai.thunderhead | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.roc_rider vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs skai.thunderhead | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.stormcaller vs water_expand_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.stormcaller vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| skai.talon_archer vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.000 | small |
| skai.talon_archer vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.100 |  |
| skai.talon_archer vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.talon_archer vs water_expand_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.talon_archer vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 0.250 | small |
| skai.thunderhead vs skai_air_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.thunderhead vs skai_expand_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs water_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.windrunner vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.000 | small |
| skai.windrunner vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.100 |  |
| skai.windrunner vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.windrunner vs water_expand_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai.windrunner vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 0.250 | small |
| skai_air_mix vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai_air_mix vs water_pressure_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.grasper | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| skai_expand_mix vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_expand_mix vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.500 |  |
| skai_expand_mix vs water.shellback | 10 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai_expand_mix vs water_leviathan_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 0.000 | small |
| skai_expand_mix vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.800 |  |
| skai_pressure_mix vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.100 |  |
| skai_pressure_mix vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_pressure_mix vs water_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_pressure_mix vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 0.750 | small |
| water.grasper vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.000 |  |
| water.ink_lobber vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| water.ink_lobber vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.900 |  |
| water.ink_lobber vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| water.leviathan vs water.reef_runner | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.100 |  |
| water.leviathan vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| water.leviathan vs water_pressure_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 0.000 | small |
| water.reef_runner vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| water.reef_runner vs water_expand_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| water.reef_runner vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 0.250 | small |
| water.shellback vs water.spoutling | 10 | 9 | 9 | 1.000 | [70.1, 100.0] | 1.000 | small |
| water.shellback vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| water.spoutling vs water_expand_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 1.000 | small |
| water.spoutling vs water_leviathan_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| water_leviathan_mix vs water_pressure_mix | 6 | 6 | 6 | 1.000 | [61.0, 100.0] | 0.000 | small |

| side (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| skai.windrunner | 148 | 148 | 101 | 0.682 | [60.4, 75.2] | 0.574 |  |
| water.reef_runner | 148 | 148 | 106 | 0.716 | [63.9, 78.3] | 0.669 |  |
| water.shellback | 148 | 133 | 96 | 0.722 | [64.0, 79.1] | 0.857 |  |
| skai_pressure_mix | 120 | 120 | 88 | 0.733 | [64.8, 80.4] | 0.608 |  |
| water_pressure_mix | 132 | 132 | 101 | 0.765 | [68.6, 82.9] | 0.765 |  |
| water_expand_mix | 86 | 86 | 67 | 0.779 | [68.1, 85.4] | 0.767 |  |
| skai.stormcaller | 142 | 142 | 111 | 0.782 | [70.7, 84.2] | 0.796 |  |
| water.ink_lobber | 142 | 142 | 115 | 0.810 | [73.7, 86.6] | 0.648 |  |
| water.grasper | 142 | 142 | 116 | 0.817 | [74.5, 87.2] | 0.725 |  |
| skai_air_mix | 80 | 79 | 67 | 0.848 | [75.3, 91.1] | 0.722 |  |
| skai.roc_rider | 148 | 142 | 121 | 0.852 | [78.4, 90.1] | 0.796 |  |
| skai.talon_archer | 142 | 142 | 121 | 0.852 | [78.4, 90.1] | 0.613 |  |
| skai.thunderhead | 144 | 143 | 126 | 0.881 | [81.8, 92.4] | 0.748 |  |
| skai_expand_mix | 136 | 130 | 115 | 0.885 | [81.8, 92.9] | 0.762 |  |
| water_leviathan_mix | 114 | 114 | 104 | 0.912 | [84.6, 95.2] | 0.491 |  |
| water.leviathan | 144 | 144 | 133 | 0.924 | [86.8, 95.7] | 0.535 |  |
| water.spoutling | 148 | 147 | 142 | 0.966 | [92.3, 98.5] | 0.912 |  |

| case | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| choke | 198 | 194 | 171 | 0.881 | [82.8, 92.0] | 0.727 |  |
| flat | 470 | 460 | 399 | 0.867 | [83.3, 89.5] | 0.720 |  |
| flooded | 232 | 232 | 175 | 0.754 | [69.5, 80.5] | 0.703 |  |
| high_ground | 232 | 231 | 170 | 0.736 | [67.6, 78.9] | 0.662 |  |

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| 001250 | 396 | 388 | 344 | 0.887 | [85.1, 91.4] | 0.722 |  |
| 001750 | 232 | 231 | 170 | 0.736 | [67.6, 78.9] | 0.662 |  |
| 002250 | 232 | 232 | 175 | 0.754 | [69.5, 80.5] | 0.703 |  |
| 002750 | 272 | 266 | 226 | 0.850 | [80.2, 88.8] | 0.722 |  |

Calibration (decided fights, predicted winner by margin bin):

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) | baseline share |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| predicted none | 20 | - | - | - | - | - |
| 1 to below 1.1 | 144 | 71 | 0.493 | [41.3, 57.4] | 0.368 | 0.553 (47/85) |
| 1.1 to below 1.25 | 150 | 114 | 0.760 | [68.6, 82.1] | 0.404 | 0.544 (62/114) |
| 1.25 to below 1.5 | 134 | 101 | 0.754 | [67.4, 81.9] | 0.582 | 0.594 (98/165) |
| 1.5 to below 2 | 141 | 123 | 0.872 | [80.7, 91.8] | 0.627 | 0.689 (115/167) |
| 2 to below 3 | 87 | 78 | 0.897 | [81.5, 94.5] | 0.807 | 0.733 (121/165) |
| 3 and above | 441 | 428 | 0.971 | [95.0, 98.3] | 0.940 | 0.837 (345/412) |

## Subset U

Start predictions of fights where seat 1's target value differs (seat1_value_ratios).

Headline: 438 of 478 decided fights = 0.916; baseline 0.814. No-winner fights 11, predicted none for 7; decided fights predicted none 2.

| unordered unit pair (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| skai.roc_rider vs water.shellback | 4 | 2 | 0 | 0.000 | [0.0, 65.8] | 1.000 | small |
| water.shellback vs water_leviathan_mix | 2 | 2 | 0 | 0.000 | [0.0, 65.8] | 1.000 | small |
| skai.talon_archer vs water.shellback | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.750 | small |
| skai.thunderhead vs water_expand_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| skai.windrunner vs water.shellback | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.750 | small |
| skai.windrunner vs water_pressure_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 1.000 | small |
| skai_pressure_mix vs water_pressure_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 1.000 | small |
| water.leviathan vs water_expand_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.500 | small |
| water.reef_runner vs water.shellback | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 0.750 | small |
| water.reef_runner vs water_pressure_mix | 4 | 4 | 2 | 0.500 | [15.0, 85.0] | 1.000 | small |
| skai.stormcaller vs water_expand_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 0.667 | small |
| skai.talon_archer vs skai_air_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 0.667 | small |
| skai_pressure_mix vs water_expand_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 0.667 | small |
| water.ink_lobber vs water_expand_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 0.333 | small |
| water.reef_runner vs water_expand_mix | 3 | 3 | 2 | 0.667 | [20.8, 93.9] | 0.333 | small |
| skai.roc_rider vs skai.windrunner | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai.roc_rider vs skai_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai.roc_rider vs water.spoutling | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| skai.roc_rider vs water_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.250 | small |
| skai.stormcaller vs water.grasper | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.stormcaller vs water_pressure_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.talon_archer vs water_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.talon_archer vs water_pressure_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai.windrunner vs skai_air_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| skai.windrunner vs water.grasper | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai_expand_mix vs water_expand_mix | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| skai_pressure_mix vs water.grasper | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai_pressure_mix vs water.shellback | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.750 | small |
| water.grasper vs water.ink_lobber | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 0.500 | small |
| water.grasper vs water.reef_runner | 4 | 4 | 3 | 0.750 | [30.1, 95.4] | 1.000 | small |
| skai.roc_rider vs skai.stormcaller | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs skai.talon_archer | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs skai.thunderhead | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs skai_air_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai.roc_rider vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| skai.roc_rider vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.roc_rider vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai.roc_rider vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai.talon_archer | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.stormcaller vs skai.thunderhead | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai.windrunner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.stormcaller vs skai_air_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.stormcaller vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| skai.stormcaller vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.stormcaller vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.stormcaller vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.stormcaller vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai.talon_archer vs skai.thunderhead | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.250 | small |
| skai.talon_archer vs skai.windrunner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| skai.talon_archer vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.talon_archer vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.talon_archer vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.talon_archer vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai.thunderhead vs skai.windrunner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.250 | small |
| skai.thunderhead vs skai_air_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai.thunderhead vs skai_expand_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai.thunderhead vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.thunderhead vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| skai.thunderhead vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.thunderhead vs water.shellback | 4 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai.thunderhead vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| skai.thunderhead vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai.thunderhead vs water_pressure_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai.windrunner vs skai_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.windrunner vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai.windrunner vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai.windrunner vs water_expand_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 0.667 | small |
| skai.windrunner vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai_air_mix vs skai_expand_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai_air_mix vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai_air_mix vs water.grasper | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai_air_mix vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.leviathan | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 0.667 | small |
| skai_air_mix vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.shellback | 4 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| skai_air_mix vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai_air_mix vs water_pressure_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| skai_expand_mix vs skai_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.grasper | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.leviathan | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 0.667 | small |
| skai_expand_mix vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_expand_mix vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| skai_expand_mix vs water_pressure_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| skai_pressure_mix vs water.ink_lobber | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai_pressure_mix vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai_pressure_mix vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_pressure_mix vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_pressure_mix vs water_leviathan_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 1.000 | small |
| water.grasper vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.grasper vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.grasper vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.grasper vs water_expand_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.grasper vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 0.000 | small |
| water.grasper vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.ink_lobber vs water.leviathan | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.750 | small |
| water.ink_lobber vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.ink_lobber vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.ink_lobber vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.ink_lobber vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| water.ink_lobber vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.leviathan vs water.reef_runner | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water.leviathan vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.leviathan vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.leviathan vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| water.leviathan vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.250 | small |
| water.reef_runner vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.reef_runner vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| water.shellback vs water.spoutling | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.shellback vs water_expand_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| water.shellback vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water.spoutling vs water_expand_mix | 3 | 3 | 3 | 1.000 | [43.8, 100.0] | 1.000 | small |
| water.spoutling vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| water.spoutling vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| water_expand_mix vs water_leviathan_mix | 2 | 2 | 2 | 1.000 | [34.2, 100.0] | 1.000 | small |
| water_expand_mix vs water_pressure_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| water_leviathan_mix vs water_pressure_mix | 1 | 1 | 1 | 1.000 | [20.7, 100.0] | 0.000 | small |
| skai_expand_mix vs water.shellback | 4 | 0 | 0 | - | - | - | small |

| side (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| water.shellback | 61 | 50 | 39 | 0.780 | [64.8, 87.2] | 0.920 |  |
| water_expand_mix | 55 | 55 | 44 | 0.800 | [67.6, 88.4] | 0.655 |  |
| water_pressure_mix | 57 | 57 | 49 | 0.860 | [74.7, 92.7] | 0.825 |  |
| skai.windrunner | 61 | 61 | 54 | 0.885 | [78.2, 94.3] | 0.787 |  |
| skai.roc_rider | 61 | 59 | 53 | 0.898 | [79.5, 95.3] | 0.915 |  |
| water.reef_runner | 61 | 61 | 55 | 0.902 | [80.2, 95.4] | 0.852 |  |
| skai_pressure_mix | 60 | 60 | 55 | 0.917 | [81.9, 96.4] | 0.800 |  |
| skai.talon_archer | 61 | 61 | 56 | 0.918 | [82.2, 96.4] | 0.770 |  |
| water.grasper | 61 | 61 | 56 | 0.918 | [82.2, 96.4] | 0.852 |  |
| water_leviathan_mix | 29 | 29 | 27 | 0.931 | [78.0, 98.1] | 0.897 |  |
| skai.stormcaller | 61 | 61 | 58 | 0.951 | [86.5, 98.3] | 0.738 |  |
| skai_air_mix | 54 | 51 | 49 | 0.961 | [86.8, 98.9] | 0.882 |  |
| skai_expand_mix | 55 | 51 | 49 | 0.961 | [86.8, 98.9] | 0.961 |  |
| skai.thunderhead | 59 | 57 | 55 | 0.965 | [88.1, 99.0] | 0.789 |  |
| water.leviathan | 60 | 60 | 58 | 0.967 | [88.6, 99.1] | 0.650 |  |
| water.ink_lobber | 61 | 61 | 59 | 0.967 | [88.8, 99.1] | 0.721 |  |
| water.spoutling | 61 | 61 | 60 | 0.984 | [91.3, 99.7] | 0.902 |  |

| case | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| flat_uneven | 489 | 478 | 438 | 0.916 | [88.8, 93.8] | 0.814 |  |

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| 001750 | 489 | 478 | 438 | 0.916 | [88.8, 93.8] | 0.814 |  |

Calibration (decided fights, predicted winner by margin bin):

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) | baseline share |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| predicted none | 2 | - | - | - | - | - |
| 1 to below 1.1 | 36 | 24 | 0.667 | [50.3, 79.8] | 0.366 | 0.409 (9/22) |
| 1.1 to below 1.25 | 63 | 56 | 0.889 | [78.8, 94.5] | 0.356 | 0.667 (20/30) |
| 1.25 to below 1.5 | 67 | 60 | 0.896 | [80.0, 94.8] | 0.631 | 0.745 (35/47) |
| 1.5 to below 2 | 67 | 60 | 0.896 | [80.0, 94.8] | 0.680 | 0.738 (62/84) |
| 2 to below 3 | 38 | 38 | 1.000 | [90.8, 100.0] | 0.804 | 0.734 (69/94) |
| 3 and above | 205 | 200 | 0.976 | [94.4, 99.0] | 0.949 | 0.965 (194/201) |

## Subset D

Snapshot predictions of damaged armies: the armies when either seat had lost snapshot_loss_fraction of its value, scored against the fight's measured winner.

Headline: 1387 of 1595 decided fights = 0.870; baseline 0.846. No-winner fights 11, predicted none for 0; decided fights predicted none 7.

| unordered unit pair (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| skai.thunderhead vs water_leviathan_mix | 10 | 10 | 1 | 0.100 | [1.8, 40.4] | 0.400 |  |
| water.shellback vs water_leviathan_mix | 10 | 10 | 2 | 0.200 | [5.7, 51.0] | 1.000 |  |
| water.ink_lobber vs water.shellback | 14 | 14 | 3 | 0.214 | [7.6, 47.6] | 0.929 |  |
| water.leviathan vs water_leviathan_mix | 10 | 10 | 3 | 0.300 | [10.8, 60.3] | 1.000 |  |
| skai.stormcaller vs water.shellback | 14 | 14 | 5 | 0.357 | [16.3, 61.2] | 0.929 |  |
| skai.stormcaller vs skai.windrunner | 14 | 14 | 6 | 0.429 | [21.4, 67.4] | 0.786 |  |
| water.leviathan vs water.shellback | 14 | 14 | 6 | 0.429 | [21.4, 67.4] | 0.714 |  |
| water.ink_lobber vs water_expand_mix | 9 | 9 | 4 | 0.444 | [18.9, 73.3] | 0.889 | small |
| water.reef_runner vs water_expand_mix | 9 | 9 | 4 | 0.444 | [18.9, 73.3] | 0.778 | small |
| skai_air_mix vs water_expand_mix | 12 | 12 | 6 | 0.500 | [25.4, 74.6] | 0.333 |  |
| skai.stormcaller vs water_expand_mix | 9 | 9 | 5 | 0.556 | [26.7, 81.1] | 0.889 | small |
| skai.windrunner vs water_expand_mix | 9 | 9 | 5 | 0.556 | [26.7, 81.1] | 0.889 | small |
| skai.stormcaller vs water.reef_runner | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | 0.643 |  |
| skai.talon_archer vs water.reef_runner | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | 0.643 |  |
| skai.windrunner vs water.reef_runner | 14 | 14 | 8 | 0.571 | [32.6, 78.6] | 0.857 |  |
| skai.talon_archer vs water_expand_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | 0.700 |  |
| skai.roc_rider vs water_expand_mix | 8 | 8 | 5 | 0.625 | [30.6, 86.3] | 0.250 | small |
| skai_expand_mix vs water_expand_mix | 8 | 8 | 5 | 0.625 | [30.6, 86.3] | 0.625 | small |
| skai.talon_archer vs skai_pressure_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.900 |  |
| skai_pressure_mix vs water.ink_lobber | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | 0.600 |  |
| skai.stormcaller vs water.grasper | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | 0.500 |  |
| skai_pressure_mix vs water_expand_mix | 7 | 7 | 5 | 0.714 | [35.9, 91.8] | 1.000 | small |
| water.grasper vs water.ink_lobber | 14 | 14 | 10 | 0.714 | [45.4, 88.3] | 0.357 |  |
| skai.thunderhead vs water_expand_mix | 8 | 8 | 6 | 0.750 | [40.9, 92.9] | 0.750 | small |
| skai.roc_rider vs skai_air_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | 0.778 | small |
| water.shellback vs water_expand_mix | 9 | 9 | 7 | 0.778 | [45.3, 93.7] | 1.000 | small |
| skai.windrunner vs skai_expand_mix | 14 | 14 | 11 | 0.786 | [52.4, 92.4] | 0.786 |  |
| skai.thunderhead vs water.shellback | 11 | 11 | 9 | 0.818 | [52.3, 94.9] | 0.818 |  |
| skai.talon_archer vs skai_expand_mix | 12 | 12 | 10 | 0.833 | [55.2, 95.3] | 0.917 |  |
| water.ink_lobber vs water_pressure_mix | 12 | 12 | 10 | 0.833 | [55.2, 95.3] | 0.250 |  |
| water_expand_mix vs water_leviathan_mix | 6 | 6 | 5 | 0.833 | [43.6, 97.0] | 1.000 | small |
| skai.roc_rider vs skai_expand_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| skai.roc_rider vs skai_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| skai.roc_rider vs water.spoutling | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| skai.stormcaller vs skai.talon_archer | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.714 |  |
| skai.talon_archer vs skai.windrunner | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.643 |  |
| skai.talon_archer vs water.ink_lobber | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.357 |  |
| skai.talon_archer vs water_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.857 |  |
| skai.thunderhead vs water.leviathan | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.786 |  |
| skai.windrunner vs skai_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.786 |  |
| skai.windrunner vs water.shellback | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| skai.windrunner vs water_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.857 |  |
| skai_pressure_mix vs water.reef_runner | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.857 |  |
| skai_pressure_mix vs water.shellback | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| water.reef_runner vs water.shellback | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| water.reef_runner vs water_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.857 |  |
| water.shellback vs water_pressure_mix | 14 | 14 | 12 | 0.857 | [60.1, 96.0] | 0.929 |  |
| skai_air_mix vs skai_expand_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 1.000 | small |
| skai_expand_mix vs skai_pressure_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 0.875 | small |
| skai_pressure_mix vs water_pressure_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 0.875 | small |
| water_expand_mix vs water_pressure_mix | 8 | 8 | 7 | 0.875 | [52.9, 97.8] | 0.875 | small |
| skai.roc_rider vs water_leviathan_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.700 |  |
| skai.thunderhead vs skai_pressure_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.600 |  |
| skai_pressure_mix vs water.grasper | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.900 |  |
| water.grasper vs water_expand_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.800 |  |
| water.grasper vs water_leviathan_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.300 |  |
| water.leviathan vs water_expand_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | 0.700 |  |
| skai.stormcaller vs water_pressure_mix | 12 | 12 | 11 | 0.917 | [64.6, 98.5] | 0.583 |  |
| skai.roc_rider vs skai.talon_archer | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.roc_rider vs skai.windrunner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.roc_rider vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.stormcaller vs water.ink_lobber | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.stormcaller vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.857 |  |
| skai.talon_archer vs skai.thunderhead | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.429 |  |
| skai.talon_archer vs water.grasper | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.talon_archer vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.thunderhead vs skai.windrunner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.571 |  |
| skai.thunderhead vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.windrunner vs water.grasper | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.windrunner vs water.ink_lobber | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.357 |  |
| skai_air_mix vs skai_pressure_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.643 |  |
| skai_expand_mix vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai_expand_mix vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.grasper vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.grasper vs water.shellback | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.grasper vs water.spoutling | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.grasper vs water_pressure_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| water.ink_lobber vs water.reef_runner | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.357 |  |
| water.spoutling vs water_pressure_mix | 14 | 14 | 13 | 0.929 | [68.5, 98.7] | 0.929 |  |
| skai.roc_rider vs skai.stormcaller | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs skai.thunderhead | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.roc_rider vs water.shellback | 7 | 6 | 6 | 1.000 | [61.0, 100.0] | 0.667 | small |
| skai.roc_rider vs water_pressure_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.stormcaller vs skai.thunderhead | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.stormcaller vs skai_air_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| skai.stormcaller vs skai_expand_mix | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.stormcaller vs skai_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.700 |  |
| skai.stormcaller vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.stormcaller vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.talon_archer vs skai_air_mix | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | 0.714 | small |
| skai.talon_archer vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.857 |  |
| skai.talon_archer vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.talon_archer vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai.thunderhead vs skai_air_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | 1.000 | small |
| skai.thunderhead vs skai_expand_mix | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | 1.000 |  |
| skai.thunderhead vs water.grasper | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.thunderhead vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.thunderhead vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.thunderhead vs water_pressure_mix | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | 1.000 |  |
| skai.windrunner vs skai_air_mix | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 0.875 | small |
| skai.windrunner vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| skai.windrunner vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai.windrunner vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| skai_air_mix vs water.grasper | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | 1.000 | small |
| skai_air_mix vs water.ink_lobber | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| skai_air_mix vs water.leviathan | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | 1.000 | small |
| skai_air_mix vs water.reef_runner | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| skai_air_mix vs water.shellback | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water.spoutling | 8 | 8 | 8 | 1.000 | [67.6, 100.0] | 1.000 | small |
| skai_air_mix vs water_leviathan_mix | 4 | 4 | 4 | 1.000 | [51.0, 100.0] | 1.000 | small |
| skai_air_mix vs water_pressure_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | 1.000 | small |
| skai_expand_mix vs water.grasper | 12 | 12 | 12 | 1.000 | [75.7, 100.0] | 1.000 |  |
| skai_expand_mix vs water.ink_lobber | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai_expand_mix vs water.leviathan | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | 1.000 |  |
| skai_expand_mix vs water.shellback | 14 | 4 | 4 | 1.000 | [51.0, 100.0] | 0.500 | small |
| skai_expand_mix vs water_leviathan_mix | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | 1.000 | small |
| skai_expand_mix vs water_pressure_mix | 12 | 12 | 12 | 1.000 | [75.7, 100.0] | 0.833 |  |
| skai_pressure_mix vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| skai_pressure_mix vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| skai_pressure_mix vs water_leviathan_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | 1.000 | small |
| water.grasper vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.571 |  |
| water.ink_lobber vs water.leviathan | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.857 |  |
| water.ink_lobber vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| water.ink_lobber vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| water.leviathan vs water.reef_runner | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 0.929 |  |
| water.leviathan vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| water.leviathan vs water_pressure_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 0.900 |  |
| water.reef_runner vs water.spoutling | 14 | 14 | 14 | 1.000 | [78.5, 100.0] | 1.000 |  |
| water.reef_runner vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| water.shellback vs water.spoutling | 13 | 13 | 13 | 1.000 | [77.2, 100.0] | 1.000 |  |
| water.spoutling vs water_expand_mix | 9 | 9 | 9 | 1.000 | [70.1, 100.0] | 1.000 | small |
| water.spoutling vs water_leviathan_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | 1.000 |  |
| water_leviathan_mix vs water_pressure_mix | 7 | 7 | 7 | 1.000 | [64.6, 100.0] | 0.143 | small |

| side (accuracy ascending) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| water_expand_mix | 141 | 141 | 97 | 0.688 | [60.7, 75.9] | 0.766 |  |
| water.shellback | 194 | 183 | 133 | 0.727 | [65.8, 78.6] | 0.902 |  |
| water_leviathan_mix | 143 | 143 | 116 | 0.811 | [73.9, 86.7] | 0.846 |  |
| skai.stormcaller | 203 | 203 | 167 | 0.823 | [76.4, 86.9] | 0.842 |  |
| water.reef_runner | 209 | 209 | 175 | 0.837 | [78.1, 88.1] | 0.842 |  |
| skai.windrunner | 209 | 209 | 176 | 0.842 | [78.7, 88.5] | 0.813 |  |
| water.ink_lobber | 203 | 203 | 173 | 0.852 | [79.7, 89.4] | 0.729 |  |
| skai.talon_archer | 203 | 203 | 176 | 0.867 | [81.3, 90.7] | 0.778 |  |
| skai_pressure_mix | 180 | 180 | 159 | 0.883 | [82.8, 92.2] | 0.844 |  |
| skai.thunderhead | 200 | 200 | 181 | 0.905 | [85.6, 93.8] | 0.840 |  |
| water.leviathan | 204 | 204 | 186 | 0.912 | [86.5, 94.3] | 0.882 |  |
| water.grasper | 203 | 203 | 186 | 0.916 | [87.0, 94.7] | 0.813 |  |
| water_pressure_mix | 189 | 189 | 174 | 0.921 | [87.3, 95.1] | 0.820 |  |
| skai_expand_mix | 191 | 181 | 167 | 0.923 | [87.4, 95.3] | 0.917 |  |
| skai_air_mix | 130 | 130 | 120 | 0.923 | [86.4, 95.8] | 0.862 |  |
| skai.roc_rider | 202 | 201 | 186 | 0.925 | [88.1, 95.4] | 0.905 |  |
| water.spoutling | 208 | 208 | 202 | 0.971 | [93.9, 98.7] | 0.966 |  |

| case | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| choke | 196 | 194 | 168 | 0.866 | [81.1, 90.7] | 0.851 |  |
| flat | 465 | 460 | 402 | 0.874 | [84.0, 90.1] | 0.850 |  |
| flat_uneven | 482 | 478 | 439 | 0.918 | [89.0, 94.0] | 0.897 |  |
| flooded | 232 | 232 | 196 | 0.845 | [79.3, 88.6] | 0.819 |  |
| high_ground | 231 | 231 | 182 | 0.788 | [73.1, 83.6] | 0.753 |  |

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | baseline accuracy | |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| 001250 | 392 | 388 | 339 | 0.874 | [83.7, 90.3] | 0.856 |  |
| 001750 | 713 | 709 | 621 | 0.876 | [85.0, 89.8] | 0.850 |  |
| 002250 | 232 | 232 | 196 | 0.845 | [79.3, 88.6] | 0.819 |  |
| 002750 | 269 | 266 | 231 | 0.868 | [82.3, 90.4] | 0.842 |  |

Calibration (decided fights, predicted winner by margin bin):

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) | baseline share |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| predicted none | 7 | - | - | - | - | - |
| 1 to below 1.1 | 212 | 131 | 0.618 | [55.1, 68.1] | 0.400 | 0.494 (41/83) |
| 1.1 to below 1.25 | 249 | 197 | 0.791 | [73.6, 83.7] | 0.471 | 0.658 (75/114) |
| 1.25 to below 1.5 | 222 | 177 | 0.797 | [74.0, 84.5] | 0.607 | 0.765 (124/162) |
| 1.5 to below 2 | 244 | 225 | 0.922 | [88.2, 95.0] | 0.661 | 0.725 (153/211) |
| 2 to below 3 | 195 | 192 | 0.985 | [95.6, 99.5] | 0.807 | 0.877 (272/310) |
| 3 and above | 466 | 465 | 0.998 | [98.8, 100.0] | 0.991 | 0.957 (684/715) |

## Bars

| ID | bar | measured | result | reason |
| --- | --- | --- | --- | --- |
| B1 | accuracy on E >= 0.80 and >= baseline + 0.05 | 0.819 (915/1117), baseline 0.705 (788/1117) | PASS |  |
| B2 | no bin with >= 10 decided fights wins less than a lower bin by more than 0.10 | no inversion | PASS |  |
| B3 | predicted winner wins >= 0.70 of decided fights with margin in [1.3, 1.5) | 0.796 (113/142) | PASS |  |
| B4 | no-winner fights predicted none >= 0.50 (judged from 5 fights); decided fights predicted none <= 0.02 | none predicted for 15/26 no-winner fights; 22/1595 decided fights | PASS |  |
| B5 | every pair of two single units in E with >= 8 decided fights has accuracy >= 0.40 | 54 pairs judged, 3 below; worst skai.windrunner vs water.reef_runner 0.000 (0/10) | FAIL |  |
| B6 | accuracy on D >= 0.75 and >= baseline's | 0.870 (1387/1595), baseline 0.846 (1349/1595) | PASS |  |
| B7 | accuracy on U >= baseline's | 0.916 (438/478), baseline 0.814 (389/478) | PASS |  |
| C1 | mean wall time per Predict call <= 1000 microseconds | stderr only (timing is never written to a file) | STDERR | the coordinator reads the stderr line; not counted in bars= |


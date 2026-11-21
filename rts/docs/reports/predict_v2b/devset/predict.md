<!-- predict.md: written by the balance harness --predict-fights (rts/tools/BalanceHarness/PredictRunner.cs); not a source file. -->
# Fight predictor against measured fights

- Predictor: FightPredictor.Predict with the predictor_* numbers of profile `aggressive_s5_fp2`; AI thresholds of that profile: attack_min_advantage 1.3, retreat_max_advantage 0.7.
- Fights: read from `rts/docs/reports/fights_2026-11-18/fights.jsonl` (the records may come from other data than the data below); current data digest: tuning a020a25c7d7c2eae, content 9cb28f0929df518c.
- Side A is seat 0; A shoots uphill only in the high_ground case. Predicted class none = both times equal (this includes both at the longest time). Margin = longer time / max(shorter time, min seconds), capped.
- Decided fights (result seat0 or seat1) give the accuracy; a predicted none on a decided fight is wrong. Stalemates, unresolved fights and draws are not in the accuracy: they agree when the predictor says none.
- Mismatched fights (start values too far apart, 0 here) are left out of every table.
- Intervals are Wilson at z = 1.96 over decided fights; a row with fewer than 10 decided fights is marked small.

**PREDICT fights=780 judged=780 decided=765 correct=652 accuracy=0.852 clean=22/24 no_winner_agree=6/15 decision=FIX_FORMULA (proposed thresholds)**

## Decision (proposed rule, C.7)

| Threshold | Proposed | Measured | Fired |
| --- | ---: | ---: | --- |
| simulate_below_clean_accuracy | 0.85 | 0.917 (22/24) | no |
| simulate_below_accuracy | 0.75 | 0.852 (652/765) | no |
| calibration_max_inversion | 0.10 | no inversion among bins with at least 10 fights | no |
| mechanic_min_fights | 20 | decided fights a mechanic needs to be judged | - |
| fix_min_mechanic_accuracy | 0.70 | lowest judged mechanic: flooding 0.742 (115/155) | - |
| fix_min_accuracy | 0.85 | - | - |

**Decision: FIX_FORMULA** (all thresholds are proposed placeholders for the developer to accept or change)

- rule 2: every judged mechanic is at or above 0.70; the mechanics below 0.85 are listed to add to the formula
- Listed mechanics: drench, momentum, flooding, regeneration

## Overall

| group | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| all scored fights | 780 | 765 | 652 | 0.852 | [82.5, 87.6] | |
| clean (no unmodelled mechanic present) | 24 | 24 | 22 | 0.917 | [74.2, 97.7] | |

No-winner fights: 15, of which the predictor said none for 6. Decided fights predicted none (counted wrong): 16 (seat 0 won 7).

Seat order: 381 pairs of decided fights differ only in seat order; in 367 of them the predictor names the same side in both orders (classes are mirror images), and in 57 of those the measured winner is a different side in the two orders (a seat split the predictor cannot see).

## By unordered unit pair (accuracy ascending)

| pair | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| skai.windrunner vs water.reef_runner | 10 | 10 | 0 | 0.000 | [0.0, 27.8] | |
| skai.roc_rider vs skai.talon_archer | 10 | 10 | 1 | 0.100 | [1.8, 40.4] | |
| skai.stormcaller vs water.reef_runner | 10 | 10 | 2 | 0.200 | [5.7, 51.0] | |
| skai.roc_rider vs water_ai_mix | 10 | 10 | 3 | 0.300 | [10.8, 60.3] | |
| skai.stormcaller vs water.grasper | 10 | 10 | 3 | 0.300 | [10.8, 60.3] | |
| skai.thunderhead vs water.shellback | 10 | 10 | 4 | 0.400 | [16.8, 68.7] | |
| skai.roc_rider vs water.shellback | 10 | 4 | 2 | 0.500 | [15.0, 85.0] | small |
| water.grasper vs water_ai_mix | 10 | 10 | 5 | 0.500 | [23.7, 76.3] | |
| skai.talon_archer vs skai_ai_mix | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| skai.talon_archer vs water.ink_lobber | 10 | 10 | 6 | 0.600 | [31.3, 83.2] | |
| water.shellback vs water_ai_mix | 10 | 10 | 7 | 0.700 | [39.7, 89.2] | |
| skai.roc_rider vs skai.windrunner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.roc_rider vs skai_ai_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.stormcaller vs water.ink_lobber | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.talon_archer vs skai.windrunner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.talon_archer vs water.reef_runner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.thunderhead vs water.leviathan | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai_ai_mix vs water_ai_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| water.grasper vs water.ink_lobber | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| water.ink_lobber vs water.reef_runner | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| water.ink_lobber vs water_ai_mix | 10 | 10 | 8 | 0.800 | [49.0, 94.3] | |
| skai.roc_rider vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.roc_rider vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.stormcaller vs skai.talon_archer | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.stormcaller vs skai.windrunner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.stormcaller vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.stormcaller vs water_ai_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.talon_archer vs skai.thunderhead | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.talon_archer vs water.grasper | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.talon_archer vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.talon_archer vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.talon_archer vs water_ai_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.thunderhead vs skai.windrunner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.thunderhead vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.thunderhead vs water_ai_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.windrunner vs skai_ai_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.windrunner vs water.grasper | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.windrunner vs water.ink_lobber | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.windrunner vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.windrunner vs water_ai_mix | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai_ai_mix vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai_ai_mix vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.grasper vs water.reef_runner | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.grasper vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.grasper vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.ink_lobber vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.ink_lobber vs water.spoutling | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.leviathan vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| water.reef_runner vs water.shellback | 10 | 10 | 9 | 0.900 | [59.6, 98.2] | |
| skai.roc_rider vs skai.stormcaller | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.roc_rider vs skai.thunderhead | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.roc_rider vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.roc_rider vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.roc_rider vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.stormcaller vs skai.thunderhead | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.stormcaller vs skai_ai_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.stormcaller vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.stormcaller vs water.shellback | 10 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| skai.talon_archer vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.thunderhead vs skai_ai_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.thunderhead vs water.grasper | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.thunderhead vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.thunderhead vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.windrunner vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai.windrunner vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai_ai_mix vs water.grasper | 10 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| skai_ai_mix vs water.ink_lobber | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai_ai_mix vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| skai_ai_mix vs water.shellback | 10 | 4 | 4 | 1.000 | [51.0, 100.0] | small |
| water.grasper vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.ink_lobber vs water.leviathan | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.leviathan vs water.reef_runner | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.leviathan vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.leviathan vs water_ai_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.reef_runner vs water.spoutling | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.reef_runner vs water_ai_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |
| water.shellback vs water.spoutling | 10 | 9 | 9 | 1.000 | [70.1, 100.0] | small |
| water.spoutling vs water_ai_mix | 10 | 10 | 10 | 1.000 | [72.2, 100.0] | |

## By side (a fight counts for both sides; accuracy ascending)

| side | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| skai.talon_archer | 120 | 120 | 93 | 0.775 | [69.2, 84.1] | |
| water.reef_runner | 120 | 120 | 93 | 0.775 | [69.2, 84.1] | |
| skai.roc_rider | 120 | 114 | 90 | 0.789 | [70.6, 85.4] | |
| water_ai_mix | 120 | 120 | 97 | 0.808 | [72.9, 86.9] | |
| skai.stormcaller | 120 | 119 | 98 | 0.824 | [74.5, 88.2] | |
| skai.windrunner | 120 | 120 | 99 | 0.825 | [74.7, 88.3] | |
| water.shellback | 120 | 106 | 89 | 0.840 | [75.8, 89.7] | |
| water.grasper | 120 | 119 | 100 | 0.840 | [76.4, 89.5] | |
| water.ink_lobber | 120 | 120 | 105 | 0.875 | [80.4, 92.3] | |
| skai.thunderhead | 120 | 120 | 108 | 0.900 | [83.3, 94.2] | |
| skai_ai_mix | 120 | 113 | 102 | 0.903 | [83.4, 94.5] | |
| water.spoutling | 120 | 119 | 113 | 0.950 | [89.4, 97.7] | |
| water.leviathan | 120 | 120 | 117 | 0.975 | [92.9, 99.1] | |

## By terrain case

| case | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| choke | 156 | 152 | 138 | 0.908 | [85.1, 94.4] | |
| flat | 312 | 304 | 279 | 0.918 | [88.1, 94.4] | |
| flooded | 156 | 155 | 115 | 0.742 | [66.8, 80.4] | |
| high_ground | 156 | 154 | 120 | 0.779 | [70.7, 83.7] | |

## By army value

| army value (zero padded) | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| 000750 | 156 | 152 | 138 | 0.908 | [85.1, 94.4] | |
| 001500 | 624 | 613 | 514 | 0.838 | [80.7, 86.6] | |

## By case and army value

Value and case are confounded: in the committed sweep only flat is played at 750, so the 750 row is a flat row.

| case / army value | fights | decided | correct | accuracy | Wilson interval (%) | |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| choke / 001500 | 156 | 152 | 138 | 0.908 | [85.1, 94.4] | |
| flat / 000750 | 156 | 152 | 138 | 0.908 | [85.1, 94.4] | |
| flat / 001500 | 156 | 152 | 141 | 0.928 | [87.5, 95.9] | |
| flooded / 001500 | 156 | 155 | 115 | 0.742 | [66.8, 80.4] | |
| high_ground / 001500 | 156 | 154 | 120 | 0.779 | [70.7, 83.7] | |

## By unmodelled mechanic

Present means the mechanic occurs in the fight by its definitions and case (windstorm_range and focus_fire never do: fights run without cycles and with attack-move control). "Only" = it is the only mechanic present.

| mechanic | present: decided | present: accuracy | absent: accuracy | only: decided | only: accuracy |
| --- | ---: | ---: | ---: | ---: | ---: |
| drench | 60 | 0.800 | 0.857 | 0 | - |
| splash | 562 | 0.867 | 0.813 | 56 | 0.982 |
| momentum | 556 | 0.835 | 0.900 | 88 | 0.761 |
| flooding | 155 | 0.742 | 0.880 | 6 | 1.000 |
| windstorm_range | 0 | - | 0.852 | 0 | - |
| regeneration | 216 | 0.829 | 0.862 | 23 | 0.913 |
| hit_delay | 0 | - | 0.852 | 0 | - |
| focus_fire | 0 | - | 0.852 | 0 | - |
| clean | 24 | 0.917 | - | - | - |

## Calibration of the predicted margin

Decided fights only. "Winner left" is the lower median of the measured winner's value left. Read: when the predictor names a winner by a ratio in this bin, it wins this often.

| bin (margin) | fights | predicted winner won | share | Wilson interval (%) | winner left (median) |
| --- | ---: | ---: | ---: | --- | ---: |
| predicted none | 16 | - | - | - | - |
| 1 to below 1.1 | 93 | 75 | 0.806 | [71.5, 87.4] | 0.270 |
| 1.1 to below 1.25 | 106 | 81 | 0.764 | [67.5, 83.5] | 0.598 |
| 1.25 to below 1.5 | 102 | 82 | 0.804 | [71.6, 86.9] | 0.472 |
| 1.5 to below 2 | 65 | 51 | 0.785 | [67.0, 86.7] | 0.655 |
| 2 to below 3 | 81 | 77 | 0.951 | [88.0, 98.1] | 0.775 |
| 3 and above | 302 | 286 | 0.947 | [91.6, 96.7] | 0.986 |

## No-winner fights

| fight | result | reason | predicted | value left leader | predicted winner |
| --- | --- | --- | --- | --- | --- |
| flat/750/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat/750/water.shellback/skai.roc_rider | stalemate | dome_absorbed | none | left1 | none |
| flat/750/water.shellback/skai_ai_mix | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat/750/skai_ai_mix/water.shellback | stalemate | dome_absorbed | seat0 | left1 | left0 |
| flat/1500/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| flat/1500/water.shellback/skai.roc_rider | unresolved | time_cap | none | left1 | none |
| flat/1500/water.shellback/skai_ai_mix | stalemate | dome_absorbed | seat1 | left0 | left1 |
| flat/1500/skai_ai_mix/water.shellback | stalemate | dome_absorbed | seat0 | left1 | left0 |
| high_ground/1500/skai.stormcaller/water.shellback | stalemate | dome_absorbed | seat1 | even | left1 |
| high_ground/1500/water.spoutling/water.shellback | stalemate | dome_absorbed | seat1 | left1 | left1 |
| flooded/1500/skai_ai_mix/water.grasper | stalemate | no_damage | seat0 | left0 | left0 |
| choke/1500/skai.roc_rider/water.shellback | stalemate | dome_absorbed | none | left0 | none |
| choke/1500/water.shellback/skai.roc_rider | unresolved | time_cap | none | left1 | none |
| choke/1500/water.shellback/skai_ai_mix | stalemate | dome_absorbed | seat1 | left0 | left1 |
| choke/1500/skai_ai_mix/water.shellback | stalemate | dome_absorbed | seat0 | left1 | left0 |

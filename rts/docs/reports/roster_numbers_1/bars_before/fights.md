<!-- fights.md: written by the balance harness --fights (rts/tools/BalanceHarness/FightCoordinator.cs); not a source file. -->
# Equal-value fights

- Data digest: tuning a020a25c7d7c2eae, content af1c217742fb39ea.
- Base seed 20261118; fight seed = SeedDerivation.GameSeed(base seed, "fights", fight ID, 0).
- Value: cost_common + cost_rare per unit; an army is the recipe repeated k = max(1, (2V + rv) / (2 rv)) times.
- Control: plain attack-move at the enemy front rank, idle units re-target every 2 s; no strategic AI, no HQ.
- Fights: 1092 (14 sides); determinism spot check: 2 fights re-run in-process, 0 mismatches.

**FIGHTS fights=1092 decided=1078 draws=4 stalemates=9 unresolved=1 mismatched=166 checks_passed=2 of=8 dome_absorbed=9**

## Design-rule checks

| Check | Rule | Threshold | Result | Detail |
| --- | --- | --- | --- | --- |
| rule1_no_immunity | P-005 1.6 rule 1 | >= 2 mobile and >= 1 static answers per enemy unit or HQ; every armed unit hits Ground | PASS | 13 targets checked |
| rule2_multiplier_band | P-005 1.6 rule 2 | multipliers in [0.5, 1.5]; ratio within a damage type <= 2 | FAIL | drench/structure 0.25 below 0.5<br>drench ratio 4.00 above 2 |
| rule3_counter_margin | P-005 1.6 rule 3 | counter keeps >= 0.2 value, exchange ratio <= 3 | FAIL | skai.roc_rider loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 12.5 above 3<br>skai.roc_rider loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 12.5 above 3<br>skai.stormcaller loses to water.shellback on flat: water.shellback in seat 0 exchange ratio 57.1 above 3<br>skai.stormcaller loses to water.shellback on flat: water.shellback in seat 1 exchange ratio 19.0 above 3<br>skai.talon_archer loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 48.2 above 3<br>skai.talon_archer loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 48.2 above 3<br>skai.thunderhead loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>skai.thunderhead loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>skai.windrunner loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 92.1 above 3<br>skai.windrunner loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 92.1 above 3<br>water.grasper loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.grasper loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.ink_lobber loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.ink_lobber loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.leviathan loses to water.shellback on flat: water.shellback does not beat it (seat 0 win (left 0.31), seat 1 loss (left 0.00))<br>water.reef_runner loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.reef_runner loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.shellback loses to skai.thunderhead on flat: skai.thunderhead in seat 0 exchange ratio inf above 3<br>water.shellback loses to skai.thunderhead on flat: skai.thunderhead in seat 1 exchange ratio inf above 3<br>water.spoutling loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio inf above 3<br>water.spoutling loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio inf above 3 |
| rule4_no_dominant_unit | P-005 1.6 rule 4 | no unit beats all its faction mates on flat | PASS | no dominant unit |
| rule4_every_unit_wins | P-005 1.6 rule 4 | every unit beats >= 1 unit of another faction on flat | FAIL | water.spoutling beats no unit of another faction on flat |
| rule5_two_answers | P-005 1.6 rule 5 | >= 1 unit and >= 1 terrain answers per threat and faction | FAIL | air (skai.roc_rider) against skai: 2 unit answers (skai.talon_archer, skai.windrunner), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only)<br>air (skai.roc_rider) against water: 1 unit answers (water.leviathan), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only)<br>massed_armour (water.grasper) against skai: 2 unit answers (skai.roc_rider, skai.thunderhead), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only)<br>massed_armour (water.grasper) against water: 2 unit answers (water.ink_lobber, water.leviathan), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only)<br>massed_light (skai.windrunner) against skai: 1 unit answers (skai.thunderhead), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only)<br>massed_light (skai.windrunner) against water: 4 unit answers (water.grasper, water.ink_lobber, water.leviathan, water.shellback), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only) |
| rule6_hq_floor | P-005 1.6 rule 6 | Rooted HQ lives >= 15.0 s against 30 supply | FAIL | skai skai.stormcaller x10 vs skai.great_aerie: 11.0 s, needs >= 15.0 s<br>skai skai.stormcaller x10 vs water.grand_shell: 13.5 s, needs >= 15.0 s<br>skai skai.thunderhead x7 vs skai.great_aerie: 8.4 s, needs >= 15.0 s<br>skai skai.thunderhead x7 vs water.grand_shell: 10.4 s, needs >= 15.0 s<br>skai skai.windrunner x30 vs skai.great_aerie: 13.9 s, needs >= 15.0 s<br>water water.ink_lobber x15 vs skai.great_aerie: 6.6 s, needs >= 15.0 s<br>water water.ink_lobber x15 vs water.grand_shell: 7.6 s, needs >= 15.0 s<br>water water.leviathan x7 vs skai.great_aerie: 3.0 s, needs >= 15.0 s<br>water water.leviathan x7 vs water.grand_shell: 3.0 s, needs >= 15.0 s<br>water water.reef_runner x30 vs skai.great_aerie: 14.2 s, needs >= 15.0 s |
| matchups_agree | P-005 developer decision 1 (rule 7) | every unit has an entry; wins and loses match the fights | FAIL | water.leviathan: claims it loses to water.shellback on flat; measured seat 0 win (left 0.28), seat 1 loss (left 0.00)<br>water.spoutling: wins is null, but every armed unit must claim a fight it wins |

## Value exchange: flat, 375

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_early | water_early | water_rs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | +0.03 | – | -0.20 | +1.00 | +1.00 | -0.75 | +1.00 | +1.00 | +0.69 | -0.25 | – | – |
| skai.stormcaller | -1.00 | · | -0.35 | – | -0.38 | -0.37 | -0.25 | -0.67 | -0.15 | -0.78 | +0.61 | -0.57 | – | – |
| skai.talon_archer | -0.09 | +0.35 | · | – | +0.27 | -0.56 | +0.15 | -0.64 | +0.30 | -0.82 | +0.82 | +0.01 | – | – |
| skai.thunderhead | – | – | – | · | – | – | – | – | – | – | – | – | – | – |
| skai.windrunner | +0.20 | +0.38 | -0.25 | – | · | -0.66 | +0.22 | -0.88 | +0.00 | -0.82 | +0.80 | -0.17 | – | – |
| water.grasper | -1.00 | +0.40 | +0.50 | – | +0.63 | · | +0.13 | -0.75 | +0.66 | +0.63 | +0.84 | +0.59 | – | – |
| water.ink_lobber | -1.00 | +0.25 | -0.15 | – | -0.22 | -0.01 | · | -0.66 | -0.29 | -0.78 | +0.58 | -0.49 | – | – |
| water.leviathan | +0.75 | +0.67 | +0.64 | – | +0.88 | +0.72 | +0.66 | · | +0.89 | -0.36 | +0.81 | +0.74 | – | – |
| water.reef_runner | -1.00 | +0.28 | -0.32 | – | +0.00 | -0.68 | +0.29 | -0.89 | · | -0.89 | +0.75 | -0.24 | – | – |
| water.shellback | -1.00 | +1.00 | +0.80 | – | +0.82 | -0.63 | +1.00 | +0.36 | +0.89 | · | +0.98 | – | – | – |
| water.spoutling | -0.69 | -0.61 | -0.82 | – | -0.80 | -0.83 | -0.58 | -0.81 | -0.75 | -0.98 | · | -0.82 | – | – |
| skai_early | +0.20 | +0.54 | -0.07 | – | +0.30 | -0.59 | +0.47 | -0.69 | +0.30 | – | +0.79 | · | -0.88 | -0.78 |
| water_early | – | – | – | – | – | – | – | – | – | – | – | +0.79 | · | +0.85 |
| water_rs | – | – | – | – | – | – | – | – | – | – | – | +0.68 | -0.86 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: flat, 525

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_early | water_early | water_rs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | – | +1.00 | +0.09 | – | +1.00 | – | +1.00 | S | – | – | – | – |
| skai.stormcaller | -1.00 | · | -0.38 | -1.00 | -0.32 | -0.12 | -0.17 | – | -0.25 | -0.94 | +0.74 | – | – | – |
| skai.talon_archer | – | +0.38 | · | -0.56 | -0.27 | -0.55 | +0.30 | – | -0.24 | -0.94 | +0.80 | – | -0.74 | -0.47 |
| skai.thunderhead | -1.00 | +1.00 | +0.56 | · | +0.63 | +1.00 | +1.00 | – | +1.00 | +1.00 | +0.79 | – | +1.00 | +1.00 |
| skai.windrunner | -0.09 | +0.32 | +0.27 | -0.63 | · | -0.57 | -0.34 | – | +0.00 | -0.95 | +0.81 | – | -0.69 | -0.56 |
| water.grasper | – | +0.12 | +0.55 | -1.00 | +0.57 | · | -0.19 | – | +0.57 | +0.41 | +0.81 | – | +0.50 | +0.72 |
| water.ink_lobber | -1.00 | +0.17 | -0.30 | -1.00 | +0.34 | +0.19 | · | – | +0.51 | -0.92 | +0.80 | – | – | – |
| water.leviathan | – | – | – | – | – | – | – | · | – | – | – | +0.74 | – | – |
| water.reef_runner | -1.00 | +0.25 | +0.24 | -1.00 | +0.00 | -0.57 | -0.51 | – | · | -0.96 | +0.77 | – | -0.75 | -0.58 |
| water.shellback | S | +1.00 | +0.92 | -1.00 | +0.92 | -0.41 | +1.00 | – | +0.92 | · | +0.98 | – | -0.65 | +0.87 |
| water.spoutling | – | -0.74 | -0.80 | -0.79 | -0.82 | -0.81 | -0.85 | – | -0.80 | -0.98 | · | – | -0.87 | -0.87 |
| skai_early | – | – | – | – | – | – | – | -0.69 | – | – | – | · | -0.88 | -0.78 |
| water_early | – | – | +0.66 | -1.00 | +0.71 | -0.56 | – | – | +0.73 | +0.59 | +0.81 | +0.79 | · | +0.85 |
| water_rs | – | – | +0.35 | -1.00 | +0.42 | -0.71 | – | – | +0.49 | -0.91 | +0.87 | +0.68 | -0.86 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: flat, 1050

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_early | water_early | water_rs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.50 | +1.00 | -0.41 | +1.00 | +1.00 | -0.93 | +1.00 | S | +0.73 | – | +1.00 | +1.00 |
| skai.stormcaller | -1.00 | · | -0.20 | -1.00 | -0.17 | -0.30 | -0.25 | -0.97 | -0.08 | -0.94 | +0.78 | –* | – | – |
| skai.talon_archer | +0.43 | +0.31 | · | -0.58 | -0.08 | -0.58 | +0.14* | -0.98 | -0.18 | -0.91 | +0.84 | – | -0.64 | -0.34 |
| skai.thunderhead | -1.00 | +1.00 | +0.48 | · | +0.79 | +1.00 | +1.00 | -0.78 | +1.00 | +1.00 | +0.87 | – | +1.00 | +1.00 |
| skai.windrunner | +0.43 | +0.20 | +0.17 | -0.79 | · | -0.65 | -0.36 | -0.99 | -0.02* | -0.90 | +0.80 | – | -0.68 | -0.41 |
| water.grasper | -1.00 | +0.07 | +0.58 | -1.00 | +0.64 | · | -0.43 | -1.00 | +0.64 | +0.62 | +0.84 | – | +0.60 | +0.75 |
| water.ink_lobber | -1.00 | +0.32 | +0.18* | -1.00 | +0.42 | +0.36 | · | -0.81 | +0.46 | -0.86 | +0.84 | – | – | – |
| water.leviathan | +0.93 | +0.94 | +0.98 | +0.78 | +0.99 | +1.00 | +0.81 | · | +0.99 | +0.28* | +1.00 | – | – | – |
| water.reef_runner | -1.00 | +0.27 | +0.06 | -1.00 | -0.14* | -0.65 | -0.35 | -0.99 | · | -0.91 | +0.78 | – | -0.69 | -0.45 |
| water.shellback | S | +0.98 | +0.96 | -1.00 | +0.99 | -0.60 | +0.98 | +0.31* | +0.98 | · | +1.00 | – | -0.66 | +0.98 |
| water.spoutling | -0.69 | -0.81 | -0.84 | -0.86 | -0.80 | -0.84 | -0.84 | -1.00 | -0.79 | -0.99 | · | – | -0.86 | -0.84 |
| skai_early | – | –* | – | – | – | – | – | – | – | – | – | · | -0.85 | -0.64 |
| water_early | -1.00 | – | +0.56 | -1.00 | +0.65 | -0.63 | – | – | +0.67 | +0.72 | +0.86 | +0.81 | · | +0.77 |
| water_rs | -1.00 | – | +0.35 | -1.00 | +0.45 | -0.77 | – | – | +0.41 | -0.99 | +0.86 | +0.70 | -0.84 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: flat, 1350

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_early | water_early | water_rs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.25 | +1.00 | -0.30 | +1.00 | +1.00 | -0.92 | +1.00 | +1.00* | +0.67 | -0.08 | +1.00 | +1.00 |
| skai.stormcaller | -1.00 | · | -0.38 | -1.00 | -0.23 | -0.11 | -0.27 | -0.94 | -0.16* | -0.96 | +0.80 | -0.17 | -0.53 | -0.53 |
| skai.talon_archer | +0.31 | +0.27 | · | -0.51 | -0.13 | -0.57 | +0.19 | -0.96 | -0.16* | -0.89 | +0.83 | +0.18 | -0.78 | -0.57 |
| skai.thunderhead | -1.00 | +1.00 | +0.61 | · | +0.68 | +1.00 | +1.00 | -0.84 | +1.00 | +1.00* | +0.90 | +0.67 | +1.00 | +1.00 |
| skai.windrunner | +0.34 | +0.15 | +0.16 | -0.71 | · | -0.63 | +0.01* | -1.00 | -0.13* | -0.94 | +0.78 | +0.41 | -0.80 | -0.67 |
| water.grasper | -1.00 | +0.27 | +0.55 | -1.00 | +0.62 | · | -0.18 | -1.00 | +0.62 | +0.58 | +0.80 | +0.67 | +0.41 | +0.66 |
| water.ink_lobber | -1.00 | +0.27 | -0.04 | -1.00 | +0.07* | +0.40 | · | -0.83 | +0.12 | -0.89 | +0.84 | +0.29* | -0.46 | -0.49 |
| water.leviathan | +0.92 | +0.94 | +0.98 | +0.84 | +1.00 | +1.00 | +0.83 | · | +1.00 | -0.25 | +1.00 | – | +0.91 | +0.95 |
| water.reef_runner | -1.00 | -0.16* | -0.03* | -1.00 | -0.06* | -0.64 | -0.30 | -1.00 | · | -0.95 | +0.76 | +0.42 | -0.80 | -0.65 |
| water.shellback | U* | +1.00 | +0.92 | S* | +1.00 | -0.59 | +1.00 | +0.56 | +0.98 | · | +0.99 | +0.98 | -0.81 | +0.96 |
| water.spoutling | -0.67 | -0.81 | -0.82 | -0.85 | -0.78 | -0.80 | -0.81 | -1.00 | -0.78 | -0.98 | · | -0.76 | -0.86 | -0.86 |
| skai_early | +0.04 | +0.21 | -0.33 | -0.65 | -0.41 | -0.68 | +0.24* | – | -0.37 | -0.94 | +0.75 | · | -0.83 | -0.75 |
| water_early | -1.00 | +0.55 | +0.76 | -1.00 | +0.80 | -0.38 | +0.33 | -0.95 | +0.82 | +0.75 | +0.85 | +0.81 | · | +0.85 |
| water_rs | -1.00 | +0.64 | +0.63 | -1.00 | +0.69 | -0.68 | +0.58 | -0.92 | +0.68 | -0.90 | +0.87 | +0.69 | -0.82 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: flat, 1800

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_early | water_early | water_rs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.37 | +1.00 | -0.43 | +1.00 | +1.00 | -0.94 | +1.00 | S | +0.71 | -0.29 | +1.00 | +1.00 |
| skai.stormcaller | -1.00 | · | -0.18 | -1.00 | -0.16 | -0.06 | -0.28 | -0.97 | +0.02* | -0.94 | +0.86 | -0.24 | -0.56 | -0.66 |
| skai.talon_archer | +0.35 | +0.23 | · | -0.72 | -0.37 | -0.56 | -0.11 | -0.97 | -0.23 | -0.90 | +0.82 | +0.23 | -0.76 | -0.65 |
| skai.thunderhead | -1.00 | +1.00 | +0.72 | · | +0.75 | +1.00 | +1.00 | -0.77 | +1.00 | +1.00 | +0.92 | +0.73 | +1.00 | +1.00 |
| skai.windrunner | +0.42 | +0.15 | +0.30 | -0.81 | · | -0.61 | -0.28* | -1.00 | +0.09 | -0.94 | +0.79 | +0.39 | -0.83 | -0.64 |
| water.grasper | -1.00 | +0.13 | +0.54 | -1.00 | +0.58 | · | -0.28 | -1.00 | +0.61 | +0.62 | +0.83 | +0.65 | +0.38 | +0.60 |
| water.ink_lobber | -1.00 | +0.28 | +0.19 | -1.00 | -0.17* | +0.30 | · | -0.89 | +0.43 | -0.86 | +0.88 | +0.24 | -0.47 | -0.61 |
| water.leviathan | +0.94 | +0.97 | +0.99 | +0.77 | +1.00 | +1.00 | +0.89 | · | +1.00 | +0.53* | +1.00 | +0.98 | +0.97 | +0.88 |
| water.reef_runner | -1.00 | +0.26* | +0.26 | -1.00 | -0.02 | -0.62 | -0.32 | -1.00 | · | -0.92 | +0.77 | +0.42 | -0.85 | -0.63 |
| water.shellback | S | +1.00 | +0.95 | -1.00 | +0.90 | -0.60 | +0.94 | +0.80* | +0.95 | · | +1.00 | +0.98 | -0.82 | +0.97 |
| water.spoutling | -0.71 | -0.84 | -0.81 | -0.90 | -0.79 | -0.82 | -0.88 | -1.00 | -0.77 | -0.99 | · | -0.75 | -0.90 | -0.89 |
| skai_early | +0.31 | +0.35 | -0.34 | -0.78 | -0.37 | -0.67 | -0.13 | -0.96 | -0.38 | -0.94 | +0.76 | · | -0.86 | -0.79 |
| water_early | -1.00 | +0.53 | +0.72 | -1.00 | +0.79 | -0.32 | +0.30 | -0.98 | +0.78 | +0.82 | +0.88 | +0.81 | · | +0.79 |
| water_rs | -1.00 | +0.88 | +0.70 | -1.00 | +0.74 | -0.62 | +0.84 | -0.86 | +0.76 | -0.96 | +0.96 | +0.82 | -0.85 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: flat, 2400

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_early | water_early | water_rs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.30 | +1.00 | -0.45 | +1.00 | +1.00 | -0.94 | +1.00 | S | +0.69 | -0.49 | +1.00 | +1.00 |
| skai.stormcaller | -1.00 | · | +0.18 | -1.00 | -0.02 | +0.24 | -0.18 | -0.93 | -0.06* | -0.88 | +0.83 | -0.24 | -0.47 | -0.55 |
| skai.talon_archer | +0.33 | -0.15 | · | -0.71 | -0.14 | -0.59 | -0.42 | -0.93 | -0.06 | -0.91 | +0.82 | -0.21 | -0.72 | -0.63 |
| skai.thunderhead | -1.00 | +1.00 | +0.72 | · | +0.80 | +1.00 | +1.00 | -0.74 | +1.00 | +1.00 | +0.89 | +0.73 | +1.00 | +1.00 |
| skai.windrunner | +0.43 | +0.07 | +0.31 | -0.83 | · | -0.61 | -0.57 | -1.00 | +0.11 | -0.93 | +0.80 | +0.12 | -0.75 | -0.64 |
| water.grasper | -1.00 | -0.42 | +0.58 | -1.00 | +0.61 | · | -0.63 | -1.00 | +0.60 | +0.57 | +0.81 | +0.58 | +0.48 | +0.64 |
| water.ink_lobber | -1.00 | +0.12 | +0.46 | -1.00 | +0.48 | +0.59 | · | -0.87 | +0.66 | -0.89 | +0.92 | +0.31 | -0.17* | -0.03 |
| water.leviathan | +0.94 | +0.94 | +0.95 | +0.71 | +1.00 | +1.00 | +0.87 | · | +1.00 | -0.52* | +0.99 | +0.95 | +0.98 | +0.89 |
| water.reef_runner | -1.00 | -0.21* | +0.08 | -1.00 | -0.10 | -0.62 | -0.55 | -1.00 | · | -0.94 | +0.77 | -0.07* | -0.76 | -0.65 |
| water.shellback | S | +1.00 | +0.90 | -1.00 | +0.92 | -0.57 | +0.91 | -0.46* | +0.92 | · | +1.00 | +0.94 | -0.73 | +0.89 |
| water.spoutling | -0.71 | -0.83 | -0.83 | -0.87 | -0.80 | -0.81 | -0.91 | -0.98 | -0.78 | -1.00 | · | -0.79 | -0.89 | -0.95 |
| skai_early | +0.43 | +0.33 | +0.28 | -0.71 | -0.11 | -0.59 | -0.30 | -0.95 | -0.17* | -0.94 | +0.78 | · | -0.74 | -0.66 |
| water_early | -1.00 | +0.37 | +0.72 | -1.00 | +0.73 | -0.46 | -0.46* | -0.98 | +0.73 | +0.68 | +0.83 | +0.73 | · | +0.75 |
| water_rs | -1.00 | +0.83 | +0.72 | -1.00 | +0.73 | -0.64 | +0.56 | -0.87 | +0.79 | -0.97 | +0.97 | +0.80 | -0.75 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## HQ floor (Rooted >= 15.0 s against 30 supply)

| Attacker | Army | Count | Defender HQ | State | Time to kill | Result |
| --- | --- | ---: | --- | --- | ---: | --- |
| skai | skai.fledgling | 30 | skai.great_aerie | uprooted | 20.6 s | reported |
| skai | skai.fledgling | 30 | skai.great_aerie | rooted | 35.2 s | PASS |
| skai | skai.fledgling | 30 | water.grand_shell | uprooted | 26.3 s | reported |
| skai | skai.fledgling | 30 | water.grand_shell | rooted | 48.3 s | PASS |
| skai | skai.roc_rider | 10 | skai.great_aerie | uprooted | 16.9 s | reported |
| skai | skai.roc_rider | 10 | skai.great_aerie | rooted | 25.3 s | PASS |
| skai | skai.roc_rider | 10 | water.grand_shell | uprooted | 20.4 s | reported |
| skai | skai.roc_rider | 10 | water.grand_shell | rooted | 32.6 s | PASS |
| skai | skai.stormcaller | 10 | skai.great_aerie | uprooted | 14.6 s | reported |
| skai | skai.stormcaller | 10 | skai.great_aerie | rooted | 11.0 s | FAIL |
| skai | skai.stormcaller | 10 | water.grand_shell | uprooted | 17.7 s | reported |
| skai | skai.stormcaller | 10 | water.grand_shell | rooted | 13.5 s | FAIL |
| skai | skai.talon_archer | 15 | skai.great_aerie | uprooted | 16.8 s | reported |
| skai | skai.talon_archer | 15 | skai.great_aerie | rooted | 26.3 s | PASS |
| skai | skai.talon_archer | 15 | water.grand_shell | uprooted | 20.2 s | reported |
| skai | skai.talon_archer | 15 | water.grand_shell | rooted | 33.3 s | PASS |
| skai | skai.thunderhead | 7 | skai.great_aerie | uprooted | 10.7 s | reported |
| skai | skai.thunderhead | 7 | skai.great_aerie | rooted | 8.4 s | FAIL |
| skai | skai.thunderhead | 7 | water.grand_shell | uprooted | 13.2 s | reported |
| skai | skai.thunderhead | 7 | water.grand_shell | rooted | 10.4 s | FAIL |
| skai | skai.windrunner | 30 | skai.great_aerie | uprooted | 9.2 s | reported |
| skai | skai.windrunner | 30 | skai.great_aerie | rooted | 13.9 s | FAIL |
| skai | skai.windrunner | 30 | water.grand_shell | uprooted | 11.0 s | reported |
| skai | skai.windrunner | 30 | water.grand_shell | rooted | 16.8 s | PASS |
| skai | skai_early | 20 | skai.great_aerie | uprooted | 13.2 s | reported |
| skai | skai_early | 20 | skai.great_aerie | rooted | 20.6 s | PASS |
| skai | skai_early | 20 | water.grand_shell | uprooted | 15.8 s | reported |
| skai | skai_early | 20 | water.grand_shell | rooted | 25.5 s | PASS |
| water | water.grasper | 15 | skai.great_aerie | uprooted | 11.9 s | reported |
| water | water.grasper | 15 | skai.great_aerie | rooted | 17.4 s | PASS |
| water | water.grasper | 15 | water.grand_shell | uprooted | 14.0 s | reported |
| water | water.grasper | 15 | water.grand_shell | rooted | 21.5 s | PASS |
| water | water.ink_lobber | 15 | skai.great_aerie | uprooted | 7.8 s | reported |
| water | water.ink_lobber | 15 | skai.great_aerie | rooted | 6.6 s | FAIL |
| water | water.ink_lobber | 15 | water.grand_shell | uprooted | 9.7 s | reported |
| water | water.ink_lobber | 15 | water.grand_shell | rooted | 7.6 s | FAIL |
| water | water.leviathan | 7 | skai.great_aerie | uprooted | 6.0 s | reported |
| water | water.leviathan | 7 | skai.great_aerie | rooted | 3.0 s | FAIL |
| water | water.leviathan | 7 | water.grand_shell | uprooted | 9.0 s | reported |
| water | water.leviathan | 7 | water.grand_shell | rooted | 3.0 s | FAIL |
| water | water.reef_runner | 30 | skai.great_aerie | uprooted | 9.6 s | reported |
| water | water.reef_runner | 30 | skai.great_aerie | rooted | 14.2 s | FAIL |
| water | water.reef_runner | 30 | water.grand_shell | uprooted | 11.4 s | reported |
| water | water.reef_runner | 30 | water.grand_shell | rooted | 17.4 s | PASS |
| water | water.shellback | 10 | skai.great_aerie | uprooted | 21.3 s | reported |
| water | water.shellback | 10 | skai.great_aerie | rooted | 31.3 s | PASS |
| water | water.shellback | 10 | water.grand_shell | uprooted | 25.3 s | reported |
| water | water.shellback | 10 | water.grand_shell | rooted | 38.7 s | PASS |
| water | water.spoutling | 15 | skai.great_aerie | uprooted | 20.4 s | reported |
| water | water.spoutling | 15 | skai.great_aerie | rooted | > 300 s | PASS |
| water | water.spoutling | 15 | water.grand_shell | uprooted | 24.4 s | reported |
| water | water.spoutling | 15 | water.grand_shell | rooted | > 300 s | PASS |
| water | water.tender | 30 | skai.great_aerie | uprooted | 24.2 s | reported |
| water | water.tender | 30 | skai.great_aerie | rooted | 39.0 s | PASS |
| water | water.tender | 30 | water.grand_shell | uprooted | 28.0 s | reported |
| water | water.tender | 30 | water.grand_shell | rooted | 48.8 s | PASS |
| water | water_early | 12 | skai.great_aerie | uprooted | 15.4 s | reported |
| water | water_early | 12 | skai.great_aerie | rooted | 22.7 s | PASS |
| water | water_early | 12 | water.grand_shell | uprooted | 18.3 s | reported |
| water | water_early | 12 | water.grand_shell | rooted | 28.0 s | PASS |
| water | water_rs | 20 | skai.great_aerie | uprooted | 14.0 s | reported |
| water | water_rs | 20 | skai.great_aerie | rooted | 20.3 s | PASS |
| water | water_rs | 20 | water.grand_shell | uprooted | 16.5 s | reported |
| water | water_rs | 20 | water.grand_shell | rooted | 24.4 s | PASS |

## Matchups file

| Unit | Claim | Measured | Agrees |
| --- | --- | --- | --- |
| skai.roc_rider | beats water.grasper on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| skai.roc_rider | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.stormcaller | beats water.spoutling on flat | measured seat 0 win (left 0.78), seat 1 win (left 0.81) | yes |
| skai.stormcaller | loses to water.shellback on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.talon_archer | beats water.spoutling on flat | measured seat 0 win (left 0.84), seat 1 win (left 0.84) | yes |
| skai.talon_archer | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.thunderhead | beats water.grasper on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| skai.thunderhead | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.windrunner | beats water.spoutling on flat | measured seat 0 win (left 0.80), seat 1 win (left 0.80) | yes |
| skai.windrunner | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.grasper | beats water.spoutling on flat | measured seat 0 win (left 0.84), seat 1 win (left 0.84) | yes |
| water.grasper | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.ink_lobber | beats water.spoutling on flat | measured seat 0 win (left 0.84), seat 1 win (left 0.84) | yes |
| water.ink_lobber | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.leviathan | beats skai.windrunner on flat | measured seat 0 win (left 0.99), seat 1 win (left 0.99) | yes |
| water.leviathan | loses to water.shellback on flat | measured seat 0 win (left 0.28), seat 1 loss (left 0.00) | no |
| water.reef_runner | beats water.spoutling on flat | measured seat 0 win (left 0.78), seat 1 win (left 0.79) | yes |
| water.reef_runner | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.shellback | beats skai.stormcaller on flat | measured seat 0 win (left 0.98), seat 1 win (left 0.94) | yes |
| water.shellback | loses to skai.thunderhead on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.spoutling | wins: null | - | no |
| water.spoutling | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |

## Suggested matchups entries

| Unit | Wins against | Loses to |
| --- | --- | --- |
| skai.roc_rider | water.grasper (left 1.00) | water.leviathan (their left 0.93) |
| skai.stormcaller | water.spoutling (left 0.79) | skai.roc_rider (their left 1.00) |
| skai.talon_archer | water.spoutling (left 0.84) | water.leviathan (their left 0.98) |
| skai.thunderhead | water.grasper (left 1.00) | skai.roc_rider (their left 1.00) |
| skai.windrunner | water.spoutling (left 0.80) | water.leviathan (their left 0.99) |
| water.grasper | water.spoutling (left 0.84) | skai.roc_rider (their left 1.00) |
| water.ink_lobber | water.spoutling (left 0.84) | skai.roc_rider (their left 1.00) |
| water.leviathan | water.grasper (left 1.00) | null |
| water.reef_runner | water.spoutling (left 0.78) | skai.roc_rider (their left 1.00) |
| water.shellback | water.spoutling (left 1.00) | skai.thunderhead (their left 1.00) |
| water.spoutling | null | water.leviathan (their left 1.00) |

## Stalemates, unresolved fights and seat splits

A seat split means the two seat orders of a pair have different winners. On high_ground and flooded the seats are not symmetric by design, so splits there show terrain at work, not seat bias. A `no_damage` stalemate counts only damage events; when shots absorbed by a shield dome fell inside the idle window the stalemate is `dome_absorbed` instead.

| Fight | Finding | Reason |
| --- | --- | --- |
| flat/525/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/525/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/1050/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/1050/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/1050/skai.talon_archer/water.ink_lobber | seat split (skai.talon_archer wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flat/1050/skai.windrunner/water.reef_runner | seat split (water.reef_runner wins this order, skai.windrunner wins the swapped order) | eliminated |
| flat/1050/water.leviathan/water.shellback | seat split (water.leviathan wins this order, water.shellback wins the swapped order) | eliminated |
| flat/1350/water.shellback/skai.roc_rider | unresolved | time_cap |
| flat/1350/skai.stormcaller/water.reef_runner | seat split (water.reef_runner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flat/1350/skai.talon_archer/water.reef_runner | seat split (water.reef_runner wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flat/1350/water.shellback/skai.thunderhead | stalemate | dome_absorbed |
| flat/1350/skai.windrunner/water.ink_lobber | seat split (skai.windrunner wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flat/1350/skai.windrunner/water.reef_runner | seat split (water.reef_runner wins this order, skai.windrunner wins the swapped order) | eliminated |
| flat/1350/skai_early/water.ink_lobber | seat split (skai_early wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flat/1800/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/1800/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/1800/skai.stormcaller/water.reef_runner | seat split (skai.stormcaller wins this order, water.reef_runner wins the swapped order) | eliminated |
| flat/1800/skai.windrunner/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.windrunner wins the swapped order) | eliminated |
| flat/1800/water.leviathan/water.shellback | seat split (water.leviathan wins this order, water.shellback wins the swapped order) | eliminated |
| flat/2400/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/2400/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/2400/skai.stormcaller/water.reef_runner | seat split (water.reef_runner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flat/2400/water.ink_lobber/water_early | seat split (water_early wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flat/2400/water.leviathan/water.shellback | seat split (water.shellback wins this order, water.leviathan wins the swapped order) | eliminated |
| flat/2400/skai_early/water.reef_runner | seat split (water.reef_runner wins this order, skai_early wins the swapped order) | eliminated |

## All fights

| Fight | Result | Reason | Seconds | Left 0 | Left 1 | Values |
| --- | --- | --- | ---: | ---: | ---: | --- |
| flat/375/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 14.4 | 1.000 | 0.000 | 400/380 |
| flat/375/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 13.4 | 0.000 | 1.000 | 380/400 |
| flat/375/skai.roc_rider/skai.talon_archer | seat0 | eliminated | 23.3 | 0.029 | 0.000 | 400/400 |
| flat/375/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 23.3 | 0.000 | 0.085 | 400/400 |
| flat/375/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 10.9 | 1.000 | 0.000 | 400/265 |
| flat/375/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 10.9 | 0.000 | 1.000 | 265/400 |
| flat/375/skai.roc_rider/skai.windrunner | seat1 | eliminated | 19.7 | 0.000 | 0.200 | 400/375 |
| flat/375/skai.windrunner/skai.roc_rider | seat0 | eliminated | 19.7 | 0.200 | 0.000 | 375/400 |
| flat/375/skai.roc_rider/water.grasper | seat0 | eliminated | 29.2 | 1.000 | 0.000 | 400/400 |
| flat/375/water.grasper/skai.roc_rider | seat1 | eliminated | 29.2 | 0.000 | 1.000 | 400/400 |
| flat/375/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 14.1 | 1.000 | 0.000 | 400/380 |
| flat/375/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 14.1 | 0.000 | 1.000 | 380/400 |
| flat/375/skai.roc_rider/water.leviathan | seat1 | eliminated | 7.1 | 0.000 | 0.753 | 400/380 |
| flat/375/water.leviathan/skai.roc_rider | seat0 | eliminated | 7.1 | 0.753 | 0.000 | 380/400 |
| flat/375/skai.roc_rider/water.reef_runner | seat0 | eliminated | 17.0 | 1.000 | 0.000 | 400/375 |
| flat/375/water.reef_runner/skai.roc_rider | seat1 | eliminated | 17.0 | 0.000 | 1.000 | 375/400 |
| flat/375/skai.roc_rider/water.shellback | seat0 | eliminated | 162.1 | 1.000 | 0.000 | 400/350 |
| flat/375/water.shellback/skai.roc_rider | seat1 | eliminated | 160.1 | 0.000 | 1.000 | 350/400 |
| flat/375/skai.roc_rider/water.spoutling | seat0 | eliminated | 10.0 | 0.688 | 0.000 | 400/375 |
| flat/375/water.spoutling/skai.roc_rider | seat1 | eliminated | 10.0 | 0.000 | 0.688 | 375/400 |
| flat/375/skai.roc_rider/skai_early | seat1 | eliminated | 18.7 | 0.000 | 0.251 | 400/425 |
| flat/375/skai_early/skai.roc_rider | seat0 | eliminated | 20.2 | 0.199 | 0.000 | 425/400 |
| flat/375/skai.roc_rider/water_early | seat0 | eliminated | 57.2 | 1.000 | 0.000 | 400/475 |
| flat/375/water_early/skai.roc_rider | seat1 | eliminated | 53.2 | 0.000 | 1.000 | 475/400 |
| flat/375/skai.roc_rider/water_rs | seat0 | eliminated | 44.6 | 1.000 | 0.000 | 400/475 |
| flat/375/water_rs/skai.roc_rider | seat1 | eliminated | 41.0 | 0.000 | 1.000 | 475/400 |
| flat/375/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 7.8 | 0.000 | 0.347 | 380/400 |
| flat/375/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 7.7 | 0.347 | 0.000 | 400/380 |
| flat/375/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 14.8 | 0.000 | 1.000 | 380/265 |
| flat/375/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 14.8 | 1.000 | 0.000 | 265/380 |
| flat/375/skai.stormcaller/skai.windrunner | seat1 | eliminated | 7.7 | 0.000 | 0.384 | 380/375 |
| flat/375/skai.windrunner/skai.stormcaller | seat0 | eliminated | 7.2 | 0.384 | 0.000 | 375/380 |
| flat/375/skai.stormcaller/water.grasper | seat1 | eliminated | 8.3 | 0.000 | 0.369 | 380/400 |
| flat/375/water.grasper/skai.stormcaller | seat0 | eliminated | 8.3 | 0.400 | 0.000 | 400/380 |
| flat/375/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 8.4 | 0.000 | 0.252 | 380/380 |
| flat/375/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 8.4 | 0.252 | 0.000 | 380/380 |
| flat/375/skai.stormcaller/water.leviathan | seat1 | eliminated | 7.8 | 0.000 | 0.669 | 380/380 |
| flat/375/water.leviathan/skai.stormcaller | seat0 | eliminated | 7.8 | 0.669 | 0.000 | 380/380 |
| flat/375/skai.stormcaller/water.reef_runner | seat1 | eliminated | 7.9 | 0.000 | 0.152 | 380/375 |
| flat/375/water.reef_runner/skai.stormcaller | seat0 | eliminated | 8.1 | 0.282 | 0.000 | 375/380 |
| flat/375/skai.stormcaller/water.shellback | seat1 | eliminated | 14.1 | 0.000 | 0.783 | 380/350 |
| flat/375/water.shellback/skai.stormcaller | seat0 | eliminated | 14.1 | 1.000 | 0.000 | 350/380 |
| flat/375/skai.stormcaller/water.spoutling | seat0 | eliminated | 9.4 | 0.611 | 0.000 | 380/375 |
| flat/375/water.spoutling/skai.stormcaller | seat1 | eliminated | 9.4 | 0.000 | 0.611 | 375/380 |
| flat/375/skai.stormcaller/skai_early | seat1 | eliminated | 7.5 | 0.000 | 0.569 | 380/425 |
| flat/375/skai_early/skai.stormcaller | seat0 | eliminated | 7.6 | 0.535 | 0.000 | 425/380 |
| flat/375/skai.stormcaller/water_early | seat1 | eliminated | 8.2 | 0.000 | 0.801 | 380/475 |
| flat/375/water_early/skai.stormcaller | seat0 | eliminated | 8.3 | 0.801 | 0.000 | 475/380 |
| flat/375/skai.stormcaller/water_rs | seat1 | eliminated | 9.1 | 0.000 | 0.688 | 380/475 |
| flat/375/water_rs/skai.stormcaller | seat0 | eliminated | 9.0 | 0.739 | 0.000 | 475/380 |
| flat/375/skai.talon_archer/skai.thunderhead | seat0 | eliminated | 9.6 | 0.143 | 0.000 | 400/265 |
| flat/375/skai.thunderhead/skai.talon_archer | seat1 | eliminated | 9.6 | 0.000 | 0.143 | 265/400 |
| flat/375/skai.talon_archer/skai.windrunner | seat0 | eliminated | 15.8 | 0.267 | 0.000 | 400/375 |
| flat/375/skai.windrunner/skai.talon_archer | seat1 | eliminated | 15.7 | 0.000 | 0.250 | 375/400 |
| flat/375/skai.talon_archer/water.grasper | seat1 | eliminated | 13.1 | 0.000 | 0.559 | 400/400 |
| flat/375/water.grasper/skai.talon_archer | seat0 | eliminated | 13.8 | 0.500 | 0.000 | 400/400 |
| flat/375/skai.talon_archer/water.ink_lobber | seat0 | eliminated | 9.5 | 0.150 | 0.000 | 400/380 |
| flat/375/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 9.5 | 0.000 | 0.150 | 380/400 |
| flat/375/skai.talon_archer/water.leviathan | seat1 | eliminated | 7.6 | 0.000 | 0.637 | 400/380 |
| flat/375/water.leviathan/skai.talon_archer | seat0 | eliminated | 7.5 | 0.635 | 0.000 | 380/400 |
| flat/375/skai.talon_archer/water.reef_runner | seat0 | eliminated | 15.8 | 0.300 | 0.000 | 400/375 |
| flat/375/water.reef_runner/skai.talon_archer | seat1 | eliminated | 15.7 | 0.000 | 0.317 | 375/400 |
| flat/375/skai.talon_archer/water.shellback | seat1 | eliminated | 22.2 | 0.000 | 0.822 | 400/350 |
| flat/375/water.shellback/skai.talon_archer | seat0 | eliminated | 22.2 | 0.795 | 0.000 | 350/400 |
| flat/375/skai.talon_archer/water.spoutling | seat0 | eliminated | 8.9 | 0.819 | 0.000 | 400/375 |
| flat/375/water.spoutling/skai.talon_archer | seat1 | eliminated | 8.9 | 0.000 | 0.819 | 375/400 |
| flat/375/skai.talon_archer/skai_early | seat0 | eliminated | 25.8 | 0.006 | 0.000 | 400/425 |
| flat/375/skai_early/skai.talon_archer | seat1 | eliminated | 22.8 | 0.000 | 0.072 | 425/400 |
| flat/375/skai.talon_archer/water_early | seat1 | eliminated | 12.3 | 0.000 | 0.790 | 400/475 |
| flat/375/water_early/skai.talon_archer | seat0 | eliminated | 12.8 | 0.767 | 0.000 | 475/400 |
| flat/375/skai.talon_archer/water_rs | seat1 | eliminated | 15.1 | 0.000 | 0.644 | 400/475 |
| flat/375/water_rs/skai.talon_archer | seat0 | eliminated | 16.2 | 0.604 | 0.000 | 475/400 |
| flat/375/skai.thunderhead/skai.windrunner | seat0 | eliminated | 8.9 | 0.120 | 0.000 | 265/375 |
| flat/375/skai.windrunner/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 0.120 | 375/265 |
| flat/375/skai.thunderhead/water.grasper | seat0 | eliminated | 11.6 | 1.000 | 0.000 | 265/400 |
| flat/375/water.grasper/skai.thunderhead | seat1 | eliminated | 14.1 | 0.000 | 1.000 | 400/265 |
| flat/375/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 14.3 | 1.000 | 0.000 | 265/380 |
| flat/375/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 14.3 | 0.000 | 1.000 | 380/265 |
| flat/375/skai.thunderhead/water.leviathan | seat1 | eliminated | 7.1 | 0.000 | 0.703 | 265/380 |
| flat/375/water.leviathan/skai.thunderhead | seat0 | eliminated | 7.1 | 0.703 | 0.000 | 380/265 |
| flat/375/skai.thunderhead/water.reef_runner | seat0 | eliminated | 8.9 | 1.000 | 0.000 | 265/375 |
| flat/375/water.reef_runner/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 1.000 | 375/265 |
| flat/375/skai.thunderhead/water.shellback | seat0 | eliminated | 129.3 | 1.000 | 0.000 | 265/350 |
| flat/375/water.shellback/skai.thunderhead | seat1 | eliminated | 119.3 | 0.000 | 1.000 | 350/265 |
| flat/375/skai.thunderhead/water.spoutling | seat0 | eliminated | 11.6 | 0.368 | 0.000 | 265/375 |
| flat/375/water.spoutling/skai.thunderhead | seat1 | eliminated | 11.6 | 0.000 | 0.368 | 375/265 |
| flat/375/skai.thunderhead/skai_early | seat1 | eliminated | 10.1 | 0.000 | 0.427 | 265/425 |
| flat/375/skai_early/skai.thunderhead | seat0 | eliminated | 9.6 | 0.383 | 0.000 | 425/265 |
| flat/375/skai.thunderhead/water_early | seat0 | eliminated | 36.6 | 1.000 | 0.000 | 265/475 |
| flat/375/water_early/skai.thunderhead | seat1 | eliminated | 39.1 | 0.000 | 1.000 | 475/265 |
| flat/375/skai.thunderhead/water_rs | seat0 | eliminated | 38.9 | 1.000 | 0.000 | 265/475 |
| flat/375/water_rs/skai.thunderhead | seat1 | eliminated | 38.9 | 0.000 | 1.000 | 475/265 |
| flat/375/skai.windrunner/water.grasper | seat1 | eliminated | 9.8 | 0.000 | 0.658 | 375/400 |
| flat/375/water.grasper/skai.windrunner | seat0 | eliminated | 10.1 | 0.627 | 0.000 | 400/375 |
| flat/375/skai.windrunner/water.ink_lobber | seat0 | eliminated | 8.6 | 0.220 | 0.000 | 375/380 |
| flat/375/water.ink_lobber/skai.windrunner | seat1 | eliminated | 8.6 | 0.000 | 0.220 | 380/375 |
| flat/375/skai.windrunner/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.878 | 375/380 |
| flat/375/water.leviathan/skai.windrunner | seat0 | eliminated | 4.2 | 0.878 | 0.000 | 380/375 |
| flat/375/skai.windrunner/water.reef_runner | draw | both_eliminated | 19.6 | 0.000 | 0.000 | 375/375 |
| flat/375/water.reef_runner/skai.windrunner | draw | both_eliminated | 19.6 | 0.000 | 0.000 | 375/375 |
| flat/375/skai.windrunner/water.shellback | seat1 | eliminated | 17.7 | 0.000 | 0.824 | 375/350 |
| flat/375/water.shellback/skai.windrunner | seat0 | eliminated | 17.7 | 0.824 | 0.000 | 350/375 |
| flat/375/skai.windrunner/water.spoutling | seat0 | eliminated | 7.3 | 0.800 | 0.000 | 375/375 |
| flat/375/water.spoutling/skai.windrunner | seat1 | eliminated | 7.3 | 0.000 | 0.800 | 375/375 |
| flat/375/skai.windrunner/skai_early | seat1 | eliminated | 16.9 | 0.000 | 0.173 | 375/425 |
| flat/375/skai_early/skai.windrunner | seat0 | eliminated | 15.0 | 0.298 | 0.000 | 425/375 |
| flat/375/skai.windrunner/water_early | seat1 | eliminated | 10.1 | 0.000 | 0.866 | 375/475 |
| flat/375/water_early/skai.windrunner | seat0 | eliminated | 9.8 | 0.831 | 0.000 | 475/375 |
| flat/375/skai.windrunner/water_rs | seat1 | eliminated | 11.3 | 0.000 | 0.732 | 375/475 |
| flat/375/water_rs/skai.windrunner | seat0 | eliminated | 10.8 | 0.809 | 0.000 | 475/375 |
| flat/375/water.grasper/water.ink_lobber | seat0 | eliminated | 11.3 | 0.127 | 0.000 | 400/380 |
| flat/375/water.ink_lobber/water.grasper | seat1 | eliminated | 10.8 | 0.000 | 0.009 | 380/400 |
| flat/375/water.grasper/water.leviathan | seat1 | eliminated | 7.4 | 0.000 | 0.747 | 400/380 |
| flat/375/water.leviathan/water.grasper | seat0 | eliminated | 7.5 | 0.719 | 0.000 | 380/400 |
| flat/375/water.grasper/water.reef_runner | seat0 | eliminated | 9.9 | 0.658 | 0.000 | 400/375 |
| flat/375/water.reef_runner/water.grasper | seat1 | eliminated | 9.8 | 0.000 | 0.678 | 375/400 |
| flat/375/water.grasper/water.shellback | seat0 | eliminated | 17.9 | 0.631 | 0.000 | 400/350 |
| flat/375/water.shellback/water.grasper | seat1 | eliminated | 17.7 | 0.000 | 0.631 | 350/400 |
| flat/375/water.grasper/water.spoutling | seat0 | eliminated | 7.6 | 0.841 | 0.000 | 400/375 |
| flat/375/water.spoutling/water.grasper | seat1 | eliminated | 7.6 | 0.000 | 0.830 | 375/400 |
| flat/375/water.grasper/skai_early | seat0 | eliminated | 12.3 | 0.593 | 0.000 | 400/425 |
| flat/375/skai_early/water.grasper | seat1 | eliminated | 12.4 | 0.000 | 0.590 | 425/400 |
| flat/375/water.grasper/water_early | seat0 | eliminated | 31.0 | 0.025 | 0.000 | 400/475 |
| flat/375/water_early/water.grasper | seat1 | eliminated | 30.5 | 0.000 | 0.100 | 475/400 |
| flat/375/water.grasper/water_rs | seat0 | eliminated | 16.4 | 0.549 | 0.000 | 400/475 |
| flat/375/water_rs/water.grasper | seat1 | eliminated | 16.4 | 0.000 | 0.559 | 475/400 |
| flat/375/water.ink_lobber/water.leviathan | seat1 | eliminated | 8.0 | 0.000 | 0.662 | 380/380 |
| flat/375/water.leviathan/water.ink_lobber | seat0 | eliminated | 8.0 | 0.662 | 0.000 | 380/380 |
| flat/375/water.ink_lobber/water.reef_runner | seat1 | eliminated | 8.7 | 0.000 | 0.285 | 380/375 |
| flat/375/water.reef_runner/water.ink_lobber | seat0 | eliminated | 8.7 | 0.285 | 0.000 | 375/380 |
| flat/375/water.ink_lobber/water.shellback | seat1 | eliminated | 15.4 | 0.000 | 0.782 | 380/350 |
| flat/375/water.shellback/water.ink_lobber | seat0 | eliminated | 15.8 | 1.000 | 0.000 | 350/380 |
| flat/375/water.ink_lobber/water.spoutling | seat0 | eliminated | 11.0 | 0.575 | 0.000 | 380/375 |
| flat/375/water.spoutling/water.ink_lobber | seat1 | eliminated | 11.0 | 0.000 | 0.575 | 375/380 |
| flat/375/water.ink_lobber/skai_early | seat1 | eliminated | 8.2 | 0.000 | 0.488 | 380/425 |
| flat/375/skai_early/water.ink_lobber | seat0 | eliminated | 8.2 | 0.471 | 0.000 | 425/380 |
| flat/375/water.ink_lobber/water_early | seat1 | eliminated | 8.9 | 0.000 | 0.724 | 380/475 |
| flat/375/water_early/water.ink_lobber | seat0 | eliminated | 10.4 | 0.652 | 0.000 | 475/380 |
| flat/375/water.ink_lobber/water_rs | seat1 | eliminated | 9.9 | 0.000 | 0.487 | 380/475 |
| flat/375/water_rs/water.ink_lobber | seat0 | eliminated | 9.4 | 0.629 | 0.000 | 475/380 |
| flat/375/water.leviathan/water.reef_runner | seat0 | eliminated | 4.2 | 0.887 | 0.000 | 380/375 |
| flat/375/water.reef_runner/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.887 | 375/380 |
| flat/375/water.leviathan/water.shellback | seat1 | eliminated | 27.2 | 0.000 | 0.364 | 380/350 |
| flat/375/water.shellback/water.leviathan | seat0 | eliminated | 27.2 | 0.364 | 0.000 | 350/380 |
| flat/375/water.leviathan/water.spoutling | seat0 | eliminated | 7.5 | 0.813 | 0.000 | 380/375 |
| flat/375/water.spoutling/water.leviathan | seat1 | eliminated | 7.5 | 0.000 | 0.813 | 375/380 |
| flat/375/water.leviathan/skai_early | seat0 | eliminated | 7.2 | 0.742 | 0.000 | 380/425 |
| flat/375/skai_early/water.leviathan | seat1 | eliminated | 10.2 | 0.000 | 0.685 | 425/380 |
| flat/375/water.leviathan/water_early | seat0 | eliminated | 19.5 | 0.020 | 0.000 | 380/475 |
| flat/375/water_early/water.leviathan | seat1 | eliminated | 19.4 | 0.000 | 0.044 | 475/380 |
| flat/375/water.leviathan/water_rs | seat0 | eliminated | 19.2 | 0.377 | 0.000 | 380/475 |
| flat/375/water_rs/water.leviathan | seat1 | eliminated | 19.2 | 0.000 | 0.292 | 475/380 |
| flat/375/water.reef_runner/water.shellback | seat1 | eliminated | 17.8 | 0.000 | 0.887 | 375/350 |
| flat/375/water.shellback/water.reef_runner | seat0 | eliminated | 17.8 | 0.887 | 0.000 | 350/375 |
| flat/375/water.reef_runner/water.spoutling | seat0 | eliminated | 8.1 | 0.750 | 0.000 | 375/375 |
| flat/375/water.spoutling/water.reef_runner | seat1 | eliminated | 8.1 | 0.000 | 0.750 | 375/375 |
| flat/375/water.reef_runner/skai_early | seat1 | eliminated | 15.6 | 0.000 | 0.235 | 375/425 |
| flat/375/skai_early/water.reef_runner | seat0 | eliminated | 14.9 | 0.298 | 0.000 | 425/375 |
| flat/375/water.reef_runner/water_early | seat1 | eliminated | 9.9 | 0.000 | 0.862 | 375/475 |
| flat/375/water_early/water.reef_runner | seat0 | eliminated | 9.8 | 0.828 | 0.000 | 475/375 |
| flat/375/water.reef_runner/water_rs | seat1 | eliminated | 10.8 | 0.000 | 0.835 | 375/475 |
| flat/375/water_rs/water.reef_runner | seat0 | eliminated | 10.9 | 0.799 | 0.000 | 475/375 |
| flat/375/water.shellback/water.spoutling | seat0 | eliminated | 14.2 | 0.977 | 0.000 | 350/375 |
| flat/375/water.spoutling/water.shellback | seat1 | eliminated | 14.2 | 0.000 | 0.977 | 375/350 |
| flat/375/water.shellback/skai_early | seat0 | eliminated | 22.0 | 0.784 | 0.000 | 350/425 |
| flat/375/skai_early/water.shellback | seat1 | eliminated | 21.2 | 0.000 | 0.766 | 425/350 |
| flat/375/water.shellback/water_early | seat1 | eliminated | 19.6 | 0.000 | 0.914 | 350/475 |
| flat/375/water_early/water.shellback | seat0 | eliminated | 19.6 | 0.914 | 0.000 | 475/350 |
| flat/375/water.shellback/water_rs | seat0 | eliminated | 55.9 | 0.622 | 0.000 | 350/475 |
| flat/375/water_rs/water.shellback | seat1 | eliminated | 56.3 | 0.000 | 0.499 | 475/350 |
| flat/375/water.spoutling/skai_early | seat1 | eliminated | 7.5 | 0.000 | 0.824 | 375/425 |
| flat/375/skai_early/water.spoutling | seat0 | eliminated | 7.5 | 0.794 | 0.000 | 425/375 |
| flat/375/water.spoutling/water_early | seat1 | eliminated | 8.4 | 0.000 | 0.914 | 375/475 |
| flat/375/water_early/water.spoutling | seat0 | eliminated | 8.4 | 0.856 | 0.000 | 475/375 |
| flat/375/water.spoutling/water_rs | seat1 | eliminated | 8.5 | 0.000 | 0.908 | 375/475 |
| flat/375/water_rs/water.spoutling | seat0 | eliminated | 8.5 | 0.921 | 0.000 | 475/375 |
| flat/375/skai_early/water_early | seat1 | eliminated | 11.0 | 0.000 | 0.881 | 425/475 |
| flat/375/water_early/skai_early | seat0 | eliminated | 11.0 | 0.790 | 0.000 | 475/425 |
| flat/375/skai_early/water_rs | seat1 | eliminated | 13.4 | 0.000 | 0.782 | 425/475 |
| flat/375/water_rs/skai_early | seat0 | eliminated | 13.3 | 0.684 | 0.000 | 475/425 |
| flat/375/water_early/water_rs | seat0 | eliminated | 17.8 | 0.848 | 0.000 | 475/475 |
| flat/375/water_rs/water_early | seat1 | eliminated | 17.5 | 0.000 | 0.862 | 475/475 |
| flat/525/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.9 | 1.000 | 0.000 | 600/570 |
| flat/525/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.9 | 0.000 | 1.000 | 570/600 |
| flat/525/skai.roc_rider/skai.talon_archer | seat0 | eliminated | 20.9 | 0.329 | 0.000 | 600/500 |
| flat/525/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 20.9 | 0.000 | 0.329 | 500/600 |
| flat/525/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 14.6 | 1.000 | 0.000 | 600/530 |
| flat/525/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 14.6 | 0.000 | 1.000 | 530/600 |
| flat/525/skai.roc_rider/skai.windrunner | seat0 | eliminated | 23.0 | 0.088 | 0.000 | 600/525 |
| flat/525/skai.windrunner/skai.roc_rider | seat1 | eliminated | 23.0 | 0.000 | 0.088 | 525/600 |
| flat/525/skai.roc_rider/water.grasper | seat0 | eliminated | 24.4 | 1.000 | 0.000 | 600/500 |
| flat/525/water.grasper/skai.roc_rider | seat1 | eliminated | 24.4 | 0.000 | 1.000 | 500/600 |
| flat/525/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 14.4 | 1.000 | 0.000 | 600/570 |
| flat/525/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 14.4 | 0.000 | 1.000 | 570/600 |
| flat/525/skai.roc_rider/water.leviathan | seat1 | eliminated | 10.1 | 0.000 | 0.512 | 600/380 |
| flat/525/water.leviathan/skai.roc_rider | seat0 | eliminated | 10.1 | 0.512 | 0.000 | 380/600 |
| flat/525/skai.roc_rider/water.reef_runner | seat0 | eliminated | 17.0 | 1.000 | 0.000 | 600/525 |
| flat/525/water.reef_runner/skai.roc_rider | seat1 | eliminated | 17.0 | 0.000 | 1.000 | 525/600 |
| flat/525/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 51.1 | 1.000 | 0.895 | 600/525 |
| flat/525/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 36.7 | 0.905 | 1.000 | 525/600 |
| flat/525/skai.roc_rider/water.spoutling | seat0 | eliminated | 10.1 | 0.781 | 0.000 | 600/500 |
| flat/525/water.spoutling/skai.roc_rider | seat1 | eliminated | 10.1 | 0.000 | 0.760 | 500/600 |
| flat/525/skai.roc_rider/skai_early | seat0 | eliminated | 14.7 | 0.573 | 0.000 | 600/425 |
| flat/525/skai_early/skai.roc_rider | seat1 | eliminated | 14.7 | 0.000 | 0.575 | 425/600 |
| flat/525/skai.roc_rider/water_early | seat0 | eliminated | 30.4 | 1.000 | 0.000 | 600/475 |
| flat/525/water_early/skai.roc_rider | seat1 | eliminated | 30.4 | 0.000 | 1.000 | 475/600 |
| flat/525/skai.roc_rider/water_rs | seat0 | eliminated | 24.2 | 1.000 | 0.000 | 600/475 |
| flat/525/water_rs/skai.roc_rider | seat1 | eliminated | 24.2 | 0.000 | 1.000 | 475/600 |
| flat/525/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 9.7 | 0.000 | 0.375 | 570/500 |
| flat/525/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 9.7 | 0.375 | 0.000 | 500/570 |
| flat/525/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 570/530 |
| flat/525/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 530/570 |
| flat/525/skai.stormcaller/skai.windrunner | seat1 | eliminated | 8.1 | 0.000 | 0.323 | 570/525 |
| flat/525/skai.windrunner/skai.stormcaller | seat0 | eliminated | 8.1 | 0.323 | 0.000 | 525/570 |
| flat/525/skai.stormcaller/water.grasper | seat1 | eliminated | 10.5 | 0.000 | 0.122 | 570/500 |
| flat/525/water.grasper/skai.stormcaller | seat0 | eliminated | 10.5 | 0.122 | 0.000 | 500/570 |
| flat/525/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 8.4 | 0.000 | 0.169 | 570/570 |
| flat/525/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 8.4 | 0.169 | 0.000 | 570/570 |
| flat/525/skai.stormcaller/water.leviathan | seat1 | eliminated | 7.6 | 0.000 | 0.572 | 570/380 |
| flat/525/water.leviathan/skai.stormcaller | seat0 | eliminated | 7.6 | 0.572 | 0.000 | 380/570 |
| flat/525/skai.stormcaller/water.reef_runner | seat1 | eliminated | 9.1 | 0.000 | 0.249 | 570/525 |
| flat/525/water.reef_runner/skai.stormcaller | seat0 | eliminated | 9.1 | 0.249 | 0.000 | 525/570 |
| flat/525/skai.stormcaller/water.shellback | seat1 | eliminated | 14.6 | 0.000 | 0.943 | 570/525 |
| flat/525/water.shellback/skai.stormcaller | seat0 | eliminated | 14.6 | 1.000 | 0.000 | 525/570 |
| flat/525/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.0 | 0.741 | 0.000 | 570/500 |
| flat/525/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.1 | 0.000 | 0.741 | 500/570 |
| flat/525/skai.stormcaller/skai_early | seat1 | eliminated | 12.5 | 0.000 | 0.324 | 570/425 |
| flat/525/skai_early/skai.stormcaller | seat0 | eliminated | 14.5 | 0.176 | 0.000 | 425/570 |
| flat/525/skai.stormcaller/water_early | seat1 | eliminated | 10.5 | 0.000 | 0.726 | 570/475 |
| flat/525/water_early/skai.stormcaller | seat0 | eliminated | 12.1 | 0.492 | 0.000 | 475/570 |
| flat/525/skai.stormcaller/water_rs | seat1 | eliminated | 14.1 | 0.000 | 0.601 | 570/475 |
| flat/525/water_rs/skai.stormcaller | seat0 | eliminated | 14.0 | 0.582 | 0.000 | 475/570 |
| flat/525/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 0.564 | 500/530 |
| flat/525/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 6.7 | 0.564 | 0.000 | 530/500 |
| flat/525/skai.talon_archer/skai.windrunner | seat1 | eliminated | 14.6 | 0.000 | 0.267 | 500/525 |
| flat/525/skai.windrunner/skai.talon_archer | seat0 | eliminated | 14.6 | 0.267 | 0.000 | 525/500 |
| flat/525/skai.talon_archer/water.grasper | seat1 | eliminated | 14.0 | 0.000 | 0.545 | 500/500 |
| flat/525/water.grasper/skai.talon_archer | seat0 | eliminated | 14.0 | 0.545 | 0.000 | 500/500 |
| flat/525/skai.talon_archer/water.ink_lobber | seat0 | eliminated | 10.9 | 0.300 | 0.000 | 500/570 |
| flat/525/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 10.9 | 0.000 | 0.300 | 570/500 |
| flat/525/skai.talon_archer/water.leviathan | seat1 | eliminated | 7.5 | 0.000 | 0.533 | 500/380 |
| flat/525/water.leviathan/skai.talon_archer | seat0 | eliminated | 7.5 | 0.533 | 0.000 | 380/500 |
| flat/525/skai.talon_archer/water.reef_runner | seat1 | eliminated | 16.3 | 0.000 | 0.238 | 500/525 |
| flat/525/water.reef_runner/skai.talon_archer | seat0 | eliminated | 16.3 | 0.238 | 0.000 | 525/500 |
| flat/525/skai.talon_archer/water.shellback | seat1 | eliminated | 20.4 | 0.000 | 0.936 | 500/525 |
| flat/525/water.shellback/skai.talon_archer | seat0 | eliminated | 20.4 | 0.918 | 0.000 | 525/500 |
| flat/525/skai.talon_archer/water.spoutling | seat0 | eliminated | 8.9 | 0.800 | 0.000 | 500/500 |
| flat/525/water.spoutling/skai.talon_archer | seat1 | eliminated | 8.9 | 0.000 | 0.800 | 500/500 |
| flat/525/skai.talon_archer/skai_early | seat0 | eliminated | 14.8 | 0.440 | 0.000 | 500/425 |
| flat/525/skai_early/skai.talon_archer | seat1 | eliminated | 14.8 | 0.000 | 0.440 | 425/500 |
| flat/525/skai.talon_archer/water_early | seat1 | eliminated | 15.6 | 0.000 | 0.740 | 500/475 |
| flat/525/water_early/skai.talon_archer | seat0 | eliminated | 17.2 | 0.663 | 0.000 | 475/500 |
| flat/525/skai.talon_archer/water_rs | seat1 | eliminated | 20.0 | 0.000 | 0.467 | 500/475 |
| flat/525/water_rs/skai.talon_archer | seat0 | eliminated | 24.4 | 0.345 | 0.000 | 475/500 |
| flat/525/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.4 | 0.628 | 0.000 | 530/525 |
| flat/525/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 0.628 | 525/530 |
| flat/525/skai.thunderhead/water.grasper | seat0 | eliminated | 9.1 | 1.000 | 0.000 | 530/500 |
| flat/525/water.grasper/skai.thunderhead | seat1 | eliminated | 9.1 | 0.000 | 1.000 | 500/530 |
| flat/525/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 530/570 |
| flat/525/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 6.8 | 0.000 | 1.000 | 570/530 |
| flat/525/skai.thunderhead/water.leviathan | seat1 | eliminated | 10.1 | 0.000 | 0.131 | 530/380 |
| flat/525/water.leviathan/skai.thunderhead | seat0 | eliminated | 10.1 | 0.131 | 0.000 | 380/530 |
| flat/525/skai.thunderhead/water.reef_runner | seat0 | eliminated | 8.9 | 1.000 | 0.000 | 530/525 |
| flat/525/water.reef_runner/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 1.000 | 525/530 |
| flat/525/skai.thunderhead/water.shellback | seat0 | eliminated | 59.3 | 1.000 | 0.000 | 530/525 |
| flat/525/water.shellback/skai.thunderhead | seat1 | eliminated | 71.8 | 0.000 | 1.000 | 525/530 |
| flat/525/skai.thunderhead/water.spoutling | seat0 | eliminated | 6.6 | 0.789 | 0.000 | 530/500 |
| flat/525/water.spoutling/skai.thunderhead | seat1 | eliminated | 6.6 | 0.000 | 0.789 | 500/530 |
| flat/525/skai.thunderhead/skai_early | seat0 | eliminated | 11.4 | 0.602 | 0.000 | 530/425 |
| flat/525/skai_early/skai.thunderhead | seat1 | eliminated | 11.4 | 0.000 | 0.635 | 425/530 |
| flat/525/skai.thunderhead/water_early | seat0 | eliminated | 19.1 | 1.000 | 0.000 | 530/475 |
| flat/525/water_early/skai.thunderhead | seat1 | eliminated | 19.1 | 0.000 | 1.000 | 475/530 |
| flat/525/skai.thunderhead/water_rs | seat0 | eliminated | 18.9 | 1.000 | 0.000 | 530/475 |
| flat/525/water_rs/skai.thunderhead | seat1 | eliminated | 18.9 | 0.000 | 1.000 | 475/530 |
| flat/525/skai.windrunner/water.grasper | seat1 | eliminated | 11.2 | 0.000 | 0.569 | 525/500 |
| flat/525/water.grasper/skai.windrunner | seat0 | eliminated | 11.2 | 0.569 | 0.000 | 500/525 |
| flat/525/skai.windrunner/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.341 | 525/570 |
| flat/525/water.ink_lobber/skai.windrunner | seat0 | eliminated | 10.8 | 0.341 | 0.000 | 570/525 |
| flat/525/skai.windrunner/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.801 | 525/380 |
| flat/525/water.leviathan/skai.windrunner | seat0 | eliminated | 4.2 | 0.801 | 0.000 | 380/525 |
| flat/525/skai.windrunner/water.reef_runner | draw | both_eliminated | 19.7 | 0.000 | 0.000 | 525/525 |
| flat/525/water.reef_runner/skai.windrunner | draw | both_eliminated | 19.7 | 0.000 | 0.000 | 525/525 |
| flat/525/skai.windrunner/water.shellback | seat1 | eliminated | 17.0 | 0.000 | 0.945 | 525/525 |
| flat/525/water.shellback/skai.windrunner | seat0 | eliminated | 17.0 | 0.920 | 0.000 | 525/525 |
| flat/525/skai.windrunner/water.spoutling | seat0 | eliminated | 7.6 | 0.810 | 0.000 | 525/500 |
| flat/525/water.spoutling/skai.windrunner | seat1 | eliminated | 7.6 | 0.000 | 0.821 | 500/525 |
| flat/525/skai.windrunner/skai_early | seat0 | eliminated | 10.1 | 0.571 | 0.000 | 525/425 |
| flat/525/skai_early/skai.windrunner | seat1 | eliminated | 10.7 | 0.000 | 0.552 | 425/525 |
| flat/525/skai.windrunner/water_early | seat1 | eliminated | 14.3 | 0.000 | 0.686 | 525/475 |
| flat/525/water_early/skai.windrunner | seat0 | eliminated | 14.4 | 0.710 | 0.000 | 475/525 |
| flat/525/skai.windrunner/water_rs | seat1 | eliminated | 15.3 | 0.000 | 0.563 | 525/475 |
| flat/525/water_rs/skai.windrunner | seat0 | eliminated | 17.9 | 0.420 | 0.000 | 475/525 |
| flat/525/water.grasper/water.ink_lobber | seat1 | eliminated | 11.1 | 0.000 | 0.187 | 500/570 |
| flat/525/water.ink_lobber/water.grasper | seat0 | eliminated | 11.1 | 0.187 | 0.000 | 570/500 |
| flat/525/water.grasper/water.leviathan | seat1 | eliminated | 7.4 | 0.000 | 0.662 | 500/380 |
| flat/525/water.leviathan/water.grasper | seat0 | eliminated | 7.4 | 0.662 | 0.000 | 380/500 |
| flat/525/water.grasper/water.reef_runner | seat0 | eliminated | 11.2 | 0.571 | 0.000 | 500/525 |
| flat/525/water.reef_runner/water.grasper | seat1 | eliminated | 11.3 | 0.000 | 0.571 | 525/500 |
| flat/525/water.grasper/water.shellback | seat0 | eliminated | 23.1 | 0.409 | 0.000 | 500/525 |
| flat/525/water.shellback/water.grasper | seat1 | eliminated | 23.1 | 0.000 | 0.409 | 525/500 |
| flat/525/water.grasper/water.spoutling | seat0 | eliminated | 8.3 | 0.809 | 0.000 | 500/500 |
| flat/525/water.spoutling/water.grasper | seat1 | eliminated | 8.3 | 0.000 | 0.809 | 500/500 |
| flat/525/water.grasper/skai_early | seat0 | eliminated | 9.6 | 0.745 | 0.000 | 500/425 |
| flat/525/skai_early/water.grasper | seat1 | eliminated | 9.9 | 0.000 | 0.724 | 425/500 |
| flat/525/water.grasper/water_early | seat0 | eliminated | 17.7 | 0.503 | 0.000 | 500/475 |
| flat/525/water_early/water.grasper | seat1 | eliminated | 18.1 | 0.000 | 0.559 | 475/500 |
| flat/525/water.grasper/water_rs | seat0 | eliminated | 13.2 | 0.718 | 0.000 | 500/475 |
| flat/525/water_rs/water.grasper | seat1 | eliminated | 13.4 | 0.000 | 0.710 | 475/500 |
| flat/525/water.ink_lobber/water.leviathan | seat1 | eliminated | 10.8 | 0.000 | 0.325 | 570/380 |
| flat/525/water.leviathan/water.ink_lobber | seat0 | eliminated | 10.8 | 0.325 | 0.000 | 380/570 |
| flat/525/water.ink_lobber/water.reef_runner | seat0 | eliminated | 7.8 | 0.507 | 0.000 | 570/525 |
| flat/525/water.reef_runner/water.ink_lobber | seat1 | eliminated | 7.8 | 0.000 | 0.507 | 525/570 |
| flat/525/water.ink_lobber/water.shellback | seat1 | eliminated | 15.9 | 0.000 | 0.918 | 570/525 |
| flat/525/water.shellback/water.ink_lobber | seat0 | eliminated | 15.9 | 1.000 | 0.000 | 525/570 |
| flat/525/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.9 | 0.800 | 0.000 | 570/500 |
| flat/525/water.spoutling/water.ink_lobber | seat1 | eliminated | 5.2 | 0.000 | 0.850 | 500/570 |
| flat/525/water.ink_lobber/skai_early | seat0 | eliminated | 13.8 | 0.343 | 0.000 | 570/425 |
| flat/525/skai_early/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.341 | 425/570 |
| flat/525/water.ink_lobber/water_early | seat1 | eliminated | 11.1 | 0.000 | 0.690 | 570/475 |
| flat/525/water_early/water.ink_lobber | seat0 | eliminated | 12.8 | 0.480 | 0.000 | 475/570 |
| flat/525/water.ink_lobber/water_rs | seat1 | eliminated | 13.7 | 0.000 | 0.567 | 570/475 |
| flat/525/water_rs/water.ink_lobber | seat0 | eliminated | 14.8 | 0.528 | 0.000 | 475/570 |
| flat/525/water.leviathan/water.reef_runner | seat0 | eliminated | 4.2 | 0.845 | 0.000 | 380/525 |
| flat/525/water.reef_runner/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.845 | 525/380 |
| flat/525/water.leviathan/water.shellback | seat1 | eliminated | 20.9 | 0.000 | 1.000 | 380/525 |
| flat/525/water.shellback/water.leviathan | seat0 | eliminated | 20.9 | 1.000 | 0.000 | 525/380 |
| flat/525/water.leviathan/water.spoutling | seat0 | eliminated | 7.5 | 0.750 | 0.000 | 380/500 |
| flat/525/water.spoutling/water.leviathan | seat1 | eliminated | 7.6 | 0.000 | 0.750 | 500/380 |
| flat/525/water.leviathan/skai_early | seat0 | eliminated | 7.2 | 0.742 | 0.000 | 380/425 |
| flat/525/skai_early/water.leviathan | seat1 | eliminated | 10.2 | 0.000 | 0.685 | 425/380 |
| flat/525/water.leviathan/water_early | seat0 | eliminated | 19.5 | 0.020 | 0.000 | 380/475 |
| flat/525/water_early/water.leviathan | seat1 | eliminated | 19.4 | 0.000 | 0.044 | 475/380 |
| flat/525/water.leviathan/water_rs | seat0 | eliminated | 19.2 | 0.377 | 0.000 | 380/475 |
| flat/525/water_rs/water.leviathan | seat1 | eliminated | 19.2 | 0.000 | 0.292 | 475/380 |
| flat/525/water.reef_runner/water.shellback | seat1 | eliminated | 17.6 | 0.000 | 0.959 | 525/525 |
| flat/525/water.shellback/water.reef_runner | seat0 | eliminated | 17.6 | 0.918 | 0.000 | 525/525 |
| flat/525/water.reef_runner/water.spoutling | seat0 | eliminated | 7.9 | 0.774 | 0.000 | 525/500 |
| flat/525/water.spoutling/water.reef_runner | seat1 | eliminated | 7.9 | 0.000 | 0.798 | 500/525 |
| flat/525/water.reef_runner/skai_early | seat0 | eliminated | 10.3 | 0.571 | 0.000 | 525/425 |
| flat/525/skai_early/water.reef_runner | seat1 | eliminated | 10.9 | 0.000 | 0.552 | 425/525 |
| flat/525/water.reef_runner/water_early | seat1 | eliminated | 13.5 | 0.000 | 0.746 | 525/475 |
| flat/525/water_early/water.reef_runner | seat0 | eliminated | 13.9 | 0.732 | 0.000 | 475/525 |
| flat/525/water.reef_runner/water_rs | seat1 | eliminated | 15.0 | 0.000 | 0.578 | 525/475 |
| flat/525/water_rs/water.reef_runner | seat0 | eliminated | 15.6 | 0.490 | 0.000 | 475/525 |
| flat/525/water.shellback/water.spoutling | seat0 | eliminated | 13.7 | 0.977 | 0.000 | 525/500 |
| flat/525/water.spoutling/water.shellback | seat1 | eliminated | 13.7 | 0.000 | 0.977 | 500/525 |
| flat/525/water.shellback/skai_early | seat0 | eliminated | 15.7 | 0.948 | 0.000 | 525/425 |
| flat/525/skai_early/water.shellback | seat1 | eliminated | 16.2 | 0.000 | 0.920 | 425/525 |
| flat/525/water.shellback/water_early | seat1 | eliminated | 34.2 | 0.000 | 0.646 | 525/475 |
| flat/525/water_early/water.shellback | seat0 | eliminated | 34.0 | 0.589 | 0.000 | 475/525 |
| flat/525/water.shellback/water_rs | seat0 | eliminated | 33.0 | 0.870 | 0.000 | 525/475 |
| flat/525/water_rs/water.shellback | seat1 | eliminated | 33.3 | 0.000 | 0.911 | 475/525 |
| flat/525/water.spoutling/skai_early | seat1 | eliminated | 10.5 | 0.000 | 0.676 | 500/425 |
| flat/525/skai_early/water.spoutling | seat0 | eliminated | 10.4 | 0.662 | 0.000 | 425/500 |
| flat/525/water.spoutling/water_early | seat1 | eliminated | 10.2 | 0.000 | 0.866 | 500/475 |
| flat/525/water_early/water.spoutling | seat0 | eliminated | 10.1 | 0.809 | 0.000 | 475/500 |
| flat/525/water.spoutling/water_rs | seat1 | eliminated | 10.6 | 0.000 | 0.868 | 500/475 |
| flat/525/water_rs/water.spoutling | seat0 | eliminated | 10.4 | 0.868 | 0.000 | 475/500 |
| flat/525/skai_early/water_early | seat1 | eliminated | 11.0 | 0.000 | 0.881 | 425/475 |
| flat/525/water_early/skai_early | seat0 | eliminated | 11.0 | 0.790 | 0.000 | 475/425 |
| flat/525/skai_early/water_rs | seat1 | eliminated | 13.4 | 0.000 | 0.782 | 425/475 |
| flat/525/water_rs/skai_early | seat0 | eliminated | 13.3 | 0.684 | 0.000 | 475/425 |
| flat/525/water_early/water_rs | seat0 | eliminated | 17.8 | 0.848 | 0.000 | 475/475 |
| flat/525/water_rs/water_early | seat1 | eliminated | 17.5 | 0.000 | 0.862 | 475/475 |
| flat/1050/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 12.5 | 1.000 | 0.000 | 1000/1140 |
| flat/1050/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 13.7 | 0.000 | 1.000 | 1140/1000 |
| flat/1050/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 17.6 | 0.000 | 0.503 | 1000/1100 |
| flat/1050/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 18.6 | 0.432 | 0.000 | 1100/1000 |
| flat/1050/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 19.4 | 1.000 | 0.000 | 1000/1060 |
| flat/1050/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 19.4 | 0.000 | 1.000 | 1060/1000 |
| flat/1050/skai.roc_rider/skai.windrunner | seat1 | eliminated | 16.5 | 0.000 | 0.412 | 1000/1050 |
| flat/1050/skai.windrunner/skai.roc_rider | seat0 | eliminated | 15.8 | 0.433 | 0.000 | 1050/1000 |
| flat/1050/skai.roc_rider/water.grasper | seat0 | eliminated | 31.8 | 1.000 | 0.000 | 1000/1100 |
| flat/1050/water.grasper/skai.roc_rider | seat1 | eliminated | 30.4 | 0.000 | 1.000 | 1100/1000 |
| flat/1050/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 13.8 | 1.000 | 0.000 | 1000/1140 |
| flat/1050/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 12.6 | 0.000 | 1.000 | 1140/1000 |
| flat/1050/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.930 | 1000/1140 |
| flat/1050/water.leviathan/skai.roc_rider | seat0 | eliminated | 4.1 | 0.930 | 0.000 | 1140/1000 |
| flat/1050/skai.roc_rider/water.reef_runner | seat0 | eliminated | 19.4 | 1.000 | 0.000 | 1000/1050 |
| flat/1050/water.reef_runner/skai.roc_rider | seat1 | eliminated | 19.5 | 0.000 | 1.000 | 1050/1000 |
| flat/1050/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 76.3 | 1.000 | 0.833 | 1000/1050 |
| flat/1050/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 36.8 | 0.960 | 1.000 | 1050/1000 |
| flat/1050/skai.roc_rider/water.spoutling | seat0 | eliminated | 11.4 | 0.725 | 0.000 | 1000/1000 |
| flat/1050/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.6 | 0.000 | 0.688 | 1000/1000 |
| flat/1050/skai.roc_rider/skai_early | seat0 | eliminated | 26.7 | 0.199 | 0.000 | 1000/850 |
| flat/1050/skai_early/skai.roc_rider | seat1 | eliminated | 21.9 | 0.000 | 0.321 | 850/1000 |
| flat/1050/skai.roc_rider/water_early | seat0 | eliminated | 42.6 | 1.000 | 0.000 | 1000/950 |
| flat/1050/water_early/skai.roc_rider | seat1 | eliminated | 42.5 | 0.000 | 1.000 | 950/1000 |
| flat/1050/skai.roc_rider/water_rs | seat0 | eliminated | 27.9 | 1.000 | 0.000 | 1000/950 |
| flat/1050/water_rs/skai.roc_rider | seat1 | eliminated | 27.9 | 0.000 | 1.000 | 950/1000 |
| flat/1050/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 11.9 | 0.000 | 0.202 | 1140/1100 |
| flat/1050/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 11.2 | 0.311 | 0.000 | 1100/1140 |
| flat/1050/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1140/1060 |
| flat/1050/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 1060/1140 |
| flat/1050/skai.stormcaller/skai.windrunner | seat1 | eliminated | 9.5 | 0.000 | 0.167 | 1140/1050 |
| flat/1050/skai.windrunner/skai.stormcaller | seat0 | eliminated | 10.2 | 0.204 | 0.000 | 1050/1140 |
| flat/1050/skai.stormcaller/water.grasper | seat1 | eliminated | 10.9 | 0.000 | 0.295 | 1140/1100 |
| flat/1050/water.grasper/skai.stormcaller | seat0 | eliminated | 15.3 | 0.068 | 0.000 | 1100/1140 |
| flat/1050/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.4 | 0.000 | 0.247 | 1140/1140 |
| flat/1050/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 9.3 | 0.316 | 0.000 | 1140/1140 |
| flat/1050/skai.stormcaller/water.leviathan | seat1 | eliminated | 2.3 | 0.000 | 0.972 | 1140/1140 |
| flat/1050/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.3 | 0.944 | 0.000 | 1140/1140 |
| flat/1050/skai.stormcaller/water.reef_runner | seat1 | eliminated | 12.0 | 0.000 | 0.080 | 1140/1050 |
| flat/1050/water.reef_runner/skai.stormcaller | seat0 | eliminated | 9.9 | 0.269 | 0.000 | 1050/1140 |
| flat/1050/skai.stormcaller/water.shellback | seat1 | eliminated | 15.6 | 0.000 | 0.943 | 1140/1050 |
| flat/1050/water.shellback/skai.stormcaller | seat0 | eliminated | 15.1 | 0.981 | 0.000 | 1050/1140 |
| flat/1050/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.2 | 0.778 | 0.000 | 1140/1000 |
| flat/1050/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.3 | 0.000 | 0.806 | 1000/1140 |
| flat/1050/skai.stormcaller/skai_early | seat1 | eliminated | 18.0 | 0.000 | 0.071 | 1140/850 |
| flat/1050/skai_early/skai.stormcaller | seat1 | eliminated | 11.8 | 0.000 | 0.485 | 850/1140 |
| flat/1050/skai.stormcaller/water_early | seat1 | eliminated | 12.8 | 0.000 | 0.391 | 1140/950 |
| flat/1050/water_early/skai.stormcaller | seat0 | eliminated | 27.3 | 0.080 | 0.000 | 950/1140 |
| flat/1050/skai.stormcaller/water_rs | seat1 | eliminated | 18.2 | 0.000 | 0.337 | 1140/950 |
| flat/1050/water_rs/skai.stormcaller | seat0 | eliminated | 18.3 | 0.316 | 0.000 | 950/1140 |
| flat/1050/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 11.7 | 0.000 | 0.576 | 1100/1060 |
| flat/1050/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 12.0 | 0.481 | 0.000 | 1060/1100 |
| flat/1050/skai.talon_archer/skai.windrunner | seat1 | eliminated | 23.7 | 0.000 | 0.076 | 1100/1050 |
| flat/1050/skai.windrunner/skai.talon_archer | seat0 | eliminated | 21.0 | 0.167 | 0.000 | 1050/1100 |
| flat/1050/skai.talon_archer/water.grasper | seat1 | eliminated | 14.0 | 0.000 | 0.577 | 1100/1100 |
| flat/1050/water.grasper/skai.talon_archer | seat0 | eliminated | 14.2 | 0.583 | 0.000 | 1100/1100 |
| flat/1050/skai.talon_archer/water.ink_lobber | seat0 | eliminated | 12.1 | 0.136 | 0.000 | 1100/1140 |
| flat/1050/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 14.4 | 0.177 | 0.000 | 1140/1100 |
| flat/1050/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.980 | 1100/1140 |
| flat/1050/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.980 | 0.000 | 1140/1100 |
| flat/1050/skai.talon_archer/water.reef_runner | seat1 | eliminated | 20.3 | 0.000 | 0.181 | 1100/1050 |
| flat/1050/water.reef_runner/skai.talon_archer | seat0 | eliminated | 25.0 | 0.057 | 0.000 | 1050/1100 |
| flat/1050/skai.talon_archer/water.shellback | seat1 | eliminated | 21.7 | 0.000 | 0.910 | 1100/1050 |
| flat/1050/water.shellback/skai.talon_archer | seat0 | eliminated | 21.5 | 0.964 | 0.000 | 1050/1100 |
| flat/1050/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.1 | 0.838 | 0.000 | 1100/1000 |
| flat/1050/water.spoutling/skai.talon_archer | seat1 | eliminated | 8.8 | 0.000 | 0.838 | 1000/1100 |
| flat/1050/skai.talon_archer/skai_early | seat0 | eliminated | 14.8 | 0.545 | 0.000 | 1100/850 |
| flat/1050/skai_early/skai.talon_archer | seat1 | eliminated | 13.8 | 0.000 | 0.572 | 850/1100 |
| flat/1050/skai.talon_archer/water_early | seat1 | eliminated | 18.3 | 0.000 | 0.644 | 1100/950 |
| flat/1050/water_early/skai.talon_archer | seat0 | eliminated | 20.6 | 0.564 | 0.000 | 950/1100 |
| flat/1050/skai.talon_archer/water_rs | seat1 | eliminated | 27.6 | 0.000 | 0.341 | 1100/950 |
| flat/1050/water_rs/skai.talon_archer | seat0 | eliminated | 28.1 | 0.350 | 0.000 | 950/1100 |
| flat/1050/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.4 | 0.785 | 0.000 | 1060/1050 |
| flat/1050/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 0.785 | 1050/1060 |
| flat/1050/skai.thunderhead/water.grasper | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 1060/1100 |
| flat/1050/water.grasper/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1100/1060 |
| flat/1050/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 6.9 | 1.000 | 0.000 | 1060/1140 |
| flat/1050/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 1140/1060 |
| flat/1050/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.777 | 1060/1140 |
| flat/1050/water.leviathan/skai.thunderhead | seat0 | eliminated | 4.1 | 0.777 | 0.000 | 1140/1060 |
| flat/1050/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.5 | 1.000 | 0.000 | 1060/1050 |
| flat/1050/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 1.000 | 1050/1060 |
| flat/1050/skai.thunderhead/water.shellback | seat0 | eliminated | 49.3 | 1.000 | 0.000 | 1060/1050 |
| flat/1050/water.shellback/skai.thunderhead | seat1 | eliminated | 102.7 | 0.000 | 1.000 | 1050/1060 |
| flat/1050/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.3 | 0.868 | 0.000 | 1060/1000 |
| flat/1050/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.4 | 0.000 | 0.855 | 1000/1060 |
| flat/1050/skai.thunderhead/skai_early | seat0 | eliminated | 9.0 | 0.752 | 0.000 | 1060/850 |
| flat/1050/skai_early/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 0.734 | 850/1060 |
| flat/1050/skai.thunderhead/water_early | seat0 | eliminated | 16.6 | 1.000 | 0.000 | 1060/950 |
| flat/1050/water_early/skai.thunderhead | seat1 | eliminated | 14.2 | 0.000 | 1.000 | 950/1060 |
| flat/1050/skai.thunderhead/water_rs | seat0 | eliminated | 16.4 | 1.000 | 0.000 | 1060/950 |
| flat/1050/water_rs/skai.thunderhead | seat1 | eliminated | 16.4 | 0.000 | 1.000 | 950/1060 |
| flat/1050/skai.windrunner/water.grasper | seat1 | eliminated | 10.7 | 0.000 | 0.648 | 1050/1100 |
| flat/1050/water.grasper/skai.windrunner | seat0 | eliminated | 10.7 | 0.637 | 0.000 | 1100/1050 |
| flat/1050/skai.windrunner/water.ink_lobber | seat1 | eliminated | 10.9 | 0.000 | 0.356 | 1050/1140 |
| flat/1050/water.ink_lobber/skai.windrunner | seat0 | eliminated | 10.9 | 0.416 | 0.000 | 1140/1050 |
| flat/1050/skai.windrunner/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.990 | 1050/1140 |
| flat/1050/water.leviathan/skai.windrunner | seat0 | eliminated | 4.0 | 0.990 | 0.000 | 1140/1050 |
| flat/1050/skai.windrunner/water.reef_runner | seat1 | eliminated | 20.9 | 0.000 | 0.021 | 1050/1050 |
| flat/1050/water.reef_runner/skai.windrunner | seat1 | eliminated | 17.9 | 0.000 | 0.136 | 1050/1050 |
| flat/1050/skai.windrunner/water.shellback | seat1 | eliminated | 18.3 | 0.000 | 0.896 | 1050/1050 |
| flat/1050/water.shellback/skai.windrunner | seat0 | eliminated | 18.0 | 0.990 | 0.000 | 1050/1050 |
| flat/1050/skai.windrunner/water.spoutling | seat0 | eliminated | 7.9 | 0.798 | 0.000 | 1050/1000 |
| flat/1050/water.spoutling/skai.windrunner | seat1 | eliminated | 8.1 | 0.000 | 0.798 | 1000/1050 |
| flat/1050/skai.windrunner/skai_early | seat0 | eliminated | 11.4 | 0.529 | 0.000 | 1050/850 |
| flat/1050/skai_early/skai.windrunner | seat1 | eliminated | 10.4 | 0.000 | 0.582 | 850/1050 |
| flat/1050/skai.windrunner/water_early | seat1 | eliminated | 15.1 | 0.000 | 0.677 | 1050/950 |
| flat/1050/water_early/skai.windrunner | seat0 | eliminated | 15.9 | 0.650 | 0.000 | 950/1050 |
| flat/1050/skai.windrunner/water_rs | seat1 | eliminated | 20.5 | 0.000 | 0.406 | 1050/950 |
| flat/1050/water_rs/skai.windrunner | seat0 | eliminated | 18.3 | 0.454 | 0.000 | 950/1050 |
| flat/1050/water.grasper/water.ink_lobber | seat1 | eliminated | 11.1 | 0.000 | 0.433 | 1100/1140 |
| flat/1050/water.ink_lobber/water.grasper | seat0 | eliminated | 11.4 | 0.360 | 0.000 | 1140/1100 |
| flat/1050/water.grasper/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 1100/1140 |
| flat/1050/water.leviathan/water.grasper | seat0 | eliminated | 4.5 | 1.000 | 0.000 | 1140/1100 |
| flat/1050/water.grasper/water.reef_runner | seat0 | eliminated | 10.9 | 0.642 | 0.000 | 1100/1050 |
| flat/1050/water.reef_runner/water.grasper | seat1 | eliminated | 10.8 | 0.000 | 0.649 | 1050/1100 |
| flat/1050/water.grasper/water.shellback | seat0 | eliminated | 20.0 | 0.618 | 0.000 | 1100/1050 |
| flat/1050/water.shellback/water.grasper | seat1 | eliminated | 20.2 | 0.000 | 0.597 | 1050/1100 |
| flat/1050/water.grasper/water.spoutling | seat0 | eliminated | 8.6 | 0.835 | 0.000 | 1100/1000 |
| flat/1050/water.spoutling/water.grasper | seat1 | eliminated | 8.6 | 0.000 | 0.839 | 1000/1100 |
| flat/1050/water.grasper/skai_early | seat0 | eliminated | 10.1 | 0.753 | 0.000 | 1100/850 |
| flat/1050/skai_early/water.grasper | seat1 | eliminated | 9.5 | 0.000 | 0.750 | 850/1100 |
| flat/1050/water.grasper/water_early | seat0 | eliminated | 16.9 | 0.600 | 0.000 | 1100/950 |
| flat/1050/water_early/water.grasper | seat1 | eliminated | 16.7 | 0.000 | 0.631 | 950/1100 |
| flat/1050/water.grasper/water_rs | seat0 | eliminated | 12.5 | 0.750 | 0.000 | 1100/950 |
| flat/1050/water_rs/water.grasper | seat1 | eliminated | 12.0 | 0.000 | 0.770 | 950/1100 |
| flat/1050/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.813 | 1140/1140 |
| flat/1050/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.6 | 0.813 | 0.000 | 1140/1140 |
| flat/1050/water.ink_lobber/water.reef_runner | seat0 | eliminated | 10.9 | 0.460 | 0.000 | 1140/1050 |
| flat/1050/water.reef_runner/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.350 | 1050/1140 |
| flat/1050/water.ink_lobber/water.shellback | seat1 | eliminated | 16.4 | 0.000 | 0.859 | 1140/1050 |
| flat/1050/water.shellback/water.ink_lobber | seat0 | eliminated | 16.7 | 0.977 | 0.000 | 1050/1140 |
| flat/1050/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.9 | 0.842 | 0.000 | 1140/1000 |
| flat/1050/water.spoutling/water.ink_lobber | seat1 | eliminated | 7.9 | 0.000 | 0.842 | 1000/1140 |
| flat/1050/water.ink_lobber/skai_early | seat0 | eliminated | 19.9 | 0.198 | 0.000 | 1140/850 |
| flat/1050/skai_early/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.473 | 850/1140 |
| flat/1050/water.ink_lobber/water_early | seat0 | eliminated | 23.2 | 0.233 | 0.000 | 1140/950 |
| flat/1050/water_early/water.ink_lobber | seat1 | eliminated | 14.4 | 0.000 | 0.550 | 950/1140 |
| flat/1050/water.ink_lobber/water_rs | seat1 | eliminated | 19.7 | 0.000 | 0.211 | 1140/950 |
| flat/1050/water_rs/water.ink_lobber | seat0 | eliminated | 15.9 | 0.316 | 0.000 | 950/1140 |
| flat/1050/water.leviathan/water.reef_runner | seat0 | eliminated | 4.0 | 0.991 | 0.000 | 1140/1050 |
| flat/1050/water.reef_runner/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.991 | 1050/1140 |
| flat/1050/water.leviathan/water.shellback | seat0 | eliminated | 32.3 | 0.276 | 0.000 | 1140/1050 |
| flat/1050/water.shellback/water.leviathan | seat0 | eliminated | 32.1 | 0.311 | 0.000 | 1050/1140 |
| flat/1050/water.leviathan/water.spoutling | seat0 | eliminated | 2.1 | 1.000 | 0.000 | 1140/1000 |
| flat/1050/water.spoutling/water.leviathan | seat1 | eliminated | 2.1 | 0.000 | 1.000 | 1000/1140 |
| flat/1050/water.leviathan/skai_early | seat0 | eliminated | 4.3 | 0.973 | 0.000 | 1140/850 |
| flat/1050/skai_early/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.966 | 850/1140 |
| flat/1050/water.leviathan/water_early | seat0 | eliminated | 7.9 | 0.948 | 0.000 | 1140/950 |
| flat/1050/water_early/water.leviathan | seat1 | eliminated | 10.4 | 0.000 | 0.952 | 950/1140 |
| flat/1050/water.leviathan/water_rs | seat0 | eliminated | 10.3 | 0.952 | 0.000 | 1140/950 |
| flat/1050/water_rs/water.leviathan | seat1 | eliminated | 10.0 | 0.000 | 0.967 | 950/1140 |
| flat/1050/water.reef_runner/water.shellback | seat1 | eliminated | 18.4 | 0.000 | 0.912 | 1050/1050 |
| flat/1050/water.shellback/water.reef_runner | seat0 | eliminated | 18.7 | 0.976 | 0.000 | 1050/1050 |
| flat/1050/water.reef_runner/water.spoutling | seat0 | eliminated | 8.6 | 0.780 | 0.000 | 1050/1000 |
| flat/1050/water.spoutling/water.reef_runner | seat1 | eliminated | 8.5 | 0.000 | 0.786 | 1000/1050 |
| flat/1050/water.reef_runner/skai_early | seat0 | eliminated | 11.1 | 0.532 | 0.000 | 1050/850 |
| flat/1050/skai_early/water.reef_runner | seat1 | eliminated | 10.9 | 0.000 | 0.560 | 850/1050 |
| flat/1050/water.reef_runner/water_early | seat1 | eliminated | 15.2 | 0.000 | 0.686 | 1050/950 |
| flat/1050/water_early/water.reef_runner | seat0 | eliminated | 15.2 | 0.672 | 0.000 | 950/1050 |
| flat/1050/water.reef_runner/water_rs | seat1 | eliminated | 19.5 | 0.000 | 0.451 | 1050/950 |
| flat/1050/water_rs/water.reef_runner | seat0 | eliminated | 19.7 | 0.409 | 0.000 | 950/1050 |
| flat/1050/water.shellback/water.spoutling | seat0 | eliminated | 14.2 | 1.000 | 0.000 | 1050/1000 |
| flat/1050/water.spoutling/water.shellback | seat1 | eliminated | 15.1 | 0.000 | 0.992 | 1000/1050 |
| flat/1050/water.shellback/skai_early | seat0 | eliminated | 17.2 | 0.973 | 0.000 | 1050/850 |
| flat/1050/skai_early/water.shellback | seat1 | eliminated | 17.4 | 0.000 | 0.977 | 850/1050 |
| flat/1050/water.shellback/water_early | seat1 | eliminated | 36.2 | 0.000 | 0.660 | 1050/950 |
| flat/1050/water_early/water.shellback | seat0 | eliminated | 30.4 | 0.715 | 0.000 | 950/1050 |
| flat/1050/water.shellback/water_rs | seat0 | eliminated | 35.2 | 0.976 | 0.000 | 1050/950 |
| flat/1050/water_rs/water.shellback | seat1 | eliminated | 36.5 | 0.000 | 0.993 | 950/1050 |
| flat/1050/water.spoutling/skai_early | seat1 | eliminated | 10.4 | 0.000 | 0.676 | 1000/850 |
| flat/1050/skai_early/water.spoutling | seat0 | eliminated | 10.5 | 0.676 | 0.000 | 850/1000 |
| flat/1050/water.spoutling/water_early | seat1 | eliminated | 11.3 | 0.000 | 0.856 | 1000/950 |
| flat/1050/water_early/water.spoutling | seat0 | eliminated | 11.2 | 0.856 | 0.000 | 950/1000 |
| flat/1050/water.spoutling/water_rs | seat1 | eliminated | 11.5 | 0.000 | 0.842 | 1000/950 |
| flat/1050/water_rs/water.spoutling | seat0 | eliminated | 11.8 | 0.855 | 0.000 | 950/1000 |
| flat/1050/skai_early/water_early | seat1 | eliminated | 12.9 | 0.000 | 0.852 | 850/950 |
| flat/1050/water_early/skai_early | seat0 | eliminated | 12.9 | 0.814 | 0.000 | 950/850 |
| flat/1050/skai_early/water_rs | seat1 | eliminated | 15.8 | 0.000 | 0.644 | 850/950 |
| flat/1050/water_rs/skai_early | seat0 | eliminated | 15.7 | 0.703 | 0.000 | 950/850 |
| flat/1050/water_early/water_rs | seat0 | eliminated | 21.8 | 0.768 | 0.000 | 950/950 |
| flat/1050/water_rs/water_early | seat1 | eliminated | 20.2 | 0.000 | 0.836 | 950/950 |
| flat/1350/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.1 | 1.000 | 0.000 | 1400/1330 |
| flat/1350/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.1 | 0.000 | 1.000 | 1330/1400 |
| flat/1350/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 25.7 | 0.000 | 0.252 | 1400/1400 |
| flat/1350/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 22.3 | 0.305 | 0.000 | 1400/1400 |
| flat/1350/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 16.9 | 1.000 | 0.000 | 1400/1325 |
| flat/1350/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 16.9 | 0.000 | 1.000 | 1325/1400 |
| flat/1350/skai.roc_rider/skai.windrunner | seat1 | eliminated | 18.7 | 0.000 | 0.298 | 1400/1350 |
| flat/1350/skai.windrunner/skai.roc_rider | seat0 | eliminated | 18.1 | 0.337 | 0.000 | 1350/1400 |
| flat/1350/skai.roc_rider/water.grasper | seat0 | eliminated | 29.4 | 1.000 | 0.000 | 1400/1400 |
| flat/1350/water.grasper/skai.roc_rider | seat1 | eliminated | 30.3 | 0.000 | 1.000 | 1400/1400 |
| flat/1350/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 11.4 | 1.000 | 0.000 | 1400/1330 |
| flat/1350/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 11.4 | 0.000 | 1.000 | 1330/1400 |
| flat/1350/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.915 | 1400/1520 |
| flat/1350/water.leviathan/skai.roc_rider | seat0 | eliminated | 4.5 | 0.915 | 0.000 | 1520/1400 |
| flat/1350/skai.roc_rider/water.reef_runner | seat0 | eliminated | 20.6 | 1.000 | 0.000 | 1400/1350 |
| flat/1350/water.reef_runner/skai.roc_rider | seat1 | eliminated | 20.6 | 0.000 | 1.000 | 1350/1400 |
| flat/1350/skai.roc_rider/water.shellback | seat0 | eliminated | 233.3 | 1.000 | 0.000 | 1400/1400 |
| flat/1350/water.shellback/skai.roc_rider | unresolved | time_cap | 300.0 | 0.359 | 1.000 | 1400/1400 |
| flat/1350/skai.roc_rider/water.spoutling | seat0 | eliminated | 12.7 | 0.670 | 0.000 | 1400/1375 |
| flat/1350/water.spoutling/skai.roc_rider | seat1 | eliminated | 13.7 | 0.000 | 0.674 | 1375/1400 |
| flat/1350/skai.roc_rider/skai_early | seat1 | eliminated | 32.7 | 0.000 | 0.084 | 1400/1275 |
| flat/1350/skai_early/skai.roc_rider | seat0 | eliminated | 36.8 | 0.042 | 0.000 | 1275/1400 |
| flat/1350/skai.roc_rider/water_early | seat0 | eliminated | 47.6 | 1.000 | 0.000 | 1400/1425 |
| flat/1350/water_early/skai.roc_rider | seat1 | eliminated | 48.4 | 0.000 | 1.000 | 1425/1400 |
| flat/1350/skai.roc_rider/water_rs | seat0 | eliminated | 31.4 | 1.000 | 0.000 | 1400/1425 |
| flat/1350/water_rs/skai.roc_rider | seat1 | eliminated | 31.5 | 0.000 | 1.000 | 1425/1400 |
| flat/1350/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 9.1 | 0.000 | 0.380 | 1330/1400 |
| flat/1350/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 9.3 | 0.272 | 0.000 | 1400/1330 |
| flat/1350/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1330/1325 |
| flat/1350/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 1325/1330 |
| flat/1350/skai.stormcaller/skai.windrunner | seat1 | eliminated | 9.3 | 0.000 | 0.228 | 1330/1350 |
| flat/1350/skai.windrunner/skai.stormcaller | seat0 | eliminated | 10.5 | 0.148 | 0.000 | 1350/1330 |
| flat/1350/skai.stormcaller/water.grasper | seat1 | eliminated | 12.5 | 0.000 | 0.114 | 1330/1400 |
| flat/1350/water.grasper/skai.stormcaller | seat0 | eliminated | 11.7 | 0.273 | 0.000 | 1400/1330 |
| flat/1350/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 8.8 | 0.000 | 0.269 | 1330/1330 |
| flat/1350/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 8.8 | 0.269 | 0.000 | 1330/1330 |
| flat/1350/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.938 | 1330/1520 |
| flat/1350/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.2 | 0.938 | 0.000 | 1520/1330 |
| flat/1350/skai.stormcaller/water.reef_runner | seat1 | eliminated | 11.2 | 0.000 | 0.160 | 1330/1350 |
| flat/1350/water.reef_runner/skai.stormcaller | seat1 | eliminated | 14.2 | 0.000 | 0.162 | 1350/1330 |
| flat/1350/skai.stormcaller/water.shellback | seat1 | eliminated | 14.7 | 0.000 | 0.957 | 1330/1400 |
| flat/1350/water.shellback/skai.stormcaller | seat0 | eliminated | 14.9 | 1.000 | 0.000 | 1400/1330 |
| flat/1350/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.3 | 0.802 | 0.000 | 1330/1375 |
| flat/1350/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.2 | 0.000 | 0.810 | 1375/1330 |
| flat/1350/skai.stormcaller/skai_early | seat1 | eliminated | 14.2 | 0.000 | 0.172 | 1330/1275 |
| flat/1350/skai_early/skai.stormcaller | seat0 | eliminated | 13.3 | 0.212 | 0.000 | 1275/1330 |
| flat/1350/skai.stormcaller/water_early | seat1 | eliminated | 12.6 | 0.000 | 0.529 | 1330/1425 |
| flat/1350/water_early/skai.stormcaller | seat0 | eliminated | 12.4 | 0.551 | 0.000 | 1425/1330 |
| flat/1350/skai.stormcaller/water_rs | seat1 | eliminated | 14.2 | 0.000 | 0.530 | 1330/1425 |
| flat/1350/water_rs/skai.stormcaller | seat0 | eliminated | 11.5 | 0.639 | 0.000 | 1425/1330 |
| flat/1350/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 12.1 | 0.000 | 0.512 | 1400/1325 |
| flat/1350/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 11.7 | 0.607 | 0.000 | 1325/1400 |
| flat/1350/skai.talon_archer/skai.windrunner | seat1 | eliminated | 20.9 | 0.000 | 0.126 | 1400/1350 |
| flat/1350/skai.windrunner/skai.talon_archer | seat0 | eliminated | 21.7 | 0.156 | 0.000 | 1350/1400 |
| flat/1350/skai.talon_archer/water.grasper | seat1 | eliminated | 14.9 | 0.000 | 0.569 | 1400/1400 |
| flat/1350/water.grasper/skai.talon_archer | seat0 | eliminated | 15.0 | 0.545 | 0.000 | 1400/1400 |
| flat/1350/skai.talon_archer/water.ink_lobber | seat0 | eliminated | 13.7 | 0.186 | 0.000 | 1400/1330 |
| flat/1350/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 14.5 | 0.000 | 0.043 | 1330/1400 |
| flat/1350/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.959 | 1400/1520 |
| flat/1350/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.980 | 0.000 | 1520/1400 |
| flat/1350/skai.talon_archer/water.reef_runner | seat1 | eliminated | 20.4 | 0.000 | 0.156 | 1400/1350 |
| flat/1350/water.reef_runner/skai.talon_archer | seat1 | eliminated | 27.0 | 0.000 | 0.033 | 1350/1400 |
| flat/1350/skai.talon_archer/water.shellback | seat1 | eliminated | 21.3 | 0.000 | 0.894 | 1400/1400 |
| flat/1350/water.shellback/skai.talon_archer | seat0 | eliminated | 21.2 | 0.915 | 0.000 | 1400/1400 |
| flat/1350/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.1 | 0.829 | 0.000 | 1400/1375 |
| flat/1350/water.spoutling/skai.talon_archer | seat1 | eliminated | 9.8 | 0.000 | 0.817 | 1375/1400 |
| flat/1350/skai.talon_archer/skai_early | seat0 | eliminated | 25.1 | 0.176 | 0.000 | 1400/1275 |
| flat/1350/skai_early/skai.talon_archer | seat1 | eliminated | 19.0 | 0.000 | 0.325 | 1275/1400 |
| flat/1350/skai.talon_archer/water_early | seat1 | eliminated | 15.7 | 0.000 | 0.781 | 1400/1425 |
| flat/1350/water_early/skai.talon_archer | seat0 | eliminated | 16.7 | 0.755 | 0.000 | 1425/1400 |
| flat/1350/skai.talon_archer/water_rs | seat1 | eliminated | 19.9 | 0.000 | 0.574 | 1400/1425 |
| flat/1350/water_rs/skai.talon_archer | seat0 | eliminated | 18.7 | 0.629 | 0.000 | 1425/1400 |
| flat/1350/skai.thunderhead/skai.windrunner | seat0 | eliminated | 8.9 | 0.681 | 0.000 | 1325/1350 |
| flat/1350/skai.windrunner/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 0.706 | 1350/1325 |
| flat/1350/skai.thunderhead/water.grasper | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1325/1400 |
| flat/1350/water.grasper/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 1400/1325 |
| flat/1350/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 4.5 | 1.000 | 0.000 | 1325/1330 |
| flat/1350/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1330/1325 |
| flat/1350/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.839 | 1325/1520 |
| flat/1350/water.leviathan/skai.thunderhead | seat0 | eliminated | 4.3 | 0.839 | 0.000 | 1520/1325 |
| flat/1350/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1325/1350 |
| flat/1350/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 1350/1325 |
| flat/1350/skai.thunderhead/water.shellback | seat0 | eliminated | 59.4 | 1.000 | 0.000 | 1325/1400 |
| flat/1350/water.shellback/skai.thunderhead | stalemate | dome_absorbed | 36.8 | 0.864 | 1.000 | 1400/1325 |
| flat/1350/skai.thunderhead/water.spoutling | seat0 | eliminated | 6.5 | 0.895 | 0.000 | 1325/1375 |
| flat/1350/water.spoutling/skai.thunderhead | seat1 | eliminated | 6.6 | 0.000 | 0.847 | 1375/1325 |
| flat/1350/skai.thunderhead/skai_early | seat0 | eliminated | 11.5 | 0.667 | 0.000 | 1325/1275 |
| flat/1350/skai_early/skai.thunderhead | seat1 | eliminated | 9.6 | 0.000 | 0.646 | 1275/1325 |
| flat/1350/skai.thunderhead/water_early | seat0 | eliminated | 14.1 | 1.000 | 0.000 | 1325/1425 |
| flat/1350/water_early/skai.thunderhead | seat1 | eliminated | 16.7 | 0.000 | 1.000 | 1425/1325 |
| flat/1350/skai.thunderhead/water_rs | seat0 | eliminated | 16.4 | 1.000 | 0.000 | 1325/1425 |
| flat/1350/water_rs/skai.thunderhead | seat1 | eliminated | 16.3 | 0.000 | 1.000 | 1425/1325 |
| flat/1350/skai.windrunner/water.grasper | seat1 | eliminated | 10.9 | 0.000 | 0.627 | 1350/1400 |
| flat/1350/water.grasper/skai.windrunner | seat0 | eliminated | 11.3 | 0.623 | 0.000 | 1400/1350 |
| flat/1350/skai.windrunner/water.ink_lobber | seat0 | eliminated | 19.6 | 0.006 | 0.000 | 1350/1330 |
| flat/1350/water.ink_lobber/skai.windrunner | seat0 | eliminated | 16.8 | 0.073 | 0.000 | 1330/1350 |
| flat/1350/skai.windrunner/water.leviathan | seat1 | eliminated | 2.2 | 0.000 | 1.000 | 1350/1520 |
| flat/1350/water.leviathan/skai.windrunner | seat0 | eliminated | 2.3 | 1.000 | 0.000 | 1520/1350 |
| flat/1350/skai.windrunner/water.reef_runner | seat1 | eliminated | 17.9 | 0.000 | 0.128 | 1350/1350 |
| flat/1350/water.reef_runner/skai.windrunner | seat1 | eliminated | 19.4 | 0.000 | 0.061 | 1350/1350 |
| flat/1350/skai.windrunner/water.shellback | seat1 | eliminated | 17.5 | 0.000 | 0.935 | 1350/1400 |
| flat/1350/water.shellback/skai.windrunner | seat0 | eliminated | 18.0 | 0.995 | 0.000 | 1400/1350 |
| flat/1350/skai.windrunner/water.spoutling | seat0 | eliminated | 8.3 | 0.778 | 0.000 | 1350/1375 |
| flat/1350/water.spoutling/skai.windrunner | seat1 | eliminated | 8.5 | 0.000 | 0.782 | 1375/1350 |
| flat/1350/skai.windrunner/skai_early | seat0 | eliminated | 14.0 | 0.411 | 0.000 | 1350/1275 |
| flat/1350/skai_early/skai.windrunner | seat1 | eliminated | 14.2 | 0.000 | 0.411 | 1275/1350 |
| flat/1350/skai.windrunner/water_early | seat1 | eliminated | 13.8 | 0.000 | 0.801 | 1350/1425 |
| flat/1350/water_early/skai.windrunner | seat0 | eliminated | 14.1 | 0.798 | 0.000 | 1425/1350 |
| flat/1350/skai.windrunner/water_rs | seat1 | eliminated | 14.7 | 0.000 | 0.669 | 1350/1425 |
| flat/1350/water_rs/skai.windrunner | seat0 | eliminated | 13.8 | 0.690 | 0.000 | 1425/1350 |
| flat/1350/water.grasper/water.ink_lobber | seat1 | eliminated | 16.9 | 0.000 | 0.183 | 1400/1330 |
| flat/1350/water.ink_lobber/water.grasper | seat0 | eliminated | 11.6 | 0.400 | 0.000 | 1330/1400 |
| flat/1350/water.grasper/water.leviathan | seat1 | eliminated | 4.7 | 0.000 | 1.000 | 1400/1520 |
| flat/1350/water.leviathan/water.grasper | seat0 | eliminated | 4.2 | 1.000 | 0.000 | 1520/1400 |
| flat/1350/water.grasper/water.reef_runner | seat0 | eliminated | 11.5 | 0.624 | 0.000 | 1400/1350 |
| flat/1350/water.reef_runner/water.grasper | seat1 | eliminated | 11.0 | 0.000 | 0.637 | 1350/1400 |
| flat/1350/water.grasper/water.shellback | seat0 | eliminated | 21.9 | 0.584 | 0.000 | 1400/1400 |
| flat/1350/water.shellback/water.grasper | seat1 | eliminated | 20.6 | 0.000 | 0.588 | 1400/1400 |
| flat/1350/water.grasper/water.spoutling | seat0 | eliminated | 9.6 | 0.799 | 0.000 | 1400/1375 |
| flat/1350/water.spoutling/water.grasper | seat1 | eliminated | 9.2 | 0.000 | 0.802 | 1375/1400 |
| flat/1350/water.grasper/skai_early | seat0 | eliminated | 11.6 | 0.666 | 0.000 | 1400/1275 |
| flat/1350/skai_early/water.grasper | seat1 | eliminated | 11.6 | 0.000 | 0.683 | 1275/1400 |
| flat/1350/water.grasper/water_early | seat0 | eliminated | 21.9 | 0.411 | 0.000 | 1400/1425 |
| flat/1350/water_early/water.grasper | seat1 | eliminated | 21.9 | 0.000 | 0.383 | 1425/1400 |
| flat/1350/water.grasper/water_rs | seat0 | eliminated | 14.9 | 0.656 | 0.000 | 1400/1425 |
| flat/1350/water_rs/water.grasper | seat1 | eliminated | 15.4 | 0.000 | 0.675 | 1425/1400 |
| flat/1350/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.6 | 0.000 | 0.831 | 1330/1520 |
| flat/1350/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.6 | 0.831 | 0.000 | 1520/1330 |
| flat/1350/water.ink_lobber/water.reef_runner | seat0 | eliminated | 13.9 | 0.123 | 0.000 | 1330/1350 |
| flat/1350/water.reef_runner/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.297 | 1350/1330 |
| flat/1350/water.ink_lobber/water.shellback | seat1 | eliminated | 15.7 | 0.000 | 0.889 | 1330/1400 |
| flat/1350/water.shellback/water.ink_lobber | seat0 | eliminated | 16.0 | 1.000 | 0.000 | 1400/1330 |
| flat/1350/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.9 | 0.836 | 0.000 | 1330/1375 |
| flat/1350/water.spoutling/water.ink_lobber | seat1 | eliminated | 7.9 | 0.000 | 0.814 | 1375/1330 |
| flat/1350/water.ink_lobber/skai_early | seat0 | eliminated | 14.2 | 0.293 | 0.000 | 1330/1275 |
| flat/1350/skai_early/water.ink_lobber | seat0 | eliminated | 14.5 | 0.239 | 0.000 | 1275/1330 |
| flat/1350/water.ink_lobber/water_early | seat1 | eliminated | 15.1 | 0.000 | 0.464 | 1330/1425 |
| flat/1350/water_early/water.ink_lobber | seat0 | eliminated | 18.6 | 0.328 | 0.000 | 1425/1330 |
| flat/1350/water.ink_lobber/water_rs | seat1 | eliminated | 16.8 | 0.000 | 0.494 | 1330/1425 |
| flat/1350/water_rs/water.ink_lobber | seat0 | eliminated | 12.8 | 0.580 | 0.000 | 1425/1330 |
| flat/1350/water.leviathan/water.reef_runner | seat0 | eliminated | 2.3 | 1.000 | 0.000 | 1520/1350 |
| flat/1350/water.reef_runner/water.leviathan | seat1 | eliminated | 2.3 | 0.000 | 1.000 | 1350/1520 |
| flat/1350/water.leviathan/water.shellback | seat1 | eliminated | 37.5 | 0.000 | 0.250 | 1520/1400 |
| flat/1350/water.shellback/water.leviathan | seat0 | eliminated | 31.0 | 0.562 | 0.000 | 1400/1520 |
| flat/1350/water.leviathan/water.spoutling | seat0 | eliminated | 2.9 | 1.000 | 0.000 | 1520/1375 |
| flat/1350/water.spoutling/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.996 | 1375/1520 |
| flat/1350/water.leviathan/skai_early | seat0 | eliminated | 4.0 | 0.964 | 0.000 | 1520/1275 |
| flat/1350/skai_early/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.959 | 1275/1520 |
| flat/1350/water.leviathan/water_early | seat0 | eliminated | 10.3 | 0.912 | 0.000 | 1520/1425 |
| flat/1350/water_early/water.leviathan | seat1 | eliminated | 10.2 | 0.000 | 0.951 | 1425/1520 |
| flat/1350/water.leviathan/water_rs | seat0 | eliminated | 10.0 | 0.950 | 0.000 | 1520/1425 |
| flat/1350/water_rs/water.leviathan | seat1 | eliminated | 10.0 | 0.000 | 0.924 | 1425/1520 |
| flat/1350/water.reef_runner/water.shellback | seat1 | eliminated | 18.4 | 0.000 | 0.949 | 1350/1400 |
| flat/1350/water.shellback/water.reef_runner | seat0 | eliminated | 18.7 | 0.980 | 0.000 | 1400/1350 |
| flat/1350/water.reef_runner/water.spoutling | seat0 | eliminated | 9.0 | 0.759 | 0.000 | 1350/1375 |
| flat/1350/water.spoutling/water.reef_runner | seat1 | eliminated | 8.8 | 0.000 | 0.778 | 1375/1350 |
| flat/1350/water.reef_runner/skai_early | seat0 | eliminated | 14.1 | 0.422 | 0.000 | 1350/1275 |
| flat/1350/skai_early/water.reef_runner | seat1 | eliminated | 15.7 | 0.000 | 0.367 | 1275/1350 |
| flat/1350/water.reef_runner/water_early | seat1 | eliminated | 13.5 | 0.000 | 0.799 | 1350/1425 |
| flat/1350/water_early/water.reef_runner | seat0 | eliminated | 13.8 | 0.817 | 0.000 | 1425/1350 |
| flat/1350/water.reef_runner/water_rs | seat1 | eliminated | 14.7 | 0.000 | 0.653 | 1350/1425 |
| flat/1350/water_rs/water.reef_runner | seat0 | eliminated | 14.6 | 0.681 | 0.000 | 1425/1350 |
| flat/1350/water.shellback/water.spoutling | seat0 | eliminated | 15.1 | 0.989 | 0.000 | 1400/1375 |
| flat/1350/water.spoutling/water.shellback | seat1 | eliminated | 15.2 | 0.000 | 0.983 | 1375/1400 |
| flat/1350/water.shellback/skai_early | seat0 | eliminated | 18.8 | 0.983 | 0.000 | 1400/1275 |
| flat/1350/skai_early/water.shellback | seat1 | eliminated | 18.7 | 0.000 | 0.939 | 1275/1400 |
| flat/1350/water.shellback/water_early | seat1 | eliminated | 28.6 | 0.000 | 0.812 | 1400/1425 |
| flat/1350/water_early/water.shellback | seat0 | eliminated | 30.5 | 0.750 | 0.000 | 1425/1400 |
| flat/1350/water.shellback/water_rs | seat0 | eliminated | 42.5 | 0.960 | 0.000 | 1400/1425 |
| flat/1350/water_rs/water.shellback | seat1 | eliminated | 41.6 | 0.000 | 0.898 | 1425/1400 |
| flat/1350/water.spoutling/skai_early | seat1 | eliminated | 10.1 | 0.000 | 0.755 | 1375/1275 |
| flat/1350/skai_early/water.spoutling | seat0 | eliminated | 10.0 | 0.745 | 0.000 | 1275/1375 |
| flat/1350/water.spoutling/water_early | seat1 | eliminated | 10.6 | 0.000 | 0.860 | 1375/1425 |
| flat/1350/water_early/water.spoutling | seat0 | eliminated | 11.1 | 0.850 | 0.000 | 1425/1375 |
| flat/1350/water.spoutling/water_rs | seat1 | eliminated | 11.3 | 0.000 | 0.864 | 1375/1425 |
| flat/1350/water_rs/water.spoutling | seat0 | eliminated | 11.2 | 0.873 | 0.000 | 1425/1375 |
| flat/1350/skai_early/water_early | seat1 | eliminated | 13.8 | 0.000 | 0.833 | 1275/1425 |
| flat/1350/water_early/skai_early | seat0 | eliminated | 14.0 | 0.814 | 0.000 | 1425/1275 |
| flat/1350/skai_early/water_rs | seat1 | eliminated | 15.1 | 0.000 | 0.752 | 1275/1425 |
| flat/1350/water_rs/skai_early | seat0 | eliminated | 15.5 | 0.693 | 0.000 | 1425/1275 |
| flat/1350/water_early/water_rs | seat0 | eliminated | 20.2 | 0.852 | 0.000 | 1425/1425 |
| flat/1350/water_rs/water_early | seat1 | eliminated | 20.9 | 0.000 | 0.819 | 1425/1425 |
| flat/1800/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.8 | 1.000 | 0.000 | 1800/1710 |
| flat/1800/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.8 | 0.000 | 1.000 | 1710/1800 |
| flat/1800/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 21.7 | 0.000 | 0.367 | 1800/1800 |
| flat/1800/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 22.7 | 0.346 | 0.000 | 1800/1800 |
| flat/1800/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 19.3 | 1.000 | 0.000 | 1800/1855 |
| flat/1800/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 19.3 | 0.000 | 1.000 | 1855/1800 |
| flat/1800/skai.roc_rider/skai.windrunner | seat1 | eliminated | 16.8 | 0.000 | 0.432 | 1800/1800 |
| flat/1800/skai.windrunner/skai.roc_rider | seat0 | eliminated | 17.3 | 0.419 | 0.000 | 1800/1800 |
| flat/1800/skai.roc_rider/water.grasper | seat0 | eliminated | 30.8 | 1.000 | 0.000 | 1800/1800 |
| flat/1800/water.grasper/skai.roc_rider | seat1 | eliminated | 30.3 | 0.000 | 1.000 | 1800/1800 |
| flat/1800/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 11.4 | 1.000 | 0.000 | 1800/1710 |
| flat/1800/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 11.4 | 0.000 | 1.000 | 1710/1800 |
| flat/1800/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.938 | 1800/1900 |
| flat/1800/water.leviathan/skai.roc_rider | seat0 | eliminated | 4.5 | 0.938 | 0.000 | 1900/1800 |
| flat/1800/skai.roc_rider/water.reef_runner | seat0 | eliminated | 20.6 | 1.000 | 0.000 | 1800/1800 |
| flat/1800/water.reef_runner/skai.roc_rider | seat1 | eliminated | 20.6 | 0.000 | 1.000 | 1800/1800 |
| flat/1800/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 36.6 | 1.000 | 0.957 | 1800/1750 |
| flat/1800/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 36.7 | 0.976 | 1.000 | 1750/1800 |
| flat/1800/skai.roc_rider/water.spoutling | seat0 | eliminated | 12.5 | 0.708 | 0.000 | 1800/1750 |
| flat/1800/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.6 | 0.000 | 0.705 | 1750/1800 |
| flat/1800/skai.roc_rider/skai_early | seat1 | eliminated | 24.3 | 0.000 | 0.294 | 1800/1700 |
| flat/1800/skai_early/skai.roc_rider | seat0 | eliminated | 24.0 | 0.307 | 0.000 | 1700/1800 |
| flat/1800/skai.roc_rider/water_early | seat0 | eliminated | 55.6 | 1.000 | 0.000 | 1800/1900 |
| flat/1800/water_early/skai.roc_rider | seat1 | eliminated | 49.6 | 0.000 | 1.000 | 1900/1800 |
| flat/1800/skai.roc_rider/water_rs | seat0 | eliminated | 37.5 | 1.000 | 0.000 | 1800/1900 |
| flat/1800/water_rs/skai.roc_rider | seat1 | eliminated | 37.4 | 0.000 | 1.000 | 1900/1800 |
| flat/1800/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 11.1 | 0.000 | 0.182 | 1710/1800 |
| flat/1800/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 10.7 | 0.226 | 0.000 | 1800/1710 |
| flat/1800/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 1710/1855 |
| flat/1800/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 4.4 | 1.000 | 0.000 | 1855/1710 |
| flat/1800/skai.stormcaller/skai.windrunner | seat1 | eliminated | 11.1 | 0.000 | 0.155 | 1710/1800 |
| flat/1800/skai.windrunner/skai.stormcaller | seat0 | eliminated | 10.8 | 0.145 | 0.000 | 1800/1710 |
| flat/1800/skai.stormcaller/water.grasper | seat1 | eliminated | 14.5 | 0.000 | 0.063 | 1710/1800 |
| flat/1800/water.grasper/skai.stormcaller | seat0 | eliminated | 12.8 | 0.133 | 0.000 | 1800/1710 |
| flat/1800/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 7.8 | 0.000 | 0.282 | 1710/1710 |
| flat/1800/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 7.8 | 0.282 | 0.000 | 1710/1710 |
| flat/1800/skai.stormcaller/water.leviathan | seat1 | eliminated | 3.1 | 0.000 | 0.967 | 1710/1900 |
| flat/1800/water.leviathan/skai.stormcaller | seat0 | eliminated | 3.1 | 0.967 | 0.000 | 1900/1710 |
| flat/1800/skai.stormcaller/water.reef_runner | seat0 | eliminated | 16.6 | 0.022 | 0.000 | 1710/1800 |
| flat/1800/water.reef_runner/skai.stormcaller | seat0 | eliminated | 10.4 | 0.258 | 0.000 | 1800/1710 |
| flat/1800/skai.stormcaller/water.shellback | seat1 | eliminated | 15.1 | 0.000 | 0.937 | 1710/1750 |
| flat/1800/water.shellback/skai.stormcaller | seat0 | eliminated | 15.1 | 1.000 | 0.000 | 1750/1710 |
| flat/1800/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.2 | 0.858 | 0.000 | 1710/1750 |
| flat/1800/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.2 | 0.000 | 0.840 | 1750/1710 |
| flat/1800/skai.stormcaller/skai_early | seat1 | eliminated | 12.7 | 0.000 | 0.240 | 1710/1700 |
| flat/1800/skai_early/skai.stormcaller | seat0 | eliminated | 12.6 | 0.354 | 0.000 | 1700/1710 |
| flat/1800/skai.stormcaller/water_early | seat1 | eliminated | 13.3 | 0.000 | 0.564 | 1710/1900 |
| flat/1800/water_early/skai.stormcaller | seat0 | eliminated | 13.2 | 0.533 | 0.000 | 1900/1710 |
| flat/1800/skai.stormcaller/water_rs | seat1 | eliminated | 10.8 | 0.000 | 0.656 | 1710/1900 |
| flat/1800/water_rs/skai.stormcaller | seat0 | eliminated | 9.6 | 0.877 | 0.000 | 1900/1710 |
| flat/1800/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 7.4 | 0.000 | 0.716 | 1800/1855 |
| flat/1800/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 7.1 | 0.720 | 0.000 | 1855/1800 |
| flat/1800/skai.talon_archer/skai.windrunner | seat1 | eliminated | 16.6 | 0.000 | 0.372 | 1800/1800 |
| flat/1800/skai.windrunner/skai.talon_archer | seat0 | eliminated | 17.6 | 0.303 | 0.000 | 1800/1800 |
| flat/1800/skai.talon_archer/water.grasper | seat1 | eliminated | 14.8 | 0.000 | 0.555 | 1800/1800 |
| flat/1800/water.grasper/skai.talon_archer | seat0 | eliminated | 15.0 | 0.539 | 0.000 | 1800/1800 |
| flat/1800/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 17.0 | 0.000 | 0.110 | 1800/1710 |
| flat/1800/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 14.7 | 0.185 | 0.000 | 1710/1800 |
| flat/1800/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.971 | 1800/1900 |
| flat/1800/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.988 | 0.000 | 1900/1800 |
| flat/1800/skai.talon_archer/water.reef_runner | seat1 | eliminated | 20.2 | 0.000 | 0.231 | 1800/1800 |
| flat/1800/water.reef_runner/skai.talon_archer | seat0 | eliminated | 19.1 | 0.258 | 0.000 | 1800/1800 |
| flat/1800/skai.talon_archer/water.shellback | seat1 | eliminated | 23.0 | 0.000 | 0.897 | 1800/1750 |
| flat/1800/water.shellback/skai.talon_archer | seat0 | eliminated | 22.2 | 0.954 | 0.000 | 1750/1800 |
| flat/1800/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.8 | 0.818 | 0.000 | 1800/1750 |
| flat/1800/water.spoutling/skai.talon_archer | seat1 | eliminated | 10.2 | 0.000 | 0.812 | 1750/1800 |
| flat/1800/skai.talon_archer/skai_early | seat0 | eliminated | 22.8 | 0.228 | 0.000 | 1800/1700 |
| flat/1800/skai_early/skai.talon_archer | seat1 | eliminated | 19.4 | 0.000 | 0.336 | 1700/1800 |
| flat/1800/skai.talon_archer/water_early | seat1 | eliminated | 16.3 | 0.000 | 0.759 | 1800/1900 |
| flat/1800/water_early/skai.talon_archer | seat0 | eliminated | 17.4 | 0.717 | 0.000 | 1900/1800 |
| flat/1800/skai.talon_archer/water_rs | seat1 | eliminated | 17.4 | 0.000 | 0.654 | 1800/1900 |
| flat/1800/water_rs/skai.talon_archer | seat0 | eliminated | 16.2 | 0.696 | 0.000 | 1900/1800 |
| flat/1800/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.6 | 0.752 | 0.000 | 1855/1800 |
| flat/1800/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 0.807 | 1800/1855 |
| flat/1800/skai.thunderhead/water.grasper | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 1855/1800 |
| flat/1800/water.grasper/skai.thunderhead | seat1 | eliminated | 7.0 | 0.000 | 1.000 | 1800/1855 |
| flat/1800/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 4.5 | 1.000 | 0.000 | 1855/1710 |
| flat/1800/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1710/1855 |
| flat/1800/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.771 | 1855/1900 |
| flat/1800/water.leviathan/skai.thunderhead | seat0 | eliminated | 4.2 | 0.771 | 0.000 | 1900/1855 |
| flat/1800/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.3 | 1.000 | 0.000 | 1855/1800 |
| flat/1800/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.3 | 0.000 | 1.000 | 1800/1855 |
| flat/1800/skai.thunderhead/water.shellback | seat0 | eliminated | 31.9 | 1.000 | 0.000 | 1855/1750 |
| flat/1800/water.shellback/skai.thunderhead | seat1 | eliminated | 41.7 | 0.000 | 1.000 | 1750/1855 |
| flat/1800/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.3 | 0.917 | 0.000 | 1855/1750 |
| flat/1800/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.3 | 0.000 | 0.902 | 1750/1855 |
| flat/1800/skai.thunderhead/skai_early | seat0 | eliminated | 8.9 | 0.730 | 0.000 | 1855/1700 |
| flat/1800/skai_early/skai.thunderhead | seat1 | eliminated | 7.1 | 0.000 | 0.779 | 1700/1855 |
| flat/1800/skai.thunderhead/water_early | seat0 | eliminated | 14.1 | 1.000 | 0.000 | 1855/1900 |
| flat/1800/water_early/skai.thunderhead | seat1 | eliminated | 16.7 | 0.000 | 1.000 | 1900/1855 |
| flat/1800/skai.thunderhead/water_rs | seat0 | eliminated | 16.5 | 1.000 | 0.000 | 1855/1900 |
| flat/1800/water_rs/skai.thunderhead | seat1 | eliminated | 13.9 | 0.000 | 1.000 | 1900/1855 |
| flat/1800/skai.windrunner/water.grasper | seat1 | eliminated | 11.6 | 0.000 | 0.613 | 1800/1800 |
| flat/1800/water.grasper/skai.windrunner | seat0 | eliminated | 12.1 | 0.580 | 0.000 | 1800/1800 |
| flat/1800/skai.windrunner/water.ink_lobber | seat1 | eliminated | 13.7 | 0.000 | 0.276 | 1800/1710 |
| flat/1800/water.ink_lobber/skai.windrunner | seat1 | eliminated | 13.3 | 0.000 | 0.174 | 1710/1800 |
| flat/1800/skai.windrunner/water.leviathan | seat1 | eliminated | 1.9 | 0.000 | 1.000 | 1800/1900 |
| flat/1800/water.leviathan/skai.windrunner | seat0 | eliminated | 2.7 | 1.000 | 0.000 | 1900/1800 |
| flat/1800/skai.windrunner/water.reef_runner | seat0 | eliminated | 21.7 | 0.092 | 0.000 | 1800/1800 |
| flat/1800/water.reef_runner/skai.windrunner | seat1 | eliminated | 22.4 | 0.000 | 0.017 | 1800/1800 |
| flat/1800/skai.windrunner/water.shellback | seat1 | eliminated | 19.1 | 0.000 | 0.937 | 1800/1750 |
| flat/1800/water.shellback/skai.windrunner | seat0 | eliminated | 19.2 | 0.900 | 0.000 | 1750/1800 |
| flat/1800/skai.windrunner/water.spoutling | seat0 | eliminated | 8.5 | 0.792 | 0.000 | 1800/1750 |
| flat/1800/water.spoutling/skai.windrunner | seat1 | eliminated | 8.7 | 0.000 | 0.788 | 1750/1800 |
| flat/1800/skai.windrunner/skai_early | seat0 | eliminated | 15.1 | 0.386 | 0.000 | 1800/1700 |
| flat/1800/skai_early/skai.windrunner | seat1 | eliminated | 16.0 | 0.000 | 0.372 | 1700/1800 |
| flat/1800/skai.windrunner/water_early | seat1 | eliminated | 13.5 | 0.000 | 0.827 | 1800/1900 |
| flat/1800/water_early/skai.windrunner | seat0 | eliminated | 14.0 | 0.789 | 0.000 | 1900/1800 |
| flat/1800/skai.windrunner/water_rs | seat1 | eliminated | 15.6 | 0.000 | 0.643 | 1800/1900 |
| flat/1800/water_rs/skai.windrunner | seat0 | eliminated | 13.2 | 0.744 | 0.000 | 1900/1800 |
| flat/1800/water.grasper/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.284 | 1800/1710 |
| flat/1800/water.ink_lobber/water.grasper | seat0 | eliminated | 13.9 | 0.298 | 0.000 | 1710/1800 |
| flat/1800/water.grasper/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 1.000 | 1800/1900 |
| flat/1800/water.leviathan/water.grasper | seat0 | eliminated | 4.2 | 1.000 | 0.000 | 1900/1800 |
| flat/1800/water.grasper/water.reef_runner | seat0 | eliminated | 12.3 | 0.605 | 0.000 | 1800/1800 |
| flat/1800/water.reef_runner/water.grasper | seat1 | eliminated | 11.7 | 0.000 | 0.621 | 1800/1800 |
| flat/1800/water.grasper/water.shellback | seat0 | eliminated | 20.8 | 0.616 | 0.000 | 1800/1750 |
| flat/1800/water.shellback/water.grasper | seat1 | eliminated | 20.2 | 0.000 | 0.600 | 1750/1800 |
| flat/1800/water.grasper/water.spoutling | seat0 | eliminated | 9.5 | 0.828 | 0.000 | 1800/1750 |
| flat/1800/water.spoutling/water.grasper | seat1 | eliminated | 9.3 | 0.000 | 0.821 | 1750/1800 |
| flat/1800/water.grasper/skai_early | seat0 | eliminated | 12.3 | 0.646 | 0.000 | 1800/1700 |
| flat/1800/skai_early/water.grasper | seat1 | eliminated | 11.9 | 0.000 | 0.670 | 1700/1800 |
| flat/1800/water.grasper/water_early | seat0 | eliminated | 24.6 | 0.380 | 0.000 | 1800/1900 |
| flat/1800/water_early/water.grasper | seat1 | eliminated | 26.5 | 0.000 | 0.316 | 1900/1800 |
| flat/1800/water.grasper/water_rs | seat0 | eliminated | 16.4 | 0.598 | 0.000 | 1800/1900 |
| flat/1800/water_rs/water.grasper | seat1 | eliminated | 16.3 | 0.000 | 0.621 | 1900/1800 |
| flat/1800/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.887 | 1710/1900 |
| flat/1800/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.5 | 0.887 | 0.000 | 1900/1710 |
| flat/1800/water.ink_lobber/water.reef_runner | seat0 | eliminated | 11.1 | 0.429 | 0.000 | 1710/1800 |
| flat/1800/water.reef_runner/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.318 | 1800/1710 |
| flat/1800/water.ink_lobber/water.shellback | seat1 | eliminated | 16.5 | 0.000 | 0.861 | 1710/1750 |
| flat/1800/water.shellback/water.ink_lobber | seat0 | eliminated | 16.1 | 0.935 | 0.000 | 1750/1710 |
| flat/1800/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.9 | 0.878 | 0.000 | 1710/1750 |
| flat/1800/water.spoutling/water.ink_lobber | seat1 | eliminated | 7.9 | 0.000 | 0.878 | 1750/1710 |
| flat/1800/water.ink_lobber/skai_early | seat0 | eliminated | 16.9 | 0.242 | 0.000 | 1710/1700 |
| flat/1800/skai_early/water.ink_lobber | seat1 | eliminated | 19.9 | 0.000 | 0.133 | 1700/1710 |
| flat/1800/water.ink_lobber/water_early | seat1 | eliminated | 16.9 | 0.000 | 0.466 | 1710/1900 |
| flat/1800/water_early/water.ink_lobber | seat0 | eliminated | 21.3 | 0.296 | 0.000 | 1900/1710 |
| flat/1800/water.ink_lobber/water_rs | seat1 | eliminated | 13.9 | 0.000 | 0.608 | 1710/1900 |
| flat/1800/water_rs/water.ink_lobber | seat0 | eliminated | 11.2 | 0.837 | 0.000 | 1900/1710 |
| flat/1800/water.leviathan/water.reef_runner | seat0 | eliminated | 2.8 | 1.000 | 0.000 | 1900/1800 |
| flat/1800/water.reef_runner/water.leviathan | seat1 | eliminated | 2.0 | 0.000 | 1.000 | 1800/1900 |
| flat/1800/water.leviathan/water.shellback | seat0 | eliminated | 20.9 | 0.525 | 0.000 | 1900/1750 |
| flat/1800/water.shellback/water.leviathan | seat0 | eliminated | 31.0 | 0.800 | 0.000 | 1750/1900 |
| flat/1800/water.leviathan/water.spoutling | seat0 | eliminated | 2.9 | 1.000 | 0.000 | 1900/1750 |
| flat/1800/water.spoutling/water.leviathan | seat1 | eliminated | 2.9 | 0.000 | 1.000 | 1750/1900 |
| flat/1800/water.leviathan/skai_early | seat0 | eliminated | 4.0 | 0.980 | 0.000 | 1900/1700 |
| flat/1800/skai_early/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.955 | 1700/1900 |
| flat/1800/water.leviathan/water_early | seat0 | eliminated | 10.1 | 0.971 | 0.000 | 1900/1900 |
| flat/1800/water_early/water.leviathan | seat1 | eliminated | 8.3 | 0.000 | 0.980 | 1900/1900 |
| flat/1800/water.leviathan/water_rs | seat0 | eliminated | 10.0 | 0.881 | 0.000 | 1900/1900 |
| flat/1800/water_rs/water.leviathan | seat1 | eliminated | 10.1 | 0.000 | 0.860 | 1900/1900 |
| flat/1800/water.reef_runner/water.shellback | seat1 | eliminated | 19.3 | 0.000 | 0.922 | 1800/1750 |
| flat/1800/water.shellback/water.reef_runner | seat0 | eliminated | 19.1 | 0.952 | 0.000 | 1750/1800 |
| flat/1800/water.reef_runner/water.spoutling | seat0 | eliminated | 9.1 | 0.767 | 0.000 | 1800/1750 |
| flat/1800/water.spoutling/water.reef_runner | seat1 | eliminated | 8.9 | 0.000 | 0.774 | 1750/1800 |
| flat/1800/water.reef_runner/skai_early | seat0 | eliminated | 14.6 | 0.421 | 0.000 | 1800/1700 |
| flat/1800/skai_early/water.reef_runner | seat1 | eliminated | 15.8 | 0.000 | 0.383 | 1700/1800 |
| flat/1800/water.reef_runner/water_early | seat1 | eliminated | 13.1 | 0.000 | 0.852 | 1800/1900 |
| flat/1800/water_early/water.reef_runner | seat0 | eliminated | 14.7 | 0.781 | 0.000 | 1900/1800 |
| flat/1800/water.reef_runner/water_rs | seat1 | eliminated | 15.1 | 0.000 | 0.632 | 1800/1900 |
| flat/1800/water_rs/water.reef_runner | seat0 | eliminated | 13.3 | 0.760 | 0.000 | 1900/1800 |
| flat/1800/water.shellback/water.spoutling | seat0 | eliminated | 15.5 | 0.995 | 0.000 | 1750/1750 |
| flat/1800/water.spoutling/water.shellback | seat1 | eliminated | 15.5 | 0.000 | 0.986 | 1750/1750 |
| flat/1800/water.shellback/skai_early | seat0 | eliminated | 20.1 | 0.984 | 0.000 | 1750/1700 |
| flat/1800/skai_early/water.shellback | seat1 | eliminated | 19.5 | 0.000 | 0.935 | 1700/1750 |
| flat/1800/water.shellback/water_early | seat1 | eliminated | 27.7 | 0.000 | 0.816 | 1750/1900 |
| flat/1800/water_early/water.shellback | seat0 | eliminated | 28.7 | 0.817 | 0.000 | 1900/1750 |
| flat/1800/water.shellback/water_rs | seat0 | eliminated | 45.6 | 0.968 | 0.000 | 1750/1900 |
| flat/1800/water_rs/water.shellback | seat1 | eliminated | 41.0 | 0.000 | 0.962 | 1900/1750 |
| flat/1800/water.spoutling/skai_early | seat1 | eliminated | 10.0 | 0.000 | 0.750 | 1750/1700 |
| flat/1800/skai_early/water.spoutling | seat0 | eliminated | 10.2 | 0.757 | 0.000 | 1700/1750 |
| flat/1800/water.spoutling/water_early | seat1 | eliminated | 10.5 | 0.000 | 0.902 | 1750/1900 |
| flat/1800/water_early/water.spoutling | seat0 | eliminated | 10.9 | 0.880 | 0.000 | 1900/1750 |
| flat/1800/water.spoutling/water_rs | seat1 | eliminated | 11.4 | 0.000 | 0.885 | 1750/1900 |
| flat/1800/water_rs/water.spoutling | seat0 | eliminated | 10.5 | 0.957 | 0.000 | 1900/1750 |
| flat/1800/skai_early/water_early | seat1 | eliminated | 13.9 | 0.000 | 0.861 | 1700/1900 |
| flat/1800/water_early/skai_early | seat0 | eliminated | 13.6 | 0.812 | 0.000 | 1900/1700 |
| flat/1800/skai_early/water_rs | seat1 | eliminated | 14.1 | 0.000 | 0.786 | 1700/1900 |
| flat/1800/water_rs/skai_early | seat0 | eliminated | 13.8 | 0.821 | 0.000 | 1900/1700 |
| flat/1800/water_early/water_rs | seat0 | eliminated | 21.2 | 0.792 | 0.000 | 1900/1900 |
| flat/1800/water_rs/water_early | seat1 | eliminated | 19.1 | 0.000 | 0.851 | 1900/1900 |
| flat/2400/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 11.4 | 1.000 | 0.000 | 2400/2470 |
| flat/2400/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 11.3 | 0.000 | 1.000 | 2470/2400 |
| flat/2400/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 22.2 | 0.000 | 0.304 | 2400/2400 |
| flat/2400/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 22.7 | 0.330 | 0.000 | 2400/2400 |
| flat/2400/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 19.2 | 1.000 | 0.000 | 2400/2385 |
| flat/2400/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 19.2 | 0.000 | 1.000 | 2385/2400 |
| flat/2400/skai.roc_rider/skai.windrunner | seat1 | eliminated | 17.9 | 0.000 | 0.447 | 2400/2400 |
| flat/2400/skai.windrunner/skai.roc_rider | seat0 | eliminated | 17.5 | 0.427 | 0.000 | 2400/2400 |
| flat/2400/skai.roc_rider/water.grasper | seat0 | eliminated | 29.2 | 1.000 | 0.000 | 2400/2400 |
| flat/2400/water.grasper/skai.roc_rider | seat1 | eliminated | 30.3 | 0.000 | 1.000 | 2400/2400 |
| flat/2400/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 12.6 | 1.000 | 0.000 | 2400/2470 |
| flat/2400/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 13.8 | 0.000 | 1.000 | 2470/2400 |
| flat/2400/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.943 | 2400/2280 |
| flat/2400/water.leviathan/skai.roc_rider | seat0 | eliminated | 4.4 | 0.943 | 0.000 | 2280/2400 |
| flat/2400/skai.roc_rider/water.reef_runner | seat0 | eliminated | 19.4 | 1.000 | 0.000 | 2400/2400 |
| flat/2400/water.reef_runner/skai.roc_rider | seat1 | eliminated | 20.8 | 0.000 | 1.000 | 2400/2400 |
| flat/2400/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 129.2 | 1.000 | 0.714 | 2400/2450 |
| flat/2400/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 250.4 | 0.854 | 1.000 | 2450/2400 |
| flat/2400/skai.roc_rider/water.spoutling | seat0 | eliminated | 14.0 | 0.685 | 0.000 | 2400/2375 |
| flat/2400/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.9 | 0.000 | 0.706 | 2375/2400 |
| flat/2400/skai.roc_rider/skai_early | seat1 | eliminated | 19.0 | 0.000 | 0.486 | 2400/2550 |
| flat/2400/skai_early/skai.roc_rider | seat0 | eliminated | 19.8 | 0.434 | 0.000 | 2550/2400 |
| flat/2400/skai.roc_rider/water_early | seat0 | eliminated | 47.1 | 1.000 | 0.000 | 2400/2375 |
| flat/2400/water_early/skai.roc_rider | seat1 | eliminated | 43.5 | 0.000 | 1.000 | 2375/2400 |
| flat/2400/skai.roc_rider/water_rs | seat0 | eliminated | 37.5 | 1.000 | 0.000 | 2400/2375 |
| flat/2400/water_rs/skai.roc_rider | seat1 | eliminated | 36.3 | 0.000 | 1.000 | 2375/2400 |
| flat/2400/skai.stormcaller/skai.talon_archer | seat0 | eliminated | 15.2 | 0.176 | 0.000 | 2470/2400 |
| flat/2400/skai.talon_archer/skai.stormcaller | seat1 | eliminated | 17.5 | 0.000 | 0.147 | 2400/2470 |
| flat/2400/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.6 | 0.000 | 1.000 | 2470/2385 |
| flat/2400/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 4.7 | 1.000 | 0.000 | 2385/2470 |
| flat/2400/skai.stormcaller/skai.windrunner | seat1 | eliminated | 16.5 | 0.000 | 0.021 | 2470/2400 |
| flat/2400/skai.windrunner/skai.stormcaller | seat0 | eliminated | 13.8 | 0.069 | 0.000 | 2400/2470 |
| flat/2400/skai.stormcaller/water.grasper | seat0 | eliminated | 14.5 | 0.236 | 0.000 | 2470/2400 |
| flat/2400/water.grasper/skai.stormcaller | seat1 | eliminated | 12.2 | 0.000 | 0.415 | 2400/2470 |
| flat/2400/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.0 | 0.000 | 0.178 | 2470/2470 |
| flat/2400/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 9.4 | 0.115 | 0.000 | 2470/2470 |
| flat/2400/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.930 | 2470/2280 |
| flat/2400/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.3 | 0.944 | 0.000 | 2280/2470 |
| flat/2400/skai.stormcaller/water.reef_runner | seat1 | eliminated | 16.2 | 0.000 | 0.056 | 2470/2400 |
| flat/2400/water.reef_runner/skai.stormcaller | seat1 | eliminated | 14.5 | 0.000 | 0.210 | 2400/2470 |
| flat/2400/skai.stormcaller/water.shellback | seat1 | eliminated | 16.2 | 0.000 | 0.880 | 2470/2450 |
| flat/2400/water.shellback/skai.stormcaller | seat0 | eliminated | 15.7 | 1.000 | 0.000 | 2450/2470 |
| flat/2400/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.1 | 0.829 | 0.000 | 2470/2375 |
| flat/2400/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.2 | 0.000 | 0.829 | 2375/2470 |
| flat/2400/skai.stormcaller/skai_early | seat1 | eliminated | 12.7 | 0.000 | 0.242 | 2470/2550 |
| flat/2400/skai_early/skai.stormcaller | seat0 | eliminated | 12.7 | 0.325 | 0.000 | 2550/2470 |
| flat/2400/skai.stormcaller/water_early | seat1 | eliminated | 16.6 | 0.000 | 0.473 | 2470/2375 |
| flat/2400/water_early/skai.stormcaller | seat0 | eliminated | 17.8 | 0.366 | 0.000 | 2375/2470 |
| flat/2400/skai.stormcaller/water_rs | seat1 | eliminated | 13.2 | 0.000 | 0.547 | 2470/2375 |
| flat/2400/water_rs/skai.stormcaller | seat0 | eliminated | 10.6 | 0.831 | 0.000 | 2375/2470 |
| flat/2400/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 9.1 | 0.000 | 0.706 | 2400/2385 |
| flat/2400/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 7.1 | 0.724 | 0.000 | 2385/2400 |
| flat/2400/skai.talon_archer/skai.windrunner | seat1 | eliminated | 22.5 | 0.000 | 0.142 | 2400/2400 |
| flat/2400/skai.windrunner/skai.talon_archer | seat0 | eliminated | 18.2 | 0.306 | 0.000 | 2400/2400 |
| flat/2400/skai.talon_archer/water.grasper | seat1 | eliminated | 14.7 | 0.000 | 0.593 | 2400/2400 |
| flat/2400/water.grasper/skai.talon_archer | seat0 | eliminated | 15.1 | 0.575 | 0.000 | 2400/2400 |
| flat/2400/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 11.6 | 0.000 | 0.415 | 2400/2470 |
| flat/2400/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 11.3 | 0.459 | 0.000 | 2470/2400 |
| flat/2400/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.7 | 0.000 | 0.932 | 2400/2280 |
| flat/2400/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.952 | 0.000 | 2280/2400 |
| flat/2400/skai.talon_archer/water.reef_runner | seat1 | eliminated | 28.2 | 0.000 | 0.060 | 2400/2400 |
| flat/2400/water.reef_runner/skai.talon_archer | seat0 | eliminated | 26.6 | 0.079 | 0.000 | 2400/2400 |
| flat/2400/skai.talon_archer/water.shellback | seat1 | eliminated | 23.1 | 0.000 | 0.907 | 2400/2450 |
| flat/2400/water.shellback/skai.talon_archer | seat0 | eliminated | 22.8 | 0.901 | 0.000 | 2450/2400 |
| flat/2400/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.9 | 0.817 | 0.000 | 2400/2375 |
| flat/2400/water.spoutling/skai.talon_archer | seat1 | eliminated | 10.1 | 0.000 | 0.826 | 2375/2400 |
| flat/2400/skai.talon_archer/skai_early | seat1 | eliminated | 24.4 | 0.000 | 0.207 | 2400/2550 |
| flat/2400/skai_early/skai.talon_archer | seat0 | eliminated | 21.4 | 0.282 | 0.000 | 2550/2400 |
| flat/2400/skai.talon_archer/water_early | seat1 | eliminated | 17.8 | 0.000 | 0.716 | 2400/2375 |
| flat/2400/water_early/skai.talon_archer | seat0 | eliminated | 18.2 | 0.716 | 0.000 | 2375/2400 |
| flat/2400/skai.talon_archer/water_rs | seat1 | eliminated | 18.7 | 0.000 | 0.626 | 2400/2375 |
| flat/2400/water_rs/skai.talon_archer | seat0 | eliminated | 17.8 | 0.719 | 0.000 | 2375/2400 |
| flat/2400/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.6 | 0.800 | 0.000 | 2385/2400 |
| flat/2400/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.3 | 0.000 | 0.827 | 2400/2385 |
| flat/2400/skai.thunderhead/water.grasper | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 2385/2400 |
| flat/2400/water.grasper/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 2400/2385 |
| flat/2400/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 2385/2470 |
| flat/2400/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 6.8 | 0.000 | 1.000 | 2470/2385 |
| flat/2400/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.735 | 2385/2280 |
| flat/2400/water.leviathan/skai.thunderhead | seat0 | eliminated | 4.3 | 0.710 | 0.000 | 2280/2385 |
| flat/2400/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 2385/2400 |
| flat/2400/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 2400/2385 |
| flat/2400/skai.thunderhead/water.shellback | seat0 | eliminated | 36.6 | 1.000 | 0.000 | 2385/2450 |
| flat/2400/water.shellback/skai.thunderhead | seat1 | eliminated | 69.4 | 0.000 | 1.000 | 2450/2385 |
| flat/2400/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.5 | 0.886 | 0.000 | 2385/2375 |
| flat/2400/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 0.874 | 2375/2385 |
| flat/2400/skai.thunderhead/skai_early | seat0 | eliminated | 7.3 | 0.727 | 0.000 | 2385/2550 |
| flat/2400/skai_early/skai.thunderhead | seat1 | eliminated | 8.8 | 0.000 | 0.708 | 2550/2385 |
| flat/2400/skai.thunderhead/water_early | seat0 | eliminated | 11.8 | 1.000 | 0.000 | 2385/2375 |
| flat/2400/water_early/skai.thunderhead | seat1 | eliminated | 14.0 | 0.000 | 1.000 | 2375/2385 |
| flat/2400/skai.thunderhead/water_rs | seat0 | eliminated | 11.8 | 1.000 | 0.000 | 2385/2375 |
| flat/2400/water_rs/skai.thunderhead | seat1 | eliminated | 11.7 | 0.000 | 1.000 | 2375/2385 |
| flat/2400/skai.windrunner/water.grasper | seat1 | eliminated | 12.1 | 0.000 | 0.607 | 2400/2400 |
| flat/2400/water.grasper/skai.windrunner | seat0 | eliminated | 12.1 | 0.609 | 0.000 | 2400/2400 |
| flat/2400/skai.windrunner/water.ink_lobber | seat1 | eliminated | 10.7 | 0.000 | 0.567 | 2400/2470 |
| flat/2400/water.ink_lobber/skai.windrunner | seat0 | eliminated | 11.0 | 0.482 | 0.000 | 2470/2400 |
| flat/2400/skai.windrunner/water.leviathan | seat1 | eliminated | 2.6 | 0.000 | 1.000 | 2400/2280 |
| flat/2400/water.leviathan/skai.windrunner | seat0 | eliminated | 3.0 | 1.000 | 0.000 | 2280/2400 |
| flat/2400/skai.windrunner/water.reef_runner | seat0 | eliminated | 20.0 | 0.112 | 0.000 | 2400/2400 |
| flat/2400/water.reef_runner/skai.windrunner | seat1 | eliminated | 20.1 | 0.000 | 0.103 | 2400/2400 |
| flat/2400/skai.windrunner/water.shellback | seat1 | eliminated | 19.3 | 0.000 | 0.926 | 2400/2450 |
| flat/2400/water.shellback/skai.windrunner | seat0 | eliminated | 19.5 | 0.917 | 0.000 | 2450/2400 |
| flat/2400/skai.windrunner/water.spoutling | seat0 | eliminated | 8.9 | 0.802 | 0.000 | 2400/2375 |
| flat/2400/water.spoutling/skai.windrunner | seat1 | eliminated | 8.7 | 0.000 | 0.802 | 2375/2400 |
| flat/2400/skai.windrunner/skai_early | seat0 | eliminated | 23.5 | 0.123 | 0.000 | 2400/2550 |
| flat/2400/skai_early/skai.windrunner | seat1 | eliminated | 24.2 | 0.000 | 0.110 | 2550/2400 |
| flat/2400/skai.windrunner/water_early | seat1 | eliminated | 15.5 | 0.000 | 0.746 | 2400/2375 |
| flat/2400/water_early/skai.windrunner | seat0 | eliminated | 15.5 | 0.729 | 0.000 | 2375/2400 |
| flat/2400/skai.windrunner/water_rs | seat1 | eliminated | 15.7 | 0.000 | 0.642 | 2400/2375 |
| flat/2400/water_rs/skai.windrunner | seat0 | eliminated | 14.8 | 0.727 | 0.000 | 2375/2400 |
| flat/2400/water.grasper/water.ink_lobber | seat1 | eliminated | 10.9 | 0.000 | 0.631 | 2400/2470 |
| flat/2400/water.ink_lobber/water.grasper | seat0 | eliminated | 11.3 | 0.591 | 0.000 | 2470/2400 |
| flat/2400/water.grasper/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 1.000 | 2400/2280 |
| flat/2400/water.leviathan/water.grasper | seat0 | eliminated | 5.0 | 1.000 | 0.000 | 2280/2400 |
| flat/2400/water.grasper/water.reef_runner | seat0 | eliminated | 12.2 | 0.603 | 0.000 | 2400/2400 |
| flat/2400/water.reef_runner/water.grasper | seat1 | eliminated | 12.1 | 0.000 | 0.615 | 2400/2400 |
| flat/2400/water.grasper/water.shellback | seat0 | eliminated | 22.4 | 0.566 | 0.000 | 2400/2450 |
| flat/2400/water.shellback/water.grasper | seat1 | eliminated | 24.1 | 0.000 | 0.567 | 2450/2400 |
| flat/2400/water.grasper/water.spoutling | seat0 | eliminated | 9.7 | 0.812 | 0.000 | 2400/2375 |
| flat/2400/water.spoutling/water.grasper | seat1 | eliminated | 9.4 | 0.000 | 0.814 | 2375/2400 |
| flat/2400/water.grasper/skai_early | seat0 | eliminated | 14.1 | 0.575 | 0.000 | 2400/2550 |
| flat/2400/skai_early/water.grasper | seat1 | eliminated | 14.0 | 0.000 | 0.592 | 2550/2400 |
| flat/2400/water.grasper/water_early | seat0 | eliminated | 22.0 | 0.484 | 0.000 | 2400/2375 |
| flat/2400/water_early/water.grasper | seat1 | eliminated | 21.8 | 0.000 | 0.456 | 2375/2400 |
| flat/2400/water.grasper/water_rs | seat0 | eliminated | 15.5 | 0.644 | 0.000 | 2400/2375 |
| flat/2400/water_rs/water.grasper | seat1 | eliminated | 15.4 | 0.000 | 0.642 | 2375/2400 |
| flat/2400/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.9 | 0.000 | 0.869 | 2470/2280 |
| flat/2400/water.leviathan/water.ink_lobber | seat0 | eliminated | 5.0 | 0.869 | 0.000 | 2280/2470 |
| flat/2400/water.ink_lobber/water.reef_runner | seat0 | eliminated | 10.7 | 0.663 | 0.000 | 2470/2400 |
| flat/2400/water.reef_runner/water.ink_lobber | seat1 | eliminated | 10.7 | 0.000 | 0.551 | 2400/2470 |
| flat/2400/water.ink_lobber/water.shellback | seat1 | eliminated | 17.8 | 0.000 | 0.894 | 2470/2450 |
| flat/2400/water.shellback/water.ink_lobber | seat0 | eliminated | 17.5 | 0.905 | 0.000 | 2450/2470 |
| flat/2400/water.ink_lobber/water.spoutling | seat0 | eliminated | 5.6 | 0.915 | 0.000 | 2470/2375 |
| flat/2400/water.spoutling/water.ink_lobber | seat1 | eliminated | 6.1 | 0.000 | 0.908 | 2375/2470 |
| flat/2400/water.ink_lobber/skai_early | seat0 | eliminated | 14.5 | 0.312 | 0.000 | 2470/2550 |
| flat/2400/skai_early/water.ink_lobber | seat1 | eliminated | 14.4 | 0.000 | 0.300 | 2550/2470 |
| flat/2400/water.ink_lobber/water_early | seat1 | eliminated | 30.2 | 0.000 | 0.174 | 2470/2375 |
| flat/2400/water_early/water.ink_lobber | seat1 | eliminated | 17.7 | 0.000 | 0.458 | 2375/2470 |
| flat/2400/water.ink_lobber/water_rs | seat1 | eliminated | 27.2 | 0.000 | 0.034 | 2470/2375 |
| flat/2400/water_rs/water.ink_lobber | seat0 | eliminated | 12.7 | 0.562 | 0.000 | 2375/2470 |
| flat/2400/water.leviathan/water.reef_runner | seat0 | eliminated | 2.7 | 1.000 | 0.000 | 2280/2400 |
| flat/2400/water.reef_runner/water.leviathan | seat1 | eliminated | 3.1 | 0.000 | 1.000 | 2400/2280 |
| flat/2400/water.leviathan/water.shellback | seat1 | eliminated | 29.3 | 0.000 | 0.516 | 2280/2450 |
| flat/2400/water.shellback/water.leviathan | seat1 | eliminated | 20.5 | 0.000 | 0.456 | 2450/2280 |
| flat/2400/water.leviathan/water.spoutling | seat0 | eliminated | 4.2 | 0.990 | 0.000 | 2280/2375 |
| flat/2400/water.spoutling/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.984 | 2375/2280 |
| flat/2400/water.leviathan/skai_early | seat0 | eliminated | 4.1 | 0.946 | 0.000 | 2280/2550 |
| flat/2400/skai_early/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.946 | 2550/2280 |
| flat/2400/water.leviathan/water_early | seat0 | eliminated | 8.3 | 0.977 | 0.000 | 2280/2375 |
| flat/2400/water_early/water.leviathan | seat1 | eliminated | 8.3 | 0.000 | 0.977 | 2375/2280 |
| flat/2400/water.leviathan/water_rs | seat0 | eliminated | 7.7 | 0.888 | 0.000 | 2280/2375 |
| flat/2400/water_rs/water.leviathan | seat1 | eliminated | 7.9 | 0.000 | 0.872 | 2375/2280 |
| flat/2400/water.reef_runner/water.shellback | seat1 | eliminated | 18.9 | 0.000 | 0.939 | 2400/2450 |
| flat/2400/water.shellback/water.reef_runner | seat0 | eliminated | 19.9 | 0.915 | 0.000 | 2450/2400 |
| flat/2400/water.reef_runner/water.spoutling | seat0 | eliminated | 9.6 | 0.773 | 0.000 | 2400/2375 |
| flat/2400/water.spoutling/water.reef_runner | seat1 | eliminated | 9.5 | 0.000 | 0.779 | 2375/2400 |
| flat/2400/water.reef_runner/skai_early | seat1 | eliminated | 25.4 | 0.000 | 0.068 | 2400/2550 |
| flat/2400/skai_early/water.reef_runner | seat1 | eliminated | 22.3 | 0.000 | 0.169 | 2550/2400 |
| flat/2400/water.reef_runner/water_early | seat1 | eliminated | 15.1 | 0.000 | 0.761 | 2400/2375 |
| flat/2400/water_early/water.reef_runner | seat0 | eliminated | 15.8 | 0.733 | 0.000 | 2375/2400 |
| flat/2400/water.reef_runner/water_rs | seat1 | eliminated | 16.4 | 0.000 | 0.645 | 2400/2375 |
| flat/2400/water_rs/water.reef_runner | seat0 | eliminated | 13.6 | 0.786 | 0.000 | 2375/2400 |
| flat/2400/water.shellback/water.spoutling | seat0 | eliminated | 15.7 | 0.995 | 0.000 | 2450/2375 |
| flat/2400/water.spoutling/water.shellback | seat1 | eliminated | 16.4 | 0.000 | 0.997 | 2375/2450 |
| flat/2400/water.shellback/skai_early | seat0 | eliminated | 21.6 | 0.936 | 0.000 | 2450/2550 |
| flat/2400/skai_early/water.shellback | seat1 | eliminated | 21.0 | 0.000 | 0.935 | 2550/2450 |
| flat/2400/water.shellback/water_early | seat1 | eliminated | 34.0 | 0.000 | 0.734 | 2450/2375 |
| flat/2400/water_early/water.shellback | seat0 | eliminated | 37.0 | 0.684 | 0.000 | 2375/2450 |
| flat/2400/water.shellback/water_rs | seat0 | eliminated | 44.1 | 0.894 | 0.000 | 2450/2375 |
| flat/2400/water_rs/water.shellback | seat1 | eliminated | 39.3 | 0.000 | 0.969 | 2375/2450 |
| flat/2400/water.spoutling/skai_early | seat1 | eliminated | 9.4 | 0.000 | 0.794 | 2375/2550 |
| flat/2400/skai_early/water.spoutling | seat0 | eliminated | 9.6 | 0.784 | 0.000 | 2550/2375 |
| flat/2400/water.spoutling/water_early | seat1 | eliminated | 11.5 | 0.000 | 0.887 | 2375/2375 |
| flat/2400/water_early/water.spoutling | seat0 | eliminated | 12.3 | 0.832 | 0.000 | 2375/2375 |
| flat/2400/water.spoutling/water_rs | seat1 | eliminated | 11.4 | 0.000 | 0.950 | 2375/2375 |
| flat/2400/water_rs/water.spoutling | seat0 | eliminated | 11.0 | 0.968 | 0.000 | 2375/2375 |
| flat/2400/skai_early/water_early | seat1 | eliminated | 16.6 | 0.000 | 0.742 | 2550/2375 |
| flat/2400/water_early/skai_early | seat0 | eliminated | 17.2 | 0.729 | 0.000 | 2375/2550 |
| flat/2400/skai_early/water_rs | seat1 | eliminated | 17.6 | 0.000 | 0.663 | 2550/2375 |
| flat/2400/water_rs/skai_early | seat0 | eliminated | 15.5 | 0.798 | 0.000 | 2375/2550 |
| flat/2400/water_early/water_rs | seat0 | eliminated | 21.5 | 0.749 | 0.000 | 2375/2375 |
| flat/2400/water_rs/water_early | seat1 | eliminated | 20.7 | 0.000 | 0.746 | 2375/2375 |

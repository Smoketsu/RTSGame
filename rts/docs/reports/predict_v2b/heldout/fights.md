<!-- fights.md: written by the balance harness --fights (rts/tools/BalanceHarness/FightCoordinator.cs); not a source file. -->
# Equal-value fights

- Data digest: tuning a020a25c7d7c2eae, content 9cb28f0929df518c.
- Base seed 20261122; fight seed = SeedDerivation.GameSeed(base seed, "fights", fight ID, 0).
- Value: cost_common + cost_rare per unit; an army is the recipe repeated k = max(1, (2V + rv) / (2 rv)) times.
- Control: plain attack-move at the enemy front rank, idle units re-target every 2 s; no strategic AI, no HQ.
- Fights: 1904 (17 sides); determinism spot check: 2 fights re-run in-process, 0 mismatches.

**FIGHTS fights=1904 decided=1873 draws=0 stalemates=31 unresolved=0 mismatched=283 checks_passed=2 of=8 dome_absorbed=31**

## Design-rule checks

| Check | Rule | Threshold | Result | Detail |
| --- | --- | --- | --- | --- |
| rule1_no_immunity | P-005 1.6 rule 1 | >= 2 mobile and >= 1 static answers per enemy unit or HQ; every armed unit hits Ground | PASS | 13 targets checked |
| rule2_multiplier_band | P-005 1.6 rule 2 | multipliers in [0.5, 1.5]; ratio within a damage type <= 2 | FAIL | drench/structure 0.25 below 0.5<br>drench ratio 4.00 above 2 |
| rule3_counter_margin | P-005 1.6 rule 3 | counter keeps >= 0.2 value, exchange ratio <= 3 | FAIL | skai.roc_rider loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 9.2 above 3<br>skai.roc_rider loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 9.2 above 3<br>skai.stormcaller loses to water.shellback on flat: water.shellback in seat 0 exchange ratio inf above 3<br>skai.stormcaller loses to water.shellback on flat: water.shellback in seat 1 exchange ratio 13.4 above 3<br>skai.talon_archer loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 27.8 above 3<br>skai.talon_archer loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 20.7 above 3<br>skai.thunderhead loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>skai.thunderhead loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>skai.windrunner loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 43.0 above 3<br>skai.windrunner loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 43.0 above 3<br>water.grasper loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.grasper loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.ink_lobber loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.ink_lobber loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.leviathan loses to water.shellback on flat: water.shellback in seat 0 exchange ratio 6.0 above 3<br>water.reef_runner loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.reef_runner loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.shellback loses to skai.thunderhead on flat: skai.thunderhead in seat 0 exchange ratio inf above 3<br>water.shellback loses to skai.thunderhead on flat: skai.thunderhead in seat 1 exchange ratio inf above 3<br>water.spoutling loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 68.5 above 3<br>water.spoutling loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 219.3 above 3 |
| rule4_no_dominant_unit | P-005 1.6 rule 4 | no unit beats all its faction mates on flat | PASS | no dominant unit |
| rule4_every_unit_wins | P-005 1.6 rule 4 | every unit beats >= 1 unit of another faction on flat | FAIL | water.spoutling beats no unit of another faction on flat |
| rule5_two_answers | P-005 1.6 rule 5 | >= 1 unit and >= 1 terrain answers per threat and faction | FAIL | air (skai.roc_rider) against skai: 2 unit answers (skai.talon_archer, skai.windrunner), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only)<br>massed_armour (water.grasper) against skai: 2 unit answers (skai.roc_rider, skai.thunderhead), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only)<br>massed_armour (water.grasper) against water: 2 unit answers (water.ink_lobber, water.leviathan), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only) |
| rule6_hq_floor | P-005 1.6 rule 6 | Rooted HQ lives >= 15.0 s against 30 supply | FAIL | skai skai.stormcaller x10 vs skai.great_aerie: 11.0 s, needs >= 15.0 s<br>skai skai.stormcaller x10 vs water.grand_shell: 13.5 s, needs >= 15.0 s<br>skai skai.thunderhead x7 vs skai.great_aerie: 8.4 s, needs >= 15.0 s<br>skai skai.thunderhead x7 vs water.grand_shell: 10.4 s, needs >= 15.0 s<br>skai skai.windrunner x30 vs skai.great_aerie: 13.9 s, needs >= 15.0 s<br>water water.ink_lobber x15 vs skai.great_aerie: 6.6 s, needs >= 15.0 s<br>water water.ink_lobber x15 vs water.grand_shell: 7.6 s, needs >= 15.0 s<br>water water.leviathan x7 vs skai.great_aerie: 3.0 s, needs >= 15.0 s<br>water water.leviathan x7 vs water.grand_shell: 3.0 s, needs >= 15.0 s<br>water water.reef_runner x30 vs skai.great_aerie: 14.2 s, needs >= 15.0 s |
| matchups_agree | P-005 developer decision 1 (rule 7) | every unit has an entry; wins and loses match the fights | FAIL | water.spoutling: wins is null, but every armed unit must claim a fight it wins |

## Value exchange: flat, 1250

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.40 | +1.00 | -0.46 | +1.00 | +1.00 | -0.89 | +1.00 | S | +0.67 | +0.16 | -0.46 | – | +1.00 | – | -0.62 |
| skai.stormcaller | -1.00 | · | -0.32 | -1.00 | -0.12 | +0.40* | -0.27 | -0.89 | -0.05 | -0.92 | +0.83 | – | -0.53 | – | -0.18 | – | -0.91 |
| skai.talon_archer | +0.32 | +0.27 | · | -0.60 | -0.22 | -0.56 | -0.11 | -0.95 | -0.08 | -0.91 | +0.83 | – | -0.16 | – | -0.59 | – | -0.71 |
| skai.thunderhead | -1.00 | +1.00 | +0.63 | · | +0.75 | +1.00 | +1.00 | -0.34 | +1.00 | +1.00 | +0.88 | – | -0.31 | – | +1.00 | – | -0.40 |
| skai.windrunner | +0.40 | +0.06 | +0.34 | -0.74 | · | -0.60 | -0.33 | -0.97 | +0.07 | -0.87 | +0.80 | +0.54 | -0.10* | – | -0.63 | –* | -0.95 |
| water.grasper | -1.00 | +0.05* | +0.54 | -1.00 | +0.61 | · | -0.27 | -0.99 | +0.61 | +0.65 | +0.81 | – | -0.31 | – | +0.13* | – | -0.49 |
| water.ink_lobber | -1.00 | +0.27 | +0.39 | -1.00 | +0.27 | +0.49 | · | -0.78 | +0.36 | -0.93 | +0.87 | – | -0.31 | – | +0.29 | – | -0.87 |
| water.leviathan | +0.89 | +0.89 | +0.96 | +0.34 | +0.97 | +1.00 | +0.78 | · | +0.98 | -0.48 | +0.98 | +0.97 | +0.93 | – | – | – | -0.39 |
| water.reef_runner | -1.00 | +0.07 | +0.15 | -1.00 | -0.04 | -0.62 | -0.30 | -0.98 | · | -0.99 | +0.77 | +0.50 | -0.31 | – | -0.63 | –* | -0.91 |
| water.shellback | S | +1.00 | +0.94 | -1.00 | +0.98 | -0.63 | +1.00 | +0.84 | +0.98 | · | +0.99 | +0.99 | S | – | -0.58 | – | +0.99 |
| water.spoutling | -0.67 | -0.82 | -0.82 | -0.88 | -0.79 | -0.83 | -0.88 | -1.00 | -0.78 | -0.99 | · | -0.71 | -0.78 | – | -0.83 | – | -0.98 |
| skai_pressure_mix | -0.32 | – | – | – | -0.46 | – | – | -0.98 | -0.47 | -0.98 | +0.71 | · | – | +0.15* | – | – | -0.90 |
| skai_expand_mix | +0.49 | +0.36 | +0.04 | +0.31 | -0.14* | +0.31 | +0.31 | -0.91 | +0.31 | S | +0.79 | – | · | – | +0.31 | –* | -0.78 |
| skai_air_mix | – | – | – | – | – | – | – | – | – | – | – | +0.07* | – | · | – | +0.73* | – |
| water_pressure_mix | -1.00 | +0.24 | +0.60 | -1.00 | +0.65 | +0.06* | -0.01 | – | +0.65 | +0.61 | +0.84 | – | -0.31 | – | · | – | -0.53 |
| water_expand_mix | – | – | – | – | –* | – | – | – | –* | – | – | – | –* | +0.62* | – | · | – |
| water_leviathan_mix | +0.76 | +0.95 | +0.84 | +0.14 | +0.91 | +0.73 | +0.73 | +0.44 | +0.94 | -0.94 | +1.00 | +1.00 | +0.86 | – | +0.77 | – | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: flat, 2750

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.37 | +1.00 | -0.45 | +1.00 | +1.00 | -0.94 | +1.00 | S | +0.71 | -0.44 | -0.12* | +0.37 | +1.00 | -0.72 | -0.73 |
| skai.stormcaller | -1.00 | · | -0.06 | -1.00 | -0.16 | +0.34 | -0.14 | -0.95 | -0.10* | -0.93 | +0.79 | -0.28 | -0.32 | -0.92 | +0.42 | -0.75 | -0.90 |
| skai.talon_archer | +0.36 | +0.10 | · | -0.70 | -0.23 | -0.60 | -0.35 | -0.97 | +0.00* | -0.92 | +0.82 | +0.21 | +0.36 | -0.51 | -0.56 | -0.79 | -0.86 |
| skai.thunderhead | -1.00 | +1.00 | +0.69 | · | +0.80 | +1.00 | +1.00 | -0.75 | +1.00 | +1.00* | +0.88 | +0.81 | -0.31 | -0.92 | +1.00 | +0.51 | -0.34 |
| skai.windrunner | +0.47 | +0.15 | +0.20 | -0.80 | · | -0.62 | -0.42 | -1.00 | +0.11 | -0.93 | +0.80 | +0.21 | +0.37 | -0.59 | -0.57 | -0.82 | -0.98 |
| water.grasper | -1.00 | -0.15 | +0.57 | -1.00 | +0.61 | · | -0.57 | -1.00 | +0.61 | +0.57 | +0.81 | +0.63 | -0.31 | -0.92 | +0.24 | +0.23 | -0.75 |
| water.ink_lobber | -1.00 | +0.31 | +0.43 | -1.00 | +0.51 | +0.49 | · | -0.89 | +0.61 | -0.91 | +0.88 | +0.59 | -0.31 | -0.92 | +0.48 | -0.55 | -0.88 |
| water.leviathan | +0.94 | +0.95 | +0.97 | +0.75 | +1.00 | +1.00 | +0.94 | · | +1.00 | -0.16* | +1.00 | +0.98 | +0.93 | +0.90 | +1.00 | +0.84 | +0.38 |
| water.reef_runner | -1.00 | -0.25* | +0.08* | -1.00 | -0.03 | -0.62 | -0.53 | -1.00 | · | -0.93 | +0.78 | +0.26 | -0.31 | -0.92 | -0.59 | -0.81 | -0.96 |
| water.shellback | S | +0.93 | +0.93 | S* | +0.93 | -0.59 | +0.94 | -0.43* | +0.94 | · | +0.99 | +0.93 | S | -0.92* | -0.46 | +0.46 | +0.91 |
| water.spoutling | -0.72 | -0.83 | -0.82 | -0.89 | -0.80 | -0.82 | -0.89 | -1.00 | -0.78 | -0.99 | · | -0.78 | -0.73 | -0.83 | -0.81 | -0.96 | -1.00 |
| skai_pressure_mix | +0.45 | +0.21 | -0.17 | -0.80 | -0.17 | -0.63 | -0.43 | -0.98 | -0.20 | -0.93 | +0.80 | · | +0.32 | -0.58 | -0.59 | -0.87 | -0.96 |
| skai_expand_mix | -0.09* | +0.34 | -0.39 | +0.31 | -0.38 | +0.31 | +0.31 | -0.89 | +0.31 | S | +0.73 | -0.28 | · | -0.55 | +0.31 | -0.86 | -0.85 |
| skai_air_mix | -0.28 | +0.92 | +0.46 | +0.92 | +0.58 | +0.92 | +0.92 | -0.97 | +0.92 | S* | +0.84 | +0.59 | +0.52 | · | +0.92 | -0.34 | -0.58 |
| water_pressure_mix | -1.00 | -0.28 | +0.58 | -1.00 | +0.57 | -0.26 | -0.60 | -1.00 | +0.58 | +0.44 | +0.80 | +0.60 | -0.31 | -0.92 | · | +0.05 | -0.83 |
| water_expand_mix | +0.71 | +0.72 | +0.72 | -0.60 | +0.76 | -0.29 | +0.56 | -0.84 | +0.79 | -0.75 | +0.92 | +0.77 | +0.84 | +0.36 | -0.19 | · | -0.77 |
| water_leviathan_mix | +0.84 | +1.00 | +0.88 | +0.32 | +0.93 | +0.76 | +0.87 | -0.25 | +0.94 | -0.88 | +1.00 | +0.92 | +0.92 | +0.49 | +0.82 | +0.69 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: high_ground, 1750

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.11 | +1.00 | +0.86* | +1.00 | +1.00 | -0.94 | +1.00 | +1.00 | +0.74 | +0.72* | +0.21* | +0.59* | +1.00 | +0.88* | –* |
| skai.stormcaller | -1.00 | · | -0.68* | -1.00 | +0.42* | +1.00* | -0.70* | -1.00 | +0.42 | +1.00* | +0.35 | -0.41* | -0.76 | -0.92 | –* | -0.78 | – |
| skai.talon_archer | +0.34 | -0.03* | · | -0.74 | +0.67 | +1.00* | -0.53* | -1.00 | +0.67 | +1.00* | +0.71 | +0.45 | -0.40* | -0.35 | +0.94* | -0.55 | –* |
| skai.thunderhead | -1.00 | +1.00 | +0.58 | · | +0.88 | +1.00 | +1.00 | -0.64* | +1.00 | +1.00 | +0.87 | +0.83 | -0.31 | -0.92 | +1.00 | +0.98 | – |
| skai.windrunner | +0.37* | +0.12* | -0.37 | -0.83 | · | +1.00* | -0.51 | -0.99 | +0.18* | +0.82* | +0.73 | +0.12* | -0.41 | -0.36 | +0.70* | -0.74 | – |
| water.grasper | -1.00 | +0.28* | +0.51* | -1.00 | +0.59* | · | -0.27 | -0.99 | +0.59* | +0.65* | +0.80* | +0.63* | -0.31 | -0.92 | -0.11 | +0.29* | – |
| water.ink_lobber | -1.00 | -0.68* | -0.69* | -1.00 | +0.44 | +1.00 | · | -1.00 | +0.44 | +1.00* | +0.26 | -0.38* | -0.70 | -0.92 | –* | -0.32 | – |
| water.leviathan | +0.77 | +0.49 | +0.68 | -0.11* | +0.82 | +1.00 | +0.31 | · | +0.82 | +0.85 | +0.84 | +0.81 | +0.69 | +0.49 | +1.00 | +0.67 | –* |
| water.reef_runner | -1.00 | -0.26 | -0.43 | -1.00 | +0.11* | +1.00* | -0.58 | -1.00 | · | +0.83* | +0.69 | -0.20 | -0.53 | -0.92 | +0.72* | -0.73 | – |
| water.shellback | -1.00 | +1.00* | +1.00* | -1.00 | +0.99* | +0.57* | +1.00* | -0.50 | +0.97* | · | +1.00* | +0.99* | -0.31 | -0.92 | +0.58* | +0.76* | –* |
| water.spoutling | -0.82 | -0.88 | -0.90 | -0.95 | -0.75 | +1.00* | -0.92 | -1.00 | -0.75 | S* | · | -0.79 | -0.87 | -0.85 | +0.92* | -0.98 | – |
| skai_pressure_mix | +0.19* | -0.28* | -0.48 | -0.84 | +0.18* | +1.00* | -0.41* | -0.97 | +0.18 | +0.91* | +0.67 | · | – | -0.50 | –* | -0.86 | – |
| skai_expand_mix | +0.55* | +0.46 | -0.14* | +0.33 | +0.77 | +1.00 | +0.39 | -0.98 | +0.77 | +1.00 | +0.75 | – | · | +0.53* | +0.97 | +0.19* | –* |
| skai_air_mix | +0.18* | +0.92 | +0.34 | +0.92 | +0.86 | +1.00 | +0.92 | -0.84 | +0.92 | +1.00 | +0.80 | +0.76 | +0.31* | · | +0.98 | +0.80* | –* |
| water_pressure_mix | -1.00 | –* | +0.65* | -1.00 | +0.63* | +0.47 | –* | -1.00 | +0.63* | +0.65* | +0.83* | –* | -0.31 | -0.92 | · | +0.35* | – |
| water_expand_mix | +0.62* | +0.81 | +0.62 | -0.64 | +0.62 | +0.77* | +0.54 | -0.89 | +0.62 | +0.85* | +0.88 | +0.67 | +0.58* | +0.53* | +0.71* | · | – |
| water_leviathan_mix | –* | – | –* | – | – | – | – | –* | – | –* | – | – | –* | –* | – | – | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal. The row is the attacker; the column holds the plateau.

## Value exchange: flooded, 2250

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -1.00* | +1.00 | -1.00* | +1.00 | +1.00 | -1.00 | -1.00* | -1.00 | -0.88* | -1.00* | -1.00* | -0.70* | +0.61 | – | -1.00 |
| skai.stormcaller | -1.00 | · | -0.82* | -1.00 | -0.89* | -0.92 | -0.78* | -0.99 | -0.94* | -1.00 | -0.35* | -0.90* | -0.87 | – | -0.93* | -1.00 | -1.00 |
| skai.talon_archer | -0.84* | -0.96* | · | -1.00* | -1.00* | -0.97 | -1.00* | -1.00 | -1.00* | -0.99 | +0.23 | -0.98* | –* | –* | -0.98 | -1.00 | -1.00 |
| skai.thunderhead | -1.00 | +1.00 | -0.01* | · | -0.07* | +1.00 | +1.00 | -1.00* | -0.38* | -0.94 | +0.65 | -0.09* | -0.31 | -0.92 | +0.88 | –* | -0.76 |
| skai.windrunner | -0.58* | -0.70* | -0.79* | -0.91* | · | -0.88 | -0.92* | -1.00 | -0.77* | -1.00 | +0.58 | -0.74* | -0.67* | –* | -0.84 | -0.97 | -1.00 |
| water.grasper | -1.00 | +0.17 | +0.65 | -1.00 | +0.65 | · | -0.46* | -0.98 | +0.61 | +0.57 | +0.84 | +0.69 | – | – | +0.39 | -0.17* | -0.78* |
| water.ink_lobber | -1.00 | -0.35* | -0.34* | -1.00 | -0.50* | -0.51* | · | -0.88 | -0.71* | -0.99 | +0.63 | -0.39* | -0.55 | – | -0.48* | -0.90* | -0.92 |
| water.leviathan | +0.71 | +0.68 | +0.64 | -0.09* | +0.58 | +0.55 | +0.59 | · | +0.55 | -0.58* | +0.79 | +0.57 | +0.72 | – | +0.65 | -0.58* | -0.77* |
| water.reef_runner | -0.65* | -0.70* | -0.75* | -0.91* | -0.75* | -0.88 | -0.90* | -1.00 | · | -0.99 | +0.62 | -0.71* | -0.64* | –* | -0.86 | -1.00 | -1.00 |
| water.shellback | +0.94 | +1.00 | +0.92 | +0.92 | +0.91 | -0.73 | +0.88 | -0.62* | +0.87 | · | +0.99 | +0.94 | +0.96 | – | -0.58 | -0.96 | +0.72 |
| water.spoutling | -1.00* | -1.00* | -1.00 | -1.00 | -1.00 | -1.00 | -1.00 | -1.00 | -1.00 | -1.00 | · | -1.00 | -1.00* | –* | -1.00 | -1.00 | -1.00 |
| skai_pressure_mix | -0.69* | -0.86* | -0.87* | -0.93* | -0.83* | -0.92 | -0.97* | -1.00 | -0.86* | -1.00 | +0.26 | · | -0.79* | -0.84* | -0.89 | – | -1.00 |
| skai_expand_mix | -0.99* | +0.31 | –* | +0.31 | -0.98* | – | +0.31 | -1.00 | -0.99* | -1.00 | -0.95* | -0.98* | · | -0.99* | -0.96 | – | – |
| skai_air_mix | -1.00* | – | –* | +0.92 | –* | – | – | – | –* | – | –* | -0.98* | -0.95* | · | +0.62 | – | – |
| water_pressure_mix | -0.95 | -0.41* | +0.48 | -0.98 | +0.50 | -0.55 | -0.63* | -1.00 | +0.42 | +0.27 | +0.79 | +0.55 | +0.21 | -0.88 | · | –* | – |
| water_expand_mix | – | +0.68 | +0.77 | –* | +0.75 | -0.63* | -0.56* | -0.93* | +0.75 | +0.64 | +0.93 | – | – | – | –* | · | -0.89* |
| water_leviathan_mix | +0.75 | +0.72 | +0.70 | +0.40 | +0.69 | -0.25* | +0.65 | -0.40* | +0.72 | -0.77 | +0.94 | +0.69 | – | – | – | -0.63* | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal. The row fights in the column's flood.

## Value exchange: choke, 1250

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | +0.29* | +1.00 | -0.43 | +1.00 | +1.00 | -0.77 | +1.00 | S | +0.68 | +0.17 | -0.35 | – | +1.00 | – | +0.42* |
| skai.stormcaller | -1.00 | · | -0.30 | -1.00 | -0.06 | +0.26* | -0.24 | -0.89 | -0.02 | -0.92 | +0.83 | – | -0.48 | – | -0.11 | – | -0.91 |
| skai.talon_archer | +0.30* | +0.25 | · | -0.62 | -0.13 | -0.56 | -0.16 | -0.95 | -0.17 | -0.92 | +0.83 | – | -0.16* | – | -0.58 | – | -0.71 |
| skai.thunderhead | -1.00 | +1.00 | +0.74 | · | +0.74 | +1.00 | +1.00 | -0.34 | +1.00 | +1.00 | +0.90 | – | -0.31 | – | +1.00 | – | -0.23 |
| skai.windrunner | +0.42 | +0.17 | +0.31 | -0.78 | · | -0.61 | -0.41 | -0.97 | +0.12 | -0.86 | +0.80 | +0.48 | -0.15* | – | -0.62 | – | -0.99 |
| water.grasper | -1.00 | +0.13* | +0.55 | -1.00 | +0.61 | · | +0.04* | -0.99 | +0.61 | +0.64 | +0.82 | – | -0.31 | – | -0.08 | – | -0.52 |
| water.ink_lobber | -1.00 | +0.24 | +0.38 | -1.00 | +0.33 | +0.49* | · | -0.78 | +0.26 | -0.90 | +0.87 | – | -0.31 | – | +0.35 | – | -0.87 |
| water.leviathan | +0.65 | +0.89 | +0.96 | +0.43 | +0.97 | +1.00 | +0.78 | · | +0.98 | -0.63 | +0.98 | +0.97 | +0.93 | – | – | – | -0.39 |
| water.reef_runner | -1.00 | +0.08 | +0.09 | -1.00 | -0.09 | -0.62 | -0.24 | -0.98 | · | -0.98 | +0.78 | +0.51 | -0.31 | – | -0.63 | –* | -0.96 |
| water.shellback | S | +1.00 | +0.91 | -1.00 | +0.99 | -0.62 | +0.96 | +0.95 | +0.99 | · | +1.00 | +0.99 | S | – | -0.60 | – | +0.92 |
| water.spoutling | -0.68 | -0.83 | -0.82 | -0.88 | -0.79 | -0.82 | -0.86 | -1.00 | -0.78 | -0.99 | · | -0.71 | -0.78 | – | -0.83 | – | -0.98 |
| skai_pressure_mix | -0.24 | – | – | – | -0.46 | – | – | -0.98 | -0.46 | -0.99 | +0.70 | · | – | -0.04 | – | – | -0.90 |
| skai_expand_mix | +0.49 | +0.36 | -0.03* | +0.31 | -0.17* | +0.31 | +0.31 | -0.91 | +0.31 | S | +0.79 | – | · | – | +0.31 | –* | -0.74 |
| skai_air_mix | – | – | – | – | – | – | – | – | – | – | – | +0.14 | – | · | – | +0.73* | – |
| water_pressure_mix | -1.00 | +0.28 | +0.60 | -1.00 | +0.64 | +0.18 | -0.26 | – | +0.64 | +0.61 | +0.83 | – | -0.31 | – | · | – | -0.60 |
| water_expand_mix | – | – | – | – | – | – | – | – | –* | – | – | – | –* | +0.62* | – | · | – |
| water_leviathan_mix | +0.69* | +0.95 | +0.84 | +0.18 | +0.91 | +0.75 | +0.73 | +0.44 | +0.94 | -0.94 | +1.00 | +1.00 | +0.86 | – | +0.77 | – | · |

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
| skai | skai_air_mix | 10 | skai.great_aerie | uprooted | 15.0 s | reported |
| skai | skai_air_mix | 10 | skai.great_aerie | rooted | 17.0 s | PASS |
| skai | skai_air_mix | 10 | water.grand_shell | uprooted | 17.5 s | reported |
| skai | skai_air_mix | 10 | water.grand_shell | rooted | 20.6 s | PASS |
| skai | skai_expand_mix | 12 | skai.great_aerie | uprooted | 19.6 s | reported |
| skai | skai_expand_mix | 12 | skai.great_aerie | rooted | 30.2 s | PASS |
| skai | skai_expand_mix | 12 | water.grand_shell | uprooted | 23.6 s | reported |
| skai | skai_expand_mix | 12 | water.grand_shell | rooted | 39.8 s | PASS |
| skai | skai_pressure_mix | 21 | skai.great_aerie | uprooted | 12.7 s | reported |
| skai | skai_pressure_mix | 21 | skai.great_aerie | rooted | 19.8 s | PASS |
| skai | skai_pressure_mix | 21 | water.grand_shell | uprooted | 15.1 s | reported |
| skai | skai_pressure_mix | 21 | water.grand_shell | rooted | 24.6 s | PASS |
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
| water | water_expand_mix | 12 | skai.great_aerie | uprooted | 17.0 s | reported |
| water | water_expand_mix | 12 | skai.great_aerie | rooted | 21.0 s | PASS |
| water | water_expand_mix | 12 | water.grand_shell | uprooted | 20.0 s | reported |
| water | water_expand_mix | 12 | water.grand_shell | rooted | 25.2 s | PASS |
| water | water_leviathan_mix | 6 | skai.great_aerie | uprooted | 21.1 s | reported |
| water | water_leviathan_mix | 6 | skai.great_aerie | rooted | 16.1 s | PASS |
| water | water_leviathan_mix | 6 | water.grand_shell | uprooted | 25.3 s | reported |
| water | water_leviathan_mix | 6 | water.grand_shell | rooted | 19.3 s | PASS |
| water | water_pressure_mix | 14 | skai.great_aerie | uprooted | 13.6 s | reported |
| water | water_pressure_mix | 14 | skai.great_aerie | rooted | 20.1 s | PASS |
| water | water_pressure_mix | 14 | water.grand_shell | uprooted | 16.1 s | reported |
| water | water_pressure_mix | 14 | water.grand_shell | rooted | 24.8 s | PASS |

## Matchups file

| Unit | Claim | Measured | Agrees |
| --- | --- | --- | --- |
| skai.roc_rider | beats water.grasper on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| skai.roc_rider | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.stormcaller | beats water.spoutling on flat | measured seat 0 win (left 0.83), seat 1 win (left 0.82) | yes |
| skai.stormcaller | loses to water.shellback on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.talon_archer | beats water.spoutling on flat | measured seat 0 win (left 0.83), seat 1 win (left 0.82) | yes |
| skai.talon_archer | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.thunderhead | beats water.grasper on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| skai.thunderhead | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.windrunner | beats water.spoutling on flat | measured seat 0 win (left 0.80), seat 1 win (left 0.79) | yes |
| skai.windrunner | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.grasper | beats water.spoutling on flat | measured seat 0 win (left 0.81), seat 1 win (left 0.83) | yes |
| water.grasper | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.ink_lobber | beats water.spoutling on flat | measured seat 0 win (left 0.87), seat 1 win (left 0.88) | yes |
| water.ink_lobber | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.leviathan | beats skai.windrunner on flat | measured seat 0 win (left 0.97), seat 1 win (left 0.97) | yes |
| water.leviathan | loses to water.shellback on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.reef_runner | beats water.spoutling on flat | measured seat 0 win (left 0.77), seat 1 win (left 0.78) | yes |
| water.reef_runner | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.shellback | beats skai.stormcaller on flat | measured seat 0 win (left 1.00), seat 1 win (left 0.92) | yes |
| water.shellback | loses to skai.thunderhead on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.spoutling | wins: null | - | no |
| water.spoutling | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |

## Suggested matchups entries

| Unit | Wins against | Loses to |
| --- | --- | --- |
| skai.roc_rider | water.grasper (left 1.00) | water.leviathan (their left 0.89) |
| skai.stormcaller | water.spoutling (left 0.83) | skai.roc_rider (their left 1.00) |
| skai.talon_archer | water.spoutling (left 0.82) | water.leviathan (their left 0.95) |
| skai.thunderhead | water.grasper (left 1.00) | skai.roc_rider (their left 1.00) |
| skai.windrunner | water.spoutling (left 0.80) | water.leviathan (their left 0.97) |
| water.grasper | water.spoutling (left 0.82) | skai.roc_rider (their left 1.00) |
| water.ink_lobber | water.spoutling (left 0.88) | skai.roc_rider (their left 1.00) |
| water.leviathan | water.grasper (left 1.00) | water.shellback (their left 0.66) |
| water.reef_runner | water.spoutling (left 0.78) | skai.roc_rider (their left 1.00) |
| water.shellback | water.spoutling (left 0.99) | skai.thunderhead (their left 1.00) |
| water.spoutling | null | water.shellback (their left 0.99) |

## Stalemates, unresolved fights and seat splits

A seat split means the two seat orders of a pair have different winners. On high_ground and flooded the seats are not symmetric by design, so splits there show terrain at work, not seat bias. A `no_damage` stalemate counts only damage events; when shots absorbed by a shield dome fell inside the idle window the stalemate is `dome_absorbed` instead.

| Fight | Finding | Reason |
| --- | --- | --- |
| flat/1250/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/1250/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/1250/skai.stormcaller/water.grasper | seat split (skai.stormcaller wins this order, water.grasper wins the swapped order) | eliminated |
| flat/1250/skai.windrunner/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| flat/1250/water.grasper/water_pressure_mix | seat split (water.grasper wins this order, water_pressure_mix wins the swapped order) | eliminated |
| flat/1250/water.shellback/skai_expand_mix | stalemate | dome_absorbed |
| flat/1250/skai_expand_mix/water.shellback | stalemate | dome_absorbed |
| flat/1250/water.shellback/skai_air_mix | stalemate | dome_absorbed |
| flat/1250/skai_air_mix/water.shellback | stalemate | dome_absorbed |
| flat/1250/skai_air_mix/skai_pressure_mix | seat split (skai_air_mix wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| flat/1250/skai_air_mix/water_expand_mix | seat split (skai_air_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| flat/2750/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/2750/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/2750/skai.roc_rider/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flat/2750/skai.stormcaller/water.reef_runner | seat split (water.reef_runner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flat/2750/skai.talon_archer/water.reef_runner | seat split (skai.talon_archer wins this order, water.reef_runner wins the swapped order) | eliminated |
| flat/2750/water.shellback/skai.thunderhead | stalemate | dome_absorbed |
| flat/2750/water.leviathan/water.shellback | seat split (water.shellback wins this order, water.leviathan wins the swapped order) | eliminated |
| flat/2750/water.shellback/skai_expand_mix | stalemate | dome_absorbed |
| flat/2750/skai_expand_mix/water.shellback | stalemate | dome_absorbed |
| flat/2750/skai_air_mix/water.shellback | stalemate | dome_absorbed |
| high_ground/1750/skai.roc_rider/skai.windrunner | seat split (skai.roc_rider wins this order, skai.windrunner wins the swapped order) | eliminated |
| high_ground/1750/skai.roc_rider/skai_pressure_mix | seat split (skai.roc_rider wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| high_ground/1750/skai.roc_rider/skai_expand_mix | seat split (skai.roc_rider wins this order, skai_expand_mix wins the swapped order) | eliminated |
| high_ground/1750/skai.roc_rider/skai_air_mix | seat split (skai.roc_rider wins this order, skai_air_mix wins the swapped order) | eliminated |
| high_ground/1750/skai.roc_rider/water_expand_mix | seat split (skai.roc_rider wins this order, water_expand_mix wins the swapped order) | eliminated |
| high_ground/1750/skai.stormcaller/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.stormcaller wins the swapped order) | eliminated |
| high_ground/1750/skai.stormcaller/skai.windrunner | seat split (skai.stormcaller wins this order, skai.windrunner wins the swapped order) | eliminated |
| high_ground/1750/skai.stormcaller/water.grasper | seat split (skai.stormcaller wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/1750/skai.stormcaller/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.stormcaller wins the swapped order) | eliminated |
| high_ground/1750/skai.stormcaller/water.shellback | seat split (skai.stormcaller wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1750/skai.stormcaller/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.stormcaller wins the swapped order) | eliminated |
| high_ground/1750/skai.talon_archer/water.grasper | seat split (skai.talon_archer wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/1750/skai.talon_archer/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.talon_archer wins the swapped order) | eliminated |
| high_ground/1750/skai.talon_archer/water.shellback | seat split (skai.talon_archer wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1750/skai.talon_archer/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| high_ground/1750/skai.talon_archer/water_pressure_mix | seat split (skai.talon_archer wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/1750/skai.thunderhead/water.leviathan | seat split (water.leviathan wins this order, skai.thunderhead wins the swapped order) | eliminated |
| high_ground/1750/skai.windrunner/water.grasper | seat split (skai.windrunner wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/1750/skai.windrunner/water.reef_runner | seat split (skai.windrunner wins this order, water.reef_runner wins the swapped order) | eliminated |
| high_ground/1750/skai.windrunner/water.shellback | seat split (skai.windrunner wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1750/skai.windrunner/skai_pressure_mix | seat split (skai.windrunner wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| high_ground/1750/skai.windrunner/water_pressure_mix | seat split (skai.windrunner wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/1750/water.grasper/water.reef_runner | seat split (water.grasper wins this order, water.reef_runner wins the swapped order) | eliminated |
| high_ground/1750/water.grasper/water.shellback | seat split (water.grasper wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1750/water.grasper/water.spoutling | seat split (water.grasper wins this order, water.spoutling wins the swapped order) | eliminated |
| high_ground/1750/skai_pressure_mix/water.grasper | seat split (skai_pressure_mix wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/1750/water.grasper/water_expand_mix | seat split (water.grasper wins this order, water_expand_mix wins the swapped order) | eliminated |
| high_ground/1750/water.ink_lobber/water.shellback | seat split (water.ink_lobber wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1750/skai_pressure_mix/water.ink_lobber | seat split (water.ink_lobber wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| high_ground/1750/water.reef_runner/water.shellback | seat split (water.reef_runner wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1750/water.reef_runner/water_pressure_mix | seat split (water.reef_runner wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/1750/water.spoutling/water.shellback | stalemate | dome_absorbed |
| high_ground/1750/skai_pressure_mix/water.shellback | seat split (skai_pressure_mix wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1750/water.shellback/water_pressure_mix | seat split (water.shellback wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/1750/water.shellback/water_expand_mix | seat split (water.shellback wins this order, water_expand_mix wins the swapped order) | eliminated |
| high_ground/1750/water_leviathan_mix/water.shellback | stalemate | dome_absorbed |
| high_ground/1750/water.spoutling/water_pressure_mix | seat split (water.spoutling wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/1750/skai_air_mix/skai_expand_mix | seat split (skai_air_mix wins this order, skai_expand_mix wins the swapped order) | eliminated |
| high_ground/1750/skai_expand_mix/water_expand_mix | seat split (skai_expand_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| high_ground/1750/skai_air_mix/water_expand_mix | seat split (skai_air_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| high_ground/1750/water_expand_mix/water_pressure_mix | seat split (water_expand_mix wins this order, water_pressure_mix wins the swapped order) | eliminated |
| flooded/2250/skai.roc_rider/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2250/skai.roc_rider/skai.windrunner | seat split (skai.windrunner wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2250/skai.roc_rider/water.reef_runner | seat split (water.reef_runner wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2250/skai.roc_rider/water.spoutling | seat split (water.spoutling wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2250/skai.roc_rider/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2250/skai.roc_rider/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2250/skai.roc_rider/skai_air_mix | seat split (skai_air_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2250/skai.stormcaller/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2250/skai.stormcaller/skai.windrunner | seat split (skai.windrunner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2250/skai.stormcaller/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2250/skai.stormcaller/water.reef_runner | seat split (water.reef_runner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2250/skai.stormcaller/water.spoutling | seat split (water.spoutling wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2250/skai.stormcaller/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2250/skai.stormcaller/water_pressure_mix | seat split (water_pressure_mix wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2250/skai.talon_archer/skai.thunderhead | seat split (skai.thunderhead wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2250/skai.talon_archer/skai.windrunner | seat split (skai.windrunner wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2250/skai.talon_archer/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2250/skai.talon_archer/water.reef_runner | seat split (water.reef_runner wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2250/skai.talon_archer/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2250/skai.thunderhead/skai.windrunner | seat split (skai.windrunner wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/2250/skai.thunderhead/water.leviathan | seat split (water.leviathan wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/2250/skai.thunderhead/water.reef_runner | seat split (water.reef_runner wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/2250/skai.thunderhead/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/2250/skai.windrunner/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2250/skai.windrunner/water.reef_runner | seat split (water.reef_runner wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2250/skai.windrunner/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2250/skai.windrunner/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2250/water.grasper/water.ink_lobber | seat split (water.ink_lobber wins this order, water.grasper wins the swapped order) | eliminated |
| flooded/2250/water.grasper/water_expand_mix | seat split (water_expand_mix wins this order, water.grasper wins the swapped order) | eliminated |
| flooded/2250/water.grasper/water_leviathan_mix | seat split (water_leviathan_mix wins this order, water.grasper wins the swapped order) | eliminated |
| flooded/2250/water.ink_lobber/water.reef_runner | seat split (water.reef_runner wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/2250/skai_pressure_mix/water.ink_lobber | seat split (water.ink_lobber wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| flooded/2250/water.ink_lobber/water_pressure_mix | seat split (water_pressure_mix wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/2250/water.ink_lobber/water_expand_mix | seat split (water_expand_mix wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/2250/water.leviathan/water.shellback | seat split (water.shellback wins this order, water.leviathan wins the swapped order) | eliminated |
| flooded/2250/water.leviathan/water_expand_mix | seat split (water_expand_mix wins this order, water.leviathan wins the swapped order) | eliminated |
| flooded/2250/water.leviathan/water_leviathan_mix | seat split (water_leviathan_mix wins this order, water.leviathan wins the swapped order) | eliminated |
| flooded/2250/skai_pressure_mix/water.reef_runner | seat split (water.reef_runner wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| flooded/2250/skai_expand_mix/water.reef_runner | seat split (water.reef_runner wins this order, skai_expand_mix wins the swapped order) | eliminated |
| flooded/2250/skai_expand_mix/water.spoutling | seat split (water.spoutling wins this order, skai_expand_mix wins the swapped order) | eliminated |
| flooded/2250/skai_expand_mix/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai_expand_mix wins the swapped order) | eliminated |
| flooded/2250/skai_air_mix/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai_air_mix wins the swapped order) | eliminated |
| flooded/2250/skai_air_mix/skai_expand_mix | seat split (skai_expand_mix wins this order, skai_air_mix wins the swapped order) | eliminated |
| flooded/2250/water_expand_mix/water_leviathan_mix | seat split (water_leviathan_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| choke/1250/skai.roc_rider/skai.talon_archer | seat split (skai.roc_rider wins this order, skai.talon_archer wins the swapped order) | eliminated |
| choke/1250/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| choke/1250/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| choke/1250/skai.roc_rider/water_leviathan_mix | seat split (skai.roc_rider wins this order, water_leviathan_mix wins the swapped order) | eliminated |
| choke/1250/skai.stormcaller/water.grasper | seat split (skai.stormcaller wins this order, water.grasper wins the swapped order) | eliminated |
| choke/1250/skai.talon_archer/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| choke/1250/skai.windrunner/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| choke/1250/water.grasper/water.ink_lobber | seat split (water.grasper wins this order, water.ink_lobber wins the swapped order) | eliminated |
| choke/1250/water.shellback/skai_expand_mix | stalemate | dome_absorbed |
| choke/1250/skai_expand_mix/water.shellback | stalemate | dome_absorbed |
| choke/1250/water.shellback/skai_air_mix | stalemate | dome_absorbed |
| choke/1250/skai_air_mix/water.shellback | stalemate | dome_absorbed |
| choke/1250/skai_air_mix/water_expand_mix | seat split (skai_air_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| flat_uneven/1750/skai.roc_rider/water.shellback/r12500 | stalemate | dome_absorbed |
| flat_uneven/1750/skai.thunderhead/water.shellback/r12500 | stalemate | dome_absorbed |
| flat_uneven/1750/water.shellback/skai_expand_mix/r12500 | stalemate | dome_absorbed |
| flat_uneven/1750/skai_expand_mix/water.shellback/r12500 | stalemate | dome_absorbed |
| flat_uneven/1750/water.shellback/skai_air_mix/r12500 | stalemate | dome_absorbed |
| flat_uneven/1750/skai_air_mix/water.shellback/r12500 | stalemate | dome_absorbed |
| flat_uneven/1750/skai.roc_rider/water.shellback/r15000 | stalemate | dome_absorbed |
| flat_uneven/1750/skai.thunderhead/water.shellback/r15000 | stalemate | dome_absorbed |
| flat_uneven/1750/water.shellback/skai_expand_mix/r15000 | stalemate | dome_absorbed |
| flat_uneven/1750/skai_expand_mix/water.shellback/r15000 | stalemate | dome_absorbed |
| flat_uneven/1750/skai_air_mix/water.shellback/r15000 | stalemate | dome_absorbed |

## All fights

| Fight | Result | Reason | Seconds | Left 0 | Left 1 | Values |
| --- | --- | --- | ---: | ---: | ---: | --- |
| flat/1250/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 12.7 | 1.000 | 0.000 | 1200/1330 |
| flat/1250/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 12.7 | 0.000 | 1.000 | 1330/1200 |
| flat/1250/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 18.7 | 0.000 | 0.395 | 1200/1300 |
| flat/1250/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 20.7 | 0.318 | 0.000 | 1300/1200 |
| flat/1250/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 19.3 | 1.000 | 0.000 | 1200/1325 |
| flat/1250/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 19.3 | 0.000 | 1.000 | 1325/1200 |
| flat/1250/skai.roc_rider/skai.windrunner | seat1 | eliminated | 16.4 | 0.000 | 0.457 | 1200/1275 |
| flat/1250/skai.windrunner/skai.roc_rider | seat0 | eliminated | 16.7 | 0.402 | 0.000 | 1275/1200 |
| flat/1250/skai.roc_rider/water.grasper | seat0 | eliminated | 32.7 | 1.000 | 0.000 | 1200/1300 |
| flat/1250/water.grasper/skai.roc_rider | seat1 | eliminated | 32.7 | 0.000 | 1.000 | 1300/1200 |
| flat/1250/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 14.1 | 1.000 | 0.000 | 1200/1330 |
| flat/1250/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 14.1 | 0.000 | 1.000 | 1330/1200 |
| flat/1250/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.886 | 1200/1140 |
| flat/1250/water.leviathan/skai.roc_rider | seat0 | eliminated | 4.4 | 0.886 | 0.000 | 1140/1200 |
| flat/1250/skai.roc_rider/water.reef_runner | seat0 | eliminated | 21.8 | 1.000 | 0.000 | 1200/1275 |
| flat/1250/water.reef_runner/skai.roc_rider | seat1 | eliminated | 19.4 | 0.000 | 1.000 | 1275/1200 |
| flat/1250/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 36.6 | 1.000 | 0.959 | 1200/1225 |
| flat/1250/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 36.6 | 0.952 | 1.000 | 1225/1200 |
| flat/1250/skai.roc_rider/water.spoutling | seat0 | eliminated | 12.5 | 0.667 | 0.000 | 1200/1250 |
| flat/1250/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.4 | 0.000 | 0.672 | 1250/1200 |
| flat/1250/skai.roc_rider/skai_pressure_mix | seat0 | eliminated | 27.8 | 0.155 | 0.000 | 1200/1100 |
| flat/1250/skai_pressure_mix/skai.roc_rider | seat1 | eliminated | 19.4 | 0.000 | 0.320 | 1100/1200 |
| flat/1250/skai.roc_rider/skai_expand_mix | seat1 | eliminated | 16.1 | 0.000 | 0.462 | 1200/1300 |
| flat/1250/skai_expand_mix/skai.roc_rider | seat0 | eliminated | 15.5 | 0.489 | 0.000 | 1300/1200 |
| flat/1250/skai.roc_rider/skai_air_mix | seat0 | eliminated | 17.0 | 0.686 | 0.000 | 1200/940 |
| flat/1250/skai_air_mix/skai.roc_rider | seat1 | eliminated | 17.0 | 0.000 | 0.648 | 940/1200 |
| flat/1250/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 31.5 | 1.000 | 0.000 | 1200/1350 |
| flat/1250/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 31.5 | 0.000 | 1.000 | 1350/1200 |
| flat/1250/skai.roc_rider/water_expand_mix | seat0 | eliminated | 28.2 | 0.833 | 0.000 | 1200/865 |
| flat/1250/water_expand_mix/skai.roc_rider | seat1 | eliminated | 34.0 | 0.000 | 0.490 | 865/1200 |
| flat/1250/skai.roc_rider/water_leviathan_mix | seat1 | eliminated | 19.2 | 0.000 | 0.619 | 1200/1235 |
| flat/1250/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 19.2 | 0.762 | 0.000 | 1235/1200 |
| flat/1250/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 9.8 | 0.000 | 0.324 | 1330/1300 |
| flat/1250/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 10.2 | 0.267 | 0.000 | 1300/1330 |
| flat/1250/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1330/1325 |
| flat/1250/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 1325/1330 |
| flat/1250/skai.stormcaller/skai.windrunner | seat1 | eliminated | 11.4 | 0.000 | 0.118 | 1330/1275 |
| flat/1250/skai.windrunner/skai.stormcaller | seat0 | eliminated | 12.1 | 0.062 | 0.000 | 1275/1330 |
| flat/1250/skai.stormcaller/water.grasper | seat0 | eliminated | 11.8 | 0.400 | 0.000 | 1330/1300 |
| flat/1250/water.grasper/skai.stormcaller | seat0 | eliminated | 14.4 | 0.047 | 0.000 | 1300/1330 |
| flat/1250/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 8.8 | 0.000 | 0.269 | 1330/1330 |
| flat/1250/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 8.8 | 0.269 | 0.000 | 1330/1330 |
| flat/1250/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.887 | 1330/1140 |
| flat/1250/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.3 | 0.887 | 0.000 | 1140/1330 |
| flat/1250/skai.stormcaller/water.reef_runner | seat1 | eliminated | 12.3 | 0.000 | 0.050 | 1330/1275 |
| flat/1250/water.reef_runner/skai.stormcaller | seat0 | eliminated | 11.9 | 0.074 | 0.000 | 1275/1330 |
| flat/1250/skai.stormcaller/water.shellback | seat1 | eliminated | 15.4 | 0.000 | 0.919 | 1330/1225 |
| flat/1250/water.shellback/skai.stormcaller | seat0 | eliminated | 15.4 | 1.000 | 0.000 | 1225/1330 |
| flat/1250/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.2 | 0.833 | 0.000 | 1330/1250 |
| flat/1250/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.2 | 0.000 | 0.817 | 1250/1330 |
| flat/1250/skai.stormcaller/skai_pressure_mix | seat0 | eliminated | 11.7 | 0.505 | 0.000 | 1330/1100 |
| flat/1250/skai_pressure_mix/skai.stormcaller | seat1 | eliminated | 11.9 | 0.000 | 0.430 | 1100/1330 |
| flat/1250/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 10.8 | 0.000 | 0.532 | 1330/1300 |
| flat/1250/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 12.3 | 0.364 | 0.000 | 1300/1330 |
| flat/1250/skai.stormcaller/skai_air_mix | seat1 | eliminated | 16.3 | 0.000 | 0.920 | 1330/940 |
| flat/1250/skai_air_mix/skai.stormcaller | seat0 | eliminated | 11.8 | 0.920 | 0.000 | 940/1330 |
| flat/1250/skai.stormcaller/water_pressure_mix | seat1 | eliminated | 12.0 | 0.000 | 0.179 | 1330/1350 |
| flat/1250/water_pressure_mix/skai.stormcaller | seat0 | eliminated | 11.5 | 0.239 | 0.000 | 1350/1330 |
| flat/1250/skai.stormcaller/water_expand_mix | seat1 | eliminated | 16.6 | 0.000 | 0.509 | 1330/865 |
| flat/1250/water_expand_mix/skai.stormcaller | seat0 | eliminated | 22.0 | 0.314 | 0.000 | 865/1330 |
| flat/1250/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 7.7 | 0.000 | 0.913 | 1330/1235 |
| flat/1250/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 8.4 | 0.946 | 0.000 | 1235/1330 |
| flat/1250/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 9.5 | 0.000 | 0.603 | 1300/1325 |
| flat/1250/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 9.3 | 0.627 | 0.000 | 1325/1300 |
| flat/1250/skai.talon_archer/skai.windrunner | seat1 | eliminated | 19.1 | 0.000 | 0.224 | 1300/1275 |
| flat/1250/skai.windrunner/skai.talon_archer | seat0 | eliminated | 17.1 | 0.341 | 0.000 | 1275/1300 |
| flat/1250/skai.talon_archer/water.grasper | seat1 | eliminated | 15.1 | 0.000 | 0.557 | 1300/1300 |
| flat/1250/water.grasper/skai.talon_archer | seat0 | eliminated | 14.9 | 0.544 | 0.000 | 1300/1300 |
| flat/1250/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 17.3 | 0.000 | 0.107 | 1300/1330 |
| flat/1250/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 11.6 | 0.393 | 0.000 | 1330/1300 |
| flat/1250/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.945 | 1300/1140 |
| flat/1250/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.5 | 0.959 | 0.000 | 1140/1300 |
| flat/1250/skai.talon_archer/water.reef_runner | seat1 | eliminated | 24.6 | 0.000 | 0.075 | 1300/1275 |
| flat/1250/water.reef_runner/skai.talon_archer | seat0 | eliminated | 21.9 | 0.149 | 0.000 | 1275/1300 |
| flat/1250/skai.talon_archer/water.shellback | seat1 | eliminated | 22.2 | 0.000 | 0.906 | 1300/1225 |
| flat/1250/water.shellback/skai.talon_archer | seat0 | eliminated | 22.9 | 0.938 | 0.000 | 1225/1300 |
| flat/1250/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.6 | 0.825 | 0.000 | 1300/1250 |
| flat/1250/water.spoutling/skai.talon_archer | seat1 | eliminated | 9.8 | 0.000 | 0.821 | 1250/1300 |
| flat/1250/skai.talon_archer/skai_pressure_mix | seat0 | eliminated | 18.5 | 0.338 | 0.000 | 1300/1100 |
| flat/1250/skai_pressure_mix/skai.talon_archer | seat1 | eliminated | 18.3 | 0.000 | 0.343 | 1100/1300 |
| flat/1250/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 22.9 | 0.000 | 0.157 | 1300/1300 |
| flat/1250/skai_expand_mix/skai.talon_archer | seat0 | eliminated | 31.6 | 0.036 | 0.000 | 1300/1300 |
| flat/1250/skai.talon_archer/skai_air_mix | seat0 | eliminated | 13.8 | 0.310 | 0.000 | 1300/940 |
| flat/1250/skai_air_mix/skai.talon_archer | seat1 | eliminated | 13.7 | 0.000 | 0.382 | 940/1300 |
| flat/1250/skai.talon_archer/water_pressure_mix | seat1 | eliminated | 14.2 | 0.000 | 0.587 | 1300/1350 |
| flat/1250/water_pressure_mix/skai.talon_archer | seat0 | eliminated | 13.7 | 0.603 | 0.000 | 1350/1300 |
| flat/1250/skai.talon_archer/water_expand_mix | seat0 | eliminated | 26.9 | 0.152 | 0.000 | 1300/865 |
| flat/1250/water_expand_mix/skai.talon_archer | seat1 | eliminated | 24.1 | 0.000 | 0.276 | 865/1300 |
| flat/1250/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 10.6 | 0.000 | 0.714 | 1300/1235 |
| flat/1250/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 10.4 | 0.837 | 0.000 | 1235/1300 |
| flat/1250/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.5 | 0.751 | 0.000 | 1325/1275 |
| flat/1250/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 0.736 | 1275/1325 |
| flat/1250/skai.thunderhead/water.grasper | seat0 | eliminated | 9.3 | 1.000 | 0.000 | 1325/1300 |
| flat/1250/water.grasper/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 1300/1325 |
| flat/1250/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 4.5 | 1.000 | 0.000 | 1325/1330 |
| flat/1250/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1330/1325 |
| flat/1250/skai.thunderhead/water.leviathan | seat1 | eliminated | 7.1 | 0.000 | 0.336 | 1325/1140 |
| flat/1250/water.leviathan/skai.thunderhead | seat0 | eliminated | 7.1 | 0.336 | 0.000 | 1140/1325 |
| flat/1250/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1325/1275 |
| flat/1250/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 1275/1325 |
| flat/1250/skai.thunderhead/water.shellback | seat0 | eliminated | 39.2 | 1.000 | 0.000 | 1325/1225 |
| flat/1250/water.shellback/skai.thunderhead | seat1 | eliminated | 34.3 | 0.000 | 1.000 | 1225/1325 |
| flat/1250/skai.thunderhead/water.spoutling | seat0 | eliminated | 6.5 | 0.884 | 0.000 | 1325/1250 |
| flat/1250/water.spoutling/skai.thunderhead | seat1 | eliminated | 6.6 | 0.000 | 0.884 | 1250/1325 |
| flat/1250/skai.thunderhead/skai_pressure_mix | seat0 | eliminated | 9.2 | 0.771 | 0.000 | 1325/1100 |
| flat/1250/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 0.777 | 1100/1325 |
| flat/1250/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 43.3 | 0.000 | 0.308 | 1325/1300 |
| flat/1250/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 43.3 | 0.308 | 0.000 | 1300/1325 |
| flat/1250/skai.thunderhead/skai_air_mix | seat1 | eliminated | 38.6 | 0.000 | 0.920 | 1325/940 |
| flat/1250/skai_air_mix/skai.thunderhead | seat0 | eliminated | 38.6 | 0.920 | 0.000 | 940/1325 |
| flat/1250/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 9.0 | 1.000 | 0.000 | 1325/1350 |
| flat/1250/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 1350/1325 |
| flat/1250/skai.thunderhead/water_expand_mix | seat0 | eliminated | 12.1 | 0.800 | 0.000 | 1325/865 |
| flat/1250/water_expand_mix/skai.thunderhead | seat1 | eliminated | 14.2 | 0.000 | 0.779 | 865/1325 |
| flat/1250/skai.thunderhead/water_leviathan_mix | seat1 | eliminated | 19.2 | 0.000 | 0.404 | 1325/1235 |
| flat/1250/water_leviathan_mix/skai.thunderhead | seat0 | eliminated | 19.1 | 0.138 | 0.000 | 1235/1325 |
| flat/1250/skai.windrunner/water.grasper | seat1 | eliminated | 11.6 | 0.000 | 0.604 | 1275/1300 |
| flat/1250/water.grasper/skai.windrunner | seat0 | eliminated | 11.6 | 0.606 | 0.000 | 1300/1275 |
| flat/1250/skai.windrunner/water.ink_lobber | seat1 | eliminated | 11.0 | 0.000 | 0.325 | 1275/1330 |
| flat/1250/water.ink_lobber/skai.windrunner | seat0 | eliminated | 13.8 | 0.267 | 0.000 | 1330/1275 |
| flat/1250/skai.windrunner/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.974 | 1275/1140 |
| flat/1250/water.leviathan/skai.windrunner | seat0 | eliminated | 4.0 | 0.974 | 0.000 | 1140/1275 |
| flat/1250/skai.windrunner/water.reef_runner | seat0 | eliminated | 19.9 | 0.071 | 0.000 | 1275/1275 |
| flat/1250/water.reef_runner/skai.windrunner | seat1 | eliminated | 20.6 | 0.000 | 0.035 | 1275/1275 |
| flat/1250/skai.windrunner/water.shellback | seat1 | eliminated | 19.1 | 0.000 | 0.871 | 1275/1225 |
| flat/1250/water.shellback/skai.windrunner | seat0 | eliminated | 18.4 | 0.983 | 0.000 | 1225/1275 |
| flat/1250/skai.windrunner/water.spoutling | seat0 | eliminated | 8.1 | 0.799 | 0.000 | 1275/1250 |
| flat/1250/water.spoutling/skai.windrunner | seat1 | eliminated | 8.6 | 0.000 | 0.794 | 1250/1275 |
| flat/1250/skai.windrunner/skai_pressure_mix | seat0 | eliminated | 11.4 | 0.539 | 0.000 | 1275/1100 |
| flat/1250/skai_pressure_mix/skai.windrunner | seat1 | eliminated | 12.5 | 0.000 | 0.457 | 1100/1275 |
| flat/1250/skai.windrunner/skai_expand_mix | seat1 | eliminated | 24.1 | 0.000 | 0.097 | 1275/1300 |
| flat/1250/skai_expand_mix/skai.windrunner | seat1 | eliminated | 20.8 | 0.000 | 0.138 | 1300/1275 |
| flat/1250/skai.windrunner/skai_air_mix | seat0 | eliminated | 12.5 | 0.256 | 0.000 | 1275/940 |
| flat/1250/skai_air_mix/skai.windrunner | seat1 | eliminated | 12.1 | 0.000 | 0.320 | 940/1275 |
| flat/1250/skai.windrunner/water_pressure_mix | seat1 | eliminated | 11.6 | 0.000 | 0.625 | 1275/1350 |
| flat/1250/water_pressure_mix/skai.windrunner | seat0 | eliminated | 10.7 | 0.646 | 0.000 | 1350/1275 |
| flat/1250/skai.windrunner/water_expand_mix | seat0 | eliminated | 21.1 | 0.207 | 0.000 | 1275/865 |
| flat/1250/water_expand_mix/skai.windrunner | seat0 | eliminated | 25.1 | 0.413 | 0.000 | 865/1275 |
| flat/1250/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 7.3 | 0.000 | 0.947 | 1275/1235 |
| flat/1250/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 7.4 | 0.905 | 0.000 | 1235/1275 |
| flat/1250/water.grasper/water.ink_lobber | seat1 | eliminated | 13.9 | 0.000 | 0.274 | 1300/1330 |
| flat/1250/water.ink_lobber/water.grasper | seat0 | eliminated | 11.1 | 0.486 | 0.000 | 1330/1300 |
| flat/1250/water.grasper/water.leviathan | seat1 | eliminated | 4.8 | 0.000 | 0.991 | 1300/1140 |
| flat/1250/water.leviathan/water.grasper | seat0 | eliminated | 4.8 | 1.000 | 0.000 | 1140/1300 |
| flat/1250/water.grasper/water.reef_runner | seat0 | eliminated | 11.8 | 0.607 | 0.000 | 1300/1275 |
| flat/1250/water.reef_runner/water.grasper | seat1 | eliminated | 11.2 | 0.000 | 0.624 | 1275/1300 |
| flat/1250/water.grasper/water.shellback | seat0 | eliminated | 19.6 | 0.650 | 0.000 | 1300/1225 |
| flat/1250/water.shellback/water.grasper | seat1 | eliminated | 19.7 | 0.000 | 0.628 | 1225/1300 |
| flat/1250/water.grasper/water.spoutling | seat0 | eliminated | 9.1 | 0.811 | 0.000 | 1300/1250 |
| flat/1250/water.spoutling/water.grasper | seat1 | eliminated | 9.0 | 0.000 | 0.825 | 1250/1300 |
| flat/1250/water.grasper/skai_pressure_mix | seat0 | eliminated | 10.4 | 0.699 | 0.000 | 1300/1100 |
| flat/1250/skai_pressure_mix/water.grasper | seat1 | eliminated | 10.4 | 0.000 | 0.703 | 1100/1300 |
| flat/1250/water.grasper/skai_expand_mix | seat1 | eliminated | 74.3 | 0.000 | 0.308 | 1300/1300 |
| flat/1250/skai_expand_mix/water.grasper | seat0 | eliminated | 69.9 | 0.308 | 0.000 | 1300/1300 |
| flat/1250/water.grasper/skai_air_mix | seat1 | eliminated | 24.1 | 0.000 | 0.920 | 1300/940 |
| flat/1250/skai_air_mix/water.grasper | seat0 | eliminated | 23.2 | 0.920 | 0.000 | 940/1300 |
| flat/1250/water.grasper/water_pressure_mix | seat0 | eliminated | 26.9 | 0.130 | 0.000 | 1300/1350 |
| flat/1250/water_pressure_mix/water.grasper | seat0 | eliminated | 28.7 | 0.056 | 0.000 | 1350/1300 |
| flat/1250/water.grasper/water_expand_mix | seat0 | eliminated | 11.8 | 0.621 | 0.000 | 1300/865 |
| flat/1250/water_expand_mix/water.grasper | seat1 | eliminated | 11.5 | 0.000 | 0.672 | 865/1300 |
| flat/1250/water.grasper/water_leviathan_mix | seat1 | eliminated | 11.9 | 0.000 | 0.487 | 1300/1235 |
| flat/1250/water_leviathan_mix/water.grasper | seat0 | eliminated | 10.6 | 0.728 | 0.000 | 1235/1300 |
| flat/1250/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.9 | 0.000 | 0.775 | 1330/1140 |
| flat/1250/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.9 | 0.775 | 0.000 | 1140/1330 |
| flat/1250/water.ink_lobber/water.reef_runner | seat0 | eliminated | 13.8 | 0.357 | 0.000 | 1330/1275 |
| flat/1250/water.reef_runner/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.300 | 1275/1330 |
| flat/1250/water.ink_lobber/water.shellback | seat1 | eliminated | 17.1 | 0.000 | 0.930 | 1330/1225 |
| flat/1250/water.shellback/water.ink_lobber | seat0 | eliminated | 17.1 | 1.000 | 0.000 | 1225/1330 |
| flat/1250/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.8 | 0.871 | 0.000 | 1330/1250 |
| flat/1250/water.spoutling/water.ink_lobber | seat1 | eliminated | 5.5 | 0.000 | 0.879 | 1250/1330 |
| flat/1250/water.ink_lobber/skai_pressure_mix | seat0 | eliminated | 13.8 | 0.541 | 0.000 | 1330/1100 |
| flat/1250/skai_pressure_mix/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.497 | 1100/1330 |
| flat/1250/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 16.2 | 0.000 | 0.308 | 1330/1300 |
| flat/1250/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 26.6 | 0.308 | 0.000 | 1300/1330 |
| flat/1250/water.ink_lobber/skai_air_mix | seat1 | eliminated | 16.3 | 0.000 | 0.920 | 1330/940 |
| flat/1250/skai_air_mix/water.ink_lobber | seat0 | eliminated | 11.8 | 0.920 | 0.000 | 940/1330 |
| flat/1250/water.ink_lobber/water_pressure_mix | seat0 | eliminated | 13.8 | 0.291 | 0.000 | 1330/1350 |
| flat/1250/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 23.0 | 0.000 | 0.014 | 1350/1330 |
| flat/1250/water.ink_lobber/water_expand_mix | seat0 | eliminated | 17.1 | 0.283 | 0.000 | 1330/865 |
| flat/1250/water_expand_mix/water.ink_lobber | seat1 | eliminated | 15.7 | 0.000 | 0.484 | 865/1330 |
| flat/1250/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 8.7 | 0.000 | 0.871 | 1330/1235 |
| flat/1250/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 10.2 | 0.725 | 0.000 | 1235/1330 |
| flat/1250/water.leviathan/water.reef_runner | seat0 | eliminated | 4.0 | 0.981 | 0.000 | 1140/1275 |
| flat/1250/water.reef_runner/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.981 | 1275/1140 |
| flat/1250/water.leviathan/water.shellback | seat1 | eliminated | 26.9 | 0.000 | 0.481 | 1140/1225 |
| flat/1250/water.shellback/water.leviathan | seat0 | eliminated | 25.7 | 0.844 | 0.000 | 1225/1140 |
| flat/1250/water.leviathan/water.spoutling | seat0 | eliminated | 4.2 | 0.984 | 0.000 | 1140/1250 |
| flat/1250/water.spoutling/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.995 | 1250/1140 |
| flat/1250/water.leviathan/skai_pressure_mix | seat0 | eliminated | 4.3 | 0.973 | 0.000 | 1140/1100 |
| flat/1250/skai_pressure_mix/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.979 | 1100/1140 |
| flat/1250/water.leviathan/skai_expand_mix | seat0 | eliminated | 4.4 | 0.932 | 0.000 | 1140/1300 |
| flat/1250/skai_expand_mix/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.914 | 1300/1140 |
| flat/1250/water.leviathan/skai_air_mix | seat0 | eliminated | 4.1 | 0.926 | 0.000 | 1140/940 |
| flat/1250/skai_air_mix/water.leviathan | seat1 | eliminated | 3.9 | 0.000 | 0.883 | 940/1140 |
| flat/1250/water.leviathan/water_pressure_mix | seat0 | eliminated | 4.9 | 1.000 | 0.000 | 1140/1350 |
| flat/1250/water_pressure_mix/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 1350/1140 |
| flat/1250/water.leviathan/water_expand_mix | seat0 | eliminated | 10.9 | 0.755 | 0.000 | 1140/865 |
| flat/1250/water_expand_mix/water.leviathan | seat1 | eliminated | 10.9 | 0.000 | 0.809 | 865/1140 |
| flat/1250/water.leviathan/water_leviathan_mix | seat1 | eliminated | 14.5 | 0.000 | 0.392 | 1140/1235 |
| flat/1250/water_leviathan_mix/water.leviathan | seat0 | eliminated | 16.0 | 0.438 | 0.000 | 1235/1140 |
| flat/1250/water.reef_runner/water.shellback | seat1 | eliminated | 19.3 | 0.000 | 0.988 | 1275/1225 |
| flat/1250/water.shellback/water.reef_runner | seat0 | eliminated | 18.5 | 0.977 | 0.000 | 1225/1275 |
| flat/1250/water.reef_runner/water.spoutling | seat0 | eliminated | 9.0 | 0.770 | 0.000 | 1275/1250 |
| flat/1250/water.spoutling/water.reef_runner | seat1 | eliminated | 8.8 | 0.000 | 0.784 | 1250/1275 |
| flat/1250/water.reef_runner/skai_pressure_mix | seat0 | eliminated | 12.1 | 0.504 | 0.000 | 1275/1100 |
| flat/1250/skai_pressure_mix/water.reef_runner | seat1 | eliminated | 12.3 | 0.000 | 0.471 | 1100/1275 |
| flat/1250/water.reef_runner/skai_expand_mix | seat1 | eliminated | 37.4 | 0.000 | 0.308 | 1275/1300 |
| flat/1250/skai_expand_mix/water.reef_runner | seat0 | eliminated | 35.0 | 0.308 | 0.000 | 1300/1275 |
| flat/1250/water.reef_runner/skai_air_mix | seat1 | eliminated | 16.5 | 0.000 | 0.920 | 1275/940 |
| flat/1250/skai_air_mix/water.reef_runner | seat0 | eliminated | 16.4 | 0.920 | 0.000 | 940/1275 |
| flat/1250/water.reef_runner/water_pressure_mix | seat1 | eliminated | 11.3 | 0.000 | 0.627 | 1275/1350 |
| flat/1250/water_pressure_mix/water.reef_runner | seat0 | eliminated | 10.9 | 0.654 | 0.000 | 1350/1275 |
| flat/1250/water.reef_runner/water_expand_mix | seat0 | eliminated | 22.0 | 0.224 | 0.000 | 1275/865 |
| flat/1250/water_expand_mix/water.reef_runner | seat0 | eliminated | 25.7 | 0.403 | 0.000 | 865/1275 |
| flat/1250/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 7.7 | 0.000 | 0.913 | 1275/1235 |
| flat/1250/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 7.6 | 0.939 | 0.000 | 1235/1275 |
| flat/1250/water.shellback/water.spoutling | seat0 | eliminated | 15.3 | 0.994 | 0.000 | 1225/1250 |
| flat/1250/water.spoutling/water.shellback | seat1 | eliminated | 16.0 | 0.000 | 0.987 | 1250/1225 |
| flat/1250/water.shellback/skai_pressure_mix | seat0 | eliminated | 17.5 | 0.987 | 0.000 | 1225/1100 |
| flat/1250/skai_pressure_mix/water.shellback | seat1 | eliminated | 17.6 | 0.000 | 0.979 | 1100/1225 |
| flat/1250/water.shellback/skai_expand_mix | stalemate | dome_absorbed | 47.4 | 0.869 | 0.308 | 1225/1300 |
| flat/1250/skai_expand_mix/water.shellback | stalemate | dome_absorbed | 47.5 | 0.308 | 0.808 | 1300/1225 |
| flat/1250/water.shellback/skai_air_mix | stalemate | dome_absorbed | 37.7 | 0.966 | 0.920 | 1225/940 |
| flat/1250/skai_air_mix/water.shellback | stalemate | dome_absorbed | 37.8 | 0.920 | 0.924 | 940/1225 |
| flat/1250/water.shellback/water_pressure_mix | seat1 | eliminated | 22.0 | 0.000 | 0.584 | 1225/1350 |
| flat/1250/water_pressure_mix/water.shellback | seat0 | eliminated | 21.3 | 0.608 | 0.000 | 1350/1225 |
| flat/1250/water.shellback/water_expand_mix | seat0 | eliminated | 27.6 | 0.882 | 0.000 | 1225/865 |
| flat/1250/water_expand_mix/water.shellback | seat1 | eliminated | 30.3 | 0.000 | 0.895 | 865/1225 |
| flat/1250/water.shellback/water_leviathan_mix | seat0 | eliminated | 31.7 | 0.990 | 0.000 | 1225/1235 |
| flat/1250/water_leviathan_mix/water.shellback | seat1 | eliminated | 32.5 | 0.000 | 0.935 | 1235/1225 |
| flat/1250/water.spoutling/skai_pressure_mix | seat1 | eliminated | 10.2 | 0.000 | 0.710 | 1250/1100 |
| flat/1250/skai_pressure_mix/water.spoutling | seat0 | eliminated | 9.9 | 0.705 | 0.000 | 1100/1250 |
| flat/1250/water.spoutling/skai_expand_mix | seat1 | eliminated | 9.2 | 0.000 | 0.779 | 1250/1300 |
| flat/1250/skai_expand_mix/water.spoutling | seat0 | eliminated | 9.0 | 0.788 | 0.000 | 1300/1250 |
| flat/1250/water.spoutling/skai_air_mix | seat1 | eliminated | 11.7 | 0.000 | 0.593 | 1250/940 |
| flat/1250/skai_air_mix/water.spoutling | seat0 | eliminated | 13.7 | 0.594 | 0.000 | 940/1250 |
| flat/1250/water.spoutling/water_pressure_mix | seat1 | eliminated | 8.8 | 0.000 | 0.828 | 1250/1350 |
| flat/1250/water_pressure_mix/water.spoutling | seat0 | eliminated | 8.8 | 0.835 | 0.000 | 1350/1250 |
| flat/1250/water.spoutling/water_expand_mix | seat1 | eliminated | 13.5 | 0.000 | 0.850 | 1250/865 |
| flat/1250/water_expand_mix/water.spoutling | seat0 | eliminated | 14.9 | 0.884 | 0.000 | 865/1250 |
| flat/1250/water.spoutling/water_leviathan_mix | seat1 | eliminated | 7.6 | 0.000 | 0.978 | 1250/1235 |
| flat/1250/water_leviathan_mix/water.spoutling | seat0 | eliminated | 5.8 | 1.000 | 0.000 | 1235/1250 |
| flat/1250/skai_pressure_mix/skai_expand_mix | seat1 | eliminated | 13.6 | 0.000 | 0.533 | 1100/1300 |
| flat/1250/skai_expand_mix/skai_pressure_mix | seat0 | eliminated | 12.9 | 0.550 | 0.000 | 1300/1100 |
| flat/1250/skai_pressure_mix/skai_air_mix | seat0 | eliminated | 19.6 | 0.154 | 0.000 | 1100/940 |
| flat/1250/skai_air_mix/skai_pressure_mix | seat0 | eliminated | 23.9 | 0.066 | 0.000 | 940/1100 |
| flat/1250/skai_pressure_mix/water_pressure_mix | seat1 | eliminated | 10.0 | 0.000 | 0.732 | 1100/1350 |
| flat/1250/water_pressure_mix/skai_pressure_mix | seat0 | eliminated | 9.9 | 0.730 | 0.000 | 1350/1100 |
| flat/1250/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 21.8 | 0.000 | 0.459 | 1100/865 |
| flat/1250/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 22.7 | 0.607 | 0.000 | 865/1100 |
| flat/1250/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 10.3 | 0.000 | 0.899 | 1100/1235 |
| flat/1250/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 8.2 | 1.000 | 0.000 | 1235/1100 |
| flat/1250/skai_expand_mix/skai_air_mix | seat0 | eliminated | 14.0 | 0.461 | 0.000 | 1300/940 |
| flat/1250/skai_air_mix/skai_expand_mix | seat1 | eliminated | 14.0 | 0.000 | 0.503 | 940/1300 |
| flat/1250/skai_expand_mix/water_pressure_mix | seat0 | eliminated | 69.9 | 0.308 | 0.000 | 1300/1350 |
| flat/1250/water_pressure_mix/skai_expand_mix | seat1 | eliminated | 69.9 | 0.000 | 0.308 | 1350/1300 |
| flat/1250/skai_expand_mix/water_expand_mix | seat0 | eliminated | 41.3 | 0.269 | 0.000 | 1300/865 |
| flat/1250/water_expand_mix/skai_expand_mix | seat0 | eliminated | 28.3 | 0.520 | 0.000 | 865/1300 |
| flat/1250/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 10.2 | 0.000 | 0.781 | 1300/1235 |
| flat/1250/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 9.8 | 0.860 | 0.000 | 1235/1300 |
| flat/1250/skai_air_mix/water_pressure_mix | seat0 | eliminated | 23.1 | 0.920 | 0.000 | 940/1350 |
| flat/1250/water_pressure_mix/skai_air_mix | seat1 | eliminated | 26.4 | 0.000 | 0.920 | 1350/940 |
| flat/1250/skai_air_mix/water_expand_mix | seat0 | eliminated | 32.8 | 0.726 | 0.000 | 940/865 |
| flat/1250/water_expand_mix/skai_air_mix | seat0 | eliminated | 30.0 | 0.618 | 0.000 | 865/940 |
| flat/1250/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 10.1 | 0.000 | 0.793 | 940/1235 |
| flat/1250/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 13.3 | 0.802 | 0.000 | 1235/940 |
| flat/1250/water_pressure_mix/water_expand_mix | seat0 | eliminated | 12.4 | 0.585 | 0.000 | 1350/865 |
| flat/1250/water_expand_mix/water_pressure_mix | seat1 | eliminated | 11.5 | 0.000 | 0.735 | 865/1350 |
| flat/1250/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 11.6 | 0.000 | 0.533 | 1350/1235 |
| flat/1250/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 10.6 | 0.765 | 0.000 | 1235/1350 |
| flat/1250/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 16.6 | 0.000 | 0.899 | 865/1235 |
| flat/1250/water_leviathan_mix/water_expand_mix | seat0 | eliminated | 20.0 | 0.776 | 0.000 | 1235/865 |
| flat/2750/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.4 | 1.000 | 0.000 | 2800/2660 |
| flat/2750/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 11.4 | 0.000 | 1.000 | 2660/2800 |
| flat/2750/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 21.7 | 0.000 | 0.373 | 2800/2800 |
| flat/2750/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 22.9 | 0.360 | 0.000 | 2800/2800 |
| flat/2750/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 18.1 | 1.000 | 0.000 | 2800/2650 |
| flat/2750/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 18.1 | 0.000 | 1.000 | 2650/2800 |
| flat/2750/skai.roc_rider/skai.windrunner | seat1 | eliminated | 17.4 | 0.000 | 0.450 | 2800/2775 |
| flat/2750/skai.windrunner/skai.roc_rider | seat0 | eliminated | 16.8 | 0.473 | 0.000 | 2775/2800 |
| flat/2750/skai.roc_rider/water.grasper | seat0 | eliminated | 30.2 | 1.000 | 0.000 | 2800/2800 |
| flat/2750/water.grasper/skai.roc_rider | seat1 | eliminated | 29.3 | 0.000 | 1.000 | 2800/2800 |
| flat/2750/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 15.7 | 1.000 | 0.000 | 2800/2660 |
| flat/2750/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 11.2 | 0.000 | 1.000 | 2660/2800 |
| flat/2750/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.940 | 2800/2660 |
| flat/2750/water.leviathan/skai.roc_rider | seat0 | eliminated | 4.2 | 0.940 | 0.000 | 2660/2800 |
| flat/2750/skai.roc_rider/water.reef_runner | seat0 | eliminated | 19.4 | 1.000 | 0.000 | 2800/2775 |
| flat/2750/water.reef_runner/skai.roc_rider | seat1 | eliminated | 19.6 | 0.000 | 1.000 | 2775/2800 |
| flat/2750/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 161.7 | 1.000 | 0.712 | 2800/2800 |
| flat/2750/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 38.0 | 0.973 | 1.000 | 2800/2800 |
| flat/2750/skai.roc_rider/water.spoutling | seat0 | eliminated | 12.6 | 0.714 | 0.000 | 2800/2750 |
| flat/2750/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.5 | 0.000 | 0.717 | 2750/2800 |
| flat/2750/skai.roc_rider/skai_pressure_mix | seat1 | eliminated | 18.8 | 0.000 | 0.439 | 2800/2750 |
| flat/2750/skai_pressure_mix/skai.roc_rider | seat0 | eliminated | 18.6 | 0.445 | 0.000 | 2750/2800 |
| flat/2750/skai.roc_rider/skai_expand_mix | seat1 | eliminated | 32.3 | 0.000 | 0.124 | 2800/2600 |
| flat/2750/skai_expand_mix/skai.roc_rider | seat1 | eliminated | 34.2 | 0.000 | 0.092 | 2600/2800 |
| flat/2750/skai.roc_rider/skai_air_mix | seat0 | eliminated | 27.7 | 0.365 | 0.000 | 2800/2820 |
| flat/2750/skai_air_mix/skai.roc_rider | seat1 | eliminated | 30.2 | 0.000 | 0.277 | 2820/2800 |
| flat/2750/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 26.8 | 1.000 | 0.000 | 2800/2700 |
| flat/2750/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 26.9 | 0.000 | 1.000 | 2700/2800 |
| flat/2750/skai.roc_rider/water_expand_mix | seat1 | eliminated | 34.7 | 0.000 | 0.717 | 2800/2595 |
| flat/2750/water_expand_mix/skai.roc_rider | seat0 | eliminated | 31.2 | 0.708 | 0.000 | 2595/2800 |
| flat/2750/skai.roc_rider/water_leviathan_mix | seat1 | eliminated | 10.5 | 0.000 | 0.726 | 2800/2470 |
| flat/2750/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 10.5 | 0.841 | 0.000 | 2470/2800 |
| flat/2750/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 16.2 | 0.000 | 0.060 | 2660/2800 |
| flat/2750/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 15.4 | 0.096 | 0.000 | 2800/2660 |
| flat/2750/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 1.000 | 2660/2650 |
| flat/2750/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 2650/2660 |
| flat/2750/skai.stormcaller/skai.windrunner | seat1 | eliminated | 10.3 | 0.000 | 0.160 | 2660/2775 |
| flat/2750/skai.windrunner/skai.stormcaller | seat0 | eliminated | 11.2 | 0.145 | 0.000 | 2775/2660 |
| flat/2750/skai.stormcaller/water.grasper | seat0 | eliminated | 12.3 | 0.343 | 0.000 | 2660/2800 |
| flat/2750/water.grasper/skai.stormcaller | seat1 | eliminated | 17.0 | 0.000 | 0.148 | 2800/2660 |
| flat/2750/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.0 | 0.000 | 0.137 | 2660/2660 |
| flat/2750/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 7.7 | 0.314 | 0.000 | 2660/2660 |
| flat/2750/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.952 | 2660/2660 |
| flat/2750/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.3 | 0.952 | 0.000 | 2660/2660 |
| flat/2750/skai.stormcaller/water.reef_runner | seat1 | eliminated | 13.0 | 0.000 | 0.096 | 2660/2775 |
| flat/2750/water.reef_runner/skai.stormcaller | seat1 | eliminated | 14.4 | 0.000 | 0.248 | 2775/2660 |
| flat/2750/skai.stormcaller/water.shellback | seat1 | eliminated | 15.4 | 0.000 | 0.934 | 2660/2800 |
| flat/2750/water.shellback/skai.stormcaller | seat0 | eliminated | 15.4 | 0.925 | 0.000 | 2800/2660 |
| flat/2750/skai.stormcaller/water.spoutling | seat0 | eliminated | 9.2 | 0.794 | 0.000 | 2660/2750 |
| flat/2750/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.4 | 0.000 | 0.825 | 2750/2660 |
| flat/2750/skai.stormcaller/skai_pressure_mix | seat1 | eliminated | 11.1 | 0.000 | 0.279 | 2660/2750 |
| flat/2750/skai_pressure_mix/skai.stormcaller | seat0 | eliminated | 13.6 | 0.214 | 0.000 | 2750/2660 |
| flat/2750/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 15.5 | 0.000 | 0.318 | 2660/2600 |
| flat/2750/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 14.9 | 0.339 | 0.000 | 2600/2660 |
| flat/2750/skai.stormcaller/skai_air_mix | seat1 | eliminated | 7.2 | 0.000 | 0.920 | 2660/2820 |
| flat/2750/skai_air_mix/skai.stormcaller | seat0 | eliminated | 7.9 | 0.920 | 0.000 | 2820/2660 |
| flat/2750/skai.stormcaller/water_pressure_mix | seat0 | eliminated | 14.3 | 0.419 | 0.000 | 2660/2700 |
| flat/2750/water_pressure_mix/skai.stormcaller | seat1 | eliminated | 14.5 | 0.000 | 0.276 | 2700/2660 |
| flat/2750/skai.stormcaller/water_expand_mix | seat1 | eliminated | 14.1 | 0.000 | 0.749 | 2660/2595 |
| flat/2750/water_expand_mix/skai.stormcaller | seat0 | eliminated | 14.4 | 0.721 | 0.000 | 2595/2660 |
| flat/2750/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 8.1 | 0.000 | 0.899 | 2660/2470 |
| flat/2750/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 8.0 | 1.000 | 0.000 | 2470/2660 |
| flat/2750/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 7.3 | 0.000 | 0.697 | 2800/2650 |
| flat/2750/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 7.3 | 0.685 | 0.000 | 2650/2800 |
| flat/2750/skai.talon_archer/skai.windrunner | seat1 | eliminated | 20.7 | 0.000 | 0.227 | 2800/2775 |
| flat/2750/skai.windrunner/skai.talon_archer | seat0 | eliminated | 21.6 | 0.196 | 0.000 | 2775/2800 |
| flat/2750/skai.talon_archer/water.grasper | seat1 | eliminated | 14.8 | 0.000 | 0.597 | 2800/2800 |
| flat/2750/water.grasper/skai.talon_archer | seat0 | eliminated | 14.9 | 0.569 | 0.000 | 2800/2800 |
| flat/2750/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 11.6 | 0.000 | 0.353 | 2800/2660 |
| flat/2750/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 11.2 | 0.429 | 0.000 | 2660/2800 |
| flat/2750/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.965 | 2800/2660 |
| flat/2750/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.974 | 0.000 | 2660/2800 |
| flat/2750/skai.talon_archer/water.reef_runner | seat0 | eliminated | 35.3 | 0.002 | 0.000 | 2800/2775 |
| flat/2750/water.reef_runner/skai.talon_archer | seat0 | eliminated | 26.7 | 0.083 | 0.000 | 2775/2800 |
| flat/2750/skai.talon_archer/water.shellback | seat1 | eliminated | 23.5 | 0.000 | 0.922 | 2800/2800 |
| flat/2750/water.shellback/skai.talon_archer | seat0 | eliminated | 22.9 | 0.926 | 0.000 | 2800/2800 |
| flat/2750/skai.talon_archer/water.spoutling | seat0 | eliminated | 10.3 | 0.817 | 0.000 | 2800/2750 |
| flat/2750/water.spoutling/skai.talon_archer | seat1 | eliminated | 10.2 | 0.000 | 0.821 | 2750/2800 |
| flat/2750/skai.talon_archer/skai_pressure_mix | seat0 | eliminated | 23.0 | 0.214 | 0.000 | 2800/2750 |
| flat/2750/skai_pressure_mix/skai.talon_archer | seat1 | eliminated | 25.3 | 0.000 | 0.169 | 2750/2800 |
| flat/2750/skai.talon_archer/skai_expand_mix | seat0 | eliminated | 20.7 | 0.361 | 0.000 | 2800/2600 |
| flat/2750/skai_expand_mix/skai.talon_archer | seat1 | eliminated | 19.3 | 0.000 | 0.387 | 2600/2800 |
| flat/2750/skai.talon_archer/skai_air_mix | seat1 | eliminated | 12.5 | 0.000 | 0.510 | 2800/2820 |
| flat/2750/skai_air_mix/skai.talon_archer | seat0 | eliminated | 14.6 | 0.459 | 0.000 | 2820/2800 |
| flat/2750/skai.talon_archer/water_pressure_mix | seat1 | eliminated | 15.2 | 0.000 | 0.562 | 2800/2700 |
| flat/2750/water_pressure_mix/skai.talon_archer | seat0 | eliminated | 15.4 | 0.575 | 0.000 | 2700/2800 |
| flat/2750/skai.talon_archer/water_expand_mix | seat1 | eliminated | 19.1 | 0.000 | 0.786 | 2800/2595 |
| flat/2750/water_expand_mix/skai.talon_archer | seat0 | eliminated | 21.5 | 0.715 | 0.000 | 2595/2800 |
| flat/2750/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 9.6 | 0.000 | 0.861 | 2800/2470 |
| flat/2750/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 9.6 | 0.882 | 0.000 | 2470/2800 |
| flat/2750/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.6 | 0.798 | 0.000 | 2650/2775 |
| flat/2750/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.6 | 0.000 | 0.803 | 2775/2650 |
| flat/2750/skai.thunderhead/water.grasper | seat0 | eliminated | 7.1 | 1.000 | 0.000 | 2650/2800 |
| flat/2750/water.grasper/skai.thunderhead | seat1 | eliminated | 7.1 | 0.000 | 1.000 | 2800/2650 |
| flat/2750/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 4.7 | 1.000 | 0.000 | 2650/2660 |
| flat/2750/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 2660/2650 |
| flat/2750/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.753 | 2650/2660 |
| flat/2750/water.leviathan/skai.thunderhead | seat0 | eliminated | 4.2 | 0.753 | 0.000 | 2660/2650 |
| flat/2750/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.4 | 1.000 | 0.000 | 2650/2775 |
| flat/2750/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 2775/2650 |
| flat/2750/skai.thunderhead/water.shellback | seat0 | eliminated | 47.1 | 1.000 | 0.000 | 2650/2800 |
| flat/2750/water.shellback/skai.thunderhead | stalemate | dome_absorbed | 64.4 | 0.790 | 1.000 | 2800/2650 |
| flat/2750/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.9 | 0.876 | 0.000 | 2650/2750 |
| flat/2750/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.8 | 0.000 | 0.892 | 2750/2650 |
| flat/2750/skai.thunderhead/skai_pressure_mix | seat0 | eliminated | 6.6 | 0.807 | 0.000 | 2650/2750 |
| flat/2750/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 0.799 | 2750/2650 |
| flat/2750/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 50.5 | 0.000 | 0.308 | 2650/2600 |
| flat/2750/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 50.5 | 0.308 | 0.000 | 2600/2650 |
| flat/2750/skai.thunderhead/skai_air_mix | seat1 | eliminated | 27.6 | 0.000 | 0.920 | 2650/2820 |
| flat/2750/skai_air_mix/skai.thunderhead | seat0 | eliminated | 26.5 | 0.920 | 0.000 | 2820/2650 |
| flat/2750/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 8.0 | 1.000 | 0.000 | 2650/2700 |
| flat/2750/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 2700/2650 |
| flat/2750/skai.thunderhead/water_expand_mix | seat0 | eliminated | 19.3 | 0.506 | 0.000 | 2650/2595 |
| flat/2750/water_expand_mix/skai.thunderhead | seat1 | eliminated | 14.6 | 0.000 | 0.601 | 2595/2650 |
| flat/2750/skai.thunderhead/water_leviathan_mix | seat1 | eliminated | 17.5 | 0.000 | 0.344 | 2650/2470 |
| flat/2750/water_leviathan_mix/skai.thunderhead | seat0 | eliminated | 16.8 | 0.315 | 0.000 | 2470/2650 |
| flat/2750/skai.windrunner/water.grasper | seat1 | eliminated | 11.8 | 0.000 | 0.618 | 2775/2800 |
| flat/2750/water.grasper/skai.windrunner | seat0 | eliminated | 11.9 | 0.608 | 0.000 | 2800/2775 |
| flat/2750/skai.windrunner/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.419 | 2775/2660 |
| flat/2750/water.ink_lobber/skai.windrunner | seat0 | eliminated | 10.9 | 0.507 | 0.000 | 2660/2775 |
| flat/2750/skai.windrunner/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.998 | 2775/2660 |
| flat/2750/water.leviathan/skai.windrunner | seat0 | eliminated | 4.0 | 0.998 | 0.000 | 2660/2775 |
| flat/2750/skai.windrunner/water.reef_runner | seat0 | eliminated | 20.6 | 0.114 | 0.000 | 2775/2775 |
| flat/2750/water.reef_runner/skai.windrunner | seat1 | eliminated | 24.9 | 0.000 | 0.027 | 2775/2775 |
| flat/2750/skai.windrunner/water.shellback | seat1 | eliminated | 19.6 | 0.000 | 0.931 | 2775/2800 |
| flat/2750/water.shellback/skai.windrunner | seat0 | eliminated | 20.3 | 0.926 | 0.000 | 2800/2775 |
| flat/2750/skai.windrunner/water.spoutling | seat0 | eliminated | 8.9 | 0.804 | 0.000 | 2775/2750 |
| flat/2750/water.spoutling/skai.windrunner | seat1 | eliminated | 9.0 | 0.000 | 0.795 | 2750/2775 |
| flat/2750/skai.windrunner/skai_pressure_mix | seat0 | eliminated | 19.2 | 0.210 | 0.000 | 2775/2750 |
| flat/2750/skai_pressure_mix/skai.windrunner | seat1 | eliminated | 21.0 | 0.000 | 0.174 | 2750/2775 |
| flat/2750/skai.windrunner/skai_expand_mix | seat0 | eliminated | 17.2 | 0.366 | 0.000 | 2775/2600 |
| flat/2750/skai_expand_mix/skai.windrunner | seat1 | eliminated | 16.5 | 0.000 | 0.377 | 2600/2775 |
| flat/2750/skai.windrunner/skai_air_mix | seat1 | eliminated | 11.1 | 0.000 | 0.594 | 2775/2820 |
| flat/2750/skai_air_mix/skai.windrunner | seat0 | eliminated | 11.6 | 0.578 | 0.000 | 2820/2775 |
| flat/2750/skai.windrunner/water_pressure_mix | seat1 | eliminated | 13.3 | 0.000 | 0.571 | 2775/2700 |
| flat/2750/water_pressure_mix/skai.windrunner | seat0 | eliminated | 12.8 | 0.570 | 0.000 | 2700/2775 |
| flat/2750/skai.windrunner/water_expand_mix | seat1 | eliminated | 15.6 | 0.000 | 0.823 | 2775/2595 |
| flat/2750/water_expand_mix/skai.windrunner | seat0 | eliminated | 17.6 | 0.761 | 0.000 | 2595/2775 |
| flat/2750/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 7.6 | 0.000 | 0.975 | 2775/2470 |
| flat/2750/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 8.3 | 0.928 | 0.000 | 2470/2775 |
| flat/2750/water.grasper/water.ink_lobber | seat1 | eliminated | 11.1 | 0.000 | 0.566 | 2800/2660 |
| flat/2750/water.ink_lobber/water.grasper | seat0 | eliminated | 11.6 | 0.494 | 0.000 | 2660/2800 |
| flat/2750/water.grasper/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 1.000 | 2800/2660 |
| flat/2750/water.leviathan/water.grasper | seat0 | eliminated | 4.2 | 1.000 | 0.000 | 2660/2800 |
| flat/2750/water.grasper/water.reef_runner | seat0 | eliminated | 12.3 | 0.608 | 0.000 | 2800/2775 |
| flat/2750/water.reef_runner/water.grasper | seat1 | eliminated | 12.0 | 0.000 | 0.619 | 2775/2800 |
| flat/2750/water.grasper/water.shellback | seat0 | eliminated | 22.2 | 0.571 | 0.000 | 2800/2800 |
| flat/2750/water.shellback/water.grasper | seat1 | eliminated | 21.6 | 0.000 | 0.588 | 2800/2800 |
| flat/2750/water.grasper/water.spoutling | seat0 | eliminated | 10.1 | 0.808 | 0.000 | 2800/2750 |
| flat/2750/water.spoutling/water.grasper | seat1 | eliminated | 9.8 | 0.000 | 0.820 | 2750/2800 |
| flat/2750/water.grasper/skai_pressure_mix | seat0 | eliminated | 12.3 | 0.633 | 0.000 | 2800/2750 |
| flat/2750/skai_pressure_mix/water.grasper | seat1 | eliminated | 12.3 | 0.000 | 0.630 | 2750/2800 |
| flat/2750/water.grasper/skai_expand_mix | seat1 | eliminated | 78.4 | 0.000 | 0.308 | 2800/2600 |
| flat/2750/skai_expand_mix/water.grasper | seat0 | eliminated | 77.3 | 0.308 | 0.000 | 2600/2800 |
| flat/2750/water.grasper/skai_air_mix | seat1 | eliminated | 14.1 | 0.000 | 0.920 | 2800/2820 |
| flat/2750/skai_air_mix/water.grasper | seat0 | eliminated | 14.3 | 0.920 | 0.000 | 2820/2800 |
| flat/2750/water.grasper/water_pressure_mix | seat0 | eliminated | 23.5 | 0.244 | 0.000 | 2800/2700 |
| flat/2750/water_pressure_mix/water.grasper | seat1 | eliminated | 23.5 | 0.000 | 0.259 | 2700/2800 |
| flat/2750/water.grasper/water_expand_mix | seat0 | eliminated | 18.4 | 0.229 | 0.000 | 2800/2595 |
| flat/2750/water_expand_mix/water.grasper | seat1 | eliminated | 19.2 | 0.000 | 0.289 | 2595/2800 |
| flat/2750/water.grasper/water_leviathan_mix | seat1 | eliminated | 10.0 | 0.000 | 0.745 | 2800/2470 |
| flat/2750/water_leviathan_mix/water.grasper | seat0 | eliminated | 11.0 | 0.755 | 0.000 | 2470/2800 |
| flat/2750/water.ink_lobber/water.leviathan | seat1 | eliminated | 5.2 | 0.000 | 0.887 | 2660/2660 |
| flat/2750/water.leviathan/water.ink_lobber | seat0 | eliminated | 5.1 | 0.936 | 0.000 | 2660/2660 |
| flat/2750/water.ink_lobber/water.reef_runner | seat0 | eliminated | 8.5 | 0.609 | 0.000 | 2660/2775 |
| flat/2750/water.reef_runner/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.527 | 2775/2660 |
| flat/2750/water.ink_lobber/water.shellback | seat1 | eliminated | 16.9 | 0.000 | 0.907 | 2660/2800 |
| flat/2750/water.shellback/water.ink_lobber | seat0 | eliminated | 16.9 | 0.938 | 0.000 | 2800/2660 |
| flat/2750/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.8 | 0.875 | 0.000 | 2660/2750 |
| flat/2750/water.spoutling/water.ink_lobber | seat1 | eliminated | 5.7 | 0.000 | 0.889 | 2750/2660 |
| flat/2750/water.ink_lobber/skai_pressure_mix | seat0 | eliminated | 10.9 | 0.594 | 0.000 | 2660/2750 |
| flat/2750/skai_pressure_mix/water.ink_lobber | seat1 | eliminated | 14.0 | 0.000 | 0.431 | 2750/2660 |
| flat/2750/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 29.5 | 0.000 | 0.308 | 2660/2600 |
| flat/2750/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 24.9 | 0.308 | 0.000 | 2600/2660 |
| flat/2750/water.ink_lobber/skai_air_mix | seat1 | eliminated | 8.9 | 0.000 | 0.920 | 2660/2820 |
| flat/2750/skai_air_mix/water.ink_lobber | seat0 | eliminated | 8.1 | 0.920 | 0.000 | 2820/2660 |
| flat/2750/water.ink_lobber/water_pressure_mix | seat0 | eliminated | 11.5 | 0.481 | 0.000 | 2660/2700 |
| flat/2750/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 11.5 | 0.000 | 0.600 | 2700/2660 |
| flat/2750/water.ink_lobber/water_expand_mix | seat1 | eliminated | 16.1 | 0.000 | 0.554 | 2660/2595 |
| flat/2750/water_expand_mix/water.ink_lobber | seat0 | eliminated | 16.1 | 0.564 | 0.000 | 2595/2660 |
| flat/2750/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 8.7 | 0.000 | 0.878 | 2660/2470 |
| flat/2750/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 9.3 | 0.868 | 0.000 | 2470/2660 |
| flat/2750/water.leviathan/water.reef_runner | seat0 | eliminated | 4.0 | 0.998 | 0.000 | 2660/2775 |
| flat/2750/water.reef_runner/water.leviathan | seat1 | eliminated | 3.4 | 0.000 | 1.000 | 2775/2660 |
| flat/2750/water.leviathan/water.shellback | seat1 | eliminated | 44.8 | 0.000 | 0.162 | 2660/2800 |
| flat/2750/water.shellback/water.leviathan | seat1 | eliminated | 26.6 | 0.000 | 0.434 | 2800/2660 |
| flat/2750/water.leviathan/water.spoutling | seat0 | eliminated | 3.1 | 1.000 | 0.000 | 2660/2750 |
| flat/2750/water.spoutling/water.leviathan | seat1 | eliminated | 2.9 | 0.000 | 1.000 | 2750/2660 |
| flat/2750/water.leviathan/skai_pressure_mix | seat0 | eliminated | 4.0 | 0.982 | 0.000 | 2660/2750 |
| flat/2750/skai_pressure_mix/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.982 | 2750/2660 |
| flat/2750/water.leviathan/skai_expand_mix | seat0 | eliminated | 7.0 | 0.928 | 0.000 | 2660/2600 |
| flat/2750/skai_expand_mix/water.leviathan | seat1 | eliminated | 7.0 | 0.000 | 0.889 | 2600/2660 |
| flat/2750/water.leviathan/skai_air_mix | seat0 | eliminated | 4.3 | 0.899 | 0.000 | 2660/2820 |
| flat/2750/skai_air_mix/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.970 | 2820/2660 |
| flat/2750/water.leviathan/water_pressure_mix | seat0 | eliminated | 4.1 | 1.000 | 0.000 | 2660/2700 |
| flat/2750/water_pressure_mix/water.leviathan | seat1 | eliminated | 3.3 | 0.000 | 1.000 | 2700/2660 |
| flat/2750/water.leviathan/water_expand_mix | seat0 | eliminated | 10.7 | 0.841 | 0.000 | 2660/2595 |
| flat/2750/water_expand_mix/water.leviathan | seat1 | eliminated | 10.3 | 0.000 | 0.841 | 2595/2660 |
| flat/2750/water.leviathan/water_leviathan_mix | seat0 | eliminated | 13.2 | 0.379 | 0.000 | 2660/2470 |
| flat/2750/water_leviathan_mix/water.leviathan | seat1 | eliminated | 14.9 | 0.000 | 0.250 | 2470/2660 |
| flat/2750/water.reef_runner/water.shellback | seat1 | eliminated | 20.5 | 0.000 | 0.930 | 2775/2800 |
| flat/2750/water.shellback/water.reef_runner | seat0 | eliminated | 20.5 | 0.935 | 0.000 | 2800/2775 |
| flat/2750/water.reef_runner/water.spoutling | seat0 | eliminated | 9.6 | 0.782 | 0.000 | 2775/2750 |
| flat/2750/water.spoutling/water.reef_runner | seat1 | eliminated | 9.3 | 0.000 | 0.784 | 2750/2775 |
| flat/2750/water.reef_runner/skai_pressure_mix | seat0 | eliminated | 18.5 | 0.258 | 0.000 | 2775/2750 |
| flat/2750/skai_pressure_mix/water.reef_runner | seat1 | eliminated | 21.4 | 0.000 | 0.195 | 2750/2775 |
| flat/2750/water.reef_runner/skai_expand_mix | seat1 | eliminated | 41.1 | 0.000 | 0.308 | 2775/2600 |
| flat/2750/skai_expand_mix/water.reef_runner | seat0 | eliminated | 39.8 | 0.308 | 0.000 | 2600/2775 |
| flat/2750/water.reef_runner/skai_air_mix | seat1 | eliminated | 10.0 | 0.000 | 0.920 | 2775/2820 |
| flat/2750/skai_air_mix/water.reef_runner | seat0 | eliminated | 11.5 | 0.920 | 0.000 | 2820/2775 |
| flat/2750/water.reef_runner/water_pressure_mix | seat1 | eliminated | 13.1 | 0.000 | 0.585 | 2775/2700 |
| flat/2750/water_pressure_mix/water.reef_runner | seat0 | eliminated | 13.1 | 0.581 | 0.000 | 2700/2775 |
| flat/2750/water.reef_runner/water_expand_mix | seat1 | eliminated | 15.4 | 0.000 | 0.813 | 2775/2595 |
| flat/2750/water_expand_mix/water.reef_runner | seat0 | eliminated | 16.4 | 0.791 | 0.000 | 2595/2775 |
| flat/2750/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 8.3 | 0.000 | 0.961 | 2775/2470 |
| flat/2750/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 9.1 | 0.944 | 0.000 | 2470/2775 |
| flat/2750/water.shellback/water.spoutling | seat0 | eliminated | 16.4 | 0.991 | 0.000 | 2800/2750 |
| flat/2750/water.spoutling/water.shellback | seat1 | eliminated | 16.7 | 0.000 | 0.991 | 2750/2800 |
| flat/2750/water.shellback/skai_pressure_mix | seat0 | eliminated | 20.4 | 0.934 | 0.000 | 2800/2750 |
| flat/2750/skai_pressure_mix/water.shellback | seat1 | eliminated | 20.6 | 0.000 | 0.932 | 2750/2800 |
| flat/2750/water.shellback/skai_expand_mix | stalemate | dome_absorbed | 46.6 | 0.961 | 0.308 | 2800/2600 |
| flat/2750/skai_expand_mix/water.shellback | stalemate | dome_absorbed | 46.4 | 0.308 | 0.944 | 2600/2800 |
| flat/2750/water.shellback/skai_air_mix | seat1 | eliminated | 199.7 | 0.000 | 0.920 | 2800/2820 |
| flat/2750/skai_air_mix/water.shellback | stalemate | dome_absorbed | 39.5 | 0.920 | 0.967 | 2820/2800 |
| flat/2750/water.shellback/water_pressure_mix | seat1 | eliminated | 29.0 | 0.000 | 0.458 | 2800/2700 |
| flat/2750/water_pressure_mix/water.shellback | seat0 | eliminated | 29.3 | 0.440 | 0.000 | 2700/2800 |
| flat/2750/water.shellback/water_expand_mix | seat0 | eliminated | 77.6 | 0.456 | 0.000 | 2800/2595 |
| flat/2750/water_expand_mix/water.shellback | seat1 | eliminated | 50.0 | 0.000 | 0.754 | 2595/2800 |
| flat/2750/water.shellback/water_leviathan_mix | seat0 | eliminated | 31.6 | 0.911 | 0.000 | 2800/2470 |
| flat/2750/water_leviathan_mix/water.shellback | seat1 | eliminated | 30.8 | 0.000 | 0.879 | 2470/2800 |
| flat/2750/water.spoutling/skai_pressure_mix | seat1 | eliminated | 9.5 | 0.000 | 0.777 | 2750/2750 |
| flat/2750/skai_pressure_mix/water.spoutling | seat0 | eliminated | 9.2 | 0.795 | 0.000 | 2750/2750 |
| flat/2750/water.spoutling/skai_expand_mix | seat1 | eliminated | 11.3 | 0.000 | 0.733 | 2750/2600 |
| flat/2750/skai_expand_mix/water.spoutling | seat0 | eliminated | 11.4 | 0.726 | 0.000 | 2600/2750 |
| flat/2750/water.spoutling/skai_air_mix | seat1 | eliminated | 7.9 | 0.000 | 0.834 | 2750/2820 |
| flat/2750/skai_air_mix/water.spoutling | seat0 | eliminated | 7.7 | 0.840 | 0.000 | 2820/2750 |
| flat/2750/water.spoutling/water_pressure_mix | seat1 | eliminated | 9.9 | 0.000 | 0.808 | 2750/2700 |
| flat/2750/water_pressure_mix/water.spoutling | seat0 | eliminated | 10.0 | 0.798 | 0.000 | 2700/2750 |
| flat/2750/water.spoutling/water_expand_mix | seat1 | eliminated | 12.2 | 0.000 | 0.958 | 2750/2595 |
| flat/2750/water_expand_mix/water.spoutling | seat0 | eliminated | 12.6 | 0.919 | 0.000 | 2595/2750 |
| flat/2750/water.spoutling/water_leviathan_mix | seat1 | eliminated | 5.7 | 0.000 | 1.000 | 2750/2470 |
| flat/2750/water_leviathan_mix/water.spoutling | seat0 | eliminated | 5.4 | 1.000 | 0.000 | 2470/2750 |
| flat/2750/skai_pressure_mix/skai_expand_mix | seat0 | eliminated | 18.9 | 0.320 | 0.000 | 2750/2600 |
| flat/2750/skai_expand_mix/skai_pressure_mix | seat1 | eliminated | 20.4 | 0.000 | 0.282 | 2600/2750 |
| flat/2750/skai_pressure_mix/skai_air_mix | seat1 | eliminated | 12.4 | 0.000 | 0.579 | 2750/2820 |
| flat/2750/skai_air_mix/skai_pressure_mix | seat0 | eliminated | 12.6 | 0.589 | 0.000 | 2820/2750 |
| flat/2750/skai_pressure_mix/water_pressure_mix | seat1 | eliminated | 13.3 | 0.000 | 0.591 | 2750/2700 |
| flat/2750/water_pressure_mix/skai_pressure_mix | seat0 | eliminated | 13.1 | 0.604 | 0.000 | 2700/2750 |
| flat/2750/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 15.2 | 0.000 | 0.874 | 2750/2595 |
| flat/2750/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 18.7 | 0.772 | 0.000 | 2595/2750 |
| flat/2750/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 9.3 | 0.000 | 0.963 | 2750/2470 |
| flat/2750/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 11.0 | 0.915 | 0.000 | 2470/2750 |
| flat/2750/skai_expand_mix/skai_air_mix | seat1 | eliminated | 14.4 | 0.000 | 0.545 | 2600/2820 |
| flat/2750/skai_air_mix/skai_expand_mix | seat0 | eliminated | 14.3 | 0.518 | 0.000 | 2820/2600 |
| flat/2750/skai_expand_mix/water_pressure_mix | seat0 | eliminated | 72.7 | 0.308 | 0.000 | 2600/2700 |
| flat/2750/water_pressure_mix/skai_expand_mix | seat1 | eliminated | 69.2 | 0.000 | 0.308 | 2700/2600 |
| flat/2750/skai_expand_mix/water_expand_mix | seat1 | eliminated | 18.0 | 0.000 | 0.855 | 2600/2595 |
| flat/2750/water_expand_mix/skai_expand_mix | seat0 | eliminated | 19.6 | 0.840 | 0.000 | 2595/2600 |
| flat/2750/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 10.7 | 0.000 | 0.851 | 2600/2470 |
| flat/2750/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 10.4 | 0.919 | 0.000 | 2470/2600 |
| flat/2750/skai_air_mix/water_pressure_mix | seat0 | eliminated | 14.1 | 0.920 | 0.000 | 2820/2700 |
| flat/2750/water_pressure_mix/skai_air_mix | seat1 | eliminated | 14.2 | 0.000 | 0.920 | 2700/2820 |
| flat/2750/skai_air_mix/water_expand_mix | seat1 | eliminated | 29.3 | 0.000 | 0.335 | 2820/2595 |
| flat/2750/water_expand_mix/skai_air_mix | seat0 | eliminated | 34.1 | 0.361 | 0.000 | 2595/2820 |
| flat/2750/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 10.6 | 0.000 | 0.580 | 2820/2470 |
| flat/2750/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 13.5 | 0.491 | 0.000 | 2470/2820 |
| flat/2750/water_pressure_mix/water_expand_mix | seat0 | eliminated | 34.8 | 0.046 | 0.000 | 2700/2595 |
| flat/2750/water_expand_mix/water_pressure_mix | seat1 | eliminated | 21.4 | 0.000 | 0.187 | 2595/2700 |
| flat/2750/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 9.9 | 0.000 | 0.828 | 2700/2470 |
| flat/2750/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 10.5 | 0.821 | 0.000 | 2470/2700 |
| flat/2750/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 17.8 | 0.000 | 0.771 | 2595/2470 |
| flat/2750/water_leviathan_mix/water_expand_mix | seat0 | eliminated | 19.9 | 0.691 | 0.000 | 2470/2595 |
| high_ground/1750/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.7 | 1.000 | 0.000 | 1800/1710 |
| high_ground/1750/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 12.8 | 0.000 | 1.000 | 1710/1800 |
| high_ground/1750/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 38.7 | 0.000 | 0.111 | 1800/1800 |
| high_ground/1750/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 25.9 | 0.337 | 0.000 | 1800/1800 |
| high_ground/1750/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 19.2 | 1.000 | 0.000 | 1800/1855 |
| high_ground/1750/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 20.3 | 0.000 | 1.000 | 1855/1800 |
| high_ground/1750/skai.roc_rider/skai.windrunner | seat0 | eliminated | 17.0 | 0.856 | 0.000 | 1800/1725 |
| high_ground/1750/skai.windrunner/skai.roc_rider | seat0 | eliminated | 20.7 | 0.371 | 0.000 | 1725/1800 |
| high_ground/1750/skai.roc_rider/water.grasper | seat0 | eliminated | 28.8 | 1.000 | 0.000 | 1800/1800 |
| high_ground/1750/water.grasper/skai.roc_rider | seat1 | eliminated | 30.9 | 0.000 | 1.000 | 1800/1800 |
| high_ground/1750/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 11.3 | 1.000 | 0.000 | 1800/1710 |
| high_ground/1750/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 15.1 | 0.000 | 1.000 | 1710/1800 |
| high_ground/1750/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.943 | 1800/1900 |
| high_ground/1750/water.leviathan/skai.roc_rider | seat0 | eliminated | 14.1 | 0.767 | 0.000 | 1900/1800 |
| high_ground/1750/skai.roc_rider/water.reef_runner | seat0 | eliminated | 17.0 | 1.000 | 0.000 | 1800/1725 |
| high_ground/1750/water.reef_runner/skai.roc_rider | seat1 | eliminated | 18.2 | 0.000 | 1.000 | 1725/1800 |
| high_ground/1750/skai.roc_rider/water.shellback | seat0 | eliminated | 110.5 | 1.000 | 0.000 | 1800/1750 |
| high_ground/1750/water.shellback/skai.roc_rider | seat1 | eliminated | 144.0 | 0.000 | 1.000 | 1750/1800 |
| high_ground/1750/skai.roc_rider/water.spoutling | seat0 | eliminated | 13.5 | 0.740 | 0.000 | 1800/1750 |
| high_ground/1750/water.spoutling/skai.roc_rider | seat1 | eliminated | 13.5 | 0.000 | 0.820 | 1750/1800 |
| high_ground/1750/skai.roc_rider/skai_pressure_mix | seat0 | eliminated | 17.4 | 0.715 | 0.000 | 1800/1650 |
| high_ground/1750/skai_pressure_mix/skai.roc_rider | seat0 | eliminated | 30.2 | 0.185 | 0.000 | 1650/1800 |
| high_ground/1750/skai.roc_rider/skai_expand_mix | seat0 | eliminated | 31.6 | 0.208 | 0.000 | 1800/1950 |
| high_ground/1750/skai_expand_mix/skai.roc_rider | seat0 | eliminated | 19.4 | 0.547 | 0.000 | 1950/1800 |
| high_ground/1750/skai.roc_rider/skai_air_mix | seat0 | eliminated | 22.0 | 0.591 | 0.000 | 1800/1880 |
| high_ground/1750/skai_air_mix/skai.roc_rider | seat0 | eliminated | 38.9 | 0.180 | 0.000 | 1880/1800 |
| high_ground/1750/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 30.6 | 1.000 | 0.000 | 1800/2025 |
| high_ground/1750/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 32.4 | 0.000 | 1.000 | 2025/1800 |
| high_ground/1750/skai.roc_rider/water_expand_mix | seat0 | eliminated | 40.3 | 0.875 | 0.000 | 1800/1730 |
| high_ground/1750/water_expand_mix/skai.roc_rider | seat0 | eliminated | 42.8 | 0.620 | 0.000 | 1730/1800 |
| high_ground/1750/skai.roc_rider/water_leviathan_mix | seat1 | eliminated | 22.4 | 0.000 | 0.432 | 1800/1235 |
| high_ground/1750/water_leviathan_mix/skai.roc_rider | seat1 | eliminated | 28.1 | 0.000 | 0.278 | 1235/1800 |
| high_ground/1750/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 11.1 | 0.000 | 0.677 | 1710/1800 |
| high_ground/1750/skai.talon_archer/skai.stormcaller | seat1 | eliminated | 23.5 | 0.000 | 0.033 | 1800/1710 |
| high_ground/1750/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 8.2 | 0.000 | 1.000 | 1710/1855 |
| high_ground/1750/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 7.8 | 1.000 | 0.000 | 1855/1710 |
| high_ground/1750/skai.stormcaller/skai.windrunner | seat0 | eliminated | 35.6 | 0.422 | 0.000 | 1710/1725 |
| high_ground/1750/skai.windrunner/skai.stormcaller | seat0 | eliminated | 14.4 | 0.118 | 0.000 | 1725/1710 |
| high_ground/1750/skai.stormcaller/water.grasper | seat0 | eliminated | 17.9 | 1.000 | 0.000 | 1710/1800 |
| high_ground/1750/water.grasper/skai.stormcaller | seat0 | eliminated | 13.6 | 0.280 | 0.000 | 1800/1710 |
| high_ground/1750/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.6 | 0.000 | 0.701 | 1710/1710 |
| high_ground/1750/water.ink_lobber/skai.stormcaller | seat1 | eliminated | 12.1 | 0.000 | 0.678 | 1710/1710 |
| high_ground/1750/skai.stormcaller/water.leviathan | seat1 | eliminated | 5.5 | 0.000 | 1.000 | 1710/1900 |
| high_ground/1750/water.leviathan/skai.stormcaller | seat0 | eliminated | 16.0 | 0.491 | 0.000 | 1900/1710 |
| high_ground/1750/skai.stormcaller/water.reef_runner | seat0 | eliminated | 35.6 | 0.422 | 0.000 | 1710/1725 |
| high_ground/1750/water.reef_runner/skai.stormcaller | seat1 | eliminated | 14.7 | 0.000 | 0.257 | 1725/1710 |
| high_ground/1750/skai.stormcaller/water.shellback | seat0 | eliminated | 162.9 | 1.000 | 0.000 | 1710/1750 |
| high_ground/1750/water.shellback/skai.stormcaller | seat0 | eliminated | 19.9 | 1.000 | 0.000 | 1750/1710 |
| high_ground/1750/skai.stormcaller/water.spoutling | seat0 | eliminated | 20.7 | 0.352 | 0.000 | 1710/1750 |
| high_ground/1750/water.spoutling/skai.stormcaller | seat1 | eliminated | 10.4 | 0.000 | 0.877 | 1750/1710 |
| high_ground/1750/skai.stormcaller/skai_pressure_mix | seat1 | eliminated | 16.2 | 0.000 | 0.412 | 1710/1650 |
| high_ground/1750/skai_pressure_mix/skai.stormcaller | seat1 | eliminated | 17.4 | 0.000 | 0.284 | 1650/1710 |
| high_ground/1750/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 11.5 | 0.000 | 0.756 | 1710/1950 |
| high_ground/1750/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 12.6 | 0.460 | 0.000 | 1950/1710 |
| high_ground/1750/skai.stormcaller/skai_air_mix | seat1 | eliminated | 10.7 | 0.000 | 0.920 | 1710/1880 |
| high_ground/1750/skai_air_mix/skai.stormcaller | seat0 | eliminated | 9.6 | 0.920 | 0.000 | 1880/1710 |
| high_ground/1750/skai.stormcaller/water_pressure_mix | seat0 | eliminated | 22.9 | 1.000 | 0.000 | 1710/2025 |
| high_ground/1750/water_pressure_mix/skai.stormcaller | seat0 | eliminated | 13.8 | 0.277 | 0.000 | 2025/1710 |
| high_ground/1750/skai.stormcaller/water_expand_mix | seat1 | eliminated | 22.0 | 0.000 | 0.779 | 1710/1730 |
| high_ground/1750/water_expand_mix/skai.stormcaller | seat0 | eliminated | 18.0 | 0.807 | 0.000 | 1730/1710 |
| high_ground/1750/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 11.7 | 0.000 | 1.000 | 1710/1235 |
| high_ground/1750/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 28.6 | 0.139 | 0.000 | 1235/1710 |
| high_ground/1750/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 10.8 | 0.000 | 0.744 | 1800/1855 |
| high_ground/1750/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 11.0 | 0.577 | 0.000 | 1855/1800 |
| high_ground/1750/skai.talon_archer/skai.windrunner | seat0 | eliminated | 21.2 | 0.674 | 0.000 | 1800/1725 |
| high_ground/1750/skai.windrunner/skai.talon_archer | seat1 | eliminated | 19.0 | 0.000 | 0.368 | 1725/1800 |
| high_ground/1750/skai.talon_archer/water.grasper | seat0 | eliminated | 28.2 | 1.000 | 0.000 | 1800/1800 |
| high_ground/1750/water.grasper/skai.talon_archer | seat0 | eliminated | 18.2 | 0.514 | 0.000 | 1800/1800 |
| high_ground/1750/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 12.7 | 0.000 | 0.531 | 1800/1710 |
| high_ground/1750/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 12.5 | 0.000 | 0.692 | 1710/1800 |
| high_ground/1750/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.9 | 0.000 | 1.000 | 1800/1900 |
| high_ground/1750/water.leviathan/skai.talon_archer | seat0 | eliminated | 14.1 | 0.684 | 0.000 | 1900/1800 |
| high_ground/1750/skai.talon_archer/water.reef_runner | seat0 | eliminated | 21.2 | 0.674 | 0.000 | 1800/1725 |
| high_ground/1750/water.reef_runner/skai.talon_archer | seat1 | eliminated | 17.9 | 0.000 | 0.432 | 1725/1800 |
| high_ground/1750/skai.talon_archer/water.shellback | seat0 | eliminated | 116.2 | 1.000 | 0.000 | 1800/1750 |
| high_ground/1750/water.shellback/skai.talon_archer | seat0 | eliminated | 27.7 | 0.995 | 0.000 | 1750/1800 |
| high_ground/1750/skai.talon_archer/water.spoutling | seat0 | eliminated | 15.2 | 0.710 | 0.000 | 1800/1750 |
| high_ground/1750/water.spoutling/skai.talon_archer | seat1 | eliminated | 10.7 | 0.000 | 0.899 | 1750/1800 |
| high_ground/1750/skai.talon_archer/skai_pressure_mix | seat0 | eliminated | 22.8 | 0.451 | 0.000 | 1800/1650 |
| high_ground/1750/skai_pressure_mix/skai.talon_archer | seat1 | eliminated | 17.8 | 0.000 | 0.482 | 1650/1800 |
| high_ground/1750/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 21.7 | 0.000 | 0.404 | 1800/1950 |
| high_ground/1750/skai_expand_mix/skai.talon_archer | seat1 | eliminated | 30.6 | 0.000 | 0.142 | 1950/1800 |
| high_ground/1750/skai.talon_archer/skai_air_mix | seat1 | eliminated | 21.4 | 0.000 | 0.354 | 1800/1880 |
| high_ground/1750/skai_air_mix/skai.talon_archer | seat0 | eliminated | 20.5 | 0.336 | 0.000 | 1880/1800 |
| high_ground/1750/skai.talon_archer/water_pressure_mix | seat0 | eliminated | 30.2 | 0.944 | 0.000 | 1800/2025 |
| high_ground/1750/water_pressure_mix/skai.talon_archer | seat0 | eliminated | 15.3 | 0.645 | 0.000 | 2025/1800 |
| high_ground/1750/skai.talon_archer/water_expand_mix | seat1 | eliminated | 45.1 | 0.000 | 0.549 | 1800/1730 |
| high_ground/1750/water_expand_mix/skai.talon_archer | seat0 | eliminated | 30.2 | 0.617 | 0.000 | 1730/1800 |
| high_ground/1750/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 17.1 | 0.000 | 0.508 | 1800/1235 |
| high_ground/1750/water_leviathan_mix/skai.talon_archer | seat1 | eliminated | 18.3 | 0.000 | 0.391 | 1235/1800 |
| high_ground/1750/skai.thunderhead/skai.windrunner | seat0 | eliminated | 9.6 | 0.882 | 0.000 | 1855/1725 |
| high_ground/1750/skai.windrunner/skai.thunderhead | seat1 | eliminated | 7.6 | 0.000 | 0.834 | 1725/1855 |
| high_ground/1750/skai.thunderhead/water.grasper | seat0 | eliminated | 8.0 | 1.000 | 0.000 | 1855/1800 |
| high_ground/1750/water.grasper/skai.thunderhead | seat1 | eliminated | 10.7 | 0.000 | 1.000 | 1800/1855 |
| high_ground/1750/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 7.8 | 1.000 | 0.000 | 1855/1710 |
| high_ground/1750/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 10.6 | 0.000 | 1.000 | 1710/1855 |
| high_ground/1750/skai.thunderhead/water.leviathan | seat1 | eliminated | 7.2 | 0.000 | 0.644 | 1855/1900 |
| high_ground/1750/water.leviathan/skai.thunderhead | seat1 | eliminated | 22.4 | 0.000 | 0.113 | 1900/1855 |
| high_ground/1750/skai.thunderhead/water.reef_runner | seat0 | eliminated | 9.6 | 1.000 | 0.000 | 1855/1725 |
| high_ground/1750/water.reef_runner/skai.thunderhead | seat1 | eliminated | 7.6 | 0.000 | 1.000 | 1725/1855 |
| high_ground/1750/skai.thunderhead/water.shellback | seat0 | eliminated | 35.4 | 1.000 | 0.000 | 1855/1750 |
| high_ground/1750/water.shellback/skai.thunderhead | seat1 | eliminated | 33.3 | 0.000 | 1.000 | 1750/1855 |
| high_ground/1750/skai.thunderhead/water.spoutling | seat0 | eliminated | 8.0 | 0.872 | 0.000 | 1855/1750 |
| high_ground/1750/water.spoutling/skai.thunderhead | seat1 | eliminated | 8.5 | 0.000 | 0.952 | 1750/1855 |
| high_ground/1750/skai.thunderhead/skai_pressure_mix | seat0 | eliminated | 9.6 | 0.827 | 0.000 | 1855/1650 |
| high_ground/1750/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 8.3 | 0.000 | 0.840 | 1650/1855 |
| high_ground/1750/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 43.6 | 0.000 | 0.308 | 1855/1950 |
| high_ground/1750/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 22.1 | 0.334 | 0.000 | 1950/1855 |
| high_ground/1750/skai.thunderhead/skai_air_mix | seat1 | eliminated | 29.7 | 0.000 | 0.920 | 1855/1880 |
| high_ground/1750/skai_air_mix/skai.thunderhead | seat0 | eliminated | 27.7 | 0.920 | 0.000 | 1880/1855 |
| high_ground/1750/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 9.6 | 1.000 | 0.000 | 1855/2025 |
| high_ground/1750/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 10.6 | 0.000 | 1.000 | 2025/1855 |
| high_ground/1750/skai.thunderhead/water_expand_mix | seat0 | eliminated | 14.6 | 0.977 | 0.000 | 1855/1730 |
| high_ground/1750/water_expand_mix/skai.thunderhead | seat1 | eliminated | 19.1 | 0.000 | 0.640 | 1730/1855 |
| high_ground/1750/skai.thunderhead/water_leviathan_mix | seat0 | eliminated | 12.9 | 0.410 | 0.000 | 1855/1235 |
| high_ground/1750/water_leviathan_mix/skai.thunderhead | seat1 | eliminated | 18.7 | 0.000 | 0.594 | 1235/1855 |
| high_ground/1750/skai.windrunner/water.grasper | seat0 | eliminated | 21.6 | 1.000 | 0.000 | 1725/1800 |
| high_ground/1750/water.grasper/skai.windrunner | seat0 | eliminated | 14.9 | 0.591 | 0.000 | 1800/1725 |
| high_ground/1750/skai.windrunner/water.ink_lobber | seat1 | eliminated | 11.6 | 0.000 | 0.505 | 1725/1710 |
| high_ground/1750/water.ink_lobber/skai.windrunner | seat0 | eliminated | 33.8 | 0.444 | 0.000 | 1710/1725 |
| high_ground/1750/skai.windrunner/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.992 | 1725/1900 |
| high_ground/1750/water.leviathan/skai.windrunner | seat0 | eliminated | 14.1 | 0.823 | 0.000 | 1900/1725 |
| high_ground/1750/skai.windrunner/water.reef_runner | seat0 | eliminated | 20.2 | 0.178 | 0.000 | 1725/1725 |
| high_ground/1750/water.reef_runner/skai.windrunner | seat0 | eliminated | 24.9 | 0.109 | 0.000 | 1725/1725 |
| high_ground/1750/skai.windrunner/water.shellback | seat0 | eliminated | 95.9 | 0.819 | 0.000 | 1725/1750 |
| high_ground/1750/water.shellback/skai.windrunner | seat0 | eliminated | 26.0 | 0.985 | 0.000 | 1750/1725 |
| high_ground/1750/skai.windrunner/water.spoutling | seat0 | eliminated | 11.4 | 0.728 | 0.000 | 1725/1750 |
| high_ground/1750/water.spoutling/skai.windrunner | seat1 | eliminated | 18.0 | 0.000 | 0.753 | 1750/1725 |
| high_ground/1750/skai.windrunner/skai_pressure_mix | seat0 | eliminated | 24.2 | 0.119 | 0.000 | 1725/1650 |
| high_ground/1750/skai_pressure_mix/skai.windrunner | seat0 | eliminated | 26.2 | 0.182 | 0.000 | 1650/1725 |
| high_ground/1750/skai.windrunner/skai_expand_mix | seat1 | eliminated | 18.6 | 0.000 | 0.409 | 1725/1950 |
| high_ground/1750/skai_expand_mix/skai.windrunner | seat0 | eliminated | 16.2 | 0.769 | 0.000 | 1950/1725 |
| high_ground/1750/skai.windrunner/skai_air_mix | seat1 | eliminated | 21.9 | 0.000 | 0.357 | 1725/1880 |
| high_ground/1750/skai_air_mix/skai.windrunner | seat0 | eliminated | 12.6 | 0.860 | 0.000 | 1880/1725 |
| high_ground/1750/skai.windrunner/water_pressure_mix | seat0 | eliminated | 24.8 | 0.696 | 0.000 | 1725/2025 |
| high_ground/1750/water_pressure_mix/skai.windrunner | seat0 | eliminated | 15.0 | 0.625 | 0.000 | 2025/1725 |
| high_ground/1750/skai.windrunner/water_expand_mix | seat1 | eliminated | 32.4 | 0.000 | 0.737 | 1725/1730 |
| high_ground/1750/water_expand_mix/skai.windrunner | seat0 | eliminated | 26.2 | 0.618 | 0.000 | 1730/1725 |
| high_ground/1750/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 11.2 | 0.000 | 0.849 | 1725/1235 |
| high_ground/1750/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 20.2 | 0.505 | 0.000 | 1235/1725 |
| high_ground/1750/water.grasper/water.ink_lobber | seat1 | eliminated | 18.3 | 0.000 | 0.267 | 1800/1710 |
| high_ground/1750/water.ink_lobber/water.grasper | seat0 | eliminated | 18.8 | 1.000 | 0.000 | 1710/1800 |
| high_ground/1750/water.grasper/water.leviathan | seat1 | eliminated | 7.7 | 0.000 | 0.994 | 1800/1900 |
| high_ground/1750/water.leviathan/water.grasper | seat0 | eliminated | 14.1 | 1.000 | 0.000 | 1900/1800 |
| high_ground/1750/water.grasper/water.reef_runner | seat0 | eliminated | 15.4 | 0.586 | 0.000 | 1800/1725 |
| high_ground/1750/water.reef_runner/water.grasper | seat0 | eliminated | 22.3 | 1.000 | 0.000 | 1725/1800 |
| high_ground/1750/water.grasper/water.shellback | seat0 | eliminated | 23.5 | 0.646 | 0.000 | 1800/1750 |
| high_ground/1750/water.shellback/water.grasper | seat0 | eliminated | 47.6 | 0.567 | 0.000 | 1750/1800 |
| high_ground/1750/water.grasper/water.spoutling | seat0 | eliminated | 12.3 | 0.795 | 0.000 | 1800/1750 |
| high_ground/1750/water.spoutling/water.grasper | seat0 | eliminated | 41.7 | 1.000 | 0.000 | 1750/1800 |
| high_ground/1750/water.grasper/skai_pressure_mix | seat0 | eliminated | 15.5 | 0.626 | 0.000 | 1800/1650 |
| high_ground/1750/skai_pressure_mix/water.grasper | seat0 | eliminated | 23.2 | 1.000 | 0.000 | 1650/1800 |
| high_ground/1750/water.grasper/skai_expand_mix | seat1 | eliminated | 58.8 | 0.000 | 0.308 | 1800/1950 |
| high_ground/1750/skai_expand_mix/water.grasper | seat0 | eliminated | 24.3 | 1.000 | 0.000 | 1950/1800 |
| high_ground/1750/water.grasper/skai_air_mix | seat1 | eliminated | 23.8 | 0.000 | 0.920 | 1800/1880 |
| high_ground/1750/skai_air_mix/water.grasper | seat0 | eliminated | 16.2 | 1.000 | 0.000 | 1880/1800 |
| high_ground/1750/water.grasper/water_pressure_mix | seat1 | eliminated | 41.2 | 0.000 | 0.112 | 1800/2025 |
| high_ground/1750/water_pressure_mix/water.grasper | seat0 | eliminated | 22.5 | 0.472 | 0.000 | 2025/1800 |
| high_ground/1750/water.grasper/water_expand_mix | seat0 | eliminated | 22.2 | 0.287 | 0.000 | 1800/1730 |
| high_ground/1750/water_expand_mix/water.grasper | seat0 | eliminated | 27.6 | 0.769 | 0.000 | 1730/1800 |
| high_ground/1750/water.grasper/water_leviathan_mix | seat1 | eliminated | 22.8 | 0.000 | 0.117 | 1800/1235 |
| high_ground/1750/water_leviathan_mix/water.grasper | seat0 | eliminated | 18.0 | 0.994 | 0.000 | 1235/1800 |
| high_ground/1750/water.ink_lobber/water.leviathan | seat1 | eliminated | 5.9 | 0.000 | 1.000 | 1710/1900 |
| high_ground/1750/water.leviathan/water.ink_lobber | seat0 | eliminated | 22.0 | 0.310 | 0.000 | 1900/1710 |
| high_ground/1750/water.ink_lobber/water.reef_runner | seat0 | eliminated | 33.8 | 0.444 | 0.000 | 1710/1725 |
| high_ground/1750/water.reef_runner/water.ink_lobber | seat1 | eliminated | 11.2 | 0.000 | 0.578 | 1725/1710 |
| high_ground/1750/water.ink_lobber/water.shellback | seat0 | eliminated | 66.8 | 1.000 | 0.000 | 1710/1750 |
| high_ground/1750/water.shellback/water.ink_lobber | seat0 | eliminated | 20.6 | 1.000 | 0.000 | 1750/1710 |
| high_ground/1750/water.ink_lobber/water.spoutling | seat0 | eliminated | 27.8 | 0.256 | 0.000 | 1710/1750 |
| high_ground/1750/water.spoutling/water.ink_lobber | seat1 | eliminated | 9.2 | 0.000 | 0.915 | 1750/1710 |
| high_ground/1750/water.ink_lobber/skai_pressure_mix | seat1 | eliminated | 19.4 | 0.000 | 0.382 | 1710/1650 |
| high_ground/1750/skai_pressure_mix/water.ink_lobber | seat1 | eliminated | 14.6 | 0.000 | 0.410 | 1650/1710 |
| high_ground/1750/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 13.5 | 0.000 | 0.696 | 1710/1950 |
| high_ground/1750/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 15.7 | 0.387 | 0.000 | 1950/1710 |
| high_ground/1750/water.ink_lobber/skai_air_mix | seat1 | eliminated | 13.0 | 0.000 | 0.920 | 1710/1880 |
| high_ground/1750/skai_air_mix/water.ink_lobber | seat0 | eliminated | 10.3 | 0.920 | 0.000 | 1880/1710 |
| high_ground/1750/water.ink_lobber/water_pressure_mix | seat0 | eliminated | 24.8 | 1.000 | 0.000 | 1710/2025 |
| high_ground/1750/water_pressure_mix/water.ink_lobber | seat0 | eliminated | 17.6 | 0.146 | 0.000 | 2025/1710 |
| high_ground/1750/water.ink_lobber/water_expand_mix | seat1 | eliminated | 49.5 | 0.000 | 0.324 | 1710/1730 |
| high_ground/1750/water_expand_mix/water.ink_lobber | seat0 | eliminated | 21.9 | 0.542 | 0.000 | 1730/1710 |
| high_ground/1750/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 13.4 | 0.000 | 1.000 | 1710/1235 |
| high_ground/1750/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 25.6 | 0.169 | 0.000 | 1235/1710 |
| high_ground/1750/water.leviathan/water.reef_runner | seat0 | eliminated | 14.1 | 0.823 | 0.000 | 1900/1725 |
| high_ground/1750/water.reef_runner/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1725/1900 |
| high_ground/1750/water.leviathan/water.shellback | seat0 | eliminated | 38.1 | 0.845 | 0.000 | 1900/1750 |
| high_ground/1750/water.shellback/water.leviathan | seat1 | eliminated | 24.4 | 0.000 | 0.499 | 1750/1900 |
| high_ground/1750/water.leviathan/water.spoutling | seat0 | eliminated | 14.1 | 0.837 | 0.000 | 1900/1750 |
| high_ground/1750/water.spoutling/water.leviathan | seat1 | eliminated | 5.0 | 0.000 | 1.000 | 1750/1900 |
| high_ground/1750/water.leviathan/skai_pressure_mix | seat0 | eliminated | 11.1 | 0.806 | 0.000 | 1900/1650 |
| high_ground/1750/skai_pressure_mix/water.leviathan | seat1 | eliminated | 7.5 | 0.000 | 0.971 | 1650/1900 |
| high_ground/1750/water.leviathan/skai_expand_mix | seat0 | eliminated | 14.1 | 0.687 | 0.000 | 1900/1950 |
| high_ground/1750/skai_expand_mix/water.leviathan | seat1 | eliminated | 7.5 | 0.000 | 0.984 | 1950/1900 |
| high_ground/1750/water.leviathan/skai_air_mix | seat0 | eliminated | 18.9 | 0.485 | 0.000 | 1900/1880 |
| high_ground/1750/skai_air_mix/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.843 | 1880/1900 |
| high_ground/1750/water.leviathan/water_pressure_mix | seat0 | eliminated | 14.1 | 0.997 | 0.000 | 1900/2025 |
| high_ground/1750/water_pressure_mix/water.leviathan | seat1 | eliminated | 5.5 | 0.000 | 1.000 | 2025/1900 |
| high_ground/1750/water.leviathan/water_expand_mix | seat0 | eliminated | 20.1 | 0.666 | 0.000 | 1900/1730 |
| high_ground/1750/water_expand_mix/water.leviathan | seat1 | eliminated | 11.5 | 0.000 | 0.887 | 1730/1900 |
| high_ground/1750/water.leviathan/water_leviathan_mix | seat1 | eliminated | 27.0 | 0.000 | 0.327 | 1900/1235 |
| high_ground/1750/water_leviathan_mix/water.leviathan | seat1 | eliminated | 8.5 | 0.000 | 1.000 | 1235/1900 |
| high_ground/1750/water.reef_runner/water.shellback | seat0 | eliminated | 94.4 | 0.826 | 0.000 | 1725/1750 |
| high_ground/1750/water.shellback/water.reef_runner | seat0 | eliminated | 25.7 | 0.965 | 0.000 | 1750/1725 |
| high_ground/1750/water.reef_runner/water.spoutling | seat0 | eliminated | 12.4 | 0.685 | 0.000 | 1725/1750 |
| high_ground/1750/water.spoutling/water.reef_runner | seat1 | eliminated | 18.0 | 0.000 | 0.753 | 1750/1725 |
| high_ground/1750/water.reef_runner/skai_pressure_mix | seat1 | eliminated | 25.4 | 0.000 | 0.198 | 1725/1650 |
| high_ground/1750/skai_pressure_mix/water.reef_runner | seat0 | eliminated | 26.2 | 0.182 | 0.000 | 1650/1725 |
| high_ground/1750/water.reef_runner/skai_expand_mix | seat1 | eliminated | 17.1 | 0.000 | 0.526 | 1725/1950 |
| high_ground/1750/skai_expand_mix/water.reef_runner | seat0 | eliminated | 16.2 | 0.769 | 0.000 | 1950/1725 |
| high_ground/1750/water.reef_runner/skai_air_mix | seat1 | eliminated | 13.4 | 0.000 | 0.920 | 1725/1880 |
| high_ground/1750/skai_air_mix/water.reef_runner | seat0 | eliminated | 12.7 | 0.920 | 0.000 | 1880/1725 |
| high_ground/1750/water.reef_runner/water_pressure_mix | seat0 | eliminated | 25.8 | 0.722 | 0.000 | 1725/2025 |
| high_ground/1750/water_pressure_mix/water.reef_runner | seat0 | eliminated | 14.8 | 0.633 | 0.000 | 2025/1725 |
| high_ground/1750/water.reef_runner/water_expand_mix | seat1 | eliminated | 32.4 | 0.000 | 0.729 | 1725/1730 |
| high_ground/1750/water_expand_mix/water.reef_runner | seat0 | eliminated | 26.2 | 0.618 | 0.000 | 1730/1725 |
| high_ground/1750/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 10.8 | 0.000 | 0.957 | 1725/1235 |
| high_ground/1750/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 20.2 | 0.505 | 0.000 | 1235/1725 |
| high_ground/1750/water.shellback/water.spoutling | seat0 | eliminated | 20.7 | 1.000 | 0.000 | 1750/1750 |
| high_ground/1750/water.spoutling/water.shellback | stalemate | dome_absorbed | 30.0 | 1.000 | 1.000 | 1750/1750 |
| high_ground/1750/water.shellback/skai_pressure_mix | seat0 | eliminated | 25.4 | 0.990 | 0.000 | 1750/1650 |
| high_ground/1750/skai_pressure_mix/water.shellback | seat0 | eliminated | 93.8 | 0.909 | 0.000 | 1650/1750 |
| high_ground/1750/water.shellback/skai_expand_mix | seat1 | eliminated | 128.5 | 0.000 | 0.308 | 1750/1950 |
| high_ground/1750/skai_expand_mix/water.shellback | seat0 | eliminated | 73.5 | 1.000 | 0.000 | 1950/1750 |
| high_ground/1750/water.shellback/skai_air_mix | seat1 | eliminated | 68.0 | 0.000 | 0.920 | 1750/1880 |
| high_ground/1750/skai_air_mix/water.shellback | seat0 | eliminated | 71.3 | 1.000 | 0.000 | 1880/1750 |
| high_ground/1750/water.shellback/water_pressure_mix | seat0 | eliminated | 51.3 | 0.584 | 0.000 | 1750/2025 |
| high_ground/1750/water_pressure_mix/water.shellback | seat0 | eliminated | 24.9 | 0.652 | 0.000 | 2025/1750 |
| high_ground/1750/water.shellback/water_expand_mix | seat0 | eliminated | 45.5 | 0.761 | 0.000 | 1750/1730 |
| high_ground/1750/water_expand_mix/water.shellback | seat0 | eliminated | 71.2 | 0.854 | 0.000 | 1730/1750 |
| high_ground/1750/water.shellback/water_leviathan_mix | seat0 | eliminated | 27.6 | 1.000 | 0.000 | 1750/1235 |
| high_ground/1750/water_leviathan_mix/water.shellback | stalemate | dome_absorbed | 150.9 | 0.615 | 0.929 | 1235/1750 |
| high_ground/1750/water.spoutling/skai_pressure_mix | seat1 | eliminated | 14.8 | 0.000 | 0.785 | 1750/1650 |
| high_ground/1750/skai_pressure_mix/water.spoutling | seat0 | eliminated | 12.3 | 0.674 | 0.000 | 1650/1750 |
| high_ground/1750/water.spoutling/skai_expand_mix | seat1 | eliminated | 11.9 | 0.000 | 0.872 | 1750/1950 |
| high_ground/1750/skai_expand_mix/water.spoutling | seat0 | eliminated | 12.2 | 0.753 | 0.000 | 1950/1750 |
| high_ground/1750/water.spoutling/skai_air_mix | seat1 | eliminated | 12.0 | 0.000 | 0.849 | 1750/1880 |
| high_ground/1750/skai_air_mix/water.spoutling | seat0 | eliminated | 10.4 | 0.795 | 0.000 | 1880/1750 |
| high_ground/1750/water.spoutling/water_pressure_mix | seat0 | eliminated | 45.6 | 0.922 | 0.000 | 1750/2025 |
| high_ground/1750/water_pressure_mix/water.spoutling | seat0 | eliminated | 12.0 | 0.825 | 0.000 | 2025/1750 |
| high_ground/1750/water.spoutling/water_expand_mix | seat1 | eliminated | 27.3 | 0.000 | 0.983 | 1750/1730 |
| high_ground/1750/water_expand_mix/water.spoutling | seat0 | eliminated | 18.6 | 0.879 | 0.000 | 1730/1750 |
| high_ground/1750/water.spoutling/water_leviathan_mix | seat1 | eliminated | 11.9 | 0.000 | 1.000 | 1750/1235 |
| high_ground/1750/water_leviathan_mix/water.spoutling | seat0 | eliminated | 16.5 | 0.751 | 0.000 | 1235/1750 |
| high_ground/1750/skai_pressure_mix/skai_expand_mix | seat1 | eliminated | 15.9 | 0.000 | 0.558 | 1650/1950 |
| high_ground/1750/skai_expand_mix/skai_pressure_mix | seat0 | eliminated | 17.2 | 0.679 | 0.000 | 1950/1650 |
| high_ground/1750/skai_pressure_mix/skai_air_mix | seat1 | eliminated | 18.3 | 0.000 | 0.498 | 1650/1880 |
| high_ground/1750/skai_air_mix/skai_pressure_mix | seat0 | eliminated | 12.6 | 0.764 | 0.000 | 1880/1650 |
| high_ground/1750/skai_pressure_mix/water_pressure_mix | seat0 | eliminated | 26.5 | 0.750 | 0.000 | 1650/2025 |
| high_ground/1750/water_pressure_mix/skai_pressure_mix | seat0 | eliminated | 14.5 | 0.684 | 0.000 | 2025/1650 |
| high_ground/1750/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 35.3 | 0.000 | 0.864 | 1650/1730 |
| high_ground/1750/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 25.2 | 0.667 | 0.000 | 1730/1650 |
| high_ground/1750/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 14.3 | 0.000 | 0.740 | 1650/1235 |
| high_ground/1750/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 25.4 | 0.400 | 0.000 | 1235/1650 |
| high_ground/1750/skai_expand_mix/skai_air_mix | seat0 | eliminated | 19.3 | 0.526 | 0.000 | 1950/1880 |
| high_ground/1750/skai_air_mix/skai_expand_mix | seat0 | eliminated | 23.3 | 0.309 | 0.000 | 1880/1950 |
| high_ground/1750/skai_expand_mix/water_pressure_mix | seat0 | eliminated | 26.1 | 0.969 | 0.000 | 1950/2025 |
| high_ground/1750/water_pressure_mix/skai_expand_mix | seat1 | eliminated | 66.9 | 0.000 | 0.308 | 2025/1950 |
| high_ground/1750/skai_expand_mix/water_expand_mix | seat0 | eliminated | 86.6 | 0.186 | 0.000 | 1950/1730 |
| high_ground/1750/water_expand_mix/skai_expand_mix | seat0 | eliminated | 32.9 | 0.583 | 0.000 | 1730/1950 |
| high_ground/1750/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 13.5 | 0.000 | 0.519 | 1950/1235 |
| high_ground/1750/water_leviathan_mix/skai_expand_mix | seat1 | eliminated | 22.8 | 0.000 | 0.196 | 1235/1950 |
| high_ground/1750/skai_air_mix/water_pressure_mix | seat0 | eliminated | 17.9 | 0.980 | 0.000 | 1880/2025 |
| high_ground/1750/water_pressure_mix/skai_air_mix | seat1 | eliminated | 21.3 | 0.000 | 0.920 | 2025/1880 |
| high_ground/1750/skai_air_mix/water_expand_mix | seat0 | eliminated | 25.9 | 0.801 | 0.000 | 1880/1730 |
| high_ground/1750/water_expand_mix/skai_air_mix | seat0 | eliminated | 44.2 | 0.532 | 0.000 | 1730/1880 |
| high_ground/1750/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 22.5 | 0.000 | 0.251 | 1880/1235 |
| high_ground/1750/water_leviathan_mix/skai_air_mix | seat1 | eliminated | 22.4 | 0.000 | 0.385 | 1235/1880 |
| high_ground/1750/water_pressure_mix/water_expand_mix | seat0 | eliminated | 20.7 | 0.346 | 0.000 | 2025/1730 |
| high_ground/1750/water_expand_mix/water_pressure_mix | seat0 | eliminated | 29.2 | 0.708 | 0.000 | 1730/2025 |
| high_ground/1750/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 22.9 | 0.000 | 0.221 | 2025/1235 |
| high_ground/1750/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 20.0 | 0.994 | 0.000 | 1235/2025 |
| high_ground/1750/water_expand_mix/water_leviathan_mix | seat0 | eliminated | 29.6 | 0.509 | 0.000 | 1730/1235 |
| high_ground/1750/water_leviathan_mix/water_expand_mix | seat1 | eliminated | 44.1 | 0.000 | 0.746 | 1235/1730 |
| flooded/2250/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 24.1 | 1.000 | 0.000 | 2200/2280 |
| flooded/2250/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.9 | 0.000 | 1.000 | 2280/2200 |
| flooded/2250/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 16.2 | 0.000 | 1.000 | 2200/2300 |
| flooded/2250/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 20.2 | 0.000 | 0.844 | 2300/2200 |
| flooded/2250/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 30.7 | 1.000 | 0.000 | 2200/2120 |
| flooded/2250/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 21.1 | 0.000 | 1.000 | 2120/2200 |
| flooded/2250/skai.roc_rider/skai.windrunner | seat1 | eliminated | 13.7 | 0.000 | 1.000 | 2200/2250 |
| flooded/2250/skai.windrunner/skai.roc_rider | seat1 | eliminated | 24.6 | 0.000 | 0.575 | 2250/2200 |
| flooded/2250/skai.roc_rider/water.grasper | seat0 | eliminated | 44.7 | 1.000 | 0.000 | 2200/2300 |
| flooded/2250/water.grasper/skai.roc_rider | seat1 | eliminated | 30.3 | 0.000 | 1.000 | 2300/2200 |
| flooded/2250/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 25.0 | 1.000 | 0.000 | 2200/2280 |
| flooded/2250/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 12.1 | 0.000 | 1.000 | 2280/2200 |
| flooded/2250/skai.roc_rider/water.leviathan | seat1 | eliminated | 9.8 | 0.000 | 1.000 | 2200/2280 |
| flooded/2250/water.leviathan/skai.roc_rider | seat0 | eliminated | 9.1 | 0.708 | 0.000 | 2280/2200 |
| flooded/2250/skai.roc_rider/water.reef_runner | seat1 | eliminated | 13.5 | 0.000 | 1.000 | 2200/2250 |
| flooded/2250/water.reef_runner/skai.roc_rider | seat1 | eliminated | 23.1 | 0.000 | 0.653 | 2250/2200 |
| flooded/2250/skai.roc_rider/water.shellback | seat1 | eliminated | 27.0 | 0.000 | 0.998 | 2200/2275 |
| flooded/2250/water.shellback/skai.roc_rider | seat0 | eliminated | 25.1 | 0.939 | 0.000 | 2275/2200 |
| flooded/2250/skai.roc_rider/water.spoutling | seat1 | eliminated | 23.1 | 0.000 | 0.883 | 2200/2250 |
| flooded/2250/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.5 | 0.000 | 1.000 | 2250/2200 |
| flooded/2250/skai.roc_rider/skai_pressure_mix | seat1 | eliminated | 13.9 | 0.000 | 1.000 | 2200/2200 |
| flooded/2250/skai_pressure_mix/skai.roc_rider | seat1 | eliminated | 22.0 | 0.000 | 0.688 | 2200/2200 |
| flooded/2250/skai.roc_rider/skai_expand_mix | seat1 | eliminated | 18.2 | 0.000 | 1.000 | 2200/1950 |
| flooded/2250/skai_expand_mix/skai.roc_rider | seat1 | eliminated | 17.2 | 0.000 | 0.987 | 1950/2200 |
| flooded/2250/skai.roc_rider/skai_air_mix | seat1 | eliminated | 30.8 | 0.000 | 0.701 | 2200/1880 |
| flooded/2250/skai_air_mix/skai.roc_rider | seat1 | eliminated | 17.3 | 0.000 | 1.000 | 1880/2200 |
| flooded/2250/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 44.8 | 0.606 | 0.000 | 2200/2025 |
| flooded/2250/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 26.3 | 0.000 | 0.946 | 2025/2200 |
| flooded/2250/skai.roc_rider/water_expand_mix | seat1 | eliminated | 21.9 | 0.000 | 1.000 | 2200/2595 |
| flooded/2250/water_expand_mix/skai.roc_rider | seat0 | eliminated | 42.1 | 0.713 | 0.000 | 2595/2200 |
| flooded/2250/skai.roc_rider/water_leviathan_mix | seat1 | eliminated | 15.9 | 0.000 | 1.000 | 2200/2470 |
| flooded/2250/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 21.1 | 0.754 | 0.000 | 2470/2200 |
| flooded/2250/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 9.8 | 0.000 | 0.816 | 2280/2300 |
| flooded/2250/skai.talon_archer/skai.stormcaller | seat1 | eliminated | 13.9 | 0.000 | 0.956 | 2300/2280 |
| flooded/2250/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 9.4 | 0.000 | 1.000 | 2280/2120 |
| flooded/2250/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 10.2 | 1.000 | 0.000 | 2120/2280 |
| flooded/2250/skai.stormcaller/skai.windrunner | seat1 | eliminated | 8.5 | 0.000 | 0.885 | 2280/2250 |
| flooded/2250/skai.windrunner/skai.stormcaller | seat1 | eliminated | 18.9 | 0.000 | 0.702 | 2250/2280 |
| flooded/2250/skai.stormcaller/water.grasper | seat1 | eliminated | 9.2 | 0.000 | 0.917 | 2280/2300 |
| flooded/2250/water.grasper/skai.stormcaller | seat0 | eliminated | 11.6 | 0.171 | 0.000 | 2300/2280 |
| flooded/2250/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 10.3 | 0.000 | 0.777 | 2280/2280 |
| flooded/2250/water.ink_lobber/skai.stormcaller | seat1 | eliminated | 14.8 | 0.000 | 0.350 | 2280/2280 |
| flooded/2250/skai.stormcaller/water.leviathan | seat1 | eliminated | 7.9 | 0.000 | 0.989 | 2280/2280 |
| flooded/2250/water.leviathan/skai.stormcaller | seat0 | eliminated | 9.1 | 0.677 | 0.000 | 2280/2280 |
| flooded/2250/skai.stormcaller/water.reef_runner | seat1 | eliminated | 7.8 | 0.000 | 0.940 | 2280/2250 |
| flooded/2250/water.reef_runner/skai.stormcaller | seat1 | eliminated | 18.7 | 0.000 | 0.700 | 2250/2280 |
| flooded/2250/skai.stormcaller/water.shellback | seat1 | eliminated | 16.7 | 0.000 | 1.000 | 2280/2275 |
| flooded/2250/water.shellback/skai.stormcaller | seat0 | eliminated | 14.2 | 1.000 | 0.000 | 2275/2280 |
| flooded/2250/skai.stormcaller/water.spoutling | seat1 | eliminated | 18.8 | 0.000 | 0.345 | 2280/2250 |
| flooded/2250/water.spoutling/skai.stormcaller | seat1 | eliminated | 10.1 | 0.000 | 1.000 | 2250/2280 |
| flooded/2250/skai.stormcaller/skai_pressure_mix | seat1 | eliminated | 8.9 | 0.000 | 0.904 | 2280/2200 |
| flooded/2250/skai_pressure_mix/skai.stormcaller | seat1 | eliminated | 17.9 | 0.000 | 0.855 | 2200/2280 |
| flooded/2250/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 10.9 | 0.000 | 0.869 | 2280/1950 |
| flooded/2250/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 50.8 | 0.308 | 0.000 | 1950/2280 |
| flooded/2250/skai.stormcaller/skai_air_mix | seat1 | eliminated | 11.9 | 0.000 | 0.920 | 2280/1880 |
| flooded/2250/skai_air_mix/skai.stormcaller | seat0 | eliminated | 20.1 | 0.920 | 0.000 | 1880/2280 |
| flooded/2250/skai.stormcaller/water_pressure_mix | seat1 | eliminated | 9.3 | 0.000 | 0.932 | 2280/2025 |
| flooded/2250/water_pressure_mix/skai.stormcaller | seat1 | eliminated | 16.7 | 0.000 | 0.411 | 2025/2280 |
| flooded/2250/skai.stormcaller/water_expand_mix | seat1 | eliminated | 11.3 | 0.000 | 1.000 | 2280/2595 |
| flooded/2250/water_expand_mix/skai.stormcaller | seat0 | eliminated | 21.6 | 0.677 | 0.000 | 2595/2280 |
| flooded/2250/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 10.6 | 0.000 | 1.000 | 2280/2470 |
| flooded/2250/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 15.1 | 0.720 | 0.000 | 2470/2280 |
| flooded/2250/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 10.8 | 0.000 | 1.000 | 2300/2120 |
| flooded/2250/skai.thunderhead/skai.talon_archer | seat1 | eliminated | 26.6 | 0.000 | 0.012 | 2120/2300 |
| flooded/2250/skai.talon_archer/skai.windrunner | seat1 | eliminated | 13.0 | 0.000 | 1.000 | 2300/2250 |
| flooded/2250/skai.windrunner/skai.talon_archer | seat1 | eliminated | 17.6 | 0.000 | 0.787 | 2250/2300 |
| flooded/2250/skai.talon_archer/water.grasper | seat1 | eliminated | 13.5 | 0.000 | 0.970 | 2300/2300 |
| flooded/2250/water.grasper/skai.talon_archer | seat0 | eliminated | 13.1 | 0.646 | 0.000 | 2300/2300 |
| flooded/2250/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 12.9 | 0.000 | 1.000 | 2300/2280 |
| flooded/2250/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 11.8 | 0.000 | 0.339 | 2280/2300 |
| flooded/2250/skai.talon_archer/water.leviathan | seat1 | eliminated | 9.9 | 0.000 | 1.000 | 2300/2280 |
| flooded/2250/water.leviathan/skai.talon_archer | seat0 | eliminated | 12.1 | 0.638 | 0.000 | 2280/2300 |
| flooded/2250/skai.talon_archer/water.reef_runner | seat1 | eliminated | 12.7 | 0.000 | 1.000 | 2300/2250 |
| flooded/2250/water.reef_runner/skai.talon_archer | seat1 | eliminated | 19.3 | 0.000 | 0.751 | 2250/2300 |
| flooded/2250/skai.talon_archer/water.shellback | seat1 | eliminated | 24.8 | 0.000 | 0.987 | 2300/2275 |
| flooded/2250/water.shellback/skai.talon_archer | seat0 | eliminated | 23.0 | 0.917 | 0.000 | 2275/2300 |
| flooded/2250/skai.talon_archer/water.spoutling | seat0 | eliminated | 31.1 | 0.232 | 0.000 | 2300/2250 |
| flooded/2250/water.spoutling/skai.talon_archer | seat1 | eliminated | 11.2 | 0.000 | 1.000 | 2250/2300 |
| flooded/2250/skai.talon_archer/skai_pressure_mix | seat1 | eliminated | 13.8 | 0.000 | 0.982 | 2300/2200 |
| flooded/2250/skai_pressure_mix/skai.talon_archer | seat1 | eliminated | 16.4 | 0.000 | 0.868 | 2200/2300 |
| flooded/2250/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 17.2 | 0.000 | 0.877 | 2300/1950 |
| flooded/2250/skai_expand_mix/skai.talon_archer | seat1 | eliminated | 14.2 | 0.000 | 0.994 | 1950/2300 |
| flooded/2250/skai.talon_archer/skai_air_mix | seat1 | eliminated | 17.7 | 0.000 | 0.850 | 2300/1880 |
| flooded/2250/skai_air_mix/skai.talon_archer | seat1 | eliminated | 14.2 | 0.000 | 1.000 | 1880/2300 |
| flooded/2250/skai.talon_archer/water_pressure_mix | seat1 | eliminated | 13.6 | 0.000 | 0.978 | 2300/2025 |
| flooded/2250/water_pressure_mix/skai.talon_archer | seat0 | eliminated | 17.3 | 0.475 | 0.000 | 2025/2300 |
| flooded/2250/skai.talon_archer/water_expand_mix | seat1 | eliminated | 16.1 | 0.000 | 0.997 | 2300/2595 |
| flooded/2250/water_expand_mix/skai.talon_archer | seat0 | eliminated | 30.2 | 0.768 | 0.000 | 2595/2300 |
| flooded/2250/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 11.9 | 0.000 | 1.000 | 2300/2470 |
| flooded/2250/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 18.1 | 0.704 | 0.000 | 2470/2300 |
| flooded/2250/skai.thunderhead/skai.windrunner | seat1 | eliminated | 17.6 | 0.000 | 0.067 | 2120/2250 |
| flooded/2250/skai.windrunner/skai.thunderhead | seat1 | eliminated | 9.9 | 0.000 | 0.909 | 2250/2120 |
| flooded/2250/skai.thunderhead/water.grasper | seat0 | eliminated | 13.5 | 1.000 | 0.000 | 2120/2300 |
| flooded/2250/water.grasper/skai.thunderhead | seat1 | eliminated | 8.8 | 0.000 | 1.000 | 2300/2120 |
| flooded/2250/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 10.6 | 1.000 | 0.000 | 2120/2280 |
| flooded/2250/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 1.000 | 2280/2120 |
| flooded/2250/skai.thunderhead/water.leviathan | seat1 | eliminated | 8.7 | 0.000 | 1.000 | 2120/2280 |
| flooded/2250/water.leviathan/skai.thunderhead | seat1 | eliminated | 17.2 | 0.000 | 0.092 | 2280/2120 |
| flooded/2250/skai.thunderhead/water.reef_runner | seat1 | eliminated | 13.9 | 0.000 | 0.382 | 2120/2250 |
| flooded/2250/water.reef_runner/skai.thunderhead | seat1 | eliminated | 13.8 | 0.000 | 0.905 | 2250/2120 |
| flooded/2250/skai.thunderhead/water.shellback | seat1 | eliminated | 24.9 | 0.000 | 0.937 | 2120/2275 |
| flooded/2250/water.shellback/skai.thunderhead | seat0 | eliminated | 22.1 | 0.922 | 0.000 | 2275/2120 |
| flooded/2250/skai.thunderhead/water.spoutling | seat0 | eliminated | 10.8 | 0.648 | 0.000 | 2120/2250 |
| flooded/2250/water.spoutling/skai.thunderhead | seat1 | eliminated | 9.5 | 0.000 | 0.997 | 2250/2120 |
| flooded/2250/skai.thunderhead/skai_pressure_mix | seat1 | eliminated | 22.4 | 0.000 | 0.090 | 2120/2200 |
| flooded/2250/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 12.6 | 0.000 | 0.931 | 2200/2120 |
| flooded/2250/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 43.6 | 0.000 | 0.308 | 2120/1950 |
| flooded/2250/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 79.6 | 0.308 | 0.000 | 1950/2120 |
| flooded/2250/skai.thunderhead/skai_air_mix | seat1 | eliminated | 32.9 | 0.000 | 0.920 | 2120/1880 |
| flooded/2250/skai_air_mix/skai.thunderhead | seat0 | eliminated | 46.4 | 0.920 | 0.000 | 1880/2120 |
| flooded/2250/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 13.0 | 0.882 | 0.000 | 2120/2025 |
| flooded/2250/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 0.982 | 2025/2120 |
| flooded/2250/skai.thunderhead/water_expand_mix | seat1 | eliminated | 20.9 | 0.000 | 0.841 | 2120/2595 |
| flooded/2250/water_expand_mix/skai.thunderhead | seat1 | eliminated | 19.9 | 0.000 | 0.757 | 2595/2120 |
| flooded/2250/skai.thunderhead/water_leviathan_mix | seat1 | eliminated | 16.9 | 0.000 | 0.762 | 2120/2470 |
| flooded/2250/water_leviathan_mix/skai.thunderhead | seat0 | eliminated | 21.1 | 0.400 | 0.000 | 2470/2120 |
| flooded/2250/skai.windrunner/water.grasper | seat1 | eliminated | 10.4 | 0.000 | 0.876 | 2250/2300 |
| flooded/2250/water.grasper/skai.windrunner | seat0 | eliminated | 10.5 | 0.653 | 0.000 | 2300/2250 |
| flooded/2250/skai.windrunner/water.ink_lobber | seat1 | eliminated | 14.5 | 0.000 | 0.922 | 2250/2280 |
| flooded/2250/water.ink_lobber/skai.windrunner | seat1 | eliminated | 9.6 | 0.000 | 0.501 | 2280/2250 |
| flooded/2250/skai.windrunner/water.leviathan | seat1 | eliminated | 9.1 | 0.000 | 1.000 | 2250/2280 |
| flooded/2250/water.leviathan/skai.windrunner | seat0 | eliminated | 12.1 | 0.583 | 0.000 | 2280/2250 |
| flooded/2250/skai.windrunner/water.reef_runner | seat1 | eliminated | 13.1 | 0.000 | 0.768 | 2250/2250 |
| flooded/2250/water.reef_runner/skai.windrunner | seat1 | eliminated | 14.4 | 0.000 | 0.747 | 2250/2250 |
| flooded/2250/skai.windrunner/water.shellback | seat1 | eliminated | 21.9 | 0.000 | 1.000 | 2250/2275 |
| flooded/2250/water.shellback/skai.windrunner | seat0 | eliminated | 19.3 | 0.911 | 0.000 | 2275/2250 |
| flooded/2250/skai.windrunner/water.spoutling | seat0 | eliminated | 19.2 | 0.575 | 0.000 | 2250/2250 |
| flooded/2250/water.spoutling/skai.windrunner | seat1 | eliminated | 9.7 | 0.000 | 1.000 | 2250/2250 |
| flooded/2250/skai.windrunner/skai_pressure_mix | seat1 | eliminated | 15.0 | 0.000 | 0.739 | 2250/2200 |
| flooded/2250/skai_pressure_mix/skai.windrunner | seat1 | eliminated | 12.9 | 0.000 | 0.828 | 2200/2250 |
| flooded/2250/skai.windrunner/skai_expand_mix | seat1 | eliminated | 20.6 | 0.000 | 0.667 | 2250/1950 |
| flooded/2250/skai_expand_mix/skai.windrunner | seat1 | eliminated | 11.5 | 0.000 | 0.983 | 1950/2250 |
| flooded/2250/skai.windrunner/skai_air_mix | seat1 | eliminated | 20.7 | 0.000 | 0.673 | 2250/1880 |
| flooded/2250/skai_air_mix/skai.windrunner | seat1 | eliminated | 12.0 | 0.000 | 0.982 | 1880/2250 |
| flooded/2250/skai.windrunner/water_pressure_mix | seat1 | eliminated | 12.2 | 0.000 | 0.836 | 2250/2025 |
| flooded/2250/water_pressure_mix/skai.windrunner | seat0 | eliminated | 14.4 | 0.501 | 0.000 | 2025/2250 |
| flooded/2250/skai.windrunner/water_expand_mix | seat1 | eliminated | 15.4 | 0.000 | 0.970 | 2250/2595 |
| flooded/2250/water_expand_mix/skai.windrunner | seat0 | eliminated | 23.8 | 0.754 | 0.000 | 2595/2250 |
| flooded/2250/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 13.6 | 0.000 | 1.000 | 2250/2470 |
| flooded/2250/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 13.3 | 0.686 | 0.000 | 2470/2250 |
| flooded/2250/water.grasper/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.462 | 2300/2280 |
| flooded/2250/water.ink_lobber/water.grasper | seat1 | eliminated | 10.6 | 0.000 | 0.510 | 2280/2300 |
| flooded/2250/water.grasper/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.982 | 2300/2280 |
| flooded/2250/water.leviathan/water.grasper | seat0 | eliminated | 12.1 | 0.547 | 0.000 | 2280/2300 |
| flooded/2250/water.grasper/water.reef_runner | seat0 | eliminated | 12.3 | 0.609 | 0.000 | 2300/2250 |
| flooded/2250/water.reef_runner/water.grasper | seat1 | eliminated | 11.0 | 0.000 | 0.879 | 2250/2300 |
| flooded/2250/water.grasper/water.shellback | seat0 | eliminated | 21.0 | 0.574 | 0.000 | 2300/2275 |
| flooded/2250/water.shellback/water.grasper | seat1 | eliminated | 19.9 | 0.000 | 0.727 | 2275/2300 |
| flooded/2250/water.grasper/water.spoutling | seat0 | eliminated | 8.1 | 0.840 | 0.000 | 2300/2250 |
| flooded/2250/water.spoutling/water.grasper | seat1 | eliminated | 10.0 | 0.000 | 1.000 | 2250/2300 |
| flooded/2250/water.grasper/skai_pressure_mix | seat0 | eliminated | 10.6 | 0.686 | 0.000 | 2300/2200 |
| flooded/2250/skai_pressure_mix/water.grasper | seat1 | eliminated | 10.5 | 0.000 | 0.916 | 2200/2300 |
| flooded/2250/water.grasper/skai_expand_mix | seat1 | eliminated | 90.1 | 0.000 | 0.308 | 2300/1950 |
| flooded/2250/skai_expand_mix/water.grasper | seat0 | eliminated | 136.1 | 0.308 | 0.000 | 1950/2300 |
| flooded/2250/water.grasper/skai_air_mix | seat1 | eliminated | 18.2 | 0.000 | 0.920 | 2300/1880 |
| flooded/2250/skai_air_mix/water.grasper | seat0 | eliminated | 33.9 | 0.920 | 0.000 | 1880/2300 |
| flooded/2250/water.grasper/water_pressure_mix | seat0 | eliminated | 19.7 | 0.391 | 0.000 | 2300/2025 |
| flooded/2250/water_pressure_mix/water.grasper | seat1 | eliminated | 19.4 | 0.000 | 0.552 | 2025/2300 |
| flooded/2250/water.grasper/water_expand_mix | seat1 | eliminated | 25.7 | 0.000 | 0.168 | 2300/2595 |
| flooded/2250/water_expand_mix/water.grasper | seat1 | eliminated | 15.9 | 0.000 | 0.632 | 2595/2300 |
| flooded/2250/water.grasper/water_leviathan_mix | seat1 | eliminated | 7.2 | 0.000 | 0.776 | 2300/2470 |
| flooded/2250/water_leviathan_mix/water.grasper | seat1 | eliminated | 17.4 | 0.000 | 0.247 | 2470/2300 |
| flooded/2250/water.ink_lobber/water.leviathan | seat1 | eliminated | 9.4 | 0.000 | 0.875 | 2280/2280 |
| flooded/2250/water.leviathan/water.ink_lobber | seat0 | eliminated | 12.1 | 0.592 | 0.000 | 2280/2280 |
| flooded/2250/water.ink_lobber/water.reef_runner | seat1 | eliminated | 9.0 | 0.000 | 0.706 | 2280/2250 |
| flooded/2250/water.reef_runner/water.ink_lobber | seat1 | eliminated | 14.4 | 0.000 | 0.900 | 2250/2280 |
| flooded/2250/water.ink_lobber/water.shellback | seat1 | eliminated | 17.1 | 0.000 | 0.993 | 2280/2275 |
| flooded/2250/water.shellback/water.ink_lobber | seat0 | eliminated | 16.8 | 0.875 | 0.000 | 2275/2280 |
| flooded/2250/water.ink_lobber/water.spoutling | seat0 | eliminated | 12.1 | 0.629 | 0.000 | 2280/2250 |
| flooded/2250/water.spoutling/water.ink_lobber | seat1 | eliminated | 10.1 | 0.000 | 1.000 | 2250/2280 |
| flooded/2250/water.ink_lobber/skai_pressure_mix | seat1 | eliminated | 10.6 | 0.000 | 0.391 | 2280/2200 |
| flooded/2250/skai_pressure_mix/water.ink_lobber | seat1 | eliminated | 16.5 | 0.000 | 0.968 | 2200/2280 |
| flooded/2250/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 13.8 | 0.000 | 0.549 | 2280/1950 |
| flooded/2250/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 57.7 | 0.308 | 0.000 | 1950/2280 |
| flooded/2250/water.ink_lobber/skai_air_mix | seat1 | eliminated | 12.9 | 0.000 | 0.920 | 2280/1880 |
| flooded/2250/skai_air_mix/water.ink_lobber | seat0 | eliminated | 23.2 | 0.920 | 0.000 | 1880/2280 |
| flooded/2250/water.ink_lobber/water_pressure_mix | seat1 | eliminated | 11.5 | 0.000 | 0.484 | 2280/2025 |
| flooded/2250/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 14.2 | 0.000 | 0.633 | 2025/2280 |
| flooded/2250/water.ink_lobber/water_expand_mix | seat1 | eliminated | 12.6 | 0.000 | 0.896 | 2280/2595 |
| flooded/2250/water_expand_mix/water.ink_lobber | seat1 | eliminated | 25.6 | 0.000 | 0.555 | 2595/2280 |
| flooded/2250/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 11.2 | 0.000 | 0.922 | 2280/2470 |
| flooded/2250/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 18.1 | 0.646 | 0.000 | 2470/2280 |
| flooded/2250/water.leviathan/water.reef_runner | seat0 | eliminated | 12.1 | 0.552 | 0.000 | 2280/2250 |
| flooded/2250/water.reef_runner/water.leviathan | seat1 | eliminated | 7.7 | 0.000 | 1.000 | 2250/2280 |
| flooded/2250/water.leviathan/water.shellback | seat1 | eliminated | 29.1 | 0.000 | 0.577 | 2280/2275 |
| flooded/2250/water.shellback/water.leviathan | seat1 | eliminated | 22.3 | 0.000 | 0.621 | 2275/2280 |
| flooded/2250/water.leviathan/water.spoutling | seat0 | eliminated | 9.1 | 0.794 | 0.000 | 2280/2250 |
| flooded/2250/water.spoutling/water.leviathan | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 2250/2280 |
| flooded/2250/water.leviathan/skai_pressure_mix | seat0 | eliminated | 12.1 | 0.571 | 0.000 | 2280/2200 |
| flooded/2250/skai_pressure_mix/water.leviathan | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 2200/2280 |
| flooded/2250/water.leviathan/skai_expand_mix | seat0 | eliminated | 9.1 | 0.717 | 0.000 | 2280/1950 |
| flooded/2250/skai_expand_mix/water.leviathan | seat1 | eliminated | 7.2 | 0.000 | 1.000 | 1950/2280 |
| flooded/2250/water.leviathan/skai_air_mix | seat0 | eliminated | 9.1 | 0.633 | 0.000 | 2280/1880 |
| flooded/2250/skai_air_mix/water.leviathan | seat1 | eliminated | 7.3 | 0.000 | 1.000 | 1880/2280 |
| flooded/2250/water.leviathan/water_pressure_mix | seat0 | eliminated | 12.1 | 0.648 | 0.000 | 2280/2025 |
| flooded/2250/water_pressure_mix/water.leviathan | seat1 | eliminated | 6.9 | 0.000 | 0.998 | 2025/2280 |
| flooded/2250/water.leviathan/water_expand_mix | seat1 | eliminated | 17.9 | 0.000 | 0.578 | 2280/2595 |
| flooded/2250/water_expand_mix/water.leviathan | seat1 | eliminated | 15.5 | 0.000 | 0.932 | 2595/2280 |
| flooded/2250/water.leviathan/water_leviathan_mix | seat1 | eliminated | 15.3 | 0.000 | 0.769 | 2280/2470 |
| flooded/2250/water_leviathan_mix/water.leviathan | seat1 | eliminated | 19.7 | 0.000 | 0.404 | 2470/2280 |
| flooded/2250/water.reef_runner/water.shellback | seat1 | eliminated | 22.6 | 0.000 | 0.993 | 2250/2275 |
| flooded/2250/water.shellback/water.reef_runner | seat0 | eliminated | 21.6 | 0.870 | 0.000 | 2275/2250 |
| flooded/2250/water.reef_runner/water.spoutling | seat0 | eliminated | 17.9 | 0.619 | 0.000 | 2250/2250 |
| flooded/2250/water.spoutling/water.reef_runner | seat1 | eliminated | 9.1 | 0.000 | 1.000 | 2250/2250 |
| flooded/2250/water.reef_runner/skai_pressure_mix | seat1 | eliminated | 16.4 | 0.000 | 0.710 | 2250/2200 |
| flooded/2250/skai_pressure_mix/water.reef_runner | seat1 | eliminated | 11.9 | 0.000 | 0.863 | 2200/2250 |
| flooded/2250/water.reef_runner/skai_expand_mix | seat1 | eliminated | 21.0 | 0.000 | 0.644 | 2250/1950 |
| flooded/2250/skai_expand_mix/water.reef_runner | seat1 | eliminated | 11.3 | 0.000 | 0.994 | 1950/2250 |
| flooded/2250/water.reef_runner/skai_air_mix | seat1 | eliminated | 15.9 | 0.000 | 0.724 | 2250/1880 |
| flooded/2250/skai_air_mix/water.reef_runner | seat1 | eliminated | 12.0 | 0.000 | 0.995 | 1880/2250 |
| flooded/2250/water.reef_runner/water_pressure_mix | seat1 | eliminated | 12.5 | 0.000 | 0.856 | 2250/2025 |
| flooded/2250/water_pressure_mix/water.reef_runner | seat0 | eliminated | 17.2 | 0.424 | 0.000 | 2025/2250 |
| flooded/2250/water.reef_runner/water_expand_mix | seat1 | eliminated | 14.4 | 0.000 | 1.000 | 2250/2595 |
| flooded/2250/water_expand_mix/water.reef_runner | seat0 | eliminated | 19.1 | 0.754 | 0.000 | 2595/2250 |
| flooded/2250/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 13.1 | 0.000 | 1.000 | 2250/2470 |
| flooded/2250/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 12.1 | 0.720 | 0.000 | 2470/2250 |
| flooded/2250/water.shellback/water.spoutling | seat0 | eliminated | 16.0 | 0.993 | 0.000 | 2275/2250 |
| flooded/2250/water.spoutling/water.shellback | seat1 | eliminated | 18.4 | 0.000 | 1.000 | 2250/2275 |
| flooded/2250/water.shellback/skai_pressure_mix | seat0 | eliminated | 19.3 | 0.941 | 0.000 | 2275/2200 |
| flooded/2250/skai_pressure_mix/water.shellback | seat1 | eliminated | 23.0 | 0.000 | 1.000 | 2200/2275 |
| flooded/2250/water.shellback/skai_expand_mix | seat0 | eliminated | 19.3 | 0.963 | 0.000 | 2275/1950 |
| flooded/2250/skai_expand_mix/water.shellback | seat1 | eliminated | 22.8 | 0.000 | 1.000 | 1950/2275 |
| flooded/2250/water.shellback/skai_air_mix | seat0 | eliminated | 20.8 | 0.946 | 0.000 | 2275/1880 |
| flooded/2250/skai_air_mix/water.shellback | seat1 | eliminated | 24.7 | 0.000 | 1.000 | 1880/2275 |
| flooded/2250/water.shellback/water_pressure_mix | seat1 | eliminated | 28.6 | 0.000 | 0.582 | 2275/2025 |
| flooded/2250/water_pressure_mix/water.shellback | seat0 | eliminated | 39.4 | 0.268 | 0.000 | 2025/2275 |
| flooded/2250/water.shellback/water_expand_mix | seat1 | eliminated | 45.4 | 0.000 | 0.961 | 2275/2595 |
| flooded/2250/water_expand_mix/water.shellback | seat0 | eliminated | 92.5 | 0.636 | 0.000 | 2595/2275 |
| flooded/2250/water.shellback/water_leviathan_mix | seat0 | eliminated | 38.5 | 0.724 | 0.000 | 2275/2470 |
| flooded/2250/water_leviathan_mix/water.shellback | seat1 | eliminated | 33.9 | 0.000 | 0.765 | 2470/2275 |
| flooded/2250/water.spoutling/skai_pressure_mix | seat1 | eliminated | 9.8 | 0.000 | 1.000 | 2250/2200 |
| flooded/2250/skai_pressure_mix/water.spoutling | seat0 | eliminated | 30.5 | 0.261 | 0.000 | 2200/2250 |
| flooded/2250/water.spoutling/skai_expand_mix | seat1 | eliminated | 11.9 | 0.000 | 1.000 | 2250/1950 |
| flooded/2250/skai_expand_mix/water.spoutling | seat1 | eliminated | 21.7 | 0.000 | 0.948 | 1950/2250 |
| flooded/2250/water.spoutling/skai_air_mix | seat1 | eliminated | 11.7 | 0.000 | 1.000 | 2250/1880 |
| flooded/2250/skai_air_mix/water.spoutling | seat1 | eliminated | 20.3 | 0.000 | 0.796 | 1880/2250 |
| flooded/2250/water.spoutling/water_pressure_mix | seat1 | eliminated | 10.7 | 0.000 | 1.000 | 2250/2025 |
| flooded/2250/water_pressure_mix/water.spoutling | seat0 | eliminated | 9.9 | 0.787 | 0.000 | 2025/2250 |
| flooded/2250/water.spoutling/water_expand_mix | seat1 | eliminated | 12.2 | 0.000 | 1.000 | 2250/2595 |
| flooded/2250/water_expand_mix/water.spoutling | seat0 | eliminated | 15.0 | 0.930 | 0.000 | 2595/2250 |
| flooded/2250/water.spoutling/water_leviathan_mix | seat1 | eliminated | 10.3 | 0.000 | 1.000 | 2250/2470 |
| flooded/2250/water_leviathan_mix/water.spoutling | seat0 | eliminated | 10.5 | 0.937 | 0.000 | 2470/2250 |
| flooded/2250/skai_pressure_mix/skai_expand_mix | seat1 | eliminated | 19.0 | 0.000 | 0.789 | 2200/1950 |
| flooded/2250/skai_expand_mix/skai_pressure_mix | seat1 | eliminated | 12.3 | 0.000 | 0.982 | 1950/2200 |
| flooded/2250/skai_pressure_mix/skai_air_mix | seat1 | eliminated | 15.9 | 0.000 | 0.843 | 2200/1880 |
| flooded/2250/skai_air_mix/skai_pressure_mix | seat1 | eliminated | 12.7 | 0.000 | 0.981 | 1880/2200 |
| flooded/2250/skai_pressure_mix/water_pressure_mix | seat1 | eliminated | 11.6 | 0.000 | 0.891 | 2200/2025 |
| flooded/2250/water_pressure_mix/skai_pressure_mix | seat0 | eliminated | 13.6 | 0.547 | 0.000 | 2025/2200 |
| flooded/2250/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 15.0 | 0.000 | 1.000 | 2200/2595 |
| flooded/2250/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 22.7 | 0.810 | 0.000 | 2595/2200 |
| flooded/2250/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 12.2 | 0.000 | 1.000 | 2200/2470 |
| flooded/2250/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 18.1 | 0.687 | 0.000 | 2470/2200 |
| flooded/2250/skai_expand_mix/skai_air_mix | seat1 | eliminated | 17.2 | 0.000 | 0.990 | 1950/1880 |
| flooded/2250/skai_air_mix/skai_expand_mix | seat1 | eliminated | 15.4 | 0.000 | 0.947 | 1880/1950 |
| flooded/2250/skai_expand_mix/water_pressure_mix | seat1 | eliminated | 32.4 | 0.000 | 0.959 | 1950/2025 |
| flooded/2250/water_pressure_mix/skai_expand_mix | seat0 | eliminated | 70.5 | 0.206 | 0.000 | 2025/1950 |
| flooded/2250/skai_expand_mix/water_expand_mix | seat1 | eliminated | 14.7 | 0.000 | 1.000 | 1950/2595 |
| flooded/2250/water_expand_mix/skai_expand_mix | seat0 | eliminated | 21.8 | 0.874 | 0.000 | 2595/1950 |
| flooded/2250/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 11.9 | 0.000 | 1.000 | 1950/2470 |
| flooded/2250/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 13.6 | 0.853 | 0.000 | 2470/1950 |
| flooded/2250/skai_air_mix/water_pressure_mix | seat0 | eliminated | 35.1 | 0.621 | 0.000 | 1880/2025 |
| flooded/2250/water_pressure_mix/skai_air_mix | seat1 | eliminated | 16.9 | 0.000 | 0.880 | 2025/1880 |
| flooded/2250/skai_air_mix/water_expand_mix | seat1 | eliminated | 19.5 | 0.000 | 0.863 | 1880/2595 |
| flooded/2250/water_expand_mix/skai_air_mix | seat0 | eliminated | 35.6 | 0.741 | 0.000 | 2595/1880 |
| flooded/2250/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 16.0 | 0.000 | 1.000 | 1880/2470 |
| flooded/2250/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 15.1 | 0.734 | 0.000 | 2470/1880 |
| flooded/2250/water_pressure_mix/water_expand_mix | seat1 | eliminated | 21.3 | 0.000 | 0.599 | 2025/2595 |
| flooded/2250/water_expand_mix/water_pressure_mix | seat1 | eliminated | 22.3 | 0.000 | 0.391 | 2595/2025 |
| flooded/2250/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 10.7 | 0.000 | 0.883 | 2025/2470 |
| flooded/2250/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 30.1 | 0.093 | 0.000 | 2470/2025 |
| flooded/2250/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 19.7 | 0.000 | 0.888 | 2595/2470 |
| flooded/2250/water_leviathan_mix/water_expand_mix | seat1 | eliminated | 24.1 | 0.000 | 0.631 | 2470/2595 |
| choke/1250/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 12.7 | 1.000 | 0.000 | 1200/1330 |
| choke/1250/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 12.7 | 0.000 | 1.000 | 1330/1200 |
| choke/1250/skai.roc_rider/skai.talon_archer | seat0 | eliminated | 36.7 | 0.289 | 0.000 | 1200/1300 |
| choke/1250/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 28.0 | 0.299 | 0.000 | 1300/1200 |
| choke/1250/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 19.3 | 1.000 | 0.000 | 1200/1325 |
| choke/1250/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 19.3 | 0.000 | 1.000 | 1325/1200 |
| choke/1250/skai.roc_rider/skai.windrunner | seat1 | eliminated | 16.2 | 0.000 | 0.433 | 1200/1275 |
| choke/1250/skai.windrunner/skai.roc_rider | seat0 | eliminated | 16.5 | 0.420 | 0.000 | 1275/1200 |
| choke/1250/skai.roc_rider/water.grasper | seat0 | eliminated | 32.7 | 1.000 | 0.000 | 1200/1300 |
| choke/1250/water.grasper/skai.roc_rider | seat1 | eliminated | 32.7 | 0.000 | 1.000 | 1300/1200 |
| choke/1250/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 14.1 | 1.000 | 0.000 | 1200/1330 |
| choke/1250/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 14.1 | 0.000 | 1.000 | 1330/1200 |
| choke/1250/skai.roc_rider/water.leviathan | seat1 | eliminated | 16.8 | 0.000 | 0.765 | 1200/1140 |
| choke/1250/water.leviathan/skai.roc_rider | seat0 | eliminated | 17.0 | 0.645 | 0.000 | 1140/1200 |
| choke/1250/skai.roc_rider/water.reef_runner | seat0 | eliminated | 21.8 | 1.000 | 0.000 | 1200/1275 |
| choke/1250/water.reef_runner/skai.roc_rider | seat1 | eliminated | 19.4 | 0.000 | 1.000 | 1275/1200 |
| choke/1250/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 36.6 | 1.000 | 0.959 | 1200/1225 |
| choke/1250/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 36.6 | 0.952 | 1.000 | 1225/1200 |
| choke/1250/skai.roc_rider/water.spoutling | seat0 | eliminated | 12.5 | 0.677 | 0.000 | 1200/1250 |
| choke/1250/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.4 | 0.000 | 0.682 | 1250/1200 |
| choke/1250/skai.roc_rider/skai_pressure_mix | seat0 | eliminated | 27.8 | 0.166 | 0.000 | 1200/1100 |
| choke/1250/skai_pressure_mix/skai.roc_rider | seat1 | eliminated | 20.6 | 0.000 | 0.235 | 1100/1200 |
| choke/1250/skai.roc_rider/skai_expand_mix | seat1 | eliminated | 32.4 | 0.000 | 0.349 | 1200/1300 |
| choke/1250/skai_expand_mix/skai.roc_rider | seat0 | eliminated | 15.5 | 0.487 | 0.000 | 1300/1200 |
| choke/1250/skai.roc_rider/skai_air_mix | seat0 | eliminated | 17.0 | 0.686 | 0.000 | 1200/940 |
| choke/1250/skai_air_mix/skai.roc_rider | seat1 | eliminated | 17.0 | 0.000 | 0.648 | 940/1200 |
| choke/1250/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 31.5 | 1.000 | 0.000 | 1200/1350 |
| choke/1250/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 31.5 | 0.000 | 1.000 | 1350/1200 |
| choke/1250/skai.roc_rider/water_expand_mix | seat0 | eliminated | 28.2 | 0.833 | 0.000 | 1200/865 |
| choke/1250/water_expand_mix/skai.roc_rider | seat1 | eliminated | 32.8 | 0.000 | 0.513 | 865/1200 |
| choke/1250/skai.roc_rider/water_leviathan_mix | seat0 | eliminated | 97.4 | 0.417 | 0.000 | 1200/1235 |
| choke/1250/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 19.3 | 0.688 | 0.000 | 1235/1200 |
| choke/1250/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 9.8 | 0.000 | 0.300 | 1330/1300 |
| choke/1250/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 9.9 | 0.249 | 0.000 | 1300/1330 |
| choke/1250/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1330/1325 |
| choke/1250/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 1325/1330 |
| choke/1250/skai.stormcaller/skai.windrunner | seat1 | eliminated | 11.3 | 0.000 | 0.060 | 1330/1275 |
| choke/1250/skai.windrunner/skai.stormcaller | seat0 | eliminated | 10.7 | 0.167 | 0.000 | 1275/1330 |
| choke/1250/skai.stormcaller/water.grasper | seat0 | eliminated | 14.2 | 0.257 | 0.000 | 1330/1300 |
| choke/1250/water.grasper/skai.stormcaller | seat0 | eliminated | 13.9 | 0.132 | 0.000 | 1300/1330 |
| choke/1250/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 8.8 | 0.000 | 0.241 | 1330/1330 |
| choke/1250/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 8.8 | 0.241 | 0.000 | 1330/1330 |
| choke/1250/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.887 | 1330/1140 |
| choke/1250/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.3 | 0.887 | 0.000 | 1140/1330 |
| choke/1250/skai.stormcaller/water.reef_runner | seat1 | eliminated | 12.3 | 0.000 | 0.019 | 1330/1275 |
| choke/1250/water.reef_runner/skai.stormcaller | seat0 | eliminated | 13.0 | 0.078 | 0.000 | 1275/1330 |
| choke/1250/skai.stormcaller/water.shellback | seat1 | eliminated | 15.6 | 0.000 | 0.919 | 1330/1225 |
| choke/1250/water.shellback/skai.stormcaller | seat0 | eliminated | 15.4 | 1.000 | 0.000 | 1225/1330 |
| choke/1250/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.2 | 0.833 | 0.000 | 1330/1250 |
| choke/1250/water.spoutling/skai.stormcaller | seat1 | eliminated | 6.9 | 0.000 | 0.833 | 1250/1330 |
| choke/1250/skai.stormcaller/skai_pressure_mix | seat0 | eliminated | 11.6 | 0.502 | 0.000 | 1330/1100 |
| choke/1250/skai_pressure_mix/skai.stormcaller | seat1 | eliminated | 11.9 | 0.000 | 0.436 | 1100/1330 |
| choke/1250/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 10.8 | 0.000 | 0.484 | 1330/1300 |
| choke/1250/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 12.5 | 0.360 | 0.000 | 1300/1330 |
| choke/1250/skai.stormcaller/skai_air_mix | seat1 | eliminated | 16.3 | 0.000 | 0.920 | 1330/940 |
| choke/1250/skai_air_mix/skai.stormcaller | seat0 | eliminated | 11.8 | 0.920 | 0.000 | 940/1330 |
| choke/1250/skai.stormcaller/water_pressure_mix | seat1 | eliminated | 13.2 | 0.000 | 0.105 | 1330/1350 |
| choke/1250/water_pressure_mix/skai.stormcaller | seat0 | eliminated | 11.1 | 0.278 | 0.000 | 1350/1330 |
| choke/1250/skai.stormcaller/water_expand_mix | seat1 | eliminated | 17.4 | 0.000 | 0.498 | 1330/865 |
| choke/1250/water_expand_mix/skai.stormcaller | seat0 | eliminated | 25.0 | 0.220 | 0.000 | 865/1330 |
| choke/1250/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 7.7 | 0.000 | 0.913 | 1330/1235 |
| choke/1250/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 8.4 | 0.946 | 0.000 | 1235/1330 |
| choke/1250/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 9.7 | 0.000 | 0.618 | 1300/1325 |
| choke/1250/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 6.7 | 0.741 | 0.000 | 1325/1300 |
| choke/1250/skai.talon_archer/skai.windrunner | seat1 | eliminated | 22.0 | 0.000 | 0.129 | 1300/1275 |
| choke/1250/skai.windrunner/skai.talon_archer | seat0 | eliminated | 17.8 | 0.306 | 0.000 | 1275/1300 |
| choke/1250/skai.talon_archer/water.grasper | seat1 | eliminated | 15.1 | 0.000 | 0.557 | 1300/1300 |
| choke/1250/water.grasper/skai.talon_archer | seat0 | eliminated | 14.6 | 0.548 | 0.000 | 1300/1300 |
| choke/1250/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 17.3 | 0.000 | 0.157 | 1300/1330 |
| choke/1250/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 11.7 | 0.380 | 0.000 | 1330/1300 |
| choke/1250/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.945 | 1300/1140 |
| choke/1250/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.5 | 0.959 | 0.000 | 1140/1300 |
| choke/1250/skai.talon_archer/water.reef_runner | seat1 | eliminated | 21.5 | 0.000 | 0.173 | 1300/1275 |
| choke/1250/water.reef_runner/skai.talon_archer | seat0 | eliminated | 24.4 | 0.086 | 0.000 | 1275/1300 |
| choke/1250/skai.talon_archer/water.shellback | seat1 | eliminated | 23.2 | 0.000 | 0.921 | 1300/1225 |
| choke/1250/water.shellback/skai.talon_archer | seat0 | eliminated | 23.0 | 0.910 | 0.000 | 1225/1300 |
| choke/1250/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.6 | 0.825 | 0.000 | 1300/1250 |
| choke/1250/water.spoutling/skai.talon_archer | seat1 | eliminated | 9.9 | 0.000 | 0.821 | 1250/1300 |
| choke/1250/skai.talon_archer/skai_pressure_mix | seat0 | eliminated | 18.7 | 0.323 | 0.000 | 1300/1100 |
| choke/1250/skai_pressure_mix/skai.talon_archer | seat1 | eliminated | 16.7 | 0.000 | 0.382 | 1100/1300 |
| choke/1250/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 23.8 | 0.000 | 0.156 | 1300/1300 |
| choke/1250/skai_expand_mix/skai.talon_archer | seat1 | eliminated | 29.3 | 0.000 | 0.030 | 1300/1300 |
| choke/1250/skai.talon_archer/skai_air_mix | seat0 | eliminated | 13.8 | 0.310 | 0.000 | 1300/940 |
| choke/1250/skai_air_mix/skai.talon_archer | seat1 | eliminated | 36.3 | 0.000 | 0.133 | 940/1300 |
| choke/1250/skai.talon_archer/water_pressure_mix | seat1 | eliminated | 14.4 | 0.000 | 0.577 | 1300/1350 |
| choke/1250/water_pressure_mix/skai.talon_archer | seat0 | eliminated | 13.8 | 0.599 | 0.000 | 1350/1300 |
| choke/1250/skai.talon_archer/water_expand_mix | seat0 | eliminated | 26.9 | 0.152 | 0.000 | 1300/865 |
| choke/1250/water_expand_mix/skai.talon_archer | seat1 | eliminated | 24.1 | 0.000 | 0.276 | 865/1300 |
| choke/1250/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 10.6 | 0.000 | 0.714 | 1300/1235 |
| choke/1250/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 10.4 | 0.837 | 0.000 | 1235/1300 |
| choke/1250/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.8 | 0.735 | 0.000 | 1325/1275 |
| choke/1250/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 0.780 | 1275/1325 |
| choke/1250/skai.thunderhead/water.grasper | seat0 | eliminated | 9.3 | 1.000 | 0.000 | 1325/1300 |
| choke/1250/water.grasper/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 1300/1325 |
| choke/1250/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 4.5 | 1.000 | 0.000 | 1325/1330 |
| choke/1250/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1330/1325 |
| choke/1250/skai.thunderhead/water.leviathan | seat1 | eliminated | 7.3 | 0.000 | 0.336 | 1325/1140 |
| choke/1250/water.leviathan/skai.thunderhead | seat0 | eliminated | 6.9 | 0.428 | 0.000 | 1140/1325 |
| choke/1250/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1325/1275 |
| choke/1250/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 1275/1325 |
| choke/1250/skai.thunderhead/water.shellback | seat0 | eliminated | 39.2 | 1.000 | 0.000 | 1325/1225 |
| choke/1250/water.shellback/skai.thunderhead | seat1 | eliminated | 34.3 | 0.000 | 1.000 | 1225/1325 |
| choke/1250/skai.thunderhead/water.spoutling | seat0 | eliminated | 6.8 | 0.895 | 0.000 | 1325/1250 |
| choke/1250/water.spoutling/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 0.884 | 1250/1325 |
| choke/1250/skai.thunderhead/skai_pressure_mix | seat0 | eliminated | 8.9 | 0.781 | 0.000 | 1325/1100 |
| choke/1250/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 0.777 | 1100/1325 |
| choke/1250/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 46.9 | 0.000 | 0.308 | 1325/1300 |
| choke/1250/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 44.5 | 0.308 | 0.000 | 1300/1325 |
| choke/1250/skai.thunderhead/skai_air_mix | seat1 | eliminated | 38.6 | 0.000 | 0.920 | 1325/940 |
| choke/1250/skai_air_mix/skai.thunderhead | seat0 | eliminated | 38.6 | 0.920 | 0.000 | 940/1325 |
| choke/1250/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 9.0 | 1.000 | 0.000 | 1325/1350 |
| choke/1250/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 1350/1325 |
| choke/1250/skai.thunderhead/water_expand_mix | seat0 | eliminated | 11.6 | 0.804 | 0.000 | 1325/865 |
| choke/1250/water_expand_mix/skai.thunderhead | seat1 | eliminated | 11.7 | 0.000 | 0.803 | 865/1325 |
| choke/1250/skai.thunderhead/water_leviathan_mix | seat1 | eliminated | 19.2 | 0.000 | 0.225 | 1325/1235 |
| choke/1250/water_leviathan_mix/skai.thunderhead | seat0 | eliminated | 13.1 | 0.179 | 0.000 | 1235/1325 |
| choke/1250/skai.windrunner/water.grasper | seat1 | eliminated | 11.4 | 0.000 | 0.610 | 1275/1300 |
| choke/1250/water.grasper/skai.windrunner | seat0 | eliminated | 11.7 | 0.606 | 0.000 | 1300/1275 |
| choke/1250/skai.windrunner/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.405 | 1275/1330 |
| choke/1250/water.ink_lobber/skai.windrunner | seat0 | eliminated | 13.8 | 0.332 | 0.000 | 1330/1275 |
| choke/1250/skai.windrunner/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.974 | 1275/1140 |
| choke/1250/water.leviathan/skai.windrunner | seat0 | eliminated | 4.0 | 0.974 | 0.000 | 1140/1275 |
| choke/1250/skai.windrunner/water.reef_runner | seat0 | eliminated | 17.4 | 0.118 | 0.000 | 1275/1275 |
| choke/1250/water.reef_runner/skai.windrunner | seat1 | eliminated | 19.5 | 0.000 | 0.088 | 1275/1275 |
| choke/1250/skai.windrunner/water.shellback | seat1 | eliminated | 19.3 | 0.000 | 0.857 | 1275/1225 |
| choke/1250/water.shellback/skai.windrunner | seat0 | eliminated | 18.8 | 0.994 | 0.000 | 1225/1275 |
| choke/1250/skai.windrunner/water.spoutling | seat0 | eliminated | 8.0 | 0.804 | 0.000 | 1275/1250 |
| choke/1250/water.spoutling/skai.windrunner | seat1 | eliminated | 8.2 | 0.000 | 0.794 | 1250/1275 |
| choke/1250/skai.windrunner/skai_pressure_mix | seat0 | eliminated | 12.3 | 0.480 | 0.000 | 1275/1100 |
| choke/1250/skai_pressure_mix/skai.windrunner | seat1 | eliminated | 12.2 | 0.000 | 0.457 | 1100/1275 |
| choke/1250/skai.windrunner/skai_expand_mix | seat1 | eliminated | 22.1 | 0.000 | 0.148 | 1275/1300 |
| choke/1250/skai_expand_mix/skai.windrunner | seat1 | eliminated | 20.5 | 0.000 | 0.166 | 1300/1275 |
| choke/1250/skai.windrunner/skai_air_mix | seat0 | eliminated | 12.5 | 0.204 | 0.000 | 1275/940 |
| choke/1250/skai_air_mix/skai.windrunner | seat1 | eliminated | 12.0 | 0.000 | 0.319 | 940/1275 |
| choke/1250/skai.windrunner/water_pressure_mix | seat1 | eliminated | 11.3 | 0.000 | 0.620 | 1275/1350 |
| choke/1250/water_pressure_mix/skai.windrunner | seat0 | eliminated | 11.3 | 0.637 | 0.000 | 1350/1275 |
| choke/1250/skai.windrunner/water_expand_mix | seat0 | eliminated | 22.2 | 0.198 | 0.000 | 1275/865 |
| choke/1250/water_expand_mix/skai.windrunner | seat1 | eliminated | 26.4 | 0.000 | 0.057 | 865/1275 |
| choke/1250/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 7.3 | 0.000 | 0.991 | 1275/1235 |
| choke/1250/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 7.4 | 0.905 | 0.000 | 1235/1275 |
| choke/1250/water.grasper/water.ink_lobber | seat0 | eliminated | 19.5 | 0.041 | 0.000 | 1300/1330 |
| choke/1250/water.ink_lobber/water.grasper | seat0 | eliminated | 11.1 | 0.486 | 0.000 | 1330/1300 |
| choke/1250/water.grasper/water.leviathan | seat1 | eliminated | 4.8 | 0.000 | 0.991 | 1300/1140 |
| choke/1250/water.leviathan/water.grasper | seat0 | eliminated | 4.8 | 1.000 | 0.000 | 1140/1300 |
| choke/1250/water.grasper/water.reef_runner | seat0 | eliminated | 11.6 | 0.607 | 0.000 | 1300/1275 |
| choke/1250/water.reef_runner/water.grasper | seat1 | eliminated | 11.4 | 0.000 | 0.620 | 1275/1300 |
| choke/1250/water.grasper/water.shellback | seat0 | eliminated | 20.2 | 0.635 | 0.000 | 1300/1225 |
| choke/1250/water.shellback/water.grasper | seat1 | eliminated | 19.8 | 0.000 | 0.617 | 1225/1300 |
| choke/1250/water.grasper/water.spoutling | seat0 | eliminated | 8.8 | 0.822 | 0.000 | 1300/1250 |
| choke/1250/water.spoutling/water.grasper | seat1 | eliminated | 9.2 | 0.000 | 0.818 | 1250/1300 |
| choke/1250/water.grasper/skai_pressure_mix | seat0 | eliminated | 10.5 | 0.703 | 0.000 | 1300/1100 |
| choke/1250/skai_pressure_mix/water.grasper | seat1 | eliminated | 10.4 | 0.000 | 0.704 | 1100/1300 |
| choke/1250/water.grasper/skai_expand_mix | seat1 | eliminated | 67.5 | 0.000 | 0.308 | 1300/1300 |
| choke/1250/skai_expand_mix/water.grasper | seat0 | eliminated | 75.3 | 0.308 | 0.000 | 1300/1300 |
| choke/1250/water.grasper/skai_air_mix | seat1 | eliminated | 21.9 | 0.000 | 0.920 | 1300/940 |
| choke/1250/skai_air_mix/water.grasper | seat0 | eliminated | 23.2 | 0.920 | 0.000 | 940/1300 |
| choke/1250/water.grasper/water_pressure_mix | seat1 | eliminated | 29.0 | 0.000 | 0.077 | 1300/1350 |
| choke/1250/water_pressure_mix/water.grasper | seat0 | eliminated | 25.4 | 0.179 | 0.000 | 1350/1300 |
| choke/1250/water.grasper/water_expand_mix | seat0 | eliminated | 11.8 | 0.609 | 0.000 | 1300/865 |
| choke/1250/water_expand_mix/water.grasper | seat1 | eliminated | 11.4 | 0.000 | 0.604 | 865/1300 |
| choke/1250/water.grasper/water_leviathan_mix | seat1 | eliminated | 11.6 | 0.000 | 0.516 | 1300/1235 |
| choke/1250/water_leviathan_mix/water.grasper | seat0 | eliminated | 10.6 | 0.751 | 0.000 | 1235/1300 |
| choke/1250/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.9 | 0.000 | 0.775 | 1330/1140 |
| choke/1250/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.9 | 0.775 | 0.000 | 1140/1330 |
| choke/1250/water.ink_lobber/water.reef_runner | seat0 | eliminated | 13.9 | 0.257 | 0.000 | 1330/1275 |
| choke/1250/water.reef_runner/water.ink_lobber | seat1 | eliminated | 13.9 | 0.000 | 0.243 | 1275/1330 |
| choke/1250/water.ink_lobber/water.shellback | seat1 | eliminated | 16.9 | 0.000 | 0.903 | 1330/1225 |
| choke/1250/water.shellback/water.ink_lobber | seat0 | eliminated | 16.9 | 0.957 | 0.000 | 1225/1330 |
| choke/1250/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.8 | 0.871 | 0.000 | 1330/1250 |
| choke/1250/water.spoutling/water.ink_lobber | seat1 | eliminated | 7.8 | 0.000 | 0.857 | 1250/1330 |
| choke/1250/water.ink_lobber/skai_pressure_mix | seat0 | eliminated | 13.6 | 0.571 | 0.000 | 1330/1100 |
| choke/1250/skai_pressure_mix/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.474 | 1100/1330 |
| choke/1250/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 25.9 | 0.000 | 0.308 | 1330/1300 |
| choke/1250/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 27.2 | 0.308 | 0.000 | 1300/1330 |
| choke/1250/water.ink_lobber/skai_air_mix | seat1 | eliminated | 16.3 | 0.000 | 0.920 | 1330/940 |
| choke/1250/skai_air_mix/water.ink_lobber | seat0 | eliminated | 11.8 | 0.920 | 0.000 | 940/1330 |
| choke/1250/water.ink_lobber/water_pressure_mix | seat0 | eliminated | 13.9 | 0.349 | 0.000 | 1330/1350 |
| choke/1250/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 16.8 | 0.000 | 0.257 | 1350/1330 |
| choke/1250/water.ink_lobber/water_expand_mix | seat0 | eliminated | 14.3 | 0.397 | 0.000 | 1330/865 |
| choke/1250/water_expand_mix/water.ink_lobber | seat1 | eliminated | 15.7 | 0.000 | 0.484 | 865/1330 |
| choke/1250/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 8.7 | 0.000 | 0.871 | 1330/1235 |
| choke/1250/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 10.2 | 0.725 | 0.000 | 1235/1330 |
| choke/1250/water.leviathan/water.reef_runner | seat0 | eliminated | 4.0 | 0.981 | 0.000 | 1140/1275 |
| choke/1250/water.reef_runner/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.981 | 1275/1140 |
| choke/1250/water.leviathan/water.shellback | seat1 | eliminated | 26.0 | 0.000 | 0.630 | 1140/1225 |
| choke/1250/water.shellback/water.leviathan | seat0 | eliminated | 25.9 | 0.948 | 0.000 | 1225/1140 |
| choke/1250/water.leviathan/water.spoutling | seat0 | eliminated | 4.2 | 0.984 | 0.000 | 1140/1250 |
| choke/1250/water.spoutling/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.995 | 1250/1140 |
| choke/1250/water.leviathan/skai_pressure_mix | seat0 | eliminated | 4.3 | 0.973 | 0.000 | 1140/1100 |
| choke/1250/skai_pressure_mix/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.979 | 1100/1140 |
| choke/1250/water.leviathan/skai_expand_mix | seat0 | eliminated | 4.4 | 0.932 | 0.000 | 1140/1300 |
| choke/1250/skai_expand_mix/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.914 | 1300/1140 |
| choke/1250/water.leviathan/skai_air_mix | seat0 | eliminated | 27.6 | 0.223 | 0.000 | 1140/940 |
| choke/1250/skai_air_mix/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.883 | 940/1140 |
| choke/1250/water.leviathan/water_pressure_mix | seat0 | eliminated | 4.9 | 1.000 | 0.000 | 1140/1350 |
| choke/1250/water_pressure_mix/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 1350/1140 |
| choke/1250/water.leviathan/water_expand_mix | seat0 | eliminated | 13.2 | 0.733 | 0.000 | 1140/865 |
| choke/1250/water_expand_mix/water.leviathan | seat1 | eliminated | 10.9 | 0.000 | 0.809 | 865/1140 |
| choke/1250/water.leviathan/water_leviathan_mix | seat1 | eliminated | 14.5 | 0.000 | 0.392 | 1140/1235 |
| choke/1250/water_leviathan_mix/water.leviathan | seat0 | eliminated | 16.0 | 0.438 | 0.000 | 1235/1140 |
| choke/1250/water.reef_runner/water.shellback | seat1 | eliminated | 19.0 | 0.000 | 0.982 | 1275/1225 |
| choke/1250/water.shellback/water.reef_runner | seat0 | eliminated | 18.7 | 0.991 | 0.000 | 1225/1275 |
| choke/1250/water.reef_runner/water.spoutling | seat0 | eliminated | 8.7 | 0.775 | 0.000 | 1275/1250 |
| choke/1250/water.spoutling/water.reef_runner | seat1 | eliminated | 9.0 | 0.000 | 0.779 | 1250/1275 |
| choke/1250/water.reef_runner/skai_pressure_mix | seat0 | eliminated | 12.0 | 0.508 | 0.000 | 1275/1100 |
| choke/1250/skai_pressure_mix/water.reef_runner | seat1 | eliminated | 12.7 | 0.000 | 0.463 | 1100/1275 |
| choke/1250/water.reef_runner/skai_expand_mix | seat1 | eliminated | 33.8 | 0.000 | 0.308 | 1275/1300 |
| choke/1250/skai_expand_mix/water.reef_runner | seat0 | eliminated | 33.8 | 0.308 | 0.000 | 1300/1275 |
| choke/1250/water.reef_runner/skai_air_mix | seat1 | eliminated | 16.5 | 0.000 | 0.920 | 1275/940 |
| choke/1250/skai_air_mix/water.reef_runner | seat0 | eliminated | 16.4 | 0.920 | 0.000 | 940/1275 |
| choke/1250/water.reef_runner/water_pressure_mix | seat1 | eliminated | 11.3 | 0.000 | 0.632 | 1275/1350 |
| choke/1250/water_pressure_mix/water.reef_runner | seat0 | eliminated | 11.3 | 0.643 | 0.000 | 1350/1275 |
| choke/1250/water.reef_runner/water_expand_mix | seat0 | eliminated | 19.7 | 0.283 | 0.000 | 1275/865 |
| choke/1250/water_expand_mix/water.reef_runner | seat0 | eliminated | 25.7 | 0.403 | 0.000 | 865/1275 |
| choke/1250/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 7.3 | 0.000 | 0.957 | 1275/1235 |
| choke/1250/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 7.6 | 0.939 | 0.000 | 1235/1275 |
| choke/1250/water.shellback/water.spoutling | seat0 | eliminated | 15.0 | 0.997 | 0.000 | 1225/1250 |
| choke/1250/water.spoutling/water.shellback | seat1 | eliminated | 15.9 | 0.000 | 0.990 | 1250/1225 |
| choke/1250/water.shellback/skai_pressure_mix | seat0 | eliminated | 17.3 | 0.989 | 0.000 | 1225/1100 |
| choke/1250/skai_pressure_mix/water.shellback | seat1 | eliminated | 18.3 | 0.000 | 0.985 | 1100/1225 |
| choke/1250/water.shellback/skai_expand_mix | stalemate | dome_absorbed | 47.4 | 0.887 | 0.308 | 1225/1300 |
| choke/1250/skai_expand_mix/water.shellback | stalemate | dome_absorbed | 47.4 | 0.308 | 0.873 | 1300/1225 |
| choke/1250/water.shellback/skai_air_mix | stalemate | dome_absorbed | 37.7 | 0.966 | 0.920 | 1225/940 |
| choke/1250/skai_air_mix/water.shellback | stalemate | dome_absorbed | 37.8 | 0.920 | 0.924 | 940/1225 |
| choke/1250/water.shellback/water_pressure_mix | seat1 | eliminated | 21.5 | 0.000 | 0.598 | 1225/1350 |
| choke/1250/water_pressure_mix/water.shellback | seat0 | eliminated | 22.0 | 0.608 | 0.000 | 1350/1225 |
| choke/1250/water.shellback/water_expand_mix | seat0 | eliminated | 28.9 | 0.886 | 0.000 | 1225/865 |
| choke/1250/water_expand_mix/water.shellback | seat1 | eliminated | 30.4 | 0.000 | 0.895 | 865/1225 |
| choke/1250/water.shellback/water_leviathan_mix | seat0 | eliminated | 32.1 | 0.921 | 0.000 | 1225/1235 |
| choke/1250/water_leviathan_mix/water.shellback | seat1 | eliminated | 32.1 | 0.000 | 0.938 | 1235/1225 |
| choke/1250/water.spoutling/skai_pressure_mix | seat1 | eliminated | 10.3 | 0.000 | 0.710 | 1250/1100 |
| choke/1250/skai_pressure_mix/water.spoutling | seat0 | eliminated | 10.1 | 0.699 | 0.000 | 1100/1250 |
| choke/1250/water.spoutling/skai_expand_mix | seat1 | eliminated | 9.2 | 0.000 | 0.779 | 1250/1300 |
| choke/1250/skai_expand_mix/water.spoutling | seat0 | eliminated | 9.0 | 0.788 | 0.000 | 1300/1250 |
| choke/1250/water.spoutling/skai_air_mix | seat1 | eliminated | 11.7 | 0.000 | 0.593 | 1250/940 |
| choke/1250/skai_air_mix/water.spoutling | seat0 | eliminated | 12.5 | 0.601 | 0.000 | 940/1250 |
| choke/1250/water.spoutling/water_pressure_mix | seat1 | eliminated | 8.8 | 0.000 | 0.828 | 1250/1350 |
| choke/1250/water_pressure_mix/water.spoutling | seat0 | eliminated | 8.8 | 0.832 | 0.000 | 1350/1250 |
| choke/1250/water.spoutling/water_expand_mix | seat1 | eliminated | 13.5 | 0.000 | 0.850 | 1250/865 |
| choke/1250/water_expand_mix/water.spoutling | seat0 | eliminated | 14.9 | 0.884 | 0.000 | 865/1250 |
| choke/1250/water.spoutling/water_leviathan_mix | seat1 | eliminated | 7.6 | 0.000 | 0.978 | 1250/1235 |
| choke/1250/water_leviathan_mix/water.spoutling | seat0 | eliminated | 5.8 | 1.000 | 0.000 | 1235/1250 |
| choke/1250/skai_pressure_mix/skai_expand_mix | seat1 | eliminated | 14.6 | 0.000 | 0.501 | 1100/1300 |
| choke/1250/skai_expand_mix/skai_pressure_mix | seat0 | eliminated | 12.9 | 0.550 | 0.000 | 1300/1100 |
| choke/1250/skai_pressure_mix/skai_air_mix | seat1 | eliminated | 24.2 | 0.000 | 0.040 | 1100/940 |
| choke/1250/skai_air_mix/skai_pressure_mix | seat0 | eliminated | 18.9 | 0.136 | 0.000 | 940/1100 |
| choke/1250/skai_pressure_mix/water_pressure_mix | seat1 | eliminated | 10.1 | 0.000 | 0.726 | 1100/1350 |
| choke/1250/water_pressure_mix/skai_pressure_mix | seat0 | eliminated | 9.9 | 0.733 | 0.000 | 1350/1100 |
| choke/1250/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 23.5 | 0.000 | 0.411 | 1100/865 |
| choke/1250/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 22.7 | 0.607 | 0.000 | 865/1100 |
| choke/1250/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 9.7 | 0.000 | 0.895 | 1100/1235 |
| choke/1250/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 8.2 | 1.000 | 0.000 | 1235/1100 |
| choke/1250/skai_expand_mix/skai_air_mix | seat0 | eliminated | 14.0 | 0.380 | 0.000 | 1300/940 |
| choke/1250/skai_air_mix/skai_expand_mix | seat1 | eliminated | 14.0 | 0.000 | 0.503 | 940/1300 |
| choke/1250/skai_expand_mix/water_pressure_mix | seat0 | eliminated | 69.9 | 0.308 | 0.000 | 1300/1350 |
| choke/1250/water_pressure_mix/skai_expand_mix | seat1 | eliminated | 71.1 | 0.000 | 0.308 | 1350/1300 |
| choke/1250/skai_expand_mix/water_expand_mix | seat0 | eliminated | 40.1 | 0.276 | 0.000 | 1300/865 |
| choke/1250/water_expand_mix/skai_expand_mix | seat0 | eliminated | 28.3 | 0.520 | 0.000 | 865/1300 |
| choke/1250/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 10.2 | 0.000 | 0.741 | 1300/1235 |
| choke/1250/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 9.8 | 0.860 | 0.000 | 1235/1300 |
| choke/1250/skai_air_mix/water_pressure_mix | seat0 | eliminated | 24.0 | 0.920 | 0.000 | 940/1350 |
| choke/1250/water_pressure_mix/skai_air_mix | seat1 | eliminated | 26.3 | 0.000 | 0.920 | 1350/940 |
| choke/1250/skai_air_mix/water_expand_mix | seat0 | eliminated | 31.6 | 0.726 | 0.000 | 940/865 |
| choke/1250/water_expand_mix/skai_air_mix | seat0 | eliminated | 30.0 | 0.618 | 0.000 | 865/940 |
| choke/1250/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 10.1 | 0.000 | 0.793 | 940/1235 |
| choke/1250/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 13.3 | 0.802 | 0.000 | 1235/940 |
| choke/1250/water_pressure_mix/water_expand_mix | seat0 | eliminated | 12.2 | 0.638 | 0.000 | 1350/865 |
| choke/1250/water_expand_mix/water_pressure_mix | seat1 | eliminated | 11.9 | 0.000 | 0.625 | 865/1350 |
| choke/1250/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 8.6 | 0.000 | 0.597 | 1350/1235 |
| choke/1250/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 10.6 | 0.771 | 0.000 | 1235/1350 |
| choke/1250/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 16.6 | 0.000 | 0.899 | 865/1235 |
| choke/1250/water_leviathan_mix/water_expand_mix | seat0 | eliminated | 20.0 | 0.776 | 0.000 | 1235/865 |
| flat_uneven/1750/skai.roc_rider/skai.stormcaller/r12500 | seat0 | eliminated | 13.7 | 1.000 | 0.000 | 1800/2280 |
| flat_uneven/1750/skai.stormcaller/skai.roc_rider/r12500 | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 1710/2200 |
| flat_uneven/1750/skai.roc_rider/skai.talon_archer/r12500 | seat1 | eliminated | 15.1 | 0.000 | 0.618 | 1800/2200 |
| flat_uneven/1750/skai.talon_archer/skai.roc_rider/r12500 | seat1 | eliminated | 23.6 | 0.000 | 0.341 | 1800/2200 |
| flat_uneven/1750/skai.roc_rider/skai.thunderhead/r12500 | seat0 | eliminated | 21.7 | 1.000 | 0.000 | 1800/2120 |
| flat_uneven/1750/skai.thunderhead/skai.roc_rider/r12500 | seat1 | eliminated | 15.9 | 0.000 | 1.000 | 1855/2200 |
| flat_uneven/1750/skai.roc_rider/skai.windrunner/r12500 | seat1 | eliminated | 12.9 | 0.000 | 0.623 | 1800/2175 |
| flat_uneven/1750/skai.windrunner/skai.roc_rider/r12500 | seat1 | eliminated | 24.3 | 0.000 | 0.233 | 1725/2200 |
| flat_uneven/1750/skai.roc_rider/water.grasper/r12500 | seat0 | eliminated | 41.3 | 1.000 | 0.000 | 1800/2200 |
| flat_uneven/1750/water.grasper/skai.roc_rider/r12500 | seat1 | eliminated | 23.4 | 0.000 | 1.000 | 1800/2200 |
| flat_uneven/1750/skai.roc_rider/water.ink_lobber/r12500 | seat0 | eliminated | 15.1 | 1.000 | 0.000 | 1800/2280 |
| flat_uneven/1750/water.ink_lobber/skai.roc_rider/r12500 | seat1 | eliminated | 9.4 | 0.000 | 1.000 | 1710/2200 |
| flat_uneven/1750/skai.roc_rider/water.leviathan/r12500 | seat1 | eliminated | 4.2 | 0.000 | 0.954 | 1800/2280 |
| flat_uneven/1750/water.leviathan/skai.roc_rider/r12500 | seat0 | eliminated | 4.4 | 0.930 | 0.000 | 1900/2200 |
| flat_uneven/1750/skai.roc_rider/water.reef_runner/r12500 | seat0 | eliminated | 24.3 | 1.000 | 0.000 | 1800/2175 |
| flat_uneven/1750/water.reef_runner/skai.roc_rider/r12500 | seat1 | eliminated | 15.0 | 0.000 | 1.000 | 1725/2200 |
| flat_uneven/1750/skai.roc_rider/water.shellback/r12500 | stalemate | dome_absorbed | 36.6 | 1.000 | 0.982 | 1800/2275 |
| flat_uneven/1750/water.shellback/skai.roc_rider/r12500 | seat1 | eliminated | 163.9 | 0.000 | 1.000 | 1750/2200 |
| flat_uneven/1750/skai.roc_rider/water.spoutling/r12500 | seat0 | eliminated | 17.4 | 0.503 | 0.000 | 1800/2250 |
| flat_uneven/1750/water.spoutling/skai.roc_rider/r12500 | seat1 | eliminated | 10.2 | 0.000 | 0.807 | 1750/2200 |
| flat_uneven/1750/skai.roc_rider/skai_pressure_mix/r12500 | seat1 | eliminated | 13.3 | 0.000 | 0.611 | 1800/2200 |
| flat_uneven/1750/skai_pressure_mix/skai.roc_rider/r12500 | seat1 | eliminated | 21.0 | 0.000 | 0.406 | 1650/2200 |
| flat_uneven/1750/skai.roc_rider/skai_expand_mix/r12500 | seat1 | eliminated | 20.6 | 0.000 | 0.440 | 1800/1950 |
| flat_uneven/1750/skai_expand_mix/skai.roc_rider/r12500 | seat1 | eliminated | 27.9 | 0.000 | 0.200 | 1950/2200 |
| flat_uneven/1750/skai.roc_rider/skai_air_mix/r12500 | seat0 | eliminated | 30.5 | 0.291 | 0.000 | 1800/1880 |
| flat_uneven/1750/skai_air_mix/skai.roc_rider/r12500 | seat1 | eliminated | 19.9 | 0.000 | 0.586 | 1880/2200 |
| flat_uneven/1750/skai.roc_rider/water_pressure_mix/r12500 | seat0 | eliminated | 33.2 | 1.000 | 0.000 | 1800/2025 |
| flat_uneven/1750/water_pressure_mix/skai.roc_rider/r12500 | seat1 | eliminated | 25.6 | 0.000 | 1.000 | 2025/2200 |
| flat_uneven/1750/skai.roc_rider/water_expand_mix/r12500 | seat1 | eliminated | 20.7 | 0.000 | 0.884 | 1800/2595 |
| flat_uneven/1750/water_expand_mix/skai.roc_rider/r12500 | seat0 | eliminated | 39.8 | 0.493 | 0.000 | 1730/2200 |
| flat_uneven/1750/skai.roc_rider/water_leviathan_mix/r12500 | seat1 | eliminated | 9.6 | 0.000 | 0.949 | 1800/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.roc_rider/r12500 | seat0 | eliminated | 25.2 | 0.194 | 0.000 | 1235/2200 |
| flat_uneven/1750/skai.stormcaller/skai.talon_archer/r12500 | seat1 | eliminated | 8.5 | 0.000 | 0.408 | 1710/2200 |
| flat_uneven/1750/skai.talon_archer/skai.stormcaller/r12500 | seat1 | eliminated | 9.7 | 0.000 | 0.550 | 1800/2280 |
| flat_uneven/1750/skai.stormcaller/skai.thunderhead/r12500 | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1710/2120 |
| flat_uneven/1750/skai.thunderhead/skai.stormcaller/r12500 | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 1855/2280 |
| flat_uneven/1750/skai.stormcaller/skai.windrunner/r12500 | seat1 | eliminated | 7.7 | 0.000 | 0.300 | 1710/2175 |
| flat_uneven/1750/skai.windrunner/skai.stormcaller/r12500 | seat1 | eliminated | 9.4 | 0.000 | 0.538 | 1725/2280 |
| flat_uneven/1750/skai.stormcaller/water.grasper/r12500 | seat1 | eliminated | 9.7 | 0.000 | 0.327 | 1710/2200 |
| flat_uneven/1750/water.grasper/skai.stormcaller/r12500 | seat1 | eliminated | 9.9 | 0.000 | 0.617 | 1800/2280 |
| flat_uneven/1750/skai.stormcaller/water.ink_lobber/r12500 | seat1 | eliminated | 6.0 | 0.000 | 0.504 | 1710/2280 |
| flat_uneven/1750/water.ink_lobber/skai.stormcaller/r12500 | seat1 | eliminated | 8.8 | 0.000 | 0.188 | 1710/2280 |
| flat_uneven/1750/skai.stormcaller/water.leviathan/r12500 | seat1 | eliminated | 3.7 | 0.000 | 0.931 | 1710/2280 |
| flat_uneven/1750/water.leviathan/skai.stormcaller/r12500 | seat0 | eliminated | 4.3 | 0.949 | 0.000 | 1900/2280 |
| flat_uneven/1750/skai.stormcaller/water.reef_runner/r12500 | seat1 | eliminated | 8.6 | 0.000 | 0.326 | 1710/2175 |
| flat_uneven/1750/water.reef_runner/skai.stormcaller/r12500 | seat1 | eliminated | 7.2 | 0.000 | 0.656 | 1725/2280 |
| flat_uneven/1750/skai.stormcaller/water.shellback/r12500 | seat1 | eliminated | 13.6 | 0.000 | 1.000 | 1710/2275 |
| flat_uneven/1750/water.shellback/skai.stormcaller/r12500 | seat0 | eliminated | 17.8 | 0.970 | 0.000 | 1750/2280 |
| flat_uneven/1750/skai.stormcaller/water.spoutling/r12500 | seat0 | eliminated | 9.4 | 0.660 | 0.000 | 1710/2250 |
| flat_uneven/1750/water.spoutling/skai.stormcaller/r12500 | seat1 | eliminated | 6.7 | 0.000 | 0.912 | 1750/2280 |
| flat_uneven/1750/skai.stormcaller/skai_pressure_mix/r12500 | seat1 | eliminated | 7.5 | 0.000 | 0.422 | 1710/2200 |
| flat_uneven/1750/skai_pressure_mix/skai.stormcaller/r12500 | seat1 | eliminated | 11.6 | 0.000 | 0.576 | 1650/2280 |
| flat_uneven/1750/skai.stormcaller/skai_expand_mix/r12500 | seat1 | eliminated | 9.6 | 0.000 | 0.608 | 1710/1950 |
| flat_uneven/1750/skai_expand_mix/skai.stormcaller/r12500 | seat0 | eliminated | 25.3 | 0.308 | 0.000 | 1950/2280 |
| flat_uneven/1750/skai.stormcaller/skai_air_mix/r12500 | seat1 | eliminated | 7.7 | 0.000 | 0.920 | 1710/1880 |
| flat_uneven/1750/skai_air_mix/skai.stormcaller/r12500 | seat0 | eliminated | 14.1 | 0.920 | 0.000 | 1880/2280 |
| flat_uneven/1750/skai.stormcaller/water_pressure_mix/r12500 | seat1 | eliminated | 10.7 | 0.000 | 0.258 | 1710/2025 |
| flat_uneven/1750/water_pressure_mix/skai.stormcaller/r12500 | seat1 | eliminated | 12.4 | 0.000 | 0.461 | 2025/2280 |
| flat_uneven/1750/skai.stormcaller/water_expand_mix/r12500 | seat1 | eliminated | 10.2 | 0.000 | 0.854 | 1710/2595 |
| flat_uneven/1750/water_expand_mix/skai.stormcaller/r12500 | seat0 | eliminated | 17.5 | 0.593 | 0.000 | 1730/2280 |
| flat_uneven/1750/skai.stormcaller/water_leviathan_mix/r12500 | seat1 | eliminated | 5.3 | 0.000 | 0.966 | 1710/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.stormcaller/r12500 | seat0 | eliminated | 11.2 | 0.480 | 0.000 | 1235/2280 |
| flat_uneven/1750/skai.talon_archer/skai.thunderhead/r12500 | seat1 | eliminated | 7.0 | 0.000 | 0.778 | 1800/2120 |
| flat_uneven/1750/skai.thunderhead/skai.talon_archer/r12500 | seat0 | eliminated | 9.3 | 0.633 | 0.000 | 1855/2200 |
| flat_uneven/1750/skai.talon_archer/skai.windrunner/r12500 | seat1 | eliminated | 12.6 | 0.000 | 0.563 | 1800/2175 |
| flat_uneven/1750/skai.windrunner/skai.talon_archer/r12500 | seat1 | eliminated | 13.9 | 0.000 | 0.499 | 1725/2200 |
| flat_uneven/1750/skai.talon_archer/water.grasper/r12500 | seat1 | eliminated | 12.1 | 0.000 | 0.691 | 1800/2200 |
| flat_uneven/1750/water.grasper/skai.talon_archer/r12500 | seat0 | eliminated | 20.3 | 0.359 | 0.000 | 1800/2200 |
| flat_uneven/1750/skai.talon_archer/water.ink_lobber/r12500 | seat1 | eliminated | 8.4 | 0.000 | 0.653 | 1800/2280 |
| flat_uneven/1750/water.ink_lobber/skai.talon_archer/r12500 | seat1 | eliminated | 11.1 | 0.000 | 0.243 | 1710/2200 |
| flat_uneven/1750/skai.talon_archer/water.leviathan/r12500 | seat1 | eliminated | 4.2 | 0.000 | 0.959 | 1800/2280 |
| flat_uneven/1750/water.leviathan/skai.talon_archer/r12500 | seat0 | eliminated | 4.2 | 0.963 | 0.000 | 1900/2200 |
| flat_uneven/1750/skai.talon_archer/water.reef_runner/r12500 | seat1 | eliminated | 13.4 | 0.000 | 0.522 | 1800/2175 |
| flat_uneven/1750/water.reef_runner/skai.talon_archer/r12500 | seat1 | eliminated | 15.2 | 0.000 | 0.473 | 1725/2200 |
| flat_uneven/1750/skai.talon_archer/water.shellback/r12500 | seat1 | eliminated | 19.1 | 0.000 | 0.938 | 1800/2275 |
| flat_uneven/1750/water.shellback/skai.talon_archer/r12500 | seat0 | eliminated | 27.1 | 0.881 | 0.000 | 1750/2200 |
| flat_uneven/1750/skai.talon_archer/water.spoutling/r12500 | seat0 | eliminated | 12.9 | 0.691 | 0.000 | 1800/2250 |
| flat_uneven/1750/water.spoutling/skai.talon_archer/r12500 | seat1 | eliminated | 8.1 | 0.000 | 0.884 | 1750/2200 |
| flat_uneven/1750/skai.talon_archer/skai_pressure_mix/r12500 | seat1 | eliminated | 13.3 | 0.000 | 0.545 | 1800/2200 |
| flat_uneven/1750/skai_pressure_mix/skai.talon_archer/r12500 | seat1 | eliminated | 12.6 | 0.000 | 0.622 | 1650/2200 |
| flat_uneven/1750/skai.talon_archer/skai_expand_mix/r12500 | seat1 | eliminated | 23.0 | 0.000 | 0.258 | 1800/1950 |
| flat_uneven/1750/skai_expand_mix/skai.talon_archer/r12500 | seat1 | eliminated | 16.9 | 0.000 | 0.463 | 1950/2200 |
| flat_uneven/1750/skai.talon_archer/skai_air_mix/r12500 | seat1 | eliminated | 19.5 | 0.000 | 0.356 | 1800/1880 |
| flat_uneven/1750/skai_air_mix/skai.talon_archer/r12500 | seat0 | eliminated | 22.2 | 0.175 | 0.000 | 1880/2200 |
| flat_uneven/1750/skai.talon_archer/water_pressure_mix/r12500 | seat1 | eliminated | 12.8 | 0.000 | 0.661 | 1800/2025 |
| flat_uneven/1750/water_pressure_mix/skai.talon_archer/r12500 | seat0 | eliminated | 16.5 | 0.496 | 0.000 | 2025/2200 |
| flat_uneven/1750/skai.talon_archer/water_expand_mix/r12500 | seat1 | eliminated | 13.2 | 0.000 | 0.940 | 1800/2595 |
| flat_uneven/1750/water_expand_mix/skai.talon_archer/r12500 | seat0 | eliminated | 24.4 | 0.653 | 0.000 | 1730/2200 |
| flat_uneven/1750/skai.talon_archer/water_leviathan_mix/r12500 | seat1 | eliminated | 7.4 | 0.000 | 0.998 | 1800/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.talon_archer/r12500 | seat0 | eliminated | 19.7 | 0.229 | 0.000 | 1235/2200 |
| flat_uneven/1750/skai.thunderhead/skai.windrunner/r12500 | seat0 | eliminated | 6.6 | 0.649 | 0.000 | 1855/2175 |
| flat_uneven/1750/skai.windrunner/skai.thunderhead/r12500 | seat1 | eliminated | 4.3 | 0.000 | 0.863 | 1725/2120 |
| flat_uneven/1750/skai.thunderhead/water.grasper/r12500 | seat0 | eliminated | 9.1 | 1.000 | 0.000 | 1855/2200 |
| flat_uneven/1750/water.grasper/skai.thunderhead/r12500 | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1800/2120 |
| flat_uneven/1750/skai.thunderhead/water.ink_lobber/r12500 | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1855/2280 |
| flat_uneven/1750/water.ink_lobber/skai.thunderhead/r12500 | seat1 | eliminated | 4.6 | 0.000 | 1.000 | 1710/2120 |
| flat_uneven/1750/skai.thunderhead/water.leviathan/r12500 | seat1 | eliminated | 4.1 | 0.000 | 0.891 | 1855/2280 |
| flat_uneven/1750/water.leviathan/skai.thunderhead/r12500 | seat0 | eliminated | 4.3 | 0.714 | 0.000 | 1900/2120 |
| flat_uneven/1750/skai.thunderhead/water.reef_runner/r12500 | seat0 | eliminated | 6.3 | 1.000 | 0.000 | 1855/2175 |
| flat_uneven/1750/water.reef_runner/skai.thunderhead/r12500 | seat1 | eliminated | 6.3 | 0.000 | 1.000 | 1725/2120 |
| flat_uneven/1750/skai.thunderhead/water.shellback/r12500 | stalemate | dome_absorbed | 36.9 | 1.000 | 0.969 | 1855/2275 |
| flat_uneven/1750/water.shellback/skai.thunderhead/r12500 | seat1 | eliminated | 30.2 | 0.000 | 1.000 | 1750/2120 |
| flat_uneven/1750/skai.thunderhead/water.spoutling/r12500 | seat0 | eliminated | 6.5 | 0.868 | 0.000 | 1855/2250 |
| flat_uneven/1750/water.spoutling/skai.thunderhead/r12500 | seat1 | eliminated | 4.5 | 0.000 | 0.924 | 1750/2120 |
| flat_uneven/1750/skai.thunderhead/skai_pressure_mix/r12500 | seat0 | eliminated | 9.7 | 0.674 | 0.000 | 1855/2200 |
| flat_uneven/1750/skai_pressure_mix/skai.thunderhead/r12500 | seat1 | eliminated | 6.7 | 0.000 | 0.840 | 1650/2120 |
| flat_uneven/1750/skai.thunderhead/skai_expand_mix/r12500 | seat1 | eliminated | 45.7 | 0.000 | 0.308 | 1855/1950 |
| flat_uneven/1750/skai_expand_mix/skai.thunderhead/r12500 | seat0 | eliminated | 51.7 | 0.308 | 0.000 | 1950/2120 |
| flat_uneven/1750/skai.thunderhead/skai_air_mix/r12500 | seat1 | eliminated | 27.7 | 0.000 | 0.920 | 1855/1880 |
| flat_uneven/1750/skai_air_mix/skai.thunderhead/r12500 | seat0 | eliminated | 31.6 | 0.920 | 0.000 | 1880/2120 |
| flat_uneven/1750/skai.thunderhead/water_pressure_mix/r12500 | seat0 | eliminated | 9.2 | 1.000 | 0.000 | 1855/2025 |
| flat_uneven/1750/water_pressure_mix/skai.thunderhead/r12500 | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 2025/2120 |
| flat_uneven/1750/skai.thunderhead/water_expand_mix/r12500 | seat1 | eliminated | 20.1 | 0.000 | 0.573 | 1855/2595 |
| flat_uneven/1750/water_expand_mix/skai.thunderhead/r12500 | seat1 | eliminated | 14.4 | 0.000 | 0.693 | 1730/2120 |
| flat_uneven/1750/skai.thunderhead/water_leviathan_mix/r12500 | seat1 | eliminated | 13.1 | 0.000 | 0.899 | 1855/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.thunderhead/r12500 | seat1 | eliminated | 9.8 | 0.000 | 0.480 | 1235/2120 |
| flat_uneven/1750/skai.windrunner/water.grasper/r12500 | seat1 | eliminated | 9.4 | 0.000 | 0.745 | 1725/2200 |
| flat_uneven/1750/water.grasper/skai.windrunner/r12500 | seat0 | eliminated | 16.3 | 0.372 | 0.000 | 1800/2175 |
| flat_uneven/1750/skai.windrunner/water.ink_lobber/r12500 | seat1 | eliminated | 5.3 | 0.000 | 0.806 | 1725/2280 |
| flat_uneven/1750/water.ink_lobber/skai.windrunner/r12500 | seat1 | eliminated | 10.7 | 0.000 | 0.305 | 1710/2175 |
| flat_uneven/1750/skai.windrunner/water.leviathan/r12500 | seat1 | eliminated | 1.8 | 0.000 | 1.000 | 1725/2280 |
| flat_uneven/1750/water.leviathan/skai.windrunner/r12500 | seat0 | eliminated | 4.0 | 0.991 | 0.000 | 1900/2175 |
| flat_uneven/1750/skai.windrunner/water.reef_runner/r12500 | seat1 | eliminated | 11.4 | 0.000 | 0.531 | 1725/2175 |
| flat_uneven/1750/water.reef_runner/skai.windrunner/r12500 | seat1 | eliminated | 11.2 | 0.000 | 0.541 | 1725/2175 |
| flat_uneven/1750/skai.windrunner/water.shellback/r12500 | seat1 | eliminated | 15.8 | 0.000 | 0.966 | 1725/2275 |
| flat_uneven/1750/water.shellback/skai.windrunner/r12500 | seat0 | eliminated | 22.9 | 0.843 | 0.000 | 1750/2175 |
| flat_uneven/1750/skai.windrunner/water.spoutling/r12500 | seat0 | eliminated | 11.2 | 0.667 | 0.000 | 1725/2250 |
| flat_uneven/1750/water.spoutling/skai.windrunner/r12500 | seat1 | eliminated | 7.6 | 0.000 | 0.845 | 1750/2175 |
| flat_uneven/1750/skai.windrunner/skai_pressure_mix/r12500 | seat1 | eliminated | 11.0 | 0.000 | 0.559 | 1725/2200 |
| flat_uneven/1750/skai_pressure_mix/skai.windrunner/r12500 | seat1 | eliminated | 10.3 | 0.000 | 0.631 | 1650/2175 |
| flat_uneven/1750/skai.windrunner/skai_expand_mix/r12500 | seat1 | eliminated | 17.0 | 0.000 | 0.348 | 1725/1950 |
| flat_uneven/1750/skai_expand_mix/skai.windrunner/r12500 | seat1 | eliminated | 14.3 | 0.000 | 0.470 | 1950/2175 |
| flat_uneven/1750/skai.windrunner/skai_air_mix/r12500 | seat1 | eliminated | 19.4 | 0.000 | 0.297 | 1725/1880 |
| flat_uneven/1750/skai_air_mix/skai.windrunner/r12500 | seat0 | eliminated | 19.0 | 0.073 | 0.000 | 1880/2175 |
| flat_uneven/1750/skai.windrunner/water_pressure_mix/r12500 | seat1 | eliminated | 10.7 | 0.000 | 0.692 | 1725/2025 |
| flat_uneven/1750/water_pressure_mix/skai.windrunner/r12500 | seat0 | eliminated | 13.6 | 0.516 | 0.000 | 2025/2175 |
| flat_uneven/1750/skai.windrunner/water_expand_mix/r12500 | seat1 | eliminated | 9.5 | 0.000 | 0.994 | 1725/2595 |
| flat_uneven/1750/water_expand_mix/skai.windrunner/r12500 | seat0 | eliminated | 19.3 | 0.691 | 0.000 | 1730/2175 |
| flat_uneven/1750/skai.windrunner/water_leviathan_mix/r12500 | seat1 | eliminated | 4.7 | 0.000 | 1.000 | 1725/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.windrunner/r12500 | seat0 | eliminated | 13.4 | 0.422 | 0.000 | 1235/2175 |
| flat_uneven/1750/water.grasper/water.ink_lobber/r12500 | seat1 | eliminated | 8.1 | 0.000 | 0.783 | 1800/2280 |
| flat_uneven/1750/water.ink_lobber/water.grasper/r12500 | seat1 | eliminated | 13.9 | 0.000 | 0.131 | 1710/2200 |
| flat_uneven/1750/water.grasper/water.leviathan/r12500 | seat1 | eliminated | 2.7 | 0.000 | 1.000 | 1800/2280 |
| flat_uneven/1750/water.leviathan/water.grasper/r12500 | seat0 | eliminated | 4.5 | 1.000 | 0.000 | 1900/2200 |
| flat_uneven/1750/water.grasper/water.reef_runner/r12500 | seat0 | eliminated | 15.7 | 0.398 | 0.000 | 1800/2175 |
| flat_uneven/1750/water.reef_runner/water.grasper/r12500 | seat1 | eliminated | 9.9 | 0.000 | 0.749 | 1725/2200 |
| flat_uneven/1750/water.grasper/water.shellback/r12500 | seat0 | eliminated | 36.0 | 0.210 | 0.000 | 1800/2275 |
| flat_uneven/1750/water.shellback/water.grasper/r12500 | seat1 | eliminated | 17.0 | 0.000 | 0.725 | 1750/2200 |
| flat_uneven/1750/water.grasper/water.spoutling/r12500 | seat0 | eliminated | 11.1 | 0.732 | 0.000 | 1800/2250 |
| flat_uneven/1750/water.spoutling/water.grasper/r12500 | seat1 | eliminated | 8.6 | 0.000 | 0.866 | 1750/2200 |
| flat_uneven/1750/water.grasper/skai_pressure_mix/r12500 | seat0 | eliminated | 15.9 | 0.416 | 0.000 | 1800/2200 |
| flat_uneven/1750/skai_pressure_mix/water.grasper/r12500 | seat1 | eliminated | 9.2 | 0.000 | 0.767 | 1650/2200 |
| flat_uneven/1750/water.grasper/skai_expand_mix/r12500 | seat1 | eliminated | 61.5 | 0.000 | 0.308 | 1800/1950 |
| flat_uneven/1750/skai_expand_mix/water.grasper/r12500 | seat0 | eliminated | 84.4 | 0.308 | 0.000 | 1950/2200 |
| flat_uneven/1750/water.grasper/skai_air_mix/r12500 | seat1 | eliminated | 14.3 | 0.000 | 0.920 | 1800/1880 |
| flat_uneven/1750/skai_air_mix/water.grasper/r12500 | seat0 | eliminated | 21.8 | 0.920 | 0.000 | 1880/2200 |
| flat_uneven/1750/water.grasper/water_pressure_mix/r12500 | seat1 | eliminated | 21.0 | 0.000 | 0.384 | 1800/2025 |
| flat_uneven/1750/water_pressure_mix/water.grasper/r12500 | seat1 | eliminated | 20.6 | 0.000 | 0.341 | 2025/2200 |
| flat_uneven/1750/water.grasper/water_expand_mix/r12500 | seat1 | eliminated | 17.0 | 0.000 | 0.605 | 1800/2595 |
| flat_uneven/1750/water_expand_mix/water.grasper/r12500 | seat1 | eliminated | 13.9 | 0.000 | 0.534 | 1730/2200 |
| flat_uneven/1750/water.grasper/water_leviathan_mix/r12500 | seat1 | eliminated | 7.2 | 0.000 | 0.890 | 1800/2470 |
| flat_uneven/1750/water_leviathan_mix/water.grasper/r12500 | seat1 | eliminated | 14.2 | 0.000 | 0.102 | 1235/2200 |
| flat_uneven/1750/water.ink_lobber/water.leviathan/r12500 | seat1 | eliminated | 4.4 | 0.000 | 0.887 | 1710/2280 |
| flat_uneven/1750/water.leviathan/water.ink_lobber/r12500 | seat0 | eliminated | 5.0 | 0.865 | 0.000 | 1900/2280 |
| flat_uneven/1750/water.ink_lobber/water.reef_runner/r12500 | seat1 | eliminated | 9.1 | 0.000 | 0.410 | 1710/2175 |
| flat_uneven/1750/water.reef_runner/water.ink_lobber/r12500 | seat1 | eliminated | 7.8 | 0.000 | 0.753 | 1725/2280 |
| flat_uneven/1750/water.ink_lobber/water.shellback/r12500 | seat1 | eliminated | 14.6 | 0.000 | 0.950 | 1710/2275 |
| flat_uneven/1750/water.shellback/water.ink_lobber/r12500 | seat0 | eliminated | 19.5 | 0.770 | 0.000 | 1750/2280 |
| flat_uneven/1750/water.ink_lobber/water.spoutling/r12500 | seat0 | eliminated | 8.4 | 0.772 | 0.000 | 1710/2250 |
| flat_uneven/1750/water.spoutling/water.ink_lobber/r12500 | seat1 | eliminated | 5.6 | 0.000 | 0.946 | 1750/2280 |
| flat_uneven/1750/water.ink_lobber/skai_pressure_mix/r12500 | seat1 | eliminated | 9.5 | 0.000 | 0.339 | 1710/2200 |
| flat_uneven/1750/skai_pressure_mix/water.ink_lobber/r12500 | seat1 | eliminated | 8.0 | 0.000 | 0.799 | 1650/2280 |
| flat_uneven/1750/water.ink_lobber/skai_expand_mix/r12500 | seat1 | eliminated | 13.9 | 0.000 | 0.356 | 1710/1950 |
| flat_uneven/1750/skai_expand_mix/water.ink_lobber/r12500 | seat0 | eliminated | 36.5 | 0.308 | 0.000 | 1950/2280 |
| flat_uneven/1750/water.ink_lobber/skai_air_mix/r12500 | seat1 | eliminated | 9.1 | 0.000 | 0.920 | 1710/1880 |
| flat_uneven/1750/skai_air_mix/water.ink_lobber/r12500 | seat0 | eliminated | 12.1 | 0.920 | 0.000 | 1880/2280 |
| flat_uneven/1750/water.ink_lobber/water_pressure_mix/r12500 | seat1 | eliminated | 12.7 | 0.000 | 0.154 | 1710/2025 |
| flat_uneven/1750/water_pressure_mix/water.ink_lobber/r12500 | seat1 | eliminated | 11.0 | 0.000 | 0.698 | 2025/2280 |
| flat_uneven/1750/water.ink_lobber/water_expand_mix/r12500 | seat1 | eliminated | 11.9 | 0.000 | 0.802 | 1710/2595 |
| flat_uneven/1750/water_expand_mix/water.ink_lobber/r12500 | seat1 | eliminated | 20.1 | 0.000 | 0.225 | 1730/2280 |
| flat_uneven/1750/water.ink_lobber/water_leviathan_mix/r12500 | seat1 | eliminated | 7.3 | 0.000 | 0.901 | 1710/2470 |
| flat_uneven/1750/water_leviathan_mix/water.ink_lobber/r12500 | seat0 | eliminated | 17.1 | 0.204 | 0.000 | 1235/2280 |
| flat_uneven/1750/water.leviathan/water.reef_runner/r12500 | seat0 | eliminated | 2.7 | 1.000 | 0.000 | 1900/2175 |
| flat_uneven/1750/water.reef_runner/water.leviathan/r12500 | seat1 | eliminated | 1.9 | 0.000 | 1.000 | 1725/2280 |
| flat_uneven/1750/water.leviathan/water.shellback/r12500 | seat1 | eliminated | 26.0 | 0.000 | 0.867 | 1900/2275 |
| flat_uneven/1750/water.shellback/water.leviathan/r12500 | seat1 | eliminated | 16.2 | 0.000 | 0.689 | 1750/2280 |
| flat_uneven/1750/water.leviathan/water.spoutling/r12500 | seat0 | eliminated | 4.2 | 0.997 | 0.000 | 1900/2250 |
| flat_uneven/1750/water.spoutling/water.leviathan/r12500 | seat1 | eliminated | 3.4 | 0.000 | 0.995 | 1750/2280 |
| flat_uneven/1750/water.leviathan/skai_pressure_mix/r12500 | seat0 | eliminated | 4.0 | 0.980 | 0.000 | 1900/2200 |
| flat_uneven/1750/skai_pressure_mix/water.leviathan/r12500 | seat1 | eliminated | 4.0 | 0.000 | 0.980 | 1650/2280 |
| flat_uneven/1750/water.leviathan/skai_expand_mix/r12500 | seat0 | eliminated | 5.1 | 0.928 | 0.000 | 1900/1950 |
| flat_uneven/1750/skai_expand_mix/water.leviathan/r12500 | seat1 | eliminated | 4.1 | 0.000 | 0.953 | 1950/2280 |
| flat_uneven/1750/water.leviathan/skai_air_mix/r12500 | seat0 | eliminated | 4.3 | 0.845 | 0.000 | 1900/1880 |
| flat_uneven/1750/skai_air_mix/water.leviathan/r12500 | seat1 | eliminated | 4.4 | 0.000 | 0.941 | 1880/2280 |
| flat_uneven/1750/water.leviathan/water_pressure_mix/r12500 | seat0 | eliminated | 5.0 | 1.000 | 0.000 | 1900/2025 |
| flat_uneven/1750/water_pressure_mix/water.leviathan/r12500 | seat1 | eliminated | 4.1 | 0.000 | 1.000 | 2025/2280 |
| flat_uneven/1750/water.leviathan/water_expand_mix/r12500 | seat0 | eliminated | 16.8 | 0.526 | 0.000 | 1900/2595 |
| flat_uneven/1750/water_expand_mix/water.leviathan/r12500 | seat1 | eliminated | 10.2 | 0.000 | 0.861 | 1730/2280 |
| flat_uneven/1750/water.leviathan/water_leviathan_mix/r12500 | seat1 | eliminated | 15.0 | 0.000 | 0.446 | 1900/2470 |
| flat_uneven/1750/water_leviathan_mix/water.leviathan/r12500 | seat1 | eliminated | 10.6 | 0.000 | 0.688 | 1235/2280 |
| flat_uneven/1750/water.reef_runner/water.shellback/r12500 | seat1 | eliminated | 16.1 | 0.000 | 0.987 | 1725/2275 |
| flat_uneven/1750/water.shellback/water.reef_runner/r12500 | seat0 | eliminated | 23.6 | 0.877 | 0.000 | 1750/2175 |
| flat_uneven/1750/water.reef_runner/water.spoutling/r12500 | seat0 | eliminated | 11.8 | 0.638 | 0.000 | 1725/2250 |
| flat_uneven/1750/water.spoutling/water.reef_runner/r12500 | seat1 | eliminated | 7.7 | 0.000 | 0.842 | 1750/2175 |
| flat_uneven/1750/water.reef_runner/skai_pressure_mix/r12500 | seat1 | eliminated | 11.4 | 0.000 | 0.519 | 1725/2200 |
| flat_uneven/1750/skai_pressure_mix/water.reef_runner/r12500 | seat1 | eliminated | 10.9 | 0.000 | 0.586 | 1650/2175 |
| flat_uneven/1750/water.reef_runner/skai_expand_mix/r12500 | seat1 | eliminated | 27.8 | 0.000 | 0.308 | 1725/1950 |
| flat_uneven/1750/skai_expand_mix/water.reef_runner/r12500 | seat0 | eliminated | 47.0 | 0.308 | 0.000 | 1950/2175 |
| flat_uneven/1750/water.reef_runner/skai_air_mix/r12500 | seat1 | eliminated | 11.0 | 0.000 | 0.920 | 1725/1880 |
| flat_uneven/1750/skai_air_mix/water.reef_runner/r12500 | seat0 | eliminated | 10.9 | 0.920 | 0.000 | 1880/2175 |
| flat_uneven/1750/water.reef_runner/water_pressure_mix/r12500 | seat1 | eliminated | 10.4 | 0.000 | 0.714 | 1725/2025 |
| flat_uneven/1750/water_pressure_mix/water.reef_runner/r12500 | seat0 | eliminated | 13.6 | 0.525 | 0.000 | 2025/2175 |
| flat_uneven/1750/water.reef_runner/water_expand_mix/r12500 | seat1 | eliminated | 10.6 | 0.000 | 0.960 | 1725/2595 |
| flat_uneven/1750/water_expand_mix/water.reef_runner/r12500 | seat0 | eliminated | 19.1 | 0.694 | 0.000 | 1730/2175 |
| flat_uneven/1750/water.reef_runner/water_leviathan_mix/r12500 | seat1 | eliminated | 5.1 | 0.000 | 1.000 | 1725/2470 |
| flat_uneven/1750/water_leviathan_mix/water.reef_runner/r12500 | seat0 | eliminated | 13.4 | 0.618 | 0.000 | 1235/2175 |
| flat_uneven/1750/water.shellback/water.spoutling/r12500 | seat0 | eliminated | 18.6 | 0.989 | 0.000 | 1750/2250 |
| flat_uneven/1750/water.spoutling/water.shellback/r12500 | seat1 | eliminated | 13.6 | 0.000 | 0.998 | 1750/2275 |
| flat_uneven/1750/water.shellback/skai_pressure_mix/r12500 | seat0 | eliminated | 24.8 | 0.830 | 0.000 | 1750/2200 |
| flat_uneven/1750/skai_pressure_mix/water.shellback/r12500 | seat1 | eliminated | 15.9 | 0.000 | 0.991 | 1650/2275 |
| flat_uneven/1750/water.shellback/skai_expand_mix/r12500 | stalemate | dome_absorbed | 47.5 | 0.908 | 0.308 | 1750/1950 |
| flat_uneven/1750/skai_expand_mix/water.shellback/r12500 | stalemate | dome_absorbed | 45.5 | 0.308 | 0.914 | 1950/2275 |
| flat_uneven/1750/water.shellback/skai_air_mix/r12500 | stalemate | dome_absorbed | 101.7 | 0.876 | 0.920 | 1750/1880 |
| flat_uneven/1750/skai_air_mix/water.shellback/r12500 | stalemate | dome_absorbed | 37.7 | 0.920 | 0.996 | 1880/2275 |
| flat_uneven/1750/water.shellback/water_pressure_mix/r12500 | seat1 | eliminated | 20.8 | 0.000 | 0.632 | 1750/2025 |
| flat_uneven/1750/water_pressure_mix/water.shellback/r12500 | seat0 | eliminated | 40.9 | 0.219 | 0.000 | 2025/2275 |
| flat_uneven/1750/water.shellback/water_expand_mix/r12500 | seat1 | eliminated | 37.8 | 0.000 | 0.907 | 1750/2595 |
| flat_uneven/1750/water_expand_mix/water.shellback/r12500 | seat1 | eliminated | 35.7 | 0.000 | 0.862 | 1730/2275 |
| flat_uneven/1750/water.shellback/water_leviathan_mix/r12500 | seat1 | eliminated | 32.2 | 0.000 | 0.755 | 1750/2470 |
| flat_uneven/1750/water_leviathan_mix/water.shellback/r12500 | seat1 | eliminated | 19.3 | 0.000 | 0.997 | 1235/2275 |
| flat_uneven/1750/water.spoutling/skai_pressure_mix/r12500 | seat1 | eliminated | 7.7 | 0.000 | 0.844 | 1750/2200 |
| flat_uneven/1750/skai_pressure_mix/water.spoutling/r12500 | seat0 | eliminated | 12.3 | 0.606 | 0.000 | 1650/2250 |
| flat_uneven/1750/water.spoutling/skai_expand_mix/r12500 | seat1 | eliminated | 8.5 | 0.000 | 0.837 | 1750/1950 |
| flat_uneven/1750/skai_expand_mix/water.spoutling/r12500 | seat0 | eliminated | 11.8 | 0.721 | 0.000 | 1950/2250 |
| flat_uneven/1750/water.spoutling/skai_air_mix/r12500 | seat1 | eliminated | 9.2 | 0.000 | 0.783 | 1750/1880 |
| flat_uneven/1750/skai_air_mix/water.spoutling/r12500 | seat0 | eliminated | 12.4 | 0.674 | 0.000 | 1880/2250 |
| flat_uneven/1750/water.spoutling/water_pressure_mix/r12500 | seat1 | eliminated | 8.6 | 0.000 | 0.854 | 1750/2025 |
| flat_uneven/1750/water_pressure_mix/water.spoutling/r12500 | seat0 | eliminated | 10.4 | 0.780 | 0.000 | 2025/2250 |
| flat_uneven/1750/water.spoutling/water_expand_mix/r12500 | seat1 | eliminated | 9.4 | 0.000 | 0.961 | 1750/2595 |
| flat_uneven/1750/water_expand_mix/water.spoutling/r12500 | seat0 | eliminated | 14.3 | 0.929 | 0.000 | 1730/2250 |
| flat_uneven/1750/water.spoutling/water_leviathan_mix/r12500 | seat1 | eliminated | 4.9 | 0.000 | 1.000 | 1750/2470 |
| flat_uneven/1750/water_leviathan_mix/water.spoutling/r12500 | seat0 | eliminated | 10.1 | 0.942 | 0.000 | 1235/2250 |
| flat_uneven/1750/skai_pressure_mix/skai_expand_mix/r12500 | seat1 | eliminated | 15.0 | 0.000 | 0.501 | 1650/1950 |
| flat_uneven/1750/skai_expand_mix/skai_pressure_mix/r12500 | seat1 | eliminated | 15.7 | 0.000 | 0.424 | 1950/2200 |
| flat_uneven/1750/skai_pressure_mix/skai_air_mix/r12500 | seat1 | eliminated | 15.8 | 0.000 | 0.499 | 1650/1880 |
| flat_uneven/1750/skai_air_mix/skai_pressure_mix/r12500 | seat0 | eliminated | 19.5 | 0.199 | 0.000 | 1880/2200 |
| flat_uneven/1750/skai_pressure_mix/water_pressure_mix/r12500 | seat1 | eliminated | 10.3 | 0.000 | 0.740 | 1650/2025 |
| flat_uneven/1750/water_pressure_mix/skai_pressure_mix/r12500 | seat0 | eliminated | 14.4 | 0.530 | 0.000 | 2025/2200 |
| flat_uneven/1750/skai_pressure_mix/water_expand_mix/r12500 | seat1 | eliminated | 10.8 | 0.000 | 1.000 | 1650/2595 |
| flat_uneven/1750/water_expand_mix/skai_pressure_mix/r12500 | seat0 | eliminated | 21.0 | 0.697 | 0.000 | 1730/2200 |
| flat_uneven/1750/skai_pressure_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 7.3 | 0.000 | 1.000 | 1650/2470 |
| flat_uneven/1750/water_leviathan_mix/skai_pressure_mix/r12500 | seat0 | eliminated | 14.2 | 0.491 | 0.000 | 1235/2200 |
| flat_uneven/1750/skai_expand_mix/skai_air_mix/r12500 | seat0 | eliminated | 23.8 | 0.206 | 0.000 | 1950/1880 |
| flat_uneven/1750/skai_air_mix/skai_expand_mix/r12500 | seat0 | eliminated | 16.7 | 0.403 | 0.000 | 1880/1950 |
| flat_uneven/1750/skai_expand_mix/water_pressure_mix/r12500 | seat0 | eliminated | 68.8 | 0.308 | 0.000 | 1950/2025 |
| flat_uneven/1750/water_pressure_mix/skai_expand_mix/r12500 | seat1 | eliminated | 75.7 | 0.000 | 0.308 | 2025/1950 |
| flat_uneven/1750/skai_expand_mix/water_expand_mix/r12500 | seat1 | eliminated | 13.6 | 0.000 | 0.963 | 1950/2595 |
| flat_uneven/1750/water_expand_mix/skai_expand_mix/r12500 | seat0 | eliminated | 18.5 | 0.807 | 0.000 | 1730/1950 |
| flat_uneven/1750/skai_expand_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 8.4 | 0.000 | 0.983 | 1950/2470 |
| flat_uneven/1750/water_leviathan_mix/skai_expand_mix/r12500 | seat0 | eliminated | 13.3 | 0.633 | 0.000 | 1235/1950 |
| flat_uneven/1750/skai_air_mix/water_pressure_mix/r12500 | seat0 | eliminated | 16.7 | 0.920 | 0.000 | 1880/2025 |
| flat_uneven/1750/water_pressure_mix/skai_air_mix/r12500 | seat1 | eliminated | 22.2 | 0.000 | 0.920 | 2025/1880 |
| flat_uneven/1750/skai_air_mix/water_expand_mix/r12500 | seat1 | eliminated | 19.3 | 0.000 | 0.737 | 1880/2595 |
| flat_uneven/1750/water_expand_mix/skai_air_mix/r12500 | seat0 | eliminated | 30.8 | 0.539 | 0.000 | 1730/1880 |
| flat_uneven/1750/skai_air_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 10.7 | 0.000 | 0.899 | 1880/2470 |
| flat_uneven/1750/water_leviathan_mix/skai_air_mix/r12500 | seat0 | eliminated | 25.2 | 0.023 | 0.000 | 1235/1880 |
| flat_uneven/1750/water_pressure_mix/water_expand_mix/r12500 | seat1 | eliminated | 17.7 | 0.000 | 0.701 | 2025/2595 |
| flat_uneven/1750/water_expand_mix/water_pressure_mix/r12500 | seat1 | eliminated | 17.7 | 0.000 | 0.280 | 1730/2025 |
| flat_uneven/1750/water_pressure_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 7.5 | 0.000 | 0.890 | 2025/2470 |
| flat_uneven/1750/water_leviathan_mix/water_pressure_mix/r12500 | seat0 | eliminated | 19.7 | 0.273 | 0.000 | 1235/2025 |
| flat_uneven/1750/water_expand_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 13.9 | 0.000 | 0.941 | 1730/2470 |
| flat_uneven/1750/water_leviathan_mix/water_expand_mix/r12500 | seat1 | eliminated | 12.9 | 0.000 | 0.968 | 1235/2595 |
| flat_uneven/1750/skai.roc_rider/skai.stormcaller/r15000 | seat0 | eliminated | 15.0 | 1.000 | 0.000 | 1800/2660 |
| flat_uneven/1750/skai.stormcaller/skai.roc_rider/r15000 | seat1 | eliminated | 7.8 | 0.000 | 1.000 | 1710/2600 |
| flat_uneven/1750/skai.roc_rider/skai.talon_archer/r15000 | seat1 | eliminated | 11.9 | 0.000 | 0.751 | 1800/2600 |
| flat_uneven/1750/skai.talon_archer/skai.roc_rider/r15000 | seat1 | eliminated | 16.3 | 0.000 | 0.589 | 1800/2600 |
| flat_uneven/1750/skai.roc_rider/skai.thunderhead/r15000 | seat0 | eliminated | 26.5 | 1.000 | 0.000 | 1800/2650 |
| flat_uneven/1750/skai.thunderhead/skai.roc_rider/r15000 | seat1 | eliminated | 13.4 | 0.000 | 1.000 | 1855/2600 |
| flat_uneven/1750/skai.roc_rider/skai.windrunner/r15000 | seat1 | eliminated | 10.0 | 0.000 | 0.780 | 1800/2625 |
| flat_uneven/1750/skai.windrunner/skai.roc_rider/r15000 | seat1 | eliminated | 17.1 | 0.000 | 0.507 | 1725/2600 |
| flat_uneven/1750/skai.roc_rider/water.grasper/r15000 | seat0 | eliminated | 43.6 | 1.000 | 0.000 | 1800/2600 |
| flat_uneven/1750/water.grasper/skai.roc_rider/r15000 | seat1 | eliminated | 20.8 | 0.000 | 1.000 | 1800/2600 |
| flat_uneven/1750/skai.roc_rider/water.ink_lobber/r15000 | seat0 | eliminated | 16.3 | 1.000 | 0.000 | 1800/2660 |
| flat_uneven/1750/water.ink_lobber/skai.roc_rider/r15000 | seat1 | eliminated | 7.9 | 0.000 | 1.000 | 1710/2600 |
| flat_uneven/1750/skai.roc_rider/water.leviathan/r15000 | seat1 | eliminated | 4.0 | 0.000 | 0.990 | 1800/2660 |
| flat_uneven/1750/water.leviathan/skai.roc_rider/r15000 | seat0 | eliminated | 7.0 | 0.903 | 0.000 | 1900/2600 |
| flat_uneven/1750/skai.roc_rider/water.reef_runner/r15000 | seat0 | eliminated | 30.3 | 1.000 | 0.000 | 1800/2625 |
| flat_uneven/1750/water.reef_runner/skai.roc_rider/r15000 | seat1 | eliminated | 13.5 | 0.000 | 1.000 | 1725/2600 |
| flat_uneven/1750/skai.roc_rider/water.shellback/r15000 | stalemate | dome_absorbed | 36.6 | 1.000 | 0.997 | 1800/2625 |
| flat_uneven/1750/water.shellback/skai.roc_rider/r15000 | seat1 | eliminated | 82.1 | 0.000 | 1.000 | 1750/2600 |
| flat_uneven/1750/skai.roc_rider/water.spoutling/r15000 | seat0 | eliminated | 27.3 | 0.188 | 0.000 | 1800/2625 |
| flat_uneven/1750/water.spoutling/skai.roc_rider/r15000 | seat1 | eliminated | 8.1 | 0.000 | 0.868 | 1750/2600 |
| flat_uneven/1750/skai.roc_rider/skai_pressure_mix/r15000 | seat1 | eliminated | 10.0 | 0.000 | 0.785 | 1800/2750 |
| flat_uneven/1750/skai_pressure_mix/skai.roc_rider/r15000 | seat1 | eliminated | 15.9 | 0.000 | 0.599 | 1650/2600 |
| flat_uneven/1750/skai.roc_rider/skai_expand_mix/r15000 | seat1 | eliminated | 12.9 | 0.000 | 0.708 | 1800/2600 |
| flat_uneven/1750/skai_expand_mix/skai.roc_rider/r15000 | seat1 | eliminated | 17.3 | 0.000 | 0.523 | 1950/2600 |
| flat_uneven/1750/skai.roc_rider/skai_air_mix/r15000 | seat1 | eliminated | 20.5 | 0.000 | 0.589 | 1800/2820 |
| flat_uneven/1750/skai_air_mix/skai.roc_rider/r15000 | seat1 | eliminated | 16.2 | 0.000 | 0.681 | 1880/2600 |
| flat_uneven/1750/skai.roc_rider/water_pressure_mix/r15000 | seat0 | eliminated | 43.5 | 1.000 | 0.000 | 1800/2700 |
| flat_uneven/1750/water_pressure_mix/skai.roc_rider/r15000 | seat1 | eliminated | 22.4 | 0.000 | 1.000 | 2025/2600 |
| flat_uneven/1750/skai.roc_rider/water_expand_mix/r15000 | seat1 | eliminated | 20.7 | 0.000 | 0.884 | 1800/2595 |
| flat_uneven/1750/water_expand_mix/skai.roc_rider/r15000 | seat1 | eliminated | 31.9 | 0.000 | 0.470 | 1730/2600 |
| flat_uneven/1750/skai.roc_rider/water_leviathan_mix/r15000 | seat1 | eliminated | 9.6 | 0.000 | 0.949 | 1800/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.roc_rider/r15000 | seat1 | eliminated | 18.6 | 0.000 | 0.135 | 1235/2600 |
| flat_uneven/1750/skai.stormcaller/skai.talon_archer/r15000 | seat1 | eliminated | 7.4 | 0.000 | 0.536 | 1710/2600 |
| flat_uneven/1750/skai.talon_archer/skai.stormcaller/r15000 | seat1 | eliminated | 9.2 | 0.000 | 0.645 | 1800/2660 |
| flat_uneven/1750/skai.stormcaller/skai.thunderhead/r15000 | seat1 | eliminated | 4.3 | 0.000 | 1.000 | 1710/2650 |
| flat_uneven/1750/skai.thunderhead/skai.stormcaller/r15000 | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1855/2660 |
| flat_uneven/1750/skai.stormcaller/skai.windrunner/r15000 | seat1 | eliminated | 6.7 | 0.000 | 0.511 | 1710/2625 |
| flat_uneven/1750/skai.windrunner/skai.stormcaller/r15000 | seat1 | eliminated | 6.5 | 0.000 | 0.778 | 1725/2660 |
| flat_uneven/1750/skai.stormcaller/water.grasper/r15000 | seat1 | eliminated | 9.1 | 0.000 | 0.435 | 1710/2600 |
| flat_uneven/1750/water.grasper/skai.stormcaller/r15000 | seat1 | eliminated | 9.1 | 0.000 | 0.790 | 1800/2660 |
| flat_uneven/1750/skai.stormcaller/water.ink_lobber/r15000 | seat1 | eliminated | 5.9 | 0.000 | 0.533 | 1710/2660 |
| flat_uneven/1750/water.ink_lobber/skai.stormcaller/r15000 | seat1 | eliminated | 8.1 | 0.000 | 0.339 | 1710/2660 |
| flat_uneven/1750/skai.stormcaller/water.leviathan/r15000 | seat1 | eliminated | 3.7 | 0.000 | 0.976 | 1710/2660 |
| flat_uneven/1750/water.leviathan/skai.stormcaller/r15000 | seat0 | eliminated | 4.4 | 0.916 | 0.000 | 1900/2660 |
| flat_uneven/1750/skai.stormcaller/water.reef_runner/r15000 | seat1 | eliminated | 7.1 | 0.000 | 0.446 | 1710/2625 |
| flat_uneven/1750/water.reef_runner/skai.stormcaller/r15000 | seat1 | eliminated | 6.8 | 0.000 | 0.767 | 1725/2660 |
| flat_uneven/1750/skai.stormcaller/water.shellback/r15000 | seat1 | eliminated | 12.5 | 0.000 | 1.000 | 1710/2625 |
| flat_uneven/1750/water.shellback/skai.stormcaller/r15000 | seat0 | eliminated | 20.3 | 0.752 | 0.000 | 1750/2660 |
| flat_uneven/1750/skai.stormcaller/water.spoutling/r15000 | seat0 | eliminated | 12.0 | 0.481 | 0.000 | 1710/2625 |
| flat_uneven/1750/water.spoutling/skai.stormcaller/r15000 | seat1 | eliminated | 5.4 | 0.000 | 0.929 | 1750/2660 |
| flat_uneven/1750/skai.stormcaller/skai_pressure_mix/r15000 | seat1 | eliminated | 6.9 | 0.000 | 0.541 | 1710/2750 |
| flat_uneven/1750/skai_pressure_mix/skai.stormcaller/r15000 | seat1 | eliminated | 9.1 | 0.000 | 0.743 | 1650/2660 |
| flat_uneven/1750/skai.stormcaller/skai_expand_mix/r15000 | seat1 | eliminated | 8.0 | 0.000 | 0.647 | 1710/2600 |
| flat_uneven/1750/skai_expand_mix/skai.stormcaller/r15000 | seat0 | eliminated | 31.1 | 0.308 | 0.000 | 1950/2660 |
| flat_uneven/1750/skai.stormcaller/skai_air_mix/r15000 | seat1 | eliminated | 5.7 | 0.000 | 0.920 | 1710/2820 |
| flat_uneven/1750/skai_air_mix/skai.stormcaller/r15000 | seat0 | eliminated | 12.5 | 0.920 | 0.000 | 1880/2660 |
| flat_uneven/1750/skai.stormcaller/water_pressure_mix/r15000 | seat1 | eliminated | 8.6 | 0.000 | 0.496 | 1710/2700 |
| flat_uneven/1750/water_pressure_mix/skai.stormcaller/r15000 | seat1 | eliminated | 12.0 | 0.000 | 0.567 | 2025/2660 |
| flat_uneven/1750/skai.stormcaller/water_expand_mix/r15000 | seat1 | eliminated | 10.2 | 0.000 | 0.854 | 1710/2595 |
| flat_uneven/1750/water_expand_mix/skai.stormcaller/r15000 | seat1 | eliminated | 22.1 | 0.000 | 0.121 | 1730/2660 |
| flat_uneven/1750/skai.stormcaller/water_leviathan_mix/r15000 | seat1 | eliminated | 5.3 | 0.000 | 0.966 | 1710/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.stormcaller/r15000 | seat0 | eliminated | 14.2 | 0.343 | 0.000 | 1235/2660 |
| flat_uneven/1750/skai.talon_archer/skai.thunderhead/r15000 | seat1 | eliminated | 6.6 | 0.000 | 0.849 | 1800/2650 |
| flat_uneven/1750/skai.thunderhead/skai.talon_archer/r15000 | seat0 | eliminated | 11.9 | 0.436 | 0.000 | 1855/2600 |
| flat_uneven/1750/skai.talon_archer/skai.windrunner/r15000 | seat1 | eliminated | 10.1 | 0.000 | 0.707 | 1800/2625 |
| flat_uneven/1750/skai.windrunner/skai.talon_archer/r15000 | seat1 | eliminated | 10.8 | 0.000 | 0.663 | 1725/2600 |
| flat_uneven/1750/skai.talon_archer/water.grasper/r15000 | seat1 | eliminated | 10.5 | 0.000 | 0.776 | 1800/2600 |
| flat_uneven/1750/water.grasper/skai.talon_archer/r15000 | seat1 | eliminated | 21.3 | 0.000 | 0.359 | 1800/2600 |
| flat_uneven/1750/skai.talon_archer/water.ink_lobber/r15000 | seat1 | eliminated | 8.0 | 0.000 | 0.729 | 1800/2660 |
| flat_uneven/1750/water.ink_lobber/skai.talon_archer/r15000 | seat1 | eliminated | 8.3 | 0.000 | 0.467 | 1710/2600 |
| flat_uneven/1750/skai.talon_archer/water.leviathan/r15000 | seat1 | eliminated | 3.2 | 0.000 | 0.997 | 1800/2660 |
| flat_uneven/1750/water.leviathan/skai.talon_archer/r15000 | seat0 | eliminated | 4.7 | 0.934 | 0.000 | 1900/2600 |
| flat_uneven/1750/skai.talon_archer/water.reef_runner/r15000 | seat1 | eliminated | 10.5 | 0.000 | 0.712 | 1800/2625 |
| flat_uneven/1750/water.reef_runner/skai.talon_archer/r15000 | seat1 | eliminated | 11.2 | 0.000 | 0.674 | 1725/2600 |
| flat_uneven/1750/skai.talon_archer/water.shellback/r15000 | seat1 | eliminated | 17.7 | 0.000 | 0.980 | 1800/2625 |
| flat_uneven/1750/water.shellback/skai.talon_archer/r15000 | seat0 | eliminated | 31.6 | 0.781 | 0.000 | 1750/2600 |
| flat_uneven/1750/skai.talon_archer/water.spoutling/r15000 | seat0 | eliminated | 15.7 | 0.568 | 0.000 | 1800/2625 |
| flat_uneven/1750/water.spoutling/skai.talon_archer/r15000 | seat1 | eliminated | 7.5 | 0.000 | 0.910 | 1750/2600 |
| flat_uneven/1750/skai.talon_archer/skai_pressure_mix/r15000 | seat1 | eliminated | 10.2 | 0.000 | 0.731 | 1800/2750 |
| flat_uneven/1750/skai_pressure_mix/skai.talon_archer/r15000 | seat1 | eliminated | 10.6 | 0.000 | 0.735 | 1650/2600 |
| flat_uneven/1750/skai.talon_archer/skai_expand_mix/r15000 | seat1 | eliminated | 12.8 | 0.000 | 0.649 | 1800/2600 |
| flat_uneven/1750/skai_expand_mix/skai.talon_archer/r15000 | seat1 | eliminated | 13.3 | 0.000 | 0.627 | 1950/2600 |
| flat_uneven/1750/skai.talon_archer/skai_air_mix/r15000 | seat1 | eliminated | 8.5 | 0.000 | 0.780 | 1800/2820 |
| flat_uneven/1750/skai_air_mix/skai.talon_archer/r15000 | seat1 | eliminated | 14.3 | 0.000 | 0.322 | 1880/2600 |
| flat_uneven/1750/skai.talon_archer/water_pressure_mix/r15000 | seat1 | eliminated | 10.3 | 0.000 | 0.781 | 1800/2700 |
| flat_uneven/1750/water_pressure_mix/skai.talon_archer/r15000 | seat0 | eliminated | 21.6 | 0.291 | 0.000 | 2025/2600 |
| flat_uneven/1750/skai.talon_archer/water_expand_mix/r15000 | seat1 | eliminated | 13.2 | 0.000 | 0.940 | 1800/2595 |
| flat_uneven/1750/water_expand_mix/skai.talon_archer/r15000 | seat0 | eliminated | 31.8 | 0.423 | 0.000 | 1730/2600 |
| flat_uneven/1750/skai.talon_archer/water_leviathan_mix/r15000 | seat1 | eliminated | 7.4 | 0.000 | 0.998 | 1800/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.talon_archer/r15000 | seat1 | eliminated | 15.1 | 0.000 | 0.047 | 1235/2600 |
| flat_uneven/1750/skai.thunderhead/skai.windrunner/r15000 | seat0 | eliminated | 9.1 | 0.494 | 0.000 | 1855/2625 |
| flat_uneven/1750/skai.windrunner/skai.thunderhead/r15000 | seat1 | eliminated | 4.1 | 0.000 | 0.918 | 1725/2650 |
| flat_uneven/1750/skai.thunderhead/water.grasper/r15000 | seat0 | eliminated | 11.5 | 1.000 | 0.000 | 1855/2600 |
| flat_uneven/1750/water.grasper/skai.thunderhead/r15000 | seat1 | eliminated | 6.5 | 0.000 | 1.000 | 1800/2650 |
| flat_uneven/1750/skai.thunderhead/water.ink_lobber/r15000 | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1855/2660 |
| flat_uneven/1750/water.ink_lobber/skai.thunderhead/r15000 | seat1 | eliminated | 4.2 | 0.000 | 1.000 | 1710/2650 |
| flat_uneven/1750/skai.thunderhead/water.leviathan/r15000 | seat1 | eliminated | 4.1 | 0.000 | 0.896 | 1855/2660 |
| flat_uneven/1750/water.leviathan/skai.thunderhead/r15000 | seat0 | eliminated | 7.3 | 0.350 | 0.000 | 1900/2650 |
| flat_uneven/1750/skai.thunderhead/water.reef_runner/r15000 | seat0 | eliminated | 6.5 | 1.000 | 0.000 | 1855/2625 |
| flat_uneven/1750/water.reef_runner/skai.thunderhead/r15000 | seat1 | eliminated | 3.9 | 0.000 | 1.000 | 1725/2650 |
| flat_uneven/1750/skai.thunderhead/water.shellback/r15000 | stalemate | dome_absorbed | 69.2 | 1.000 | 0.753 | 1855/2625 |
| flat_uneven/1750/water.shellback/skai.thunderhead/r15000 | seat1 | eliminated | 15.1 | 0.000 | 1.000 | 1750/2650 |
| flat_uneven/1750/skai.thunderhead/water.spoutling/r15000 | seat0 | eliminated | 6.6 | 0.816 | 0.000 | 1855/2625 |
| flat_uneven/1750/water.spoutling/skai.thunderhead/r15000 | seat1 | eliminated | 4.5 | 0.000 | 0.942 | 1750/2650 |
| flat_uneven/1750/skai.thunderhead/skai_pressure_mix/r15000 | seat0 | eliminated | 19.0 | 0.284 | 0.000 | 1855/2750 |
| flat_uneven/1750/skai_pressure_mix/skai.thunderhead/r15000 | seat1 | eliminated | 4.9 | 0.000 | 0.914 | 1650/2650 |
| flat_uneven/1750/skai.thunderhead/skai_expand_mix/r15000 | seat1 | eliminated | 27.8 | 0.000 | 0.308 | 1855/2600 |
| flat_uneven/1750/skai_expand_mix/skai.thunderhead/r15000 | seat0 | eliminated | 69.7 | 0.308 | 0.000 | 1950/2650 |
| flat_uneven/1750/skai.thunderhead/skai_air_mix/r15000 | seat1 | eliminated | 18.6 | 0.000 | 0.920 | 1855/2820 |
| flat_uneven/1750/skai_air_mix/skai.thunderhead/r15000 | seat0 | eliminated | 39.7 | 0.920 | 0.000 | 1880/2650 |
| flat_uneven/1750/skai.thunderhead/water_pressure_mix/r15000 | seat0 | eliminated | 9.1 | 1.000 | 0.000 | 1855/2700 |
| flat_uneven/1750/water_pressure_mix/skai.thunderhead/r15000 | seat1 | eliminated | 6.5 | 0.000 | 1.000 | 2025/2650 |
| flat_uneven/1750/skai.thunderhead/water_expand_mix/r15000 | seat1 | eliminated | 20.1 | 0.000 | 0.573 | 1855/2595 |
| flat_uneven/1750/water_expand_mix/skai.thunderhead/r15000 | seat1 | eliminated | 14.0 | 0.000 | 0.713 | 1730/2650 |
| flat_uneven/1750/skai.thunderhead/water_leviathan_mix/r15000 | seat1 | eliminated | 13.1 | 0.000 | 0.899 | 1855/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.thunderhead/r15000 | seat1 | eliminated | 6.9 | 0.000 | 0.631 | 1235/2650 |
| flat_uneven/1750/skai.windrunner/water.grasper/r15000 | seat1 | eliminated | 8.4 | 0.000 | 0.811 | 1725/2600 |
| flat_uneven/1750/water.grasper/skai.windrunner/r15000 | seat1 | eliminated | 19.0 | 0.000 | 0.297 | 1800/2625 |
| flat_uneven/1750/skai.windrunner/water.ink_lobber/r15000 | seat1 | eliminated | 5.5 | 0.000 | 0.857 | 1725/2660 |
| flat_uneven/1750/water.ink_lobber/skai.windrunner/r15000 | seat1 | eliminated | 7.5 | 0.000 | 0.557 | 1710/2625 |
| flat_uneven/1750/skai.windrunner/water.leviathan/r15000 | seat1 | eliminated | 2.1 | 0.000 | 1.000 | 1725/2660 |
| flat_uneven/1750/water.leviathan/skai.windrunner/r15000 | seat0 | eliminated | 4.0 | 0.984 | 0.000 | 1900/2625 |
| flat_uneven/1750/skai.windrunner/water.reef_runner/r15000 | seat1 | eliminated | 8.9 | 0.000 | 0.697 | 1725/2625 |
| flat_uneven/1750/water.reef_runner/skai.windrunner/r15000 | seat1 | eliminated | 8.8 | 0.000 | 0.694 | 1725/2625 |
| flat_uneven/1750/skai.windrunner/water.shellback/r15000 | seat1 | eliminated | 14.9 | 0.000 | 0.970 | 1725/2625 |
| flat_uneven/1750/water.shellback/skai.windrunner/r15000 | seat0 | eliminated | 29.3 | 0.736 | 0.000 | 1750/2625 |
| flat_uneven/1750/skai.windrunner/water.spoutling/r15000 | seat0 | eliminated | 14.2 | 0.525 | 0.000 | 1725/2625 |
| flat_uneven/1750/water.spoutling/skai.windrunner/r15000 | seat1 | eliminated | 6.6 | 0.000 | 0.886 | 1750/2625 |
| flat_uneven/1750/skai.windrunner/skai_pressure_mix/r15000 | seat1 | eliminated | 8.5 | 0.000 | 0.696 | 1725/2750 |
| flat_uneven/1750/skai_pressure_mix/skai.windrunner/r15000 | seat1 | eliminated | 8.8 | 0.000 | 0.730 | 1650/2625 |
| flat_uneven/1750/skai.windrunner/skai_expand_mix/r15000 | seat1 | eliminated | 11.6 | 0.000 | 0.641 | 1725/2600 |
| flat_uneven/1750/skai_expand_mix/skai.windrunner/r15000 | seat1 | eliminated | 11.0 | 0.000 | 0.669 | 1950/2625 |
| flat_uneven/1750/skai.windrunner/skai_air_mix/r15000 | seat1 | eliminated | 7.1 | 0.000 | 0.832 | 1725/2820 |
| flat_uneven/1750/skai_air_mix/skai.windrunner/r15000 | seat1 | eliminated | 11.7 | 0.000 | 0.340 | 1880/2625 |
| flat_uneven/1750/skai.windrunner/water_pressure_mix/r15000 | seat1 | eliminated | 8.5 | 0.000 | 0.818 | 1725/2700 |
| flat_uneven/1750/water_pressure_mix/skai.windrunner/r15000 | seat0 | eliminated | 22.3 | 0.197 | 0.000 | 2025/2625 |
| flat_uneven/1750/skai.windrunner/water_expand_mix/r15000 | seat1 | eliminated | 9.5 | 0.000 | 0.994 | 1725/2595 |
| flat_uneven/1750/water_expand_mix/skai.windrunner/r15000 | seat1 | eliminated | 27.9 | 0.000 | 0.053 | 1730/2625 |
| flat_uneven/1750/skai.windrunner/water_leviathan_mix/r15000 | seat1 | eliminated | 4.7 | 0.000 | 1.000 | 1725/2470 |
| flat_uneven/1750/water_leviathan_mix/skai.windrunner/r15000 | seat0 | eliminated | 13.4 | 0.207 | 0.000 | 1235/2625 |
| flat_uneven/1750/water.grasper/water.ink_lobber/r15000 | seat1 | eliminated | 8.3 | 0.000 | 0.806 | 1800/2660 |
| flat_uneven/1750/water.ink_lobber/water.grasper/r15000 | seat1 | eliminated | 11.2 | 0.000 | 0.228 | 1710/2600 |
| flat_uneven/1750/water.grasper/water.leviathan/r15000 | seat1 | eliminated | 2.6 | 0.000 | 1.000 | 1800/2660 |
| flat_uneven/1750/water.leviathan/water.grasper/r15000 | seat0 | eliminated | 5.1 | 1.000 | 0.000 | 1900/2600 |
| flat_uneven/1750/water.grasper/water.reef_runner/r15000 | seat1 | eliminated | 20.9 | 0.000 | 0.223 | 1800/2625 |
| flat_uneven/1750/water.reef_runner/water.grasper/r15000 | seat1 | eliminated | 8.7 | 0.000 | 0.813 | 1725/2600 |
| flat_uneven/1750/water.grasper/water.shellback/r15000 | seat1 | eliminated | 30.6 | 0.000 | 0.366 | 1800/2625 |
| flat_uneven/1750/water.shellback/water.grasper/r15000 | seat1 | eliminated | 15.4 | 0.000 | 0.804 | 1750/2600 |
| flat_uneven/1750/water.grasper/water.spoutling/r15000 | seat0 | eliminated | 12.9 | 0.631 | 0.000 | 1800/2625 |
| flat_uneven/1750/water.spoutling/water.grasper/r15000 | seat1 | eliminated | 8.0 | 0.000 | 0.895 | 1750/2600 |
| flat_uneven/1750/water.grasper/skai_pressure_mix/r15000 | seat1 | eliminated | 19.3 | 0.000 | 0.269 | 1800/2750 |
| flat_uneven/1750/skai_pressure_mix/water.grasper/r15000 | seat1 | eliminated | 8.7 | 0.000 | 0.820 | 1650/2600 |
| flat_uneven/1750/water.grasper/skai_expand_mix/r15000 | seat1 | eliminated | 37.6 | 0.000 | 0.308 | 1800/2600 |
| flat_uneven/1750/skai_expand_mix/water.grasper/r15000 | seat0 | eliminated | 107.6 | 0.308 | 0.000 | 1950/2600 |
| flat_uneven/1750/water.grasper/skai_air_mix/r15000 | seat1 | eliminated | 9.4 | 0.000 | 0.920 | 1800/2820 |
| flat_uneven/1750/skai_air_mix/water.grasper/r15000 | seat0 | eliminated | 26.6 | 0.920 | 0.000 | 1880/2600 |
| flat_uneven/1750/water.grasper/water_pressure_mix/r15000 | seat1 | eliminated | 13.7 | 0.000 | 0.680 | 1800/2700 |
| flat_uneven/1750/water_pressure_mix/water.grasper/r15000 | seat1 | eliminated | 14.9 | 0.000 | 0.582 | 2025/2600 |
| flat_uneven/1750/water.grasper/water_expand_mix/r15000 | seat1 | eliminated | 17.0 | 0.000 | 0.605 | 1800/2595 |
| flat_uneven/1750/water_expand_mix/water.grasper/r15000 | seat1 | eliminated | 12.3 | 0.000 | 0.563 | 1730/2600 |
| flat_uneven/1750/water.grasper/water_leviathan_mix/r15000 | seat1 | eliminated | 7.2 | 0.000 | 0.890 | 1800/2470 |
| flat_uneven/1750/water_leviathan_mix/water.grasper/r15000 | seat1 | eliminated | 11.2 | 0.000 | 0.140 | 1235/2600 |
| flat_uneven/1750/water.ink_lobber/water.leviathan/r15000 | seat1 | eliminated | 4.6 | 0.000 | 0.936 | 1710/2660 |
| flat_uneven/1750/water.leviathan/water.ink_lobber/r15000 | seat0 | eliminated | 5.1 | 0.820 | 0.000 | 1900/2660 |
| flat_uneven/1750/water.ink_lobber/water.reef_runner/r15000 | seat1 | eliminated | 8.3 | 0.000 | 0.411 | 1710/2625 |
| flat_uneven/1750/water.reef_runner/water.ink_lobber/r15000 | seat1 | eliminated | 5.1 | 0.000 | 0.864 | 1725/2660 |
| flat_uneven/1750/water.ink_lobber/water.shellback/r15000 | seat1 | eliminated | 13.7 | 0.000 | 0.967 | 1710/2625 |
| flat_uneven/1750/water.shellback/water.ink_lobber/r15000 | seat0 | eliminated | 24.9 | 0.602 | 0.000 | 1750/2660 |
| flat_uneven/1750/water.ink_lobber/water.spoutling/r15000 | seat0 | eliminated | 11.2 | 0.661 | 0.000 | 1710/2625 |
| flat_uneven/1750/water.spoutling/water.ink_lobber/r15000 | seat1 | eliminated | 5.1 | 0.000 | 0.954 | 1750/2660 |
| flat_uneven/1750/water.ink_lobber/skai_pressure_mix/r15000 | seat1 | eliminated | 7.4 | 0.000 | 0.525 | 1710/2750 |
| flat_uneven/1750/skai_pressure_mix/water.ink_lobber/r15000 | seat1 | eliminated | 7.7 | 0.000 | 0.860 | 1650/2660 |
| flat_uneven/1750/water.ink_lobber/skai_expand_mix/r15000 | seat1 | eliminated | 9.2 | 0.000 | 0.562 | 1710/2600 |
| flat_uneven/1750/skai_expand_mix/water.ink_lobber/r15000 | seat0 | eliminated | 44.8 | 0.308 | 0.000 | 1950/2660 |
| flat_uneven/1750/water.ink_lobber/skai_air_mix/r15000 | seat1 | eliminated | 6.6 | 0.000 | 0.920 | 1710/2820 |
| flat_uneven/1750/skai_air_mix/water.ink_lobber/r15000 | seat0 | eliminated | 12.8 | 0.920 | 0.000 | 1880/2660 |
| flat_uneven/1750/water.ink_lobber/water_pressure_mix/r15000 | seat1 | eliminated | 10.1 | 0.000 | 0.336 | 1710/2700 |
| flat_uneven/1750/water_pressure_mix/water.ink_lobber/r15000 | seat1 | eliminated | 10.9 | 0.000 | 0.743 | 2025/2660 |
| flat_uneven/1750/water.ink_lobber/water_expand_mix/r15000 | seat1 | eliminated | 11.9 | 0.000 | 0.802 | 1710/2595 |
| flat_uneven/1750/water_expand_mix/water.ink_lobber/r15000 | seat1 | eliminated | 14.7 | 0.000 | 0.513 | 1730/2660 |
| flat_uneven/1750/water.ink_lobber/water_leviathan_mix/r15000 | seat1 | eliminated | 7.3 | 0.000 | 0.901 | 1710/2470 |
| flat_uneven/1750/water_leviathan_mix/water.ink_lobber/r15000 | seat0 | eliminated | 17.8 | 0.031 | 0.000 | 1235/2660 |
| flat_uneven/1750/water.leviathan/water.reef_runner/r15000 | seat0 | eliminated | 4.0 | 0.986 | 0.000 | 1900/2625 |
| flat_uneven/1750/water.reef_runner/water.leviathan/r15000 | seat1 | eliminated | 2.2 | 0.000 | 1.000 | 1725/2660 |
| flat_uneven/1750/water.leviathan/water.shellback/r15000 | seat1 | eliminated | 23.2 | 0.000 | 0.870 | 1900/2625 |
| flat_uneven/1750/water.shellback/water.leviathan/r15000 | seat1 | eliminated | 16.4 | 0.000 | 0.790 | 1750/2660 |
| flat_uneven/1750/water.leviathan/water.spoutling/r15000 | seat0 | eliminated | 4.2 | 0.984 | 0.000 | 1900/2625 |
| flat_uneven/1750/water.spoutling/water.leviathan/r15000 | seat1 | eliminated | 2.4 | 0.000 | 1.000 | 1750/2660 |
| flat_uneven/1750/water.leviathan/skai_pressure_mix/r15000 | seat0 | eliminated | 5.6 | 0.945 | 0.000 | 1900/2750 |
| flat_uneven/1750/skai_pressure_mix/water.leviathan/r15000 | seat1 | eliminated | 4.1 | 0.000 | 0.983 | 1650/2660 |
| flat_uneven/1750/water.leviathan/skai_expand_mix/r15000 | seat0 | eliminated | 4.5 | 0.872 | 0.000 | 1900/2600 |
| flat_uneven/1750/skai_expand_mix/water.leviathan/r15000 | seat1 | eliminated | 4.0 | 0.000 | 0.956 | 1950/2660 |
| flat_uneven/1750/water.leviathan/skai_air_mix/r15000 | seat0 | eliminated | 4.3 | 0.755 | 0.000 | 1900/2820 |
| flat_uneven/1750/skai_air_mix/water.leviathan/r15000 | seat1 | eliminated | 4.0 | 0.000 | 0.947 | 1880/2660 |
| flat_uneven/1750/water.leviathan/water_pressure_mix/r15000 | seat0 | eliminated | 5.5 | 1.000 | 0.000 | 1900/2700 |
| flat_uneven/1750/water_pressure_mix/water.leviathan/r15000 | seat1 | eliminated | 3.2 | 0.000 | 1.000 | 2025/2660 |
| flat_uneven/1750/water.leviathan/water_expand_mix/r15000 | seat0 | eliminated | 16.8 | 0.526 | 0.000 | 1900/2595 |
| flat_uneven/1750/water_expand_mix/water.leviathan/r15000 | seat1 | eliminated | 7.5 | 0.000 | 0.927 | 1730/2660 |
| flat_uneven/1750/water.leviathan/water_leviathan_mix/r15000 | seat1 | eliminated | 15.0 | 0.000 | 0.446 | 1900/2470 |
| flat_uneven/1750/water_leviathan_mix/water.leviathan/r15000 | seat1 | eliminated | 8.3 | 0.000 | 0.714 | 1235/2660 |
| flat_uneven/1750/water.reef_runner/water.shellback/r15000 | seat1 | eliminated | 14.9 | 0.000 | 0.997 | 1725/2625 |
| flat_uneven/1750/water.shellback/water.reef_runner/r15000 | seat0 | eliminated | 29.1 | 0.722 | 0.000 | 1750/2625 |
| flat_uneven/1750/water.reef_runner/water.spoutling/r15000 | seat0 | eliminated | 15.3 | 0.464 | 0.000 | 1725/2625 |
| flat_uneven/1750/water.spoutling/water.reef_runner/r15000 | seat1 | eliminated | 7.1 | 0.000 | 0.883 | 1750/2625 |
| flat_uneven/1750/water.reef_runner/skai_pressure_mix/r15000 | seat1 | eliminated | 8.7 | 0.000 | 0.711 | 1725/2750 |
| flat_uneven/1750/skai_pressure_mix/water.reef_runner/r15000 | seat1 | eliminated | 8.9 | 0.000 | 0.730 | 1650/2625 |
| flat_uneven/1750/water.reef_runner/skai_expand_mix/r15000 | seat1 | eliminated | 12.1 | 0.000 | 0.638 | 1725/2600 |
| flat_uneven/1750/skai_expand_mix/water.reef_runner/r15000 | seat0 | eliminated | 59.0 | 0.308 | 0.000 | 1950/2625 |
| flat_uneven/1750/water.reef_runner/skai_air_mix/r15000 | seat1 | eliminated | 6.8 | 0.000 | 0.920 | 1725/2820 |
| flat_uneven/1750/skai_air_mix/water.reef_runner/r15000 | seat0 | eliminated | 14.5 | 0.920 | 0.000 | 1880/2625 |
| flat_uneven/1750/water.reef_runner/water_pressure_mix/r15000 | seat1 | eliminated | 8.7 | 0.000 | 0.818 | 1725/2700 |
| flat_uneven/1750/water_pressure_mix/water.reef_runner/r15000 | seat0 | eliminated | 19.3 | 0.280 | 0.000 | 2025/2625 |
| flat_uneven/1750/water.reef_runner/water_expand_mix/r15000 | seat1 | eliminated | 10.6 | 0.000 | 0.960 | 1725/2595 |
| flat_uneven/1750/water_expand_mix/water.reef_runner/r15000 | seat0 | eliminated | 29.0 | 0.127 | 0.000 | 1730/2625 |
| flat_uneven/1750/water.reef_runner/water_leviathan_mix/r15000 | seat1 | eliminated | 5.1 | 0.000 | 1.000 | 1725/2470 |
| flat_uneven/1750/water_leviathan_mix/water.reef_runner/r15000 | seat0 | eliminated | 13.4 | 0.234 | 0.000 | 1235/2625 |
| flat_uneven/1750/water.shellback/water.spoutling/r15000 | seat0 | eliminated | 20.3 | 0.984 | 0.000 | 1750/2625 |
| flat_uneven/1750/water.spoutling/water.shellback/r15000 | seat1 | eliminated | 13.1 | 0.000 | 0.994 | 1750/2625 |
| flat_uneven/1750/water.shellback/skai_pressure_mix/r15000 | seat0 | eliminated | 30.8 | 0.719 | 0.000 | 1750/2750 |
| flat_uneven/1750/skai_pressure_mix/water.shellback/r15000 | seat1 | eliminated | 14.8 | 0.000 | 0.980 | 1650/2625 |
| flat_uneven/1750/water.shellback/skai_expand_mix/r15000 | stalemate | dome_absorbed | 51.7 | 0.805 | 0.308 | 1750/2600 |
| flat_uneven/1750/skai_expand_mix/water.shellback/r15000 | stalemate | dome_absorbed | 43.9 | 0.308 | 0.965 | 1950/2625 |
| flat_uneven/1750/water.shellback/skai_air_mix/r15000 | seat1 | eliminated | 37.8 | 0.000 | 0.920 | 1750/2820 |
| flat_uneven/1750/skai_air_mix/water.shellback/r15000 | stalemate | dome_absorbed | 37.7 | 0.920 | 0.997 | 1880/2625 |
| flat_uneven/1750/water.shellback/water_pressure_mix/r15000 | seat1 | eliminated | 16.9 | 0.000 | 0.789 | 1750/2700 |
| flat_uneven/1750/water_pressure_mix/water.shellback/r15000 | seat1 | eliminated | 31.2 | 0.000 | 0.374 | 2025/2625 |
| flat_uneven/1750/water.shellback/water_expand_mix/r15000 | seat1 | eliminated | 37.8 | 0.000 | 0.907 | 1750/2595 |
| flat_uneven/1750/water_expand_mix/water.shellback/r15000 | seat1 | eliminated | 32.5 | 0.000 | 0.901 | 1730/2625 |
| flat_uneven/1750/water.shellback/water_leviathan_mix/r15000 | seat1 | eliminated | 32.2 | 0.000 | 0.755 | 1750/2470 |
| flat_uneven/1750/water_leviathan_mix/water.shellback/r15000 | seat1 | eliminated | 18.3 | 0.000 | 1.000 | 1235/2625 |
| flat_uneven/1750/water.spoutling/skai_pressure_mix/r15000 | seat1 | eliminated | 6.8 | 0.000 | 0.891 | 1750/2750 |
| flat_uneven/1750/skai_pressure_mix/water.spoutling/r15000 | seat0 | eliminated | 16.7 | 0.424 | 0.000 | 1650/2625 |
| flat_uneven/1750/water.spoutling/skai_expand_mix/r15000 | seat1 | eliminated | 7.4 | 0.000 | 0.885 | 1750/2600 |
| flat_uneven/1750/skai_expand_mix/water.spoutling/r15000 | seat0 | eliminated | 14.9 | 0.576 | 0.000 | 1950/2625 |
| flat_uneven/1750/water.spoutling/skai_air_mix/r15000 | seat1 | eliminated | 5.2 | 0.000 | 0.929 | 1750/2820 |
| flat_uneven/1750/skai_air_mix/water.spoutling/r15000 | seat0 | eliminated | 14.9 | 0.548 | 0.000 | 1880/2625 |
| flat_uneven/1750/water.spoutling/water_pressure_mix/r15000 | seat1 | eliminated | 7.7 | 0.000 | 0.901 | 1750/2700 |
| flat_uneven/1750/water_pressure_mix/water.spoutling/r15000 | seat0 | eliminated | 11.8 | 0.706 | 0.000 | 2025/2625 |
| flat_uneven/1750/water.spoutling/water_expand_mix/r15000 | seat1 | eliminated | 9.4 | 0.000 | 0.961 | 1750/2595 |
| flat_uneven/1750/water_expand_mix/water.spoutling/r15000 | seat0 | eliminated | 14.7 | 0.898 | 0.000 | 1730/2625 |
| flat_uneven/1750/water.spoutling/water_leviathan_mix/r15000 | seat1 | eliminated | 4.9 | 0.000 | 1.000 | 1750/2470 |
| flat_uneven/1750/water_leviathan_mix/water.spoutling/r15000 | seat0 | eliminated | 13.6 | 0.818 | 0.000 | 1235/2625 |
| flat_uneven/1750/skai_pressure_mix/skai_expand_mix/r15000 | seat1 | eliminated | 11.0 | 0.000 | 0.708 | 1650/2600 |
| flat_uneven/1750/skai_expand_mix/skai_pressure_mix/r15000 | seat1 | eliminated | 11.3 | 0.000 | 0.635 | 1950/2750 |
| flat_uneven/1750/skai_pressure_mix/skai_air_mix/r15000 | seat1 | eliminated | 7.1 | 0.000 | 0.852 | 1650/2820 |
| flat_uneven/1750/skai_air_mix/skai_pressure_mix/r15000 | seat1 | eliminated | 12.4 | 0.000 | 0.426 | 1880/2750 |
| flat_uneven/1750/skai_pressure_mix/water_pressure_mix/r15000 | seat1 | eliminated | 8.4 | 0.000 | 0.834 | 1650/2700 |
| flat_uneven/1750/water_pressure_mix/skai_pressure_mix/r15000 | seat0 | eliminated | 23.6 | 0.191 | 0.000 | 2025/2750 |
| flat_uneven/1750/skai_pressure_mix/water_expand_mix/r15000 | seat1 | eliminated | 10.8 | 0.000 | 1.000 | 1650/2595 |
| flat_uneven/1750/water_expand_mix/skai_pressure_mix/r15000 | seat1 | eliminated | 23.0 | 0.000 | 0.213 | 1730/2750 |
| flat_uneven/1750/skai_pressure_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 7.3 | 0.000 | 1.000 | 1650/2470 |
| flat_uneven/1750/water_leviathan_mix/skai_pressure_mix/r15000 | seat1 | eliminated | 16.2 | 0.000 | 0.133 | 1235/2750 |
| flat_uneven/1750/skai_expand_mix/skai_air_mix/r15000 | seat1 | eliminated | 9.9 | 0.000 | 0.743 | 1950/2820 |
| flat_uneven/1750/skai_air_mix/skai_expand_mix/r15000 | seat1 | eliminated | 16.3 | 0.000 | 0.272 | 1880/2600 |
| flat_uneven/1750/skai_expand_mix/water_pressure_mix/r15000 | seat0 | eliminated | 102.3 | 0.308 | 0.000 | 1950/2700 |
| flat_uneven/1750/water_pressure_mix/skai_expand_mix/r15000 | seat1 | eliminated | 44.9 | 0.000 | 0.308 | 2025/2600 |
| flat_uneven/1750/skai_expand_mix/water_expand_mix/r15000 | seat1 | eliminated | 13.6 | 0.000 | 0.963 | 1950/2595 |
| flat_uneven/1750/water_expand_mix/skai_expand_mix/r15000 | seat0 | eliminated | 28.1 | 0.541 | 0.000 | 1730/2600 |
| flat_uneven/1750/skai_expand_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 8.4 | 0.000 | 0.983 | 1950/2470 |
| flat_uneven/1750/water_leviathan_mix/skai_expand_mix/r15000 | seat1 | eliminated | 19.2 | 0.000 | 0.077 | 1235/2600 |
| flat_uneven/1750/skai_air_mix/water_pressure_mix/r15000 | seat0 | eliminated | 21.6 | 0.920 | 0.000 | 1880/2700 |
| flat_uneven/1750/water_pressure_mix/skai_air_mix/r15000 | seat1 | eliminated | 10.1 | 0.000 | 0.920 | 2025/2820 |
| flat_uneven/1750/skai_air_mix/water_expand_mix/r15000 | seat1 | eliminated | 19.3 | 0.000 | 0.737 | 1880/2595 |
| flat_uneven/1750/water_expand_mix/skai_air_mix/r15000 | seat1 | eliminated | 16.8 | 0.000 | 0.670 | 1730/2820 |
| flat_uneven/1750/skai_air_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 10.7 | 0.000 | 0.899 | 1880/2470 |
| flat_uneven/1750/water_leviathan_mix/skai_air_mix/r15000 | seat1 | eliminated | 11.3 | 0.000 | 0.313 | 1235/2820 |
| flat_uneven/1750/water_pressure_mix/water_expand_mix/r15000 | seat1 | eliminated | 17.7 | 0.000 | 0.701 | 2025/2595 |
| flat_uneven/1750/water_expand_mix/water_pressure_mix/r15000 | seat1 | eliminated | 13.1 | 0.000 | 0.560 | 1730/2700 |
| flat_uneven/1750/water_pressure_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 7.5 | 0.000 | 0.890 | 2025/2470 |
| flat_uneven/1750/water_leviathan_mix/water_pressure_mix/r15000 | seat1 | eliminated | 12.9 | 0.000 | 0.202 | 1235/2700 |
| flat_uneven/1750/water_expand_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 13.9 | 0.000 | 0.941 | 1730/2470 |
| flat_uneven/1750/water_leviathan_mix/water_expand_mix/r15000 | seat1 | eliminated | 12.9 | 0.000 | 0.968 | 1235/2595 |

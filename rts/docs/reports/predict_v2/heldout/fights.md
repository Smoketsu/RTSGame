<!-- fights.md: written by the balance harness --fights (rts/tools/BalanceHarness/FightCoordinator.cs); not a source file. -->
# Equal-value fights

- Data digest: tuning a020a25c7d7c2eae, content 9cb28f0929df518c.
- Base seed 20261121; fight seed = SeedDerivation.GameSeed(base seed, "fights", fight ID, 0).
- Value: cost_common + cost_rare per unit; an army is the recipe repeated k = max(1, (2V + rv) / (2 rv)) times.
- Control: plain attack-move at the enemy front rank, idle units re-target every 2 s; no strategic AI, no HQ.
- Fights: 1904 (17 sides); determinism spot check: 2 fights re-run in-process, 0 mismatches.

**FIGHTS fights=1904 decided=1869 draws=0 stalemates=34 unresolved=1 mismatched=271 checks_passed=2 of=8 dome_absorbed=34**

## Design-rule checks

| Check | Rule | Threshold | Result | Detail |
| --- | --- | --- | --- | --- |
| rule1_no_immunity | P-005 1.6 rule 1 | >= 2 mobile and >= 1 static answers per enemy unit or HQ; every armed unit hits Ground | PASS | 13 targets checked |
| rule2_multiplier_band | P-005 1.6 rule 2 | multipliers in [0.5, 1.5]; ratio within a damage type <= 2 | FAIL | drench/structure 0.25 below 0.5<br>drench ratio 4.00 above 2 |
| rule3_counter_margin | P-005 1.6 rule 3 | counter keeps >= 0.2 value, exchange ratio <= 3 | FAIL | skai.roc_rider loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 12.5 above 3<br>skai.roc_rider loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 12.5 above 3<br>skai.stormcaller loses to water.shellback on flat: water.shellback in seat 0 exchange ratio inf above 3<br>skai.stormcaller loses to water.shellback on flat: water.shellback in seat 1 exchange ratio inf above 3<br>skai.talon_archer loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 32.5 above 3<br>skai.talon_archer loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 43.9 above 3<br>skai.thunderhead loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>skai.thunderhead loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>skai.windrunner loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio inf above 3<br>skai.windrunner loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio inf above 3<br>water.grasper loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.grasper loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.ink_lobber loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.ink_lobber loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.leviathan loses to water.shellback on flat: water.shellback does not beat it (seat 0 win (left 0.31), seat 1 loss (left 0.00))<br>water.reef_runner loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.reef_runner loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.shellback loses to skai.thunderhead on flat: skai.thunderhead in seat 0 exchange ratio inf above 3<br>water.shellback loses to skai.thunderhead on flat: skai.thunderhead in seat 1 exchange ratio inf above 3<br>water.spoutling loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio inf above 3<br>water.spoutling loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio inf above 3 |
| rule4_no_dominant_unit | P-005 1.6 rule 4 | no unit beats all its faction mates on flat | PASS | no dominant unit |
| rule4_every_unit_wins | P-005 1.6 rule 4 | every unit beats >= 1 unit of another faction on flat | FAIL | water.spoutling beats no unit of another faction on flat |
| rule5_two_answers | P-005 1.6 rule 5 | >= 1 unit and >= 1 terrain answers per threat and faction | FAIL | air (skai.roc_rider) against skai: 2 unit answers (skai.talon_archer, skai.windrunner), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only) |
| rule6_hq_floor | P-005 1.6 rule 6 | Rooted HQ lives >= 15.0 s against 30 supply | FAIL | skai skai.stormcaller x10 vs skai.great_aerie: 11.0 s, needs >= 15.0 s<br>skai skai.stormcaller x10 vs water.grand_shell: 13.5 s, needs >= 15.0 s<br>skai skai.thunderhead x7 vs skai.great_aerie: 8.4 s, needs >= 15.0 s<br>skai skai.thunderhead x7 vs water.grand_shell: 10.4 s, needs >= 15.0 s<br>skai skai.windrunner x30 vs skai.great_aerie: 13.9 s, needs >= 15.0 s<br>water water.ink_lobber x15 vs skai.great_aerie: 6.6 s, needs >= 15.0 s<br>water water.ink_lobber x15 vs water.grand_shell: 7.6 s, needs >= 15.0 s<br>water water.leviathan x7 vs skai.great_aerie: 3.0 s, needs >= 15.0 s<br>water water.leviathan x7 vs water.grand_shell: 3.0 s, needs >= 15.0 s<br>water water.reef_runner x30 vs skai.great_aerie: 14.2 s, needs >= 15.0 s |
| matchups_agree | P-005 developer decision 1 (rule 7) | every unit has an entry; wins and loses match the fights | FAIL | water.leviathan: claims it loses to water.shellback on flat; measured seat 0 win (left 0.28), seat 1 loss (left 0.00)<br>water.spoutling: wins is null, but every armed unit must claim a fight it wins |

## Value exchange: flat, 1000

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.15 | +1.00 | -0.26 | +1.00 | +1.00 | -0.93 | +1.00 | S | +0.73 | -0.46 | – | +0.47 | – | +0.80* | – |
| skai.stormcaller | -1.00 | · | -0.39 | -1.00 | -0.29 | -0.32 | -0.32 | – | -0.23 | -1.00 | +0.69 | -0.43 | – | -0.92 | – | -0.97 | – |
| skai.talon_archer | +0.36 | +0.39 | · | -0.62 | +0.03* | -0.57 | +0.18 | -0.98 | -0.15 | -0.90 | +0.79 | -0.37 | – | -0.16 | – | -0.60 | – |
| skai.thunderhead | -1.00 | +1.00 | +0.67 | · | +0.80 | +1.00 | +1.00 | -0.78 | +1.00 | +1.00 | +0.87 | +0.61 | – | -0.92 | – | – | -0.72 |
| skai.windrunner | +0.28 | +0.32 | +0.09* | -0.80 | · | -0.62 | -0.19 | -1.00 | +0.10 | -0.87 | +0.78 | -0.30 | – | -0.31 | – | -0.44 | – |
| water.grasper | -1.00 | +0.18 | +0.58 | -1.00 | +0.63 | · | -0.03* | -1.00 | +0.62 | +0.50 | +0.82 | +0.53 | – | -0.92 | – | +0.34 | – |
| water.ink_lobber | -1.00 | +0.32 | -0.25 | -1.00 | +0.36 | -0.12* | · | – | +0.43 | -0.93 | +0.80 | -0.20 | – | -0.92 | – | -0.50 | – |
| water.leviathan | +0.93 | – | +0.97 | +0.78 | +1.00 | +1.00 | – | · | +1.00 | +0.28* | +1.00 | +0.97 | +0.93 | – | – | – | -0.39 |
| water.reef_runner | -1.00 | +0.30 | +0.11 | -1.00 | -0.14 | -0.62 | -0.49 | -1.00 | · | -0.91 | +0.75 | -0.38 | – | -0.92 | –* | -0.55 | – |
| water.shellback | S | +1.00 | +0.99 | -1.00 | +0.97 | -0.48 | +1.00 | +0.31* | +0.98 | · | +1.00 | +0.97 | – | S | – | – | +0.61 |
| water.spoutling | -0.69 | -0.70 | -0.82 | -0.86 | -0.78 | -0.81 | -0.79 | -1.00 | -0.76 | -0.99 | · | -0.80 | – | -0.75 | – | -0.96 | – |
| skai_pressure_mix | +0.51 | +0.44 | +0.38 | -0.61 | +0.37 | -0.54 | +0.22 | -0.98 | +0.33 | -0.90 | +0.81 | · | – | +0.15* | – | – | -0.90 |
| skai_expand_mix | – | – | – | – | – | – | – | -0.91 | – | – | – | – | · | – | – | –* | -0.78 |
| skai_air_mix | -0.32 | +0.92 | +0.24 | +0.92 | +0.27 | +0.92 | +0.92 | – | +0.92 | S | +0.71 | +0.07* | – | · | – | +0.73* | – |
| water_pressure_mix | – | – | – | – | – | – | – | – | –* | – | – | – | – | – | · | – | – |
| water_expand_mix | +0.23* | +0.75 | +0.66 | – | +0.68 | -0.59 | +0.69 | – | +0.75 | – | +0.93 | – | –* | +0.62* | – | · | – |
| water_leviathan_mix | – | – | – | +0.79 | – | – | – | +0.44 | – | -0.93 | – | +1.00 | +0.86 | – | – | – | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: flat, 3000

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.38 | +1.00 | -0.43 | +1.00 | +1.00 | -0.97 | +1.00 | S* | +0.71 | -0.20 | -0.47 | +0.55 | +1.00 | -0.63 | – |
| skai.stormcaller | -1.00 | · | +0.31 | -1.00 | -0.09* | +0.49 | -0.23 | -0.92 | +0.47 | -0.92 | +0.88 | +0.30 | -0.33 | -0.92 | +0.69 | -0.55 | – |
| skai.talon_archer | +0.39 | -0.28 | · | -0.69 | -0.22 | -0.59 | -0.59 | -0.94 | -0.15 | -0.92 | +0.81 | +0.28 | -0.24 | -0.45 | -0.48 | -0.77 | – |
| skai.thunderhead | -1.00 | +1.00 | +0.71 | · | +0.83 | +1.00 | +1.00 | -0.75 | +1.00 | S* | +0.89 | +0.83 | -0.31 | -0.92 | +1.00 | +0.72 | – |
| skai.windrunner | +0.49 | -0.16* | +0.24 | -0.81 | · | -0.60 | -0.59 | -1.00 | +0.09 | -0.93 | +0.79 | +0.39 | -0.27 | -0.45 | -0.48 | -0.71 | – |
| water.grasper | -1.00 | -0.48 | +0.57 | -1.00 | +0.60 | · | -0.70 | -1.00 | +0.60 | +0.54 | +0.81 | +0.67 | -0.31 | -0.92 | +0.40 | +0.29 | – |
| water.ink_lobber | -1.00 | +0.28 | +0.57 | -1.00 | +0.60 | +0.75 | · | -0.90 | +0.71 | -0.83 | +0.92 | +0.69 | -0.31 | -0.92 | +0.73 | -0.22 | – |
| water.leviathan | +0.97 | +0.92 | +0.98 | +0.84 | +1.00 | +1.00 | +0.89 | · | +0.99 | +0.46 | +1.00 | +0.98 | +0.94 | +0.90 | +1.00 | +0.93 | – |
| water.reef_runner | -1.00 | -0.46 | +0.09 | -1.00 | -0.15 | -0.62 | -0.67 | -1.00 | · | -0.93 | +0.77 | +0.40 | -0.31 | -0.92 | -0.53 | -0.78 | – |
| water.shellback | -1.00* | +0.97 | +0.90 | -1.00* | +0.91 | -0.59 | +0.86 | -0.57 | +0.91 | · | +1.00 | +0.96 | S | -0.92 | -0.35 | +0.68 | – |
| water.spoutling | -0.69 | -0.87 | -0.82 | -0.88 | -0.78 | -0.82 | -0.88 | -1.00 | -0.78 | -0.98 | · | -0.74 | -0.77 | -0.82 | -0.77 | -0.95 | – |
| skai_pressure_mix | +0.29 | -0.29 | -0.32 | -0.84 | -0.42 | -0.67 | -0.71 | -0.98 | -0.42 | -0.93 | +0.76 | · | – | -0.58 | -0.59 | -0.87 | -0.96 |
| skai_expand_mix | +0.42 | +0.40 | +0.21 | +0.31 | +0.08 | +0.31 | +0.31 | -0.94 | +0.31 | S | +0.78 | – | · | -0.15 | – | – | – |
| skai_air_mix | -0.42 | +0.92 | +0.37 | +0.92 | +0.52 | +0.92 | +0.92 | -0.96 | +0.92 | +0.92 | +0.79 | +0.59 | +0.12 | · | +0.92 | -0.34 | -0.58 |
| water_pressure_mix | -1.00 | -0.58 | +0.49 | -1.00 | +0.50 | -0.39 | -0.74 | -1.00 | +0.52 | +0.34 | +0.77 | +0.60 | – | -0.92 | · | +0.05 | -0.83 |
| water_expand_mix | +0.62 | +0.59 | +0.68 | -0.64 | +0.71 | -0.29 | +0.33 | -0.90 | +0.71 | -0.70 | +0.91 | +0.77 | – | +0.36 | -0.19 | · | -0.77 |
| water_leviathan_mix | – | – | – | – | – | – | – | – | – | – | – | +0.92 | – | +0.49 | +0.82 | +0.69 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: high_ground, 2500

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.16 | +1.00 | +0.81* | +1.00 | +1.00 | -0.96 | +1.00 | +1.00 | +0.76 | +0.64* | +0.19* | +0.62* | +1.00 | S* | -0.87 |
| skai.stormcaller | -1.00 | · | -0.63* | -1.00 | +0.67* | +1.00 | -0.63* | -1.00 | +0.67 | S* | +0.46 | -0.24 | -0.64 | -0.92 | +1.00* | -0.58 | -1.00 |
| skai.talon_archer | +0.34 | -0.06* | · | -0.61 | +0.81 | +1.00* | -0.61* | -1.00 | +0.81 | S* | +0.76 | +0.48 | -0.28* | -0.61 | +0.95* | -0.85 | -1.00 |
| skai.thunderhead | -1.00 | +1.00 | +0.53 | · | +0.87 | +1.00 | +1.00 | -0.79* | +1.00 | +1.00 | +0.88 | +0.74 | -0.31 | – | +1.00 | +0.87 | -0.50 |
| skai.windrunner | +0.38* | +0.07* | -0.30 | -0.76 | · | +1.00* | -0.68 | -1.00 | +0.31* | S* | +0.74* | +0.01* | -0.06 | -0.62 | +0.75* | -0.79 | -1.00 |
| water.grasper | -1.00 | -0.22 | +0.54* | -1.00 | +0.56* | · | -0.58 | -1.00 | +0.56* | +0.56* | +0.78* | +0.48* | -0.31 | -0.92 | +0.02* | +0.18* | -0.86 |
| water.ink_lobber | -1.00 | -0.60* | -0.69* | -1.00 | +0.74 | +1.00 | · | -1.00 | +0.74 | +1.00* | +0.48 | -0.15* | -0.67 | -0.92 | +1.00 | -0.57 | -1.00 |
| water.leviathan | +0.82 | +0.55 | +0.59 | -0.35* | +0.92 | +1.00 | +0.44 | · | +0.92 | -0.44* | +0.87 | +0.83 | +0.75 | +0.56 | +1.00 | +0.77 | -1.00* |
| water.reef_runner | -1.00 | -0.44 | -0.43 | -1.00 | +0.27* | +1.00* | -0.66 | -1.00 | · | S* | +0.72* | -0.26 | -0.31 | -0.92 | +0.74* | -0.80 | -1.00 |
| water.shellback | -1.00 | +1.00* | +0.93* | -1.00 | +0.96* | +0.45* | +0.86* | -0.71* | +0.96* | · | +1.00* | +0.92* | S | -0.92 | +0.46* | +0.66* | +0.83* |
| water.spoutling | -0.81 | -0.87 | -0.89 | -0.91 | +0.32* | +1.00* | -0.94 | -1.00 | +0.32* | S* | · | -0.76 | -0.86 | -0.90 | +0.90* | -0.96 | -1.00 |
| skai_pressure_mix | +0.50* | +0.10 | -0.11 | -0.65 | +0.45* | +1.00* | -0.52* | -1.00 | +0.45 | +0.79* | +0.78 | · | +0.23* | -0.52 | +0.78* | -0.75 | -1.00 |
| skai_expand_mix | +0.52* | +0.35 | -0.35* | +0.47 | +0.77 | +1.00 | +0.31 | -0.95 | +0.77 | S | +0.74 | +0.64* | · | +0.25* | +0.93 | -0.60 | -0.92 |
| skai_air_mix | +0.15* | +0.92 | +0.50 | – | +0.83 | +1.00 | +0.92 | -0.88 | +0.92 | +1.00 | +0.82 | +0.73 | +0.56* | · | +1.00 | +0.47 | -0.86 |
| water_pressure_mix | -1.00 | +0.28* | +0.60* | -1.00 | +0.59* | +0.45* | -0.35 | -1.00 | +0.60* | +0.61* | +0.80* | +0.54* | -0.31 | -0.92 | · | +0.14* | -0.89 |
| water_expand_mix | +0.72* | +0.62 | +0.66 | -0.78 | +0.65 | +0.74* | +0.58 | -1.00 | +0.65 | +0.78* | +0.86 | +0.63 | +0.65 | -0.70 | +0.76* | · | -0.76 |
| water_leviathan_mix | +0.75 | +0.88 | +0.71 | +0.32 | +0.92 | +0.99 | +0.56 | -1.00* | +0.92 | U* | +0.91 | +0.81 | +0.79 | +0.58 | +0.99 | +0.77 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal. The row is the attacker; the column holds the plateau.

## Value exchange: flooded, 2500

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.92* | +1.00 | -1.00* | +1.00 | +1.00 | -1.00 | -1.00* | -1.00 | -0.69* | -1.00* | -0.99* | -0.85* | +0.52 | -1.00 | -1.00 |
| skai.stormcaller | -1.00 | · | -0.80* | -1.00 | -0.84* | -0.85* | -0.85* | -1.00 | -0.88* | -1.00 | -0.16* | -0.84* | -0.88 | -0.96 | -0.90 | -0.98 | -1.00 |
| skai.talon_archer | -0.81* | -0.92* | · | -1.00 | -0.96* | -0.98 | -0.98* | -1.00 | -0.98* | -0.99 | +0.31 | -0.99* | -0.91* | -0.98* | -0.99 | -1.00 | -1.00 |
| skai.thunderhead | -1.00 | +1.00 | +0.19 | · | +0.33 | +1.00 | +1.00 | -1.00* | +0.18 | -0.98 | +0.68 | -0.18* | -0.38 | – | +0.86 | -0.61* | -0.74* |
| skai.windrunner | -0.66* | -0.73* | -0.73* | -0.87 | · | -0.85 | -0.90* | -1.00 | -0.75* | -1.00 | +0.61 | -0.77* | -0.73* | -0.86* | -0.86 | -1.00 | -1.00 |
| water.grasper | -1.00 | -0.05* | +0.63 | -1.00 | +0.66 | · | -0.43* | -1.00 | +0.61 | +0.57 | +0.84 | +0.60 | -0.31 | -0.92 | -0.31 | +0.18 | -0.75* |
| water.ink_lobber | -1.00 | -0.29* | -0.39* | -1.00 | -0.30* | -0.45* | · | -0.94 | -0.64* | -0.86 | +0.63 | -0.50* | -0.53 | -0.92 | -0.50* | -0.84* | -0.90 |
| water.leviathan | +0.72 | +0.69 | +0.70 | -0.39* | +0.64 | +0.63 | +0.60 | · | +0.65 | +0.22 | +0.84 | +0.57 | +0.64 | +0.42 | +0.51 | +0.41 | -0.80* |
| water.reef_runner | -0.53* | -0.73* | -0.68* | -0.92 | -0.71* | -0.86 | -0.85* | -1.00 | · | -1.00 | +0.63 | -0.75* | -0.72* | -0.85* | -0.87 | -1.00 | -1.00 |
| water.shellback | +0.93 | +0.98 | +0.93 | +0.83 | +0.93 | -0.72 | +0.79 | -0.56 | +0.86 | · | +1.00 | +0.91 | +0.91 | +0.91 | -0.74 | -0.90* | +0.78 |
| water.spoutling | -1.00* | -1.00* | -1.00 | -1.00 | -1.00 | -1.00 | -1.00 | -1.00 | -1.00 | -1.00 | · | -1.00 | -1.00* | -1.00 | -1.00 | -1.00 | -1.00 |
| skai_pressure_mix | -0.61* | -0.57* | -0.77* | -0.91* | -0.78* | -0.89 | -0.88* | -1.00 | -0.82* | -1.00 | +0.56 | · | -0.69* | -0.89* | -0.89 | -0.98 | -1.00 |
| skai_expand_mix | -0.89* | +0.31 | -0.90* | +0.31 | -0.97* | +0.31 | +0.31 | -1.00 | -0.99* | -1.00 | -0.22* | -0.98* | · | -0.97* | -0.93 | -1.00 | -1.00 |
| skai_air_mix | -0.68* | +0.92 | -0.51* | – | -0.61* | +0.92 | +0.92 | -1.00 | -0.71* | -0.92 | +0.57 | -0.65* | -0.64* | · | +0.70 | -0.84* | -0.70 |
| water_pressure_mix | -0.75 | +0.16 | +0.70 | -0.95 | +0.69 | +0.27 | -0.38* | -1.00 | +0.69 | +0.61 | +0.86 | +0.65 | +0.56 | -0.83 | · | -0.13* | -0.79* |
| water_expand_mix | +0.59 | +0.64 | +0.72 | -0.78* | +0.68 | -0.71 | -0.67* | -0.95 | +0.69 | -0.68* | +0.92 | +0.40 | +0.62 | -0.55* | -0.69* | · | -0.89* |
| water_leviathan_mix | +0.63 | +0.66 | +0.64 | -0.06* | +0.54 | -0.21* | +0.52 | -0.61* | +0.59 | -1.00 | +0.93 | +0.48 | +0.59 | +0.32 | -0.22* | -0.63* | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal. The row fights in the column's flood.

## Value exchange: choke, 1000

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | skai_pressure_mix | skai_expand_mix | skai_air_mix | water_pressure_mix | water_expand_mix | water_leviathan_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -0.24 | +1.00 | -0.26 | +1.00 | +1.00 | -0.52 | +1.00 | S | +0.74 | -0.48 | – | +0.47 | – | +0.80* | –* |
| skai.stormcaller | -1.00 | · | -0.39 | -1.00 | -0.38 | -0.31 | -0.32 | – | -0.22 | -1.00 | +0.68 | -0.45 | – | -0.92 | – | -0.85 | – |
| skai.talon_archer | +0.31 | +0.35 | · | -0.62 | -0.14 | -0.57 | +0.15 | -0.98 | +0.03 | -0.93 | +0.79 | -0.43 | – | -0.25 | – | -0.60 | – |
| skai.thunderhead | -1.00 | +1.00 | +0.67 | · | +0.78 | +1.00 | +1.00 | -0.78 | +1.00 | +1.00 | +0.87 | +0.67 | – | -0.92 | – | – | +0.15* |
| skai.windrunner | +0.28 | +0.33 | +0.15 | -0.80 | · | -0.61 | -0.26 | -1.00 | +0.07 | -0.88 | +0.80 | -0.30 | – | -0.21 | – | -0.44 | – |
| water.grasper | -1.00 | +0.18 | +0.58 | -1.00 | +0.62 | · | -0.03* | -1.00 | +0.61 | +0.50 | +0.82 | +0.53 | – | -0.92 | – | +0.40 | – |
| water.ink_lobber | -1.00 | +0.32 | -0.25 | -1.00 | +0.35 | -0.12* | · | – | +0.44 | -0.93 | +0.80 | -0.20 | – | -0.92 | – | -0.41 | – |
| water.leviathan | +0.52 | – | +0.97 | +0.38 | +1.00 | +1.00 | – | · | +1.00 | -0.44 | +1.00 | +0.97 | +0.93 | – | – | – | -0.39 |
| water.reef_runner | -1.00 | +0.21 | -0.07 | -1.00 | -0.05 | -0.61 | -0.43 | -1.00 | · | -0.92 | +0.76 | -0.34 | – | -0.92 | – | -0.45 | – |
| water.shellback | S | +1.00 | +0.97 | -1.00 | +0.97 | -0.46 | +1.00 | +0.33 | +0.99 | · | +1.00 | +0.97 | – | S | – | – | +0.86 |
| water.spoutling | -0.70 | -0.69 | -0.82 | -0.86 | -0.78 | -0.81 | -0.82 | -1.00 | -0.76 | -0.99 | · | -0.81 | – | -0.75 | – | -0.94 | – |
| skai_pressure_mix | +0.51 | +0.44 | +0.32 | -0.61 | +0.35 | -0.55 | +0.20 | -0.98 | +0.34 | -0.90 | +0.81 | · | – | -0.04 | – | – | -0.90 |
| skai_expand_mix | – | – | – | – | – | – | – | -0.91 | – | – | – | – | · | – | – | –* | -0.74 |
| skai_air_mix | -0.32 | +0.92 | +0.23 | +0.92 | +0.26 | +0.92 | +0.92 | – | +0.92 | S | +0.71 | +0.14 | – | · | – | +0.73* | – |
| water_pressure_mix | – | – | – | – | – | – | – | – | – | – | – | – | – | – | · | – | – |
| water_expand_mix | +0.23* | +0.79 | +0.66 | – | +0.68 | -0.59 | +0.69 | – | +0.75 | – | +0.93 | – | –* | +0.62* | – | · | – |
| water_leviathan_mix | –* | – | – | +0.79* | – | – | – | +0.44 | – | -0.89 | – | +1.00 | +0.86 | – | – | – | · |

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
| skai.stormcaller | beats water.spoutling on flat | measured seat 0 win (left 0.69), seat 1 win (left 0.70) | yes |
| skai.stormcaller | loses to water.shellback on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.talon_archer | beats water.spoutling on flat | measured seat 0 win (left 0.79), seat 1 win (left 0.82) | yes |
| skai.talon_archer | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.thunderhead | beats water.grasper on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| skai.thunderhead | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.windrunner | beats water.spoutling on flat | measured seat 0 win (left 0.78), seat 1 win (left 0.78) | yes |
| skai.windrunner | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.grasper | beats water.spoutling on flat | measured seat 0 win (left 0.82), seat 1 win (left 0.81) | yes |
| water.grasper | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.ink_lobber | beats water.spoutling on flat | measured seat 0 win (left 0.80), seat 1 win (left 0.79) | yes |
| water.ink_lobber | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.leviathan | beats skai.windrunner on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| water.leviathan | loses to water.shellback on flat | measured seat 0 win (left 0.28), seat 1 loss (left 0.00) | no |
| water.reef_runner | beats water.spoutling on flat | measured seat 0 win (left 0.75), seat 1 win (left 0.76) | yes |
| water.reef_runner | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.shellback | beats skai.stormcaller on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| water.shellback | loses to skai.thunderhead on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.spoutling | wins: null | - | no |
| water.spoutling | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |

## Suggested matchups entries

| Unit | Wins against | Loses to |
| --- | --- | --- |
| skai.roc_rider | water.grasper (left 1.00) | water.leviathan (their left 0.93) |
| skai.stormcaller | water.spoutling (left 0.69) | water.shellback (their left 1.00) |
| skai.talon_archer | water.spoutling (left 0.81) | water.leviathan (their left 0.98) |
| skai.thunderhead | water.grasper (left 1.00) | skai.roc_rider (their left 1.00) |
| skai.windrunner | water.spoutling (left 0.78) | water.leviathan (their left 1.00) |
| water.grasper | water.spoutling (left 0.82) | skai.roc_rider (their left 1.00) |
| water.ink_lobber | water.spoutling (left 0.80) | skai.roc_rider (their left 1.00) |
| water.leviathan | skai.windrunner (left 1.00) | null |
| water.reef_runner | water.spoutling (left 0.75) | skai.roc_rider (their left 1.00) |
| water.shellback | skai.stormcaller (left 1.00) | skai.thunderhead (their left 1.00) |
| water.spoutling | null | water.leviathan (their left 1.00) |

## Stalemates, unresolved fights and seat splits

A seat split means the two seat orders of a pair have different winners. On high_ground and flooded the seats are not symmetric by design, so splits there show terrain at work, not seat bias. A `no_damage` stalemate counts only damage events; when shots absorbed by a shield dome fell inside the idle window the stalemate is `dome_absorbed` instead.

| Fight | Finding | Reason |
| --- | --- | --- |
| flat/1000/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/1000/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/1000/skai.roc_rider/water_expand_mix | seat split (skai.roc_rider wins this order, water_expand_mix wins the swapped order) | eliminated |
| flat/1000/skai.talon_archer/skai.windrunner | seat split (skai.talon_archer wins this order, skai.windrunner wins the swapped order) | eliminated |
| flat/1000/water.grasper/water.ink_lobber | seat split (water.ink_lobber wins this order, water.grasper wins the swapped order) | eliminated |
| flat/1000/water.leviathan/water.shellback | seat split (water.leviathan wins this order, water.shellback wins the swapped order) | eliminated |
| flat/1000/water.shellback/skai_expand_mix | stalemate | dome_absorbed |
| flat/1000/skai_expand_mix/water.shellback | stalemate | dome_absorbed |
| flat/1000/water.shellback/skai_air_mix | stalemate | dome_absorbed |
| flat/1000/skai_air_mix/water.shellback | stalemate | dome_absorbed |
| flat/1000/skai_air_mix/skai_pressure_mix | seat split (skai_air_mix wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| flat/1000/skai_air_mix/water_expand_mix | seat split (skai_air_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| flat/3000/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/3000/skai.stormcaller/skai.windrunner | seat split (skai.windrunner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flat/3000/skai.thunderhead/water.shellback | stalemate | dome_absorbed |
| flat/3000/water.shellback/skai_expand_mix | stalemate | dome_absorbed |
| flat/3000/skai_expand_mix/water.shellback | stalemate | dome_absorbed |
| high_ground/2500/skai.roc_rider/skai.windrunner | seat split (skai.roc_rider wins this order, skai.windrunner wins the swapped order) | eliminated |
| high_ground/2500/skai.roc_rider/skai_pressure_mix | seat split (skai.roc_rider wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/skai.roc_rider/skai_expand_mix | seat split (skai.roc_rider wins this order, skai_expand_mix wins the swapped order) | eliminated |
| high_ground/2500/skai.roc_rider/skai_air_mix | seat split (skai.roc_rider wins this order, skai_air_mix wins the swapped order) | eliminated |
| high_ground/2500/skai.roc_rider/water_expand_mix | stalemate | dome_absorbed |
| high_ground/2500/skai.stormcaller/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.stormcaller wins the swapped order) | eliminated |
| high_ground/2500/skai.stormcaller/skai.windrunner | seat split (skai.stormcaller wins this order, skai.windrunner wins the swapped order) | eliminated |
| high_ground/2500/skai.stormcaller/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.stormcaller wins the swapped order) | eliminated |
| high_ground/2500/skai.stormcaller/water.shellback | stalemate | dome_absorbed |
| high_ground/2500/skai.stormcaller/water_pressure_mix | seat split (skai.stormcaller wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/skai.talon_archer/water.grasper | seat split (skai.talon_archer wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/2500/skai.talon_archer/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.talon_archer wins the swapped order) | eliminated |
| high_ground/2500/skai.talon_archer/water.shellback | stalemate | dome_absorbed |
| high_ground/2500/skai.talon_archer/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| high_ground/2500/skai.talon_archer/water_pressure_mix | seat split (skai.talon_archer wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/skai.thunderhead/water.leviathan | seat split (water.leviathan wins this order, skai.thunderhead wins the swapped order) | eliminated |
| high_ground/2500/skai.windrunner/water.grasper | seat split (skai.windrunner wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/2500/skai.windrunner/water.reef_runner | seat split (skai.windrunner wins this order, water.reef_runner wins the swapped order) | eliminated |
| high_ground/2500/skai.windrunner/water.shellback | stalemate | dome_absorbed |
| high_ground/2500/skai.windrunner/water.spoutling | seat split (skai.windrunner wins this order, water.spoutling wins the swapped order) | eliminated |
| high_ground/2500/skai.windrunner/skai_pressure_mix | seat split (skai.windrunner wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/skai.windrunner/water_pressure_mix | seat split (skai.windrunner wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/water.grasper/water.reef_runner | seat split (water.grasper wins this order, water.reef_runner wins the swapped order) | eliminated |
| high_ground/2500/water.grasper/water.shellback | seat split (water.grasper wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/2500/water.grasper/water.spoutling | seat split (water.grasper wins this order, water.spoutling wins the swapped order) | eliminated |
| high_ground/2500/skai_pressure_mix/water.grasper | seat split (skai_pressure_mix wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/2500/water.grasper/water_pressure_mix | seat split (water.grasper wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/water.grasper/water_expand_mix | seat split (water.grasper wins this order, water_expand_mix wins the swapped order) | eliminated |
| high_ground/2500/water.ink_lobber/water.shellback | seat split (water.ink_lobber wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/2500/skai_pressure_mix/water.ink_lobber | seat split (water.ink_lobber wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/water.leviathan/water.shellback | seat split (water.shellback wins this order, water.leviathan wins the swapped order) | eliminated |
| high_ground/2500/water.leviathan/water_leviathan_mix | seat split (water_leviathan_mix wins this order, water.leviathan wins the swapped order) | eliminated |
| high_ground/2500/water.reef_runner/water.shellback | stalemate | dome_absorbed |
| high_ground/2500/water.reef_runner/water.spoutling | seat split (water.reef_runner wins this order, water.spoutling wins the swapped order) | eliminated |
| high_ground/2500/water.reef_runner/water_pressure_mix | seat split (water.reef_runner wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/water.spoutling/water.shellback | stalemate | dome_absorbed |
| high_ground/2500/skai_pressure_mix/water.shellback | seat split (skai_pressure_mix wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/2500/water.shellback/skai_expand_mix | stalemate | dome_absorbed |
| high_ground/2500/skai_expand_mix/water.shellback | stalemate | dome_absorbed |
| high_ground/2500/water.shellback/water_pressure_mix | seat split (water.shellback wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/water.shellback/water_expand_mix | seat split (water.shellback wins this order, water_expand_mix wins the swapped order) | eliminated |
| high_ground/2500/water_leviathan_mix/water.shellback | unresolved | time_cap |
| high_ground/2500/water.spoutling/water_pressure_mix | seat split (water.spoutling wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/skai_expand_mix/skai_pressure_mix | seat split (skai_expand_mix wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/skai_pressure_mix/water_pressure_mix | seat split (skai_pressure_mix wins this order, water_pressure_mix wins the swapped order) | eliminated |
| high_ground/2500/skai_air_mix/skai_expand_mix | seat split (skai_air_mix wins this order, skai_expand_mix wins the swapped order) | eliminated |
| high_ground/2500/water_expand_mix/water_pressure_mix | seat split (water_expand_mix wins this order, water_pressure_mix wins the swapped order) | eliminated |
| flooded/2500/skai.roc_rider/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2500/skai.roc_rider/skai.windrunner | seat split (skai.windrunner wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2500/skai.roc_rider/water.reef_runner | seat split (water.reef_runner wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2500/skai.roc_rider/water.spoutling | seat split (water.spoutling wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2500/skai.roc_rider/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2500/skai.roc_rider/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2500/skai.roc_rider/skai_air_mix | seat split (skai_air_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/2500/skai.stormcaller/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2500/skai.stormcaller/skai.windrunner | seat split (skai.windrunner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2500/skai.stormcaller/water.grasper | seat split (water.grasper wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2500/skai.stormcaller/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2500/skai.stormcaller/water.reef_runner | seat split (water.reef_runner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2500/skai.stormcaller/water.spoutling | seat split (water.spoutling wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2500/skai.stormcaller/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/2500/skai.talon_archer/skai.windrunner | seat split (skai.windrunner wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2500/skai.talon_archer/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2500/skai.talon_archer/water.reef_runner | seat split (water.reef_runner wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2500/skai.talon_archer/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2500/skai.talon_archer/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2500/skai.talon_archer/skai_air_mix | seat split (skai_air_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/2500/skai.thunderhead/water.leviathan | seat split (water.leviathan wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/2500/skai.thunderhead/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/2500/skai.thunderhead/water_expand_mix | seat split (water_expand_mix wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/2500/skai.thunderhead/water_leviathan_mix | seat split (water_leviathan_mix wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/2500/skai.windrunner/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2500/skai.windrunner/water.reef_runner | seat split (water.reef_runner wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2500/skai.windrunner/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2500/skai.windrunner/skai_expand_mix | seat split (skai_expand_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2500/skai.windrunner/skai_air_mix | seat split (skai_air_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/2500/water.grasper/water.ink_lobber | seat split (water.ink_lobber wins this order, water.grasper wins the swapped order) | eliminated |
| flooded/2500/water.grasper/water_leviathan_mix | seat split (water_leviathan_mix wins this order, water.grasper wins the swapped order) | eliminated |
| flooded/2500/water.ink_lobber/water.reef_runner | seat split (water.reef_runner wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/2500/skai_pressure_mix/water.ink_lobber | seat split (water.ink_lobber wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| flooded/2500/water.ink_lobber/water_pressure_mix | seat split (water_pressure_mix wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/2500/water.ink_lobber/water_expand_mix | seat split (water_expand_mix wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/2500/water.leviathan/water_leviathan_mix | seat split (water_leviathan_mix wins this order, water.leviathan wins the swapped order) | eliminated |
| flooded/2500/skai_pressure_mix/water.reef_runner | seat split (water.reef_runner wins this order, skai_pressure_mix wins the swapped order) | eliminated |
| flooded/2500/skai_expand_mix/water.reef_runner | seat split (water.reef_runner wins this order, skai_expand_mix wins the swapped order) | eliminated |
| flooded/2500/skai_air_mix/water.reef_runner | seat split (water.reef_runner wins this order, skai_air_mix wins the swapped order) | eliminated |
| flooded/2500/water.shellback/water_expand_mix | seat split (water_expand_mix wins this order, water.shellback wins the swapped order) | eliminated |
| flooded/2500/skai_expand_mix/water.spoutling | seat split (water.spoutling wins this order, skai_expand_mix wins the swapped order) | eliminated |
| flooded/2500/skai_expand_mix/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai_expand_mix wins the swapped order) | eliminated |
| flooded/2500/skai_air_mix/skai_pressure_mix | seat split (skai_pressure_mix wins this order, skai_air_mix wins the swapped order) | eliminated |
| flooded/2500/skai_air_mix/skai_expand_mix | seat split (skai_expand_mix wins this order, skai_air_mix wins the swapped order) | eliminated |
| flooded/2500/skai_air_mix/water_expand_mix | seat split (water_expand_mix wins this order, skai_air_mix wins the swapped order) | eliminated |
| flooded/2500/water_expand_mix/water_pressure_mix | seat split (water_pressure_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| flooded/2500/water_leviathan_mix/water_pressure_mix | seat split (water_pressure_mix wins this order, water_leviathan_mix wins the swapped order) | eliminated |
| flooded/2500/water_expand_mix/water_leviathan_mix | seat split (water_leviathan_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| choke/1000/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| choke/1000/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| choke/1000/skai.roc_rider/water_expand_mix | seat split (skai.roc_rider wins this order, water_expand_mix wins the swapped order) | eliminated |
| choke/1000/skai.roc_rider/water_leviathan_mix | stalemate | dome_absorbed |
| choke/1000/skai.thunderhead/water_leviathan_mix | seat split (skai.thunderhead wins this order, water_leviathan_mix wins the swapped order) | eliminated |
| choke/1000/water.grasper/water.ink_lobber | seat split (water.ink_lobber wins this order, water.grasper wins the swapped order) | eliminated |
| choke/1000/water.shellback/skai_expand_mix | stalemate | dome_absorbed |
| choke/1000/skai_expand_mix/water.shellback | stalemate | dome_absorbed |
| choke/1000/water.shellback/skai_air_mix | stalemate | dome_absorbed |
| choke/1000/skai_air_mix/water.shellback | stalemate | dome_absorbed |
| choke/1000/skai_air_mix/water_expand_mix | seat split (skai_air_mix wins this order, water_expand_mix wins the swapped order) | eliminated |
| flat_uneven/2000/skai.roc_rider/water.shellback/r12500 | stalemate | dome_absorbed |
| flat_uneven/2000/water.shellback/skai_expand_mix/r12500 | stalemate | dome_absorbed |
| flat_uneven/2000/skai_expand_mix/water.shellback/r12500 | stalemate | dome_absorbed |
| flat_uneven/2000/skai_air_mix/water.shellback/r12500 | stalemate | dome_absorbed |
| flat_uneven/2000/skai.roc_rider/water.shellback/r15000 | stalemate | dome_absorbed |
| flat_uneven/2000/skai.thunderhead/water.shellback/r15000 | stalemate | dome_absorbed |
| flat_uneven/2000/water.shellback/skai_expand_mix/r15000 | stalemate | dome_absorbed |
| flat_uneven/2000/skai_expand_mix/water.shellback/r15000 | stalemate | dome_absorbed |
| flat_uneven/2000/skai_air_mix/water.shellback/r15000 | stalemate | dome_absorbed |

## All fights

| Fight | Result | Reason | Seconds | Left 0 | Left 1 | Values |
| --- | --- | --- | ---: | ---: | ---: | --- |
| flat/1000/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 11.3 | 1.000 | 0.000 | 1000/950 |
| flat/1000/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 11.3 | 0.000 | 1.000 | 950/1000 |
| flat/1000/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 25.7 | 0.000 | 0.153 | 1000/1000 |
| flat/1000/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 19.7 | 0.360 | 0.000 | 1000/1000 |
| flat/1000/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 19.4 | 1.000 | 0.000 | 1000/1060 |
| flat/1000/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 19.4 | 0.000 | 1.000 | 1060/1000 |
| flat/1000/skai.roc_rider/skai.windrunner | seat1 | eliminated | 19.7 | 0.000 | 0.259 | 1000/975 |
| flat/1000/skai.windrunner/skai.roc_rider | seat0 | eliminated | 19.9 | 0.277 | 0.000 | 975/1000 |
| flat/1000/skai.roc_rider/water.grasper | seat0 | eliminated | 30.5 | 1.000 | 0.000 | 1000/1000 |
| flat/1000/water.grasper/skai.roc_rider | seat1 | eliminated | 28.1 | 0.000 | 1.000 | 1000/1000 |
| flat/1000/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 12.7 | 1.000 | 0.000 | 1000/950 |
| flat/1000/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 12.7 | 0.000 | 1.000 | 950/1000 |
| flat/1000/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.930 | 1000/1140 |
| flat/1000/water.leviathan/skai.roc_rider | seat0 | eliminated | 4.1 | 0.930 | 0.000 | 1140/1000 |
| flat/1000/skai.roc_rider/water.reef_runner | seat0 | eliminated | 19.5 | 1.000 | 0.000 | 1000/975 |
| flat/1000/water.reef_runner/skai.roc_rider | seat1 | eliminated | 18.3 | 0.000 | 1.000 | 975/1000 |
| flat/1000/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 76.3 | 1.000 | 0.833 | 1000/1050 |
| flat/1000/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 36.8 | 0.960 | 1.000 | 1050/1000 |
| flat/1000/skai.roc_rider/water.spoutling | seat0 | eliminated | 11.4 | 0.725 | 0.000 | 1000/1000 |
| flat/1000/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.6 | 0.000 | 0.688 | 1000/1000 |
| flat/1000/skai.roc_rider/skai_pressure_mix | seat1 | eliminated | 15.6 | 0.000 | 0.464 | 1000/1100 |
| flat/1000/skai_pressure_mix/skai.roc_rider | seat0 | eliminated | 15.7 | 0.507 | 0.000 | 1100/1000 |
| flat/1000/skai.roc_rider/skai_expand_mix | seat1 | eliminated | 15.0 | 0.000 | 0.545 | 1000/1300 |
| flat/1000/skai_expand_mix/skai.roc_rider | seat0 | eliminated | 13.9 | 0.635 | 0.000 | 1300/1000 |
| flat/1000/skai.roc_rider/skai_air_mix | seat0 | eliminated | 23.0 | 0.469 | 0.000 | 1000/940 |
| flat/1000/skai_air_mix/skai.roc_rider | seat1 | eliminated | 24.2 | 0.000 | 0.321 | 940/1000 |
| flat/1000/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 19.8 | 1.000 | 0.000 | 1000/675 |
| flat/1000/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 20.8 | 0.000 | 1.000 | 675/1000 |
| flat/1000/skai.roc_rider/water_expand_mix | seat0 | eliminated | 50.8 | 0.800 | 0.000 | 1000/865 |
| flat/1000/water_expand_mix/skai.roc_rider | seat0 | eliminated | 43.9 | 0.230 | 0.000 | 865/1000 |
| flat/1000/skai.roc_rider/water_leviathan_mix | seat1 | eliminated | 13.2 | 0.000 | 0.878 | 1000/1235 |
| flat/1000/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 10.3 | 0.865 | 0.000 | 1235/1000 |
| flat/1000/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 9.4 | 0.000 | 0.390 | 950/1000 |
| flat/1000/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 8.8 | 0.391 | 0.000 | 1000/950 |
| flat/1000/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.8 | 0.000 | 1.000 | 950/1060 |
| flat/1000/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1060/950 |
| flat/1000/skai.stormcaller/skai.windrunner | seat1 | eliminated | 8.6 | 0.000 | 0.293 | 950/975 |
| flat/1000/skai.windrunner/skai.stormcaller | seat0 | eliminated | 8.1 | 0.320 | 0.000 | 975/950 |
| flat/1000/skai.stormcaller/water.grasper | seat1 | eliminated | 10.6 | 0.000 | 0.321 | 950/1000 |
| flat/1000/water.grasper/skai.stormcaller | seat0 | eliminated | 11.9 | 0.183 | 0.000 | 1000/950 |
| flat/1000/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.3 | 0.000 | 0.315 | 950/950 |
| flat/1000/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 9.3 | 0.315 | 0.000 | 950/950 |
| flat/1000/skai.stormcaller/water.leviathan | seat1 | eliminated | 2.5 | 0.000 | 0.944 | 950/1140 |
| flat/1000/water.leviathan/skai.stormcaller | seat0 | eliminated | 2.5 | 0.944 | 0.000 | 1140/950 |
| flat/1000/skai.stormcaller/water.reef_runner | seat1 | eliminated | 9.4 | 0.000 | 0.229 | 950/975 |
| flat/1000/water.reef_runner/skai.stormcaller | seat0 | eliminated | 9.0 | 0.296 | 0.000 | 975/950 |
| flat/1000/skai.stormcaller/water.shellback | seat1 | eliminated | 13.9 | 0.000 | 1.000 | 950/1050 |
| flat/1000/water.shellback/skai.stormcaller | seat0 | eliminated | 14.4 | 1.000 | 0.000 | 1050/950 |
| flat/1000/skai.stormcaller/water.spoutling | seat0 | eliminated | 9.4 | 0.689 | 0.000 | 950/1000 |
| flat/1000/water.spoutling/skai.stormcaller | seat1 | eliminated | 9.4 | 0.000 | 0.700 | 1000/950 |
| flat/1000/skai.stormcaller/skai_pressure_mix | seat1 | eliminated | 7.7 | 0.000 | 0.430 | 950/1100 |
| flat/1000/skai_pressure_mix/skai.stormcaller | seat0 | eliminated | 7.5 | 0.435 | 0.000 | 1100/950 |
| flat/1000/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 7.8 | 0.000 | 0.771 | 950/1300 |
| flat/1000/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 7.8 | 0.769 | 0.000 | 1300/950 |
| flat/1000/skai.stormcaller/skai_air_mix | seat1 | eliminated | 9.3 | 0.000 | 0.920 | 950/940 |
| flat/1000/skai_air_mix/skai.stormcaller | seat0 | eliminated | 9.1 | 0.920 | 0.000 | 940/950 |
| flat/1000/skai.stormcaller/water_pressure_mix | seat0 | eliminated | 9.6 | 0.547 | 0.000 | 950/675 |
| flat/1000/water_pressure_mix/skai.stormcaller | seat1 | eliminated | 11.8 | 0.000 | 0.360 | 675/950 |
| flat/1000/skai.stormcaller/water_expand_mix | seat1 | eliminated | 11.8 | 0.000 | 0.966 | 950/865 |
| flat/1000/water_expand_mix/skai.stormcaller | seat0 | eliminated | 13.2 | 0.754 | 0.000 | 865/950 |
| flat/1000/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 6.0 | 0.000 | 0.943 | 950/1235 |
| flat/1000/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 7.0 | 1.000 | 0.000 | 1235/950 |
| flat/1000/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 9.5 | 0.000 | 0.624 | 1000/1060 |
| flat/1000/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 9.4 | 0.671 | 0.000 | 1060/1000 |
| flat/1000/skai.talon_archer/skai.windrunner | seat0 | eliminated | 22.7 | 0.028 | 0.000 | 1000/975 |
| flat/1000/skai.windrunner/skai.talon_archer | seat0 | eliminated | 21.8 | 0.092 | 0.000 | 975/1000 |
| flat/1000/skai.talon_archer/water.grasper | seat1 | eliminated | 14.1 | 0.000 | 0.574 | 1000/1000 |
| flat/1000/water.grasper/skai.talon_archer | seat0 | eliminated | 13.9 | 0.580 | 0.000 | 1000/1000 |
| flat/1000/skai.talon_archer/water.ink_lobber | seat0 | eliminated | 10.4 | 0.180 | 0.000 | 1000/950 |
| flat/1000/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 10.8 | 0.000 | 0.245 | 950/1000 |
| flat/1000/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.980 | 1000/1140 |
| flat/1000/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.973 | 0.000 | 1140/1000 |
| flat/1000/skai.talon_archer/water.reef_runner | seat1 | eliminated | 20.3 | 0.000 | 0.154 | 1000/975 |
| flat/1000/water.reef_runner/skai.talon_archer | seat0 | eliminated | 20.9 | 0.108 | 0.000 | 975/1000 |
| flat/1000/skai.talon_archer/water.shellback | seat1 | eliminated | 22.5 | 0.000 | 0.903 | 1000/1050 |
| flat/1000/water.shellback/skai.talon_archer | seat0 | eliminated | 20.3 | 0.986 | 0.000 | 1050/1000 |
| flat/1000/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.8 | 0.794 | 0.000 | 1000/1000 |
| flat/1000/water.spoutling/skai.talon_archer | seat1 | eliminated | 9.8 | 0.000 | 0.817 | 1000/1000 |
| flat/1000/skai.talon_archer/skai_pressure_mix | seat1 | eliminated | 15.5 | 0.000 | 0.373 | 1000/1100 |
| flat/1000/skai_pressure_mix/skai.talon_archer | seat0 | eliminated | 15.5 | 0.377 | 0.000 | 1100/1000 |
| flat/1000/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 11.8 | 0.000 | 0.612 | 1000/1300 |
| flat/1000/skai_expand_mix/skai.talon_archer | seat0 | eliminated | 12.8 | 0.578 | 0.000 | 1300/1000 |
| flat/1000/skai.talon_archer/skai_air_mix | seat1 | eliminated | 21.8 | 0.000 | 0.157 | 1000/940 |
| flat/1000/skai_air_mix/skai.talon_archer | seat0 | eliminated | 16.7 | 0.242 | 0.000 | 940/1000 |
| flat/1000/skai.talon_archer/water_pressure_mix | seat0 | eliminated | 18.1 | 0.433 | 0.000 | 1000/675 |
| flat/1000/water_pressure_mix/skai.talon_archer | seat1 | eliminated | 20.1 | 0.000 | 0.273 | 675/1000 |
| flat/1000/skai.talon_archer/water_expand_mix | seat1 | eliminated | 21.3 | 0.000 | 0.597 | 1000/865 |
| flat/1000/water_expand_mix/skai.talon_archer | seat0 | eliminated | 22.0 | 0.664 | 0.000 | 865/1000 |
| flat/1000/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 8.1 | 0.000 | 0.874 | 1000/1235 |
| flat/1000/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 7.8 | 0.947 | 0.000 | 1235/1000 |
| flat/1000/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.5 | 0.798 | 0.000 | 1060/975 |
| flat/1000/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 0.798 | 975/1060 |
| flat/1000/skai.thunderhead/water.grasper | seat0 | eliminated | 9.1 | 1.000 | 0.000 | 1060/1000 |
| flat/1000/water.grasper/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1000/1060 |
| flat/1000/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 9.3 | 1.000 | 0.000 | 1060/950 |
| flat/1000/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 950/1060 |
| flat/1000/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.777 | 1060/1140 |
| flat/1000/water.leviathan/skai.thunderhead | seat0 | eliminated | 4.1 | 0.777 | 0.000 | 1140/1060 |
| flat/1000/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.5 | 1.000 | 0.000 | 1060/975 |
| flat/1000/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 975/1060 |
| flat/1000/skai.thunderhead/water.shellback | seat0 | eliminated | 49.3 | 1.000 | 0.000 | 1060/1050 |
| flat/1000/water.shellback/skai.thunderhead | seat1 | eliminated | 102.7 | 0.000 | 1.000 | 1050/1060 |
| flat/1000/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.3 | 0.868 | 0.000 | 1060/1000 |
| flat/1000/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.4 | 0.000 | 0.855 | 1000/1060 |
| flat/1000/skai.thunderhead/skai_pressure_mix | seat0 | eliminated | 14.0 | 0.612 | 0.000 | 1060/1100 |
| flat/1000/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 13.9 | 0.000 | 0.612 | 1100/1060 |
| flat/1000/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 32.5 | 0.000 | 0.308 | 1060/1300 |
| flat/1000/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 28.9 | 0.308 | 0.000 | 1300/1060 |
| flat/1000/skai.thunderhead/skai_air_mix | seat1 | eliminated | 31.3 | 0.000 | 0.920 | 1060/940 |
| flat/1000/skai_air_mix/skai.thunderhead | seat0 | eliminated | 32.5 | 0.920 | 0.000 | 940/1060 |
| flat/1000/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 1060/675 |
| flat/1000/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 9.1 | 0.000 | 1.000 | 675/1060 |
| flat/1000/skai.thunderhead/water_expand_mix | seat0 | eliminated | 19.1 | 0.750 | 0.000 | 1060/865 |
| flat/1000/water_expand_mix/skai.thunderhead | seat1 | eliminated | 24.1 | 0.000 | 0.400 | 865/1060 |
| flat/1000/skai.thunderhead/water_leviathan_mix | seat1 | eliminated | 13.1 | 0.000 | 0.718 | 1060/1235 |
| flat/1000/water_leviathan_mix/skai.thunderhead | seat0 | eliminated | 13.1 | 0.790 | 0.000 | 1235/1060 |
| flat/1000/skai.windrunner/water.grasper | seat1 | eliminated | 11.1 | 0.000 | 0.618 | 975/1000 |
| flat/1000/water.grasper/skai.windrunner | seat0 | eliminated | 11.0 | 0.626 | 0.000 | 1000/975 |
| flat/1000/skai.windrunner/water.ink_lobber | seat1 | eliminated | 13.8 | 0.000 | 0.194 | 975/950 |
| flat/1000/water.ink_lobber/skai.windrunner | seat0 | eliminated | 10.8 | 0.356 | 0.000 | 950/975 |
| flat/1000/skai.windrunner/water.leviathan | seat1 | eliminated | 2.0 | 0.000 | 1.000 | 975/1140 |
| flat/1000/water.leviathan/skai.windrunner | seat0 | eliminated | 2.0 | 1.000 | 0.000 | 1140/975 |
| flat/1000/skai.windrunner/water.reef_runner | seat0 | eliminated | 18.5 | 0.100 | 0.000 | 975/975 |
| flat/1000/water.reef_runner/skai.windrunner | seat1 | eliminated | 18.2 | 0.000 | 0.138 | 975/975 |
| flat/1000/skai.windrunner/water.shellback | seat1 | eliminated | 17.8 | 0.000 | 0.867 | 975/1050 |
| flat/1000/water.shellback/skai.windrunner | seat0 | eliminated | 17.4 | 0.973 | 0.000 | 1050/975 |
| flat/1000/skai.windrunner/water.spoutling | seat0 | eliminated | 8.1 | 0.782 | 0.000 | 975/1000 |
| flat/1000/water.spoutling/skai.windrunner | seat1 | eliminated | 8.1 | 0.000 | 0.776 | 1000/975 |
| flat/1000/skai.windrunner/skai_pressure_mix | seat1 | eliminated | 14.4 | 0.000 | 0.302 | 975/1100 |
| flat/1000/skai_pressure_mix/skai.windrunner | seat0 | eliminated | 13.4 | 0.365 | 0.000 | 1100/975 |
| flat/1000/skai.windrunner/skai_expand_mix | seat1 | eliminated | 10.8 | 0.000 | 0.645 | 975/1300 |
| flat/1000/skai_expand_mix/skai.windrunner | seat0 | eliminated | 10.8 | 0.639 | 0.000 | 1300/975 |
| flat/1000/skai.windrunner/skai_air_mix | seat1 | eliminated | 14.6 | 0.000 | 0.305 | 975/940 |
| flat/1000/skai_air_mix/skai.windrunner | seat0 | eliminated | 14.6 | 0.272 | 0.000 | 940/975 |
| flat/1000/skai.windrunner/water_pressure_mix | seat0 | eliminated | 15.1 | 0.385 | 0.000 | 975/675 |
| flat/1000/water_pressure_mix/skai.windrunner | seat1 | eliminated | 19.3 | 0.000 | 0.208 | 675/975 |
| flat/1000/skai.windrunner/water_expand_mix | seat1 | eliminated | 17.0 | 0.000 | 0.443 | 975/865 |
| flat/1000/water_expand_mix/skai.windrunner | seat0 | eliminated | 18.3 | 0.682 | 0.000 | 865/975 |
| flat/1000/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 7.3 | 0.000 | 0.982 | 975/1235 |
| flat/1000/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 5.4 | 1.000 | 0.000 | 1235/975 |
| flat/1000/water.grasper/water.ink_lobber | seat1 | eliminated | 17.0 | 0.000 | 0.032 | 1000/950 |
| flat/1000/water.ink_lobber/water.grasper | seat1 | eliminated | 11.9 | 0.000 | 0.120 | 950/1000 |
| flat/1000/water.grasper/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 1000/1140 |
| flat/1000/water.leviathan/water.grasper | seat0 | eliminated | 4.6 | 1.000 | 0.000 | 1140/1000 |
| flat/1000/water.grasper/water.reef_runner | seat0 | eliminated | 11.3 | 0.618 | 0.000 | 1000/975 |
| flat/1000/water.reef_runner/water.grasper | seat1 | eliminated | 10.9 | 0.000 | 0.622 | 975/1000 |
| flat/1000/water.grasper/water.shellback | seat0 | eliminated | 22.8 | 0.498 | 0.000 | 1000/1050 |
| flat/1000/water.shellback/water.grasper | seat1 | eliminated | 24.1 | 0.000 | 0.484 | 1050/1000 |
| flat/1000/water.grasper/water.spoutling | seat0 | eliminated | 8.8 | 0.818 | 0.000 | 1000/1000 |
| flat/1000/water.spoutling/water.grasper | seat1 | eliminated | 9.0 | 0.000 | 0.814 | 1000/1000 |
| flat/1000/water.grasper/skai_pressure_mix | seat0 | eliminated | 13.5 | 0.534 | 0.000 | 1000/1100 |
| flat/1000/skai_pressure_mix/water.grasper | seat1 | eliminated | 13.6 | 0.000 | 0.535 | 1100/1000 |
| flat/1000/water.grasper/skai_expand_mix | seat1 | eliminated | 43.5 | 0.000 | 0.308 | 1000/1300 |
| flat/1000/skai_expand_mix/water.grasper | seat0 | eliminated | 47.2 | 0.308 | 0.000 | 1300/1000 |
| flat/1000/water.grasper/skai_air_mix | seat1 | eliminated | 17.2 | 0.000 | 0.920 | 1000/940 |
| flat/1000/skai_air_mix/water.grasper | seat0 | eliminated | 18.4 | 0.920 | 0.000 | 940/1000 |
| flat/1000/water.grasper/water_pressure_mix | seat0 | eliminated | 12.4 | 0.661 | 0.000 | 1000/675 |
| flat/1000/water_pressure_mix/water.grasper | seat1 | eliminated | 11.8 | 0.000 | 0.685 | 675/1000 |
| flat/1000/water.grasper/water_expand_mix | seat0 | eliminated | 15.1 | 0.344 | 0.000 | 1000/865 |
| flat/1000/water_expand_mix/water.grasper | seat1 | eliminated | 13.9 | 0.000 | 0.587 | 865/1000 |
| flat/1000/water.grasper/water_leviathan_mix | seat1 | eliminated | 7.7 | 0.000 | 0.696 | 1000/1235 |
| flat/1000/water_leviathan_mix/water.grasper | seat0 | eliminated | 8.3 | 0.794 | 0.000 | 1235/1000 |
| flat/1000/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.850 | 950/1140 |
| flat/1000/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.5 | 0.850 | 0.000 | 1140/950 |
| flat/1000/water.ink_lobber/water.reef_runner | seat0 | eliminated | 10.7 | 0.432 | 0.000 | 950/975 |
| flat/1000/water.reef_runner/water.ink_lobber | seat1 | eliminated | 10.7 | 0.000 | 0.492 | 975/950 |
| flat/1000/water.ink_lobber/water.shellback | seat1 | eliminated | 14.8 | 0.000 | 0.932 | 950/1050 |
| flat/1000/water.shellback/water.ink_lobber | seat0 | eliminated | 15.1 | 1.000 | 0.000 | 1050/950 |
| flat/1000/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.9 | 0.800 | 0.000 | 950/1000 |
| flat/1000/water.spoutling/water.ink_lobber | seat1 | eliminated | 8.0 | 0.000 | 0.790 | 1000/950 |
| flat/1000/water.ink_lobber/skai_pressure_mix | seat1 | eliminated | 13.4 | 0.000 | 0.204 | 950/1100 |
| flat/1000/skai_pressure_mix/water.ink_lobber | seat0 | eliminated | 11.0 | 0.218 | 0.000 | 1100/950 |
| flat/1000/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 8.8 | 0.000 | 0.688 | 950/1300 |
| flat/1000/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 9.1 | 0.642 | 0.000 | 1300/950 |
| flat/1000/water.ink_lobber/skai_air_mix | seat1 | eliminated | 9.1 | 0.000 | 0.920 | 950/940 |
| flat/1000/skai_air_mix/water.ink_lobber | seat0 | eliminated | 9.3 | 0.920 | 0.000 | 940/950 |
| flat/1000/water.ink_lobber/water_pressure_mix | seat0 | eliminated | 8.2 | 0.800 | 0.000 | 950/675 |
| flat/1000/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.632 | 675/950 |
| flat/1000/water.ink_lobber/water_expand_mix | seat1 | eliminated | 13.4 | 0.000 | 0.496 | 950/865 |
| flat/1000/water_expand_mix/water.ink_lobber | seat0 | eliminated | 15.4 | 0.686 | 0.000 | 865/950 |
| flat/1000/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 7.9 | 0.000 | 0.899 | 950/1235 |
| flat/1000/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 8.1 | 1.000 | 0.000 | 1235/950 |
| flat/1000/water.leviathan/water.reef_runner | seat0 | eliminated | 2.1 | 1.000 | 0.000 | 1140/975 |
| flat/1000/water.reef_runner/water.leviathan | seat1 | eliminated | 2.1 | 0.000 | 1.000 | 975/1140 |
| flat/1000/water.leviathan/water.shellback | seat0 | eliminated | 32.3 | 0.276 | 0.000 | 1140/1050 |
| flat/1000/water.shellback/water.leviathan | seat0 | eliminated | 32.1 | 0.311 | 0.000 | 1050/1140 |
| flat/1000/water.leviathan/water.spoutling | seat0 | eliminated | 2.1 | 1.000 | 0.000 | 1140/1000 |
| flat/1000/water.spoutling/water.leviathan | seat1 | eliminated | 2.1 | 0.000 | 1.000 | 1000/1140 |
| flat/1000/water.leviathan/skai_pressure_mix | seat0 | eliminated | 4.3 | 0.973 | 0.000 | 1140/1100 |
| flat/1000/skai_pressure_mix/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.979 | 1100/1140 |
| flat/1000/water.leviathan/skai_expand_mix | seat0 | eliminated | 4.4 | 0.932 | 0.000 | 1140/1300 |
| flat/1000/skai_expand_mix/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.914 | 1300/1140 |
| flat/1000/water.leviathan/skai_air_mix | seat0 | eliminated | 4.1 | 0.926 | 0.000 | 1140/940 |
| flat/1000/skai_air_mix/water.leviathan | seat1 | eliminated | 3.9 | 0.000 | 0.883 | 940/1140 |
| flat/1000/water.leviathan/water_pressure_mix | seat0 | eliminated | 2.0 | 1.000 | 0.000 | 1140/675 |
| flat/1000/water_pressure_mix/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 1.000 | 675/1140 |
| flat/1000/water.leviathan/water_expand_mix | seat0 | eliminated | 10.9 | 0.755 | 0.000 | 1140/865 |
| flat/1000/water_expand_mix/water.leviathan | seat1 | eliminated | 10.9 | 0.000 | 0.809 | 865/1140 |
| flat/1000/water.leviathan/water_leviathan_mix | seat1 | eliminated | 14.5 | 0.000 | 0.392 | 1140/1235 |
| flat/1000/water_leviathan_mix/water.leviathan | seat0 | eliminated | 16.0 | 0.438 | 0.000 | 1235/1140 |
| flat/1000/water.reef_runner/water.shellback | seat1 | eliminated | 16.8 | 0.000 | 0.908 | 975/1050 |
| flat/1000/water.shellback/water.reef_runner | seat0 | eliminated | 17.3 | 0.980 | 0.000 | 1050/975 |
| flat/1000/water.reef_runner/water.spoutling | seat0 | eliminated | 9.2 | 0.750 | 0.000 | 975/1000 |
| flat/1000/water.spoutling/water.reef_runner | seat1 | eliminated | 8.8 | 0.000 | 0.756 | 1000/975 |
| flat/1000/water.reef_runner/skai_pressure_mix | seat1 | eliminated | 13.3 | 0.000 | 0.380 | 975/1100 |
| flat/1000/skai_pressure_mix/water.reef_runner | seat0 | eliminated | 14.2 | 0.325 | 0.000 | 1100/975 |
| flat/1000/water.reef_runner/skai_expand_mix | seat1 | eliminated | 13.7 | 0.000 | 0.487 | 975/1300 |
| flat/1000/skai_expand_mix/water.reef_runner | seat0 | eliminated | 12.8 | 0.549 | 0.000 | 1300/975 |
| flat/1000/water.reef_runner/skai_air_mix | seat1 | eliminated | 12.2 | 0.000 | 0.920 | 975/940 |
| flat/1000/skai_air_mix/water.reef_runner | seat0 | eliminated | 13.4 | 0.920 | 0.000 | 940/975 |
| flat/1000/water.reef_runner/water_pressure_mix | seat0 | eliminated | 16.5 | 0.354 | 0.000 | 975/675 |
| flat/1000/water_pressure_mix/water.reef_runner | seat0 | eliminated | 20.3 | 0.111 | 0.000 | 675/975 |
| flat/1000/water.reef_runner/water_expand_mix | seat1 | eliminated | 15.4 | 0.000 | 0.549 | 975/865 |
| flat/1000/water_expand_mix/water.reef_runner | seat0 | eliminated | 16.5 | 0.752 | 0.000 | 865/975 |
| flat/1000/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 7.3 | 0.000 | 0.965 | 975/1235 |
| flat/1000/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 6.8 | 0.983 | 0.000 | 1235/975 |
| flat/1000/water.shellback/water.spoutling | seat0 | eliminated | 14.2 | 1.000 | 0.000 | 1050/1000 |
| flat/1000/water.spoutling/water.shellback | seat1 | eliminated | 15.1 | 0.000 | 0.992 | 1000/1050 |
| flat/1000/water.shellback/skai_pressure_mix | seat0 | eliminated | 20.3 | 0.967 | 0.000 | 1050/1100 |
| flat/1000/skai_pressure_mix/water.shellback | seat1 | eliminated | 20.0 | 0.000 | 0.900 | 1100/1050 |
| flat/1000/water.shellback/skai_expand_mix | stalemate | dome_absorbed | 49.8 | 0.857 | 0.308 | 1050/1300 |
| flat/1000/skai_expand_mix/water.shellback | stalemate | dome_absorbed | 48.5 | 0.308 | 0.813 | 1300/1050 |
| flat/1000/water.shellback/skai_air_mix | stalemate | dome_absorbed | 37.8 | 0.960 | 0.920 | 1050/940 |
| flat/1000/skai_air_mix/water.shellback | stalemate | dome_absorbed | 37.8 | 0.920 | 0.885 | 940/1050 |
| flat/1000/water.shellback/water_pressure_mix | seat0 | eliminated | 22.7 | 0.500 | 0.000 | 1050/675 |
| flat/1000/water_pressure_mix/water.shellback | seat1 | eliminated | 21.8 | 0.000 | 0.595 | 675/1050 |
| flat/1000/water.shellback/water_expand_mix | seat0 | eliminated | 33.7 | 0.857 | 0.000 | 1050/865 |
| flat/1000/water_expand_mix/water.shellback | seat1 | eliminated | 36.2 | 0.000 | 0.830 | 865/1050 |
| flat/1000/water.shellback/water_leviathan_mix | seat0 | eliminated | 44.1 | 0.606 | 0.000 | 1050/1235 |
| flat/1000/water_leviathan_mix/water.shellback | seat1 | eliminated | 36.8 | 0.000 | 0.931 | 1235/1050 |
| flat/1000/water.spoutling/skai_pressure_mix | seat1 | eliminated | 8.2 | 0.000 | 0.801 | 1000/1100 |
| flat/1000/skai_pressure_mix/water.spoutling | seat0 | eliminated | 7.8 | 0.807 | 0.000 | 1100/1000 |
| flat/1000/water.spoutling/skai_expand_mix | seat1 | eliminated | 7.6 | 0.000 | 0.851 | 1000/1300 |
| flat/1000/skai_expand_mix/water.spoutling | seat0 | eliminated | 7.6 | 0.856 | 0.000 | 1300/1000 |
| flat/1000/water.spoutling/skai_air_mix | seat1 | eliminated | 10.0 | 0.000 | 0.754 | 1000/940 |
| flat/1000/skai_air_mix/water.spoutling | seat0 | eliminated | 11.3 | 0.714 | 0.000 | 940/1000 |
| flat/1000/water.spoutling/water_pressure_mix | seat1 | eliminated | 12.5 | 0.000 | 0.593 | 1000/675 |
| flat/1000/water_pressure_mix/water.spoutling | seat0 | eliminated | 12.5 | 0.630 | 0.000 | 675/1000 |
| flat/1000/water.spoutling/water_expand_mix | seat1 | eliminated | 11.2 | 0.000 | 0.959 | 1000/865 |
| flat/1000/water_expand_mix/water.spoutling | seat0 | eliminated | 11.4 | 0.926 | 0.000 | 865/1000 |
| flat/1000/water.spoutling/water_leviathan_mix | seat1 | eliminated | 4.8 | 0.000 | 0.978 | 1000/1235 |
| flat/1000/water_leviathan_mix/water.spoutling | seat0 | eliminated | 4.9 | 1.000 | 0.000 | 1235/1000 |
| flat/1000/skai_pressure_mix/skai_expand_mix | seat1 | eliminated | 13.6 | 0.000 | 0.533 | 1100/1300 |
| flat/1000/skai_expand_mix/skai_pressure_mix | seat0 | eliminated | 12.9 | 0.550 | 0.000 | 1300/1100 |
| flat/1000/skai_pressure_mix/skai_air_mix | seat0 | eliminated | 19.6 | 0.154 | 0.000 | 1100/940 |
| flat/1000/skai_air_mix/skai_pressure_mix | seat0 | eliminated | 23.9 | 0.066 | 0.000 | 940/1100 |
| flat/1000/skai_pressure_mix/water_pressure_mix | seat0 | eliminated | 14.2 | 0.455 | 0.000 | 1100/675 |
| flat/1000/water_pressure_mix/skai_pressure_mix | seat1 | eliminated | 14.6 | 0.000 | 0.468 | 675/1100 |
| flat/1000/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 21.8 | 0.000 | 0.459 | 1100/865 |
| flat/1000/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 22.7 | 0.607 | 0.000 | 865/1100 |
| flat/1000/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 10.3 | 0.000 | 0.899 | 1100/1235 |
| flat/1000/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 8.2 | 1.000 | 0.000 | 1235/1100 |
| flat/1000/skai_expand_mix/skai_air_mix | seat0 | eliminated | 14.0 | 0.461 | 0.000 | 1300/940 |
| flat/1000/skai_air_mix/skai_expand_mix | seat1 | eliminated | 14.0 | 0.000 | 0.503 | 940/1300 |
| flat/1000/skai_expand_mix/water_pressure_mix | seat0 | eliminated | 13.6 | 0.641 | 0.000 | 1300/675 |
| flat/1000/water_pressure_mix/skai_expand_mix | seat1 | eliminated | 13.5 | 0.000 | 0.626 | 675/1300 |
| flat/1000/skai_expand_mix/water_expand_mix | seat0 | eliminated | 41.3 | 0.269 | 0.000 | 1300/865 |
| flat/1000/water_expand_mix/skai_expand_mix | seat0 | eliminated | 28.3 | 0.520 | 0.000 | 865/1300 |
| flat/1000/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 10.2 | 0.000 | 0.781 | 1300/1235 |
| flat/1000/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 9.8 | 0.860 | 0.000 | 1235/1300 |
| flat/1000/skai_air_mix/water_pressure_mix | seat0 | eliminated | 11.6 | 0.920 | 0.000 | 940/675 |
| flat/1000/water_pressure_mix/skai_air_mix | seat1 | eliminated | 13.5 | 0.000 | 0.920 | 675/940 |
| flat/1000/skai_air_mix/water_expand_mix | seat0 | eliminated | 32.8 | 0.726 | 0.000 | 940/865 |
| flat/1000/water_expand_mix/skai_air_mix | seat0 | eliminated | 30.0 | 0.618 | 0.000 | 865/940 |
| flat/1000/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 10.1 | 0.000 | 0.793 | 940/1235 |
| flat/1000/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 13.3 | 0.802 | 0.000 | 1235/940 |
| flat/1000/water_pressure_mix/water_expand_mix | seat1 | eliminated | 22.8 | 0.000 | 0.376 | 675/865 |
| flat/1000/water_expand_mix/water_pressure_mix | seat0 | eliminated | 20.0 | 0.393 | 0.000 | 865/675 |
| flat/1000/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 6.0 | 0.000 | 0.913 | 675/1235 |
| flat/1000/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 7.3 | 0.870 | 0.000 | 1235/675 |
| flat/1000/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 16.6 | 0.000 | 0.899 | 865/1235 |
| flat/1000/water_leviathan_mix/water_expand_mix | seat0 | eliminated | 20.0 | 0.776 | 0.000 | 1235/865 |
| flat/3000/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 11.3 | 1.000 | 0.000 | 3000/3040 |
| flat/3000/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 11.6 | 0.000 | 1.000 | 3040/3000 |
| flat/3000/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 22.2 | 0.000 | 0.376 | 3000/3000 |
| flat/3000/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 22.1 | 0.390 | 0.000 | 3000/3000 |
| flat/3000/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 18.6 | 1.000 | 0.000 | 3000/2915 |
| flat/3000/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 18.6 | 0.000 | 1.000 | 2915/3000 |
| flat/3000/skai.roc_rider/skai.windrunner | seat1 | eliminated | 17.4 | 0.000 | 0.434 | 3000/3000 |
| flat/3000/skai.windrunner/skai.roc_rider | seat0 | eliminated | 16.8 | 0.493 | 0.000 | 3000/3000 |
| flat/3000/skai.roc_rider/water.grasper | seat0 | eliminated | 29.3 | 1.000 | 0.000 | 3000/3000 |
| flat/3000/water.grasper/skai.roc_rider | seat1 | eliminated | 29.6 | 0.000 | 1.000 | 3000/3000 |
| flat/3000/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 12.5 | 1.000 | 0.000 | 3000/3040 |
| flat/3000/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 11.9 | 0.000 | 1.000 | 3040/3000 |
| flat/3000/skai.roc_rider/water.leviathan | seat1 | eliminated | 3.9 | 0.000 | 0.974 | 3000/3040 |
| flat/3000/water.leviathan/skai.roc_rider | seat0 | eliminated | 3.9 | 0.974 | 0.000 | 3040/3000 |
| flat/3000/skai.roc_rider/water.reef_runner | seat0 | eliminated | 19.9 | 1.000 | 0.000 | 3000/3000 |
| flat/3000/water.reef_runner/skai.roc_rider | seat1 | eliminated | 20.6 | 0.000 | 1.000 | 3000/3000 |
| flat/3000/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 36.8 | 1.000 | 0.980 | 3000/2975 |
| flat/3000/water.shellback/skai.roc_rider | seat1 | eliminated | 234.1 | 0.000 | 1.000 | 2975/3000 |
| flat/3000/skai.roc_rider/water.spoutling | seat0 | eliminated | 12.5 | 0.712 | 0.000 | 3000/3000 |
| flat/3000/water.spoutling/skai.roc_rider | seat1 | eliminated | 13.9 | 0.000 | 0.688 | 3000/3000 |
| flat/3000/skai.roc_rider/skai_pressure_mix | seat1 | eliminated | 27.2 | 0.000 | 0.196 | 3000/2750 |
| flat/3000/skai_pressure_mix/skai.roc_rider | seat0 | eliminated | 23.4 | 0.286 | 0.000 | 2750/3000 |
| flat/3000/skai.roc_rider/skai_expand_mix | seat1 | eliminated | 20.6 | 0.000 | 0.466 | 3000/3250 |
| flat/3000/skai_expand_mix/skai.roc_rider | seat0 | eliminated | 21.9 | 0.421 | 0.000 | 3250/3000 |
| flat/3000/skai.roc_rider/skai_air_mix | seat0 | eliminated | 23.2 | 0.552 | 0.000 | 3000/2820 |
| flat/3000/skai_air_mix/skai.roc_rider | seat1 | eliminated | 25.6 | 0.000 | 0.419 | 2820/3000 |
| flat/3000/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 25.7 | 1.000 | 0.000 | 3000/2700 |
| flat/3000/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 25.2 | 0.000 | 1.000 | 2700/3000 |
| flat/3000/skai.roc_rider/water_expand_mix | seat1 | eliminated | 35.0 | 0.000 | 0.629 | 3000/2595 |
| flat/3000/water_expand_mix/skai.roc_rider | seat0 | eliminated | 35.2 | 0.617 | 0.000 | 2595/3000 |
| flat/3000/skai.roc_rider/water_leviathan_mix | seat1 | eliminated | 13.8 | 0.000 | 0.673 | 3000/2470 |
| flat/3000/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 14.0 | 0.787 | 0.000 | 2470/3000 |
| flat/3000/skai.stormcaller/skai.talon_archer | seat0 | eliminated | 12.5 | 0.313 | 0.000 | 3040/3000 |
| flat/3000/skai.talon_archer/skai.stormcaller | seat1 | eliminated | 13.0 | 0.000 | 0.277 | 3000/3040 |
| flat/3000/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 1.000 | 3040/2915 |
| flat/3000/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 4.7 | 1.000 | 0.000 | 2915/3040 |
| flat/3000/skai.stormcaller/skai.windrunner | seat1 | eliminated | 13.1 | 0.000 | 0.086 | 3040/3000 |
| flat/3000/skai.windrunner/skai.stormcaller | seat1 | eliminated | 14.7 | 0.000 | 0.157 | 3000/3040 |
| flat/3000/skai.stormcaller/water.grasper | seat0 | eliminated | 12.0 | 0.492 | 0.000 | 3040/3000 |
| flat/3000/water.grasper/skai.stormcaller | seat1 | eliminated | 12.2 | 0.000 | 0.479 | 3000/3040 |
| flat/3000/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 8.9 | 0.000 | 0.226 | 3040/3040 |
| flat/3000/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 6.9 | 0.282 | 0.000 | 3040/3040 |
| flat/3000/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.917 | 3040/3040 |
| flat/3000/water.leviathan/skai.stormcaller | seat0 | eliminated | 3.8 | 0.917 | 0.000 | 3040/3040 |
| flat/3000/skai.stormcaller/water.reef_runner | seat0 | eliminated | 9.5 | 0.471 | 0.000 | 3040/3000 |
| flat/3000/water.reef_runner/skai.stormcaller | seat1 | eliminated | 11.6 | 0.000 | 0.458 | 3000/3040 |
| flat/3000/skai.stormcaller/water.shellback | seat1 | eliminated | 16.7 | 0.000 | 0.921 | 3040/2975 |
| flat/3000/water.shellback/skai.stormcaller | seat0 | eliminated | 16.1 | 0.970 | 0.000 | 2975/3040 |
| flat/3000/skai.stormcaller/water.spoutling | seat0 | eliminated | 6.7 | 0.878 | 0.000 | 3040/3000 |
| flat/3000/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.0 | 0.000 | 0.865 | 3000/3040 |
| flat/3000/skai.stormcaller/skai_pressure_mix | seat0 | eliminated | 14.7 | 0.302 | 0.000 | 3040/2750 |
| flat/3000/skai_pressure_mix/skai.stormcaller | seat1 | eliminated | 16.7 | 0.000 | 0.285 | 2750/3040 |
| flat/3000/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 14.0 | 0.000 | 0.326 | 3040/3250 |
| flat/3000/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 11.6 | 0.402 | 0.000 | 3250/3040 |
| flat/3000/skai.stormcaller/skai_air_mix | seat1 | eliminated | 7.9 | 0.000 | 0.920 | 3040/2820 |
| flat/3000/skai_air_mix/skai.stormcaller | seat0 | eliminated | 8.4 | 0.920 | 0.000 | 2820/3040 |
| flat/3000/skai.stormcaller/water_pressure_mix | seat0 | eliminated | 9.8 | 0.688 | 0.000 | 3040/2700 |
| flat/3000/water_pressure_mix/skai.stormcaller | seat1 | eliminated | 12.0 | 0.000 | 0.575 | 2700/3040 |
| flat/3000/skai.stormcaller/water_expand_mix | seat1 | eliminated | 17.3 | 0.000 | 0.551 | 3040/2595 |
| flat/3000/water_expand_mix/skai.stormcaller | seat0 | eliminated | 17.9 | 0.585 | 0.000 | 2595/3040 |
| flat/3000/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 8.1 | 0.000 | 0.899 | 3040/2470 |
| flat/3000/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 8.3 | 0.956 | 0.000 | 2470/3040 |
| flat/3000/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 7.5 | 0.000 | 0.686 | 3000/2915 |
| flat/3000/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 7.4 | 0.707 | 0.000 | 2915/3000 |
| flat/3000/skai.talon_archer/skai.windrunner | seat1 | eliminated | 21.2 | 0.000 | 0.218 | 3000/3000 |
| flat/3000/skai.windrunner/skai.talon_archer | seat0 | eliminated | 20.0 | 0.240 | 0.000 | 3000/3000 |
| flat/3000/skai.talon_archer/water.grasper | seat1 | eliminated | 14.9 | 0.000 | 0.592 | 3000/3000 |
| flat/3000/water.grasper/skai.talon_archer | seat0 | eliminated | 15.1 | 0.572 | 0.000 | 3000/3000 |
| flat/3000/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 8.7 | 0.000 | 0.591 | 3000/3040 |
| flat/3000/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 8.8 | 0.574 | 0.000 | 3040/3000 |
| flat/3000/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.944 | 3000/3040 |
| flat/3000/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.977 | 0.000 | 3040/3000 |
| flat/3000/skai.talon_archer/water.reef_runner | seat1 | eliminated | 23.0 | 0.000 | 0.147 | 3000/3000 |
| flat/3000/water.reef_runner/skai.talon_archer | seat0 | eliminated | 26.8 | 0.093 | 0.000 | 3000/3000 |
| flat/3000/skai.talon_archer/water.shellback | seat1 | eliminated | 23.2 | 0.000 | 0.917 | 3000/2975 |
| flat/3000/water.shellback/skai.talon_archer | seat0 | eliminated | 22.7 | 0.897 | 0.000 | 2975/3000 |
| flat/3000/skai.talon_archer/water.spoutling | seat0 | eliminated | 10.7 | 0.811 | 0.000 | 3000/3000 |
| flat/3000/water.spoutling/skai.talon_archer | seat1 | eliminated | 10.4 | 0.000 | 0.819 | 3000/3000 |
| flat/3000/skai.talon_archer/skai_pressure_mix | seat0 | eliminated | 21.3 | 0.277 | 0.000 | 3000/2750 |
| flat/3000/skai_pressure_mix/skai.talon_archer | seat1 | eliminated | 20.3 | 0.000 | 0.318 | 2750/3000 |
| flat/3000/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 24.3 | 0.000 | 0.241 | 3000/3250 |
| flat/3000/skai_expand_mix/skai.talon_archer | seat0 | eliminated | 25.0 | 0.214 | 0.000 | 3250/3000 |
| flat/3000/skai.talon_archer/skai_air_mix | seat1 | eliminated | 14.7 | 0.000 | 0.450 | 3000/2820 |
| flat/3000/skai_air_mix/skai.talon_archer | seat0 | eliminated | 16.3 | 0.371 | 0.000 | 2820/3000 |
| flat/3000/skai.talon_archer/water_pressure_mix | seat1 | eliminated | 17.6 | 0.000 | 0.478 | 3000/2700 |
| flat/3000/water_pressure_mix/skai.talon_archer | seat0 | eliminated | 17.1 | 0.490 | 0.000 | 2700/3000 |
| flat/3000/skai.talon_archer/water_expand_mix | seat1 | eliminated | 21.1 | 0.000 | 0.765 | 3000/2595 |
| flat/3000/water_expand_mix/skai.talon_archer | seat0 | eliminated | 23.9 | 0.681 | 0.000 | 2595/3000 |
| flat/3000/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 10.5 | 0.000 | 0.832 | 3000/2470 |
| flat/3000/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 10.5 | 0.836 | 0.000 | 2470/3000 |
| flat/3000/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.3 | 0.826 | 0.000 | 2915/3000 |
| flat/3000/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.3 | 0.000 | 0.812 | 3000/2915 |
| flat/3000/skai.thunderhead/water.grasper | seat0 | eliminated | 6.9 | 1.000 | 0.000 | 2915/3000 |
| flat/3000/water.grasper/skai.thunderhead | seat1 | eliminated | 6.9 | 0.000 | 1.000 | 3000/2915 |
| flat/3000/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 4.7 | 1.000 | 0.000 | 2915/3040 |
| flat/3000/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 4.8 | 0.000 | 1.000 | 3040/2915 |
| flat/3000/skai.thunderhead/water.leviathan | seat1 | eliminated | 6.8 | 0.000 | 0.746 | 2915/3040 |
| flat/3000/water.leviathan/skai.thunderhead | seat0 | eliminated | 4.2 | 0.838 | 0.000 | 3040/2915 |
| flat/3000/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.4 | 1.000 | 0.000 | 2915/3000 |
| flat/3000/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.3 | 0.000 | 1.000 | 3000/2915 |
| flat/3000/skai.thunderhead/water.shellback | stalemate | dome_absorbed | 64.5 | 1.000 | 0.933 | 2915/2975 |
| flat/3000/water.shellback/skai.thunderhead | seat1 | eliminated | 36.9 | 0.000 | 1.000 | 2975/2915 |
| flat/3000/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.6 | 0.888 | 0.000 | 2915/3000 |
| flat/3000/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.8 | 0.000 | 0.878 | 3000/2915 |
| flat/3000/skai.thunderhead/skai_pressure_mix | seat0 | eliminated | 6.3 | 0.830 | 0.000 | 2915/2750 |
| flat/3000/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 0.835 | 2750/2915 |
| flat/3000/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 42.1 | 0.000 | 0.308 | 2915/3250 |
| flat/3000/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 42.1 | 0.308 | 0.000 | 3250/2915 |
| flat/3000/skai.thunderhead/skai_air_mix | seat1 | eliminated | 29.1 | 0.000 | 0.920 | 2915/2820 |
| flat/3000/skai_air_mix/skai.thunderhead | seat0 | eliminated | 28.8 | 0.920 | 0.000 | 2820/2915 |
| flat/3000/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 2915/2700 |
| flat/3000/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 2700/2915 |
| flat/3000/skai.thunderhead/water_expand_mix | seat0 | eliminated | 14.0 | 0.715 | 0.000 | 2915/2595 |
| flat/3000/water_expand_mix/skai.thunderhead | seat1 | eliminated | 14.3 | 0.000 | 0.639 | 2595/2915 |
| flat/3000/skai.thunderhead/water_leviathan_mix | seat1 | eliminated | 16.4 | 0.000 | 0.145 | 2915/2470 |
| flat/3000/water_leviathan_mix/skai.thunderhead | seat0 | eliminated | 16.1 | 0.178 | 0.000 | 2470/2915 |
| flat/3000/skai.windrunner/water.grasper | seat1 | eliminated | 12.4 | 0.000 | 0.600 | 3000/3000 |
| flat/3000/water.grasper/skai.windrunner | seat0 | eliminated | 12.3 | 0.595 | 0.000 | 3000/3000 |
| flat/3000/skai.windrunner/water.ink_lobber | seat1 | eliminated | 10.7 | 0.000 | 0.588 | 3000/3040 |
| flat/3000/water.ink_lobber/skai.windrunner | seat0 | eliminated | 10.9 | 0.600 | 0.000 | 3040/3000 |
| flat/3000/skai.windrunner/water.leviathan | seat1 | eliminated | 3.3 | 0.000 | 0.998 | 3000/3040 |
| flat/3000/water.leviathan/skai.windrunner | seat0 | eliminated | 2.6 | 1.000 | 0.000 | 3040/3000 |
| flat/3000/skai.windrunner/water.reef_runner | seat0 | eliminated | 22.7 | 0.090 | 0.000 | 3000/3000 |
| flat/3000/water.reef_runner/skai.windrunner | seat1 | eliminated | 20.6 | 0.000 | 0.150 | 3000/3000 |
| flat/3000/skai.windrunner/water.shellback | seat1 | eliminated | 20.2 | 0.000 | 0.931 | 3000/2975 |
| flat/3000/water.shellback/skai.windrunner | seat0 | eliminated | 20.5 | 0.911 | 0.000 | 2975/3000 |
| flat/3000/skai.windrunner/water.spoutling | seat0 | eliminated | 9.2 | 0.785 | 0.000 | 3000/3000 |
| flat/3000/water.spoutling/skai.windrunner | seat1 | eliminated | 9.3 | 0.000 | 0.781 | 3000/3000 |
| flat/3000/skai.windrunner/skai_pressure_mix | seat0 | eliminated | 15.3 | 0.386 | 0.000 | 3000/2750 |
| flat/3000/skai_pressure_mix/skai.windrunner | seat1 | eliminated | 14.5 | 0.000 | 0.423 | 2750/3000 |
| flat/3000/skai.windrunner/skai_expand_mix | seat1 | eliminated | 20.2 | 0.000 | 0.269 | 3000/3250 |
| flat/3000/skai_expand_mix/skai.windrunner | seat0 | eliminated | 29.5 | 0.081 | 0.000 | 3250/3000 |
| flat/3000/skai.windrunner/skai_air_mix | seat1 | eliminated | 13.3 | 0.000 | 0.448 | 3000/2820 |
| flat/3000/skai_air_mix/skai.windrunner | seat0 | eliminated | 11.6 | 0.516 | 0.000 | 2820/3000 |
| flat/3000/skai.windrunner/water_pressure_mix | seat1 | eliminated | 14.9 | 0.000 | 0.478 | 3000/2700 |
| flat/3000/water_pressure_mix/skai.windrunner | seat0 | eliminated | 14.7 | 0.498 | 0.000 | 2700/3000 |
| flat/3000/skai.windrunner/water_expand_mix | seat1 | eliminated | 17.9 | 0.000 | 0.707 | 3000/2595 |
| flat/3000/water_expand_mix/skai.windrunner | seat0 | eliminated | 18.3 | 0.707 | 0.000 | 2595/3000 |
| flat/3000/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 8.1 | 0.000 | 0.918 | 3000/2470 |
| flat/3000/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 10.7 | 0.880 | 0.000 | 2470/3000 |
| flat/3000/water.grasper/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.700 | 3000/3040 |
| flat/3000/water.ink_lobber/water.grasper | seat0 | eliminated | 8.6 | 0.745 | 0.000 | 3040/3000 |
| flat/3000/water.grasper/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 1.000 | 3000/3040 |
| flat/3000/water.leviathan/water.grasper | seat0 | eliminated | 4.2 | 1.000 | 0.000 | 3040/3000 |
| flat/3000/water.grasper/water.reef_runner | seat0 | eliminated | 12.5 | 0.602 | 0.000 | 3000/3000 |
| flat/3000/water.reef_runner/water.grasper | seat1 | eliminated | 12.4 | 0.000 | 0.618 | 3000/3000 |
| flat/3000/water.grasper/water.shellback | seat0 | eliminated | 23.3 | 0.542 | 0.000 | 3000/2975 |
| flat/3000/water.shellback/water.grasper | seat1 | eliminated | 22.8 | 0.000 | 0.586 | 2975/3000 |
| flat/3000/water.grasper/water.spoutling | seat0 | eliminated | 9.9 | 0.806 | 0.000 | 3000/3000 |
| flat/3000/water.spoutling/water.grasper | seat1 | eliminated | 10.0 | 0.000 | 0.817 | 3000/3000 |
| flat/3000/water.grasper/skai_pressure_mix | seat0 | eliminated | 11.5 | 0.670 | 0.000 | 3000/2750 |
| flat/3000/skai_pressure_mix/water.grasper | seat1 | eliminated | 11.5 | 0.000 | 0.669 | 2750/3000 |
| flat/3000/water.grasper/skai_expand_mix | seat1 | eliminated | 65.2 | 0.000 | 0.308 | 3000/3250 |
| flat/3000/skai_expand_mix/water.grasper | seat0 | eliminated | 66.5 | 0.308 | 0.000 | 3250/3000 |
| flat/3000/water.grasper/skai_air_mix | seat1 | eliminated | 14.6 | 0.000 | 0.920 | 3000/2820 |
| flat/3000/skai_air_mix/water.grasper | seat0 | eliminated | 16.1 | 0.920 | 0.000 | 2820/3000 |
| flat/3000/water.grasper/water_pressure_mix | seat0 | eliminated | 20.0 | 0.395 | 0.000 | 3000/2700 |
| flat/3000/water_pressure_mix/water.grasper | seat1 | eliminated | 20.0 | 0.000 | 0.394 | 2700/3000 |
| flat/3000/water.grasper/water_expand_mix | seat0 | eliminated | 17.0 | 0.290 | 0.000 | 3000/2595 |
| flat/3000/water_expand_mix/water.grasper | seat1 | eliminated | 17.5 | 0.000 | 0.286 | 2595/3000 |
| flat/3000/water.grasper/water_leviathan_mix | seat1 | eliminated | 10.5 | 0.000 | 0.716 | 3000/2470 |
| flat/3000/water_leviathan_mix/water.grasper | seat0 | eliminated | 11.1 | 0.728 | 0.000 | 2470/3000 |
| flat/3000/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.902 | 3040/3040 |
| flat/3000/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.4 | 0.887 | 0.000 | 3040/3040 |
| flat/3000/water.ink_lobber/water.reef_runner | seat0 | eliminated | 8.3 | 0.711 | 0.000 | 3040/3000 |
| flat/3000/water.reef_runner/water.ink_lobber | seat1 | eliminated | 8.1 | 0.000 | 0.665 | 3000/3040 |
| flat/3000/water.ink_lobber/water.shellback | seat1 | eliminated | 18.5 | 0.000 | 0.829 | 3040/2975 |
| flat/3000/water.shellback/water.ink_lobber | seat0 | eliminated | 17.7 | 0.864 | 0.000 | 2975/3040 |
| flat/3000/water.ink_lobber/water.spoutling | seat0 | eliminated | 6.1 | 0.916 | 0.000 | 3040/3000 |
| flat/3000/water.spoutling/water.ink_lobber | seat1 | eliminated | 6.2 | 0.000 | 0.884 | 3000/3040 |
| flat/3000/water.ink_lobber/skai_pressure_mix | seat0 | eliminated | 10.6 | 0.690 | 0.000 | 3040/2750 |
| flat/3000/skai_pressure_mix/water.ink_lobber | seat1 | eliminated | 8.9 | 0.000 | 0.711 | 2750/3040 |
| flat/3000/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 25.3 | 0.000 | 0.308 | 3040/3250 |
| flat/3000/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 21.3 | 0.308 | 0.000 | 3250/3040 |
| flat/3000/water.ink_lobber/skai_air_mix | seat1 | eliminated | 9.7 | 0.000 | 0.920 | 3040/2820 |
| flat/3000/skai_air_mix/water.ink_lobber | seat0 | eliminated | 9.7 | 0.920 | 0.000 | 2820/3040 |
| flat/3000/water.ink_lobber/water_pressure_mix | seat0 | eliminated | 10.8 | 0.734 | 0.000 | 3040/2700 |
| flat/3000/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 10.7 | 0.000 | 0.741 | 2700/3040 |
| flat/3000/water.ink_lobber/water_expand_mix | seat1 | eliminated | 24.3 | 0.000 | 0.220 | 3040/2595 |
| flat/3000/water_expand_mix/water.ink_lobber | seat0 | eliminated | 19.6 | 0.331 | 0.000 | 2595/3040 |
| flat/3000/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 9.2 | 0.000 | 0.878 | 3040/2470 |
| flat/3000/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 8.5 | 0.771 | 0.000 | 2470/3040 |
| flat/3000/water.leviathan/water.reef_runner | seat0 | eliminated | 4.0 | 0.988 | 0.000 | 3040/3000 |
| flat/3000/water.reef_runner/water.leviathan | seat1 | eliminated | 3.4 | 0.000 | 0.998 | 3000/3040 |
| flat/3000/water.leviathan/water.shellback | seat0 | eliminated | 23.1 | 0.458 | 0.000 | 3040/2975 |
| flat/3000/water.shellback/water.leviathan | seat1 | eliminated | 19.4 | 0.000 | 0.568 | 2975/3040 |
| flat/3000/water.leviathan/water.spoutling | seat0 | eliminated | 3.1 | 1.000 | 0.000 | 3040/3000 |
| flat/3000/water.spoutling/water.leviathan | seat1 | eliminated | 2.7 | 0.000 | 0.998 | 3000/3040 |
| flat/3000/water.leviathan/skai_pressure_mix | seat0 | eliminated | 4.1 | 0.982 | 0.000 | 3040/2750 |
| flat/3000/skai_pressure_mix/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.984 | 2750/3040 |
| flat/3000/water.leviathan/skai_expand_mix | seat0 | eliminated | 4.1 | 0.935 | 0.000 | 3040/3250 |
| flat/3000/skai_expand_mix/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.937 | 3250/3040 |
| flat/3000/water.leviathan/skai_air_mix | seat0 | eliminated | 4.0 | 0.900 | 0.000 | 3040/2820 |
| flat/3000/skai_air_mix/water.leviathan | seat1 | eliminated | 3.9 | 0.000 | 0.957 | 2820/3040 |
| flat/3000/water.leviathan/water_pressure_mix | seat0 | eliminated | 4.1 | 1.000 | 0.000 | 3040/2700 |
| flat/3000/water_pressure_mix/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 1.000 | 2700/3040 |
| flat/3000/water.leviathan/water_expand_mix | seat0 | eliminated | 8.6 | 0.926 | 0.000 | 3040/2595 |
| flat/3000/water_expand_mix/water.leviathan | seat1 | eliminated | 8.0 | 0.000 | 0.904 | 2595/3040 |
| flat/3000/water.leviathan/water_leviathan_mix | seat0 | eliminated | 11.0 | 0.500 | 0.000 | 3040/2470 |
| flat/3000/water_leviathan_mix/water.leviathan | seat1 | eliminated | 11.2 | 0.000 | 0.472 | 2470/3040 |
| flat/3000/water.reef_runner/water.shellback | seat1 | eliminated | 20.4 | 0.000 | 0.932 | 3000/2975 |
| flat/3000/water.shellback/water.reef_runner | seat0 | eliminated | 20.6 | 0.907 | 0.000 | 2975/3000 |
| flat/3000/water.reef_runner/water.spoutling | seat0 | eliminated | 10.0 | 0.773 | 0.000 | 3000/3000 |
| flat/3000/water.spoutling/water.reef_runner | seat1 | eliminated | 9.5 | 0.000 | 0.779 | 3000/3000 |
| flat/3000/water.reef_runner/skai_pressure_mix | seat0 | eliminated | 15.2 | 0.404 | 0.000 | 3000/2750 |
| flat/3000/skai_pressure_mix/water.reef_runner | seat1 | eliminated | 14.9 | 0.000 | 0.422 | 2750/3000 |
| flat/3000/water.reef_runner/skai_expand_mix | seat1 | eliminated | 35.2 | 0.000 | 0.308 | 3000/3250 |
| flat/3000/skai_expand_mix/water.reef_runner | seat0 | eliminated | 32.7 | 0.308 | 0.000 | 3250/3000 |
| flat/3000/water.reef_runner/skai_air_mix | seat1 | eliminated | 11.2 | 0.000 | 0.920 | 3000/2820 |
| flat/3000/skai_air_mix/water.reef_runner | seat0 | eliminated | 12.2 | 0.920 | 0.000 | 2820/3000 |
| flat/3000/water.reef_runner/water_pressure_mix | seat1 | eliminated | 14.1 | 0.000 | 0.525 | 3000/2700 |
| flat/3000/water_pressure_mix/water.reef_runner | seat0 | eliminated | 14.3 | 0.518 | 0.000 | 2700/3000 |
| flat/3000/water.reef_runner/water_expand_mix | seat1 | eliminated | 17.1 | 0.000 | 0.779 | 3000/2595 |
| flat/3000/water_expand_mix/water.reef_runner | seat0 | eliminated | 19.0 | 0.708 | 0.000 | 2595/3000 |
| flat/3000/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 8.3 | 0.000 | 0.939 | 3000/2470 |
| flat/3000/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 9.5 | 0.900 | 0.000 | 2470/3000 |
| flat/3000/water.shellback/water.spoutling | seat0 | eliminated | 16.8 | 0.996 | 0.000 | 2975/3000 |
| flat/3000/water.spoutling/water.shellback | seat1 | eliminated | 16.3 | 0.000 | 0.979 | 3000/2975 |
| flat/3000/water.shellback/skai_pressure_mix | seat0 | eliminated | 19.5 | 0.961 | 0.000 | 2975/2750 |
| flat/3000/skai_pressure_mix/water.shellback | seat1 | eliminated | 19.1 | 0.000 | 0.930 | 2750/2975 |
| flat/3000/water.shellback/skai_expand_mix | stalemate | dome_absorbed | 48.3 | 0.887 | 0.308 | 2975/3250 |
| flat/3000/skai_expand_mix/water.shellback | stalemate | dome_absorbed | 48.8 | 0.308 | 0.904 | 3250/2975 |
| flat/3000/water.shellback/skai_air_mix | seat1 | eliminated | 217.0 | 0.000 | 0.920 | 2975/2820 |
| flat/3000/skai_air_mix/water.shellback | seat0 | eliminated | 251.5 | 0.920 | 0.000 | 2820/2975 |
| flat/3000/water.shellback/water_pressure_mix | seat1 | eliminated | 32.2 | 0.000 | 0.354 | 2975/2700 |
| flat/3000/water_pressure_mix/water.shellback | seat0 | eliminated | 33.1 | 0.340 | 0.000 | 2700/2975 |
| flat/3000/water.shellback/water_expand_mix | seat0 | eliminated | 52.6 | 0.675 | 0.000 | 2975/2595 |
| flat/3000/water_expand_mix/water.shellback | seat1 | eliminated | 55.5 | 0.000 | 0.702 | 2595/2975 |
| flat/3000/water.shellback/water_leviathan_mix | seat0 | eliminated | 29.2 | 0.892 | 0.000 | 2975/2470 |
| flat/3000/water_leviathan_mix/water.shellback | seat1 | eliminated | 28.9 | 0.000 | 0.986 | 2470/2975 |
| flat/3000/water.spoutling/skai_pressure_mix | seat1 | eliminated | 10.1 | 0.000 | 0.743 | 3000/2750 |
| flat/3000/skai_pressure_mix/water.spoutling | seat0 | eliminated | 10.1 | 0.755 | 0.000 | 2750/3000 |
| flat/3000/water.spoutling/skai_expand_mix | seat1 | eliminated | 10.9 | 0.000 | 0.771 | 3000/3250 |
| flat/3000/skai_expand_mix/water.spoutling | seat0 | eliminated | 10.7 | 0.783 | 0.000 | 3250/3000 |
| flat/3000/water.spoutling/skai_air_mix | seat1 | eliminated | 8.3 | 0.000 | 0.818 | 3000/2820 |
| flat/3000/skai_air_mix/water.spoutling | seat0 | eliminated | 8.9 | 0.792 | 0.000 | 2820/3000 |
| flat/3000/water.spoutling/water_pressure_mix | seat1 | eliminated | 10.8 | 0.000 | 0.766 | 3000/2700 |
| flat/3000/water_pressure_mix/water.spoutling | seat0 | eliminated | 10.5 | 0.774 | 0.000 | 2700/3000 |
| flat/3000/water.spoutling/water_expand_mix | seat1 | eliminated | 12.5 | 0.000 | 0.953 | 3000/2595 |
| flat/3000/water_expand_mix/water.spoutling | seat0 | eliminated | 14.0 | 0.907 | 0.000 | 2595/3000 |
| flat/3000/water.spoutling/water_leviathan_mix | seat1 | eliminated | 7.0 | 0.000 | 1.000 | 3000/2470 |
| flat/3000/water_leviathan_mix/water.spoutling | seat0 | eliminated | 7.6 | 1.000 | 0.000 | 2470/3000 |
| flat/3000/skai_pressure_mix/skai_expand_mix | seat1 | eliminated | 17.4 | 0.000 | 0.431 | 2750/3250 |
| flat/3000/skai_expand_mix/skai_pressure_mix | seat0 | eliminated | 17.4 | 0.433 | 0.000 | 3250/2750 |
| flat/3000/skai_pressure_mix/skai_air_mix | seat1 | eliminated | 12.4 | 0.000 | 0.579 | 2750/2820 |
| flat/3000/skai_air_mix/skai_pressure_mix | seat0 | eliminated | 12.6 | 0.589 | 0.000 | 2820/2750 |
| flat/3000/skai_pressure_mix/water_pressure_mix | seat1 | eliminated | 13.3 | 0.000 | 0.591 | 2750/2700 |
| flat/3000/water_pressure_mix/skai_pressure_mix | seat0 | eliminated | 13.1 | 0.604 | 0.000 | 2700/2750 |
| flat/3000/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 15.2 | 0.000 | 0.874 | 2750/2595 |
| flat/3000/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 18.7 | 0.772 | 0.000 | 2595/2750 |
| flat/3000/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 9.3 | 0.000 | 0.963 | 2750/2470 |
| flat/3000/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 11.0 | 0.915 | 0.000 | 2470/2750 |
| flat/3000/skai_expand_mix/skai_air_mix | seat1 | eliminated | 30.5 | 0.000 | 0.148 | 3250/2820 |
| flat/3000/skai_air_mix/skai_expand_mix | seat0 | eliminated | 31.6 | 0.118 | 0.000 | 2820/3250 |
| flat/3000/skai_expand_mix/water_pressure_mix | seat0 | eliminated | 51.1 | 0.308 | 0.000 | 3250/2700 |
| flat/3000/water_pressure_mix/skai_expand_mix | seat1 | eliminated | 57.5 | 0.000 | 0.308 | 2700/3250 |
| flat/3000/skai_expand_mix/water_expand_mix | seat1 | eliminated | 25.0 | 0.000 | 0.755 | 3250/2595 |
| flat/3000/water_expand_mix/skai_expand_mix | seat0 | eliminated | 24.6 | 0.703 | 0.000 | 2595/3250 |
| flat/3000/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 10.8 | 0.000 | 0.740 | 3250/2470 |
| flat/3000/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 12.2 | 0.829 | 0.000 | 2470/3250 |
| flat/3000/skai_air_mix/water_pressure_mix | seat0 | eliminated | 14.1 | 0.920 | 0.000 | 2820/2700 |
| flat/3000/water_pressure_mix/skai_air_mix | seat1 | eliminated | 14.2 | 0.000 | 0.920 | 2700/2820 |
| flat/3000/skai_air_mix/water_expand_mix | seat1 | eliminated | 29.3 | 0.000 | 0.335 | 2820/2595 |
| flat/3000/water_expand_mix/skai_air_mix | seat0 | eliminated | 34.1 | 0.361 | 0.000 | 2595/2820 |
| flat/3000/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 10.6 | 0.000 | 0.580 | 2820/2470 |
| flat/3000/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 13.5 | 0.491 | 0.000 | 2470/2820 |
| flat/3000/water_pressure_mix/water_expand_mix | seat0 | eliminated | 34.8 | 0.046 | 0.000 | 2700/2595 |
| flat/3000/water_expand_mix/water_pressure_mix | seat1 | eliminated | 21.4 | 0.000 | 0.187 | 2595/2700 |
| flat/3000/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 9.9 | 0.000 | 0.828 | 2700/2470 |
| flat/3000/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 10.5 | 0.821 | 0.000 | 2470/2700 |
| flat/3000/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 17.8 | 0.000 | 0.771 | 2595/2470 |
| flat/3000/water_leviathan_mix/water_expand_mix | seat0 | eliminated | 19.9 | 0.691 | 0.000 | 2470/2595 |
| high_ground/2500/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.1 | 1.000 | 0.000 | 2600/2470 |
| high_ground/2500/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 13.0 | 0.000 | 1.000 | 2470/2600 |
| high_ground/2500/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 37.9 | 0.000 | 0.160 | 2600/2500 |
| high_ground/2500/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 27.3 | 0.338 | 0.000 | 2500/2600 |
| high_ground/2500/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 17.4 | 1.000 | 0.000 | 2600/2385 |
| high_ground/2500/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 18.1 | 0.000 | 1.000 | 2385/2600 |
| high_ground/2500/skai.roc_rider/skai.windrunner | seat0 | eliminated | 17.2 | 0.808 | 0.000 | 2600/2475 |
| high_ground/2500/skai.windrunner/skai.roc_rider | seat0 | eliminated | 21.1 | 0.382 | 0.000 | 2475/2600 |
| high_ground/2500/skai.roc_rider/water.grasper | seat0 | eliminated | 27.2 | 1.000 | 0.000 | 2600/2500 |
| high_ground/2500/water.grasper/skai.roc_rider | seat1 | eliminated | 30.0 | 0.000 | 1.000 | 2500/2600 |
| high_ground/2500/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 11.2 | 1.000 | 0.000 | 2600/2470 |
| high_ground/2500/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 15.4 | 0.000 | 1.000 | 2470/2600 |
| high_ground/2500/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.959 | 2600/2660 |
| high_ground/2500/water.leviathan/skai.roc_rider | seat0 | eliminated | 14.1 | 0.815 | 0.000 | 2660/2600 |
| high_ground/2500/skai.roc_rider/water.reef_runner | seat0 | eliminated | 17.1 | 1.000 | 0.000 | 2600/2475 |
| high_ground/2500/water.reef_runner/skai.roc_rider | seat1 | eliminated | 19.7 | 0.000 | 1.000 | 2475/2600 |
| high_ground/2500/skai.roc_rider/water.shellback | seat0 | eliminated | 146.7 | 1.000 | 0.000 | 2600/2450 |
| high_ground/2500/water.shellback/skai.roc_rider | seat1 | eliminated | 105.3 | 0.000 | 1.000 | 2450/2600 |
| high_ground/2500/skai.roc_rider/water.spoutling | seat0 | eliminated | 13.3 | 0.755 | 0.000 | 2600/2500 |
| high_ground/2500/water.spoutling/skai.roc_rider | seat1 | eliminated | 14.5 | 0.000 | 0.805 | 2500/2600 |
| high_ground/2500/skai.roc_rider/skai_pressure_mix | seat0 | eliminated | 21.0 | 0.639 | 0.000 | 2600/2750 |
| high_ground/2500/skai_pressure_mix/skai.roc_rider | seat0 | eliminated | 18.2 | 0.502 | 0.000 | 2750/2600 |
| high_ground/2500/skai.roc_rider/skai_expand_mix | seat0 | eliminated | 34.8 | 0.190 | 0.000 | 2600/2600 |
| high_ground/2500/skai_expand_mix/skai.roc_rider | seat0 | eliminated | 22.0 | 0.522 | 0.000 | 2600/2600 |
| high_ground/2500/skai.roc_rider/skai_air_mix | seat0 | eliminated | 25.7 | 0.622 | 0.000 | 2600/2820 |
| high_ground/2500/skai_air_mix/skai.roc_rider | seat0 | eliminated | 43.8 | 0.152 | 0.000 | 2820/2600 |
| high_ground/2500/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 28.8 | 1.000 | 0.000 | 2600/2700 |
| high_ground/2500/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 31.0 | 0.000 | 1.000 | 2700/2600 |
| high_ground/2500/skai.roc_rider/water_expand_mix | stalemate | dome_absorbed | 87.0 | 0.231 | 0.439 | 2600/2595 |
| high_ground/2500/water_expand_mix/skai.roc_rider | seat0 | eliminated | 32.7 | 0.724 | 0.000 | 2595/2600 |
| high_ground/2500/skai.roc_rider/water_leviathan_mix | seat1 | eliminated | 13.5 | 0.000 | 0.866 | 2600/2470 |
| high_ground/2500/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 22.3 | 0.746 | 0.000 | 2470/2600 |
| high_ground/2500/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 11.7 | 0.000 | 0.627 | 2470/2500 |
| high_ground/2500/skai.talon_archer/skai.stormcaller | seat1 | eliminated | 26.1 | 0.000 | 0.056 | 2500/2470 |
| high_ground/2500/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 9.1 | 0.000 | 1.000 | 2470/2385 |
| high_ground/2500/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 8.3 | 1.000 | 0.000 | 2385/2470 |
| high_ground/2500/skai.stormcaller/skai.windrunner | seat0 | eliminated | 25.2 | 0.672 | 0.000 | 2470/2475 |
| high_ground/2500/skai.windrunner/skai.stormcaller | seat0 | eliminated | 19.4 | 0.074 | 0.000 | 2475/2470 |
| high_ground/2500/skai.stormcaller/water.grasper | seat0 | eliminated | 17.7 | 1.000 | 0.000 | 2470/2500 |
| high_ground/2500/water.grasper/skai.stormcaller | seat1 | eliminated | 16.1 | 0.000 | 0.221 | 2500/2470 |
| high_ground/2500/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.5 | 0.000 | 0.634 | 2470/2470 |
| high_ground/2500/water.ink_lobber/skai.stormcaller | seat1 | eliminated | 12.0 | 0.000 | 0.598 | 2470/2470 |
| high_ground/2500/skai.stormcaller/water.leviathan | seat1 | eliminated | 5.2 | 0.000 | 1.000 | 2470/2660 |
| high_ground/2500/water.leviathan/skai.stormcaller | seat0 | eliminated | 16.6 | 0.549 | 0.000 | 2660/2470 |
| high_ground/2500/skai.stormcaller/water.reef_runner | seat0 | eliminated | 25.2 | 0.672 | 0.000 | 2470/2475 |
| high_ground/2500/water.reef_runner/skai.stormcaller | seat1 | eliminated | 12.7 | 0.000 | 0.442 | 2475/2470 |
| high_ground/2500/skai.stormcaller/water.shellback | stalemate | dome_absorbed | 30.0 | 1.000 | 1.000 | 2470/2450 |
| high_ground/2500/water.shellback/skai.stormcaller | seat0 | eliminated | 19.6 | 1.000 | 0.000 | 2450/2470 |
| high_ground/2500/skai.stormcaller/water.spoutling | seat0 | eliminated | 20.2 | 0.457 | 0.000 | 2470/2500 |
| high_ground/2500/water.spoutling/skai.stormcaller | seat1 | eliminated | 10.3 | 0.000 | 0.872 | 2500/2470 |
| high_ground/2500/skai.stormcaller/skai_pressure_mix | seat1 | eliminated | 27.8 | 0.000 | 0.238 | 2470/2750 |
| high_ground/2500/skai_pressure_mix/skai.stormcaller | seat0 | eliminated | 18.6 | 0.098 | 0.000 | 2750/2470 |
| high_ground/2500/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 12.4 | 0.000 | 0.636 | 2470/2600 |
| high_ground/2500/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 16.0 | 0.347 | 0.000 | 2600/2470 |
| high_ground/2500/skai.stormcaller/skai_air_mix | seat1 | eliminated | 11.4 | 0.000 | 0.920 | 2470/2820 |
| high_ground/2500/skai_air_mix/skai.stormcaller | seat0 | eliminated | 9.7 | 0.920 | 0.000 | 2820/2470 |
| high_ground/2500/skai.stormcaller/water_pressure_mix | seat0 | eliminated | 20.2 | 1.000 | 0.000 | 2470/2700 |
| high_ground/2500/water_pressure_mix/skai.stormcaller | seat0 | eliminated | 14.0 | 0.278 | 0.000 | 2700/2470 |
| high_ground/2500/skai.stormcaller/water_expand_mix | seat1 | eliminated | 23.7 | 0.000 | 0.575 | 2470/2595 |
| high_ground/2500/water_expand_mix/skai.stormcaller | seat0 | eliminated | 20.4 | 0.622 | 0.000 | 2595/2470 |
| high_ground/2500/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 9.7 | 0.000 | 1.000 | 2470/2470 |
| high_ground/2500/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 11.9 | 0.875 | 0.000 | 2470/2470 |
| high_ground/2500/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 13.8 | 0.000 | 0.612 | 2500/2385 |
| high_ground/2500/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 13.0 | 0.532 | 0.000 | 2385/2500 |
| high_ground/2500/skai.talon_archer/skai.windrunner | seat0 | eliminated | 20.9 | 0.811 | 0.000 | 2500/2475 |
| high_ground/2500/skai.windrunner/skai.talon_archer | seat1 | eliminated | 21.9 | 0.000 | 0.298 | 2475/2500 |
| high_ground/2500/skai.talon_archer/water.grasper | seat0 | eliminated | 28.7 | 1.000 | 0.000 | 2500/2500 |
| high_ground/2500/water.grasper/skai.talon_archer | seat0 | eliminated | 18.2 | 0.542 | 0.000 | 2500/2500 |
| high_ground/2500/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 12.5 | 0.000 | 0.610 | 2500/2470 |
| high_ground/2500/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 13.3 | 0.000 | 0.690 | 2470/2500 |
| high_ground/2500/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.9 | 0.000 | 1.000 | 2500/2660 |
| high_ground/2500/water.leviathan/skai.talon_archer | seat0 | eliminated | 15.0 | 0.591 | 0.000 | 2660/2500 |
| high_ground/2500/skai.talon_archer/water.reef_runner | seat0 | eliminated | 20.9 | 0.811 | 0.000 | 2500/2475 |
| high_ground/2500/water.reef_runner/skai.talon_archer | seat1 | eliminated | 19.1 | 0.000 | 0.433 | 2475/2500 |
| high_ground/2500/skai.talon_archer/water.shellback | stalemate | dome_absorbed | 45.1 | 0.960 | 0.990 | 2500/2450 |
| high_ground/2500/water.shellback/skai.talon_archer | seat0 | eliminated | 29.0 | 0.925 | 0.000 | 2450/2500 |
| high_ground/2500/skai.talon_archer/water.spoutling | seat0 | eliminated | 15.1 | 0.756 | 0.000 | 2500/2500 |
| high_ground/2500/water.spoutling/skai.talon_archer | seat1 | eliminated | 11.6 | 0.000 | 0.891 | 2500/2500 |
| high_ground/2500/skai.talon_archer/skai_pressure_mix | seat0 | eliminated | 26.1 | 0.477 | 0.000 | 2500/2750 |
| high_ground/2500/skai_pressure_mix/skai.talon_archer | seat1 | eliminated | 32.0 | 0.000 | 0.111 | 2750/2500 |
| high_ground/2500/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 27.7 | 0.000 | 0.276 | 2500/2600 |
| high_ground/2500/skai_expand_mix/skai.talon_archer | seat1 | eliminated | 23.0 | 0.000 | 0.345 | 2600/2500 |
| high_ground/2500/skai.talon_archer/skai_air_mix | seat1 | eliminated | 14.8 | 0.000 | 0.609 | 2500/2820 |
| high_ground/2500/skai_air_mix/skai.talon_archer | seat0 | eliminated | 16.2 | 0.498 | 0.000 | 2820/2500 |
| high_ground/2500/skai.talon_archer/water_pressure_mix | seat0 | eliminated | 29.5 | 0.947 | 0.000 | 2500/2700 |
| high_ground/2500/water_pressure_mix/skai.talon_archer | seat0 | eliminated | 16.7 | 0.603 | 0.000 | 2700/2500 |
| high_ground/2500/skai.talon_archer/water_expand_mix | seat1 | eliminated | 33.1 | 0.000 | 0.850 | 2500/2595 |
| high_ground/2500/water_expand_mix/skai.talon_archer | seat0 | eliminated | 29.1 | 0.657 | 0.000 | 2595/2500 |
| high_ground/2500/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 11.3 | 0.000 | 0.998 | 2500/2470 |
| high_ground/2500/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 19.3 | 0.707 | 0.000 | 2470/2500 |
| high_ground/2500/skai.thunderhead/skai.windrunner | seat0 | eliminated | 12.2 | 0.871 | 0.000 | 2385/2475 |
| high_ground/2500/skai.windrunner/skai.thunderhead | seat1 | eliminated | 8.0 | 0.000 | 0.755 | 2475/2385 |
| high_ground/2500/skai.thunderhead/water.grasper | seat0 | eliminated | 10.5 | 1.000 | 0.000 | 2385/2500 |
| high_ground/2500/water.grasper/skai.thunderhead | seat1 | eliminated | 10.7 | 0.000 | 1.000 | 2500/2385 |
| high_ground/2500/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 8.3 | 1.000 | 0.000 | 2385/2470 |
| high_ground/2500/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 10.5 | 0.000 | 1.000 | 2470/2385 |
| high_ground/2500/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.788 | 2385/2660 |
| high_ground/2500/water.leviathan/skai.thunderhead | seat1 | eliminated | 19.2 | 0.000 | 0.351 | 2660/2385 |
| high_ground/2500/skai.thunderhead/water.reef_runner | seat0 | eliminated | 12.2 | 1.000 | 0.000 | 2385/2475 |
| high_ground/2500/water.reef_runner/skai.thunderhead | seat1 | eliminated | 8.3 | 0.000 | 1.000 | 2475/2385 |
| high_ground/2500/skai.thunderhead/water.shellback | seat0 | eliminated | 33.1 | 1.000 | 0.000 | 2385/2450 |
| high_ground/2500/water.shellback/skai.thunderhead | seat1 | eliminated | 40.0 | 0.000 | 1.000 | 2450/2385 |
| high_ground/2500/skai.thunderhead/water.spoutling | seat0 | eliminated | 7.7 | 0.883 | 0.000 | 2385/2500 |
| high_ground/2500/water.spoutling/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 0.908 | 2500/2385 |
| high_ground/2500/skai.thunderhead/skai_pressure_mix | seat0 | eliminated | 13.1 | 0.742 | 0.000 | 2385/2750 |
| high_ground/2500/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 11.1 | 0.000 | 0.654 | 2750/2385 |
| high_ground/2500/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 42.3 | 0.000 | 0.308 | 2385/2600 |
| high_ground/2500/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 20.2 | 0.467 | 0.000 | 2600/2385 |
| high_ground/2500/skai.thunderhead/skai_air_mix | seat1 | eliminated | 26.4 | 0.000 | 0.920 | 2385/2820 |
| high_ground/2500/skai_air_mix/skai.thunderhead | seat0 | eliminated | 25.0 | 0.920 | 0.000 | 2820/2385 |
| high_ground/2500/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 12.2 | 1.000 | 0.000 | 2385/2700 |
| high_ground/2500/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 10.4 | 0.000 | 1.000 | 2700/2385 |
| high_ground/2500/skai.thunderhead/water_expand_mix | seat0 | eliminated | 15.3 | 0.871 | 0.000 | 2385/2595 |
| high_ground/2500/water_expand_mix/skai.thunderhead | seat1 | eliminated | 21.4 | 0.000 | 0.775 | 2595/2385 |
| high_ground/2500/skai.thunderhead/water_leviathan_mix | seat1 | eliminated | 13.6 | 0.000 | 0.501 | 2385/2470 |
| high_ground/2500/water_leviathan_mix/skai.thunderhead | seat0 | eliminated | 27.4 | 0.315 | 0.000 | 2470/2385 |
| high_ground/2500/skai.windrunner/water.grasper | seat0 | eliminated | 20.5 | 1.000 | 0.000 | 2475/2500 |
| high_ground/2500/water.grasper/skai.windrunner | seat0 | eliminated | 16.4 | 0.561 | 0.000 | 2500/2475 |
| high_ground/2500/skai.windrunner/water.ink_lobber | seat1 | eliminated | 8.9 | 0.000 | 0.676 | 2475/2470 |
| high_ground/2500/water.ink_lobber/skai.windrunner | seat0 | eliminated | 30.6 | 0.742 | 0.000 | 2470/2475 |
| high_ground/2500/skai.windrunner/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 2475/2660 |
| high_ground/2500/water.leviathan/skai.windrunner | seat0 | eliminated | 13.4 | 0.916 | 0.000 | 2660/2475 |
| high_ground/2500/skai.windrunner/water.reef_runner | seat0 | eliminated | 18.5 | 0.312 | 0.000 | 2475/2475 |
| high_ground/2500/water.reef_runner/skai.windrunner | seat0 | eliminated | 20.2 | 0.267 | 0.000 | 2475/2475 |
| high_ground/2500/skai.windrunner/water.shellback | stalemate | dome_absorbed | 46.2 | 0.818 | 0.934 | 2475/2450 |
| high_ground/2500/water.shellback/skai.windrunner | seat0 | eliminated | 27.3 | 0.963 | 0.000 | 2450/2475 |
| high_ground/2500/skai.windrunner/water.spoutling | seat0 | eliminated | 11.3 | 0.740 | 0.000 | 2475/2500 |
| high_ground/2500/water.spoutling/skai.windrunner | seat0 | eliminated | 59.9 | 0.320 | 0.000 | 2500/2475 |
| high_ground/2500/skai.windrunner/skai_pressure_mix | seat0 | eliminated | 36.7 | 0.013 | 0.000 | 2475/2750 |
| high_ground/2500/skai_pressure_mix/skai.windrunner | seat0 | eliminated | 16.6 | 0.449 | 0.000 | 2750/2475 |
| high_ground/2500/skai.windrunner/skai_expand_mix | seat1 | eliminated | 32.0 | 0.000 | 0.064 | 2475/2600 |
| high_ground/2500/skai_expand_mix/skai.windrunner | seat0 | eliminated | 17.6 | 0.769 | 0.000 | 2600/2475 |
| high_ground/2500/skai.windrunner/skai_air_mix | seat1 | eliminated | 12.9 | 0.000 | 0.617 | 2475/2820 |
| high_ground/2500/skai_air_mix/skai.windrunner | seat0 | eliminated | 13.0 | 0.830 | 0.000 | 2820/2475 |
| high_ground/2500/skai.windrunner/water_pressure_mix | seat0 | eliminated | 22.7 | 0.752 | 0.000 | 2475/2700 |
| high_ground/2500/water_pressure_mix/skai.windrunner | seat0 | eliminated | 15.9 | 0.594 | 0.000 | 2700/2475 |
| high_ground/2500/skai.windrunner/water_expand_mix | seat1 | eliminated | 30.9 | 0.000 | 0.785 | 2475/2595 |
| high_ground/2500/water_expand_mix/skai.windrunner | seat0 | eliminated | 25.3 | 0.653 | 0.000 | 2595/2475 |
| high_ground/2500/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 7.8 | 0.000 | 1.000 | 2475/2470 |
| high_ground/2500/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 15.6 | 0.918 | 0.000 | 2470/2475 |
| high_ground/2500/water.grasper/water.ink_lobber | seat1 | eliminated | 12.4 | 0.000 | 0.582 | 2500/2470 |
| high_ground/2500/water.ink_lobber/water.grasper | seat0 | eliminated | 18.6 | 1.000 | 0.000 | 2470/2500 |
| high_ground/2500/water.grasper/water.leviathan | seat1 | eliminated | 5.7 | 0.000 | 1.000 | 2500/2660 |
| high_ground/2500/water.leviathan/water.grasper | seat0 | eliminated | 10.4 | 1.000 | 0.000 | 2660/2500 |
| high_ground/2500/water.grasper/water.reef_runner | seat0 | eliminated | 16.0 | 0.561 | 0.000 | 2500/2475 |
| high_ground/2500/water.reef_runner/water.grasper | seat0 | eliminated | 21.6 | 1.000 | 0.000 | 2475/2500 |
| high_ground/2500/water.grasper/water.shellback | seat0 | eliminated | 25.3 | 0.558 | 0.000 | 2500/2450 |
| high_ground/2500/water.shellback/water.grasper | seat0 | eliminated | 47.9 | 0.452 | 0.000 | 2450/2500 |
| high_ground/2500/water.grasper/water.spoutling | seat0 | eliminated | 13.6 | 0.775 | 0.000 | 2500/2500 |
| high_ground/2500/water.spoutling/water.grasper | seat0 | eliminated | 40.4 | 1.000 | 0.000 | 2500/2500 |
| high_ground/2500/water.grasper/skai_pressure_mix | seat0 | eliminated | 17.9 | 0.479 | 0.000 | 2500/2750 |
| high_ground/2500/skai_pressure_mix/water.grasper | seat0 | eliminated | 19.9 | 1.000 | 0.000 | 2750/2500 |
| high_ground/2500/water.grasper/skai_expand_mix | seat1 | eliminated | 72.5 | 0.000 | 0.308 | 2500/2600 |
| high_ground/2500/skai_expand_mix/water.grasper | seat0 | eliminated | 25.0 | 1.000 | 0.000 | 2600/2500 |
| high_ground/2500/water.grasper/skai_air_mix | seat1 | eliminated | 17.8 | 0.000 | 0.920 | 2500/2820 |
| high_ground/2500/skai_air_mix/water.grasper | seat0 | eliminated | 15.8 | 1.000 | 0.000 | 2820/2500 |
| high_ground/2500/water.grasper/water_pressure_mix | seat0 | eliminated | 45.0 | 0.024 | 0.000 | 2500/2700 |
| high_ground/2500/water_pressure_mix/water.grasper | seat0 | eliminated | 23.7 | 0.451 | 0.000 | 2700/2500 |
| high_ground/2500/water.grasper/water_expand_mix | seat0 | eliminated | 28.9 | 0.175 | 0.000 | 2500/2595 |
| high_ground/2500/water_expand_mix/water.grasper | seat0 | eliminated | 27.1 | 0.744 | 0.000 | 2595/2500 |
| high_ground/2500/water.grasper/water_leviathan_mix | seat1 | eliminated | 11.2 | 0.000 | 0.864 | 2500/2470 |
| high_ground/2500/water_leviathan_mix/water.grasper | seat0 | eliminated | 13.9 | 0.994 | 0.000 | 2470/2500 |
| high_ground/2500/water.ink_lobber/water.leviathan | seat1 | eliminated | 5.9 | 0.000 | 1.000 | 2470/2660 |
| high_ground/2500/water.leviathan/water.ink_lobber | seat0 | eliminated | 18.1 | 0.443 | 0.000 | 2660/2470 |
| high_ground/2500/water.ink_lobber/water.reef_runner | seat0 | eliminated | 30.6 | 0.742 | 0.000 | 2470/2475 |
| high_ground/2500/water.reef_runner/water.ink_lobber | seat1 | eliminated | 11.2 | 0.000 | 0.655 | 2475/2470 |
| high_ground/2500/water.ink_lobber/water.shellback | seat0 | eliminated | 132.6 | 1.000 | 0.000 | 2470/2450 |
| high_ground/2500/water.shellback/water.ink_lobber | seat0 | eliminated | 21.3 | 0.858 | 0.000 | 2450/2470 |
| high_ground/2500/water.ink_lobber/water.spoutling | seat0 | eliminated | 21.6 | 0.481 | 0.000 | 2470/2500 |
| high_ground/2500/water.spoutling/water.ink_lobber | seat1 | eliminated | 8.8 | 0.000 | 0.941 | 2500/2470 |
| high_ground/2500/water.ink_lobber/skai_pressure_mix | seat1 | eliminated | 32.4 | 0.000 | 0.153 | 2470/2750 |
| high_ground/2500/skai_pressure_mix/water.ink_lobber | seat1 | eliminated | 14.5 | 0.000 | 0.515 | 2750/2470 |
| high_ground/2500/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 14.4 | 0.000 | 0.667 | 2470/2600 |
| high_ground/2500/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 24.4 | 0.308 | 0.000 | 2600/2470 |
| high_ground/2500/water.ink_lobber/skai_air_mix | seat1 | eliminated | 13.0 | 0.000 | 0.920 | 2470/2820 |
| high_ground/2500/skai_air_mix/water.ink_lobber | seat0 | eliminated | 10.0 | 0.920 | 0.000 | 2820/2470 |
| high_ground/2500/water.ink_lobber/water_pressure_mix | seat0 | eliminated | 21.6 | 1.000 | 0.000 | 2470/2700 |
| high_ground/2500/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 17.8 | 0.000 | 0.348 | 2700/2470 |
| high_ground/2500/water.ink_lobber/water_expand_mix | seat1 | eliminated | 27.9 | 0.000 | 0.574 | 2470/2595 |
| high_ground/2500/water_expand_mix/water.ink_lobber | seat0 | eliminated | 21.9 | 0.580 | 0.000 | 2595/2470 |
| high_ground/2500/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 10.4 | 0.000 | 1.000 | 2470/2470 |
| high_ground/2500/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 16.3 | 0.556 | 0.000 | 2470/2470 |
| high_ground/2500/water.leviathan/water.reef_runner | seat0 | eliminated | 13.4 | 0.916 | 0.000 | 2660/2475 |
| high_ground/2500/water.reef_runner/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 2475/2660 |
| high_ground/2500/water.leviathan/water.shellback | seat1 | eliminated | 114.7 | 0.000 | 0.442 | 2660/2450 |
| high_ground/2500/water.shellback/water.leviathan | seat1 | eliminated | 18.6 | 0.000 | 0.708 | 2450/2660 |
| high_ground/2500/water.leviathan/water.spoutling | seat0 | eliminated | 10.4 | 0.866 | 0.000 | 2660/2500 |
| high_ground/2500/water.spoutling/water.leviathan | seat1 | eliminated | 4.9 | 0.000 | 1.000 | 2500/2660 |
| high_ground/2500/water.leviathan/skai_pressure_mix | seat0 | eliminated | 13.4 | 0.825 | 0.000 | 2660/2750 |
| high_ground/2500/skai_pressure_mix/water.leviathan | seat1 | eliminated | 5.9 | 0.000 | 1.000 | 2750/2660 |
| high_ground/2500/water.leviathan/skai_expand_mix | seat0 | eliminated | 13.4 | 0.753 | 0.000 | 2660/2600 |
| high_ground/2500/skai_expand_mix/water.leviathan | seat1 | eliminated | 4.8 | 0.000 | 0.952 | 2600/2660 |
| high_ground/2500/water.leviathan/skai_air_mix | seat0 | eliminated | 15.0 | 0.558 | 0.000 | 2660/2820 |
| high_ground/2500/skai_air_mix/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.876 | 2820/2660 |
| high_ground/2500/water.leviathan/water_pressure_mix | seat0 | eliminated | 10.4 | 0.998 | 0.000 | 2660/2700 |
| high_ground/2500/water_pressure_mix/water.leviathan | seat1 | eliminated | 5.6 | 0.000 | 1.000 | 2700/2660 |
| high_ground/2500/water.leviathan/water_expand_mix | seat0 | eliminated | 18.1 | 0.770 | 0.000 | 2660/2595 |
| high_ground/2500/water_expand_mix/water.leviathan | seat1 | eliminated | 8.9 | 0.000 | 1.000 | 2595/2660 |
| high_ground/2500/water.leviathan/water_leviathan_mix | seat1 | eliminated | 17.8 | 0.000 | 1.000 | 2660/2470 |
| high_ground/2500/water_leviathan_mix/water.leviathan | seat1 | eliminated | 11.1 | 0.000 | 1.000 | 2470/2660 |
| high_ground/2500/water.reef_runner/water.shellback | stalemate | dome_absorbed | 56.8 | 0.788 | 0.960 | 2475/2450 |
| high_ground/2500/water.shellback/water.reef_runner | seat0 | eliminated | 27.5 | 0.960 | 0.000 | 2450/2475 |
| high_ground/2500/water.reef_runner/water.spoutling | seat0 | eliminated | 12.7 | 0.722 | 0.000 | 2475/2500 |
| high_ground/2500/water.spoutling/water.reef_runner | seat0 | eliminated | 59.9 | 0.320 | 0.000 | 2500/2475 |
| high_ground/2500/water.reef_runner/skai_pressure_mix | seat1 | eliminated | 24.1 | 0.000 | 0.255 | 2475/2750 |
| high_ground/2500/skai_pressure_mix/water.reef_runner | seat0 | eliminated | 16.6 | 0.449 | 0.000 | 2750/2475 |
| high_ground/2500/water.reef_runner/skai_expand_mix | seat1 | eliminated | 26.1 | 0.000 | 0.308 | 2475/2600 |
| high_ground/2500/skai_expand_mix/water.reef_runner | seat0 | eliminated | 17.6 | 0.769 | 0.000 | 2600/2475 |
| high_ground/2500/water.reef_runner/skai_air_mix | seat1 | eliminated | 11.1 | 0.000 | 0.920 | 2475/2820 |
| high_ground/2500/skai_air_mix/water.reef_runner | seat0 | eliminated | 13.1 | 0.920 | 0.000 | 2820/2475 |
| high_ground/2500/water.reef_runner/water_pressure_mix | seat0 | eliminated | 24.0 | 0.739 | 0.000 | 2475/2700 |
| high_ground/2500/water_pressure_mix/water.reef_runner | seat0 | eliminated | 15.5 | 0.595 | 0.000 | 2700/2475 |
| high_ground/2500/water.reef_runner/water_expand_mix | seat1 | eliminated | 29.7 | 0.000 | 0.797 | 2475/2595 |
| high_ground/2500/water_expand_mix/water.reef_runner | seat0 | eliminated | 25.3 | 0.653 | 0.000 | 2595/2475 |
| high_ground/2500/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 7.2 | 0.000 | 1.000 | 2475/2470 |
| high_ground/2500/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 15.6 | 0.918 | 0.000 | 2470/2475 |
| high_ground/2500/water.shellback/water.spoutling | seat0 | eliminated | 22.3 | 1.000 | 0.000 | 2450/2500 |
| high_ground/2500/water.spoutling/water.shellback | stalemate | dome_absorbed | 41.5 | 0.950 | 1.000 | 2500/2450 |
| high_ground/2500/water.shellback/skai_pressure_mix | seat0 | eliminated | 29.3 | 0.922 | 0.000 | 2450/2750 |
| high_ground/2500/skai_pressure_mix/water.shellback | seat0 | eliminated | 102.8 | 0.791 | 0.000 | 2750/2450 |
| high_ground/2500/water.shellback/skai_expand_mix | stalemate | dome_absorbed | 211.0 | 0.786 | 0.308 | 2450/2600 |
| high_ground/2500/skai_expand_mix/water.shellback | stalemate | dome_absorbed | 30.0 | 1.000 | 1.000 | 2600/2450 |
| high_ground/2500/water.shellback/skai_air_mix | seat1 | eliminated | 65.8 | 0.000 | 0.920 | 2450/2820 |
| high_ground/2500/skai_air_mix/water.shellback | seat0 | eliminated | 72.8 | 1.000 | 0.000 | 2820/2450 |
| high_ground/2500/water.shellback/water_pressure_mix | seat0 | eliminated | 49.7 | 0.463 | 0.000 | 2450/2700 |
| high_ground/2500/water_pressure_mix/water.shellback | seat0 | eliminated | 26.1 | 0.610 | 0.000 | 2700/2450 |
| high_ground/2500/water.shellback/water_expand_mix | seat0 | eliminated | 63.9 | 0.661 | 0.000 | 2450/2595 |
| high_ground/2500/water_expand_mix/water.shellback | seat0 | eliminated | 82.7 | 0.784 | 0.000 | 2595/2450 |
| high_ground/2500/water.shellback/water_leviathan_mix | seat0 | eliminated | 37.7 | 0.832 | 0.000 | 2450/2470 |
| high_ground/2500/water_leviathan_mix/water.shellback | unresolved | time_cap | 300.0 | 0.672 | 0.998 | 2470/2450 |
| high_ground/2500/water.spoutling/skai_pressure_mix | seat1 | eliminated | 19.6 | 0.000 | 0.762 | 2500/2750 |
| high_ground/2500/skai_pressure_mix/water.spoutling | seat0 | eliminated | 11.0 | 0.780 | 0.000 | 2750/2500 |
| high_ground/2500/water.spoutling/skai_expand_mix | seat1 | eliminated | 12.3 | 0.000 | 0.864 | 2500/2600 |
| high_ground/2500/skai_expand_mix/water.spoutling | seat0 | eliminated | 13.3 | 0.738 | 0.000 | 2600/2500 |
| high_ground/2500/water.spoutling/skai_air_mix | seat1 | eliminated | 10.6 | 0.000 | 0.900 | 2500/2820 |
| high_ground/2500/skai_air_mix/water.spoutling | seat0 | eliminated | 10.1 | 0.816 | 0.000 | 2820/2500 |
| high_ground/2500/water.spoutling/water_pressure_mix | seat0 | eliminated | 42.6 | 0.896 | 0.000 | 2500/2700 |
| high_ground/2500/water_pressure_mix/water.spoutling | seat0 | eliminated | 12.3 | 0.800 | 0.000 | 2700/2500 |
| high_ground/2500/water.spoutling/water_expand_mix | seat1 | eliminated | 21.5 | 0.000 | 0.961 | 2500/2595 |
| high_ground/2500/water_expand_mix/water.spoutling | seat0 | eliminated | 17.7 | 0.855 | 0.000 | 2595/2500 |
| high_ground/2500/water.spoutling/water_leviathan_mix | seat1 | eliminated | 7.6 | 0.000 | 1.000 | 2500/2470 |
| high_ground/2500/water_leviathan_mix/water.spoutling | seat0 | eliminated | 12.6 | 0.906 | 0.000 | 2470/2500 |
| high_ground/2500/skai_pressure_mix/skai_expand_mix | seat0 | eliminated | 23.8 | 0.229 | 0.000 | 2750/2600 |
| high_ground/2500/skai_expand_mix/skai_pressure_mix | seat0 | eliminated | 20.9 | 0.642 | 0.000 | 2600/2750 |
| high_ground/2500/skai_pressure_mix/skai_air_mix | seat1 | eliminated | 17.3 | 0.000 | 0.524 | 2750/2820 |
| high_ground/2500/skai_air_mix/skai_pressure_mix | seat0 | eliminated | 14.8 | 0.731 | 0.000 | 2820/2750 |
| high_ground/2500/skai_pressure_mix/water_pressure_mix | seat0 | eliminated | 22.1 | 0.782 | 0.000 | 2750/2700 |
| high_ground/2500/water_pressure_mix/skai_pressure_mix | seat0 | eliminated | 16.5 | 0.535 | 0.000 | 2700/2750 |
| high_ground/2500/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 34.9 | 0.000 | 0.748 | 2750/2595 |
| high_ground/2500/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 28.2 | 0.630 | 0.000 | 2595/2750 |
| high_ground/2500/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 11.0 | 0.000 | 1.000 | 2750/2470 |
| high_ground/2500/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 16.2 | 0.809 | 0.000 | 2470/2750 |
| high_ground/2500/skai_expand_mix/skai_air_mix | seat0 | eliminated | 25.6 | 0.246 | 0.000 | 2600/2820 |
| high_ground/2500/skai_air_mix/skai_expand_mix | seat0 | eliminated | 17.2 | 0.556 | 0.000 | 2820/2600 |
| high_ground/2500/skai_expand_mix/water_pressure_mix | seat0 | eliminated | 26.4 | 0.925 | 0.000 | 2600/2700 |
| high_ground/2500/water_pressure_mix/skai_expand_mix | seat1 | eliminated | 68.5 | 0.000 | 0.308 | 2700/2600 |
| high_ground/2500/skai_expand_mix/water_expand_mix | seat1 | eliminated | 43.5 | 0.000 | 0.600 | 2600/2595 |
| high_ground/2500/water_expand_mix/skai_expand_mix | seat0 | eliminated | 31.8 | 0.647 | 0.000 | 2595/2600 |
| high_ground/2500/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 10.8 | 0.000 | 0.923 | 2600/2470 |
| high_ground/2500/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 19.3 | 0.785 | 0.000 | 2470/2600 |
| high_ground/2500/skai_air_mix/water_pressure_mix | seat0 | eliminated | 15.9 | 0.995 | 0.000 | 2820/2700 |
| high_ground/2500/water_pressure_mix/skai_air_mix | seat1 | eliminated | 18.6 | 0.000 | 0.920 | 2700/2820 |
| high_ground/2500/skai_air_mix/water_expand_mix | seat0 | eliminated | 40.4 | 0.468 | 0.000 | 2820/2595 |
| high_ground/2500/water_expand_mix/skai_air_mix | seat1 | eliminated | 28.0 | 0.000 | 0.703 | 2595/2820 |
| high_ground/2500/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 10.6 | 0.000 | 0.855 | 2820/2470 |
| high_ground/2500/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 19.3 | 0.583 | 0.000 | 2470/2820 |
| high_ground/2500/water_pressure_mix/water_expand_mix | seat0 | eliminated | 35.9 | 0.143 | 0.000 | 2700/2595 |
| high_ground/2500/water_expand_mix/water_pressure_mix | seat0 | eliminated | 28.2 | 0.758 | 0.000 | 2595/2700 |
| high_ground/2500/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 11.1 | 0.000 | 0.886 | 2700/2470 |
| high_ground/2500/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 14.2 | 0.994 | 0.000 | 2470/2700 |
| high_ground/2500/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 21.4 | 0.000 | 0.755 | 2595/2470 |
| high_ground/2500/water_leviathan_mix/water_expand_mix | seat0 | eliminated | 27.1 | 0.769 | 0.000 | 2470/2595 |
| flooded/2500/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 20.5 | 1.000 | 0.000 | 2600/2470 |
| flooded/2500/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.3 | 0.000 | 1.000 | 2470/2600 |
| flooded/2500/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 17.8 | 0.000 | 0.919 | 2600/2500 |
| flooded/2500/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 20.4 | 0.000 | 0.813 | 2500/2600 |
| flooded/2500/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 27.8 | 1.000 | 0.000 | 2600/2385 |
| flooded/2500/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 18.3 | 0.000 | 1.000 | 2385/2600 |
| flooded/2500/skai.roc_rider/skai.windrunner | seat1 | eliminated | 14.5 | 0.000 | 1.000 | 2600/2475 |
| flooded/2500/skai.windrunner/skai.roc_rider | seat1 | eliminated | 23.2 | 0.000 | 0.656 | 2475/2600 |
| flooded/2500/skai.roc_rider/water.grasper | seat0 | eliminated | 39.8 | 1.000 | 0.000 | 2600/2500 |
| flooded/2500/water.grasper/skai.roc_rider | seat1 | eliminated | 29.1 | 0.000 | 1.000 | 2500/2600 |
| flooded/2500/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 21.8 | 1.000 | 0.000 | 2600/2470 |
| flooded/2500/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 11.1 | 0.000 | 1.000 | 2470/2600 |
| flooded/2500/skai.roc_rider/water.leviathan | seat1 | eliminated | 7.1 | 0.000 | 1.000 | 2600/2660 |
| flooded/2500/water.leviathan/skai.roc_rider | seat0 | eliminated | 9.1 | 0.724 | 0.000 | 2660/2600 |
| flooded/2500/skai.roc_rider/water.reef_runner | seat1 | eliminated | 14.5 | 0.000 | 1.000 | 2600/2475 |
| flooded/2500/water.reef_runner/skai.roc_rider | seat1 | eliminated | 26.6 | 0.000 | 0.530 | 2475/2600 |
| flooded/2500/skai.roc_rider/water.shellback | seat1 | eliminated | 28.7 | 0.000 | 1.000 | 2600/2450 |
| flooded/2500/water.shellback/skai.roc_rider | seat0 | eliminated | 26.6 | 0.929 | 0.000 | 2450/2600 |
| flooded/2500/skai.roc_rider/water.spoutling | seat1 | eliminated | 25.7 | 0.000 | 0.694 | 2600/2500 |
| flooded/2500/water.spoutling/skai.roc_rider | seat1 | eliminated | 11.8 | 0.000 | 1.000 | 2500/2600 |
| flooded/2500/skai.roc_rider/skai_pressure_mix | seat1 | eliminated | 13.8 | 0.000 | 1.000 | 2600/2750 |
| flooded/2500/skai_pressure_mix/skai.roc_rider | seat1 | eliminated | 21.7 | 0.000 | 0.607 | 2750/2600 |
| flooded/2500/skai.roc_rider/skai_expand_mix | seat1 | eliminated | 16.7 | 0.000 | 0.987 | 2600/2600 |
| flooded/2500/skai_expand_mix/skai.roc_rider | seat1 | eliminated | 19.5 | 0.000 | 0.888 | 2600/2600 |
| flooded/2500/skai.roc_rider/skai_air_mix | seat1 | eliminated | 24.0 | 0.000 | 0.846 | 2600/2820 |
| flooded/2500/skai_air_mix/skai.roc_rider | seat1 | eliminated | 22.9 | 0.000 | 0.680 | 2820/2600 |
| flooded/2500/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 49.4 | 0.518 | 0.000 | 2600/2700 |
| flooded/2500/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 31.9 | 0.000 | 0.747 | 2700/2600 |
| flooded/2500/skai.roc_rider/water_expand_mix | seat1 | eliminated | 25.8 | 0.000 | 1.000 | 2600/2595 |
| flooded/2500/water_expand_mix/skai.roc_rider | seat0 | eliminated | 48.1 | 0.587 | 0.000 | 2595/2600 |
| flooded/2500/skai.roc_rider/water_leviathan_mix | seat1 | eliminated | 16.5 | 0.000 | 1.000 | 2600/2470 |
| flooded/2500/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 18.1 | 0.630 | 0.000 | 2470/2600 |
| flooded/2500/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 10.1 | 0.000 | 0.802 | 2470/2500 |
| flooded/2500/skai.talon_archer/skai.stormcaller | seat1 | eliminated | 13.3 | 0.000 | 0.917 | 2500/2470 |
| flooded/2500/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 7.5 | 0.000 | 1.000 | 2470/2385 |
| flooded/2500/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 10.1 | 1.000 | 0.000 | 2385/2470 |
| flooded/2500/skai.stormcaller/skai.windrunner | seat1 | eliminated | 8.6 | 0.000 | 0.839 | 2470/2475 |
| flooded/2500/skai.windrunner/skai.stormcaller | seat1 | eliminated | 17.6 | 0.000 | 0.730 | 2475/2470 |
| flooded/2500/skai.stormcaller/water.grasper | seat1 | eliminated | 9.2 | 0.000 | 0.853 | 2470/2500 |
| flooded/2500/water.grasper/skai.stormcaller | seat1 | eliminated | 16.7 | 0.000 | 0.051 | 2500/2470 |
| flooded/2500/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.2 | 0.000 | 0.846 | 2470/2470 |
| flooded/2500/water.ink_lobber/skai.stormcaller | seat1 | eliminated | 12.7 | 0.000 | 0.285 | 2470/2470 |
| flooded/2500/skai.stormcaller/water.leviathan | seat1 | eliminated | 6.9 | 0.000 | 1.000 | 2470/2660 |
| flooded/2500/water.leviathan/skai.stormcaller | seat0 | eliminated | 9.1 | 0.693 | 0.000 | 2660/2470 |
| flooded/2500/skai.stormcaller/water.reef_runner | seat1 | eliminated | 8.1 | 0.000 | 0.880 | 2470/2475 |
| flooded/2500/water.reef_runner/skai.stormcaller | seat1 | eliminated | 16.5 | 0.000 | 0.728 | 2475/2470 |
| flooded/2500/skai.stormcaller/water.shellback | seat1 | eliminated | 17.0 | 0.000 | 1.000 | 2470/2450 |
| flooded/2500/water.shellback/skai.stormcaller | seat0 | eliminated | 14.3 | 0.982 | 0.000 | 2450/2470 |
| flooded/2500/skai.stormcaller/water.spoutling | seat1 | eliminated | 24.0 | 0.000 | 0.163 | 2470/2500 |
| flooded/2500/water.spoutling/skai.stormcaller | seat1 | eliminated | 11.2 | 0.000 | 1.000 | 2500/2470 |
| flooded/2500/skai.stormcaller/skai_pressure_mix | seat1 | eliminated | 8.3 | 0.000 | 0.842 | 2470/2750 |
| flooded/2500/skai_pressure_mix/skai.stormcaller | seat1 | eliminated | 19.6 | 0.000 | 0.567 | 2750/2470 |
| flooded/2500/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 9.4 | 0.000 | 0.884 | 2470/2600 |
| flooded/2500/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 44.6 | 0.308 | 0.000 | 2600/2470 |
| flooded/2500/skai.stormcaller/skai_air_mix | seat1 | eliminated | 8.2 | 0.000 | 0.958 | 2470/2820 |
| flooded/2500/skai_air_mix/skai.stormcaller | seat0 | eliminated | 13.5 | 0.920 | 0.000 | 2820/2470 |
| flooded/2500/skai.stormcaller/water_pressure_mix | seat1 | eliminated | 8.6 | 0.000 | 0.903 | 2470/2700 |
| flooded/2500/water_pressure_mix/skai.stormcaller | seat0 | eliminated | 12.4 | 0.163 | 0.000 | 2700/2470 |
| flooded/2500/skai.stormcaller/water_expand_mix | seat1 | eliminated | 11.9 | 0.000 | 0.982 | 2470/2595 |
| flooded/2500/water_expand_mix/skai.stormcaller | seat0 | eliminated | 23.7 | 0.639 | 0.000 | 2595/2470 |
| flooded/2500/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 9.2 | 0.000 | 1.000 | 2470/2470 |
| flooded/2500/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 15.1 | 0.662 | 0.000 | 2470/2470 |
| flooded/2500/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 11.5 | 0.000 | 1.000 | 2500/2385 |
| flooded/2500/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 27.6 | 0.191 | 0.000 | 2385/2500 |
| flooded/2500/skai.talon_archer/skai.windrunner | seat1 | eliminated | 13.0 | 0.000 | 0.964 | 2500/2475 |
| flooded/2500/skai.windrunner/skai.talon_archer | seat1 | eliminated | 19.4 | 0.000 | 0.731 | 2475/2500 |
| flooded/2500/skai.talon_archer/water.grasper | seat1 | eliminated | 12.8 | 0.000 | 0.980 | 2500/2500 |
| flooded/2500/water.grasper/skai.talon_archer | seat0 | eliminated | 13.4 | 0.631 | 0.000 | 2500/2500 |
| flooded/2500/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 11.9 | 0.000 | 0.975 | 2500/2470 |
| flooded/2500/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 11.6 | 0.000 | 0.386 | 2470/2500 |
| flooded/2500/skai.talon_archer/water.leviathan | seat1 | eliminated | 7.9 | 0.000 | 1.000 | 2500/2660 |
| flooded/2500/water.leviathan/skai.talon_archer | seat0 | eliminated | 9.1 | 0.695 | 0.000 | 2660/2500 |
| flooded/2500/skai.talon_archer/water.reef_runner | seat1 | eliminated | 12.7 | 0.000 | 0.977 | 2500/2475 |
| flooded/2500/water.reef_runner/skai.talon_archer | seat1 | eliminated | 20.6 | 0.000 | 0.683 | 2475/2500 |
| flooded/2500/skai.talon_archer/water.shellback | seat1 | eliminated | 25.1 | 0.000 | 0.994 | 2500/2450 |
| flooded/2500/water.shellback/skai.talon_archer | seat0 | eliminated | 22.3 | 0.925 | 0.000 | 2450/2500 |
| flooded/2500/skai.talon_archer/water.spoutling | seat0 | eliminated | 27.8 | 0.307 | 0.000 | 2500/2500 |
| flooded/2500/water.spoutling/skai.talon_archer | seat1 | eliminated | 11.4 | 0.000 | 1.000 | 2500/2500 |
| flooded/2500/skai.talon_archer/skai_pressure_mix | seat1 | eliminated | 12.4 | 0.000 | 0.993 | 2500/2750 |
| flooded/2500/skai_pressure_mix/skai.talon_archer | seat1 | eliminated | 17.7 | 0.000 | 0.767 | 2750/2500 |
| flooded/2500/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 14.9 | 0.000 | 0.912 | 2500/2600 |
| flooded/2500/skai_expand_mix/skai.talon_archer | seat1 | eliminated | 16.1 | 0.000 | 0.904 | 2600/2500 |
| flooded/2500/skai.talon_archer/skai_air_mix | seat1 | eliminated | 12.7 | 0.000 | 0.975 | 2500/2820 |
| flooded/2500/skai_air_mix/skai.talon_archer | seat1 | eliminated | 18.7 | 0.000 | 0.509 | 2820/2500 |
| flooded/2500/skai.talon_archer/water_pressure_mix | seat1 | eliminated | 12.2 | 0.000 | 0.987 | 2500/2700 |
| flooded/2500/water_pressure_mix/skai.talon_archer | seat0 | eliminated | 12.0 | 0.697 | 0.000 | 2700/2500 |
| flooded/2500/skai.talon_archer/water_expand_mix | seat1 | eliminated | 16.0 | 0.000 | 1.000 | 2500/2595 |
| flooded/2500/water_expand_mix/skai.talon_archer | seat0 | eliminated | 34.1 | 0.720 | 0.000 | 2595/2500 |
| flooded/2500/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 14.5 | 0.000 | 1.000 | 2500/2470 |
| flooded/2500/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 16.0 | 0.643 | 0.000 | 2470/2500 |
| flooded/2500/skai.thunderhead/skai.windrunner | seat0 | eliminated | 15.4 | 0.327 | 0.000 | 2385/2475 |
| flooded/2500/skai.windrunner/skai.thunderhead | seat1 | eliminated | 11.4 | 0.000 | 0.873 | 2475/2385 |
| flooded/2500/skai.thunderhead/water.grasper | seat0 | eliminated | 12.6 | 1.000 | 0.000 | 2385/2500 |
| flooded/2500/water.grasper/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 2500/2385 |
| flooded/2500/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 10.1 | 1.000 | 0.000 | 2385/2470 |
| flooded/2500/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 8.0 | 0.000 | 1.000 | 2470/2385 |
| flooded/2500/skai.thunderhead/water.leviathan | seat1 | eliminated | 7.1 | 0.000 | 1.000 | 2385/2660 |
| flooded/2500/water.leviathan/skai.thunderhead | seat1 | eliminated | 12.2 | 0.000 | 0.386 | 2660/2385 |
| flooded/2500/skai.thunderhead/water.reef_runner | seat0 | eliminated | 20.1 | 0.183 | 0.000 | 2385/2475 |
| flooded/2500/water.reef_runner/skai.thunderhead | seat1 | eliminated | 9.2 | 0.000 | 0.918 | 2475/2385 |
| flooded/2500/skai.thunderhead/water.shellback | seat1 | eliminated | 24.6 | 0.000 | 0.976 | 2385/2450 |
| flooded/2500/water.shellback/skai.thunderhead | seat0 | eliminated | 24.1 | 0.832 | 0.000 | 2450/2385 |
| flooded/2500/skai.thunderhead/water.spoutling | seat0 | eliminated | 10.4 | 0.681 | 0.000 | 2385/2500 |
| flooded/2500/water.spoutling/skai.thunderhead | seat1 | eliminated | 7.7 | 0.000 | 1.000 | 2500/2385 |
| flooded/2500/skai.thunderhead/skai_pressure_mix | seat1 | eliminated | 19.8 | 0.000 | 0.182 | 2385/2750 |
| flooded/2500/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 12.6 | 0.000 | 0.913 | 2750/2385 |
| flooded/2500/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 19.3 | 0.000 | 0.384 | 2385/2600 |
| flooded/2500/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 73.3 | 0.308 | 0.000 | 2600/2385 |
| flooded/2500/skai.thunderhead/skai_air_mix | seat1 | eliminated | 23.9 | 0.000 | 0.920 | 2385/2820 |
| flooded/2500/skai_air_mix/skai.thunderhead | seat0 | eliminated | 30.1 | 0.920 | 0.000 | 2820/2385 |
| flooded/2500/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 13.5 | 0.857 | 0.000 | 2385/2700 |
| flooded/2500/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 0.947 | 2700/2385 |
| flooded/2500/skai.thunderhead/water_expand_mix | seat1 | eliminated | 22.5 | 0.000 | 0.612 | 2385/2595 |
| flooded/2500/water_expand_mix/skai.thunderhead | seat1 | eliminated | 17.6 | 0.000 | 0.781 | 2595/2385 |
| flooded/2500/skai.thunderhead/water_leviathan_mix | seat1 | eliminated | 14.7 | 0.000 | 0.743 | 2385/2470 |
| flooded/2500/water_leviathan_mix/skai.thunderhead | seat1 | eliminated | 20.4 | 0.000 | 0.064 | 2470/2385 |
| flooded/2500/skai.windrunner/water.grasper | seat1 | eliminated | 11.0 | 0.000 | 0.848 | 2475/2500 |
| flooded/2500/water.grasper/skai.windrunner | seat0 | eliminated | 10.7 | 0.658 | 0.000 | 2500/2475 |
| flooded/2500/skai.windrunner/water.ink_lobber | seat1 | eliminated | 14.1 | 0.000 | 0.897 | 2475/2470 |
| flooded/2500/water.ink_lobber/skai.windrunner | seat1 | eliminated | 10.8 | 0.000 | 0.297 | 2470/2475 |
| flooded/2500/skai.windrunner/water.leviathan | seat1 | eliminated | 7.0 | 0.000 | 1.000 | 2475/2660 |
| flooded/2500/water.leviathan/skai.windrunner | seat0 | eliminated | 12.1 | 0.638 | 0.000 | 2660/2475 |
| flooded/2500/skai.windrunner/water.reef_runner | seat1 | eliminated | 13.1 | 0.000 | 0.754 | 2475/2475 |
| flooded/2500/water.reef_runner/skai.windrunner | seat1 | eliminated | 14.6 | 0.000 | 0.706 | 2475/2475 |
| flooded/2500/skai.windrunner/water.shellback | seat1 | eliminated | 21.0 | 0.000 | 0.997 | 2475/2450 |
| flooded/2500/water.shellback/skai.windrunner | seat0 | eliminated | 20.7 | 0.926 | 0.000 | 2450/2475 |
| flooded/2500/skai.windrunner/water.spoutling | seat0 | eliminated | 17.1 | 0.606 | 0.000 | 2475/2500 |
| flooded/2500/water.spoutling/skai.windrunner | seat1 | eliminated | 10.1 | 0.000 | 1.000 | 2500/2475 |
| flooded/2500/skai.windrunner/skai_pressure_mix | seat1 | eliminated | 13.5 | 0.000 | 0.766 | 2475/2750 |
| flooded/2500/skai_pressure_mix/skai.windrunner | seat1 | eliminated | 14.7 | 0.000 | 0.783 | 2750/2475 |
| flooded/2500/skai.windrunner/skai_expand_mix | seat1 | eliminated | 17.4 | 0.000 | 0.728 | 2475/2600 |
| flooded/2500/skai_expand_mix/skai.windrunner | seat1 | eliminated | 13.6 | 0.000 | 0.971 | 2600/2475 |
| flooded/2500/skai.windrunner/skai_air_mix | seat1 | eliminated | 13.0 | 0.000 | 0.860 | 2475/2820 |
| flooded/2500/skai_air_mix/skai.windrunner | seat1 | eliminated | 15.5 | 0.000 | 0.613 | 2820/2475 |
| flooded/2500/skai.windrunner/water_pressure_mix | seat1 | eliminated | 10.8 | 0.000 | 0.861 | 2475/2700 |
| flooded/2500/water_pressure_mix/skai.windrunner | seat0 | eliminated | 10.3 | 0.691 | 0.000 | 2700/2475 |
| flooded/2500/skai.windrunner/water_expand_mix | seat1 | eliminated | 15.9 | 0.000 | 1.000 | 2475/2595 |
| flooded/2500/water_expand_mix/skai.windrunner | seat0 | eliminated | 31.2 | 0.677 | 0.000 | 2595/2475 |
| flooded/2500/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 13.1 | 0.000 | 1.000 | 2475/2470 |
| flooded/2500/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 17.9 | 0.540 | 0.000 | 2470/2475 |
| flooded/2500/water.grasper/water.ink_lobber | seat1 | eliminated | 10.9 | 0.000 | 0.429 | 2500/2470 |
| flooded/2500/water.ink_lobber/water.grasper | seat1 | eliminated | 10.7 | 0.000 | 0.447 | 2470/2500 |
| flooded/2500/water.grasper/water.leviathan | seat1 | eliminated | 3.8 | 0.000 | 0.996 | 2500/2660 |
| flooded/2500/water.leviathan/water.grasper | seat0 | eliminated | 12.1 | 0.628 | 0.000 | 2660/2500 |
| flooded/2500/water.grasper/water.reef_runner | seat0 | eliminated | 12.2 | 0.605 | 0.000 | 2500/2475 |
| flooded/2500/water.reef_runner/water.grasper | seat1 | eliminated | 10.6 | 0.000 | 0.861 | 2475/2500 |
| flooded/2500/water.grasper/water.shellback | seat0 | eliminated | 21.1 | 0.566 | 0.000 | 2500/2450 |
| flooded/2500/water.shellback/water.grasper | seat1 | eliminated | 19.2 | 0.000 | 0.717 | 2450/2500 |
| flooded/2500/water.grasper/water.spoutling | seat0 | eliminated | 8.4 | 0.838 | 0.000 | 2500/2500 |
| flooded/2500/water.spoutling/water.grasper | seat1 | eliminated | 10.6 | 0.000 | 1.000 | 2500/2500 |
| flooded/2500/water.grasper/skai_pressure_mix | seat0 | eliminated | 12.5 | 0.596 | 0.000 | 2500/2750 |
| flooded/2500/skai_pressure_mix/water.grasper | seat1 | eliminated | 11.5 | 0.000 | 0.887 | 2750/2500 |
| flooded/2500/water.grasper/skai_expand_mix | seat1 | eliminated | 68.6 | 0.000 | 0.308 | 2500/2600 |
| flooded/2500/skai_expand_mix/water.grasper | seat0 | eliminated | 114.1 | 0.308 | 0.000 | 2600/2500 |
| flooded/2500/water.grasper/skai_air_mix | seat1 | eliminated | 13.3 | 0.000 | 0.920 | 2500/2820 |
| flooded/2500/skai_air_mix/water.grasper | seat0 | eliminated | 22.5 | 0.920 | 0.000 | 2820/2500 |
| flooded/2500/water.grasper/water_pressure_mix | seat1 | eliminated | 21.8 | 0.000 | 0.312 | 2500/2700 |
| flooded/2500/water_pressure_mix/water.grasper | seat0 | eliminated | 23.8 | 0.272 | 0.000 | 2700/2500 |
| flooded/2500/water.grasper/water_expand_mix | seat0 | eliminated | 22.5 | 0.177 | 0.000 | 2500/2595 |
| flooded/2500/water_expand_mix/water.grasper | seat1 | eliminated | 15.0 | 0.000 | 0.706 | 2595/2500 |
| flooded/2500/water.grasper/water_leviathan_mix | seat1 | eliminated | 7.3 | 0.000 | 0.752 | 2500/2470 |
| flooded/2500/water_leviathan_mix/water.grasper | seat1 | eliminated | 20.4 | 0.000 | 0.211 | 2470/2500 |
| flooded/2500/water.ink_lobber/water.leviathan | seat1 | eliminated | 8.0 | 0.000 | 0.942 | 2470/2660 |
| flooded/2500/water.leviathan/water.ink_lobber | seat0 | eliminated | 9.1 | 0.602 | 0.000 | 2660/2470 |
| flooded/2500/water.ink_lobber/water.reef_runner | seat1 | eliminated | 9.3 | 0.000 | 0.642 | 2470/2475 |
| flooded/2500/water.reef_runner/water.ink_lobber | seat1 | eliminated | 16.2 | 0.000 | 0.846 | 2475/2470 |
| flooded/2500/water.ink_lobber/water.shellback | seat1 | eliminated | 17.9 | 0.000 | 0.864 | 2470/2450 |
| flooded/2500/water.shellback/water.ink_lobber | seat0 | eliminated | 17.1 | 0.792 | 0.000 | 2450/2470 |
| flooded/2500/water.ink_lobber/water.spoutling | seat0 | eliminated | 12.1 | 0.627 | 0.000 | 2470/2500 |
| flooded/2500/water.spoutling/water.ink_lobber | seat1 | eliminated | 10.9 | 0.000 | 1.000 | 2500/2470 |
| flooded/2500/water.ink_lobber/skai_pressure_mix | seat1 | eliminated | 9.9 | 0.000 | 0.504 | 2470/2750 |
| flooded/2500/skai_pressure_mix/water.ink_lobber | seat1 | eliminated | 16.5 | 0.000 | 0.881 | 2750/2470 |
| flooded/2500/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 12.2 | 0.000 | 0.531 | 2470/2600 |
| flooded/2500/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 49.3 | 0.308 | 0.000 | 2600/2470 |
| flooded/2500/water.ink_lobber/skai_air_mix | seat1 | eliminated | 10.0 | 0.000 | 0.920 | 2470/2820 |
| flooded/2500/skai_air_mix/water.ink_lobber | seat0 | eliminated | 14.5 | 0.920 | 0.000 | 2820/2470 |
| flooded/2500/water.ink_lobber/water_pressure_mix | seat1 | eliminated | 10.2 | 0.000 | 0.495 | 2470/2700 |
| flooded/2500/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 13.4 | 0.000 | 0.382 | 2700/2470 |
| flooded/2500/water.ink_lobber/water_expand_mix | seat1 | eliminated | 14.4 | 0.000 | 0.839 | 2470/2595 |
| flooded/2500/water_expand_mix/water.ink_lobber | seat1 | eliminated | 25.7 | 0.000 | 0.670 | 2595/2470 |
| flooded/2500/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 10.9 | 0.000 | 0.899 | 2470/2470 |
| flooded/2500/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 18.1 | 0.522 | 0.000 | 2470/2470 |
| flooded/2500/water.leviathan/water.reef_runner | seat0 | eliminated | 9.1 | 0.652 | 0.000 | 2660/2475 |
| flooded/2500/water.reef_runner/water.leviathan | seat1 | eliminated | 7.4 | 0.000 | 1.000 | 2475/2660 |
| flooded/2500/water.leviathan/water.shellback | seat0 | eliminated | 39.1 | 0.221 | 0.000 | 2660/2450 |
| flooded/2500/water.shellback/water.leviathan | seat1 | eliminated | 24.9 | 0.000 | 0.558 | 2450/2660 |
| flooded/2500/water.leviathan/water.spoutling | seat0 | eliminated | 6.1 | 0.842 | 0.000 | 2660/2500 |
| flooded/2500/water.spoutling/water.leviathan | seat1 | eliminated | 7.4 | 0.000 | 1.000 | 2500/2660 |
| flooded/2500/water.leviathan/skai_pressure_mix | seat0 | eliminated | 12.1 | 0.566 | 0.000 | 2660/2750 |
| flooded/2500/skai_pressure_mix/water.leviathan | seat1 | eliminated | 9.1 | 0.000 | 1.000 | 2750/2660 |
| flooded/2500/water.leviathan/skai_expand_mix | seat0 | eliminated | 12.1 | 0.638 | 0.000 | 2660/2600 |
| flooded/2500/skai_expand_mix/water.leviathan | seat1 | eliminated | 8.0 | 0.000 | 1.000 | 2600/2660 |
| flooded/2500/water.leviathan/skai_air_mix | seat0 | eliminated | 12.1 | 0.419 | 0.000 | 2660/2820 |
| flooded/2500/skai_air_mix/water.leviathan | seat1 | eliminated | 8.7 | 0.000 | 1.000 | 2820/2660 |
| flooded/2500/water.leviathan/water_pressure_mix | seat0 | eliminated | 18.1 | 0.505 | 0.000 | 2660/2700 |
| flooded/2500/water_pressure_mix/water.leviathan | seat1 | eliminated | 3.8 | 0.000 | 1.000 | 2700/2660 |
| flooded/2500/water.leviathan/water_expand_mix | seat0 | eliminated | 24.1 | 0.408 | 0.000 | 2660/2595 |
| flooded/2500/water_expand_mix/water.leviathan | seat1 | eliminated | 13.0 | 0.000 | 0.947 | 2595/2660 |
| flooded/2500/water.leviathan/water_leviathan_mix | seat1 | eliminated | 16.5 | 0.000 | 0.795 | 2660/2470 |
| flooded/2500/water_leviathan_mix/water.leviathan | seat1 | eliminated | 14.0 | 0.000 | 0.611 | 2470/2660 |
| flooded/2500/water.reef_runner/water.shellback | seat1 | eliminated | 22.1 | 0.000 | 0.996 | 2475/2450 |
| flooded/2500/water.shellback/water.reef_runner | seat0 | eliminated | 21.9 | 0.861 | 0.000 | 2450/2475 |
| flooded/2500/water.reef_runner/water.spoutling | seat0 | eliminated | 15.8 | 0.626 | 0.000 | 2475/2500 |
| flooded/2500/water.spoutling/water.reef_runner | seat1 | eliminated | 9.5 | 0.000 | 1.000 | 2500/2475 |
| flooded/2500/water.reef_runner/skai_pressure_mix | seat1 | eliminated | 14.2 | 0.000 | 0.749 | 2475/2750 |
| flooded/2500/skai_pressure_mix/water.reef_runner | seat1 | eliminated | 14.0 | 0.000 | 0.820 | 2750/2475 |
| flooded/2500/water.reef_runner/skai_expand_mix | seat1 | eliminated | 18.0 | 0.000 | 0.722 | 2475/2600 |
| flooded/2500/skai_expand_mix/water.reef_runner | seat1 | eliminated | 13.3 | 0.000 | 0.990 | 2600/2475 |
| flooded/2500/water.reef_runner/skai_air_mix | seat1 | eliminated | 12.4 | 0.000 | 0.845 | 2475/2820 |
| flooded/2500/skai_air_mix/water.reef_runner | seat1 | eliminated | 15.7 | 0.000 | 0.713 | 2820/2475 |
| flooded/2500/water.reef_runner/water_pressure_mix | seat1 | eliminated | 10.8 | 0.000 | 0.868 | 2475/2700 |
| flooded/2500/water_pressure_mix/water.reef_runner | seat0 | eliminated | 10.8 | 0.688 | 0.000 | 2700/2475 |
| flooded/2500/water.reef_runner/water_expand_mix | seat1 | eliminated | 15.1 | 0.000 | 0.995 | 2475/2595 |
| flooded/2500/water_expand_mix/water.reef_runner | seat0 | eliminated | 25.3 | 0.690 | 0.000 | 2595/2475 |
| flooded/2500/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 13.2 | 0.000 | 1.000 | 2475/2470 |
| flooded/2500/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 18.1 | 0.585 | 0.000 | 2470/2475 |
| flooded/2500/water.shellback/water.spoutling | seat0 | eliminated | 16.6 | 0.995 | 0.000 | 2450/2500 |
| flooded/2500/water.spoutling/water.shellback | seat1 | eliminated | 19.4 | 0.000 | 1.000 | 2500/2450 |
| flooded/2500/water.shellback/skai_pressure_mix | seat0 | eliminated | 22.4 | 0.907 | 0.000 | 2450/2750 |
| flooded/2500/skai_pressure_mix/water.shellback | seat1 | eliminated | 23.2 | 0.000 | 0.997 | 2750/2450 |
| flooded/2500/water.shellback/skai_expand_mix | seat0 | eliminated | 25.0 | 0.911 | 0.000 | 2450/2600 |
| flooded/2500/skai_expand_mix/water.shellback | seat1 | eliminated | 26.3 | 0.000 | 0.998 | 2600/2450 |
| flooded/2500/water.shellback/skai_air_mix | seat0 | eliminated | 28.2 | 0.908 | 0.000 | 2450/2820 |
| flooded/2500/skai_air_mix/water.shellback | seat1 | eliminated | 30.6 | 0.000 | 0.924 | 2820/2450 |
| flooded/2500/water.shellback/water_pressure_mix | seat1 | eliminated | 20.9 | 0.000 | 0.740 | 2450/2700 |
| flooded/2500/water_pressure_mix/water.shellback | seat0 | eliminated | 21.9 | 0.608 | 0.000 | 2700/2450 |
| flooded/2500/water.shellback/water_expand_mix | seat1 | eliminated | 44.3 | 0.000 | 0.903 | 2450/2595 |
| flooded/2500/water_expand_mix/water.shellback | seat1 | eliminated | 70.3 | 0.000 | 0.681 | 2595/2450 |
| flooded/2500/water.shellback/water_leviathan_mix | seat0 | eliminated | 35.1 | 0.781 | 0.000 | 2450/2470 |
| flooded/2500/water_leviathan_mix/water.shellback | seat1 | eliminated | 29.5 | 0.000 | 1.000 | 2470/2450 |
| flooded/2500/water.spoutling/skai_pressure_mix | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 2500/2750 |
| flooded/2500/skai_pressure_mix/water.spoutling | seat0 | eliminated | 20.8 | 0.555 | 0.000 | 2750/2500 |
| flooded/2500/water.spoutling/skai_expand_mix | seat1 | eliminated | 11.0 | 0.000 | 1.000 | 2500/2600 |
| flooded/2500/skai_expand_mix/water.spoutling | seat1 | eliminated | 37.9 | 0.000 | 0.221 | 2600/2500 |
| flooded/2500/water.spoutling/skai_air_mix | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 2500/2820 |
| flooded/2500/skai_air_mix/water.spoutling | seat0 | eliminated | 16.3 | 0.567 | 0.000 | 2820/2500 |
| flooded/2500/water.spoutling/water_pressure_mix | seat1 | eliminated | 10.4 | 0.000 | 1.000 | 2500/2700 |
| flooded/2500/water_pressure_mix/water.spoutling | seat0 | eliminated | 7.9 | 0.857 | 0.000 | 2700/2500 |
| flooded/2500/water.spoutling/water_expand_mix | seat1 | eliminated | 13.0 | 0.000 | 1.000 | 2500/2595 |
| flooded/2500/water_expand_mix/water.spoutling | seat0 | eliminated | 16.8 | 0.923 | 0.000 | 2595/2500 |
| flooded/2500/water.spoutling/water_leviathan_mix | seat1 | eliminated | 10.8 | 0.000 | 1.000 | 2500/2470 |
| flooded/2500/water_leviathan_mix/water.spoutling | seat0 | eliminated | 12.1 | 0.928 | 0.000 | 2470/2500 |
| flooded/2500/skai_pressure_mix/skai_expand_mix | seat1 | eliminated | 18.6 | 0.000 | 0.692 | 2750/2600 |
| flooded/2500/skai_expand_mix/skai_pressure_mix | seat1 | eliminated | 13.2 | 0.000 | 0.977 | 2600/2750 |
| flooded/2500/skai_pressure_mix/skai_air_mix | seat1 | eliminated | 13.8 | 0.000 | 0.886 | 2750/2820 |
| flooded/2500/skai_air_mix/skai_pressure_mix | seat1 | eliminated | 14.9 | 0.000 | 0.648 | 2820/2750 |
| flooded/2500/skai_pressure_mix/water_pressure_mix | seat1 | eliminated | 11.5 | 0.000 | 0.890 | 2750/2700 |
| flooded/2500/water_pressure_mix/skai_pressure_mix | seat0 | eliminated | 11.9 | 0.651 | 0.000 | 2700/2750 |
| flooded/2500/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 16.1 | 0.000 | 0.976 | 2750/2595 |
| flooded/2500/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 44.1 | 0.400 | 0.000 | 2595/2750 |
| flooded/2500/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 14.2 | 0.000 | 1.000 | 2750/2470 |
| flooded/2500/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 18.1 | 0.480 | 0.000 | 2470/2750 |
| flooded/2500/skai_expand_mix/skai_air_mix | seat1 | eliminated | 15.4 | 0.000 | 0.969 | 2600/2820 |
| flooded/2500/skai_air_mix/skai_expand_mix | seat1 | eliminated | 19.1 | 0.000 | 0.635 | 2820/2600 |
| flooded/2500/skai_expand_mix/water_pressure_mix | seat1 | eliminated | 34.7 | 0.000 | 0.927 | 2600/2700 |
| flooded/2500/water_pressure_mix/skai_expand_mix | seat0 | eliminated | 32.4 | 0.558 | 0.000 | 2700/2600 |
| flooded/2500/skai_expand_mix/water_expand_mix | seat1 | eliminated | 18.3 | 0.000 | 1.000 | 2600/2595 |
| flooded/2500/water_expand_mix/skai_expand_mix | seat0 | eliminated | 42.0 | 0.621 | 0.000 | 2595/2600 |
| flooded/2500/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 15.4 | 0.000 | 1.000 | 2600/2470 |
| flooded/2500/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 18.1 | 0.590 | 0.000 | 2470/2600 |
| flooded/2500/skai_air_mix/water_pressure_mix | seat0 | eliminated | 24.3 | 0.701 | 0.000 | 2820/2700 |
| flooded/2500/water_pressure_mix/skai_air_mix | seat1 | eliminated | 13.3 | 0.000 | 0.825 | 2700/2820 |
| flooded/2500/skai_air_mix/water_expand_mix | seat1 | eliminated | 24.9 | 0.000 | 0.839 | 2820/2595 |
| flooded/2500/water_expand_mix/skai_air_mix | seat1 | eliminated | 31.3 | 0.000 | 0.549 | 2595/2820 |
| flooded/2500/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 20.1 | 0.000 | 0.699 | 2820/2470 |
| flooded/2500/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 21.1 | 0.318 | 0.000 | 2470/2820 |
| flooded/2500/water_pressure_mix/water_expand_mix | seat1 | eliminated | 34.5 | 0.000 | 0.126 | 2700/2595 |
| flooded/2500/water_expand_mix/water_pressure_mix | seat1 | eliminated | 15.3 | 0.000 | 0.693 | 2595/2700 |
| flooded/2500/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 10.2 | 0.000 | 0.793 | 2700/2470 |
| flooded/2500/water_leviathan_mix/water_pressure_mix | seat1 | eliminated | 16.0 | 0.000 | 0.224 | 2470/2700 |
| flooded/2500/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 19.7 | 0.000 | 0.888 | 2595/2470 |
| flooded/2500/water_leviathan_mix/water_expand_mix | seat1 | eliminated | 24.1 | 0.000 | 0.631 | 2470/2595 |
| choke/1000/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 11.3 | 1.000 | 0.000 | 1000/950 |
| choke/1000/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 11.3 | 0.000 | 1.000 | 950/1000 |
| choke/1000/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 29.5 | 0.000 | 0.240 | 1000/1000 |
| choke/1000/skai.talon_archer/skai.roc_rider | seat0 | eliminated | 27.4 | 0.308 | 0.000 | 1000/1000 |
| choke/1000/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 19.4 | 1.000 | 0.000 | 1000/1060 |
| choke/1000/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 19.4 | 0.000 | 1.000 | 1060/1000 |
| choke/1000/skai.roc_rider/skai.windrunner | seat1 | eliminated | 19.8 | 0.000 | 0.259 | 1000/975 |
| choke/1000/skai.windrunner/skai.roc_rider | seat0 | eliminated | 19.8 | 0.277 | 0.000 | 975/1000 |
| choke/1000/skai.roc_rider/water.grasper | seat0 | eliminated | 30.5 | 1.000 | 0.000 | 1000/1000 |
| choke/1000/water.grasper/skai.roc_rider | seat1 | eliminated | 28.1 | 0.000 | 1.000 | 1000/1000 |
| choke/1000/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 12.7 | 1.000 | 0.000 | 1000/950 |
| choke/1000/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 12.7 | 0.000 | 1.000 | 950/1000 |
| choke/1000/skai.roc_rider/water.leviathan | seat1 | eliminated | 49.0 | 0.000 | 0.521 | 1000/1140 |
| choke/1000/water.leviathan/skai.roc_rider | seat0 | eliminated | 49.3 | 0.521 | 0.000 | 1140/1000 |
| choke/1000/skai.roc_rider/water.reef_runner | seat0 | eliminated | 19.5 | 1.000 | 0.000 | 1000/975 |
| choke/1000/water.reef_runner/skai.roc_rider | seat1 | eliminated | 18.3 | 0.000 | 1.000 | 975/1000 |
| choke/1000/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 76.3 | 1.000 | 0.833 | 1000/1050 |
| choke/1000/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 36.8 | 0.960 | 1.000 | 1050/1000 |
| choke/1000/skai.roc_rider/water.spoutling | seat0 | eliminated | 11.3 | 0.737 | 0.000 | 1000/1000 |
| choke/1000/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.5 | 0.000 | 0.700 | 1000/1000 |
| choke/1000/skai.roc_rider/skai_pressure_mix | seat1 | eliminated | 15.8 | 0.000 | 0.475 | 1000/1100 |
| choke/1000/skai_pressure_mix/skai.roc_rider | seat0 | eliminated | 15.8 | 0.511 | 0.000 | 1100/1000 |
| choke/1000/skai.roc_rider/skai_expand_mix | seat1 | eliminated | 14.6 | 0.000 | 0.565 | 1000/1300 |
| choke/1000/skai_expand_mix/skai.roc_rider | seat0 | eliminated | 13.9 | 0.631 | 0.000 | 1300/1000 |
| choke/1000/skai.roc_rider/skai_air_mix | seat0 | eliminated | 23.0 | 0.469 | 0.000 | 1000/940 |
| choke/1000/skai_air_mix/skai.roc_rider | seat1 | eliminated | 24.2 | 0.000 | 0.321 | 940/1000 |
| choke/1000/skai.roc_rider/water_pressure_mix | seat0 | eliminated | 19.8 | 1.000 | 0.000 | 1000/675 |
| choke/1000/water_pressure_mix/skai.roc_rider | seat1 | eliminated | 20.8 | 0.000 | 1.000 | 675/1000 |
| choke/1000/skai.roc_rider/water_expand_mix | seat0 | eliminated | 50.8 | 0.800 | 0.000 | 1000/865 |
| choke/1000/water_expand_mix/skai.roc_rider | seat0 | eliminated | 43.9 | 0.230 | 0.000 | 865/1000 |
| choke/1000/skai.roc_rider/water_leviathan_mix | stalemate | dome_absorbed | 43.8 | 0.200 | 0.893 | 1000/1235 |
| choke/1000/water_leviathan_mix/skai.roc_rider | seat0 | eliminated | 10.3 | 0.865 | 0.000 | 1235/1000 |
| choke/1000/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 9.4 | 0.000 | 0.390 | 950/1000 |
| choke/1000/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 8.8 | 0.348 | 0.000 | 1000/950 |
| choke/1000/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.8 | 0.000 | 1.000 | 950/1060 |
| choke/1000/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.8 | 1.000 | 0.000 | 1060/950 |
| choke/1000/skai.stormcaller/skai.windrunner | seat1 | eliminated | 8.8 | 0.000 | 0.379 | 950/975 |
| choke/1000/skai.windrunner/skai.stormcaller | seat0 | eliminated | 8.4 | 0.331 | 0.000 | 975/950 |
| choke/1000/skai.stormcaller/water.grasper | seat1 | eliminated | 10.6 | 0.000 | 0.310 | 950/1000 |
| choke/1000/water.grasper/skai.stormcaller | seat0 | eliminated | 11.9 | 0.183 | 0.000 | 1000/950 |
| choke/1000/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.3 | 0.000 | 0.315 | 950/950 |
| choke/1000/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 9.3 | 0.315 | 0.000 | 950/950 |
| choke/1000/skai.stormcaller/water.leviathan | seat1 | eliminated | 2.5 | 0.000 | 0.944 | 950/1140 |
| choke/1000/water.leviathan/skai.stormcaller | seat0 | eliminated | 2.5 | 0.944 | 0.000 | 1140/950 |
| choke/1000/skai.stormcaller/water.reef_runner | seat1 | eliminated | 9.5 | 0.000 | 0.222 | 950/975 |
| choke/1000/water.reef_runner/skai.stormcaller | seat0 | eliminated | 9.4 | 0.207 | 0.000 | 975/950 |
| choke/1000/skai.stormcaller/water.shellback | seat1 | eliminated | 13.9 | 0.000 | 1.000 | 950/1050 |
| choke/1000/water.shellback/skai.stormcaller | seat0 | eliminated | 14.3 | 1.000 | 0.000 | 1050/950 |
| choke/1000/skai.stormcaller/water.spoutling | seat0 | eliminated | 9.5 | 0.678 | 0.000 | 950/1000 |
| choke/1000/water.spoutling/skai.stormcaller | seat1 | eliminated | 9.3 | 0.000 | 0.689 | 1000/950 |
| choke/1000/skai.stormcaller/skai_pressure_mix | seat1 | eliminated | 8.2 | 0.000 | 0.449 | 950/1100 |
| choke/1000/skai_pressure_mix/skai.stormcaller | seat0 | eliminated | 7.7 | 0.444 | 0.000 | 1100/950 |
| choke/1000/skai.stormcaller/skai_expand_mix | seat1 | eliminated | 7.8 | 0.000 | 0.771 | 950/1300 |
| choke/1000/skai_expand_mix/skai.stormcaller | seat0 | eliminated | 7.8 | 0.721 | 0.000 | 1300/950 |
| choke/1000/skai.stormcaller/skai_air_mix | seat1 | eliminated | 9.3 | 0.000 | 0.920 | 950/940 |
| choke/1000/skai_air_mix/skai.stormcaller | seat0 | eliminated | 9.1 | 0.920 | 0.000 | 940/950 |
| choke/1000/skai.stormcaller/water_pressure_mix | seat0 | eliminated | 9.6 | 0.547 | 0.000 | 950/675 |
| choke/1000/water_pressure_mix/skai.stormcaller | seat1 | eliminated | 11.8 | 0.000 | 0.360 | 675/950 |
| choke/1000/skai.stormcaller/water_expand_mix | seat1 | eliminated | 11.8 | 0.000 | 0.847 | 950/865 |
| choke/1000/water_expand_mix/skai.stormcaller | seat0 | eliminated | 13.2 | 0.785 | 0.000 | 865/950 |
| choke/1000/skai.stormcaller/water_leviathan_mix | seat1 | eliminated | 6.0 | 0.000 | 0.943 | 950/1235 |
| choke/1000/water_leviathan_mix/skai.stormcaller | seat0 | eliminated | 7.0 | 1.000 | 0.000 | 1235/950 |
| choke/1000/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 9.5 | 0.000 | 0.624 | 1000/1060 |
| choke/1000/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 9.4 | 0.671 | 0.000 | 1060/1000 |
| choke/1000/skai.talon_archer/skai.windrunner | seat1 | eliminated | 21.1 | 0.000 | 0.138 | 1000/975 |
| choke/1000/skai.windrunner/skai.talon_archer | seat0 | eliminated | 20.6 | 0.149 | 0.000 | 975/1000 |
| choke/1000/skai.talon_archer/water.grasper | seat1 | eliminated | 14.1 | 0.000 | 0.574 | 1000/1000 |
| choke/1000/water.grasper/skai.talon_archer | seat0 | eliminated | 13.9 | 0.580 | 0.000 | 1000/1000 |
| choke/1000/skai.talon_archer/water.ink_lobber | seat0 | eliminated | 11.6 | 0.145 | 0.000 | 1000/950 |
| choke/1000/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 10.8 | 0.000 | 0.245 | 950/1000 |
| choke/1000/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.980 | 1000/1140 |
| choke/1000/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.973 | 0.000 | 1140/1000 |
| choke/1000/skai.talon_archer/water.reef_runner | seat0 | eliminated | 26.7 | 0.033 | 0.000 | 1000/975 |
| choke/1000/water.reef_runner/skai.talon_archer | seat1 | eliminated | 23.8 | 0.000 | 0.067 | 975/1000 |
| choke/1000/skai.talon_archer/water.shellback | seat1 | eliminated | 20.6 | 0.000 | 0.925 | 1000/1050 |
| choke/1000/water.shellback/skai.talon_archer | seat0 | eliminated | 20.6 | 0.968 | 0.000 | 1050/1000 |
| choke/1000/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.8 | 0.794 | 0.000 | 1000/1000 |
| choke/1000/water.spoutling/skai.talon_archer | seat1 | eliminated | 9.8 | 0.000 | 0.817 | 1000/1000 |
| choke/1000/skai.talon_archer/skai_pressure_mix | seat1 | eliminated | 15.1 | 0.000 | 0.432 | 1000/1100 |
| choke/1000/skai_pressure_mix/skai.talon_archer | seat0 | eliminated | 16.7 | 0.323 | 0.000 | 1100/1000 |
| choke/1000/skai.talon_archer/skai_expand_mix | seat1 | eliminated | 11.8 | 0.000 | 0.612 | 1000/1300 |
| choke/1000/skai_expand_mix/skai.talon_archer | seat0 | eliminated | 12.8 | 0.578 | 0.000 | 1300/1000 |
| choke/1000/skai.talon_archer/skai_air_mix | seat1 | eliminated | 25.3 | 0.000 | 0.250 | 1000/940 |
| choke/1000/skai_air_mix/skai.talon_archer | seat0 | eliminated | 18.6 | 0.225 | 0.000 | 940/1000 |
| choke/1000/skai.talon_archer/water_pressure_mix | seat0 | eliminated | 18.1 | 0.433 | 0.000 | 1000/675 |
| choke/1000/water_pressure_mix/skai.talon_archer | seat1 | eliminated | 20.1 | 0.000 | 0.273 | 675/1000 |
| choke/1000/skai.talon_archer/water_expand_mix | seat1 | eliminated | 21.1 | 0.000 | 0.597 | 1000/865 |
| choke/1000/water_expand_mix/skai.talon_archer | seat0 | eliminated | 22.0 | 0.664 | 0.000 | 865/1000 |
| choke/1000/skai.talon_archer/water_leviathan_mix | seat1 | eliminated | 8.1 | 0.000 | 0.874 | 1000/1235 |
| choke/1000/water_leviathan_mix/skai.talon_archer | seat0 | eliminated | 7.8 | 0.947 | 0.000 | 1235/1000 |
| choke/1000/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.5 | 0.777 | 0.000 | 1060/975 |
| choke/1000/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 0.798 | 975/1060 |
| choke/1000/skai.thunderhead/water.grasper | seat0 | eliminated | 9.1 | 1.000 | 0.000 | 1060/1000 |
| choke/1000/water.grasper/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1000/1060 |
| choke/1000/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 9.3 | 1.000 | 0.000 | 1060/950 |
| choke/1000/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 950/1060 |
| choke/1000/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.777 | 1060/1140 |
| choke/1000/water.leviathan/skai.thunderhead | seat0 | eliminated | 15.6 | 0.382 | 0.000 | 1140/1060 |
| choke/1000/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.5 | 1.000 | 0.000 | 1060/975 |
| choke/1000/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 975/1060 |
| choke/1000/skai.thunderhead/water.shellback | seat0 | eliminated | 49.3 | 1.000 | 0.000 | 1060/1050 |
| choke/1000/water.shellback/skai.thunderhead | seat1 | eliminated | 44.3 | 0.000 | 1.000 | 1050/1060 |
| choke/1000/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.3 | 0.868 | 0.000 | 1060/1000 |
| choke/1000/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.4 | 0.000 | 0.855 | 1000/1060 |
| choke/1000/skai.thunderhead/skai_pressure_mix | seat0 | eliminated | 11.4 | 0.668 | 0.000 | 1060/1100 |
| choke/1000/skai_pressure_mix/skai.thunderhead | seat1 | eliminated | 13.9 | 0.000 | 0.612 | 1100/1060 |
| choke/1000/skai.thunderhead/skai_expand_mix | seat1 | eliminated | 32.5 | 0.000 | 0.308 | 1060/1300 |
| choke/1000/skai_expand_mix/skai.thunderhead | seat0 | eliminated | 27.7 | 0.308 | 0.000 | 1300/1060 |
| choke/1000/skai.thunderhead/skai_air_mix | seat1 | eliminated | 31.3 | 0.000 | 0.920 | 1060/940 |
| choke/1000/skai_air_mix/skai.thunderhead | seat0 | eliminated | 32.5 | 0.920 | 0.000 | 940/1060 |
| choke/1000/skai.thunderhead/water_pressure_mix | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 1060/675 |
| choke/1000/water_pressure_mix/skai.thunderhead | seat1 | eliminated | 9.1 | 0.000 | 1.000 | 675/1060 |
| choke/1000/skai.thunderhead/water_expand_mix | seat0 | eliminated | 19.1 | 0.750 | 0.000 | 1060/865 |
| choke/1000/water_expand_mix/skai.thunderhead | seat1 | eliminated | 24.1 | 0.000 | 0.400 | 865/1060 |
| choke/1000/skai.thunderhead/water_leviathan_mix | seat0 | eliminated | 189.8 | 0.145 | 0.000 | 1060/1235 |
| choke/1000/water_leviathan_mix/skai.thunderhead | seat0 | eliminated | 13.1 | 0.790 | 0.000 | 1235/1060 |
| choke/1000/skai.windrunner/water.grasper | seat1 | eliminated | 10.9 | 0.000 | 0.614 | 975/1000 |
| choke/1000/water.grasper/skai.windrunner | seat0 | eliminated | 11.0 | 0.622 | 0.000 | 1000/975 |
| choke/1000/skai.windrunner/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.256 | 975/950 |
| choke/1000/water.ink_lobber/skai.windrunner | seat0 | eliminated | 10.6 | 0.346 | 0.000 | 950/975 |
| choke/1000/skai.windrunner/water.leviathan | seat1 | eliminated | 2.0 | 0.000 | 1.000 | 975/1140 |
| choke/1000/water.leviathan/skai.windrunner | seat0 | eliminated | 2.0 | 1.000 | 0.000 | 1140/975 |
| choke/1000/skai.windrunner/water.reef_runner | seat0 | eliminated | 19.9 | 0.069 | 0.000 | 975/975 |
| choke/1000/water.reef_runner/skai.windrunner | seat1 | eliminated | 19.1 | 0.000 | 0.054 | 975/975 |
| choke/1000/skai.windrunner/water.shellback | seat1 | eliminated | 17.2 | 0.000 | 0.884 | 975/1050 |
| choke/1000/water.shellback/skai.windrunner | seat0 | eliminated | 18.0 | 0.973 | 0.000 | 1050/975 |
| choke/1000/skai.windrunner/water.spoutling | seat0 | eliminated | 8.0 | 0.795 | 0.000 | 975/1000 |
| choke/1000/water.spoutling/skai.windrunner | seat1 | eliminated | 8.1 | 0.000 | 0.776 | 1000/975 |
| choke/1000/skai.windrunner/skai_pressure_mix | seat1 | eliminated | 14.3 | 0.000 | 0.302 | 975/1100 |
| choke/1000/skai_pressure_mix/skai.windrunner | seat0 | eliminated | 13.8 | 0.351 | 0.000 | 1100/975 |
| choke/1000/skai.windrunner/skai_expand_mix | seat1 | eliminated | 10.2 | 0.000 | 0.644 | 975/1300 |
| choke/1000/skai_expand_mix/skai.windrunner | seat0 | eliminated | 10.8 | 0.639 | 0.000 | 1300/975 |
| choke/1000/skai.windrunner/skai_air_mix | seat1 | eliminated | 16.5 | 0.000 | 0.213 | 975/940 |
| choke/1000/skai_air_mix/skai.windrunner | seat0 | eliminated | 14.6 | 0.263 | 0.000 | 940/975 |
| choke/1000/skai.windrunner/water_pressure_mix | seat0 | eliminated | 15.1 | 0.385 | 0.000 | 975/675 |
| choke/1000/water_pressure_mix/skai.windrunner | seat1 | eliminated | 17.4 | 0.000 | 0.254 | 675/975 |
| choke/1000/skai.windrunner/water_expand_mix | seat1 | eliminated | 18.2 | 0.000 | 0.443 | 975/865 |
| choke/1000/water_expand_mix/skai.windrunner | seat0 | eliminated | 18.3 | 0.682 | 0.000 | 865/975 |
| choke/1000/skai.windrunner/water_leviathan_mix | seat1 | eliminated | 6.8 | 0.000 | 0.991 | 975/1235 |
| choke/1000/water_leviathan_mix/skai.windrunner | seat0 | eliminated | 5.4 | 1.000 | 0.000 | 1235/975 |
| choke/1000/water.grasper/water.ink_lobber | seat1 | eliminated | 17.0 | 0.000 | 0.032 | 1000/950 |
| choke/1000/water.ink_lobber/water.grasper | seat1 | eliminated | 11.9 | 0.000 | 0.120 | 950/1000 |
| choke/1000/water.grasper/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 1000/1140 |
| choke/1000/water.leviathan/water.grasper | seat0 | eliminated | 4.6 | 1.000 | 0.000 | 1140/1000 |
| choke/1000/water.grasper/water.reef_runner | seat0 | eliminated | 11.0 | 0.610 | 0.000 | 1000/975 |
| choke/1000/water.reef_runner/water.grasper | seat1 | eliminated | 11.3 | 0.000 | 0.610 | 975/1000 |
| choke/1000/water.grasper/water.shellback | seat0 | eliminated | 22.8 | 0.498 | 0.000 | 1000/1050 |
| choke/1000/water.shellback/water.grasper | seat1 | eliminated | 24.0 | 0.000 | 0.464 | 1050/1000 |
| choke/1000/water.grasper/water.spoutling | seat0 | eliminated | 8.8 | 0.818 | 0.000 | 1000/1000 |
| choke/1000/water.spoutling/water.grasper | seat1 | eliminated | 9.0 | 0.000 | 0.814 | 1000/1000 |
| choke/1000/water.grasper/skai_pressure_mix | seat0 | eliminated | 13.7 | 0.533 | 0.000 | 1000/1100 |
| choke/1000/skai_pressure_mix/water.grasper | seat1 | eliminated | 12.9 | 0.000 | 0.545 | 1100/1000 |
| choke/1000/water.grasper/skai_expand_mix | seat1 | eliminated | 44.7 | 0.000 | 0.308 | 1000/1300 |
| choke/1000/skai_expand_mix/water.grasper | seat0 | eliminated | 46.0 | 0.308 | 0.000 | 1300/1000 |
| choke/1000/water.grasper/skai_air_mix | seat1 | eliminated | 17.2 | 0.000 | 0.920 | 1000/940 |
| choke/1000/skai_air_mix/water.grasper | seat0 | eliminated | 18.4 | 0.920 | 0.000 | 940/1000 |
| choke/1000/water.grasper/water_pressure_mix | seat0 | eliminated | 11.8 | 0.671 | 0.000 | 1000/675 |
| choke/1000/water_pressure_mix/water.grasper | seat1 | eliminated | 12.3 | 0.000 | 0.665 | 675/1000 |
| choke/1000/water.grasper/water_expand_mix | seat0 | eliminated | 14.9 | 0.396 | 0.000 | 1000/865 |
| choke/1000/water_expand_mix/water.grasper | seat1 | eliminated | 13.9 | 0.000 | 0.587 | 865/1000 |
| choke/1000/water.grasper/water_leviathan_mix | seat1 | eliminated | 7.8 | 0.000 | 0.671 | 1000/1235 |
| choke/1000/water_leviathan_mix/water.grasper | seat0 | eliminated | 8.3 | 0.794 | 0.000 | 1235/1000 |
| choke/1000/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.850 | 950/1140 |
| choke/1000/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.5 | 0.850 | 0.000 | 1140/950 |
| choke/1000/water.ink_lobber/water.reef_runner | seat0 | eliminated | 10.7 | 0.444 | 0.000 | 950/975 |
| choke/1000/water.reef_runner/water.ink_lobber | seat1 | eliminated | 10.9 | 0.000 | 0.432 | 975/950 |
| choke/1000/water.ink_lobber/water.shellback | seat1 | eliminated | 15.2 | 0.000 | 0.932 | 950/1050 |
| choke/1000/water.shellback/water.ink_lobber | seat0 | eliminated | 15.4 | 1.000 | 0.000 | 1050/950 |
| choke/1000/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.9 | 0.800 | 0.000 | 950/1000 |
| choke/1000/water.spoutling/water.ink_lobber | seat1 | eliminated | 7.9 | 0.000 | 0.820 | 1000/950 |
| choke/1000/water.ink_lobber/skai_pressure_mix | seat1 | eliminated | 13.6 | 0.000 | 0.204 | 950/1100 |
| choke/1000/skai_pressure_mix/water.ink_lobber | seat0 | eliminated | 12.1 | 0.195 | 0.000 | 1100/950 |
| choke/1000/water.ink_lobber/skai_expand_mix | seat1 | eliminated | 8.8 | 0.000 | 0.665 | 950/1300 |
| choke/1000/skai_expand_mix/water.ink_lobber | seat0 | eliminated | 9.3 | 0.562 | 0.000 | 1300/950 |
| choke/1000/water.ink_lobber/skai_air_mix | seat1 | eliminated | 9.1 | 0.000 | 0.920 | 950/940 |
| choke/1000/skai_air_mix/water.ink_lobber | seat0 | eliminated | 9.3 | 0.920 | 0.000 | 940/950 |
| choke/1000/water.ink_lobber/water_pressure_mix | seat0 | eliminated | 8.2 | 0.800 | 0.000 | 950/675 |
| choke/1000/water_pressure_mix/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.632 | 675/950 |
| choke/1000/water.ink_lobber/water_expand_mix | seat1 | eliminated | 15.1 | 0.000 | 0.409 | 950/865 |
| choke/1000/water_expand_mix/water.ink_lobber | seat0 | eliminated | 15.4 | 0.686 | 0.000 | 865/950 |
| choke/1000/water.ink_lobber/water_leviathan_mix | seat1 | eliminated | 7.9 | 0.000 | 0.899 | 950/1235 |
| choke/1000/water_leviathan_mix/water.ink_lobber | seat0 | eliminated | 8.1 | 1.000 | 0.000 | 1235/950 |
| choke/1000/water.leviathan/water.reef_runner | seat0 | eliminated | 2.1 | 1.000 | 0.000 | 1140/975 |
| choke/1000/water.reef_runner/water.leviathan | seat1 | eliminated | 2.1 | 0.000 | 1.000 | 975/1140 |
| choke/1000/water.leviathan/water.shellback | seat1 | eliminated | 29.8 | 0.000 | 0.437 | 1140/1050 |
| choke/1000/water.shellback/water.leviathan | seat0 | eliminated | 32.1 | 0.333 | 0.000 | 1050/1140 |
| choke/1000/water.leviathan/water.spoutling | seat0 | eliminated | 2.1 | 1.000 | 0.000 | 1140/1000 |
| choke/1000/water.spoutling/water.leviathan | seat1 | eliminated | 2.1 | 0.000 | 1.000 | 1000/1140 |
| choke/1000/water.leviathan/skai_pressure_mix | seat0 | eliminated | 4.3 | 0.973 | 0.000 | 1140/1100 |
| choke/1000/skai_pressure_mix/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.979 | 1100/1140 |
| choke/1000/water.leviathan/skai_expand_mix | seat0 | eliminated | 4.4 | 0.932 | 0.000 | 1140/1300 |
| choke/1000/skai_expand_mix/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.914 | 1300/1140 |
| choke/1000/water.leviathan/skai_air_mix | seat0 | eliminated | 27.6 | 0.223 | 0.000 | 1140/940 |
| choke/1000/skai_air_mix/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.883 | 940/1140 |
| choke/1000/water.leviathan/water_pressure_mix | seat0 | eliminated | 2.0 | 1.000 | 0.000 | 1140/675 |
| choke/1000/water_pressure_mix/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 1.000 | 675/1140 |
| choke/1000/water.leviathan/water_expand_mix | seat0 | eliminated | 13.2 | 0.733 | 0.000 | 1140/865 |
| choke/1000/water_expand_mix/water.leviathan | seat1 | eliminated | 10.9 | 0.000 | 0.809 | 865/1140 |
| choke/1000/water.leviathan/water_leviathan_mix | seat1 | eliminated | 14.5 | 0.000 | 0.392 | 1140/1235 |
| choke/1000/water_leviathan_mix/water.leviathan | seat0 | eliminated | 16.0 | 0.438 | 0.000 | 1235/1140 |
| choke/1000/water.reef_runner/water.shellback | seat1 | eliminated | 17.8 | 0.000 | 0.918 | 975/1050 |
| choke/1000/water.shellback/water.reef_runner | seat0 | eliminated | 16.8 | 0.992 | 0.000 | 1050/975 |
| choke/1000/water.reef_runner/water.spoutling | seat0 | eliminated | 8.9 | 0.756 | 0.000 | 975/1000 |
| choke/1000/water.spoutling/water.reef_runner | seat1 | eliminated | 8.6 | 0.000 | 0.763 | 1000/975 |
| choke/1000/water.reef_runner/skai_pressure_mix | seat1 | eliminated | 14.3 | 0.000 | 0.339 | 975/1100 |
| choke/1000/skai_pressure_mix/water.reef_runner | seat0 | eliminated | 14.1 | 0.339 | 0.000 | 1100/975 |
| choke/1000/water.reef_runner/skai_expand_mix | seat1 | eliminated | 14.6 | 0.000 | 0.462 | 975/1300 |
| choke/1000/skai_expand_mix/water.reef_runner | seat0 | eliminated | 12.8 | 0.528 | 0.000 | 1300/975 |
| choke/1000/water.reef_runner/skai_air_mix | seat1 | eliminated | 12.2 | 0.000 | 0.920 | 975/940 |
| choke/1000/skai_air_mix/water.reef_runner | seat0 | eliminated | 13.4 | 0.920 | 0.000 | 940/975 |
| choke/1000/water.reef_runner/water_pressure_mix | seat0 | eliminated | 16.7 | 0.323 | 0.000 | 975/675 |
| choke/1000/water_pressure_mix/water.reef_runner | seat1 | eliminated | 21.6 | 0.000 | 0.092 | 675/975 |
| choke/1000/water.reef_runner/water_expand_mix | seat1 | eliminated | 16.9 | 0.000 | 0.448 | 975/865 |
| choke/1000/water_expand_mix/water.reef_runner | seat0 | eliminated | 16.5 | 0.752 | 0.000 | 865/975 |
| choke/1000/water.reef_runner/water_leviathan_mix | seat1 | eliminated | 7.3 | 0.000 | 0.991 | 975/1235 |
| choke/1000/water_leviathan_mix/water.reef_runner | seat0 | eliminated | 7.3 | 0.974 | 0.000 | 1235/975 |
| choke/1000/water.shellback/water.spoutling | seat0 | eliminated | 14.4 | 1.000 | 0.000 | 1050/1000 |
| choke/1000/water.spoutling/water.shellback | seat1 | eliminated | 15.2 | 0.000 | 0.989 | 1000/1050 |
| choke/1000/water.shellback/skai_pressure_mix | seat0 | eliminated | 20.4 | 0.974 | 0.000 | 1050/1100 |
| choke/1000/skai_pressure_mix/water.shellback | seat1 | eliminated | 20.1 | 0.000 | 0.903 | 1100/1050 |
| choke/1000/water.shellback/skai_expand_mix | stalemate | dome_absorbed | 48.6 | 0.873 | 0.308 | 1050/1300 |
| choke/1000/skai_expand_mix/water.shellback | stalemate | dome_absorbed | 48.7 | 0.308 | 0.813 | 1300/1050 |
| choke/1000/water.shellback/skai_air_mix | stalemate | dome_absorbed | 37.8 | 0.960 | 0.920 | 1050/940 |
| choke/1000/skai_air_mix/water.shellback | stalemate | dome_absorbed | 37.8 | 0.920 | 0.885 | 940/1050 |
| choke/1000/water.shellback/water_pressure_mix | seat0 | eliminated | 22.0 | 0.533 | 0.000 | 1050/675 |
| choke/1000/water_pressure_mix/water.shellback | seat1 | eliminated | 21.8 | 0.000 | 0.595 | 675/1050 |
| choke/1000/water.shellback/water_expand_mix | seat0 | eliminated | 30.7 | 0.850 | 0.000 | 1050/865 |
| choke/1000/water_expand_mix/water.shellback | seat1 | eliminated | 37.5 | 0.000 | 0.833 | 865/1050 |
| choke/1000/water.shellback/water_leviathan_mix | seat0 | eliminated | 40.2 | 0.857 | 0.000 | 1050/1235 |
| choke/1000/water_leviathan_mix/water.shellback | seat1 | eliminated | 37.1 | 0.000 | 0.894 | 1235/1050 |
| choke/1000/water.spoutling/skai_pressure_mix | seat1 | eliminated | 7.9 | 0.000 | 0.807 | 1000/1100 |
| choke/1000/skai_pressure_mix/water.spoutling | seat0 | eliminated | 7.9 | 0.813 | 0.000 | 1100/1000 |
| choke/1000/water.spoutling/skai_expand_mix | seat1 | eliminated | 7.6 | 0.000 | 0.851 | 1000/1300 |
| choke/1000/skai_expand_mix/water.spoutling | seat0 | eliminated | 7.6 | 0.856 | 0.000 | 1300/1000 |
| choke/1000/water.spoutling/skai_air_mix | seat1 | eliminated | 10.0 | 0.000 | 0.754 | 1000/940 |
| choke/1000/skai_air_mix/water.spoutling | seat0 | eliminated | 11.3 | 0.714 | 0.000 | 940/1000 |
| choke/1000/water.spoutling/water_pressure_mix | seat1 | eliminated | 12.5 | 0.000 | 0.593 | 1000/675 |
| choke/1000/water_pressure_mix/water.spoutling | seat0 | eliminated | 12.5 | 0.630 | 0.000 | 675/1000 |
| choke/1000/water.spoutling/water_expand_mix | seat1 | eliminated | 11.3 | 0.000 | 0.938 | 1000/865 |
| choke/1000/water_expand_mix/water.spoutling | seat0 | eliminated | 11.4 | 0.926 | 0.000 | 865/1000 |
| choke/1000/water.spoutling/water_leviathan_mix | seat1 | eliminated | 4.8 | 0.000 | 0.978 | 1000/1235 |
| choke/1000/water_leviathan_mix/water.spoutling | seat0 | eliminated | 4.9 | 1.000 | 0.000 | 1235/1000 |
| choke/1000/skai_pressure_mix/skai_expand_mix | seat1 | eliminated | 14.6 | 0.000 | 0.501 | 1100/1300 |
| choke/1000/skai_expand_mix/skai_pressure_mix | seat0 | eliminated | 12.9 | 0.550 | 0.000 | 1300/1100 |
| choke/1000/skai_pressure_mix/skai_air_mix | seat1 | eliminated | 24.2 | 0.000 | 0.040 | 1100/940 |
| choke/1000/skai_air_mix/skai_pressure_mix | seat0 | eliminated | 18.9 | 0.136 | 0.000 | 940/1100 |
| choke/1000/skai_pressure_mix/water_pressure_mix | seat0 | eliminated | 14.2 | 0.455 | 0.000 | 1100/675 |
| choke/1000/water_pressure_mix/skai_pressure_mix | seat1 | eliminated | 14.0 | 0.000 | 0.495 | 675/1100 |
| choke/1000/skai_pressure_mix/water_expand_mix | seat1 | eliminated | 23.5 | 0.000 | 0.411 | 1100/865 |
| choke/1000/water_expand_mix/skai_pressure_mix | seat0 | eliminated | 22.7 | 0.607 | 0.000 | 865/1100 |
| choke/1000/skai_pressure_mix/water_leviathan_mix | seat1 | eliminated | 9.7 | 0.000 | 0.895 | 1100/1235 |
| choke/1000/water_leviathan_mix/skai_pressure_mix | seat0 | eliminated | 8.2 | 1.000 | 0.000 | 1235/1100 |
| choke/1000/skai_expand_mix/skai_air_mix | seat0 | eliminated | 14.0 | 0.380 | 0.000 | 1300/940 |
| choke/1000/skai_air_mix/skai_expand_mix | seat1 | eliminated | 14.0 | 0.000 | 0.503 | 940/1300 |
| choke/1000/skai_expand_mix/water_pressure_mix | seat0 | eliminated | 13.6 | 0.641 | 0.000 | 1300/675 |
| choke/1000/water_pressure_mix/skai_expand_mix | seat1 | eliminated | 13.5 | 0.000 | 0.626 | 675/1300 |
| choke/1000/skai_expand_mix/water_expand_mix | seat0 | eliminated | 40.1 | 0.276 | 0.000 | 1300/865 |
| choke/1000/water_expand_mix/skai_expand_mix | seat0 | eliminated | 28.3 | 0.520 | 0.000 | 865/1300 |
| choke/1000/skai_expand_mix/water_leviathan_mix | seat1 | eliminated | 10.2 | 0.000 | 0.741 | 1300/1235 |
| choke/1000/water_leviathan_mix/skai_expand_mix | seat0 | eliminated | 9.8 | 0.860 | 0.000 | 1235/1300 |
| choke/1000/skai_air_mix/water_pressure_mix | seat0 | eliminated | 11.6 | 0.920 | 0.000 | 940/675 |
| choke/1000/water_pressure_mix/skai_air_mix | seat1 | eliminated | 13.5 | 0.000 | 0.920 | 675/940 |
| choke/1000/skai_air_mix/water_expand_mix | seat0 | eliminated | 31.6 | 0.726 | 0.000 | 940/865 |
| choke/1000/water_expand_mix/skai_air_mix | seat0 | eliminated | 30.0 | 0.618 | 0.000 | 865/940 |
| choke/1000/skai_air_mix/water_leviathan_mix | seat1 | eliminated | 10.1 | 0.000 | 0.793 | 940/1235 |
| choke/1000/water_leviathan_mix/skai_air_mix | seat0 | eliminated | 13.3 | 0.802 | 0.000 | 1235/940 |
| choke/1000/water_pressure_mix/water_expand_mix | seat1 | eliminated | 22.8 | 0.000 | 0.376 | 675/865 |
| choke/1000/water_expand_mix/water_pressure_mix | seat0 | eliminated | 20.0 | 0.376 | 0.000 | 865/675 |
| choke/1000/water_pressure_mix/water_leviathan_mix | seat1 | eliminated | 6.0 | 0.000 | 0.913 | 675/1235 |
| choke/1000/water_leviathan_mix/water_pressure_mix | seat0 | eliminated | 7.3 | 0.870 | 0.000 | 1235/675 |
| choke/1000/water_expand_mix/water_leviathan_mix | seat1 | eliminated | 16.6 | 0.000 | 0.899 | 865/1235 |
| choke/1000/water_leviathan_mix/water_expand_mix | seat0 | eliminated | 20.0 | 0.776 | 0.000 | 1235/865 |
| flat_uneven/2000/skai.roc_rider/skai.stormcaller/r12500 | seat0 | eliminated | 13.7 | 1.000 | 0.000 | 2000/2470 |
| flat_uneven/2000/skai.stormcaller/skai.roc_rider/r12500 | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 2090/2600 |
| flat_uneven/2000/skai.roc_rider/skai.talon_archer/r12500 | seat1 | eliminated | 15.2 | 0.000 | 0.577 | 2000/2500 |
| flat_uneven/2000/skai.talon_archer/skai.roc_rider/r12500 | seat1 | eliminated | 18.8 | 0.000 | 0.486 | 2000/2600 |
| flat_uneven/2000/skai.roc_rider/skai.thunderhead/r12500 | seat0 | eliminated | 22.9 | 1.000 | 0.000 | 2000/2385 |
| flat_uneven/2000/skai.thunderhead/skai.roc_rider/r12500 | seat1 | eliminated | 15.7 | 0.000 | 1.000 | 2120/2600 |
| flat_uneven/2000/skai.roc_rider/skai.windrunner/r12500 | seat1 | eliminated | 12.8 | 0.000 | 0.640 | 2000/2475 |
| flat_uneven/2000/skai.windrunner/skai.roc_rider/r12500 | seat1 | eliminated | 23.1 | 0.000 | 0.287 | 2025/2600 |
| flat_uneven/2000/skai.roc_rider/water.grasper/r12500 | seat0 | eliminated | 36.7 | 1.000 | 0.000 | 2000/2500 |
| flat_uneven/2000/water.grasper/skai.roc_rider/r12500 | seat1 | eliminated | 23.2 | 0.000 | 1.000 | 2000/2600 |
| flat_uneven/2000/skai.roc_rider/water.ink_lobber/r12500 | seat0 | eliminated | 15.0 | 1.000 | 0.000 | 2000/2470 |
| flat_uneven/2000/water.ink_lobber/skai.roc_rider/r12500 | seat1 | eliminated | 10.1 | 0.000 | 1.000 | 2090/2600 |
| flat_uneven/2000/skai.roc_rider/water.leviathan/r12500 | seat1 | eliminated | 4.4 | 0.000 | 0.960 | 2000/2660 |
| flat_uneven/2000/water.leviathan/skai.roc_rider/r12500 | seat0 | eliminated | 7.0 | 0.903 | 0.000 | 1900/2600 |
| flat_uneven/2000/skai.roc_rider/water.reef_runner/r12500 | seat0 | eliminated | 24.2 | 1.000 | 0.000 | 2000/2475 |
| flat_uneven/2000/water.reef_runner/skai.roc_rider/r12500 | seat1 | eliminated | 15.8 | 0.000 | 1.000 | 2025/2600 |
| flat_uneven/2000/skai.roc_rider/water.shellback/r12500 | stalemate | dome_absorbed | 64.1 | 1.000 | 0.977 | 2000/2450 |
| flat_uneven/2000/water.shellback/skai.roc_rider/r12500 | seat1 | eliminated | 101.7 | 0.000 | 1.000 | 1925/2600 |
| flat_uneven/2000/skai.roc_rider/water.spoutling/r12500 | seat0 | eliminated | 16.5 | 0.519 | 0.000 | 2000/2500 |
| flat_uneven/2000/water.spoutling/skai.roc_rider/r12500 | seat1 | eliminated | 9.3 | 0.000 | 0.822 | 2000/2600 |
| flat_uneven/2000/skai.roc_rider/skai_pressure_mix/r12500 | seat1 | eliminated | 11.4 | 0.000 | 0.705 | 2000/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.roc_rider/r12500 | seat1 | eliminated | 37.7 | 0.000 | 0.066 | 2200/2600 |
| flat_uneven/2000/skai.roc_rider/skai_expand_mix/r12500 | seat1 | eliminated | 15.7 | 0.000 | 0.600 | 2000/2600 |
| flat_uneven/2000/skai_expand_mix/skai.roc_rider/r12500 | seat1 | eliminated | 17.3 | 0.000 | 0.523 | 1950/2600 |
| flat_uneven/2000/skai.roc_rider/skai_air_mix/r12500 | seat1 | eliminated | 24.5 | 0.000 | 0.455 | 2000/2820 |
| flat_uneven/2000/skai_air_mix/skai.roc_rider/r12500 | seat1 | eliminated | 16.2 | 0.000 | 0.681 | 1880/2600 |
| flat_uneven/2000/skai.roc_rider/water_pressure_mix/r12500 | seat0 | eliminated | 38.7 | 1.000 | 0.000 | 2000/2700 |
| flat_uneven/2000/water_pressure_mix/skai.roc_rider/r12500 | seat1 | eliminated | 22.4 | 0.000 | 1.000 | 2025/2600 |
| flat_uneven/2000/skai.roc_rider/water_expand_mix/r12500 | seat1 | eliminated | 22.5 | 0.000 | 0.858 | 2000/2595 |
| flat_uneven/2000/water_expand_mix/skai.roc_rider/r12500 | seat1 | eliminated | 31.9 | 0.000 | 0.470 | 1730/2600 |
| flat_uneven/2000/skai.roc_rider/water_leviathan_mix/r12500 | seat1 | eliminated | 10.7 | 0.000 | 0.888 | 2000/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.roc_rider/r12500 | seat0 | eliminated | 13.2 | 0.841 | 0.000 | 2470/2600 |
| flat_uneven/2000/skai.stormcaller/skai.talon_archer/r12500 | seat1 | eliminated | 9.0 | 0.000 | 0.328 | 2090/2500 |
| flat_uneven/2000/skai.talon_archer/skai.stormcaller/r12500 | seat1 | eliminated | 9.7 | 0.000 | 0.526 | 2000/2470 |
| flat_uneven/2000/skai.stormcaller/skai.thunderhead/r12500 | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 2090/2385 |
| flat_uneven/2000/skai.thunderhead/skai.stormcaller/r12500 | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 2120/2470 |
| flat_uneven/2000/skai.stormcaller/skai.windrunner/r12500 | seat1 | eliminated | 8.2 | 0.000 | 0.389 | 2090/2475 |
| flat_uneven/2000/skai.windrunner/skai.stormcaller/r12500 | seat1 | eliminated | 9.1 | 0.000 | 0.530 | 2025/2470 |
| flat_uneven/2000/skai.stormcaller/water.grasper/r12500 | seat1 | eliminated | 13.2 | 0.000 | 0.165 | 2090/2500 |
| flat_uneven/2000/water.grasper/skai.stormcaller/r12500 | seat1 | eliminated | 9.8 | 0.000 | 0.610 | 2000/2470 |
| flat_uneven/2000/skai.stormcaller/water.ink_lobber/r12500 | seat1 | eliminated | 6.7 | 0.000 | 0.333 | 2090/2470 |
| flat_uneven/2000/water.ink_lobber/skai.stormcaller/r12500 | seat1 | eliminated | 10.9 | 0.000 | 0.108 | 2090/2470 |
| flat_uneven/2000/skai.stormcaller/water.leviathan/r12500 | seat1 | eliminated | 4.2 | 0.000 | 0.964 | 2090/2660 |
| flat_uneven/2000/water.leviathan/skai.stormcaller/r12500 | seat0 | eliminated | 4.4 | 0.916 | 0.000 | 1900/2470 |
| flat_uneven/2000/skai.stormcaller/water.reef_runner/r12500 | seat1 | eliminated | 9.8 | 0.000 | 0.251 | 2090/2475 |
| flat_uneven/2000/water.reef_runner/skai.stormcaller/r12500 | seat1 | eliminated | 9.5 | 0.000 | 0.549 | 2025/2470 |
| flat_uneven/2000/skai.stormcaller/water.shellback/r12500 | seat1 | eliminated | 14.5 | 0.000 | 0.984 | 2090/2450 |
| flat_uneven/2000/water.shellback/skai.stormcaller/r12500 | seat0 | eliminated | 17.6 | 0.885 | 0.000 | 1925/2470 |
| flat_uneven/2000/skai.stormcaller/water.spoutling/r12500 | seat0 | eliminated | 7.4 | 0.742 | 0.000 | 2090/2500 |
| flat_uneven/2000/water.spoutling/skai.stormcaller/r12500 | seat1 | eliminated | 6.8 | 0.000 | 0.893 | 2000/2470 |
| flat_uneven/2000/skai.stormcaller/skai_pressure_mix/r12500 | seat1 | eliminated | 9.0 | 0.000 | 0.427 | 2090/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.stormcaller/r12500 | seat1 | eliminated | 16.8 | 0.000 | 0.231 | 2200/2470 |
| flat_uneven/2000/skai.stormcaller/skai_expand_mix/r12500 | seat1 | eliminated | 9.4 | 0.000 | 0.519 | 2090/2600 |
| flat_uneven/2000/skai_expand_mix/skai.stormcaller/r12500 | seat0 | eliminated | 17.3 | 0.327 | 0.000 | 1950/2470 |
| flat_uneven/2000/skai.stormcaller/skai_air_mix/r12500 | seat1 | eliminated | 5.5 | 0.000 | 0.920 | 2090/2820 |
| flat_uneven/2000/skai_air_mix/skai.stormcaller/r12500 | seat0 | eliminated | 10.4 | 0.920 | 0.000 | 1880/2470 |
| flat_uneven/2000/skai.stormcaller/water_pressure_mix/r12500 | seat1 | eliminated | 10.6 | 0.000 | 0.329 | 2090/2700 |
| flat_uneven/2000/water_pressure_mix/skai.stormcaller/r12500 | seat1 | eliminated | 12.0 | 0.000 | 0.538 | 2025/2470 |
| flat_uneven/2000/skai.stormcaller/water_expand_mix/r12500 | seat1 | eliminated | 11.8 | 0.000 | 0.822 | 2090/2595 |
| flat_uneven/2000/water_expand_mix/skai.stormcaller/r12500 | seat0 | eliminated | 18.5 | 0.616 | 0.000 | 1730/2470 |
| flat_uneven/2000/skai.stormcaller/water_leviathan_mix/r12500 | seat1 | eliminated | 7.6 | 0.000 | 0.924 | 2090/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.stormcaller/r12500 | seat0 | eliminated | 8.1 | 1.000 | 0.000 | 2470/2470 |
| flat_uneven/2000/skai.talon_archer/skai.thunderhead/r12500 | seat1 | eliminated | 7.3 | 0.000 | 0.745 | 2000/2385 |
| flat_uneven/2000/skai.thunderhead/skai.talon_archer/r12500 | seat0 | eliminated | 9.7 | 0.582 | 0.000 | 2120/2500 |
| flat_uneven/2000/skai.talon_archer/skai.windrunner/r12500 | seat1 | eliminated | 12.5 | 0.000 | 0.552 | 2000/2475 |
| flat_uneven/2000/skai.windrunner/skai.talon_archer/r12500 | seat1 | eliminated | 15.2 | 0.000 | 0.445 | 2025/2500 |
| flat_uneven/2000/skai.talon_archer/water.grasper/r12500 | seat1 | eliminated | 11.9 | 0.000 | 0.723 | 2000/2500 |
| flat_uneven/2000/water.grasper/skai.talon_archer/r12500 | seat0 | eliminated | 22.7 | 0.276 | 0.000 | 2000/2500 |
| flat_uneven/2000/skai.talon_archer/water.ink_lobber/r12500 | seat1 | eliminated | 8.4 | 0.000 | 0.632 | 2000/2470 |
| flat_uneven/2000/water.ink_lobber/skai.talon_archer/r12500 | seat1 | eliminated | 13.1 | 0.000 | 0.096 | 2090/2500 |
| flat_uneven/2000/skai.talon_archer/water.leviathan/r12500 | seat1 | eliminated | 4.0 | 0.000 | 0.991 | 2000/2660 |
| flat_uneven/2000/water.leviathan/skai.talon_archer/r12500 | seat0 | eliminated | 4.7 | 0.946 | 0.000 | 1900/2500 |
| flat_uneven/2000/skai.talon_archer/water.reef_runner/r12500 | seat1 | eliminated | 13.2 | 0.000 | 0.562 | 2000/2475 |
| flat_uneven/2000/water.reef_runner/skai.talon_archer/r12500 | seat1 | eliminated | 14.0 | 0.000 | 0.512 | 2025/2500 |
| flat_uneven/2000/skai.talon_archer/water.shellback/r12500 | seat1 | eliminated | 19.7 | 0.000 | 0.944 | 2000/2450 |
| flat_uneven/2000/water.shellback/skai.talon_archer/r12500 | seat0 | eliminated | 27.6 | 0.904 | 0.000 | 1925/2500 |
| flat_uneven/2000/skai.talon_archer/water.spoutling/r12500 | seat0 | eliminated | 12.3 | 0.711 | 0.000 | 2000/2500 |
| flat_uneven/2000/water.spoutling/skai.talon_archer/r12500 | seat1 | eliminated | 8.3 | 0.000 | 0.884 | 2000/2500 |
| flat_uneven/2000/skai.talon_archer/skai_pressure_mix/r12500 | seat1 | eliminated | 11.5 | 0.000 | 0.647 | 2000/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.talon_archer/r12500 | seat1 | eliminated | 18.0 | 0.000 | 0.373 | 2200/2500 |
| flat_uneven/2000/skai.talon_archer/skai_expand_mix/r12500 | seat1 | eliminated | 15.1 | 0.000 | 0.529 | 2000/2600 |
| flat_uneven/2000/skai_expand_mix/skai.talon_archer/r12500 | seat1 | eliminated | 14.1 | 0.000 | 0.588 | 1950/2500 |
| flat_uneven/2000/skai.talon_archer/skai_air_mix/r12500 | seat1 | eliminated | 9.4 | 0.000 | 0.729 | 2000/2820 |
| flat_uneven/2000/skai_air_mix/skai.talon_archer/r12500 | seat1 | eliminated | 17.3 | 0.000 | 0.164 | 1880/2500 |
| flat_uneven/2000/skai.talon_archer/water_pressure_mix/r12500 | seat1 | eliminated | 11.2 | 0.000 | 0.758 | 2000/2700 |
| flat_uneven/2000/water_pressure_mix/skai.talon_archer/r12500 | seat0 | eliminated | 21.5 | 0.316 | 0.000 | 2025/2500 |
| flat_uneven/2000/skai.talon_archer/water_expand_mix/r12500 | seat1 | eliminated | 13.7 | 0.000 | 0.910 | 2000/2595 |
| flat_uneven/2000/water_expand_mix/skai.talon_archer/r12500 | seat0 | eliminated | 27.8 | 0.469 | 0.000 | 1730/2500 |
| flat_uneven/2000/skai.talon_archer/water_leviathan_mix/r12500 | seat1 | eliminated | 7.8 | 0.000 | 0.986 | 2000/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.talon_archer/r12500 | seat0 | eliminated | 9.0 | 0.924 | 0.000 | 2470/2500 |
| flat_uneven/2000/skai.thunderhead/skai.windrunner/r12500 | seat0 | eliminated | 9.0 | 0.610 | 0.000 | 2120/2475 |
| flat_uneven/2000/skai.windrunner/skai.thunderhead/r12500 | seat1 | eliminated | 6.3 | 0.000 | 0.880 | 2025/2385 |
| flat_uneven/2000/skai.thunderhead/water.grasper/r12500 | seat0 | eliminated | 9.0 | 1.000 | 0.000 | 2120/2500 |
| flat_uneven/2000/water.grasper/skai.thunderhead/r12500 | seat1 | eliminated | 6.5 | 0.000 | 1.000 | 2000/2385 |
| flat_uneven/2000/skai.thunderhead/water.ink_lobber/r12500 | seat0 | eliminated | 4.8 | 1.000 | 0.000 | 2120/2470 |
| flat_uneven/2000/water.ink_lobber/skai.thunderhead/r12500 | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 2090/2385 |
| flat_uneven/2000/skai.thunderhead/water.leviathan/r12500 | seat1 | eliminated | 4.1 | 0.000 | 0.876 | 2120/2660 |
| flat_uneven/2000/water.leviathan/skai.thunderhead/r12500 | seat0 | eliminated | 7.0 | 0.599 | 0.000 | 1900/2385 |
| flat_uneven/2000/skai.thunderhead/water.reef_runner/r12500 | seat0 | eliminated | 6.5 | 1.000 | 0.000 | 2120/2475 |
| flat_uneven/2000/water.reef_runner/skai.thunderhead/r12500 | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 2025/2385 |
| flat_uneven/2000/skai.thunderhead/water.shellback/r12500 | seat0 | eliminated | 54.4 | 1.000 | 0.000 | 2120/2450 |
| flat_uneven/2000/water.shellback/skai.thunderhead/r12500 | seat1 | eliminated | 19.6 | 0.000 | 1.000 | 1925/2385 |
| flat_uneven/2000/skai.thunderhead/water.spoutling/r12500 | seat0 | eliminated | 6.9 | 0.852 | 0.000 | 2120/2500 |
| flat_uneven/2000/water.spoutling/skai.thunderhead/r12500 | seat1 | eliminated | 4.5 | 0.000 | 0.895 | 2000/2385 |
| flat_uneven/2000/skai.thunderhead/skai_pressure_mix/r12500 | seat0 | eliminated | 14.5 | 0.465 | 0.000 | 2120/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.thunderhead/r12500 | seat1 | eliminated | 6.7 | 0.000 | 0.827 | 2200/2385 |
| flat_uneven/2000/skai.thunderhead/skai_expand_mix/r12500 | seat1 | eliminated | 37.4 | 0.000 | 0.308 | 2120/2600 |
| flat_uneven/2000/skai_expand_mix/skai.thunderhead/r12500 | seat0 | eliminated | 61.3 | 0.308 | 0.000 | 1950/2385 |
| flat_uneven/2000/skai.thunderhead/skai_air_mix/r12500 | seat1 | eliminated | 22.1 | 0.000 | 0.920 | 2120/2820 |
| flat_uneven/2000/skai_air_mix/skai.thunderhead/r12500 | seat0 | eliminated | 37.3 | 0.920 | 0.000 | 1880/2385 |
| flat_uneven/2000/skai.thunderhead/water_pressure_mix/r12500 | seat0 | eliminated | 9.0 | 1.000 | 0.000 | 2120/2700 |
| flat_uneven/2000/water_pressure_mix/skai.thunderhead/r12500 | seat1 | eliminated | 6.6 | 0.000 | 1.000 | 2025/2385 |
| flat_uneven/2000/skai.thunderhead/water_expand_mix/r12500 | seat1 | eliminated | 24.3 | 0.000 | 0.294 | 2120/2595 |
| flat_uneven/2000/water_expand_mix/skai.thunderhead/r12500 | seat1 | eliminated | 14.1 | 0.000 | 0.760 | 1730/2385 |
| flat_uneven/2000/skai.thunderhead/water_leviathan_mix/r12500 | seat1 | eliminated | 13.1 | 0.000 | 0.716 | 2120/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.thunderhead/r12500 | seat0 | eliminated | 13.2 | 0.466 | 0.000 | 2470/2385 |
| flat_uneven/2000/skai.windrunner/water.grasper/r12500 | seat1 | eliminated | 9.8 | 0.000 | 0.725 | 2025/2500 |
| flat_uneven/2000/water.grasper/skai.windrunner/r12500 | seat0 | eliminated | 17.1 | 0.337 | 0.000 | 2000/2475 |
| flat_uneven/2000/skai.windrunner/water.ink_lobber/r12500 | seat1 | eliminated | 8.0 | 0.000 | 0.702 | 2025/2470 |
| flat_uneven/2000/water.ink_lobber/skai.windrunner/r12500 | seat1 | eliminated | 15.8 | 0.000 | 0.026 | 2090/2475 |
| flat_uneven/2000/skai.windrunner/water.leviathan/r12500 | seat1 | eliminated | 2.9 | 0.000 | 1.000 | 2025/2660 |
| flat_uneven/2000/water.leviathan/skai.windrunner/r12500 | seat0 | eliminated | 4.0 | 0.987 | 0.000 | 1900/2475 |
| flat_uneven/2000/skai.windrunner/water.reef_runner/r12500 | seat1 | eliminated | 12.0 | 0.000 | 0.515 | 2025/2475 |
| flat_uneven/2000/water.reef_runner/skai.windrunner/r12500 | seat1 | eliminated | 11.8 | 0.000 | 0.503 | 2025/2475 |
| flat_uneven/2000/skai.windrunner/water.shellback/r12500 | seat1 | eliminated | 16.7 | 0.000 | 0.932 | 2025/2450 |
| flat_uneven/2000/water.shellback/skai.windrunner/r12500 | seat0 | eliminated | 23.8 | 0.861 | 0.000 | 1925/2475 |
| flat_uneven/2000/skai.windrunner/water.spoutling/r12500 | seat0 | eliminated | 10.6 | 0.698 | 0.000 | 2025/2500 |
| flat_uneven/2000/water.spoutling/skai.windrunner/r12500 | seat1 | eliminated | 7.5 | 0.000 | 0.851 | 2000/2475 |
| flat_uneven/2000/skai.windrunner/skai_pressure_mix/r12500 | seat1 | eliminated | 10.8 | 0.000 | 0.594 | 2025/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.windrunner/r12500 | seat1 | eliminated | 13.4 | 0.000 | 0.434 | 2200/2475 |
| flat_uneven/2000/skai.windrunner/skai_expand_mix/r12500 | seat1 | eliminated | 14.7 | 0.000 | 0.489 | 2025/2600 |
| flat_uneven/2000/skai_expand_mix/skai.windrunner/r12500 | seat1 | eliminated | 12.1 | 0.000 | 0.585 | 1950/2475 |
| flat_uneven/2000/skai.windrunner/skai_air_mix/r12500 | seat1 | eliminated | 7.4 | 0.000 | 0.787 | 2025/2820 |
| flat_uneven/2000/skai_air_mix/skai.windrunner/r12500 | seat1 | eliminated | 16.3 | 0.000 | 0.189 | 1880/2475 |
| flat_uneven/2000/skai.windrunner/water_pressure_mix/r12500 | seat1 | eliminated | 9.6 | 0.000 | 0.751 | 2025/2700 |
| flat_uneven/2000/water_pressure_mix/skai.windrunner/r12500 | seat0 | eliminated | 16.9 | 0.360 | 0.000 | 2025/2475 |
| flat_uneven/2000/skai.windrunner/water_expand_mix/r12500 | seat1 | eliminated | 10.8 | 0.000 | 0.977 | 2025/2595 |
| flat_uneven/2000/water_expand_mix/skai.windrunner/r12500 | seat0 | eliminated | 24.0 | 0.510 | 0.000 | 1730/2475 |
| flat_uneven/2000/skai.windrunner/water_leviathan_mix/r12500 | seat1 | eliminated | 5.0 | 0.000 | 1.000 | 2025/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.windrunner/r12500 | seat0 | eliminated | 9.4 | 0.943 | 0.000 | 2470/2475 |
| flat_uneven/2000/water.grasper/water.ink_lobber/r12500 | seat1 | eliminated | 8.7 | 0.000 | 0.754 | 2000/2470 |
| flat_uneven/2000/water.ink_lobber/water.grasper/r12500 | seat0 | eliminated | 14.0 | 0.367 | 0.000 | 2090/2500 |
| flat_uneven/2000/water.grasper/water.leviathan/r12500 | seat1 | eliminated | 3.3 | 0.000 | 1.000 | 2000/2660 |
| flat_uneven/2000/water.leviathan/water.grasper/r12500 | seat0 | eliminated | 5.1 | 1.000 | 0.000 | 1900/2500 |
| flat_uneven/2000/water.grasper/water.reef_runner/r12500 | seat0 | eliminated | 15.8 | 0.406 | 0.000 | 2000/2475 |
| flat_uneven/2000/water.reef_runner/water.grasper/r12500 | seat1 | eliminated | 10.0 | 0.000 | 0.726 | 2025/2500 |
| flat_uneven/2000/water.grasper/water.shellback/r12500 | seat0 | eliminated | 32.0 | 0.274 | 0.000 | 2000/2450 |
| flat_uneven/2000/water.shellback/water.grasper/r12500 | seat1 | eliminated | 17.2 | 0.000 | 0.732 | 1925/2500 |
| flat_uneven/2000/water.grasper/water.spoutling/r12500 | seat0 | eliminated | 11.1 | 0.730 | 0.000 | 2000/2500 |
| flat_uneven/2000/water.spoutling/water.grasper/r12500 | seat1 | eliminated | 8.7 | 0.000 | 0.864 | 2000/2500 |
| flat_uneven/2000/water.grasper/skai_pressure_mix/r12500 | seat0 | eliminated | 24.1 | 0.182 | 0.000 | 2000/2750 |
| flat_uneven/2000/skai_pressure_mix/water.grasper/r12500 | seat1 | eliminated | 10.8 | 0.000 | 0.697 | 2200/2500 |
| flat_uneven/2000/water.grasper/skai_expand_mix/r12500 | seat1 | eliminated | 47.2 | 0.000 | 0.308 | 2000/2600 |
| flat_uneven/2000/skai_expand_mix/water.grasper/r12500 | seat0 | eliminated | 101.7 | 0.308 | 0.000 | 1950/2500 |
| flat_uneven/2000/water.grasper/skai_air_mix/r12500 | seat1 | eliminated | 10.1 | 0.000 | 0.920 | 2000/2820 |
| flat_uneven/2000/skai_air_mix/water.grasper/r12500 | seat0 | eliminated | 21.7 | 0.920 | 0.000 | 1880/2500 |
| flat_uneven/2000/water.grasper/water_pressure_mix/r12500 | seat1 | eliminated | 15.1 | 0.000 | 0.592 | 2000/2700 |
| flat_uneven/2000/water_pressure_mix/water.grasper/r12500 | seat1 | eliminated | 16.8 | 0.000 | 0.513 | 2025/2500 |
| flat_uneven/2000/water.grasper/water_expand_mix/r12500 | seat1 | eliminated | 17.1 | 0.000 | 0.566 | 2000/2595 |
| flat_uneven/2000/water_expand_mix/water.grasper/r12500 | seat1 | eliminated | 13.3 | 0.000 | 0.611 | 1730/2500 |
| flat_uneven/2000/water.grasper/water_leviathan_mix/r12500 | seat1 | eliminated | 7.8 | 0.000 | 0.857 | 2000/2470 |
| flat_uneven/2000/water_leviathan_mix/water.grasper/r12500 | seat0 | eliminated | 10.5 | 0.775 | 0.000 | 2470/2500 |
| flat_uneven/2000/water.ink_lobber/water.leviathan/r12500 | seat1 | eliminated | 4.8 | 0.000 | 0.952 | 2090/2660 |
| flat_uneven/2000/water.leviathan/water.ink_lobber/r12500 | seat0 | eliminated | 5.2 | 0.887 | 0.000 | 1900/2470 |
| flat_uneven/2000/water.ink_lobber/water.reef_runner/r12500 | seat0 | eliminated | 13.6 | 0.318 | 0.000 | 2090/2475 |
| flat_uneven/2000/water.reef_runner/water.ink_lobber/r12500 | seat1 | eliminated | 8.0 | 0.000 | 0.717 | 2025/2470 |
| flat_uneven/2000/water.ink_lobber/water.shellback/r12500 | seat1 | eliminated | 16.0 | 0.000 | 0.853 | 2090/2450 |
| flat_uneven/2000/water.shellback/water.ink_lobber/r12500 | seat0 | eliminated | 19.8 | 0.807 | 0.000 | 1925/2470 |
| flat_uneven/2000/water.ink_lobber/water.spoutling/r12500 | seat0 | eliminated | 10.9 | 0.736 | 0.000 | 2090/2500 |
| flat_uneven/2000/water.spoutling/water.ink_lobber/r12500 | seat1 | eliminated | 5.4 | 0.000 | 0.919 | 2000/2470 |
| flat_uneven/2000/water.ink_lobber/skai_pressure_mix/r12500 | seat1 | eliminated | 10.5 | 0.000 | 0.283 | 2090/2750 |
| flat_uneven/2000/skai_pressure_mix/water.ink_lobber/r12500 | seat1 | eliminated | 11.0 | 0.000 | 0.595 | 2200/2470 |
| flat_uneven/2000/water.ink_lobber/skai_expand_mix/r12500 | seat1 | eliminated | 11.8 | 0.000 | 0.381 | 2090/2600 |
| flat_uneven/2000/skai_expand_mix/water.ink_lobber/r12500 | seat0 | eliminated | 38.3 | 0.308 | 0.000 | 1950/2470 |
| flat_uneven/2000/water.ink_lobber/skai_air_mix/r12500 | seat1 | eliminated | 7.0 | 0.000 | 0.920 | 2090/2820 |
| flat_uneven/2000/skai_air_mix/water.ink_lobber/r12500 | seat0 | eliminated | 12.1 | 0.920 | 0.000 | 1880/2470 |
| flat_uneven/2000/water.ink_lobber/water_pressure_mix/r12500 | seat1 | eliminated | 12.5 | 0.000 | 0.200 | 2090/2700 |
| flat_uneven/2000/water_pressure_mix/water.ink_lobber/r12500 | seat1 | eliminated | 8.6 | 0.000 | 0.751 | 2025/2470 |
| flat_uneven/2000/water.ink_lobber/water_expand_mix/r12500 | seat1 | eliminated | 13.5 | 0.000 | 0.779 | 2090/2595 |
| flat_uneven/2000/water_expand_mix/water.ink_lobber/r12500 | seat1 | eliminated | 17.3 | 0.000 | 0.348 | 1730/2470 |
| flat_uneven/2000/water.ink_lobber/water_leviathan_mix/r12500 | seat1 | eliminated | 7.4 | 0.000 | 0.899 | 2090/2470 |
| flat_uneven/2000/water_leviathan_mix/water.ink_lobber/r12500 | seat0 | eliminated | 8.6 | 0.938 | 0.000 | 2470/2470 |
| flat_uneven/2000/water.leviathan/water.reef_runner/r12500 | seat0 | eliminated | 4.0 | 0.992 | 0.000 | 1900/2475 |
| flat_uneven/2000/water.reef_runner/water.leviathan/r12500 | seat1 | eliminated | 2.9 | 0.000 | 1.000 | 2025/2660 |
| flat_uneven/2000/water.leviathan/water.shellback/r12500 | seat1 | eliminated | 25.0 | 0.000 | 0.766 | 1900/2450 |
| flat_uneven/2000/water.shellback/water.leviathan/r12500 | seat1 | eliminated | 14.0 | 0.000 | 0.787 | 1925/2660 |
| flat_uneven/2000/water.leviathan/water.spoutling/r12500 | seat0 | eliminated | 4.2 | 0.994 | 0.000 | 1900/2500 |
| flat_uneven/2000/water.spoutling/water.leviathan/r12500 | seat1 | eliminated | 2.4 | 0.000 | 1.000 | 2000/2660 |
| flat_uneven/2000/water.leviathan/skai_pressure_mix/r12500 | seat0 | eliminated | 5.6 | 0.945 | 0.000 | 1900/2750 |
| flat_uneven/2000/skai_pressure_mix/water.leviathan/r12500 | seat1 | eliminated | 4.1 | 0.000 | 0.982 | 2200/2660 |
| flat_uneven/2000/water.leviathan/skai_expand_mix/r12500 | seat0 | eliminated | 4.5 | 0.872 | 0.000 | 1900/2600 |
| flat_uneven/2000/skai_expand_mix/water.leviathan/r12500 | seat1 | eliminated | 4.0 | 0.000 | 0.956 | 1950/2660 |
| flat_uneven/2000/water.leviathan/skai_air_mix/r12500 | seat0 | eliminated | 4.3 | 0.755 | 0.000 | 1900/2820 |
| flat_uneven/2000/skai_air_mix/water.leviathan/r12500 | seat1 | eliminated | 4.0 | 0.000 | 0.947 | 1880/2660 |
| flat_uneven/2000/water.leviathan/water_pressure_mix/r12500 | seat0 | eliminated | 5.5 | 1.000 | 0.000 | 1900/2700 |
| flat_uneven/2000/water_pressure_mix/water.leviathan/r12500 | seat1 | eliminated | 3.2 | 0.000 | 1.000 | 2025/2660 |
| flat_uneven/2000/water.leviathan/water_expand_mix/r12500 | seat0 | eliminated | 16.8 | 0.526 | 0.000 | 1900/2595 |
| flat_uneven/2000/water_expand_mix/water.leviathan/r12500 | seat1 | eliminated | 7.5 | 0.000 | 0.927 | 1730/2660 |
| flat_uneven/2000/water.leviathan/water_leviathan_mix/r12500 | seat1 | eliminated | 15.0 | 0.000 | 0.446 | 1900/2470 |
| flat_uneven/2000/water_leviathan_mix/water.leviathan/r12500 | seat1 | eliminated | 14.9 | 0.000 | 0.250 | 2470/2660 |
| flat_uneven/2000/water.reef_runner/water.shellback/r12500 | seat1 | eliminated | 17.1 | 0.000 | 0.949 | 2025/2450 |
| flat_uneven/2000/water.shellback/water.reef_runner/r12500 | seat0 | eliminated | 24.4 | 0.835 | 0.000 | 1925/2475 |
| flat_uneven/2000/water.reef_runner/water.spoutling/r12500 | seat0 | eliminated | 11.5 | 0.676 | 0.000 | 2025/2500 |
| flat_uneven/2000/water.spoutling/water.reef_runner/r12500 | seat1 | eliminated | 7.9 | 0.000 | 0.846 | 2000/2475 |
| flat_uneven/2000/water.reef_runner/skai_pressure_mix/r12500 | seat1 | eliminated | 10.7 | 0.000 | 0.607 | 2025/2750 |
| flat_uneven/2000/skai_pressure_mix/water.reef_runner/r12500 | seat1 | eliminated | 14.2 | 0.000 | 0.421 | 2200/2475 |
| flat_uneven/2000/water.reef_runner/skai_expand_mix/r12500 | seat1 | eliminated | 17.1 | 0.000 | 0.400 | 2025/2600 |
| flat_uneven/2000/skai_expand_mix/water.reef_runner/r12500 | seat0 | eliminated | 57.8 | 0.308 | 0.000 | 1950/2475 |
| flat_uneven/2000/water.reef_runner/skai_air_mix/r12500 | seat1 | eliminated | 9.0 | 0.000 | 0.920 | 2025/2820 |
| flat_uneven/2000/skai_air_mix/water.reef_runner/r12500 | seat0 | eliminated | 12.1 | 0.920 | 0.000 | 1880/2475 |
| flat_uneven/2000/water.reef_runner/water_pressure_mix/r12500 | seat1 | eliminated | 9.8 | 0.000 | 0.757 | 2025/2700 |
| flat_uneven/2000/water_pressure_mix/water.reef_runner/r12500 | seat0 | eliminated | 17.7 | 0.346 | 0.000 | 2025/2475 |
| flat_uneven/2000/water.reef_runner/water_expand_mix/r12500 | seat1 | eliminated | 11.3 | 0.000 | 0.946 | 2025/2595 |
| flat_uneven/2000/water_expand_mix/water.reef_runner/r12500 | seat0 | eliminated | 23.4 | 0.467 | 0.000 | 1730/2475 |
| flat_uneven/2000/water.reef_runner/water_leviathan_mix/r12500 | seat1 | eliminated | 7.3 | 0.000 | 1.000 | 2025/2470 |
| flat_uneven/2000/water_leviathan_mix/water.reef_runner/r12500 | seat0 | eliminated | 8.7 | 1.000 | 0.000 | 2470/2475 |
| flat_uneven/2000/water.shellback/water.spoutling/r12500 | seat0 | eliminated | 18.6 | 0.988 | 0.000 | 1925/2500 |
| flat_uneven/2000/water.spoutling/water.shellback/r12500 | seat1 | eliminated | 14.5 | 0.000 | 0.995 | 2000/2450 |
| flat_uneven/2000/water.shellback/skai_pressure_mix/r12500 | seat0 | eliminated | 26.9 | 0.818 | 0.000 | 1925/2750 |
| flat_uneven/2000/skai_pressure_mix/water.shellback/r12500 | seat1 | eliminated | 18.4 | 0.000 | 0.927 | 2200/2450 |
| flat_uneven/2000/water.shellback/skai_expand_mix/r12500 | stalemate | dome_absorbed | 50.3 | 0.822 | 0.308 | 1925/2600 |
| flat_uneven/2000/skai_expand_mix/water.shellback/r12500 | stalemate | dome_absorbed | 45.0 | 0.308 | 0.958 | 1950/2450 |
| flat_uneven/2000/water.shellback/skai_air_mix/r12500 | seat1 | eliminated | 65.0 | 0.000 | 0.920 | 1925/2820 |
| flat_uneven/2000/skai_air_mix/water.shellback/r12500 | stalemate | dome_absorbed | 53.4 | 0.920 | 0.929 | 1880/2450 |
| flat_uneven/2000/water.shellback/water_pressure_mix/r12500 | seat1 | eliminated | 17.3 | 0.000 | 0.751 | 1925/2700 |
| flat_uneven/2000/water_pressure_mix/water.shellback/r12500 | seat1 | eliminated | 41.5 | 0.000 | 0.227 | 2025/2450 |
| flat_uneven/2000/water.shellback/water_expand_mix/r12500 | seat1 | eliminated | 41.2 | 0.000 | 0.890 | 1925/2595 |
| flat_uneven/2000/water_expand_mix/water.shellback/r12500 | seat1 | eliminated | 34.6 | 0.000 | 0.879 | 1730/2450 |
| flat_uneven/2000/water.shellback/water_leviathan_mix/r12500 | seat0 | eliminated | 45.4 | 0.664 | 0.000 | 1925/2470 |
| flat_uneven/2000/water_leviathan_mix/water.shellback/r12500 | seat1 | eliminated | 34.9 | 0.000 | 0.864 | 2470/2450 |
| flat_uneven/2000/water.spoutling/skai_pressure_mix/r12500 | seat1 | eliminated | 7.5 | 0.000 | 0.873 | 2000/2750 |
| flat_uneven/2000/skai_pressure_mix/water.spoutling/r12500 | seat0 | eliminated | 10.5 | 0.716 | 0.000 | 2200/2500 |
| flat_uneven/2000/water.spoutling/skai_expand_mix/r12500 | seat1 | eliminated | 8.5 | 0.000 | 0.844 | 2000/2600 |
| flat_uneven/2000/skai_expand_mix/water.spoutling/r12500 | seat0 | eliminated | 13.8 | 0.622 | 0.000 | 1950/2500 |
| flat_uneven/2000/water.spoutling/skai_air_mix/r12500 | seat1 | eliminated | 6.4 | 0.000 | 0.896 | 2000/2820 |
| flat_uneven/2000/skai_air_mix/water.spoutling/r12500 | seat0 | eliminated | 14.3 | 0.591 | 0.000 | 1880/2500 |
| flat_uneven/2000/water.spoutling/water_pressure_mix/r12500 | seat1 | eliminated | 8.3 | 0.000 | 0.882 | 2000/2700 |
| flat_uneven/2000/water_pressure_mix/water.spoutling/r12500 | seat0 | eliminated | 10.9 | 0.737 | 0.000 | 2025/2500 |
| flat_uneven/2000/water.spoutling/water_expand_mix/r12500 | seat1 | eliminated | 10.1 | 0.000 | 0.984 | 2000/2595 |
| flat_uneven/2000/water_expand_mix/water.spoutling/r12500 | seat0 | eliminated | 15.7 | 0.869 | 0.000 | 1730/2500 |
| flat_uneven/2000/water.spoutling/water_leviathan_mix/r12500 | seat1 | eliminated | 5.1 | 0.000 | 1.000 | 2000/2470 |
| flat_uneven/2000/water_leviathan_mix/water.spoutling/r12500 | seat0 | eliminated | 5.9 | 1.000 | 0.000 | 2470/2500 |
| flat_uneven/2000/skai_pressure_mix/skai_expand_mix/r12500 | seat1 | eliminated | 16.6 | 0.000 | 0.433 | 2200/2600 |
| flat_uneven/2000/skai_expand_mix/skai_pressure_mix/r12500 | seat1 | eliminated | 11.3 | 0.000 | 0.635 | 1950/2750 |
| flat_uneven/2000/skai_pressure_mix/skai_air_mix/r12500 | seat1 | eliminated | 9.4 | 0.000 | 0.738 | 2200/2820 |
| flat_uneven/2000/skai_air_mix/skai_pressure_mix/r12500 | seat1 | eliminated | 12.4 | 0.000 | 0.426 | 1880/2750 |
| flat_uneven/2000/skai_pressure_mix/water_pressure_mix/r12500 | seat1 | eliminated | 10.5 | 0.000 | 0.727 | 2200/2700 |
| flat_uneven/2000/water_pressure_mix/skai_pressure_mix/r12500 | seat0 | eliminated | 23.6 | 0.191 | 0.000 | 2025/2750 |
| flat_uneven/2000/skai_pressure_mix/water_expand_mix/r12500 | seat1 | eliminated | 12.8 | 0.000 | 0.958 | 2200/2595 |
| flat_uneven/2000/water_expand_mix/skai_pressure_mix/r12500 | seat1 | eliminated | 23.0 | 0.000 | 0.213 | 1730/2750 |
| flat_uneven/2000/skai_pressure_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 8.2 | 0.000 | 0.982 | 2200/2470 |
| flat_uneven/2000/water_leviathan_mix/skai_pressure_mix/r12500 | seat0 | eliminated | 11.0 | 0.915 | 0.000 | 2470/2750 |
| flat_uneven/2000/skai_expand_mix/skai_air_mix/r12500 | seat1 | eliminated | 9.9 | 0.000 | 0.743 | 1950/2820 |
| flat_uneven/2000/skai_air_mix/skai_expand_mix/r12500 | seat1 | eliminated | 16.3 | 0.000 | 0.272 | 1880/2600 |
| flat_uneven/2000/skai_expand_mix/water_pressure_mix/r12500 | seat0 | eliminated | 102.3 | 0.308 | 0.000 | 1950/2700 |
| flat_uneven/2000/water_pressure_mix/skai_expand_mix/r12500 | seat1 | eliminated | 44.9 | 0.000 | 0.308 | 2025/2600 |
| flat_uneven/2000/skai_expand_mix/water_expand_mix/r12500 | seat1 | eliminated | 13.6 | 0.000 | 0.963 | 1950/2595 |
| flat_uneven/2000/water_expand_mix/skai_expand_mix/r12500 | seat0 | eliminated | 28.1 | 0.541 | 0.000 | 1730/2600 |
| flat_uneven/2000/skai_expand_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 8.4 | 0.000 | 0.983 | 1950/2470 |
| flat_uneven/2000/water_leviathan_mix/skai_expand_mix/r12500 | seat0 | eliminated | 10.4 | 0.919 | 0.000 | 2470/2600 |
| flat_uneven/2000/skai_air_mix/water_pressure_mix/r12500 | seat0 | eliminated | 21.6 | 0.920 | 0.000 | 1880/2700 |
| flat_uneven/2000/water_pressure_mix/skai_air_mix/r12500 | seat1 | eliminated | 10.1 | 0.000 | 0.920 | 2025/2820 |
| flat_uneven/2000/skai_air_mix/water_expand_mix/r12500 | seat1 | eliminated | 19.3 | 0.000 | 0.737 | 1880/2595 |
| flat_uneven/2000/water_expand_mix/skai_air_mix/r12500 | seat1 | eliminated | 16.8 | 0.000 | 0.670 | 1730/2820 |
| flat_uneven/2000/skai_air_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 10.7 | 0.000 | 0.899 | 1880/2470 |
| flat_uneven/2000/water_leviathan_mix/skai_air_mix/r12500 | seat0 | eliminated | 13.5 | 0.491 | 0.000 | 2470/2820 |
| flat_uneven/2000/water_pressure_mix/water_expand_mix/r12500 | seat1 | eliminated | 17.7 | 0.000 | 0.701 | 2025/2595 |
| flat_uneven/2000/water_expand_mix/water_pressure_mix/r12500 | seat1 | eliminated | 13.1 | 0.000 | 0.560 | 1730/2700 |
| flat_uneven/2000/water_pressure_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 7.5 | 0.000 | 0.890 | 2025/2470 |
| flat_uneven/2000/water_leviathan_mix/water_pressure_mix/r12500 | seat0 | eliminated | 10.5 | 0.821 | 0.000 | 2470/2700 |
| flat_uneven/2000/water_expand_mix/water_leviathan_mix/r12500 | seat1 | eliminated | 13.9 | 0.000 | 0.941 | 1730/2470 |
| flat_uneven/2000/water_leviathan_mix/water_expand_mix/r12500 | seat0 | eliminated | 19.9 | 0.691 | 0.000 | 2470/2595 |
| flat_uneven/2000/skai.roc_rider/skai.stormcaller/r15000 | seat0 | eliminated | 16.2 | 1.000 | 0.000 | 2000/3040 |
| flat_uneven/2000/skai.stormcaller/skai.roc_rider/r15000 | seat1 | eliminated | 8.4 | 0.000 | 1.000 | 2090/3000 |
| flat_uneven/2000/skai.roc_rider/skai.talon_archer/r15000 | seat1 | eliminated | 11.8 | 0.000 | 0.704 | 2000/3000 |
| flat_uneven/2000/skai.talon_archer/skai.roc_rider/r15000 | seat1 | eliminated | 15.4 | 0.000 | 0.623 | 2000/3000 |
| flat_uneven/2000/skai.roc_rider/skai.thunderhead/r15000 | seat0 | eliminated | 26.5 | 1.000 | 0.000 | 2000/2915 |
| flat_uneven/2000/skai.thunderhead/skai.roc_rider/r15000 | seat1 | eliminated | 13.4 | 0.000 | 1.000 | 2120/3000 |
| flat_uneven/2000/skai.roc_rider/skai.windrunner/r15000 | seat1 | eliminated | 10.2 | 0.000 | 0.749 | 2000/3000 |
| flat_uneven/2000/skai.windrunner/skai.roc_rider/r15000 | seat1 | eliminated | 15.7 | 0.000 | 0.521 | 2025/3000 |
| flat_uneven/2000/skai.roc_rider/water.grasper/r15000 | seat0 | eliminated | 44.9 | 1.000 | 0.000 | 2000/3000 |
| flat_uneven/2000/water.grasper/skai.roc_rider/r15000 | seat1 | eliminated | 20.6 | 0.000 | 1.000 | 2000/3000 |
| flat_uneven/2000/skai.roc_rider/water.ink_lobber/r15000 | seat0 | eliminated | 17.4 | 1.000 | 0.000 | 2000/3040 |
| flat_uneven/2000/water.ink_lobber/skai.roc_rider/r15000 | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 2090/3000 |
| flat_uneven/2000/skai.roc_rider/water.leviathan/r15000 | seat1 | eliminated | 4.2 | 0.000 | 0.974 | 2000/3040 |
| flat_uneven/2000/water.leviathan/skai.roc_rider/r15000 | seat0 | eliminated | 6.9 | 0.848 | 0.000 | 1900/3000 |
| flat_uneven/2000/skai.roc_rider/water.reef_runner/r15000 | seat0 | eliminated | 28.3 | 1.000 | 0.000 | 2000/3000 |
| flat_uneven/2000/water.reef_runner/skai.roc_rider/r15000 | seat1 | eliminated | 13.9 | 0.000 | 1.000 | 2025/3000 |
| flat_uneven/2000/skai.roc_rider/water.shellback/r15000 | stalemate | dome_absorbed | 36.5 | 1.000 | 0.989 | 2000/2975 |
| flat_uneven/2000/water.shellback/skai.roc_rider/r15000 | seat1 | eliminated | 68.1 | 0.000 | 1.000 | 1925/3000 |
| flat_uneven/2000/skai.roc_rider/water.spoutling/r15000 | seat0 | eliminated | 23.2 | 0.244 | 0.000 | 2000/3000 |
| flat_uneven/2000/water.spoutling/skai.roc_rider/r15000 | seat1 | eliminated | 8.7 | 0.000 | 0.862 | 2000/3000 |
| flat_uneven/2000/skai.roc_rider/skai_pressure_mix/r15000 | seat1 | eliminated | 11.4 | 0.000 | 0.705 | 2000/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.roc_rider/r15000 | seat1 | eliminated | 18.2 | 0.000 | 0.465 | 2200/3000 |
| flat_uneven/2000/skai.roc_rider/skai_expand_mix/r15000 | seat1 | eliminated | 11.2 | 0.000 | 0.763 | 2000/3250 |
| flat_uneven/2000/skai_expand_mix/skai.roc_rider/r15000 | seat1 | eliminated | 15.2 | 0.000 | 0.661 | 1950/3000 |
| flat_uneven/2000/skai.roc_rider/skai_air_mix/r15000 | seat1 | eliminated | 24.5 | 0.000 | 0.455 | 2000/2820 |
| flat_uneven/2000/skai_air_mix/skai.roc_rider/r15000 | seat1 | eliminated | 13.8 | 0.000 | 0.789 | 1880/3000 |
| flat_uneven/2000/skai.roc_rider/water_pressure_mix/r15000 | seat0 | eliminated | 38.7 | 1.000 | 0.000 | 2000/2700 |
| flat_uneven/2000/water_pressure_mix/skai.roc_rider/r15000 | seat1 | eliminated | 19.7 | 0.000 | 1.000 | 2025/3000 |
| flat_uneven/2000/skai.roc_rider/water_expand_mix/r15000 | seat1 | eliminated | 22.5 | 0.000 | 0.858 | 2000/2595 |
| flat_uneven/2000/water_expand_mix/skai.roc_rider/r15000 | seat1 | eliminated | 23.1 | 0.000 | 0.637 | 1730/3000 |
| flat_uneven/2000/skai.roc_rider/water_leviathan_mix/r15000 | seat1 | eliminated | 10.7 | 0.000 | 0.888 | 2000/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.roc_rider/r15000 | seat0 | eliminated | 14.0 | 0.787 | 0.000 | 2470/3000 |
| flat_uneven/2000/skai.stormcaller/skai.talon_archer/r15000 | seat1 | eliminated | 7.8 | 0.000 | 0.428 | 2090/3000 |
| flat_uneven/2000/skai.talon_archer/skai.stormcaller/r15000 | seat1 | eliminated | 7.2 | 0.000 | 0.720 | 2000/3040 |
| flat_uneven/2000/skai.stormcaller/skai.thunderhead/r15000 | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 2090/2915 |
| flat_uneven/2000/skai.thunderhead/skai.stormcaller/r15000 | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 2120/3040 |
| flat_uneven/2000/skai.stormcaller/skai.windrunner/r15000 | seat1 | eliminated | 7.2 | 0.000 | 0.421 | 2090/3000 |
| flat_uneven/2000/skai.windrunner/skai.stormcaller/r15000 | seat1 | eliminated | 6.9 | 0.000 | 0.729 | 2025/3040 |
| flat_uneven/2000/skai.stormcaller/water.grasper/r15000 | seat1 | eliminated | 10.4 | 0.000 | 0.330 | 2090/3000 |
| flat_uneven/2000/water.grasper/skai.stormcaller/r15000 | seat1 | eliminated | 9.1 | 0.000 | 0.775 | 2000/3040 |
| flat_uneven/2000/skai.stormcaller/water.ink_lobber/r15000 | seat1 | eliminated | 6.3 | 0.000 | 0.504 | 2090/3040 |
| flat_uneven/2000/water.ink_lobber/skai.stormcaller/r15000 | seat1 | eliminated | 8.5 | 0.000 | 0.256 | 2090/3040 |
| flat_uneven/2000/skai.stormcaller/water.leviathan/r15000 | seat1 | eliminated | 3.9 | 0.000 | 0.969 | 2090/3040 |
| flat_uneven/2000/water.leviathan/skai.stormcaller/r15000 | seat0 | eliminated | 4.5 | 0.847 | 0.000 | 1900/3040 |
| flat_uneven/2000/skai.stormcaller/water.reef_runner/r15000 | seat1 | eliminated | 7.6 | 0.000 | 0.417 | 2090/3000 |
| flat_uneven/2000/water.reef_runner/skai.stormcaller/r15000 | seat1 | eliminated | 6.7 | 0.000 | 0.817 | 2025/3040 |
| flat_uneven/2000/skai.stormcaller/water.shellback/r15000 | seat1 | eliminated | 13.4 | 0.000 | 0.977 | 2090/2975 |
| flat_uneven/2000/water.shellback/skai.stormcaller/r15000 | seat0 | eliminated | 21.0 | 0.816 | 0.000 | 1925/3040 |
| flat_uneven/2000/skai.stormcaller/water.spoutling/r15000 | seat0 | eliminated | 12.0 | 0.596 | 0.000 | 2090/3000 |
| flat_uneven/2000/water.spoutling/skai.stormcaller/r15000 | seat1 | eliminated | 4.8 | 0.000 | 0.938 | 2000/3040 |
| flat_uneven/2000/skai.stormcaller/skai_pressure_mix/r15000 | seat1 | eliminated | 9.0 | 0.000 | 0.427 | 2090/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.stormcaller/r15000 | seat1 | eliminated | 9.0 | 0.000 | 0.728 | 2200/3040 |
| flat_uneven/2000/skai.stormcaller/skai_expand_mix/r15000 | seat1 | eliminated | 7.9 | 0.000 | 0.653 | 2090/3250 |
| flat_uneven/2000/skai_expand_mix/skai.stormcaller/r15000 | seat0 | eliminated | 40.3 | 0.308 | 0.000 | 1950/3040 |
| flat_uneven/2000/skai.stormcaller/skai_air_mix/r15000 | seat1 | eliminated | 5.5 | 0.000 | 0.920 | 2090/2820 |
| flat_uneven/2000/skai_air_mix/skai.stormcaller/r15000 | seat0 | eliminated | 11.7 | 0.920 | 0.000 | 1880/3040 |
| flat_uneven/2000/skai.stormcaller/water_pressure_mix/r15000 | seat1 | eliminated | 10.6 | 0.000 | 0.329 | 2090/2700 |
| flat_uneven/2000/water_pressure_mix/skai.stormcaller/r15000 | seat1 | eliminated | 9.4 | 0.000 | 0.783 | 2025/3040 |
| flat_uneven/2000/skai.stormcaller/water_expand_mix/r15000 | seat1 | eliminated | 11.8 | 0.000 | 0.822 | 2090/2595 |
| flat_uneven/2000/water_expand_mix/skai.stormcaller/r15000 | seat1 | eliminated | 14.7 | 0.000 | 0.499 | 1730/3040 |
| flat_uneven/2000/skai.stormcaller/water_leviathan_mix/r15000 | seat1 | eliminated | 7.6 | 0.000 | 0.924 | 2090/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.stormcaller/r15000 | seat0 | eliminated | 8.3 | 0.956 | 0.000 | 2470/3040 |
| flat_uneven/2000/skai.talon_archer/skai.thunderhead/r15000 | seat1 | eliminated | 6.5 | 0.000 | 0.850 | 2000/2915 |
| flat_uneven/2000/skai.thunderhead/skai.talon_archer/r15000 | seat0 | eliminated | 12.0 | 0.418 | 0.000 | 2120/3000 |
| flat_uneven/2000/skai.talon_archer/skai.windrunner/r15000 | seat1 | eliminated | 10.0 | 0.000 | 0.713 | 2000/3000 |
| flat_uneven/2000/skai.windrunner/skai.talon_archer/r15000 | seat1 | eliminated | 11.0 | 0.000 | 0.664 | 2025/3000 |
| flat_uneven/2000/skai.talon_archer/water.grasper/r15000 | seat1 | eliminated | 10.5 | 0.000 | 0.786 | 2000/3000 |
| flat_uneven/2000/water.grasper/skai.talon_archer/r15000 | seat1 | eliminated | 19.3 | 0.000 | 0.440 | 2000/3000 |
| flat_uneven/2000/skai.talon_archer/water.ink_lobber/r15000 | seat1 | eliminated | 6.5 | 0.000 | 0.785 | 2000/3040 |
| flat_uneven/2000/water.ink_lobber/skai.talon_archer/r15000 | seat1 | eliminated | 10.0 | 0.000 | 0.293 | 2090/3000 |
| flat_uneven/2000/skai.talon_archer/water.leviathan/r15000 | seat1 | eliminated | 4.1 | 0.000 | 0.985 | 2000/3040 |
| flat_uneven/2000/water.leviathan/skai.talon_archer/r15000 | seat0 | eliminated | 5.4 | 0.892 | 0.000 | 1900/3000 |
| flat_uneven/2000/skai.talon_archer/water.reef_runner/r15000 | seat1 | eliminated | 10.9 | 0.000 | 0.675 | 2000/3000 |
| flat_uneven/2000/water.reef_runner/skai.talon_archer/r15000 | seat1 | eliminated | 11.0 | 0.000 | 0.696 | 2025/3000 |
| flat_uneven/2000/skai.talon_archer/water.shellback/r15000 | seat1 | eliminated | 17.1 | 0.000 | 0.945 | 2000/2975 |
| flat_uneven/2000/water.shellback/skai.talon_archer/r15000 | seat0 | eliminated | 32.6 | 0.840 | 0.000 | 1925/3000 |
| flat_uneven/2000/skai.talon_archer/water.spoutling/r15000 | seat0 | eliminated | 17.2 | 0.542 | 0.000 | 2000/3000 |
| flat_uneven/2000/water.spoutling/skai.talon_archer/r15000 | seat1 | eliminated | 7.3 | 0.000 | 0.922 | 2000/3000 |
| flat_uneven/2000/skai.talon_archer/skai_pressure_mix/r15000 | seat1 | eliminated | 11.5 | 0.000 | 0.647 | 2000/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.talon_archer/r15000 | seat1 | eliminated | 12.8 | 0.000 | 0.645 | 2200/3000 |
| flat_uneven/2000/skai.talon_archer/skai_expand_mix/r15000 | seat1 | eliminated | 11.7 | 0.000 | 0.719 | 2000/3250 |
| flat_uneven/2000/skai_expand_mix/skai.talon_archer/r15000 | seat1 | eliminated | 10.9 | 0.000 | 0.729 | 1950/3000 |
| flat_uneven/2000/skai.talon_archer/skai_air_mix/r15000 | seat1 | eliminated | 9.4 | 0.000 | 0.729 | 2000/2820 |
| flat_uneven/2000/skai_air_mix/skai.talon_archer/r15000 | seat1 | eliminated | 11.3 | 0.000 | 0.462 | 1880/3000 |
| flat_uneven/2000/skai.talon_archer/water_pressure_mix/r15000 | seat1 | eliminated | 11.2 | 0.000 | 0.758 | 2000/2700 |
| flat_uneven/2000/water_pressure_mix/skai.talon_archer/r15000 | seat1 | eliminated | 20.1 | 0.000 | 0.396 | 2025/3000 |
| flat_uneven/2000/skai.talon_archer/water_expand_mix/r15000 | seat1 | eliminated | 13.7 | 0.000 | 0.910 | 2000/2595 |
| flat_uneven/2000/water_expand_mix/skai.talon_archer/r15000 | seat1 | eliminated | 18.8 | 0.000 | 0.469 | 1730/3000 |
| flat_uneven/2000/skai.talon_archer/water_leviathan_mix/r15000 | seat1 | eliminated | 7.8 | 0.000 | 0.986 | 2000/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.talon_archer/r15000 | seat0 | eliminated | 10.5 | 0.836 | 0.000 | 2470/3000 |
| flat_uneven/2000/skai.thunderhead/skai.windrunner/r15000 | seat0 | eliminated | 11.5 | 0.422 | 0.000 | 2120/3000 |
| flat_uneven/2000/skai.windrunner/skai.thunderhead/r15000 | seat1 | eliminated | 4.3 | 0.000 | 0.908 | 2025/2915 |
| flat_uneven/2000/skai.thunderhead/water.grasper/r15000 | seat0 | eliminated | 9.3 | 1.000 | 0.000 | 2120/3000 |
| flat_uneven/2000/water.grasper/skai.thunderhead/r15000 | seat1 | eliminated | 6.4 | 0.000 | 1.000 | 2000/2915 |
| flat_uneven/2000/skai.thunderhead/water.ink_lobber/r15000 | seat0 | eliminated | 6.7 | 1.000 | 0.000 | 2120/3040 |
| flat_uneven/2000/water.ink_lobber/skai.thunderhead/r15000 | seat1 | eliminated | 4.4 | 0.000 | 1.000 | 2090/2915 |
| flat_uneven/2000/skai.thunderhead/water.leviathan/r15000 | seat1 | eliminated | 4.1 | 0.000 | 0.864 | 2120/3040 |
| flat_uneven/2000/water.leviathan/skai.thunderhead/r15000 | seat0 | eliminated | 13.2 | 0.199 | 0.000 | 1900/2915 |
| flat_uneven/2000/skai.thunderhead/water.reef_runner/r15000 | seat0 | eliminated | 7.3 | 1.000 | 0.000 | 2120/3000 |
| flat_uneven/2000/water.reef_runner/skai.thunderhead/r15000 | seat1 | eliminated | 4.2 | 0.000 | 1.000 | 2025/2915 |
| flat_uneven/2000/skai.thunderhead/water.shellback/r15000 | stalemate | dome_absorbed | 36.9 | 1.000 | 0.982 | 2120/2975 |
| flat_uneven/2000/water.shellback/skai.thunderhead/r15000 | seat1 | eliminated | 14.4 | 0.000 | 1.000 | 1925/2915 |
| flat_uneven/2000/skai.thunderhead/water.spoutling/r15000 | seat0 | eliminated | 7.0 | 0.793 | 0.000 | 2120/3000 |
| flat_uneven/2000/water.spoutling/skai.thunderhead/r15000 | seat1 | eliminated | 4.3 | 0.000 | 0.935 | 2000/2915 |
| flat_uneven/2000/skai.thunderhead/skai_pressure_mix/r15000 | seat0 | eliminated | 14.5 | 0.465 | 0.000 | 2120/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.thunderhead/r15000 | seat1 | eliminated | 6.3 | 0.000 | 0.877 | 2200/2915 |
| flat_uneven/2000/skai.thunderhead/skai_expand_mix/r15000 | seat1 | eliminated | 21.8 | 0.000 | 0.308 | 2120/3250 |
| flat_uneven/2000/skai_expand_mix/skai.thunderhead/r15000 | seat0 | eliminated | 76.8 | 0.308 | 0.000 | 1950/2915 |
| flat_uneven/2000/skai.thunderhead/skai_air_mix/r15000 | seat1 | eliminated | 22.1 | 0.000 | 0.920 | 2120/2820 |
| flat_uneven/2000/skai_air_mix/skai.thunderhead/r15000 | seat0 | eliminated | 45.6 | 0.920 | 0.000 | 1880/2915 |
| flat_uneven/2000/skai.thunderhead/water_pressure_mix/r15000 | seat0 | eliminated | 9.0 | 1.000 | 0.000 | 2120/2700 |
| flat_uneven/2000/water_pressure_mix/skai.thunderhead/r15000 | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 2025/2915 |
| flat_uneven/2000/skai.thunderhead/water_expand_mix/r15000 | seat1 | eliminated | 24.3 | 0.000 | 0.294 | 2120/2595 |
| flat_uneven/2000/water_expand_mix/skai.thunderhead/r15000 | seat1 | eliminated | 9.6 | 0.000 | 0.858 | 1730/2915 |
| flat_uneven/2000/skai.thunderhead/water_leviathan_mix/r15000 | seat1 | eliminated | 13.1 | 0.000 | 0.716 | 2120/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.thunderhead/r15000 | seat0 | eliminated | 16.1 | 0.178 | 0.000 | 2470/2915 |
| flat_uneven/2000/skai.windrunner/water.grasper/r15000 | seat1 | eliminated | 8.8 | 0.000 | 0.795 | 2025/3000 |
| flat_uneven/2000/water.grasper/skai.windrunner/r15000 | seat1 | eliminated | 18.3 | 0.000 | 0.325 | 2000/3000 |
| flat_uneven/2000/skai.windrunner/water.ink_lobber/r15000 | seat1 | eliminated | 5.2 | 0.000 | 0.880 | 2025/3040 |
| flat_uneven/2000/water.ink_lobber/skai.windrunner/r15000 | seat1 | eliminated | 8.6 | 0.000 | 0.367 | 2090/3000 |
| flat_uneven/2000/skai.windrunner/water.leviathan/r15000 | seat1 | eliminated | 2.2 | 0.000 | 1.000 | 2025/3040 |
| flat_uneven/2000/water.leviathan/skai.windrunner/r15000 | seat0 | eliminated | 4.7 | 0.969 | 0.000 | 1900/3000 |
| flat_uneven/2000/skai.windrunner/water.reef_runner/r15000 | seat1 | eliminated | 9.4 | 0.000 | 0.672 | 2025/3000 |
| flat_uneven/2000/water.reef_runner/skai.windrunner/r15000 | seat1 | eliminated | 9.3 | 0.000 | 0.680 | 2025/3000 |
| flat_uneven/2000/skai.windrunner/water.shellback/r15000 | seat1 | eliminated | 14.9 | 0.000 | 0.973 | 2025/2975 |
| flat_uneven/2000/water.shellback/skai.windrunner/r15000 | seat0 | eliminated | 31.8 | 0.632 | 0.000 | 1925/3000 |
| flat_uneven/2000/skai.windrunner/water.spoutling/r15000 | seat0 | eliminated | 14.1 | 0.540 | 0.000 | 2025/3000 |
| flat_uneven/2000/water.spoutling/skai.windrunner/r15000 | seat1 | eliminated | 6.6 | 0.000 | 0.894 | 2000/3000 |
| flat_uneven/2000/skai.windrunner/skai_pressure_mix/r15000 | seat1 | eliminated | 10.8 | 0.000 | 0.594 | 2025/2750 |
| flat_uneven/2000/skai_pressure_mix/skai.windrunner/r15000 | seat1 | eliminated | 10.6 | 0.000 | 0.640 | 2200/3000 |
| flat_uneven/2000/skai.windrunner/skai_expand_mix/r15000 | seat1 | eliminated | 11.2 | 0.000 | 0.673 | 2025/3250 |
| flat_uneven/2000/skai_expand_mix/skai.windrunner/r15000 | seat1 | eliminated | 9.8 | 0.000 | 0.709 | 1950/3000 |
| flat_uneven/2000/skai.windrunner/skai_air_mix/r15000 | seat1 | eliminated | 7.4 | 0.000 | 0.787 | 2025/2820 |
| flat_uneven/2000/skai_air_mix/skai.windrunner/r15000 | seat1 | eliminated | 10.0 | 0.000 | 0.518 | 1880/3000 |
| flat_uneven/2000/skai.windrunner/water_pressure_mix/r15000 | seat1 | eliminated | 9.6 | 0.000 | 0.751 | 2025/2700 |
| flat_uneven/2000/water_pressure_mix/skai.windrunner/r15000 | seat1 | eliminated | 17.2 | 0.000 | 0.387 | 2025/3000 |
| flat_uneven/2000/skai.windrunner/water_expand_mix/r15000 | seat1 | eliminated | 10.8 | 0.000 | 0.977 | 2025/2595 |
| flat_uneven/2000/water_expand_mix/skai.windrunner/r15000 | seat1 | eliminated | 15.8 | 0.000 | 0.405 | 1730/3000 |
| flat_uneven/2000/skai.windrunner/water_leviathan_mix/r15000 | seat1 | eliminated | 5.0 | 0.000 | 1.000 | 2025/2470 |
| flat_uneven/2000/water_leviathan_mix/skai.windrunner/r15000 | seat0 | eliminated | 10.7 | 0.880 | 0.000 | 2470/3000 |
| flat_uneven/2000/water.grasper/water.ink_lobber/r15000 | seat1 | eliminated | 7.8 | 0.000 | 0.885 | 2000/3040 |
| flat_uneven/2000/water.ink_lobber/water.grasper/r15000 | seat1 | eliminated | 13.5 | 0.000 | 0.186 | 2090/3000 |
| flat_uneven/2000/water.grasper/water.leviathan/r15000 | seat1 | eliminated | 3.7 | 0.000 | 1.000 | 2000/3040 |
| flat_uneven/2000/water.leviathan/water.grasper/r15000 | seat0 | eliminated | 5.3 | 1.000 | 0.000 | 1900/3000 |
| flat_uneven/2000/water.grasper/water.reef_runner/r15000 | seat1 | eliminated | 18.5 | 0.000 | 0.320 | 2000/3000 |
| flat_uneven/2000/water.reef_runner/water.grasper/r15000 | seat1 | eliminated | 9.0 | 0.000 | 0.797 | 2025/3000 |
| flat_uneven/2000/water.grasper/water.shellback/r15000 | seat1 | eliminated | 28.5 | 0.000 | 0.397 | 2000/2975 |
| flat_uneven/2000/water.shellback/water.grasper/r15000 | seat1 | eliminated | 15.2 | 0.000 | 0.812 | 1925/3000 |
| flat_uneven/2000/water.grasper/water.spoutling/r15000 | seat0 | eliminated | 13.0 | 0.623 | 0.000 | 2000/3000 |
| flat_uneven/2000/water.spoutling/water.grasper/r15000 | seat1 | eliminated | 7.9 | 0.000 | 0.898 | 2000/3000 |
| flat_uneven/2000/water.grasper/skai_pressure_mix/r15000 | seat0 | eliminated | 24.1 | 0.182 | 0.000 | 2000/2750 |
| flat_uneven/2000/skai_pressure_mix/water.grasper/r15000 | seat1 | eliminated | 9.4 | 0.000 | 0.778 | 2200/3000 |
| flat_uneven/2000/water.grasper/skai_expand_mix/r15000 | seat1 | eliminated | 24.5 | 0.000 | 0.308 | 2000/3250 |
| flat_uneven/2000/skai_expand_mix/water.grasper/r15000 | seat0 | eliminated | 122.7 | 0.308 | 0.000 | 1950/3000 |
| flat_uneven/2000/water.grasper/skai_air_mix/r15000 | seat1 | eliminated | 10.1 | 0.000 | 0.920 | 2000/2820 |
| flat_uneven/2000/skai_air_mix/water.grasper/r15000 | seat0 | eliminated | 24.4 | 0.920 | 0.000 | 1880/3000 |
| flat_uneven/2000/water.grasper/water_pressure_mix/r15000 | seat1 | eliminated | 15.1 | 0.000 | 0.592 | 2000/2700 |
| flat_uneven/2000/water_pressure_mix/water.grasper/r15000 | seat1 | eliminated | 13.2 | 0.000 | 0.674 | 2025/3000 |
| flat_uneven/2000/water.grasper/water_expand_mix/r15000 | seat1 | eliminated | 17.1 | 0.000 | 0.566 | 2000/2595 |
| flat_uneven/2000/water_expand_mix/water.grasper/r15000 | seat1 | eliminated | 11.1 | 0.000 | 0.655 | 1730/3000 |
| flat_uneven/2000/water.grasper/water_leviathan_mix/r15000 | seat1 | eliminated | 7.8 | 0.000 | 0.857 | 2000/2470 |
| flat_uneven/2000/water_leviathan_mix/water.grasper/r15000 | seat0 | eliminated | 11.1 | 0.728 | 0.000 | 2470/3000 |
| flat_uneven/2000/water.ink_lobber/water.leviathan/r15000 | seat1 | eliminated | 4.7 | 0.000 | 0.923 | 2090/3040 |
| flat_uneven/2000/water.leviathan/water.ink_lobber/r15000 | seat0 | eliminated | 6.3 | 0.777 | 0.000 | 1900/3040 |
| flat_uneven/2000/water.ink_lobber/water.reef_runner/r15000 | seat1 | eliminated | 11.0 | 0.000 | 0.212 | 2090/3000 |
| flat_uneven/2000/water.reef_runner/water.ink_lobber/r15000 | seat1 | eliminated | 5.5 | 0.000 | 0.870 | 2025/3040 |
| flat_uneven/2000/water.ink_lobber/water.shellback/r15000 | seat1 | eliminated | 14.5 | 0.000 | 0.976 | 2090/2975 |
| flat_uneven/2000/water.shellback/water.ink_lobber/r15000 | seat0 | eliminated | 24.2 | 0.560 | 0.000 | 1925/3040 |
| flat_uneven/2000/water.ink_lobber/water.spoutling/r15000 | seat0 | eliminated | 10.9 | 0.695 | 0.000 | 2090/3000 |
| flat_uneven/2000/water.spoutling/water.ink_lobber/r15000 | seat1 | eliminated | 5.1 | 0.000 | 0.956 | 2000/3040 |
| flat_uneven/2000/water.ink_lobber/skai_pressure_mix/r15000 | seat1 | eliminated | 10.5 | 0.000 | 0.283 | 2090/2750 |
| flat_uneven/2000/skai_pressure_mix/water.ink_lobber/r15000 | seat1 | eliminated | 7.7 | 0.000 | 0.813 | 2200/3040 |
| flat_uneven/2000/water.ink_lobber/skai_expand_mix/r15000 | seat1 | eliminated | 9.1 | 0.000 | 0.505 | 2090/3250 |
| flat_uneven/2000/skai_expand_mix/water.ink_lobber/r15000 | seat0 | eliminated | 48.3 | 0.308 | 0.000 | 1950/3040 |
| flat_uneven/2000/water.ink_lobber/skai_air_mix/r15000 | seat1 | eliminated | 7.0 | 0.000 | 0.920 | 2090/2820 |
| flat_uneven/2000/skai_air_mix/water.ink_lobber/r15000 | seat0 | eliminated | 12.6 | 0.920 | 0.000 | 1880/3040 |
| flat_uneven/2000/water.ink_lobber/water_pressure_mix/r15000 | seat1 | eliminated | 12.5 | 0.000 | 0.200 | 2090/2700 |
| flat_uneven/2000/water_pressure_mix/water.ink_lobber/r15000 | seat1 | eliminated | 7.8 | 0.000 | 0.914 | 2025/3040 |
| flat_uneven/2000/water.ink_lobber/water_expand_mix/r15000 | seat1 | eliminated | 13.5 | 0.000 | 0.779 | 2090/2595 |
| flat_uneven/2000/water_expand_mix/water.ink_lobber/r15000 | seat1 | eliminated | 12.3 | 0.000 | 0.622 | 1730/3040 |
| flat_uneven/2000/water.ink_lobber/water_leviathan_mix/r15000 | seat1 | eliminated | 7.4 | 0.000 | 0.899 | 2090/2470 |
| flat_uneven/2000/water_leviathan_mix/water.ink_lobber/r15000 | seat0 | eliminated | 8.5 | 0.771 | 0.000 | 2470/3040 |
| flat_uneven/2000/water.leviathan/water.reef_runner/r15000 | seat0 | eliminated | 4.0 | 0.975 | 0.000 | 1900/3000 |
| flat_uneven/2000/water.reef_runner/water.leviathan/r15000 | seat1 | eliminated | 2.3 | 0.000 | 1.000 | 2025/3040 |
| flat_uneven/2000/water.leviathan/water.shellback/r15000 | seat1 | eliminated | 21.5 | 0.000 | 0.797 | 1900/2975 |
| flat_uneven/2000/water.shellback/water.leviathan/r15000 | seat1 | eliminated | 12.0 | 0.000 | 0.843 | 1925/3040 |
| flat_uneven/2000/water.leviathan/water.spoutling/r15000 | seat0 | eliminated | 4.2 | 0.981 | 0.000 | 1900/3000 |
| flat_uneven/2000/water.spoutling/water.leviathan/r15000 | seat1 | eliminated | 2.8 | 0.000 | 0.998 | 2000/3040 |
| flat_uneven/2000/water.leviathan/skai_pressure_mix/r15000 | seat0 | eliminated | 5.6 | 0.945 | 0.000 | 1900/2750 |
| flat_uneven/2000/skai_pressure_mix/water.leviathan/r15000 | seat1 | eliminated | 4.0 | 0.000 | 0.992 | 2200/3040 |
| flat_uneven/2000/water.leviathan/skai_expand_mix/r15000 | seat0 | eliminated | 7.0 | 0.872 | 0.000 | 1900/3250 |
| flat_uneven/2000/skai_expand_mix/water.leviathan/r15000 | seat1 | eliminated | 3.9 | 0.000 | 0.982 | 1950/3040 |
| flat_uneven/2000/water.leviathan/skai_air_mix/r15000 | seat0 | eliminated | 4.3 | 0.755 | 0.000 | 1900/2820 |
| flat_uneven/2000/skai_air_mix/water.leviathan/r15000 | seat1 | eliminated | 4.0 | 0.000 | 0.911 | 1880/3040 |
| flat_uneven/2000/water.leviathan/water_pressure_mix/r15000 | seat0 | eliminated | 5.5 | 1.000 | 0.000 | 1900/2700 |
| flat_uneven/2000/water_pressure_mix/water.leviathan/r15000 | seat1 | eliminated | 3.2 | 0.000 | 1.000 | 2025/3040 |
| flat_uneven/2000/water.leviathan/water_expand_mix/r15000 | seat0 | eliminated | 16.8 | 0.526 | 0.000 | 1900/2595 |
| flat_uneven/2000/water_expand_mix/water.leviathan/r15000 | seat1 | eliminated | 7.3 | 0.000 | 0.934 | 1730/3040 |
| flat_uneven/2000/water.leviathan/water_leviathan_mix/r15000 | seat1 | eliminated | 15.0 | 0.000 | 0.446 | 1900/2470 |
| flat_uneven/2000/water_leviathan_mix/water.leviathan/r15000 | seat1 | eliminated | 11.2 | 0.000 | 0.472 | 2470/3040 |
| flat_uneven/2000/water.reef_runner/water.shellback/r15000 | seat1 | eliminated | 15.6 | 0.000 | 0.951 | 2025/2975 |
| flat_uneven/2000/water.shellback/water.reef_runner/r15000 | seat0 | eliminated | 30.4 | 0.759 | 0.000 | 1925/3000 |
| flat_uneven/2000/water.reef_runner/water.spoutling/r15000 | seat0 | eliminated | 14.5 | 0.515 | 0.000 | 2025/3000 |
| flat_uneven/2000/water.spoutling/water.reef_runner/r15000 | seat1 | eliminated | 7.2 | 0.000 | 0.881 | 2000/3000 |
| flat_uneven/2000/water.reef_runner/skai_pressure_mix/r15000 | seat1 | eliminated | 10.7 | 0.000 | 0.607 | 2025/2750 |
| flat_uneven/2000/skai_pressure_mix/water.reef_runner/r15000 | seat1 | eliminated | 10.9 | 0.000 | 0.607 | 2200/3000 |
| flat_uneven/2000/water.reef_runner/skai_expand_mix/r15000 | seat1 | eliminated | 11.5 | 0.000 | 0.654 | 2025/3250 |
| flat_uneven/2000/skai_expand_mix/water.reef_runner/r15000 | seat0 | eliminated | 76.6 | 0.308 | 0.000 | 1950/3000 |
| flat_uneven/2000/water.reef_runner/skai_air_mix/r15000 | seat1 | eliminated | 9.0 | 0.000 | 0.920 | 2025/2820 |
| flat_uneven/2000/skai_air_mix/water.reef_runner/r15000 | seat0 | eliminated | 14.6 | 0.920 | 0.000 | 1880/3000 |
| flat_uneven/2000/water.reef_runner/water_pressure_mix/r15000 | seat1 | eliminated | 9.8 | 0.000 | 0.757 | 2025/2700 |
| flat_uneven/2000/water_pressure_mix/water.reef_runner/r15000 | seat1 | eliminated | 17.2 | 0.000 | 0.385 | 2025/3000 |
| flat_uneven/2000/water.reef_runner/water_expand_mix/r15000 | seat1 | eliminated | 11.3 | 0.000 | 0.946 | 2025/2595 |
| flat_uneven/2000/water_expand_mix/water.reef_runner/r15000 | seat1 | eliminated | 15.8 | 0.000 | 0.414 | 1730/3000 |
| flat_uneven/2000/water.reef_runner/water_leviathan_mix/r15000 | seat1 | eliminated | 7.3 | 0.000 | 1.000 | 2025/2470 |
| flat_uneven/2000/water_leviathan_mix/water.reef_runner/r15000 | seat0 | eliminated | 9.5 | 0.900 | 0.000 | 2470/3000 |
| flat_uneven/2000/water.shellback/water.spoutling/r15000 | seat0 | eliminated | 21.4 | 0.992 | 0.000 | 1925/3000 |
| flat_uneven/2000/water.spoutling/water.shellback/r15000 | seat1 | eliminated | 13.4 | 0.000 | 1.000 | 2000/2975 |
| flat_uneven/2000/water.shellback/skai_pressure_mix/r15000 | seat0 | eliminated | 26.9 | 0.818 | 0.000 | 1925/2750 |
| flat_uneven/2000/skai_pressure_mix/water.shellback/r15000 | seat1 | eliminated | 16.1 | 0.000 | 0.965 | 2200/2975 |
| flat_uneven/2000/water.shellback/skai_expand_mix/r15000 | stalemate | dome_absorbed | 54.0 | 0.639 | 0.308 | 1925/3250 |
| flat_uneven/2000/skai_expand_mix/water.shellback/r15000 | stalemate | dome_absorbed | 43.4 | 0.308 | 0.980 | 1950/2975 |
| flat_uneven/2000/water.shellback/skai_air_mix/r15000 | seat1 | eliminated | 65.0 | 0.000 | 0.920 | 1925/2820 |
| flat_uneven/2000/skai_air_mix/water.shellback/r15000 | stalemate | dome_absorbed | 35.4 | 0.920 | 1.000 | 1880/2975 |
| flat_uneven/2000/water.shellback/water_pressure_mix/r15000 | seat1 | eliminated | 17.3 | 0.000 | 0.751 | 1925/2700 |
| flat_uneven/2000/water_pressure_mix/water.shellback/r15000 | seat1 | eliminated | 26.2 | 0.000 | 0.548 | 2025/2975 |
| flat_uneven/2000/water.shellback/water_expand_mix/r15000 | seat1 | eliminated | 41.2 | 0.000 | 0.890 | 1925/2595 |
| flat_uneven/2000/water_expand_mix/water.shellback/r15000 | seat1 | eliminated | 28.0 | 0.000 | 0.875 | 1730/2975 |
| flat_uneven/2000/water.shellback/water_leviathan_mix/r15000 | seat0 | eliminated | 45.4 | 0.664 | 0.000 | 1925/2470 |
| flat_uneven/2000/water_leviathan_mix/water.shellback/r15000 | seat1 | eliminated | 28.9 | 0.000 | 0.986 | 2470/2975 |
| flat_uneven/2000/water.spoutling/skai_pressure_mix/r15000 | seat1 | eliminated | 7.5 | 0.000 | 0.873 | 2000/2750 |
| flat_uneven/2000/skai_pressure_mix/water.spoutling/r15000 | seat0 | eliminated | 13.5 | 0.588 | 0.000 | 2200/3000 |
| flat_uneven/2000/water.spoutling/skai_expand_mix/r15000 | seat1 | eliminated | 7.6 | 0.000 | 0.892 | 2000/3250 |
| flat_uneven/2000/skai_expand_mix/water.spoutling/r15000 | seat0 | eliminated | 19.2 | 0.399 | 0.000 | 1950/3000 |
| flat_uneven/2000/water.spoutling/skai_air_mix/r15000 | seat1 | eliminated | 6.4 | 0.000 | 0.896 | 2000/2820 |
| flat_uneven/2000/skai_air_mix/water.spoutling/r15000 | seat0 | eliminated | 16.8 | 0.418 | 0.000 | 1880/3000 |
| flat_uneven/2000/water.spoutling/water_pressure_mix/r15000 | seat1 | eliminated | 8.3 | 0.000 | 0.882 | 2000/2700 |
| flat_uneven/2000/water_pressure_mix/water.spoutling/r15000 | seat0 | eliminated | 13.0 | 0.639 | 0.000 | 2025/3000 |
| flat_uneven/2000/water.spoutling/water_expand_mix/r15000 | seat1 | eliminated | 10.1 | 0.000 | 0.984 | 2000/2595 |
| flat_uneven/2000/water_expand_mix/water.spoutling/r15000 | seat0 | eliminated | 16.7 | 0.816 | 0.000 | 1730/3000 |
| flat_uneven/2000/water.spoutling/water_leviathan_mix/r15000 | seat1 | eliminated | 5.1 | 0.000 | 1.000 | 2000/2470 |
| flat_uneven/2000/water_leviathan_mix/water.spoutling/r15000 | seat0 | eliminated | 7.6 | 1.000 | 0.000 | 2470/3000 |
| flat_uneven/2000/skai_pressure_mix/skai_expand_mix/r15000 | seat1 | eliminated | 12.4 | 0.000 | 0.676 | 2200/3250 |
| flat_uneven/2000/skai_expand_mix/skai_pressure_mix/r15000 | seat1 | eliminated | 11.3 | 0.000 | 0.635 | 1950/2750 |
| flat_uneven/2000/skai_pressure_mix/skai_air_mix/r15000 | seat1 | eliminated | 9.4 | 0.000 | 0.738 | 2200/2820 |
| flat_uneven/2000/skai_air_mix/skai_pressure_mix/r15000 | seat1 | eliminated | 12.4 | 0.000 | 0.426 | 1880/2750 |
| flat_uneven/2000/skai_pressure_mix/water_pressure_mix/r15000 | seat1 | eliminated | 10.5 | 0.000 | 0.727 | 2200/2700 |
| flat_uneven/2000/water_pressure_mix/skai_pressure_mix/r15000 | seat0 | eliminated | 23.6 | 0.191 | 0.000 | 2025/2750 |
| flat_uneven/2000/skai_pressure_mix/water_expand_mix/r15000 | seat1 | eliminated | 12.8 | 0.000 | 0.958 | 2200/2595 |
| flat_uneven/2000/water_expand_mix/skai_pressure_mix/r15000 | seat1 | eliminated | 23.0 | 0.000 | 0.213 | 1730/2750 |
| flat_uneven/2000/skai_pressure_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 8.2 | 0.000 | 0.982 | 2200/2470 |
| flat_uneven/2000/water_leviathan_mix/skai_pressure_mix/r15000 | seat0 | eliminated | 11.0 | 0.915 | 0.000 | 2470/2750 |
| flat_uneven/2000/skai_expand_mix/skai_air_mix/r15000 | seat1 | eliminated | 9.9 | 0.000 | 0.743 | 1950/2820 |
| flat_uneven/2000/skai_air_mix/skai_expand_mix/r15000 | seat1 | eliminated | 12.2 | 0.000 | 0.543 | 1880/3250 |
| flat_uneven/2000/skai_expand_mix/water_pressure_mix/r15000 | seat0 | eliminated | 102.3 | 0.308 | 0.000 | 1950/2700 |
| flat_uneven/2000/water_pressure_mix/skai_expand_mix/r15000 | seat1 | eliminated | 22.1 | 0.000 | 0.353 | 2025/3250 |
| flat_uneven/2000/skai_expand_mix/water_expand_mix/r15000 | seat1 | eliminated | 13.6 | 0.000 | 0.963 | 1950/2595 |
| flat_uneven/2000/water_expand_mix/skai_expand_mix/r15000 | seat1 | eliminated | 17.8 | 0.000 | 0.530 | 1730/3250 |
| flat_uneven/2000/skai_expand_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 8.4 | 0.000 | 0.983 | 1950/2470 |
| flat_uneven/2000/water_leviathan_mix/skai_expand_mix/r15000 | seat0 | eliminated | 12.2 | 0.829 | 0.000 | 2470/3250 |
| flat_uneven/2000/skai_air_mix/water_pressure_mix/r15000 | seat0 | eliminated | 21.6 | 0.920 | 0.000 | 1880/2700 |
| flat_uneven/2000/water_pressure_mix/skai_air_mix/r15000 | seat1 | eliminated | 10.1 | 0.000 | 0.920 | 2025/2820 |
| flat_uneven/2000/skai_air_mix/water_expand_mix/r15000 | seat1 | eliminated | 19.3 | 0.000 | 0.737 | 1880/2595 |
| flat_uneven/2000/water_expand_mix/skai_air_mix/r15000 | seat1 | eliminated | 16.8 | 0.000 | 0.670 | 1730/2820 |
| flat_uneven/2000/skai_air_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 10.7 | 0.000 | 0.899 | 1880/2470 |
| flat_uneven/2000/water_leviathan_mix/skai_air_mix/r15000 | seat0 | eliminated | 13.5 | 0.491 | 0.000 | 2470/2820 |
| flat_uneven/2000/water_pressure_mix/water_expand_mix/r15000 | seat1 | eliminated | 17.7 | 0.000 | 0.701 | 2025/2595 |
| flat_uneven/2000/water_expand_mix/water_pressure_mix/r15000 | seat1 | eliminated | 13.1 | 0.000 | 0.560 | 1730/2700 |
| flat_uneven/2000/water_pressure_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 7.5 | 0.000 | 0.890 | 2025/2470 |
| flat_uneven/2000/water_leviathan_mix/water_pressure_mix/r15000 | seat0 | eliminated | 10.5 | 0.821 | 0.000 | 2470/2700 |
| flat_uneven/2000/water_expand_mix/water_leviathan_mix/r15000 | seat1 | eliminated | 13.9 | 0.000 | 0.941 | 1730/2470 |
| flat_uneven/2000/water_leviathan_mix/water_expand_mix/r15000 | seat0 | eliminated | 19.9 | 0.691 | 0.000 | 2470/2595 |

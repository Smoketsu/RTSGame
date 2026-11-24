<!-- fights.md: written by the balance harness --fights (rts/tools/BalanceHarness/FightCoordinator.cs); not a source file. -->
# Equal-value fights

- Data digest: tuning a020a25c7d7c2eae, content bc85169fa2a3c598.
- Base seed 20261118; fight seed = SeedDerivation.GameSeed(base seed, "fights", fight ID, 0).
- Value: cost_common + cost_rare per unit; an army is the recipe repeated k = max(1, (2V + rv) / (2 rv)) times.
- Control: plain attack-move at the enemy front rank, idle units re-target every 2 s; no strategic AI, no HQ.
- Fights: 780 (13 sides); determinism spot check: 2 fights re-run in-process, 0 mismatches.

**FIGHTS fights=780 decided=765 draws=0 stalemates=13 unresolved=2 mismatched=0 checks_passed=2 of=8 dome_absorbed=12**

## Design-rule checks

| Check | Rule | Threshold | Result | Detail |
| --- | --- | --- | --- | --- |
| rule1_no_immunity | P-005 1.6 rule 1 | >= 2 mobile and >= 1 static answers per enemy unit or HQ; every armed unit hits Ground | PASS | 13 targets checked |
| rule2_multiplier_band | P-005 1.6 rule 2 | multipliers in [0.5, 1.5]; ratio within a damage type <= 2 | FAIL | drench/structure 0.25 below 0.5<br>drench ratio 4.00 above 2 |
| rule3_counter_margin | P-005 1.6 rule 3 | counter keeps >= 0.2 value, exchange ratio <= 3 | FAIL | skai.roc_rider loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 11.2 above 3<br>skai.roc_rider loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 11.2 above 3<br>skai.stormcaller loses to water.shellback on flat: water.shellback in seat 0 exchange ratio inf above 3<br>skai.stormcaller loses to water.shellback on flat: water.shellback in seat 1 exchange ratio inf above 3<br>skai.talon_archer loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 49.3 above 3<br>skai.talon_archer loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 24.1 above 3<br>skai.thunderhead loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>skai.thunderhead loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>skai.windrunner loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio inf above 3<br>skai.windrunner loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio inf above 3<br>water.grasper loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.grasper loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.ink_lobber loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.ink_lobber loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.leviathan loses to water.shellback on flat: water.shellback in seat 1 exchange ratio 3.0 above 3<br>water.reef_runner loses to skai.roc_rider on flat: skai.roc_rider in seat 0 exchange ratio inf above 3<br>water.reef_runner loses to skai.roc_rider on flat: skai.roc_rider in seat 1 exchange ratio inf above 3<br>water.shellback loses to skai.thunderhead on flat: skai.thunderhead in seat 0 exchange ratio inf above 3<br>water.shellback loses to skai.thunderhead on flat: skai.thunderhead in seat 1 exchange ratio inf above 3<br>water.spoutling loses to water.leviathan on flat: water.leviathan in seat 0 exchange ratio 123.4 above 3<br>water.spoutling loses to water.leviathan on flat: water.leviathan in seat 1 exchange ratio 123.4 above 3 |
| rule4_no_dominant_unit | P-005 1.6 rule 4 | no unit beats all its faction mates on flat | PASS | no dominant unit |
| rule4_every_unit_wins | P-005 1.6 rule 4 | every unit beats >= 1 unit of another faction on flat | FAIL | water.spoutling beats no unit of another faction on flat |
| rule5_two_answers | P-005 1.6 rule 5 | >= 1 unit and >= 1 terrain answers per threat and faction | FAIL | massed_armour (water.grasper) against water: 2 unit answers (water.ink_lobber, water.leviathan), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only)<br>massed_light (skai.windrunner) against water: 5 unit answers (water.grasper, water.ink_lobber, water.leviathan, water.reef_runner, water.shellback), needs 1; 0 terrain answers (), needs 1 (non-purchase answers measured: terrain only) |
| rule6_hq_floor | P-005 1.6 rule 6 | Rooted HQ lives >= 15.0 s against 30 supply | FAIL | skai skai.stormcaller x10 vs skai.great_aerie: 11.0 s, needs >= 15.0 s<br>skai skai.stormcaller x10 vs water.grand_shell: 13.5 s, needs >= 15.0 s<br>skai skai.thunderhead x7 vs skai.great_aerie: 8.4 s, needs >= 15.0 s<br>skai skai.thunderhead x7 vs water.grand_shell: 10.4 s, needs >= 15.0 s<br>skai skai.windrunner x30 vs skai.great_aerie: 13.9 s, needs >= 15.0 s<br>water water.ink_lobber x15 vs skai.great_aerie: 6.6 s, needs >= 15.0 s<br>water water.ink_lobber x15 vs water.grand_shell: 7.6 s, needs >= 15.0 s<br>water water.leviathan x7 vs skai.great_aerie: 3.0 s, needs >= 15.0 s<br>water water.leviathan x7 vs water.grand_shell: 3.0 s, needs >= 15.0 s<br>water water.reef_runner x30 vs skai.great_aerie: 14.2 s, needs >= 15.0 s |
| matchups_agree | P-005 developer decision 1 (rule 7) | every unit has an entry; wins and loses match the fights | FAIL | water.spoutling: wins is null, but every armed unit must claim a fight it wins |

## Value exchange: flat, 750

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | water_ai_mix | skai_ai_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | +0.09 | +1.00 | -0.25 | +1.00 | +1.00 | -0.79 | +1.00 | S | +0.72 | -0.49* | +0.24 |
| skai.stormcaller | -1.00 | · | -0.38 | -1.00 | -0.30 | -0.18 | -0.17 | -0.83 | -0.24 | -0.94 | +0.71 | -0.65 | -0.60 |
| skai.talon_archer | -0.11 | +0.39 | · | -0.61 | +0.11 | -0.57 | +0.24 | -0.85 | +0.14 | -0.95 | +0.84 | -0.66 | +0.05 |
| skai.thunderhead | -1.00 | +1.00 | +0.65 | · | +0.72 | +1.00 | +1.00 | -0.55 | +1.00 | +1.00 | +0.87 | +0.34 | -0.25 |
| skai.windrunner | +0.23 | +0.25 | -0.26 | -0.55 | · | -0.66 | +0.05 | -0.99 | -0.24 | -0.85 | +0.78 | -0.75 | -0.40 |
| water.grasper | -1.00 | +0.37 | +0.59 | -1.00 | +0.65 | · | -0.22 | -0.99 | +0.59 | +0.65 | +0.83 | +0.27 | -0.25 |
| water.ink_lobber | -1.00 | +0.30 | -0.31 | -1.00 | -0.10 | +0.23 | · | -0.78 | -0.09* | -0.82 | +0.76 | -0.39 | -0.47 |
| water.leviathan | +0.79 | +0.83 | +0.86 | +0.55 | +0.99 | +0.99 | +0.78 | · | +0.83 | -0.59 | +0.94 | +0.69 | +0.87 |
| water.reef_runner | -1.00 | +0.04 | -0.04 | -1.00 | +0.29 | -0.61 | -0.04* | -0.84 | · | -0.85 | +0.79 | -0.73 | -0.25 |
| water.shellback | S | +1.00 | +0.91 | -1.00 | +0.95 | -0.64 | +1.00 | +0.59 | +0.93 | · | +0.98 | +0.49 | S |
| water.spoutling | -0.70 | -0.75 | -0.83 | -0.86 | -0.80 | -0.84 | -0.76 | -0.94 | -0.80 | -0.96 | · | -0.91 | -0.83 |
| water_ai_mix | -0.15* | +0.76 | +0.66 | -0.14 | +0.71 | -0.34 | +0.57 | -0.66 | +0.70 | -0.52 | +0.88 | · | +0.62 |
| skai_ai_mix | -0.29 | +0.60 | -0.02 | +0.25 | +0.35 | +0.25 | +0.39 | -0.88 | +0.25 | S | +0.81 | -0.68 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: flat, 1500

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | water_ai_mix | skai_ai_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | +0.23 | +1.00 | -0.16 | +1.00 | +1.00 | -0.91 | +1.00 | S | +0.75 | -0.71 | +0.41 |
| skai.stormcaller | -1.00 | · | -0.25 | -1.00 | -0.16 | -0.19* | -0.18 | -0.92 | -0.09 | -1.00 | +0.84 | -0.75 | -0.67 |
| skai.talon_archer | -0.23 | +0.19 | · | -0.69 | -0.23 | -0.57 | -0.03 | -0.96 | -0.33 | -0.92 | +0.80 | -0.78 | -0.33 |
| skai.thunderhead | -1.00 | +1.00 | +0.72 | · | +0.81 | +1.00 | +1.00 | -0.79 | +1.00 | +1.00 | +0.89 | +0.65 | -0.25 |
| skai.windrunner | +0.33 | +0.30 | +0.23 | -0.81 | · | -0.61 | -0.36 | -1.00 | -0.27 | -0.91 | +0.79 | -0.77 | -0.42 |
| water.grasper | -1.00 | -0.15* | +0.57 | -1.00 | +0.60 | · | -0.35 | -1.00 | +0.54 | +0.52 | +0.80 | -0.15 | -0.25 |
| water.ink_lobber | -1.00 | +0.37 | +0.35 | -1.00 | +0.36 | +0.29 | · | -0.80 | +0.39 | -0.90 | +0.86 | -0.54 | -0.49 |
| water.leviathan | +0.91 | +0.96 | +0.98 | +0.79 | +1.00 | +1.00 | +0.80 | · | +0.96 | -0.68 | +0.99 | +0.76 | +0.91 |
| water.reef_runner | -1.00 | +0.09 | +0.29 | -1.00 | +0.22 | -0.55 | -0.44 | -0.95 | · | -0.94 | +0.80 | -0.76 | -0.25 |
| water.shellback | U | +1.00 | +0.99 | -1.00 | +1.00 | -0.51 | +1.00 | +0.61 | +0.98 | · | +1.00 | +0.60 | S |
| water.spoutling | -0.72 | -0.84 | -0.83 | -0.90 | -0.79 | -0.80 | -0.85 | -0.99 | -0.82 | -0.98 | · | -0.93 | -0.82 |
| water_ai_mix | +0.44 | +0.55 | +0.71 | -0.57 | +0.76 | +0.25 | +0.55 | -0.80 | +0.69 | -0.66 | +0.86 | · | +0.58 |
| skai_ai_mix | -0.41 | +0.50 | +0.34 | +0.25 | +0.42 | +0.25 | +0.25 | -0.91 | +0.25 | S | +0.82 | -0.64 | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal.

## Value exchange: high_ground, 1500

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | water_ai_mix | skai_ai_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | +0.21 | +1.00 | +0.88* | +1.00 | +1.00 | -0.85 | +1.00 | +1.00 | +0.76 | +0.96* | +0.57* |
| skai.stormcaller | -1.00 | · | -0.59* | -1.00 | -0.34* | +1.00 | -0.64* | -1.00 | -0.43 | S* | +0.23 | +0.44* | -0.56 |
| skai.talon_archer | -0.19 | -0.02* | · | -0.72 | +0.53 | +1.00* | -0.48* | -0.99 | +0.52 | +1.00* | +0.72 | -0.49 | -0.49* |
| skai.thunderhead | -1.00 | +1.00 | +0.52 | · | +0.93 | +1.00 | +1.00 | -0.63* | +1.00 | +1.00 | +0.86 | +0.90 | -0.25 |
| skai.windrunner | +0.33* | -0.19* | -0.25 | -0.72 | · | +1.00* | -0.53 | -1.00 | +0.06* | +0.85* | +0.73 | -0.59 | -0.44 |
| water.grasper | -1.00 | -0.13 | +0.54* | -1.00 | +0.57* | · | -0.22 | -1.00 | +0.47* | +0.54* | +0.79* | +0.09* | -0.25 |
| water.ink_lobber | -1.00 | -0.39* | -0.56* | -1.00 | +0.36 | +1.00 | · | -1.00 | +0.36 | +1.00* | +0.34 | +0.46* | -0.54 |
| water.leviathan | +0.67 | +0.50 | +0.61 | -0.29* | +0.79 | +1.00 | +0.11 | · | +0.79 | +0.75* | +0.81 | +0.71 | +0.64 |
| water.reef_runner | -1.00 | +0.03 | -0.27 | -1.00 | +0.13* | +1.00* | -0.58 | -0.98 | · | +0.90* | +0.75 | -0.64 | -0.40 |
| water.shellback | -1.00 | +1.00* | +0.99* | -1.00 | +0.98* | +0.57* | +1.00* | +0.36* | +0.98* | · | +1.00* | +0.69* | -0.25 |
| water.spoutling | -0.82 | -0.85 | -0.89 | -0.91 | -0.78 | +1.00* | -0.90 | -1.00 | -0.80 | S* | · | -0.84 | -0.88 |
| water_ai_mix | +0.44* | +0.45* | +0.55 | -0.83 | +0.49 | +0.65* | +0.26* | -1.00 | +0.51 | +0.56* | +0.80 | · | +0.32* |
| skai_ai_mix | +0.15* | +0.26 | -0.25* | +0.38 | +0.72 | +1.00 | +0.25 | -0.91 | +0.72 | +1.00 | +0.71 | +0.11* | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal. The row is the attacker; the column holds the plateau.

## Value exchange: flooded, 1500

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | water_ai_mix | skai_ai_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | -1.00* | +1.00 | -1.00* | +1.00 | +1.00 | -1.00 | -1.00* | -1.00 | -0.95* | -1.00* | -1.00* |
| skai.stormcaller | -1.00 | · | -1.00* | -1.00 | -1.00* | -1.00 | -1.00* | -1.00 | -1.00* | -1.00 | -0.65* | -1.00 | -1.00 |
| skai.talon_archer | -1.00* | -1.00* | · | -1.00* | -1.00* | -1.00 | -1.00* | -1.00 | -1.00* | -1.00 | -0.61* | -1.00* | -1.00* |
| skai.thunderhead | -1.00 | +1.00 | -0.31* | · | -0.45* | +1.00 | +1.00 | -1.00* | -0.60* | -1.00 | +0.56 | -0.54* | -0.31 |
| skai.windrunner | -0.79* | -0.82* | -0.83* | -0.93* | · | -0.90 | -0.91* | -1.00 | -0.87* | -1.00 | +0.25 | -0.94* | -0.87* |
| water.grasper | -1.00 | +0.35 | +0.61 | -1.00 | +0.63 | · | +0.10 | -0.97 | +0.59 | +0.51 | +0.83 | +0.14 | -0.25* |
| water.ink_lobber | -1.00 | -0.70* | -0.86* | -1.00 | -1.00* | -0.96 | · | -1.00 | -1.00* | -1.00 | -0.21* | -1.00* | -0.98 |
| water.leviathan | +0.66 | +0.71 | +0.72 | -0.21* | +0.59 | +0.66 | +0.39 | · | +0.41 | -0.89 | +0.78 | +0.36 | +0.53 |
| water.reef_runner | -0.82* | -0.82* | -0.79* | -0.94* | -0.80* | -0.90 | -0.92* | -1.00 | · | -1.00 | +0.51 | -0.96* | -0.84* |
| water.shellback | +1.00 | +0.98 | +0.99 | +0.87 | +0.99 | -0.70 | +0.91 | +0.19 | +0.99 | · | +1.00 | -0.71* | +1.00 |
| water.spoutling | -1.00* | -1.00* | -1.00* | -1.00 | -1.00 | -1.00 | -1.00* | -1.00 | -1.00 | -1.00 | · | -1.00 | -1.00* |
| water_ai_mix | -0.73* | +0.15 | -0.20* | -0.93* | -0.19* | -0.82 | -0.53* | -0.97 | -0.17* | -0.72* | +0.89 | · | -0.42* |
| skai_ai_mix | -0.91* | +0.25 | -0.99* | +0.25 | -0.97* | S* | +0.25 | -1.00 | -0.99* | -1.00 | -0.75* | -0.99* | · |

Cell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, `*` the two seats of the pair disagree on the winner, · the diagonal. The row fights in the column's flood.

## Value exchange: choke, 1500

| | skai.roc_rider | skai.stormcaller | skai.talon_archer | skai.thunderhead | skai.windrunner | water.grasper | water.ink_lobber | water.leviathan | water.reef_runner | water.shellback | water.spoutling | water_ai_mix | skai_ai_mix |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai.roc_rider | · | +1.00 | +0.46 | +1.00 | -0.25 | +1.00 | +1.00 | -0.33 | +1.00 | S | +0.73 | -0.64 | +0.44 |
| skai.stormcaller | -1.00 | · | -0.23 | -1.00 | -0.14 | -0.06 | -0.18 | -0.92 | -0.13 | -1.00 | +0.84 | -0.74 | -0.60 |
| skai.talon_archer | -0.13 | +0.15 | · | -0.73 | -0.23 | -0.57 | -0.22 | -0.96 | -0.33 | -0.97 | +0.79 | -0.77 | -0.37 |
| skai.thunderhead | -1.00 | +1.00 | +0.76 | · | +0.82 | +1.00 | +1.00 | -0.47 | +1.00 | +1.00 | +0.90 | +0.65 | -0.25 |
| skai.windrunner | +0.38 | +0.21 | +0.29 | -0.79 | · | -0.62 | -0.51 | -1.00 | -0.33 | -0.96 | +0.79 | -0.78 | -0.42 |
| water.grasper | -1.00 | +0.11 | +0.56 | -1.00 | +0.60 | · | -0.43 | -1.00 | +0.54 | +0.52 | +0.81 | +0.11 | -0.25 |
| water.ink_lobber | -1.00 | +0.37 | +0.38 | -1.00 | +0.41 | +0.27 | · | -0.80 | +0.47 | -0.95 | +0.86 | -0.53 | -0.47 |
| water.leviathan | +0.72 | +0.96 | +0.98 | +0.47 | +1.00 | +1.00 | +0.80 | · | +0.96 | -0.61 | +0.99 | +0.76 | +0.91 |
| water.reef_runner | -1.00 | +0.14 | +0.32 | -1.00 | +0.22 | -0.53 | -0.43 | -0.95 | · | -0.93 | +0.81 | -0.77 | -0.25 |
| water.shellback | U | +1.00 | +1.00 | -1.00 | +1.00 | -0.51 | +1.00 | +0.71 | +0.95 | · | +1.00 | +0.61 | S |
| water.spoutling | -0.73 | -0.84 | -0.83 | -0.90 | -0.79 | -0.81 | -0.85 | -0.99 | -0.81 | -0.98 | · | -0.93 | -0.82 |
| water_ai_mix | +0.52 | +0.55 | +0.71 | -0.39 | +0.76 | -0.01 | +0.55 | -0.80 | +0.69 | -0.67 | +0.86 | · | +0.63 |
| skai_ai_mix | -0.42 | +0.52 | +0.36 | +0.25 | +0.41 | +0.25 | +0.25 | -0.91 | +0.25 | S | +0.82 | -0.63 | · |

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
| skai | skai_ai_mix | 14 | skai.great_aerie | uprooted | 15.5 s | reported |
| skai | skai_ai_mix | 14 | skai.great_aerie | rooted | 20.0 s | PASS |
| skai | skai_ai_mix | 14 | water.grand_shell | uprooted | 18.9 s | reported |
| skai | skai_ai_mix | 14 | water.grand_shell | rooted | 25.0 s | PASS |
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
| water | water.reef_runner | 30 | water.grand_shell | rooted | 17.3 s | PASS |
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
| water | water_ai_mix | 12 | skai.great_aerie | uprooted | 16.3 s | reported |
| water | water_ai_mix | 12 | skai.great_aerie | rooted | 20.8 s | PASS |
| water | water_ai_mix | 12 | water.grand_shell | uprooted | 19.2 s | reported |
| water | water_ai_mix | 12 | water.grand_shell | rooted | 24.9 s | PASS |

## Matchups file

| Unit | Claim | Measured | Agrees |
| --- | --- | --- | --- |
| skai.roc_rider | beats water.grasper on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| skai.roc_rider | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.stormcaller | beats water.spoutling on flat | measured seat 0 win (left 0.84), seat 1 win (left 0.84) | yes |
| skai.stormcaller | loses to water.shellback on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.talon_archer | beats water.spoutling on flat | measured seat 0 win (left 0.80), seat 1 win (left 0.83) | yes |
| skai.talon_archer | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.thunderhead | beats water.grasper on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| skai.thunderhead | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| skai.windrunner | beats water.spoutling on flat | measured seat 0 win (left 0.79), seat 1 win (left 0.79) | yes |
| skai.windrunner | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.grasper | beats water.spoutling on flat | measured seat 0 win (left 0.80), seat 1 win (left 0.80) | yes |
| water.grasper | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.ink_lobber | beats water.spoutling on flat | measured seat 0 win (left 0.86), seat 1 win (left 0.85) | yes |
| water.ink_lobber | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.leviathan | beats skai.windrunner on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| water.leviathan | loses to water.shellback on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.reef_runner | beats water.spoutling on flat | measured seat 0 win (left 0.80), seat 1 win (left 0.82) | yes |
| water.reef_runner | loses to skai.roc_rider on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.shellback | beats skai.stormcaller on flat | measured seat 0 win (left 1.00), seat 1 win (left 1.00) | yes |
| water.shellback | loses to skai.thunderhead on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |
| water.spoutling | wins: null | - | no |
| water.spoutling | loses to water.leviathan on flat | measured seat 0 loss (left 0.00), seat 1 loss (left 0.00) | yes |

## Suggested matchups entries

| Unit | Wins against | Loses to |
| --- | --- | --- |
| skai.roc_rider | water.grasper (left 1.00) | water.leviathan (their left 0.91) |
| skai.stormcaller | water.spoutling (left 0.84) | water.shellback (their left 1.00) |
| skai.talon_archer | water.spoutling (left 0.81) | water.leviathan (their left 0.97) |
| skai.thunderhead | water.grasper (left 1.00) | skai.roc_rider (their left 1.00) |
| skai.windrunner | water.spoutling (left 0.79) | water.leviathan (their left 1.00) |
| water.grasper | water.spoutling (left 0.80) | skai.roc_rider (their left 1.00) |
| water.ink_lobber | water.spoutling (left 0.86) | skai.roc_rider (their left 1.00) |
| water.leviathan | skai.windrunner (left 1.00) | water.shellback (their left 0.64) |
| water.reef_runner | water.spoutling (left 0.81) | skai.roc_rider (their left 1.00) |
| water.shellback | skai.stormcaller (left 1.00) | skai.thunderhead (their left 1.00) |
| water.spoutling | null | water.leviathan (their left 0.99) |

## Stalemates, unresolved fights and seat splits

A seat split means the two seat orders of a pair have different winners. On high_ground and flooded the seats are not symmetric by design, so splits there show terrain at work, not seat bias. A `no_damage` stalemate counts only damage events; when shots absorbed by a shield dome fell inside the idle window the stalemate is `dome_absorbed` instead.

| Fight | Finding | Reason |
| --- | --- | --- |
| flat/750/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/750/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/750/skai.roc_rider/water_ai_mix | seat split (water_ai_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flat/750/water.ink_lobber/water.reef_runner | seat split (water.reef_runner wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flat/750/water.shellback/skai_ai_mix | stalemate | dome_absorbed |
| flat/750/skai_ai_mix/water.shellback | stalemate | dome_absorbed |
| flat/1500/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/1500/water.shellback/skai.roc_rider | unresolved | time_cap |
| flat/1500/skai.stormcaller/water.grasper | seat split (water.grasper wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flat/1500/water.shellback/skai_ai_mix | stalemate | dome_absorbed |
| flat/1500/skai_ai_mix/water.shellback | stalemate | dome_absorbed |
| high_ground/1500/skai.roc_rider/skai.windrunner | seat split (skai.roc_rider wins this order, skai.windrunner wins the swapped order) | eliminated |
| high_ground/1500/skai.roc_rider/water_ai_mix | seat split (skai.roc_rider wins this order, water_ai_mix wins the swapped order) | eliminated |
| high_ground/1500/skai.roc_rider/skai_ai_mix | seat split (skai.roc_rider wins this order, skai_ai_mix wins the swapped order) | eliminated |
| high_ground/1500/skai.stormcaller/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.stormcaller wins the swapped order) | eliminated |
| high_ground/1500/skai.stormcaller/skai.windrunner | seat split (skai.windrunner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| high_ground/1500/skai.stormcaller/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.stormcaller wins the swapped order) | eliminated |
| high_ground/1500/skai.stormcaller/water.shellback | stalemate | dome_absorbed |
| high_ground/1500/skai.stormcaller/water_ai_mix | seat split (skai.stormcaller wins this order, water_ai_mix wins the swapped order) | eliminated |
| high_ground/1500/skai.talon_archer/water.grasper | seat split (skai.talon_archer wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/1500/skai.talon_archer/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.talon_archer wins the swapped order) | eliminated |
| high_ground/1500/skai.talon_archer/water.shellback | seat split (skai.talon_archer wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1500/skai.talon_archer/skai_ai_mix | seat split (skai_ai_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| high_ground/1500/skai.thunderhead/water.leviathan | seat split (water.leviathan wins this order, skai.thunderhead wins the swapped order) | eliminated |
| high_ground/1500/skai.windrunner/water.grasper | seat split (skai.windrunner wins this order, water.grasper wins the swapped order) | eliminated |
| high_ground/1500/skai.windrunner/water.reef_runner | seat split (skai.windrunner wins this order, water.reef_runner wins the swapped order) | eliminated |
| high_ground/1500/skai.windrunner/water.shellback | seat split (skai.windrunner wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1500/water.grasper/water.reef_runner | seat split (water.grasper wins this order, water.reef_runner wins the swapped order) | eliminated |
| high_ground/1500/water.grasper/water.shellback | seat split (water.grasper wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1500/water.grasper/water.spoutling | seat split (water.grasper wins this order, water.spoutling wins the swapped order) | eliminated |
| high_ground/1500/water.grasper/water_ai_mix | seat split (water.grasper wins this order, water_ai_mix wins the swapped order) | eliminated |
| high_ground/1500/water.ink_lobber/water.shellback | seat split (water.ink_lobber wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1500/water.ink_lobber/water_ai_mix | seat split (water.ink_lobber wins this order, water_ai_mix wins the swapped order) | eliminated |
| high_ground/1500/water.leviathan/water.shellback | seat split (water.leviathan wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1500/water.reef_runner/water.shellback | seat split (water.reef_runner wins this order, water.shellback wins the swapped order) | eliminated |
| high_ground/1500/water.spoutling/water.shellback | stalemate | dome_absorbed |
| high_ground/1500/water.shellback/water_ai_mix | seat split (water.shellback wins this order, water_ai_mix wins the swapped order) | eliminated |
| high_ground/1500/skai_ai_mix/water_ai_mix | seat split (skai_ai_mix wins this order, water_ai_mix wins the swapped order) | eliminated |
| flooded/1500/skai.roc_rider/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/1500/skai.roc_rider/skai.windrunner | seat split (skai.windrunner wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/1500/skai.roc_rider/water.reef_runner | seat split (water.reef_runner wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/1500/skai.roc_rider/water.spoutling | seat split (water.spoutling wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/1500/skai.roc_rider/water_ai_mix | seat split (water_ai_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/1500/skai.roc_rider/skai_ai_mix | seat split (skai_ai_mix wins this order, skai.roc_rider wins the swapped order) | eliminated |
| flooded/1500/skai.stormcaller/skai.talon_archer | seat split (skai.talon_archer wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/1500/skai.stormcaller/skai.windrunner | seat split (skai.windrunner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/1500/skai.stormcaller/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/1500/skai.stormcaller/water.reef_runner | seat split (water.reef_runner wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/1500/skai.stormcaller/water.spoutling | seat split (water.spoutling wins this order, skai.stormcaller wins the swapped order) | eliminated |
| flooded/1500/skai.talon_archer/skai.thunderhead | seat split (skai.thunderhead wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/1500/skai.talon_archer/skai.windrunner | seat split (skai.windrunner wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/1500/skai.talon_archer/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/1500/skai.talon_archer/water.reef_runner | seat split (water.reef_runner wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/1500/skai.talon_archer/water.spoutling | seat split (water.spoutling wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/1500/skai.talon_archer/water_ai_mix | seat split (water_ai_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/1500/skai.talon_archer/skai_ai_mix | seat split (skai_ai_mix wins this order, skai.talon_archer wins the swapped order) | eliminated |
| flooded/1500/skai.thunderhead/skai.windrunner | seat split (skai.windrunner wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/1500/skai.thunderhead/water.leviathan | seat split (water.leviathan wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/1500/skai.thunderhead/water.reef_runner | seat split (water.reef_runner wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/1500/skai.thunderhead/water_ai_mix | seat split (water_ai_mix wins this order, skai.thunderhead wins the swapped order) | eliminated |
| flooded/1500/skai.windrunner/water.ink_lobber | seat split (water.ink_lobber wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/1500/skai.windrunner/water.reef_runner | seat split (water.reef_runner wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/1500/skai.windrunner/water_ai_mix | seat split (water_ai_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/1500/skai.windrunner/skai_ai_mix | seat split (skai_ai_mix wins this order, skai.windrunner wins the swapped order) | eliminated |
| flooded/1500/skai_ai_mix/water.grasper | stalemate | no_damage |
| flooded/1500/water.ink_lobber/water.reef_runner | seat split (water.reef_runner wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/1500/water.ink_lobber/water.spoutling | seat split (water.spoutling wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/1500/water.ink_lobber/water_ai_mix | seat split (water_ai_mix wins this order, water.ink_lobber wins the swapped order) | eliminated |
| flooded/1500/water.reef_runner/water_ai_mix | seat split (water_ai_mix wins this order, water.reef_runner wins the swapped order) | eliminated |
| flooded/1500/skai_ai_mix/water.reef_runner | seat split (water.reef_runner wins this order, skai_ai_mix wins the swapped order) | eliminated |
| flooded/1500/water.shellback/water_ai_mix | seat split (water_ai_mix wins this order, water.shellback wins the swapped order) | eliminated |
| flooded/1500/skai_ai_mix/water.spoutling | seat split (water.spoutling wins this order, skai_ai_mix wins the swapped order) | eliminated |
| flooded/1500/skai_ai_mix/water_ai_mix | seat split (water_ai_mix wins this order, skai_ai_mix wins the swapped order) | eliminated |
| choke/1500/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| choke/1500/water.shellback/skai.roc_rider | unresolved | time_cap |
| choke/1500/water.shellback/skai_ai_mix | stalemate | dome_absorbed |
| choke/1500/skai_ai_mix/water.shellback | stalemate | dome_absorbed |

## All fights

| Fight | Result | Reason | Seconds | Left 0 | Left 1 | Values |
| --- | --- | --- | ---: | ---: | ---: | --- |
| flat/750/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 13.9 | 1.000 | 0.000 | 800/760 |
| flat/750/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.1 | 0.000 | 1.000 | 760/800 |
| flat/750/skai.roc_rider/skai.talon_archer | seat0 | eliminated | 24.5 | 0.093 | 0.000 | 800/800 |
| flat/750/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 22.4 | 0.000 | 0.113 | 800/800 |
| flat/750/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 18.1 | 1.000 | 0.000 | 800/795 |
| flat/750/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 18.1 | 0.000 | 1.000 | 795/800 |
| flat/750/skai.roc_rider/skai.windrunner | seat1 | eliminated | 20.9 | 0.000 | 0.253 | 800/750 |
| flat/750/skai.windrunner/skai.roc_rider | seat0 | eliminated | 20.7 | 0.230 | 0.000 | 750/800 |
| flat/750/skai.roc_rider/water.grasper | seat0 | eliminated | 28.0 | 1.000 | 0.000 | 800/800 |
| flat/750/water.grasper/skai.roc_rider | seat1 | eliminated | 29.2 | 0.000 | 1.000 | 800/800 |
| flat/750/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 12.7 | 1.000 | 0.000 | 800/760 |
| flat/750/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 11.4 | 0.000 | 1.000 | 760/800 |
| flat/750/skai.roc_rider/water.leviathan | seat1 | eliminated | 7.0 | 0.000 | 0.793 | 800/760 |
| flat/750/water.leviathan/skai.roc_rider | seat0 | eliminated | 7.0 | 0.793 | 0.000 | 760/800 |
| flat/750/skai.roc_rider/water.reef_runner | seat0 | eliminated | 19.4 | 1.000 | 0.000 | 800/750 |
| flat/750/water.reef_runner/skai.roc_rider | seat1 | eliminated | 19.4 | 0.000 | 1.000 | 750/800 |
| flat/750/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 36.6 | 1.000 | 0.928 | 800/700 |
| flat/750/water.shellback/skai.roc_rider | stalemate | dome_absorbed | 36.6 | 0.928 | 1.000 | 700/800 |
| flat/750/skai.roc_rider/water.spoutling | seat0 | eliminated | 11.3 | 0.719 | 0.000 | 800/750 |
| flat/750/water.spoutling/skai.roc_rider | seat1 | eliminated | 11.2 | 0.000 | 0.703 | 750/800 |
| flat/750/skai.roc_rider/water_ai_mix | seat1 | eliminated | 26.7 | 0.000 | 0.494 | 800/790 |
| flat/750/water_ai_mix/skai.roc_rider | seat1 | eliminated | 42.4 | 0.000 | 0.148 | 790/800 |
| flat/750/skai.roc_rider/skai_ai_mix | seat0 | eliminated | 25.4 | 0.242 | 0.000 | 800/815 |
| flat/750/skai_ai_mix/skai.roc_rider | seat1 | eliminated | 24.2 | 0.000 | 0.291 | 815/800 |
| flat/750/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 8.1 | 0.000 | 0.384 | 760/800 |
| flat/750/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 8.3 | 0.393 | 0.000 | 800/760 |
| flat/750/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 760/795 |
| flat/750/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 4.3 | 1.000 | 0.000 | 795/760 |
| flat/750/skai.stormcaller/skai.windrunner | seat1 | eliminated | 7.9 | 0.000 | 0.297 | 760/750 |
| flat/750/skai.windrunner/skai.stormcaller | seat0 | eliminated | 8.0 | 0.253 | 0.000 | 750/760 |
| flat/750/skai.stormcaller/water.grasper | seat1 | eliminated | 11.0 | 0.000 | 0.176 | 760/800 |
| flat/750/water.grasper/skai.stormcaller | seat0 | eliminated | 10.0 | 0.371 | 0.000 | 800/760 |
| flat/750/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 8.4 | 0.000 | 0.174 | 760/760 |
| flat/750/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 6.0 | 0.298 | 0.000 | 760/760 |
| flat/750/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.831 | 760/760 |
| flat/750/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.5 | 0.831 | 0.000 | 760/760 |
| flat/750/skai.stormcaller/water.reef_runner | seat1 | eliminated | 9.3 | 0.000 | 0.239 | 760/750 |
| flat/750/water.reef_runner/skai.stormcaller | seat0 | eliminated | 13.2 | 0.041 | 0.000 | 750/760 |
| flat/750/skai.stormcaller/water.shellback | seat1 | eliminated | 15.5 | 0.000 | 0.943 | 760/700 |
| flat/750/water.shellback/skai.stormcaller | seat0 | eliminated | 14.6 | 1.000 | 0.000 | 700/760 |
| flat/750/skai.stormcaller/water.spoutling | seat0 | eliminated | 9.4 | 0.708 | 0.000 | 760/750 |
| flat/750/water.spoutling/skai.stormcaller | seat1 | eliminated | 9.3 | 0.000 | 0.750 | 750/760 |
| flat/750/skai.stormcaller/water_ai_mix | seat1 | eliminated | 9.9 | 0.000 | 0.653 | 760/790 |
| flat/750/water_ai_mix/skai.stormcaller | seat0 | eliminated | 9.8 | 0.760 | 0.000 | 790/760 |
| flat/750/skai.stormcaller/skai_ai_mix | seat1 | eliminated | 10.1 | 0.000 | 0.599 | 760/815 |
| flat/750/skai_ai_mix/skai.stormcaller | seat0 | eliminated | 10.2 | 0.599 | 0.000 | 815/760 |
| flat/750/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 11.7 | 0.000 | 0.614 | 800/795 |
| flat/750/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 9.5 | 0.646 | 0.000 | 795/800 |
| flat/750/skai.talon_archer/skai.windrunner | seat0 | eliminated | 18.8 | 0.108 | 0.000 | 800/750 |
| flat/750/skai.windrunner/skai.talon_archer | seat1 | eliminated | 17.8 | 0.000 | 0.258 | 750/800 |
| flat/750/skai.talon_archer/water.grasper | seat1 | eliminated | 13.5 | 0.000 | 0.572 | 800/800 |
| flat/750/water.grasper/skai.talon_archer | seat0 | eliminated | 13.4 | 0.586 | 0.000 | 800/800 |
| flat/750/skai.talon_archer/water.ink_lobber | seat0 | eliminated | 11.4 | 0.237 | 0.000 | 800/760 |
| flat/750/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 9.7 | 0.000 | 0.306 | 760/800 |
| flat/750/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.9 | 0.000 | 0.847 | 800/760 |
| flat/750/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.3 | 0.857 | 0.000 | 760/800 |
| flat/750/skai.talon_archer/water.reef_runner | seat0 | eliminated | 20.8 | 0.142 | 0.000 | 800/750 |
| flat/750/water.reef_runner/skai.talon_archer | seat1 | eliminated | 25.8 | 0.000 | 0.042 | 750/800 |
| flat/750/skai.talon_archer/water.shellback | seat1 | eliminated | 23.1 | 0.000 | 0.945 | 800/700 |
| flat/750/water.shellback/skai.talon_archer | seat0 | eliminated | 23.1 | 0.905 | 0.000 | 700/800 |
| flat/750/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.0 | 0.840 | 0.000 | 800/750 |
| flat/750/water.spoutling/skai.talon_archer | seat1 | eliminated | 9.0 | 0.000 | 0.826 | 750/800 |
| flat/750/skai.talon_archer/water_ai_mix | seat1 | eliminated | 16.2 | 0.000 | 0.664 | 800/790 |
| flat/750/water_ai_mix/skai.talon_archer | seat0 | eliminated | 17.1 | 0.657 | 0.000 | 790/800 |
| flat/750/skai.talon_archer/skai_ai_mix | seat0 | eliminated | 23.9 | 0.047 | 0.000 | 800/815 |
| flat/750/skai_ai_mix/skai.talon_archer | seat1 | eliminated | 23.7 | 0.000 | 0.021 | 815/800 |
| flat/750/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.5 | 0.718 | 0.000 | 795/750 |
| flat/750/skai.windrunner/skai.thunderhead | seat1 | eliminated | 11.5 | 0.000 | 0.548 | 750/795 |
| flat/750/skai.thunderhead/water.grasper | seat0 | eliminated | 9.2 | 1.000 | 0.000 | 795/800 |
| flat/750/water.grasper/skai.thunderhead | seat1 | eliminated | 9.1 | 0.000 | 1.000 | 800/795 |
| flat/750/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 6.9 | 1.000 | 0.000 | 795/760 |
| flat/750/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 6.8 | 0.000 | 1.000 | 760/795 |
| flat/750/skai.thunderhead/water.leviathan | seat1 | eliminated | 7.0 | 0.000 | 0.554 | 795/760 |
| flat/750/water.leviathan/skai.thunderhead | seat0 | eliminated | 7.0 | 0.554 | 0.000 | 760/795 |
| flat/750/skai.thunderhead/water.reef_runner | seat0 | eliminated | 9.0 | 1.000 | 0.000 | 795/750 |
| flat/750/water.reef_runner/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 1.000 | 750/795 |
| flat/750/skai.thunderhead/water.shellback | seat0 | eliminated | 67.5 | 1.000 | 0.000 | 795/700 |
| flat/750/water.shellback/skai.thunderhead | seat1 | eliminated | 54.3 | 0.000 | 1.000 | 700/795 |
| flat/750/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.3 | 0.868 | 0.000 | 795/750 |
| flat/750/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.2 | 0.000 | 0.860 | 750/795 |
| flat/750/skai.thunderhead/water_ai_mix | seat0 | eliminated | 21.6 | 0.343 | 0.000 | 795/790 |
| flat/750/water_ai_mix/skai.thunderhead | seat1 | eliminated | 30.4 | 0.000 | 0.137 | 790/795 |
| flat/750/skai.thunderhead/skai_ai_mix | seat1 | eliminated | 51.7 | 0.000 | 0.245 | 795/815 |
| flat/750/skai_ai_mix/skai.thunderhead | seat0 | eliminated | 50.6 | 0.245 | 0.000 | 815/795 |
| flat/750/skai.windrunner/water.grasper | seat1 | eliminated | 10.0 | 0.000 | 0.662 | 750/800 |
| flat/750/water.grasper/skai.windrunner | seat0 | eliminated | 10.1 | 0.651 | 0.000 | 800/750 |
| flat/750/skai.windrunner/water.ink_lobber | seat0 | eliminated | 11.3 | 0.052 | 0.000 | 750/760 |
| flat/750/water.ink_lobber/skai.windrunner | seat1 | eliminated | 12.5 | 0.000 | 0.097 | 760/750 |
| flat/750/skai.windrunner/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.985 | 750/760 |
| flat/750/water.leviathan/skai.windrunner | seat0 | eliminated | 4.1 | 0.985 | 0.000 | 760/750 |
| flat/750/skai.windrunner/water.reef_runner | seat1 | eliminated | 14.2 | 0.000 | 0.244 | 750/750 |
| flat/750/water.reef_runner/skai.windrunner | seat0 | eliminated | 14.1 | 0.289 | 0.000 | 750/750 |
| flat/750/skai.windrunner/water.shellback | seat1 | eliminated | 18.7 | 0.000 | 0.846 | 750/700 |
| flat/750/water.shellback/skai.windrunner | seat0 | eliminated | 18.3 | 0.954 | 0.000 | 700/750 |
| flat/750/skai.windrunner/water.spoutling | seat0 | eliminated | 8.1 | 0.775 | 0.000 | 750/750 |
| flat/750/water.spoutling/skai.windrunner | seat1 | eliminated | 7.7 | 0.000 | 0.800 | 750/750 |
| flat/750/skai.windrunner/water_ai_mix | seat1 | eliminated | 12.7 | 0.000 | 0.754 | 750/790 |
| flat/750/water_ai_mix/skai.windrunner | seat0 | eliminated | 13.7 | 0.705 | 0.000 | 790/750 |
| flat/750/skai.windrunner/skai_ai_mix | seat1 | eliminated | 14.2 | 0.000 | 0.400 | 750/815 |
| flat/750/skai_ai_mix/skai.windrunner | seat0 | eliminated | 15.8 | 0.348 | 0.000 | 815/750 |
| flat/750/water.grasper/water.ink_lobber | seat1 | eliminated | 14.0 | 0.000 | 0.220 | 800/760 |
| flat/750/water.ink_lobber/water.grasper | seat0 | eliminated | 14.0 | 0.230 | 0.000 | 760/800 |
| flat/750/water.grasper/water.leviathan | seat1 | eliminated | 4.9 | 0.000 | 0.986 | 800/760 |
| flat/750/water.leviathan/water.grasper | seat0 | eliminated | 4.8 | 0.986 | 0.000 | 760/800 |
| flat/750/water.grasper/water.reef_runner | seat0 | eliminated | 11.9 | 0.591 | 0.000 | 800/750 |
| flat/750/water.reef_runner/water.grasper | seat1 | eliminated | 11.6 | 0.000 | 0.612 | 750/800 |
| flat/750/water.grasper/water.shellback | seat0 | eliminated | 17.3 | 0.653 | 0.000 | 800/700 |
| flat/750/water.shellback/water.grasper | seat1 | eliminated | 17.7 | 0.000 | 0.639 | 700/800 |
| flat/750/water.grasper/water.spoutling | seat0 | eliminated | 8.5 | 0.830 | 0.000 | 800/750 |
| flat/750/water.spoutling/water.grasper | seat1 | eliminated | 8.5 | 0.000 | 0.841 | 750/800 |
| flat/750/water.grasper/water_ai_mix | seat0 | eliminated | 18.1 | 0.270 | 0.000 | 800/790 |
| flat/750/water_ai_mix/water.grasper | seat1 | eliminated | 16.7 | 0.000 | 0.336 | 790/800 |
| flat/750/water.grasper/skai_ai_mix | seat1 | eliminated | 59.1 | 0.000 | 0.245 | 800/815 |
| flat/750/skai_ai_mix/water.grasper | seat0 | eliminated | 63.5 | 0.245 | 0.000 | 815/800 |
| flat/750/water.ink_lobber/water.leviathan | seat1 | eliminated | 5.6 | 0.000 | 0.775 | 760/760 |
| flat/750/water.leviathan/water.ink_lobber | seat0 | eliminated | 5.7 | 0.775 | 0.000 | 760/760 |
| flat/750/water.ink_lobber/water.reef_runner | seat1 | eliminated | 12.6 | 0.000 | 0.093 | 760/750 |
| flat/750/water.reef_runner/water.ink_lobber | seat1 | eliminated | 13.7 | 0.000 | 0.040 | 750/760 |
| flat/750/water.ink_lobber/water.shellback | seat1 | eliminated | 15.8 | 0.000 | 0.816 | 760/700 |
| flat/750/water.shellback/water.ink_lobber | seat0 | eliminated | 15.9 | 1.000 | 0.000 | 700/760 |
| flat/750/water.ink_lobber/water.spoutling | seat0 | eliminated | 8.1 | 0.762 | 0.000 | 760/750 |
| flat/750/water.spoutling/water.ink_lobber | seat1 | eliminated | 8.0 | 0.000 | 0.762 | 750/760 |
| flat/750/water.ink_lobber/water_ai_mix | seat1 | eliminated | 15.0 | 0.000 | 0.390 | 760/790 |
| flat/750/water_ai_mix/water.ink_lobber | seat0 | eliminated | 13.6 | 0.566 | 0.000 | 790/760 |
| flat/750/water.ink_lobber/skai_ai_mix | seat1 | eliminated | 12.6 | 0.000 | 0.474 | 760/815 |
| flat/750/skai_ai_mix/water.ink_lobber | seat0 | eliminated | 11.5 | 0.388 | 0.000 | 815/760 |
| flat/750/water.leviathan/water.reef_runner | seat0 | eliminated | 7.1 | 0.831 | 0.000 | 760/750 |
| flat/750/water.reef_runner/water.leviathan | seat1 | eliminated | 7.1 | 0.000 | 0.838 | 750/760 |
| flat/750/water.leviathan/water.shellback | seat1 | eliminated | 28.6 | 0.000 | 0.591 | 760/700 |
| flat/750/water.shellback/water.leviathan | seat0 | eliminated | 28.6 | 0.591 | 0.000 | 700/760 |
| flat/750/water.leviathan/water.spoutling | seat0 | eliminated | 4.3 | 0.938 | 0.000 | 760/750 |
| flat/750/water.spoutling/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.938 | 750/760 |
| flat/750/water.leviathan/water_ai_mix | seat0 | eliminated | 13.4 | 0.691 | 0.000 | 760/790 |
| flat/750/water_ai_mix/water.leviathan | seat1 | eliminated | 13.9 | 0.000 | 0.661 | 790/760 |
| flat/750/water.leviathan/skai_ai_mix | seat0 | eliminated | 7.0 | 0.872 | 0.000 | 760/815 |
| flat/750/skai_ai_mix/water.leviathan | seat1 | eliminated | 7.0 | 0.000 | 0.875 | 815/760 |
| flat/750/water.reef_runner/water.shellback | seat1 | eliminated | 21.3 | 0.000 | 0.847 | 750/700 |
| flat/750/water.shellback/water.reef_runner | seat0 | eliminated | 21.0 | 0.934 | 0.000 | 700/750 |
| flat/750/water.reef_runner/water.spoutling | seat0 | eliminated | 8.2 | 0.794 | 0.000 | 750/750 |
| flat/750/water.spoutling/water.reef_runner | seat1 | eliminated | 8.5 | 0.000 | 0.802 | 750/750 |
| flat/750/water.reef_runner/water_ai_mix | seat1 | eliminated | 13.7 | 0.000 | 0.728 | 750/790 |
| flat/750/water_ai_mix/water.reef_runner | seat0 | eliminated | 14.1 | 0.700 | 0.000 | 790/750 |
| flat/750/water.reef_runner/skai_ai_mix | seat1 | eliminated | 32.6 | 0.000 | 0.245 | 750/815 |
| flat/750/skai_ai_mix/water.reef_runner | seat0 | eliminated | 29.0 | 0.245 | 0.000 | 815/750 |
| flat/750/water.shellback/water.spoutling | seat0 | eliminated | 15.3 | 0.977 | 0.000 | 700/750 |
| flat/750/water.spoutling/water.shellback | seat1 | eliminated | 15.8 | 0.000 | 0.955 | 750/700 |
| flat/750/water.shellback/water_ai_mix | seat0 | eliminated | 55.4 | 0.494 | 0.000 | 700/790 |
| flat/750/water_ai_mix/water.shellback | seat1 | eliminated | 52.5 | 0.000 | 0.524 | 790/700 |
| flat/750/water.shellback/skai_ai_mix | stalemate | dome_absorbed | 47.4 | 0.867 | 0.245 | 700/815 |
| flat/750/skai_ai_mix/water.shellback | stalemate | dome_absorbed | 47.4 | 0.245 | 0.850 | 815/700 |
| flat/750/water.spoutling/water_ai_mix | seat1 | eliminated | 9.7 | 0.000 | 0.908 | 750/790 |
| flat/750/water_ai_mix/water.spoutling | seat0 | eliminated | 9.1 | 0.879 | 0.000 | 790/750 |
| flat/750/water.spoutling/skai_ai_mix | seat1 | eliminated | 7.6 | 0.000 | 0.831 | 750/815 |
| flat/750/skai_ai_mix/water.spoutling | seat0 | eliminated | 8.0 | 0.808 | 0.000 | 815/750 |
| flat/750/water_ai_mix/skai_ai_mix | seat0 | eliminated | 16.9 | 0.621 | 0.000 | 790/815 |
| flat/750/skai_ai_mix/water_ai_mix | seat1 | eliminated | 14.3 | 0.000 | 0.679 | 815/790 |
| flat/1500/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.2 | 1.000 | 0.000 | 1600/1520 |
| flat/1500/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.4 | 0.000 | 1.000 | 1520/1600 |
| flat/1500/skai.roc_rider/skai.talon_archer | seat0 | eliminated | 22.1 | 0.227 | 0.000 | 1600/1500 |
| flat/1500/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 22.3 | 0.000 | 0.225 | 1500/1600 |
| flat/1500/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 18.2 | 1.000 | 0.000 | 1600/1590 |
| flat/1500/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 18.2 | 0.000 | 1.000 | 1590/1600 |
| flat/1500/skai.roc_rider/skai.windrunner | seat1 | eliminated | 23.6 | 0.000 | 0.157 | 1600/1500 |
| flat/1500/skai.windrunner/skai.roc_rider | seat0 | eliminated | 19.9 | 0.330 | 0.000 | 1500/1600 |
| flat/1500/skai.roc_rider/water.grasper | seat0 | eliminated | 29.1 | 1.000 | 0.000 | 1600/1500 |
| flat/1500/water.grasper/skai.roc_rider | seat1 | eliminated | 30.3 | 0.000 | 1.000 | 1500/1600 |
| flat/1500/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 11.5 | 1.000 | 0.000 | 1600/1520 |
| flat/1500/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 10.8 | 0.000 | 1.000 | 1520/1600 |
| flat/1500/skai.roc_rider/water.leviathan | seat1 | eliminated | 4.5 | 0.000 | 0.906 | 1600/1520 |
| flat/1500/water.leviathan/skai.roc_rider | seat0 | eliminated | 4.5 | 0.906 | 0.000 | 1520/1600 |
| flat/1500/skai.roc_rider/water.reef_runner | seat0 | eliminated | 20.6 | 1.000 | 0.000 | 1600/1500 |
| flat/1500/water.reef_runner/skai.roc_rider | seat1 | eliminated | 20.6 | 0.000 | 1.000 | 1500/1600 |
| flat/1500/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 58.2 | 1.000 | 0.939 | 1600/1575 |
| flat/1500/water.shellback/skai.roc_rider | unresolved | time_cap | 180.0 | 0.693 | 1.000 | 1575/1600 |
| flat/1500/skai.roc_rider/water.spoutling | seat0 | eliminated | 11.2 | 0.750 | 0.000 | 1600/1500 |
| flat/1500/water.spoutling/skai.roc_rider | seat1 | eliminated | 11.7 | 0.000 | 0.719 | 1500/1600 |
| flat/1500/skai.roc_rider/water_ai_mix | seat1 | eliminated | 24.4 | 0.000 | 0.709 | 1600/1580 |
| flat/1500/water_ai_mix/skai.roc_rider | seat0 | eliminated | 33.4 | 0.442 | 0.000 | 1580/1600 |
| flat/1500/skai.roc_rider/skai_ai_mix | seat0 | eliminated | 18.2 | 0.412 | 0.000 | 1600/1630 |
| flat/1500/skai_ai_mix/skai.roc_rider | seat1 | eliminated | 19.6 | 0.000 | 0.413 | 1630/1600 |
| flat/1500/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 11.3 | 0.000 | 0.252 | 1520/1500 |
| flat/1500/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 11.6 | 0.194 | 0.000 | 1500/1520 |
| flat/1500/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1520/1590 |
| flat/1500/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 1590/1520 |
| flat/1500/skai.stormcaller/skai.windrunner | seat1 | eliminated | 10.2 | 0.000 | 0.164 | 1520/1500 |
| flat/1500/skai.windrunner/skai.stormcaller | seat0 | eliminated | 9.3 | 0.303 | 0.000 | 1500/1520 |
| flat/1500/skai.stormcaller/water.grasper | seat1 | eliminated | 12.7 | 0.000 | 0.194 | 1520/1500 |
| flat/1500/water.grasper/skai.stormcaller | seat1 | eliminated | 14.3 | 0.000 | 0.150 | 1500/1520 |
| flat/1500/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.1 | 0.000 | 0.181 | 1520/1520 |
| flat/1500/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 8.8 | 0.372 | 0.000 | 1520/1520 |
| flat/1500/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.917 | 1520/1520 |
| flat/1500/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.3 | 0.959 | 0.000 | 1520/1520 |
| flat/1500/skai.stormcaller/water.reef_runner | seat1 | eliminated | 12.1 | 0.000 | 0.091 | 1520/1500 |
| flat/1500/water.reef_runner/skai.stormcaller | seat0 | eliminated | 13.6 | 0.086 | 0.000 | 1500/1520 |
| flat/1500/skai.stormcaller/water.shellback | seat1 | eliminated | 14.8 | 0.000 | 1.000 | 1520/1575 |
| flat/1500/water.shellback/skai.stormcaller | seat0 | eliminated | 15.6 | 1.000 | 0.000 | 1575/1520 |
| flat/1500/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.1 | 0.840 | 0.000 | 1520/1500 |
| flat/1500/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.2 | 0.000 | 0.840 | 1500/1520 |
| flat/1500/skai.stormcaller/water_ai_mix | seat1 | eliminated | 10.5 | 0.000 | 0.752 | 1520/1580 |
| flat/1500/water_ai_mix/skai.stormcaller | seat0 | eliminated | 15.6 | 0.545 | 0.000 | 1580/1520 |
| flat/1500/skai.stormcaller/skai_ai_mix | seat1 | eliminated | 10.2 | 0.000 | 0.667 | 1520/1630 |
| flat/1500/skai_ai_mix/skai.stormcaller | seat0 | eliminated | 11.3 | 0.499 | 0.000 | 1630/1520 |
| flat/1500/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 7.4 | 0.000 | 0.691 | 1500/1590 |
| flat/1500/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 9.1 | 0.719 | 0.000 | 1590/1500 |
| flat/1500/skai.talon_archer/skai.windrunner | seat1 | eliminated | 20.2 | 0.000 | 0.230 | 1500/1500 |
| flat/1500/skai.windrunner/skai.talon_archer | seat0 | eliminated | 19.0 | 0.230 | 0.000 | 1500/1500 |
| flat/1500/skai.talon_archer/water.grasper | seat1 | eliminated | 14.9 | 0.000 | 0.565 | 1500/1500 |
| flat/1500/water.grasper/skai.talon_archer | seat0 | eliminated | 14.6 | 0.565 | 0.000 | 1500/1500 |
| flat/1500/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 20.7 | 0.000 | 0.033 | 1500/1520 |
| flat/1500/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 14.3 | 0.349 | 0.000 | 1520/1500 |
| flat/1500/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.959 | 1500/1520 |
| flat/1500/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.980 | 0.000 | 1520/1500 |
| flat/1500/skai.talon_archer/water.reef_runner | seat1 | eliminated | 18.0 | 0.000 | 0.326 | 1500/1500 |
| flat/1500/water.reef_runner/skai.talon_archer | seat0 | eliminated | 18.6 | 0.289 | 0.000 | 1500/1500 |
| flat/1500/skai.talon_archer/water.shellback | seat1 | eliminated | 20.9 | 0.000 | 0.921 | 1500/1575 |
| flat/1500/water.shellback/skai.talon_archer | seat0 | eliminated | 21.0 | 0.994 | 0.000 | 1575/1500 |
| flat/1500/skai.talon_archer/water.spoutling | seat0 | eliminated | 9.9 | 0.796 | 0.000 | 1500/1500 |
| flat/1500/water.spoutling/skai.talon_archer | seat1 | eliminated | 9.9 | 0.000 | 0.826 | 1500/1500 |
| flat/1500/skai.talon_archer/water_ai_mix | seat1 | eliminated | 14.7 | 0.000 | 0.776 | 1500/1580 |
| flat/1500/water_ai_mix/skai.talon_archer | seat0 | eliminated | 16.2 | 0.714 | 0.000 | 1580/1500 |
| flat/1500/skai.talon_archer/skai_ai_mix | seat1 | eliminated | 18.3 | 0.000 | 0.331 | 1500/1630 |
| flat/1500/skai_ai_mix/skai.talon_archer | seat0 | eliminated | 19.3 | 0.335 | 0.000 | 1630/1500 |
| flat/1500/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.5 | 0.805 | 0.000 | 1590/1500 |
| flat/1500/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.4 | 0.000 | 0.809 | 1500/1590 |
| flat/1500/skai.thunderhead/water.grasper | seat0 | eliminated | 8.9 | 1.000 | 0.000 | 1590/1500 |
| flat/1500/water.grasper/skai.thunderhead | seat1 | eliminated | 6.7 | 0.000 | 1.000 | 1500/1590 |
| flat/1500/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 4.5 | 1.000 | 0.000 | 1590/1520 |
| flat/1500/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1520/1590 |
| flat/1500/skai.thunderhead/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 0.785 | 1590/1520 |
| flat/1500/water.leviathan/skai.thunderhead | seat0 | eliminated | 4.3 | 0.785 | 0.000 | 1520/1590 |
| flat/1500/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 1590/1500 |
| flat/1500/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.6 | 0.000 | 1.000 | 1500/1590 |
| flat/1500/skai.thunderhead/water.shellback | seat0 | eliminated | 39.4 | 1.000 | 0.000 | 1590/1575 |
| flat/1500/water.shellback/skai.thunderhead | seat1 | eliminated | 41.7 | 0.000 | 1.000 | 1575/1590 |
| flat/1500/skai.thunderhead/water.spoutling | seat0 | eliminated | 4.4 | 0.886 | 0.000 | 1590/1500 |
| flat/1500/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.4 | 0.000 | 0.904 | 1500/1590 |
| flat/1500/skai.thunderhead/water_ai_mix | seat0 | eliminated | 14.3 | 0.645 | 0.000 | 1590/1580 |
| flat/1500/water_ai_mix/skai.thunderhead | seat1 | eliminated | 16.4 | 0.000 | 0.567 | 1580/1590 |
| flat/1500/skai.thunderhead/skai_ai_mix | seat1 | eliminated | 60.1 | 0.000 | 0.245 | 1590/1630 |
| flat/1500/skai_ai_mix/skai.thunderhead | seat0 | eliminated | 57.6 | 0.245 | 0.000 | 1630/1590 |
| flat/1500/skai.windrunner/water.grasper | seat1 | eliminated | 11.9 | 0.000 | 0.609 | 1500/1500 |
| flat/1500/water.grasper/skai.windrunner | seat0 | eliminated | 11.8 | 0.598 | 0.000 | 1500/1500 |
| flat/1500/skai.windrunner/water.ink_lobber | seat1 | eliminated | 13.7 | 0.000 | 0.363 | 1500/1520 |
| flat/1500/water.ink_lobber/skai.windrunner | seat0 | eliminated | 11.0 | 0.359 | 0.000 | 1520/1500 |
| flat/1500/skai.windrunner/water.leviathan | seat1 | eliminated | 2.2 | 0.000 | 1.000 | 1500/1520 |
| flat/1500/water.leviathan/skai.windrunner | seat0 | eliminated | 2.3 | 1.000 | 0.000 | 1520/1500 |
| flat/1500/skai.windrunner/water.reef_runner | seat1 | eliminated | 16.0 | 0.000 | 0.268 | 1500/1500 |
| flat/1500/water.reef_runner/skai.windrunner | seat0 | eliminated | 16.5 | 0.218 | 0.000 | 1500/1500 |
| flat/1500/skai.windrunner/water.shellback | seat1 | eliminated | 17.8 | 0.000 | 0.907 | 1500/1575 |
| flat/1500/water.shellback/skai.windrunner | seat0 | eliminated | 17.8 | 0.998 | 0.000 | 1575/1500 |
| flat/1500/skai.windrunner/water.spoutling | seat0 | eliminated | 8.6 | 0.787 | 0.000 | 1500/1500 |
| flat/1500/water.spoutling/skai.windrunner | seat1 | eliminated | 8.7 | 0.000 | 0.792 | 1500/1500 |
| flat/1500/skai.windrunner/water_ai_mix | seat1 | eliminated | 12.4 | 0.000 | 0.773 | 1500/1580 |
| flat/1500/water_ai_mix/skai.windrunner | seat0 | eliminated | 13.4 | 0.761 | 0.000 | 1580/1500 |
| flat/1500/skai.windrunner/skai_ai_mix | seat1 | eliminated | 14.2 | 0.000 | 0.420 | 1500/1630 |
| flat/1500/skai_ai_mix/skai.windrunner | seat0 | eliminated | 13.9 | 0.424 | 0.000 | 1630/1500 |
| flat/1500/water.grasper/water.ink_lobber | seat1 | eliminated | 13.9 | 0.000 | 0.350 | 1500/1520 |
| flat/1500/water.ink_lobber/water.grasper | seat0 | eliminated | 14.1 | 0.285 | 0.000 | 1520/1500 |
| flat/1500/water.grasper/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 1.000 | 1500/1520 |
| flat/1500/water.leviathan/water.grasper | seat0 | eliminated | 4.3 | 1.000 | 0.000 | 1520/1500 |
| flat/1500/water.grasper/water.reef_runner | seat0 | eliminated | 13.7 | 0.541 | 0.000 | 1500/1500 |
| flat/1500/water.reef_runner/water.grasper | seat1 | eliminated | 13.7 | 0.000 | 0.552 | 1500/1500 |
| flat/1500/water.grasper/water.shellback | seat0 | eliminated | 23.5 | 0.524 | 0.000 | 1500/1575 |
| flat/1500/water.shellback/water.grasper | seat1 | eliminated | 23.8 | 0.000 | 0.511 | 1575/1500 |
| flat/1500/water.grasper/water.spoutling | seat0 | eliminated | 9.5 | 0.800 | 0.000 | 1500/1500 |
| flat/1500/water.spoutling/water.grasper | seat1 | eliminated | 9.3 | 0.000 | 0.803 | 1500/1500 |
| flat/1500/water.grasper/water_ai_mix | seat1 | eliminated | 26.0 | 0.000 | 0.154 | 1500/1580 |
| flat/1500/water_ai_mix/water.grasper | seat0 | eliminated | 22.9 | 0.247 | 0.000 | 1580/1500 |
| flat/1500/water.grasper/skai_ai_mix | seat1 | eliminated | 57.3 | 0.000 | 0.245 | 1500/1630 |
| flat/1500/skai_ai_mix/water.grasper | seat0 | eliminated | 53.6 | 0.245 | 0.000 | 1630/1500 |
| flat/1500/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.6 | 0.000 | 0.803 | 1520/1520 |
| flat/1500/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.7 | 0.803 | 0.000 | 1520/1520 |
| flat/1500/water.ink_lobber/water.reef_runner | seat0 | eliminated | 11.1 | 0.385 | 0.000 | 1520/1500 |
| flat/1500/water.reef_runner/water.ink_lobber | seat1 | eliminated | 10.8 | 0.000 | 0.440 | 1500/1520 |
| flat/1500/water.ink_lobber/water.shellback | seat1 | eliminated | 15.8 | 0.000 | 0.903 | 1520/1575 |
| flat/1500/water.shellback/water.ink_lobber | seat0 | eliminated | 15.6 | 1.000 | 0.000 | 1575/1520 |
| flat/1500/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.9 | 0.862 | 0.000 | 1520/1500 |
| flat/1500/water.spoutling/water.ink_lobber | seat1 | eliminated | 8.0 | 0.000 | 0.850 | 1500/1520 |
| flat/1500/water.ink_lobber/water_ai_mix | seat1 | eliminated | 14.9 | 0.000 | 0.539 | 1520/1580 |
| flat/1500/water_ai_mix/water.ink_lobber | seat0 | eliminated | 15.2 | 0.548 | 0.000 | 1580/1520 |
| flat/1500/water.ink_lobber/skai_ai_mix | seat1 | eliminated | 11.6 | 0.000 | 0.485 | 1520/1630 |
| flat/1500/skai_ai_mix/water.ink_lobber | seat0 | eliminated | 17.5 | 0.245 | 0.000 | 1630/1520 |
| flat/1500/water.leviathan/water.reef_runner | seat0 | eliminated | 4.2 | 0.961 | 0.000 | 1520/1500 |
| flat/1500/water.reef_runner/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.954 | 1500/1520 |
| flat/1500/water.leviathan/water.shellback | seat1 | eliminated | 27.1 | 0.000 | 0.682 | 1520/1575 |
| flat/1500/water.shellback/water.leviathan | seat0 | eliminated | 27.9 | 0.606 | 0.000 | 1575/1520 |
| flat/1500/water.leviathan/water.spoutling | seat0 | eliminated | 4.1 | 0.992 | 0.000 | 1520/1500 |
| flat/1500/water.spoutling/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.992 | 1500/1520 |
| flat/1500/water.leviathan/water_ai_mix | seat0 | eliminated | 10.8 | 0.764 | 0.000 | 1520/1580 |
| flat/1500/water_ai_mix/water.leviathan | seat1 | eliminated | 10.3 | 0.000 | 0.803 | 1580/1520 |
| flat/1500/water.leviathan/skai_ai_mix | seat0 | eliminated | 5.2 | 0.909 | 0.000 | 1520/1630 |
| flat/1500/skai_ai_mix/water.leviathan | seat1 | eliminated | 5.1 | 0.000 | 0.908 | 1630/1520 |
| flat/1500/water.reef_runner/water.shellback | seat1 | eliminated | 20.8 | 0.000 | 0.936 | 1500/1575 |
| flat/1500/water.shellback/water.reef_runner | seat0 | eliminated | 20.3 | 0.984 | 0.000 | 1575/1500 |
| flat/1500/water.reef_runner/water.spoutling | seat0 | eliminated | 9.0 | 0.802 | 0.000 | 1500/1500 |
| flat/1500/water.spoutling/water.reef_runner | seat1 | eliminated | 8.7 | 0.000 | 0.816 | 1500/1500 |
| flat/1500/water.reef_runner/water_ai_mix | seat1 | eliminated | 13.8 | 0.000 | 0.764 | 1500/1580 |
| flat/1500/water_ai_mix/water.reef_runner | seat0 | eliminated | 14.8 | 0.687 | 0.000 | 1580/1500 |
| flat/1500/water.reef_runner/skai_ai_mix | seat1 | eliminated | 31.4 | 0.000 | 0.245 | 1500/1630 |
| flat/1500/skai_ai_mix/water.reef_runner | seat0 | eliminated | 24.2 | 0.245 | 0.000 | 1630/1500 |
| flat/1500/water.shellback/water.spoutling | seat0 | eliminated | 14.7 | 0.997 | 0.000 | 1575/1500 |
| flat/1500/water.spoutling/water.shellback | seat1 | eliminated | 14.8 | 0.000 | 0.975 | 1500/1575 |
| flat/1500/water.shellback/water_ai_mix | seat0 | eliminated | 43.9 | 0.604 | 0.000 | 1575/1580 |
| flat/1500/water_ai_mix/water.shellback | seat1 | eliminated | 43.9 | 0.000 | 0.655 | 1580/1575 |
| flat/1500/water.shellback/skai_ai_mix | stalemate | dome_absorbed | 46.1 | 0.941 | 0.245 | 1575/1630 |
| flat/1500/skai_ai_mix/water.shellback | stalemate | dome_absorbed | 46.2 | 0.245 | 0.917 | 1630/1575 |
| flat/1500/water.spoutling/water_ai_mix | seat1 | eliminated | 9.3 | 0.000 | 0.928 | 1500/1580 |
| flat/1500/water_ai_mix/water.spoutling | seat0 | eliminated | 9.9 | 0.859 | 0.000 | 1580/1500 |
| flat/1500/water.spoutling/skai_ai_mix | seat1 | eliminated | 8.7 | 0.000 | 0.824 | 1500/1630 |
| flat/1500/skai_ai_mix/water.spoutling | seat0 | eliminated | 9.1 | 0.816 | 0.000 | 1630/1500 |
| flat/1500/water_ai_mix/skai_ai_mix | seat0 | eliminated | 17.2 | 0.579 | 0.000 | 1580/1630 |
| flat/1500/skai_ai_mix/water_ai_mix | seat1 | eliminated | 15.3 | 0.000 | 0.635 | 1630/1580 |
| high_ground/1500/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.2 | 1.000 | 0.000 | 1600/1520 |
| high_ground/1500/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 13.9 | 0.000 | 1.000 | 1520/1600 |
| high_ground/1500/skai.roc_rider/skai.talon_archer | seat0 | eliminated | 29.0 | 0.206 | 0.000 | 1600/1500 |
| high_ground/1500/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 30.4 | 0.000 | 0.194 | 1500/1600 |
| high_ground/1500/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 18.8 | 1.000 | 0.000 | 1600/1590 |
| high_ground/1500/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 19.4 | 0.000 | 1.000 | 1590/1600 |
| high_ground/1500/skai.roc_rider/skai.windrunner | seat0 | eliminated | 18.4 | 0.880 | 0.000 | 1600/1500 |
| high_ground/1500/skai.windrunner/skai.roc_rider | seat0 | eliminated | 21.8 | 0.327 | 0.000 | 1500/1600 |
| high_ground/1500/skai.roc_rider/water.grasper | seat0 | eliminated | 27.6 | 1.000 | 0.000 | 1600/1500 |
| high_ground/1500/water.grasper/skai.roc_rider | seat1 | eliminated | 30.1 | 0.000 | 1.000 | 1500/1600 |
| high_ground/1500/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 11.0 | 1.000 | 0.000 | 1600/1520 |
| high_ground/1500/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 16.2 | 0.000 | 1.000 | 1520/1600 |
| high_ground/1500/skai.roc_rider/water.leviathan | seat1 | eliminated | 7.3 | 0.000 | 0.851 | 1600/1520 |
| high_ground/1500/water.leviathan/skai.roc_rider | seat0 | eliminated | 18.3 | 0.668 | 0.000 | 1520/1600 |
| high_ground/1500/skai.roc_rider/water.reef_runner | seat0 | eliminated | 19.6 | 1.000 | 0.000 | 1600/1500 |
| high_ground/1500/water.reef_runner/skai.roc_rider | seat1 | eliminated | 18.4 | 0.000 | 1.000 | 1500/1600 |
| high_ground/1500/skai.roc_rider/water.shellback | seat0 | eliminated | 85.5 | 1.000 | 0.000 | 1600/1575 |
| high_ground/1500/water.shellback/skai.roc_rider | seat1 | eliminated | 107.1 | 0.000 | 1.000 | 1575/1600 |
| high_ground/1500/skai.roc_rider/water.spoutling | seat0 | eliminated | 12.5 | 0.758 | 0.000 | 1600/1500 |
| high_ground/1500/water.spoutling/skai.roc_rider | seat1 | eliminated | 13.3 | 0.000 | 0.823 | 1500/1600 |
| high_ground/1500/skai.roc_rider/water_ai_mix | seat0 | eliminated | 30.6 | 0.961 | 0.000 | 1600/1580 |
| high_ground/1500/water_ai_mix/skai.roc_rider | seat0 | eliminated | 40.3 | 0.436 | 0.000 | 1580/1600 |
| high_ground/1500/skai.roc_rider/skai_ai_mix | seat0 | eliminated | 20.4 | 0.574 | 0.000 | 1600/1630 |
| high_ground/1500/skai_ai_mix/skai.roc_rider | seat0 | eliminated | 39.0 | 0.149 | 0.000 | 1630/1600 |
| high_ground/1500/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 11.2 | 0.000 | 0.592 | 1520/1500 |
| high_ground/1500/skai.talon_archer/skai.stormcaller | seat1 | eliminated | 28.3 | 0.000 | 0.015 | 1500/1520 |
| high_ground/1500/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 9.3 | 0.000 | 1.000 | 1520/1590 |
| high_ground/1500/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 8.0 | 1.000 | 0.000 | 1590/1520 |
| high_ground/1500/skai.stormcaller/skai.windrunner | seat1 | eliminated | 19.3 | 0.000 | 0.344 | 1520/1500 |
| high_ground/1500/skai.windrunner/skai.stormcaller | seat1 | eliminated | 15.1 | 0.000 | 0.191 | 1500/1520 |
| high_ground/1500/skai.stormcaller/water.grasper | seat0 | eliminated | 20.2 | 1.000 | 0.000 | 1520/1500 |
| high_ground/1500/water.grasper/skai.stormcaller | seat1 | eliminated | 18.4 | 0.000 | 0.125 | 1500/1520 |
| high_ground/1500/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.9 | 0.000 | 0.637 | 1520/1520 |
| high_ground/1500/water.ink_lobber/skai.stormcaller | seat1 | eliminated | 14.9 | 0.000 | 0.387 | 1520/1520 |
| high_ground/1500/skai.stormcaller/water.leviathan | seat1 | eliminated | 5.2 | 0.000 | 1.000 | 1520/1520 |
| high_ground/1500/water.leviathan/skai.stormcaller | seat0 | eliminated | 17.3 | 0.500 | 0.000 | 1520/1520 |
| high_ground/1500/skai.stormcaller/water.reef_runner | seat1 | eliminated | 17.4 | 0.000 | 0.430 | 1520/1500 |
| high_ground/1500/water.reef_runner/skai.stormcaller | seat0 | eliminated | 19.5 | 0.029 | 0.000 | 1500/1520 |
| high_ground/1500/skai.stormcaller/water.shellback | stalemate | dome_absorbed | 30.0 | 1.000 | 1.000 | 1520/1575 |
| high_ground/1500/water.shellback/skai.stormcaller | seat0 | eliminated | 18.1 | 1.000 | 0.000 | 1575/1520 |
| high_ground/1500/skai.stormcaller/water.spoutling | seat0 | eliminated | 25.2 | 0.229 | 0.000 | 1520/1500 |
| high_ground/1500/water.spoutling/skai.stormcaller | seat1 | eliminated | 10.4 | 0.000 | 0.847 | 1500/1520 |
| high_ground/1500/skai.stormcaller/water_ai_mix | seat0 | eliminated | 37.7 | 0.440 | 0.000 | 1520/1580 |
| high_ground/1500/water_ai_mix/skai.stormcaller | seat0 | eliminated | 21.3 | 0.448 | 0.000 | 1580/1520 |
| high_ground/1500/skai.stormcaller/skai_ai_mix | seat1 | eliminated | 12.5 | 0.000 | 0.563 | 1520/1630 |
| high_ground/1500/skai_ai_mix/skai.stormcaller | seat0 | eliminated | 17.4 | 0.260 | 0.000 | 1630/1520 |
| high_ground/1500/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 11.7 | 0.000 | 0.715 | 1500/1590 |
| high_ground/1500/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 13.0 | 0.519 | 0.000 | 1590/1500 |
| high_ground/1500/skai.talon_archer/skai.windrunner | seat0 | eliminated | 23.5 | 0.529 | 0.000 | 1500/1500 |
| high_ground/1500/skai.windrunner/skai.talon_archer | seat1 | eliminated | 20.6 | 0.000 | 0.254 | 1500/1500 |
| high_ground/1500/skai.talon_archer/water.grasper | seat0 | eliminated | 29.2 | 1.000 | 0.000 | 1500/1500 |
| high_ground/1500/water.grasper/skai.talon_archer | seat0 | eliminated | 17.0 | 0.539 | 0.000 | 1500/1500 |
| high_ground/1500/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 15.2 | 0.000 | 0.476 | 1500/1520 |
| high_ground/1500/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 12.7 | 0.000 | 0.555 | 1520/1500 |
| high_ground/1500/skai.talon_archer/water.leviathan | seat1 | eliminated | 7.9 | 0.000 | 0.989 | 1500/1520 |
| high_ground/1500/water.leviathan/skai.talon_archer | seat0 | eliminated | 17.7 | 0.609 | 0.000 | 1520/1500 |
| high_ground/1500/skai.talon_archer/water.reef_runner | seat0 | eliminated | 26.2 | 0.516 | 0.000 | 1500/1500 |
| high_ground/1500/water.reef_runner/skai.talon_archer | seat1 | eliminated | 22.7 | 0.000 | 0.274 | 1500/1500 |
| high_ground/1500/skai.talon_archer/water.shellback | seat0 | eliminated | 169.2 | 1.000 | 0.000 | 1500/1575 |
| high_ground/1500/water.shellback/skai.talon_archer | seat0 | eliminated | 26.1 | 0.988 | 0.000 | 1575/1500 |
| high_ground/1500/skai.talon_archer/water.spoutling | seat0 | eliminated | 15.3 | 0.715 | 0.000 | 1500/1500 |
| high_ground/1500/water.spoutling/skai.talon_archer | seat1 | eliminated | 11.3 | 0.000 | 0.890 | 1500/1500 |
| high_ground/1500/skai.talon_archer/water_ai_mix | seat1 | eliminated | 39.1 | 0.000 | 0.491 | 1500/1580 |
| high_ground/1500/water_ai_mix/skai.talon_archer | seat0 | eliminated | 26.2 | 0.554 | 0.000 | 1580/1500 |
| high_ground/1500/skai.talon_archer/skai_ai_mix | seat1 | eliminated | 18.5 | 0.000 | 0.487 | 1500/1630 |
| high_ground/1500/skai_ai_mix/skai.talon_archer | seat1 | eliminated | 19.7 | 0.000 | 0.247 | 1630/1500 |
| high_ground/1500/skai.thunderhead/skai.windrunner | seat0 | eliminated | 8.6 | 0.925 | 0.000 | 1590/1500 |
| high_ground/1500/skai.windrunner/skai.thunderhead | seat1 | eliminated | 8.1 | 0.000 | 0.721 | 1500/1590 |
| high_ground/1500/skai.thunderhead/water.grasper | seat0 | eliminated | 10.6 | 1.000 | 0.000 | 1590/1500 |
| high_ground/1500/water.grasper/skai.thunderhead | seat1 | eliminated | 11.2 | 0.000 | 1.000 | 1500/1590 |
| high_ground/1500/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 8.0 | 1.000 | 0.000 | 1590/1520 |
| high_ground/1500/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 10.4 | 0.000 | 1.000 | 1520/1590 |
| high_ground/1500/skai.thunderhead/water.leviathan | seat1 | eliminated | 7.2 | 0.000 | 0.629 | 1590/1520 |
| high_ground/1500/water.leviathan/skai.thunderhead | seat1 | eliminated | 18.7 | 0.000 | 0.289 | 1520/1590 |
| high_ground/1500/skai.thunderhead/water.reef_runner | seat0 | eliminated | 8.6 | 1.000 | 0.000 | 1590/1500 |
| high_ground/1500/water.reef_runner/skai.thunderhead | seat1 | eliminated | 8.3 | 0.000 | 1.000 | 1500/1590 |
| high_ground/1500/skai.thunderhead/water.shellback | seat0 | eliminated | 32.8 | 1.000 | 0.000 | 1590/1575 |
| high_ground/1500/water.shellback/skai.thunderhead | seat1 | eliminated | 40.9 | 0.000 | 1.000 | 1575/1590 |
| high_ground/1500/skai.thunderhead/water.spoutling | seat0 | eliminated | 8.0 | 0.855 | 0.000 | 1590/1500 |
| high_ground/1500/water.spoutling/skai.thunderhead | seat1 | eliminated | 8.7 | 0.000 | 0.914 | 1500/1590 |
| high_ground/1500/skai.thunderhead/water_ai_mix | seat0 | eliminated | 12.5 | 0.902 | 0.000 | 1590/1580 |
| high_ground/1500/water_ai_mix/skai.thunderhead | seat1 | eliminated | 16.3 | 0.000 | 0.833 | 1580/1590 |
| high_ground/1500/skai.thunderhead/skai_ai_mix | seat1 | eliminated | 61.4 | 0.000 | 0.245 | 1590/1630 |
| high_ground/1500/skai_ai_mix/skai.thunderhead | seat0 | eliminated | 32.6 | 0.384 | 0.000 | 1630/1590 |
| high_ground/1500/skai.windrunner/water.grasper | seat0 | eliminated | 20.4 | 1.000 | 0.000 | 1500/1500 |
| high_ground/1500/water.grasper/skai.windrunner | seat0 | eliminated | 15.4 | 0.572 | 0.000 | 1500/1500 |
| high_ground/1500/skai.windrunner/water.ink_lobber | seat1 | eliminated | 11.5 | 0.000 | 0.534 | 1500/1520 |
| high_ground/1500/water.ink_lobber/skai.windrunner | seat0 | eliminated | 39.8 | 0.360 | 0.000 | 1520/1500 |
| high_ground/1500/skai.windrunner/water.leviathan | seat1 | eliminated | 2.2 | 0.000 | 1.000 | 1500/1520 |
| high_ground/1500/water.leviathan/skai.windrunner | seat0 | eliminated | 14.7 | 0.786 | 0.000 | 1520/1500 |
| high_ground/1500/skai.windrunner/water.reef_runner | seat0 | eliminated | 29.1 | 0.060 | 0.000 | 1500/1500 |
| high_ground/1500/water.reef_runner/skai.windrunner | seat0 | eliminated | 25.2 | 0.127 | 0.000 | 1500/1500 |
| high_ground/1500/skai.windrunner/water.shellback | seat0 | eliminated | 111.4 | 0.850 | 0.000 | 1500/1575 |
| high_ground/1500/water.shellback/skai.windrunner | seat0 | eliminated | 24.5 | 0.984 | 0.000 | 1575/1500 |
| high_ground/1500/skai.windrunner/water.spoutling | seat0 | eliminated | 11.2 | 0.729 | 0.000 | 1500/1500 |
| high_ground/1500/water.spoutling/skai.windrunner | seat1 | eliminated | 15.5 | 0.000 | 0.784 | 1500/1500 |
| high_ground/1500/skai.windrunner/water_ai_mix | seat1 | eliminated | 29.3 | 0.000 | 0.592 | 1500/1580 |
| high_ground/1500/water_ai_mix/skai.windrunner | seat0 | eliminated | 26.4 | 0.491 | 0.000 | 1580/1500 |
| high_ground/1500/skai.windrunner/skai_ai_mix | seat1 | eliminated | 16.1 | 0.000 | 0.438 | 1500/1630 |
| high_ground/1500/skai_ai_mix/skai.windrunner | seat0 | eliminated | 18.2 | 0.720 | 0.000 | 1630/1500 |
| high_ground/1500/water.grasper/water.ink_lobber | seat1 | eliminated | 17.8 | 0.000 | 0.220 | 1500/1520 |
| high_ground/1500/water.ink_lobber/water.grasper | seat0 | eliminated | 18.6 | 1.000 | 0.000 | 1520/1500 |
| high_ground/1500/water.grasper/water.leviathan | seat1 | eliminated | 5.8 | 0.000 | 1.000 | 1500/1520 |
| high_ground/1500/water.leviathan/water.grasper | seat0 | eliminated | 14.7 | 1.000 | 0.000 | 1520/1500 |
| high_ground/1500/water.grasper/water.reef_runner | seat0 | eliminated | 18.9 | 0.468 | 0.000 | 1500/1500 |
| high_ground/1500/water.reef_runner/water.grasper | seat0 | eliminated | 21.8 | 1.000 | 0.000 | 1500/1500 |
| high_ground/1500/water.grasper/water.shellback | seat0 | eliminated | 26.2 | 0.535 | 0.000 | 1500/1575 |
| high_ground/1500/water.shellback/water.grasper | seat0 | eliminated | 41.4 | 0.569 | 0.000 | 1575/1500 |
| high_ground/1500/water.grasper/water.spoutling | seat0 | eliminated | 12.5 | 0.791 | 0.000 | 1500/1500 |
| high_ground/1500/water.spoutling/water.grasper | seat0 | eliminated | 41.5 | 1.000 | 0.000 | 1500/1500 |
| high_ground/1500/water.grasper/water_ai_mix | seat0 | eliminated | 31.3 | 0.090 | 0.000 | 1500/1580 |
| high_ground/1500/water_ai_mix/water.grasper | seat0 | eliminated | 25.7 | 0.654 | 0.000 | 1580/1500 |
| high_ground/1500/water.grasper/skai_ai_mix | seat1 | eliminated | 59.9 | 0.000 | 0.245 | 1500/1630 |
| high_ground/1500/skai_ai_mix/water.grasper | seat0 | eliminated | 20.8 | 1.000 | 0.000 | 1630/1500 |
| high_ground/1500/water.ink_lobber/water.leviathan | seat1 | eliminated | 6.2 | 0.000 | 1.000 | 1520/1520 |
| high_ground/1500/water.leviathan/water.ink_lobber | seat0 | eliminated | 27.9 | 0.109 | 0.000 | 1520/1520 |
| high_ground/1500/water.ink_lobber/water.reef_runner | seat0 | eliminated | 39.8 | 0.360 | 0.000 | 1520/1500 |
| high_ground/1500/water.reef_runner/water.ink_lobber | seat1 | eliminated | 11.2 | 0.000 | 0.582 | 1500/1520 |
| high_ground/1500/water.ink_lobber/water.shellback | seat0 | eliminated | 78.6 | 1.000 | 0.000 | 1520/1575 |
| high_ground/1500/water.shellback/water.ink_lobber | seat0 | eliminated | 19.0 | 1.000 | 0.000 | 1575/1520 |
| high_ground/1500/water.ink_lobber/water.spoutling | seat0 | eliminated | 24.7 | 0.344 | 0.000 | 1520/1500 |
| high_ground/1500/water.spoutling/water.ink_lobber | seat1 | eliminated | 9.2 | 0.000 | 0.897 | 1500/1520 |
| high_ground/1500/water.ink_lobber/water_ai_mix | seat0 | eliminated | 39.6 | 0.462 | 0.000 | 1520/1580 |
| high_ground/1500/water_ai_mix/water.ink_lobber | seat0 | eliminated | 24.3 | 0.261 | 0.000 | 1580/1520 |
| high_ground/1500/water.ink_lobber/skai_ai_mix | seat1 | eliminated | 14.6 | 0.000 | 0.535 | 1520/1630 |
| high_ground/1500/skai_ai_mix/water.ink_lobber | seat0 | eliminated | 21.7 | 0.245 | 0.000 | 1630/1520 |
| high_ground/1500/water.leviathan/water.reef_runner | seat0 | eliminated | 14.7 | 0.786 | 0.000 | 1520/1500 |
| high_ground/1500/water.reef_runner/water.leviathan | seat1 | eliminated | 4.6 | 0.000 | 0.978 | 1500/1520 |
| high_ground/1500/water.leviathan/water.shellback | seat0 | eliminated | 41.7 | 0.754 | 0.000 | 1520/1575 |
| high_ground/1500/water.shellback/water.leviathan | seat0 | eliminated | 34.2 | 0.359 | 0.000 | 1575/1520 |
| high_ground/1500/water.leviathan/water.spoutling | seat0 | eliminated | 14.7 | 0.809 | 0.000 | 1520/1500 |
| high_ground/1500/water.spoutling/water.leviathan | seat1 | eliminated | 5.3 | 0.000 | 1.000 | 1500/1520 |
| high_ground/1500/water.leviathan/water_ai_mix | seat0 | eliminated | 23.7 | 0.707 | 0.000 | 1520/1580 |
| high_ground/1500/water_ai_mix/water.leviathan | seat1 | eliminated | 8.3 | 0.000 | 1.000 | 1580/1520 |
| high_ground/1500/water.leviathan/skai_ai_mix | seat0 | eliminated | 17.7 | 0.636 | 0.000 | 1520/1630 |
| high_ground/1500/skai_ai_mix/water.leviathan | seat1 | eliminated | 7.5 | 0.000 | 0.908 | 1630/1520 |
| high_ground/1500/water.reef_runner/water.shellback | seat0 | eliminated | 82.0 | 0.900 | 0.000 | 1500/1575 |
| high_ground/1500/water.shellback/water.reef_runner | seat0 | eliminated | 27.1 | 0.980 | 0.000 | 1575/1500 |
| high_ground/1500/water.reef_runner/water.spoutling | seat0 | eliminated | 11.7 | 0.747 | 0.000 | 1500/1500 |
| high_ground/1500/water.spoutling/water.reef_runner | seat1 | eliminated | 15.2 | 0.000 | 0.802 | 1500/1500 |
| high_ground/1500/water.reef_runner/water_ai_mix | seat1 | eliminated | 29.4 | 0.000 | 0.640 | 1500/1580 |
| high_ground/1500/water_ai_mix/water.reef_runner | seat0 | eliminated | 25.8 | 0.505 | 0.000 | 1580/1500 |
| high_ground/1500/water.reef_runner/skai_ai_mix | seat1 | eliminated | 17.0 | 0.000 | 0.396 | 1500/1630 |
| high_ground/1500/skai_ai_mix/water.reef_runner | seat0 | eliminated | 20.3 | 0.724 | 0.000 | 1630/1500 |
| high_ground/1500/water.shellback/water.spoutling | seat0 | eliminated | 19.9 | 0.997 | 0.000 | 1575/1500 |
| high_ground/1500/water.spoutling/water.shellback | stalemate | dome_absorbed | 39.8 | 0.917 | 1.000 | 1500/1575 |
| high_ground/1500/water.shellback/water_ai_mix | seat0 | eliminated | 45.7 | 0.689 | 0.000 | 1575/1580 |
| high_ground/1500/water_ai_mix/water.shellback | seat0 | eliminated | 68.0 | 0.563 | 0.000 | 1580/1575 |
| high_ground/1500/water.shellback/skai_ai_mix | seat1 | eliminated | 126.9 | 0.000 | 0.245 | 1575/1630 |
| high_ground/1500/skai_ai_mix/water.shellback | seat0 | eliminated | 89.0 | 1.000 | 0.000 | 1630/1575 |
| high_ground/1500/water.spoutling/water_ai_mix | seat1 | eliminated | 24.2 | 0.000 | 0.838 | 1500/1580 |
| high_ground/1500/water_ai_mix/water.spoutling | seat0 | eliminated | 15.4 | 0.801 | 0.000 | 1580/1500 |
| high_ground/1500/water.spoutling/skai_ai_mix | seat1 | eliminated | 11.2 | 0.000 | 0.884 | 1500/1630 |
| high_ground/1500/skai_ai_mix/water.spoutling | seat0 | eliminated | 13.4 | 0.712 | 0.000 | 1630/1500 |
| high_ground/1500/water_ai_mix/skai_ai_mix | seat0 | eliminated | 31.9 | 0.320 | 0.000 | 1580/1630 |
| high_ground/1500/skai_ai_mix/water_ai_mix | seat0 | eliminated | 47.2 | 0.114 | 0.000 | 1630/1580 |
| flooded/1500/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 27.8 | 1.000 | 0.000 | 1600/1520 |
| flooded/1500/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.7 | 0.000 | 1.000 | 1520/1600 |
| flooded/1500/skai.roc_rider/skai.talon_archer | seat1 | eliminated | 17.3 | 0.000 | 1.000 | 1600/1500 |
| flooded/1500/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 18.8 | 0.000 | 1.000 | 1500/1600 |
| flooded/1500/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 36.7 | 1.000 | 0.000 | 1600/1590 |
| flooded/1500/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 22.4 | 0.000 | 1.000 | 1590/1600 |
| flooded/1500/skai.roc_rider/skai.windrunner | seat1 | eliminated | 14.0 | 0.000 | 1.000 | 1600/1500 |
| flooded/1500/skai.windrunner/skai.roc_rider | seat1 | eliminated | 19.3 | 0.000 | 0.794 | 1500/1600 |
| flooded/1500/skai.roc_rider/water.grasper | seat0 | eliminated | 47.6 | 1.000 | 0.000 | 1600/1500 |
| flooded/1500/water.grasper/skai.roc_rider | seat1 | eliminated | 29.0 | 0.000 | 1.000 | 1500/1600 |
| flooded/1500/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 29.2 | 1.000 | 0.000 | 1600/1520 |
| flooded/1500/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 11.0 | 0.000 | 1.000 | 1520/1600 |
| flooded/1500/skai.roc_rider/water.leviathan | seat1 | eliminated | 7.6 | 0.000 | 1.000 | 1600/1520 |
| flooded/1500/water.leviathan/skai.roc_rider | seat0 | eliminated | 12.1 | 0.658 | 0.000 | 1520/1600 |
| flooded/1500/skai.roc_rider/water.reef_runner | seat1 | eliminated | 14.3 | 0.000 | 1.000 | 1600/1500 |
| flooded/1500/water.reef_runner/skai.roc_rider | seat1 | eliminated | 18.4 | 0.000 | 0.822 | 1500/1600 |
| flooded/1500/skai.roc_rider/water.shellback | seat1 | eliminated | 28.2 | 0.000 | 1.000 | 1600/1575 |
| flooded/1500/water.shellback/skai.roc_rider | seat0 | eliminated | 26.0 | 1.000 | 0.000 | 1575/1600 |
| flooded/1500/skai.roc_rider/water.spoutling | seat1 | eliminated | 24.5 | 0.000 | 0.948 | 1600/1500 |
| flooded/1500/water.spoutling/skai.roc_rider | seat1 | eliminated | 10.7 | 0.000 | 1.000 | 1500/1600 |
| flooded/1500/skai.roc_rider/water_ai_mix | seat1 | eliminated | 25.0 | 0.000 | 1.000 | 1600/1580 |
| flooded/1500/water_ai_mix/skai.roc_rider | seat1 | eliminated | 33.9 | 0.000 | 0.725 | 1580/1600 |
| flooded/1500/skai.roc_rider/skai_ai_mix | seat1 | eliminated | 20.0 | 0.000 | 1.000 | 1600/1630 |
| flooded/1500/skai_ai_mix/skai.roc_rider | seat1 | eliminated | 19.6 | 0.000 | 0.913 | 1630/1600 |
| flooded/1500/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 9.5 | 0.000 | 1.000 | 1520/1500 |
| flooded/1500/skai.talon_archer/skai.stormcaller | seat1 | eliminated | 14.9 | 0.000 | 1.000 | 1500/1520 |
| flooded/1500/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 7.8 | 0.000 | 1.000 | 1520/1590 |
| flooded/1500/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 12.8 | 1.000 | 0.000 | 1590/1520 |
| flooded/1500/skai.stormcaller/skai.windrunner | seat1 | eliminated | 8.1 | 0.000 | 1.000 | 1520/1500 |
| flooded/1500/skai.windrunner/skai.stormcaller | seat1 | eliminated | 16.6 | 0.000 | 0.819 | 1500/1520 |
| flooded/1500/skai.stormcaller/water.grasper | seat1 | eliminated | 8.6 | 0.000 | 1.000 | 1520/1500 |
| flooded/1500/water.grasper/skai.stormcaller | seat0 | eliminated | 9.6 | 0.347 | 0.000 | 1500/1520 |
| flooded/1500/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 10.3 | 0.000 | 1.000 | 1520/1520 |
| flooded/1500/water.ink_lobber/skai.stormcaller | seat1 | eliminated | 11.5 | 0.000 | 0.700 | 1520/1520 |
| flooded/1500/skai.stormcaller/water.leviathan | seat1 | eliminated | 8.8 | 0.000 | 1.000 | 1520/1520 |
| flooded/1500/water.leviathan/skai.stormcaller | seat0 | eliminated | 9.1 | 0.705 | 0.000 | 1520/1520 |
| flooded/1500/skai.stormcaller/water.reef_runner | seat1 | eliminated | 7.7 | 0.000 | 1.000 | 1520/1500 |
| flooded/1500/water.reef_runner/skai.stormcaller | seat1 | eliminated | 17.4 | 0.000 | 0.817 | 1500/1520 |
| flooded/1500/skai.stormcaller/water.shellback | seat1 | eliminated | 15.7 | 0.000 | 1.000 | 1520/1575 |
| flooded/1500/water.shellback/skai.stormcaller | seat0 | eliminated | 14.4 | 0.981 | 0.000 | 1575/1520 |
| flooded/1500/skai.stormcaller/water.spoutling | seat1 | eliminated | 15.4 | 0.000 | 0.648 | 1520/1500 |
| flooded/1500/water.spoutling/skai.stormcaller | seat1 | eliminated | 12.9 | 0.000 | 1.000 | 1500/1520 |
| flooded/1500/skai.stormcaller/water_ai_mix | seat1 | eliminated | 10.6 | 0.000 | 1.000 | 1520/1580 |
| flooded/1500/water_ai_mix/skai.stormcaller | seat0 | eliminated | 36.6 | 0.150 | 0.000 | 1580/1520 |
| flooded/1500/skai.stormcaller/skai_ai_mix | seat1 | eliminated | 8.8 | 0.000 | 1.000 | 1520/1630 |
| flooded/1500/skai_ai_mix/skai.stormcaller | seat0 | eliminated | 39.0 | 0.245 | 0.000 | 1630/1520 |
| flooded/1500/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 10.3 | 0.000 | 1.000 | 1500/1590 |
| flooded/1500/skai.thunderhead/skai.talon_archer | seat1 | eliminated | 19.7 | 0.000 | 0.308 | 1590/1500 |
| flooded/1500/skai.talon_archer/skai.windrunner | seat1 | eliminated | 12.6 | 0.000 | 1.000 | 1500/1500 |
| flooded/1500/skai.windrunner/skai.talon_archer | seat1 | eliminated | 18.2 | 0.000 | 0.832 | 1500/1500 |
| flooded/1500/skai.talon_archer/water.grasper | seat1 | eliminated | 12.8 | 0.000 | 1.000 | 1500/1500 |
| flooded/1500/water.grasper/skai.talon_archer | seat0 | eliminated | 12.9 | 0.612 | 0.000 | 1500/1500 |
| flooded/1500/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 14.1 | 0.000 | 1.000 | 1500/1520 |
| flooded/1500/water.ink_lobber/skai.talon_archer | seat1 | eliminated | 10.0 | 0.000 | 0.860 | 1520/1500 |
| flooded/1500/skai.talon_archer/water.leviathan | seat1 | eliminated | 10.6 | 0.000 | 1.000 | 1500/1520 |
| flooded/1500/water.leviathan/skai.talon_archer | seat0 | eliminated | 9.1 | 0.720 | 0.000 | 1520/1500 |
| flooded/1500/skai.talon_archer/water.reef_runner | seat1 | eliminated | 11.9 | 0.000 | 1.000 | 1500/1500 |
| flooded/1500/water.reef_runner/skai.talon_archer | seat1 | eliminated | 19.5 | 0.000 | 0.791 | 1500/1500 |
| flooded/1500/skai.talon_archer/water.shellback | seat1 | eliminated | 23.1 | 0.000 | 0.999 | 1500/1575 |
| flooded/1500/water.shellback/skai.talon_archer | seat0 | eliminated | 21.0 | 0.985 | 0.000 | 1575/1500 |
| flooded/1500/skai.talon_archer/water.spoutling | seat1 | eliminated | 28.1 | 0.000 | 0.607 | 1500/1500 |
| flooded/1500/water.spoutling/skai.talon_archer | seat1 | eliminated | 10.9 | 0.000 | 1.000 | 1500/1500 |
| flooded/1500/skai.talon_archer/water_ai_mix | seat1 | eliminated | 15.3 | 0.000 | 1.000 | 1500/1580 |
| flooded/1500/water_ai_mix/skai.talon_archer | seat1 | eliminated | 43.3 | 0.000 | 0.202 | 1580/1500 |
| flooded/1500/skai.talon_archer/skai_ai_mix | seat1 | eliminated | 14.0 | 0.000 | 1.000 | 1500/1630 |
| flooded/1500/skai_ai_mix/skai.talon_archer | seat1 | eliminated | 14.6 | 0.000 | 0.986 | 1630/1500 |
| flooded/1500/skai.thunderhead/skai.windrunner | seat1 | eliminated | 15.2 | 0.000 | 0.454 | 1590/1500 |
| flooded/1500/skai.windrunner/skai.thunderhead | seat1 | eliminated | 11.4 | 0.000 | 0.933 | 1500/1590 |
| flooded/1500/skai.thunderhead/water.grasper | seat0 | eliminated | 15.4 | 1.000 | 0.000 | 1590/1500 |
| flooded/1500/water.grasper/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 1.000 | 1500/1590 |
| flooded/1500/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 11.1 | 1.000 | 0.000 | 1590/1520 |
| flooded/1500/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 8.0 | 0.000 | 1.000 | 1520/1590 |
| flooded/1500/skai.thunderhead/water.leviathan | seat1 | eliminated | 8.6 | 0.000 | 1.000 | 1590/1520 |
| flooded/1500/water.leviathan/skai.thunderhead | seat1 | eliminated | 9.9 | 0.000 | 0.211 | 1520/1590 |
| flooded/1500/skai.thunderhead/water.reef_runner | seat1 | eliminated | 13.2 | 0.000 | 0.596 | 1590/1500 |
| flooded/1500/water.reef_runner/skai.thunderhead | seat1 | eliminated | 11.3 | 0.000 | 0.937 | 1500/1590 |
| flooded/1500/skai.thunderhead/water.shellback | seat1 | eliminated | 24.5 | 0.000 | 1.000 | 1590/1575 |
| flooded/1500/water.shellback/skai.thunderhead | seat0 | eliminated | 22.6 | 0.873 | 0.000 | 1575/1590 |
| flooded/1500/skai.thunderhead/water.spoutling | seat0 | eliminated | 13.3 | 0.561 | 0.000 | 1590/1500 |
| flooded/1500/water.spoutling/skai.thunderhead | seat1 | eliminated | 9.2 | 0.000 | 1.000 | 1500/1590 |
| flooded/1500/skai.thunderhead/water_ai_mix | seat1 | eliminated | 22.7 | 0.000 | 0.535 | 1590/1580 |
| flooded/1500/water_ai_mix/skai.thunderhead | seat1 | eliminated | 14.4 | 0.000 | 0.928 | 1580/1590 |
| flooded/1500/skai.thunderhead/skai_ai_mix | seat1 | eliminated | 22.3 | 0.000 | 0.312 | 1590/1630 |
| flooded/1500/skai_ai_mix/skai.thunderhead | seat0 | eliminated | 80.7 | 0.245 | 0.000 | 1630/1590 |
| flooded/1500/skai.windrunner/water.grasper | seat1 | eliminated | 11.5 | 0.000 | 0.901 | 1500/1500 |
| flooded/1500/water.grasper/skai.windrunner | seat0 | eliminated | 10.4 | 0.630 | 0.000 | 1500/1500 |
| flooded/1500/skai.windrunner/water.ink_lobber | seat1 | eliminated | 14.2 | 0.000 | 0.914 | 1500/1520 |
| flooded/1500/water.ink_lobber/skai.windrunner | seat1 | eliminated | 8.4 | 0.000 | 1.000 | 1520/1500 |
| flooded/1500/skai.windrunner/water.leviathan | seat1 | eliminated | 9.2 | 0.000 | 1.000 | 1500/1520 |
| flooded/1500/water.leviathan/skai.windrunner | seat0 | eliminated | 9.1 | 0.592 | 0.000 | 1520/1500 |
| flooded/1500/skai.windrunner/water.reef_runner | seat1 | eliminated | 11.9 | 0.000 | 0.874 | 1500/1500 |
| flooded/1500/water.reef_runner/skai.windrunner | seat1 | eliminated | 13.8 | 0.000 | 0.800 | 1500/1500 |
| flooded/1500/skai.windrunner/water.shellback | seat1 | eliminated | 22.7 | 0.000 | 1.000 | 1500/1575 |
| flooded/1500/water.shellback/skai.windrunner | seat0 | eliminated | 18.3 | 0.986 | 0.000 | 1575/1500 |
| flooded/1500/skai.windrunner/water.spoutling | seat0 | eliminated | 33.2 | 0.254 | 0.000 | 1500/1500 |
| flooded/1500/water.spoutling/skai.windrunner | seat1 | eliminated | 9.4 | 0.000 | 1.000 | 1500/1500 |
| flooded/1500/skai.windrunner/water_ai_mix | seat1 | eliminated | 14.0 | 0.000 | 0.937 | 1500/1580 |
| flooded/1500/water_ai_mix/skai.windrunner | seat1 | eliminated | 39.4 | 0.000 | 0.188 | 1580/1500 |
| flooded/1500/skai.windrunner/skai_ai_mix | seat1 | eliminated | 13.6 | 0.000 | 0.871 | 1500/1630 |
| flooded/1500/skai_ai_mix/skai.windrunner | seat1 | eliminated | 11.9 | 0.000 | 0.974 | 1630/1500 |
| flooded/1500/water.grasper/water.ink_lobber | seat0 | eliminated | 12.4 | 0.104 | 0.000 | 1500/1520 |
| flooded/1500/water.ink_lobber/water.grasper | seat1 | eliminated | 9.4 | 0.000 | 0.956 | 1520/1500 |
| flooded/1500/water.grasper/water.leviathan | seat1 | eliminated | 4.0 | 0.000 | 0.973 | 1500/1520 |
| flooded/1500/water.leviathan/water.grasper | seat0 | eliminated | 9.1 | 0.659 | 0.000 | 1520/1500 |
| flooded/1500/water.grasper/water.reef_runner | seat0 | eliminated | 12.0 | 0.594 | 0.000 | 1500/1500 |
| flooded/1500/water.reef_runner/water.grasper | seat1 | eliminated | 13.2 | 0.000 | 0.900 | 1500/1500 |
| flooded/1500/water.grasper/water.shellback | seat0 | eliminated | 23.2 | 0.505 | 0.000 | 1500/1575 |
| flooded/1500/water.shellback/water.grasper | seat1 | eliminated | 21.4 | 0.000 | 0.701 | 1575/1500 |
| flooded/1500/water.grasper/water.spoutling | seat0 | eliminated | 7.8 | 0.830 | 0.000 | 1500/1500 |
| flooded/1500/water.spoutling/water.grasper | seat1 | eliminated | 9.8 | 0.000 | 1.000 | 1500/1500 |
| flooded/1500/water.grasper/water_ai_mix | seat0 | eliminated | 20.0 | 0.136 | 0.000 | 1500/1580 |
| flooded/1500/water_ai_mix/water.grasper | seat1 | eliminated | 14.7 | 0.000 | 0.815 | 1580/1500 |
| flooded/1500/water.grasper/skai_ai_mix | seat1 | eliminated | 61.3 | 0.000 | 0.245 | 1500/1630 |
| flooded/1500/skai_ai_mix/water.grasper | stalemate | no_damage | 159.9 | 0.245 | 0.067 | 1630/1500 |
| flooded/1500/water.ink_lobber/water.leviathan | seat1 | eliminated | 8.8 | 0.000 | 1.000 | 1520/1520 |
| flooded/1500/water.leviathan/water.ink_lobber | seat0 | eliminated | 12.1 | 0.387 | 0.000 | 1520/1520 |
| flooded/1500/water.ink_lobber/water.reef_runner | seat1 | eliminated | 8.3 | 0.000 | 1.000 | 1520/1500 |
| flooded/1500/water.reef_runner/water.ink_lobber | seat1 | eliminated | 13.4 | 0.000 | 0.915 | 1500/1520 |
| flooded/1500/water.ink_lobber/water.shellback | seat1 | eliminated | 16.5 | 0.000 | 1.000 | 1520/1575 |
| flooded/1500/water.shellback/water.ink_lobber | seat0 | eliminated | 15.8 | 0.906 | 0.000 | 1575/1520 |
| flooded/1500/water.ink_lobber/water.spoutling | seat1 | eliminated | 22.4 | 0.000 | 0.209 | 1520/1500 |
| flooded/1500/water.spoutling/water.ink_lobber | seat1 | eliminated | 12.3 | 0.000 | 1.000 | 1500/1520 |
| flooded/1500/water.ink_lobber/water_ai_mix | seat1 | eliminated | 10.8 | 0.000 | 1.000 | 1520/1580 |
| flooded/1500/water_ai_mix/water.ink_lobber | seat1 | eliminated | 25.6 | 0.000 | 0.525 | 1580/1520 |
| flooded/1500/water.ink_lobber/skai_ai_mix | seat1 | eliminated | 9.6 | 0.000 | 0.979 | 1520/1630 |
| flooded/1500/skai_ai_mix/water.ink_lobber | seat0 | eliminated | 48.3 | 0.245 | 0.000 | 1630/1520 |
| flooded/1500/water.leviathan/water.reef_runner | seat0 | eliminated | 15.1 | 0.409 | 0.000 | 1520/1500 |
| flooded/1500/water.reef_runner/water.leviathan | seat1 | eliminated | 11.9 | 0.000 | 1.000 | 1500/1520 |
| flooded/1500/water.leviathan/water.shellback | seat1 | eliminated | 27.1 | 0.000 | 0.889 | 1520/1575 |
| flooded/1500/water.shellback/water.leviathan | seat0 | eliminated | 33.0 | 0.192 | 0.000 | 1575/1520 |
| flooded/1500/water.leviathan/water.spoutling | seat0 | eliminated | 9.1 | 0.777 | 0.000 | 1520/1500 |
| flooded/1500/water.spoutling/water.leviathan | seat1 | eliminated | 9.4 | 0.000 | 1.000 | 1500/1520 |
| flooded/1500/water.leviathan/water_ai_mix | seat0 | eliminated | 21.1 | 0.363 | 0.000 | 1520/1580 |
| flooded/1500/water_ai_mix/water.leviathan | seat1 | eliminated | 13.1 | 0.000 | 0.965 | 1580/1520 |
| flooded/1500/water.leviathan/skai_ai_mix | seat0 | eliminated | 12.1 | 0.530 | 0.000 | 1520/1630 |
| flooded/1500/skai_ai_mix/water.leviathan | seat1 | eliminated | 7.3 | 0.000 | 1.000 | 1630/1520 |
| flooded/1500/water.reef_runner/water.shellback | seat1 | eliminated | 24.9 | 0.000 | 1.000 | 1500/1575 |
| flooded/1500/water.shellback/water.reef_runner | seat0 | eliminated | 20.8 | 0.986 | 0.000 | 1575/1500 |
| flooded/1500/water.reef_runner/water.spoutling | seat0 | eliminated | 26.3 | 0.509 | 0.000 | 1500/1500 |
| flooded/1500/water.spoutling/water.reef_runner | seat1 | eliminated | 8.9 | 0.000 | 1.000 | 1500/1500 |
| flooded/1500/water.reef_runner/water_ai_mix | seat1 | eliminated | 15.6 | 0.000 | 0.962 | 1500/1580 |
| flooded/1500/water_ai_mix/water.reef_runner | seat1 | eliminated | 33.9 | 0.000 | 0.170 | 1580/1500 |
| flooded/1500/water.reef_runner/skai_ai_mix | seat1 | eliminated | 15.4 | 0.000 | 0.836 | 1500/1630 |
| flooded/1500/skai_ai_mix/water.reef_runner | seat1 | eliminated | 11.7 | 0.000 | 0.992 | 1630/1500 |
| flooded/1500/water.shellback/water.spoutling | seat0 | eliminated | 15.6 | 0.995 | 0.000 | 1575/1500 |
| flooded/1500/water.spoutling/water.shellback | seat1 | eliminated | 18.2 | 0.000 | 1.000 | 1500/1575 |
| flooded/1500/water.shellback/water_ai_mix | seat1 | eliminated | 57.7 | 0.000 | 0.705 | 1575/1580 |
| flooded/1500/water_ai_mix/water.shellback | seat1 | eliminated | 44.8 | 0.000 | 0.716 | 1580/1575 |
| flooded/1500/water.shellback/skai_ai_mix | seat0 | eliminated | 21.2 | 1.000 | 0.000 | 1575/1630 |
| flooded/1500/skai_ai_mix/water.shellback | seat1 | eliminated | 23.1 | 0.000 | 1.000 | 1630/1575 |
| flooded/1500/water.spoutling/water_ai_mix | seat1 | eliminated | 11.7 | 0.000 | 1.000 | 1500/1580 |
| flooded/1500/water_ai_mix/water.spoutling | seat0 | eliminated | 14.6 | 0.891 | 0.000 | 1580/1500 |
| flooded/1500/water.spoutling/skai_ai_mix | seat1 | eliminated | 10.3 | 0.000 | 1.000 | 1500/1630 |
| flooded/1500/skai_ai_mix/water.spoutling | seat1 | eliminated | 24.6 | 0.000 | 0.746 | 1630/1500 |
| flooded/1500/water_ai_mix/skai_ai_mix | seat1 | eliminated | 32.0 | 0.000 | 0.419 | 1580/1630 |
| flooded/1500/skai_ai_mix/water_ai_mix | seat1 | eliminated | 14.5 | 0.000 | 0.988 | 1630/1580 |
| choke/1500/skai.roc_rider/skai.stormcaller | seat0 | eliminated | 10.2 | 1.000 | 0.000 | 1600/1520 |
| choke/1500/skai.stormcaller/skai.roc_rider | seat1 | eliminated | 10.4 | 0.000 | 1.000 | 1520/1600 |
| choke/1500/skai.roc_rider/skai.talon_archer | seat0 | eliminated | 24.6 | 0.464 | 0.000 | 1600/1500 |
| choke/1500/skai.talon_archer/skai.roc_rider | seat1 | eliminated | 32.5 | 0.000 | 0.126 | 1500/1600 |
| choke/1500/skai.roc_rider/skai.thunderhead | seat0 | eliminated | 18.2 | 1.000 | 0.000 | 1600/1590 |
| choke/1500/skai.thunderhead/skai.roc_rider | seat1 | eliminated | 18.2 | 0.000 | 1.000 | 1590/1600 |
| choke/1500/skai.roc_rider/skai.windrunner | seat1 | eliminated | 21.7 | 0.000 | 0.253 | 1600/1500 |
| choke/1500/skai.windrunner/skai.roc_rider | seat0 | eliminated | 18.9 | 0.380 | 0.000 | 1500/1600 |
| choke/1500/skai.roc_rider/water.grasper | seat0 | eliminated | 29.1 | 1.000 | 0.000 | 1600/1500 |
| choke/1500/water.grasper/skai.roc_rider | seat1 | eliminated | 28.3 | 0.000 | 1.000 | 1500/1600 |
| choke/1500/skai.roc_rider/water.ink_lobber | seat0 | eliminated | 11.5 | 1.000 | 0.000 | 1600/1520 |
| choke/1500/water.ink_lobber/skai.roc_rider | seat1 | eliminated | 10.8 | 0.000 | 1.000 | 1520/1600 |
| choke/1500/skai.roc_rider/water.leviathan | seat1 | eliminated | 29.0 | 0.000 | 0.328 | 1600/1520 |
| choke/1500/water.leviathan/skai.roc_rider | seat0 | eliminated | 17.8 | 0.724 | 0.000 | 1520/1600 |
| choke/1500/skai.roc_rider/water.reef_runner | seat0 | eliminated | 20.6 | 1.000 | 0.000 | 1600/1500 |
| choke/1500/water.reef_runner/skai.roc_rider | seat1 | eliminated | 20.6 | 0.000 | 1.000 | 1500/1600 |
| choke/1500/skai.roc_rider/water.shellback | stalemate | dome_absorbed | 111.1 | 1.000 | 0.889 | 1600/1575 |
| choke/1500/water.shellback/skai.roc_rider | unresolved | time_cap | 180.0 | 0.767 | 1.000 | 1575/1600 |
| choke/1500/skai.roc_rider/water.spoutling | seat0 | eliminated | 12.4 | 0.734 | 0.000 | 1600/1500 |
| choke/1500/water.spoutling/skai.roc_rider | seat1 | eliminated | 12.4 | 0.000 | 0.730 | 1500/1600 |
| choke/1500/skai.roc_rider/water_ai_mix | seat1 | eliminated | 24.9 | 0.000 | 0.641 | 1600/1580 |
| choke/1500/water_ai_mix/skai.roc_rider | seat0 | eliminated | 27.0 | 0.517 | 0.000 | 1580/1600 |
| choke/1500/skai.roc_rider/skai_ai_mix | seat0 | eliminated | 18.2 | 0.439 | 0.000 | 1600/1630 |
| choke/1500/skai_ai_mix/skai.roc_rider | seat1 | eliminated | 19.8 | 0.000 | 0.417 | 1630/1600 |
| choke/1500/skai.stormcaller/skai.talon_archer | seat1 | eliminated | 11.4 | 0.000 | 0.225 | 1520/1500 |
| choke/1500/skai.talon_archer/skai.stormcaller | seat0 | eliminated | 11.5 | 0.150 | 0.000 | 1500/1520 |
| choke/1500/skai.stormcaller/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1520/1590 |
| choke/1500/skai.thunderhead/skai.stormcaller | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 1590/1520 |
| choke/1500/skai.stormcaller/skai.windrunner | seat1 | eliminated | 10.1 | 0.000 | 0.135 | 1520/1500 |
| choke/1500/skai.windrunner/skai.stormcaller | seat0 | eliminated | 10.9 | 0.206 | 0.000 | 1500/1520 |
| choke/1500/skai.stormcaller/water.grasper | seat1 | eliminated | 15.2 | 0.000 | 0.056 | 1520/1500 |
| choke/1500/water.grasper/skai.stormcaller | seat0 | eliminated | 14.6 | 0.109 | 0.000 | 1500/1520 |
| choke/1500/skai.stormcaller/water.ink_lobber | seat1 | eliminated | 9.1 | 0.000 | 0.181 | 1520/1520 |
| choke/1500/water.ink_lobber/skai.stormcaller | seat0 | eliminated | 8.8 | 0.372 | 0.000 | 1520/1520 |
| choke/1500/skai.stormcaller/water.leviathan | seat1 | eliminated | 4.4 | 0.000 | 0.917 | 1520/1520 |
| choke/1500/water.leviathan/skai.stormcaller | seat0 | eliminated | 4.3 | 0.959 | 0.000 | 1520/1520 |
| choke/1500/skai.stormcaller/water.reef_runner | seat1 | eliminated | 12.0 | 0.000 | 0.134 | 1520/1500 |
| choke/1500/water.reef_runner/skai.stormcaller | seat0 | eliminated | 13.7 | 0.143 | 0.000 | 1500/1520 |
| choke/1500/skai.stormcaller/water.shellback | seat1 | eliminated | 14.8 | 0.000 | 1.000 | 1520/1575 |
| choke/1500/water.shellback/skai.stormcaller | seat0 | eliminated | 14.9 | 1.000 | 0.000 | 1575/1520 |
| choke/1500/skai.stormcaller/water.spoutling | seat0 | eliminated | 7.1 | 0.840 | 0.000 | 1520/1500 |
| choke/1500/water.spoutling/skai.stormcaller | seat1 | eliminated | 7.2 | 0.000 | 0.840 | 1500/1520 |
| choke/1500/skai.stormcaller/water_ai_mix | seat1 | eliminated | 10.5 | 0.000 | 0.739 | 1520/1580 |
| choke/1500/water_ai_mix/skai.stormcaller | seat0 | eliminated | 15.6 | 0.545 | 0.000 | 1580/1520 |
| choke/1500/skai.stormcaller/skai_ai_mix | seat1 | eliminated | 10.2 | 0.000 | 0.599 | 1520/1630 |
| choke/1500/skai_ai_mix/skai.stormcaller | seat0 | eliminated | 11.3 | 0.517 | 0.000 | 1630/1520 |
| choke/1500/skai.talon_archer/skai.thunderhead | seat1 | eliminated | 9.0 | 0.000 | 0.726 | 1500/1590 |
| choke/1500/skai.thunderhead/skai.talon_archer | seat0 | eliminated | 7.1 | 0.759 | 0.000 | 1590/1500 |
| choke/1500/skai.talon_archer/skai.windrunner | seat1 | eliminated | 19.7 | 0.000 | 0.233 | 1500/1500 |
| choke/1500/skai.windrunner/skai.talon_archer | seat0 | eliminated | 18.4 | 0.287 | 0.000 | 1500/1500 |
| choke/1500/skai.talon_archer/water.grasper | seat1 | eliminated | 14.9 | 0.000 | 0.572 | 1500/1500 |
| choke/1500/water.grasper/skai.talon_archer | seat0 | eliminated | 14.7 | 0.560 | 0.000 | 1500/1500 |
| choke/1500/skai.talon_archer/water.ink_lobber | seat1 | eliminated | 14.7 | 0.000 | 0.217 | 1500/1520 |
| choke/1500/water.ink_lobber/skai.talon_archer | seat0 | eliminated | 11.4 | 0.380 | 0.000 | 1520/1500 |
| choke/1500/skai.talon_archer/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.959 | 1500/1520 |
| choke/1500/water.leviathan/skai.talon_archer | seat0 | eliminated | 4.2 | 0.980 | 0.000 | 1520/1500 |
| choke/1500/skai.talon_archer/water.reef_runner | seat1 | eliminated | 18.3 | 0.000 | 0.327 | 1500/1500 |
| choke/1500/water.reef_runner/skai.talon_archer | seat0 | eliminated | 17.6 | 0.321 | 0.000 | 1500/1500 |
| choke/1500/skai.talon_archer/water.shellback | seat1 | eliminated | 21.0 | 0.000 | 0.966 | 1500/1575 |
| choke/1500/water.shellback/skai.talon_archer | seat0 | eliminated | 21.3 | 0.997 | 0.000 | 1575/1500 |
| choke/1500/skai.talon_archer/water.spoutling | seat0 | eliminated | 10.0 | 0.793 | 0.000 | 1500/1500 |
| choke/1500/water.spoutling/skai.talon_archer | seat1 | eliminated | 9.9 | 0.000 | 0.826 | 1500/1500 |
| choke/1500/skai.talon_archer/water_ai_mix | seat1 | eliminated | 14.7 | 0.000 | 0.769 | 1500/1580 |
| choke/1500/water_ai_mix/skai.talon_archer | seat0 | eliminated | 16.2 | 0.714 | 0.000 | 1580/1500 |
| choke/1500/skai.talon_archer/skai_ai_mix | seat1 | eliminated | 17.4 | 0.000 | 0.369 | 1500/1630 |
| choke/1500/skai_ai_mix/skai.talon_archer | seat0 | eliminated | 18.3 | 0.355 | 0.000 | 1630/1500 |
| choke/1500/skai.thunderhead/skai.windrunner | seat0 | eliminated | 6.3 | 0.817 | 0.000 | 1590/1500 |
| choke/1500/skai.windrunner/skai.thunderhead | seat1 | eliminated | 6.5 | 0.000 | 0.787 | 1500/1590 |
| choke/1500/skai.thunderhead/water.grasper | seat0 | eliminated | 8.9 | 1.000 | 0.000 | 1590/1500 |
| choke/1500/water.grasper/skai.thunderhead | seat1 | eliminated | 8.9 | 0.000 | 1.000 | 1500/1590 |
| choke/1500/skai.thunderhead/water.ink_lobber | seat0 | eliminated | 4.5 | 1.000 | 0.000 | 1590/1520 |
| choke/1500/water.ink_lobber/skai.thunderhead | seat1 | eliminated | 4.5 | 0.000 | 1.000 | 1520/1590 |
| choke/1500/skai.thunderhead/water.leviathan | seat1 | eliminated | 18.3 | 0.000 | 0.472 | 1590/1520 |
| choke/1500/water.leviathan/skai.thunderhead | seat0 | eliminated | 18.6 | 0.472 | 0.000 | 1520/1590 |
| choke/1500/skai.thunderhead/water.reef_runner | seat0 | eliminated | 6.6 | 1.000 | 0.000 | 1590/1500 |
| choke/1500/water.reef_runner/skai.thunderhead | seat1 | eliminated | 6.6 | 0.000 | 1.000 | 1500/1590 |
| choke/1500/skai.thunderhead/water.shellback | seat0 | eliminated | 49.4 | 1.000 | 0.000 | 1590/1575 |
| choke/1500/water.shellback/skai.thunderhead | seat1 | eliminated | 56.8 | 0.000 | 1.000 | 1575/1590 |
| choke/1500/skai.thunderhead/water.spoutling | seat0 | eliminated | 6.5 | 0.895 | 0.000 | 1590/1500 |
| choke/1500/water.spoutling/skai.thunderhead | seat1 | eliminated | 4.4 | 0.000 | 0.904 | 1500/1590 |
| choke/1500/skai.thunderhead/water_ai_mix | seat0 | eliminated | 14.3 | 0.649 | 0.000 | 1590/1580 |
| choke/1500/water_ai_mix/skai.thunderhead | seat1 | eliminated | 26.6 | 0.000 | 0.389 | 1580/1590 |
| choke/1500/skai.thunderhead/skai_ai_mix | seat1 | eliminated | 60.1 | 0.000 | 0.245 | 1590/1630 |
| choke/1500/skai_ai_mix/skai.thunderhead | seat0 | eliminated | 56.5 | 0.245 | 0.000 | 1630/1590 |
| choke/1500/skai.windrunner/water.grasper | seat1 | eliminated | 11.5 | 0.000 | 0.615 | 1500/1500 |
| choke/1500/water.grasper/skai.windrunner | seat0 | eliminated | 12.1 | 0.596 | 0.000 | 1500/1500 |
| choke/1500/skai.windrunner/water.ink_lobber | seat1 | eliminated | 10.7 | 0.000 | 0.506 | 1500/1520 |
| choke/1500/water.ink_lobber/skai.windrunner | seat0 | eliminated | 10.8 | 0.411 | 0.000 | 1520/1500 |
| choke/1500/skai.windrunner/water.leviathan | seat1 | eliminated | 2.2 | 0.000 | 1.000 | 1500/1520 |
| choke/1500/water.leviathan/skai.windrunner | seat0 | eliminated | 2.3 | 1.000 | 0.000 | 1520/1500 |
| choke/1500/skai.windrunner/water.reef_runner | seat1 | eliminated | 14.7 | 0.000 | 0.332 | 1500/1500 |
| choke/1500/water.reef_runner/skai.windrunner | seat0 | eliminated | 16.8 | 0.223 | 0.000 | 1500/1500 |
| choke/1500/skai.windrunner/water.shellback | seat1 | eliminated | 18.0 | 0.000 | 0.963 | 1500/1575 |
| choke/1500/water.shellback/skai.windrunner | seat0 | eliminated | 18.4 | 0.995 | 0.000 | 1575/1500 |
| choke/1500/skai.windrunner/water.spoutling | seat0 | eliminated | 8.6 | 0.792 | 0.000 | 1500/1500 |
| choke/1500/water.spoutling/skai.windrunner | seat1 | eliminated | 8.8 | 0.000 | 0.787 | 1500/1500 |
| choke/1500/skai.windrunner/water_ai_mix | seat1 | eliminated | 12.5 | 0.000 | 0.779 | 1500/1580 |
| choke/1500/water_ai_mix/skai.windrunner | seat0 | eliminated | 13.0 | 0.763 | 0.000 | 1580/1500 |
| choke/1500/skai.windrunner/skai_ai_mix | seat1 | eliminated | 14.2 | 0.000 | 0.423 | 1500/1630 |
| choke/1500/skai_ai_mix/skai.windrunner | seat0 | eliminated | 14.0 | 0.412 | 0.000 | 1630/1500 |
| choke/1500/water.grasper/water.ink_lobber | seat1 | eliminated | 13.9 | 0.000 | 0.425 | 1500/1520 |
| choke/1500/water.ink_lobber/water.grasper | seat0 | eliminated | 14.1 | 0.270 | 0.000 | 1520/1500 |
| choke/1500/water.grasper/water.leviathan | seat1 | eliminated | 4.3 | 0.000 | 1.000 | 1500/1520 |
| choke/1500/water.leviathan/water.grasper | seat0 | eliminated | 4.3 | 1.000 | 0.000 | 1520/1500 |
| choke/1500/water.grasper/water.reef_runner | seat0 | eliminated | 13.9 | 0.543 | 0.000 | 1500/1500 |
| choke/1500/water.reef_runner/water.grasper | seat1 | eliminated | 14.4 | 0.000 | 0.526 | 1500/1500 |
| choke/1500/water.grasper/water.shellback | seat0 | eliminated | 23.7 | 0.520 | 0.000 | 1500/1575 |
| choke/1500/water.shellback/water.grasper | seat1 | eliminated | 24.4 | 0.000 | 0.509 | 1575/1500 |
| choke/1500/water.grasper/water.spoutling | seat0 | eliminated | 9.4 | 0.806 | 0.000 | 1500/1500 |
| choke/1500/water.spoutling/water.grasper | seat1 | eliminated | 9.4 | 0.000 | 0.806 | 1500/1500 |
| choke/1500/water.grasper/water_ai_mix | seat0 | eliminated | 22.5 | 0.113 | 0.000 | 1500/1580 |
| choke/1500/water_ai_mix/water.grasper | seat1 | eliminated | 26.7 | 0.000 | 0.012 | 1580/1500 |
| choke/1500/water.grasper/skai_ai_mix | seat1 | eliminated | 58.6 | 0.000 | 0.245 | 1500/1630 |
| choke/1500/skai_ai_mix/water.grasper | seat0 | eliminated | 53.1 | 0.245 | 0.000 | 1630/1500 |
| choke/1500/water.ink_lobber/water.leviathan | seat1 | eliminated | 4.6 | 0.000 | 0.803 | 1520/1520 |
| choke/1500/water.leviathan/water.ink_lobber | seat0 | eliminated | 4.7 | 0.803 | 0.000 | 1520/1520 |
| choke/1500/water.ink_lobber/water.reef_runner | seat0 | eliminated | 10.8 | 0.470 | 0.000 | 1520/1500 |
| choke/1500/water.reef_runner/water.ink_lobber | seat1 | eliminated | 10.9 | 0.000 | 0.432 | 1500/1520 |
| choke/1500/water.ink_lobber/water.shellback | seat1 | eliminated | 15.5 | 0.000 | 0.945 | 1520/1575 |
| choke/1500/water.shellback/water.ink_lobber | seat0 | eliminated | 15.6 | 0.997 | 0.000 | 1575/1520 |
| choke/1500/water.ink_lobber/water.spoutling | seat0 | eliminated | 7.9 | 0.862 | 0.000 | 1520/1500 |
| choke/1500/water.spoutling/water.ink_lobber | seat1 | eliminated | 8.0 | 0.000 | 0.850 | 1500/1520 |
| choke/1500/water.ink_lobber/water_ai_mix | seat1 | eliminated | 14.9 | 0.000 | 0.530 | 1520/1580 |
| choke/1500/water_ai_mix/water.ink_lobber | seat0 | eliminated | 15.2 | 0.548 | 0.000 | 1580/1520 |
| choke/1500/water.ink_lobber/skai_ai_mix | seat1 | eliminated | 11.6 | 0.000 | 0.467 | 1520/1630 |
| choke/1500/skai_ai_mix/water.ink_lobber | seat0 | eliminated | 17.5 | 0.245 | 0.000 | 1630/1520 |
| choke/1500/water.leviathan/water.reef_runner | seat0 | eliminated | 4.2 | 0.961 | 0.000 | 1520/1500 |
| choke/1500/water.reef_runner/water.leviathan | seat1 | eliminated | 4.1 | 0.000 | 0.954 | 1500/1520 |
| choke/1500/water.leviathan/water.shellback | seat1 | eliminated | 28.1 | 0.000 | 0.606 | 1520/1575 |
| choke/1500/water.shellback/water.leviathan | seat0 | eliminated | 27.5 | 0.714 | 0.000 | 1575/1520 |
| choke/1500/water.leviathan/water.spoutling | seat0 | eliminated | 4.1 | 0.992 | 0.000 | 1520/1500 |
| choke/1500/water.spoutling/water.leviathan | seat1 | eliminated | 4.2 | 0.000 | 0.992 | 1500/1520 |
| choke/1500/water.leviathan/water_ai_mix | seat0 | eliminated | 10.8 | 0.764 | 0.000 | 1520/1580 |
| choke/1500/water_ai_mix/water.leviathan | seat1 | eliminated | 10.3 | 0.000 | 0.803 | 1580/1520 |
| choke/1500/water.leviathan/skai_ai_mix | seat0 | eliminated | 5.2 | 0.909 | 0.000 | 1520/1630 |
| choke/1500/skai_ai_mix/water.leviathan | seat1 | eliminated | 5.3 | 0.000 | 0.908 | 1630/1520 |
| choke/1500/water.reef_runner/water.shellback | seat1 | eliminated | 20.5 | 0.000 | 0.930 | 1500/1575 |
| choke/1500/water.shellback/water.reef_runner | seat0 | eliminated | 21.0 | 0.946 | 0.000 | 1575/1500 |
| choke/1500/water.reef_runner/water.spoutling | seat0 | eliminated | 8.9 | 0.808 | 0.000 | 1500/1500 |
| choke/1500/water.spoutling/water.reef_runner | seat1 | eliminated | 8.7 | 0.000 | 0.812 | 1500/1500 |
| choke/1500/water.reef_runner/water_ai_mix | seat1 | eliminated | 13.7 | 0.000 | 0.766 | 1500/1580 |
| choke/1500/water_ai_mix/water.reef_runner | seat0 | eliminated | 15.6 | 0.686 | 0.000 | 1580/1500 |
| choke/1500/water.reef_runner/skai_ai_mix | seat1 | eliminated | 35.1 | 0.000 | 0.245 | 1500/1630 |
| choke/1500/skai_ai_mix/water.reef_runner | seat0 | eliminated | 26.6 | 0.245 | 0.000 | 1630/1500 |
| choke/1500/water.shellback/water.spoutling | seat0 | eliminated | 14.7 | 1.000 | 0.000 | 1575/1500 |
| choke/1500/water.spoutling/water.shellback | seat1 | eliminated | 15.1 | 0.000 | 0.982 | 1500/1575 |
| choke/1500/water.shellback/water_ai_mix | seat0 | eliminated | 46.4 | 0.605 | 0.000 | 1575/1580 |
| choke/1500/water_ai_mix/water.shellback | seat1 | eliminated | 44.4 | 0.000 | 0.671 | 1580/1575 |
| choke/1500/water.shellback/skai_ai_mix | stalemate | dome_absorbed | 46.1 | 0.939 | 0.245 | 1575/1630 |
| choke/1500/skai_ai_mix/water.shellback | stalemate | dome_absorbed | 46.2 | 0.245 | 0.943 | 1630/1575 |
| choke/1500/water.spoutling/water_ai_mix | seat1 | eliminated | 9.3 | 0.000 | 0.931 | 1500/1580 |
| choke/1500/water_ai_mix/water.spoutling | seat0 | eliminated | 9.9 | 0.859 | 0.000 | 1580/1500 |
| choke/1500/water.spoutling/skai_ai_mix | seat1 | eliminated | 8.7 | 0.000 | 0.820 | 1500/1630 |
| choke/1500/skai_ai_mix/water.spoutling | seat0 | eliminated | 9.0 | 0.816 | 0.000 | 1630/1500 |
| choke/1500/water_ai_mix/skai_ai_mix | seat0 | eliminated | 14.7 | 0.629 | 0.000 | 1580/1630 |
| choke/1500/skai_ai_mix/water_ai_mix | seat1 | eliminated | 14.6 | 0.000 | 0.634 | 1630/1580 |

<!-- balance.md: written by the balance harness --balance (rts/tools/BalanceHarness/BalanceRunner.cs); not a source file. -->
# Balance report: aggressive_s5_b

- Profile aggressive_s5_b plays both slots; base seed 20261121, 72 games per cell, game cap 2400 s; factions swap on odd games.
- Content digest 7cb328f35c617679; 216 games, 216 distinct decided games, 0 undecided, 0 draws.
- Intervals are Wilson at z = 1.96 over distinct games (play signature); a cell shows wins/decided games.

## Faction matrix

Row faction's wins against the column faction; the diagonal is slot 0's win share in the mirror (seat).

| | skai | water |
| --- | --- | --- |
| skai | seat 48/72 [55.2, 76.5] (distinct 72) | 72/72 [94.9, 100.0] (distinct 72) |
| water | 0/72 [0.0, 5.1] (distinct 72) | seat 51/72 [59.5, 80.1] (distinct 72) |

## Opening matrix

### skai (rows) against skai (columns)

| | skai_air | skai_fast_expand | skai_standard | skai_windrunner_pressure |
| --- | --- | --- | --- | --- |
| skai_air | games 6 (distinct 6) | 0/8 [0.0, 32.4] (distinct 8) | 2/13 [4.3, 42.2] (distinct 13) | 2/3 [20.8, 93.9] (distinct 3) |
| skai_fast_expand | 8/8 [67.6, 100.0] (distinct 8) | games 3 (distinct 3) | 10/18 [33.7, 75.4] (distinct 18) | 4/6 [30.0, 90.3] (distinct 6) |
| skai_standard | 11/13 [57.8, 95.7] (distinct 13) | 8/18 [24.6, 66.3] (distinct 18) | games 5 (distinct 5) | 3/8 [13.7, 69.4] (distinct 8) |
| skai_windrunner_pressure | 1/3 [6.1, 79.2] (distinct 3) | 2/6 [9.7, 70.0] (distinct 6) | 5/8 [30.6, 86.3] (distinct 8) | games 2 (distinct 2) |

### skai (rows) against water (columns)

| | water_fast_expand | water_grasper_pressure | water_leviathan_tech | water_standard |
| --- | --- | --- | --- | --- |
| skai_air | 2/2 [34.2, 100.0] (distinct 2) | 3/3 [43.8, 100.0] (distinct 3) | 4/4 [51.0, 100.0] (distinct 4) | 7/7 [64.6, 100.0] (distinct 7) |
| skai_fast_expand | 2/2 [34.2, 100.0] (distinct 2) | 2/2 [34.2, 100.0] (distinct 2) | 4/4 [51.0, 100.0] (distinct 4) | 5/5 [56.6, 100.0] (distinct 5) |
| skai_standard | 8/8 [67.6, 100.0] (distinct 8) | 6/6 [61.0, 100.0] (distinct 6) | 8/8 [67.6, 100.0] (distinct 8) | 9/9 [70.1, 100.0] (distinct 9) |
| skai_windrunner_pressure | 1/1 [20.7, 100.0] (distinct 1) | 1/1 [20.7, 100.0] (distinct 1) | 6/6 [61.0, 100.0] (distinct 6) | 4/4 [51.0, 100.0] (distinct 4) |

### water (rows) against water (columns)

| | water_fast_expand | water_grasper_pressure | water_leviathan_tech | water_standard |
| --- | --- | --- | --- | --- |
| water_fast_expand | games 7 (distinct 7) | 2/8 [7.1, 59.1] (distinct 8) | 4/8 [21.5, 78.5] (distinct 8) | 4/13 [12.7, 57.6] (distinct 13) |
| water_grasper_pressure | 6/8 [40.9, 92.9] (distinct 8) | games 2 (distinct 2) | 6/8 [40.9, 92.9] (distinct 8) | 5/11 [21.3, 72.0] (distinct 11) |
| water_leviathan_tech | 4/8 [21.5, 78.5] (distinct 8) | 2/8 [7.1, 59.1] (distinct 8) | games 2 (distinct 2) | 4/8 [21.5, 78.5] (distinct 8) |
| water_standard | 9/13 [42.4, 87.3] (distinct 13) | 6/11 [28.0, 78.7] (distinct 11) | 4/8 [21.5, 78.5] (distinct 8) | games 5 (distinct 5) |

Opening rows (draws, use share, mirror games against other openings, wins, distinct games, interval, cross games):

| faction | opening | draws | use share | mirror games | distinct | wins | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai | skai_air | 52 | 0.241 | 24 | 24 | 4 | 16 | 16 |
| skai | skai_fast_expand | 51 | 0.236 | 32 | 32 | 22 | 13 | 13 |
| skai | skai_standard | 80 | 0.370 | 39 | 39 | 22 | 31 | 31 |
| skai | skai_windrunner_pressure | 33 | 0.153 | 17 | 17 | 8 | 12 | 12 |
| water | water_fast_expand | 56 | 0.259 | 29 | 29 | 10 | 13 | 0 |
| water | water_grasper_pressure | 43 | 0.199 | 27 | 27 | 17 | 12 | 0 |
| water | water_leviathan_tech | 50 | 0.231 | 24 | 24 | 10 | 22 | 0 |
| water | water_standard | 67 | 0.310 | 32 | 32 | 19 | 25 | 0 |

## Unit use

Share of the faction's slot-games that trained (or completed) the definition at least once.

| faction | definition | used | slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | skai.perch_depot | 216 | 216 | 1.000 |
| skai | skai.perch_tower | 195 | 216 | 0.903 |
| skai | skai.roc_rider | 216 | 216 | 1.000 |
| skai | skai.roost | 216 | 216 | 1.000 |
| skai | skai.storm_spire | 213 | 216 | 0.986 |
| skai | skai.stormcaller | 120 | 216 | 0.556 |
| skai | skai.talon_archer | 216 | 216 | 1.000 |
| skai | skai.thunderhead | 201 | 216 | 0.931 |
| skai | skai.wind_baffle | 0 | 216 | 0.000 |
| skai | skai.wind_gate | 0 | 216 | 0.000 |
| skai | skai.windrunner | 216 | 216 | 1.000 |
| skai | skai.windspire | 216 | 216 | 1.000 |
| water | water.coral_gate | 0 | 216 | 0.000 |
| water | water.coral_turret | 216 | 216 | 1.000 |
| water | water.coral_wall | 0 | 216 | 0.000 |
| water | water.grasper | 216 | 216 | 1.000 |
| water | water.ink_lobber | 185 | 216 | 0.856 |
| water | water.leviathan | 161 | 216 | 0.745 |
| water | water.pearl_depot | 216 | 216 | 1.000 |
| water | water.reef_runner | 216 | 216 | 1.000 |
| water | water.reef_workshop | 211 | 216 | 0.977 |
| water | water.shell_forge | 216 | 216 | 1.000 |
| water | water.shellback | 216 | 216 | 1.000 |
| water | water.spoutling | 210 | 216 | 0.972 |
| water | water.tidecaller | 0 | 216 | 0.000 |
| water | water.tidepool | 216 | 216 | 1.000 |

## Mechanic use

Share of the faction's applicable slot-games with at least one use.

| faction | mechanic | used | applicable slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | downdraft | 0 | 216 | 0.000 |
| water | wave | 0 | 216 | 0.000 |
| water | drench | 66 | 216 | 0.306 |
| skai | splash | 198 | 216 | 0.917 |
| water | splash | 196 | 216 | 0.907 |
| water | shield_dome | 215 | 216 | 0.995 |
| skai | momentum | 216 | 216 | 1.000 |
| water | anti_air_on_water | 55 | 216 | 0.255 |
| water | flood_source | 216 | 216 | 1.000 |
| skai | wall | 0 | 216 | 0.000 |
| water | wall | 0 | 216 | 0.000 |
| skai | gate | 0 | 216 | 0.000 |
| water | gate | 0 | 216 | 0.000 |
| skai | static_defence | 195 | 216 | 0.903 |
| water | static_defence | 216 | 216 | 1.000 |
| skai | expansion | 216 | 216 | 1.000 |
| water | expansion | 216 | 216 | 1.000 |
| skai | rare | 216 | 216 | 1.000 |
| water | rare | 214 | 216 | 0.991 |
| skai | hq_move | 193 | 216 | 0.894 |
| water | hq_move | 153 | 216 | 0.708 |
| skai | hq_last_stand | 0 | 216 | 0.000 |
| water | hq_last_stand | 0 | 216 | 0.000 |
| skai | core_capture | 216 | 216 | 1.000 |
| water | core_capture | 216 | 216 | 1.000 |
| skai | cycle_pick | 70 | 216 | 0.324 |
| water | cycle_pick | 87 | 216 | 0.403 |

Not measurable as "used" from events or state today:

- focus fire, kiting and retreats: AI micro; only its commands show it, no event or state does.
- regeneration and Windstorm range: passive or global, not a choice.
- tech choices as such: covered by the unit use table: each tech structure is a definition.

## Unspent resources

Lower median of the bank over the slot-games still running at the time (n).

| faction | time (s) | n | common | rare |
| --- | ---: | ---: | ---: | ---: |
| skai | 300 | 216 | 320 | 165 |
| skai | 600 | 100 | 275 | 235 |
| skai | 900 | 0 | - | - |
| water | 300 | 216 | 285 | 80 |
| water | 600 | 114 | 80 | 310 |
| water | 900 | 28 | 90 | 950 |

## Trust checks

Bars are placeholders for the developer (balance_report.json).

    CHECK balance_use FAIL 13 of 53 items below 0.100: skai/skai.wind_baffle 0.000, skai/skai.wind_gate 0.000, water/water.coral_gate 0.000, water/water.coral_wall 0.000, water/water.tidecaller 0.000, skai/downdraft 0.000, water/wave 0.000, skai/wall 0.000, water/wall 0.000, skai/gate 0.000, water/gate 0.000, skai/hq_last_stand 0.000, water/hq_last_stand 0.000
    CHECK balance_predictor FAIL accuracy=0.735 correct=562 decided=765 bar=0.850
    CHECK balance_unspent FAIL over bars common=400 rare=150: skai@300s common=320 rare=165, skai@600s common=275 rare=235, water@600s common=80 rare=310, water@900s common=90 rare=950
    CHECK balance_openings PASS openings judged=8 all within 0.100 to 0.900 of distinct mirror games (at least 10)

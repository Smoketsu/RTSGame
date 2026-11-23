<!-- balance.md: written by the balance harness --balance (rts/tools/BalanceHarness/BalanceRunner.cs); not a source file. -->
# Balance report: aggressive_s5_m

- Profile aggressive_s5_m plays both slots; base seed 20261121, 72 games per cell, game cap 2400 s; factions swap on odd games.
- Content digest d47f86bff68b781d; 216 games, 215 distinct decided games, 1 undecided, 0 draws.
- Intervals are Wilson at z = 1.96 over distinct games (play signature); a cell shows wins/decided games.

## Faction matrix

Row faction's wins against the column faction; the diagonal is slot 0's win share in the mirror (seat).

| | skai | water |
| --- | --- | --- |
| skai | seat 33/72 [34.8, 57.3] (distinct 72) | 72/72 [94.9, 100.0] (distinct 72) |
| water | 0/72 [0.0, 5.1] (distinct 72) | seat 31/71 [32.7, 55.2] (distinct 71) |

## Opening matrix

### skai (rows) against skai (columns)

| | skai_air | skai_fast_expand | skai_standard | skai_windrunner_pressure |
| --- | --- | --- | --- | --- |
| skai_air | games 6 (distinct 6) | 4/8 [21.5, 78.5] (distinct 8) | 9/13 [42.4, 87.3] (distinct 13) | 0/3 [0.0, 56.2] (distinct 3) |
| skai_fast_expand | 4/8 [21.5, 78.5] (distinct 8) | games 3 (distinct 3) | 8/18 [24.6, 66.3] (distinct 18) | 2/6 [9.7, 70.0] (distinct 6) |
| skai_standard | 4/13 [12.7, 57.6] (distinct 13) | 10/18 [33.7, 75.4] (distinct 18) | games 5 (distinct 5) | 4/8 [21.5, 78.5] (distinct 8) |
| skai_windrunner_pressure | 3/3 [43.8, 100.0] (distinct 3) | 4/6 [30.0, 90.3] (distinct 6) | 4/8 [21.5, 78.5] (distinct 8) | games 2 (distinct 2) |

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
| water_fast_expand | games 7 (distinct 7) | 3/8 [13.7, 69.4] (distinct 8) | 3/8 [13.7, 69.4] (distinct 8) | 7/13 [29.1, 76.8] (distinct 13) |
| water_grasper_pressure | 5/8 [30.6, 86.3] (distinct 8) | games 2 (distinct 2) | 4/8 [21.5, 78.5] (distinct 8) | 8/11 [43.4, 90.3] (distinct 11) |
| water_leviathan_tech | 5/8 [30.6, 86.3] (distinct 8) | 4/8 [21.5, 78.5] (distinct 8) | games 1 (distinct 1) | 5/8 [30.6, 86.3] (distinct 8) |
| water_standard | 6/13 [23.2, 70.9] (distinct 13) | 3/11 [9.7, 56.6] (distinct 11) | 3/8 [13.7, 69.4] (distinct 8) | games 5 (distinct 5) |

Opening rows (draws, use share, mirror games against other openings, wins, distinct games, interval, cross games):

| faction | opening | draws | use share | mirror games | distinct | wins | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai | skai_air | 52 | 0.241 | 24 | 24 | 13 | 16 | 16 |
| skai | skai_fast_expand | 51 | 0.236 | 32 | 32 | 14 | 13 | 13 |
| skai | skai_standard | 80 | 0.370 | 39 | 39 | 18 | 31 | 31 |
| skai | skai_windrunner_pressure | 33 | 0.153 | 17 | 17 | 11 | 12 | 12 |
| water | water_fast_expand | 56 | 0.259 | 29 | 29 | 13 | 13 | 0 |
| water | water_grasper_pressure | 43 | 0.199 | 27 | 27 | 17 | 12 | 0 |
| water | water_leviathan_tech | 50 | 0.231 | 24 | 24 | 14 | 22 | 0 |
| water | water_standard | 67 | 0.310 | 32 | 32 | 12 | 25 | 0 |

## Unit use

Share of the faction's slot-games that trained (or completed) the definition at least once.

| faction | definition | used | slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | skai.perch_depot | 216 | 216 | 1.000 |
| skai | skai.perch_tower | 185 | 216 | 0.856 |
| skai | skai.roc_rider | 216 | 216 | 1.000 |
| skai | skai.roost | 216 | 216 | 1.000 |
| skai | skai.storm_spire | 216 | 216 | 1.000 |
| skai | skai.stormcaller | 178 | 216 | 0.824 |
| skai | skai.talon_archer | 216 | 216 | 1.000 |
| skai | skai.thunderhead | 129 | 216 | 0.597 |
| skai | skai.wind_baffle | 169 | 216 | 0.782 |
| skai | skai.wind_gate | 97 | 216 | 0.449 |
| skai | skai.windrunner | 216 | 216 | 1.000 |
| skai | skai.windspire | 216 | 216 | 1.000 |
| skai | skai.stormcaller_plans | 215 | 216 | 0.995 |
| skai | skai.thunderhead_plans | 215 | 216 | 0.995 |
| water | water.coral_gate | 116 | 216 | 0.537 |
| water | water.coral_turret | 216 | 216 | 1.000 |
| water | water.coral_wall | 173 | 216 | 0.801 |
| water | water.grasper | 216 | 216 | 1.000 |
| water | water.ink_lobber | 134 | 216 | 0.620 |
| water | water.leviathan | 165 | 216 | 0.764 |
| water | water.pearl_depot | 216 | 216 | 1.000 |
| water | water.reef_runner | 216 | 216 | 1.000 |
| water | water.reef_workshop | 215 | 216 | 0.995 |
| water | water.shell_forge | 216 | 216 | 1.000 |
| water | water.shellback | 216 | 216 | 1.000 |
| water | water.spoutling | 213 | 216 | 0.986 |
| water | water.tidecaller | 211 | 216 | 0.977 |
| water | water.tidepool | 216 | 216 | 1.000 |
| water | water.ink_lobber_plans | 212 | 216 | 0.981 |
| water | water.leviathan_plans | 212 | 216 | 0.981 |

## Mechanic use

Share of the faction's applicable slot-games with at least one use.

| faction | mechanic | used | applicable slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | downdraft | 125 | 144 | 0.868 |
| water | wave | 167 | 216 | 0.773 |
| water | drench | 63 | 216 | 0.292 |
| skai | splash | 191 | 216 | 0.884 |
| water | splash | 179 | 216 | 0.829 |
| water | shield_dome | 216 | 216 | 1.000 |
| skai | momentum | 216 | 216 | 1.000 |
| water | anti_air_on_water | 47 | 216 | 0.218 |
| water | flood_source | 216 | 216 | 1.000 |
| skai | wall | 169 | 216 | 0.782 |
| water | wall | 173 | 216 | 0.801 |
| skai | gate | 97 | 216 | 0.449 |
| water | gate | 116 | 216 | 0.537 |
| skai | static_defence | 185 | 216 | 0.856 |
| water | static_defence | 216 | 216 | 1.000 |
| skai | expansion | 216 | 216 | 1.000 |
| water | expansion | 216 | 216 | 1.000 |
| skai | rare | 216 | 216 | 1.000 |
| water | rare | 216 | 216 | 1.000 |
| skai | hq_move | 191 | 216 | 0.884 |
| water | hq_move | 145 | 216 | 0.671 |
| skai | hq_last_stand | 0 | 216 | 0.000 |
| water | hq_last_stand | 0 | 216 | 0.000 |
| skai | core_capture | 216 | 216 | 1.000 |
| water | core_capture | 216 | 216 | 1.000 |
| skai | cycle_pick | 84 | 216 | 0.389 |
| water | cycle_pick | 121 | 216 | 0.560 |

Not measurable as "used" from events or state today:

- focus fire, kiting and retreats: AI micro; only its commands show it, no event or state does.
- regeneration and Windstorm range: passive or global, not a choice.
- tech choices as such: covered by the unit use table: each tech structure is a definition.

## Unspent resources

Lower median of the bank over the slot-games still running at the time (n).

| faction | time (s) | n | common | rare |
| --- | ---: | ---: | ---: | ---: |
| skai | 300 | 216 | 305 | 205 |
| skai | 600 | 81 | 95 | 270 |
| skai | 900 | 6 | 15 | 170 |
| water | 300 | 216 | 650 | 150 |
| water | 600 | 123 | 60 | 295 |
| water | 900 | 26 | 45 | 990 |

## Production and defence metrics

Median over the cell's slot-games (the mean of the two middle values for an even count); cells are the slot's faction in a cross or a mirror matchup. Window 600 s; raid radius 12 tiles; income 240-300 is the sum of the income buckets covering 240 to 300 s (slot-games whose last bucket had not started are left out).

| cell | slot-games | worker_losses | worker_fed_losses | raid_seconds | raid_unanswered_edge_seconds | bank_mean | bank at 300 s | income 240-300 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 72 | 0 | 0 | 10 | 0.5 | 1232 | 662.5 | 1847.5 |
| skai mirror | 144 | 14.5 | 3 | 89.5 | 9 | 211 | 192.5 | 1815 |
| water cross | 72 | 31 | 6 | 127.5 | 8 | 301.5 | 545 | 1645 |
| water mirror | 144 | 11 | 1 | 98 | 10 | 551.5 | 695 | 1767.5 |

Overrun cut (reported only, not a bar): overrun_second is the first second at which enemy army supply near the slot's base reached 20 (median over the slot-games that reached it); the pre-overrun counts are the losses before it (all losses when never).

| cell | slot-games | reached overrun | overrun_second | worker_losses_pre_overrun | worker_fed_losses_pre_overrun |
| --- | ---: | ---: | ---: | ---: | ---: |
| skai cross | 72 | 12 | 395.5 | 0 | 0 |
| skai mirror | 144 | 85 | 430 | 7 | 2 |
| water cross | 72 | 68 | 426.5 | 9 | 2 |
| water mirror | 144 | 104 | 439 | 3 | 0 |

Samples (null after the game's end are left out). The last two columns are the median of producers x 1000 / income_60 and of capacity x 60 / income_60 per slot-game (integer division; slot-games with income_60 0 left out).

| cell | time (s) | n | producers | capacity | income_60 | army_supply | producers x 1000 / income_60 | capacity x 60 / income_60 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 180 | 72 | 4 | 1050 | 1385 | 12.5 | 2 | 44.5 |
| skai cross | 300 | 72 | 6 | 1800 | 1847.5 | 26.5 | 3 | 61 |
| skai cross | 450 | 70 | 11 | 3300 | 2400 | 59.5 | 4 | 85 |
| skai mirror | 180 | 144 | 4 | 1050 | 1405 | 13 | 2 | 43 |
| skai mirror | 300 | 144 | 6 | 1800 | 1815 | 22 | 3 | 59 |
| skai mirror | 450 | 140 | 10 | 3000 | 2027.5 | 42 | 4 | 89 |
| water cross | 180 | 72 | 4 | 1374 | 1337.5 | 12 | 2 | 59 |
| water cross | 300 | 72 | 5 | 1707 | 1645 | 19 | 3 | 70 |
| water cross | 450 | 70 | 9 | 3039 | 1905 | 21.5 | 5 | 102 |
| water mirror | 180 | 144 | 4 | 1374 | 1387.5 | 14 | 2 | 59 |
| water mirror | 300 | 144 | 6 | 2040 | 1767.5 | 26 | 3 | 69.5 |
| water mirror | 450 | 144 | 11 | 3705 | 2150 | 52 | 5 | 105 |

## Trust checks

Bars are placeholders for the developer (balance_report.json).

    CHECK balance_use FAIL 2 of 57 items below 0.100: skai/hq_last_stand 0.000, water/hq_last_stand 0.000
    CHECK balance_predictor FAIL accuracy=0.735 correct=562 decided=765 bar=0.850
    CHECK balance_unspent FAIL over bars common=400 rare=150: skai@300s common=305 rare=205, skai@600s common=95 rare=270, skai@900s common=15 rare=170, water@300s common=650 rare=150, water@600s common=60 rare=295, water@900s common=45 rare=990
    CHECK balance_openings PASS openings judged=8 all within 0.100 to 0.900 of distinct mirror games (at least 10)

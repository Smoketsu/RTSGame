<!-- balance.md: written by the balance harness --balance (rts/tools/BalanceHarness/BalanceRunner.cs); not a source file. -->
# Balance report: aggressive_s5_pd

- Profile aggressive_s5_pd plays both slots; base seed 20261121, 72 games per cell, game cap 2400 s; factions swap on odd games.
- Content digest d47f86bff68b781d; 216 games, 215 distinct decided games, 1 undecided, 0 draws.
- Intervals are Wilson at z = 1.96 over distinct games (play signature); a cell shows wins/decided games.

## Faction matrix

Row faction's wins against the column faction; the diagonal is slot 0's win share in the mirror (seat).

| | skai | water |
| --- | --- | --- |
| skai | seat 41/71 [46.2, 68.5] (distinct 71) | 72/72 [94.9, 100.0] (distinct 72) |
| water | 0/72 [0.0, 5.1] (distinct 72) | seat 29/72 [29.7, 51.8] (distinct 72) |

## Opening matrix

### skai (rows) against skai (columns)

| | skai_air | skai_fast_expand | skai_standard | skai_windrunner_pressure |
| --- | --- | --- | --- | --- |
| skai_air | games 6 (distinct 6) | 5/8 [30.6, 86.3] (distinct 8) | 2/13 [4.3, 42.2] (distinct 13) | 0/3 [0.0, 56.2] (distinct 3) |
| skai_fast_expand | 3/8 [13.7, 69.4] (distinct 8) | games 3 (distinct 3) | 13/17 [52.7, 90.4] (distinct 17) | 3/6 [18.8, 81.2] (distinct 6) |
| skai_standard | 11/13 [57.8, 95.7] (distinct 13) | 4/17 [9.6, 47.3] (distinct 17) | games 5 (distinct 5) | 0/8 [0.0, 32.4] (distinct 8) |
| skai_windrunner_pressure | 3/3 [43.8, 100.0] (distinct 3) | 3/6 [18.8, 81.2] (distinct 6) | 8/8 [67.6, 100.0] (distinct 8) | games 2 (distinct 2) |

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
| water_fast_expand | games 7 (distinct 7) | 4/8 [21.5, 78.5] (distinct 8) | 6/8 [40.9, 92.9] (distinct 8) | 8/13 [35.5, 82.3] (distinct 13) |
| water_grasper_pressure | 4/8 [21.5, 78.5] (distinct 8) | games 2 (distinct 2) | 2/8 [7.1, 59.1] (distinct 8) | 8/11 [43.4, 90.3] (distinct 11) |
| water_leviathan_tech | 2/8 [7.1, 59.1] (distinct 8) | 6/8 [40.9, 92.9] (distinct 8) | games 2 (distinct 2) | 6/8 [40.9, 92.9] (distinct 8) |
| water_standard | 5/13 [17.7, 64.5] (distinct 13) | 3/11 [9.7, 56.6] (distinct 11) | 2/8 [7.1, 59.1] (distinct 8) | games 5 (distinct 5) |

Opening rows (draws, use share, mirror games against other openings, wins, distinct games, interval, cross games):

| faction | opening | draws | use share | mirror games | distinct | wins | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai | skai_air | 52 | 0.241 | 24 | 24 | 7 | 16 | 16 |
| skai | skai_fast_expand | 51 | 0.236 | 31 | 31 | 19 | 13 | 13 |
| skai | skai_standard | 80 | 0.370 | 38 | 38 | 15 | 31 | 31 |
| skai | skai_windrunner_pressure | 33 | 0.153 | 17 | 17 | 14 | 12 | 12 |
| water | water_fast_expand | 56 | 0.259 | 29 | 29 | 18 | 13 | 0 |
| water | water_grasper_pressure | 43 | 0.199 | 27 | 27 | 14 | 12 | 0 |
| water | water_leviathan_tech | 50 | 0.231 | 24 | 24 | 14 | 22 | 0 |
| water | water_standard | 67 | 0.310 | 32 | 32 | 10 | 25 | 0 |

## Unit use

Share of the faction's slot-games that trained (or completed) the definition at least once.

| faction | definition | used | slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | skai.perch_depot | 216 | 216 | 1.000 |
| skai | skai.perch_tower | 166 | 216 | 0.769 |
| skai | skai.roc_rider | 212 | 216 | 0.981 |
| skai | skai.roost | 216 | 216 | 1.000 |
| skai | skai.storm_spire | 216 | 216 | 1.000 |
| skai | skai.stormcaller | 136 | 216 | 0.630 |
| skai | skai.talon_archer | 216 | 216 | 1.000 |
| skai | skai.thunderhead | 136 | 216 | 0.630 |
| skai | skai.wind_baffle | 169 | 216 | 0.782 |
| skai | skai.wind_gate | 97 | 216 | 0.449 |
| skai | skai.windrunner | 216 | 216 | 1.000 |
| skai | skai.windspire | 216 | 216 | 1.000 |
| skai | skai.stormcaller_plans | 209 | 216 | 0.968 |
| skai | skai.thunderhead_plans | 204 | 216 | 0.944 |
| water | water.coral_gate | 115 | 216 | 0.532 |
| water | water.coral_turret | 214 | 216 | 0.991 |
| water | water.coral_wall | 173 | 216 | 0.801 |
| water | water.grasper | 215 | 216 | 0.995 |
| water | water.ink_lobber | 78 | 216 | 0.361 |
| water | water.leviathan | 138 | 216 | 0.639 |
| water | water.pearl_depot | 216 | 216 | 1.000 |
| water | water.reef_runner | 216 | 216 | 1.000 |
| water | water.reef_workshop | 214 | 216 | 0.991 |
| water | water.shell_forge | 216 | 216 | 1.000 |
| water | water.shellback | 216 | 216 | 1.000 |
| water | water.spoutling | 200 | 216 | 0.926 |
| water | water.tidecaller | 207 | 216 | 0.958 |
| water | water.tidepool | 216 | 216 | 1.000 |
| water | water.ink_lobber_plans | 195 | 216 | 0.903 |
| water | water.leviathan_plans | 194 | 216 | 0.898 |

## Mechanic use

Share of the faction's applicable slot-games with at least one use.

| faction | mechanic | used | applicable slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | downdraft | 100 | 140 | 0.714 |
| water | wave | 167 | 216 | 0.773 |
| water | drench | 50 | 216 | 0.231 |
| skai | splash | 158 | 216 | 0.731 |
| water | splash | 136 | 216 | 0.630 |
| water | shield_dome | 215 | 216 | 0.995 |
| skai | momentum | 216 | 216 | 1.000 |
| water | anti_air_on_water | 52 | 216 | 0.241 |
| water | flood_source | 216 | 216 | 1.000 |
| skai | wall | 169 | 216 | 0.782 |
| water | wall | 173 | 216 | 0.801 |
| skai | gate | 97 | 216 | 0.449 |
| water | gate | 115 | 216 | 0.532 |
| skai | static_defence | 166 | 216 | 0.769 |
| water | static_defence | 214 | 216 | 0.991 |
| skai | expansion | 216 | 216 | 1.000 |
| water | expansion | 216 | 216 | 1.000 |
| skai | rare | 216 | 216 | 1.000 |
| water | rare | 216 | 216 | 1.000 |
| skai | hq_move | 176 | 216 | 0.815 |
| water | hq_move | 126 | 216 | 0.583 |
| skai | hq_last_stand | 0 | 216 | 0.000 |
| water | hq_last_stand | 0 | 216 | 0.000 |
| skai | core_capture | 216 | 216 | 1.000 |
| water | core_capture | 216 | 216 | 1.000 |
| skai | cycle_pick | 77 | 216 | 0.356 |
| water | cycle_pick | 110 | 216 | 0.509 |

Not measurable as "used" from events or state today:

- focus fire, kiting and retreats: AI micro; only its commands show it, no event or state does.
- regeneration and Windstorm range: passive or global, not a choice.
- tech choices as such: covered by the unit use table: each tech structure is a definition.

## Unspent resources

Lower median of the bank over the slot-games still running at the time (n).

| faction | time (s) | n | common | rare |
| --- | ---: | ---: | ---: | ---: |
| skai | 300 | 216 | 50 | 200 |
| skai | 600 | 47 | 60 | 265 |
| skai | 900 | 6 | 55 | 165 |
| water | 300 | 216 | 55 | 130 |
| water | 600 | 101 | 55 | 620 |
| water | 900 | 24 | 50 | 965 |

## Production and defence metrics

Median over the cell's slot-games (the mean of the two middle values for an even count); cells are the slot's faction in a cross or a mirror matchup. Window 600 s; raid radius 12 tiles; income 240-300 is the sum of the income buckets covering 240 to 300 s (slot-games whose last bucket had not started are left out).

| cell | slot-games | worker_losses | worker_fed_losses | raid_seconds | raid_unanswered_edge_seconds | bank_mean | bank at 300 s | income 240-300 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 72 | 0 | 0 | 8 | 1 | 66.5 | 55 | 1815 |
| skai mirror | 144 | 11 | 2 | 84.5 | 7 | 62 | 45 | 1715 |
| water cross | 72 | 26 | 6 | 120 | 8.5 | 69.5 | 47.5 | 1317.5 |
| water mirror | 144 | 7 | 1 | 100 | 7 | 68 | 60 | 1750 |

Samples (null after the game's end are left out). The last two columns are the median of producers x 1000 / income_60 and of capacity x 60 / income_60 per slot-game (integer division; slot-games with income_60 0 left out).

| cell | time (s) | n | producers | capacity | income_60 | army_supply | producers x 1000 / income_60 | capacity x 60 / income_60 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 180 | 72 | 4 | 1050 | 1270 | 12 | 3 | 53.5 |
| skai cross | 300 | 72 | 8 | 2400 | 1815 | 28.5 | 4 | 80 |
| skai cross | 450 | 66 | 19 | 5700 | 2380 | 83 | 8 | 144 |
| skai mirror | 180 | 144 | 4 | 1050 | 1285 | 12 | 3 | 49 |
| skai mirror | 300 | 144 | 8 | 2400 | 1715 | 20.5 | 4 | 79 |
| skai mirror | 450 | 138 | 14 | 4200 | 2025 | 37 | 6 | 124 |
| water cross | 180 | 72 | 4 | 1374 | 1265 | 10 | 3 | 70 |
| water cross | 300 | 72 | 7 | 2373 | 1317.5 | 18 | 5 | 108.5 |
| water cross | 450 | 66 | 10 | 3372 | 1127.5 | 17.5 | 7 | 159.5 |
| water mirror | 180 | 144 | 4 | 1374 | 1225 | 12 | 3 | 71 |
| water mirror | 300 | 144 | 8 | 2706 | 1750 | 28 | 4 | 91 |
| water mirror | 450 | 144 | 14 | 4704 | 2015 | 54 | 6.5 | 140.5 |

## Trust checks

Bars are placeholders for the developer (balance_report.json).

    CHECK balance_use FAIL 2 of 57 items below 0.100: skai/hq_last_stand 0.000, water/hq_last_stand 0.000
    CHECK balance_predictor FAIL accuracy=0.735 correct=562 decided=765 bar=0.850
    CHECK balance_unspent FAIL over bars common=400 rare=150: skai@300s common=50 rare=200, skai@600s common=60 rare=265, skai@900s common=55 rare=165, water@600s common=55 rare=620, water@900s common=50 rare=965
    CHECK balance_openings PASS openings judged=8 all within 0.100 to 0.900 of distinct mirror games (at least 10)

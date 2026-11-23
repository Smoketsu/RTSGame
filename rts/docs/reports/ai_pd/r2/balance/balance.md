<!-- balance.md: written by the balance harness --balance (rts/tools/BalanceHarness/BalanceRunner.cs); not a source file. -->
# Balance report: aggressive_s5_pd

- Profile aggressive_s5_pd plays both slots; base seed 20261121, 72 games per cell, game cap 2400 s; factions swap on odd games.
- Content digest d47f86bff68b781d; 216 games, 216 distinct decided games, 0 undecided, 0 draws.
- Intervals are Wilson at z = 1.96 over distinct games (play signature); a cell shows wins/decided games.

## Faction matrix

Row faction's wins against the column faction; the diagonal is slot 0's win share in the mirror (seat).

| | skai | water |
| --- | --- | --- |
| skai | seat 44/72 [49.6, 71.5] (distinct 72) | 72/72 [94.9, 100.0] (distinct 72) |
| water | 0/72 [0.0, 5.1] (distinct 72) | seat 36/72 [38.7, 61.3] (distinct 72) |

## Opening matrix

### skai (rows) against skai (columns)

| | skai_air | skai_fast_expand | skai_standard | skai_windrunner_pressure |
| --- | --- | --- | --- | --- |
| skai_air | games 6 (distinct 6) | 2/8 [7.1, 59.1] (distinct 8) | 2/13 [4.3, 42.2] (distinct 13) | 0/3 [0.0, 56.2] (distinct 3) |
| skai_fast_expand | 6/8 [40.9, 92.9] (distinct 8) | games 3 (distinct 3) | 16/18 [67.2, 96.9] (distinct 18) | 3/6 [18.8, 81.2] (distinct 6) |
| skai_standard | 11/13 [57.8, 95.7] (distinct 13) | 2/18 [3.1, 32.8] (distinct 18) | games 5 (distinct 5) | 0/8 [0.0, 32.4] (distinct 8) |
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
| water_fast_expand | games 7 (distinct 7) | 2/8 [7.1, 59.1] (distinct 8) | 6/8 [40.9, 92.9] (distinct 8) | 4/13 [12.7, 57.6] (distinct 13) |
| water_grasper_pressure | 6/8 [40.9, 92.9] (distinct 8) | games 2 (distinct 2) | 3/8 [13.7, 69.4] (distinct 8) | 9/11 [52.3, 94.9] (distinct 11) |
| water_leviathan_tech | 2/8 [7.1, 59.1] (distinct 8) | 5/8 [30.6, 86.3] (distinct 8) | games 2 (distinct 2) | 2/8 [7.1, 59.1] (distinct 8) |
| water_standard | 9/13 [42.4, 87.3] (distinct 13) | 2/11 [5.1, 47.7] (distinct 11) | 6/8 [40.9, 92.9] (distinct 8) | games 5 (distinct 5) |

Opening rows (draws, use share, mirror games against other openings, wins, distinct games, interval, cross games):

| faction | opening | draws | use share | mirror games | distinct | wins | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai | skai_air | 52 | 0.241 | 24 | 24 | 4 | 16 | 16 |
| skai | skai_fast_expand | 51 | 0.236 | 32 | 32 | 25 | 13 | 13 |
| skai | skai_standard | 80 | 0.370 | 39 | 39 | 13 | 31 | 31 |
| skai | skai_windrunner_pressure | 33 | 0.153 | 17 | 17 | 14 | 12 | 12 |
| water | water_fast_expand | 56 | 0.259 | 29 | 29 | 12 | 13 | 0 |
| water | water_grasper_pressure | 43 | 0.199 | 27 | 27 | 18 | 12 | 0 |
| water | water_leviathan_tech | 50 | 0.231 | 24 | 24 | 9 | 22 | 0 |
| water | water_standard | 67 | 0.310 | 32 | 32 | 17 | 25 | 0 |

## Unit use

Share of the faction's slot-games that trained (or completed) the definition at least once.

| faction | definition | used | slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | skai.perch_depot | 216 | 216 | 1.000 |
| skai | skai.perch_tower | 166 | 216 | 0.769 |
| skai | skai.roc_rider | 209 | 216 | 0.968 |
| skai | skai.roost | 216 | 216 | 1.000 |
| skai | skai.storm_spire | 216 | 216 | 1.000 |
| skai | skai.stormcaller | 148 | 216 | 0.685 |
| skai | skai.talon_archer | 216 | 216 | 1.000 |
| skai | skai.thunderhead | 117 | 216 | 0.542 |
| skai | skai.wind_baffle | 169 | 216 | 0.782 |
| skai | skai.wind_gate | 95 | 216 | 0.440 |
| skai | skai.windrunner | 216 | 216 | 1.000 |
| skai | skai.windspire | 216 | 216 | 1.000 |
| skai | skai.stormcaller_plans | 209 | 216 | 0.968 |
| skai | skai.thunderhead_plans | 203 | 216 | 0.940 |
| water | water.coral_gate | 113 | 216 | 0.523 |
| water | water.coral_turret | 211 | 216 | 0.977 |
| water | water.coral_wall | 173 | 216 | 0.801 |
| water | water.grasper | 215 | 216 | 0.995 |
| water | water.ink_lobber | 82 | 216 | 0.380 |
| water | water.leviathan | 136 | 216 | 0.630 |
| water | water.pearl_depot | 216 | 216 | 1.000 |
| water | water.reef_runner | 216 | 216 | 1.000 |
| water | water.reef_workshop | 213 | 216 | 0.986 |
| water | water.shell_forge | 216 | 216 | 1.000 |
| water | water.shellback | 216 | 216 | 1.000 |
| water | water.spoutling | 208 | 216 | 0.963 |
| water | water.tidecaller | 207 | 216 | 0.958 |
| water | water.tidepool | 216 | 216 | 1.000 |
| water | water.ink_lobber_plans | 189 | 216 | 0.875 |
| water | water.leviathan_plans | 189 | 216 | 0.875 |

## Mechanic use

Share of the faction's applicable slot-games with at least one use.

| faction | mechanic | used | applicable slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | downdraft | 101 | 137 | 0.737 |
| water | wave | 161 | 216 | 0.745 |
| water | drench | 51 | 216 | 0.236 |
| skai | splash | 162 | 216 | 0.750 |
| water | splash | 144 | 216 | 0.667 |
| water | shield_dome | 216 | 216 | 1.000 |
| skai | momentum | 216 | 216 | 1.000 |
| water | anti_air_on_water | 45 | 216 | 0.208 |
| water | flood_source | 216 | 216 | 1.000 |
| skai | wall | 169 | 216 | 0.782 |
| water | wall | 173 | 216 | 0.801 |
| skai | gate | 95 | 216 | 0.440 |
| water | gate | 113 | 216 | 0.523 |
| skai | static_defence | 166 | 216 | 0.769 |
| water | static_defence | 211 | 216 | 0.977 |
| skai | expansion | 216 | 216 | 1.000 |
| water | expansion | 216 | 216 | 1.000 |
| skai | rare | 216 | 216 | 1.000 |
| water | rare | 216 | 216 | 1.000 |
| skai | hq_move | 182 | 216 | 0.843 |
| water | hq_move | 131 | 216 | 0.606 |
| skai | hq_last_stand | 0 | 216 | 0.000 |
| water | hq_last_stand | 0 | 216 | 0.000 |
| skai | core_capture | 216 | 216 | 1.000 |
| water | core_capture | 216 | 216 | 1.000 |
| skai | cycle_pick | 74 | 216 | 0.343 |
| water | cycle_pick | 93 | 216 | 0.431 |

Not measurable as "used" from events or state today:

- focus fire, kiting and retreats: AI micro; only its commands show it, no event or state does.
- regeneration and Windstorm range: passive or global, not a choice.
- tech choices as such: covered by the unit use table: each tech structure is a definition.

## Unspent resources

Lower median of the bank over the slot-games still running at the time (n).

| faction | time (s) | n | common | rare |
| --- | ---: | ---: | ---: | ---: |
| skai | 300 | 216 | 55 | 200 |
| skai | 600 | 49 | 55 | 240 |
| skai | 900 | 4 | 20 | 185 |
| water | 300 | 216 | 60 | 115 |
| water | 600 | 85 | 60 | 540 |
| water | 900 | 16 | 60 | 960 |

## Production and defence metrics

Median over the cell's slot-games (the mean of the two middle values for an even count); cells are the slot's faction in a cross or a mirror matchup. Window 600 s; raid radius 12 tiles; income 240-300 is the sum of the income buckets covering 240 to 300 s (slot-games whose last bucket had not started are left out).

| cell | slot-games | worker_losses | worker_fed_losses | raid_seconds | raid_unanswered_edge_seconds | bank_mean | bank at 300 s | income 240-300 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 72 | 0 | 0 | 9 | 0 | 66 | 60 | 1832.5 |
| skai mirror | 144 | 13 | 2 | 86.5 | 8 | 62 | 52.5 | 1695 |
| water cross | 72 | 24.5 | 5 | 115.5 | 10 | 68.5 | 70 | 1292.5 |
| water mirror | 144 | 6 | 1 | 86 | 8 | 69.5 | 55 | 1682.5 |

Overrun cut (reported only, not a bar): overrun_second is the first second at which enemy army supply near the slot's base reached 20 (median over the slot-games that reached it); the pre-overrun counts are the losses before it (all losses when never).

| cell | slot-games | reached overrun | overrun_second | worker_losses_pre_overrun | worker_fed_losses_pre_overrun |
| --- | ---: | ---: | ---: | ---: | ---: |
| skai cross | 72 | 4 | 316 | 0 | 0 |
| skai mirror | 144 | 79 | 429 | 7 | 2 |
| water cross | 72 | 71 | 356 | 7.5 | 2 |
| water mirror | 144 | 102 | 395 | 2 | 0 |

Samples (null after the game's end are left out). The last two columns are the median of producers x 1000 / income_60 and of capacity x 60 / income_60 per slot-game (integer division; slot-games with income_60 0 left out).

| cell | time (s) | n | producers | capacity | income_60 | army_supply | producers x 1000 / income_60 | capacity x 60 / income_60 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 180 | 72 | 4 | 1200 | 1192.5 | 11 | 3 | 60 |
| skai cross | 300 | 72 | 8 | 2400 | 1832.5 | 27 | 4 | 81 |
| skai cross | 450 | 64 | 19 | 5700 | 2387.5 | 78.5 | 8 | 144.5 |
| skai mirror | 180 | 144 | 4 | 1050 | 1270 | 12 | 3 | 49 |
| skai mirror | 300 | 144 | 8 | 2350 | 1695 | 21 | 4 | 80 |
| skai mirror | 450 | 142 | 14 | 4200 | 1900 | 35 | 7 | 129 |
| water cross | 180 | 72 | 4 | 1374 | 1235 | 10 | 3 | 70 |
| water cross | 300 | 72 | 7 | 2373 | 1292.5 | 18.5 | 5 | 116.5 |
| water cross | 450 | 64 | 11 | 3705 | 1190 | 18 | 8 | 169.5 |
| water mirror | 180 | 144 | 4 | 1374 | 1145 | 12 | 3 | 73 |
| water mirror | 300 | 144 | 7 | 2373 | 1682.5 | 26 | 4 | 91 |
| water mirror | 450 | 144 | 14 | 4704 | 1957.5 | 51 | 7 | 145 |

## Trust checks

Bars are placeholders for the developer (balance_report.json).

    CHECK balance_use FAIL 2 of 57 items below 0.100: skai/hq_last_stand 0.000, water/hq_last_stand 0.000
    CHECK balance_predictor FAIL accuracy=0.735 correct=562 decided=765 bar=0.850
    CHECK balance_unspent FAIL over bars common=400 rare=150: skai@300s common=55 rare=200, skai@600s common=55 rare=240, skai@900s common=20 rare=185, water@600s common=60 rare=540, water@900s common=60 rare=960
    CHECK balance_openings PASS openings judged=8 all within 0.100 to 0.900 of distinct mirror games (at least 10)

<!-- balance.md: written by the balance harness --balance (rts/tools/BalanceHarness/BalanceRunner.cs); not a source file. -->
# Balance report: aggressive_s5

- Profile aggressive_s5 plays both slots; base seed 20261121, 72 games per cell, game cap 2400 s; factions swap on odd games.
- Content digest d47f86bff68b781d; 72 games, 72 distinct decided games, 0 undecided, 0 draws.
- Intervals are Wilson at z = 1.96 over distinct games (play signature); a cell shows wins/decided games.

## Faction matrix

Row faction's wins against the column faction; the diagonal is slot 0's win share in the mirror (seat).

| | skai | water |
| --- | --- | --- |
| skai | seat - | 72/72 [94.9, 100.0] (distinct 72) |
| water | 0/72 [0.0, 5.1] (distinct 72) | seat - |

## Opening matrix

### skai (rows) against skai (columns)

| | skai_air | skai_fast_expand | skai_standard | skai_windrunner_pressure |
| --- | --- | --- | --- | --- |
| skai_air | - | - | - | - |
| skai_fast_expand | - | - | - | - |
| skai_standard | - | - | - | - |
| skai_windrunner_pressure | - | - | - | - |

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
| water_fast_expand | - | - | - | - |
| water_grasper_pressure | - | - | - | - |
| water_leviathan_tech | - | - | - | - |
| water_standard | - | - | - | - |

Opening rows (draws, use share, mirror games against other openings, wins, distinct games, interval, cross games):

| faction | opening | draws | use share | mirror games | distinct | wins | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai | skai_air | 16 | 0.222 | 0 | 0 | 0 | 16 | 16 |
| skai | skai_fast_expand | 13 | 0.181 | 0 | 0 | 0 | 13 | 13 |
| skai | skai_standard | 31 | 0.431 | 0 | 0 | 0 | 31 | 31 |
| skai | skai_windrunner_pressure | 12 | 0.167 | 0 | 0 | 0 | 12 | 12 |
| water | water_fast_expand | 13 | 0.181 | 0 | 0 | 0 | 13 | 0 |
| water | water_grasper_pressure | 12 | 0.167 | 0 | 0 | 0 | 12 | 0 |
| water | water_leviathan_tech | 22 | 0.306 | 0 | 0 | 0 | 22 | 0 |
| water | water_standard | 25 | 0.347 | 0 | 0 | 0 | 25 | 0 |

## Unit use

Share of the faction's slot-games that trained (or completed) the definition at least once.

| faction | definition | used | slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | skai.perch_depot | 72 | 72 | 1.000 |
| skai | skai.perch_tower | 38 | 72 | 0.528 |
| skai | skai.roc_rider | 72 | 72 | 1.000 |
| skai | skai.roost | 72 | 72 | 1.000 |
| skai | skai.storm_spire | 72 | 72 | 1.000 |
| skai | skai.stormcaller | 39 | 72 | 0.542 |
| skai | skai.talon_archer | 72 | 72 | 1.000 |
| skai | skai.thunderhead | 46 | 72 | 0.639 |
| skai | skai.wind_baffle | 0 | 72 | 0.000 |
| skai | skai.wind_gate | 0 | 72 | 0.000 |
| skai | skai.windrunner | 72 | 72 | 1.000 |
| skai | skai.windspire | 72 | 72 | 1.000 |
| skai | skai.stormcaller_plans | 72 | 72 | 1.000 |
| skai | skai.thunderhead_plans | 70 | 72 | 0.972 |
| water | water.coral_gate | 0 | 72 | 0.000 |
| water | water.coral_turret | 72 | 72 | 1.000 |
| water | water.coral_wall | 0 | 72 | 0.000 |
| water | water.grasper | 72 | 72 | 1.000 |
| water | water.ink_lobber | 18 | 72 | 0.250 |
| water | water.leviathan | 17 | 72 | 0.236 |
| water | water.pearl_depot | 72 | 72 | 1.000 |
| water | water.reef_runner | 72 | 72 | 1.000 |
| water | water.reef_workshop | 71 | 72 | 0.986 |
| water | water.shell_forge | 72 | 72 | 1.000 |
| water | water.shellback | 72 | 72 | 1.000 |
| water | water.spoutling | 71 | 72 | 0.986 |
| water | water.tidecaller | 0 | 72 | 0.000 |
| water | water.tidepool | 72 | 72 | 1.000 |
| water | water.ink_lobber_plans | 65 | 72 | 0.903 |
| water | water.leviathan_plans | 61 | 72 | 0.847 |

## Mechanic use

Share of the faction's applicable slot-games with at least one use.

| faction | mechanic | used | applicable slot-games | share |
| --- | --- | ---: | ---: | ---: |
| water | wave | 0 | 72 | 0.000 |
| water | drench | 64 | 72 | 0.889 |
| skai | splash | 46 | 72 | 0.639 |
| water | splash | 28 | 72 | 0.389 |
| water | shield_dome | 72 | 72 | 1.000 |
| skai | momentum | 72 | 72 | 1.000 |
| water | anti_air_on_water | 46 | 72 | 0.639 |
| water | flood_source | 72 | 72 | 1.000 |
| skai | wall | 0 | 72 | 0.000 |
| water | wall | 0 | 72 | 0.000 |
| skai | gate | 0 | 72 | 0.000 |
| water | gate | 0 | 72 | 0.000 |
| skai | static_defence | 38 | 72 | 0.528 |
| water | static_defence | 72 | 72 | 1.000 |
| skai | expansion | 72 | 72 | 1.000 |
| water | expansion | 72 | 72 | 1.000 |
| skai | rare | 72 | 72 | 1.000 |
| water | rare | 72 | 72 | 1.000 |
| skai | hq_move | 70 | 72 | 0.972 |
| water | hq_move | 10 | 72 | 0.139 |
| skai | hq_last_stand | 0 | 72 | 0.000 |
| water | hq_last_stand | 0 | 72 | 0.000 |
| skai | core_capture | 72 | 72 | 1.000 |
| water | core_capture | 72 | 72 | 1.000 |
| skai | cycle_pick | 30 | 72 | 0.417 |
| water | cycle_pick | 5 | 72 | 0.069 |

Not measurable as "used" from events or state today:

- focus fire, kiting and retreats: AI micro; only its commands show it, no event or state does.
- regeneration and Windstorm range: passive or global, not a choice.
- tech choices as such: covered by the unit use table: each tech structure is a definition.

## Unspent resources

Lower median of the bank over the slot-games still running at the time (n).

| faction | time (s) | n | common | rare |
| --- | ---: | ---: | ---: | ---: |
| skai | 300 | 72 | 885 | 210 |
| skai | 600 | 2 | 3245 | 390 |
| skai | 900 | 0 | - | - |
| water | 300 | 72 | 320 | 125 |
| water | 600 | 2 | 25 | 235 |
| water | 900 | 0 | - | - |

## Production and defence metrics

Median over the cell's slot-games (the mean of the two middle values for an even count); cells are the slot's faction in a cross or a mirror matchup. Window 600 s; raid radius 12 tiles; income 240-300 is the sum of the income buckets covering 240 to 300 s (slot-games whose last bucket had not started are left out).

| cell | slot-games | worker_losses | worker_fed_losses | raid_seconds | raid_unanswered_edge_seconds | bank_mean | bank at 300 s | income 240-300 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 72 | 0 | 0 | 8 | 0 | 1294 | 885 | 1950 |
| water cross | 72 | 35 | 11 | 121 | 3.5 | 193 | 332.5 | 1682.5 |

Overrun cut (reported only, not a bar): overrun_second is the first second at which enemy army supply near the slot's base reached 20 (median over the slot-games that reached it); the pre-overrun counts are the losses before it (all losses when never).

| cell | slot-games | reached overrun | overrun_second | worker_losses_pre_overrun | worker_fed_losses_pre_overrun |
| --- | ---: | ---: | ---: | ---: | ---: |
| skai cross | 72 | 13 | 305 | 0 | 0 |
| water cross | 72 | 72 | 319.5 | 5 | 2 |

Samples (null after the game's end are left out). The last two columns are the median of producers x 1000 / income_60 and of capacity x 60 / income_60 per slot-game (integer division; slot-games with income_60 0 left out).

| cell | time (s) | n | producers | capacity | income_60 | army_supply | producers x 1000 / income_60 | capacity x 60 / income_60 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 180 | 72 | 4 | 1050 | 1385 | 12.5 | 2 | 44.5 |
| skai cross | 300 | 72 | 7 | 2100 | 1950 | 34 | 3 | 64 |
| skai cross | 450 | 67 | 12 | 3600 | 2380 | 69 | 5 | 90 |
| water cross | 180 | 72 | 4 | 1374 | 1337.5 | 12 | 2 | 59 |
| water cross | 300 | 72 | 7 | 2373 | 1682.5 | 20 | 4 | 82 |
| water cross | 450 | 67 | 8 | 2706 | 1570 | 9 | 5 | 111 |

## Trust checks

Bars are placeholders for the developer (balance_report.json).

    CHECK balance_use FAIL 13 of 56 items below 0.100: skai/skai.wind_baffle 0.000, skai/skai.wind_gate 0.000, water/water.coral_gate 0.000, water/water.coral_wall 0.000, water/water.tidecaller 0.000, water/wave 0.000, skai/wall 0.000, water/wall 0.000, skai/gate 0.000, water/gate 0.000, skai/hq_last_stand 0.000, water/hq_last_stand 0.000, water/cycle_pick 0.069
    CHECK balance_predictor FAIL accuracy=0.735 correct=562 decided=765 bar=0.850
    CHECK balance_unspent FAIL over bars common=400 rare=150: skai@300s common=885 rare=210, skai@600s common=3245 rare=390, water@600s common=25 rare=235
    CHECK balance_openings PASS openings judged=0 all within 0.100 to 0.900 of distinct mirror games (at least 10)

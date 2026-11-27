<!-- balance.md: written by the balance harness --balance (rts/tools/BalanceHarness/BalanceRunner.cs); not a source file. -->
# Balance report: aggressive_s5

- Profile aggressive_s5 plays both slots; base seed 20261203, 24 games per cell, game cap 2400 s; factions swap on odd games.
- Content digest c6fae931039c8e32; 48 games, 48 distinct decided games, 0 undecided, 0 draws.
- Intervals are Wilson at z = 1.96 over distinct games (play signature); a cell shows wins/decided games.

## Faction matrix

Row faction's wins against the column faction; the diagonal is slot 0's win share in the mirror (seat).

| | skai | water |
| --- | --- | --- |
| skai | seat 16/24 [46.7, 82.0] (distinct 24) | - |
| water | - | seat 11/24 [27.9, 64.9] (distinct 24) |

## Opening matrix

### skai (rows) against skai (columns)

| | skai_air | skai_fast_expand | skai_standard | skai_windrunner_pressure |
| --- | --- | --- | --- | --- |
| skai_air | - | 1/3 [6.1, 79.2] (distinct 3) | 3/3 [43.8, 100.0] (distinct 3) | 1/2 [9.5, 90.5] (distinct 2) |
| skai_fast_expand | 2/3 [20.8, 93.9] (distinct 3) | - | 4/6 [30.0, 90.3] (distinct 6) | 3/3 [43.8, 100.0] (distinct 3) |
| skai_standard | 0/3 [0.0, 56.2] (distinct 3) | 2/6 [9.7, 70.0] (distinct 6) | games 1 (distinct 1) | 1/4 [4.6, 69.9] (distinct 4) |
| skai_windrunner_pressure | 1/2 [9.5, 90.5] (distinct 2) | 0/3 [0.0, 56.2] (distinct 3) | 3/4 [30.1, 95.4] (distinct 4) | games 2 (distinct 2) |

### skai (rows) against water (columns)

| | water_fast_expand | water_grasper_pressure | water_leviathan_tech | water_standard |
| --- | --- | --- | --- | --- |
| skai_air | - | - | - | - |
| skai_fast_expand | - | - | - | - |
| skai_standard | - | - | - | - |
| skai_windrunner_pressure | - | - | - | - |

### water (rows) against water (columns)

| | water_fast_expand | water_grasper_pressure | water_leviathan_tech | water_standard |
| --- | --- | --- | --- | --- |
| water_fast_expand | games 1 (distinct 1) | 2/5 [11.8, 76.9] (distinct 5) | 0/4 [0.0, 49.0] (distinct 4) | 1/2 [9.5, 90.5] (distinct 2) |
| water_grasper_pressure | 3/5 [23.1, 88.2] (distinct 5) | - | 1/4 [4.6, 69.9] (distinct 4) | 1/4 [4.6, 69.9] (distinct 4) |
| water_leviathan_tech | 4/4 [51.0, 100.0] (distinct 4) | 3/4 [30.1, 95.4] (distinct 4) | games 2 (distinct 2) | 0/1 [0.0, 79.3] (distinct 1) |
| water_standard | 1/2 [9.5, 90.5] (distinct 2) | 3/4 [30.1, 95.4] (distinct 4) | 1/1 [20.7, 100.0] (distinct 1) | games 1 (distinct 1) |

Opening rows (draws, use share, mirror games against other openings, wins, distinct games, interval, cross games):

| faction | opening | draws | use share | mirror games | distinct | wins | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai | skai_air | 8 | 0.167 | 8 | 8 | 5 | 0 | 0 |
| skai | skai_fast_expand | 12 | 0.250 | 12 | 12 | 9 | 0 | 0 |
| skai | skai_standard | 15 | 0.313 | 13 | 13 | 3 | 0 | 0 |
| skai | skai_windrunner_pressure | 13 | 0.271 | 9 | 9 | 4 | 0 | 0 |
| water | water_fast_expand | 13 | 0.271 | 11 | 11 | 3 | 0 | 0 |
| water | water_grasper_pressure | 13 | 0.271 | 13 | 13 | 5 | 0 | 0 |
| water | water_leviathan_tech | 13 | 0.271 | 9 | 9 | 7 | 0 | 0 |
| water | water_standard | 9 | 0.188 | 7 | 7 | 5 | 0 | 0 |

## Unit use

Share of the faction's slot-games that trained (or completed) the definition at least once.

| faction | definition | used | slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | skai.perch_depot | 48 | 48 | 1.000 |
| skai | skai.perch_tower | 48 | 48 | 1.000 |
| skai | skai.roc_rider | 48 | 48 | 1.000 |
| skai | skai.roost | 48 | 48 | 1.000 |
| skai | skai.storm_spire | 48 | 48 | 1.000 |
| skai | skai.stormcaller | 5 | 48 | 0.104 |
| skai | skai.talon_archer | 48 | 48 | 1.000 |
| skai | skai.thunderhead | 33 | 48 | 0.688 |
| skai | skai.wind_baffle | 0 | 48 | 0.000 |
| skai | skai.wind_gate | 0 | 48 | 0.000 |
| skai | skai.windrunner | 48 | 48 | 1.000 |
| skai | skai.windspire | 48 | 48 | 1.000 |
| skai | skai.stormcaller_plans | 45 | 48 | 0.938 |
| skai | skai.thunderhead_plans | 48 | 48 | 1.000 |
| water | water.coral_gate | 0 | 48 | 0.000 |
| water | water.coral_turret | 47 | 48 | 0.979 |
| water | water.coral_wall | 0 | 48 | 0.000 |
| water | water.grasper | 48 | 48 | 1.000 |
| water | water.ink_lobber | 26 | 48 | 0.542 |
| water | water.leviathan | 46 | 48 | 0.958 |
| water | water.pearl_depot | 48 | 48 | 1.000 |
| water | water.reef_runner | 48 | 48 | 1.000 |
| water | water.reef_workshop | 48 | 48 | 1.000 |
| water | water.shell_forge | 48 | 48 | 1.000 |
| water | water.shellback | 48 | 48 | 1.000 |
| water | water.spoutling | 48 | 48 | 1.000 |
| water | water.tidecaller | 0 | 48 | 0.000 |
| water | water.tidepool | 48 | 48 | 1.000 |
| water | water.ink_lobber_plans | 48 | 48 | 1.000 |
| water | water.leviathan_plans | 48 | 48 | 1.000 |

## Mechanic use

Share of the faction's applicable slot-games with at least one use.

| faction | mechanic | used | applicable slot-games | share |
| --- | --- | ---: | ---: | ---: |
| skai | downdraft | 0 | 48 | 0.000 |
| water | wave | 0 | 48 | 0.000 |
| water | drench | 0 | 48 | 0.000 |
| skai | splash | 30 | 48 | 0.625 |
| water | splash | 46 | 48 | 0.958 |
| water | shield_dome | 48 | 48 | 1.000 |
| skai | momentum | 48 | 48 | 1.000 |
| water | anti_air_on_water | 0 | 48 | 0.000 |
| water | flood_source | 48 | 48 | 1.000 |
| skai | wall | 0 | 48 | 0.000 |
| water | wall | 0 | 48 | 0.000 |
| skai | gate | 0 | 48 | 0.000 |
| water | gate | 0 | 48 | 0.000 |
| skai | static_defence | 48 | 48 | 1.000 |
| water | static_defence | 47 | 48 | 0.979 |
| skai | expansion | 48 | 48 | 1.000 |
| water | expansion | 48 | 48 | 1.000 |
| skai | rare | 48 | 48 | 1.000 |
| water | rare | 48 | 48 | 1.000 |
| skai | hq_move | 44 | 48 | 0.917 |
| water | hq_move | 39 | 48 | 0.813 |
| skai | hq_last_stand | 0 | 48 | 0.000 |
| water | hq_last_stand | 0 | 48 | 0.000 |
| skai | core_capture | 48 | 48 | 1.000 |
| water | core_capture | 48 | 48 | 1.000 |
| skai | cycle_pick | 13 | 48 | 0.271 |
| water | cycle_pick | 36 | 48 | 0.750 |

Not measurable as "used" from events or state today:

- focus fire, kiting and retreats: AI micro; only its commands show it, no event or state does.
- regeneration and Windstorm range: passive or global, not a choice.
- tech choices as such: covered by the unit use table: each tech structure is a definition.

## Unspent resources

Lower median of the bank over the slot-games still running at the time (n).

| faction | time (s) | n | common | rare |
| --- | ---: | ---: | ---: | ---: |
| skai | 300 | 48 | 115 | 200 |
| skai | 600 | 18 | 70 | 245 |
| skai | 900 | 0 | - | - |
| water | 300 | 48 | 435 | 250 |
| water | 600 | 32 | 50 | 505 |
| water | 900 | 4 | 15 | 850 |

## Production and defence metrics

Median over the cell's slot-games (the mean of the two middle values for an even count); cells are the slot's faction in a cross or a mirror matchup. Window 600 s; raid radius 12 tiles; income 240-300 is the sum of the income buckets covering 240 to 300 s (slot-games whose last bucket had not started are left out).

| cell | slot-games | worker_losses | worker_fed_losses | raid_seconds | raid_unanswered_edge_seconds | bank_mean | bank at 300 s | income 240-300 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai mirror | 48 | 14 | 3 | 69 | 7.5 | 195.5 | 117.5 | 1845 |
| water mirror | 48 | 13 | 2 | 95 | 12 | 254 | 452.5 | 1802.5 |

Overrun cut (reported only, not a bar): overrun_second is the first second at which enemy army supply near the slot's base reached 20 (median over the slot-games that reached it); the pre-overrun counts are the losses before it (all losses when never).

| cell | slot-games | reached overrun | overrun_second | worker_losses_pre_overrun | worker_fed_losses_pre_overrun |
| --- | ---: | ---: | ---: | ---: | ---: |
| skai mirror | 48 | 33 | 405 | 5 | 1 |
| water mirror | 48 | 42 | 376 | 2 | 0 |

Samples (null after the game's end are left out). The last two columns are the median of producers x 1000 / income_60 and of capacity x 60 / income_60 per slot-game (integer division; slot-games with income_60 0 left out).

| cell | time (s) | n | producers | capacity | income_60 | army_supply | producers x 1000 / income_60 | capacity x 60 / income_60 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai mirror | 180 | 48 | 4 | 1050 | 1405 | 13 | 2 | 44 |
| skai mirror | 300 | 48 | 7 | 2100 | 1845 | 27 | 3 | 63.5 |
| skai mirror | 450 | 46 | 11 | 3175 | 1892.5 | 36.5 | 5 | 100 |
| water mirror | 180 | 48 | 3.5 | 1357.5 | 1390 | 14 | 2 | 57 |
| water mirror | 300 | 48 | 7 | 2523 | 1802.5 | 34 | 3 | 85 |
| water mirror | 450 | 48 | 12 | 4188 | 2182.5 | 54.5 | 5 | 113 |

## Trust checks

Bars are placeholders for the developer (balance_report.json).

    CHECK balance_use FAIL 15 of 57 items below 0.100: skai/skai.wind_baffle 0.000, skai/skai.wind_gate 0.000, water/water.coral_gate 0.000, water/water.coral_wall 0.000, water/water.tidecaller 0.000, skai/downdraft 0.000, water/wave 0.000, water/drench 0.000, water/anti_air_on_water 0.000, skai/wall 0.000, water/wall 0.000, skai/gate 0.000, water/gate 0.000, skai/hq_last_stand 0.000, water/hq_last_stand 0.000
    CHECK balance_predictor FAIL accuracy=0.686 correct=525 decided=765 bar=0.850
    CHECK balance_unspent FAIL over bars common=400 rare=150: skai@300s common=115 rare=200, skai@600s common=70 rare=245, water@300s common=435 rare=250, water@600s common=50 rare=505, water@900s common=15 rare=850
    CHECK balance_openings PASS openings judged=4 all within 0.100 to 0.900 of distinct mirror games (at least 10)

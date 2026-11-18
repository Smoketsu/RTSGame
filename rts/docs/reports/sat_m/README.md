<!-- rts/docs/reports/sat_m/README.md -->
# SAT-M: re-baseline after field saturation

Package SAT-M (`rts/docs/specs/field_saturation.md`): measurement only. No code, profile, data or tuning change. Branch `review/sat-m`, head 25c4023 at the start (SAT merged on `main`, plus the review follow-ups). `prototype_1v1`, `-c Release`, `--jobs 4`, base seed 20261117 for ladders, 20261118 for fights. Pre-saturation values are quoted from `rts/docs/reports/ai_v4_rl/README.md` ("After package L-R", commit 5627271) and `rts/docs/reports/fights_2026-11-18/`. `rts/build` is git-ignored; the facts below were copied out of it. A record, not a fix: regressions are not explained or tuned here. Each run is added as it is written up.

## Run 1: self-ladder (`aggressive_s4r` against itself), 216 of 216 decided, 2812.1 s

Command (the output folder name `ladder_sat_self` and the profiles `aggressive_s4r` / `aggressive_s4r` are in `ladder.md` / `ladder.jsonl`; the surviving run was verified complete, 216 records, no rerun):

```
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s4r --profile-b aggressive_s4r --jobs 4 --out rts/build/ladder_sat_self
```

```
WARNING opening water/water_grasper_pressure: wins 3 of 18 mirror games against other openings (0.167, below 0.35).
WARNING opening water/water_leviathan_tech: wins 23 of 31 mirror games against other openings (0.742, above 0.65).
LADDER candidate=aggressive_s4r baseline=aggressive_s4r decided=216 wins=113 fraction=0.523 distinct=216 mirror=75/144 cross=38/72 bar=0.650 FAIL
```

Pre-saturation (same command, L-R section):

```
WARNING opening water/water_leviathan_tech: wins 21 of 31 mirror games against other openings (0.677, above 0.65).
LADDER candidate=aggressive_s4r baseline=aggressive_s4r decided=216 wins=107 fraction=0.495 distinct=216 mirror=70/144 cross=37/72 bar=0.650 FAIL
```

(The FAIL and exit code are meaningless in a self-ladder; the gate items are variety and openings.) The determinism spot check re-ran 2 games, 0 mismatches.

Distinct games per cell, computed from the counts (distinct play signatures among decided games in `ladder.jsonl`), not from the `ladder.md` "distinct fraction" column (that column prints the candidate win share):

| cell | decided | distinct | distinct / decided | before |
| --- | ---: | ---: | ---: | ---: |
| skai_vs_skai | 72 | 72 | 1.000 | 1.000 |
| skai_vs_water | 72 | 72 | 1.000 | 1.000 |
| water_vs_water | 72 | 72 | 1.000 | 1.000 |

Undecided games: 0 of 216 (before: 0). Draws: 0. Results: slot0 99, slot1 117. Median game length (20 ticks per second): all 736.4 s (min 338.6, max 1369.1); skai_vs_skai 799.7 s; skai_vs_water 447.7 s; water_vs_water 1027.1 s. No game reached the 2400 s cap. The pre-saturation median was not recorded in the earlier report and its `ladder.jsonl` did not survive, so there is no before value.

Per cell candidate wins (candidate = baseline = the same profile, so seat A):

| cell | candidate wins / decided | fraction | Wilson |
| --- | ---: | ---: | --- |
| skai_vs_skai | 38/72 | 0.528 | [41.4, 63.9] |
| skai_vs_water | 38/72 | 0.528 | [41.4, 63.9] |
| water_vs_water | 37/72 | 0.514 | [40.1, 62.6] |

Mirror and cross rows (before in the last column):

| row | decided | candidate wins | fraction | Wilson | before |
| --- | ---: | ---: | ---: | --- | --- |
| mirror | 144 | 75 | 0.521 | [44.0, 60.1] | 70/144 = 0.486, [40.6, 56.7] |
| cross | 72 | 38 | 0.528 | [41.4, 63.9] | 37/72 = 0.514 |
| cross, candidate as skai | 36 | 35 | 0.972 | [85.8, 99.5] | 36/36 |
| cross, candidate as water | 36 | 3 | 0.083 | [2.9, 21.8] | 1/36 |
| all | 216 | 113 | 0.523 | [45.7, 58.9] | 107/216 = 0.495 |
| seat 0 | 108 | 52 | 0.481 | [39.0, 57.5] | not recorded |
| seat 1 | 108 | 61 | 0.565 | [47.1, 65.5] | not recorded |

Openings (draws = times drawn; mirror = games against other openings of the same faction; win fraction over decided mirror games; use share = draws / 216). Before values from the L-R section.

| faction | opening | draws | use share | mirror games | wins | win fraction | Wilson | before (wins, fraction) | cross games | cross wins | before cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: |
| skai | skai_air | 57 | 0.264 | 22 | 13 | 0.591 | [38.7, 76.7] | 12, 0.545 | 19 | 19 | 19 |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 10 | 0.500 | [29.9, 70.1] | 9, 0.450 | 16 | 15 | 15 |
| skai | skai_standard | 76 | 0.352 | 28 | 11 | 0.393 | [23.6, 57.6] | 11, 0.393 | 24 | 22 | 24 |
| skai | skai_windrunner_pressure | 35 | 0.162 | 20 | 11 | 0.550 | [34.2, 74.2] | 13, 0.650 | 13 | 12 | 13 |
| water | water_fast_expand | 52 | 0.241 | 25 | 12 | 0.480 | [30.0, 66.5] | 11, 0.440 | 15 | 0 | 0 |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 3 | **0.167** | [5.8, 39.2] | 7, 0.389 | 20 | 0 | 0 |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 23 | **0.742** | [56.8, 86.3] | 21, 0.677 | 17 | 4 | 1 |
| water | water_standard | 60 | 0.278 | 32 | 15 | 0.469 | [30.9, 63.6] | 14, 0.438 | 20 | 0 | 0 |

Draws are identical to before (57, 48, 76, 35, 52, 42, 62, 60), as expected.

Gate items from this run (self-ladder):

| item | bar | result | verdict |
| --- | --- | --- | --- |
| variety, every cell | distinct / decided >= 0.8 | 1.000, 1.000, 1.000 (before 1.000 each) | PASS (unchanged) |
| use share | none above 0.40 | 0.162 to 0.352 | PASS (unchanged) |
| opening win fraction 0.35 to 0.65 (report only) | 8 openings in band | water_grasper_pressure 0.167 (before 0.389, in band), water_leviathan_tech 0.742 (before 0.677, above): 2 of 8 outside, before 1 of 8 (L-R: skai pressure on the bar at 0.650) | worse by one opening |

Files: raw `ladder.jsonl` and `ladder.md` were not committed (`rts/build`); the numbers above are the full content.

## Run 4: `--fights` (equal-value fights, expected identical to `reports/fights_2026-11-18`)

Command: `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --fights` (output surviving in `rts/build/fights_sat`, verified complete: 780 records in `fights.jsonl`, 60 in `hq_floor.jsonl`, `fights.md` written; the profile column is not part of fights).

```
FIGHTS fights=780 decided=765 draws=0 stalemates=13 unresolved=2 mismatched=0 checks_passed=2 of=8 dome_absorbed=12
```

Before (`fights_2026-11-18`): the identical line. Compared record by record (780 fights matched by ID): `result`, `end_tick`, `left0`, `left1`, `alive0`, `alive1` are identical in all 780; the only differing field is `state_hash`, in all 780 (the fight state hash includes the tuning values, and the tuning digest moved: tuning `a020a25c7d7c2eae` now, `8af17d045a9c63e6` in the 18 Nov report; content digest `9cb28f0929df518c` here against `b0014f3f65365bb8` in the report header, and the same `9cb28f0929df518c` in the diffed `fights.md` of the committed folder after matchups were filled). `fights.md` is identical apart from the tuning digest line; `hq_floor.jsonl` is byte-identical (60 records). The matchup table, the 8 design-rule checks (2 passing) and the stalemates are therefore unchanged: no worker gathers in a fight, as expected. Wall time was not recorded.

## Run 3: response scenarios (`--response`, report only; exit code 2 as before, the gate is a count)

Commands: `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --response --profile aggressive_s4r` and the same with `aggressive_s3`.

`aggressive_s4r`: `RESPONSE_SUMMARY passed=5 of=10` (before: `passed=5 of=10`). `aggressive_s3`: `RESPONSE_SUMMARY passed=5 of=10` (before: `passed=5 of=10`). Passing: roc rush Skai and Water, mass Windrunner Skai and Water, turtle Water, for both profiles; failing: mass Pummeler x2, worker raid x2, turtle Skai, for both profiles; the pass sets are the same as before. Counted gate (6 of 9, "fewer than baseline" fails the package): the count is not below its baseline.

Lines for `aggressive_s4r` as printed now (before, from the L-R section, in brackets where a number moved):

```
RESPONSE response_roc_rush_skai.json air_answer PASS p1=PASS air_first_seen_s=165.2 model_air_known_s=85.0 p2=PASS aa_units=20 p3=PASS hq_alive=true
RESPONSE response_roc_rush_water.json air_answer PASS p1=PASS air_first_seen_s=167.9 model_air_known_s=89.0 p2=PASS aa_units=5 p3=PASS hq_alive=true
RESPONSE response_mass_pummeler_skai.json armoured_answer FAIL p1=FAIL share_gain=-0.065 share=0.396 base_share=0.462 armour=armoured p2=PASS hq_alive=true
RESPONSE response_mass_pummeler_water.json armoured_answer FAIL p1=FAIL share_gain=0.000 share=0.308 base_share=0.308 armour=armoured p2=PASS hq_alive=true      [before share_gain=-0.036 share=0.272]
RESPONSE response_mass_windrunner_skai.json trade PASS p1=PASS trade_ratio=1.407 killed=38 lost=27 hq_alive=true
RESPONSE response_mass_windrunner_water.json trade PASS p1=PASS trade_ratio=1.065 killed=49 lost=46 hq_alive=true      [before trade_ratio=1.058 killed=55 lost=52]
RESPONSE response_worker_raid_skai.json raid FAIL p1=FAIL static_defence_near_raided_base=false p2=FAIL max_workers_lost_in_wave=7
RESPONSE response_worker_raid_water.json raid FAIL p1=PASS static_defence_near_raided_base=true p2=FAIL max_workers_lost_in_wave=7
RESPONSE response_turtle_skai.json turtle FAIL p1=FAIL supply_lost_before_first_kill=58 first_structure_kill_s=535.4 p2=PASS won_by_s=841.8      [before 38, 484.6, 589.6]
RESPONSE response_turtle_water.json turtle PASS p1=PASS supply_lost_before_first_kill=9 first_structure_kill_s=381.6 p2=PASS won_by_s=465.2      [before 9, 381.6, 464.4]
```

The roc rush, Skai Pummeler, Skai Windrunner and raid lines are equal to the before lines. The numbers in brackets are the ones that moved; the before values are from the L-R section for `aggressive_s4r`.

Lines for `aggressive_s3` as printed now (the earlier reports list only its summary and pass set, so there is no per-line before value):

```
RESPONSE response_roc_rush_skai.json air_answer PASS p1=PASS air_first_seen_s=165.2 model_air_known_s=85.0 p2=PASS aa_units=19 p3=PASS hq_alive=true
RESPONSE response_roc_rush_water.json air_answer PASS p1=PASS air_first_seen_s=167.9 model_air_known_s=89.0 p2=PASS aa_units=5 p3=PASS hq_alive=true
RESPONSE response_mass_pummeler_skai.json armoured_answer FAIL p1=FAIL share_gain=-0.040 share=0.421 base_share=0.462 armour=armoured p2=PASS hq_alive=true
RESPONSE response_mass_pummeler_water.json armoured_answer FAIL p1=FAIL share_gain=-0.091 share=0.217 base_share=0.308 armour=armoured p2=PASS hq_alive=true
RESPONSE response_mass_windrunner_skai.json trade PASS p1=PASS trade_ratio=1.609 killed=37 lost=23 hq_alive=true
RESPONSE response_mass_windrunner_water.json trade PASS p1=PASS trade_ratio=1.146 killed=55 lost=48 hq_alive=true
RESPONSE response_worker_raid_skai.json raid FAIL p1=FAIL static_defence_near_raided_base=false p2=FAIL max_workers_lost_in_wave=7
RESPONSE response_worker_raid_water.json raid FAIL p1=PASS static_defence_near_raided_base=true p2=FAIL max_workers_lost_in_wave=7
RESPONSE response_turtle_skai.json turtle FAIL p1=FAIL supply_lost_before_first_kill=19 first_structure_kill_s=443.2 p2=PASS won_by_s=849.9
RESPONSE response_turtle_water.json turtle PASS p1=PASS supply_lost_before_first_kill=3 first_structure_kill_s=343.6 p2=PASS won_by_s=430.1
```

## Run 5: SAT-15 income, 1 base against 2 (the test `GatherSlotTests`, `dotnet test ... --filter FullyQualifiedName~GatherSlotTests`, Release, `TestContext` output)

Common gathered per minute over 300 s, player 0's HQ Rooted at its spawn; "waiting" is worker-ticks spent Waiting over the run. Pre-saturation there was no slot limit, so there is no before value (the spec's section 3 table is the prediction).

| workers | 1 base per minute | waiting worker-ticks (1 base) | 2 bases per minute | waiting worker-ticks (2 bases) |
| ---: | ---: | ---: | ---: | ---: |
| 6 | 656 | 662 | 706 | 712 |
| 10 | 894 | 11758 | 1178 | 1187 |
| 14 | 894 | 35640 | 1638 | 3430 |
| 20 | 894 | 71640 | 1787 | 31517 |
| 24 | 894 | 95592 | 1788 | 55343 |

The 14-worker row is SAT-15's own case: 1638 / 894 = 1.83 times, against the pass bar 1.25.

## Run 6: how much the unchanged AI over-stacks fields (`waiting=` in traces)

Traces of the ladder games for `prototype_1v1`, base seed 20261117, games 0 to 3 of each matchup, `aggressive_s4r` in both seats, `--max-game-seconds 2400`, 12 traces (each trace's result line equals the ladder's; for example game 0 of skai_vs_skai has state hash `b89fea77924ef994`). Command, per matchup and game g: `--trace --map prototype_1v1 --matchup <m> --game g --seed 20261117 --max-game-seconds 2400 --profile-a aggressive_s4r --profile-b aggressive_s4r --out ...`. Cells are workers / waiting / completed depots (`skai.perch_depot` or the Water depot) at the 30 s sample; `-` means the game was already over.

| game | 180 s P0 | 180 s P1 | 300 s P0 | 300 s P1 | 480 s P0 | 480 s P1 | most waiting at one sample (P0, P1) | samples with waiting > 0 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| skai_vs_skai 0 | 11/0/1 | 14/0/1 | 14/0/1 | 15/0/2 | 17/2/2 | 17/1/2 | 4, 2 | 23 of 64 |
| skai_vs_skai 1 | 14/0/1 | 14/0/1 | 14/2/1 | 14/0/1 | 17/3/2 | 17/1/2 | 3, 3 | 18 of 50 |
| skai_vs_skai 2 | 14/0/1 | 14/0/1 | 15/0/1 | 15/0/1 | 13/0/2 | 17/1/2 | 2, 3 | 12 of 40 |
| skai_vs_skai 3 | 14/0/1 | 14/0/1 | 14/0/1 | 15/0/1 | 17/1/2 | 17/1/2 | 5, 4 | 20 of 62 |
| skai_vs_water 0 | 14/0/1 | 14/0/1 | 14/1/1 | 14/1/1 | - | - | 1, 1 | 6 of 26 |
| skai_vs_water 1 | 14/0/1 | 14/0/1 | 6/1/0 | 15/0/1 | - | - | 2, 1 | 8 of 26 |
| skai_vs_water 2 | 14/0/1 | 14/0/1 | 15/1/1 | 12/0/1 | - | - | 2, 1 | 7 of 30 |
| skai_vs_water 3 | 13/0/1 | 14/0/1 | 7/0/0 | 14/0/1 | - | - | 1, 3 | 9 of 32 |
| water_vs_water 0 | 14/0/1 | 14/0/1 | 12/1/1 | 14/0/1 | 17/0/2 | 19/1/2 | 4, 4 | 24 of 58 |
| water_vs_water 1 | 14/0/1 | 14/0/1 | 14/1/1 | 14/1/1 | 18/1/2 | 19/1/2 | 3, 3 | 15 of 42 |
| water_vs_water 2 | 14/0/1 | 14/0/1 | 14/1/1 | 15/0/1 | 18/0/2 | 19/0/2 | 3, 7 | 22 of 54 |
| water_vs_water 3 | 14/0/1 | 14/0/1 | 14/1/1 | 14/0/1 | 17/1/2 | 19/0/2 | 3, 1 | 15 of 42 |

Reading (a measurement, nothing tuned): at 180 s no player waits at all, and every player has 14 workers and one depot; at 300 s waiting is 0 to 2 per player; at 480 s (long games only) 17 to 19 workers on 2 depots wait 0 to 3. The largest queue in any sample is 7 workers (water_vs_water game 2, P1). The AI holds about 14 workers to 300 s and has 1 depot to 300 s, so the over-stacking the spec expected is modest in these 12 games: about a third of the player-samples show any waiting. The pre-saturation `waiting=` column did not exist, so there is no before value. Trace files are in `rts/build/sat_trace` (git-ignored).

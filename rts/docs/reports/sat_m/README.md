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

<!-- rts/docs/reports/ai_v4_rl/README.md: L-M measurement report of skirmish AI v4 packages R and L (specs/skirmish_ai_v4_rl.md section E): self-ladder, strength ladder against aggressive_s3, response scenarios, per-opening traces, and the gate verdict item by item. Measured on branch review/ai-v4-rl, 18 Nov 2026, prototype_1v1 only. A record of the AI, not a balance reading (P-005 decision 11). -->
# AI v4 R and L: measurement report

## After the weight scale (commit 2142640; current result)

Composition weights of `aggressive_s4r` multiplied by 10 (jitter band 0.25 now moves every weight both ways). Same commands, seeds and game counts as the sections below; `--jobs 4`; output under `rts/build/ladder_w10_*` (git-ignored). Step 1 checks: harness smoke `c423332711029434`, economy_smoke `ce62b175ea751ba6`, `dotnet test -c Release`: Replay 27, Net 33, Sim 873 passed, 0 failed. Neutrality evidence: test `R10_WeightScaleIsNeutralWithoutJitter` (jitter 0, x1 and x10 text give equal end tick, state hash and play signature); `R11_ScaledWeightsMoveBothWays`. No existing pin changed.

### Self-ladder (`aggressive_s4r` against itself), 216 of 216 decided, 2396 s

```
WARNING opening skai/skai_fast_expand: wins 15 of 20 mirror games against other openings (0.750, above 0.65).
WARNING opening skai/skai_windrunner_rush: wins 0 of 20 mirror games against other openings (0.000, below 0.35).
WARNING opening water/water_grasper_rush: wins 1 of 18 mirror games against other openings (0.056, below 0.35).
WARNING opening water/water_leviathan_tech: wins 22 of 31 mirror games against other openings (0.710, above 0.65).
LADDER candidate=aggressive_s4r baseline=aggressive_s4r decided=216 wins=102 fraction=0.472 distinct=216 mirror=64/144 cross=38/72 bar=0.650 FAIL
```

Distinct / decided per cell: skai_vs_skai 72/72 = 1.000, skai_vs_water 72/72 = 1.000, water_vs_water 72/72 = 1.000. Mirror 64/144 = 0.444, Wilson [36.6, 52.6]; cross 38/72 = 0.528 (as skai 36/36, as water 2/36).

| faction | opening | draws | use share | mirror games | wins | win fraction | Wilson interval | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 57 | 0.264 | 22 | 14 | 0.636 | [43.0, 80.3] | 19 | 19 |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 15 | 0.750 | [53.1, 88.8] | 16 | 15 |
| skai | skai_standard | 76 | 0.352 | 28 | 16 | 0.571 | [39.1, 73.5] | 24 | 24 |
| skai | skai_windrunner_rush | 35 | 0.162 | 20 | 0 | 0.000 | [0.0, 16.1] | 13 | 12 |
| water | water_fast_expand | 52 | 0.241 | 25 | 12 | 0.480 | [30.0, 66.5] | 15 | 0 |
| water | water_grasper_rush | 42 | 0.194 | 18 | 1 | 0.056 | [1.0, 25.8] | 20 | 0 |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 22 | 0.710 | [53.4, 83.9] | 17 | 2 |
| water | water_standard | 60 | 0.278 | 32 | 18 | 0.563 | [39.3, 71.8] | 20 | 0 |

(Strength ladder, response scenarios and gate verdict follow in later commits.)

---

## After the reservation fix, before the weight scale (commit 7f37605; superseded by the section above)

**Verdict in one line: gate item 1 (variety) PASSES; item 2 (not weaker) PASSES (0.463 against 0.45, mirror upper bound 0.513 against 0.50); item 3 (no dominant, no dud opening) still FAILS on the win-fraction halves (use-share half passes).** Nothing was tuned: no opening weight, build order, jitter, threshold, bar, scenario or game count was changed; the only change is the fix below. Same commands, seeds and game counts as section 1 (the tail of the old report, kept below).

**The fix.** In `rts/src/Ai/AiBrain.cs` rule R, a reservation in one resource no longer blocks a purchase that costs none of that resource: `CanPay`, `TryBuild` and the army-production candidate filter skip the rare check when the rare cost is 0 (and the common check when the common cost is 0). It is unconditional (no profile key): every old pin stayed green without re-pinning (harness smoke `c423332711029434`, economy_smoke `ce62b175ea751ba6`, `V2ReferenceHash`, `BlindTraceHash`, `GoldenFinalHash`, R-0 pin, whole AI project). Test L-10 was renamed `L10_RareReservationDoesNotBlockRareFreePurchases` and now asserts the Perch Depot is bought while the Storm Spire waits for rare (it failed before the fix). Nothing under `rts/src/Sim` changed.

### Forced traces now (`rts/build/force/<opening>`, seed 20261117, game 0, slot 0, 30 s samples)

| opening | Tech building (before) | Tech building (now) | First army (before) | First army (now) |
| --- | --- | --- | --- | --- |
| water_leviathan_tech | Reef Workshop 630 s | Reef Workshop 240 s (Pearl Depot 90 s) | none until 540 s, first attack 660 s | Reef Runner 90 s, Grasper/Shellback 150 s, first Leviathan 360 s, supply 15 / 24 / 50 / 61 at 120 / 150 / 240 / 300 s |
| skai_air | Storm Spire 210 s | Storm Spire 210 s (Perch Depot 90 s, was 300 s) | Windrunners from 60 s, paused 60 to 180 s | Windrunner 60 s, Talon Archer 150 s, first Roc Rider 210 s, first Thunderhead 300 s, supply 16 / 32 / 57 at 120 / 180 / 300 s |

### Self-ladder (`aggressive_s4r` against itself), 215 of 216 decided, 2339 s

```
WARNING opening skai/skai_standard: wins 19 of 28 mirror games against other openings (0.679, above 0.65).
WARNING opening skai/skai_windrunner_rush: wins 0 of 20 mirror games against other openings (0.000, below 0.35).
WARNING opening water/water_grasper_rush: wins 0 of 18 mirror games against other openings (0.000, below 0.35).
WARNING opening water/water_leviathan_tech: wins 26 of 31 mirror games against other openings (0.839, above 0.65).
LADDER candidate=aggressive_s4r baseline=aggressive_s4r decided=215 wins=99 fraction=0.460 distinct=212 mirror=63/143 cross=36/72 bar=0.650 FAIL
```

Distinct / decided per cell (from `ladder.jsonl`; no distinct-share warning): skai_vs_skai 71/71 = 1.000, skai_vs_water 70/72 = 0.972, water_vs_water 71/72 = 0.986.

| faction | opening | draws | use share | mirror games | wins | win fraction | Wilson interval | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 57 | 0.264 | 22 | 13 | 0.591 | [38.7, 76.7] | 19 | 19 |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 13 | 0.650 | [43.3, 81.9] | 16 | 16 |
| skai | skai_standard | 76 | 0.352 | 28 | 19 | 0.679 | [49.3, 82.1] | 24 | 22 |
| skai | skai_windrunner_rush | 35 | 0.162 | 20 | 0 | 0.000 | [0.0, 16.1] | 13 | 13 |
| water | water_fast_expand | 52 | 0.241 | 25 | 14 | 0.560 | [37.1, 73.3] | 15 | 0 |
| water | water_grasper_rush | 42 | 0.194 | 18 | 0 | 0.000 | [0.0, 17.6] | 20 | 0 |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 26 | 0.839 | [66.4, 92.7] | 17 | 2 |
| water | water_standard | 60 | 0.278 | 32 | 13 | 0.406 | [26.4, 59.2] | 20 | 0 |

### Strength ladder (`aggressive_s3` baseline, `--non-regression`), 216 of 216 decided, 2363 s

```
WARNING opening skai/skai_windrunner_rush: wins 0 of 12 mirror games against other openings (0.000, below 0.35).
WARNING opening water/water_fast_expand: wins 15 of 22 mirror games against other openings (0.682, above 0.65).
WARNING opening water/water_grasper_rush: wins 0 of 14 mirror games against other openings (0.000, below 0.35).
WARNING opening water/water_standard: wins 5 of 17 mirror games against other openings (0.294, below 0.35).
LADDER candidate=aggressive_s4r baseline=aggressive_s3 decided=216 wins=100 fraction=0.463 distinct=194 mirror=63/144 cross=37/72 bar=0.450 PASS
```

No mirror-interval warning and no distinct-share warning. Distinct / decided: skai_vs_skai 67/72 = 0.931, skai_vs_water 63/72 = 0.875, water_vs_water 64/72 = 0.889.

| row | games | decided | distinct | candidate wins | baseline wins | candidate fraction | Wilson (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 67 | 34 | 38 | 0.472 | [36.3, 59.5] |
| skai_vs_water | 72 | 72 | 63 | 37 | 35 | 0.514 | [44.9, 68.6] |
| water_vs_water | 72 | 72 | 64 | 29 | 43 | 0.403 | [26.7, 49.7] |
| **mirror** | 144 | 144 | 131 | 63 | 81 | **0.438** | **[34.6, 51.3]** |
| cross | 72 | 72 | 63 | 37 | 35 | 0.514 | [44.9, 68.6] |
| cross, candidate as skai | 36 | 36 | 35 | 36 | 0 | 1.000 | [90.1, 100.0] |
| cross, candidate as water | 36 | 36 | 28 | 1 | 35 | 0.028 | [0.6, 17.7] |
| all | 216 | 216 | 194 | 100 | 116 | 0.463 | [40.5, 54.4] |

| faction | opening | draws | use share | games | wins | win fraction | Wilson interval | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 23 | 0.213 | 17 | 8 | 0.471 | [23.1, 66.8] | 6 | 6 |
| skai | skai_fast_expand | 28 | 0.259 | 19 | 11 | 0.579 | [33.7, 75.4] | 9 | 9 |
| skai | skai_standard | 35 | 0.324 | 24 | 15 | 0.625 | [47.3, 83.6] | 11 | 11 |
| skai | skai_windrunner_rush | 22 | 0.204 | 12 | 0 | 0.000 | [0.0, 25.9] | 10 | 10 |
| water | water_fast_expand | 30 | 0.278 | 22 | 15 | 0.682 | [45.4, 82.8] | 8 | 0 |
| water | water_grasper_rush | 22 | 0.204 | 14 | 0 | 0.000 | [0.0, 21.5] | 8 | 0 |
| water | water_leviathan_tech | 28 | 0.259 | 19 | 9 | 0.474 | [16.3, 61.2] | 9 | 1 |
| water | water_standard | 28 | 0.259 | 17 | 5 | 0.294 | [15.2, 58.3] | 11 | 0 |

### Response scenarios

`aggressive_s4r`: `RESPONSE_SUMMARY passed=5 of=10`, the same five pass and five fail as before. Only `response_mass_windrunner_water` and `response_worker_raid_water` numbers moved (trade_ratio 1.233, killed 53, lost 43; max_workers_lost_in_wave 7); every verdict is unchanged. (The `aggressive_s3` response run was not repeated: the fix does not change that profile.)

### Gate verdict (current)

| item | result | verdict |
| --- | --- | --- |
| 1 Variety | 1.000, 0.972, 0.986 distinct per decided game, no warning | **PASS** |
| 2 Not weaker | fraction 0.463 >= 0.45; mirror 63/144 decided, Wilson upper bound 0.513 >= 0.50 | **PASS** (both halves) |
| 3 Openings, self-ladder | use shares 0.162 to 0.352, none above 0.40: pass. Win fractions outside 0.35 to 0.65: skai_standard 0.679, skai_windrunner_rush 0.000, water_grasper_rush 0.000, water_leviathan_tech 0.839 | use share PASS; win fractions **FAIL (4 of 8)** |
| 3 Openings, strength ladder "below" half | skai_windrunner_rush 0.000, water_grasper_rush 0.000, water_standard 0.294 below 0.35 | **FAIL** |

Reading: the fix lifted skai_air (0.000 to 0.591) and water_leviathan_tech (0.065 to 0.839, now over the top bar), and moved the ladder past the 0.45 bar. The two rush openings still lose every mirror game on merit (no rare step, so the fix does not touch them); `water_leviathan_tech` is now dominant. Per spec the weights may be set to 0 once for failing openings; that was not done.

---

# Before the reservation fix (measured on 7ca4f20; kept unchanged for the record)


**Verdict in one line: gate item 1 (variety) PASSES; item 2 (not weaker) FAILS (0.384 against the 0.45 bar, mirror upper bound 0.464 against 0.50); item 3 (no dominant, no dud opening) FAILS on the "below" and "above" halves (the use-share half passes).** Nothing was tuned after seeing results: no threshold, scenario, bar, jitter or opening weight was changed. The most important finding is not a tuning matter: two of the eight openings (`skai_air`, `water_leviathan_tech`) are crippled by a reservation rule in the existing brain (section 5).

## 1. Commands (from the repository root, `dotnet build rts/RtsGame.sln -c Release` first; 4 cores)

```
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s4r --profile-b aggressive_s4r --jobs 4 --out rts/build/ladder_rl_self
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s3 --profile-b aggressive_s4r --non-regression --jobs 4 --out rts/build/ladder_rl_vs_s3
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --response --profile aggressive_s4r
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --response --profile aggressive_s3
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data            (smoke)
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --script rts/data/scripts/economy_smoke.json
dotnet test rts/RtsGame.sln -c Release --no-build
```

Wall time 1706 s and 2029 s for the two ladders (216 games each, both with the in-process determinism spot check: 2 games, 0 mismatches). `rts/build` is not committed.

## 2. Self-ladder (`aggressive_s4r` against itself): gate items 1 and 3

stdout summary and warnings, as printed (the LADDER verdict and exit code 2 are meaningless in a self-ladder, spec section E):

```
WARNING opening skai/skai_air: wins 0 of 22 mirror games against other openings (0.000, below 0.35).
WARNING opening skai/skai_fast_expand: wins 15 of 20 mirror games against other openings (0.750, above 0.65).
WARNING opening skai/skai_standard: wins 28 of 28 mirror games against other openings (1.000, above 0.65).
WARNING opening skai/skai_windrunner_rush: wins 2 of 20 mirror games against other openings (0.100, below 0.35).
WARNING opening water/water_fast_expand: wins 21 of 25 mirror games against other openings (0.840, above 0.65).
WARNING opening water/water_grasper_rush: wins 6 of 18 mirror games against other openings (0.333, below 0.35).
WARNING opening water/water_leviathan_tech: wins 2 of 31 mirror games against other openings (0.065, below 0.35).
WARNING opening water/water_standard: wins 24 of 32 mirror games against other openings (0.750, above 0.65).
LADDER candidate=aggressive_s4r baseline=aggressive_s4r decided=215 wins=109 fraction=0.507 distinct=207 mirror=73/143 cross=36/72 bar=0.650 FAIL
```

**Item 1, distinct games per cell** (play signatures among decided games; no distinct-share `WARNING` line was printed). Note that the column headed "distinct fraction" in the existing `ladder.md` tables is the candidate's win share over distinct games, not distinct over decided; the numbers below are computed from `ladder.jsonl`:

| cell | decided | distinct | distinct / decided | gate 0.8 |
| --- | ---: | ---: | ---: | --- |
| skai_vs_skai | 71 | 69 | 0.972 | pass |
| skai_vs_water | 72 | 70 | 0.972 | pass |
| water_vs_water | 72 | 68 | 0.944 | pass |

(For comparison `aggressive_s3` against itself gave 20 distinct of 216; spec section 0.) One game of skai_vs_skai was undecided at the 2400 s cap.

**Opening usage and mirror results** (`## Openings` of `ladder.md`; draws are slots, mirror games are against a different opening; the Wilson interval is over distinct games):

| faction | opening | draws | use share | mirror games | wins | win fraction | Wilson interval | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 57 | 0.264 | 22 | 0 | 0.000 | [0.0, 16.1] | 19 | 19 |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 15 | 0.750 | [51.2, 88.2] | 16 | 16 |
| skai | skai_standard | 76 | 0.352 | 28 | 28 | 1.000 | [87.5, 100.0] | 24 | 24 |
| skai | skai_windrunner_rush | 35 | 0.162 | 20 | 2 | 0.100 | [2.8, 30.1] | 13 | 13 |
| water | water_fast_expand | 52 | 0.241 | 25 | 21 | 0.840 | [65.3, 93.6] | 15 | 0 |
| water | water_grasper_rush | 42 | 0.194 | 18 | 6 | 0.333 | [16.3, 56.3] | 20 | 0 |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 2 | 0.065 | [2.0, 22.6] | 17 | 0 |
| water | water_standard | 60 | 0.278 | 32 | 24 | 0.750 | [54.3, 85.3] | 20 | 0 |

Cross-faction cells follow the faction (Skai won all 36 as candidate-as-skai games and Water none), as the spec expected.

## 3. Strength ladder (`aggressive_s3` baseline, `aggressive_s4r` candidate, `--non-regression`): gate items 2 and 3

```
WARNING prototype_1v1/water_vs_water: 56 distinct of 72 decided games (0.778); seeds barely vary games, do not read these win rates as balance (P-005 decision 11).
WARNING opening skai/skai_air: wins 0 of 17 mirror games against other openings (0.000, below 0.35).
WARNING opening skai/skai_windrunner_rush: wins 0 of 12 mirror games against other openings (0.000, below 0.35).
WARNING opening water/water_fast_expand: wins 15 of 22 mirror games against other openings (0.682, above 0.65).
WARNING opening water/water_grasper_rush: wins 0 of 14 mirror games against other openings (0.000, below 0.35).
WARNING opening water/water_leviathan_tech: wins 1 of 19 mirror games against other openings (0.053, below 0.35).
WARNING opening water/water_standard: wins 5 of 17 mirror games against other openings (0.294, below 0.35).
WARNING mirror: candidate wins 45 of 120 distinct mirror games, Wilson upper bound 0.464 below 0.50; the candidate may be weaker than the baseline.
LADDER candidate=aggressive_s4r baseline=aggressive_s3 decided=216 wins=83 fraction=0.384 distinct=181 mirror=47/144 cross=36/72 bar=0.450 FAIL
```

| row | games | decided | distinct | candidate wins | baseline wins | candidate fraction | Wilson (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 64 | 26 | 46 | 0.361 | [28.1, 51.3] |
| skai_vs_water | 72 | 72 | 61 | 36 | 36 | 0.500 | [46.5, 70.5] |
| water_vs_water | 72 | 72 | 56 | 21 | 51 | 0.292 | [24.5, 48.8] |
| **mirror** | 144 | 144 | 120 | 47 | 97 | **0.326** | **[29.4, 46.4]** |
| cross | 72 | 72 | 61 | 36 | 36 | 0.500 | [46.5, 70.5] |
| cross, candidate as skai | 36 | 36 | 36 | 36 | 0 | 1.000 | [90.4, 100.0] |
| cross, candidate as water | 36 | 36 | 25 | 0 | 36 | 0.000 | [0.0, 13.3] |
| all | 216 | 216 | 181 | 83 | 133 | 0.384 | [37.7, 52.0] |

Per opening against `aggressive_s3` (`## Openings`; "mirror games" are games against the baseline here, which has no opening and counts as different):

| faction | opening | draws | use share | games | wins | win fraction | Wilson interval | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 23 | 0.213 | 17 | 0 | 0.000 | [0.0, 22.8] | 6 | 6 |
| skai | skai_fast_expand | 28 | 0.259 | 19 | 11 | 0.579 | [33.7, 75.4] | 9 | 9 |
| skai | skai_standard | 35 | 0.324 | 24 | 15 | 0.625 | [47.3, 83.6] | 11 | 11 |
| skai | skai_windrunner_rush | 22 | 0.204 | 12 | 0 | 0.000 | [0.0, 25.9] | 10 | 10 |
| water | water_fast_expand | 30 | 0.278 | 22 | 15 | 0.682 | [45.4, 82.8] | 8 | 0 |
| water | water_grasper_rush | 22 | 0.204 | 14 | 0 | 0.000 | [0.0, 21.5] | 8 | 0 |
| water | water_leviathan_tech | 28 | 0.259 | 19 | 1 | 0.053 | [3.0, 56.4] | 9 | 0 |
| water | water_standard | 28 | 0.259 | 17 | 5 | 0.294 | [15.2, 58.3] | 11 | 0 |

Distinct games against `aggressive_s3` (distinct / decided): skai_vs_skai 64/72 = 0.889, skai_vs_water 61/72 = 0.847, water_vs_water 56/72 = 0.778 (the one distinct-share warning; the baseline is deterministic, so only the candidate's variety counts).

## 4. Response scenarios (`--response`)

`aggressive_s4r`: `RESPONSE_SUMMARY passed=5 of=10`; `aggressive_s3`: `passed=5 of=10`. The same five pass for both (roc rush x2, mass Windrunner x2, turtle Water); the same five fail (mass Pummeler x2, worker raid x2, turtle Skai). Lines for `aggressive_s4r`, as printed:

```
RESPONSE response_roc_rush_skai.json air_answer PASS p1=PASS air_first_seen_s=165.2 model_air_known_s=85.0 p2=PASS aa_units=19 p3=PASS hq_alive=true
RESPONSE response_roc_rush_water.json air_answer PASS p1=PASS air_first_seen_s=167.9 model_air_known_s=89.0 p2=PASS aa_units=6 p3=PASS hq_alive=true
RESPONSE response_mass_pummeler_skai.json armoured_answer FAIL p1=FAIL share_gain=-0.004 share=0.458 base_share=0.462 armour=armoured p2=PASS hq_alive=true
RESPONSE response_mass_pummeler_water.json armoured_answer FAIL p1=FAIL share_gain=-0.015 share=0.293 base_share=0.308 armour=armoured p2=PASS hq_alive=true
RESPONSE response_mass_windrunner_skai.json trade PASS p1=PASS trade_ratio=1.542 killed=37 lost=24 hq_alive=true
RESPONSE response_mass_windrunner_water.json trade PASS p1=PASS trade_ratio=1.688 killed=81 lost=48 hq_alive=true
RESPONSE response_worker_raid_skai.json raid FAIL p1=FAIL static_defence_near_raided_base=false p2=FAIL max_workers_lost_in_wave=7
RESPONSE response_worker_raid_water.json raid FAIL p1=PASS static_defence_near_raided_base=true p2=FAIL max_workers_lost_in_wave=5
RESPONSE response_turtle_skai.json turtle FAIL p1=FAIL supply_lost_before_first_kill=18 first_structure_kill_s=464.2 p2=PASS won_by_s=537.7
RESPONSE response_turtle_water.json turtle PASS p1=PASS supply_lost_before_first_kill=9 first_structure_kill_s=381.6 p2=PASS won_by_s=470.3
```

The per-opening response runs of spec section E run (4) were not made (the task asked for the one run; Q-9 says report only).

## 5. Per-opening traces and the rare-reservation finding (spec section E run 5, partly)

One `--trace` per opening on forced profile copies (`rts/build/force/<opening>`: that opening weight 100, the same faction's others 0; `prototype_1v1`, `skai_vs_skai` or `water_vs_water`, seed 20261117, game 0; the trace samples every 30 s, so times are good to 30 s; both seats draw the forced opening). First depot, first attack and supply used at 120/180/240/300 s of slot 0:

| opening | first depot (s) | first attack (s) | supply at 120/180/240/300 s | intended (L.5) |
| --- | ---: | ---: | --- | --- |
| skai_standard | 90 | 210 | 17 / 35 / 40 / 49 | depot at once, attack 8 to 14 supply |
| skai_windrunner_rush | 270 | 150 | 13 / 17 / 21 / 27 | depot not before 240, attack 150 to 200: as intended |
| skai_fast_expand | 90 | 210 | 20 / 33 / 46 / 50 | attack at about 360: 150 s early (more than 60 s off) |
| skai_air | 90 | 270 | 13 / 19 / 42 / 65 | Storm Spire about 240 (measured 210), first Thunderhead about 300 (seen at 300) |
| water_standard | 90 | 180 | 16 / 39 / 47 / 57 | as skai_standard |
| water_grasper_rush | 300 | 120 | 16 / 18 / 26 / 32 | depot not before 240 as intended, attack 150 to 200 (30 s early) |
| water_fast_expand | 90 | 210 | 20 / 34 / 50 / 63 | attack at about 360: 150 s early (more than 60 s off) |
| **water_leviathan_tech** | **540** | **660** | **6 / 6 / 6 / 6** | depot at once, Reef Workshop about 240 (measured 630), first Leviathan about 330: **more than 300 s off** |

**Finding (cause of the "dud" rows).** `water_leviathan_tech` sits at 6 supply with 2850 common in the bank and no army until 540 s. A due structure the stock cannot pay reserves its cost while the AI has income (rule R), and every later purchase checks `rare - reserved rare >= its rare cost` (`AiBrain.CanPay`, `TryBuild`; `Produce` filters candidates with `CostRare <= _rare - _reserveRare`). With 0 rare and the Reef Workshop or Storm Spire (50 rare each) reserved, that is -50, so even purchases with a rare cost of 0 (army units, the Pearl Depot, the only source of rare) are refused. `skai_air` escapes by luck of order (its depot was placed before the Spire came due, but army training paused from about 60 s to 180 s with 1035 common banked at 150 s). The new test `L10_RareBuildStepBlocksTrainingUntilRareIsBanked` pins the behaviour. A throw-away experiment (three comparisons made `CostRare == 0 ||`, traces re-run, then reverted; not committed) made both traces flow: depot 90 s, Reef Workshop 240 s, supply 15 / 32 / 50 / 61 and first attack 240 s for the Leviathan opening. The builder did not change the brain: the fix touches rule R for every profile, and old profiles must stay bit-identical (it would need a re-check that none of the pins move). This is the coordinator's decision; **the item 3 and item 2 numbers above are therefore a measurement of openings with this bug in, not of the openings as designed.**

## 6. Gate verdict

| item | requirement | result | verdict |
| --- | --- | --- | --- |
| 1 Variety | every self-ladder cell distinct / decided >= 0.8, no S.2 `WARNING` | 0.972, 0.972, 0.944; no warning | **PASS** |
| 2 Not weaker | LADDER >= 0.45 over decided games, and mirror Wilson upper bound over distinct games >= 0.50 | fraction 0.384 (83 of 216); mirror 45 of 120 distinct, upper bound 0.464 (warning printed) | **FAIL, both halves** |
| 3 Openings (self-ladder) | no use share above 0.40; no mirror win fraction above 0.65; none below 0.35 with at least 10 games | use shares 0.162 to 0.352: pass. Above 0.65: skai_standard 1.000, skai_fast_expand 0.750, water_fast_expand 0.840, water_standard 0.750. Below 0.35: skai_air 0.000, skai_windrunner_rush 0.100, water_grasper_rush 0.333, water_leviathan_tech 0.065 | use share PASS, win fractions **FAIL (8 of 8 openings outside 0.35 to 0.65)** |
| 3 Openings (strength ladder, "below" half) | none below 0.35 against `aggressive_s3` with at least 10 games | skai_air 0.000, skai_windrunner_rush 0.000, water_grasper_rush 0.000, water_leviathan_tech 0.053, water_standard 0.294 (the use-share and "above" halves are only gated on the self-ladder) | **FAIL** |

Reading, not tuning. (a) Variety is real and almost free: the AI's games now differ (207 of 215 distinct). (b) The randomisation made the AI weaker than `aggressive_s3` in mirrors (0.326), and every opening except `*_standard` and `*_fast_expand` loses to it outright; the winners are the two that keep the proven build order or the economy-first line. (c) Part of the weakness is the rare-reservation bug of section 5; the rush openings (`skai_windrunner_rush` 2 of 20, `water_grasper_rush` 6 of 18) have no rare step and lose on merit against the slower, bigger openings. Whether openings that are weaker by design belong in the pool (they are the point of variety) or the gate item 3 limits should apply to them is a decision for the coordinator; the spec allows setting a failing opening's weight to 0 once, and this report did not do it. (d) The jitter keys were not changed; the "holds at 0.5 and 1.5 times the value" rule was not exercised. (e) Spec acceptance run (1) (`aggressive_s3` self-ladder equal to a run on `c6515ca`) was not made; the pins in section 7 and the R-0 test (`S3SelfGame_IsUnchangedByPackagesRAndL`) are the evidence that old profiles are bit-identical.

## 7. Pins and tests

- `dotnet test rts/RtsGame.sln -c Release --no-build` on the final tree: Rts.Replay.Tests 27 passed; Rts.Net.Tests 33 passed; Rts.Sim.Tests **871 passed, 0 failed** (931 in all). Warnings are errors in the build; the build prints 0 warnings.
- Harness smoke `STATE_HASH=c423332711029434` and economy_smoke `STATE_HASH=ce62b175ea751ba6`: unchanged. `V2ReferenceHash`, `BlindTraceHash`, `GoldenFinalHash`, `WorkerConstructionTests` and the R-0 pin (`S3SelfTraceHash e5c323f688131dd0`, signature `fde24604c8dc84e6`, end tick 15462) are in the suite and green without re-pinning.

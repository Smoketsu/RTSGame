<!-- rts/docs/reports/ai_p/README.md -->
**Outcome (19 Nov 2026): the AI-P gate failed, so every AI-P switch is off on `aggressive_s5` (`use_forward_producers`, `use_threat_defence`, the four `threat_defence_*` keys and `static_defence_max_per_base` at the `abl_s5_no_p` values); the code merges inert and `aggressive_s5` plays bit for bit as before AI-P. The developer decides what happens next.**

# AI-P: producers at expansions, static defence by threat

Package AI-P of `rts/docs/specs/field_saturation.md`: rule P1 (`use_forward_producers`), rule P2 (`use_threat_defence`, step SD-4 with hysteresis) and the ablation `abl_s5_no_p`. This report was written by a second builder that resumed after a container restart killed the first one, which left no report; every number below is read from the surviving output files (kept in `rts/build/`, git-ignored) or measured again here. Nothing was tuned and no code was changed to make an item pass.

**Result in one line: the AI-P gate FAILS.** `aggressive_s5` (AI-P on) beats its own ablation `abl_s5_no_p` in 80 of 216 games (0.370, bar 0.450), the mirror Wilson upper bound is 0.385 (bar 0.50), and the self-ladder and the traces are in the sections that follow.

## Commits

| Commit | What it changed |
| --- | --- |
| `81a7ea7` | The build: `AiBrainPlacement.cs` (site search around a chosen base, base with fewest producers, exposed base, SD-4), `AiBrain.cs`, `AiMemory.cs` (hysteresis memory), `AiProfile.cs` (6 keys, parser ranges), `AiWorldView.cs` (`StaticDefencesPerBaseMax`), the 6 keys with old values in every old profile, `aggressive_s5` switched on, new `abl_s5_no_p.json`, `AiPlacementTests.cs` (P-0 to P-6), `rts/src/Ai/README.md`. |
| `a213506` | Profile-list tests list `abl_s5_no_p`; AI-E test E-6 now plays `abl_s5_no_p`; spec test rows P-5, P-6 and the build notes. |
| `d96d3d9` | `AiPlacementMeasure.cs`: an explicit (never in the suite) measurement test that replays harness games and prints each producer and static defence order with tick, player, tile and base. Tests only. |

## New profile keys

| Key | Old profiles | `aggressive_s5` |
| --- | --- | --- |
| `use_forward_producers` | false | true |
| `use_threat_defence` | false | true |
| `threat_defence_min_enemy_supply` | 0 | 8 |
| `threat_defence_min_enemy_advantage` | 0 | 1 |
| `threat_defence_max_value_share` | 0 | 0.4 |
| `threat_defence_release_fraction` (added in the build, P-5) | 1 (no band) | 0.75 |
| `static_defence_max_per_base` (existing) | 2 (`aggressive_s4r` and the other old profiles; unchanged) | 6 |

`abl_s5_no_p.json` is `aggressive_s5` with the six new keys at the old values and `static_defence_max_per_base` 2.

## Proof that old profiles and the ablation are unchanged

- Diff `97f4d97..HEAD` of `aggressive_s4r.json`: only the six new keys at their old values (`false`, `false`, 0, 0, 0, 1); no existing value changes. The other 14 old profiles got the same 12-line addition (see `git show 81a7ea7 --stat`).
- Test `P6_AblationPlaysThePreAiPGame` exists and passes (full suite below): `abl_s5_no_p` plays three full prototype_1v1 games (seed 20261117; skai_vs_water 2, skai_vs_skai 3, water_vs_water 2) to the final state hashes `ae4f362f8bdc9885`, `235626745316a81d` and `b171e5cd2bf0bfeb`. The test comment and spec row P-6 say these were recorded on `aggressive_s5` at `97f4d97`; I did **not** re-derive them on `97f4d97` here, so that provenance is the first builder's statement.
- Test `P2_SwitchOffKeepsTheHqRing` (every site equals today's with the switch off) passes.
- Not rerun here: the `abl_s5_no_e` against `aggressive_s4r` hash check of AI-E (its test E-7 is in the suite and passes).

## Strength ladder: `aggressive_s5` against `abl_s5_no_p` (`--non-regression`), 216 of 216 decided

Output in `rts/build/ladder_p_vs_nop/`; `ladder.jsonl` holds 216 records (72 per matchup, 108 with the candidate in each slot, profiles checked from the records), elapsed 3593.7 s. Determinism spot check 2 games, 0 mismatches.

```
WARNING opening water/water_fast_expand: wins 4 of 25 mirror games against other openings (0.160, below 0.35).
WARNING opening water/water_leviathan_tech: wins 22 of 31 mirror games against other openings (0.710, above 0.65).
WARNING mirror: candidate wins 44 of 144 distinct mirror games, Wilson upper bound 0.385 below 0.50; the candidate may be weaker than the baseline.
LADDER candidate=aggressive_s5 baseline=abl_s5_no_p decided=216 wins=80 fraction=0.370 distinct=216 mirror=44/144 cross=36/72 bar=0.450 FAIL
```

**Distinct games per cell** (distinct play signatures counted from the records; the `distinct fraction` column of `ladder.md` is a win share, not this): 

| cell | decided | distinct games | distinct / decided | candidate wins | baseline wins | fraction | Wilson |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 13 | 59 | 0.181 | [10.9, 28.5] |
| skai_vs_water | 72 | 72 | 1.000 | 36 | 36 | 0.500 | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 31 | 41 | 0.431 | [32.3, 54.6] |

| row | decided | candidate wins | fraction | Wilson |
| --- | ---: | ---: | ---: | --- |
| mirror | 144 | 44 | 0.306 | [23.6, 38.5] |
| cross | 72 | 36 | 0.500 | [38.7, 61.3] |
| cross, candidate as skai / water | 36 / 36 | 36 / 0 | 1.000 / 0.000 | [90.4, 100.0] / [0.0, 9.6] |
| all | 216 | 80 | 0.370 | [30.9, 43.7] |
| slot 0 / slot 1 | 108 / 108 | 40 / 40 | 0.370 / 0.370 | [28.5, 46.4] each |

Undecided 0, draws 0. Median game length (ticks / 20, from the records): all 580.0 s (min 426.0, max 1180.0); skai_vs_skai 560.3 s; skai_vs_water 542.5 s; water_vs_water 653.0 s.

**Openings of the candidate** (mirror games against other openings; win fraction = candidate wins / those games; draw count = how often the opening was drawn, use share its share of draws):

| faction | opening | draws | use share | mirror games | wins | win fraction | in 0.35 to 0.65 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| skai | skai_air | 57 | 0.264 | 22 | 13 | 0.591 | yes |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 8 | 0.400 | yes |
| skai | skai_standard | 76 | 0.352 | 28 | 14 | 0.500 | yes |
| skai | skai_windrunner_pressure | 35 | 0.162 | 20 | 10 | 0.500 | yes |
| water | water_fast_expand | 52 | 0.241 | 25 | 4 | **0.160** | no |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 11 | 0.611 | yes |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 22 | **0.710** | no |
| water | water_standard | 60 | 0.278 | 32 | 16 | 0.500 | yes |

Reading: the cross cell is split by faction as in every earlier ladder (the Skai side wins all 36, the Water side none). In the Skai mirror the AI-P profile loses 59 of 72 games; in the Water mirror 41 of 72. AI-P is worse than not having it on the strength measure.

## Response scenarios (`--response --profile <id>`)

Both complete (`response_s5.log`, `response_nop.log`; the run ended with `done`).

`aggressive_s5`: `RESPONSE_SUMMARY passed=6 of=10`.

| scenario | `aggressive_s5` | `abl_s5_no_p` |
| --- | --- | --- |
| roc_rush skai | PASS (aa_units 17) | PASS (16) |
| roc_rush water | PASS (aa_units 4) | PASS (4) |
| mass_pummeler skai | FAIL (share_gain -0.024) | FAIL (-0.050) |
| mass_pummeler water | FAIL (share_gain -0.054) | FAIL (-0.062) |
| mass_windrunner skai | PASS (trade_ratio 1.125, killed 36, lost 32) | PASS (1.560, 39, 25) |
| mass_windrunner water | PASS (1.053, 79, 75) | PASS (1.529, 78, 51) |
| worker_raid skai | FAIL (p1 pass: defence near raided base; p2 fail: max_workers_lost_in_wave 4) | FAIL (p1 fail, p2 fail: 6) |
| worker_raid water | FAIL (p1 fail: no defence near raided base; p2 pass: 2) | FAIL (p1 fail; p2 pass: 2) |
| turtle skai | PASS (supply lost before first kill 8, won by 586.9 s) | FAIL (26 lost; won by 538.8 s) |
| turtle water | PASS (8, 445.9 s) | PASS (8, 444.3 s) |
| **passed** | **6 of 10** | **5 of 10** |

`abl_s5_no_p` run: `RESPONSE_SUMMARY passed=5 of=10`. The response gate "no fewer passes than the baseline" holds (6 against 5); the spec's gate text says 6 of 9 counted scenarios and the harness prints 10, which I did not reconcile. The mass_windrunner trade ratios fall from about 1.55 to 1.1 with AI-P on.

## Tests

Full `dotnet test rts/RtsGame.sln -c Release` of the head tree (`rts/build/fulltest2.log`, 07:29, after the last commit `d96d3d9`): Sim **919 of 919 passed**, Net 33 of 33, Replay 27 of 27 (919 + 33 + 27 = 979). The earlier run `fulltest.log` (06:09, before `a213506`) had 4 failures (`DProfiles_SwitchesAndValidation`, `L1_ShippedProfilesParse`, `E6_MirrorRun_CapsHold_AndFirstDepotIsOnTime`, `R6_NoVarietyForOldProfiles`); `a213506` addressed them (profile lists, E-6 on the ablation) and the rerun passed. I did not rerun the full suite again after the resume: the tree is unchanged since `d96d3d9` apart from this report.

## Spec deviations found in the tests

- P-3: the advantage 1.5 and 0.9 are tested on `AiBrain.ThreatDefenceHolds` directly, not in a real view (the spec row asked for a real view; the build note in the spec says why).
- P-1: the depot sits at tile (31, 10) so no producer is within `base_radius_tiles`.
- P-5 and key `threat_defence_release_fraction` and test P-6 were added beyond the spec's original rows (the spec now lists them).
- AI-E test E-6 now plays `abl_s5_no_p`: with AI-P on, the same seed-1234 game trips its cap check for two thinks at 544 s.

## Placement traces (`AiPlacementMeasure`, explicit test of `d96d3d9`)

Games 0 to 3 of each matchup (12 games, seed 20261117, the harness setup), once with `aggressive_s5` in both seats and once with `abl_s5_no_p` in both seats (`rts/build/place/*.log`; command `P_PROFILE=<id> P_GAMES=4 dotnet test rts/tests/Sim.Tests -c Release --no-build --filter "FullyQualifiedName~AiPlacementMeasure" --logger "console;verbosity=detailed"`). The helper prints build **orders** (not completed structures: an order that fails and is retried counts again), each with the base (the Rooted HQ or a complete depot) its tile stands nearest to. The helper's final state hashes agree with the P-6 hashes for the ablation games it shares (`235626745316a81d` skai_vs_skai 3, `ae4f362f8bdc9885` skai_vs_water 2, `b171e5cd2bf0bfeb` water_vs_water 2), so it replays the harness game. The ORDER lines are printed twice in the log; counts below use the per-game `GAME` lines. 24 player-games per profile.

| per 24 player-games | `aggressive_s5` | `abl_s5_no_p` |
| --- | ---: | ---: |
| producer orders nearest the HQ | 321 | 286 |
| producer orders nearest a depot | 21 | 20 |
| static defence orders nearest the HQ | 13 | 32 |
| static defence orders nearest a depot | 290 | 93 |
| median tick of a player's first static defence order | 135.0 s | 257.2 s |
| mean game length of these 12 games | 641.7 s | 580.7 s |

Producers: with AI-P on, depot producers (roost, tidepool, shell forge) were ordered at 132 s, 265 s and then 395 to 511 s (median 424.5 s, 19 orders; the other two of the 21 are windspires, supply buildings, which the rule leaves at their sites). So forward production is about 6 percent of producer orders; the HQ ring still takes the rest. The ablation also shows 20 producer orders nearest a depot (median 416 s): there the label only says the HQ-ring tile is nearer a depot than the HQ, so that number is the baseline rate of that coincidence, not forward production. Producers at depots in `aggressive_s5` are therefore barely above that baseline in these 12 games (21 against 20).

Defences: SD-4 changes the picture much more than P1. `aggressive_s5` orders 303 defences (290 at a depot) against the ablation's 125 (93), the first one 2 minutes earlier (135 s against 257 s), and depot defences are ordered from 224 s (first quartile), median 323 s, last 825 s. The count includes retries and the cap is 6 per base, so it is orders, not towers standing. More towers early and a longer game (641.7 s against 580.7 s over these games) go with the lost ladder.

## Self-ladder (`aggressive_s5` against itself), 216 of 216 decided, 4704.6 s

`dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s5 --profile-b aggressive_s5 --jobs 4 --out rts/build/ladder_p_self` (rerun from the start after the kill; it shared the machine with the trace runs, hence the longer time). Records: 216, all `aggressive_s5` in both seats. Determinism spot check 2 games, 0 mismatches.

```
WARNING opening skai/skai_air: wins 15 of 22 mirror games against other openings (0.682, above 0.65).
WARNING opening skai/skai_fast_expand: wins 14 of 20 mirror games against other openings (0.700, above 0.65).
WARNING opening skai/skai_windrunner_pressure: wins 4 of 20 mirror games against other openings (0.200, below 0.35).
WARNING opening water/water_fast_expand: wins 7 of 25 mirror games against other openings (0.280, below 0.35).
WARNING opening water/water_leviathan_tech: wins 25 of 31 mirror games against other openings (0.806, above 0.65).
WARNING opening water/water_standard: wins 11 of 32 mirror games against other openings (0.344, below 0.35).
LADDER candidate=aggressive_s5 baseline=aggressive_s5 decided=216 wins=107 fraction=0.495 distinct=216 mirror=71/144 cross=36/72 bar=0.650 FAIL
```

(The FAIL is meaningless in a self-ladder.)

| cell | decided | distinct games | distinct / decided | slot-A wins | Wilson |
| --- | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 36 | [38.7, 61.3] |
| skai_vs_water | 72 | 72 | 1.000 | 36 | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 35 | [37.4, 59.9] |

Rows: mirror 71/144 = 0.493 [41.3, 57.4]; cross 36/72 (Skai side 36 of 36, Water side 0 of 36); all 107/216 = 0.495 [42.9, 56.2]; candidate seat 0 / seat 1 63 and 44 wins (0.583 / 0.407). Undecided 0, draws 0. Median game length: all 628.1 s (min 454.6, max 1272.7); skai_vs_skai 626.7 s; skai_vs_water 592.2 s; water_vs_water 677.1 s.

Openings (mirror win fraction; draws counts as in the table above): skai_air 15/22 = **0.682**, skai_fast_expand 14/20 = **0.700**, skai_standard 12/28 = 0.429, skai_windrunner_pressure 4/20 = **0.200**, water_fast_expand 7/25 = **0.280**, water_grasper_pressure 10/18 = 0.556, water_leviathan_tech 25/31 = **0.806**, water_standard 11/32 = **0.344**. Six of eight are outside 0.35 to 0.65 (AI-E after the fixes: three; SAT-M: two). Report-only item.

## Smokes

`dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data` prints `STATE_HASH=e88b3b972175571d`; with `--script rts/data/scripts/economy_smoke.json` it prints `STATE_HASH=12259d6626becbc0`. Both as required.

## Acceptance table (spec section 9 row AI-P and tests P-0 to P-6)

| Item | Bar | Result | Verdict |
| --- | --- | --- | --- |
| Ladder `aggressive_s5` vs `abl_s5_no_p`, `--non-regression` | fraction >= 0.450 | 80/216 = 0.370 | **FAIL** |
| Mirror row Wilson upper bound | >= 0.50 | 44/144, upper bound 0.385 | **FAIL** |
| Self-ladder distinct games per decided game, every cell | >= 0.8 | 1.000, 1.000, 1.000 | PASS |
| Openings in mirrors 0.35 to 0.65 (report only) | report | vs ablation: 2 of 8 outside; self-ladder: 6 of 8 outside | reported |
| Response passes, not fewer than `abl_s5_no_p` | >= baseline | 6 of 10 against 5 of 10 | PASS |
| Response gate "6 of 9" (spec section 9 text) | 6 | 6 passed (the harness runs 10 scenarios) | met, mismatch of 9 against 10 not reconciled |
| Count of producers outside the HQ ring and of SD-4 defences per game | report | 21 and 290 orders in 24 player-games (ablation 20 and 93); see traces | reported |
| P-0 shipped values, parser ranges | pass | in the suite, 919 of 919 | PASS |
| P-1 producer at the base with fewest producers | pass | in the suite | PASS |
| P-2 switch off keeps HQ ring sites | pass | in the suite | PASS |
| P-3 exposed base and SD-4 (literal numbers on `ThreatDefenceHolds`) | pass | in the suite | PASS |
| P-4 effective cap | pass | in the suite | PASS |
| P-5 hysteresis | pass | in the suite | PASS |
| P-6 ablation plays the pre-AI-P game | pass | `P6_AblationPlaysThePreAiPGame` exists and passes; hashes in the test | PASS (provenance at `97f4d97` not re-derived) |
| Both smokes | `e88b3b972175571d`, `12259d6626becbc0` | both printed | PASS |
| Old profiles unchanged | no value change | diff shows only the six added keys at old values; `abl_s5_no_p` hashes match P-6 | PASS |
| AI pins from SAT hold without re-pinning | no re-pin | suite passes with no pin edited by AI-P (only profile-list tests and E-6 changed) | PASS as far as the suite shows |

**The package fails its gate** on the ladder and on the mirror interval. By spec section 9 the next step is: "as above with the P switches", that is, set the AI-P switches in `aggressive_s5` to the old values (the code stays) and let the developer decide. I did not do that and changed nothing in the data. Where the loss comes from is outside this report: in the traces SD-4 builds about three times as many defences, and about two minutes earlier, as the ablation, while the producers move to depots only slightly more than by coincidence; the two effects are not separated here (no ablation with only one of the two switches was run).

## Not checked

- No ablation with only P1 or only P2 on, so the loss is not attributed to one rule.
- The hashes of P-6 were not reproduced on `97f4d97`.
- Producer and defence figures are orders from 12 games per profile, not standing structures.
- The response gate count (9 in the spec, 10 printed) is not reconciled.
- The full suite was not rerun after the resume (tree unchanged since `d96d3d9`).

## Which part loses

Diagnostic question: does the loss against `abl_s5_no_p` come from P1 (forward producers) or from P2 (static defence by threat, with `static_defence_max_per_base` 6)? Two diagnostic profiles were added (data only, tests list them): `abl_s5_p1_only` (P1 on; P2 keys and `static_defence_max_per_base` 2 at the `abl_s5_no_p` values) and `abl_s5_p2_only` (P2 keys and cap 6 as in `aggressive_s5`; P1 off). Each is a copy of `aggressive_s5` differing only as stated. Nothing in a shipped profile, the code or any threshold was changed.

Ladder command (the AI-P ladder's own command line is not recorded above; this is the form read from the harness usage and the `ladder.md` header: baseline is `--profile-a`, candidate `--profile-b`, seed default 20261117, map `prototype_1v1`, 72 games per cell, `--non-regression`): `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a abl_s5_no_p --profile-b <diagnostic> --jobs 4 --non-regression --out rts/build/ladder_<p1_only|p2_only>`.

### P1 only: `abl_s5_p1_only` against `abl_s5_no_p`, 216 of 216 decided, 4365.5 s

(Machine shared with the trace runs.) Output `rts/build/ladder_p1_only/`. Determinism spot check 2 games, 0 mismatches.

```
WARNING opening skai/skai_air: wins 2 of 22 mirror games against other openings (0.091, below 0.35).
WARNING opening skai/skai_fast_expand: wins 14 of 20 mirror games against other openings (0.700, above 0.65).
WARNING opening skai/skai_standard: wins 19 of 28 mirror games against other openings (0.679, above 0.65).
LADDER candidate=abl_s5_p1_only baseline=abl_s5_no_p decided=216 wins=116 fraction=0.537 distinct=216 mirror=80/144 cross=36/72 bar=0.450 PASS
```

| row | decided | candidate wins | fraction | Wilson |
| --- | ---: | ---: | ---: | --- |
| mirror | 144 | 80 | 0.556 | [47.4, 63.4] |
| cross | 72 | 36 | 0.500 | [38.7, 61.3] |
| cross, candidate as skai / water | 36 / 36 | 36 / 0 | 1.000 / 0.000 | [90.4, 100.0] / [0.0, 9.6] |
| all | 216 | 116 | 0.537 | [47.0, 60.2] |

Per cell wins (of 72): skai_vs_skai 41 (0.569), skai_vs_water 36 (0.500), water_vs_water 39 (0.542). Median game length: all 555.5 s; skai_vs_skai 567.8 s; skai_vs_water 512.0 s; water_vs_water 610.8 s.

### P2 only: `abl_s5_p2_only` against `abl_s5_no_p`, 216 of 216 decided, 4048.9 s

(Machine shared with the trace runs only at its start.) Output `rts/build/ladder_p2_only/`.

```
WARNING opening water/water_fast_expand: wins 4 of 25 mirror games against other openings (0.160, below 0.35).
WARNING opening water/water_leviathan_tech: wins 21 of 31 mirror games against other openings (0.677, above 0.65).
WARNING mirror: candidate wins 45 of 144 distinct mirror games, Wilson upper bound 0.392 below 0.50; the candidate may be weaker than the baseline.
LADDER candidate=abl_s5_p2_only baseline=abl_s5_no_p decided=216 wins=81 fraction=0.375 distinct=216 mirror=45/144 cross=36/72 bar=0.450 FAIL
```

| row | decided | candidate wins | fraction | Wilson |
| --- | ---: | ---: | ---: | --- |
| mirror | 144 | 45 | 0.313 | [24.2, 39.2] |
| cross | 72 | 36 | 0.500 | [38.7, 61.3] |
| cross, candidate as skai / water | 36 / 36 | 36 / 0 | 1.000 / 0.000 | [90.4, 100.0] / [0.0, 9.6] |
| all | 216 | 81 | 0.375 | [31.3, 44.1] |

Per cell wins (of 72): skai_vs_skai 15 (0.208), skai_vs_water 36 (0.500), water_vs_water 30 (0.417). Median game length: all 581.1 s; skai_vs_skai 562.4 s; skai_vs_water 541.1 s; water_vs_water 635.7 s.

### Comparison (ladders against `abl_s5_no_p`, 216 games each)

| candidate | wins | fraction | mirror | skai_vs_skai | skai_vs_water | water_vs_water | median length (s) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `aggressive_s5` (full AI-P) | 80 | 0.370 | 44/144 (0.306) | 13 | 36 | 31 | 580.0 |
| `abl_s5_p1_only` | 116 | 0.537 | 80/144 (0.556) | 41 | 36 | 39 | 555.5 |
| `abl_s5_p2_only` | 81 | 0.375 | 45/144 (0.313) | 15 | 36 | 30 | 581.1 |

### Traces (`AiPlacementMeasure`, 12 games per profile, seed 20261117, games 0 to 3 per matchup, both seats the same profile; 24 player-games)

The helper now also prints, per game and player, the total cost of static defence orders and the army supply and army value at 300 s and 480 s (army = finished, mobile, non-worker, non-HQ units; value = common plus rare cost; -1 when the game had ended). Measured in a copy of the tree (so the running ladder's binaries stayed untouched); logs are not committed. Defence orders include retries and every defence costs 100 (30300 / 303), so cost is orders times 100.

| per 24 player-games | `aggressive_s5` | `abl_s5_p1_only` | `abl_s5_p2_only` | `abl_s5_no_p` |
| --- | ---: | ---: | ---: | ---: |
| static defence orders (HQ / depot) | 303 (13 / 290) | 108 (27 / 81) | 314 (13 / 301) | 125 (32 / 93) |
| total defence order cost | 30300 | 10800 | 31400 | 12500 |
| defence cost per player-game | 1262 | 450 | 1308 | 521 |
| median first defence order | 135.0 s | 257.2 s | 135.0 s | 257.2 s |
| army supply at 300 s (mean, 24 players) | 28.8 | 27.6 | 29.5 | 27.7 |
| army supply at 480 s (mean, players still in game: 20 / 20 / 22 / 20) | 43.3 | 42.5 | 44.0 | 47.5 |
| army value at 480 s (mean, same players) | 2744 | 2674 | 2788 | 3011 |
| defence cost per player-game as share of mean army value at 480 s | 0.46 | 0.17 | 0.47 | 0.17 |
| mean game length of these games | 641.7 s | 567.7 s | 690.1 s | 580.7 s |

The last share is a rough ratio of two different means (order cost over the whole game against army value standing at 480 s), not a per-game share of total spend.

### Reading

The loss sits in P2 (static defence by threat with cap 6), not in P1. P2 alone scores 81 of 216 (0.375), the full package 80 of 216 (0.370): the same, within one game, and the same cells lose (skai mirror 15 and 13 wins of 72, water mirror 30 and 31). P1 alone scores 116 of 216 (0.537), above 0.50 and above the 0.45 bar, mirror 80 of 144 (0.556). The trace agrees: with P2 on, defence orders are about 2.5 times the ablation's (303 and 314 against 125, with P1 alone at 108 and no earlier first defence), at 135 s against 257 s, costing about 1260 to 1310 per player-game, 0.46 of the army value standing at 480 s (0.17 off). Army supply at 480 s is 43 to 44 with P2 and 47.5 without; at 300 s it is the same for all four (27.6 to 29.5), so the army gap opens after 300 s, and the games with P2 run longer (641.7 s and 690.1 s against 580.7 s and 567.7 s). The numbers show no interaction between P1 and P2 that matters: the full package equals P2 alone, and P1 alone is not worse than the ablation (it is nominally better, 0.537, Wilson [47.0, 60.2], which includes 0.50; one ladder, so I do not claim P1 helps).

### Not checked

- One ladder per profile, seed 20261117 only: the Wilson interval of P1 alone includes 0.50, so "P1 is neutral" fits as well as "P1 helps".
- The cause inside P2 is not separated: the four threshold keys and `static_defence_max_per_base` 6 were switched together (the cap raised from 2 to 6 may alone account for the extra defences); no run of the cap alone or of a threshold alone.
- Traces are orders, not standing towers, from 12 games per profile; the army supply is a point value at two ticks, over players still in game (20 or 22 of 24 at 480 s, survivorship differs by profile); no kill or loss statistics were taken.
- The ladder command line of the earlier AI-P ladder is not recorded in this report; baseline in `--profile-a` and candidate in `--profile-b` was inferred from the harness usage and header (the new `ladder.md` headers confirm candidate and baseline).
- The profile-list tests were run on the AI classes (`FullyQualifiedName~Ai`: 234 of 235 passed on the first run, the failing `P0_ShippedValues` was fixed by skipping the two diagnostics and `AiPlacementTests` then passed 13 of 13); I did not rerun the whole solution.

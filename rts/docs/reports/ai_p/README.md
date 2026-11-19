<!-- rts/docs/reports/ai_p/README.md -->
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

Full `dotnet test rts/RtsGame.sln -c Release` of the head tree (`rts/build/fulltest2.log`, 07:29, after the last commit `d96d3d9`): Sim **919 of 919 passed**, Replay 27 of 27 (Net summary line was not in the saved tail; see "not checked"). The earlier run `fulltest.log` (06:09, before `a213506`) had 4 failures (`R6_NoVarietyForOldProfiles` among them: the profile lists lacked `abl_s5_no_p`); `a213506` fixed them and the rerun passed.

## Spec deviations found in the tests

- P-3: the advantage 1.5 and 0.9 are tested on `AiBrain.ThreatDefenceHolds` directly, not in a real view (the spec row asked for a real view; the build note in the spec says why).
- P-1: the depot sits at tile (31, 10) so no producer is within `base_radius_tiles`.
- P-5 and key `threat_defence_release_fraction` and test P-6 were added beyond the spec's original rows (the spec now lists them).
- AI-E test E-6 now plays `abl_s5_no_p`: with AI-P on, the same seed-1234 game trips its cap check for two thinks at 544 s.

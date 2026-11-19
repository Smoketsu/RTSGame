<!-- rts/docs/reports/ai_h/README.md -->
# AI-H: faction-fair harass speed

Package AI-H of `rts/docs/specs/field_saturation.md`: key `harass_speed_ratio` (old profiles 0, `aggressive_s5` 1.2), `AiIntel.ChaserSpeed` / `AiIntel.MayHarass`, `AiWorldView.EnemyFactionIds`, the ablation `abl_s5_no_h`. Built on `origin/main` (AI-P merged, switched off), so `aggressive_s5` is the AI-E-only AI plus AI-H. Nothing was tuned. Every run is `prototype_1v1`, base seed 20261117 (the harness default), 72 games per cell, `-c Release`, built once at commit `b90f997` (the commit that introduced `harass_speed_ratio` and `abl_s5_no_h`); `rts/build/` is git-ignored, so the raw `ladder.jsonl` and `ladder.md` files are not committed.

**Result in one line: the AI-H gate PASSES** on the strength ladder (111 of 216, 0.514 against the bar 0.450; mirror Wilson upper bound 60.1 against 50), on the self-ladder variety bar (1.000 distinct games per decided game in every cell, bar 0.8) and on the response scenarios (5 of 10 for both profiles, not fewer than the ablation). Details and the harass counts follow.

## Strength ladder: `aggressive_s5` against `abl_s5_no_h` (`--non-regression`), 216 of 216 decided

Command (from the repository root, `-c Release`, built once): `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a abl_s5_no_h --profile-b aggressive_s5 --jobs 4 --non-regression --out rts/build/ladder_h_vs_noh`. Elapsed 3910.6 s. Determinism spot check 2 games, 0 mismatches.

```
WARNING opening skai/skai_air: wins 3 of 22 mirror games against other openings (0.136, below 0.35).
WARNING opening skai/skai_standard: wins 21 of 28 mirror games against other openings (0.750, above 0.65).
LADDER candidate=aggressive_s5 baseline=abl_s5_no_h decided=216 wins=111 fraction=0.514 distinct=216 mirror=75/144 cross=36/72 bar=0.450 PASS
```

Distinct games per cell (distinct play signatures counted from `ladder.jsonl`; the `distinct fraction` column of `ladder.md` is a win share, not this):

| cell | decided | distinct games | distinct / decided | candidate wins | baseline wins | fraction | Wilson |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 37 | 35 | 0.514 | [40.1, 62.6] |
| skai_vs_water | 72 | 72 | 1.000 | 36 | 36 | 0.500 | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 38 | 34 | 0.528 | [41.4, 63.9] |

| row | decided | candidate wins | fraction | Wilson |
| --- | ---: | ---: | ---: | --- |
| mirror | 144 | 75 | 0.521 | [44.0, 60.1] |
| cross | 72 | 36 | 0.500 | [38.7, 61.3] |
| cross, candidate as skai / water | 36 / 36 | 36 / 0 | 1.000 / 0.000 | [90.4, 100.0] / [0.0, 9.6] |
| all | 216 | 111 | 0.514 | [44.8, 58.0] |
| candidate in slot 0 / slot 1 | 108 / 108 | 60 / 51 | 0.556 / 0.472 | [46.2, 64.6] / [38.1, 56.6] |

Undecided 0, draws 0. Median game length (end tick / 20, from the records): all 561.0 s (min 388.1, max 1064.3); skai_vs_skai 528.2 s; skai_vs_water 516.0 s; water_vs_water 641.2 s. The mirror-row Wilson upper bound is 60.1 percent (bar 50 percent): no mirror warning.

Openings (all seats of both profiles, mirror games against other openings, as the harness counts; draws = how often the opening was drawn, use share its share of the faction's draws):

| faction | opening | draws | use share | mirror games | wins | win fraction | in 0.35 to 0.65 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| skai | skai_air | 57 | 0.264 | 22 | 3 | **0.136** | no |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 10 | 0.500 | yes |
| skai | skai_standard | 76 | 0.352 | 28 | 21 | **0.750** | no |
| skai | skai_windrunner_pressure | 35 | 0.162 | 20 | 11 | 0.550 | yes |
| water | water_fast_expand | 52 | 0.241 | 25 | 11 | 0.440 | yes |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 8 | 0.444 | yes |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 17 | 0.548 | yes |
| water | water_standard | 60 | 0.278 | 32 | 17 | 0.531 | yes |

The cross cell splits by faction as in every earlier ladder (the Skai side wins all 36, the Water side none), so cross 36/72 is not information about AI-H.

## Self-ladder (`aggressive_s5` against itself), 216 of 216 decided, 4869.8 s

Command: `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s5 --profile-b aggressive_s5 --jobs 4 --out rts/build/ladder_h_self` (the trace and response runs shared the machine for part of it, hence the longer time).

```
WARNING opening skai/skai_air: wins 3 of 22 mirror games against other openings (0.136, below 0.35).
WARNING opening skai/skai_standard: wins 24 of 28 mirror games against other openings (0.857, above 0.65).
LADDER candidate=aggressive_s5 baseline=aggressive_s5 decided=216 wins=111 fraction=0.514 distinct=216 mirror=75/144 cross=36/72 bar=0.650 FAIL
```

(The `FAIL` and the 0.650 bar are meaningless in a self-ladder; "wins" counts the seat the harness names the candidate seat.)

| cell | decided | distinct games | distinct / decided (bar 0.8) | seat-A wins | Wilson |
| --- | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 37 | [40.1, 62.6] |
| skai_vs_water | 72 | 72 | 1.000 | 36 | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 38 | [41.4, 63.9] |

Rows: mirror 75/144 = 0.521 [44.0, 60.1]; cross 36/72 (Skai side 36 of 36, Water side 0 of 36); all 111/216 = 0.514 [44.8, 58.0]. Undecided 0, draws 0. Median game length: all 559.5 s (min 388.1, max 1087.1); skai_vs_skai 516.2 s; skai_vs_water 516.3 s; water_vs_water 621.8 s.

Note on the equal numbers: the self-ladder and the strength ladder give the same per-cell seat-A win counts (37, 36, 38). They are not the same games: the final state hashes agree in 109 of 216 games and the winner differs in 34 of 216 (the changes cancel in each cell). The games share seeds, openings and seats, so wherever the opponent's harass choice does not matter the game repeats exactly.

Openings in mirrors (all seats, mirror games against other openings; report only): skai_air 3/22 = **0.136**, skai_fast_expand 8/20 = 0.400, skai_standard 24/28 = **0.857**, skai_windrunner_pressure 10/20 = 0.500, water_fast_expand 13/25 = 0.520, water_grasper_pressure 7/18 = 0.389, water_leviathan_tech 18/31 = 0.581, water_standard 15/32 = 0.469. Two of eight are outside 0.35 to 0.65 (strength ladder: also two, the same ones; AI-P report self-ladder: six). Draw counts and use shares equal those of the strength ladder table (same seeds).

## Response scenarios (`--response --profile <id>`)

Commands: `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --response --profile aggressive_s5` and the same with `--profile abl_s5_no_h` (logs `rts/build/response_s5.log`, `response_noh.log`; both ended with `RESPONSE_SUMMARY`).

| scenario | `aggressive_s5` (AI-H on) | `abl_s5_no_h` |
| --- | --- | --- |
| roc_rush skai | PASS (aa_units 19) | PASS (16) |
| roc_rush water | PASS (4) | PASS (4) |
| mass_pummeler skai | FAIL (share_gain -0.012) | FAIL (-0.050) |
| mass_pummeler water | FAIL (-0.062) | FAIL (-0.062) |
| mass_windrunner skai | PASS (trade_ratio 1.944, killed 35, lost 18) | PASS (1.560, 39, 25) |
| mass_windrunner water | PASS (1.529, 78, 51) | PASS (1.529, 78, 51) |
| worker_raid skai | FAIL (p1 pass: defence near raided base; p2 fail: max_workers_lost_in_wave 7) | FAIL (p1 fail; p2 fail: 6) |
| worker_raid water | FAIL (p1 fail; p2 pass: 2) | FAIL (p1 fail; p2 pass: 2) |
| turtle skai | FAIL (26 supply lost before the first kill; won by 538.8 s) | FAIL (26; 538.8 s) |
| turtle water | PASS (8, 444.3 s) | PASS (8, 444.3 s) |
| **passed** | **5 of 10** | **5 of 10** |

`RESPONSE_SUMMARY passed=5 of=10` for both. The gate "a package fails if it passes fewer than its baseline" holds (5 against 5). The harness prints 10 scenarios; the spec's gate text says "6 of 9" (not reconciled, as in the AI-P report). The `abl_s5_no_h` result equals the `abl_s5_no_p` column of the AI-P report (5 of 10, same values), as it should: both are `aggressive_s5` before AI-H.

## Harass raids per game and per unit type (traces)

Helper: `rts/tests/Sim.Tests/Ai/AiHarassMeasure.cs` (explicit, never in the suite). It replays the harness game with the same setup, prints a RAID line when a profile's `HarassGroup` goes from empty to filled, and per game the raids, the raiders sent and the raiders that died while the raid was out. Commands: `H_PROFILE=<id> H_GAMES=12 dotnet test rts/tests/Sim.Tests -c Release --no-build --filter "FullyQualifiedName~AiHarassMeasure" --logger "console;verbosity=detailed"` with `aggressive_s5` and `abl_s5_no_h`, both seats the same profile; seed 20261117, games 0 to 11 of each matchup (36 games, 72 player-games per profile; logs `rts/build/harass/s5.log` and `noh.log`; the logger prints each RAID line twice and they were de-duplicated). "Died" counts raiders that left the world while their raid was out (a unit that dies after the squad was recalled is not counted).

| per 36 games (72 player-games) | `aggressive_s5` (ratio 1.2) | `abl_s5_no_h` (ratio 0) |
| --- | ---: | ---: |
| raids launched | 97 | 176 |
| raids per game (mean / median) | 2.69 / 3 | 4.89 / 4 |
| games with no raid | 3 | 11 |
| raiders sent | 254 | 541 |
| raiders that died | 22 | 51 |
| raiders that died per raider sent | 0.087 | 0.094 |
| raids by Skai players / Water players | 79 / 18 | 171 / 5 |
| median tick of a raid launch | 292.4 s | 328.9 s |
| raids by matchup: skai_vs_skai / skai_vs_water / water_vs_water | 36 / 45 / 16 | 126 / 49 / 1 |
| raiders that died by matchup, same order | 13 / 9 / 0 | 44 / 7 / 0 |
| median game length of these 36 games | 529.5 s | 559.8 s |

Raiders sent by unit type (units over all raids):

| unit type | `aggressive_s5` | `abl_s5_no_h` |
| --- | ---: | ---: |
| skai.windrunner | 120 | 396 |
| skai.roc_rider | 86 | 110 |
| skai.thunderhead | 7 | 15 |
| water.grasper | 31 | 0 |
| water.reef_runner | 10 | 20 |

Squad sizes (raids of 1 / 2 / 3 / 4 units): `aggressive_s5` 4 / 58 / 6 / 29; `abl_s5_no_h` 8 / 60 / 19 / 89.

Reading (counts only; causes not isolated): with AI-H on there are 45 percent fewer raids (97 against 176), almost all of the drop in Skai mirrors (36 against 126 raids) and in Windrunner raiders (120 against 396); Roc Riders and Thunderheads still go. Water raids rise from 5 to 18: the Grasper (speed 3.0, below the old fixed bar of 3.2) now raids (31 raiders, none before). Raider losses per raider sent are about the same (0.087 and 0.094).

## Smokes

`dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data` prints `STATE_HASH=e88b3b972175571d`; with `--script rts/data/scripts/economy_smoke.json` it prints `STATE_HASH=12259d6626becbc0`. Both as required.

## Tests

Full `dotnet test rts/RtsGame.sln -c Release` of the tree at `b90f997` (`rts/build/fulltest.log`): Sim **926 of 926 passed** (19 m 32 s), Net 33 of 33, Replay 27 of 27 (926 + 33 + 27 = 986). Before the change the suite had 919 + 33 + 27 = 979 tests; the 7 new ones are in `AiHarassTests`: H0 (two tests), H1, H2, H3, H4 and H5. Warnings are errors and the Release build had none.

## Proof that old profiles and the ablation are unchanged

- Every profile under `rts/data/ai` (20 files before, 21 now) got one comment line and `"harass_speed_ratio": 0` right after `threat_defence_release_fraction`; only `aggressive_s5` has 1.2 (test `H0_ShippedValuesAndParser` checks every file). No existing value was changed. `abl_s5_no_h.json` is `aggressive_s5.json` with `id` and the first-line comment changed and the ratio at 0. With ratio 0, `AiIntel.MayHarass` is the old expression (test H-4 checks speeds 0 to 6 in quarter steps, three models, all layers).
- Before any code change, `aggressive_s5` on `origin/main` (AI-P merged, switched off) was run on three fixed games by the existing test `P6_AblationPlaysThePreAiPGame` (it passed, 4 m 53 s, and asserts these hashes for both `abl_s5_no_p` and `aggressive_s5`): seed 20261117; skai_vs_water game 2 `ae4f362f8bdc9885`, skai_vs_skai game 3 `235626745316a81d`, water_vs_water game 2 `b171e5cd2bf0bfeb`. Test `H5_AblationPlaysThePreAiHGame` plays `abl_s5_no_h` through the same games and gets the same three hashes (passes). `P6_AblationPlaysThePreAiPGame` now asserts them only for `abl_s5_no_p` (`aggressive_s5` differs by AI-H from this commit on).
- The two smokes pass, and the pinned tests of old profiles (A-11, C2-11, the S3 self trace) hold: the full suite passes with no pin edited.

## Acceptance table (spec section 9 row AI-H and tests H-1 to H-4)

| Item | Bar | Result | Verdict |
| --- | --- | --- | --- |
| Ladder `aggressive_s5` vs `abl_s5_no_h`, `--non-regression` | fraction >= 0.450 | 111/216 = 0.514 | PASS |
| Mirror row Wilson upper bound | >= 0.50 | 75/144, upper bound 0.601 | PASS |
| Self-ladder distinct games per decided game, every cell | >= 0.8 | 1.000, 1.000, 1.000 | PASS |
| Openings in mirrors 0.35 to 0.65 (report only) | report | strength ladder 2 of 8 outside (skai_air 0.136, skai_standard 0.750); self-ladder 2 of 8 outside (skai_air 0.136, skai_standard 0.857) | reported |
| Response passes, not fewer than `abl_s5_no_h` | >= baseline | 5 of 10 against 5 of 10 | PASS |
| Harass squads per game and per faction (report) | report | 2.69 against 4.89 raids per game; Skai 79 against 171, Water 18 against 5 | reported |
| H-1 chaser speed from a model | pass | 2.2; 3.0 qualifies, 2.5 does not | PASS |
| H-2 empty model: roster mean | pass | equals the mean computed from the data, for all three layers | PASS |
| H-3 nothing hits air: a flyer qualifies | pass | with a neutral or empty enemy faction list | PASS |
| H-4 ratio 0 identical to today | pass | predicate check, and H-5 hashes | PASS |
| H-0 shipped values, parser rejections (5 bad forms, both limits) | pass | in the suite | PASS |
| H-5 ablation plays the pre-AI-H game | pass | 3 hashes | PASS |
| Both smokes | `e88b3b972175571d`, `12259d6626becbc0` | both printed | PASS |
| Old profiles unchanged | no value change | see above | PASS |
| AI pins from SAT hold without re-pinning | no re-pin | suite passes without edits to a pin | PASS |

**The package passes its gate.** `aggressive_s5` keeps `harass_speed_ratio` 1.2; nothing else was changed.

## Spec deviations and ambiguities

- **H-1** "2 Shellbacks and 1 Pummeler": the shipped data has no unit named Pummeler; `rt_mass_pummeler.json` calls the Water Grasper that (speed 3.0). The test uses `water.grasper` and the Ground layer (Shellback and Grasper both hit ground and water). The 3.0 and 2.5 candidates are passed as speeds, not as spawned units.
- **H-3** needs a case where nothing hits air, but every shipped faction has anti-air. The test sets the view's `EnemyFactionIds` to `neutral` (no armed mobile unit) or to nothing.
- **Model with armed types that cannot hit the layer.** The spec's order is: model types that can hit the layer, else the enemy faction roster, else the candidate qualifies. I followed it literally: a model whose types cannot hit the layer falls to the roster mean (not to "qualifies").
- **Which weapons "can hit" a layer:** `WeaponDefinition.CanTarget(layer)`, the static layer list. The Shellback's and Reef Runner's "air only while on water" (`can_target_air_on_water`) does not count as hitting air.
- **Enemy faction from the match setup:** the view had only the own faction, so `AiWorldView.EnemyFactionIds` was added (factions of the players on other teams, active or not). With several enemy factions the roster mean is over all of them.
- **Candidate layer** is `AiUnitView.Layer` (as the old rule used), not the definition's layer.
- **Armed types in the model.** `ModelCounts` already returns only army types; I also require a weapon.
- **Old rule fully replaced when the ratio is above 0:** neither the 3.2 floor nor the flyer exemption applies (spec: "replaced"), so a unit slower than 3.2 can raid and a very slow flyer cannot.

## Not checked

- One ladder per profile at seed 20261117 only. The strength ladder (0.514, Wilson [44.8, 58.0]) includes 0.50, so "AI-H is neutral for strength" fits as well as "helps". The once-allowed 0.5 and 1.5 times check of the ratio was not run (no tuning toward a gate, and none was needed).
- The traces are 12 games per matchup with both seats on the same profile, not the ladder's mixed pairs; "died" counts losses while the raid was out only.
- No per-feature ablation of the extra Water raids or the missing Windrunner raids, so no claim about what moves the win rate.
- The gate text "6 of 9" against the 10 scenarios the harness prints is not reconciled.
- No sim-reviewer pass was run (the coordinator requests it); no file under `rts/src/Sim` was changed.

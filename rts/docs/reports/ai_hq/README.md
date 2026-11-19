<!-- rts/docs/reports/ai_hq/README.md -->
# AI-HQ: HQ mobility (cluster relocation)

Package AI-HQ of `rts/docs/specs/field_saturation.md`: rule HQ-C, keys `use_cluster_relocation` (old profiles false, `aggressive_s5` true) and `relocate_gain_ratio` (old 1, `aggressive_s5` 2.0), `relocate_when_home_remaining_fraction` 0.35 in `aggressive_s5` (old 0.1), the ablation `abl_s5_no_hq`. Built on `origin/main` at `d89533a` (AI-E on, AI-P switched off, AI-H on), so `aggressive_s5` is the AI-E plus AI-H AI plus AI-HQ. Nothing was tuned. Every run is `prototype_1v1`, base seed 20261117 (the harness default), 72 games per cell, `-c Release`, built once; `rts/build/` is git-ignored, so the raw `ladder.jsonl` and `ladder.md` files are not committed.

RESULT_PLACEHOLDER

## Strength ladder: `aggressive_s5` against `abl_s5_no_hq` (`--non-regression`), 216 of 216 decided

Command (from the repository root, `-c Release`, built once): `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a abl_s5_no_hq --profile-b aggressive_s5 --jobs 4 --non-regression --out rts/build/ladder_hq_vs_nohq`. Elapsed 4065.2 s. Determinism spot check 2 games, 0 mismatches.

```
WARNING opening skai/skai_air: wins 3 of 22 mirror games against other openings (0.136, below 0.35).
WARNING opening skai/skai_standard: wins 24 of 28 mirror games against other openings (0.857, above 0.65).
WARNING opening water/water_leviathan_tech: wins 23 of 31 mirror games against other openings (0.742, above 0.65).
LADDER candidate=aggressive_s5 baseline=abl_s5_no_hq decided=216 wins=111 fraction=0.514 distinct=216 mirror=75/144 cross=36/72 bar=0.450 PASS
```

Distinct games per cell (distinct play signatures counted from `ladder.jsonl`; the `distinct fraction` column of `ladder.md` is a win share, not this):

| cell | decided | distinct games | distinct / decided | candidate wins | baseline wins | fraction | Wilson |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 32 | 40 | 0.444 | [33.5, 55.9] |
| skai_vs_water | 72 | 72 | 1.000 | 36 | 36 | 0.500 | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 43 | 29 | 0.597 | [48.2, 70.3] |

| row | decided | candidate wins | fraction | Wilson |
| --- | ---: | ---: | ---: | --- |
| mirror | 144 | 75 | 0.521 | [44.0, 60.1] |
| cross | 72 | 36 | 0.500 | [38.7, 61.3] |
| cross, candidate as skai / water | 36 / 36 | 36 / 0 | 1.000 / 0.000 | [90.4, 100.0] / [0.0, 9.6] |
| all | 216 | 111 | 0.514 | [44.8, 58.0] |
| candidate in slot 0 / slot 1 | 108 / 108 | 56 / 55 | 0.519 / 0.509 | [42.5, 61.0] / [41.6, 60.2] |

Undecided 0, draws 0. Median game length (end tick / 20, from the records): all 592.8 s (min 388.1, max 1296.9); skai_vs_skai 619.2 s; skai_vs_water 508.1 s; water_vs_water 694.1 s. The mirror-row Wilson upper bound is 60.1 percent (bar 50 percent): no mirror warning.

Openings (all seats of both profiles, mirror games against other openings, as the harness counts; draws = how often the opening was drawn, use share its share of the faction's draws):

| faction | opening | draws | use share | mirror games | wins | win fraction | in 0.35 to 0.65 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| skai | skai_air | 57 | 0.264 | 22 | 3 | **0.136** | no |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 10 | 0.500 | yes |
| skai | skai_standard | 76 | 0.352 | 28 | 24 | **0.857** | no |
| skai | skai_windrunner_pressure | 35 | 0.162 | 20 | 8 | 0.400 | yes |
| water | water_fast_expand | 52 | 0.241 | 25 | 10 | 0.400 | yes |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 8 | 0.444 | yes |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 23 | **0.742** | no |
| water | water_standard | 60 | 0.278 | 32 | 12 | 0.375 | yes |

Three of eight openings are outside 0.35 to 0.65 (report only; the AI-H ladder had two: the same two Skai ones). The cross cell splits by faction as in every earlier ladder (the Skai side wins all 36, the Water side none), so cross 36/72 is not information about AI-HQ.

## Self-ladder (`aggressive_s5` against itself), 216 of 216 decided, 4188.1 s

Command: `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s5 --profile-b aggressive_s5 --jobs 4 --out rts/build/ladder_hq_self`.

```
WARNING opening skai/skai_air: wins 3 of 22 mirror games against other openings (0.136, below 0.35).
WARNING opening skai/skai_standard: wins 22 of 28 mirror games against other openings (0.786, above 0.65).
WARNING opening water/water_fast_expand: wins 6 of 25 mirror games against other openings (0.240, below 0.35).
WARNING opening water/water_leviathan_tech: wins 26 of 31 mirror games against other openings (0.839, above 0.65).
WARNING opening water/water_standard: wins 11 of 32 mirror games against other openings (0.344, below 0.35).
LADDER candidate=aggressive_s5 baseline=aggressive_s5 decided=216 wins=109 fraction=0.505 distinct=216 mirror=73/144 cross=36/72 bar=0.650 FAIL
```

(The `FAIL` and the 0.650 bar are meaningless in a self-ladder; "wins" counts the seat the harness names the candidate seat.)

| cell | decided | distinct games | distinct / decided (bar 0.8) | seat-A wins | Wilson |
| --- | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 38 | [41.4, 63.9] |
| skai_vs_water | 72 | 72 | 1.000 | 36 | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 35 | [37.4, 59.9] |

Rows: mirror 73/144 = 0.507 [42.6, 58.7]; cross 36/72 (Skai side 36 of 36, Water side 0 of 36); all 109/216 = 0.505 [43.8, 57.1]. Undecided 0, draws 0. Median game length: all 614.7 s (min 388.1, max 1240.1); skai_vs_skai 645.4 s; skai_vs_water 509.6 s; water_vs_water 686.4 s.

Openings in mirrors (all seats, mirror games against other openings; report only): skai_air 3/22 = **0.136**, skai_fast_expand 7/20 = 0.350, skai_standard 22/28 = **0.786**, skai_windrunner_pressure 13/20 = 0.650, water_fast_expand 6/25 = **0.240**, water_grasper_pressure 10/18 = 0.556, water_leviathan_tech 26/31 = **0.839**, water_standard 11/32 = **0.344**. Five of eight are outside 0.35 to 0.65 (AI-H self-ladder: two). Draw counts and use shares equal those of the strength ladder (same seeds).

## Response scenarios (`--response --profile <id>`)

Commands: `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --response --profile aggressive_s5` and the same with `--profile abl_s5_no_hq` (logs `rts/build/response_s5.log`, `response_nohq.log`; both ended with `RESPONSE_SUMMARY`).

| scenario | `aggressive_s5` (AI-HQ on) | `abl_s5_no_hq` |
| --- | --- | --- |
| roc_rush skai | PASS (aa_units 19) | PASS (19) |
| roc_rush water | PASS (4) | PASS (4) |
| mass_pummeler skai | FAIL (share_gain -0.012) | FAIL (-0.012) |
| mass_pummeler water | FAIL (-0.062) | FAIL (-0.062) |
| mass_windrunner skai | PASS (trade_ratio 2.059, killed 35, lost 17) | PASS (1.944, 35, 18) |
| mass_windrunner water | PASS (1.451, 74, 51) | PASS (1.529, 78, 51) |
| worker_raid skai | FAIL (p1 pass; p2 fail: max_workers_lost_in_wave 7) | FAIL (p1 pass; p2 fail: 7) |
| worker_raid water | FAIL (p1 fail; p2 pass: 2) | FAIL (p1 fail; p2 pass: 2) |
| turtle skai | PASS (16 supply lost before the first kill; won by 613.2 s) | FAIL (26; 538.8 s) |
| turtle water | PASS (8, 443.3 s) | PASS (8, 444.3 s) |
| **passed** | **6 of 10** | **5 of 10** |

`RESPONSE_SUMMARY passed=6 of=10` against `passed=5 of=10`. The gate "a package fails if it passes fewer than its baseline" holds (6 against 5). The harness prints 10 scenarios; the spec's gate text says "6 of 9" (not reconciled, as in the AI-P and AI-H reports). The `abl_s5_no_hq` column equals the AI-H report's `aggressive_s5` column except that turtle skai failed there too and mass_windrunner skai passed with 1.944. The scripted scenarios start from a prepared state; the one changed result (turtle skai) was not traced to an HQ move (which of the AI-HQ rules acted in it was not checked).

TRACE_PLACEHOLDER

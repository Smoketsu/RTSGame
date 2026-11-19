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

SELF_PLACEHOLDER

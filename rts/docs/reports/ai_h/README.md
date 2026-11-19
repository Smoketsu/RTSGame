<!-- rts/docs/reports/ai_h/README.md -->
# AI-H: faction-fair harass speed

Package AI-H of `rts/docs/specs/field_saturation.md`: key `harass_speed_ratio` (old profiles 0, `aggressive_s5` 1.2), `AiIntel.ChaserSpeed` / `AiIntel.MayHarass`, `AiWorldView.EnemyFactionIds`, the ablation `abl_s5_no_h`. Built on `origin/main` (AI-P merged, switched off), so `aggressive_s5` is the AI-E-only AI plus AI-H. Nothing was tuned. This report is filled in stage by stage; sections still marked PENDING were not measured yet.

**Status: IN PROGRESS** (ladder done, self-ladder, response and traces pending).

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

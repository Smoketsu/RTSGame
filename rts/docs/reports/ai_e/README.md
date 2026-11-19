<!-- rts/docs/reports/ai_e/README.md -->
# AI-E: the AI economy follows the saturation rule

Package AI-E of `rts/docs/specs/field_saturation.md`: worker caps from gather slots plus walk cover, the rare cap yielding to saturation, depots at 80 percent saturation, up to 4 live depots, a threat blocks a depot only from 4 supply. Branch `review/ai-e`, built on `origin/main` at 0fc7fa5. `prototype_1v1`, `-c Release`, `--jobs 4`, base seed 20261117, 72 games per cell. Baseline: `aggressive_s4r`; `abl_s5_no_e` (aggressive_s5 with only AI-E at its old values) plays it bit-identically (same state hash `e289ebedbf0ddc26` for water_vs_water game 2 over the whole game, and the 180 s hash test E-7), so the ladder baseline is `aggressive_s4r` as the spec's section 9 says. No threshold, weight, scenario or bar was changed after seeing results. `rts/build` is git-ignored; the numbers below were copied out of it as each run finished.

## Run 1: strength ladder (`aggressive_s5` candidate against `aggressive_s4r`, `--non-regression`), 216 of 216 decided, 2990.4 s

```
dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s4r --profile-b aggressive_s5 --non-regression --jobs 4 --out rts/build/ladder_e_vs_s4r
```

```
WARNING opening water/water_standard: wins 10 of 32 mirror games against other openings (0.313, below 0.35).
LADDER candidate=aggressive_s5 baseline=aggressive_s4r decided=216 wins=164 fraction=0.759 distinct=216 mirror=127/144 cross=37/72 bar=0.450 PASS
```

The determinism spot check re-ran 2 games, 0 mismatches. Distinct games per cell, computed from the play signatures of decided games in `ladder.jsonl` (the `ladder.md` "distinct fraction" column is a win share, not used):

| cell | decided | distinct | distinct / decided | candidate wins / decided | fraction | Wilson |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 67/72 | 0.931 | [84.8, 97.0] |
| skai_vs_water | 72 | 72 | 1.000 | 37/72 | 0.514 | [40.1, 62.6] |
| water_vs_water | 72 | 72 | 1.000 | 60/72 | 0.833 | [73.1, 90.2] |

Undecided 0, draws 0; slot 0 wins 108, slot 1 wins 108. Median game length (20 ticks per second): all 619.3 s (min 350.5, max 1156.5); skai_vs_skai 641.2 s; skai_vs_water 479.2 s; water_vs_water 693.8 s. No game reached the 2400 s cap (the SAT-M self-ladder of `aggressive_s4r` had a median of 736.4 s).

| row | decided | candidate wins | fraction | Wilson |
| --- | ---: | ---: | ---: | --- |
| mirror | 144 | 127 | 0.882 | [81.9, 92.5] |
| cross | 72 | 37 | 0.514 | [40.1, 62.6] |
| cross, candidate as skai | 36 | 36 | 1.000 | [90.4, 100.0] |
| cross, candidate as water | 36 | 1 | 0.028 | [0.5, 14.2] |
| all | 216 | 164 | 0.759 | [69.8, 81.1] |
| candidate in slot 0 / slot 1 | 108 / 108 | 82 / 82 | 0.759 / 0.759 | [67.1, 83.0] each |

Openings of the candidate (draws = times drawn; mirror = decided games against other openings of the same faction; the draws equal the SAT-M ones because the opening draw does not depend on the economy):

| faction | opening | draws | use share | mirror games | wins | win fraction | Wilson | cross games | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 57 | 0.264 | 22 | 9 | 0.409 | [23.3, 61.3] | 19 | 19 |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 13 | 0.650 | [43.3, 81.9] | 16 | 16 |
| skai | skai_standard | 76 | 0.352 | 28 | 11 | 0.393 | [23.6, 57.6] | 24 | 23 |
| skai | skai_windrunner_pressure | 35 | 0.162 | 20 | 12 | 0.600 | [38.7, 78.1] | 13 | 13 |
| water | water_fast_expand | 52 | 0.241 | 25 | 15 | 0.600 | [40.7, 76.6] | 15 | 0 |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 9 | 0.500 | [29.0, 71.0] | 20 | 1 |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 19 | 0.613 | [43.8, 76.3] | 17 | 0 |
| water | water_standard | 60 | 0.278 | 32 | 10 | 0.313 | [18.0, 48.6] | 20 | 0 |

## Run 3: traces (workers, waiting, depots, income) beside SAT-M

Twelve traces per profile: games 0 to 3 of each matchup, seed 20261117, the profile in both seats, `--max-game-seconds 2400`. Command per profile, matchup and game: `--trace --map prototype_1v1 --matchup <m> --game g --seed 20261117 --max-game-seconds 2400 --profile-a <p> --profile-b <p>`. `aggressive_s4r` was re-run here, because its SAT-M traces were not kept apart from the table; its 180 s and 300 s values are the SAT-M ones (14 workers, 0 waiting, 1 depot for nearly every player). Cells are workers / waiting / completed depots / common income in the 30 s before the sample; `-` means the game was over. Full per-trace lines and the summaries are in `trace_summary.txt` in this folder.

Medians over the player-samples (24 at 180 s and 300 s, 20 and 16 at 480 s because short games had ended):

| sample | profile | workers | waiting | completed depots | common income per 30 s |
| --- | --- | ---: | ---: | ---: | ---: |
| 180 s | aggressive_s5 | 17.0 (16 to 18) | 0.5 (max 2) | 2.0 (1 to 2) | 740 |
| 180 s | aggressive_s4r | 14.0 (11 to 14) | 0.0 (max 0) | 1.0 | 667.5 |
| 300 s | aggressive_s5 | 25.0 (12 to 26) | 1.0 (max 3) | 3.0 (1 to 4) | 990 |
| 300 s | aggressive_s4r | 14.0 (6 to 15) | 0.0 (max 2) | 1.0 (0 to 2) | 662.5 |
| 480 s | aggressive_s5 | 24.0 (0 to 34) | 1.5 (max 5) | 4.0 (0 to 4) | 905 |
| 480 s | aggressive_s4r | 17.0 (13 to 19) | 1.0 (max 3) | 2.0 | 885 |

First depot (placed or complete), median over 24 players: s5 90 s placed, 120 s complete; s4r 60 s placed, 90 s complete (the s4r figure is the 30 s sample at which it first shows, so it is coarse). Second depot complete: s5 180 s (24 of 24 players); s4r 330 s (20 of 24 players had one).

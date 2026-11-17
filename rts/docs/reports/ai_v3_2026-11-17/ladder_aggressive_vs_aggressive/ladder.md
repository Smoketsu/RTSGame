<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive against baseline aggressive

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive baseline=aggressive decided=288 wins=135 fraction=0.469 bar=0.650 FAIL**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 8 | 16 | 0 | 0 | 0.333 | [18.0, 53.3] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| greybox_1v1 / water_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| long_lowland / skai_vs_skai | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |
| long_lowland / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| long_lowland / water_vs_water | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 9 | 15 | 0 | 0 | 0.375 | [21.2, 57.3] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 7 | 17 | 0 | 0 | 0.292 | [14.9, 49.2] |
| prototype_1v1 / water_vs_water | 24 | 24 | 14 | 10 | 0 | 0 | 0.583 | [38.8, 75.5] |
| twin_chokes / skai_vs_skai | 24 | 24 | 13 | 11 | 0 | 0 | 0.542 | [35.1, 72.1] |
| twin_chokes / skai_vs_water | 24 | 24 | 15 | 9 | 0 | 0 | 0.625 | [42.7, 78.8] |
| twin_chokes / water_vs_water | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 32 | 40 | 0 | 0 | 0.444 | [33.5, 55.9] |
| long_lowland | 72 | 72 | 34 | 38 | 0 | 0 | 0.472 | [36.1, 58.6] |
| prototype_1v1 | 72 | 72 | 30 | 42 | 0 | 0 | 0.417 | [31.0, 53.2] |
| twin_chokes | 72 | 72 | 39 | 33 | 0 | 0 | 0.542 | [42.7, 65.2] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 78 | 66 | 0 | 0 | 0.542 | [46.0, 62.1] |
| slot 1 | 144 | 144 | 57 | 87 | 0 | 0 | 0.396 | [32.0, 47.7] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 135 | 153 | 0 | 0 | 0.469 | [41.2, 52.6] |

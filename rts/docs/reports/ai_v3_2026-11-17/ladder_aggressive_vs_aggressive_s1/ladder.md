<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s1 against baseline aggressive

- Base seed 20261117, 24 games per cell, game cap 2400 s, non-regression bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s1 baseline=aggressive decided=288 wins=119 fraction=0.413 bar=0.450 FAIL**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 8 | 16 | 0 | 0 | 0.333 | [18.0, 53.3] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |
| greybox_1v1 / water_vs_water | 24 | 24 | 14 | 10 | 0 | 0 | 0.583 | [38.8, 75.5] |
| long_lowland / skai_vs_skai | 24 | 24 | 8 | 16 | 0 | 0 | 0.333 | [18.0, 53.3] |
| long_lowland / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| long_lowland / water_vs_water | 24 | 24 | 10 | 14 | 0 | 0 | 0.417 | [24.5, 61.2] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 5 | 19 | 0 | 0 | 0.208 | [9.2, 40.5] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 15 | 9 | 0 | 0 | 0.625 | [42.7, 78.8] |
| twin_chokes / skai_vs_skai | 24 | 24 | 3 | 21 | 0 | 0 | 0.125 | [4.3, 31.0] |
| twin_chokes / skai_vs_water | 24 | 24 | 9 | 15 | 0 | 0 | 0.375 | [21.2, 57.3] |
| twin_chokes / water_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 33 | 39 | 0 | 0 | 0.458 | [34.8, 57.3] |
| long_lowland | 72 | 72 | 30 | 42 | 0 | 0 | 0.417 | [31.0, 53.2] |
| prototype_1v1 | 72 | 72 | 32 | 40 | 0 | 0 | 0.444 | [33.5, 55.9] |
| twin_chokes | 72 | 72 | 24 | 48 | 0 | 0 | 0.333 | [23.5, 44.8] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 65 | 79 | 0 | 0 | 0.451 | [37.2, 53.3] |
| slot 1 | 144 | 144 | 54 | 90 | 0 | 0 | 0.375 | [30.0, 45.6] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 119 | 169 | 0 | 0 | 0.413 | [35.8, 47.1] |

<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s2b against baseline abl_s2_blind

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s2b baseline=abl_s2_blind decided=288 wins=176 fraction=0.611 bar=0.650 FAIL**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| greybox_1v1 / water_vs_water | 24 | 24 | 16 | 8 | 0 | 0 | 0.667 | [46.7, 82.0] |
| long_lowland / skai_vs_skai | 24 | 24 | 14 | 10 | 0 | 0 | 0.583 | [38.8, 75.5] |
| long_lowland / skai_vs_water | 24 | 24 | 17 | 7 | 0 | 0 | 0.708 | [50.8, 85.1] |
| long_lowland / water_vs_water | 24 | 24 | 18 | 6 | 0 | 0 | 0.750 | [55.1, 88.0] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 20 | 4 | 0 | 0 | 0.833 | [64.1, 93.3] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 17 | 7 | 0 | 0 | 0.708 | [50.8, 85.1] |
| twin_chokes / skai_vs_skai | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |
| twin_chokes / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| twin_chokes / water_vs_water | 24 | 24 | 15 | 9 | 0 | 0 | 0.625 | [42.7, 78.8] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 40 | 32 | 0 | 0 | 0.556 | [44.1, 66.5] |
| long_lowland | 72 | 72 | 49 | 23 | 0 | 0 | 0.681 | [56.6, 77.7] |
| prototype_1v1 | 72 | 72 | 49 | 23 | 0 | 0 | 0.681 | [56.6, 77.7] |
| twin_chokes | 72 | 72 | 38 | 34 | 0 | 0 | 0.528 | [41.4, 63.9] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 79 | 65 | 0 | 0 | 0.549 | [46.7, 62.8] |
| slot 1 | 144 | 144 | 97 | 47 | 0 | 0 | 0.674 | [59.3, 74.5] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 176 | 112 | 0 | 0 | 0.611 | [55.4, 66.6] |

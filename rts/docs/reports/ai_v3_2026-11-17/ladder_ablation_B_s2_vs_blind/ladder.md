<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s2 against baseline abl_s2_blind

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s2 baseline=abl_s2_blind decided=288 wins=126 fraction=0.438 bar=0.650 FAIL**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 5 | 19 | 0 | 0 | 0.208 | [9.2, 40.5] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 13 | 11 | 0 | 0 | 0.542 | [35.1, 72.1] |
| greybox_1v1 / water_vs_water | 24 | 24 | 6 | 18 | 0 | 0 | 0.250 | [12.0, 44.9] |
| long_lowland / skai_vs_skai | 24 | 24 | 14 | 10 | 0 | 0 | 0.583 | [38.8, 75.5] |
| long_lowland / skai_vs_water | 24 | 24 | 20 | 4 | 0 | 0 | 0.833 | [64.1, 93.3] |
| long_lowland / water_vs_water | 24 | 24 | 17 | 7 | 0 | 0 | 0.708 | [50.8, 85.1] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 0 | 24 | 0 | 0 | 0.000 | [0.0, 13.8] |
| twin_chokes / skai_vs_skai | 24 | 24 | 13 | 11 | 0 | 0 | 0.542 | [35.1, 72.1] |
| twin_chokes / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| twin_chokes / water_vs_water | 24 | 24 | 2 | 22 | 0 | 0 | 0.083 | [2.3, 25.8] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 24 | 48 | 0 | 0 | 0.333 | [23.5, 44.8] |
| long_lowland | 72 | 72 | 51 | 21 | 0 | 0 | 0.708 | [59.5, 80.1] |
| prototype_1v1 | 72 | 72 | 24 | 48 | 0 | 0 | 0.333 | [23.5, 44.8] |
| twin_chokes | 72 | 72 | 27 | 45 | 0 | 0 | 0.375 | [27.2, 49.0] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 57 | 87 | 0 | 0 | 0.396 | [32.0, 47.7] |
| slot 1 | 144 | 144 | 69 | 75 | 0 | 0 | 0.479 | [39.9, 56.0] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 126 | 162 | 0 | 0 | 0.438 | [38.1, 49.5] |

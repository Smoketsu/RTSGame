<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s2b against baseline abl_s2_blind

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s2b baseline=abl_s2_blind decided=288 wins=154 fraction=0.535 bar=0.650 FAIL**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 8 | 16 | 0 | 0 | 0.333 | [18.0, 53.3] |
| greybox_1v1 / water_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| long_lowland / skai_vs_skai | 24 | 24 | 10 | 14 | 0 | 0 | 0.417 | [24.5, 61.2] |
| long_lowland / skai_vs_water | 24 | 24 | 18 | 6 | 0 | 0 | 0.750 | [55.1, 88.0] |
| long_lowland / water_vs_water | 24 | 24 | 20 | 4 | 0 | 0 | 0.833 | [64.1, 93.3] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 21 | 3 | 0 | 0 | 0.875 | [69.0, 95.7] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 14 | 10 | 0 | 0 | 0.583 | [38.8, 75.5] |
| twin_chokes / skai_vs_skai | 24 | 24 | 10 | 14 | 0 | 0 | 0.417 | [24.5, 61.2] |
| twin_chokes / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| twin_chokes / water_vs_water | 24 | 24 | 6 | 18 | 0 | 0 | 0.250 | [12.0, 44.9] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 31 | 41 | 0 | 0 | 0.431 | [32.3, 54.6] |
| long_lowland | 72 | 72 | 48 | 24 | 0 | 0 | 0.667 | [55.2, 76.5] |
| prototype_1v1 | 72 | 72 | 47 | 25 | 0 | 0 | 0.653 | [53.8, 75.2] |
| twin_chokes | 72 | 72 | 28 | 44 | 0 | 0 | 0.389 | [28.5, 50.4] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 80 | 64 | 0 | 0 | 0.556 | [47.4, 63.4] |
| slot 1 | 144 | 144 | 74 | 70 | 0 | 0 | 0.514 | [43.3, 59.4] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 154 | 134 | 0 | 0 | 0.535 | [47.7, 59.1] |

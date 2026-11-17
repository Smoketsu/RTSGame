<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s2b against baseline abl_s2_blind

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s2b baseline=abl_s2_blind decided=288 wins=172 fraction=0.597 bar=0.650 FAIL**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 14 | 10 | 0 | 0 | 0.583 | [38.8, 75.5] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |
| greybox_1v1 / water_vs_water | 24 | 24 | 15 | 9 | 0 | 0 | 0.625 | [42.7, 78.8] |
| long_lowland / skai_vs_skai | 24 | 24 | 13 | 11 | 0 | 0 | 0.542 | [35.1, 72.1] |
| long_lowland / skai_vs_water | 24 | 24 | 17 | 7 | 0 | 0 | 0.708 | [50.8, 85.1] |
| long_lowland / water_vs_water | 24 | 24 | 21 | 3 | 0 | 0 | 0.875 | [69.0, 95.7] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 23 | 1 | 0 | 0 | 0.958 | [79.8, 99.3] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 15 | 9 | 0 | 0 | 0.625 | [42.7, 78.8] |
| twin_chokes / skai_vs_skai | 24 | 24 | 5 | 19 | 0 | 0 | 0.208 | [9.2, 40.5] |
| twin_chokes / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| twin_chokes / water_vs_water | 24 | 24 | 14 | 10 | 0 | 0 | 0.583 | [38.8, 75.5] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 40 | 32 | 0 | 0 | 0.556 | [44.1, 66.5] |
| long_lowland | 72 | 72 | 51 | 21 | 0 | 0 | 0.708 | [59.5, 80.1] |
| prototype_1v1 | 72 | 72 | 50 | 22 | 0 | 0 | 0.694 | [58.0, 78.9] |
| twin_chokes | 72 | 72 | 31 | 41 | 0 | 0 | 0.431 | [32.3, 54.6] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 87 | 57 | 0 | 0 | 0.604 | [52.3, 68.0] |
| slot 1 | 144 | 144 | 85 | 59 | 0 | 0 | 0.590 | [50.9, 66.7] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 172 | 116 | 0 | 0 | 0.597 | [54.0, 65.2] |

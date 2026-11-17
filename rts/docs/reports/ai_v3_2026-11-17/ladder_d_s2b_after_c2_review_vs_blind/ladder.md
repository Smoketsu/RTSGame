<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s2b against baseline abl_s2_blind

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s2b baseline=abl_s2_blind decided=288 wins=177 fraction=0.615 bar=0.650 FAIL**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 13 | 11 | 0 | 0 | 0.542 | [35.1, 72.1] |
| greybox_1v1 / water_vs_water | 24 | 24 | 18 | 6 | 0 | 0 | 0.750 | [55.1, 88.0] |
| long_lowland / skai_vs_skai | 24 | 24 | 8 | 16 | 0 | 0 | 0.333 | [18.0, 53.3] |
| long_lowland / skai_vs_water | 24 | 24 | 19 | 5 | 0 | 0 | 0.792 | [59.5, 90.8] |
| long_lowland / water_vs_water | 24 | 24 | 15 | 9 | 0 | 0 | 0.625 | [42.7, 78.8] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 19 | 5 | 0 | 0 | 0.792 | [59.5, 90.8] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 16 | 8 | 0 | 0 | 0.667 | [46.7, 82.0] |
| twin_chokes / skai_vs_skai | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |
| twin_chokes / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| twin_chokes / water_vs_water | 24 | 24 | 22 | 2 | 0 | 0 | 0.917 | [74.2, 97.7] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 43 | 29 | 0 | 0 | 0.597 | [48.2, 70.3] |
| long_lowland | 72 | 72 | 42 | 30 | 0 | 0 | 0.583 | [46.8, 69.0] |
| prototype_1v1 | 72 | 72 | 47 | 25 | 0 | 0 | 0.653 | [53.8, 75.2] |
| twin_chokes | 72 | 72 | 45 | 27 | 0 | 0 | 0.625 | [51.0, 72.8] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 85 | 59 | 0 | 0 | 0.590 | [50.9, 66.7] |
| slot 1 | 144 | 144 | 92 | 52 | 0 | 0 | 0.639 | [55.8, 71.3] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 177 | 111 | 0 | 0 | 0.615 | [55.7, 66.9] |

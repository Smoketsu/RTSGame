<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s3 against baseline aggressive_s2b

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s3 baseline=aggressive_s2b decided=288 wins=255 fraction=0.885 bar=0.650 PASS**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 18 | 6 | 0 | 0 | 0.750 | [55.1, 88.0] |
| greybox_1v1 / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / skai_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| twin_chokes / skai_vs_skai | 24 | 24 | 20 | 4 | 0 | 0 | 0.833 | [64.1, 93.3] |
| twin_chokes / skai_vs_water | 24 | 24 | 13 | 11 | 0 | 0 | 0.542 | [35.1, 72.1] |
| twin_chokes / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 66 | 6 | 0 | 0 | 0.917 | [83.0, 96.1] |
| long_lowland | 72 | 72 | 72 | 0 | 0 | 0 | 1.000 | [94.9, 100.0] |
| prototype_1v1 | 72 | 72 | 60 | 12 | 0 | 0 | 0.833 | [73.1, 90.2] |
| twin_chokes | 72 | 72 | 57 | 15 | 0 | 0 | 0.792 | [68.4, 86.9] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 125 | 19 | 0 | 0 | 0.868 | [80.3, 91.4] |
| slot 1 | 144 | 144 | 130 | 14 | 0 | 0 | 0.903 | [84.3, 94.1] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 255 | 33 | 0 | 0 | 0.885 | [84.3, 91.7] |

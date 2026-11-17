<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s2 against baseline aggressive

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s2 baseline=aggressive decided=288 wins=285 fraction=0.990 bar=0.650 PASS**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| greybox_1v1 / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / skai_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / water_vs_water | 24 | 24 | 23 | 1 | 0 | 0 | 0.958 | [79.8, 99.3] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| prototype_1v1 / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| twin_chokes / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| twin_chokes / skai_vs_water | 24 | 24 | 22 | 2 | 0 | 0 | 0.917 | [74.2, 97.7] |
| twin_chokes / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 72 | 0 | 0 | 0 | 1.000 | [94.9, 100.0] |
| long_lowland | 72 | 72 | 71 | 1 | 0 | 0 | 0.986 | [92.5, 99.8] |
| prototype_1v1 | 72 | 72 | 72 | 0 | 0 | 0 | 1.000 | [94.9, 100.0] |
| twin_chokes | 72 | 72 | 70 | 2 | 0 | 0 | 0.972 | [90.4, 99.2] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 141 | 3 | 0 | 0 | 0.979 | [94.1, 99.3] |
| slot 1 | 144 | 144 | 144 | 0 | 0 | 0 | 1.000 | [97.4, 100.0] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 285 | 3 | 0 | 0 | 0.990 | [97.0, 99.6] |

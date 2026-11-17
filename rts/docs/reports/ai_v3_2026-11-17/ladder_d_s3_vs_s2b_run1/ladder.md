<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s3 against baseline aggressive_s2b

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s3 baseline=aggressive_s2b decided=288 wins=260 fraction=0.903 bar=0.650 PASS**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 20 | 4 | 0 | 0 | 0.833 | [64.1, 93.3] |
| greybox_1v1 / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / skai_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| long_lowland / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| twin_chokes / skai_vs_skai | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |
| twin_chokes / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| twin_chokes / water_vs_water | 24 | 24 | 24 | 0 | 0 | 0 | 1.000 | [86.2, 100.0] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 68 | 4 | 0 | 0 | 0.944 | [86.6, 97.8] |
| long_lowland | 72 | 72 | 72 | 0 | 0 | 0 | 1.000 | [94.9, 100.0] |
| prototype_1v1 | 72 | 72 | 60 | 12 | 0 | 0 | 0.833 | [73.1, 90.2] |
| twin_chokes | 72 | 72 | 60 | 12 | 0 | 0 | 0.833 | [73.1, 90.2] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 131 | 13 | 0 | 0 | 0.910 | [85.2, 94.6] |
| slot 1 | 144 | 144 | 129 | 15 | 0 | 0 | 0.896 | [83.5, 93.6] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 260 | 28 | 0 | 0 | 0.903 | [86.3, 93.2] |

<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate abl_s2_no_rare against baseline aggressive

- Base seed 20261117, 24 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=abl_s2_no_rare baseline=aggressive decided=288 wins=77 fraction=0.267 bar=0.650 FAIL**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 / skai_vs_skai | 24 | 24 | 10 | 14 | 0 | 0 | 0.417 | [24.5, 61.2] |
| greybox_1v1 / skai_vs_water | 24 | 24 | 9 | 15 | 0 | 0 | 0.375 | [21.2, 57.3] |
| greybox_1v1 / water_vs_water | 24 | 24 | 6 | 18 | 0 | 0 | 0.250 | [12.0, 44.9] |
| long_lowland / skai_vs_skai | 24 | 24 | 10 | 14 | 0 | 0 | 0.417 | [24.5, 61.2] |
| long_lowland / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| long_lowland / water_vs_water | 24 | 24 | 2 | 22 | 0 | 0 | 0.083 | [2.3, 25.8] |
| prototype_1v1 / skai_vs_skai | 24 | 24 | 2 | 22 | 0 | 0 | 0.083 | [2.3, 25.8] |
| prototype_1v1 / skai_vs_water | 24 | 24 | 12 | 12 | 0 | 0 | 0.500 | [31.4, 68.6] |
| prototype_1v1 / water_vs_water | 24 | 24 | 0 | 24 | 0 | 0 | 0.000 | [0.0, 13.8] |
| twin_chokes / skai_vs_skai | 24 | 24 | 3 | 21 | 0 | 0 | 0.125 | [4.3, 31.0] |
| twin_chokes / skai_vs_water | 24 | 24 | 11 | 13 | 0 | 0 | 0.458 | [27.9, 64.9] |
| twin_chokes / water_vs_water | 24 | 24 | 0 | 24 | 0 | 0 | 0.000 | [0.0, 13.8] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| greybox_1v1 | 72 | 72 | 25 | 47 | 0 | 0 | 0.347 | [24.8, 46.2] |
| long_lowland | 72 | 72 | 24 | 48 | 0 | 0 | 0.333 | [23.5, 44.8] |
| prototype_1v1 | 72 | 72 | 14 | 58 | 0 | 0 | 0.194 | [12.0, 30.0] |
| twin_chokes | 72 | 72 | 14 | 58 | 0 | 0 | 0.194 | [12.0, 30.0] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 144 | 144 | 39 | 105 | 0 | 0 | 0.271 | [20.5, 34.9] |
| slot 1 | 144 | 144 | 38 | 106 | 0 | 0 | 0.264 | [19.9, 34.1] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 288 | 288 | 77 | 211 | 0 | 0 | 0.267 | [22.0, 32.1] |

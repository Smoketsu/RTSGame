<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s3 against baseline aggressive_s2b

- Base seed 20261117, 72 games per cell, game cap 2400 s, win bar.
- Seats: in game g slot s plays the baseline when (g / 2) % 2 == s; factions swap on odd g.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s3 baseline=aggressive_s2b decided=216 wins=180 fraction=0.833 bar=0.650 PASS**

## Per cell

| map / matchup | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| prototype_1v1 / skai_vs_skai | 72 | 72 | 72 | 0 | 0 | 0 | 1.000 | [94.9, 100.0] |
| prototype_1v1 / skai_vs_water | 72 | 72 | 36 | 36 | 0 | 0 | 0.500 | [38.7, 61.3] |
| prototype_1v1 / water_vs_water | 72 | 72 | 72 | 0 | 0 | 0 | 1.000 | [94.9, 100.0] |

## Per map

| map | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| prototype_1v1 | 216 | 216 | 180 | 36 | 0 | 0 | 0.833 | [77.8, 87.7] |

## Per seat

| candidate seat | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 108 | 108 | 90 | 18 | 0 | 0 | 0.833 | [75.2, 89.2] |
| slot 1 | 108 | 108 | 90 | 18 | 0 | 0 | 0.833 | [75.2, 89.2] |

## Overall

| all | games | decided | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 216 | 216 | 180 | 36 | 0 | 0 | 0.833 | [77.8, 87.7] |

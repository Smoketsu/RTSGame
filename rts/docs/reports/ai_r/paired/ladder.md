<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s5_r against baseline aggressive_s5

- Base seed 20261117, 72 games per cell, game cap 2400 s, win bar.
- Paired layout (balance_measurement.md M.1.2): pair p = g / 2, the candidate plays slot g % 2, factions swap on odd pairs and both games of a pair share the seed of the pair.
- Pairing kind per faction (M.1.3): skai:full,water:full. The variety streams are keyed by (seed, slot); the small chance that a draw takes more than one raw draw is ignored.
- Sign test and interval at two-sided alpha 0.0500, exact (integer arithmetic); MDE at power from z = 0.8416.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s5_r baseline=aggressive_s5 decided=216 wins=96 fraction=0.444 distinct=215 mirror=60/144 cross=36/72 bar=0.650 FAIL**

**PAIRED candidate=aggressive_s5_r baseline=aggressive_s5 pairs=108 complete=108 distinct=108 concordant=64 candidate_both=16 baseline_both=28 p=0.0961 advantage=-5.6 interval=[-11.3,+1.0] mde=8.7 pairing=skai:full,water:full verdict=NO_MEASURABLE_EFFECT**

WARNING opening skai/skai_fast_expand: wins 14 of 20 mirror games against other openings (0.700, above 0.65).

WARNING opening water/water_grasper_pressure: wins 13 of 18 mirror games against other openings (0.722, above 0.65).

Intervals count each distinct game once (play signature, skirmish_ai_v4.md S.1); games with the same signature are the same game.

## Per cell

| map / matchup | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| prototype_1v1 / skai_vs_skai | 72 | 72 | 72 | 0.181 | 13 | 59 | 0 | 0 | 0.181 | [10.9, 28.5] |
| prototype_1v1 / skai_vs_water | 72 | 72 | 71 | 0.507 | 36 | 36 | 0 | 0 | 0.500 | [39.3, 62.0] |
| prototype_1v1 / water_vs_water | 72 | 72 | 72 | 0.653 | 47 | 25 | 0 | 0 | 0.653 | [53.8, 75.2] |

## Per map

| map | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| prototype_1v1 | 216 | 216 | 215 | 0.447 | 96 | 120 | 0 | 0 | 0.444 | [38.2, 51.3] |

## Per seat

| candidate seat | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 108 | 108 | 107 | 0.477 | 51 | 57 | 0 | 0 | 0.472 | [38.4, 57.0] |
| slot 1 | 108 | 108 | 108 | 0.417 | 45 | 63 | 0 | 0 | 0.417 | [32.8, 51.1] |

## Mirror and cross-faction

| row | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| cross | 72 | 72 | 71 | 0.507 | 36 | 36 | 0 | 0 | 0.500 | [39.3, 62.0] |
| cross, candidate as skai | 36 | 36 | 36 | 1.000 | 36 | 0 | 0 | 0 | 1.000 | [90.4, 100.0] |
| cross, candidate as water | 36 | 36 | 35 | 0.000 | 0 | 36 | 0 | 0 | 0.000 | [0.0, 9.9] |
| mirror | 144 | 144 | 144 | 0.417 | 60 | 84 | 0 | 0 | 0.417 | [33.9, 49.8] |

## Openings

| faction | opening | draws | use share | mirror games vs other openings (decided) | distinct | wins | win fraction | Wilson interval (distinct games) | cross games (decided) | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 58 | 0.269 | 24 | 24 | 10 | 0.417 | [24.5, 61.2] | 22 | 22 |
| skai | skai_fast_expand | 46 | 0.213 | 20 | 20 | 14 | 0.700 | [48.1, 85.5] | 10 | 10 |
| skai | skai_standard | 78 | 0.361 | 28 | 28 | 12 | 0.429 | [26.5, 60.9] | 26 | 26 |
| skai | skai_windrunner_pressure | 34 | 0.157 | 20 | 20 | 10 | 0.500 | [29.9, 70.1] | 14 | 14 |
| water | water_fast_expand | 54 | 0.250 | 30 | 30 | 16 | 0.533 | [36.1, 69.8] | 12 | 0 |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 18 | 13 | 0.722 | [49.1, 87.5] | 20 | 0 |
| water | water_leviathan_tech | 68 | 0.315 | 40 | 40 | 16 | 0.400 | [26.3, 55.4] | 16 | 0 |
| water | water_standard | 52 | 0.241 | 28 | 28 | 13 | 0.464 | [29.5, 64.2] | 24 | 0 |

## Pairs

Counts S, W, L and n are over distinct complete pairs (pair signature); the plain counts over all complete pairs are in the next three columns. r = (W + L) / n. The advantage is in points of game win rate with the exact Clopper-Pearson interval on the discordant share; MDE is the smallest effect that would have been detected with the stated power. The verdict uses the `all` row only.

| row | pairs | complete | distinct complete (n) | concordant (S) | candidate both (W) | baseline both (L) | plain S / W / L | r | openings held | p | advantage [interval] | MDE |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | ---: |
| prototype_1v1 / skai_vs_skai | 36 | 36 | 36 | 13 | 0 | 23 | 13 / 0 / 23 | 0.639 | 36 | 0.0000 | -31.9 [-32.0, -22.4] | 18.7 |
| prototype_1v1 / skai_vs_water | 36 | 36 | 36 | 36 | 0 | 0 | 36 / 0 / 0 | 0.000 | 36 | 1.0000 | +0.0 [+0.0, +0.0] | 9.6 |
| prototype_1v1 / water_vs_water | 36 | 36 | 36 | 15 | 16 | 5 | 15 / 16 / 5 | 0.583 | 36 | 0.0266 | +15.3 [+1.6, +24.4] | 17.9 |
| mirror | 72 | 72 | 72 | 28 | 16 | 28 | 28 / 16 / 28 | 0.611 | 72 | 0.0961 | -8.3 [-16.9, +1.5] | 13.0 |
| cross | 36 | 36 | 36 | 36 | 0 | 0 | 36 / 0 / 0 | 0.000 | 36 | 1.0000 | +0.0 [+0.0, +0.0] | 9.6 |
| all | 108 | 108 | 108 | 64 | 16 | 28 | 64 / 16 / 28 | 0.407 | 108 | 0.0961 | -5.6 [-11.3, +1.0] | 8.7 |

## Overall

| all | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 216 | 216 | 215 | 0.447 | 96 | 120 | 0 | 0 | 0.444 | [38.2, 51.3] |

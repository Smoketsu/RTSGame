<!-- ladder.md: written by the balance harness --ladder (rts/tools/BalanceHarness/LadderRunner.cs); not a source file. -->
# Ladder: candidate aggressive_s5 against baseline abl_s5_no_h

- Base seed 20261117, 72 games per cell, game cap 2400 s, win bar.
- Paired layout (balance_measurement.md M.1.2): pair p = g / 2, the candidate plays slot g % 2, factions swap on odd pairs and both games of a pair share the seed of the pair.
- Pairing kind per faction (M.1.3): skai:full,water:full. The variety streams are keyed by (seed, slot); the small chance that a draw takes more than one raw draw is ignored.
- Sign test and interval at two-sided alpha 0.0500, exact (integer arithmetic); MDE at power from z = 0.8416.
- Candidate fraction = candidate wins / decided games; draws count as decided, not as wins; the interval is Wilson at z = 1.96.
- Determinism spot check: 2 games re-run in-process, 0 mismatches.

**LADDER candidate=aggressive_s5 baseline=abl_s5_no_h decided=216 wins=107 fraction=0.495 distinct=182 mirror=71/144 cross=36/72 bar=0.650 FAIL**

**PAIRED candidate=aggressive_s5 baseline=abl_s5_no_h pairs=108 complete=108 distinct=108 concordant=83 candidate_both=12 baseline_both=13 p=1.0000 advantage=-0.5 interval=[-5.2,+4.4] mde=6.5 pairing=skai:full,water:full verdict=NO_MEASURABLE_EFFECT**

WARNING prototype_1v1/skai_vs_water: 49 distinct of 72 decided games (0.681); seeds barely vary games, do not read these win rates as balance (P-005 decision 11).

WARNING opening skai/skai_air: wins 4 of 24 mirror games against other openings (0.167, below 0.35).

WARNING opening skai/skai_windrunner_pressure: wins 14 of 20 mirror games against other openings (0.700, above 0.65).

Intervals count each distinct game once (play signature, skirmish_ai_v4.md S.1); games with the same signature are the same game.

## Per cell

| map / matchup | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| prototype_1v1 / skai_vs_skai | 72 | 72 | 72 | 0.486 | 35 | 37 | 0 | 0 | 0.486 | [37.4, 59.9] |
| prototype_1v1 / skai_vs_water | 72 | 72 | 49 | 0.735 | 36 | 36 | 0 | 0 | 0.500 | [59.7, 83.8] |
| prototype_1v1 / water_vs_water | 72 | 72 | 61 | 0.590 | 36 | 36 | 0 | 0 | 0.500 | [46.5, 70.5] |

## Per map

| map | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| prototype_1v1 | 216 | 216 | 182 | 0.588 | 107 | 109 | 0 | 0 | 0.495 | [51.5, 65.7] |

## Per seat

| candidate seat | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| slot 0 | 108 | 108 | 108 | 0.556 | 60 | 48 | 0 | 0 | 0.556 | [46.2, 64.6] |
| slot 1 | 108 | 108 | 108 | 0.435 | 47 | 61 | 0 | 0 | 0.435 | [34.5, 52.9] |

## Mirror and cross-faction

| row | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| cross | 72 | 72 | 49 | 0.735 | 36 | 36 | 0 | 0 | 0.500 | [59.7, 83.8] |
| cross, candidate as skai | 36 | 36 | 36 | 1.000 | 36 | 0 | 0 | 0 | 1.000 | [90.4, 100.0] |
| cross, candidate as water | 36 | 36 | 36 | 0.000 | 0 | 36 | 0 | 0 | 0.000 | [0.0, 9.6] |
| mirror | 144 | 144 | 133 | 0.534 | 71 | 73 | 0 | 0 | 0.493 | [44.9, 61.6] |

## Openings

| faction | opening | draws | use share | mirror games vs other openings (decided) | distinct | wins | win fraction | Wilson interval (distinct games) | cross games (decided) | cross wins |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| skai | skai_air | 58 | 0.269 | 24 | 24 | 4 | 0.167 | [6.7, 35.9] | 22 | 22 |
| skai | skai_fast_expand | 46 | 0.213 | 20 | 20 | 12 | 0.600 | [38.7, 78.1] | 10 | 10 |
| skai | skai_standard | 78 | 0.361 | 28 | 28 | 16 | 0.571 | [39.1, 73.5] | 26 | 26 |
| skai | skai_windrunner_pressure | 34 | 0.157 | 20 | 20 | 14 | 0.700 | [48.1, 85.5] | 14 | 14 |
| water | water_fast_expand | 54 | 0.250 | 30 | 25 | 14 | 0.467 | [30.0, 66.5] | 12 | 0 |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 13 | 10 | 0.556 | [35.5, 82.3] | 20 | 0 |
| water | water_leviathan_tech | 68 | 0.315 | 40 | 39 | 21 | 0.525 | [38.6, 68.4] | 16 | 0 |
| water | water_standard | 52 | 0.241 | 28 | 21 | 13 | 0.464 | [20.8, 59.1] | 24 | 0 |

## Pairs

Counts S, W, L and n are over distinct complete pairs (pair signature); the plain counts over all complete pairs are in the next three columns. r = (W + L) / n. The advantage is in points of game win rate with the exact Clopper-Pearson interval on the discordant share; MDE is the smallest effect that would have been detected with the stated power. The verdict uses the `all` row only.

| row | pairs | complete | distinct complete (n) | concordant (S) | candidate both (W) | baseline both (L) | plain S / W / L | r | openings held | p | advantage [interval] | MDE |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | ---: |
| prototype_1v1 / skai_vs_skai | 36 | 36 | 36 | 25 | 5 | 6 | 25 / 5 / 6 | 0.306 | 36 | 1.0000 | -1.4 [-10.2, +8.2] | 13.0 |
| prototype_1v1 / skai_vs_water | 36 | 36 | 36 | 36 | 0 | 0 | 36 / 0 / 0 | 0.000 | 36 | 1.0000 | +0.0 [+0.0, +0.0] | 9.6 |
| prototype_1v1 / water_vs_water | 36 | 36 | 36 | 22 | 7 | 7 | 22 / 7 / 7 | 0.389 | 36 | 1.0000 | +0.0 [-10.5, +10.5] | 14.6 |
| mirror | 72 | 72 | 72 | 47 | 12 | 13 | 47 / 12 / 13 | 0.347 | 72 | 1.0000 | -0.7 [-7.8, +6.5] | 9.8 |
| cross | 36 | 36 | 36 | 36 | 0 | 0 | 36 / 0 / 0 | 0.000 | 36 | 1.0000 | +0.0 [+0.0, +0.0] | 9.6 |
| all | 108 | 108 | 108 | 83 | 12 | 13 | 83 / 12 / 13 | 0.231 | 108 | 1.0000 | -0.5 [-5.2, +4.4] | 6.5 |

## Overall

| all | games | decided | distinct | distinct fraction | candidate wins | baseline wins | draws | undecided | candidate fraction | Wilson interval (distinct games) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| all games | 216 | 216 | 182 | 0.588 | 107 | 109 | 0 | 0 | 0.495 | [51.5, 65.7] |

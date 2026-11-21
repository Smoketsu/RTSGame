<!-- pool.md: written by the balance harness --pool (rts/tools/BalanceHarness/PoolRunner.cs); not a source file. -->
# Opponent pool: candidate aggressive_s5, stage 1

- Base seed 20261117, game cap 2400 s. Each member is the baseline of a paired ladder (balance_measurement.md M.1): pair p = g / 2, the candidate plays slot g % 2, factions swap on odd pairs, both games of a pair share the seed.
- Sign test and interval at two-sided alpha 0.0500, exact (integer arithmetic). "worse" is tested at alpha; "better" at alpha divided by the number of members run when better_bonferroni is on (on), so "better" needs a larger effect than the printed MDE, which is stated at alpha.
- Determinism spot check: 2 games of the first member re-run in-process, 0 mismatches.

**POOL candidate=aggressive_s5 stage=1 members=2 skipped=1 better=2 worse=0 no_effect=0 incomplete=0 games=368 verdict=PASS**

# Member s3

Profile aggressive_s3, stage 1.

Pairing kind per faction: skai:seed,water:seed.

**MEMBER s3 complete=92 distinct=92 concordant=5 candidate_both=87 baseline_both=0 p=0.0000 advantage=+47.3 interval=[+43.3,+47.3] mde=14.3 pairing=skai:seed,water:seed verdict=PASS**

## Pairs

Counts S, W, L and n are over distinct complete pairs (pair signature); the plain counts over all complete pairs are in the next three columns. r = (W + L) / n. The advantage is in points of game win rate with the exact Clopper-Pearson interval on the discordant share; MDE is the smallest effect that would have been detected with the stated power. The verdict uses the `all` row only.

| row | pairs | complete | distinct complete (n) | concordant (S) | candidate both (W) | baseline both (L) | plain S / W / L | r | openings held | p | advantage [interval] | MDE |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | ---: |
| prototype_1v1 / skai_vs_skai | 46 | 46 | 46 | 1 | 45 | 0 | 1 / 45 / 0 | 0.978 | 0 | 0.0000 | +48.9 [+41.1, +49.0] | 20.5 |
| prototype_1v1 / water_vs_water | 46 | 46 | 46 | 4 | 42 | 0 | 4 / 42 / 0 | 0.913 | 0 | 0.0000 | +45.7 [+37.8, +45.7] | 19.8 |
| mirror | 92 | 92 | 92 | 5 | 87 | 0 | 5 / 87 / 0 | 0.946 | 0 | 0.0000 | +47.3 [+43.3, +47.3] | 14.3 |
| all | 92 | 92 | 92 | 5 | 87 | 0 | 5 / 87 / 0 | 0.946 | 0 | 0.0000 | +47.3 [+43.3, +47.3] | 14.3 |

# Member s4r

Profile aggressive_s4r, stage 1.

Pairing kind per faction: skai:full,water:full.

**MEMBER s4r complete=92 distinct=92 concordant=21 candidate_both=70 baseline_both=1 p=0.0000 advantage=+37.5 interval=[+32.7,+38.6] mde=12.9 pairing=skai:full,water:full verdict=PASS**

## Pairs

Counts S, W, L and n are over distinct complete pairs (pair signature); the plain counts over all complete pairs are in the next three columns. r = (W + L) / n. The advantage is in points of game win rate with the exact Clopper-Pearson interval on the discordant share; MDE is the smallest effect that would have been detected with the stated power. The verdict uses the `all` row only.

| row | pairs | complete | distinct complete (n) | concordant (S) | candidate both (W) | baseline both (L) | plain S / W / L | r | openings held | p | advantage [interval] | MDE |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | ---: |
| prototype_1v1 / skai_vs_skai | 46 | 46 | 46 | 9 | 37 | 0 | 9 / 37 / 0 | 0.804 | 46 | 0.0000 | +40.2 [+32.5, +40.3] | 18.6 |
| prototype_1v1 / water_vs_water | 46 | 46 | 46 | 12 | 33 | 1 | 12 / 33 / 1 | 0.739 | 46 | 0.0000 | +34.8 [+25.5, +37.0] | 17.8 |
| mirror | 92 | 92 | 92 | 21 | 70 | 1 | 21 / 70 / 1 | 0.772 | 92 | 0.0000 | +37.5 [+32.7, +38.6] | 12.9 |
| all | 92 | 92 | 92 | 21 | 70 | 1 | 21 / 70 / 1 | 0.772 | 92 | 0.0000 | +37.5 [+32.7, +38.6] | 12.9 |

# Member s5

Profile aggressive_s5, stage 1.

Skipped: this member is the candidate's own profile.

# Gate

| member | status | verdict of its Pairs table | candidate both (W) | baseline both (L) | p | advantage [interval] | MDE |
| --- | --- | --- | ---: | ---: | ---: | --- | ---: |
| s3 | better | PASS | 87 | 0 | 0.0000 | +47.3 [+43.3, +47.3] | 14.3 |
| s4r | better | PASS | 70 | 1 | 0.0000 | +37.5 [+32.7, +38.6] | 12.9 |
| s5 | skipped | - | - | - | - | - | - |

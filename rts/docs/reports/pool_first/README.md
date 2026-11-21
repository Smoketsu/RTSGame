<!-- rts/docs/reports/pool_first/README.md: first run of the opponent pool (balance_measurement.md M.3), stage 1, candidate aggressive_s5 with AI-H switched off (harass_speed_ratio 0); the command, wall time, every table and verdict as the harness printed them, and the places where the spec was ambiguous. Part of rts/docs/reports; the files pool.md and pool.jsonl next to it are the harness output of this run. -->
# First pool run: stage 1, candidate aggressive_s5 (21 Nov 2026)

## What was run

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --pool --profile-b aggressive_s5 --stage 1 --jobs 4 --out rts/docs/reports/pool_first
```

- Code: branch `review/p006-m2`, built from `origin/main` at `2aa8299` plus the commits of this package (AI-H switched off on `aggressive_s5`, pool, behaviour checks, balance report). Data: `rts/data` as committed, content digest unchanged; `scripts/ai_eval/pool.json` as shipped (stage 1: `s3`, `s4r`, `s5`; mirror cells only, 46 pairs per cell, `better_bonferroni` on).
- Wall time: 89 min 44 s (5384 s; the harness's own `elapsed` line says 5383.3 s), start 10:15:25 UTC, end 11:45:09 UTC. The machine was shared with other sessions (load average 3 to 5 during the run), so this is a slow-side figure. Exit code 0.
- Games: 2 members played x 2 cells (`skai_vs_skai`, `water_vs_water` on `prototype_1v1`) x 46 pairs x 2 games = 368 games, game cap 2400 s, base seed 20261117 (`ladder_base_seed`). `s5` is the candidate's own profile and was skipped. Determinism spot check: 2 games of the first member re-run in-process, 0 mismatches.
- Not run: stage 2 (forced openings), as instructed.

## stdout, as printed

```
MEMBER s3 complete=92 distinct=92 concordant=5 candidate_both=87 baseline_both=0 p=0.0000 advantage=+47.3 interval=[+43.3,+47.3] mde=14.3 pairing=skai:seed,water:seed verdict=PASS
MEMBER s4r complete=92 distinct=92 concordant=21 candidate_both=70 baseline_both=1 p=0.0000 advantage=+37.5 interval=[+32.7,+38.6] mde=12.9 pairing=skai:full,water:full verdict=PASS
MEMBER s5 skipped (candidate)
POOL candidate=aggressive_s5 stage=1 members=2 skipped=1 better=2 worse=0 no_effect=0 incomplete=0 games=368 verdict=PASS
```

## Tables, as written to pool.md

### Member s3 (aggressive_s3, no openings, pairing kind skai:seed,water:seed)

| row | pairs | complete | distinct complete (n) | concordant (S) | candidate both (W) | baseline both (L) | plain S / W / L | r | openings held | p | advantage [interval] | MDE |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | ---: |
| prototype_1v1 / skai_vs_skai | 46 | 46 | 46 | 1 | 45 | 0 | 1 / 45 / 0 | 0.978 | 0 | 0.0000 | +48.9 [+41.1, +49.0] | 20.5 |
| prototype_1v1 / water_vs_water | 46 | 46 | 46 | 4 | 42 | 0 | 4 / 42 / 0 | 0.913 | 0 | 0.0000 | +45.7 [+37.8, +45.7] | 19.8 |
| mirror | 92 | 92 | 92 | 5 | 87 | 0 | 5 / 87 / 0 | 0.946 | 0 | 0.0000 | +47.3 [+43.3, +47.3] | 14.3 |
| all | 92 | 92 | 92 | 5 | 87 | 0 | 5 / 87 / 0 | 0.946 | 0 | 0.0000 | +47.3 [+43.3, +47.3] | 14.3 |

### Member s4r (aggressive_s4r, pairing kind skai:full,water:full)

| row | pairs | complete | distinct complete (n) | concordant (S) | candidate both (W) | baseline both (L) | plain S / W / L | r | openings held | p | advantage [interval] | MDE |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | ---: |
| prototype_1v1 / skai_vs_skai | 46 | 46 | 46 | 9 | 37 | 0 | 9 / 37 / 0 | 0.804 | 46 | 0.0000 | +40.2 [+32.5, +40.3] | 18.6 |
| prototype_1v1 / water_vs_water | 46 | 46 | 46 | 12 | 33 | 1 | 12 / 33 / 1 | 0.739 | 46 | 0.0000 | +34.8 [+25.5, +37.0] | 17.8 |
| mirror | 92 | 92 | 92 | 21 | 70 | 1 | 21 / 70 / 1 | 0.772 | 92 | 0.0000 | +37.5 [+32.7, +38.6] | 12.9 |
| all | 92 | 92 | 92 | 21 | 70 | 1 | 21 / 70 / 1 | 0.772 | 92 | 0.0000 | +37.5 [+32.7, +38.6] | 12.9 |

### Gate

| member | status | verdict of its Pairs table | candidate both (W) | baseline both (L) | p | advantage [interval] | MDE |
| --- | --- | --- | ---: | ---: | ---: | --- | ---: |
| s3 | better | PASS | 87 | 0 | 0.0000 | +47.3 [+43.3, +47.3] | 14.3 |
| s4r | better | PASS | 70 | 1 | 0.0000 | +37.5 [+32.7, +38.6] | 12.9 |
| s5 | skipped | - | - | - | - | - | - |

## Verdict

**POOL ... verdict=PASS**: no member is worse, two are better. "Better" was tested at 0.05 / 2 members run (Bonferroni); both p values print as 0.0000, far below it. The numbers are measured, not tuned: no threshold, bar or weight was changed after the run.

Reading it (not a finding of the harness): against `aggressive_s3` and `aggressive_s4r` in mirror cells the candidate wins almost every pair, so the discordant share r is 0.946 (s3) and 0.772 (s4r), well above the 0.5 where pairing starts to help; with `s3` the pairing is only `seed`, and the draws of the two profiles differ (openings held 0 because `s3` has none). This stage says the candidate is not worse than the older profiles; it does not say anything about a close opponent, which is what stage 2 (forced openings of `aggressive_s5` itself) is for.

## Where the spec was ambiguous, and what the builder did

1. **Several statuses for one member.** M.3.4 defines "worse" and "better" from the `all` row and INCOMPLETE per member, so one member can be several. A member gets one status, by precedence worse, incomplete, better, no_effect, which gives the pool verdict order the spec states (FAIL, INCOMPLETE, PASS, NO_MEASURABLE_EFFECT) and makes the counts on the POOL line add up to the members run.
2. **`members=` on the POOL line.** Taken as the members that played (skipped ones are in `skipped=`). The Bonferroni divisor is the same number (2 here), not the 3 members of stage 1 in the file; the spec says "the m members run".
3. **Name of the baseline side in a record.** M.3.3 only adds `"member"`. The baseline side's `profile_slot0` / `profile_slot1` now holds the member ID (for example `s5_standard`), because the profile ID alone would not tell the forced members apart; the candidate side holds its profile ID.
4. **Workers.** The pool worker gets `--profile-a <member's profile>` as well as `--pool-member <id>` (the shared option check wants the pair); it resolves the member, with its forced openings, from `pool.json` and ignores the profile text.
5. **`pool.md` shape.** "One `## Pairs` table per member": each member has a `# Member <id>` heading and the existing `## Pairs` table under it, then `# Gate`.
6. **Spot check.** "The first `ladder_verify_games` games of the first member": the first member that plays, in its first cell.
7. **Bar of a behaviour check (`bar=` in M.4.5).** For `static_defence_share` it is `max_share`; for `hq_move_new_income` the metric is the number of violating moves and the bar printed is 0.000 (any violating move violates the slot-game); `max_violation_fraction` decides the PASS or FAIL of the profile in both.
8. **Bank times (M.4.2).** "After the step at each time": the bank is read when the clock first equals the time (that many steps have run).
9. **`balance.jsonl` and `--from` (M.5.1).** A byte-identical re-render needs the run's profile, seed, games per cell, cap and the predictor result, none of which can be recovered from game records, so `balance.jsonl` has a meta line first and a predictor line last; `--from` reads both. `--fights-from` together with `--from` recomputes the predictor.
10. **Faction matrix cell (M.5.2).** "Wins / decided games" is printed as plain counts with the Wilson interval over distinct games (draws counted as losses, as in the ladder); the interval text carries the distinct count.
11. **Pool maps.** `pool_maps` is empty, so the ladder's maps (`prototype_1v1`) are used; `--maps` and `--matchups` override the file for testing.
12. **Mechanic applicability to the Core (M.5.3).** `core_capture` applies when the record's map has a Core (all shipped maps have one).

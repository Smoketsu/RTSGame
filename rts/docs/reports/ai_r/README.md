<!-- rts/docs/reports/ai_r/README.md -->
# R2.5 measurement: the skirmish AI uses research (candidate `aggressive_s5_r`)

Spec: `rts/docs/specs/research_and_unlocks.md` R2.5. Candidate `aggressive_s5_r` (a copy of `aggressive_s5` with `use_research` true) against `aggressive_s5` (`use_research` false). Map `prototype_1v1` only, three matchups, 72 games each (216 games), the defaults of `ai_eval.json` (base seed 20261121 for `--balance`, 20261117 for the ladder). Every bar is the spec's, unchanged; nothing was tuned after a result. Built from the Release build of the `review/p007-r2` branch on a 4-core container. The opponent pool and the other profiles were not run, and `use_research` stays false in every shipped profile except the candidate.

## Bars

| # | Requirement (spec) | Measured | Verdict |
| --- | --- | --- | --- |
| 1 | `CHECK balance_unspent` passes at 300 and 600 s for both factions (median rare at most 150) | Rare at 300 s: skai 205, water 170. At 600 s: skai 255, water 355. Common is within its bar (at most 370). The candidate has `use_budget_queues` false (it is a copy of `aggressive_s5`), so rule RQ-2 never ran | FAIL |
| 2 | Each research item reaches the use share 0.100 in its faction's games | `skai.stormcaller_plans` 0.977, `skai.thunderhead_plans` 0.991, `water.ink_lobber_plans` 0.968, `water.leviathan_plans` 0.949; none of the four is in the `balance_use` fail list | PASS |
| 3 | Median time of the first Thunderhead and first Leviathan moves later by at most the research time + 30 s (75 s for Thunderhead, 75 s for Leviathan) against the stored pre-R1 baseline `balance_first` | Candidate: first Thunderhead median 409.4 s (150 slot-games), first Leviathan 413.9 s (160). `balance_first` stores no first-unit times (its records hold only counts), so the baseline value does not exist | REPORTED, not judged (needs a decision, see below) |
| 4 | Skai rare spent per game does not fall below 1000 | Median 1280, mean 1395.1 (216 skai slot-games); `balance_first`: median 1130, mean 1234.9. The bar says "per game"; the median was used, as every other bar uses a median | PASS |
| 5 | Water's 900 s bank is reported | Water rare at 900 s: 1050 (n = 18 slot-games still running); `balance_first`: 1020 | reported |
| 6 | One traced game per matchup shows no `missing_prerequisite`, `already_queued` or `already_researched` rejection from the AI | `trace_skai_vs_skai.txt`, `trace_skai_vs_water.txt`, `trace_water_vs_water.txt` (game 0, candidate in both seats): "commands rejected, whole game" is empty for all six players | PASS |

Bar 3: the spec reads a stored pre-R1 baseline of the median first-Thunderhead and first-Leviathan time from `balance_first`, but those records carry no times. Regenerating it would mean running the pre-R1 code again with the new `first_tick` field, which is a different measurement from the one stated, so it was not done. The developer decides whether to regenerate it.

## Research and unit use (candidate, 216 games, 216 slot-games per faction)

Median completion tick of the research (`first_tick`, seconds = tick / 20), and how many slot-games finished it:

| Item | Slot-games finished | Median completion |
| --- | ---: | ---: |
| `skai.stormcaller_plans` | 211 | 367.4 s |
| `skai.thunderhead_plans` | 214 | 316.9 s |
| `water.ink_lobber_plans` | 209 | 289.5 s |
| `water.leviathan_plans` | 205 | 326.4 s |

The four gated units (share of slot-games that trained at least one; `balance_first` in brackets; median time of the first one):

| Unit | Share | First |
| --- | ---: | ---: |
| `skai.stormcaller` | 0.250 (0.347) | 390.5 s |
| `skai.thunderhead` | 0.694 (0.838) | 409.4 s |
| `water.ink_lobber` | 0.560 (0.731) | 332.0 s |
| `water.leviathan` | 0.741 (0.819) | 413.9 s |

Rare banks, lower median over the slot-games still running (bar 150), common is within its bar everywhere:

| Faction | 300 s | 600 s | 900 s |
| --- | ---: | ---: | ---: |
| skai | 205 | 255 | 175 |
| water | 170 | 355 | 1050 |

Other results of the balance report: `balance_use` FAIL (13 of 57 items below 0.100: walls, gates, Tidecaller, wave, Downdraft, wind baffle, HQ last stand: the same 13 as `balance_first`; the four research rows pass), `balance_predictor` FAIL (accuracy 0.735, bar 0.850), `balance_openings` PASS.

## Paired comparison against `aggressive_s5`

`LADDER candidate=aggressive_s5_r baseline=aggressive_s5 decided=216 wins=96 fraction=0.444 ... FAIL` and `PAIRED ... pairs=108 concordant=64 candidate_both=16 baseline_both=28 p=0.0961 advantage=-5.6 interval=[-11.3,+1.0] mde=8.7 verdict=NO_MEASURABLE_EFFECT`. The spec's bars do not use the ladder result; it is given for information. Note that `aggressive_s5` on R1 never researches, so it plays without its four gated units.

## Commands

Run from the repository root.

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5_r --maps prototype_1v1 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4 --out rts/docs/reports/ai_r/balance
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --ladder --paired --profile-a aggressive_s5 --profile-b aggressive_s5_r --maps prototype_1v1 --jobs 4 --out rts/docs/reports/ai_r/paired
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --trace --map prototype_1v1 --matchup <skai_vs_skai|skai_vs_water|water_vs_water> --profile-a aggressive_s5_r --profile-b aggressive_s5_r --out rts/docs/reports/ai_r/trace_<matchup>.txt
```

Wall-clock: balance report 2635 s (43 min 55 s); paired ladder 2724 s (45 min 24 s, exit code 4 because the ladder bar failed); traces 15 s, 35 s and 53 s.

Files: `balance/balance.md` and `balance/balance.jsonl` (records, with the new `first_tick` map), `paired/`, the three trace files. The medians above were computed from `balance/balance.jsonl` (lower median, as the harness does).

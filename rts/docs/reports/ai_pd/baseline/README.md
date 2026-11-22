<!-- rts/docs/reports/ai_pd/baseline/README.md -->
# PD-M baseline run B0: candidate `aggressive_s5_m` with the production and defence metrics (spec `rts/docs/specs/ai_production_defence.md` sections 7 and 9)

Same 216 games as `rts/docs/reports/ai_m2` (map `prototype_1v1`, three matchups, 72 games each, base seed 20261121 from `ai_eval.json`), now with the PD-M metrics in each slot summary. Release build of branch `review/ai-pd-m`. Nothing was tuned; no bar, profile or rule changed. Wall-clock 4679 s, exit code 0.

## Check M-0 (reproduction)

The spec names `reports/ai_m` as the reference; `ai_m2` is the same candidate after two wall-rule fixes on `main`, and `main` is what this branch is built on, so the comparison is against `ai_m2/balance/balance.jsonl`. Result: all 216 games have the same `state_hash` and `result` as `ai_m2` (the metrics are observation only). Not compared against `ai_m`, whose games differ from `ai_m2` where the wall fixes changed play.

## Baseline values of the bars (B0)

Cells are the slot's faction in a cross or mirror matchup. Values are medians over the cell's slot-games (mean of the two middle values for an even count), window 600 s. Bars: PD-B1 `worker_fed_losses`, PD-B2 `worker_losses`, PD-B3 `raid_unanswered_edge_seconds`, PD-B4 `bank_mean`, PD-B5 `producers x 1000 / income_60` at 300 s, PD-B6 `income 240-300`. PD-B7 (rejected-command kinds in the three B0 traces) is not part of this run: the traces are taken in PD-5.

### Production and defence metrics

Median over the cell's slot-games (the mean of the two middle values for an even count); cells are the slot's faction in a cross or a mirror matchup. Window 600 s; raid radius 12 tiles; income 240-300 is the sum of the income buckets covering 240 to 300 s (slot-games whose last bucket had not started are left out).

| cell | slot-games | worker_losses | worker_fed_losses | raid_seconds | raid_unanswered_edge_seconds | bank_mean | bank at 300 s | income 240-300 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 72 | 0 | 0 | 10 | 0.5 | 1232 | 662.5 | 1847.5 |
| skai mirror | 144 | 14.5 | 3 | 89.5 | 9 | 211 | 192.5 | 1815 |
| water cross | 72 | 31 | 6 | 127.5 | 8 | 301.5 | 545 | 1645 |
| water mirror | 144 | 11 | 1 | 98 | 10 | 551.5 | 695 | 1767.5 |

Samples (null after the game's end are left out). The last two columns are the median of producers x 1000 / income_60 and of capacity x 60 / income_60 per slot-game (integer division; slot-games with income_60 0 left out).

| cell | time (s) | n | producers | capacity | income_60 | army_supply | producers x 1000 / income_60 | capacity x 60 / income_60 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| skai cross | 180 | 72 | 4 | 1050 | 1385 | 12.5 | 2 | 44.5 |
| skai cross | 300 | 72 | 6 | 1800 | 1847.5 | 26.5 | 3 | 61 |
| skai cross | 450 | 70 | 11 | 3300 | 2400 | 59.5 | 4 | 85 |
| skai mirror | 180 | 144 | 4 | 1050 | 1405 | 13 | 2 | 43 |
| skai mirror | 300 | 144 | 6 | 1800 | 1815 | 22 | 3 | 59 |
| skai mirror | 450 | 140 | 10 | 3000 | 2027.5 | 42 | 4 | 89 |
| water cross | 180 | 72 | 4 | 1374 | 1337.5 | 12 | 2 | 59 |
| water cross | 300 | 72 | 5 | 1707 | 1645 | 19 | 3 | 70 |
| water cross | 450 | 70 | 9 | 3039 | 1905 | 21.5 | 5 | 102 |
| water mirror | 180 | 144 | 4 | 1374 | 1387.5 | 14 | 2 | 59 |
| water mirror | 300 | 144 | 6 | 2040 | 1767.5 | 26 | 3 | 69.5 |
| water mirror | 450 | 144 | 11 | 3705 | 2150 | 52 | 5 | 105 |


## Notes

- `bank at 300 s` and `income 240-300` agree with the known B0 values of the spec (Water cross 545 vs 535 and Skai cross 662.5 vs 652.5 for the bank differ slightly: the spec's figures were taken from `ai_m`, not `ai_m2`; income 240-300 Skai cross 1847.5 vs 1845, others equal).
- PD-B5 as written (`producers x 1000 / income_60`, integer division) gives values of 2 to 5 only, so its resolution is coarse: a candidate must be at least equal on these integers. Recorded as is; no change made.
- `capacity x 60 / income_60` is printed as the spec writes it (integer division).
- Games reaching 450 s: 70 of 72 in the cross cells, 140 of 144 in the Skai mirror; the rest end earlier and show no sample.

## Command

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5_m --maps prototype_1v1 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4 --out rts/docs/reports/ai_pd/baseline
```

Files: `balance.md` and `balance.jsonl` (the new metric keys are appended to each slot summary).

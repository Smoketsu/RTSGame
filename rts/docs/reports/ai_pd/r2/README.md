<!-- rts/docs/reports/ai_pd/r2/README.md -->
# PD-R2M measurement: raid rules revision 2 (candidate `aggressive_s5_pd`, run C2)

Spec `rts/docs/specs/ai_production_defence.md` section R2 (R2.4). Run C2: `aggressive_s5_pd` plays both seats, map `prototype_1v1`, three matchups, 72 games each (216 games, 432 slot-games), base seed 20261121, `--jobs 4`, Release build of branch `review/ai-pd-r2-build` at the commit that adds rules RS, RW and RA revision 2 (fixes H1 to H5) and the overrun metric. Baselines: B0 is `rts/docs/reports/ai_pd/baseline/README.md` (`aggressive_s5_m`); C1 is `rts/docs/reports/ai_pd/README.md` (`aggressive_s5_pd` before revision 2). Run B0b (`aggressive_s5_m`, `rts/docs/reports/ai_pd/baseline_r2/`) adds only the pre-overrun baselines. Nothing was tuned: no bar, threshold, profile value, key or rule changed after any result. A failing bar is reported as measured.

## Check B0b (R2.4 step 1)

All 216 games of B0b have the same `seed`, `state_hash`, `play_signature` and `result` as `rts/docs/reports/ai_pd/baseline/balance.jsonl` (the new keys are observation only). Its bar medians equal the B0 values recorded in the B0 README (for example water cross `worker_losses` 31, `worker_fed_losses` 6, income 240-300 1645, bank_mean 301.5), so every bar's B0 below is that recorded value.

## Bars (B0 / C1 / C2 per cell; medians over the cell's slot-games, window 600 s)

Cells: skai cross 72, water cross 72, skai mirror 144, water mirror 144 slot-games. A cell shows `B0 / C1 / C2` and the verdict of C2 against B0 by the bar's own formula; C1's verdict was in the C1 README.

| Bar | Pass when | skai cross | skai mirror | water cross | water mirror | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| PD-B1 `worker_fed_losses` | C2 <= floor(B0/2) if B0 >= 2, else <= B0 | 0 / 0 / 0 pass | 3 / 2 / 2 FAIL (limit 1) | 6 / 6 / 5 FAIL (limit 3) | 1 / 1 / 1 pass | FAIL (2 of 4 cells) |
| PD-B2 `worker_losses` | C2 <= floor(0.6 B0) if B0 >= 6, else <= B0+2 | 0 / 0 / 0 pass | 14.5 / 11 / 13 FAIL (limit 8) | 31 / 26 / 24.5 FAIL (limit 18) | 11 / 7 / 6 pass (limit 6) | FAIL (2 of 4) |
| PD-B3 `raid_unanswered_edge_seconds` | C2 <= floor(B0/2) if B0 >= 10, else <= B0+5 | 0.5 / 1 / 0 pass | 9 / 7 / 8 pass | 8 / 8.5 / 10 pass (limit 13) | 10 / 7 / 8 FAIL (limit 5) | FAIL (1 of 4) |
| PD-B4 `bank_mean` | C2 <= floor(0.6 B0) if B0 >= 300, else <= B0+50 | 1232 / 66.5 / 66 pass | 211 / 62 / 62 pass | 301.5 / 69.5 / 68.5 pass | 551.5 / 68 / 69.5 pass | PASS |
| PD-B5 producers x 1000 / income_60 at 300 s | C2 >= B0 | 3 / 4 / 4 pass | 3 / 4 / 4 pass | 3 / 5 / 5 pass | 3 / 4 / 4 pass | PASS |
| PD-B6 income 240-300 s | C2 >= 0.9 B0 | 1847.5 / 1815 / 1832.5 pass | 1815 / 1715 / 1695 pass | 1645 / 1317.5 / 1292.5 FAIL (limit 1480.5) | 1767.5 / 1750 / 1682.5 pass | FAIL (1 of 4) |
| PD-B7 clean commands | no rejection pair absent from the B0 traces | no rejection in any C2 trace | | | | PASS |

Overall: PD-B4, PD-B5 and PD-B7 pass; PD-B1, PD-B2, PD-B3 and PD-B6 fail in the cells shown. Against C1: PD-B2 water mirror changed from fail (7) to pass (6); PD-B3 water mirror still fails (8, limit 5), skai mirror and water cross passed in both runs; PD-B1 and PD-B6 fail in the same cells as in C1.

PD-B7 detail: the three C2 traces show "commands rejected, whole game=-" for all six players, so no rejection pair exists, whatever the kinds.

## Alternative reading, not a bar and not judged (R2-Q1)

PD-B1's and PD-B2's formulas applied to the pre-overrun keys (`worker_fed_losses_pre_overrun`, `worker_losses_pre_overrun`; overrun = enemy army supply of at least 20 within 12 tiles of an own drop-off), C2 against B0b. Cell medians, `B0b / C2`; a slot-game that never reached the overrun counts all its losses. The limit is the bar's formula applied to the B0b value.

| Reading | skai cross | skai mirror | water cross | water mirror |
| --- | --- | --- | --- | --- |
| PD-B1 on `worker_fed_losses_pre_overrun` | 0 / 0 (pass) | 2 / 2 (limit 1, would fail) | 2 / 2 (limit 1, would fail) | 0 / 0 (pass) |
| PD-B2 on `worker_losses_pre_overrun` | 0 / 0 (pass) | 7 / 7 (limit 4, would fail) | 9 / 7.5 (limit 5, would fail) | 3 / 2 (limit 5, would pass) |

Slot-games that reached the overrun, and the median `overrun_second` (B0b / C2): skai cross 12 of 72, 395.5 s / 4 of 72, 316 s; skai mirror 85 of 144, 430 s / 79 of 144, 429 s; water cross 68 of 72, 426.5 s / 71 of 72, 356 s; water mirror 104 of 144, 439 s / 102 of 144, 395 s. In the water cross cell the overrun comes 70 s sooner in C2 (as in C1, spec R2.0.2). The spec's guess of a limit near 8 for PD-B2 water cross (from the 12-game sample) does not hold: B0b's 72-game median is 9, so the limit is 5.

## Reported, not judged

Water cross (72 games, from `balance.jsonl`): median game end 556.3 s (B0), 485.4 s (C1), 494.3 s (C2); median HQ siege (first HQ damage to death) 40.4 s (B0), 19.9 s (C1), 25.4 s (C2). Median end tick over all 216 games: 11788.5 (B0), 10887.5 (C1), 10900.5 (C2).

Production samples, C2 (B0b in brackets): producers x 1000 / income_60, capacity x 60 / income_60.

| cell | 180 s | 300 s | 450 s |
| --- | --- | --- | --- |
| skai cross | 3 / 60 (2 / 44.5) | 4 / 81 (3 / 61) | 8 / 144.5 (4 / 85) |
| skai mirror | 3 / 49 (2 / 43) | 4 / 80 (3 / 59) | 7 / 129 (4 / 89) |
| water cross | 3 / 70 (2 / 59) | 5 / 116.5 (3 / 70) | 8 / 169.5 (5 / 102) |
| water mirror | 3 / 73 (2 / 59) | 4 / 91 (3 / 69.5) | 7 / 145 (5 / 105) |

Raid seconds (C2, C1, B0): skai cross 9 (8, 10), skai mirror 86.5 (84.5, 89.5), water cross 115.5 (120, 127.5), water mirror 86 (100, 98). Army supply at 300 s (C2, C1, B0): skai cross 27 (28.5, 26.5), skai mirror 21 (20.5, 22), water cross 18.5 (18, 19), water mirror 26 (28, 26). Full tables in `balance/balance.md`.

Faction matrix (information only, no win-rate target): Skai beat Water 72 of 72, Water beat Skai 0 of 72; mirrors: Skai seat 44/72, Water seat 36/72; no undecided game, no draw. Trust checks as printed: `balance_use` FAIL (only the two `hq_last_stand` rows, as in `ai_m2` and C1), `balance_predictor` FAIL (0.735, as C1), `balance_unspent` FAIL, `balance_openings` PASS.

### Traces (game 0 of each matchup, 30 s lines; idle workers and the first Skai `Attack` stance)

Idle workers per line, `idle` of `workers`, at 210 / 240 / 270 / 300 / 330 s:

| trace | player | C1 trace | C2 trace |
| --- | --- | --- | --- |
| skai_vs_water | Skai (P0) | 2/17, 1/20, 2/22, 0/24, 0/27 | 6/19, 1/21, 1/24, 0/26, 0/28 |
| skai_vs_water | Water (P1) | 6/16, 4/19, 3/18, 1/20, 1/21 | 3/17, 0/20, 2/20, 12/22, 0/13 |
| skai_vs_skai | Skai (P0) | 2/17, 2/19, 5/19, 1/19, 9/19 | 2/16, 2/16, 1/17, 2/18, 0/19 |
| skai_vs_skai | Skai (P1) | 3/18, 2/20, 0/23, 0/25, 0/28 | 4/18, 1/21, 0/23, 0/26, 0/28 |
| water_vs_water | Water (P0) | 4/20, 4/22, 2/25, 1/27, 1/29 | 0/18, 2/20, 3/22, 1/25, 1/27 |
| water_vs_water | Water (P1) | 3/21, 2/23, 1/20, 1/21, 0/23 | 2/19, 2/21, 1/17, 1/18, 1/21 |

First Skai `Attack` stance (30 s line), C1 / C2 trace: skai_vs_water P0 300 s / 300 s; skai_vs_skai P1 180 s / 180 s and P0 330 s / 450 s; water_vs_water none. Game end tick: skai_vs_water 10784 / 10222 (the Skai army overruns Water's base from about 480 s in the C2 trace: Water's workers 22 at 300 s, 13 at 330 s, 10 at 480 s, 0 at 510 s), skai_vs_skai 9660 / 10754, water_vs_water 13227 / 10540. One traced game per matchup, so these are examples, not medians.

## Commands

Run from the repository root after `dotnet build rts/RtsGame.sln -c Release`.

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5_m --maps prototype_1v1 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4 --out rts/docs/reports/ai_pd/baseline_r2
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5_pd --maps prototype_1v1 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4 --out rts/docs/reports/ai_pd/r2/balance
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --trace --map prototype_1v1 --matchup <skai_vs_skai|skai_vs_water|water_vs_water> --profile-a aggressive_s5_pd --profile-b aggressive_s5_pd --out rts/docs/reports/ai_pd/r2/trace_<matchup>.txt
```

Wall-clock: B0b 4728 s, C2 4227 s, traces 28 s, 64 s and 75 s; every exit code 0. Full suite before the runs: `dotnet test rts/RtsGame.sln -c Release` 33 + 30 + 1210 passed, 0 failed. Files: `balance/balance.md`, `balance/balance.jsonl`, `../baseline_r2/balance.md`, `../baseline_r2/balance.jsonl`, three trace files.

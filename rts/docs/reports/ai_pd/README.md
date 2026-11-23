<!-- rts/docs/reports/ai_pd/README.md -->
# PD-5 measurement: production capacity and raid defence (candidate `aggressive_s5_pd`, run C1)

Spec `rts/docs/specs/ai_production_defence.md` section 9. Run C1: `aggressive_s5_pd` (all four switches on) plays both seats, map `prototype_1v1`, three matchups, 72 games each (216 games, 432 slot-games), base seed 20261121, `--jobs 4`, Release build of branch `review/ai-pd-5` (main plus comment-only changes). Baseline B0 is `rts/docs/reports/ai_pd/baseline/README.md` (`aggressive_s5_m`, same games). Nothing was tuned: no bar, threshold, profile value, key or rule changed after any result. A failing bar is reported as measured.

## Bars (B0 value / C1 value, per cell; medians over the cell's slot-games, window 600 s)

Cells: skai cross 72, water cross 72, skai mirror 144, water mirror 144 slot-games.

| Bar | Pass when | skai cross | skai mirror | water cross | water mirror | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| PD-B1 `worker_fed_losses` | C1 <= floor(B0/2) if B0 >= 2, else <= B0 | 0 / 0 pass | 3 / 2 FAIL (limit 1) | 6 / 6 FAIL (limit 3) | 1 / 1 pass | FAIL (2 of 4 cells) |
| PD-B2 `worker_losses` | C1 <= floor(0.6 B0) if B0 >= 6, else <= B0+2 | 0 / 0 pass | 14.5 / 11 FAIL (limit 8) | 31 / 26 FAIL (limit 18) | 11 / 7 FAIL (limit 6) | FAIL (3 of 4) |
| PD-B3 `raid_unanswered_edge_seconds` | C1 <= floor(B0/2) if B0 >= 10, else <= B0+5 | 0.5 / 1 pass | 9 / 7 pass | 8 / 8.5 pass | 10 / 7 FAIL (limit 5) | FAIL (1 of 4) |
| PD-B4 `bank_mean` | C1 <= floor(0.6 B0) if B0 >= 300, else <= B0+50 | 1232 / 66.5 pass | 211 / 62 pass | 301.5 / 69.5 pass | 551.5 / 68 pass | PASS |
| PD-B5 producers x 1000 / income_60 at 300 s | C1 >= B0 | 3 / 4 pass | 3 / 4 pass | 3 / 5 pass | 3 / 4 pass | PASS |
| PD-B6 income 240-300 s | C1 >= 0.9 B0 | 1847.5 / 1815 pass | 1815 / 1715 pass | 1645 / 1317.5 FAIL (limit 1480.5) | 1767.5 / 1750 pass | FAIL (1 of 4) |
| PD-B7 clean commands | no rejection pair absent from the B0 traces | no rejection in any C1 trace | | | | PASS |

Overall: PD-B4, PD-B5 and PD-B7 pass; PD-B1, PD-B2, PD-B3 and PD-B6 fail in the cells shown.

PD-B7 detail: the C1 traces show "commands rejected, whole game=-" for all six players. The B0 traces (the `ai_m2` traces, same candidate `aggressive_s5_m`; no separate B0 traces were committed) show `tile_blocked` 1 in `water_vs_water`. The trace prints rejections by reason only, not by command kind; with zero rejections in C1 the bar holds whatever the kinds.

## Reported, not judged

Production samples, C1 (B0 in brackets): producers / capacity x 60 / income_60.

| cell | 180 s | 300 s | 450 s |
| --- | --- | --- | --- |
| skai cross | 4 / 53.5 (4 / 44.5) | 8 / 80 (6 / 61) | 19 / 144 (11 / 85) |
| skai mirror | 4 / 49 (4 / 43) | 8 / 79 (6 / 59) | 14 / 124 (10 / 89) |
| water cross | 4 / 70 (4 / 59) | 7 / 108.5 (5 / 70) | 10 / 159.5 (9 / 102) |
| water mirror | 4 / 71 (4 / 59) | 8 / 91 (6 / 69.5) | 14 / 140.5 (11 / 105) |

Raid seconds (C1, B0): skai cross 8 (10), skai mirror 84.5 (89.5), water cross 120 (127.5), water mirror 100 (98). Army supply at 300 s (C1, B0): skai cross 28.5 (26.5), skai mirror 20.5 (22), water cross 18 (19), water mirror 28 (26); at 450 s water cross 17.5 (21.5). Full sample table in `balance/balance.md`.

Faction matrix (information only, no win-rate target): Skai beat Water 72 of 72, Water beat Skai 0 of 72; mirrors: Skai seat 41/71, Water seat 29/72; one undecided game. Opening matrix in `balance/balance.md`. Trust checks as printed: `balance_use` FAIL (only the two `hq_last_stand` rows, as in `ai_m2`), `balance_predictor` FAIL (0.735, as `ai_m2`), `balance_unspent` FAIL, `balance_openings` PASS. Median end tick over the games' records: 10887.5 (B0 11788.5).

### Water against Skai (traced game and cell data)

The `balance.jsonl` records hold worker-loss counts, not their times, so timing comes only from the one traced `skai_vs_water` game (30 s lines; Water's `water.tender` is its worker). Fields closed: no metric records it; not available.

| | B0 trace (`ai_m2`) | C1 trace |
| --- | --- | --- |
| first worker loss (30 s line) | 120 s | 180 s (2 lost in the 150-180 s line) |
| worker losses up to 180 s / 180-360 s / after 360 s | 4 / 18 / 6 (28 total) | 2 / 8 / 30 (40 total) |
| game end | 455 s | 539 s |

One game, so the "median" first loss is that game's value. In C1 the losses move late: Water's worker count grows to 26 at 450 s (B0 trace: 10) and then collapses (22, 13, 5) while Skai's army overruns the base, 12 tenders lost in 480-510 s. Cell medians (C1 vs B0): `worker_losses` 26 vs 31, `raid_seconds` 120 vs 127.5, `worker_fed_losses` 6 vs 6, income 240-300 1317.5 vs 1645.

### Hypotheses H1 and H2 (from the code in `rts/src/Ai/AiBrainRaids.cs`; the traces do not print raid state, so no trace evidence)

- H1 confirmed by reading: `RaidHits` is keyed by drop-off and each hit with `tick >= stored.Tick` overwrites the stored position (lines 94 to 97), so the closed-field centre moves with each new hit; `NearRaid` tests fields against that one position, so a field a raider still stands on can reopen when the position moves more than `raid_field_radius_tiles` (8) away.
- H2 confirmed by reading: an entry expires when its tick is older than `raid_active_seconds` (10 s, `rts/data/ai/aggressive_s5_pd.json`); the expiry loop never checks whether enemy units still stand near the position, so fields reopen 10 s after the last hit even with raiders present.
- Neither is shown by a trace or metric to cause the losses; both are consistent with the Water-cross failures of PD-B1, PD-B2 and PD-B6.

## Commands

Run from the repository root after `dotnet build rts/RtsGame.sln -c Release`.

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5_pd --maps prototype_1v1 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4 --out rts/docs/reports/ai_pd/balance
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --trace --map prototype_1v1 --matchup <skai_vs_skai|skai_vs_water|water_vs_water> --profile-a aggressive_s5_pd --profile-b aggressive_s5_pd --out rts/docs/reports/ai_pd/trace_<matchup>.txt
```

Wall-clock: balance report 4508 s, exit code 0; traces 26 s, 71 s and 110 s (run while the balance run was in progress). Full suite not run for this report (no logic change). Files: `balance/balance.md`, `balance/balance.jsonl`, three trace files.

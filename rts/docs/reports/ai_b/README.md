<!-- rts/docs/reports/ai_b/README.md: results of specs/ai_budget_queues.md B.4.2 (P-006 package B, budget queues): the behaviour run of aggressive_s5_b against abl_s5b_no_b, the balance report of aggressive_s5_b, the bar table (BB-1 to BB-4) and why the pool did not run. Part of rts/docs/reports; files here: behaviour.md/.jsonl, balance.md/.jsonl and the stdout of both runs. -->
# P-006 package B: budget queues, behaviour bars

Built from `rts/docs/specs/ai_budget_queues.md`; no value of the spec was changed before or after a result. Base seeds, maps and matchups are the harness defaults (`ai_eval.json`): `prototype_1v1`, `skai_vs_skai`, `skai_vs_water`, `water_vs_water`.
Result: **BB-2 and BB-3 fail, so the pool (B.4.3) was not run** and the candidate stays off (B.4.3 "Off: any behaviour bar fails").

## Commands and wall times (4 cores, `-c Release --jobs 4`)

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a abl_s5b_no_b --profile-b aggressive_s5_b --jobs 4 --out rts/build/harness/ai_b_behaviour
    elapsed 1635.7 s (real 27m16s), 72 slot-games per profile and check (24 paired games in each of 3 cells), spot check 2 games, 0 mismatches
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --balance --profile aggressive_s5_b --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4 --out rts/build/harness/ai_b_balance
    elapsed 2929.7 s (real 48m50s), 216 games, 216 distinct decided
dotnet test rts/RtsGame.sln -c Release      (once, before the runs, on commit 1c42c5d)
    real 30m38s: Rts.Net.Tests 33 of 33; Rts.Replay.Tests 27 of 27; Rts.Sim.Tests 1014 passed, 3 failed of 1017 (29m20s)
```

The 3 failures were old tests (`DProfiles_SwitchesAndValidation`, `L1_ShippedProfilesParse`, `R6_NoVarietyForOldProfiles`) that loop over every shipped profile file and expect the old values for any profile not on a list of known ones; the two new profiles had to be added to those lists (commit a7b7326). The three were rerun alone and pass; the full suite was not run a second time.

## Every line as printed: `--behaviour`

```
BEHAVIOUR static_defence_share profile=aggressive_s5_b games=72 held=72 violated=0 unjudged=0 metric_median=0.039 metric_max=0.076 bar=0.150 PASS
BEHAVIOUR static_defence_share profile=abl_s5b_no_b games=72 held=72 violated=0 unjudged=0 metric_median=0.026 metric_max=0.048 bar=0.150 PASS
BEHAVIOUR hq_move_new_income profile=aggressive_s5_b games=72 held=62 violated=7 unjudged=3 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=16 violating_moves=7 unjudged_moves=3
BEHAVIOUR hq_move_new_income profile=abl_s5b_no_b games=72 held=18 violated=36 unjudged=18 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=57 violating_moves=36 unjudged_moves=19
BEHAVIOUR workers_at profile=aggressive_s5_b games=72 held=72 violated=0 unjudged=0 metric_median=21.000 metric_max=24.000 bar=0.000 PASS
BEHAVIOUR workers_at profile=abl_s5b_no_b games=72 held=72 violated=0 unjudged=0 metric_median=24.000 metric_max=26.000 bar=0.000 PASS
BEHAVIOUR first_drop_off profile=aggressive_s5_b games=72 held=72 violated=0 unjudged=0 metric_median=82.000 metric_max=119.000 bar=7200.000 PASS
BEHAVIOUR first_drop_off profile=abl_s5b_no_b games=72 held=72 violated=0 unjudged=0 metric_median=102.000 metric_max=114.000 bar=7200.000 PASS
BEHAVIOUR_SUMMARY candidate=aggressive_s5_b passed=3 of=4
```

`workers_at` (300 s) and `first_drop_off` carry bars that always pass (B.4.1); BB-3 and BB-4 compare the printed medians. `hq_move_new_income` is information only (the spec's B.4.2).

## Every line as printed: `--balance`

```
CHECK balance_use FAIL 13 of 53 items below 0.100: skai/skai.wind_baffle 0.000, skai/skai.wind_gate 0.000, water/water.coral_gate 0.000, water/water.coral_wall 0.000, water/water.tidecaller 0.000, skai/downdraft 0.000, water/wave 0.000, skai/wall 0.000, water/wall 0.000, skai/gate 0.000, water/gate ...
CHECK balance_predictor FAIL accuracy=0.735 correct=562 decided=765 bar=0.850
CHECK balance_unspent FAIL over bars common=400 rare=150: skai@300s common=320 rare=165, skai@600s common=275 rare=235, water@600s common=80 rare=310, water@900s common=90 rare=950
CHECK balance_openings PASS openings judged=8 all within 0.100 to 0.900 of distinct mirror games (at least 10)
BALANCE profile=aggressive_s5_b games=216 decided=216 distinct=216 checks_passed=1 of=4
```

(The first line is cut at 300 characters here; `balance_stdout.txt` has it in full. `balance_use` and `balance_predictor` fail the same way for `aggressive_s5` in `reports/balance_first/`; they do not depend on this package.)

Unspent table of the candidate against `balance_first` (`aggressive_s5`, the same bars; lower median over the slot-games still running, n in brackets):

| faction | time | `aggressive_s5` common / rare | `aggressive_s5_b` common / rare |
| --- | ---: | --- | --- |
| skai | 300 s | 385 / 205 (216) | 320 / 165 (216) |
| skai | 600 s | 125 / 285 (59) | 275 / 235 (100) |
| water | 300 s | 230 / 220 (216) | 285 / 80 (216) |
| water | 600 s | 50 / 475 (117) | 80 / 310 (114) |
| water | 900 s | 75 / 1020 (16) | 90 / 950 (28) |

## Bar table

| Bar | Measured | Passes when | Result |
| --- | --- | --- | --- |
| BB-1 static defence share | `aggressive_s5_b` 72 held, 0 violated, median 0.039, max 0.076 (ablation median 0.026) | PASS (bar 0.150, 0.05 violations) | PASS |
| BB-2 unspent resources | skai@300 s rare 165 (bar 150), skai@600 s rare 235, water@600 s rare 310, water@900 s rare 950; common within 400 everywhere | `CHECK balance_unspent` PASS | **FAIL** |
| BB-3 workers at 300 s | candidate median 21, ablation median 24; needs 24 - 1 = 23 or more | candidate >= ablation - 1 | **FAIL** (2 below) |
| BB-4 first depot | candidate median 82 s, ablation 102 s; allowed up to 102 + 20 = 122 s | candidate <= ablation + 20 s | PASS (20 s earlier) |

Pool (B.4.3, stage 1, 552 games): **not run**, because two bars failed. Stage 2 and the switch-on of `aggressive_s5` were not touched.

## What the runs show (information, not a re-judgement)

- **Rare bank.** Against the ablation (`aggressive_s5`'s own play) the rare bank at 300 s is lower: skai 205 to 165, water 220 to 80 (the Water bank is under the bar at 300 s). At 600 s and 900 s it is still above 150: skai 235, water 310 and 950 (n = 28, the long games). Rare spent per game (median slot-game, `summary.spent_rare`): skai 1130 to 1310, water 730 to 855. So the rare-surplus rule moved rare into units but not enough to meet the bar in the long games.
- **Spending by class** (median share of common spent; `aggressive_s5` from `balance_first`, candidate from this run): skai structure 0.148 to 0.158, static defence 0.029 to 0.037, army 0.668 to 0.661; water structure 0.128 to 0.127, static defence 0.028 to 0.042, army 0.687 to 0.689. The harness records spending by class, not by queue; the per-queue totals (`AiMemory.BudgetSpent`) are not in the game record, so a per-queue table was not produced.
- **Workers.** At 300 s the candidate has 21 workers against 24 (water 20 against 24, skai 21 against 25 in the behaviour games), at 600 s 23 against 33 for skai. Traces of the first minute (`--trace`, aggressive_s5_b, skai_vs_skai) show the cause: the economy and expansion queues hold money in proportion to their weights (120 each), so while money is scarce a worker's 50 is funded in slices (a 5 to 10 common share per half second next to a 100 depot want) and the worker waits about 11 s in the trace (ticks 250 to 470); and a worker is bought one think (0.5 s) after its want is recorded. This is the specified split working as written, not a code deviation.
- **Held money.** A production or tech head that costs rare while no rare arrives (a Reef Workshop, 150 common and 50 rare, wanted by `production` and by `tech` at once) keeps 150 common in each of two accounts while the rare never comes (seen in the Water trace of `skai_vs_water` seed 20261026: both accounts at 150 from about tick 2300, bank 845 at tick 4700). Rule R reserved the same cost, but only once.
- **Seat.** The mirror cells show slot 0 winning more often than before: skai mirror 48 of 72 (interval 55.2 to 76.5) against 35 of 72 for `aggressive_s5`, water mirror 51 of 72 (59.5 to 80.1) against 30 of 72. The cross-faction cells are unchanged (Skai 72 of 72, `balance_first` the same). The candidate's first think of a game and the spending order inside a think are the same for both slots; the slots think at different ticks, so a one-think lag in funding may favour the slot that thinks first. Not investigated.
- **HQ moves.** `hq_move_new_income` violating moves: 7 of 16 for the candidate against 36 of 57 for the ablation (information only).
- **Unit and mechanic use** of the candidate against `balance_first` (`aggressive_s5`), share of slot-games: Thunderhead 0.931 against 0.838, Storm Spire 0.986 against 1.000, Ink Lobber 0.856 against 0.731, Leviathan 0.745 against 0.819, Reef Workshop 0.977 against 0.995; the `rare` mechanic 1.000 and 0.991 against 1.000 and 1.000.

## Checks that the off path is unchanged

- `--trace` of `aggressive_s5` (prototype_1v1, skai_vs_water, seed 20261026, 600 s cap): the trace file and the result line (state hash `79b49601e07be618`, play signature `ca372a76ed8ac6da`, content digest `7cb328f35c617679`) are byte-identical before and after the change (`cmp`).
- `--behaviour --profile-a aggressive_s5 --profile-b aggressive_s5 --games 4 --maps prototype_1v1 --matchups skai_vs_water`: stdout and `behaviour.md` differ only by the four new `BEHAVIOUR workers_at` and `first_drop_off` lines and the summary count (1 of 2 to 3 of 4); `behaviour.jsonl` is identical once the two new fields (`workers`, `drop_off_ticks`) are removed from each record.
- `abl_s5b_no_b` and `aggressive_s5` give equal state hash, play signature and end tick in one 600 s game per matchup (test BQ-1).

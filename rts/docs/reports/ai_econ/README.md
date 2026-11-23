<!-- rts/docs/reports/ai_econ/README.md: results of specs/ai_economy_calc.md section 6 (P-006 package E-M, economy by calculation): the paired behaviour run of aggressive_s5_e against aggressive_s5 on prototype_1v1, the bar table EB-1 to EB-6 with values and verdicts, the E-OPT numbers (decision gap, total gap, model error), the per-opening lines, the reported-not-judged values, the commands and wall times. Part of rts/docs/reports; files here: behaviour.md/.jsonl, behaviour_stdout.txt, trace_<matchup>.txt. -->
# P-006 package E-M: economy by calculation, behaviour bars

Built from `rts/docs/specs/ai_economy_calc.md`; no bar, threshold, default, weight or key was changed before or after the result, and no unit, combat, economy or map number changed. Measured commit `7701303` (branch `review/ai-econ-opt`, on top of `cb98bc9`), Release build, 4 cores. Harness defaults of `ai_eval.json` (base seed 20261117, three matchups, 24 paired games per cell, game cap 2400 s), map `prototype_1v1` only: 72 slot-games per profile and check. Baseline = `aggressive_s5` (profile-a), candidate = `aggressive_s5_e` (profile-b).

**Result: EB-4 fails; EB-1, EB-2, EB-3, EB-5 and EB-6 pass.** By section 6 ("Any bar fails") the switch stays off in every profile except `aggressive_s5_e`. EB-4 is a miss, not a tie: the candidate's decision gap is 5 permille higher than the baseline's (96 against 91), the wrong way for a strict "less than". No value was tuned toward it.

## Bar table

| Bar | Baseline `aggressive_s5` | Candidate `aggressive_s5_e` | Pass formula | Verdict |
| --- | --- | --- | --- | --- |
| EB-1 workers at 300 s (`workers_at`, median) | 24 | 24 | candidate >= baseline - 1, i.e. 24 >= 23 | PASS |
| EB-2 first depot (`first_drop_off`, median seconds) | 112 | 112 | candidate <= baseline + 20, i.e. 112 <= 132 | PASS |
| EB-3 income 0 to 300 s (`income_by`, median, common + rare) | 6910 | 6875 | candidate >= floor(0.98 x baseline) = floor(6771.8) = 6771 | PASS |
| EB-4 decision gap at 300 s (`econ_gap`, median permille) | 91 | 96 | candidate < baseline (strictly): 96 < 91 is false | **FAIL** |
| EB-5 every opening keeps its economy (`income_by` per (faction, opening), at least 5 judged games in both profiles) | see the table below | see the table below | for every such opening candidate >= floor(0.90 x baseline) | PASS (7 of 7 openings) |
| EB-6 clean run | 0 mismatches (determinism spot check, 2 games) | | 0 mismatches and every pin of section 0 holds on the measured commit | PASS |

EB-6 pins: the full suite on `7701303` passed (1232 Sim + 33 Net + 30 Replay), which includes the pin tests: smoke `207be73eaa460b3c`, economy_smoke `98b2e00d9babe167`, `aggressive_s5` trace `837f8e7118a9c9de` end tick 9505, `aggressive_s5` and `aggressive_s3` pinned traces (`c04bdcc0d03b3910`, end tick 9223), `V2ReferenceHash`, `BlindTraceHash`; none moved, nothing was re-pinned.

### EB-5 per opening (`BEHAVIOUR_BY_OPENING check=income_by`, median income 0 to 300 s)

| Faction, opening | Judged games (each profile) | Baseline | Candidate | floor(0.90 x baseline) | Verdict |
| --- | --- | --- | --- | --- | --- |
| skai, skai_air | 11 | 7190 | 7245 | 6471 | pass |
| skai, skai_fast_expand | 5 | 6950 | 6990 | 6255 | pass |
| skai, skai_standard | 17 | 7095 | 7035 | 6385 | pass |
| skai, skai_windrunner_pressure | 3 | 7035 | 7180 | not judged (fewer than 5 games) | excluded |
| water, water_fast_expand | 8 | 6785 | 6740 | 6106 | pass |
| water, water_grasper_pressure | 5 | 6510 | 6555 | 5859 | pass |
| water, water_leviathan_tech | 12 | 6615 | 6285 | 5953 | pass |
| water, water_standard | 11 | 6325 | 6320 | 5692 | pass |

Both profiles draw the same openings (no random draw was added), so the judged counts are equal. The weakest margin is `water_leviathan_tech`: candidate 6285 against 6615 (minus 5.0 percent), inside the 10 percent allowance.

## E-OPT numbers (model at 300 s; all permille, floor; 72 slot-games each, none unjudged)

| Quantity | Baseline `aggressive_s5` | Candidate `aggressive_s5_e` |
| --- | --- | --- |
| Decision gap, 1000 x (OPT - MOD) / OPT: median (the bar) | 91 | 96 |
| Decision gap: mean | 101.3 | 104.9 |
| Decision gap: largest | 209 | 229 |
| Total gap, 1000 x (OPT - REAL) / OPT: mean (sum 6031 and 5904 over 72) | 83.8 | 82.0 |
| Model error, 1000 x (MOD - REAL) / MOD: mean, signed (sum -1400 and -1835 over 72) | -19.4 | -25.5 |
| Model error: mean of the absolute value | 24.9 | 30.3 |
| Model error: median of the absolute value | 26 | 34 |

Per-opening decision gap (`BEHAVIOUR_BY_OPENING check=econ_gap`, median), baseline then candidate: skai_air 74, 77; skai_fast_expand 60, 62; skai_standard 77, 96; skai_windrunner_pressure 42, 29 (3 games); water_fast_expand 95, 92; water_grasper_pressure 65, 64; water_leviathan_tech 161, 176; water_standard 144, 147. The candidate is higher in 5 of the 8 and lower in 3; the largest single move is `skai_standard` (+19).

### How large the model error is against the decision gap

The model error is not small against what EB-4 measures. Its mean absolute value (25 permille baseline, 30 candidate) is about a quarter to three tenths of the mean decision gap (101, 105), and a single game's model error (median 26 and 34 in absolute value) is about as large as the whole difference between the two medians the bar compares (5 permille). The model error is also systematically negative (the model's income for the AI's schedule is 2 to 3 percent below what the AI really deposited), and it differs between the profiles by 6 permille (mean), more than the 5 permille EB-4 difference. So the EB-4 verdict is inside the model's noise: the data do not show that the candidate is further from the optimum, they show that the check cannot tell the two profiles apart at this resolution, and the strict "less than" fails on a difference the model cannot resolve. Part of the bias is the replay's definition (section 3.2): MOD reads the workers of the bucket before, so with 30-s buckets and a fast-growing worker count it undercounts every game; in the hand-built H-E5 map that lag alone cost 2.5 percent at 30-s buckets (the test therefore uses 5-s buckets to hold the spec's 1 percent). The decision gap of about 10 percent for both profiles includes that lag and the coarse grid (steps of 2).

## Reported, not judged

| Value | Baseline | Candidate |
| --- | --- | --- |
| Median common bank at 300 s (snapshot, 72 each) | 355 | 310 |
| Median rare bank at 300 s | 205 | 140 |
| Median second of depot 2 (`drop_off_ticks[1]`, 72 games each) | 181 | 187 |
| Median second of depot 3 (72 games; 71 for the candidate) | 261 | 272 |
| Workers and income at 600 s | not measured (the checks read 300 s; `ai_eval.json` has bank snapshots at 300, 600, 900 but `workers_at` and `income_by` are set to 300) | |

Thinks where `Fired` was true (rule ED ordered a depot by calculation alone), from a separate in-process replay of the three trace games (seed 20261026, `aggressive_s5_e` in both seats, 600 s), not from the trace files, because the `econ=` column carries only `w`, `n` and `l`. Format: matchup, player, second, wanted workers, `DepotNet`, lead (ticks).

```
skai_vs_skai    P0 162 s w19 n975 l650 | P1 169 s w19 n975 l650 | P1 239 s w24 n850 l853 | P1 288 s w28 n795 l957 | P0 292 s w22 n850 l853 | P0 355 s w25 n825 l993
skai_vs_water   P0 146 s w19 n975 l650 | P1 208 s w19 n905 l703 | P0 214 s w25 n850 l853 | P0 264 s w29 n795 l957
water_vs_water  P1 161 s w19 n905 l703 | P0 182 s w19 n905 l703 | P0 276 s w23 n765 l957 | P1 281 s w22 n765 l957 | P1 303 s w27 n690 l1086 | P0 343 s w25 n740 l1131
```

Every fired think has `DepotNet` above 0 (690 to 975 resource units). The earliest fired think is at 146 s; the first depot's median completion is 112 s (the clock gate of the openings), and whether each fired depot was a first or later depot was not separated. The trace files carry the `econ=` column on every player sample line (42, 30 and 42 lines).

Other checks of the same run, printed and not bars: `static_defence_share` passes for both (median 0.024 and 0.025). `hq_move_new_income` fails for both profiles (slot-games violated out of those judged: baseline 43 of 65, candidate 16 of 60); it is an older check, not part of this spec, and is why the command exits 2 and `BEHAVIOUR_SUMMARY` says `passed=5 of=6`. The win columns are not read.

## Commands and wall times (4 cores, Release)

```
dotnet test rts/RtsGame.sln -c Release       (on 7701303, before the run)
    real 50m13s: Rts.Net.Tests 33 of 33; Rts.Replay.Tests 30 of 30; Rts.Sim.Tests 1232 of 1232 (49m53s)
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_e --maps prototype_1v1 --jobs 4 --out rts/build/harness/ai_econ_behaviour
    elapsed 2676.9 s (44m40s real), 72 slot-games per profile and check (24 paired games in each of 3 cells), spot check 2 games, 0 mismatches, exit code 2 (hq_move_new_income fails for both profiles)
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --trace --map prototype_1v1 --matchup <skai_vs_skai|skai_vs_water|water_vs_water> --seed 20261026 --max-game-seconds 600 --profile-a aggressive_s5_e --profile-b aggressive_s5_e --out rts/docs/reports/ai_econ/trace_<matchup>.txt
    22 s, 44 s and 99 s
```

Search time of E-OPT: one slot-game (26180 schedules of 300 steps, evaluated once per slot-game and cached) took 0.26 to 0.45 s in Release on this machine, 8 sampled slot-games; far inside the spec's 5 s budget, so the grid was not touched. The whole econ_gap evaluation of the 144 slot-games is below 2 minutes of the 44.

The three trace result lines (`end_tick`, `state_hash`): skai_vs_skai 11999 `b1563b3cb9d4cf82`; skai_vs_water 8979 `3f48239dc33183a2`; water_vs_water 11999 `a8533541b6a9a6de`.

## Reading (section 6)

One bar fails (EB-4), so `use_economy_calc` stays off in every profile except `aggressive_s5_e` and the `aggressive_s5` pin does not move. The developer's question is whether EB-4 is the right test: on this run the economy itself did not get worse (EB-1 to EB-3 and EB-5 hold, with income 0.5 percent lower and the bank 13 percent lower at 300 s), and the decision-gap measure cannot separate the two profiles. Changing the horizon, the model or the bar is a new revision with its bars restated before the next run.

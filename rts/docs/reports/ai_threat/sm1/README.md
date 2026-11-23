<!-- rts/docs/reports/ai_threat/sm1/README.md -->
<!-- Run SM-1 of rts/docs/specs/ai_threat_map.md section 10: the paired behaviour measurement of rule TS (IsUnsafe reads the threat map) on prototype_1v1, candidate aggressive_s5_s (map and rule TS on, rule TH absent) against aggressive_s5, measured at commit de0de0b. Bars TS-B1 to TS-B4 judged by the spec's formula; nothing tuned. Part of rts/docs/reports/ai_threat. -->
# SM-1: rule TS (safety by the threat map) against `aggressive_s5`

Measured 23 Nov 2026 on commit `de0de0b` (branch `review/ai-threat-rules`, S-TS built, no rule TH). Release build of that commit (a clean `git archive` copy, so the work in progress on rule TH did not enter the build). No bar, margin, key or default was changed after the result.

## Result in one line

**TS-B1 (the primary bar, fewer depots lost young) is NO_MEASURABLE_EFFECT, so the bar fails.** The three guards pass. By spec section 10.4 `use_threat_safety` stays false in every profile, including the final `aggressive_s5_s`.

## Commands and times

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_s --maps prototype_1v1 --games 48 --jobs 4 --out rts/build/harness/sm1
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --trace --threat-grid --map prototype_1v1 --matchup <skai_vs_skai|skai_vs_water|water_vs_water> --seed 20261026 --max-game-seconds 600 --profile-a aggressive_s5_s --profile-b aggressive_s5_s --out rts/build/harness/sm1/trace_<matchup>.txt
dotnet test rts/RtsGame.sln -c Release        (the suite on the measured commit, before the run)
```

(`--no-build` was added to both `dotnet run` lines after a Release build of the same commit, as in the base-rate run.)

| Step | Started (UTC) | Wall clock |
| --- | --- | --- |
| Suite on `de0de0b` | 2026-11-23 20:11:39 | **55 min 3 s** (1264 Sim + 33 Net + 30 Replay, all passed; the Sim project alone 54 min 8 s) |
| Behaviour run, 144 games | 2026-11-23 21:07:18 | **56 min 15 s** (3374 s by the harness) |
| Traces | 2026-11-23 22:04 | 22 s (`skai_vs_skai`), 39 s (`skai_vs_water`), 89 s (`water_vs_water`) |

Timing note: for part of the behaviour run (roughly the first 40 minutes) the machine was shared with builds and test runs of the next package (S-TH, a few minutes of CPU in all), so the wall clock is a little longer than a run alone on 4 cores. Results do not depend on timing. The `--behaviour` run header now prints the pairing kind (new line in `behaviour.md` and on stderr): `skai:full,water:full`, as the spec requires. Base seed and matchups of `ai_eval.json` (seed 20261117; `skai_vs_skai`, `skai_vs_water`, `water_vs_water`), 48 games per cell: 144 games, 72 pairs, 288 slot-games. Game cap 2400 s, summary window 600 s. Determinism spot check: 2 games re-run, **0 mismatches**. The summary line is `passed=8 of=9`: the one failing check is the older `hq_move_new_income` (information, not a package S bar; it also fails for the baseline). The exit code was not recorded.

Files here: `behaviour.md` (the harness output, as written), `threat_300s_<matchup>.txt` (the `thr=` column and both `--threat-grid` layers of each player at 300 s of the traced games).

## Bars

Every bar is the paired sign test of spec 7.3 on pair sums (72 pairs, alpha 0.05). D is the number of decisive pairs (better plus worse). `mde_permille` is the stated power: the share of decisive pairs in which the candidate must be better for the test to see it with power 0.80 (a value above 1000 means no result could pass).

| Bar | Check (direction) | Baseline `aggressive_s5` | Candidate `aggressive_s5_s` | Better pairs | Worse pairs | Ties | D | p | `mde_permille` | Verdict | Bar |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TS-B1 Fewer depots lost young (primary) | `depot_losses_young` (lower) | median 0, max 2 per slot-game; 19 young depots lost in all | median 0, max 3 per slot-game; 32 young depots lost in all | 4 | 11 | 57 | 15 | 0.1185 | 862 | NO_MEASURABLE_EFFECT | **FAIL** (needs BETTER) |
| TS-B2 Expansion not delayed (guard) | `first_drop_off` (lower) | median 109 s, max 120 s | median 109 s, max 120 s | 0 | 0 | 72 | 0 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | PASS (not WORSE) |
| TS-B3 Economy kept (guard) | `income_by` 300 s (higher) | median 6900, max 7435 | median 6910, max 7365 | 16 | 9 | 47 | 25 | 0.2295 | 781 | NO_MEASURABLE_EFFECT | PASS (not WORSE) |
| TS-B4 Clean run (guard) | spot check and suite | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a | 0 mismatches in 2 re-run games; suite 1264 + 33 + 30 passed; every pin held | PASS |

Plainly, for the main bar: **TS-B1 shows "no measurable effect".** The 15 decisive pairs lean the wrong way (4 pairs with fewer young depot losses for the candidate, 11 with more, p 0.1185), which is not significant, so the verdict is not "significantly worse" either. The bar needed at least 12 better of 15 (see `base_rates/README.md`). Pairs in which both sums were 0: 47 of 72; pairs by matchup (candidate more, fewer, equal young losses): `skai_vs_skai` 5, 2, 17; `skai_vs_water` 2, 2, 20; `water_vs_water` 4, 0, 20.

Power beside each guard (what a guard can see, section 10.3):
- **TS-B2 `first_drop_off`:** D is 0 (all 72 pairs tie: the pair sums of the first depot's second were equal in every pair, so the opening depot was not moved by rule TS). With D below 6 no result can be seen; the guard could not have caught a regression, and none is visible in the data either (median 109 s and max 120 s for both).
- **TS-B3 `income_by` 300 s:** D is 25 and `mde_permille` 781: the guard sees a regression only if the candidate was worse in about 78 percent of the decisive pairs. Here it was better in 16 and worse in 9, so no regression of that size exists; a smaller one could pass unseen.
- **TS-B4:** the spot check re-runs 2 games in process, so it is a spot check, not a proof; the suite is what holds the pins.

Pins (section 0), all held with no re-pinning, by the tests of the full suite on `de0de0b`: smoke `207be73eaa460b3c`, economy_smoke `98b2e00d9babe167`, `aggressive_s5` trace end tick 9505 hash `837f8e7118a9c9de`, `aggressive_s3` `c04bdcc0d03b3910`, `V2ReferenceHash`, `BlindTraceHash`, the wall-distribution pin.

## Verdict and what follows (section 10.4)

The primary bar did not pass (NO_MEASURABLE_EFFECT), so by the spec `use_threat_safety` stays false in every profile. SM-2 (package S-TH) runs with rule TS off in the candidate, as the spec says; this report does not change the candidate's switches. Nothing in this report was used to retune rule TS, `threat_unsafe_margin` (150), the map keys or any bar.

## Reported, not judged (section 10.5)

Per slot-game, both profiles over 144 slot-games each, from `behaviour.jsonl` (not kept here) and the check lines:

| Quantity | `aggressive_s5` | `aggressive_s5_s` (rule TS on) |
| --- | --- | --- |
| Medians of the checks (as printed): `static_defence_share` | 0.025 | 0.027 |
| `hq_move_new_income` (violating moves of all moves; information only) | 64 of 85 | 83 of 106 |
| `workers_at` 300 s (workers) | median 25, max 27 | median 24, max 27 |
| `first_drop_off` (s) | median 109 | median 109 |
| `income_by` 300 s | median 6900 | median 6910 |
| `econ_gap` | median 88 | median 91 |
| `depot_losses_young` | median 0 | median 0 |
| `harass_trade` | median 0 | median 0 |
| `harass_squads` | median 2 | median 2 |
| Depots placed per slot-game | mean 4.92, median 5 | mean 5.58, median 5 |
| Second depot, median second | 179.5 | 177.4 |
| Third depot, median second | 261.5 | 268.8 |

The candidate placed more depots (5.58 against 4.92 per slot-game) and lost more of them young (32 against 19); rule TS sends depots to fields it rates safe, and the data here show no gain from it.

`thr=` at 300 s of the traced games (seed 20261026, 600 s, `aggressive_s5_s` in both seats; `s` sightings, `e` enemy strength and `o` own strength at the own HQ cell, `m` largest enemy cell): `skai_vs_skai` P0 `s9/e0/o58/m376@7,16`, P1 `s12/e0/o58/m857@7,15`; `skai_vs_water` P0 `s1/e0/o1094/m175@15,19`, P1 `s10/e49/o66/m620@13,18`; `water_vs_water` P0 `s8/e0/o283/m307@9,9`, P1 `s4/e0/o1113/m213@8,8`. The full grids are in the `threat_300s_*.txt` files. The win and loss columns were not read.

Note for `depot_losses_young`: a depot that dies with no enemy killer (no `UnitKilledEvent` with an enemy `KillerOwner`) counts as alive, so it never counts as lost young.

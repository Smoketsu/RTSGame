<!-- rts/docs/reports/ai_threat/sm2/README.md -->
<!-- Run SM-2 of rts/docs/specs/ai_threat_map.md section 10: the paired behaviour measurement of rule TH (harass target and route by the threat map) on prototype_1v1, candidate aggressive_s5_s (map and rule TH on, rule TS off) against aggressive_s5, measured at commit 4598381 (code of d5fca50). Bars TH-B1 to TH-B4 judged by the spec's formula; nothing tuned; ends with the candidate's final switches. Part of rts/docs/reports/ai_threat. -->
# SM-2: rule TH (harass target and route by the threat map) against `aggressive_s5`

Measured 23 Nov 2026 on commit `4598381` (branch `review/ai-threat-rules`; its code is that of `d5fca50`, S-TH built, and the commit adds only the SM-1 report). Release build of a clean `git archive` copy of that commit. In the measured candidate `use_threat_map` and `use_threat_harass` are true and `use_threat_safety` is false. No bar, margin, key or default was changed after the result.

## Result in one line

**TH-B1 (the primary bar, better harass trades) is NO_MEASURABLE_EFFECT, so it fails, and the guard TH-B3 (economy kept, `income_by` at 300 s) is WORSE, so it fails too.** TH-B2 and TH-B4 pass. By spec section 10.4 `use_threat_harass` stays false in every profile, including the final `aggressive_s5_s`.

## Commands and times

```
dotnet test rts/RtsGame.sln -c Release        (the suite on the measured code, before the run)
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_s --maps prototype_1v1 --games 48 --jobs 4 --out rts/build/harness/sm2
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --trace --threat-grid --map prototype_1v1 --matchup <skai_vs_skai|skai_vs_water|water_vs_water> --seed 20261026 --max-game-seconds 600 --profile-a aggressive_s5_s --profile-b aggressive_s5_s --out rts/build/harness/sm2/trace_<matchup>.txt
```

(`--no-build` was added to both `dotnet run` lines after a Release build of the same commit.)

| Step | Started (UTC) | Wall clock |
| --- | --- | --- |
| Suite on the measured code | 2026-11-23 22:08:03 | **54 min 56 s** (1273 Sim + 33 Net + 30 Replay, all passed; the Sim project alone 54 min 8 s) |
| Behaviour run, 144 games | 2026-11-23 23:03:24 | **54 min 36 s** (alone on 4 cores) |
| Traces | 2026-11-23 23:59 | 19 s (`skai_vs_skai`), 60 s (`skai_vs_water`), 74 s (`water_vs_water`) |

The run header prints the pairing kind `skai:full,water:full`. Base seed and matchups of `ai_eval.json` (seed 20261117; `skai_vs_skai`, `skai_vs_water`, `water_vs_water`), 48 games per cell: 144 games, 72 pairs, 288 slot-games. Game cap 2400 s, summary window 600 s. Determinism spot check: 2 games re-run, **0 mismatches**. The summary line is `passed=8 of=9`; the failing check is the older `hq_move_new_income` (information, also failing for the baseline). The exit code was not recorded.

Files here: `behaviour.md` (the harness output, as written), `threat_300s_<matchup>.txt` (the `thr=` column and both `--threat-grid` layers of each player at 300 s of the traced games).

## Bars

Every bar is the paired sign test of spec 7.3 on pair sums (72 pairs, alpha 0.05). D is the number of decisive pairs (better plus worse). `mde_permille` is the stated power: the share of decisive pairs in which the candidate must be better for the test to see it with power 0.80.

| Bar | Check (direction) | Baseline `aggressive_s5` | Candidate `aggressive_s5_s` | Better pairs | Worse pairs | Ties | D | p | `mde_permille` | Verdict | Bar |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TH-B1 Better harass trades (primary) | `harass_trade` (higher) | median 0, max 790 per slot-game; 15310 summed trade | median 0, max 850; 13010 summed trade | 20 | 22 | 30 | 42 | 0.8776 | 717 | NO_MEASURABLE_EFFECT | **FAIL** (needs BETTER) |
| TH-B2 Harass kept, variety (guard) | `harass_squads` (higher) | median 2, max 6; 334 squads in all | median 2, max 6; 330 squads in all | 10 | 19 | 43 | 29 | 0.1360 | 761 | NO_MEASURABLE_EFFECT | PASS (not WORSE) |
| TH-B3 Economy kept (guard) | `income_by` 300 s (higher) | median 6890, max 7435 | median 6875, max 7425 | 10 | 23 | 39 | 33 | 0.0351 | 744 | **WORSE** | **FAIL** (WORSE) |
| TH-B4 Clean run (guard) | spot check and suite | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a | 0 mismatches in 2 re-run games; suite 1273 + 33 + 30 passed; every pin held | PASS |

Plainly, for the main bar: **TH-B1 shows "no measurable effect".** Of the 42 decisive pairs the candidate had the better trade in 20 and the worse in 22 (p 0.8776); the candidate's summed trade over all pairs is lower (13010 against 15310), but the sign test sees no difference. The bar needed at least 28 better of the 42 decisive pairs. By matchup (candidate higher, lower, equal trade): `skai_vs_skai` 12, 11, 1; `skai_vs_water` 8, 11, 5; `water_vs_water` 0, 0, 24 (no harass in Water mirrors, as in the base rates).

For the economy guard: **TH-B3 shows "significantly worse".** The candidate's pair sum of `income_by` at 300 s was lower in 23 pairs and higher in 10 (p 0.0351; 23 is the smallest worse count that is significant with 33 decisive pairs). The stated power says the test sees a regression with power 0.80 when the candidate is worse in about 744 permille of the decisive pairs; the observed 697 permille (23 of 33) is below that, so the result is at the edge of what the test can see, and it is one of the six paired tests of this run (the spec applies alpha 0.05 to each and has no correction for several tests). The medians differ by 15 (6875 against 6890). No reason for the lower income was investigated, because nothing is tuned toward a bar.

Power beside each guard (section 10.3):
- **TH-B2 `harass_squads`:** D 29, `mde_permille` 761: it sees the candidate harassing less only if the candidate was worse in about 76 percent of the decisive pairs or more. The result (10 better, 19 worse, 655 permille worse) is not significant but leans toward fewer squads than the baseline; a smaller regression can pass unseen. By matchup (higher, lower, equal): `skai_vs_skai` 7, 9, 8; `skai_vs_water` 3, 10, 11; `water_vs_water` 0, 0, 24. Squads in all: 330 against 334.
- **TH-B3 `income_by`:** as above, and this guard did see a regression.
- **TH-B4:** the spot check re-runs 2 games in process, so it is a spot check, not a proof; the suite is what holds the pins.

Pins (section 0), all held with no re-pinning, by the tests of the full suite on the measured code: smoke `207be73eaa460b3c`, economy_smoke `98b2e00d9babe167`, `aggressive_s5` trace end tick 9505 hash `837f8e7118a9c9de`, `aggressive_s3` `c04bdcc0d03b3910`, `V2ReferenceHash`, `BlindTraceHash`, the wall-distribution pin.

## Verdict and the candidate's final switches (section 10.4)

A primary bar with NO_MEASURABLE_EFFECT and a guard WORSE: `use_threat_harass` stays **false** in every profile. Together with SM-1 (`use_threat_safety` false), the final `aggressive_s5_s` has `use_threat_map` **true**, `use_threat_safety` **false**, `use_threat_harass` **false**: the passive map only, which plays exactly as `aggressive_s5` (test TM-13). The only data edit after the run is that profile's `use_threat_harass` value and its header and `$doc` text; no key value, bar, direction or window changed. Whether rule TS or TH is ever switched on in `aggressive_s5` is the developer's decision (question 10 of the spec); neither measurement supports it.

## Reported, not judged (section 10.5)

Per slot-game over 144 slot-games each, from the check lines and `behaviour.jsonl` (not kept here):

| Quantity | `aggressive_s5` | `aggressive_s5_s` (rule TH on) |
| --- | --- | --- |
| Medians of the checks (as printed): `static_defence_share` | 0.025 | 0.026 |
| `hq_move_new_income` (violating moves of all moves; information only) | 71 of 89 | 66 of 94 |
| `workers_at` 300 s | median 24, max 27 | median 24, max 27 |
| `first_drop_off` (s) | median 109 | median 109 |
| `econ_gap` | median 85 | median 91 |
| `depot_losses_young` (young depots lost in all) | median 0 (32) | median 0 (26) |
| `harass_squads` | median 2 | median 2 |
| Harass `worker_kills` in all | 220 | 245 |
| Harass `kills_value` in all | 25485 | 23225 |
| Harass `losses_value` in all | 10175 | 10215 |
| Workers lost by the opponent before its overrun, a Skai opponent (median, mean over 72 slot-games) | 3.0, 4.74 | 5.0, 5.47 |
| Same, a Water opponent | 4.0, 4.15 | 4.0, 4.11 |

(The last two rows are the worker losses of the other side while the profile in the column raids it: in the `aggressive_s5_s` column, the baseline's losses; in the `aggressive_s5` column, the candidate's losses.) The candidate killed more workers (245 against 220) with a smaller value killed (23225 against 25485): the trade did not change measurably.

`thr=` at 300 s of the traced games (seed 20261026, 600 s, `aggressive_s5_s` in both seats, rule TH on in both): `skai_vs_skai` P0 `s9/e0/o58/m409@7,16`, P1 `s14/e0/o199/m678@7,15`; `skai_vs_water` P0 `s2/e0/o1487/m194@16,20`, P1 `s2/e0/o66/m165@14,19`; `water_vs_water` P0 `s8/e0/o283/m307@9,9`, P1 `s4/e0/o1113/m213@8,8`. The full grids are in the `threat_300s_*.txt` files. The win and loss columns were not read.

Note for `depot_losses_young`: a depot that dies with no enemy killer (no `UnitKilledEvent` with an enemy `KillerOwner`) counts as alive, so it never counts as lost young.

<!-- rts/docs/reports/roster_numbers_2/README.md -->
<!-- Report of the roster_numbers_2 measurement (Shellback build 28 to 20, Spoutling damage 5 to 10 and health 70 to 90): commands, wall times, every bar and guard with numbers, the verdict, deviations and what was not done. Part of rts/docs/reports/roster_numbers_2; spec is rts/docs/specs/roster_numbers_2.md. -->
# roster_numbers_2 report

Branch `review/roster-numbers-2-build`, started from `origin/main` at `44fdff7`. Base seed 20261203, run once. Release build.

## Verdict: FAIL (by the spec's rule)

- **G-6 fails**: `response_turtle_skai.json` passes before and fails after. The spec named the response suite as the risk.
- **G-3 CHECK clause fails literally**: a second CHECK line changes (`rule4_every_unit_wins`), see below. This is a consequence of the same Stormcaller flip the spec predicted, so on substance it is minor.
- On substance: B-1 NO_MEASURABLE_EFFECT (0 Water wins after, 1 before), B-2 not WORSE, B-3 BETTER, G-1, G-2, G-4, G-5, G-8 hold.
- As spec 5.1 step 3 and 4.4 require on a guard failure, the data change was **reverted on the branch** (commit `c8e3102`, reverting `5fbb029`). `rts/data` equals `origin/main`. Re-pins and the suite were **not** done (they are for a data change that is not merged). If the developer accepts the change over G-6, cherry-pick `5fbb029` and do spec 5.2.

## Commands and wall times

Command shape (spec 4.1), from the repository root, `RTS_CHUNK_GAMES=1`, `RTS_WORKER_CACHE=<dir>/cache_<run>`, `--jobs 4`:
`--balance --profile aggressive_s5 --maps prototype_1v1 --matchups skai_vs_water --games 72 --seed 20261203 --fights-from rts/docs/reports/roster_numbers_1/sweep/fights.jsonl`.
Mirrors: `--matchups skai_vs_skai,water_vs_water --games 24`. Sweep: `--fights`. Bars: `--fights --fight-settings rts/data/scripts/fights/roster_numbers_1.json`. Response: `--response --profile aggressive_s5`.
Wall times are in `walltimes.txt` (main 2516 s before, 2131 s after; mirrors 1404 s and 874 s; sweep 33 s; bars 122 s and 47 s; response 299 s and 312 s). Another process shared the machine. Exit code 2 of the response and balance runs is the known `hq_move_new_income` failure.
Data smoke check (`--fights --only flat/1500/water.spoutling/skai.roc_rider`, exit 0): result seat1, `left0` 0.000, `left1` 0.125 (Water Spoutling lost; 3 Rocs alive).

## Bars (details and the per-pair table in `pairs.md`; join done with a throwaway script)

| ID | Measure | better / worse / ties | p (two-sided exact) | Verdict |
| --- | --- | --- | --- | --- |
| B-1 | Water won | 0 / 1 / 71 (Water wins 1 before, 0 after, of 72) | 1.0 | not significant, not WORSE: NO_MEASURABLE_EFFECT |
| B-2 | army ratio at 300 s (a pair with a null sample left out) | 44 / 27 / 1 | 0.0568 | not WORSE |
| B-3 | Shellbacks trained | 54 / 6 / 12 | below 0.0001 | BETTER |

## Guards (evidence in `guards.md`)

| ID | Result | Numbers |
| --- | --- | --- |
| G-1 | holds | 42 pairs; max rise among pairs Water wins +0.180 (bound 0.20); no pair crosses +0.35; mean +0.096 (old +0.018; bound 0.12). Matches the designer's S1 run exactly. |
| G-2 | holds | 48 held-out pairs; max rise +0.000; none crosses +0.35; Spoutling against Roc Rider mean margin +0.258 (bound +0.35); mean old +0.033, new +0.097. |
| G-3 | records hold; **CHECK clause fails literally** | 550 of 550 records without a Spoutling side are identical (seed and state_hash ignored); 224 of 230 Spoutling records differ. FIGHTS line: decided 765 to 766, stalemates 13 to 12, dome_absorbed 12 to 11 (explained by Spoutling records). CHECK lines, old data run on this commit against the new sweep: `matchups_agree` changes as predicted (old: `water.spoutling: wins is null...`; new: the Stormcaller line quoted in the spec, measured seat 0 loss left 0.00, seat 1 win left 0.33). **Also `rule4_every_unit_wins` changes**: old `water.spoutling beats no unit of another faction on flat`, new `skai.stormcaller beats no unit of another faction on flat` (the Spoutling now wins a fight; the Stormcaller's only win was over the Spoutling). Both still FAIL; each check reports its first failing unit only. The spec did not list this line. |
| G-4 | holds | (a) water_vs_water decided 24 before, 24 after of 24; (b) 100 of 100 Water-against-Water records without a Spoutling are identical. |
| G-5 | holds | 24 of 24 skai_vs_skai games have the same result, end_tick and play_signature. |
| G-6 | **FAILS** | passed 6 of 10 before, 5 of 10 after. `response_turtle_skai.json` PASS before (supply_lost_before_first_kill 6, first_structure_kill 426.9 s, won_by 682.0 s), FAIL after (p1=FAIL, supply lost 20, first structure kill 491.1 s, p2 PASS won_by 1197.7 s). Other lines moved without changing verdict: mass_windrunner_water trade_ratio 1.477 to 1.019 (PASS), mass_pummeler_water share_gain -0.063 to -0.058 (FAIL both), worker_raid_water max_workers_lost 5 to 4 (FAIL both), turtle_water PASS both. |
| G-7 | not run | Suite and re-pins not done after the revert; no "spot check" line was seen in any harness output, so 0 mismatches was not observed. |
| G-8 | holds | median Shellbacks per Water game 8.5 before, 10.0 after (bound 17); Shellback share of trained Water army units (Tender excluded as the worker) median 0.123 before, 0.145 after (bound 0.35). |

## Reported, not judged (medians over the 72 Water slot-games, before / after)

Shellbacks 8.5 / 10; Grasper (the Pummeler) 28 / 27; Reef Runner 24 / 24; Spoutling 9 / 11. Water army supply at 180 / 300 / 450 s: 10 / 23 / 9 before, 12 / 27 / 11 after; Skai 13 / 33 / 68 both. Water income to 180 s 2755 / 2745. Worker losses before the overrun 5 / 5. Overrun second 313.5 / 325. Game end tick 10321 / 10457. Skai Roc Riders trained 13 / 13. Spec expectation of 25 s later overrun was not seen (about 12 s). Full list at the foot of `pairs.md`. Response files side by side: `response_before.txt`, `response_after.txt`. Balance checks (`balance_use`, `balance_predictor`, `balance_unspent`) fail in both runs; `balance_predictor` accuracy 0.740 before, 0.686 after (not a bar).

## Re-pins

None made (change reverted). The pins that would move are listed in spec 5.2. Not measured.

## Deviations from the spec

- Data change reverted and re-pins and suite skipped, per spec 5.1 step 3 (the coordinator's brief asked for the pins; the spec's stop rule was taken as controlling).
- `main` is `44fdff7`, not `c31e4c9`.
- The matchups.json Stormcaller comment sentence was part of the reverted commit `5fbb029`.
- G-1 and G-2 margins use all flat cross-faction fights with left values (stalemates and unresolved count as their left values), which reproduces the spec's 42 pairs and +0.018 / +0.096.

## Files

`main_before/`, `main_after/`, `mirrors_before/`, `mirrors_after/`, `sweep/`, `bars_before/`, `bars_after/`, `cache_*/` (worker caches), `log_*.txt`, `response_before.txt`, `response_after.txt`, `walltimes.txt`, `pairs.md`, `guards.md`, this README. Data commit (reverted): `5fbb029` changing `rts/data/units/water/shellback.json`, `rts/data/units/water/spoutling.json`, `rts/data/scripts/fights/matchups.json`.

<!-- rts/docs/reports/ai_v3_2026-11-17/README.md: baseline runs of the skirmish AI v3 package A harness modes (ladder and response) on the unchanged v2 AI, 17 Nov 2026. Part of rts/docs/reports; spec rts/docs/specs/skirmish_ai_v3.md A.1, A.4, A.5 (A-10). -->
# Skirmish AI v3, package A: baseline runs (17 Nov 2026)

Both runs use the shipped data after the Reef Runner was added and the v2 AI (profile `aggressive`, every v3 switch absent). Built in Release; 4 worker processes.

## Ladder: `aggressive` against `aggressive` (the full 288 games)

```
dotnet run -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive --profile-b aggressive --out <dir>
```

- Files: `ladder_aggressive_vs_aggressive/ladder.md` (per cell, map, seat, overall), `ladder.jsonl` (one record per game), `stdout.txt`.
- Result line: `LADDER candidate=aggressive baseline=aggressive decided=288 wins=135 fraction=0.469 bar=0.650 FAIL` (exit 2, bar 0.65 not met, as expected for identical profiles).
- All 288 games were decided (no draws, none undecided). Wall time 27 min. Determinism spot check: 2 games re-run in-process, 0 mismatches.
- Candidate share of decided games 46.9 percent, Wilson 95 percent interval [41.2, 52.6], so 50 percent is inside it. Per map: greybox_1v1 44.4, long_lowland 47.2, prototype_1v1 41.7, twin_chokes 54.2 percent.
- Seat effect worth knowing for the later gates: slot 0 won 165 of 288 games (57 percent) between identical profiles (candidate in slot 0: 78 of 144; candidate in slot 1: 57 of 144). The seat rule balances it, but it widens the noise of any ladder.

## Response tests: `--response --profile aggressive`

`response_aggressive.txt`. 0 of 10 pass; expected, since the AI has no scouting, enemy model, counter-mix or static defence yet. Per scenario (p1, p2 are the numbered parts of the check in the spec's A.4 table):

| Scenario | Result | What was observed |
| --- | --- | --- |
| roc_rush_skai | FAIL | p1 fails (no enemy model, "model_air_known_s=none"); p2 passes (8 anti-air units at 270 s: Skai's own units hit air); p3 passes (HQ alive at 600 s) |
| roc_rush_water | FAIL | p1 fails (no model); p2 fails (0 anti-air units at 270 s); p3 passes |
| mass_pummeler_skai | FAIL | answer share 0.000 against base share 0.375 (gain -0.375, needs +0.15); the HQ was dead before 600 s |
| mass_pummeler_water | FAIL | answer share 0.000 against base share 0.182 (gain -0.182); the HQ was dead before 600 s |
| mass_windrunner_skai | FAIL | trade ratio 0.66 (31 supply killed, 47 lost); HQ dead before 600 s |
| mass_windrunner_water | FAIL | trade ratio 0.87 (80 killed, 92 lost); HQ alive |
| worker_raid_skai | FAIL | p1 fails (no static defence near the raided base); p2 passes (1 worker lost per wave, limit 3) |
| worker_raid_water | FAIL | p1 fails (no static defence); p2 passes (3 workers lost in the worst wave, at the limit) |
| turtle_skai | FAIL | 97 army supply lost before any enemy structure fell (limit 16); no enemy structure destroyed and no win by 2400 s |
| turtle_water | FAIL | 83 supply lost before the first structure kill (498.4 s; limit 16); p2 passes (won at 498.4 s) |

Notes: the air_answer part 1 reads the enemy model, which arrives with package B; until then `ResponseSeries.ModelAirKnownTick` stays -1 and the part fails by construction. The runs are deterministic: running the command twice gave identical lines.

## Package C: `aggressive_s2` (counter-mix, tech on demand, static defence, rare priority)

- Ladder against the v2 `aggressive` (full 288 games, Release, 4 workers, 11 min): `ladder_aggressive_vs_aggressive_s2/` (`ladder.md`, `ladder.jsonl`, `stdout.txt`).
  `LADDER candidate=aggressive_s2 baseline=aggressive decided=288 wins=285 fraction=0.990 bar=0.650 PASS`. Cells below 100 percent: long_lowland water mirror 23 of 24, twin_chokes Skai versus water 22 of 24.
- Response tests, `response_aggressive_s2.txt`: 4 of 10 pass (both `air_answer`, `armoured_answer` Skai, `trade` water). Failing and why: see the builder report; short version: `armoured_answer` water (the Lanchester score prefers Graspers over Ink Lobbers, the check wants the highest damage multiplier), `trade` Skai 0.847, `raid` part 1 (the check measures the depot nearest the wave spawn, a later depot the defences near the HQ do not cover), `turtle` (package D).
- `traces/s2_vs_aggressive_greybox_skai_game0.txt`: one traced ladder game (aggressive in slot 0, `aggressive_s2` in slot 1; ladder game 0, ends at tick 6132, hash 017cdd392bb119ca).
- The profile was tuned once from the spec's placeholders: `static_defence_after_raids` 2 to 1 (see the builder report). `counter_weight` 0.9 was tried on the response scenarios and reverted.

### Package C review: what the 99.0 percent shows (ablations, no tuning)

The shipped `aggressive_s2` result (285 of 288 against `aggressive`) is mostly rare priority, not counter-mix play. Rare priority fires at tick 0 (the base composition's rare share 2/7 = 286 of 1000 is above `rare_need_weight_share` 250), which removes rule E's clock, so `aggressive_s2` places its first depot at about 30 s and has far more workers than v2 by 120 s.
Both ablation profiles are committed as `rts/data/ai/abl_s2_no_rare.json` and `abl_s2_blind.json` (ablation only, not game profiles; the profile loading and list tests stay green). Both ladders are full (288 games) and were run with the code after the review fixes.

- **A. `abl_s2_no_rare` (rare priority off) against `aggressive`**, `ladder_ablation_A_no_rare_vs_aggressive/`:
  `LADDER candidate=abl_s2_no_rare baseline=aggressive decided=288 wins=77 fraction=0.267 bar=0.650 FAIL`.
  Per matchup (greybox, long_lowland, prototype, twin_chokes; skai mirror / skai vs water / water mirror): greybox 0.417 / 0.375 / 0.250; long_lowland 0.417 / 0.500 / 0.083; prototype 0.083 / 0.500 / 0.000; twin_chokes 0.125 / 0.458 / 0.000.
  It shows: with the early rare depot removed, counter-mix, tech on demand and static defence together do not beat v2 (they lose, mostly in the mirrors). The 99.0 percent is the economy.
- **B. `aggressive_s2` against `abl_s2_blind`** (same economy: rare priority on, counter-mix weight 0 so the mix equals the base composition, tech on demand and static defence off), `ladder_ablation_B_s2_vs_blind/`:
  `LADDER candidate=aggressive_s2 baseline=abl_s2_blind decided=288 wins=126 fraction=0.438 bar=0.650 FAIL`.
  Per matchup: greybox 0.208 / 0.542 / 0.250; long_lowland 0.583 / 0.833 / 0.708; prototype 0.500 / 0.500 / 0.000; twin_chokes 0.542 / 0.500 / 0.083.
  It shows: between two sides with the same economy, reacting to the enemy does not win on average (43.8 percent, interval 38.1 to 49.5). It helps on long_lowland (70.8 percent) and hurts in the water mirrors and the greybox Skai mirror. `abl_s2_blind` is the counter-mix with `counter_weight` 0, because the switch validation requires `use_counter_mix` for rare priority; tech on demand and static defence are off.
- The earlier trace `traces/s2_vs_aggressive_greybox_skai_game0.txt` was re-recorded with the shipped profile and now equals ladder game 0 (end tick 6132, hash 017cdd392bb119ca). The first version was recorded with `static_defence_after_raids` 2, so it ended at tick 6255; the shipped-profile game ends at tick 6132 (306.6 s).

### Package C2 diagnosis traces (17 Nov 2026)

Evidence for `rts/docs/specs/skirmish_ai_v3.md` Package C2 (C2.1): five ladder games of the B ablation re-traced with the ladder seed 20261117 (`--trace --profile-a abl_s2_blind --profile-b aggressive_s2 --seed 20261117 --game N`; each ends on the tick of its `ladder.jsonl` record): `traces/c2_prototype_water_g0.txt`, `c2_prototype_water_g2.txt`, `c2_twin_chokes_water_g0.txt`, `c2_twin_chokes_water_g2.txt` and `c2_greybox_skai_g0.txt`; and `traces/c2_equal_cost_fights.txt`, equal-cost army fights in the simulation from scratch command scripts (setup in its header).

## Package C2: `aggressive_s2b` (counter-mix revision)

Gate result: **MISSED** (see the plain statement below). Four full ladders were run (one gate run and the three retunes the spec allows), every one 288 games, Release, 4 workers, 21 to 25 minutes, `aggressive_s2b` against `abl_s2_blind`, seed 20261117:

- `ladder_c2_s2b_vs_blind/` (run 1 (spec values)): `LADDER candidate=aggressive_s2b baseline=abl_s2_blind decided=288 wins=172 fraction=0.597 bar=0.650 FAIL`
- `ladder_c2_s2b_vs_blind_run2/` (run 2 (counter_weight 0.5)): `LADDER candidate=aggressive_s2b baseline=abl_s2_blind decided=288 wins=154 fraction=0.535 bar=0.650 FAIL`
- `ladder_c2_s2b_vs_blind_run3/` (run 3 (budget 1200; SHIPPED)): `LADDER candidate=aggressive_s2b baseline=abl_s2_blind decided=288 wins=176 fraction=0.611 bar=0.650 FAIL`
- `ladder_c2_s2b_vs_blind_run4/` (run 4 (budget 1200, share 0.5)): `LADDER candidate=aggressive_s2b baseline=abl_s2_blind decided=288 wins=171 fraction=0.594 bar=0.650 FAIL`

Candidate wins per cell (of 24 decided games each; the cell floor is 9 wins = 0.375, the gate wants 0.350, so 9 of 24 is the lowest passing count):

| map / matchup | run 1 | run 2 | run 3 (shipped) | run 4 |
| --- | ---: | ---: | ---: | ---: |
| greybox_1v1 / skai_vs_skai | 14 | 11 | 12 | 12 |
| greybox_1v1 / skai_vs_water | 11 | 8 | 12 | 8 |
| greybox_1v1 / water_vs_water | 15 | 12 | 16 | 18 |
| long_lowland / skai_vs_skai | 13 | 10 | 14 | 14 |
| long_lowland / skai_vs_water | 17 | 18 | 17 | 18 |
| long_lowland / water_vs_water | 21 | 20 | 18 | 19 |
| prototype_1v1 / skai_vs_skai | 23 | 21 | 20 | 17 |
| prototype_1v1 / skai_vs_water | 12 | 12 | 12 | 12 |
| prototype_1v1 / water_vs_water | 15 | 14 | 17 | 17 |
| twin_chokes / skai_vs_skai | 5 | 10 | 11 | 11 |
| twin_chokes / skai_vs_water | 12 | 12 | 12 | 11 |
| twin_chokes / water_vs_water | 14 | 6 | 15 | 14 |

Shipped `aggressive_s2b` (run 3: `mix_eval_budget_common` 1200, every other value as the spec) passes the ladder part of the gate (0.611 of 288, lowest cell 11 of 24 = 0.458). It fails the response part: `response_aggressive_s2b.txt` has `RESPONSE_SUMMARY passed=3 of=10`; excluding the water `armoured_answer` (decision Q-C2-1) that is 3 of 9, the gate wants 5. Run 4 (`static_defence_max_value_share` 0.5) passes 5 of 9 (both `raid` files join the three; `response_aggressive_s2b_share05.txt`) but its ladder is 171 of 288 = 0.594 with greybox Skai versus water at 8 of 24 = 0.333 (below 0.35). No single value passed both parts; the three allowed retunes are used. The two gate parts are therefore not met together.

Response lines of the shipped profile: air_answer skai and water PASS; armoured_answer skai FAIL (share_gain -0.033, needs a gain; base share 0.462, share 0.429), armoured_answer water FAIL (expected, Q-C2-1: share_gain -0.090); trade skai FAIL (0.860 of 1.0; killed 74, lost 86), trade water PASS (1.038); raid skai and water FAIL (static_defence_near_raided_base=false, part 2 passes); turtle skai and water FAIL (package D).

Tuning evidence (all in this folder): `c2_tuning_cell_probes.txt` (24-game probes of the twin_chokes Skai mirror, the worst cell of run 1 at 5 of 24, for single-value variants), `c2_tuning_raid_grid.txt` (both `raid` scenarios over `static_defence_max_value_share` 0, 0.15, 0.3, 0.5, 1, 2 times `raid_min_supply` 1 to 4; the pass pattern is noisy, share 0.5 passes both at `raid_min_supply` 2, 3 and 4).

Traces (`traces/`): `c2_final_prototype_water_g0.txt` and `c2_final_prototype_water_g2.txt` (prototype_1v1 water mirror, `aggressive_s2b` shipped against `abl_s2_blind`, ladder seed 20261117, games 0 and 2; the candidate wins both, game 0 as slot 1 at tick 12895 and game 2 as slot 0 at tick 17808), and `c2_final_twin_chokes_skai_g14_loss.txt` (the worst cell, twin_chokes Skai mirror, a lost game, candidate slot 0, ends tick 7104 in the ladder).

C2.1-style columns for the two water traces (summed from the 30-second lines; `trained` counts units, `lost` counts units; common and rare are gathered totals; first army unit is the first 30-second line with an army, so at most 30 s late):

| Game | Side | First army unit | Static defences (max seen / destroyed) | Reef Runners trained = lost | Shellbacks | Pummelers | Ink Lobbers | Spoutlings | Common / rare gathered | Rare bank at 300 s / peak |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| g0 | `aggressive_s2b` (slot 1) | at or before 90 s | 0 / 0 | 3 = 3 | 8 | 75 | 4 | 16 | 14230 / 1200 | 435 / 680 |
| g0 | blind (slot 0) | at or before 90 s | 0 / 0 | 6 = 6 | 8 | 70 | 7 | 26 | 14725 / 1200 | 355 / 670 |
| g2 | `aggressive_s2b` (slot 0) | at or before 90 s | 2 / 1 | 3 = 3 | 14 | 118 | 5 | 27 | 21000 / 1885 | 395 / 1010 |
| g2 | blind (slot 1) | at or before 90 s | 0 / 0 | 3 = 3 | 10 | 90 | 11 | 34 | 18785 / 2400 | 355 / 1130 |

Package C had the reacting side build 25 and 31 Reef Runners and 6 to 10 destroyed turrets in the same games (the C2.1 table); now it builds the 3 scout Reef Runners and 0 to 2 turrets. The rare bank still reaches 680 to 1130 at its peak (blind and candidate alike), so the cap of 300 does not stop rare piling up; why was not investigated. Spent-resource columns are not in the 30-second lines and are not reported.

Reading of the worst cell: in the twin_chokes Skai mirror both sides end with almost the same army (9 Roc Riders, 3 Stormcallers each in game 14: rare-limited) and the result follows the seat (the candidate as slot 0 won 4 of 12, as slot 1 7 of 12 in run 3), so the cell is close to a coin flip with a seat effect and the predictor mix is not what loses it once `counter_weight` is 0.7 and `mix_eval_budget_common` 1200. In run 1 (budget 600) the same cell was 5 of 24; `counter_weight` 0.5 gave 10 of 24 there but lowered the total to 154.


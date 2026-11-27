<!-- rts/docs/specs/response_suite_guard.md -->
<!-- Proposal and build spec (27 Nov 2026) for how the AI response suite is used as a guard. Measures how noisy each of the 10 response scenarios is on main (aggressive_s5, 6 to 13 seeds per scenario), reads the four recent verdicts the suite decided by one scenario flipping, proposes a seeded, paired guard fixed before any run (RG), keeps the single-game guard as the alternative, and lists the harness packages for one Sonnet builder. A decision for the developer: nothing here changes any bar or guard by itself. Part of rts/docs/specs (index: README.md). -->
# Spec: the response suite as a guard

Written 27 Nov 2026 against `main` at `b76b15f`. Every number below that is not a measurement is a **placeholder**. This is a proposal: no bar, guard or verdict changes until the developer decides (section 6). **Verdicts already issued stand as recorded** (`roster_numbers_1` G-6, `ai_kite` K-G3, `roster_numbers_2` G-6 and the `ai_reef_on` reading).

## 0. Goal and sources

- The response suite (spec `skirmish_ai_v3.md` A.4; harness `--response --profile <id>`, `rts/tools/BalanceHarness/ResponseRunner.cs`, checks in `ResponseChecks.cs`, entries in `rts/data/scripts/ai_eval/ai_eval.json` `response_tests`, scripts `rts/data/scripts/ai_eval/response_*.json`) plays **one** game per scenario. Each script has `"seed": 1234`, and the harness has no way to change it in response mode (`--seed` is not read there). That seed drives the simulation's generator, the AI's own generator and its variety stream (opening, composition bands, jitters; `SkirmishAiController.SeedFor`, `AiVariety.SeedFor`).
- Recent specs use it as a guard of the form "no scenario that passes before fails after" (`roster_numbers_1.md` G-6, `ai_kite_correction.md` K-G3, `roster_numbers_2.md` G-6, `ai_midgame_window.md` guards). Three verdicts in a row were decided by one scenario flipping (table in 2.1).
- Goal: say whether those flips are noise, and propose a guard that tells a regression from a re-rolled game, with stated power and cost, fixed before any run.

## 1. Noise measurement (designer's worktree at `b76b15f`, Release build)

**Method.** A scratch copy of `rts/data` per seed, with `"seed": 1234` replaced by the seed in every `response_*.json` (nothing else changed), then `--response --profile aggressive_s5`. This is the smallest perturbation the data allows: it changes the match seed, which re-rolls the AI's variety draws and the simulation's generator. Seeds: 1234 (the shipped one), 1, 2, 3, 4, 5 for all 10 scenarios; 7 to 13 as well for the two turtle scenarios (`--only`; seed 6 was lost to a copy race in the scratch script). Seed 1234 reproduced `reports/roster_numbers_2/response_before.txt` line for line, so the scratch method plays the same games as the harness. The thresholds are those of `ai_eval.json`.

| Scenario | Games | Passed | Measured value (threshold) | Spread over the seeds |
| --- | --- | --- | --- | --- |
| `roc_rush_skai` | 6 | 6 | `aa_units` (min 2) | 12 to 22 |
| `roc_rush_water` | 6 | **5** | `aa_units` (min 2) | 1 to 4 (seed 5: 1, FAIL); `model_air_known_s` 94 to 130 |
| `mass_pummeler_skai` | 6 | 0 | `share_gain` (min 0.15) | -0.312 to +0.146 |
| `mass_pummeler_water` | 6 | 0 | `share_gain` (min 0.15) | -0.098 to +0.058 |
| `mass_windrunner_skai` | 6 | 6 | `trade_ratio` (min 1.0) | 1.000 to 2.909 (seed 1 exactly at the threshold) |
| `mass_windrunner_water` | 6 | 6 | `trade_ratio` (min 1.0) | 1.143 to 2.474 |
| `worker_raid_skai` | 6 | 0 | p1 static defence; p2 workers lost (max 3) | p1 false 6 of 6; workers lost 4 to 5 |
| `worker_raid_water` | 6 | **1** | as above | p1 true once (seed 5); workers lost 2 to 5 (p2 passes 4 of 6) |
| `turtle_skai` | 13 | **7** | supply lost before first structure kill (max 16) | 6 to 58: passes at 6, 12, 12, 12, 13, 15, 16; fails at 18, 37, 45, 54, 57, 58 |
| `turtle_water` | 13 | **8** | as above | 4 to 41: passes at 4, 5, 5, 7, 8, 13, 15, 16; fails at 18, 21, 25, 32, 41 |

Suite totals per seed (6 full runs): 6, 5, 5, 6, 6, 6 of 10. Wall time per full run (10 games, one process, two runs side by side on a 4-core machine that two builders were also loading, load average about 18): 243 to 373 s, mean about 307 s, so about 31 s per game.

**Reading.** Six scenarios gave the same verdict on every seed tried (three always pass, three always fail; `mass_windrunner_skai` once sat exactly on its threshold). Four are noisy: both turtles are close to a coin toss (pass rates 0.54 and 0.62), `roc_rush_water` and `worker_raid_water` flip about one game in six. The suite's single-game total moves by one between seeds with no change at all.

### 1.1 Why `response_turtle_water` jumps between about 8 to 12 and 26

A scratch observer (outside the repository, reading `ResponseSeries.OwnArmyLost` after `ResponseRunner.Run`) on seed 1234, `aggressive_s5` against `aggressive_s5_kite`:

- The two games are identical to 368.7 s (three Reef Runners lost at 177.9, 279.3 and 368.7 s, 3 supply).
- At about 390 s the first attack meets the turret ring and the turtle's Shellbacks. In the passing game it loses 2 Reef Runners and 2 Graspers, kills six Shellbacks and the first structure at 428.8 s: 8 supply lost. In the failing game 11 Graspers die between 390.5 and 420.9 s (22 supply), nothing structural falls, and the second attack kills the first structure at 486.4 s: 26 supply lost.

So at a fixed seed the value is **bimodal on one event**: whether the first push (about 390 s) breaks a structure. "26" recurs in `roster_numbers_1` and `ai_kite` because seed 1234 always draws the same opening, so the first wave has the same size (3 + 22 + 1). The threshold of 16 sits between the two modes, so the verdict is the outcome of one fight. Across seeds the openings differ and the spread is wider (4 to 41) but still decided mainly by that first push (passing games kill their first structure at 299 to 531 s, failing ones at 484 to 587 s). `turtle_skai` behaves the same way, more sharply: 6 to 16 or 37 to 58, with one game at 18.

## 2. The four flips: noise, not evidence of regression

### 2.1 The flips

| Verdict | Scenario | Before | After |
| --- | --- | --- | --- |
| `roster_numbers_1` G-6 (Reef Runner health) | `turtle_water` | PASS 12 (first kill 411.0 s) | FAIL 26 (451.1 s) |
| `ai_reef_on` (Reef Runner weight 30 on) | `turtle_water` | FAIL (5 of 10) | PASS 8 (6 of 10) |
| `ai_kite` K-G3 (`aggressive_s5_kite`) | `turtle_water` | PASS 8 (428.8 s) | FAIL 26 (486.4 s) |
| `roster_numbers_2` G-6 (Shellback, Spoutling) | `turtle_skai` | PASS 6 (426.9 s) | FAIL 20 (491.1 s) |

### 2.2 Reading

1. **All four flips are in the two turtle scenarios**, whose own pass rate on unchanged `main` is 0.54 and 0.62 over 13 seeds. Every after-value (26, 26, 20) lies inside the baseline's spread over seeds (up to 41 and 58). The one flip in the other direction (`ai_reef_on`) is the same mechanism.
2. **Any change re-rolls the decisive fight.** The kite trace is identical until 368.7 s and diverges at the first push, the event that decides the verdict (1.1). A data or profile change that touches a scenario changes its trajectory from some tick on, much as a new seed does.
3. **The current guard fails a harmless change often.** If a change re-rolls the noisy scenarios it touches, a scenario with pass rate p passes before and fails after with chance p(1 - p): 0.237 for `turtle_water`, 0.249 for `turtle_skai`, 0.139 each for `roc_rush_water` and `worker_raid_water`. A Water-plan-only profile change (it leaves the `_skai` scenarios byte-identical, as the three Water changes did) fails "no scenario that passes before fails after" with chance about 1 - 0.861 x 0.861 x 0.763 = **0.43**; a Water data change that also touches the turtle opponent (as `roster_numbers_2`) about **0.57**. Three fails in four verdicts fit this.
4. **What this does not show.** It does not prove the candidates were harmless: one game per side has no power to tell. The model above treats a change as a full re-roll (an upper bound on the false alarm rate; a change that diverges after the decisive fight flips nothing). No candidate was re-run on other seeds here.

## 3. Proposed guard RG (paired seeds, fixed before any run)

### 3.1 Definition

- **Games.** For each of the 10 scenarios in `ai_eval.json` `response_tests` and each game index g in 0 to N - 1, play the scenario with match seed `SeedDerivation.GameSeed(B, map, stem, g)` (B the base seed, map the script's map ID, stem the script file name without `.json`). Baseline and candidate play the **same** seeds, so (scenario, g) is a pair. A scenario the candidate does not touch gives identical games and only concordant pairs.
- **Outcome per game.** PASS or FAIL by the existing check and its existing thresholds (unchanged).
- **Counts per scenario s.** `B_s` = pairs the baseline passed and the candidate failed; `C_s` = pairs the candidate passed and the baseline failed.
- **RG fails** when either holds:
  - **RG-a (one scenario collapses):** for some s, the exact one-sided sign test of `B_s` against `C_s` gives p at or below `response_guard_scenario_alpha` (0.01). At N = 8 this means `B_s` at least 7 with `C_s` = 0, or `B_s` = 8.
  - **RG-b (the suite gets worse):** the exact one-sided sign test of the sum of `B_s` against the sum of `C_s` gives p at or below `response_guard_suite_alpha` (0.05).
- **Otherwise RG holds.** The report also prints, as information only, each scenario's pass count for both sides and the single shipped-seed game (seed 1234) lines.
- **Fixed before any run.** The spec that uses RG names the base seed and N before any game; the designer's trial runs never use that base seed; there is one run per side, no re-run with another seed after a result, and thresholds and alphas are not changed after a result. The verdict is computed by the harness (package RG-2), not by hand.

### 3.2 Stated power (Monte Carlo, 5000 to 20000 trials, the independent re-roll model of 2.2, baseline pass rates of section 1)

| N seeds | Games (both sides) | False alarm on a harmless re-rolling change | One always-pass scenario collapses (1 to 0) | One always-pass scenario halves (1 to 0.5) | `turtle_water` 0.62 to 0.2 | `turtle_water` 0.62 to 0 | Every scenario -0.2 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 6 | 120 | 0.02 | 0.34 | 0.12 | 0.10 | 0.17 | 0.67 |
| **8 (proposed)** | **160** | **0.03** | **1.00** | 0.20 | 0.16 | 0.33 | **0.82** |
| 10 | 200 | 0.03 | 1.00 | 0.33 | 0.25 | 0.57 | 0.90 |
| 12 | 240 | 0.03 | 1.00 | 0.51 | 0.33 | 0.77 | 0.94 |

Current single-game guard for comparison: false alarm 0.43 to 0.57 (2.2), and it catches a collapse of an always-pass scenario with certainty but cannot tell a coin-toss scenario getting worse from a re-roll. RG at N = 8 keeps the certain catch of a collapse, cuts the false alarm to about 0.03, catches a broad regression 82 percent of the time, and is weak (16 to 33 percent) on a partial regression of one noisy scenario. That weakness is stated, not hidden: a spec that targets one scenario should name that scenario as its own bar with its own N.

### 3.3 Cost

- 8 seeds x 10 scenarios x 2 sides = **160 games**. At the measured 31 s per game per process under shared load: about 83 min serial; about **21 to 25 min at `--jobs 4`** on an unshared 4-core machine (estimate; the turtle games are the longest). The baseline side depends only on `main`, `aggressive_s5` and B, so a run already recorded for the same `main` commit can be reused (80 games, about 11 min).
- The single-game suite costs 20 games (about 5 to 6 min).

### 3.4 Alternative: keep the single-game suite

"No scenario that passes before fails after", one game per scenario at seed 1234, as today. Cost 20 games. False alarm 0.43 to 0.57 on a change that touches the noisy scenarios (2.2); a verdict decided by a turtle flip says nothing about the candidate.

## 4. Data (harness data, not simulation): `rts/data/scripts/ai_eval/ai_eval.json`

Each key gets the standard comment line (`// <what>. Unit: <unit>. Range: <range>. Source: rts/docs/specs/response_suite_guard.md 3.1; placeholder.`) and is added to `AiEvalSettings` with `RejectUnknownKeys` updated.

| Key | Unit | Range | Starting value |
| --- | --- | --- | --- |
| `response_guard_base_seed` | seed | 0 to 2147483647 | 20261128 (placeholder; never run by the designer) |
| `response_guard_seeds` | games per scenario | 1 to 64 | 8 (placeholder) |
| `response_guard_scenario_alpha` | fraction | 0.0001 to 0.5 | 0.01 (placeholder) |
| `response_guard_suite_alpha` | fraction | 0.0001 to 0.5 | 0.05 (placeholder) |

Alphas are read with `Fix64.Parse` and converted to parts per 10 000 as the existing paired statistics do (`PairedStats`, integer arithmetic only).

## 5. Packages (one Sonnet builder; harness only, no `rts/src/Sim` or `rts/src/Ai` change, so no sim-reviewer pass is required; ask for one if any file under `rts/src` is touched)

### RG-1: seeded response runs

- `ResponseRunner.Run` gains an optional match seed (null = the script's seed, today's behaviour, byte for byte).
- Response mode reads `--seeds N` (1 to 64), `--seed B` (base seed; default `response_guard_base_seed`), `--jobs J` and `--out <file>`. Without `--seeds` the mode is unchanged. With `--seeds`, games run through `WorkerPool` (a `--worker` job per (scenario, g), as `--fights` does, so `RTS_WORKER_CACHE` and `RTS_CHUNK_GAMES` resume work), and results are written in (`response_tests` order, g) order regardless of completion order.
- `--out` writes JSON Lines, one record per game: `script`, `check`, `game` (g), `seed` (decimal string), `profile`, `content_digest` (as `GameRecord`), `passed`, `parts` (number, passed, metric string), `final_hash` (x16). Stdout: the existing `RESPONSE` line with ` seed=<g>` appended, then per scenario `RESPONSE_RATE <script> passed=<k> of=<N>`, then `RESPONSE_SUMMARY`.

### RG-2: the guard verdict

- New command `--response-guard --from-a <baseline.jsonl> --from-b <candidate.jsonl>` (works for a profile change and for a data change alike, since both are just two RG-1 runs). Joins records on (`script`, `game`); fails with exit 1 if the two files differ in scripts, game counts or seeds, or if any record is missing.
- Prints per scenario: baseline and candidate pass counts, `B_s`, `C_s`, the RG-a p-value; then the pooled sums and the RG-b p-value; then `RESPONSE_GUARD HOLDS` (exit 0) or `RESPONSE_GUARD FAILS <reasons>` (exit 2). Uses `PairedStats.SignTest` (or a one-sided variant added beside it, integer arithmetic).

### RG-3: tests (NUnit, `rts/tests/Sim.Tests/Harness/ResponseGuardTests.cs`)

1. `Run` with a null seed equals today's run of `response_turtle_water.json` with `aggressive_s5` (same `final_hash` and `Line()`).
2. `Run` with seed 1234 passed explicitly equals the null-seed run.
3. The derived seeds for g = 0 to 7 of one script are distinct, differ between two scripts, and equal `SeedDerivation.GameSeed(B, map, stem, g)`.
4. `--only response_roc_rush_skai.json --seeds 2` with `--jobs 1` and `--jobs 2` give byte-identical `--out` files.
5. Guard on synthetic records: identical files give `HOLDS` with every `B_s` = `C_s` = 0.
6. One scenario with `B_s` = 7, `C_s` = 0 at N = 8, all others concordant: `FAILS` with reason RG-a for that scenario.
7. `B_s` = 6, `C_s` = 0 in one scenario and all others concordant: RG-a holds (p = 1/64 > 0.01) and RG-b fails (p = 1/64 <= 0.05), so `FAILS` with reason RG-b only.
8. Spread discordance (sum B = 10, sum C = 6, no scenario above 3): `HOLDS` (one-sided p about 0.23).
9. Mismatched seeds or a missing (script, game) record: exit 1 with a message naming the record.
10. The four new `ai_eval.json` keys load, and a value out of range fails validation.

Changelog bullets for the builder's report: one for RG-1 (seeded response runs), one for RG-2 (guard verdict), one for the data keys.

## 6. What the developer decides

1. **Replace** the single-game guard with RG in specs written after RG-1 to RG-3 are merged (designer's recommendation).
2. **Keep** the single-game guard as it is (3.4).
3. **Report only**: run either form and print it, but no verdict fails on the response suite.

**Default until the developer answers: option 2, nothing changes.** Specs already written (including `ai_midgame_window.md` MG-B) apply their guards as written. Verdicts already issued stand as recorded. Building RG-1 to RG-3 does not by itself change any spec's guard.

## 7. Out of scope

- Any change to a check, threshold or scenario script (the turtle threshold of 16 between the two modes is noted, not changed).
- P-005 bar item 1 ("every response scenario passes for both factions"): unchanged; see Q-2.
- Making the AI pass the turtle scenarios more reliably.
- Re-running past candidates on many seeds to re-judge them.

## Questions for the developer (builders use the defaults)

- **Q-1. Which guard (section 6)?** *Default: keep the single-game guard (option 2) until you answer; recommendation: replace with RG (option 1).*
- **Q-2. Should P-005 bar item 1 be read over seeds** (for example, every scenario passes in at least 7 of 8 seeds) instead of one game? *Default: no change; bar item 1 stays as recorded.*
- **Q-3. N = 8 or 10?** N = 10 doubles the chance of catching a turtle-sized partial regression (0.33 to 0.57 for 0.62 to 0) for 40 more games. *Default: 8.*
- **Q-4. Should the baseline side be reused** from an earlier run on the same `main` commit (same profile, base seed and N, checked by `content_digest`)? *Default: yes.*
- **Q-5. Should the shipped-seed games (1234) stay in reports** as information? *Default: yes, printed, never judged under RG.*

## What the designer did not check

- No candidate was run on several seeds, so the reading of the four flips rests on the baseline's noise and one trace, not on a paired re-run of those candidates.
- Six seeds for eight scenarios is a small sample: a scenario that passed 6 of 6 may still fail sometimes (`mass_windrunner_skai` touched its threshold once).
- Wall times were taken on a loaded machine; the `--jobs 4` estimate is not measured.

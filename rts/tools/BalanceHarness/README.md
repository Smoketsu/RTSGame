<!-- rts/tools/BalanceHarness/README.md: what the headless runner does today and what the balance harness grows into. -->
# Balance harness (headless runner)

A console program that plays the simulation without Unity. It is the proof that the simulation is engine-free and
deterministic, and it is the seed of the balance harness described in the Game Design Proposal ("Matchups and
balance / Balance philosophy").

## What it does now (Phase 0)

1. Reads every `*.json` file under `--data` (normally `rts/data`) and hands the text to `Rts.Sim.Data.DataLoader`.
   This tool does the file IO; the simulation assembly never does.
2. Parses the command script with `Rts.Sim.Data.CommandScript` (in the simulation assembly, so the Unity host reads the very same file) and builds a `Simulation` from the loaded content, `BuiltInMechanics.CreateRegistry()` and the players in the script.
3. Submits the script's commands, steps the requested number of ticks, and prints:
   - one line per state hash: `tick=<n> hash=<16 hex digits>` (every `state_hash_interval_ticks`, default 20 = once a second);
   - a final line `STATE_HASH=<16 hex digits>` for the state after the last tick.
4. Exit code 0 on success. Any error (bad arguments, missing file, invalid data, bad script) prints
   `error: <message>` to stderr, where data and script errors start with the file path and name the field, and exits with 1.
   Commands the simulation rejects (for example moving a unit that does not exist) print a `warning:` on stderr and do not change the exit code.

5. Skirmish AI (Phase 1, `rts/src/Ai`): a player entry with `"ai": "<profile>"` in the script, or the repeatable option
   `--ai <slot>:<profile>`, attaches a `SkirmishAiController` playing that slot by `rts/data/ai/<profile>.json` (an option
   wins over the script for the same slot). Before every step the controllers think in ascending slot order and submit
   ordinary commands for that tick. In a match with two or more teams the run stops as soon as `MatchOutcome` is decided.
   Every run prints `OUTCOME winner=<team number> tick=<n>` (or `OUTCOME winner=draw tick=<n>`, or `OUTCOME undecided tick=<n>`
   when the tick limit is reached first; n is the last tick stepped) just before `STATE_HASH`.
   `rts/data/scripts/ai_vs_ai.json` is the Phase 1 exit script: Skai (aggressive AI) against Water (defensive AI) on `greybox_1v1`.

Same data, script, seed and tick count always give the same output, on any machine. CI runs the smoke script twice and
requires the two `STATE_HASH` lines to match. The default script, `rts/data/scripts/smoke.json`, is also what the Unity
`SimulationHost` plays, so the `tick=<n> hash=<hex>` lines can be compared with the Unity Console (see `docs/UNITY_SETUP.md`).

```
dotnet run --project rts/tools/BalanceHarness -- --data rts/data [--script <file>] [--ticks N] [--seed S] [--record <replay file>] [--ai <slot>:<profile> ...]
dotnet run --project rts/tools/BalanceHarness -- --data rts/data --script rts/data/scripts/ai_vs_ai.json --ai 0:aggressive --ai 1:defensive
```

| Option | Meaning | Unit and range |
| --- | --- | --- |
| `--data <folder>` | Data folder with `tuning/`, `units/`, `factions/`, `maps/`, `cycles/`, `ai/`, `scripts/` | Required |
| `--script <file>` | Script JSON (comments allowed) | Optional; default `<data folder>/scripts/smoke.json` |
| `--ticks N` | Overrides the script's `ticks` | Ticks, 0 to 1 000 000 |
| `--seed S` | Overrides the script's `seed` | Unsigned integer |
| `--ai <slot>:<profile>` | Repeatable: an AI plays player slot `<slot>` with `rts/data/ai/<profile>.json` | Slot 0 to 254 (must be a script player); profile file name without `.json` |
| `--record <file>` | Writes a replay of the run (script and AI commands, hash checkpoints, final hash; ADR-009) after the `STATE_HASH` line and prints `REPLAY_RECORDED file=<path> ticks=<n> commands=<n> checkpoints=<n>`; `tools/ReplayCheck` verifies it | Any writable file path |

## Script format

Scripts live in `rts/data/scripts/` (the data loader skips that folder; `Rts.Sim.Data.CommandScript` parses them). See `rts/data/scripts/smoke.json`, which is commented line by line. In short:

```
{ "$schema_version": 1, "$doc": "...", "seed": 1234, "ticks": 600,
  "players":  [ { "id": 0, "team": 0, "faction": "skai", "name": "A" }, ... ],
  "commands": [ { "tick": 0, "issuer": 0, "sequence": 0, "type": "spawn_unit",
                  "definition": "skai.windrunner", "owner": 0, "x": "4", "y": "4" },
                { "tick": 20, "issuer": 0, "sequence": 1, "type": "move_unit", "unit": 1, "x": "20.5", "y": "12" },
                { "tick": 300, "issuer": 0, "sequence": 2, "type": "remove_unit", "unit": 1 },
                { "tick": 310, "issuer": 255, "sequence": 0, "type": "noop" } ] }
```

- `issuer` 255 means "the script itself" (no player). `x` and `y` are tiles, written as decimal text or as numbers.
- Command `type` values: `noop`, `spawn_unit` (`definition`, `owner`, `x`, `y`; script issuer only), `move_unit` (`unit`, `x`, `y`), `remove_unit` (`unit`), `attack_move` (`unit`, `x`, `y`), `attack_unit` (`unit`, `target`), `stop` (`unit`), `root_hq` and `uproot_hq` (`unit`), `gather` (`unit`, `field`: index in the map's `resource_fields`), `train_unit` (`producer`, `definition`) and `build_structure` (`unit`: the worker, `definition`, `x` and `y` as whole tile numbers; the worker walks to the site and places the structure after `worker_placement_seconds`; the site must be explored). `pick_cycle` (`cycle`: a cycle ID) forces the next Planetary Cycle when issued by the script (issuer 255); a player on the Core-holding team may issue it too. The harness passes the data set's cycles to the match. `cast_wave` (`unit`: the caster, `x`, `y`: the point the wave heads towards) casts the Water wave; `rts/data/scripts/flood_smoke.json` exercises flood sources and the flood limits. `rts/data/scripts/economy_smoke.json` exercises the HQ and economy commands on `greybox_1v1`.
- Entity IDs start at 1 and are handed out in the order spawn commands are applied: by tick, then issuer, then sequence.
- Commands scheduled at or after the last tick never run; the tool warns about them on stderr.
- Unknown keys, missing keys and wrong types are errors that name the script path and the field.

## What it grows into (Phase 2)

The same executable becomes the AI-versus-AI balance harness. Planned shape, with the numbers coming from
`rts/data/tuning/harness.json` and `hq.json` rather than from code:

- Play **500 games per matchup per map** (`games_per_matchup_per_map`) with scripted or AI players, in parallel, each game
  headless and seeded, so a whole run finishes on one developer machine.
- Report per matchup and map: **win rate** (flag matchups outside the target band), **match length** (median, with the
  hard cap `water_vs_earth_median_max_seconds` for water versus earth), and **HQ time to kill** against the
  strongest HQ-killing army (the survivability floor `hq_floor_seconds` at `hq_floor_supply`).
- Write results as a machine-readable file (JSON or CSV) plus a short text summary, so CI can fail a build whose balance drifts.
- Keep the determinism check: every game still ends with a `STATE_HASH`, and replaying a seed must reproduce it.

## Batch mode (Phase 2 step 5)

`--batch` plays N AI-versus-AI games per matchup per map in parallel worker processes (this same executable with `--worker`) and writes
`report.md`, `results.jsonl` and `hq_floor.jsonl` to `--out` (default `rts/build/harness`). Spec: `rts/docs/specs/balance_harness.md`; settings:
`rts/data/tuning/harness_batch.json`.

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --batch [--ci | --games N] [--jobs J] [--maps a,b] [--matchups x_vs_y,...]
    [--seed S] [--max-game-seconds T] [--verify K] [--out <dir>] [--report-only]
```

Exit codes: 0 every hard limit passed or deferred (or `--report-only`), 1 error, 2 a hard limit failed, 3 the determinism spot check failed.
The report never depends on `--jobs`. The single-run mode above is unchanged.

## Trace mode

`--trace` plays the one AI-versus-AI game a batch would play for a map, matchup, base seed and game index (same seed derivation, AI profile and cap),
and writes a readable timeline: every 30 game-seconds one line per player (bank, income, supply, workers (`gather=` counts every worker in a gather loop; `waiting=` is how many of those queue at a full field, specs/field_saturation.md), structures, army, trained and lost,
HQ, commands by kind, rejected commands by reason, attack state, army distance to the enemy HQ), then a summary with totals, first ticks, stance
changes and each player's last 20 commands. Build orders are reported as ordered (spend on acceptance), placed (the structure appeared) and cancelled by reason (`new_order`, `worker_lost`, `site_blocked`, `site_occupied`, `unreachable`; refunds come off the spent total). It only reads events and state, so the final state hash equals the untraced game's.

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --trace --map greybox_1v1 --matchup skai_vs_water --seed 20261026
    [--game N] [--max-game-seconds T] [--out <file>]
```

With `--profile-a <id> --profile-b <id>` the two slots play the ladder seat rule for the game index (use the ladder's `--seed` and `--max-game-seconds` to replay a ladder game). Without `--out` the timeline goes to stdout; the game's batch-style result line is always printed to stdout after it. A review made with it is in
`rts/docs/reports/ai_trace_2026-11-16/`.

## Fight mode (micro benchmark, packages F1 to F3)

Spec: `rts/docs/specs/micro_benchmark.md`. Settings: `rts/data/scripts/fights/fights.json` (read by `FightSettings`; one value per line, so a script can edit it by key) and the matchups file
`rts/data/scripts/fights/matchups.json` (read by `FightMatchups`). Both live under `scripts/`, outside the content digest. `--fights` plays equal-value army fights with no strategic AI
and no HQ: every swept combat unit and composition against every other, in both seats, on every terrain case (`flat`, `high_ground`, `flooded`, `choke`) at every army value. The control is
plain attack-move with idle re-targeting. A fight ends by elimination, a stalemate (neither side can target the other, or no damage for a while) or the cap. The winner is the side with
value left, and the output is a value-exchange matrix.

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --fights [--jobs J] [--seed S] [--only <fight id>] [--out <dir>] [--strict]
```

- Fights run in worker processes (`--worker --fights --first <i> --count <n>`), the HQ floor trials as `--worker --fights --hq-floor <attacker faction>:<unit or composition>:<defender faction>:<rooted|uprooted>`.
  The first `verify_fights` fights are re-run in-process and must give identical JSON lines. No output depends on `--jobs`; elapsed time goes to stderr only.
- `--only <fight id>` (for example `flat/1500/water.grasper/water.ink_lobber`) plays one fight in-process and prints its JSON line; no files, no checks.
- Writes `fights.jsonl`, `hq_floor.jsonl` and `fights.md` to `--out` (default `rts/build/harness/fights`). stdout: `fights <done>/<total>` progress, one `CHECK <name> PASS|FAIL <detail>` per design-rule check, then
  `FIGHTS fights=<n> decided=<n> draws=<n> stalemates=<n> unresolved=<n> mismatched=<n> checks_passed=<n> of=<m> dome_absorbed=<n>`. A stalemate with reason `dome_absorbed` had dome-absorbed shots inside the idle window (skirmish_ai_v4.md S.4); `no_damage` had none.
- Exit codes: 0 done (failed checks included), 1 error, 2 a check failed and `--strict` was given, 3 spot check mismatch.
- The checks (`FightChecks`) are the P-005 section 1.6 rules 1 to 6 plus `matchups_agree`; the HQ floor (rule 6) reuses `HqFloorTrial`, extended to compositions.
- A new unit joins the sweep by being a combat unit (mobile, supplied, armed, not a worker or HQ); `matchups_agree` then fails until it has an entry in `matchups.json`.

## Ladder mode and response mode (skirmish AI v3, package A)

Spec: `rts/docs/specs/skirmish_ai_v3.md` A.1, A.2 and A.4. Settings: `rts/data/scripts/ai_eval/ai_eval.json` (read by `AiEvalSettings`; unknown keys and bad values are errors naming the file and the field).

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --ladder --profile-a <baseline id> --profile-b <candidate id>
    [--games N] [--maps a,b] [--matchups x,y] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>] [--non-regression]
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --response --profile <id> [--only <script file name>]
```

- `--ladder` plays profile A (baseline) against profile B (candidate) on every selected map and matchup, in both seats (game g: slot s plays A when `(g / 2) % 2 == s`, else B; factions swap on odd g), in the same worker processes as `--batch`.
  It writes `ladder.jsonl` and `ladder.md` to `--out` (default `rts/build/harness/ladder`) and prints `LADDER candidate=<id> baseline=<id> decided=<n> wins=<n> fraction=<0.000> distinct=<n> mirror=<wins>/<decided> cross=<wins>/<decided> bar=<0.000> PASS|FAIL`.
  Every game record carries `play_signature` (hash of every AI command, independent of the seed); `ladder.md` counts distinct games, puts Wilson intervals over them, splits mirror and cross-faction rows, and warns (never gates) when a cell's distinct share is below `ladder_min_distinct_fraction` in `ai_eval.json` (skirmish_ai_v4.md S).
  Every game record also carries `opening0` and `opening1` (the opening each slot's AI drew, "" for a profile without openings; skirmish_ai_v4_rl.md H.1); when any record has one, `ladder.md` gets a `## Openings` section (draws, use share, mirror games against other openings, wins, distinct games, Wilson interval, cross games) and `WARNING opening ...` lines for an opening drawn too often or winning too many or too few mirror games (thresholds `opening_*` in `ai_eval.json`); with `--non-regression` a `WARNING mirror: ...` line appears when the mirror row's Wilson upper bound over distinct games is below `ladder_mirror_upper_min`. None of them changes the exit code.
  Draws count as decided games and not as wins. Exit codes: 0 pass, 1 error, 2 bar missed or too few decided games, 3 determinism spot check failed (`ladder_verify_games` games re-run in-process).
  Every record line carries `profile_slot0` and `profile_slot1`.
- `--response` plays each scenario of `ai_eval.json` `response_tests` (a script in `rts/data/scripts/ai_eval` whose slot 1 is a responder profile `rt_*`, slot 0 the profile under test) with a read-only observer and prints
  `RESPONSE <script> <check> PASS|FAIL p<n>=PASS|FAIL <metric>=<value> ...` per scenario and `RESPONSE_SUMMARY passed=<n> of=<m>`; exit 0 all passed, 1 error, 2 any failed.
  Part 1 of `air_answer` reads the enemy model of the AI under test (`AiMemory.ModelAirKnownTick`, skirmish AI v3 package B); a profile with `use_enemy_model` false (such as `aggressive`) has no model, so it always fails there (`model_air_known_s=none`).

## Paired ladder, stated power and the fight predictor (specs/balance_measurement.md, M.1, M.2, C)

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --ladder --paired --profile-a <baseline id> --profile-b <candidate id> [--games N] [--maps a,b] [--matchups x,y] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>]
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --predict-fights [--from <fights.jsonl>] [--profile <id>] [--jobs J] [--seed S] [--out <dir>]
```

- **`--paired`** (with `--ladder`, a ladder worker or `--trace --profile-a --profile-b`; an error with `--non-regression`) changes the seat layout: game g of a cell is in pair `p = g / 2`, the candidate (profile B) plays slot `g % 2`, the factions swap on odd pairs and both games of a pair share `SeedDerivation.GameSeed(base, map, matchup, p)`. `--games` stays a multiple of 4.
  A paired record ends with `"pair":<p>,"candidate_slot":<0|1>`; unpaired records have no such keys. Everything that needs the candidate's slot reads it from the record (`LadderTally.CandidateSlot(GameRecord)`), and workers, the spot check and `--trace` all play through `GameRunner.PlayLadder`.
- **What a pair means.** Candidate won both games = W, baseline won both = L, one win each (the same slot won both) = S "concordant", any draw or undecided game = incomplete (left out and counted). Equal pair signatures (FNV-1a over the two play signatures) count once. The exact two-sided sign test, the Clopper-Pearson interval on W / (W + L) (grid of 1/1000), the advantage and the minimum detectable effect are integer arithmetic (`PairedStats`, `BigInteger`), so reports are the same on every machine. Settings `effect_alpha`, `effect_z_alpha`, `effect_z_power` and `paired_min_complete_fraction` are in `scripts/ai_eval/ai_eval.json`.
- **Pairing kind** per faction, printed in the report and on the last line: `full` when both profiles have the same openings, the same units in every composition and equal jitter settings (then each slot draws the same opening, band steps and expansion offset in both games), `seed` otherwise (only the random streams are shared).
- `ladder.md` gains a `## Pairs` table (per cell, then `mirror`, `cross`, `all`: pairs, complete, distinct complete n, S, W, L, plain counts, discordant share r, openings held, p, advantage with interval, MDE). The verdict uses the `all` row.
- Last stdout line: `PAIRED candidate=<id> baseline=<id> pairs=<n> complete=<n> distinct=<n> concordant=<S> candidate_both=<W> baseline_both=<L> p=<0.0000> advantage=<+x.x> interval=[<+x.x>,<+x.x>] mde=<x.x> pairing=<faction>:<full|seed>,... verdict=<PASS|FAIL|NO_MEASURABLE_EFFECT|INCOMPLETE>`. Exit codes: 0 PASS, 2 FAIL or INCOMPLETE (complete pairs below `paired_min_complete_fraction`), 3 spot check failed, 4 NO_MEASURABLE_EFFECT (not significant: effects below the printed MDE could not have been seen), 1 error.
- An unpaired `--ladder` prints one information-only line just before the `LADDER` line (and repeats it in `ladder.md`); the exit code stays bar-based: `EFFECT candidate=<id> baseline=<id> games=<n> wins=<w> p=<0.0000> advantage=<+x.x> interval=[<+x.x>,<+x.x>] mde=<x.x> verdict=<PASS|FAIL|NO_MEASURABLE_EFFECT>` (n = distinct games with a winner). Records, `ladder.jsonl` and the `LADDER` line are unchanged.
- **`--predict-fights`** scores `FightPredictor` against measured fights (spec C): with `--from <fights.jsonl>` it reads the records, without it it runs the `--fights` sweep exactly as `--fights` does (same files) and then evaluates. It predicts each fight from its two armies, counts decided fights for the headline accuracy (stalemates, unresolved fights and draws are scored apart: they agree when the predictor says "none"), and writes `predict.jsonl` and `predict.md` (accuracy by unit pair, side, terrain case, army value and unmodelled mechanic, the calibration table of the predicted margin, the no-winner table, and the decision of the proposed rule: SIMULATE, FIX_FORMULA or DEVELOPER_CHOICE with the thresholds beside the measured values) to `--out` (default `rts/build/harness/fights`). Settings: `scripts/fights/predict.json` (outside the content digest). Last line: `PREDICT fights=<n> judged=<n> decided=<n> correct=<n> accuracy=<0.000> clean=<correct>/<decided> no_winner_agree=<a>/<b> decision=<...> (proposed thresholds)`. Exit 0 (any decision), 3 spot check of a sweep, 1 error. A report is in `rts/docs/reports/predict_fights/`.

## Opponent pool, behaviour checks and the balance report (specs/balance_measurement.md, M.3 to M.5)

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --pool --profile-b <candidate> [--stage 1|2|all] [--pairs N] [--matchups x,y] [--maps a,b] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>]
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a <baseline> --profile-b <candidate> [--checks id,id] [--games N] [--maps a,b] [--matchups x,y] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>]
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --balance [--profile <id>] [--games N] [--maps a,b] [--matchups x,y] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>] [--from <balance.jsonl>] [--fights-from <fights.jsonl>] [--strict]
```

- **`--pool`** plays the candidate (`--profile-b`) as a paired ladder (see above) against every member of `scripts/ai_eval/pool.json` of the chosen stage (default 1), all chunks of all members in one worker pool. A member is a profile, optionally with one forced opening per faction (`AiProfile.WithForcedOpenings`, the only change under `rts/src/Ai`: a copy whose plan has that one opening at weight 1; no AI file and no play changes). A member that is the candidate's own profile without forced openings is skipped. Workers get `--pool-member <id>`; records carry `"member":"<id>"` and name the baseline side by the member ID. Pairs per cell come from `stage1_pairs_per_cell` / `stage2_pairs_per_cell` (`--pairs` overrides, even); the pool plays mirror cells only (`pool_matchups`) while the cross cell is decided by faction. The gate (`PoolGate`) classifies each member from its `all` row: **worse** (significant at `effect_alpha`, W below L), else **incomplete** (complete pairs below `paired_min_complete_fraction`), else **better** (W above L, significant at `effect_alpha` divided by the members run when `better_bonferroni` is on), else **no_effect**; the pool verdict is FAIL if any member is worse, else INCOMPLETE, else PASS if any is better, else NO_MEASURABLE_EFFECT. Writes `pool.jsonl` and `pool.md` (default `rts/build/harness/pool`); stdout has one `MEMBER <id> complete=...` line per member (the PAIRED line fields) and the last line `POOL candidate=<id> stage=<s> members=<run> skipped=<n> better=<n> worse=<n> no_effect=<n> incomplete=<n> games=<records written> verdict=...`. Exit codes as the paired ladder (0 PASS, 2 FAIL or INCOMPLETE, 3 spot check, 4 NO_MEASURABLE_EFFECT).
- **Game summary.** `--behaviour` and `--balance` games run a read-only `SummaryObserver` (spending through the shared `PlayerLedger`, bank at `summary_bank_times_seconds`, income per `summary_bucket_seconds` bucket, HQ moves, units trained, structures completed, mechanic uses from `MechanicDetectors`, own workers at each bank time (`workers`, null after the game ended) and the completion tick of each non-HQ drop-off (`drop_off_ticks`); older records without the last two parse as empty); the record gets `"summary0"` and `"summary1"`. The observer never submits or steps, so the state hash and play signature are those of the unobserved game.
- **Production and defence metrics** (spec `rts/docs/specs/ai_production_defence.md` section 7). With the settings of `ai_eval.json` (`summary_window_seconds`, `summary_sample_seconds`, `fed_window_seconds`, `raid_radius_tiles`, `bank_mean_from_seconds`) the summary observer also runs `ProductionMetricsCollector` and appends `worker_losses`, `worker_fed_losses`, `raid_seconds`, `raid_unanswered_edge_seconds`, `producers`, `capacity`, `income_60`, `army_supply` (sample arrays, null after the game ends) and `bank_mean` to each slot summary; `balance.md` gets a "Production and defence metrics" section with per-cell medians. Measurement only: it reads everything the harness sees and never reaches a controller. An observer built without settings writes none of these keys.
- **`--behaviour`** plays the paired layout with the summary observer (`games_per_cell` of `scripts/ai_eval/behaviour_checks.json`), evaluates every enabled check (`--checks` narrows) for both profiles and prints `BEHAVIOUR <check> profile=<id> games=<slot-games> held=<n> violated=<n> unjudged=<n> metric_median=<0.000> metric_max=<0.000> bar=<0.000> PASS|FAIL [extra]` per check and profile (candidate first), then `BEHAVIOUR_SUMMARY candidate=<id> passed=<n> of=<m>`. Checks are registered by ID in `BehaviourChecks.cs` and declared in the data file (`static_defence_share`, `hq_move_new_income`, `workers_at` (own workers at one bank time) and `first_drop_off` (second of the first non-HQ drop-off), specs/ai_budget_queues.md B.4.1); an entry with an unknown ID is an error, a registered check without an entry is listed as "not declared" in `behaviour.md` and not run. Exit 0 every candidate check passes, 2 any fails, 3 spot check. Writes `behaviour.jsonl` and `behaviour.md`.
- **`--balance`** plays one profile in both slots (`scripts/ai_eval/balance_report.json`), adds the fight predictor's accuracy (the `--fights` sweep, or `--fights-from`) and writes `balance.jsonl` (meta line, games, predictor line) and `balance.md`: faction matrix, opening matrices, unit use, mechanic use, unspent resources and the four trust checks (`balance_use`, `balance_predictor`, `balance_unspent`, `balance_openings`; every bar is a placeholder in the data file). stdout: four `CHECK balance_<name> PASS|FAIL|NOT_MEASURED <detail>` lines and `BALANCE profile=<id> games=<n> decided=<n> distinct=<n> checks_passed=<k> of=4`. `--from <balance.jsonl>` re-renders the report without playing, byte for byte. Exit 0 (failed checks included), 2 a check failed and `--strict` was given, 3 spot check.

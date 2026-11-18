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
and writes a readable timeline: every 30 game-seconds one line per player (bank, income, supply, workers, structures, army, trained and lost,
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

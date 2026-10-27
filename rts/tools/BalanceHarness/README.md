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
- Command `type` values: `noop`, `spawn_unit` (`definition`, `owner`, `x`, `y`; script issuer only), `move_unit` (`unit`, `x`, `y`), `remove_unit` (`unit`), `attack_move` (`unit`, `x`, `y`), `attack_unit` (`unit`, `target`), `stop` (`unit`), `root_hq` and `uproot_hq` (`unit`), `gather` (`unit`, `field`: index in the map's `resource_fields`), `train_unit` (`producer`, `definition`) and `build_structure` (`unit`: the worker, `definition`, `x` and `y` as whole tile numbers). `cast_wave` (`unit`: the caster, `x`, `y`: the point the wave heads towards) casts the Water wave; `rts/data/scripts/flood_smoke.json` exercises flood sources and the flood limits. `rts/data/scripts/economy_smoke.json` exercises the HQ and economy commands on `greybox_1v1`.
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

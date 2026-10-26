<!-- rts/data/README.md: what each game data folder holds and the format every data file follows (JSON with // comments, documented values, $schema_version). -->
# Game data

All tuning and content lives here, never in code (architecture rule 3). Files are JSON with `//` and `/* */` comments, read by the data loader in `rts/src/Sim/Data` (ADR-002).

- `tuning/`: one file per system (match, flood, supply, statuses, momentum, earth, hq, harness, online). Read at match start.
- `units/<faction>/`: one file per unit, for example `units/skai/windrunner.json`.
- `factions/`: one file per faction: its mechanic ID and its unit list.
- `maps/`: one file per map: its size in tiles and its two to four spawn points.
- `cycles/`: one file per Planetary Cycle type: what it favours.
- `scripts/`: scripted command streams (seed, players, tick count and commands), for example `scripts/smoke.json`. The headless harness (its default script), the Unity bootstrap (`SimulationHost`) and later campaign missions play them; they are parsed by `Rts.Sim.Data.CommandScript`, not by the data loader, which skips this folder. See `rts/tools/BalanceHarness/README.md` for the format.

Every file has `"$schema_version"` (currently 1). Every value has a comment line above it that gives what it controls, its unit, its valid range and its design-doc source.

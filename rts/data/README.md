<!-- rts/data/README.md: what each game data folder holds and the format every data file follows (JSON with // comments, documented values, $schema_version). -->
# Game data

All tuning and content lives here, never in code (architecture rule 3). Files are JSON with `//` and `/* */` comments, read by the data loader in `rts/src/Sim/Data` (ADR-002).

- `tuning/`: one file per system (match, flood, supply, statuses, momentum, earth, hq, harness, online, combat). Read at match start. Every tuning file is required.
- `units/<faction>/`: one file per unit, for example `units/skai/windrunner.json`. Combat keys, all optional: `armour_type` (defaults to `default_armour_type` in `tuning/combat.json`), `sight_range_tiles` (default 8; stored for fog of war) and a `weapon` object (`damage`, `damage_type`, `range_tiles`, `cooldown_seconds`, `can_target`). A unit without `weapon` is unarmed (workers).
- `factions/`: one file per faction: its mechanic ID and its unit list.
- `maps/`: one file per map: its size in tiles and its two to four spawn points.
- `cycles/`: one file per Planetary Cycle type: what it favours.
- `scripts/`: scripted command streams (seed, players, tick count and commands), for example `scripts/smoke.json`. The headless harness (its default script), the Unity bootstrap (`SimulationHost`) and later campaign missions play them; they are parsed by `Rts.Sim.Data.CommandScript`, not by the data loader, which skips this folder. See `rts/tools/BalanceHarness/README.md` for the format.

Every file has `"$schema_version"` (currently 1). Every value has a comment line above it that gives what it controls, its unit, its valid range and its design-doc source.

## Combat tuning (`tuning/combat.json`)

The combat system (`rts/src/Sim/Combat`) reads this file through `CombatTuning` and `CombatRules`:

- `damage_types` and `armour_types` list the names units and weapons may use. A unit's `armour_type` and a weapon's `damage_type` must be in these lists; the loader names the unit file and field when they are not.
- `multipliers` is the damage table: for each damage type, a fraction for each armour type (0 to 3, soft counters only). Final damage is `weapon.damage x multiplier x` any mechanic damage modifiers (for example Momentum later).
- `acquire_range_bonus_tiles` is the extra distance, beyond weapon range, in which an attack-moving unit notices and chases an enemy.
- `default_armour_type` is the armour type of a unit whose file names none.

Weapon `range_tiles` is measured between unit edges (centre distance minus both radii); 1.5 or less counts as melee. `cooldown_seconds` becomes whole ticks (at least 1; 0.8 s is 16 ticks at 20 ticks per second). `can_target` lists the movement layers (`ground`, `water`, `air`) the weapon may hit. All numbers are placeholders for playtesting.

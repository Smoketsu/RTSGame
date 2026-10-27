<!-- rts/data/README.md: what each game data folder holds and the format every data file follows (JSON with // comments, documented values, $schema_version). -->
# Game data

All tuning and content lives here, never in code (architecture rule 3). Files are JSON with `//` and `/* */` comments, read by the data loader in `rts/src/Sim/Data` (ADR-002).

- `tuning/`: one file per system (match, flood, supply, statuses, momentum, earth, hq, harness, online, combat, terrain, economy). Read at match start. Every tuning file is required.
- `units/<faction>/`: one file per unit, for example `units/skai/windrunner.json`. Combat keys, all optional: `armour_type` (defaults to `default_armour_type` in `tuning/combat.json`), `sight_range_tiles` (default 8; stored for fog of war) and a `weapon` object (`damage`, `damage_type`, `range_tiles`, `cooldown_seconds`, `can_target`). A unit without `weapon` is unarmed (workers). Economy keys, all optional: `build_seconds` (train or construct time, 0.05 to 3600, default 10), `can_gather` (a worker; gathers and places structures), `is_drop_off` (workers deposit here once it is complete), `trains` (unit IDs it can produce), `is_static_defence` (costs `static_defence_supply_cost` and counts against the per-base cap), `footprint` (0 or 1 tiles blocked while it stands) and `provides_supply` (added to the supply cap while complete). An `hq` object (`is_hq`, `rooted_armour_type`, `uprooted_armour_type`, `worker_unit`) makes a unit a mobile HQ.
- `factions/`: one file per faction: its mechanic ID, its unit list and `hq_unit`, the unit a host or script spawns as the player's HQ (required for a faction that lists units, absent for neutral; it must belong to the faction and have an `hq` object).
- `maps/`: one file per map: its size in tiles and its two to four spawn points. Optional `quake_paths` (up to 16 groups of 1 to 64 tiles with an `id` and `closed_outside_quake`) are walled or opened by a Quake cycle; no path tile may hold a resource field.
- `cycles/`: one file per Planetary Cycle type: what it favours and an `effects` object (air speed multiplier, surface ranged range bonus, defence damage rate and floor, quake path toggle, Core damage multiplier and radius), every key required. At least two play styles must exist so the no-repeat rule can always pick.
- `ai/`: one file per skirmish AI profile (`ai/aggressive.json`, `ai/defensive.json`): decision timing, economy targets, build placement, attack, staging, focus fire, retreat and defence numbers, and a plan per faction (worker, supply building, build order, army composition, tide window). The loader checks the header and that `id` matches the file name and keeps the parsed file in `DataSet.AiProfiles`; `Rts.Ai.AiProfile.Parse` (rts/src/Ai) validates the rest. Script player entries name a profile with `"ai"`.
- `scripts/`: scripted command streams (seed, players, tick count and commands), for example `scripts/smoke.json` (units and combat) and `scripts/economy_smoke.json` (HQ root and uproot, gather, train, build) and `scripts/flood_smoke.json` (Water flood sources, tides and the flood limits next to a Skai drop-off) and `scripts/ai_vs_ai.json` (the Phase 1 exit match: two skirmish AIs from tick 0, each player entry naming its AI profile with `"ai"`). The headless harness (its default script), the Unity bootstrap (`SimulationHost`) and later campaign missions play them; they are parsed by `Rts.Sim.Data.CommandScript`, not by the data loader, which skips this folder. See `rts/tools/BalanceHarness/README.md` for the format.

## Match tuning for cycles and the Core (`tuning/match.json`)

`planetary_cycle_interval_seconds` (240) and `planetary_cycle_warning_seconds` (60, must be shorter than the interval) set the shift and lock ticks; `core_capture_seconds` (20) is the uncontested time to capture the Core; `ffa_core_win_cumulative_seconds` (480) is the hold time that wins, when the match has at least `core_win_min_teams` (3) teams; `quake_path_relocate_radius_tiles` (3) is how far a ground unit is moved off a quake-path tile that closes.

## Line endings

The loader turns CRLF and a lone CR into LF before it parses or hashes any file, so a Windows checkout gives the same content digest and state hashes as a Unix one. `.gitattributes` also keeps `rts/data` at LF.

Every file has `"$schema_version"` (currently 1). Every value has a comment line above it that gives what it controls, its unit, its valid range and its design-doc source.

## Combat tuning (`tuning/combat.json`)

The combat system (`rts/src/Sim/Combat`) reads this file through `CombatTuning` and `CombatRules`:

- `damage_types` and `armour_types` list the names units and weapons may use. A unit's `armour_type` and a weapon's `damage_type` must be in these lists; the loader names the unit file and field when they are not.
- `multipliers` is the damage table: for each damage type, a fraction for each armour type (0 to 3, soft counters only). Final damage is `weapon.damage x multiplier x` any mechanic damage modifiers (for example Momentum later).
- `acquire_range_bonus_tiles` is the extra distance, beyond weapon range, in which an attack-moving unit notices and chases an enemy.
- `default_armour_type` is the armour type of a unit whose file names none.

Weapon `range_tiles` is measured between unit edges (centre distance minus both radii); 1.5 or less counts as melee. `cooldown_seconds` becomes whole ticks (at least 1; 0.8 s is 16 ticks at 20 ticks per second). `can_target` lists the movement layers (`ground`, `water`, `air`) the weapon may hit. All numbers are placeholders for playtesting.

## HQ and economy tuning

- `tuning/hq.json` (`HqTuning`, read by `Units/HqSystem.cs`): `root_seconds`, `uproot_seconds`, the Last Stand switch and values (`last_stand_enabled` is false by default), and `hq_root_max_distance_to_field_tiles`, how close to the centre of a field that still has resources an HQ must stand to root (also decides whether a script-spawned HQ starts Rooted or Uprooted). Switching times are counted in ticks from the command's own tick.
- `tuning/economy.json` (`EconomyTuning`, read by `Economy/EconomySystem.cs` and the economy commands): `starting_common`, `starting_rare`, `base_supply`, the gather trip (`gather_amount_per_trip`, `gather_seconds_per_trip`, `worker_speed_multiplier_while_carrying`, `interact_range_tiles`), `max_queue_length`, `base_radius_tiles` (the radius around a drop-off that counts as one base for the static defence cap) `construction_start_health_fraction`, `spawn_search_radius_tiles` (how many rings around a producer are searched for the tile a trained unit appears on) and `starting_worker_count` (workers beside the HQ in `Rts.Sim.Data.StandardMatchStart`).
- `tuning/supply.json`: `supply_cap` (the global limit), `static_defence_supply_cost` and `static_defences_per_base_max`.

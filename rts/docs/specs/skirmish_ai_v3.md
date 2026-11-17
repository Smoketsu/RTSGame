<!-- rts/docs/specs/skirmish_ai_v3.md -->
<!-- Build spec for stages 1 to 3 of proposal P-004 (approved by the developer 17 Nov 2026): harness ladder and response tests plus the Reef Runner water scout (package A), scouting and the enemy model (B), fight predictor, counter-mix, tech on demand, static defence and rare priority (C), the counter-mix revision after the package C review: predictor-scored mix, confidence from sightings, raid strength and value cap for static defence, rare-worker cap and the early expansion as its own switch (C2), predictor-driven attacks, target choice, measured defence and unit control (D). Extends rts/docs/specs/skirmish_ai_v2.md and the rules in the header of rts/src/Ai/AiBrain.cs. Part of rts/docs/specs (index: README.md). -->
# Spec: Skirmish AI v3 (know the enemy, build the right army, choose fights)

## 0. Goal and ground rules

Goal (P-004 stages 1 to 3): an AI that scouts, models what the enemy has, builds counters computed from unit data, and fights only when a prediction says it wins. One strong AI; difficulty comes later by lowering profile numbers, so every strength-related number is a profile key.

Ground rules (all packages):

- **No change to `rts/src/Sim`.** Everything needed is public already: `UnitDefinition` (weapon, armour, layer, speed, health, `ShieldDome.Pool`, `Economy.Trains`, `Economy.IsStaticDefence`, `Economy.IsDropOff`), `ContentCatalog.Combat` (`GetMultiplier`, `UphillRangedDamageMultiplier`), `UnitKilledEvent`. If a builder finds a Sim change unavoidable, stop and report to the coordinator with the reason.
- The AI stays a pure function of `AiWorldView` + `AiProfile` + `AiMemory` + its own `DeterministicRandom`. No new random draws (the only draw stays the attack-threshold jitter). Fix64 and integers only; collections iterated in ascending entity ID, field index, spawn index or ordinal unit ID; distances compared squared; every tie goes to the lower ID or index unless a rule says otherwise. AI memory is not hashed; determinism is proven by harness hashes.
- Commands: only those a player can issue (`gather`, `train_unit`, `build_structure`, `move_unit`, `attack_move`, `attack_unit`, `root_hq`, `uproot_hq`, `pick_cycle`). No new command or event types.
- Knowledge: own units, `VisibleEnemies`, `IsVisible`, public map data (terrain, spawns, fields, start amounts), unit definitions, and `UnitKilledEvent`s whose victim was in the previous think's visible set. Never a fogged unit.
- **The core AI code never names a faction or unit ID.** Unit IDs appear only in profile data. The predictor and counter table are computed from `UnitDefinition` and `ContentCatalog.Combat`.
- Every number is a profile key (`rts/data/ai/*.json`) or an `ai_eval.json` key (A.2), commented in the house format `// <what>. Unit: . Range: . Source: skirmish_ai_v3.md <section>; placeholder.` Every new value is a **placeholder**.
- `AiProfile.Parse` reads every new key with the `Require*` helpers and adds it to `RejectUnknownKeys`; every profile file under `rts/data/ai` gets every new key (switches off unless stated). Update the `AiProfile.cs` "Differences" header list and the `AiBrain.cs` header per package.
- Build order: A, B, C, D. Each package ends with `dotnet build` and `dotnet test` green, a `sim-reviewer` pass on `rts/src/Ai` and the harness changes, and its gate run (E).

### 0.1 Versions by profile (how old and new behaviour coexist)

One code base. Each new behaviour sits behind a boolean profile switch; with every switch false the AI plays exactly as v2 (test A-11 pins this). Profile files:

| File | Switches on | Made in |
| --- | --- | --- |
| `aggressive.json`, `defensive.json` | none (v2 behaviour, unchanged) | keys added in B, C, D |
| `aggressive_s1.json` | stage 1 | B |
| `aggressive_s2.json` | stages 1 and 2 | C |
| `aggressive_s2b.json` | stages 1 and 2 with the C2 revision (C2.7) | C2 |
| `aggressive_s3.json` | stages 1 to 3 | D |
| `rt_*.json` (5 responder profiles) | none | A |

`aggressive_sN.json` is a copy of `aggressive.json` with its switches set and its `id` changed; all non-switch values equal to `aggressive.json` unless this spec states otherwise (C.8 base compositions). Switch dependencies are validated (error names the file and both fields): `use_counter_mix`, `use_tech_on_demand`, `use_static_defence`, `use_rare_priority` and `use_predictor_attack` require `use_enemy_model`; `use_tech_on_demand` and `use_rare_priority` require `use_counter_mix`.

### 0.2 Existing rules replaced (only while the named switch is on; say so in the `AiBrain.cs` header)

1. Rule 5 (P) and 5b (X): the plan's `composition` is replaced by the counter-mix weights (C.4) when `use_counter_mix`. The P loop and `PickByDeficit` stay.
2. Rule 6 Massing trigger "army supply >= drawn threshold": replaced by the predictor test D.1 when `use_predictor_attack`; the drawn threshold stays as the fallback when nothing is known.
3. `ShouldRetreat`'s power test (`retreat_power_ratio`): replaced by D.2 when `use_predictor_attack`. `retreat_army_fraction` stays.
4. `TryChooseTarget`: tiers D.3 are tried before the existing order when `use_target_choice`. Rule K still comes first.
5. `DefendIfThreatened` (every army unit defends; `defend_recall_supply`): replaced by D.5 when `use_measured_defence`.
6. `Focus` (nearest enemy): replaced by D.6.3 (fastest kill) when `use_unit_control`.
7. Army membership: the scout (B.2), fallen-back units (D.6.1), the defend group (D.5) and the harass group (D.4) are excluded from stance orders.

## Package A: harness support and the Reef Runner

### A.1 Per-slot profiles and ladder mode (`rts/tools/BalanceHarness`)

- `GameRunner.Play(..., string profileSlot0, string profileSlot1, ...)` replaces the single `aiProfile` parameter; batch and trace pass the batch profile twice. `GameRecord` gains `profile_slot0` and `profile_slot1` (JSON line fields after `faction_slot1`). The trace header names both.
- Seat rule for a ladder game index g (0 upward): slot `s` plays profile A when `(g / 2) % 2 == s`, else B. With the existing faction rule (`FactionInSlot`: factions swap on odd g) games g..g+3 cover every seat and faction combination. `ladder_games_per_cell` must be a multiple of 4.
- New mode:

```
--ladder --profile-a <baseline id> --profile-b <candidate id> [--games N] [--maps a,b] [--matchups x,y] [--jobs J] [--seed S]
         [--max-game-seconds T] [--out <dir>] [--non-regression]
```

  Plays every (map, matchup, game) cell like `--batch` (same worker processes and chunking; the worker command line gains `--profile-a` and `--profile-b`; seeds from the batch seed derivation with `ladder_base_seed`). Writes `ladder.jsonl` (one `GameRecord` per game) and `ladder.md` to `--out` (default `rts/build/harness/ladder`): per cell, per map, per seat and overall: games, decided, candidate wins, baseline wins, draws, undecided, candidate fraction of decided games with the batch's Wilson interval (`confidence_z`). Last stdout line: `LADDER candidate=<id> baseline=<id> decided=<n> wins=<n> fraction=<0.000> bar=<0.000> PASS|FAIL`.
- Pass: decided games `>= ladder_min_decided` and candidate wins / decided `>= ladder_candidate_win_fraction_min` (with `--non-regression`: `>= ladder_non_regression_fraction_min`). Draws count as decided and not as wins. Exit codes: 0 pass, 1 error, 2 bar missed or too few decided, 3 determinism spot check failed (re-run `ladder_verify_games` games in-process, compare final hashes).
- `--games`, `--maps`, `--matchups`, `--seed`, `--max-game-seconds` override the `ai_eval.json` values (same validation).

### A.2 Settings file `rts/data/scripts/ai_eval/ai_eval.json` (read by the harness only)

Lives under `scripts/` because `DataLoader` skips that folder (no Sim change, outside the content digest); in a subfolder because CI runs every `rts/data/scripts/*.json` as a command script. Parsed with `JsonValue` and the `Require*` helpers; unknown keys rejected; errors name `scripts/ai_eval/ai_eval.json` and the field.

| Key | Value | Unit | Range |
| --- | --- | --- | --- |
| `ladder_base_seed` | 20261117 | seed | 0 to 2147483647 |
| `ladder_games_per_cell` | 24 | games | 4 to 1000, multiple of 4 |
| `ladder_maps` | `[]` | map IDs | 0 to 32; empty = every map with exactly 2 spawns, ordinal order |
| `ladder_matchups` | `["skai_vs_skai", "skai_vs_water", "water_vs_water"]` | matchup IDs | 1 to 16 |
| `ladder_max_game_seconds` | 2400 | seconds | 60 to 7200 |
| `ladder_min_decided` | 200 | games | 1 to 100000 |
| `ladder_candidate_win_fraction_min` | 0.65 | fraction | 0 to 1 (developer decision, 17 Nov 2026) |
| `ladder_non_regression_fraction_min` | 0.45 | fraction | 0 to 1 (Q1) |
| `ladder_verify_games` | 2 | games | 0 to 16 |
| `response_tests` | list, A.4 | objects | 1 to 64 |

Four 2-spawn maps x 3 matchups x 24 = 288 games, so 200 decided games are reachable with 30 percent undecided.

### A.3 Reef Runner (new unit) `rts/data/units/water/reef_runner.json`

ID `water.reef_runner`, display name `"Reef Runner"` (named by the developer 17 Nov 2026; the creature is a seahorse riding a single wheel, concept sheet `rts/docs/art/concept/turnarounds8/water_scout_sheet.jpg`). Role: the water counterpart of the Windrunner (cheap, fast, light, weak scout), armed with a needle launcher in its snout: a light ranged needle shot. Copy the structure and comment style of `skai/windrunner.json`; the `display_name` comment cites the developer's naming (17 Nov 2026); every tuning comment ends `Source: P-004 / skirmish_ai_v3.md A.3 (water scout, counterpart of skai.windrunner); placeholder.`; `$doc`, the line-1 header and the weapon comment say it is the water scout, a seahorse on a single wheel firing light needle shots from its snout.

| Key | `skai.windrunner` | `water.reef_runner` | Note |
| --- | --- | --- | --- |
| `layer` | ground | **water** | see below |
| `max_health` | 60 | 60 | |
| `speed_tiles_per_second` | 3.5 | **4.1** | dry ground: 4.1 x 0.85 = 3.485 vs 3.5 x 1.0 |
| effective speed dry / shallow / deep | 3.5 / slowed by enemy water | 3.485 / 4.715 / 5.33 | water rows of `terrain.json` |
| `supply` | 1 | 1 | |
| `radius_tiles` | 0.4 | 0.4 | |
| `cost_common` / `cost_rare` | 75 / 0 | 75 / 0 | |
| `build_seconds` | 18 | 18 | |
| `armour_type` | light | light | |
| `sight_range_tiles` | 9 | 9 | |
| weapon `damage`, `damage_type` | 6, kinetic | 6, kinetic | |
| `range_tiles`, `cooldown_seconds`, `hit_delay_ticks` | 4, 0.8, 2 | 4, 0.8, 2 | |
| `can_target` | ground, water, air | **ground, water** | Q2 |
| trained at | Roost | Tidepool | |

Layer: **water**. Checked in code: `TerrainGrid.IsStepPassable` treats ground and water layers identically (blockers and the height/ramp rule; only air differs), so a water-layer unit crosses a fully dry map; `TerrainGrid.SpeedMultiplier` gives water-layer units `water_unit_dry_speed_multiplier` (0.85) on dry tiles and 1.15 / 1.3 on shallow / deep water of any team; `FloodingMechanic.Heal` heals water-layer units on own water. Every weapon in the data lists both `ground` and `water`, so the layer changes nothing about who can hit it. The existing water units (`water.grasper`, `water.shellback`, `water.spoutling`, `water.ink_lobber`) already use `layer: water` and fight on dry maps. The base speed 4.1 makes its dry speed match the Windrunner's.

Data edits: `water/tidepool.json` `trains` becomes `["water.spoutling", "water.grasper", "water.reef_runner"]` (comment updated); `factions/water.json` `units` gets `"water.reef_runner"` right after `"water.tender"` (cheapest units first, as Skai lists the Windrunner after the Fledgling).

### A.4 Response tests (scripted opponents as data)

A scenario = a command script `rts/data/scripts/ai_eval/response_<name>.json` (existing `CommandScript` format, players with `"ai"`) plus an entry in `ai_eval.json` `response_tests`. The opponent (slot 1) is a responder AI profile; timed `spawn_unit` commands (issuer 255) give it units, which its AI then orders (so no script needs entity IDs of AI-trained units). Slot 0 is the AI under test: its `"ai"` value in the file is `aggressive`, and the runner replaces it with `--profile`. Map `greybox_1v1`; setup copies `ai_vs_ai.json` (slot 0 at (7, 7), slot 1 at (57, 57), four workers each); `ticks` = the entry's longest `*_seconds` x 20. Seed 1234.

Responder profiles (`rts/data/ai/rt_<name>.json`, copies of `aggressive.json`, all switches off, one faction plan only):

| Profile | Plan | Composition | Changed keys |
| --- | --- | --- | --- |
| `rt_roc_rush` | skai | `{ "skai.roc_rider": 1 }` | `attack_army_supply` 6, `attack_supply_jitter` 0, `stage_distance_tiles` 0 |
| `rt_mass_pummeler` | water | `{ "water.grasper": 1 }` | `attack_army_supply` 12, `attack_supply_jitter` 0 |
| `rt_mass_windrunner` | skai | `{ "skai.windrunner": 1 }` | `attack_army_supply` 10, `attack_supply_jitter` 0 |
| `rt_worker_raid` | skai | `{ "skai.windrunner": 1 }` | `attack_army_supply` 4, `attack_supply_jitter` 0, `stage_distance_tiles` 0, `rally_at_core` false |
| `rt_turtle` | water | `{ "water.shellback": 1 }` | `attack_army_supply` 200 (never attacks) |

Scenarios (each played with the AI under test as Skai and as Water: 10 files, suffix `_skai` / `_water`):

| Name | Opponent | Timed spawns (slot 1) | Check |
| --- | --- | --- | --- |
| `roc_rush` | skai, `rt_roc_rush` | 2 Roc Riders at (55, 55) at 150 s and 2 more at 210 s | `air_answer` |
| `mass_pummeler` | water, `rt_mass_pummeler` | none | `armoured_answer` |
| `mass_windrunner` | skai, `rt_mass_windrunner` | none | `trade` |
| `worker_raid` | skai, `rt_worker_raid` | 4 Windrunners at (18, 18) at 120 s and 4 more at 240 s | `raid` |
| `turtle` | water, `rt_turtle` | 4 Coral Turrets at 0 s on (53, 57), (57, 53), (53, 53), (60, 52) | `turtle` |

Spawn tiles must pass the AI's site rule (unblocked, not a ramp, HQ height, not occupied); if one fails, use the nearest tile that passes and write it in the file's comment. Spawns use the `spawn_unit` sequence after the setup sequences.

`response_tests` entries: `{ "script": "<file name>", "check": "<kind>", <parameters> }`. Parameters and what passes (all measured by the harness observer, which may read full state):

| Check | Parameters (value) | Pass when | First package that must pass |
| --- | --- | --- | --- |
| `air_answer` | `known_within_seconds` 30, `by_seconds` 270, `min_aa_units` 2, `survive_seconds` 600 | (1) the AI's enemy model reports air supply above 0 no later than `known_within_seconds` after an enemy air unit was first visible to the AI's team; (2) at `by_seconds` the AI owns at least `min_aa_units` complete non-worker, non-HQ units (static defences included) whose weapon can target air; (3) the AI's HQ is alive at `survive_seconds` | (1) B, (2) C, (3) D |
| `armoured_answer` | `delay_seconds` 30, `until_seconds` 600, `min_share_gain` 0.15, `survive_seconds` 600 | (1) answer share S of the army supply the AI trains between first sighting + `delay_seconds` and `until_seconds`, minus the same share S0 of its plan's base composition (weight x supply), is at least `min_share_gain`. An answer unit: its damage multiplier against the most-supplied seen enemy armour type is the highest in the AI's pool (C.4), or no seen enemy type can target its layer. (2) HQ alive at `survive_seconds` | (1) C, (2) D |
| `trade` | `until_seconds` 600, `min_trade_ratio` 1.0 | enemy army supply killed by the AI / max(1, own army supply lost), both in [0, `until_seconds`], is at least `min_trade_ratio`, and the HQ is alive at `until_seconds` | C |
| `raid` | `wave_seconds` [120, 240], `window_seconds` 60, `max_workers_lost` 3, `defence_by_seconds` 330, `defence_radius_tiles` 10 | (1) by `defence_by_seconds` the AI owns a static defence (any state) within `defence_radius_tiles` of its drop-off nearest the wave spawn point; (2) in each wave window [w, w + `window_seconds`] it loses at most `max_workers_lost` workers | (1) C, (2) D |
| `turtle` | `until_seconds` 2400, `max_supply_lost_before_first_kill` 16 | (1) army supply the AI loses before it destroys its first enemy structure is at most the limit; (2) the AI wins by `until_seconds` | D |

"Enemy model air supply" is read from the slot-0 controller's `AiMemory` (B.3). The observer is read-only (never submits, never steps); its final hash equals the unobserved run's.

Harness: `ResponseRunner` (new) runs one scenario through the single-run path (extract the script-to-simulation setup from `Program` into a reusable method if it is inline), `ResponseChecks` (new) holds one method per check kind. CLI:

```
--response --profile <id> [--only <script file name>]
```

prints `RESPONSE <script> <check> PASS|FAIL <metric>=<value> ...` per scenario and a final `RESPONSE_SUMMARY passed=<n> of=<m>`; exit 0 all passed, 1 error, 2 any failed. NUnit tests call the same `ResponseRunner`.

### A.5 Package A tests (`rts/tests/Sim.Tests/Harness/LadderTests.cs`, `ResponseTests.cs`; `Data/ReefRunnerTests.cs`)

- **A-1 Seat rule.** g = 0, 1, 2, 3: slot 0 plays A, A, B, B; with `skai_vs_water`, slot 0 faction skai, water, skai, water.
- **A-2 Same profile twice.** `GameRunner.Play` with (aggressive, aggressive) twice: identical `GameRecord` lines; the line holds both profile fields.
- **A-3 Ladder arithmetic.** Synthetic records: 210 decided, 140 candidate wins: fraction 0.667, PASS, exit 0; 150 decided: FAIL, exit 2; 200 decided, 129 wins: FAIL; with `--non-regression` 90 of 200: PASS. Draws are decided, not wins.
- **A-4 Ladder determinism.** 1 map, 1 matchup, 4 games, cap 120 s: `--jobs 1` and `--jobs 2` give byte-identical `ladder.jsonl`.
- **A-5 Settings validation.** `ladder_games_per_cell` 10: error naming `scripts/ai_eval/ai_eval.json` and `ladder_games_per_cell`; an unknown check kind: error naming `response_tests[0].check`.
- **A-6 Response runner.** `response_roc_rush_water.json` with `--profile aggressive_s1` (after B; use `aggressive` in A): slot 0's controller uses the given profile, slot 1 uses `rt_roc_rush`; observed and unobserved final hashes are equal.
- **A-7 Check arithmetic.** Synthetic series per check kind: one passing and one failing case each, including `trade` with 0 own losses (ratio uses max(1, lost)).
- **A-8 Reef Runner data.** Loads with layer Water, can_target ground and water only, cost 75/0, supply 1; the Tidepool trains it; `factions.water.units[1]` is `water.reef_runner`.
- **A-9 Reef Runner crosses a dry map.** greybox_1v1, a Reef Runner spawned at (8, 9) with `move_unit` to (55, 54): within 20 s x 20 ticks it stands within 1 tile of the goal (path length about 67 tiles at 3.485 tiles/s is about 19.3 s; if the measured path is longer, set the limit to measured + 2 s and say so).
- **A-10 Baseline report (not asserted).** Run `--response --profile aggressive` and put the PASS/FAIL table in the builder's report.
- **A-11 v2 reference hash.** After A.3's data edits and before any AI code change: run `ai_vs_ai.json` (aggressive vs defensive) and pin its `STATE_HASH` as a constant `V2ReferenceHash` in `SkirmishAiTests` with a comment. Packages B, C and D must keep this test green (all switches off reproduce v2).

Existing tests that change in A: `EconomyDataTests` Tidepool `Trains` (adds `water.reef_runner`); `DataLoaderTests` water `UnitIds` (adds `water.reef_runner` second) and `Content.Units.Count` 20 to 21; `HarnessBatchTests` assertions that compare whole `results.jsonl` lines (new profile fields); `Loader_RoutesAiFolder_AndShippedProfilesValidate` (five new profiles). Every `STATE_HASH` on shipped data changes (new unit file and Tidepool edit enter the content digest); `DeterminismTests.GoldenFinalHash` (TestWorld) must **not** change. The PC session repeats the Unity-versus-headless hash check and gives the Reef Runner a placeholder model.

## Package B: stage 1, know the enemy

### B.1 Profile keys

Root keys:

| Key | aggressive / defensive | `_s1` and later | Unit | Range | Meaning |
| --- | --- | --- | --- | --- | --- |
| `use_scouting` | false | true | boolean | | Rule S on. |
| `use_enemy_model` | false | true | boolean | | Enemy model updated and read. |
| `scout_first_seconds` | 40 | 40 | seconds | 0 to 600 | First scout is wanted from this time. |
| `scout_interval_seconds` | 90 | 90 | seconds | 10 to 900 | A new round starts this long after the last one started. |
| `scout_retry_seconds` | 30 | 30 | seconds | 0 to 600 | Wait after a scout dies. |
| `scout_max_losses` | 3 | 3 | units | 0 to 20 | After this many dead scouts no scout is trained (borrowed flyers still scout). |
| `scout_route_max_waypoints` | 8 | 8 | waypoints | 1 to 32 | Route length cap. |
| `scout_merge_tiles` | 8 | 8 | tiles | 0 to 64 | A field this close to an earlier waypoint adds no waypoint. |
| `scout_air_max_cost` | 250 | 250 | resource units | 0 to 100000 | An own air army unit with `cost_common + cost_rare` at or below this may be borrowed as scout; 0 never borrows. |
| `enemy_model_hold_seconds` | 30 | 30 | seconds | 0 to 600 | A sighting counts fully for this long. |
| `enemy_model_decay_seconds` | 120 | 120 | seconds | 1 to 3600 | Then it fades linearly to the floor over this long. |
| `enemy_model_floor_fraction` | 0.25 | 0.25 | fraction | 0 to 1 | Old sightings never count less than this share. |
| `enemy_model_implied_count` | 1 | 1 | units | 0 to 20 | Count implied for each unit type a known enemy structure trains. |

Per faction plan, optional `scout_unit` (`"skai.windrunner"`, `"water.reef_runner"` in every shipped profile that has the plan): must be a unit of the faction, mobile (footprint 0), not a worker, not an HQ, listed in the `trains` of some structure of the faction (errors name `factions.<id>.scout_unit`). Absent: the AI trains no scout (it may still borrow flyers).

### B.2 Rule S: scouting (rule 4c, after W, before P; only when `use_scouting`)

Memory (`AiMemory`): `ScoutId` (EntityId, None when no scout), `ScoutBorrowed` (bool), `ScoutRoute` (`List<FixVec2>`, at most `scout_route_max_waypoints`), `ScoutWaypoint` (int), `ScoutRoundStartTick` (int, -1 before the first), `NextScoutTick` (int), `ScoutLosses` (int).

1. **Death.** `ScoutId` set but not among own units: `ScoutLosses++`, `ScoutId` = None, `NextScoutTick = tick + SecondsToTicks(scout_retry_seconds)`.
2. **Wanted.** No scout, `tick >= SecondsToTicks(scout_first_seconds)`, `tick >= NextScoutTick`, and (`ScoutRoundStartTick < 0` or `tick >= ScoutRoundStartTick + SecondsToTicks(scout_interval_seconds)`).
3. **Assign** (when wanted), first match: (a) an own army unit with `Layer == Air`, cost at most `scout_air_max_cost`, not in the attack, defend or harass group, stance Massing; lowest supply, then lowest ID; `ScoutBorrowed = true`. (b) the lowest-ID own unit of type `scout_unit` not in those groups; `ScoutBorrowed = false`. Never a unit with `IsWorker`. On assign: build the route, `ScoutWaypoint = 0`, `ScoutRoundStartTick = tick`.
4. **Train** (wanted, nothing to assign, `scout_unit` set, `ScoutLosses < scout_max_losses`, no own unit of type `scout_unit` alive or queued): train it at the lowest-ID free producer that lists it (rule P's free-producer test), paid from the ledger; mark the producer used. Cannot pay: reserve its cost under rule R. The trained unit is assigned by step 3 on a later think.
5. **Route** (public data only): (a) every spawn except the own, ascending squared distance from the HQ, ties lower index; (b) every field whose centre is farther than `field_max_distance_tiles` from the own spawn and from every own drop-off, ascending squared distance from waypoint 0, ties lower index, skipped when within `scout_merge_tiles` of an earlier waypoint; truncated to `scout_route_max_waypoints`. Waypoints are field centres or spawn points.
6. **Steer.** While the current waypoint's tile `IsVisible`, advance `ScoutWaypoint`. Route not finished: if the scout has no move target, or the waypoint changed this think, emit `MoveUnit(scout, Standable(waypoint))` (a plain move: it shoots in passing and does not stop). Route finished: a borrowed scout is released (`ScoutId` = None, rejoins the army); an own scout gets `MoveUnit` to `Standable(Home())` once and stays the scout (excluded from the army) until the next round.
7. Commands: at most 2 per think (one train, one move).

### B.3 Enemy model (rule 0 extension; only when `use_enemy_model`)

Memory:

- `EnemyTypes`: `SortedDictionary<string, EnemyTypeEstimate>` (ordinal), `EnemyTypeEstimate { Fix64 Count; int LastSeenTick; }`. Only mobile non-worker, non-HQ enemy types (army). Bounded by the number of unit definitions.
- `LastVisibleEnemyTypes`: `SortedDictionary<EntityId, string>`: the visible enemy army units of the previous think and their type. Replaced every think; bounded by the visible count.
- `PendingEnemyKills`: `List<EntityId>` written by `SkirmishAiController.ReadEvents` from `UnitKilledEvent` (victim owner an enemy of the player), consumed and cleared by the next think. Bounded: cleared each think; at most `max_commands_per_think x 8` entries kept (older dropped, lowest tick first); the brain ignores IDs not in `LastVisibleEnemyTypes`.
- `KnownStructure.DefinitionId` (string, new): the definition ID, set with the other fields.

Decay factor for an age `a = tick - LastSeenTick` (ticks), with `H = SecondsToTicks(hold)`, `D = SecondsToTicks(decay)`, floor `f`: `1` when `a <= H`; `f` when `a >= H + D`; else `1 - (1 - f) x (a - H) / D`. Effective count `Eff(t) = max(visibleNow(t), Count(t) x factor)`.

Update each think, in this order:
1. Kills: for each ID in `PendingEnemyKills` (ascending) found in `LastVisibleEnemyTypes`: that type's `Count = max(0, Count - 1)`.
2. Sightings: for every army type t (ordinal), `visibleNow(t)` = visible enemy army units of type t. If `visibleNow(t) >= Count(t) x factor(t)`: `Count = visibleNow(t)`, `LastSeenTick = tick` (creating the entry if needed).
3. Replace `LastVisibleEnemyTypes`; clear `PendingEnemyKills`.

Derived (read by C and D; pure functions of memory, view and profile):
- `Eff(t)` for each tracked type.
- **Implied**: for each `KnownEnemyStructures` entry whose definition trains army types, each such type t gets `Implied(t) = enemy_model_implied_count`. **Model count** `M(t) = max(Eff(t), Implied(t))`.
- **Known army supply** `S_E = sum over t of M(t) x supply(t)`; **air supply** = the same over types with `Layer == Air`.
- Entries for C.1: `(definition t, count M(t), health M(t) x MaxHealth)` in ordinal order, types with `M(t) > 0` only.

### B.4 Package B tests (`rts/tests/Sim.Tests/Ai/AiIntelTests.cs`, hand-built views as in the existing AI tests)

- **B-1 Scout timing and unit.** Skai view at 39 s: no scout order; at 40 s with an idle Roost: `TrainUnit(roost, skai.windrunner)`. Water: `TrainUnit(tidepool, water.reef_runner)`. No think ever orders a worker as scout (assert over 200 thinks of B-10).
- **B-2 Route.** greybox_1v1, P0 HQ at (7, 7): waypoint 0 is spawn 1 (56, 56); fields 0 and 1 are excluded (within 10 tiles of the own spawn); the rest sorted by distance from (56, 56) with `scout_merge_tiles` 8 give exactly (56, 56), (42.5, 53.5) [field 8], (51.5, 14.5) [11], (12.5, 49.5) [10], (21.5, 10.5) [6]. Merged away: fields 2 and 3 (into the spawn), 9 (into 8), 5 (into 11), 4 (into 10), 7 (into 6).
- **B-3 Steer.** Waypoint tile visible: next think the scout gets `MoveUnit` to waypoint 1; not visible and moving: no order.
- **B-4 Death.** Scout missing: `ScoutLosses` 1, nothing trained until 30 s later. After 3 losses: never trained again; an idle own Roc Rider (cost 200) is borrowed instead.
- **B-5 Borrowed flyer released.** Borrowed scout finishes its route: `ScoutId` None and the unit receives a Massing rally order next think.
- **B-6 Scout excluded from the army.** In Attack, the scout gets no attack-move and is not in `AttackGroup`.
- **B-7 Model sighting and decay.** 3 Roc Riders visible at tick 0: `Eff` 3. Not visible: at 30 s 3; at 90 s 3 x (1 - 0.75 x 60/120) = 1.875; at 150 s and later 0.75. 1 visible at 90 s: `Eff` = max(1, 1.875) = 1.875, `Count` stays 3.
- **B-8 Kills.** 3 seen; two `UnitKilledEvent`s for IDs visible last think: `Count` 1. A kill of an ID never seen: no change.
- **B-9 Implied.** A known enemy Roost (structure entry with `DefinitionId` "skai.roost"): `M(skai.windrunner)` 1, `M(skai.roc_rider)` 1, air supply 3.
- **B-10 Fog.** Real simulation (TestWorld-style, greybox_1v1): an enemy Roc Rider spawned in a tile the AI's team cannot see; 200 thinks with the unit kept in fog: model air supply 0 at every think; the think after the tile becomes visible: 3.
- **B-11 Bounds.** After 2000 thinks of B-10's match: `EnemyTypes.Count <= Content.Units.Count`, `ScoutRoute.Count <= 8`, `PendingEnemyKills` empty after each think.
- **B-12 Determinism.** Same view, profile, memory copy and seed twice: identical intents and memory; `rng.DrawCount` unchanged by rules S and the model.
- **B-13 Validation.** `scout_unit` naming `water.tender`: error naming `factions.water.scout_unit` and "worker"; `use_counter_mix` true with `use_enemy_model` false: error naming both.

## Package C: stage 2, build the right army

### C.1 Fight predictor (`rts/src/Ai/FightPredictor.cs`, static, pure)

```
public readonly struct FightEntry { UnitDefinition Definition; Fix64 Count; Fix64 Health; }   // Count units >= 0; Health = total health points of the group
public readonly struct FightPrediction { Fix64 TimeAKillsB; Fix64 TimeBKillsA; bool AWins; Fix64 Advantage; }   // seconds, seconds, -, ratio
public static FightPrediction Predict(IReadOnlyList<FightEntry> a, IReadOnlyList<FightEntry> b, bool aUphill, CombatRules rules, PredictorSettings s)
public static List<FightEntry> Group(IEnumerable<AiUnitView> units)   // by definition ID, ordinal order; Count = units, Health = sum of Health
```

Entries are in ordinal definition-ID order; all sums run in that order (Fix64 truncation makes order matter). Only entries with `Count > 0` take part. Layer = `Definition.Layer`; armour = `Definition.ArmourType`.

For attacker type x (side X) and target type y (side Y):
- `mult(x, y) = rules.GetMultiplier(x.Weapon.DamageType, y.ArmourType)`.
- `up(x, y) = rules.UphillRangedDamageMultiplier` when X is side A, `aUphill`, `!x.Weapon.IsMelee`, x layer not Air and y layer not Air; else 1.
- `dps(x, y) = x.Weapon.Damage x mult x up / x.Weapon.CooldownSeconds` when x has a weapon and `x.Weapon.CanTarget(y layer)`; else 0. Evaluated left to right.

Time for side X to kill side Y (`T(X, Y)`):
0. Empty sides, checked first and in this order: Y has no entry: `T = predictor_min_seconds`. X has no entry (Y has some): `T = predictor_max_seconds` (an empty side never kills). This is what C-6 needs: B empty gives T(A, B) = 0.05 and T(B, A) = 600, so `AWins` true and Advantage 100. (Revised by C2.0: the earlier text let an empty X fall through step 3 to step 4 and get `predictor_min_seconds`, which contradicted C-6.)
1. `Hittable(x)` = entries y of Y that x can target. `D(x) = (sum over Hittable(x) of y.Health x dps(x, y)) / (sum over Hittable(x) of y.Health)`; 0 when x is unarmed or nothing is hittable.
2. `DPS = sum over x of x.Count x D(x)`; `Ranged` = the same over x with `!Weapon.IsMelee`.
3. Unhittable: entries y no armed x of X can target. If any such y is armed and `dps(y, x') > 0` for some x' in X: `T = predictor_max_seconds`. Otherwise those y are ignored.
4. `HP = sum of Health over the other entries of Y`. No entry of Y left after step 3 (every entry was unhittable and harmless): `T = predictor_min_seconds`.
5. `Dome = predictor_dome_weight x sum over y of y.Count x y.ShieldDome.Pool` (entries with a dome).
6. `DPS == 0`: `T = predictor_max_seconds`. Else `T = (HP + Dome x Ranged / DPS) / DPS`, capped at `predictor_max_seconds`.
7. Range head start (only when `T < predictor_max_seconds`): `rX = (sum of x.Count x D(x) x x.Weapon.RangeTiles) / DPS`; `rY` the same for Y against X; when Y's DPS against X is 0 (Y cannot hurt X), `rY` = the Count-weighted mean `Weapon.RangeTiles` of Y's armed entries (0 when none is armed), as worked example C-2 uses (Pummelers cannot hit Roc Riders, rY = 1, head = (5 - 1) / 3 = 4/3; revised by C2.0, the earlier text said 0); `vY` = mean `SpeedTilesPerSecond` of Y's armed entries weighted by Count. When `rX > rY`: `head = predictor_range_headstart_max_seconds` if `vY == 0`, else `min((rX - rY) / vY, predictor_range_headstart_max_seconds)`; `T = max(predictor_min_seconds, T - predictor_range_weight x head)`.

Result: `TimeAKillsB = T(A, B)`, `TimeBKillsA = T(B, A)`; `AWins = TimeAKillsB < TimeBKillsA` (ties: false, the defender holds); `Advantage = min(predictor_max_advantage, TimeBKillsA / max(TimeAKillsB, predictor_min_seconds))`. A empty: `AWins` false, `Advantage` 0.

Profile keys (root, same in every profile):

| Key | Value | Unit | Range |
| --- | --- | --- | --- |
| `predictor_max_seconds` | 600 | seconds | 10 to 36000 |
| `predictor_min_seconds` | 0.05 | seconds | 0.01 to 10 |
| `predictor_range_weight` | 1 | fraction | 0 to 1 |
| `predictor_range_headstart_max_seconds` | 5 | seconds | 0 to 60 |
| `predictor_dome_weight` | 1 | fraction | 0 to 2 |
| `predictor_max_advantage` | 100 | ratio | 1 to 10000 |

Not modelled (simple on purpose): drench slow and grounding, splash, momentum, flooding, Windstorm range, regeneration, hit delay, focus fire.

### C.2 Worked examples (tests C-1 to C-5; full health, shipped data, settings above; tolerance 0.001)

| # | A | B | `aUphill` | TimeAKillsB | TimeBKillsA | AWins | Advantage |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C-1 | 4 Windrunner | 2 Pummeler | no | 220 / 22.5 - 3/3 = 8.7778 | 240 / 24 = 10.0 | true | 1.1392 |
| C-2 | 3 Pummeler | 2 Roc Rider | no | 600 (Roc unhittable, armed) | 330 / 17.5 - 4/3 = 17.5238 | false | 0.0292 |
| C-3 | 2 Talon Marksman | 1 Shellback | no | (220 + 150) / 12 - 4/1.8 = 28.6111 | 180 / 10 = 18.0 | false | 0.6291 |
| C-4 | 4 Windrunner | 2 Pummeler | yes | 220 / 16.875 - 1 = 12.0370 | 10.0 | false | 0.8308 |
| C-5 | 4 Windrunner | 1 Coral Turret | no | 320 / 15 = 21.3333 | 240 / 6 - 3/3.5 = 39.1429 | true | 1.8348 |

Per-unit DPS used: Windrunner vs Pummeler 6 x 0.75 / 0.8 = 5.625; Pummeler vs Windrunner 12; Roc vs Pummeler 14 x 0.75 / 1.2 = 8.75; Talon vs Shellback 6; Shellback vs Talon 10 (range 2 is ranged); uphill Windrunner 5.625 x 0.75 = 4.21875; Windrunner vs turret 6 x 0.5 / 0.8 = 3.75; turret vs Windrunner 6 (speed 0, so the turret's head start uses the attackers' speed 3.5). Also **C-6**: B empty gives `AWins` true, TimeAKillsB 0.05, Advantage 100; A empty gives `AWins` false, Advantage 0; **C-7**: swapping the order of entries within a side gives bit-identical results when the caller sorts (Group sorts ordinally).

### C.3 Unit pool (computed from data)

Pool of a faction = every unit u with `FactionId` = the plan's faction, footprint 0, not a worker, not an HQ, listed in the `trains` of some structure of the faction. Shipped: Skai {roc_rider, stormcaller, talon_archer, windrunner}; Water {reef_runner, grasper, ink_lobber, shellback, spoutling}. Static defences of the faction = structures with `IsStaticDefence` and a weapon.

### C.4 Counter-mix (rule 5 input when `use_counter_mix`)

Inputs: enemy entries E (B.3), pool P, base composition `w_base` (plan `composition`).

1. For u in P: `cost(u) = cost_common + counter_rare_cost_weight x cost_rare`. `Dout(u)` = C.1 `D(u)` with X = {u} and Y = E. `Din(u) = (sum over e in E of e.Count x dps(e, u)) / (sum of e.Count)`. `score(u) = Dout(u) x MaxHealth(u) / (max(Din(u), counter_min_incoming_dps) x cost(u) x cost(u))` (Lanchester: strength per budget squared).
2. `top` = max score. Keep `score(u)` where `score(u) >= counter_keep_fraction x top` and `score(u) > 0`; others 0. `s(u) = score(u) / sum of kept scores` (all 0 when nothing kept).
3. Confidence `k = counter_weight x min(1, S_E / counter_full_confidence_supply)`; `k = 0` when E is empty or nothing is kept.
4. For u in P union base keys (ordinal): `mix(u) = floor(mix_resolution x ((1 - k) x w_base(u) / sum w_base + k x s(u)))`. These integers replace `composition` in rule P, rule X and rule T. With `k = 0` this is the base composition scaled (deficit order unchanged).

Profile keys (root): `counter_weight` 0.7 (fraction, 0 to 1), `counter_full_confidence_supply` 8 (supply, 1 to 200), `counter_keep_fraction` 0.5 (fraction, 0 to 1), `counter_rare_cost_weight` 2 (ratio, 0 to 20), `counter_min_incoming_dps` 1 (health points per second, 0.01 to 1000), `mix_resolution` 1000 (weight units, 10 to 100000).

### C.5 Rule T: tech on demand (rule 3b, after Structures; when `use_tech_on_demand`)

`u_top` = the mix unit with the largest `mix(u)` (ties ordinal) among those no own structure (complete or under construction) trains. If `mix(u_top) >= tech_min_weight_share x mix_resolution` and no own structure that trains a mix unit is under construction: `TryBuild` its producer (rule X's choice: first build-order structure that trains it, else the ordinally lowest faction structure that does) at the HQ ring sites. Key: `tech_min_weight_share` 0.15 (fraction, 0 to 1).

### C.6 Rule SD: static defence (rule 3c; when `use_static_defence`)

Memory: `RaidLog`: `SortedDictionary<EntityId, List<int>>` by own drop-off ID, raid start ticks (each list at most `static_defence_after_raids` + 1 entries, oldest dropped; entries of drop-offs that no longer exist removed each think). `LastRaidTick`: `SortedDictionary<EntityId, int>`.

1. **Raid.** For each own drop-off (complete depot or Rooted HQ), ascending ID: a visible enemy army unit within `defend_radius_tiles` of it, or a recent hit report there, is raid activity. Activity at tick t with no activity there in the previous `raided_gap_seconds` starts a raid (append t). Always set `LastRaidTick`.
2. **Raided base.** Raid starts within the last `raided_window_seconds` `>= static_defence_after_raids`, fewer than `static_defence_max_per_base` own static defences within `field_max_distance_tiles` of it, none under construction anywhere: build one.
3. **Unanswered layer.** For each layer (ground, water, air order) where the model has supply `>= unanswered_layer_min_supply` of that layer, no own army unit or static defence can target it, and no pool unit that can target it has `cost_rare <= rare stock`: build one static defence at the HQ (same caps).
4. **Which and where.** The faction static defence with the highest C.4 score against E (ties ordinal) among those that can target every layer that triggered it; site: Chebyshev rings `static_defence_ring_min_tiles` to `static_defence_ring_max_tiles` around the drop-off tile, `IsGoodSite` with that tile's height, lowest squared distance to the mean raid position (step 3: to the enemy spawn), ties scan order of rule E. Built through `TryBuild` with the site (rule R reservations apply). At most one per think.

Profile keys (root): `raided_window_seconds` 120 (seconds, 10 to 1800), `raided_gap_seconds` 20 (seconds, 0 to 600), `static_defence_after_raids` 2 (raids, 1 to 20), `static_defence_max_per_base` 2 (structures, 0 to 6; must not exceed `supply.json` `static_defences_per_base_max`, checked in a test, not in code), `static_defence_ring_min_tiles` 2 (tiles, 1 to 10), `static_defence_ring_max_tiles` 4 (tiles, ring min to 12), `unanswered_layer_min_supply` 3 (supply, 1 to 200).

### C.7 Rule RS: rare priority (changes rule E; when `use_rare_priority`)

When the mix weights of units with `cost_rare > 0` sum to at least `rare_need_weight_share x mix_resolution` and no rare field is served: rule E ignores its clock gate (all other gates stay), and its target order key becomes (safe first, rare site first, squared distance, index), where a rare site is a rare field or a field within `field_max_distance_tiles` of a rare field. Key: `rare_need_weight_share` 0.25 (fraction, 0 to 1).

### C.8 Base compositions in `aggressive_s2.json` and `_s3.json`

Skai `{ "skai.windrunner": 3, "skai.talon_archer": 2, "skai.roc_rider": 1, "skai.stormcaller": 1 }`; water `{ "water.grasper": 3, "water.shellback": 1, "water.ink_lobber": 1, "water.spoutling": 1 }`. Every pool unit stays reachable through C.4 even with base weight 0.

### C.9 Package C tests (`rts/tests/Sim.Tests/Ai/FightPredictorTests.cs`, `AiArmyMixTests.cs`)

- **C-1 to C-7** as in C.2.
- **C-8 Counter scores, water vs 4 Roc Riders.** Spoutling: 5 x 70 / (11.6667 x 150 x 150) = 0.0013333; grasper, shellback, ink_lobber and reef_runner 0 (cannot hit air). With C.8 base and S_E = 12: k = 0.7; mix = grasper 150, shellback 50, ink_lobber 50, spoutling 750, reef_runner 0.
- **C-9 Counter scores, Skai vs 6 Pummelers.** Windrunner 0.005, Talon 0.0045, Stormcaller 900 / (12 x 230 x 230) = 0.0014178, Roc 8.75 x 160 / (1 x 250 x 250) = 0.0224 (untouchable). Kept: Roc only. Mix: windrunner 128, talon 85, roc 742, stormcaller 42.
- **C-10 Nothing known.** E empty: mix equals floor(1000 x base share) for every base unit; P trains the same unit as v2 on C-1 of skirmish_ai_v2.md's view.
- **C-11 Tech.** Skai, mix gives Stormcaller 300 of 1000, no Windspire: `BuildStructure(skai.windspire)`; Windspire under construction: none.
- **C-12 Raided twice.** Threat at the HQ at 100 s, gone, again at 130 s (gap 20 s): one static defence build order next to the HQ toward the threat; a third raid with 2 defences there: none.
- **C-13 Unanswered air.** Water, model air supply 6, no own air-capable unit, rare 0: `BuildStructure(water.coral_turret)` at the HQ; rare 25 and a Tidepool: no turret (Spoutling is affordable; rule P handles it).
- **C-14 Rare priority.** Mix gives rare units 400 of 1000, no rare field served, 100 s: a depot at the rare flank site (greybox P0: the depot site of field 10 by rule E's scan) before the 240 s clock.
- **C-15 Profile check.** Every shipped profile's `static_defence_max_per_base <= static_defences_per_base_max` from `supply.json`.
- **C-16 Determinism and purity.** `Predict` twice on the same inputs: bit-identical; it allocates no state outside its result; no `float`/`double` in the new files (grep test like the existing ones, if present).

## Package D: stage 3, choose fights

### D.1 Attack decision (replaces the Massing threshold when `use_predictor_attack`)

Own side: `Group` of army units excluding scout, defend group, harass group and fallen-back units. Enemy side at target p (from D.3 or `TryChooseTarget`): model entries x `undefended_army_share` when p is undefended (D.3), else x 1; plus every known enemy static defence within `target_defence_radius_tiles` of p (Count 1, Health = `HealthFraction x MaxHealth`). `aUphill` = `HeightAt(p)` > `HeightAt(rally point)`.

- Info empty (known army supply 0 and no static defence counted): the v2 rule (drawn threshold) decides.
- Otherwise attack when `AWins` and `Advantage >= attack_min_advantage` and own army supply `>= attack_min_supply`, or own army supply `>= attack_force_supply`.
- Tide window, regroup timer, rule K and all-in are unchanged.

Keys: `attack_min_advantage` 1.3 (ratio, 0 to 100), `attack_min_supply` 6 (supply, 1 to 200), `attack_force_supply` 60 (supply, 1 to 400), `undefended_army_share` 0.5 (fraction, 0 to 1), `target_defence_radius_tiles` 12 (tiles, 2 to 64).

### D.2 Retreat (when `use_predictor_attack`)

Replaces the power test: retreat when the visible armed enemies (not workers; static defences included) within `engagement_radius_tiles` of any group unit are non-empty and `Predict(group, them, aUphill).Advantage < retreat_max_advantage`. `retreat_army_fraction` stays. Key: `retreat_max_advantage` 0.7 (ratio, 0 to 10).

### D.3 Target choice (when `use_target_choice`)

Enemy army position: `LastSeenEnemyPosition` if seen within `enemy_army_memory_seconds`, else the nearest known enemy HQ, else the nearest uncleared enemy spawn. A known structure is **undefended** when no known enemy static defence is within `target_defence_radius_tiles` of it and the enemy army position is farther than that. Order: rule K; then (a) the undefended known enemy drop-off that is not an HQ, nearest to Home; (b) the undefended known enemy producer (`Trains` non-empty), nearest to Home; then the v2 order (HQ, structure, spawn, last seen). Ties lower entity ID.

### D.4 Rule HR: harass workers (in Army, before stance orders; when `use_target_choice`)

Memory: `HarassGroup` (`SortedSet<EntityId>`), `HarassTarget` (FixVec2), `HarassArrivedTick` (int, -1), `NextHarassTick` (int).

- **Start** (group empty, `tick >= NextHarassTick`, stance Massing or Attack, army supply `>= harass_min_army_supply`): candidates are army units not in the attack, defend or scout roles with `Layer == Air` or `SpeedTilesPerSecond >= harass_min_speed_tiles_per_second`, ascending ID, taken until their supply reaches `harass_squad_supply` (not reached: no harass). Target: the known enemy drop-off (HQ included) farthest from the enemy army position (ties lower ID), only if `Predict(squad, static defences within target_defence_radius_tiles of it)` has `AWins` (or none there). Emit `AttackMove(Standable(target))` per member.
- **Run**: members within `focus_radius_tiles` of a visible enemy worker get `AttackUnit` on the lowest-health such worker (ties nearer, then lower ID), kept while valid. First think a member is within `stage_radius_tiles` of the target: set `HarassArrivedTick`.
- **End** (all members home-ordered with `MoveUnit(Standable(Home()))`, group cleared, `NextHarassTick = tick + SecondsToTicks(harass_cooldown_seconds)`): the D.2 test fails for the squad; or arrived and no visible enemy worker within `target_defence_radius_tiles` of the target for `harass_linger_seconds`. Group empty (all dead): cleared, same cooldown.
- At most `harass_max_commands_per_think` commands.

Keys: `harass_min_army_supply` 10 (supply, 1 to 200), `harass_min_speed_tiles_per_second` 3.2 (tiles per second, 0 to 20), `harass_squad_supply` 4 (supply, 1 to 50), `harass_cooldown_seconds` 60 (seconds, 0 to 900), `harass_linger_seconds` 10 (seconds, 0 to 120), `harass_max_commands_per_think` 8 (commands, 0 to 40).

### D.5 Measured defence (replaces `DefendIfThreatened` when `use_measured_defence`)

Threat set: the visible enemy army units inside the base (as `ScanThreat`). Own fixed side: own complete static defences within `engagement_radius_tiles` of the threat centre. Candidates: army units not scout, not fallen back, ascending squared distance to the threat centre (ties lower ID), at most `defend_max_candidates`. Add candidates one by one until `Predict(fixed + chosen, threat).Advantage >= defend_min_advantage`, or all are used. Hit report with no visible threat: choose nearest candidates until their supply `>= defend_blind_supply`.
- Chosen units form `DefendGroup` (memory, `SortedSet<EntityId>`, recomputed each think with a threat) and get the v2 Defend orders (focus, attack-move to the goal).
- Candidates that are not in the attack group come first (nearest first), attack-group units last (review of 17 Nov 2026). The attack group is recalled (stance Defend as in v2, every army unit defends) only when the supply the predictor asks for from attack-group units reaches `defend_recall_supply`; below that the group's units are dropped from the chosen set, the stance is unchanged and they keep attacking. Otherwise the other units keep their orders.
- No threat: `DefendGroup` holds (no orders) until `LastThreatTick + defend_hold_seconds`, then is cleared (rule D unchanged).

Keys: `defend_min_advantage` 1.5 (ratio, 0 to 100), `defend_blind_supply` 4 (supply, 1 to 200), `defend_max_candidates` 40 (units, 1 to 200).

### D.6 Unit control (when `use_unit_control`; runs in Army before stance orders; a unit ordered here gets no stance order this think; never under all-in or rule K for D.6.1)

1. **Fall back.** An army unit (not in harass or scout roles) with `Health / MaxHealth <= fallback_health_fraction` and a visible armed enemy that can target its layer within `fallback_danger_tiles`: `MoveUnit(Standable(Home()))`, removed from `AttackGroup`, added to `FallenBack` (memory `SortedSet<EntityId>`). A fallen-back unit within 3 tiles (the existing `AtRallySquared`) of `Standable(Home())` leaves `FallenBack`. Dead IDs pruned each think. Budget `fallback_max_commands_per_think`.
2. **Keep distance.** A ranged unit (`!Weapon.IsMelee`) whose nearest visible armed enemy army unit e that can target it (not only melee: D-9 kites from a range-2 Shellback; the range gap separates short-range enemies from long-range ones) is within `kite_trigger_tiles` (centre distance), with `range(unit) >= range(e) + kite_min_range_gap_tiles` and `speed(unit) >= kite_speed_ratio_min x speed(e)`: `MoveUnit(Standable(p))` where p is exactly `kite_step_tiles` from the unit directly away from e (always the full step, kept 0.5 tiles inside the map edge; no order when the two positions coincide to within rounding). Budget `kite_max_commands_per_think`.
3. **Focus fire on what dies fastest** (replaces `Focus`'s choice): for each visible enemy army unit f, `ttk(f) = f.Health / (sum of dps(o, f) over own army units o within focus_radius_tiles of f)` (no shooter: skipped). A unit picks, among foes it can target within `focus_radius_tiles`, the lowest `ttk` (ties nearer, then lower ID). It keeps its current forced target while that is still a candidate and its `ttk <= focus_switch_ratio x` the best. Budget `focus_max_commands_per_think`; units beyond the budget get the stance order.

Review additions (17 Nov 2026), each a profile key in every profile file: `use_focus_fire` (switch; D.6.3 fastest-kill focus is split from `use_unit_control`), `fallback_release_seconds` 30 (a fallen-back unit idle away from home is sent home again and released after this time), `attack_retreat_grace_seconds` 5 (D.2: no predicted retreat this long after an attack starts unless the group has lost supply), `harass_max_seconds` 90 (D.4: the squad is recalled this long after it started), `rare_bank_return_cap` (C2.3 hysteresis: closed rare fields reopen below this; 0 = the cap).

Keys: `fallback_health_fraction` 0.3 (fraction, 0 to 1), `fallback_danger_tiles` 7 (tiles, 1 to 30), `fallback_max_commands_per_think` 6, `kite_trigger_tiles` 3 (tiles, 0.5 to 20), `kite_step_tiles` 3 (tiles, 0.5 to 20), `kite_min_range_gap_tiles` 2 (tiles, 0 to 20), `kite_speed_ratio_min` 0.8 (ratio, 0 to 5), `kite_max_commands_per_think` 8, `focus_switch_ratio` 1.25 (ratio, 1 to 10), `focus_max_commands_per_think` 12 (all budgets: commands, 0 to 200). All intents still go through `Emit`, so `max_commands_per_think` caps the total.

### D.7 Package D tests (`rts/tests/Sim.Tests/Ai/AiFightTests.cs`)

- **D-1 Attack by prediction.** Own 4 Windrunners, model 2 Pummelers, target on level ground: Advantage 1.1392 < 1.3: no attack; own 6 Windrunners: attack (Advantage above 1.3; assert the stance). Same 6 with the target one height layer up: recompute and assert the expected decision.
- **D-2 Fallback threshold.** Model empty, no static known: attack at the drawn threshold as in v2 (`rng.DrawCount` as v2).
- **D-3 Force.** Model 6 Roc Riders, own army 60 supply of Pummelers (Advantage 0): attack anyway.
- **D-4 Retreat.** Group of 3 Pummelers sees 2 Roc Riders within 10 tiles: retreat (Advantage 0.0292 < 0.7).
- **D-5 Targets.** Known enemy HQ, a depot 30 tiles from `LastSeenEnemyPosition` (seen 10 s ago) with no turret: target is the depot. A turret within 12 tiles of the depot: target is the HQ.
- **D-6 Harass.** Army 12 supply including 4 Windrunners (speed 3.5): HR picks them, attack-moves them to the known depot farthest from the enemy army; a visible enemy worker within 8 tiles: `AttackUnit` on the lowest-health worker. 10 s at the target with no worker: home and cooldown.
- **D-7 Measured defence.** 2 enemy Windrunners in the base; own 8 Pummelers at home, attack group elsewhere: only the nearest Pummelers needed for Advantage 1.5 defend (assert the count from the predictor), stance stays Attack, the group is not recalled. 8 enemy Roc Riders: the attack group is recalled.
- **D-8 Fall back.** Unit at 25 percent health with an armed enemy 5 tiles away: `MoveUnit` home, out of `AttackGroup`; within 3 tiles of home: back in the army.
- **D-9 Keep distance.** Talon Marksman (range 6, speed 2.5) with a Pummeler (range 1, speed 3) 2 tiles away: `MoveUnit` 3 tiles directly away (point computed by the formula); a Shellback (range 2) at 2 tiles: range gap 4 >= 2 and speed ratio 2.5/1.8 >= 0.8: moves away too.
- **D-10 Focus.** Two visible enemies: Windrunner at 20 health and Pummeler at 110, both in reach of 3 own Talons: all three focus the Windrunner; next think the Windrunner gone: switch to the Pummeler.
- **D-11 Budgets.** 30 army units all qualifying for focus with `focus_max_commands_per_think` 12: exactly 12 `AttackUnit` intents; total intents never exceed `max_commands_per_think`.
- **D-12 Determinism.** Same inputs twice: identical intents and memory.

## E. Acceptance per package

| Package | NUnit | Response tests that must pass (`--response --profile <p>`) | Gate run |
| --- | --- | --- | --- |
| A | A-1 to A-11 | none (baseline report A-10) | `--ladder --profile-a aggressive --profile-b aggressive --games 4 --maps greybox_1v1` runs, exit 0 or 2, report attached |
| B | B-1 to B-13, A-11 green | `air_answer` (1) on both roc_rush files with `aggressive_s1` | `--ladder --profile-a aggressive --profile-b aggressive_s1 --non-regression` passes (Q1) |
| C | C-1 to C-16, A-11 green | with `aggressive_s2`: all of B's, `air_answer` (2), `armoured_answer` (1), `trade`, `raid` (1) | `--ladder --profile-a aggressive_s1 --profile-b aggressive_s2` passes (65 percent of at least 200 decided) |
| D | D-1 to D-12, A-11 green | with `aggressive_s3`: every check of every scenario | `--ladder --profile-a aggressive_s2 --profile-b aggressive_s3` passes |

Each package also: `dotnet test` green, `sim-reviewer` pass on `rts/src/Ai` and the harness diff, the ladder report and response summary in the builder's report. If a response test or the ladder fails, the builder reports the numbers and does not tune combat data; profile numbers in the new `_sN` file may be tuned (say which and why).

Existing tests and hashes affected:
- Package A: listed under A.5. All shipped-data `STATE_HASH` values change once (Reef Runner); CI compares two runs, so it stays green; older replays no longer re-simulate.
- Packages B to D: no shipped-data hash may change for `aggressive`/`defensive` runs (A-11 guards it). `HarnessTraceTests` may need the new profile fields in the trace header. `AiProfile` parse tests: add the new keys to any hand-written profile JSON in tests (or the parse fails on a missing key).
- `DeterminismTests.GoldenFinalHash` never changes.

## Out of scope

- Stages 4 to 6 of P-004 (openings, difficulty levels, faction mechanics such as flooding, momentum, the Core and cycles, self-play tuning).
- Combat, unit and map tuning; the Reef Runner's 3D model (PC session: placeholder model per ID until the art pipeline delivers one).
- Drench grounding, splash, dome placement and regeneration in the predictor; path-aware danger avoidance for scouts; switching `harness_batch.json` `ai_profile` (Q5).
- Any `rts/src/Sim` change.

## Questions for the developer (the builder uses the defaults)

- **Q1. Stage 1 gate.** Scouting and a model without acting on them cannot win 65 percent; it costs a scout. Default: package B merges on a non-regression ladder (candidate at least 45 percent of decided games) plus its tests; the 65 percent bar gates C and D.
- **Q2. Can the Reef Runner hit air?** The Windrunner can. Default: no (ground and water only) so the Spoutling and Coral Turret stay water's anti-air answer as P-004's counter table says; the two scouts are otherwise equal. If you want exact parity, add `air` (the counter-mix would then prefer Reef Runners over Spoutlings against Roc Riders, C-8 changes).
- **Q3. Reef Runner speed.** 4.1 base gives 3.485 on dry ground (Windrunner 3.5) and 4.7 / 5.3 on flooded ground. Default: keep; a faster scout on its own water fits the faction.
- **Q4. Scripted opponents get free units.** The roc rush, worker raid and turtle scenarios spawn units (the Roc Riders without rare). Default: acceptable for response tests; they test the answer, not the opponent's economy.
- **Q5. Which profile the balance batch uses.** Default: after D's ladder passes, the coordinator switches `harness_batch.json` `ai_profile` to `aggressive_s3` in a separate change; `defensive.json` stays v2.
- **Q6. Borrowing a Roc Rider as scout.** `scout_air_max_cost` 250 lets the AI send a Roc Rider. Default: yes.
- **Q7. Early water anti-air.** Starting rare is 0 and the Spoutling costs 25 rare, so before its first rare depot water's only anti-air is the Coral Turret (rule SD step 3). Default: leave the data as it is.

## Package C2: counter-mix revision

Written 17 Nov 2026 after the package C review. Applies on top of package C; package D is unchanged except that it reads the mix of C2.2 when the new switch is on. Every rule below sits behind a switch or a key whose "off" value reproduces package C exactly, so `aggressive`, `defensive`, `aggressive_s1` and the `rt_*` profiles play as before (A-11 stays green) and `abl_s2_blind` plays exactly as before (C2-11).

### C2.0 Text fixes to C.1 (no code change)

The builder found two places where C.1's text contradicted its own worked examples; `FightPredictor.cs` implements the examples, and C.1 now says the same:

1. **Empty sides (C.1 new step 0, step 4).** Y empty: `T = predictor_min_seconds`; X empty (Y not): `T = predictor_max_seconds`; both checked before step 1, in that order. The old text sent an empty X through step 3 to step 4 and gave it `predictor_min_seconds`, so B empty would have given `AWins` false against C-6.
2. **Reach when Y cannot hurt X (C.1 step 7).** `rY` is the Count-weighted mean `Weapon.RangeTiles` of Y's armed entries (0 when none is armed), not 0. C-2's head start 4/3 = (5 - 1) / 3 needs it.

Builder: delete the "Deviation from C.1 step 7" comment in `FightPredictor.cs` (the spec now matches) and cite C.1 step 7 instead; C-1 to C-7 stay as they are.

### C2.1 Diagnosis

**What was run.** The B ablation ladder (`aggressive_s2` against `abl_s2_blind`, 288 games, 43.8 percent) lost 0 of 24 water mirrors on prototype_1v1 and 2 of 24 on twin_chokes. Five of its games were re-traced (ladder seed 20261117, cap 2400 s; each trace ends on the same tick as its `ladder.jsonl` record) and saved under `rts/docs/reports/ai_v3_2026-11-17/traces/`:

```
dotnet run -c Release --project rts/tools/BalanceHarness -- --data rts/data --trace --map <map> --matchup <matchup> --seed 20261117 \
    --profile-a abl_s2_blind --profile-b aggressive_s2 --game <N> --out <file>
```

| Trace file | Map, matchup, game | Reacting side (`aggressive_s2`) | Winner |
| --- | --- | --- | --- |
| `c2_prototype_water_g0.txt` | prototype_1v1, water_vs_water, 0 | slot 1 | blind, tick 14205 |
| `c2_prototype_water_g2.txt` | prototype_1v1, water_vs_water, 2 | slot 0 | blind, tick 19158 |
| `c2_twin_chokes_water_g0.txt` | twin_chokes, water_vs_water, 0 | slot 1 | blind, tick 11478 |
| `c2_twin_chokes_water_g2.txt` | twin_chokes, water_vs_water, 2 | slot 0 | blind, tick 13537 |
| `c2_greybox_skai_g0.txt` | greybox_1v1, skai_vs_skai, 0 | slot 1 | blind, tick 11645 |

**What the reacting side (R) builds and spends compared with the blind side (B)** (water mirrors; totals over the whole game; common / rare in resource units):

| Game | Side | First army unit | First static defence | Static defences placed (destroyed) | Reef Runners trained = lost | Shellbacks | Pummelers | Ink Lobbers | Spoutlings | Common gathered / spent | Rare gathered / spent | Rare bank at 300 s / peak |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| prototype g0 | R | 73 s | tick 2239 (112 s) | 11 (10) | 25 | 15 | 67 | 1 | 8 | 15000 / 15150 | 1200 / 240 | 535 / 1010 |
| prototype g0 | B | 71 s | none | 0 | 3 | 8 | 74 | 9 | 24 | 15480 / 15650 | 1620 / 960 | 340 / 675 |
| prototype g2 | R | 71 s | tick 2310 (116 s) | 15 (10) | 31 | 19 | 76 | 1 | 10 | 18305 / 18500 | 2575 / 290 | 485 / 2285 |
| prototype g2 | B | 73 s | none | 0 | 4 | 9 | 78 | 12 | 28 | 17000 / 17100 | 1800 / 1205 | 380 / 750 |
| twin_chokes g0 | R | 127 s | tick 1999 (100 s) | 2 (0) | 18 | 9 | 35 | 2 | 5 | 9505 / 9700 | 600 / 205 | 550 / 550 |
| twin_chokes g0 | B | 80 s | none | 0 | 4 | 7 | 49 | 6 | 14 | 11215 / 11125 | 600 / 590 | 355 / 445 |
| twin_chokes g2 | R | 128 s | tick 2020 (101 s) | 11 (6) | 16 | 12 | 52 | 1 | 5 | 12265 / 12400 | 600 / 165 | 550 / 550 |
| twin_chokes g2 | B | 79 s | none | 0 | 5 | 10 | 67 | 6 | 14 | 13735 / 13850 | 600 / 590 | 380 / 420 |

Skai mirror, greybox game 0: R placed 11 Perch Towers (first at tick 1539, 77 s; 8 destroyed), gathered 12850 common against B's 15000 and lost 22 Fledglings against 1.

**Candidates, confirmed or rejected:**

1. **Static defence spends the army's money: confirmed, the largest single cause.** In every traced game R's first static defence comes at 77 to 116 s, right after B's scout reached R's base (on twin_chokes B's scout hit R's HQ at 97 s and R ordered a Coral Turret at tick 1999). With `static_defence_after_raids` 1 and C.6 counting any visible enemy army unit or any recent hit as raid activity, a lone 1-supply scout is a raid. R then keeps building at every raided depot: 2 to 15 defences per game, 200 to 1500 common, 9 to 11 percent of its army spending in the three games with 11 or more, most of them destroyed at forward depots. On twin_chokes the turret and its rule-R reservation pushed R's first army unit from 80 s to 127-128 s. At 240 s in prototype g2 R had 22 army supply (5 Pummelers, 4 Reef Runners, 2 Shellbacks, 1 Spoutling) against B's 31 (9 Pummelers, 1 Ink Lobber, 1 Shellback, 4 Spoutlings), with 4 turrets (400 common) at home; R lost the first fights there (2 Pummelers, 1 Shellback, 2 Reef Runners against none).
2. **Per-cost scoring keeps cheap units that lose: confirmed, the second cause.** C.4 scores against the blind water army (3 Pummelers, 1 Shellback, 1 Ink Lobber, 1 Spoutling, 1 implied Reef Runner) are Pummeler 0.0133, Shellback 0.0073, Reef Runner 0.0068, Ink Lobber 0.0022, Spoutling 0.0016. With `counter_keep_fraction` 0.5 all three of the first are kept with shares 0.49 / 0.27 / 0.25, so at k = 0.7 a quarter of the counter share goes to Reef Runners and a quarter to Shellbacks. R trained 16 to 31 Reef Runners per game and lost every one; B trained 3 to 5 (its scouts). The score divides by cost squared, so the 75-cost Reef Runner scores close to the 100-cost Pummeler although it has 60 health against 110 and the enemy's main unit hits light armour harder. Equal-cost fights in the simulation (C2.1 table below) show the Pummeler is the best water unit against every water army tried, and that adding Reef Runners or Shellbacks to Pummelers loses.
3. **The mix chases a model made of scout-only sightings: confirmed for the confidence, by construction (the trace does not print k).** B.3 gives every type a known structure trains an implied count of 1. When R's scout sees a Tidepool and a Shell Forge, the implied units (Pummeler 2, Spoutling 2, Reef Runner 1, Shellback 3, Ink Lobber 2 supply) already make S_E = 10, above `counter_full_confidence_supply` 8, so k = 0.7, full confidence, before a single enemy army unit was seen.
4. **Rare units starve the common budget: rejected; the opposite happens.** R gathers rare and does not spend it: 165 to 290 rare spent of 600 to 2575 gathered, a bank of 485 to 550 at 300 s and up to 2285 at the peak, while its common bank stays below 150. The mix squeezes rare units out (weight 2, squared in the score) but rule G keeps `rare_workers_per_field` workers on every served rare field, so those workers produce nothing the AI uses.
5. **The mix swings every think: rejected.** From 150 s to the end, every 30-second line of R lists the same three units in training (Pummelers, Shellbacks, Reef Runners); there is no back-and-forth. No hysteresis is added; the new mix (C2.2) is continuous in its inputs.

**Equal-cost fights in the simulation** (scratch command scripts, not committed; results in `traces/c2_equal_cost_fights.txt`): greybox_1v1, both HQs at their spawns, armies spawned 16 tiles apart (corners at (24, 24) and (40, 40)) or 51 tiles apart ((14, 14) and (50, 50)), every unit attack-moves to the enemy HQ at tick 2; each pair is played in both seats; the winner is the side that destroys the other HQ. Costs counted as common + rare.

| Side A | Side B | Result | Predictor Advantage of A (C.1) |
| --- | --- | --- | --- |
| 15 Pummelers (1500) | 8 Ink Lobbers (1520) | A wins both seats | 3.417 |
| 15 Pummelers | blind mix: 6 Pummelers, 2 Shellbacks, 2 Ink Lobbers, 2 Spoutlings (1580) | A wins both seats, both distances | 1.555 |
| 20 Reef Runners (1500) | 15 Pummelers | B wins both seats | 0.488 |
| 8 Shellbacks (1400) | 14 Pummelers | B wins both seats | 0.551 |
| 7 Pummelers + 8 Reef Runners (1300) | 13 Pummelers | B wins both seats | |
| 10 Pummelers + 7 Reef Runners (1525), 51 tiles | 15 Pummelers | B wins both seats | |
| 7 Pummelers + 4 Shellbacks (1400), 51 tiles | 13 Pummelers | B wins both seats | |
| package C mix: 8 Pummelers, 3 Shellbacks, 4 Reef Runners (1625) | blind mix | 16 tiles: A wins both seats; 51 tiles: slot 0 wins both | 1.504 |
| 10 Pummelers + 3 Ink Lobbers (1570) | 15 Pummelers | slot 1 wins both (seat-decided) | |

So the predictor ranks water units in the same order as the simulation in every water fight tried, while C.4's per-cost score does not (it keeps Reef Runners and Shellbacks). Skai fights with this setup end undecided (the survivors cannot finish a Great Aerie), so they say nothing; the Skai side rests on the predictor alone. The C.4 composition itself is about even with the blind one at equal cost; R lost mainly because less of its money became army (finding 1), the money that did went partly to units that lose (finding 2), and its rare income went unused (finding 4).

**Diagnosis in one line.** Reacting loses the water mirror because rule SD treats the enemy scout as a raid and keeps buying static defence with the army's money, and because C.4's cost-squared score with a loose keep fraction spends a third of the counter share on cheap units that lose equal-cost fights, at full confidence from implied units; the fix is a predictor-scored mix, confidence from seen units only, a raid strength threshold and a value cap on static defence, and a rare-worker cap.

### C2.2 Predictor mix (replaces C.4 steps 1 to 3 when `use_counter_mix` and `use_predictor_mix`)

Lives in `AiCounter` (a new `PredictorMix` beside `Mix`; `Mix` calls it when the switch is on). Computed where `Mix` is computed now (the `Turn` constructor), once per think. Names no faction or unit.

Inputs: E = the model entries of B.3 (implied counts included), in ordinal order; P = the pool of C.3; O = `FightPredictor.Group` of own army units that are complete (`IsArmy`, not under construction) and are not the current scout (`ScoutId`), with their real health; base composition `w_base`; rare stock `R = view.Rare` (resource units, bank only); budget `B = mix_eval_budget_common`.

1. **Value.** `v(u) = cost_common(u) + counter_rare_cost_weight x cost_rare(u)` (Fix64). `V(O) = sum over O of Count x v`; `V(E) = sum over E of Count x v`. Sums in ordinal entry order.
2. **Nothing to score.** E empty: k = 0 and the mix is the base composition scaled (C.4 step 4 with k = 0). `V(E)` is above 0 whenever E is not empty (every army unit costs something; a definition with cost 0 is skipped from E for this sum and the scaling, and if that leaves E empty, step 2 applies).
3. **Equal-value enemy.** `f = (V(O) + B) / V(E)`. `E_f` = E with each entry's Count and Health multiplied by f (order kept). The question asked is "which unit, added to my army, makes it beat the enemy's composition at equal value", so composition decides, not size.
4. **Candidate armies.** For each u in P, ordinal: `n(u) = B / cost_common(u)` when `cost_rare(u) = 0`; else `n(u) = min(B / cost_common(u), (R + mix_rare_allowance) / cost_rare(u))`. A pool unit with `cost_common` 0 uses only the rare term; with both costs 0 it gets `a(u) = 0`. `O_u` = O with u's entry Count + n(u) and Health + n(u) x MaxHealth(u) (a new entry inserted at its ordinal place when O has none). `a(u) = FightPredictor.Predict(O_u, E_f, aUphill: false, rules, PredictorSettings.From(profile)).Advantage`.
5. **Kept units and shares.** `top = max a(u)`. top = 0: nothing kept. Kept: `a(u) > 0` and `a(u) >= counter_keep_fraction x top`. Weight `w(u) = a(u) - counter_keep_fraction x top` for kept u; if the kept weights sum to 0 (only possible with `counter_keep_fraction` 1), each kept unit gets weight 1. `s(u) = w(u) / sum of kept weights`; 0 for the others.
6. **Confidence.** `k = counter_weight x min(1, S_seen / counter_full_confidence_supply)`, where `S_seen = sum over tracked types t of Eff(t) x supply(t)` (B.3 `Eff`, implied counts excluded); k = 0 when nothing is kept. Implied entries still shape E (a known Roost still argues for anti-air) but never raise confidence.
7. **Mix.** C.4 step 4 unchanged: `mix(u) = floor(mix_resolution x ((1 - k) x w_base(u) / sum w_base + k x s(u)))` with the existing `MixTolerance`; these integers replace `composition` for rules P, X, T and RS.

Determinism: Fix64 only; every sum in ordinal entry order; P, O and E in ordinal order; the divisions in the order written; no random draws; `Predict` is pure. Ties need no rule (proportional shares; flooring per unit in ordinal order). Cost per think: at most |P| predictions (9 in the shipped data), each linear in the entry counts.

Why this is honest: the advantage of `O_u` against `E_f` is the predictor's own answer to "which purchase wins this fight", including the existing army (an army of Spoutlings stops needing Spoutlings, C2-2), range head start, shield domes and layers. It inherits the predictor's blind spots (C.1 "Not modelled": no focus fire, drench, splash), listed as Q-C2-5.

C.4's `Score` stays: it still chooses the static defence type (C.6 step 4) and the whole mix when `use_predictor_mix` is false.

### C2.3 Rule G: rare-worker cap (when `rare_bank_soft_cap` > 0)

While `view.Rare >= rare_bank_soft_cap`, `CapOf` returns 0 for rare fields. Every user of `CapOf` follows: the idle-worker target (below-cap test), rule G2's long-haul choice, rule G4's rebalance (rare-field workers are over cap and move, highest ID first, at most `rebalance_workers_per_think`, to the nearest served field below its cap; none below cap: they stay) and rule 4b's worker target (rare fields add 0). Rule G's fallback "nearest served field at all" skips fields whose cap is 0 unless every served field has cap 0. Below the cap everything is as in package C. Rule RS and rule P are unchanged.

### C2.4 Rule SD revisions

1. **Raid strength (C.6 step 1).** For a drop-off, `strength = (sum of supply of visible enemy army units within defend_radius_tiles of it) + (1 when a damage report within that radius is in RecentDamage)`. Activity when `strength >= raid_min_supply`. Everything else in step 1 (gap, log, raid position) is unchanged. `raid_min_supply` 1 reproduces C.6 exactly (one unit or one hit is activity).
2. **Value cap (C.6 step 2 only).** When `static_defence_max_value_share > 0`, step 2 builds only if `D + v(d) <= static_defence_max_value_share x A`: `D` = sum of `v` over own static defences complete or under construction (anywhere), `v(d)` = the value of the defence step 4 would build, `A` = sum of `v` over own complete army units (the scout included), `v` as in C2.2 step 1. Not met: no build this think (step 3 is still tried). Step 3 (unanswered layer) is never capped, so early water anti-air (Q7, C-13) is unaffected.

### C2.5 Rule RS and the early expansion (replaces C.7's clock part)

- **Rule RS (`use_rare_priority`)** keeps its test (rare-cost mix weight `>= rare_need_weight_share x mix_resolution` and no served field holds the rare resource) but now changes only rule E's target order (rare sites first, as C.7). It no longer lifts the clock gate.
- **Early expansion (`use_early_expansion`, new switch, independent of the counter-mix switches).** In rule E, `due = expand_at_seconds + expand_interval_seconds x DepotsPlaced` as in v2; when `use_early_expansion` is on and no served field holds the rare resource, `due = min(due, early_expansion_seconds)`. All other gates of rule E stay (threat, depot cap, one under construction, money through `TryBuild`).
- Why it reproduces the old profiles: in `abl_s2_blind` the mix is always the base composition, whose rare share 286 of 1000 is above 250, so the old RS test was "no served rare field"; with `use_early_expansion` true and `early_expansion_seconds` 0 the new gate is the same test and the target order is unchanged (C2-11 pins it). In `aggressive_s2` the old lift also needed the counter-mix's rare share to stay above 250; the new one does not, so `aggressive_s2` can change slightly (accepted: it is superseded by `aggressive_s2b`; its old ladders stay on record).
- C-14 is replaced by C2-10 (b) and (c).

### C2.6 Profile keys (root; every profile file under `rts/data/ai` gets every key)

| Key | Off value (every profile except `aggressive_s2b`; see C2.7 for `use_early_expansion`) | `aggressive_s2b` | Unit | Range | Meaning |
| --- | --- | --- | --- | --- | --- |
| `use_predictor_mix` | false | true | boolean | requires `use_counter_mix` | C2.2 replaces C.4 steps 1 to 3. |
| `mix_eval_budget_common` | 600 | 600 | resource units | 50 to 100000 | B of C2.2: the purchase each candidate is valued by. Placeholder. |
| `mix_rare_allowance` | 100 | 100 | resource units | 0 to 100000 | Rare a candidate may plan beyond the bank (so rare units are valued before the first rare arrives, at most this much). Placeholder. |
| `rare_bank_soft_cap` | 0 | 300 | resource units | 0 to 100000; 0 = off | C2.3: rare fields take no workers while the rare bank is at or above this. Placeholder. |
| `raid_min_supply` | 1 | 3 | supply | 1 to 200 | C2.4 step 1: raid strength that counts as a raid; 1 = package C. Placeholder. |
| `static_defence_max_value_share` | 0 | 0.15 | fraction | 0 to 10; 0 = off | C2.4 step 2: static defence value at most this share of the army value. Placeholder. |
| `use_early_expansion` | see C2.7 | true | boolean | | C2.5: the first rare depot's clock gate is `early_expansion_seconds`. |
| `early_expansion_seconds` | 0 | 0 | seconds | 0 to 1800 | C2.5: clock gate of rule E while no rare field is served. Placeholder. |

Changed values in `aggressive_s2b` only: `counter_keep_fraction` 0.9 (was 0.5; with predictor advantages, which are ratios near 1 in even fights, 0.5 would keep almost every unit) and `counter_rare_cost_weight` 1 (the value of rare in C2.2 step 1 and C2.4; 2 counted rare twice although it piles up unspent). Both placeholders.

Comments in the house format, for example `// Predictor mix: purchase each candidate unit is valued by. Unit: resource units (common). Range: 50 to 100000. Source: skirmish_ai_v3.md C2.2; placeholder.` `AiProfile.Parse` reads every new key with the `Require*` helpers, adds them to `RejectUnknownKeys`, and validates `use_predictor_mix` requires `use_counter_mix` (error names the file and both fields). Update the `AiProfile.cs` "Differences" list and the `AiBrain.cs` header (rules G, SD, E and the Package C paragraph) and the `AiCounter.cs` header.

### C2.7 Profiles

| File | Change |
| --- | --- |
| `aggressive_s2b.json` (new, id `aggressive_s2b`) | Copy of `aggressive_s2.json` with the `aggressive_s2b` column of C2.6 and the two changed values; every other value equal to `aggressive_s2` (including `static_defence_after_raids` 1, `counter_weight` 0.7 and the C.8 base compositions). Header and `$doc` say it is the package C2 candidate (listed in the 0.1 table). |
| `abl_s2_blind.json` | New keys at their off values, `use_early_expansion` true, `early_expansion_seconds` 0. Plays exactly as before (C2-11). |
| `aggressive_s2.json` | Same as `abl_s2_blind` for the new keys (`use_early_expansion` true, 0). |
| `aggressive.json`, `defensive.json`, `aggressive_s1.json`, `abl_s2_no_rare.json`, `rt_*.json` | New keys at their off values, `use_early_expansion` false (none of them lifted the clock before). |

### C2.8 Package C2 tests (`rts/tests/Sim.Tests/Ai/AiArmyMixTests.cs`, `AiExpansionTests` or the file holding the rule E tests, `SkirmishAiTests`)

Settings for C2-1 to C2-6 unless stated: shipped unit data and damage table, C.1 predictor settings, `mix_eval_budget_common` 600, `mix_rare_allowance` 100, `counter_keep_fraction` 0.9, `counter_rare_cost_weight` 1, `counter_weight` 0.7, `counter_full_confidence_supply` 8, `mix_resolution` 1000, C.8 base compositions, model counts all seen (so `S_seen` = S_E), own army at full health. Tolerance 0.001 on advantages; mix integers exact.

- **C2-1 Water against 6 Pummelers, nothing owned, rare 0.** V(E) 600, f 1. n: Pummeler 6, Ink Lobber 2.5 (100 / 40), Reef Runner 8, Shellback 3.4286, Spoutling 4. a: Pummeler 1.0000 (12.2222 s each way; a tie), Ink Lobber 0.1736, Reef Runner 0.4878, Shellback 0.5514, Spoutling 0.1228. Kept: Pummeler only. S_seen 12, k 0.7. Mix: grasper 850, shellback 50, ink_lobber 50, spoutling 50, reef_runner 0. With rare 400 (n: Ink Lobber 4, Spoutling 6; a 0.4728 and 0.2823): same mix. (C.4 gave Shellbacks and Reef Runners a share here.)
- **C2-2 Water against 4 Roc Riders, rare 0.** V(E) 800, f 0.75. a: Pummeler 0.0397, Ink Lobber 0.0119, Reef Runner 0.0225, Shellback 0.0778 (each 600 s to kill: unhittable armed Rocs), Spoutling 0.3333 (n 4; 24.0 s against 8.0 s). Kept: Spoutling. Mix: grasper 150, shellback 50, ink_lobber 50, spoutling 750, reef_runner 0. **C2-2b**, same with 4 own Spoutlings: V(O) 500, f 1.375; a: Pummeler 0.4038, Ink Lobber 0.1877, Reef Runner 0.2692, Shellback 0.6708, Spoutling 0.3967; kept Shellback; mix grasper 150, shellback 750, ink_lobber 50, spoutling 50, reef_runner 0 (the own army already answers air, so the predictor now adds health in front of it).
- **C2-3 Skai against 6 Pummelers, rare 0.** n: Roc Rider 2 (100 / 50), Stormcaller 2.5, Talon 6, Windrunner 8. a: Roc Rider 16.4921 (36.3810 s against 600 s), Stormcaller 0.1281, Talon 0.4500, Windrunner 0.4878. Kept: Roc Rider. Mix: windrunner 128, talon_archer 85, roc_rider 742, stormcaller 42 (same as C-9).
- **C2-4 Skai against 8 Windrunners, rare 0.** a: Roc Rider 0.3505, Stormcaller 0.1516, Talon 0.9545, Windrunner 1.0000. Kept: Windrunner (weight 0.1) and Talon (0.0545); s 0.6471 and 0.3529. S_seen 8, k 0.7. Mix: windrunner 581, talon_archer 332, roc_rider 42, stormcaller 42.
- **C2-5 The traced situation.** Water, own army 5 Pummelers, 2 Shellbacks, 1 Spoutling (V(O) 975); model 3 Pummelers, 1 Shellback, 1 Ink Lobber, 1 Spoutling, 1 Reef Runner (V(E) 865, S_seen 14); rare 0. f = 1575 / 865 = 1.8208. a: Pummeler 1.5376, Ink Lobber 0.8568, Reef Runner 1.1546, Shellback 1.3267, Spoutling 0.7932. Kept: Pummeler (1.3267 < 0.9 x 1.5376 = 1.3838). Mix: grasper 850, shellback 50, ink_lobber 50, spoutling 50, reef_runner 0. (C.4 on the same model kept Pummeler, Shellback and Reef Runner.)
- **C2-6 Confidence from sightings only.** Water; model = a known enemy Tidepool and Shell Forge, nothing seen (implied 1 each: S_E 10, S_seen 0): k 0, mix = base: grasper 500, shellback 166, ink_lobber 166, spoutling 166, reef_runner 0. Same with 2 Pummelers seen (S_seen 4): k 0.35. With `use_predictor_mix` false the old C.4 path still gives k 0.7 here (C-8 to C-10 unchanged).
- **C2-7 Rare-worker cap.** `rare_bank_soft_cap` 300; one served common field with 1 worker (cap 3), one served rare field with 2 workers, `rebalance_workers_per_think` 2. Rare 300: both rare workers get `Gather` to the common field, the higher ID first; an idle worker goes to the common field; rule 4b's target counts 3 for the two fields (not 5). Rare 299: no move, target 5. Cap 0 (off): no move at any rare stock.
- **C2-8 Raid strength.** `raid_min_supply` 3, `static_defence_after_raids` 1, no value cap. One enemy Reef Runner (supply 1) 5 tiles from the HQ and a hit on the HQ 1 s ago: strength 2, no raid logged, no build. Two enemy Windrunners and a hit: strength 3, raid logged, `BuildStructure` of the faction defence next to the HQ. `raid_min_supply` 1: the lone Reef Runner is a raid (package C behaviour, C-12 unchanged).
- **C2-9 Value cap.** Water, `static_defence_max_value_share` 0.15, a logged raid that calls for a defence (cost 100). Own army 6 Pummelers (A 600): 90 < 100, no build. 7 Pummelers (700): 105, build. One own turret already standing and 13 Pummelers: 0.15 x 1300 = 195 < 200, no build; 14 Pummelers: 210, build. Share 0: builds with no army. Step 3 is not capped: C-13 with no army still orders the turret.
- **C2-10 Early expansion.** greybox_1v1 P0, enough common, no threat. (a) `use_early_expansion` true, `early_expansion_seconds` 0, no rare field served, mix with rare share 400: a depot at the rare site (field 10's depot site, as C-14) on the first think that can pay. (b) `use_early_expansion` false, `use_rare_priority` true, same mix, 100 s: no depot; at 240 s the depot goes to the rare site (RS reorders, no longer lifts the clock). (c) `use_early_expansion` true, `early_expansion_seconds` 60: none at 59.5 s, one at 60 s. (d) as (a) but a rare field is already served: no depot before 240 s.
- **C2-11 `abl_s2_blind` unchanged.** Before changing any code, the builder runs `--trace --map prototype_1v1 --matchup water_vs_water --seed 20261117 --profile-a abl_s2_blind --profile-b abl_s2_blind --game 0` and pins the final state hash and end tick as constants in a test; after the change the same run gives the same hash and tick. A-11 stays green.
- **C2-12 Switch off.** With `use_predictor_mix` false every C-8 to C-10 number is unchanged; with `rare_bank_soft_cap` 0, `raid_min_supply` 1 and `static_defence_max_value_share` 0, C-11 to C-13 are unchanged.
- **C2-13 Determinism and purity.** The same view, profile, memory copy and seed twice: identical mix, intents and memory; `rng.DrawCount` unchanged by C2; O built from units given in a shuffled order gives a bit-identical mix (Group sorts); no `float`, `double` or `decimal` in changed files (the existing grep test).
- **C2-14 Validation.** `use_predictor_mix` true with `use_counter_mix` false: error naming the file and both fields; a profile missing `raid_min_supply`: error naming it; `aggressive_s2b` loads; the shipped-profile list test includes it.
- **C2-15 C.1 text.** C-2 and C-6 pass unchanged; the "Deviation" comment is gone from `FightPredictor.cs`.

### C2.9 Gate

1. `dotnet build` and `dotnet test` green; C2-1 to C2-15 and A-11 pass; `sim-reviewer` pass on the `rts/src/Ai` diff.
2. Ladder: `dotnet run -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a abl_s2_blind --profile-b aggressive_s2b --out rts/docs/reports/ai_v3_2026-11-17/ladder_c2_s2b_vs_blind`. Passes when at least 200 games are decided, the `fraction` in the `LADDER` line is at least 0.600, and every one of the 12 (map, matchup) cells in `ladder.md` has a candidate fraction of decided games of at least 0.350. `ai_eval.json`'s bar stays 0.65, so exit code 2 is expected and accepted when these three hold; do not edit `ai_eval.json` (Q-C2-4).
3. Response: `--response --profile aggressive_s2b` gives `RESPONSE_SUMMARY passed=` 6 or more of 10. Likely passes: both `air_answer` (C2-2), `armoured_answer` Skai (C2-3), `trade` water (passed in package C). Uncertain: `trade` Skai (0.847 in package C; C2-4 now keeps Windrunners and Talons), and the two `raid` files (the four-Windrunner waves, strength 4 or 5, still log raids under C2.4, but package C failed part 1 because the check measures the drop-off nearest the wave spawn and the defence went next to the HQ). Expected to fail: `armoured_answer` water (by design, Q-C2-1) and both `turtle` (package D). If fewer than 6 pass, the builder reports each failing line with its metric; it does not change the checks.
4. If the gate is missed, the builder may change only these `aggressive_s2b` values: `counter_keep_fraction`, `counter_weight`, `mix_eval_budget_common`, `mix_rare_allowance`, `rare_bank_soft_cap`, `raid_min_supply`, `static_defence_max_value_share`; at most three full ladders; every run's numbers go in the report with what changed and why. No change to `abl_s2_blind`, the combat or unit data, or the response checks.
5. The report (`rts/docs/reports/ai_v3_2026-11-17/README.md`, new section) holds the ladder line, the 12 cells, the response summary and one traced water mirror of `aggressive_s2b` against `abl_s2_blind` on prototype_1v1 with the same columns as the C2.1 table.

### C2.10 Out of scope

- Package D, the response check definitions, combat and unit data, `ai_eval.json`.
- Predictor model changes (focus fire, drench, splash); C2.2 uses `Predict` as it is.
- Hysteresis or smoothing of the mix (finding 5 found no swing).
- Any `rts/src/Sim` change.

### C2.11 Questions for the developer (the builder uses the defaults)

- **Q-C2-1. The water `armoured_answer` check wants the wrong answer.** It counts as an answer the unit with the highest damage multiplier against armoured (Ink Lobber, Spoutling), but in the simulation 15 Pummelers beat 8 Ink Lobbers at equal cost in both seats, and the predictor agrees (3.417). Recommended: redefine an answer unit as the pool unit with the highest C2.2 advantage against the scenario's enemy at equal value (computed by the harness from the same data); default until you reply: leave the check alone, it fails for water, and the gate counts 6 of the other 9.
- **Q-C2-2. Is the 30-second rare depot part of the stage-2 AI?** It is now an explicit switch. Recommended: on in `aggressive_s2b` (the same economy as `abl_s2_blind`, so the ladder measures reaction alone) and decided again with P-004 stage 4 (openings) for later profiles.
- **Q-C2-3. The rare-worker cap is an economy rule, not a counter rule.** It is here because the traces show rare piling up unused. Recommended: keep it in `aggressive_s2b`, 300 placeholder.
- **Q-C2-4. Gate bar.** This spec gates `aggressive_s2b` at 60 percent with a 35 percent cell floor, while `ai_eval.json` says 0.65. Recommended: keep 0.65 in the file for packages C and D and read the 0.60 bar from the ladder line for this gate only.
- **Q-C2-5. Predictor blind spots.** No focus fire means the predictor values health in front of damage (C2-2b adds Shellbacks to Spoutlings against Roc Riders, which focused Roc Riders would punish). Recommended: accept for C2; revisit with D.6.3's focus model in package D.
- **Q-C2-6. Static defence value cap exempts the unanswered-layer case.** Recommended: yes, so water still gets a Coral Turret against early Roc Riders before its first rare (Q7).

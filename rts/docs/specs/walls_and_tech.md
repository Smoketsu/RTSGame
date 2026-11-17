<!-- rts/docs/specs/walls_and_tech.md -->
<!-- Build spec for P-005 developer decisions 5 and 8 (18 Nov 2026): worker-built walls (package W), a data-driven prerequisite list with one tech building per faction (package T), and, as an option that is NOT approved for build, a flyer bound to flood water (package F). Mechanics and data shape only; every number is a placeholder. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by the sim-reviewer after each package. -->
# Spec: walls, tech prerequisites, and a flood-bound flyer (option)

Written 18 Nov 2026 by the designer, against `review/combat-mechanics-2` at `b6d20dc` (combat mechanics packages 1 to 4 built). Sources: `proposals/P-005-roster-holes-and-ai-for-balance.md` (developer decisions 5 and 8), `specs/worker_construction.md`, `specs/combat_mechanics.md`, `specs/flooding.md` (walls in water), `specs/planetary_cycles.md` (Quake: "Walls and defences take damage"; review item 10, the shared `Wall` flag), `specs/statuses.md` (Grounded: "walls block it"), `art/CONCEPT_BRIEF.md` (building jobs: Wall = Wind Baffle / Coral Wall, Tech = Storm Spire / Reef Workshop), `rts/src/Sim/Terrain/TerrainTile.cs` (the `TileBlockers` enum lives here; there is no `TileBlockers.cs`), `Economy/EconomySystem.cs`, `Commands/EconomyCommands.cs`, `Ai/AiWorldView.cs`, `Ai/AiBrain.cs`.

## 0. Ground rules for all packages

- **Mechanics first, numbers last** (developer, 18 Nov). Every number below is a **placeholder** worked out by the rule stated next to it. Data comments say `Source: placeholder, developer to confirm (rts/docs/specs/walls_and_tech.md <section>).` No existing number changes.
- **Game names as data names.** New IDs use the concept brief's names as they stand: `skai.wind_baffle`, `water.coral_wall`, `skai.storm_spire`, `water.reef_workshop`. The developer renames at the end.
- **No new step, no new command, no new codec.** W runs in the existing spawn and removal hooks (step 2 scripts, step 6d deaths, step 7 placement) and in step 9 (quake damage). T runs in step 2 only. The step numbers in the `Simulation.cs` header do not change; each package adds one clause to it.
- **Packages are independent.** W and T touch different code except one line each in `UnitEconomyDefinition` (constructor argument and `WriteHash`). Each appends its field **at the end** of `UnitEconomyDefinition.WriteHash` when it is built; expected order is W then T (`... ProvidesSupply, IsWall, Requires`). Either order is valid because the digest moves anyway.
- **Pins.** Both W and T add unit files, so the content digest and every pinned hash move (smoke, economy_smoke, `GoldenFinalHash`, `V2ReferenceHash`, `BlindTraceHash`). Re-pin as `combat_mechanics.md` section 5 describes, with comments that name this spec and the package. W is **hash only** everywhere (the AI never builds walls; if an AI outcome moves, stop and report). T may move the AI outcomes (the AI must now build a tech building before Thunderheads and Leviathans): write the old and new `OUTCOME` lines in the report and the re-pin comment. Older replays are refused by the content digest (ADR-009), no format or protocol bump.
- **Test setups.** "Economy setup" = `TestWorld.StartedEconomy()` as in `worker_construction.md` 11.1: worker speed 4 tiles/s (0.2 tiles/tick), `worker_placement_seconds` 2.0 (P = 40 ticks), reach = 1.5 + structure radius 0.5 = 2.0, P0 starts with 200 common, 20 ticks/s. "Combat setup" = `combat_mechanics.md` section 0: 24 x 24 flat open map, no cycles, two players on different teams, mechanics `none`, separation off, shipped damage table (kinetic 1.0 / 0.75 / 0.5, blast 0.75 / 1.0 / 1.25 against light / armoured / structure), hand-built definitions. New test definitions go in test-local catalogs (a copy of `TestWorld.Catalog()` plus additions), **not** into `TestWorld.Catalog()`. `GoldenFinalHash` reads TestWorld, not shipped data; it still moves (hash only) because each package appends a field to `UnitEconomyDefinition.WriteHash`, but not because of the test definitions.

---

## Package W: walls

### W.1 Rules

A **wall** is a structure whose definition has `is_wall: true`. It is built, paid, cancelled and refunded exactly like any structure under `worker_construction.md` (one worker, one tile per order, walk, `worker_placement_seconds`, then `build_seconds` alone). Everything below that is not listed as new follows from existing code; it is listed so the builder writes no rule for it.

1. **New blocker flag.** A wall's tile gets `TileBlockers.BuiltWall = 4` (new value in `Terrain/TerrainTile.cs`) instead of `Footprint`, added by `EconomySystem.OnUnitSpawned` at spawn (so a wall under construction blocks at once, like every structure today) and removed by `EconomySystem.OnUnitRemoved`, unless another unit with footprint 1 **and the same `IsWall` value** stands on the same tile. Why a third flag and not `Wall` or `Footprint`:
   - `Footprint` displaces flood water (footprints stay dry); the design says "walls can stand in shallow water and block the flow" (`flooding.md` section 1).
   - `Wall` is treated as public map knowledge by the AI view (quake paths) and is opened by `CycleSystem` path logic; a player's wall must be hidden in fog and owned by its unit (this also settles `planetary_cycles.md` review item 10 for player walls).
2. **Movement (new: nothing).** `TerrainTile.IsBlocked` is `Blockers != None`, so ground and water movement, `IsStandable`, `IsStepPassable` (no corner cutting), spawn search and `StandablePointNear` already treat `BuiltWall` as impassable. Air ignores it.
3. **Pathing on placement and death (new: nothing).** `AddBlockers`/`RemoveBlockers` raise `TerrainChangedEvent` and bump `TerrainGrid.Version`; `FlowFieldCache` rebuilds on the next `Get`. So a moving unit re-paths on the first movement step after the change: a wall placed in step 7 of tick t is seen by movement on t + 1; a wall spawned by a script in step 2 of tick t is seen on t; a wall killed in step 6d of tick t opens on t + 1. A unit whose goal becomes unreachable stops with `MoveUnreachableEvent` (existing `MovementSystem` rule) and does **not** resume when the wall later dies (Q-W1).
4. **Placement rules (new: none beyond "not a static defence").** The existing `build_structure` checks apply: explored site, `tile_blocked`, `tile_occupied` (any ground or water unit, own or enemy, on the tile), `site_pending`, cost, supply; the site check runs again on arrival and on the spawn tick. A wall is not a static defence, so it needs no base (`no_base` does not apply) and does not count toward `static_defences_per_base_max`. There is no per-base or per-player wall cap (Q-W4). A wall may be placed on a flooded tile; the water stays (rule 5).
5. **Sealing in.** A wall can never be placed on a tile where a ground or water unit stands (rule 4). There is **no enclosure check**: walls may close any area, including around own or enemy units and around an HQ. Every wall can be destroyed by any enemy weapon that targets ground (rule 7). Reason: an enclosure test needs a region search per placement and a definition of what may not be enclosed, which the design does not give. Own units sealed in by own walls cannot attack them (`target_not_enemy`, unchanged); see Q-W3.
6. **Flood water (one line in `FloodingMechanic.ReadField`).** `field.Wall[i] = (tile.Blockers & (TileBlockers.Wall | TileBlockers.BuiltWall)) != 0`. So exactly as `flooding.md` specifies for `Wall`: water never flows into a wall tile, water already on it is not displaced and does not flow out, waves wash over it without filling it, and the wave push stops before it. Applies to every team's water, the owner's included (Q-W8). When the wall dies, the water on its tile flows again from the next flood update.
7. **Combat (new: nothing).** A wall has footprint 1, so `CombatSystem.IsStructureTarget` is true: its armour row (shipped walls use `structure`) and the attacker's `structure_damage_multiplier` apply. It is a ground-layer unit, so ground and water weapons and **ground blasts** hit it (enemy walls only, no friendly fire), with the splash part of `combat_mechanics.md` 1.1 step 6. Air-only weapons cannot attack it. It has no weapon. Automatic acquisition is armed-first, so an armed enemy in range is always chosen before a wall; with no armed enemy in reach, an attack-moving or idle unit shoots a wall in reach as it does any unarmed structure.
8. **Flyers.** Flyers pass over walls. A flyer Grounded over a wall tile is moved off by the existing landing rule (`statuses.md`, "Landing": Chebyshev rings, ascending index, first standable tile). A Grounded flyer is then blocked by walls like any ground unit. Downdraft and Drench need no change.
9. **Vision.** Walls do not block sight (there is no line of sight in the game; `fog_of_war.md` defers it). A wall sees its own `sight_range_tiles` like any unit.
10. **Quake (one condition in `CycleSystem.QuakeDamage`).** The design says "Walls and defences take damage" and `planetary_cycles.md` says wall structures join the set when they exist. The skip test becomes `!(IsStaticDefence || IsWall) || ...`; amount, floor and event are unchanged. Quake path closing already skips any blocked tile (`CycleSystem` line 622, `IsBlocked`), so a player wall on a path tile keeps the path open on that tile, and a closed path tile rejects a wall order with `tile_blocked`.
11. **Rooted HQ floor.** Unchanged. The harness floor trials (`balance_harness.md`, `micro_benchmark.md` F0: 15 s) spawn the HQ and the attackers with no walls, so the floor still measures the bare HQ. Walls add protection the floor does not count. Walls do not change HQ armour or `IsStructureTarget` of the HQ.
12. **Supply.** Shipped walls have `supply` 0 and `provides_supply` 0.
13. **Gates.** None (the design does not need them; Q-W2 covers getting own units out).
14. **The two factions.** The brief gives an art reason only (Skai: conifer-style, feathers pointing up; water: reef coral). Both walls have identical rules and numbers. Water's floods and wave interact with walls through rule 6, which gives water its own uses (channelling its flood) without a rule difference. Q-W6.

### W.2 Data

Unit file key (`UnitSchema.ReadEconomy`, add to the root `RejectUnknownKeys` list; `UnitEconomyDefinition.IsWall`, optional constructor argument defaulting to false; appended to `UnitEconomyDefinition.WriteHash`):
```
// Marks a wall: its tile is blocked with the wall flag (blocks ground and water movement and flood flow, keeps the water already on the tile), and Quake damages it like a static defence. Unit: boolean. Range: true or false (false when absent; true needs footprint 1). Source: P-005 developer decision 8 (18 Nov 2026); rts/docs/specs/walls_and_tech.md W.2.
"is_wall": true,
```
Loader: `is_wall: true` with `footprint` 0 or absent fails `"is_wall needs footprint 1"` naming the file and `is_wall`.

New files (headers and per-key comments per the documentation standard, copied from `perch_tower.json`'s comment style; every number placeholder):

| Key | `skai/wind_baffle.json` | `water/coral_wall.json` | Placeholder rule |
| --- | --- | --- | --- |
| `id`, `display_name` | `skai.wind_baffle`, "Wind Baffle" | `water.coral_wall`, "Coral Wall" | concept brief names |
| `kind`, `layer` | building, ground | building, ground | like every structure |
| `max_health` | 200 | 200 | two thirds of a Perch Tower (300) |
| `speed_tiles_per_second`, `supply`, `provides_supply` | 0, 0, 0 | 0, 0, 0 | walls cost no supply |
| `radius_tiles` | 0.5 | 0.5 | one tile |
| `cost_common`, `cost_rare` | 25, 0 | 25, 0 | a quarter of a static defence (100): "cheap" |
| `build_seconds` | 5 | 5 | a quarter of a static defence (20) |
| `footprint`, `is_wall` | 1, true | 1, true | |
| `armour_type` | structure | structure | like every structure |
| `sight_range_tiles` | 3 | 3 | enough to see who is hitting it |
| weapon, trains | none | none | |

Add each ID to its faction's `units` list (`factions/skai.json`, `factions/water.json`).

### W.3 State, hash, commands, events

- No new per-unit or per-player state. The tile's `Blockers` byte is already hashed per tile (new value 4). `IsWall` is in the content digest.
- No new command (walls use `build_structure`; scripts may `spawn_unit` a wall, complete at once). No new event: `BuildOrderAcceptedEvent`, `StructurePlacedEvent`, `StructureCompletedEvent`, `UnitDamagedEvent`, `UnitRemovedEvent` and `TerrainChangedEvent` cover it.
- Documentation edits: `TerrainTile.cs` (enum value summary: "A wall structure stands on the tile; blocks movement and flood flow, keeps water already there; hidden in fog for AI views"), `TileBlockers` summary, `IsBlocked` summary; `Simulation.cs` header (spawn and removal hooks: "a structure's footprint or wall blocker is added and removed"; step 9: "quake damage to static defences and walls"); `EconomySystem.cs` header "Footprint" paragraph; `Terrain/README.md`; `rts/data/README.md`; append a dated note to ADR-006's `Blockers` bullet ("18 Nov 2026: third flag `BuiltWall` for player-built walls, walls_and_tech.md W.1").

### W.4 AI (minimum)

- The AI never builds walls with no code change: build order steps name no wall, `ProducerStructureFor` needs `trains`, rule SD needs `IsStaticDefence`, depots need `IsDropOff`, supply buildings need `provides_supply`. Test W-14 guards it.
- **Required fix (fog):** `AiWorldView` line 471 treats any `Wall` flag as known everywhere. Change to: `Wall` known everywhere (map-public quake paths); `Footprint` **or `BuiltWall`** only where the tile is visible. Otherwise the AI would see enemy walls through fog.
- Known limit, for a later AI package: the AI does not attack a wall that seals its route; a full wall-in stops its attack moves (`MoveUnreachableEvent`) and it stays put. AI-versus-AI games have no walls, so the harness, the ladder and the response suite are unaffected. Record "full wall-in against the AI" as a response scenario candidate once Q-W1 is answered.

### W.5 Unity (PC session)

- One model per wall (Wind Baffle, Coral Wall) on its tile, with a health bar; connecting segments between orthogonally adjacent walls of the same owner are chosen in Unity from unit positions (presentation only). The terrain view already darkens `IsBlocked` tiles (`TerrainView.cs` line 335), which covers the new flag.
- Build UI: a wall button in the worker build menu; a dragged line becomes one `build_structure` per tile, spread over the selected workers (nearest worker per tile, each worker one order at a time). Unity may keep a client-side list and issue a worker's next tile when `structure_placed` or `build_order_cancelled` arrives for it; that is player input, not simulation state.
- Show the reason of a cancelled wall order (`site_occupied` is common when units stand in a line).

### W.6 Acceptance tests (`rts/tests/Sim.Tests/Economy/WallTests.cs`)

Test wall definition `test.wall`: ground, building, max health 200, armour `structure` (combat setup) or `armoured` (economy setup, whose test table has no `structure` row), radius 0.5, cost 25, build 5 s (100 ticks), footprint 1, `is_wall` true, sight 3, supply 0. Ground mover: speed 4 (0.2 tiles/tick), radius 0.5. Walls for movement tests are spawned complete by script `spawn_unit` (issuer 255) on 24 x 24 combat setup.

| # | Test | Setup and expected values |
| --- | --- | --- |
| W-1 | **Placed by a worker** | Economy setup, worker at (14, 14), wall order at tile (20, 14) on t0 (the layout of WC-1). `BuildOrderStartedEvent` on t0 + 22; `StructurePlacedEvent` on **t0 + 62**, health 200 x 0.1 = **20**; on that tick tile (20, 14) has `Blockers == BuiltWall` (not `Footprint`) and a `TerrainChangedEvent` for index 14 x map width + 20; `StructureCompletedEvent` on **t0 + 162**. Common 200 - 25 = **175**. `SupplyUsed` unchanged by the wall. |
| W-2 | **Placed mid-walk: stop** | Mover at (5.5, 10.5) ordered to (15.5, 10.5) on tick 0. After tick 9, x = 5.5 + 0.2 x 10 = **7.5**. On tick 10, 24 `spawn_unit` walls at (10, 0) to (10, 23). On tick 10: `MoveUnreachableEvent` for the mover, x stays **7.5**, `HasMoveTarget` false. |
| W-3 | **Gap: re-route** | Same, but the column leaves out (10, 20) and the walls are spawned on tick 0. The mover's tile is never a wall tile on any tick; it is on tile (10, 20) on at least one tick; `UnitArrivedEvent` before tick 300. |
| W-4 | **Wall dies: opens** | W-2, then on tick 20 a `remove_unit` of the wall at (10, 10): on tick 20 `TerrainChangedEvent` for index 250 and the tile has no blocker; the mover has not moved (x 7.5, no auto-resume). New move order on tick 21: x = 7.5 + 0.2 x (k - 20) after tick k, so **10.5 on tick 35**; `UnitArrivedEvent` before tick 70. Combat variant: the wall killed by an enemy shooter; the blocker goes in 6d of the death tick. |
| W-5 | **Flood blocked, water kept** | On the `FloodingTests` setup, three tiles in a row A, B, C, with the tide not turning during the test. (a) Wall on B, 1000 drops of team 0 on A: after 4 flood updates A holds **1000**, B and C **0** (`flooding.md` test 4 through the real mechanic). (b) 500 drops on B, then a wall spawned on B: after 4 updates B still holds **500** and A, C hold 0. Control: a `test.tower` (footprint, not wall) spawned on B instead: B is **0** after the first update (displaced). |
| W-6 | **Armour and structure multiplier** | Combat setup. Enemy wall at (9.5, 10.5). Attacker weapon blast 12, `structure_damage_multiplier` 1.5, `hit_delay_ticks` 0: wall 200 - 12 x 1.25 x 1.5 = **177.5**. Kinetic 8, no multiplier: 200 - 8 x 0.5 = **196**. |
| W-7 | **Splash hits walls, enemies only** | `combat_mechanics.md` A1 geometry: attacker at (2.5, 10.5), blast 12, `splash_radius_tiles` 1, `structure_damage_multiplier` 1.5; primary light T at (8.5, 10.5); enemy wall at (9.5, 10.5) (distance 1.0 ≤ 1 + 0.5); own (attacker's) wall at (8.5, 11.5). T: 100 - 12 x 0.75 = **91**. Enemy wall: 200 - (12 x 0.5) x 1.25 x 1.5 = 200 - 11.25 = **188.75**. Own wall **200**. `BlastEvent.victims` **1**. |
| W-8 | **Flyers pass, Grounded flyers land beside** | Column of W-2 spawned on tick 0. Flyer speed 4 at (5.5, 10.5) ordered to (15.5, 10.5): x = 5.5 + 0.2 x 25 = **10.5 after tick 24** (over tile (10, 10)). Repeat; on tick 24 `apply_status` Grounded (applied in step 2, before that tick's movement, at x = 5.5 + 0.2 x 24 = 10.3, tile (10, 10)): the flyer lands at **(9.5, 9.5)** (ring 1 around (10, 10), lowest index (9, 9) = 225; (10, 9) = 226 is a wall), and on the same tick `MoveUnreachableEvent` (the column is closed to ground). |
| W-9 | **Vision not blocked** | Column of W-2. P0 mover (sight 8) at (5.5, 10.5): tile (12, 10) is visible to team 0 after the next vision recompute. |
| W-10 | **Placement rules** | Economy setup. Wall order on a tile with an enemy `TankId`: `tile_occupied`. On a wall tile: `tile_blocked`. Outside every base (no drop-off within 12 tiles): accepted (no `no_base`). Base with six standing towers: a wall order in it is accepted (cap not counted), and a seventh tower is still rejected `static_defence_cap`. On a tile holding water: accepted, and after placement the water depth on the tile is unchanged. |
| W-11 | **Sealing allowed** | Combat setup. Enemy mover at (5.5, 5.5). Walls spawned on its 8 neighbours: all accepted. Its move order to (15.5, 5.5): `MoveUnreachableEvent` on the order tick. |
| W-12 | **Quake damages walls** | Copy `CycleEffectsTests.Quake_DamagesStaticDefencesOncePerSecondDownToTheFloor` with a wall (max health 200) beside the defence: the wall takes 200 x `defence_damage_per_second_fraction` per second, down to 200 x `defence_damage_min_health_fraction`, on the same ticks as the defence, with `UnitDamagedEvent` attacker None. |
| W-13 | **Blocker kind on removal** | Two units with footprint 1 on one tile through `spawn_unit` (a wall and a tower): the tile has `BuiltWall | Footprint`. Remove the tower: `BuiltWall` stays, `Footprint` goes. Remove the wall: no blocker. |
| W-14 | **AI view and AI behaviour** | An enemy wall on a tile the AI's team does not see: `AiWorldView` reports the tile not blocked; once visible, blocked. A quake-path `Wall` tile is blocked whether seen or not. Over a full `ai_vs_ai.json` run no `build_structure` names a wall definition, and the exit test still has zero rejections. |
| W-15 | **Loader** | `is_wall: true` with `footprint` 0 is rejected naming the file and `is_wall`. Shipped Wind Baffle and Coral Wall load with `IsWall` true and the W.2 values; no other shipped unit has it. |
| W-16 | **Determinism** | Two identical runs of W-4 give equal hashes at every hash tick; a run with the wall at (10, 11) instead of (10, 10) differs at the first hash tick after the spawn. `TerrainTile.WriteHash` writes `(byte)Blockers` = 4 for a wall tile. |

---

## Package T: tech building and prerequisites

### T.1 Rules

1. **`requires`** is an optional list on any unit or structure definition: definition IDs of buildings (footprint 1) of the same faction. A player **meets** a requirement `R` when the player owns a registered unit with `Definition.Id == R`, `!IsUnderConstruction` and `Health > 0`. Allies' and enemies' buildings never count (Q-T4).
2. **Check points, and only these:**
   - `TrainUnitCommand.Apply`: new rejection **`missing_prerequisite`** after `hq_not_rooted` and before `queue_full`.
   - `BuildStructureCommand.Apply`: new rejection **`missing_prerequisite`** after `wrong_faction` and before `out_of_bounds` (it reads only own buildings, so its position reveals nothing about fog).
   - `spawn_unit`, `StandardMatchStart` and anything else that spawns directly ignore `requires` (scripts, the harness `--fights` mode and tests keep working).
3. **When a prerequisite dies** (Q-T2): nothing is undone. Units already queued keep their place and finish (the check is at queue time only), pending build orders of a gated structure go on and place it, and units and structures already built are unaffected. Only new `train_unit` and `build_structure` commands are rejected until the player again has a complete one. Reason: there is no command to cancel a queue entry, so a paused head would block the producer forever; and this needs no new state.
4. **Public query** for Unity, the AI and tools, in `EconomyRules`: `public static string? FirstMissingRequirement(Simulation sim, PlayerId owner, UnitDefinition definition)`: the first entry of `definition.Economy.Requires`, in file order, that `owner` does not meet; null when all are met (or the list is empty). One scan of `sim.Units.All` per entry; no allocation. Both commands use it.
5. **The tech buildings** do nothing else: no `trains`, no supply, no weapon, no research (Q-T3). Their only job is to exist.
6. **Smallest gating** (current producers: Windspire trains Talon Marksman, Stormcaller, Thunderhead; Shell Forge trains Shellback, Brinecaster, Leviathan; Roost trains Windrunner and Roc Rider): add `"requires": ["skai.storm_spire"]` to `skai/thunderhead.json` and `"requires": ["water.reef_workshop"]` to `water/leviathan.json`. Producers keep their `trains` lists. The tech buildings themselves have no `requires`, so the top unit needs its producer plus one more building (P-005 rule 8, "switching costs time"). The Roc Rider stays ungated (Q-T1).

### T.2 Data

Unit file key at the root (`UnitSchema.ReadEconomy` like `trains`; `UnitEconomyDefinition.Requires`, `IReadOnlyList<string>`, optional constructor argument defaulting to empty; appended to `WriteHash` as count then each ID in file order):
```
// Buildings the owner must have standing and complete to train or build this; checked only when the order is given. Unit: list of unit IDs. Range: 0 to 8 distinct IDs of buildings (footprint 1) of this unit's faction, never this unit, no cycles (empty when absent). Source: P-005 developer decision 8 (18 Nov 2026); choice of gated units is a placeholder, developer to confirm (rts/docs/specs/walls_and_tech.md T.2).
"requires": ["skai.storm_spire"],
```
Loader (each failure is a `DataValidationException` naming the unit's file and `requires`, or `requires[i]`):
- Per file (`UnitSchema`): not an array of strings; more than 8 entries; a duplicate ID ("lists 'x' twice"); the unit's own ID ("requires itself").
- In `DataLoader.CheckReferences`, after the `trains` check: an unknown ID ("names the unit 'x', which has no file in units/"); a unit of another faction ("names 'x' of faction 'f'; requirements must be of the same faction"); a unit with footprint 0 ("names 'x', which is not a building (footprint 1)").
- New `DataLoader.CheckRequirementCycles(content, unitFiles)`, after `CheckReferences`: depth-first search over the `requires` graph, starting nodes in ordinal ID order, neighbours in file order, three colours. On the first back edge, throw naming the file of the **ordinally lowest ID on the cycle** and the message `"requirement cycle: a -> b -> a"`, the path written starting from that lowest ID. Self-reference is caught earlier.

New files (every number placeholder):

| Key | `skai/storm_spire.json` | `water/reef_workshop.json` | Placeholder rule |
| --- | --- | --- | --- |
| `id`, `display_name` | `skai.storm_spire`, "Storm Spire" | `water.reef_workshop`, "Reef Workshop" | concept brief names |
| `kind`, `layer` | building, ground | building, ground | |
| `max_health` | 400 | 400 | a producer (Windspire, Shell Forge) |
| `speed_tiles_per_second`, `supply`, `provides_supply` | 0, 0, 0 | 0, 0, 0 | |
| `radius_tiles`, `footprint` | 0.5, 1 | 0.5, 1 | |
| `cost_common`, `cost_rare` | 150, 50 | 150, 50 | a producer's common cost; the rare cost of a Roc Rider, so the tech is a rare-resource decision |
| `build_seconds` | 30 | 30 | a producer (25) plus a fifth, rounded to 5 |
| `armour_type`, `sight_range_tiles` | structure, 8 | structure, 8 | like a producer |
| `trains`, weapon, `requires` | none | none | |

Add both IDs to the faction `units` lists. Update the `$doc` of Thunderhead and Leviathan: "needs a Storm Spire" / "needs a Reef Workshop".

### T.3 State, hash, commands, events

- No new runtime state. `Requires` is in the content digest. No new command, no new event: the rejection travels in the existing `CommandRejectedEvent` with reason `missing_prerequisite`.
- Documentation: `Simulation.cs` header step 2 ("train and build orders need the definition's `requires` buildings, standing and complete"); the rejection lists in the `TrainUnitCommand.Apply` and `BuildStructureCommand.Apply` summaries; `Economy/README.md`; `rts/data/README.md` (the `requires` key).

### T.4 AI (minimum: no rejections, and the counter-mix can still reach the top units)

The counter-mix already wants Thunderheads and Leviathans (`AiArmyMixTests`: weight 700). Without changes the AI would get `missing_prerequisite` and fail the exit test. Changes in `rts/src/Ai/AiBrain.cs`, all reading own units only (no hidden information):
1. Helper `bool RequirementsMet(UnitDefinition d)`: every `d.Economy.Requires` entry has an own `AiUnitView` with that definition ID that is not under construction. Pending build orders do not count.
2. `FindFreeProducer(unitId)` returns null when the unit's requirements are not met. This one change covers `Produce` (the unit is not a candidate; the deficit choice moves to the next unit) and the scout purchase.
3. `TryBuild(structure)` returns without emitting when the structure's requirements are not met.
4. **Rule T2**, inside `Tech()`, under the same `use_tech_on_demand` switch, run only when rule T emitted no build this think: among composition units with weight at least `tech_min_weight_share x mix_resolution`, in descending weight then ordinal ID, take the first whose producer exists (`AnyOwnTrains`) but whose requirements are not met. Let `R` be its first unmet requirement (file order). If no own unit in `_planned` (complete, under construction or pending order) has definition `R`, `TryBuild(R)`. Nothing else.
Update `AiArmyMixTests` line 786 (it expects a Thunderhead `Train` intent: give the AI a complete Storm Spire in that setup) and `MissingRosterTests` line 131 (training a Thunderhead and a Leviathan: spawn the tech building first). The sim-reviewer reviews the `rts/src/Ai` diff (precedent: AI v3 packages).

### T.5 Unity (PC session)

Production and build buttons of a gated item are disabled while `FirstMissingRequirement` returns an ID, with a tooltip naming that building's display name; a `missing_prerequisite` rejection shows the same text. The two tech buildings need models (Storm Spire, Reef Workshop: art round 13 drawings exist).

### T.6 Acceptance tests (`rts/tests/Sim.Tests/Economy/PrerequisiteTests.cs`)

Economy setup with a test-local catalog adding: `test.tech` (building, footprint 1, radius 0.5, cost 50, build 5 s = 100 ticks, max health 200, no requires); `test.gated` (mobile ground, speed 4, supply 1, cost 50, build 2 s = 40 ticks, `requires ["test.tech"]`); `test.gate_barracks` (building, footprint 1, trains `["test.gated", "test.runner"]`, build 5 s, cost 100, provides 8 supply). "Producer" = a `test.gate_barracks` spawned complete for P0 at (12, 16).

| # | Test | Setup and expected values |
| --- | --- | --- |
| T-1 | **Rejected without the building** | `train_unit(producer, test.gated)`: rejected `missing_prerequisite`; common stays **200**, queue empty. `test.tech` spawned complete for P0 at (16, 16); the same command next tick is accepted, common **150**. |
| T-2 | **Under construction does not count** | Worker at (14, 14) orders `test.tech` at (20, 14) on t0 (WC-1 layout): placed on t0 + 62, completed on **t0 + 162** (step 7). A train command on t0 + 162 (step 2, before completion) is rejected `missing_prerequisite`; on **t0 + 163** it is accepted. Common: 200 - 50 (tech) - 50 (unit) = **100**. |
| T-3 | **Only own buildings count** | A complete `test.tech` owned by P1 (enemy), and in a second run by an ally of P0: P0's train is rejected `missing_prerequisite` in both. |
| T-4 | **Prerequisite dies: queue finishes** | `test.tech` complete for P0. On tick T, two `train_unit(producer, test.gated)` (sequences s, s + 1): both accepted, common 200 - 100 = **100**, `SupplyUsed` +2. On T + 10, `remove_unit` of the tech. The first `test.gated` appears on **T + 39** (the command tick counts as the first of 40), the second on **T + 79**. A third train on T + 11 and one on T + 80 are rejected `missing_prerequisite`. A `test.runner` train on T + 80 is accepted (ungated units are unaffected). |
| T-5 | **Built units unaffected** | After T-4, both `test.gated` units accept move orders and keep their health; nothing is removed or changed on T + 10. |
| T-6 | **Gated structure, and rejection order** | Catalog also has `test.gated_tower` (footprint 1, cost 10, `requires ["test.tech"]`). Without the tech: a worker order at an out-of-map tile (-1, 5) is rejected `missing_prerequisite` (it comes before `out_of_bounds`), and so is an order at an unexplored tile (before `site_unexplored`). With the tech complete: the order is accepted; the tech is then removed while the worker walks; the tower is still placed (`StructurePlacedEvent`) and nothing is refunded. |
| T-7 | **Direct spawns ignore requires** | `spawn_unit` of `test.gated` for P0 with no tech: the unit exists. |
| T-8 | **Query** | `FirstMissingRequirement(sim, P0, test.gated)` is `"test.tech"` before, null after the tech completes. A definition with `requires ["test.tech", "test.barracks"]` and only the barracks complete returns `"test.tech"`; with neither, `"test.tech"` (file order). |
| T-9 | **Loader** | Each is rejected with the file and field: unknown ID; duplicate; self; a mobile unit (`test.runner`, footprint 0); a building of another faction; a two-cycle `a -> b -> a` (message `requirement cycle: a -> b -> a`, file of `a`, the lower ID); a three-cycle `c -> a -> b -> c` written starting from `a`; 9 entries. A chain with no cycle (`x -> y`, `y -> z`) loads. |
| T-10 | **Shipped data** | `skai.thunderhead` requires `["skai.storm_spire"]`, `water.leviathan` requires `["water.reef_workshop"]`; no other shipped unit has `requires`; both tech buildings are in their faction lists and load with the T.2 values. |
| T-11 | **Digest, not state** | Two catalogs differing only in one `requires` entry give different `ComputeDigest()`; `Unit.WriteHash` is unchanged (`HashCoverageTests` unchanged). |

AI tests (`rts/tests/Sim.Tests/Ai/AiPrerequisiteTests.cs`):

| # | Test | Assertion |
| --- | --- | --- |
| TA-1 | No gated training | Water AI with a complete Shell Forge, mix weight 700 on the Leviathan, money for everything, no Reef Workshop: the think emits no `Train` of the Leviathan; it trains another mix unit from the Shell Forge. |
| TA-2 | Rule T2 builds the tech | Same, `use_tech_on_demand` on: the think emits a `BuildStructure` of `water.reef_workshop`. With a pending order or one under construction: no second one. Switch off: none. |
| TA-3 | Trains once complete | With a complete Reef Workshop the think emits the Leviathan `Train`. |
| TA-4 | Exit test | `ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided` green with zero rejections; over the run no `missing_prerequisite` appears. |

---

## Package F: flood-bound water flyer (OPTION ONLY, NOT APPROVED FOR BUILD)

The developer said "Maybe" (decision 5). This section exists so the developer can decide; **no builder implements it** until the developer approves it. P-005 recommended against a water flyer.

- **Data.** An air unit (`layer: "air"`) of the water faction with a new boolean `flood_bound` in its flood block (`UnitFloodDefinition`, content digest). Placeholder ID `water.flood_flyer` (no name exists in the brief); producer, cost and weapon to be decided when approved.
- **Movement (default).** Air movement stays a straight line. In `MovementSystem`, a `flood_bound` unit whose next position lies on a tile that is not `IsFlooded` does not move: its order is cleared and `MoveUnreachableEvent` is emitted (it stops at the shoreline). Rejected alternative: a flow field over flooded tiles (paths along channels, but rebuilt whenever a depth class changes). A producer spawns it only on a flooded tile within `spawn_search_radius_tiles`; otherwise the head of the queue waits, as when no tile is free.
- **Water recedes under it.** Options: (a) **stranded = Grounded** while its tile is dry: step 3 calls `StatusSystem.ApplyGrounded` with source `"stranded"` every tick its tile is dry, so it lands, can walk on the ground layer, and lifts off at most `grounded_duration_seconds` after its tile is wet again; (b) it loses a share of health per second while over dry land; (c) it moves to the nearest flooded tile within N rings, else (a); (d) it dies. **Default: (a)**, because it reuses the Grounded status and every rule that already follows from it.
- **Interactions.** It is an Air target: `can_target_air_on_water` attackers hit it whenever they stand on water, which is wherever it can fly, so water's own anti-air on water and every air-capable unit counter it. Downdraft and Drench ground it as any flyer; when Grounded ends over a dry tile, (a) applies at once. Blasts reach it only from weapons whose `SplashLayers` include air.
- **Not specified until approved:** weapon, role in the matchups file, AI use, art.

---

## Determinism notes for the sim-reviewer

- W: blocker changes go through the existing `TerrainGrid` mutators in fixed order (spawn and removal hooks in their existing order); flow fields are a pure function of terrain. `OnUnitRemoved`'s same-tile scan is `sim.Units.All` (ascending ID). Flood reads one more flag bit. Quake damage keeps its ascending ID loop. No RNG, no new collection, Fix64 and integers only.
- T: `FirstMissingRequirement` scans `sim.Units.All` once per entry; the result is an existence test, so order cannot change it. The cycle search is ordinal and file-ordered, at load time only. No runtime state.
- Both: every value that affects later ticks is hashed (tile blockers per tile; definitions in the digest).

## Build order and tiers

W and T are independent; build W first, then T (so the `WriteHash` order is `IsWall, Requires` and pins move serially). Each: Sonnet `builder` (code, data, tests, re-pins, full suite green, no warnings), then the Opus `sim-reviewer` (T includes the `rts/src/Ai` diff). F is not built.

## Rejected extras (one line each)

- Gates: the design does not ask for them; walls die to attack.
- Multi-tile wall segments or a line-build command: one tile per order already works; the UI splits a drag.
- A per-worker build queue: out of scope in `worker_construction.md`; Unity can hold the list.
- Wall adjacency bonuses, wall upgrades, repair: not in the design.
- A demolish command or attacking own walls: see Q-W3.
- Line-of-sight blocking by walls: no line of sight exists in the game.
- A separate placement time for walls: one global key today (Q-W7).
- Research, upgrades, more tech tiers, tech that trains units: one gate does the job.
- Pausing or refunding queued units when a prerequisite dies: needs new state and a cancel command.
- Re-checking `requires` when a structure is placed: only the order is checked, like production.
- Sharing tech with allies: Q-T4.

## Questions for the developer

Each has the default the builder uses until you reply.

- **Q-W1. Goal sealed off by a wall.** Today a unit whose goal cannot be reached stops at once where it is, so an attack-move into a fully walled base does nothing until the player targets a wall. Should units instead walk to the nearest reachable point (and then attack what is in the way)? *Default: keep today's rule (stop); players and, later, the AI target walls directly.* This decides whether a full wall-in is a strong defence or a short delay.
- **Q-W2. Sealing in.** May a wall close an area around units (own or enemy)? *Default: yes, no enclosure check; only a unit standing on the tile blocks placement.*
- **Q-W3. Getting out of your own walls.** Own units cannot attack own walls, and there is no demolish order. *Default: no way out except an enemy breaking the wall.* Alternatives: a demolish command (refunding nothing), or allowing forced attacks on own walls.
- **Q-W4. Where walls may go, and how many.** *Default: any explored, free tile (no base needed), no cap.*
- **Q-W5. Walls in deep water.** The design says "walls can stand in shallow water". *Default: any depth allowed (as `flooding.md` Phase 1: walls always block).*
- **Q-W6. Faction differences for walls.** *Default: identical rules and numbers for Wind Baffle and Coral Wall.*
- **Q-W7. Placement time for walls.** *Default: the global `worker_placement_seconds` (2 s) like every structure.*
- **Q-W8. Do walls block their owner's own flood?** *Default: yes, every team's water.*
- **Q-T1. Gate the Roc Rider too?** P-005 calls it the strongest Skai unit from the first building. *Default: no; only Thunderhead and Leviathan. Gating it is one data line.*
- **Q-T2. Losing the prerequisite.** *Default: queued units and pending build orders finish; built units are unaffected; only new orders are refused.*
- **Q-T3. Does the tech building do anything else** (supply, a weapon, training)? *Default: no.*
- **Q-T4. Does an ally's tech building count?** *Default: no, only your own.*
- **Q-T5. Does the tech building itself need anything** (for example the producer)? *Default: no requirement.*
- **Q-F1. Build the flood-bound flyer at all?** *Default: no (not approved).* If yes: **Q-F2.** what happens when the water under it recedes? *Default: it is stranded as Grounded while its tile is dry (option a).*

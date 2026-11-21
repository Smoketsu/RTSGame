<!-- rts/docs/specs/walls_and_tech.md -->
<!-- Build spec for P-005 developer decisions 5 and 8 (18 Nov 2026): worker-built walls (package W), a data-driven prerequisite list with one tech building per faction (package T), gates (package G) and drag to place for walls (package D), both added 18 Nov 2026 at the developer's request, and, as an option that is NOT approved for build, a flyer bound to flood water (package F). Mechanics and data shape only; every number is a placeholder. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by the sim-reviewer after each package. -->
# Spec: walls, tech prerequisites, gates, drag to place, and a flood-bound flyer (option)

Written 18 Nov 2026 by the designer, against `review/combat-mechanics-2` at `b6d20dc` (combat mechanics packages 1 to 4 built). Sources: `proposals/P-005-roster-holes-and-ai-for-balance.md` (developer decisions 5 and 8), `specs/worker_construction.md`, `specs/combat_mechanics.md`, `specs/flooding.md` (walls in water), `specs/planetary_cycles.md` (Quake: "Walls and defences take damage"; review item 10, the shared `Wall` flag), `specs/statuses.md` (Grounded: "walls block it"), `art/CONCEPT_BRIEF.md` (building jobs: Wall = Wind Baffle / Coral Wall, Tech = Storm Spire / Reef Workshop), `rts/src/Sim/Terrain/TerrainTile.cs` (the `TileBlockers` enum lives here; there is no `TileBlockers.cs`), `Economy/EconomySystem.cs`, `Commands/EconomyCommands.cs`, `Ai/AiWorldView.cs`, `Ai/AiBrain.cs`.

## 0. Ground rules for all packages

- **Mechanics first, numbers last** (developer, 18 Nov). Every number below is a **placeholder** worked out by the rule stated next to it. Data comments say `Source: placeholder, developer to confirm (rts/docs/specs/walls_and_tech.md <section>).` No existing number changes.
- **Game names as data names.** New IDs use the concept brief's names as they stand: `skai.wind_baffle`, `water.coral_wall`, `skai.storm_spire`, `water.reef_workshop`. The developer renames at the end.
- **No new step, no new command, no new codec** in W and T (G adds no command either; D adds one command, one codec and one event, section D.4). W runs in the existing spawn and removal hooks (step 2 scripts, step 6d deaths, step 7 placement) and in step 9 (quake damage). T runs in step 2 only. The step numbers in the `Simulation.cs` header do not change; each package adds one clause to it.
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
6. **Flood water (one line in `FloodingMechanic.ReadField`).** `field.Wall[i] = (tile.Blockers & (TileBlockers.Wall | TileBlockers.BuiltWall)) != 0`. So exactly as `flooding.md` specifies for `Wall`: water never flows into a wall tile, water already on it is not displaced and does not flow out, waves wash over it without filling it, and the wave push stops before it. Applies to every team's water, the owner's included (Q-W8). When the wall dies, the water on its tile flows again from the next flood update. **Tide retreat is not stopped by a wall** (measured when W was built, `FloodingTests.BuiltWall_WaterStillRetreatsWithTheTide`): `FloodRules.Retreat` drains any tile of the retreating team that holds water, walls included, farthest from the sources first, so water kept on a wall tile drains when the tide goes out; only the flow rules (redistribution, displacement, wave push) treat the tile as a wall.
7. **Combat (new: nothing).** A wall has footprint 1, so `CombatSystem.IsStructureTarget` is true: its armour row (shipped walls use `structure`) and the attacker's `structure_damage_multiplier` apply. It is a ground-layer unit, so ground and water weapons and **ground blasts** hit it (enemy walls only, no friendly fire), with the splash part of `combat_mechanics.md` 1.1 step 6. Air-only weapons cannot attack it. It has no weapon. Automatic acquisition is armed-first, so an armed enemy in range is always chosen before a wall; with no armed enemy in reach, an attack-moving or idle unit shoots a wall in reach as it does any unarmed structure.
8. **Flyers.** Flyers pass over walls. A flyer Grounded over a wall tile is moved off by the existing landing rule (`statuses.md`, "Landing": Chebyshev rings, ascending index, first standable tile). A Grounded flyer is then blocked by walls like any ground unit. Downdraft and Drench need no change.
9. **Vision.** Walls do not block sight (there is no line of sight in the game; `fog_of_war.md` defers it). A wall sees its own `sight_range_tiles` like any unit.
10. **Quake (one condition in `CycleSystem.QuakeDamage`).** The design says "Walls and defences take damage" and `planetary_cycles.md` says wall structures join the set when they exist. The skip test becomes `!(IsStaticDefence || IsWall) || ...`; amount, floor and event are unchanged. Quake path closing already skips any blocked tile (`CycleSystem` line 622, `IsBlocked`), so a player wall on a path tile keeps the path open on that tile, and a closed path tile rejects a wall order with `tile_blocked`.
11. **Rooted HQ floor.** Unchanged. The harness floor trials (`balance_harness.md`, `micro_benchmark.md` F0: 15 s) spawn the HQ and the attackers with no walls, so the floor still measures the bare HQ. Walls add protection the floor does not count. Walls do not change HQ armour or `IsStructureTarget` of the HQ.
12. **Supply.** Shipped walls have `supply` 0 and `provides_supply` 0.
13. **Gates.** None in W; added by package G (18 Nov 2026, developer request).
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
- Build UI: a wall button in the worker build menu. Dragging a line is specified in package D (superseding the client-side list this bullet described before 18 Nov 2026).
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
5. **The tech buildings** do nothing else: no `trains`, no supply, no weapon, no research (Q-T3). Their only job is to exist. *21 Nov 2026: tech buildings research (research_and_unlocks.md): no `trains`, no supply, no weapon; research only.*
6. **Smallest gating** (current producers: Windspire trains Talon Marksman, Stormcaller, Thunderhead; Shell Forge trains Shellback, Brinecaster, Leviathan; Roost trains Windrunner and Roc Rider): add `"requires": ["skai.storm_spire"]` to `skai/thunderhead.json` and `"requires": ["water.reef_workshop"]` to `water/leviathan.json`. Producers keep their `trains` lists. The tech buildings themselves have no `requires`, so the top unit needs its producer plus one more building (P-005 rule 8, "switching costs time"). The Roc Rider stays ungated (Q-T1).

### T.2 Data

Unit file key at the root (`UnitSchema.ReadEconomy` like `trains`; `UnitEconomyDefinition.Requires`, `IReadOnlyList<string>`, optional constructor argument defaulting to empty; appended to `WriteHash` as count then each ID in file order):
```
// Buildings the owner must have standing and complete to train or build this; checked only when the order is given. Unit: list of unit IDs. Range: 0 to 8 distinct IDs of buildings (footprint 1) of this unit's faction, never this unit, no cycles (empty when absent). Source: P-005 developer decision 8 (18 Nov 2026); choice of gated units is a placeholder, developer to confirm (rts/docs/specs/walls_and_tech.md T.2).
"requires": ["skai.storm_spire"],
```
Loader (each failure is a `DataValidationException` naming the unit's file and `requires`, or `requires[i]`):
- Per file (`UnitSchema`): not an array of strings; more than 8 entries; a duplicate ID ("lists 'x' twice"). The unit's own ID ("requires itself", field `requires[i]`) is checked in `DataLoader.CheckReferences` with the other references, because the file reader does not know the unit's ID.
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

## Package G: gates

Added 18 Nov 2026 against `main` at `87e4a6a` (W and T built), after the developer's request "Sounds like we need to add gates and drag to place for the walls". Every claim about current behaviour names the file and method it was read from.

### G.0 What the code does today (the constraint)

- Passability is one bit per tile shared by every player: `TerrainTile.IsBlocked` is `Blockers != None`; `TerrainGrid.EdgePassable` refuses a step when either tile `IsBlocked`; `TerrainGrid.IsStepPassable` adds the no-corner-cutting rule (a diagonal step needs both orthogonal tiles passable); `TerrainGrid.IsStandable` is `!IsBlocked` for ground and water, always true for air.
- Flow fields are keyed by (layer, goal tile) only (`FlowFieldCache.Get`, key `layer << 32 | goal`), built by `FlowField.Build` from `IsStandable`/`IsStepPassable`, reused while `TerrainGrid.Version` matches, not hashed. Their costs already ignore teams on purpose (`TerrainGrid.SpeedMultiplier(int, MovementLayer)`: "shared by all teams").
- Two systems ask the cache: `MovementSystem.Step` and `CombatSystem.CanReach`. Four other places test passability directly: `UnitSeparation.TerrainChecked`/`Allowed`, `FloodingMechanic.PushEnemies`, `CycleSystem.FindDestination` (quake relocation), and `EconomyRules.StandablePointNear`/`TryFindSpawnPosition` and `StatusSystem.Land` (standable tiles).
- Movement does not test the position it moves to: `MovementSystem.Step` adds the flow direction vector to the position. Only the flow field (and the corner rule inside it) keeps a ground unit off blocked tiles. So a tile one team may cross and another may not **needs a flow field per team**. There is no smaller correct mechanism: a shared field either leads enemies into the gate (where they would then walk through, because nothing else stops them) or keeps owners out.

### G.1 Rules

1. **A gate is its own structure type**, not a converted wall. A definition with `is_gate: true` (which needs `is_wall: true`) is built, paid, cancelled, refunded, placed in lines (package D) and damaged exactly like a wall. Why not conversion: converting needs a new command, a codec, a conversion timer on the unit, and rules for the wall's health and refund during conversion; a separate type needs one data flag and two unit files and reuses `build_structure`. A gate tile is chosen by leaving a gap in a wall and building the gate there (a gate cannot be placed on a wall tile: `tile_blocked`).
2. **New blocker flag** `TileBlockers.Gate = 8` in `Terrain/TerrainTile.cs`, and a new tile field **`GateTeam`** (`TeamId`): the team whose ground and water units may cross the tile; `TeamId.None` whenever the `Gate` flag is absent (the constructor normalises it, as it does `WaterTeam`). `EconomySystem.BlockerOf` returns `Gate` for a gate, `BuiltWall` for another wall, `Footprint` otherwise. `EconomySystem.OnUnitSpawned` adds the flag through a new `TerrainGrid.SetGate(sim, index, team)` with the owner's team (`sim.Players.TeamOf(owner)`); when the tile already has `Gate` (only a script can stack two gates), the flag and the existing `GateTeam` stay. `EconomySystem.OnUnitRemoved` keeps a flag when another unit **with the same `BlockerOf` value** stands on the tile (this replaces today's `IsWall ==` comparison and gives the same answer for walls and footprints); for a gate it then sets `GateTeam` to the team of the lowest-ID remaining gate on the tile; with none left it removes `Gate` and `GateTeam` becomes None. Teams never change during a match (`PlayerRegistry.TeamOf` reads a fixed `Player.Team`; no command changes it), so the stored team cannot go stale.
3. **Who passes: the gate owner's team** (so allies pass; one team is the alliance unit already used for water, `TerrainTile.WaterTeam`). New `TerrainTile.IsBlockedFor(TeamId team)`: `(Blockers & ~Gate) != 0 || ((Blockers & Gate) != 0 && (!team.IsValid || GateTeam != team))`. `IsBlocked` keeps its meaning (any blocker; equal to `IsBlockedFor(TeamId.None)`), so every caller not listed in rule 5 treats a gate as a wall. Q-G1.
4. **Automatic, no open or closed state.** A gate is always passable to its team and never to anyone else. No command, no timer, no state on the unit. Unity animates it open when a unit of its team is on or next to its tile (presentation only). Q-G2.
5. **Per-team passability, exactly what changes:**
   - `TerrainGrid`: new overloads `IsStandable(int index, MovementLayer layer, TeamId team)` and `IsStepPassable(int from, int to, MovementLayer layer, TeamId team)`; `EdgePassable(a, b, team)` uses `IsBlockedFor(team)`; the existing two-argument and three-argument forms call the new ones with `TeamId.None`. New derived property `GateTileCount` (tiles whose `Blockers` has `Gate`; kept by `Replace`; range 0 to tile count; not hashed, it is a function of the hashed tiles). `Replace` also bumps `Version` when `GateTeam` changes.
   - `FlowField`: constructor takes `TeamId passTeam` (new property `PassTeam`); `TileCost`, the goal seeding and both passes of `Build` use the team overloads.
   - `FlowFieldCache.Get(MovementLayer layer, int goalIndex, TeamId team)`: first `if (_grid.GateTileCount == 0) team = TeamId.None;`, then key `((long)(byte)layer << 40) | ((long)team.Value << 32) | (uint)goalIndex`. The old `Get(layer, goal)` calls it with None.
   - Callers passing the moving unit's team (`sim.Players.TeamOf(unit.Owner)`; None for a neutral unit, so gates block it): `MovementSystem.Step`, `CombatSystem.CanReach`, `UnitSeparation.TerrainChecked`/`Allowed` (the pushed unit's team), `FloodingMechanic.PushEnemies` (the pushed unit's team).
   - Callers left on the team-less forms (a gate blocks them for everyone): `EconomyRules.StandablePointNear`, `EconomyRules.TryFindSpawnPosition` (trained units never appear in a gate), `StatusSystem.Land` (a Grounded flyer never lands in a gate, own or not), `CycleSystem.FindDestination` and `CycleSystem.SetPath` (a closed quake path tile skips a gate tile like any blocked tile, `IsBlocked`), every placement check (`BuildStructureCommand.Apply`, `EconomySystem.SiteProblem`), and flood reading (rule 7).
6. **Cost and caching.** With no gate on the map every key uses team None, so fields, build counts and eviction are exactly today's. With a gate anywhere, a goal used by units of k teams is built up to k times (one Dijkstra over the grid each) and the k fields share the `max_cached_flow_fields` capacity (64), so eviction comes sooner. Fields stay a pure function of (terrain, layer, goal, team) and are still not hashed; eviction still follows creation order (`FlowFieldCache.EvictOldest`), so the cost is time only, never a desync. Placing or losing a gate bumps `Version` like any blocker, so every field rebuilds on its next `Get` (unchanged rule, W.1 rule 3).
7. **Flood water and waves.** `FloodingMechanic.ReadField`: `field.Wall[i]` also tests `TileBlockers.Gate`. A gate is a wall to every team's water, its own team's included (as Q-W8 for walls): water never flows into it, water already on it stays, waves wash over without filling it. A wave pushes a unit through a gate only when the gate belongs to the pushed unit's team (rule 5); an enemy of the gate is stopped before it. Q-G3.
8. **An enemy standing in a gate.** No game rule can put one there: placement needs a free tile (`tile_occupied`, and the second site check in `EconomySystem.StepBuild`), and movement, separation, wave pushes and landing all refuse the tile to an enemy (rule 5). Only a script (`spawn_unit` of a gate on an occupied tile) can. Then the unit behaves as a unit inside a wall does today: its tile is not standable for it, so `FlowField.IsReachable` is false on its tile, every move order ends with `MoveUnreachableEvent`, and separation cannot push it out (`Allowed` refuses a step from a blocked tile). It stays until the gate dies. Own-team units may stand in their gate (their goal tile may be the gate tile).
9. **Targeting a gate.** An enemy ordered to the gate's centre gets a field seeded on the standable neighbours (`FlowField.Build`, blocked goal), so it stops beside the gate as beside any structure. A gate is a structure target (footprint 1, `CombatSystem.IsStructureTarget`), takes splash, armour and `structure_damage_multiplier` exactly like a wall (W.1 rule 7); a blast on a gate also hits enemies of the attacker standing in it (unchanged splash rule).
10. **Flyers** pass over gates (air ignores blockers). A Grounded flyer is landed off a gate tile (rule 5) and then crosses gates by its team like any ground unit.
11. **Vision**: no change (no line of sight). **Quake**: a gate has `is_wall`, so `CycleSystem.QuakeDamage` already damages it. **Rooted HQ floor**: unchanged (the floor trials have no walls or gates). **Supply**: shipped gates have 0.
12. **AI (minimum).** `AiWorldView` line 471 adds `TileBlockers.Gate` to the flags known only where the tile is visible (with `Footprint` and `BuiltWall`). The AI never builds a gate for the same reasons it never builds a wall (W.4). An AI attack move into a base sealed by enemy walls and gates stops with `MoveUnreachableEvent` (W.4 known limit, unchanged).

### G.2 Data

Unit file key (`UnitSchema.ReadEconomy`; add `"is_gate"` to the root `RejectUnknownKeys` list in `DataLoader` line 329; `UnitEconomyDefinition.IsGate`, optional constructor argument `bool isGate = false` after `requires`; appended to `UnitEconomyDefinition.WriteHash` after `Requires`):
```
// Marks a gate: a wall piece that units of its owner's team (allies included) cross and every other unit treats as a wall; flood water treats it as a wall for every team. Unit: boolean. Range: true or false (false when absent; true needs is_wall true). Source: developer request 18 Nov 2026 ("add gates"); rts/docs/specs/walls_and_tech.md G.2.
"is_gate": true,
```
Loader and constructor: `is_gate: true` without `is_wall: true` fails `"is_gate needs is_wall"` naming the file and `is_gate`.

New files (copy `wind_baffle.json` / `coral_wall.json` and their comment style; every number and name a **placeholder**):

| Key | `skai/wind_gate.json` | `water/coral_gate.json` | Placeholder rule |
| --- | --- | --- | --- |
| `id`, `display_name` | `skai.wind_gate`, "Wind Gate" | `water.coral_gate`, "Coral Gate" | the brief names no gate; wall name + "Gate" |
| `max_health` | 200 | 200 | same as a wall |
| `cost_common`, `cost_rare` | 50, 0 | 50, 0 | two wall tiles |
| `build_seconds` | 10 | 10 | two wall tiles |
| `is_wall`, `is_gate`, `footprint` | true, true, 1 | true, true, 1 | |
| everything else | as the faction's wall | as the faction's wall | |

Add each ID to its faction's `units` list.

### G.3 State, hash, commands, events

- **Hashed state:** `TerrainTile.WriteHash` appends `GateTeam` (a `TeamId`, one byte) **after** `(byte)Blockers`, for every tile. No per-unit or per-player state. `IsGate` goes into the content digest.
- **Not hashed:** `TerrainGrid.GateTileCount` (derived from tiles) and the flow fields (pure function of terrain, as before).
- `new TerrainTile(...)` takes `TeamId gateTeam` as a new **required** seventh argument: `default(TeamId)` is team 0, not None (`Ids.cs`: `None = new TeamId(255)`), so a default value would be a bug. Every call site (8 today in `rts/src` and `rts/tests`) passes `tile.GateTeam` or `TeamId.None`.
- No new command, codec or event: `build_structure` builds a gate; `TerrainChangedEvent` already carries the whole tile, now with `GateTeam`.
- **Documentation edits:** `TerrainTile.cs` (the `Gate` value, `GateTeam`, `IsBlockedFor`), `TerrainGrid.cs` (header, overloads, `GateTileCount`), `FlowField.cs` and `FlowFieldCache.cs` headers (team in the key and why), `Simulation.cs` header (step 5: "ground and water flow fields are per team while any gate stands"; spawn hooks: "a structure's footprint, wall or gate blocker"), `EconomySystem.cs` "Footprint" paragraph, `Pathing/README.md`, `Terrain/README.md`, `rts/data/README.md`, and a dated note on ADR-007 ("18 Nov 2026: cache key gains the passing team while any gate stands, walls_and_tech.md G.1 rule 6") and on ADR-006's `Blockers` bullet (`Gate`, `GateTeam`).

### G.4 What moves hashes

Every pinned hash moves: the digest (new key and files) and the tile hash (one byte more per tile, so `GoldenFinalHash` moves too although TestWorld has no gate). **Hash only**: no AI game contains a gate, so `GateTileCount` is 0 and every flow field is today's. If an AI outcome moves, stop and report. Re-pin as section 0 says.

### G.5 Unity (PC session)

Gate models (Wind Gate, Coral Gate; no concept art exists), team colour, open animation while a unit of the gate's team is within one tile (read from unit positions), a gate button in the worker build menu, and the gate in the drag tool (package D accepts any `is_wall` definition).

### G.6 Acceptance tests (`rts/tests/Sim.Tests/Economy/GateTests.cs`)

Test gate `test.gate` = `test.wall` (W.6) with cost 50, build 10 s (200 ticks), `is_gate` true. Combat setup with P1 (team 1) and P2 (team 2); for G-4 a test-local three-player list adds P3 on team 1 (build the simulation directly, as `WallTests.EconomySim` does, if `CombatRig` cannot take a player list). "Gated column" = `test.wall` of P1 on (10, 0) to (10, 23) except (10, 10), and a `test.gate` of P1 on (10, 10), all spawned by script on tick 0 before the move orders. Mover speed 4 (0.2 tiles/tick), so after tick k a straight mover from x = 5.5 is at x = 5.5 + 0.2 (k + 1) (W-2).

| # | Test | Setup and expected values |
| --- | --- | --- |
| G-1 | **Placed by a worker** | Economy setup, W-1 layout with `test.gate`: `BuildOrderStartedEvent` **t0 + 22**, `StructurePlacedEvent` **t0 + 62** with health **20**; on that tick tile (20, 14) has `Blockers == Gate` and `GateTeam` = P0's team; `StructureCompletedEvent` **t0 + 262**. Common 200 - 50 = **150**. |
| G-2 | **Own team passes** | Gated column. P1 mover at (5.5, 10.5) to (15.5, 10.5) on tick 0: on tile (10, 10) after tick **22** (x = 10.1); x = 10.5 after tick 24; `UnitArrivedEvent` on tick **48** (x = 15.3, within the 0.25 arrival radius). Its tile is never a `BuiltWall` tile. |
| G-3 | **Enemy blocked** | Same with a P2 mover: `MoveUnreachableEvent` on tick **0**, x stays **5.5**. |
| G-4 | **Ally passes** | Same with a P3 mover (team 1): as G-2. |
| G-5 | **Gate as a goal** | P1 mover ordered to (10.5, 10.5): `UnitArrivedEvent` on tick **23** at x = 10.3, standing on the gate tile. P2 mover ordered there: it stops beside the gate, `UnitArrivedEvent` on tick **18** at x = 9.1 (tile (9, 10), a seeded neighbour). |
| G-6 | **Cache cost** | No gate on the map: a P1 and a P2 mover ordered to the same goal on tick 0 raise `Pathing.BuildCount` by **1**. With a P1 gate spawned at (20, 20) on tick 0: by **2**. |
| G-7 | **Gate dies** | G-3, then `remove_unit` of the gate on tick 20: tile (10, 10) has no blocker and `GateTeam` None; a new P2 order on tick 21 passes (arrives before tick 80). |
| G-8 | **Stacked gates** | Script: a P1 gate then a P2 gate on tile (30, 30) (economy setup): `GateTeam` = team of P1. Remove the P1 gate: P2's team. Remove the P2 gate: no blocker, None. A wall and a gate on one tile: `BuiltWall | Gate`; removing the gate leaves `BuiltWall`. |
| G-9 | **Corner rule by team** | Gate of P1 at (10, 10), no walls: `IsStepPassable((9,10) -> (10,11), Ground, team 1)` true; with team 2 false; with `TeamId.None` false. `IsStandable((10,10), Ground, team 1)` true, team 2 false, Air true for both. |
| G-10 | **Flood** | W-5 with `test.gate` on B instead of the wall: (a) A **1000**, B and C **0** after 4 updates; (b) B keeps **500**. Same result when the water's team is the gate's team. |
| G-11 | **Wave push** | Copy the existing wave-push test in `FloodingTests` (the one asserting `UnitPushedEvent`; walker (5.5, 5.5), caster (2.5, 5.5), push to (7.5, 5.5)) and put a gate on the first tile of the push path, tile (6, 5), spawned on tick 0 (builder's choice for the tile, measured when G was built; count only the walker's `UnitPushedEvent`, and `PushEnemies` skips every unit with `Economy.FootprintTiles > 0`, so an enemy gate in the strip is never shoved, see G-11b): a gate of the caster's team stops the push before it (no `UnitPushedEvent`, position unchanged); a gate of the pushed unit's team lets it through (same end position as without the gate). |
| G-12 | **Grounded flyer** | W-8 with the gated column and the flyer of P1 grounded over the gate tile (10, 10): it lands on tile (9, 9) (centre (9.5, 9.5)), never in its own gate. Measured when G was built: unlike W-8 the flyer's own team may use the gate, so its move order stays valid and on the landing tick it walks on, one step north (the corner rule cuts NE past the wall at (10, 9)), ending the tick at **(9.5, 9.7)**; there is no `MoveUnreachableEvent`. |
| G-13 | **Quake and splash** | W-12 and W-7 with `test.gate` in place of the walls: the same numbers. |
| G-14 | **AI view** | W-14 with an enemy gate: not blocked in fog, blocked when visible. Over `ai_vs_ai.json` no `build_structure` names a gate. |
| G-15 | **Loader** | `is_gate: true` with `is_wall` absent is rejected naming the file and `is_gate`. Shipped Wind Gate and Coral Gate load with the G.2 values; no other shipped unit has `is_gate`. |
| G-16 | **Hash** | `TerrainTile.WriteHash` writes `GateTeam` after `Blockers`; two runs differing only in the owner (P1 or P2) of one gate differ at the first hash tick after the spawn; two identical runs of G-2 match at every hash tick. `HashCoverageTests` covers `GateTeam`. |

### G.7 Rejected extras

Open and close or lock commands; owner-only gates; gates that let own flood water through; multi-tile gates; converting a wall into a gate; AI gate building.

---

## Package D: drag to place

### D.0 What the code does today

- One worker holds one build order: `Unit.SetBuildOrder` stores one definition and one tile; `BuildStructureCommand.Apply` first calls `EconomySystem.CancelBuild(sim, worker, "new_order", false)`, which refunds the previous order. So N `build_structure` commands for one worker on one tick leave only the last; the others are paid and refunded (one `BuildOrderCancelledEvent` each).
- W.5 told Unity to keep a client-side list and issue a worker's next tile when `structure_placed` arrives. That costs one input delay per segment (`online.json` `input_delay_ticks` 3, applied by `LockstepPeer.NextInputTick`), puts sequencing logic and its state in Unity where replays, reconnects and the harness cannot see it, and reserves no money. **D replaces that W.5 bullet.**

### D.1 Decision: one new command plus a per-worker tile queue in the simulation

Unity rasterises the drag (with a public Sim function, so it draws exactly what the simulation will accept), splits the tiles among the selected workers and sends **one `build_wall_line` per worker** with that worker's tiles. The simulation stores the tiles on the worker and works through them. A tile list rather than two end points, because a contiguous slice of a rasterised line is not in general the rasterisation of its own end points, so per-worker end points would not reproduce the drawn line.

### D.2 Rules

1. **Command `build_wall_line`** (`Commands/EconomyCommands.cs`, `BuildWallLineCommand`): `Worker` (EntityId), `DefinitionId` (string), `Tiles` (list of (x, y) tile coordinates in build order, 0 to 255 entries in the codec). Checks, in order, each a rejection with no state change: `unknown_unit`, `not_owner`, `cannot_build` (as `build_structure`); `unknown_definition`; **`not_a_wall`** (the definition lacks `is_wall`; gates have it); `wrong_faction`; `missing_prerequisite`; **`no_tiles`** (empty list); **`too_many_tiles`** (more than `wall_line_max_tiles`).
2. **Static tile filter**, each tile in list order, by the new public `EconomyRules.WallLineTileProblem(Simulation sim, Unit worker, int tileX, int tileY)` (returns a reason or null; Unity calls the same function for the preview): `out_of_bounds`; `site_unexplored` (worker's team); `tile_blocked` (`IsBlocked`); `on_resource_field` (a field, depleted or not, is on the tile); `site_pending` (another own unit, **not this worker**, has its active order or a queued tile there; this worker is excluded because its orders are about to be replaced). The command adds `duplicate_tile` for a tile already earlier in the list. A tile with a unit standing on it is **not** filtered: units move, and the existing arrival and placement checks handle it (rule 6). If every tile fails, the command is rejected with **the first tile's reason**.
3. **Paying for the line up front** (as a production queue pays when queued, `EconomySystem` header). Walk the remaining tiles in list order and keep each while the owner can still afford the running total (`CanAfford`) and supply fits (`SupplyUsed + SupplyOf(def) x kept <= SupplyCap`, after `RecomputeSupply`); the first tile that fails ends the walk, and it and every later tile are dropped with reason `insufficient_resources` or `supply_cap`. If no tile is kept, reject with that reason. Q-D2.
4. **Accepting:** `resume` as in `BuildStructureCommand`; `CancelBuild(sim, worker, "new_order", false)` (refunds and drops any previous order and queue, rule 7); spend cost x kept; `ClearCombatOrders`; `ClearGather`; `SetBuildOrder` for the first kept tile; `SetBuildQueue` with the other kept tiles (tile indices, list order); `SetMoveTarget` to the first tile's centre; `RecomputeSupply`. Events, in this order: `BuildOrderAcceptedEvent` for the first tile with **cost = the whole amount spent** (cost x kept), then one `BuildLineTileSkippedEvent` per filtered or dropped tile in list order (refund 0).
5. **Advancing** (new `EconomySystem.AdvanceBuildQueue(sim, worker, resumeField)`): pop tiles from the head; a tile now `IsBlocked` is dropped (`BuildLineTileSkippedEvent`, reason `site_blocked`, refund one tile's cost); the first other tile becomes the order (`SetBuildOrder` with the same definition and `resumeField`, `SetMoveTarget`, `BuildOrderAcceptedEvent` with **cost 0**: already paid). The reach check for it first runs on the next tick (the worker moves on the next tick's step 5). If the queue empties without an order, the worker resumes its gather field as today. Called (a) in `StepBuild` right after `StructurePlacedEvent`, in place of today's resume, and (b) from `CancelBuild` when `resume` is true (the `site_blocked`, `site_occupied` and `unreachable` cancels of the active tile).
6. **A unit in the way** (the developer's "rejected tiles in the middle of a line"): the active tile is cancelled by the existing rule (`site_occupied` on arrival or at the second check, refund one tile's cost), and the worker goes straight on to the next queued tile on the same tick. The line gets a hole. Dragging the same line again fills only the holes, because built tiles are `tile_blocked` and skipped. Q-D1.
7. **Dropping the queue:** `CancelBuild` with `resume` false (reasons `new_order` and `worker_lost`: any accepted move, attack, stop, gather, build or line order, and the worker's removal in `OnUnitRemoved`, so death in step 6d) refunds the active tile as today, then refunds one tile's cost per queued tile and emits `BuildLineTileSkippedEvent` with the same reason for each, in queue order, and clears the queue. **A worker dying mid-line** therefore refunds everything not yet placed; its tiles are not handed to other workers.
8. **Several workers selected:** one command each, disjoint tiles (Unity, D.5). Commands apply in (issuer, sequence) order; a tile already in another own worker's order or queue is skipped `site_pending`. `BuildStructureCommand.Apply`'s `site_pending` loop also checks every own unit's `BuildQueue` (the worker itself included, as today for its active tile).
9. **Supply:** `EconomySystem.RecomputeSupply` adds `SupplyOf(def)` once per queued tile (shipped walls and gates have 0).
10. **Invariant:** a non-empty `BuildQueue` implies `BuildState != None`. `ClearBuildOrder` does not touch the queue; only `CancelBuild`, `AdvanceBuildQueue` and `SetBuildQueue` do.
11. **AI:** no change. The AI emits only its own intents and no `build_wall_line`; its workers never have a queue.

### D.3 Line rasterisation (public, pure, integer)

`public static int WallLine.Rasterise(int x0, int y0, int x1, int y1, int maxTiles, List<(int X, int Y)> output)` in `rts/src/Sim/Economy/WallLine.cs`: clears `output`, appends tiles from (x0, y0) toward (x1, y1) and returns the count. Algorithm (all-octant Bresenham, integers only): `dx = |x1 - x0|`, `sx = sign`, `dy = -|y1 - y0|`, `sy = sign`, `err = dx + dy`; loop: append (x, y); stop when (x, y) = (x1, y1) or the count reaches `maxTiles`; `e2 = 2 err`; if `e2 >= dy` then `err += dy, x += sx`; if `e2 <= dx` then `err += dx, y += sy`. The tile count is `min(max(|x1 - x0|, |y1 - y0|) + 1, maxTiles)`; a longer drag keeps the tiles nearest the start. It ignores map bounds (the command checks them).

**No gaps:** the line is 8-connected (consecutive tiles share an edge or a corner). That is enough for every rule that crosses it: ground and water movement need both orthogonal tiles of a diagonal step passable (`TerrainGrid.IsStepPassable`), and so do separation (`UnitSeparation.Allowed`) and wave pushes (`FloodingMechanic.PushEnemies`); flood water flows only to the 4 edge neighbours (`FloodRules`, `direction < 4`), which an 8-connected line cuts. No thicker (4-connected) line is needed. Tests D-11 and D-12 prove it.

### D.4 Data, codec, events, state, hash

- **Tuning** (`rts/data/tuning/economy.json`, `EconomyTuning.WallLineMaxTiles`, loader range check):
  ```
  // Most tiles one build_wall_line order may name; Unity caps a dragged line at this length. Unit: tiles (count). Range: 1 to 64. Source: placeholder, developer to confirm (rts/docs/specs/walls_and_tech.md D.4).
  "wall_line_max_tiles": 16,
  ```
- **Codec** `BuildWallLineCodec` in `Commands/Codec/CoreCommandCodecs.cs`, registered in `RegisterAll`: worker (EntityId), definition (string), count (byte), then count x (x int32, y int32). No `ProtocolVersion` (3) or replay format (2) bump: no existing layout changes (the `cast_grounding` precedent, `combat_mechanics.md` 4); a byte-pin test for the payload.
- **Script** (`Data/CommandScript.cs`): kind `build_wall_line` added to the known list (line 31) and the switch: keys `type`, `tick`, `issuer`, `sequence`, `unit`, `definition`, `tiles` (array of `[x, y]` pairs, x and y 0 to 1023 like `build_structure`, at most 255 pairs).
- **Command hash** (`BuildWallLineCommand.WriteHash`): Kind, Worker, DefinitionId, count, then each x, y.
- **New event** `BuildLineTileSkippedEvent` (`Events/EconomyEvents.cs`): tick, worker, owner, definition ID, tile x, tile y, reason (`out_of_bounds`, `site_unexplored`, `tile_blocked`, `site_pending`, `duplicate_tile`, `insufficient_resources`, `supply_cap`, `site_blocked`, `new_order`, `worker_lost`), refund common, refund rare. Events are not hashed.
- **Hashed state:** `Unit.BuildQueue` (tile indices, list order, 0 to `wall_line_max_tiles` - 1 entries), written in `Unit.WriteHash` **right after `BuildResumeFieldIndex`**: count, then each index. `HashCoverageTests` gains it.
- **Documentation edits:** `Simulation.cs` header (step 2: "`build_wall_line` pays for a whole line and queues its tiles on the worker"; step 7: "a worker with queued tiles takes the next one when a structure is placed or its tile fails"), `EconomySystem.cs` header (Build orders: queue, advance, drop), `Economy/README.md`, `rts/data/README.md` (script kind), `Unit.cs` summaries. `worker_construction.md` listed a per-worker queue as out of scope; D now provides one for walls only.

### D.5 Sim versus Unity

| Cloud session (`rts/src/Sim`, tests, data) | PC session (`rts/src/Game`) |
| --- | --- |
| G: tile flag and team, per-team passability and cache key, hooks, flood mask, loader, data files, `AiWorldView` line, tests. D: command, codec, script kind, queue on `Unit`, advance and drop, supply, `WallLine.Rasterise`, `EconomyRules.WallLineTileProblem`, tuning key, event, tests, re-pins. | Drag input with a wall or gate button armed: on mouse down and every move, call `WallLine.Rasterise` with `wall_line_max_tiles` and draw one ghost per tile: valid (green), invalid (red, tooltip from `WallLineTileProblem`), occupied by a unit (amber: accepted, may become a hole). Label "N x cost = total" and turn it red when the owner cannot pay for all valid tiles, marking the tiles past the affordable count red ("insufficient resources"). On release, split the valid tiles into k contiguous chunks (k = min(selected workers, tiles), sizes differing by at most one, earlier chunks larger); give chunk i, in drag order, to the unassigned worker nearest its first tile (ties: lowest ID), reversing a chunk when the worker is nearer its last tile; send one `build_wall_line` each. Draw own workers' queued tiles as faint ghosts (read `Unit.BuildQueue`), and show `BuildLineTileSkippedEvent` and cancel reasons. Gate models and animation (G.5). Repeat the Unity-versus-headless hash check. |

### D.6 What moves hashes

Every pinned hash moves: the tuning digest (new key) and `Unit.WriteHash` (one count per unit, so `GoldenFinalHash` too). **Hash only**: nothing in AI games or the smoke scripts issues the new command. If an outcome moves, stop and report.

### D.7 Acceptance tests (`rts/tests/Sim.Tests/Economy/WallLineTests.cs`)

Economy setup with `test.wall` (W.6, cost 25, build 100 ticks). Worker spawned at (14, 14) by `EconomyTestKit.Spawn` (so before t0, and its sight 8 has explored the tiles used; each test asserts the tiles are explored as a precondition). `line(a, b)` = `WallLine.Rasterise(a, b, 16)`. Times come from W-1 and from the walk below; reach to a wall centre is 2.0.

Walk used by D-1: after placing (20, 14) on t0 + 62 the worker stands at (18.6, 14.0), tile (18, 14). To (20, 15), the field steps NE (both (19, 14) and (18, 15) free; NE neighbour integration 1024 beats 2048), 0.2 x 0.70711 = 0.14142 per axis per tick: after 2 steps (18.883, 14.283), distance² 4.097 > 4; after 3 steps (19.024, 14.424), distance² 3.336: **started t0 + 65**, placed t0 + 105. To (20, 16) with walls on (20, 14) and (20, 15): from tile (19, 14) the field steps N ((19, 15) at 2048 is the cheapest passable neighbour; NE is cut by the corner rule), and on in (19, 15) N again (NE cut by (20, 15)); after 3 steps y = 15.024, distance² 4.355; after 4 steps y = 15.224, distance² 3.805: **started t0 + 109**, placed t0 + 149.

| # | Test | Setup and expected values |
| --- | --- | --- |
| D-1 | **Three walls in a row, one worker** | `build_wall_line(worker, test.wall, [(20,14), (20,15), (20,16)])` on t0. `BuildOrderAcceptedEvent`: t0 (tile (20, 14), cost **75**), **t0 + 62** ((20, 15), cost 0), **t0 + 105** ((20, 16), cost 0). `BuildOrderStartedEvent`: t0 + 22, t0 + 65, t0 + 109. `StructurePlacedEvent`: t0 + 62, t0 + 105, t0 + 149. `StructureCompletedEvent`: t0 + 162, t0 + 205, t0 + 249. Common **125** from t0 to the end. After t0 + 149: `BuildState` None, queue empty. The worker is never on a line tile. |
| D-2 | **Money runs out** | `line((17,10), (17,19))` (10 tiles), 200 common: accepted; tiles (17, 10) to (17, 17) kept, common **0**; two `BuildLineTileSkippedEvent` on t0 for (17, 18), (17, 19), reason `insufficient_resources`, refund 0; queue holds 7. With 20 common: rejected `insufficient_resources`, nothing changes. |
| D-3 | **Bad tiles in the middle** | Enemy `test.wall` spawned at (17, 14) before t0. `line((17,12), (17,16))`: skip (17, 14) `tile_blocked`; paid 4 x 25 = 100, common **100**; queue (17, 13), (17, 15), (17, 16). List [(17,12), (17,13), (17,12)]: third skipped `duplicate_tile`, paid **50**. List of three out-of-map tiles: rejected `out_of_bounds`, common unchanged. |
| D-4 | **Unit in the way leaves a hole** | P1 `TankId` at the tile centre (20.5, 15.5) before t0 (it never moves; measured when D was built: at the tile corner (20, 15), where `EconomyTestKit.Spawn` puts units, separation from the walking worker pushed it onto tile (19, 14) and no hole appeared), D-1's line: accepted with all 3 tiles, common 125. On **t0 + 65** `BuildOrderCancelledEvent` (20, 15) `site_occupied` refund 25, and on the same tick `BuildOrderAcceptedEvent` (20, 16) cost 0; `StructurePlacedEvent` (20, 16) on **t0 + 109** (path through (19, 15) then NE, now open because (20, 15) is free: started t0 + 69). Common **150**. Then remove the tank and send the same line: (20, 14), (20, 16) skipped `tile_blocked`, only (20, 15) queued, common **125**. |
| D-5 | **Worker dies mid-line** | D-1, `remove_unit` of the worker on t0 + 70: `BuildOrderCancelledEvent` (20, 15) `worker_lost` refund 25, `BuildLineTileSkippedEvent` (20, 16) `worker_lost` refund 25. Common 125 + 50 = **175**. Only (20, 14) has a wall. |
| D-6 | **New order mid-line** | D-1, `move_unit` of the worker on t0 + 70: as D-5 with reason `new_order`; the worker's queue is empty and it moves. A second `build_wall_line` instead: same refunds, then the new line is paid. |
| D-7 | **Two workers** | Workers A at (14, 14) and B at (14, 16). A: [(20,14)]; B: [(20,15), (20,16)], same tick: both accepted, common 125. Then A: [(20,16), (20,17)]: (20, 16) skipped `site_pending` (B's queue), (20, 17) accepted, A's old order refunded `new_order`. `build_structure(A, test.wall, 20, 16)`: rejected `site_pending`. |
| D-8 | **Limits** | Empty list: `no_tiles`. 17 tiles: `too_many_tiles`. `test.tower`: `not_a_wall`. A worker of P1 named by P0: `not_owner`. Each leaves stock and orders unchanged. |
| D-9 | **Supply** | Test-local `test.wall_s1` (supply 1): a 3-tile line raises `SupplyUsed` by **3** on t0; after D-5's removal on t0 + 70 the increase is **1** (the placed wall). |
| D-10 | **Rasteriser** | (0,0)-(3,1): (0,0), (1,0), (2,1), (3,1). (3,1)-(0,0): (3,1), (2,1), (1,0), (0,0). (0,0)-(2,2): (0,0), (1,1), (2,2). (5,5)-(5,5): (5,5). (0,0)-(4,0): 5 tiles. (0,0)-(20,0) with max 16: (0,0) to (15,0). Return value equals the list count. |
| D-11 | **Diagonal lines have no walkable gap** | Combat setup (24 x 24, separation off), P2 walls spawned by script on every tile of `Rasterise(0,0,23,11,24)` (24 tiles). P1 mover at (20.5, 2.5) ordered to (2.5, 20.5) on tick 0: `MoveUnreachableEvent` on tick **0**. Same for `Rasterise(0,0,23,23,24)` with the mover at (15.5, 5.5) to (5.5, 15.5). Control: the first line without its tile in column 12: `UnitArrivedEvent` before tick 300. |
| D-12 | **Diagonal lines hold water** | `FloodingTests` setup, walls on an 8-connected diagonal line from one map edge to another, water placed on one side only, tide not turning: after 8 updates every tile strictly on the other side holds **0**. (Measured when D was built: `FloodingTests.DiagonalWallLine_HoldsWater`, 8 x 8 map, walls on (i, i) for i = 0 to 7, 1000 drops of team 0 at (7, 0) below the line, `min_retreat_per_second` 0; after 8 updates every tile with y > x holds 0 and all 1000 drops are still below the line.) |
| D-13 | **Codec and script** | Round trip of a 3-tile command; byte pin of its payload; a 255-tile command round-trips; a script `build_wall_line` with 256 pairs or an x of 1024 fails to parse with the field named. |
| D-14 | **Invariant and determinism** | At the end of every tick of D-1 to D-7, a unit with a non-empty `BuildQueue` has `BuildState != None`. Two runs of D-1 give equal hashes at every hash tick; a run with the second tile changed differs at the first hash tick after t0. Over `ai_vs_ai.json` no `build_wall_line` is issued. |

### D.8 Rejected extras

A line command with end points; lines of non-wall structures; appending to an existing queue (shift-queue); workers waiting for money; retrying occupied tiles later; handing a dead worker's tiles to others; the simulation splitting a line among workers; rectangles or free-form paths (the tile list already allows them later, Unity sends lines only); AI use.

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
- G: per-team passability reads `GateTeam`, a hashed tile field written only by the spawn and removal hooks (fixed order; stacked gates resolve to the lowest unit ID). Flow fields gain the team in their key only while `GateTileCount > 0`; they stay unhashed, a pure function of (terrain, layer, goal, team), evicted in creation order. Check that every passability caller listed in G.1 rule 5 passes the unit's team and that no other caller does.
- D: the queue is a hashed list on `Unit` in list order; the command walks tiles in list order with ordinal tie-free checks; payment, refunds and events follow list and queue order; `AdvanceBuildQueue` runs inside the ascending-ID economy loop. Integer rasterisation, no Fix64 needed. A rejected `build_wall_line` changes nothing.

## Build order and tiers

W and T are independent; build W first, then T (so the `WriteHash` order is `IsWall, Requires` and pins move serially). Each: Sonnet `builder` (code, data, tests, re-pins, full suite green, no warnings), then the Opus `sim-reviewer` (T includes the `rts/src/Ai` diff). F is not built.

G and D (18 Nov 2026) are independent of each other (G touches terrain, pathing and the blocker hooks; D touches `Unit`, the build order code and commands; both touch `EconomySystem` in different methods). Build G first, then D, each with its own re-pin. Same tiers: Sonnet `builder`, then the Opus `sim-reviewer` (G includes the one-line `rts/src/Ai` diff), then the Fable final review for simulation code. The PC session's work (G.5, D.5) can start once D's command and `WallLine.Rasterise` are on `main`.

## Rejected extras (one line each)

- Multi-tile wall segments. (Gates and drag to place, rejected here earlier, are now packages G and D at the developer's request; their own rejected extras are in G.7 and D.8.)
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

### Gates and drag to place (packages G and D, 18 Nov 2026)

Most play-changing first. The builder uses each default until you reply.

- **Q-W1 revisited: gates make it more pressing, not moot.** Without gates a fully closed base also shut its owner in, which was its own cost. With gates the owner walks out freely while every enemy order into the base stops at once with "unreachable" (G.1 rule 9; `MovementSystem.Step`), so a full wall-in becomes the obvious default. *Default unchanged: units stop; players (and later the AI) attack walls or gates directly.* If you want walls to be a delay rather than a lock, the alternative is that a unit with an unreachable goal walks to the nearest reachable point and attacks what blocks it.
- **Q-G1. Who may pass a gate?** *Default: the owner's whole team, so allies pass (the same team rule as flood water).* Alternative: the owner only (a per-player key instead of per-team; same mechanism).
- **Q-D1. A unit standing on a line tile.** *Default: when the worker reaches that tile it skips it and goes on, leaving a hole that a second drag fills (D.2 rule 6).* Alternatives: the worker waits until the tile is free, or retries the tile at the end of its queue (both need more state).
- **Q-G2. Do gates open by themselves?** *Default: yes, always open to the team and shut to everyone else; no lock order.* Alternative: a lock order that shuts a gate to its own team too (new command and hashed state).
- **Q-D2. Paying for a dragged line.** *Default: the whole line is paid when the order is given, tiles that cannot be paid are left out, and every tile not built is refunded (like a production queue).* Alternative: pay each tile when the worker starts it, and stop the line when money runs out.
- **Q-G3. Does a gate let its own team's flood water through?** *Default: no; to water a gate is a wall for every team (as Q-W8).*
- **Q-W3 revisited: gates make it mostly moot.** The planned way out of your own walls is now a gate. What is left is mistakes: units already sealed in by walls only cannot get out, because a gate cannot be built on a wall tile and own walls cannot be attacked. *Default unchanged: no demolish order.* If mistakes matter, a demolish command (no refund) is the smallest fix and would also let a wall be swapped for a gate.

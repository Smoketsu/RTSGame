<!-- rts/docs/specs/fog_of_war.md -->
<!-- Implementation spec for fog of war computed in the simulation per team: integer circle stamping, the high-ground rule, short airborne sight, visible-now and explored-ever grids in the state hash, and the read-only query API used by combat, the AI and the renderer. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by sim-reviewer. -->
# Spec: Fog of war (`rts/src/Sim/Vision`)

## 1. Purpose and design-doc source

- Technical planning: "Fog of war is computed inside the simulation, for determinism and anti-cheat, so Asset Store kits only draw it."
- Maps / Layout rules: "Low ground cannot see high ground without a spotter. Skai flyers see everything, so their vision must be limited (for example by short sight range when airborne) or this becomes a free advantage."
- Skai cliff structures: "Every structure still stays in range of ground ranged units from below once a spotter gives vision."
- Modes: 2v2 has "Shared vision". Allies (same `TeamId`) share one vision grid.

## 2. Data

No tuning keys in Phase 1. Unit-data fields (`UnitDefinition`, read by `UnitSchema`; both are in the content digest):

| Field | Type | Unit | Range | Default / placeholder |
| --- | --- | --- | --- | --- |
| `sight_range_tiles` | Fix64 | tiles | 1 to 64 | **Exists** (optional, schema default 8). Ground and water sight, and a Grounded flyer's. Placeholders to write into the unit files: workers 7, Windrunner 10, Grasper 8, Spoutling 9, Roc Rider 9. |
| `airborne_sight_range_tiles` | Fix64 | tiles | 1 to 64 | **New.** Required when `layer` is `air`, rejected otherwise (`UnitDefinition.AirborneSightRangeTiles`, equal to `SightRangeTiles` for non-air units). Placeholder: Roc Rider 6 ("short sight range when airborne"). |

Buildings use `sight_range_tiles` the same way (placeholder 9 when building data lands).

## 3. State

`VisionSystem` (`sim.Vision`), created by `Simulation.Start` after the terrain, one `TeamVision` per team in `sim.Players.Teams` order (ascending `TeamId`):

| Field | Type | Unit | Range |
| --- | --- | --- | --- |
| `Team` | TeamId | | a team of the match |
| `Visible` | `ulong[]` bit grid | 1 bit per tile, bit `i % 64` of word `i / 64`, `i = y * Width + x` | 1 = seen this tick |
| `Explored` | `ulong[]` bit grid | same layout | 1 = seen on any tick so far; never cleared |

Both grids affect future ticks (combat target acquisition and Phase 2 command checks read them), so both are hashed. Hash position: after `Terrain.WriteHash`, before the mechanics (README hash order). Order: team count, then per team ascending: `Team`, word count, every `Visible` word ascending, every `Explored` word ascending. Unused bits of the last word are always 0.

Cached and **not** hashed (pure functions of data): a `SortedDictionary<long, int[]>` from a radius's `Fix64.Raw` to its stamp offsets.

## 4. Per-tick algorithm

New step **7, vision**, after faction mechanics and before the state hash, so the hash, the next tick's combat and the AI (which runs between steps) all read end-of-tick vision. `Start` also runs one recompute after `OnMatchStart`, so tick 0 has vision.

1. For each team ascending: clear `Visible` (all words 0).
2. For each unit in ascending ID whose `Owner` is a player (neutral units give no vision): `team = TeamOf(owner)`; `(cx, cy) = (Position.X.FloorToInt(), Position.Y.FloorToInt())` (the tile `TerrainGrid.TileOf` returns); skip if off-map.
   - `layer = unit.EffectiveLayer` (driven by statuses.md).
   - Air: `R = airborne_sight_range_tiles`, `eye = int.MaxValue` (sees every height).
   - Ground or Water: `R = sight_range_tiles`, `eye = At(cx, cy).Height + (At(cx, cy).IsRamp ? 1 : 0)`.
   - For each offset `(dx, dy)` of radius `R` (below), tile `(cx + dx, cy + dy)`: skip off-map; set its `Visible` bit when `Height(tile) <= eye`.
3. For each team ascending: `Explored[w] |= Visible[w]` for every word.

**Stamp offsets for radius R** (`VisionStamp.Offsets(R)`, computed once per distinct `R.Raw`, no floats, no square roots): `r = R.CeilingToInt()`, `R2 = R * R` (Fix64). For `dy = -r .. r`, then `dx = -r .. r` (this order), include `(dx, dy)` when `Fix64.FromInt(dx * dx + dy * dy) <= R2`. Distances are tile-centre to tile-centre; the viewer's own tile `(0, 0)` is always included (R may be 0). Radius 3 gives 29 tiles; radius 2.5 gives 21.

**High-ground rule.** A Ground or Water viewer never marks a tile higher than its eye height. A spotter standing on the higher layer marks those tiles with its own sight, and because the grid is per team, every ally (including ranged units below) then sees them. No line-of-sight raycasting across tiles at the same height in Phase 1. A Grounded flyer loses airborne sight and is bound by the rule, a deliberate counter-play.

Cost: one clear per team plus at most ~400 offsets per unit (R = 11); 200 units is about 80 000 bit sets per tick, cheap at 20 tps.

## 5. Commands and events

No commands, no per-tick events (vision changes every tick; the renderer polls the query API after each step). The simulation does **not** hide anything from commands in Phase 1: a command naming an enemy unit the issuer's team cannot see is applied normally. Phase 2 anti-cheat plan:
- Commands that target an enemy unit (attack, ability) are rejected `target_not_visible` unless the target's tile is `Visible` for the issuer's team on the apply tick; attack-ground and move orders stay unrestricted.
- Ranked replays are re-simulated on a server; a per-command "issuer could see the target" flag computed from these grids feeds the statistical maphack detector ("players who react to hidden units far more often than chance").
- The renderer of a networked client draws only what the local team's grids allow; the full state still exists on every machine (lockstep), which is why detection is server-side.

## 6. Interfaces

Query API on `VisionSystem` (read-only, safe between steps, no allocation except the list method):
- `bool IsTileVisible(TeamId team, int x, int y)`, `bool IsTileExplored(TeamId team, int x, int y)`; false off-map or for an unknown team.
- `bool IsUnitVisibleTo(TeamId team, Unit unit)`: true when the unit's owner is on `team`, or when its floored tile is `Visible` for `team`.
- `void CollectVisibleEnemies(TeamId team, List<EntityId> into)`: clears `into`, then adds, ascending ID, every unit owned by an enemy of the team (`AreEnemies`) or neutral, that `IsUnitVisibleTo` the team.
- `TeamId TeamOfPlayer(PlayerId)` passes through to `PlayerRegistry.TeamOf`.

Touches: `Simulation` (field, `Start`, step 7, hash), `UnitDefinition`, `UnitSchema` and `ContentCatalog` digest (one new field, targeted edits), reads `TerrainGrid` (`Width`, `Height`, `At(x, y).Height`, `.IsRamp`) and `Unit.EffectiveLayer`. Request to the coordinator: `CombatSystem`'s automatic target acquisition (not forced `attack_unit` orders) should skip enemies for which `IsUnitVisibleTo(attackerTeam, target)` is false; it reads the previous tick's vision, a one-tick delay that is deterministic. That edit is a combat change and gets its own sim-reviewer pass. The HQ system's FFA reveal (`ffa_uprooted_reveal_seconds`) will call a Phase 1 follow-up `VisionSystem.SetRevealedToAll(EntityId, bool)` (a hashed sorted set stamped as visible for every team at step 7.2); not part of this build unless the HQ builder needs it.

Must NOT: use floats, `Math.Sqrt` or `Fix64.Sqrt`; iterate a `Dictionary`/`HashSet`; let neutral units grant vision; read anything from the renderer; mutate units or terrain.

## 7. Acceptance tests (`rts/tests/Sim.Tests/Vision/`)

Setup unless stated: 16x16 map, all heights 0, no ramps; two players on teams 0 and 1; test definitions with the given sight.

1. **Stamp counts.** `VisionStamp.Offsets` for R = 3 gives 29 tiles, R = 2.5 gives 21, R = 1 gives 5, R = 0 gives 1 (the pure function accepts 0 even though unit data starts at 1).
2. **Circle edge.** Team 0 unit at (4.5, 4.5), R = 3: tiles (7,4), (6,6), (4,1) visible; (8,4), (7,5), (7,7) not visible.
3. **Explored persists.** The unit moves from (4.5, 4.5) to (12.5, 4.5): after arrival (4,4) is not visible but explored; (12,4) is both; team 1 has explored nothing.
4. **Allies share.** Players 0 and 2 on team 0, player 1 on team 1; a unit of player 2 at (10.5, 10.5) R 2: `IsTileVisible(team 0, 10, 12)` true, `IsTileVisible(team 1, 10, 12)` false.
5. **High ground hidden.** Columns x >= 8 have height 1. Team 0 ground unit at (6.5, 6.5) R 4: (9, 6) not visible, (5, 6) visible.
6. **Spotter.** Add a team 0 unit at (9.5, 2.5) on height 1, R 4: (9, 6) becomes visible to team 0 (via the spotter) and `IsUnitVisibleTo(team 0, enemy at (9.5, 6.5))` is true.
7. **Ramp eye.** Tile (7, 6) is a ramp at height 0: the low unit moved to (7.5, 6.5) sees (9, 6).
8. **Airborne sight.** Air unit with `sight_range_tiles` 9 and `airborne_sight_range_tiles` 6 at (3.5, 3.5) on the height map of test 5: tile (9, 3) (distance 6, height 1) visible, (3, 10) (distance 7) not. Grounded (statuses.md): (9, 3) is no longer visible, and (3, 10) (distance 7, height 0) is visible.
9. **Neutral gives none.** A neutral unit at (8.5, 8.5) marks nothing for any team.
10. **Enemy list.** Team 1 units with IDs 5 and 3 inside team 0's sight and ID 4 outside: `CollectVisibleEnemies(team 0)` returns [3, 5].
11. **Hash.** Two identical runs give equal hashes; moving one unit by one tile before a hash tick changes the hash; a run that differs only in an already-explored tile's history (explored earlier, same visible set now) also changes the hash.
12. **Start vision.** Immediately after `Start` (before the first step), team 0 sees the tiles around its starting units.

## 8. Open questions and Phase 2+ follow-ups

- Line of sight blocked by cliffs or walls at the same height, and vision bonus for units on high ground: not in Phase 1.
- Fire smoke "blocks flyer vision" (Phase 5): a per-tile flag that airborne stamping skips.
- Observer and replay views: union of all teams' grids, computed outside the simulation.
- Should explored-ever remember last-seen structures (ghosts)? Renderer and AI memory, not simulation state.

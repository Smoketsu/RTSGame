<!-- rts/docs/archive/2026-11-16-duplicate-build/phase-1a-systems-spec.md: archived copy of rts/docs/specs/phase-1a-systems-spec.md from the 16 Nov 2026 duplicate build; not current, see the banner. -->
# Phase 1A systems spec: move and fight

> **ARCHIVED, NOT CURRENT.** Written on 16 Nov 2026 by a session that started on the old Phase 0 branch and did not know Phase 1 and most of Phase 2 were already built. Its status, dates and phase references are wrong. Kept only for the ideas listed in this folder's README. The current state is `rts/docs/STATUS.md`.

Status: written 16 Nov 2026; Q2, Q3, Q4, Q5 and Q7 answered 16 Nov 2026 (section 10). Builders may use the recommended defaults for the other questions until the developer replies.

**Goal.** Two scripted armies leave their high-ground plateaus by ramps, path around cliffs on a three-level greybox map, meet in the low basin and fight to the last unit, with the same final hash every run. The harness reports simulation speed.

**How to read this.** Sections 1 to 7 are build tasks in build order; each can be handed to one builder. Section 8 holds every acceptance test, numbered by section. Every number is a tuning key; values the Game Design Proposal (GDP, 26 Oct 2026) does not give are marked *placeholder*. Every tuning key below goes into its data file with the standard comment line (`// what. Unit: … Range: … Source: …`), and every new type, field and local follows the documentation standard.

Conventions used throughout:

- Positions are `FixVec2` in tiles. Tile `(x, y)` covers `[x, x+1) × [y, y+1)`; its centre is `(x + 0.5, y + 0.5)`. +X is east, +Y is north. Tile index `i = y × Width + x`.
- "Ascending ID" means ascending `EntityId`. Every selection that could tie names its tie-break.
- `Direction8 : byte` (new, `Rts.Sim.Terrain`): `E = 0, NE = 1, N = 2, NW = 3, W = 4, SW = 5, S = 6, SE = 7`, offsets `(1,0) (1,1) (0,1) (-1,1) (-1,0) (-1,-1) (0,-1) (1,-1)`. This order is the tie-break order wherever directions tie.

---

## 1. Tuning refactor (build first)

Phase 0 follow-up from the changelog: each faction mechanic declares and parses its own tuning file, so adding a faction never edits the core loader.

### 1.1 Files

| Before | After | Owner class (moves to) |
| --- | --- | --- |
| `data/tuning/momentum.json` | `data/tuning/mechanics/momentum.json` | `Rts.Sim.Factions.Skai.MomentumTuning` |
| `data/tuning/flood.json` | `data/tuning/mechanics/flooding.json` | `Rts.Sim.Factions.Water.FloodingTuning` (renamed from `FloodTuning`) |
| `data/tuning/earth.json` | `data/tuning/mechanics/tailoring.json` | `Rts.Sim.Factions.Earth.TailoringTuning` (renamed from `EarthTuning`) |

The file stem is the mechanic ID. Keys, values and comments inside the files do not change. `statuses.json` (shared statuses), `harness.json`, `match.json`, `hq.json`, `supply.json` and `online.json` stay core. New core files from later sections: `tuning/terrain.json` (§2), `tuning/pathing.json` (§3), `tuning/combat.json` (§4).

### 1.2 Types

- `DataLoader`: a file at `tuning/mechanics/<id>.json` is header-checked (`$schema_version`, `$doc`) and stored unparsed. The loader never names a mechanic. Any other unknown `tuning/` file is still rejected.
- `DataSet.MechanicTuning : IReadOnlyDictionary<string, JsonValue>` — mechanic ID to parsed root, ordinal order.
- `Tuning` drops `Momentum`, `Flood`, `Earth`; gains `Terrain`, `Pathing`, `Combat`.
- `FactionMechanicRegistry` gains:

```
void Register<TTuning>(string id, Func<JsonValue, TTuning> parseTuning, Func<TTuning, IFactionMechanic> factory) where TTuning : class
void BindTuning(IReadOnlyDictionary<string, JsonValue> mechanicTuning)   // parses every declared file once
bool IsTuningBound { get; }
static string TuningPathFor(string mechanicId)                          // "tuning/mechanics/<id>.json"
```

  The existing `Register(id, Func<IFactionMechanic>)` stays for mechanics without tuning (`none`). `BindTuning` throws `DataValidationException` naming the path when a registered mechanic that declares tuning has no file ("is missing; mechanic '<id>' declares a tuning file"), or when a file has no registered mechanic ("no registered mechanic claims this tuning file"). Parse errors keep the existing file-and-key messages. `Create` throws `InvalidOperationException` if the mechanic declares tuning and `BindTuning` has not run. `BindTuning` may run once.
- `MomentumMechanic(MomentumTuning)`, `FloodingMechanic(FloodingTuning)`; new shell `Rts.Sim.Factions.Earth.TailoringMechanic(TailoringTuning)`, ID `"tailoring"` (no faction uses it yet; see Q9). `BuiltInMechanics.RegisterAll` registers all three with their parsers, plus a convenience `CreateRegistry(DataSet data)` that also calls `BindTuning(data.MechanicTuning)`.
- Every tuning class the simulation reads gains `void WriteHash(StateHasher)` writing every property in declaration order. Each mechanic's `WriteHash` writes its ID, then its tuning.
- Hosts (harness, `SimulationHost`, `TestWorld`/`TestData`) switch to `CreateRegistry(data)`.

### 1.3 Hash

Mechanic tuning is now in the state hash (through each mechanic's `WriteHash`), so peers with different mechanic tuning diverge at tick 0. The golden hash changes; re-pin it at the end of 1A (T7.4).

---

## 2. Terrain grid

### 2.1 Model

`Rts.Sim.Terrain.TerrainGrid`, one per match, built in the `Simulation` constructor from the map.

| Field | Type | Unit, range | Notes |
| --- | --- | --- | --- |
| `Width`, `Height` | `int` | tiles, 8 to 1024 | from the map |
| `ElevationLevels` | `int` | levels, `terrain.min_elevation_levels` to 8 | from the map |
| `Level[i]` | `byte` | 0 to `ElevationLevels − 1` | static in 1A |
| `Kind[i]` | `TileKind : byte` | `Open = 0, Ramp = 1, Blocked = 2` | static in 1A |
| `Surface[i]` | `SurfaceState : byte` | `Dry = 0, Flooded = 1, Raised = 2, Scorched = 3` | all `Dry` in 1A |
| `WaterDepth[i]` | `Fix64` | elevation levels of water, 0 to `ElevationLevels` | all 0 in 1A |
| `Version` | `int` | 0 up | +1 on every change |

- A **ramp** tile's `Level` is its lower level `L`; it joins level `L` and `L + 1`. Height in world units is `Level × terrain.elevation_level_height_tiles` (presentation and 1D sight; the simulation compares levels only).
- **Combat level** of a tile = `Level` (ramps count as their lower level).
- Methods: `int Index(int x, int y)`, `bool InBounds(int x, int y)`, `int TileOf(FixVec2 p)` (floor each component, clamp to `[0, Width−1] × [0, Height−1]`), `FixVec2 CentreOf(int i)`, `bool CanStep(int from, Direction8 d)` (§2.2), `void WriteHash(StateHasher)`.
- Mutation for 1C, internal and step-only: `SetSurface(int i, SurfaceState s)`, `SetWaterDepth(int i, Fix64 depth)`. Each records `i` in a dirty-tile set (`SortedSet<int>`), increments `Version`, and is a no-op (no version bump) when the value is unchanged. `TakeDirtyTiles()` returns and clears the set in ascending order. In 1A only tests call these.

### 2.2 Passability (ground)

Orthogonal step `a → b` (`b` in bounds) is allowed when neither is `Blocked` and one of:

1. `Level[a] == Level[b]`, or
2. one of them is a ramp `r` and the other `o` has `Level[o] == Level[r] + 1`.

Any other level difference is a **cliff**. Diagonal step `a → b` through orthogonal neighbours `c1`, `c2` is allowed when the four orthogonal steps `a–c1`, `c1–b`, `a–c2`, `c2–b` are all allowed (no corner cutting). Both rules are symmetric. 1A ignores `Surface` and `WaterDepth` for passability; 1C adds them through the layer rules in §3.1, not here.

### 2.3 Map format (`rts/data/maps/<id>.json`)

New required keys (`$schema_version` stays 1; the game is unreleased):

| Key | Type | Meaning |
| --- | --- | --- |
| `elevation_levels` | int | number of levels, `terrain.min_elevation_levels` to 8 |
| `rows` | array of `height_tiles` strings, each `width_tiles` characters | `rows[0]` is the **top** row (`y = height_tiles − 1`), so the file reads north-up |

Characters: `0`–`7` open tile at that level; `a`–`g` ramp with lower level 0–6 (`a` joins 0 and 1); `#` blocked. Loader errors (file and field named, row and column in the message): wrong row count or length, unknown character, level ≥ `elevation_levels`, ramp lower level > `elevation_levels − 2`, a ramp with no orthogonal neighbour at its level or none at its level + 1, fewer than `terrain.min_elevation_levels` distinct levels actually used, a spawn on a blocked tile. `MapDefinition` gains `ElevationLevels`, `IReadOnlyList<byte> TileLevels`, `IReadOnlyList<TileKind> TileKinds` (index order, `y × Width + x`).

### 2.4 Greybox map rewrite (`maps/greybox_1v1.json`)

64 × 64, `elevation_levels: 3`, spawns unchanged at (8, 8) and (56, 56). 180° rotational symmetry: tile `(x, y)` equals tile `(63 − x, 63 − y)`. Start from all level `1`, then (inclusive ranges):

| Feature | Tiles | Char |
| --- | --- | --- |
| Plateau P0 (level 2) | x 0–15, y 0–15 | `2` |
| Plateau P1 (level 2) | x 48–63, y 48–63 | `2` |
| Basin (level 0) | x 22–41, y 22–41 | `0` |
| P0 ramps (1↔2) | x 16, y 6–8; and y 16, x 6–8 | `b` |
| P1 ramps (1↔2) | x 47, y 55–57; and y 47, x 55–57 | `b` |
| Basin ramps (0↔1) | x 22, y 30–33; x 41, y 30–33; y 22, x 30–33; y 41, x 30–33 | `a` |
| Rock pillars | x 26–28, y 35–37; x 35–37, y 26–28 | `#` |

Plateaus are left only by their ramps; the basin is entered only by its four ramps. Basin area is 400 tiles (9.8 %), under the 35 % lowest-layer cap. Write the rows by hand or with a throwaway script (not committed).

### 2.5 Tuning: `tuning/terrain.json`

| Key | Type | Start | Range | Source |
| --- | --- | --- | --- | --- |
| `min_elevation_levels` | int | 3 | 3 to 8 | GDP Maps (three elevation layers minimum) |
| `elevation_level_height_tiles` | Fix64 | 1.0 *placeholder* | 0.25 to 4 | not in the GDP |

Neither affects simulation results after load, so neither is hashed.

### 2.6 Hash

`TerrainGrid.WriteHash`, written after units: `Width`, `Height`, `ElevationLevels`, `Version`, then for each tile in index order `Level`, `(byte)Kind`, `(byte)Surface`, `WaterDepth`. The dirty set is drained every step that changes terrain (1C), so it is empty at hash time and not hashed; T2.8 asserts it.

---

## 3. Ground-layer flow fields on static terrain

Namespace `Rts.Sim.Pathing`. Built for 1A's ground layer; 1C adds the water and air layers and terrain-change updates by adding a rules class and filling in the invalidation hook, without rewriting the field code.

### 3.1 Layer rules

```
interface IPathLayerRules
{
    MovementLayer Layer { get; }
    bool CanStep(TerrainGrid t, int from, Direction8 d);   // passability
    int TileCost(TerrainGrid t, int tile);                  // 1..254; only called on passable tiles
}
```

`GroundLayerRules`: `CanStep` is `TerrainGrid.CanStep`; `TileCost` returns `pathing.ground_tile_cost_dry` (1C adds per-surface costs here). `PathingService` holds one rules object per layer in a `SortedDictionary<MovementLayer, IPathLayerRules>`; 1A registers ground only.

**Layer used by a unit (1A):** `PathingService.LayerFor(Unit u)` returns `Ground` for every unit, whatever its definition's layer (Q1). This is the single place 1C changes.

### 3.2 Integration field

`FlowField` for (layer, goal tile): `int[] Integrated` (cost to reach the goal; `int.MaxValue` = unreachable) and `byte[] Dir` (`Direction8`, `8` = at goal, `255` = unreachable).

- Entering tile `y` from a neighbour in direction `d` costs `w(d) × TileCost(y)`, where `w` is `pathing.orthogonal_step_weight` for E/N/W/S and `pathing.diagonal_step_weight` for diagonals.
- Build by Dijkstra from the goal over reversed steps: popping `y`, for each `d` in `Direction8` order with `x = y − offset(d)` and `CanStep(x, d)`: `Integrated[x] = min(Integrated[x], Integrated[y] + w(d) × TileCost(y))`.
- Priority queue: binary heap of `long` keys `(cost << 32) | tileIndex`; stale entries (cost above the stored value) are skipped when popped. Keys are unique, so the pop order is total.
- `Dir[x]` = the `d` minimising `Integrated[y] + w(d) × TileCost(y)` over allowed steps `x → y`, ties to the lowest `Direction8` value.
- Overflow guard: `Simulation.Start` throws `InvalidOperationException` when `Width × Height × max(w) × maxTileCost > int.MaxValue − 1`, where `maxTileCost` is the largest tile cost any registered path layer can return (`IPathLayerRules.MaxTileCost`; in 1A the ground layer's dry cost). The greybox and any 1024² map with the start values pass; only extreme tuning fails. (Corrected 16 Nov 2026 after review: the first wording used the constant 254, which rejected maps above about 777².) The 1C spec must revisit this when surface costs approach 254.

### 3.3 Goals and reachability

- `Components`: per layer, `int[] Label` from flood fills seeded in ascending tile index over `CanStep` (8-connected); blocked tiles get −1. Rebuilt when `TerrainGrid.Version` differs from the version it was built at.
- `ResolveGoal(MovementLayer layer, int fromTile, FixVec2 point) → (int goalTile, FixVec2 steerPoint)`: let `t = TileOf(point)`. If `t` is passable and `Label[t] == Label[fromTile]`, the goal is `t` and the steer point is `point` (clamped into the map, §3.5). Otherwise scan every tile with `Label == Label[fromTile]` for the minimum of (squared distance from `point` to the tile centre, tile index); the goal is that tile and the steer point is its centre. Never fails while `fromTile` is passable.

### 3.4 Cache and invalidation

- `FlowFieldCache`: `SortedDictionary<long, FlowField>` keyed by `((long)layer << 32) | goalTile`. Each entry records `BuiltAtVersion` and `LastUsedTick`.
- `GetFlow(layer, goalTile)` returns the cached field when `BuiltAtVersion == TerrainGrid.Version`, else builds it. When full (`pathing.flow_cache_max_fields`), it evicts the entry with the lowest (`LastUsedTick`, key) before inserting.
- **The cache is a pure memo**: a field is a function of terrain and goal only, so cache size and eviction never change results. It is not hashed, and T3.9 runs the exit scenario with cache sizes 1 and 64 and compares hashes.
- 1C hook: `PathingService.OnTerrainChanged(IReadOnlyCollection<int> dirtyTiles)` is called by the simulation after any system that changed terrain (1A: never, so the hook is empty and stale entries simply rebuild on next use). When 1C spreads rebuilds over several ticks under a budget, fields can serve stale data, results then depend on the cache, and the cache contents and versions must enter the hash. That decision belongs to the 1C spec.

### 3.5 Steering, separation and terrain collision (`MovementSystem.Step`, rewritten)

Per tick, after the order system (§5.3) has set each unit's steering goal (`HasMoveTarget`, `MoveTarget`):

1. **Snapshot** every live unit's position. Build a `SpatialGrid` (cells of `pathing.spatial_cell_tiles`, each cell a list in ascending ID) from the snapshot. Query results are filtered by exact distance, and every use is order-independent (sums of raw `Fix64`) or uses an explicit key, so cell size never changes results (T3.10).
2. **Desired step** for each unit with a steering goal, using the snapshot: `from = TileOf(pos)`; `(goal, steer) = ResolveGoal(layer, from, MoveTarget)`. If `from == goal`, aim at `steer`; otherwise aim at the centre of `from + offset(Dir[from])`. `step = clampLength(aim − pos, speedPerTick)`, where `speedPerTick = MatchClock.PerSecondToPerTick(SpeedTilesPerSecond)`. A unit without a goal has a zero step.
3. **Separation** for every unit A (moving or not) against every B within `rA + rB` in the snapshot (ascending ID, B ≠ A): `overlap = rA + rB − |d|` with `d = posA − posB`; `push += normalise(d) × overlap × pathing.separation_strength / 2`. When `|d| == 0`, `normalise(d)` is `(+1, 0)` if `A.Id > B.Id`, else `(−1, 0)`. Clamp the summed push to `pathing.separation_max_push_tiles_per_tick`. All units use the ground snapshot in 1A; 1C separates per layer.
4. **Candidate** `p = pos + step + push`, clamped to `[0, Width − ε] × [0, Height − ε]` with `ε = Fix64.Epsilon`.
5. **Terrain collision**: first clamp `|step + push|` to 0.9 tiles (a safety bound so a unit never crosses more than one tile boundary per axis in a tick; with speeds capped at 16 tiles per second, §4.2, only large pushes ever reach it). Then `a = TileOf(pos)`, `b = TileOf(p)`. Accept `p` if `a == b`, or `b` is a neighbour of `a` and `CanStep(a, dir(a→b))`. Otherwise try `(p.X, pos.Y)`, then `(pos.X, p.Y)`, under the same test; else stay at `pos`. Units collide with terrain as points; radius is ignored against cliffs (Q12).
6. Apply positions in ascending ID (all computed from the snapshot, so order does not matter).
7. **Arrival** (only for Move and AttackMove orders steering at their order point, §5.3): let `dist = |OrderPoint − pos|` after the move.
   - `dist ≤ pathing.arrival_tolerance_tiles` → arrive.
   - Else if `dist < BestDistance − pathing.stall_progress_epsilon_tiles`: `BestDistance = dist`, `StallTicks = 0`; otherwise `StallTicks++`.
   - Arrive when `StallTicks ≥ stall_arrival_seconds` (in ticks) and `dist ≤ pathing.stall_arrival_radius_tiles` (crowd at the destination), or when `StallTicks ≥ stall_give_up_seconds` (in ticks) anywhere (blocked).
   - Arriving clears the order to `None`, clears the steering goal, resets `BestDistance` to `Fix64.MaxValue` and `StallTicks` to 0, and emits `UnitArrivedEvent` and `UnitOrderChangedEvent`.

Normalise uses `FixVec2.Length` (`Fix64.Sqrt`); compare squared distances wherever a comparison is enough. No trigonometry is needed.

### 3.5a Sidestep (amendment, 16 Nov 2026)

Found in the combat build and confirmed in review: separation (step 3) pushes only along the line between two units, so a unit walking exactly head-on into another has no sideways push and can only shove or stall, then gives up after `stall_give_up_seconds`, which silently ends its order. Exact lines are common because flow fields aim at tile centres. Move orders ignore enemies, and friendly traffic meets in chokes, so this must be fixed in 1A.

Add to step 3, for every unit A whose desired step (step 2) is not zero, against every B within `rA + rB` in the snapshot (the same pairs, in the same ascending-ID order):

- `n = normalise(d)` exactly as in step 3, including the coincident-unit rule.
- If `dot(step, −n) > 0` (A is heading into B): `push += perp(normalise(step)) × overlap × pathing.sidestep_strength / 2`, where `perp(v) = (v.Y, −v.X)`. The side is fixed by this formula ("keep right" relative to the step), so two units meeting head-on each move to their own right and pass.
- A unit with a zero step never sidesteps. The summed push (separation plus sidestep) is clamped to `pathing.separation_max_push_tiles_per_tick` as before, then steps 4 to 7 are unchanged.

Also recorded: while a unit is engaged with an attack target, the order system resets its stall tracker (`ResetStall`), because the best distance recorded before the fight is out of date afterwards (§5.3 was silent; built and accepted in review).

Rulings recorded from review: "within `rA + rB`" is a strict less-than; the coincident-unit rule triggers when the computed length is zero (not when `d` is exactly zero); `MovementSystem.MaxMoveTilesPerTick` (0.9) is a documented constant, not a tuning key, because the collision rule is only correct below one tile.

New tuning key in `tuning/pathing.json`, hashed with the other result-affecting pathing keys (after `separation_max_push_tiles_per_tick`):

| Key | Type | Start | Range | Hashed | Source |
| --- | --- | --- | --- | --- | --- |
| `sidestep_strength` | Fix64 | 0.5 | 0 to 2 | yes | *placeholder*; 0 turns sidestep off |

Acceptance tests:

- T3.13 A unit ordered to move through a stationary unit standing exactly on its line (same row, tile centres) reaches a point beyond it and arrives, without the give-up timeout firing.
- T3.14 Two units ordered to swap places along one exact row pass each other and both arrive.
- T3.15 With `sidestep_strength` 0 the T3.13 unit does not pass (it stalls), which pins the behaviour to the key.
- The 0.4-tile offset workaround in the order tests (T5.5) is removed; the test runs on the exact line.

### 3.6 Unit state added for movement

| Field | Type | Unit, range | Hashed |
| --- | --- | --- | --- |
| `HasMoveTarget`, `MoveTarget` (existing, now "steering goal this tick") | `bool`, `FixVec2` | tiles | yes (existing) |
| `BestDistance` | `Fix64` | tiles, 0 to `MaxValue` (`MaxValue` = no progress recorded) | yes |
| `StallTicks` | `int` | ticks, 0 up | yes |

### 3.7 Tuning: `tuning/pathing.json`

| Key | Type | Start | Range | Hashed | Source |
| --- | --- | --- | --- | --- | --- |
| `ground_tile_cost_dry` | int | 10 | 1 to 254 | yes | *placeholder* |
| `orthogonal_step_weight` | int | 10 | 1 to 100 | yes | *placeholder* |
| `diagonal_step_weight` | int | 14 | 1 to 141 | yes | *placeholder* (≈ √2 × orthogonal) |
| `flow_cache_max_fields` | int | 64 | 1 to 1024 | no (memo) | *placeholder* |
| `spatial_cell_tiles` | int | 2 | 1 to 16 | no (no effect on results) | *placeholder* |
| `separation_strength` | Fix64 | 0.5 | 0 to 1 | yes | *placeholder* |
| `separation_max_push_tiles_per_tick` | Fix64 | 0.1 | 0 to 0.5 | yes | *placeholder* |
| `arrival_tolerance_tiles` | Fix64 | 0.1 | 0.01 to 1 | yes | *placeholder* |
| `stall_progress_epsilon_tiles` | Fix64 | 0.02 | 0.001 to 0.5 | yes | *placeholder* |
| `stall_arrival_seconds` | Fix64 | 1.0 | 0.1 to 10 | yes | *placeholder* |
| `stall_arrival_radius_tiles` | Fix64 | 2.0 | 0 to 10 | yes | *placeholder* |
| `stall_give_up_seconds` | Fix64 | 6.0 | 1 to 60 | yes | *placeholder* |

Durations get `…Ticks(MatchClock)` methods as in the existing tuning classes.

---

## 4. Combat model

### 4.1 Damage and armour types

Soft counters only (GDP): every multiplier is strictly positive, so no matchup is a hard zero. The smallest set that gives every type something it is good and bad against is three by three, a balanced triangle (every row and column sums to 3.0, so no type is better on average).

`tuning/combat.json` (names and values *placeholder*, Q7):

```
"damage_types": ["blade", "crush", "pierce"],
"armour_types": ["heavy", "light", "medium"],
"damage_multipliers": {
  "blade":  { "heavy": 1.0,  "light": 0.75, "medium": 1.25 },
  "crush":  { "heavy": 1.25, "light": 1.0,  "medium": 0.75 },
  "pierce": { "heavy": 0.75, "light": 1.25, "medium": 1.0  }
}
```

The loader requires each ID list to be non-empty, unique and snake_case; the matrix to have exactly one entry per (damage, armour) pair; every entry in **0.25 to 2.0** (the lower bound enforces the soft-counter rule). The type IDs are data; no code names them. `CombatTuning.Multiplier(string damageType, string armourType) → Fix64`.

Other `combat.json` keys:

| Key | Type | Start | Range | Source |
| --- | --- | --- | --- | --- |
| `melee_range_max_tiles` | Fix64 | 1.0 | 0 to 2 | *placeholder*; upper bound on a melee weapon's range |
| `uphill_ranged_damage_multiplier` | Fix64 | 0.75 | 0.1 to 1.0 | *placeholder*, Q2 |

### 4.2 Unit data additions (`data/units/**.json`)

| Key | Type | Range | Notes |
| --- | --- | --- | --- |
| `armour_type` | string | an ID from `armour_types` | |
| `acquisition_range_tiles` | Fix64 | 0 to 30, ≥ weapon range | auto-target scan radius (edge to edge) |
| `weapon` | object or `null` | | `null` = unarmed |
| `weapon.kind` | `"melee"` or `"ranged"` | | |
| `weapon.damage_type` | string | an ID from `damage_types` | |
| `weapon.damage` | Fix64 | 0.1 to 10000 | health points per hit |
| `weapon.range_tiles` | Fix64 | 0 to 30; melee ≤ `melee_range_max_tiles` | edge to edge |
| `weapon.cooldown_seconds` | Fix64 | 0.05 to 30 | converted to ticks, at least 1 |
| `weapon.targets` | array of layer names | non-empty subset of `ground`, `water`, `air` | |
| `weapon.hit_delay_ticks` | int | 0 to 10 | ticks from firing to the hit landing (§4.4); 0 = same tick. Melee and ranged weapons use the same field. Start *placeholder*: 0 for melee, 2 (100 ms at 50 ms per tick, ADR-004) for ranged. Source: developer answer to Q5 |

The loader cross-checks type IDs against `combat.json`. `UnitDefinition` gains `ArmourTypeId`, `AcquisitionRangeTiles` and `WeaponDefinition? Weapon` (`Kind`, `DamageTypeId`, `Damage`, `RangeTiles`, `CooldownSeconds`, `HitDelayTicks`, `TargetLayers` as a byte bit mask, bit `n` = `MovementLayer` value `n`). `ContentCatalog.ComputeDigest` covers every new field. Also validate `speed_tiles_per_second ≤ 16` (0.8 tiles per tick, §3.5 step 5).

Starting stats for the 1A units, all *placeholder* (the Haiku data task enters them):

| Unit | Armour | Acq. | Kind | Type | Damage | Range | Cooldown s | Targets |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| skai.fledgling | light | 4 | melee | blade | 4 | 0.25 | 1.5 | ground, water |
| skai.windrunner | light | 7 | ranged | pierce | 8 | 4 | 1.0 | ground, water |
| skai.roc_rider | heavy | 7 | ranged | crush | 18 | 3 | 2.0 | ground, water, air |
| water.tender | light | 4 | melee | blade | 3 | 0.25 | 1.5 | ground, water |
| water.grasper | medium | 6 | melee | crush | 14 | 0.5 | 1.25 | ground, water |
| water.spoutling | light | 7 | ranged | pierce | 7 | 5 | 1.25 | ground, water, air (Q3) |

Every melee unit above gets `hit_delay_ticks: 0` and every ranged unit `hit_delay_ticks: 2` (*placeholder*).

### 4.3 Rules

- **Enemies.** A unit may auto-acquire only units whose owner `Players.AreEnemies` with its own owner. Neutral units (`PlayerId.None`) are never auto-acquired; an explicit attack on them is allowed (Q13). No friendly fire.
- **Can hit.** Attacker has a weapon, and `(TargetLayers >> (int)target.Layer) & 1` is set, where `target.Layer` is the definition's layer (in 1A it never changes).
- **In range.** `|posA − posT|² ≤ (range + rA + rT)²`.
- **Damage per hit** = `weapon.damage × Multiplier(damage_type, target armour)`, then × `uphill_ranged_damage_multiplier` when the weapon is ranged and the attacker's tile combat level is lower than the target's. Melee and level or downhill shots are unmodified. Damage is computed once, at fire time, from post-movement positions (§4.4).
- **Cooldown.** `CooldownTicks` (int, 0 up, hashed) starts at 0. Each combat phase first decrements it towards 0; a unit fires only at 0 and then sets it to the weapon's cooldown in ticks. Period = cooldown ticks exactly.
- **Target acquisition** (order system, §5.3): among live units within `acquisition_range_tiles` (edge to edge, squared test as above) that are enemies and can be hit, pick the minimum key (armed first: 0 if armed, 1 if not; then squared centre distance; then `EntityId`). This uses positions at the start of the tick.
- **Simultaneous resolution.** The combat phase decides every shot from one state, then applies them, so the order units are visited in never changes the outcome:
  1. Decrement cooldowns (ascending ID).
  2. Fire: each unit in ascending ID with health > 0, a live `AttackTarget` it can hit, cooldown 0 and the target in range at post-movement positions computes its damage (§4.3), resets its cooldown, adds a `PendingHit` to the queue with `LandTick = current tick + HitDelayTicks` (§4.4) and emits `ShotFiredEvent`.
  3. Land: remove from the queue, in key order, every pending hit with `LandTick == current tick` (including zero-delay hits fired in step 2). A hit whose target is no longer live (removed on an earlier tick) is discarded, with no event and no retarget. Otherwise subtract its damage from the target's health (`Unit.SetHealth` clamps at 0; a target already at 0 this tick still takes the hit), add it to the target's per-tick damage tally under the hit's attacker ID, emit `AttackHitEvent`. The attacker need not be live.
  4. For each unit hit this tick, in ascending ID: emit `UnitHealthChangedEvent`.
  5. For each unit with health 0, in ascending ID: emit `UnitKilledEvent` (killer = the attacker that dealt the most damage to it this tick, ties to lowest ID; killer owner = the owner recorded in that hit), then `Simulation.RemoveUnit(id, "killed")`. A unit killed this tick still fired this tick (step 2 ran first), and its hits still in the queue land later.
- **Death and removal.** IDs are never reused. References to removed units (`AttackTarget`, `OrderTarget`) are cleared lazily by the order system on the next tick; until then they are hashed as plain IDs.
- **High ground** beyond the uphill multiplier: none in 1A. Low ground not seeing high ground is fog (1D).

### 4.4 Hit delay (pending hits)

A shot is effectively instant but lands a few ticks after it is fired (developer answer to Q5). There is no projectile entity: the hit is bound to the target entity at fire time, cannot be dodged and lands wherever the target has moved, including out of range or out of sight.

- **Fire-time evaluation.** Damage, including the armour multiplier and the uphill multiplier, is computed at fire time from post-movement positions and stored in the hit. Movement, level changes or the attacker's death during the delay never change it.
- **Target gone.** If the target is not live at the land tick, the hit is discarded (no damage, no event, no retarget).
- **Attacker gone.** If the attacker died or was removed, the hit still lands and still counts towards kill credit under the attacker's ID and recorded owner.
- **Cooldown** starts at fire time, independent of the delay.

`Rts.Sim.Combat.PendingHit` (immutable):

| Field | Type | Unit, range |
| --- | --- | --- |
| `LandTick` | `int` (as `MatchClock.Tick`) | tick, fire tick to fire tick + 10 |
| `Attacker` | `EntityId` | the firing unit; may be removed by the land tick |
| `Sequence` | `long` | 0 up, from `CombatSystem.NextHitSequence`, incremented once per hit queued; never reused |
| `AttackerOwner` | `PlayerId` | attacker's owner at fire time |
| `Target` | `EntityId` | bound at fire time |
| `Damage` | `Fix64` | health points, > 0, final value after all multipliers |
| `Kind` | `WeaponKind : byte` | melee or ranged, for events |

The queue is `SortedDictionary<(LandTick, Attacker, Sequence), PendingHit>` (or an equivalent sorted structure) owned by the combat system. The key order (land tick, then attacker ID, then sequence) is the order hits land; `Sequence` makes every key unique. After step 3 no entry has `LandTick ≤ current tick`; the combat system asserts it. `NextHitSequence` (`long`, starts at 0) and the queue are simulation state.

---

## 5. Order commands, order system and events

### 5.1 Unit order state (all hashed, in this order after the existing fields)

| Field | Type | Unit, range |
| --- | --- | --- |
| `Order` | `OrderKind : byte` — `None = 0, Move = 1, Attack = 2, AttackMove = 3` | |
| `OrderPoint` | `FixVec2` | tiles inside the map; zero when unused |
| `OrderTarget` | `EntityId` | `None` when unused |
| `AttackTarget` | `EntityId` | engaged target (explicit or acquired); `None` when none |
| `CooldownTicks` | `int` | ticks, 0 to the weapon cooldown |
| `BestDistance`, `StallTicks` | see §3.6 | |

### 5.2 Commands (`Rts.Sim.Commands.OrderCommands.cs`)

All take `IReadOnlyList<EntityId> units`. The constructor copies and sorts the list ascending and throws `ArgumentException` when it is empty, has duplicates or has more than `MaxUnitsPerOrder = 256` (a schema bound like `WorldBoundTiles`, not tuning). `CommandScript` turns that throw into a file error.

| Kind | Class | Extra parameters |
| --- | --- | --- |
| `move` | `MoveCommand` | `FixVec2 target` |
| `attack` | `AttackCommand` | `EntityId target` |
| `attack_move` | `AttackMoveCommand` | `FixVec2 target` |
| `stop` | `StopCommand` | — |

`MoveUnitCommand` (`move_unit`) stays for existing scripts and is applied through the same code path as `move` with a one-element list.

**Validation.** Command-level checks reject the whole command (`CommandRejectedEvent`):

| Reason | When |
| --- | --- |
| `out_of_bounds` | point outside ±`WorldBoundTiles` (existing) |
| `outside_map` | point outside `[0, Width] × [0, Height]` |
| `unknown_target` | `attack` target not live |

Per-unit checks, run in ascending ID: a failing unit is skipped and gets an `OrderUnitSkippedEvent(command, unit, reason)`. If every unit is skipped, the command is rejected with the first skipped unit's reason instead (so a one-unit command keeps today's reasons).

| Reason | When |
| --- | --- |
| `unknown_unit` | not live |
| `not_owner` | issuer is a player and does not own the unit |
| `no_weapon` | `attack` and the unit is unarmed |
| `target_is_self` | `attack` names the unit itself |
| `target_not_enemy` | `attack` target's owner is not an enemy of the unit's owner and is not `PlayerId.None` |
| `cannot_target_layer` | `attack` and the weapon cannot hit the target's layer |

An unarmed unit given `attack_move` behaves as `move` (accepted). Accepted units: set `Order`, `OrderPoint`, `OrderTarget`; clear `AttackTarget` (for `attack`, set it to the target); clear the steering goal; reset `BestDistance` to `MaxValue` and `StallTicks` to 0; emit `UnitOrderChangedEvent`. `stop` sets `Order = None` and clears everything except `CooldownTicks`. `SpawnUnitCommand` additionally rejects `outside_map` and `blocked_tile` and starts units at `Order = None`.

### 5.3 Order system (`Rts.Sim.Combat.OrderSystem.Step`)

Runs once per tick before movement, per unit in ascending ID, using positions at the start of the tick. It decides each unit's `AttackTarget` and steering goal.

1. **Validate `AttackTarget`.** Clear it if the unit is not live, or (for orders other than `Attack`) is no longer an enemy that can be hit, or its edge distance exceeds `acquisition_range_tiles`.
2. By `Order`:
   - `None` (idle): if armed and no target, acquire (§4.3). With a target, engage. Otherwise no steering goal. Idle units chase and do not return (Q6).
   - `Move`: `AttackTarget = None`; steer to `OrderPoint`. Moving units ignore enemies (Q8).
   - `Attack`: if `OrderTarget` is not live, set `Order = None` (emit `UnitOrderChangedEvent`) and continue as idle. Else `AttackTarget = OrderTarget` and engage. The explicit target is chased at any distance.
   - `AttackMove`: if no target, acquire. With a target, engage; otherwise steer to `OrderPoint`.
3. **Engage**: if the target is in weapon range (start-of-tick positions), clear the steering goal (hold and fire). Otherwise steer to the target's position, re-resolving the goal tile each tick, so a target that changes tile gets a new cached field.

Arrival (§3.5 step 7) applies only while a Move or AttackMove unit steers at its `OrderPoint`.

### 5.4 Events (new classes in `Rts.Sim.Events.CombatEvents.cs`)

| Event | Kind | Fields | Emitted |
| --- | --- | --- | --- |
| `UnitOrderChangedEvent` | `unit_order_changed` | unit, `OrderKind`, point, target | every change of `Order` (command, completion, arrival) |
| `OrderUnitSkippedEvent` | `order_unit_skipped` | command, unit, reason | §5.2 |
| `ShotFiredEvent` | `shot_fired` | attacker, target, `WeaponKind`, land tick | each shot at fire time, §4.3 step 2 (the Unity layer draws a tracer or lobbed shell lasting until the land tick) |
| `AttackHitEvent` | `attack_hit` | attacker, target, damage (Fix64 hp), `WeaponKind` | each landed hit, §4.3 step 3 |
| `UnitHealthChangedEvent` | `unit_health_changed` | unit, health, max health | once per damaged unit per tick |
| `UnitKilledEvent` | `unit_killed` | unit, owner, killer, killer owner | before `UnitRemovedEvent("killed")` |

Health bars read `Unit.Health` and `Definition.MaxHealth` directly or use `UnitHealthChangedEvent`; `UnitRemovedEvent` stays the signal to destroy the view.

### 5.5 Command script additions (`CommandScript`)

- New required top-level key `"map"`: a map ID from `rts/data/maps`. The harness and `SimulationHost` pass that map to `SimulationConfig`. `smoke.json` gets `"map": "greybox_1v1"`.
- New command types `move`, `attack`, `attack_move` with `"units": [ids]` plus `x`/`y` or `"target": id`, and `stop` with `"units"`.

---

## 6. Step order and wiring

`SimulationConfig` takes two more required parameters: `MapDefinition map` and `Tuning tuning`. The old four-parameter constructor is removed. `Simulation` gains `Terrain` (`TerrainGrid`), `Pathing` (`PathingService`, not hashed), `CombatTuning` and `PathingTuning` properties.

New step order (replace the header of `Simulation.cs` and the copy in `rts/src/Sim/README.md`):

1. clear the event log
2. apply this tick's commands in (issuer, sequence) order
3. order system: validate targets, acquire, set steering goals (ascending ID)
4. movement: flow fields, separation, terrain collision, arrival (snapshot, then apply in ascending ID)
5. combat: cooldowns, fire new shots into the pending-hit queue, land due pending hits (land tick, attacker ID, sequence), deaths and removal (ascending ID). Pending hits land after movement and after this tick's new shots, so a zero-delay shot lands in the tick it is fired.
6. faction mechanics in ascending mechanic ID order
7. if terrain changed this tick: `Pathing.OnTerrainChanged(Terrain.TakeDirtyTiles())` (no-op in 1A)
8. state hash every `StateHashIntervalTicks` (reported as `StateHashedEvent`)
9. advance the clock

Hash order (`ComputeStateHash`): content digest, clock, RNG, ID allocator, players, units (with the new fields), **terrain**, **pending hits** (`NextHitSequence`, the entry count, then each entry in queue key order: `LandTick`, `Attacker`, `Sequence`, `AttackerOwner`, `Target`, `Damage`, `(byte)Kind`), **combat tuning**, **pathing tuning (hashed keys only)**, mechanics (ID, state, tuning), hand-over policy. `IFactionMechanic.OnTick`'s summary changes to "after commands, orders, movement and combat".

---

## 7. Harness speed and the 1A exit scenario

### 7.1 Speed report (`rts/tools/BalanceHarness`)

The harness times each `Step()` call with `Stopwatch` (allowed in tools, never in `rts/src/Sim`) and prints after `STATE_HASH=`:

```
SURVIVORS player=<id> units=<n>              one line per player, ascending ID
PERF ticks=<n> wall_ms=<0.0> ticks_per_second=<0.0> realtime_multiple=<0.0> step_avg_ms=<0.000> step_max_ms=<0.000> units_peak=<n>
```

Numbers use the invariant culture. `realtime_multiple = ticks_per_second / match ticks_per_second`. The CI `determinism` job's whole-output `diff` must ignore `PERF ` lines: change it to compare `grep -v '^PERF '` of each run, and note this in `docs/CI.md`. `SURVIVORS` lines are deterministic and stay in the comparison.

### 7.2 Exit scenario script (`rts/data/scripts/exit_1a.json`)

- `map: greybox_1v1`, seed 6657, `ticks: 6000` (5 minutes), players 0 (skai, team 0) and 1 (water, team 1).
- Slots, as tile centres: `s0…s7 = (4.5 + i, 6.5)` for i = 0…7, `s8 = (6.5, 4.5)`, `s9 = (9.5, 4.5)`. Player 1's slot `k` is the point reflection `(64 − x, 64 − y)`.
- Tick 0, issuer 255: player 0 gets `skai.windrunner` on s0–s7 and `skai.roc_rider` on s8–s9 (entities 1–10). Player 1 gets `water.grasper` on s0–s5 and `water.spoutling` on s6–s9 (entities 11–20).
- Tick 20: player 0 `attack_move` units 1–10 to (32.0, 32.0); player 1 `attack_move` units 11–20 to (32.0, 32.0). Both armies must take a plateau ramp and a basin ramp.
- Tick 2400: each player `attack_move`s its own units to the enemy spawn ((56, 56) for player 0, (8, 8) for player 1), to clean up stragglers. Units already dead make that unit skipped, not the command rejected, unless all are dead.

With Q1's default every unit uses the ground field, so the air and water units walk; the scenario stays valid when 1C gives them their own layers, but its golden hash will change then.

### 7.3 Performance scenario (`rts/data/scripts/perf_400.json`)

Same map, `ticks: 2400`. Player 0: 200 `skai.windrunner` on tile centres x 2.5–21.5, y 18.5–27.5 (20 × 10, level 1). Player 1: 200 `water.grasper` at the point reflection of those positions. Tick 20: both `attack_move` everything to the enemy spawn. The script bypasses supply; it only measures speed. Without flooding (1C), this measures the 400-unit step budget early.

---

## 8. Acceptance tests

NUnit tests in `rts/tests/Sim.Tests` unless marked *harness* or *explicit* (`[Explicit, Category("Performance")]`, not run by CI). Test fixtures may build small maps from row strings.

**§1 Tuning refactor**
- T1.1 Loading `rts/data` yields `DataSet.MechanicTuning` keys exactly `flooding`, `momentum`, `tailoring`; `Tuning` has no Momentum/Flood/Earth members.
- T1.2 `BindTuning` with `momentum.json` removed throws `DataValidationException` whose path is `tuning/mechanics/momentum.json`.
- T1.3 An extra `tuning/mechanics/fire.json` with no registered mechanic throws, naming that path.
- T1.4 A bad key in `mechanics/momentum.json` reports file and key, as the old loader did.
- T1.5 `Create("momentum")` before `BindTuning` throws `InvalidOperationException`.
- T1.6 The design values asserted by today's `DataLoaderTests` (drain delay 30 ticks, 5 stacks, 0.04 bonuses, flood gap 4, recasting 0.25) still hold through the new classes.
- T1.7 Changing `momentum.max_stacks` changes the tick-0 state hash of a match with a Skai player.
- T1.8 `DataLoader` source contains no mechanic ID string (a grep test, as the core-never-names-a-faction check).

**§2 Terrain**
- T2.1 The greybox loads: 64 × 64, 3 levels, all three used, spawns on level 2.
- T2.2 Greybox symmetry: for all tiles, level and kind at `(x, y)` equal those at `(63 − x, 63 − y)`.
- T2.3 Greybox spot checks: (8, 8) level 2 open; (16, 7) ramp level 1; (22, 31) ramp level 0; (31, 31) level 0; (27, 36) blocked; (20, 20) level 1.
- T2.4 Each loader error in §2.3 is produced by a minimal bad map and names the file, field, row and column.
- T2.5 `CanStep`: same level allowed; 0→1 without a ramp refused; ramp(0)↔1 and ramp(0)↔0 allowed; ramp(0)↔2 refused; anything into `Blocked` refused; a diagonal past one blocked or cliff orthogonal refused; `CanStep(a, d) == CanStep(b, opposite(d))` for every tile pair of the greybox.
- T2.6 `SetSurface` and `SetWaterDepth` bump `Version` only on a real change and list the tile once in `TakeDirtyTiles`, in ascending order.
- T2.7 Changing one tile's surface, depth or level changes the state hash (per-field hash coverage, like the existing unit-field test).
- T2.8 After any step, the dirty set is empty.

**§3 Pathfinding**
- T3.1 On an open 8 × 8 level map, `Integrated` from goal (0, 0) equals the octile formula with weights 10/14 times cost 10, and `Dir` follows the E-first tie-break at a hand-checked tile.
- T3.2 On a map with a wall across it and one gap, every reachable tile's `Dir` chain reaches the goal through the gap with no step refused by `CanStep`.
- T3.3 On the greybox, from (8, 8) to (32, 32), following `Dir` from the start reaches the goal, uses a `b` ramp tile and an `a` ramp tile, and never takes a refused step.
- T3.4 Building the same field twice gives identical arrays; building it on a copy of the grid gives identical arrays.
- T3.5 `ResolveGoal` on a blocked target returns the nearest passable tile, tie to the lower index; on a target in another component (an island) it returns the closest tile in the unit's component.
- T3.6 A unit ordered to (32, 32) from (8, 8) arrives (`UnitArrivedEvent`) within 60 seconds of ticks, and at every tick its tile is its previous tile or a neighbour reachable by `CanStep`.
- T3.7 Separation: two units spawned on the same point end at least `0.9 × (rA + rB)` apart within 40 ticks; the lower ID is west.
- T3.8 Crowd arrival: 12 units ordered to one point all reach `Order = None` within 30 seconds.
- T3.9 The exit scenario gives the same final hash with `flow_cache_max_fields` 1 and 64.
- T3.10 The exit scenario gives the same final hash with `spatial_cell_tiles` 1, 2 and 7.
- T3.11 A unit pushed by separation towards a cliff edge never ends a tick on a tile it could not step to.
- T3.12 `Simulation.Start` throws on a map and tuning combination over the integration overflow guard.

**§4 Combat**
- T4.1 `combat.json` loads; a multiplier of 0, a missing pair, an unknown type, a duplicate ID each fail with file and key.
- T4.2 A unit file with an unknown `armour_type`, a melee range above `melee_range_max_tiles`, an acquisition range below weapon range, or a speed above 16 each fail.
- T4.3 Damage: a Windrunner (pierce 8) hitting a light target deals exactly `8 × 1.25 = 10`; hitting a heavy target deals 6.
- T4.4 Uphill: a Windrunner on level 1 hitting a level 2 target deals `8 × 1.25 × 0.75`; from level 2 down, the plain value; a melee hit uphill is unmodified; a shooter standing on a ramp (combat level 0) at a level 1 target is uphill.
- T4.5 Cooldown: an attacker with a 1.0 s cooldown at 20 ticks per second fires on ticks t, t+20, t+40 against a stationary target in range.
- T4.6 Simultaneous death: two identical units with exactly lethal damage spawned in range of each other on the same tick both die on the same tick, whatever their ID order.
- T4.7 Kill credit: two attackers hitting one unit on its last tick credit the higher-damage attacker, tie to lower ID.
- T4.8 Acquisition: with an unarmed enemy at distance 2 and an armed enemy at distance 5, the armed one is chosen; with two armed enemies at the same distance, the lower ID.
- T4.9 A unit with `targets: ["air"]` does not auto-acquire a ground enemy.
- T4.10 Neutral units are never auto-acquired; allies are never auto-acquired; there is no friendly fire.
- T4.11 A killed unit emits, in order, `attack_hit`, `unit_health_changed`, `unit_killed`, `unit_removed` on its land tick, and its ID is never reused.
- T4.12 Delay respected: a ranged attacker with `hit_delay_ticks: 2` that fires on tick t emits `shot_fired` (land tick t+2) on tick t, and the target's health changes and `attack_hit` is emitted on tick t+2, not before. Repeat with 1 and 10.
- T4.13 Zero delay: with `hit_delay_ticks: 0`, `shot_fired` and `attack_hit` for the same shot appear in the same tick, in that order, and the pending-hit queue is empty after the step.
- T4.14 No dodge: a target that moves out of weapon range (and up a level) during the delay still takes the full damage computed at fire time, including the uphill multiplier if the shot was fired uphill and not if it was fired level.
- T4.15 Target died first: a pending hit whose target is removed (killed by another unit) before its land tick is discarded: no `attack_hit`, no damage elsewhere, no retarget; the queue no longer holds it.
- T4.16 Attacker died first: an attacker killed after firing and before its hit's land tick still deals the damage on the land tick, and gets kill credit (its ID and recorded owner in `unit_killed`) if the hit is lethal.
- T4.17 Land order: hits landing on the same tick apply in (attacker ID, sequence) order, so `attack_hit` events on that tick appear in that order, and `NextHitSequence` grows by one per shot.
- T4.18 Pending hits in the hash: with a hash interval falling between a shot's fire and land ticks, two runs give identical hashes at every interval; changing that weapon's `hit_delay_ticks`, or one pending hit's damage or target (test hook), changes the hash at that interval.

**§5 Orders and events**
- T5.1 Each command-level rejection reason in §5.2 is produced by a minimal case.
- T5.2 Each per-unit skip reason is produced; a mixed list applies to the valid units and emits one `order_unit_skipped` per invalid one; an all-invalid list is rejected with the first skipped unit's reason.
- T5.3 Constructors throw on empty, duplicate or 257-unit lists; unit lists are stored sorted.
- T5.4 `move_unit` and `move` with one unit produce identical state hashes.
- T5.5 A moving unit passes an enemy within acquisition range without firing; an `attack_move` unit on the same path stops and fights, then resumes and arrives.
- T5.6 `attack` on a target out of acquisition range chases it; when the target dies the order becomes `None` with one `unit_order_changed`.
- T5.7 `stop` mid-move halts the unit within one tick and keeps its cooldown.
- T5.8 Idle units engage an enemy entering acquisition range and do not return after the kill.
- T5.9 An unarmed unit accepts `attack_move` and moves like `move`.
- T5.10 Spawn rejects `outside_map` and `blocked_tile`.
- T5.11 Every new unit field (§5.1) changes the hash when changed alone.
- T5.12 `CommandScript` parses every new command type and the `map` key, and rejects a missing `map`.

**§6 Step order**
- T6.1 Within one tick, events appear in step order: command rejections, order changes, arrivals, shots fired, hits, health changes, kills and removals, then the hash.
- T6.2 A unit killed this tick does not move or acquire on the next tick (it is gone), and no other unit keeps it as `AttackTarget` after the next order phase.

**§7 Harness and exit scenario**
- T7.1 Exit determinism: `exit_1a.json` run twice in-process gives identical hashes at every interval and the same final hash.
- T7.2 Exit outcome: by tick 6000 at least one player has no units; each army had at least one unit on a `b` (plateau) ramp tile and at least one unit of either army stood on an `a` (basin) ramp tile; no unit ever ended a tick on a tile it could not have stepped to from its previous tile.
- T7.3 *Harness*: running the exit script prints `STATE_HASH=`, one `SURVIVORS` line per player and one `PERF` line with all seven fields; two runs differ only in `PERF` lines. CI's determinism job passes with the `PERF` filter.
- T7.4 Golden hash: after the sim-reviewer pass, pin the exit scenario's final hash next to the existing golden test, and re-pin the smoke golden.
- T7.5 *Explicit*, performance budgets from the plan (measured without flooding in 1A; re-checked when 1C adds it):
  - `perf_400.json`: average step ≤ 5 ms, worst step ≤ 15 ms.
  - `exit_1a.json`: `realtime_multiple` ≥ 50 (at least 1000 ticks per second on one core).
  - Flow-field rebuild after a terrain change: not measurable in 1A (static terrain); the 1C spec sets the test.

The developer runs T7.5 on their machine at the 1A exit and records the `PERF` line in the milestone report. A miss gets a profiling task before new features (plan rule).

---

## 9. Out of scope for 1A

- Economy, resources, supply enforcement, production, buildings and the HQ (1B).
- Fog of war, sight and the low-ground-cannot-see-high-ground rule (1D); `elevation_level_height_tiles` is stored for it.
- Flooding behaviour, tides, water depth effects on speed and pathing, surface costs, and terrain-change field updates (1C); only the fields and hooks exist.
- Air and water movement layers and their fields; Grounded and slowed; Momentum, Drench, Downdraft (1C).
- Simulated projectiles (entities with flight paths that can miss or be dodged), splash, abilities, armour values beyond the type multiplier, regeneration, formations, hold position, patrol, waypoint queues.
- AI (1D) and the build-order bot (1B).
- Unity work (camera, selection, control groups, health bars): its own brief. This spec only fixes what it reads: the commands in §5.2 and the events in §5.4.
- Balance. Soft counters here are structural (positive multipliers); the paper balance pass (1D) checks that no unit loses every equal-cost fight.

---

## 10. Questions for the developer

Each open question has a recommended default that builders use until answered.

1. **Layers in 1A.** Air (Roc Rider) and water (Grasper, Spoutling) layers arrive in 1C. Until then, should every unit path on the ground field? *Recommended: yes, through `PathingService.LayerFor`; targeting still uses the definition's layer.*
2. **High-ground combat rule.** Answered 16 Nov 2026: default accepted; ranged shots uphill take `uphill_ranged_damage_multiplier` 0.75, melee unaffected.
3. **Spoutling targets.** Answered 16 Nov 2026: default accepted; the Spoutling hits all layers.
4. **Armed workers.** Answered 16 Nov 2026: default accepted; Fledgling and Tender carry a weak melee weapon and acquisition puts armed enemies first.
5. **Projectiles.** Answered 16 Nov 2026: default rejected; no projectile entities, but every hit lands `weapon.hit_delay_ticks` after firing, bound to its target and undodgeable (§4.4).
6. **Idle units.** *Recommended: idle units engage enemies inside acquisition range, chase, and stay where the fight ends (no return and no leash).*
7. **Damage and armour types.** Answered 16 Nov 2026: default accepted; three by three (blade, crush, pierce against heavy, light, medium) with the balanced table in §4.1.
8. **Move ignores enemies.** *Recommended: yes, the classic RTS rule: move never fires; attack-move fights on the way.*
9. **Earth tuning owner.** `earth.json` has no mechanic. *Recommended: add a `tailoring` shell mechanic in `Factions/Earth` that owns `mechanics/tailoring.json`; no earth faction yet.*
10. **Greybox layout.** *Recommended: the layout in §2.4 (corner plateaus with two ramps each, a central basin with four ramps and two rock pillars).*
11. **Ramps and speed.** *Recommended: ramps cost and move like flat ground in 1A.*
12. **Unit size against terrain.** *Recommended: units collide with cliffs as points (radius ignored), and unit speed is capped at 16 tiles per second so no move crosses more than one tile per tick.*
13. **Neutral units.** *Recommended: never auto-acquired; can be attacked explicitly.*
14. **Harness matchup limit.** `harness.json` holds `water_vs_earth_median_max_seconds`, a faction-named key in a core file. *Recommended: leave it until the Phase 2 harness spec, which can move matchup limits into a data table.*
15. **Chasing a target that cannot be reached** (raised in review, 16 Nov 2026). An idle or attack-moving unit that acquires a target inside acquisition range but on the far side of a cliff chases forever: arrival and give-up do not apply while it is engaged, so an attack-move never resumes. *Recommended: a unit that makes no progress toward its attack target for `stall_give_up_seconds` drops that target, ignores it for the same length of time, and returns to its order. Not built in 1A until the developer answers.*

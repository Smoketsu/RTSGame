<!-- rts/docs/decisions/ADR-006-terrain-model.md: how the map is represented as a tile grid with layered ground states, so flooding, raising, scorching and pathfinding all read one model. -->
# ADR-006: Terrain is a tile grid of layered ground states

Status: Accepted, 26 October 2026 (start of Phase 1).

## Context

The design makes the map a weapon: Skai fly over it, water floods it, earth raises it, and later fire scorches it. The development plan requires "terrain as layered ground states" from the first prototype so that floods, raised ground, scorch and pathfinding never need a rewrite. Phase 0 positions are continuous `FixVec2` tiles with no terrain at all.

## Decision

- The map is a rectangular grid of `WidthTiles × HeightTiles` tiles. Tile `(x, y)` covers positions `[x, x+1) × [y, y+1)`; a unit's tile is the floor of its position. Tiles are indexed `y * Width + x` and iterated in ascending index order wherever order matters.
- Each tile holds, in `TerrainTile`:
  - `Height`: elevation layer, `byte` 0 to 7. The design wants at least three layers per map; the lowest layer may cover at most 35% of the map (checked by the map checklist, Phase 2).
  - `IsRamp`: a tile that connects two adjacent layers for ground movement.
  - `WaterDepth`: `Fix64` depth units, 0 = dry. Shallow and deep are derived from depth thresholds in `data/tuning/flood.json`, never stored.
  - `Surface`: `Dry`, `Raised`, `Scorched`. Flooding is not a surface value; it is `WaterDepth > 0`, so a scorched tile can still be flooded and dried by rules rather than by replacing a state.
  - `Blockers`: flags `Wall`, `Footprint` (building). Blocked tiles are impassable for ground and water movement; air ignores them.
- Passability for ground and water movement between two edge-adjacent tiles: neither is blocked, and either both have the same `Height` or they differ by exactly one and at least one of them is a ramp. Diagonal moves need both orthogonal neighbours passable (no corner cutting). Air movement is passable everywhere inside the map.
- Movement speed on a tile is the unit's base speed times a multiplier from the tile and the unit's movement layer: dry ground 1.0; shallow and deep water use `flood.json` (ground units slowed, water units faster); raised and scorched multipliers are data too. Multipliers are `Fix64` fractions in data, never code.
- Every terrain change goes through `TerrainGrid` mutators that run only inside a step, raise a `TerrainChangedEvent` listing the tile indices, and increment `TerrainGrid.Version`. Pathfinding caches read the version and rebuild. Flood water redistribution (ADR for Phase 1 flooding spec) visits tiles in ascending index order.
- Map data (`data/maps/<id>.json`) gains: `height_rows` (one string per row, one digit per tile), `ramp_rows` (`.` or `R`), `resource_fields` (`x`, `y`, `kind` common or rare, `amount`), and `core` (`x`, `y`, `radius_tiles`). The loader validates row counts and lengths against the size and that spawns and fields sit on unblocked tiles.
- The terrain is part of the state hash: every tile's height, ramp flag, depth, surface and blockers in ascending index order, plus the version.

## Alternatives considered

- Navmesh or polygon terrain: smoother paths but far harder to keep deterministic under constant flood and raise changes; a grid updates in O(changed tiles).
- Storing "flooded" as a surface state: loses the depth needed for shallow versus deep and makes "scorch boils shallow water dry" a state-machine special case instead of a depth rule.
- Height as a continuous `Fix64`: the design talks in discrete layers (high ground, ramps, lowest layer cap), and discrete heights make ramp and cliff rules exact.

## Consequences

- Flooding, earth raising and fire scorching are all tile rule passes over one structure; none needs new geometry.
- Fog of war (Phase 1) gets its "low ground cannot see high ground" rule from `Height` directly.
- Memory is small: a 256 × 256 map is 65 536 tiles of a few bytes each; hashing it every second is cheap.
- Unit positions stay continuous, so rendering is smooth; only passability and speed are per tile.

<!-- rts/docs/decisions/ADR-007-pathfinding.md: flow-field pathfinding per movement layer over the terrain grid, rebuilt when terrain changes. -->
# ADR-007: Flow-field pathfinding per movement layer

Status: Accepted, 26 October 2026 (start of Phase 1).

## Context

Armies of up to 200 supply must path across terrain that changes every few seconds (tides, floods, walls, raised ground) on three movement layers, and a Grounded flyer must switch from air to ground mid-flight. Per-unit A* would recompute hundreds of paths on every terrain change. The development plan names flow fields as the fit for large armies.

## Decision

- A **flow field** is computed per `(movement layer, goal tile)` over the whole grid:
  1. **Cost field**: each tile's traversal cost is an integer, `CostScale / speedMultiplier` (with `CostScale` = 1024), so slow tiles cost more; impassable tiles are infinite. Air uses cost 1024 everywhere.
  2. **Integration field**: Dijkstra from the goal tile outward using a binary heap keyed by `(cost, tile index)`, so ties break by index and the result is identical on every machine. Straight edges add the neighbour's cost; diagonal edges add 1.5× (fixed integer ratio 3/2), with no corner cutting.
  3. **Flow**: each tile points to the edge-adjacent or diagonal neighbour with the lowest integration value (ties by neighbour order N, E, S, W, NE, SE, SW, NW). The goal tile points to itself.
- Fields are cached in `FlowFieldCache` keyed by `(layer, goal index)` and tagged with `TerrainGrid.Version`; a lookup with a stale version rebuilds. The cache keeps at most `max_cached_flow_fields` (tuning) entries and evicts the entry with the smallest creation sequence number, which is deterministic.
- **Unit movement** each tick, in ascending entity ID order: a unit with a goal reads the flow direction of its current tile, steers toward the centre of the pointed-to tile, and advances by `speed × tile multiplier × seconds per tick`. It arrives when within `arrival_radius_tiles` (tuning) of the goal position or when its tile is the goal tile and the goal is unreachable from elsewhere. Air units ignore the field and fly straight (Phase 0 `MoveTowards`).
- **Layer switching**: a unit's effective layer is its definition's layer unless a status overrides it (Grounded forces `Ground`). The movement system asks the status layer each tick, so a Grounded flyer immediately follows ground fields and respects walls and cliffs.
- **Unreachable goals**: if the integration value at the unit's tile is infinite, the unit moves to the reachable tile nearest the goal (lowest integration value among tiles adjacent to the unreachable region is Phase 2 work); Phase 1 simply reports `MoveUnreachableEvent` and stops.
- No local avoidance or unit collision in Phase 1; units may overlap. Separation steering is a Phase 2 item and must stay deterministic (ascending-ID neighbour scans).

## Alternatives considered

- Per-unit A*: simple, but re-pathing every unit after every tide tick is the exact cost spike the design warns about.
- Hierarchical pathfinding: better for huge maps; our maps are at most a few hundred tiles across, where a full Dijkstra is well under a millisecond.
- Navmesh with Unity's NavMesh: engine-bound, non-deterministic, and incompatible with the headless harness.

## Consequences

- Many units sharing a goal (a move order on a selected group) share one field; the cost is per goal, not per unit.
- Every terrain change invalidates all fields, which is correct but means a flood that ticks every second rebuilds fields every second. That is acceptable at Phase 1 map sizes; if profiling disagrees, fields can be invalidated by region.
- Flow fields are not hashed: they are a pure function of terrain and goal, rebuilt on demand. Unit positions and goals are hashed, so a wrong field would still surface as a hash mismatch.

## Amendment, 26 October 2026 (same branch, before acceptance review)

- Flow-field costs are team-agnostic: fields stay keyed by `(layer, goal)` and use the team-less terrain speed query, which prices every water tile as enemy water for ground units. A team's own water therefore looks slower to the pathfinder than it is, which only makes ground units prefer dry routes through their own floods. Keying fields by team as well would multiply the cache size by the team count; revisit if playtests show bad routing.
- Movement itself uses the team-aware speed query, so actual speed on own-team water is correct.

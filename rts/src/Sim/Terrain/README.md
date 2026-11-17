<!-- rts/src/Sim/Terrain/README.md: placeholder for the terrain grid, water depth, surface state and flood redistribution; not built yet. -->
# Terrain (Phase 1 placeholder)

Not built yet. Phase 1 (Prototype, Skai vs water, Jan-Mar 2027) adds here:

- A tile grid in which each tile has a height, a water depth and a surface state: dry, flooded, raised or scorched.
- Flood redistribution, with tides, applied in a fixed tile order so every peer computes the same result.

Flood limits are data in `rts/data/tuning/flood.json`, not constants in code.

`TileBlockers.BuiltWall` marks a worker-built wall: it blocks ground movement and flood flow, and water already on the tile stays until the tide retreats.
`TileBlockers.Gate` marks a worker-built gate: a wall to flood water and to every unit except those of the tile's `GateTeam` (the gate owner's team), which `IsBlockedFor(team)`, `TerrainGrid.IsStandable(index, layer, team)` and `IsStepPassable(from, to, layer, team)` honour; the team-less forms treat a gate as a wall (walls_and_tech.md G.1).

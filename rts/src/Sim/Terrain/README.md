<!-- rts/src/Sim/Terrain/README.md: placeholder for the terrain grid, water depth, surface state and flood redistribution; not built yet. -->
# Terrain (Phase 1 placeholder)

Not built yet. Phase 1 (Prototype, Skai vs water, Jan-Mar 2027) adds here:

- A tile grid in which each tile has a height, a water depth and a surface state: dry, flooded, raised or scorched.
- Flood redistribution, with tides, applied in a fixed tile order so every peer computes the same result.

Flood limits are data in `rts/data/tuning/flood.json`, not constants in code.

<!-- rts/src/Sim/Vision/README.md: placeholder for fog of war, computed inside the simulation rather than the renderer; not built yet. -->
# Vision (Phase 1 placeholder)

Not built yet. Phase 1 (Prototype, Skai vs water, Jan-Mar 2027) adds fog of war here.

- Visibility is computed in the simulation, not in the renderer, so every peer and every replay sees the same fog.
- Visibility state affects future ticks, so it is written into the state hash in a fixed order (ADR-005).

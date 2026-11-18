<!-- rts/src/Sim/Pathing/README.md: placeholder for flow-field pathfinding with one field per movement layer; not built yet. -->
# Pathing (Phase 1 placeholder)

Not built yet. Phase 1 (Prototype, Skai vs water, Jan-Mar 2027) adds here:

- Flow fields: one field per movement layer (ground, water, air), shared by all units on that layer.
- Layer switching: a flyer that becomes Grounded switches from the air layer to the ground layer.

Movement layers are defined in `rts/src/Sim/Units/MovementLayer.cs`.

Flow fields are per team while a gate stands (18 Nov 2026): `FlowFieldCache.Get(layer, goal, team)` keys by the passing team only while `TerrainGrid.GateTileCount > 0`; with no gate every key uses `TeamId.None` and fields are shared as before (walls_and_tech.md G.1 rule 6). Callers pass the moving unit's team: `MovementSystem`, `CombatSystem.CanReach`.

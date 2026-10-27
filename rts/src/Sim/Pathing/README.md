<!-- rts/src/Sim/Pathing/README.md: placeholder for flow-field pathfinding with one field per movement layer; not built yet. -->
# Pathing (Phase 1 placeholder)

Not built yet. Phase 1 (Prototype, Skai vs water, Jan-Mar 2027) adds here:

- Flow fields: one field per movement layer (ground, water, air), shared by all units on that layer.
- Layer switching: a flyer that becomes Grounded switches from the air layer to the ground layer.

Movement layers are defined in `rts/src/Sim/Units/MovementLayer.cs`.

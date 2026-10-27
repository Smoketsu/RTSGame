<!-- rts/docs/PHASE1_PLAN.md: the Phase 1 (Prototype, Skai versus water) build order, who builds what, and the exit test. -->
# Phase 1 plan: Prototype, Skai versus water

Goal (development plan): Skai versus water is playable against the AI on a greybox map, and the headless runner plays AI-versus-AI matches to the end. Every number stays in data; every system stays deterministic and reviewed.

## Build order and ownership

| Step | What | Depends on | Tier |
| --- | --- | --- | --- |
| 1 | Terrain grid with height layers, ramps, water depth, surface states and blockers; map format with rows, resource fields and the Core; terrain in the state hash (ADR-006) | Phase 0 | Fable |
| 2 | Flow-field pathfinding per movement layer with cache invalidation on terrain change; unit movement on fields; air flies straight (ADR-007) | 1 | Fable |
| 3 | Specs with acceptance tests for Flooding and tides, Momentum, Grounded and slowed statuses, fog of war, skirmish AI (`rts/docs/specs/`) | design doc, ADR-006/007 | Opus designs |
| 4 | Combat: weapons and armour types in unit data, damage table in tuning, target acquisition, attack and attack-move commands, death | Phase 0 units | Sonnet builds |
| 5 | Shared statuses (Grounded, slowed) and layer switching; Momentum plug-in | 2, 3 | Sonnet builds from the spec |
| 6 | Rooted and Uprooted HQ with survivability floor and Last Stand behind its switch; player Defeated on HQ death | 4 | Sonnet builds |
| 7 | Economy: two resources, depleting fields from map data, workers and drop-offs, supply cap, static defence caps, train and build commands | 1, 6 | Sonnet builds |
| 8 | Flooding with the shared tide clock, shallow and deep speeds, first-pass waves | 1, 2, 3 | Sonnet builds from the spec |
| 9 | Unit and building data for 5 to 6 units per faction | 4, 6, 7 | Haiku |
| 10 | Fog of war in the simulation with the high-ground rule | 1 | Opus designs, Sonnet builds |
| 11 | Basic skirmish AI producing commands | 4, 6, 7 | Opus |
| 12 | Unity: camera, selection, control groups, greybox map rendering | 1 | Sonnet (needs the editor to verify) |
| 13 | Review of every simulation change (sim-reviewer, then Fable) | all | Opus, Fable |

## Exit test

- The headless runner plays a scripted AI-versus-AI match on `greybox_1v1` to a decided outcome, deterministically (same hash twice).
- A move order routes a ground unit around a cliff and up a ramp; a Grounded flyer stops at a wall; a flooded tile slows a ground unit and speeds a water unit.
- All tuning values from the design doc are read from data and exercised by at least one test.

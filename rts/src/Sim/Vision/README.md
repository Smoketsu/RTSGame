<!-- rts/src/Sim/Vision/README.md: what the Vision folder holds (fog of war inside the simulation), its rules and where it plugs into Simulation. Part of rts/src/Sim; spec: rts/docs/specs/fog_of_war.md. -->
# Vision (fog of war)

Fog of war is computed in the simulation, not the renderer, so every peer, replay and AI sees the same fog (architecture rule 7). Asset Store kits only draw it.

- `VisionSystem` (`sim.Vision`): one `TeamVision` per team in ascending `TeamId`. `Recompute` is step 9 of `Simulation.Step` (after faction mechanics, before the state hash) and runs once at the end of `Start`. `SpawnUnit` also stamps a new unit's sight at once (`OnUnitSpawned`) so it can fight on its spawn tick; the step's recompute rebuilds the grids from scratch, so the end-of-tick state and hash are exactly the recompute's.
- `TeamVision`: two bit grids over the terrain (1 bit per tile, bit `i % 64` of word `i / 64`, `i = y * Width + x`): `Visible` (seen this tick) and `Explored` (seen on any tick, never cleared). Both are hashed per team in ascending `TeamId` right after the terrain hash.
- `VisionStamp`: the integer sight circle (`dx*dx + dy*dy <= R*R` in `Fix64`, no floats, no square roots). Radius 3 gives 29 tiles, 2.5 gives 21. Cached per radius in a `SortedDictionary` (not hashed).

Rules:
- Allies (same `TeamId`) share one grid. Neutral units give no vision.
- Sight radius is `sight_range_tiles`, or `airborne_sight_range_tiles` while `Unit.EffectiveLayer` is Air (a Grounded flyer uses the ground figure and the rule below).
- High-ground rule: a ground or water viewer never marks a tile higher than its eye height (its tile's height, plus 1 on a ramp tile). Flyers see every height. A spotter on the higher layer reveals those tiles to the whole team. No line-of-sight raycasting across tiles of the same height yet.

Query API (read-only, safe between steps): `IsTileVisible`, `IsTileExplored`, `IsUnitVisibleTo`, `CollectVisibleEnemies`, `TeamOfPlayer`, plus the aliases `CanTeamSee`, `IsExplored`, `CanTeamSeeUnit`.

Readers: `CombatSystem` automatic target acquisition skips enemies the attacker's team cannot see (an explicit `attack_unit` order still works). **Commands are not yet filtered by vision**: a command naming an enemy the issuer's team cannot see is applied normally. The Phase 2 anti-cheat plan (spec section 5) rejects such commands as `target_not_visible` and feeds a server-side maphack detector.

Follow-ups: line of sight across walls, smoke that blocks flyer vision, `SetRevealedToAll` for the FFA uprooted-HQ reveal (spec sections 6 and 8).

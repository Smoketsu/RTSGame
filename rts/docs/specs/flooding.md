<!-- rts/docs/specs/flooding.md -->
<!-- Implementation spec for the water faction mechanic "flooding": the shared tide clock, flood sources, deterministic water redistribution on the ADR-006 terrain grid, flood limits, depth speed effects, healing and first-pass waves. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by sim-reviewer. -->
# Spec: Flooding with tides and waves (mechanic ID `flooding`)

## 1. Purpose and design-doc source

- **Flooding** (Factions / Water / Signature mechanic): "Structures release water that flows downhill and fills low ground first. On flooded ground, water units move faster and heal, and enemy ground units are slowed."
- **Tides**: "At high tide the water reaches its full extent ... at low tide it pulls back and exposes ground." "One tide clock per match is shared by every water player; tide upgrades only affect the owner's water." Tide length 210 s; "210 doesn't divide 240, high tide drifts across cycles."
- **Waves**: "Active abilities that push water outward: shove enemy units back ... or briefly extend the flood onto dry ground." Phase 1 builds one wave (push + extend). Currents ("carry units along set lanes") are deferred.
- **Depth**: "deep water halves enemy ground units' speed, shallow water slows them slightly. Neither blocks them."
- **Flood limits**: "Floods stop 4 tiles short of enemy resource fields and drop-offs, and building footprints stay dry ... When earth raises ground under water, the water moves to the lowest neighbouring tiles in a fixed order. Walls can stand in shallow water and block the flow."

## 2. Data

Existing keys: `tuning/match.json` `tide_cycle_seconds` (210); `tuning/flood.json` `enemy_resource_gap_tiles` (4), `deep_water_enemy_speed_multiplier` (0.5), `shallow_water_enemy_speed_multiplier` (0.85). `lowest_layer_max_map_fraction` is the map checklist's, not read here.

New keys in `tuning/flood.json` (add to `FloodTuning` with the same comment text; every one is a **placeholder** unless a source is given):

| Key | Unit | Range | Default | Controls / source |
| --- | --- | --- | --- | --- |
| `depth_units_per_height_layer` | depth units | 1 to 16 | 4 | Water depth that fills one height layer; sets how "downhill" compares with depth. Placeholder. |
| `shallow_water_min_depth` | depth units | 0.0625 to 4 | 0.25 | Below this a tile counts as dry for speed and healing (it still holds water). Placeholder. |
| `deep_water_min_depth` | depth units | 0.5 to 16 | 2 | At or above this a tile is deep. Placeholder; Currents and depth. |
| `flood_update_interval_seconds` | seconds | 0.25 to 5 | 1 | How often emission, tide and flow run. Placeholder. |
| `flow_passes_per_update` | passes | 1 to 16 | 4 | Redistribution passes per update; sets flow speed. Placeholder. |
| `low_tide_volume_fraction` | fraction of high-tide volume | 0 to 1 | 0.5 | Volume a team may hold at low tide. Placeholder; Tides. |
| `min_retreat_per_second` | depth-unit tiles per second | 0 to 64 | 0.5 | Floor on tide retreat speed, so a team with no sources still drains. Placeholder. |
| `flood_source_release_radius_tiles` | tiles | 0 to 6 | 2 | Ring search radius for the tile a source releases onto (its own footprint stays dry). Placeholder. |
| `resource_claim_radius_tiles` | tiles | 1 to 32 | 10 | A resource field within this Chebyshev distance of an enemy drop-off is an "enemy resource field". Placeholder; Flood limits. |
| `water_unit_shallow_speed_multiplier` | multiplier | 1 to 3 | 1.25 | Water-layer unit speed on shallow water. Placeholder; Flooding. |
| `water_unit_deep_speed_multiplier` | multiplier | 1 to 3 | 1.5 | Water-layer unit speed on deep water. Placeholder; Flooding. |
| `water_unit_dry_speed_multiplier` | multiplier | 0.1 to 1 | 1 | Water-layer unit speed on dry tiles ("weak on dry land" lever). Placeholder. |
| `water_unit_heal_per_second` | health points per second | 0 to 50 | 2 | Healing of water-layer units on their own team's flooded tiles. Placeholder; Flooding. |
| `wave_cooldown_seconds` | seconds | 1 to 300 | 30 | Per-caster wave cooldown. Placeholder; Waves. |
| `wave_length_tiles` | tiles | 1 to 20 | 6 | Wave reach from the caster. Placeholder; Waves. |
| `wave_width_tiles` | tiles, odd | 1 to 9 | 3 | Wave width. Placeholder; Waves. |
| `wave_water_depth` | depth units | 0 to 8 | 1 | Water added to each wave tile. Placeholder; Waves. |
| `wave_push_tiles` | tiles | 0 to 6 | 2 | Distance enemy non-air units are shoved. Placeholder; Waves. |

The loader converts every depth key to **drops** (below) with `RoundToInt(value * 256)` and must reject a value that is not a multiple of 1/256 (`"must be a multiple of 1/256"`).

Unit-data fields (optional, `UnitDefinition`; loader validates ranges):
- `flood_source_output_per_second` (Fix64, depth-unit tiles per second, 0 to 64, default 0). Greater than 0 makes the unit a **flood source**. Placeholder for water structures: 1.
- `flood_source_capacity` (Fix64, depth-unit tiles, 0 to 4096, default 0): the source's share of its team's high-tide volume. Required > 0 when output > 0. Placeholder: 40.
- `can_cast_wave` (bool, default false): Tidecaller only.

## 3. State

**Representation constant** (not tuning, like ADR-007's `CostScale`): `FloodMath.DropsPerDepthUnit = 256`. All flood maths is integer **drops**; `drops = depth.Raw >> 24`, `depth = Fix64.FromRaw((long)drops << 24)`, both exact. Invariant: every `TerrainTile.WaterDepth` is a whole number of drops (`TerrainGrid.SetWater` throws otherwise).

Terrain (authoritative, hashed by the terrain in ADR-006 order): `WaterDepth` (Fix64, depth units, 0 to 2^20) and **new** `WaterTeam` (`TeamId`, `None` exactly when depth is 0): the team whose water fills the tile. This is an ADR-006 amendment the coordinator must accept (see README). Water of different teams never mixes.

Mechanic state (`FloodingMechanic.WriteHash`, in this order): `Id`; count of wave cooldowns; then per caster ascending `EntityId`: caster ID, `NextWaveTick` (int, ticks, 0 to int.MaxValue). Entries for removed casters are pruned at the start of `OnTick`. Everything else (tide level, no-flood masks, BFS distances) is derived each update from hashed state and is not stored across ticks.

Cached at `OnMatchStart` (pure functions of tuning, not hashed): `TideTicks = SecondsToTicks(tide_cycle_seconds)` (4200 at 20 tps), `UpdateTicks = max(1, SecondsToTicks(flood_update_interval_seconds))` (20), `LayerDrops` (1024), `WaveCooldownTicks` (600), heal per update `HealPerUpdate = water_unit_heal_per_second * FromInt(UpdateTicks) / FromInt(TicksPerSecond)` (2.0).

## 4. Per-tick algorithm

`OnTick` every tick: (a) prune cooldowns; (b) tide events: `t = tick % TideTicks`; `t == 0` emits `TideTurnedEvent(Low)`, `t == TideTicks / 2` (integer division) emits `TideTurnedEvent(High)`; (c) if `tick % UpdateTicks == 0` run the update below. Read terrain into `drops[]`, `team[]` at the start; write back at the end.

**Tide level** (pure helper `Rts.Sim.Core.TideClock`, also used by the UI and AI): `tri = T - |2t - T|` (0 at low, T at high; integers). `f = low + ((One - low) * FromInt(tri)) / FromInt(T)` with `low = low_tide_volume_fraction`; evaluate exactly in that bracket order. Tick 0 is low tide.

**Update order** (all loops ascending; neighbours always N=(x,y-1), E=(x+1,y), S=(x,y+1), W=(x-1,y), skipping off-map):

1. *No-flood masks.* For each team T that owns a flood source or holds water (ascending `TeamId`): anchors = every drop-off (incl. a Rooted HQ) owned by a player not allied with T (all its footprint tiles), plus every resource field whose tile is within `resource_claim_radius_tiles` (Chebyshev) of such a drop-off. `noFlood[T][i]` is true when `max(|dx|,|dy|) <= enemy_resource_gap_tiles` to any anchor tile.
2. *Limits*, ascending tile index `i` with `drops[i] > 0`: if the tile has a `Footprint` blocker, **Displace(i)**; else if `noFlood[team[i]][i]`, remove all its drops. Walls do not displace (walls may stand in water).
   - `Accepts(j, T)`: `j` in map, no `Wall`, no `Footprint`, not `noFlood[T][j]`, and `team[j]` (or this pass's pending team) is `None` or `T`.
   - `Level(i) = Height(i) * LayerDrops + drops[i]` (int).
   - Displace(i): among neighbours `j` with `Accepts(j, team[i])` pick the lowest `Level(j)`, ties to the earlier of N, E, S, W; move all drops there (claiming `team[j]`). No candidate: the drops are removed.
3. *Tide.* For each team T ascending: sources = live units owned by T's players with output > 0, ascending ID. `C = sum(RoundToInt(capacity * 256))`, `V = (FromInt(C) * f).FloorToInt()`, `W = sum(drops[i] where team[i] == T)` (long). `out_s = (output * FromInt(256) * FromInt(UpdateTicks) / FromInt(TicksPerSecond)).FloorToInt()` (left to right).
   - `W < V`: for each source: `o = Origin(s)`; skip if none; `a = min(out_s, V - W)`; `drops[o] += a`, `team[o] = T`, `W += a`.
   - `W > V`: `R = min(W - V, max(sum(out_s), minRetreatDrops))` where `minRetreatDrops` uses the `out_s` formula on `min_retreat_per_second`; **Retreat(T, R)**.
   - Origin(s): for `r = 0 .. flood_source_release_radius_tiles`, the tiles at Chebyshev distance exactly `r` from the source's tile, ascending index; the first with `Accepts(·, T)`.
   - Retreat(T, R): multi-source BFS (4-neighbour, N, E, S, W) seeded with every source's origin tile (dist 0, seeds ascending index), visiting only tiles with `team == T`; unvisited T tiles get dist `int.MaxValue`. Order T tiles by dist descending, then index descending; take `min(drops, R_left)` from each until `R_left == 0`. Water pulls back toward its sources.
4. *Redistribution*, `flow_passes_per_update` times (Jacobi: every pass reads one snapshot):
```
for pass in 1..P:
  level[i] = Height(i) * LayerDrops + drops[i]   for all i        // snapshot
  delta[i] = 0; pending[i] = None                                 for all i
  for i in 0 .. N-1 ascending:
    if drops[i] == 0 or HasWall(i): continue
    remaining = drops[i]
    for j in neighbours(i) in order N, E, S, W:
      if not Accepts(j, team[i]) (pending counts as owner): continue
      diff = level[i] - level[j]
      if diff <= 0: continue
      amount = min(diff / 5, remaining)        // integer floor; 5 = 4 neighbours + 1, prevents overshoot
      if amount == 0: continue
      remaining -= amount; delta[i] -= amount; delta[j] += amount
      if team[j] == None and pending[j] == None: pending[j] = team[i]
  for i ascending: drops[i] += delta[i]; if pending[i] != None and team[i] == None: team[i] = pending[i]
                   if drops[i] == 0: team[i] = None
```
   Water crosses cliffs downward freely (no ramp needed) and climbs only when levels allow. Total drops are conserved by redistribution.
5. *Write back.* Tiles whose drops or team changed, ascending, go to `TerrainGrid.SetWater` in one batch (one `TerrainChangedEvent`).
6. *Healing.* Units ascending ID with `Definition.Layer == Water`, health above 0 and below max, on a tile with `WaterDepth >= shallow_water_min_depth` and `WaterTeam` equal to the owner's team: `SetHealth(Health + HealPerUpdate)`.

**Speed table** (`TerrainSpeedTable.WaterMultiplier(layer, depth, unitTeam, waterTeam)`, read by the movement speed chain, see statuses.md 6): depth class Dry `< shallow_min`, Shallow `< deep_min`, else Deep. Layer from `EffectiveLayer` (a Grounded flyer is Ground). Air: One. Water layer: dry/shallow/deep keys above (any team's water). Ground: Dry or `waterTeam` allied with the unit: One; enemy Shallow `shallow_water_enemy_speed_multiplier`; enemy Deep `deep_water_enemy_speed_multiplier`. Water-layer units use ground passability (ADR-006).

## 5. Commands and events

- `CastWaveCommand(tick, issuer, sequence, EntityId caster, FixVec2 target)` in `Factions/Water/`, script name `cast_wave`. Rejections in this order: `unknown_unit`, `not_owner`, `not_wave_caster` (`can_cast_wave` false), `out_of_bounds`, `on_cooldown` (`tick < NextWaveTick`), `no_direction` (target tile equals caster tile). Apply: direction `(ux, uy)` from `dx, dy` (target minus caster position): `|dx| > 2|dy|` gives `(sign dx, 0)`; `|dy| > 2|dx|` gives `(0, sign dy)`; else `(sign dx, sign dy)`. Area: for `s = 1..wave_length_tiles`, `o = -(w-1)/2 .. (w-1)/2`: tile `(cx + s*ux - o*uy, cy + s*uy + o*ux)`, collected into an ascending set (off-map skipped). Each area tile with `Accepts(·, casterTeam)` gains the wave drops (walls are washed over, never filled). Then each enemy unit, ascending ID, whose effective layer is not Air and whose tile is in the area moves `wave_push_tiles` single steps of `(ux, uy)` tiles, stopping before the first step that is not passable for its layer (ADR-006 rules, no corner cutting). Pushes never set `LastSelfMoveTick` (momentum.md). `NextWaveTick = tick + WaveCooldownTicks`.
- Events: `TideTurnedEvent(tick, TidePhase phase {Low, High}, int cycleIndex = tick / TideTicks)`; `WaveCastEvent(tick, caster, owner, sbyte dirX, sbyte dirY, int tilesFlooded)`; `UnitPushedEvent(tick, unit, FixVec2 from, FixVec2 to)`; `FloodWaterRemovedEvent(tick, TeamId team, int drops, string reason)` with reason `"resource_gap"` or `"displaced_off"`, once per team per reason per update when drops > 0.

## 6. Interfaces

- `FloodingMechanic : IFactionMechanic` (exists as a shell). Pure integer rules go in `Factions/Water/FloodRules.cs` as static methods over arrays (`Redistribute`, `Retreat`, `Displace`, `NoFloodMask`, `Origin`) so tests run without a simulation.
- Reads `sim.Terrain` (Fable's `TerrainGrid`: `Width`, `Height`, tile `Height`, `Blockers`, `WaterDepth`, `WaterTeam`, ADR-006 passability) and writes only through `TerrainGrid.SetWater(list of (index, depth, team))`. Recommend to Fable: bump `TerrainGrid.Version` only when a tile's depth class (dry, shallow, deep) or blockers change, so a 1 s flood tick does not rebuild every flow field.
- Reads drop-offs and resource fields from the economy (`sim.Economy.DropOffs`: ID, owner, footprint tiles; `sim.Map.ResourceFields`). Until economy lands, the anchor list is empty.
- Must NOT: name a faction, draw from `sim.Rng`, touch flow-field caches, hold `Dictionary`/`HashSet`, or use any float. Healing uses the definition layer; speed uses the effective layer.

## 7. Acceptance tests (`rts/tests/Sim.Tests/Flooding/`)

1. **Flat flow, exact drops.** `FloodRules.Redistribute` on 3x1, heights 0, drops `[1000,0,0]`, team 0: 1 pass gives `[800,200,0]`; 2 passes `[680,280,40]`; 4 passes `[543,325,132]`. Sum stays 1000.
2. **No overshoot.** 3x3 flat, 1000 drops in the centre, 1 pass: centre 200, each orthogonal neighbour 200, corners 0.
3. **Downhill across a cliff.** 2x1, heights `[1,0]`, drops `[100,0]`, LayerDrops 1024, 1 pass: `[0,100]`.
4. **Walls block flow.** 3x1, tile 1 has `Wall`, drops `[1000,0,0]`, 4 passes: `[1000,0,0]`.
5. **Teams do not mix.** 3x1, `[1000(T0), 0, 1000(T1)]`, 1 pass: `[800,200,1000]`, tile 1 owned by T0 (ascending index claims first).
6. **Footprint stays dry.** 3x1 flat, drops `[0,300,0]` T0, tile 1 gets `Footprint`; one update: tile 1 is 0, tile 2 holds 300 (E beats W on the tie), tile 0 is 0.
7. **Tide level.** T = 4200, low 0.5: f at t = 0 is 0.5, t = 1050 is 0.75, t = 2100 is 1, t = 3150 is 0.75. `TideTurnedEvent` High at tick 2100, Low at ticks 0 and 4200.
8. **Emission respects volume.** One source, output 1, capacity 1 (256 drops): after the tick-0 update the team holds 128 drops; after the tick-20 update, 129. With capacity 40 on a flat 16x16 map, after the tick-400 update the team holds exactly 21 x 256 = 5376 drops.
9. **Retreat toward the source.** `FloodRules.Retreat` on 5x1 flat T0 `[200,200,200,200,200]`, seed tile 0, R = 256: `[200,200,200,144,0]`.
10. **Resource gap.** 16x1 flat, enemy drop-off anchor at x = 12, gap 4: `NoFloodMask` true for x = 8..15 only; after 60 updates with a source (output 1, capacity 40) at x = 0, x = 7 holds water and x = 8..15 hold none.
11. **Speeds and healing.** A test definition with speed 2.5 tiles/s on a flat east-bound path: enemy ground unit on deep water moves 0.0625 tiles per tick; allied ground unit 0.125; water unit speed 3 on shallow moves 0.1875. A water unit at 50/100 health on its own team's shallow water reads 52 after one update and 56 after three; on enemy water it stays 50.
12. **Wave.** Caster at (2.5, 5.5), target (10.5, 5.5), defaults: tiles x 3..8, y 4..6 gain 256 drops each; an enemy ground unit at (5.5, 5.5) ends at (7.5, 5.5) with one `UnitPushedEvent`; a second cast 599 ticks later is rejected `on_cooldown`; the state hash differs from a run without the wave.

## 8. Open questions and Phase 2+ follow-ups

- Currents (lanes that carry units), wave variants (carry own units forward, wash over walls with knockback), tide upgrades per owner, tide-triggering abilities.
- Earth raising ground under water uses `FloodRules.Displace` from the earth system (Phase 4); fire boils shallow to dry and deep to shallow (Phase 5) through `SetWater`.
- Should flow-field costs see enemy water? Fields are per layer, not per team (ADR-007); Phase 1 costs use the dry multiplier. Coordinator decision.
- Whether water stops at walls only when shallow (design: "walls can stand in shallow water"): Phase 1 walls always block.
- Under-construction structures should not emit: needs the economy's construction flag.
- Spoutling "stronger on flooded ground" is not modelled yet (statuses.md 8).

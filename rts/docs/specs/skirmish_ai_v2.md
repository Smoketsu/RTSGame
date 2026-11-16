<!-- rts/docs/specs/skirmish_ai_v2.md -->
<!-- Build spec for the skirmish AI rework set by the developer on 16 Nov 2026 ("a cpu AI that can reliably play the game"): drop-off depots and expansion fields in the data and maps (package A), an AI economy that expands by depot and by HQ relocation and never freezes (B), a grouped army with a fixed production mix (C), and the measured exit criteria (D). Extends rts/docs/specs/skirmish_ai.md and the rules in the header of rts/src/Ai/AiBrain.cs; evidence in rts/docs/reports/ai_trace_2026-11-16/README.md. Part of rts/docs/specs (index: README.md). -->
# Spec: Skirmish AI v2 (expansion, anti-freeze, grouped army)

## 0. Goal, evidence, ground rules

Goal (developer, 16 Nov 2026): a CPU AI that reliably plays whole games. Today every capped game freezes 4 to 7 minutes in.

Evidence used (trace review, 16 Nov 2026):

- **Confirmed** (measured in the traces): each side mines exactly 3000 common and 0 rare; fields are empty at 240 to 390 s; the last real command is at 270 to 450 s; in all 9 capped games both players sit from then to 2400 s with idle workers, bank under 50, no army and `cmds=-`; Water trained 0 to 1 Shellback per game against a 3:1 target; up to 14 stance changes per game (7 `Massing`/`Defend` flips in 69 s); single units die at the map centre while `Massing`; 0 rejected commands; greybox_1v1 is not 180-degree symmetric (66 height and 16 ramp mismatches, counted per tile of each mismatched pair).
- **Inferred** (not proven by the review): the composition miss comes from the producer loop (`AiBrain.cs` lines 730 to 751); piecemeal deaths come from `rally_at_core`. Package C treats the first as unproven and makes the builder prove it with a failing test first. Reading the code for this spec: a Roost's `trains` list holds `skai.windrunner` and `skai.roc_rider`, so `PickByDeficit` with that producer's filter can only return Windrunner, and it returns it even when Windrunner's deficit is negative (`best == null || deficit > bestDeficit`). That is the likely mechanism; test C-1 confirms or refutes it.

Ground rules for every package:

- The AI stays outside `Simulation.Step`. It is a pure function of `AiWorldView` + `AiProfile` + `AiMemory` + its own `DeterministicRandom`. AI memory is not simulation state and is not hashed; its determinism is proven by harness hashes (unchanged rule from skirmish_ai.md section 3).
- Only Fix64 and integers; collections iterated in ascending entity ID or field index; distances compared squared; every tie broken by lower field index, then lower entity ID, unless a rule says otherwise. No new random draws: the only draw stays the attack-threshold jitter (and package C draws it less often).
- Commands are only those a player can issue: `gather`, `train_unit`, `build_structure`, `move_unit`, `attack_move`, `attack_unit`, `root_hq`, `uproot_hq`, `pick_cycle`. No new command or event types. No change to `rts/src/Sim` code; package A changes only `rts/data` (which changes the content digest, see D.5).
- Only team knowledge: enemy units through `VisibleEnemies`; field amounts through the field memory of B.2 (a field's live amount is read only while its tile is visible to the team). Terrain, spawns, field positions and start amounts are public map data.
- Every number below is a data key with a house-format comment (`// <what>. Unit: . Range: . Source: .`). New AI keys live in `rts/data/ai/aggressive.json` and `defensive.json` (same values in both unless stated); harness keys in `rts/data/tuning/harness_batch.json`. Every new value is a **placeholder**.
- Build order: A, then B, then C, then D. Each package ends with `dotnet build` and `dotnet test` green and a `sim-reviewer` pass on `rts/src/Ai` (the AI is determinism-relevant even though it is outside `rts/src/Sim`).

Existing rules that are **deleted or replaced** (say so in the `AiBrain.cs` header when you change it):

1. Rule 2's gate "only while the HQ is Rooted" and the HQ-only `_fieldsInRange` list: replaced by served fields (B.3).
2. Rule 4's worker target `min(max_workers, workers_per_field x fields in range)`: replaced (B.5 rule W).
3. Rule 5's per-producer loop (each idle producer buys its own best unit): replaced by C.2 rule P. `PickByDeficit` itself stays.
4. The Massing rally at the Core (`RallyPoint`'s `rally_at_core` branch): deleted; `rally_at_core` now moves the attack's staging point (C.2 rule G).
5. Attack reinforcement (idle army units join a running attack): deleted; only the attack group attacks (C.2 rule G).
6. Threshold redraw on every `Massing` entry: now only when an attack ends (C.2 rule D).
7. `AllIn` ("no field within field_max_distance_tiles of the HQ has resources"): now "no field on the map has known resources" (B.5 rule A).
8. Rule 1's header text "Phase 1: the HQ stays Rooted; no relocation": replaced by B.5 rule H.

## Package A: data and maps for expansion

### A.1 Drop-off buildings (two new unit files)

Files `rts/data/units/skai/perch_depot.json` (ID `skai.perch_depot`) and `rts/data/units/water/pearl_depot.json` (ID `water.pearl_depot`). The developer's IDs `perch_depot` and `pearl_depot` get the faction prefix every unit ID has (ADR-003). Names from `art/CONCEPT_BRIEF.md` (Buildings, round 13). Copy the structure and comment style of `skai/roost.json`; every comment ends `Source: Economy (a drop-off per base); placeholder.` (the name and display keys cite the concept brief).

| Key | `skai.perch_depot` | `water.pearl_depot` | Unit, range |
| --- | --- | --- | --- |
| `display_name` | `"Perch Depot"` | `"Pearl Depot"` | text, 1 to 40 chars |
| `faction_id` | `"skai"` | `"water"` | faction ID |
| `kind` | `"building"` | `"building"` | |
| `layer` | `"ground"` | `"ground"` | |
| `max_health` | 350 | 400 | health points, 1 to 100000 |
| `speed_tiles_per_second` | 0 | 0 | tiles/s |
| `supply` | 0 | 0 | supply |
| `radius_tiles` | 0.5 | 0.5 | tiles, 0.05 to 10 |
| `cost_common` | 100 | 100 | resource units, 0 to 100000 |
| `cost_rare` | 0 | 0 | resource units |
| `build_seconds` | 20 | 20 | seconds, 0.05 to 3600 |
| `footprint` | 1 | 1 | tiles, 0 or 1 |
| `is_drop_off` | true | true | boolean |
| `armour_type` | `"structure"` | `"structure"` | armour type |
| `sight_range_tiles` | 8 | 8 | tiles, 1 to 64 |

No `trains`, no `provides_supply`, no flood keys. Health follows the existing pattern (water structures 50 above Skai: Tidepool 450, Roost 400). The economy code already treats a complete `is_drop_off` structure as a drop-off (`EconomyRules.IsDropOff`), so no simulation change is needed.

### A.2 Field amounts

Every `"kind": "common"` field on `greybox_1v1`, `long_lowland` and `twin_chokes` goes from 1500 to 3000, including the new fields of A.3. Rare fields stay at 600 (question Q1). Update each map's `resource_fields` comment and `$doc` to describe the new layout (main pair, natural pair, flank field beside a rare).

### A.3 Expansion fields per map

Today each side has only its two main fields; the rare fields are 23 to 45 tiles away. Each map gains, per side, a **natural pair** (two common fields 6 to 7 tiles apart, just outside the main's ramp, 12.7 to 15.9 tiles from the HQ, so the HQ does not already serve them) and one **flank field** (common) 3 to 4 tiles from a rare field, so a depot or HQ there gathers rare too. All are 180-degree images of each other ((x, y) to (63 - x, 63 - y)), on layer 1 or 0, not ramps, at least 3 tiles (Chebyshev) from every other field, 23 tiles or more from the Core centre (radius 4), and ground-reachable from both spawns (checked with the `IsStepPassable` rule on the height and ramp rows, after the greybox fix of A.4).

**Append** the new fields after the existing entries, in the order below, so existing indices (0 to 5 or 0 to 7) do not change (scripts and tests use them).

| Map | Index | Tile | Role | Height | To own HQ | To Core | Expected depot tile (B.5 rule E) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| greybox_1v1 | 6 | (21, 10) | P0 natural | 1 | 13.7 | 23.9 | (21, 7) |
| | 7 | (21, 4) | P0 natural | 1 | 13.9 | 29.4 | same depot |
| | 8 | (42, 53) | P1 natural | 1 | 13.7 | 23.9 | (42, 56) |
| | 9 | (42, 59) | P1 natural | 1 | 13.9 | 29.4 | same depot |
| | 10 | (12, 49) | P0 flank, beside rare 4 (8, 52) | 1 | 41.7 | 26.2 | |
| | 11 | (51, 14) | P1 flank, beside rare 5 (55, 11) | 1 | 41.7 | 26.2 | |
| long_lowland | 8 | (5, 47) | P0 natural | 1 | 15.7 | 30.7 | (8, 47) |
| | 9 | (11, 47) | P0 natural | 1 | 15.9 | 25.7 | same depot |
| | 10 | (58, 16) | P1 natural | 1 | 15.7 | 30.7 | (55, 16) |
| | 11 | (52, 16) | P1 natural | 1 | 15.9 | 25.7 | same depot |
| | 12 | (22, 55) | P0 flank, beside rare 4 (20, 52) | 1 | 27.6 | 25.3 | |
| | 13 | (41, 8) | P1 flank, beside rare 6 (43, 11) | 1 | 27.6 | 25.3 | |
| twin_chokes | 6 | (21, 52) | P0 natural | 1 | 12.7 | 23.0 | (21, 55) |
| | 7 | (21, 59) | P0 natural | 1 | 13.3 | 29.4 | same depot |
| | 8 | (42, 11) | P1 natural | 1 | 12.7 | 23.0 | (42, 8) |
| | 9 | (42, 4) | P1 natural | 1 | 13.3 | 29.4 | same depot |
| | 10 | (7, 13) | P0-side flank, beside rare 5 (10, 10), SW basin | 0 | 41.5 | 30.7 | |
| | 11 | (56, 50) | P1-side flank, beside rare 4 (53, 53), NE basin | 0 | 41.5 | 30.7 | |

All new fields: `"kind": "common", "amount": 3000`. Distances are tiles from the HQ spawn point and the Core centre to the field tile centre. How rare is used: rare fields are gathered once a depot or the HQ serves them (B.5 rules G and W); the flank field makes such a site worth a depot. Rare only buys units whose `cost_rare` is above 0 (Q4).

### A.4 greybox_1v1 symmetry fix (map bug)

Fix, exact: for every tile (x, y) with x + y > 63, set its height digit and ramp flag to those of tile (63 - x, 63 - y). This changes 33 height tiles and 8 ramp tiles (the review's 66 and 16 count both tiles of each pair), all in rows y = 26 to 44 on the basin rim; afterwards the terrain is symmetric under the 180-degree turn and still under the x/y swap. The east and north basin ramps move from x = 44 to x = 43 (rows 31 and 32) and from y = 44 to y = 43 (columns 31 and 32). Result spot checks: (31, 43) height 0 ramp; (32, 43) height 1 ramp; (43, 31) height 0 ramp; (43, 32) height 1 ramp; (44, 31) and (44, 32) height 1, no ramp; (32, 32) height 0. The layer-0 basin shrinks from 441 to 408 tiles (10.0 percent of the map, under the 35 percent cap). Spawns, plateaus and the Core are unchanged. Generate the rows with a throwaway script, paste them, and let test A-1 guard the result.

### A.5 Tests (package A, in `rts/tests/Sim.Tests/Data/MapLayoutTests.cs`, new)

- **A-1 Symmetry.** For every shipped map with exactly 2 spawns: height and ramp of (x, y) equal those of (W-1-x, H-1-y); spawn 1 equals (W - x0, H - y0) of spawn 0; the field multiset maps onto itself under (x, y, kind, amount) to (W-1-x, H-1-y, kind, amount); the Core centre maps to itself under p to (W, H) - p. Passes for all three maps (fails on today's greybox).
- **A-2 Reachability.** On a started simulation of each map, every field tile is reachable from both spawn tiles by breadth-first search over `sim.Terrain.IsStepPassable(..., MovementLayer.Ground)`.
- **A-3 Amounts and counts.** Every common field holds 3000, every rare 600; field counts: greybox 12 (10 common), long_lowland 14 (10 common), twin_chokes 12 (10 common).
- **A-4 Core clearance.** Every field tile centre is at least 12 tiles from the Core centre (map authoring rule of this spec; the expected value is a test constant with that comment).
- **A-5 Depot site exists.** For each field of index 6 or more, some tile at Chebyshev distance 3 to 6 has a flat 3x3 block on the field's height layer with no ramp and is more than 2 tiles (Chebyshev) from every field.
- **A-6 Depot definitions.** `skai.perch_depot` and `water.pearl_depot` load with `IsDropOff` true, footprint 1, kind building, the right faction, empty `Trains`, `ProvidesSupply` 0, cost 100/0.
- **A-7 Depot in the economy.** TestWorld-style match on shipped data: a worker gathering from a field 3 tiles from a complete Perch Depot and 20 tiles from the Rooted HQ deposits at the depot (`ResourceDepositedEvent` names the depot's ID); while the depot is under construction it deposits at the HQ.
- **A-8 Greybox fix.** The spot checks of A.4 hold.

## Package B: AI economy (expansion and anti-freeze)

### B.1 New and changed profile keys (root of each profile)

| Key | Value | Unit | Range | Meaning |
| --- | --- | --- | --- | --- |
| `max_workers` (changed) | 24 (both profiles) | workers | 1 to 100 | Worker count the AI stops training at. |
| `workers_per_field` (meaning narrowed) | 3 | workers | 1 to 8 | Workers per served **common** field. |
| `rare_workers_per_field` | 2 | workers | 0 to 8 | Workers per served rare field. |
| `field_max_distance_tiles` (meaning changed) | 10 | tiles | 2 to 64 | A field is served when its centre is this close to an own drop-off (was: to the HQ). |
| `expand_at_seconds` | 240 | seconds | 0 to 1800 | Earliest time for the first depot by the clock. Source: Core gameplay loop (Expansion 3 to 9 min). |
| `expand_interval_seconds` | 150 | seconds | 0 to 900 | Added per depot already placed. |
| `expand_when_served_remaining_fraction` | 0.5 | fraction | 0 to 1 | Expand early when served fields hold less than this share of their start amount. |
| `max_live_depots` | 2 | depots | 0 to 6 | Most live depots (B.3); 0 turns depots off. |
| `expand_min_field_remaining` | 300 | resource units | 1 to 1000000 | A field below this is not an expansion or relocation target (unless no field qualifies). |
| `expand_safe_radius_tiles` | 14 | tiles | 2 to 64 | A target this close to a known enemy structure, an uncleared enemy spawn or a recently seen enemy army is unsafe. |
| `enemy_army_memory_seconds` | 60 | seconds | 0 to 600 | How long the last seen enemy army position counts for safety. |
| `depot_ring_min_tiles` | 3 | tiles | 1 to 10 | Innermost Chebyshev ring around the target field searched for a depot site. |
| `depot_ring_max_tiles` | 6 | tiles | depot_ring_min_tiles to 20 | Outermost ring. |
| `rebalance_workers_per_think` | 2 | workers | 0 to 20 | Most workers moved off an over-full field per think. |
| `relocate_when_home_remaining_fraction` | 0.1 | fraction | 0 to 1 | HQ may relocate once its home fields hold this share or less of their start amount. |
| `relocate_stand_off_tiles` | 3 | tiles | 0.5 to 32 | Where the HQ stops: this far from the target field centre toward its old position (capped at half the root distance). |
| `relocate_timeout_seconds` | 120 | seconds | 10 to 600 | A relocation not rooted after this re-targets from where the HQ stands. |
| `relocate_cooldown_seconds` | 60 | seconds | 0 to 600 | No new relocation this soon after rooting. |
| `all_in_when_mined_out` (meaning changed) | true | boolean | | All in once no field on the map has known resources. |

Per faction plan, new **optional** key `drop_off_building` (`"skai.perch_depot"`, `"water.pearl_depot"`): must exist, belong to the faction, have footprint 1 and `is_drop_off` true (errors name file and field like the other checks). Absent: no depots (the AI still relocates). Optional so the in-code test faction (`TestWorld.Catalog`) needs no depot and its hashes stay put.

`AiProfile.Parse` adds the keys to `RejectUnknownKeys` and reads them with the `Require*` helpers; the `AiProfile.cs` header's "Differences" list is updated (expansion keys now exist).

### B.2 View and memory additions

- `AiFieldView.StartAmount` (int, resource units, 1 to 1000000): from the map definition.
- `AiMemory.KnownFieldRemaining` (`int[]`, by field index; resource units, 0 to start amount): set to every field's `StartAmount` on the first think; each think, for every field whose tile `IsVisible`, set to its live `Remaining`. Every AI rule reads amounts from here, never from `AiFieldView.Remaining` directly ("known remaining"). "Live" = known remaining above 0.
- `AiMemory.DepotsPlaced` (int, 0 upward): depot build intents emitted.
- `AiMemory.UnbuildableFields` (`SortedSet<int>`): fields with no depot site found; skipped as depot targets.
- `AiMemory` relocation state: `RelocationActive` (bool), `RelocationField` (int field index), `RelocationDestination` (FixVec2, tiles), `RelocationStartTick` (int), `RelocationCooldownUntilTick` (int), `Relocations` (int, stats).
- `KnownStructure.HealthFraction` (Fix64, 0 to 1): health / max health when last seen (used by C).

### B.3 Definitions (computed once per think in `Turn`)

- **Drop-off**: an own complete unit with `Economy.IsDropOff`, or the own HQ while Rooted. **Planned drop-off**: a drop-off, or an own `IsDropOff` structure under construction.
- **Served field**: a live field whose centre is within `field_max_distance_tiles` of a planned drop-off. Replaces `_fieldsInRange`.
- **Live depot**: an own `IsDropOff` structure under construction, or complete with at least one live field within `field_max_distance_tiles`.
- **Home fields**: all fields (any amount) within `field_max_distance_tiles` of the HQ position. Home fraction = sum of known remaining / sum of `StartAmount` (0 when there are none).
- **Income**: true when at least one own worker has `GatherState` other than `Idle`.
- **Unsafe point**: within `expand_safe_radius_tiles` of a `KnownEnemyStructures` entry, of an enemy spawn not in `ClearedSpawns` (not the own spawn), or of `LastSeenEnemyPosition` when `HasLastSeenEnemy` and `LastSeenEnemyTick >= tick - SecondsToTicks(enemy_army_memory_seconds)`.
- **Starved**: no served field exists.
- **Target order** (used by E and H): candidate fields sorted by (safe before unsafe, squared distance from the HQ position, field index). Unsafe candidates are allowed only while Starved. Candidates first need known remaining `>= expand_min_field_remaining`; if none qualify, any live field qualifies.

### B.4 Rule order per think (replaces the header list of `AiBrain.cs`)

0 Intel (as now, plus field memory and `HealthFraction`) → 1 H: HQ root and relocation → 1b Cycle (unchanged) → 2 G: gather, rebalance, long haul, follow the HQ → 3 Structures (supply and build order, as now; reservations per rule R) → 4 E: expand (depot) → 5 W: train workers → 6 P: production (C.2) → 7 X: extra producer (C.2) → 8 Army (C.2). Rule A (all in) is a property read by the army rules.

### B.5 Rules

**R (reservations).** A due structure, depot or production head that cannot be paid reserves its cost only while Income is true. Without income nothing is reserved, so the bank is spent on whatever it can buy.

**G (gather).** Runs whenever the player has a drop-off (not only with a Rooted HQ). Per-field caps: `workers_per_field` for common, `rare_workers_per_field` for rare. Assigned counts come from workers' `GatherFieldIndex`.
1. Each idle worker (ascending ID; idle = `GatherState.Idle`, no move target, not attack-moving) gets `Gather` to the nearest served field under its cap (squared distance from the worker; ties lower index), else the nearest served field.
2. Long haul: if Starved and a drop-off exists, the idle worker instead gets `Gather` to the first live field in target order (under cap first, then any). Income at any distance beats none.
3. Follow the HQ: if no drop-off exists and `RelocationActive`, the idle worker gets `MoveUnit` to `RelocationDestination`.
4. Rebalance: then, for each served field in index order with more assigned workers than its cap, the highest-ID excess workers get `Gather` to the nearest served field under its cap, at most `rebalance_workers_per_think` per think in total. This is how workers move between drop-offs.

**W (train workers).** At the Rooted HQ, one at a time (as now): target = `min(max_workers, workers_per_field x served common fields + rare_workers_per_field x served rare fields)`; while any field on the map is live, the target is at least `workers_per_field`. Workers ignore reservations (unchanged).

**E (expand: depot).** All must hold: the plan has `drop_off_building`; no Threat (rule 1 of the army); live depots `< max_live_depots`; no own depot under construction; and either `tick >= SecondsToTicks(expand_at_seconds + DepotsPlaced x expand_interval_seconds)`, or the served remaining fraction (sum of known remaining of served fields / sum of their start amounts; 0 when Starved) `< expand_when_served_remaining_fraction`.
- Target: the first field in target order that is not served, not in `UnbuildableFields`.
- Site: tiles on Chebyshev rings `depot_ring_min_tiles` to `depot_ring_max_tiles` around the target's tile, scanned ring by ring, dy ascending, then dx ascending (the existing scan), that pass `IsGoodSite` with the target tile's height as the base height. Score = sum of squared distances from the site tile centre to every live field whose centre is within `field_max_distance_tiles` of the target's centre (target included). Lowest score wins; a later tile must score strictly lower to replace an earlier one. No site: add the target to `UnbuildableFields` and stop for this think. (Expected sites: A.3 table.)
- Build through the existing `TryBuild` path (retry time, supply, payment, rule R, `PickBuilder`), generalised to take the site. On emit: `DepotsPlaced++`.

**H (HQ root and relocation).** Let the destination for a field f be `Standable(f.Centre.MoveTowards(hq.Position, min(relocate_stand_off_tiles, HqRootMaxDistanceTiles / 2)))`.
1. Root (existing rule 1, now on known amounts): Uprooted, no move target, a live field within `HqRootMaxDistanceTiles` → `RootHq`. When the HQ is Rooted: clear `RelocationActive`, set `RelocationCooldownUntilTick = tick + SecondsToTicks(relocate_cooldown_seconds)`, `Relocations++` if a relocation was active.
2. Trigger (HQ Rooted, not `RelocationActive`, no Threat, `tick >= RelocationCooldownUntilTick`): home fraction `<= relocate_when_home_remaining_fraction`, and no cheaper option: live depots `>= max_live_depots`, or the plan has no `drop_off_building`, or (no Income and common `<` the depot's cost). Target: the first field in target order that is not within `field_max_distance_tiles` of an own depot and not a home field. If one exists: `UprootHq`, set `RelocationActive`, `RelocationField`, `RelocationDestination`, `RelocationStartTick`.
3. Move: Uprooted, `RelocationActive`, no move target and not able to root (step 1) → `MoveUnit` to `RelocationDestination`.
4. Re-target: while `RelocationActive`, if the target field's known remaining is 0, or its destination is now unsafe and a safe candidate exists, or `tick >= RelocationStartTick + SecondsToTicks(relocate_timeout_seconds)`: choose again by target order measured from the HQ's current position (restart the timer). No candidate: keep moving to the old destination; step 1 roots wherever a live field is in reach.
5. An Uprooted HQ with no relocation active (for example after a script) starts one at step 4's choice.
- Escort: none. The army's home anchor moves with the relocation (C.2 rule M), and Defend already covers anything near the HQ, so the massing army walks to the new base and guards it.
- While uprooted: production structures keep training (they do not need the HQ); the HQ trains no workers (the simulation refuses); depots keep their workers; HQ-only workers follow it (rule G3) and gather once it roots.

**A (all in).** `AllIn` = `all_in_when_mined_out` and no field on the map is live (known amounts).

### B.6 Anti-freeze rules (one per frozen state the review found)

| Frozen state in the review | Rule that prevents it |
| --- | --- |
| All 9 capped games: home fields `C0`, 6 idle workers, bank 0 to 50, no army, `cmds=-` for 30+ minutes | E builds a depot at the natural before the main is empty (by 240 s or at 50 percent left); G sends idle workers to served fields; G2 long-hauls to any live field when nothing is served. |
| Same, with bank under the depot's cost and no income | R drops reservations without income; H2's "no income and common < depot cost" relocates the HQ, which costs nothing; G3 brings the workers along. |
| Workers idle while far fields (rare at 23 to 45 tiles) still hold resources | G1 works every served field, not only those near the HQ; G2 works unserved fields when starved. |
| Worker count stuck at 6 of `max_workers` 8 | W's target grows with served fields; `max_workers` 24. |
| Army gone after an attack, never re-formed (`Attack -> Massing`, no income) | Income continues through E, H and G; C.2 rules P and G rebuild a group at home. |
| Enemy HQ left at 81 to 324 health, or a Water HQ healed from 190 to 1800 | C.2 rule K attacks a known low HQ with a small army. |
| Money but nothing to buy (not seen yet, possible once income lasts) | C.2 rule P skips units whose rare cost cannot be paid; rule X adds producers when every producer is busy and the bank is high; R never holds money without income. |
| HQ uprooted far from fields (new risk from H) | H4 re-targets on depletion, danger and timeout; H1 roots at any live field in reach. |
| `all_in_when_mined_out` firing at home while depots still pay (new risk) | Rule A uses the whole map. |

### B.7 Tests (package B, `rts/tests/Sim.Tests/Ai/AiEconomyTests.cs`, new; hand-built views as in the existing AI tests)

- **B-1 Field memory.** First think: known = start amounts. A visible field updates to its live amount; a field whose tile is not visible keeps its last known value even after its live amount changes.
- **B-2 Served.** A field 8 tiles from a complete depot is served; 8 tiles from a depot under construction is served; 4 tiles from an Uprooted HQ and 30 from any drop-off is not; a field with known remaining 0 is not.
- **B-3 Worker target.** 2 served common + 1 served rare: target `min(24, 3 x 2 + 2 x 1)` = 8. No served field but a live field elsewhere: target 3.
- **B-4 Expand by time (greybox_1v1, P0).** Tick of 240 s, no threat, no depot, bank 200, one gathering worker: one `BuildStructure(skai.perch_depot, 21, 7)` by the lowest-ID gathering worker; `DepotsPlaced` 1. P1 mirror: tile (42, 56).
- **B-5 Expand by fraction.** At 100 s with served fields at 40 percent: a depot intent. At 100 s with 60 percent: none.
- **B-6 Expand limits.** No depot under a Threat; none when live depots = `max_live_depots`; a complete depot whose fields are all known empty is not live, so a new depot is allowed; none while a depot is under construction.
- **B-7 Safety.** Nearest unserved field within 14 tiles of a known enemy structure: the next safe field is chosen. Only unsafe candidates and served fields still live: nothing. Only unsafe candidates and Starved: the nearest unsafe one.
- **B-8 No-income reservation.** Bank 80, depot due (cost 100), no gathering worker, an idle Roost: the Roost trains a Windrunner (75); with one gathering worker instead, nothing is trained (100 reserved).
- **B-9 Rebalance.** Field A holds 5 workers (cap 3), served field B holds 0: the two highest-ID workers of A get `Gather` to B; with `rebalance_workers_per_think` 1, only the highest-ID one.
- **B-10 Rare.** Served common fields full, served rare field with 0 workers, two idle workers: both go to the rare field; a third idle worker goes to the nearest served field (cap exceeded fallback).
- **B-11 Long haul.** Starved, HQ Rooted, a live field 30 tiles away: idle worker gets `Gather` to it.
- **B-12 Relocation trigger.** Home fraction 0.05, live depots 2 = max, an unserved safe field 20 tiles away: `UprootHq`; memory holds that field and destination = field centre moved 3 tiles toward the HQ (made standable). Next think, HQ Uprooted and idle: `MoveUnit` to the destination.
- **B-13 Rescue relocation.** Home fraction 0, no depot, bank 40, no income, `max_live_depots` 2: `UprootHq` (not a depot).
- **B-14 Depot before relocation.** Home fraction 0, live depots 0 of 2, bank 200, income: a depot intent and no `UprootHq`.
- **B-15 Root and cooldown.** Uprooted, idle, 3 tiles from a live field: `RootHq`; once Rooted, relocation cleared and `Relocations` 1; a new trigger 30 s later does nothing, 61 s later it can fire.
- **B-16 Re-target on depletion.** Target field's known remaining becomes 0 during the move: `MoveUnit` to the next field's destination.
- **B-17 Timeout.** 121 s after the uproot, not rooted: re-target from the current position.
- **B-18 Follow.** Relocating, no drop-off: idle worker gets `MoveUnit` to the destination. With a complete depot serving a live field: `Gather` to it instead.
- **B-19 All in.** Home mined out, depot-served field live: threshold is the drawn one. Every field known empty: threshold 1, tide ignored, no retreat.
- **B-20 Determinism.** Same view, profile, memory copy and seed twice: identical intent lists; `rng.DrawCount` after a think with only economy decisions is unchanged.
- **B-21 Profile validation.** Missing `rare_workers_per_field`: `ai/aggressive.json: field 'rare_workers_per_field' is missing`; `drop_off_building` naming `skai.roost`: error naming `factions.skai.drop_off_building` and "is_drop_off".

## Package C: AI army and production

### C.1 New profile keys (root)

| Key | Value | Unit | Range | Meaning |
| --- | --- | --- | --- | --- |
| `defend_hold_seconds` | 5 | seconds | 0 to 60 | Stay in Defend this long after the last threat before massing again. |
| `kill_hq_health_fraction` | 0.35 | fraction | 0 to 1 | A known enemy HQ last seen at or below this share of its health triggers rule K. |
| `kill_min_army_supply` | 4 | supply | 1 to 200 | Army supply rule K needs. |
| `extra_producer_bank_common` | 400 | resource units | 0 to 100000 | Bank (after this think's spending and reservations) at which rule X adds a producer. |
| `max_producers` | 8 | structures | 1 to 30 | Most own structures that train a composition unit (complete or under construction). |
| `rally_at_core` (meaning changed) | aggressive true, defensive false | boolean | | When true and the own team does not hold the Core, the attack group stages at the Core centre. |

### C.2 Rules

**P (production; replaces rule 5).** Step 1, required before any fix: write test C-1 and run it against the current code. If it fails as predicted, the cause is confirmed; record that in the commit message. If it passes, stop and report to the coordinator with the trace numbers instead of changing the loop. Step 2, the loop:
1. Counts per composition unit as now (alive plus queued).
2. Repeat: candidates are composition units with weight above 0 for which some own complete producer with an empty queue, not used this think, lists the unit in `Trains`, and whose `cost_rare` is 0 or at most the rare stock minus rare reservations. Pick u* with `PickByDeficit` over the candidates. None: stop. The producer is the lowest-ID such structure. If affordable after reservations and supply is free: emit `TrainUnit`, `Pay`, count u* + 1, mark the producer used, repeat. Otherwise reserve u*'s cost under rule R and stop: cheaper units never jump ahead of the most-needed one.
Because the deficits sum to W above 0, some unit always has a positive deficit, so the head is always a unit the mix needs.

**X (extra producer).** After P, when no Threat, common minus reservations `>= extra_producer_bank_common`, every own complete structure that trains a composition unit has a non-empty queue or was used this think, none is under construction, and their count `< max_producers`: build the structure that trains the composition unit with the largest deficit over all composition units (the first such structure in the plan's `build_order`, else the ordinally lowest structure ID of the faction whose `Trains` lists it), through `TryBuild` at the HQ ring sites.

**M (home rally; replaces the Core rally).** Home = `RelocationDestination` while `RelocationActive`, else the HQ position. Rally point = `Standable(Home.MoveTowards(target, rally_distance_tiles))` with target from `TryChooseTarget`, else `Standable(Home)`. Massing army units not in the attack group, idle and more than 3 tiles from it, get `AttackMove` there (the existing check).

**G (attack group).** `AiMemory.AttackGroup` (`SortedSet<EntityId>`). On Massing to Attack: the group is every current army unit; `AttackStartSupply` = their supply. In Attack only living group members are ordered (target, staging, siege, focus as now); army units outside the group follow rule M. Retreat and power tests use the group's units and supply. The group empty: Massing with a new threshold. With AllIn or rule K, every army unit joins the group each think. Staging point: the Core centre when `rally_at_core`, the map has a Core and the own team does not hold it; otherwise as now (stage_distance_tiles short of the target).

**D (Defend hysteresis; damps the flip-flop).** `AiMemory.LastThreatTick` (ticks). A think that sees a threat sets it and orders Defend as now. A think in Defend with no threat: if `tick < LastThreatTick + SecondsToTicks(defend_hold_seconds)` stay in Defend and order nothing; else go to Massing **without** redrawing the threshold. The threshold is drawn on the first think and when an attack ends (group empty, retreat, or no target left), never on leaving Defend.

**K (finish a low HQ).** Kill target = the known enemy HQ with `HealthFraction <= kill_hq_health_fraction` (lowest fraction, ties nearer to the own HQ, then lower ID). While one exists: Massing attacks once army supply `>= kill_min_army_supply`, ignoring the threshold, the tide window and the regroup timer; `TryChooseTarget` returns the kill target first; the attack skips staging (`AttackStaged` true) and never retreats.

Rebuild after a lost attack follows from P (production keeps buying the mix), M (new units gather at home, not one by one at the target) and G (the next group forms at the next threshold).

### C.3 Tests (package C, `rts/tests/Sim.Tests/Ai/AiArmyTests.cs`, new)

- **C-1 Composition cause (write first; must fail on today's code).** Skai view: Roosts E10, E11, E12 and Windspire E13, all complete and idle; 6 Windrunners, 0 Talon Archers; bank 175, supply free, one gathering worker. Expected, in order: `TrainUnit(E13, skai.talon_archer)` (deficits: archer `1 x 7 - 0 x 4 = 7`, windrunner `3 x 7 - 6 x 4 = -3`), then `TrainUnit(E10, skai.windrunner)` with the 75 left (now archer 4, but its only producer is used; windrunner `3 x 8 - 6 x 4 = 0`). Today's loop trains Windrunners at E10 and E11 and no archer.
- **C-2 Head reservation.** Same view, bank 90: nothing trained, 100 reserved. Bank 100: the archer.
- **C-3 Rare candidate.** Composition with `skai.roc_rider` weight 1 holding the largest deficit, rare stock 0: Roc Rider is skipped and the next unit is trained; rare 50: Roc Rider is trained.
- **C-4 Water mix.** 3 Tidepools and a Shell Forge, 6 Graspers, 0 Shellbacks, bank 200: one Shellback at the Forge.
- **C-5 Home rally.** `rally_at_core` true, Core not held, Massing: an idle army unit attack-moves to the home rally point, not the Core centre.
- **C-6 Attack group.** Attack starts with units E20 and E21; E22 trained next think: E22 attack-moves to the home rally point, not the target; the group stays {E20, E21}.
- **C-7 Staging at the Core.** `rally_at_core` true, Core not held: staging point is the Core centre; team holds the Core: staging point is 14 tiles short of the target toward home.
- **C-8 Flip-flop damping.** A threat appears (Defend), disappears: Defend and no orders for 5 s; after 5 s: Massing with the same `AttackThreshold` and `rng.DrawCount` unchanged.
- **C-9 Kill mode.** Known enemy HQ last seen at 30 percent health, army 4 supply, threshold 8: Attack targeting that HQ; group losses beyond `retreat_army_fraction` give no retreat.
- **C-10 Rebuild.** Group all dead: Massing, exactly one new draw, and that think still emits `TrainUnit` for an idle producer; the new unit next think goes to the home rally.
- **C-11 Extra producer.** Bank 450, every producer busy, 4 producers: `BuildStructure` of the producer of the top-deficit unit. With one under construction, or 8 producers: none.
- **C-12 Unchanged.** The existing `Composition_TrainsTheLargestDeficit` (`PickByDeficit`) passes unchanged.

## Package D: acceptance

### D.1 Tooling (rts/tools/BalanceHarness)

- Extend the `--trace` summary per player with: `stall_max_seconds`, `unspent_max_seconds`, `depots_placed`, `relocations`, `first_depot_tick`, `first_rare_tick`, `max_stance_changes_in_window` (metrics below).
- New mode `--ai-check`: plays the 18 review scenarios (maps from `harness_batch.json` `maps`, matchups from `matchups`, seeds from `ai_check_seeds`, game index 0, profile `ai_profile`, cap `ai_check_max_game_seconds`) through `GameRunner.Play` with a read-only observer (same contract as `GameTrace`: never submits or steps; test that the final hash equals an unobserved run). Prints one line per player-game with the metrics, then one PASS/FAIL line per criterion. Re-runs the first scenario of each map and compares final hashes. Exit codes as the batch: 0 pass, 1 error, 2 a criterion failed, 3 hash mismatch.
- Metrics, sampled every `ai_check_sample_seconds`:
  - **stalled** sample: the player is Active, has an own worker with `GatherState.Idle` and no move target, deposited nothing in the last `ai_check_income_window_seconds`, has no army unit (mobile, not worker, not HQ), and some field on the map has `Remaining > 0`. `stall_max_seconds` = longest run of consecutive stalled samples x sample seconds.
  - **unspent** sample: common `>= ai_check_unspent_common_min`, `SupplyUsed + 3 <= SupplyCap`, and no accepted `train_unit` or `build_structure` by the player in the last window. `unspent_max_seconds` likewise.
  - Expansion: an accepted `build_structure` of an `is_drop_off` definition or an accepted `uproot_hq`.
  - Rare: first `ResourceDepositedEvent` of kind rare.
  - Stance changes: from `AiMemory` stance per think (as the trace already records).

New keys in `rts/data/tuning/harness_batch.json` (read by the harness only, outside the content digest):

| Key | Value | Unit | Range |
| --- | --- | --- | --- |
| `ai_check_seeds` | [20261026, 7] | seeds | 1 to 16 entries, 0 to 2147483647 |
| `ai_check_max_game_seconds` | 2400 | seconds | 300 to 7200 |
| `ai_check_sample_seconds` | 1 | seconds | 1 to 30 |
| `ai_check_income_window_seconds` | 10 | seconds | 1 to 120 |
| `ai_check_stall_max_seconds` | 90 | seconds | 0 to 7200 |
| `ai_check_unspent_common_min` | 400 | resource units | 0 to 100000 |
| `ai_check_unspent_max_seconds` | 60 | seconds | 0 to 7200 |
| `ai_check_decided_fraction_min` | 0.8 | fraction | 0 to 1 |
| `ai_check_expand_by_seconds` | 360 | seconds | 0 to 7200 |
| `ai_check_rare_by_seconds` | 720 | seconds | 0 to 7200 |
| `ai_check_composition_min_share` | 0.5 | fraction of intended share | 0 to 1 |
| `ai_check_composition_min_units` | 8 | units | 1 to 1000 |
| `ai_check_stance_window_seconds` | 60 | seconds | 1 to 600 |
| `ai_check_stance_changes_max` | 4 | changes | 1 to 100 |
| `ai_check_rejected_fraction_max` | 0.01 | fraction | 0 to 1 |

### D.2 Exit criteria (all must pass on `--ai-check`), with the reason for each number

1. **No freeze.** Every player-game: `stall_max_seconds <= 90`. Review: stalls of 1950 to 2130 s in all 9 capped games. A legitimate relocation without depots on these maps takes at most 5 s uproot + about 45 tiles at 0.85 tiles/s (Grand Shell on dry ground) = 53 s + 10 s root, 68 s; 90 s leaves margin.
2. **No idle money.** Every player-game: `unspent_max_seconds <= 60`. New income must be spent; 60 s is more than two production cycles (`build_seconds` 25 for a structure).
3. **Games end.** At least 80 percent (15 of 18) are decided before 2400 s. Review: 9 of 18; all 9 capped games were economy freezes (section 4), which B removes; 3 games of slack because combat balance is on hold (Q6).
4. **Expansion happens.** Every player still in the match at 360 s placed a depot or uprooted the HQ by then. With 6000 in the main and the review's income of 8.6 to 13 per second, the main is at 50 percent between 230 and 350 s and `expand_at_seconds` is 240; 360 s allows for saving 100.
5. **Rare is gathered.** Every player still in the match at 720 s has deposited rare. The second depot is due at 240 + 150 = 390 s; 720 s allows its build, worker transfer and travel.
6. **Mix reached.** Every player-game with at least 8 composition units trained: each composition unit's trained count `>= floor(0.5 x weight / total weight x composition units trained)`. Review: Shellback 0 to 4 percent against 25 percent intended.
7. **No flip-flop.** No player has more than 4 stance changes in any 60 s window. Review: 7 in 69 s. Massing, Attack, Retreat, Massing, Defend is 4 changes.
8. **Clean commands.** Rejected AI commands `<= 1` percent of AI commands per player-game. Review: 0; a depot placed into fog may rarely hit an unseen unit (`tile_occupied`).
9. **Deterministic.** Re-run hashes equal.

Also run `--batch --ci` and report each cell's undecided fraction next to the review's 27 of 54; it is information, not a gate (its existing 0.1 warning stays a warning).

### D.3 Integration tests (NUnit, `rts/tests/Sim.Tests/Ai/AiExpansionRunTests.cs`, new)

Profile: shipped `aggressive` edited with `"attack_army_supply": 200` (the AI never attacks, so the economy is observed). Opponent: a passive player with its standard start (HQ and workers) and no controller. 24 000 ticks (1200 s) each.

- **D-1 greybox_1v1, Skai AI in slot 0.** Depot placed by 360 s at tile (21, 7); at least one completed relocation (accepted `uproot_hq` then `root_hq` and HQ Rooted) by 1200 s; rare deposited by 1200 s; no rejected AI command; the stall predicate never holds for more than 90 s in a row.
- **D-2 long_lowland, Water AI in slot 1.** Same assertions; depot tile (55, 16).
- **D-3 twin_chokes, Skai AI in slot 0.** Same assertions; depot tile (21, 55).
- **D-4 Determinism.** D-1 twice: identical final hash.
- **D-5 Observer is read-only.** `--ai-check` observer on one scenario: final hash equals the unobserved game.
- **D-6 Metric arithmetic.** Synthetic sample series: stalled runs of 3, 0, 5 samples at 1 s give 5 s; a stance series with 5 changes in 50 s fails criterion 7; a record with 2 of 100 commands rejected fails criterion 8 and the exit code is 2.

### D.4 Existing tests that change

- `TerrainAndPathingTests.GreyboxMap_LoadsLayersRampsFieldsAndCore_AndRunsDeterministically`: `ResourceFields.Count` 6 becomes 12.
- `SkirmishAiTests.ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided`: may no longer decide within 6000 ticks (bigger economy, grouped attacks). If so, raise `ticks` in `rts/data/scripts/ai_vs_ai.json` to 24000 (comment updated, still placeholder) and change the `6000` literals in the test to read `script.Ticks`; keep every other assertion. Report it.
- `CycleAiTests.RallyAtCore_MassingArmyGathersAtTheCoreUntilTheTeamHoldsIt`: rewritten to C-5 and C-7 behaviour (name it `RallyAtCore_MassesAtHomeAndStagesAtTheCore`).
- `CycleAiTests.AiVersusAi_WithCyclesOn_IsDeterministicAndShowsACycleShift`: keep; if the cycle-shift assertion fails because no team holds the Core, report it rather than weakening it.
- `SkirmishAiTests` attack, retreat and defend tests: adjust only where the group (G) and the Defend hold (D) change what is expected; each adjustment is listed in the builder's report.
- `HarnessTraceTests`: update any assertion that reads the summary layout.
- **Hashes.** `DeterminismTests.GoldenFinalHash` uses `TestWorld` only and must **not** change; if it does, that is a bug. Every `STATE_HASH` of a script or batch on shipped data changes (map rows, field amounts and two new unit files enter the content digest). CI compares two runs, so it stays green; replays recorded before this change no longer re-simulate; the PC session must repeat the Unity-versus-headless hash check (`UNITY_SETUP.md`). The 16 Nov trace files stay as they are (historical evidence).

## Out of scope

- Combat and unit tuning (developer decision 3), Last Stand, the water HQ heal, the spawn-side bias in the water mirror.
- Simulation changes: builders still do not walk to the site; any player can place a depot anywhere on the map, and so can the AI.
- Ground reachability checks inside the AI (the maps are fully connected; test A-2 guards it).
- Scouting, difficulty levels, team play beyond what already works, FFA reveal handling.
- Unity presentation of the two depots (PC session: a placeholder model per building ID).
- `skirmish_ai.md` stays as written; this spec supersedes its sections 2 (expansion keys) and 4 (rules 2, 4, 5, 6, 7 and 8) where they differ.

## Questions for the developer

- **Q1. Rare field amounts.** Leave rare at 600, or double to 1200 like common? Recommended default: leave at 600 until the AI spends rare (Q4), then decide from traces.
- **Q2. How many depots at once?** `max_live_depots` 2 means the HQ relocates only when the main is nearly empty and both depots are busy. Recommended default: 2, which makes both expansion modes appear in games longer than about 8 minutes.
- **Q3. Expansion field amounts.** The new natural and flank fields use 3000 like the doubled mains. Recommended default: 3000.
- **Q4. Rare units in the AI mix.** Add one rare-cost unit per faction to the composition (for example `skai.roc_rider: 1`, `water.ink_lobber: 1`) so rare is spent? Recommended default: add them; rule P only buys them while rare is in the bank, so they cannot block production.
- **Q5. Core priority.** `rally_at_core` now makes the attack group stage at the Core instead of single units rallying there. Should the AI also hold the Core for the Core win? Recommended default: no, staging only, until the AI plays whole games.
- **Q6. If only criterion 3 fails.** If every freeze criterion passes but fewer than 15 of 18 games are decided, is that a combat-balance finding (on hold) rather than an AI failure? Recommended default: yes; the builder reports the capped games' final states and does not tune combat.
- **Q7. Depot names and stats.** "Perch Depot" and "Pearl Depot" with cost 100, 20 s, 350 and 400 health. Recommended default: keep as placeholders.

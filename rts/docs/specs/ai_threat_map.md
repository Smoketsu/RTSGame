<!-- rts/docs/specs/ai_threat_map.md -->
<!-- Build spec for P-006 package S (one threat map): a coarse grid in the skirmish AI's memory holding its own strength and its remembered enemy strength, each fading with distance, built only from the AI's fogged view and memory, plus the first two rules that read it: rule TS (is a point safe: replaces IsUnsafe at every call site, so expansion choice, the long haul and HQ relocation read the map) and rule TH (harass target and route). Three profile switches (use_threat_map, use_threat_safety, use_threat_harass), off in every existing profile and on only in the new candidate aggressive_s5_s (a copy of aggressive_s5). A harness package adds the depot-life and harass metrics and a paired sign test with a stated power for behaviour checks. Behaviour bars on prototype_1v1 against aggressive_s5 are fixed here before any run; no bar is a win rate. Raid response, producer placement, the tower base and HQ moves are later packages (section 11). No rts/src/Sim change, no unit, combat, economy or map number change, no pin moves. Part of rts/docs/specs (index: README.md); built by Sonnet builders, the sim-reviewer after each AI package. -->
# Spec: one threat map (P-006 package S)

Written 23 Nov 2026 against `main` at `67bf368`. Every new number is a **placeholder** unless it is read from existing game or profile data. File and line references are to `67bf368`.

## 0. Goal, sources, ground rules

**Goal.** Today every spatial decision of the AI has its own test of "where the enemy is", and each reads a different, thin piece of memory: `IsUnsafe` (`AiBrain.cs:2073-2096`) treats a point as unsafe when it is within `expand_safe_radius_tiles` (14) of any known enemy structure (armed or not), an uncleared enemy spawn, or **one** remembered point, the enemy army unit nearest the own HQ at the last think any was seen (`UpdateIntel`, `AiBrain.cs:727-765`), kept for `enemy_army_memory_seconds` (60). One scout seen near a natural makes it unsafe for a minute and also erases what was known about the main army elsewhere. Rule HR picks the known enemy drop-off farthest from that same single point (`AiBrainFight.cs:452-468`) and never asks whether the target or the way there is defended beyond the static defences at the target. P-006 section 4 S asks for one map of own and remembered enemy strength that every spatial decision reads.

**What this spec builds:** the map (section 3) and two consumers, chosen as the smallest and most measurable of the live rules (section 1): rule TS (safety, section 4) and rule TH (harass target and route, section 5). Everything else P-006 lists for S is a later package (section 11).

**Sources read.** `CLAUDE.md`; `docs/STATUS.md` ("AI work resumed" paragraph); `proposals/P-006-ai-built-for-balancing.md` in full; `specs/ai_production_defence.md` (sections 0 to 11 and R2 in full); `reports/ai_pd/README.md`; `reports/ai_pd/r2/README.md`; `specs/ai_economy_calc.md`; `reports/ai_econ/README.md`; `specs/balance_measurement.md` M.1 and M.2. Code: `src/Ai/AiBrain.cs` (header, `Decide`, `Probe`, `UpdateIntel`, `Hq`, `TriggerRelocation`, `RelocationCandidates`, `Retarget`, `Gather`, `TryFindSite`, `IsGoodSite`, `IsUnsafe`, `TargetOrder`, `DropOffs`, `UpdateRaids`, `TryFindDefenceSite`, `BuildDefence`, `StaticDefence`, `Expand`, `WantedWorkersUncapped`), `AiBrainPlacement.cs` (whole file), `AiBrainFight.cs` (`EnemyArmyPosition`, `Harass`, `StartSquad`, `RunSquad`), `AiMemory.cs`, `AiProximity.cs`, `AiCounter.cs` (`Value`), `AiIntel.cs` (`MayHarass`), `SkirmishAiController.cs`; harness `BehaviourRunner.cs`, `BehaviourChecks.cs` (`hq_move_new_income`), `PairedStats.cs`, `SummaryObserver.cs`, `ProductionMetrics.cs`, `GameSummary.cs`, `GameTrace.cs`; data `ai/aggressive_s5.json`, `scripts/ai_eval/ai_eval.json`, `scripts/ai_eval/behaviour_checks.json`, `maps/prototype_1v1.json` (96 by 96 tiles), every `units/*/*.json` (costs, speeds, layers).

**Ground rules (every package).**
- **No `rts/src/Sim` change.** Every Sim read is public. If a builder finds a Sim change unavoidable, stop and report.
- **No pin moves, no re-pinning.** With the three switches false every existing profile plays bit for bit as on `67bf368`. Pins: smoke `207be73eaa460b3c` and economy_smoke `98b2e00d9babe167` (`Harness/HarnessUnitTests.cs`); `aggressive_s5` trace end tick 9505 hash `837f8e7118a9c9de` and `aggressive_s3` trace end tick 9223 hash `c04bdcc0d03b3910` (`Ai/FightSimTests.cs`, `OldProfiles_FixedSeedGamesKeepTheirHash`); `V2ReferenceHash` and `BlindTraceHash` (`Ai/SkirmishAiTests.cs`). If one moves, stop and report. If `main` has re-pinned any before a package starts, use the values on `main` then.
- **No unit, combat, economy, tuning or map number changes** (P-005 freeze). Walls stay as built: rule WL places them from the authored lines in `rts/data/ai/walls`, and its own "skip an area with a visible enemy army near it" test is not changed.
- **Fog.** The map is built only from `AiWorldView` (own units, `VisibleEnemies`, tile visibility) and AI memory (`KnownEnemyStructures`, the new sightings). Nothing reads hidden enemy units, enemy stock or queues.
- **Determinism (AI).** Integers and `Fix64` only; products that can exceed 2^31 in `long`; own units, enemies, sightings and known structures walked in ascending entity ID, cells in ascending index (row x columns + column); every tie stated. **No new random draw**: `AiVariety` and the attack-threshold generator are untouched. AI memory is not hashed (unchanged).
- **Faction-neutral.** Strength comes from unit definitions and `AiCounter.Value`; the brain never names a faction or unit.
- **Commands and events.** None new. TS changes which field or site existing rules pick; TH changes which target an existing `attack_move` names, or whether a squad starts.
- **Parsing.** `AiProfile.Parse` reads every new key with the `Require*` helpers and adds it to `RejectUnknownKeys`; every profile file under `rts/data/ai` and every hand-written test profile gets every new key (switches false). Tests that loop over shipped profiles and list the known non-old ones (`DProfiles_SwitchesAndValidation`, `L1_ShippedProfilesParse`, `R6_NoVarietyForOldProfiles`, and any like them) get `aggressive_s5_s` added.
- **Other packages stay off in the candidate.** Budget queues (B), economy by calculation (E), production capacity, supply ahead and the raid rules are off in `aggressive_s5` and so in `aggressive_s5_s`. S is not tested together with any of them (out of scope).

## 1. What S replaces, leaves in place and deletes

| Rule P-006 names | Where | In `aggressive_s5` | Under this spec |
| --- | --- | --- | --- |
| `IsUnsafe` | `AiBrain.cs:2073-2096`; callers `TargetOrder` (`2130`, read by rule E `2928`, `2959`, by `RelocationCandidates` `1529`, by the worker-target floor `3135`), the long haul (`1652`), `Retarget` (`1593`, `1608`) | live | **replaced by rule TS** (section 4) while `use_threat_safety` is on: every caller reads the map through it. Expansion choice, the long haul, the worker-target floor and HQ relocation destinations all change through this one function. |
| Harass target (rule HR) | `AiBrainFight.cs:427-495`, target at `452-468` | live (`use_target_choice` true) | **replaced by rule TH** (section 5) while `use_threat_harass` is on: target by the map's strength at the target and on the way, and no squad when every way is stronger than the squad. `RunSquad` is unchanged. |
| Exposed-base rule (SD-4) | `AiBrainPlacement.cs:193-353` (`EnemyPoints`, `FindExposedBase`, `ThreatDefence`) | off (`use_threat_defence` false in every profile but the ablation `abl_s5_p2_only`) | **left in place**, off. Tower base by the map is later package S-D (section 11). |
| Safe-cluster test (AI-HQ) | branch `review/ai-hq`, not on `main` (P-006 decision 2) | absent | nothing to replace. HQ moves with "a move must bring new income" are later package S-H. |
| Producer placement (AI-P P1) | `AiBrainPlacement.cs:169-187` | off | **left in place**, off. Later package S-P. |
| Tower placement (rule SD steps 2 and 3) | site `TryFindDefenceSite` `AiBrain.cs:2709-2758`, called at `2845` (toward the raid position) and `2860` (toward the enemy spawn) | live | **left in place**: the site within a base is a local choice and already faces the raiders. Which base gets a tower is S-D. |
| Raid response (rules RS, RW, RA) | `AiBrainRaids.cs`, `AiBrainFight.cs` `MeasuredDefence` | on only in `aggressive_s5_pd`, paused by the developer (23 Nov) | **not touched**. Section 2 says what S can and cannot do; later package S-R. |

**Deleted:** nothing. `IsUnsafe`'s old body, the old HR target loop, `EnemyPoints` and `FindExposedBase` are the off path of every existing profile, and the pins of `aggressive_s3` and `aggressive_s5` depend on them. If the developer later switches TS or TH on in `aggressive_s5` and retires the older profiles, a cleanup package may delete the old branches; this spec does not.

**Not used by any consumer here, so not built:** a terrain or path-distance layer (consumers use straight lines, as today), a territory layer, a "danger to workers" layer, per-layer (ground, air, water) strength. Section 11 says which later package would need which.

## 2. Raids: what S can and cannot do

The raid rules were measured twice (`reports/ai_pd/README.md` C1, `reports/ai_pd/r2/README.md` C2) and failed their bars both times; the developer paused them on 23 Nov. P-006 puts raid response in S. Plainly:

**What the reports show.** Water's median worker losses against Skai (window 600 s) were 31 in B0, 26 in C1 and 24.5 in C2 against a limit of 18 (PD-B2). About half of them (52 percent in B0, 51 percent in C1, 12-game sample, `ai_production_defence.md` R2.0.2) fall while Skai's main army is inside Water's base; Skai's army reached that overrun in 68 of 72 Water-cross slot-games in B0b and 71 of 72 in C2. Even counting only losses before the overrun, C2 went from 9 to 7.5 against a limit of 5. Raid-phase losses did not fall under revision 1 (median 14 against 14.5, sample). Revision 2 fixed the four rule flaws H1 to H4 directly and the bars still failed.

**What S cannot do.**
- Stop the overrun. Losing workers to a main army inside the base is a lost fight, not a placement or target choice; no map read adds defenders.
- Close the strength gap behind the raids. Water's Tender (range 0.3) cannot answer a Windrunner (range 4, 3.5 tiles per second), and those numbers are frozen.
- Remove the economy cost of evacuating (PD-B6: idle and walking workers, `ai_production_defence.md` R2.0.3). A field closed by a map is as idle as a field closed by a hit.

**What S could do, in a later package (S-R), with no result promised.** The raid memory of rules RS and RW is keyed by hit positions; H1 and H2 were flaws of that memory (closed fields moving with each hit, raids ending while raiders stayed). The map's enemy strength in a field's cell could decide "closed" instead (closed while enemy strength exceeds own strength plus a margin, reopened when the sighting ages out or is seen gone), and rule RA could pick the raid to answer by the strength difference. Revision 2 already fixed H1 and H2 another way and losses did not move enough, so the reports give no reason to expect S-R to pass PD-B2. S-R waits until the developer returns to raid response, and it would need its own bars restated before its run.

**What this spec does to raids.** Nothing on the defending side. Rule TH makes harass squads pick weaker targets, so the candidate may kill more of an opponent's workers: in the paired run that opponent is `aggressive_s5`, and Water's losses to a Skai on TH could rise. That is the "one strong AI" principle at work; the opponent's worker losses are reported, not judged (section 10.5).

## 3. The map (package S-MAP)

### 3.1 Grid

| Quantity | Value | Unit |
| --- | --- | --- |
| cell side `c` | profile key `threat_cell_tiles` (4) | tiles |
| columns, rows | `ceil(map width / c)`, `ceil(map height / c)` (24 by 24 on `prototype_1v1`) | cells |
| cell of a point (x, y) | `(min(cols - 1, max(0, floor(x / c))), min(rows - 1, max(0, floor(y / c))))`, `Fix64` floor | cell |
| cell index | `row x cols + column` | index, 0 to cells - 1 |
| ring distance of two cells | `max(|dx|, |dy|)` (Chebyshev) | cells |

### 3.2 Strength

**Strength of one unit** = `floor(AiCounter.Value(definition, profile) x min(1, health / max health))`: resource units (common plus `counter_rare_cost_weight` x rare), integer, 0 upward. A Windrunner at full health is 75, a Talon Archer 100, a Coral Turret or Perch Tower 100. An HQ has cost 0, so its weapon adds nothing (Q-3).

**Enemy sources** (both read from memory, so they are the AI's knowledge, not the truth):
1. **Sightings** (new memory, section 3.4): enemy units that are army units (`IsArmy`: mobile, not a worker, not an HQ) and have a weapon, as last seen. A sighting's strength is aged: `s_aged = s x (M - a) / M` (integer, `long` product), where `a` = now minus the sighting's tick (ticks) and `M = SecondsToTicks(enemy_army_memory_seconds)` (Q-4: the existing key, 60 s, so the map remembers exactly as long as `IsUnsafe` did).
2. **Armed known structures:** each `KnownEnemyStructures` entry whose definition has a weapon and is not an HQ: strength `floor(Value x HealthFraction)`, not aged (structures do not move; the existing intel rule already forgets one whose tile is seen empty, `AiBrain.cs:766-778`).

**Own sources** (the present only, no memory): own units that are complete, have a weapon and are either army units or static defences (`Economy.IsStaticDefence`). Workers and the HQ add nothing.

### 3.3 Layers and how strength fades

Two `int` arrays of `cells` entries, `Enemy` and `Own` (resource units, 0 upward). Each source at cell `q` with strength `s` adds, to every cell `r` of the grid with ring distance `k = ring(q, r) <= K`:

`floor(s x (K + 1 - k) / (K + 1))`

where `K` = profile key `threat_spread_cells` (2). With `K` 2 a Windrunner adds 75 to its own cell, 50 to the eight around it and 25 to the next ring (about 12 tiles across at `c` 4, the size of `defend_radius_tiles`). Sources simply add up.

### 3.4 Memory and update order

New `AiMemory` members (not hashed; written only with `use_threat_map`):
- `ThreatSightings`: `SortedDictionary<EntityId, (FixVec2 Position, int Strength, int Tick)>` (tiles, resource units, ticks).
- `ThreatEnemy`, `ThreatOwn`: `int[]` (the layers of the last think, so traces and tests can read them); `ThreatColumns`, `ThreatRows` (cells); `ThreatTick` (the tick they were built, -1 at start).

New file `rts/src/Ai/AiThreatMap.cs` with one entry point, `internal static void Update(AiWorldView view, AiProfile profile, AiMemory memory)`, called in `Decide` **right after `UpdateIntel`** and before the "no HQ" return (`AiBrain.cs:231-239`), only when `use_threat_map` is true. So it runs on every think, before rule H (which reads `TargetOrder`), and after the intel step has refreshed `KnownEnemyStructures`. Steps:
1. **Seen now.** For each `VisibleEnemies` unit (ascending ID) that is an armed army unit: `ThreatSightings[id] = (position, strength, now)`.
2. **Forget.** For each sighting (ascending ID) not refreshed in step 1: remove it when `now - Tick >= M`, or when the tile of its stored position (`floor` of x and y) is visible now (it moved or died where the team can see). Removals are collected, then applied.
3. **Build.** Allocate both arrays zeroed (size from the view's map size and `c`). Add sightings (ascending ID, aged), then armed known structures (ascending ID) to `Enemy`; own sources (ascending ID) to `Own`. Set `ThreatTick = now`.

Reads for consumers: `Turn.EnemyAt(FixVec2 p)` and `Turn.OwnAt(FixVec2 p)` return the layer value at `p`'s cell.

Cost: at most (sightings + structures + own sources) x (2K + 1)^2 additions per think (about 200 x 25 = 5,000 at `K` 2); no unit-pair loops.

### 3.5 Test hook and trace

- `AiBrain.ProbeThreat(view, profile, plan, memory)` (internal, beside `ProbePlacement`): runs `UpdateIntel` and `AiThreatMap.Update` and returns columns, rows, copies of both layers and of the sightings. Null with no HQ.
- `GameTrace` adds `thr=s<sightings>/e<Enemy at own HQ cell>/o<Own at own HQ cell>/m<largest Enemy>@<column>,<row>` (largest ties to the lower cell index; `m0@-1,-1` when the layer is all 0) to each player sample line **only when** that player's profile has `use_threat_map` true, so every existing trace stays byte-identical (test TM-14).
- New trace flag `--threat-grid`: at each sample, for each player whose profile has `use_threat_map` true, prints `threat enemy P<n>` followed by one line per row (row 0 first, values space-separated), then `threat own P<n>` the same way. Without the flag nothing is printed.

With `use_threat_map` on and both consumers off the map changes no intent (test TM-13): it is passive until a consumer reads it.

## 4. Rule TS: is a point safe (package S-TS, `use_threat_safety`)

With the switch on, `IsUnsafe(point)` returns true when **either**:
1. an uncleared enemy spawn (not the own spawn) or a **known enemy HQ** lies within `expand_safe_radius_tiles` of the point (squared `Fix64`, as today); or
2. `EnemyAt(point) > OwnAt(point) + threat_unsafe_margin` (integers).

Compared with today: the "last seen enemy army within the radius" clause is replaced by clause 2 (strength-weighted, every remembered unit, own strength counted); the "any known enemy structure within the radius" clause keeps only enemy HQs, because armed structures are in the map and unarmed ones (depots, producers) are targets, not danger (Q-2). The spawn clause is unchanged.

Every caller of `IsUnsafe` is unchanged and so reads the map: `TargetOrder` (safe fields first: rule E's depot target, ED's target when E is on, `RelocationCandidates`, the worker-target floor of `WantedWorkersUncapped`), the long-haul filter in `Gather` and both tests in `Retarget`. `threat_unsafe_margin` 150 is the value of two Windrunners: a lone scout or single raider no longer makes a field unsafe; a group does, unless the own army stands there.

Validation: `use_threat_safety` true needs `use_threat_map` true (load error naming the file and `use_threat_safety`).

## 5. Rule TH: harass target and route (package S-TH, `use_threat_harass`)

With the switch on, `StartSquad` (`AiBrainFight.cs:427-495`) keeps the squad selection (`452` and earlier) and the static-defence predictor check (`474-478`) and replaces the target choice:
1. **Squad strength** `S` = sum of the members' strengths (section 3.2 formula, own units, ascending ID).
2. **Candidates:** known enemy drop-offs, HQs included (today's filter, `456-460`), ascending ID.
3. **Risk** of a candidate at position `B`: from the squad centre `A` (mean of member positions, `Fix64`), `N = max(1, CeilingToInt(2 x |B - A| / c))`; sample points `P_i = A + (B - A) x i / N` for `i` = 1 to N (`Fix64`); risk `r` = the largest `EnemyAt(P_i)`. So `r` covers the way and the target cell.
4. **Eligible** when `r < S`.
5. **Choice:** the eligible candidate with the smallest `r`; ties to the larger squared distance from `EnemyArmyPosition` (today's key; skipped when no position is known); then the lower ID.
6. **No eligible candidate:** no squad this think and no intent; `NextHarassTick` is not changed, so the next think tries again (Q-5). Today's early return when `EnemyArmyPosition` finds nothing (`448`) does not apply with the switch on (an empty map is safe).
7. The chosen target then goes through the existing defence check and order code unchanged.

Validation: `use_threat_harass` true needs `use_threat_map` and `use_target_choice` true (load error naming the file and `use_threat_harass`).

## 6. Switches, keys and profiles

All root keys, placed after `use_raid_response`. Comment lines in the house format.

| Key | Unit | Range | Old profiles | `aggressive_s5_s` |
| --- | --- | --- | --- | --- |
| `use_threat_map` | boolean | true or false | false | true |
| `threat_cell_tiles` | tiles (integer) | 1 to 16 | 4 | 4 |
| `threat_spread_cells` | cells (integer) | 0 to 8 | 2 | 2 |
| `use_threat_safety` | boolean | true or false | false | see 6.1 |
| `threat_unsafe_margin` | resource units (integer) | 0 to 100000 | 150 | 150 |
| `use_threat_harass` | boolean | true or false | false | see 6.1 |

```
  // P-006 S: keep the threat map (own and remembered enemy strength per cell, built from the fogged view and memory); changes no decision by itself, rules TS and TH read it. Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_threat_map.md section 3; placeholder.
  "use_threat_map": false,
  // P-006 S: side of one threat-map cell. Unit: tiles. Range: 1 to 16. Source: rts/docs/specs/ai_threat_map.md section 3.1; placeholder.
  "threat_cell_tiles": 4,
  // P-006 S: how far a unit's strength reaches on the threat map: ring k of K adds (K + 1 - k) / (K + 1) of it. Unit: cells. Range: 0 to 8. Source: rts/docs/specs/ai_threat_map.md section 3.3; placeholder.
  "threat_spread_cells": 2,
  // P-006 S rule TS: IsUnsafe reads the threat map (enemy strength above own strength plus threat_unsafe_margin, or an enemy HQ or uncleared enemy spawn within expand_safe_radius_tiles); needs use_threat_map. Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_threat_map.md section 4; placeholder.
  "use_threat_safety": false,
  // P-006 S rule TS: enemy strength a point may hold above own strength and still be safe. Unit: resource units of army value. Range: 0 to 100000. Source: rts/docs/specs/ai_threat_map.md section 4; placeholder.
  "threat_unsafe_margin": 150,
  // P-006 S rule TH: the harass squad picks the known enemy drop-off with the least enemy strength on the way, and starts only when that is below the squad's strength; needs use_threat_map and use_target_choice. Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_threat_map.md section 5; placeholder.
  "use_threat_harass": false,
```

Existing keys read, unchanged: `enemy_army_memory_seconds` (sighting age), `expand_safe_radius_tiles` (spawn and HQ clause), `counter_rare_cost_weight` (through `AiCounter.Value`).

### 6.1 Candidate profile

`rts/data/ai/aggressive_s5_s.json`: a byte copy of `aggressive_s5.json` as on `main` when S-MAP starts, with its line-1 header, `$doc` and `id` changed and the S keys. Created by S-MAP with `use_threat_map` true and both consumer switches false. S-TS sets `use_threat_safety` true. S-TH sets `use_threat_harass` true **and `use_threat_safety` false**, so each measurement is one rule wide against the same baseline (section 10). After SM-2 the report package sets each consumer switch in `aggressive_s5_s` by its verdict (section 10.4); nothing else.

**Why `aggressive_s5`.** It is the main AI and the baseline of every recent package; rules IsUnsafe and HR are live in it (`use_target_choice` true). A copy with one rule changed makes the paired comparison exactly that rule. `aggressive_s5_pd` carries the paused raid rules and `aggressive_s5_e` the economy rules that showed no measurable effect; either would mix their effects into S's.

## 7. Harness package S-M (`rts/tools/BalanceHarness` and `rts/data/scripts/ai_eval` only)

Measurement only; nothing here reaches a controller.

### 7.1 Summary fields (`SummaryObserver`, `GameSummary`; old records parse without them)

| JSON key | Meaning | Unit |
| --- | --- | --- |
| `drop_off_life` | one `[placed_tick, death_tick]` per own non-HQ drop-off structure, in placement order: `placed_tick` = the first step it exists as an own unit (under construction counts); `death_tick` = the tick of its `UnitKilledEvent` with an enemy `KillerOwner`, else -1 | ticks |
| `harass` | `[squads, worker_kills, kills_value, losses_value]` up to `T = min(summary_window_seconds, game end)` | see below |

`harass` is counted from the controller's memory: in `AfterThink`, for each controller that is a `SkirmishAiController`, the observer copies `Memory.HarassGroup` into that slot's current set (an empty set for any other controller). `squads` = thinks at which the set went from empty to not empty. On each `UnitKilledEvent` up to T: when `Killer` is in the killer slot's set, add the victim's `cost_common + cost_rare` to `kills_value`, and 1 to `worker_kills` when the victim can gather and has footprint 0; when `Victim` is in the victim slot's set, add its `cost_common + cost_rare` to `losses_value`. Rare weight 1, as `rare_weight` in `behaviour_checks.json`. The victim's definition comes from the observer's own unit record (it already learns every unit it sees).

### 7.2 Checks (`BehaviourChecks.cs`, entries in `behaviour_checks.json`)

Each is Held whenever judged (the bar is the paired test of 7.3), so `max_violation_fraction` is 1.

| Check | Metric per slot-game | Unjudged when | Keys |
| --- | --- | --- | --- |
| `depot_losses_young` | entries of `drop_off_life` with `death_tick >= 0`, `death_tick - placed_tick <= young_seconds x ticks per second`, `death_tick <= T`, and `death_tick` before the slot's `overrun_second` when it has one | never | `enabled` true; `young_seconds` 120 (seconds, 1 to 1800; placeholder: two minutes covers the build and the first trips); `max_violation_fraction` 1; `paired_direction` `"lower"` |
| `harass_trade` | `kills_value - losses_value` (may be negative) | never | `enabled`; `max_violation_fraction` 1; `paired_direction` `"higher"` |
| `harass_squads` | `squads` | never | `enabled`; `max_violation_fraction` 1; `paired_direction` `"higher"` |

`overrun_second` is the existing PD-M2 metric (`ai_eval.json` `overrun_supply` 20, `raid_radius_tiles` 12). The builder confirms it is written in `--behaviour` records; if it is not, the collector is enabled for behaviour runs with the same settings (no new setting).

The existing entries `first_drop_off` and `income_by` gain `paired_direction` `"lower"` and `"higher"`.

### 7.3 The paired behaviour test

For every enabled check whose entry has `paired_direction` (optional; `"lower"` or `"higher"`; any other value is a settings error naming the key), `BehaviourRunner` adds one line after the check's `BEHAVIOUR` lines and one row to a new `## Paired` table in `behaviour.md`:

```
BEHAVIOUR_PAIRED check=<id> candidate=<b> baseline=<a> pairs=<n> unjudged_pairs=<u> better=<W> worse=<L> ties=<T> p=<0.0000> mde_permille=<m> verdict=<BETTER|WORSE|NO_MEASURABLE_EFFECT>
```

- **Pair value.** A pair is the two games of the paired layout (M.1.2) with the same seed. The candidate's value `X` = its metric in its slot of game 2p plus its metric in its slot of game 2p+1; the baseline's `Y` the same over its slots. So each profile is summed over both seats and, in a cross cell, both factions. A pair in which any of the four slot-games is Unjudged is left out and counted in `u`.
- **Better, worse, tie.** With `"lower"`: better when `X < Y`, worse when `X > Y`; `"higher"` the reverse; equal is a tie. `D = W + L`.
- **Test.** `PairedStats.SignTest(W, L, alpha_pt)` with `effect_alpha` of `ai_eval.json` (0.05): BETTER when significant and `W > L`; WORSE when significant and `W < L`; else NO_MEASURABLE_EFFECT.
- **Stated power.** `mde_permille` = the smallest integer m with `400 x D' x (m - 500)^2 >= Z^2`, where `Z = round(10000 x (effect_z_alpha + effect_z_power))` = 28016 and `D' = max(D, D_min)` (`PairedStats.MinDiscordant`, 6 at alpha 0.05). It is the share of decisive pairs, in permille, in which the candidate must be better for the test to see it with power 0.80 (`balance_measurement.md` M.2, written for a share instead of a win rate). A value above 1000 means no result could pass. One new pure function `PairedStats.MdeSharePermille(int d, int z, int alphaPt)`.

Stdout and `behaviour.md` are otherwise unchanged; a check without `paired_direction` prints nothing new.

## 8. Build packages (one builder session each; `dotnet build` with no warnings and `dotnet test` green)

| # | Package | Contents | Tests | Depends on |
| --- | --- | --- | --- | --- |
| 1 | **S-MAP Map and trace** | Section 3 (`AiThreatMap.cs`, the memory, the `Decide` call, `ProbeThreat`, the trace column and `--threat-grid`); `use_threat_map`, `threat_cell_tiles`, `threat_spread_cells` in every profile; `aggressive_s5_s.json`; doc updates (8.1) | TM-1 to TM-15 | nothing |
| 2 | **S-M Metrics** (harness only) | Section 7 | H-S1 to H-S6 | nothing (may run beside S-MAP) |
| 3 | **S-TS Safety** | Section 4; `use_threat_safety`, `threat_unsafe_margin` in every profile; validation; `aggressive_s5_s` safety on | TS-1 to TS-10 | S-MAP |
| 4 | **SM-1 Measure TS** | Section 10 run SM-1; `rts/docs/reports/ai_threat/ts/README.md` | none | S-M, S-TS |
| 5 | **S-TH Harass** | Section 5; `use_threat_harass` in every profile; validation; `aggressive_s5_s` harass on, safety off | TH-1 to TH-9 | S-MAP (and SM-1 run, so the profile edit does not change SM-1) |
| 6 | **SM-2 Measure TH** | Section 10 run SM-2; `rts/docs/reports/ai_threat/th/README.md`; then section 10.4's profile edit | none | S-M, S-TH |

The `sim-reviewer` reviews S-MAP, S-TS and S-TH (determinism notes in section 12). Order: the map first (passive, provable by TM-13), then the smallest consumer (TS is one function body and one harness metric), then TH (more harness attribution).

### 8.1 Documentation each AI package updates

The `AiBrain.cs` header (a paragraph "S. Threat map (ai_threat_map.md, only with use_threat_map): ..." after the 4a''' paragraph, one sentence each for the map, rule TS and rule TH as each lands), the `AiProfile.cs` "Differences" list, the `AiMemory.cs` header, `rts/src/Ai/README.md`, the headers of every touched file, and `rts/tools/BalanceHarness/README.md` for S-M.

## 9. Acceptance tests

AI tests in `rts/tests/Sim.Tests/Ai/AiThreatMapTests.cs` (TM), `AiThreatSafetyTests.cs` (TS), `AiThreatHarassTests.cs` (TH); harness tests in `rts/tests/Sim.Tests/Harness/ThreatMetricsTests.cs`. Real simulations as in `AiFightTests`, `AiBrain.Decide` or the probes on the view; `greybox_1v1` unless stated, P0 Skai, map explored for P0. Hand-made test profiles: copies of `aggressive_s5_s` with the switch under test on, `use_counter_mix` false, no `openings`, jitter keys 0. Where a test writes sightings into memory directly it says so; the builder places units on standable tiles, states the positions in the test, and checks distances against the map file. Values: Windrunner 75, Talon Archer 100, Perch Tower 100, Coral Turret 100, Pearl Depot 100 (unarmed), HQ 0; 20 ticks per second, so `M` = 1200 ticks.

**Map (S-MAP)**

| Id | Pass |
| --- | --- |
| TM-1 | Grid: a 96 by 96 map with `c` 4 has 24 by 24 cells; (95.9, 0.0) is cell (23, 0); a 10-wide map with `c` 4 has 3 columns; (-1, 5) clamps to column 0. |
| TM-2 | Spread: one visible P1 Windrunner at full health at the centre of cell (3, 3), `K` 2: `Enemy` is 75 at (3, 3), 50 on ring 1, 25 on ring 2, 0 on ring 3; `K` 0 gives 75 in its cell only. Cells off the grid are skipped (a unit in cell (0, 0) writes nothing outside). |
| TM-3 | Health: the same Windrunner at half health gives 37 in its cell, `floor(37 x 2 / 3)` = 24 on ring 1. |
| TM-4 | Ageing (sighting written into memory, tile not visible to P0): tick now - 600 gives 37 in its cell; tick now - 1200 removes it and the layer is 0. |
| TM-5 | Seen gone: a sighting whose stored tile is visible to P0 now, with no enemy there, is removed on the next update. |
| TM-6 | Structures: a known P1 Coral Turret (health share 1, last seen 300 s ago) adds 100 in its cell; a known Pearl Depot and a known HQ add 0. |
| TM-7 | Own: a complete own Talon Archer adds 100 to `Own`; a worker, the HQ and a Perch Tower under construction add 0; a complete Perch Tower adds 100. |
| TM-8 | Only armed army units become sightings: a visible P1 worker adds nothing and makes no sighting. |
| TM-9 | Fog: a P1 Windrunner present in the simulation but not in `VisibleEnemies` adds nothing anywhere. |
| TM-10 | Sum: two Windrunners in one cell give 150 there. |
| TM-11 | Determinism: two runs of the same think give identical layers and sightings. |
| TM-12 | Off path: with `use_threat_map` false, `ThreatSightings` stays empty and `ThreatTick` stays -1 over a traced game; every pin of section 0 holds without re-pinning. |
| TM-13 | Passive map: `aggressive_s5` and `aggressive_s5_s` (map on, both consumers off) give identical intents on every think and identical generator and `AiVariety` draw sequences over a fixed traced game (`prototype_1v1`, skai_vs_water, seed 20261026, 600 s), and the same trace result line. |
| TM-14 | Trace: the `aggressive_s5` trace file is byte-identical before and after S-MAP (`cmp`); an `aggressive_s5_s` trace has `thr=` on every player sample line; `--threat-grid` prints 24 rows per layer per player on `prototype_1v1`. |
| TM-15 | Parsing: every shipped profile has the three keys; only `aggressive_s5_s` has `use_threat_map` true; `threat_cell_tiles` 0 and 17 and `threat_spread_cells` 9 fail naming the key; a missing key fails. `aggressive_s5_s.json` differs from `aggressive_s5.json` only in line 1, `$doc`, `id` and the S keys (line diff after removing comment lines). |

**Rule TS (S-TS)** (a field F with no known enemy structure and no uncleared enemy spawn within 14 tiles, out of P0's vision unless stated)

| Id | Pass |
| --- | --- |
| TS-1 | Lone scout: one visible P1 Windrunner in F's cell. Switch on: F is safe (75 <= 0 + 150) and comes in `TargetOrder` by distance among the safe fields. Switch off: F is unsafe (the old army clause) and is listed after them. |
| TS-2 | Group: three Windrunners and one Talon Archer visible in F's cell (325): unsafe with the switch on and off. |
| TS-3 | Own army counts: TS-2 plus three own Talon Archers in F's cell: switch on safe (325 <= 300 + 150); switch off unsafe. |
| TS-4 | Memory: TS-2's four units written as sightings at now - 200: each aged `floor(75 x 1000 / 1200)` = 62 and 83, total 269: unsafe. At now - 800: 25 x 3 + 33 = 108: safe. |
| TS-5 | Structures: a known P1 Pearl Depot within 14 tiles of F and nothing else: switch on safe, switch off unsafe. One known Coral Turret in F's cell: safe (100); two: unsafe (200). |
| TS-6 | Spawn and HQ clause: F within 14 tiles of an uncleared enemy spawn, or of a known enemy HQ, is unsafe with the switch on and off. |
| TS-7 | Expansion: with TS-1's scout at the nearest unserved field, rule E (clock due, common 500) orders the depot beside that field with the switch on and beside a different field with it off. |
| TS-8 | Relocation: a running relocation whose destination cell holds TS-2's group re-targets (switch on); with only TS-1's scout there it does not (switch on) while the old rule re-targets. |
| TS-9 | Determinism: TS-3 and TS-7 run twice give identical intents and memory. |
| TS-10 | Off path and validation: pins hold; a copy of `aggressive_s5` with `threat_unsafe_margin` 0 gives the same trace result line as `aggressive_s5` (key unread when off); `use_threat_safety` true with `use_threat_map` false fails to load naming `use_threat_safety`. |

**Rule TH (S-TH)** (P0 squad of two Windrunners, `S` = 150; `use_target_choice` true; harass cooldown passed, Massing; known P1 depots placed as stated)

| Id | Pass |
| --- | --- |
| TH-1 | Weakest target: four visible P1 Windrunners at depot B (`Enemy` 300 there), one visible P1 Windrunner nearer the P0 HQ than any other enemy army unit and within one cell of depot A, B farther from that unit than A. Switch on: the squad's `AttackMove` goal is `Standable` of A. Switch off: B (farthest from the single last-seen point). |
| TH-2 | Route: depot C has `Enemy` 0 but the line from the squad centre to C passes a cell holding three remembered Windrunners (sightings, 225 there); depot D's line is clear. Switch on: target D. With D removed: no squad. |
| TH-3 | None eligible: every candidate has `r >= 150`: no intent for any squad unit, `HarassGroup` empty, `NextHarassTick` unchanged; on the next think with the blocking sightings expired, the squad starts. |
| TH-4 | Ties: two eligible depots with equal `r`: the one farther from `EnemyArmyPosition`; equal again: the lower ID. |
| TH-5 | Defence check kept: the only eligible depot has a P1 Coral Turret beside it and the predictor says the squad loses: no squad (as today). |
| TH-6 | No army known: no enemy army position and no known HQ, one known depot with `r` 0: switch on, the squad starts; switch off, no squad (today's early return). |
| TH-7 | Validation: `use_threat_harass` true with `use_threat_map` false, and separately with `use_target_choice` false, fails to load naming `use_threat_harass`. |
| TH-8 | Determinism: TH-1 and TH-2 run twice give identical intents and memory. |
| TH-9 | Off path: pins hold; `RunSquad` intents are identical with the switch on and off once a squad is running toward the same target. |

**Harness (S-M)**

| Id | Pass |
| --- | --- |
| H-S1 | `drop_off_life` from a hand-built stream: depot placed at 100 s and killed by the enemy at 180 s, depot placed at 200 s alive: `[[2000, 3600], [4000, -1]]`. |
| H-S2 | `depot_losses_young` with `young_seconds` 120: the first depot counts (80 s); with `overrun_second` 170 it does not; a depot killed at 400 s after placement at 200 s does not (200 s). |
| H-S3 | Harass attribution with a stub controller whose `HarassGroup` is {u1} from think 3: u1 kills a worker (50) and a Windrunner (75), u2 (not in the set) kills a worker, u1 dies (75): `squads` 1, `worker_kills` 1, `kills_value` 125, `losses_value` 75, `harass_trade` 50. |
| H-S4 | Paired test on hand-made records: 10 pairs, `"lower"`, 9 better and 1 worse: p 0.0215, BETTER; 8 and 2: p 0.1094, NO_MEASURABLE_EFFECT; 1 and 9: WORSE; a pair with an Unjudged slot-game is counted in `unjudged_pairs` and left out. `MdeSharePermille`: D 72 gives 666, D 48 gives 703, D 24 gives 786, D 4 gives 1072 (D' = 6). |
| H-S5 | Observation only: the same seeds give the same `state_hash` and `result` per game with and without the new fields; a record without them parses (empty `drop_off_life`, zero `harass`). |
| H-S6 | A behaviour run with no `paired_direction` in any entry prints the same stdout as before S-M apart from the new checks' `BEHAVIOUR` lines and the summary count. |

## 10. Measurement (fixed before any run; nothing is tuned after a result)

### 10.1 Runs

Paired behaviour runs on `prototype_1v1` only, Release build of the measured commit, baseline **`aggressive_s5`** (profile-a), candidate **`aggressive_s5_s`** (profile-b), base seed and matchups of `ai_eval.json`, 48 games per cell (24 pairs per cell, 72 pairs, 144 games; Q-7):

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_s --maps prototype_1v1 --games 48 --jobs 4 --out rts/build/harness/<sm1|sm2>
```

plus one `--trace --threat-grid` per matchup with `aggressive_s5_s` in both seats (seed 20261026, 600 s). Expected wall time about 90 minutes per run (E-M took 45 minutes for 72 games).

- **SM-1**, after S-TS and S-M merge: `aggressive_s5_s` has the map and rule TS on, TH absent.
- **SM-2**, after S-TH merges: `aggressive_s5_s` has the map and rule TH on, TS off.

Both profiles have the same openings and jitter keys, so the pairing kind is `full` for both factions (M.1.3); the run header must say so, else stop and report.

### 10.2 Bars

Every bar is a paired test of 7.3 (pair sums, exact two-sided sign test at alpha 0.05), so none is a strict comparison of two medians inside their noise (the EB-4 lesson, `reports/ai_econ/README.md`). A **primary** bar passes only on BETTER: it is the rule's reason to exist. A **guard** bar passes unless WORSE.

**SM-1 (rule TS)**

| Bar | Check (direction) | Kind | Pass when |
| --- | --- | --- | --- |
| TS-B1 Fewer depots lost young | `depot_losses_young` (lower) | primary | verdict BETTER |
| TS-B2 Expansion not delayed | `first_drop_off` (lower) | guard | verdict not WORSE |
| TS-B3 Economy kept | `income_by` 300 s (higher) | guard | verdict not WORSE |
| TS-B4 Clean run | spot check; suite on the measured commit | guard | 0 mismatches; every pin of section 0 holds |

**SM-2 (rule TH)**

| Bar | Check (direction) | Kind | Pass when |
| --- | --- | --- | --- |
| TH-B1 Better harass trades | `harass_trade` (higher) | primary | verdict BETTER |
| TH-B2 Harass kept (variety) | `harass_squads` (higher) | guard | verdict not WORSE |
| TH-B3 Economy kept | `income_by` 300 s (higher) | guard | verdict not WORSE |
| TH-B4 Clean run | as TS-B4 | guard | as TS-B4 |

### 10.3 Stated power (before the run)

The sample is 72 pairs per run. What a bar can see depends on D, the pairs that are not ties, which is known only after the run; the report prints D and `mde_permille` per bar. Fixed now:

| D (decisive pairs) | better pairs needed for BETTER | smallest effect seen with power 0.80 (candidate better in this share of decisive pairs) |
| --- | --- | --- |
| 72 | 45 (625 permille) | 666 permille, about 2 in 3 |
| 48 | 32 (667) | 703 |
| 36 | 25 (694) | 734 |
| 24 | 18 (750) | 786 |
| 12 | 10 (833) | 905 |
| 6 | 6 (all) | 1072: no result can be seen reliably |
| below 6 | cannot pass | none |

Reading for the primary bars: a rule that changes the metric in only a few pairs (for example depots are rarely lost young, or only Skai builds harass squads, so Water-mirror pairs tie at 0) has a small D and needs a large, consistent effect to pass; if it does not, it is recorded as NO_MEASURABLE_EFFECT and switched off (P-006 decision 3). Reading for the guards: a guard can only catch a regression at least as large as its row (with D 72: the candidate worse in about 2 of 3 decisive pairs); smaller regressions can pass unseen, and the report says so beside each guard.

### 10.4 Verdicts and what follows

- A run whose bars all pass: its consumer switch is set true in the final `aggressive_s5_s` (done by SM-2 for both runs, a data edit by this rule, no tuning). The developer decides whether it goes on in `aggressive_s5` (which moves the `aggressive_s5` pin once).
- A primary bar NO_MEASURABLE_EFFECT or WORSE, or any guard WORSE, or TS-B4/TH-B4 failing: the switch stays false in every profile, including the final `aggressive_s5_s`.
- The combined effect of TS and TH together is not measured by this spec (Q-6).
- No key value, bar, direction, window or rule changes after a result; a change is a new revision with its bars restated before the next run.

### 10.5 Reported, not judged

Per run: the medians of every check for both profiles (as printed); for SM-1, depots placed per slot-game, the median second of the second and third depots (`drop_off_ticks`), HQ relocations (`hq_move_new_income` moves, information only), workers at 300 s; for SM-2, `worker_kills`, `kills_value`, `losses_value` and the opponent's `worker_losses_pre_overrun` per faction; from the traces, the `thr=` column and one `--threat-grid` dump at 300 s per matchup. The win and loss columns are not read.

## 11. Later packages (not specified here)

| Package | What it reads from the map | Waits on |
| --- | --- | --- |
| S-R Raid response | `Enemy` minus `Own` at field cells for closed fields (replacing the hit-position memory of RS and RW), and the raid to answer in RA (section 2) | the developer's return to raid rules; new bars |
| S-D Tower base | the base whose cell has the largest `Enemy` minus `Own`, replacing `FindExposedBase`; how many towers stays with package B's defence share | package B or a defence-share rule (AI-P's failure was spending, not placement) |
| S-P Producer placement | the base with the lowest `Enemy`, replacing AI-P P1's "fewest producers" | a measurable metric for production loss (none exists) |
| S-H HQ moves | destination safety already comes through TS; "a move must bring new income" is an economy rule from E's numbers | E's verdict review by the developer |

A per-layer (ground, air, water) split of strength would be needed by S-D (a tower that cannot hit air) and is not built until then.

## 12. Determinism notes for the sim-reviewer

- No Sim change. New AI state: `ThreatSightings` (sorted dictionary by entity ID), `ThreatEnemy` and `ThreatOwn` (arrays indexed by cell), `ThreatColumns`, `ThreatRows`, `ThreatTick`; written only with `use_threat_map`; not hashed.
- The map is rebuilt from zero each think: sightings, then structures, then own sources, each in ascending ID; the result does not depend on the order anyway (integer additions), but the order is fixed. Ageing and spread are integer floor divisions with `long` products; the only `Fix64` work is the cell of a point (`FloorToInt`), unit health shares, and TH's squad centre, line length and sample points.
- Rule TS adds no loop over units (one array read per call). Rule TH's choice has a total tie-break (risk, then distance from the army position, then ID).
- No new random draw; check that no new path calls the generator or `AiVariety` (TM-13 proves it for the passive map).
- Fog: sightings come from `VisibleEnemies`, structures from `KnownEnemyStructures`, own sources from `OwnUnits`; nothing else about the enemy is read.
- The harness reads controller memory (`HarassGroup`) after each think for measurement only; it never writes it.

## Rejected extras (one line each)

- Path distance or terrain cost in the spread or the route: no consumer here needs it; straight lines are what IsUnsafe and HR use today.
- Spreading a remembered unit's position over time (where it could have walked): ageing the strength is simpler and has a plain meaning.
- A separate memory window for the map: `enemy_army_memory_seconds` already says how long a seen army counts.
- Counting the enemy HQ's weapon: its value is 0 by the data (cost 0), and HR keeps its predictor check against defences at the target.
- Replacing rule SD's site search: it already faces the raiders; the base choice is S-D.
- Measuring TS and TH together in one run: one rule per run, so each verdict belongs to one rule.
- A median bar with a margin: the EB-4 result showed a median compare inside noise decides nothing; the sign test states its power.

## Out of scope

- Any `rts/src/Sim` change; any unit, combat, economy, tuning or map number; wall placement (authored lines stay).
- Raid response, tower base, producer placement and HQ moves (section 11).
- Turning any S switch on in a profile other than `aggressive_s5_s`; S together with packages B, E or the production-defence rules.
- Maps other than `prototype_1v1` in the measurement; win rates as bars; difficulty levels.

## Questions for the developer (builders use the defaults)

1. **Scope: the map plus rules TS and TH only.** *Default: yes*; raid response (S-R), the tower base (S-D), producer placement (S-P) and HQ moves (S-H) are later packages (section 11). Alternative: add S-R now with new bars.
2. **Unarmed enemy structures no longer make a site unsafe** (an enemy depot near a field is a target, not danger; armed structures count by strength). *Default: yes.* Alternative: keep "any known enemy structure within 14 tiles" as a third clause of TS.
3. **Strength is army value** (cost_common + counter_rare_cost_weight x cost_rare) times health share; an HQ's weapon counts 0. *Default: yes.* Alternative: the fight predictor's power per unit (more exact, slower, and tied to the predictor's known errors).
4. **The map forgets a sighting after `enemy_army_memory_seconds` (60 s)**, ageing linearly, and at once when its tile is seen empty. *Default: yes, reuse the key.* Alternative: a separate `threat_memory_seconds`.
5. **Harass waits when every target or way there holds more enemy strength than the squad.** *Default: yes; TH-B2 guards that the AI keeps harassing.* Alternative: fall back to today's target when none is eligible.
6. **Each rule is measured alone against `aggressive_s5`; the final candidate carries every rule that passed, and their combined effect is not measured.** *Default: yes.* Alternative: a third run of the combined candidate before any is switched on in `aggressive_s5`.
7. **48 games per cell (72 pairs) per run**, about 90 minutes each, twice the behaviour default, so a 2-in-3 effect is visible when most pairs are decisive. *Default: yes.* Alternative: the default 24 games per cell (36 pairs; D 36 needs 25 better and sees about 734 permille).
8. **Guards pass unless the sign test finds the candidate worse**, so a regression smaller than the stated power can pass. *Default: yes, the power is printed beside each guard.* Alternative: a guard on medians with a fixed margin (the method EB-4 showed to be unreliable).
9. **Placeholders:** cell 4 tiles, spread 2 cells, margin 150 (two Windrunners), young depot 120 s. *Default: as given; a change is a new revision with bars restated.*
10. **If a rule passes, switch it on in `aggressive_s5`?** *Default: no change until you decide*; doing so moves the `aggressive_s5` trace pin once.

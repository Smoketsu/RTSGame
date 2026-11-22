<!-- rts/docs/specs/ai_mechanic_use.md -->
<!-- Build spec for P-006 package A ("mechanic use"), revision 3 (22 Nov): the skirmish AI casts Downdraft and the wave, trains and places Tidecallers (and Stormcallers as support, only when what the enemy fields calls for them), breaches enemy walls, and walls its main base and its expansions along authored choke lines read from rts/data/ai/walls/<map id>.json (the Wind Baffle is the Skai wall). HQ Last Stand has no AI rule (it is switched off in tuning; see M5). Four profile switches, off in every existing profile; candidate aggressive_s5_m. One load-time change in rts/src/Sim/Data (routing ai/walls/), nothing in the step, no unit or combat number change, no pin moves. Measurement bars are fixed in section 7 before any run. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by the sim-reviewer after each package. -->
# Spec: the skirmish AI uses the mechanics it never uses (P-006 package A)

**Revision 3 (22 Nov)**, after the developer's decision of 22 Nov: "Yes, use authored choke lines" (walls used "strategically, like walling off the ramps and areas they need to protect during expansion"). Revision 2 was commit `1394034` (merged at `c91bdeb`). P1 is merged; P2 is being built as revision 1 wrote it; P2b, P3 and P5 are as revision 2 wrote them; P4 is not started. Changes:
- **Rules WI and WX are replaced by one rule WL** (M3.2): the AI walls the authored lines of the areas it holds, main base first. Revision 2's computed plateau plan (WI) and computed approach ring (WX) are dropped, with their keys `wall_plateau_max_tiles`, `wall_max_tiles`, `expansion_wall_ring_steps`, `expansion_wall_detour_steps` and `expansion_wall_max_tiles`, their memory (`WallPlan`, `EnemySpawnSteps`, `ExpansionWallPlans`) and the view field `WallLineMaxTiles`. On a map with no authored lines the AI builds no walls (no fallback).
- **Authored lines, data in `rts/data/ai/walls/<map id>.json`** (M3.2.1, why there): per area its ID, kind (`main` or `expansion`), owning start slot (or -1 for neutral), resource-field indices (how the AI matches a depot to it), anchor tiles, the areas it covers, and straight lines with an optional gate tile. Every area's lines, with the cliffs and rock, seal it against ground movement (checkable property, test WD-2). **The file for `prototype_1v1` is in this commit**: 10 areas, every one seals (M3.2.3).
- **One Sim change, load-time only (package P4-0):** the data loader routes `ai/walls/<map id>.json` into a new `DataSet.AiWallLines`, as it routes profiles. Nothing in the step, not in the content digest, no pin moves. **Merge order: the data file in this commit makes today's loader fail (it rejects any `ai/` file that is not `ai/<id>.json`), so this branch must reach `main` together with or after P4-0, never before.**
- New key `wall_max_spend_common` (spending cap per match); candidate `wall_funnel_gap_tiles` 1 becomes 0 (a funnel leaves only the gate tile open: with authored ramp lines of 4 tiles a gap radius of 1 left 3 of 4 tiles open). Placeholders; no run has been made.
- New view field `MapId`; new memory `WallSpentCommon`; `AiProfile.FromDataSet` attaches the parsed lines (hosts unchanged).
- Tests WI-1 to WI-12 and WX-1 to WX-11 are replaced by WD-1 to WD-4 (data: validation, seal in the simulation's own path code, mirror, digest) and WL-1 to WL-14 (rule).
- Section 7.2 restated with the same bars; the reported list changes (per area kind instead of "outside the HQ plateau"). Determinism notes updated.
- Questions: Q-10 and Q-12 removed (no computed approach ring); Q-11 reworded for authored data; new Q-14 and Q-15.

Revision 2 (22 Nov) changes, still in force where revision 3 does not replace them:
- **P2b (new, follows P2):** each `support_units` entry can be made conditional on what the AI has seen of the enemy (plan key `support_needs`, M2.1b). Candidate: Stormcaller support only once an enemy flyer has been seen; Tidecaller as before. P2 itself is not rewritten.
- **P4 amended:** new rule WX walls the approach to each expansion the AI holds or is building; four new keys; the HQ-plateau rule WI is unchanged. A funnel counts as wall use (decided). *(Revision 3 replaces WI and WX by rule WL; the funnel decision stands.)*
- **P5 amended:** the `skai/downdraft` bar is judged only over Skai slot-games whose opponent fielded a flyer; P5 gains the harness change that makes this count (7.3). P5 now runs after P2b and P4.
- **Section 7.2 bars restated** (bar 1 restricted for `skai/downdraft`; the rest unchanged). `balance_unspent` is no longer in the reported list (the rare pile-up is not an issue, developer 22 Nov).
- **Decided 22 Nov, removed from the questions:** Q-1 Last Stand stays off, rows reported not judged; Q-2 the AI walls, and walls expansion approaches too; Q-3 Water's walls hold back its own flood, no special rule; Q-4 a funnel counts; Q-5 one Tidecaller per 12 and one Stormcaller per 20 army supply, at most 3 each, conditional on need; Q-6 per-match wave-aim draw; Q-7 and Q-9 Downdraft only at visible flyers, never used or judged in games without enemy flyers; Q-8 style draws from the existing variety stream. New questions Q-10 to Q-13 at the end.
- Research is being turned on in `aggressive_s5` by the coordinator in a separate change; that moves the `aggressive_s5` trace pin once (section 0). The candidate `aggressive_s5_m` is unchanged by it.

Revision 1 written 22 Nov 2026 by the designer against `main` at `fbb4f86`; revision 2 against `main` at `4d31aa0`; revision 3 against `main` at `c91bdeb` (also read: `Sim/Data/DataLoader.cs` folder routing and `CheckDepth`, `Sim/Data/DataSet.cs`, `Simulation.cs` content digest, `Pathing/FlowFieldCache.cs`, `AiBrain.cs` `Expand`, `TryFindDepotSite`, `IsGoodSite` and `TargetOrder`, `SkirmishAiController` construction in every host). Revision 2 also read `rts/src/Ai/AiBrainPlacement.cs` (`Bases`), `AiBrain.cs` (`Expand`, `TryFindDepotSite`, `TargetOrder`, `ReachSquared`), `AiIntel.cs` (`UpdateModel`, `EnemyTypes`), `Terrain/TerrainGrid.cs` (`IsStepPassable`), `Commands/EconomyCommands.cs` (wall-line tile checks), `maps/prototype_1v1.json` (regions computed from its height and ramp rows, M3.2b) and the harness `BalanceReport.MechanicUse` and `MechanicDetectors`. Sources: `proposals/P-006-ai-built-for-balancing.md` (section 3 item 1, section 4 "Then: A", section 6 check 1), `proposals/P-005-roster-holes-and-ai-for-balance.md` (decisions of 18 Nov: unit and combat numbers frozen, AI-vs-AI win rates not read as balance), `specs/research_and_unlocks.md` R2 (format), `specs/skirmish_ai_v4.md` package A (A2 casting and support, A4 breach: specified 18 Nov, never built; reused here and adapted to today's code), `specs/walls_and_tech.md` (W, G, D), `reports/ai_r/README.md` and `reports/ai_r/balance/balance.md`. Code read: `rts/src/Ai/AiBrain.cs` (header, `Decide`, `TryBuild`, `Army`), `AiBrainFight.cs` (`_controlled`), `AiWorldView.cs`, `AiIntent.cs`, `AiMemory.cs`, `AiVariety.cs`, `AiProfile.cs`, `SkirmishAiController.cs` (`ReadEvents`); Sim: `Economy/WallLine.cs`, `Commands/EconomyCommands.cs` (`BuildWallLineCommand`), `Commands/StatusCommands.cs` (`CastGroundingCommand`), `Factions/Water/CastWaveCommand.cs`, `FloodingMechanic.cs` (`TryGetNextWaveTick`, `WaveDirection`), `Terrain/TerrainGrid.cs` (`EdgePassable`), `Units/HqSystem.cs` (`AfterDamage`), `Units/Unit.cs`; data: `units/*`, `tuning/hq.json`, `tuning/flood.json`, `tuning/economy.json`, `ai/aggressive_s5_r.json`; harness: `MechanicDetectors.cs`, `SummaryObserver.cs`, `GameTrace.cs`, `PlayerLedger.cs`.

## 0. Why, what is out, and ground rules

`balance_use` on `prototype_1v1` (R2.5 run, candidate `aggressive_s5_r`) fails with 13 of 57 items at use share 0.000:

| Item(s) | Why the share is 0 today | Section |
| --- | --- | --- |
| `skai/downdraft` | No rule emits `cast_grounding` | M1 |
| `water/wave`, `water.tidecaller` | No rule emits `cast_wave`; the Tidecaller is unarmed, so the counter-mix pool (`AiCounter.Pool`, weapon required) never picks it | M2 |
| `skai/wall`, `water/wall`, `skai/gate`, `water/gate`, `water.coral_wall`, `water.coral_gate`, `skai.wind_gate` | No rule builds a wall (walls_and_tech.md W.4: by design, "the AI never builds walls") | M3 |
| `skai.wind_baffle` | It is the Skai wall unit: the same row as `skai/wall`, seen as a unit | M4 |
| `skai/hq_last_stand`, `water/hq_last_stand` | `tuning/hq.json` has `last_stand_enabled: false`: the mechanic cannot fire for anyone | M5 |

**Ground rules.**
- **No `rts/src/Sim` change except P4-0.** Every Sim read below is public and was checked (file and member named where used). The one exception (revision 3) is P4-0: the data loader routes `ai/walls/<map id>.json` into `DataSet.AiWallLines` (M3.2.1); it runs at load time, never in a step, and the content digest does not cover it. If a builder finds any other Sim change unavoidable, stop and report to the coordinator.
- **No pin moves.** AI profiles and AI wall-line files are not in the content digest. With the four new switches false, every existing profile plays bit for bit as on `fbb4f86`. The pins (current values in the tests; the values in `STATUS.md` line 83 are older and out of date): smoke `207be73eaa460b3c` and economy_smoke `98b2e00d9babe167` (`Harness/HarnessUnitTests.cs`), `aggressive_s5` trace `837f8e7118a9c9de` end tick 9505 (re-pinned 22 Nov when research went on in the main AI) and `aggressive_s3` trace `c04bdcc0d03b3910` end tick 9223 (`Ai/FightSimTests.OldProfiles_FixedSeedGamesKeepTheirHash`), plus `V2ReferenceHash` and `BlindTraceHash` (`Ai/SkirmishAiTests.cs`). If one moves, stop and report. Exception (revision 2): the coordinator's separate change that turns research on in `aggressive_s5` re-pins the `aggressive_s5` trace once; a package started after that change uses the pins as they stand on `main` when it starts, and still moves none of them.
- **Determinism (AI).** `Fix64` and integers only; own units, enemies, fields and tiles walked in ascending entity ID or tile index; distances compared squared; every tie to the lower ID or tile index unless a rule says otherwise. The only new random draws are the two match-start style draws of section 6, from the existing variety stream (`AiVariety`), and only when their switch is on. No `System.Random`, no clock. AI memory is not hashed; determinism is shown by harness hashes and by "same inputs twice" tests.
- **Fog.** The AI reads only `AiWorldView`: own units (now including their ability cooldowns and build queues), `VisibleEnemies`, tiles the team sees now, public map data (heights, ramps, the map ID), the authored wall lines of the map (public data, like the map), unit definitions, tuning, its enemy model and memory, and events about its own units. Every view addition below is own-team or public information.
- **Commands**: only those a player can issue. New for the AI: `cast_wave`, `cast_grounding`, `build_wall_line`. No new command or event type.
- **The brain never names a faction or unit ID.** Unit IDs appear only in profile data. Walls and gates are found by `Economy.IsWall` / `Economy.IsGate`, casters by `Definition.Flood.CanCastWave` / `Definition.GroundingAbility`. Two plumbing files name Sim faction types: `AiWorldView.cs` reads the wave cooldown through `FloodingMechanic`, and `AiCommandWriter` (in `AiIntent.cs`) constructs `CastWaveCommand` (namespace `Rts.Sim.Factions.Water`). Say so in both file headers.
- **Numbers.** No unit, combat, economy, map or tuning number changes (P-005 freeze). Every new AI decision number is a profile key in `rts/data/ai/*.json` with the house comment line `// <what it controls>. Unit: <unit>. Range: <range>. Source: rts/docs/specs/ai_mechanic_use.md <section>; placeholder.` Every value in this spec is a **placeholder**.
- **Parsing.** `AiProfile.Parse` reads every new key with the `Require*` helpers and adds it to `RejectUnknownKeys`. Every profile file under `rts/data/ai` gets every new key (switch false, "old" value of section 1). Hand-written profile JSON in tests gets them too.

## 1. Switches, keys and profiles

Four switches, one per mechanic group:

| Switch | Turns on | Section |
| --- | --- | --- |
| `use_casts` | rules CG (Downdraft) and CW (wave), and the wave-aim draw | M1, M2 |
| `use_support_units` | rules SU (train support units) and ST (support trail), rule RQ-1's support extension, and the support needs of P2b | M2 |
| `use_wall_breach` | rule WB (attack a wall that seals the route) | M3 |
| `use_walls` | rule WL (walls and gates on the authored lines of the main base and of held expansions; expansions also need `expansion_wall_max_bases` above 0) and the wall-style draw | M3 |

All root keys (in the root object of the profile) unless "plan" is stated (in each faction plan under `factions.<id>`):

| Key | Unit | Range | Old profiles | `aggressive_s5_m` |
| --- | --- | --- | --- | --- |
| `use_casts` | boolean | true or false | false | true |
| `wave_min_enemy_supply` | supply, weighted (Fix64) | 0.5 to 200 | 4 | 4 |
| `wave_melee_range_tiles` | tiles (Fix64) | 0.5 to 3 | 1 | 1 |
| `wave_aim_weights` | object `{ "melee": int, "all": int }`, weights | each 0 to 100, sum above 0 | `{ "melee": 100, "all": 0 }` | `{ "melee": 50, "all": 50 }` |
| `wave_max_commands_per_think` | commands | 0 to 40 | 4 | 4 |
| `grounding_min_air_supply` | supply | 1 to 200 | 3 | 3 |
| `grounding_max_commands_per_think` | commands | 0 to 40 | 4 | 4 |
| `use_support_units` | boolean | true or false | false | true |
| `support_max_per_type` | units | 0 to 20 | 3 | 3 |
| `support_trail_tiles` | tiles (Fix64) | 0 to 20 | 3 | 3 |
| `support_regroup_tiles` | tiles (Fix64) | 1 to 20 | 3 | 3 |
| `support_units` (plan) | object unit ID to army supply per unit | each 1 to 200 | `{}` | water `{ "water.tidecaller": 12 }`, skai `{ "skai.stormcaller": 20 }` |
| `support_needs` (plan; P2b) | object unit ID to a list of movement layer names (`"ground"`, `"water"`, `"air"`) | keys a subset of the plan's `support_units` keys; each list 0 to 3 distinct names | `{}` | water `{}`, skai `{ "skai.stormcaller": ["air"] }` |
| `use_wall_breach` | boolean | true or false | false | true |
| `wall_breach_memory_seconds` | seconds | 1 to 120 | 10 | 10 |
| `wall_breach_standoff_tiles` | tiles (Fix64) | 0.5 to 10 | 2 | 2 |
| `wall_probe_step_tiles` | tiles (Fix64) | 1 to 20 | 6 | 6 |
| `wall_breach_max_commands_per_think` | commands | 0 to 60 | 12 | 12 |
| `use_walls` | boolean | true or false | false | true |
| `wall_style_weights` | object `{ "none": int, "funnel": int, "sealed": int }`, weights | each 0 to 100, sum above 0 | `{ "none": 100, "funnel": 0, "sealed": 0 }` | `{ "none": 20, "funnel": 30, "sealed": 50 }` |
| `wall_earliest_seconds` | seconds | 0 to 1800 | 180 | 180 |
| `wall_min_enemy_ground_share` | fraction (Fix64) | 0 to 1 | 0.5 | 0.5 |
| `wall_threat_tiles` | tiles (Fix64) | 0 to 30 | 10 | 10 |
| `wall_funnel_gap_tiles` | tiles (Chebyshev radius around a line's gate tile left open in style `funnel`) | 0 to 3 | 1 | 0 (revision 3; was 1) |
| `wall_max_lines_per_think` | commands | 1 to 8 | 2 | 2 |
| `expansion_wall_max_bases` (P4) | expansion areas | 0 to 4; 0 walls the main base only | 0 | 2 |
| `wall_max_spend_common` (P4, revision 3) | common resource units | 0 to 20000; 0 buys no wall or gate | 0 | 1200 |

Starting values: the v4 A2 and A4 values where v4 gave one (`wave_min_enemy_supply` 4 = two Pummelers in the strip; `grounding_min_air_supply` 3 = one Roc Rider; support 12 and 20 = v4 Q-5, confirmed by the developer 22 Nov); the rest are new placeholders. `wall_max_spend_common` 1200 covers the slot-0 progression on `prototype_1v1` in style `sealed` (main 225, then the natural ring 475, then the basin ring 300 more: 1000, M3.2.3) plus eight rebuilt walls; placeholder.

Validation (errors name the file and key path, as existing keys do): each `support_units` ID is a unit of the plan's faction with `Supply > 0`; each `support_needs` key is also a key of the same plan's `support_units`, each list holds only `"ground"`, `"water"`, `"air"` without repeats, and a non-empty list with `use_enemy_model` false is an error (the needs are read from the enemy model's sightings); weight objects have exactly their listed keys and a sum above 0.

Profile comment lines for the new keys (house format, section 0), to be copied as written:
- `support_needs`: `// P2b: per support unit, the enemy movement layers of which at least one army type must have been sighted before rule SU trains it; a unit not listed, or listed with [], is always needed. Unit: object unit ID to list of layer names. Range: keys from support_units; names ground, water, air. Source: rts/docs/specs/ai_mechanic_use.md M2.1b; placeholder.`
- `expansion_wall_max_bases`: `// Rule WL: how many held expansion areas (in the order they were first held) get their authored lines; 0 walls the main base only. Unit: expansion areas. Range: 0 to 4. Source: rts/docs/specs/ai_mechanic_use.md M3.2; placeholder.`
- `wall_max_spend_common`: `// Rule WL: common committed to wall lines and gates over the whole match (rebuilds included, refunds not credited back); no wall or gate is ordered past it; 0 buys none. Unit: common resource units. Range: 0 to 20000. Source: rts/docs/specs/ai_mechanic_use.md M3.2; placeholder.`
- `wall_funnel_gap_tiles` (comment unchanged in meaning, reworded for authored lines): `// Rule WL, style funnel: tiles of a line within this Chebyshev radius of the line's gate tile stay open (radius 0 leaves the gate tile only); lines without a gate tile are walled whole. Unit: tiles. Range: 0 to 3. Source: rts/docs/specs/ai_mechanic_use.md M3.2; placeholder.`

**Profiles.**
- Every existing file: the keys above with the "old" values. With every switch false the AI plays as on `fbb4f86` (test OFF-1).
- New `rts/data/ai/aggressive_s5_m.json`: a copy of `aggressive_s5_r.json` with `id` changed, the "candidate" values above, and a line-1 header naming this spec. Package P1 creates it with only `use_casts` true; each later package turns its own switch on there. Its off-copy for comparison is `aggressive_s5_r` itself.

## 2. View, intent and memory additions (shared by the sections below)

**`AiUnitView`** (new optional constructor parameters at the end, so existing callers compile unchanged; the synthetic pending-order views in the `Turn` constructor pass the defaults):
- `WaveReadyTick` (int, ticks, 0 upward): own units, `FloodingMechanic.TryGetNextWaveTick(id, out t) ? t : 0` (the mechanic found in `sim.Mechanics` by `FloodingMechanic.MechanicId`; 0 when the match has none); enemies 0.
- `GroundingReadyTick` (int, ticks, 0 upward): own units `Unit.GroundingReadyTick`; enemies 0.
- `BuildQueue` (`IReadOnlyList<int>`, tile indices, 0 to 255 entries): own units a copy of `Unit.BuildQueue`; enemies empty.

**`AiWorldView`**: `WaveLengthTiles`, `WaveWidthTiles` (int, tiles, from `sim.Tuning.Flood`), `MapId` (string, `sim.Map.Id`, public; revision 3, replaces revision 2's `WallLineMaxTiles`, which rule WL no longer needs because the data test holds every authored line within `wall_line_max_tiles`).

**Intents** (`AiIntentKind`, appended values; `AiCommandWriter` builds the command with the usual schedule tick and sequence):
- `CastWave = 9`: `AiIntent.CastWave(EntityId caster, FixVec2 target)` to `CastWaveCommand`.
- `CastGrounding = 10`: `AiIntent.CastGrounding(EntityId caster, FixVec2 target)` to `CastGroundingCommand`.
- `BuildWallLine = 11`: `AiIntent.BuildWallLine(EntityId worker, string definitionId, IReadOnlyList<(int X, int Y)> tiles)` to `BuildWallLineCommand`. New `AiIntent.Tiles` (empty for every other kind).

**Readiness.** An ability is ready when `readyTick <= view.Tick + memory.CommandDelayTicks`.

**Memory** (`AiMemory`, not hashed): `UnreachableGoals` (M3.1), `WallTilesRefused` and `WallSpentCommon` (M3.2). Style values drawn at match start live in `AiVariety` (section 6). P2b reads the existing `AiMemory.EnemyTypes` and adds nothing.

## M1. Downdraft (`use_casts`)

**Helper** (`rts/src/Ai/AiAbilities.cs`, new, `public static`, pure): `ChooseGrounding(AiWorldView view, AiUnitView caster, out FixVec2 target) -> int score` (supply). Candidate points: positions of visible enemy units with `Layer == Air` whose centre is within `GroundingAbility.RangeTiles` of the caster (squared). Score of a point: total supply of visible enemy units with `Layer == Air` whose centre is within `GroundingAbility.RadiusTiles + their definition's RadiusTiles` of it (squared; the Sim's test, combat_mechanics.md 4.2). Best score; ties to the lower candidate entity ID. No candidate: score 0.

**Rule CG.** In `Army`, immediately after `UnitControl(armyAll)` and before `StanceArmy` (so it runs before `MeasuredDefence` can return early): for each own unit, ascending ID, with `Definition.GroundingAbility != null`, not under construction, ability ready: if `ChooseGrounding(...) >= grounding_min_air_supply`, emit `CastGrounding(caster, target)`. At most `grounding_max_commands_per_think` per think. A cast does not add the unit to `_controlled` (casting does not change its orders).

**When it fires, in view terms:** an own Stormcaller is off cooldown and sees, within 7 tiles (its data), enemy flyers worth at least 3 supply that one 2-tile Downdraft would catch. Water has no flyer in shipped data, so Downdraft fires only against Skai.

**Variety.** Situational only: whether and where it fires depends on which flyers are seen and where. No pre-emptive casts (v4 Q-12; confirmed 22 Nov).

**No enemy flyer seen, no Downdraft (decided 22 Nov).** Rule CG needs a visible enemy flyer, so it never fires in a game where none has been seen; no change to the merged P1 code is needed. Stormcallers are not trained as support in such a game either (P2b); the counter-mix may still choose them on its own, as it does today.

**Tests** (`rts/tests/Sim.Tests/Ai/AiMechanicTests.cs`; real `greybox_1v1` simulations as in `AiFightTests` (flat at height 1 for x 14 to 33, y 15 to 19), units spawned by script, `AiBrain.Decide` on the view; profile `aggressive_s5_m` unless stated):
- **M1-1 Downdraft.** P0 Skai Stormcaller at (18.5, 17.5); P1 Roc Riders at (23.5, 17.5) and (24.5, 17.5). Decide emits `CastGrounding(stormcaller, (23.5, 17.5))` (both points score 6; tie to the lower ID). After the scheduled step: both Roc Riders have `EffectiveLayer` Ground and the `GroundingCastEvent` reports 2 units grounded. Next think: no cast (cooldown 30 s).
- **M1-2 Below the bar and out of range.** One Roc Rider at (23.5, 17.5) already grounded by a script `apply_status`, the other at (30.5, 17.5) (beyond range 7): no cast.
- **M1-3 Fog.** Roc Riders at (29.5, 17.5) and (30.5, 17.5), not visible to P0: no cast.
- **M1-4 Off.** M1-1 with `use_casts` false: no `CastGrounding`; intents equal those of `aggressive_s5_r` on the same view.
- **M1-5 Determinism.** M1-1 twice from scratch: identical intent lists.

## M2. The Tidecaller and its wave (`use_casts`, `use_support_units`)

### M2.1 Training support units (rule SU, `use_support_units`)

The Tidecaller is unarmed and never chosen by the counter-mix; the research-gated Stormcaller is chosen in 25 percent of Skai slot-games (R2.5) and Downdraft needs one. Rule SU trains them by army size.

In `Produce`, before the deficit loop: army supply A = supply of own complete army units, the active scout excluded. For each `support_units` entry (unit S, per-supply n) in ordinal ID order: `wanted = min(support_max_per_type, A / n)` (integer division); `have` = own S units plus S entries in own production queues. If `have < wanted`:
1. If S's first unmet requirement (`FirstUnmetRequirement`) is not null: skip S (RQ-1 below or rule T2 handles it).
2. `FindFreeProducer(S)` non-null, `CanPay` and `HasSupply`: emit `Train(producer, S)` and mark the producer used. With budget queues, S is paid from the `army` queue with its can-pay and want rules.
3. Producer free but not affordable: reserve S's cost under rule R (no budget queues) or record the `army` want, and stop `Produce`, as the deficit loop does.

**RQ-1 support extension** (only when `use_support_units` and `use_research`): RQ-1 step 1's list is the composition units as today, followed by each `support_units` entry with `wanted > have`, in ordinal ID order. Everything else in RQ-1 is unchanged.

M2.1 is built as written above (package P2). Revision 2 changes it only through M2.1b.

### M2.1b Support needs (package P2b, a follow-up to the merged P2 code)

The developer (22 Nov): train support units only when what the enemy is using calls for them. A small general mechanism, in data:

**Key.** Plan key `support_needs` (section 1): unit ID to a list of movement layer names. An entry of `support_units` whose ID is not a key of `support_needs`, or whose list is empty, is always needed (revision 1 behaviour).

**Helper** (`AiBrain`, `internal static`, pure): `SupportNeedMet(AiWorldView view, AiFactionPlan plan, AiMemory memory, string unitId) -> bool`: true when the unit has no list or an empty list; otherwise true when some key of `memory.EnemyTypes` (enemy army types sighted this match, kept even when their count has decayed to 0) names a definition in `view.Content.Units` whose `Layer` is in the list. Walks `EnemyTypes` in its ordinal key order and returns at the first match; no ordering choice affects the result.

**"Seen" latches for the match**: one sighting of a type keeps the need met for the rest of the match (Q-13). Implied types (from known enemy structures, `ModelCounts`) do not count; only units actually seen.

**Change to the P2 code.** Wherever P2 computes `wanted` for a `support_units` entry (rule SU and the RQ-1 support extension), `wanted` is 0 when `SupportNeedMet` is false. Nothing else in SU, ST or RQ-1 changes: support units already trained or queued stay and keep trailing (rule ST); the order, payment and reservation steps are untouched. If the P2 code computes `wanted` in more than one place, the builder routes every place through one private method so the condition is applied once.

**Order within the think.** `EnemyTypes` is updated in `UpdateIntel` at the start of `Decide`, before `Produce`, so a flyer seen this think already counts this think.

**Candidate values:** water `support_needs` `{}` (Tidecallers as revision 1: one per 12 army supply, max 3); skai `{ "skai.stormcaller": ["air"] }` (one per 20 army supply, max 3, only once an enemy flyer has been seen; with today's units that means a Skai opponent's Roc Rider or Thunderhead). Old profiles: `{}` in every plan.

**When it fires, in view terms:** as rule SU, plus: for a listed unit, the AI has at some point this match seen an enemy army unit on one of the listed layers.

**Tests** (`AiMechanicTests.cs`; greybox scenes as in M2.4; profile `aggressive_s5_m` with `use_support_units` true):
- **SN-1 No flyer seen.** M2-6's second scene (Skai, `skai.stormcaller_plans` in `OwnResearched`, idle complete Windspire, army 20 supply, money), `EnemyTypes` empty: Decide emits no `Train(_, skai.stormcaller)`.
- **SN-2 Flyer seen.** SN-1 plus a visible P1 `skai.roc_rider` at (23.5, 17.5): Decide emits `Train(windspire, skai.stormcaller)`. Then remove the Roc Rider (script `remove_unit`), clear the production queue and think again: `EnemyTypes` still holds `skai.roc_rider`, and the `Train` is emitted again (latch).
- **SN-3 Ground sighting does not count.** SN-1 plus a visible P1 `skai.talon_archer`: no `Train(_, skai.stormcaller)`.
- **SN-4 Research waits for the need.** M2-6's first scene (plans not researched, idle Storm Spire): with `EnemyTypes` empty, no `Train(storm_spire, skai.stormcaller_plans)` from the support extension; with a visible P1 Roc Rider, it is emitted.
- **SN-5 Unlisted unit unchanged.** M2-5 (water Tidecaller) passes unchanged with the candidate's water `support_needs` `{}` and no enemy seen.
- **SN-6 Validation.** `AiProfile.Parse` rejects, with file and key path in the message: a `support_needs` key that is not in the plan's `support_units`; a layer name `"flying"`; a repeated name; a non-empty list in a profile with `use_enemy_model` false.
- **SN-7 Off and determinism.** With every `support_needs` `{}`, the P2 tests M2-5 to M2-8 pass unchanged and the intents of SN-1's view equal those P2 gave before P2b. SN-2 twice from scratch: identical intent lists.

### M2.2 Support trail (rule ST, `use_support_units`)

Unarmed army units (`IsArmy && Definition.Weapon == null`) never get stance orders while the switch is on: each is added to `_controlled` every think. In stance Attack with a non-empty attack group: point P = mean position (Fix64) of the live armed attack-group units, moved `support_trail_tiles` toward Home (`MoveTowards`), then made standable (`Standable`); in any other stance, P = `RallyPoint()`. A unit farther than `support_regroup_tiles` from P (squared) gets `Move(P)` unless it already has a move target within `SameGoalSquared` of P. Fall back (D.6.1) still applies first. The Tidecaller is a flood source wherever it stands, so trailing the army is how the AI places its flood (no separate flood placement rule).

### M2.3 Casting the wave (rule CW, `use_casts`)

**Helpers** (`AiAbilities.cs`):
- `WaveTiles(int casterX, int casterY, int ux, int uy, int length, int width) -> SortedSet<int>`: the strip of flooding.md section 5: `s = 1..length`, `o = -(width - 1) / 2 .. (width - 1) / 2`, tile `(cx + s*ux - o*uy, cy + s*uy + o*ux)`, off-map tiles skipped, as tile indices.
- `ChooseWave(AiWorldView view, AiUnitView caster, bool meleeOnly, Fix64 meleeRange, out FixVec2 target) -> Fix64 score`: for each of the 8 directions in `TerrainGrid.NeighbourDx/Dy` order, score = total supply of visible enemy **army** units with `Layer != Air`, a weapon, and their tile in the strip; with `meleeOnly`, only those whose `Weapon.RangeTiles <= meleeRange`. Best score; ties to the lower direction index. `target = caster.Position + (4 ux, 4 uy)`: the Sim's `WaveDirection` maps it back to `(ux, uy)` (a straight offset has one axis more than twice the other; a diagonal has equal axes).

**Rule CW.** Same place as rule CG, after it: for each own unit, ascending ID, with `Definition.Flood.CanCastWave`, not under construction, wave ready: `score = ChooseWave(view, caster, memory.Variety.WaveAim == Melee, wave_melee_range_tiles, out target)`; if `score >= wave_min_enemy_supply`, emit `CastWave(caster, target)`. At most `wave_max_commands_per_think`. Not added to `_controlled`.

**When it fires, in view terms:** an own Tidecaller is off cooldown and at least 4 supply of visible enemy ground or water army (melee only, for the melee aim) stands in one of the eight 6 by 3 strips ahead of it.

**Variety.** The wave aim is drawn per match (section 6; confirmed 22 Nov): `melee` pushes only melee units (P-005: "answer a melee ball with a wave"); `all` also pushes ranged units off own melee units. Where and when it fires is situational (the trailing Tidecaller casts wherever the fight is, in attack and in defence).

### M2.4 Tests (`AiMechanicTests.cs`)

- **M2-1 Strip.** `WaveTiles(18, 17, 1, 0, 6, 3)` = x 19..24, y 16..18 (18 tiles). `WaveTiles(18, 17, 1, 1, 6, 3)` = the 18 tiles `(18 + s - o, 17 + s + o)`, s 1..6, o -1..1.
- **M2-2 Wave against a melee ball.** Melee aim forced (section 6 test hook). P0 water Tidecaller at (18.5, 17.5). P1 Pummelers (`water.grasper`) at (21.5, 17.5), (22.5, 17.5), (23.5, 18.5) and a Brinecaster (`water.ink_lobber`) at (18.5, 19.5). Decide: one `CastWave(tidecaller, (22.5, 17.5))` (direction (1, 0), score 6). After the scheduled step: `WaveCastEvent` direction (1, 0) and a `UnitPushedEvent` for each of the three Pummelers. With only the first Pummeler: score 2 < 4, no cast.
- **M2-3 Aim "all".** M2-2's scene with the Pummelers removed and Brinecasters at (18.5, 19.5), (18.5, 20.5), (17.5, 21.5): melee aim, no cast; all aim, `CastWave(tidecaller, (18.5, 21.5))` (direction (0, 1)).
- **M2-4 Cooldown.** After M2-2, the next think: no cast, and the Tidecaller's `WaveReadyTick` equals the cast tick + 600 (30 s at 20 ticks per second).
- **M2-5 Support training.** Water AI with a complete idle Tidepool, money, own complete army of 12 supply: Decide emits `Train(tidepool, water.tidecaller)` before any composition `Train`. With 11 army supply: none. With 3 Tidecallers and 60 supply: none (max 3). A Tidecaller already queued at the Tidepool counts as had.
- **M2-6 Support research.** Skai AI with `use_research` and `use_support_units` on, own army 20 supply, a complete Windspire, an idle complete Storm Spire, `skai.stormcaller` weight 0 in the composition, `skai.stormcaller_plans` not researched, money: Decide emits `Train(storm_spire, skai.stormcaller_plans)`. With the plans in `OwnResearched`: `Train(windspire, skai.stormcaller)`.
- **M2-7 Trail.** Stance Attack, attack group of 3 Pummelers around (30.5, 17.5), a Tidecaller at (18.5, 17.5): `Move(tidecaller, P)` with P computed by the M2.2 formula; no `AttackMove` for it. With `use_support_units` false: it gets the stance attack-move (today's behaviour).
- **M2-8 Off and determinism.** With `use_casts` and `use_support_units` false, M2-2 and M2-5 give the intents of `aggressive_s5_r` on the same views. M2-2 twice: identical intents.

## M3. Walls and gates (`use_wall_breach`, `use_walls`)

Two rules. WB answers enemy walls; WL walls the authored choke lines of the AI's main base and of the expansions it holds (revision 3; the developer, 22 Nov: "walling off the ramps and areas they need to protect during expansion", "Yes, use authored choke lines"). WB and WL are separate switches so WB can later be measured on its own (against humans and in a `wall_in` scenario), but the candidate needs both: an AI that seals its base against an opponent with no breach rule ends the game in a stalemate, because units whose goal is sealed off stop and do not resume (walls_and_tech.md W.1 rule 3, Q-W1 default).

### M3.1 Breaching a wall that seals the route (rule WB, `use_wall_breach`)

This is v4 A4, unchanged except for its place in the order.

**Memory.** `UnreachableGoals`: `SortedDictionary<EntityId, (int Tick, FixVec2 Goal)>`. `SkirmishAiController.ReadEvents` records each `MoveUnreachableEvent` (`Events/TerrainEvents.cs`) whose unit is an own live unit, overwriting, only when `use_wall_breach` is on. In rule 0, entries of dead units and entries older than `wall_breach_memory_seconds` are dropped.

**Rule WB.** In `Army`, after rules CG and CW, before `StanceArmy`: for each own army unit u, ascending ID, with an entry, not in `_controlled`, not fallen back, not in the harass group, whose weapon can target Ground. Units whose definition layer is Air are skipped:
1. Candidate walls: `KnownEnemyStructures` entries whose `DefinitionId` names a definition with `Economy.IsWall` (gates included). Choose the one minimising `|u - w| + |w - goal|` (`FixVec2.Length`, which is `Fix64.Sqrt` and deterministic), ties to the lower entity ID.
2. Chosen wall visible now (in `VisibleEnemies`): `AttackUnit(u, wall)`. Not visible: `AttackMove(u, Standable(w.Position.MoveTowards(u.Position, wall_breach_standoff_tiles)))`.
3. No known wall: `AttackMove(u, Standable(u.Position.MoveTowards(goal, wall_probe_step_tiles)))`, so the unit walks toward its goal until it sees what blocks it.
4. u joins `_controlled`. At most `wall_breach_max_commands_per_think`.

### M3.2 Walling along authored choke lines (rule WL, `use_walls`; revision 3)

Revision 2's computed rules are gone: the plateau plan (WI) walled 17 mouth tiles around the start plateau of `prototype_1v1` and the approach ring (WX) only narrowed the natural's approach with 8 tiles. The map was designed with causeways and ramps as chokes, so the lines are authored per map, the way a human would wall them, and checked by tests.

#### M3.2.1 Where the lines live, and why

**Chosen: one AI data file per map, `rts/data/ai/walls/<map id>.json`**, routed by the data loader into a new `DataSet.AiWallLines` exactly as `ai/<id>.json` profiles are routed into `DataSet.AiProfiles`, and validated by Rts.Ai.

Why not somewhere with no Sim change at all: there is no such place under `rts/data`. Every host (harness, ReplayCheck, MissionRunner, Unity `SimulationHost`, the test fixtures) passes every `*.json` under `rts/data` to `DataLoader`, which rejects a file outside its known folders ("is not under a known data folder") and any `ai/` file that is not `ai/<id>.json` (`CheckDepth`), and treats every `ai/<id>.json` as a profile (parsed by about ten tests and listed in the Unity skirmish setup). So a new AI file needs the loader to know it. The alternatives are worse:
- **A field of the map definition** (`maps/prototype_1v1.json`): needs `ReadMap` and `MapDefinition` changes too, and `Map.WriteHash` feeds `Simulation.ContentDigest` (`Simulation.cs`, content digest block), so hashing it moves every pin of a game on `prototype_1v1`: the `aggressive_s5` and `aggressive_s3` trace pins and any other fixed-seed game there; leaving it out of the hash would break the rule that the digest covers the map. Not worth it for data the simulation never reads.
- **A key in every profile that walls**: no Sim change, but map data copied into each walling profile (drift), and `AiProfile.Parse` sees only the content catalogue, not the map, so it cannot check tiles.

**P4-0, the loader route** (`rts/src/Sim/Data/DataLoader.cs`, `DataSet.cs`; load time only):
- In the `ai` folder case: a path of exactly three parts whose second part is `walls` (`ai/walls/<stem>.json`) is checked for the header and for `id` equal to the stem (`ReadId`, as for profiles) and stored as its parsed `JsonValue` in `AiWallLines`, keyed by `id`; every other `ai/` path keeps today's `CheckDepth(path, parts, 2, "ai/<id>.json")`. The loader does not check that the ID names a map (Rts.Ai does) and never reads the contents.
- `DataSet.AiWallLines`: `IReadOnlyDictionary<string, JsonValue>`, ordinal ID order (a `SortedDictionary` with `StringComparer.Ordinal`, as `AiProfiles`), empty when the folder is absent. New constructor overload with the extra dictionary; the existing constructors pass an empty one, so hand-built `DataSet`s in tests compile unchanged.
- Not written to any hasher; the simulation never reads it. OFF-1 (every pin unchanged) and WD-4 prove it.

**Rts.Ai side.** New `rts/src/Ai/AiWallLines.cs`: `AiWallLines.Parse(JsonValue root, DataSet data) -> AiMapWalls` validates one file (rules below) against `data.Maps[id]`, `data.Content` and `data.Tuning.Economy.WallLineMaxTiles`. `AiProfile.FromDataSet(data, id)` parses every entry of `data.AiWallLines` (ordinal order) and attaches them as `AiProfile.WallLines` (`IReadOnlyDictionary<string, AiMapWalls>`, map ID to lines); profiles made by `AiProfile.Parse` from text get an empty dictionary; `WithForcedOpenings` (used by the harness pool) and any other copy carry `WallLines` over unchanged. Every host already builds controllers from `FromDataSet`, so no host changes (Unity included). An invalid wall file therefore fails loudly when any profile is loaded, naming the file and key path.

#### M3.2.2 The file format

Root: `$schema_version` 1, `$doc`, `id` (the map ID, equal to the file stem), `areas` (list, 0 to 32). Each area:

| Key | Meaning | Unit | Range |
| --- | --- | --- | --- |
| `id` | area ID | text ID | unique in the file; lower-case letters, digits, underscores |
| `kind` | `main` (walled before expansions, never counted against `expansion_wall_max_bases`) or `expansion` | enum | `main`, `expansion` |
| `slot` | start slot that owns the area; its lines keep out every other start. -1: neutral, any player may wall it, sealed against every start | spawn index | -1 to spawn count - 1 |
| `fields` | resource-field indices that identify the area (how a depot is matched to it) | field indices | 1 or more, valid for the map, no index in two areas of the file |
| `anchors` | tiles that must lie inside the seal besides the fields' neighbours: the depot site the AI picks for the area on an empty map (`TryFindDepotSite`; one per approach side for neutral areas) | tiles | 1 to 4, on the map |
| `covers` | areas of the same file that lie inside this area's seal; while this area is wanted, they are not | area IDs | 0 or more, not itself |
| `lines` | straight lines: `from`, `to` (tiles; the line's tiles are `WallLine.Rasterise(from, to, wall_line_max_tiles)`, so `from` = `to` is one tile), optional `gate` (a tile of that line) | tiles | 1 or more lines; each line 1 to `wall_line_max_tiles` tiles, every tile on the map and not a resource-field tile; at least one `gate` per area |

`Parse` rejects (error names file and key path): unknown keys (`RejectUnknownKeys` at every level), an `id` that is not a loaded map, duplicate area IDs, a bad `kind` or `slot`, a field index out of range or listed twice in the file, an anchor off the map, a `covers` ID not in the file or naming itself, a line longer than `wall_line_max_tiles`, a tile off the map or on a resource field, a `gate` not on its line, an area without a gate. The seal, exit and mirror properties need a simulation and are checked by tests WD-2 and WD-3, not by `Parse`.

Pure helpers in `AiWallLines.cs`: `Tiles(line)` (rasterised, cached at parse), `AllTiles(area)` (lines in file order, tiles in line order, duplicates dropped, first kept).

**The checkable property (seal).** For an area A with tile set T (all line tiles, gates included), target set G = A's anchors, plus every tile among the 8 neighbours of each listed field tile that has the field tile's height, plus the slot's spawn tile when `slot` ≥ 0; enemy starts E = every spawn except `slot` (every spawn when `slot` is -1). With a wall on every tile of T and a gate on every gate tile: no tile of G is reachable by ground from any tile of E under `TerrainGrid.IsStepPassable(from, to, MovementLayer.Ground, TeamId.None)` (every gate counts as a wall). Also: **exit**, for the owner's team (gates passable) every tile of G reaches some tile of E; **covers**, every target of every covered area is unreachable from E with A's lines built; **not vacuous**, on the bare map every tile of G is reachable from E.

#### M3.2.3 The authored lines of `prototype_1v1`

File: `rts/data/ai/walls/prototype_1v1.json` (in this commit). Slot 0 and the north-west neutral areas were authored; slot 1 and the south-east neutral areas are their point mirrors `(x, y) -> (95 - x, 95 - y)`. Costs at today's prices (walls 25 common, gates 50 common, both factions); `funnel` with the candidate gap 0 = all tiles but the gate tile, as walls.

| Area | Slot | Fields | Lines (`from`-`to`; gate) | Tiles | Sealed | Funnel |
| --- | --- | --- | --- | --- | --- | --- |
| `main_0` | 0 | 0, 1 | north ramp (4,21)-(7,21); east ramp (22,7)-(22,10), gate (22,8) | 8 | 225 | 175 |
| `natural_0` (covers `main_0`) | 0 | 2, 3, 16 | north-west corridor (8,27)-(14,27); north of the natural (24,25)-(27,25); basin ramp (33,15)-(36,15) and (33,16)-(33,17), gate (33,16); knoll gap (46,3) | 18 | 475 | 425 |
| `basin_0` (covers `main_0`, `natural_0`) | 0 | 7, 18 | (8,27)-(14,27); pocket gap (25,30); west of the basin (29,31)-(33,31); causeway ramps (45,13)-(45,14) and (45,22)-(45,24), gate (45,23); (46,3) | 19 | 500 | 450 |
| `main_1` | 1 | 8, 9 | mirror of `main_0`, gate (73,87) | 8 | 225 | 175 |
| `natural_1` (covers `main_1`) | 1 | 10, 11, 19 | mirror of `natural_0`, gate (62,79) | 18 | 475 | 425 |
| `basin_1` (covers `main_1`, `natural_1`) | 1 | 15, 21 | mirror of `basin_0`, gate (50,72) | 19 | 500 | 450 |
| `flank_w` | -1 | 4, 17 | (7,66)-(7,68); (14,59)-(21,59); (19,71)-(22,71); (22,68)-(22,70); (23,68)-(24,68); (24,67), gate (24,67) | 21 | 550 | 500 |
| `flank_e` | -1 | 12, 20 | mirror of `flank_w`, gate (71,28) | 21 | 550 | 500 |
| `corner_nw` | -1 | 5, 6 | (7,66)-(7,68); (18,73)-(21,73); (20,77)-(20,80), gate (20,78); (25,85)-(25,89) | 16 | 425 | 375 |
| `corner_se` | -1 | 13, 14 | mirror of `corner_nw`, gate (75,17) | 16 | 425 | 375 |

Every area seals, none needs more than 21 tiles (the authoring limit was 24), and the areas cover all 22 fields of the map, so every site the expansion logic can take (`Expand` walks `TargetOrder` over every field with known resources: from slot 0 the natural fields at 14.6 to 18.0 tiles, the basin at 28.6 to 29.7, the west flank at 54.3 to 54.9, then the far sides and corners) lies in an area. Areas of the other slot (for example slot 1's natural taken by slot 0) are never walled by the taker. Slot-0 progression in style `sealed`: main 225 if it is walled before the natural is held; once the natural is held, its ring (475) replaces the main's lines (they are covered); once the basin is held, its ring adds 11 tiles (8 are shared with the natural ring): 300. Total 1000 common, or 775 when the natural depot comes before `wall_earliest_seconds` (the main is then covered before its first wall).

How the lines were found and checked (scratch script, not committed; the same check is test WD-2 in the simulation's own code): ground steps by the rule of `TerrainGrid.IsStepPassable` on public heights and ramps (equal heights, or one layer apart with a ramp on either tile; a diagonal needs its four orthogonal edges; line and gate tiles closed). Candidate lines came from a minimum vertex cut (max-flow, one unit per tile) between the area's targets and the enemy start, with the cut held within a Chebyshev radius of the area, and were then read against `rts/docs/art/maps/prototype_1v1_layout.png`: the cuts are the two plateau ramps, the corridors and causeway ramps, and the one-tile gap between the south knoll and the rim. For each area: seal from the enemy start(s) holds, exit through the gate holds, covers hold, no tile is rock or a field tile, the nearest wall tile is at least 2 tiles (Chebyshev) from any field and outside every anchor's 3 by 3 site, and the slot-1 and south-east sets are the exact mirrors.

Anchors: `main_0` (10,10); `natural_0` (27,13); `basin_0` (37,20) and (43,20) (the sites `TryFindDepotSite` picks scanning from each side); `flank_w` (20,63) and (20,69); `corner_nw` (12,81) and (14,84); slot 1 and south-east are mirrors.

#### M3.2.4 Rule WL

**Wall and gate definitions**: the AI faction's unit definitions with `Economy.IsWall && !Economy.IsGate` and with `Economy.IsGate`, each the ordinally lowest ID (shipped: `skai.wind_baffle` / `skai.wind_gate`, `water.coral_wall` / `water.coral_gate`). None, or requirements unmet (`RequirementsMet`): rule WL does nothing.

**Place.** In `Decide`, after `StaticDefence()` and before `Expand()` (revision 2's place of rule WI).

**Fires only when all hold** (checked once per think; otherwise nothing):
1. `_profile.WallLines` has an entry for `view.MapId`. No entry: rule WL is skipped on that map. There is no computed fallback (revision 3: one mechanism; the computed plateau rule is removed, Q-14).
2. The drawn wall style (section 6) is not `none`.
3. `view.Tick >= SecondsToTicks(wall_earliest_seconds)`.
4. Enemy ground share: with `use_enemy_model`, the supply share of non-Air types in the enemy model is at least `wall_min_enemy_ground_share`, or the model is empty; without the enemy model, always true. (Walls do not stop flyers.)
5. `memory.WallSpentCommon < wall_max_spend_common`.

**Held areas** (`AiWallLines.HeldAreas(AiWorldView view, AiMapWalls walls, int ownSpawn, Fix64 reachSquared) -> List<AiWallArea>`, pure; `reachSquared` = `field_max_distance_tiles` squared, the brain's `ReachSquared`):
1. Usable areas: `slot == ownSpawn` (the brain passes `memory.OwnSpawnIndex`) or `slot == -1`.
2. Holders, in this order: (a) own units in ascending entity ID that are an HQ (`Definition.Hq != null`) with `HqState == Rooted`, or a structure with `Economy.IsDropOff` (complete or under construction); holder tile = the tile of its position; (b) own workers in ascending entity ID with an active build order whose `BuildDefinitionId` names a definition with `Economy.IsDropOff`; holder tile = (`BuildTileX`, `BuildTileY`).
3. A holder's area: among the fields listed by usable areas, the one whose centre is nearest the holder tile's centre (squared, Fix64) and within `reachSquared`; ties to the lower field index. The area listing that field is the holder's area. No such field: the holder holds nothing.
4. Held list: areas in the order of their first holder, each once.
5. Cap: keep every `main` area and the first `expansion_wall_max_bases` `expansion` areas of the held list; drop the others.
6. Covers: drop every kept area named in the `covers` of another kept area.
7. Order: kept `main` areas first, then kept `expansion` areas, each group in held-list order.

**Wanted tiles of an area** for the drawn style, line by line in file order, tiles in line order:
- `sealed`: every tile; the gate tile gets the gate definition, the others the wall definition.
- `funnel`: a line with a gate tile: its tiles at Chebyshev distance greater than `wall_funnel_gap_tiles` from the gate tile get walls, the others stay open, no gate. A line without a gate tile: every tile gets a wall. (A funnel counts as wall use, decided 22 Nov.)

A wanted tile is **missing** when: no own unit with `IsWall` stands on it (complete or under construction); it is not an own worker's active build tile (`BuildTileX/Y`) nor in an own worker's `BuildQueue`; it is not in `_reservedSites`; it is not in `WallTilesRefused`; and `!IsBlocked` (a structure already there closes the tile).

**For each area in order** (the rule stops for this think once `wall_max_lines_per_think` lines have been emitted; a line's gate step still runs after its walls, and a gate does not count as a line):
1. **Under attack.** Skip this area this think when a visible enemy army unit is within `wall_threat_tiles` (squared) of the centre of any of its anchors or line tiles (walls are not built under fire; the next area is still tried).
2. **For each line** of the area in file order:
   - **Walls**: the line's missing wall tiles in line order (at most `wall_line_max_tiles`, by the data rule, so one command). If none, go to the gate. n = the largest count, from the start, that free money pays (no budget queues: `common - reserveCommon >= n x cost`, same for rare; with budget queues: the `defence` queue's account, through `DefenceAllows` and `BqCanPay` per tile) and that keeps `WallSpentCommon + n x cost common <= wall_max_spend_common`. n = 0: **stop rule WL for this think without reserving** (walls are bought from spare money, never ahead of the army or a depot; with budget queues no want is recorded). Otherwise the builder is `PickBuilder(centre of the first tile)`, not a worker picked this think; none: stop. If the first tile was never explored, the builder is sent there (`Move`), as `TryBuild` does, it joins `_pickedBuilders`, the line waits and counts as one emitted line. Otherwise emit `BuildWallLine(builder, wallId, first n tiles)`, charge n x cost through the same ledger as `Pay` (the `defence` queue with budget queues), add n x cost common to `WallSpentCommon`, add the builder to `_pickedBuilders` and the tiles to `_reservedSites`.
   - **Gate** (style `sealed` only): when the line has a gate tile and it is missing: if free money pays the gate (same test as one wall tile) and `WallSpentCommon + gate cost common <= wall_max_spend_common`, call `TryBuild(gate definition, gate tile, false, AiBudget.Defence)` and, when it emitted a `Build`, add the gate's common cost to `WallSpentCommon`; otherwise stop rule WL for this think (no reservation, since `TryBuild` is called only when the gate is affordable now).

Tiles shared by two wanted areas, or ordered earlier this think, are in `_reservedSites` and are ordered once. `WallSpentCommon` counts common at order time and is never reduced by refunds of skipped tiles (conservative; the cap only stops more spending).

**Refused tiles.** `ReadEvents` (only when `use_walls`) adds the tile index of each own `BuildLineTileSkippedEvent` (`Events/EconomyEvents.cs`) with reason `out_of_bounds`, `tile_blocked` or `on_resource_field` to `WallTilesRefused`, and of each own `CommandRejectedEvent` of a `build_structure` of a gate with reason `tile_blocked` or `on_resource_field`. Other reasons (a unit in the way, money, `site_unexplored`) are retried by later thinks. A tile freed later (an own wall killed) is missing again and is rebuilt while its area is wanted and the cap allows, so the walls heal.

**Refusals and occupied gate tiles (changed after the P5 measurement).** A gate is not ordered while a ground unit the AI can see (its own or an enemy's) stands on the gate tile; the gate waits for a later think. A `build_wall_line` the simulation refuses whole as `tile_blocked` or `on_resource_field` (every tile filtered, so no skipped-tile event follows) puts the first tile of the order into `WallTilesRefused`, so it is not re-sent; a wall the enemy built on a tile the AI cannot see is only learnt from this one refusal, and the AI does not look at it.

**When it is skipped, in short:** switch off; no lines for this map; style `none`; before `wall_earliest_seconds`; enemy seen mostly air; spending cap reached; an area not held, held by the other slot, beyond `expansion_wall_max_bases`, or covered by a wanted area; enemy army near that area; no affordable tile (nothing reserved, walls never delay the army or a depot); no free builder. Lines of an area that stops being wanted stay standing; their missing tiles are not rebuilt.

**When it fires, in view terms:** the map has authored lines; the AI's HQ is Rooted in its main (or it holds or is building a depot at an expansion of its own side or a neutral one); three minutes have passed; the enemy seen so far is mostly ground; no enemy army is near the area's lines; there is spare common within the match cap. Main first; once the natural (or the basin) is held, the wider ring that closes main and natural together replaces the main's ramp lines.

**Interactions.** Own units pass own gates (team rule, walls_and_tech.md G.1 rule 3) in style `sealed` and the open gate tile in style `funnel`; every area has a gate tile, so no own unit is shut in. Water walls also stop water's own flood at the lines (W.1 rule 6, Q-W8 default; accepted 22 Nov, no special rule). Existing test W-14 ("no `build_structure` names a wall over `ai_vs_ai.json`") still holds: that script uses profiles with `use_walls` false, and `greybox_1v1` has no wall file.

**Removed with revision 2's rules:** `AiWalls.cs` (`Plan`, `PublicStep`, `StepDistances`, `ApproachPlan`), `AiWallPlan`, memory `WallPlan`, `EnemySpawnSteps`, `ExpansionWallPlans`, and the keys listed in the change list. P4 is not built, so nothing existing is deleted.

### M3.3 Harness accounting for wall lines (build first within P4)

`SummaryObserver.AfterStep` and `GameTrace` charge only accepted `TrainUnitCommand` and `BuildStructureCommand`, so an AI wall line would be built but not counted as spent. Change both call sites: an accepted `BuildWallLineCommand` of an AI is charged the cost carried by the `BuildOrderAcceptedEvent` of the same worker in the same step (that event carries cost x tiles kept, walls_and_tech.md D.2 rule 4) to the `wall` class, through a new `PlayerLedger.SpendAmount(string spendClass, int common, int rare)`. Refunds are not subtracted (the same rule as for structures today).

### M3.4 Tests (`AiMechanicTests.cs` for rules WB and WL, `AiWallLineDataTests.cs` for the data)

Breach (greybox; P0 Talon Marksman at (18.5, 17.5) with `UnreachableGoals` = (tick - 1 s, (40.5, 17.5)) unless stated):
- **WB-1 Visible wall.** P1 `water.coral_wall` at tile (22, 17), visible to P0: `AttackUnit(talon, wall)`; no stance order for the talon this think.
- **WB-2 Remembered wall.** The wall at (30, 17) in `KnownEnemyStructures`, not visible: `AttackMove(talon, (28.5, 17.5))`.
- **WB-3 No known wall.** `AttackMove(talon, (24.5, 17.5))`.
- **WB-4 Old entry.** Entry at tick - 11 s: no WB order.
- **WB-5 Flyer.** A Roc Rider with an entry: no WB order.
- **WB-6 Recording.** A P0 unit inside a ring of eight P1 walls around tile (18, 17) is ordered out by script; after the next think `UnreachableGoals` holds the unit with that goal. With `use_wall_breach` false it stays empty.

Wall-line data (`rts/tests/Sim.Tests/Ai/AiWallLineDataTests.cs`; every map file in `DataSet.AiWallLines` loaded from `rts/data`, so a new map file is covered without a new test):
- **WD-1 Parse and validation.** Every entry of `AiWallLines` parses with `AiWallLines.Parse` and its ID is a loaded map. `prototype_1v1` has 10 areas with the IDs, slots, kinds, fields, gates and tile counts of the M3.2.3 table. `AiProfile.FromDataSet(data, "aggressive_s5_m").WallLines` holds `prototype_1v1`, and so does its `WithForcedOpenings` copy. Hand-written files are rejected, the message naming file and key path, for: an unknown key at root, area and line level; an `id` that is no map; a duplicate area ID; `kind` `"base"`; `slot` 2 on a two-spawn map; field index 22 on `prototype_1v1`; a field listed by two areas; a `covers` ID not in the file, and one naming its own area; a line of 17 tiles (with `wall_line_max_tiles` 16); a line tile off the map; a line tile on a resource field (for example (30,13)); a `gate` not on its line; an area with no gate.
- **WD-2 Seal, in the simulation's own path code.** For every file and every area: start a simulation on that map with one player per spawn (factions alternating water, Skai; teams one per player), and in one step spawn by script (`SpawnUnitCommand`, as `WallTests` does) a complete wall of player P on every non-gate tile of the area and a gate of player P on every gate tile, where P is the area's slot player (player 0 for a neutral area). Targets G and enemy starts E as defined in M3.2.2 (spawn tiles from `MapDefinition` spawns). Then, with `sim.Pathing.Get(MovementLayer.Ground, goal, team)`:
  1. Seal: for every t in G and every e in E, `Get(Ground, t, TeamId.None).IsReachable(e)` is false.
  2. Exit: for every t in G, `Get(Ground, e, P's team).IsReachable(t)` is true for at least one e in E (own units leave through the gate).
  3. Covers: for every covered area C and every target t of C, `Get(Ground, t, TeamId.None).IsReachable(e)` is false for every e in E.
  4. Not vacuous: on a second, wall-free simulation of the map, every t in G is reachable from every e in E.
  The test writes per area the tile, wall and gate counts and the sealed and funnel costs from the unit definitions (`TestContext.WriteLine`), and for `prototype_1v1` asserts them equal to the M3.2.3 table (8, 18, 19, 8, 18, 19, 21, 21, 16, 16 tiles; one gate each).
- **WD-3 Mirror.** For every file whose map is point-symmetric (every tile's height and ramp flag equal those of `(W - 1 - x, H - 1 - y)`, the spawn list and the resource-field list each map onto themselves under that mirror; the field pairing is by position), every area A has exactly one partner B, found as the area whose field set is the mirrored field set of A, with: the same `kind`; `slot` swapped to the mirrored spawn's index, or both -1; anchor sets mirrored; the union of rasterised line tiles mirrored as sets; gate tile sets mirrored; `covers` mapped to the partners of A's covered areas. For `prototype_1v1` the test also asserts the map is point-symmetric and the pairs are `main_0`/`main_1`, `natural_0`/`natural_1`, `basin_0`/`basin_1`, `flank_w`/`flank_e`, `corner_nw`/`corner_se`.
- **WD-4 Loader route and digest (P4-0).** Loading `rts/data` puts `prototype_1v1` in `DataSet.AiWallLines` and not in `AiProfiles`; a file `ai/other/x.json` is still rejected as in the wrong place; a wall file whose `id` differs from its stem is rejected. A simulation built from a `DataSet` with the wall files removed has the same `ContentDigest` and the same state hash after 200 ticks of `smoke.json` as one built with them.

Rule WL (`AiMechanicTests.cs`; real `prototype_1v1` simulation, slot 0 water unless stated, Rooted HQ at its spawn, workers, money granted, tick past 180 s, style forced by the section 6 test hook, every line tile of the areas under test explored (a scripted own unit stands beside each line for one step before the think), profile `aggressive_s5_m` with `use_walls` true; tiles listed in line order):
- **WL-1 Main, sealed.** Decide emits `BuildWallLine(_, water.coral_wall, [(4,21), (5,21), (6,21), (7,21)])` by the free worker nearest (4,21), then `BuildWallLine(_, water.coral_wall, [(22,7), (22,9), (22,10)])` by the free worker nearest (22,7), then `Build(water.coral_gate, (22,8))`. The next think, with those orders pending: no new wall or gate intent. Run again with slot 0 Skai: `skai.wind_baffle` and `skai.wind_gate` (found by flags). Run as slot 1: the mirrored tiles, gate (73,87).
- **WL-2 Funnel.** WL-1 with style `funnel`: lines [(4,21) .. (7,21)] and [(22,7), (22,9), (22,10)]; no gate intent; (22,8) stays open. With `wall_funnel_gap_tiles` 1: the second line is [(22,10)].
- **WL-3 Natural held.** WL-1's scene with an own complete drop-off depot at (27,13) (script) and `expansion_wall_max_bases` 2: no intent names a `main_0` tile (covered). First think: `BuildWallLine` [(8,27) .. (14,27)] (7 tiles) and [(24,25) .. (27,25)] (2 lines, the per-think cap). With those pending, the next think: [(33,15) .. (36,15)], and [(33,17)] then `Build(water.coral_gate, (33,16))`. The think after: [(46,3)]. Then nothing.
- **WL-4 About to expand.** WL-3 without the depot but with an own worker holding an accepted build order of the drop-off at (27,13): the same first-think intents.
- **WL-5 Basin covers.** WL-3 plus an own complete depot at (37,20): only `basin_0` lines are wanted; first think [(8,27) .. (14,27)] and [(25,30)].
- **WL-6 Cap and order.** WL-3 with `expansion_wall_max_bases` 0: the main's WL-1 intents (the natural is dropped by the cap, so it covers nothing). WL-1 with own depots at (27,13) and at P1's natural site (68,82): the (68,82) depot holds nothing (slot-1 area); the intents equal WL-3's.
- **WL-7 Skips.** No WL intent when: `use_walls` false; style `none`; tick before 180 s; the enemy model holds only Roc Riders (share 0); a visible P1 army unit at (24.5, 9.5) (2 tiles from the main's east line; and in WL-3's scene, 5 tiles from the natural's anchor (27,13), so the natural is skipped too); free common below 25, and then the reservation totals are unchanged; `WallSpentCommon` equal to `wall_max_spend_common`; the same scene on `greybox_1v1` (no wall file: no intent and no exception).
- **WL-8 Line payment.** No budget queues, common 100, walls 25: the first line holds the 4 north-ramp tiles; the second line is not emitted (n = 0 stops the rule), no reservation is made. With budget queues on: the `defence` queue pays and no want is recorded.
- **WL-9 Spending cap.** `wall_max_spend_common` 100: WL-1 emits only the first line (4 tiles, `WallSpentCommon` 100); every later think emits no WL intent, even after a wall is removed.
- **WL-10 Rebuild and refused.** After WL-1's walls are built, a script `remove_unit` of the wall at (5,21): the next think emits `BuildWallLine` [(5,21)]. A `BuildLineTileSkippedEvent` of an own line with reason `on_resource_field` for (6,21): the tile is in `WallTilesRefused` and is never ordered again.
- **WL-11 Unexplored.** WL-3 with (8,27) to (14,27) unexplored: the first think emits `Move(worker, centre of (8,27))` for the nearest free worker and `BuildWallLine` [(24,25) .. (27,25)]; no `BuildWallLine` names (8,27).
- **WL-12 Harness.** In a game where an AI line of 3 Coral Walls is accepted, the slot's ledger `wall` class holds 75 common; `BalanceReport` counts `water/wall` for the slot once a wall completes.
- **WL-13 Exit game.** A copy of `ai_vs_ai.json` on `prototype_1v1` with both profiles `aggressive_s5_m` (all four switches on) runs twice with identical hashes at every hash tick and zero AI command rejections other than `site_occupied`; whether it is decided, and which areas were walled, is reported, not asserted.
- **WL-14 Off and determinism.** With `use_walls` and `use_wall_breach` false: no `BuildWallLine`, `UnreachableGoals` and `WallTilesRefused` stay empty, `WallSpentCommon` stays 0, and the intents equal those of `aggressive_s5_r` on WL-1's view. WL-3 twice from scratch: identical intent lists. Old profiles have `use_walls` false, `expansion_wall_max_bases` 0 and `wall_max_spend_common` 0.

## M4. Wind Baffle

The Wind Baffle (`skai.wind_baffle`, `is_wall`, not a gate) is the Skai wall. Its unit row in `balance_use` and the `skai/wall` mechanic row count the same thing (a completed non-gate Skai wall), so rule WL is its rule; there is nothing Skai-specific to add (walls_and_tech.md Q-W6 default: identical rules). Its test is WL-1 run with slot 0 as Skai: the `BuildWallLine` names `skai.wind_baffle` and the gate `Build` names `skai.wind_gate`, found by flags, not by name.

## M5. HQ Last Stand

**No AI rule, by design.** Last Stand is passive: `HqSystem.AfterDamage` triggers it when a hit leaves an HQ at or below `last_stand_health_fraction` of its health, and only when `tuning/hq.json` `last_stand_enabled` is true. It is false in shipped tuning ("Switch that enables Last Stand for the prototype test"), so no player, human or AI, can use it, and no AI decision changes that. Turning it on is a game-rule change that moves every pin; it is not the AI's to make (the developer kept it off, 22 Nov). The two `hq_last_stand` rows are **reported, not judged** in section 7.

## 6. Variety: how these rules avoid one fixed pattern

The developer requires that the AI "have a variety of tactics". These rules add variety in three ways, using only AI decision keys (no unit or combat number):
1. **Two per-match style draws** from the variety stream (`AiVariety`, already seeded per match and player). At the end of `AiVariety.Create`, after the existing draws and in this order: when `use_walls`, the wall style (`none`, `funnel`, `sealed`) by `wall_style_weights`; when `use_casts`, the wave aim (`melee`, `all`) by `wave_aim_weights`. Each draw is one `rng.NextInt(sum)` mapped through the cumulative weights in the key order listed (the same method as the opening draw). `AiVariety.IsNeeded` also returns true when either switch is on. Stored as `AiVariety.WallStyle` and `AiVariety.WaveAim`; without a variety stream (both switches off) they read `none` and `melee`. A test hook (as `AiVariety.ForTest`) sets them directly. With the candidate's weights: one game in five does not wall, three in ten leave a funnel, half seal with a gate; half the Tidecallers push only melee, half push everything. The one wall style applies to every area rule WL walls in that match.
2. **Situational triggers.** Every rule reacts to what the view shows: walls only against an enemy seen to be mostly ground, only on the authored lines of areas the AI holds (main first, then wider rings as it expands), only from spare money within a match cap, not under fire; Downdraft only at flyer clumps; the wave only at clumps of the chosen kind; support units in proportion to the army and only when the enemy fields what they answer; breaching only when an own order was sealed off. The same profile therefore plays differently against different openings and factions.
3. **Combination with the existing openings** (package L: each plan draws one of its openings) and the composition jitter: the style draws are independent of them, so the number of distinct starts multiplies.

Draws happen only when their switch is on, so the old profiles' streams are unchanged (OFF-2). In the candidate, the two extra creation draws shift its later attack-advantage offsets relative to `aggressive_s5_r` (accepted 22 Nov). Revisions 2 and 3 add no draw: rule WL uses the same wall style, and P2b is decided by sightings.

**Tests.**
- **V-1 Draw reproducibility.** `AiVariety.Create` for `aggressive_s5_m`, the same seed twice: equal `WallStyle`, `WaveAim`, opening and offsets.
- **V-2 Distribution.** Over seeds 1 to 1000 (player 0, water plan), `WallStyle` counts are within 50 of 200, 300 and 500, and `WaveAim` counts within 50 of 500 each. (The counts are deterministic; the builder pins the exact values in the test comment.)

## 7. Off path and measurement

### 7.1 Off path (tests)
- **OFF-1 Pins.** Every pin of section 0 is unchanged; no test is re-pinned.
- **OFF-2 Old streams.** For `aggressive_s5_r` (variety stream present, new switches false): the sequence of `AiVariety` draws over a fixed traced game equals the sequence before this spec (same opening, offsets and advantage offsets). For a fixed view, the think intents of `aggressive_s5_r` and of a copy with all four switches explicitly false and every other new key at its candidate value are equal.

### 7.2 Measurement (restated in revision 3, before any run; nothing is tuned after a result)

No run has been made under revision 1 or 2. Run after P2b, P4 (with `rts/data/ai/walls/prototype_1v1.json` as committed in revision 3) and the harness change of 7.3, from the Release build of the branch, with `aggressive_s5_m` holding all four switches true and the revision-3 candidate values of section 1: `--balance --profile aggressive_s5_m --maps prototype_1v1` with the defaults of `ai_eval.json` (base seed 20261121, 72 games per matchup, three matchups), `--fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl`, and one `--trace` per matchup with the candidate in both seats; output under `rts/docs/reports/ai_m/`. Map `prototype_1v1` only. No ladder and no win-rate bar: the developer has ruled that AI-against-AI win rates are not read as balance.

Bars (each is the existing `balance_use` share, 0.100; revision 3 changes no bar):
1. Each of these 11 items reaches a use share of at least 0.100: `water/wave`, `water.tidecaller`, `skai/wall`, `water/wall`, `skai/gate`, `water/gate`, `skai.wind_baffle`, `water.coral_wall`, `skai.wind_gate`, `water.coral_gate` over their faction's slot-games (as revision 1), and **`skai/downdraft` over the Skai slot-games whose opponent fielded a flyer** (trained or completed at least one unit whose definition's layer is air; with today's data, the Skai-against-Skai games), counted by the harness change of 7.3. Changed in revision 2 (developer, 22 Nov: do not judge Downdraft over games it is not useful in).
2. Unchanged: every item at or above 0.100 in `reports/ai_r/balance/balance.md` (the 44 that passed there) is still at or above 0.100. This includes `skai.stormcaller`: P2b only adds support Stormcallers, it never removes the counter-mix's.
3. Unchanged: in each traced game, no AI command of kind `cast_wave`, `cast_grounding` or `build_wall_line` is rejected, and no AI `build_structure` of a gate is rejected other than `site_occupied`. This covers every line and gate of rule WL.

Reported, not judged: the two `hq_last_stand` rows (M5); games reaching `max_game_seconds` and the median end tick, against `reports/ai_r`; the share of slot-games per drawn wall style and wave aim; from the traces, per area kind (`main`, `expansion` of the own slot, neutral `expansion`) the share of slot-games in which a `build_wall_line` or gate `build_structure` named a tile of such an area, and per area of `prototype_1v1` the share of slot-games in which every wanted tile of the drawn style stood at some tick; median `WallSpentCommon` at match end (or the common charged to the ledger's `wall` class) and the share of slot-games that reached `wall_max_spend_common`; in Skai slot-games, the Stormcallers trained with and without an enemy flyer on the field; median time of the first use of each bar-1 item; `balance_predictor` and `balance_openings` as printed. Unspent rare is not read (developer, 22 Nov).

**No number is tuned after a result.** A failing bar is reported with its value. Any change of a rule or a key value is a new revision of this spec, with its bars restated before the next run.

### 7.3 Harness: judging a mechanic only where it can be used (P5, before the run)

`BalanceReport.MechanicUse` counts a slot-game for a detector when `detector.Applicable(content, faction, mapHasCore)` holds. Add an optional opponent condition to `MechanicDetector` (`rts/tools/BalanceHarness/MechanicDetectors.cs`): a nullable `MovementLayer? OpponentLayer`, null for every detector except `downdraft`, which gets `Air`. In `MechanicUse`, a slot-game counts for a detector with `OpponentLayer` set only when the **other** slot's `GameSummary` has, in `Trained` or `Completed`, at least one definition ID with count at least 1 whose `UnitDefinition.Layer` equals it (ordinal ID walk; unknown IDs ignored). A row with 0 slot-games stays unjudged, as today. This is cheap: both summaries are already in each `GameRecord`. The change alters only the `skai/downdraft` row's denominator in new reports; old reports are not re-rendered.

Tests (`rts/tests/Sim.Tests/Harness/`):
- **H-1.** Two hand-made records on `prototype_1v1`. Record A, Skai against Skai: the slot-1 summary trained one `skai.roc_rider`; the slot-0 summary trained no flyer and counts one `downdraft`. Record B, Skai against Water, no flyer anywhere. The `skai/downdraft` row has 1 slot-game (record A slot 0; record A slot 1's opponent fielded no flyer; record B's opponent is Water) and share 1.000. With the Roc Rider removed from record A, the row has 0 slot-games and is not judged.
- **H-2.** Every other detector's rows over the same records are equal before and after the change.

## 8. Build packages (cheapest first; one builder session each)

Each package: Sonnet `builder`, then the Opus `sim-reviewer` on the `rts/src/Ai` and harness diff; `dotnet build` with no warnings and `dotnet test` green, including OFF-1 and OFF-2. Every package adds its keys to every profile file (switch off) and to `aggressive_s5_m` (switch on), and updates the `AiBrain.cs` header (rule order), `AiProfile.cs` "Differences" list, `rts/src/Ai/README.md`, and the headers of the files it touches.

| Package | Contents | Tests | Depends on |
| --- | --- | --- | --- |
| **P1 Casts** (merged) | Section 2 view fields `WaveReadyTick`, `GroundingReadyTick`, `WaveLengthTiles`, `WaveWidthTiles`; intents 9 and 10 and the writer; `AiAbilities.cs`; rules CG and CW; `use_casts` keys; the wave-aim draw (section 6 part 1); creates `aggressive_s5_m` | M1-1 to M1-5, M2-1 to M2-4, M2-8 (cast half), V-1, V-2 (aim half), OFF-1, OFF-2 | nothing |
| **P2 Support units** (being built as revision 1) | Rules SU and ST, the RQ-1 extension, `use_support_units` keys and plan `support_units` with validation | M2-5 to M2-7, M2-8 (support half) | P1 (the candidate profile) |
| **P2b Support needs** (new) | M2.1b: plan key `support_needs` in every profile (with its comment line and validation), `SupportNeedMet`, `wanted` 0 when the need is unmet in rule SU and the RQ-1 extension; candidate values; a small change to the merged P2 code, not a rewrite | SN-1 to SN-7; P2's tests unchanged | P2 merged |
| **P3 Breach** | `UnreachableGoals`, the `ReadEvents` recording, rule WB, `use_wall_breach` keys | WB-1 to WB-6 | P1 |
| **P4-0 Wall-line route** (revision 3; the one `rts/src/Sim` change, load time only) | `DataLoader` routes `ai/walls/<id>.json` into `DataSet.AiWallLines` (M3.2.1); `DataSet` property and constructor overload; `rts/data/README.md` gains the `ai/walls/` bullet; headers of both Sim files. Merge before or together with this spec's data file (today's loader rejects it) | WD-4 (loader half), OFF-1 | nothing |
| **P4 Walls and gates** (amended in revision 3) | M3.3 harness accounting first; view `BuildQueue`, `MapId`; intent 11 and the writer; `AiWallLines.cs` (`Parse`, `AiMapWalls`, `HeldAreas`, tile helpers); `AiProfile.FromDataSet` attaches `WallLines`; memory `WallTilesRefused`, `WallSpentCommon` and the `ReadEvents` recording; rule WL; `use_walls` keys with `expansion_wall_max_bases` and `wall_max_spend_common`; the wall-style draw | WD-1 to WD-4, WL-1 to WL-14, V-1, V-2 (wall half) | P3 (the candidate needs WB), P4-0 |
| **P5 Measurement** (amended) | 7.3 harness change and its tests first (`rts/tools` only, reviewed by the `sim-reviewer` like the M3.3 change); then the section 7.2 runs and `reports/ai_m/README.md` in the R2.5 report's format | H-1, H-2 | P1 to P4 and P2b merged |

## Determinism notes for the sim-reviewer

- One Sim change (P4-0), in the data loader only: `ai/walls/<id>.json` goes to `DataSet.AiWallLines`. Check that it runs only at load, that nothing in `Rts.Sim` reads `AiWallLines`, and that nothing of it reaches `Content.ComputeDigest`, `Tuning.SourceDigest`, `Map.WriteHash` or the state hash (WD-4, OFF-1). No shipped-data hash moves.
- New AI state is in `AiMemory` (`UnreachableGoals`, `WallTilesRefused`: sorted collections; `WallSpentCommon`: an int) and in `AiVariety` (two drawn values); none is hashed. Parsed wall lines (`AiProfile.WallLines`) are immutable after load; `AiProfile.FromDataSet` walks `DataSet.AiWallLines` in ordinal ID order.
- The two draws are appended at the end of `AiVariety.Create` and happen only when their switch is on; check that `IsNeeded` cannot become true for an old profile. Revisions 2 and 3 add no draw and no random stream.
- Rule WL's order is fixed by data and IDs: holders in ascending entity ID (HQs and depots), then ascending worker ID (depot orders); a holder's area by the nearest listed field (squared Fix64 from tile centres) with ties to the lower field index; areas main first, then held-list order; lines in file order; tiles in rasterised line order (`WallLine.Rasterise`, integers only). Threat and reach tests are squared Fix64. Nothing reads hidden state: lines and fields are public map data, holders are own units.
- P2b's `SupportNeedMet` is an existence test over the ordinal-sorted `EnemyTypes`; no order choice reaches the result.
- Readiness uses `CommandDelayTicks`, so a cast is never scheduled before its cooldown ends (bar 3 checks it in whole games).
- `ReadEvents` records new events only when the matching switch is on.

## Rejected extras (one line each)

- A separate flood placement rule for the Tidecaller: trailing the army covers where the AI fights; route flooding needs path knowledge.
- Pre-emptive or protective Downdraft: v4 Q-12; confirmed 22 Nov; Downdraft has a 30 s cooldown.
- A choke finder in the AI at run time (a min-cut search per expansion): the cuts depend on a radius choice and need reading by eye against the map; authored lines are checked once by tests and cost nothing in a think. The same search is a fine authoring aid outside the game (revision 3 used one).
- The computed plateau rule (revision 2's WI) as a fallback on maps without authored lines: two mechanisms to test and tune; on `prototype_1v1` it wanted 17 tiles where the ramps need 8 (Q-14).
- Wall lines as a field of the map file, or as a key of each walling profile: see M3.2.1.
- Rebuilding a covered area's lines, or walls of areas the AI no longer holds: the wider ring already protects them; walls standing there stay.
- Implied enemy types (from seen structures) as a support need: the developer asked for the units the enemy is using; sightings only.
- Converting a wall to a gate or demolishing own walls: no such command (walls_and_tech.md Q-W3).
- A third generator for the style draws: one more seeded stream for the same purpose as the variety stream (rejected 22 Nov).
- A `wall_in` response scenario: useful for WB against humans, but not needed for this spec's bars; v4 A4 specifies it if wanted later.
- Reading Last Stand as an AI decision: it is passive and switched off (M5).
- A fully walled-in Water base holding deeper water behind gates: a game-mechanics change parked by the developer (needs a proposal).

## Out of scope

- Any `rts/src/Sim` change other than P4-0's loader route; any unit, combat, economy, map or tuning change, including `last_stand_enabled`.
- Wall-line files for `greybox_1v1`, `long_lowland` and `twin_chokes` (Q-14).
- Water anti-air positioning, splash aim and the predictor structure multiplier (v4 A3); ablation ladders (v4 A5).
- Switching any new switch on in a profile other than `aggressive_s5_m` (the developer decides after section 7.2). Turning research on in `aggressive_s5` is the coordinator's separate change.
- Unity presentation of AI casts and walls (PC session).

## Questions for the developer

Q-1 to Q-9 were answered on 22 Nov (see the change list at the top). The builder uses each default below until you reply.

- **Q-11. Team games** (reworded in revision 3). Wall-line files name one owning slot per area, and test WD-2 seals each area against every other start, allies included; rule WL walls only areas of the AI's own slot or neutral ones. A team map would need areas sealed only against the other team's starts, which needs the map (or the file) to say which spawns are allied. *Default: accepted until a team map exists; then add a per-area `enemy_slots` list to the file format in a later revision.*
- **Q-13. One sighting keeps the need for the match.** A single enemy flyer seen once keeps Stormcaller support on for the rest of the game, even if the enemy never builds another. *Default: yes, latch (simple, and Stormcallers are useful while any flyer may return).* Alternative: need met only while the enemy model's decayed air count is above 0.
- **Q-14. Maps without authored lines get no walls.** Rule WL is skipped on `greybox_1v1`, `long_lowland` and `twin_chokes` (no file); revision 2's computed plateau rule is not kept as a fallback, so there is one mechanism. *Default: yes; authoring files for those maps is a later data task (a Sonnet or Haiku chore guarded by WD-1 to WD-3), not needed for this spec's measurement, which runs on `prototype_1v1` only.* Alternative: keep the computed plateau rule for maps without a file.
- **Q-15. A wider ring replaces the inner lines.** Once the natural is held, the main's ramp lines are no longer wanted (the natural ring closes the main too), and once the basin is held the natural's inner lines are not either; walls already standing stay but are not rebuilt. A destroyed outer ring therefore leaves the main open until the ring is rebuilt. *Default: yes (no main walls are bought when the natural is held before the first wall, 775 common instead of 1000 for main, natural and basin, and inner lines are never rebuilt).* Alternative: keep building and rebuilding every held area's lines, covered or not.

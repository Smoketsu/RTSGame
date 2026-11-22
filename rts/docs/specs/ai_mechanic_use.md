<!-- rts/docs/specs/ai_mechanic_use.md -->
<!-- Build spec for P-006 package A ("mechanic use"): the skirmish AI casts Downdraft and the wave, trains and places Tidecallers (and Stormcallers as support), breaches enemy walls, and walls in its own base with walls and gates (the Wind Baffle is the Skai wall). HQ Last Stand has no AI rule (it is switched off in tuning; see M5). Four profile switches, off in every existing profile; candidate aggressive_s5_m. No rts/src/Sim change, no unit or combat number change, no pin moves. Measurement bars are fixed in section 7 before any run. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by the sim-reviewer after each package. -->
# Spec: the skirmish AI uses the mechanics it never uses (P-006 package A)

Written 22 Nov 2026 by the designer against `main` at `fbb4f86`. Sources: `proposals/P-006-ai-built-for-balancing.md` (section 3 item 1, section 4 "Then: A", section 6 check 1), `proposals/P-005-roster-holes-and-ai-for-balance.md` (decisions of 18 Nov: unit and combat numbers frozen, AI-vs-AI win rates not read as balance), `specs/research_and_unlocks.md` R2 (format), `specs/skirmish_ai_v4.md` package A (A2 casting and support, A4 breach: specified 18 Nov, never built; reused here and adapted to today's code), `specs/walls_and_tech.md` (W, G, D), `reports/ai_r/README.md` and `reports/ai_r/balance/balance.md`. Code read: `rts/src/Ai/AiBrain.cs` (header, `Decide`, `TryBuild`, `Army`), `AiBrainFight.cs` (`_controlled`), `AiWorldView.cs`, `AiIntent.cs`, `AiMemory.cs`, `AiVariety.cs`, `AiProfile.cs`, `SkirmishAiController.cs` (`ReadEvents`); Sim: `Economy/WallLine.cs`, `Commands/EconomyCommands.cs` (`BuildWallLineCommand`), `Commands/StatusCommands.cs` (`CastGroundingCommand`), `Factions/Water/CastWaveCommand.cs`, `FloodingMechanic.cs` (`TryGetNextWaveTick`, `WaveDirection`), `Terrain/TerrainGrid.cs` (`EdgePassable`), `Units/HqSystem.cs` (`AfterDamage`), `Units/Unit.cs`; data: `units/*`, `tuning/hq.json`, `tuning/flood.json`, `tuning/economy.json`, `ai/aggressive_s5_r.json`; harness: `MechanicDetectors.cs`, `SummaryObserver.cs`, `GameTrace.cs`, `PlayerLedger.cs`.

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
- **No `rts/src/Sim` change.** Every Sim read below is public and was checked (file and member named where used). If a builder finds a Sim change unavoidable, stop and report to the coordinator.
- **No pin moves.** AI profiles are not in the content digest. With the four new switches false, every existing profile plays bit for bit as on `fbb4f86`. The pins (current values in the tests; the values in `STATUS.md` line 83 are older and out of date): smoke `207be73eaa460b3c` and economy_smoke `98b2e00d9babe167` (`Harness/HarnessUnitTests.cs`), `aggressive_s5` trace `963626e47dd707bf` end tick 10378 and `aggressive_s3` trace `c04bdcc0d03b3910` end tick 9223 (`Ai/FightSimTests.OldProfiles_FixedSeedGamesKeepTheirHash`), plus `V2ReferenceHash` and `BlindTraceHash` (`Ai/SkirmishAiTests.cs`). If one moves, stop and report.
- **Determinism (AI).** `Fix64` and integers only; own units, enemies, fields and tiles walked in ascending entity ID or tile index; distances compared squared; every tie to the lower ID or tile index unless a rule says otherwise. The only new random draws are the two match-start style draws of section 6, from the existing variety stream (`AiVariety`), and only when their switch is on. No `System.Random`, no clock. AI memory is not hashed; determinism is shown by harness hashes and by "same inputs twice" tests.
- **Fog.** The AI reads only `AiWorldView`: own units (now including their ability cooldowns and build queues), `VisibleEnemies`, tiles the team sees now, public map data (heights, ramps), unit definitions, tuning, its enemy model and memory, and events about its own units. Every view addition below is own-team or public information.
- **Commands**: only those a player can issue. New for the AI: `cast_wave`, `cast_grounding`, `build_wall_line`. No new command or event type.
- **The brain never names a faction or unit ID.** Unit IDs appear only in profile data. Walls and gates are found by `Economy.IsWall` / `Economy.IsGate`, casters by `Definition.Flood.CanCastWave` / `Definition.GroundingAbility`. Two plumbing files name Sim faction types: `AiWorldView.cs` reads the wave cooldown through `FloodingMechanic`, and `AiCommandWriter` (in `AiIntent.cs`) constructs `CastWaveCommand` (namespace `Rts.Sim.Factions.Water`). Say so in both file headers.
- **Numbers.** No unit, combat, economy, map or tuning number changes (P-005 freeze). Every new AI decision number is a profile key in `rts/data/ai/*.json` with the house comment line `// <what it controls>. Unit: <unit>. Range: <range>. Source: rts/docs/specs/ai_mechanic_use.md <section>; placeholder.` Every value in this spec is a **placeholder**.
- **Parsing.** `AiProfile.Parse` reads every new key with the `Require*` helpers and adds it to `RejectUnknownKeys`. Every profile file under `rts/data/ai` gets every new key (switch false, "old" value of section 1). Hand-written profile JSON in tests gets them too.

## 1. Switches, keys and profiles

Four switches, one per mechanic group:

| Switch | Turns on | Section |
| --- | --- | --- |
| `use_casts` | rules CG (Downdraft) and CW (wave), and the wave-aim draw | M1, M2 |
| `use_support_units` | rules SU (train support units) and ST (support trail), and rule RQ-1's support extension | M2 |
| `use_wall_breach` | rule WB (attack a wall that seals the route) | M3 |
| `use_walls` | rule WI (wall in the base, with gates) and the wall-style draw | M3 |

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
| `wall_plateau_max_tiles` | tiles (Chebyshev) | 4 to 48 | 20 | 20 |
| `wall_max_tiles` | tiles | 1 to 64 | 24 | 24 |
| `wall_funnel_gap_tiles` | tiles (Chebyshev radius) | 0 to 3 | 1 | 1 |
| `wall_max_lines_per_think` | commands | 1 to 8 | 2 | 2 |

Starting values: the v4 A2 and A4 values where v4 gave one (`wave_min_enemy_supply` 4 = two Pummelers in the strip; `grounding_min_air_supply` 3 = one Roc Rider; support 12 and 20 = v4 Q-5); the rest are new placeholders.

Validation (errors name the file and key path, as existing keys do): each `support_units` ID is a unit of the plan's faction with `Supply > 0`; weight objects have exactly their listed keys and a sum above 0.

**Profiles.**
- Every existing file: the keys above with the "old" values. With every switch false the AI plays as on `fbb4f86` (test OFF-1).
- New `rts/data/ai/aggressive_s5_m.json`: a copy of `aggressive_s5_r.json` with `id` changed, the "candidate" values above, and a line-1 header naming this spec. Package P1 creates it with only `use_casts` true; each later package turns its own switch on there. Its off-copy for comparison is `aggressive_s5_r` itself.

## 2. View, intent and memory additions (shared by the sections below)

**`AiUnitView`** (new optional constructor parameters at the end, so existing callers compile unchanged; the synthetic pending-order views in the `Turn` constructor pass the defaults):
- `WaveReadyTick` (int, ticks, 0 upward): own units, `FloodingMechanic.TryGetNextWaveTick(id, out t) ? t : 0` (the mechanic found in `sim.Mechanics` by `FloodingMechanic.MechanicId`; 0 when the match has none); enemies 0.
- `GroundingReadyTick` (int, ticks, 0 upward): own units `Unit.GroundingReadyTick`; enemies 0.
- `BuildQueue` (`IReadOnlyList<int>`, tile indices, 0 to 255 entries): own units a copy of `Unit.BuildQueue`; enemies empty.

**`AiWorldView`**: `WaveLengthTiles`, `WaveWidthTiles` (int, tiles, from `sim.Tuning.Flood`), `WallLineMaxTiles` (int, tiles, 1 to 64, from the economy tuning key `wall_line_max_tiles`).

**Intents** (`AiIntentKind`, appended values; `AiCommandWriter` builds the command with the usual schedule tick and sequence):
- `CastWave = 9`: `AiIntent.CastWave(EntityId caster, FixVec2 target)` to `CastWaveCommand`.
- `CastGrounding = 10`: `AiIntent.CastGrounding(EntityId caster, FixVec2 target)` to `CastGroundingCommand`.
- `BuildWallLine = 11`: `AiIntent.BuildWallLine(EntityId worker, string definitionId, IReadOnlyList<(int X, int Y)> tiles)` to `BuildWallLineCommand`. New `AiIntent.Tiles` (empty for every other kind).

**Readiness.** An ability is ready when `readyTick <= view.Tick + memory.CommandDelayTicks`.

**Memory** (`AiMemory`, not hashed): `UnreachableGoals` (M3.1), `WallPlan`, `WallTilesRefused` (M3.2). Style values drawn at match start live in `AiVariety` (section 6).

## M1. Downdraft (`use_casts`)

**Helper** (`rts/src/Ai/AiAbilities.cs`, new, `public static`, pure): `ChooseGrounding(AiWorldView view, AiUnitView caster, out FixVec2 target) -> int score` (supply). Candidate points: positions of visible enemy units with `Layer == Air` whose centre is within `GroundingAbility.RangeTiles` of the caster (squared). Score of a point: total supply of visible enemy units with `Layer == Air` whose centre is within `GroundingAbility.RadiusTiles + their definition's RadiusTiles` of it (squared; the Sim's test, combat_mechanics.md 4.2). Best score; ties to the lower candidate entity ID. No candidate: score 0.

**Rule CG.** In `Army`, immediately after `UnitControl(armyAll)` and before `StanceArmy` (so it runs before `MeasuredDefence` can return early): for each own unit, ascending ID, with `Definition.GroundingAbility != null`, not under construction, ability ready: if `ChooseGrounding(...) >= grounding_min_air_supply`, emit `CastGrounding(caster, target)`. At most `grounding_max_commands_per_think` per think. A cast does not add the unit to `_controlled` (casting does not change its orders).

**When it fires, in view terms:** an own Stormcaller is off cooldown and sees, within 7 tiles (its data), enemy flyers worth at least 3 supply that one 2-tile Downdraft would catch. Water has no flyer in shipped data, so Downdraft fires only against Skai.

**Variety.** Situational only: whether and where it fires depends on which flyers are seen and where. No pre-emptive casts (v4 Q-12 default; Q-7 below).

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

### M2.2 Support trail (rule ST, `use_support_units`)

Unarmed army units (`IsArmy && Definition.Weapon == null`) never get stance orders while the switch is on: each is added to `_controlled` every think. In stance Attack with a non-empty attack group: point P = mean position (Fix64) of the live armed attack-group units, moved `support_trail_tiles` toward Home (`MoveTowards`), then made standable (`Standable`); in any other stance, P = `RallyPoint()`. A unit farther than `support_regroup_tiles` from P (squared) gets `Move(P)` unless it already has a move target within `SameGoalSquared` of P. Fall back (D.6.1) still applies first. The Tidecaller is a flood source wherever it stands, so trailing the army is how the AI places its flood (no separate flood placement rule).

### M2.3 Casting the wave (rule CW, `use_casts`)

**Helpers** (`AiAbilities.cs`):
- `WaveTiles(int casterX, int casterY, int ux, int uy, int length, int width) -> SortedSet<int>`: the strip of flooding.md section 5: `s = 1..length`, `o = -(width - 1) / 2 .. (width - 1) / 2`, tile `(cx + s*ux - o*uy, cy + s*uy + o*ux)`, off-map tiles skipped, as tile indices.
- `ChooseWave(AiWorldView view, AiUnitView caster, bool meleeOnly, Fix64 meleeRange, out FixVec2 target) -> Fix64 score`: for each of the 8 directions in `TerrainGrid.NeighbourDx/Dy` order, score = total supply of visible enemy **army** units with `Layer != Air`, a weapon, and their tile in the strip; with `meleeOnly`, only those whose `Weapon.RangeTiles <= meleeRange`. Best score; ties to the lower direction index. `target = caster.Position + (4 ux, 4 uy)`: the Sim's `WaveDirection` maps it back to `(ux, uy)` (a straight offset has one axis more than twice the other; a diagonal has equal axes).

**Rule CW.** Same place as rule CG, after it: for each own unit, ascending ID, with `Definition.Flood.CanCastWave`, not under construction, wave ready: `score = ChooseWave(view, caster, memory.Variety.WaveAim == Melee, wave_melee_range_tiles, out target)`; if `score >= wave_min_enemy_supply`, emit `CastWave(caster, target)`. At most `wave_max_commands_per_think`. Not added to `_controlled`.

**When it fires, in view terms:** an own Tidecaller is off cooldown and at least 4 supply of visible enemy ground or water army (melee only, for the melee aim) stands in one of the eight 6 by 3 strips ahead of it.

**Variety.** The wave aim is drawn per match (section 6): `melee` pushes only melee units (P-005: "answer a melee ball with a wave"); `all` also pushes ranged units off own melee units. Where and when it fires is situational (the trailing Tidecaller casts wherever the fight is, in attack and in defence).

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

Two rules. WB answers enemy walls; WI builds own walls. They are separate switches so WB can later be measured on its own (against humans and in a `wall_in` scenario), but the candidate needs both: an AI that seals its base against an opponent with no breach rule ends the game in a stalemate, because units whose goal is sealed off stop and do not resume (walls_and_tech.md W.1 rule 3, Q-W1 default).

### M3.1 Breaching a wall that seals the route (rule WB, `use_wall_breach`)

This is v4 A4, unchanged except for its place in the order.

**Memory.** `UnreachableGoals`: `SortedDictionary<EntityId, (int Tick, FixVec2 Goal)>`. `SkirmishAiController.ReadEvents` records each `MoveUnreachableEvent` (`Events/TerrainEvents.cs`) whose unit is an own live unit, overwriting, only when `use_wall_breach` is on. In rule 0, entries of dead units and entries older than `wall_breach_memory_seconds` are dropped.

**Rule WB.** In `Army`, after rules CG and CW, before `StanceArmy`: for each own army unit u, ascending ID, with an entry, not in `_controlled`, not fallen back, not in the harass group, whose weapon can target Ground:
1. Candidate walls: `KnownEnemyStructures` entries whose `DefinitionId` names a definition with `Economy.IsWall` (gates included). Choose the one minimising `|u - w| + |w - goal|` (`FixVec2.Length`, which is `Fix64.Sqrt` and deterministic), ties to the lower entity ID.
2. Chosen wall visible now (in `VisibleEnemies`): `AttackUnit(u, wall)`. Not visible: `AttackMove(u, Standable(w.Position.MoveTowards(u.Position, wall_breach_standoff_tiles)))`.
3. No known wall: `AttackMove(u, Standable(u.Position.MoveTowards(goal, wall_probe_step_tiles)))`, so the unit walks toward its goal until it sees what blocks it.
4. u joins `_controlled`. At most `wall_breach_max_commands_per_think`.

### M3.2 Walling in the base (rule WI, `use_walls`)

**Wall plan** (`rts/src/Ai/AiWalls.cs`, new, `public static`, pure; `Plan(AiWorldView view, int hqX, int hqY, int maxTiles, int plateauMax) -> AiWallPlan`). All on public map data (`HeightAt`, `IsRamp`, `Contains`), so it is the same whatever the fog:
1. **Plateau P**: from the HQ tile, a breadth-first fill over the 4 edge neighbours of tiles with `HeightAt == HeightAt(hq)` and `!IsRamp`. Blockers are ignored (own buildings stand on the plateau). If the fill reaches any tile at Chebyshev distance `wall_plateau_max_tiles` from the HQ tile, the base is open: the plan is empty.
2. **Mouth tiles M**: tiles of P with at least one ramp tile among their 8 neighbours. Why this seals: `TerrainGrid.EdgePassable` lets a ground or water unit change height only across a ramp, and P is a maximal same-height region, so every way into P crosses from a ramp tile onto a tile of M (a diagonal step also needs both orthogonal steps passable, `IsStepPassable`).
3. If M is empty or holds more than `wall_max_tiles` tiles: the plan is empty.
4. **Mouths**: the 8-connected components of M, each seeded from its lowest tile index, listed in order of that index. Within a mouth, tiles in ascending index.
5. **Gate tile** of a mouth: the tile whose centre is nearest (squared, Fix64) the mean of the mouth's tile centres; ties to the lower index.

`AiWallPlan` holds the mouths (tile index lists) and their gate tiles. It is cached in `AiMemory.WallPlan` with the HQ tile it was made for, and remade when the Rooted HQ's tile changes.

**Wall and gate definitions**: the AI faction's unit definitions with `Economy.IsWall && !Economy.IsGate` and with `Economy.IsGate`, each the ordinally lowest ID (shipped: `skai.wind_baffle` / `skai.wind_gate`, `water.coral_wall` / `water.coral_gate`). None, or requirements unmet (`RequirementsMet`): rule WI does nothing.

**Tiles wanted** for the drawn style (section 6): `none`: no tiles, the rule does nothing. `funnel`: every mouth tile except those within Chebyshev `wall_funnel_gap_tiles` of the mouth's gate tile, all walls, no gate. `sealed`: every mouth tile; the gate tile gets the gate, the others walls.

**Rule WI.** Runs in `Decide` after `StaticDefence()` and before `Expand()`. It fires when all hold:
1. The own HQ is Rooted and the plan is not empty.
2. `view.Tick >= SecondsToTicks(wall_earliest_seconds)`.
3. Enemy ground share: with `use_enemy_model`, the supply share of non-Air types in the enemy model is at least `wall_min_enemy_ground_share`, or the model is empty; without the enemy model, always true. (Walls do not stop flyers.)
4. No visible enemy army unit within `wall_threat_tiles` (squared) of any tile of the plan.

Then a tile of the wanted set is **missing** when: no own unit with `IsWall` stands on it (complete or under construction); it is not an own worker's active build tile (`BuildTileX/Y`) nor in an own worker's `BuildQueue`; it is not in `_reservedSites`; it is not in `WallTilesRefused`; and `!IsBlocked` (a structure already there closes the tile).

For each mouth in order, while fewer than `wall_max_lines_per_think` lines were emitted this think:
- **Walls**: the mouth's missing wall tiles in ascending index, cut into chunks of `view.WallLineMaxTiles`. For each chunk: n = the largest count, from the chunk's start, that free money pays (no budget queues: `common - reserveCommon >= n x cost`, same for rare; with budget queues: the `defence` queue's account, through `DefenceAllows` and `BqCanPay` per tile). n = 0: stop rule WI for this think **without reserving** (walls are bought from spare money, never ahead of the army; with budget queues no want is recorded). Else the builder is `PickBuilder(centre of the first tile)`, not a worker picked this think; none: stop. If the first tile was never explored, the builder is sent there (`Move`) as `TryBuild` does and the line waits. Otherwise emit `BuildWallLine(builder, wallId, first n tiles)`, charge n x cost through the same ledger as `Pay` (the `defence` queue with budget queues), add the builder to `_pickedBuilders` and the tiles to `_reservedSites`.
- **Gate** (style `sealed` only): when the mouth's gate tile is missing, `TryBuild(gate definition, gate tile, false, AiBudget.Defence)` (its own affordability, reservation, retry and builder rules apply).

**Refused tiles.** `ReadEvents` (only when `use_walls`) adds the tile index of each own `BuildLineTileSkippedEvent` (`Events/EconomyEvents.cs`) with reason `out_of_bounds`, `tile_blocked` or `on_resource_field` to `WallTilesRefused`, and of each own `CommandRejectedEvent` of a `build_structure` of a gate with reason `tile_blocked` or `on_resource_field`. Other reasons (a unit in the way, money) are retried by later thinks. A tile freed later (an own wall killed) is missing again and is rebuilt, so the plan heals.

**When it fires, in view terms:** the HQ is Rooted on a closed plateau with at most 24 mouth tiles, three minutes have passed, the enemy seen so far is mostly ground, no enemy army is near the ramps, and there is spare common.

**Interactions.** Own units pass own gates (team rule, walls_and_tech.md G.1 rule 3), so workers and the army leave and return through the gate in `sealed` style and through the gap in `funnel`. Water walls also stop water's own flood at the mouths (W.1 rule 6, Q-W8 default; Q-3 below). Existing test W-14 ("no `build_structure` names a wall over `ai_vs_ai.json`") still holds: that script uses profiles with `use_walls` false.

### M3.3 Harness accounting for wall lines (build first within P4)

`SummaryObserver.AfterStep` and `GameTrace` charge only accepted `TrainUnitCommand` and `BuildStructureCommand`, so an AI wall line would be built but not counted as spent. Change both call sites: an accepted `BuildWallLineCommand` of an AI is charged the cost carried by the `BuildOrderAcceptedEvent` of the same worker in the same step (that event carries cost x tiles kept, walls_and_tech.md D.2 rule 4) to the `wall` class, through a new `PlayerLedger.SpendAmount(string spendClass, int common, int rare)`. Refunds are not subtracted (the same rule as for structures today).

### M3.4 Tests (`AiMechanicTests.cs`, `AiWallTests.cs` for the plan)

Breach (greybox; P0 Talon Marksman at (18.5, 17.5) with `UnreachableGoals` = (tick - 1 s, (40.5, 17.5)) unless stated):
- **WB-1 Visible wall.** P1 `water.coral_wall` at tile (22, 17), visible to P0: `AttackUnit(talon, wall)`; no stance order for the talon this think.
- **WB-2 Remembered wall.** The wall at (30, 17) in `KnownEnemyStructures`, not visible: `AttackMove(talon, (28.5, 17.5))`.
- **WB-3 No known wall.** `AttackMove(talon, (24.5, 17.5))`.
- **WB-4 Old entry.** Entry at tick - 11 s: no WB order.
- **WB-5 Flyer.** A Roc Rider with an entry: no WB order.
- **WB-6 Recording.** A P0 unit inside a ring of eight P1 walls around tile (18, 17) is ordered out by script; after the next think `UnreachableGoals` holds the unit with that goal. With `use_wall_breach` false it stays empty.

Plan (`prototype_1v1`, pure function on a tick-0 view):
- **WI-1 Geometry.** For each of the two spawns the plan is not empty and has 2 mouths (the map's two ramps per start plateau); every tile is at the HQ tile's height, not a ramp, and has a ramp among its 8 neighbours; the south-west plan is the point mirror `(95 - x, 95 - y)` of the north-east plan. Expected north-east set (from v4 A4, read off the map file, not yet checked in code): (73, 84), (73, 89), (74, 84) to (74, 89), (87, 74), (87, 75), (88, 75), (89, 75), (90, 75), (90, 76), (91, 76), (92, 75), (92, 76), 17 tiles. If the computed set differs, WI-2 decides; the builder reports both sets and does not change the rule.
- **WI-2 Seal (Sim).** Script-spawn the north-east `sealed` plan for slot 1 (walls and gates) on a started `prototype_1v1` match: a slot-0 ground unit at (55.5, 55.5) ordered to (83.5, 83.5) gets `MoveUnreachableEvent`; a slot-1 ground unit at (55.5, 55.5) ordered there arrives (`UnitArrivedEvent`). With the `funnel` plan instead, the slot-0 unit arrives.
- **WI-3 Open base.** With `wall_plateau_max_tiles` 4 the plan is empty; with `wall_max_tiles` 16 it is empty.

Rule (`prototype_1v1`, slot 0 water then Skai, Rooted HQ at its spawn, money granted, tick past 180 s, style forced by the section 6 test hook):
- **WI-4 Sealed.** Decide emits, for the first mouth (lowest index) then the second, one `BuildWallLine` with that mouth's non-gate tiles in ascending index (each line at most 16 tiles), each by the free worker nearest its first tile, and a `Build` of the faction's gate at each gate tile. With all those orders pending (the next think, orders accepted), no new wall or gate intent. At most 2 lines per think.
- **WI-5 Funnel.** Lines exclude the gate tile and every mouth tile within Chebyshev 1 of it; no gate intent.
- **WI-6 Conditions.** No wall intent when: style `none`; tick before 180 s; a visible P1 army unit within 10 tiles of a plan tile; the enemy model holds only Roc Riders (share 0); free common below one tile's cost; and in the last case the reservation totals are unchanged.
- **WI-7 Rebuild.** After WI-4's walls are built, a script `remove_unit` of one wall: the next think emits a `BuildWallLine` of exactly that tile.
- **WI-8 Refused.** A `BuildLineTileSkippedEvent` of an own line with reason `on_resource_field` for a plan tile: the tile is in `WallTilesRefused` and is never ordered again.
- **WI-9 Line payment.** No budget queues, common 100, walls costing 25: the first line holds 4 tiles; no reservation is made.
- **WI-10 Harness.** In a game where an AI line of 3 Coral Walls is accepted, the slot's ledger `wall` class holds 75 common; `BalanceReport` counts `water/wall` for the slot once a wall completes.
- **WI-11 Exit game.** A copy of `ai_vs_ai.json` on `prototype_1v1` with both profiles `aggressive_s5_m` (all four switches on) runs twice with identical hashes at every hash tick and zero AI command rejections other than `site_occupied`; whether it is decided is reported, not asserted.
- **WI-12 Off.** With `use_walls` and `use_wall_breach` false: no `BuildWallLine`, `UnreachableGoals` and `WallTilesRefused` stay empty, and the intents equal those of `aggressive_s5_r` on WI-4's view.

## M4. Wind Baffle

The Wind Baffle (`skai.wind_baffle`, `is_wall`, not a gate) is the Skai wall. Its unit row in `balance_use` and the `skai/wall` mechanic row count the same thing (a completed non-gate Skai wall), so rule WI is its rule; there is nothing Skai-specific to add (walls_and_tech.md Q-W6 default: identical rules). Its test is WI-4 run with slot 0 as Skai: the `BuildWallLine` names `skai.wind_baffle` and the gate `Build` names `skai.wind_gate`, found by flags, not by name.

## M5. HQ Last Stand

**No AI rule, by design.** Last Stand is passive: `HqSystem.AfterDamage` triggers it when a hit leaves an HQ at or below `last_stand_health_fraction` of its health, and only when `tuning/hq.json` `last_stand_enabled` is true. It is false in shipped tuning ("Switch that enables Last Stand for the prototype test"), so no player, human or AI, can use it, and no AI decision changes that. Turning it on is a game-rule change that moves every pin; it is not the AI's to make (Q-1). The two `hq_last_stand` rows are **reported, not judged** in section 7.

## 6. Variety: how these rules avoid one fixed pattern

The developer requires that the AI "have a variety of tactics". These rules add variety in three ways, using only AI decision keys (no unit or combat number):
1. **Two per-match style draws** from the variety stream (`AiVariety`, already seeded per match and player). At the end of `AiVariety.Create`, after the existing draws and in this order: when `use_walls`, the wall style (`none`, `funnel`, `sealed`) by `wall_style_weights`; when `use_casts`, the wave aim (`melee`, `all`) by `wave_aim_weights`. Each draw is one `rng.NextInt(sum)` mapped through the cumulative weights in the key order listed (the same method as the opening draw). `AiVariety.IsNeeded` also returns true when either switch is on. Stored as `AiVariety.WallStyle` and `AiVariety.WaveAim`; without a variety stream (both switches off) they read `none` and `melee`. A test hook (as `AiVariety.ForTest`) sets them directly. With the candidate's weights: one game in five does not wall, three in ten leave a funnel, half seal with a gate; half the Tidecallers push only melee, half push everything.
2. **Situational triggers.** Every rule reacts to what the view shows: walls only against an enemy seen to be mostly ground, only on a closed plateau, only from spare money; Downdraft only at flyer clumps; the wave only at clumps of the chosen kind; support units in proportion to the army; breaching only when an own order was sealed off. The same profile therefore plays differently against different openings and factions.
3. **Combination with the existing openings** (package L: each plan draws one of its openings) and the composition jitter: the style draws are independent of them, so the number of distinct starts multiplies.

Draws happen only when their switch is on, so the old profiles' streams are unchanged (OFF-2). In the candidate, the two extra creation draws shift its later attack-advantage offsets relative to `aggressive_s5_r` (Q-8).

**Tests.**
- **V-1 Draw reproducibility.** `AiVariety.Create` for `aggressive_s5_m`, the same seed twice: equal `WallStyle`, `WaveAim`, opening and offsets.
- **V-2 Distribution.** Over seeds 1 to 1000 (player 0, water plan), `WallStyle` counts are within 50 of 200, 300 and 500, and `WaveAim` counts within 50 of 500 each. (The counts are deterministic; the builder pins the exact values in the test comment.)

## 7. Off path and measurement

### 7.1 Off path (tests)
- **OFF-1 Pins.** Every pin of section 0 is unchanged; no test is re-pinned.
- **OFF-2 Old streams.** For `aggressive_s5_r` (variety stream present, new switches false): the sequence of `AiVariety` draws over a fixed traced game equals the sequence before this spec (same opening, offsets and advantage offsets). For a fixed view, the think intents of `aggressive_s5_r` and of a copy with all four switches explicitly false and every other new key at its candidate value are equal.

### 7.2 Measurement (stated before any run; nothing is tuned after a result)

Run after P4, from the Release build of the branch: `--balance --profile aggressive_s5_m --maps prototype_1v1` with the defaults of `ai_eval.json` (base seed 20261121, 72 games per matchup, three matchups), `--fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl`, and one `--trace` per matchup with the candidate in both seats; output under `rts/docs/reports/ai_m/`. Map `prototype_1v1` only. No ladder and no win-rate bar: the developer has ruled that AI-against-AI win rates are not read as balance.

Bars (each is the existing `balance_use` share, 0.100, unchanged):
1. Each of these 11 items reaches a use share of at least 0.100 in its faction's slot-games: `skai/downdraft`, `water/wave`, `water.tidecaller`, `skai/wall`, `water/wall`, `skai/gate`, `water/gate`, `skai.wind_baffle`, `water.coral_wall`, `skai.wind_gate`, `water.coral_gate`.
2. Every item at or above 0.100 in `reports/ai_r/balance/balance.md` (the 44 that passed there) is still at or above 0.100.
3. In each traced game, no AI command of kind `cast_wave`, `cast_grounding` or `build_wall_line` is rejected, and no AI `build_structure` of a gate is rejected other than `site_occupied`.

Reported, not judged: the two `hq_last_stand` rows (M5); games reaching `max_game_seconds` and the median end tick, against `reports/ai_r`; the share of slot-games per drawn wall style and wave aim; median time of the first use of each bar-1 item; `balance_unspent`, `balance_predictor` and `balance_openings` as printed.

**No number is tuned after a result.** A failing bar is reported with its value. Any change of a rule or a key value is a new revision of this spec, with its bars restated before the next run.

## 8. Build packages (cheapest first; one builder session each)

Each package: Sonnet `builder`, then the Opus `sim-reviewer` on the `rts/src/Ai` and harness diff; `dotnet build` with no warnings and `dotnet test` green, including OFF-1 and OFF-2. Every package adds its keys to every profile file (switch off) and to `aggressive_s5_m` (switch on), and updates the `AiBrain.cs` header (rule order), `AiProfile.cs` "Differences" list, `rts/src/Ai/README.md`, and the headers of the files it touches.

| Package | Contents | Tests | Depends on |
| --- | --- | --- | --- |
| **P1 Casts** | Section 2 view fields `WaveReadyTick`, `GroundingReadyTick`, `WaveLengthTiles`, `WaveWidthTiles`; intents 9 and 10 and the writer; `AiAbilities.cs`; rules CG and CW; `use_casts` keys; the wave-aim draw (section 6 part 1); creates `aggressive_s5_m` | M1-1 to M1-5, M2-1 to M2-4, M2-8 (cast half), V-1, V-2 (aim half), OFF-1, OFF-2 | nothing |
| **P2 Support units** | Rules SU and ST, the RQ-1 extension, `use_support_units` keys and plan `support_units` with validation | M2-5 to M2-7, M2-8 (support half) | P1 (the candidate profile) |
| **P3 Breach** | `UnreachableGoals`, the `ReadEvents` recording, rule WB, `use_wall_breach` keys | WB-1 to WB-6 | P1 |
| **P4 Walls and gates** | M3.3 harness accounting first; view `BuildQueue`, `WallLineMaxTiles`; intent 11 and the writer; `AiWalls.cs`; `WallPlan`, `WallTilesRefused` and their `ReadEvents` recording; rule WI; `use_walls` keys; the wall-style draw | WI-1 to WI-12, V-1, V-2 (wall half) | P3 (the candidate needs WB) |
| **P5 Measurement** | Section 7.2 runs and `reports/ai_m/README.md` in the R2.5 report's format; no code | none | P1 to P4 merged |

## Determinism notes for the sim-reviewer

- No Sim file changes; no shipped-data hash moves. New AI state is in `AiMemory` (`UnreachableGoals`, `WallPlan`, `WallTilesRefused`, all sorted collections) and in `AiVariety` (two drawn values); none is hashed.
- The two draws are appended at the end of `AiVariety.Create` and happen only when their switch is on; check that `IsNeeded` cannot become true for an old profile.
- The plateau fill's result is a set (order-free); mouths are seeded by lowest index; the gate tile is chosen with Fix64 means and a lower-index tie; chunks follow ascending index.
- Readiness uses `CommandDelayTicks`, so a cast is never scheduled before its cooldown ends (bar 3 checks it in whole games).
- `ReadEvents` records new events only when the matching switch is on.

## Rejected extras (one line each)

- A separate flood placement rule for the Tidecaller: trailing the army covers where the AI fights; route flooding needs path knowledge.
- Pre-emptive or protective Downdraft: v4 Q-12 default; Downdraft has a 30 s cooldown.
- Walling expansions or chokes away from the HQ plateau: needs a choke finder; the plateau mouths are public geometry and cover the main base.
- Converting a wall to a gate or demolishing own walls: no such command (walls_and_tech.md Q-W3).
- A third generator for the style draws: one more seeded stream for the same purpose as the variety stream (Q-8 keeps it as the alternative).
- A `wall_in` response scenario: useful for WB against humans, but not needed for this spec's bars; v4 A4 specifies it if wanted later.
- Reading Last Stand as an AI decision: it is passive and switched off (M5).

## Out of scope

- Any `rts/src/Sim` change; any unit, combat, economy, map or tuning change, including `last_stand_enabled`.
- Water anti-air positioning, splash aim and the predictor structure multiplier (v4 A3); ablation ladders (v4 A5).
- Switching any new switch on in a profile other than `aggressive_s5_m` (the developer decides after section 7.2).
- Unity presentation of AI casts and walls (PC session).

## Questions for the developer

The builder uses each default until you reply.

- **Q-1. HQ Last Stand is off for everyone** (`last_stand_enabled` false in `tuning/hq.json`), so its two `balance_use` rows can never pass, whatever the AI does. *Default: leave it off; report the two rows, do not judge them.* Alternatives: turn Last Stand on (a game-rule change; every pin moves; in every decided game the losing HQ would trigger it), or have the harness mark the rows "not applicable" while it is off (harness only).
- **Q-2. Should the AI wall in at all?** walls_and_tech.md W.4 kept the AI out of walls. With Q-W1's default (units stop at a sealed goal) a sealed base is a hard lock against any opponent without a breach rule. *Default: yes, behind `use_walls`, and the candidate always has rule WB on as well.*
- **Q-3. Water walls hold back water's own flood** at the plateau mouths (Q-W8 default: walls block every team's water). *Default: accepted; no special rule.*
- **Q-4. Does a funnel (walls with a gap, no gate) count as competent wall use?** *Default: yes; it is one of the three styles.* Alternative: only `none` and `sealed`.
- **Q-5. Support numbers.** *Default (v4 Q-5): one Tidecaller per 12 army supply and one Stormcaller per 20, at most 3 of each, trained before counter-mix units.*
- **Q-6. What waves are for.** v4 Q-6 defaulted to melee only. *Default here: a per-match draw, half melee only and half everything, for variety.* Alternative: melee only (`wave_aim_weights` 100 / 0).
- **Q-7. Downdraft targeting.** *Default (v4 Q-12): visible flyers only, by most flyer supply caught; no pre-emptive casts.*
- **Q-8. The style draws come from the existing variety stream,** which shifts the candidate's later attack-advantage offsets against `aggressive_s5_r` (old profiles are unaffected). *Default: accepted, since no win-rate bar is read.* Alternative: a third seeded stream for tactics.
- **Q-9. Downdraft can only be used against Skai** (water has no flyer), so its share over all Skai slot-games is capped at about two thirds on `prototype_1v1`. *Default: keep the 0.100 bar over all Skai slot-games, as `balance_use` defines it.*

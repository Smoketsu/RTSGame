<!-- rts/docs/specs/skirmish_ai_v4.md: build spec for the P-005 part 2 work on the skirmish AI as a balance tool (developer decisions of 18 Nov 2026): raid response and economy bug fixes (package F), honest statistics in the balance harness (S), use of the built combat mechanics and abilities (A, five steps), and points to skirmish_ai_v4_rl.md for seeded randomness and an opening library (R+L). Extends skirmish_ai_v3.md and the rules in the header of rts/src/Ai/AiBrain.cs. Part of rts/docs/specs (index: README.md). -->
# Spec: Skirmish AI v4 (an AI whose games can count as balance data)

## 0. Goal, sources and ground rules

**Goal.** The developer's words: "We dove into the cpu ai in the first place as a way to test balance, and if it's not good, or able to compete with a human, we won't get proper balancing data." P-005 (decisions 10, 11 and 13) adopts the "good enough to trust" bar and forbids reading AI-against-AI win rates as balance until bar items 1 to 3 pass: (1) every response scenario passes for both factions without a knife-edge retune; (2) the AI uses every built faction mechanic, with a measured positive effect; (3) at least 80 percent distinct games per cell, intervals over distinct games. This spec works toward items 1 and 2 (packages F and A) and makes item 3 measurable (package S). Item 3 itself needs package R. One strong AI, no difficulty levels. AI evaluation runs only on `prototype_1v1`.

**Sources read.** `proposals/P-005-roster-holes-and-ai-for-balance.md` (all of it, including the decisions table and the bar), `specs/skirmish_ai_v3.md`, `specs/combat_mechanics.md`, `specs/walls_and_tech.md`, `specs/micro_benchmark.md` (sections 3, 4, 7), `specs/flooding.md` section 5, `reports/missing_roster_r3/README.md`, `reports/fights_2026-11-18/README.md`, `rts/src/Ai/*.cs`, `rts/data/ai/*.json`, `rts/data/scripts/ai_eval/*`, and the Sim types the AI reads. Written against `main` at `f4241f5`.

**What was run for this spec (on `f4241f5`, Linux, Release build).**
- `--response --profile aggressive_s3`: `RESPONSE_SUMMARY passed=5 of=10`, the same set as the R-3 report. Two metrics moved since R-3: `raid_water` loses 5 workers in a wave (R-3: 7) and `turtle_skai` loses 18 supply before its first kill (R-3: 41).
- A scratch observer (outside the repository) replaying `response_worker_raid_skai.json` and `_water.json` with `aggressive_s3` and printing losses, stance, defend group and raid log every 2 s. Its findings are in F.0.
- The R-3 ladder file `reports/missing_roster_r3/ladder_s3_vs_s2b/ladder.jsonl`, grouped by (slot-0 faction, slot-0 profile, result, end tick): see S.0.
- A replica of the `--fights` fight `flat/750/skai.roc_rider/water.shellback` (same arena, formation and attack-move, without the idle re-targeting) that counted events: see S.4.

**Ground rules (all packages).**
- **No change to `rts/src/Sim`.** Every Sim read this spec needs is public and was checked: `Unit.GroundingReadyTick` (`Units/Unit.cs:171`), `FloodingMechanic.TryGetNextWaveTick` (`Factions/Water/FloodingMechanic.cs:172`), `FloodingMechanic.MechanicId`, `Simulation.Mechanics`, `TerrainTile.WaterDepth`, `Tuning.Flood.WaveLengthTiles` / `WaveWidthTiles`, `UnitDefinition.GroundingAbility` and `.Flood.CanCastWave`, `WeaponDefinition.SplashRadiusTiles` / `StructureDamageMultiplier` / `CanTargetAirOnWater`, `UnitEconomyDefinition.IsWall` and `.Requires`, `CastWaveCommand` and `CastGroundingCommand` constructors, `MoveUnreachableEvent`, `CommandRejectedEvent`, `ShieldDomeHitEvent`, `WaveCastEvent`, `GroundingCastEvent`. If a builder finds a Sim change unavoidable, stop and report to the coordinator with the reason; it would move every hash and need a determinism review. No state hash of a shipped-data script changes in this spec.
- The AI stays a pure function of `AiWorldView` + `AiProfile` + `AiMemory` + its own `DeterministicRandom` (seeded per player in `SkirmishAiController.OnMatchStart`; verified). **No new random draw** in F, S or A (the only draw stays the attack-threshold jitter, `AiBrain.EnterMassing`). Fix64 and integers only; own units, enemies, fields and tiles walked in ascending entity ID or index; distances compared squared (one exception, A4, uses `FixVec2.Length`, which is `Fix64.Sqrt` and deterministic); every tie goes to the lower ID or index unless a rule says otherwise. AI memory is not hashed; determinism is proven by harness hashes and by "same inputs twice" tests.
- **Fog.** The AI may read: own units (including their ability cooldowns), `VisibleEnemies`, tiles the team sees now (water depth only on those), public map data, unit definitions, tuning, events about its own units and its own commands (`MoveUnreachableEvent` for an own unit, `CommandRejectedEvent` for an own command), and `UnitKilledEvent`s as v3 already reads them. Never a fogged unit or a fogged tile's water.
- **Commands**: only those a player can issue. New for the AI: `cast_wave` and `cast_grounding` (both exist in the Sim). No new command or event type.
- **The brain never names a faction or a unit ID.** Unit IDs appear only in profile data. Two plumbing files name a faction-mechanic type: `AiWorldView.cs` reads the wave cooldown through `FloodingMechanic`, and `AiCommandWriter` (in `AiIntent.cs`) constructs `CastWaveCommand` (it lives in `Rts.Sim.Factions.Water`). Say so in both file headers.
- Every number is a profile key (`rts/data/ai/*.json`), an `ai_eval.json` key, a `tuning/harness_batch.json` key or a `scripts/fights/fights.json` key, commented in the house format `// <what>. Unit: . Range: . Source: skirmish_ai_v4.md <section>; placeholder.` Every new value is a **placeholder**.
- `AiProfile.Parse` reads every new key with the `Require*` helpers and adds it to `RejectUnknownKeys`; **every profile file under `rts/data/ai` gets every new key**, with the switch off and the "old value" of the tables below, unless stated. Update the `AiProfile.cs` "Differences" list and the `AiBrain.cs` header per package. Hand-written profile JSON in tests gets the new keys too.
- With every new switch off, the AI plays exactly as on `f4241f5`: test A-11 (`V2ReferenceHash`) and every other pinned AI hash (`BlindTraceHash`) stay green without re-pinning. If one moves, stop and report.

### 0.1 Profiles

| File | What it is | Made in |
| --- | --- | --- |
| every existing profile | new keys added, switches off | F, A |
| `aggressive_s4f.json` | copy of `aggressive_s3.json`, `id` changed, F switches on | F |
| `aggressive_s4.json` | copy of `aggressive_s4f.json`, `id` changed, A switches on (each A step turns on its own) | A1 |
| `abl_s4_no_wave.json`, `abl_s4_no_grounding.json`, `abl_s4_no_water_aa.json` | copies of `aggressive_s4.json` with one switch off (`use_wave`, `use_grounding`, `use_water_anti_air`) | the A step that adds the switch |

The **current baseline** for every `--ladder` in this spec is `aggressive_s3` (the best profile on `main`; R-3: 0.782 against `aggressive_s2b`).

### 0.2 Build order and dependencies

| Package | Depends on | Can run in parallel with | Reviewer |
| --- | --- | --- | --- |
| F (raid response, economy bugs) | nothing new | S | `sim-reviewer` on `rts/src/Ai` |
| S (honest statistics) | nothing new | F | `sim-reviewer` on the harness diff (it reads hashes and events) |
| A1 to A5 (mechanics) | F (profile `aggressive_s4f`), S (distinct counts in the gate reports) | nothing | `sim-reviewer` on `rts/src/Ai` and the harness diff after each step |
| R+L | S | - | specified in skirmish_ai_v4_rl.md |

P-005 section 2.4 orders the work F and S, then R+L, then A. This spec puts A before R+L because R+L is not specified yet and A is (Q-1). Each package ends with `dotnet build` (no warnings) and `dotnet test` green, a `sim-reviewer` pass, and its gate run (section E). A Sonnet builder builds each package; a task that fails twice moves up a tier with its failing test.

---

## Package F: raid response and economy bugs

### F.0 What fails today and why (verified against the code and one traced run per scenario)

Counted response scenarios on `f4241f5` with `aggressive_s3` (water `armoured_answer` is not counted, as in every report since WC-3): **5 of 9 pass** (`air_answer` both, `trade` both, `turtle` water). The four counted failures:

| Scenario | Failing part | Cause, from the code and the trace |
| --- | --- | --- |
| `worker_raid_skai` | (1) `static_defence_near_raided_base=false`; (2) 7 workers lost in the 120 to 180 s window | See the three causes below. (1) is cause 3. |
| `worker_raid_water` | (2) 5 workers lost in the 120 to 180 s window | Causes 1 and 2. Its part (1) passes. |
| `mass_pummeler_skai` (`armoured_answer`) | (1) share gain -0.012, needs 0.15 | Counter-mix share; not a raid or mechanics fault. **Not targeted by this spec** (Q-11). |
| `turtle_skai` | (1) 18 supply lost before the first structure kill, limit 16 | Skai siege; Skai has no `structure_damage_multiplier` weapon, so A3's predictor change does not help it. **Not targeted** (Q-11). |

The raid is at the expansion, not the main. In both raid traces the raiders (the responder's own early Windrunners from 116 s, then the scripted waves at 120 s and 240 s at (23, 23)) kill workers at the natural depot (Skai `perch_depot` at (27.5, 13.5); water `pearl_depot` at (27.5, 12.5)), about 15 tiles from the HQ at (12.5, 12.5) and so outside the HQ weapon's 6 tiles. Both depots are destroyed (Skai at 177.8 s and again at 253.8 s; water at 151.3 s).

1. **Workers never leave a raided field.** `Turn.Gather` (`AiBrain.cs`, rule G) orders only idle workers, and `Rebalance` only moves workers above a field's cap. Nothing moves a worker away from danger, so workers gathering beside the raiders keep gathering until they die (trace: Skai fledglings die at (26.9, 15.4) at 116.6, 118.0, 122.8, 124.3, 125.3, 167.1, 167.7, 168.3 and 175.2 s). New workers are trained into the same field (rule 4b counts the field's cap).
2. **The defence trickles.** `MeasuredDefence` (`AiBrainFight.cs`) adds candidates until the predicted advantage reaches `defend_min_advantage`, and when even all of them fall short it still sends all of them (the loop simply ends). Units are trained one or two at a time, so they arrive one or two at a time and die (trace: the Skai army never had more than 4 units alive between 120 and 330 s and lost 18 army units; the stance stayed `Massing` with a defend group of 0 to 3). The Rooted HQ is never counted on the defending side (only `IsStaticDefence` structures are).
3. **The raid tower is blocked by the value cap (Skai).** Rule SD step 2 (`Turn.StaticDefence`) needs one logged raid (`static_defence_after_raids` 1; the trace shows the raid logged on the depot at about 118 s) and then calls `WithinValueCap`: own defences plus the new one must be worth at most `static_defence_max_value_share` (0.15) of the army. With an army worth about 175 to 300 (1 to 3 units) the cap is 26 to 45, below a Perch Tower's 100, so step 2 never builds. Inference from the trace and the arithmetic; the cap's return value was not instrumented.

**The depleted-field bug** (P-005; `CHANGELOG.md` WC-2: "the AI re-issues gather orders to depleted fields from stale memory, seen as `field_depleted` rejections"). Code path: rule 0 (`UpdateIntel`) updates `AiMemory.KnownFieldRemaining` only for fields whose tile the team sees; `AiWorldView.Build` reports the remembered amount for fogged fields. A field depleted in the fog (by the enemy, or by own workers after vision moved on) stays "known > 0"; `Gather` (served field, or the long haul `TargetOrder` when starved) sends an idle worker; the Sim rejects `field_depleted` (`GatherCommand`, rejections in order `unknown_unit`, `not_owner`, `cannot_gather`, `unknown_field`, `field_depleted`, `no_drop_off`); the worker stays idle where it is, never sees the field, and the next think repeats the order. **Not reproduced on `f4241f5`**: zero `field_depleted` rejections in one 963 s water mirror trace (`--trace --map prototype_1v1 --matchup water_vs_water --game 0`, both `aggressive_s3`) and in six response runs. Test F-7 builds the case directly.

**Unreachable build sites** (known item): `SkirmishAiController.ReadEvents` adds a tile to `AiMemory.UnreachableSites` on a `BuildOrderCancelledEvent` with reason `unreachable`, and `IsGoodSite` skips it for the rest of the match, even after the wall that caused it dies.

### F.1 Rule WS: worker safety (in rule G, when `use_worker_safety`)

Memory: `FieldDangerUntilTick` (`int[]`, one per field, 0 at start; sized like `KnownFieldRemaining`). Not hashed.

1. **Mark danger** (start of `Gather`, before any order): for each field f in index order, f is marked when (a) a visible enemy army unit whose weapon can target the plan worker's layer (`Weapon != null && Weapon.CanTarget(workerDefinition.Layer)`) has its centre within `worker_danger_radius_tiles` of f's centre (squared compare), or (b) a `RecentDamage` report on an own worker (the report's unit is an own worker in this view) with tick `>= tick - damage_memory_seconds` lies within that radius of f's centre. Marked: `FieldDangerUntilTick[f] = tick + SecondsToTicks(worker_danger_memory_seconds)`. A field is **dangerous** while `tick < FieldDangerUntilTick[f]`.
2. **Dangerous fields hold no workers.** `CapOf(f)` returns 0 for a dangerous field. `NearestField` skips dangerous fields in both modes (also when no field is open). The long haul list skips dangerous fields. `Rebalance` skips dangerous fields (step 3 handles them). Rule 4b's `WantedWorkers` therefore stops training for them.
3. **Evacuate.** Then, before the idle-worker loop: every own worker, ascending ID, without a build order, whose `GatherFieldIndex` is a dangerous field, is ordered `Gather` to `NearestField(worker.Position, assigned, true)` (else the `false` mode), updating `assigned`. When that returns null: `Move(Standable(Home()))` if Home is not within `worker_danger_radius_tiles` of a visible enemy army unit that can target the worker's layer, else no order. At most `worker_evacuate_max_commands_per_think` orders; the rest wait for the next think.
4. Workers with a build order are never re-ordered (as today). A carrying worker ordered to another field keeps its load (Sim behaviour, unchanged).

### F.2 Rule DH: hold when outmatched (in `MeasuredDefence`, when `use_defend_hold`)

Only when `_threatCount > 0` (a visible threat; the blind "hit with no visible threat" branch is unchanged).
1. **The HQ defends.** Before the candidate loop: if the own HQ is Rooted, complete, has a weapon, and the threat centre is within `HQ.Weapon.RangeTiles + defend_hq_cover_margin_tiles` of the HQ position (squared), the HQ joins the fixed side (`FightPredictor.Group` over the static defences plus the HQ view; real health).
2. The candidate loop is unchanged. If after it `enough` is still false: **hold**. `H = Standable(Home())`. Every chosen unit (all candidates, after the attack-group rule below) joins `DefendGroup`; a unit farther than `defend_hold_radius_tiles` from H gets `Move(H)` unless it already has a move target within `SameGoalSquared` of H; a unit inside that radius gets no order (idle units fire at enemies in weapon range; Sim rule). No `Focus` call for held units.
3. The attack-group rule (`defend_recall_supply`, recall to stance Defend) is unchanged; a recalled group holds too when step 2 applies.
4. When `enough` becomes true on a later think (new units, or the threat walked into HQ range), the existing orders apply (attack-move to the threat centre, focus fire).

### F.3 Rule SD revision: a raid that kills workers earns its tower (when `raid_worker_losses_exempt` > 0)

Memory: `OwnWorkerPositions` (`SortedDictionary<EntityId, FixVec2>`, the own workers of the previous think) and `WorkerLossLog` (`List<(int Tick, FixVec2 Position)>`). Rule 0, each think with an HQ: every ID in `OwnWorkerPositions` that is not an own worker now is appended to the log with this think's tick and its last position (ascending ID); then `OwnWorkerPositions` is rebuilt from the view; log entries older than `raided_window_seconds` are dropped. (Own units only; no event is read. Workers do not leave the unit list except by dying.)

Rule SD step 2: a drop-off that qualifies by raids builds even when `WithinValueCap()` is false, if the log holds at least `raid_worker_losses_exempt` entries within `defend_radius_tiles` of that drop-off. Everything else in step 2 (per-base cap, one under construction, site choice) is unchanged.

### F.4 Depleted fields and unreachable sites

- **`use_depleted_rejection`**: in `SkirmishAiController.ReadEvents`, a `CommandRejectedEvent` with reason `field_depleted` whose command is a `GatherCommand` issued by this player sets `Memory.KnownFieldRemaining[FieldIndex] = 0` (index in range only). This is feedback on the AI's own command, which a human player also gets.
- **`unreachable_site_forget_seconds`**: `AiMemory.UnreachableSites` becomes `SortedDictionary<int, int>` (tile index to the tick it was added). `IsGoodSite` skips the tile while `forget == 0` (old behaviour, forever) or `tick < added + SecondsToTicks(forget)`. A re-cancel refreshes the tick.

### F.5 Profile keys (root of every profile)

| Key | Unit | Range | Old profiles | `aggressive_s4f` |
| --- | --- | --- | --- | --- |
| `use_worker_safety` | switch | true/false | false | true |
| `worker_danger_radius_tiles` | tiles | 1 to 30 | 10 | 10 |
| `worker_danger_memory_seconds` | seconds | 0 to 120 | 10 | 10 |
| `worker_evacuate_max_commands_per_think` | commands | 0 to 100 | 12 | 12 |
| `use_defend_hold` | switch | true/false | false | true |
| `defend_hold_radius_tiles` | tiles | 1 to 20 | 4 | 4 |
| `defend_hq_cover_margin_tiles` | tiles | 0 to 20 | 2 | 2 |
| `raid_worker_losses_exempt` | workers | 0 to 50 (0 = off) | 0 | 2 |
| `use_depleted_rejection` | switch | true/false | false | true |
| `unreachable_site_forget_seconds` | seconds | 0 to 3600 (0 = never) | 0 | 120 |

Why 10 tiles: the natural fields on `prototype_1v1` are about 15 tiles from the HQ, so a 10-tile danger radius around a raided natural does not reach the main fields; it equals `field_max_distance_tiles` (10) in the shipped profiles.

### F.6 Tests (`rts/tests/Sim.Tests/Ai/AiRaidTests.cs`; real `greybox_1v1` simulations as in `AiFightTests`, own units spawned by script, `AiBrain.Decide` on the view; `aggressive_s4f` unless stated)

Common setup: P0 Skai HQ at the greybox spawn as `AiFightTests.Greybox` places it, a complete `skai.perch_depot` of P0 at (18.5, 7.5), the map explored for P0 (`ExploreKit.ExploreAll`). Fields: 1 at (10, 4), 0 at (4, 10), 6 at (21, 10), 7 at (21, 4) (map file).

- **F-1 Evacuation.** Three P0 fledglings ordered to gather field 6 and stepped until their `GatherState` is not Idle; no worker on fields 0 and 1. A P1 Windrunner spawned at (21.5, 12.5). Decide: exactly three `Gather` intents, all to field 1 (centre (10.5, 4.5) is nearer to the workers than field 0's; field 7's centre is 8.0 tiles from the raider, inside 10, so it is dangerous). No intent targets field 6 or 7.
- **F-2 Danger memory.** After F-1, remove the Windrunner (script) and run Decide at +5 s with one idle fledgling at (20.5, 9.5): no `Gather` to field 6 or 7. At +11 s: the idle fledgling gets `Gather` to field 6 (nearest served field under cap).
- **F-3 Nowhere safe.** As F-1 but also P1 Windrunners at (10.5, 6.5) and (6.5, 10.5) (fields 0 and 1 dangerous): the three workers get `Move` to `Standable(Home())` only if the HQ position is farther than 10 tiles from every raider, else no order. With this setup the HQ is within 10 tiles of (10.5, 6.5): assert no order.
- **F-4 Switch off.** F-1 with `aggressive_s3`: no `Gather` intent for the three workers.
- **F-5 Hold.** One P0 `skai.talon_archer` at (14.5, 8.5); two P1 Windrunners at (20.5, 8.5) (inside the base through the depot). Compute `Advantage` of (talon) against (2 Windrunners) with the profile's predictor settings in the test; it is below 1.5 (assert). Decide: `Move(talon, Standable(Home()))` (the talon is more than 4 tiles from Home), no `AttackMove` and no `AttackUnit` for it; `DefendGroup` holds the talon. Same with `aggressive_s3`: `AttackMove` to `Standable(threat centre)`.
- **F-6 HQ joins.** As F-5 but the Windrunners at (12.5, 8.5) (inside the HQ's 6 + 2 tiles). Compute the advantage of (HQ + talon) against them in the test; when it is at least 1.5 the talon gets `AttackMove` to the threat centre (or `AttackUnit` from focus); assert the branch the computed value selects.
- **F-7 Depleted field.** HQ Rooted, one idle fledgling beside it. Set `Memory.KnownFieldRemaining` to 0 for every field but field 4 (rare, (8, 52), fogged), which stays 600; set the Sim's field 4 remaining to 0 (internal setter). Controller `Think`, step, `Think` again: the first think submits `gather` to field 4; the step rejects it `field_depleted`; after the second think `KnownFieldRemaining[4] == 0` and no `gather` to field 4 was submitted by the second think. With `use_depleted_rejection` false the second think submits it again.
- **F-8 Worker-loss exemption.** No P0 army (value cap fails), money for everything. `RaidLog[depot] = [tick - 10 s]`, `RaidPosition[depot] = (21.5, 9.5)`, `WorkerLossLog` two entries at tick - 20 s at (20.5, 9.5): Decide emits a `Build` of `skai.perch_tower` (the defence `PickDefence` returns) on a ring site around the depot. With one entry: no `Build` of a static defence.
- **F-9 Loss detection.** `OwnWorkerPositions` holds a fledgling ID that no longer exists, last at (20.5, 9.5): after Decide, `WorkerLossLog` has one entry with that position and this tick; the ID is gone from `OwnWorkerPositions`.
- **F-10 Unreachable site forgets.** `UnreachableSites[t] = tick0` for the first ring site t: not chosen at `tick0 + 119 s`, chosen at `tick0 + 121 s` (forget 120). With forget 0: never chosen.
- **F-11 Determinism.** F-1 and F-5 run twice: identical intents and memory.

### F.7 Response scenarios targeted and measurement

Targets: `worker_raid_skai` (both parts) and `worker_raid_water` (part 2). Expected mechanism: WS moves workers off the natural while raiders are there, DH stops the trickle, the SD exemption gives Skai its tower. Gate in section E.

---

## Package S: honest statistics in the harness

### S.0 What the numbers show today

- **Seeds barely vary games.** A game's seed reaches play in exactly two places: the AI attack-threshold jitter (`AiBrain.EnterMassing`, `attack_supply_jitter` 2, a draw of 0 to 2 supply on the first think and after each attack; under `use_predictor_attack` it decides only when nothing is known about the enemy, skirmish_ai_v3.md D.1) and the Sim's cycle pick when no valid pick exists at lock time (`CycleSystem.cs:486`). Seat and faction follow the game index, not the seed.
- **Measured:** in the R-3 ladder (216 games, 72 per cell), grouping by (slot-0 faction, slot-0 profile, result, end tick) gives 40 distinct games in `skai_vs_skai`, 30 in `skai_vs_water` and 20 in `water_vs_water` (90 of 216); one game repeats 16 times. The Wilson intervals in `ladder.md` treat all 216 as independent, so they are too narrow (P-005 inference, now measured).
- **Mirror and cross-faction games are pooled.** The LADDER fraction mixes the mirrors (which measure the AI) with `skai_vs_water` (which follows the faction: Skai won all 72 in R-3, whichever profile played it).

**Minimum change.** Package S adds no randomness; it makes the reports say how many games are distinct and computes intervals over distinct games. Making seeds produce different games is package R (outline at the end). Until R exists, the reports will show the low distinct fraction and the warning below, which is the honest state.

### S.1 Play signature (`GameRunner`, `GameRecord`)

`play_signature`: a 64-bit FNV-1a value (`StateHasher`, fresh instance per game) over every command the AI controllers submit, in submission order: after each `AiHost.ThinkAll`, for each controller in ascending slot, for each command in its `LastCommands`: `Write(command.Tick)`, `Write(command.Issuer)`, `Write(command.Sequence)`, then `command.WriteHash(hasher)`; at the end `Write(endTick)` and `Write(result)` (the result string). The setup commands of `StandardMatchStart` are the same in every game of a cell and are not hashed. It does not include the seed or the Sim RNG, so two games that play the same commands have the same signature. `GameRecord` gains `play_signature` (16 lowercase hex) as the JSON field after `state_hash`, in batch, worker, ladder and trace records. Tool-side only; nothing feeds back into a simulation.

### S.2 Distinct counts and intervals (`HarnessStats`, `LadderRunner`, `ReportWriter`)

For any set of records: `games`, `decided` as today; `distinct` = number of different `play_signature` values among the decided records. `fraction_distinct` = (candidate-winning distinct signatures) / `distinct` (ladder) or the faction's distinct wins (batch). The Wilson interval (`confidence_z`) is computed over distinct games only: successes = distinct winning signatures, n = `distinct`. The plain fraction over all decided games stays (it is the gate today), next to it.

- `ladder.md` and the batch report: every table gains the columns `distinct` and `distinct fraction` after `decided`; the `Wilson interval` column becomes `Wilson interval (distinct games)`. A line under the first heading: `Intervals count each distinct game once (play signature, skirmish_ai_v4.md S.1); games with the same signature are the same game.`
- **Warning** when a cell's `distinct / decided < min_distinct_fraction`: one line per such cell, `WARNING <map>/<matchup>: <distinct> distinct of <decided> decided games (<f>); seeds barely vary games, do not read these win rates as balance (P-005 decision 11).` Printed to stdout and in the report under the LADDER line. It never changes the exit code (Q-2).

### S.3 Mirror and cross-faction separation (`LadderRunner`)

- New table `## Mirror and cross-faction` with rows: `mirror` (every cell whose two factions are equal), `cross` (the rest), and one row per faction `cross, candidate as <faction>` (cross cells split by the candidate's faction in that game), each with the S.2 columns.
- The LADDER line gains, before `bar=`: `distinct=<n> mirror=<wins>/<decided> cross=<wins>/<decided>`. PASS and FAIL still use all decided games against the same bar (developer decision of 17 Nov 2026 on the 65 percent bar; whether to gate on mirrors only is Q-2).

### S.4 Dome stalemates in `--fights` (`FightRunner`, `FightReport`)

**What is verified.** The replica of `flat/750/skai.roc_rider/water.shellback` (4 Roc Riders against 4 Shellbacks): from 10 s to 60 s the Roc Riders fire 16 to 20 shots per 5 s, every one is absorbed by a dome (`ShieldDomeHitEvent`, about 168 absorbed per 5 s) and no `UnitDamagedEvent` is emitted. The Shellbacks cannot hit air. So this is a real stalemate (overlapping domes absorb in ascending dome ID, so the domes not being hit regenerate after `regen_delay_seconds` 4), not a missing event, and `no_damage` describes it wrongly. The other 11 Shellback stalemates were not checked one by one; S.4 makes the sweep classify them.

**Rule.** `FightRunner` also tracks `lastAbsorb`: the last tick with a `ShieldDomeHitEvent` whose `Absorbed > 0` and whose dome unit is an army unit of seat 0 or 1. When order 4 of micro_benchmark.md 4.5 fires (`t - lastDamage >= idle`), the reason is `dome_absorbed` if `t - lastAbsorb < idle`, else `no_damage`. The result stays `stalemate`. The `FIGHTS` line and `fights.md` count stalemates by reason. No settings key.

### S.5 Settings

| File | Key | Unit | Range | Value |
| --- | --- | --- | --- | --- |
| `tuning/harness_batch.json` (outside the content digest; micro_benchmark.md test 14 guards it) | `min_distinct_fraction` | fraction | 0 to 1 | 0.8 (P-005 bar item 3; placeholder) |

### S.6 Tests (`rts/tests/Sim.Tests/Harness/StatisticsTests.cs`, plus edits)

- **S-1 Signature ignores the seed.** Two `GameRunner.Play` calls on `prototype_1v1`, `water_vs_water`, game 0, cap 120 s (no cycle lock happens that early), both slots an edited `aggressive_s3` with `attack_supply_jitter` 0 (so the AI's only draw always returns 0), seeds 1 and 2: equal `play_signature`, different `state_hash` (the Sim RNG state is hashed). Same seed twice: equal signatures and equal hashes.
- **S-2 Signature changes with play.** Same game with `aggressive_s3` against `aggressive_s2b` in slot 1: a different signature.
- **S-3 Distinct arithmetic.** Synthetic records: 10 decided, signatures A A A B B C C C C D, candidate wins on A and C: `distinct` 4, `fraction` 0.700 (7 of 10), `fraction_distinct` 0.500, Wilson over (2, 4) at z 1.96 matches `HarnessStats.Wilson(2, 0, 2, 1.96)`.
- **S-4 Warning.** The same set with `min_distinct_fraction` 0.8: the warning line for that cell, exit code unchanged.
- **S-5 Mirror and cross.** Records in `skai_vs_skai` (4, candidate wins 3) and `skai_vs_water` (4: candidate as Skai wins 2 of 2, as water 0 of 2): `mirror=3/4 cross=2/4`, rows `cross, candidate as skai` 2/2 and `cross, candidate as water` 0/2.
- **S-6 Dome reason.** `--fights --only flat/750/skai.roc_rider/water.shellback`: `result` `stalemate`, `reason` `dome_absorbed`. A fight with no dome that stalls (any existing `no_damage` case without a Shellback, e.g. `flooded/1500/skai_ai_mix/water.grasper` if it still stalls; else a synthetic plan) keeps `no_damage`.
- Edits: `HarnessBatchTests`, `LadderTests` (A-2, A-4) and `HarnessTraceTests` assertions on whole record lines gain `play_signature`; `FightTests` that assert reasons.

### S.7 Measurement

Re-run the R-3 ladder (`--ladder --profile-a aggressive_s2b --profile-b aggressive_s3`) and the fight sweep (`--fights`) and attach both reports. Expected: about 90 of 216 distinct (warnings on all three cells); most Shellback stalemates reclassified `dome_absorbed`. No gate value.

---

## Package A: ability and mechanic use

Five steps, built in order A1 to A5; each is a mergeable unit with its own tests and review. `aggressive_s4.json` is created in A1 and each step turns on its switches there.

### A1. Gating hygiene (no switch: no shipped-data behaviour changes)

1. **Worker training respects `requires`.** `Turn.TrainWorkers` returns without an intent when `!RequirementsMet(worker definition)`. Verified bypass: it emits `AiIntent.Train(_hq.Id, worker.Id)` directly (`AiBrain.cs`, rule 4b), the only `Train` not behind `FindFreeProducer` (the others are `TrainScout` and `Produce`, both through `FindFreeProducer`).
2. **Requirement chains deeper than one.** Verified stall: `TechPrerequisite` (rule T2) takes the first unmet requirement R and calls `TryBuild(R)`; `TryBuild` returns false when R's own requirements are unmet, and nothing walks further. Change, in `TryBuild` only when no explicit site is given: when `definition`'s requirements are unmet, find `D` = follow the first unmet requirement from `definition` until a definition whose requirements are all met (at most as many steps as there are unit definitions; the loader rejects cycles, `DataLoader.CheckRequirementCycles`); if no `_planned` entry has definition `D`, call `TryBuild(D)` and return its result; else return false. With an explicit site (depot or defence site) it returns false as today. This covers rule T2, rule T (`TechProducer`), rule X and build-order steps.
3. Shipped data has depth 1 only (Thunderhead requires Storm Spire, Leviathan requires Reef Workshop, neither tech building requires anything), so both changes are latent today; the tests use edited data.

Tests (`AiPrerequisiteTests.cs`): load the shipped data with one file's text edited (the profile text-edit approach of `AiFightTests.S3Edited`, applied to a data file; add a small helper to `TestData` if none exists).
- **A1-1** `units/water/tender.json` gains `"requires": ["water.reef_workshop"]`: a water AI with a Rooted HQ and money emits no `Train` of `water.tender`; with a complete Reef Workshop it does.
- **A1-2** `units/skai/storm_spire.json` gains `"requires": ["skai.perch_tower"]`; `aggressive_s3` with `use_tech_on_demand`, mix weight 700 on the Thunderhead (as TA-2), a complete Windspire, no Storm Spire, no Perch Tower: Decide emits `Build` of `skai.perch_tower`. With a Perch Tower complete: `Build` of `skai.storm_spire`. With a pending Perch Tower order: neither.
- **A1-3** Existing TA-1 and TA-2 unchanged.

### A2. Casting: waves, Downdraft, and the Tidecaller

**View additions (`AiWorldView`, `AiUnitView`).** `AiUnitView.WaveReadyTick` (int, ticks; own units: `FloodingMechanic.TryGetNextWaveTick` value, 0 when never cast or when the match has no flooding mechanic; enemies 0) and `AiUnitView.GroundingReadyTick` (own units: `Unit.GroundingReadyTick`; enemies 0). `AiWorldView.WaveLengthTiles`, `WaveWidthTiles` (ints, from `sim.Tuning.Flood`). The synthetic pending-order views in the `Turn` constructor pass 0 for both.

**Intents.** `AiIntentKind.CastWave` (`AiIntent.CastWave(EntityId caster, FixVec2 target)`) and `AiIntentKind.CastGrounding` (`AiIntent.CastGrounding(EntityId caster, FixVec2 target)`); `AiCommandWriter` builds `CastWaveCommand` and `CastGroundingCommand` with the usual schedule tick and sequence.

**Shared helpers (`rts/src/Ai/AiAbilities.cs`, new, `public static`, pure; also used by A5).** "Ready" means `readyTick <= view.Tick + memory.CommandDelayTicks`.
- `WaveTiles(int casterX, int casterY, int ux, int uy, int length, int width) -> SortedSet<int>`: the strip of flooding.md section 5 (`s = 1..length`, `o = -(width-1)/2..(width-1)/2`, tile `(cx + s*ux - o*uy, cy + s*uy + o*ux)`, off-map skipped), as tile indices.
- `ChooseWave(AiWorldView view, AiUnitView caster, Fix64 rangedWeight, out FixVec2 target) -> Fix64 score`: for each of the 8 directions in `TerrainGrid.NeighbourDx/Dy` order, score = sum over visible enemy army units whose `Layer != Air` and whose tile is in the strip of `supply x (Weapon is melee ? 1 : rangedWeight)` (unarmed units count 0). Best score, ties to the lower direction index. `target = caster.Position + (4 ux, 4 uy)` (an offset whose direction the Sim's rule maps back to `(ux, uy)`: dominant axis for straight, neither dominant for diagonal).
- `ChooseGrounding(AiWorldView view, AiUnitView caster, out FixVec2 target) -> int score`: candidate points are the positions of visible enemy units with `Layer == Air` whose centre is within `GroundingAbility.RangeTiles` of the caster (squared). Score of a point = supply of visible enemy units with `Layer == Air` whose centre is within `RadiusTiles + their definition radius` of it (the Sim's test, combat_mechanics.md 4.2). Best score, ties to the lower candidate entity ID.

**Rule CW: cast waves (when `use_wave`).** In `Army`, after `UnitControl`, before the stance orders: for each own unit with `Definition.Flood.CanCastWave`, complete, ascending ID, wave ready: `score = ChooseWave(...)`; if `score >= wave_min_enemy_supply`, emit `CastWave(caster, target)`. At most `wave_max_commands_per_think`. A cast does not mark the unit controlled (casting does not touch orders).

**Rule CG: cast Downdraft (when `use_grounding`).** Same place: for each own unit with `GroundingAbility != null`, complete, ascending ID, ready: if `ChooseGrounding(...) >= grounding_min_air_supply`, emit `CastGrounding(caster, target)`. At most `grounding_max_commands_per_think`. Not controlled.

**Rule SU: support units (plan key `support_units`, root key `support_max_per_type`).** The unarmed Tidecaller is outside the counter-mix pool (`AiCounter.Pool` requires a weapon; missing_roster.md's pool guard), so the AI never trains it. `support_units` in each faction plan maps unit IDs to "one per this many army supply". In `Produce`, before the deficit loop: for each support unit in ordinal ID order, `wanted = min(support_max_per_type, armySupply / perSupply)` (integer division; army supply = supply of own complete army units, the scout excluded); `have` = own plus queued; if `have < wanted`: `FindFreeProducer(id)` non-null and `CanPay` and `HasSupply`: `Train`, mark the producer used; producer free but not affordable: reserve under rule R and stop `Produce` (as the deficit loop does). Validation: each ID is a unit of the plan's faction with `Supply > 0`, value 1 to 200 (error names the file and `factions.<faction>.support_units.<id>`).

**Rule ST: support trail (when `use_support_trail`).** Unarmed army units (`IsArmy && Weapon == null`) never get stance orders while the switch is on (added to `_controlled` each think). In stance Attack with a non-empty attack group: point P = mean position of the live armed attack-group units moved `support_trail_tiles` toward Home (`MoveTowards`), then `Standable`; otherwise P = `RallyPoint()`. A unit farther than `support_regroup_tiles` from P gets `Move(P)` unless already moving to within `SameGoalSquared` of P. Fall back (D.6.1) still applies first. A Tidecaller is a flood source wherever it stands, so trailing the army is how the AI "places" its flood (no separate placement rule; rejected extras).

**Keys.**

| Key | Where | Unit | Range | Old profiles | `aggressive_s4` |
| --- | --- | --- | --- | --- | --- |
| `use_wave` | root | switch | | false | true |
| `wave_min_enemy_supply` | root | supply (weighted) | 0.5 to 200 | 4 | 4 |
| `wave_ranged_weight` | root | fraction | 0 to 1 | 0 | 0 |
| `wave_max_commands_per_think` | root | commands | 0 to 40 | 4 | 4 |
| `use_grounding` | root | switch | | false | true |
| `grounding_min_air_supply` | root | supply | 1 to 200 | 3 | 3 |
| `grounding_max_commands_per_think` | root | commands | 0 to 40 | 4 | 4 |
| `use_support_trail` | root | switch | | false | true |
| `support_trail_tiles` | root | tiles | 0 to 20 | 3 | 3 |
| `support_regroup_tiles` | root | tiles | 1 to 20 | 3 | 3 |
| `support_max_per_type` | root | units | 0 to 20 | 3 | 3 |
| `support_units` | each faction plan | army supply per unit | 1 to 200 each | `{}` | water `{ "water.tidecaller": 12 }`, skai `{ "skai.stormcaller": 20 }` (Q-5) |

`wave_min_enemy_supply` 4 = two Pummelers in the strip. `grounding_min_air_supply` 3 = one Roc Rider.

**Tests (`rts/tests/Sim.Tests/Ai/AiAbilityTests.cs`; `greybox_1v1`, which is flat at height 1 for x 14 to 33, y 15 to 19 (map file); `aggressive_s4`).**
- **A2-1 Strip.** `WaveTiles(18, 17, 1, 0, 6, 3)` = x 19..24, y 16..18 (18 tiles). `(1, 1)` from (18, 17): the 18 tiles `(18 + s - o, 17 + s + o)`.
- **A2-2 Wave against a melee ball.** P0 water: Tidecaller at (18.5, 17.5). P1: Pummelers (`water.grasper`) at (21.5, 17.5), (22.5, 17.5), (23.5, 18.5) and a Brinecaster (`water.ink_lobber`) at (18.5, 19.5). Decide: one `CastWave(tidecaller, (22.5, 17.5))` (direction (1, 0), score 6; direction (0, 1) holds the Brinecaster, score 0). After the scheduled step: `WaveCastEvent` dir (1, 0) and three `UnitPushedEvent` for the Pummelers, each moved 2 tiles in +x. With only the first Pummeler: score 2 < 4, no cast.
- **A2-3 Wave cooldown.** After A2-2, the next think (wave not ready): no cast. `WaveReadyTick` of the Tidecaller equals the cast tick + 600.
- **A2-4 Downdraft.** P0 Skai Stormcaller at (18.5, 17.5). P1 Roc Riders at (23.5, 17.5) and (24.5, 17.5). Decide: `CastGrounding(stormcaller, (23.5, 17.5))` (both points score 6; tie to the lower ID). After the step: both have `EffectiveLayer` Ground, `GroundingCastEvent.unitsGrounded` 2. Next think: no cast (cooldown 600 ticks). One Roc Rider grounded already by a script status and the other at (30.5, 17.5) (out of range 7): no cast.
- **A2-5 Fog.** A2-4 with the Roc Riders not visible to P0 (place them at (29.5, 17.5) and (30.5, 17.5): beyond the Stormcaller's sight 10 and range 7): no cast.
- **A2-6 Support training.** Water AI with a complete Tidepool (trains the Tidecaller), army of 12 supply, money: Decide emits `Train(tidepool, water.tidecaller)` before any composition `Train`. With 11 army supply: none. With 3 Tidecallers and 60 supply: none (max 3).
- **A2-7 Trail.** Stance Attack, attack group of 3 Pummelers around (30.5, 17.5), a Tidecaller at (18.5, 17.5): `Move(tidecaller, P)` with P computed by the formula; no `AttackMove` for it. With `use_support_trail` false: it gets the stance attack-move (v3).
- **A2-8 Determinism.** A2-2 and A2-4 twice: identical intents.

### A3. Water anti-air, splash aim, and the structure multiplier in the predictor

**View addition.** `AiWorldView.IsFlooded(x, y)`: true when the tile is visible to the team now and `TerrainTile.WaterDepth > 0` (any team's water, the Sim's "on water" rule, combat_mechanics.md 3.1). False on fogged tiles.

**Rule WA: stand on water against flyers (when `use_water_anti_air`).** In `Army` after the casts: for each own army unit u, ascending ID, not fallen back, not in the harass group, whose weapon has `CanTargetAirOnWater`, when a visible enemy unit with `Layer == Air` is within `water_aa_threat_tiles` of u:
- u's tile is flooded: add u to `_controlled` (it holds; no stance order this think). No intent.
- else: scan Chebyshev rings 1 to `water_aa_seek_tiles` around u's tile, ring by ring, ascending tile index within a ring; the first tile with `IsFlooded`, `!IsBlocked`: `Move(u, CentreOf(tile))` and add u to `_controlled`. None found: nothing.
At most `water_aa_max_commands_per_think` intents.

**Rule SA: aim blasts at clumps (when `use_splash_aim`, only with `use_focus_fire`).** In `FocusFastestKill`, for a unit whose weapon has `SplashRadiusTiles > 0`: among its candidate foes, first maximise the count of other visible enemy units whose layer the weapon can hit and whose centre is within `SplashRadiusTiles + their definition radius` of the foe's centre (squared; enemies only, since there is no friendly fire); then the existing order (lowest time to kill, nearer, lower ID). Uses the existing `AiProximity` foe index.

**Predictor: structure multiplier (when `predictor_structure_multiplier`).** In `FightPredictor.Dps`, when `target.Economy.FootprintTiles > 0`, multiply by `weapon.StructureDamageMultiplier` after the table multiplier (combat_mechanics.md 2.1 order; uphill after). The flag reaches `Dps` through `PredictorSettings` (new field `StructureMultiplier`, from the profile). Rooted enemy HQs never enter predictor entries today, so only static defences are affected. Only the Leviathan has a multiplier in shipped data.

**Keys.**

| Key | Unit | Range | Old | `aggressive_s4` |
| --- | --- | --- | --- | --- |
| `use_water_anti_air` | switch | | false | true |
| `water_aa_threat_tiles` | tiles | 1 to 30 | 8 | 8 |
| `water_aa_seek_tiles` | tiles (rings) | 1 to 16 | 6 | 6 |
| `water_aa_max_commands_per_think` | commands | 0 to 40 | 8 | 8 |
| `use_splash_aim` | switch | | false | true |
| `predictor_structure_multiplier` | switch | | false | true |

**Tests (`AiAbilityTests.cs`).**
- **A3-1 Seek water.** P0 water: HQ spawned as usual, Shellback at (18.5, 17.5) on a dry tile; tile (21, 17) gets 64 drops of team 0 water through `TerrainGrid.SetWater` in a test step hook (as `FloodingTests`), visible to P0. P1 Roc Rider at (22.5, 15.5). Decide within the first 40 ticks (so the HQ's own flood has not spread near the test area; assert no other flooded tile within 6 rings of (18, 17)): `Move(shellback, (21.5, 17.5))`. No flyer: no intent. Shellback already on (21, 17): no intent and no stance order for it this think.
- **A3-2 Fogged water is unknown.** Same scene: `AiWorldView.Build(sim, P0).IsFlooded(21, 17)` is true and `AiWorldView.Build(sim, P1).IsFlooded(21, 17)` is false (P1 has no unit that sees the tile).
- **A3-3 Splash aim.** P0 water Brinecaster at (18.5, 17.5), `use_focus_fire` and `use_splash_aim` on. P1 Windrunners: W1 at (22.5, 19.5) alone; W2 (24.5, 16.5), W3 (25.5, 16.5), W4 (24.5, 15.5). W2 has 2 neighbours within 1 + 0.4 (W3 at 1.0, W4 at 1.0); W3 and W4 have 1 (they are 1.414 apart). Decide: `AttackUnit(brinecaster, W2)`. With `use_splash_aim` off: W1 (nearest at equal time to kill).
- **A3-4 Predictor.** `FightPredictor.Dps(leviathan, coral_turret)` with the flag: 80 x 1.25 x 1.5 / 3 = **50**; without: **33.333** (tolerance 0.001). Against `water.grasper`: 80 x 1.0 / 3 = **26.667** either way.

### A4. Breaching a wall that seals the route (when `use_wall_breach`)

Assumes walls_and_tech.md Q-W1's default (a unit whose goal is unreachable stops with `MoveUnreachableEvent` and does not resume). The AI never builds walls; AI-against-AI games have none, so A4 matters against humans and in the `wall_in` scenario.

**Memory.** `UnreachableGoals` (`SortedDictionary<EntityId, (int Tick, FixVec2 Goal)>`): `SkirmishAiController.ReadEvents` records each `MoveUnreachableEvent` whose unit is an own live unit (overwriting). Entries of dead units and entries older than `wall_breach_memory_seconds` are dropped in rule 0.

**Rule WB.** In `Army` after rule WA: for each own army unit u, ascending ID, with an entry, not in `_controlled`, not fallen back, not harassing, whose weapon can target Ground:
1. Candidate walls: `KnownEnemyStructures` entries whose definition has `Economy.IsWall`. Choose the one minimising `|u - w| + |w - goal|` (`FixVec2.Length`, Fix64), ties to the lower entity ID.
2. Chosen wall visible now (in `VisibleEnemies`): `AttackUnit(u, wall)`. Not visible: `AttackMove(u, Standable(w.Position.MoveTowards(u.Position, wall_breach_standoff_tiles)))` (units shoot an unarmed structure in reach when no armed enemy is near; Sim rule).
3. No known wall: `AttackMove(u, Standable(u.Position.MoveTowards(goal, wall_probe_step_tiles)))`, so the unit walks toward the goal until it sees what blocks it.
4. u joins `_controlled`. At most `wall_breach_max_commands_per_think`. A unit whose new order is accepted and reaches its goal simply stops generating entries; when the wall dies the next stance order succeeds.

**Keys.**

| Key | Unit | Range | Old | `aggressive_s4` |
| --- | --- | --- | --- | --- |
| `use_wall_breach` | switch | | false | true |
| `wall_breach_memory_seconds` | seconds | 1 to 120 | 10 | 10 |
| `wall_breach_standoff_tiles` | tiles | 0.5 to 10 | 2 | 2 |
| `wall_probe_step_tiles` | tiles | 1 to 20 | 6 | 6 |
| `wall_breach_max_commands_per_think` | commands | 0 to 60 | 12 | 12 |

**New response scenario `wall_in`** (`response_wall_in_skai.json`, `response_wall_in_water.json`, the AI under test as Skai and as water). Copy `response_turtle_<faction>.json` (opponent water, `rt_turtle`, never attacks; drop its four Coral Turret spawns) and add tick-0 `spawn_unit` of `water.coral_wall` owned by slot 1 on every tile of the north-east start plateau (height 2) that is not a ramp and has a ramp tile of the plateau among its 8 neighbours. Computed from the map file (not checked in the Sim): 17 tiles, (73, 84), (73, 89), (74, 84), (74, 85), (74, 86), (74, 87), (74, 88), (74, 89), (87, 74), (87, 75), (88, 75), (89, 75), (90, 75), (90, 76), (91, 76), (92, 75), (92, 76). A tile that fails the spawn (occupied or blocked) is moved to the nearest tile that keeps the seal, noted in the file comment. Seal check (test A4-5): a ground unit at (55.5, 55.5) ordered to (83.5, 83.5) gets `MoveUnreachableEvent`.

New check kind `wall_in` in `ResponseChecks`: parameters `breach_by_seconds` 900, `until_seconds` 2400 (placeholders). Pass when (1) a wall owned by slot 1 is destroyed no later than `breach_by_seconds` and (2) the AI wins by `until_seconds`. Observer: first `UnitRemovedEvent` of a slot-1 unit whose definition `IsWall` (with health 0), and the match outcome. Baseline expectation (not measured): water fails both parts today (no flyer and no breach rule); Skai may pass through Roc Riders, which fly over walls and can shoot them.

**Tests (`AiAbilityTests.cs`).**
- **A4-1 Visible wall.** P1 `water.coral_wall` at tile (22, 17), visible to P0. P0 Talon Marksman at (18.5, 17.5) with `UnreachableGoals` = (tick - 1 s, (40.5, 17.5)). Decide: `AttackUnit(talon, wall)`; no stance order for the talon.
- **A4-2 Remembered wall.** The wall at (30, 17) known in `KnownEnemyStructures` (definition `water.coral_wall`) but not visible: `AttackMove(talon, (28.5, 17.5))`.
- **A4-3 No known wall.** `AttackMove(talon, (24.5, 17.5))`.
- **A4-4 Old entry.** Entry at tick - 11 s: no WB order.
- **A4-5 Seal and record.** The `wall_in` setup run in the Sim: the seal check above; then a controller test: P0 unit inside a ring of eight P1 walls around tile (18, 17), the controller orders a move out (or the test submits one), and after the next `Think` `UnreachableGoals` holds that unit with the goal.
- **A4-6 Air unit.** A Roc Rider with an entry: no WB order (flyers are never blocked; they never get the event).

### A5. Measuring abilities: `--fights` cast control (micro_benchmark.md section 7) and ablations

**Why this is the smallest way.** Bar item 2 asks for a measured positive effect per mechanic. The ladder ablation (the same AI with one switch off) measures it in whole games, but the games are few and barely vary (S.0). The fight mode measures the ability alone, deterministically, in about a minute, and may be read at any time (P-005 decision 11). Both are used: fights as the mechanism check, the ablation ladder as the gate.

**Fight settings (`scripts/fights/fights.json`).**

| Key | Unit | Range | Value |
| --- | --- | --- | --- |
| `controls` | list of control IDs | 1 to 2 of `attack_move`, `cast`, unique, `attack_move` first | `["attack_move", "cast"]` |
| `cast_profile` | AI profile ID | a file under `rts/data/ai` | `"aggressive_s4"` |

and composition `water_wave_mix` = `{ "water.grasper": 3, "water.shellback": 1, "water.ink_lobber": 1, "water.spoutling": 1, "water.tidecaller": 1 }` (`water_ai_mix` plus one Tidecaller; placeholder). **Validation change:** a composition may contain unarmed army units (`FightSettings.IsArmy`) as long as it holds at least one combat unit.

**Plan.** For each fight of today's plan, a `cast` twin is added right after it when either side contains a unit with `GroundingAbility != null` or `Flood.CanCastWave`. Its ID is the plain ID plus `/cast`. Seeds derive from the full ID.

**Cast control.** Exactly the `attack_move` control (micro_benchmark.md 4.4) plus, at tick S + 1 and every re-issue tick, for each seat in order: `AiWorldView.Build(sim, seat player)`, then for each own unit in ascending ID that has an ability, the rule of A2 with the thresholds of `cast_profile` (`ChooseWave` with `wave_ranged_weight` against `wave_min_enemy_supply`; `ChooseGrounding` against `grounding_min_air_supply`), submitting the cast with the next sequence. Readiness uses `CommandDelayTicks` 0.

**Record and report.** Record fields after `seat1`: `control` (`attack_move` or `cast`); after `alive1`: `casts0`, `casts1` (accepted casts per seat: `WaveCastEvent` plus `GroundingCastEvent` by owner). New `fights.md` section `## Ability effect` after the matrices: `| Fight | Left 0 (attack_move) | Left 0 (cast) | Left 1 (attack_move) | Left 1 (cast) | Casts 0 | Casts 1 | Result changed |`, one row per twin pair. The checks (micro_benchmark.md section 6) read `attack_move` fights only, so their results do not change.

**Ablation ladder.** New flag `--ablation` for `--ladder`: the bar is `ladder_ablation_fraction_min` (new `ai_eval.json` key, fraction, 0 to 1, value 0.60, P-005 option A "at least 60 percent of mirrors"; placeholder). The LADDER line ends `bar=<0.600> PASS|FAIL`. `--ablation` and `--non-regression` together are an error.

**Response check `ability_used`** (new kind): parameters `what` (`wave_cast`, `grounding_cast` or `air_shot_from_water`), `min_count`, `until_seconds`. Pass when the AI under test caused at least `min_count` such events in [0, `until_seconds`]: `WaveCastEvent` / `GroundingCastEvent` owned by slot 0; or `ShotFiredEvent` whose attacker is a slot-0 unit with `CanTargetAirOnWater` and whose target's `EffectiveLayer` is Air at that tick. New `ai_eval.json` entries reusing existing scripts:

| Script | Check | Parameters |
| --- | --- | --- |
| `response_mass_pummeler_water.json` | `ability_used` | `what` `wave_cast`, `min_count` 1, `until_seconds` 600 |
| `response_roc_rush_skai.json` | `ability_used` | `what` `grounding_cast`, `min_count` 1, `until_seconds` 600 |
| `response_roc_rush_water.json` | `ability_used` | `what` `air_shot_from_water`, `min_count` 1, `until_seconds` 600 |
| `response_wall_in_skai.json`, `response_wall_in_water.json` | `wall_in` | A4 |

**Tests (`FightTests.cs`, `ResponseTests.cs`, `LadderTests.cs`).**
- **A5-1 Plan.** With the shipped settings, `flat/750/skai.roc_rider/skai.stormcaller/cast` exists directly after `flat/750/skai.roc_rider/skai.stormcaller`, and `flat/750/skai.stormcaller/skai.roc_rider/cast` after its twin; `flat/750/skai.roc_rider/water.grasper` has no twin.
- **A5-2 Casts.** In `flat/750/skai.stormcaller/skai.roc_rider/cast` (Stormcallers in seat 0), `casts0 >= 1`; in the `attack_move` twin `casts0 == 0`. Determinism: the cast fight twice gives identical lines.
- **A5-3 Composition validation.** `water_wave_mix` loads; a composition of only `water.tidecaller` is rejected naming `compositions[i].units`.
- **A5-4 Check arithmetic.** Synthetic series for `ability_used` and `wall_in`: one pass and one fail each.
- **A5-5 Ablation bar.** Synthetic ladder records 200 decided, 121 wins: `--ablation` PASS (0.605); 119: FAIL; `--ablation --non-regression`: error exit 1.

---

## E. Acceptance per package (gate runs; attach every report)

| Package | NUnit | Response (`--response --profile <p>`) | Ladder (all on `prototype_1v1`, `ai_eval.json` defaults) |
| --- | --- | --- | --- |
| F | F-1 to F-11; A-11 and `BlindTraceHash` green | `aggressive_s4f`: both `worker_raid` scenarios pass; the five scenarios that pass with `aggressive_s3` still pass. **Knife-edge check** (bar item 1): both raid scenarios also pass with temporary profile copies (under `rts/build`, not committed) where `worker_danger_radius_tiles` is 8 and 12, and where `defend_min_advantage` is 1.2 and 1.8 (one key changed per copy; 8 runs) | `--ladder --profile-a aggressive_s3 --profile-b aggressive_s4f --non-regression` passes (at least 0.45) |
| S | S-1 to S-6 | none | S.7 runs, report attached, no bar |
| A1 | A1-1 to A1-3 | none new | none |
| A2 | A2-1 to A2-8 | `aggressive_s4` (A2 switches on): F's passes kept; `ability_used` wave and grounding pass | `--ladder --ablation --profile-a abl_s4_no_wave --profile-b aggressive_s4 --matchups water_vs_water` and `--ladder --ablation --profile-a abl_s4_no_grounding --profile-b aggressive_s4 --matchups skai_vs_skai` each pass (at least 0.60) |
| A3 | A3-1 to A3-4 | `ability_used` `air_shot_from_water` passes | `--ladder --ablation --profile-a abl_s4_no_water_aa --profile-b aggressive_s4 --matchups skai_vs_water`: report the fraction with the candidate as water (S.3 row); pass when that row is at least 0.60 |
| A4 | A4-1 to A4-6 | both `wall_in` scenarios pass | none (no walls in AI-against-AI games) |
| A5 | A5-1 to A5-5 | all of the above with `aggressive_s4` | `--ladder --profile-a aggressive_s3 --profile-b aggressive_s4 --non-regression`; `--fights` report with the "Ability effect" section attached |

Rules for every gate: `dotnet test` green, `sim-reviewer` pass on the `rts/src/Ai` and harness diffs, and the S.2 distinct counts quoted with every ladder number. If a scenario or a ladder fails, the builder reports the numbers and does **not** tune combat or unit data; keys in `aggressive_s4f` / `aggressive_s4` may be tuned only if the result also holds at the neighbouring values (the F knife-edge rule; for A the ability key at 0.8 and 1.2 times its value). Ladder results are a record of the AI, not a balance reading (P-005 decision 11).

Existing tests and files affected: every profile under `rts/data/ai` (new keys); `AiProfile` parse tests and hand-written profile JSON in tests; `AiUnitView` constructor callers (two new fields); `HarnessBatchTests`, `LadderTests`, `HarnessTraceTests`, `FightTests` (record fields); `ai_eval.json` (new entries and key; `AiEvalSettings` validation for the new check kinds and parameters); `fights.json` and `FightSettings` (new keys, composition rule); file headers of `AiBrain.cs`, `AiBrainFight.cs`, `AiWorldView.cs`, `AiIntent.cs`, `AiMemory.cs`, `SkirmishAiController.cs`, `FightPredictor.cs`, `rts/src/Ai/README.md`, `rts/tools/BalanceHarness/README.md`, `specs/balance_harness.md` (report columns), `specs/micro_benchmark.md` section 7 (mark the abilities row as built when A5 lands). No `rts/src/Sim` file, no shipped-data `STATE_HASH`, no replay format.

---

## R+L. Randomness and an opening library

Specified in [skirmish_ai_v4_rl.md](skirmish_ai_v4_rl.md) (18 Nov 2026): package R (a second seeded generator per AI, the base army mix within a band, the expansion clock and the attack edge jittered), package L (named openings per faction plan, drawn by weight at match start, handing over to the existing rules), package H (opening columns and warnings in the ladder report), the gate (variety, not weaker, no dominant or dud opening) and how package F's options are re-measured. The developer's P-005 order puts R+L before package A (coordinator, 18 Nov 2026), which answers Q-1 below the other way round from its default.

---

## Rejected extras (one line each)

- Spreading units against blasts: unit separation already spreads them; a formation controller is large and the fight mode does not vary formations yet.
- Flyers avoiding Stormcallers: needs threat maps; Downdraft has a 30 s cooldown, so the AI can learn it later from the ablation.
- Splash in the fight predictor: needs a clump model of both armies; the counter-mix stays splash-blind (combat_mechanics.md section 6).
- Anti-air on water in the predictor: needs a position-aware fight; rule WA moves units onto water without it, and the predictor stays pessimistic (safe) for Reef Runners and Shellbacks.
- A wave cast to wet own anti-air units: two-step planning for a small gain; the Tidecaller's trailing flood does it passively.
- Placing flood on routes (a separate Tidecaller placement rule): trailing the army covers where the AI fights; route flooding needs path knowledge.
- Workers fighting raiders: Windrunners outrun and outrange workers (3.5 against 2.5 tiles per second); fleeing keeps the economy.
- The AI building walls: walls_and_tech.md W.4 keeps it out; no response scenario needs it.
- A momentum hit-and-run rule: not in this request; keep-distance (D.6.2) already kites.
- Gating on intervals instead of point fractions: changes the developer's 17 Nov bar; asked as Q-2.

## Out of scope

- Fixing `armoured_answer` Skai and `turtle` Skai (Q-11). Water `armoured_answer` stays uncounted.
- Any `rts/src/Sim` change, any combat or unit data retune, any change to the developer's ladder bar (65 percent) or to `harness_batch.json` `ai_profile`.
- Packages R and L (specified in skirmish_ai_v4_rl.md); exploiters and a league (P-005 decision 13); the human benchmark (decision 12).
- Unity input or presentation for casts (PC session).
- walls_and_tech.md Q-W1 (units walking to the nearest reachable point): A4 assumes the built rule.

## Questions for the developer (the builder uses the defaults)

- **Q-1. Order of A and R+L.** P-005 puts R+L before A. *Default: F and S, then A, then the R+L spec,* because A is specified now and L's openings should include the mechanics.
- **Q-2. What the ladder gate reads.** Today PASS uses all decided games, pooled over mirrors and cross-faction cells. *Default: unchanged; the report adds distinct counts, intervals over distinct games, the mirror and cross rows and a warning.* Alternatives: gate on mirror cells only, or on the lower interval bound over distinct games.
- **Q-3. Workers under raid: flee or fight?** *Default: flee (rule WS); they fight only through the Sim's automatic retaliation when cornered.*
- **Q-4. Worker losses lift the static defence value cap.** *Default: 2 workers lost near the drop-off within `raided_window_seconds` (120 s) lift it for that drop-off.*
- **Q-5. Support units outside the counter-mix.** The Tidecaller is unarmed and never chosen by the counter-mix; the Stormcaller may not be chosen either, and Downdraft needs one. *Default: one Tidecaller per 12 army supply and one Stormcaller per 20, at most 3 of each,* trained before the counter-mix units.
- **Q-6. What waves are for.** *Default: push melee units only (`wave_ranged_weight` 0),* P-005's "answer a melee ball with a wave". Raising the weight also pushes ranged units away, which can save own melee units the chase.
- **Q-7. Ablation bar.** *Default: 0.60 (P-005 option A placeholder).*
- **Q-8. Do the new response entries count toward bar item 1** (all scenarios pass)? *Default: yes: `wall_in` both factions and the three `ability_used` entries join the counted set once their package is built (9 counted becomes 14).*
- **Q-9. Knife-edge neighbours.** *Default: each tuned key at 0.8 and 1.2 times its value (the F gate uses 8 and 12 tiles, and 1.2 and 1.8).*
- **Q-10. `wall_in` timings.** *Default: the first wall dies by 900 s; the AI wins by 2400 s.*
- **Q-11. The two failures this spec does not target** (`armoured_answer` Skai, share gain -0.012; `turtle` Skai, 18 supply against a 16 limit). Bar item 1 needs both. *Default: not addressed in v4; diagnose after F and A are measured, since splash, Downdraft and the structure multiplier may change both; no knife-edge retune of `max_supply_lost_before_first_kill`.*
- **Q-12. Downdraft targeting.** The AI casts only at visible enemy flyers, by the most flyer supply hit. *Default: no other rule (no pre-emptive casts, no casts timed to protect own units).*

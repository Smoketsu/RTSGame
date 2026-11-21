<!-- rts/docs/specs/interface_support.md: build spec for the five simulation gaps listed in player_interface.md section 4 (placement query, wave preview, ability names, cancel a queued entry, rally points), as packages I1 to I5, each independently buildable, smallest first. Written 21 Nov 2026 by the designer on the developer's decisions of that day. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by the sim-reviewer after each package. -->
# Spec: simulation support for the player interface (I1 to I5)

Written 21 Nov 2026 against `main` at `2c8cf1d`. I4 is written against the production queue **as research package R1 leaves it** (`review/p007-r1` at `427b063`: research IDs share `ProductionQueue`, `EconomyRules.QueueEntryTicks`, `Content.Researches`); build I4 only after R1 is merged. Sources: `specs/player_interface.md` section 4 and question 3; `specs/research_and_unlocks.md` R1.1 to R1.6; `specs/walls_and_tech.md` (style, setups); `specs/combat_mechanics.md` section 5 (re-pin procedure); the `Simulation.cs` header. Code read: `Commands/EconomyCommands.cs` (`TrainUnitCommand`, `BuildStructureCommand`, `CheckStaticDefenceCap`), `Economy/EconomyRules.cs`, `Economy/EconomySystem.cs` (`RecomputeSupply`, `StepProduction`, `CancelBuild`), `Units/Unit.cs` (queue methods, `WriteHash`), `Factions/Water/CastWaveCommand.cs`, `FloodingMechanic.cs` (`CastWave`, `BuildMasks`, `ReadField`), `FloodRules.cs`, `Data/UnitSchema.cs`, `Data/DataLoader.cs`, `Data/CommandScript.cs`, `Commands/Codec/*`, `Core/ContentCatalog.cs` (`ComputeDigest`), `Terrain/TerrainTile.cs`, `tests/Sim.Tests/TestWorld.cs`, `HashCoverageTests.cs`.

## 0. Developer decisions (21 Nov 2026, final) and ground rules

1. Cancel a queued unit: **yes, with a full refund of what was paid.** Research is queued through `train_unit` in the same queue, so it is cancelled the same way.
2. Rally points: **yes.**
3. Get every feature usable first; AI work later. Unit and combat numbers frozen.

- **No new tuning key** in any package, and no number changes. The only new data are four text keys (I3).
- **No new step.** I1 to I3 are read-only queries plus a refactor; I4 runs in step 2; I5 in step 2 and step 7b.
- **Shared code, not copies.** Each query is the command's own check, extracted; the command calls the extracted code. A query never mutates state, raises an event or draws from the RNG, and works between steps.
- **Pins: none move in any package** (I1, I2 refactor only; I3 text is outside the content digest; I4 adds a command only; I5 writes rally state to the hash only when set, I5.4). Every existing pinned hash (smoke, economy_smoke, `GoldenFinalHash`, `V2ReferenceHash`, `BlindTraceHash`, the AI and harness pins) must stay green unchanged: a moved pin is a bug in the package. If the sim-reviewer rejects the I5 conditional write, re-pin once as `combat_mechanics.md` section 5 describes, with comments naming this spec and I5.
- **AI:** no change in any package. The AI never sends `cancel_production` or `set_rally_point`; `AiWorldView` keeps copying own production queues as today and does not copy rally points. Every profile plays as before; `ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided` stays green with its pins.
- **Setups.** "Economy setup" = `TestWorld.StartedEconomy()`: 20 ticks/s, P0 200 common; `test.barracks` (footprint 1, trains `test.runner` and `test.worker`), `test.worker` costs 50 common, supply 1, `build_seconds` 5 (B = 100 ticks). "Flood setup" = the `FloodingTests` wave-caster world. "R1 setup" = the `ResearchTests` world (`test.lab`, `test.plans_a`).

---

## I1. Placement query for a single structure

### I1.1 Shared check

Move every check of `BuildStructureCommand.Apply` after `cannot_build`, and `CheckStaticDefenceCap`, into `EconomyRules`:

```
internal static string? BuildSiteProblem(Simulation sim, Player owner, string definitionId, int tileX, int tileY, bool fogSafe)
public static string? PlacementProblem(Simulation sim, PlayerId player, string definitionId, int tileX, int tileY)
```

`BuildSiteProblem` returns the first reason, in exactly today's order, or null: `unknown_definition`, `not_a_structure`, `wrong_faction`, `missing_prerequisite`, `out_of_bounds`, `site_unexplored`, `tile_blocked`, `tile_occupied`, `site_pending`, `no_base` and `static_defence_cap` (static defences only, the old `CheckStaticDefenceCap` made a private `string?` helper), `insufficient_resources`, `supply_cap` (`owner.SupplyUsed + SupplyOf(definition) > owner.SupplyCap`, reading the stored figures; it never calls `RecomputeSupply`).

`BuildStructureCommand.Apply` keeps `unknown_unit`, `not_owner`, `cannot_build`, then calls `EconomySystem.RecomputeSupply(sim)` (moved up from before the supply test; it is idempotent and the hashed supply is rewritten in step 7 anyway, so no hash moves), then `BuildSiteProblem(sim, owner, DefinitionId, TileX, TileY, fogSafe: false)`; a non-null result is the rejection. The acceptance path is unchanged (it looks the definition up again). Update the `Apply` summary to point at `BuildSiteProblem`.

`PlacementProblem` (for Unity's ghost) returns `cannot_build` when `player` has no record, else `BuildSiteProblem(..., fogSafe: true)`. Its supply figures are those left by the last step's step 7, so it can say `supply_cap` where the command, after a death in step 8 or 9, accepts; the simulation decides (player_interface.md rule 4).

### I1.2 Fog (how the query reveals nothing hidden)

Today the command's own rejections leak two facts about explored but not currently visible tiles: a hidden unit (`tile_occupied`) and a hidden structure built since the tile was last seen (`tile_blocked` from `Footprint`, `BuiltWall`, `Gate`). With `fogSafe` true and `!sim.Vision.IsTileVisible(team, x, y)` (team = `TeamOf(owner.Id)`):
- `tile_blocked` only when `(Blockers & TileBlockers.Wall) != 0` (map walls are public map knowledge, as in the AI view);
- `tile_occupied` counts only units whose owner is on `team`.
On a visible tile, and always for the command, the checks are unchanged. `site_unexplored` stays before `tile_blocked`. `site_pending`, `no_base` and `static_defence_cap` read own units only. Whether the command itself should stop leaking is open (Q1).

### I1.3 Acceptance tests (`tests/Sim.Tests/Economy/PlacementQueryTests.cs`)

- **IS-1 Agreement.** Economy setup, P0's worker. For each case, call `PlacementProblem` between steps, then submit `build_structure` for the next tick: the query equals the rejection reason (null equals accepted). Cases: `unknown_definition`; `not_a_structure` (`test.runner`); `missing_prerequisite`; `out_of_bounds` (x = width); `site_unexplored`; `tile_blocked` (a footprint on a visible tile); `tile_occupied` (own unit); `site_pending` (another own worker's order on the tile); `no_base` (`test.tower` far from drop-offs); `static_defence_cap` (base full); `insufficient_resources` (common 0); `supply_cap`; and an accepted free tile.
- **IS-2 Order.** A tile with an own unit on it and an own pending order gives `tile_occupied`; an unexplored tile with a wall gives `site_unexplored`; an unaffordable tower with no base gives `no_base`. Query and command agree in each.
- **IS-3 Fog.** P1 unit on a tile P0 explored but does not see now: query is not `tile_occupied` (null on a free tile), the command still rejects `tile_occupied`. P1 tower spawned on such a tile: query is not `tile_blocked`, command is. A map wall on such a tile: query `tile_blocked`. Same tile made visible: query equals command.
- **IS-4 Pure.** Between steps, `PlacementProblem` does not throw; `ComputeStateHash` before and after 100 calls is equal; the event log is unchanged.
- **IS-5 Unchanged.** All `WorkerConstructionTests`, `WallTests`, prerequisite tests and every pin pass unchanged.

---

## I2. Wave preview query

### I2.1 Shared code (all in `Factions/Water`)

1. `internal static string? WaveDirection(Simulation sim, FixVec2 from, FixVec2 target, out int dirX, out int dirY)`: today's same-tile test and axis rule, moved verbatim out of `CastWaveCommand`; returns `no_direction` or null. The command calls it where the code stands today (after `on_cooldown`).
2. `private static void CollectWaveArea(TerrainGrid terrain, int casterTile, int dirX, int dirY, int lengthTiles, int widthTiles, SortedSet<int> area)`: the s/o loop of `CastWave`, moved.
3. `private bool WaveGains(FloodField field, int tile, int team)`: `FloodRules.Accepts(field, tile, team, null) && Math.Min(WaveDrops, FloodRules.Room(field, tile)) > 0`. `CastWave` floods exactly the area tiles where it is true.
4. `BuildMasks(sim, field, TeamId viewer)`: with `TeamId.None` (the cast and every other caller) unchanged; with a team, a drop-off whose owner is not on `viewer` and that `!sim.Vision.IsUnitVisibleTo(viewer, unit)` is skipped as an anchor, with the fields it claims.

### I2.2 Query

`public string? PreviewWave(Simulation sim, Unit caster, FixVec2 target, List<int> area, List<int> floods)` on `FloodingMechanic`. Clears both lists. Returns the first of `not_wave_caster` (`!CanCast`), `out_of_bounds`, `no_direction`, leaving the lists empty. `on_cooldown` is not checked (the strip is the same; Unity shows the cooldown sweep). Otherwise fills `area` with every covered tile and `floods` with the area tiles that are visible now to the caster's team and pass `WaveGains` on a field from `ReadField` with `BuildMasks(sim, field, team)`, both ascending tile index. Tiles not visible are in `area` only, so Unity draws them as unknown. Hidden water, footprints and hidden enemy drop-offs never change the result. It allocates a map-sized field per call: Unity calls it only when the caster or target tile changes.

### I2.3 Acceptance tests (`tests/Sim.Tests/Factions/WavePreviewTests.cs`)

- **IS-6 Agreement.** Flood setup with every tile visible to the caster's team: `PreviewWave` between steps, then `cast_wave` on the next tick with the same target: `floods` equals the set of tiles whose `WaterDepth` rose, its count equals `WaveCastEvent.TilesFlooded`, and away from map edges `area` has `wave_length_tiles * (2 * ((wave_width_tiles - 1) / 2) + 1)` tiles. Four directions plus one diagonal.
- **IS-7 Reasons.** Target in the caster's tile: `no_direction`; target beyond `WorldBoundTiles`: `out_of_bounds`; a non-caster: `not_wave_caster`; lists empty in each. Right after a cast (on cooldown): null and lists filled.
- **IS-8 Fog.** Area tiles outside the team's vision are in `area` and not in `floods`. An enemy drop-off hidden in fog whose no-flood mask covers a visible area tile: that tile is in `floods` (the cast itself will not flood it).
- **IS-9 Pure and unchanged.** As IS-4; all `FloodingTests` and pins pass unchanged.

---

## I3. Ability display names in data

### I3.1 Keys

Inside `grounding_ability`, as its **first two keys** (so the `GroundingAbilityTests` text replacements on the later keys still match):

```
// Name of the ability shown to players. Unit: text. Range: non-empty, 1 to 40 characters. Source: Factions / Grounded (Stormcaller Downdraft).
"display_name": "Downdraft",
// One-line ability description shown to players. Unit: text. Range: non-empty, 1 to 200 characters. Source: placeholder, developer to confirm (rts/docs/specs/interface_support.md I3).
"description": "Slams enemy flyers near the target point to the ground.",
```

Top level, after `can_cast_wave`, in units with `can_cast_wave: true` only:

```
// Name of the wave ability shown to players. Unit: text. Range: non-empty, 1 to 40 characters; only with can_cast_wave true. Source: placeholder, developer to confirm (rts/docs/specs/interface_support.md I3).
"wave_display_name": "Wave",
// One-line wave description shown to players. Unit: text. Range: non-empty, 1 to 200 characters; only with can_cast_wave true. Source: placeholder, developer to confirm (rts/docs/specs/interface_support.md I3).
"wave_description": "Floods a strip ahead of the caster and pushes enemies back.",
```

Files: `rts/data/units/skai/stormcaller.json`, `rts/data/units/water/tidecaller.json` (the only units with either ability).

### I3.2 Types and loader

`GroundingAbilityDefinition.DisplayName`, `Description`; `UnitFloodDefinition.WaveDisplayName`, `WaveDescription` (strings; empty when not loaded from data). Constructors take them as optional trailing parameters defaulting to empty, so code-built test definitions compile unchanged. They are **not** written by either `WriteHash`, so the content digest does not move (display names are already left out, `ComputeDigest` summary). Loader: `ReadGroundingAbility` adds both keys to `RejectUnknownKeys` and requires them (`RequireNonEmptyString`, length 40 / 200 else `Fail("must be 1 to 40 characters")` / `"... 200 ..."`). `DataLoader` adds `wave_display_name`, `wave_description` to the unit key list; `ReadFlood` requires both when `can_cast_wave` is true and rejects either with `Fail("is only allowed with can_cast_wave true")` otherwise.

### I3.3 Acceptance tests (`tests/Sim.Tests/Data/AbilityNameTests.cs`)

- **IS-10 Shipped.** Stormcaller: `GroundingAbility.DisplayName` "Downdraft"; Tidecaller: `Flood.WaveDisplayName` "Wave"; descriptions non-empty.
- **IS-11 Loader.** Rejected with file and field: `grounding_ability` without `display_name`; an empty `description`; a 41-character name; `wave_display_name` on a unit without `can_cast_wave`; `can_cast_wave: true` without `wave_description`. Changing a name leaves `ComputeDigest` equal. `GroundingAbilityTests` pass unchanged.

---

## I4. Cancel a queued production entry (after R1)

### I4.1 Command `cancel_production`

`CancelProductionCommand(int tick, PlayerId issuer, uint sequence, EntityId producer, int index, string definitionId)` in `Commands/EconomyCommands.cs`. Fields: `Producer`; `Index` (queue position, unit: entries, range 0 = head to `ProductionQueue.Count - 1`); `DefinitionId` (the unit or research ID the player saw at that position; guards against the queue having shifted under input delay). Rejections, in order: `unknown_unit`, `not_owner` (issuer not the owner; issuer None may command any unit), `not_owner` (the owner has no player record), `no_entry` (`Index < 0` or `>= Count`, so an empty queue too), `entry_mismatch` (`ProductionQueue[Index]` differs, ordinal). A rejection changes nothing.

On acceptance, in this order:
1. **Refund in full** to `producer.Owner` (the current owner, also after a hand-over): `CostCommon` and `CostRare` from `Content.Units`, else `Content.Researches`, through `Player.AddResource` as `CancelBuild` does.
2. **Remove.** `Index == 0`: `FinishProductionHead(Count > 1 ? QueueEntryTicks(sim, ProductionQueue[1]) : 0)`: the head's progress is lost and the next entry starts at its full length (step 7b of the same tick counts it once, as for a fresh `train_unit`). `Index > 0`: new `internal void Unit.RemoveQueuedProduction(int index)` (throws `ArgumentOutOfRangeException` for index < 1 or >= Count); the head and its countdown are untouched.
3. `EconomySystem.RecomputeSupply(sim)`: a unit's reserved supply is released (research reserves none).
4. Event `ProductionCancelledEvent(Tick, Producer, Owner, DefinitionId, Index, RefundCommon, RefundRare)` in `Events/EconomyEvents.cs`.

Also allowed while the producer is paused (Uprooted HQ) and when the head is finished but waiting for a spawn tile (full refund, no unit). A cancelled research item is no longer `IsResearchQueued` and may be queued again, paid again. Losing the building still refunds nothing (R1.1 rule 8). Two cancels on one tick apply in (issuer, sequence) order, each against the queue as the previous left it. `WriteHash`: Kind, Producer, Index, DefinitionId.

### I4.2 Codec, script, docs

`CancelProductionCodec` in `CoreCommandCodecs.cs` (kind `cancel_production`; `WriteEntityId`, `WriteInt32`, `WriteString` in field order) plus its `RegisterAll` line; script kind `cancel_production` in `CommandScript` with keys `producer`, `index` (0 to `max_queue_length` maximum), `definition`. Header edits: `Simulation.cs` step 2 "(cancel_production removes a queued entry and refunds it in full)"; `EconomySystem.cs` production paragraph "there is no refund (placeholder)" becomes "cancel_production refunds an entry in full; a lost producer refunds nothing". R1's four research files: the cost comments' "not refunded" becomes "refunded in full only by cancel_production". No hash layout change (the queue is already hashed).

### I4.3 Acceptance tests (`tests/Sim.Tests/Economy/CancelProductionTests.cs`; economy setup, barracks spawned complete)

- **IS-12 Waiting entry.** T: two `train_unit` worker (common 100, `SupplyUsed` +2). T + 10: cancel index 1: accepted, common 150, `SupplyUsed` +1, queue length 1, head `ProductionTicksRemaining` 89 after the step (unchanged by the cancel), event `(T + 10, barracks, P0, test.worker, 1, 50, 0)`; the head worker appears on T + 99.
- **IS-13 Head in progress.** As IS-12, cancel index 0 at T + 50: common 150; no worker on T + 99; the second worker has 99 ticks left after the step and appears on **T + 149**.
- **IS-14 Only entry.** One worker queued, cancel index 0: common 200, `SupplyUsed` back to its value before the order, queue empty, `ProductionTicksRemaining` 0, no unit through T + 200.
- **IS-15 Rejections.** `unknown_unit`; issuer P1: `not_owner`; index 2 of 2, index -1, index 0 of an empty queue: `no_entry`; right index, `test.runner`: `entry_mismatch`; index 5 with a wrong ID: `no_entry`. Stock, supply, queue unchanged; no event.
- **IS-16 Same tick.** Three workers queued; on one tick cancel index 1 (seq s) and index 1 (seq s + 1): both accepted, refund 100, the head remains. Two queued, cancel index 1 twice: the second gives `no_entry`.
- **IS-17 Paused HQ.** HQ queues a worker, is uprooted; cancel index 0 while Uprooting: full refund.
- **IS-18 Research (R1 setup).** Queue `test.plans_a`, cancel it on T + 10: common and rare back to their values before the order, `IsResearchQueued` false, no `ResearchCompletedEvent` through T + 100, `HasResearched` false; queuing it again is accepted and paid again.
- **IS-19 Codec and script.** Round trip through `BuiltInMechanics.CreateCodecRegistry()` and through a script line; the codec fixture covers the new class.
- **IS-20 Pins.** Every existing pin unchanged; the same script with cancels run twice gives equal hashes.

---

## I5. Rally points

### I5.1 State

On `Unit`: `HasRallyPoint` (bool, false at spawn) and `RallyPoint` (`FixVec2`, tiles, `FixVec2.Zero` when none; within ±`WorldBoundTiles`), set by `internal SetRallyPoint(FixVec2)` and `ClearRallyPoint()`. Only meaningful on a producer: `Economy.Trains` non-empty or `Hq != null`.

### I5.2 Command `set_rally_point`

`SetRallyPointCommand(int tick, PlayerId issuer, uint sequence, EntityId producer, bool clear, FixVec2 point)` in `Commands/EconomyCommands.cs`; `Point` is stored as Zero when `Clear`. Rejections, in order: `unknown_unit`, `not_owner`, `not_a_producer` (no `trains` and not an HQ; research-only buildings and towers), `out_of_bounds` (`!Simulation.IsWithinWorldBounds(point)`, not checked when `Clear`). Allowed under construction and in any HQ state. Any in-bounds point is accepted, blocked, unexplored or unreachable included (as `move_unit`). No event: the point is state that Unity reads. `WriteHash`: Kind, Producer, Clear, Point. Codec `SetRallyPointCodec` (`WriteEntityId`, `WriteByte` 0 or 1, `WriteFixVec2`; any other byte throws `CommandCodecException`); script kind `set_rally_point` with `producer` and either `x`, `y` or `"clear": true`.

### I5.3 Trained units

In `StepProduction` (step 7b), after `SpawnUnit` and `UnitTrainedEvent`: when `producer.HasRallyPoint` and the new unit's `SpeedTilesPerSecond > 0`, `created.SetMoveTarget(producer.RallyPoint)`. That is all `move_unit` does to a new unit (it has no build, combat or gather order to clear), so the unit behaves exactly as if `move_unit` to the point had been applied on the next tick: it starts moving in step 5 of the following tick, stops at the point, and an unreachable point gives what a move order gives (`MoveUnreachableEvent` from `MovementSystem`). Workers get the same plain move, even onto a resource field (Q2). The producer dying removes its rally point with it; units already sent keep their order. A hand-over keeps the point with the producer. Header edit: `Simulation.cs` step 7 "production queues (a trained unit is sent to its producer's rally point)".

### I5.4 Hash, visibility, AI

`Unit.WriteHash` appends, after `Statuses.WriteHash`: `if (HasRallyPoint) { Write(true); Write(RallyPoint); }`. Without a rally point no byte is written, so no existing pin moves (no shipped script, AI or test sets one). Add `Unit.RallyPoint` to `HashCoverageTests`. Visibility: own only. No event carries it, `AiWorldView` does not copy it, and Unity draws it only for the local player's producers.

### I5.5 Acceptance tests (`tests/Sim.Tests/Economy/RallyPointTests.cs`; economy setup, barracks spawned complete)

- **IS-21 Rally then train.** T - 1: rally at point P on open ground 6 tiles away. T: train a worker. On T + 99 it spawns with `HasMoveTarget` and `MoveTarget == P`; its positions on every tick T + 100 to T + 400 equal those in a control run with no rally and `move_unit(worker, P)` on T + 100; it ends within arrival radius of P.
- **IS-22 None and clear.** No rally: the new unit has no move target (today's behaviour). Set then clear before the spawn: same.
- **IS-23 Rejections.** `unknown_unit`; issuer P1: `not_owner`; `test.tower`: `not_a_producer`; point beyond `WorldBoundTiles`: `out_of_bounds`; the same with `clear`: accepted. Barracks under construction: accepted.
- **IS-24 Unreachable.** P on a blocked tile (a footprint or map wall): the worker gets the move order, and its positions and `MoveUnreachableEvent`s match the IS-21 control run with that P.
- **IS-25 Producer lost.** Rally set, two workers trained and sent; `remove_unit` the barracks: both keep `MoveTarget == P`, nothing throws.
- **IS-26 Hash and codec.** Setting a rally point changes `ComputeStateHash`; clearing it restores the earlier value. Codec and script round trips. Every existing pin unchanged.

---

## Out of scope

Unity work (the PC session updates UI-2, UI-3 and UI-5 after each package); the AI using cancel or rally; rally on a unit, attack-move rallies, queued waypoints; cancel of build orders (Stop already refunds); last-seen structures in fog; a fog-safe `build_structure` (Q1); wall-line previews beyond `WallLineTileProblem`.

## Questions for the developer

1. **Fog probe through `build_structure` rejections.** Today a player can learn about hidden units and structures from `tile_occupied` and `tile_blocked` on explored tiles. Default: leave the command as it is (I1 hides it from the ghost only). Alternative: make the command fog-safe like the query; the walk-up check then cancels with a refund (moves AI-game pins).
2. **Worker rally on a resource field.** Default: a plain move, the player orders gathering. Alternative: a worker whose rally tile is a resource field tile starts gathering there under `gather` rules.
3. **Rally kind.** Default: move to a point. Alternatives: attack-move to the point, or follow a unit.
4. **Ability texts.** "Downdraft" comes from the design; "Wave" and both descriptions are placeholders. Default: as written in I3.1.

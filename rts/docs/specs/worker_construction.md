<!-- rts/docs/specs/worker_construction.md -->
<!-- Build spec for the worker construction rule (developer decision, 18 Nov 2026): a build order names a worker, the worker walks to the site, spends a short placement time there, then the structure appears and finishes on its own. Covers the simulation state machine, hash order, events, tuning key, AI changes, scripts, re-pinned hashes and acceptance tests, split into packages WC-1 (Sim), WC-2 (AI, harness, scripts) and WC-3 (AI re-baseline). Part of rts/docs/specs (index: README.md); built by Sonnet builders, reviewed by sim-reviewer. -->
# Spec: worker construction (walk to the site, place, then build alone)

## 0. Purpose, source and packages

Source: the developer, 18 Nov 2026, verbatim: "Workers should have to go over and spend a couple seconds constructing." The orchestrator's interpretation (section 11 lists each default as a question to confirm): the build command names a worker; the worker walks to the site; it stays busy there for a placement time (tuning, 2 s); then the structure appears under construction and finishes by its existing `build_seconds` without the worker, who goes back to its previous gathering job.

Today (`rts/src/Sim/Commands/EconomyCommands.cs`, `BuildStructureCommand.Apply`) the command checks cost, supply, faction, bounds, blocked and occupied tiles and the static defence cap, and spawns the structure on the same tick on any tile. The worker never moves, is not involved, and the fog is not checked. `STATUS.md` lists this as an open design question.

Built against `main` at `62685a8` (18 Nov 2026). AI v3 package D (`aggressive_s3`) is being built now and also edits `rts/src/Ai/AiBrain.cs`. Build WC-2 after D is merged, or rebase it onto D. WC-1 does not touch `rts/src/Ai`.

| Package | Changes | Touches | Review |
| --- | --- | --- | --- |
| **WC-1** Sim | Build order state on `Unit`, new `BuildStructureCommand` rules, the economy state machine, cancellation hooks, refunds, pending orders in the supply and static defence checks, fog check, new events, one tuning key, `economy_smoke.json`, Sim tests, re-pinned Sim and harness hashes | `rts/src/Sim/**`, `rts/data/tuning/economy.json`, `rts/data/scripts/economy_smoke.json`, `rts/tests/Sim.Tests` (not `Ai/`) | **sim-reviewer (determinism), then Fable final review** |
| **WC-2** AI, harness | AI view of build orders, builder choice, pending orders counted as structures, a scouting trip to unexplored sites, cancel handling, harness trace, AI tests, A-11 and C2-11 re-pinned; the two `Rts.Net.Tests` that run AI matches (`LockstepTests.cs:59`, `MultiPeerLockstepTests.cs:77`, "not decided by tick 12000") must be decided again | `rts/src/Ai/**`, `rts/tools/BalanceHarness/GameTrace.cs`, `rts/tools/BalanceHarness/README.md`, `rts/tests/Sim.Tests/Ai`, `rts/tests/Sim.Tests/Harness` | **sim-reviewer on the `rts/src/Ai` and harness diff** (same rule as AI v3 packages) |
| **WC-3** re-baseline | Ladder, response and batch runs on the new rule; a report that also gives the `site_occupied` cancellation counts. No code. | `rts/docs/reports/worker_construction_<date>/` | none (the coordinator reads the report) |

WC-1 and WC-2 go on one feature branch, in that order, and merge together. After WC-1 alone the AI tests listed in 9.3 may fail; WC-1's report lists which ones fail and why. `main` never gets WC-1 without WC-2.

No step is added to `Simulation.Step` and no step moves. The new work runs inside step 2 (commands), step 6d (deaths, through the existing `RemoveUnit` hook) and step 7 (economy).

---

## 1. Data

### 1.1 `rts/data/tuning/economy.json`: one new key

Insert after `construction_start_health_fraction`:

```
  // Time a worker spends at the site, once it has walked there, before the structure appears; the structure then finishes by its own build_seconds without the worker. Unit: seconds. Range: 0.05 to 30. Source: developer decision 18 Nov 2026 ("Workers should have to go over and spend a couple seconds constructing"); placeholder.
  "worker_placement_seconds": 2.0,
```

`EconomyTuning` (`rts/src/Sim/Data/Tuning/EconomyTuning.cs`):
- add `worker_placement_seconds` to the known-keys list. Read it with `RequireFix64("worker_placement_seconds", "0.05", "30")` as the property `WorkerPlacementSeconds` (`Fix64`). Its doc comment uses the same Unit, Range and Source text.
- add `int WorkerPlacementTicks(MatchClock clock)` returning `Math.Max(1, clock.SecondsToTicks(WorkerPlacementSeconds))`, the same pattern as `GatherTicksPerTrip`. At 20 ticks per second, 2.0 s gives 40 ticks, 0.5 s gives 10 and 0.05 s gives 1.

The key is required. A missing key is a load error naming the file and the key; this is how the loader treats every other key.

No other data changes. The arrival distance reuses existing values (section 3.2), so there is no new key for it.

### 1.2 Content digest

The tuning text is part of `Simulation.ContentDigest`, so the new key changes every state hash from tick 0, even in scripts that never build. Section 8 lists every pinned value that moves.

---

## 2. State

### 2.1 New enum `rts/src/Sim/Economy/BuildOrderState.cs`

```
public enum BuildOrderState : byte { None = 0, ToSite = 1, Placing = 2 }
```

The values are stable because they appear in hashes.
- `None`: no build order.
- `ToSite`: walking to the site.
- `Placing`: at the site, counting down the placement time.

### 2.2 New fields on `Unit` (`rts/src/Sim/Units/Unit.cs`)

All of them have private setters. The constructor sets the values in the "None" column.

| Property | Type | Unit | Range | None value |
| --- | --- | --- | --- | --- |
| `BuildState` | `BuildOrderState` | - | None, ToSite, Placing | `None` |
| `BuildDefinitionId` | `string` | definition ID | a structure ID of the owner's faction while the state is not None | `""` |
| `BuildTileX` | `int` | tiles | 0 to map width - 1 while the state is not None | `-1` |
| `BuildTileY` | `int` | tiles | 0 to map height - 1 while the state is not None | `-1` |
| `BuildTicksRemaining` | `int` | ticks | 0 to `WorkerPlacementTicks` while Placing; 0 otherwise | `0` |
| `BuildResumeFieldIndex` | `int` | field index | -1, or 0 to field count - 1: the gather field to go back to afterwards | `-1` |

The order does not store what was paid. The refund is always the definition's `cost_common` and `cost_rare`, and definitions are frozen at `Start`.

Internal mutators, used only by the build command and `EconomySystem`:
- `SetBuildOrder(string definitionId, int tileX, int tileY, int resumeFieldIndex)`: sets state ToSite and ticks 0.
- `StartPlacing(int ticks)`: sets state Placing and the countdown.
- `TickPlacing()`: counts down by 1, never below 0.
- `ClearBuildOrder()`: resets every field to its None value.

### 2.3 Hash order

`Unit.WriteHash` writes six values right after `ConstructionTicksRemaining` and before `ShieldPool`, in this order:
1. `(byte)BuildState`
2. `BuildDefinitionId` (string)
3. `BuildTileX`
4. `BuildTileY`
5. `BuildTicksRemaining`
6. `BuildResumeFieldIndex`

Nothing else is new state. Player stock already hashes the payment and refunds, and `SupplyUsed` already hashes the supply of pending orders. Update the class header comment and the `ComputeStateHash` summary if they list unit fields.

---

## 3. Rules

### 3.1 `BuildStructureCommand.Apply` (step 2)

The wire fields stay the same: `Worker`, `DefinitionId`, `TileX`, `TileY`. Section 7 covers the codec.

Rejections are checked in this order. The new ones are in **bold**. A rejected command changes nothing: no state, no cost and no cancellation of anything the worker was doing.
1. `unknown_unit`
2. `not_owner`
3. `cannot_build`
4. `unknown_definition`
5. `not_a_structure`
6. `wrong_faction`
7. `out_of_bounds`
8. **`site_unexplored`**: the site tile is not explored by the team of the worker's owner. Use `sim.Vision.IsTileExplored(sim.Players.TeamOf(worker.Owner), TileX, TileY)`. Explored is the explored-ever grid of `VisionSystem`. It is never cleared and is hashed. The tile does not have to be visible now. The script issuer (`PlayerId.None`) gets no exemption, because the check uses the worker owner's team. This check comes before `tile_blocked` so that a rejection never reveals what stands on a tile the team has never seen.
9. `tile_blocked`
10. `tile_occupied`: unchanged. Any non-air unit whose tile is the site tile counts, including the worker itself.
11. **`site_pending`**: a unit with the same owner as the worker (the worker included) has `BuildState != None` with this site tile. So re-ordering a worker to the site it is already heading for is rejected, and its order goes on unchanged.
12. `no_base`
13. `static_defence_cap`: now also counts pending orders (section 3.5).
14. `insufficient_resources`
15. `supply_cap`: `RecomputeSupply` already runs first, and it now counts pending orders (section 3.5).

When the command is accepted, apply these steps in this order:
1. If the worker already has a build order, call `EconomySystem.CancelBuild(sim, worker, "new_order", resume: false)` (section 3.4). That refunds the old order and emits its cancellation event. The old order still counted during validation above. That is deliberate: validation reads the state as it is, and a rejected replacement leaves the old order in place.
2. Resume field. If `worker.GatherState != Idle`, it is `worker.GatherFieldIndex`. Otherwise, if step 1 cancelled an order, it is that order's `BuildResumeFieldIndex` (read before the cancel). Otherwise it is -1.
3. `owner.Spend(cost_common, cost_rare)`.
4. `worker.ClearCombatOrders()`, `worker.ClearGather()`. A carried load stays carried, as with a move order.
5. `worker.SetBuildOrder(DefinitionId, TileX, TileY, resume)`.
6. `worker.SetMoveTarget(centre of the site tile)`.
7. `EconomySystem.RecomputeSupply(sim)`.
8. Emit `BuildOrderAcceptedEvent` (section 5).

Nothing is spawned here any more.

### 3.2 The build order state machine (`EconomySystem`, step 7)

Add `StepBuild(sim, unit)` to the per-unit loop of `EconomySystem.Step`. It is called when `unit.BuildState != None`, after `StepGather` and before `StepProduction`. That position means a gather loop resumed by `StepBuild` first advances on the next tick. Units are visited in ascending ID order, from the snapshot taken at the start of the loop, as today.

Definitions used below:
- **site centre**: `sim.Terrain.CentreOf(sim.Terrain.IndexOf(BuildTileX, BuildTileY))`.
- **reach**: `InteractRange(sim) + definition.Radius`. `InteractRange` is the existing `max(interact_range_tiles, arrival_radius_tiles)`. This is the same reach a worker uses to deposit at a drop-off, measured to the structure's edge. With the shipped data it is 1.5 + 0.5 = 2.0 tiles.
- **within reach**: `(siteCentre - unit.Position).LengthSquared() <= reach * reach`.
- **site check**: returns `site_blocked` when `sim.Terrain[siteTile].IsBlocked`. Otherwise it returns `site_occupied` when any unit with `EffectiveLayer != Air` stands on the site tile, the builder included. Otherwise it returns ok. Units are scanned in ascending ID order; the result does not depend on the order.
- **P**: `sim.Tuning.Economy.WorkerPlacementTicks(sim.Clock)`.

```
ToSite:
  if within reach:
      unit.ClearMoveTarget()
      r = site check
      if r != ok: CancelBuild(sim, unit, r, resume: true); return
      unit.StartPlacing(P)
      emit BuildOrderStartedEvent(placement ticks = P)
  else if !unit.HasMoveTarget:
      // movement gave up (MoveUnreachableEvent) or something cleared the order
      CancelBuild(sim, unit, "unreachable", resume: true)
  // else: keep walking

Placing:
  unit.TickPlacing()
  if unit.BuildTicksRemaining > 0: return
  r = site check
  if r != ok: CancelBuild(sim, unit, r, resume: true); return
  structure = sim.SpawnUnit(definition, unit.Owner, siteCentre)      // footprint blocker added by the spawn hook
  structure.SetHealth(definition.MaxHealth * construction_start_health_fraction)
  structure.SetConstructionTicks(EconomyRules.BuildTicks(sim, definition))
  emit StructurePlacedEvent
  resume = unit.BuildResumeFieldIndex
  unit.ClearBuildOrder()
  if resume >= 0: StartGatherLoop(sim, unit, resume)
```

How the timing works:
- An order accepted on tick T sets the move target in step 2. Movement in step 5 of tick T already moves the worker. Step 7 of tick T checks arrival, so a worker that starts within reach arrives on tick T.
- If arrival happens on tick A, the countdown is set to P on tick A and decremented on A+1, A+2 and so on. The structure appears on tick **A + P**.
- The new structure is spawned inside the economy loop, so the snapshot does not hold it. Its construction countdown first runs on A + P + 1, and it completes on tick **A + P + build_ticks**. Today a structure placed by a command on tick T completes on T + build_ticks - 1, because the snapshot included it. Note this one-tick difference in the code comment.
- The worker stops at the edge of reach, about 2 tiles from the site centre. That is more than the 0.71-tile half-diagonal of the site tile, so the builder never stands on its own site. The site check still counts the builder for safety.
- The flow field to a site that became blocked leads to a neighbour tile, so the worker usually comes within reach and the site check cancels the order as `site_blocked`. A worker approaching diagonally can stop just outside reach (about 2.006 tiles from the centre): when the walk ends short, the site check runs first and its reason (`site_blocked` or `site_occupied`) is used; only a free site gives `unreachable`.
- The site is checked twice: on arrival and on the spawn tick. The fog, the base, the cap and the supply are not checked again. Pending orders already counted toward the cap and the supply, and losing the drop-off later does not void an order that was accepted. See Q6.

`StartGatherLoop(sim, unit, fieldIndex)` is a new internal helper in `EconomySystem`. It holds the state-setting part of `GatherCommand.Apply`, and `GatherCommand` now calls it so that its behaviour stays exactly as it is:
- If the field index is out of range or the field is depleted, the unit stays Idle.
- Otherwise, if `CarriedAmount > 0`: with a drop-off, it goes `ToDropOff` toward `StandablePointNear(nearest drop-off)`; with no drop-off, it stays Idle and keeps the load.
- Otherwise it goes `ToField` toward `StandablePointNear(field centre)`.

The command's rejection checks stay in the command.

### 3.3 Separation

In `UnitSeparation.IsParticipant`, a unit with `BuildState != None` does not take part, the same as a gathering worker. Two reasons:
- crowd arrival would end the walk early, at up to `crowd_arrival_radius_tiles` (3), and that would cancel the order as `unreachable`;
- pushes could move a placing worker onto its own site.

This is a one-line change.

### 3.4 Cancellation and refund

Add `internal static void EconomySystem.CancelBuild(Simulation sim, Unit worker, string reason, bool resume)`:
1. Look up the definition and the owner. If `sim.Players.TryGet(worker.Owner, out owner)` succeeds, call `owner.AddResource(Common, cost_common)` and `owner.AddResource(Rare, cost_rare)`.
2. Emit `BuildOrderCancelledEvent`, with the refund amounts.
3. Read the resume field, then call `worker.ClearBuildOrder()`. If `resume` is true and the field is 0 or more, call `StartGatherLoop`.
4. Do **not** call `RecomputeSupply` here. The callers that need it already run it: the command path before its own supply check, and the economy step at its end. The step-6d death path is covered by the step-7 recompute on the same tick.

Who cancels, and with which reason:

| Trigger | Where | Reason | Resume gather |
| --- | --- | --- | --- |
| An accepted `MoveUnitCommand`, `AttackMoveCommand`, `AttackUnitCommand` (both branches), `StopCommand` or `GatherCommand` for the worker | step 2. Call it right after the command's last rejection check and before it changes any other state. In `GatherCommand` that is after `no_drop_off`. | `new_order` | no (the new order rules) |
| An accepted `BuildStructureCommand` for a worker that already has an order | step 2, section 3.1 | `new_order` | no (replaced) |
| The worker leaves `UnitRegistry` for any reason: combat death, `remove_unit`, anything else that calls `Simulation.RemoveUnit` | `EconomySystem.OnUnitRemoved` (step 6d for deaths) | `worker_lost` | no (the unit is gone; refund and event only) |
| The site check fails on arrival or on the spawn tick | step 7 | `site_blocked` or `site_occupied` | yes |
| The walk ends short | step 7 | `unreachable` | yes |

Rejected commands cancel nothing. Commands that do not order the worker do not cancel either: `apply_status`, `root_hq`, `uproot_hq` and `cast_wave` name other units. A Grounded or slowed worker keeps its order.

Once the structure has appeared there is nothing left to refund. Destroying a structure that is still under construction refunds nothing, which is today's rule and is unchanged.

### 3.5 Pending orders in supply and the static defence cap

- `EconomySystem.RecomputeSupply`: inside the existing unit loop, for each unit owned by a known player with `BuildState != None`, add `EconomyRules.SupplyOf(sim, definition of BuildDefinitionId)` to that player's `used`. Supply is not double counted when the structure appears: on that tick the order is cleared and the structure counts instead, for the same amount.
- `BuildStructureCommand.CheckStaticDefenceCap`: after counting standing static defences in the base, also count every unit with `Owner == owner`, `BuildState != None`, a definition with `IsStaticDefence`, and a site centre within `base_radius_tiles` of the same drop-off (squared comparison). The pending order of the worker being validated counts too. The rejection rule `count >= static_defences_per_base_max` stays the same.
- Both scans are in ascending ID order. Two commands on one tick see each other, because each accepted command sets its pending order at once and `RecomputeSupply` runs before every supply check.

### 3.6 What stays unchanged

- HQ rooting, uprooting and relocation.
- `spawn_unit`, `StandardMatchStart` and anything else that spawns a structure directly: complete, at once, with no worker.
- Production, construction (`StepConstruction`), footprints, drop-off rules and the cost of training.
- When a player leaves the match or is defeated, the orders of their workers go on under the hand-over policy, as their other orders do. Out of scope (section 10).

---

## 4. Determinism

- Fix64 and integers only: reach and distances are squared Fix64 comparisons, P comes from `MatchClock.SecondsToTicks`, and refunds are integers.
- No new collection that is iterated. Every scan is `sim.Units.All` (sorted by ID) or the economy snapshot (ascending ID). Ties cannot happen: each order belongs to exactly one unit, and the cap and `site_pending` checks are counts or existence tests.
- No RNG draw. No new step. Events are emitted in a fixed order inside one tick: step-2 commands in (issuer, sequence) order, step-6d deaths in their existing sorted order, and step-7 units in ascending ID.
- Within one `Apply`, the cancellation event of a replaced order comes before `BuildOrderAcceptedEvent`.
- Every field that affects later ticks is hashed (section 2.3).

---

## 5. Events (`rts/src/Sim/Events/EconomyEvents.cs`)

Unity draws these events: the ghost of the site, the worker walking and the placement progress. The AI and the harness read them. None of them changes state.

| Class | `Kind` | Fields (besides `Tick`) | When |
| --- | --- | --- | --- |
| `BuildOrderAcceptedEvent` | `build_order_accepted` | `Worker` (EntityId), `Owner` (PlayerId), `DefinitionId` (string), `TileX`, `TileY` (int, tiles), `CostCommon`, `CostRare` (int, resource units, the amount paid) | step 2, on acceptance |
| `BuildOrderStartedEvent` | `build_order_started` | `Worker`, `DefinitionId`, `TileX`, `TileY`, `PlacementTicks` (int, ticks, 1 or more) | step 7, on arrival |
| `BuildOrderCancelledEvent` | `build_order_cancelled` | `Worker`, `Owner`, `DefinitionId`, `TileX`, `TileY`, `Reason` (string: `new_order`, `worker_lost`, `site_blocked`, `site_occupied`, `unreachable`), `RefundCommon`, `RefundRare` (int, resource units) | steps 2, 6d or 7 |
| `StructurePlacedEvent` | `structure_placed` | `Worker`, `Structure` (EntityId), `DefinitionId`, `TileX`, `TileY` | step 7, the tick the structure appears (`UnitSpawnedEvent` for the structure comes first on the same tick) |

Each event gets a `Describe()` line in the style of the existing events. `StructureCompletedEvent` is unchanged.

---

## 6. Documentation edits inside WC-1

- `Simulation.cs` header. Step 2: add "an accepted move, attack, stop, gather or build order cancels a worker's pending build order and refunds it". Step 6d: deaths cancel pending orders through the `RemoveUnit` hook. Step 7 reads: "economy (EconomySystem: worker gather loops, build orders (walk to the site, placement, structure spawn), production queues, construction, supply; units in ascending ID order)". Make the same edit to the step list in `rts/src/Sim/README.md`.
- The `EconomySystem.cs` header: replace the "Construction" paragraph with the state machine of section 3.2, and add "pending build orders" to the supply paragraph.
- `BuildStructureCommand`: rewrite the class summary (remove "Phase 1 placeholder") and the rejection list in the `Apply` summary. Update the `EconomyCommands.cs` header line.
- `rts/src/Sim/Economy/README.md` and `rts/data/README.md`: wherever they describe `build_structure`, describe the new behaviour.
- `rts/docs/specs/README.md`: add a "Rule changes (worker construction, 18 Nov 2026)" table row for this spec.

---

## 7. Wire format, codec and replays

- **No payload change.** `BuildStructureCodec` still writes worker (EntityId), definition (string), tile x (int32), tile y (int32). `CoreCommandCodecs`, `MessageCodec.ProtocolVersion` (3) and `ReplayFormat.CurrentVersion` (2) stay as they are. WC-1 adds a byte pin for the payload (test WC-19) to prove this.
- **Replays recorded before this change will not re-simulate.** Section 1.2 means their content digest differs from the new data set's, so `ReplayCheck` refuses them (`REPLAY REFUSED`, exit 2) before any step runs. They are not reported as a desync. ADR-009 allows this: "a replay recorded before a change must still verify after it unless the change was meant to alter the rules, in which case the data digest or the format version moves with it". Here the digest moves. Mention it in the changelog. See Q8.
- Lockstep peers on different builds diverge at the tick-0 hash, because the state hash includes the content digest. No protocol bump is needed.

---

## 8. Pinned hashes that move

Every value below changes. At the very least the tuning text, and so the content digest, changes. Re-pin each one in the package listed, with a comment line naming this spec.

| # | Value today | Where | What it pins | Package |
| --- | --- | --- | --- | --- |
| 1 | `0xc5368cde05d19445` | `rts/tests/Sim.Tests/DeterminismTests.cs:92` `GoldenFinalHash` | seed 777 determinism script (digest only, no builds) | WC-1 |
| 2 | `STATE_HASH=9ece0ce1b0338423` | `rts/tests/Sim.Tests/Harness/HarnessUnitTests.cs:165` | harness default `scripts/smoke.json` (digest only) | WC-1 |
| 3 | `STATE_HASH=38db6dd44d0ec48b` | `rts/tests/Sim.Tests/Harness/HarnessUnitTests.cs:167` | `scripts/economy_smoke.json` (digest and behaviour) | WC-1 |
| 4 | `0x9422f0628a36cfca` and `OUTCOME winner=1 tick=5631` | `rts/tests/Sim.Tests/Ai/SkirmishAiTests.cs:828` `V2ReferenceHash` and the assertion at about line 839 (A-11) | `scripts/ai_vs_ai.json`, aggressive vs defensive | WC-2 |
| 5 | `18210` and `584d762eb253f6ce` | `SkirmishAiTests.cs:843` `BlindTraceEndTick` and `:849` `BlindTraceHash` (C2-11) | traced `abl_s2_blind` mirror, prototype_1v1, water_vs_water, seed 20261117, game 0 | WC-2 |

How to re-pin:
- Run the same command the test runs, on the finished package, and paste the new value.
- Extend each constant's comment with one line: "Re-pinned for worker construction (specs/worker_construction.md): new tuning key `worker_placement_seconds` and workers walk to build sites."
- A-11's comment says the value "must never change unless the shipped data or the simulation changes on purpose". This is such a change. A-11 then goes back to guarding the profile switches. If the outcome line changes (winner or tick), re-pin it too. The test must still see a decided game; if it does not, stop and report.

Documents that quote these values (`STATUS.md`, the Unity parity hash in `STATUS.md`) are the coordinator's job. The PC session must repeat the Unity check against the new `economy_smoke` hash. `Replay.Tests/EndToEndTests.cs` computes its hash at run time, so nothing needs re-pinning there.

---

## 9. Changes outside the core rule

### 9.1 `rts/data/scripts/economy_smoke.json` (WC-1)

The three `build_structure` commands stay, at the same ticks and on the same tiles:
- tick 10: Tender 5, Tidepool at (55, 54);
- tick 330: Fledgling 2, Roost at (9, 9);
- tick 800: Fledgling 3, Perch Tower at (11, 8).

Each worker is near its site, and every site is inside its own team's starting sight. The builder:
1. Runs `dotnet run --project rts/tools/BalanceHarness -- --data rts/data --script rts/data/scripts/economy_smoke.json`. It checks that there are no `warning:` lines (no rejections). It checks, with a temporary event dump or a test, that each order produces `build_order_started` and `structure_placed` and no `build_order_cancelled`.
2. Checks that the Roost completes before its `train_unit` at tick 1000 and the Tidepool before tick 1100. If one does not, the builder moves that `train_unit` later by the smallest whole second that works and says so.
3. Rewrites the comment lines that state entity IDs and completion ticks ("7 the Tidepool (placed at tick 10)", "complete at tick 829", "complete at tick 509") with the observed placement and completion ticks. If the order in which units appear changes, the IDs change as well, and the later `train_unit` producer IDs must follow.

`smoke.json`, `combat_smoke.json`, `flood_smoke.json`, the `ai_vs_ai*.json` scripts and the `ai_eval/response_*.json` scenarios contain no `build_structure`. Their scripted structures use `spawn_unit` and stay as they are. Their AI players change through WC-2.

### 9.2 Existing Sim tests (WC-1)

`rts/tests/Sim.Tests/Economy/EconomyTests.cs` has 21 uses of `BuildStructureCommand`. Several tests give one worker many orders on one tick (for example `StaticDefenceCap_SixPerBase`, `Build_Rejections_BlockedOccupiedOutOfBoundsAndMore`), or expect the structure on the command tick (`Footprint_BlocksPathing_WhileAlive_ClearsOnDeath`, `SupplyCap_BlocksTraining_UntilBuildingCompletes`). Rewrite them to the new rule and keep what each one meant to prove:
- one worker per pending order, or step until each order is placed;
- sites inside explored tiles: spawn the worker or a spotter near the site and step once, so the explored grid is filled. Add no test-only API to `Rts.Sim`;
- assert on the tick the structure is placed, not the command tick.

Do the same in `Data/MapLayoutTests.cs` (1 use). The 8 uses in `tests/Sim.Tests/Ai/*` belong to WC-2.

### 9.3 Skirmish AI (`rts/src/Ai`, WC-2)

Today the AI assumes that a build order turns into a structure at once. Every rule that counts structures would re-issue the order while the worker walks, and rule G would send the placing worker back to gather, which cancels its order. Changes:

1. **View.** `AiUnitView` gets `BuildState`, `BuildDefinitionId`, `BuildTileX` and `BuildTileY`, copied from the unit for **own units only**. Enemy views always carry `None`, `""`, -1 and -1: an enemy's build order is hidden information, and the harness vision-violation check must stay empty. Add the helper `bool HasBuildOrder => BuildState != None`. `AiWorldView` gets `bool IsExplored(int x, int y)`, which wraps `sim.Vision.IsTileExplored` for the AI's team, the same way `IsVisible` does today.
2. **Pending orders count as structures under construction.** Wherever a rule counts or locates own structures, an own worker with a build order counts as one structure of `BuildDefinitionId`, under construction, at the centre of its site tile. Add one helper, for example a `Turn` method that lists the planned structures as (definition, position, under construction). Use it at each of these places:
   - the `Turn` constructor: `_plannedDropOffs`, `DepotsPlaced`, `_depotUnderConstruction`, `_liveDepots`;
   - `Structures()`: `supplyPending`, and `CountOwn` for the build order steps;
   - `Tech()`: the "producer of a mix unit under construction" test (about line 1526);
   - `StaticDefencesNear`, `StaticDefenceUnderConstruction`, and the value share test (about line 1830);
   - `ExtraProducer()`: the "none under construction" test (about line 2417).

   A pending order never counts as a drop-off that workers can use (`_hasDropOff` stays as it is) and never counts as a producer.
3. **Sites.** `IsGoodSite` and the depot site search (`TryFindDepotSite`) skip any tile that holds an own pending order, the same way `_reservedSites` works. Then the AI never gets `site_pending`. Neither of them requires the tile to be explored (item 5 handles that). Otherwise an unexplored field would land permanently in `UnbuildableFields`.
4. **Builder.** `PickBuilder` gets the site. It picks the own worker with no build order whose position is nearest the site centre (squared distance), ties to the lower ID. Workers that are gathering, idle or carrying all count. If there is none, it returns null. With travel time, the nearest worker gives the shortest delay.
5. **Unexplored site.** In `TryBuild`, after choosing the site and the builder: if `!_view.IsExplored(x, y)`, emit `AiIntent.Move(builder.Id, site centre)` instead of the build. Do not `Pay` or reserve the cost, but do set `LastBuildAttemptTick[definition.Id]`. The walk explores the site, and the next attempt after `build_retry_seconds` builds there with the nearest worker, which is usually the same one. This is the only new branch in `TryBuild`.
6. **No other orders to builders.** Rule G (idle workers and rebalancing), the "follow a relocating HQ" move, and any other rule that orders a worker must skip workers where `HasBuildOrder` is true. A placing worker has no gather loop and no move target, so without this it looks idle.
7. **Cancel handling.** `SkirmishAiController` reads events in the same loop as today's `tile_blocked` rejection. On a `BuildOrderCancelledEvent` for one of its own workers: with reason `site_blocked`, add the site tile to `Memory.RejectedSites`. With any reason other than `new_order`, remove `Memory.LastBuildAttemptTick[DefinitionId]` so the next think may try again. The refund is already in the stock the view reads. Keep the `tile_blocked` rejection handler.
8. **Timings that shift.** There is no profile retuning in this spec. These are the expected effects for WC-3 to measure:
   - Every structure appears later by walk time plus placement time. Walk ticks are about ceil((distance - 2.0) / step), where step is speed / 20. The Fledgling's step is 2.5 / 20 = 0.125 tiles per tick; the Tender's is 2 / 20 = 0.1. Placement is 40 ticks.
   - Example: a depot 25 tiles from its builder appears ceil(23 / 0.125) + 40 = 184 + 40 = 224 ticks (11.2 s) later for Skai, and ceil(23 / 0.1) + 40 = 270 ticks (13.5 s) later for Water.
   - The first rare depot of `use_early_expansion` (`early_expansion_seconds` 0 in `aggressive_s2`, `aggressive_s2b` and `abl_s2_blind`) also waits for the scouting trip when its site is unexplored. That is likely tens of seconds on the shipped maps.
   - A static defence 8 tiles away comes ceil(6 / 0.125) + 40 = 88 ticks (4.4 s) later for Skai.
   - The builder stops gathering for the trip, so income dips slightly.

### 9.4 Harness (WC-2)

`rts/tools/BalanceHarness/GameTrace.cs`:
- an accepted `BuildStructureCommand` still records the spend. Rename what it records to "ordered".
- add handlers for `BuildOrderCancelledEvent` (subtract the refund from the owner's spend, count the reason) and `StructurePlacedEvent` (`TotalPlaced`, `Sites` and `FirstStructureTick` move here from the acceptance branch).
- add the cancellation counts to the trace header and per-player summary, next to the rejection counts.

Update `HarnessTraceTests` if they assert those lines. In `rts/tools/BalanceHarness/README.md`, the `build_structure` sentence gets: "the worker walks to the site and places the structure after `worker_placement_seconds`; the site must be explored".

`Program.cs` prints `CommandRejectedEvent` as warnings. Cancellations are not rejections and are not printed. The exit test (`ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided`) keeps asserting zero rejections. WC-2 must keep it green, which is the check on items 3, 5 and 6 above.

### 9.5 Unity (PC session, not in these packages)

Unity does not issue `build_structure` today. When the build UI is made:
- it must name a selected worker, and only offer explored tiles;
- it draws the site ghost from `build_order_accepted`, progress from `build_order_started` (`PlacementTicks`), clears the ghost on `build_order_cancelled` (with the reason as a message) or `structure_placed`.

---

## 10. Out of scope

- Several workers speeding up one structure, a worker staying through construction, and repairing.
- Multi-tile footprints. Footprint is 1 today, and reach is measured from the centre plus the radius.
- A queue of build orders per worker (shift-queue).
- Refunds for structures destroyed or cancelled while under construction.
- Vision-gating any other command (the anti-cheat plan in `fog_of_war.md` section 5).
- What a leaving or defeated player's orders do beyond the current hand-over policy.
- Retuning AI profiles, unit data or the ladder bars. WC-3 only measures.
- The Unity build UI and drawing the events (9.5).

---

## 11. Acceptance tests

### 11.1 WC-1, Sim (`rts/tests/Sim.Tests/Economy/WorkerConstructionTests.cs`)

Setup unless stated otherwise:
- `TestWorld.StartedEconomy()` with the shipped tuning, so `worker_placement_seconds` is 2.0 (P = 40 ticks), `interact_range_tiles` is 1.5 and `arrival_radius_tiles` is 0.25. The test barracks has radius 0.5, so reach is 2.0. 20 ticks per second.
- The test worker has speed 4 tiles/s (0.2 tiles per tick), radius 0.3, sight 8, cost 50.
- The test barracks costs 100 common, builds in 5 s (100 ticks) and provides 8 supply. The test tower costs 10, builds in 2 s (40 ticks), is a static defence, and its supply is 1 from `supply.json`.
- P0 starts with 200 common. Helpers come from `EconomyTestKit`.
- "t0" is the tick on which the build command is applied (`Do(sim, build)` steps exactly that tick).

| # | Test | Worked numbers and assertions |
| --- | --- | --- |
| WC-1 | **Walk time from distance and speed** | Worker spawned at (14, 14) (position (14, 14), tile (14, 14)). Barracks at tile (20, 14), centre (20.5, 14.5). The worker moves +0.2 x per tick along row 14 (flow direction E). It arrives once (20.5 - x)² + 0.5² <= 2.0², that is x >= 18.5635. After tick t0 + k, x = 14 + 0.2 (k + 1). At k = 21, x = 18.4 (not yet). At k = 22, x = 18.6. So `BuildOrderStartedEvent` (PlacementTicks 40) comes on **t0 + 22**, and from that tick the worker has `HasMoveTarget` false and is in state Placing. `BuildOrderAcceptedEvent` comes on t0 with cost 100/0, and common is 100 from t0 on. |
| WC-2 | **Placement time in ticks** | Same run. On t0 + 61 there is no barracks, and the worker has state Placing with `BuildTicksRemaining` 1. On **t0 + 62** come `UnitSpawnedEvent` then `StructurePlacedEvent`. The barracks is at (20.5, 14.5) with health 200 × 0.1 = 20 and `ConstructionTicksRemaining` 100. Tile (20, 14) is blocked. The worker has state None and every build field at its None value. `StructureCompletedEvent` comes on **t0 + 162**. Common is 100 throughout (no refund). Supply cap is 10 until completion and 18 after. |
| WC-3 | Placement ticks follow the tuning | A tuning copy with `worker_placement_seconds` 0.5 gives P = 10 (placed on t0 + 32 in the WC-1 layout), and 0.05 gives P = 1. Loading 0.01 or 31 throws a `DataValidationException` naming `tuning/economy.json` and `worker_placement_seconds`. |
| WC-4 | Arrival on the command tick | Worker at (19, 14). (20.5 - 19.2)² + 0.25 = 1.94 <= 4, so it starts on **t0** and the barracks appears on t0 + 40. |
| WC-5 | **Refund when a new order arrives** | WC-1 layout. On t0 + 5, `MoveUnitCommand` to (14, 30). That tick has `BuildOrderCancelledEvent` (reason `new_order`, refund 100/0). Common goes 100 → 200. State is None. The worker's move target is (14, 30). After 100 more ticks there are 0 barracks and tile (20, 14) is free. Repeat the same with `StopCommand`, `AttackMoveCommand`, `GatherCommand` (field 2) and `AttackUnitCommand` (target a P1 unit): each cancels with `new_order`. A `MoveUnitCommand` to (-1, 5) is rejected `out_of_bounds` and does **not** cancel: the state stays ToSite and common stays 100. |
| WC-6 | **Worker killed mid-placement** | WC-1 layout. On t0 + 30 (Placing, `BuildTicksRemaining` 32), a script `RemoveUnitCommand` for the worker. That tick has `UnitRemovedEvent`, then `BuildOrderCancelledEvent` (reason `worker_lost`, refund 100/0, the worker's ID). Common is 200. After 100 more ticks there are 0 barracks and tile (20, 14) is not blocked. Combat variant: a P1 `ShooterId` at (21, 17) shoots the worker as it comes into range, and kills it in 4 hits (40 health, 10 damage each, 16-tick cooldown). The cancel reason is `worker_lost`, the refund comes on the death tick, and no barracks ever appears. |
| WC-7 | **Site blocked on arrival** | WC-1 layout. On t0 + 1, a script `spawn_unit` of `TowerId` for P1 at (20, 14). It is complete at once and blocks the tile. On **t0 + 22**: `BuildOrderCancelledEvent` (reason `site_blocked`, refund 100/0), common 200, no `BuildOrderStartedEvent` at any tick. The worker had no gather job, so it ends Idle with no move target. |
| WC-8 | Site occupied, on arrival and during placement | (a) On t0 + 1, spawn a P1 `TankId` at (20, 14). It does not move. On t0 + 22 the cancel reason is `site_occupied`. (b) Nothing in the way until t0 + 22. On t0 + 50, spawn the tank at (20, 14). On t0 + 62 the cancel reason is `site_occupied`, the refund is 100, and no barracks appears. |
| WC-9 | **Unexplored site rejected** | Worker at (14, 14) (explores a radius of 8 around tile (14, 14)), gathering field 2. A barracks at tile (40, 14) (26 tiles away, never seen by team 0) is rejected `site_unexplored`. Common stays 200, there is no build event, and the worker's `GatherState` and `GatherFieldIndex` are unchanged. |
| WC-10 | Fog is checked before blocked | A P1 tower spawned at (40, 40), where team 0 has never looked. A P0 barracks order at (40, 40) is rejected `site_unexplored`, not `tile_blocked`. |
| WC-11 | Explored, not visible, is enough | A P0 spotter (`RunnerId`) spawned at (40, 14) explores the area. Then it moves to (60, 60), and the test steps until tile (40, 14) is no longer visible to team 0 but still explored. A barracks order at (40, 14) is accepted. |
| WC-12 | **Two workers, one static defence cap** | HQ at (12, 12) (Rooted, base radius 12). Five P0 towers spawned complete at (16, 10) to (20, 10). Workers W1 at (14, 14) and W2 at (15, 15). Common 200. On one tick: P0 sequence s orders W1 to build a tower at (21, 10), and sequence s + 1 orders W2 to build a tower at (22, 10). W1 is accepted, and W2 is rejected `static_defence_cap` (5 standing + 1 pending = 6). Common is 190. `SupplyUsed` is 5 + 1 + 2 workers = 8. Next tick, `StopCommand` W1: cancel `new_order`, refund 10, common 200, `SupplyUsed` 7. Then W2's same order is accepted and common is 190. |
| WC-13 | **Two workers, one supply** | HQ at (12, 12). Nine P0 workers: `SupplyUsed` 9, cap 10 (`base_supply` 10, the test HQ provides 0). Two tower orders on one tick, by two of the workers, to explored free tiles within the base. The first is accepted and `SupplyUsed` becomes 10. The second is rejected `supply_cap`. When the first tower appears, `SupplyUsed` is still 10: the order's 1 is replaced by the structure's 1. |
| WC-14 | Same site twice | W1 and W2 both ordered to tile (20, 14) on one tick. The first is accepted. The second is rejected `site_pending`. W1 re-ordered to (20, 14) is rejected `site_pending`: its order goes on, common is not charged twice, and there is no cancel event. |
| WC-15 | Replacing an order | W1 has a pending barracks at (20, 14) (common 100). A tower order for W1 at (16, 16) is accepted. On the same tick: `BuildOrderCancelledEvent` (`new_order`, 100/0) **then** `BuildOrderAcceptedEvent` (10/0). Common is 100 + 100 - 10 = 190. The barracks site is free again. |
| WC-16 | Back to gathering | HQ at (12, 12) Rooted. The worker gathers field 0 (tile (10, 10)). On the tick its state is `Gathering`, order a barracks at (16, 12). `BuildResumeFieldIndex` is 0, and `GatherState` is Idle until the barracks is placed. On the next tick after `StructurePlacedEvent`, `GatherState` is ToField and `GatherFieldIndex` is 0. Carrying variant: order the barracks while the worker carries 5. The load stays carried. After placement the state is ToDropOff. Cancelled variant (WC-7 with a gathering worker): after `site_blocked` the worker resumes field 0. Depleted variant: field 0 is emptied by another worker before placement, and the worker ends Idle. |
| WC-17 | Unreachable site | A copy of `TerracedMap` built in the test with no ramp. The worker is on the low terrace at (10, 30). The site is tile (40, 30) on the high terrace, explored by a P0 `FlyerId` spotter that flies over it first. On t0, movement emits `MoveUnreachableEvent` for the worker, and the same tick has `BuildOrderCancelledEvent` with reason `unreachable` and refund 100/0. |
| WC-18 | Hash covers the new fields | Two identical sims, stepped to a tick where the worker is Placing. Their hashes are equal. Then change, one at a time through the internal setters on a clone run, `BuildTicksRemaining`, `BuildTileX`, `BuildTileY`, `BuildDefinitionId`, `BuildResumeFieldIndex` and `BuildState`. Each change gives a different `ComputeStateHash()`. |
| WC-19 | Wire format unchanged | `EncodePayload(new BuildStructureCommand(0, P0, 0, new EntityId(42), "ab", 3, 4))` as hex equals `"2a000000" + "0200" + "6162" + "03000000" + "04000000"`, the same layout as the existing move, spawn and status pins in `CommandCodecTests`. It round-trips through decode. |
| WC-20 | Builders do not separate | A placing worker with an idle P0 `RunnerId` spawned touching it. After 10 ticks the worker's position is unchanged (separation is on in the shipped tuning). A walking builder passing a stopped unit near its goal is not stopped early by crowd arrival: it still arrives on the tick computed as in WC-1. |
| WC-21 | Direct spawns unchanged | A script `spawn_unit` of `BarracksId` is complete on its spawn tick (`ConstructionTicksRemaining` 0) and emits no build-order events. The existing HQ, root, uproot and relocation tests pass without edits. |
| WC-22 | economy_smoke | Two harness runs of `economy_smoke.json` give identical output. There are no `warning:` lines. The re-pinned `STATE_HASH` (section 8 row 3) holds. Its three orders each produce `structure_placed` and no `build_order_cancelled` (assert through an in-process run of the script). |
| WC-23 | Re-pins | `GoldenHash_IsPinned` and the two `HarnessUnitTests` lines hold their new values (section 8 rows 1 to 3). |

`dotnet test rts/RtsGame.sln` must be green, apart from the WC-2 AI tests listed in WC-1's report.

### 11.2 WC-2, AI and harness (`rts/tests/Sim.Tests/Ai/AiConstructionTests.cs` plus edits)

| # | Test | Assertion |
| --- | --- | --- |
| WCA-1 | Builder choice | Three own workers: W2 gathering, 20 tiles from the site; W3 idle, 6 tiles away; W4 gathering, 6 tiles away with a higher ID. `PickBuilder` picks W3. When W3 has a build order, it picks W4. |
| WCA-2 | Pending counts as built | An own worker with a pending supply-building order and supply at the buffer: rule 3a emits no second supply build. A build order step with count 1 and a pending order of that structure: rule 3b moves on to the next step. |
| WCA-3 | Placing worker is not idle | An own worker in state Placing (no gather, no move): rule G emits no Gather or Move for it on that think. |
| WCA-4 | Unexplored site | A depot due at a site that is not explored: the think emits a `MoveUnit` for the builder to the site centre, no `BuildStructureCommand`, the common stock in the ledger is unchanged, and `LastBuildAttemptTick` is set. Once the tile is explored and `build_retry_seconds` has passed, it emits the build. |
| WCA-5 | Own pending sites skipped | With an own pending order at the first ring tile, `TryFindSite` returns the next good tile. Over a full `ai_vs_ai.json` run there is no `site_pending` rejection. |
| WCA-6 | Cancel handling | After a `site_blocked` cancel of an own order, `RejectedSites` holds the tile and `LastBuildAttemptTick` has no entry for that definition. After an `unreachable` cancel, only the entry is removed. A `new_order` cancel changes neither. |
| WCA-7 | Static defence counts pending | A pending static defence order in a base: `StaticDefenceUnderConstruction()` is true, and `StaticDefencesNear(base)` counts it. |
| WCA-8 | No hidden information | An enemy worker with a pending order: its `AiUnitView` (if visible) has `BuildState` None and `BuildTileX` -1. The harness `VisionViolations` stay empty in the exit test. |
| WCA-9 | Exit test | `ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided` is green: identical hashes, decided, **zero rejections**. |
| WCA-10 | Re-pins | A-11 and C2-11 hold their new values (section 8 rows 4 and 5). Existing AI tests that used `BuildStructureCommand` (`AiArmyMixTests`, `AiArmyTests`, `AiEconomyTests`, `SkirmishAiTests`) are updated to wait for `StructurePlacedEvent` where they expected the structure on the command tick. |
| WCA-11 | Trace | A traced game's per-player summary shows ordered, placed and cancelled counts. Spend minus refunds equals the stock spent on structures that were placed. |

### 11.3 WC-3, re-baseline (report only, not asserted)

Run each of these with `-c Release` on the merged WC-1 and WC-2. Save the outputs under `rts/docs/reports/worker_construction_<date>/` with a `README.md`.

1. `--ladder --profile-a abl_s2_blind --profile-b aggressive_s2b`, compared with C2's 61.1 percent.
2. `--response --profile aggressive_s2b`, compared with C2's 3 of 9.
3. If package D has merged, its gate ladder (`--profile-a aggressive_s2 --profile-b aggressive_s3`) and `--response --profile aggressive_s3`.
4. The 60-game `prototype_1v1` batch as in `reports/ai_v2_2026-11-16/batch_prototype_1v1.md`, compared on decided share and mean length.
5. One trace each for skai_vs_water and water_vs_water: first depot tick, first static defence tick, cancellation counts by reason.

No profile edits, no bar changes, no data edits. If numbers collapse (for example an AI never expands), report it with a trace; do not fix it in WC-3.

---

## 12. Questions for the developer

Each question has a recommended answer that the builders use until the developer replies.

- **Q1. Placement time.** Is 2 seconds right for "a couple seconds", the same for every structure? *Recommended: yes, one key `worker_placement_seconds` = 2.0 (placeholder). A per-structure value can come later as an optional unit key.*
- **Q2. When the cost is paid.** *Recommended: paid in full when the order is accepted. Fully refunded if the order is cancelled before the structure appears: a new order, the worker dies, the site is blocked, occupied or unreachable. No refund once the structure exists (today's rule).*
- **Q3. Fog.** Must the site be explored, or visible now? The simulation has both grids. *Recommended: explored (seen at least once). Visible-now would force a spotter at every expansion.*
- **Q4. After placing.** *Recommended: the worker goes back to the field it was gathering when ordered, carrying any load to a drop-off first. It does the same after a site failure. It stays idle after a new order or if the field ran dry.*
- **Q5. Worker during construction.** You said "spend a couple seconds". *Recommended: the worker is needed only for the placement time; the structure then finishes its full `build_seconds` alone. The alternative, the worker staying the whole build, makes expansions much slower and is out of scope.*
- **Q6. What is checked again on arrival.** *Recommended: only the tile (blocked by a structure, or a unit standing on it), on arrival and again when the structure would appear. Both cancel with a refund. The base, the static defence cap and supply are not checked again, because pending orders already counted.*
- **Q7. Units standing on the site when the timer ends.** Cancel, or wait until the tile is free? *Recommended: cancel (refund, the worker goes back to work). It is simple and predictable. Waiting could leave a worker stuck forever behind a parked enemy.*
- **Q8. Replays and network version.** The command bytes do not change; the data digest does. *Recommended: no replay format or protocol version bump. Old replays are refused with a data-digest message, which ADR-009 allows for a rule change.*
- **Q9. Re-ordering a worker.** *Recommended: a new build order replaces the pending one (refund, then pay for the new one). The same order to the same site is rejected as `site_pending` and the original continues. A build order sent from a script for a player's worker follows the same fog rule (campaign scripts can use `spawn_unit` for pre-placed buildings).*
- **Q10. AI after the change.** Expansions and static defences will come later (section 9.3 item 8). *Recommended: no retuning in this change. WC-3 measures the effect, and you decide on retuning or new bars after reading its report.*

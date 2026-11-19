<!-- rts/docs/specs/field_saturation.md -->
<!-- Build spec for field saturation and the AI economy rules that depend on it (developer decision, 18 Nov 2026: "Alright, yes, let's add saturation. Let's handle the rest of these rules as well."). Package SAT (simulation: gather slots per field with a first-come queue), SAT-M (re-baseline), AI-E (worker target, depots and rare cap derived from the saturation data), AI-P (buildings at expansions, static defence by threat), AI-H (faction-fair harass speed), AI-HQ (HQ mobility). Part of rts/docs/specs (index: README.md); built by Sonnet builders, SAT reviewed by sim-reviewer, every AI package by sim-reviewer on the rts/src/Ai diff. -->
# Spec: field saturation, and the AI economy that follows it

Written 18 Nov 2026 by the designer against `main` at `f296a9c`. Every number is a **placeholder**.

## 0. Purpose, sources and packages

**Source.** The developer, 18 Nov 2026, verbatim: "Alright, yes, let's add saturation. Let's handle the rest of these rules as well." "These rules" are the audit findings below. The Game Design Proposal says nothing about saturation; the decision above is the only source, so every new data comment says `Source: developer decision 18 Nov 2026 (field saturation); placeholder.`

**Audit findings this spec resolves.**

| Id | Today | Resolved by |
| --- | --- | --- |
| A | Any number of workers gather one field at full rate (`EconomySystem.cs:10-11`, "Phase 1 placeholder: no slot limit"). Gather-loop workers are left out of unit separation (`UnitSeparation.IsParticipant`). Nobody decided either. | SAT (slots); Q-3 (separation) |
| B1 | AI worker caps are hand-written: `workers_per_field` 3, `rare_workers_per_field` 2, `max_workers` 24. | AI-E rule E1 |
| B2 | `max_live_depots` 2, one depot at a time, never while a threat is seen; `field_max_distance_tiles` 10. | AI-E rule E2 |
| B3 | The AI builds only in a ring around the HQ; the Sim allows any explored tile. | AI-P rule P1 |
| B4 | `static_defence_max_per_base` 2 (the Sim allows 6), built only after a raid, capped at 15 percent of army value. | AI-P rule P2 |
| B5 | The HQ relocates only when its home fields are nearly empty (`relocate_when_home_remaining_fraction` 0.1). | AI-HQ |
| C1 | `harass_min_speed_tiles_per_second` 3.2: Windrunner (3.5) and every flyer qualify for Skai, only the Reef Runner (4.1) for water. | AI-H |
| C2 | `rare_bank_soft_cap` 300 closes rare fields to the AI. | AI-E rule E1.4 |

**Packages and order (one commit per package).**

| # | Package | Touches | Review | Gate |
| --- | --- | --- | --- | --- |
| 1 | **SAT** Sim | `rts/src/Sim/Economy/*`, `Units/Unit.cs`, `Data/Tuning/EconomyTuning.cs`, `Simulation.cs` header, `rts/data/tuning/economy.json`, `rts/tests/Sim.Tests`, re-pins (section 3), one trace column in `rts/tools/BalanceHarness/GameTrace.cs` | **sim-reviewer, then Fable final review** | section 2.8 |
| 2 | **SAT-M** re-baseline | `rts/docs/reports/saturation_sat/README.md` only | coordinator reads | report only (section 9) |
| 3 | **AI-E** economy | `rts/src/Ai/*`, every `rts/data/ai/*.json`, new `aggressive_s5.json` and `abl_s5_no_e.json`, AI tests | sim-reviewer on `rts/src/Ai` | section 9 |
| 4 | **AI-P** placement, defence | same files, `abl_s5_no_p.json` | sim-reviewer | section 9 |
| 5 | **AI-H** harass speed | same files, `abl_s5_no_h.json` | sim-reviewer | section 9 |
| 6 | **AI-HQ** HQ mobility | same files, `abl_s5_no_hq.json` | sim-reviewer | section 9 |

SAT goes to `main` on its own: it is a game rule and the AI plays under it without any AI change (old conventions sit below saturation, section 2.7). The two open branches are handled in section 10.

**Ground rules (every package).**
- Unit and combat numbers stay frozen (P-005). No change to `rts/data/units`, `tuning/combat.json`, `tuning/supply.json` or maps.
- Data-driven: every number is a key in `rts/data/tuning/economy.json` (SAT) or an AI profile (AI packages), commented in the house format.
- Any player count. Slots are per field and shared by every player (Q-4).
- Determinism: integers and `Fix64` only; units in ascending `EntityId`, fields in ascending index; every ordering below names its tie-break. AI memory is not hashed (unchanged).
- AI keys follow the house rule of `skirmish_ai_v4.md` section 0: `AiProfile.Parse` reads every new key with the `Require*` helpers and adds it to `RejectUnknownKeys`; **every profile file under `rts/data/ai` and every hand-written test profile gets every new key with its "old" value**, which reproduces today's rule exactly. So every existing profile keeps today's AI rules; its hashes move only in SAT.
- After AI-E, AI-P, AI-H and AI-HQ, the AI pins re-pinned in SAT (section 3 rows 5 to 7) must hold **without re-pinning**. If one moves, stop and report.

---

## Package SAT: gather slots per field

### 1. The model and why

**Rule in one sentence.** Each field has a number of **gather slots** (one value for common fields, one for rare); a worker that reaches a field with every slot taken **waits beside it** and gets the next free slot in the order workers arrived.

A slot is held for the whole `gather_seconds_per_trip` of one load, so one field yields at most `slots x gather_amount_per_trip` per `gather_seconds_per_trip` however many workers it has. Walking is not limited, so the number of workers that saturate a field grows with the walk to the drop-off.

**Why this model and not a rate falloff.**
1. *A second base raises income.* One base's yield is capped by its slots; a depot at a second field cluster adds its slots. Section 1.2 gives the numbers.
2. *Legible.* A player sees workers standing at a full field. "3 of 3 slots, 2 waiting" can be shown on the field. A falloff (each trip slower as more workers share the field) is invisible and its trip time depends on a count that changes during the trip.
3. *Deterministic and cheap.* Occupancy is derived from unit state each tick (no per-field state to keep in step), two integers per worker are the only new hashed values (the queue tick and the slot grant count), and the queue order is a total order on (arrival tick, slot grants so far, entity ID). Below saturation the timing of every trip is **identical** to today (section 2.4), so the change bites only where workers are crowded.

Rejected: falloff (above); a per-player slot count (enemy workers at a contested field would not compete, and the rule gets player-count cases); automatic redirection of waiting workers to another field (a rule about which field is "near" and "free" that the player cannot see; Q-2).

### 1.1 Tuning keys (`rts/data/tuning/economy.json`, insert after `gather_seconds_per_trip`)

```
  // Workers that can gather one common field at the same time; a worker arriving at a full field waits beside it for the next free slot, first come first served. Unit: workers (count). Range: 1 to 16. Source: developer decision 18 Nov 2026 (field saturation); placeholder.
  "gather_slots_per_common_field": 3,
  // Workers that can gather one rare field at the same time; extra workers wait as at a common field. Unit: workers (count). Range: 1 to 16. Source: developer decision 18 Nov 2026 (field saturation); placeholder.
  "gather_slots_per_rare_field": 2,
```

`EconomyTuning`: add both to the known keys; read with `RequireInt(key, 1, 16)` as `GatherSlotsCommon` and `GatherSlotsRare` (doc comments repeat Unit, Range, Source); add `int GatherSlots(ResourceKind kind)`. Both keys are required; a missing key is a load error naming the file and the key, as for every other key. Update the file's line-1 header to mention gather slots.

### 1.2 Expected income at these numbers (the developer's view of the effect)

Inputs: `gather_amount_per_trip` 5, `gather_seconds_per_trip` 2.0 (40 ticks at 20 ticks per second), so one slot yields at most 2.5 common per second (150 per minute). Measured per-worker rate at the `prototype_1v1` main fields (5.4 tiles from the HQ): about 1.6 common per second (96 per minute; `reports/ai_v4_rush/README.md` section 3, same at 6 and 14 workers). One worker then holds a slot 40 of about 62 ticks, so about 1.55 workers fill one slot and **one main field saturates at about 4.7 workers, the two main fields at about 9.3**. "2 bases" = the main plus a depot at the natural (fields 2 and 3, common); it also serves rare field 16.

| Workers on common | Today, any number of bases | SAT, 1 base (cap 900/min) | SAT, 2 bases (cap 1800/min) |
| ---: | ---: | ---: | ---: |
| 6 | 576 | 576 | 576 |
| 10 | 960 | 900 | 960 |
| 14 | 1344 | 900 | 1344 |
| 20 | 1920 | 900 | 1800 |
| 24 | 2304 | 900 | 1800 |

Common per minute, `min(workers x 96, cap)`. Rare: field 16 with 2 slots yields at most 300 rare per minute (about 3 workers), so its 600 last about 2 minutes at saturation. Today a second base adds nothing until the first runs dry; with SAT it doubles the ceiling, and a third base is needed beyond about 19 common workers. The 2-base column assumes the natural's walk equals the main's; the depot site (3 to 6 tiles from the field) may be longer, which only raises the workers needed, not the cap. Test SAT-15 measures the real values; SAT-M reports them.

### 2. Simulation rules

#### 2.1 State

- `GatherState` (`Economy/GatherState.cs`): add `Waiting = 4` ("At a full field, waiting for a gather slot; first come first served."). Values stay stable.
- `Unit` (`Units/Unit.cs`): add `int GatherQueuedTick` ("Tick this worker started waiting at its field; -1 unless GatherState is Waiting. Unit: ticks. Range: -1, or 0 to the current tick. Hashed."). `SetGather` sets it to -1 for every state. New `internal void SetGatherWaiting(int fieldIndex, int tick)` sets `GatherState = Waiting`, `GatherFieldIndex = fieldIndex`, `GatherTicksRemaining = 0`, `GatherQueuedTick = tick`. Initial value -1.
- `Unit`: add `int GatherSlotGrants` ("Slot grants this worker has received in its lifetime. Unit: grants (count). Range: 0 to int.MaxValue. Hashed."). Incremented by 7a on each grant; never reset (not by leaving Waiting, not by any order). Initial value 0.
- No per-field state. Occupancy is counted each tick from units (2.3), so it can never disagree with the units.

#### 2.2 Where it runs

Inside step 7 (economy); no new step. `EconomySystem.Step` calls a new `AssignGatherSlots(sim)` **before** taking the unit-ID snapshot and running the unit loop. Header clause for step 7 in `Simulation.cs` and the `EconomySystem.cs` header: "7a gather slots (EconomySystem.AssignGatherSlots: waiting workers take free field slots in (queue tick, ID) order); 7b the unit loop". Replace the `EconomySystem.cs` line "Any number of workers may share a field (Phase 1 placeholder: no slot limit)" with the slot rule.

#### 2.3 `AssignGatherSlots` (7a)

1. Walk `sim.Units.All` (ascending ID) and collect every unit with `GatherState == Waiting` and a valid field index into a list `waiting`; return at once when it is empty. Otherwise `occupied = new int[sim.Resources.Count]` and a second walk adds 1 to `occupied[index]` for each unit with `GatherState == Gathering` and a valid field index.
2. Sort `waiting` by `GatherQueuedTick` ascending, then `GatherSlotGrants` ascending (the worker with the fewest grants so far first), then `Id` ascending (a total order, so `List.Sort` with this comparer is deterministic).
3. For each worker in that order: field `f = sim.Resources[index]`. Skip it when `f.IsDepleted` or when the worker is not within `InteractRange(sim)` of the field's tile centre (the loop deals with both, 2.4). Otherwise, if `occupied[index] < GatherSlots(f.Kind)`: `SetGather(Gathering, index, GatherTicksPerTrip(clock))`, `worker.AddGatherSlotGrant()` (increments `GatherSlotGrants`) and `occupied[index]++`.

Fields are independent, so the cross-field order does not matter. Cost: one pass over units plus a sort of the waiting workers.

#### 2.4 Changes in `StepGather` (7b)

- **ToField, arrival** (today: `SetGather(Gathering, ...)` at `EconomySystem.cs:517`): `ClearMoveTarget()` as today, then `SetGatherWaiting(field.Index, sim.Clock.Tick)`. Every arrival waits at least until the next tick's 7a, even at an empty field. This is the only way into `Gathering`.
- **Waiting** (new case): if `field.IsDepleted`: `ClearGather()`, `ClearMoveTarget()` (as Gathering does today). Else if the worker is not within `InteractRange(sim)` of the field point (pushed away, for example by a flood wave): `SetGather(ToField, index, 0)` and `SetMoveTarget(EconomyRules.StandablePointNear(sim, fieldPoint, worker.EffectiveLayer))`; it queues again, with a new tick, when it arrives. Else nothing.
- **Gathering, ToDropOff:** unchanged.

**Timing is unchanged below saturation.** Today a worker arriving on tick A is set to Gathering with G ticks and decrements from tick A + 1, so it takes its load on tick A + G. With SAT it waits on tick A, 7a of tick A + 1 sets G and the loop decrements it in the same step, so it takes the load on tick A + G as well. Test SAT-2 pins this.

**Hand-over at a full field.** A worker that takes its load on tick t leaves the slot in 7b of tick t. 7a of tick t + 1 gives the slot to the first waiting worker, which takes its load on tick t + G. A slot is therefore held without a gap: one load per G ticks per slot.

#### 2.5 Interactions

| Case | Behaviour | New code |
| --- | --- | --- |
| Field depleted | A load that empties the field emits `FieldDepletedEvent` (unchanged). Waiting workers go Idle in 7b of the same tick if their ID comes after the gatherer's, else on the next tick (7a skips them, 7b clears them). They carry nothing. | Waiting case only |
| Worker dies (6d) | It is gone before 7a of the same tick, so its slot is free on that tick. | none |
| Move, attack, stop, gather, build orders (step 2) | They leave the loop or restart it (`StartGatherLoop`, `CancelBuild`), so the slot or queue place is freed on that tick. A worker that returns from a build order goes ToField and queues again. A gather order to the field the worker already waits at also re-queues it (new tick). | none |
| Flood wave push (step 8) | A pushed Waiting worker goes back to ToField (2.4). A pushed Gathering worker keeps its trip, as today. | Waiting case only |
| Unit separation (step 5) | Gather-loop workers, waiting ones included, stay out of separation (`IsParticipant` already excludes `GatherState != Idle`). Q-3. | none |
| Gather command to a full field | Accepted. No new rejection, no new command. | none |
| Enemy workers at the same field | They share its slots (Q-4). | none |
| HQ relocation, drop-off loss | Unchanged: they affect ToDropOff only. | none |

#### 2.6 Hash, digest, events, wire format, replays

- **Hash.** `Unit.WriteHash` writes `GatherQueuedTick` (int) right after `GatherTicksRemaining`, then `GatherSlotGrants` (int). `GatherState` is already hashed as a byte; `Waiting` is its new value 4. Nothing else is added: occupancy is derived.
- **Content digest.** The two tuning keys change `Simulation.ContentDigest`, so every state hash moves from tick 0.
- **Events.** None new. The renderer reads `Unit.GatherState == Waiting` and the slot counts from `sim.Tuning.Economy` (section 4). Listeners never change state (unchanged rule).
- **Wire format and replays.** No command or payload changes; `MessageCodec.ProtocolVersion` and `ReplayFormat.CurrentVersion` stay. Replays recorded before SAT are refused by `ReplayCheck` on the content digest (`REPLAY REFUSED`, exit 2) before any step runs, as ADR-009 allows for a rule change (same handling as `worker_construction.md` section 7). Say so in the changelog.

#### 2.7 What SAT does to the existing AI (no AI code change in SAT)

Old profiles put 3 workers on a common field (below about 4.7) and 2 on a rare field (below about 3), so their normal income does not change. Queues appear only where the AI crowds a field: the long haul when starved, `Rebalance` fallbacks, and (on the unmerged branch `review/ai-v4-f`) the worker-safety evacuation. The AI treats a Waiting worker as busy on its field (`GatherState != Idle`, `GatherFieldIndex` set), which is correct. `AiWorldView` passes `GatherState` through unchanged.

#### 2.8 Tests (`rts/tests/Sim.Tests/Economy/GatherSlotTests.cs`; S = `Tuning.Economy.GatherSlotsCommon`, R = `GatherSlotsRare`, G = `GatherTicksPerTrip`; tests read S, R and G from the tuning so they survive a retune)

Setup: `TestWorld.StartedEconomy()` as in `worker_construction.md` 11.1 (worker speed 4 tiles per second, 20 ticks per second), workers spawned within interact range of the field unless stated, a drop-off beside it.

| Id | Test | Pass |
| --- | --- | --- |
| SAT-1 | Loader | Both keys required; missing, 0 and 17 are load errors naming `tuning/economy.json` and the key. `GatherSlots(Common) == 3`, `GatherSlots(Rare) == 2` on shipped data. |
| SAT-2 | Timing below saturation | One worker ordered to gather, arriving on tick A, takes its first load on tick A + G (same as before SAT); on tick A it is Waiting with `GatherQueuedTick == A`, on A + 1 Gathering. |
| SAT-3 | Overflow waits | S + 1 workers arrive on the same tick: S are Gathering on the next tick, the highest-ID one is Waiting; it becomes Gathering on the tick after the first of the others takes its load. |
| SAT-4 | First come first served | Field full. Worker with ID 9 starts waiting on tick 100, worker with ID 5 on tick 101: when one slot frees, ID 9 gets it. Two workers starting on the same tick: the one with fewer slot grants so far gets it; if that is equal too, the lower ID. |
| SAT-5 | No starvation | 2S + 2 workers on one field with a drop-off 2 tiles away for 120 s: every worker deposits at least `floor(total loads / workers) - 1` loads. |
| SAT-6 | Throughput cap | Same setup, N = 2S + 2, T = 1200 ticks: resources taken from the field by tick T are at most `S x amount x (floor(T / G) + 1)` and at least 90 percent of that (the cap is reached, not just bounded). |
| SAT-7 | Rare uses rare slots | R + 1 workers on a rare field: R Gathering, 1 Waiting. |
| SAT-8 | Depletion | A field with `amount` units left, S gathering and 2 waiting: after the load that empties it, exactly one `FieldDepletedEvent`; within one tick every waiting worker is Idle with nothing carried. |
| SAT-9 | Death frees a slot | Full field with one waiting worker; a gathering worker is killed (`remove_unit` or a combat kill) on tick t: the waiting worker is Gathering on tick t. |
| SAT-10 | Orders free a slot | A move order to a gathering worker on tick t: the waiting worker is Gathering on tick t. |
| SAT-11 | Pushed away | A Waiting worker moved out of interact range (internal test hook on position): next step it is ToField with `GatherQueuedTick == -1`; it queues again on arrival with the new tick. |
| SAT-12 | Build order and back | A Waiting worker given `build_structure`: it leaves the queue; after placing it returns to ToField and queues with a new tick. |
| SAT-13 | Separation | With separation on, a Waiting worker touching another unit is not moved by step 5. |
| SAT-14 | Hash | Two simulations equal except one worker's `GatherQueuedTick` hash differently; one unit's hash input grows by exactly 4 bytes against a stream written without the field (`BytesWritten`). |
| SAT-15 | Income, 1 vs 2 bases (`prototype_1v1`, shipped data) | Player 0's HQ Rooted at its spawn. (a) 14 workers gather fields 0 and 1 (7 each) for 300 s; (b) the same plus a complete `skai.perch_depot` spawned on the first good tile 3 to 6 tiles from field 3, 7 workers on fields 0 and 1 and 7 on fields 2 and 3 (split as evenly as possible, lower field index first). Pass: common gathered in (a) at most `2 x S x amount x (6000 / G + 1)`; (b) at least 1.25 times (a). Print both rates per minute and the count of worker-ticks spent Waiting to `TestContext` for the SAT-M report. |
| SAT-16 | Any player count | Three players on three teams, each with S workers on one shared field: no more than S Gathering on any tick; two runs give the same hash on every tick. |
| SAT-17 | Determinism | Two runs of `economy_smoke.json` and of a 6-worker one-field script give identical output and hashes. |
| SAT-18 | Re-pins | Every value in section 3 holds its new value. |

Existing tests that put more than S workers on one field and assert income or timing will change: update their expected values with a comment line naming this spec, and list them in the SAT report.

### 3. Pinned values that move (all re-pinned in SAT)

Re-pin each with one comment line: "Re-pinned for field saturation (specs/field_saturation.md): gather slot keys in the tuning digest, `GatherQueuedTick` in the unit hash." For AI outcomes, write the old and new `OUTCOME` line in the report and the comment. Each AI game must still be decided; if not, stop and report.

| # | Value today | Where |
| --- | --- | --- |
| 1 | `0x414b070cac0f68aaUL` | `rts/tests/Sim.Tests/DeterminismTests.cs:102` `GoldenFinalHash` |
| 2 | `STATE_HASH=c423332711029434` | `rts/tests/Sim.Tests/Harness/HarnessUnitTests.cs:175` (smoke) |
| 3 | `STATE_HASH=ce62b175ea751ba6` | `HarnessUnitTests.cs:177` (economy_smoke) |
| 4 | `STATE_HASH=ce62b175ea751ba6` | `rts/tests/Sim.Tests/Economy/WorkerConstructionTests.cs:737` |
| 5 | `0x38e1f3919c6d0aebUL`, `OUTCOME winner=1 tick=7613` | `rts/tests/Sim.Tests/Ai/SkirmishAiTests.cs:866`, `:876` (A-11) |
| 6 | `20151`, `"015496c40671ead9"` | `SkirmishAiTests.cs:895`, `:898` (C2-11) |
| 7 | `15462`, `"e5c323f688131dd0"`, `"fde24604c8dc84e6"` | `rts/tests/Sim.Tests/Ai/AiVarietyTests.cs:30`, `:33`, `:36` (S3 self trace) |

Also: the two `Rts.Net.Tests` AI matches (`LockstepTests.cs`, `MultiPeerLockstepTests.cs`, "not decided by tick 12000") must still be decided. `Replay.Tests/EndToEndTests.cs` computes its hash at run time. Documents that quote hashes (`STATUS.md`) are the coordinator's.

**Harness trace column (SAT, no hash effect).** `GameTrace` adds `waiting=<n>` per player to each timeline sample (own workers in `GatherState.Waiting`), so SAT-M and the AI reports can see queues. Document it in `rts/tools/BalanceHarness/README.md`.

### 4. PC-session follow-ups (Unity, not in SAT)

1. Repeat the Unity-versus-headless hash check against the new `economy_smoke` hash (every hash moves).
2. Draw Waiting workers distinctly (standing at the field, no gather animation).
3. On field hover or selection show "gathering a / slots s, waiting w" (count units by `GatherState` and `GatherFieldIndex`; slots from `sim.Tuning.Economy.GatherSlots(kind)`).
4. Optional HUD: own income per minute, so the effect of a second base is visible.

---

## AI packages: common rules

**Profile.** AI-E creates `rts/data/ai/aggressive_s5.json`, a copy of `aggressive_s4r.json` (as on `main` when AI-E starts, so with the L-R openings if they were merged, section 10), `id` changed, AI-E keys on. AI-P, AI-H and AI-HQ turn their own keys on in the same file. Each package also commits an ablation `abl_s5_no_<pkg>.json`: `aggressive_s5` with only that package's keys at their old values (so it equals `aggressive_s5` as it was before the package). The header comment of each file says what it is.

**`AiWorldView` additions (AI-E).** Read once per view from the Sim tuning: `GatherSlotsCommon`, `GatherSlotsRare`, `GatherTicksPerTrip` (`sim.Tuning.Economy.GatherTicksPerTrip(clock)`), `InteractRangeTiles` (the same value as `EconomySystem.InteractRange`: `interact_range_tiles`, never less than the movement arrival radius); AI-P adds `StaticDefencesPerBaseMax` (`sim.Tuning.Supply.StaticDefencesPerBaseMax`). Public tuning only.

Each package updates the `AiBrain.cs` header (its rules), the `AiProfile.cs` "Differences" list and `rts/src/Ai/README.md`.

---

## Package AI-E: the AI economy follows the rule

### E1. Worker caps from saturation (`use_saturation_workers`)

1. **Per-field cap.** For a served field f with nearest planned drop-off D (nearest by squared distance, ties to the lower ID): `d = |f centre - D position|` (`FixVec2.Length`, deterministic; capped at `field_max_distance_tiles`); `walk = max(0, d - 2 x InteractRangeTiles - D radius)` (one way, tiles); `W = ceil(2 x walk x TicksPerSecond / workerSpeed)` (ticks, `Fix64` then `CeilToInt`; workerSpeed = the plan worker's `SpeedTilesPerSecond`); `slots = GatherSlots(kind)`; `cap_f = slots + ceil(slots x W / G) + saturation_extra_workers_per_field` (integer ceil `(a + b - 1) / b`). Example: main field, slots 3, G 40, d 5.4, interact 1.5, Great Aerie radius 1.2, speed 2.5: walk 1.2, W = ceil(19.2) = 20, cap = 3 + ceil(60 / 40) = 5.
2. `CapOf(f)` returns `cap_f` instead of `workers_per_field` / `rare_workers_per_field` (rare subject to 4). `Rebalance`, `NearestField` and `WantedWorkers` use it unchanged.
3. **`WantedWorkers`** = min(`max_workers`, sum of `cap_f` over served fields); while any field has known resources, at least `cap_f` of the first field in target order (replacing "at least `workers_per_field`"). `max_workers` becomes a safety ceiling only: `aggressive_s5` sets 48 (two bases need about 21, three about 32).
4. **Rare cap (C2).** With `rare_cap_yields_to_saturation`, the rare soft cap closes rare fields only while some served common field has fewer assigned workers than its `cap_f`; when every served common field is full, rare fields stay open whatever the rare bank. The hysteresis (`rare_bank_return_cap`) is unchanged.

### E2. Depots from saturation (`use_saturation_expansion`)

1. **Saturation trigger.** Rule E's `low` test becomes `low || saturated`, where `saturated` = own workers (queued at the HQ included) >= `expand_saturation_fraction` x `WantedWorkers` computed without the `max_workers` ceiling. So a full base expands even before its clock gate; the clock gate and opening timings still apply as today.
2. **Threat gate.** `UnderThreat` blocks a depot only while the visible threat supply is at least `expand_block_threat_supply` (0 keeps today: any threat blocks). The target-site safety test (`expand_safe_radius_tiles`) is unchanged, so a depot is still never sent into a seen army.
3. `aggressive_s5` sets `max_live_depots` 4. One depot under construction at a time is kept (the order costs a worker trip and 100 common; two at once has no measured need). `field_max_distance_tiles` 10 stays: depot sites are 3 to 6 tiles from their field, and E1 counts walk distance, so a farther field simply gets a larger cap.

### E3. Keys (root of every profile)

| Key | Unit, range | Old profiles | `aggressive_s5` |
| --- | --- | --- | --- |
| `use_saturation_workers` | boolean | false | true |
| `saturation_extra_workers_per_field` | workers, 0 to 4 | 0 | 0 |
| `rare_cap_yields_to_saturation` | boolean | false | true |
| `use_saturation_expansion` | boolean | false | true |
| `expand_saturation_fraction` | fraction, 0 to 1 | 1 | 0.8 |
| `expand_block_threat_supply` | supply, 0 to 200 | 0 | 4 |
| `max_workers` (existing) | | unchanged | 48 |
| `max_live_depots` (existing) | | unchanged | 4 |

Comment line per key: `// <what>. Unit: . Range: . Source: field_saturation.md E<n>; placeholder.`

### E4. Tests (`rts/tests/Sim.Tests/Ai/AiSaturationTests.cs`)

| Id | Pass |
| --- | --- |
| E-1 | Cap formula: the worked example in E1.1 gives 5. A common field 9 tiles from a depot (radius 0.5), worker speed 2.0: walk 5.5, W = 110, cap = 3 + ceil(330 / 40) = 12. With `saturation_extra_workers_per_field` 1 each cap is one higher. |
| E-2 | `WantedWorkers` = sum of caps over served fields, clipped by `max_workers`; the floor uses the first field in target order. |
| E-3 | Rare cap: rare bank above `rare_bank_soft_cap`, every served common field at cap: rare `CapOf` > 0. One common field below cap: rare `CapOf` = 0. With the switch off: today's result in both cases. |
| E-4 | Saturation expansion: before `expand_at_seconds`, workers at 0.8 x wanted: a depot is ordered; at 0.7: not. Live depots at `max_live_depots`: not. |
| E-5 | Threat gate: 3 supply of visible threat with `expand_block_threat_supply` 4: depot ordered; 4 supply: not; with 0: any threat blocks. |
| E-6 | Greybox run, `aggressive_s5` mirror, 600 s: assigned workers on any served field never exceed its `cap_f` + 1 for more than one think; the first depot is placed by the clock gate or saturation, whichever is first. As built (review fixes): the first depot is ordered no later than min(the think where the saturation trigger holds and the bank can pay, the clock gate) plus a walk and two thinks; a closed rare field (cap 0) keeps the workers it has and is never the target of a new gather order (rule G4 does not force them onto a full common field when `rare_cap_yields_to_saturation` is on); the cap check applies only while some served field below its cap could take a worker. |
| E-7 | Old profiles: A-11, C2-11 and the S3 self trace hold their SAT values without re-pinning. |

---

## Package AI-P: placement and static defence

### P1. Producers at expansions (`use_forward_producers`)

The Sim lets a player build on any explored tile; the AI's HQ ring is a convention. Modest change: an **extra producer** (rule X) and a **tech producer** (rule T) go to the own base with the fewest producers within `base_radius_tiles` (economy tuning) of it, ties to the HQ first, then the lower depot ID. A base = the Rooted HQ, or a complete own depot whose served fields have known resources. Around a depot the site search is the HQ ring search with the depot as centre and the depot's tile height as the base height (same `IsGoodSite`, same `build_ring_min_tiles` and `build_ring_max_tiles`, same field clearance). If that base has no good site, fall back to the HQ ring. Build-order steps, supply buildings and depots keep their sites. Effect: a raid on one base no longer stops all production, and reinforcements appear nearer the expansions.

**Out of scope now (opening for later):** forward or proxy production near the enemy or the Core. The site search takes its centre as a parameter, so a later opening can add an anchor point without another site rule.

### P2. Static defence by threat (`use_threat_defence`)

1. **Per-base cap.** Effective cap = min(`static_defence_max_per_base`, `StaticDefencesPerBaseMax`). Old profiles are at most 6 (key range 0 to 6), so the min changes nothing for them. `aggressive_s5` sets 6.
2. **New step SD-4, after the existing steps (raid, unanswered layer).** The **exposed base** is the base (as P1) nearest to the last seen enemy army position (within `enemy_army_memory_seconds`), else nearest to the nearest known enemy structure or uncleared enemy spawn; ties to the HQ, then the lower depot ID. The fight predictor (`AiCounter`, as rule SD and package D already call it) compares the enemy model's army against own army units within `defend_radius_tiles` of that base plus own static defences there plus the Rooted HQ when it is the base. When the enemy model's army supply is at least `threat_defence_min_enemy_supply` and the predicted enemy advantage is at least `threat_defence_min_enemy_advantage`, build one defence at the exposed base (ring sites `static_defence_ring_min_tiles` to `_max_tiles`, nearest the enemy point; `PickDefence` as today) if: no defence is under construction (as today), the base is below its cap, and own defences plus the new one are worth at most `threat_defence_max_value_share` x own army value (same formula as `WithinValueCap`, own key).
3. The raid trigger and its value cap stay as they are (package F changes them on its own branch).

**Out of scope now:** a turtle opening that builds defences by build order (an opening field for later).

### P3. Keys

| Key | Unit, range | Old profiles | `aggressive_s5` |
| --- | --- | --- | --- |
| `use_forward_producers` | boolean | false | true |
| `use_threat_defence` | boolean | false | true |
| `threat_defence_min_enemy_supply` | supply, 0 to 200 | 0 | 8 |
| `threat_defence_min_enemy_advantage` | ratio, 0 to 100 | 0 | 1.0 |
| `threat_defence_max_value_share` | fraction, 0 to 2 | 0 | 0.4 |
| `static_defence_max_per_base` (existing) | | unchanged | 6 |

### P4. Tests (`AiPlacementTests.cs`)

| Id | Pass |
| --- | --- |
| P-1 | HQ with 2 producers, one complete depot with live fields and 0 producers: rule X's site is within `build_ring_max_tiles` of the depot, on the depot's height. Equal counts: the HQ ring. Depot with no good site: the HQ ring. |
| P-2 | Switch off: every site equals today's (`TryFindSite`) on the same view. |
| P-3 | Exposed base and SD-4: a modelled enemy army of 10 supply, predicted advantage 1.5, near the depot: one defence ordered at the depot ring; with 7 supply or advantage 0.9: none; at the per-base cap: none; above the value share: none. |
| P-4 | Effective cap: profile 6, Sim 6: 6; a test tuning with Sim 4: 4. |
| P-5 (added in the build) | Hysteresis: SD-4 stays armed while the enemy supply and advantage stay at `threat_defence_release_fraction` (key added, old 1, `aggressive_s5` 0.75) of their thresholds, and the exposed base changes only to a base whose distance to the enemy is below that fraction of the current one's. Test: supply alternating 8 and 7 around the threshold 8 for 20 thinks flips the trigger once with the band and 20 times without; an enemy 8.6 tiles from the depot and 10.5 from the HQ leaves a remembered HQ exposure with the band and moves it without. |
| P-6 (added in the build) | `abl_s5_no_p` plays the pre-AI-P `aggressive_s5` bit for bit: three full prototype_1v1 games (seed 20261117; skai_vs_water 2, skai_vs_skai 3, water_vs_water 2) end in the state hashes recorded on `aggressive_s5` at `97f4d97`, before any AI-P code. |

Build notes (AI-P builder, 19 Nov 2026), deviations from the rows above: **P-3** the predicted advantage cannot be dialled to 1.5 or 0.9 in a real view, so the literal numbers are tested on the trigger rule itself (`AiBrain.ThreatDefenceHolds`: 10 supply with 1.5 builds, 7 supply or 0.9 does not) and the real-view tests use 10, 8 and 6 supply of Water graspers with no own units near the depot (advantage at the predictor cap) and an own army of 12 windrunners beside it (advantage below 1). The raid step is moved out of the way with `static_defence_after_raids` 20 in those tests. **P-1** the depot sits at tile (31, 10) so that no producer is within `base_radius_tiles` (12) of it. **E-6** (AI-E) now plays `abl_s5_no_p`, which is `aggressive_s5` as E-6 was written; with AI-P on, the same seed-1234 game trips its cap check for two thinks at 544 s (see `reports/ai_p/README.md`).

---

## Package AI-H: faction-fair harass speed

**Rule (when `harass_speed_ratio` > 0).** A candidate army unit may join the harass squad when its `SpeedTilesPerSecond` >= `harass_speed_ratio` x **chaser speed**. Chaser speed = the count-weighted mean speed of the enemy model's armed types whose weapon can target the candidate's layer; when the model has none, the plain mean over the enemy faction's mobile armed unit definitions that can target that layer (public unit data; the enemy faction is known from the match setup); when no enemy unit can target that layer at all, the candidate qualifies. The flyer exemption ("any air unit qualifies") is replaced by this test. With `harass_speed_ratio` 0 (old profiles) the rule is today's: air or speed >= `harass_min_speed_tiles_per_second`.

Rough effect at `aggressive_s5`'s 1.2 against the roster means (no model yet; means taken over all mobile army units, not checked weapon by weapon, so the tests compute the real values): against water (mean about 2.6, bar about 3.1) the Windrunner (3.5), Roc Rider and Thunderhead (4.0) qualify and the Talon Marksman (2.5) does not; against Skai (mean about 3.2, bar about 3.8) only the Reef Runner (4.1) qualifies. Water stays the slower faction, but the rule now follows what the enemy actually fields (against Shellbacks at 1.8 most water units qualify).

Key: `harass_speed_ratio`, ratio, 0 to 4; old profiles 0; `aggressive_s5` 1.2. Comment: `Source: field_saturation.md AI-H; placeholder.`

Tests (`AiHarassTests.cs`): H-1 chaser speed from a model of 2 Shellbacks and 1 Pummeler that hit ground: mean (1.8 + 1.8 + 3) / 3 = 2.2; a 3.0 unit qualifies at 1.2, a 2.5 unit does not. H-2 empty model: roster mean. H-3 nothing hits air: a flyer qualifies. H-4 ratio 0: identical squad to today on the same view.

Build notes (AI-H builder, 19 Nov 2026): the rule is `AiIntel.ChaserSpeed` and `AiIntel.MayHarass`; the enemy factions come from the new `AiWorldView.EnemyFactionIds` (players on other teams, from the match setup). Extra tests: **H-0** (shipped values, parser rejections, enemy factions in the view) and **H-5** (`abl_s5_no_h` plays three full games to the hashes recorded on `aggressive_s5` before any AI-H code: seed 20261117; skai_vs_water 2 `ae4f362f8bdc9885`, skai_vs_skai 3 `235626745316a81d`, water_vs_water 2 `b171e5cd2bf0bfeb`). **H-1**: "Pummeler" (speed 3.0) is the Water Grasper in the shipped data; the model is 2 `water.shellback` and 1 `water.grasper` on the Ground layer; the 3.0 and 2.5 candidates are passed as speeds. **H-3**: the shipped factions all have an anti-air unit, so "nothing hits air" is built by setting the view's enemy faction list to `neutral` (no armed mobile unit) or to nothing. A model that has armed types, none of which can hit the layer, falls to the roster mean (the spec's order), not to "qualifies". `Weapon.CanTarget` answers the static layer list, so the Shellback's and Reef Runner's "air only from water" does not count as hitting air.

---

## Package AI-HQ: what HQ mobility means

The HQ is the only worker producer, a drop-off and the strongest defence where it stands; uprooted it has reduced armour, no weapon and moves at 1.0 to 1.5 tiles per second. Meanings, and what is specified now:

| Meaning | Now? | Why |
| --- | --- | --- |
| **Consolidate on the best field cluster** when home is running out | **Yes** (below) | Keeps worker production and the HQ weapon where the income is. Today it waits until home is at 10 percent. |
| Move to a rare field | Deferred | A depot does it for 100 common without uprooting the only worker producer. |
| Retreat from a losing base | Deferred | Uprooting under fire removes the weapon and the armour for at least `uproot_seconds` + travel; with Last Stand disabled this mostly kills the HQ sooner. Revisit once Last Stand and the HQ floor are decided. |
| Forward move (toward the Core or the enemy) | Deferred | Needs forward production (P1's later opening) and a Core plan. |

**Rule HQ-C (when `use_cluster_relocation`).** For each relocation candidate (today's `RelocationCandidates`, same safety filter), **live slots** = sum of `GatherSlots(kind)` over fields with known remaining >= `expand_min_field_remaining` within `field_max_distance_tiles` of its stand-off point; home live slots are counted the same way around the HQ. Trigger: home known remaining <= `relocate_when_home_remaining_fraction` of its start (existing key; `aggressive_s5` 0.35) **and** the best candidate's live slots >= `relocate_gain_ratio` x home live slots (any candidate with slots when home has 0). Best = most live slots, then nearest to the HQ (squared), then lower field index. The "cheaper option exists" gate is skipped when the candidate's field is served by an own complete depot (the move consolidates; no depot is needed); otherwise it applies as today. **Safety, unchanged plus one:** no threat seen, cooldown, timeout, re-target on an unsafe destination, and never while the army stance is Defend.

Keys: `use_cluster_relocation` (boolean; old false; s5 true), `relocate_gain_ratio` (ratio, 1 to 10; old 1 (unused); s5 2.0); `relocate_when_home_remaining_fraction` s5 0.35.

Tests (`AiRelocationTests.cs`): HQ-1 home at 30 percent with 3 live slots, a safe depot cluster with 6: relocation starts there although live depots are at the cap. HQ-2 home at 40 percent: none. HQ-3 candidate 5 slots, home 3, ratio 2: none. HQ-4 stance Defend: none. HQ-5 switch off: today's choice on the same view.

Build notes (AI-HQ builder, 19 Nov 2026), deviations from the rows above: HQ-1 to HQ-5 use the spec's literal numbers (greybox_1v1; home 1800 of 6000 with field 1 at 200, below `expand_min_field_remaining`, so 3 live slots; the depot cluster is fields 6 and 7, 6 slots; the other clusters 5; live depots at the cap by `max_live_depots` 2 with two complete depots). Extra tests: **HQ-0** (shipped values, parser rejections for both keys), **HQ-6** (the one-way latch `AiMemory.ClusterLeftFields`: the fields within `field_max_distance_tiles` of the HQ when it uproots are never a cluster target again; the spec asked for hysteresis or a latch without naming one) and **HQ-7** (`abl_s5_no_hq` plays three full games to the hashes recorded on `aggressive_s5` before any AI-HQ code: seed 20261117; skai_vs_water 2 `ae4f362f8bdc9885`, skai_vs_skai 3 `f6ee85b4dc84a331`, water_vs_water 2 `b171e5cd2bf0bfeb`). Choices where the text was open: the cluster trigger uses a variant of `RelocationCandidates` that does not exclude fields served by an own complete depot (otherwise HQ-1 could never pick the depot cluster); the anchor field of a cluster is a common field (a move to a rare field is deferred), while rare fields in the cluster count as slots; "nearest to the HQ" is the anchor field's squared distance; a live field counts when its known remaining is at least `expand_min_field_remaining`. **E-6** (AI-E) now plays `abl_s5_no_hq`, which is `aggressive_s5` as E-6 was written; with AI-HQ on, the same seed-1234 game trips its cap check for two thinks at 485 s (see `reports/ai_hq/README.md`).

---

## 9. Measurement and gates (each package its own step; reports under `rts/docs/reports/saturation_<pkg>/`)

Every run: `prototype_1v1`, the base seed and game counts of the `ai_v4_rl` reports, `-c Release`. The gate bars are the existing ones and are not changed: LADDER >= 0.45 (`ladder_non_regression_fraction_min`) with a mirror-row Wilson upper bound >= 0.50; every self-ladder cell >= 0.8 distinct games per decided game; openings 0.35 to 0.65 in mirrors (report only, as in L-R); counted response scenarios: the gate stays 6 of 9, and a package fails if it passes **fewer** than its baseline.

| Step | Runs | Fails when | Then |
| --- | --- | --- | --- |
| SAT | Sim tests, review | any SAT test, a determinism finding | fix in SAT; nothing merges |
| SAT-M | (1) `--ladder --profile-a aggressive_s4r --profile-b aggressive_s4r`; (2) `--ladder --profile-a aggressive_s3 --profile-b aggressive_s4r --non-regression`; (3) `--response --profile aggressive_s4r` and `aggressive_s3`; (4) `--fights` once, matchup table compared with `reports/fights_2026-11-18` (expected identical: no worker gathers in a fight; any difference is reported); (5) SAT-15 numbers; (6) `waiting` per player at 180, 300, 480 s from one trace per matchup | never (record) | becomes the baseline for AI-E |
| AI-E | ladder `aggressive_s4r` vs `aggressive_s5 --non-regression`; s5 self-ladder; response; traces: workers and income at 180, 300, 480 s, first and second depot times | ladder or variety bar missed, fewer response passes, or median income at 300 s below `aggressive_s4r`'s | report; set the E switches to old values in `aggressive_s5` (the code stays); the developer decides; later packages go on |
| AI-P | ladder `abl_s5_no_p` vs `aggressive_s5 --non-regression`; response; count of producers outside the HQ ring and of SD-4 defences per game | bars as above, or fewer response passes than `abl_s5_no_p` | as above with the P switches |
| AI-H | ladder `abl_s5_no_h` vs `aggressive_s5`; harass squads per game and per faction | bars as above | as above |
| AI-HQ | ladder `abl_s5_no_hq` vs `aggressive_s5`; relocations per game, HQ deaths within 60 s of an uproot | bars as above, or more HQ deaths after uproot than the ablation | as above |

No tuning toward a gate. Allowed once per package, and reported: change one placeholder of that package if the result also holds at 0.5 and 1.5 times the new value (the v4 knife-edge rule). Ladder results are a record of the AI, not a balance reading (P-005 decision 11).

## 10. Branches invalidated

- **`review/ai-v4-lr`** (pressure openings, data only, measured on the pre-saturation rule). Its diagnosis rests on rule W's cap of 3 per field. Redo **first, right after SAT-M**: rebase onto `main`, re-run L-R-M on the SAT rule, merge on its own gate. AI-E then copies `aggressive_s4r` with L-R in it.
- **`review/ai-v4-f`** (raid response, failed its gate because evacuation crowds a field). Redo **after AI-E**: rebase, build its switches into `aggressive_s5` with `abl_s5_no_f` as the baseline, and measure options A and B there. With SAT a crowded field queues instead of over-gathering, and E1's caps make option A ("never overflow past the cap") the natural form. F goes before AI-P because AI-P's defence step sits next to F's raid-tower change.

Order: SAT, SAT-M, L-R, AI-E, F, AI-P, AI-H, AI-HQ.

## 11. Out of scope

Per-field slot counts in map data; automatic redirection of waiting workers; worker carry speed or trip amount changes; any unit, combat or map number; Unity work (section 4); forward/proxy production; turtle openings; the deferred HQ meanings; the rule R rare-reservation fix (`skirmish_ai_v4_rl.md` L-1 note).

## Questions for the developer (builders use the defaults)

- **Q-1. Saturation model and numbers.** *Recommended: gather slots with a first-come queue, 3 per common field and 2 per rare field (1 base tops out at about 900 common per minute with about 9 workers; 2 bases at about 1800).* Alternative: 2 common slots and 1 rare (1 base tops out at about 600 per minute with about 6 workers; expansion is forced in the first two minutes, close to the AI's current 3-per-field habit). Rejected alternative: a gather-rate falloff (invisible to the player).
- **Q-2. Do waiting workers move on by themselves?** *Default: no; the player (and AI rule G) moves them. The field display (section 4) makes the queue visible.*
- **Q-3. Gather-loop workers and unit separation.** *Default: keep them out of separation (walking, waiting and gathering), as today; otherwise they jam at fields and drop-offs and get pushed out of range.* Alternative: separate walking workers only.
- **Q-4. Do enemy workers share a field's slots?** *Default: yes; a contested field is shared.*
- **Q-5. `max_workers` for the new AI.** *Default: 48, a ceiling only; the target comes from served slots.*
- **Q-6. HQ retreat from a losing base.** *Default: deferred until Last Stand and the HQ floor are decided.*
- **Q-7. Forward production near the enemy.** *Default: later, as an opening.*
- **Q-8. Old replays.** *Default: refused by the content digest; no format or protocol bump.*

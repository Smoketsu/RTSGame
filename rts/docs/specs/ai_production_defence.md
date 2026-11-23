<!-- rts/docs/specs/ai_production_defence.md -->
<!-- Build spec, revision 1 (22 Nov 2026): the skirmish AI's production capacity and its defence of workers and bases under raid. Written after the developer's decision of 22 Nov that the next AI job is the Water AI's production and worker defence (Skai beat Water 72 of 72 on prototype_1v1 in reports/ai_m, ai_r and balance_first). Verifies the reported causes against the code and data, then specifies four faction-neutral packages behind four profile switches (use_production_capacity, use_supply_ahead, use_raid_workers, use_raid_response), off in every existing profile and on in one new candidate aggressive_s5_pd, plus a harness metrics package. No rts/src/Sim change, no unit, combat, economy, map or tuning number change, no pin moves. Behaviour bars on prototype_1v1 are fixed in section 9 before any run. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by the sim-reviewer after each package. Continues proposals/P-006 (takes slices of packages E and S, section 10). Revision 2 (23 Nov 2026, section R2 at the end): diagnosis of run C1's failed bars and a delta to rules RS, RW and RA (raid memory per field, raids kept alive by visible raiders, hits on units that died between thinks, a sticky per-raid answer, a fresh common-full test), plus a reported-only harness metric; sections 0 to 11 stay as built. -->
# Spec: production capacity and raid defence for the skirmish AI

**Revision 1, 22 Nov 2026**, written against `main` at `20858f2`; built and measured (`reports/ai_pd/`). **Revision 2, 23 Nov 2026**, written against `main` at `4d4e506`: section R2 at the end of this file, a delta on the built code; it changes nothing in sections 0 to 11 except where R2 says so.

Sources: developer decision of 22 Nov (next AI job: Water's production and worker defence; roster numbers frozen); `proposals/P-006-ai-built-for-balancing.md` (packages E and S, both paused); `proposals/P-005` (unit and combat numbers frozen; AI-versus-AI win rates are not read as balance); `specs/ai_mechanic_use.md` (form, switches, pins); `specs/skirmish_ai_v4.md` package F (raid response; built on branch `review/ai-v4-f`, failed its gate) and its report `reports/ai_v4_f/README.md` on that branch (the water-mirror diagnosis of rule WS); `reports/ai_m/README.md`, `reports/ai_m/balance/balance.jsonl`, `reports/ai_m/trace_*.txt`; `reports/ai_r/balance/balance.jsonl`; `reports/balance_first/`. Code read: `rts/src/Ai/AiBrain.cs` (header, `Decide`, `Turn` constructor, `Gather`, `Structures`, `TryBuild`, `BuyResearch`, `UpdateRaids`, `StaticDefence`, `WantedWorkers`, `TrainWorkers`, `Produce`, `TrainSupport`, `FindFreeProducer`, `ExtraProducer`, `ProducerStructureFor`, `Army`, `ScanThreat`, `DefendIfThreatened`, `IsInsideBase`, `Mass`), `AiBrainFight.cs` (`PredictedAttack`, `MeasuredDefence`), `AiCounter.cs` (header, `Pool`), `AiWorldView.cs`, `AiMemory.cs`, `SkirmishAiController.cs` (`Think`, `ReadEvents`); Sim (public reads only): `Economy/EconomyRules.cs` (`BuildTicks`, `FirstMissingRequirement`), `Units/UnitEconomyDefinition.cs`, the event classes `ResourceDepositedEvent`, `UnitDamagedEvent`, `UnitKilledEvent`, `UnitTrainedEvent`; harness `SummaryObserver.cs`; data `ai/aggressive_s5_m.json`, every `units/*/*.json`, `tuning/supply.json`, `tuning/match.json`, `scripts/ai_eval/ai_eval.json`.

## 0. Why: the reported causes, checked

The first investigation named four AI-side causes. Each was checked against the code (file and line on `20858f2`) and the recorded runs. Tallies below were recomputed from `reports/ai_m/balance/balance.jsonl` (216 games) and `reports/ai_m/trace_skai_vs_water.txt` (one game, 455 s, Skai P0, Water P1).

| # | Claim | Verdict | What the code and data show |
| --- | --- | --- | --- |
| 1 | Water builds its army from 1 Shell Forge plus Tidepools against Skai's 7 to 11 Roosts | **Corrected** | Both factions' supply buildings are producers (`units/skai/roost.json` trains Windrunner and Roc Rider; `units/water/tidepool.json` trains Spoutling, Pummeler, Reef Runner, Tidecaller). Final counts over the 72 cross games (medians): Skai 14 Roosts (range 9 to 19) and 1 Windspire; Water 9 Tidepools (2 to 13) and 1 Shell Forge (1 to 2). The gap is early, not at the end: in the trace, producers at 180 / 300 / 450 s were Skai 4 / 7 / 12 and Water 3 / 4 / 5. **Which rule sets the count:** for both factions it is mainly the supply rule 3a (`AiBrain.cs:1548-1556`: build `supply_building` when cap minus used is at most `supply_buffer` and no supply structure is under construction). Skai's supply stays at its cap from 240 s in the trace (49/50, 58/58 ... 106/106), so it builds Roost after Roost; Water's army and workers keep dying, its supply stays well under the cap (26/34 at 180 s, 13/50 at 420 s), so it builds few Tidepools. The rule that is meant to add producers when money piles up, rule X (`ExtraProducer`, `AiBrain.cs:3339-3376`), cannot fire for Water: it returns when any threat or recent hit is seen inside the base (`AiBrain.cs:3341`; Skai raids from about 120 s), and when any producer of a composition unit has an empty queue (`AiBrain.cs:3354-3357`) even if that producer can train nothing the AI can pay for now. In the trace the Shell Forge trained a Shellback about every 30 s from 120 to 300 s while the Tidepool trained 1 Pummeler by 300 s and the bank stood at 275 to 620. |
| 2 | Skai raids workers from about 120 s; Water loses 28 of 34 Tenders and keeps training replacements one by one into the raid | **Confirmed** | Trace: first Tender lost by 120 s; 28 Tenders lost and 34 trained in the 455 s game. Rule 4b (`TrainWorkers`, `AiBrain.cs:2816-2855`) trains whenever workers alive plus queued are below `WantedWorkers` (`AiBrain.cs:2728-2759`, the sum of the served fields' caps); it has no threat test. Rule G (`Gather`, `AiBrain.cs:1375-1447`) sends each idle worker to the nearest served field below its cap with no danger test (`IsUnsafe` filters only the long-haul list, `AiBrain.cs:1394-1402`); nothing moves a worker that is gathering beside raiders. The trace also has `worker_lost` build cancellations at 210, 360, 390 and 420 s. |
| 3 | Water's army sits at the HQ in `Massing` all game, never reaches the attack threshold, and does not defend the workers | **Partly corrected** | The stance label is `Massing` by construction: with `use_measured_defence` (on in the profile) `MeasuredDefence` (`AiBrainFight.cs:614-737`) orders a defend group without changing the stance, and sets `Defend` only when it recalls an attacking group (`AiBrainFight.cs:708`). The trace shows defending: Water units attack-moving (`atk=1/3`, `2/3`, `2/4`, `3/3` from 150 to 270 s) with the army centre 15 to 25 tiles from the HQ. So the army does respond. What fails is how: (a) when the whole army is short of `defend_min_advantage`, every candidate is still sent (the loop at `AiBrainFight.cs:680-684` ends with all of them chosen), and units trained one at a time arrive one at a time (Water lost 4 Shellbacks between 270 and 300 s); (b) the defence goes to the mean of every visible threat in every base (`AiBrainFight.cs:626`, `ScanThreat` `AiBrain.cs:3665-3690`), not to the raid that is killing workers; (c) the Rooted HQ's weapon is never counted on the defending side. The attack never starts (`attacks=0` all game) because `PredictedAttack` (`AiBrainFight.cs:179-204`) needs a predicted win at `attack_min_advantage` and `attack_min_supply`, or `attack_force_supply` 60; Water's army never exceeded about 11 supply. **The massing stall is a result of claims 1, 2 and 4, not a separate rule fault**; this spec adds no attack rule (section 9 reports army supply and attacks so the effect can be seen). |
| 4 | 400 to 600 common sits idle between 90 s and 270 s | **Confirmed, and found on both factions with different causes** | Water trace bank 275, 620, 450, 575, 415, 570, 465 at 90 to 270 s. Over the 72 cross games the median bank at 300 s is Water 535 and Skai 652; in the mirrors Water 695 and Skai 192. Causes found in the code: (a) rule X blocked as in claim 1; (b) **rule SU stops all production while rare is short**: `TrainSupport` (`AiBrain.cs:3249-3257`) reserves the full cost of a wanted support unit and returns true, which ends `Produce` for the think, even when only the rare part is missing. The Tidecaller (100 common, 25 rare, no `requires`) is wanted from 12 army supply, so while Water has no rare income no unit at all is trained on those thinks. Rule P itself filters rare-short candidates (`AiBrain.cs:3117`); SU does not. Consistent with this, Water's median bank at 300 s rose from 332 (cross) and 415 (mirror) in `reports/ai_r` (support units off) to 535 and 695 in `reports/ai_m` (support units on); walls also changed between the runs, so this is not isolated; (c) rule R (`TryBuild` `AiBrain.cs:1629-1636`, `BuyResearch` `AiBrain.cs:2192-2200`) reserves the common cost of an item that waits only for rare (a Reef Workshop at 150 common 50 rare, research items), holding common for as long as rare is missing; (d) **Skai's later idle bank is supply throughput**: rule 3a allows one supply structure under construction at a time (`AiBrain.cs:1548-1553`), 8 supply per 25 s, while its producers could use more; Skai's bank in the trace was 1085 at 300 s and 4245 at 450 s with supply at its cap. |

Could not confirm: whether, on the thinks where the trace's Tidepool stood idle, the cause was (b) above or a counter-mix weight of 0 for every Tidepool unit it could pay for (the counter-mix is recomputed each think from the enemy model and is not traced). Both are covered by the rules below (PC counts a producer's capacity only for units it can train and pay for now). Not verified per think: how much of the idle bank each of (a) to (d) holds; the measurement in section 9 reads the result, not the split.

Roster causes (Water's short range and slow front line against Windrunners at 3.5 tiles per second and range 4) are unit numbers, **frozen**. Nothing in this spec changes or depends on a change of a unit, combat, economy, tuning or map number.

### Ground rules

- **No `rts/src/Sim` change.** Every Sim read below is public and named. If a builder finds a Sim change unavoidable, stop and report to the coordinator.
- **No pin moves.** With the four new switches false every existing profile plays bit for bit as on `20858f2`. Pins (current values in the tests): smoke `207be73eaa460b3c` and economy_smoke `98b2e00d9babe167` (`Harness/HarnessUnitTests.cs:177-179`); `aggressive_s5` trace end tick 9505 hash `837f8e7118a9c9de` and `aggressive_s3` trace end tick 9223 hash `c04bdcc0d03b3910` (`Ai/FightSimTests.cs:113-114`); `V2ReferenceHash` `0xdfc8991f885607f2` and `BlindTraceHash` `846056c1a9efe5f4` (`Ai/SkirmishAiTests.cs:882, 917`). If one moves, stop and report. If `main` has re-pinned any of them before a package starts, use the values on `main` then.
- **Faction-neutral.** The brain never names a faction or unit ID; producers, supply buildings, workers and drop-offs are found by `Economy.Trains`, `Economy.ProvidesSupply`, `Economy.CanGather`, `Economy.IsDropOff` and the plan's `supply_building` and `worker_unit` (profile data). Every rule applies to Skai exactly as to Water.
- **Fog.** The AI reads only `AiWorldView`, its memory, and events about its own player (deposits to its own stock, hits on its own units). No hidden enemy state.
- **Commands.** Only `train_unit`, `build_structure`, `gather`, `move_unit` and `attack_move`, all already issued by the AI. No new command, intent kind or event type.
- **Determinism (AI).** `Fix64` and integers only; own units, fields and drop-offs walked in ascending entity ID or field index; distances compared squared; every tie to the lower ID or index unless a rule says otherwise. **No new random draw** in any package (the attack-threshold generator and `AiVariety` are untouched), so the draw sequence is identical with any switch on or off. AI memory is not hashed.
- **Numbers.** Every new decision number is a profile key with the house comment line `// <what it controls>. Unit: <unit>. Range: <range>. Source: rts/docs/specs/ai_production_defence.md <section>; placeholder.` Every value in this spec is a **placeholder**.
- **Parsing.** `AiProfile.Parse` reads every new key with the `Require*` helpers and adds it to `RejectUnknownKeys`; every profile file under `rts/data/ai` gets every new key (switch false, the "old" value of section 1); hand-written profile JSON in tests gets them too.

## 1. Switches, keys and profiles

| Switch | Turns on | Section |
| --- | --- | --- |
| `use_production_capacity` | rule RR (no common held for rare-short items) and rule PC (producers scaled to income; replaces rule X), and the deposit log | 3 |
| `use_supply_ahead` | rule SA (more than one supply structure under construction when production needs it) | 4 |
| `use_raid_workers` | rule RS (raid sites from hits and worker losses) and rule RW (closed fields: no workers sent, none trained for them, workers moved off) | 5 |
| `use_raid_response` | rule RS and rule RA (defend the newest raid, the HQ counts, hold when outmatched) | 6 |

All keys are root keys.

| Key | Unit | Range | Old profiles | `aggressive_s5_pd` |
| --- | --- | --- | --- | --- |
| `use_production_capacity` | boolean | true or false | false | true |
| `income_window_seconds` | seconds (integer) | 10 to 300 | 60 | 60 |
| `production_capacity_target` | fraction (Fix64) | 0.1 to 3 | 1 | 1 |
| `use_supply_ahead` | boolean | true or false | false | true |
| `supply_ahead_max_pending` | supply structures | 1 to 8 | 1 | 3 |
| `use_raid_workers` | boolean | true or false | false | true |
| `use_raid_response` | boolean | true or false | false | true |
| `raid_active_seconds` | seconds (Fix64) | 1 to 120 | 10 | 10 |
| `raid_field_radius_tiles` | tiles (Fix64) | 1 to 30 | 8 | 8 |
| `raid_evacuate_max_commands_per_think` | commands | 0 to 100 | 12 | 12 |
| `raid_hold_radius_tiles` | tiles (Fix64) | 1 to 20 | 4 | 4 |
| `raid_hq_cover_margin_tiles` | tiles (Fix64) | 0 to 20 | 2 | 2 |

Starting values (all placeholders): `income_window_seconds` 60 equals two of the harness's 30 s income buckets; `production_capacity_target` 1 means "producers can spend all army income"; `supply_ahead_max_pending` 3 lets three 8-supply structures (25 s each) cover what about ten producers can train in 25 s; `raid_active_seconds` 10 and `raid_field_radius_tiles` 8 follow package F's danger memory (10 s) and the natural clusters of `prototype_1v1` (three fields within about 5 tiles of each other, about 15 tiles from the HQ, so 8 tiles closes a whole natural without reaching the main fields); `raid_evacuate_max_commands_per_think` 12, `raid_hold_radius_tiles` 4 and `raid_hq_cover_margin_tiles` 2 are package F's values.

Validation (errors name the file and key path): `use_raid_response` true needs `use_measured_defence` true (rule RA lives in `MeasuredDefence`).

Profile comment lines (house format, copy as written):
- `use_production_capacity`: `// Rules RR and PC: no common is held for an item that waits only for rare, and a producer is added while producer capacity is below army income x production_capacity_target (replaces rule X). Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_production_defence.md section 3; placeholder.`
- `income_window_seconds`: `// Rule PC: common deposited to the own stock over this many seconds is the measured income. Unit: seconds. Range: 10 to 300. Source: rts/docs/specs/ai_production_defence.md section 3; placeholder.`
- `production_capacity_target`: `// Rule PC: producer capacity (common per minute the producers can turn into units) wanted per unit of army income (common per minute). Unit: fraction. Range: 0.1 to 3. Source: rts/docs/specs/ai_production_defence.md section 3; placeholder.`
- `use_supply_ahead`: `// Rule SA: another supply structure may be ordered while one is under construction when what the producers can train during one supply build would fill the cap. Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_production_defence.md section 4; placeholder.`
- `supply_ahead_max_pending`: `// Rule SA: most supply structures under construction or ordered at once. Unit: structures. Range: 1 to 8. Source: rts/docs/specs/ai_production_defence.md section 4; placeholder.`
- `use_raid_workers`: `// Rules RS and RW: fields near an active raid are closed (no workers sent or trained for them; workers on them move to an open field or home). Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_production_defence.md section 5; placeholder.`
- `use_raid_response`: `// Rules RS and RA: the defence goes to the newest raid, counts the Rooted HQ, and holds at home when even every candidate is predicted short. Needs use_measured_defence. Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_production_defence.md section 6; placeholder.`
- `raid_active_seconds`: `// Rule RS: a raid stays active this long after the last hit on an own worker or structure, or worker loss, near one of its drop-offs. Unit: seconds. Range: 1 to 120. Source: rts/docs/specs/ai_production_defence.md section 5; placeholder.`
- `raid_field_radius_tiles`: `// Rule RW: a field whose centre is within this distance of an active raid's last hit is closed. Unit: tiles. Range: 1 to 30. Source: rts/docs/specs/ai_production_defence.md section 5; placeholder.`
- `raid_evacuate_max_commands_per_think`: `// Rule RW: most orders per think that move workers off closed fields. Unit: commands. Range: 0 to 100. Source: rts/docs/specs/ai_production_defence.md section 5; placeholder.`
- `raid_hold_radius_tiles`: `// Rule RA: when the defence is predicted short, defenders farther than this from home move home; nearer ones get no order. Unit: tiles. Range: 1 to 20. Source: rts/docs/specs/ai_production_defence.md section 6; placeholder.`
- `raid_hq_cover_margin_tiles`: `// Rule RA: the Rooted HQ joins the defending side when the threat centre is within its weapon range plus this margin. Unit: tiles. Range: 0 to 20. Source: rts/docs/specs/ai_production_defence.md section 6; placeholder.`

**Profiles.**
- Every existing file under `rts/data/ai`: the keys above with the "old" values. With every switch false the AI plays as on `20858f2` (test OFF-1).
- New `rts/data/ai/aggressive_s5_pd.json`: a copy of `aggressive_s5_m.json` with `id` changed, the candidate values above and a line-1 header naming this spec. Package PD-1 creates it with only `use_production_capacity` true; each later package turns its own switch on there. Its off-copy for comparison is `aggressive_s5_m` itself.

## 2. Shared additions

**Memory** (`AiMemory`, not hashed; each written only when its switch is on):
- `DepositLog` (`List<(int Tick, int Common)>`, ticks and resource units; entries in tick order; pruned to ticks above `now - income_window_seconds` at each think). Written by `SkirmishAiController.ReadEvents` (it runs before every step, so no tick is missed): for each `ResourceDepositedEvent` with `Owner == Player`, `Resource == Common` and `Tick > Memory.LastEventTickRead`, append `(Tick, Amount)`. Only with `use_production_capacity`. Place the branch first in the loop, ending in `continue` (no existing branch reads this event, so no other branch changes).
- `RaidHits` (`SortedDictionary<EntityId, (int Tick, FixVec2 Position)>`, key an own drop-off, value the newest hit tick and its position), `OwnWorkerPositions` (`SortedDictionary<EntityId, FixVec2>`, the own workers of the last think, package F.3's method) and `RaidScanTick` (int, the tick of the last rule RS scan, -1 at start). With `use_raid_workers` or `use_raid_response`.

**Helpers in `Turn`** (integers; `BuildTicks(u) = max(1, view.SecondsToTicks(u.Economy.BuildSeconds))`, the AI-side copy of `EconomyRules.BuildTicks`):
- `Producible(u)`: composition unit u (weight above 0) with `RequirementsMet(u)` and (`u.CostRare == 0` or `RareServed()` or `u.CostRare <= rare - reserveRare`).
- `RateOf(u)`: `u.CostCommon * 60 * view.TicksPerSecond / BuildTicks(u)`, integer division. Unit: common per minute. Example: Pummeler 100 x 60 x 20 / 360 = 333; Shellback 175 x 1200 / 560 = 375; Windrunner 75 x 1200 / 360 = 250.
- `CapOfProducer(p)`: the largest `RateOf(u)` over the units in `p.Definition.Economy.Trains` that are `Producible`; 0 when none.
- `IncomePerMinute()`: sum of `DepositLog` amounts with tick above `now - SecondsToTicks(income_window_seconds)`, times 60, divided by `income_window_seconds`. Unit: common per minute.
- `WorkerRate()`: when the HQ is Rooted and `WorkersIncludingQueued() < WantedWorkers()`, `RateOf(worker unit)` (Tender or Fledgling: 50 x 1200 / 240 = 250); else 0.

## 3. Package PC: production scaled to income (`use_production_capacity`)

### 3.1 Rule RR: no common held for rare-short items

1. `TrainSupport`: a support unit with `CostRare > rare - reserveRare` (or, with budget queues, more than the army account plus free rare) is skipped (`continue`) before the money test; it reserves nothing and does not end `Produce`. This is the filter rule P already applies (`AiBrain.cs:3117`).
2. `TryBuild` and `BuyResearch`, rule R branch (no budget queues): when the common part can be paid (`common - reserveCommon >= CostCommon`) and only the rare part is short, add `CostRare` to `reserveRare` only. When the common part is short, reserve both as today.
3. Budget-queue branches are unchanged.

### 3.2 Rule PC: add producers while capacity is below income (replaces rule X)

Runs where `ExtraProducer` runs (after `Produce`, before `BudgetClose`); with the switch on, rule X does not run.

1. Skip while `view.Tick < SecondsToTicks(income_window_seconds)` (no full window yet).
2. `A = max(0, IncomePerMinute() - WorkerRate())` (army income, common per minute).
3. `C` = sum of `CapOfProducer(p)` over own structures in `_planned` (complete, under construction and pending build orders), ascending ID.
4. Skip unless `Fix64.FromInt(C) < Fix64.FromInt(A) * production_capacity_target`.
5. Skip when a visible enemy army unit is within `defend_radius_tiles` of the HQ position (the main is under attack; a raid elsewhere does not block).
6. Skip when the count of own structures in `_planned` that train a composition unit is at least `max_producers` (the existing ceiling).
7. Choice: among composition units u that are `Producible` and have no free producer (`FindFreeProducer(u) == null`), the one `PickByDeficit` picks (existing function, ties to the ordinally lowest ID). No choice: skip. Structure: `ProducerStructureFor(choice)`; null: skip.
8. Skip unless the structure can be paid now (`common - reserveCommon >= CostCommon` and `rare - reserveRare >= CostRare`; with budget queues, the production account plus free money). So rule PC never makes a reservation. Then `TryBuild(structure, null, true, AiBudget.Production)`. At most one structure per think; `build_retry_seconds` applies as for every structure.

Why each part: step 5 removes rule X's whole-base threat gate (the cause in claim 1); step 7's "no free producer that can train it" replaces rule X's "every producer busy", which a producer with nothing payable to train blocked forever; step 8 keeps the new rule from starving units.

## 4. Package SA: supply ahead of production (`use_supply_ahead`)

In `Structures`, the supply part (rule 3a). The build-order part is unchanged.

1. The old rule runs first, unchanged (one supply structure, with rule R reservation).
2. Then, with the switch on: `P` = sum of `ProvidesSupply` over own supply structures under construction or pending in `_planned`; `n` = their count; `H = BuildTicks(supply_building)`.
3. `N` = sum over own complete structures p that train a `Producible` unit of the largest `ceil(view.SupplyOf(u) * H / BuildTicks(u))` over p's `Producible` units, plus `ceil(H / BuildTicks(worker)) * view.SupplyOf(worker)` when `WorkerRate() > 0`. Integers (ceil as `(a + b - 1) / b`). Unit: supply.
4. Order one more supply structure when `SupplyCap + P - SupplyUsed < N + supply_buffer`, `SupplyCap + P < SupplyCapLimit`, `n < supply_ahead_max_pending`, and it can be paid now (as PC step 8; no reservation). At most one per think through `TryBuild(..., AiBudget.Economy)`.

## 5. Packages RS and RW: raids and workers (`use_raid_workers`)

### 5.1 Rule RS: raid sites (with `use_raid_workers` or `use_raid_response`)

New step `Turn.Raids()` in `Decide`, after `Cycle` and before `Gather`. A raid is recognised from **hits and losses only, never from a sighting**: package F's rule WS marked danger from any visible enemy and lost the water mirror 0 of 72 because single scouts and passing armies emptied whole naturals (`reports/ai_v4_f/README.md`, option B was the measured fix).
1. Losses: each ID in `OwnWorkerPositions` (ascending) that is not an own worker in the view is a loss at its stored position, at this tick. Then rebuild `OwnWorkerPositions` from the view's own workers.
2. Hits: each `RecentDamage` report (ascending unit ID) with `Tick > RaidScanTick` whose unit is not an own army unit in the view (an own worker, structure or HQ, or a unit no longer in the view) is a hit at the report's position and tick.
3. Each hit, then each loss: the nearest own drop-off (`DropOffs()`, ascending ID, squared distance, ties to the lower ID) within `defend_radius_tiles` gets `RaidHits[d] = (tick, position)` when the tick is at least the stored one (equal tick: the later in this order wins). None in range: ignored.
4. Remove entries whose drop-off is no longer an own unit or whose tick is below `now - SecondsToTicks(raid_active_seconds)`. Set `RaidScanTick = now`.
5. An **active raid** is any remaining entry. The **newest raid** is the entry with the largest tick, ties to the lower drop-off ID.

### 5.2 Rule RW: closed fields (`use_raid_workers`)

A field is **closed** while some active raid's position is within `raid_field_radius_tiles` of its centre (squared Fix64).
1. `CapOf(f)` is 0 for a closed field, so `WantedWorkers` drops its cap and rule 4b trains no worker for it (**no replacement is fed into an active raid**). With `use_saturation_workers` the floor of `WantedWorkersUncapped` is the cap of the first open field in target order (0 when every field is closed); without it the floor applies only while some served field is open.
2. `NearestField` (both modes), the long-haul list and `Rebalance` skip closed fields.
3. Evacuate, in `Gather` before the idle-worker loop: each own worker (ascending ID) without a build order whose `GatherFieldIndex` is a closed field is ordered `Gather` to `NearestField(position, assigned, true) ?? NearestField(position, assigned, false)` (open fields only), updating `assigned`. When both are null: `Move(Standable(Home()))` if no active raid's position is within `raid_field_radius_tiles` of Home, else no order. At most `raid_evacuate_max_commands_per_think` orders per think; the rest wait.
4. A worker with a build order is never re-ordered (as today). When the raid expires the fields reopen and idle workers are sent by the existing loop.

## 6. Rule RA: answering raids (`use_raid_response`)

Inside `MeasuredDefence`; the early return when there is no threat and no hit is unchanged.
1. **One raid at a time.** When an active raid exists: the threat set is the visible enemy army units within `defend_radius_tiles` of the newest raid's position, and the centre is their mean position, else the raid's position (then the existing blind branch, `defend_blind_supply`, picks the defenders). Threats elsewhere are left for later thinks. With no active raid: unchanged.
2. **The HQ counts.** When the own HQ is Rooted, complete, has a weapon and the centre is within `HQ weapon range + raid_hq_cover_margin_tiles` of it (squared), the HQ joins the fixed side with the static defences (package F.2 step 1).
3. **Hold when short.** When the candidate loop ends with `enough` still false: `H = Standable(Home())`; every candidate joins `DefendGroup`; a candidate farther than `raid_hold_radius_tiles` from H gets `Move(H)` unless it already has a move target within 2 tiles of H; nearer ones get no order and no `Focus` call (units fire at enemies in range by Sim rule). Package F.2 step 2. Units trained during the hold therefore wait at home instead of walking into the raid one at a time.
4. The attack-group recall rule (`defend_recall_supply`) is unchanged; a recalled group holds too when step 3 applies. When `enough` becomes true on a later think, the existing orders apply.

## 7. Package PD-M: harness metrics (build first; `rts/tools` only, no AI change)

New `GameSummary` fields, written to `balance.jsonl` per slot (appended keys; old reports are not re-rendered). Window end `T = min(summary_window_seconds, game end)`.

| JSON key | Meaning | Unit |
| --- | --- | --- |
| `worker_losses` | own workers (`CanGather`, footprint 0) in `UnitKilledEvent`s with `VictimOwner` = the slot and `KillerOwner` an enemy, up to T | workers |
| `worker_fed_losses` | of those, workers killed within `fed_window_seconds` of their own `UnitTrainedEvent` (start workers never count) | workers |
| `raid_seconds` | whole seconds up to T in which an own worker, structure or HQ within `raid_radius_tiles` of an own drop-off (complete drop-off structure or Rooted HQ) took a `UnitDamagedEvent` from an enemy-owned attacker | seconds |
| `raid_unanswered_edge_seconds` | raid seconds at whose last tick no own armed army unit (mobile, not worker, not HQ, has a weapon) stands within `raid_radius_tiles` of the second's newest hit or has a move or attack-move goal within that radius, while the own army supply is at least the supply of the visible-or-not enemy army units within `raid_radius_tiles` of that hit (the harness sees everything; this is measurement, not AI input) | seconds |
| `producers` | at each `summary_sample_seconds`: own complete structures whose `trains` holds a non-worker | structures |
| `capacity` | at each sample: sum over those producers of the largest `cost_common x 60 x ticks_per_second / build ticks` over their non-worker trainable units whose requirements are met (`EconomyRules.FirstMissingRequirement` null) and whose rare cost is 0 or at most the player's rare | common per minute |
| `income_60` | at each sample: common deposited to the slot in the 60 s before it | common |
| `army_supply` | at each sample: supply of own army units | supply |
| `bank_mean` | mean of the slot's common, sampled at every whole second from `bank_mean_from_seconds` to T inclusive (integer mean, floor); null when T is earlier | common |

Sample arrays hold null for samples after the game end. `BalanceReport` prints, per (faction, matchup) cell, the median of each field, and the median of `producers x 1000 / income_60` and of `capacity x 60 / income_60` per sample (null samples skipped).

Settings, in `rts/data/scripts/ai_eval/ai_eval.json`, each with the house comment line (Source: this spec section 7; placeholder): `summary_window_seconds` 600 (seconds, 60 to 2400), `summary_sample_seconds` [180, 300, 450] (seconds, 1 to 8 entries, ascending), `fed_window_seconds` 60 (seconds, 1 to 600), `raid_radius_tiles` 12 (tiles, 1 to 30; equals `defend_radius_tiles` of the profiles), `bank_mean_from_seconds` 60 (seconds, 0 to 600).

## 8. Tests

Real simulations as in `AiFightTests` and package F.6, `AiBrain.Decide` on the view; profile `aggressive_s5_pd` with the switch under test on and the others off unless stated; in hand-made test profiles `use_counter_mix` false, no `openings`, and `composition_jitter_fraction`, `expand_jitter_seconds` and `attack_advantage_jitter` 0, so the composition is the plan's as written in the test. Files: `rts/tests/Sim.Tests/Ai/AiProductionTests.cs` (PC, RR, SA), `AiRaidDefenceTests.cs` (RS, RW, RA), `Harness/ProductionDefenceMetricsTests.cs` (PD-M). **Raid common setup** is package F.6's: `greybox_1v1`, P0 Skai HQ at the greybox spawn, a complete `skai.perch_depot` of P0 at (18.5, 7.5), map explored for P0; fields 1 at (10, 4), 0 at (4, 10), 6 at (21, 10), 7 at (21, 4) (check against the map file; if they differ, use the file and state it in the test).

**Rule RR**
- **RR-1 Support does not stop production.** Water plan with composition `{ grasper 30, spoutling 10 }`, `support_units` `{ "water.tidecaller": 12 }`, one complete idle Tidepool, six own Pummelers (12 army supply), common 500, rare 0, workers gathering. Decide: one `Train` of `water.grasper` at the Tidepool (the Spoutling is filtered on rare); no Tidecaller. With `use_production_capacity` false: no `Train` at all.
- **RR-2 Rare-short structure holds no common.** A build-order step due for a structure costing 150 common and 50 rare, common 300, rare 0, income true: after `Structures`, `reserveCommon` is 0 and `reserveRare` is 50 (internal probe or a following `Train` of a 100-common unit that succeeds). Switch off: `reserveCommon` 150.
- **RR-3 Common short reserves both.** As RR-2 with common 100: `reserveCommon` 150 and `reserveRare` 50.

**Rule PC**
- **PC-1 Builds a producer when capacity is short.** Water plan, composition `{ grasper 30, shellback 10, ink_lobber 10, spoutling 10 }`, one complete Tidepool and one complete Shell Forge, both with a queued unit; rare 0, no rare field served; `DepositLog` holding 1200 common within the last 60 s; workers below `WantedWorkers`; common 400; tick 2400 (120 s); no enemy visible. Then I = 1200, W = 250, A = 950; caps: Tidepool 333 (Pummeler; Spoutling not producible), Shell Forge 375 (Shellback; Ink Lobber requirement unmet), C = 708 < 950. Choice: Pummeler (largest weight, no free producer). Expected: one `Build` of `water.tidepool`. Same with a visible Skai Windrunner 15 tiles from the HQ but within `defend_radius_tiles` of the depot (raid elsewhere): still the `Build`.
- **PC-2 No build when capacity meets income.** PC-1 with two more complete busy Tidepools (C = 1374 >= 950): no `Build` of a producer.
- **PC-3 Main under attack.** PC-1 with a visible enemy army unit within `defend_radius_tiles` of the HQ position: no `Build` of a producer.
- **PC-4 Idle producer that can train nothing payable does not block.** PC-1 with the Tidepool's queue empty and the composition `{ shellback 30, spoutling 30 }` (Spoutling not producible, rare 0): C = 375 (Forge) + 0 (Tidepool) < 950; choice Shellback (no free producer that can train it); expected `Build` of `water.shell_forge`. Rule X on the same view (switch off): no `Build` (it returns on the free Tidepool).
- **PC-5 No reservation.** PC-1 with common 100: no `Build`, and `reserveCommon` after the step equals its value before.
- **PC-6 Window.** PC-1 at tick 1000 (50 s, window not full): no `Build`.
- **PC-7 Faction-neutral.** Skai plan, composition `{ windrunner 30, talon_archer 20 }`, one busy Roost and one busy Windspire, same `DepositLog` and workers: caps 250 + 300 = 550 < 950; expected `Build` of `skai.roost`.
- **PC-8 Deposit log.** A real simulation with three P0 workers gathering for 60 s: `DepositLog` sums to the common the ledger shows deposited for P0 in that span (from `ResourceDepositedEvent`s), entries in tick order, nothing older than the window after a think. With the switch off the log stays empty.

**Rule SA**
- **SA-1 Second supply structure.** Skai plan, composition `{ windrunner 30, talon_archer 20 }`, four complete Roosts and one Windspire, one Roost under construction (P = 8, n = 1), cap 50, used 44, workers below target, common 400. H = 500; N = 4 x ceil(1 x 500 / 360) = 8, plus Windspire ceil(2 x 500 / 400) = 3, plus workers ceil(500 / 240) = 3: 14. Headroom 50 + 8 - 44 = 14 < 14 + 3. Expected one `Build` of `skai.roost`. Switch off: none (one is already under construction).
- **SA-2 Pending cap.** SA-1 with three Roosts under construction and `supply_ahead_max_pending` 3: none.
- **SA-3 No reservation.** SA-1 with common 100: none, and no reservation made by SA.
- **SA-4 Supply limit.** SA-1 with `SupplyCap + P` at `SupplyCapLimit`: none.

**Rules RS and RW** (raid common setup)
- **RS-1 Hit opens a raid.** Three P0 fledglings gathering field 6; a P1 Windrunner at (21.5, 12.5) stepped until it has hit one. Decide: `RaidHits` holds the depot with that hit's tick and the worker's position.
- **RS-2 Sighting alone does not.** As RS-1 but the Windrunner is placed and Decide runs before it fires (or with its weapon cooldown not elapsed): `RaidHits` empty, no field closed, no worker re-ordered. (Package F's failure mode.)
- **RS-3 Loss counts.** `OwnWorkerPositions` holds a fledgling ID no longer present, last at (20.5, 9.5): after Decide the depot's entry has this tick and that position.
- **RS-4 Expiry.** After RS-1, remove the Windrunner and run Decide at +9 s: the raid is active; at +11 s it is gone.
- **RW-1 Evacuation.** After RS-1: three `Gather` intents to field 1 (fields 6 and 7 are within 8 tiles of the hit; field 1 is the nearest open field below its cap). No intent names field 6 or 7.
- **RW-2 No replacement into the raid.** After RS-1, with fields 0 and 1 filled to their caps by own workers, HQ Rooted with an empty queue, common 500: no `Train` of a worker. Switch off: one `Train` of `skai.fledgling`.
- **RW-3 Nowhere open.** Raids also at the HQ's fields (active raid positions within 8 tiles of fields 0 and 1, and of Home): the three workers get no order. With Home farther than 8 tiles from every raid position and no open field: `Move` to `Standable(Home())`.
- **RW-4 Reopening.** After RS-4's expiry, an idle fledgling at the depot gets `Gather` to field 6.
- **RW-5 Command cap.** Fifteen workers on closed fields and the cap at 12: 12 orders this think, the other 3 next think.

**Rule RA** (raid common setup)
- **RA-1 Newest raid.** Two P1 Windrunners at (20.5, 9.5) that have hit a P0 worker (active raid at the depot) and one P1 Windrunner that has hit nothing, on a standable tile within `defend_radius_tiles` of the HQ and more than `defend_radius_tiles` from the raid position (the builder picks the tile and states it in the test): the defenders' `AttackMove` goal is `Standable` of the mean of the two raiders. Switch off: the goal is `Standable` of the mean of all three.
- **RA-2 Hold.** One P0 `skai.talon_archer` at (14.5, 8.5) against the two raiders: compute the predictor advantage of (talon) against (2 Windrunners) in the test and assert it is below `defend_min_advantage`; Decide: `Move(talon, Standable(Home()))`, no `AttackMove` or `AttackUnit` for it; `DefendGroup` holds it. Switch off: `AttackMove` to the threat centre (or `AttackUnit` from focus, as package F-5 found).
- **RA-3 HQ joins.** As RA-2 with the raiders at (12.5, 8.5), inside the HQ's weapon range plus 2: compute the advantage of (HQ plus talon) in the test and assert the branch it selects (attack-move or hold).
- **RA-4 Validation.** A profile with `use_raid_response` true and `use_measured_defence` false fails to load, naming the key.

**Determinism and off path**
- **D-1** PC-1, SA-1, RW-1 and RA-2 run twice from identical inputs: identical intents and memory.
- **OFF-1 Pins.** Every pin of section 0 holds; no test is re-pinned.
- **OFF-2 Old behaviour and draws.** For `aggressive_s5_m` and a copy with the four switches explicitly false and every other new key at its candidate value: identical think intents over a fixed traced game, and identical sequences of generator and `AiVariety` draws.

**Harness (PD-M)**
- **H-1** A hand-built event stream for two slots: a worker trained at 100 s and killed by an enemy at 130 s, one trained at 100 s and killed at 200 s, one start worker killed at 50 s: `worker_losses` 3, `worker_fed_losses` 1.
- **H-2** Damage events on a slot-0 worker beside its depot in seconds 120 to 124, a slot-0 armed unit arriving within 12 tiles at 123 s, slot-0 army supply above the raiders': `raid_seconds` 5, `raid_unanswered_edge_seconds` 3 (120, 121, 122).
- **H-3** At 180 s one complete Tidepool and one complete Shell Forge, rare 0, no research: `producers` 2, `capacity` 333 + 375 = 708 (the Tidepool's best payable unit is the Pummeler; the Shell Forge's the Shellback).
- **H-4** Observation only: the same seeds give the same `state_hash` and `result` per game with and without the new fields.

## 9. Measurement (fixed before any run; nothing is tuned after a result)

Map `prototype_1v1` only, three matchups, 72 games each, the seeds and command of `reports/ai_m/README.md` (`--balance ... --maps prototype_1v1 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4`), Release build of the branch, output under `rts/docs/reports/ai_pd/`. Plus one `--trace` per matchup with the profile in both seats.

**Run B0 (baseline), right after PD-M merges and before any rule package:** `--profile aggressive_s5_m`. **Check M-0:** every game's `state_hash` and `result` equal those in `reports/ai_m/balance/balance.jsonl` (the baseline is the ai_m games, now measured). If not, stop and report. B0's printed medians are the baseline values; the report records them before run C1 starts.

**Run C1 (candidate), after PD-1 to PD-4 merge:** `--profile aggressive_s5_pd`, all four switches on.

Cells: (Skai, cross), (Water, cross), (Skai, mirror), (Water, mirror); each value below is the median over the cell's slot-games (72, 72, 144, 144). Bars, each judged per cell, C1 against B0:

| Bar | Metric | Pass when |
| --- | --- | --- |
| PD-B1 Feeding the raid | `worker_fed_losses` | C1 <= floor(B0 / 2) when B0 >= 2; else C1 <= B0 |
| PD-B2 Worker losses | `worker_losses` | C1 <= floor(0.6 x B0) when B0 >= 6; else C1 <= B0 + 2 |
| PD-B3 Raids left unanswered | `raid_unanswered_edge_seconds` | C1 <= floor(B0 / 2) when B0 >= 10; else C1 <= B0 + 5 |
| PD-B4 Idle bank | `bank_mean` | C1 <= floor(0.6 x B0) when B0 >= 300; else C1 <= B0 + 50 |
| PD-B5 Producers per income | `producers x 1000 / income_60` at 300 s | C1 >= B0 (no cell loses production capacity relative to income) |
| PD-B6 Economy guard (package F's failure) | `income` buckets 240 to 300 s summed (existing field) | C1 >= 0.9 x B0 |
| PD-B7 Clean commands | traces | no AI command is rejected with a (command kind, reason) pair that does not occur in the three B0 traces |

Baseline values already known (from `reports/ai_m`, the same games B0 replays): median common bank at 300 s Skai cross 652.5, Water cross 535, Skai mirror 192.5, Water mirror 695; median income 240 to 300 s Skai cross 1845, Water cross 1640, Skai mirror 1815, Water mirror 1767.5 (PD-B6's B0, final); median end 556 s (cross), 561 s (Skai mirror), 674 s (Water mirror). From the one traced cross game (informational, not a bar value): Water lost 28 workers and Skai 0 up to 455 s; mean bank 60 s to end Water 340, Skai 1305; producers at 180 / 300 / 450 s Water 3 / 4 / 5, Skai 4 / 7 / 12. Every other B0 value comes from run B0 and is recorded before C1.

Reported, not judged: the faction matrix and opening matrix (for information only; AI-versus-AI win rates are not read as balance and no win-rate target is set); `raid_seconds`; `army_supply` and `capacity / income` at 180, 300 and 450 s; from the traces, attacks started, stance changes and producers ordered by rule PC (structures ordered beyond the build order and the supply rule); `balance_use`, `balance_unspent`, `balance_predictor` and `balance_openings` as printed, against `reports/ai_m`; games reaching `max_game_seconds` and the median end tick.

A failing bar is reported with its value. Any change of a rule or a key value is a new revision of this spec with its bars restated before the next run.

## 10. Relation to P-006 packages E and S

- **From E (economy by calculation)** this spec takes two slices: the measured income (`DepositLog`, which E's worker-value calculation needs as its income input) and one case of E's worker rule ("a worker is worth building while the income it adds exceeds its cost": a field under an active raid adds none, so RW gives it cap 0). **Left for E:** the worker and depot schedule from slots, trip time and a horizon, the offline optimum check, and the replacement of AI-E's 80 percent trigger.
- **From S (one threat map)** this spec takes the raid-response purpose that S absorbed from package F (worker safety, hold when outmatched, the HQ on the defending side), rebuilt on hits and losses instead of sightings after F's measured failure. **Left for S:** the grid of own and remembered enemy strength, and every placement and target decision it was to serve (producer and tower placement, safe expansion, HQ moves, raid target choice), plus F.3 (a raid that kills workers earns its tower) and F.4 (depleted fields, unreachable sites).
- Rule PC is not in P-006; it is the production half of package B's "how income is divided" question, scoped to one ceiling. If package B is built later, PC's capacity test becomes the production queue's need.

## 11. Build packages (one builder session each; the `sim-reviewer` after each)

Each package: Sonnet `builder`; `dotnet build` with no warnings and `dotnet test` green including OFF-1 and OFF-2; adds its keys to every profile file (switch off) and to `aggressive_s5_pd` (switch on); updates the `AiBrain.cs` header (rule order), the `AiProfile.cs` "Differences" list, `rts/src/Ai/README.md`, and the headers of the files it touches.

| Package | Contents | Tests | Depends on |
| --- | --- | --- | --- |
| **PD-M Metrics** | Section 7 in `SummaryObserver`, `GameSummary`, `BalanceReport`, `ai_eval.json` settings; then run B0 and commit `reports/ai_pd/baseline/` with M-0's result | H-1 to H-4 | nothing |
| **PD-1 Production** | Section 2 helpers and `DepositLog`, rules RR and PC, `use_production_capacity` keys, creates `aggressive_s5_pd` | RR-1 to RR-3, PC-1 to PC-8, D-1 (PC), OFF-1, OFF-2 | PD-M merged |
| **PD-2 Supply** | Rule SA, `use_supply_ahead` keys | SA-1 to SA-4, D-1 (SA) | PD-1 |
| **PD-3 Raid workers** | Memory of section 2, rule RS, rule RW, `use_raid_workers` and the shared raid keys | RS-1 to RS-4, RW-1 to RW-5, D-1 (RW) | PD-1 |
| **PD-4 Raid response** | Rule RA, `use_raid_response`, the hold and HQ keys, the validation | RA-1 to RA-4, D-1 (RA) | PD-3 |
| **PD-5 Measurement** | Run C1 and the traces; `reports/ai_pd/README.md` in the `reports/ai_m` format with every bar's B0 and C1 values | none | PD-1 to PD-4 merged |

## Determinism notes for the sim-reviewer

- No Sim change. New state is in `AiMemory` (`DepositLog`: a list appended in event order within a tick and in tick order across ticks; `RaidHits` and `OwnWorkerPositions`: sorted dictionaries; `RaidScanTick`: an int), written only when the matching switch is on; none is hashed.
- `ReadEvents` gains one branch (own common deposits) placed first, guarded by the switch, ending in `continue`; no existing branch read that event type, so no other branch sees a different event.
- No new random draw anywhere; check that no new code path calls the generator or `AiVariety`.
- Every new loop walks own units, drop-offs or fields in ascending ID or index; `PickByDeficit` keeps its ordinal tie; distances are squared `Fix64`; rates and supply projections are integer arithmetic (`RateOf`, ceil by `(a + b - 1) / b`); the one fraction is a `Fix64` compare.
- Fog: deposits and hits are own-player events; threats are `VisibleEnemies`; nothing reads enemy stock, queues or hidden units. The harness metrics read the full state but are measurement only and never reach a controller.

## Rejected extras (one line each)

- A new attack rule for the massing stall: the stall follows from claims 1, 2 and 4 (section 0); a lower threshold would only feed a weaker army to Skai.
- Danger from sightings (package F's rule WS): measured to lose the water mirror 0 of 72 by emptying naturals for scouts.
- A static-defence rule change (F.3): left for S; rule SD already built Water's turret in the trace.
- Pulling workers to fight raiders: workers are the income and Water's Tender (range 0.3) cannot reach a range-4 raider.
- Per-producer-type capacity targets: one income-based ceiling plus the deficit choice already picks the producer of the most needed unit.
- Turning rule X's threat gate off alone: the free-producer test would still block (PC-4).

## Out of scope

- Any `rts/src/Sim` change; any unit, combat, economy, map or tuning number (P-005 freeze), including Water's range and speed.
- Maps other than `prototype_1v1` in the measurement.
- Switching any new switch on in a profile other than `aggressive_s5_pd` (the developer decides after section 9).
- The rest of P-006 E and S (section 10); package B; Unity presentation.

## Questions for the developer

- **Q-1. Raids are recognised from hits and worker losses only, not from enemies seen near a field.** A raider that sits beside the workers without firing yet closes nothing until its first hit. *Default applied: yes (package F's measured failure came from sightings).* Alternative: sightings of 2 or more enemy army supply also count.
- **Q-2. Workers with no open field go home.** When every field near them is closed, evacuated workers move to the HQ and idle there until the raid expires. *Default applied: yes.* Alternative: they keep mining (package F option A; it gave back the water raid result).
- **Q-3. The army holds at home when even all of it is predicted short.** An outlying base's workers are pulled (Q-2) rather than defended by a losing army. *Default applied: yes (package F.2).* Alternative: always send everyone (today's behaviour).
- **Q-4. Rule PC replaces rule X when its switch is on.** *Default applied: yes, one producer rule.* Alternative: run rule X as well, as a bank-based fallback.
- **Q-5. Supply ahead (SA) is in this job.** It mostly changes Skai (supply-capped from about 240 s with 1000 to 4000 common idle), so Skai's army grows too. *Default applied: yes, behind its own switch, so it can be measured or left off separately.* Alternative: drop SA and judge PD-B4 for Water cells only.
- **Q-6. Bars are judged per (faction, matchup) cell, with the same formula for every cell.** The mirror cells mostly act as non-regression checks because their baselines are lower. *Default applied: yes.* Alternative: judge only the cross-faction cells.
- **Q-7. The "unanswered raid" bar counts only seconds in which the defender had at least the raiders' army supply.** A defender with less army is not blamed for holding (Q-3). *Default applied: yes.* Alternative: count every unanswered raid second.

---

# R2. Revision 2 (23 Nov 2026): raid rules RS, RW and RA

A delta on the built code of packages PD-3 and PD-4 (`rts/src/Ai/AiBrainRaids.cs`, `AiBrainFight.cs` `MeasuredDefence`, `AiBrain.cs` `UpdateIntel` and the `Turn` constructor, `AiMemory.cs`, `SkirmishAiController.cs` `ReadEvents`) plus one reported-only harness metric. Rules RR, PC and SA, every key value, every bar and every unit, combat, economy or map number are unchanged. No new switch and no new profile key: every change runs only when `use_raid_workers` or `use_raid_response` is true (as section 5 and 6 code does today), so with both false every profile plays bit for bit as on `4d4e506` and no pin moves.

Sources added for this revision: `reports/ai_pd/README.md` (run C1), `reports/ai_pd/baseline/README.md` (run B0), `reports/ai_pd/balance/balance.jsonl` and `baseline/balance.jsonl` (per-slot records), the three C1 traces in `reports/ai_pd/`, the B0-candidate traces in `reports/ai_m2/`, and 24 extra traces made for this diagnosis (not committed; reproduce with the command in R2.0.4): games 0 to 11 of `skai_vs_water` at the run's base seed 20261121, once with `aggressive_s5_m` (B0) and once with `aggressive_s5_pd` (C1) in both seats. Each extra trace's seed equals the seed of the same game in `balance.jsonl` (checked for game 0: 13859732737029828365). Water is P1 in even games and P0 in odd games.

## R2.0 Diagnosis: why PD-B1, PD-B2, PD-B3 and PD-B6 failed

Values marked **(sample)** come from the 12 + 12 extra traces (30 s lines), not from the 72-game cells; values marked **(cell)** come from `balance.jsonl`; **inference** marks a cause that fits the data but is not printed by any trace or metric (the traces do not print raid state).

### R2.0.1 (a) Rule flaws: all five confirmed in the code, plus the mechanism of H3

| # | Flaw | Where (on `4d4e506`) | Effect |
| --- | --- | --- | --- |
| H1 | One raid position per drop-off, overwritten by each newer hit or loss | `AiBrainRaids.cs` lines 94 to 97; `NearRaid` lines 175 to 191 tests fields against that one position | A field raiders still stand on reopens when the newest hit is more than `raid_field_radius_tiles` (8) from it, typically when raiders chase a fleeing worker to the next field. |
| H2 | Expiry `raid_active_seconds` (10 s) after the last hit or loss, with no look at the raiders | lines 99 to 114 | Evacuation works against the raid: once no worker is left to hit, the raid ends 10 s later and the idle-worker loop of `Gather` sends workers back to raiders that never left. |
| H3 | A worker trained and killed between two thinks is never counted | `UpdateIntel` (`AiBrain.cs` lines 640 to 660) runs before `Raids` and removes every `RecentDamage` report whose unit is no longer alive; `OwnWorkerPositions` holds only workers seen at the last think | Revision 1's step 2 clause "or a unit no longer in the view" can never fire (its reports are already gone). A worker that is new since the last think and dies before the next leaves no hit and no loss. With `think_interval_seconds` 0.5 this is rare (a worker killed within 10 ticks of spawning, at the HQ), so its share of the losses is small; it is still a hole in a rule that exists to stop feeding the raid. |
| H4 | Rule RA answers only the newest raid | `MeasuredDefence` (`AiBrainFight.cs` lines 631 to 645) with `NewestRaid` (`AiBrainRaids.cs` lines 120 to 138) | (i) Two raids hit in turn swing the newest choice, and with it every defender's goal, between drop-offs from think to think. (ii) When the newest raid is predicted short the whole defence holds at home even if an older raid elsewhere could be won; that raid is unanswered (PD-B3). |
| H5 | `_commonFull` is computed in the `Turn` constructor (`AiBrain.cs` lines 1007 to 1017) through `CapOf`, which reads `IsClosed`, before `Raids` updates the raid memory | `Decide` order, `AiBrain.cs` lines 243 to 247 | With `rare_cap_yields_to_saturation` (true in the candidate) `RareCapped`, and so the rare fields' caps, use the raid state of the previous think for the whole think. |

Evidence that the raid rules did not work even before the overrun (sample): raid-phase Water worker losses per game (definition in R2.0.2) have median **14 in B0 and 14.5 in C1**. The rules moved losses later in the published C1 trace but did not reduce the raid-phase total. Fitting H1 and H2 (inference): C1 traces show idle spikes followed by losses on the next lines (game 2: idle 10 at 330 s, then 4 and 6 tenders lost at 360 and 390 s; game 7: idle 13 and 9 at 300 and 330 s, then 3 and 6 lost at 360 and 390 s), the pattern of workers evacuated, the raid expiring and the workers returning.

Fed losses (PD-B1, Water cross 6 and 6): rule RW stops training only for closed fields, and H1 and H2 reopen them while raiders remain, so replacements are trained and walk back (inference). Fed losses during the overrun (R2.0.2) also count.

### R2.0.2 (b) Losses the raid rules cannot fix: the base overrun by Skai's main army

Definition used here (trace lines, a heuristic): a Water loss is **overrun** when it falls in a 30 s line in which Skai's stance is `Attack` with at least 8 attack-moving units whose centre is within 30 tiles of Water's HQ, or in the line in which Water's HQ is gone; every other loss is **raid-phase**.

| Sample (12 games each) | Water worker losses | of which overrun | share |
| --- | --- | --- | --- |
| B0 (`aggressive_s5_m`) | 344 | 178 | 52% |
| C1 (`aggressive_s5_pd`) | 323 | 164 | 51% |
| Published C1 trace (`reports/ai_pd/trace_skai_vs_water.txt`) | 40 | 27 (480 to 539 s, Skai's third attack, 48 of 51 units attack-moving at 510 s) | 68% |

So **about half of Water's worker losses against Skai fall in the overrun (inference from the sample, heuristic cut)**. The trace counts run to the game end; the cell metric stops at 600 s, which cuts only the two B0 sample games longer than 600 s.

The overrun also came sooner in C1, which moves the PD-B6 window: Water-cross games end at a median of **556 s in B0 and 485 s in C1 (cell, 72 games)**; the HQ siege (first HQ damage to death) has a median of 40 s in B0 and 20 s in C1 (cell); Skai's first `Attack` stance comes at a median of **302 s in B0 and 230 s in C1 (sample)**. Both seats play the candidate, so Skai also gains rules PC, SA and RA. Skai's army at 300 s is the same (median 20.5 units in both, sample), so the earlier attack is not a larger Skai army. Inference: Water's rule RA hold keeps its army home, so Skai's raiders survive (B0 traces show Skai's army shrinking between 240 and 300 s in 2 of 12 games; in C1 traces it grows in all 12) and Skai's attack predictor sees a weaker defence sooner.

### R2.0.3 (c) The income drop PD-B6 (Water cross, 1645 to 1317.5)

Sample medians over the 240, 270 and 300 s lines (income summed over 240 to 300 s):

| | B0 | C1 | Reading |
| --- | --- | --- | --- |
| common income 240 to 300 s | 1545 | 1282.5 | -17%, the cell's drop (-20%) reproduced |
| workers alive | 19.0 | 19.3 | **not fewer workers** |
| tenders lost 180 to 300 s | 5 | 4 | **not lost workers** |
| tenders trained 180 to 300 s | 10 | 8 | fewer trained (closed fields lower `WantedWorkers`) but offset by fewer losses |
| idle workers per line | 0.7 | 2.0 | about 7% of the workforce idle: **evacuation idling** |
| common per gathering worker (per minute) | 86.5 | 80.8 | about 7% lower yield per worker; inference: evacuees walking to farther open fields and crowding them above their caps (the trace counts a walking worker as gathering) |
| rare income 240 to 300 s | 132.5 | 202.5 | about one worker more on rare; a rule RR and PC effect, not a raid effect |

So PD-B6 fell from **evacuation (idle workers and longer trips) and a small shift to rare, not from fewer or lost workers** (inference for the split; the totals are measured). Revision 2 keeps closed fields closed longer (H1, H2), which can idle more workers; PD-B6 stays the guard for that.

### R2.0.4 Reproducing the sample

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --trace --map prototype_1v1 --matchup skai_vs_water --seed 20261121 --game <0..11> --profile-a <aggressive_s5_m|aggressive_s5_pd> --profile-b <same> --out <file>
```

## R2.1 Rule changes (delta)

Ground rules of section 0 apply unchanged (fog, commands, determinism, no new random draw, AI memory not hashed). Every new memory member below is written only when `use_raid_workers` or `use_raid_response` is true, except `DamageReport.IsArmy` (R2.1.3), which is written always and read only by rule RS.

### R2.1.1 Rule RS: fields stay closed for the whole raid (fixes H1)

New memory: `RaidClosedFields` (`SortedDictionary<EntityId, SortedSet<int>>`; key an own drop-off with an active raid, value the indices of the fields that raid has closed; field indices 0 to field count - 1).
1. In `Raids`, after the hits and losses are applied (section 5.1 step 3) and before expiry: for each `RaidHits` entry (ascending drop-off ID), add to its set every field index (ascending) whose centre is within `raid_field_radius_tiles` of the entry's position (squared `Fix64`, as `NearRaid`). Sets only grow while the raid is active.
2. When an entry leaves `RaidHits` (expiry or drop-off gone), its set is removed in the same loop.
3. `IsClosed(field)` becomes: `use_raid_workers` and some set in `RaidClosedFields` contains `field.Index`. `NearRaid(Home())` (evacuation's "home is safe" test, section 5.2 step 3) keeps its position test unchanged.

`RaidHits` keeps its type and meaning (newest hit tick and position per drop-off), so rule RA's ordering and tests RS-1 and RS-3 are unchanged.

### R2.1.2 Rule RS: visible raiders keep an existing raid alive (fixes H2)

New memory: `RaidSeenTick` (`SortedDictionary<EntityId, int>`; key an own drop-off with an active raid, value the last think tick at which a raider was seen in its area; ticks, 0 to now).
1. In `Raids`, after R2.1.1 step 1 and before expiry: for each `RaidHits` entry (ascending ID), when any `VisibleEnemies` unit with `IsArmy` is within `raid_field_radius_tiles` of the entry's position or of the centre of any field in its `RaidClosedFields` set (squared `Fix64`), set `RaidSeenTick[d] = now`.
2. Expiry (section 5.1 step 4) becomes: remove the entry when the drop-off is no longer an own unit, or when both its hit tick and its `RaidSeenTick` (when present) are below `now - SecondsToTicks(raid_active_seconds)`. Remove its `RaidSeenTick` and `RaidClosedFields` entries with it.
3. **A sighting never creates a raid** and never changes a `RaidHits` tick or position; only an entry made by a hit or a loss can be kept alive. Package F's failure (raids from sightings) stays excluded, and an expired raid is not revived by a later sighting.

### R2.1.3 Rule RS: hits on units that died between thinks (fixes H3)

1. `DamageReport` gains `IsArmy` (boolean): `SkirmishAiController.ReadEvents` sets it each time it writes a report, with the predicate of `AiUnitView.IsArmy` applied to the target's definition (footprint 0, no `Hq`, cannot gather). Nothing reads it when both raid switches are off.
2. New memory: `RaidCarriedHits` (`List<(int Tick, FixVec2 Position)>`, ticks and tiles). In `UpdateIntel`'s `RecentDamage` prune, when a raid switch is on, each report removed because its unit is not alive (not for age), with `Tick > RaidScanTick` and `IsArmy` false, is appended in the prune loop's order (ascending unit ID).
3. `Raids` step 2: after the `RecentDamage` hits, append the carried hits in list order; then clear the list. Section 5.1 step 3 applies to them unchanged (nearest drop-off within `defend_radius_tiles`, a later-or-equal tick replaces). Workers seen at the last think and now gone are still losses as before; a hit carried for the same worker is harmless (same drop-off, the loss's tick is later and wins).
4. Known limit (stated in the code as a shortcut comment): a unit killed by its first hit leaves no report (`ReadEvents` drops hits on units already removed), so a worker new since the last think and killed by a single hit is still uncounted.

### R2.1.4 Rule RA: one answer per raid, sticky, and the first raid that can be won (fixes H4)

New memory: `RaidAnswered` (`EntityId`, the drop-off of the raid the defence answered at the last think; `EntityId.None` at start). Cleared to `None` in `Raids` when its entry is removed.

Replaces section 6 step 1 when an active raid exists (no active raid: unchanged):
1. **Order.** The raid `RaidAnswered` names, if still active, comes first; then the other active raids by hit tick descending, ties to the lower drop-off ID.
2. **First winnable raid.** For each raid in that order whose `ThreatsNear(position)` is not empty: centre = mean of those threats; candidates sorted and truncated as today against this centre; fixed side = static defences within `engagement_radius_tiles` of this centre plus the HQ when `HqCovers(centre)`; then the existing candidate loop. The first raid that reaches `enough` is answered: its threats, centre and chosen units go on through the existing recall and order code; `RaidAnswered` = its drop-off.
3. **None winnable.** When some raid has threats and none reaches `enough`: hold (section 6 step 3) with every candidate of the first such raid in the order; `RaidAnswered` = that raid.
4. **No raid has visible threats.** The existing blind branch (`defend_blind_supply`) toward the position of the first raid in the order; `RaidAnswered` = that raid.
5. Predictor calls per think: at most (active raids) x (`defend_max_candidates` + 1). No random draw; every tie is stated above.

Threats in the base that are near no active raid stay for later thinks, as in revision 1.

### R2.1.5 A fresh common-full test after the raid scan (fixes H5)

Move the loop that sets `_commonFull` (`AiBrain.cs` lines 1009 to 1016) into a private method; the constructor calls it as today, and `Raids` calls it again at its end when `use_raid_workers` and `rare_cap_yields_to_saturation` are both true. `_assignedAtStart` is unchanged (it reads only the view). Rules before `Raids` (`Hq`, `Cycle`) do not read `CapOf`, `RareCapped` or `IsClosed` on `4d4e506`; the builder confirms this and reports any read found.

### R2.1.6 Unchanged

Rule RW's evacuation, training and field-choice parts (section 5.2) apply as written, now through the new `IsClosed`. Rule RA steps 2 to 4 (HQ joins, hold, recall) are unchanged. Keys, validation and profiles: unchanged.

## R2.2 Harness: the alternative reading, reported only (package PD-M2, `rts/tools` only)

Needed only so the measurement can print both readings of the question R2-Q1; no bar reads it.

New per-slot keys in `balance.jsonl` (appended):

| JSON key | Meaning | Unit |
| --- | --- | --- |
| `overrun_second` | first whole second up to T at which the supply of enemy army units (mobile, not worker, not HQ) within `raid_radius_tiles` of the slot's HQ or of any own drop-off is at least `overrun_supply`; null when never. Once reached, every later second counts as overrun (no reset). The harness sees everything; measurement only. | seconds |
| `worker_losses_pre_overrun` | `worker_losses` with kill tick before `overrun_second` (all of them when null) | workers |
| `worker_fed_losses_pre_overrun` | the same cut applied to `worker_fed_losses` | workers |

New setting in `rts/data/scripts/ai_eval/ai_eval.json` with the house comment line (`// Overrun cut of the reported-only alternative reading: enemy army supply near the slot's base that marks the main attack, not a raid. Unit: supply. Range: 1 to 200. Source: rts/docs/specs/ai_production_defence.md R2.2; placeholder.`): `overrun_supply` **20** (placeholder: the traced Skai raids are 4 to 8 one-supply Windrunners, the main attacks 30 units and more). `BalanceReport` prints the cell medians of the three keys beside the existing table.

## R2.3 Tests

In `rts/tests/Sim.Tests/Ai/AiRaidDefenceTests.cs`, raid common setup of section 8 (greybox fields 0 at (4, 10), 1 at (10, 4), 6 at (21, 10), 7 at (21, 4); depot at (18.5, 7.5)), `Hand()` profiles, hits fed as `ReadEvents` does unless the test says otherwise. Each test names the revision-1 result, so the reviewer can confirm it fails on `4d4e506` and passes after PD-R2.

**H1**
- **R2-H1-1 Closed field stays closed while the raid moves.** After RS-1 (raid at the depot from a hit on field 6; fields 6 and 7 closed), write a `RecentDamage` report (tick now, `IsArmy` false) for a live P0 fledgling with position (14.5, 15.5) (nearer the depot than the HQ, within 12 tiles of the depot, more than 8 from the centres of fields 6 and 7; the builder confirms this against `greybox_1v1.json` and lists any field within 8 tiles of it, which joins the set). Decide. Expected: `RaidHits[depot]` position is (14.5, 15.5); `RaidClosedFields[depot]` holds 6 and 7; `AiBrain.Probe(...).ServedCaps` shows cap 0 for fields 6 and 7; no `Gather` intent names 6 or 7. Revision 1: fields 6 and 7 reopen (cap above 0).
- **R2-H1-2 Sets go with the raid.** After R2-H1-1, raiders gone, step past `raid_active_seconds` and Decide: `RaidClosedFields` empty; field 6's cap above 0.

**H2**
- **R2-H2-1 A visible raider keeps the raid.** After RS-1 and RW-1, step until the three workers have left field 6 (assert no new damage report after the RS-1 hit; the Windrunner at (21.5, 12.5) is 5.8 tiles from the depot, out of its range 4) and until 11 s after the hit tick. Decide: the raid is still active, its `RaidHits` tick is the RS-1 hit tick, `RaidSeenTick[depot]` is this think's tick, field 6 is closed. Revision 1: raid gone (RS-4). Then move the raider to (60, 60) and step: at the first think 11 s after the last think that saw it, the raid is gone and field 6 reopens.
- **R2-H2-2 No revival by sighting.** After RS-4's expiry, spawn a P1 Windrunner at (21.5, 12.5) and Decide before it fires: `RaidHits`, `RaidSeenTick` and `RaidClosedFields` empty; no worker re-ordered. RS-2 must also still pass unchanged.

**H3**
- **R2-H3-1 Hit on a worker that died between thinks.** `RecentDamage` holds a report for an ID not alive (9998), `IsArmy` false, tick now - 2, position (20.5, 9.5); `OwnWorkerPositions` does not hold 9998. Decide: `RaidHits[depot]` = (now - 2, (20.5, 9.5)); `RaidCarriedHits` empty after the think. Same with `IsArmy` true: `RaidHits` empty. Revision 1: `RaidHits` empty in both.
- **R2-H3-2 ReadEvents marks army hits.** A real simulation with a P1 Windrunner hitting a P0 `skai.talon_archer` and, later, a P0 fledgling, read by a `SkirmishAiController` for P0: the archer's report has `IsArmy` true, the fledgling's false.
- **R2-H3-3 Switches off.** R2-H3-1 with both raid switches false: `RaidCarriedHits` stays empty and the report is pruned as today.

**H4** (Talon Archers of P0 and Windrunners of P1; the test computes each predictor advantage and asserts which side of `defend_min_advantage` it is on before asserting the orders)
- **R2-H4-1 Sticky answer.** Raid A at the depot (hit on a field-6 worker, one Windrunner near it) and raid B at the HQ (hit on a field-0 worker 1 s later, one Windrunner near it); two P0 Talon Archers between them, enough for either. Think 1: answered B (newest), defenders' `AttackMove` goal is `Standable` of B's raider. Then a new hit at A (A becomes newest). Think 2: `RaidAnswered` is still B and the goal is unchanged (no new `AttackMove`). Revision 1: think 2 sends the defenders to A.
- **R2-H4-2 A winnable older raid is answered.** Newest raid at the HQ with three Windrunners (two archers predicted short), older raid at the depot with one Windrunner (predicted enough). Expected: `AttackMove` toward the depot raider; no `Move` home; `RaidAnswered` = depot. Revision 1: hold (`Move` to `Standable(Home())`).
- **R2-H4-3 All short holds.** Both raids with three Windrunners: hold as RA-2, every candidate in `DefendGroup`; `RaidAnswered` = the newest raid.
- **R2-H4-4 Cleared on expiry.** After R2-H4-2, remove the depot raider and step past expiry: `RaidAnswered` is `EntityId.None` or names a still-active raid.
- RA-1 to RA-4 must pass unchanged (each has one raid).

**H5**
- **R2-H5-1 Common-full read after the scan.** `rare_cap_yields_to_saturation` true, `RareCapActive` true, fields 0 and 1 filled to their caps, field 6 below its cap (so the constructor finds common not full), a hit this think that closes fields 6 and 7. After `Decide`, the common-full value (exposed as an `internal` read for tests, as PD-1's reservation probe) is true and a served rare field's cap is above 0 if the map has one in reach (the builder checks `greybox_1v1.json`; with no rare field in reach assert the common-full value only). Revision 1: false for this think.

**Harness (PD-M2)**
- **R2-M-1** Hand-built event stream: enemy army supply near the slot's HQ reaches 20 at 300 s; worker kills at 200 s and 350 s, the second worker trained at 320 s: `overrun_second` 300, `worker_losses` 2, `worker_losses_pre_overrun` 1, `worker_fed_losses` 1, `worker_fed_losses_pre_overrun` 0.
- **R2-M-2** Observation only: same `state_hash` and `result` per game with and without the new keys (as H-4).

**Determinism and off path**
- **R2-D-1** R2-H1-1, R2-H2-1 and R2-H4-2 run twice from identical inputs: identical intents and memory (including the new members).
- **OFF-1 and OFF-2** of section 8 hold unchanged; with both raid switches false the new memory members stay empty and `RaidAnswered` stays `None` over the traced game of OFF-2.
- Every existing test in `AiRaidDefenceTests.cs` passes unchanged; if one cannot, stop and report rather than edit it.

## R2.4 Measurement (fixed before any run)

1. **Run B0b** (after PD-M2 merges, before any rule change): B0's command with `--profile aggressive_s5_m`, output `reports/ai_pd/baseline_r2/`. Check: every game's `state_hash` and `result` equal `reports/ai_pd/baseline/balance.jsonl`; if not, stop and report. It adds only the alternative-reading baselines; every bar's B0 stays the value recorded in `reports/ai_pd/baseline/README.md`.
2. **Run C2** (after package PD-R2 merges): C1's command and seeds, `--profile aggressive_s5_pd`, output `reports/ai_pd/r2/`, plus the three traces.
3. **Bars:** PD-B1 to PD-B7 exactly as section 9, C2 against B0, per cell. Not restated, not changed.
4. **Reported, not judged:** everything section 9 reports; the R2-Q1 alternative reading, that is PD-B1's and PD-B2's formulas applied to `worker_fed_losses_pre_overrun` and `worker_losses_pre_overrun` (C2 against B0b), printed as "alternative reading, not a bar"; Water-cross median game end and HQ siege length; and from the three traces, idle workers per line at 210 to 330 s and the first Skai `Attack` stance.

What the report will print under the two readings of PD-B2 (Water cross), so the developer can compare: as written, C2 against B0 31 (pass at 18 or below); alternative, C2's pre-overrun losses against B0b's pre-overrun value (not yet measured; the sample's raid-phase median of 14 suggests a limit near 8). Both readings are printed for every cell of PD-B1 and PD-B2; only the as-written one is judged unless the developer answers R2-Q1 otherwise before run C2.

## R2.5 Build packages

| Package | Contents | Tests | Depends on |
| --- | --- | --- | --- |
| **PD-M2 Overrun cut** | R2.2 in `SummaryObserver`, `GameSummary`, `BalanceReport`, `ai_eval.json`; then run B0b | R2-M-1, R2-M-2 | nothing |
| **PD-R2 Raid rules** | R2.1.1 to R2.1.5 in `AiBrainRaids.cs`, `AiBrainFight.cs`, `AiBrain.cs`, `AiMemory.cs`, `SkirmishAiController.cs`; headers of every touched file, the `AiBrain.cs` header rule text for RS, RW and RA, `rts/src/Ai/README.md` | R2-H1-1 to R2-H5-1, R2-D-1, OFF-1, OFF-2 | nothing (PD-M2 before the run only) |
| **PD-R2M Measurement** | R2.4 steps 2 to 4; `reports/ai_pd/r2/README.md` in the format of `reports/ai_pd/README.md` | none | PD-M2, B0b, PD-R2 |

Sonnet `builder` for each; `sim-reviewer` after PD-R2 (determinism notes below).

### Determinism notes for the sim-reviewer (revision 2)

- New AI memory: `RaidClosedFields` and `RaidSeenTick` (sorted dictionaries keyed by drop-off ID; sets of field indices), `RaidCarriedHits` (a list filled in the ascending-ID order of the `UpdateIntel` prune and emptied each think), `RaidAnswered` (one ID), `DamageReport.IsArmy` (a bool). None is hashed; all but `IsArmy` are written only with a raid switch on, and `IsArmy` is never read with both off.
- Every loop walks drop-offs, units or fields in ascending ID or index; distances are squared `Fix64`; the raid order has a total tie-break (answered first, then hit tick descending, then drop-off ID ascending).
- No new random draw; no new command, intent kind or event type. Fog: sightings read `VisibleEnemies` only; the carried hits are own-unit damage events already read by `ReadEvents`.

## R2.6 Rejected extras (one line each)

- Starting a raid from sightings: package F's measured failure; sightings only keep a raid that hits or losses started.
- A cap on how long sightings can keep a raid (a new key): no evidence yet that raiders park for long; see R2-Q2.
- Reading `UnitKilledEvent` for worker losses: the event carries no position or definition, and the unit is gone by the time `ReadEvents` runs.
- Changing the hold rule's threshold or the evacuation targets: not shown to be a flaw; the evacuation's long walks (R2.0.3) are left for package E.
- Any rule for the overrun itself (army size, attack timing): a lost fight, not a raid; outside this spec.

## R2.7 Questions for the developer (revision 2)

- **R2-Q1. Do PD-B1 and PD-B2 count worker losses after the main army has reached the base?** About half of Water's losses against Skai come in the overrun (R2.0.2), which raid rules cannot prevent; and C1's earlier overrun moved the counts for reasons outside the raid rules. Even read the other way, revision 1 failed: its raid-phase losses did not fall (median 14 against 14.5, sample). *Default applied: the bars are judged as written (all losses up to 600 s); the pre-overrun reading is printed beside them, not judged.* Alternative: judge PD-B1 and PD-B2 on the pre-overrun keys (with B0b as baseline), decided before run C2.
- **R2-Q2. May visible raiders keep a raid alive with no time limit?** A raid started by a real hit stays active while any enemy army unit is seen within `raid_field_radius_tiles` of its area, so an army parked beside a natural keeps it closed. *Default applied: no limit (no new key).* Alternative: a new key capping the sighting extension at N seconds after the last hit.
- **R2-Q3. Should the defence stay on the raid it answered last think while it can still win there?** *Default applied: yes (stops the swing of H4).* Alternative: always the newest winnable raid.
- **R2-Q4. Should hits on own army units that died between thinks open raids?** Revision 1's text said any unit no longer in the view counts; the built code never did (H3). *Default applied: no, only non-army units (workers, structures, HQ), so an army fight near a drop-off does not close its fields.* Alternative: all units, as revision 1 was written.
- **R2-Q5. PD-B6 and the cross bars are measured with the candidate in both seats**, so Skai's gains from rules PC, SA and RA act on Water's numbers (C1's earlier Skai attack, R2.0.2). *Default applied: judged as written.* Alternative: a reported-only extra run with Skai on `aggressive_s5_m` and Water on `aggressive_s5_pd` to separate the effect.

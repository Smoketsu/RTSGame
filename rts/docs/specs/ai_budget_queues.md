<!-- rts/docs/specs/ai_budget_queues.md: build spec for P-006 package B (budget queues, the 0 A.D. Petra method): the skirmish AI divides its bank between six named spending queues with per-resource weights instead of each rule checking its own cap and the single reservation rule R. Behind one profile switch (off in every existing profile), candidate profile aggressive_s5_b and ablation abl_s5b_no_b, two small harness checks, behaviour bars and the pool gate stated before any run. No change under rts/src/Sim, no unit or combat number changes. Part of rts/docs/specs (index: README.md); read with proposals/P-006-ai-built-for-balancing.md and specs/balance_measurement.md. -->
# Spec: Budget queues (P-006 package B)

## 0. Goal, sources, ground rules

**Goal.** In 216 self-play games `aggressive_s5` banks its rare resource (median Water 220, 475, 1020 at 300, 600, 900 s; Skai 205, 285) while common is spent (at most 385) (`reports/balance_first/README.md`). AI-P's threat-defence rule, which had caps per base and a value share but no budget, put 46 percent of army value into towers and lost (0.370, `reports/ai_p/README.md`). No part of the AI decides how income is divided. This package adds that part: named queues, each with a weight per resource, accumulating money for the next item and buying it when the account covers it (Petra's `queueManager.js` with `config.priorities`, P-006 section 2). What to buy stays with the existing rules; this package decides only who may spend which money.

**Not UAlbertaBot's goal search.** The AI's goal is already given as data: the composition (or counter-mix) for the army and the build order for structures. A search for the fastest order to a goal belongs to package E (economy by calculation), not here.

**Sources read.** `CLAUDE.md`; `docs/STATUS.md`; `proposals/P-006-ai-built-for-balancing.md`; `specs/balance_measurement.md` (M.3, M.4, M.5, M.6); `reports/balance_first/` (README and `balance.jsonl` summaries), `reports/ai_p`, `reports/ai_e`, `reports/sat_m`, `reports/pool_first`; `src/Ai/AiBrain.cs` (header, `Turn` ledger, rules 3, 3b, 3c, 4, 4b, 4c, 5, 5b), `AiMemory.cs`, `AiProfile.cs`; `data/ai/aggressive_s5.json`; `data/tuning/economy.json`; unit files under `data/units`; `tools/BalanceHarness/GameSummary.cs`, `BehaviourChecks.cs`, `data/scripts/ai_eval/behaviour_checks.json`, `pool.json`.

**Ground rules.**
- No change under `rts/src/Sim`. No unit, combat or tuning number changes (frozen, P-005). No existing AI key changes value in any existing profile.
- AI code only: integers and `Fix64`, no floating point; arrays indexed by the fixed queue order of B.1; ties as stated. State lives in `AiMemory` (never hashed; a pure function of the public state and the AI's own generator, as now).
- With `use_budget_queues` false every code path is today's, bit for bit (test BQ-1).
- No starting value below is chosen from a ladder result, and none is changed after a measurement. A value that the reasoning does not pin is marked placeholder.

## B.1 The model

### B.1.1 Queues and what each holds

Six queues in this fixed order (index 0 to 5; the order is the tie-break everywhere):

| # | Queue ID | Rules whose purchases it pays (existing rule names of the `AiBrain` header) | Items |
| --- | --- | --- | --- |
| 0 | `economy` | 3a supply building, then 4b workers | supply structure, worker |
| 1 | `expansion` | 4 (rule E) depots | drop-off structure |
| 2 | `production` | 3b build-order steps, then 5b (rule X) extra producers | producers and any structure in a build order (Storm Spire in `skai_air` included) |
| 3 | `army` | 4c scout training, then 5 (rule P) units | army units |
| 4 | `tech` | 3b' rule T, then rule T2 | producer of a missing mix unit; a missing `requires` building |
| 5 | `defence` | 3c (rule SD) steps 2, 3 and SD-4 | static defences |

Expansion and production buildings are their own queues: a depot is income with its own trigger (rule E) and a different urgency from producers; producers compete with army units for the same common and the build order needs its own claim during the opening (B.1.4). Walls and gates have no queue (the AI does not build them; package A).

### B.1.2 One think

The rule order of `AiBrain.Decide` is unchanged. Two budget steps are added: **Open** after `Gather` (before `Structures`, which is the first rule that buys) and **Close** after `ExtraProducer` (before `Army`, which buys nothing).

Per queue q and resource r (common, rare) `AiMemory` holds: account `A[q,r]` and want `N[q,r]` (both resource units, 0 upward, carried from the last Close), cumulative spending `S[q,r]` (resource units, since match start), plus `RareSurplusActive` (boolean).

**Open.**
1. **No income.** When the Turn's `_income` is false (no own worker is earning; rule R's test), set every `A` and `N` to 0 and spend this think exactly as today without income (the bank buys whatever it can, in rule order; `Produce` skips unaffordable units). Skip steps 2 to 5. This keeps rule R's no-deadlock property.
2. **Clamp.** Bank `B[r]` = the view's stock. If `sum_q A[q,r] > B[r]` (only possible after an unexpected loss of money), take the excess from accounts in reverse queue order (defence first) until the sum equals `B[r]`.
3. **Effective weights** `w[q,r]` = the profile weight (B.3) times: `budget_opening_production_factor` for `production` while the drawn opening is before its handover (`AiVariety.InOpening`; no opening: never); `budget_threat_defence_factor` for `defence` while `UnderThreat`. While own workers alive (not queued) are fewer than `budget_worker_floor`, `army` and `defence` are **paused**: their accounts are released into free money, they receive nothing and buy nothing this think.
4. **Distribute** free money `F[r] = B[r] - sum_q A[q,r]`, separately per resource. Needy = unpaused queues with `N[q,r] > A[q,r]`. Up to 6 passes: `W` = sum of `w[q,r]` over needy queues with `w > 0`; stop if `W = 0`; with `base = F[r]` at the start of the pass, in queue order each such queue gets `g = min(N - A, floor(base x w / W))` (64-bit integer); subtract from `F`; drop queues now satisfied; stop when a pass gives nothing. **Leftover:** what remains goes, in queue order, to needy queues of any weight (0 included), each up to its need. Money no queue needs stays free.
5. **Rare surplus.** Free rare after step 4 at or above `budget_rare_surplus_on` sets `RareSurplusActive`; below `budget_rare_surplus_off` clears it (hysteresis; `budget_rare_surplus_on` 0 means always clear). While active, the composition the Turn uses (`_composition`, which rules P, X, T and T2 read) is a copy in which every unit with `CostRare > 0` has its weight multiplied by `budget_rare_surplus_weight_factor`. The Turn computes `_composition` in its constructor, so step 5 runs at the end of the constructor from the memory's accounts as closed last think (free rare = view rare minus their sum); step 4's result is used from the next think on. Stated so the builder does not reorder: the surplus decision lags one think (0.5 s).

**Buying.** A rule that would buy an item for queue q (each call site passes its queue):
- **Can pay** when, for each resource the item costs, `A[q,r] >= cost` (a resource it does not cost is not checked, as `CanPay` does today). Supply is checked first, as today.
- **Pays**: the order is emitted (`Emit` true), then `A[q,r] -= cost`, the Turn's bank ledger `_common`, `_rare` falls as today, and `S[q,r] += cost`.
- **Money short**: the first item of queue q that fails only on money this think is its **head**: record `N[q] = cost` for this think and buy nothing more for q this think (later items of q wait, as rule P's "cheaper units never jump ahead"). Any other failure (supply, no site, no builder, retry timer, requirements, `max_commands_per_think`) records no want.
- **Reachable** replaces "stock minus reservations" in the two places that read it without buying: rule P's candidate filter on rare (`CostRare <= A[army,rare] + F[rare]`, so a rare unit can become the head and draw rare) and rule X's bank test (`F[common] >= extra_producer_bank_common`, F after this think's purchases).
- **Defence ceiling**: a `defence` item is refused (as a non-money failure: no want) when `budget_defence_max_spend_share` is above 0 and `(S[defence] + c) x 10000 > share_pt x (S_total + c)`, where `c` = the item's common + rare cost, `S[...]` is summed common + rare (rare counted once, as `rare_weight` 1 in `behaviour_checks.json`) and `S_total` is over all queues. This bounds the defence share of spending by construction.

**Close.** For each queue: no want this think (bought its head, had none, or was paused): `A[q,r] = 0` for both resources (released to free money). With a want: `A[q,r] = min(A[q,r], N[q,r])`. Store this think's wants as `N`.

**The unspent-resource failure, answered.** A queue with nothing to buy holds nothing after Close, and no account ever exceeds its head item, so money never piles up inside a queue: everything above the six heads is free and goes to the first queue that wants it. Rare can still pile up when no queue's head costs rare; that is the case B.1.2 step 5 addresses, by making rare-cost units weigh more in the army's pick while the rare bank is idle, until it is spent down.

### B.1.3 Shares and game state

- **Opening** (before the drawn opening's `handover_seconds`): `production` weight times `budget_opening_production_factor`, so a due build-order step keeps the claim rule R gave it over army units. The openings keep their build orders, compositions and timing keys unchanged; they do not carry budget weights in this package (Q-5).
- **Under threat** (`UnderThreat`, the existing definition): `defence` weight times `budget_threat_defence_factor`; the ceiling still holds.
- **Saturated economy**: no factor is needed. When workers reach rule W's target, `economy` has no head, holds nothing and drops out of the split; the same holds for `expansion` between depots and `tech` when no mix unit lacks a producer.
- **Workers lost below the floor**: `army` and `defence` pause (P-006 B: "army and defence queues pause while workers are below a floor").

### B.1.4 What the switch replaces, and what stays

| Today | With `use_budget_queues` true |
| --- | --- |
| Rule R reservations (`_reserveCommon`, `_reserveRare`; one shared hold that blocked every later rule) | Per-queue accounts and wants; a head blocks only its own queue. Rule R's no-income branch is kept (B.1.2 Open step 1). |
| Workers ignore reservations | Workers buy from `economy`, whose high weight (B.3) keeps them first in practice. |
| `extra_producer_bank_common` read against bank minus reservations | Read against free common `F` |
| `static_defence_max_value_share` (C2.4 step 2 value check) | Not read; `budget_defence_max_spend_share` covers every defence purchase (steps 2, 3, SD-4) |
| Static defence per base (`static_defence_max_per_base`, the Sim's cap), one under construction | Unchanged (placement rules, not budget) |
| Rare soft cap C2.3 and `rare_cap_yields_to_saturation` (which workers gather rare) | Unchanged; a gather rule, left for package E (Q-4) |
| Rule RS (rare sites first), rule E's gates, `max_live_depots`, `max_producers`, rule W's worker target | Unchanged (they decide what to buy, not with whose money) |

## B.2 Profiles

- **Switch** `use_budget_queues` (boolean): false in every existing profile file under `rts/data/ai` (21 files). Every file carries every key of B.3 (the profile parser requires all keys, as for `harass_speed_ratio`); with the switch false they are read, range-checked and ignored. The existing files get the B.3 starting values for the other keys.
- **Candidate** `rts/data/ai/aggressive_s5_b.json`: a copy of `aggressive_s5.json` with `id` changed, `use_budget_queues` true and the B.3 values. `use_forward_producers` and `use_threat_defence` stay false (B.6).
- **Ablation** `rts/data/ai/abl_s5b_no_b.json`: the candidate with `use_budget_queues` false. It must play bit-identically to `aggressive_s5` (test BQ-1); its purpose is a named off-copy for reports and future packages built on the candidate.

## B.3 Data keys (every profile file under `rts/data/ai`)

Each value gets the standard comment line in the files (`// what. Unit. Range. Source: specs/ai_budget_queues.md B.3; placeholder` where marked).

| Key | Controls | Unit | Range | Starting value | Reasoning |
| --- | --- | --- | --- | --- | --- |
| `use_budget_queues` | switch for this package | boolean | true or false | false (candidate true) | Existing profiles must play as before. |
| `budget_common_weights` | weight of each queue in the split of free common; object with exactly the six queue IDs | weight (relative) | 0 to 1000 each, sum above 0 | economy 120, expansion 120, production 10, army 60, tech 4, defence 6 | `army` 60 is the reference (Petra `citizenSoldier` 60). `production` 10 + `tech` 4 = 14 is about 0.21 x 60: `aggressive_s5` spent 0.148 (Skai) and 0.128 (Water) of its common on structures against 0.669 and 0.687 on army (median slot-game shares in `balance_first/balance.jsonl`; a spending measurement, not a result). `economy` 120: one worker per 12 s train time needs 125 common per 30 s; median common income in the 90 to 150 s buckets is about 390 per 30 s, so economy needs at least 0.32 of contested money; against opening production (10 x 12 = 120) and army (60) it gets 120 / 300 = 0.40; it holds nothing once workers are saturated, so a high weight costs nothing later. `expansion` 120: a depot (100) is income like two workers, and AI-E measured 740 to 990 common per 30 s at 180 to 300 s from 2 to 3 depots against 662 from one; equal to economy so neither delays the other by design. `defence` 6: contested only with the army it gets 6 / 66 = 0.09, under the 0.15 behaviour bar and about twice `aggressive_s5`'s measured 0.03 share, so rule SD as built is not starved. Placeholder beyond those anchors. |
| `budget_rare_weights` | the same for free rare | weight (relative) | 0 to 1000 each, sum above 0 | economy 0, expansion 0, production 30, army 60, tech 30, defence 0 | Only army units, Storm Spire and Reef Workshop cost rare (unit files); production holds them when a build order lists one (`skai_air`). 0 is inert where no item costs rare; the leftover pass still serves such a queue if data later adds one. `tech` 30 = half the army: a 50-rare tech building then takes about a third of rare income (65 to 190 per 30 s measured, `balance_first`), 25 to 70 s. Placeholder. |
| `budget_opening_production_factor` | multiplier on `production` weights before the opening's handover | factor | 1 to 20 (integer) | 12 | 10 x 12 = 120 equals `economy` and is twice `army`, so a due build-order step gets two thirds of money contested with army units, the nearest proportional form of rule R's full hold. Placeholder. |
| `budget_threat_defence_factor` | multiplier on `defence` weights while a threat is inside the base | factor | 1 to 20 (integer) | 2 | Doubles defence's contested share against the army (0.09 to 0.17) only while threatened; the ceiling still bounds the game total. Placeholder. |
| `budget_worker_floor` | own workers below which `army` and `defence` pause | workers | 0 to 200 (0 = never) | 4 | `starting_worker_count` 4 (`tuning/economy.json`): the opening never pauses; only losses below the start count do. |
| `budget_defence_max_spend_share` | ceiling on defence spending as a share of all spending, checked before each defence purchase | fraction | 0 to 1 (0 = no ceiling) | 0.12 | The `static_defence_share` bar (0.15, `behaviour_checks.json`) less 0.03 for orders the AI counts that the simulation refuses or refunds (the observer counts accepted orders only). Margin is a placeholder. |
| `budget_rare_surplus_on` | free rare at which rare-cost units are boosted | resource units (rare) | 0 to 100000 (0 = rule off) | 100 | Above the dearest rare item (Leviathan 80) so saving for one never triggers it; below the 150 `balance_unspent` bar so it acts before the bar is crossed. |
| `budget_rare_surplus_off` | free rare below which the boost ends | resource units (rare) | 0 to `budget_rare_surplus_on` | 80 | One Leviathan: when the idle bank can no longer pay the dearest item, it is no longer idle. |
| `budget_rare_surplus_weight_factor` | multiplier on composition weights of units with a rare cost while the surplus is active | factor | 1 to 20 (integer) | 3 | Smallest integer at which the Water standard composition (grasper 30, shellback 10, ink lobber 10, spoutling 10) asks for at least the rare income it receives: measured rare-to-common income about 0.1 (Water 65 to 130 against 800 to 1100 per 30 s, `balance_first`), army about 0.67 of common spending. Factor 1: the mix costs 650 rare per 7250 common (0.090), times 0.67 = 0.06; factor 2: 0.133 x 0.67 = 0.089; factor 3: 1950 / 12250 = 0.159, x 0.67 = 0.107, above 0.1. |

Parser errors name the file and field: missing key, unknown or missing queue ID, out-of-range value, all-zero weights, `off > on`.

## B.4 Checks, stated before any run

### B.4.1 Harness addition (package B0)

The summary observer has no worker count or depot time, so two fields and two behaviour checks are added (`balance_measurement.md` M.4.1: one registry entry and one data entry each):

- `GameSummary` appends `workers` (own units with `CanGather` and no footprint after the step at each `summary_bank_times_seconds`; `null` when the game ended earlier) and `drop_off_ticks` (completion tick of each own non-HQ drop-off, ascending, from `StructureCompletedEvent`). Older records without them parse (empty).
- Check `workers_at`: settings `enabled`, `at_seconds` (must be one of `summary_bank_times_seconds`), `min_workers` (0 to 200), `max_violation_fraction`. Metric = workers at that time; Unjudged when the game ended before it; Held when at least `min_workers`. Data: `enabled` true, `at_seconds` 300, `min_workers` 0, `max_violation_fraction` 0 (always passes; the bar of B.4.2 compares the printed medians).
- Check `first_drop_off`: settings `enabled`, `max_seconds` (0 to 7200), `max_violation_fraction`. Metric = seconds of the first entry of `drop_off_ticks` (ticks / ticks per second, integer division); Unjudged when there is none; Held when at most `max_seconds`. Data: true, 7200, 0.

### B.4.2 Behaviour bars (all must pass before the pool runs)

Runs (on `main` with B0 and B1 merged; `-c Release --jobs 4`):

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a abl_s5b_no_b --profile-b aggressive_s5_b --out rts/build/harness/ai_b_behaviour
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --balance --profile aggressive_s5_b --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --out rts/build/harness/ai_b_balance
```

| Bar | Check and source line | Passes when |
| --- | --- | --- |
| BB-1 Static defence share | `BEHAVIOUR static_defence_share profile=aggressive_s5_b` (existing data: `max_share` 0.15, `max_violation_fraction` 0.05) | PASS |
| BB-2 Unspent resources | `CHECK balance_unspent` of the candidate's balance report (existing bars: median common at most 400, rare at most 150 at 300, 600 and 900 s, each faction) | PASS |
| BB-3 Workers | `BEHAVIOUR workers_at` lines of both profiles | candidate `metric_median` >= ablation's minus 1 (one worker = one 12 s train cycle) |
| BB-4 Depot timing | `BEHAVIOUR first_drop_off` lines of both profiles | candidate `metric_median` <= ablation's + 20 s (one depot build time, `perch_depot` and `pearl_depot` 20 s) |

The report also prints, as information only: the `hq_move_new_income` lines, the candidate's balance-report unit and mechanic use (rare-cost units, Thunderhead, Leviathan, Storm Spire, Reef Workshop) against `balance_first`.

### B.4.3 Pool gate

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --pool --profile-b aggressive_s5_b --stage 1 --out rts/build/harness/ai_b_pool
```

Stage 1: members `s3`, `s4r`, `s5`, mirror cells, 46 pairs per cell (552 games, about 2 to 3 hours). `s5` plays as the ablation, so no separate ladder is run.

**Switch-on rule.** BB-1 to BB-4 pass and the pool verdict is PASS, or NO_MEASURABLE_EFFECT (no member worse; Q-1): `use_budget_queues` stays true in `aggressive_s5_b`, then stage 2 (`--stage 2`, 384 games) runs once before the package is marked done; a stage-2 FAIL switches it off. **Off:** any behaviour bar fails (the pool is not run), or the pool verdict is FAIL. Off means `use_budget_queues` false in the candidate; the code, keys and report stay. Nothing is retuned after a run.

## B.5 Build packages

| Package | Content | Files | Review |
| --- | --- | --- | --- |
| B0 | B.4.1 summary fields and checks | `tools/BalanceHarness/GameSummary.cs`, `SummaryObserver.cs`, `BehaviourChecks.cs`, `data/scripts/ai_eval/behaviour_checks.json`, `tools/BalanceHarness/README.md`, `tests/Sim.Tests/Harness/MeasurementTests.cs` | builder tests; `docs-writer` for the README |
| B1 | B.1 to B.3 | new `src/Ai/AiBudget.cs` (pure allocation over arrays, unit-testable without a world), `src/Ai/AiBrain.cs` (Open, Close, queue argument on `TryBuild` and the train sites, rule X and SD changes, P's rare filter, the header's rule list), `AiMemory.cs`, `AiProfile.cs` (keys, known-key list), every file under `data/ai`, new `aggressive_s5_b.json` and `abl_s5b_no_b.json`, `src/Ai/README.md`, new `tests/Sim.Tests/Ai/AiBudgetTests.cs` | `sim-reviewer` (Opus) determinism review of the `rts/src/Ai` diff before merge |
| B2 | B.4.2 then B.4.3; report `docs/reports/ai_b/README.md` with commands, every bar line and the verdict | report only | coordinator |

B0 and B1 can be built in parallel; B2 needs both on `main`.

### Acceptance tests

B0 (`MeasurementTests.cs`):
- **H-1 Fields.** One short game: `workers` has one entry per bank time (null after the end), `drop_off_ticks` ascending and equal to the completion ticks in the game's events; B-1 (observer read-only) still passes; a record without the fields parses with both empty.
- **H-2 `workers_at`.** Synthetic summaries: at the bar edge Held, one below Violated, game ended early Unjudged; `at_seconds` not in the bank times fails naming the field.
- **H-3 `first_drop_off`.** Synthetic: ticks converted with integer division; edge Held; none Unjudged.

B1 (`AiBudgetTests.cs`):
- **BQ-1 Off is identical.** Every shipped profile loads; the pinned AI hashes (`FightSimTests` FP2-2-1 (b), `AiVarietyTests`, `SkirmishAiTests`) stay green; `abl_s5b_no_b` and `aggressive_s5` give equal final state hash and play signature in one fixed-seed 600 s game per matchup on `prototype_1v1`.
- **BQ-2 Parser.** Missing key, unknown queue ID, missing queue ID, weight 1001, all-zero common weights, `budget_rare_surplus_off` above `on`: each fails naming file and field.
- **BQ-3 Proportional split.** `AiBudget` with free common 300, needs army 300 (w 60) and defence 300 (w 6): army 272, defence 27 after pass 1, then the leftover 1 to army: 273 and 27.
- **BQ-4 Cap and free money.** Free 1000, needs army 150 (w 60), defence 100 (w 6): army 150, defence 90 in pass 1, 10 in pass 2; 750 stays free.
- **BQ-5 Release and bounds.** After Close a queue without a want holds 0 and one with a want holds at most its want; over 1000 random inputs from `DeterministicRandom` (seeded), after Open `sum A <= bank` per resource and no account exceeds its want.
- **BQ-6 Head order.** Army head an unaffordable ink lobber, a grasper affordable from the army account: no grasper is ordered that think.
- **BQ-7 Queues do not block each other.** A defence want of 100 unpaid does not stop a worker paid from the economy account (with the switch off, the same view reserves under rule R and the worker still trains: unchanged behaviour).
- **BQ-8 No income.** Every worker idle: all accounts 0 and the intents equal those of the switch-off profile for that think.
- **BQ-9 Defence ceiling.** Ceiling 0.12: spent 1000 of which defence 100, a 100 tower is refused (200 x 10000 > 1200 x 1100) and leaves no want; spent 2000 of which defence 100: allowed.
- **BQ-10 Worker floor.** 3 earning workers, floor 4: army and defence buy nothing and hold 0; a worker is bought from `economy`.
- **BQ-11 Rare surplus.** Free rare 100 sets it, 90 keeps it, 79 clears it; while set, rule P picks an ink lobber where the plain weights pick a grasper (constructed counts).
- **BQ-12 Factors.** Production weight x 12 before the opening's handover and x 1 after; defence x 2 only while `UnderThreat`.
- **BQ-13 Real game.** `aggressive_s5_b` in both slots, 600 s, `prototype_1v1`, both mirrors, fixed seed: no exception; after every Open `sum A <= bank`; each side trains at least one rare-cost unit; two runs give the same state hash.

## B.6 Producer placement and threat defence

- **Producer placement** (`use_forward_producers`, 0.537 alone in AI-P): not switched on in this package. It is a placement question (P-006 package S); folding it in would make B's pool result unreadable. Default: measured separately after B, on top of whichever profile B leaves (Q-2).
- **Threat-defence rule** (`use_threat_defence`): stays off. The defence ceiling would now bound it, but turning it on in the same measurement again mixes two changes. Default: off; it may return as its own package under the ceiling, measured alone (Q-3).

## B.7 Out of scope

Changes under `rts/src/Sim`; unit or combat numbers; any existing AI key's value; which workers gather rare (C2.3, AI-E: package E); goal search for build orders (UAlbertaBot/BOSS); placement (package S); walls, gates, Downdraft, the Tidecaller's wave (package A); per-opening budget weights (Q-5); the response scenarios; a cross-cell pool run.

## Questions for the developer

- **Q-1 No measurable effect on win rate, behaviour bars passed.** P-006 decision 3 switches off packages with no measurable effect. B's purpose is behaviour (unspent money, defence share), which BB-1 to BB-4 measure directly. Default: switch on when the behaviour bars pass and no pool member is worse.
- **Q-2 Producer placement.** Default: not in B; its own measurement after B (or folded into package S).
- **Q-3 Threat-defence rule.** Default: stays off; may return as a separate package under the defence ceiling.
- **Q-4 Rare soft cap.** P-006 says B replaces it, but it decides which workers gather rare, not who spends. Default: unchanged in B; replaced in package E.
- **Q-5 Openings and budget.** Should openings carry their own queue weights (a pressure opening favouring army, a tech opening tech)? Default: no in B; the openings keep their build orders and the production factor gives them their claim.
- **Q-6 The rare-surplus rule goes beyond Petra.** Petra's queues spend what they want; they do not create demand for an idle resource. Without such a rule the queues alone would not stop the rare bank growing. Default: accept, with the hysteresis keys of B.3.
- **Q-7 Placeholders.** The bars of `balance_measurement.md` Q-8 (0.15 defence share, 150 rare and 400 common unspent) and the B.3 values marked placeholder. Default: use them for B2 and set them from its report, never retuning B's verdict after it.
- **Q-8 After switch-on.** Default: `aggressive_s5_b` becomes the base for packages E and S, the `--balance` default profile and a stage-1 pool member `s5b`; until the developer says so, `pool.json` and `balance_report.json` stay as they are.

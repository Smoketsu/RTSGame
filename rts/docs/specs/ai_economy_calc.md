<!-- rts/docs/specs/ai_economy_calc.md -->
<!-- Build spec for P-006 package E (economy by calculation): the skirmish AI decides how many workers each field gets and whether a depot is worth building from the game's own numbers (gather slots, trip time, walk distance, cost, build time) and one horizon value in the profile, replacing AI-E's fixed walk cover and 80 percent depot trigger (specs/field_saturation.md). One profile switch (use_economy_calc), off in every existing profile and on only in the new candidate aggressive_s5_e (a copy of aggressive_s5). A harness-only package computes, per game, the best worker and depot schedule by search in an integer economy model and reports how far the AI's income is from it. Behaviour bars on prototype_1v1 against aggressive_s5 are fixed here before any run; no bar is a win rate. No rts/src/Sim change, no unit, combat, economy or map number change, no pin moves. Part of rts/docs/specs (index: README.md); built by Sonnet builders, the sim-reviewer after each AI package. -->
# Spec: economy by calculation (P-006 package E)

Written 23 Nov 2026 against `main` at `f77a1f6`. Every new number is a **placeholder** unless it is read from existing game data.

## 0. Goal, sources, ground rules

**Goal.** Today (`aggressive_s5`, AI-E of `specs/field_saturation.md`) a field's worker cap is "slots plus enough workers to cover the walk", whether or not those workers repay their cost, and a depot is ordered when the workers reach 80 percent of the home target (`expand_saturation_fraction` 0.8), a guessed threshold. P-006 section 4 E asks for both decisions to come from the game's numbers: **a worker is worth building while the income it adds over a stated horizon exceeds its cost; a depot is worth building when the next worker at home would add nothing and the depot repays itself inside the horizon.** The horizon is one profile value. A harness check computes the best schedule offline and reports the AI's distance from it.

**What E replaces (only with the switch on):**

| Today (AI-E) | Under E |
| --- | --- |
| `SaturationCapOf`: `slots + ceil(slots x W / G) + saturation_extra_workers_per_field` (fixed walk cover) | `CalcCapOf` (rule EW, section 2.2): the count of workers whose added income over the horizon exceeds the worker's cost. On a full field with a normal walk it equals AI-E's cap with extra 0; it is lower on a nearly empty field, on a far field when the horizon is short, and when the last partial worker does not repay. |
| Rule E's `saturated` test (workers >= `expand_saturation_fraction` x uncapped target) | Rule ED (section 2.3): home full, counting the workers the HQ can still train while the depot is being made, **and** the depot's net value over the horizon above 0. |
| Keys `saturation_extra_workers_per_field`, `expand_saturation_fraction` | Not read while `use_economy_calc` is true. Nothing is deleted: `aggressive_s5` and every older profile still use AI-E and the pins depend on it. If the developer later switches E on in the main AI, a cleanup package may delete the two keys and the `saturated` branch once no shipped profile reads them. |

**Kept unchanged under E:** rule E's clock gate (`expand_at_seconds` + interval, the openings' own `expand_at_seconds`, `early_expansion_seconds`), the remaining-fraction trigger `low`, the threat gate, `max_live_depots`, one depot under construction at a time, the target-field choice (`TargetOrder`) and the site search (`TryFindDepotSite`), rule G and the rebalance (they read `CapOf`, which now returns E's cap), the rare soft cap with `rare_cap_yields_to_saturation`, rule RW's closed fields (cap 0 first, as today), `max_workers` as a ceiling, and how a worker or depot is paid (`TrainWorkers`, `TryBuild`).

**Sources read.** `CLAUDE.md`; `docs/STATUS.md` (the "AI work resumed" paragraph and plan item 2); `proposals/P-006-ai-built-for-balancing.md` in full; `specs/field_saturation.md` (SAT 1.2, AI packages common rules, AI-E E1 to E4, section 9); `specs/ai_budget_queues.md` and `reports/ai_b/README.md`; `specs/ai_production_defence.md` (ground rules, sections 1, 9, 10, 11); `src/Ai/AiBrain.cs` (header, `Probe`, `SaturationCap`, `Turn` fields, `CapOf`, `SaturationCapOf`, `Expand`, `TryFindDepotSite`, `WantedWorkers`, `WantedWorkersUncapped`, `WorkersIncludingQueued`, `TrainWorkers`), `AiBrainProduction.cs` (`BuildTicks`, `DepositLog` use), `AiWorldView.cs`, `AiMemory.cs`, `AiProfile.cs` (switch parsing); `Sim/Data/Tuning/EconomyTuning.cs` (public reads); `data/tuning/economy.json`, `data/tuning/match.json`; `data/units/skai/fledgling.json`, `perch_depot.json`, `data/units/water/tender.json`, `pearl_depot.json`; `data/ai/aggressive_s5.json`, `aggressive_s5_pd.json`; harness `GameSummary.cs`, `BehaviourChecks.cs`, `data/scripts/ai_eval/behaviour_checks.json`, `ai_eval.json`; tests `Ai/FightSimTests.cs` (pins).

**Ground rules (every package).**
- **No `rts/src/Sim` change.** Every Sim read is public (tuning, unit definitions). If a builder finds a Sim change unavoidable, stop and report.
- **No pin moves.** With `use_economy_calc` false every existing profile plays bit for bit as on `f77a1f6`. Pins: smoke `207be73eaa460b3c`, economy_smoke `98b2e00d9babe167` (`Harness/HarnessUnitTests.cs`); `aggressive_s5` trace end tick 9505 hash `837f8e7118a9c9de`, `aggressive_s3` trace end tick 9223 hash `c04bdcc0d03b3910` (`Ai/FightSimTests.cs`, `OldProfiles_FixedSeedGamesKeepTheirHash`); `V2ReferenceHash` and `BlindTraceHash` (`Ai/SkirmishAiTests.cs`). If one moves, stop and report. If `main` has re-pinned any before a package starts, use the values on `main` then.
- **No unit, combat, economy, tuning or map number changes** (P-005 freeze, developer standing rule). E reads those numbers; it never changes them.
- **Fog.** The AI reads only `AiWorldView` and its memory: field positions and start amounts (public map data), the **known** remaining amount (`Known(field)`, intel memory, never the live amount), own units, public tuning and unit definitions. Nothing about the enemy is read by E.
- **Determinism (AI).** Integers and `Fix64` only; products that can exceed 2^31 are computed in `long` (stated below); fields in ascending index, planned drop-offs in the existing list order; distances compared squared where only order matters; every tie stated. **No new random draw**: `AiVariety` and the attack-threshold generator are untouched, so opening draws are identical with the switch on or off. AI memory is not hashed (unchanged).
- **Faction-neutral.** Workers, drop-offs and costs come from the plan's `worker_unit` and `drop_off_building` definitions; the brain never names a faction or unit.
- **Commands and events.** None new. E issues only `train_unit` and `build_structure`, as rules 4 and 4b do today.
- **Parsing.** `AiProfile.Parse` reads both new keys with the `Require*` helpers and adds them to `RejectUnknownKeys`; every profile file under `rts/data/ai` and every hand-written test profile gets both keys (switch false). Tests that loop over shipped profiles and list the known non-old ones (`DProfiles_SwitchesAndValidation`, `L1_ShippedProfilesParse`, `R6_NoVarietyForOldProfiles`, and any like them) get `aggressive_s5_e` added to their lists (the B report found these three).
- **Budget queues are assumed off.** Package B failed BB-2 and BB-3 (workers at 300 s: 21 against 24, `reports/ai_b/README.md`) and stays off; STATUS keeps it off until E. E is specified, built and measured with `use_budget_queues` false. E changes only *how many* workers are wanted and *whether* a depot is due; with B on, `TrainWorkers` and `TryBuild` already route those wants through the economy and expansion queues, so E needs no B-specific code. The two together are not tested (out of scope).
- **Production-defence rules.** RR, PC, SA and the raid rules stay off in the candidate (it copies `aggressive_s5`). E does not use `AiMemory.DepositLog`: its inputs are the game's numbers, so every decision can be recomputed from the logged inputs (P-006 section 3 item 5). `ai_production_defence.md` section 10 says E's worker value "needs" the deposit log; this spec supersedes that line (Q-5).

## 1. Switch, keys and profiles

| Key | Unit | Range | Old profiles | `aggressive_s5_e` |
| --- | --- | --- | --- | --- |
| `use_economy_calc` | boolean | true or false | false | true |
| `economy_horizon_seconds` | seconds (integer) | 10 to 1800 | 180 | 180 |

Comment lines (house format, root keys, placed after `use_research`):

```
  // P-006 E: worker caps (rule EW) and the depot trigger (rule ED) come from the income a worker or depot adds over economy_horizon_seconds against its cost, replacing AI-E's walk cover and expand_saturation_fraction; needs use_saturation_workers and use_saturation_expansion. false keeps AI-E. Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_economy_calc.md section 2; placeholder.
  "use_economy_calc": false,
  // P-006 E: how far ahead the AI plans its economy: a worker or depot is worth building when what it gathers within this time after the decision exceeds its cost (only read when use_economy_calc is true). Unit: seconds. Range: 10 to 1800. Source: rts/docs/specs/ai_economy_calc.md section 2.1; placeholder.
  "economy_horizon_seconds": 180,
```

**Validation.** `use_economy_calc` true with `use_saturation_workers` or `use_saturation_expansion` false is a load error naming the file and the key `use_economy_calc` (E replaces parts of both rules, so it cannot run without them).

**Candidate profile.** `rts/data/ai/aggressive_s5_e.json`: a byte copy of `aggressive_s5.json` as on `main` when package E1 starts, with `id` `aggressive_s5_e`, its line-1 header and `$doc` saying what it is, and `use_economy_calc` true. Nothing else differs (test EC-9).

**Why `aggressive_s5` and not `aggressive_s5_pd`.** `aggressive_s5` is the main AI (research on by the developer's decision) and the baseline every recent package is measured against; a copy of it with one switch makes the comparison one change wide, so the baseline is `aggressive_s5` itself and no ablation file is needed. `aggressive_s5_pd` carries casts, walls, support units and the raid rules that failed four of seven bars and are paused (STATUS, 23 Nov); any effect of E there could not be told apart from those rules.

## 2. Rules

### 2.1 Shared quantities (computed once per think, inside `Turn`)

All ticks are at the match rate (`_view.TicksPerSecond`, 20 today).

| Name | Meaning | Source | Unit |
| --- | --- | --- | --- |
| `A` | resource per load | new `AiWorldView.GatherAmountPerTrip` = `sim.Tuning.Economy.GatherAmountPerTrip` | resource units (5 today) |
| `G` | ticks a slot is held per load | `AiWorldView.GatherTicksPerTrip` (existing) | ticks (40) |
| `s` | slots of a field | `GatherSlotsCommon` / `GatherSlotsRare` (existing) | workers (3 / 2) |
| `P` | worker placement time at a site | new `AiWorldView.WorkerPlacementTicks` = `sim.Tuning.Economy.WorkerPlacementTicks(sim.Clock)` | ticks (40) |
| `Hk` | horizon | `_view.SecondsToTicks(Fix64.FromInt(economy_horizon_seconds))` | ticks (3600 at 180 s) |
| `B` | worker build time | `BuildTicks(worker definition)` (existing helper in `AiBrainProduction.cs`) | ticks (240) |
| `cw` | worker cost | worker `CostCommon + CostRare` | resource units (50) |
| `v` | worker speed | worker `SpeedTilesPerSecond` (as `SaturationCapOf`) | tiles per second (Skai 2.5, Water 2.0) |
| `W(d, r)` | round-trip walk between a field at distance `d` and a drop-off of radius `r` | the walk-ticks part of the existing `SaturationCap`, extracted unchanged into `internal static int WalkTicks(Fix64 distance, Fix64 radius, Fix64 interact, Fix64 speed, int ticksPerSecond)`; `SaturationCap` calls it, so its results do not change | ticks |
| `T` | one trip cycle of a worker that never waits | `G + W` | ticks |
| `K` | known remaining amount of the field | `Known(field)` (existing) | resource units |

Rare is valued 1 to 1 with common in every sum below (Q-2).

**Loads helper.** `internal static long Loads(long span, int n, int s, int g, int t)` = `span x min(n x g, s x t) / (g x t)` with integer division (floor), 0 when `span <= 0`; all products in `long`. It is the number of loads `n` workers deliver from one field in `span` ticks: each worker cycles in `t` ticks, and the field yields at most `s` loads per `g` ticks.

**Value of the j-th worker on a field over a span** (j counts from 1):
`Gain(span, j, field) = min(K, A x Loads(span, j, s, G, T)) - min(K, A x Loads(span, j - 1, s, G, T))` (resource units, 0 or more).

### 2.2 Rule EW: worker caps (replaces `SaturationCapOf` when the switch is on)

`CalcCapOf(field)`:
1. `d`, `r`: the field's nearest planned drop-off and its radius, found exactly as in `SaturationCapOf` today (nearest by squared distance, ties to the earlier list entry; with none, `d = field_max_distance_tiles`, `r = 0`; `d` capped at `field_max_distance_tiles`).
2. `T = G + W(d, r)`; `span = Hk - B` (the next worker starts earning after it is trained).
3. `cap` = the smallest `j - 1` (j = 1, 2, ...) with `Gain(span, j, field) <= cw`; the loop stops at the latest when `j - 1` reaches `ceil(s x T / G)` (beyond that `Gain` is 0). So `0 <= cap <= ceil(s x T / G)`.
4. Memoised per think in the existing `_saturationCaps` (`SortedDictionary<int, int>`, keyed by field index).

`CapOf` calls `CalcCapOf` where it now calls `SaturationCapOf` when `use_economy_calc` is true; every other branch of `CapOf` (closed field first, rare soft cap) is unchanged. `WantedWorkers`, `WantedWorkersUncapped` (including its floor "the cap of the first field in target order"), rule G, the rebalance, `ServedCaps` and the probe read `CapOf` and need no change.

**Worked example (test EC-2).** `s` 3, `G` 40, `A` 5, `W` 20 (`T` 60), `B` 240, `cw` 50, horizon 180 s (`Hk` 3600, `span` 3360), `K` 10000: workers 1 to 4 each add 56 loads (280); the 5th adds `Loads(3360,5) - Loads(3360,4)` = 252 - 224 = 28 loads (140 > 50); the 6th adds 0, so `cap` = 5, AI-E's cap. Same field with `K` 300: the 1st adds 280, the 2nd adds 20, so `cap` = 1. Same field with horizon 20 s (`span` 160): the 1st adds 2 loads (10), so `cap` = 0. A far field (`W` 130, `T` 170, Water speed 2.0, 10 tiles from a radius-0.5 depot): horizon 180 s gives `cap` 13 (AI-E: 13); horizon 60 s (`span` 960) gives 5 loads per worker (25 <= 50), `cap` 0.

### 2.3 Rule ED: the depot trigger (replaces `saturated` in `Expand` when the switch is on)

In `Expand`, after the existing early returns (no drop-off building, `blocked`, live depots at the limit, a depot under construction) and the computation of `dueSeconds`, `low`, `earlyGate`, `earlyLift` and `rarePriority`, which stay as they are:

1. **Target and site first.** With the switch on, choose the target field and its site **before** the trigger test, by the existing code (first field in `TargetOrder(_hq.Position, rarePriority)` that is not served and not in `UnbuildableFields`; `TryFindDepotSite`; no site adds the field to `UnbuildableFields` and returns, as today). With no target, return.
2. **Lead time.** `lead = WalkOneWay + P + BuildTicks(depot definition)`, where `WalkOneWay = ceil(|site centre - HQ position| x TicksPerSecond / v)` (`Fix64`, `CeilingToInt`; 0 when `v` is 0). The HQ position stands for the builder's start (the builder is chosen later by `TryBuild`; Q-4 keeps the site rule as is).
3. **Home full.** `homeFull = WorkersIncludingQueued() + lead / B >= WantedWorkersUncapped()` (integer division: the workers the HQ can still finish while the depot is made). With no served field (`Starved`), `homeFull` is true.
4. **Net value.** `DepotNet(site)`:
   - New fields: every field with `Known > 0`, not served, whose centre lies within `field_max_distance_tiles` of the site centre (squared compare, as the served test), ascending index; a rare field only while `RareCapped` is false. Each has `T_f = G + W(d_f, depot radius)` with `d_f = min(|field centre - site centre|, field_max_distance_tiles)`.
   - Order the new fields by `T_f` ascending, ties to the lower index.
   - `headroom = max(0, max_workers - max(WorkersIncludingQueued(), WantedWorkersUncapped()))` (workers).
   - Walk the fields in that order with a worker counter `k` starting at 1 and, per field, a slot counter `j` starting at 1: `span_k = Hk - lead - k x B`; `g = Gain(span_k, j, field)` (with that field's `T_f` and `K`); if `g > cw` and `k <= headroom` the worker is counted (`net += g - cw`, `k++`, `j++`, stay on this field); otherwise go to the next field (keep `k`, reset `j` to 1). Stop when the fields run out, `span_k <= 0` or `k > headroom`.
   - `DepotNet = net - (depot CostCommon + CostRare)` (resource units, `long`; may be negative).
5. **Trigger.** `calc = homeFull && DepotNet > 0`. The existing return becomes: return when the clock is before `dueSeconds` **and** `!low` **and** `!calc` (the `saturated` term is replaced by `calc`). Otherwise build at the site of step 1 with the existing `TryBuild(..., AiBudget.Expansion)` call.

With the switch off, `Expand` runs exactly as today (target and site after the trigger, `saturated` from `expand_saturation_fraction`).

**Worked example (test EC-4).** Depot cost 100, build 20 s (400 ticks), `P` 40, site 20 tiles from the HQ, `v` 2.5: `WalkOneWay` 160, `lead` 600. One new common field, `T_f` 60, `s` 3, `K` 10000, `B` 240, `cw` 50, horizon 180 s, headroom 20: workers k = 1 to 4 have spans 2760, 2520, 2280, 2040 and gain 230, 210, 190, 170; worker 5 (span 1800, the field's 5th) gains 135 - 120 = 15 loads (75); worker 6 gains 0 and ends the field. `net` = 875 - 250 = 625; `DepotNet` = 525 > 0. With horizon 60 s: worker 1's span is 360, 6 loads (30 <= 50); `DepotNet` = -100. `homeFull` with `lead` 600 and `B` 240 adds 2 workers: wanted 10, workers 7 is not full, 8 is.

**Expected effect at today's numbers (for the reader, not a bar).** On `prototype_1v1` at 180 s E gives the same caps as AI-E on every full field, and the home test fires at about the same worker count as 80 percent (8 of 10). E differs where fields run low (caps fall with the known remainder, so `homeFull` holds earlier and workers are not trained for a field about to empty), on far or partial-slot fields, and for the second and later depots (the lead time and the net value decide, not a share).

### 2.4 Explanation record (for traces and test EC-12)

`AiMemory.EconomyNote` (new, not hashed): `(int Tick, int Wanted, long DepotNet, int Lead, bool Fired)`, written once per think by `Expand` only with the switch on: `Wanted` = `WantedWorkersUncapped()`, `DepotNet` and `Lead` of this think's target (0 and 0 when no target was evaluated), `Fired` true when the depot was ordered by `calc` alone (clock and `low` false). Initial value: `Tick` -1.

`GameTrace` adds `econ=w<Wanted>/n<DepotNet>/l<Lead>` to each player's sample line **only when** that player's profile has `use_economy_calc` true, so an `aggressive_s5` trace file stays byte-identical (test EC-13).

### 2.5 Where it runs, hash, commands, events

AI only. Rule order in `Decide` is unchanged: rule 2 (gather) reads `CapOf`, rule 4 (`Expand`) runs rule ED, rule 4b (`TrainWorkers`) reads `WantedWorkers`. No Sim step changes, nothing new in the state hash, no new command, intent kind or event. Update the `AiBrain.cs` header (a line "4a'''. Economy by calculation (ai_economy_calc.md, only with use_economy_calc)" after the AI-E paragraph, stating rules EW and ED in one sentence each), the `AiProfile.cs` "Differences" list, `AiMemory.cs` header, `rts/src/Ai/README.md` and the headers of touched files.

## 3. Harness

### 3.1 Package E0: metrics (`rts/tools/BalanceHarness` and `rts/data/scripts/ai_eval` only)

1. **Summary fields** (`GameSummary`, written by `SummaryObserver`, read back by `GameRecord`; an old record without them parses with empty lists):
   - `workers_by_bucket`: own workers (units with `CanGather` and no footprint) after the last step of each income bucket that ended. Unit: workers. One entry per completed bucket.
   - `spent_other_by_bucket`: `[common, rare]` net spending per bucket on every spending class **except** `worker` and `drop_off` (the classes of `Spent`). Unit: resource units per bucket. One entry per bucket that started (same indexing as `Income`).
2. **Check `income_by`** (`BehaviourChecks.cs`, one registry entry; `behaviour_checks.json` entry): metric = common plus `rare_weight` x rare deposited in the buckets that end at or before `at_seconds` (from `Income`); Unjudged when the game ended before `at_seconds`; Held when the metric is at least `min_income`. Keys: `enabled` true; `at_seconds` 300 (seconds, 1 to 7200, a multiple of `summary_bucket_seconds`, else a settings error naming the key); `min_income` 0 (resource units, 0 to 1000000; 0 always holds, the bar is on medians); `max_violation_fraction` 0; `print_by_opening` true.
3. **`print_by_opening`** (optional boolean on the `income_by` and `econ_gap` entries only; read by those two checks): after the check's `BEHAVIOUR` line the runner prints one line per (faction, opening) of the profile's slot-games, factions then openings in ordinal order: `BEHAVIOUR_BY_OPENING check=<id> profile=<p> faction=<f> opening=<o> games=<judged> metric_median=<m>` (the median as the existing line computes it). One shared helper in `BehaviourRunner`.

Tests H-E1 to H-E3 (section 5).

### 3.2 Package E-OPT: best schedule by search (`rts/tools/BalanceHarness` and `rts/data/scripts/ai_eval` only)

A check `econ_gap` that, for each judged slot-game, builds a small integer **economy model** of that player on that map, finds the best worker and depot schedule by exhaustive grid search, and compares three income totals up to `at_seconds`:

- `OPT`: the model's income under the best schedule;
- `MOD`: the model's income under the AI's actual schedule (its workers per bucket and its drop-off completion ticks);
- `REAL`: the AI's real deposits (`income_by`'s metric with rare weight 1).

Metrics in permille (integers, floor): **decision gap** `= 1000 x (OPT - MOD) / OPT` (the check's metric: how much the AI's choices cost inside the model); total gap `= 1000 x (OPT - REAL) / OPT`; model error `= 1000 x (MOD - REAL) / MOD` (signed). The last two are extras; the check prints their sums and judged count, and with `print_by_opening` the decision gap per opening. Unjudged when the game ended before `at_seconds`, or `OPT` or `MOD` is 0.

**Model (all integers, one step per second, `at_seconds` steps; the slot's faction plan from the profile under test).**
- *Inputs:* the map's fields (position, kind, start amount; the model uses start amounts, it has no fog), the slot's spawn point as the HQ position, the faction plan's worker and drop-off definitions (`v`, `cw`, `B`, depot cost, build ticks, radius), the HQ definition's radius, the tuning `A`, `G`, slots, interact range, `P`, `starting_common`, `starting_worker_count`, and the profile's `field_max_distance_tiles` (`R`) and `depot_ring_min_tiles`.
- *Bases:* the HQ serves every field within `R` of the spawn, walk distance = centre to spawn, HQ radius. Depot number m goes to the first field in **model order** (fields not yet served with amount left, by squared distance from the spawn ascending, ties to the lower index; common and rare alike) and serves every unserved field within `R` of that field's centre; the walk distance to the target field is `depot_ring_min_tiles` and to another field `g` it is `|g - target| + depot_ring_min_tiles`; drop-off radius = the depot's. Each field's `T_f = G + WalkTicks(...)` with the same function as the AI.
- *Workers:* a new worker joins the served field with the smallest `T_f` (ties lower index) that has fewer than `ceil(s x T_f / G)` workers; with none, the HQ does not train. Travel to the first load is not modelled (stated limit).
- *Income per second:* per served field, `acc_f += TicksPerSecond x min(n_f x G, s x T_f)`; `loads = acc_f / (G x T_f)`; `acc_f -= loads x G x T_f`; deposit `min(remaining_f, A x loads)` (all `long`).
- *Spending:* at the first second of each bucket, the bank pays that bucket's `spent_other_by_bucket` (common plus rare, 1 to 1) of the game being judged, so the model carries the same army, supply, tech and defence spending as the AI's opening; the bank may go negative, and nothing is bought while `bank < cost`.
- *Schedule under search:* `(N, w_1 .. w_M)`: the HQ trains (one at a time, `B` ticks, rounded up to whole seconds) whenever it is idle, `bank >= cw`, workers (training included) `< N` and some served field has room; depot m is ordered at the first second at which depot m - 1 has been ordered (for m > 1), no depot is pending, workers (training included) `>= w_m` and `bank >= depot cost`; it completes `lead` seconds later (`lead` from section 2.3 with the model's site at `depot_ring_min_tiles` from the target field centre, rounded up to whole seconds); `w_1 <= w_2 <= ... <= w_M`, and a schedule may stop after any number of depots from 0 to `max_depots`.
- *AI schedule replay (`MOD`):* workers = the AI's `workers_by_bucket` entry of the bucket before (the starting count in bucket 0), assigned by the same fill (workers beyond the room earn nothing); depot m completes at the AI's m-th `drop_off_ticks` entry (rounded down to the second) at the model's m-th site; no bank test.
- *Search:* every `N` from `worker_cap_min` to `worker_cap_max` step `worker_cap_step`, every non-decreasing trigger tuple from `depot_trigger_min` to `depot_trigger_max` step `depot_trigger_step` with 0 to `max_depots` depots. Best = largest income at `at_seconds`; tie to the smaller `N`, then fewer depots, then the lexicographically smaller trigger tuple. At the starting values that is 17 x 1540 = 26180 schedules per slot-game.

**Keys** (`econ_gap` entry of `behaviour_checks.json`, house comment lines, `Source: rts/docs/specs/ai_economy_calc.md 3.2; placeholder.`): `enabled` true; `at_seconds` 300 (a multiple of `summary_bucket_seconds`); `worker_cap_min` 8, `worker_cap_max` 40, `worker_cap_step` 2 (workers, 1 to 200, min <= max); `depot_trigger_min` 4, `depot_trigger_max` 40, `depot_trigger_step` 2 (workers, 1 to 200); `max_depots` 3 (0 to 4); `max_gap_permille` 1000 (0 to 1000; 1000 always holds, the bar is on medians); `max_violation_fraction` 0; `print_by_opening` true.

**Size.** One new file (`EconomyModel.cs`: the model and the search, about 300 lines), one check class, one data entry. If one slot-game's evaluation takes more than 5 s in Release on the build machine, the builder reports the time instead of shrinking the grid.

## 4. Build packages (one builder session each; `dotnet build` with no warnings and `dotnet test` green; the `sim-reviewer` after E1 and E2)

| # | Package | Contents | Tests | Depends on |
| --- | --- | --- | --- | --- |
| 1 | **E0 Metrics** (harness only) | Section 3.1 | H-E1 to H-E3 | nothing |
| 2 | **E1 Worker worth** | `AiWorldView.GatherAmountPerTrip` and `WorkerPlacementTicks`; `WalkTicks` extracted from `SaturationCap`; `Loads`, `Gain`, `CalcCapOf`, the `CapOf` switch; both keys in every profile file and test profile, validation; `aggressive_s5_e.json`; `AiMemory.EconomyNote` (field only) and the trace column; doc updates of section 2.5 | EC-1 to EC-3, EC-8 to EC-11, EC-13 | nothing (may run beside E0) |
| 3 | **E2 Depot worth** | Rule ED in `Expand`, `DepotNet`, writing `EconomyNote` | EC-4 to EC-7, EC-12 | E1 |
| 4 | **E-OPT Best schedule** (harness only) | Section 3.2 | H-E4 to H-E8 | E0 |
| 5 | **E-M Measurement** | Section 6 runs; `rts/docs/reports/ai_econ/README.md` in the `reports/ai_b` format with every bar's values | none | E0 to E-OPT merged |

## 5. Acceptance tests

AI tests in `rts/tests/Sim.Tests/Ai/AiEconomyCalcTests.cs` (hand-built views and the existing `Probe` hook unless stated); harness tests in `rts/tests/Sim.Tests/Harness/`.

| Id | Pass |
| --- | --- |
| EC-1 | `WalkTicks` extraction: every existing AI-E test (`AiSaturationTests`, E-1 to E-7) passes unchanged; `SaturationCap` returns the same values on its two worked examples (5 and 12). |
| EC-2 | `CalcCapOf` worked examples of 2.2: caps 5, 1, 0, 13 and 0 for the five cases. `Loads(0, ...)` and `Loads(-5, ...)` are 0. With `K` 0 the cap is 0. |
| EC-3 | With the switch on, `Probe(...).WantedWorkers` = min(`max_workers`, sum of `CalcCapOf` over served fields, at least the first target-order field's `CalcCapOf`); `ServedCaps` are the `CalcCapOf` values; with the switch off they equal AI-E's on the same view. |
| EC-4 | `DepotNet` worked example of 2.3: 525 at 180 s, -100 at 60 s; `homeFull` false at 7 workers and true at 8 (wanted 10, lead 600, `B` 240). Headroom 3 counts only workers 1 to 3 (`DepotNet` = 630 - 150 - 100 = 380). |
| EC-5 | Expand integration, before the clock gate and with `low` false: home full and `DepotNet` > 0 and bank >= 100: a depot is ordered at the step-1 site; `DepotNet` <= 0: no depot; home not full: no depot. After the clock gate with `DepotNet` <= 0: a depot is ordered (clock kept). Threat gate, live-depot limit and depot under construction still block. |
| EC-6 | Starved (no served field): `homeFull` true; a depot is ordered when `DepotNet` > 0 even before the clock gate. |
| EC-7 | Rare: with `RareCapped` true a rare new field adds nothing to `DepotNet`; with it false it adds by the same formula as common. |
| EC-8 | Parsing: every shipped profile has both keys; only `aggressive_s5_e` has `use_economy_calc` true; a profile with `use_economy_calc` true and `use_saturation_workers` false (and separately `use_saturation_expansion` false) fails naming the file and `use_economy_calc`; `economy_horizon_seconds` 9 and 1801 fail naming the key; a missing key fails. |
| EC-9 | `aggressive_s5_e.json` differs from `aggressive_s5.json` only in line 1, `$doc`, `id` and `use_economy_calc` (line diff after removing comment lines). |
| EC-10 | Off path: the pins listed in section 0 hold without re-pinning; a copy of `aggressive_s5` with `economy_horizon_seconds` 600 gives the same trace result line as `aggressive_s5` (key unread when off). |
| EC-11 | Determinism: two `--trace` runs of `aggressive_s5_e` (seed 20261026, skai_vs_water, 600 s) give the same result line; in one fixed-seed game each slot draws the same opening as `aggressive_s5` does with that seed (read from the game record's `opening0` and `opening1`, or the controller's `Memory.Variety`), so no draw was added. |
| EC-12 | Greybox run, `aggressive_s5_e` mirror, 600 s: assigned workers on any served field never exceed its `CapOf` + 1 for more than one think (as E-6); every think whose `EconomyNote.Fired` is true has `DepotNet` > 0 and `homeFull` true; a field whose known remainder drops below `cw` gets cap 0. |
| EC-13 | The `aggressive_s5` trace file is byte-identical before and after E1 (`cmp`); an `aggressive_s5_e` trace has `econ=` on every player sample line. |
| H-E1 | `income_by` on a hand-made summary: buckets of 30 s, deposits [100,0], [200,50], ...: the metric at 60 s is 350 with rare weight 1; at 90 s with the game ended at 75 s: Unjudged; `at_seconds` 45 with 30-s buckets is a settings error naming `at_seconds`. |
| H-E2 | `workers_by_bucket` and `spent_other_by_bucket` are written for a 120-s game (4 entries each) and read back equal; a record without them parses with empty lists; worker and drop_off spending are not in `spent_other_by_bucket`. |
| H-E3 | Harness off path: `--behaviour --profile-a aggressive_s5 --profile-b aggressive_s5 --games 4 --maps prototype_1v1 --matchups skai_vs_water` stdout differs from before E0 only by the new `income_by` and `BEHAVIOUR_BY_OPENING` lines and the summary count; `behaviour.jsonl` is identical once the new fields are removed. |
| H-E4 | Model on a hand-built one-field map (field 5 tiles from the spawn, no withdrawals, 60 s): the income of a fixed schedule matches a hand computation written in the test. |
| H-E5 | Replay identity: feeding the model's own best schedule back as the "AI schedule" (its per-bucket workers and depot ticks) gives `MOD` = `OPT` within 1 percent (the replay samples workers per bucket). |
| H-E6 | Search: the best schedule and tie-break are as specified on a hand-built map where two schedules tie (the smaller `N` wins); the result is identical across two runs. |
| H-E7 | Withdrawals: a bucket withdrawal larger than the bank blocks every purchase until income repays it. |
| H-E8 | `econ_gap` Unjudged for a game that ended before `at_seconds`; settings errors for `worker_cap_min` > `worker_cap_max` and `at_seconds` not a multiple of the bucket, naming the key. |

## 6. Measurement (fixed before any run; nothing is tuned after a result)

**Run.** After E0 to E-OPT merge, one paired behaviour run on `prototype_1v1` only, the harness defaults of `ai_eval.json` (seeds, three matchups, 24 paired games per cell, 72 slot-games per profile and check), Release build:

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_e --maps prototype_1v1 --jobs 4 --out rts/build/harness/ai_econ_behaviour
```

plus one `--trace` per matchup with `aggressive_s5_e` in both seats (seed 20261026, 600 s) for the `econ=` column. **Baseline: `aggressive_s5`** (profile-a of the same run). Every median below is the printed `metric_median` over the profile's judged slot-games.

| Bar | Metric | Pass when |
| --- | --- | --- |
| EB-1 Workers | `workers_at` (300 s) | candidate median >= baseline median - 1 |
| EB-2 First depot | `first_drop_off` | candidate median <= baseline median + 20 s |
| EB-3 Income | `income_by` (0 to 300 s, common + rare) | candidate median >= floor(0.98 x baseline median) |
| EB-4 Closer to the optimum | `econ_gap` decision gap at 300 s (permille) | candidate median < baseline median (strictly) |
| EB-5 Every opening keeps its economy | `BEHAVIOUR_BY_OPENING check=income_by`, per (faction, opening) with at least 5 judged games in both profiles | for every such opening: candidate median >= floor(0.90 x baseline median) |
| EB-6 Clean run | spot check of the behaviour run | 0 mismatches; every pin of section 0 holds on the measured commit |

**Reading.** All six pass: the report says so and the developer decides whether `use_economy_calc` goes on in `aggressive_s5` (which moves the `aggressive_s5` pin once). Any bar fails: the switch stays off in every profile except `aggressive_s5_e`; the report gives each bar's values and the per-opening lines; no value of this spec is changed after a result (a change is a new revision with its bars restated before the next run). EB-4 equal to the baseline counts as "no measurable effect" (P-006 decision 3): switch off.

**Reported, not judged:** total gap and model error (means from the extras, and the per-opening decision gaps); `workers_at` and income at 600 s if present; second and third depot times from `drop_off_ticks`; median common and rare bank at 300 s; from the traces, the thinks where `Fired` was true and their `DepotNet` and `Lead`. The win and loss columns the behaviour run prints are not read: AI-versus-AI win rates are not balance data and no bar uses them.

## 7. Determinism notes for the sim-reviewer

- No Sim change; new AI state is one memory record (`EconomyNote`), written only with the switch on, not hashed.
- `CalcCapOf` and `DepotNet` loop over fields in ascending index (then by `T_f` with an index tie) and over integer worker counters with a stated bound; `Loads` uses `long` and floor division; the only `Fix64` work is distances (`Length`, as `SaturationCapOf` already does) and the ceil of the walk.
- No new random draw; `AiVariety` untouched (EC-11).
- With the switch off the only changed code path is the `WalkTicks` extraction, which must be a pure move (EC-1, EC-10, EC-13).
- Harness code is measurement only and never reaches a controller.

## 8. Out of scope (one line each)

- Any `rts/src/Sim` change; any unit, combat, economy, tuning or map number.
- Choosing the depot site by best net value (site choice stays; placement is package S).
- The carrying-speed multiplier in the trip time (1.0 today; if it changes, `WalkTicks` must split the two legs).
- Supply as a cost of a worker; worker losses to raids in the worth calculation (raids are rule RW's).
- Package B together with E; turning E on in any profile but `aggressive_s5_e`; difficulty levels.
- Maps other than `prototype_1v1` in the measurement.
- Deleting AI-E's keys and code (only after the developer switches E on in the main AI, section 0).

## Questions for the developer (builders use the defaults)

- **Q-1. Horizon value.** *Default: 180 s* (placeholder). At today's numbers the depot of the EC-4 example first repays with a horizon between 90 and 120 s, a worker on a main field from about 45 s and a worker on the farthest Water field (10 tiles) from about 105 s, so 180 s plans past all three. The offline check (EB-4 and the per-opening gaps) shows whether it is too short or long; changing it is a new revision.
- **Q-2. Rare against common.** *Default: 1 rare = 1 common* in worker and depot worth (no exchange key). Alternative: a profile weight.
- **Q-3. Keep the clock gate and the remaining-fraction trigger beside rule ED.** *Default: yes.* P-006 names only the 80 percent trigger and the walk cover; the clock gate carries the openings' expansion timing (variety). Alternative: E replaces all depot triggers.
- **Q-4. Depot site.** *Default: unchanged* (first unserved field in target order, the existing ring search); E decides only whether. The builder's start is taken as the HQ position in the lead time.
- **Q-5. Measured income.** *Default: E's decisions use the game's numbers, not `AiMemory.DepositLog`*; the harness's model error reports how far the numbers are from reality. This supersedes the line in `ai_production_defence.md` section 10.
- **Q-6. Budget queues.** *Default: off; E is built and measured with B off* (B failed workers at 300 s). If B is revived, E's worker and depot wants feed its economy and expansion queues unchanged.
- **Q-7. Candidate base.** *Default: copy `aggressive_s5`* (section 1). Alternative: copy `aggressive_s5_pd`, which mixes in paused rules.
- **Q-8. If every bar passes, switch E on in `aggressive_s5`?** *Default: no change until you decide*; doing so moves the `aggressive_s5` trace pin once.

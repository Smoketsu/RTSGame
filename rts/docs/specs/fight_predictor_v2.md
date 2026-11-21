<!-- rts/docs/specs/fight_predictor_v2.md -->
<!-- Build spec for the second half of P-006 package C: the fight predictor the AI uses for attack, retreat, counter-mix and defence decisions is replaced, behind one profile switch, by a small deterministic lane simulation inside rts/src/Ai; it is checked on a held-out fight set generated once with --fights (bars stated here, before any result), then by a behaviour check and a paired ladder against its ablation. No change under rts/src/Sim, no unit or combat number changes. Part of rts/docs/specs (index: README.md); read with proposals/P-006-ai-built-for-balancing.md, specs/balance_measurement.md (C.1 to C.9, M.1, M.2) and reports/predict_fights/README.md. -->
# Spec: Fight predictor v2 (P-006 package C, second half)

## 0. Goal, sources, ground rules

**Goal.** `FightPredictor` names the winner in 562 of 765 decided measured fights (0.735), never predicts a stalemate (0 of 15), is weakest on high ground (0.649) and flooding (0.677), has Leviathan and Windrunner pairs at 0 to 1 of 10, and its calibration is inverted exactly where the AI decides: a predicted advantage of 1.1 to 1.25 wins 0.365, the 1.25 to 1.5 bin (holding `attack_min_advantage` 1.3) wins 0.626 (`reports/predict_fights/README.md`). Every attack, retreat, counter-mix, defence and tower decision reads it. This spec gives the AI a better predictor, proves it on fights it was not built from, and then measures whether the AI plays better with it.

**Sources read.** `CLAUDE.md`; `docs/STATUS.md`; `proposals/P-006-ai-built-for-balancing.md`; `specs/balance_measurement.md`; `reports/predict_fights/README.md`; `reports/paired_first/README.md`; `src/Ai/FightPredictor.cs`, `AiBrainFight.cs`, `AiBrainPlacement.cs`, `AiCounter.cs`, `AiProfile.cs`, `AiWorldView.cs`; `tools/BalanceHarness/FightPlan.cs`, `FightRunner.cs`, `FightSettings.cs`, `FightRecord.cs`, `PredictEvaluator.cs`, `GameTrace.cs`; `data/scripts/fights/fights.json`, `matchups.json`, `predict.json`; `data/ai/aggressive_s5.json`; unit files of the Leviathan, Windrunner, Shellback; `tuning/momentum.json`, `flood.json`, `statuses.json`; `specs/p003_rules.md` (dome), `specs/flooding.md`. Written against `main` at `2aa8299`.

**Ground rules.**
- No change under `rts/src/Sim`. No unit, combat or tuning number changes (frozen, P-005). No AI threshold is retuned (`attack_min_advantage`, `retreat_max_advantage`, `defend_min_advantage` and the counter keys keep their values).
- AI code: `Fix64` and integers only, no `System.Random`, no clock, no files at think time, collections walked in ordinal or index order, every tie broken as stated. Everything the predictor needs is data loaded at match start (profile, `ContentCatalog`, `Tuning`, `TerrainTuning`).
- Every new number is a key in a data file, commented in the house format `// <what>. Unit: . Range: . Source: fight_predictor_v2.md <section>; placeholder.` All values below are **placeholders** unless a source is named.
- Harness verdicts are integer arithmetic (pt = parts per 10 000), as in `balance_measurement.md` section 0.

**Corrections to the brief this spec is based on.**
1. `rts/data/scripts/fights/matchups.json` is not read by the counter-mix or any file under `rts/src/Ai`; only `tools/BalanceHarness/FightMatchups.cs` reads it (a documentation check). The counter-mix scores with `FightPredictor.Predict` directly (`AiCounter.cs` line 300).
2. The C.5 `hit_delay` rule ("some weapon has `HitDelayTicks` above 0") is true for every fight, so the `clean` row of the committed report is empty and rule 1's first condition could not fire. Package FP2-1 corrects the rule for future reports (key `hit_delay_ignore_ticks`, section 5.4). The committed verdict (SIMULATE) is **not** re-run under the corrected rule.

---

## 1. Where the predictor is used (every caller)

| Caller | Call | Decision |
| --- | --- | --- |
| `AiBrainFight.PredictFight` (line 96) | the one wrapper for all brain calls below; builds `PredictorSettings.From(_profile)` once | |
| `AiBrainFight` line 193 | army against the enemy model at the target, uphill from rally | attack (`attack_min_advantage`) |
| `AiBrainFight` line 233 | a group against the enemies engaged with it | retreat (`retreat_max_advantage`) |
| `AiBrainFight` line 469 | harass squad against static defences at the target | harass target skip (`AWins`) |
| `AiBrainFight` line 678 | fixed defenders plus chosen units against the threat | defend group size (`defend_min_advantage`) |
| `AiBrainPlacement` line 326 | enemy model against own fighters at the exposed base | threat defence (AI-P, off in shipped profiles) |
| `AiCounter` line 300 | own army plus a purchase against the scaled enemy model, per pool unit | counter-mix weights (relative advantages) |
| `AiCounter` lines 100, 106; `AiBrainFight` line 903 | `FightPredictor.Dps`, `AverageDps` only | not changed |
| `tools/BalanceHarness/PredictEvaluator.cs` line 322 | one measured fight | `--predict-fights` |

All `Predict` callers pass a `PredictorSettings`; that object is where the switch lives (section 4).

---

## 2. Candidate designs

Common to all three: the AI only knows what it sees (unit types, counts, health, positions, terrain heights), so no design can model what the simulation does with pathing, formation shape or unit separation.

| | (a) Lane simulation in `rts/src/Ai` | (b) Table of measured pair results | (c) Repair the formula |
| --- | --- | --- | --- |
| What it is | Per call, the units of both sides fight in a one-dimensional lane in fixed time steps, with the game's damage table, cooldowns, layers, ranges, speeds, dome, splash, drench, momentum, uphill and flood speed rules (section 3) | A table (case x army value x unit pair -> value exchange) generated offline by `--fights`, loaded at start, combined for mixed armies by a Lanchester rule | Add terms for the named weak spots: dome as a wall, flood speed, splash, momentum; re-derive the margin |
| Stalemates | Yes, by the same end rules as the sweep (no damage for the idle window, nobody can hit anybody, time cap) and the dome's regeneration | Only for measured pairs at measured values; mixed armies need a rule nobody measured | Possible with a "dome regen outpaces incoming ranged damage" term; a second special case |
| Flooding | Speed of ground and water units by depth and water team; water healing on own water | Only the measured case (neutral water, 1500) | A speed term in the range head start |
| High ground | Uphill damage multiplier only; the ramp and the high-ground vision rule are not modelled | Measured at 1500 only, seat 1 on top | As now (uphill multiplier) |
| Mixed armies | Natural (each unit picks its own target) | A combination assumption that is exactly what the table cannot check | Linear sums as now |
| Partial health | Yes, health per unit (evenly split within an entry) | No (the sweep starts at full health) | Yes (sums health) |
| Unmeasured army sizes | Yes: discrete units, overkill, splash on clumped units | Interpolation in value; size effects (Lanchester square law, splash density) extrapolated | Continuous; no overkill, no discreteness |
| Seat order | No (symmetric by construction; the measured seat splits come from the arena and the ID order of the fight runner, which the AI never faces) | Could copy seat splits, but they are arena artefacts | No |
| Cost per call | Up to about 1 ms for 64 units a side (bar C1, section 6); tens of microseconds for small armies | Microseconds | Microseconds |
| Build cost | About 600 lines and 15 tests in `rts/src/Ai`; determinism review | Generator, loader, combiner; regenerate on every unit change (units unfreeze later) and refuse a stale table | About 100 lines |
| Main risk | Time budget in the harness; mechanics left out (choke width, ramps, kiting, focus-fire micro) | Circular: the table is the 780 fights, so it scores well in-sample and has no held-out claim outside the measured grid | Fitting by inspection to the 780 fights; no identified cause for the calibration inversion, so a term added for it is a guess |

**Recommendation: (a).** It is the method P-006 cites (UAlbertaBot with SparCraft), the decision rule of C.7 returned SIMULATE, and it is the only design that answers mixed armies, partial health and unmeasured sizes from the game's own rules rather than from the 780 fights. (b) is rejected as the predictor but its data is still used: the 780 fights are the development set the builder may debug against. (c) is rejected because each repair would be chosen by looking at the 780 fights, and the inverted calibration has no known formula cause.

---

## 3. The lane simulation (`FightSim`)

### 3.1 Inputs and set-up

- Entries of side A and side B as today (`FightEntry`: definition, `Count` Fix64, `Health` Fix64), ordinal definition-ID order.
- A `FightContext` (new, section 4): `AUphill`, `Water`, `WaterSide`, `GapTiles`.
- `FightSimRules` (new, built once per match): `CombatRules`; `MomentumTuning`; `StatusTuning`; `FloodTuning`; the four water-depth speed multipliers of `TerrainTuning`; the set of definition IDs whose faction's `MechanicId` equals `MomentumMechanic.MechanicId` (ordinal `SortedSet<string>`; the same test `PredictEvaluator` uses, so no faction is named).
- **Units.** Per entry, units = `Count` rounded half up (entries that round to 0 are dropped); health per unit = `min(MaxHealth, Health / Count)`. If the larger side has more than `predictor_sim_max_units_per_side` units, every entry of both sides is multiplied by `cap / larger` (rounded half up, at least 1 for an entry above 0 before scaling); per-unit health unchanged. Unit order within a side: entry order, then unit number. That index is the tie-break everywhere below.
- **Lane.** Side A units start at x = 0, side B units at x = G. G = `GapTiles` when it is 0 or more, else `largest weapon range of any unit in the fight + predictor_sim_approach_tiles`. A moves towards +x, B towards -x. Distance between two units = |x_i - x_j|; "in reach" uses the reach test of `CombatSystem.InReach` on that distance (the builder ports the rule, including radii if the game adds them).
- **Start state.** Cooldown timers 0, momentum stacks 0, dome pools full and up, no statuses.

### 3.2 One step (length `predictor_sim_step_seconds`, t from 0)

Shots are computed from the state at the start of the step and applied together, so neither side moves or fires first (no seat bias).

1. **Statuses and domes.** Expire slows and Grounded whose time is up. Dome regeneration as `p003_rules.md` 6a: a dome below its pool refills by `RegenPerSecond x dt` once `RegenDelaySeconds` have passed since it last absorbed; a down dome comes up only when full.
2. **Targets.** Each living armed unit whose cooldown timer is 0 or less picks the nearest living enemy it can hit (weapon `CanTarget` of the target's effective layer; Grounded flyers are ground; `can_target_air_on_water` counts as able only when `Water` is not `None`); ties by lower index. Out of reach: no shot, the unit moves in step 4.
3. **Shots.** Per shot, while the timer is 0 or less (several shots in one step when the cooldown is shorter than the step): damage = `Damage x GetMultiplier(type, armour) x uphill x (1 + stacks x damage_bonus_per_stack)`, uphill as `FightPredictor.Dps` (A's ranged non-air shots at non-air targets when `AUphill`); timer += `CooldownSeconds`.
   - **Dome** (ranged shots only): the first up dome of the target's side (lowest index) whose unit is within its radius of the target and farther than its radius from the shooter absorbs as much as its pool holds; the rest carries to the next such dome, then to the target (`p003_rules.md` A4 in one dimension).
   - **Splash** (`SplashRadiusTiles` above 0): besides the target, up to `floor(predictor_sim_splash_density x pi x r^2) - 1` other living enemies (at least 0) within r of the target in x also take the damage, nearest first, ties by index; each goes through the dome rule. Pi is a `Fix64` constant (a mathematical constant, not tuning).
   - **Drench** (`AppliesDrench` against a flyer): slow by `drench_slow_multiplier` for `drench_slow_duration_seconds`; count hits, reset after `spoutling_drench_reset_seconds` without a hit; at `spoutling_drench_hits_to_ground` hits the flyer is Grounded for `grounded_duration_seconds` (`statuses.md` rules).
   - Damage beyond a target's remaining health is lost (overkill).
4. **Apply and move.** Apply all damage; units at 0 health or less die. Each living unit with speed above 0 that had no target in reach moves towards the nearest living enemy it can hit (if it can hit none, it holds) by `speed x multipliers x dt`, stopping where that enemy comes in reach. Multipliers: momentum `1 + stacks x speed_bonus_per_stack`; slow; water: when `Water` is `Shallow` or `Deep`, a unit of effective layer ground not on `WaterSide` uses the `FloodTuning` enemy multiplier of that depth, a water-layer unit uses the `TerrainTuning` water-unit multiplier of that depth. Unarmed units and units with speed 0 hold.
5. **Momentum and healing.** Units in the momentum set: moved this step -> stacks += `stack_gain_per_second x dt` up to `max_stacks`; held -> after `drain_delay_seconds` without moving, stacks -= `drain_stacks_per_second x dt` down to 0; lost on Grounded or slowed per `lost_on_grounded`, `lost_on_slowed`. The damage and speed bonus uses the whole number of stacks (`floor`). Water-layer units on own water (`WaterSide` is their side) heal `water_unit_heal_per_second x dt` up to MaxHealth.
6. **Timers.** Cooldown timers -= dt.
7. **End.** One side has no living unit: the other wins. Both empty in the same step: no winner. No damage dealt by anybody for `predictor_sim_idle_seconds`, or no living unit can hit any living enemy: no winner. t reaches `predictor_sim_max_seconds`: no winner.

### 3.3 Output (same `FightPrediction` struct)

Let f = value left of the winner = `sum(value x health / MaxHealth) / sum(value)` over its units at the start of the call, value = `CostCommon + CostRare` (the fight runner's measure). T = end time in seconds.

- A wins: `Advantage = min(MaxAdvantage, 1 / (1 - f^2))` (`MaxAdvantage` when `1 - f^2 <= 1 / MaxAdvantage`), `TimeAKillsB = max(MinSeconds, T)`, `TimeBKillsA = min(MaxSeconds, TimeAKillsB x Advantage)`, `AWins` true.
- B wins: `Advantage = 1 - f^2`; `TimeBKillsA = max(MinSeconds, T)`, `TimeAKillsB = min(MaxSeconds, TimeBKillsA / Advantage)` (`MaxSeconds` when Advantage is 0); `AWins` false.
- No winner: both times `MaxSeconds`, `AWins` false, `Advantage` 1. Side A empty: `Advantage` 0 (as today).

Why this mapping (stated before any result, not fitted): under Lanchester's square law, which the old formula's time ratio follows (its ratio is (HP_A x DPS_A) / (HP_B x DPS_B)), a side with force ratio R keeps `sqrt(1 - 1/R^2)` of its strength, so `R^2 = 1 / (1 - f^2)`. The AI's thresholds 1.3, 0.7 and 1.5 therefore keep their meaning. The C.3 margin of `--predict-fights` becomes `1 / (1 - f^2)` of the winner, so the calibration table reads the same way.

### 3.4 Left out on purpose (stated in the file header)

Choke width and the number of melee units that can reach one target; the ramp and the high-ground vision rule (only the uphill damage multiplier); formation depth (all units of a side start at one point); hit delay (2 ticks, 0.1 s); Windstorm range (cycles); the AI's own focus-fire and kiting micro; Downdraft casts; pathing and unit separation; structures' footprints (static defences and HQs fight as speed-0 units at their side's start).

### 3.5 Cost per call

The AI thinks every `think_interval_seconds` (0.5 s in `aggressive_s5`); a think makes at most about a dozen `Predict` calls (one per counter-mix pool unit, one attack, one per engaged group, one defend). A step is O(units) with a maintained front index per side (nearest enemy is at the enemy front in one dimension); splash adds O(extra hits). With the starting keys (0.25 s steps, 180 s cap, early end on elimination or idle) a typical call is under 200 steps. Bar C1 (section 6) fixes the budget; if it fails, the developer decides between a coarser step and narrowing the switch (Q-3), not the builder.

---

## 4. Interface and switch

- `FightPredictor.Predict(a, b, aUphill, rules, settings)` keeps its signature and forwards to a new overload `Predict(a, b, in FightContext context, rules, settings)` with `context = FightContext.Dry(aUphill)` (water `None`, `WaterSide` -1, `GapTiles` -1).
- The overload: `settings.Model == PredictorModel.Formula` -> today's code, unchanged and ignoring the new context fields; `PredictorModel.Sim` -> `FightSim.Predict`.
- `PredictorSettings` gains `Model`, the `predictor_sim_*` values and a `FightSimRules?` (required when `Model` is `Sim`, else may be null). The existing six-argument constructor stays and sets `Formula`. `PredictorSettings.From(profile, simRules)` replaces `From(profile)` at its three callers (`AiBrainFight`, `AiCounter`, `PredictEvaluator`/`PredictRunner`); `FightSimRules` is built once in `SkirmishAiController.OnMatchStart` from the `Simulation` (`sim.Content`, `sim.Tuning`, the terrain tuning behind `sim.Terrain`) and in the harness from the loaded data.
- Callers change only where the context adds information: the retreat call (`AiBrainFight` line 233, units already engaged) passes `GapTiles` 0. Every other in-game call passes `FightContext.Dry(aUphill)` (flood state in game: Q-2).

```
public enum PredictorModel : byte { Formula, Sim }
public enum FightWater : byte { None, Shallow, Deep }
public readonly struct FightContext { bool AUphill; FightWater Water; int WaterSide /* -1 neutral, 0 A, 1 B */; Fix64 GapTiles /* -1 = default rule */; }
public static class FightSim { public static FightPrediction Predict(IReadOnlyList<FightEntry> a, IReadOnlyList<FightEntry> b, in FightContext c, FightSimRules rules, PredictorSettings s); }
```

### 4.1 Profile keys (every file under `rts/data/ai`, since every profile carries every key; added to the known-key list in `AiProfile`)

| Key | Unit | Range | Value in existing profiles | Value in `aggressive_s5_fp2` | Source |
| --- | --- | --- | --- | --- | --- |
| `predictor_model` | text | `formula` or `sim` | `formula` | `sim` | this spec, 4 |
| `predictor_sim_step_seconds` | seconds | 0.05 to 1 | 0.25 | 0.25 | placeholder (section 3.5) |
| `predictor_sim_max_seconds` | seconds | 10 to 600 | 180 | 180 | `fights.json` `max_fight_seconds` |
| `predictor_sim_idle_seconds` | seconds | 1 to 600 | 30 | 30 | `fights.json` `stalemate_idle_seconds` |
| `predictor_sim_max_units_per_side` | units | 4 to 500 | 64 | 64 | placeholder |
| `predictor_sim_splash_density` | units per square tile | 0.1 to 4 | 1 | 1 | `fights.json` `formation_spacing_tiles` 1 (one unit per tile); placeholder for games |
| `predictor_sim_approach_tiles` | tiles | 0 to 32 | 4 | 4 | placeholder |

`predictor_max_seconds`, `predictor_min_seconds` and `predictor_max_advantage` are reused by both models. The `predictor_sim_*` keys are read and range-checked in every profile even when the model is `formula`.

### 4.2 Profiles

- **`aggressive_s5_fp2`** (new, candidate): a copy of `aggressive_s5` with `id` and `predictor_model: "sim"` changed, nothing else.
- **Ablation: `aggressive_s5` itself** (unchanged play, `predictor_model: "formula"`). Every existing profile plays exactly as before (test FP2-2-1).

---

## 5. Package FP2-1: held-out fight set support (harness only)

### 5.1 `--fight-settings <path>`

Accepted by `--fights` and `--predict-fights` (and passed to workers). Default: `scripts/fights/fights.json` inside `--data`, as now.

### 5.2 Uneven sizes: case key `seat1_value_ratios`

Optional list per case (ratio, Fix64 text, 0.2 to 5, 1 to 4 entries; absent = `["1"]`). Each fight of the case is planned once per ratio: seat 0 target value V, seat 1 target value `V x ratio` (multiplier rule of `FightSide.Multiplier` on that value). Fight IDs for ratio 1 are unchanged; others gain `/r<ratio in pt>` (for example `/r12500`). `Mismatch` compares seat 1's start value with `ratio x Value0`: mismatch when `|Value1 - ratio x Value0| > max_value_mismatch_fraction x max(Value1, ratio x Value0)`.

### 5.3 Extended records: settings key `extended_records` (bool, default false) and `snapshot_loss_fraction`

When `extended_records` is true, each record appends, after the last existing key:
- `"value_ratio_pt":<int>` (10000 for ratio 1);
- `"lane_depth_class":<0|1|2|-1>`: for flooded cases the depth class (`TerrainGrid.DepthClass`) held by most lane tiles at army spawn, ties to the lower class; -1 for other cases;
- `"snap_tick":<int>,"snap0":{...},"snap1":{...}`: at the end of the first step in which either seat's value left (the measure of `FightRunner` line 313) is at most `1 - snapshot_loss_fraction` of its start, per seat the living army units grouped by definition ID (ordinal) as `"<id>":[<count>,<health in thousandths, floor>]`. `snap_tick` -1 and empty maps when the fight ended first.

`snapshot_loss_fraction`: fraction, 0.05 to 0.95, required when `extended_records` is true. `FightRecord.Parse` reads the new keys when present. With `extended_records` false every record is byte-identical to today's.

### 5.4 `--predict-fights` additions

- Records with extended keys are scored in three subsets: **E** start predictions of ratio-1 fights, **U** start predictions of other ratios, **D** snapshot predictions (entries from `snap0`/`snap1`, `FightContext` with `GapTiles` 0). Start predictions pass `GapTiles` = `start_distance_tiles` of the fight settings, `Water` from `lane_depth_class` (records without it: `Shallow` for flooded cases), `WaterSide` -1 (neutral flood source in `FightRunner`), `AUphill` as C.3.
- `--baseline-profile <id>`: also predicts every record with that profile; tables gain baseline columns; the bars of section 6 that compare with the baseline are judged only when it is given.
- New tables in `predict.md`: subsets E, U, D (C.5 breakdowns 2 to 4 and the C.6 calibration per subset), then `## Bars` with every bar of section 6: value, measured, PASS / FAIL / UNJUDGED (with the reason).
- C.5 correction: `hit_delay` is present only when some weapon's `HitDelayTicks` is above `hit_delay_ignore_ticks`.
- Stderr only (never in files or stdout): mean and largest wall time per `Predict` call in microseconds, per profile.
- Last stdout line gains ` bars=<passed>/<judged> verdict=<PASS|FAIL|INCOMPLETE>` when the records are extended (INCOMPLETE when any bar is unjudged and none failed).

### 5.5 Data files

`rts/data/scripts/fights/predict.json` gains (all placeholders, see section 6): `hit_delay_ignore_ticks` (ticks, 0 to 20, 2), and the bar keys of section 6.

`rts/data/scripts/fights/heldout.json` (new; a copy of `fights.json` with these values; committed in FP2-1, **run only in FP2-3**):

| Key | Value | Why |
| --- | --- | --- |
| `base_seed` | 20261121 | other seeds than the sweep |
| `units` | `[]` (every combat unit) | |
| `compositions` | the four non-standard opening compositions of `aggressive_s5` per faction divided by 10: `skai_pressure_mix` {windrunner 6, talon_archer 1}, `skai_expand_mix` {talon_archer 3, windrunner 2, roc_rider 1}, `skai_air_mix` {roc_rider 3, thunderhead 1, windrunner 1}, `water_pressure_mix` {grasper 6, reef_runner 1}, `water_expand_mix` {grasper 2, shellback 2, ink_lobber 1, spoutling 1}, `water_leviathan_mix` {ink_lobber 2, shellback 2, leviathan 1, spoutling 1} | the armies the AI builds; the two `*_ai_mix` of the sweep are left out |
| `cases` | `flat` [1000, 3000]; `high_ground` [2500]; `flooded` [2500]; `choke` [1000]; `flat_uneven` (kind flat) [2000] with `seat1_value_ratios` ["1.25", "1.5"] | values the sweep never played (750, 1500); both seat orders give "seat 1 bigger" and "seat 0 bigger" for every pair |
| `max_fight_seconds` | 300 | larger armies |
| `extended_records` | true | |
| `snapshot_loss_fraction` | 0.25 | damaged armies |
| `rule_army_value` | 1000 | must be a value of `rule_case` |
| everything else | as `fights.json` | if the plan rejects an army as too large for the arena, raise `arena_width_tiles` and say so in the report |

About 19 sides, 171 pairs, 342 fights per case-value-ratio: 2394 fights (E 1710, U 684), each with up to one snapshot.

### 5.6 Acceptance tests (`rts/tests/Sim.Tests/Harness/HeldOutTests.cs`)

- **FP2-1-1 Default unchanged.** Without `--fight-settings` the plan of `fights.json` has 780 fights with today's IDs; `--fights --only <id>` gives a record byte-identical to the committed line.
- **FP2-1-2 Ratios.** A case with `["1", "1.5"]` plans twice the fights; ratio-1 IDs unchanged, others end in `/r15000`; Value1 is within the multiplier rounding of 1.5 x Value0; mismatch judged against the target ratio.
- **FP2-1-3 Extended records off.** Records byte-identical to today's with `extended_records` false or absent.
- **FP2-1-4 Snapshot.** On a short fight the snapshot is taken in the first step meeting the loss rule; counts and health thousandths equal the living units' at that tick; a fight ending first has `snap_tick` -1 and empty maps.
- **FP2-1-5 Lane depth.** Flooded case: class equals the majority class of lane tiles at spawn; other cases -1.
- **FP2-1-6 Parse round-trip.** With and without every optional key.
- **FP2-1-7 Subsets and context.** E, U and D counted correctly on synthetic records; D predictions are called with `GapTiles` 0 and the snapshot health; start predictions with `start_distance_tiles` and the water of `lane_depth_class`.
- **FP2-1-8 Bars.** Synthetic predictions at each bar's edge give PASS, FAIL and UNJUDGED as section 6 states; without `--baseline-profile` the comparison bars are UNJUDGED.
- **FP2-1-9 hit_delay correction.** With `hit_delay_ignore_ticks` 2 a fight whose weapons all have 2 ticks has no `hit_delay`; the committed sweep then has a non-empty `clean` row.
- **FP2-1-10 Determinism.** `--jobs 1` and `--jobs 4` give identical `fights.jsonl`, `predict.jsonl` and `predict.md` on a small extended settings file.

---

## 6. The bars (stated before any held-out result)

Scored by `--predict-fights --from <heldout fights.jsonl> --fight-settings rts/data/scripts/fights/heldout.json --profile aggressive_s5_fp2 --baseline-profile aggressive_s5`. Mismatched fights are excluded; "decided" and "no winner" as C.4. Each bar is a key in `predict.json`; every value is a **placeholder the developer may set before FP2-3 runs**. Once FP2-3 has run, a bar may change only with the developer's written decision recorded in the report.

| ID | Bar | Key, unit, range | Value |
| --- | --- | --- | --- |
| B1 | Headline: accuracy on E at least the value, and at least the baseline's accuracy on E plus the gain | `v2_min_accuracy` fraction 0 to 1; `v2_min_gain` fraction 0 to 1 | 0.80; 0.05 |
| B2 | Calibration not inverted on E and U together: no margin bin with at least `min_fights_per_row` decided fights has a win share below a lower bin's by more than `calibration_max_inversion` (existing key, 0.10) | (existing keys) | |
| B3 | Attack edge: decided E and U fights with margin in [`attack_min_advantage` of the profile, `v2_attack_edge_upper`): the predicted winner wins at least the share; UNJUDGED below `min_fights_per_row` fights | `v2_attack_edge_upper` ratio 1 to 10; `v2_attack_edge_min_share` fraction | 1.5; 0.70 |
| B4 | Stalemates: of no-winner E and U fights, predicted `none` at least the share (UNJUDGED below `v2_min_no_winner_fights`); of decided E and U fights, predicted `none` at most the share | `v2_min_no_winner_agree` fraction; `v2_min_no_winner_fights` fights 1 to 1000; `v2_max_false_none` fraction | 0.50; 5; 0.02 |
| B5 | Worst pair: every unordered pair of two single units in E with at least `v2_pair_min_fights` decided fights has accuracy at least the value | `v2_min_pair_accuracy` fraction; `v2_pair_min_fights` fights | 0.40; 8 |
| B6 | Damaged armies: accuracy on D at least the value and not below the baseline's on D | `v2_min_damaged_accuracy` fraction | 0.75 |
| B7 | Uneven sizes: accuracy on U not below the baseline's on U | (none) | |
| C1 | Cost: mean wall time per call on the held-out set (stderr; the coordinator reads it, not a code verdict) | `v2_max_mean_call_microseconds` microseconds 1 to 100000 | 1000 |

**Rules against fitting.**
1. `heldout.json` and the bar values are committed in FP2-1, before `FightSim` exists. The held-out sweep is run only in FP2-3, after FP2-2 is merged; the report names the commit of the predictor code and the profile keys.
2. The builder may develop and debug `FightSim` against the committed 780 fights (the development set). Their scores are printed in the FP2-3 report for information and carry no bar.
3. If any bar fails, nothing is retuned: the report goes to the developer. Changing any `predictor_sim_*` key or the `FightSim` rules after seeing held-out results requires a new held-out set (new `base_seed` and new army values) generated after the change.
4. Seat-order splits (C.5 breakdown 1) are reported, not barred (Q-6).

---

## 7. Package FP2-2: `FightSim` and the switch (`rts/src/Ai`, data)

**Files.** New `rts/src/Ai/FightSim.cs` (`FightSim`, `FightSimRules`, `FightContext`, `FightWater`, `PredictorModel`); `FightPredictor.cs` (overload, dispatch, `PredictorSettings` fields and `From(profile, simRules)`, header updated); `AiProfile.cs` (seven keys, known-key list, header); `AiBrainFight.cs`, `AiCounter.cs`, `SkirmishAiController.cs` (settings and rules plumbing, retreat `GapTiles` 0); `tools/BalanceHarness/PredictEvaluator.cs`, `PredictRunner.cs` (settings with rules); every file under `rts/data/ai` (seven keys); new `rts/data/ai/aggressive_s5_fp2.json`; tests. No file under `rts/src/Sim`.

**Acceptance tests** (`rts/tests/Sim.Tests/Ai/FightSimTests.cs` unless stated; synthetic units are added to a fresh `TestWorld.Catalog()` as `p003_rules.md` describes):

- **FP2-2-1 Old profiles unchanged.** (a) `--predict-fights --from` the committed sweep with `--profile aggressive_s5` writes a `predict.jsonl` byte-identical to `reports/predict_fights/predict.jsonl`. (b) One fixed-seed AI game per shipped profile family (`aggressive_s5`, `aggressive_s3`) ends with the final state hash pinned from `main` before this package.
- **FP2-2-2 Profiles.** Every file under `rts/data/ai` parses; all but `aggressive_s5_fp2` have `predictor_model` `formula`; an unknown model text and each out-of-range `predictor_sim_*` value fail naming file and key; `aggressive_s5_fp2` differs from `aggressive_s5` in `id` and `predictor_model` only.
- **FP2-2-3 Symmetry.** Dry, no uphill: `Predict(a, b)` and `Predict(b, a)` give mirrored classes, swapped times and `Advantage(a, b) x Advantage(b, a)` within one `Fix64` rounding step of 1, for five unit pairs and one mixed pair.
- **FP2-2-4 Determinism.** The same call twice gives equal results; a source scan of `FightSim.cs` (added to the list of the existing scan in `FightPredictorTests`) finds no `float`, `double`, `decimal`, `System.Random`, `DateTime`, `Stopwatch` or `Dictionary<`.
- **FP2-2-5 Empty and unhittable.** B empty: A wins with `MaxAdvantage`; A empty: `Advantage` 0. Flyers against a ground-only side that cannot hit air: the flyers win; two sides that cannot hit each other: no winner.
- **FP2-2-6 Dome stalemate.** An unarmed dome unit (pool 150, regen 25 per second, delay 0) with an ally against a ranged attacker of 10 damage per second firing from outside the radius: no winner (idle or cap rule), both times `MaxSeconds`.
- **FP2-2-7 Splash.** One splash attacker (r 2, density 1) against 12 units at one point kills them sooner than the same attacker with radius 0; no shot hits more than 12 units (1 + floor(pi x 4) - 1 = 12 at most).
- **FP2-2-8 Uphill.** A ranged ground side that wins on the flat has a lower `Advantage` with `AUphill`; melee and air shooters are unaffected.
- **FP2-2-9 Drench.** A side of drench shooters plus ground-only melee beats flyers that beat the same side without drench (grounding makes them hittable).
- **FP2-2-10 Momentum.** A momentum-set unit that approaches for at least `max_stacks` seconds kills a static target sooner than with `damage_bonus_per_stack` 0 in the rules.
- **FP2-2-11 Flood.** Ranged water side against melee ground side: `Advantage` higher with `Water` `Deep`, `WaterSide` -1 than dry; with `WaterSide` = the ground side's own side the ground side is not slowed.
- **FP2-2-12 Counts and health.** `Count` 2.6 with `Health` 260 gives 3 units of 100 health; 200 against 100 units are scaled to 64 against 32.
- **FP2-2-13 Gap.** `GapTiles` 0 removes the longer-range side's free shots (its `Advantage` is lower than with the default gap).
- **FP2-2-14 Mapping.** A win with value left f gives `Advantage = 1 / (1 - f^2)` and the times of 3.3; a no-winner result gives class `none` in `PredictEvaluator`.
- **FP2-2-15 Retreat context** (`AiFightTests`): with `predictor_model` `sim` the retreat call passes `GapTiles` 0; with `formula` the result equals today's.

**Review.** No `rts/src/Sim` change, so no `sim-reviewer` pass is required by CLAUDE.md. This spec requires one anyway on `FightSim.cs` and the dispatch: its output decides commands, so a platform difference would split lockstep games.

---

## 8. Package FP2-3: held-out measurement (report only)

1. Run `--fights --fight-settings rts/data/scripts/fights/heldout.json --out rts/docs/reports/predict_v2/heldout` once; commit `fights.jsonl` and `fights.md`.
2. Run section 6's command with `--out rts/docs/reports/predict_v2`.
3. For information: the same with `--from rts/docs/reports/fights_2026-11-18/fights.jsonl` (development set, no bars), `--out rts/docs/reports/predict_v2/devset`.
4. `rts/docs/reports/predict_v2/README.md`: commands, commit of the predictor code, the last stdout lines, the bar table, C1 timing, the four mechanics left out (3.4) with the subsets they likely affect, and nothing retuned.

Gate: all bars PASS -> FP2-4. Any FAIL -> stop and report to the developer (section 6 rule 3).

---

## 9. Package FP2-4: behaviour check, then paired ladder

### 9.1 `--ledger` (harness only)

A read-only observer passed to `GameRunner.Play` beside the optional trace (reads controller memory, events and state after each think and step; never submits), enabled by `--ledger` on `--ladder --paired`. Per slot: an **attack episode** runs from a think after which `Memory.Stance` is `Attack` while it was not before, to the first think after which it is not `Attack` (or the game's end). During an episode it sums the value (`CostCommon + CostRare`) of own army units killed (`UnitKilledEvent`, mobile non-worker units) and of enemy units and structures killed. Records gain `"ledger0":[episodes, won, own_lost, enemy_killed, retreats],"ledger1":[...]` (won = episodes with `enemy_killed > own_lost`; retreats = `Memory.Retreats` at the end). `ladder.md` gains `## Attack ledger` per profile (mirror cells): episodes per game, won share, `enemy_killed / own_lost`.

Tests (`rts/tests/Sim.Tests/Harness/LedgerTests.cs`): **FP2-4-1** without `--ledger` records are byte-identical; **FP2-4-2** a fixed game has the same final state hash with and without `--ledger`; **FP2-4-3** episodes equal the number of stance changes into `Attack` that `--trace` lists for the same game.

### 9.2 Run

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --ladder --paired --ledger --profile-a aggressive_s5 --profile-b aggressive_s5_fp2 --matchups skai_vs_skai,water_vs_water --jobs 4
```

Mirror cells only: the cross cell is decided by faction (36 of 36 pairs concordant in `reports/paired_first`), so it carries no information about the AI. Defaults of `ai_eval.json`: `prototype_1v1`, 72 games per cell, so 72 pairs. At the measured mirror discordant share r = 0.347 the minimum detectable effect is about 9.8 points (M.2); the report states the measured one. About 31 minutes at 4.7 games a minute.

### 9.3 Reading, in this order

1. **Behaviour** (read first; if it fails, the ladder verdict is reported but the package is not switched on): over the mirror games, the candidate's won share of attack episodes is at least the ablation's plus `fp2_min_won_share_gain`, and its episodes per game are at least `fp2_min_episode_fraction` of the ablation's (it still attacks). Keys in `rts/data/scripts/ai_eval/ai_eval.json`: `fp2_min_won_share_gain` fraction 0 to 1, placeholder 0.05; `fp2_min_episode_fraction` fraction 0 to 1, placeholder 0.5.
2. **Paired verdict** (the `PAIRED` line; with mirror matchups only, its `all` row is the mirror row): PASS, FAIL or NO_MEASURABLE_EFFECT as M.2.
3. For information: `--response` for both profiles (scenarios passed, candidate not fewer than ablation is expected but not barred).

Report: `rts/docs/reports/fp2_ladder/README.md` with commands, the `PAIRED` line, MDE, the ledger table and the response counts. What happens on NO_MEASURABLE_EFFECT is Q-4.

---

## 10. Build order, tiers and reviews

| Package | Touches | Tier | Review |
| --- | --- | --- | --- |
| FP2-1 held-out support | `tools/BalanceHarness`, `data/scripts/fights/predict.json`, new `heldout.json`, harness tests | Sonnet `builder` | normal harness review |
| FP2-2 `FightSim` and switch | `rts/src/Ai`, `rts/data/ai`, evaluator plumbing, AI tests | Sonnet `builder` (move to Opus if it fails twice, CLAUDE.md) | `sim-reviewer` on `FightSim.cs` and the dispatch (section 7) |
| FP2-3 held-out measurement | reports only | Sonnet `builder` | coordinator reads the bars |
| FP2-4 ledger and ladder | `tools/BalanceHarness`, `ai_eval.json`, report | Sonnet `builder` | normal harness review |

FP2-1 and FP2-2 may be built in parallel; FP2-3 needs both merged; FP2-4 needs FP2-3 to pass. Each package updates `rts/docs/STATUS.md` and gives changelog bullets to the coordinator.

## Out of scope

Any change under `rts/src/Sim`; unit, combat or tuning numbers; AI thresholds; packages B, E, S and A of P-006; reading flood state in game (Q-2); kiting, focus-fire micro and Downdraft in the predictor; re-running the committed C.7 verdict under the corrected `hit_delay` rule; using the sim for target choice or micro.

## Questions for the developer (the builder uses the defaults)

- **Q-1 Which design.** P-006 decision 5 deferred "fix or simulate" to the calibration report; its rule says SIMULATE. Accept (a), the lane simulation? Default: yes.
- **Q-2 Flood state in game.** In-game callers pass dry water because `AiWorldView` exposes no water depth or water team. Add a read of visible tiles' depth class and water team to `AiWorldView` now, or leave it to package S (one threat map)? Default: leave it to S; the held-out bar still measures flooding through the harness.
- **Q-3 Counter-mix on the sim.** The counter-mix makes one call per pool unit per evaluation, the bulk of the cost. Use the same switch for it, or keep the counter-mix on the formula? Default: same switch; revisit only if C1 or the ladder wall time fails.
- **Q-4 Bars pass but the ladder shows no measurable effect.** P-006 decision 3 switches off a package with no measurable effect, but P-006 section 6 check 2 asks for a predictor that names the right winner. Keep `predictor_model` `sim` in the next base profile? Default: keep it on; a more accurate predictor is a balance requirement in itself; record the decision.
- **Q-5 Bar values.** Every value in sections 6 and 9.3 is a placeholder. Set them before FP2-3 runs? Default: use them as written.
- **Q-6 Seat-order splits.** Leave them out of the bars as arena artefacts (ID order and spawn side in the fight runner)? Default: yes, reported only.
- **Q-7 Retuning after a failed bar.** May `predictor_sim_*` keys be changed after FP2-3 at the cost of a new held-out set (section 6 rule 3), and how many times? Default: once, with a new `base_seed` and new army values; a second failure goes back to the design choice of Q-1.
- **Q-8 Ablation naming.** The ablation is `aggressive_s5` itself and the candidate the new `aggressive_s5_fp2`. Should the candidate become `aggressive_s6` once it passes? Default: no new base profile until package B (budget queues) needs one.

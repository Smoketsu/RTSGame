<!-- rts/docs/specs/ai_reef_runner_defence.md -->
<!-- Build spec (24 Nov 2026) for the developer's request "Teach the Water AI to build Reef Runners for defence": why the Water skirmish AI trains Reef Runners only as its scout (read from rts/src/Ai), the smallest change that makes it field them against Skai's early units (Reef Runner weight in Water's compositions, data only, in a new candidate profile aggressive_s5_rr), a harness package for faction-paired bars, the bars fixed before any run with their stated power, every trial the designer ran, and the open questions with their defaults. Part of rts/docs/specs (index: README.md); built by Sonnet builders. -->
# Spec: the Water AI trains Reef Runners for field defence

Written 24 Nov 2026 against local `main` at `3759fa6` (roster merge included: Reef Runner `max_health` 66). Every value here is a **placeholder** for playtesting.

## 0. Goal, sources, ground rules

**Goal.** Make the Water skirmish AI train Reef Runners as army units early enough to meet Skai's first raiders (Windrunners at Water's fields from about 120 to 180 s), with the smallest change that works, measured against Skai on `prototype_1v1`.

**Sources read.** `CLAUDE.md`; `rts/docs/STATUS.md` ("AI work resumed"); `specs/roster_numbers_1.md` and `reports/roster_numbers_1/README.md`; `specs/ai_threat_map.md` section 10 (bar format); `rts/src/Ai/AiCounter.cs` (counter-mix, `PredictorMix`, `Integers`), `FightPredictor.cs` (formula model), `AiBrain.cs` (`_composition`, scout rule B.1, lines 1275 and 3474 to 3540), `AiBrainFight.cs` (`TryKite`, rule D.6.2), `AiVariety.cs` (openings, handover, jitter), `AiProfile.cs` (faction plan parser); `rts/data/ai/aggressive_s5.json`; `rts/tools/BalanceHarness` (`README.md`, `BehaviourRunner.PairedTest`, `BehaviourChecks.cs`, `GameSummary`, `ProductionMetrics`); `rts/data/scripts/ai_eval/{ai_eval,behaviour_checks}.json`.

**Ground rules (developer decisions in force).**
- No unit, combat or economy number changes. **No Sim change and no `rts/src/Ai` change.** The design is one new AI profile file plus harness code for the bars.
- Behind a new candidate profile, `aggressive_s5_rr`. `aggressive_s5` and every other profile are untouched, so **no pin moves**.
- Evaluated only on `prototype_1v1`. One strong AI; the four Water openings stay distinct.
- Bars are fixed here, before the builder runs anything, and never changed after a result.

## 1. Why the Water AI trains Reef Runners only as its scout (from the code)

What the AI trains is `_composition` (`AiBrain.cs` line 1275): with `use_counter_mix` true it is `AiCounter.Mix`, which (with `use_predictor_mix` true) is `PredictorMix`, and production picks the unit with the largest deficit against those weights (`PickByDeficit`). The weights are (`AiCounter.Integers`):

`weight(u) = floor(mix_resolution x ((1 - k) x base(u) / sum(base) + k x kept(u) / sum(kept)))`, with confidence `k = counter_weight x min(1, seen enemy supply / counter_full_confidence_supply)` (0.7 and 8 in `aggressive_s5`).

1. **Base share.** `base` is the plan's composition (the drawn opening's composition until `handover_seconds`, then the main composition). In `aggressive_s5` the Reef Runner is in none of Water's compositions except `water_grasper_pressure` (10 of 70). Main composition: Pummeler (`water.grasper`) 30, Shellback 10, Ink Lobber 10, Spoutling 10.
2. **Before the enemy is seen, k = 0** and the base share is everything. The first raid arrives in this window (Windrunners at the fields at about 120 to 180 s), so **whatever the base composition holds is what defends the first raid.** Confidence reaches its cap only at 8 seen supply.
3. **Counter share.** `PredictorMix` keeps only the pool units whose predicted advantage is within `counter_keep_fraction` (0.9) of the best one. The formula predictor does not model kiting (`FightPredictor.cs` header: only weapon, armour, layer, speed, dome; range only as a head start of at most 5 s). Against Skai's early units it ranks the Pummeler first. The pinned traced situation `AiArmyMixTests.C2_5_TracedSituation` reads grasper 1.5286, shellback 1.3195, reef_runner 1.1773, so only the Pummeler clears the 0.9 x 1.5286 = 1.376 floor and gets the whole counter share. In the game, the AI's own kite rule D.6.2 makes the Pummeler lose those fights by 0.6 to 0.7 of value (`roster_numbers_1.md` 1.2).
4. **Scout.** Rule B.1 trains one `scout_unit` (`water.reef_runner`) whenever the AI has none, until `scout_max_losses` (3), and `PredictorMix` leaves the scout out of the own army. Measured: 5.35 Reef Runners per Water game (`reports/roster_numbers_1`, balance after).

So two things keep the Reef Runner out: it is absent from the base composition, which alone decides the first raid; and once Skai is seen, the predictor's counter share goes to the Pummeler.

## 2. Options, and the choice

| Option | Size | What the trials showed (section 6) | Verdict |
| --- | --- | --- | --- |
| **A. Data: Reef Runner weight 30 in Water's main composition and all four openings** (`aggressive_s5_rr`) | one profile file, no code | Reef Runners 26.4 per Water game (was 6.6); Water worker losses before the overrun 25 pairs better, 8 worse (p 0.0046); army supply ratio at 300 s 24 better, 11 worse (p 0.041); Water wins 0 of 36 | **chosen** |
| B. Predictor correction: a kite factor in `PredictorMix` (the AI believes a slower, shorter-ranged unit deals only a fraction of its damage to a unit that can kite it under rule D.6.2's own thresholds), faction-plan key, Water only | about 40 lines in `FightPredictor`/`AiCounter`, a loader key, tests, sim-reviewer | Reef Runners 23.6 to 29.9 per game, but worker losses before the overrun unchanged (12 better, 11 worse at factor 0.5; 13 and 13 at 0.25). Games last longer (26 of 36 pairs longer at 0.5, p 0.011). Wins 0 of 36 | not chosen: it acts only after Skai is seen (k > 0), which is too late for the first raid, so on its own it does not defend |
| A + B | both | Reef Runners 40.4; worker losses 24 better, 9 worse (p 0.0135); ratio 25 better, 11 worse (p 0.029); **Water wins 4 of 36 (W 4, L 0, p 0.125, not significant)**; games longer than A alone in 26 of 36 pairs | not chosen now: larger, and its extra effect is not significant at this sample. Q-2 offers it as a follow-up measured on top of A |
| C. A new rule (for example "train a fast defender when raided") | new AI rule, switch, tests | not tried | rejected: A already reaches the raid window without code |

**Reasons for A, from the code.** The first raid is met by the base composition (section 1 item 2), and the base composition is data. A is the smallest change: zero code, a candidate profile only, and unit names stay in data. The general fix B is not similar in size, and alone it does not do the defence job, because its effect waits for the confidence ramp. B remains the right correction of the predictor's belief, so it is Q-2, with its trial numbers.

## 3. The change (package RR-1, data only)

New file `rts/data/ai/aggressive_s5_rr.json`: a byte copy of `rts/data/ai/aggressive_s5.json` at `3759fa6` with exactly these differences.

| Where | Old | New |
| --- | --- | --- |
| Line 1 header | (aggressive_s5's) | `// rts/data/ai/aggressive_s5_rr.json: the Reef Runner defence candidate of rts/docs/specs/ai_reef_runner_defence.md: a copy of aggressive_s5.json (main at 3759fa6) whose Water compositions (main and the four openings) carry water.reef_runner at weight 30, so the Water AI trains Reef Runners as army units from its first army unit, before the first Skai raid is seen. Nothing else differs. Part of rts/data/ai; read by Rts.Ai.AiProfile.` |
| `$doc` | (aggressive_s5's) | `"Reef Runner defence candidate (ai_reef_runner_defence.md): aggressive_s5 with water.reef_runner weight 30 in Water's main composition and in all four Water openings. Every number is a placeholder."` |
| `id` | `aggressive_s5` | `aggressive_s5_rr` |
| `factions.water.composition` | `{ "water.grasper": 30, "water.shellback": 10, "water.ink_lobber": 10, "water.spoutling": 10 }` | the same plus `"water.reef_runner": 30` (last key) |
| opening `water_standard` composition | as main | the same plus `"water.reef_runner": 30` |
| opening `water_grasper_pressure` composition | `{ "water.grasper": 60, "water.reef_runner": 10 }` | `{ "water.grasper": 60, "water.reef_runner": 30 }` |
| opening `water_fast_expand` composition | `{ "water.grasper": 20, "water.shellback": 20, "water.ink_lobber": 10, "water.spoutling": 10 }` | the same plus `"water.reef_runner": 30` |
| opening `water_leviathan_tech` composition | `{ "water.ink_lobber": 20, "water.shellback": 20, "water.leviathan": 10, "water.spoutling": 10 }` | the same plus `"water.reef_runner": 30` |
| The comment line above each of those five `composition` keys | `... Source: <as now>; placeholder.` | keep the line and replace its last sentence with `Source: <as now>; water.reef_runner 30 from rts/docs/specs/ai_reef_runner_defence.md section 3 (equal to the Pummeler's weight in the standard mix); placeholder.` |

The Skai plan, every root key and every other Water key stay as in `aggressive_s5`. The tuning key, its file and unit:

| Key | File | Unit | Range | Value |
| --- | --- | --- | --- | --- |
| `factions.water.composition["water.reef_runner"]` and the same key in each Water opening's `composition` | `rts/data/ai/aggressive_s5_rr.json` | weight (integer, relative to the other weights of that composition) | 0 to 100 | **30** (placeholder; the only value tried, section 6) |

**Effect on the mix (arithmetic).** Before any sighting the Reef Runner gets 30/90 of the standard mix (30/90 grasper pressure, 30/90 fast expand, 30/90 leviathan tech). At full confidence it keeps (1 - 0.7) x 1/3 = 10 percent plus any counter share. The jitter of `composition_jitter_fraction` applies to the new key as to the others. The four openings keep their distinct units, weights and build orders.

**Pairing.** The candidate's Water compositions differ from the baseline's, so the paired harness reports pairing `water:seed` (`skai:full`). In the trials both profiles drew the same Water opening in every pair (36 of 36). **The candidate's Skai plays exactly like `aggressive_s5`'s Skai.** Measured: in every trial the 36 games in which the candidate played Skai had the same state hash and play signature in all four trials. So in a cross pair, one game is exactly the baseline game and the other differs only in Water's AI.

**Determinism.** No code changes. The AI reads the new weights through the existing parser into integers. AI profiles are outside the content digest (`DataLoader`: only tuning texts are hashed), so adding a profile moves no pin.

### RR-1 acceptance tests (NUnit, `rts/tests/Sim.Tests/Ai`)

1. `ReefDefenceProfileTests.Rr1_ParsesAndDiffersOnlyInWaterReefWeights`: `AiProfile.FromDataSet(Data, "aggressive_s5_rr")` parses. Compare it with `aggressive_s5`. The Skai plan is equal field by field (composition entries, build order, openings with their compositions and every opening key, scout, support keys). Every root profile value is equal (compare the two parsed JSON trees with `id` and `$doc` removed and the five Water compositions removed). Each of the five Water compositions equals the `aggressive_s5` one plus `water.reef_runner` = 30, with `water_grasper_pressure` holding 30 instead of 10.
2. `Rr1_OpeningsStayDistinct`: the four Water openings of `aggressive_s5_rr` have the same IDs and weights as `aggressive_s5` (`water_standard=30,water_grasper_pressure=20,water_fast_expand=25,water_leviathan_tech=25`), and no two openings have the same composition.
3. Existing lists: add `aggressive_s5_rr` to the profile-ID lists in `AiOpeningTests.L1_ShippedProfilesParse` and `AiVarietyTests.R6_NoVarietyForOldProfiles` (both list every profile that carries the four openings). Any other test that walks every shipped profile must pass unchanged. If one fails, the builder reports it and does not loosen it.
4. `Rr1_CandidateSkaiIsTheBaselineSkai`: through the harness's paired path (`GameRunner.PlayLadder`, the one `--trace --paired` uses), with baseline `aggressive_s5`, candidate `aggressive_s5_rr`, `prototype_1v1`, matchup `skai_vs_skai`, base seed 20261026, cap 120 s: games 0 and 1 of pair 0 end with the same state hash and play signature.
5. `Rr1_WaterTrainsReefRunnersBeyondTheScout`: same path, matchup `skai_vs_water`, pair 0, the game in which the candidate plays Water, cap 300 s, with the summary observer. The candidate's Water trains at least 4 `water.reef_runner` (the scout rule alone trains 1 at a time). In the game of the same pair in which the baseline plays Water, the baseline trains fewer than the candidate. Record both counts in the test comment.
6. Full suite green with **no pin moved** (any pin that moves is a stop).

## 4. Harness package RR-M (`rts/tools/BalanceHarness` and `rts/data/scripts/ai_eval` only)

The existing paired behaviour test (`BehaviourRunner.PairedTest`) sums the candidate's metric over **both** of its slots in a pair. In a cross pair that is Skai in one game and Water in the other, so a Water metric would be mixed with Skai's. RR-M adds a faction filter and four small checks that read fields the summaries already carry. No new collection, no change to any game.

### 4.1 `--paired-faction <faction id>`

- Allowed only with `--behaviour`. With any other mode it is an error, as is an ID that is not a faction of the data set. Each error names the flag.
- When set, `PairedTest` uses, in each pair, only the slot-games whose slot plays that faction. The candidate's X is the sum of its metric over its slot-games of that faction, and the baseline's Y likewise.
  - Cross pair: X is the candidate's Water slot in one game and Y the baseline's Water slot in the other.
  - Mirror pair of that faction: both slots, as now.
  - Pair in which the candidate or the baseline has no slot-game of that faction (for example a Skai mirror with `water`): left out and counted in `skipped_pairs`, never in `unjudged_pairs`.
- With the flag, each `BEHAVIOUR_PAIRED` line ends with ` faction=<id> skipped_pairs=<n>`, and the `## Paired` table of `behaviour.md` gains those two columns. **Without the flag every output byte is as today.**
- The flag changes only the paired rows. The per-profile `BEHAVIOUR` lines are unchanged.

### 4.2 Four checks (`BehaviourChecks.cs`, registry IDs, entries in `behaviour_checks.json`)

All four are always Held: `max_violation_fraction` 1 and no bar of their own. The bar is the paired test, as with `depot_losses_young`. Each `Evaluate(slot, game)` finds the slot index by reference against `game.Summary0` and `game.Summary1`.

| ID | Metric (num / den) | Unjudged when | Entry keys (all required) | `paired_direction` |
| --- | --- | --- | --- | --- |
| `won` | 1 / 1 when `game.Result` names this slot, else 0 / 1 (draw or undecided: 0) | never | `enabled`, `max_violation_fraction`, `paired_direction` | `higher` |
| `worker_losses_pre_overrun` | `Production.WorkerLossesPreOverrun` / 1 | the summary has no production metrics | as `won` | `lower` |
| `army_supply_ratio_at` | own `ArmySupply[i]` / max(1, enemy `ArmySupply[i]`), where i is the index of `at_seconds` in `summary_sample_seconds` | either sample is null (game ended earlier) or either summary lacks production metrics | `enabled`, `at_seconds`, `max_violation_fraction`, `paired_direction` | `higher` |
| `trained_unit` | `Trained[unit]` / 1 (0 when absent; whole game, as the field holds) | never | `enabled`, `unit`, `max_violation_fraction`, `paired_direction` | `higher` |

Entry validation: `at_seconds` must be one of `ai_eval.json` `summary_sample_seconds` (now 180, 300, 450), else an error naming `at_seconds`. `unit` must be a unit ID of the data set, else an error naming `unit`. Unknown keys are errors (`RejectUnknownKeys`).

New entries in `behaviour_checks.json`, each with its comment line in the house form ending `Source: rts/docs/specs/ai_reef_runner_defence.md 4.2; placeholder.`:

```
"won": { "enabled": true, "max_violation_fraction": 1, "paired_direction": "higher" },
"worker_losses_pre_overrun": { "enabled": true, "max_violation_fraction": 1, "paired_direction": "lower" },
"army_supply_ratio_at": { "enabled": true, "at_seconds": 300, "max_violation_fraction": 1, "paired_direction": "higher" },
"trained_unit": { "enabled": true, "unit": "water.reef_runner", "max_violation_fraction": 1, "paired_direction": "higher" }
```

`overrun_supply` (20) and `summary_window_seconds` (600) stay as they are.

### 4.3 RR-M acceptance tests (NUnit, `rts/tests/Sim.Tests/Harness`)

1. `won`: a hand-made record with result `slot1` gives 1/1 for slot 1 and 0/1 for slot 0. A draw gives 0/1 for both.
2. `worker_losses_pre_overrun`: reads the field. A summary without production metrics is Unjudged.
3. `army_supply_ratio_at`: at 300 s with own 12 and enemy 30 gives exactly 12/30. Enemy 0 gives 12/1. A null sample is Unjudged. `at_seconds` 240 fails to load, naming `at_seconds`.
4. `trained_unit`: count read; absent unit gives 0. `unit` `"water.nope"` fails to load, naming `unit`.
5. `--paired-faction water` on hand-made paired records:
   - (a) A cross pair with these worker losses before the overrun: the candidate's Water slot 3, the baseline's Water slot 6, the candidate's Skai slot 9, the baseline's Skai slot 0. With the flag the pair counts as **better** (3 against 6). Without the flag it counts as **worse** (3 + 9 = 12 against 6 + 0 = 6), which shows the filter is what changes the result.
   - (b) A Skai mirror pair is skipped (`skipped_pairs=1`).
   - (c) The line ends with ` faction=water skipped_pairs=1`.
6. Without the flag, a `--behaviour` run on the existing hand-made records of `BehaviourTests` prints byte-identical paired lines (the old rows are unchanged).
7. `--paired-faction` with `--ladder` is an error naming the flag. `--paired-faction earth` (no such faction) is an error naming the flag.
8. Re-pin, each with a comment "four checks added by ai_reef_runner_defence.md RR-M": every harness test that counts the shipped checks or indexes the `BEHAVIOUR_SUMMARY` line. Known: `EconomyMetricsTests` (`of=9` becomes `of=13`) and `ThreatMetricsTests` (`lines[8]`). No other expected value may change.

## 5. Measurement (package RR-B; fixed before any run, nothing tuned after a result)

### 5.1 Runs (Release build of the measured commit, after RR-1 and RR-M are merged)

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_rr --maps prototype_1v1 --matchups skai_vs_water --games 144 --paired-faction water --jobs 4 --out <dir>/main
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_rr --maps prototype_1v1 --matchups skai_vs_skai,water_vs_water --games 48 --paired-faction water --jobs 4 --out <dir>/mirrors
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --response --profile aggressive_s5      > <dir>/response_s5.txt
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --response --profile aggressive_s5_rr   > <dir>/response_rr.txt
```

Plus four traces (`--trace --paired --profile-a aggressive_s5 --profile-b aggressive_s5_rr --map prototype_1v1 --seed 20261117`, 600 s): `--matchup skai_vs_water --game 0` (baseline Water) and `--game 1` (candidate Water), and `--matchup water_vs_water --game 0` and `--game 1`.

- **No `--seed` on the behaviour runs.** They use the ladder base seed of `ai_eval.json` (20261117). The designer never ran that seed (section 6: every designer game used base seed 5551).
- Main run: 72 cross pairs (144 games; 72 games with the candidate as Water against `aggressive_s5`'s Skai, and the 72 baseline games of the same seeds). About 40 minutes on 4 cores; the trials took 20 minutes per 72 games.
- The run header must read `pairing=skai:full,water:seed`. Anything else: stop and report.

### 5.2 Bars

Every bar is the faction-paired sign test of 4.1: pairs judged from Water's point of view, exact two-sided sign test at `effect_alpha` 0.05. **Primary** bars pass only on BETTER. **Secondary** bars and **guards** fail only on WORSE (or as stated).

| ID | Run | Check (direction) | Kind | Pass when |
| --- | --- | --- | --- | --- |
| RR-B1 Reef Runners fielded (the mechanism) | main | `trained_unit` water.reef_runner (higher) | primary | BETTER |
| RR-B2 Fields defended (the point) | main | `worker_losses_pre_overrun` (lower) | primary | BETTER |
| RR-B3 Army kept | main | `army_supply_ratio_at` 300 s (higher) | secondary | not WORSE (verdict reported) |
| RR-B4 Water wins against Skai | main | `won` (higher) | secondary | not WORSE (verdict reported; also print candidate-Water wins of 72 and baseline-Water wins of 72) |
| RR-G1 Skai mirror unchanged | mirrors | in every `skai_vs_skai` pair, both games have the same `state_hash` and `play_signature` (the candidate's Skai is the baseline's) | guard | 24 of 24 pairs identical |
| RR-G2 Water mirror | mirrors | `won` (higher), Water mirror pairs | guard | not WORSE |
| RR-G3 Response suite | response | no scenario that passes for `aggressive_s5` fails for `aggressive_s5_rr` | guard | holds |
| RR-G4 Clean commands | traces | each candidate trace has no rejection reason, and no larger count of any reason, than the baseline trace of the same matchup and seed (`site_occupied` excepted, as in WL-13) | guard | holds |
| RR-G5 Determinism and suite | main | the harness spot check reports 0 mismatches; full suite green on the measured commit with no pin moved | guard | holds |

**Verdict.**
- **PASS**: RR-B1 and RR-B2 BETTER, RR-B3 and RR-B4 not WORSE, RR-G1 to RR-G5 hold.
- **NO_MEASURABLE_EFFECT**: RR-B1 BETTER, RR-B2 not significant, nothing WORSE, guards hold. The AI fields Reef Runners, but no effect on field defence was seen at this sample size.
- **FAIL**: RR-B1 not BETTER, or anything WORSE, or a guard fails.

On PASS the developer decides whether the weights go into `aggressive_s5` (that moves the `aggressive_s5` pin once). On any other verdict `aggressive_s5_rr` stays a candidate. Nothing is retuned. A new value is a new revision with its bars restated.

### 5.3 Stated power (before the run)

D is the number of decisive (non-tied) pairs; the report prints it per bar. Exact two-sided sign test at 0.05:

| D | better pairs needed | power if the candidate is better in 65 / 70 / 75 / 80 percent of decisive pairs |
| --- | --- | --- |
| 72 | 45 (0.625) | 0.72 / 0.93 / 0.99 / 1.00 |
| 66 | 42 (0.636) | 0.65 / 0.90 / 0.99 / 1.00 |
| 48 | 32 (0.667) | 0.47 / 0.75 / 0.93 / 0.99 |
| 24 | 18 (0.750) | 0.21 / 0.39 / 0.61 / 0.81 |
| 12 | 10 (0.833) | 0.15 / 0.25 / 0.39 / 0.56 |
| 6 | 6 (all) | 0.08 / 0.12 / 0.18 / 0.26 |
| 5 or fewer | cannot pass | none |

Minimum detectable effect with power 0.80 (`mde_permille`, printed): about 0.67 of decisive pairs at D 72. For RR-B2 the trials had 3 ties in 36 pairs, so D is expected near 66. The trial share was 0.76 (25 of 33), for which the power is above 0.95. A true share of 0.65 would be missed about one time in three.
- **RR-B4:** Water wins are rare, so D will be small: 0 decisive pairs in the A trial, 4 in A + B. RR-B4 can therefore only read NO_MEASURABLE_EFFECT, unless Water wins in at least 6 more pairs than it loses with none the other way. It guards against Water winning less, nothing more.
- **Guards:** with 24 Water-mirror pairs, RR-G2 sees only a large regression (the candidate worse in about 3 of 4 decisive pairs). Smaller ones pass unseen, and the report says so.

### 5.4 Reported, not judged

Medians for both profiles, Water slot-games of the main run: Reef Runners trained, worker losses before the overrun and in the 600 s window, the overrun second, army supply at 180, 300 and 450 s (own and Skai's), game end tick. Unit mix trained per Water opening (mean per game), and the Water opening draws (expected identical, as in the trials). From the response runs, every line of both profiles side by side. From the traces, the first Reef Runner beyond the scout and the army at 180 s.

### 5.5 Expected result (stated before the run)

From the A trial (36 pairs, base seed 5551):
- RR-B1 BETTER (36 of 36 there).
- RR-B2 likely BETTER (25 better, 8 worse there; power above 0.9 if the effect holds).
- RR-B3 not WORSE and probably BETTER (24 better, 11 worse there).
- RR-B4 NO_MEASURABLE_EFFECT.

**Skai is expected to keep winning: at least 69 of the 72 candidate-Water games, most likely all 72.** Water's army at 300 s was still about three quarters of Skai's (ratio median 0.75 against 0.64), and Skai's larger ball with Roc Riders still overruns the base at about 330 s. This change targets the raid phase, not the overrun. The guards are expected to hold. RR-G3 was already observed on the trial build (section 6, run R): 6 of 10 scenarios against 5 of 10, none worse.

### 5.6 Guard G-6 of roster_numbers_1 (`response_turtle_water`)

- **This design does not touch it for `aggressive_s5`**, whose files and code are unchanged. Its 26 supply lost before the first kill stays, and the cause stays undiagnosed (Q-5).
- With the candidate profile, the scenario plays a different Water mix and passes (8 supply lost, run R).
- That is one deterministic game in which the AI under test changed. It is not evidence about the regression's cause, and it must not be read as a fix.

## 6. Every trial the designer ran (worktree at `3759fa6`, Release, uncommitted edits, all reverted)

All game trials: `--behaviour --profile-a aggressive_s5 --profile-b <trial profile> --maps prototype_1v1 --matchups skai_vs_water --games 72 --seed 5551 --jobs 4` (36 pairs). Each was judged from Water's point of view with a scratch script (not committed) that applied the comparison 4.1 specifies. Trial profiles were copies of `aggressive_s5` with a new ID. Trials B used a scratch, environment-gated kite factor in `AiCounter.PredictorMix` and `FightPredictor.TimeToKill`, Water plan only: an attacker's damage against a target that kites it under D.6.2's tests (`kite_min_range_gap_tiles` 2, `kite_speed_ratio_min` 0.8) multiplied by the factor; formula model, mix only.

Columns are candidate-Water against baseline-Water in the same pair: pairs better / worse (exact two-sided p).

| Trial | Change | Reef Runners per Water game (cand / base) | Worker losses before overrun (lower is better) | Army supply ratio at 300 s (median cand / base) | Water wins (cand / base, of 36) | Game end later | Unit mix of candidate Water (mean per game) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| T1 | B, factor 0.25 | 29.9 / 6.6 | 14 / 13 (1.00) | 0.742 / 0.637; 19 / 11 (0.20) | 0 / 0 | 28 / 8 (0.0012) | grasper 23.5, reef 29.9, spoutling 14.9, shellback 5.7, leviathan 2.4 |
| T2 | **A (chosen)**, weight 30 | 26.4 / 6.6 | **25 / 8 (0.0046)** | 0.750 / 0.637; **24 / 11 (0.041)** | 0 / 0 | 21 / 15 (0.41) | grasper 30.1, reef 26.4, spoutling 10.4, shellback 8.6 |
| T3 | B, factor 0.5 | 23.6 / 6.6 | 11 / 12 (1.00) | 0.715 / 0.637; 18 / 10 (0.18) | 0 / 0 | 26 / 10 (0.011) | grasper 25.0, reef 23.6, spoutling 15.1, shellback 6.6, leviathan 1.9 |
| T4 | A + B (0.5) | 40.4 / 6.6 | 24 / 9 (0.0135) | 0.731 / 0.637; 25 / 11 (0.029) | **4 / 0** (p 0.125) | 23 / 13 (0.13) | grasper 19.7, reef 40.4, spoutling 13.9, shellback 6.4, leviathan 2.7 |
| T4 against T2 | (same pairs) | | 12 / 8 | 7 / 12 | 4 / 0 | 26 / 10 | |

| Run | What | Result |
| --- | --- | --- |
| I | Identity: the 36 games in which the candidate played Skai, compared across T1 to T4 | 36 of 36 identical state hash and play signature in every trial (the candidate's Skai is the baseline's) |
| O | T2 Water opening draws against baseline | identical in all 36 pairs (`water_standard` 9, `water_grasper_pressure` 10, `water_fast_expand` 6, `water_leviathan_tech` 11); worker losses before overrun fell in three openings (fast expand 6.2 to 4.0, grasper pressure 6.4 to 4.7, leviathan tech 7.6 to 3.8) and not in standard (5.4 to 5.7) |
| V | T2 overrun second (median) | 330 s candidate, 323 s baseline (the pre-overrun window did not shrink) |
| R | `--response --profile aggressive_s5` and `--profile <T2 profile>` | baseline 5 of 10 (`response_turtle_water` FAIL, 26: reproduces roster_numbers_1). T2 6 of 10: `turtle_water` PASS (8 supply lost, first kill 428.8 s); `mass_windrunner_water` trade 1.204 to 1.477 (PASS both); `mass_pummeler_water` -0.062 to -0.063 (FAIL both); `worker_raid_water` max workers lost 4 to 5 (FAIL both); the Skai scenarios are identical |

**Values tried:** composition weight 30 (only). Kite factors 0.25 and 0.5 for option B. Factor 0.5 was calibrated before running it, from the measured kite fights of `roster_numbers_1` 1.2 (Pummeler against Windrunner and Talon at equal value, margins -0.61 and -0.71) through the formula's time-to-kill ratio. No other value was run.

**Seeds and games not used by the designer:** only base seed 5551 and only matchup `skai_vs_water` were played. The builder's runs use base seed 20261117 for every matchup, all three matchups, 72 cross pairs and 24 pairs per mirror, none of which the designer ran. The response suite is deterministic (fixed seeds per scenario). Its candidate result (run R) is already known. It was not used to choose between options (option A was chosen on T1 to T4 before R was run), and RR-G3 is judged on the builder's run as written.

## 7. Build order

1. **RR-M** (harness, one Sonnet session): sections 4.1 to 4.3. No `rts/src` change, so no sim-reviewer pass is needed (tools only). The suite stays green after the re-pins of 4.3 item 8.
2. **RR-1** (data, one Sonnet session): section 3 and its tests. No code. The suite stays green with no pin moved.
3. **RR-B** (measurement, one Sonnet session after 1 and 2 are merged): section 5. Report in `rts/docs/reports/ai_reef_defence/README.md` with the run commands, the header line, every bar row with D, p and verdict, the reported medians of 5.4, the four traces, and `main/` and `mirrors/` (`behaviour.jsonl`, `behaviour.md`) plus both response files. Judge from the outputs before touching anything. Nothing is tuned.

Documentation each package updates: the harness `README.md` (RR-M: the flag, the four checks); `rts/data/ai` needs no index change. Changelog bullets go to the coordinator; STATUS is updated by the coordinator.

## 8. Out of scope

- Any unit, combat, economy or map number.
- Any `rts/src` code, including the predictor's kite correction (Q-2) and the fight decisions (attack, defend, retreat).
- The raid rules (`use_raid_workers`, `use_raid_response`) and Skai's AI.
- The scout rule (the Reef Runner stays the scout).
- The cause of the `response_turtle_water` regression on `aggressive_s5` (Q-5).
- Anti-air after 300 s.
- Any map but `prototype_1v1`.

## Questions for the developer (builders use the defaults)

| # | Question | Default |
| --- | --- | --- |
| Q-1 | Accept the data-only design (Reef Runner weight 30 in every Water composition of a candidate profile) rather than a predictor change, given that the first raid is met before the counter-mix has any confidence? | Yes. |
| Q-2 | Add the predictor's kite correction (option B: a faction-plan key, Water's `PredictorMix` only, the AI's own D.6.2 thresholds, factor 0.5) as a follow-up measured on top of `aggressive_s5_rr`? In the trials, B alone did not reduce raid losses, but it lengthened games (p 0.011). On top of A it gave the first Water wins against Skai in these runs (4 of 36, not significant). | Yes, as a separate spec after RR-B's report, with its own bars (likely Water wins and game length), measured against `aggressive_s5_rr`. |
| Q-3 | One weight (30) in all four openings and the main mix, or a different weight per opening? | 30 everywhere (equal to the Pummeler's weight in the standard mix); per-opening weights only if RR-B shows an opening where it hurts. |
| Q-4 | If RR-B passes, switch the weights into `aggressive_s5`? | Your decision after the report; until then `aggressive_s5` is unchanged. |
| Q-5 | Diagnose `response_turtle_water` on `aggressive_s5` (26 supply lost, was 12) as a separate task? | Yes, separately: trace that one game before and after the health change; nothing in this spec depends on it. |
| Q-6 | The worker-loss bar counts losses before the overrun second (an existing field) instead of before 300 s (the harness keeps no kill times). Accept? | Yes. The overrun median was 323 to 330 s in the trials, so the windows are close. |
| Q-7 | Accept that the Water mirror guard (24 pairs) can catch only a large regression? | Yes. |
| Q-8 | Accept that Skai is expected to keep winning almost every game (at least 69 of 72) even if RR passes? | Yes: this design targets the raid phase, not Skai's overrun at about 330 s. |

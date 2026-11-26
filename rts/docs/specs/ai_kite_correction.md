<!-- rts/docs/specs/ai_kite_correction.md -->
<!-- Build spec (26 Nov 2026) for the kite correction in the skirmish AI's fight prediction (Q-2 of ai_reef_runner_defence.md, developer decision of 25 Nov): the formula predictor believes the AI's own kite rule D.6.2, so a slower, shorter-ranged unit deals a fraction of its damage to a ranged unit that would kite it. Behind an optional faction-plan key predictor_kite_factor (absent = off), read only by the predictor mix (unit choice), on only in a new candidate profile aggressive_s5_kite. Packages K-1 (Rts.Ai and the profile), K-H (harness accuracy check), K-B (measurement with bars fixed before any run), every trial the designer ran and the open questions. Part of rts/docs/specs (index: README.md); built by Sonnet builders. -->
# Spec: kite correction in the AI's fight prediction

Written 26 Nov 2026 against `origin/main` at `c7f2b37` (Reef Runner weights on in `aggressive_s5`: `water.reef_runner` 30 in Water's main mix and all four openings; `aggressive_s5` trace pin `91c318f5a0ad05cb`, end tick 9750). Every number here is a **placeholder** for playtesting.

## 0. Goal, sources, ground rules

**Goal.** The skirmish AI's fight predictor should believe what the AI's own kite rule does. A unit that a ranged enemy would kite under rule D.6.2 (the enemy out-ranges it by the rule's gap and is fast enough by the rule's ratio) deals only a fraction of its damage to that enemy in the prediction. Aim: Water's counter-mix stops giving its whole counter share to the Pummeler against Skai's kiting raiders, and Water wins more games against Skai. Measured on `prototype_1v1` against the new `aggressive_s5`.

**Sources read.** `CLAUDE.md`; `specs/ai_reef_runner_defence.md` (section 1, option B, Q-2, trials T1 to T4); `reports/ai_reef_defence/README.md` (RR-B: NO_MEASURABLE_EFFECT on worker losses, 40 better / 25 worse, p 0.0817; army ratio BETTER; Water mirror 19 / 0; Water 0 of 72 wins); `reports/ai_reef_on/README.md`; `specs/roster_numbers_1.md` 1.2 (kited fights); `rts/src/Ai/FightPredictor.cs` (formula model, `PredictorSettings`), `AiCounter.cs` (`PredictorMix`, `Integers`), `AiBrainFight.cs` (`PredictFight`, `TryKite`, `UnitControl`), `AiProfile.cs` (`AiFactionPlan`, `ParseFaction`, `WithOrders`, kite keys), `AiVariety.cs` (plan copies); `rts/tools/BalanceHarness` (`README.md`: `--predict-fights`, `--paired-faction`, `RTS_CHUNK_GAMES`, `RTS_WORKER_CACHE`; `PredictEvaluator.Predict`); `rts/data/ai/aggressive_s5.json`; `rts/data/tuning/terrain.json`, `flood.json`.

**Ground rules.**
- No Sim change. No unit, combat, economy or map number changes.
- The change sits behind an optional key that is absent from every existing profile. Absent means off, and off runs the old code path exactly. Every existing profile plays as before, so **no pin moves**.
- The core AI names no faction or unit: the rule is read from unit data and the profile's kite keys. Unit and faction names appear only in data and in tests.
- Evaluated only on `prototype_1v1`.
- Bars are fixed here, before the builder runs anything, and never changed after a result.

## 1. What the code does now

1. **The kite rule** (`AiBrainFight.TryKite`, rule D.6.2, runs only with `use_unit_control` true; on in `aggressive_s5`). A unit with a ranged weapon (not melee) steps `kite_step_tiles` away from the nearest visible armed enemy army unit within `kite_trigger_tiles` that can hit it. Two tests decide (line 1015): it kites only when `own range >= enemy range + kite_min_range_gap_tiles` and `own base speed >= kite_speed_ratio_min x enemy base speed`. Both speeds are `UnitDefinition.SpeedTilesPerSecond`, the **base** speed: terrain is not read. `aggressive_s5` values: gap 2 tiles, ratio 0.8.
2. **The predictor** (`FightPredictor`, `predictor_model` "formula" in `aggressive_s5`) ignores kiting. Range enters only as a head start of at most `predictor_range_headstart_max_seconds` (5 s).
3. **Consumers of the predictor.**
   - `AiCounter.PredictorMix` (unit choice; also rule T's producer choice and rule RS, which read the mix weights).
   - `AiBrainFight.PredictFight`: attack, defend, retreat and harass decisions (`attack_min_advantage`, `retreat_max_advantage` and the D-package rules).
   - The harness's `--predict-fights` (`PredictEvaluator`).
4. **Effect.** Against Skai's early units the mix ranks the Pummeler first (`AiArmyMixTests.C2_5_TracedSituation`: grasper 1.5286, shellback 1.3195, reef_runner 1.1773), so only the Pummeler clears the 0.9 keep floor and takes the whole counter share. In the AI's fights the Pummeler is kited and loses by 0.6 to 0.7 of value (`roster_numbers_1.md` 1.2: Windrunner -0.61, Talon -0.71, `skai_early` -0.64; attack-move +0.61, +0.56, +0.58).

## 2. Design

### 2.1 The rule (general, from unit data)

`Kites(kiter, chaser)` is true when all of these hold. It is the negation of `TryKite`'s two early returns, with the same keys read from the same place (the profile's `kite_min_range_gap_tiles` and `kite_speed_ratio_min`):
- the kiter has a weapon and it is not melee;
- the chaser has a weapon;
- `kiter.Weapon.RangeTiles >= chaser.Weapon.RangeTiles + kite_min_range_gap_tiles`;
- `kiter.SpeedTilesPerSecond >= kite_speed_ratio_min x chaser.SpeedTilesPerSecond`.

In `TimeToKill(X, Y)` step 2, an X entry's damage per second against each hittable Y entry is multiplied by `predictor_kite_factor` when `Kites(y, x)` holds, and by 1 otherwise. It is a health-weighted mean as before, and the denominator (the health of the hittable rest) is unchanged. The correction applies in both directions: Y's units are corrected against X's in the other `TimeToKill` call. The AI's own ranged units kite under D.6.2, and the AI assumes the enemy uses the same rule.

Not corrected: step 7 (reach and the head start keep the uncorrected damage weights), the dome term and step 3 (unhittable entries). `TryKite`'s other conditions (trigger distance, "can hit it", the per-think command budget) are not modelled. The chaser's "can hit the kiter" condition needs no extra test: a chaser that cannot hit the kiter has zero damage per second against it, so the factor changes nothing.

**Terrain: base speed, on purpose.**
- The predictor uses base speed because the kite rule does. The point is to predict what the AI's own rule will try to do, and that decision ignores terrain.
- The mix has no place. It chooses the army's units for the whole map and calls the predictor with `FightContext.Dry`. The formula reads only the uphill flag, so a terrain-aware test would need a fight location the mix does not have.
- Known error, one-sided and stated: the correction is too pessimistic for Water on flood water. In shallow water a Water unit moves at x1.15 (deep x1.3), while an enemy ground unit slows to x0.85 (deep x0.5) (`terrain.json`, `flood.json`). A Windrunner therefore cannot open distance from a Pummeler in flood water, but the rule still says it kites.
- On dry ground the error goes the other way. A Water unit moves at x0.85 there, so kiting works better than the base-speed test says.
- Inference, not measured: the raids of the first 300 s land on Water's fields, which are mostly dry ground at those times. Q-3 asks whether to add a location-aware version later.

### 2.2 The key and where it lives

`factions.<faction id>.predictor_kite_factor`: an optional key of a **faction plan**.
- Absent: off. The plan holds 1 and the predictor takes the old path.
- Present: a Fix64 from 0 to 1. Exactly 1 behaves as absent.

Why a faction-plan key and not a root key:
- A root key would also change the candidate's Skai mix, so the guard "Skai mirror identical" could not hold, and the cross pairs would no longer compare one changed Water against one unchanged Skai.
- Only Water's problem is measured. The code stays general: any faction's plan may carry the key.

The faction plan is the place that `scout_unit`, `drop_off_building` and `support_units` already use for optional per-faction behaviour.

The loader rejects the key, with an error naming `predictor_kite_factor`, when any of these holds:
- `predictor_model` is `sim` (the correction exists only in the formula);
- `use_predictor_mix` is false (nothing would read it);
- `use_unit_control` is false (the AI does not kite, so there is no rule to believe).

### 2.3 Which consumers see it: the predictor mix only

Only `AiCounter.PredictorMix` builds its settings with the kite term. `AiBrainFight.PredictFight` (attack, defend, retreat, harass), `AiCounter.Score` (the old counter mix) and every other caller keep `PredictorSettings.From(profile)`, which has no kite term.

Why: the problem is unit choice (section 1 item 4), and the smallest surface that changes unit choice is the mix. Correcting the fight decisions as well would change when Water attacks and retreats with any army. That is a second effect with its own risk (a more timid Water, longer games: option B alone lengthened games, p 0.011), and with both on the bars could not tell which change did what. The designer's trials (section 6 and T1 to T4 of the reef spec) corrected the mix only. Q-4 asks whether to extend the correction to fight decisions as a separate step.

The mix's downstream readers (rule T producer choice, rule RS rare-site order, `BudgetComposition`) see the corrected weights because they read the mix. That is intended.

### 2.4 Variety: the Pummeler's share

- **Before any sighting** (k = 0) the correction does nothing. The base composition decides: Pummeler 30 of 90 in the standard mix, 60 of 90 in grasper pressure.
- **At full confidence** the base share is (1 - 0.7) = 0.3, so the Pummeler keeps at least 0.3 x 30/90 = 10 percent of the mix in the standard mix (20 percent in grasper pressure). The counter share (0.7) goes to the kept units. Against a Skai model of Windrunners and Talons the Pummeler's advantage falls by roughly the factor, so it usually drops out of the kept set. Against models whose units do not kite it (melee units, slow units, or units that out-range it by less than 2 tiles), the Pummeler is scored as today.
- **Flood water.** The mix does not see terrain (2.1), so Water does not field more Pummelers on flood water than on dry ground. Water still fields the Pummeler everywhere through the base share and the openings, but this design does not make the AI prefer it on flood water. Q-3.
- The four openings stay distinct: their compositions are not touched.

Measured unit mix: section 6.

### 2.5 The factor value

The factor is calibrated from the kited fights of `roster_numbers_1.md` 1.2 with Lanchester's square law.
- **Model.** A fight between two pools with strengths S = damage per second x health has an advantage A = S_water / S_skai. The winner keeps r = sqrt(1 - 1/A) of its value, so a measured margin m gives A = 1 / (1 - m²) when Water wins and A = 1 - m² when Water loses.
- **Kiting** lowers only the kited side's damage, so the factor is f = A_kited / A_attack-move.
- **Pure Pummeler rows:**
  - Windrunner: +0.61 to -0.61 gives f = 0.628 / 1.593 = **0.39**.
  - Talon: +0.56 to -0.71 gives 0.496 / 1.457 = **0.34**.
  - `skai_early`: +0.58 to -0.64 gives 0.590 / 1.507 = **0.39**.
  - The `water_early` row is left out because it holds a Shellback, whose dome changes the fight.
- **Value: 0.4**, the median of the three rows (0.39) rounded to one decimal.
- **Assumption:** the fight report's margin is the winner's fraction of value left, with its sign. Read the result as a calibration of size, not an exact one: the kite instrument was a scratch copy without the AI's other orders, and the arena was flat.

The trial of section 6 also ran 0.5 (the value of the reef spec's T3 and T4).

| Key | File | Unit | Range | Value |
| --- | --- | --- | --- | --- |
| `factions.water.predictor_kite_factor` | `rts/data/ai/aggressive_s5_kite.json` | fraction of damage per second a kited unit deals to its kiter in the predictor mix | 0 to 1 (absent or 1 = off) | **SEE 6** (placeholder) |
| `kite_min_range_gap_tiles`, `kite_speed_ratio_min` | every profile (existing) | tiles; ratio | as now | read, not changed (2 and 0.8 in `aggressive_s5`) |

## 3. Package K-1 (`rts/src/Ai`, one new profile, tests)

### 3.1 Code

1. **`AiFactionPlan`** (`AiProfile.cs`)
   - New property `public Fix64 PredictorKiteFactor { get; }`: the share of damage per second a unit deals to an enemy that kites it, in the predictor mix. Unit: fraction. Range: 0 to 1; 1 means off.
   - New optional last constructor parameter `Fix64? predictorKiteFactor = null`; null stores `Fix64.One`.
   - `WithOrders` and the `WithForcedOpenings` site (`AiProfile.cs` near line 1462) pass the value through, so the opening and main plans of `AiVariety` carry it.
2. **`ParseFaction`**
   - Add `"predictor_kite_factor"` to the `RejectUnknownKeys` list.
   - When the key is present, read it with `RequireFix64("predictor_kite_factor", "0", "1")`. When it is absent, pass null.
3. **`AiProfile.Parse`**, after the faction plans and the root switches are read: for every plan whose factor is not 1, throw an error on that plan's `predictor_kite_factor` node when any of these holds:
   - `PredictorModel == Sim`: "needs predictor_model formula";
   - `!UsePredictorMix`: "needs use_predictor_mix";
   - `!UseUnitControl`: "needs use_unit_control".
4. **`PredictorSettings`** (`FightPredictor.cs`)
   - Three new get-only properties:
     - `KiteFactor`: fraction, 0 to 1; `Fix64.One` = off, the default of both existing constructors and of `From`.
     - `KiteMinRangeGapTiles`: tiles, 0 to 20; 0 when off.
     - `KiteSpeedRatioMin`: ratio, 0 to 5; 0 when off.
   - One new method returning a copy with every other field equal:
     ```csharp
     public PredictorSettings WithKite(Fix64 factor, Fix64 minRangeGapTiles, Fix64 speedRatioMin)
     ```
   - Existing constructors and `From` are unchanged in signature and result.
5. **`FightPredictor`**
   - `internal static bool Kites(UnitDefinition kiter, UnitDefinition chaser, PredictorSettings s)`: exactly the four conditions of 2.1.
   - In `TimeToKill` step 2:
     - When `s.KiteFactor == Fix64.One`, the line `x.Count * AverageDps(x.Definition, rest, xUphill, rules)` runs unchanged. That is the identity path; do not multiply by one.
     - Otherwise it uses a new private `KitedAverageDps(x.Definition, rest, xUphill, rules, s)`: the same loop as `AverageDps`, where each term is `target.Health * Dps(...)`, then times `s.KiteFactor` when `Kites(target.Definition, attacker, s)` holds. The health sum is unchanged. Evaluation order is left to right as in `AverageDps`.
   - `ranged` uses the corrected contribution, because it is the same variable. Steps 3, 4, 5 and 7 are unchanged.
   - `FightSim` is not touched (the loader keeps the key away from `sim`).
   - The file header gains one sentence: the optional kite term of `ai_kite_correction.md`, read only by the predictor mix.
6. **`AiCounter.PredictorMix`**, right after `PredictorSettings settings = PredictorSettings.From(profile, memory.FightSimRules);`: when `plan.PredictorKiteFactor != Fix64.One`, replace `settings` with `settings.WithKite(plan.PredictorKiteFactor, profile.KiteMinRangeGapTiles, profile.KiteSpeedRatioMin)`. Nothing else in the file changes.
7. **`AiBrainFight`**: unchanged.

**Determinism.** Fix64 only. Entries are walked in the order given (ordinal definition-ID order, as now). No new state, no random draw. `Kites` is a pure function of two definitions and the settings, and `>=` on Fix64 is exact, so there is no tie to break. With the key absent the predictor runs the old instructions, which is why no pin moves. AI profiles are outside the content digest. **A `sim-reviewer` (Opus) determinism review of the `rts/src/Ai` diff is required before merge.**

### 3.2 Data: `rts/data/ai/aggressive_s5_kite.json`

A byte copy of `rts/data/ai/aggressive_s5.json` at `c7f2b37` with exactly these differences:

| Where | New |
| --- | --- |
| Line 1 header | `// rts/data/ai/aggressive_s5_kite.json: the kite correction candidate of rts/docs/specs/ai_kite_correction.md: a copy of aggressive_s5.json (main at c7f2b37, Reef Runner weights on) whose Water plan carries predictor_kite_factor, so Water's predictor mix believes the AI's own kite rule D.6.2. Nothing else differs. Part of rts/data/ai; read by Rts.Ai.AiProfile.` |
| `$doc` | `"Kite correction candidate (ai_kite_correction.md): aggressive_s5 with predictor_kite_factor in the Water plan. Every number is a placeholder."` |
| `id` | `aggressive_s5_kite` |
| `factions.water`, new last key, with its comment line above | `// Predictor mix only: share of damage per second a unit deals to an enemy that kites it under rule D.6.2 (kite_min_range_gap_tiles, kite_speed_ratio_min). Unit: fraction. Range: 0 to 1 (absent or 1 = off). Source: rts/docs/specs/ai_kite_correction.md 2.5 (calibrated from roster_numbers_1.md 1.2); placeholder.` then `"predictor_kite_factor": <value of 2.5>` |

The Skai plan and every other key stay as in `aggressive_s5`.

### 3.3 K-1 acceptance tests (NUnit, `rts/tests/Sim.Tests/Ai`, new class `AiKiteCorrectionTests` unless stated)

1. **`K1_ProfileParsesAndDiffersOnlyInTheKey`.**
   - `aggressive_s5_kite` parses. Its Water plan's `PredictorKiteFactor` equals the 2.5 value, and its Skai plan holds `Fix64.One`.
   - Compare the parsed JSON trees of `aggressive_s5` and `aggressive_s5_kite` with `id`, `$doc` and `factions.water.predictor_kite_factor` removed: they are equal.
2. **`K1_OldProfilesAreOff`.** Every shipped profile other than `aggressive_s5_kite` has `PredictorKiteFactor == Fix64.One` in every plan.
3. **`K1_LoaderErrors`.** Each of these fails to load with a message naming `predictor_kite_factor`:
   - the kite profile's text with the factor 1.5;
   - the same with -0.1;
   - the same with `predictor_model` `"sim"`;
   - the same with `use_predictor_mix` false;
   - the same with `use_unit_control` false.
4. **`K1_PlanCopiesCarryTheFactor`.** `WithOrders` keeps the factor. On the kite profile, `AiVariety`'s opening plan and main plan for Water carry it for every one of the four openings (force each through `WithForcedOpenings`).
5. **`K1_KitesTable`** (data units, settings with gap 2 and ratio 0.8):
   - `Kites(windrunner, grasper)` true; `Kites(talon_archer, grasper)` true; `Kites(windrunner, shellback)` true.
   - `Kites(windrunner, reef_runner)` false; `Kites(talon_archer, reef_runner)` false; `Kites(windrunner, spoutling)` false.
   - `Kites(grasper, windrunner)` false (melee never kites).
   - Equality boundaries: `Kites(windrunner, grasper)` with gap 3 true and gap 3.01 false; `Kites(spoutling, grasper)` with ratio 1 true and ratio 1.01 false (both speeds 3).
   - Record the unit numbers used in a comment.
6. **`K1_OffIsIdentity`.**
   - `PredictorSettings.From(kiteProfile).KiteFactor == Fix64.One`.
   - For the fights of the existing predictor tests and the C2_5 situation, `Predict` with `settings` and with `settings.WithKite(Fix64.One, 2, 0.8)` returns equal times, winner and advantage.
   - `AiArmyMixTests.C2_5_TracedSituation` stays unchanged.
7. **`K1_KitedSideIsSlower`.** 3 Pummelers against 4 Windrunners, formula, `aggressive_s5` numbers, factor 0.4:
   - Pummeler side as A: `TimeAKillsB` with the kite term is at least the off value divided by 0.4, within 2^-20 relative (the Pummeler gets no head start), or equals `predictor_max_seconds`.
   - `TimeBKillsA` is unchanged.
   - `Advantage` is strictly lower.
   - Record the four times in a comment.
8. **`K1_PerTargetFactor`.** Pummelers against {Windrunners, Reef Runners}: A's time lies strictly between the off time and the off time divided by the factor. Only the Windrunner entry is kited.
9. **`K1_MixUsesItAndFightDoesNot`.** In the C2_5 traced situation with the kite profile's Water plan, `PredictorMix(..., advantagesOut)` gives a lower grasper advantage than the same call with the key removed. Record both advantage tables and both integer mixes in a comment; no other direction is asserted.
10. **`K1_SkaiMixUnchanged`.** `PredictorMix` for the Skai plan returns identical weights under `aggressive_s5` and `aggressive_s5_kite` on the same view.
11. **`K1_CandidateSkaiIsTheBaselineSkai`** (harness path, as `Rr1_CandidateSkaiIsTheBaselineSkai`): baseline `aggressive_s5`, candidate `aggressive_s5_kite`, `prototype_1v1`, `skai_vs_skai`, base seed 20261026, cap 120 s. Games 0 and 1 of pair 0 have the same state hash and play signature.
12. **`K1_WaterGameDiffers`**: same path, `skai_vs_water`, pair 0, cap 300 s. The candidate-Water game's state hash differs from the baseline-Water game of the same pair. Record both games' trained counts per unit in a comment. No direction is asserted, because one game is not evidence.
13. **Existing lists.** Add `aggressive_s5_kite` to the profile lists of:
    - `AiOpeningTests.L1_ShippedProfilesParse`;
    - `AiVarietyTests.R6_NoVarietyForOldProfiles`;
    - `AiFightTests.DProfiles_SwitchesAndValidation` (the s5-derived list; RR-B found this one missing for `aggressive_s5_rr`).

    Any other test that walks every shipped profile must pass unchanged. If one fails, report it and do not loosen it.
14. **Full suite green, no pin moved.** A moved pin is a stop.

## 4. Package K-H: the predictor's accuracy with the kite term (`rts/tools/BalanceHarness` only)

### 4.1 Flag `--predict-kite-factor <f>`

- Allowed only with `--predict-fights`, else an error naming the flag. `f` is Fix64 text from 0 to 1, else an error naming the flag.
- With the flag, `PredictEvaluator` predicts every record with `PredictorSettings.From(profile, simRules).WithKite(f, profile.KiteMinRangeGapTiles, profile.KiteSpeedRatioMin)`. The profile is `--profile`, or predict.json's default as now. Both seats are corrected, as in the game: each player's AI kites.
- With the flag, each `predict.jsonl` line gains a last field `"kite_pair":true|false`. It is true when some entry x of one seat and some entry y of the other have `Kites(y, x)` and `Dps(x, y) > 0`; this needs `Kites` and `Dps` visible to the harness (make them `public`, or add an `InternalsVisibleTo("Rts.BalanceHarness")`; the builder picks whichever the assembly already uses for the harness). The `PREDICT` line ends with ` kite_factor=<f> kite_pair_fights=<n>`, and `predict.md` gains one table, accuracy on decided fights split by `kite_pair`.
- **Without the flag every output byte is as today.**

### 4.2 Why the accuracy check is a guard only on un-kited fights

The 18 Nov records were played with plain attack-move (`FightRunner.Control`): nobody kites. So for a record with a kite pair the term predicts a fight that was not played, and its accuracy against those records is expected to fall. That fall is the intended belief, not an error, and it is not barred.

On a record without a kite pair the term must change nothing. **Guard G6** checks exactly that. The designer's measurement (section 6, P1 to P3) found accuracy 0.735 off, 0.661 at 0.5 and 0.610 at 0.4. In both runs all 570 records without a kite pair were byte-identical to the off run, and all 210 changed records hold a kite pair. A kited reference set (fights played under the kite rule) would be the fair accuracy test for the changed records. It does not exist in the harness (`roster_numbers_1.md` Q-2 proposes it); Q-5 asks for it.

### 4.3 K-H acceptance tests (NUnit, `rts/tests/Sim.Tests/Harness`)

1. Without the flag, the existing predict tests' outputs are byte-identical.
2. With `--predict-kite-factor 0.5` on hand-made records:
   - a Pummeler against Windrunner record has `"kite_pair":true` and different times;
   - a Windrunner against Reef Runner record has `"kite_pair":false`, and its line is identical to the line without the flag except for the added field.
3. `--predict-kite-factor` without `--predict-fights` is an error naming the flag. So are `1.5` and `-0.1`.
4. The `PREDICT` line ends with ` kite_factor=0.5 kite_pair_fights=<n>`, with n the number of `true` lines.
5. Harness README: document the flag in the `--predict-fights` paragraph.

## 5. Measurement K-B (fixed before any run, nothing tuned after a result)

### 5.1 Runs (Release build of the measured commit, after K-1 and K-H are merged)

**Base seed 20261201** for every behaviour run and trace. It is unspent: not in any report, test or data file at `c7f2b37`, and the designer never ran it. 5551, 20261117 and the designer's trial seed 31337 are spent.

```
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_main    dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_kite --maps prototype_1v1 --matchups skai_vs_water --games 144 --seed 20261201 --paired-faction water --jobs 4 --out <dir>/main
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_mirrors dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_kite --maps prototype_1v1 --matchups skai_vs_skai,water_vs_water --games 48 --seed 20261201 --paired-faction water --jobs 4 --out <dir>/mirrors
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --response --profile aggressive_s5      > <dir>/response_s5.txt
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --response --profile aggressive_s5_kite > <dir>/response_kite.txt
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --predict-fights --from rts/docs/reports/fights_2026-11-18/fights.jsonl --profile aggressive_s5 --out <dir>/predict_off
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --predict-fights --from rts/docs/reports/fights_2026-11-18/fights.jsonl --profile aggressive_s5 --predict-kite-factor <2.5 value> --out <dir>/predict_kite
```

Plus four traces: `--trace --paired --profile-a aggressive_s5 --profile-b aggressive_s5_kite --map prototype_1v1 --seed 20261201 --max-game-seconds 600`, with `--matchup skai_vs_water --game 0` and `--game 1`, then `--matchup water_vs_water --game 0` and `--game 1`.

- `<dir>` is `rts/docs/reports/ai_kite_correction/`.
- The main run's header must read `pairing kind: skai:full,water:seed`. Anything else: stop and report.
- **Resumable pieces.** Run with `RTS_CHUNK_GAMES=1` and `RTS_WORKER_CACHE` inside the report folder, as RR-B did. After every 24 finished games, and after each run, commit the cache folder and push. A container restart then loses at most the games in flight; rerunning the same command resumes from the cache.
- **Suite in chunks.** Run the suite by namespace filter in the chunks RR-B used (`reports/ai_reef_defence/README.md`, "Suite"), record each chunk's totals in `suite_chunks.md`, and push after each chunk.

### 5.2 Bars

Every game bar is the faction-paired sign test of `--paired-faction water` (Water's point of view, exact two-sided, `effect_alpha` 0.05), using the checks already in `behaviour_checks.json`. No new check is needed.

| ID | Run | Check (direction) | Kind | Pass when |
| --- | --- | --- | --- | --- |
| K-B1 Water wins against Skai (the point) | main | `won` (higher) | primary | BETTER |
| K-B2 Reef Runners fielded (the mechanism) | main | `trained_unit` water.reef_runner (higher) | mechanism | BETTER |
| K-B3 Fields | main | `worker_losses_pre_overrun` (lower) | secondary | not WORSE |
| K-B4 Army kept | main | `army_supply_ratio_at` 300 s (higher) | secondary | not WORSE |
| K-G1 Skai mirror identical | mirrors | every `skai_vs_skai` pair: both games same `state_hash` and `play_signature` | guard | 24 of 24 |
| K-G2 Water mirror | mirrors | `won` (higher), Water-mirror pairs | guard | not WORSE |
| K-G3 Response suite | response | no scenario that passes for `aggressive_s5` fails for `aggressive_s5_kite` | guard | holds |
| K-G4 Clean commands | traces | no candidate trace has a rejection reason, or a larger count of one, than the baseline trace of the same matchup (`site_occupied` excepted) | guard | holds |
| K-G5 Determinism, suite, pins | main, suite | spot check 0 mismatches; full suite green on the measured commit; no pin moved | guard | holds |
| K-G6 Predictor unchanged on un-kited fights | predict | every `predict_kite` line with `"kite_pair":false` equals the `predict_off` line of the same fight, with the field removed | guard | holds |

**Verdict.**
- **PASS**: K-B1 and K-B2 BETTER, K-B3 and K-B4 not WORSE, K-G1 to K-G6 hold.
- **NO_MEASURABLE_EFFECT**: K-B2 BETTER, K-B1 not significant, nothing WORSE, guards hold.
- **FAIL**: K-B2 not BETTER, or anything WORSE, or a guard fails.

On PASS the developer decides whether the key goes into `aggressive_s5`; that would move the `aggressive_s5` pin once. On any other verdict `aggressive_s5_kite` stays a candidate. Nothing is retuned: a new factor is a new revision with its bars restated.

### 5.3 Stated power and minimum detectable effect (before the run)

The exact two-sided sign test at 0.05 needs, for D decisive pairs:

| D | better needed | D | better needed |
| --- | --- | --- | --- |
| 5 or fewer | cannot pass | 15 | 12 |
| 6 | 6 | 20 | 15 |
| 8 | 8 | 24 | 18 |
| 10 | 9 | 48 | 32 |
| 12 | 10 | 72 | 45 |

**K-B1 (wins).** Water almost never wins this matchup: baseline Water 0 of 72 in RR-B on 20261117, where `aggressive_s5_rr` equals the new `aggressive_s5`. So D is roughly the number of pairs in which only one side's Water wins. With 72 pairs, let a be the share of pairs where only the candidate's Water wins and b the share where only the baseline's does. Power (exact enumeration over the 72 pairs):

| a (candidate-only wins) | b (baseline-only wins) | power |
| --- | --- | --- |
| 0.056 (4 of 72) | 0 | 0.22 |
| 0.083 (6 of 72) | 0 | 0.56 |
| 0.111 (8 of 72) | 0 | 0.82 |
| 0.111 | 0.014 (1 of 72) | 0.56 |
| 0.139 (10 of 72) | 0.014 | 0.76 |
| 0.167 (12 of 72) | 0.028 (2 of 72) | 0.74 |

**Minimum detectable effect at power 0.8: the candidate's Water must win in about 8 of 72 pairs (11 percent) that the baseline's Water loses, with none the other way.** The reef spec's T4 hint (4 of 36 = 11 percent, not significant) sits at that edge.

**K-B2.** RR-B saw 72 of 72 for a larger change. Here both profiles carry the weights, so D is smaller and the per-pair effect weaker; section 6 gives the trial share. **Guards:** 24 Water-mirror pairs see only a large regression (about 3 of 4 decisive pairs worse), as in the reef spec.

### 5.4 Reported, not judged

From the Water slot-games of the main run, for both profiles:
- medians of trained units per unit type, with the Pummeler (`water.grasper`) and Reef Runner shares of trained army units;
- worker losses before the overrun, the overrun second, army supply at 180, 300 and 450 s (own and Skai's), the game end tick;
- candidate-Water wins of 72 and baseline-Water wins of 72;
- the unit mix per Water opening, to show that the openings stay distinct and the Pummeler is still fielded.

Also reported: the K-H accuracy split (overall, on kite-pair records and on the rest); both response files side by side; from the traces, the army at 180 and 300 s.

### 5.5 Expected result (stated before the run)

SEE 6.

## 6. Every trial the designer ran (worktree at `c7f2b37`, Release, uncommitted scratch edits, all reverted)

**Scratch instrument.** A thread-static, environment-gated version of the 3.1 term in `FightPredictor.TimeToKill` step 2. It was applied in `AiCounter.PredictorMix` only for a trial profile `aggressive_s5_k` (a copy of `aggressive_s5` with a new ID), Water plan only, using the profile's kite keys. In `PredictEvaluator` it was applied to both seats with gap 2 and ratio 0.8.

### 6.1 Predictor accuracy (`--predict-fights --from rts/docs/reports/fights_2026-11-18/fights.jsonl --profile aggressive_s5`)

| Run | Factor | PREDICT line (accuracy, correct / decided) | Records changed (all hold a kite pair) | Unchanged records byte-identical | Correct on changed decided records (off → on) |
| --- | --- | --- | --- | --- | --- |
| P1 | off | 0.735, 562 / 765 (reproduces the report) | | | |
| P2 | 0.4 | 0.610, 467 / 765 | 210 | 570 of 570 | 174 → 79 of 208 |
| P3 | 0.5 | 0.661, 506 / 765 | 210 | 570 of 570 | 174 → 118 of 208 |

The flips are the pairs expected from 1.2 (Pummeler, Shellback and `water_ai_mix` against Windrunner and Talon; Water-mirror pairs in which the Reef Runner kites the Pummeler or Shellback). They also include the Shellback pairs. Measured under the kite instrument, the Shellback still wins kited (`roster_numbers_1.md` 1.2: +0.89) because of its dome, so for the Shellback the term is wrong. See Q-2.

### 6.2 Games (`--behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_k --maps prototype_1v1 --matchups skai_vs_water --games 72 --seed 31337 --paired-faction water --jobs 4`, 36 cross pairs)

PENDING.

**Values tried:** factor 0.4 (calibrated, 2.5) and 0.5 (the reef spec's value). No other value was run. **Seeds:** only base seed 31337 for games, and only matchup `skai_vs_water`. The builder's bar seed 20261201, both mirrors and the response suite on the candidate were not run by the designer.

## 7. Build order

1. **K-1** (one Sonnet session): section 3. Then a `sim-reviewer` (Opus) determinism review of the `rts/src/Ai` diff before merge.
2. **K-H** (one Sonnet session, can run in parallel with K-1 once `Kites` exists; else after K-1): section 4. Tools only.
3. **K-B** (one Sonnet session after both are merged): section 5. Report in `rts/docs/reports/ai_kite_correction/README.md` with:
   - the commands, the header line, and every bar row with D, p and verdict;
   - the 5.4 items and the four traces;
   - `main/`, `mirrors/`, both response files, both predict folders, `suite_chunks.md` and the caches.

   Judge from the outputs before touching anything. Nothing is tuned.

Each package pushes after every commit. Changelog bullets go to the coordinator, who also updates STATUS.

## 8. Out of scope

- Any Sim, unit, combat, economy or map number; any map but `prototype_1v1`.
- The fight decisions (attack, defend, retreat, harass) and the old counter mix (`Score`), which keep the uncorrected predictor (Q-4).
- The lane simulation (`predictor_model` sim).
- A location- or terrain-aware kite term (Q-3).
- A dome exemption (Q-2).
- A kited fight mode in the harness (Q-5).
- The kite rule itself and its keys; Skai's AI; the openings and compositions.

## Questions for the developer (builders use the defaults)

| # | Question | Default |
| --- | --- | --- |
| Q-1 | Accept a faction-plan key (Water plan only in the candidate) instead of a root key, so that the candidate's Skai plays exactly like `aggressive_s5`'s? | Yes. |
| Q-2 | The term also marks the Shellback as kited, but it still wins kited fights through its dome (1.2: +0.89). Exempt units with a shield dome from the term? | No, not in this revision: the predictor believes the kite rule as written. If K-B shows the Shellback's share collapsing, an exemption is a new revision. |
| Q-3 | Base speed ignores flood water, where a Water unit cannot be kited by a ground unit, so the term is too pessimistic for Water there. Add a location-aware version later? | Not now: the mix has no fight location. Revisit only if the candidate passes and Water's flood-water play suffers. |
| Q-4 | Also apply the term to the fight decisions (attack, retreat)? | No, a separate step after K-B, with its own bars. |
| Q-5 | Add an opt-in kite control to the fight mode (`roster_numbers_1.md` Q-2), so the predictor can be scored against kited fights? | Yes, as a separate harness spec; K-G6 stands until then. |
| Q-6 | Accept that the win bar can detect only about 8 of 72 extra Water wins (power 0.8), and that NO_MEASURABLE_EFFECT is the likely verdict at this sample? | Yes. |
| Q-7 | Factor from the square-law calibration (0.4) or the earlier 0.5? | See 2.5 (the value written there). |

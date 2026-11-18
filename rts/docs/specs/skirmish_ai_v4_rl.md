<!-- rts/docs/specs/skirmish_ai_v4_rl.md: build spec for packages R (seeded randomness) and L (opening library) of the skirmish AI, P-005 part 2 option R and L, so that ladder games differ from seed to seed without the AI getting weaker. Replaces the R+L outline of skirmish_ai_v4.md; also says how package F's options are re-measured once R is in. Part of rts/docs/specs (index: README.md). -->
# Spec: Skirmish AI v4, packages R and L (seeded variety and an opening library)

## 0. Goal, sources and ground rules

**Why now.** Package S (on `main`, 18 Nov 2026) measured that `aggressive_s3` against itself on `prototype_1v1` gives **20 distinct games of 216** (8 in `skai_vs_skai`, 7 in `skai_vs_water`, 5 in `water_vs_water`; distinct = different `play_signature`, the hash of every AI command, skirmish_ai_v4.md S.1; source `CHANGELOG.md` package S entry and `STATUS.md`). Ladder win rates therefore rest on a handful of games, and P-005 bar item 3 ("at least 80 percent distinct games per cell") fails by a factor of ten. The developer's goal: "We dove into the cpu ai in the first place as a way to test balance, and if it's not good, or able to compete with a human, we won't get proper balancing data." The developer's P-005 order (section 2.4) puts R+L before ability use (package A). One strong AI, no difficulty levels. AI evaluation runs only on `prototype_1v1` (`ai_eval.json` `ladder_maps`).

**Goal metric (all three must hold; section E).**
1. **Variety.** On the self-ladder of the new profile (`--ladder --profile-a aggressive_s4r --profile-b aggressive_s4r`, 72 games per cell, `ai_eval.json` defaults), **every cell** has `distinct / decided >= min_distinct_fraction` (0.8, `tuning/harness_batch.json`), i.e. no S.2 `WARNING` line.
2. **Not weaker.** On `--ladder --profile-a aggressive_s3 --profile-b aggressive_s4r --non-regression`: the LADDER line passes (candidate fraction at least `ladder_non_regression_fraction_min` 0.45 over all decided games), **and** in the S.3 `mirror` row the Wilson interval over distinct games has its upper bound at or above `ladder_mirror_upper_min` (0.50, new key, placeholder), i.e. the data cannot show at z = 1.96 that the randomised AI loses mirrors to the deterministic one. The cross-faction cells are left out of item 2 because they follow the faction (R-3: Skai won all 72), so they sit near 0.5 whatever either profile does.
3. **No dominant and no dud opening** (P-005 option L metrics). On the self-ladder of item 1: no opening is drawn in more than `opening_max_use_fraction` (0.40) of its faction's draws, and no opening's mirror win fraction against other openings is above `opening_max_mirror_win_fraction` (0.65). On both ladders: no opening with at least `opening_min_games` (10) decided mirror games against other openings is below `opening_min_mirror_win_fraction` (0.35, placeholder; on the strength ladder that is "against `aggressive_s3`").

Why item 2 is needed: a play signature differs as soon as one command differs, so any jitter (even a random build tile) reaches item 1 by itself. Item 2 shows the variety did not cost strength, and item 3 shows it is spread over real alternatives.

**Sources read.** `CLAUDE.md`, `STATUS.md`, `proposals/P-005-roster-holes-and-ai-for-balance.md` (options R and L, the bar, both decision tables), `specs/skirmish_ai_v4.md` (format; F, S, A, the R+L outline), `reports/ai_v4_f/README.md` (on branch `review/ai-v4-f`, commit `5b7987b`; it is not on `main`), `rts/src/Ai/*.cs`, `rts/data/ai/aggressive_s3.json`, every file under `rts/data/units`, `rts/tools/BalanceHarness/{GameRunner,LadderRunner,SeedDerivation,GameRecord,HarnessStats}.cs`, `scripts/ai_eval/ai_eval.json`, `tuning/harness_batch.json`, `tuning/economy.json`, `maps/prototype_1v1.json` (fields), `rts/src/Sim/Data/DataLoader.cs`, `rts/src/Sim/Core/DeterministicRandom.cs`, the pinned-hash tests. Written against `main` at `c6515ca`. Nothing was run for this spec.

### 0.1 Current behaviour (verified in the code)

| Claim | Where |
| --- | --- |
| The per-game seed is derived per (base seed, map, matchup, game index): FNV-1a of `"<map>/<matchup>"` xor base seed xor game index, then the SplitMix64 finaliser. Every game of a cell therefore already has its own seed. | `SeedDerivation.GameSeed` |
| The seed becomes the match seed: `data.CreateMatchSettings(seed)` in the `SimulationConfig`; `AiHost.StartAll` then calls each controller's `OnMatchStart`. | `GameRunner.Play` |
| Each AI seeds its own `DeterministicRandom` (PCG32) with `matchSeed ^ (0x9E3779B97F4A7C15 * (player + 1))`, so the two seats already have different streams. It never touches `sim.Rng`. | `SkirmishAiController.SeedFor`, `OnMatchStart`, file header |
| The AI's only draw: `EnterMassing` sets `AttackThreshold = attack_army_supply + rng.NextInt(attack_supply_jitter + 1)`, on the first think (`AiBrain.Decide`) and when an attack ends (three calls in `Attack`, one in `Army` after Retreat). | `AiBrain.EnterMassing`; grep of `rng`/`_rng` in `rts/src/Ai` |
| Under `use_predictor_attack` (on in `aggressive_s3`) the drawn threshold decides only when nothing is known about the enemy at the target; otherwise `PredictedAttack` decides: `AWins && Advantage >= attack_min_advantage && armySupply >= attack_min_supply`, or `armySupply >= attack_force_supply`. | `AiBrain.Mass`, `AiBrainFight.PredictedAttack` |
| The only Sim draw is the cycle pick when no valid pick exists at lock time. | `CycleSystem.cs:486` |
| Build order: strict, "keep at least `count` once supply used >= `at_supply_used`"; only the first unmet step is tried. Site: first good tile on the HQ rings. | `Turn.Structures`, `TryBuild`, `TryFindSite` |
| Army mix: `plan.Composition` directly, or with `use_counter_mix` `AiCounter.Mix` / `PredictorMix`, which blend `(1 - k) x` base composition share with `k x` counter share, `k = counter_weight x min(1, known or sighted supply / counter_full_confidence_supply)`; with nothing known the result is the base composition scaled to `mix_resolution`. | `Turn` constructor (`_composition = ...`), `AiCounter.Mix` (doc comment lines 117 to 118), `PredictorMix` step 6 |
| Expansion clock: `dueSeconds = expand_at_seconds + expand_interval_seconds x DepotsPlaced`, lowered to `early_expansion_seconds` while `use_early_expansion` and no rare field is served (0 in `aggressive_s3`, so the first rare depot is due at once). | `Turn.Expand` |
| `rts/data/ai` is flat: every file there is loaded as a profile (`CheckDepth(path, parts, 2, "ai/<id>.json")`) and several tests parse every loaded profile. A second kind of file in that folder, or a subfolder, needs a `DataLoader` change, which is in `rts/src/Sim`. | `DataLoader.Load` (case `AiFolder`); `SkirmishAiTests.Loader_RoutesAiFolder_AndShippedProfilesValidate`, `AiFightTests.DProfiles_SwitchesAndValidation`, `AiArmyMixTests.C15_ProfilesRespectTheBaseCap` |
| Ladder seats: game g puts the first-named faction in slot 0 on even g; profile A plays slot s when `(g / 2) % 2 == s`. In a self-ladder all 72 games of a mirror cell are the same set-up and differ only by seed. | `GameRunner.FactionInSlot`, `LadderProfileInSlot` |

So: the harness already varies the seed per game and already gives each seat its own stream. Games barely differ because the AI barely draws. **Package R makes the AI draw at the points where a good player would vary; package L gives those draws something worth choosing between.**

### 0.2 Ground rules (both packages)

- **No change to `rts/src/Sim`.** No new command, event, Sim state or hash field; no shipped-data `STATE_HASH` changes; replays and lockstep are unaffected (replays record commands; in lockstep the owning peer runs the AI and its commands travel like a player's, CLAUDE.md rule 2). If a builder finds a Sim change unavoidable, stop and report.
- The AI stays a pure function of `AiWorldView` + `AiProfile` + `AiMemory` + its own generators. A (seed, profiles, map, matchup, game) tuple replays bit-identically. AI memory is not hashed; determinism is proven by harness hashes, play signatures and "same inputs twice" tests.
- **Old behaviour is untouched.** All new draws come from a **second generator** (the variety stream, R.1), never from the existing `SkirmishAiController.Rng`. A profile whose new keys are off (all jitters 0, every `openings` list empty) creates no variety stream and plays exactly as on `c6515ca`.
- Fix64 and integers only; draws happen at fixed points in a fixed order (R.2); every list walked in file order or ordinal ID order as stated; ties as stated.
- The brain never names a faction or unit ID; IDs appear only in profile data.
- Every number is a profile key, an `ai_eval.json` key or a `harness_batch.json` key, commented in the house format `// <what>. Unit: . Range: . Source: skirmish_ai_v4_rl.md <section>; placeholder.`; every new value is a **placeholder**.
- `AiProfile.Parse` reads every new key with the `Require*` helpers and adds it to `RejectUnknownKeys`; **every profile file under `rts/data/ai` gets every new key** with the "old" value of the tables below. Hand-written profile JSON in tests gets them too. Update the `AiProfile.cs` "Differences" list, the `AiBrain.cs` header (the rule list and its determinism line "the only random draw is ..."), the `SkirmishAiController.cs` header and `rts/src/Ai/README.md`.

### 0.3 Profiles

| File | What it is | Made in |
| --- | --- | --- |
| every existing profile | new root keys at 0, `"openings": []` in each faction plan | R-1, L-1 |
| `aggressive_s4r.json` | copy of `aggressive_s3.json`, `id` changed, R keys at the `aggressive_s4r` values (R-1), openings of section L.5 (L-2) | R-1, L-2 |

`aggressive_s4r` builds on `aggressive_s3` because F is not merged and A is not built. When either lands first, its profile gains the R and L keys the same way; the keys are independent of F's and A's.

---

## Package R: seeded variety

### R.1 The variety stream

- New class `AiVariety` (`rts/src/Ai/AiVariety.cs`), held by `AiMemory.Variety` (`AiVariety?`, null by default; so tests that build an `AiMemory` by hand get no variety).
- Seed: `AiVariety.SeedFor(ulong matchSeed, PlayerId player) = SplitMix64(SkirmishAiController.SeedFor(matchSeed, player) ^ VarietySalt)`, where `SplitMix64` is the finaliser written out in `SeedDerivation.GameSeed` (add `z + 0x9E3779B97F4A7C15`, then the two xor-shift-multiply rounds and the final xor-shift; copy it into `AiVariety`, Rts.Ai cannot reference the harness) and `VarietySalt` is the fixed constant `0x5641524945545931` (ASCII "VARIETY1"; an algorithm constant like `SeedMultiplier`, not tuning). The finaliser decorrelates the variety stream from the existing stream although both start from the same per-player seed.
- **How the per-game seed reaches each AI:** `SeedDerivation.GameSeed` → `GameRunner.Play` → `MatchSettings.Seed` → `SkirmishAiController.OnMatchStart(sim)` reads `sim.Settings.Seed` → old stream `SeedFor(seed, player)` (unchanged) and, when the profile's plan needs one (below), `Memory.Variety = AiVariety.Create(profile, plan, AiVariety.SeedFor(seed, player), ticksPerSecond)`. The two seats get different `SeedFor` values (the player multiplier), hence **independent streams**.
- Created only when the plan has at least one opening, or `composition_jitter_fraction > 0`, or `expand_jitter_seconds > 0`, or `attack_advantage_jitter > 0`. Otherwise `Memory.Variety` stays null.
- `OnMatchStart` already runs again after a lockstep reconnect (`LockstepSession` test harness) and reseeds the old stream; it rebuilds the variety object from the same seed, so the opening and the match-start values come out the same. (Per-attack draws restart, exactly as the old stream's jitter does today; not changed here.)

### R.2 Draws, in this fixed order

At match start, inside `AiVariety.Create`, all from the variety stream:

1. **Opening** (only when the plan's `openings` list is non-empty): `r = NextInt(totalWeight)`; the chosen opening is the first in file order whose running weight sum exceeds `r`. One draw.
2. **Opening composition band** (only when an opening was drawn and `composition_jitter_fraction > 0`): `J = floor(composition_jitter_fraction x 100)`; for each unit ID of the opening's `composition` with weight above 0, in ordinal ID order: `d = NextIntInclusive(-J, J)`, weight becomes `Jitter(w, d)`.
3. **Main composition band** (only when `composition_jitter_fraction > 0`): the same over the plan's `composition`.
4. **Expansion offset** (only when `expand_jitter_seconds > 0`): `ExpandOffsetSeconds = NextIntInclusive(-expand_jitter_seconds, expand_jitter_seconds)`.

During the match, in `AiBrain.EnterMassing` (same call sites as today, after the existing threshold draw from the old stream):

5. **Attack advantage offset** (only when `Variety != null` and `attack_advantage_jitter > 0`): `A = round(attack_advantage_jitter x 100)` (built, 18 Nov 2026: `floor` was wrong because 0.15 is 0.1499... in Q32.32 and would give 14; the same rounding is used for `J` in draws 2 and 3); `AttackAdvantageOffset = Fix64.FromInt(NextIntInclusive(-A, A)) / 100`.

`Jitter(int w, int d)` (public static, pure): `w == 0` gives 0; else `clamp((w x (100 + d) + 50) / 100, 1, 100)` in integers. So a band never adds a unit the data excluded, never removes one it included, and stays inside the composition range 0 to 100.

### R.3 Where the drawn values are used (and how each is bounded)

| Source | Used in | Bound that keeps it from picking a clearly bad option |
| --- | --- | --- |
| Opening (package L) | the plan the brain sees (L.3) | Openings are authored data; each must pass goal item 3 against `aggressive_s3` and the other openings, else its weight goes to 0 (section E). |
| Composition band | the base composition: `plan.Composition` (directly, or as the `(1 - k)` share of `AiCounter.Mix` / `PredictorMix`) | Only the base share moves, by at most `composition_jitter_fraction` of each weight; the counter share (what the AI answers after scouting) is untouched, so a seen threat is answered as before. |
| Expansion offset | effective `expand_at_seconds` and `early_expansion_seconds` in `Turn.Expand`: value + offset, clamped to 0 to 1800 | At most `expand_jitter_seconds` around the authored timing; every other gate of rule E (no threat, depot caps, safety) is unchanged. |
| Attack advantage offset | effective `attack_min_advantage` in `PredictedAttack`: `clamp(attack_min_advantage + offset, 0, 100)` | `AWins` is still required, so the AI never attacks into a predicted loss; it only varies how large a predicted edge it waits for. `attack_force_supply` is unchanged. |
| Existing threshold jitter | unchanged (old stream) | unchanged |

Implementation shape (illustrative): small `Turn` helpers `ExpandAtSeconds()`, `EarlyExpansionSeconds()`, `AttackMinAdvantage()`, `AttackMinSupply()` and an `EnterMassing(memory, profile, rng, tick)` that reads the attack supply through the opening (L.3); each returns the profile value when `Memory.Variety` is null. `EnterMassing` gains the `tick` parameter; update its five callers (no test calls it; checked by grep).

### R.4 Profile keys (root of every profile)

| Key | Unit | Range | Old profiles | `aggressive_s4r` |
| --- | --- | --- | --- | --- |
| `composition_jitter_fraction` | fraction | 0 to 0.5 | 0 | 0.25 |
| `expand_jitter_seconds` | seconds (integer) | 0 to 120 | 0 | 20 |
| `attack_advantage_jitter` | ratio | 0 to 0.5 | 0 | 0.15 |
| `attack_supply_jitter` (exists) | supply | 0 to 40 | unchanged | 2 (as `aggressive_s3`) |

With `attack_min_advantage` 1.3 the AI attacks on a predicted edge of 1.15 to 1.45. Validation: none beyond the ranges.

### R.5 Tests (`rts/tests/Sim.Tests/Ai/AiVarietyTests.cs`; harness tests in `Harness/StatisticsTests.cs`)

- **R-0 Pin first** (first commit of R-1, on unchanged code): `aggressive_s3` against itself, `--trace --map prototype_1v1 --matchup skai_vs_skai --seed 20261117 --game 0`, same form as `AblationBlind_TracedGame_IsUnchangedByPackageC2`: record `end_tick`, `state_hash` and `play_signature` as constants `S3SelfTraceEndTick`, `S3SelfTraceHash`, `S3SelfTraceSignature`. The test must still pass, unchanged, after R and L.
- **R-1 Jitter arithmetic.** `Jitter(3, -25) == 2`, `Jitter(3, 25) == 4`, `Jitter(1, -25) == 1`, `Jitter(100, 25) == 100`, `Jitter(0, 25) == 0`, `Jitter(4, 0) == 4`.
- **R-2 Seeds.** `AiVariety.SeedFor(s, P0) != AiVariety.SeedFor(s, P1)` and `AiVariety.SeedFor(s, P) != SkirmishAiController.SeedFor(s, P)` for s in 1..32.
- **R-3 Create is deterministic.** `AiVariety.Create(aggressive_s4r, skai plan, seed 7, 20)` twice: same opening ID, same composition weights, same expansion offset. Builder records the values for seeds 1 to 8 (both factions) as a pin in the test. (Built: the pin is taken on `aggressive_s3` text with the three R keys switched on, so it does not move when L-2 adds openings to `aggressive_s4r`. Measured: the base weights are 1 to 3, so the 25 percent band moves them by at most one step; the expansion offsets of seeds 1 to 8 are -4, -20, -4, 11, 10, -8, 5, 20. With `early_expansion_seconds` 0 every negative offset clamps to 0, so half the draws do not move the first rare depot.)
- **R-4 Bounds.** For seeds 1 to 200: every expansion offset is in -20..20; every jittered weight `w'` of a base weight `w > 0` satisfies `max(1, floor(0.75 w)) <= w' <= min(100, ceil(1.25 w))`; 50 consecutive `EnterMassing` calls give offsets in -0.15..0.15 with a step of 0.01.
- **R-5 Old stream untouched.** Two controllers on the same started greybox simulation, slot 0, one with `aggressive_s3`, one with `aggressive_s4r`: after `OnMatchStart` and one think each, `controller.Rng.State` and `controller.Rng.DrawCount` are equal (both drew only the threshold jitter).
- **R-6 No variety for old profiles.** For every shipped profile except `aggressive_s4r`: after `OnMatchStart`, `Memory.Variety` is null.
- **R-7 Effective values.** With `Memory.Variety` set by hand (`ExpandOffsetSeconds = -20`, `AttackAdvantageOffset = 0.15`): `Turn.ExpandAtSeconds()` is 220 for `expand_at_seconds` 240 and 0 for 10 (clamp); `AttackMinAdvantage()` is 1.45 for 1.3. With `Variety` null: 240 and 1.3.
- **R-8 Same seed twice.** `GameRunner.Play` on `prototype_1v1`, `water_vs_water`, game 0, cap 300 s, both slots `aggressive_s4r`, seed 11 twice: equal `play_signature` and equal `state_hash`.
- **R-9 Seeds change play.** Same game for seeds 1 to 8: 6 different `play_signature` values (measured on R-1, before any opening exists: the spec guessed at least 7; pinned as 6; re-measured at L-2).
- **R-10 Pins.** `V2ReferenceHash` (A-11), `BlindTraceHash` (C2-11), R-0, the harness smoke pins (`HarnessUnitTests`: smoke `c423332711029434`, economy_smoke `ce62b175ea751ba6`), `WorkerConstructionTests` (economy_smoke) and `DeterminismTests.GoldenFinalHash` stay green **without re-pinning**. If one moves, stop and report.

---

## Package L: opening library

### L.1 Where openings live and why

Openings are a list in each faction plan of a profile: `factions.<faction>.openings`. Not separate files: `rts/data/ai` is flat and every file there is parsed as a profile (0.1), so an `ai/openings/` folder would need a `DataLoader` change in `rts/src/Sim` (rejected extras; Q-11). The cost is that a profile that wants openings carries them; only `aggressive_s4r` does.

### L.2 Schema (`AiOpening`, parsed in `AiProfile.ParseFaction`)

`openings`: array, 0 to 16 entries, required in every faction plan (`[]` = no openings). When non-empty, the sum of `weight` must be above 0. Each entry, all keys required, unknown keys rejected:

| Key | Type, unit | Range | Meaning |
| --- | --- | --- | --- |
| `id` | text ID | lower-case letters, digits, underscores; 1 to 48 characters; unique within the plan | Name in reports and records. |
| `weight` | integer weight | 0 to 100 | Chance of being drawn = weight / sum of weights. 0 keeps an opening in the file without drawing it (used to switch one off or to force one in a test copy). |
| `build_order` | array of steps | 1 to 20 steps; each step exactly the schema and checks of the plan's `build_order` (`structure`, `count` 1 to 20, `at_supply_used` 0 to 200) | Replaces the plan's build order **for the whole game** (it is a list of minimum counts; once met, rules supply, T, T2, SD, E and X run as today). |
| `composition` | object unit ID to weight | as the plan's `composition` (army unit of the faction, trained by a faction structure, 0 to 100, sum above 0) | The base composition **until handover**. |
| `handover_seconds` | seconds | 0 to 3600 | Clock time at which the opening's composition and timing values give way to the plan's. |
| `expand_at_seconds` | seconds | 0 to 1800 | Replaces the root key until handover. |
| `early_expansion_seconds` | seconds | 0 to 1800 | Replaces the root key until handover (used only when `use_early_expansion`). |
| `attack_army_supply` | supply | 1 to 200 | Replaces the root key in `EnterMassing` until handover (the fallback threshold when nothing is known). |
| `attack_min_supply` | supply | 1 to 200 | Replaces the root key in `PredictedAttack` until handover. |

Extra validation, error naming the file and `factions.<f>.openings[<i>].<key>`:
- **Requirements are buildable:** for every `composition` unit with weight above 0 and a non-empty `requires` list, every required structure appears in that opening's `build_order` (the Thunderhead needs `skai.storm_spire`, the Leviathan `water.reef_workshop`). This keeps a gated unit from stalling the opening when `use_tech_on_demand` is off.
- A `build_order` step's structure must have its own `requires` met by an earlier step of the same build order (latent today: no structure has `requires`).

### L.3 Choice and handover

- **Choice:** at match start, draw 1 of R.2, by weight only. The choice is blind, as a human's is at tick 0.
- **Reaction to scouting needs no new rule:** the opening's composition is the base composition that the counter-mix blends with (0.1). As the enemy model fills, `k` rises to `counter_weight` (0.7) and production moves to the counter share; rule T builds the counter's producer. A Windrunner rush that scouts Roc Riders therefore turns toward anti-air within one confidence ramp. A re-pick of the opening after the first scout is a rejected extra (Q-3).
- **Plans:** `AiVariety.Create` builds two `AiFactionPlan`s with every field copied from the plan except: `OpeningPlan` = opening `build_order` + jittered opening `composition`; `MainPlan` = opening `build_order` + jittered plan `composition`. With no opening: both are the plan with its jittered composition. `HandoverTick = SecondsToTicks(handover_seconds)` (0 with no opening).
- **Where it runs:** first statement of `AiBrain.Decide`: `plan = memory.Variety?.PlanAt(view.Tick) ?? plan` (`OpeningPlan` while `tick < HandoverTick`, else `MainPlan`). Everything after reads that plan, so rules 3 (build order), 3b/T2 (via `_composition`), 5 (Produce), 5b and `ProducerStructureFor` all see the opening without further edits. The four timing helpers of R.3 return the opening's value while `tick < HandoverTick`, else the root key, then add the R offsets.
- `SkirmishAiController.OpeningId` (string, "" when none) exposes the drawn ID for the harness and for traces.

### L.4 Handover in one line per rule

Build order: the opening's, whole game. Composition and the four timing keys: opening's until `handover_seconds`, then the plan's root and plan values. Everything else (supply, tech, static defence, expansion caps and safety, scouting, army stance, defence, unit control) is never touched by an opening.

### L.5 Starting openings (`aggressive_s4r`; every number a **placeholder**)

Only units and structures in `rts/data/units` today. Facts used (unit files, `tuning/economy.json`): start 200 common, 0 rare, 4 workers, base supply 10. Skai: Roost 150 c, +8 supply, trains Windrunner (75 c) and Roc Rider (150/50); Windspire 150 c, +8 supply, trains Talon Marksman (`skai.talon_archer`, 100 c), Stormcaller (150/40) and Thunderhead (200/65, requires Storm Spire); Storm Spire 150/50, requires nothing. Water: Tidepool 150 c, +8, trains Spoutling (100/25), Pummeler (`water.grasper`, 100 c), Reef Runner (75 c), Tidecaller; Shell Forge 150 c, +8, trains Shellback (175 c), Brinecaster (`water.ink_lobber`, 150/40) and Leviathan (300/80, requires Reef Workshop); Reef Workshop 150/50. Rare comes only from a rare field, so rare-cost openings expand to rare at once (`early_expansion_seconds` 0). The Tidecaller and Downdraft are left out: the AI cannot use them until package A, which can add openings for them afterwards. The intended timings are targets, **not measured**; L-M measures them.

Columns: build order as `structure x count @ at_supply_used`; composition weights; `handover` / `expand_at` / `early_expansion` / `attack_army_supply` / `attack_min_supply` (seconds, seconds, seconds, supply, supply). IDs are written without the faction prefix; the file uses the full IDs (`skai.roost`, `water.grasper` and so on).

**Skai**

| ID | Weight | Build order | Composition | Values | Intent and timings |
| --- | --- | --- | --- | --- | --- |
| `skai_standard` | 30 | roost x1 @0, roost x2 @6, windspire x1 @10, roost x3 @14 | windrunner 3, talon_archer 2, roc_rider 1, stormcaller 1 | 300 / 240 / 0 / 8 / 14 | Today's `aggressive_s3` plan, so the proven line stays in the pool. |
| `skai_windrunner_rush` | 20 | roost x1 @0, roost x2 @5, roost x3 @8, windspire x1 @16 | windrunner 6, talon_archer 1 | 300 / 300 / 240 / 6 / 8 | One-base pressure: three Roosts by about 100 s, first attack with 6 to 8 supply of Windrunners at about 150 to 200 s, rare depot not before 240 s. |
| `skai_fast_expand` | 25 | roost x1 @0, windspire x1 @10, roost x2 @12 | talon_archer 3, windrunner 2, roc_rider 1 | 420 / 120 / 0 / 14 / 20 | Economy first: rare depot at once, second depot due at 120 + 150 = 270 s; first attack at 20 supply around 360 s. |
| `skai_air` | 25 | roost x1 @0, roost x2 @6, storm_spire x1 @12, windspire x1 @14 | roc_rider 3, thunderhead 1, windrunner 1 | 420 / 240 / 0 / 12 / 16 | Air tech: rare depot at once, first Roc Rider once rare arrives, Storm Spire about 240 s, first Thunderhead about 300 s, attack at 16 supply. |

**Water**

| ID | Weight | Build order | Composition | Values | Intent and timings |
| --- | --- | --- | --- | --- | --- |
| `water_standard` | 30 | tidepool x1 @0, tidepool x2 @6, shell_forge x1 @10, tidepool x3 @14 | grasper 3, shellback 1, ink_lobber 1, spoutling 1 | 300 / 240 / 0 / 8 / 14 | Today's `aggressive_s3` plan. |
| `water_grasper_rush` | 20 | tidepool x1 @0, tidepool x2 @5, tidepool x3 @8, shell_forge x1 @16 | grasper 6, reef_runner 1 | 300 / 300 / 240 / 6 / 8 | Pummeler pressure from three Tidepools, first attack at about 150 to 200 s, rare depot not before 240 s. |
| `water_fast_expand` | 25 | tidepool x1 @0, shell_forge x1 @10, tidepool x2 @12 | grasper 2, shellback 2, ink_lobber 1, spoutling 1 | 420 / 120 / 0 / 14 / 20 | Economy first, second depot due at 270 s, first attack at 20 supply around 360 s. |
| `water_leviathan_tech` | 25 | tidepool x1 @0, shell_forge x1 @6, reef_workshop x1 @12, tidepool x2 @16 | ink_lobber 2, shellback 2, leviathan 1, spoutling 1 | 480 / 240 / 0 / 12 / 16 | Siege tech: rare at once, Reef Workshop about 240 s, first Leviathan (60 s train) about 330 s. |

Largest weight 30 of 100, so the expected largest use share (0.30) is below the 0.40 limit. In every opening of this table the root and opening values satisfy the existing profile validation.

### L.6 Tests (`AiOpeningTests.cs`, greybox simulations as in `AiFightTests`, profile text edits as `AiFightTests.S3Edited`)

- **L-1 Parse.** Every shipped profile parses; `aggressive_s4r` has 4 openings per faction with the IDs and weights of L.5; every other profile has `[]`.
- **L-2 Validation errors** (each names the file and the field path): unknown key in an opening; duplicate `id`; weights all 0; `composition` with `skai.thunderhead` weight 1 and no `skai.storm_spire` step (message names `requires`); a step with a non-structure; `handover_seconds` 3601.
- **L-3 Weights.** An edited `aggressive_s4r` with weights 0, 0, 100, 0 (skai): for seeds 1 to 50 the drawn opening is always the third. With 30/20/25/25: over seeds 1 to 400 every opening is drawn and each share is within 0.10 of its weight share (deterministic; the builder pins the counts).
- **L-4 Opening build order.** Fresh greybox start, edited opening `build_order` = windspire x1 @0 only, weight 100: the first think emits `Build` of `skai.windspire`; with `aggressive_s3` it emits `Build` of `skai.roost`.
- **L-5 Handover of the composition.** Edited opening composition `roc_rider 1` only (and `windrunner 0`), plan composition `windrunner 1` only, the counter-mix switches off in the copy (`use_counter_mix`, `use_tech_on_demand`, `use_rare_priority`, `use_predictor_mix` false, since validation ties the last three to the first), jitter 0, handover 60 s; a complete free Roost, money and rare in stock: Decide at tick `60 s - 1` emits `Train(roost, skai.roc_rider)`; at tick `60 s` emits `Train(roost, skai.windrunner)`.
- **L-6 Timing values.** Opening `early_expansion_seconds` 240 and `expand_at_seconds` 300, handover 300, root `early_expansion_seconds` 0 (`use_early_expansion` true), expansion jitter 0, no rare field served, depot affordable: no depot `Build` at 239 s; a depot `Build` at 240 s. With handover 100: a depot `Build` at 100 s (root value 0 applies).
- **L-7 Attack values.** Opening `attack_army_supply` 6, handover 300: the first `EnterMassing` sets `AttackThreshold` in 6..8 (`attack_supply_jitter` 2); after a forced `EnterMassing` at 301 s it is in 8..10 (root 8).
- **L-8 OpeningId.** `SkirmishAiController.OpeningId` is "" for `aggressive_s3` and the drawn ID for `aggressive_s4r`, equal to `Memory.Variety.OpeningId`.
- **L-9 Determinism.** L-4 and L-5 run twice: identical intents and memory.
- Edit: `AiFightTests.DProfiles_SwitchesAndValidation` adds `aggressive_s4r` to the profiles allowed to carry the package D switches (it is a copy of `aggressive_s3`), the way branch `review/ai-v4-f` exempted `aggressive_s4f`.

---

## Package H: harness report of openings

### H.1 Records (`GameRunner`, `GameRecord`)

`GameRecord` gains `opening0` and `opening1` (text, "" when that slot's profile has no openings), written after `play_signature` and parsed back; `GameRunner.Play` reads `SkirmishAiController.OpeningId` of each slot after `AiHost.StartAll`. Batch, worker, ladder and trace lines carry them. Tool-side only. Update the whole-line assertions in `HarnessBatchTests`, `LadderTests` and `HarnessTraceTests`.

### H.2 Ladder report (`LadderRunner`)

No seed change is needed: games already get distinct seeds and seats already get independent streams (0.1, R.1). **Mirrored seats split the draw** (each seat draws from its own stream). Sharing it would make every mirror game opening X against X, so the per-opening mirror win rate could not be computed, and the self-ladder would cover 4 pairings per faction instead of 16.

New section `## Openings` in `ladder.md` after `## Mirror and cross-faction`, only when some record has an opening. One row per (faction, opening ID), ordinal order:

`| faction | opening | draws | use share | mirror games vs other openings (decided) | distinct | wins | win fraction | Wilson interval (distinct games) | cross games (decided) | cross wins |`

Definitions. A **draw** is one slot of one record with a non-empty opening; `use share` = draws / all draws of that faction. For opening o of faction f, the **mirror games vs other openings** are the decided records of mirror cells (both factions f) in which a slot plays o and the other slot's opening is different (a baseline without openings counts as different); a **win** is a game that slot won; a game where both slots differ from each other is counted once for each slot's opening. `distinct` and the interval follow S.2 (distinct play signatures; successes = distinct winning signatures). Cross columns: the same for records where faction f plays o in a cross cell.

Warnings (stdout before the LADDER line and in the report under it, like S.2; **never change the exit code**):
- `WARNING opening <f>/<o>: drawn in <share> of <f> draws (above <opening_max_use_fraction>).`
- `WARNING opening <f>/<o>: wins <w> of <n> mirror games against other openings (<fraction>, above <opening_max_mirror_win_fraction>).` (only when n >= `opening_min_games`)
- `WARNING opening <f>/<o>: wins <w> of <n> mirror games against other openings (<fraction>, below <opening_min_mirror_win_fraction>).` (only when n >= `opening_min_games`)
- With `--non-regression` only: `WARNING mirror: candidate wins <w> of <d> distinct mirror games, Wilson upper bound <u> below <ladder_mirror_upper_min>; the candidate may be weaker than the baseline.`

### H.3 Settings (`scripts/ai_eval/ai_eval.json`, `AiEvalSettings` validation)

| Key | Unit | Range | Value |
| --- | --- | --- | --- |
| `opening_max_use_fraction` | fraction | 0 to 1 | 0.40 (P-005 option L) |
| `opening_max_mirror_win_fraction` | fraction | 0 to 1 | 0.65 (P-005 option L) |
| `opening_min_mirror_win_fraction` | fraction | 0 to 1 | 0.35 (placeholder) |
| `opening_min_games` | games | 1 to 1000 | 10 (placeholder) |
| `ladder_mirror_upper_min` | fraction | 0 to 1 | 0.50 (placeholder) |

### H.4 Tests (`StatisticsTests.cs`)

- **H-1 Record round trip.** A record with `opening0` "skai_air" and `opening1` "" writes both after `play_signature` and parses back equal.
- **H-2 Opening arithmetic.** Synthetic `skai_vs_skai` records, 8 decided, written as (opening0, opening1) winner, signature: (a, b) slot 0 wins, S1; (a, b) slot 1 wins, S2; (a, a) slot 0 wins, S3; (b, c) slot 0 wins, S4; (c, a) slot 1 wins, S5; (a, b) slot 0 wins, S1 again; (c, c) slot 1, S6; (c, b) slot 0, S7. Expected rows (16 draws): `a` draws 6 (use share 0.375), mirror games vs other openings 4 (records 1, 2, 5, 6), wins 3 (1, 5, 6), distinct 3 (S1, S2, S5), distinct wins 2; `b` draws 5 (0.3125), games 5 (1, 2, 4, 6, 8), wins 2 (2, 4), distinct 4; `c` draws 5 (0.3125), games 3 (4, 5, 8; record 7 is c against c and is left out), wins 1 (8).
- **H-3 Warnings.** The same set with `opening_min_games` 3: `a` (0.750) gets the "above 0.65" line, `c` (0.333) the "below 0.35" line, `b` (0.400) none; no use-share line (largest 0.375 < 0.40); exit code unchanged.
- **H-4 Mirror upper bound.** Synthetic mirror records 40 decided, all distinct, candidate wins 12, `--non-regression`: the mirror warning is printed (`HarnessStats.Wilson(12, 0, 28, 1.96)` upper bound about 0.454); with 16 wins it is not (about 0.554).
- **H-5 No openings.** Records with every opening "": no `## Openings` section, no opening warning.

---

## E. Acceptance (gate runs; attach every report under `rts/docs/reports/ai_v4_rl/`)

| Package | NUnit | Runs (`prototype_1v1`, `ai_eval.json` defaults, `-c Release`) |
| --- | --- | --- |
| R-1 | R-0 to R-10 | none |
| L-1 | L-1 to L-9; R-0 and R-10 still green | none |
| L-2 | L-1 (data part) | none |
| H-1 | H-1 to H-5 | none |
| L-M (measure) | full suite green | (1) `--ladder --profile-a aggressive_s3 --profile-b aggressive_s3`: every game's `play_signature` and `state_hash` equal to a run of the same command on `c6515ca` (old profiles unchanged; 20 distinct of 216 expected). (2) `--ladder --profile-a aggressive_s4r --profile-b aggressive_s4r`: goal items 1 and 3 (its LADDER verdict and exit code are meaningless in a self-ladder and are ignored). (3) `--ladder --profile-a aggressive_s3 --profile-b aggressive_s4r --non-regression`: goal item 2 and the "below" half of item 3. (4) `--response --profile aggressive_s4r`, plus one run per forced opening (temporary profile copies under `rts/build` with one weight 100, the rest 0; not committed): report only (Q-9). (5) One `--trace` per opening (forced copies, `skai_vs_skai` or `water_vs_water`, game 0): the tick of each build-order step, the first depot and the first attack, next to L.5's intended timings; more than 60 s off is noted, not failed. |

Rules: `sim-reviewer` pass on the `rts/src/Ai` diff after R-1 and L-1 and on the harness diff after H-1. If a goal item fails, the builder reports the numbers and does **not** tune combat or unit data. Allowed once, and reported: set the weight of an opening that triggers a "below" or "above" warning to 0 in `aggressive_s4r` and re-run runs (2) and (3). Jitter keys may be changed only if the result also holds at 0.5 and 1.5 times the new value (the v4 knife-edge rule). Ladder results are a record of the AI, not a balance reading (P-005 decision 11).

Files affected: every profile under `rts/data/ai` (new keys); `AiProfile.cs`, `AiBrain.cs`, `AiBrainFight.cs` (`PredictedAttack`), `AiMemory.cs`, `SkirmishAiController.cs`, new `AiVariety.cs`; `GameRecord.cs`, `GameRunner.cs`, `LadderRunner.cs`, `AiEvalSettings.cs`, `ai_eval.json`; tests named above; headers of every changed file; `rts/src/Ai/README.md`, `rts/tools/BalanceHarness/README.md`, `specs/balance_harness.md` (record fields and ladder sections). **No `rts/src/Sim` file**, no shipped-data `STATE_HASH`, no replay format.

### E.1 Build order and packages

| Package | What | Depends on | Tier | Reviewer |
| --- | --- | --- | --- | --- |
| R-1 | R-0 pin (first commit), `AiVariety` with seed and draws 3 to 5 (composition band of the plan, expansion offset, advantage offset), the timing helpers, root keys in every profile, `aggressive_s4r` with R keys and `"openings": []`, tests R-0 to R-10 | S (on `main`) | Sonnet | `sim-reviewer` |
| L-1 | `openings` schema and validation, draws 1 and 2, `OpeningPlan` / `MainPlan` / `PlanAt`, handover of the timing helpers, `OpeningId`, `[]` in every profile, tests L-1 to L-9 | R-1 | Sonnet | `sim-reviewer` |
| L-2 | The L.5 openings in `aggressive_s4r.json` with house-format comments | L-1 | Sonnet (data), Haiku for the comment lines | none (data) |
| H-1 | Record fields, `## Openings`, warnings, `ai_eval.json` keys, tests H-1 to H-5 | L-1 (for `OpeningId`); can run in parallel with L-2 | Sonnet | `sim-reviewer` on the harness diff |
| L-M | The runs of section E, report `rts/docs/reports/ai_v4_rl/README.md` | L-2, H-1 | Sonnet | coordinator reads it |

---

## F. Re-measuring package F once R is in

Package F (branch `review/ai-v4-f`, not merged) failed its ladder against `aggressive_s3` (0.296, water mirror 0 of 72). Its diagnosis (`reports/ai_v4_f/README.md`, section "Diagnosis of the water mirror 0/72") traced this to rule WS: one passing enemy unit marks all three natural fields, and every evacuated worker is sent to the one home field, which serves only a few gatherers. It measured two candidate fixes on four near-identical games only: **A** "evacuation never overflows" (move a worker only to a field below its cap, else keep mining) and **B** "overflow evacuation only while an own worker was hit within `damage_memory_seconds`". This spec does not choose between them. How to measure them once R and L are on `main`:

1. **Make both options playable from one build.** F's rebase adds one profile key, e.g. `worker_evacuate_overflow` with values `always` (as built), `never` (option A), `after_hit` (option B), and two profiles: copies of `aggressive_s4r` with the F switches on and the key at `never` or `after_hit` (for example `aggressive_s4rf_a`, `aggressive_s4rf_b`). Old profiles get `always` with `use_worker_safety` off, so nothing else moves.
2. **Ladder.** For each option: `--ladder --profile-a aggressive_s4r --profile-b <option profile> --non-regression` on `prototype_1v1`, all three cells, 72 games per cell. Both sides are randomised, so the games are distinct. The baseline is `aggressive_s4r`, not `aggressive_s3`, so the only difference is F.
3. **Enough distinct games to decide.** Each mirror cell needs at least 58 distinct decided games (0.8 x 72). If a mirror cell has fewer, re-run that option with `--games 144`. With 58 distinct games the Wilson half-width at 0.5 is about 0.13. That is enough to tell F's 0 of 72 failure from parity, and enough for the "not weaker" test. It is not enough to rank two options that differ by less than about 15 points.
4. **What an option must show.** (a) LADDER PASS at 0.45. (b) In `water_vs_water`, the cell that failed, the Wilson upper bound over distinct games is at least `ladder_mirror_upper_min`. (c) F's response gate of skirmish_ai_v4.md section E, unchanged (both raid scenarios plus the knife-edge copies), run on the profile as committed. Also report the raid scenarios once per forced opening, because the script seed now picks an opening.
5. **Report both options to the developer** with these numbers, whether one, both or neither passes. If both pass, the choice between them is the developer's.

---

## Rejected extras (one line each)

- Random build site among the top-k tiles: changes signatures without changing strategy, and a random tile can block worker paths.
- Random attack target or path among near-equal ones: `TryChooseTarget` runs several times per think (rally point, attack test, attack), so a random target would need memory to stop it flickering; the Sim's flow field picks the path; in 1v1 there is one HQ.
- Random scouting route: `prototype_1v1` has one enemy spawn, so a different route order only moves information by seconds.
- Random expansion target among near-equal fields: on `prototype_1v1` the near-equal fields are the natural pair, which one depot serves.
- Re-picking the opening after the first scout (an opening tree): the counter-mix already reacts (L.3); a tree is P-004 stage 4 and a larger design.
- Opening files under `rts/data/ai/openings/`: needs a `DataLoader` change in `rts/src/Sim` (flat `ai` folder).
- A harness flag to force an opening: temporary profile copies with one weight do it.
- Paired seeds across swapped seats (common random numbers): in a self-ladder this makes pairs of games identical and halves the distinct count.
- Difficulty levels: the developer wants one strong AI.
- A tower or defensive opening: build-order sites are HQ rings, so a tower cannot guard an expansion; rule SD already builds raid defences.
- Tidecaller and Downdraft openings: the AI cannot use either until package A; add them afterwards.
- Making the distinct fraction or the opening warnings change the exit code: S Q-2 keeps these reports non-gating until the developer decides.

## Out of scope

- Package F's choice between options A and B (section F only says how to measure).
- Package A (ability use) and any `rts/src/Sim` change, combat or unit data retune, or change to the ladder bars.
- The human benchmark (P-005 decision 12) and exploiters (decision 13).
- Response scenario fixes; Unity presentation of the drawn opening.

## Questions for the developer (the builder uses the defaults; the ones that change how the AI plays come first)

- **Q-1. How long does an opening's army mix last?** *Default: until its `handover_seconds` (300 to 480 s), then the plan's mix.* The build order lasts the whole game. Setting `handover_seconds` to 3600 keeps an opening's mix for the whole game, so either answer needs only data.
- **Q-2. Which openings and weights?** *Default: the four per faction of L.5 (standard, rush, fast expand, tech) at 30/20/25/25.*
- **Q-3. Should the AI react to scouting beyond the counter-mix** (re-pick or abandon its opening when it sees a counter)? *Default: no; the counter-mix and rule T react, and the opening's build order still finishes.*
- **Q-4. Should today's plan stay in the pool as an opening (`*_standard`)?** *Default: yes, at 30.*
- **Q-5. Jitter sizes.** *Default: base mix ±25 percent per unit, expansion clock ±20 s, attack edge ±0.15 around `attack_min_advantage`, existing threshold jitter 2 supply.*
- **Q-6. Do the two seats of a mirror draw independently?** *Default: yes (H.2).*
- **Q-7. The strength gate.** *Default: LADDER at least 0.45 plus a mirror-row Wilson upper bound of at least 0.50 over distinct games against `aggressive_s3`, and no opening below 0.35 in mirrors.* Alternative: require the plain mirror fraction to reach 0.50.
- **Q-8. Is "distinct" strong enough?** Any changed command makes a game distinct, so R alone reaches 0.8 almost by construction. *Default: keep `play_signature` for bar item 3 (as decided), with goal items 2 and 3 guarding against variety that is only noise.* Alternative: a coarser signature (opening pair, result, end minute).
- **Q-9. Response scenarios with a randomised profile.** A scenario's script seed now picks one opening. *Default: the counted result is the script seed's draw, as today; the per-opening runs are reported only.* Alternative: a scenario passes only if it passes for every opening.
- **Q-10. Which profile is the baseline afterwards?** *Default: `aggressive_s4r` becomes the baseline of every later ladder once section E passes. F and A are rebuilt on it, and balance batches still wait for bar items 1 to 3.*
- **Q-11. Openings inside the profile, or in their own files?** *Default: inside the profile (no `rts/src/Sim` change).*
- **Q-12. Does the human benchmark wait for L?** (open in the v4 outline) *Default: no dependency; decision 12 places it at the end anyway, and the developer then plays `aggressive_s4r`.*

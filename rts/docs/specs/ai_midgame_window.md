<!-- rts/docs/specs/ai_midgame_window.md -->
<!-- Build spec (27 Nov 2026, developer decision of that day; Q-5 of roster_numbers_2.md): the skirmish AI in the 210 to 450 s window, where Water's AI loses to Skai's. Checks the three hypotheses of the numbers designer against six traced games and five small trials, proposes the smallest set of general AI rules behind optional profile keys (rule FK: the fight decisions believe the kite rule; rule DH: a defence predicted short holds at home), on only in a new candidate aggressive_s5_mid, and fixes the confirming measurement (base seed 20261209, faction-paired bars against aggressive_s5, guards, verdict rule) and two build packages before any run. Part of rts/docs/specs (index: README.md); built by Sonnet builders; the sim-reviewer pass is required for package MG-1. -->
# Spec: the AI in the 210 to 450 s window

Written 27 Nov 2026 against `main` at `44fdff7`. Every value here is a **placeholder** for playtesting unless a source is named.

## 0. Goal, sources, ground rules

**Goal.** Find out from traces why Water's AI loses to Skai's AI between 210 and 450 s, and design the smallest set of general AI rules that address what the traces confirm. Fix the confirming measurement before anything is run.

**Sources read.** `CLAUDE.md`; `specs/roster_numbers_2.md` (section 1); `specs/ai_kite_correction.md` (sections 1, 2, 5, Q-4); `reports/ai_kite/README.md` and `stats_main.md`; the two Skai-against-Water traces in `reports/ai_kite/traces/`; in `rts/src/Ai`: `AiBrain.cs` (rule order in the header, `Army`, `Mass`, `Attack`, `ShouldRetreat`), `AiBrainFight.cs` (D.1 `PredictedAttack`, D.2 `PredictedRetreat`, D.5 `MeasuredDefence` and `DefenceEnough`, D.6.2 `TryKite`), `AiBrainProduction.cs` (rule SA), `AiCounter.cs` (the predictor mix and its kite term), `FightPredictor.cs` (`PredictorSettings.WithKite`, `Kites`), `AiProfile.cs` (loader); `rts/data/ai/aggressive_s5.json` and `aggressive_s5_pd.json`; the unit files of both factions.

**Ground rules (standing developer rules).**
- One strong AI. Rules name no faction and no unit; each sits behind a profile key that is off when absent and on only in the new candidate `aggressive_s5_mid`.
- The AI keeps its variety of tactics (openings, harass, attacks).
- Evaluation only on `prototype_1v1`. Nothing is tuned toward a bar. Nothing is tuned on the confirming seed.
- With the keys absent, every existing profile plays exactly as before: no pin moves.

**Data baseline.** `roster_numbers_2` (Shellback `build_seconds` 28 to 20; Spoutling damage 5 to 10, health 70 to 90) is being built and will most likely be on `main` before this spec is built. Every trial of section 1 ran with those three values applied as an uncommitted scratch change to the designer's worktree data, so the trial baseline matches that `main`. The scratch change was reverted before committing. The confirming run uses whatever `main` holds when MG-B starts, and the report names that commit and whether `roster_numbers_2` is in it. The bars compare two profiles on the same data, so they hold either way.

## 1. Diagnosis

Labels: **trace** (one game, an example), **trial** (a small harness run, section 1.5), **code**, **data**, **inference**.

### 1.1 Six traced games

Two are the committed K-B traces (`reports/ai_kite/traces/skai_vs_water_game0.txt` and `game1.txt`). Up to 360 s both Water players are `aggressive_s5` on the same seed, so they are one game. The other four the designer traced: `--trace --map prototype_1v1 --matchup skai_vs_water --seed 8086 --game 0..3 --profile-a aggressive_s5 --profile-b aggressive_s5 --max-game-seconds 600` on scratch `roster_numbers_2` data. That gives five distinct games.

Water's stance and where it lost its army units between 150 and 300 s:

| Game | Water stance 150 to 300 s | Water army centre to own HQ, 30-s samples 150 to 300 s (tiles) | Water army units lost 210 to 300 s | Skai army units lost 210 to 300 s |
| --- | --- | --- | --- | --- |
| K-B (seed 20261201, game 0) | Massing, no attack until after 360 s | 3, 3, 15, 15, 9, 4 | 8 Pummelers, 8 Reef Runners | 4 Windrunners |
| 8086 game 0 | Massing to 270 s; first attack just before 300 s | 30, 20, 8, 18, 17, 25 | 7 Reef Runners, 1 Shellback | 7 Windrunners |
| 8086 game 1 | Massing to 210 s; attack from 240 s, retreat by 300 s | 3, 3, 16, 19, 46, 30 | 7 Reef Runners, 1 Spoutling (on the attack) | 3 Windrunners |
| 8086 game 2 | Massing, no attack until 360 s | 4, 3, 13, 4, 10, 16 | 1 Pummeler, 2 Reef Runners, 1 Shellback (then 3 Pummelers and 6 Reef Runners in 300 to 330 s) | 5 Windrunners, 1 Talon |
| 8086 game 3 | Massing to 210 s; attack from 240 s, retreat by 300 s | 19, 16, 16, 30, 12, 10 | 8 Pummelers, 5 Reef Runners, 2 Shellbacks, 2 Spoutlings (on the attack) | 6 Windrunners |

(Losses are the `lost=` fields of the 240, 270 and 300 s lines; workers are left out.)

Supply (used / cap, workers included) and common in the bank:

| Game | Water 150 / 180 / 210 / 240 / 270 / 300 s | Water bank at those times | Skai at 210 / 300 s | Skai bank 210 / 300 s |
| --- | --- | --- | --- | --- |
| K-B | 28/42, 39/42, 46/50, 47/50, 50/58, 58/58 | 105, 5, 240, 85, 50, 65 | 41/42, 66/66 | 540, 770 |
| 8086 g0 | 25/26, 32/34, 32/42, 40/42, 48/50, 57/58 | 460, 530, 760, 615, 470, 405 | 42/42, 66/66 | 445, 815 |
| 8086 g1 | 26/26, 33/34, 34/42, 41/42, 50/50, 58/58 | 335, 405, 325, 130, 160, 90 | 42/42, 66/66 | 230, 550 |
| 8086 g2 | 26/34, 34/34, 36/42, 46/50, 49/50, 58/58 | 370, 385, 75, 80, 370, 475 | 42/42, 74/74 | 570, 45 |
| 8086 g3 | 25/42, 35/42, 42/42, 50/50, 55/58, 52/66 | 200, 235, 270, 300, 55, 65 | 41/42, 66/66 | 505, 1050 |

Skai's army at 450 s (supply 3 per Roc Rider, 1 per Windrunner, 2 per Talon):

| Game | Roc Riders | Windrunners | Talons | Roc Rider share of Skai army supply (inference: Thunderhead and Stormcaller counted at their data supply) |
| --- | --- | --- | --- | --- |
| K-B | 9 | 22 | 6 | about 0.35 |
| 8086 g0 | 6 | 26 | 10 | about 0.28 |
| 8086 g1 | 7 | 22 | 7 | about 0.30 |
| 8086 g2 | 12 | 20 | 9 | about 0.45 |
| 8086 g3 | 10 | 19 | 4 | about 0.40 |

Water's army is already about half of Skai's at 330 to 360 s, when Skai has 2 to 4 Roc Riders.

### 1.2 Hypothesis 1: "the AI attacks into kiting". Partly confirmed, and wider than stated

- **Trace, attacks:** in 2 of the 5 games Water attacks at 240 s, Massing until then. Both attacks lose 2 to 3 times what they kill: 8 units for 3, and 17 for 6. Both retreat by 300 s. So the hypothesis holds for every traced attack.
- **Trace, defence:** in the other 3 games Water does not attack before about 300 s and fights Skai's attack groups of Windrunners and Talons within about 30 tiles of its HQ. One of them loses badly in defence (K-B: 16 units for 4). The other two trade about evenly in 210 to 300 s (8 for 7, and 4 for 6), and one of those loses 9 units in the next 30 s.
- **Code:** both paths ask the uncorrected predictor.
  - Attack: D.1 (`PredictedAttack`) believes a Pummeler army beats Windrunners and Talons (attack-move margins +0.56 to +0.61, `roster_numbers_1.md` 1.2).
  - Defence: D.5 (`DefenceEnough`) picks defenders until the predictor says `defend_min_advantage` (1.5). If even every candidate is short, every candidate is sent anyway: it attack-moves to the threat centre. The hold of rule RA step 3 (hold at home when short) runs only with `use_raid_response`, which is off in `aggressive_s5`.
  - The army units then chase Skai units that kite them under Skai's own rule D.6.2 (`TryKite`). The Windrunner kites the Pummeler and the Shellback; the Talon kites the Pummeler and the Shellback (data, `FightPredictor.Kites` with gap 2 and ratio 0.8).
- **Reading:** confirmed for attacks, and for some defences. Water engages Windrunner and Talon armies on open ground because every fight decision believes the uncorrected predictor. The kite correction reached unit choice only (`ai_kite_correction.md` 2.3, Q-4). The traces support rule FK (it would have called off both 240 s attacks) more directly than rule DH. DH rests on the code path and on the trial (1.5).
- **Terrain (code, data):** the AI view has no per-tile flood depth (`AiWorldView` exposes height, ramps, blocking and the global `TideLevel` only). The tide is a 210 s triangle (`match.json` `tide_cycle_seconds`): level 0 at 0 s and 210 s, 1 at 105 s and 315 s. So the 210 to 300 s trades happen on a rising tide, and the overrun at about 315 to 340 s comes at high tide. The tide window keys `attack_tide_min` and `attack_tide_max` already exist in every faction plan and are set to 0 and 1 (open).

### 1.3 Hypothesis 2: "the AI hits the supply cap at 210 to 300 s". Confirmed as a state, refuted as the cause

- **Trace:** Water is at or within 2 of its cap in most samples from 150 to 300 s. In two of the four seed-8086 games it sits there with 300 to 760 common banked. Skai is at its cap in every sample from 210 s (42/42, 50/50, 58/58, 66/66) with more banked than Water.
- **Trial** (1.5, row SA): rule SA (`use_supply_ahead`, as in `aggressive_s5_pd`) on both sides made Water clearly worse. The army ratio at 300 s was better in 5 pairs and worse in 18 (median 0.53 against 0.78), the overrun came 18 s earlier, and Water was alive at 450 s in 17 of 24 games against 24. Over the whole game Water's army spend fell from a median 7325 to 6188 common and its structure spend rose from 1425 to 1800. Skai's structure spend rose from 2550 to 3750, and its median army at 450 s from 68 to 76.5 supply (a game already over at 450 s counts as 0).
- **Reading:** Skai's army is limited by its cap; Water's is limited by its income and its losses. Building supply earlier helps Skai and takes common from Water's army. Rule SA is **not** proposed for this window (Q-3).

### 1.4 Hypothesis 3: "no plan against massed Roc Riders". Not the swing; no rule proposed

- **Trace:** Skai's army at 450 s is mixed, not mostly Roc Riders. They are 6 to 12 of 33 to 41 units, about 0.28 to 0.45 of its army supply. Windrunners are always the most numerous unit.
- The swing is earlier. Water's army is about half of Skai's at 330 to 360 s, when Skai has 2 to 4 Roc Riders.
- **Trace, code:** the counter-mix already answers air. Water fields 1 to 4 Spoutlings from 240 to 390 s in all four seed-8086 games, on `roster_numbers_2` data. In K-B, on the old data, the first Spoutlings come at 360 s, after the first Roc Riders.
- **Reading:** a rule for a massed air army would act on a game that is already lost. It is left out (Q-4). Rule FK (below) also helps against air, through the Spoutling's and Shellback's air fights: a Roc Rider kites the Shellback, so the defence holds instead of chasing it.

### 1.5 Trials (designer's worktree at `44fdff7`, Release, scratch `roster_numbers_2` data, scratch code gated by environment variables and the trial profile id, all reverted)

Self-play runs: `--balance --profile <p> --maps prototype_1v1 --matchups skai_vs_water --games 24 --seed 8086 --max-game-seconds 600 --jobs 2` with `RTS_CHUNK_GAMES=1`. Both sides play the same profile, so a rule acts for both factions, as it would in a shipped AI. Games are paired by index with the base run (same seed and seats). "Ratio" is Water's army supply over Skai's at 300 s, given as pairs better / worse. "Alive at 450" counts Water slot-games still playing at 450 s. "Army at 450" is the median Water / Skai army supply, with a dead player counted as 0. Seed 8086 is now spent.

| Trial | Change | Water wins of 24 | Ratio better / worse; median | Army at 450 (Water / Skai) | Alive at 450 | Overrun median (s) | Worker losses before overrun, lower / higher pairs; median | Game end later / earlier |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| base | `aggressive_s5` | 0 | 0.78 | 12 / 68 | 24 | 331 | 4 | |
| FK | fight decisions use the kite term at 0.4 (rule FK) | 0 | 13 / 10; 0.80 | 14 / 67.5 | 24 | 350.5 | 4 / 10; 7 | 17 / 7 |
| FK+DH | FK and rule DH | 0 | 15 / 8; 0.79 | **30** / 67 | 23 | 332 | 7 / 12; 5 | 9 / 14 |
| SA | `use_supply_ahead` true, `supply_ahead_max_pending` 3 | 0 | 5 / 18; 0.53 | 2 / 76.5 | 17 | 313 | 11 / 13; 5 | 5 / 19 |
| FK+DH+SA | all three | 0 | 11 / 13; 0.60 | 0 / 60.5 | 14 | 317.5 | 9 / 12; 5 | 5 / 18 |

Self-play sign tests against base (24 pairs): FK+DH, Water army at 450 s (dead = 0) better in 20 and worse in 4 (p 0.0015); Water income to 300 s better in 8 and worse in 15 (p 0.21). FK alone: army at 450 s 15 / 9 (p 0.31); income 11 / 10.

Head-to-head trial (the h2h design of section 4, smaller; scratch code active only for the trial profile): `--behaviour --profile-a aggressive_s5 --profile-b trial_mid --maps prototype_1v1 --matchups skai_vs_water --games 48 --seed 8086 --paired-faction water --jobs 2`, full game length, 24 pairs, FK+DH. Pairing kind `skai:full,water:full`; spot check 0 mismatches.

| Check | Better / worse / ties | p |
| --- | --- | --- |
| `won` | 0 / 0 / 24 (no Water win in either profile) | 1 |
| `army_supply_ratio_at` 300 s | 14 / 8 / 2 | 0.29 |
| Water army at 450 s, dead = 0 (script) | 13 / 11; median 24.5 against 18 | about 0.84 |
| `worker_losses_pre_overrun` | 8 / 11 / 5 | 0.65 |
| `income_by` (300 s) | 6 / 17 / 1 | **0.035, WORSE** (median income to 300 s 5828 against 5988 common) |
| `harass_squads` | 13 / 4 / 7 | 0.049, BETTER |
| `trained_unit` `water.reef_runner` | 7 / 15 / 2 | 0.13 |

Readings:
- No rule moved self-play wins (0 of 24 everywhere, at a 600 s cap).
- FK+DH keeps Water's army alive: 30 against 12 supply at 450 s. Skai's army is unchanged, so Water still loses.
- FK alone delays the overrun by about 20 s but loses more workers before it. That count is partly longer exposure: the overrun comes later.
- SA hurts Water (1.3).
- Head to head, against an unchanged Skai, FK+DH keeps much less of the army effect (13 / 11), and Water's income to 300 s is worse. In self-play the Skai side also runs the rules, and Skai's defence and attacks change with them (2.1). Inference: the army effect depends on both sides using the rules, and the income cost appears in both designs.

### 1.6 What a rule can and cannot do here (inference)

The traces show a trade problem: Water throws its army at kiting units. The smallest change that addresses it is to let the fight decisions believe the kite rule the AI already plays, and to stop a defence that is predicted short from chasing. Neither rule creates income or army supply, and Skai's army at 450 s is about twice Water's even when Water keeps its army. **This spec does not expect a measurable change in Water wins** (4.5). It expects Water's army to survive the window, which is the precondition for any later rule (tide-timed fights, a counter to air) to matter.

## 2. The rules

Both rules run in step 6 (Army) of the AI's think order (`AiBrain.cs` header). Neither adds a random draw, a new memory field or a new iteration. Both read only the profile, the view and existing memory.

### 2.1 Rule FK: the fight decisions believe the kite rule

**Definition.** When `fight_kite_factor` is below 1, every predictor call the brain's per-think `Turn` makes uses `PredictorSettings.From(profile, memory.FightSimRules).WithKite(fight_kite_factor, kite_min_range_gap_tiles, kite_speed_ratio_min)` instead of `PredictorSettings.From(...)`. This is the one place, `Turn.PredictFight` (`AiBrainFight.cs`, the lazily built `_predictorSettings`). That covers:
- D.1 attack by prediction (`PredictedAttack`);
- D.2 retreat by prediction and the end test of rule HR (`PredictedRetreat`);
- D.5 measured defence (`DefenceEnough`);
- rule HR's squad-against-defences test (`StartSquad`);
- the placement advantage (`AiBrainPlacement.cs`, the `PredictFight` call returning `.Advantage`).

The predictor mix (`AiCounter.PredictorMix`) is not touched. It keeps reading the faction plan's `predictor_kite_factor`, which is absent in the candidate (Q-2).

The term is the existing one (`ai_kite_correction.md` 2.1): an entry's damage per second against a target that `Kites(target, attacker)` is multiplied by the factor. It uses base speed, as rule D.6.2 does.

**Terrain.** Rule FK does not read terrain. The view has no flood depth, and D.6.2, the rule being believed, ignores terrain too. So on flood water the term is too pessimistic for a Water defender, as stated in `ai_kite_correction.md` 2.1. A location-aware version needs a flood-depth query in `AiWorldView` and a terrain-aware `TryKite`. It is a separate revision (Q-1).

**What it does for each faction (data, `Kites` with gap 2 and ratio 0.8):**
- **Water's AI** believes its Pummelers and Shellbacks deal 0.4 of their damage to Windrunners, Talons, Roc Riders and Thunderheads, and its Shellbacks 0.4 to Stormcallers. It therefore attacks less often with a Pummeler-heavy army, retreats sooner, and asks for more defenders. Its Reef Runners and Spoutlings are not kited by Skai's early units (range gap or speed test fails), so armies built on them are judged as before.
- **Skai's AI** against Water: no Water unit kites a Skai army unit except the Leviathan against the Great Aerie. So Skai's own units are judged as before, but Skai believes Water's Pummelers and Shellbacks hurt it less. Skai attacks a Pummeler or Shellback army sooner and defends against it with fewer units.
- **Skai mirror:** no Skai army unit kites another under these data except against the Fledgling (a worker), so play should change little.
- **Water mirror:** Reef Runners and Spoutlings kite Pummelers and Shellbacks, so both sides' decisions change.

**Key.**

| Key | File | Unit | Range | Value |
| --- | --- | --- | --- | --- |
| `fight_kite_factor` | root of an AI profile, optional (absent = 1 = off); present only in `rts/data/ai/aggressive_s5_mid.json` | fraction of damage per second a kited unit deals to its kiter, in the brain's fight decisions | 0 to 1; exactly 1 behaves as absent | **0.4** in the candidate (placeholder; the calibration of `ai_kite_correction.md` 2.5) |
| `kite_min_range_gap_tiles`, `kite_speed_ratio_min` | every profile (existing) | tiles; ratio | as now | read, not changed (2 and 0.8) |

The loader rejects the key with an error naming `fight_kite_factor` when it lies outside 0 to 1, or when it is below 1 and either `predictor_model` is `sim` (the term exists only in the formula) or `use_unit_control` is false (the AI does not kite, so there is no rule to believe). The checks follow the faction-plan checks for `predictor_kite_factor` (`AiProfile.cs`, near line 1378).

### 2.2 Rule DH: a defence predicted short holds at home

**Definition.** When `use_defence_hold` is true, in D.5 (`MeasuredDefence`), on the branch without an active raid and with visible threats, set `hold = true` when `DefenceEnough` returns false. The existing code does `hold = UseRaidResponse && !enough`; with the switch it becomes `(UseRaidResponse || UseDefenceHold) && !enough`. Everything after that is the existing rule RA step 3:
- the chosen units (every candidate, because the defence is short) become the defend group;
- each one farther than `raid_hold_radius_tiles` from `Standable(Home())`, not ordered by unit control this think and not already moving to within 2 tiles of that point, is ordered to move there;
- the others get no order, so they stand and fire at whatever comes into range.

Each think re-runs `DefenceEnough` with the threats and fixed defences at that moment. When the threats come within `engagement_radius_tiles` of complete own static defences, those defences join the fixed side; when the prediction then reaches `defend_min_advantage`, the hold ends and the normal Defend orders go out. That is the "favourable fight" condition: no new test is needed.

Not changed:
- the raid branch, which runs only with `use_raid_response`;
- the blind branch (no visible threat);
- the recall test while attacking. A short defence with attack-group members still recalls the attack when their supply reaches `defend_recall_supply`, and the recalled army then holds as well.

The Rooted HQ joins the fixed side only under `use_raid_response`, as now. Rule DH does not add it (Q-5).

**What it does for each faction.** Both factions' defences stop chasing threats they are predicted to lose to, and wait near the HQ instead. Together with rule FK, Water's defence against Windrunner and Talon groups is usually short, so it holds. Skai's defence against a Pummeler or Shellback raid is usually enough, because Skai believes them weaker, so Skai's defence engages as before. Workers in fields away from the HQ are less defended while a hold lasts (bar MB-4 watches this).

**Keys.**

| Key | File | Unit | Range | Value |
| --- | --- | --- | --- | --- |
| `use_defence_hold` | root of an AI profile, optional (absent = false); present only in `aggressive_s5_mid.json` | boolean | true or false | **true** in the candidate |
| `raid_hold_radius_tiles` | every profile (existing, rule RA) | tiles | 1 to 20 | read, not changed (4 in `aggressive_s5`) |
| `defend_min_advantage` | every profile (existing) | ratio | as now | read, not changed (1.5) |

The loader rejects `use_defence_hold` true with an error naming the key when `use_measured_defence` is false (D.5 does not run).

### 2.3 Considered and not proposed

| Idea | Why not |
| --- | --- |
| Rule SA (supply ahead) | Trial SA: worse for Water when both sides use it (1.3). Skai is the cap-limited side. |
| Production capacity (rule PC) | Water spends its income; the limit is income and losses, not producers (`roster_numbers_2.md` 1.1: Water banks 90 at 300 s in K-B). |
| A response to a massed air army | Not the swing (1.4). The counter-mix already adds Spoutlings. Q-4. |
| Fight on flood water / tide-timed attacks | Needs flood depth in the AI view and a terrain-aware kite rule; the tide keys exist but are a per-faction tuning choice, not a general rule. Q-1. |
| The predictor-mix kite term in the candidate | It is a separate, measured candidate (`aggressive_s5_kite`, FAIL on a guard). Adding it would mix two effects. Q-2. |

## 3. Package MG-1 (`rts/src/Ai`, one new profile, tests; sim-reviewer afterwards)

### 3.1 Code

1. **`AiProfile`** (`AiProfile.cs`).
   - Add `public Fix64 FightKiteFactor { get; }` (default `Fix64.One`) and `public bool UseDefenceHold { get; }` (default false), each with a `/// <summary>` giving unit and range as in 2.1 and 2.2.
   - Read both as optional root keys (absent = default), add both names to the root's allowed-key list next to `use_supply_ahead`, and apply the rejections of 2.1 and 2.2 with errors that name the key.
   - Add two lines to the file header's list of packages: `ai_midgame_window.md package MG-1: fight_kite_factor (rule FK) and use_defence_hold (rule DH), optional, absent = off; only aggressive_s5_mid sets them`.
2. **`Turn.PredictFight`** (`AiBrainFight.cs`). When building `_predictorSettings`, apply `.WithKite(_profile.FightKiteFactor, _profile.KiteMinRangeGapTiles, _profile.KiteSpeedRatioMin)` only when `FightKiteFactor != Fix64.One`. Update the file header (one line naming rule FK) and the `_predictorSettings` summary.
3. **`MeasuredDefence`** (`AiBrainFight.cs`). In the no-raid branch with threats, write `hold = (_profile.UseRaidResponse || _profile.UseDefenceHold) && !enough;`. Update the comment on `hold` and the method summary to name rule DH.
4. **`AiBrain.cs` header.** In rule 6, add one sentence after 6': "Rule DH (ai_midgame_window.md, only with use_defence_hold, which needs use_measured_defence): without an active raid, a defence predicted short holds as in rule RA step 3. Rule FK (only with fight_kite_factor below 1): every fight-decision predictor call carries the kite term of ai_kite_correction.md."

Determinism: no float, no new collection, no new draw. `WithKite` returns a copy (existing). The order of every loop is unchanged.

### 3.2 Data: `rts/data/ai/aggressive_s5_mid.json`

A copy of `aggressive_s5.json` with:
- `id` set to `aggressive_s5_mid`;
- the path header and file description saying "candidate of rts/docs/specs/ai_midgame_window.md: aggressive_s5 plus rules FK and DH";
- two added keys, each with the standard comment line above it:

```
// Rule FK: share of damage per second a kited unit deals to its kiter in the AI's fight decisions (attack, retreat, defence, harass, placement); 1 = off. Unit: fraction. Range: 0 to 1. Source: rts/docs/specs/ai_midgame_window.md 2.1 (calibration of ai_kite_correction.md 2.5); placeholder.
"fight_kite_factor": 0.4,
// Rule DH: a defence predicted short (D.5) holds at home as in rule RA step 3 instead of chasing the threat. Unit: boolean. Range: true or false. Source: rts/docs/specs/ai_midgame_window.md 2.2; placeholder.
"use_defence_hold": true,
```

No other file in `rts/data` changes. No existing profile gets the keys.

### 3.3 Acceptance tests (NUnit, `rts/tests/Sim.Tests/Ai`, new class `AiMidgameTests` unless stated)

1. `aggressive_s5` loads with `FightKiteFactor == Fix64.One` and `UseDefenceHold == false`. Every profile file under `rts/data/ai` loads.
2. `aggressive_s5_mid` loads with `FightKiteFactor == Fix64.Parse("0.4")` and `UseDefenceHold == true`. Every other property equals `aggressive_s5`'s, compared field by field as `AiKiteCorrectionTests` compares `aggressive_s5_kite`, except `Id`.
3. Loader rejections, each with an error message containing the key name: `fight_kite_factor` 1.5; `fight_kite_factor` -0.1; `fight_kite_factor` 0.4 with `predictor_model` `sim`; `fight_kite_factor` 0.4 with `use_unit_control` false; `use_defence_hold` true with `use_measured_defence` false.
4. `fight_kite_factor` 1 present behaves as absent: the same think on the same view gives byte-identical intents to a profile without the key.
5. Rule FK in D.1. Build the view the way `AiFightTests` builds its D.1 cases: an own army of 8 Pummelers at the rally, and an enemy model of 8 Windrunners known at the chosen target, with no static defence. Use `aggressive_s5` with `use_predictor_attack` true and `attack_force_supply` above the army. Pick the counts so the base profile starts an attack (stance becomes Attack). With `fight_kite_factor` 0.4 the stance stays Massing. With 8 Reef Runners instead of Pummelers both profiles give the same decision (Windrunners do not kite Reef Runners).
6. Rule FK in D.2. Use an attack group of Pummelers engaged with visible Windrunners, past `attack_retreat_grace_seconds`, at counts where the base profile does not retreat. With 0.4 it retreats (stance Retreat, `Retreats` up by 1).
7. Rule DH. Build the view the way `AiRaidDefenceTests.RA2_Hold` does, but with `use_raid_response` false and no raid: visible threats inside the base that every candidate together cannot beat at `defend_min_advantage`. Without `use_defence_hold`, every candidate gets an attack-move to the threat centre. With it, candidates farther than `raid_hold_radius_tiles` from home get a move to home, the nearer ones get no order, and all are in `DefendGroup`.
8. Rule DH ends. Same as 7, plus one own complete static defence within `engagement_radius_tiles` of the threat centre, with enough strength that the fixed side plus the candidates reach `defend_min_advantage`. With `use_defence_hold` the defenders get attack-moves to the threat centre (no hold).
9. Rule DH leaves the blind branch alone. With no visible threat and a recent hit inside the base, the intents with and without `use_defence_hold` are identical.
10. Determinism. Play one `skai_vs_water` game of `aggressive_s5_mid` against itself on `prototype_1v1` to 300 s twice, in-process, with the same seed: equal state hashes. (Use the harness helper the existing AI game tests use.)
11. No pin moves: the existing trace pins (`FightSimTests`, `SkirmishAiTests`, `AiVarietyTests`, the ablation hashes) pass unchanged. This is the suite itself; no new assertion.

## 4. Measurement MG-B (fixed before any run; nothing tuned after a result)

### 4.0 Design: self-play pairs for the bars, head-to-head pairs for the guards

The developer's problem is that Water's AI loses to Skai's AI when both play the same profile. A shipped rule acts for both factions. So the bars compare **self-play** games: `aggressive_s5_mid` against itself, and `aggressive_s5` against itself, game g of one run paired with game g of the other. The game seed depends on (base seed, map, matchup, game index), not on the profile (`SeedDerivation.GameSeed`), so a pair shares seed and seats. Each pair has one Water slot-game per run. This is the data-paired design of `roster_numbers_2.md` 4.1.

The **head-to-head** run (`--behaviour --paired-faction`, candidate against baseline) answers a different question: is the candidate weaker than `aggressive_s5` for either faction? Those are guards (one strong AI).

Stated plainly: the designer chose this split after seeing both trials (1.5). The self-play trial moved the mechanism strongly (army at 450 s better in 20 pairs, worse in 4); the head-to-head trial did not (13 / 11). The split follows from the question, not from the numbers, and both runs are reported in full. The head-to-head trial's income result (worse, p 0.035) is the reason for bar MB-5.

### 4.1 Runs (Release build of `main` after MG-1 is merged)

The **base seed is 20261209** for every game run and trace. It is unspent: it is not in any file on any `origin` branch at `44fdff7`, and the designer never ran it. Spent seeds: 5551, 8086 (this spec's trials), 20261117, 20261127, 20261201, 20261203, 31337. `<dir>` is `rts/docs/reports/ai_midgame`.

```
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_self_s5  dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5     --maps prototype_1v1 --matchups skai_vs_water --games 72 --seed 20261209 --jobs 4 --out <dir>/self_s5
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_self_mid dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5_mid --maps prototype_1v1 --matchups skai_vs_water --games 72 --seed 20261209 --jobs 4 --out <dir>/self_mid
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_h2h      dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_mid --maps prototype_1v1 --matchups skai_vs_water --games 96 --seed 20261209 --paired-faction water --jobs 4 --out <dir>/h2h
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_h2h      dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_mid --maps prototype_1v1 --matchups skai_vs_water --games 96 --seed 20261209 --paired-faction skai  --jobs 4 --out <dir>/h2h_skai
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_mirrors  dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_mid --maps prototype_1v1 --matchups skai_vs_skai   --games 48 --seed 20261209 --paired-faction skai  --jobs 4 --out <dir>/mirror_skai
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_mirrors  dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5_mid --maps prototype_1v1 --matchups water_vs_water --games 48 --seed 20261209 --paired-faction water --jobs 4 --out <dir>/mirror_water
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --response --profile aggressive_s5     > <dir>/response_s5.txt
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --response --profile aggressive_s5_mid > <dir>/response_mid.txt
```

There are also four self-play traces: `--trace --map prototype_1v1 --matchup skai_vs_water --seed 20261209 --max-game-seconds 600 --game <g>` for g = 0 and 1, once with `--profile-a aggressive_s5 --profile-b aggressive_s5` and once with `--profile-a aggressive_s5_mid --profile-b aggressive_s5_mid`. Each goes to `<dir>/traces/<profile>_game<g>.txt`.

- The `h2h_skai` command reuses the `h2h` cache. It plays no new game; it only re-tallies the same 96 games from Skai's point of view. If it starts playing games, stop and report.
- The `h2h` run's header must read `pairing kind: skai:full,water:full`. Anything else: stop and report.
- **Resumable pieces.** Every game run uses `RTS_CHUNK_GAMES=1` and its own `RTS_WORKER_CACHE` folder inside `<dir>`. After every 24 finished games, and after each run, commit the cache folder and the outputs and push. Rerunning the same command after a container restart resumes from the cache.
- **The self-play pairing.** No harness flag pairs two `--balance` runs, so the builder joins `self_s5/balance.jsonl` and `self_mid/balance.jsonl` on (`matchup`, `game`) with a throwaway script, not committed (as `roster_numbers_2.md` 4.1 does). The builder commits the per-pair table as `<dir>/pairs.md`. Columns: game; Water slot; Water won (s5 / mid); Water and Skai army supply at 300 s and at 450 s (`army_supply[1]`, `[2]`; null = dead = 0); Water income to 300 s (sum of the first 10 entries of `income`, common); Water `worker_losses_pre_overrun`; Water `overrun_second`; end tick.
- **Sign test.** Exact two-sided, α = 0.05, on the decisive pairs (ties left out). The script prints, per bar, D, better, worse, ties and p.
- **Suite in chunks,** as `reports/ai_kite/suite_chunks.md`: record each chunk's totals in `<dir>/suite_chunks.md` and push after each chunk.

### 4.2 Bars

Self-play bars (72 pairs, from `pairs.md`, Water's point of view):

| ID | Measure (direction) | Kind | Pass when |
| --- | --- | --- | --- |
| MB-1 Water wins (the point) | Water won (higher) | primary | BETTER |
| MB-2 Army at 300 s | Water army supply over Skai's at 300 s (higher); a pair with a null sample is left out | secondary | not WORSE |
| MB-3 Army survives the window (the mechanism) | Water's own army supply at 450 s, dead = 0 (higher) | mechanism | BETTER |
| MB-4 Workers | Water `worker_losses_pre_overrun` (lower) | secondary | not WORSE |
| MB-5 Income | Water income to 300 s, common (higher) | secondary | not WORSE |

Guards:

| ID | Run | Check | Pass when |
| --- | --- | --- | --- |
| MG-1 Head to head, Water | h2h | `won` (higher), `--paired-faction water` | not WORSE |
| MG-2 Head to head, Skai | h2h_skai | `won` (higher), `--paired-faction skai` | not WORSE |
| MG-3 Skai mirror | mirror_skai | `won` (higher) | not WORSE |
| MG-4 Water mirror | mirror_water | `won` (higher) | not WORSE |
| MG-5 Response suite | response | no scenario that passes for `aggressive_s5` fails for `aggressive_s5_mid` | holds |
| MG-6 Clean commands | traces, h2h | (a) no `aggressive_s5_mid` trace line has a rejection reason, or a larger count of one, than the `aggressive_s5` trace of the same game index (`site_occupied` excepted); (b) the h2h spot check prints 0 mismatches | holds |
| MG-7 Variety | h2h, self-play, traces | (a) the h2h pairing kind is `full`, so the Water opening is the same in both games of every pair; (b) `harass_squads` not WORSE in h2h for Water and in h2h_skai for Skai; (c) for each of the four Water openings in self-play, the candidate's median trained count of each Water army unit type is reported next to the baseline's, and no unit type the baseline trains (median at least 2) has a candidate median of 0; (d) in each of the four traces, each side that is still alive at 300 s starts at least one attack (`attacks=` above 0) before 600 s or before its HQ dies, or did so in the `aggressive_s5` trace of the same game index either | holds |
| MG-8 Suite and pins | suite | full suite green on the measured commit; no existing pin moved; no assertion loosened | holds |

### 4.3 Stated power (before the run)

Sign-test thresholds for D decisive pairs (exact, two-sided, 0.05; `ai_kite_correction.md` 5.3): 8 need 8 better, 10 need 9, 12 need 10, 24 need 18, 48 need 32, 72 need 45.
- **MB-1.** Self-play Water wins about 0 of 72 (0 of 24 in every trial). With none the other way, power is 0.22 for 4 candidate-only wins, 0.56 for 6 and 0.82 for 8. **Minimum detectable effect at power 0.8: about 8 of 72 pairs (11 percent) in which only the candidate's Water wins.**
- **MB-3.** The self-play trial gave 20 better and 4 worse of 24. If the true share of better pairs among decisive ones is 0.7, 70 decisive pairs give power of about 0.93. At 0.6 the power is about 0.45.
- **MB-5.** The trials leaned worse: 8 better, 15 worse in self-play, and the h2h check `income_by` was 6 / 17 (p 0.035). At a true worse share of 0.65, 72 pairs detect WORSE with power of about 0.75. **This is the bar most likely to fail.**
- **Guards.** MG-1 and MG-2 (48 pairs) and MG-3 and MG-4 (24 pairs) detect only a large regression. MG-2 has a ceiling: Skai wins almost every game already.

### 4.4 Verdict rule

- **PASS**: MB-1 BETTER, MB-3 BETTER, MB-2, MB-4 and MB-5 not WORSE, MG-1 to MG-8 hold.
- **NO_MEASURABLE_EFFECT**: MB-1 not significant and not WORSE, MB-3 BETTER, MB-2, MB-4 and MB-5 not WORSE, MG-1 to MG-8 hold.
- **FAIL**: MB-3 not BETTER, anything WORSE, or a guard fails.

On PASS the developer decides whether the keys go into `aggressive_s5`; that would move the `aggressive_s5` pins once. On any other verdict `aggressive_s5_mid` stays a candidate (Q-6). There is **no revision round on 20261209**: a new value or rule is a new revision with a new unspent seed.

### 4.5 Expected result (stated before the run)

- **MB-1: NO_MEASURABLE_EFFECT**, with 0 to 2 candidate-only Water wins of 72. No trial game was won by Water, in self-play or head to head.
- **MB-3: BETTER** (self-play trial 20 / 4, median 30 against 12).
- **MB-2:** not WORSE, likely not significant (self-play trial 15 / 8).
- **MB-4:** not WORSE, likely not significant (self-play trial 7 better, 12 worse; h2h 8 / 11).
- **MB-5: the risk.** It leans worse in both trials. If it comes out WORSE, the verdict is FAIL, and the report should say whether the income fell during holds (rule DH leaves outlying fields to raiders) or for another reason.
- **MG-5:** a risk, as for every recent candidate: `response_turtle_water` has flipped with small AI changes twice (RR-B, K-B).
- **Other guards:** expected to hold. In the h2h trial, `harass_squads` was better for Water (13 / 4) and the spot check read 0 mismatches.
- **Games:** about as long as before (self-play trial median end 526.5 s against 532 s at a 600 s cap).
- **Skai still wins at least 66 of the 72** self-play games.

### 4.6 Reported, not judged

Medians over the Water slot-games of self-play, for both profiles:
- trained units per type;
- army supply at 180, 300 and 450 s, own and Skai's (dead = 0, plus the count alive);
- income per 30-s bucket to 300 s;
- worker losses before the overrun;
- the overrun second and the game end tick.

Also reported:
- the same medians for the h2h Water slot-games, and every `BEHAVIOUR_PAIRED` line of h2h and h2h_skai;
- Water wins in each run;
- both response files side by side;
- from the traces: stance, attacks, retreats, army and losses at 210, 240, 270, 300, 330 and 360 s, in the table form of 1.1.

## 5. Build order (each package sized for one Sonnet session)

1. **MG-1** (builder): section 3, on a branch from `main`. Run the suite in chunks (`reports/ai_kite/suite_chunks.md`). Then the `sim-reviewer` (Opus) reviews `rts/src/Ai` for determinism and for "no faction or unit named". Merge after review. No pin moves.
2. **MG-B** (builder): section 4 on `main` with MG-1 merged.
   - Run self_s5, self_mid, h2h, h2h_skai (re-tally), the two mirrors, response, then the traces, committing and pushing after every 24 games and after each run.
   - Write `pairs.md` and the sign tests, judge every bar and guard from the outputs, and write `<dir>/README.md` (path header): the commands, wall times, the data commit (with or without `roster_numbers_2`), every bar row (D, better / worse / ties, p, verdict), the guards with evidence, 4.6, the verdict and the files.
   - Do not tune or rerun on another seed.
   - Give changelog bullets to the coordinator; do not edit `CHANGELOG.md` or `STATUS.md`.

## 6. Out of scope

- Unit choice and the predictor mix (`predictor_kite_factor`, Q-2).
- Supply ahead and production capacity (2.3).
- A plan against massed air (Q-4).
- Terrain-aware kiting, flood depth in the AI view, tide-timed attacks (Q-1).
- The HQ in the defence without a raid (Q-5).
- Any `rts/src/Sim`, data value or roster change; other maps.

## Questions for the developer (builders use the defaults)

| # | Question | Default |
| --- | --- | --- |
| Q-1 | Should the AI learn to fight on flood water and not chase kiters on dry ground? That needs a flood-depth query in `AiWorldView` and a terrain-aware kite rule (D.6.2 and the predictor term), so a Windrunner is not believed to kite a Pummeler in deep water. | Not in this revision. A separate spec after MG-B, if MB-3 passes. |
| Q-2 | Include the predictor-mix kite term (`predictor_kite_factor` 0.4 in Water's plan, candidate `aggressive_s5_kite`) in `aggressive_s5_mid`? It would make unit choice and fight decisions believe the same thing, but mixes two effects. | No; measure FK and DH alone. A later revision may combine them. |
| Q-3 | Rule SA hurt Water in self-play because Skai, the cap-limited side, gains more from it (1.3). Leave `use_supply_ahead` off in the main AI? | Yes, off. |
| Q-4 | A rule for a massed air army (for example, raise the anti-air share when the enemy's air supply passes a fraction of its army)? The traces say Roc Riders are 0.28 to 0.45 of Skai's army at 450 s and come after the swing. | Not now. Revisit if Water's army survives the window (MB-3) and still loses to air. |
| Q-5 | Should the Rooted HQ's weapon count on the defending side in rule DH, as it does under rule RA? | No; it would change Skai's defence too, and RA's measurement did not show it helping. |
| Q-6 | The expected verdict is NO_MEASURABLE_EFFECT: Water's army survives but the wins do not move. Keep `aggressive_s5_mid` as the base for the next window rule (Q-1 or Q-4), or put the keys in `aggressive_s5` on that verdict? | Keep it as a candidate and build the next rule on it. |
| Q-7 | Accept that the win bar can detect only about 8 extra wins in 72 (power 0.8)? | Yes. |

<!-- rts/docs/specs/roster_numbers_1.md -->
<!-- Build spec (24 Nov 2026), written after the developer reopened the roster ("just change numbers here; if a unit is necessary, write a proposal"): a fight-level diagnosis of why Water loses to Skai in the first 300 s on prototype_1v1, one number change in rts/data (Reef Runner max_health 60 to 66) with its tuning comment, a measurement plan fixed before the builder runs anything (fight bars on army values the designer never ran, guards, and the AI balance run as information only), the list of pins and reports the change moves, an existing AI fault the changed game exposes (section 4.5, fix RN-0 offered as a default), and the open questions with their defaults. No code change for the roster, no new unit (section 7 says why no proposal was written). Part of rts/docs/specs (index: README.md); built by a Sonnet builder; the sim-reviewer pass is still required because the change moves every pinned hash. -->
# Spec: roster numbers 1 (Water against Skai in the first 300 s)

Written 24 Nov 2026 against `main` at `cdff393`. Every value in this spec is a **placeholder** for playtesting. Unit, combat and economy numbers were frozen until the developer reopened the roster for this task ("Let's try to just change numbers here; if you think there is a necessary unit to add, write a proposal for it").

## 0. Goal, sources, ground rules

**Goal.** Explain, with fight-level evidence, where Water is behind Skai at equal cost in the first 300 s and why. Then name the smallest set of number changes that closes the part numbers can close, without weakening Skai's identity (the mobile air faction) and without flattening the asymmetry (Water is the tide and flood faction). The 72 of 72 Skai wins in every AI balance run is a **symptom to explain, not the target**.

**Sources read.** `CLAUDE.md`; `rts/docs/STATUS.md` (the "AI work resumed" paragraph); `proposals/P-005-roster-holes-and-ai-for-balance.md` (holes H1 to H16, design rules 1.6, developer decisions of 18 Nov); `specs/missing_roster.md`; `specs/ai_production_defence.md` sections 0, 1 and R2.0; reports `ai_pd/baseline`, `ai_pd/r2` (with its three traces), `ai_m2` (with its three traces), `fights_2026-11-18`, `predict_v2b`; `rts/tools/BalanceHarness/README.md` (fight mode); every file in `rts/data/units`, `rts/data/factions`, `rts/data/tuning/{combat,terrain,flood,momentum,economy,supply,hq,statuses}.json`; `rts/data/ai/aggressive_s5.json`; code read for the kite rule only: `rts/src/Ai/AiBrainFight.cs` `TryKite` and `UnitControl` (rule D.6.2), and `rts/tools/BalanceHarness/FightRunner.cs` (the fight mode's control).

**Ground rules.**
- **Data only.** The change is one value in `rts/data/units/water/reef_runner.json` plus comment and description text, one documentation line in `rts/data/scripts/fights/matchups.json`, and one new fight settings file for the bars (`rts/data/scripts/fights/roster_numbers_1.json`, outside the content digest). No `rts/src` or `rts/tools` change in RN-1. Tests change only where a pin moves (section 4). The one exception is RN-0 (section 4.5): a one-condition fix of an existing AI fault the changed game exposes, offered as the default of Q-8.
- **Determinism.** A data value is read through the existing loader into `Fix64`; no new rule, no new random draw, no new iteration. The content digest changes, so every state hash moves; the sim-reviewer checks that nothing else did (section 4.3).
- **No chasing a win rate.** The bars are fight-level (section 3); the AI run is information with a stated expectation; one revision round at most (section 3.5).

## 1. Diagnosis

Every number below is labelled **measured** (a harness run named in 1.7), **data** (arithmetic on the files under `rts/data`), **trace** (read from a committed AI trace, one game each, so an example and not a median) or **inference** (my reading; nothing prints it).

### 1.1 At equal cost in a meeting fight, Water's early army wins (measured)

The standard sweep (`--fights`, default settings) on `cdff393` reproduces `reports/fights_2026-11-18/fights.jsonl` exactly: 780 fights, 0 records differ, so the unit numbers in these fights have not changed since 18 Nov. Its cross-faction early-unit rows (flat case, 750 and 1500, both seats; margin = Water's value left minus Skai's):

| Skai side | Water side | Water wins | margins |
| --- | --- | --- | --- |
| Windrunner | Pummeler (`water.grasper`) | 4 of 4 | +0.60 to +0.66 |
| Talon Marksman | Pummeler | 4 of 4 | +0.56 to +0.59 |
| Windrunner | Shellback | 4 of 4 | +0.85 to +1.00 |
| Talon Marksman | Shellback | 4 of 4 | +0.91 to +0.99 |
| Windrunner | Reef Runner | 2 of 4 | -0.09 to +0.06 (an exact mirror: same health, damage, cooldown, range, cost) |
| Talon Marksman | Reef Runner | 2 of 4 | -0.23 to +0.20 |
| Windrunner, Talon | Spoutling | 0 of 8 | -0.78 to -0.84 |

So **unit power per cost in an open meeting fight is not where Water is behind**. This matches P-005 H3 (the Pummeler is the strongest mobile unit per value).

### 1.2 The game's AI kites; under its kite rule the Pummeler flips to a heavy loss (measured)

The fight mode's control is plain attack-move with idle re-targeting (`FightRunner.Control`). The skirmish AI is not: with `use_unit_control` true (on in `aggressive_s5`), rule D.6.2 (`AiBrainFight.TryKite`) moves a ranged unit `kite_step_tiles` (3) straight away from the nearest armed enemy army unit within `kite_trigger_tiles` (3) that can hit it, when its range exceeds that enemy's by at least `kite_min_range_gap_tiles` (2) and its base speed is at least `kite_speed_ratio_min` (0.8) times the enemy's, on every think (`think_interval_seconds` 0.5).

**Data:** which early pairs the rule kites (Windrunner range 4, base speed 3.5; Talon range 6, base speed 2.5):

| Water unit (range, base speed) | Windrunner kites it | Talon kites it |
| --- | --- | --- |
| Pummeler (1, 3.0) | yes (gap 3; 3.5 >= 2.4) | yes (gap 5; 2.5 >= 2.4) |
| Shellback (2, 1.8) | yes | yes |
| Reef Runner (4, 4.1) | no (gap 0) | no (2.5 < 3.28) |
| Spoutling (5, 3.0) | no (gap -1) | no (gap 1) |

**Instrument (scratch, never committed).** To measure what the AI's fights look like, I added the D.6.2 rule with the `aggressive_s5` values to a local copy of `FightRunner` (a kite pass for both seats every 10 ticks, attack-move re-targeting unchanged every 2 s), switched on by an environment variable, and ran it on a scratch copy of the data with a flat arena of 96 x 48 tiles (twice the default length, so kiting units are not pinned against the arena edge at once) and eight army values (300, 450, 600, 750, 900, 1200, 1500, 2000), both seats. The edit was reverted before commit. Limits: the AI's other orders (stance orders, fall-back, focus) are not reproduced, and the arena is still smaller than a map; read it as the direction and size of the effect, not an exact copy of a game fight. Results (fights whose two values differ by more than 15 percent left out, as the harness does):

| Skai side | Water side | attack-move: Water wins, mean margin | AI kite rule: Water wins, mean margin |
| --- | --- | --- | --- |
| Windrunner | Pummeler | 16 of 16, +0.61 | **0 of 16, -0.61** |
| Talon Marksman | Pummeler | 16 of 16, +0.56 | **0 of 16, -0.71** |
| 3 Windrunner + 2 Talon (`skai_early`) | Pummeler | 10 of 10, +0.58 | **0 of 10, -0.64** |
| `skai_early` | 3 Pummeler + 1 Shellback (`water_early`) | 14 of 14, +0.81 | **0 of 14, -0.53** |
| Windrunner | Shellback | 16 of 16, +0.94 | 16 of 16, +0.89 |
| `skai_early` | Shellback | 8 of 8, +0.95 | 8 of 8, +0.81 |
| `skai_early` | 4 Reef Runner + 1 Shellback (`water_rs`) | 14 of 14, +0.70 | 14 of 14, +0.70 |
| Windrunner | Reef Runner | 9 of 16, -0.02 | 9 of 16, -0.02 (not kited) |
| Talon Marksman | Reef Runner | 12 of 16, +0.05 | 12 of 16, +0.05 (not kited) |
| `skai_early` | Reef Runner | 7 of 12, +0.04 | 7 of 12, +0.04 |

Readings (measured unless marked): the **Pummeler, Water's plan core (`composition` weight 30 of 60 in `aggressive_s5`), loses every early fight the AI kites, by 0.6 to 0.7 of value, at every army value from 300 to 2000**. The Shellback still wins (inference: its dome absorbs shots fired from outside its 3-tile radius, and kiting keeps the shooters outside it; the 12 dome stalemates of the 18 Nov sweep are the same effect). The Reef Runner is not kited and is an even trade, nothing more.

### 1.3 Reach, production and workers (data)

| Item | Water | Skai |
| --- | --- | --- |
| Dry-ground speed of the early units (water units x `water_unit_dry_speed_multiplier` 0.85) | Reef Runner 3.485, Pummeler and Spoutling 2.55, Shellback 1.53, Tender 2.0 | Windrunner 3.5 (4.2 at 5 momentum stacks), Talon 2.5 (3.0), Fledgling 2.5 |
| Producer of the early unit that wins under kiting | Shellback: only the Shell Forge (150 common, 1 per base plan), 28 s each | Windrunner: every Roost (the supply building, built 2 to 3 times by 180 s in the traces); Talon: the Windspire |
| What the supply building trains | Tidepool: Pummeler (loses under kiting), Reef Runner (even), Spoutling (loses to every Skai unit), Tidecaller (unarmed) | Roost: Windrunner (wins), Roc Rider |
| Value produced per producer-second | Shellback 6.25, Pummeler 5.56, Reef Runner 4.17 | Talon 5.0, Windrunner 4.17 |
| Worker | Tender 50 health, 2.0 tiles/s | Fledgling 40 health, 2.5 tiles/s |
| Time for raiders to kill one worker | 1 Windrunner: 9 hits, 6.4 s; 4 Windrunners: 1.6 s | 1 Reef Runner: 7 hits, 4.8 s |

So: **no Water unit is faster than a Windrunner on dry ground** (the Reef Runner is 0.015 tiles/s slower); the one Water unit that beats Skai's early ground army under kiting (the Shellback) moves at 1.53 tiles/s on dry ground and comes from one building at 28 s each; Water's supply building, which the AI builds most often, trains nothing that wins against Skai's early units in the AI's fights. Water's workers are tougher than Skai's, so the worker numbers are not the weak point; the reach of the defenders is.

### 1.4 What the games show (trace, one game each)

Cumulative value trained and lost, from `reports/ai_pd/r2/trace_skai_vs_water.txt` (C2) and `reports/ai_m2/trace_skai_vs_water.txt` (B0 candidate); workers are Tenders and Fledglings:

| Game | t | Skai army trained / lost | Water army trained / lost | Water workers lost |
| --- | --- | --- | --- | --- |
| C2 | 180 s | 850 / 0 | 600 / 0 | 50 |
| C2 | 300 s | 2250 / 375 | 1600 / 200 | 250 |
| B0 candidate | 180 s | 1000 / 0 | 525 / 75 | 200 |
| B0 candidate | 300 s | 2250 / 225 | 1425 / 1200 | 600 |
| Water mirror (ai_m2), each side | 300 s | - | 1875 and 1650 | 0 and 0 |

First Tender loss by 120 s (B0 candidate) or 180 s (C2), to 1 to 3 Windrunners; Skai loses no worker in either game. In the B0-candidate game Water lost 4 Shellbacks and 2 Spoutlings between 270 and 300 s for 1 Windrunner, against a Skai army of 8 Talons and 11 Windrunners (about 1625 value) plus 2 Roc Riders trained in that line; Water's army before the fight was about 700 value. Cell medians (`ai_pd/r2`, 72 games): army supply at 300 s Skai 27, Water 18.5; Skai's first `Attack` stance at 230 to 300 s.

### 1.5 Where Water is behind, and why

**Measured:** at equal cost and equal time Water is not behind in a meeting fight (1.1). It is behind in three fight-level places in the first 300 s:

1. **Against kiting ranged units on dry ground, the Pummeler loses by 0.6 to 0.7 of value** (1.2). This is a melee unit against faster ranged units off its water, a designed weakness (P-005 1.1: "melee fighter"; on flood water it moves 1.15 to 1.3 times as fast). This spec does not change it (Q-3).
2. **The Tidepool, Water's supply building and most numerous producer, trains no unit that wins against Skai's early units in the AI's fights.** Its only kite-proof armed unit, the Reef Runner, is an exact copy of the Windrunner in a fight (same health, damage, cooldown, range and cost: an even trade, 9 of 16), while the Windrunner also hits air, gains momentum and is marginally faster on dry ground. Skai's supply building trains the Windrunner. This is the one roster hole in the first 300 s that a number can close.
3. **Reach.** The unit that does win (the Shellback) is slow (1.53 on dry ground) and comes from one Shell Forge at 28 s each, so Water's winning army is small and arrives late and piecemeal (data 1.3; trace 1.4).

**Inference:** the game-level loss runs: 1 to 3 Skai raiders, mostly Windrunners, reach Water's fields at about 120 to 180 s; the units that can reach them in time (Reef Runner) only trade even and the ones that would win (Shellback) cannot reach them; Tenders die and are replaced (Water's worker spend matches Skai's but its income falls), Water's army is 50 to 70 percent of Skai's value by 180 to 300 s, and Skai's larger ball, with Roc Riders from about 300 s, overruns the base (about half of Water's worker losses fall there, `ai_production_defence.md` R2.0.2). Water's AI also adds to it: its plan composition is half Pummelers, the counter-mix's predictor is an attack-move fight simulation that does not model kiting (it rates the Pummeler +0.6 against Windrunners), and the Reef Runner is not in Water's plan composition at all (it is built as the scout). Those are AI matters, out of this spec (Q-4).

### 1.6 What this spec does not try to fix

- The Pummeler's kiting weakness (designed; Q-3).
- Water's anti-air once Roc Riders arrive (about 300 s and later; P-005 H1, conditional water anti-air already built). Outside the 300 s window (Q-7).
- The AI's unit choice, production on a single Shell Forge, and its raid rules (AI work; `ai_production_defence.md` R2 and the paused P-006 packages).
- Tender durability (Q-5): Water's worker is already tougher than Skai's, and the fight mode does not play workers, so a change could not be measured at fight level.

### 1.7 Runs behind the measured numbers (designer's worktree, `cdff393`, Release)

| Run | What | Result |
| --- | --- | --- |
| S0 | `--fights` default settings | 780 fights, identical to `fights_2026-11-18` (0 records differ); `FIGHTS fights=780 decided=765 draws=0 stalemates=13 unresolved=2 mismatched=0 checks_passed=2 of=8 dome_absorbed=12` |
| K1 | scratch kite instrument, flat 300/750/1500, units Windrunner, Talon, Pummeler, Shellback, Reef Runner, Spoutling plus `skai_early` and `water_early`; with and without the kite pass | 168 fights each; the flip of 1.2 at all three values |
| K2 | as K1 with eight values and `water_rs` added | the table of 1.2 |

## 2. The change

### 2.1 Change list

| # | File | Key | Old | New | Reason | Expected fight-level effect |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `rts/data/units/water/reef_runner.json` | `max_health` | 60 | **66** | Hole 2 of 1.5: the Tidepool gets one early unit that wins against Skai's early ground units at equal cost, is not kited by them (range 4, base speed 4.1) and is fast enough to reach a raid. The Windrunner keeps everything that makes Skai mobile: air targeting, momentum, the better dry-ground speed. | Reef Runner against Windrunner from an even trade (9 of 16, -0.02) to a modest win (designer's run: 16 of 16, +0.23); against Talon +0.05 to +0.17; against `skai_early` +0.04 to +0.19; unchanged with or without kiting. 6 of 780 standard-sweep results flip, all Reef Runner fights (section 3.3 G-1). No other unit's fight changes. |

That is the whole change list: one value, 10 percent. The arithmetic: a Windrunner now needs 11 hits (8.0 s) to kill a Reef Runner, and a Reef Runner still needs 10 hits (7.2 s) to kill a Windrunner, so one beats one with 6 health left; in larger fights the edge compounds to about a quarter of the army's value.

### 2.2 Text that changes with it (no other number)

- `reef_runner.json` line 1, the sentence "Water counterpart of skai.windrunner: cheap, fast, light, weak." becomes "Water counterpart of skai.windrunner: cheap, fast and light; a little sturdier than the Windrunner, so it wins their equal-cost fight, while the Windrunner keeps air targeting, momentum and the better dry-ground speed (rts/docs/specs/roster_numbers_1.md)."
- `reef_runner.json` `$doc`: append " Health raised to 66 by rts/docs/specs/roster_numbers_1.md section 2 so it wins the equal-cost fight against the Windrunner."
- The comment line above `max_health` becomes exactly:
  `// Health at full; 66 (was 60) so it wins the equal-cost fight against skai.windrunner by about a quarter of army value while not being kited by it. Unit: health points. Range: 1 to 100000 (design placeholders sit between 40 and 160). Source: rts/docs/specs/roster_numbers_1.md section 2.1 (was P-004 / skirmish_ai_v3.md A.3); placeholder.`
- `rts/data/scripts/fights/matchups.json`, entry `water.reef_runner`: `"job": "scout"` becomes `"job": "scout and field defence"`; its comment line keeps its form with `Source: P-005 1.1 table, developer decision 1 (rule 7) and rts/docs/specs/roster_numbers_1.md; fights run <date of the builder's run>, data digest <new digest>.` The `wins` and `loses` stay (`water.spoutling`, `skai.roc_rider`; the designer's run measures both unchanged).

### 2.3 Every value tried (designer's worktree, uncommitted edits on scratch copies of the data, all reverted)

Columns: Water wins and mean margin for Reef Runner against Windrunner / Talon / `skai_early` (and the Pummeler rows where the trial touched it). "K1" is 3 values, "K2" 8 values (see 1.7). Kite and no-kite results were identical in every Reef Runner row.

| Trial | Edit | Set | Result | Kept? |
| --- | --- | --- | --- | --- |
| base | none | K2 | Reef: 9/16 -0.02, 12/16 +0.05, 7/12 +0.04 | baseline |
| T1 | Pummeler `speed_tiles_per_second` 3.0 to 3.5 | K1 | kite: Talon against Pummeler 0/6 -0.75 to 6/6 +0.58 (only because 2.5 < 0.8 x 3.5 switches the AI's kite rule off: an AI threshold, not a fight); Windrunner against Pummeler 0/6 -0.57 to 0/6 -0.63; `skai_early` against `water_early` 0/6 -0.54 to 5/6 +0.20 | no: the gain is the AI rule's speed cut-off, and Skai's raider still wins |
| T2 | Pummeler `max_health` 110 to 130 | K1 | kite: Windrunner against Pummeler -0.61, Talon -0.66 (both still 0/6); no-kite margins grow to +0.64 to +0.68 | no: no help where Water loses, more dominance where it already wins (H3) |
| T3 | Reef Runner `max_health` 60 to 70 | K1 then K2 | K2: 16/16 +0.31, 14/16 +0.22, 9/12 +0.25 | no: larger than needed; +0.31 against its own mirror unit is close to the 0.35 cap of section 3.2 |
| T5 | `tuning/terrain.json` `water_unit_dry_speed_multiplier` 0.85 to 0.95 | K1 | kite: `skai_early` against `water_early` -0.54 to -0.44, Windrunner against `water_early` -0.34 to -0.13, Pummeler rows still 0/6; Reef against Windrunner 6/6 +0.07 | no: touches every Water unit and Water's off-water identity for a small gain |
| T6 | Reef Runner `max_health` 60 to 66 | K2 | 16/16 +0.23, 12/16 +0.17, 9/12 +0.19 | **yes** |
| T7 | Reef Runner `weapon.damage` 6 to 7 | K2 | 16/16 +0.23, 14/16 +0.27, 8/12 +0.21 | no: same edge against the Windrunner as T6 but a larger one against the Talon, and it raises the Reef Runner's raid damage on Fledglings (6 hits instead of 7) |
| T9 | Reef Runner `max_health` 60 to 63 | K2 | 16/16 +0.14, 12/16 +0.06, 8/12 +0.12 | no: near the edge of the bars; a smaller rounding of the same fight could put it back at even |
| F66 | T6 on the full default sweep (780 fights) and the 60 HQ floor trials | S0 settings | 87 records differ (all with a Reef Runner side), 6 results flip (listed in G-1), checks 2 of 8 as before with identical detail lines, one HQ trial moves (Reef Runner x30 against the Rooted Grand Shell, 17.4 s to 17.3 s) | confirms T6 |
| H66 | T6, harness smoke and economy smoke | default scripts | `STATE_HASH=df73e9584e791bc0` (was `207be73eaa460b3c`), `STATE_HASH=b724ff589cd6aa1b` (was `98b2e00d9babe167`); smoke still `OUTCOME undecided tick=599` | the digest moves every hash (section 4) |
| P66 | T6, `--predict-fights --from reports/fights_2026-11-18/fights.jsonl --profile aggressive_s5` | committed sweep | accuracy still 0.735 (562 of 765), but `predict.jsonl` differs from `reports/predict_fights/predict.jsonl` from line 15 (the predictor reads the new health) | a pinned file moves (section 4) |

Considered and not run: Reef Runner speed (dry 3.485 against the Windrunner's 3.5; a defender does not need to outrun a raider that stops to shoot, and a faster Reef Runner raids Fledglings harder); Tender health (the fight mode does not play workers, Q-5); Shellback build time or speed (helps production and reach, but strengthens the unit that already wins by 0.8 to 0.9 and fixes nothing the AI does with it); any Skai change (the brief prefers raising Water's options, and the Windrunner's numbers are its identity).

## 3. Measurement plan (fixed before the builder runs anything; nothing is tuned after a result)

### 3.1 The bar set: `rts/data/scripts/fights/roster_numbers_1.json`

A copy of `rts/data/scripts/fights/fights.json` with only these keys changed (every other key, value and comment as in `fights.json`; header line 1 `// rts/data/scripts/fights/roster_numbers_1.json: the bar set of rts/docs/specs/roster_numbers_1.md section 3: flat fights at army values the designer never ran, three early compositions, a longer arena. Read by rts/tools/BalanceHarness/FightSettings.cs only through --fight-settings; outside the content digest.`; each changed value's comment ends `Source: rts/docs/specs/roster_numbers_1.md 3.1; placeholder.`):

| Key | Value | Why |
| --- | --- | --- |
| `$doc` | "Bar set of roster_numbers_1: flat fights at held-out army values, early compositions, arena 96 x 48." | |
| `units` | `[]` (every combat unit) | the matchups file needs every combat unit in the sweep |
| `compositions` | `skai_early` {`skai.windrunner` 3, `skai.talon_archer` 2}; `water_early` {`water.grasper` 3, `water.shellback` 1}; `water_rs` {`water.reef_runner` 4, `water.shellback` 1} | the early armies of the diagnosis |
| `cases` | one case `{ "id": "flat", "kind": "flat", "army_values": [375, 525, 1050, 1350, 1800, 2400] }` | values the designer never ran (K1, K2 and S0 used 300, 450, 600, 750, 900, 1200, 1500, 2000); chosen before any run |
| `arena_width_tiles` | 96 | as K2 |
| `arena_height_tiles` | 48 | as K2 |
| `max_fight_seconds` | 300 | larger armies (as `heldout2.json`) |
| `rule_army_value` | 1050 | must be a value of the flat case |

Command (after `dotnet build rts/RtsGame.sln -c Release`):

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --fights --fight-settings rts/data/scripts/fights/roster_numbers_1.json --jobs 4 --out <dir>
```

Run it twice: **before** (on `main` with only the new settings file added, Reef Runner health 60) and **after** (with the change). The fights are deterministic and no random draw affects a fight outcome (checked: `--only flat/750/skai.windrunner/water.reef_runner` with `--seed` 20261118, 1 and 999 gives the same result, value left and length; only the `seed` field differs), so each (value, seat) pair is one fight and a re-run gives the same line. The checks the harness prints for this set are not bars.

### 3.2 Main bars (from the "after" run; "valid" = not marked `mismatch`; margin = Water's `left` minus Skai's `left`, mean over the valid fights of the pair, both seats)

| ID | Pair | Pass when | Designer's trial (K2, other values) | "Before" expected |
| --- | --- | --- | --- | --- |
| RB-1 | Reef Runner against Windrunner | at least 8 valid fights; Water wins at least 0.80 of them; mean margin from +0.10 to +0.35 | 16/16, +0.23 | about even (9/16, -0.02 in K2) |
| RB-2 | Reef Runner against Talon Marksman | at least 8 valid fights; Water wins at least 0.50; mean margin from 0.00 to +0.35 | 12/16, +0.17 | +0.05 |
| RB-3 | Reef Runner against `skai_early` | at least 6 valid fights; mean margin from +0.05 to +0.35 | 9/12, +0.19 | +0.04 |
| RB-4 | `water_rs` against `skai_early` | at least 6 valid fights; Water wins at least 0.80 (guards that the Reef Runner and Shellback army the diagnosis points at holds) | 14/14, +0.70 | the same or lower |

The upper limit +0.35 is P-005 rule 3 read for a soft counter: the Reef Runner may win its fight, not erase the Windrunner. **Power, stated honestly:** with 12 valid fights, 10 wins rejects "even" at a one-sided sign-test level of 0.019, but the fights are deterministic points across army values, not random draws, so this is descriptive. The held-out values guard against a value chosen to fit the designer's sample. A pair with fewer valid fights than its minimum is **not measured**: stop and report, do not add values.

### 3.3 Guards (all must hold)

| ID | Guard | How |
| --- | --- | --- |
| G-1 | In the default sweep (`--fights`, no settings flag), only fights with a `water.reef_runner` side change, ignoring `seed` and `state_hash`. Expected: 87 records differ and exactly these 6 results flip: `flat/750/skai.windrunner/water.reef_runner` (seat0 to seat1), `flat/1500/water.reef_runner/skai.windrunner` (seat1 to seat0), `high_ground/1500/water.reef_runner/skai.stormcaller` (seat1 to seat0), `flooded/1500/water_ai_mix/water.reef_runner` (seat0 to seat1), `choke/1500/skai.windrunner/water.reef_runner` (seat0 to seat1), `choke/1500/water.reef_runner/skai.windrunner` (seat1 to seat0) | compare the new `fights.jsonl` with `reports/fights_2026-11-18/fights.jsonl` record by record. A difference in any fight without a Reef Runner side: **stop** |
| G-2 | Design-rule checks unchanged: `checks_passed=2 of=8`, the same two pass, and every `CHECK` detail line is identical to S0 | stdout of the same run |
| G-3 | HQ floor: only Reef Runner trials change, each by at most 0.5 s, and none crosses the 15 s floor in either direction (expected: Reef Runner x30 against Rooted Grand Shell 17.4 to 17.3 s) | `hq_floor.jsonl` against the 18 Nov file |
| G-4 | Water mirror: the Pummeler still beats the Reef Runner in every default-sweep fight it won before (4 flat, 2 choke, 2 flooded, 1 high ground; designer's run: margins 0.47 to 0.90 after, 0.56 to 0.91 before); the Reef Runner's `wins` and `loses` in `matchups.json` still agree (`matchups_agree` fails only on the Spoutling, as now) | same run |
| G-5 | Skai-only fights: every Skai against Skai record identical (covered by G-1, named so it is read) | same run |
| G-6 | Response suite: no scenario that passes before fails after | `--response --profile aggressive_s5` on `main` and on the change |
| G-7 | Full suite green after the re-pins of section 4, and nothing outside section 4's list needed a new expected value | `dotnet test rts/RtsGame.sln -c Release` |

### 3.4 AI balance run (information only, not a bar)

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5 --maps prototype_1v1 --matchups skai_vs_water --fights-from <the new default sweep fights.jsonl> --jobs 4 --out <dir>
```

Run before (on `main`, with `--fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl`) and after. Report: Skai's and Water's wins of 72; median Water `worker_losses`, army supply at 300 s, game end; Reef Runner trained per Water slot-game (from the unit-use table and the records).

**Expectation, stated before the run:** Skai still wins at least 66 of 72, most likely 72 of 72, and Reef Runner counts barely move. Reason: the change reaches the AI only through the scout, because the Reef Runner is not in Water's plan composition and the counter-mix's predictor already ranks the Shellback and Pummeler above it (1.5). A result outside this expectation is reported and explained, never tuned against. Whether the AI should be taught to use the Reef Runner is Q-4.

### 3.5 Revision rounds

**One.** If a main bar or a guard fails, the builder stops, reports the measured values, and changes nothing. The designer may then choose one replacement value **from section 2.3's tried values only** (for RB failures on the low side, 70; on the high side, 63), and the bars of 3.2 are re-run once on the same held-out set. If that fails too, the result goes to the developer; no third value.

### 3.6 Report

`rts/docs/reports/roster_numbers_1/README.md` (path header): commit, commands, wall time, the four RB rows (before and after), G-1 to G-7 each with its evidence, the AI run against its expectation, and the files `bars_before/`, `bars_after/`, `sweep/` (default sweep after the change, the new committed sweep for later work), `balance_before/`, `balance_after/`.

## 4. What moves

### 4.1 Re-pin or regenerate (expected to change; the builder records old and new values in the report and in each test's comment, "re-pinned for roster_numbers_1")

| Pin | Where | Old |
| --- | --- | --- |
| smoke hash | `rts/tests/Sim.Tests/Harness/HarnessUnitTests.cs:177` | `207be73eaa460b3c` (designer's run: new `df73e9584e791bc0`) |
| economy_smoke hash | `HarnessUnitTests.cs:179`, `rts/tests/Sim.Tests/Economy/WorkerConstructionTests.cs:739` | `98b2e00d9babe167` (designer's run: new `b724ff589cd6aa1b`) |
| `aggressive_s5` trace | `rts/tests/Sim.Tests/Ai/FightSimTests.cs:113` | end tick 9505, `837f8e7118a9c9de` (end tick may move too) |
| `aggressive_s3` trace | `FightSimTests.cs:114` | end tick 9223, `c04bdcc0d03b3910` |
| `V2ReferenceHash` | `rts/tests/Sim.Tests/Ai/SkirmishAiTests.cs:882` | `0xdfc8991f885607f2` |
| `BlindTraceHash` | `SkirmishAiTests.cs:917` | `846056c1a9efe5f4` |
| Formula predictor output on the committed sweep | `FightSimTests.OldProfiles_PredictJsonlByteIdentical` reads `rts/docs/reports/predict_fights/predict.jsonl` | regenerate that file with the test's own command (accuracy 0.735 is expected to stay; P66) and say in `predict_fights/README.md` that it was regenerated for roster_numbers_1 |
| Recorded ablation and self-game hashes | `rts/tests/Sim.Tests/Ai/AiHarassTests.cs` `H5_AblationPlaysThePreAiHGame`, `AiPlacementTests.cs` `P6_AblationPlaysThePreAiPGame`, `AiVarietyTests.cs` `S3SelfTraceHash` (`4d71b4da89d3db91`), `SkirmishAiTests.cs` `AblationBlind_TracedGame_IsUnchangedByPackageC2` | the recorded values in each test |
| Predictor advantages | `rts/tests/Sim.Tests/Ai/AiArmyMixTests.cs` `C2_1_WaterAgainstPummelers`, `C2_2_WaterAgainstRocRiders`, `C2_5_TracedSituation` (`AssertAdvantages` strings) | re-pin **only** the `water.reef_runner` entries; if any other unit's advantage or any `AssertMix` weight changes, **stop** |
| Wall-distribution pin | `rts/tests/Sim.Tests/Ai/AiMechanicTests.cs` `V2_WallDistribution` (and the P1 counts beside it) | re-pin only if it fails (it passed in the designer's run) |
| Any other test that fails only because it reads the Reef Runner's health or a game played with it | wherever the suite reports it | re-pin, list each in the report; a failure that is not explained by this change is a **stop** |
| Unity hand-off table | `rts/docs/HANDOFF_UNITY.md` lines 23 and 24 | coordinator updates with the new smoke and economy_smoke hashes |
| `tutorial_01` headless hash `0596b0ca3c13635d` (STATUS, campaign docs) | wherever pinned | re-pin if the campaign runner reports a new hash (the digest is in the hash) |

**Designer's suite run (T6 applied to `rts/data`, Release, 24 Nov):** Net 33 of 33 and Replay 30 of 30 pass; Sim 1260 of 1273 pass and these 13 fail: the 12 pins above (`SingleRunMode_IsUnchanged`, `Wc22_EconomySmoke`, `OldProfiles_FixedSeedGamesKeepTheirHash`, `AiVsAi_FinalHash_MatchesTheV2Reference`, `AblationBlind_TracedGame_IsUnchangedByPackageC2`, `H5_AblationPlaysThePreAiHGame`, `P6_AblationPlaysThePreAiPGame`, `S3SelfGame_IsUnchangedByPackagesRAndL`, `OldProfiles_PredictJsonlByteIdentical`, `C2_1_WaterAgainstPummelers`, `C2_2_WaterAgainstRocRiders`, `C2_5_TracedSituation`) and **one that is not a pin: `AiMechanicTests.Wl13_ExitGame`** (section 4.5).

The fight predictor has no fitted parameters in data (`FightSim` ports the game rules); its committed held-out sets (`heldout.json`, `heldout2.json`) are settings files and **do not change**. The sweeps measured with them (`reports/predict_v2`, `predict_v2b`) describe the old Reef Runner in their Reef Runner rows (for example B5's "Windrunner against Reef Runner 0 of 10", which was a coin-flip mirror and is now a Reef Runner win); they are not re-run by this spec.

### 4.2 Must not change

- Any file under `rts/src` or `rts/tools` in RN-1 (the scratch kite instrument was never committed; RN-0 touches only `AiBrain.TrainWorkers`).
- `rts/data/scripts/fights/fights.json`, `heldout.json`, `heldout2.json`, `predict.json`, every AI profile, every map, every other unit file, every tuning file.
- The committed input file `rts/docs/reports/fights_2026-11-18/fights.jsonl` (tests read it as a fixed input; it stays as the record of 18 Nov).
- The design-rule check results (G-2), every Skai against Skai fight (G-5), and every fight without a Reef Runner side (G-1).

### 4.3 For the sim-reviewer

RN-1 changes no code, so its review is of the data and the pins (RN-0 is reviewed as an AI change): that the only data value that changed is `water.reef_runner` `max_health`; that every re-pinned test moved because of the content digest or the Reef Runner and nothing else (G-1, G-5 are the evidence); that no test was loosened (an `Is.EqualTo` stays an `Is.EqualTo`).

### 4.4 Reports that become out of date

Every AI report whose hashes or games were played on the old data no longer reproduces on `main`: `balance_first`, `paired_first`, `pool_first`, `ai_*` (including `ai_pd/baseline`, `ai_pd/r2`, `ai_m2`, `ai_econ`, `ai_threat/*`), `sat_m`, `worker_construction_wc2` and `wc3`, `missing_roster_r3`. Their conclusions about AI rules stand as history; their numbers are not a baseline for later work, which needs a fresh baseline after this change. Among the fight reports, `fights_2026-11-18` is out of date in its 87 Reef Runner records (the other 693 still describe the game), and `predict_fights`, `predict_v2`, `predict_v2b` are out of date in their Reef Runner rows. The new default sweep in `reports/roster_numbers_1/sweep/` is the current one.

### 4.5 An existing AI fault the changed game exposes (WL-13)

With the change, the WL-13 exit game (`ai_vs_ai`, both `aggressive_s5_m`, prototype_1v1) still ends decided and is still deterministic (two runs, identical hashes), but its report reads `rejected other than site_occupied: 1 ([train_unit:hq_not_rooted, 1])`. **Cause, confirmed in the code (`cdff393`):** in `AiBrain.Decide` (`AiBrain.cs` lines 260 to 272) `turn.Hq()` can emit `UprootHq` (rule H relocation, line 1549, after `BeginRelocation` sets `_memory.RelocationActive`), and later in the same think `turn.TrainWorkers()` (line 3309) tests only the view's `_hq.HqState`, which still reads `Rooted`, so it queues a worker that the simulation then rejects with `hq_not_rooted` (the uproot is applied first). The fault exists on `main`; the old game simply never relocated its HQ on that think. It is not a roster matter, and the test must not be loosened. Default (Q-8): package **RN-0**, before RN-1: in `TrainWorkers`, also return when `_memory.RelocationActive` is true (one condition; no new key, no new draw). With the current data the exit game has no rejection of this kind, so RN-0 should move no pin; it would move one only if the fault already fires in a pinned game (the skipped worker order also frees its reservation for the rest of that think). The builder runs the full suite after RN-0 and reports any pin that moves, and the sim-reviewer reviews RN-0 like any AI change.

## 5. Build package RN-1 (one builder session; then the sim-reviewer)

Order (after RN-0 of 4.5 is merged, or after the developer answers Q-8 otherwise):
1. Add `rts/data/scripts/fights/roster_numbers_1.json` (3.1). Build. Run the "before" bar set, the default sweep and the AI run "before" (3.4) on this commit; save the outputs.
2. Make the data and text change of section 2 (2.1 and 2.2).
3. Run the "after" bar set, the default sweep and the AI run "after". Judge RB-1 to RB-4 and G-1 to G-5 from the outputs **before** touching any test.
4. If a bar or guard fails: stop (3.5). Otherwise re-pin per 4.1, run G-6 and G-7.
5. Write the report (3.6). Changelog bullets to the coordinator.

Acceptance tests (NUnit, in `rts/tests/Sim.Tests`):

1. `ReefRunnerTests` (existing file): add `Assert.That(unit.MaxHealth, Is.EqualTo(Fix64.FromInt(66)))` (`UnitDefinition.MaxHealth` is a `Fix64`), and that speed (4.1), damage (6), cooldown (0.8), range (4), cost (75 common, 0 rare) and supply (1) are unchanged.
2. `FightTests` (or the file holding `--only` checks): `--fights --only flat/1500/water.reef_runner/skai.windrunner` exits 0 and its line has `"result":"seat0"` (the designer's run: seat 0 wins with 0.218 left; pin the result and `left0` to three decimals).
3. `FightTests`: `--fights --only flat/1500/skai.windrunner/skai.talon_archer` gives the same line as `reports/fights_2026-11-18/fights.jsonl` with `seed` and `state_hash` removed (a fight without a Reef Runner side is untouched).
4. `HeldOutTests` (or a new `RosterNumbersTests`): `roster_numbers_1.json` parses with the shipped data, plans its fights with no army rejected by the arena, uses only army values that no committed set used (`fights.json`, `heldout.json`, `heldout2.json`), and the matchups file accepts it; nothing is played.
5. The re-pins of 4.1, each with its comment.

## 6. Out of scope

Any Skai number; any other Water number; the Pummeler's kiting weakness; anti-air after 300 s; the AI (composition, predictor, kiting, raid rules, production); a kite control in the fight mode (Q-2); Tender numbers; maps; new units (section 7).

## 7. Is a new unit needed? No, so no proposal (P-008) was written

Every Water hole found in the first 300 s is either closed by a number or is not a roster hole:

- The Tidepool's lack of a winning early unit: closed by one number (2.1).
- Reach on dry ground: the Reef Runner reaches a raid at 3.485 tiles/s, and a defender does not need to outrun a raider that stops to shoot. If the developer wants Water to catch Windrunners in pursuit, that is one more number (Reef Runner speed), not a unit.
- The Pummeler kited: a designed weakness of a melee flood fighter on dry ground.
- Anti-air from 300 s: P-005 H1 and the conditional water anti-air; outside the window, and the Spoutling and Shellback numbers are the first levers there, not a new unit. A Water flyer was rejected by the developer and is not proposed.
- The rest (single-Forge production, the predictor that does not model kiting, half-Pummeler plans, raid response) is AI behaviour.

## 8. Questions for the developer

| # | Question | Default this spec runs on |
| --- | --- | --- |
| Q-1 | May the Reef Runner become Water's early field defender that wins the equal-cost fight against the Windrunner by about a quarter of value, while the Windrunner keeps air targeting, momentum and the better dry-ground speed? | Yes. |
| Q-2 | The fight mode plays plain attack-move; the AI kites, and that flips the Pummeler's early fights from +0.6 to -0.6. Should the fight mode get an opt-in kite control (the AI's D.6.2 rule with profile values, tools only, a separate small spec) so roster bars measure the fights the game plays? | Yes, as a separate harness spec after this one; this spec does not need it (the Reef Runner is not kited, and its results were identical with and without kiting). |
| Q-3 | Accept the Pummeler's heavy loss to kiting ranged units on dry ground as its designed weakness (it is the flood fighter)? | Yes; no Pummeler change. |
| Q-4 | Teach Water's AI to use the Reef Runner (plan composition, and a predictor that models kiting)? Without it the AI run will barely move. | Not in this spec; decide after RN-1's report. |
| Q-5 | Change Tender numbers for worker safety? | No: the Tender is already tougher than the Fledgling and the fight mode cannot measure worker fights. |
| Q-6 | Accept that the AI balance run is reported as information and may still read 72 of 72 for Skai? | Yes. |
| Q-7 | A second numbers pass for anti-air after 300 s (Spoutling, Shellback off water)? | Later, as `roster_numbers_2`, only if the developer asks. |
| Q-8 | The changed game exposes an existing AI fault (a worker queued at an HQ that the same think uproots; 4.5). Fix it as RN-0 before RN-1, rather than allowing `hq_not_rooted` in WL-13? | Yes: RN-0, one condition in `TrainWorkers`, reviewed by the sim-reviewer. |

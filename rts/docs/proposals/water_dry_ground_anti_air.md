<!-- rts/docs/proposals/water_dry_ground_anti_air.md -->
<!-- Proposal (28 Nov 2026) for the developer: does Water need a new unit to answer Skai's Roc Rider on dry ground? Restates the evidence with numbers, checks the premise against the unit data, tests three cheaper data-only alternatives and one new unit (Archerfish, air-only) on scratch fight sweeps and 36-game trials on a spent seed, recommends building neither for now (both made Water's AI games worse), gives the cheapest option (Reef Runner air targeting off the flood) and the fallback unit in full for a decision, the knock-on work, a measurement plan fixed before any run (seed 20261213, guard RG) and the open questions with defaults. No code or data is changed by this file. Index: rts/docs/proposals/README.md. -->
# Proposal: a dry-ground answer to the Roc Rider for Water

Written 28 Nov 2026 against `main` at `9322961`. Every number is a **placeholder**. Nothing here is built until the developer decides.

## Decision asked (three lines)

1. **Add no unit and change no unit now.** I trialled both the cheapest fix (Reef Runner hits air on any ground, one key) and a new air-only Tidepool unit (Archerfish) for 36 games each. Water still won 0, and its army at 300 s got **worse** (7 better against 29 worse pairs, and 3 against 33). The AI moved spending from Coral Turrets, Spoutlings and Reef Runners into an early army, which then lost the 180 to 300 s fight.
2. **The premise is partly wrong.** Water already has dry-ground anti-air. Since `roster_numbers_2` the Spoutling trades about even with the Roc Rider (-0.10), the AI's Water mix beats it (+0.55), and the Leviathan beats it (+0.8) but the AI never builds one. The real hole is narrower: the Pummeler and the Reef Runner, 70 percent of Water's army, cannot shoot a flyer off the flood.
3. **If you still want a roster change**, the Reef Runner key (V1) is the cheaper option and the Archerfish is the fallback. Section 5 fixes the measurement for V1 and predicts FAIL on the army bar. My recommended next step is AI work instead (Q-3).

## 1. Evidence: what Water cannot do today

### 1.1 The premise, checked against the data

The premise "no dry-ground answer to the Roc Rider" is **partly wrong**. These Water units can hit air on any ground (`can_target` lists `air`, files in `rts/data/units/water/`):

| Unit | Hits air on dry ground | Against `skai.roc_rider`, flat sweep, mean margin (Water value left minus Skai's) | Trained per AI game (median, `ai_midgame` baseline) |
| --- | --- | --- | --- |
| Spoutling | yes; drench grounds a flyer after 3 hits | **-0.10** (750: -0.20, -0.23; 1500: +0.16, -0.13) | 11 |
| Leviathan | yes (decision of 18 Nov) | +0.79 to +0.91 | **0** in every opening, including `water_leviathan_tech` |
| Coral Turret, Grand Shell | yes (static, HQ) | not in the sweep | 2 to 3 turrets |
| `water_ai_mix` (3 Pummeler, Shellback, Brinecaster, Spoutling) | through the Spoutling's drench | **+0.55** (+0.42 to +0.69) | |
| Reef Runner, Shellback | **only on a flooded tile** (`can_target_air_on_water`) | Reef -1.00; Shellback -0.13 (stalemates; dome) | 24 and 10 |
| Pummeler, Brinecaster, Tidecaller | never | -1.00 | 27, 0, 0 |

Sources: `rts/docs/reports/roster_numbers_2/sweep/fights.jsonl` (current data; reproduced exactly by the designer's base run, 0 of 780 records differ) and `rts/docs/reports/ai_midgame/README.md`.

So since `roster_numbers_2` (Spoutling damage 10, health 90) the designated anti-air unit is close to an even trade with the Roc Rider at equal cost, and Water's standard mix beats it. What Water lacks is narrower:

- **Its two most-trained units cannot touch a flyer off the flood.** The Pummeler (27 per game) and the Reef Runner (24 per game) are about 70 percent of Water's trained army units (51 of 72 by median count). On dry ground a Roc Rider shoots them for free. The Reef Runner's Skai mirror, the Windrunner, hits air everywhere.
- **Its only dry-ground anti-air loses every ground fight.** Spoutling against Windrunner -0.36, Talon -0.47, `skai_ai_mix` -0.46 (flat). The AI builds about 11, and they die to the Windrunners around them.
- **The unit that beats Rocs outright is never built.** The Leviathan wins by +0.8 but 0 are trained per game in every opening (`ai_midgame` MG-7(c)). That is an AI matter, not a roster one.

### 1.2 Which Skai units, range, speed, terrain, time window

| Item | Value | Source |
| --- | --- | --- |
| Roc Rider | air, armoured, 160 health, kinetic 14 per 1.2 s, range 5, speed 4.0 (terrain ignored), cost 150 + 50 rare, 30 s | `skai/roc_rider.json` |
| Water speeds on dry ground (x `water_unit_dry_speed_multiplier` 0.85) | Reef Runner 3.49, Spoutling and Pummeler 2.55, Shellback 1.53 | `tuning/terrain.json` |
| Ranges | Reef Runner 4, Spoutling 5, Shellback 2; no mobile Water unit but the Leviathan (9) out-ranges the Roc | unit files |
| First Roc Rider | about 270 s; 11 to 17 per game, 13 median | `roster_numbers_2` 1.3, report; `ai_midgame` traces |
| Terrain | `prototype_1v1` is dry until Water floods it; fights at 210 to 450 s are mostly off the flood | `CONCEPT_BRIEF.md` (map round 20); traces |
| Army supply, Water / Skai | 180 s 12 / 13; 300 s 27 to 28 / 33; 450 s 9 to 11 / 67 to 68 | `roster_numbers_2` report; `ai_midgame` self-play |

### 1.3 What the games show, and what that means for this proposal

The 300 to 450 s collapse is not only the Roc. In `ai_midgame/traces/aggressive_s5_game0.txt` Skai's army at 450 s is 25 Windrunners, 9 Roc Riders, 5 Talons and 2 Thunderheads; over the game Skai trained 56 Windrunners and 17 Roc Riders and lost 36 army units to Water's 83. Game 1 is the same shape (53 Windrunners, 11 Rocs). In game 0 at 450 s the Rocs are 9 of 41 Skai army units (27 of 70 army supply).

**Inference, stated plainly:** at fight level Water's mix already beats Skai's mix (`water_ai_mix` against `skai_ai_mix` +0.69 flat) and beats the Roc (+0.55). Five measured changes did not move wins. A roster change can close the Reef Runner's air gap, but the evidence does not show that this gap is what decides the games. The trials in 2.1 and 3.3 agree: adding anti-air changed how the AI spends and fights, and those changes outweighed the unit's own effect.

## 2. The cheaper alternative, tested first

Three data-only variants on scratch copies of `rts/data` (designer's worktree, uncommitted, deleted after), default `--fights` sweep, compared record by record with the base run.

| Variant | Change | Records changed of 780 | Effect on the fights that matter (flat, mean margin) | Verdict |
| --- | --- | --- | --- | --- |
| **V1** | Reef Runner `can_target` gains `air`; `can_target_air_on_water` removed | 24, all Reef Runner against a flyer or `skai_ai_mix` | Against Roc **-1.00 to +0.16** (-0.03, +0.12, +0.28, +0.27); choke +0.23; high ground -0.30. Against Thunderhead -1.00 to -0.71 (still loses). Against `skai_ai_mix` -0.25 to -0.32 (shots go to the Roc). Flooded case unchanged (it already hit air there). Mean over the 42 flat cross-faction pairs +0.096 to +0.129. | Best at fight level: a soft counter, under the 0.35 cap, wet game unchanged. **Worse in AI games (2.1).** |
| V2 | Shellback the same | 41 | Against Roc -0.13 to **+0.97**, Thunderhead -1.00 to **+0.87**, `skai_ai_mix` +0.65 to +0.95; flat mean +0.181 | Rejected: hard counter, far over the cap. |
| V3 | Spoutling `range_tiles` 5 to 6 | 181 | Against Roc -0.10 to +0.03; Windrunner -0.36 to -0.38, Talon -0.47 to -0.42; `water_ai_mix` against Roc +0.55 to +0.63; flat mean +0.112 | Rejected: barely moves the Roc fight, still loses every ground fight, changes 181 records. |

**Arithmetic for V1** (damage table: kinetic against armoured 0.75, against light 1.0):
- A Reef Runner hits a Roc for 6 x 0.75 = 4.5: 36 hits to kill one (160 health), 5.63 damage per second.
- A Roc hits a Reef Runner for 14: 5 hits kill one (66 health), the fifth at 4.8 s; 11.67 damage per second.
- At 1500: 20 Reef Runners (1500) against 8 Rocs (1600). Kill rates per second: a Reef Runner kills 5.63 / 160 = 0.0352 Rocs, a Roc kills 11.67 / 66 = 0.177 Reef Runners. Square law: Water 0.0352 x 20² = 14.1 against Skai 0.177 x 8² = 11.3, Water wins with sqrt(400 - 5.03 x 64) = 8.8 of 20 alive (+0.44).
- The sweep measures +0.16. The same formula gives +0.28 for the Spoutling against the Roc, measured -0.10. The square law over-rates Water here by 0.28 to 0.38 (two points; used below as a correction, not a law).
- Kiting: the AI kites only at a range gap of 2 or more (`kite_min_range_gap_tiles`); Roc 5 against Reef 4 is a gap of 1, so neither side kites the other.

**Why V1 would fit the faction.** P-005 decision 5 says Water answers air "by grounding and spread anti-air". V1 spreads anti-air to the Tidepool's most-trained unit without a new unit, model or producer. The Windrunner, its mirror, already does exactly this. What V1 gives up is decision 3's link between the flood and Water's anti-air, for this one unit.

### 2.1 Trial games (spent seed, information for the decision, not a verdict)

`--balance --profile aggressive_s5 --maps prototype_1v1 --matchups skai_vs_water --games 36 --seed 20261127` (spent by `roster_numbers_2`), Release build at `9322961`, base data against scratch data, games paired by index (same seed and seats). Exact two-sided sign test on the decisive pairs; Water's view.

| Measure | Base median | V1 median | V1 better / worse (p) | Archerfish median (3.3) | Archerfish better / worse (p) |
| --- | --- | --- | --- | --- | --- |
| Water won | 0 of 36 | 0 of 36 | 0 / 0 | 0 of 36 | 0 / 0 |
| Water army supply at 180 s | 11 | 13 | 33 / 0 | 14 | 31 / 1 |
| Water army supply at 300 s | 26 | 17.5 | 6 / 28 | 17 | 3 / 32 |
| Army ratio Water over Skai at 300 s | 0.78 | 0.54 | **7 / 29 (0.0003)** | 0.49 | **3 / 33 (below 0.0001)** |
| Water army at 450 s (dead = 0) | 12.5 | 13 | 13 / 22 (0.18) | 9 | 13 / 22 (0.18) |
| Overrun second | 342.5 | 336 | 20 / 16 (0.62) | 330.5 | 18 / 18 |
| Coral Turret spend (common) | 300 | 200 | 20 pairs lower | 200 | 17 pairs lower |
| Reef Runners / Spoutlings trained | 24 / 10.5 | 22 / 8.5 | 24 and 27 pairs fewer | 16.5 / 7 | 32 and 31 pairs fewer |
| Skai Roc Riders trained | 13 | 12.5 | 19 / 14 | 13.5 | 18 / 16 |

**Reading (measured):** both changes produce the same pattern. Water fields more army at 180 s, has far less at 300 s, builds fewer turrets and other structures, and still wins nothing. **Inference:** once Water can shoot flyers with mobile units, the AI spends less on static anti-air and on the units it used as anti-air insurance, and it loses that army in the 180 to 300 s window (the kited trade of `roster_numbers_2` 1.2; the first Roc arrives at about 270 s). In AI games the roster change mostly changes the AI's choices, not the Roc fight. I did not trace the games to confirm the mechanism.

## 3. Fallback unit (only if Q-1 is answered "no" and you still want a unit)

One design. No second alternative: every other shape considered (an all-target unit like the Talon, which loses to the Roc and changes ground fights, on paper; a longer-range Spoutling, measured as V3) either changes Water's ground fights or does not move the Roc fight (V3).

**Archerfish** (placeholder name; the fish that knocks flying insects down with a jet of water, which fits "each unit type is a different sea creature" and the hydro-cannon look of `CONCEPT_BRIEF.md`). Job: dedicated anti-air that shoots **only flyers**, cheap, no rare cost, from the Tidepool, so it is on the field in the 270 s window. Because it cannot shoot ground or water units, it changes no ground fight anywhere, on wet or dry ground.

### 3.1 Stats (same fields as `water/spoutling.json`)

| Key | Value | Unit | Range | Rule |
| --- | --- | --- | --- | --- |
| id / display_name / faction_id | `water.archerfish` / `Archerfish` / `water` | | | placeholder name (Q-5) |
| kind / layer | `unit` / `water` | | | every Water unit is on the water layer; not a flyer |
| max_health | 70 | health points | 1 to 100000 | Spoutling before `roster_numbers_2`; dies to 5 Roc hits |
| speed_tiles_per_second | 3.0 | tiles per second | 0.1 to 20 | Spoutling (2.55 on dry ground) |
| supply | 2 | supply | 0 to 20 | Spoutling |
| radius_tiles | 0.4 | tiles | 0.1 to 3 | Spoutling |
| cost_common / cost_rare | 100 / 0 | common, rare | 0 to 10000 | no rare, so it is affordable at 270 s; V 100 |
| build_seconds | 20 | seconds | 0.05 to 3600 | Spoutling |
| armour_type | `light` | | armour types of `combat.json` | Spoutling |
| sight_range_tiles | 9 | tiles | 1 to 30 | Spoutling |
| weapon.damage | 10 | health points per hit | 0.01 to 100000 | sized against the Roc, below |
| weapon.damage_type | `drench` | | `combat.json` damage types | Water's own type: 1.0 against armoured flyers |
| weapon.range_tiles | 5 | tiles | 0.3 to 20 | equal to the Roc: no kiting either way |
| weapon.cooldown_seconds | 1.0 | seconds | above 0 | Spoutling |
| weapon.hit_delay_ticks | 2 | ticks | 0 to max | every ranged unit |
| weapon.can_target | `["air"]` | layers | at least one | the point of the unit |
| weapon.applies_drench | absent (false) | | | grounding stays the Spoutling's job |
| Producer | `water.tidepool` (`trains` list) | | | Water's supply building, the most numerous producer |
| Tech requirement | none | | | |

Efficiency check (`missing_roster.md` D2 rule, current Water pool): pool 0.077 damage per second and 0.88 health per value. Archerfish 0.10 and 0.70. More damage and less health than the pool, accepted because its damage reaches flyers only.

### 3.2 Paper maths against Skai (same method and correction as V1)

| Skai unit | Working | Square law | Expected after the 0.28 to 0.38 correction |
| --- | --- | --- | --- |
| Roc Rider | Archerfish hits 10 (drench 1.0): 16 hits, 10 dps; Roc hits 14 (kinetic, light 1.0): 5 hits, 11.67 dps. 16 Archerfish (1600) against 8 Rocs (1600): kill rates 10 / 160 = 0.0625 and 11.67 / 70 = 0.167; Water 0.0625 x 256 = 16.0 against Skai 0.167 x 64 = 10.7; alive sqrt(256 - 2.67 x 64) = 9.2 of 16 | +0.58 | about +0.2 to +0.3; **measured +0.29** flat (+0.24 to +0.35), choke +0.02, high ground -0.03, flooded +0.61 |
| Two against one (200 against 200) | Roc hits at 0, 1.2, ... kill the first Archerfish at 4.8 s; the Archerfish deal 2 x 5 hits by then, 100; the survivor needs 6 more hits, Roc dies at 10.0 s; the second Archerfish has taken 4 hits (14 left) | Water wins, one of two alive | |
| Thunderhead (190 health, armoured; blast 44 per 2.5 s, light 0.75, splash 1.25 tiles) | 6 Thunderheads (1590) against 16 Archerfish: kill rates 10 / 190 = 0.053 and 13.2 / 70 = 0.189; Water 0.053 x 256 = 13.5 against 0.189 x 36 = 6.8; splash makes it worse for Water | +0.70 before splash | **measured -0.57** (splash decides it; the square law is wrong here). Every Water unit but the Leviathan loses to it today (Spoutling -0.72) |
| Windrunner, Talon Marksman, Stormcaller, Fledgling | cannot shoot them | -1.00 | -1.00 (by design; this is what counters it) |
| Great Aerie | ground layer in the data | cannot shoot it | |

**Counters to it:** every Skai ground unit (it cannot shoot back), the Stormcaller's splash, and Talons out-ranging it (6 against 5). **It counters:** the Roc Rider only (it loses to the Thunderhead, measured -0.57). Design rule 4 ("wins at least one fight") holds through the Roc.

**Wet game:** no ground fight changes, because it hits no ground or water target. On the flood it adds to anti-air Water already has there (Reef Runner, Shellback). The one wet change is more anti-air per Tidepool, which only matters against flyers.

### 3.3 Scratch measurement of the Archerfish

Scratch data: the stats of 3.1, added to the Tidepool's `trains`, weight 10 in all five Water compositions of `aggressive_s5`. **Sweep:** 910 records, and all 780 old records are identical (seed and hash ignored), so it changes no existing fight. Against `skai_ai_mix` it scores -0.76 (it shoots only the one Roc). **Games:** see the table in 2.1. The AI trained a median of 12.5 Archerfish, and Water's army at 300 s was worse in 33 of 36 pairs.

## 4. Knock-on work (if either is built)

| Area | V1 (cheaper option) | Archerfish (fallback) |
| --- | --- | --- |
| Data | `units/water/reef_runner.json`: `can_target` gains `air`, `can_target_air_on_water` and its comment removed (the loader rejects both together), comments and `$doc`. `scripts/fights/matchups.json` Reef Runner `loses` from `skai.roc_rider` to `skai.thunderhead` (measured -0.71). | New `units/water/archerfish.json`; `water/tidepool.json` `trains` and `$doc`; `matchups.json` entry (wins Roc Rider, loses Windrunner). The default sweep plays every unit, so it grows (about 780 to about 900 records). |
| AI | None needed: `FightSim` and the army mix read `can_target` from the data, and the Reef Runner is already weight 30 in every Water composition of `aggressive_s5`. The predictor's Reef Runner advantages move (re-pins). | Add `water.archerfish` to the Water compositions of the profile under test (`aggressive_s5`), placeholder weight 10 (Q-4); other profiles unchanged. |
| Tests that change | `Combat/WaterAntiAirTests.cs` line 231 asserts exactly Reef Runner and Shellback carry the flag: becomes Shellback only. New: Reef Runner on a dry tile targets an adjacent flyer (one test). | Unit-count and roster tests that list Tidepool products (builder greps `water.tidecaller` in `rts/tests`); a new test that the Archerfish never targets a ground or water unit. |
| Pins that move (both) | Every pinned hash, through the content digest: the `roster_numbers_2` re-pin table lists them (smoke and economy_smoke hashes, `FightSimTests` traces and predict accuracy, `SkirmishAiTests` V2 reference and blind trace, `AiVarietyTests`, the ablation hashes in `AiHarassTests` and `AiPlacementTests`, `AiArmyMixTests` advantages, `HANDOFF_UNITY.md`, `tutorial_01`). `reports/predict_fights/` regenerated. | Same list, plus `ProductionDefenceMetricsTests` if it counts Tidepool products. |
| Harness | `anti_air_on_water` mechanic counts drop (the Reef Runner no longer uses that rule). Information only. | None. |
| Sim review | Data only, but the `sim-reviewer` pass is required because every hash moves. | Same. |
| Unity and art | None: the Reef Runner model exists (`rts/src/Game/Assets/Resources/Models/water.reef_runner.fbx`); shots at flyers already render for the Windrunner. | A model (Tripo3D pipeline, P-001), an icon, a Tidepool button, and a concept sheet first. **Art generation is billed and needs the developer's say** (Q-6). The PC session owns `rts/src/Game`. Until then the unit can run headless with no model. |

## 5. Measurement plan for V1 (fixed before any run; for the Archerfish, the same plan with its data commit and its 3.3 sweep in place of G-1's record list)

### 5.1 Design and runs

The design of `roster_numbers_2` 4.1: data-paired, before against after on the same seeds. **Base seed 20261213.** It appears in no file on any `origin` branch at `9322961`, and the designer never ran it. Spent: 5551, 8086, 31337, 20261117, 20261127, 20261201, 20261203, 20261209; 20261128 is reserved for RG. Release build. `<dir>` is `rts/docs/reports/water_reef_air`.

```
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_<run> dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5 --maps prototype_1v1 --matchups skai_vs_water --games 72 --seed 20261213 --fights-from <dir>/sweep_<side>/fights.jsonl --jobs 4 --out <dir>/<run>
```

| Run | Sides | Differences |
| --- | --- | --- |
| M | `main_before`, `main_after` | none |
| X | `mirrors_before`, `mirrors_after` | `--matchups skai_vs_skai,water_vs_water --games 24` |
| F | `sweep_before`, `sweep_after` | `--fights --jobs 4` |
| H | `bars_before`, `bars_after` | `--fights --fight-settings rts/data/scripts/fights/roster_numbers_1.json --jobs 4` |
| RG | `respseeds_before.jsonl`, `respseeds_after.jsonl`, then `--response-guard --from-a ... --from-b ...` | `--response --profile aggressive_s5 --seeds 8 --seed 20261128 --jobs 4` |

Join `main_before` and `main_after` on (`matchup`, `game`) with a throwaway script; commit `<dir>/pairs.md`. Commit and push caches every 24 games. One run per side, no second round on 20261213.

### 5.2 Bars (exact two-sided sign test, 0.05, decisive pairs, Water's view)

| ID | Measure | Pass when |
| --- | --- | --- |
| B-1 | Water won (higher) | BETTER |
| B-2 | Water army supply over Skai's at 300 s, `army_supply[1]` (higher) | not WORSE |
| B-3 (mechanism) | Water's own army supply at 450 s, `army_supply[2]`, null counted as 0 (higher) | BETTER |

### 5.3 Guards

| ID | Measure | Holds when |
| --- | --- | --- |
| G-1 | F: every record without a Reef Runner side facing a side that contains a flyer is identical (ignoring `seed`, `state_hash`) | 0 other records differ (designer's V1: 24 changed, all of this kind) |
| G-2 | F: Reef Runner against Roc Rider, flat mean margin | at most +0.35 (designer's V1: +0.16); mean of the 42 flat cross-faction pairs at most +0.15 (V1: +0.129) |
| G-3 | H: no flat cross-faction pair rises from +0.35 or below to above +0.35; Reef Runner against Roc at most +0.35 | holds |
| G-4 | X: `skai_vs_skai` 24 of 24 games same `result`, `end_tick`, `play_signature`; `water_vs_water` decided games after not fewer than before minus 2 (identity expected, since no flyer is in it; reported) | holds |
| RG | seeded response guard (`response_suite_guard.md` 3) | `RESPONSE_GUARD HOLDS` |
| G-5 | full suite green after re-pins; every re-pin explained by the content digest or the Reef Runner change; no assertion loosened; spot check 0 mismatches | holds |

### 5.4 Verdict

- **Ship:** B-1 BETTER, B-2 not WORSE, B-3 BETTER, every guard holds.
- **Keep as a parity fix:** B-1 not significant and not WORSE, B-3 BETTER, every guard holds. Merge on the developer's word (Q-2). Record that the roster is not the lever, and do not build the Archerfish.
- **Adjust numbers:** B-3 BETTER and B-1 not WORSE, but only G-2 or G-3 fails (too strong against air). The Reef Runner's damage against flyers cannot be lowered without changing its ground fights (no per-layer damage field), so the adjustment is to revert V1 and measure the Archerfish instead, whose damage is a free number, in a new spec with a new unspent seed.
- **Drop:** B-1 or B-2 WORSE, B-3 not BETTER, or G-1, G-4, RG or G-5 fails. Revert the data on the branch and report.

Power (as `roster_numbers_2` 4.3): baseline Water wins 0 to 1 of 72, so the minimum detectable effect at power 0.8 is about 8 of 72 pairs in which only the changed data's Water wins.

### 5.5 Expected result (stated before the run)

- **B-1: no measurable effect**, 0 to 1 Water wins of 72 (trial: 0 of 36).
- **B-2: WORSE** (trial: 7 better against 29 worse, p 0.0003). The plan's verdict is then **Drop**.
- **B-3: not BETTER** (trial: 13 / 22).
- G-1 and G-2 are expected to hold (designer's sweep: 24 records changed, all of the guarded kind, Reef Runner against Roc +0.16). G-4 is expected to show the two mirrors identical. RG is not predicted.
- Skai still wins at least 70 of 72.

Running it still settles the question on an unspent seed, and costs about 3 hours of machine time.

## 6. Questions for the developer (defaults the builder uses until you reply)

| # | Question | Default |
| --- | --- | --- |
| Q-1 | Reverse decision 3 of 18 Nov 2026 ("some water units can hit flyers, but only while they stand on water") for the Reef Runner only, so it hits air on any ground (V1)? The Shellback keeps the on-water rule. | Not now (Q-3). If a roster measurement is run, yes, for that measurement only. |
| Q-2 | If V1 gives "keep as a parity fix" (no win effect, guards hold), merge it? | Yes: its mirror, the Windrunner, already hits air everywhere, and V1 changes no fight Water already wins. |
| Q-3 | Given the trials, take the next step in AI work instead of the roster: why the AI's 300 s army collapses when it has more mobile anti-air (it cuts turrets and loses the 180 to 300 s trade), and why it trains 0 Leviathans per game? | Yes. Run neither roster measurement until you decide. If you want one anyway, run V1 (section 5); build the Archerfish only if you answer Q-1 "no" and still want a unit. |
| Q-4 | If built: Archerfish air-only targeting, no rare cost, Tidepool, weight 10 in the `aggressive_s5` Water compositions? | Yes; all placeholders. |
| Q-5 | Name and creature: Archerfish? The design doc names no such unit. | Placeholder "Archerfish" until you name it. |
| Q-6 | Art for the fallback (concept sheet, Tripo3D model, icon) is billed. Generate it before the unit passes its measurement? | No: measure headless first; art only after "ship". |
| Q-7 | Accept that the win bar can detect only about 8 extra wins in 72? | Yes, as for the last three measurements. |

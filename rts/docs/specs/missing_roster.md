<!-- rts/docs/specs/missing_roster.md: build spec for the three units the Game Design Proposal names that have no data file yet (Skai Thunderhead, water Tidecaller, water Leviathan): unit files with placeholder stats derived by stated rules, what the simulation and the skirmish AI already support, a one-line AI pool guard, acceptance tests, the pinned hashes that move, and what the units can and cannot change in the WC-3 measurements. Part of rts/docs/specs (index: README.md). -->
# Spec: the missing roster (Thunderhead, Tidecaller, Leviathan)

Written 18 Nov 2026 by the designer. Data and AI only: **no change in `rts/src/Sim`**, no new tuning key, no new command, event or hash field.

**Developer decisions of 18 Nov 2026** (they override the defaults below; section 10 records each): (1) the derived placeholder stats are accepted; (2) the Thunderhead is trained at the Windspire and cannot target air ("fine for now"); (3) the Tidecaller is unarmed, on the water layer, and "should generate water around it", built as a flood source on the unit at half the Tidepool's output and a quarter of its capacity (placeholder, developer to confirm); (4) the Leviathan can target air.

## 1. Goal and sources

Give the three named units a data file so players, scripts and the skirmish AI can train them, and re-measure the WC-3 numbers (`rts/docs/reports/worker_construction_wc3/README.md`) with them in. The developer's hypothesis (18 Nov) is that the missing roster causes the failing response scenarios and the lopsided Skai-versus-water matchup; section 6 says plainly where it can and cannot.

Decided (Game Design Proposal as quoted in project records; nothing else is decided):
- Water roster: "Tidecaller (creates waves and extends the flood), Leviathan (capstone siege unit)".
- Thunderhead: Skai flying-wing bomber, an air unit; concept art shows underwing missile pods and two guns.
- Art: Tidecaller is a hovering jellyfish support caster; Leviathan is a colossal king crab with two long-barrelled hydro-cannons, about three times the Tidecaller's size.
- `flooding.md` section 2: `can_cast_wave` is "Tidecaller only".

**Every number in section 3 is a placeholder.** The developer accepted them as derived on 18 Nov 2026 (Q1); the two the developer set (the Tidecaller's flood values, the Leviathan's air targeting) say so. None comes from design material. Each one is worked out from the shipped units by a rule in section 2.

## 2. Derivation rules (placeholders)

- **D1, tier factor `t`.** Each new combat unit is a step above one existing unit of its faction, its *sibling*: the faction's highest-value unit, where value `V = cost_common + cost_rare` (`counter_rare_cost_weight` is 1 in `aggressive_s3`). Skai: Roc Rider, V 200. Water: Brinecaster (`ink_lobber`), V 190 (Shellback is 175). Thunderhead `t = 4/3`: one tier up, because the design does not call it a capstone. Leviathan `t = 2`, because the design calls it the capstone and it is "colossal". `cost_common`, `cost_rare`, `supply` and `build_seconds` are the sibling's values times `t`. Costs round down to a multiple of 5; supply and seconds round to the nearest whole number.
- **D2, faction efficiency.** Health and damage are set so the new unit gives as much health and raw damage per second (`damage / cooldown_seconds`, before the damage table) per unit of value as the faction's AI pool does today (`skirmish_ai_v3.md` C.3):
  - Skai pool (Windrunner, Talon Marksman, Stormcaller, Roc Rider): dps 7.5 + 8 + 10 + 11.6667 = 37.1667; health 60 + 90 + 90 + 160 = 400; V 75 + 100 + 190 + 200 = 565. That is **0.065782 dps per V** and **0.707965 health per V**.
  - Water pool (Reef Runner, Pummeler, Brinecaster, Shellback, Spoutling): dps 7.5 + 12 + 12 + 10 + 5 = 46.5; health 60 + 110 + 100 + 220 + 70 = 560; V 75 + 100 + 190 + 175 + 125 = 665. That is **0.069925 dps per V** and **0.842105 health per V**.
  - `max_health = V x health rate`, rounded to the nearest 10. `damage = V x dps rate x cooldown`, rounded to the nearest whole number.
- **D3, copied fields.** Every other number is copied from a named unit, or follows a stated art rule.

## 3. Unit files

House format, the same as `rts/data/units/skai/roc_rider.json`:
- line 1 is the `//` path comment with a one-line description; then `$schema_version` 1 and a `$doc`;
- every key has its comment line above it. Copy the comment of the same key from the **comment source** file named for each unit. Then do two things to it: replace its `Source:` with the Source given here, and delete any "(placeholders sit between ...)" note the new value falls outside;
- Source for every derived number: `Source: placeholder, developer to confirm (rts/docs/specs/missing_roster.md section 3).`

Key order: the comment source's order.

### 3.1 `rts/data/units/skai/thunderhead.json` (comment source `skai/roc_rider.json`; weapon comments from `skai/stormcaller.json`)

| Key | Value | Rule |
| --- | --- | --- |
| id / display_name / faction_id | `skai.thunderhead` / `Thunderhead` / `skai` | Source of display_name and layer: `Factions / Skai Riders / Unit concepts (Thunderhead, flying-wing bomber)`. |
| kind / layer | `unit` / `air` | Design: an air unit. |
| max_health | 190 | D2: 265 x 0.707965 = 187.6 |
| speed_tiles_per_second | 4 | D3: Roc Rider |
| supply | 4 | D1: 3 x 4/3 |
| radius_tiles | 0.8 | D1 applied to size: 0.6 x 4/3 |
| cost_common / cost_rare | 200 / 65 | D1: 150 x 4/3; 50 x 4/3 = 66.7, rounded down to 65 |
| build_seconds | 40 | D1: 30 x 4/3 |
| armour_type | `armoured` | D3: Roc Rider |
| sight_range_tiles / airborne_sight_range_tiles | 10 / 6 | D3: Roc Rider (airborne sight is required for air units) |
| weapon.damage | 44 | D2: 265 x 0.065782 x 2.5 = 43.6 |
| weapon.damage_type | `blast` | D3: Stormcaller (bombardment; missile pods) |
| weapon.range_tiles | 5 | D3: Roc Rider |
| weapon.cooldown_seconds | 2.5 | D3: Stormcaller |
| weapon.hit_delay_ticks | 2 | D3: every ranged unit |
| weapon.can_target | `["ground", "water"]` | A bomber hits only surface targets; the Roc Rider covers air. **Decided 18 Nov 2026 (Q3): cannot target air, "fine for now".** |

### 3.2 `rts/data/units/water/tidecaller.json` (comment source `water/spoutling.json`; `can_cast_wave` comment written new, below)

| Key | Value | Rule |
| --- | --- | --- |
| id / display_name / faction_id | `water.tidecaller` / `Tidecaller` / `water` | Source of display_name: `Factions / Water faction / Unit concepts (Tidecaller: "creates waves and extends the flood")`. |
| kind / layer | `unit` / `water` | Same layer as every water unit. "Hovering" in the art is not an air layer. **Decided 18 Nov 2026 (Q4): water layer.** |
| max_health, speed_tiles_per_second, supply, radius_tiles | 70, 3, 2, 0.4 | D3: Spoutling chassis. The Spoutling is the Tidepool's other rare-cost support unit. |
| cost_common / cost_rare / build_seconds | 100 / 25 / 20 | D3: Spoutling |
| armour_type / sight_range_tiles | `light` / 9 | D3: Spoutling |
| weapon | **absent** | The design gives it no attack. **Decided 18 Nov 2026 (Q5): unarmed.** |
| can_cast_wave | `true` | `flooding.md` section 2 |
| flood_source_output_per_second | 0.5 | **Decided 18 Nov 2026 (Q9):** "it should generate water around it". Half the Tidepool's output of 1. Placeholder, developer to confirm. |
| flood_source_capacity | 10 | **Decided 18 Nov 2026 (Q9):** a quarter of the Tidepool's 40. Placeholder, developer to confirm. |

New comment for `can_cast_wave`: `// Lets this unit take the cast_wave command (push enemies back and extend its team's flood; cooldown and size in tuning/flood.json). Unit: boolean. Range: true or false (false when absent). Source: Factions / Water faction / Unit concepts (Tidecaller: "creates waves and extends the flood"); rts/docs/specs/flooding.md section 2.`

### 3.3 `rts/data/units/water/leviathan.json` (comment source `water/ink_lobber.json`)

| Key | Value | Rule |
| --- | --- | --- |
| id / display_name / faction_id | `water.leviathan` / `Leviathan` / `water` | Source of display_name: `Factions / Water faction / Unit concepts (Leviathan, capstone siege unit)`. |
| kind / layer | `unit` / `water` | Same layer as every water unit |
| max_health | 320 | D2: 380 x 0.842105 = 320.0 |
| speed_tiles_per_second | 1.5 | Brinecaster 2.0 x 0.75 because it is colossal. It sits between the Grand Shell (1.0) and the Shellback (1.8). |
| supply | 4 | D1: 2 x 2 |
| radius_tiles | 1.2 | Art: three times the Tidecaller (0.4 x 3). The mobile Grand Shell (1.4) already shows that a radius this large moves and paths. |
| cost_common / cost_rare | 300 / 80 | D1: 150 x 2; 40 x 2 |
| build_seconds | 60 | D1: 30 x 2 |
| armour_type | `armoured` | Art: armour-plated carapace |
| sight_range_tiles | 12 | Range + 3, the same margin as the Brinecaster (10 - 7) |
| weapon.damage | 80 | D2: 380 x 0.069925 x 3.0 = 79.7 |
| weapon.damage_type | `blast` | D3: Brinecaster. Siege comes from the table: blast does 1.25 against `structure`. |
| weapon.range_tiles | 9 | The longest ground range in the data (7) + 2: siege means the longest reach. |
| weapon.cooldown_seconds | 3.0 | D3: Brinecaster |
| weapon.hit_delay_ticks | 2 | D3 |
| weapon.can_target | `["ground", "water", "air"]` | **Decided 18 Nov 2026 (Q8): includes air** (the spec default was ground and water, as the Brinecaster). |

### 3.4 Producers and faction lists

There is no tech-prerequisite system. The schema has no such key, and the only gate on a unit is owning a structure whose `trains` lists it. So the "tech step" is the order in which producers are built. In every shipped AI build order the first producer is the Roost or Tidepool, and the Windspire or Shell Forge comes second, at 10 supply.

| File | `trains` becomes | Why |
| --- | --- | --- |
| `skai/windspire.json` | `["skai.talon_archer", "skai.stormcaller", "skai.thunderhead"]` | The second producer, home of the bombardment unit. The Roost (with the Roc Rider) is the alternative (Q2). |
| `water/tidepool.json` | `["water.spoutling", "water.grasper", "water.reef_runner", "water.tidecaller"]` | The flood-source building. The wave extends its flood. |
| `water/shell_forge.json` | `["water.shellback", "water.ink_lobber", "water.leviathan"]` | The second producer, home of the artillery sibling |

Update each file's `trains` comment and `$doc` to name the new unit. Faction `units` lists, with new units placed after the last combat unit and before the HQ:
- `factions/skai.json`: `"skai.thunderhead"` after `"skai.roc_rider"`.
- `factions/water.json`: `"water.tidecaller", "water.leviathan"` after `"water.ink_lobber"`.

## 4. What the simulation already has (no new rule needed)

| Need | Exists? | Evidence | Lost in the no-new-rule version |
| --- | --- | --- | --- |
| Bomber: an air unit that hits only the ground | Yes | `layer: air` plus `can_target` without `air`; air units need `airborne_sight_range_tiles` (`UnitSchema.ReadAirborneSightRange`). The Spoutling's Drench grounds it (`statuses.md`), and then ground weapons can hit it. | **No area damage.** No splash key exists (the Stormcaller's `$doc` already notes this). Each bomb hits one target. Pods and guns are one weapon (the schema allows one per unit). It shoots from range 5 rather than flying over its target. |
| Mobile flood source | **Yes, from data alone** (checked 18 Nov 2026) | `FloodingMechanic.SourcesOf` rebuilds the source list at every flood update from each unit's current `Position` (`TerrainGrid.TileOf`), and `IsActiveSource` tests only the definition's `Flood.IsSource`, completeness, an owner in a flooding faction and (for an HQ) the Rooted state. Nothing assumes a source stands still. Test `MissingRosterTests.Tidecaller_FloodsAroundItself_AndTheWaterFollowsIt`. | None. Water already placed stays and drains at the tide's retreat rate; only new water follows the unit. |
| Wave casting | Yes, built and tested | `CastWaveCommand` with the script name `cast_wave`, codecs in Net and Replay, `FloodingMechanic.CanCast` (definition `CanCastWave`, and the owner's faction uses `flooding`), `FloodingTests` 12a to 12e (flood strip, push, cooldown, Rooted HQ, rejections, direction, walls). The tests use a hand-built caster, but nothing in the code is specific to test units: any unit whose file has `can_cast_wave: true` can cast. `UnitFloodDefinition` already writes `CanCastWave` into the content digest. | Nothing from the design text (flooding.md's "first-pass" wave: push and extend; currents deferred). **Not in the repository: a Unity input for `cast_wave`** (no reference in `rts/src/Game`). This is PC-session work. |
| Siege: a structure bonus and long range | Partly, as data | Blast against `structure` is 1.25 in `tuning/combat.json`; `range_tiles` goes up to 64. | No per-unit structure bonus beyond the shared table. No minimum range, deploy mode or two-cannon volley. |
| Unarmed unit | Yes | `weapon` is optional (`UnitSchema.ReadWeapon` returns null) | None |

Optional Sim packages that are **not in this build order**. Each would need its own spec and a sim-reviewer pass, and the developer would have to ask for it:
- **S-1 splash**: `weapon.splash_radius_tiles` (0 to 4, default 0), damage to every enemy within the radius of the target when the hit lands, in step 6b, ascending victim ID. It is written in `WeaponDefinition.WriteHash` after `hit_delay_ticks`. It would serve the Stormcaller too.
- **S-2 structure bonus**: `weapon.structure_damage_multiplier` (default 1), applied after the table multiplier.

**Step order, state hash, commands, events, tuning:** unchanged. The new files only reach the state hash through the content digest (`Simulation.cs` line 228).

## 5. Skirmish AI (`rts/src/Ai`), checked in code

| Question | Answer |
| --- | --- |
| Will the counter-mix see the new units? | Yes, for profiles with `use_counter_mix` (`aggressive_s2`, `_s2b`, `_s3`). `AiCounter.Pool` collects every mobile, non-worker, non-HQ unit of the faction that appears in some structure's `trains`, and names no unit (P-004 line 110 is right). The C2.2 predictor mix scores every pool unit; base compositions stay unchanged (Q7), so a new unit is trained only when the mix keeps it. |
| Tech on demand | Yes. Rule T (C.5) builds the producer of the top mix unit (`ProducerStructureFor` reads `trains`). |
| Will it train the Thunderhead? | When it is kept. Against Pummelers it is (section 7, R2-6): Pummelers cannot hit air, so the predictor gives it a capped advantage, as it does the Roc Rider. Rule P skips any unit whose `cost_rare` the stock cannot pay, so rare income still gates it. |
| Will it train the Leviathan? | Against armoured ground armies when rare is available: with rare 400 it is kept against 6 Pummelers (advantage 1.2208 against the Pummeler's 1.0). With rare 0 it is not (0.100). Against air armies it is kept even at rare 0, because it can now hit air (decision 4; R2-8). See R2-7. |
| Will it train the Tidecaller? | It should not, and package R-1 makes sure. Unarmed, the predictor values it only as extra health, which in some armies could score above an armed unit. That would buy units that never fight and never cast. (It is also a flood source now, but no AI rule counts that.) |
| What will it not do? | **Cast waves** (no AI rule issues `CastWaveCommand`; a later package, Q6). It will not use the Thunderhead's "bombing" differently from any ranged unit, and it will not keep the Leviathan back as artillery (rule D unit control treats it like any ranged unit). Profiles without the counter-mix (`aggressive`, `defensive`, `aggressive_s1`, `abl_s2_blind`, `rt_*`) never train the new units, and their games are unchanged except for the hash (R2-9). |
| Predictor gaps that matter here | The predictor does not model splash, drench grounding or flooding (C.1). It models the Leviathan's range head start, capped at `predictor_range_headstart_max_seconds` 5. |

The harness `armoured_answer` check has its own pool (`ResponseChecks.Pool`). It is left alone: an unarmed unit has multiplier 0 and is never an answer against Pummelers.

## 6. Can this change the failing measurements?

| Measurement | Can the new roster change it? | Why |
| --- | --- | --- |
| `armoured_answer`, Skai (share gain -0.073; share 0.389 against base 0.462) | **Possibly, not by design** | The Skai pool already holds two answer units, the Roc Rider (Pummelers cannot target air) and the Stormcaller (blast 1.0 against armoured). The Thunderhead would be a third. Rule P drops units whose rare cost the bank cannot pay, and the Thunderhead costs rare (65) like the other two. The likely cause, rare starvation, is untouched; this is a hypothesis, not yet checked against the trace's rare bank. |
| `armoured_answer`, water (share gain -0.075; not counted, Q-C2-1) | **Possibly** | The Leviathan is blast, so it is an answer unit, and its supply of 4 lifts the answer share fast. Its derived stats beat Pummelers at equal value, so the mix keeps it **when rare is 220 or more** (R2-7). With little rare it changes nothing. |
| `raid` (both factions, 7 workers lost in a wave; Skai builds no static defence) | **No** | This depends on rule SD's static defence placement and on worker defence against four Windrunners. No new unit is a static defence, and none changes those rules. |
| Skai beat water in every cross-faction game (`aggressive_s3` 36 of 36 as Skai, 0 of 36 as water) | **Unknown; likely worse for water** | No diagnosis of this split exists in the repository. The Thunderhead gives Skai a second air unit that only the Spoutling, the Coral Turret and the Grand Shell can hit. The Leviathan can hit air (decision 4). The AI never casts waves. Expect the split to stay or widen; R-3 measures it. |
| 15 Pummelers beat 8 Brinecasters at equal cost | **No** | That is a property of two existing units, and new units do not change it. The Leviathan offers water a different answer to mass Pummelers, rare permitting. |

## 7. Packages and acceptance tests

Build order: R-1, R-2, R-3. Builder: Sonnet. Review: an Opus `sim-reviewer` pass on R-1 (AI determinism, as for the P-004 packages). R-2 changes no code in `rts/src/Sim`.

### R-1. AI pool guard (Sonnet)

`AiCounter.Pool` also requires `unit.Weapon != null`. Update its summary and the C.3 wording in the `AiCounter.cs` header ("armed"). No other code change. Every shipped pool unit is armed, so behaviour and every hash stay the same.

- **R1-1** A hand-built catalogue with one armed and one unarmed trainable unit of a faction: `Pool` returns only the armed one.
- **R1-2** On shipped data, `Pool("skai")` is {roc_rider, stormcaller, talon_archer, thunderhead, windrunner} and `Pool("water")` is {grasper, ink_lobber, leviathan, reef_runner, shellback, spoutling}, ordinal. (Written as four and five units before R-2; R-2 adds the Thunderhead and Leviathan, never the Tidecaller.)
- **R1-3** The whole suite is green with no re-pin (`AiVsAi_FinalHash_MatchesTheV2Reference`, `AblationBlind_TracedGame_IsUnchangedByPackageC2`, the harness smoke hashes).

### R-2. Unit data (Sonnet)

The files in section 3, plus test updates.

- **R2-1 Load.** Shipped data loads. `Content.Units.Count` goes from 21 to 24. Faction `UnitIds` match section 3.4 exactly. Every field of the three definitions equals sections 3.1 to 3.3, including layer, `AirborneSightRangeTiles` (Thunderhead 6; the property equals the sight range for non-air units, it is not null), the Tidecaller's flood source (output 0.5, capacity 10), `Weapon` (null for the Tidecaller), `Flood.CanCastWave` (Tidecaller true, others false) and `Radius`.
- **R2-2 Trains.** Windspire, Tidepool and Shell Forge `Economy.Trains` equal section 3.4 (update `EconomyDataTests` lines 45 to 49 and add the Windspire and Shell Forge rows).
- **R2-3 Trainable.** Shipped data, map `greybox_1v1`, one complete producer per unit spawned with `SpawnUnitCommand` (issuer 255), and the stock set with the `SetStock` helper pattern of `AiConstructionTests` (cost plus margin). `TrainUnitCommand` is accepted. The stock drops by the unit's cost. A `UnitTrainedEvent` for the definition arrives at build seconds x 20 ticks after acceptance (Thunderhead 800, Tidecaller 400, Leviathan 1200; allow the spawn-retry rule of `EconomySystem`), and supply used rises by 4, 2 and 4.
- **R2-4 Real Tidecaller casts.** In the same setup, a trained or spawned `water.tidecaller` owned by a water player, with `CastWaveCommand` toward a tile 6 east (built on the 64 by 64 flat map, not `greybox_1v1`, so terrain cannot interfere). One `WaveCastEvent` with `tilesFlooded` above 0, and an enemy ground unit inside the strip gets one `UnitPushedEvent`. A `skai.thunderhead` given the same command is rejected with `not_wave_caster`.
- **R2-5 Thunderhead cannot hit air; Leviathan sieges.** Level ground, no cycles. A Thunderhead within 5 tiles of an enemy Roc Rider fires no shot at it. A Leviathan attacking an enemy Roost lands hits of 80 x 1.25 = 100. A Leviathan also hits an enemy Roc Rider (decision 4).
- **R2-5b Mobile flood source.** A shipped Tidecaller raises water around its own tile; ordered 30 tiles east, water appears around its new tile (no change in `rts/src/Sim`).
- **R2-6 AI mix, Skai** (`AiArmyMixTests` style, `aggressive_s3` settings: `mix_eval_budget_common` 1200, `mix_rare_allowance` 100, keep 0.9, `counter_weight` 0.7; nothing owned; model: 6 Pummelers seen; rare 400). n: thunderhead 6, roc_rider 8. Advantages: thunderhead 600 / (1320 / 105.6 - 4/3) = 53.731; roc_rider 600 / (1320 / 70 - 4/3) = 34.239. Kept: thunderhead only. Mix: roc_rider 42, stormcaller 42, talon_archer 85, thunderhead 700, windrunner 128. Then with a free complete Windspire, stock 1000 / 65 and supply room, rule P orders `TrainUnit(windspire, skai.thunderhead)` (built: the spec's 200 common is not enough, because the HQ buys a Fledgling first under rule 4b). Confirmed by the build: advantages 53.731 and 34.239, the mix above.
- **R2-7 AI mix, water** (same settings and model). Rare 400: n(leviathan) 4; advantage (1280 / 108) / (1320 / 106.667 - 8/3) = 1.2208; grasper 1.0000. Kept: leviathan only. Mix: grasper 150, ink_lobber 50, leviathan 700, reef_runner 0, shellback 50, spoutling 50; `water.tidecaller` is absent. Rare 0: n(leviathan) 1.25, advantage 0.100. Mix: grasper 850, ink_lobber 50, leviathan 0, reef_runner 0, shellback 50, spoutling 50. Tolerance 0.001 on advantages; mix integers exact. Confirmed by the build (advantages 1.2208, 1.0000, 0.1003 and every mix integer). If the code gives other numbers, the builder reports both computations and stops. The arithmetic here may be wrong; the AI code is not to be changed to match it.
- **R2-8 Existing AI tests on shipped data.** Tests that assert pools, mixes, advantages or implied enemy-model entries over shipped data change in only two ways: a new pool entry appears, or a newly kept unit changes the mix. Known case: C2-1 "with rare 400: same mix" becomes grasper 150, shellback 50, ink_lobber 50, spoutling 50, leviathan 700, reef_runner 0 (the same arithmetic as R2-7 at budget 600: n 2, advantage 1.2208). Implied units (B.3) now include thunderhead (Windspire), tidecaller (Tidepool) and leviathan (Shell Forge). The builder recomputes each changed assertion by hand and lists it in the report. Any other change means stop and report.

  **Built, 18 Nov 2026.** Changed tests: C2-1 (rare 400, as above); C2-2 (with the Leviathan able to hit air it is kept against 4 Roc Riders even at rare 0, advantage 1.1372, and 0.7826 with 4 Spoutlings owned: the Spoutling mix 750 and the Shellback mix 750 become 50 and the Leviathan takes 700); C2-3/4 (the Thunderhead alone is kept against 6 Pummelers at rare 0, advantage 26.0398 against the Roc Rider's 16.4921, so the Roc Rider mix 742 becomes 42 and the Thunderhead takes 700; against 8 Windrunners the Thunderhead is not kept, 0.2782); C-8 and C-9 (package C scores: the Leviathan 0.0046089 beats the Spoutling's 0.0013333, so the Spoutling mix 750 becomes 50 and the Leviathan takes 700; the Thunderhead 0.0307071 is the top Skai score against Pummelers, so the Roc Rider mix 742 becomes 338 and the Thunderhead gets 404). Each is recomputed in its test comment; the C2-2 advantages are checked by a Lanchester estimate only, not by the predictor's own arithmetic.
- **R2-9 Re-pins: hash only, outcomes unchanged.** Each test below gets a new hash value. Its other assertions must hold unchanged:

| Pin | File | Old | Must stay |
| --- | --- | --- | --- |
| smoke `STATE_HASH` | `Harness/HarnessUnitTests.cs:166` | `03674dfb1efc68b7` | nothing else in that test changes |
| economy_smoke `STATE_HASH` | `Harness/HarnessUnitTests.cs:168`, `Economy/WorkerConstructionTests.cs:730` | `93762ddc16910b23` | same |
| `V2ReferenceHash` | `Ai/SkirmishAiTests.cs:839` | `0x368285be720371c6` | `OUTCOME winner=1 tick=7613` (the `aggressive` profile has no counter-mix) |
| `BlindTraceHash` | `Ai/SkirmishAiTests.cs:861` | `963068ebe8578f25` | `BlindTraceEndTick` 11438 |
| `DeterminismTests.GoldenFinalHash` | `DeterminismTests.cs:93` | `0xc152b29fd0252a8d` | **must not change** (TestWorld, not shipped data) |

  **New values (built 18 Nov 2026):** smoke `a65a7eea003009f4`; economy_smoke `aff851b30f63e4f8`; `V2ReferenceHash` `0x8b437ebc2b943b25` (outcome `winner=1 tick=7613` unchanged); `BlindTraceHash` `57b6a930c9cec707` (end tick 11438 unchanged); `GoldenFinalHash` unchanged. Two test fixtures also gained a Thunderhead damage edit (`HarnessBatchTests.FloorEdits`, `HarnessUnitTests.FloorData`) so the Windrunner stays the fastest HQ killer of the floor arithmetic scenario, and `DataLoaderTests.Units_Load_WithLayersAndValues` exempts the Leviathan (health 320, speed 1.5) from the step 9 roster bounds.

  `DataLoaderTests` lines 125, 126 and 133 change as R2-1 says. Older replays are refused (content digest). Replay end-to-end tests compute their hash at run time and need no pin. The re-pin comments name this spec.
- **R2-10 AI trains them in a traced game.** `--trace --map prototype_1v1 --matchup skai_vs_water --seed 20261117 --profile-a aggressive_s3 --profile-b aggressive_s3`, games 0 to 3. Passes when, in at least one game, the Skai player's trained summary lists `skai.thunderhead` and, in at least one game, the water player's lists `water.leviathan`. No game may list `water.tidecaller` for an AI. If either unit never appears, that is a finding, not something to tune: the builder reports the rare bank lines and the mix at the first sighting. The gate is R2-6 and R2-7.

### R-3. Re-measure (Sonnet; report only, no tuning)

Rerun WC-3 unchanged on the new data. That is the ladder `aggressive_s3` against `aggressive_s2b` (216 games, `prototype_1v1`, seed 20261117) and `--response --profile aggressive_s3`. Then split the Skai-versus-water cell by which profile played Skai. Write `rts/docs/reports/missing_roster_r3/README.md` with the WC-3 tables side by side and section 6's rows marked confirmed or refuted. Both ladder profiles gain the units, so the 0.750 is not a like-for-like baseline: say so in the report.

## 8. Out of scope

Splash and structure-bonus rules (S-1, S-2). AI wave casting. Unity input, models and rigging for the three units (PC session; the models exist in `art/models/batch1/`). Retuning of any existing unit or AI profile. Base composition changes. Balance batches.

## 9. Other Skai and water things named in design records without a data file (not specified here)

- Walls, both factions: `art/CONCEPT_BRIEF.md` line 134 ("The design doc names what a base needs (... static defences capped at six per base, walls ...)") and line 144 (placeholder names Wind Baffle and Coral Wall). Also `specs/planetary_cycles.md` line 9 (Quake: "Walls and defences take damage"). No wall structure exists in `rts/data`.
- Building jobs: `CONCEPT_BRIEF.md` lines 136 to 144 draw seven building jobs per faction (drop-off, supply, ground production, air production or flood source, tech, static defence, wall). The data has four structures per faction: Perch Depot, Roost, Windspire, Perch Tower; Pearl Depot, Tidepool, Shell Forge, Coral Turret. Line 132 maps the art to the data names but does not say which of the seven jobs the Roost/Windspire and Tidepool/Shell Forge cover. So three drawn jobs per faction (wall, plus two of supply, air production or flood source, and tech) have no structure of their own.
- No other Skai or water unit is named without a file. P-004 line 110 names exactly these three, and `art/models/batch1/README.md` lists thirteen units, all in `rts/data` once this spec is built.

## 10. Questions for the developer

Each has a default the builder uses until you reply. Answered by the developer on 18 Nov 2026: Q1 to Q5, Q8 and Q9 (marked **Decided**); Q6 and Q7 are still open and keep their defaults.

- **Q1. Stats. Decided 18 Nov 2026: accepted.** Section 3 is entirely placeholders worked out from the rules in section 2; they stand as derived.
- **Q2. Thunderhead producer. Decided 18 Nov 2026: the Windspire.**
- **Q3. Thunderhead against air. Decided 18 Nov 2026: cannot target air** ("fine for now").
- **Q4. Tidecaller layer. Decided 18 Nov 2026: water.**
- **Q5. Tidecaller weapon. Decided 18 Nov 2026: unarmed.**
- **Q6. AI wave casting.** *Default: a later package with its own spec, after R-3 shows whether the roster matters.*
- **Q7. Base compositions.** Add the new units to the `aggressive_s3` plan compositions? *Default: no. The counter-mix reaches them, and leaving the compositions alone keeps R-3 comparable with WC-3.*
- **Q8. Leviathan against air. Decided 18 Nov 2026: `can_target` includes air.** Splash for bombers (S-1) is not asked for: no splash rule now.
- **Q9. Tidecaller flood source. Decided 18 Nov 2026:** it generates water around itself, as a flood source on the unit (`flood_source_output_per_second` 0.5, `flood_source_capacity` 10: half the Tidepool's output and a quarter of its capacity; placeholder, developer to confirm). The simulation already supports a source that moves (section 4), so no Sim change was needed.

<!-- rts/docs/specs/micro_benchmark.md: build spec for the equal-value fight mode of the balance harness (`--fights`, option M of proposals/P-005-roster-holes-and-ai-for-balance.md, built first by developer decision 9 of 18 Nov 2026). Army-against-army fights with no strategic AI, swept over unit pairs, compositions, army values, terrain cases and both seats; a value-exchange matrix; the automated design-rule checks of P-005 section 1.6 (rules 1 to 6, HQ floor 15 s); and the matchups file that replaces design rule 7. Harness and data only, no rts/src/Sim change. Part of rts/docs/specs (index: README.md). -->
# Spec: Equal-value fights (`--fights`, balance harness)

## 1. Purpose and sources

- P-005 part 2 option M: "a harness mode that plays equal-value army fights with no strategic AI ... Winner by remaining value, not HQ kill ... Output: a value-exchange matrix." It is the measuring tool for every mechanic and number change that follows (developer decision 9, 18 Nov 2026).
- P-005 section 1.6 rules 1 to 6 as accepted by the developer (decision 1), with the Rooted HQ floor at 15 s (decision 4). Rule 7 is replaced: "every new or changed unit updates a matchups file that records its job, the fight it wins and the fight it loses"; nothing about it goes in the unit files.
- `reports/ai_v3_2026-11-17/traces/c2_equal_cost_fights.txt`: the scratch fights this mode generalises. Their Skai rows ended undecided because survivors had to kill an HQ; this mode has no HQ in a fight.

The mode is tool code in `rts/tools/BalanceHarness`. It may use processes, files and `double` for statistics and formatting, as `--batch` and `--ladder` do; nothing it computes is fed back into a simulation. It changes no file in `rts/src/Sim` or `rts/src/Ai`.

## 2. What is reused, and what is not

| Existing piece | Use |
| --- | --- |
| `HqFloorTrial` | Pattern for a fight: in-memory `MapDefinition` arena, `SimulationConfig` without cycles, tick-0 `SpawnUnitCommand`s (issuer `PlayerId.None`), player-issued orders from tick 1, event reading after each `Step`. Also run as is for the HQ floor sweep, extended to compositions (section 6, rule 6). |
| `ResponseObserver` | Pattern for the read-only fight observer (records damage and deaths from `sim.Events.Current` after each step, never submits or steps). Its `IsArmy` rule (footprint 0, not a gatherer, no `Hq`) becomes one shared internal helper both use. |
| `WorkerPool`, `BatchRunner.Chunk`, `SeedDerivation.GameSeed` | Parallel worker processes, chunking, per-fight seeds. |
| `LadderRunner` | Output style: `<mode>.jsonl` plus `<mode>.md` with a comment header, settings bullets, a bold summary line and tables; in-process determinism spot check; exit code 3 on a mismatch. |
| `AiEvalSettings` | Pattern for the settings file: lives under `scripts/<subfolder>/` (DataLoader skips `scripts/`, so it is outside the content digest), parsed with `JsonReader` and the `Require*` helpers, unknown keys rejected, every error names the file and field. |
| `HarnessTestSupport.CopyData` | Tests edit a copy of `rts/data` (settings, units, matchups) without touching the real files. |

Not used:
- `CommandScript`. A script names a map file under `rts/data/maps`, and arenas must not become data maps (`--batch` and `--ladder` play every 2-spawn map). Fights are built in memory like the HQ floor arena. Exporting a fight as a script for viewing in Unity is question Q8.
- `rts/src/Ai` unit control (kiting, focus fire, fallback; skirmish_ai_v3.md D.6). It runs inside `AiBrain`'s army step and needs the brain's memory, HQ and stance. Running the whole `SkirmishAiController` in an arena would add strategic behaviour (retreats, production, expansion) to the measurement; using D.6 alone first needs it moved out of the brain into a standalone controller, which is an AI refactor with its own review. So this build has one control column, plain attack-move (section 4.4). A second "AI control" column is question Q1. It is worth adding later, because the gap between the two columns shows how much a unit's result depends on micro (P-005 2.3).

## 3. Data

### 3.1 Settings file `rts/data/scripts/fights/fights.json`

Parsed by a new `FightSettings` class in the harness. The file opens with its path comment, and every value has the comment line `// <what it controls>. Unit: <unit>. Range: <range>. Source: <source>.`. Every value marked placeholder is a placeholder. P-005 calls all its thresholds placeholders, and the developer accepted them as such.

| Key | Unit | Range | Value | Controls; source |
| --- | --- | --- | --- | --- |
| `base_seed` | seed | 0 to 2 147 483 647 | 20261118 | Seed each fight seed is derived from (4.6). Placeholder. |
| `units` | unit IDs | 0 to 64 entries | `[]` | Units in the sweep. Empty means every **combat unit** (4.1). A non-empty list restricts the sweep; each entry must be a combat unit. |
| `compositions` | list of `{ "id", "units": { "<unit id>": count } }` | 0 to 16 entries; IDs `[a-z0-9_]+` (no dot, so never equal to a unit ID), unique; 1 to 8 units of one faction, counts 1 to 50 | `water_ai_mix` {grasper 3, shellback 1, ink_lobber 1, spoutling 1}; `skai_ai_mix` {windrunner 3, talon_archer 2, roc_rider 1, stormcaller 1} | Mixed armies scaled to the army value as a whole (4.2). Source: the plan compositions in `ai/aggressive_s3.json`. |
| `cases` | list of `{ "id", "kind", "army_values" }` | 1 to 16 entries; unique IDs `[a-z0-9_]+`; `kind` one of `flat`, `high_ground`, `flooded`, `choke`; 1 to 8 army values of 50 to 20 000 | `flat` [750, 1500]; `high_ground` [1500]; `flooded` [1500]; `choke` [1500] | Terrain cases and the army values each is played at (4.3). Placeholder. |
| `arena_width_tiles` | tiles | 16 to 128 | 48 | Arena size along the line of approach. Placeholder. |
| `arena_height_tiles` | tiles | 16 to 128 | 32 | Arena size across it. Placeholder. |
| `start_distance_tiles` | tiles | 2 to 64 | 16 | Gap between the two front ranks at spawn. Source: the 16-tile C2.1 fights. |
| `formation_width_units` | units | 1 to 50 | 10 | Units per rank. Placeholder. |
| `formation_spacing_tiles` | tiles | 0.5 to 4 | 1 | Distance between neighbouring spawn positions, within and between ranks. Placeholder. |
| `ramp_width_tiles` | tiles | 1 to 16 | 4 | Width of the one ramp onto the plateau (`high_ground`). Placeholder. |
| `choke_width_tiles` | tiles | 1 to 16 | 3 | Width of the gap in the wall (`choke`). Placeholder. |
| `choke_wall_thickness_tiles` | tiles | 1 to 8 | 2 | Thickness of the wall (`choke`). Placeholder. |
| `flood_source_unit` | unit ID | a unit with `flood_source_output_per_second` > 0 | `"water.tidepool"` | Structure spawned to flood the arena (`flooded`). |
| `flood_source_offsets` | list of `[dx, dy]` | 1 to 8 entries; whole tiles; inside the arena | `[[0,-8],[0,8],[-4,-8],[-4,8]]` | Flood source tiles relative to the arena centre tile. Placeholder. |
| `flood_warmup_seconds` | seconds | 0 to 600 | 40 | Time the flood spreads before the armies spawn. Placeholder. |
| `flood_min_lane_fraction` | fraction | 0 to 1 | 0.5 | Share of lane tiles (4.3) that must be flooded at army spawn, else the run is a settings error. Placeholder. |
| `reissue_seconds` | seconds | 0.25 to 30 | 2 | Interval of the idle re-targeting rule (4.4). Placeholder. |
| `stalemate_confirm_seconds` | seconds | 0.05 to 60 | 5 | How long "neither side can target the other" must hold to end the fight as a stalemate. Placeholder. |
| `stalemate_idle_seconds` | seconds | 0.05 to 600 | 30 | Time with no damage to any army unit that ends the fight as a stalemate. Placeholder. |
| `max_fight_seconds` | seconds | 1 to 1800 | 180 | Cap. A fight still running at the cap is `unresolved`. Placeholder. |
| `max_value_mismatch_fraction` | fraction | 0 to 1 | 0.15 | Fights whose two army values differ by more than this share of the larger are played and reported, but marked `mismatch` and left out of the checks. Placeholder. |
| `verify_fights` | fights | 0 to 16 | 2 | Fights re-run in-process for the determinism spot check. Placeholder. |
| `chunk_fights` | fights | 1 to 1000 | 25 | Fights per worker process call. Placeholder. |
| `rule_case` | case ID | a case of kind `flat` | `"flat"` | Case the rule checks and the matchups file read. Source: P-005 1.6 rule 3 ("open-ground fight"). |
| `rule_army_value` | resource units | one of that case's `army_values` | 1500 | Army value the checks and the matchups file read. Placeholder. |
| `threats` | list of `{ "id", "side" }` | 0 to 16 entries; `side` a unit or composition ID in the sweep | `air`: skai.roc_rider; `massed_armour`: water.grasper; `massed_light`: skai.windrunner | Threat armies for rule 5. Source: P-005 1.2 threat table (the unit-army rows). |
| `rule1_min_mobile_answers` | unit types | 0 to 20 | 2 | Rule 1. Source: P-005 1.6 rule 1. |
| `rule1_min_static_answers` | unit types | 0 to 20 | 1 | Rule 1. Source: P-005 1.6 rule 1. |
| `rule2_min_multiplier` | fraction | 0 to 3 | 0.5 | Rule 2. Source: P-005 1.6 rule 2. |
| `rule2_max_multiplier` | fraction | 0 to 3 | 1.5 | Rule 2. Source: P-005 1.6 rule 2. |
| `rule2_max_ratio_within_type` | ratio | 1 to 100 | 2 | Rule 2. Source: P-005 1.6 rule 2. |
| `rule3_min_value_left` | fraction | 0 to 1 | 0.2 | Rule 3. Source: P-005 1.6 rule 3. |
| `rule3_max_exchange_ratio` | ratio | 1 to 100 | 3 | Rule 3. Source: P-005 1.6 rule 3. |
| `rule5_min_unit_answers` | unit types | 0 to 20 | 1 | Rule 5, purchase half. Source: P-005 1.6 rule 5. |
| `rule5_min_terrain_answers` | cases | 0 to 20 | 1 | Rule 5, non-purchase half (terrain only for now). Source: P-005 1.6 rule 5. |

Rule 6 has no key here. Its floor and army supply stay in `tuning/harness.json` (`hq_floor_seconds`, `hq_floor_supply`), which the harness already checks against `tuning/hq.json`, so the floor has one source of truth.

**Validation** (exit 1, `DataValidationException` naming `scripts/fights/fights.json` and the field): unknown keys; ranges; duplicate IDs; an unknown `kind`; a composition that mixes factions, names an unknown unit or a non-combat unit, or has an ID with a dot; a `threats` side not in the sweep; a `flood_source_unit` with no flood output; a `rule_case` that is not of kind `flat`, or a `rule_army_value` not in its `army_values`. At plan time (exit 1, `HarnessException` naming the fight ID): a spawn position off the arena or on a tile its layer cannot stand on; `start_distance_tiles` too small for the choke wall (needs `start_distance_tiles >= choke_wall_thickness_tiles + 2`).

### 3.2 Matchups file `rts/data/scripts/fights/matchups.json`

This file is documentation, not simulation input. Each combat unit has one entry keyed by its unit ID. `$schema_version` and `$doc` are the only other top-level keys. Above each entry is a comment line in the house format:

```
// Pummeler (water.grasper): its job and the fights it wins and loses, measured by --fights at rule_army_value on the named case. Unit: unit IDs and case IDs. Range: combat units of the sweep; a case ID in fights.json. Source: P-005 developer decision 1 (rule 7); fights run 18 Nov 2026, data digest <16 hex>.
"water.grasper": {
  "job": "melee fighter that holds the front line",
  "wins":  { "against": "water.ink_lobber", "case": "flat" },
  "loses": { "against": "skai.roc_rider",   "case": "flat" }
}
```

- `job`: 1 to 120 characters, the designed job (P-005 1.1 "What it is for" column for today's units).
- `wins` and `loses`: `against` is a combat unit other than the entry's own unit, of any faction; `case` is a case ID whose `army_values` contain `rule_army_value`. Either may be `null`, meaning no such fight is claimed. The `matchups_agree` check reports a `null` `wins` on an armed unit as a disagreement, the same finding as rule 4b.
- Validation (exit 1, naming `scripts/fights/matchups.json` and the field): unknown keys, an unknown unit or case ID, an entry for a unit outside the sweep. A combat unit with no entry is not an error: `matchups_agree` reports it as missing.

**Out of the content digest, and how that is enforced.** The file sits under `scripts/`, which `DataLoader` skips before parsing (`DataLoader.cs`, `parts[0] == ScriptsFolder`), the same mechanism that keeps `ai_eval.json` out. Only the harness project parses it. Acceptance test 14 guards this: changing `matchups.json`, `fights.json` or `tuning/harness_batch.json` must leave `Tuning.SourceDigest` and `Simulation.ContentDigest` unchanged. That test also covers the `harness_batch.json` guard that `STATUS.md` lists as still to do.

### 3.3 HQ floor value (developer decision 4)

Package F0 sets `tuning/hq.json` `survivability_floor_seconds` and `tuning/harness.json` `hq_floor_seconds` from 20 to 15, with `Source: P-005 developer decision 4 (18 Nov 2026)`. Both files are in the content digest, so **this changes every state hash**. The smoke and economy_smoke pins, `DeterminismTests.GoldenFinalHash` and any replay fixtures must be re-pinned, and the Unity hash check goes stale again. F0 is kept apart from F1 to F3 so that the harness packages change no hash.

## 4. Algorithm

### 4.1 Combat units, value, sides

- **Combat unit**: a definition with speed > 0, `Supply > 0`, no `Hq`, `Economy.FootprintTiles == 0`, `!Economy.CanGather` and a weapon. That is `IsArmy` plus "armed". Unarmed army units (the Tidecaller once built) are not swept as sides in this build (question Q9). Today this gives 9 units in ordinal order: `skai.roc_rider`, `skai.stormcaller`, `skai.talon_archer`, `skai.windrunner`, `water.grasper`, `water.ink_lobber`, `water.reef_runner`, `water.shellback`, `water.spoutling`.
- **Value** of a unit: `cost_common + cost_rare` (integers). This is the AI's value rule `AiCounter.Value` with `counter_rare_cost_weight` 1, as in every shipped profile.
- **Sides**: the swept units in ordinal ID order, then the compositions in file order. A side's faction is that of its units, and a fight's player in a seat plays that faction.

### 4.2 Equal-value armies

For army value `V` and a side whose recipe (a unit counts once, a composition counts its counts) has value `rv`: `k = max(1, (2V + rv) / (2 rv))` (integer division, so the result rounds half up), and the army is `k` times the recipe. Examples: V 1500 gives 15 Pummelers (rv 100) and 8 Brinecasters (rv 190, `3190 / 380 = 8`), the C2.1 case. V 750 gives 4 Roc Riders (800). `water_ai_mix` (rv 790) gives k 1 at 750 and k 2 at 1500 (1580). A fight is `mismatch` when `|value0 - value1| > max_value_mismatch_fraction * max(value0, value1)`.

### 4.3 Fight plan, arena and terrain cases

**Plan order**, which is also the order of the record file: for each case in file order, each of its army values in order, each unordered side pair `(i, j)` with `i < j` in side order, then seat order `(i in seat 0, j in seat 1)` followed by `(j, i)`. Mirrors of one side against itself are not played. Fight ID: `<case>/<value>/<seat-0 side>/<seat-1 side>`, for example `flat/1500/water.grasper/water.ink_lobber`.

**Default sweep size**: 11 sides give 55 pairs and 110 fights per (case, value). Five (case, value) cells give **550 fights**, plus 44 HQ floor trials (6.6). Once the missing roster is built, the Thunderhead and Leviathan join (the unarmed Tidecaller does not), so 13 sides give 78 pairs and 780 fights.

**Arena** (in-memory `MapDefinition`, id `fight_arena`, `arena_width_tiles` by `arena_height_tiles`, spawns at (2, 2) and (W-2, H-2) because the constructor needs them, no fields, no Core, no quake paths, `cycles` null). `cx = W / 2`, `cy = H / 2` (integer). Front columns: `f0 = cx - (d + 1) / 2`, `f1 = f0 + d` with `d = start_distance_tiles`.

**Formation.** A side's units are sorted by weapon range ascending (melee in front), ties by definition ID ordinal, and filled into ranks of `formation_width_units`, rank 0 first. With `s = formation_spacing_tiles` and a rank of `n` units, unit `j` (0-based) of rank `r` stands at:
- seat 0: `x = f0 + 0.5 - r*s`, `y = cy + 0.5 + s*(2j - (n - 1))/2`
- seat 1: `x = f1 + 0.5 + r*s`, same `y`

All values are Fix64. Units spawn at the army spawn tick `S` in plan order, seat 0 first, with ascending command sequence.

**Lane tiles**: the tiles with `f0 <= x <= f1` and `|y - cy| <= (formation_width_units * s) / 2`.

| Kind | Terrain | Orders |
| --- | --- | --- |
| `flat` | All height 0. | Both seats attack (4.4). |
| `high_ground` | Plateau at height 1 for every column `x >= f1 - 2`. Ramp: column `f1 - 3`, rows `cy - ramp_width_tiles/2` up to `ramp_width_tiles` rows, height 0 with `IsRamp`. Seat 1 stands on the plateau. | Seat 0 attacks; seat 1 holds and gets no command. Idle units fire at enemies in weapon range (combat rule), so the holders defend from the plateau. Low-ground units see the plateau only from the ramp or from the air (fog_of_war.md high-ground rule), and uphill ranged hits do `uphill_ranged_damage_multiplier`. |
| `flooded` | Flat. A third player, slot 2, team 1 (seat 1's team), faction = `flood_source_unit`'s faction, owns one `flood_source_unit` at each offset from `(cx, cy)`, spawned at tick 0. The armies spawn at `S = SecondsToTicks(flood_warmup_seconds)`. The flood is seat 1's own water: seat 1's ground units are not slowed and its water units heal; seat 0's ground units are slowed. | Both seats attack. Flood sources are not army: they never count toward value, the hunt rule never targets them, and damage to them does not reset the idle timer. If fewer than `flood_min_lane_fraction` of lane tiles are flooded at `S`, the fight fails with a settings error naming the case and the measured fraction. The flooding mechanic exists in the match because slot 2 is a water player. |
| `choke` | Flat with a wall of height 1 (no ramp) on columns `cx - t/2` up to `t` columns (`t = choke_wall_thickness_tiles`), except the gap rows `cy - g/2` up to `g` rows (`g = choke_width_tiles`). Air units fly over it. | Both seats attack. |

`S` is 0 except in `flooded`. Players: slot 0 team 0 and slot 1 team 1, named `"seat 0"` and `"seat 1"`. Mechanics come from `BuiltInMechanics.CreateRegistry()`, so momentum, flooding, statuses and the dome all run as in a match.

### 4.4 Control: attack-move with idle re-targeting

The controller runs between steps and submits commands for the next tick as the seat's player, with sequences from 0 per issuer per tick in ascending entity ID. It reads state only.
1. Tick `S + 1`: every unit of an attacking seat gets `AttackMoveCommand` to the enemy front-rank centre, `(f1 + 0.5, cy + 0.5)` for seat 0 and `(f0 + 0.5, cy + 0.5)` for seat 1.
2. Ticks `S + 1 + m*R` for m >= 1, with `R = SecondsToTicks(reissue_seconds)`: every unit of an attacking seat that is idle (`!HasMoveTarget && AttackTarget == EntityId.None`) gets `AttackMoveCommand` to the position of the nearest live enemy army unit whose `EffectiveLayer` its weapon can target. Nearest means squared centre distance in Fix64, ties to the lower entity ID. A unit with no such enemy gets no command.

This is the "attack-move at each other" baseline. Rule 2 only stops a fight from stalling when the enemy is out of acquire range, and it never moves a unit that is fighting.

### 4.5 Result

After each `Step` at tick `t >= S`, with `alive0` and `alive1` the live army units of seats 0 and 1, the checks below run in this order and the first that matches ends the fight. `end_tick = t - S`.

| Order | Condition | `result` | `reason` |
| --- | --- | --- | --- |
| 1 | both empty | `draw` | `both_eliminated` |
| 2 | one empty | the other seat (`seat0` or `seat1`) | `eliminated` |
| 3 | for `SecondsToTicks(stalemate_confirm_seconds)` consecutive ticks (tick `S` counts as the first), no unit of either seat has a weapon that can target the `EffectiveLayer` of any live enemy army unit | `stalemate` | `cannot_target` |
| 4 | `t - lastDamage >= SecondsToTicks(stalemate_idle_seconds)`, where `lastDamage` starts at `S` and is set by every `UnitDamagedEvent` with damage > 0 on an army unit of seat 0 or 1 | `stalemate` | `no_damage` |
| 5 | `t - S >= SecondsToTicks(max_fight_seconds)` | `unresolved` | `time_cap` |

**Value left** of a seat, in Fix64: `sum over alive units of FromInt(value) * Health / MaxHealth`, divided by the seat's start value. It is a fraction from 0 to 1, since healing is capped at full health. **Exchange ratio** of the winner (tool-side `double`): `enemy start value / (own start value * (1 - own value left))`, or infinity when the winner lost nothing.

### 4.6 Seeds, workers and determinism

- Fight seed: `SeedDerivation.GameSeed(base_seed, "fights", fightId, 0)`.
- Coordinator: plans every fight, splits the plan into chunks of `chunk_fights`, and runs `--worker --fights --first <i> --count <n> --data <dir> --seed <s>` through `WorkerPool` (`--jobs`, default processor count, 1 to 64). A worker re-plans from the data, plays its fights in plan order and writes one JSON line per fight. HQ floor trials run as `--worker --fights --hq-floor <attacker faction>:<side>:<defender faction>:<rooted|uprooted>`, where `<side>` is a unit or composition ID. The coordinator sorts by plan index. Worker failures and malformed or missing lines are exit 1, as in `--batch`.
- Spot check: the first `verify_fights` fights of the plan are re-run in-process, and their JSON lines must be identical (else exit 3).
- No output depends on `--jobs`, scheduling or the clock. Elapsed time goes to stderr only.

## 5. Output

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --fights [--jobs J] [--seed S] [--only <fight id>] [--out <dir>] [--strict]
```

- `--only` plays one fight in-process and prints its JSON line, with no files and no checks. It is for debugging.
- `--out` defaults to `rts/build/harness/fights`. The mode writes `fights.jsonl`, `hq_floor.jsonl` (the existing `HqFloorRecord` line, where the `unit` field holds the side ID) and `fights.md`.
- stdout: progress `fights <done>/<total>` in chunk order, one `CHECK <name> PASS|FAIL <detail>` per check, then `FIGHTS fights=<n> decided=<n> draws=<n> stalemates=<n> unresolved=<n> mismatched=<n> checks_passed=<n> of=<m>`.
- Exit codes: 0 done (failed checks included); 1 error; 2 a check failed and `--strict` was given; 3 spot-check mismatch.

**Record** (one line, keys in this order): `fight`, `case`, `army_value`, `seat0`, `seat1` (side IDs), `units0`, `units1` (objects of unit ID to count, ordinal), `value0`, `value1` (ints), `mismatch` (bool), `result`, `reason`, `end_tick`, `seconds` (one decimal), `left0`, `left1` (three decimals), `alive0`, `alive1`, `flooded_lane_tiles` (-1 outside `flooded`), `seed` (decimal text), `state_hash` (16 hex). Numbers are formatted in the invariant culture.

**fights.md** (exact headings):
1. `<!-- fights.md: written by the balance harness --fights (...); not a source file. -->`, `# Equal-value fights`, bullets: data digest, base seed, value rule, control rule, fight count, then the bold `FIGHTS ...` line.
2. `## Design-rule checks`: table `| Check | Rule | Threshold | Result | Detail |`, one row per check in section 6 order. Detail lists every failing item.
3. `## Value exchange: <case>, <value>`, one per (case, value): a matrix with sides as rows and columns. Cell (r, c) is `left_r - left_c` with sign and two decimals, taken from the fight with r in seat 0 and c in seat 1. On `high_ground` r is the attacker; on `flooded` r fights in c's flood. `S` marks a stalemate, `U` unresolved, `–` mismatch, `·` the diagonal, and a cell gets `*` when the two seats of the pair disagree on the winner. A line under the table states the seat convention.
4. `## HQ floor (Rooted >= <floor> s against <supply> supply)`: `| Attacker | Army | Count | Defender HQ | State | Time to kill | Result |`.
5. `## Matchups file`: `| Unit | Claim | Measured | Agrees |`.
6. `## Suggested matchups entries`: per combat unit, the measured best win and worst loss ("Suggested entries" in section 6). This is the source for new entries.
7. `## Stalemates, unresolved fights and seat splits`, then `## All fights` (`| Fight | Result | Reason | Seconds | Left 0 | Left 1 | Values |`).

## 6. Checks

All checks read fights on `rule_case` at `rule_army_value` and skip `mismatch` fights, except where a check names other cases. **A beats B** means A's side won in both seats. **Split** means the seats disagree. A check never changes the exit code unless `--strict` is given. Each check is a pure function `FightChecks.<Name>(records, settings, matchups, data)` returning PASS or FAIL with detail lines.

1. **`rule1_no_immunity`** (data lint). For each faction F with combat units and each combat unit or HQ U of another faction: at least `rule1_min_mobile_answers` of F's combat units, and `rule1_min_static_answers` of F's static defences (`Economy.IsStaticDefence`), have a weapon with `CanTarget(U.Layer)`. In addition, every armed combat unit can target `Ground` (a Grounded flyer counts as ground, statuses.md). Detail example: `water: skai.roc_rider attacked by 1 mobile unit (water.spoutling), needs 2`.
2. **`rule2_multiplier_band`** (data lint on `combat.json`). Every multiplier is within [`rule2_min_multiplier`, `rule2_max_multiplier`], and within each damage type the highest over the lowest is at most `rule2_max_ratio_within_type`.
3. **`rule3_counter_margin`**. For each matchups entry whose `loses` names Y, Y is that unit's stated counter. Y must beat the unit, and in each seat Y's value left must be at least `rule3_min_value_left` and Y's exchange ratio at most `rule3_max_exchange_ratio`. This is read on the `loses.case` (default the flat case).
4. **`rule4_no_dominant_unit`**. No combat unit beats every other combat unit of its own faction (n/a for a faction with one unit).
5. **`rule4_every_unit_wins`**. Every combat unit beats at least one combat unit of another faction.
6. **`rule5_two_answers`**. For each threat T and each faction F: the unit answers are F's combat units that beat T on `rule_case`, and there must be at least `rule5_min_unit_answers` of them. The terrain answers are the non-flat cases on which an F unit that does not beat T on flat wins against T in the seat where F holds the terrain (seat 1 on `high_ground` and `flooded`, both seats on `choke`), at the case's army value nearest `rule_army_value` (lower on a tie). There must be at least `rule5_min_terrain_answers` of them. When T is a unit of F itself, that unit is not its own answer. The detail line ends `(non-purchase answers measured: terrain only)`, because static defence, HQ and mechanic answers are not measured yet.
7. **`rule6_hq_floor`**. `HqFloorTrial.Plan` for every ordered pair of factions with an `hq_unit` (mirrors included), extended so that each composition of the attacker's faction is also an attacker with `k = hq_floor_supply / recipe supply` (integer division, skipped with a warning when 0), placed in recipe order in the existing columns of 5. Rooted trials must last at least `SecondsToTicks(hq_floor_seconds)` (15 s after F0). Uprooted trials are reported only, as in `--batch`.
8. **`matchups_agree`**. Every combat unit has an entry; every `wins` is a measured beat on its case; every `loses` is a measured loss on its case; a `null` `wins` on an armed unit counts as a disagreement. Detail example: `water.grasper: claims it beats water.ink_lobber on flat; measured seat 0 win (left 0.41), seat 1 loss (left 0.00)`.

**Suggested entries** (report only, not a check). For unit X, `wins` is the opponent X beats with the highest mean value left over both seats, and `loses` is the opponent that beats X with the highest mean value left. Ties go to an other-faction opponent first, then ordinal ID. Either is `null` when no such fight exists.

## 7. Adding mechanics later

Generic, needing no change when a mechanic lands:
- Unit discovery, the value rule, army scaling, the plan, seats, the result rules, records, the matrix, and checks 1 to 8. A new unit joins the sweep by existing, and `matchups_agree` fails until it has an entry, which is how rule 7 is enforced.
- Everything inside the simulation: area damage, the structure multiplier, Downdraft's status, flood-bound targeting, walls as blockers. They act in fights because fights run the real `Simulation`.

What a new mechanic must add:

| Mechanic | Add |
| --- | --- |
| Area damage (S-1) | A formation dimension: a `formations` list with today's spacing as `block` and a `spread` entry with a wider spacing, so clumped and spread armies are both measured. |
| Per-weapon structure multiplier (S-2) | Nothing. It shows in `rule6_hq_floor`. |
| Water anti-air while on water (decision 3) | A public combat query `CanAttack(sim, attacker, target)` in Rts.Sim that this mode's `cannot_target` test and rule 1 then call instead of `Weapon.CanTarget(EffectiveLayer)`. Without it a conditional target rule makes `cannot_target` declare false stalemates. Also, rule 1 must count conditional answers and name them. |
| Walls | A terrain kind `walled` that spawns wall structures with `SpawnUnitCommand` (as `flooded` spawns flood sources), and a wall count among the non-purchase answers of rule 5. |
| Abilities (Downdraft, waves, Tidecaller) | Plain attack-move never casts. Each ability needs a control entry, for example "cast on cooldown at the nearest enemy group", added as a `controls` list with `attack_move` as today's only entry; and the Tidecaller as a side once unarmed units are swept (Q9). |
| Planetary Cycles (Windstorm range) | A case field that forces a cycle at `S`. |
| Tech prerequisites | Nothing, because fights spawn units directly. |

## 8. Acceptance tests (`rts/tests/Sim.Tests/Harness/FightTests.cs`)

Quick tests use `CopyData` with `units` limited to `["skai.windrunner", "water.grasper", "water.ink_lobber"]`, `compositions: []` and one case `flat` [1500] (6 fights).

1. **Settings validation**. Each edit below gives exit 1 with a message naming the file and field: an unknown key; `kind: "swamp"`; composition `{skai.windrunner 1, water.grasper 1}` (mixed factions); composition ID `a.b`; a threat side `skai.nothing`; `flood_source_unit: "water.grasper"`; `rule_army_value: 999`; `rule_case: "choke"`; a matchups entry naming `water.nothing`; a matchups `case: "nowhere"`. Plan-time error: `start_distance_tiles: 60` with the shipped arena names the first fight.
2. **Plan**. Shipped settings: the 9 combat units in the order of 4.1, 11 sides, 550 fights; the first ID is `flat/750/skai.roc_rider/skai.stormcaller` and the second `flat/750/skai.stormcaller/skai.roc_rider`. Army counts: V 1500 gives 15 grasper and 8 ink_lobber; V 750 gives 4 roc_rider and 8 grasper; `water_ai_mix` at 1500 is k 2 (1580); no shipped fight is `mismatch`. With `max_value_mismatch_fraction: 0.01`, `flat/750/skai.roc_rider/water.grasper` (800 against 800) is not mismatch and `flat/750/water.grasper/water.ink_lobber` (800 against 760) is.
3. **Formation**. `flat/1500/water.grasper/water.ink_lobber`: grasper rank 0 at x 16.5, y 12.0 to 21.0 in steps of 1; rank 1 at x 15.5, y 14.5 to 18.5. ink_lobber rank 0 at x 32.5, y 13.0 to 20.0.
4. **Known case**. `flat/1500/water.grasper/water.ink_lobber` and its swap: the grasper side wins both (`reason` `eliminated`, its value left > 0).
5. **Stalemate, cannot target**. Edit `skai.roc_rider` `can_target` to `["air"]`. `flat/1500/skai.roc_rider/water.grasper`: `stalemate`, `cannot_target`, `end_tick` 99 (5 s confirm), both values left 1.000.
6. **Stalemate, no damage**. `stalemate_idle_seconds: 1`: the first fight ends `stalemate`, `no_damage`, `end_tick` 20 (the armies are 16 tiles apart and cannot touch within 1 s).
7. **Cap**. `max_fight_seconds: 1`, `stalemate_idle_seconds: 30`: `unresolved`, `time_cap`, `end_tick` 20.
8. **Determinism**. The quick sweep run twice gives byte-identical `fights.jsonl`, `hq_floor.jsonl`, `fights.md` and stdout.
9. **Order independence**. The quick sweep with `--jobs 1` and `--jobs 3` gives byte-identical files and stdout.
10. **Flooded**. Shipped `flooded` case, `flooded/1500/water.grasper/water.ink_lobber`: `flooded_lane_tiles` is at least `flood_min_lane_fraction` of lane tiles; no flood source counts in `value0` or `value1`. With `flood_warmup_seconds: 0` the run fails with the lane-fraction error.
11. **High ground**. `high_ground/1500/...`: seat 1 issues no command in the whole fight; plateau tiles have height 1 and the ramp tiles `IsRamp`.
12. **Choke**. The arena has height 1 on the wall columns except the `choke_width_tiles` gap rows.
13. **Checks**. Shipped data: `rule2_multiplier_band` FAIL listing `drench/structure 0.25 below 0.5` and `drench ratio 4.00 above 2`; `rule1_no_immunity` FAIL listing `water: skai.roc_rider attacked by 1 mobile unit (water.spoutling), needs 2`. Using hand-built records (no simulation): A beats B with A's value left 0.90 fails rule 3 (exchange 10.0 above 3); with 0.50 it passes; a unit beating both others of its faction fails rule 4a; a matchups entry claiming `water.grasper` loses to `water.ink_lobber` gives a `matchups_agree` disagreement in the detail format of section 6. Rule 6: `skai_ai_mix` (supply 13) gives k 2 against each HQ.
14. **Digest guard**. Editing the text of `scripts/fights/matchups.json`, `scripts/fights/fights.json` or `tuning/harness_batch.json` leaves `Tuning.SourceDigest` and `Simulation.ContentDigest` unchanged; editing `tuning/hq.json` changes them (control).
15. **Strict**. A failing check gives exit 0 without `--strict` and exit 2 with it; `CHECK ... FAIL` is printed either way.
16. **Existing modes**. Every existing harness test still passes unchanged.

## 9. Packages (build in order)

| Package | Content | Tier | Review |
| --- | --- | --- | --- |
| F0 | HQ floor 20 to 15 s in `hq.json` and `harness.json` (3.3); re-pin every hash it moves; STATUS hash lines. | Sonnet builder | sim-reviewer glance (data-only hash change) |
| F1 | `FightSettings` and `fights.json` (3.1); combat-unit discovery, scaling, plan, arenas, formation (4.1 to 4.3); `FightController` (4.4), `FightObserver` and `FightRunner.Run` with the result rules (4.5); the record writer; `--only`. Shared `IsArmy` helper. Tests 1 to 7, 10 to 12. | Sonnet builder | Opus review of the result rules recommended (harness only, so no sim-reviewer pass is needed) |
| F2 | Coordinator: workers, chunks, spot check, `fights.md` matrix and tables, stdout, exit codes, `--strict`; `HqFloorTrial` composition attackers; README section. Tests 8, 9, 15, 16. | Sonnet builder | none beyond tests |
| F3 | `FightChecks` (checks 1 to 8 of section 6), matchups parser and validation (3.2), suggested entries. Tests 13, 14. | Sonnet builder | none beyond tests |
| F4 | Run the default sweep; write `matchups.json` for the 9 units from the suggested entries (jobs from the P-005 1.1 table, comments per the standard, digest in each Source); commit `docs/reports/fights_<date>/` (`fights.md`, `fights.jsonl`, `hq_floor.jsonl`, wall time); give the coordinator the check results. | Sonnet builder; Haiku for the comment pass | coordinator reads the report |

Runtime: one 25-unit Pummeler-against-Brinecaster fight (1220 ticks on a 64 by 64 map) took 0.8 s in a single process on the cloud machine (measured 17 Nov 2026). At roughly 0.5 to 1 s per fight on the smaller arena, the 550 fights plus 44 floor trials are about 6 to 9 minutes of processor time, so **about 2 to 3 minutes on 4 cores**. F4 records the real figure. If it exceeds 5 minutes, cut `army_values` in data, not code.

## 10. Out of scope

AI unit control as a second column (Q1); unarmed sides (Q9); formations, abilities, walls and cycles (section 7); a comparison against `FightPredictor`; any `rts/src/Sim` or `rts/src/Ai` change; CI wiring.

## Questions for the developer

Each has a recommended answer the builder uses until the developer replies.

- **Q1. AI unit control as a second column?** Recommended: not now. Add it once D.6 (kiting, focus fire, fallback) is moved out of `AiBrain` into a standalone controller. The difference between the two columns then shows how much a result depends on micro.
- **Q2. Weight of the rare resource in value.** Recommended: 1, as the AI uses (`counter_rare_cost_weight`).
- **Q3. Value left counts health, not only whole units.** Recommended: health-weighted, so a win with every unit at 1 health shows as a near-even trade.
- **Q4. "Beats" needs a win in both seats; a seat split counts as neither.** Recommended: yes.
- **Q5. Rule 5's non-purchase half counts only terrain until static defence, HQ and mechanic cases exist.** Recommended: accept, reported as "terrain only". A FAIL there may be a false fail.
- **Q6. Rules 3 and 4 read only open ground at 1500.** Recommended: yes. Terrain changes who wins on purpose.
- **Q7. Uprooted HQ floor.** Recommended: reported, not enforced, as in `--batch`.
- **Q8. Export a fight as a command script on a committed arena map, to watch it in Unity?** Recommended: later, if the developer wants to watch fights. It needs a map file that `--batch` and `--ladder` skip.
- **Q9. Sweep unarmed units (Tidecaller) as sides?** Recommended: not until its waves have a control entry (section 7). Plain attack-move would only measure it as a target.
- **Q10. The flood always belongs to seat 1, also in Skai-against-Skai pairs.** Recommended: yes, so every pair is measured once with the flood on each side.
- **Q11. Melee in front, ranged behind, in every formation.** Recommended: yes, as a player would deploy.
- **Q12. The auto-filled `loses` is each unit's worst loss, which will often break rule 3's 3-to-1 cap.** Recommended: keep it measured as the starting content. The developer edits a unit's `loses` to its intended counter when a design is settled, and rule 3 then tests the intent.

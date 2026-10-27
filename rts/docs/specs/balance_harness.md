<!-- rts/docs/specs/balance_harness.md -->
<!-- Implementation spec for the full-scale balance harness: N AI-versus-AI games per matchup per map in parallel worker processes with derived seeds, win rate with a confidence interval, match length, HQ time-to-kill, the hard limits (HQ survivability floor; water versus earth median, deferred), the Markdown report, the CI reduced batch versus the local 500 and the exit codes. Part of rts/docs/specs (index: README.md); PHASE2_PLAN.md step 5, built by a Sonnet builder in rts/tools/BalanceHarness. -->
# Spec: Balance harness at full scale (`rts/tools/BalanceHarness`, batch mode)

## 1. Purpose and design-doc source

- Matchups and balance / Balance philosophy: "run headless AI-versus-AI simulations every patch, 500 games per matchup per map" (`games_per_matchup_per_map`); soft counters only; hard limits are checked by the harness.
- Survivability floor (The mobile HQ unit): the HQ survives at least 20 s (`hq_floor_seconds`) against the strongest HQ-killing army of 30 supply (`hq_floor_supply`).
- Balance philosophy: the median water versus earth match lasts at most 1500 s (`water_vs_earth_median_max_seconds`). No earth faction exists yet, so this limit is reported **DEFERRED** until one does.
- PHASE2_PLAN exit test: a reduced batch (20 games per matchup on each map) in CI and the full 500 locally; the report shows every hard limit.
- Architecture rule 9: everything runs headless. The harness is a tool, not simulation code: it may use processes, files, `double` for statistics and the processor count, but never feeds any of them back into a simulation.

The existing single-run mode (`--script`, `--ticks`, `--seed`, `--ai`) is unchanged; CI's determinism job keeps using it.

## 2. Data

**New keys in `tuning/harness.json`** (add to `HarnessTuning` with the same comment text). Every value is **PLACEHOLDER** unless a source is given.

| Key | Unit | Range | Value | Controls |
| --- | --- | --- | --- | --- |
| `ci_games_per_matchup_per_map` | games (count) | 1 to 500 | 20 | Reduced batch size for CI. Source: PHASE2_PLAN exit test ("for example 20"). |
| `base_seed` | integer | 0 to 2 147 483 647 | 20261026 | Seed every game seed is derived from (section 4). |
| `matchups` | list of matchup IDs | 1 to 64 entries | `["skai_vs_skai", "skai_vs_water", "water_vs_water"]` | Faction pairs played; each `<a>_vs_<b>` with `a <= b` ordinal, both factions with an `hq_unit`. Mirrors measure spawn bias. Add `earth_vs_water` etc. when earth lands. |
| `maps` | list of map IDs | 0 to 32 entries | `[]` | Maps played; empty means every map with exactly 2 spawns, in ordinal ID order. |
| `ai_profile` | AI profile ID | a file under `rts/data/ai` | `"aggressive"` | Profile both slots play, so results measure factions, not AI styles. |
| `max_game_seconds` | seconds | 300 to 7200 | 2400 | Tick limit per game; a game still undecided there is **undecided**. |
| `win_rate_band_min` | fraction | 0 to 0.5 | 0.45 | Lower edge of the acceptable win rate of the first-named faction. |
| `win_rate_band_max` | fraction | 0.5 to 1 | 0.55 | Upper edge. |
| `confidence_z` | z-score | 1 to 4 | 1.96 | Width of the Wilson interval (1.96 is 95 percent). Source: standard statistics. |
| `max_undecided_fraction` | fraction | 0 to 1 | 0.1 | Warn when more of a cell's games than this are undecided. |
| `median_cap_matchup` | matchup ID | any `<a>_vs_<b>` | `"earth_vs_water"` | Matchup the `water_vs_earth_median_max_seconds` limit applies to. Source: Balance philosophy. |
| `hq_floor_max_test_seconds` | seconds | 30 to 1800 | 300 | Longest HQ floor trial; an HQ alive at the end reports "> N s" and passes. |
| `hq_arena_size_tiles` | tiles | 32 to 128 | 48 | Side of the flat HQ floor arena (section 4). |
| `hq_floor_standoff_tiles` | tiles | 0 to 16 | 2 | Gap between the HQ's weapon reach and the nearest attacker at spawn. |
| `verify_games_per_cell` | games (count) | 0 to 10 | 2 | Games per (map, matchup) re-run in fresh processes to check determinism. |
| `chunk_games` | games (count) | 1 to 500 | 25 | Games per worker process call. |

`games_per_matchup_per_map` (500), `hq_floor_seconds` (20), `hq_floor_supply` (30) and `water_vs_earth_median_max_seconds` (1500) exist. One truth for the floor: the harness reads `harness.json` and refuses to run (exit 1, `"tuning/hq.json survivability_floor_seconds must equal tuning/harness.json hq_floor_seconds"`) when `hq.json`'s `survivability_floor_seconds` / `survivability_floor_supply` differ.

**Digest request**: `harness.json` is read by no simulation system; the coordinator removes it from `Tuning.SourceDigest`, so changing a batch size never changes state hashes or invalidates replays (sim-reviewer pass).

**New key in `tuning/economy.json`**: `starting_worker_count` (workers, 0 to 20, value 4, Source: Core gameplay loop / Opening "spawn with the mobile HQ unit and a few workers"; PLACEHOLDER count). Read by the standard match start below; the Unity skirmish lobby (plan step 9) uses the same helper.

**Standard match start** (`Rts.Sim.Data.StandardMatchStart.Commands(DataSet data, MapDefinition map, IReadOnlyList<PlayerSetup> players)`, pure, returns `spawn_unit` commands at tick 0, issuer `PlayerId.None`, sequence from 0): for each player ascending slot, the faction's `hq_unit` at spawn point `slot` exactly as the map gives it (`map.Spawns[slot]`; the spawn tile is `TileOf` of that point), then `starting_worker_count` of the HQ's worker unit on the first tiles, by Chebyshev ring 1 to 3 around the spawn tile and ascending index per ring, that have the spawn tile's height, no ramp and no resource field, at tile centres. Fewer tiles than workers is an `InvalidOperationException` naming the map and slot. The map checklist (plan step 6) should require that each spawn lies within `hq_root_max_distance_to_field_tiles` of a field, so the HQ starts Rooted.

## 3. State

The harness holds no simulation state between games. Per game it produces one **game record** (immutable, also one JSON line in `results.jsonl`):

| Field | Meaning |
| --- | --- |
| `map`, `matchup`, `game` | Cell and game index (0 to N-1). |
| `seed` | Derived seed (decimal text, it is a ulong). |
| `slot0`, `slot1` | Faction per slot after the side swap. |
| `result` | `"slot0"`, `"slot1"`, `"draw"` or `"undecided"`. |
| `end_tick` | Last tick stepped. |
| `state_hash` | `STATE_HASH` after the last tick, 16 hex digits. |
| `hq_kills` | List of `{ "faction", "first_damage_tick", "death_tick" }` for every HQ that died. |

Plus one **HQ floor record** per trial: `attacker_faction`, `unit`, `count`, `defender_hq`, `hq_state` (`rooted` or `uprooted`), `first_damage_tick`, `death_tick` (or -1), `ttk_ticks` (or -1 for survived).

## 4. Algorithm

**Cells and games.** Maps (from `maps`, or every 2-spawn map) times `matchups`, both ordinal. `N` = `--games`, else `ci_games_per_matchup_per_map` with `--ci`, else `games_per_matchup_per_map`. Game `i` of matchup `a_vs_b`: even `i` puts `a` in slot 0 (spawn 0) and `b` in slot 1; odd `i` swaps them, cancelling spawn bias. Players: slot 0 team 0, slot 1 team 1, both AI `ai_profile`, names `"<faction> AI"`.

**Seed derivation** (pure, `SeedDerivation.GameSeed`): `key = mapId + "/" + matchupId`; `h = FNV-1a 64` over the key's UTF-8 bytes (offset `0xCBF29CE484222325`, prime `0x100000001B3`); `x = baseSeed ^ h ^ (ulong)gameIndex`; seed = SplitMix64 finaliser of `x + 0x9E3779B97F4A7C15`: `z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9; z = (z ^ (z >> 27)) * 0x94D049BB133111EB; z ^ (z >> 31)` (unchecked). The AI generators derive from this seed as today.

**One game** (`GameRunner.Play`, also what a worker runs): build the `Simulation` from the data set, the map, the two players, `BuiltInMechanics.CreateRegistry()` and `data.Cycles.Values`; `Start`; submit `StandardMatchStart.Commands`; attach two `SkirmishAiController`s; then the existing loop (`AiHost.ThinkAll`, `Step`) until `MatchOutcome` is decided or `SecondsToTicks(max_game_seconds)` ticks have run. While stepping, track per HQ unit (definition with `Hq != null`) the tick of its first `UnitDamagedEvent` with damage above 0 and its `UnitRemovedEvent` tick. Result: decided with a winner team gives the slot of that team; a draw is `"draw"`.

**Parallel processes.** `--batch` is the coordinator. It splits every cell into chunks of `chunk_games` consecutive game indices and runs up to `--jobs` (default `Environment.ProcessorCount`, 1 to 64) worker processes at a time: the same executable with `--worker --data <dir> --map <id> --matchup <id> --first <i> --count <n> --games <N> --seed <base>`. A worker loads the data once, plays its games in order and writes one game-record JSON line per game to stdout, then exits 0. A worker that exits non-zero, writes a malformed line or returns the wrong count fails the batch (exit 1) with its stderr. The coordinator sorts all records by (map, matchup, game) before anything else, so **the report never depends on `--jobs` or on scheduling**. The HQ floor trials run as further worker calls (`--worker --hq-floor <attacker>:<unit>:<defender>:<state>`), one record each.

**Determinism spot check.** After the batch, the first `verify_games_per_cell` games of every cell are re-run in new worker processes; each pair must have equal `result`, `end_tick` and `state_hash`. Any difference is a determinism failure (exit 3) listed in the report.

**HQ floor trials** (the hard limit). For every ordered pair (attacker faction A, defender faction D) appearing in `matchups` (both orders, mirrors included), for every unit type U of A that is mobile, has `Supply > 0`, is not an HQ and has a weapon that can target D's HQ definition layer: `count = hq_floor_supply / Supply(U)` (integer division; `EconomyRules` supply cost). Two trials per (A, U, D): HQ Rooted and HQ Uprooted. Arena: an in-memory flat `MapDefinition` of `hq_arena_size_tiles` square, heights 0, one common field (amount 1 000 000) at (8, size/2), spawns at (4, 4) and (size-4, size-4), no Core, no cycles. Defender (slot 1, team 1) HQ of D at (12.5, size/2 + 0.5) for Rooted (within root distance of the field), at (size/2 + 0.5, size/2 + 0.5) for Uprooted (field out of range); no defender workers. Attackers (slot 0, team 0): `count` units of U spawned at tick 0 on tile centres in columns of 5, starting `gap = ceil(HQ weapon range + HQ radius) + hq_floor_standoff_tiles` tiles east of the HQ tile, column `c = i / 5` at `x + gap + c`, row `y - 2 + i % 5`. On tick 1 each attacker gets `AttackUnitCommand` (issuer slot 0, sequences ascending ID) on the HQ. Run until the HQ is removed or `hq_floor_max_test_seconds`. `ttk_ticks = death_tick - first_damage_tick`. The HQ defends itself with its own weapon (Rooted only, as in play); there are no other defenders. Mechanics are those of A and D (Momentum and flooding run).
- **Hard limit HQ floor**: the minimum `ttk_ticks` over all **Rooted** trials (survived trials count as infinite) must be at least `SecondsToTicks(hq_floor_seconds)` (400 ticks). Uprooted trials are reported, not enforced (design call; open question).

**Statistics** (pure, `HarnessStats`, `double` allowed here and only here):
- *Win rate* of the first-named faction `a` in a cell: `n` = decided games plus draws (undecided excluded and counted separately); score `s = wins_a + 0.5 * draws`; `p = s / n`. Wilson interval with `z = confidence_z`: `centre = p + z^2/(2n)`, `half = z * sqrt(p(1-p)/n + z^2/(4n^2))`, `low = (centre - half) / (1 + z^2/n)`, `high = (centre + half) / (1 + z^2/n)`, clamped to [0, 1]. For a mirror, `a` is slot 0. Flags: **outside band** when `p` is outside [`win_rate_band_min`, `win_rate_band_max`]; **significant** when the whole interval is outside the band. Both are warnings, not hard limits.
- *Match length*: over decided games, ticks `end_tick + 1`, sorted ascending; median = element `(n - 1) / 2` (lower median, integer division), p10 = element `(n - 1) * 10 / 100`, p90 = element `(n - 1) * 90 / 100`; printed in seconds with one decimal (`ticks / ticks_per_second`).
- *HQ time-to-kill in games* (information): per cell and killed HQ faction, the median of `death_tick - first_damage_tick` over `hq_kills`.
- **Hard limit median cap** for `median_cap_matchup`: DEFERRED (and printed as such) while either faction of it has no faction file, or no map is played; otherwise for each map the median match length must be at most `water_vs_earth_median_max_seconds`.
- Formatting: invariant culture; percentages with one decimal (`50.0%`), intervals `[29.9, 70.1]`.

## 5. Command line, outputs and exit codes

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --batch [--ci | --games N] [--jobs J]
    [--maps a,b] [--matchups x_vs_y,...] [--seed S] [--max-game-seconds T] [--verify K] [--out <dir>] [--report-only]
```

| Option | Meaning | Default |
| --- | --- | --- |
| `--batch` | Batch mode (all other single-run options are then errors) | off |
| `--ci` / `--games N` | Reduced size / explicit games per cell (1 to 5000); mutually exclusive | `games_per_matchup_per_map` |
| `--jobs J` | Worker processes at once, 1 to 64 | processor count |
| `--maps`, `--matchups` | Comma lists overriding `maps` / `matchups`; each must exist | data |
| `--seed S` | Overrides `base_seed` | data |
| `--max-game-seconds T` | Overrides `max_game_seconds` (tests) | data |
| `--verify K` | Overrides `verify_games_per_cell` | data |
| `--out <dir>` | Output folder, created if missing | `rts/build/harness` (git-ignored) |
| `--report-only` | A failed hard limit still exits 0 (CI until the first balance pass) | off |

Outputs in `--out`: `report.md`, `results.jsonl` (game records, sorted), `hq_floor.jsonl` (trial records, sorted by attacker, unit, defender, state). stdout: progress lines `cell <map> <matchup> <done>/<N>` and the final `HARNESS RESULT=<PASS|FAIL|DETERMINISM_FAIL>`; elapsed wall time goes to stderr only, so stdout and the files are byte-identical between runs.

Exit codes: **0** every hard limit passed or is deferred (or `--report-only`); **1** an error (arguments, data, missing map, worker crash); **2** a hard limit failed; **3** the determinism spot check failed (wins over 2).

**report.md layout** (exact headings; tables sorted as the records are):
```markdown
# Balance report
Data digest `<16 hex: ContentDigest of the first game>` · base seed 20261026 · 20 games per matchup per map · AI profile `aggressive` · max game 2400 s

## Hard limits
| Limit | Requirement | Measured | Result |
| --- | --- | --- | --- |
| HQ survivability floor (Rooted) | >= 20.0 s against 30 supply | 13.3 s (skai.windrunner x30 vs water.grand_shell) | FAIL |
| Water vs earth median length | <= 1500.0 s | no earth faction | DEFERRED |

## Win rates
| Map | Matchup | Games | Decided | Draws | Undecided | First faction wins | 95% interval | Flags |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| greybox_1v1 | skai_vs_water | 20 | 19 | 0 | 1 | 52.6% | [31.7, 72.7] | |

## Match length (decided games)
| Map | Matchup | Median | p10 | p90 |

## HQ time-to-kill
### Floor trials
| Attacker | Unit | Count | Defender HQ | State | Time to kill |
### In games (median, information only)
| Map | Matchup | Killed HQ | Kills | Median |

## Determinism spot check
| Map | Matchup | Games re-run | Mismatches |

## Warnings
- greybox_1v1 skai_vs_skai: 3 of 20 games undecided (above 10%).
```
(The numbers above illustrate the layout only.) The coordinator appends `report.md` to the CI job summary.

**CI** (request to the coordinator, `.github/workflows/ci.yml`, new job `balance-batch` after `build-and-test`): `--batch --ci --jobs 2 --report-only --out rts/build/harness`, fail on exit 1 or 3, upload `report.md`, `results.jsonl` and `hq_floor.jsonl` as artifact `balance-report`, append the report to the summary. Drop `--report-only` once the developer has tuned the HQ floor. Locally the full run is `--batch` (500 per cell). Budget: the CI job should finish in 20 minutes; if it does not, lower `ci_games_per_matchup_per_map` in data, not the code.

## 6. Interfaces

- New files in `rts/tools/BalanceHarness/`: `BatchOptions.cs` (parsing; `HarnessOptions` routes `--batch` / `--worker` to it), `SeedDerivation.cs`, `GameRunner.cs` (one game, returns a `GameRecord`; also used in-process by tests), `HqFloorTrial.cs`, `WorkerPool.cs` (process start, stdout capture, ordering), `HarnessStats.cs`, `ReportWriter.cs`, `GameRecord.cs` (record plus JSON line writer and reader through `Rts.Sim.Data.JsonReader`).
- `StandardMatchStart` in `rts/src/Sim/Data/` (pure, review pass) and the `starting_worker_count` key in `EconomyTuning`.
- Reads only public APIs: `DataLoader`, `Simulation`, `MatchOutcome`, events, `AiHost`, `SkirmishAiController`, `EconomyRules.SupplyCost`.
- Must NOT: change simulation code paths for the harness, use `double` outside `HarnessStats`/`ReportWriter`, print timestamps into stdout or files, or depend on process completion order.

## 7. Acceptance tests (`rts/tests/Sim.Tests/Harness/`, the test project references the harness project)

1. **Seed derivation.** `FNV-1a("greybox_1v1/skai_vs_water") = 0xDA7141FC5CB80F77`; with base 20261026: game 0 seed `0x6CB3B0FB23ACA4E0`, game 1 `0x95F249730176B09A`, game 2 `0xA4D5B07EAD235131`, game 499 `0x80243880BB17C8C3`. `"greybox_1v1/skai_vs_skai"`: FNV `0x27E9E0F647F98A9C`, game 0 `0x542BF6F4AD48990F`. FNV of `""` is `0xCBF29CE484222325`, of `"a"` `0xAF63DC4C8601EC8C`.
2. **Side swap.** Matchup `skai_vs_water`: game 0 slot 0 skai, game 1 slot 0 water; a slot-1 win in game 1 counts for skai.
3. **Matchup IDs.** `water_vs_skai` in data or `--matchups` is an error suggesting `skai_vs_water`; `neutral_vs_skai` is an error (no `hq_unit`); unknown map in `--maps` is an error; all exit 1.
4. **Wilson.** z 1.96: 10 of 20 gives 50.0% [29.9, 70.1]; 250 of 500 gives [45.6, 54.4]; 275 of 500 gives 55.0% [50.6, 59.3] (outside band: no, edge inclusive; significant: no); 0 of 20 gives [0.0, 16.1]; 9 wins, 2 draws, 9 losses gives p 0.5.
5. **Percentiles.** Lengths in ticks `[100, 200, 300, 400]` at 20 tps: median 10.0 s (element 1), p10 5.0 s, p90 15.0 s; one value: all three equal it; zero decided games: the row prints `-`.
6. **Standard match start.** `greybox_1v1`, skai slot 0, water slot 1: 10 commands, sequences 0 to 9; HQs at (8, 8) and (56, 56); worker tiles are on ring 1 at height 2, not ramps or fields, in ascending index; both HQs start Rooted.
7. **One game is deterministic.** `GameRunner.Play` for `greybox_1v1`, `skai_vs_water`, game 0, `max_game_seconds` 300, twice in-process: equal records; the same game through a worker process gives the same JSON line.
8. **Order independence.** `--batch --games 4 --maps greybox_1v1 --matchups skai_vs_water --max-game-seconds 120 --verify 1` with `--jobs 1` and `--jobs 3`: byte-identical `report.md`, `results.jsonl`, `hq_floor.jsonl` and stdout.
9. **HQ floor arithmetic.** Test data: HQ 1000 health, armour multiplier 1, weapon damage 0.01; attacker supply 3, weapon damage 10, cooldown 1 s, placed in reach (standoff set so all fire on their first tick): 10 attackers, `ttk_ticks` 180 (ten volleys on t0 .. t0 + 180), 9.0 s, hard limit FAIL, exit 2; with HQ health 2500: 25 volleys, `ttk_ticks` 480, 24.0 s, PASS, exit 0; `--report-only` with the failing data exits 0 and still prints FAIL.
10. **Survived trial.** An attacker that cannot hurt the HQ (damage table 0): `ttk_ticks` -1, reported `> 300 s`, passes.
11. **Deferred limit.** Shipped data (no earth): the median cap row reads DEFERRED and does not affect the exit code; test data with factions `earth` and `water` and a matchup `earth_vs_water` whose median exceeds the cap: FAIL, exit 2.
12. **Determinism failure.** A worker started with a hidden test switch that perturbs one game's seed on re-run: report lists one mismatch, `HARNESS RESULT=DETERMINISM_FAIL`, exit 3.
13. **Worker failure.** A worker that exits 1: batch exit 1 with the worker's stderr text in the message.
14. **Floor consistency.** `hq.json survivability_floor_seconds` 25 with `harness.json hq_floor_seconds` 20: exit 1 with the message from section 2.
15. **Single-run mode unchanged.** `ai_vs_ai.json` through the old command line prints the same lines as before this change (apart from the hash change requested by planetary_cycles.md).

## 8. Open questions and follow-ups

- Should the Uprooted HQ also meet the floor? Chosen: reported only; the design says an Uprooted HQ is weaker on purpose.
- "Strongest HQ-killing army" is approximated by pure 30-supply armies of each unit type; mixed armies (the AI composition, or a search) are a later refinement.
- Whether the HQ should fight back in the floor trial (chosen: yes, as in play) or stand passive (a stricter floor).
- Several AI profiles per batch, and asymmetric profiles, once difficulty levels exist.
- With placeholder unit data the HQ floor is likely to fail at first (30 Windrunners deal about 112 damage per second to a Rooted 1500-health HQ, about 13 s); that is a tuning finding, not a harness bug.

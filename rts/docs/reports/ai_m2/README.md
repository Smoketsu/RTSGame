<!-- rts/docs/reports/ai_m2/README.md -->
# P5 measurement, second run: the skirmish AI uses its mechanics after the rule WL fixes (candidate `aggressive_s5_m`)

Same measurement as `rts/docs/reports/ai_m/README.md` (spec `rts/docs/specs/ai_mechanic_use.md` section 7.2, revision 3): candidate `aggressive_s5_m` plays both seats, map `prototype_1v1`, three matchups, 72 games each (216 games, 432 slot-games), defaults of `ai_eval.json` (base seed 20261121), `--jobs 4`, same commands. Built from the Release build of branch `review/ai-mech-fix` (commit `a5790c9`: two changes in rule WL, below). Nothing was tuned: no threshold, bar, profile value, weight, data file (`ai/walls/prototype_1v1.json` included) or measurement command changed.

## What changed since `ai_m`

1. A gate is no longer ordered while a ground unit the AI can see stands on its tile (`AiBrainWalls.OrderGate`). Cause of the `skai.wind_gate` `tile_occupied` refusal in `skai_vs_skai`: the order went onto a tile with a Windrunner of the AI's own on it (visible); the simulation refuses a build onto a tile with a ground unit, and `TryBuild` with a given site never looked at the occupancy the view already carries.
2. A `build_wall_line` the simulation refuses whole as `tile_blocked` or `on_resource_field` is now remembered (`SkirmishAiController.ReadEvents`: the first tile of the order goes into `WallTilesRefused`). Cause of the four `tile_blocked` refusals in `water_vs_water`: the one tile `(7,66)` carried an enemy `water.coral_wall` (player 0's) in fog (not visible to player 1, blocker `BuiltWall`); the view hides enemy structures in fog, as it must. A refusal where every tile of the order is filtered arrives as a `CommandRejectedEvent`, not as the per-tile skipped event the controller already read, so the tile was never remembered and the line was re-sent on four thinks.

## Bars

| # | Requirement (spec 7.2) | Measured | Verdict |
| --- | --- | --- | --- |
| 1 | Each of 11 items has a use share of at least 0.100 | `water/wave` 0.773 (167/216), `water.tidecaller` 0.977 (211/216), `skai/wall` 0.782 (169/216), `water/wall` 0.801 (173/216), `skai/gate` 0.449 (97/216), `water/gate` 0.537 (116/216), `skai.wind_baffle` 0.782, `water.coral_wall` 0.801, `skai.wind_gate` 0.449, `water.coral_gate` 0.537, `skai/downdraft` 0.868 (125/144) | PASS |
| 2 | Every item at or above 0.100 in `reports/ai_r/balance/balance.md` is still at or above 0.100 | None regressed (63 item rows compared). `skai.stormcaller` 0.824. `balance_use` fails only the two `hq_last_stand` rows (2 of 57) | PASS |
| 3 | In each traced game no AI `cast_wave`, `cast_grounding` or `build_wall_line` is rejected, and no AI gate `build_structure` is rejected other than `site_occupied` | `skai_vs_skai`: no rejection (the gate refusal is gone). `skai_vs_water`: none. `water_vs_water` game 0, P1: `build_wall_line` rejected once with `tile_blocked` at tick 12000 (was four times, ticks 11999 to 12029); no cast rejected | FAIL |

Bar 3 detail. The one remaining refusal is the first learning of an enemy wall the AI cannot see: tile `(7,66)` is out of sight of player 1 and the wall on it is the opponent's; an AI that reads only what its player sees cannot know it. Peeking was not added. After the refusal the tile is not ordered again (the repeats of `ai_m` are gone). By the letter of the bar this is a miss; whether a refusal caused by something the player legitimately cannot see should count is the developer's call. The only other `tile_blocked` in the traces is a `water.pearl_depot` of player 0 at tick 13381 (a depot site on a tile with an enemy footprint in fog), as in `ai_m`; it is neither a wall line nor a gate.

HQ Last Stand is reported, not judged: `skai/hq_last_stand` 0 of 216 and `water/hq_last_stand` 0 of 216.

## Reported, not judged

- **Wall style drawn** (432 slot-games, from a rerun of the same 216 games with temporary logging, since reverted; its `balance.jsonl` is byte-identical to the committed one; the logging also saw 4 extra replay-verification games, taken out using the 4 records a first logging attempt had captured): Sealed 213 (0.493), Funnel 129 (0.299), None 90 (0.208). Wave aim drawn: Melee 237 (0.549), All 195 (0.451). Identical to `ai_m`.
- **Complete rings.** 208 slot-games stood every tile of at least one authored area at one tick (all 208 had drawn Sealed; 0.977 of the 213). By area: `basin_0` 95, `basin_1` 88, `natural_0` 23, `natural_1` 16, `flank_w` 12, `corner_se` 10, `corner_nw` 10, `flank_e` 4. Of the 258 ring events, 222 are in the AI's own start area family and 36 in neutral areas. No slot-game completed `main_0` or `main_1` (see below).
- **AI command rejections by reason** (three traced games, six players): `tile_blocked` 2 (`build_wall_line` once, `water.pearl_depot` once). `ai_m`: `tile_blocked` 5, `tile_occupied` 1.
- **Wall spend**: lower median 700 common per slot-game; 90 slot-games spent none; 1 reached `wall_max_spend_common` 1200.
- **Length**: median end tick 11788 (`ai_m`: 11771); one game reached `max_game_seconds` (`water_vs_water` game 29, undecided at tick 47999, both `water_leviathan_tech`), as in `ai_m`.
- `balance_predictor` FAIL (accuracy 0.735, bar 0.850); `balance_openings` PASS; `balance_unspent` FAIL against the bars it printed (as in `ai_m`). Not part of this spec.
- **Faction matrix**: Skai beat Water in 72 of 72 games and Water beat Skai in 0 of 72 (win rates are not read as balance); mirrors: Skai seat 33/72, Water seat 31/71 (one undecided).

## Why no slot-game completes `main_0` or `main_1`

Not a code bug; it follows from the authored data and the rule as specified, and it is not fixed here. `natural_0` lists `covers: ["main_0"]`, and rule WL drops every kept area that another kept area covers (`AiWallLines.HeldAreas`, step 6). The AI takes its natural depot long before `wall_earliest_seconds` (180 s): in the traced `water_vs_water` game player 0 has its second depot at t=120 s and its first walls at t=210 s, so the main is covered before it is ever walled and the natural ring is built instead. The spec's own cost line says the same ("775 when the natural depot comes before `wall_earliest_seconds`: the main is then covered before its first wall"). The P4 exit game stood all 8 main tiles under its own set-up, not through this timing. Making slot-games complete the main ring needs a design choice (a different wall time, or the natural not covering the main), so none was made.

## Commands

Run from the repository root, after `dotnet build rts/tools/BalanceHarness -c Release`.

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5_m --maps prototype_1v1 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4 --out rts/docs/reports/ai_m2/balance
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --trace --map prototype_1v1 --matchup <skai_vs_skai|skai_vs_water|water_vs_water> --profile-a aggressive_s5_m --profile-b aggressive_s5_m --out rts/docs/reports/ai_m2/trace_<matchup>.txt
```

Wall-clock: balance report 4852 s, exit code 0. Full test suite before the run: 1154 (Sim) + 33 (Net) + 30 (Replay) tests, all passed, every pin held (two new tests: `Wl10b_WholeLineRefusalIsRemembered` and `Wl10c_GateWaitsForAnOccupiedTile`, each fails without its fix). The rejection kinds and tiles in bar 3 were read by printing the rejected command and the tile state during a rerun of the traces (the trace text is unchanged by that printing).

Files: `balance/balance.md` and `balance/balance.jsonl`, and the three trace files.

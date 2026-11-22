<!-- rts/docs/reports/ai_m/README.md -->
# P5 measurement: the skirmish AI uses its mechanics (candidate `aggressive_s5_m`)

Spec: `rts/docs/specs/ai_mechanic_use.md` section 7.2 (revision 3) and the harness change of 7.3. Candidate `aggressive_s5_m` (all four switches true) plays both seats. Map `prototype_1v1` only, three matchups, 72 games each (216 games, 432 slot-games), the defaults of `ai_eval.json` (base seed 20261121). No ladder and no win-rate bar. Built from the Release build of the `review/ai-mech-p5` branch on a 4-core container, after P1 to P4 and P2b were merged and the 7.3 change (commit 1 of this branch) was in. Nothing was tuned after a result: no threshold, bar, profile value, weight, data file or AI rule was changed.

## Bars

| # | Requirement (spec 7.2) | Measured | Verdict |
| --- | --- | --- | --- |
| 1 | Each of 11 items has a use share of at least 0.100 | `water/wave` 0.778 (168/216), `water.tidecaller` 0.977 (211/216), `skai/wall` 0.782 (169/216), `water/wall` 0.801 (173/216), `skai/gate` 0.449 (97/216), `water/gate` 0.537 (116/216), `skai.wind_baffle` 0.782, `water.coral_wall` 0.801, `skai.wind_gate` 0.449, `water.coral_gate` 0.537, `skai/downdraft` over Skai slot-games whose opponent fielded a flyer 0.868 (125/144; the 144 are the 72 Skai-against-Skai games, both seats) | PASS |
| 2 | Every item at or above 0.100 in `reports/ai_r/balance/balance.md` is still at or above 0.100 (includes `skai.stormcaller`) | None of the 44 regressed. `skai.stormcaller` 0.819 (it was 0.250). `balance_use` now fails only the two `hq_last_stand` rows (2 of 57 items; 13 of 57 in `ai_r`) | PASS |
| 3 | In each traced game no AI `cast_wave`, `cast_grounding` or `build_wall_line` is rejected, and no AI gate `build_structure` is rejected other than `site_occupied` | `water_vs_water` game 0, P1: `build_wall_line` rejected 4 times with `tile_blocked` at ticks 11999, 12009, 12019 and 12029. `skai_vs_skai` game 0, P1: `build_structure` of `skai.wind_gate` rejected once with `tile_occupied` at tick 12219. `skai_vs_water`: no rejection. No cast was rejected | FAIL |

Bar 3 detail. The four wall-line rejections come on four consecutive thinks (10 ticks apart) in the last minute of a long game, so the AI re-sends a line whose tile is blocked and learns one refusal at a time; this was not verified in the code path. The gate rejection is `tile_occupied` (a unit stood on the tile at command time). The spec names `site_occupied`, which is the cancel reason of an accepted build order and never a rejection reason, so the spec's exception reads as meant for `tile_occupied`. It is counted here as written, which is a miss; with the exception read as `tile_occupied` it would not be, and the wall-line rejections would still fail the bar. This is the developer's call.

HQ Last Stand is reported, not judged: `skai/hq_last_stand` 0 of 216 and `water/hq_last_stand` 0 of 216.

## Reported, not judged

- **Wall style drawn** (432 slot-games, from a rerun of the same 216 games with temporary logging of the draw, since reverted; its `balance.jsonl` is byte-identical to the committed one): Sealed 213 (0.493), Funnel 129 (0.299), None 90 (0.208). Wave aim drawn: Melee 237 (0.549), All 195 (0.451).
- **Complete rings.** 207 slot-games stood every tile of at least one authored area at one tick (all 207 had drawn Sealed; 213 slot-games drew Sealed, so 0.972 of them). By area: `basin_0` 95, `basin_1` 87, `natural_0` 19, `natural_1` 13, `corner_se` 10, `flank_w` 10, `corner_nw` 9, `flank_e` 4. Areas owned by slot 0: 114, by slot 1: 100, neutral: 33. No slot-game completed `main_0` or `main_1`.
- **AI command rejections by reason**: only the traces record them (the balance records do not). Three traced games, six players: `tile_blocked` 5 (4 of them `build_wall_line`, 1 `build_structure` of `water.pearl_depot` in `water_vs_water` at tick 13380), `tile_occupied` 1 (the wind gate above).
- **Wall spend**: lower median 700 common per slot-game; 90 slot-games spent none (the 90 that drew None); 1 reached `wall_max_spend_common` 1200.
- **Length**: median end tick 11771 (`ai_r`: 11108); one game reached `max_game_seconds` (`water_vs_water` game 29, undecided at tick 47999, both `water_leviathan_tech`); `ai_r` had none.
- `balance_predictor` FAIL (accuracy 0.735, bar 0.850); `balance_openings` PASS; `balance_unspent` FAIL against the bars it printed (common 400, rare 150): rare over at skai 300 s (205), 600 s (265) and 900 s (170) and water 600 s (285) and 900 s (990); common over only at water 300 s (640). These bars were not part of this spec.
- Not computed: the per-area-kind trace shares, the first-use medians of the bar-1 items, and the Stormcaller split with and without an enemy flyer. The harness does not record them and no code was added for them.

Odd: Skai beat Water in 72 of 72 games (the faction matrix; win rates are not read as balance). The one undecided game is a long Water mirror.

## Commands

Run from the repository root, after `dotnet build rts/tools/BalanceHarness -c Release`.

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5_m --maps prototype_1v1 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --jobs 4 --out rts/docs/reports/ai_m/balance
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --trace --map prototype_1v1 --matchup <skai_vs_skai|skai_vs_water|water_vs_water> --profile-a aggressive_s5_m --profile-b aggressive_s5_m --out rts/docs/reports/ai_m/trace_<matchup>.txt
```

Wall-clock: balance report 3312 s (55 min 12 s), exit code 0; traces 25 s, 30 s and 89 s. The full test suite before the run: 1152 (Sim) + 33 (Net) + 30 (Replay) tests, all passed, every pin held.

Files: `balance/balance.md` and `balance/balance.jsonl`, and the three trace files. The rejection kinds in bar 3 were read by printing the rejected command's type during a rerun of the three traces (the trace text itself is byte-identical).

<!-- rts/docs/reports/balance_first/README.md -->
# First balance report (package M.5): `aggressive_s5` self-play

Spec: `rts/docs/specs/balance_measurement.md` M.5 and M.6. Settings: `rts/data/scripts/ai_eval/balance_report.json` as shipped (every bar a placeholder). Code: `main` at `a95467e` (AI-H off, before the fight simulation was merged; `aggressive_s5` plays the same after it). Nothing was tuned.

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --jobs 4 --fights-from rts/docs/reports/fights_2026-11-18/fights.jsonl --out rts/docs/reports/balance_first
```

216 games (72 per matchup on `prototype_1v1`), 58 min wall at 4 jobs on a shared machine. Full tables: `balance.md`; records: `balance.jsonl`.

```
CHECK balance_use FAIL 13 of 53 items below 0.100: skai/skai.wind_baffle 0.000, skai/skai.wind_gate 0.000, water/water.coral_gate 0.000, water/water.coral_wall 0.000, water/water.tidecaller 0.000, skai/downdraft 0.000, water/wave 0.000, skai/wall 0.000, water/wall 0.000, skai/gate 0.000, water/gate 0.000, skai/hq_last_stand 0.000, water/hq_last_stand 0.000
CHECK balance_predictor FAIL accuracy=0.735 correct=562 decided=765 bar=0.850
CHECK balance_unspent FAIL over bars common=400 rare=150: skai@300s common=385 rare=205, skai@600s common=125 rare=285, water@300s common=230 rare=220, water@600s common=50 rare=475, water@900s common=75 rare=1020
CHECK balance_openings PASS openings judged=8 all within 0.100 to 0.900 of distinct mirror games (at least 10)
BALANCE profile=aggressive_s5 games=216 decided=216 distinct=216 checks_passed=1 of=4
```

## What it says

- **Factions:** Skai wins 72 of 72 cross-faction games. Mirrors are fair by seat (Skai 35 of 72 in slot 0, Water 30 of 72). Every game is decided and distinct.
- **Never used (the AI has no rule for them):** walls and gates (both factions), the Tidecaller and its wave, Downdraft, the HQ last stand. Used in a minority of games: Stormcaller 0.347, drench 0.319, anti-air on water 0.250, cycle pick 0.218 (Skai) and 0.440 (Water).
- **Unspent rare:** the median rare bank grows through the game (Water 220 at 300 s, 475 at 600 s, 1020 at 900 s; Skai 205, 285). Common is spent (at most 385).
- **Openings (mirror games against other openings):** `skai_fast_expand` 26 of 32, `water_leviathan_tech` 18 of 24, `skai_air` 4 of 24, `skai_windrunner_pressure` 5 of 17; all inside the report's wide 0.10 to 0.90 band, several outside the older 0.35 to 0.65 band.
- **Predictor:** the profile uses the old formula (0.735 on the development fights).

## Reading it

The AI is not yet a valid source of balance numbers: three of the four trust checks fail. The cross-faction result cannot be read as "Water is weak" while the Water AI never trains the Tidecaller, never floods on purpose and banks its rare resource.

## Not checked

- Game length and time of first attack per matchup are in `balance.jsonl` but not summarised here.
- The use detectors (`MechanicDetectors`) were tested on short games only; a zero share was not cross-checked against a trace.

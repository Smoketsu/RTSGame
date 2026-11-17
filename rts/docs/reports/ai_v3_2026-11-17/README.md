<!-- rts/docs/reports/ai_v3_2026-11-17/README.md: baseline runs of the skirmish AI v3 package A harness modes (ladder and response) on the unchanged v2 AI, 17 Nov 2026. Part of rts/docs/reports; spec rts/docs/specs/skirmish_ai_v3.md A.1, A.4, A.5 (A-10). -->
# Skirmish AI v3, package A: baseline runs (17 Nov 2026)

Both runs use the shipped data after the Reef Runner was added and the v2 AI (profile `aggressive`, every v3 switch absent). Built in Release; 4 worker processes.

## Ladder: `aggressive` against `aggressive` (the full 288 games)

```
dotnet run -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive --profile-b aggressive --out <dir>
```

- Files: `ladder_aggressive_vs_aggressive/ladder.md` (per cell, map, seat, overall), `ladder.jsonl` (one record per game), `stdout.txt`.
- Result line: `LADDER candidate=aggressive baseline=aggressive decided=288 wins=135 fraction=0.469 bar=0.650 FAIL` (exit 2, bar 0.65 not met, as expected for identical profiles).
- All 288 games were decided (no draws, none undecided). Wall time 27 min. Determinism spot check: 2 games re-run in-process, 0 mismatches.
- Candidate share of decided games 46.9 percent, Wilson 95 percent interval [41.2, 52.6], so 50 percent is inside it. Per map: greybox_1v1 44.4, long_lowland 47.2, prototype_1v1 41.7, twin_chokes 54.2 percent.
- Seat effect worth knowing for the later gates: slot 0 won 165 of 288 games (57 percent) between identical profiles (candidate in slot 0: 78 of 144; candidate in slot 1: 57 of 144). The seat rule balances it, but it widens the noise of any ladder.

## Response tests: `--response --profile aggressive`

`response_aggressive.txt`. 0 of 10 pass; expected, since the AI has no scouting, enemy model, counter-mix or static defence yet. Per scenario (p1, p2 are the numbered parts of the check in the spec's A.4 table):

| Scenario | Result | What was observed |
| --- | --- | --- |
| roc_rush_skai | FAIL | p1 fails (no enemy model, "model_air_known_s=none"); p2 passes (8 anti-air units at 270 s: Skai's own units hit air); p3 passes (HQ alive at 600 s) |
| roc_rush_water | FAIL | p1 fails (no model); p2 fails (0 anti-air units at 270 s); p3 passes |
| mass_pummeler_skai | FAIL | answer share 0.000 against base share 0.375 (gain -0.375, needs +0.15); the HQ was dead before 600 s |
| mass_pummeler_water | FAIL | answer share 0.000 against base share 0.182 (gain -0.182); the HQ was dead before 600 s |
| mass_windrunner_skai | FAIL | trade ratio 0.66 (31 supply killed, 47 lost); HQ dead before 600 s |
| mass_windrunner_water | FAIL | trade ratio 0.87 (80 killed, 92 lost); HQ alive |
| worker_raid_skai | FAIL | p1 fails (no static defence near the raided base); p2 passes (1 worker lost per wave, limit 3) |
| worker_raid_water | FAIL | p1 fails (no static defence); p2 passes (3 workers lost in the worst wave, at the limit) |
| turtle_skai | FAIL | 97 army supply lost before any enemy structure fell (limit 16); no enemy structure destroyed and no win by 2400 s |
| turtle_water | FAIL | 83 supply lost before the first structure kill (498.4 s; limit 16); p2 passes (won at 498.4 s) |

Notes: the air_answer part 1 reads the enemy model, which arrives with package B; until then `ResponseSeries.ModelAirKnownTick` stays -1 and the part fails by construction. The runs are deterministic: running the command twice gave identical lines.

## Package C: `aggressive_s2` (counter-mix, tech on demand, static defence, rare priority)

- Ladder against the v2 `aggressive` (full 288 games, Release, 4 workers, 11 min): `ladder_aggressive_vs_aggressive_s2/` (`ladder.md`, `ladder.jsonl`, `stdout.txt`).
  `LADDER candidate=aggressive_s2 baseline=aggressive decided=288 wins=285 fraction=0.990 bar=0.650 PASS`. Cells below 100 percent: long_lowland water mirror 23 of 24, twin_chokes Skai versus water 22 of 24.
- Response tests, `response_aggressive_s2.txt`: 4 of 10 pass (both `air_answer`, `armoured_answer` Skai, `trade` water). Failing and why: see the builder report; short version: `armoured_answer` water (the Lanchester score prefers Graspers over Ink Lobbers, the check wants the highest damage multiplier), `trade` Skai 0.847, `raid` part 1 (the check measures the depot nearest the wave spawn, a later depot the defences near the HQ do not cover), `turtle` (package D).
- `traces/s2_vs_aggressive_greybox_skai_game0.txt`: one traced ladder game (aggressive in slot 0, `aggressive_s2` in slot 1, won at 312 s).
- The profile was tuned once from the spec's placeholders: `static_defence_after_raids` 2 to 1 (see the builder report). `counter_weight` 0.9 was tried on the response scenarios and reverted.

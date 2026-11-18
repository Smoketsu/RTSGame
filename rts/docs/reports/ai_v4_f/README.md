<!-- rts/docs/reports/ai_v4_f/README.md -->
# AI v4 package F: measured result (branch review/ai-v4-f)

Outcome: **package F does not pass its gate.** Code, profile `aggressive_s4f` and tests F-1 to F-11 are built and green; the gate in `skirmish_ai_v4.md` section E fails on three counts (below). Nothing was tuned toward the gate; scenarios and thresholds are unchanged. Runs use `-c Release`, seed 20261117 (ladder), one run per scenario (the scenarios are scripted and deterministic). The distinct-game counts of package S are not available on this branch (S is not built), so the ladder numbers are over all 216 games and the intervals are too narrow (spec S.0).

## Commands

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --response --profile aggressive_s4f
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --response --profile aggressive_s3
# knife edge and ablations: copy of rts/data under rts/build with one key changed, then
#   --data <copy> --response --profile aggressive_s4f --only <script>
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --ladder --profile-a aggressive_s3 --profile-b aggressive_s4f --non-regression --jobs 4 --out rts/build/ladder_f
```

## Response scenarios (before = `aggressive_s3`, after = `aggressive_s4f`)

| Scenario | before | after |
| --- | --- | --- |
| roc_rush_skai (air_answer) | PASS aa_units=19 | PASS aa_units=15 |
| roc_rush_water (air_answer) | PASS aa_units=5 | PASS aa_units=4 |
| mass_pummeler_skai (armoured_answer) | FAIL share_gain=-0.012 | FAIL share_gain=-0.024 |
| mass_pummeler_water (armoured_answer) | FAIL share_gain=-0.029 | FAIL share_gain=-0.017 |
| mass_windrunner_skai (trade) | PASS ratio 1.500 (36/24) | PASS ratio 2.333 (35/15) |
| mass_windrunner_water (trade) | PASS ratio 1.717 (79/46) | **FAIL ratio 0.923 (84/91)** (regression) |
| worker_raid_skai | FAIL tower=false, 7 workers lost | FAIL tower=false; workers lost 0 (part 2 now passes) |
| worker_raid_water | FAIL 5 workers lost (tower true) | **PASS**, 1 worker lost |
| turtle_skai | FAIL 18 supply | FAIL 18 supply (not targeted) |
| turtle_water | PASS | PASS |

Summary lines: `RESPONSE_SUMMARY passed=5 of=10` for both profiles (the water `armoured_answer` is not counted by the gate, as before).

## Knife edge (raid scenarios, one key per copy; skai / water)

| Copy | worker_raid_skai | worker_raid_water |
| --- | --- | --- |
| base (danger 10, defend_min_advantage 1.5) | FAIL (tower false; lost 0) | PASS (lost 1) |
| `defend_min_advantage` 1.2 | PASS (lost 2) | PASS (lost 2) |
| `defend_min_advantage` 1.8 | FAIL (tower false; lost 0) | PASS (lost 0) |
| `worker_danger_radius_tiles` 8 | FAIL (tower false; lost 1) | PASS (lost 2) |
| `worker_danger_radius_tiles` 12 | FAIL (tower false; lost 0) | PASS (lost 3) |

Water raid passes at all five values. Skai raid fails at the base and at three neighbours and passes only at 1.2, so it is not robust: the gate's knife-edge condition is not met and 1.2 must not be adopted from this evidence.

## Ablations (aggressive_s4f with one switch off)

| Switch off | raid_skai | raid_water | mass_windrunner_water |
| --- | --- | --- | --- |
| `use_worker_safety` | FAIL, lost 5, tower false | FAIL, lost 5 | PASS ratio 1.152 |
| `use_defend_hold` | FAIL, lost 0 | FAIL, tower false | FAIL 0.978 |
| `raid_worker_losses_exempt` 0 | same as base | PASS, lost 2 | FAIL 0.923 |
| depleted rejection and forget | same as base | same as base | FAIL 0.923 |

Reading: worker safety removes the worker losses (both raid part 2) and is also what breaks the water Windrunner trade (it takes fields out of use while raiders are near); hold is needed for the water tower; the exemption does nothing in these runs because safety leaves fewer than 2 worker losses, so Skai's tower never qualifies.

## Spec causes: verified or not

- Value cap blocks the Skai raid tower: **verified by experiment.** `aggressive_s3` with `static_defence_max_value_share` 0 passes part 1 (tower built; part 2 still fails, 6 workers lost). Not instrumented inside `WithinValueCap`.
- Exemption (F.3) cannot fix it once safety works: verified by the ablation above. F.3 and F.1 work against each other for Skai; a different trigger for the tower (the raid log alone, or raided-field value) is the open design question.
- Depleted-field bug (P-005): **not reproduced** in any match run; covered only by unit test F-7 (stale memory re-sent without feedback, not re-sent with it). Ablation `nodepl` changes no scenario result.

## Ladder (`--non-regression`, bar 0.45)

`LADDER candidate=aggressive_s4f baseline=aggressive_s3 decided=216 wins=64 fraction=0.296 bar=0.450 FAIL` (exit 2, 4174 s)

| Cell | candidate wins / decided |
| --- | --- |
| skai_vs_skai | 28 / 72 (0.389) |
| skai_vs_water | 36 / 72 (0.500) |
| water_vs_water | **0 / 72** (0.000) |

Seats: candidate in slot 0 wins 46/108, in slot 1 18/108. The water mirror loses every game; the cause was not investigated (worker safety is the first suspect from the mass_windrunner_water regression).

## Tests and pins

`dotnet test rts/RtsGame.sln`: Rts.Sim.Tests 814 passed, Rts.Net.Tests 33 passed, Rts.Replay.Tests 27 passed, 0 failed. Harness smoke `STATE_HASH=9c985e8fc052ce36`, economy_smoke `STATE_HASH=57e92601f6755ea6`; `V2ReferenceHash` and `BlindTraceHash` are asserted by the suite and unchanged.

## Spec corrections made

F-5 (the s3 comparison sends the talon with `AttackUnit` when the raiders are inside the focus radius) and F-7 (field emptying, no money, relocation cooldown) now match what the tests need.

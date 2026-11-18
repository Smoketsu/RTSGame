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

## Diagnosis of the water mirror 0/72 (18 Nov 2026)

Verdict: **a design trade-off of rule WS (F.1), not a bug. No code, data or test was changed.** Rule WS works as specified; the specified evacuation is what loses the mirror. Method: traces of `--trace --map prototype_1v1 --matchup water_vs_water --seed 20261117 --game N --profile-a aggressive_s3 --profile-b <candidate>`, ablations with scratch profiles (copy of `rts/data` under `rts/build/f2/data`), plus temporary stderr instrumentation of `MarkDangerousFields`, `EvacuateWorkers` and `Gather` (removed again). "Cand" below is the candidate; game g puts the candidate in slot 1 for g = 0, 1 and slot 0 for g = 2, 3 (ladder seat rule).

### Evidence

- **Not the hold, not the HQ-as-defender (b, c).** Ablations (win = candidate wins; `-` = not run). Hold on or off changes nothing here (identical end ticks with WS only and with hold off); WS off restores the wins:

  | Profile | g0 | g2 | g1 | g3 |
  | --- | --- | --- | --- | --- |
  | `aggressive_s4f` (all F) | loss | loss | loss | loss |
  | all F off (`f_none`) | loss | win | - | - |
  | `use_worker_safety` off only (`f_nows`) | win | win | loss | win |
  | hold only (WS off) | win | win | - | - |
  | no hold (WS on) | loss | loss | - | - |
  | WS only | loss | loss | - | - |

  The cause is rule WS alone. Games 0 and 1 (and 4/5, 6/7) are near-duplicates in a water mirror, so 4 games are 2 to 3 distinct games per seat.
- **Not an attack-decision effect.** The trace of game 0 shows the candidate's army attacking and retreating on the same cadence as s3 (attacks 8, retreats 7 versus 8 and 6 at 600 s). The difference is the economy: at t = 300 s both sides have 14 workers, 14 gathering, 0 idle, but income is 395 (candidate) against 675 (s3); at 450 s 345 against 840 with 8 against 16 workers. Gathered over the game: 7440 against 13735 common. With WS off the same seat gathers 18155 against 20880 (income 775 against 755 at 300 s).
- **Mechanism (a), confirmed from the instrumented run of game 0.** 64 field-marking events in 12519 ticks, all on the candidate. Triggers: one enemy unit 34 times (many a lone `reef_runner` scout), two units 12, three or more 13, a hit on a worker 3 times. Nothing marks the candidate's own flood or stale data, and the danger does expire (`worker_danger_memory_seconds` 10): each episode lasts 15 to 20 s. What costs the income is what an episode does. The candidate's natural has three fields (11, 19, 10) within about 5 tiles of each other, so one scout or one passing wave marks all three in turn (3639, 3659, 3669 ticks), and every worker (8 to 11 of 14) is ordered off. Their order is `NearestField(.., underCap=true)` and, when every near field is dangerous or full, the `false` mode: all of them are sent to the single home field 9 (assignment trace `8:3 9:11 10D:0 11D:0 19D:0` for 240 ticks, then `9:3 10:3 11:3 19:2` again). A field serves a few gatherers at once, so 11 workers on field 9 mine little, and they walk there and back. 127 evacuation orders for 14 workers in one game; the episodes repeat whenever any enemy unit, including every scout or army that rallies at its own natural 10 tiles from the field, comes near (12 episodes). s3 has no such stop and keeps mining.
- Slot 1 is the weaker seat in every comparison (candidate slot 0 won 46/108 before, slot 1 18/108), so a worsened economy loses there first.

### Same cause behind `mass_windrunner_water`

With Windrunners circling, the natural is marked on every think; the 0.923 trade ratio is the economy loss above. With WS off it passes (1.152).

### Options, measured on a few games (no option adopted)

All values: mirror candidate wins on games 0 to 3 (win count), and the response lines that changed. These are knife-edge results on 2 to 4 games and are not evidence of a pass.

| Option | Mirror g0 to g3 | `mass_windrunner_water` | `worker_raid_water` | `worker_raid_skai` | `RESPONSE_SUMMARY` |
| --- | --- | --- | --- | --- | --- |
| as built | 0/4 | FAIL 0.923 | PASS lost 1 | FAIL | 5 of 10 |
| WS off | 3/4 | PASS 1.152 | FAIL lost 5 | FAIL lost 5 | (see ablations) |
| A. evacuation never overflows (move only to a field below its cap, else keep mining) | 3/4 | PASS 1.103 | FAIL lost 4 | FAIL | 5 of 10 |
| B. overflow evacuation only while an own worker was hit within `damage_memory_seconds` (the raid has started) | 2/4 | PASS 1.042 | PASS lost 2 | FAIL tower false | 6 of 10 |
| C. only shorter memory or smaller radius (`worker_danger_memory_seconds` 3 / `worker_danger_radius_tiles` 6 / both) | 2/4 for each | memory 3 PASS 1.173; radius 6 + memory 3 FAIL 0.837 | memory 3 FAIL (tower false); both FAIL lost 4 | memory 3 FAIL lost 9; both PASS | 5 of 10 each |
| D. threat must weigh at least 2 supply (a lone scout is ignored unless it hits) | 0/4 | not run | not run | not run | - |

Reading: B is the smallest change that keeps the raid result and restores the Windrunner trade (it also keeps the response total at 6 of 10, and the water gate lines pass), but it adds a rule (a new trigger for overflow) and the mirror is still only 2/4 on four near-duplicate games, so it needs a full ladder before anyone calls it a fix. A fixes the mirror but gives back the water raid. C shows how fragile the raid scenarios are (they flip with the radius and the memory). D does nothing: the episodes are caused by armies of two or more units, not only scouts. Whether a lone scout should count as a threat at all is the open question; the evidence does not support a lone scout as the main cause.

### Not explained

- `worker_raid_skai` still fails in every option except C with radius 6 + memory 3 (PASS lost 1), which is the knife edge already seen with `defend_min_advantage`; the tower trigger is untouched (cap blocks it, F.3 and F.1 work against each other).
- Why slot 1 (with games 1 and 3 the same as 0 and 2 in a water mirror) loses more even with WS off (g1 loss). Not investigated.
- Distinct-game count of the ladder is not available without package S.

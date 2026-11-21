<!-- rts/docs/reports/predict_v2/README.md -->
# Fight predictor v2 on the held-out fights (package FP2-3)

Spec: `rts/docs/specs/fight_predictor_v2.md` sections 6 and 8. Report only: nothing was retuned before or after this run. Predictor code: commit `8b0bdeb` on `main` (`FightSim` behind `predictor_model`), candidate profile `aggressive_s5_fp2`, baseline `aggressive_s5` (the old formula). The held-out settings `rts/data/scripts/fights/heldout.json` and every bar were committed (`c3efa82`) before the simulation code existed.

## Commands

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --fights --fight-settings rts/data/scripts/fights/heldout.json --jobs 4 --out rts/docs/reports/predict_v2/heldout
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --predict-fights --profile aggressive_s5_fp2 --baseline-profile aggressive_s5 --from rts/docs/reports/predict_v2/heldout/fights.jsonl --fight-settings rts/data/scripts/fights/heldout.json --out rts/docs/reports/predict_v2
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --predict-fights --profile aggressive_s5_fp2 --baseline-profile aggressive_s5 --from rts/docs/reports/fights_2026-11-18/fights.jsonl --out rts/docs/reports/predict_v2/devset
```

The held-out sweep was run once (110 s wall at 4 jobs).

## Output as printed

```
FIGHTS fights=1904 decided=1869 draws=0 stalemates=34 unresolved=1 mismatched=271 checks_passed=2 of=8 dome_absorbed=34
PREDICT fights=1904 judged=1633 decided=1603 correct=1293 accuracy=0.807 clean=47/60 no_winner_agree=15/30 decision=SIMULATE (proposed thresholds) bars=5/7 verdict=FAIL
predict time profile aggressive_s5_fp2: mean 240 us, largest 7363 us over 3786 calls
predict time profile aggressive_s5: mean 6 us, largest 1704 us over 3786 calls
```

Development set, for information only (no bar): `accuracy=0.841` (643 of 765), mean 686 us per call.

The 271 mismatched fights (the two armies could not be bought to the target values) are in no table, as the spec says. The `decision=` text is the old C.7 rule printed for the candidate and is not the verdict of this package; the bars are.

## Bars

| ID | Bar | Measured | Result |
| --- | --- | --- | --- |
| B1 | accuracy on equal-value fights at least 0.80 and at least the formula's + 0.05 | 0.783 (878 of 1121); formula 0.708 (794 of 1121) | FAIL (misses 0.80 by 0.017; the + 0.05 half holds, + 0.075) |
| B2 | no margin bin wins more than 0.10 less often than a lower bin | no inversion (0.590, 0.656, 0.722, 0.798, 0.937, 0.973) | PASS |
| B3 | predicted winner wins at least 0.70 of fights with margin 1.3 to 1.5 | 0.752 (124 of 165) | PASS |
| B4 | at least 0.50 of no-winner fights predicted none; at most 0.02 of decided fights predicted none | 15 of 30; 27 of 1603 (0.017) | PASS |
| B5 | every single-unit pair with 8 or more decided fights at least 0.40 | 52 pairs judged, 5 below | FAIL |
| B6 | damaged armies at least 0.75 and not below the formula | 0.856 (1371 of 1602); formula 0.844 | PASS |
| B7 | uneven armies not below the formula | 0.861 (415 of 482); formula 0.807 | PASS |
| C1 | mean time per call at most 1000 microseconds | 240 | within the bar (largest single call 7.4 ms) |

Gate: FAIL (5 of 7). By section 6 rule 3 nothing is retuned and the decision goes to the developer.

## Where it is wrong

- **B5, pairs below 0.40 with 8 or more decided fights** (equal-value fights; the formula's accuracy in brackets): Windrunner against Reef Runner 0 of 10 (0 of 10), Windrunner against Ink Lobber 1 of 10 (1 of 10), Ink Lobber against Reef Runner 1 of 10 (1 of 10), air mix against pressure mix (Skai) 2 of 10 (2 of 10), Thunderhead against Shellback 2 of 9 (7 of 9), Grasper against Ink Lobber 3 of 10 (3 of 10). All but the Thunderhead row are fights the formula gets equally wrong, so they point at something neither model has (kiting by a faster ranged unit, the Ink Lobber's splash against clumps, the Reef Runner on water), not at a regression. Thunderhead against Shellback is a regression (dome handling against a splash flyer).
- **Small rows** (fewer than 8 decided fights, not judged): Skai pressure mix against Water pressure mix 0 of 6 where the formula has 5 of 6.
- **By terrain:** choke 0.909, uneven flat 0.861, flat 0.819, flooded 0.737, high ground 0.690. High ground and flooding are still the weak terrains, as on the development set.
- **Close calls:** a predicted margin below 1.1 wins 0.590 and 1.1 to 1.25 wins 0.656; the old formula's inversion (0.365 on the development set) is gone.

## Mechanics the simulation leaves out (spec 3.4)

Choke width and ramps (subset: choke and high-ground cases), formation depth (large armies, mixes), hit delay (all), Windstorm range and the AI's own micro such as kiting (the Windrunner rows). In real games the simulation is always told the ground is dry (Q-2).

## Not checked

- Why the Windrunner and Ink Lobber pairs are wrong in both models; no fight was traced.
- Cost inside a real game per AI think (many calls per think); C1 is per call on these fights only.
- The paired ladder and the attack ledger (FP2-4) are not built or run: the gate did not pass.

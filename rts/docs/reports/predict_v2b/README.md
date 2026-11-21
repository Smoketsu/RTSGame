<!-- rts/docs/reports/predict_v2b/README.md -->
# Fight predictor v2 after one diagnosis-and-fix round (second held-out set)

Spec: `rts/docs/specs/fight_predictor_v2.md` section 6 rule 3 and Q-7. The first held-out run (`rts/docs/reports/predict_v2/README.md`) failed 5 of 7 bars. This report is the one fix round that rule allows: the failing rows were traced against the game's records, only ports of game rules were fixed, no `predictor_sim_*` value and no bar changed, and because `FightSim` changed after those results were seen, a new held-out set was made and run once. There is no second fix round, whatever the result below.

Branch `review/p006-fp2b`. Order of commits: fixes and `heldout2.json` first (`FightSim.cs` fixes, `rts/data/scripts/fights/heldout2.json`), then the sweep and the scoring. Candidate `aggressive_s5_fp2`, baseline `aggressive_s5` (the old formula).

## The new set, and why these values

`heldout2.json` is `heldout.json` with a new `base_seed` (20261122) and new army values: flat 1250 and 2750, high ground 1750, flooded 2250, choke 1250, uneven flat 1750 (ratios 1.25 and 1.5 as before); `rule_army_value` 1250. The sweep played 750 and 1500, the first held-out set 1000, 2000, 2500 and 3000; the new values lie between them, were chosen before anything was run, and a test asserts that none was used before (`HeldOut2Settings_ParseAndPlan_NewSeedAndValues`). The arena accepted every army without change (1904 fights planned).

## Commands

```
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --fights --fight-settings rts/data/scripts/fights/heldout2.json --jobs 4 --out rts/docs/reports/predict_v2b/heldout
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --predict-fights --profile aggressive_s5_fp2 --baseline-profile aggressive_s5 --from rts/docs/reports/predict_v2b/heldout/fights.jsonl --fight-settings rts/data/scripts/fights/heldout2.json --out rts/docs/reports/predict_v2b
dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --predict-fights --profile aggressive_s5_fp2 --baseline-profile aggressive_s5 --from rts/docs/reports/fights_2026-11-18/fights.jsonl --out rts/docs/reports/predict_v2b/devset
```

The sweep was run once (97 s at 4 jobs).

## Output as printed

```
FIGHTS fights=1904 decided=1873 draws=0 stalemates=31 unresolved=0 mismatched=283 checks_passed=2 of=8 dome_absorbed=31
PREDICT fights=1904 judged=1621 decided=1595 correct=1353 accuracy=0.848 clean=60/72 no_winner_agree=15/26 decision=SIMULATE (proposed thresholds) bars=6/7 verdict=FAIL
predict time profile aggressive_s5_fp2: mean 176 us, largest 8100 us over 3789 calls
predict time profile aggressive_s5: mean 6 us, largest 1784 us over 3789 calls
```

Development set, for information only (no bar), before the fixes `accuracy=0.841` (643 of 765), after:

```
PREDICT fights=780 judged=780 decided=765 correct=652 accuracy=0.852 clean=22/24 no_winner_agree=6/15 decision=FIX_FORMULA (proposed thresholds)
predict time profile aggressive_s5_fp2: mean 530 us, largest 7274 us over 780 calls
```

The `decision=` text is the old C.7 rule printed for the candidate and is not the verdict of this package; the bars are. The 283 mismatched fights are in no table.

## Bars

| ID | Bar | Measured | Result |
| --- | --- | --- | --- |
| B1 | accuracy on equal-value fights at least 0.80 and at least the formula's + 0.05 | 0.819 (915 of 1117); formula 0.705 (788 of 1117) | PASS (first set: 0.783, FAIL) |
| B2 | no margin bin wins more than 0.10 less often than a lower bin | no inversion (0.528, 0.798, 0.801, 0.880, 0.928, 0.972) | PASS |
| B3 | predicted winner wins at least 0.70 of fights with margin 1.3 to 1.5 | 0.796 (113 of 142) | PASS |
| B4 | at least 0.50 of no-winner fights predicted none; at most 0.02 of decided fights predicted none | 15 of 26; 22 of 1595 (0.014) | PASS |
| B5 | every single-unit pair with 8 or more decided fights at least 0.40 | 54 pairs judged, 3 below | FAIL |
| B6 | damaged armies at least 0.75 and not below the formula | 0.870 (1387 of 1595); formula 0.846 | PASS |
| B7 | uneven armies not below the formula | 0.916 (438 of 478); formula 0.814 | PASS |
| C1 | mean time per call at most 1000 microseconds | 176 | within the bar (largest single call 8.1 ms) |

Gate: FAIL (6 of 7). B5, pairs below 0.40 (equal-value fights): Windrunner against Reef Runner 0 of 10 (formula 0 of 10), Thunderhead against Shellback 2 of 9 (formula 7 of 9), Stormcaller against Windrunner 3 of 10 (formula 8 of 10). No further round: the decision goes to the developer.

## Classification of the failing rows

(a) a game rule the simulation ported wrongly, (b) a mechanic the spec leaves out (3.4) or a limit of the one-point-per-side lane, (c) arena or seat artefact. Each row was traced by running the same fight in the simulation and in the game (the game sampled at several time caps through `--only` with `max_fight_seconds` edited, which changes nothing in the simulation) and comparing units alive and value left over time. Counts below are for the first held-out set unless stated.

| Row | First set | Trace | Class | Fixed? |
| --- | --- | --- | --- | --- |
| Windrunner v Reef Runner | 0 of 10; predicted `none`, 600 s both | The two units are near twins; the simulation kills one unit per volley on each side in lockstep and the last units die in the same step, which the spec's end rule calls no winner. The game never draws (0 draws in 1904 fights): its Windrunner wins narrowly through Momentum and spread fire. | (b) | No |
| Windrunner v Ink Lobber | 1 of 10 | The simulation had the Ink Lobber's blast hit 2 other units; in the game a blast reaches the blast radius plus the victim's radius (`CombatSystem.cs` line 619), about 6 units in a clump. The game's lobbers deal about 63 damage a shot (measured from the game's value left at 5 s), the simulation's 35. | (a), plus (b) focus | Yes (fix A) |
| Ink Lobber v Reef Runner | 1 of 10 | Same cause, same fix. | (a), plus (b) | Yes (fix A) |
| Grasper v Ink Lobber | 3 of 10 | Same cause, same fix. | (a), plus (b) | Yes (fix A) |
| Thunderhead v Shellback | 2 of 9 (formula 7 of 9) | The game's Thunderheads win slowly with no loss (shellbacks lose about 1.3 percent of their value a second from 10 s). In the lane every Shellback stands at one point, so all six domes cover every victim and regenerate 150 a second against 70 damage a second: a perfect stalemate. In the game's formation not every dome covers the victim and the cycle leaks. | (b) formation geometry | No |
| Skai pressure mix v Water pressure mix | 0 of 6 (formula 5 of 6) | The simulation ends in 31 s, the game in 13 s: the game kills 4 to 5 units a second, the simulation one a second per side, because all units of a side shoot the one lowest-index enemy and the surplus damage is lost. | (b) one-point lane (focus and overkill) | No |
| Decided fights predicted `none` (27) | 27 of 1603 | 17 are a flyer against Shellbacks (the dome stalemate above), 8 are Windrunner against Reef Runner (the lockstep draw above), 2 are Windrunner against Reef Runner on flooded ground (same draw). | (b) | No |

Not a row of the first report, found in this set: Stormcaller against Windrunner (3 of 10; formula 8 of 10). The seat-1 army wins 8 of the 10 fights whichever army it is (flat, choke, flooded), so the result follows the seat, not the units. Class (c), not fixed.

What the table says: the dominant remaining cause is the one-point lane itself. Every unit of a side stands at one place, so every shooter picks the same lowest-index target, damage is wasted on overkill, domes all cover everyone, and symmetric fights end in the same step. That is a stated simplification of the spec (3.4, formation depth), not a mistake in porting a rule, so it was not changed here. Fixing it would need a spec decision (for example spreading targets over the formation).

## What was fixed (class (a) only)

- **Fix A, blast disc** (`FightSim.cs`, `BlastCap`, used in `Blast`). The cap on units one blast reaches was floor(density x pi x r^2) - 1 with the blast radius r; the game's blast reaches the blast radius plus the victim's own radius (`rts/src/Sim/Combat/CombatSystem.cs` line 619), so the disc counted is that wide. Test `BlastCap_CountsTheWholeDisc` (and `Splash_KillsSoonerAndCapsAtTwelve`, now 19 units for radius 2).
- **Fix B, armed before unarmed** (`FightSim.cs`, `Nearest`, and the front index now keeps armed and unarmed units apart). The game ranks candidate targets armed (rank 0) before unarmed (rank 1) and only then by distance (`CombatSystem.cs` lines 338 to 340); the simulation took the nearest of either. None of the swept units is unarmed, so it does not change a fight in these sets. Test `Targets_ArmedBeforeUnarmed` (index and scan agree, the order of entries does not matter).
- The 4 earlier review fixes are unchanged. The front-index test still compares index and scan on the same armies.

No `predictor_sim_*` value, no bar, no unit or combat number and nothing in `rts/src/Sim` was changed, and nothing was fitted to a unit name or to these fights. The effect of the fixes is visible in the lobber rows (first set 1, 1 and 3 of 10, this set 6, 6 and 8 of 10 with different armies) but the fixes were chosen from the game's own rules, not from the size of that effect.

## Where it is still wrong

- B5 rows above. By terrain: choke 0.881, flat 0.867, uneven flat 0.916, flooded 0.754, high ground weakest as before.
- A predicted margin below 1.1 wins 0.528 (180 fights); 1.1 and above wins 0.80 or more.

## Proof the formula path is unchanged

Both smokes unchanged (`e88b3b972175571d`, `12259d6626becbc0`); `--predict-fights --profile aggressive_s5` on the committed sweep gives a `predict.jsonl` byte-identical to `reports/predict_fights/predict.jsonl`; the pinned game hashes of `FightSimTests.OldProfiles_FixedSeedGamesKeepTheirHash` pass in the full suite (totals in the report that accompanied this branch).

## Not checked

- Whether spreading targets over the formation (the spec decision named above) would fix the (b) rows; it was not tried.
- The Stormcaller and Thunderhead rows were traced on a single fight each, the others on the sampled times named in the table; no fight was traced tick by tick against the game's unit positions.
- The simulation is still told the ground is dry in real games (Q-2); cost inside a real game per AI think; the paired ladder and attack ledger (FP2-4) are not built or run.
- The first set's rows were used to find the fixes, so it is spent; this set was run once and its result is reported as it came out.

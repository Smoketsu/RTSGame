<!-- rts/docs/reports/predict_fights/README.md -->
# Fight predictor against measured fights (package C, first half)

Spec: `rts/docs/specs/balance_measurement.md` C.1 to C.9. Measurement only: `FightPredictor`, the fight sweep and its records are unchanged.

- Command: `dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --predict-fights --from rts/docs/reports/fights_2026-11-18/fights.jsonl --out rts/docs/reports/predict_fights`
- Files here: `predict.md` (all tables), `predict.jsonl` (one line per fight: prediction in integers, milli = thousandths).
- Predictor numbers: profile `aggressive_s5` (`predictor_profile` in `rts/data/scripts/fights/predict.json`); that profile's AI thresholds are attack_min_advantage 1.3 and retreat_max_advantage 0.7.
- Thresholds: the proposed placeholders of C.8, unchanged.

```
PREDICT fights=780 judged=780 decided=765 correct=562 accuracy=0.735 clean=0/0 no_winner_agree=0/15 decision=SIMULATE (proposed thresholds)
```

**Regenerated 24 Nov 2026 for rts/docs/specs/roster_numbers_1.md** (Reef Runner `max_health` 60 to 66: the predictor reads the new health, so `predict.jsonl` and `predict.md` changed from line 15; the fights are still the 18 Nov records). Same command; accuracy is still 0.735 (562 of 765); the first line below is the old output, the new one reads `PREDICT fights=780 judged=780 decided=765 correct=562 accuracy=0.735 clean=22/24 no_winner_agree=0/15 decision=SIMULATE (proposed thresholds)`. The tables below describe the numbers before this regeneration.

**Regenerated 27 Nov 2026 for rts/docs/specs/roster_numbers_2.md** (Spoutling `max_health` 70 to 90 and weapon damage 5 to 10: the predictor reads the new numbers, so `predict.jsonl` and `predict.md` changed; the fights are still the 18 Nov records). Same command; accuracy moves from 0.735 (562 of 765) to 0.681 (521 of 765), `clean=22/24` and `no_winner_agree=0/15` are unchanged, and the lowest judged mechanic is now drench at 0.483 (29/60). The tables further up describe the numbers before the 24 Nov regeneration.

## Was the committed sweep still valid for the current data?

The records carry everything the predictor needs (units, case, army value, result, value left, mismatch flag). The committed `fights.md` says data digest tuning `8af17d045a9c63e6`, content `9cb28f0929df518c`. The current data gives tuning `a020a25c7d7c2eae` (the only data change since is the gather-slot values added to `tuning/economy.json` on 18 Nov) and the same content digest `9cb28f0929df518c`. Because the tuning digest differs, `--fights` was rerun on the current data (28 s on this machine, not 15 minutes): all 780 records are identical to the committed ones in every field except `state_hash` (which includes tuning), and `hq_floor.jsonl` is byte-identical. The committed sweep is therefore valid and is what this report reads.

## Headline

- 780 fights, 0 mismatched, 765 decided, 15 with no winner (13 stalemates, 2 unresolved, 0 draws).
- Accuracy 562 of 765 decided fights = 0.735, Wilson 95 percent interval [70.2, 76.5] percent. The proposed bar for SIMULATE is 0.75, which lies inside the interval: the headline alone is a near miss, not a clear one.
- 9 decided fights were predicted `none` (counted wrong; seat 0 won 3 of them).

## How stalemates and unresolved fights were counted

They are not in the accuracy. A no-winner fight agrees when the predictor says `none` (equal times, which includes both sides at the 600 s cap). The predictor said `none` for none of the 15: `no_winner_agree=0/15`. Of the 15 (12 Shellback dome stalemates, 2 `time_cap` unresolved fights of the Shellback against the Roc Rider, 1 `no_damage` stalemate), the predicted winner is the side with more value left in 8, the other side in 6 and 1 is even (table in `predict.md`). So the predictor never sees "neither side can finish the other"; it sees the dome as extra health, never as a wall.

## Breakdowns (full tables in `predict.md`)

- **By terrain case:** flat 0.786 (239/304), choke 0.776 (118/152), flooded 0.677 (105/155), high_ground 0.649 (100/154). The two modelled-badly terrains are the lowest. The predictor models uphill but not flooding.
- **By army value:** 750 0.816 (124/152), 1500 0.715 (438/613). Value and case are confounded (the only 750 row is flat); flat at 1500 is 0.757 (115/152), so most of the drop is not the value.
- **By side:** lowest Windrunner 0.567, Leviathan 0.575, Talon Archer 0.600; highest Spoutling 0.882, Shellback 0.840.
- **By unit pair (78 rows, 10 fights each):** worst are Windrunner against Reef Runner, Grasper against Leviathan and Leviathan against the Water mix at 0 of 10, and six more at 1 of 10; seven of those nine pairs involve the Leviathan or the Windrunner. 16 pairs are at or below 0.4. Rows with fewer than 10 decided fights are marked small (the Shellback rows with stalemates).
- **Seat order:** 381 pairs of decided fights differ only in seat order; in 364 the predictor names the same side in both orders, and in 57 of those (15.7 percent) the measured winner is a different side in the two orders (a seat split the predictor cannot see by construction).
- **By unmodelled mechanic** (decided fights present / accuracy present / accuracy absent): drench 60 / 0.800 / 0.729, splash 562 / 0.706 / 0.813, momentum 556 / 0.748 / 0.699, flooding 155 / 0.677 / 0.749, regeneration (shield domes) 216 / 0.792 / 0.712, hit_delay 765 / 0.735 / none absent, windstorm_range and focus_fire never present. **`hit_delay` is present in every fight** (every weapon but the Grasper's and a few support units has `hit_delay_ticks` 2), so the C.5 rule "some unit's weapon has `HitDelayTicks` above 0" leaves the `clean` row empty (0 fights). Only-this counts are 0 for every mechanic except hit_delay (24 fights, 0.917); with hit_delay everywhere nothing else can be alone.
- **Calibration** (decided fights, predicted winner won / fights):

| margin bin | fights | predicted winner won | share |
| --- | ---: | ---: | ---: |
| predicted none | 9 | n/a | n/a |
| 1 to below 1.1 | 42 | 31 | 0.738 |
| 1.1 to below 1.25 | 96 | 35 | 0.365 |
| 1.25 to below 1.5 | 99 | 62 | 0.626 |
| 1.5 to below 2 | 70 | 56 | 0.800 |
| 2 to below 3 | 132 | 102 | 0.773 |
| 3 and above | 317 | 276 | 0.871 |

The AI attacks at a predicted advantage of 1.3 (`attack_min_advantage`), which is inside the 1.25 to 1.5 bin (0.626): at that edge the predicted winner wins about three fights in five. The 1.1 to 1.25 bin wins 0.365, worse than a coin flip: when the predictor says "slightly ahead" the other side usually wins.

## The verdict of the proposed rule

**SIMULATE.** Rule 1 fires on two of its three conditions (`predict.md` prints every threshold next to its measured value):

| Condition | Proposed | Measured | Fired |
| --- | ---: | ---: | --- |
| clean accuracy below `simulate_below_clean_accuracy` | 0.85 | 0 of 0 decided fights (empty) | no (cannot fire) |
| headline accuracy below `simulate_below_accuracy` | 0.75 | 562/765 = 0.735 | yes (by 0.015; Wilson upper bound 0.765) |
| calibration inverted by more than `calibration_max_inversion` | 0.10 | bin 1.1 to 1.25 wins 35/96 = 0.365, below the 1 to 1.1 bin at 31/42 = 0.738 | yes (by 0.37) |

Had rule 1 not fired, rule 2 would not have held either: the lowest judged mechanic is flooding at 0.677 (105/155), below `fix_min_mechanic_accuracy` 0.70, so the result would have been DEVELOPER_CHOICE (flooding listed; splash at 0.706 is 6 hundredths above the floor and below the 0.85 add-to-formula line). The decision is for the developer: the headline is within sampling noise of its bar and the clean-row rule is inert because of `hit_delay`, but the calibration inversion and the near-zero pairs (Leviathan, Windrunner) are not noise.

## Ambiguities in the spec and how they were resolved

- The `clean` row is empty because `hit_delay` is present everywhere; the spec's rule was applied as written, and the empty clean row cannot fire rule 1's first condition (stated in the report).
- Calibration "lower median of the winner's value left": taken as the measured winner's value left.
- The inversion test requires both bins to have at least `min_fights_per_row` decided fights, and compares every higher bin with every lower bin.
- Fights per table row count decided and no-winner fights; mismatched fights are in no table.
- Percent figures in intervals are Wilson (z 1.96) over decided fights; accuracy and the decision use integer arithmetic.
- The sweep run for the digest check (`--fights` on the current data) is not committed: it matches the committed records except `state_hash`.

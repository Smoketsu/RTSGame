<!-- rts/docs/reports/ai_kite/stats_main.md -->
<!-- Reported-not-judged statistics of the K-B main run (spec 5.4), computed from main/behaviour.jsonl by a throwaway script. Part of rts/docs/reports/ai_kite. -->
# K-B main run statistics

## Medians over the Water slot-games of the main run (72 per profile)

| Measure | aggressive_s5 | aggressive_s5_kite |
| --- | --- | --- |
| Trained water.grasper | 28.0 | 17.5 |
| Trained water.reef_runner | 24.0 | 40.0 |
| Trained water.spoutling | 9.0 | 12.0 |
| Trained water.shellback | 8.5 | 6.0 |
| Trained water.leviathan | 0.0 | 2.0 |
| Trained water.ink_lobber | 0.0 | 1.0 |
| Trained water.tender | 35.5 | 36.0 |
| Pummeler share of trained army units | 0.4 | 0.2405 |
| Reef Runner share of trained army units | 0.3355 | 0.508 |
| Worker losses before the overrun | 5.0 | 4.0 |
| Worker losses in the 600 s window | 34.5 | 31.0 |
| Army supply at 180 s, own | 10.0 | 10.0 |
| Army supply at 180 s, Skai | 13.0 | 13.0 |
| Army supply at 300 s, own | 23.0 | 23.0 |
| Army supply at 300 s, Skai | 33.0 | 33.0 |
| Army supply at 450 s, own | 9 | 10.5 |
| Army supply at 450 s, Skai | 68 | 65.0 |
| Game end tick | 10321.0 | 11191.5 |

Overrun second (median, games with one): aggressive_s5 313.5 of 72
Overrun second (median, games with one): aggressive_s5_kite 320.0 of 72

## Water wins

- aggressive_s5 as Water: 1 wins of 72
- aggressive_s5_kite as Water: 3 wins of 72
- pairs where only the candidate Water wins: 3; only the baseline Water: 1
- pairs where the candidate trained fewer Pummelers: 64 of 72
- pairs where the candidate game ended later: 45, earlier: 27 (of 72)
- pairs with the same Water opening in both games: 72 of 72
- candidate Water games that trained at least one Pummeler: 66 of 72

## Unit mix per Water opening (median trained per game, baseline / candidate; games baseline / candidate)

| Opening | games | grasper | reef_runner | spoutling | shellback | leviathan | ink_lobber |
| --- | --- | --- | --- | --- | --- | --- | --- |
| water_fast_expand | 18 / 18 | 27.5 / 18.5 | 22.0 / 41.0 | 9.5 / 12.5 | 9.0 / 6.0 | 0.0 / 2.0 | 1.0 / 1.0 |
| water_grasper_pressure | 13 / 13 | 33 / 26 | 35 / 38 | 12 / 13 | 4 / 1 | 1 / 2 | 0 / 0 |
| water_leviathan_tech | 18 / 18 | 20.5 / 2.0 | 28.5 / 49.5 | 10.0 / 14.0 | 10.0 / 8.0 | 0.0 / 2.5 | 1.0 / 1.0 |
| water_standard | 23 / 23 | 28 / 18 | 21 / 34 | 8 / 8 | 8 / 6 | 0 / 1 | 0 / 1 |

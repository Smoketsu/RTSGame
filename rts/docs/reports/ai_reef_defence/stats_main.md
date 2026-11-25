<!-- rts/docs/reports/ai_reef_defence/stats_main.md -->
<!-- Reported-not-judged statistics of the RR-B main run (spec 5.4) and the Water win counts of bar RR-B4, computed from main/behaviour.jsonl by a throwaway script. Part of rts/docs/reports/ai_reef_defence. -->
# RR-B main run statistics

## Medians over the Water slot-games of the main run (72 per profile)

| Measure | aggressive_s5 | aggressive_s5_rr |
| --- | --- | --- |
| Reef Runners trained | 3.0 | 25.5 |
| Worker losses before the overrun | 6.0 | 5.0 |
| Worker losses in the 600 s window | 35.5 | 34.0 |
| Overrun second (games with one) | 323.0 | 331 |
| Army supply at 180 s, own | 12.0 | 11.0 |
| Army supply at 180 s, Skai | 12.0 | 12.0 |
| Army supply at 300 s, own | 20.0 | 26.0 |
| Army supply at 300 s, Skai | 33.0 | 33.0 |
| Army supply at 450 s, own | 7.5 | 13 |
| Army supply at 450 s, Skai | 69.5 | 68 |
| Game end tick | 10257.5 | 10625.5 |

Games with an overrun second: aggressive_s5 72 of 72, aggressive_s5_rr 71 of 72

## Water wins

- aggressive_s5 as Water: 0 wins of 72, 0 draws or undecided
- aggressive_s5_rr as Water: 0 wins of 72, 0 draws or undecided

## Water opening draws (pair: baseline opening, candidate opening)

Pairs with the same Water opening in both games: 72 of 72 (different: 0). Baseline draws: {'water_fast_expand': 15, 'water_grasper_pressure': 20, 'water_leviathan_tech': 17, 'water_standard': 20}

## Unit mix trained per Water opening (mean per game)

### aggressive_s5

- water_fast_expand (15 games): grasper 38.1, ink_lobber 0.2, leviathan 0.2, reef_runner 3.2, shellback 10.3, spoutling 14.6, tender 34.7
- water_grasper_pressure (20 games): grasper 39.1, leviathan 0.9, reef_runner 15.8, shellback 3.5, spoutling 11.2, tender 36.6
- water_leviathan_tech (17 games): grasper 23.0, ink_lobber 0.5, leviathan 0.3, reef_runner 3.3, shellback 11.7, spoutling 11.0, tender 35.2
- water_standard (20 games): grasper 36.0, ink_lobber 0.3, leviathan 0.1, reef_runner 3.1, shellback 8.7, spoutling 9.4, tender 35.3

### aggressive_s5_rr

- water_fast_expand (15 games): grasper 32.1, ink_lobber 0.5, leviathan 0.3, reef_runner 25.4, shellback 10.1, spoutling 11.5, tender 34.5
- water_grasper_pressure (20 games): grasper 31.4, ink_lobber 0.1, leviathan 0.8, reef_runner 28.8, shellback 3.6, spoutling 9.9, tender 35.4
- water_leviathan_tech (17 games): grasper 23.6, ink_lobber 1.6, leviathan 1.2, reef_runner 25.6, shellback 12.0, spoutling 9.1, tender 36.5
- water_standard (20 games): grasper 28.9, ink_lobber 0.3, leviathan 0.2, reef_runner 22.2, shellback 8.5, spoutling 9.2, tender 35.8


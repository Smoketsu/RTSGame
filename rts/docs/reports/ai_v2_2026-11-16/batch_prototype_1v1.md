<!-- rts/docs/reports/ai_v2_2026-11-16/batch_prototype_1v1.md: balance harness report for 20 AI-versus-AI games per matchup on prototype_1v1 (17 Nov 2026, main at ca2589c, 1500 s cap), kept as evidence that the skirmish AI v2 finishes games and that mirror matches are not decided by spawn side. -->
# Balance report
Data digest `5a56ebaaa1b6f187` · base seed 20261026 · 20 games per matchup per map · AI profile `aggressive` · max game 1500 s

## Hard limits
| Limit | Requirement | Measured | Result |
| --- | --- | --- | --- |
| HQ survivability floor (Rooted) | >= 20.0 s against 30 supply | 6.6 s (water.ink_lobber x15 vs skai.great_aerie) | FAIL |
| Water vs earth median length | <= 1500.0 s | no earth faction | DEFERRED |

## Win rates
| Map | Matchup | Games | Decided | Draws | Undecided | First faction wins | 95% interval | Flags |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| prototype_1v1 | skai_vs_skai | 20 | 20 | 0 | 0 | 55.0% | [34.2, 74.2] |  |
| prototype_1v1 | skai_vs_water | 20 | 20 | 0 | 0 | 75.0% | [53.1, 88.8] | outside band |
| prototype_1v1 | water_vs_water | 20 | 20 | 0 | 0 | 45.0% | [25.8, 65.8] |  |

## Match length (decided games)
| Map | Matchup | Median | p10 | p90 |
| --- | --- | --- | --- | --- |
| prototype_1v1 | skai_vs_skai | 571.6 | 462.8 | 990.7 |
| prototype_1v1 | skai_vs_water | 605.3 | 474.4 | 981.6 |
| prototype_1v1 | water_vs_water | 1027.5 | 907.8 | 1147.9 |

## HQ time-to-kill
### Floor trials
| Attacker | Unit | Count | Defender HQ | State | Time to kill |
| --- | --- | --- | --- | --- | --- |
| skai | skai.fledgling | 30 | skai.great_aerie | Uprooted | 20.6 s |
| skai | skai.fledgling | 30 | skai.great_aerie | Rooted | 35.2 s |
| skai | skai.fledgling | 30 | water.grand_shell | Uprooted | 26.3 s |
| skai | skai.fledgling | 30 | water.grand_shell | Rooted | 48.3 s |
| skai | skai.roc_rider | 10 | skai.great_aerie | Uprooted | 16.9 s |
| skai | skai.roc_rider | 10 | skai.great_aerie | Rooted | 25.3 s |
| skai | skai.roc_rider | 10 | water.grand_shell | Uprooted | 20.4 s |
| skai | skai.roc_rider | 10 | water.grand_shell | Rooted | 32.6 s |
| skai | skai.stormcaller | 10 | skai.great_aerie | Uprooted | 14.6 s |
| skai | skai.stormcaller | 10 | skai.great_aerie | Rooted | 11.0 s |
| skai | skai.stormcaller | 10 | water.grand_shell | Uprooted | 17.7 s |
| skai | skai.stormcaller | 10 | water.grand_shell | Rooted | 13.5 s |
| skai | skai.talon_archer | 15 | skai.great_aerie | Uprooted | 16.8 s |
| skai | skai.talon_archer | 15 | skai.great_aerie | Rooted | 26.3 s |
| skai | skai.talon_archer | 15 | water.grand_shell | Uprooted | 20.2 s |
| skai | skai.talon_archer | 15 | water.grand_shell | Rooted | 33.3 s |
| skai | skai.windrunner | 30 | skai.great_aerie | Uprooted | 9.2 s |
| skai | skai.windrunner | 30 | skai.great_aerie | Rooted | 13.9 s |
| skai | skai.windrunner | 30 | water.grand_shell | Uprooted | 11.0 s |
| skai | skai.windrunner | 30 | water.grand_shell | Rooted | 16.8 s |
| water | water.grasper | 15 | skai.great_aerie | Uprooted | 11.9 s |
| water | water.grasper | 15 | skai.great_aerie | Rooted | 17.4 s |
| water | water.grasper | 15 | water.grand_shell | Uprooted | 14.0 s |
| water | water.grasper | 15 | water.grand_shell | Rooted | 21.5 s |
| water | water.ink_lobber | 15 | skai.great_aerie | Uprooted | 7.8 s |
| water | water.ink_lobber | 15 | skai.great_aerie | Rooted | 6.6 s |
| water | water.ink_lobber | 15 | water.grand_shell | Uprooted | 9.7 s |
| water | water.ink_lobber | 15 | water.grand_shell | Rooted | 7.6 s |
| water | water.shellback | 10 | skai.great_aerie | Uprooted | 21.3 s |
| water | water.shellback | 10 | skai.great_aerie | Rooted | 31.3 s |
| water | water.shellback | 10 | water.grand_shell | Uprooted | 25.3 s |
| water | water.shellback | 10 | water.grand_shell | Rooted | 38.7 s |
| water | water.spoutling | 15 | skai.great_aerie | Uprooted | 20.4 s |
| water | water.spoutling | 15 | skai.great_aerie | Rooted | > 300 s |
| water | water.spoutling | 15 | water.grand_shell | Uprooted | 24.4 s |
| water | water.spoutling | 15 | water.grand_shell | Rooted | > 300 s |
| water | water.tender | 30 | skai.great_aerie | Uprooted | 24.2 s |
| water | water.tender | 30 | skai.great_aerie | Rooted | 39.0 s |
| water | water.tender | 30 | water.grand_shell | Uprooted | 28.0 s |
| water | water.tender | 30 | water.grand_shell | Rooted | 48.8 s |

### In games (median, information only)
| Map | Matchup | Killed HQ | Kills | Median |
| --- | --- | --- | --- | --- |
| prototype_1v1 | skai_vs_skai | skai | 20 | 38.6 s |
| prototype_1v1 | skai_vs_water | skai | 5 | 145.2 s |
| prototype_1v1 | skai_vs_water | water | 15 | 18.5 s |
| prototype_1v1 | water_vs_water | water | 20 | 38.9 s |

## Determinism spot check
| Map | Matchup | Games re-run | Mismatches |
| --- | --- | --- | --- |
| prototype_1v1 | skai_vs_skai | 2 | 0 |
| prototype_1v1 | skai_vs_water | 2 | 0 |
| prototype_1v1 | water_vs_water | 2 | 0 |

## Warnings
- none

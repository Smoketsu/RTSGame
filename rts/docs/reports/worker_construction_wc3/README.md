<!-- rts/docs/reports/worker_construction_wc3/README.md -->
# Worker construction WC-3: re-baseline (18 Nov 2026)

First measurement of the skirmish AI after worker-built construction (`rts/docs/specs/worker_construction.md`) reached `main` (commit 4d23283). Nothing was tuned. All runs are on `prototype_1v1`, base seed 20261117.

## Ladder: `aggressive_s3` against `aggressive_s2b` (216 games, bar 0.65)

`LADDER decided=216 wins=162 fraction=0.750 PASS` (before worker construction: 180 of 216, 0.833). Raw output: `ladder_s3_vs_s2b/`, `ladder_s3_vs_s2b.txt`.

| matchup | wins of 72 | before |
| --- | ---: | ---: |
| skai_vs_skai | 54 | 72 |
| skai_vs_water | 36 | 36 |
| water_vs_water | 72 | 72 |

Every game was decided. The Skai-versus-water cell is still exactly half, the split that followed the faction before (not re-split by faction in this run).

## Response scenarios: `aggressive_s3` (`response_aggressive_s3.txt`)

5 of 10 pass; 5 of the 9 counted (water `armoured_answer` is a known balance failure). Gate is 6: missed, as before.

| scenario | Skai | water |
| --- | --- | --- |
| air_answer | PASS | PASS |
| armoured_answer | FAIL (share gain -0.073) | FAIL (-0.075, not counted) |
| trade | PASS (1.417) | PASS (1.169) |
| raid | FAIL (no static defence, 7 workers lost in a wave) | FAIL (static defence built, 7 workers lost in a wave) |
| turtle | FAIL (17 supply lost before first kill, limit 16) | PASS |

## Not measured

- `site_occupied` cancellation counts across the ladder (the ladder output does not carry them). In the three WC-2 traces (`../worker_construction_wc2/traces/`) they were 0, 0 and 5.
- Ablations, and the split of the Skai-versus-water cell by faction.

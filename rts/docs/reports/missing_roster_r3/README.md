<!-- rts/docs/reports/missing_roster_r3/README.md -->
# Missing roster R-3: re-measure with the full roster (18 Nov 2026)

Measured after the Thunderhead, Tidecaller and Leviathan reached `main` (`rts/docs/specs/missing_roster.md`, packages R-1 and R-2). Nothing was tuned. All runs are on `prototype_1v1`, base seed 20261117. By developer decision 11 on P-005, these AI-against-AI numbers are a record of the AI, not a balance reading.

## Ladder: `aggressive_s3` against `aggressive_s2b` (216 games, bar 0.65)

`LADDER decided=216 wins=169 fraction=0.782 PASS`. Raw output: `ladder_s3_vs_s2b/`, `ladder_s3_vs_s2b.txt`.

| matchup | wins of 72 | before the roster (WC-3) |
| --- | ---: | ---: |
| skai_vs_skai | 61 | 54 |
| skai_vs_water | 36 | 36 |
| water_vs_water | 72 | 72 |

The Skai-versus-water cell split by faction (from `ladder.jsonl`): Skai won all 72 games, whichever profile played it. The new units did not change that.

## Response scenarios: `aggressive_s3` (`response_aggressive_s3.txt`)

5 of 10 pass; 5 of the 9 counted. Gate 6: missed, unchanged.

| scenario | Skai | water |
| --- | --- | --- |
| air_answer | PASS | PASS |
| armoured_answer | FAIL (share gain -0.012; was -0.073) | FAIL (-0.029; was -0.075; not counted) |
| trade | PASS (1.417) | PASS (1.139) |
| raid | FAIL (no static defence, 7 workers lost in a wave) | FAIL (7 workers lost in a wave) |
| turtle | FAIL (41 supply lost before first kill, limit 16; was 17) | PASS |

## Reading

- The missing roster was not the cause of the failures: the same five scenarios fail, and Skai still wins every cross-faction game.
- `armoured_answer` moved toward passing for both factions but did not pass.
- Skai `turtle` got worse (17 to 41 supply lost before the first structure kill). Not investigated.

## Not measured

Ablations; which units each side built across the ladder; the equal-value fights (`rts/docs/specs/micro_benchmark.md`, not built yet).

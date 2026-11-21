<!-- rts/docs/reports/paired_first/README.md -->
# First paired ladder: `aggressive_s5` against `abl_s5_no_h` (package M, M.1 and M.2)

The same comparison as the unpaired AI-H strength ladder (`rts/docs/reports/ai_h/README.md`: 111 of 216, 0.514), played as 108 pairs. One run, nothing tuned.

- Command: `dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --ladder --paired --profile-a abl_s5_no_h --profile-b aggressive_s5 --jobs 4` (the defaults of `ai_eval.json`: `prototype_1v1`, three matchups, 72 games per cell, base seed 20261117, cap 2400 s). Run from a copy of the Release build of commit `5ba6f4b`, started 2026-11-21 08:06 UTC.
- Wall time 46 min 17 s at 4 jobs (141 CPU minutes) for 216 games, 4.7 games a minute (the spec's stated rate was 4.6; the AI-H ladder took 65 minutes).
- Files here: `ladder.md` (the full report) and `ladder.jsonl` (216 records, each with `pair` and `candidate_slot`).
- Determinism spot check: 2 games re-run in-process with the paired seat rule, 0 mismatches.

## Output (stdout, verbatim)

```
cell prototype_1v1 skai_vs_skai 25/72
cell prototype_1v1 skai_vs_skai 50/72
cell prototype_1v1 skai_vs_skai 72/72
cell prototype_1v1 skai_vs_water 25/72
cell prototype_1v1 skai_vs_water 50/72
cell prototype_1v1 skai_vs_water 72/72
cell prototype_1v1 water_vs_water 25/72
cell prototype_1v1 water_vs_water 50/72
cell prototype_1v1 water_vs_water 72/72
WARNING prototype_1v1/skai_vs_water: 49 distinct of 72 decided games (0.681); seeds barely vary games, do not read these win rates as balance (P-005 decision 11).
WARNING opening skai/skai_air: wins 4 of 24 mirror games against other openings (0.167, below 0.35).
WARNING opening skai/skai_windrunner_pressure: wins 14 of 20 mirror games against other openings (0.700, above 0.65).
LADDER candidate=aggressive_s5 baseline=abl_s5_no_h decided=216 wins=107 fraction=0.495 distinct=182 mirror=71/144 cross=36/72 bar=0.650 FAIL
PAIRED candidate=aggressive_s5 baseline=abl_s5_no_h pairs=108 complete=108 distinct=108 concordant=83 candidate_both=12 baseline_both=13 p=1.0000 advantage=-0.5 interval=[-5.2,+4.4] mde=6.5 pairing=skai:full,water:full verdict=NO_MEASURABLE_EFFECT
```

Exit code 4 (NO_MEASURABLE_EFFECT). The `LADDER ... FAIL` is the old win bar of 0.65 (not the question here; the exit code is the paired verdict).

## Pairs, per cell (from `ladder.md`)

| row | pairs | complete | concordant (S) | candidate both (W) | baseline both (L) | discordant (W + L) | share r | openings held | p | advantage [interval] | MDE |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| skai_vs_skai | 36 | 36 | 25 | 5 | 6 | 11 | 0.306 | 36 | 1.0000 | -1.4 [-10.2, +8.2] | 13.0 |
| skai_vs_water | 36 | 36 | 36 | 0 | 0 | 0 | 0.000 | 36 | 1.0000 | +0.0 [+0.0, +0.0] | 9.6 |
| water_vs_water | 36 | 36 | 22 | 7 | 7 | 14 | 0.389 | 36 | 1.0000 | +0.0 [-10.5, +10.5] | 14.6 |
| mirror (the two above) | 72 | 72 | 47 | 12 | 13 | 25 | 0.347 | 72 | 1.0000 | -0.7 [-7.8, +6.5] | 9.8 |
| cross | 36 | 36 | 36 | 0 | 0 | 0 | 0.000 | 36 | 1.0000 | +0.0 [+0.0, +0.0] | 9.6 |
| **all** | 108 | 108 | 83 | 12 | 13 | 25 | **0.231** | 108 | 1.0000 | -0.5 [-5.2, +4.4] | 6.5 |

All 108 pairs are complete (no draw, no undecided game; median game 11 146 ticks, longest 21 742 of 48 000) and all 108 pair signatures are distinct (n = 108). Plain and distinct counts agree. Pairing kind is `full` for both factions, and openings held is 108 of 108, as test P-2 requires: both games of every pair drew the same openings.

## The number that decides whether pairing is worth using

**The discordant share is r = 25 / 108 = 0.231 over all pairs** (0.347 over the mirror pairs, 0.000 over the cross pairs). The spec says pairing helps only below one half; it does. The measured gain matches the spec's `sqrt(2r)` rule: sqrt(2 x 0.231) = 0.68, and 6.5 / 9.6 = 0.68.

Where the concordant pairs come from:
- **Cross cell: 36 of 36 pairs concordant.** Skai wins every cross game whichever profile it plays (the table above in `ladder.md`: candidate as Skai 36 of 36, as Water 0 of 36). A third of the games say nothing about the AI.
- **Identical games.** In 23 of the 36 skai_vs_water pairs and 11 of the 36 water mirror pairs the two games of a pair ended in the same state hash: the two profiles played the same game (the harassment code only matters when a game reaches it). In the Skai mirror none were identical, but 25 of 36 pairs were still concordant: seat and faction order decided them. (Computed from `ladder.jsonl`.)
- Candidate in slot 0 won 60 of 108 games, in slot 1 47 of 108; the paired layout holds seat constant inside each pair, which is why this does not bias the sign test.

## Minimum detectable effect beside the unpaired one

| Design | games or pairs | MDE (alpha 0.05 two-sided, power 0.80) |
| --- | --- | --- |
| unpaired, all cells (the AI-H design) | 216 games, all distinct | 9.6 points |
| paired, all cells (this run) | 108 pairs, D = 25 | **6.5 points** |
| unpaired, mirror cells only | 144 games | 11.7 points (spec M.2 table) |
| paired, mirror pairs only (this run) | 72 pairs, D = 25 | **9.8 points** |

Read the all-cells MDE with care. The 36 cross pairs add to n but can show no effect (the faction decides them), so the 6.5 assumes the profile matters as much in cross games as in mirror games; the honest figure for games where the profiles can differ is the mirror row, 9.8 points: pairing is 16 percent better than unpaired there (9.8 against 11.7), not 32 percent. The per-cell MDEs (13.0, 14.6) are for 36 pairs each. The cross row shows D = 0 and the degenerate interval [+0.0, +0.0] of the formula in M.1.5 (no discordant pair, so nothing is scaled); its MDE 9.6 uses D' = D_min = 6 and means nothing.

## Verdict

**NO_MEASURABLE_EFFECT.** W = 12, L = 13 (two-sided exact sign test p = 1.0000), advantage -0.5 points of game win rate, interval [-5.2, +4.4] (Clopper-Pearson, 0.05). Effects smaller than 6.5 points (9.8 in the mirror cells) could not have been seen. This agrees with the unpaired AI-H reading (111 of 216, "fits neutral as well as helps") and sharpens it: AI-H is not shown to change strength by more than about 5 points either way, using the same 216 games. By P-006 decision 3 a package whose effect cannot be measured is switched off, so this result does not by itself justify AI-H.

## Planning row for M.2 (filled from this run)

Pairs needed for a target MDE of m points, `n >= Z^2 x r / (400 m^2)` with Z = 28016 and r measured here (games = 2 x pairs):

| Target MDE | all cells, r = 25/108 | mirror cells only, r = 25/72 |
| --- | ---: | ---: |
| 10.0 points | 46 pairs (92 games) | 69 pairs (138 games) |
| 7.0 points | 93 pairs (186 games) | 140 pairs (280 games) |
| 5.0 points | 182 pairs (364 games) | 273 pairs (546 games) |

The all-cells column inherits the dilution described above; for effects that live in mirror games use the second column. At 4.7 games a minute, 280 games take about 60 minutes.

## Notes and caveats

- One ladder, one base seed (20261117): the interval is for these 108 pairs, not for the profile pair in general.
- The `EFFECT` line of an unpaired ladder counts distinct play signatures; applied to paired records it would over-count the candidate (identical games in a pair, one a candidate win and one a baseline win, collapse to one "candidate win"), so it is information for unpaired ladders only. Paired runs print the `PAIRED` line instead.
- The warnings about distinct games (skai_vs_water 49 of 72) and the two opening warnings are the existing warnings, which still read the game records; they do not change the exit code.

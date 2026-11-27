<!-- rts/docs/reports/ai_midgame/README.md -->
<!-- Report of package MG-B (rts/docs/specs/ai_midgame_window.md section 4): the confirming measurement of aggressive_s5_mid (rules FK and DH) on base seed 20261209. Part of rts/docs/reports/ai_midgame. -->
# MG-B: aggressive_s5_mid on seed 20261209

Build: branch `review/ai-midgame-build` (MG-1 code, merged with `main` after `roster_numbers_2`, so the new Water unit numbers and the seeded response guard are in). Release build for every game run. Nothing was changed after a result.

**Coordinator decisions (27 Nov 2026, fixed before any run, developer delegated):** the verdict design is head-to-head (candidate against `aggressive_s5`, 72 cross pairs, `--paired-faction water`, 144 games, the design of K-B); the spec's both-sides design (self-play, 72 pairs) is run in full and reported as SECONDARY, not part of the verdict; the response guard is the seeded guard RG (`--response --seeds 8`, base seed 20261128, then `--response-guard`), with the single-game `--response` lines recorded as information only. No second round.

## Verdict: NO_MEASURABLE_EFFECT (MG-8 pending the last suite line in `suite_chunks.md`)

- Primary design: MB-1 not significant and not WORSE (Water won 0 of 72 games with either profile), MB-2, MB-4 and MB-5 not WORSE, guards RG, MG-1 to MG-4, MG-6, MG-7 hold. By the coordinator's text the mechanism bar is only reported in the primary design, so the verdict is NO_MEASURABLE_EFFECT.
- **Caveat the developer should see:** the spec's literal rule says FAIL when MB-3 is "not BETTER", and in the head-to-head design own army at 450 s is not BETTER (37 / 33, p 0.72). In the secondary self-play design, where the spec defines MB-3, it is BETTER (55 / 17), and that design alone by the spec's rule is NO_MEASURABLE_EFFECT too.
- The candidate wins no more Water games than the baseline in any design. The mechanism (Water's army surviving to 450 s) shows in self-play only.

## Primary design: head to head (`h2h/`, 72 Water pairs; exact two-sided sign test, 0.05)

Header `pairing kind: skai:full,water:full`; determinism spot check 0 mismatches. Bars computed from `h2h/behaviour.jsonl` by a throwaway script (not committed); `won`, army ratio and worker losses equal the harness `BEHAVIOUR_PAIRED` lines.

| Bar | D | better / worse / ties | p | Verdict |
| --- | --- | --- | --- | --- |
| MB-1 won (higher) | 0 | 0 / 0 / 72 | 1.0000 | not significant, not WORSE |
| MB-2 army ratio Water over Skai at 300 s (higher) | 66 | 39 / 27 / 6 | 0.1753 | not WORSE |
| MB-3 own army at 450 s, dead = 0 (higher) (reported) | 70 | 37 / 33 / 2 | 0.7202 | not significant |
| MB-4 worker losses pre overrun (lower) | 61 | 38 / 23 / 11 | 0.0722 | not WORSE |
| MB-5 income to 300 s, common (higher) | 66 | 31 / 35 / 6 | 0.7122 | not WORSE |

Harness lines for the same run (informational): `income_by` 27 / 38 / 7 (p 0.2145), `harass_squads` 33 / 9 / 30 (p 0.0003, BETTER), `trained_unit` 23 / 46 / 3 (p 0.0076, WORSE, a count of trained units, not a bar), `harass_trade` 30 / 26, `depot_losses_young` 17 / 8. Water slot-game medians: army at 300 s 27.0 (baseline) against 29.5 (candidate), at 450 s 23.0 against 29.0, worker losses 5 against 4.

## Secondary design: both sides (`self_s5/`, `self_mid/`, `pairs.md`; 72 pairs, not part of the verdict)

| Bar | D | better / worse / ties | p | Verdict |
| --- | --- | --- | --- | --- |
| MB-1 Water won | 0 | 0 / 0 / 72 | 1.0000 | not significant |
| MB-2 army ratio at 300 s | 68 | 39 / 29 / 4 | 0.2750 | not WORSE |
| MB-3 own army at 450 s (the mechanism) | 72 | 55 / 17 / 0 | below 0.0001 | BETTER |
| MB-4 worker losses pre overrun | 59 | 26 / 33 / 13 | 0.4350 | not WORSE |
| MB-5 income to 300 s | 64 | 26 / 38 / 8 | 0.1686 | not WORSE |

By the spec's own rule this design gives NO_MEASURABLE_EFFECT (MB-1 not significant, MB-3 BETTER, the rest not WORSE). The spec's expectation that Skai wins at least 66 of 72 is met (72 of 72, both profiles).

Reported medians over the 72 Water slot-games, baseline / candidate: army at 180 s own 12 / 12, Skai 13 / 13; at 300 s own 28 / 28, Skai 33 / 32; at 450 s own (dead = 0) 11.5 / 27.5, Skai 67 / 67; Water armies alive at 450 s 65 / 66 of 72; worker losses before the overrun 5 / 5; overrun second 325 / 341.5; game end tick 10346 / 10555.5; Water common income per 30 s bucket to 300 s 187.5, 300, 395, 490, 640, 720, 760, 820, 855, 870 / 187.5, 300, 395, 490, 640, 720, 780, 837.5, 845, 860 (no fall in income during the window; the sign test on the sum leans worse, 26 / 38, p 0.17). Median trained units, baseline / candidate: Pummeler 27 / 26, Reef Runner 24 / 22, Shellback 10 / 11, Spoutling 11 / 9, Tender 35 / 37. Water opening draws are identical in both runs (13 grasper_pressure, 19 leviathan_tech, 18 fast_expand, 22 standard).

## Guards

| ID | Result | Numbers | Holds |
| --- | --- | --- | --- |
| MG-1 head to head, Water `won` | h2h | 0 / 0 / 72 | yes |
| MG-2 head to head, Skai `won` (`h2h_skai`, re-tally of the same cache; no new game) | h2h_skai | 0 / 0 / 72 | yes |
| MG-3 Skai mirror `won` (24 pairs) | mirror_skai | 3 / 1 / 20, p 0.625 | yes |
| MG-4 Water mirror `won` (24 pairs) | mirror_water | 5 / 9 / 10, p 0.424 | yes (not significant) |
| RG seeded response guard (replaces spec MG-5; `respseeds_*.jsonl`, 8 seeds, 10 scenarios) | `RESPONSE_GUARD HOLDS` | suite B 6, C 1, p_pt 625 (above the suite alpha); per scenario worst: `response_turtle_water` baseline 8, candidate 5 of 8 (B 3, p_pt 1250); `response_worker_raid_water` 2 to 0 (B 2, p_pt 2500); `response_mass_windrunner_water` 8 to 7 | yes |
| MG-6 clean commands | traces | `rejected=-` on every line of all four traces; spot check 0 mismatches in all six behaviour runs | yes |
| MG-7 variety | h2h, traces, self-play | (a) pairing kind full; (b) `harass_squads` Water 33 / 9 BETTER, Skai 24 / 25 not WORSE; (c) no Water unit type with baseline median at least 2 has candidate median 0 in any opening (table below); (d) attacks per trace at the last line: s5 game 0 P0 5 P1 5, game 1 P0 3 P1 4; mid game 0 P0 5 P1 4, game 1 P0 5 P1 5 | yes |
| MG-8 suite and pins | `suite_chunks.md` | see that file | see that file |

MG-7(c), median trained Water army units per opening, baseline / candidate (`ink_lobber` and `leviathan` are 0 / 0 in all openings): fast_expand (18 pairs) Pummeler 29 / 28, Reef Runner 22.5 / 18, Shellback 11 / 11.5, Spoutling 10.5 / 9; grasper_pressure (13) 32 / 31, 25 / 26, 8 / 8, 12 / 10; leviathan_tech (19) 20 / 20, 28 / 28, 12 / 12, 11 / 10; standard (22) 26.5 / 29, 21 / 21, 9 / 11, 10 / 9.

Single-game `--response` lines, information only (`response_aggressive_s5_stdout.txt`, `response_aggressive_s5_mid_stdout.txt`): baseline passes 5 of 10, candidate 4 of 10. The one flip is `response_mass_windrunner_skai`: PASS (trade ratio 1.323) to FAIL (0.600). Seeded: baseline 45 of 80, candidate 40 of 80; the same scenario passes 8 of 8 for both profiles in the seeded run.

## Reported, not judged

`BEHAVIOUR_PAIRED` lines of every run are in `<run>/behaviour.md`. Traces (`traces/`, seed 20261209, 600 s cap, games 0 and 1, both sides on one profile) carry the per-30-s stance, attacks, retreats, army and losses. The Water income and worker-loss bars lean worse in self-play and head to head (income to 300 s 26 / 38 and 31 / 35) but none reaches significance; the per-bucket medians show no income fall in the window, only the sum leaning, so the report cannot say whether rule DH's holds leave outlying fields to raiders.

## Deviations and notes

- The head-to-head runs use 144 games (72 pairs) as the coordinator decided, not the spec's 96. `h2h_skai` re-tallied the same 144 games from the cache.
- The spec's MG-5 (single-game response suite) is replaced by the seeded guard RG, as decided; the single-game lines are above.
- The income bar is the sum of the first 10 entries of `income` (common), as the spec says; the harness `income_by` check measures something else and is shown separately.
- The behaviour runs exit 2 because the existing check `hq_move_new_income` and similar fail for both profiles alike (12 of 13 pass for the candidate in every run); the response runs exit 2 because scenarios fail, as in earlier reports.
- The first attempt at the unit tests in one command was killed by the 30-minute limit while the game runs used all four cores; they ran again in the suite chunks.

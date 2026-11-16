<!-- rts/docs/reports/ai_v2_2026-11-16/README.md: AI traces after skirmish_ai_v2.md packages A and B and their review fixes (data, maps, AI economy and expansion; package C was already in), re-run on 16 Nov 2026 after the fixes, plus the spawn-swap check. Numbers read from each trace summary. Compare rts/docs/reports/ai_trace_2026-11-16/README.md (the freeze evidence). -->
# AI traces after packages A and B plus review fixes, 16 Nov 2026

Made with `dotnet run --project rts/tools/BalanceHarness -- --data rts/data --trace --map <map> --matchup <a_vs_b> --seed 20261026 --max-game-seconds 1200 --out <file>` (profile `aggressive`, game 0). None of the games reached the 1200 s cap. Files: `<map>__<matchup>__seed20261026.txt` (the four main games) and `swapped_spawns__prototype_1v1__<matchup>__seed20261026.txt` (the spawn-swap runs: a temporary copy of the data folder, not committed, in which the two spawn entries of `prototype_1v1` are exchanged, so slot 0 starts in the north-east at (83.5, 83.5) and slot 1 in the south-west at (12.5, 12.5); all else equal).

Relocation intervals are the 30 s sample in which the uproot was ordered; depot ticks are exact.

## The four main games

| Game | Ended | First depot (tick, tile) P0 / P1 | HQ relocations (sample, P0 / P1) | Gathered common / rare, P0 and P1 | Trained P0 | Trained P1 |
| --- | --- | --- | --- | --- | --- | --- |
| prototype skai_vs_water | slot 1 (Water) wins, tick 19820 (991 s) | 4900 (40, 19) / 4959 (69, 82) | 570 s / 600 s | 16000 / 600 and 17800 / 330 | fledgling 25, windrunner 111, talon_archer 30, roc_rider 3 | tender 11, grasper 104, shellback 20, ink_lobber 1 |
| prototype water_vs_water | slot 0 wins, tick 21517 (1075 s) | 4800 (26, 13) / 4989 (69, 82) | 600 s and 1050 s / 600 s | 19990 / 600 and 16545 / 255 | tender 11, grasper 120, shellback 21, ink_lobber 3 | tender 17, grasper 99, shellback 18, ink_lobber 1 |
| prototype skai_vs_skai | slot 0 wins, tick 9264 (463 s) | 4900 (26, 13) / 5089 (69, 82) | none / none | 6965 / 0 and 4800 / 0 | fledgling 11, windrunner 48, talon_archer 12 | fledgling 12, windrunner 30, talon_archer 10 |
| greybox skai_vs_water | slot 0 (Skai) wins, tick 15056 (752 s) | 4960 (9, 49) / 4519 (42, 56) | 450 s (Skai) / 420 s and 720 s (Water) | 12275 / 785 and 14300 / 25 | fledgling 16, windrunner 93, talon_archer 24, roc_rider 5 | tender 14, grasper 94, shellback 18 |

- On `prototype_1v1` the two players' first depot tiles are now exact mirrors, (26, 13) and (69, 82) (the scan fix; before the fix player 1 built at (68, 82)).
- Frozen state (idle workers, zero income and zero army while fields remained): none in any of the 14 player-games below. The review's pattern (6 idle workers, bank under 50, `cmds=-` for 30 minutes) did not occur.
- Rare is now gathered in three of the four games (Skai on prototype and greybox, both Water players on prototype), by depots and relocated HQs that reach the rare corner fields.
- Every player placed its first depot between 4519 and 5089 ticks (226 to 254 s).

## Spawn-side check (prototype_1v1, seed 20261026)

| Matchup | Normal spawns: winner, time | Swapped spawns: winner, time |
| --- | --- | --- |
| skai_vs_water | slot 1 (Water, north-east) wins, 991 s | slot 1 (Water, now south-west) wins, 536 s |
| water_vs_water | slot 0 (south-west) wins, 1075 s | slot 0 (now north-east) wins, 993 s |
| skai_vs_skai | slot 0 (south-west) wins, 463 s | slot 1 (now south-west) wins, 579 s |

What the six games say (one seed, one game each, so only indications):

- Skai versus Water: Water wins in both arrangements, so in these two games the result follows the faction, not the corner or the slot.
- Water mirror: slot 0 wins in both arrangements, in the south-west corner and in the north-east one, so the corner did not decide it here and the slot (or the think order that comes with it) may have.
- Skai mirror: the south-west player wins in both arrangements (slot 0 in the normal game, slot 1 in the swapped one), the one case that follows the corner. Its 463 s and 579 s games also differ in length by 116 s, so the corner is not the only difference.
- With three of six games following the slot or faction and one following the corner, these runs do not show a spawn-side lean in the AI itself. The site scans are now mirrored (see above), so the build-site asymmetry of the 16 Nov review no longer applies.

## Notes

- Relocation happens late (after 420 s), as the spec says (`max_live_depots` 2 reached and the main nearly empty). The games decided before 520 s needed none.
- The longest games, the Water mirrors (1075 s and 993 s), are the closest to the cap.

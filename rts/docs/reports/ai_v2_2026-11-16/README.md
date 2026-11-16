<!-- rts/docs/reports/ai_v2_2026-11-16/README.md: four 1200-second AI traces after skirmish_ai_v2.md packages A and B (data, maps, AI economy and expansion; package C was already in). Numbers read from each trace summary. Compare rts/docs/reports/ai_trace_2026-11-16/README.md (the freeze evidence). -->
# AI traces after packages A and B, 16 Nov 2026

Made with `dotnet run --project rts/tools/BalanceHarness -- --data rts/data --trace --map <map> --matchup <a_vs_b> --seed 20261026 --max-game-seconds 1200 --out <file>` (profile `aggressive`, game 0). The cap was 1200 s; none of the four games reached it. Files: `<map>__<matchup>__seed20261026.txt` (timeline every 30 s and summary).

Relocation ticks are only known to the 30 s sample (the command appears in the line of the interval it was issued in); first depot ticks are exact (the summary's "structure sites").

| Game | Ended | First depot (tick, tile) P0 / P1 | HQ relocation (interval it was ordered) P0 / P1 |
| --- | --- | --- | --- |
| prototype_1v1 skai_vs_water | slot 0 (Skai) wins, HQ destroyed at tick 12452 (622 s) | 4840 (242 s) at (26, 13) / 4959 (248 s) at (68, 82) | 540 to 570 s, rooted again by 600 s at (21.8, 63.5) / 570 to 600 s (game ended at 622 s) |
| prototype_1v1 water_vs_water | slot 0 wins, tick 22093 (1104 s) | 4920 at (26, 13) / 4959 at (68, 82) | 570 to 600 s (rooted at (21.7, 63.4) by 660 s) and again 1020 to 1050 s / 570 to 600 s (rooting at (74.3, 32.6) by 660 s) and again 1020 to 1050 s |
| prototype_1v1 skai_vs_skai | slot 0 wins, tick 10400 (520 s) | 4900 at (26, 13) / 5219 at (55, 70) | none / none |
| greybox_1v1 skai_vs_water | slot 1 (Water) wins, tick 7334 (366 s) | 6330 (316 s) at (9, 49) / 4519 (226 s) at (42, 56) | none / none |

Gathered, spent and trained per player (summary lines):

| Game | P0 gathered (common / rare) | P0 trained | P1 gathered (common / rare) | P1 trained |
| --- | --- | --- | --- | --- |
| prototype skai_vs_water | 11225 / 0 | fledgling 11, windrunner 94, talon_archer 20 | 10515 / 0 | tender 12, grasper 62, shellback 12 |
| prototype water_vs_water | 20420 / 600 | tender 11, grasper 119, shellback 21, ink_lobber 4 | 16110 / 600 | tender 22, grasper 87, shellback 17, ink_lobber 1 |
| prototype skai_vs_skai | 8695 / 0 | fledgling 11, windrunner 63, talon_archer 15 | 6825 / 0 | fledgling 11, windrunner 47, talon_archer 16 |
| greybox skai_vs_water | 4800 / 0 | fledgling 5, windrunner 35, talon_archer 11 | 6360 / 0 | tender 8, grasper 32, shellback 7 |

Frozen state (idle workers, zero income and zero army while fields remained): none. Of all the 30 s samples of the eight player-games only one has idle workers (3 idle at 1080 s, water mirror P0, with an income of 600 and an army of 49 Graspers: an HQ on the move). The review's pattern (6 idle workers, bank under 50, `cmds=-` for 30 minutes) did not occur.

Notes read from the traces:

- Both sides expand: every player placed a depot between 226 and 316 s (the `expand_at_seconds` 240 time, or the served share falling under 50 percent first), and 7 of the 8 player-games placed a second depot (the first greybox Skai player was beaten at 366 s with one).
- HQ relocation happens late (after 540 s), as the spec says (`max_live_depots` 2 reached and the main nearly empty), and the AI plays the long games through it: no relocation was needed in the games decided before 520 s.
- Rare is gathered only in the water mirror (600 each, the whole rare field), and spent on 4 and 1 Ink Lobbers. In the other games no depot or HQ reached a rare field before the game ended (the depots go to the nearest safe unserved field, which on these maps is a natural or a flank common field, and a rare field is served only when it lies within 10 tiles of such a depot). The `--ai-check` rare criterion (package D, rare by 720 s) is therefore not met by these four games; whether it matters is for package D.
- The Water mirror on prototype_1v1 reaches 1104 s: depots at the natural and at the far shelves, two relocations per player, then the first HQ falls. It is the longest game and the closest one to the cap.
- Stance changes: still frequent (Skai 17, Water 27 in the prototype skai_vs_water game over 622 s); each Attack to Massing pair is a retreat followed by a new attack 10 s later (`regroup_seconds`), which is the army package's behaviour, not the economy's.

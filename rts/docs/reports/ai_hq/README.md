<!-- rts/docs/reports/ai_hq/README.md -->
# AI-HQ: HQ mobility (cluster relocation)

Package AI-HQ of `rts/docs/specs/field_saturation.md`: rule HQ-C, keys `use_cluster_relocation` (old profiles false, `aggressive_s5` true) and `relocate_gain_ratio` (old 1, `aggressive_s5` 2.0), `relocate_when_home_remaining_fraction` 0.35 in `aggressive_s5` (old 0.1), the ablation `abl_s5_no_hq`. Built on `origin/main` at `d89533a` (AI-E on, AI-P switched off, AI-H on), so `aggressive_s5` is the AI-E plus AI-H AI plus AI-HQ. Nothing was tuned. Every run is `prototype_1v1`, base seed 20261117 (the harness default), 72 games per cell, `-c Release`, built once; `rts/build/` is git-ignored, so the raw `ladder.jsonl` and `ladder.md` files are not committed.

**Result in one line: the AI-HQ gate PASSES** on the strength ladder (111 of 216, 0.514 against the bar 0.450; mirror Wilson upper bound 60.1 against 50), on the self-ladder variety bar (1.000 distinct games per decided game in every cell, bar 0.8), on the response scenarios (6 of 10 against 5 of 10 for the ablation) and on HQ deaths after an uproot (2 within 60 s of an uproot against 6 for the ablation; 7 against 9 HQs lost while not Rooted). Two things to read before merging: with AI-HQ the HQ moves 104 times in 36 games (60 for the ablation) because a second move to a far cluster is common (31 of 72 player-games move twice or more), and the AI-E test E-6 no longer holds for `aggressive_s5` (see Tests). The strength ladder is a record of the AI, not a balance reading.

## Strength ladder: `aggressive_s5` against `abl_s5_no_hq` (`--non-regression`), 216 of 216 decided

Command (from the repository root, `-c Release`, built once): `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a abl_s5_no_hq --profile-b aggressive_s5 --jobs 4 --non-regression --out rts/build/ladder_hq_vs_nohq`. Elapsed 4065.2 s. Determinism spot check 2 games, 0 mismatches.

```
WARNING opening skai/skai_air: wins 3 of 22 mirror games against other openings (0.136, below 0.35).
WARNING opening skai/skai_standard: wins 24 of 28 mirror games against other openings (0.857, above 0.65).
WARNING opening water/water_leviathan_tech: wins 23 of 31 mirror games against other openings (0.742, above 0.65).
LADDER candidate=aggressive_s5 baseline=abl_s5_no_hq decided=216 wins=111 fraction=0.514 distinct=216 mirror=75/144 cross=36/72 bar=0.450 PASS
```

Distinct games per cell (distinct play signatures counted from `ladder.jsonl`; the `distinct fraction` column of `ladder.md` is a win share, not this):

| cell | decided | distinct games | distinct / decided | candidate wins | baseline wins | fraction | Wilson |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 32 | 40 | 0.444 | [33.5, 55.9] |
| skai_vs_water | 72 | 72 | 1.000 | 36 | 36 | 0.500 | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 43 | 29 | 0.597 | [48.2, 70.3] |

| row | decided | candidate wins | fraction | Wilson |
| --- | ---: | ---: | ---: | --- |
| mirror | 144 | 75 | 0.521 | [44.0, 60.1] |
| cross | 72 | 36 | 0.500 | [38.7, 61.3] |
| cross, candidate as skai / water | 36 / 36 | 36 / 0 | 1.000 / 0.000 | [90.4, 100.0] / [0.0, 9.6] |
| all | 216 | 111 | 0.514 | [44.8, 58.0] |
| candidate in slot 0 / slot 1 | 108 / 108 | 56 / 55 | 0.519 / 0.509 | [42.5, 61.0] / [41.6, 60.2] |

Undecided 0, draws 0. Median game length (end tick / 20, from the records): all 592.8 s (min 388.1, max 1296.9); skai_vs_skai 619.2 s; skai_vs_water 508.1 s; water_vs_water 694.1 s. The mirror-row Wilson upper bound is 60.1 percent (bar 50 percent): no mirror warning.

Openings (all seats of both profiles, mirror games against other openings, as the harness counts; draws = how often the opening was drawn, use share its share of the faction's draws):

| faction | opening | draws | use share | mirror games | wins | win fraction | in 0.35 to 0.65 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| skai | skai_air | 57 | 0.264 | 22 | 3 | **0.136** | no |
| skai | skai_fast_expand | 48 | 0.222 | 20 | 10 | 0.500 | yes |
| skai | skai_standard | 76 | 0.352 | 28 | 24 | **0.857** | no |
| skai | skai_windrunner_pressure | 35 | 0.162 | 20 | 8 | 0.400 | yes |
| water | water_fast_expand | 52 | 0.241 | 25 | 10 | 0.400 | yes |
| water | water_grasper_pressure | 42 | 0.194 | 18 | 8 | 0.444 | yes |
| water | water_leviathan_tech | 62 | 0.287 | 31 | 23 | **0.742** | no |
| water | water_standard | 60 | 0.278 | 32 | 12 | 0.375 | yes |

Three of eight openings are outside 0.35 to 0.65 (report only; the AI-H ladder had two: the same two Skai ones). The cross cell splits by faction as in every earlier ladder (the Skai side wins all 36, the Water side none), so cross 36/72 is not information about AI-HQ.

## Self-ladder (`aggressive_s5` against itself), 216 of 216 decided, 4188.1 s

Command: `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --ladder --profile-a aggressive_s5 --profile-b aggressive_s5 --jobs 4 --out rts/build/ladder_hq_self`.

```
WARNING opening skai/skai_air: wins 3 of 22 mirror games against other openings (0.136, below 0.35).
WARNING opening skai/skai_standard: wins 22 of 28 mirror games against other openings (0.786, above 0.65).
WARNING opening water/water_fast_expand: wins 6 of 25 mirror games against other openings (0.240, below 0.35).
WARNING opening water/water_leviathan_tech: wins 26 of 31 mirror games against other openings (0.839, above 0.65).
WARNING opening water/water_standard: wins 11 of 32 mirror games against other openings (0.344, below 0.35).
LADDER candidate=aggressive_s5 baseline=aggressive_s5 decided=216 wins=109 fraction=0.505 distinct=216 mirror=73/144 cross=36/72 bar=0.650 FAIL
```

(The `FAIL` and the 0.650 bar are meaningless in a self-ladder; "wins" counts the seat the harness names the candidate seat.)

| cell | decided | distinct games | distinct / decided (bar 0.8) | seat-A wins | Wilson |
| --- | ---: | ---: | ---: | ---: | --- |
| skai_vs_skai | 72 | 72 | 1.000 | 38 | [41.4, 63.9] |
| skai_vs_water | 72 | 72 | 1.000 | 36 | [38.7, 61.3] |
| water_vs_water | 72 | 72 | 1.000 | 35 | [37.4, 59.9] |

Rows: mirror 73/144 = 0.507 [42.6, 58.7]; cross 36/72 (Skai side 36 of 36, Water side 0 of 36); all 109/216 = 0.505 [43.8, 57.1]. Undecided 0, draws 0. Median game length: all 614.7 s (min 388.1, max 1240.1); skai_vs_skai 645.4 s; skai_vs_water 509.6 s; water_vs_water 686.4 s.

Openings in mirrors (all seats, mirror games against other openings; report only): skai_air 3/22 = **0.136**, skai_fast_expand 7/20 = 0.350, skai_standard 22/28 = **0.786**, skai_windrunner_pressure 13/20 = 0.650, water_fast_expand 6/25 = **0.240**, water_grasper_pressure 10/18 = 0.556, water_leviathan_tech 26/31 = **0.839**, water_standard 11/32 = **0.344**. Five of eight are outside 0.35 to 0.65 (AI-H self-ladder: two). Draw counts and use shares equal those of the strength ladder (same seeds).

## Response scenarios (`--response --profile <id>`)

Commands: `dotnet run --no-build -c Release --project rts/tools/BalanceHarness -- --data rts/data --response --profile aggressive_s5` and the same with `--profile abl_s5_no_hq` (logs `rts/build/response_s5.log`, `response_nohq.log`; both ended with `RESPONSE_SUMMARY`).

| scenario | `aggressive_s5` (AI-HQ on) | `abl_s5_no_hq` |
| --- | --- | --- |
| roc_rush skai | PASS (aa_units 19) | PASS (19) |
| roc_rush water | PASS (4) | PASS (4) |
| mass_pummeler skai | FAIL (share_gain -0.012) | FAIL (-0.012) |
| mass_pummeler water | FAIL (-0.062) | FAIL (-0.062) |
| mass_windrunner skai | PASS (trade_ratio 2.059, killed 35, lost 17) | PASS (1.944, 35, 18) |
| mass_windrunner water | PASS (1.451, 74, 51) | PASS (1.529, 78, 51) |
| worker_raid skai | FAIL (p1 pass; p2 fail: max_workers_lost_in_wave 7) | FAIL (p1 pass; p2 fail: 7) |
| worker_raid water | FAIL (p1 fail; p2 pass: 2) | FAIL (p1 fail; p2 pass: 2) |
| turtle skai | PASS (16 supply lost before the first kill; won by 613.2 s) | FAIL (26; 538.8 s) |
| turtle water | PASS (8, 443.3 s) | PASS (8, 444.3 s) |
| **passed** | **6 of 10** | **5 of 10** |

`RESPONSE_SUMMARY passed=6 of=10` against `passed=5 of=10`. The gate "a package fails if it passes fewer than its baseline" holds (6 against 5). The harness prints 10 scenarios; the spec's gate text says "6 of 9" (not reconciled, as in the AI-P and AI-H reports). The `abl_s5_no_hq` column equals the AI-H report's `aggressive_s5` column in every scenario and value (as it should: the same AI before AI-HQ). The one changed result (turtle skai, 26 to 16 supply lost before the first kill) was not traced to an HQ move; which rule acted in it was not checked.

## HQ moves per game (traces)

Helper: `rts/tests/Sim.Tests/Ai/AiHqMeasure.cs` (explicit, never in the suite). It replays the harness game with the same setup and loop as `GameRunner.Play`, prints UPROOT (tick, position) when an HQ leaves the Rooted state, ROOT when it stands again, and HQ_LOST (state at death, seconds since its last uproot began) when an HQ dies, and one GAME line per game with the final state hash. Commands (one process per matchup, three at a time): `HQ_PROFILE=<id> HQ_GAMES=12 HQ_ONLY=<matchup:0,...,matchup:11> dotnet test rts/tests/Sim.Tests -c Release --no-build --filter "FullyQualifiedName~AiHqMeasure" --logger "console;verbosity=detailed"` with `aggressive_s5` and `abl_s5_no_hq`, both seats the same profile; seed 20261117, games 0 to 11 of each of the three matchups (36 games, 72 player-games per profile; logs `rts/build/hqtrace/*.log`; the logger prints each line twice and they were de-duplicated). The `aggressive_s5` games were run twice (the second time with positions added to the helper); both runs gave the same counts. A move = one UPROOT. "HQ lost while moving" = the HQ died in state Uprooted, Uprooting or Rooting; "within 60 s of an uproot" = it died at most 60 s after its last uproot began (so also an HQ that had meanwhile rooted again).

| per 36 games (72 player-games) | `aggressive_s5` (AI-HQ on) | `abl_s5_no_hq` |
| --- | ---: | ---: |
| games in which at least one HQ moved | 36 of 36 | 36 of 36 |
| player-games in which the HQ moved at least once | 67 of 72 | 58 of 72 |
| HQ moves (uproots) in total | 104 | 60 |
| moves per game (mean) | 2.89 | 1.67 |
| uproots per player-game 0 / 1 / 2 / 3 / 4 | 5 / 36 / 26 / 4 / 1 | 14 / 56 / 2 / 0 / 0 |
| first move per player-game: earliest / median / latest | 371.0 s / 381.6 s / 580.0 s | 391.1 s / 400.3 s / 644.0 s |
| all moves: median / latest | 418.8 s / 856.5 s | 400.6 s / 925.0 s |
| moves by Skai / Water players | 57 / 47 | 33 / 27 |
| moves by matchup skai_vs_skai / skai_vs_water / water_vs_water | 43 / 21 / 40 | 21 / 15 / 24 |
| HQs lost (one per decided game) | 36 | 36 |
| HQ lost while moving (not Rooted at death) | 7 | 9 |
| HQ lost within 60 s of an uproot | 2 | 6 |
| median game length of these 36 games | 602.6 s | 529.5 s |
| games with the same final state hash as the other profile | 0 of 36 | 0 of 36 |

Reading (counts only; causes not isolated):
- The HQ already moved in every game before AI-HQ (the old rule at 10 percent fires at about 400 s on this map). With AI-HQ the first move comes about 20 s earlier (median 381.6 s against 400.3 s), and the HQ moves again: 31 of 72 player-games have two or more moves against 2 without (104 moves against 60). The first move goes to the nearby depot cluster (about 11 tiles); the second, from there, is often a long trip to a far cluster (for example from (23.5, 15.5) to (18.0, 76.2), about 90 s of travel).
- No move went back: for every player-game the stand points (where the HQ rooted) were compared pairwise; no later stand point lies within 10 tiles of an earlier one except the 7 cases where the HQ uprooted and rooted again at the same position (a relocation whose target was gone after the 15 s uproot, so it rooted where it stood: 7 of the 104 moves; seen in `skai_vs_skai` games 1, 5 and 11, `water_vs_water` games 4, 5 and 11). The latch `AiMemory.ClusterLeftFields` is what excludes the left home; the HQ-6 test checks it directly.
- HQ deaths after an uproot are not more than the ablation's: 7 against 9 HQs died not Rooted, 2 against 6 died within 60 s of an uproot began, although AI-HQ makes 104 moves against 60 (7 per 104 against 9 per 60).
- The games differ from the ablation's in all 36 (the first move changes the game from about 7500 ticks on), so the ladder cells are the measure of strength; the traces only count moves.

## Smokes

`dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data` prints `STATE_HASH=e88b3b972175571d`; with `--script rts/data/scripts/economy_smoke.json` it prints `STATE_HASH=12259d6626becbc0`. Both as required.

## Tests

Full `dotnet test rts/RtsGame.sln -c Release` of the final tree (`rts/build/fulltest2.log`): Sim **934 of 934 passed** (21 m 50 s), Net 33 of 33, Replay 27 of 27 (934 + 33 + 27 = 994). Before the change the suite had 926 + 33 + 27 = 986 tests; the 8 new ones are in `AiRelocationTests`: HQ0, HQ1 to HQ7. Warnings are errors and the Release build had none. The first full run of the tree at `624b800` had two failures, both of tests that name the shipped profiles, and both were fixed in the test files (no Sim code, no pin edited): `H0_ShippedValuesAndParser` (AI-H) now expects `abl_s5_no_hq` to carry harass ratio 1.2 like `aggressive_s5`, and `E6_MirrorRun_CapsHold_AndFirstDepotIsOnTime` (AI-E) now plays `abl_s5_no_hq` (the `aggressive_s5` of before AI-HQ): with AI-HQ on, the same seed-1234 game trips E-6's cap check ("field 2 over cap + 1 for two thinks at tick 9700", caps 4,3,4,4,2,3,3 against assigned 0,0,8,4,...), the same kind of result as AI-P had (see `reports/ai_p/README.md`). The cause was not analysed; a plausible one is the HQ move in that window (workers follow a relocating HQ, rule G3), not verified.

## Proof that old profiles and the ablation are unchanged

- Every profile under `rts/data/ai` (21 files after the ablation was added, 20 before) got two comment lines and `"use_cluster_relocation": false` and `"relocate_gain_ratio": 1` right after `harass_speed_ratio`; only `aggressive_s5` has `true`, `2` and, changed in place, `relocate_when_home_remaining_fraction` 0.35 (was 0.1). No other existing value was changed (test `HQ0_ShippedValuesAndParser` checks every file). `abl_s5_no_hq.json` is the old `aggressive_s5.json` (diff checked: only the first-line comment, the `id`, and the two added keys at the old values).
- With the switch off the code path is the old one: `TriggerRelocation` calls the old checks in the old order, `RelocationCandidates(false)` is the old filter, the latch set is empty and tested only through `Contains`. Test HQ-5 checks the old choice (field 11) on a prepared view.
- Before any code change `aggressive_s5` on `origin/main` (`d89533a`, AI-H merged) was run on three fixed games by the helper `AiHqMeasure` (the same loop as `GameRunner.Play`); seed 20261117, both seats `aggressive_s5`: skai_vs_water game 2 `ae4f362f8bdc9885`, skai_vs_skai game 3 `f6ee85b4dc84a331`, water_vs_water game 2 `b171e5cd2bf0bfeb`. Test `HQ7_AblationPlaysThePreAiHqGame` plays `abl_s5_no_hq` through `GameRunner.Play` and gets the same three hashes (passes, in the full run). (The skai_vs_skai hash differs from the one in the AI-H note, `235626745316a81d`, which was recorded before AI-H.)
- The response scenarios of `abl_s5_no_hq` equal the AI-H report's `aggressive_s5` column, value for value.
- The two smokes pass, and the pinned tests of old profiles (A-11, C2-11, the S3 self trace) hold: the full suite passes with no pin edited.

## Acceptance table (spec section 9 row AI-HQ and tests HQ-1 to HQ-5)

| Item | Bar | Result | Verdict |
| --- | --- | --- | --- |
| Ladder `aggressive_s5` vs `abl_s5_no_hq`, `--non-regression` | fraction >= 0.450 | 111/216 = 0.514 | PASS |
| Mirror row Wilson upper bound | >= 0.50 | 75/144, upper bound 0.601 | PASS |
| Self-ladder distinct games per decided game, every cell | >= 0.8 | 1.000, 1.000, 1.000 | PASS |
| Openings in mirrors 0.35 to 0.65 (report only) | report | strength ladder 3 of 8 outside (skai_air 0.136, skai_standard 0.857, water_leviathan_tech 0.742); self-ladder 5 of 8 outside | reported |
| Response passes, not fewer than `abl_s5_no_hq` | >= baseline | 6 of 10 against 5 of 10 | PASS |
| HQ deaths within 60 s of an uproot, not more than the ablation | <= ablation | 2 against 6 (not Rooted at death: 7 against 9) | PASS |
| HQ moves per game (report) | report | 2.89 against 1.67; first move median 381.6 s against 400.3 s; no return to an earlier stand point; 7 in-place uproot-and-reroot cycles | reported |
| HQ-1 to HQ-5 with the spec's numbers, HQ-0, HQ-6, HQ-7 | pass | 8 of 8 | PASS |

## What was not checked

- Only 12 games per matchup were traced (36 games); the ladder games themselves (216 each) were not traced for HQ moves.
- Whether the 7 in-place uproot-and-reroot cycles and the long second moves are good play, and why the AI-E cap check trips during a move, were not analysed.
- The Defend-stance rule uses the stance of the previous think (the HQ step runs before the army step); not tested against a threat appearing in the same think.
- The change in the turtle skai response scenario (26 to 16 supply lost before the first kill) was not traced to a cause.
- No `Sim` code changed; the `sim-reviewer` pass on the `rts/src/Ai` diff is still to be requested by the coordinator.

<!-- rts/docs/reports/ai_reef_on/README.md -->
<!-- Report of the developer's decision of 25 Nov 2026 to switch the Reef Runner defence weights (water.reef_runner 30) on in the main AI: profiles changed, pins re-pinned, suite totals and the response suite. Part of rts/docs/reports. -->
# Report: Reef Runner defence weights on in aggressive_s5

Data change only. `water.reef_runner` 30 (the `aggressive_s5_rr` difference: Water's main composition and all four Water openings, with the candidate file's tuning comment lines) is now in `aggressive_s5` and in its derived profiles. No change to `rts/src/Sim`, `rts/src/Ai`, `rts/src/Game`, unit numbers, bars or thresholds.

## Profiles

Changed (13): `aggressive_s5`, `aggressive_s5_b`, `aggressive_s5_e`, `aggressive_s5_fp2`, `aggressive_s5_m`, `aggressive_s5_pd`, `aggressive_s5_r`, `aggressive_s5_s`, `abl_s5_no_h`, `abl_s5_no_p`, `abl_s5_p1_only`, `abl_s5_p2_only`, `abl_s5b_no_b`. Each had the same ten lines (five tuning comments and five compositions) at the same line numbers as `aggressive_s5`; each header comment got one sentence naming the change.

`aggressive_s5_rr` kept, header says it now equals `aggressive_s5` apart from id and header (the diff is six lines: header, `$doc`, `id`).

Left alone:
- `abl_s5_no_e`: its definition is "plays exactly as `aggressive_s4r`" and `E7_AblationEqualsS4r` asserts it; `aggressive_s4r` must not be touched, so giving it the weights would break that equality.
- `aggressive_s3`, `aggressive_s4r` and older: as instructed.
- `abl_s2_blind`, `abl_s2_no_rare`: copies of `aggressive_s2`, not of `aggressive_s5`.
- Judgement call: `aggressive_s5_m` and `aggressive_s5_pd` are chain copies (of `_r`, and of `_m`) with a package of changes, not "s5 plus one change"; they were changed so each stays equal to its own baseline plus its stated package (`_m` against `_r`, `_pd` against `_m`).

## Pins (old to new)

| Test | Old | New |
| --- | --- | --- |
| `OldProfiles_FixedSeedGamesKeepTheirHash` (`aggressive_s5`) | end tick 9336, `5d921472ffe76c92` | 9750, `91c318f5a0ad05cb` (winner slot0 unchanged) |
| `H5_AblationPlaysThePreAiHGame` (`abl_s5_no_h`), `P6_AblationPlaysThePreAiPGame` (`abl_s5_no_p`): skai_vs_water game 2 | `0x19bd86827db8fe7f` (tick 11216) | `0x7ff001b72095a475` (11531) |
| same, water_vs_water game 2 | `0x1f1a676c5fa4472a` (15509) | `0xa0b95b56e61cf63a` (14764) |
| same, skai_vs_skai game 3 | `0x53c785498c77ec59` | unchanged |
| `V2_Distribution` melee / all | 498 / 502 | 499 / 501 |
| `V2_WallDistribution` none / funnel / sealed / melee | 190 / 308 / 502 / 484 | 201 / 298 / 501 / 482 |

Unmoved, as required: smoke `df73e9584e791bc0`, economy_smoke `b724ff589cd6aa1b`, the `aggressive_s3` trace, every Sim-only pin (their chunks passed unchanged).

Tests updated because they assert the old situation (assertions of the same kind kept):
- `Rr1_ParsesAndDiffersOnlyInWaterReefWeights`: baseline opening weight 10 becomes 30.
- `Rr1_WaterTrainsReefRunnersBeyondTheScout`: "baseline fewer than candidate" becomes "equal" (19 and 19, baseline was 1).
- `Bq6_HeadOrder`: three Reef Runners added next to the three graspers so the Ink Lobber is still the head (Reef Runner weight equals the grasper's).
- `Off2_OldBehaviourAndDraws` (`AiProductionTests`): the baseline is now parsed from the file text like its twin. `AiProfile.FromDataSet` also attaches the authored wall lines, which a text parse cannot (the setter is private), and with the new mix the drawn wall style builds them, so the two differed for that reason alone; the assertion (switched-off keys are inert) is unchanged.

## Suite (Release, in chunks, `suite_chunks.md`)

Economy and Combat 237, Data and the other data folders 373, Sim rest 113, Ai (4 classes) 67, Ai (rest) 349, Harness 148: Sim 1287 run, all passed. Net 33, Replay 30. `--list-tests` for Sim lists 1289; 1287 + 2 `[Explicit]` (`AiHarassMeasure.PrintRaids`, `AiPlacementMeasure.PrintPlacements`) = 1289. Two chunk lines were superseded by reruns after the re-pins (`ai_b_firstrun`: 9 failures that are the pins above; `harness_firstrun` ran while the re-pins were being built, rerun passed).

## Response suite (`response_s5.txt`)

`BalanceHarness --data rts/data --response --profile aggressive_s5`: `RESPONSE_SUMMARY passed=6 of=10`, identical line by line to `aggressive_s5_rr` in `rts/docs/reports/ai_reef_defence/response_rr.txt` (`response_turtle_water` PASS with 8 supply lost; before the change `aggressive_s5` read 5 of 10). The exit code is 2 because four scenarios fail, as before.

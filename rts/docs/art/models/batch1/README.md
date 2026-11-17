<!-- rts/docs/art/models/batch1/README.md: which 3D model file is the approved one for each Skai and water unit, and how the models were made. Read this before importing unit models into Unity. -->
# Unit models, batch 1 (16 Nov 2026)

All thirteen Skai and water units have a model the developer marked Keep. Use only the files in the table; the other `.glb` files in this folder are rejected attempts kept for reference.

| Unit (data ID) | Approved model | Built from |
| --- | --- | --- |
| Fledgling, Skai worker (`fledgling`) | `skai_worker.glb` | `concept/turnarounds2/skai_worker_sheet.jpg` |
| Windrunner, scout (`windrunner`) | `../trial2/skai_scout.glb` | `concept/turnarounds1/skai_scout_sheet.jpg` |
| Talon Marksman (`talon_archer`) | `skai_marksman_v3.glb` | `concept/turnarounds6/skai_marksman_sheet.jpg` |
| Stormcaller (`stormcaller`) | `skai_stormcaller_v4.glb` | `concept/turnarounds5/skai_stormcaller_sheet.jpg`; texture regenerated |
| Roc Rider (`roc_rider`) | `skai_roc_rider_v2.glb` | `concept/turnarounds6/skai_roc_rider_sheet.jpg`; wings flat for rigging |
| Thunderhead (`thunderhead`) | `skai_thunderhead_v6.glb` | `concept/turnarounds7/skai_thunderhead_sheet.jpg`; hull grey darkened in the texture |
| Tender, water worker (`tender`) | `water_worker_v2.glb` | `concept/turnarounds5/water_worker_sheet.jpg` |
| Shellback (`shellback`) | `../trial2/water_clam.glb` | `concept/turnarounds1/water_clam_sheet.jpg` |
| Pummeler (`grasper`) | `water_pummeler_v2.glb` | `concept/turnarounds4/water_pummeler_sheet.jpg` |
| Spoutling (`spoutling`) | `water_spoutling.glb` | `concept/turnarounds2/water_spoutling_sheet.jpg` |
| Brinecaster (`ink_lobber`) | `../trial2/water_snail.glb` | `concept/turnarounds1/water_snail_sheet.jpg` |
| Tidecaller (`tidecaller`) | `water_tidecaller.glb` | `concept/turnarounds3/water_tidecaller_sheet.jpg` |
| Leviathan (`leviathan`) | `water_leviathan.glb` | `concept/turnarounds3/water_leviathan_sheet.jpg` |
| Reef Runner, water scout (`reef_runner`, unit data not written yet) | `water_reef_runner.glb` (awaiting the developer's mark) | `concept/turnarounds8/water_scout_sheet.jpg` |

## How they were made

- Each unit was drawn as a front, side and back sheet, cut into three images (`<name>_front.png`, `_left.png`, `_back.png`) and sent to Tripo's multi-view endpoint with `rts/tools/Models3D/tripo.py` (model `P2-20260801`, face limit 5000, texture on, PBR off). One model costs 110 credits.
- `tripo.py --retexture <task id>` regenerates only the texture of an earlier model for 10 credits. It fixed the Stormcaller, whose first texture came out grey.
- Two Thunderhead textures were adjusted by hand (grey pixels darkened with a gamma curve, yellows and whites untouched) because the generator's greys were lighter than the other Skai units.
- Lessons: every view the generator does not see is guessed, so a flat aircraft needs a back view, not a view from below; loose hoses and parts near the shoulders become stray geometry; hands must be clearly traceable in every view; wings meant for rigging should be drawn flat and level.

## Not done yet

- Rigging, animation and the Unity import are untested for every model.
- The models are smoother than the faceted low-poly drawings.
- HQs, buildings and the earth faction have concept art but no model sheets or models.
- 1,790 Tripo credits remained after this batch.

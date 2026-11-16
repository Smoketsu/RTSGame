<!-- rts/docs/art/concept/turnarounds5/README.md: results of the fifth set of model sheets, a redraw of four sheets after the third developer review of 16 Nov 2026 (front, side, back), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 5

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`, same style text as turnarounds4. References: Marksman `turnarounds4/skai_marksman_sheet.jpg`; Water Worker `turnarounds4/water_worker_sheet.jpg`; Stormcaller `turnarounds2/skai_stormcaller_sheet.jpg`; Roc Rider only the original `round9/skai_air_units.jpg` (no turnarounds3 or turnarounds4 sheet). 5 image requests in total: 4 first attempts, then 1 regeneration (Stormcaller). PNG originals are kept outside the repository.

Approximate x-ranges of the three views (front, side, back) in the 1536-pixel-wide image, for cropping (est. where views touch):

| Shot id | Front | Side | Back |
| --- | --- | --- | --- |
| skai_marksman_sheet | 30-345 | 400-1035 (rifle included) | 1105-1515 |
| skai_roc_rider_sheet | 10-510 | 495-1020 | 1030-1530 |
| skai_stormcaller_sheet | 40-500 | 580-915 | 1060-1470 |
| water_worker_sheet | 50-530 | 570-940 | 1010-1400 |

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_marksman_sheet | skai_marksman_sheet.jpg | gpt-image-2.6-sunburst | pass | Rifle on the bird's right: muzzle offset to the viewer's left in front with the chest emblem clear, level in side view, rifle visible beside the body on the viewer's right in back view; two-handed. |
| skai_roc_rider_sheet | skai_roc_rider_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Same unit as round9 (see feature list in the report); vents and metal feathers added. Reservation: wings droop in front and back views as in the original, and the under-wing crystals are seen only in the front and back views. |
| skai_stormcaller_sheet | skai_stormcaller_sheet.jpg | gpt-image-2.6-sunburst | pass | No hoses; two dishes on thick arms rising from the backpack, symmetrical, visible in all three views; plain identical shoulder plates; staff in the bird's right hand. |
| water_worker_sheet | water_worker_sheet.jpg | gpt-image-2.6-sunburst | pass | Short hooked sea-green beak centred below the eyes in front and side views; mouth and siphon gone; no glass. |

## Changed prompts

Stormcaller, appended to change 2: "In the FRONT view the two dishes must be visible too: they rise on their arms from behind the shoulders to just above and beside the owl's head, one dish on each side of the head, so that the arm roots are hidden behind the owl but the dishes and the upper arms are seen, as in the reference sheet." Reason: the first attempt hid the dishes in the front view. `shots.json` holds the final prompts.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

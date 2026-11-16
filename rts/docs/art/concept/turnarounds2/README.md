<!-- rts/docs/art/concept/turnarounds2/README.md: results of the second set of three-view model sheets (front, side, back; front, side, top for the Thunderhead) for 3D generation, checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 2

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references` (the approved line-ups under `round8/`, `round9/` and `round20/`; each prompt names which unit of the line-up is meant), `quality: "high"`. 17 image requests in total: 10 first attempts, then 7 regenerations (one each for the seven sheets that failed on a dark glowing backdrop or a wrong view); no sheet needed a third attempt. PNG originals are kept outside the repository for the next step (cutting the views).

Approximate x-ranges of the three views (front, side, back; for the Thunderhead front, side, top) in the 1536-pixel-wide image, for cropping. Measured on the grey backdrop where the views are clearly separated; marked (est.) where the views touch or overlap in x and the range is estimated by eye:

| Shot id | Front | Side | Back / top |
| --- | --- | --- | --- |
| skai_worker_sheet | 60-450 | 520-955 | 1085-1475 |
| skai_marksman_sheet | 105-340 | 390-1040 (rifle included) | 1190-1430 |
| skai_stormcaller_sheet | 35-485 | 600-940 | 1050-1500 |
| skai_roc_rider_sheet | 15-545 (est.) | 515-1000 (est.) | 1015-1520 (est.) |
| skai_thunderhead_sheet | 20-490 | 520-955 | 980-1500 (top) |
| water_worker_sheet | 60-515 | 590-925 | 1030-1470 |
| water_pummeler_sheet | 45-530 (est.) | 545-960 | 1015-1500 |
| water_spoutling_sheet | 35-505 | 555-980 | 1025-1495 |
| water_tidecaller_sheet | 50-495 | 565-965 | 1040-1485 |
| water_leviathan_sheet | 15-540 (est.) | 535-985 (est.) | 1015-1520 (est.) |

The Roc Rider and Leviathan views are closer together than the others (wings and claws nearly touch the neighbouring view), so crop them with a small manual check.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_worker_sheet | skai_worker_sheet.jpg | gpt-image-2.6-sunburst | pass | One pelican, three agreeing views on plain grey. Plasma cutter on the same arm in all views, pointing forward-down; cargo-claw backpack with two arched arms visible from front, side and back. Charcoal, white, yellow only. |
| skai_marksman_sheet | skai_marksman_sheet.jpg | gpt-image-2.6-sunburst | pass | One heron. Long rifle points forward with the beak in every view (toward the viewer in front, left in side, away in back); yellow back fins and white and charcoal crest agree. Back view shows the pack but no rifle, correct. |
| skai_stormcaller_sheet | skai_stormcaller_sheet.jpg | gpt-image-2.6-sunburst | pass | One owl. Trident staff in the same hand in all views, tip up; backpack with two yellow rotor discs visible in side and back views; face and talons agree. Armour, feather skirt and colours match the reference. |
| skai_roc_rider_sheet | skai_roc_rider_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Rider on mechanical raptor, wings spread level and symmetrical, head and handlebar forward in all views, correct palette. Reservation: the units fill only about half the sheet height (small in frame), and the side view shows the raptor banking slightly with the legs trailing down-back while the front and back views show the legs hanging straight down. |
| skai_thunderhead_sheet | skai_thunderhead_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Front (eye level, nose and wing-tip hooks), side facing left and top (nose up) as asked. Feathers only on the trailing edge, none on the front. Reservation: the top view is much larger than the front and side views (not the same scale), and the sheet does not follow the "same height" rule. |
| water_worker_sheet | water_worker_sheet.jpg | gpt-image-2.6-sunburst | pass | One octopus in pressure suit; helmet face matches front and side, backpack only on the back and side, eight tentacles, turquoise claws, mirrored left and right. Palette correct. |
| water_pummeler_sheet | water_pummeler_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | One mantis shrimp, club claws held forward at chest height in all views (toward the viewer in front, left in side). Reservation: the rose-coral reads strongly red-orange in this sheet; the stalk eyes are turquoise at the front but plain at the back. |
| water_spoutling_sheet | water_spoutling_sheet.jpg | gpt-image-2.6-sunburst | pass | One cuttlefish with three barrels on its head sloping up toward the face side (left in side view, away from the viewer in back view); the blade effects of the reference are omitted. Face, rose collar and curled tentacles agree across views. |
| water_tidecaller_sheet | water_tidecaller_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | One jellyfish, bell, rim, round lights, turquoise underside and tentacle bands agree; no wave. Reservation: in the side view the tentacles sweep left instead of hanging symmetrically. |
| water_leviathan_sheet | water_leviathan_sheet.jpg | gpt-image-2.6-sunburst | pass | One king crab. Twin cannons point forward-up in all views (toward the viewer, left in side, away in back, never backward); big claws low in front, spiked carapace, studded legs. No track marks. |

## Changed prompts

The first attempt of seven sheets failed: skai_worker_sheet, skai_roc_rider_sheet, water_worker_sheet, water_pummeler_sheet, water_spoutling_sheet and water_tidecaller_sheet were drawn on a dark, black backdrop with coloured glow halos (the pummeler's halo was red), and the Thunderhead's front view was drawn as a second top view. Every other sheet passed first time.

Change for the six glow sheets, appended to the end of each prompt: "BACKDROP: the whole image background is one flat plain light-grey colour from edge to edge, exactly like a studio model sheet; NOT dark, NOT black, no vignette, no gradient. Every light and panel is a flat painted colour that does not emit light: no glow, no halo, no bloom, no light spill, no coloured haze around the unit." The same text was appended to skai_thunderhead_sheet, whose front-view wording was also changed: "a FRONT view looking at the nose from straight ahead," became "a FRONT view: the aircraft seen from directly in front at eye level, so it looks like a flat low wide shape, only the nose tip, the two raised wing-tip hooks and the thin edge of the wing visible, the wing seen edge-on and NOT from above,", and "There is no back view." gained "Only the right-hand view may be seen from above; the left and middle views must be horizontal eye-level views." The second attempt of all seven passed. `shots.json` holds the final prompts.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

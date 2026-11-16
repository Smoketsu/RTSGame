<!-- rts/docs/art/concept/turnarounds3/README.md: results of the third set of model sheets, a revision of four turnarounds2 sheets after the developer review of 16 Nov 2026 (front, side, back; front, side, bottom for the Thunderhead), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 3

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`, same style text as turnarounds2. Each shot passes the original line-up plus the turnarounds2 sheet of the same unit as references (the two water sheets also pass `water_pummeler_sheet.jpg` and `water_spoutling_sheet.jpg` from turnarounds2 as colour references). 6 image requests in total: 4 first attempts, then 2 regenerations (Roc Rider and Thunderhead); no sheet needed a third attempt. PNG originals are kept outside the repository.

The Thunderhead's third view is a view from directly BELOW (underside, nose up in the image), not from above, so the underwing missile pods and guns show; the views are front, side, bottom.

Approximate x-ranges of the three views (front, side, back; for the Thunderhead front, side, bottom) in the 1536-pixel-wide image, for cropping (est. where views touch):

| Shot id | Front | Side | Back / bottom |
| --- | --- | --- | --- |
| skai_roc_rider_sheet | 25-575 | 500-1010 (est.) | 1010-1530 (est.) |
| skai_thunderhead_sheet | 30-500 | 530-975 | 1005-1500 (bottom) |
| water_tidecaller_sheet | 50-500 | 565-965 | 1045-1490 |
| water_leviathan_sheet | 20-540 | 535-1000 (est.) | 1010-1530 (est.) |

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_roc_rider_sheet | skai_roc_rider_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Slatted vent grilles on chest and flanks, rivets, panel seams and joints; wing and tail feathers are rigid metal blades. Legs identical (straight down) in all three views, body level. Reservation: figures fill about 65-70 percent of the sheet height, not 85-90, because three wide views must fit the width. |
| skai_thunderhead_sheet | skai_thunderhead_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Two missile pods (six tubes) and two gun barrels under the wings, symmetrical, visible in front and side; third view is genuinely from below (plain underside, no yellow top panels). Reservation: the bottom view is a little larger than the front and side views (about 10 to 15 percent). |
| water_tidecaller_sheet | water_tidecaller_sheet.jpg | gpt-image-2.6-sunburst | pass | Coral caps, rim, bands and light rings are the dusty brick-red coral of the Pummeler sheet, not pink. Tentacles hang straight down in all views, side view roughly symmetric. |
| water_leviathan_sheet | water_leviathan_sheet.jpg | gpt-image-2.6-sunburst | pass | Spikes, ring, claws and leg plates are the same muted coral as the Pummeler; design unchanged, cannons point forward-up in all views. |

## Changed prompts

Appended to the end of the first prompt (before the backdrop text) for the two regenerated sheets.

Roc Rider: "RETRY NOTES: in the previous attempt the figures were too small and the side-view legs trailed backward. Now: make each figure BIG, filling at least 75 percent of the sheet height from the rider's crest to the talon tips, with the three views nearly touching side by side (wings may be slightly shorter and more compact so three big views fit the width). LEGS: in ALL THREE views, including the side view, the two talon legs hang straight down from the belly in a short folded position, vertical, NOT trailing backward, NOT swept back; the raptor body is perfectly horizontal in the side view, not banking or tilting."

Thunderhead: "RETRY NOTES: in the previous attempt the third view was still seen from above and was larger than the others. Now: the third view is a BOTTOM view from directly below: show only the plain charcoal underside (belly) with panel lines and the white trailing-edge feathers, NO yellow top-surface panels and NO top of the cockpit; the four underwing missile pods and gun barrels are big and clear on the underside. SCALE: the bottom view has exactly the same wingspan as the front view and exactly the same nose-to-tail length as the side view, so all three views are the same size; draw all three views smaller if needed, about 480 pixels wide each, vertically centred on the same line, never larger than each other." `shots.json` holds the final prompts.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

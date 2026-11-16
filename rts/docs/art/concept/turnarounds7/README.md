<!-- rts/docs/art/concept/turnarounds7/README.md: result of the seventh set of model sheets, one redraw of the Thunderhead sheet (front, side, back; boxy missile pods) after the developer's 3D model review of 16 Nov 2026, checked against rts/docs/art/CONCEPT_BRIEF.md; image made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 7

Model: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`, same style text as turnarounds4. The shot passes `turnarounds4/skai_thunderhead_sheet.jpg` as its only reference. 2 image requests in total: 1 first attempt, then 1 regeneration. The PNG original is kept outside the repository.

Approximate x-ranges of the three views (front, side, back) in the 1536-pixel-wide image, for cropping (est. where views touch):

| Shot id | Front | Side | Back |
| --- | --- | --- | --- |
| skai_thunderhead_sheet | 35-505 | 520-975 | 1035-1505 |

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_thunderhead_sheet | skai_thunderhead_sheet.jpg | gpt-image-2.6-sunburst | pass | Pods are short closed charcoal boxes on short pylons: six round yellow missile-tip holes (2 x 3) on the front face in the front and side views, plain closed rear faces in the back view. Exactly two thin guns close to the body (both in the front view, one in the side view). Bird head with white beak and yellow eyes in front and side views; back view is a true level elevation. Reservation: the missile-tip holes are yellow, not charcoal-ringed; views are fairly small on the sheet. |

## Changed prompts

Appended before the backdrop text for the regenerated sheet. The first attempt drew the back view from above (a top view), which the brief for this sheet excludes.

Thunderhead: "RETRY NOTES: in the previous attempt the BACK view was drawn from above like a top view (the whole upper hull and the long tail spike seen from above). The BACK view must be a true elevation seen from directly behind at the aircraft's own height, exactly like the FRONT view but from the other side: the same wing span and the same height as the front view, wings seen almost edge-on as a thin shallow V line, the rear of the hull as a low narrow shape in the middle, the two wing-tip hooks up at the ends, the white trailing-edge feather blades as a row along the back edge of the wings, the flat plain rear faces of both pods hanging below the wings. No bird head is visible from behind, and nothing is seen from above." `shots.json` holds the final prompt.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

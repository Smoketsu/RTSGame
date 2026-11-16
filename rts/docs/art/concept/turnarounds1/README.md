<!-- rts/docs/art/concept/turnarounds1/README.md: results of the first three-view model sheets (front, side, back) for 3D generation, checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 1

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references` (the trial renders under `rts/docs/art/models/trial/`), `quality: "high"`. 3 requests in total: each shot once, no regeneration. PNG originals are kept outside the repository for the next step (cutting the views).

Approximate x-ranges of the three views (front, side, back) in the 1536-pixel-wide image, for cropping:

| Shot id | Front | Side | Back |
| --- | --- | --- | --- |
| skai_scout_sheet | 170-440 | 590-940 | 1100-1440 |
| water_clam_sheet | 20-500 | 530-1005 | 1035-1500 |
| water_snail_sheet | 40-420 | 510-975 | 1085-1460 |

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_scout_sheet | skai_scout_sheet.jpg | gpt-image-2.6-sunburst | pass | Three true orthographic views, same height, plain grey, no text. Standing on both legs; both legs identical with yellow knee guard, ankle cuff and three yellow blade talons. Layered charcoal and white feather mantle on shoulders and back, visible in side and back views. Charcoal, white, yellow only; no real-world ornament. The views agree well; the side view shows the talons pointing forward from a slightly angled foot. |
| water_clam_sheet | water_clam_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | No dome. Two detailed tracks with separate tread plates, five turquoise-hub road wheels and a larger rear sprocket, identical both sides. Shell, rims and studs match across views. Reservation: the front shows the shell mouth open with the turquoise core; the back shows it nearly closed, so the shell gap differs a little, and the side view of the track is wider than the front-view track suggests. Gaps between views are narrow (about 30 pixels). |
| water_snail_sheet | water_snail_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Shell upright; one brace arm each side rising from the slug's back with a pivot disc on the shell's outer wall, matching in all three views. Exactly one scope (round lens with crosshair) on one eye stalk, the other a plain eye; tidy. Reservation: the slug body is drawn quite differently in each view (the back view is a plain symmetrical dome, the side view has a short tail), and a rose collar sits between shell base and slug in the side and back views that the front view hides. |

## Changed prompts

None. Every sheet passed on its first attempt, so no prompt was changed.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

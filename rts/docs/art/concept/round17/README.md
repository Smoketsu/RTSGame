<!-- rts/docs/art/concept/round17/README.md: results of concept art round 17 (Great Aerie redrawn with consistent perspective: hovering from below, hovering from the game camera, landed), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 17

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`, `quality: "high"`. 2 requests in total: the first attempt, then one regeneration with a changed prompt.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_great_aerie_hq | skai_great_aerie_hq.jpg | gpt-image-2.6-sunburst | pass | Same citadel three times, no eagle head, rim vanes all sweep one way, charcoal, white, electric yellow, no cultural ornaments. Left (from below): only the round belly, four spinning ducted propellers and folded talons; no mast, dome or turret shows. Middle (hovering, game camera): top deck, dome, two turrets, pads and workers visible, propeller ducts only peek below the rim at the sides, soft ground shadow; the belly is not visible from above. Right (landed): four taloned legs, ramp down, turrets raised, propellers stopped. Reservation: as in round 16, the propellers sit on the lower sides of the hull and read as side vents when landed; the left drawing's rim is seen slightly from the side, which reads as a low upward angle. |

## Changed prompts

Attempt 1 failed on the left drawing: a mast stuck up above the rim, so part of the top deck showed in the from-below view. The middle and right drawings were already correct. For attempt 2 the left-drawing sentence "the top deck is not visible at all in this drawing." became "the top deck is not visible at all in this drawing: no mast, dome, turret or pad sticks up above the rim, the silhouette is just the vane ring and the round belly." The change is recorded in `shots.json`. Attempt 2 passed; the three drawings did not need splitting.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

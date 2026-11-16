<!-- rts/docs/art/concept/round15/README.md: results of concept art round 15 (Great Aerie with one-way rim feathers, three tree-like Skai building options, water buildings with supply and tide well told apart), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 15

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`, all at `quality: "high"`. 6 requests in total: each shot once, plus one regeneration of `water_buildings`.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_great_aerie_hq | skai_great_aerie_hq.jpg | gpt-image-2.6-sunburst | pass (with reservation) | One nest citadel drawn twice, no eagle head, matte low-poly, charcoal, white and yellow. Every rim vane leans the same way around the circle like turbine blades, in both drawings. Landed drawing: four taloned bird legs, ramp, two raised turrets, three pelican workers. Reservation: in the hovering drawing the legs are not visible at all (hidden under the body), so "legs folded" is implied rather than shown. |
| skai_buildings_option_a_pine | skai_buildings_option_a_pine.jpg | gpt-image-2.6-sunburst | pass | Seven buildings counted (four top, three bottom): crane depot, supply roost, muster deck, sky dock with two hexagon pads, ringed storm spire, twin-emitter turret, wall of five pines. Reads as stacked angular conifers with white-tipped triangular boughs; talon roots on all. No curves. |
| skai_buildings_option_b_canopy | skai_buildings_option_b_canopy.jpg | gpt-image-2.6-sunburst | pass | Seven buildings counted (four top, three bottom), same jobs. Forked faceted trunks with flat diamond and hexagon canopy platforms, hanging pods, talon roots. Clearly different from A. Reservation: the wall reads as a long table on three stumps more than a tree line. |
| skai_buildings_option_c_lightning | skai_buildings_option_c_lightning.jpg | gpt-image-2.6-sunburst | pass | Seven buildings counted (four top, three bottom), same jobs. Zigzag lightning-bolt trunks and branches with yellow strips, white tips, pods and platforms at the forks; a small craft sits on one pad. Most distinct of the three. Slightly less tree-like than A and B. |
| water_buildings | water_buildings.jpg | gpt-image-2.6-sunburst | pass (second attempt) | Seven buildings counted (four top, three bottom). Supply is now a low cluster of mushroom and bubble corals with turquoise lights, no tubes. Tide well is a tall flared chimney taller than all others, overflowing with turquoise water down channels and out of spouts. The other five match round 14. Coral is now a dusty pink-rose, not red. No white. |

## Changed prompts

`water_buildings`, first attempt failed on colour and height (coral read as salmon-red, tide well only moderately taller than the rest). Regenerated once with two changes in `shots.json`:

- "dusky rose-pink coral (soft and greyish, not red)" became "dusty mauve-pink coral, a pale greyish pink like faded rose petals, clearly lighter and greyer than salmon or red".
- "one very tall thick central chimney of tube coral, much taller than every other building," became "one extremely tall slender-but-thick central chimney of tube coral, at least twice as tall as every other building and reaching the top edge of the picture,".

All other shots passed first time.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

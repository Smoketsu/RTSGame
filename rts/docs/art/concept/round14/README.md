<!-- rts/docs/art/concept/round14/README.md: results of concept art round 14 (Great Aerie leg options, Skai buildings with talon feet, water buildings as a coral reef), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 14

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`, all at `quality: "high"`. 3 requests in total: each shot once, no regeneration.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_great_aerie_leg_options | skai_great_aerie_leg_options.jpg | gpt-image-2.6-sunburst | pass | The landed nest citadel drawn three times, no eagle head in any; the centre is a low yellow-lit dome with an antenna mast. Legs differ clearly: four jointed bird legs with three-toed talons; six slender stilt legs with small spiked feet; three thick tripod pillars on round pads with claw anchors. Charcoal, white and yellow; matte low-poly; no cultural ornament. Small workers and a ramp are kept from round 13. |
| skai_buildings | skai_buildings.jpg | gpt-image-2.6-sunburst | pass | Seven buildings counted (four top, three bottom), the same as round 13: crane depot, pod-tree roost, muster deck, sky dock, ringed spire, twin turret, wall. Every leg and strut now ends in a small clawed bird foot with yellow tips, visible on all seven. The wall's white feathers stand upright along its top, none hang down. Matte, faceted. |
| water_buildings | water_buildings.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Seven buildings counted (four top, three bottom): table-coral depot with ramp, tube-sponge supply cluster, brain-coral ring brood pool, tube-coral tide well with turquoise water pouring from arches, brain-coral workshop dome with staghorn crown, anemone turret with a raised cannon, branching-coral wall. Reads as a reef, no clam shells and no spiral shells. Reservation: the coral bands are closer to red-coral than dusky rose, and the ring pool reads a little like a plain donut. No white. |

## Changed prompts

None. Every shot passed on its first attempt, so no prompt was changed.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

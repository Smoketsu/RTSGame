<!-- rts/docs/art/concept/round25/README.md: results of concept art round 25 (second pass on the Skai HQ redesign, from the developer's notes on round 24), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 25

Developer notes on round 24: option A (great tree) and option B (sky island) were both "Maybe" and to be combined, with the island's turbines becoming feet in some way; option C (perched roc) was "Maybe" but the bird must be bulkier, perhaps a different bird; option D (ring spire) was dropped and not drawn. Five images, each showing the same design twice on a light-grey backdrop: left Uprooted (flying), right Rooted (landed), both from the high three-quarter game camera.

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`. References: shots 1 to 3 use `round24/skai_hq_option_a_great_tree.jpg`, `round24/skai_hq_option_b_sky_island.jpg` and `round16/skai_buildings.jpg`; shots 4 and 5 use `round24/skai_hq_option_c_perched_roc.jpg` and `round16/skai_buildings.jpg`. 5 generations in total, no regenerations, no failed requests. No prompt changes.

| Shot id | File | Verdict | What it shows and critique |
| --- | --- | --- | --- |
| skai_hq_island_tree_fan_feet | skai_hq_island_tree_fan_feet.jpg | pass | The rock island carries the great tree: one big charcoal trunk, five feathered canopy platforms with fans, hanging yellow pods, hangar door and ramp, twin turret in the crown. Left: floating, three turbine pods under a tapering rock point, shadow below. Right: three stout jointed arms reach down and out, each ending in a fan pod planted flat with yellow talons around its rim; the rock hovers well above the ground. Trunk dominates; underside kept. Reservation: the arms are not shown tucked in the left drawing (the pods are simply clustered on the underside); the Rooted legs read as tripod legs more than as arms. |
| skai_hq_island_tree_fan_legs | skai_hq_island_tree_fan_legs.jpg | pass | Same island and tree. Left: four turbines on the underside of the rock, tapering point, shadow. Right: four thick jointed legs on big taloned feet, with round fans visible in the upper legs as shoulder vents, rock raised on top. Clear and the strongest base read of the three. Reservation: left drawing shows the fans on the rock underside rather than on visibly folded legs, so the "folded legs" step is only implied. |
| skai_hq_island_tree_root_feet | skai_hq_island_tree_root_feet.jpg | pass (reservation) | Same island and tree. Left: roots spread over the rock's top edge with yellow talon tips, three turbines under the rock, tapering point. Right: roots run down the sides of the rock and grip a low rocky patch with big yellow talons, the rock sitting low between them, turbines still visible. Trunk dominates; most organic of the three. Reservation: the Uprooted roots lie over the top rim rather than curled against the sides, and the Rooted rock is very low, so the turbines nearly touch the ground. |
| skai_hq_bird_owl | skai_hq_bird_owl.jpg | pass (reservation) | A great owl citadel: very broad barrel body about as wide as tall, flat faceted face with two round yellow window-eyes, small beak, hall door with ramp, turret pedestal on the back, short thick legs. Left: wings spread with white-tipped blade feathers, shadow below. Right: perched on a rocky pad, wings folded forward as walls. Clearly bulkier than the round 24 roc and reads as a building. Reservation: the face is an owl face with a small beak, more bird-like than a plain prow, though small and faceted, with no realistic head. |
| skai_hq_bird_condor | skai_hq_bird_condor.jpg | pass (reservation) | A condor citadel: hunched heavy body with raised shoulders, a feather collar, long broad wings, small low head with a hooked beak, hall door and ramp, turret on the back. Left: wings spread, flying, shadow below. Right: perched upright on thick taloned feet, wings folded forward as walls, dome-like silhouette. Heavier than the round 24 roc, and the Rooted drawing is very solid. Reservation: in flight the body is still fairly slim; the head is hooked-beak eagle-like though small, so the developer may want it blunter. |

All five: low-poly matte, charcoal, white feather vanes and electric yellow, no text, no lightning motif, no cultural ornament, no large realistic head. The backdrop renders as a light lavender-grey rather than a neutral grey, as in earlier rounds.

## Deviations from the shot list

None. Option D was not drawn.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

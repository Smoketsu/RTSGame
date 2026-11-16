<!-- rts/docs/art/concept/round13/README.md: results of concept art round 13 (Skai and water HQ units in Uprooted and Rooted states, first building sheets for both factions), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 13

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`. The two HQ shots at the default `medium` quality; the two building sheets at `quality: "high"`. 4 requests in total: each shot once, no regeneration.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_great_aerie_hq | skai_great_aerie_hq.jpg | gpt-image-2.6-sunburst | pass | One round nest-shaped citadel ringed by charcoal and white feather plates with a bird head crest, drawn twice. Left: hovering on four lift engines with yellow thrust panels, plates swept in. Right: landed on four legs, two arc turrets, a lowered ramp, landing pads, small pelican-like workers. Charcoal, white and electric yellow only; faceted and matte; no cultural ornament. Reads as a nest, not a ship. |
| water_grand_shell_hq | water_grand_shell_hq.jpg | gpt-image-2.6-sunburst | pass (with reservation) | One nautilus-like creature on tentacles with a spiral shell, drawn twice, in sea-green, rose-coral bands and turquoise, no white. Right: waterfalls from body vents into a turquoise pool, a twin-barrel turret raised, an open docking bay, octopus workers. Reservation: the left (Uprooted) drawing does not show the shell visibly closed and sealed (the same dome row and spires are lit and exposed), so the two states differ mainly by water and the opened bay; the bands are closer to red-coral than dusky rose. |
| skai_buildings | skai_buildings.jpg | gpt-image-2.6-sunburst | pass | Seven separate buildings counted (four top, three bottom): crane depot with open bay, pod-tree roost, muster deck platform with stairs, sky dock tower with two pads and a parked flyer, ringed spire with lightning zigzag, twin-emitter turret, feathered wall segment. All different, tall and pointed on thin struts, charcoal, white and yellow, matte low-poly. |
| water_buildings | water_buildings.jpg | gpt-image-2.6-sunburst | pass | Seven separate buildings counted (four top, three bottom): open clam depot with ramp, three-shell cluster, ring-shaped brood pool, tide well with a spiral shell and four water spouts, domed workshop with coral antennae, dome turret with one raised siphon cannon, coral wall. All low and rounded in green, coral and turquoise, no white. Clearly different from the Skai sheet by silhouette (domes against spires). |

## Changed prompts

None. Every shot passed on its first attempt, so no prompt was changed.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

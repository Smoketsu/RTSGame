<!-- rts/docs/art/concept/round16/README.md: results of concept art round 16 (combined conifer and canopy Skai building sheet, Great Aerie hovering on propellers, sturdier water turret), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 16

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`, all at `quality: "high"`. 3 requests in total: each shot once, no regenerations.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_buildings | skai_buildings.jpg | gpt-image-2.6-sunburst | pass | Seven buildings counted (four top, three bottom). Canopy style: crane depot (top left), supply roost (top second), defence turret (bottom middle, flat platform on a forked trunk with two yellow emitter arms). Conifer style: muster deck (top third), sky dock with two hexagon pads (top right), storm spire with ring and bolt (bottom left), wall of five pines on a barrier (bottom right). Charcoal, white, electric yellow; talon roots on all; no red, gold or teal; no cultural ornaments. Reservation: the turret's emitters are chunky slabs rather than clear emitter tips. |
| skai_great_aerie_hq | skai_great_aerie_hq.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Same citadel twice, no eagle head, rim vanes all sweep one way in both drawings, matte low-poly. Hovering drawing (seen from below): four large ducted rotors with yellow rims and motion-blur discs, folded talons visible between them, no thrust panels. Landed drawing: four taloned legs, ramp, two turrets, three pelican workers, propellers with distinct blades visible under the body. Reservation: in the landed drawing the rotors sit on the lower side of the hull rather than flat underneath, so they read as side vents; they are stopped. |
| water_buildings | water_buildings.jpg | gpt-image-2.6-sunburst | pass | Seven buildings counted (four top, three bottom), same positions as round 15. Turret is now a ring of thick rigid spikes round a heavy green armoured mount with a long straight angled cannon and two turquoise sensor eyes; nothing droops. Coral reads dusty rose-pink, at the warm end but not red. No white. |

## Changed prompts

None. All three shots passed first time; `shots.json` is unchanged.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

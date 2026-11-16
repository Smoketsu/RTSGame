<!-- rts/docs/art/concept/round18/README.md: results of concept art round 18 (water Tidecaller and Leviathan, earth roster, Titan HQ, earth buildings, low-poly 1v1 map), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 18

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`, `quality: "high"`. 6 requests in total: one attempt per shot, then one regeneration of the map with a changed prompt.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| water_tidecaller_leviathan | water_tidecaller_leviathan.jpg | gpt-image-2.6-sunburst | pass | Two units. Jellyfish Tidecaller with dome bell, turquoise rim panels, tentacles and a flat turquoise wave; a much larger king-crab Leviathan with two long hydro-cannons on its back, claws and eye stalks. Faceted, matte, no white. Reservation: the "rose-pink" bands read as brick red-brown, close to red, which is reserved for fire; the reference's bands are slightly pinker. The Tidecaller is about a third the width of the Leviathan, as asked. |
| earth_roster | earth_roster.jpg | gpt-image-2.6-sunburst | pass | Four units, left to right: worker with two floating arms holding blue ore, tall spire with a crystal lance and ring of prisms, arch with a translucent hexagonal amber shield, four-legged fortress with three crystal cannons. Hexagons clearly visible, same cream, lapis and amber look as the round 10 line-up. Reservation: the sheet is slightly cleaner and smoother than the round 10 reference. |
| earth_titan_hq | earth_titan_hq.jpg | gpt-image-2.6-sunburst | pass | Walking Titan on the left; on the right a settled Titan with a ring wall of raised hexagonal columns, a spread ring of floating prisms, a raised amber emitter, an open bay with tiny workers. Reservation: the Rooted body is crouched and wider, and the left Titan's fist is a multi-barrel hex cannon, so it reads as the same design only loosely; the left arms are not fully lowered. |
| earth_buildings | earth_buildings.jpg | gpt-image-2.6-sunburst | pass | Seven buildings counted (four on top, three below), each on its own hexagon-tiled patch: drop-off hub with bay and floating ore, column cluster, forge ring with amber pit, shaping structure with floating crystal between two pylons, monolith vault, gravity spire with orbiting rings, column wall. Clearly different silhouettes, same earth look. |
| map_full_1v1 | map_full_1v1.jpg | gpt-image-2.6-sunburst | pass (attempt 2) | Mirror symmetric left to right, with bare base plateaus at the far left and right, flank crystal sites, flat and deep turquoise water, ramps between three layers, the Core with a gold sphere in the centre, no units or buildings. Reservations: the bases and expansion shelves are not strongly separated by chokes, the ramps look like thin bridges, and the top and bottom halves are also near-mirrored. |

Counts: 2 units in the water sheet; 4 units in the earth roster; 1 construct drawn twice in the Titan sheet (plus tiny workers); 7 buildings in the building sheet.

## Changed prompts

map_full_1v1, attempt 1: the two base plateaus sat in the upper corners and carried small glowing spires and ring markings, so the bases were unclear and structures appeared. For attempt 2 this sentence was appended to the prompt: "The two main bases are plain large high plateaus centred vertically at the far left and far right edges of the image (not in the corners), each one bare rock with a single ramp, with no spires, no structures and no markings other than a thin glowing line; the map is mirror symmetric left to right but the top and bottom halves differ, so it is clear which two plateaus are the starting bases." The change is recorded in `shots.json`. Attempt 2 passed. No other shot was regenerated.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

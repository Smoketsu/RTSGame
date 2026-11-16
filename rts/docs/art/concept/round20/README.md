<!-- rts/docs/art/concept/round20/README.md: results of concept art round 20 (crabbier Leviathan, forward siege cannons, Titan gun arm and symmetric base, spiked earth wall, more map ramps), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 20

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`, `quality: "high"`. 5 requests in total: one attempt per shot, no regenerations.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| water_tidecaller_leviathan | water_tidecaller_leviathan.jpg | gpt-image-2.6-sunburst | pass | Same jellyfish and king crab. The wavy scalloped rim is gone: the back is now layered angular plates with a centre seam, a row of short pink spikes along the edge and a bolted turret ring carrying two cannons. Claws still huge and dragging furrows; pink bands unchanged. Reservation: the carapace is still a low dome rather than truly flat-topped. |
| earth_roster | earth_roster.jpg | gpt-image-2.6-sunburst | pass | Four units as before. The beast's three crystal cannons now point forward over its head, the way it faces. The lancer stands upright, feet apart, both hands on a diagonal lance, much more natural. Worker and four-armed guardian unchanged. |
| earth_titan_hq | earth_titan_hq.jpg | gpt-image-2.6-sunburst | pass | Both drawings show the multi-barrel hex gun arm (hanging when walking, across the knee when seated). The rooted base has two mirrored amber emitter pylons and an even ring wall with a central gateway. Reservation: the halo ring is still much larger than the walking Titan's. |
| earth_buildings | earth_buildings.jpg | gpt-image-2.6-sunburst | pass | Seven buildings, six unchanged. The wall now has one small blue crystal point per column, modest and tidy. |
| map_full_1v1 | map_full_1v1.jpg | gpt-image-2.6-sunburst | pass | Dry and grassy, no water; starting plateaus unchanged; glowing lines are gone. Roughly ten wide sloped wedge ramps are visible between levels (up from about eight narrow glowing ones). Reservations: short of a dozen, and a few ramps are hard to tell from ledges. |

Counts: 2 units in the water sheet; 4 units in the earth roster; 1 Titan drawn twice (plus tiny workers); 7 buildings; about 10 ramps on the map.

## Changed prompts

None. Every shot passed on the first attempt.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

## Coordinator correction (16 Nov 2026)

`earth_roster` was regenerated once more after the first check. The first attempt was passed in error: the siege beast's cannons still pointed back over its tail. The prompt in `shots.json` now states the direction by picture side (crystal tips on the left, above the head), and the second attempt has the cannons facing the same way as the head. Total image requests for the round: 6.

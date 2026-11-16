<!-- rts/docs/art/concept/round19/README.md: results of concept art round 19 (Leviathan claws, earth roster as beings, meditating Titan HQ, redesigned earth buildings, dry grassy 1v1 map), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 19

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`, `quality: "high"`. 5 requests in total: one attempt per shot, no regenerations.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| water_tidecaller_leviathan | water_tidecaller_leviathan.jpg | gpt-image-2.6-sunburst | pass | Same jellyfish and king crab as round 18. The two claws are now huge (each about as big as the carapace), rest on the ground and leave two gouged furrows. Bands are dusty mauve-pink like the round 8 line-up, not red or brown. Faceted and matte. |
| earth_roster | earth_roster.jpg | gpt-image-2.6-sunburst | pass | Four living beings: the same worker; a lean lancer leaning back and aiming a crystal lance at the sky; a hunched four-armed guardian holding an amber hex shield with a core at its chest; a four-legged rhino-like beast with a head, amber eye slit and three crystal cannons on its back. All have heads and faces; none reads as a structure. Reservation: the beast's spiky back is busy and the guardian's head is partly hidden behind the shield. |
| earth_titan_hq | earth_titan_hq.jpg | gpt-image-2.6-sunburst | pass | Walking Titan on the left, the same Titan seated cross-legged on the right with hands on knees, inside a ring wall of hex columns with a stepped platform, an amber emitter pylon and a gateway with tiny workers. Same shoulders, chest core and crystal crest in both; no gun hands. Reservations: the head is a crest of columns and crystal, not a clear face; the right halo is a much wider ring than the left; the seated Titan is wider. |
| earth_buildings | earth_buildings.jpg | gpt-image-2.6-sunburst | pass | Seven buildings counted (four on top, three below). Top left to right: drop-off hub, supply rack of crystal cells, production gateway arch with a ramp and a worker stepping out, shaping structure. Bottom: tech vault, gravity spire (stout arch tower, downward amber dish, boulder floating in rings), plain straight column wall with blue cap stones. Each job is readable. Reservation: the wall's caps are slightly scalloped rather than perfectly flat. |
| map_full_1v1 | map_full_1v1.jpg | gpt-image-2.6-sunburst | pass | No water anywhere. Green grass low ground, paler tan middle level, grey rock high level, told apart by colour. Mirror symmetric, bases on large rock plateaus at the far left and right, crystal sites at the corners and sides, the Core with a gold sphere in the centre, no units or buildings. Reservations: the ramps are narrow glowing strips rather than wide roads, the chokes are weak, and the bases are bare plateaus. |

Counts: 2 units in the water sheet; 4 units in the earth roster; 1 Titan drawn twice (plus tiny workers); 7 buildings in the building sheet.

## Changed prompts

None. Every shot passed on the first attempt.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

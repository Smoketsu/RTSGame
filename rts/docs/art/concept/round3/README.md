<!-- rts/docs/art/concept/round3/README.md: results of concept art round 3 (revisions from the developer's notes on round 2), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 3

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size from `shots.json` (1536x1024). 7 requests in total, one per shot, no regenerations. All prompts are exactly as in `shots.json` (with the `style` text prepended).

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_ground_scheme_a_dawn | skai_ground_scheme_a_dawn.jpg | gpt-image-2.6-sunburst | pass | Ivory, gold and cyan. Blade crests, swept fins, triangular hard-light wings and thruster legs give four clearly different outlines, much sharper than round 2. No red. Lavender feather tips are the only extra colour. |
| skai_ground_scheme_b_stormfront | skai_ground_scheme_b_stormfront.jpg | gpt-image-2.6-sunburst | pass | Charcoal, white feather tips and electric yellow. Clearly a different scheme from A and C on the same four units; spiky and angular, the yellow glow reads strongly as a team colour. No red. |
| skai_ground_scheme_c_zenith | skai_ground_scheme_c_zenith.jpg | gpt-image-2.6-sunburst | pass | Silver, indigo and magenta-violet. Same four units, sharp silhouettes; the most saturated team colour of the three. Magenta is close to the pink-red range on some glow, but reads as violet. |
| skai_air_units | skai_air_units.jpg | gpt-image-2.6-sunburst | pass | Raptor flyer with forward-swept blade wings and energy lances, and a serrated feather-edged flying wing with plasma bay and lightning. Angular, no red. Plates are still a little stone-like, and the scale of the two is not comparable (side view sheet). |
| water_lineup | water_lineup.jpg | gpt-image-2.6-sunburst | pass | Rounds, domes and spirals kept, but narrowed glowing eyes, hooked beaks, claws and barbed tentacles make them read as predators, not cute. The worker is still the least threatening. |
| earth_mood | earth_mood.jpg | gpt-image-2.6-sunburst | pass | Hexagons are visible in torso plates, column arms, legs and the raised column field. Sentinel, bruiser and titan have different silhouettes. Still reads somewhat as stone golem rather than machine. |
| map_full_1v1 | map_full_1v1.jpg | gpt-image-2.6-sunburst | pass | Whole map in frame: two base plateaus left and right, expansions, flank crystal sites top and bottom, flooded basins, ramps and the Core sphere in the centre. Far wider than round 2. Weak points: the layout is symmetric in four directions, not only left-right, and the terrain is still mostly natural rock with glowing seams. |

## Changed prompt

None. Every shot passed on the first generation.

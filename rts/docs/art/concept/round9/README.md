<!-- rts/docs/art/concept/round9/README.md: results of concept art round 9 (answers to the developer's notes on round 8), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 9

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size 1536x1024, edit endpoint with `references` (the low-poly line-up `round8/water_lineup_style_c_lowpoly.jpg` is in every shot). 4 requests in total: each shot once, then the Skai ground line-up once more.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_ground_lineup | skai_ground_lineup.jpg | gpt-image-2.6-sunburst | pass (second attempt, with reservation) | Four units in order: pelican worker (beak, pouch, visor, plasma cutter, two claws with crates), secretary-bird scout with yellow foot blades, heron trooper with a very long rifle, augmented owl with trident staff and twin side rotors. Charcoal, white and yellow only; no red, gold or cyan. Matte and mostly faceted, but the feathers and faces carry more fine detail than the low-poly reference, so it is not fully as chunky. |
| skai_air_units | skai_air_units.jpg | gpt-image-2.6-sunburst | pass | Clean low-poly, matte, matches the reference style. Roc Rider in side view with the wings joined ahead of the rider, one tail, energy lances; Thunderhead top-down, nose up, feathers only on the rear edge, flat yellow lightning. Missiles are not shown (not asked for in this shot). |
| water_snail_poses | water_snail_poses.jpg | gpt-image-2.6-sunburst | pass | Same snail and same tall conical spiral shell in both poses, as in the line-up. Left: shell upright, tip up. Right: shell tipped forward horizontal, round opening as the muzzle firing flat dark ink. Rose-coral hinge collar and two sea-green struts, no bare steel. In the travelling pose the shell opening is hidden by the body, so "opening facing down" is not visible. |

## Changed prompts

- `skai_ground_lineup`: the first attempt matched the units but looked too detailed and painted (barbed feathers, soft shading). The prompt now asks for very few large flat triangular facets, each feather a single flat wedge with no barbs or fine texture, and smooth matte fills with no gradients or highlights. The new text is in `shots.json`. The second result is flatter but still finer than the reference.

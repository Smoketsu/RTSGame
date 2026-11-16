<!-- rts/docs/art/concept/round11/README.md: results of concept art round 11 (snail with larger cannon and reticles, three takes on the battle), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 11

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size 1536x1024, edit endpoint with `references`. 6 requests in total: each shot once, then battle takes B and C once more.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| water_snail_poses | water_snail_poses.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Same snail twice; brace arm and pivot disc on the outside of the shell in the same place in both poses. Shell is far larger and taller than in round 10 (towering siege gun, coral muzzle ring, vents, turquoise panels), the same in both. Both eye stalks end in a turquoise crosshair reticle. Left upright, right horizontal and firing ink. Reservation: the shell has smoother, more rendered shading than the low-poly reference, and the ink blast is a flat 2D splash. |
| battle_take_a_ingame | battle_take_a_ingame.jpg | gpt-image-2.6-sunburst | pass (with reservation) | High top-down, Skai on pale rock at left, water army standing in shallow water at right, three clams under domes, mantis shrimp, cuttlefish, three snails, Thunderhead and three Roc Riders overhead, health bars, clean simple terrain. Reservation: about 50 units rather than 60; the Skai side includes six pelican workers not asked for; the domes are translucent. |
| battle_take_b_cinematic | battle_take_b_cinematic.jpg | gpt-image-2.6-sunburst | pass (second attempt, with reservation) | Low camera, three large heron troopers firing right, owl with trident, Thunderhead and Roc Rider overhead, clam under dome, mantis shrimp, snail firing ink. Flat pale sky and plain rocks now. Reservation: units are finer and more rendered than the low-poly reference; the heron's rifle is long but sits a little short of the brief's "very long". |
| battle_take_c_wide_map | battle_take_c_wide_map.jpg | gpt-image-2.6-sunburst | pass (second attempt, with reservation) | Very high view of a whole map: Skai base left, water base right, glowing Core in the centre, flood water in the basins, several small separate fights (raid at the top, domed push at the bottom, Core skirmish), tiny units. Terrain is now plain grey low-poly facets. Reservation: the flood water reads as flat turquoise basins rather than visibly spreading from the water base. |

The three takes are clearly different views: a near-top-down army screenshot, a low cinematic close-up, and a whole-map overview.

## Changed prompts

- `battle_take_b_cinematic`: the first attempt had a painted, cloudy sky and painted rock texture. The last sentence now asks for rocks of a few large flat facets and a plain flat pale-grey sky with no clouds or painted texture. The new text is in `shots.json`.
- `battle_take_c_wide_map`: the first attempt was painted and cluttered (trees, crystals, grass, floating rocks, clouds, mist). The terrain sentence now asks for very simple low-poly terrain of large flat pale-grey facets with no trees, plants, crystals, grass, floating rocks, clouds, mist or painted detail, and a plain flat background. The new text is in `shots.json`.

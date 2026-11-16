<!-- rts/docs/art/concept/round7/README.md: results of concept art round 7 (answers to the developer's notes on round 6), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 7

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size from `shots.json` (1536x1024). Every shot has `references`, so all went to the image edit endpoint (`/v1/images/edits`). 4 requests in total: one per shot, no regenerations. Prompts are as in `shots.json` (with the `style` text prepended); no prompt was changed.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_thunderhead | skai_thunderhead.jpg | gpt-image-2.6-sunburst | pass | Top view unchanged in substance: nose up, white feathers only on the rear edge of both wings. Front view is head-on and level with no feathers visible; the missiles are a row of five round yellow-tipped pods under each wing, read as nose cones facing the viewer, not angled down. Charcoal, white, yellow; no red, gold, cyan. |
| skai_worker_species_options | skai_worker_species_options.jpg | gpt-image-2.6-sunburst | pass | Penguin, puffin, pelican, raven, each with the same plasma cutter arm, visor, and backpack with two cargo grabber claws holding crates. Species clearly differ. No red. |
| water_lineup | water_lineup.jpg | gpt-image-2.6-sunburst | pass | Octopus, clam, mantis shrimp, cuttlefish with four-barrel head gun, sea snail. Matte rusty verdigris surfaces, clearly less shiny than round 6. Clam has no small shield disc and sits on treads, with the big dome kept. Snail shell is upright. |
| water_snail_poses | water_snail_poses.jpg | gpt-image-2.6-sunburst | pass | Same snail twice, side view, matte patina. Left: shell upright, normal snail position. Right: shell swung forward on a pivot mount, opening as muzzle, firing dark ink-plasma. |

## Changed prompts

None.

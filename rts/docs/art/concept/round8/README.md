<!-- rts/docs/art/concept/round8/README.md: results of concept art round 8 (answers to the developer's notes on round 7), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 8

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size 1536x1024. 5 requests in total: the three line-up styles and the snail sheet once each (edit endpoint, with `references`), then the snail sheet once more through the generation endpoint with no `references`. The three line-up shots are judged against their own rendering treatment, not the brief's "hand-painted 3D look" line, because the developer found the round 7 line-up too shiny.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| water_lineup_style_a_flat | water_lineup_style_a_flat.jpg | gpt-image-2.6-sunburst | pass | Flat cel-shaded, dark outlines, no gradients or gloss. Same five units and dome. Clearly less AI-rendered than round 7. The ink-like blade shapes of the cuttlefish fire are glowy teal but flat. |
| water_lineup_style_b_gouache | water_lineup_style_b_gouache.jpg | gpt-image-2.6-sunburst | pass | Matte chalky painted look on a paper-like ground, dome and spray as soft brush strokes. No gloss or bloom. Weakest of the three on the "less detail" point: the surfaces still carry fine scale texture, and it reads closer to pixel-painted than loose gouache. |
| water_lineup_style_c_lowpoly | water_lineup_style_c_lowpoly.jpg | gpt-image-2.6-sunburst | pass | Faceted low-poly, flat matte shading, no specular or glow. Same five units. Reads as game models; the most useful of the three for the 3D pipeline. |
| water_snail_poses | water_snail_poses.jpg | gpt-image-2.6-sunburst | pass (second attempt) | Same snail, same round shell in both drawings, never a tube. Armoured cradle, front hinge pin and two hydraulic pistons clearly visible; retracted on the left, extended and tipping the shell forward on the right, with the opening firing ink-plasma. Remaining weakness: the shell is more ball-like than a long spiral snail shell, and in the travelling pose the opening faces forward rather than down and back. |

## Changed prompts

- `water_snail_poses`: the first attempt (with the round 7 image as `references`) failed. The right-hand shell was again stretched into a long tube and the left shell differed from it. The reference was removed (the shot now uses the generation endpoint) and the prompt was rewritten to describe the shell in words as a short, fat, rigid, round spiral that never changes shape, to name the mechanism (cradle, front hinge pin, two silver pistons under the shell), and to tilt the shell about 60 degrees instead of 90. The new text is in `shots.json`.

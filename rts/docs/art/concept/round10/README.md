<!-- rts/docs/art/concept/round10/README.md: results of concept art round 10 (snail braces, earth in low-poly, Skai raid battle mock), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 10

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size 1536x1024, edit endpoint with `references`. 4 requests in total: each shot once, then the battle mock once more.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| water_snail_poses | water_snail_poses.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Same snail and same conical shell twice. The brace arm stands outside the shell with a pivot disc on the outer wall, nowhere inside the shell or its opening; base and arm are in the same place in both poses. Left: shell upright. Right: shell horizontal, opening forward, firing dark ink. Reservation: in side view only the near arm is visible; the far arm is hidden, so "two arms" is not shown. The shell in the firing pose is a little stubbier than in the upright pose. |
| earth_lineup | earth_lineup.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Sentinel, bruiser and titan with hexagonal columns, ring of prisms, cream stone, lapis plates, blue prisms, amber panels; hexagons clear; matte. Reservation: faceting is finer and more render-like than the low-poly reference. |
| battle_skai_raid_low_tide | battle_skai_raid_low_tide.jpg | gpt-image-2.6-sunburst | pass (second attempt, with reservation) | Skai on the left (herons, secretary-birds, owl with trident, Roc Rider, Thunderhead) against the sea-green and coral water base on the right (tracked clam under a turquoise dome, mantis shrimp, two cuttlefish with head barrels firing arcs at the flyers, snail firing ink, an extra octopus). Wet mud, shallow water, no UI, no red, no cultural ornaments. Reservation: terrain and units are finer and more painted than the low-poly reference. |

## Changed prompts

- `battle_skai_raid_low_tide`: in the first attempt the cuttlefish were not identifiable and the water jets appeared to start at the flyers. The prompt now asks for two clearly visible cuttlefish at the dome edge with a cluster of gun barrels on the head, jets starting at their barrels and arcing upward to the flyers. The new text is in `shots.json`.

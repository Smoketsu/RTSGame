<!-- rts/docs/art/concept/round12/README.md: results of concept art round 12 (snail with lobbing shot and scope lens, two in-game engagement mock-ups), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 12

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint with `references`. `water_snail_poses` at the default `medium` quality; the two in-game shots at `quality: "high"` (accepted by the API, no fallback). 3 requests in total: each shot once, no regeneration.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| water_snail_poses | water_snail_poses.jpg | gpt-image-2.6-sunburst | pass | Same snail twice; large cannon and outside brace with pivot disc kept. Firing pose: cannon rotated about 45 degrees upward, muzzle to the upper left, a turquoise low-poly ball on a curved lobbing trail with flat droplets, no ink. Eyes are plain; one eye stalk on a jointed arm holds a round scope lens with a crosshair, the other has none. The lens appears in both poses. |
| ingame_engagement_choke | ingame_engagement_choke.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Reads as a real RTS screenshot: tilted three-quarter view at play zoom, ramp between cliffs, waterfall and shoreline, melee clump at the contact point under the clam dome, herons in a loose arc firing yellow bolts with a dozen green selection rings, owl lightning, flyers dropping bombs, snail shells bursting, wrecks, health bars, HUD with minimap, portrait grid, command buttons and resource counters (digits and icons only). Reservation: the water army stands in shallow water in a fairly neat cluster at the left and the Skai reinforcements are evenly spaced up the ramp, so it still looks a little staged; the unit count is about 45, and the style is more painted than strict low-poly. |
| ingame_raid_expansion | ingame_raid_expansion.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Tilted view of a flooded water base: spiral-shell drop-off, domes, a turret cluster, crystals, workers fleeing, secretary-bird scouts with selection rings, Roc Riders diving, a burning wreck, a strung-out stream of mantis shrimp from the right, tracked clam with dome, fog corner, ramp, full HUD with a red ping on the minimap. Reservation: units are spread evenly across a flat water plain with large gaps, so it looks more staged than the choke shot; only three or four scouts are visible, not eight, and the HUD centre panel is half empty. |

## Changed prompts

None. Every shot passed on its first attempt, so no prompt was changed.

## Tool changes

`rts/tools/ConceptArt/generate.py` now lets a shot's `quality` field override the default `QUALITY` on both the generation and edit paths; `TIMEOUT` was raised from 300 to 600 seconds. The README of the tool has one added sentence.

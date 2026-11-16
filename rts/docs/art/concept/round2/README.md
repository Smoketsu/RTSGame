<!-- rts/docs/art/concept/round2/README.md: results of concept art round 2 (science fiction direction), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 2

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size from `shots.json` (1536x1024). 9 requests in total: one per shot plus one regeneration of `map_core_flooded`. All prompts are exactly as in `shots.json` (with the `style` text prepended) except the changed map prompt below.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_ground_lineup | skai_ground_lineup.jpg | gpt-image-2.6-sunburst | pass | Four distinct avian silhouettes with exosuit, thruster scout, glider-wing rifleman and storm emitter; red panels strong; leaf-shaped chest insignia is invented; no headdress, no scrolls. Red head crests are natural plumage. |
| skai_air_units | skai_air_units.jpg | gpt-image-2.6-sunburst | pass | Mechanical raptor with energy lances and a bird-faced flying-wing bomber with plasma bomb bay clearly sci-fi; red wing blocks are bold. Not a scene shot, so camera is a side view. |
| skai_great_aerie_hq | skai_great_aerie_hq.jpg | gpt-image-2.6-sunburst | pass | Reads as a hovering feather-plated nest on four teal lift engines, high three-quarter camera, pads with flyers. Weakest sci-fi point: the plates and deck look like carved stone and the gold bands look woven; red foliage at lower left. |
| water_lineup | water_lineup.jpg | gpt-image-2.6-sunburst | pass | Five low rounded units in order, pressure suit, shield emitter, augmented tentacles, siphon cannon, ink-plasma tank; clearly different from Skai. |
| water_grand_shell_hq | water_grand_shell_hq.jpg | gpt-image-2.6-sunburst | pass | Armoured nautilus with reactor domes, docking bays with craft, water pouring from vents; camera a little lower than a true top-down. |
| map_core_flooded | map_core_flooded.jpg | gpt-image-2.6-sunburst | pass (after one regeneration) | Three levels, ramps, flooded basin, Core sphere with pylons and glowing conduits. Still the least sci-fi image: the machinery is stone-coloured and the terrain is natural rock. |
| battle_skai_raid_low_tide | battle_skai_raid_low_tide.jpg | gpt-image-2.6-sunburst | pass | Red troopers, hover vehicles (not requested) and raptors against blue walkers and shell domes; clear team split and good tech. Some stone rims round the pools look low-tech. |
| earth_mood | earth_mood.jpg | gpt-image-2.6-sunburst | pass | Floating sentinel, bruiser, large titan raising hex columns; amber energy and green inlays. Reads more as magic stone golems than sci-fi machines. |

## Changed prompt

`map_core_flooded` first attempt read as natural rock with faint glowing cracks and a stone ring, too little technology. Regenerated once with the Core sentence replaced by the following (the rest of the prompt is unchanged, and `shots.json` now holds this text). Full prompt (style text prepended as for every shot):

> Environment concept for a science fiction strategy game map on an alien planet, seen from a high three-quarter top-down camera. Rocky terrain with three clear elevation layers joined by ramps: high plateaus, a middle shelf and a low basin. The low basin is partly flooded with clear shallow water spreading from one side, deeper and darker in the middle of the flood. At the centre of the map on the middle shelf sits the Core: an ancient exposed planetary reactor, a ring of half-buried high-tech alien machinery made of smooth metallic alloy plates, power conduits, emitter pylons and containment rings, around a pulsing sphere of white-gold energy, with bright glowing conduit lines running out across the ground and along the ramps. Add a few buried metal alien machine panels with glowing seams in the terrain. Alien vegetation in muted grey-green and pale lilac only, nothing red or blue. Narrow chokes and cliffs are easy to read. No units, no buildings.

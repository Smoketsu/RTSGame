<!-- rts/docs/art/concept/round5/README.md: results of concept art round 5 (answers to the developer's notes on round 4), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 5

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size from `shots.json` (1536x1024). Every shot has `references`, so all went to the image edit endpoint (`/v1/images/edits`). 8 requests in total, one per shot, no regenerations. All prompts are exactly as in `shots.json` (with the `style` text prepended).

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_ground_lineup | skai_ground_lineup.jpg | gpt-image-2.6-sunburst | pass | Four units: raven worker, secretary-bird scout with glowing yellow energy blades on both feet, heron trooper, great horned owl storm-caller. The rifle is now very long and heavy (roughly the trooper's own height, far longer than round 4). Charcoal, white, yellow; no red, no cultural ornament. The trooper gained a large yellow angular fin on its back that was not asked for. |
| skai_owl_staff_backpack_options | skai_owl_staff_backpack_options.jpg | gpt-image-2.6-sunburst | pass | The same owl four times with four clearly different staffs (tuning fork, gyroscope ring, trident, orb sceptre) and backpacks (twin capacitors, antenna fan, side rotors, coil with storm cloud). No red, no cultural ornament. |
| skai_air_units | skai_air_units.jpg | gpt-image-2.6-sunburst | pass | The raptor rider has one pair of wings and a tail; the stray rear wing is gone. Bomber feather blades hang along the trailing edge, none on the leading edge; the beak-like prow points forward. Weakness: the raptor faces right while the bomber faces left, although the prompt asked for the same direction; the bomber's left wing shows no feathers. |
| earth_marble_a_white | earth_marble_a_white.jpg | gpt-image-2.6-sunburst | pass | Same three constructs. Bright white veined marble, amber glow, emerald prisms; reads clearly as white. Gold seams are thin. |
| earth_marble_b_honey | earth_marble_b_honey.jpg | gpt-image-2.6-sunburst | pass | Same three constructs. Honey and caramel banded onyx with turquoise prisms; the warmest and most orange, the stone blends somewhat with the amber glow. |
| earth_marble_c_lapis | earth_marble_c_lapis.jpg | gpt-image-2.6-sunburst | pass | Same three constructs. Cream limestone with bold lapis blue plates and sapphire prisms; the most contrast with the amber glow. A few tiny green flecks remain. |
| water_species_option_1 | water_species_option_1.jpg | gpt-image-2.6-sunburst | pass | Octopus worker, nautilus defender with shield, giant squid fighter with hooked tentacles, cuttlefish anti-air with water jet, sea-snail artillery: five visibly different species. Reef colours (sea-green, rose-coral banding, turquoise glow); menacing faces. |
| water_species_option_2 | water_species_option_2.jpg | gpt-image-2.6-sunburst | pass | Hermit crab worker, giant clam defender, mantis shrimp fighter with club claws, vampire squid anti-air, blue-ringed octopus artillery: five clearly different species, different from option 1. Reef colours, menacing. The hermit crab's spiral shell is mostly a dome. |

## Changed prompt

None. Every shot was accepted on the first generation.

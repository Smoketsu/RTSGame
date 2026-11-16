<!-- rts/docs/art/concept/round4/README.md: results of concept art round 4 (options from the developer's notes on round 3), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 4

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size from `shots.json` (1536x1024). Every shot has `references`, so all went to the image edit endpoint (`/v1/images/edits`, multipart field `image[]`, accepted first time). 9 requests in total, one per shot, no regenerations. All prompts are exactly as in `shots.json` (with the `style` text prepended).

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_species_option_1 | skai_species_option_1.jpg | gpt-image-2.6-sunburst | pass | Puffin-like worker, falcon-like scout, heron trooper with long dagger beak, horned owl storm-caller: four clearly different heads and silhouettes. Charcoal, white and yellow, no red, no cultural ornament. The worker's beak is yellow rather than charcoal, which blends into the energy colour. |
| skai_species_option_2 | skai_species_option_2.jpg | gpt-image-2.6-sunburst | pass | Raven worker, crested roadrunner/secretary-bird scout, hornbill trooper with casque beak, vulture storm-caller with neck ruff. Clearly different from option 1 and between units. Same scheme, no red. |
| skai_air_units | skai_air_units.jpg | gpt-image-2.6-sunburst | pass | Same raptor rider and serrated flying wing as round 3, now charcoal with yellow glow and lightning on faceted metal plates. No cyan or red. A little bronze trim on beaks and edges, not enough to read as gold. |
| water_scheme_a_abalone | water_scheme_a_abalone.jpg | gpt-image-2.6-sunburst | pass | Same five designs. Teal-to-violet iridescent shells, indigo bodies, coral-pink lights; no white shells. Coral-pink glow is close to red, but it is the water faction's own glow. |
| water_scheme_b_abyss | water_scheme_b_abyss.jpg | gpt-image-2.6-sunburst | pass | Same five designs. Near-black navy matte shells with aqua-green glow; strongest contrast of the three. The darkest sheet, so detail is lowest at small size. |
| water_scheme_c_reef | water_scheme_c_reef.jpg | gpt-image-2.6-sunburst | pass | Same five designs. Sea-green shells with dusky rose-coral banding and turquoise glow; the lightest and most colourful of the three, clearly different from A and B. |
| earth_stone_a_marble | earth_stone_a_marble.jpg | gpt-image-2.6-sunburst | pass | Same three constructs. Cream marble and travertine with dark veins, amber glow, green prisms. Reads as cream or sandstone more than white marble; gold seams are hard to see. Only slightly more varied than plain grey. |
| earth_stone_b_malachite | earth_stone_b_malachite.jpg | gpt-image-2.6-sunburst | pass | Same three constructs. Banded green malachite with bronze trim and clear quartz prisms; the most distinct and striking option. |
| earth_stone_c_strata | earth_stone_c_strata.jpg | gpt-image-2.6-sunburst | pass | Mixed rock types (basalt, granite, lapis, sandstone, gneiss, amethyst geodes) read as a geology collection. The titan's pose and the halo position drifted from the reference, and there is a small stray mark at the top right edge. Busiest option; the silhouette is harder to read. |

## Changed prompt

None. Every shot passed on the first generation.

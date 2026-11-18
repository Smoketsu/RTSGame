<!-- rts/docs/art/concept/round24/README.md: results of concept art round 24 (four new Skai HQ design options, from the developer's note on the round 17 Great Aerie sheet), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 24

Developer note: "I'm thinking since we're finally at 3d model stage, maybe we change this thing up. Can I get some new designs for an HQ for these guys?" Four options, each one image showing the same design twice on a light-grey backdrop: left Uprooted (flying), right Rooted (landed), both from the high three-quarter game camera.

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`. References for every shot: `round16/skai_buildings.jpg` and `round9/skai_air_units.jpg` (the Great Aerie drawing was not used). 6 successful generations in total (4 first attempts, 2 regenerations), plus 1 request that failed with a connection reset and produced nothing.

| Shot id | File | Result | What it shows and critique |
| --- | --- | --- | --- |
| skai_hq_option_a_great_tree | skai_hq_option_a_great_tree.jpg | pass | A colossal canopy tree: charcoal trunk, five feathered canopy platforms at different heights with ducted rotors under them, hanging yellow pods, a large arched hangar door with ramp, a twin-barrel turret in the crown. Left: roots folded into a tapering clawed point, flying, ground shadow. Right: roots spread with yellow talons on a rocky patch. Same design in both. Reads as a base; silhouette is clearly not a disc. Rotors are visible as fans under the platforms. |
| skai_hq_option_b_sky_island | skai_hq_option_b_sky_island.jpg | pass | Faceted rock island with a conifer tower, canopy trees, hangar, crane, two pads and a twin turret on top. Left: floating, underside tapering to a point with three turbines set into it. Right: settled on four large taloned mechanical legs, reads as a hill fort. Strongest base read of the four; same island in both. Reservation: the turbines are only visible while flying, as intended. |
| skai_hq_option_c_perched_roc | skai_hq_option_c_perched_roc.jpg | pass (reservation) | A building shaped as a bird of prey: hall body with a big door, ramp and windows, a turret pedestal on top, white-tipped blade wings. Left: wings spread, gliding, bird-shaped shadow. Right: upright on two talons, wings folded forward as walls, the hall door in front. Reservation: even after the regeneration the small head still reads as a stylised eagle head; the brief's round 13 note rejected a large eagle head on the Great Aerie, so the developer may want it removed or turned into a plain prow. |
| skai_hq_option_d_ring_spire | skai_hq_option_d_ring_spire.jpg | pass | A tall conifer spire with three feathered ring platforms, a yellow crystal emitter with a small ring at the tip. Left: rings spinning with motion blur, spire tapering to a point below, talon foot retracted. Right: one wide talon foot, rings lowered as landing decks. Same design in both. Reservation: the Rooted drawing looks like a tall tiered tree and the turret/defensive weapon is not visible; it is the least base-like of the four. |

All four: low-poly matte, charcoal, white feather vanes and electric yellow, no text, no cultural ornament, all silhouettes differ from the Great Aerie disc and from each other. No deviation from the shot list except the wording changes below.

## Changed prompts

- Option C: attempt 1 had a large, realistic eagle head with a beak and golden eye on a long neck, which made it read as a creature. The sentence "with no feathers or eyes drawn naturalistically" became "with no naturalistic feathers or eyes: the head is only a small faceted charcoal prow with a blunt hooked beak shape and one yellow slit window, clearly secondary to the large building body." The head is smaller and sits lower, but is still bird-like.
- Option D: attempt 1 put a yellow lightning-bolt symbol on the tip, which is an icon (the lightning style was dropped in round 15). "a storm emitter at the tip" became "a storm emitter at the tip built as a tall angular yellow crystal antenna with a small floating ring around it (no lightning-bolt symbol or icon anywhere)."

Both changes are recorded in `shots.json`. Options A and B passed on the first attempt.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

<!-- rts/docs/art/concept/round6/README.md: results of concept art round 6 (answers to the developer's notes on round 5), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Concept art round 6

Model for every shot: `gpt-image-2.6-sunburst`, `quality: "medium"`, size from `shots.json` (1536x1024). Every shot has `references`, so all went to the image edit endpoint (`/v1/images/edits`). 6 requests in total: one per shot plus one regeneration of `skai_roc_rider`. Prompts are as in `shots.json` (with the `style` text prepended); `skai_roc_rider` shows its second prompt.

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_thunderhead | skai_thunderhead.jpg | gpt-image-2.6-sunburst | pass | Top-down plan view: nose at the top, white feather blades only on the rear trailing edge of both wings, symmetric; leading edge bare armour. Front view shows missile racks with yellow tips on pylons under both wings. Charcoal, white, yellow; no red, gold, cyan. |
| skai_roc_rider | skai_roc_rider.jpg | gpt-image-2.6-sunburst | pass (second try) | Two wings, roots at the shoulders behind the neck and in front of the saddle, flight feathers on the trailing edges, one tail, rider behind the wing roots, two energy lances. First try failed: the near wing grew from the saddle and hip area. Some brown-bronze trim on the armour and legs. |
| skai_raven_worker_tool_options | skai_raven_worker_tool_options.jpg | gpt-image-2.6-sunburst | pass | Same raven four times: drill and canister; plasma cutter with visor and tank; two cargo claws carrying crates; fabricator arm with hologram blueprint and survey drone. Clearly different. No red. |
| skai_owl_storm_caller | skai_owl_storm_caller.jpg | gpt-image-2.6-sunburst | pass | Front and back views with trident staff and twin side-rotor backpack. Visibly more augmented than round 5: targeting visor, cybernetic arm with conduits, plated armour, cables into the staff, antenna tufts, orbiting capacitor nodes. |
| water_lineup | water_lineup.jpg | gpt-image-2.6-sunburst | pass | Five units left to right: octopus worker, giant clam, mantis shrimp, cuttlefish with four-barrel head gun firing jets, sea snail whose whole shell is the cannon with no separate tube. The large dome shield is much bigger than the clam and covers the first four units; the snail stands just outside it. Reef colours. |

## Changed prompt

`skai_roc_rider`, attempt 1 failed (near wing attached at the saddle and hips). In attempt 2 the sentence "The raptor has exactly two wings, and each wing is attached at the shoulder ... the way an eagle's wings join its body." was replaced with: "The raptor has exactly two wings. Both wing roots are at the shoulders, in the FRONT THIRD of the body, right behind the base of the neck and well in FRONT of the rider's saddle; the rider sits on the middle of the back BEHIND the wing roots. The rear half of the body behind the saddle is only a plain tapered body ending in one fan-shaped tail, with no wing, no wing root and no feathers attached to it, like a real eagle in flight." The `references` entry was kept.

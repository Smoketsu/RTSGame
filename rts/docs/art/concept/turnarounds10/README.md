<!-- rts/docs/art/concept/turnarounds10/README.md: result of the tenth set of model sheets, front, side and back sheets of the new Skai HQ (island tree, picked from round 25) in its Rooted and Uprooted states, made as input for 3D generation and checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 10

Model: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`. Style text is the turnarounds9 style text, unchanged. Both shots pass `round25/skai_hq_island_tree_root_feet.jpg` (the developer's pick, no change notes) as the only reference and name the half to use. 2 image requests in total: 2 first attempts, 0 regenerations. PNG originals are kept outside the repository. The design was not changed from the picked drawing.

The style text says the building "stands still on flat ground" and "no ground shadow". The Uprooted shot adds an explicit override in its own prompt (no ground, no rocky patch, no shadow, nothing beneath it); the style text itself is untouched.

| Shot id | Game building | Reference | Drawing used |
| --- | --- | --- | --- |
| skai_great_aerie_rooted_sheet | Skai HQ, Rooted | round25/skai_hq_island_tree_root_feet.jpg | right half (roots down the sides, rock low) |
| skai_great_aerie_uprooted_sheet | Skai HQ, Uprooted | round25/skai_hq_island_tree_root_feet.jpg | left half (flying, tapering underside) |

Approximate x-ranges of the three views (front, side, back) in the 1536-pixel-wide image, for cropping (est.):

| Shot id | Front | Side | Back |
| --- | --- | --- | --- |
| skai_great_aerie_rooted_sheet | 40-530 | 560-985 | 1005-1500 |
| skai_great_aerie_uprooted_sheet | 30-500 | 575-940 | 1010-1500 |

| Shot id | File | Verdict | Description and deviations |
| --- | --- | --- | --- |
| skai_great_aerie_rooted_sheet | skai_great_aerie_rooted_sheet.jpg | pass (reservation) | Three level views, same scale, same colours. Island rock with the great tree, five feathered canopy platforms with fans under them, four hanging yellow pods, hangar door with striped ramp and crates on the front only, twin turret in the crown, three turbines in the rock (three on front and back, one plus an edge visible in the side), thick roots down the rock's sides as four jointed legs with yellow talons on a low rocky patch. Deviations: the rock stands higher on the legs than in the reference, so there is a visible gap under it, and the back view is a plain closed rear with no door, a guess. The side view shows only the hangar's yellow edge stripe. |
| skai_great_aerie_uprooted_sheet | skai_great_aerie_uprooted_sheet.jpg | pass (reservation) | Three level views, same scale, nothing beneath the building, no shadow. Same tree, canopy, pods, hangar and turret as the Rooted sheet; the rock's underside tapers to a point with three turbines (two plus an edge in the side view). Deviation: the roots are not curled in against the sides. As in the reference's left drawing they lie over the rock's top rim with yellow talons pointing out and down, so the "lifted and curled in" read is only implied. The side view tree leans slightly differently from the Rooted side view. |

The two sheets are clearly the same building: same trunk, canopy layout, pod positions, turret, hangar and turbine rock, with the same charcoal, white and yellow colours.

## Details a 3D generator is likely to get wrong

- Hanging pods: thin arms and small pods dangle below the canopy platforms with air around them; expect them to be fused to the platform or merged into the trunk.
- Roots: the many thin root fingers over the rock's rim and the talon tips are thin spikes; expect them to be fused into a lump or to lose the talons. In the Rooted sheet the legs are thick and should survive; the talons are the weak point.
- Uprooted state: the whole building floats on a point with nothing under it, so the pointed tip is thin and the turbines under the rock overhang; the roots over the rim read as spikes, not as curled roots.
- Canopy platforms: layered feather vanes are many thin blades; expect a solid disc instead. The platforms also overhang the trunk and the rock.
- Side view of the tree: several canopy platforms overlap there, which makes the layout hard to read; use the front and back views for placement.
- Turbines: the round fans in the rock and under the canopies are shallow discs with thin blades; expect flat circles. In the Rooted sheet the fans are partly hidden by the legs.
- The two sheets must become one model with two root and base states; a generator will build them as two separate models.

## Deviations from the shot list

None.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

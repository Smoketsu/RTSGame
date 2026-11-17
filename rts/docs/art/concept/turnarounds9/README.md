<!-- rts/docs/art/concept/turnarounds9/README.md: result of the ninth set of model sheets, front, side and back sheets of eight Skai and water base buildings and the two HQs (Great Aerie landed, Grand Shell rooted), made as input for 3D generation and checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 9

Model: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`. Style text is the turnarounds4 model-sheet text with "building" for "unit" and an added rule that all three views are level elevations at the same scale (the back view is a true rear view, not a top view). Each shot passes one approved multi-building or HQ drawing as its only reference and names the one building to use by position. 11 image requests in total: 10 first attempts, 1 regeneration. PNG originals are kept outside the repository.

Mapping from game building to approved drawing (no mapping needed correcting; the round 16 sheets have a drawing for every job):

| Shot id | Game building (placeholder name) | Reference | Drawing used |
| --- | --- | --- | --- |
| skai_perch_depot_sheet | Skai drop-off (Perch Depot) | round16/skai_buildings.jpg | top row, first: canopy crane depot |
| skai_roost_sheet | Skai supply (Roost) | round16/skai_buildings.jpg | top row, second: small canopy roost with hanging pods |
| skai_windspire_sheet | Skai tech (Storm Spire) | round16/skai_buildings.jpg | bottom row, first: conifer with ring and bolt |
| skai_perch_tower_sheet | Skai defence (Arc Turret) | round16/skai_buildings.jpg | bottom row, second: canopy turret with two emitter arms |
| water_pearl_depot_sheet | water drop-off (Pearl Depot) | round16/water_buildings.jpg | top row, first: coral tree over a doorway with stairs |
| water_tidepool_sheet | water flood source (Tide Well) | round16/water_buildings.jpg | top row, fourth: hourglass coral tower with a water rim |
| water_shell_forge_sheet | water tech (Reef Workshop) | round16/water_buildings.jpg | bottom row, first: brain-coral dome with antler crown |
| water_coral_turret_sheet | water defence (Jet Turret) | round16/water_buildings.jpg | bottom row, second: spiked ring and angled cannon |
| skai_great_aerie_sheet | Great Aerie, landed | round17/skai_great_aerie_hq.jpg | right drawing (landed) |
| water_grand_shell_sheet | Grand Shell, rooted | round13/water_grand_shell_hq.jpg | large drawing (rooted) |

Approximate x-ranges of the three views (front, side, back) in the 1536-pixel-wide image, for cropping (est.):

| Shot id | Front | Side | Back |
| --- | --- | --- | --- |
| skai_perch_depot_sheet | 20-545 | 555-980 | 1000-1475 |
| skai_roost_sheet | 75-500 | 600-955 | 1030-1455 |
| skai_windspire_sheet | 65-520 | 550-985 | 1020-1485 |
| skai_perch_tower_sheet | 55-500 | 560-935 | 1040-1495 |
| water_pearl_depot_sheet | 35-520 | 535-985 | 1015-1500 |
| water_tidepool_sheet | 35-510 | 535-1000 | 1030-1500 |
| water_shell_forge_sheet | 20-500 | 545-960 | 1010-1480 |
| water_coral_turret_sheet | 55-460 | 525-980 | 1035-1470 |
| skai_great_aerie_sheet | 20-515 | 500-1010 | 1025-1515 |
| water_grand_shell_sheet | 10-400 | 440-1110 | 1105-1500 |

| Shot id | File | Result | Critique |
| --- | --- | --- | --- |
| skai_perch_depot_sheet | skai_perch_depot_sheet.jpg | pass | Same crane depot: three diamond canopies, cargo arch with ramp and crates, crane with cargo box on the same side in all three views, talon roots; the back view is a plain closed rear. |
| skai_roost_sheet | skai_roost_sheet.jpg | pass | Slender talon-rooted tree, three diamond canopies with hanging yellow-banded pods; side view is a believable profile of the same tree. |
| skai_windspire_sheet | skai_windspire_sheet.jpg | pass (with reservation) | Conifer tiers, yellow stripe, octagonal ring and bolt agree in all views; the ring floats around the upper tree as in the reference, and the side view shows only a flat band. |
| skai_perch_tower_sheet | skai_perch_tower_sheet.jpg | pass | X-trunk, octagonal platform, canopy and two emitter arms agree; the side view shows one arm pointing left, as expected for a forward-pointing pair. |
| water_pearl_depot_sheet | water_pearl_depot_sheet.jpg | pass | Coral canopy tree over a turquoise door with stairs on the front; the side shows the stair rail and the back a closed rear arch. Coral reads dusty pink, no white. |
| water_tidepool_sheet | water_tidepool_sheet.jpg | pass | Hourglass coral tower with turquoise stripes, scalloped rim and water channels at the base; the three views agree and are near-symmetric. |
| water_shell_forge_sheet | water_shell_forge_sheet.jpg | pass | Brain-coral dome with antler crown and polyps; door only on the front, plain back. |
| water_coral_turret_sheet | water_coral_turret_sheet.jpg | pass (with reservation) | Spiked ring, armoured mount and angled cannon agree; the cannon points toward the viewer in the front view and left in the side view. The back view shows the cannon as a stub with its muzzle ring seen end-on above the mount, which is a loose reading of a barrel pointing away. |
| skai_great_aerie_sheet | skai_great_aerie_sheet.jpg | pass (with reservation) | Landed on four taloned legs with ramp, dome, mast, rim vanes all one way, no workers. The four propellers are drawn as vents on the side of the hull (as in the round 17 reference), not flat underneath, and only two turrets and no landing pads show on the deck. |
| water_grand_shell_sheet | water_grand_shell_sheet.jpg | pass (with reservation) | Rooted Grand Shell without workers: head, three domes, spires, twin gun, two waterfalls, two recessed garage decks, spiral shell, eight tentacle legs. The back view shows the shell face-on as a spiral, which the reference never shows, so it is a guess; the shell banding is a salmon coral and reads close to red, as in the reference. |

## Changed prompts

Grand Shell: the first attempt drew the two garage decks as wide flat shelves sticking out sideways in the front and back views, while the side view had them recessed. Appended before the backdrop text: "RETRY NOTES: in the previous attempt the FRONT and BACK views showed the garage decks as wide flat shelves sticking out sideways from the body. They must not: the two garage decks are recessed openings cut into the shell's flank, flush with the body, so in the FRONT and BACK views the silhouette has NO shelves or platforms sticking out sideways; ..." `shots.json` holds the final prompt.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

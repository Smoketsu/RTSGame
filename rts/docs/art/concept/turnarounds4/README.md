<!-- rts/docs/art/concept/turnarounds4/README.md: results of the fourth set of model sheets, a revision of five sheets after the second developer review of 16 Nov 2026 (front, side, back; front, side, bottom for the Thunderhead), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 4

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`, same style text as turnarounds3. Each shot passes the previous sheet of the unit as reference (Marksman, Pummeler, Water Worker: turnarounds2; Roc Rider, Thunderhead: turnarounds3), plus `round9/skai_air_units.jpg` for both air units and `round7/skai_thunderhead.jpg` for the Thunderhead. 6 image requests in total: 5 first attempts, then 1 regeneration (Roc Rider). PNG originals are kept outside the repository.

Approximate x-ranges of the three views (front, side, back; for the Thunderhead front, side, bottom) in the 1536-pixel-wide image, for cropping (est. where views touch):

| Shot id | Front | Side | Back / bottom |
| --- | --- | --- | --- |
| skai_marksman_sheet | 100-345 | 375-1035 (rifle included) | 1185-1440 |
| skai_thunderhead_sheet | 30-500 | 525-975 | 1005-1500 (bottom) |
| water_pummeler_sheet | 30-535 | 545-960 | 1000-1500 |
| water_worker_sheet | 150-430 | 590-935 | 1010-1395 |
| skai_roc_rider_sheet | 10-570 (est.) | 560-975 | 1000-1510 |

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_marksman_sheet | skai_marksman_sheet.jpg | gpt-image-2.6-sunburst | pass | Shouldered rifle: grip hand near the body, support hand on the forward handguard, barrel level along the beak direction (at the viewer in front, left in side). Back view shows both arms raised, rifle hidden behind the body. |
| skai_thunderhead_sheet | skai_thunderhead_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Bird head with hooked beak and yellow eyes at the nose in front and side views, beak at the nose tip in the bottom view; pods and guns kept. Reservation: the head is small and the bottom view is a little larger than the others. |
| water_pummeler_sheet | water_pummeler_sheet.jpg | gpt-image-2.6-sunburst | pass | Thick upper arms and club fists about torso size hanging low in front; coral kept as in turnarounds2 (still a strong red-coral). |
| water_worker_sheet | water_worker_sheet.jpg | gpt-image-2.6-sunburst | pass | Opaque sea-green head with raised turquoise eyes, brow ridges, mouth and siphon in the front and side views inside a solid coral ring; nothing transparent. |
| skai_roc_rider_sheet | skai_roc_rider_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Raptor much bigger than the small rider, legs tucked with curled talons, three yellow crystals under each wing visible in all views, vents and metal-blade feathers kept. Reservation: the side view wings sweep slightly down, not perfectly level. |

## Changed prompts

Appended before the backdrop text for the regenerated Roc Rider sheet.

Roc Rider: "RETRY NOTES: in the previous attempt the raptor was still small and the views occupied only half of the sheet height. Now: scale everything up so each view fills at least 80 percent of the sheet height and the three views nearly touch: the RAPTOR is a huge machine, its body is about four times the rider's height and its wingspan dominates, the rider is a small figure on its back (about one quarter of the raptor's body length). Keep the tucked legs with curled talons and the yellow crystals under both wings." `shots.json` holds the final prompts.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

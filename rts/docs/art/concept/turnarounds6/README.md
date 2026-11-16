<!-- rts/docs/art/concept/turnarounds6/README.md: results of the sixth set of model sheets, a redraw of two sheets (Marksman hands, Roc Rider rigging-friendly wings) after the developer's review of the 3D models of 16 Nov 2026 (front, side, back), checked against rts/docs/art/CONCEPT_BRIEF.md; images made by rts/tools/ConceptArt/generate.py. -->
# Turnarounds 6

Model for every shot: `gpt-image-2.6-sunburst`, size 1536x1024, edit endpoint, `quality: "high"`, same style text as turnarounds5. References: the turnarounds5 sheet of the same unit (`turnarounds5/skai_marksman_sheet.jpg`, `turnarounds5/skai_roc_rider_sheet.jpg`). 2 image requests in total: 2 first attempts, no regeneration. PNG originals are kept outside the repository.

Approximate x-ranges of the three views (front, side, back) in the 1536-pixel-wide image, for cropping (est. where views touch):

| Shot id | Front | Side | Back |
| --- | --- | --- | --- |
| skai_marksman_sheet | 100-345 | 405-1035 (rifle included) | 1105-1450 |
| skai_roc_rider_sheet | 10-510 | 545-965 | 1005-1525 |

| Shot id | File | Model | Result | Critique |
| --- | --- | --- | --- | --- |
| skai_marksman_sheet | skai_marksman_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Exactly two hands counted in each view, no glove on chest, scope or stock; rifle on the bird's right in all views. Reservation: in the front view the left (support) hand is foreshortened and sits close to the grip hand below the rifle rather than clearly forward on the handguard; its arm crosses the chest. |
| skai_roc_rider_sheet | skai_roc_rider_sheet.jpg | gpt-image-2.6-sunburst | pass (with reservation) | Wings straight, level and symmetrical in front and back, with shoulder, inner and outer segments and flat evenly spaced feathers; tail straight back; yellow patches, crystals, vents and neck band kept. Reservation: the views are small on the sheet (about half the height), and in the side view no wing line is drawn at all (hidden behind the body rather than a thin line). |

## Changed prompts

None; `shots.json` holds the prompts used.

## Tool changes

None; `rts/tools/ConceptArt/generate.py` was used unchanged.

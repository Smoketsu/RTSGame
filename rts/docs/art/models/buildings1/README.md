<!-- rts/docs/art/models/buildings1/README.md: first Tripo 3D models of nine Skai and water buildings, one model each, all nine marked Keep by the developer on 18 Nov 2026. -->
# Building models, batch 1 (18 Nov 2026)

One Tripo model per building, made from the nine model sheets the developer marked Keep (`concept/turnarounds9/`). **All nine were marked Keep by the developer on 18 Nov 2026** (no notes) and are the approved models. The Great Aerie (marked Revise) was not made. Each model was generated once and not retried; the notes come from a look at the preview image only, not from opening the `.glb`.

| Building | Data ID | Model file | Source sheet | Tripo task id | Note on the preview |
| --- | --- | --- | --- | --- | --- |
| Skai supply (Roost) | `roost` | `skai_roost.glb` | `skai_roost_sheet.jpg` | `bdd8b14a-857e-4183-b51e-b0333d024835` | Good match: trunk, talon roots, three canopies with hanging pods, yellow diamond. |
| Skai tech (Windspire) | `windspire` | `skai_windspire.glb` | `skai_windspire_sheet.jpg` | `48fa346c-2104-44e7-8b13-a94ae19e1e35` | Tiers, bolt and ring present, but the ring is thin and fused to the tree, tiers are uneven and the yellow stripe is broken. |
| Skai defence (Perch Tower) | `perch_tower` | `skai_perch_tower.glb` | `skai_perch_tower_sheet.jpg` | `1626cbd2-a12d-4646-8502-63010abe542a` | Good shape: X trunk, platform, canopy, two emitter arms. The platform top is muddy yellow and the arms are blocky. |
| Skai drop-off (Perch Depot) | `perch_depot` | `skai_perch_depot.glb` | `skai_perch_depot_sheet.jpg` | `9eb2d523-a481-4da4-ba44-022f6de2e556` | Good match: arch with crates, ramp, three canopies, crane with cargo box. |
| Grand Shell (water HQ) | `grand_shell` | `water_grand_shell.glb` | `water_grand_shell_sheet.jpg` | `a4fe58e7-b42c-4720-ad51-1be1f9135764` | Roughly right (tentacle legs, three domes, spires, waterfall) but the colours are washed out, the shell reads as a flat plate at the back, and the twin gun and the garage decks are not clear. |
| Water flood source (Tidepool) | `tidepool` | `water_tidepool.glb` | `water_tidepool_sheet.jpg` | `39b2e984-99ce-48a3-b0e1-6874dec437c1` | Good match: hourglass tower, turquoise stripes, scalloped rim, arched base. Puddle is pale. |
| Water tech (Shell Forge) | `shell_forge` | `water_shell_forge.glb` | `water_shell_forge_sheet.jpg` | `aca296bc-4f71-49c1-be90-7b3b9697f116` | Good match: brain-coral dome, antler crown, polyps, door. Colours slightly pastel. |
| Water defence (Coral Turret) | `coral_turret` | `water_coral_turret.glb` | `water_coral_turret_sheet.jpg` | `e09dbb20-e3a0-4400-b56e-9fcbdd45ab2c` | Spiked ring and base fine, but the cannon is short and points up at the viewer, merged into the mount; the side view's long angled barrel was lost. |
| Water drop-off (Pearl Depot) | `pearl_depot` | `water_pearl_depot.glb` | `water_pearl_depot_sheet.jpg` | `303a0b62-e531-493d-87f7-679e7b32b8bc` | Good match: coral canopies, door, stairs and rails. The stairs smoothed into a ramp. |
| Skai HQ, Rooted (Great Aerie) | `great_aerie` (`rts/data/units/skai/great_aerie.json`) | `skai_great_aerie_rooted.glb` | `turnarounds10/skai_great_aerie_rooted_sheet.jpg` | `54b105d6-53c7-4011-bfa5-3532ea82b559` | Good match: island rock on four taloned legs, trunk with hangar door and ramp, five feathered canopies with pods, twin turret, fans. Canopy feathers are soft, the roots over the rim fused into thick spikes, the ground patch is pale and the colours slightly washed out. Not approved yet. |
| Skai HQ, Uprooted (Great Aerie) | `great_aerie` (`rts/data/units/skai/great_aerie.json`) | `skai_great_aerie_uprooted.glb` | `turnarounds10/skai_great_aerie_uprooted_sheet.jpg` | `cc05a657-6dab-4cb6-90ed-679c0d96d666` | Good match: floating rock tapering to a point, three turbines, trunk with hangar, five canopies with pods, twin turret. The tip is stubby and its yellow end is faint, canopy feathers are soft, the roots on the rim are lumpy, colours slightly washed out. Not approved yet. |

Each model also has its preview (`<name>_preview.webp`), its log (`<name>_log.txt`, with the task id and balance) and the three cropped views (`<name>_front.png`, `_left.png`, `_back.png`). The data IDs are the files under `rts/data/units/skai` and `rts/data/units/water`; the sheet names use placeholder building names that differ from some data IDs.

## How they were made

- Each sheet was cut into front, side (`_left`) and back images with Pillow, using the x-ranges in `concept/turnarounds9/README.md` plus a few pixels of margin; the ranges were widened by hand for the Perch Depot, Shell Forge and Coral Turret so that nothing was cut off (the Coral Turret cannon, the Perch Depot crane box) and narrowed for the Grand Shell back view to drop a sliver of the side view. Every crop was checked by eye before any credits were spent.
- The three images went to `rts/tools/Models3D/tripo.py` with `--views left=... back=...` (multi-view endpoint, model `P2-20260801`, face limit 5000, texture on, PBR off), as for the units in `batch1`. The tool's output was renamed from `<name>_front.glb` to `<name>.glb`.
- One generation per building, nine in all, no retries, no retexture calls.

## Credits

- Balance before: 1,680. Balance after: 690. Spent: 990 (9 x 110).
- Great Aerie, Rooted and Uprooted (18 Nov 2026, second batch): balance before 690, after 470. Spent: 220 (2 x 110).

## Not done yet

- Import into Unity (PC session). All eleven models here are approved, including the two Skai HQ states.
- Rigging and Unity import untested; the models are smoother than the faceted drawings.

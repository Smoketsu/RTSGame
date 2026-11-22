<!-- rts/docs/art/models/unity/README.md: how the 25 approved GLB models were converted to Unity-ready FBX files, the normalisation rule, the table of results and problems, and the rigging pilot report. Read before importing the models in Unity. -->
# Unity-ready models (22 Nov 2026)

Produced by `rts/tools/Models3D/convert_static.py` (Blender 4.0.2, headless). Output: `rts/src/Game/Assets/Resources/Models/<data id>.fbx`, one per data ID (no `.meta` files; Unity creates those). The two Skai HQ states are `skai.great_aerie.fbx` (Rooted) and `skai.great_aerie.uprooted.fbx`.

## How to re-run

From the repository root (the scripts only need Blender; the contact sheets need Python 3 with Pillow):

```
blender -b -noaudio --python rts/tools/Models3D/convert_static.py -- --renders /tmp/renders      # all 25 models
blender -b -noaudio --python rts/tools/Models3D/convert_static.py -- --probe /tmp/probe          # 4-side + top views at yaw 0, no export
python3 rts/tools/Models3D/contact_sheet.py out.jpg 5 230 label=image.png ...                    # contact sheet
```

## Normalisation rule

1. Import the GLB, join into one mesh, apply all transforms.
2. Yaw about the vertical axis by the value in the table (chosen by looking at the probe renders; stored in the `MODELS` table of the script).
3. Scale uniformly so the **longest footprint side (bounding box width or depth) is exactly 1.0 unit**. The footprint includes everything that sticks out (rifle, spear, crest, crane, ground patch of buildings).
4. Footprint (bounding-box) centre at the origin; lowest vertex at height 0.
5. Axes: Blender up = +Z, front = -Y. Export `axis_forward='-Z'`, `axis_up='Y'`, `apply_scale_options='FBX_SCALE_ALL'`, `bake_space_transform=True`, `path_mode='COPY'`, `embed_textures=True`. This is the usual recipe for a Blender model to stand upright in Unity facing +Z with scale 1 and rotation 0.

To scale in Unity: the instance scale is `2 x radius_tiles x tile size` for units, or the footprint width in tiles for buildings; the height then follows from the table (height y = table value x that scale). Because the rule uses the longest side, a unit with a long rifle or spear (talon_archer, stormcaller) has a body much narrower than 1.0; the Unity session may want a per-model scale override for those.

**Facing:** every approved model came out of the generator facing +X (face, door, barrel, snout). Yaw 270 turns +X to -Y. I checked this on the four-side probe renders and on the final front and top renders (`*_front.jpg`, `*_top.jpg`): in every front render the face, door or barrel looks at the camera; in every top render it points to the bottom of the image. Eight models are near-symmetric (roost, windspire, perch tower, tidecaller, tidepool, shellback and so on) and have no real front: they just follow the generator's +X.

**FBX check:** each FBX was re-imported into a fresh Blender scene with Blender's own importer; bounds, floor height and vertex centroid match the pre-export model to under 0.002 units and the texture survives (`roundtrip` column of `sizes.csv`, all ok). This proves Blender-to-Blender consistency only, not what Unity does.

**File sizes:** each FBX is within about 10 percent of its GLB. The embedded texture is the generator's 2048 x 2048 colour map in its original compressed format; nothing was expanded to raw pixels.

## Contact sheets

- Three-quarter view: `skai_contact_sheet.jpg`, `water_contact_sheet.jpg`
- Front view (facing check): `skai_front.jpg`, `water_front.jpg`
- Top view (facing and footprint check, front is at the bottom): `skai_top.jpg`, `water_top.jpg`
- Same camera angle and lens for every model, framing fitted to each model. Plain grey ground disc and one sun lamp.

## Table (all 25 models)

Width x, depth z and height y are in normalised units, as they will appear in Unity (Blender depth y becomes Unity z). `sizes.csv` has the same numbers plus the applied scale factor.

| Data ID | Source GLB | Yaw (deg) | Width x | Depth z | Height y | FBX (KB) | GLB (KB) | Problems |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `skai.fledgling` | `batch1/skai_worker.glb` | 270 | 0.922 | 1.000 | 1.725 | 1303 | 1233 | Fine. Two crane arms joined by a cross bar on the back; face and torso clean. |
| `skai.windrunner` | `trial2/skai_scout.glb` | 270 | 0.847 | 1.000 | 2.562 | 1254 | 1189 | Very tall and thin (crest and tail curl set the footprint); body only about half of the footprint width. |
| `skai.talon_archer` | `batch1/skai_marksman_v3.glb` | 270 | 0.436 | 1.000 | 1.088 | 1270 | 1189 | Rifle sticks out ahead and sets the footprint: the body is only 0.44 wide, so scaling from the footprint makes the archer look small. Wing blades are flat plates. |
| `skai.stormcaller` | `batch1/skai_stormcaller_v4.glb` | 270 | 1.000 | 0.712 | 1.807 | 1217 | 1132 | Spear sets the width (1.0 vs body about 0.5). Backpack and staff are fused to the body. |
| `skai.roc_rider` | `batch1/skai_roc_rider_v2.glb` | 270 | 1.000 | 0.818 | 0.569 | 1314 | 1248 | Wings flat and level as intended (span = footprint 1.0). Rider fused to the bird. Tail spike is a thin point. |
| `skai.thunderhead` | `batch1/skai_thunderhead_v6.glb` | 270 | 1.000 | 1.000 | 0.380 | 2520 | 2976 | Very flat (height 0.38). Source GLB is 3 MB (larger texture file); the FBX embeds a smaller one. |
| `water.tender` | `batch1/water_worker_v2.glb` | 270 | 1.000 | 0.826 | 1.249 | 1027 | 942 | Fine. Backpack fused to the head. |
| `water.shellback` | `trial2/water_clam.glb` | 270 | 1.000 | 0.999 | 1.267 | 964 | 890 | Clam shell fused open, pearl inside is a blob. Footprint is nearly square. |
| `water.grasper` | `batch1/water_pummeler_v2.glb` | 270 | 1.000 | 0.876 | 1.564 | 1111 | 1025 | Two big fists fused to the body; antennae are thin and may shimmer at small size. |
| `water.spoutling` | `batch1/water_spoutling.glb` | 270 | 1.000 | 0.823 | 1.533 | 972 | 887 | Cannon fused to the head; tail spike at the bottom is a thin point. |
| `water.ink_lobber` | `trial2/water_snail.glb` | 270 | 0.904 | 1.000 | 2.082 | 1253 | 1172 | Snail cone; small side cannon is a stub. Tall (height 2.08). |
| `water.tidecaller` | `batch1/water_tidecaller.glb` | 270 | 1.000 | 0.996 | 1.513 | 933 | 844 | Fine; round, no real front. |
| `water.leviathan` | `batch1/water_leviathan.glb` | 270 | 1.000 | 0.985 | 0.959 | 938 | 845 | Crab legs fused at the tips; hermit shell and claw at the front are lumpy. |
| `water.reef_runner` | `batch1/water_reef_runner.glb` | 270 | 0.651 | 1.000 | 2.247 | 1051 | 960 | Very thin (footprint 0.65 x 1.0, height 2.25). Gear wheel under the seahorse is a thin ring. |
| `skai.roost` | `buildings1/skai_roost.glb` | 270 | 1.000 | 0.821 | 1.417 | 936 | 865 | Good. Ground patch is part of the mesh and counts in the footprint. |
| `skai.windspire` | `buildings1/skai_windspire.glb` | 270 | 1.000 | 0.950 | 1.883 | 942 | 892 | Tiers uneven, ring fused to the tree, yellow stripe broken (per buildings README). |
| `skai.perch_tower` | `buildings1/skai_perch_tower.glb` | 270 | 1.000 | 0.947 | 1.287 | 930 | 833 | Arms blocky, platform top muddy yellow (README). No front: yaw only follows the generator. |
| `skai.perch_depot` | `buildings1/skai_perch_depot.glb` | 270 | 1.000 | 0.904 | 1.021 | 1107 | 1022 | Good. Door faces forward. Crane box hangs off one side (counts in the footprint). |
| `skai.great_aerie` | `buildings1/skai_great_aerie_rooted.glb` | 270 | 1.000 | 0.757 | 1.478 | 1395 | 1333 | Rooted HQ. Roots fused into spikes at the rim, ground patch pale, colours washed out. Not approved yet per buildings README, converted anyway. |
| `skai.great_aerie.uprooted` | `buildings1/skai_great_aerie_uprooted.glb` | 270 | 1.000 | 0.789 | 1.576 | 1404 | 1326 | Uprooted HQ. Stubby tip, lumpy rim roots, washed colours. Not approved yet per buildings README, converted anyway. |
| `water.grand_shell` | `buildings1/water_grand_shell.glb` | 270 | 0.658 | 1.000 | 0.655 | 1163 | 1085 | Shell at the back reads as a flat plate; colours washed out; twin gun not clear. Longest side is the length (depth z). |
| `water.tidepool` | `buildings1/water_tidepool.glb` | 270 | 1.000 | 1.000 | 1.155 | 1182 | 1098 | Fine; round, no front. Pale puddle in the footprint. |
| `water.shell_forge` | `buildings1/water_shell_forge.glb` | 270 | 0.968 | 1.000 | 0.991 | 906 | 806 | Fine; door faces forward. Colours slightly pastel. |
| `water.coral_turret` | `buildings1/water_coral_turret.glb` | 270 | 0.904 | 1.000 | 0.954 | 1060 | 974 | Cannon short, merged into the mount (README). Barrel direction used as front. |
| `water.pearl_depot` | `buildings1/water_pearl_depot.glb` | 270 | 1.000 | 0.992 | 0.895 | 1070 | 981 | Stairs smoothed into a ramp; door faces forward. |

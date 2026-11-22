<!-- rts/docs/art/models/unity/README.md: how the 25 approved GLB models were converted to Unity-ready FBX files, the normalisation rule, the table of results and problems, and the rigging pilot report. Read before importing the models in Unity. -->
# Unity-ready models and rigging pilot (22 Nov 2026)

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

**Facing:** every approved model came out of the generator facing +X (face, door, barrel, snout). Yaw 270 turns +X to -Y. I checked this on the four-side probe renders and on the final front and top renders (`*_front.jpg`, `*_top.jpg`): in every front render the face, door or barrel looks at the camera; in every top render it points to the bottom of the image. Six models are near-symmetric (roost, windspire, perch tower, tidecaller, tidepool, shellback) and have no real front: they just follow the generator's +X.

**FBX check:** each FBX was re-imported into a fresh Blender scene with Blender's own importer; bounds, floor height and vertex centroid match the pre-export model to under 0.002 units and the texture survives (`roundtrip` column of `sizes.csv`, all ok). This proves Blender-to-Blender consistency only, not what Unity does.

**File sizes:** each static FBX is within about 15 percent of its GLB (the Thunderhead is 15 percent smaller). The size match shows the 2048 x 2048 colour map stayed compressed and was not expanded to raw pixels. The two rigged FBX files are about 50 percent larger because of the baked animation.

## Contact sheets

- Three-quarter view: `skai_contact_sheet.jpg`, `water_contact_sheet.jpg`
- Front view (facing check): `skai_front.jpg`, `water_front.jpg`
- Top view (facing and footprint check, front is at the bottom): `skai_top.jpg`, `water_top.jpg`
- Same camera angle and lens for every model, framing fitted to each model. Plain grey ground disc and one sun lamp.

## Table (all 25 models)

Width x, depth z and height y are in normalised units, as they will appear in Unity (Blender depth y becomes Unity z). `sizes.csv` has the same numbers plus the applied scale factor. The FBX size of `skai.fledgling` and `skai.roc_rider` is the rigged file (the static FBX was 1303 and 1314 KB); `sizes.csv` lists the static sizes.

| Data ID | Source GLB | Yaw (deg) | Width x | Depth z | Height y | FBX (KB) | GLB (KB) | Problems |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `skai.fledgling` | `batch1/skai_worker.glb` | 270 | 0.922 | 1.000 | 1.725 | 1926 (rigged) | 1233 | Fine. Two crane arms joined by a cross bar on the back; face and torso clean. |
| `skai.windrunner` | `trial2/skai_scout.glb` | 270 | 0.847 | 1.000 | 2.562 | 1254 | 1189 | Very tall and thin (crest and tail curl set the footprint); body only about half of the footprint width. |
| `skai.talon_archer` | `batch1/skai_marksman_v3.glb` | 270 | 0.436 | 1.000 | 1.088 | 1270 | 1189 | Rifle sticks out ahead and sets the footprint: the body is only 0.44 wide, so scaling from the footprint makes the archer look small. Wing blades are flat plates. |
| `skai.stormcaller` | `batch1/skai_stormcaller_v4.glb` | 270 | 1.000 | 0.712 | 1.807 | 1217 | 1132 | Spear sets the width (1.0 vs body about 0.5). Backpack and staff are fused to the body. |
| `skai.roc_rider` | `batch1/skai_roc_rider_v2.glb` | 270 | 1.000 | 0.818 | 0.569 | 1838 (rigged) | 1248 | Wings flat and level as intended (span = footprint 1.0). Rider fused to the bird. Tail spike is a thin point. |
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

## Part 2: rigging pilot (`skai.fledgling`, `skai.roc_rider`)

Both FBX files in `rts/src/Game/Assets/Resources/Models/` now carry an armature, skin weights and four clips, replacing the static FBX. `convert_static.py` does not overwrite them (it exports the static version of rigged units to its scratch folder only).

```
blender -b -noaudio --python rts/tools/Models3D/rig.py -- --unit skai.fledgling     # builds, exports, re-imports, checks, renders the sheet
blender -b -noaudio --python rts/tools/Models3D/rig.py -- --unit skai.roc_rider
# options: --weights auto|nearest (default nearest), --no-export, --renders <dir>, --sheet <jpg>, --debug-frames attack:12:three_quarter,...
```

Joints, bone radii, weighting settings and every clip are in `rts/tools/Models3D/rig_data.json` (commented). To rig another unit, add an entry to that file; the script reads the GLB and yaw from `convert_static.py`.

### Contact sheets (look at these)

- `pilot/skai.fledgling.jpg` and `pilot/skai.roc_rider.jpg`: one row per action (idle, move, attack, death), four evenly spaced frames each, three-quarter view, grey ground disc. Looping clips show frames 1 to 3/4 of the loop, one-shot clips show first, third, two-thirds and last frame.
- `pilot/auto_weights_failure.jpg`: Blender automatic weights against my weights at the same frame.

### Weighting method

**Blender automatic weights (bone heat) failed on both meshes**: Blender prints "Bone Heat Weighting: failed to find solution for one or more bones" and all vertices end up with no weight, so nothing moves (left column of `auto_weights_failure.jpg`). The generator meshes are fused, have many separate shells and non-manifold pieces, which bone heat cannot solve. I did not try envelope weights; I wrote my own instead (`nearest_bone_weights` in `rig.py`): for every vertex, the distance to each bone segment minus a per-bone radius, soft-max of those distances (`exp(-(d - nearest)/sigma)`), the 3 to 4 strongest bones kept, then smoothed over the mesh edges for 3 to 5 passes. It needs no manifold mesh and no voxelisation, and it is deterministic. The radius per bone matters: without it a thick fused part such as the tool arm is split between arm and torso and shreds.

### `skai.fledgling` (biped worker)

- Bones (14): `root`, `hips`, `spine`, `head`, `arm_l_upper`, `arm_l_lower`, `arm_r_upper`, `arm_r_lower`, `leg_l_upper`, `leg_l_lower`, `leg_r_upper`, `leg_r_lower`, `crane`, `crane_tip`. `_l` is +X, the model's left. The two crane masts share one bone, so the cranes cannot move independently.
- Clips: `idle` 30 frames (loop), `move` 24 (loop, two steps), `attack` 24 (one-shot, tool-arm strike), `death` 36 (one-shot, falls onto its side), all 30 fps.
- Attempts: three rounds of joint, radius and sigma changes plus small clip fixes (backpack shredding, then arm shredding, then walk foot dip).
- What still looks wrong:
  - At the extreme of the attack (frames 12 to 18) the seam between the right tool arm and the torso stretches: thin torn triangles and white feather streaks at the shoulder. Small at RTS zoom, visible when zoomed in.
  - In the walk, rear claws and heel of the lifted foot dip up to 0.05 units (3 percent of height) below the ground, and a few feather shards trail near the lifted foot (move frame 18).
  - Foot sliding against ground speed was not tested: the stride is about 0.17 units per step; Unity must match walking speed to this or the feet will slide.
  - Death ends lying on the side with the body about 0.05 units below the ground at the worst frame; the crane boom sticks out rigid.
  - The cargo boxes under the crane tip float in the source mesh (not attached to anything) and simply follow the crane tip.

### `skai.roc_rider` (flyer)

- Bones (11): `root`, `body`, `rider`, `head`, `tail`, `wing_l1`, `wing_l2`, `wing_l3`, `wing_r1`, `wing_r2`, `wing_r3`. The rider is one rigid bone.
- Clips: `idle` 30 frames (loop, hover), `move` 20 (loop, one wing beat), `attack` 24 (one-shot, rear up then dive), `death` 36 (one-shot, wings fold, pitches and rolls), 30 fps.
- Attempts: two rounds (neck tearing in the dive, death pose with wings through the body).
- Result: the flat wings made this much easier. The flap is clean at all three wing sections with no visible tearing.
- What still looks wrong:
  - Wing sections are rigid slabs, so the flap bends at three creases rather than curving.
  - During the dive the wing underside passes through the rider's legs and the bird's body, and in the death pose the wings overlap the body (feather tips show thin streaks).
  - The dive dips 0.16 units and the death 0.15 units below the starting height; for a flyer that is relative to its hover height, Unity has to lift and land it.
  - The long talon spikes and the rider's arms are fused and move rigidly with the body.

### FBX checks done in Blender

Re-importing each rigged FBX into a fresh scene: one armature, one mesh, 14 and 11 bones, four actions, rest-pose bounds identical to the source, and the deformed mesh bounds identical at four frames of every clip (largest error 0.0000 over all clips). The animation take names stored in the FBX are exactly `idle`, `move`, `attack`, `death` (read from the file bytes). Two Blender quirks to know about:

- With `bake_anim_use_all_actions=True` (as the brief suggested) Blender 4.0 names the takes `Armature|idle` etc. I therefore bake from NLA strips (one track per action, named like the action), which gives the bare names. Everything else is as briefed (`bake_anim=True`, `add_leaf_bones=False`, same axes as part 1).
- Blender's own FBX importer ignores translation keys on bones that are the only child of their parent. The check un-joins those bones first. The translation keys are in the file (60 translation curve nodes found); Unity does not have this limit, but I could not test Unity.

### What the Unity session must check

Scale (the mesh should be 1.0 unit on its longest footprint side with import scale 1; if it is 100 times too big or small, look at the FBX unit setting), facing (+Z is the front, upright), texture (embedded 2048 x 2048, needs a material with that texture), the clip names (the four names above, loop flags are not stored in the FBX, so idle and move need Loop Time set in the Animation tab), the rig type (Generic, root bone `root`). For the two rigged files also check that the model does not come in rotated -90 degrees about X or scaled 100 times on the armature node, a known problem when Blender exports armatures with baked space transform; if it does, report it and I will change the export recipe for rigged files. Not run in Unity at all: skinning with 4 bones per vertex, animation playback.

### Estimate for the other 12 units, by body type

| Body type | Units | Expected with this method | Better as rigid parts |
| --- | --- | --- | --- |
| Biped bird | `windrunner`, `talon_archer`, `stormcaller` | Same quality as the Fledgling. Two to three table iterations each. Thin limbs bind better than the Fledgling's thick ones. Risks: the rifle and the spear cross the body, so arm weights bleed into the torso; feather skirts stretch. | No: skeleton. |
| Biped squid | `tender` | Arms, head and backpack fine; the eight tentacles touch each other and would streak. Skeleton for the upper body, simple sway bones (or a vertex shader) for the tentacles. | Backpack and head rigid. |
| Creature | `grasper`, `reef_runner`, `spoutling`, `tidecaller` | `grasper` (two big claws, 2 bones each) and `reef_runner` (thin seahorse, 4 to 5 bone spine chain, wheel spins as a rigid part) should work well. `spoutling` and `tidecaller` (jellyfish bells with tentacles) work as a bell pulse plus tentacle chains, but the fused tentacles pull on each other; a vertex-shader sway is cheaper and looks as good at RTS distance. | `spoutling` cannon, `reef_runner` wheel. |
| Flyer | `thunderhead` | Flat, 0.38 high manta. A body bone plus two or three bones per wing tip is enough, same quality as the Roc Rider. | Practically rigid: a tilt and bob is nearly enough. |
| Rigid machine | `shellback`, `leviathan`, `ink_lobber` | Not worth skinning. `shellback`: the two clam halves open and close as rigid parts, treads as a scrolled texture. `leviathan`: turret turns as a rigid part, body bobs, eight legs are fused to the ground at the tips and would need 16 bones; do rigid turret plus body bob. `ink_lobber`: rigid snail cone with a rigid cannon and a squash wobble. | Yes, all three. |

Rigid part animation does not need a separate pipeline: it is this rig with hard weights (`smooth_passes` 0 and a tiny `sigma`), so each region follows exactly one bone. I did not test that. The buildings (11 models) are best left static, with rigid parts only where they have an obvious moving part (the Perch Depot crane, the Coral Turret cannon, the Great Aerie turrets and fans).

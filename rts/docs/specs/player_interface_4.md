<!-- rts/docs/specs/player_interface_4.md: build spec for Unity package UI-17, drawing the 25 imported 3D models instead of cubes: import settings, lookup by data ID with the cube as fallback, scale, facing, owner marking, HQ model swap, construction, clips for the two rigged units, death, fog, picking and the Play Mode and hash checks. For the session on the developer's PC (owns rts/src/Game). Written 22 Nov 2026. -->
# Player interface, part 4: 3D models instead of cubes (UI-17)

**Update, 22 Nov 2026:** the developer approved rigging the other twelve units, in progress on `review/models-2`. Every unit model will carry the same root bone `root` and the same four clips (`idle`, `move`, `attack`, `death`). Wherever this spec says "the two rigged files", apply the rule to every FBX that contains animation clips, found by inspecting the imported asset, never by a list of names; a unit whose rig was not good enough stays static and takes the static path.

**For:** the session on the developer's PC (owns `rts/src/Game` and the Unity editor through Funplay). **Follows:** `player_interface.md` section 1 (all nine rules apply unchanged; rule 6 hash check after the package). **Sources:** `rts/docs/art/models/unity/README.md` and `sizes.csv` (normalisation, scale rule, facing, import checks, rigging pilot); `SimulationHost`, `FogView`, `EventView`, `GateView`, `SelectionController`, `BuildPlacement` on `main` at `61174e2`.

**What exists:** 25 FBX files in `Assets/Resources/Models/<data id>.fbx`, no `.meta` files. Every model is normalised to a longest footprint side of 1.0, footprint centre at the origin, lowest vertex at y 0, front +Z. `skai.great_aerie.uprooted.fbx` is the Uprooted Skai HQ; Water's HQ has one model. `skai.fledgling` and `skai.roc_rider` carry clips `idle`, `move`, `attack`, `death` (30 fps); the other 23 are static. The project uses the built-in render pipeline (no URP package in `manifest.json`).

**How cubes work today (what must keep working):** `SimulationHost.CreateView` (on `UnitSpawnedEvent`) makes a primitive cube, drops its collider, sets a uniform `localScale` = `max(minimumCubeSize, Definition.Radius x cubeScalePerRadius)` and the shared per-player material (`GetPlayerMaterial`, colour `ColorOf(owner)`), and places it at `ToWorld(position, Height)`. `Height` is the cube centre above the terrain, 0.5 (`GroundHeight`) or 2 (`AirHeight`), changed by `UnitLayerChangedEvent`. `LateUpdate` lerps `Previous` to `Current`. `UnitRemovedEvent` destroys it at once. Other code depends on that transform:
- `SelectionController.TryPickUnit` picks by the distance from the mouse ray to `view.Transform.position`, within `max(minPickRadiusTiles, Radius x pickRadiusPerUnitRadius)`. It uses no collider and no mesh size. The selection ring is a child of the cube sized from `cube.localScale.x`.
- `FogView.LateUpdate` sets `view.Transform.GetComponent<Renderer>().enabled = CanSee(...)`. `EventView` line 202 reads the same `Renderer.enabled` to decide whether a kill effect shows. It also sizes health bars from `cube.localScale`.
- `GateView` squashes `cube.localScale.y` for an open gate.
- `BuildPlacement` ghosts are separate cubes (not unit views).
- The simulation has **no facing**: no `Facing` or `Heading` member exists on `Unit` (checked by grep in `rts/src/Sim`). `EventView` already derives a heading from the view's movement between frames.

## 1. Import settings (once, through Editor APIs, then read back)

An editor menu script `Assets/Scripts/Editor/ModelImportSettings.cs` ("RTS/Apply Model Import Settings"). It walks every `ModelImporter` under `Assets/Resources/Models` (found by folder, no ID list), sets the values below and calls `SaveAndReimport`. It then logs each value it reads back, per file. Never hand-edit a `.meta` file. Commit the 25 generated `.meta` files plus the extracted materials and textures.
- **Model tab:** `globalScale` 1, `useFileScale` on, `importCameras`/`importLights` off, `addCollider` off (picking needs none). Read back: each prefab's `Renderer.bounds` at scale 1 has its longest x/z side 1.0 +-0.01 and min y 0 +-0.01. Report any file that is 100x off, lying rotated -90 about X, or with a scaled armature node (README asks for this).
- **Rig tab:** a file with animation takes (`importedTakeInfos.Length > 0`) gets `animationType = Generic`, `avatarSetup = CreateFromThisModel` and root node `root` (README bone name). Every other file gets `animationType = None` (no Animator, no avatar).
- **Animation tab** (rigged files only): one `ModelImporterClipAnimation` per take, named exactly as the take. `loopTime` is on for `idle` and `move` and off for `attack` and `death`. Read back the four names and loop flags.
- **Materials:** extract the embedded materials and textures (`AssetDatabase.ExtractAsset`, `ModelImporter.ExtractTextures`) to `Assets/Resources/Models/Materials/`, one `.mat` per model. Turn on `enableInstancing` on each `.mat`. Read back that each model's renderer uses its extracted `.mat` with a non-null `mainTexture` (2048 x 2048).

## 2. Drawing a unit

- **View root.** `CreateView` keeps a root `GameObject` per unit with exactly today's position, `Height`, lerp and uniform `localScale` (`size`). Picking, the selection ring, health bars and `GateView` are then unchanged. The root no longer has a renderer. It gets one child: the model if there is one, else today's cube (local scale 1, local position 0, same player material).
- **Lookup.** `Resources.Load<GameObject>("Models/" + spawned.DefinitionId)`, cached per definition ID in a `Dictionary<string, GameObject>` (a null result is cached too). Null means fallback to the cube. Today that covers walls, gates and the two tech buildings. No unit ID string literal is added (rule 3 grep). The folder prefix `"Models/"` and the HQ suffix are Inspector fields: `modelFolder` "Models/" and `uprootedSuffix` ".uprooted".
- **Scale and offset** (README scale rule). The model's world size is `s = 2 x Definition.Radius` for a mobile unit (`Economy.FootprintTiles == 0`). For a structure it is `s = Economy.FootprintTiles` (1 tile in Phase 1). Child `localScale = s / size` (uniform), times `modelScaleMultiplier` (Inspector, 1.0, placeholder). Child `localPosition.y = -GroundHeight / size`, so a ground model's lowest vertex sits on the terrain and an air model hovers at `AirHeight - GroundHeight` above it. Read radius and footprint from the simulation, never typed in.
- **Facing (presentation only; the simulation has none).** Structures: child yaw `structureYawDegrees` (Inspector, 180, placeholder), so the door faces the camera, which looks along +Z. Mobile units start at the same yaw. Each frame, the root's horizontal motion since the last frame sets yaw = `atan2(dx, dz)`, but only when it is above `turnThreshold` (Inspector, 0.0001 world units squared, placeholder). Otherwise the yaw is kept. This is `EventView`'s heading rule. A `ShotFiredEvent` turns its `Attacker`'s model to face the `Target` view, if that view exists. Turning is instant. Rotate the child only, never the root (the ring and health bars assume an unrotated root).
- **Owner marking.** The textures are faction-coloured, so model units also get a flat disc child in the owner's colour: a cylinder primitive, collider removed, shadows off, `GetPlayerMaterial(owner)` (no new material). Diameter `ownerDiscDiameter` (Inspector, 1.1 x s, placeholder), height 0.02, just above the terrain under the selection ring. Neutral units are grey, as now. This is a placeholder until the developer decides (question 1).
- **HQ models.** Pick from `unit.HqState`: Rooted and Uprooting show `<id>`; Uprooted and Rooting show `<id><uprootedSuffix>` when that model loads, else `<id>`. Choose at spawn. Swap the child on `HqStateChangedEvent` (`Unit`, `To`) by destroying the old child and making the new one, keeping the yaw. Only the HQ child changes; the root stays.
- **Under construction.** While `unit.IsUnderConstruction`, the child's Y scale is multiplied by `max(constructionMinHeight, 1 - ConstructionTicksRemaining / EconomyRules.BuildTicks(sim, Definition))`. `constructionMinHeight` is an Inspector field, 0.15, placeholder. Update this after each step in `RefreshViewStates`. It reaches full height on `StructureCompletedEvent` or when the flag clears. Cubes behave as today.
- **Visibility.** `SimulationHost` caches each view's `Renderer[]` (child and disc) at creation and on an HQ swap. It adds `SetUnitVisible(EntityId, bool)` and `IsUnitVisible(EntityId)`. `FogView` line 144 and `EventView` line 202 call these instead of `GetComponent<Renderer>()`. Hidden units are hidden exactly as cubes are now, disc included.

## 3. Clips (rigged models only)

- A model is rigged when it has an `Animator` after load (section 1 sets `None` on the static ones). Static models and cubes get nothing.
- Playback uses the Playables API: `AnimationPlayableUtilities.PlayClip(animator, clip, out graph)`, with the clips from `Resources.LoadAll<AnimationClip>(modelFolder + id)` stored by name. The four clip names are constants matching README part 2. No Animator Controller asset. Destroy the graph on a change and in `OnDestroy`. `animator.cullingMode = CullCompletely`.
- **Choice, after each step, highest first:**
  1. `death`: see below.
  2. `attack`: a `ShotFiredEvent` with this `Attacker` this tick (ranged and melee alike). It plays once from the start, a new shot restarts it, and when it ends the next rule applies.
  3. `move`: horizontal distance between `view.Current` and `view.Previous` above `turnThreshold`. That covers walking and gathering trips (`GatherState.ToField`, `ToDropOff`).
  4. `idle`: everything else, including `GatherState.Gathering`, waiting and building.
  A rule switches the clip only when the clip changes. Clip speed is 1, except that `move` runs at `SpeedTilesPerSecond / (moveStrideNormalised x s)` loops per second against the clip's own rate. `moveStrideNormalised` is an Inspector field, 0.34 per loop (README: 0.17 per step, two steps), placeholder.
- **Death.** A `UnitKilledEvent` (`Victim`) marks the view dying. The `UnitRemovedEvent` for the same unit follows in the same tick. On that event, for a dying rigged view that `IsUnitVisible`: unparent the model child, keeping its world pose, snapped to `Current`. Put it in a host corpse list, play `death` and destroy it after `clip.length` seconds of real time (checked in `Update`). Everything else is removed at once, as today: root, disc, ring and the `_views` entry. So picking, selection, fog, health bars and the simulation never see the corpse. Hidden, static and cube views are removed with no clip. Clear corpses in `OnDestroy` and wherever `_views` is cleared.

## 4. Performance (smallest sufficient)

Share materials (the extracted `.mat`, `GetPlayerMaterial` for discs and cubes, never `renderer.material`). Turn on GPU instancing on those materials (section 1). Skinned models do not instance; only two unit types are rigged. Set `SkinnedMeshRenderer.quality = Bone4`. Check: `ui_sandbox` plus 100 spawned units from a temporary local script holds the frame rate seen with cubes within 20 percent (placeholder bar). Report the numbers. Nothing more (no LOD, no pooling) unless this check fails.

## 5. Play Mode acceptance (simulated input only; `execute_code` only reads between steps)

1. **Import read-back:** run the menu item. The log lists 25 files with scale, bounds, rig type and, for the two rigged files, four clips with the stated loop flags. None is reported off.
2. **`ui_sandbox`, slot 0:** both HQs and all eight workers show models, not cubes. Fledglings play `idle`. Each unit's disc has its owner's colour. Read `UnitViews` and check that each root's position equals what the cube would have had.
3. **Move and face:** drag-select the Fledglings (`simulate_mouse_drag`), right-click ground (`simulate_mouse_click`). They play `move` and face the travel direction. On arrival they turn back to `idle` and keep the last yaw. Clicking a single Fledgling selects it, and the ring appears as before.
4. **Gather:** right-click a field with a Fledgling. It plays `move` on the trips and `idle` while gathering.
5. **HQ swap:** select the Great Aerie and press the uproot key. The model changes to the Uprooted one when `Uprooting` ends. Press the root key and it changes back when `Rooting` ends. Slot 1 (Grand Shell) keeps its one model in every state.
6. **Construction:** order a Fledgling to build a Roost. The model grows from 0.15 height to full. A Wind Baffle wall shows as a cube (fallback).
7. **Attack and death (`ui_tech`):** the enemy Roc Riders near slot 0's base play `attack` when they fire. When killed, `death` plays once and the corpse vanishes. Its ring, health bar and picking are gone on the removal tick.
8. **Fog (slot 0, fog on):** enemy models and discs outside vision are hidden, exactly as cubes were. A Roc Rider killed in the fog plays no death.
9. **Hash check (rule 6):** (a) `economy_smoke.json` with no input. Unity's final hash equals the harness `STATE_HASH=` for the same script on the same commit. (b) Checks 3 to 7 recorded, then `ReplayCheck` prints `REPLAY OK`. Drawing must not move any hash. Both results go in the commit message. Rule 3 grep (`"skai.`, `"water.`) finds nothing in `rts/src/Game`.

## 6. Out of scope

Model build-placement ghosts (they stay cubes), rigging further units, attachment points for projectiles, team-colour masks in textures, LOD, terrain-conforming tilt, smooth turning, any change to `rts/src/Sim`.

## 7. Cloud-side gaps

None blocking. The simulation needs no facing value; section 2 is presentation-only. Conditional: if check 1 finds a rigged file rotated -90 about X or 100x scaled on the armature, or any model whose bounds are off, the cloud session fixes the export in `rts/tools/Models3D` (README asks for this report). The rest of UI-17 does not wait for that fix.

## 8. Questions for the developer

1. **Telling players apart:** coloured disc, tint multiply on the texture, or team-colour masks? Default: coloured disc (section 2).
2. **Thin models:** `talon_archer` (body 0.44 wide) and `stormcaller` look small under the footprint rule. Should they get a per-model scale override? Default: none now. If wanted, set it as the importer `globalScale` of that file (no ID in code).
3. **HQ swap moment:** should the model change when the transition starts or when it ends? Default: when it ends (Rooted and Uprooting show Rooted).
4. **Worker building and gathering:** should these play `idle`, or `attack` as a work loop? Default: `idle`.
5. **Corpse in fog:** should a death that starts visible keep playing if fog covers it? Default: yes, at most one clip length.

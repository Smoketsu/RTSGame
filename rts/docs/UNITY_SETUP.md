<!-- rts/docs/UNITY_SETUP.md: step-by-step setup of the Unity 6 project in rts/src/Game, what to expect on Play, how to check determinism, known limits and troubleshooting. -->
# Unity setup (Phase 0 bridge, with the Phase 1 camera, terrain and selection)

The simulation (`rts/src/Sim`, assembly `Rts.Sim`) is an engine-free C# library. The Unity project in `rts/src/Game` consumes it as a **local package** and only renders it: a generated terrain mesh and cubes for units now, real art later. Unity never changes the simulation directly; the only way in is `Simulation.Submit` (called from `SelectionController` when you give orders), and everything else reads events, terrain tiles and unit positions.

The files in this repository were written without Unity available, so the first open in the editor is the first compile. If anything below does not match what you see, see Troubleshooting and report it.

## What is in the repo

| Path | Purpose |
| --- | --- |
| `rts/src/Sim/package.json`, `Rts.Sim.asmdef`, `Rts.Sim.rsp` | Make the simulation folder a Unity package (`com.rtsgame.sim`, display name "RTS Simulation") with no engine references. See `rts/src/Sim/README.md`. |
| `rts/src/Game/Packages/manifest.json` | Project dependencies; `"com.rtsgame.sim": "file:../../Sim"` points at the simulation folder (path is relative to the `Packages` folder: `Packages/../../Sim` = `rts/src/Sim`). |
| `rts/src/Game/ProjectSettings/ProjectVersion.txt` | Pins the editor version (`6000.0.58f1`). Unity creates every other settings file itself. |
| `rts/src/Game/Assets/Scripts/Rts.Game.asmdef` | Game-side assembly, references `Rts.Sim` by name. |
| `rts/src/Game/Assets/Scripts/Bootstrap/SimulationHost.cs` | Builds the match from `rts/data` and the command script `rts/data/scripts/economy_smoke.json`, steps it (and keeps stepping after the script ends so you can play), draws cubes standing on the terrain, and exposes read-only views (`UnitViews`, `TryGetViewTransform`, `EventRaised`) to the scripts below. |
| `rts/src/Game/Assets/Scripts/Bootstrap/AutoBootstrap.cs` | Adds the host, a camera with `RtsCameraController`, a light, a `TerrainView` and a `SelectionController` when you press Play in any scene. |
| `rts/src/Game/Assets/Scripts/Camera/RtsCameraController.cs` | Fixed three-quarter top-down camera: WASD / arrow / edge-scroll / middle-drag panning, mouse-wheel zoom along a 55 degree pitch, clamped to the map. |
| `rts/src/Game/Assets/Scripts/Terrain/TerrainView.cs` | Draws the terrain once as a single coloured mesh (one flat quad per tile at its elevation layer, cliff faces between layers) and updates it from `TerrainChangedEvent`. |
| `rts/src/Game/Assets/Scripts/Selection/SelectionController.cs` | Click and box selection, control groups, selection discs, and the order hotkeys; turns orders into commands for the local player and submits them. |
| `rts/src/Game/Assets/Scripts/Presentation/RtsMaterials.cs` | Creates the vertex-colour material for the terrain and picks the best available shader (fallback chain documented in the file). |
| `rts/src/Game/Assets/Resources/Shaders/RtsVertexColorUnlit.shader` | Our small unlit vertex-colour shader, kept under `Resources` so player builds include it. |
| `rts/src/Game/Assets/Scripts/Editor/` | Editor-only assembly and the menu item `RTS > Create Bootstrap Scene`. |
| `rts/src/Game/Directory.Build.props` | Empty on purpose: stops the project files Unity generates for your IDE from inheriting `rts/Directory.Build.props` (warnings as errors, redirected output). |

## Steps

1. **Install Unity 6 LTS.** In Unity Hub: Installs > Install Editor > pick a `6000.0.x` LTS release (the project pins `6000.0.58f1`). Add the module for your IDE or platform only if you need it; none is needed to press Play.
2. **Add the project.** Hub > Projects > Add > "Add project from disk" > choose `rts/src/Game` (the folder that contains `Assets`, `Packages` and `ProjectSettings`, not `rts` and not `rts/src`).
3. **Editor version prompt.** If you installed a different 6000.x LTS than `6000.0.58f1`, Hub offers to upgrade the project to it. Accept; that is fine. Unity rewrites `ProjectSettings/ProjectVersion.txt` (adds a revision line). Commit that change so the whole team opens the same version.
4. **First open.** Unity 6 creates a Built-in Render Pipeline project (no URP or HDRP package is listed on purpose). It resolves the local package, then imports and compiles. Wait for the progress bar in the bottom-right corner to finish, and for the Console (Window > General > Console, Ctrl+Shift+C) to show no red errors. Turn "Collapse" off in the Console toolbar so repeated lines are not merged.
5. **Check the package.** In the Project window, expand `Packages`. You should see "RTS Simulation" containing `Core`, `Commands`, `Units`, `Data` and so on, plus `Rts.Sim.asmdef` and `README.md`. Also `Assets/Scripts` with `Bootstrap` and `Editor`.
6. **Commit the generated files.** Unity creates a `.meta` file next to every file and folder, including all of `rts/src/Sim/` (hundreds of them), `rts/src/Sim/README.md`, and everything under `rts/src/Game/Assets/`. **Commit all `.meta` files**: they hold the GUIDs assets reference each other by, and without them every teammate gets different GUIDs and broken references. Also commit `rts/src/Game/ProjectSettings/*`, `rts/src/Game/Packages/packages-lock.json` and any scenes. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or the generated `*.csproj`/`*.sln` (already in `.gitignore`).
7. **Press Play** in the open scene (an empty untitled scene is fine), or first run **RTS > Create Bootstrap Scene** to save `Assets/Scenes/Bootstrap.unity` (host, camera, light) and add it to the Build Settings. Commit the scene and its `.meta`. Either way `AutoBootstrap` fills in whatever is missing at Play, so the menu item is optional. The saved scene also contains the camera controller, terrain view and selection controller so you can tune their Inspector fields.

**Input.** The scripts use the **legacy Input Manager** (`Input.GetKey`, `Input.GetMouseButton`, `Input.mousePosition`, `Input.mouseScrollDelta`), so no Input System package is needed and none is listed in `Packages/manifest.json`. If you later install the Input System package, set Edit > Project Settings > Player > Other Settings > **Active Input Handling** to **Both** (or Input Manager (Old)); with only the new system selected these calls throw `InvalidOperationException`.

## What you should see

- **Game view:** a coloured 64 by 64 terrain seen from a three-quarter top-down angle (55 degree pitch), centred on the map. Three elevation layers are drawn at world height layer x 0.5: dark green for the low central basin (layer 0), green for the middle ring (layer 1) and light green for the two start plateaus in the bottom-left and top-right corners (layer 2), with darker cliff faces between layers, grey ramp tiles where the plateaus connect, and a faint checker so single tiles are readable. Water tiles, once a Water faction flood runs, appear blue (lighter shallow, darker deep) and blocked tiles (walls, building footprints) dark.
- **Cubes on plateaus:** units are cubes that stand on the tile height under them (blue for player 0, Skai, red for player 1, Water). With the default economy script each player starts on its plateau beside its resource fields. Air units float 2 world units above the terrain, ground units half a unit.
- **The economy script running:** `SimulationHost` plays `rts/data/scripts/economy_smoke.json` (75 seconds, 1500 ticks). Watch the Console and the cubes: Skai's Great Aerie walks to its fields and roots (tick 120), workers gather and shuttle between the fields and the HQ, more workers and a Roost, a Perch Tower and army units appear as the script trains and builds them, and at about tick 1300 the Skai HQ uproots. Water's HQ starts rooted beside its fields and does the same economy. When the 1500 ticks are done the Console prints `script finished at tick 1500, final hash ...` once and the match keeps running, so you can keep playing.
- **Selecting and ordering:** left-click one of your blue cubes (player 0): a green disc appears under it and the top-left text says `Player 0   Selected: 1 (...)`. Drag a rectangle to select several. Right-click the ground and they walk there; press **A** then left-click to attack-move; **S** stops them; select the Great Aerie and press **R** or **U** to root or uproot it; select a worker, hover a resource field and press **G** to gather. See Controls below.
- **Console:** one info line at start (`[SimulationHost] Match started from <path>/rts/data: N data files, script scripts/economy_smoke.json (N commands, 2 players, 1500 ticks), 20 ticks per second, seed 4321.`), then one line per second of match time:

  ```
  tick=0 hash=<16 hex digits>
  tick=20 hash=<16 hex digits>
  tick=40 hash=<16 hex digits>
  ```

  The hash interval comes from `rts/data/tuning/match.json` (`state_hash_interval_ticks`, 20 at the time of writing), so the spacing follows that file. Orders you give change the match, so hashes after your first order no longer match the headless runner (see the determinism section).
- **Hierarchy:** `SimulationHost`, `Simulation Units` (one cube per unit), `Main Camera` (with `RtsCameraController`), `Directional Light`, `Terrain View` (the generated mesh) and `Selection Controller`. Selected cubes have a child `Selection Ring`.

The command script is read from the same data folder (`scripts/economy_smoke.json` below it); in a player build it is part of the `StreamingAssets/data` copy. To play the Phase 0 movement scenario instead, set `Script Relative Path` on `SimulationHost` to `scripts/smoke.json` (in the saved Bootstrap scene).

## Controls

All controls act on **player 0** (Inspector field `Local Player Slot` on `SelectionController`; the slot must exist in the script). Orders are submitted as commands for the current tick with a sequence number counted from 1,000,000, so they never clash with the script's own numbers.

| Input | Action |
| --- | --- |
| Mouse to the screen edge | Pan the camera (switch off with `Edge Scroll Enabled` on `RtsCameraController` if it bothers you in the editor). |
| Arrow keys | Pan the camera (always). |
| W A S D | Pan the camera **only while nothing is selected**; with a selection A and S are the attack-move and stop commands, so WASD stops panning. |
| Middle mouse drag | Grab the ground and drag it. |
| Mouse wheel | Zoom between 10 and 45 world units of height along the fixed pitch. |
| Left click | Select the nearest own unit under the cursor; click empty ground to clear. |
| Shift + left click | Add the unit to the selection. |
| Left drag | Box select every own unit inside the rectangle (Shift adds). |
| Ctrl + 0..9 | Store the selection in a control group. |
| 0..9 | Select that control group (Shift adds; dead units drop out). |
| Right click on ground | `MoveUnitCommand` for each selected unit that can move (rooted HQs and buildings are skipped). |
| Right click on an enemy unit | `AttackUnitCommand` for each selected armed unit. |
| A, then left click | `AttackMoveCommand` to the clicked point (unarmed units just move). Esc or right click cancels. |
| S | `StopCommand`. |
| R / U | `RootHqCommand` for a selected uprooted HQ / `UprootHqCommand` for a rooted one. |
| G | `GatherCommand`: selected workers gather at the resource field under or next to the cursor. |

Notes. Picking uses the distance from the mouse ray to each cube, so air units are clickable where you see them. Order targets are snapped to the centre of a quarter-tile cell through integers (`Fix64.FromFraction`), so no floating-point value enters a command. There is no fog of war in the view yet: enemy units are visible and targetable anywhere (the simulation validates every order; a rejected one shows as a warning in the Console).

Where the data comes from: in the editor the host reads `<repo>/rts/data` directly (resolved as `Application.dataPath + "/../../../data"`, and `dataPath` is `rts/src/Game/Assets`), so there is no copy step and the editor never sees a stale copy. In a player build it reads `Application.streamingAssetsPath + "/data"`: before building, copy `rts/data` to `rts/src/Game/Assets/StreamingAssets/data` (that folder is git-ignored; an automatic copy at build time is a Phase 1 follow-up). The Inspector field "Data Directory Override" on `SimulationHost` beats both. Desktop players only: Android and WebGL need `UnityWebRequest` to read StreamingAssets.

## Confirm the simulation has no engine references

1. Project window > `Packages` > `RTS Simulation` > select `Rts.Sim.asmdef`.
2. In the Inspector, "General" shows **No Engine References** checked (the field may be greyed out because the package is not in `Assets`; that is fine). "Allow 'unsafe' Code" is unchecked.
3. The compiler enforces it: adding `using UnityEngine;` to any `.cs` in `rts/src/Sim` makes the assembly fail to compile.

## Check determinism between Unity and the headless runner

The state hash is a function of the match state only, so Unity and the headless harness must print the same hash at the same tick when they run the same seed, players and commands.

1. Run the harness on the file Unity plays: `dotnet run --project rts/tools/BalanceHarness -- --data rts/data --script rts/data/scripts/economy_smoke.json` (the harness plays `rts/data/scripts/smoke.json` when `--script` is omitted) and note the lines `tick=<n> hash=<16 hex>` and the final `STATE_HASH=<16 hex>`.
2. In Unity (Play) **without giving any orders**, find the line with the same `tick=<n>` in the Console. The two hashes must be identical. When the script's 1500 ticks are done the host logs once `script finished at tick 1500, final hash <16 hex>`; that value equals the harness's `STATE_HASH=` line. Any order you give adds a command the harness does not have, so the hashes legitimately diverge from the tick it is applied.
3. The comparison is meaningful because both programs play the same file. `SimulationHost` has no match definition of its own: it reads `<data dir>/scripts/economy_smoke.json` (Inspector field `Script Relative Path`) with `Rts.Sim.Data.CommandScript`, takes the players, the map, the seed and the tick count from it, submits every command before the first tick, and logs the final hash at the script's tick count. With `Continue After Script` on (the default) it then keeps stepping so you can play on; switch it off for a pure replay that stops at the last tick. The harness does the same with the same class. To keep them in step, leave the Inspector field `Match Seed` at -1 (use the script's seed); any value of 0 or more replaces the seed and the hashes will no longer match the harness unless you also pass `--seed` to it.
4. To try another scenario, edit `rts/data/scripts/economy_smoke.json` (or add a second file under `rts/data/scripts/` and point `Script Relative Path` and the harness `--script` at it); no C# changes are needed. The format is in `rts/tools/BalanceHarness/README.md`.
5. The Unity hash is printed after the tick has run, exactly as the harness does, so `tick=N` means the same thing in both.

## Known limitations (Phase 0)

- Camera, selection and orders are a hand-written fallback (legacy Input Manager, IMGUI drag box); nothing from the Asset Store is installed. Better packages are optional polish and are tracked in `rts/docs/ASSET_STORE.md`; anything chosen there must stay presentation only.
- No fog of war in the view (the simulation computes vision; drawing it is a later step), no minimap, no building placement or training UI yet (the scripts still train and build), and no formation: all selected units get the same target point and the simulation separates them.
- Ramps are drawn as flat grey tiles at their own layer, so a ramp looks like a step; units standing on it use the same height.
- Placeholder cubes only; no animation, no health bars; the only UI is the small status text and the drag rectangle.
- The match is whatever `rts/data/scripts/economy_smoke.json` says (players, map, seed, commands); it is replaced by a lobby later, and campaign missions reuse the same script format. The host keeps stepping past the script's tick count (`Continue After Script`); without it orders given after the last tick would never run.
- `AutoBootstrap` adds a host to every scene you press Play in. Delete `AutoBootstrap.cs` once a real entry scene exists.
- The host steps at most 10 ticks per frame (Inspector `Max Steps Per Frame`) and drops the rest, so a very slow machine runs the match slower than real time. That is correct for a local match and must be replaced by lockstep waiting when networking arrives (Phase 2).
- Player builds need `rts/data` copied to `Assets/StreamingAssets/data` by hand (desktop only).
- Not yet run in the Unity editor by the author of these files. Treat the first compile as a test and report anything that fails.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| Console: `[Package Manager Window] Error adding package ... com.rtsgame.sim` or "Cannot find ... Sim" | The `file:` path in `rts/src/Game/Packages/manifest.json` is relative to the `Packages` folder. It must resolve to `rts/src/Sim` (`file:../../Sim`). Check that you added `rts/src/Game` (not another folder) and that `rts/src/Sim/package.json` exists. |
| Red compile errors in `Rts.Sim` | The simulation must compile as `netstandard2.1` with C# 9 and no engine types. It builds clean with the .NET SDK, so an error here is a Unity-specific incompatibility. Do not patch around it in the Unity project: copy the full error (file, line, message) and report it so the simulation source can be fixed for both builds. |
| Yellow warnings CS8632 ("annotation for nullable reference types ...") in `Rts.Sim` | `rts/src/Sim/Rts.Sim.rsp` (`-nullable:enable`) is not being applied by your Unity version. Harmless; the alternative is a project-wide `Assets/csc.rsp` with the same line. |
| `error CS0246: The type or namespace name 'Rts' could not be found` in `Rts.Game` scripts | `Rts.Game.asmdef` must list `Rts.Sim` under References and the package must have resolved. Reimport the package (Assets > Reimport All) after fixing the manifest. |
| Console error `[SimulationHost] Could not start the simulation: Data directory not found` | The data folder could not be found. In the editor the repo must keep its layout (`rts/data` three folders above `Assets`); in a build copy `rts/data` to `Assets/StreamingAssets/data`; or set the Inspector override. |
| `DataValidationException` text such as `units/skai/windrunner.json: field 'speed_tiles_per_second' ...` | A data file is invalid; the message names the file and field. Fix the file in `rts/data` (the harness fails the same way). |
| `Player 1 uses unknown faction 'water'` | `rts/data/factions/water.json` is missing or its `id` differs; the host needs factions `skai` and `water`. |
| Nothing visible, no errors | Check the Game tab (not Scene), that the scene has no other camera pointing elsewhere (`AutoBootstrap` only adds a camera when none exists, and `RtsCameraController` takes over the one it finds), and that `Time.timeScale` is not 0. |
| Terrain is one flat colour, Console warns `Shader '...' ignores vertex colours` | `Assets/Resources/Shaders/RtsVertexColorUnlit.shader` did not compile or was not imported (look for shader errors in the Console, then Assets > Reimport). Without it the material falls back to `Sprites/Default`, which does draw vertex colours; the flat look means only a shader without vertex-colour support was found. |
| Terrain pink or invisible | The project uses a render pipeline whose materials cannot use the shader chain in `RtsMaterials`. Check the Console for the `[RtsMaterials]` message and report it with your pipeline (Built-in is the tested target). |
| `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package` | Project Settings > Player > Active Input Handling must be Input Manager (Old) or Both; see the Input note in the Steps. |
| WASD does not pan | Something is selected (A and S are commands then). Press Esc or click empty ground to deselect, or use the arrow keys. |
| Orders do nothing, Console warns `Player slot N does not exist` | `Local Player Slot` on `SelectionController` must be a slot of the script (0 or 1 in the shipped scripts). |
| Clicking selects nothing | You can only select your own units (player 0, the blue cubes); enemy cubes are targets for right-click. Small units need a click within about 0.6 tiles of the cube centre (`Min Pick Radius Tiles`). |
| Pink cubes | The project was switched to URP or HDRP and the default material is not valid for it. Phase 0 is Built-in Render Pipeline; either convert materials (Edit > Rendering > Materials) or remove the pipeline asset. |
| Hash lines never appear | `Log State Hashes` is off on `SimulationHost`, or the Console filter hides info messages. |
| Hashes differ from the headless runner | The Inspector `Match Seed` is not -1, `Script Relative Path` points at a different file than the harness runs, the harness was given `--script`/`--seed`/`--ticks` overrides, or the data files differ; Unity reads `rts/data` directly, so check the harness uses the same checkout. A genuine mismatch with identical inputs is a determinism bug in the simulation: report the first differing tick. |

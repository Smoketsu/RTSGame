<!-- rts/docs/UNITY_SETUP.md: step-by-step setup of the Unity 6 project in rts/src/Game, what to expect on Play, how to check determinism, known limits and troubleshooting. -->
# Unity setup (Phase 0 bridge)

The simulation (`rts/src/Sim`, assembly `Rts.Sim`) is an engine-free C# library. The Unity project in `rts/src/Game` consumes it as a **local package** and only renders it: cubes for units now, real art later. Unity never changes the simulation directly; it submits commands (`Simulation.Submit`) and reads events and unit positions.

The files in this repository were written without Unity available, so the first open in the editor is the first compile. If anything below does not match what you see, see Troubleshooting and report it.

## What is in the repo

| Path | Purpose |
| --- | --- |
| `rts/src/Sim/package.json`, `Rts.Sim.asmdef`, `Rts.Sim.rsp` | Make the simulation folder a Unity package (`com.rtsgame.sim`, display name "RTS Simulation") with no engine references. See `rts/src/Sim/README.md`. |
| `rts/src/Game/Packages/manifest.json` | Project dependencies; `"com.rtsgame.sim": "file:../../Sim"` points at the simulation folder (path is relative to the `Packages` folder: `Packages/../../Sim` = `rts/src/Sim`). |
| `rts/src/Game/ProjectSettings/ProjectVersion.txt` | Pins the editor version (`6000.0.58f1`). Unity creates every other settings file itself. |
| `rts/src/Game/Assets/Scripts/Rts.Game.asmdef` | Game-side assembly, references `Rts.Sim` by name. |
| `rts/src/Game/Assets/Scripts/Bootstrap/SimulationHost.cs` | Builds the match from `rts/data`, steps it, draws cubes. |
| `rts/src/Game/Assets/Scripts/Bootstrap/AutoBootstrap.cs` | Adds the host, a camera, a light and a ground plane when you press Play in any scene. |
| `rts/src/Game/Assets/Scripts/Editor/` | Editor-only assembly and the menu item `RTS > Create Bootstrap Scene`. |
| `rts/src/Game/Directory.Build.props` | Empty on purpose: stops the project files Unity generates for your IDE from inheriting `rts/Directory.Build.props` (warnings as errors, redirected output). |

## Steps

1. **Install Unity 6 LTS.** In Unity Hub: Installs > Install Editor > pick a `6000.0.x` LTS release (the project pins `6000.0.58f1`). Add the module for your IDE or platform only if you need it; none is needed to press Play.
2. **Add the project.** Hub > Projects > Add > "Add project from disk" > choose `rts/src/Game` (the folder that contains `Assets`, `Packages` and `ProjectSettings`, not `rts` and not `rts/src`).
3. **Editor version prompt.** If you installed a different 6000.x LTS than `6000.0.58f1`, Hub offers to upgrade the project to it. Accept; that is fine. Unity rewrites `ProjectSettings/ProjectVersion.txt` (adds a revision line). Commit that change so the whole team opens the same version.
4. **First open.** Unity 6 creates a Built-in Render Pipeline project (no URP or HDRP package is listed on purpose). It resolves the local package, then imports and compiles. Wait for the progress bar in the bottom-right corner to finish, and for the Console (Window > General > Console, Ctrl+Shift+C) to show no red errors. Turn "Collapse" off in the Console toolbar so repeated lines are not merged.
5. **Check the package.** In the Project window, expand `Packages`. You should see "RTS Simulation" containing `Core`, `Commands`, `Units`, `Data` and so on, plus `Rts.Sim.asmdef` and `README.md`. Also `Assets/Scripts` with `Bootstrap` and `Editor`.
6. **Commit the generated files.** Unity creates a `.meta` file next to every file and folder, including all of `rts/src/Sim/` (hundreds of them), `rts/src/Sim/README.md`, and everything under `rts/src/Game/Assets/`. **Commit all `.meta` files**: they hold the GUIDs assets reference each other by, and without them every teammate gets different GUIDs and broken references. Also commit `rts/src/Game/ProjectSettings/*`, `rts/src/Game/Packages/packages-lock.json` and any scenes. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or the generated `*.csproj`/`*.sln` (already in `.gitignore`).
7. **Press Play** in the open scene (an empty untitled scene is fine), or first run **RTS > Create Bootstrap Scene** to save `Assets/Scenes/Bootstrap.unity` (host, camera, light) and add it to the Build Settings. Commit the scene and its `.meta`. Either way `AutoBootstrap` fills in whatever is missing at Play, so the menu item is optional.

## What you should see

- **Game view:** a dark grey 64 by 64 ground plane seen from a three-quarter top-down angle. Four cubes appear at the corners within the first frame: blue (player 0, skai) at the bottom-left, red (player 1, water) at the top-right. A second after the start they begin to glide toward each other across the map. Air units (`skai.windrunner`) float higher (y = 2) than ground and water units (y = 0.5). Motion is smooth at any frame rate because the picture blends between the last two ticks.
- **Console:** one info line at start (`[SimulationHost] Match started from <path>/rts/data: N data files, 20 ticks per second, seed 1234.`), then one line per second of match time:

  ```
  tick=0 hash=<16 hex digits>
  tick=20 hash=<16 hex digits>
  tick=40 hash=<16 hex digits>
  ```

  The hash interval comes from `rts/data/tuning/match.json` (`state_hash_interval_ticks`, 20 at the time of writing), so the spacing follows that file.
- **Hierarchy:** `SimulationHost`, `Simulation Units` (one cube per unit), `Main Camera`, `Directional Light`, `Greybox Ground`.

Where the data comes from: in the editor the host reads `<repo>/rts/data` directly (resolved as `Application.dataPath + "/../../../data"`, and `dataPath` is `rts/src/Game/Assets`), so there is no copy step and the editor never sees a stale copy. In a player build it reads `Application.streamingAssetsPath + "/data"`: before building, copy `rts/data` to `rts/src/Game/Assets/StreamingAssets/data` (that folder is git-ignored; an automatic copy at build time is a Phase 1 follow-up). The Inspector field "Data Directory Override" on `SimulationHost` beats both. Desktop players only: Android and WebGL need `UnityWebRequest` to read StreamingAssets.

## Confirm the simulation has no engine references

1. Project window > `Packages` > `RTS Simulation` > select `Rts.Sim.asmdef`.
2. In the Inspector, "General" shows **No Engine References** checked (the field may be greyed out because the package is not in `Assets`; that is fine). "Allow 'unsafe' Code" is unchecked.
3. The compiler enforces it: adding `using UnityEngine;` to any `.cs` in `rts/src/Sim` makes the assembly fail to compile.

## Check determinism between Unity and the headless runner

The state hash is a function of the match state only, so Unity and the headless harness must print the same hash at the same tick when they run the same seed, players and commands.

1. Run the harness: `dotnet run --project rts/tools/BalanceHarness -- --data rts/data --script rts/tools/BalanceHarness/scripts/smoke.json` and note the lines `tick=<n> hash=<16 hex>`.
2. In Unity (Play), find the line with the same `tick=<n>` in the Console. The two hashes must be identical.
3. This is only meaningful when the host's scripted commands equal `smoke.json`. The host (`SimulationHost.BuildScript`) uses seed 1234 (Inspector field `Match Seed`), players `0` team 0 faction `skai` and `1` team 1 faction `water`, and this script. All commands have issuer 255 (none) and one running sequence number:

   | tick | sequence | command | details |
   | --- | --- | --- | --- |
   | 0 | 0 | `spawn_unit` | `skai.windrunner`, owner 0, (8, 8) |
   | 0 | 1 | `spawn_unit` | `water.spoutling`, owner 0, (10, 8) |
   | 0 | 2 | `spawn_unit` | `skai.windrunner`, owner 1, (56, 56) |
   | 0 | 3 | `spawn_unit` | `water.spoutling`, owner 1, (54, 56) |
   | 20 | 4 | `move_unit` | unit 1 to (56, 56) |
   | 20 | 5 | `move_unit` | unit 2 to (32, 32) |
   | 20 | 6 | `move_unit` | unit 3 to (8, 8) |
   | 20 | 7 | `move_unit` | unit 4 to (34, 32) |

   The move tick is the spawn tick plus one second of ticks (`ticks_per_second`, 20). If `smoke.json` differs, the numbers will not match even when everything is correct. A shared script file that both the harness and the Unity host read (so the two cannot drift apart) is a Phase 1 follow-up.
4. The Unity hash is printed after the tick has run, exactly as the harness does, so `tick=N` means the same thing in both.

## Known limitations (Phase 0)

- No selection, no unit orders from the player, no camera control: the picture is a fixed camera on a scripted match. Camera rig, box selection and input are tracked in `rts/docs/ASSET_STORE.md` (Asset Store picks pending; nothing from the Asset Store is installed, and anything chosen there must stay presentation only).
- Placeholder cubes only; no animation, no health bars, no UI.
- The scripted match is hard-coded in `SimulationHost`; it is replaced by real lobby and input code in Phase 1.
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
| Nothing visible, no errors | Check the Game tab (not Scene), that the scene has no other camera pointing elsewhere (`AutoBootstrap` only adds a camera when none exists), and that `Time.timeScale` is not 0. |
| Pink cubes | The project was switched to URP or HDRP and the default material is not valid for it. Phase 0 is Built-in Render Pipeline; either convert materials (Edit > Rendering > Materials) or remove the pipeline asset. |
| Hash lines never appear | `Log State Hashes` is off on `SimulationHost`, or the Console filter hides info messages. |
| Hashes differ from the headless runner | The scripted commands, seed or players differ from `smoke.json` (see the table above), or the data files differ; Unity reads `rts/data` directly, so check the harness uses the same checkout. A genuine mismatch with identical inputs is a determinism bug in the simulation: report the first differing tick. |

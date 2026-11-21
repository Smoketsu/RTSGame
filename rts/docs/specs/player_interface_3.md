<!-- rts/docs/specs/player_interface_3.md: build spec for Unity package UI-16, playing a campaign mission (the tutorial tutorial_01) in the Unity game: the menu entry, Rts.Campaign as a Unity package, the host call order with the mission runner and the replay recorder, how cues, objectives and hint areas are drawn, when camera and selection signals are reported, the end screens, and Play Mode checks driven by simulated input. For the session on the developer's PC (owns rts/src/Game). Written 21 Nov 2026. -->
# Player interface, part 3: playing a campaign mission (UI-16)

**For:** the session on the developer's PC (owns `rts/src/Game` and the Unity editor through Funplay). **Plan step:** Phase 2 step 8 (tutorial) meets step 9 (UI). **Follows:** `player_interface.md` (section 1 rules apply unchanged) and `player_interface_2.md` (rules 1 to 4, UI-12 `StartMatch`, UI-13 menu flow). **Sources:** `campaign_scripting.md`, `rts/src/Campaign` (`README.md` host call order, `MissionRunner` class summary), `rts/tools/MissionRunner/MissionPlay.cs` (the reference loop), `rts/campaign/missions/tutorial_01.json`.

**What exists (checked on `main` at `a959855`):** `Rts.Campaign` has `package.json` (`com.rtsgame.campaign`) and `Rts.Campaign.asmdef` (references `Rts.Sim`, no engine references) but no `.meta` files and no manifest entry. `SimulationHost.StartMatch(MatchRequest)` handles `MatchKind.Script` and `Skirmish`; `StepOnce` runs AI `Think`, `_recorder.Step()` (which is `sim.Step()` then `recorder.AfterStep()`), views, then stops on `MatchOutcome.Evaluate(...).IsDecided`. `MainMenu` has Skirmish and Quit. `GameMenu` draws its end screen from `SimulationHost.Outcome`.

## 1. Rules (in addition to the earlier specs)

1. **Single-player only.** The mission runner runs only in a local match with exactly one `MissionPlayer.IsHuman` slot. `StartMatch` refuses any other mission with "Missions are single-player only" (shown on the menu, rule 4 of part 2). Never attach a `MissionRunner` to a networked or lockstep match: each peer would report its own camera and selection and submit outside the command bundles, and the peers would desync (`MissionRunner` class summary).
2. **The runner only reads and submits.** Every command it makes goes through `SimulationHost.Submit` (so through the recorder). The presenter only stores cues; it never calls the simulation or the runner.
3. **Signals only from the step loop.** `ReportSignal` is called only inside `StepOnce`, before the AI and the step (section 4). Never from `Update`, `LateUpdate`, `OnGUI` or an input callback.
4. **No match-outcome stop in a mission.** A one-team match is "decided" from tick 0 (`MatchOutcome.IsDecided` summary), so with a mission running the host skips `MatchOutcome.Evaluate` and stops only on `runner.Status != MissionStatus.Running`. Losing the HQ is the mission's own `defeat_when`.

## 2. Package: `Rts.Campaign` in Unity

- Add `"com.rtsgame.campaign": "file:../../Campaign"` to `rts/src/Game/Packages/manifest.json` and `Rts.Campaign` to `Rts.Game.asmdef` references.
- Let Unity import it and commit the `.meta` files Unity generates for every file in `rts/src/Campaign`, with no other change in that folder, as commit `1737e04` did for `rts/src/Ai`. Confirm `Rts.Campaign` compiles in Unity with no errors or warnings.
- Mission files: the campaign folder sits beside the data folder: `Path.Combine(dataDirectory, "..", "campaign")`, so `rts/campaign` in the editor and `StreamingAssets/campaign` in a build. Add an Inspector `campaignDirectoryOverride` (empty by default), like `dataDirectoryOverride`.

## 3. Choosing the mission (menu) and the host call order

- **Menu:** `MainMenu` gets a "Tutorial" button between Skirmish and Quit. It takes the first `*.json` in `<campaign>/missions` in ordinal file-name order (today `tutorial_01.json`). It calls `MatchLauncher.Load(new MatchRequest { Kind = MatchKind.Mission, MissionId = <file name without .json> })`. Add `Mission` to `MatchKind` and `string MissionId` to `MatchRequest`. No mission ID literal appears in code. Open question 1 covers a list instead.
- **`StartMatch`, `MatchKind.Mission` branch, in this order** (README steps 1 to 3 and the `MissionRunner` summary):
  1. Read `<campaign>/missions/<MissionId>.json`. Parse it with `MissionLoader.Parse(text, path, Data, BuiltInMechanics.ScriptParsers)`; a `DataValidationException` blocks the start and its `Message` is shown.
  2. Check rule 1, then build controllers for slots with `MissionPlayer.AiProfile != null` (same code as script mode, ascending slot). The tutorial has none.
  3. `_sim = mission.CreateSimulation(Data)`, then `_sim.Start()`. Do not build a `SimulationConfig` yourself: the mission decides whether Planetary Cycles are on.
  4. When `recordSession` is on: `_recorder = new ReplayRecorder(_sim, mission.PlayerSetups(), BuiltInMechanics.CreateCodecRegistry())`, before anything is submitted.
  5. `_mission = new MissionRunner(mission, _cues, Submit)`, then `_mission.Start(_sim)`. The setup commands (issuer 255) go through `Submit` and so into the replay. `_cues` is a small Unity-side `MissionCueQueue : IMissionPresenter` owned by the host. It only appends cues to a list, because `Start` runs in `Awake` before the HUD exists.
  6. `AiHost.StartAll(_sim, _controllers)`. Set `LocalSlotOverride` to the human slot and `StartFocus` to `map.Spawns[humanSlot]` (`Data.RequireMap(mission.MapId)`). `_scriptTicks = int.MaxValue`; the mission's `max_seconds` is the limit.
- **`StepOnce` with a mission:**
  - (a) camera and selection signals (section 4), or in inputs mode `_inputs.BeforeStep(_sim, _mission, Submit)`;
  - (b) AI `Think` as now;
  - (c) `_recorder.Step()` or `_sim.Step()`;
  - (d) `_mission.AfterStep(_sim)`, exactly once per step;
  - (e) `RefreshViewStates`, `ProcessEvents`;
  - (f) if `_mission.Status != Running`: stop stepping, log `[SimulationHost] mission <status> at tick <EndTick> <EndReason>, final hash <_sim.ComputeStateHash() x16>`, then `WriteReplay()`.

  The `tick=N hash=` Console lines stay as they are. Expose `MissionRunner Mission` (null outside missions) and `IReadOnlyList<MissionCue> MissionCues` for the HUD.
- **Inputs mode (for the parity check only):** an Inspector switch `playMissionInputs` (default off). When it is on, `StartMatch` also parses `<campaign>/inputs/<MissionId>.json` with `MissionInputs.Parse(text, path, mission, Data)`, and step (a) uses the inputs and reports no Unity signals. Human commands then come only from the file.

## 4. Camera and selection signals

- In step (a), before each step:
  - Camera: `focus = selection.ToSimPoint(new Vector3(camera.FocusTiles.x, 0, camera.FocusTiles.y))` (integer sub-tile cells, `Fix64.FromFraction`). Report `new CameraMovedSignal(focus.X, focus.Y)` if it differs from the last reported focus.
  - Selection: `SelectionController.Selected` sorted ascending by `EntityId`. Report `new SelectionChangedSignal(ids)` if the list differs from the last reported one.
  - If the last `AfterStep` activated an objective (any objective is `Active` with `FirstCountingTick == _sim.Clock.Tick`), report both even if they are unchanged. That way a selection made earlier still counts. Open question 3 covers this.
  - Objectives active from the start trigger the re-report before tick 0. So the starting focus is reported too: for the tutorial that is spawn (8, 8), outside the Core circle.
  - At most one signal of each kind per step. They are stamped with `_sim.Clock.Tick` by the runner and counted by that step's `AfterStep`.
- **Why this is replay-safe:** signals never reach the simulation. They can only change which mission commands are submitted, and those go through the recorder. Frame timing can move a command by a tick in live play, but it can never make the replay differ from the session. `ReplayCheck` replays with no runner.

## 5. What is shown (`MissionHud`, added by `AutoBootstrap.EnsureHud` when `host.Mission != null`)

Each cue expires at tick `cue.Tick + sim.Clock.SecondsToTicks(cue.Seconds)`, game time, so pausing pauses it. Ties: newest on top.

| Source in code | Drawn as |
| --- | --- |
| `CueKind.ShowMessage` (`Text`, `Seconds`) | Mission message box, top centre under the resource bar. Up to 3 messages, newest on top, each until it expires. Kept apart from `MessageLine`. |
| `CueKind.HighlightUnits` (`Units`, `Seconds`) | Pulsing ground ring (`LinePool`) under each listed unit found with `host.TryGetViewTransform`, until expiry. Dead or unknown IDs are skipped. |
| `CueKind.HighlightArea` (`X`, `Y`, `RadiusTiles`, `Seconds`) | Ground ring of that radius in tiles at (X, Y), until expiry. |
| `CueKind.CameraFocus` (`X`, `Y`) | `RtsCameraController.CentreOn(X, Y)` at once (`ToFloatForPresentation`). The move is reported as a camera signal before the next step, like any other move. |
| `runner.Objectives` (`Definition.Text`, `Status`) | Objective panel, top left, in file order. `Locked` hidden; `Active` plain; `Completed` with a tick mark; `Failed` in red. Title: `mission.DisplayName`. |
| `ObjectiveDefinition.Hint` (`Centre`, `RadiusTiles`) | A steady ground ring while its objective is `Active`. |
| Mission time | Elapsed time against `mission.MaxSeconds` in the panel, as m:ss. |

**Out of scope (not in the code):** voice-over, portraits, cinematics or camera lock, arrows to off-screen targets, minimap pings, mission briefing and debriefing screens, saving progress or campaign unlocks, a mission-select map, research or structure-placed conditions, cue sounds. `runner.Log` is not shown (Console only, optional).

## 6. End screens and back to the menu

`GameMenu` reads `host.Mission` when it is not null instead of `Outcome`:
- `Won`: "Mission complete".
- `Lost` with `EndReason == "timeout"`: "Out of time".
- `Lost` with `"defeat"`: "Mission failed: your HQ was lost".
- `objective_failed:<id>`: "Mission failed: " plus that objective's `Text`.
- Any other reason: "Mission failed (<reason>)".

Buttons: Restart (`MatchLauncher.Load(host.CurrentRequest)`, same mission and seed), Main Menu (`Load(null)`) and Quit. F10 keeps Surrender, Restart, Main Menu and Resume. Surrender defeats the player, so `defeat_when` ends the mission as `defeat`. The replay is written at the end (step f) and on `OnDestroy`, as now.

## 7. Play Mode checks

1. **Play the tutorial (simulated input only).** Main menu, click Tutorial. Do what each objective asks:
   - pan with arrow keys (`simulate_key_press`) to the Core;
   - click the Windrunner (entity 4, highlighted);
   - right-click below the east ramp at the marked ring (18.5, 8.5);
   - box-select Fledglings 2 and 3 and right-click the highlighted crystal field;
   - after two deliveries, select a Fledgling and place a Roost on the plateau with the build menu.
   
   Each objective turns to a tick mark in order, the cues appear as in section 5, and "Mission complete" shows. `execute_code` reads `host.Mission.Status == Won`. Then `dotnet run --project rts/tools/ReplayCheck -- --data rts/data --replay <written file>` prints `REPLAY OK`.
2. **Parity, exact hash: not achievable through simulated input.** The headless inputs fire a fixed number of ticks after each activation (`FirstCountingTick + SecondsToTicks(delay)`). Simulated keys and clicks land on whatever tick the frame is on, and that depends on frame time and `maxStepsPerFrame`. One tick of difference in the camera signal shifts every later activation, so the end tick (726) and `0596b0ca3c13635d` cannot be reproduced. **Nearest check:** turn on `playMissionInputs`, click Tutorial and touch nothing. Unity logs "mission Won at tick 726" and final hash `0596b0ca3c13635d`. Every `tick=N hash=` line equals the tool's output from `dotnet run --project rts/tools/MissionRunner -- --data rts/data --campaign rts/campaign --mission tutorial_01`, and the session's replay passes `ReplayCheck`. This proves Unity's loop is identical to `MissionPlay`; check 1 proves the live input path.
3. **Loss paths.** Tutorial, F10, Surrender: "Mission failed: your HQ was lost", then Main Menu shows the menu and the replay passes `ReplayCheck`. Timeout is not checked in Play Mode (900 s); the tool's tests cover it.
4. **Rule 6a of part 1** (economy_smoke hash) still holds in Script mode.

## 8. Cloud-side gaps

None. Every member used is public on `main`: `MissionLoader.Parse`, `MissionDefinition.CreateSimulation`, `PlayerSetups`, `Players`, `MapId`, `DisplayName`, `MaxSeconds`, `ObjectiveDefinition.Hint`, `MissionRunner.Start`, `ReportSignal`, `AfterStep`, `Status`, `EndReason`, `EndTick`, `Objectives`, `MissionCue` fields, `MissionInputs.Parse`, `BeforeStep`, and `ReplayRecorder(sim, players, codecs)`. The `.meta` files are generated by the PC session, as for Ai (developer approved then; confirm it applies here, question 6).

## 9. Questions for the developer (defaults apply until answered)

1. **Tutorial entry:** start the first mission file, or show a mission list? Default: first file; a list comes with the second mission.
2. **`camera_focus`:** pan at once, or show a "press Space to look" prompt? Default: pan at once.
3. **Re-report signals on activation** (section 4), so a selection made earlier still counts? Default: yes.
4. **Cue durations in game time or real time?** Default: game time (ticks).
5. **Mission files in player builds:** copying `rts/campaign` to `StreamingAssets/campaign` is a build-step job. Default: out of scope here, as for `rts/data`.
6. **`.meta` files in `rts/src/Campaign` committed by the PC session**, as was done for Ai? Default: yes, with no other change there.

| Action | Key or mouse | Where on screen (placeholders) |
| --- | --- | --- |
| Tutorial | mouse | main menu, between Skirmish and Quit |
| Objective panel | read-only | top left, 360 px wide at 1920 x 1080 |
| Mission messages | read-only | top centre, under the resource bar |
| End screen (Restart, Main Menu, Quit) | mouse | centre dialog |

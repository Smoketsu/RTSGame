<!-- rts/docs/specs/player_interface_2.md: build spec for the next Unity packages after UI-0 to UI-10 (Phase 2 plan step 9, menus part): skirmish setup with a local AI opponent, main menu and match flow, minimap, replay playback, and four verification scripts. Each package says the player action, the exact API it calls, what it reads for feedback, its refusals, a Play Mode check driven by simulated input, and the cloud-side gaps it waits on. For the session on the developer's PC (owns rts/src/Game). Written 21 Nov 2026 on the developer's direction "get every feature usable, then make the AI work". -->
# Player interface, part 2: skirmish, menus, minimap, replays

**For:** the session on the developer's PC (owns `rts/src/Game` and the Unity editor through Funplay). **Plan step:** Phase 2 step 9 (menus; lobby and Steam transport come later). **Follows:** `player_interface.md` (UI-0 to UI-10, the research panel and interface support I1 to I5 are on `main` as of 21 Nov). **Sources:** `CLAUDE.md`, `HANDOFF_UNITY.md`, `UNITY_SETUP.md`, the specs `balance_harness` (section 2, standard start) and `skirmish_ai` (section 6, host loop), ADR-009 (replays).

**What exists (checked on `main` at `b548aa4`):** `SimulationHost.Awake` loads `rts/data`, parses one command script (`scriptRelativePath`), builds `SimulationConfig` with the script's players, map and seed plus `dataSet.Cycles.Values`, starts it, attaches a `ReplayRecorder`, submits the script through `SimulationHost.Submit` and steps through `_recorder.Step()`; it stops on `MatchOutcome.IsDecided` and writes the replay in `WriteReplay` (also on `OnDestroy`). `LocalCommands` holds the local slot (Inspector `localPlayerSlot`) and the one sequence counter. `AutoBootstrap.Run` (`RuntimeInitializeOnLoadMethod(AfterSceneLoad)`, runs once) adds the host, views, camera, selection and HUD. `GameMenu` has F10 Surrender and the end screen, whose Leave stops Play Mode. `Rts.Game.asmdef` references `Rts.Sim` and `Rts.Replay`, not `Rts.Ai`; `Packages/manifest.json` has `com.rtsgame.sim` and `com.rtsgame.replay` only.

---

## 1. Rules

Section 1 of `player_interface.md` applies unchanged (commands only, every number from the simulation, lists from data, no ID literal, no float in a command, hash check after each package, uGUI built from code, documentation standard, lowest-`EntityId` tie-breaks). In addition:

1. **One match per scene load.** Every transition (menu to match, restart, back to menu, replay) stores a request in a static `MatchLauncher.Pending` (presentation state, never read by the simulation) and reloads the active scene. Nothing tears down a running match by hand; `SimulationHost.OnDestroy` already writes the replay. `MatchLauncher.Pending` is reset in a `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` so a Play session never inherits the last one when domain reload is off.
2. **AI thinks on ticks, never on frames.** Every AI controller's `Think` runs exactly once immediately before each `Simulation.Step`, inside the host's step loop, for every stepped tick, including each of several ticks stepped in one frame. No AI call happens in `Update` outside that loop, during a pause, or between frames. This is the order of the harness (`Program.cs`: `ThinkAll`, `Step`, `AfterStep`).
3. **Lists in ascending ID order:** maps (`dataSet.Maps`), factions (`dataSet.Content.Factions` with `HqUnitId != null`), AI profiles (`dataSet.AiProfiles`), replay files (by file name, newest first is the one exception, since names are timestamps).
4. **Errors are shown, never thrown into the frame loop.** A `DataValidationException`, `InvalidOperationException` from `StandardMatchStart.Commands`, or `ReplayException` is shown on the screen that caused it, with its `Message`, and logged.

## 2. Packages, in build order

### UI-12 Skirmish setup and local AI opponent

- **Player action:** on the setup screen pick a map, then per slot: Human, AI or Closed; team; faction; AI profile (AI slots); and a seed (number field plus a "Random" toggle). Start.
- **Build (UI-13 reuses it):**
  - `SimulationHost` gets an Inspector `startMode`: Script (default until UI-13) starts the Inspector script at once, exactly as today; Skirmish shows the setup screen.
  - A `MatchLauncher` static class (Bootstrap folder) with `Pending` (a `MatchRequest` or null) and `Load(MatchRequest?)`, which sets `Pending` and calls `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`. `MatchRequest` holds: kind (Script, Skirmish, Replay), map ID, seed, "random seed" flag, slots (slot, team, faction ID, Human/AI, profile ID), and the replay path.
  - `AutoBootstrap` subscribes once to `SceneManager.sceneLoaded` and runs its `Ensure*` set on every load (the `AfterSceneLoad` hook fires only once). When the host is idle (`Match == null`) it adds only the camera, light, EventSystem and a menu canvas (same `CanvasScaler` settings, `HudBuild` helpers), none of the match views.
  - Setup Start calls `MatchLauncher.Load(request)`; after the reload the host starts `Pending` in `Awake`.
- **Setup lists:**
  - Maps from `dataSet.Maps` showing `MapDefinition.DisplayName`; slots `0 .. map.Spawns.Count - 1` (every shipped map has 2).
  - Factions with `FactionDefinition.HqUnitId != null`, shown by `DisplayName`.
  - AI profiles: the IDs of `dataSet.AiProfiles` whose parsed `AiProfile.Factions` contains the slot's faction (parse with `AiProfile.FromDataSet(dataSet, id)` once per ID when the screen opens; an ID that throws is left out and logged); after gap G2, only profiles with a `MenuName`, shown by it.
  - Team: shown as 1 to the slot count, stored as `TeamId` 0 to count - 1; default team = slot index.
  - Seed: 0 to 2147483647; with "Random" on (default) a new one is drawn at Start with `UnityEngine.Random` (an input to the match, not simulation code) and written into the field so it can be read back.
- **Start is enabled** when exactly one slot is Human, at least two slots are open and at least two distinct teams are among them.
- **API, in this order (a new `SimulationHost.StartMatch(MatchRequest)` that replaces the body of `Awake`; script mode calls it too):**
  1. `players`: one `PlayerSetup(new PlayerId(slot), new TeamId(team), factionId, name)` per open slot, ascending slot. Name: "Player" for the human, "AI <slot + 1>" for AI slots (text only).
  2. `StandardMatchStart.Commands(dataSet, map, players)` before anything is built, so its `InvalidOperationException` (no spawn, no HQ, too few worker tiles) blocks Start with its message.
  3. `new SimulationConfig(dataSet.CreateMatchSettings(seed), dataSet.Content, players, BuiltInMechanics.CreateRegistry(), dataSet.Tuning, map, dataSet.Cycles.Values)`, `new Simulation(config)`, `Start()`, then `new ReplayRecorder(sim, players, BuiltInMechanics.CreateCodecRegistry())` when recording.
  4. Each start command through `SimulationHost.Submit` (recorder first, so they are in the replay).
  5. Controllers: `new SkirmishAiController(new PlayerId(slot), AiProfile.FromDataSet(dataSet, profileId))` per AI slot; `AiHost.StartAll(sim, controllers)`.
  6. `LocalCommands` gets the human slot through a new setter (replacing the Inspector-only `localPlayerSlot` for skirmish; script mode keeps the Inspector value). The camera centres on `map.Spawns[localSlot]` with `RtsCameraController.CentreOn`.
  7. No tick limit: the match runs until `MatchOutcome.IsDecided` or the player leaves.
- **How AI commands are submitted (so `ReplayCheck` accepts the session):**
  - The host holds the controllers in a list sorted by `Player`.
  - In `StepOnce`, before `_recorder.Step()` (or `_sim.Step()`), it calls `controller.Think(_sim, _aiSink)` for each in ascending `PlayerId`.
  - `_aiSink` is a small Unity-side `ICommandSink`: `ScheduleTick => _sim.Clock.Tick`; `Submit(command) => SimulationHost.Submit(command)`, which goes through `ReplayRecorder.Submit` (codec check, `Simulation.Submit`, recorded).
  - Do **not** call `AiHost.ThinkAll`: its own `SimulationCommandSink` submits around the recorder, and the replay would miss every AI command and desync.
  - The local player's and the AI's commands for the same tick are ordered by the simulation by (issuer, sequence); the AI keeps its own sequence counter per player, so they cannot collide with `LocalCommands`.
- **Determinism:**
  - Rule 2.
  - The AI reads the simulation only between steps (where `Think` is called), seeds its generator from the match seed and its slot, and keeps no static state; `Time.deltaTime` decides only how many ticks run per frame.
  - If the local slot is AI-controlled (script mode with an `ai` key on it), `LocalCommands.Submit` drops the command and logs it once.
- **Script mode gets AI too:** when the script's `CommandScript.AiProfiles` names slots, the host creates the same controllers for them. This is what makes the parity check below possible.
- **Feedback:** the AI's `CommandRejectedEvent`s are not shown on the message line (it filters on the local issuer already); the host's Console warning for rejections is skipped for AI issuers unless a new Inspector switch "Log AI Rejections" (default off) is on.
- **Plainly: the AI does not research yet.** It never queues research, so it never builds the Stormcaller, Thunderhead, Ink Lobber or Leviathan. It also does not use walls, gates, Downdraft, the wave or the HQ Last Stand (`STATUS.md`). That is expected until the AI work resumes (research and unlocks R2).
- **Refusals shown on the setup screen:** `StandardMatchStart` messages as given; a profile that fails to parse is absent from the list; a profile without a plan for the slot's faction is absent from that slot's list.
- **Play Mode check:**
  - (a) Script mode, `economy_smoke.json`, no input: final hash equals the harness `STATE_HASH=` (rule 6a).
  - (b) Script mode, `scripts/ai_vs_ai.json`, no input: every Unity `tick=N hash=` line equals the harness line for the same tick from `dotnet run --project rts/tools/BalanceHarness -- --data rts/data --script rts/data/scripts/ai_vs_ai.json`, and Unity's "match decided ... final hash" equals the harness `STATE_HASH=`.
  - (c) Play with `startMode` Skirmish: `prototype_1v1`, slot 0 Human Skai, slot 1 AI Water with `aggressive_s5`, seed 1234, Random off; click Start; within 60 s the enemy's worker count rises (read with `execute_code`); play 3 minutes with input; F10, Surrender: the replay passes `ReplayCheck` with `REPLAY OK`.
- **Cloud-side gaps:** G1 and G2 (section 3). G1 blocks this package; G2 does not (until it lands, every profile is listed by ID and the first in the list is preselected).
- **Out of scope:** AI difficulty levels, AI research, a lobby, more than one human, observer slots, handicaps, saving setups.

### UI-13 Main menu and match flow

- **Player action:** main menu: Skirmish, Replays (shown once UI-15 is built), Quit. Setup: Start, Back (Esc). In a match, F10 adds Restart and Main Menu to the existing Surrender and Resume. End screen (Victory, Defeat, Draw): Restart, Main Menu, Quit, replacing Leave. "Defeated" with Keep Watching keeps Keep Watching and gets Main Menu.
- **Build:**
  - `startMode` gains Menu, which becomes the default (Skirmish is folded into it). Script is unchanged; every hash check and verification script uses it.
  - Menu starts `MatchLauncher.Pending` when set; when it is null the host stays idle and the main menu canvas shows (UI-12's idle path), with the setup screen one click away.
- **Transitions:**
  - Start: `Load(request)`.
  - Restart: `Load(same request)`, with a new seed drawn when its random flag is on.
  - Main Menu and Back after a match: `Load(null)`.
  - Quit: `Application.Quit()`, and `EditorApplication.isPlaying = false` in the editor (as `GameMenu` Leave does).
  - Leaving a live match sends no command; the replay simply ends at that tick (question 3).
- **Reads:** `SimulationHost.Outcome` and the local player's `IsInMatch`, as UI-10 does now.
- **Rejections:** none new.
- **Play Mode check:**
  - Play with `startMode` Menu: the menu shows.
  - Click Skirmish, set up as UI-12 (c), Start: the match runs.
  - F10, Restart: tick restarts at 0, a replay file of the first match exists.
  - F10, Surrender, confirm: Defeat screen; Main Menu: the menu shows and no `SimulationHost` has a match (`execute_code` read).
  - Main Menu, then Quit: Play Mode stops.
  - Both replay files pass `ReplayCheck`.
- **Cloud-side gaps:** none.
- **Out of scope:** settings screen, key rebinding, credits, a campaign entry, a loading screen.

### UI-14 Minimap

- **Player action:** left-click or left-drag on the minimap moves the camera there; right-click orders the selection to that point; while attack-move targeting (A) is armed, left-click on the minimap sends attack-move there.
- **Build:** one `RawImage` with a `Texture2D` of `sim.Terrain` width x height (one pixel per tile, point filtering), bottom left, square, scaled to fit the map's aspect.
- **Reads, local team only** (`sim.Players.TeamOf(local)`):
  - Terrain colour per tile from the same height, ramp, blocker and water-depth rules `TerrainView` uses (share its colour function; rebuild on `TerrainChangedEvent`).
  - Fog per tile, refreshed every stepped tick at most once per frame: `sim.Vision.IsTileVisible(team, x, y)` full colour, else `IsTileExplored` dimmed, else black (the `FogView` rule; F8 Show All applies).
  - Units each frame as dots in `SimulationHost.ColorOf(owner)`: own and allied always; enemy and neutral only when `FogView.CanSee(sim, team, unit)`. Structures (`Economy.FootprintTiles > 0`) as their footprint in tiles, mobile units as 2 x 2 pixels (placeholder).
  - Core: `sim.Map.Core.Centre` and `RadiusTiles` as a ring in the holder colour (`sim.Cycles.Holder`), grey with no holder.
  - Camera frame: the four viewport corner rays of the main camera intersected with the ground plane y = 0, drawn as a quadrilateral.
- **Commands:**
  - Minimap pixel to tile point: `x = u * width`, `y = v * height`, through `SelectionController.ToSimPoint(new Vector3(x, 0, y))` (integer sub-tile cells, `Fix64.FromFraction`).
  - Right-click: the ground half of `SelectionController.IssueRightClickOrder`, split out as a public `IssueGroundOrder(FixVec2 destination)`: `MoveUnitCommand` per selected unit that can move, `SetRallyPointCommand(tick, local, seq, producer, false, destination)` per selected producer that cannot.
  - Attack-move: `AttackMoveCommand` per selected unit, then targeting ends.
  - No enemy picking on the minimap.
- **Rejection texts:** the existing ones (`out_of_bounds` "Outside the map", rally refusals as I5).
- **Play Mode check:**
  - Skirmish on `prototype_1v1` (UI-12 (c)): the enemy corner is black on the minimap.
  - Click it: the camera moves there.
  - Select two workers, right-click the minimap at the map centre: they walk there and the explored tiles turn dimmed on the minimap behind them.
  - An enemy dot shows only while one of them sees it.
  - The replay passes `ReplayCheck`.
- **Cloud-side gaps:** none.
- **Out of scope:** pings, alerts on attack, minimap zoom or rotation, last-seen structures.

### UI-15 Replay playback

- **Player action:** main menu, Replays: a list of `*.rtsreplay` files in the replay folder (`WriteReplay`'s folder, moved into one shared helper), newest first, showing file name, `MapId`, player names and length (`TickCount / TicksPerSecond` as m:ss). Pick one: `MatchLauncher.Load` with kind Replay. During playback: pause, speed down, speed up, Main Menu.
- **API:**
  - `ReplayReader.Read(bytes, BuiltInMechanics.CreateCodecRegistry())` when the list opens (a file that throws shows "Unreadable: <message>" and cannot be picked).
  - Play through gap G3's `ReplayPlayback(replay, dataSet)`: the host steps `playback.Step()` instead of `_recorder.Step()`, with no recorder, no AI controllers and no `LocalCommands` (absent or `IsReady` false, so no input reaches the simulation; selection and the selected-unit panel still work, the command card is hidden).
  - Speed: the accumulator adds `Time.deltaTime * speed`, speeds 0 (pause), 0.5, 1, 2, 4, 8 (placeholders); `maxStepsPerFrame` stays.
  - Stepping stops at `replay.TickCount` or at the first desync.
- **Viewpoint:** observer: fog off (`FogView.ShowAll` forced on in playback, also in builds); the resource bar shows one player at a time, the first slot at start, Tab cycles through `replay.Players` in ascending slot order.
- **Feedback:** a playback bar: tick and time, speed, and at the end "Final hash <16 hex> / recorded <16 hex>: match" or "differs"; on a checkpoint mismatch "Desync at tick N" and stepping stops.
- **Refusals (from `ReplayException.Message`):** other format version, different data digest ("recorded with other game data"), unknown map, hash interval differs: shown on the list, playback not started.
- **Play Mode check:**
  - Play the replay written in UI-13's check: units move as in the match.
  - Space pauses (tick stops), + raises speed to 8 (ticks advance about 8 times as fast).
  - Clicks on the map send nothing (no new `CommandRejectedEvent`, recorder absent).
  - At the end the bar shows the final hash equal to `ReplayCheck`'s `REPLAY OK final=` for the same file.
- **Cloud-side gaps:** G3 blocks this package.
- **Out of scope:** seeking or rewinding, per-team fog view, camera follow, replay renaming or deletion, playing replays from older builds.

## 3. Cloud-side gaps (the cloud session fills these first)

- **G1 (blocks UI-12): `Rts.Ai` as a Unity package.** `rts/src/Ai` has `package.json` (`com.rtsgame.ai`) and `Rts.Ai.asmdef` but, unlike `rts/src/Replay`, no committed `.meta` files. Either the cloud session commits `.meta` files for every file and folder in `rts/src/Ai` (as were committed for Replay), or the developer allows the PC session to commit the ones Unity generates there, with no other change in `rts/src/Ai`. The PC session then adds `"com.rtsgame.ai": "file:../../Ai"` to `Packages/manifest.json` and `Rts.Ai` to `Rts.Game.asmdef`, and confirms `Rts.Ai` compiles in Unity with no warnings.
- **G2 (does not block): AI profile menu names.** `AiProfile` accepts an optional top-level `menu_name` (text, 1 to 40 characters) and exposes `string? MenuName`; the setup lists only profiles that have one. Set it on `aggressive_s5.json` ("Standard AI", placeholder); evaluation profiles (`abl_*`, `rt_*`, older `aggressive_s*`) get none. Data comment line per the documentation standard. No hash change.
- **G3 (blocks UI-15): stepwise replay playback in `Rts.Replay`.** `ReplayVerifier.Verify` runs the whole replay in one call. Add `public sealed class ReplayPlayback`:
  - constructor `(ReplayData replay, DataSet data)`: calls `ReplayVerifier.CreateSimulation`, applies the digest and hash-interval refusals `Verify` applies (throwing `ReplayRefusedException`) and submits every recorded command;
  - `Simulation Sim`; `int TickCount`; `bool IsFinished` (clock at `TickCount`, or desynced);
  - `bool Step()`: steps one tick, compares its `StateHashedEvent` with the next checkpoint exactly as `Verify` does, returns false once finished;
  - `int DesyncTick` (-1 while none), `ulong ExpectedHash`, `ulong ActualHash`; `ulong FinalHash` and `bool FinalMatches` once the last tick ran;
  - `Verify` is rewritten on top of it, so there is one comparison.
  - Tests: a recorded harness match plays to `FinalMatches`; a tampered checkpoint gives its `DesyncTick`; refusals as `Verify`'s. Needs a determinism review (it steps a simulation).

## 4. Verification scripts (the four checks the PC session could not do)

Data files under `rts/data/scripts/`, `CommandScript` format, header and per-key comments, played in Script mode with "Local Player Slot" 0, on `greybox_1v1`, seed 1234, `ticks` 72000. Positions are placeholders: the author checks each with the harness (`--script <file> --ticks 2400`: no rejection warnings except those named) and states every entity ID in `$doc`. All start from `ui_sandbox.json`'s spawns (HQ and four workers per side, entity IDs 1 to 10). Each check ends with `ReplayCheck` on the session.

1. **`ui_research.json`: a research running to completion.**
   - **Spawns:** slot 0 gets a complete Storm Spire beside its HQ (as in `ui_tech.json`), a complete Perch Depot on a free tile next to rare field index 4 at (8, 52), and two Fledglings beside the field with `gather` (field 4) at tick 0; two is `gather_slots_per_rare_field`.
   - **Why:** the starting Rare is `starting_rare` 0 in `tuning/economy.json` and no script command can set a stock (question 4), so the script gives a short Rare income instead. Stormcaller Plans costs 25 Common and 125 Rare (`research/skai/stormcaller_plans.json`); two gatherers at a 1-tile trip bring about that much in under a minute.
   - **Check:** wait until the resource bar shows Rare at least 125, select the Storm Spire, click the Stormcaller Plans button: Rare falls by 125, the queue icon fills over 30 s, `ResearchCompletedEvent` gives a notice, the Stormcaller train button turns from grey to enabled.
2. **`ui_victory.json`: Victory by destroying the enemy HQ.**
   - **Spawns:** slot 0 gets eight Talon Archers (placeholder count; the Grand Shell has 1800 health, an archer does 8 damage per shot) at 7 to 8 tiles from slot 1's Grand Shell (entity 6), outside its 6-tile weapon range so nothing fires before input.
   - **Why:** `last_stand_enabled` is false in `tuning/hq.json`, so the HQ dies outright and its owner is defeated with cause `hq_destroyed`.
   - **Check:** box-select the archers, right-click the Grand Shell: it dies (time is a placeholder; note it in the commit) and the Victory screen shows cause `hq_destroyed`.
3. **`ui_2v2_defeated.json`: "Defeated, Keep Watching" in a 2v2.**
   - **Spawns:** four players: slot 0 Skai team 0 (local), slot 1 Water team 0, slot 2 Skai team 1, slot 3 Water team 1. Each gets an HQ and two workers in its own map quarter; slot 2 also gets eight Talon Archers beside slot 0's HQ with `attack_unit` on it at tick 20. No map has four spawns; `spawn_unit` places units anywhere, so the script does not need one.
   - **Why:** a scripted attack removes slot 0's HQ without input while slots 1, 2 and 3 keep theirs, so `MatchOutcome` stays undecided and only the local player is out. Harness check: `OUTCOME undecided` at tick 2400.
   - **Check:** no input; slot 0's HQ dies; the "Defeated" screen offers Keep Watching and Leave (Main Menu after UI-13); click Keep Watching: the screen hides and the clock keeps advancing (the match is not stopped).
4. **`ui_health.json`: a health bar and death ring on screen.**
   - **Spawns:** slot 0 gets three Talon Archers and slot 1 one Talon Archer, 5 tiles apart, 4 tiles from slot 0's HQ (inside the starting camera view and in slot 0's sight), and `attack_unit` from each of slot 0's archers on the enemy one at tick 20.
   - **Why:** three against one ends in a few seconds with one death, so the damaged archer's health bar and the `UnitKilledEvent` ring are both on screen in the first 10 s with no input.
   - **Check:** take a screenshot at about 3 s (health bars below full) and at the death (ring visible), and read the unit count with `execute_code`.

## 5. Questions for the developer (defaults apply until answered)

1. **Keys and layout** (table below). Default: as listed.
2. **Which AI profiles appear in the setup?** Default: only `aggressive_s5` after G2; all profiles by ID until then.
3. **Leaving a live match:** send `SurrenderCommand` first, so the replay records the result? Default: no; leaving just ends the replay.
4. **A script command to set resources** (issuer `PlayerId.None` only, for test scripts and campaign)? Default: no for now; `ui_research.json` gathers Rare instead. Adding it is a Sim change with a hash and codec impact.
5. **Restart seed:** reuse the seed or draw a new one? Default: new seed when "Random" was on, else the same.
6. **Observer and all-AI skirmish** (zero human slots, fog off)? Default: not in UI-12; script mode with `ai_vs_ai.json` covers watching AI against AI.
7. **Replay viewpoint:** observer with fog off, or one team's fog? Default: fog off.

### Keys and layout (placeholders)

| Action | Key or mouse | Where on screen |
| --- | --- | --- |
| Main menu (Skirmish, Replays, Quit) | mouse | centre column |
| Setup (map, slots, seed, Start, Back) | mouse; Esc = Back | centre panel, one row per slot |
| In-match menu (Surrender, Restart, Main Menu, Resume) | F10 | centre dialog |
| End screen (Restart, Main Menu, Quit; Keep Watching) | mouse | centre dialog |
| Minimap: move camera / order / attack-move | left click or drag / right click / A then left click | bottom left, 256 x 256 at 1920 x 1080 |
| Replay: pause, slower, faster | Space, minus, plus | playback bar, top centre |
| Replay: next player on the resource bar | Tab | top right |
| Replay list | mouse | centre panel |

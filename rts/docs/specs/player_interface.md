<!-- rts/docs/specs/player_interface.md: build spec for the Unity player interface (Phase 2 plan step 9, HUD part). An ordered list of small packages that each make one simulation feature usable by a human through commands, with the queries and events each one reads, its rejection texts and a Play Mode check. For the session on the developer's PC (owns rts/src/Game). Written 21 Nov 2026 on the developer's direction "get every feature usable, then make the AI work". -->
# Player interface: every simulation feature usable by a human

**For:** the session on the developer's PC (owns `rts/src/Game` and the Unity editor through Funplay). **Plan step:** Phase 2 step 9 (HUD, timeline bar, surrender; menus and lobby come later). **Sources:** `CLAUDE.md` (architecture rules, Unity section), `UNITY_SETUP.md`, the specs `worker_construction`, `walls_and_tech` (T.5, G.5, D.5), `fog_of_war`, `planetary_cycles`, `combat_mechanics`, `flooding`, proposal P-007.

**What exists (checked on `main` at `3d6a51e`):** seven scripts under `rts/src/Game/Assets/Scripts`. `SimulationHost` steps the match from a command script, draws every unit as a cube (no fog) and raises `EventRaised` for every event of every tick. `TerrainView` draws heights, ramps, blockers and water depth, and redraws on `TerrainChangedEvent`, so flood water is already visible. `SelectionController` is the only input path: click, drag and control-group selection, and `MoveUnitCommand`, `AttackUnitCommand`, `AttackMoveCommand`, `StopCommand`, `RootHqCommand`, `UprootHqCommand` and `GatherCommand`, with its own sequence counter (from 1,000,000) and an `OnGUI` status line. There is no HUD, no train or build input, no fog, no ability input, no wall drag, no cycle pick, no Core display, no surrender and no match end screen. `Rts.Game.asmdef` references only `Rts.Sim`.

---

## 1. Rules for every package

1. **Commands are the only way in.** Every player action becomes a `Command` submitted through the one local command path built in UI-0. No package changes simulation state any other way: not from `execute_code`, not from a MonoBehaviour, not by calling a public mutator. Play Mode checks drive input with `simulate_key_press`, `simulate_mouse_click` and `simulate_mouse_drag`; `execute_code` may only read (between steps).
2. **Every number comes from the simulation.** Costs (`UnitDefinition.CostCommon`, `CostRare`), supply (`EconomyRules.SupplyOf`), build time (`EconomyRules.BuildTicks`), queue length (`sim.Tuning.Economy.MaxQueueLength`), wall line length (`sim.Tuning.Economy.WallLineMaxTiles`), ranges, radii, cooldowns and timers are read from definitions, tuning and queries at the moment they are drawn. Ticks are shown as seconds with `sim.Clock.TicksPerSecond`. Unity holds only layout, colours, key bindings and text.
3. **Button lists come from data.** Train buttons: the HQ worker (`Definition.Hq.WorkerUnitId`) first when the producer is an HQ, then `Definition.Economy.Trains` in file order. Build buttons: `sim.Content.Factions[player.FactionId].UnitIds` in file order. Ability buttons: from definition fields. No unit or faction ID string literal appears in `rts/src/Game` (a grep for `"skai.` and `"water.` must find nothing).
4. **The simulation decides.** Greyed-out buttons and red ghosts are hints computed from public queries. Only a missing prerequisite, a producer under construction and an HQ that is not Rooted grey a button out; anything else is sent and the simulation answers. Every `CommandRejectedEvent` whose `Command.Issuer` is the local player is shown on the message line (UI-0) with the text from the package's table; an unlisted reason shows the raw reason string.
5. **No float enters a command.** Points are quantised to integer sub-tile cells and built with `Fix64.FromFraction`, as `SelectionController` does today; tiles are integers.
6. **Hash check after each package.** (a) Play `economy_smoke.json` without input: the final hash Unity logs equals the harness `STATE_HASH=` for `--script rts/data/scripts/economy_smoke.json` on the same commit. (b) Play the package's acceptance check with recording on (UI-0), then `dotnet run --project rts/tools/ReplayCheck -- --data rts/data --replay <file>` prints `REPLAY OK`. Both results go in the package's commit message.
7. **UI technology.** uGUI, built from code at runtime by an `AutoBootstrap.EnsureHud` helper, like the existing `Ensure*` helpers, so no scene or prefab is edited by text. One Canvas, `CanvasScaler` "Scale With Screen Size", reference 1920 x 1080, match 0.5. Skip the mobile-only checks of the Funplay UI skill.
8. **Documentation standard** of `CLAUDE.md` applies to every new file and member.
9. **Order and selection tie-breaks** in the UI are fixed so a test can predict them: whenever the UI must choose one unit among the selection (producer, worker, caster), it takes the best by the stated rule and breaks ties by the lowest `EntityId`.

## 2. Test scripts

Data files under `rts/data/scripts/`, parsed by `CommandScript`, header and per-key comments as usual. Positions and counts are placeholders.

- `ui_sandbox.json`: `greybox_1v1`, the spawns of `ai_vs_ai.json` (HQ plus four workers per side) without `ai` keys and without orders, `ticks` 72000 (placeholder, one hour). Slot 0 Skai, slot 1 Water. Set `LocalCommands`' "Local Player Slot" to 1 to play Water.
- `ui_tech.json`: `ui_sandbox.json` plus script `spawn_unit`s at tick 0 of complete structures and casters beside each HQ: for slot 0 a Windspire, a Storm Spire and two Stormcallers; for slot 1 a Shell Forge, a Reef Workshop and two Tidecallers; and two Skai Windrunners owned by slot 1 near slot 0's base as grounding targets (`spawn_unit` does not check factions). Structures spawned by script are complete at once.
- `ui_core.json`: `ui_sandbox.json` plus four armed units per side spawned 3 tiles from the Core centre (`sim.Map.Core.Centre`).

---

## 3. Packages, in build order

### UI-0 Local command path, rejection line, session recording

- **Player action:** none new; all existing orders move onto this path.
- **Build:** one component (placeholder name `LocalCommands`, added by `AutoBootstrap`) owns the local `PlayerId` (moved from `SelectionController.localPlayerSlot`), the single sequence counter (`uint`, from 1,000,000) and `Submit(Command)`. Every package and `SelectionController` call it; there is exactly one counter, so two panels cannot send duplicate sequences. Commands are made for `sim.Clock.Tick`. `Submit` goes through an `Rts.Replay.ReplayRecorder` attached right after `Start`, before the script's commands, which `SimulationHost` also submits through the recorder (`ReplayRecorder` refuses a simulation that already holds commands). The host calls the recorder's `Step` instead of `Simulation.Step`. On leaving Play Mode or on match end (UI-10) the host writes `ReplayWriter.Write(recorder.Finish(), BuiltInMechanics.CreateCodecRegistry())` to `rts/build/replays/<local time>.rtsreplay` (outside the simulation; the clock is only used for the file name). Add `Rts.Replay` to `Rts.Game.asmdef`. An Inspector switch "Record Session" (default on).
- **Feedback:** a message line (top centre) lists the last few local rejections and notices for a few seconds each. Source: `CommandRejectedEvent` (`Command`, `Reason`) filtered on `Command.Issuer == local`. The `OnGUI` status line of `SelectionController` stays as a debug overlay, off by default.
- **Rejections shown:** every reason, through the per-package tables; `unknown_unit` (the unit died before the tick) shows "Unit lost", `not_owner` is logged only (a UI bug). Texts for the existing orders: `out_of_bounds` "Outside the map"; `unknown_target` "Target lost"; `no_weapon` "This unit cannot attack"; `target_not_enemy` "Not an enemy"; `cannot_target_layer` "Cannot hit that target"; `cannot_gather` "Only workers gather"; `unknown_field` logged only; `field_depleted` "That field is empty"; `no_drop_off` "No drop-off to return to"; `not_hq` logged only; `already_rooted`, `already_uprooted` silent (the controller filters them); `transition_in_progress` "The HQ is still moving its roots"; `not_near_resources` "Too far from resources to root".
- **Play Mode check:** in `ui_sandbox`, select a worker, right-click ground, press S. Select the HQ, press U, wait until it is Uprooted, right-click a point far from every field, and after it stops press R: the message line shows "Too far from resources to root" and the Console warning names `not_near_resources`. Leave Play Mode: a replay file exists and `ReplayCheck` prints `REPLAY OK`.
- **Out of scope:** input delay and lockstep submission (Steam transport), replay playback (later package).

### UI-1 Resource, supply and clock bar; selected-unit panel

- **Player action:** none (read-only).
- **Reads:** for the local player `sim.Players.TryGet(local, out Player p)`: `p.Common`, `p.Rare`, `p.SupplyUsed`, `p.SupplyCap`; match time `sim.Clock.Tick / sim.Clock.TicksPerSecond` as m:ss. Selected-unit panel (one unit selected): `Definition.DisplayName`, `Health` / `Definition.MaxHealth`, `ShieldPool` and `ShieldDown` when `Definition.ShieldDome != null`, `HqState` and `HqTransitionTicksRemaining` for HQs, `IsInvulnerable` and `LastStandTicksRemaining`, construction progress `1 - ConstructionTicksRemaining / EconomyRules.BuildTicks(sim, Definition)` while `IsUnderConstruction`, `GatherState` and `CarriedAmount` for workers, active statuses from `Statuses`, Momentum stacks from `MomentumMechanic.TryGetState` when the mechanic is present (`sim.Mechanics`). Several units selected: one icon per unit with a health bar, grouped by definition in `EntityId` order.
- **Notices:** `HqStateChangedEvent` and `HqLastStandEvent` for own HQs ("Last Stand"), `FieldDepletedEvent` for fields the local player gathers from.
- **Play Mode check:** in `ui_sandbox`, the bar shows the starting stock from `economy.json`; send all workers to gather (G); within 30 s Common rises and equals the value read with `execute_code`. Select the HQ, press U: the panel shows Uprooting and a countdown that reaches 0 when `HqStateChangedEvent` arrives.
- **Out of scope:** minimap, unit portraits and art, damage numbers.

### UI-2 Train buttons, queue and prerequisites

- **Player action:** with an own producer selected, click a train button (or its key). Several producers of the same definition selected: the one with the shortest `ProductionQueue`, ties lowest ID.
- **Command:** `TrainUnitCommand(tick, local, seq, Producer: producer.Id, DefinitionId: button's ID)`.
- **Button state:** cost, supply and time on the tooltip (rule 2). Greyed out with the tooltip "Requires <DisplayName>" while `EconomyRules.FirstMissingRequirement(sim, local, definition)` returns an ID; greyed out while `IsUnderConstruction` or an HQ is not `HqState.Rooted`. Cost text red when `!p.CanAfford(...)`, still clickable.
- **Queue:** the producer's `ProductionQueue` as icons, the head with progress `1 - ProductionTicksRemaining / BuildTicks(head)`, count against `MaxQueueLength`. `UnitTrainedEvent` (`Producer`, `Unit`, `DefinitionId`) gives a notice for own producers.
- **Rejection texts:** `under_construction` "Still under construction"; `cannot_train` "This building cannot train that"; `hq_not_rooted` "Root the HQ first"; `missing_prerequisite` "Requires <display name of FirstMissingRequirement>"; `queue_full` "Queue is full"; `insufficient_resources` "Not enough resources"; `supply_cap` "Not enough supply"; `unknown_definition` logged only.
- **Research slot (P-007):** the command card of a producer reserves one named, empty panel "Research" shown only for definitions that will list research items. Its contents, command and events come with the research spec; nothing is built here.
- **Play Mode check:** in `ui_sandbox` select the HQ, click the worker button: Common falls by the worker's `CostCommon` on the next tick, the queue shows one icon; after `BuildTicks` a new worker cube and a notice appear. Keep clicking until refused: the number accepted is min(`max_queue_length`, Common / `CostCommon` at the first click, rounded down) and the refusal shows "Queue is full" or "Not enough resources" accordingly. In `ui_tech` select the Windspire: the Thunderhead button is enabled; in `ui_sandbox` after building a Windspire (UI-3) it is grey with "Requires Storm Spire".
- **Out of scope:** cancelling a queued unit and rally points (no simulation command, section 4).

### UI-3 Build menu with ghost placement

- **Player action:** with own workers selected (`Definition.Economy.CanGather`), press B (or the button) to open the build menu; pick a structure; a ghost follows the cursor tile; left-click places; Shift keeps the menu armed; Esc or right-click cancels.
- **Menu list:** faction `UnitIds` whose definition has `Economy.FootprintTiles > 0`, `Economy.IsWall` false and `Hq == null`. Same button state rules as UI-2.
- **Command:** `BuildStructureCommand(tick, local, seq, Worker, DefinitionId, TileX, TileY)`. Worker: the selected worker nearest the tile centre (`sim.Terrain.CentreOf`, squared distance in Fix64), ties lowest ID.
- **Ghost colour:** red with the reason as tooltip when `EconomyRules.WallLineTileProblem(sim, worker, x, y)` returns one (it covers `out_of_bounds`, `site_unexplored`, `tile_blocked` and `site_pending` and is safe for any structure), or `FirstMissingRequirement` is not null; amber when a unit stands on the tile; green otherwise. `no_base` and `static_defence_cap` are known only from the rejection until the query in section 4 exists.
- **Pending sites:** each frame, for every own unit with `BuildState != BuildOrderState.None`, draw a faint ghost at (`BuildTileX`, `BuildTileY`) of `BuildDefinitionId` and for each tile in `BuildQueue`; placement progress from `BuildOrderStartedEvent.PlacementTicks`. Notices: `BuildOrderCancelledEvent` for own orders with its `Reason` and refund (`new_order` is silent), `StructurePlacedEvent`, `StructureCompletedEvent`.
- **Rejection texts:** `cannot_build` "Only workers can build"; `not_a_structure`, `wrong_faction`, `unknown_definition` logged only; `missing_prerequisite` as UI-2; `out_of_bounds` "Outside the map"; `site_unexplored` "You have not explored that spot"; `tile_blocked` "Blocked"; `tile_occupied` "A unit is in the way"; `site_pending` "Already being built there"; `no_base` "Defences must be built near a drop-off"; `static_defence_cap` "This base has the most defences allowed"; `insufficient_resources`, `supply_cap` as UI-2. Cancel reasons: `worker_lost` "Builder lost", `site_blocked` "Site blocked", `site_occupied` "A unit stood on the site", `unreachable` "Builder could not reach the site" (all refunded).
- **Play Mode check:** in `ui_sandbox` select a worker, B, the depot button, click an explored free tile: a ghost appears, the worker walks, `StructurePlacedEvent` replaces the ghost with a structure under construction, Common fell by the cost. Click a cliff tile: red ghost; clicking gives "Blocked". Press S on a worker walking to a site: notice of the refund.
- **Out of scope:** multi-worker construction, repair, shift-queued orders of different structures.

### UI-4 Wall and gate line drag

- **Player action:** with workers selected, open the build menu and pick a wall or gate (faction `UnitIds` with `Economy.IsWall`; gates have `IsGate` too), then left-drag from tile to tile; release sends.
- **Behaviour:** exactly `walls_and_tech.md` D.5, PC column: `WallLine.Rasterise(x0, y0, x1, y1, sim.Tuning.Economy.WallLineMaxTiles, list)` on press and each move, per-tile ghost (green; red with `WallLineTileProblem`; amber with a unit on it), the label "N x cost = total" red past the affordable count, contiguous chunks per worker, chunk i to the unassigned worker nearest its first tile (ties lowest ID, reversed when nearer its last tile).
- **Command:** one `BuildWallLineCommand(tick, local, seq, Worker, DefinitionId, Tiles)` per worker with its chunk in drag order.
- **Feedback:** queued tiles from `Unit.BuildQueue` (UI-3 drawing), `BuildLineTileSkippedEvent` (`TileX`, `TileY`, `Reason`, refund) as a red flash on the tile plus a notice, `BuildOrderCancelledEvent` as UI-3. Gates: a gate opens visually when a unit of `TerrainTile.GateTeam`'s team is on or next to its tile (presentation only; the simulation has no gate state).
- **Rejection texts:** UI-3 texts plus `not_a_wall` logged only, `no_tiles` silent (a zero-length drag sends nothing), `too_many_tiles` logged only (the rasteriser caps it), `duplicate_tile` logged only.
- **Play Mode check:** in `ui_sandbox` select two workers, drag a wall line of 8 tiles over open ground: two commands, 8 ghosts, Common falls by 8 x cost, both workers build their halves. Drag across a resource field tile: that tile is red and its skip notice appears; the rest is built.
- **Out of scope:** line commands by end points, appending to a queue, gate models (art).

### UI-5 Abilities: Downdraft and the wave

- **Player action:** with own casters selected, press the ability key (or button), then left-click a target point; Esc or right-click cancels.
- **Grounding:** button shown when a selected unit has `Definition.GroundingAbility != null`. While targeting, draw the range circle `RangeTiles` around each selected caster and the `RadiusTiles` circle at the cursor. Command `CastGroundingCommand(tick, local, seq, Caster, Target)`; caster: among selected casters with `sim.Clock.Tick >= GroundingReadyTick` and the target within `RangeTiles`, the nearest; if none qualifies, the nearest caster (so the simulation's reason is shown). Cooldown sweep: `(GroundingReadyTick - Clock.Tick) / Clock.SecondsToTicks(GroundingAbility.CooldownSeconds)`. Feedback: `GroundingCastEvent` (`Caster`, `Target`, `UnitsGrounded`) draws a pulse at `Target`; `StatusAppliedEvent` and `StatusExpiredEvent` (`Status`, `UntilTick`) show the Grounded icon on victims; `UnitLayerChangedEvent` drops the flyer's cube to the ground height.
- **Wave:** button shown when `Definition.Flood.CanCastWave`. Draw an arrow from the caster to the cursor (no strip preview; section 4). Command `CastWaveCommand(tick, local, seq, Caster, Target)`; caster: the ready one nearest the target, else the nearest. Cooldown from the flooding mechanic in `sim.Mechanics` (`TryGetNextWaveTick(caster.Id, out next)`, `WaveCooldownTicks`). Feedback: `WaveCastEvent` (`DirX`, `DirY`, `TilesFlooded`), `UnitPushedEvent` (`From`, `To`) snaps the pushed cube; water comes from `TerrainView` already.
- **Rejection texts:** `no_ability`, `not_wave_caster` logged only; `under_construction` as UI-2; `out_of_bounds` "Outside the map"; `out_of_range` "Out of range"; `on_cooldown` "Not ready"; `no_direction` "Aim away from the caster".
- **Play Mode check:** in `ui_tech` as slot 0 select a Stormcaller, press the key, click on the two enemy Windrunners: they drop to ground height, the Grounded icon shows, the button sweeps; casting again at once gives "Not ready". As slot 1 select a Tidecaller and cast toward open ground: water appears in that direction.
- **Out of scope:** autocast, smart-cast without a click, a wave strip preview.

### UI-6 Fog of war

- **Reads:** local team `sim.Players.TeamOf(local)`. Per tile after each step: `sim.Vision.IsTileVisible(team, x, y)` (clear), else `IsTileExplored` (dimmed), else black, drawn as one overlay texture over `TerrainView`. Enemy and neutral unit views are hidden unless `sim.Vision.IsUnitVisibleTo(team, unit)`; own and allied units are always drawn. Event drawing (UI-7) and enemy picking (right-click attack, A-click) skip hidden units.
- **Debug:** an editor-only "Show All" toggle (key placeholder) for testing; off in builds.
- **Play Mode check:** in `ui_sandbox` the enemy base is black at start, the own base clear. Send a worker toward the enemy: tiles it passes turn clear then dimmed after it leaves, the enemy HQ cube appears only while in sight. Ordering a structure on a black tile gives "You have not explored that spot".
- **Out of scope:** last-seen structure ghosts in explored fog (section 5, Q4), minimap fog.

### UI-7 Combat, flood and terrain event drawing

- **Reads (events, each drawn only when visible per UI-6):** `ShotFiredEvent` (`Attacker`, `Target`, `LandTick`, `IsMelee`): a tracer that lands at `LandTick`; `UnitDamagedEvent` (`Damage`, `RemainingHealth`): health bar flash; `UnitKilledEvent`: death effect; `BlastEvent` (`Centre`, `RadiusTiles`, `Victims`): a ring; `ShieldDomeHitEvent`, `ShieldDomeDownEvent`, `ShieldDomeUpEvent`: the dome bubble; `DrenchHitEvent` (`Hits`, `Grounded`); `MomentumChangedEvent` / `MomentumLostEvent`: a trail on Skai units by stack count; `TideTurnedEvent` (`Phase`): a notice; `FloodWaterRemovedEvent`: notice for own team; `QuakePathChangedEvent` (`PathId`, `Closed`) and `UnitRelocatedEvent` (`From`, `To`): notice and snap; `MoveUnreachableEvent`: a red marker at `Goal` for own units.
- **Play Mode check:** in `ui_core` attack-move one side into the other: tracers, flashes and deaths show; replay file passes `ReplayCheck`.
- **Out of scope:** art effects, sound, damage numbers.

### UI-8 Timeline bar and cycle pick

- **Reads:** shown when `sim.Cycles.IsEnabled`. A bar over the next `IntervalTicks`: now, `NextLockTick`, `NextShiftTick`; the active cycle (`ActiveCycle.DisplayName`, `Description`, since `ActiveSinceTick`), the next cycle once `IsNextLocked` (`NextCycle`), the pending pick (`PendingPick`, `PendingPickTeam`). Tide, when the flooding mechanic is present: its `TideLevel(sim, tick)` as a curve and `TideClock.IsLowTurn` / `IsHighTurn` marks over the same span using its `TideTicks`.
- **Player action:** while the local team is `Holder` and `!IsNextLocked`, the bar shows one button per `AllowedNextCycles()` entry (ascending ID); click picks.
- **Command:** `PickCycleCommand(tick, local, seq, CycleId: cycle.Id)` (never empty: the constructor throws on an empty ID).
- **Notices:** `CyclePickedEvent` (`Team`, `CycleId`) only for the local team (enemy picks are hidden until the lock), `CycleWarningEvent` (`CycleId`, `ShiftTick`, `Method`, `ChosenBy`), `CycleStartedEvent` (`CycleId`, `EndsAtTick`).
- **Rejection texts:** `cycles_disabled` logged only; `player_out_of_match` "You are out of the match"; `not_holder` "Hold the Core to pick"; `unknown_cycle` logged only; `pick_locked` "The next cycle is already locked"; `repeats_play_style` "Cannot repeat the current play style".
- **Play Mode check:** in `ui_core` as slot 0, move the own units into the Core and wait for capture (UI-9); the pick buttons appear and exclude the active cycle's play style; click one: notice; at `NextLockTick` the bar shows it as next and the buttons go away; at `NextShiftTick` `CycleStartedEvent` names it.
- **Out of scope:** cycle art and weather effects.

### UI-9 Core capture display

- **Reads:** `sim.Map.Core` (`Centre`, `RadiusTiles`) as a ground ring; holder colour from `sim.Cycles.Holder`; capture arc `CaptureProgressTicks / CaptureTicks` in `CaptureTeam`'s colour; `IsContested` pulses the ring. When `IsCoreWinEnabled`, one bar per team `HoldTicks(team) / WinTicks`.
- **Notices:** `CoreCaptureStartedEvent`, `CoreCaptureResetEvent` (`Reason`), `CoreCapturedEvent` (`Team`, `PreviousHolder`), `CoreLostEvent` (`Reason`), `CoreContestedChangedEvent`, `CoreWinEvent` (to UI-10).
- **Play Mode check:** in `ui_core` move slot 0's units into the ring: the arc fills over `core_capture_seconds` and `CoreCapturedEvent` colours the ring; move one enemy unit in: the ring pulses (contested).
- **Out of scope:** Core model and art.

### UI-10 Surrender and match end

- **Player action:** F10 opens a game menu with Surrender; a confirm dialog sends.
- **Command:** `SurrenderCommand(tick, local, seq, Player: local)`.
- **Reads:** `PlayerStateChangedEvent` (`Player`, `Previous`, `Current`, `Cause`) as a notice naming `DisplayName`; after each step `MatchOutcome.Evaluate(sim.Players)`: when `IsDecided`, stop stepping (as the harness does), finish and write the replay (UI-0), and show Victory if `Winner == local team`, Draw if `IsDraw`, else Defeat, with the cause. If the local player is out (`!IsInMatch`) but the match goes on, show "Defeated" with Keep Watching (no input) or Leave.
- **Rejection texts:** `not_self`, `unknown_player` logged only; `player_disconnected` "Disconnected players cannot surrender"; `player_out_of_match` "You are already out".
- **Play Mode check:** in `ui_sandbox` F10, Surrender, confirm: the Defeat screen; the replay passes `ReplayCheck`. Destroy the enemy HQ with units from `ui_core`: Victory with cause `hq_destroyed`.
- **Out of scope:** post-match statistics, rematch, lobby.

### Later packages (separate specs or small follow-ups)

- **UI-11 Research panel** at the tech buildings, filling the UI-2 slot, once the P-007 research spec lands (`start_research`, research events, research IDs in `requires`).
- **UI-12 Menus and skirmish setup:** main menu, map, faction and slot pick, built on `StandardMatchStart`, and a local skirmish AI opponent from `rts/src/Ai`, replacing the scripted start.
- **UI-13 Replay playback** from `rts/build/replays` through `ReplayReader`, with speed control; minimap; lobby and Steam transport (plan step 9 rest).

## 4. Simulation gaps (cloud session fills these first)

1. **Placement check for single structures.** No public query gives the `BuildStructureCommand` reason for a site: `tile_occupied`, `no_base` and `static_defence_cap` are private to `Apply` / `CheckStaticDefenceCap`. Add a pure `EconomyRules.BuildSiteProblem(Simulation, Unit worker, UnitDefinition, int x, int y)` returning the first reason the command would give, used by `Apply` itself. Until then UI-3 uses `WallLineTileProblem` and learns the rest from the rejection.
2. **Cancelling a queued unit.** No command removes an entry from `ProductionQueue` or refunds it (P-007 also has no research cancel). A player can stop a build order (Stop refunds it) but not a training order.
3. **Rally points.** No command or state; trained units appear by `EconomyRules.TryFindSpawnPosition` and stand still.
4. **Wave preview.** No public query returns the tiles a cast would flood from a caster and direction; UI-5 draws an arrow only.
5. **Ability names.** `grounding_ability` and the wave have no `display_name` or description in the unit files; UI-5 uses Unity-side labels until data has them.

## 5. Questions for the developer

1. **Keys and layout** (table below). Default: as listed.
2. **Unaffordable buttons:** greyed out, or clickable with a "Not enough resources" message? Default: clickable, cost in red (rule 4).
3. **Cancel training (gap 2):** add a cancel command with a full refund? Default: yes, refund in full, last queued first; the cloud session adds it before UI-2 is called done, or UI-2 ships without cancel.
4. **Last-seen structures in fog:** remember enemy structures seen once? Default: not in UI-6; add as renderer memory later.
5. **Enemy cycle picks:** show the enemy's pick before the lock? Default: no, shown at `CycleWarningEvent`.
6. **Rejection text home:** a C# table in `rts/src/Game` or a localisable data file? Default: one C# table now, moved to a strings file with localisation.
7. **HUD built from code or as prefabs through Funplay?** Default: from code (rule 7), matching `AutoBootstrap`.

### Keys and layout (placeholders)

| Action | Key or mouse | Where on screen |
| --- | --- | --- |
| Existing: select, box, groups, move, attack, attack-move, stop, root, uproot, gather | as in `SelectionController` (A, S, R, U, G, Ctrl+0-9, 0-9, right click) | - |
| Command card slots 1 to 8 (train, build, ability) | Q, W, E, T, Z, X, C, V | bottom right, 4 x 2 grid |
| Open build menu | B | command card |
| Cancel targeting, placement or drag | Esc, right click | - |
| Keep placing after a click | hold Shift | - |
| Game menu, surrender | F10 | centre dialog |
| Fog "Show All" (editor only) | F8 | - |
| Resource, supply, clock bar | - | top right |
| Timeline bar, cycle pick, tide | - | top centre |
| Core widget | - | under the timeline |
| Message line | - | top centre, under the Core widget |
| Selected-unit panel, queue | - | bottom centre |
| Research slot (empty) | - | command card of tech buildings |

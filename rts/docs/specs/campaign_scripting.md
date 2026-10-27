<!-- rts/docs/specs/campaign_scripting.md -->
<!-- Implementation spec for the campaign scripting layer: missions as data under rts/campaign (objectives, conditions, triggers, scripted commands), the mission loader and its validation, the mission runner that reads events and submits commands (never mutating the simulation), host signals for camera and selection, the headless MissionRunner tool, and the first tutorial mission written out in full. Part of rts/docs/specs (index: README.md); PHASE2_PLAN.md step 8, built by a Sonnet builder in rts/src/Campaign and rts/tools/MissionRunner. -->
# Spec: Campaign scripting and the first tutorial (`rts/src/Campaign`, `rts/campaign`)

## 1. Purpose and design-doc source

- Development plan, Phase 2 goal: "outside playtesters can learn the game from the tutorial". PHASE2_PLAN step 8: missions as data with triggers, objectives and scripted commands; the first tutorial teaches camera, selection, movement, gathering and building; exit test: the tutorial runs headless to its last objective.
- Technical planning / AI: "vs AI is how new free players practise, how the campaign is built". Mission enemies are ordinary skirmish AI controllers or script-driven slots.
- Architecture rule 2: missions produce the same command objects as players, applied on a set tick. Rule 8: events out, never in. The mission layer **reads** `sim.Events` and read-only state after each step and **submits** commands between steps; it never calls a mutation method. Rule 3: missions are data. Rule 9: a mission runs headless.
- Camera and selection are presentation. The simulation never sees them; the host (Unity, or the headless test inputs) reports them to the mission runner as **host signals**. They can change which commands the mission submits, never the simulation directly, and replays need no mission runner because they record the commands themselves.

## 2. Data

**Locations.** Missions: `rts/campaign/missions/<id>.json`. Headless test inputs (the scripted player): `rts/campaign/inputs/<mission id>.json`. Both are JSON with comments, `$schema_version` 1 and `$doc`, path header on line 1, every tuning value with its comment line (documentation standard). Game data (maps, units, factions, tuning, AI profiles) still comes from `rts/data` through `DataLoader`; a mission only names IDs from it.

**Mission file** (root keys, all required unless marked optional; unknown keys are errors):

| Key | Type | Range | Meaning |
| --- | --- | --- | --- |
| `id` | text ID | equals the file stem | Mission ID. |
| `display_name` | text | 1 to 60 characters | Shown to players. |
| `map` | map ID | a map in `rts/data/maps` | Map played. |
| `seed` | integer | 0 to 2 147 483 647 | Simulation seed. |
| `max_seconds` | seconds | 30 to 7200 | The mission times out (lost, reason `timeout`) after this. |
| `planetary_cycles` | boolean | | False passes no cycle list (planetary_cycles.md section 2): no shifts. |
| `players` | list | 1 to 8 entries | `id` (slot), `team`, `faction`, `name` as in command scripts, plus `controller`: `"human"`, `"script"` or `"ai:<profile>"`. At least one `human`. |
| `setup` | list of command objects | 0 to 500 | Applied on tick 0 by the script issuer (255), sequences 0 upward in file order. |
| `objectives` | list | 1 to 64 | Section below. |
| `triggers` | list | 0 to 256 | Section below. |
| `victory_objective` | objective ID | an objective | Completing it wins the mission. |
| `defeat_when` | condition | optional | True on any tick loses the mission (reason `defeat`). |

**Command objects** in `setup` and trigger actions are exactly the command-script objects (`rts/tools/BalanceHarness/README.md`) **without** `tick`, `issuer` and `sequence`, which the runner supplies. They are parsed by a new public entry point in the simulation assembly, `CommandScript.ParseCommandObject(JsonValue node, int tick, PlayerId issuer, uint sequence, string sourceName, IReadOnlyList<IScriptCommandParser> parsers)` (request to the coordinator; same type list and error messages as the script reader, plus `pick_cycle`), so faction commands such as `cast_wave` work through `BuiltInMechanics.ScriptParsers`.

**Objective**: `id` (snake_case, unique), `text` (1 to 120 characters, shown in the objective panel), `requires` (list of objective IDs that appear **earlier** in the file; empty means active from the start; so the graph has no cycles), `complete_when` (condition), optional `fail_when` (condition; true loses the mission with reason `objective_failed:<id>`), optional `hint_area` (`x`, `y`, `radius_tiles`; the UI marks it while the objective is active).

**Conditions** (closed set; `type` plus the keys listed; unknown types and keys are errors):

| Type | Keys | True when |
| --- | --- | --- |
| `event_count` | `event`, `min_count` (1 to 1000), optional `owner` (slot), `definition` (unit ID), `unit` (entity ID) | At least `min_count` matching events since the condition started counting. |
| `units_in_area` | `owner`, optional `definition`, `x`, `y`, `radius_tiles` (0.5 to 64), `min_count` (1 to 500) | That many live units of the owner (and definition) have their centre within the radius now. |
| `resources_at_least` | `player`, `common`, `rare` (0 to 1 000 000) | The player's stock is at least both now. |
| `seconds_active` | `seconds` (0 to 3600) | The objective (or, for a trigger, the mission) has been running that long. |
| `camera_in_area` | `x`, `y`, `radius_tiles` | A `camera_moved` signal with its focus in the circle arrived since counting started (latched). |
| `selection_contains` | `owner`, optional `definition`, `min_count` (1 to 500) | A `selection_changed` signal arrived whose units (resolved live) include that many matching units (latched). |
| `player_state` | `player`, `state` (`active`, `surrendered`, `disconnected`, `defeated`, `eliminated`) | That player is in that state now. |
| `all_of`, `any_of` | `conditions` (1 to 8 conditions; nesting depth at most 3) | All or any are true. |

`event_count` events and how `owner` / `definition` are matched: `unit_spawned` (`Owner`, `DefinitionId`), `unit_arrived` (live unit lookup), `resource_deposited` (`Owner`; definition of the worker), `unit_trained` (owner of `Unit`, `DefinitionId`), `structure_completed` (owner of `Structure`, `DefinitionId`), `unit_killed` (`VictimOwner`; no definition filter), `hq_state_changed` (owner of `Unit`), `core_captured` (owner matches a player on `Team`), `cycle_started` (no filters). A live lookup that finds no unit never matches. Coordinates are decimal text or numbers, in tiles, inside the map.

**Trigger**: `id` (unique), `when` (exactly one of `{"mission_started": true}`, `{"objective_activated": "<id>"}`, `{"objective_completed": "<id>"}`, `{"condition": <condition>}`), `actions` (1 to 32). Each trigger fires at most once.

**Actions**:

| Type | Keys | Effect |
| --- | --- | --- |
| `commands` | `commands` (1 to 100 command objects), optional `issuer` (`"script"`, default, or a slot whose controller is `"script"`), optional `delay_seconds` (0 to 3600, default 0) | Submits the commands for tick `now + delay`, in order. |
| `show_message` | `text` (1 to 300 characters), `seconds` (1 to 60) | Cue to the presenter. |
| `highlight_area` | `x`, `y`, `radius_tiles`, `seconds` | Cue. |
| `highlight_units` | `units` (entity IDs), `seconds` | Cue. |
| `camera_focus` | `x`, `y` | Cue: the host may pan the camera. |
| `win_mission` / `lose_mission` | `lose_mission` takes `reason` (snake_case) | Ends the mission. |

**Test inputs file** (`rts/campaign/inputs/<id>.json`): `mission` (ID), `player` (a `human` slot), `inputs`: list of `{ "on_objective": <id>, "delay_seconds": s, and exactly one of "signal" or "command" }`. A signal is `{"type": "camera_moved", "x", "y"}` or `{"type": "selection_changed", "units": [ids]}`; a command is a command object without tick, issuer and sequence, issued by `player` with the inputs' own sequence counter from 0. Each input fires once, `SecondsToTicks(delay)` ticks after its objective's first counting tick.

## 3. State

Mission state lives in `MissionRunner`, outside the simulation, and is **not hashed** (like AI memory). Per objective: `Status` (`Locked`, `Active`, `Completed`, `Failed`), `FirstCountingTick` (int ticks; the first tick whose events count, 0 for objectives active from the start), `CompletedTick` (int, -1). Per condition node (flattened, file order): event counter (int) and signal latch (bool). Per trigger: `Fired` (bool) and its condition nodes (counting from tick 0). `PendingSignals` (list stamped with the tick they arrive before). `NextSequence` per issuer the runner owns (script issuer and each `script` slot; uint from 0, the script issuer continuing after the setup commands). `Status` (`Running`, `Won`, `Lost`) and `EndReason`. `Log` (list of output lines). All collections are lists in file order or `SortedDictionary`; no float, no clock, no `System.Random`; nothing static.

## 4. Runtime algorithm

**Host loop** (the MissionRunner tool, tests, and later Unity):
```
data = DataLoader.Load(...); mission = MissionLoader.Parse(text, path, data, BuiltInMechanics.ScriptParsers)
sim = mission.CreateSimulation(data)          // map, seed, players, registry, tuning, cycles or none
sim.Start(); runner = new MissionRunner(mission, presenter); runner.Start(sim)
controllers = AI controllers for "ai:<profile>" slots; AiHost.StartAll(sim, controllers)
loop until runner.Status != Running or max_seconds reached:
    inputs?.BeforeStep(sim, runner)     // headless only: signals and human commands due at sim.Clock.Tick
    AiHost.ThinkAll(sim, controllers)
    sim.Step()
    runner.AfterStep(sim)
```
Unity calls `runner.ReportSignal(...)` from its camera and selection code between steps instead of `inputs`.

**Start(sim)**: submit `setup` (tick 0, issuer 255, sequences 0..n-1). Objectives with empty `requires` become `Active` with `FirstCountingTick` 0. Then fire triggers in file order whose `when` is `mission_started` or `objective_activated` for those objectives.

**ReportSignal(signal)**: stamps it with `sim.Clock.Tick` (the tick about to be stepped) and buffers it. Signals never reach the simulation.

**AfterStep(sim)** with `t = sim.Clock.Tick - 1` (the tick just stepped), skipped once `Status != Running`:
1. Take the signals stamped `t`.
2. Update counters: for every condition node of every `Active` objective and every unfired `condition` trigger, add the matching events of `sim.Events.Current` and latch matching signals (`FirstCountingTick <= t` is always true here).
3. Objectives in file order: an `Active` objective whose `complete_when` is true becomes `Completed` (`CompletedTick = t`, log `OBJECTIVE <id> complete tick=<t>`); otherwise if its `fail_when` is true it becomes `Failed` and the mission is lost.
4. `defeat_when` true: lost (reason `defeat`).
5. Activation, objectives in file order: a `Locked` objective whose `requires` are all `Completed` becomes `Active` with `FirstCountingTick = t + 1` (log `OBJECTIVE <id> active tick=<t + 1>`). An objective activated now is first evaluated after the next step, so chains advance one objective per tick at most.
6. Triggers in file order, unfired, whose `when` holds this tick (`objective_completed` or `objective_activated` in steps 3 and 5, or `condition` true): mark fired, run its actions in order. `commands` are submitted at `sim.Clock.Tick + SecondsToTicks(delay_seconds)` (between steps, so `Submit` is legal), issuer and sequence as in section 3. Cues go to `IMissionPresenter.Show(MissionCue)` and to the log (`CUE <type> <fields>`). `win_mission` / `lose_mission` set the status.
7. If `victory_objective` is `Completed` and the mission is not lost: won (log `MISSION won tick=<t>`). A loss on the same tick beats a win.

A timeout is checked by the host: after the step of tick `SecondsToTicks(max_seconds) - 1` a running mission is lost with reason `timeout`. The runner reads `sim.Events`, `sim.Units`, `sim.Players` and `sim.Map` only; it calls `sim.Submit` and nothing else that changes the simulation.

**Inputs.BeforeStep(sim, runner)**: for each input in file order whose objective has become `Active` and whose due tick `FirstCountingTick + SecondsToTicks(delay_seconds)` equals `sim.Clock.Tick`: report the signal, or submit the command (tick `sim.Clock.Tick`, issuer `player`, next sequence).

## 5. Commands, events and outputs

No new simulation commands or events (the mission uses the existing set plus `pick_cycle`). Rejected mission commands appear as `command_rejected` events like any other; the runner logs them as `WARN tick=<t> <description>`.

**MissionRunner tool** (`rts/tools/MissionRunner`):
```
dotnet run --project rts/tools/MissionRunner -- --data rts/data --campaign rts/campaign --mission tutorial_01
    [--inputs rts/campaign/inputs/tutorial_01.json] [--ticks N]
```
It prints the runner log lines, one `tick=<n> hash=<hex>` line per state hash (as the harness), then `MISSION <won|lost|timeout> tick=<t>[ reason=<r>]` and `STATE_HASH=<16 hex>`. Exit codes: 0 won, 2 lost or timed out, 1 any error (arguments, data, mission validation; messages name the file and field). `--ticks` caps the run below `max_seconds` (tests).

## 6. Interfaces

- New assembly `Rts.Campaign` in `rts/src/Campaign` (C# 9, `netstandard2.1`, references `Rts.Sim` and `Rts.Ai`; `Rts.Campaign.asmdef` with `noEngineReferences`, `package.json` like `rts/src/Ai`): `MissionLoader` (parse and validate into immutable `MissionDefinition`, `ObjectiveDefinition`, `ConditionDefinition`, `TriggerDefinition`, `MissionAction`), `MissionRunner`, `HostSignal` (`CameraMovedSignal`, `SelectionChangedSignal`), `IMissionPresenter` and `MissionCue`, `ConditionEvaluator` (pure: a condition node, the counters, the events and a read-only view), `MissionInputs` (headless inputs). Code lives in `rts/src` with the other assemblies; `rts/campaign` holds only data and its README (request: add `rts/src/Campaign` and `rts/tools/MissionRunner` to the CLAUDE.md folder layout and to `RtsGame.sln`).
- **Validation** (`MissionLoader`, every error a `DataValidationException` naming the mission path and a field path such as `triggers[2].actions[0].commands[0].x`): header; `id` equals the stem; map, factions, unit definitions and AI profiles exist; player slots unique and inside the map's spawn count; every coordinate inside the map; objective and trigger IDs unique; `requires` only names earlier objectives; trigger `when` names existing objectives; `victory_objective` exists; `issuer` slots have controller `script`; every command object parses; condition nesting depth at most 3. The inputs file is validated the same way against its mission.
- Must NOT: call `Step`, any internal mutation method or `sim.Rng`; issue commands as a `human` or `ai:` slot; use float, the clock, `Dictionary`/`HashSet` iteration, threads or static state; do file IO (the tool reads the files and passes text, like the harness).
- CI requests: extend `.github/scripts/validate_jsonc.py` to `rts/campaign/**/*.json`; the determinism job runs the tutorial twice with its inputs and requires identical output and `MISSION won`.

## 7. The first tutorial mission

Skai on `greybox_1v1`, one human player, no enemy, no cycles. Entity IDs: 1 Great Aerie (Rooted: it stands 4.3 tiles from fields 0 and 1), 2 and 3 Fledglings, 4 Windrunner, 5 the Fledgling hatched by trigger `hatch_helper`. Starting stock 200 common (`economy.json`); the Roost costs 150.

`rts/campaign/missions/tutorial_01.json`:
```jsonc
// rts/campaign/missions/tutorial_01.json: first tutorial mission (camera, selection, movement, gathering, building). Part of rts/campaign/missions; read by Rts.Campaign.MissionLoader and played by Rts.Campaign.MissionRunner (rts/docs/specs/campaign_scripting.md).
{
  "$schema_version": 1,
  "$doc": "Tutorial 1, First Flight: pan the camera to the Core, select the Windrunner, fly it off the plateau, gather with two Fledglings and build a Roost. One human Skai player on greybox_1v1, no enemy, no Planetary Cycles. Entity IDs: 1 Great Aerie, 2 and 3 Fledglings, 4 Windrunner, 5 the Fledgling hatched when gathering starts.",
  "id": "tutorial_01",
  "display_name": "First Flight",
  "map": "greybox_1v1",
  // Simulation seed. Unit: integer. Range: 0 to 2147483647. Source: placeholder (any fixed seed; nothing random happens without cycles or AI).
  "seed": 1,
  // Time limit after which the mission is lost with reason timeout. Unit: seconds. Range: 30 to 7200. Source: placeholder for the tutorial length.
  "max_seconds": 900,
  // Planetary Cycles off, so no warning interrupts the lesson. Unit: boolean. Range: true or false. Source: tutorial design choice.
  "planetary_cycles": false,
  "players": [
    { "id": 0, "team": 0, "faction": "skai", "name": "You", "controller": "human" }
  ],
  // Starting units on the south-west plateau; positions match rts/data/scripts/ai_vs_ai.json so the HQ starts Rooted. Unit: tiles. Source: Core gameplay loop (Opening).
  "setup": [
    { "type": "spawn_unit", "definition": "skai.great_aerie", "owner": 0, "x": "7", "y": "7" },
    { "type": "spawn_unit", "definition": "skai.fledgling", "owner": 0, "x": "6", "y": "9" },
    { "type": "spawn_unit", "definition": "skai.fledgling", "owner": 0, "x": "9", "y": "6" },
    { "type": "spawn_unit", "definition": "skai.windrunner", "owner": 0, "x": "10.5", "y": "10.5" }
  ],
  "objectives": [
    { "id": "look_around", "text": "Move the camera to the Core at the centre of the map.", "requires": [],
      // Camera focus within this circle around the Core. Unit: tiles. Range: radius 0.5 to 64. Source: tutorial placeholder.
      "complete_when": { "type": "camera_in_area", "x": "32", "y": "32", "radius_tiles": "6" },
      "hint_area": { "x": "32", "y": "32", "radius_tiles": "4" } },
    { "id": "select_rider", "text": "Select your Windrunner.", "requires": ["look_around"],
      "complete_when": { "type": "selection_contains", "owner": 0, "definition": "skai.windrunner", "min_count": 1 } },
    { "id": "move_rider", "text": "Ride the Windrunner down the east ramp to the marked spot.", "requires": ["select_rider"],
      // Arrival circle just below the east ramp (tiles 14,6 to 14,9). Unit: tiles. Range: radius 0.5 to 64. Source: tutorial placeholder.
      "complete_when": { "type": "units_in_area", "owner": 0, "definition": "skai.windrunner", "x": "18.5", "y": "8.5", "radius_tiles": "1.5", "min_count": 1 },
      "hint_area": { "x": "18.5", "y": "8.5", "radius_tiles": "1.5" } },
    { "id": "gather", "text": "Send your Fledglings to gather. Bring back two loads.", "requires": ["move_rider"],
      // Deliveries needed. Unit: resource_deposited events. Range: 1 to 1000. Source: tutorial placeholder.
      "complete_when": { "type": "event_count", "event": "resource_deposited", "owner": 0, "min_count": 2 } },
    { "id": "build_roost", "text": "Have a Fledgling build a Roost on the plateau.", "requires": ["gather"],
      "complete_when": { "type": "event_count", "event": "structure_completed", "owner": 0, "definition": "skai.roost", "min_count": 1 } }
  ],
  "triggers": [
    { "id": "welcome", "when": { "mission_started": true }, "actions": [
      // Message length on screen. Unit: seconds. Range: 1 to 60. Source: tutorial placeholder.
      { "type": "show_message", "text": "Welcome, rider. Drag the screen edge or use the arrow keys to look around.", "seconds": 8 } ] },
    { "id": "explain_select", "when": { "objective_activated": "select_rider" }, "actions": [
      { "type": "show_message", "text": "Left-click a unit to select it.", "seconds": 6 },
      { "type": "highlight_units", "units": [4], "seconds": 6 } ] },
    { "id": "explain_move", "when": { "objective_activated": "move_rider" }, "actions": [
      { "type": "show_message", "text": "Right-click the ground to move. Ramps lead off the high ground.", "seconds": 6 },
      { "type": "camera_focus", "x": "16", "y": "8" } ] },
    { "id": "hatch_helper", "when": { "objective_activated": "gather" }, "actions": [
      { "type": "show_message", "text": "A new Fledgling hatches. Right-click a crystal field with Fledglings selected to gather.", "seconds": 6 },
      // A third worker joins; scripted through the command pipeline (issuer: script). Unit: tiles. Source: tutorial placeholder.
      { "type": "commands", "issuer": "script", "delay_seconds": 0, "commands": [
        { "type": "spawn_unit", "definition": "skai.fledgling", "owner": 0, "x": "8.5", "y": "9.5" } ] },
      { "type": "highlight_area", "x": "4.5", "y": "10.5", "radius_tiles": "1.5", "seconds": 6 } ] },
    { "id": "explain_build", "when": { "objective_activated": "build_roost" }, "actions": [
      { "type": "show_message", "text": "Select a Fledgling, open the build menu and place a Roost. It trains Windrunners.", "seconds": 8 } ] },
    { "id": "farewell", "when": { "objective_completed": "build_roost" }, "actions": [
      { "type": "show_message", "text": "Well flown. Your aerie grows.", "seconds": 6 } ] }
  ],
  "victory_objective": "build_roost",
  // Losing the HQ (removed by any means) ends the lesson. Source: The mobile HQ unit (a player loses when their HQ is destroyed).
  "defeat_when": { "type": "player_state", "player": 0, "state": "defeated" }
}
```

`rts/campaign/inputs/tutorial_01.json` (the scripted player):
```jsonc
// rts/campaign/inputs/tutorial_01.json: scripted player inputs that play tutorial_01 headless to its last objective. Part of rts/campaign/inputs; read by Rts.Campaign.MissionInputs in the MissionRunner tool and the tutorial tests (rts/docs/specs/campaign_scripting.md).
{
  "$schema_version": 1,
  "$doc": "Plays tutorial_01 as player 0: camera to the Core, select the Windrunner (ID 4), move it to (18.5, 8.5), Fledglings 2 and 3 to fields 0 and 1, Fledgling 3 builds a Roost at tile (11, 11).",
  "mission": "tutorial_01",
  "player": 0,
  // Each delay is a reaction time after the objective becomes active. Unit: seconds. Range: 0 to 3600. Source: test placeholder.
  "inputs": [
    { "on_objective": "look_around", "delay_seconds": 1, "signal": { "type": "camera_moved", "x": "32", "y": "32" } },
    { "on_objective": "select_rider", "delay_seconds": 0.5, "signal": { "type": "selection_changed", "units": [4] } },
    { "on_objective": "move_rider", "delay_seconds": 0.5, "command": { "type": "move_unit", "unit": 4, "x": "18.5", "y": "8.5" } },
    { "on_objective": "gather", "delay_seconds": 0.5, "command": { "type": "gather", "unit": 2, "field": 0 } },
    { "on_objective": "gather", "delay_seconds": 0.5, "command": { "type": "gather", "unit": 3, "field": 1 } },
    { "on_objective": "build_roost", "delay_seconds": 0.5, "command": { "type": "build_structure", "unit": 3, "definition": "skai.roost", "x": 11, "y": 11 } }
  ]
}
```

## 8. Acceptance tests (`rts/tests/Sim.Tests/Campaign/`, the test project references `Rts.Campaign` and the tool)

1. **Tutorial runs headless to its last objective.** Shipped data, mission and inputs: `look_around` complete on tick 20, `select_rider` active on 21 and complete on 31, `move_rider` active on 32, then `gather` and `build_roost` complete in file order; `MISSION won` on a tick between 700 and 1500; the log holds the six `show_message` cues in trigger order; no `WARN` line; exit 0.
2. **Determinism.** Two tool runs: byte-identical stdout; `STATE_HASH` equal.
3. **Hands off.** After `AfterStep`, `sim.ComputeStateHash()`, `sim.Rng.DrawCount` and `sim.Commands` for issuers other than 255 are unchanged; `ReportSignal` changes no hash.
4. **Scripted command path.** Trigger `hatch_helper` submits exactly one `spawn_unit` with issuer 255 and sequence 4 (after setup 0 to 3), applied on the tick after `move_rider` completes; entity 5 is a `skai.fledgling` of player 0.
5. **Signals latch, events count from activation.** A `camera_moved` at (32, 32) reported before `look_around`'s condition exists (on a mission where it requires another objective) does not count later; one reported while active completes it on that tick. A `resource_deposited` of player 0 on the tick `gather` activates does not count.
6. **Timeout.** Inputs without the `move_unit` and `--ticks 2000`: `move_rider` stays active, `MISSION timeout`, exit 2. With `max_seconds` 30 and no `--ticks`: timeout on tick 599.
7. **Defeat.** A test mission whose trigger removes the HQ (`remove_unit` 1, issuer script) on `mission_started`: `MISSION lost reason=defeat` on tick 0; exit 2.
8. **Loss beats win.** A mission where the same tick completes the victory objective and triggers `lose_mission`: lost.
9. **Validation.** Each gives exit 1 and a message with the path and field: `requires` naming a later objective; a duplicate objective ID; condition type `unit_count`; a `commands` action with `issuer` 0 (a human slot); a command object with `"x": "abc"` (`triggers[3].actions[1].commands[0].x`); an unknown unit definition; an inputs file naming objective `fly`.
10. **AI slot.** A test mission with player 1 `"ai:defensive"` on team 1: the host attaches the controller; its commands carry issuer 1; the runner never issues as slot 1.
11. **Cycles option.** `planetary_cycles: true` on a 5-minute test mission shows `cycle_warning` on tick 3600; `false` shows none.

## 9. Open questions and follow-ups

- Unity side (plan step 9, on the developer's machine): load mission and input files from StreamingAssets, report camera and selection signals, show cues, the objective panel and hint areas, a mission-select menu.
- A tutorial map of its own (smaller, with a ramp next to the start) instead of `greybox_1v1`.
- Variables and counters, repeating triggers, timers that can be cancelled, scripted unit orders for `script` slots in cutscenes, a `grant_resources` command (needs a script-only simulation command, sim review).
- Online co-op missions: the runner would run on every peer from the same command stream; signals would have to travel as commands then.

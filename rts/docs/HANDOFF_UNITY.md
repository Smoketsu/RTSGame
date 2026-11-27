<!-- rts/docs/HANDOFF_UNITY.md -->
# Handoff to the Unity session (developer's PC)

Written 21 Nov 2026 by the cloud session, for the session that owns `rts/src/Game` and drives the Unity editor through Funplay. It says what changed on `main`, what to build next and in what order, and what is still on its way from the cloud side. It is a pointer to the specs, not a copy of them; where this file and a spec disagree, the spec wins.

## 1. The direction

The developer decided on 21 Nov: **get every feature usable by a player first, then make the AI work.** All AI work is paused. Almost every game feature already exists in the simulation, but Unity lets a player only move, attack, attack-move, stop, gather, and root or uproot the HQ. Closing that gap is your work.

## 2. Before you start

1. `git pull` on `main`. Check `git log -1 origin/main` is at or after the commit that added this file.
2. Read, in this order: `CLAUDE.md` (root), `rts/src/Game/CLAUDE.md`, `rts/docs/STATUS.md`, `rts/docs/specs/player_interface.md` (your build spec), `rts/docs/UNITY_SETUP.md`.
3. Open Unity on `rts/src/Game` before starting the session, so the Funplay tools connect. If they are missing, start a new session; do not edit scenes by hand.
4. Run the hash check in section 4 once, before changing anything. It has not been run since several simulation changes.

## 3. What changed in the simulation that Unity must know

- **Every state hash moved twice on 21 Nov** (research was added, then the rally point joined the hash). Old values in notes or logs no longer match.

| Check | New value |
| --- | --- |
| Harness, default script `rts/data/scripts/smoke.json` | `STATE_HASH=6032cbb453322ec8` |
| Harness, `--script rts/data/scripts/economy_smoke.json` (the script `SimulationHost` plays by default) | `STATE_HASH=3c51166b41f970cb` |

- **Research exists** (`rts/docs/specs/research_and_unlocks.md`, package R1). The Storm Spire and Reef Workshop each list research items in `Definition.Economy.Researches`. A player queues one with the ordinary train order: `train_unit(building, researchId)`. Stormcaller, Thunderhead, Ink Lobber and Leviathan cannot be trained until their item is done.
  - Queries: `Player.HasResearched(id)`, `EconomyRules.IsResearchQueued(...)`, `EconomyRules.QueueEntryTicks(...)`, `EconomyRules.FirstMissingRequirement(...)` (now also returns research IDs). Names: `Content.Researches[id].DisplayName`.
  - Event: `ResearchCompletedEvent`. Show it for the local player only; an enemy's research is never shown.
  - **A production queue can now hold a research ID.** Any Unity code that looks a queue entry up in `Content.Units` must also accept an ID found in `Content.Researches`, or it will throw.
- **The AI does not research yet.** In a match against the AI it will not build the four gated units. That is expected until the AI work resumes.

## 4. The hash check (run first, and after every package)

1. Headless: `dotnet run --project rts/tools/BalanceHarness -- --data rts/data --script rts/data/scripts/economy_smoke.json`. Note the `STATE_HASH=` line; on current `main` it is `98b2e00d9babe167`.
2. Unity: enter Play Mode with the default script and no input. The final hash it logs must be the same.
3. If they differ, stop and report it in `STATUS.md` before building anything. A mismatch means Unity and the headless run have diverged, and every later check would be meaningless.

## 5. What to build, in order

All of it is specified in `rts/docs/specs/player_interface.md`. Build one package at a time; each has its own Play Mode check.

| Order | Package | Makes usable | Waits on the cloud side? |
| --- | --- | --- | --- |
| 1 | UI-0 | One command path, a line showing why an order was refused, session recording for `ReplayCheck` | No |
| 2 | UI-1 | Resource, supply and clock bar; selected-unit panel | No |
| 3 | UI-2 | Train buttons, queue, greyed buttons for missing prerequisites | No |
| 4 | Research panel | Research buttons on the two tech buildings (`research_and_unlocks.md` section R1.10) | No; it is the train order with a research ID, so it follows UI-2 directly |
| 5 | UI-3 | Build menu with ghost placement | No; uses I1 in section 6 |
| 6 | UI-4 | Wall and gate drag | No |
| 7 | UI-5 | Downdraft and the wave | No; uses I2 and I3 in section 6 |
| 8 | UI-6 | Fog of war drawing | No |
| 9 | UI-7 | Combat, flood and terrain event drawing | No |
| 10 | UI-8 | Timeline bar and cycle pick | No |
| 11 | UI-9 | Core capture display | No |
| 12 | UI-10 | Surrender and match end | No |

Later, each needing its own short spec: menus and skirmish setup against a local AI, map picker, replay playback, cancel buttons and rally points (section 6).

## 6. Interface support from the cloud side (on `main` since 21 Nov)

Spec: `rts/docs/specs/interface_support.md`. All five items are built, reviewed and merged; the signatures below are as they are in the code.

| Item | What it gives Unity | Used by |
| --- | --- | --- |
| I1 | `EconomyRules.PlacementProblem(sim, player, definitionId, tileX, tileY)`: the reason a structure cannot go on a tile (null when it can), the same answer the build order would give; hides what the player's team cannot see | UI-3 ghost colour and tooltip |
| I2 | `FloodingMechanic.PreviewWave(sim, viewer, caster, target, area, floods)`: fills the tiles a wave cast would cover and flood; returns a refusal reason for a caster the viewer does not own. Call it between steps, on the thread that steps the simulation | UI-5 cast preview |
| I3 | Display names and descriptions for Downdraft (`grounding_ability.display_name`, `.description`) and the wave (`wave_display_name`, `wave_description`), in the unit data | UI-5 button text |
| I4 | `CancelProductionCommand(tick, issuer, sequence, producer, index, definitionId)`, full refund, for units and research; `ProductionCancelledEvent` | A cancel button on queue entries (after UI-2) |
| I5 | `SetRallyPointCommand(tick, issuer, sequence, producer, clear, point)`; a newly trained unit walks to the point; read `Unit.HasRallyPoint` and `Unit.RallyPoint` to draw it (own units only) | Right-click with a producer selected (after UI-2) |

## 7. Rules that are easy to break

These are in `CLAUDE.md` and section 1 of the interface spec; they are repeated here because each one silently breaks determinism or the architecture.

- **Commands are the only way in.** Every player action is a command through the one path UI-0 builds. Never change simulation state from `execute_code`, a MonoBehaviour or a public mutator. In Play Mode drive the game with `simulate_key_press`, `simulate_mouse_click` and `simulate_mouse_drag`; `execute_code` may only read.
- **No game number lives in Unity.** Costs, build times, ranges, queue length and timers are read from the simulation's definitions and tuning when drawn. Unity holds layout, colours, key bindings and text only.
- **No unit or faction ID in Unity code.** Button lists come from data (`Trains`, `Researches`, the faction's unit list). A grep for `"skai.` or `"water.` under `rts/src/Game` must find nothing.
- **No float in a command.** Build points with `Fix64.FromFraction` from integer cells, as `SelectionController` already does.
- **Never edit `.unity`, `.prefab` or `.asset` files as text.** The HUD is built from code at run time (`AutoBootstrap.EnsureHud`), one Canvas, reference resolution 1920 x 1080 landscape. Skip the phone-only checks of the Funplay UI skill.
- **Do not edit `rts/src/Sim`, `rts/src/Net`, `rts/src/Replay` or `rts/src/Ai`.** If the interface needs something the simulation cannot answer, write it under "Needed from the cloud session" in `STATUS.md` and build around it.
- **Documentation standard:** every new file opens with its path; every member and local says what it holds, its unit and its range.

## 8. Open questions (defaults are applied; the developer may change them)

| Question | Default in force |
| --- | --- |
| Keys and screen layout | Placeholders in the table at the end of `player_interface.md` |
| A button the player cannot afford | Still clickable; the simulation refuses and the message line says why |
| Fog: remember enemy buildings seen once | Not in UI-6; added later as renderer memory |
| Enemy cycle pick shown before the lock | No; shown at `CycleWarningEvent` |
| Where refusal texts live | One C# table in `rts/src/Game` for now |
| Build order onto an unseen tile reveals a hidden enemy through its refusal | Left as it is for now; the cloud session has recommended closing it |
| A worker rallied onto a resource field | Walks there; does not start gathering |

## 9. When you finish a package

- Commit to `main` with the two check results in the message: the Unity-against-harness hash, and `REPLAY OK` from `rts/tools/ReplayCheck` on the recorded session.
- Add a line under `[Unreleased]` in `rts/docs/CHANGELOG.md`, and update the step 9 row of `rts/docs/STATUS.md` in the same commit.
- Pull `main` before the next package; the cloud session is pushing simulation changes in parallel, and a hash that moved will be listed in `STATUS.md`.

## 10. Not for this handoff

Unit models, rigs and art import; Steam transport and lobby; the campaign layer; Earth and Fire. None of them blocks making the existing features usable.

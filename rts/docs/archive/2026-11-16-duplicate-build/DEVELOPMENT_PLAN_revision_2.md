<!-- rts/docs/archive/2026-11-16-duplicate-build/DEVELOPMENT_PLAN_revision_2.md: archived copy of rts/docs/DEVELOPMENT_PLAN.md from the 16 Nov 2026 duplicate build; not current, see the banner. -->
# Development plan, revision 2

> **ARCHIVED, NOT CURRENT.** Written on 16 Nov 2026 by a session that started on the old Phase 0 branch and did not know Phase 1 and most of Phase 2 were already built. Its status, dates and phase references are wrong. Kept only for the ideas listed in this folder's README. The current state is `rts/docs/STATUS.md`.

Revision 2 starts Phase 1 six weeks early, splits it into four playable milestones, and adds the four things revision 1 left out: a design track, an art track, a community track and a way to run the work through Claude agents without burning the weekly usage. Architecture, coding standards and the roadblock watchlist are unchanged; they now live in [CLAUDE.md](../../CLAUDE.md) and the [ADRs](decisions/README.md).

Items marked **(proposed)** change scope, money or public commitments and wait for the developer's approval. Everything else is in effect.

## Status on 16 Nov 2026

| Phase 0 exit criterion | State |
| --- | --- |
| Same scripted command stream twice gives identical state hashes, headless | Met. Pinned golden hash test and the CI `determinism` job. 135 tests. |
| Same hash in Unity as headless | **Not confirmed.** Cloud sessions cannot run Unity. The developer runs the check in [UNITY_SETUP.md](UNITY_SETUP.md). |
| A unit defined in a data file appears on screen | **Not confirmed**, same reason. |
| Asset Store packages picked | Shortlist written ([ASSET_STORE.md](ASSET_STORE.md)); the developer picks in the editor. |

Phase 0 was planned for Nov to Dec 2026 and its code was finished by mid November. The two Unity checks are the first developer task below.

## What changed from revision 1

| # | Change | Why |
| --- | --- | --- |
| 1 | Phase 1 starts 16 Nov 2026, not January; its end date (31 Mar 2027) stays. | Phase 0 finished early. Phase 1 held eleven large tasks in twelve weeks; the six spare weeks become its buffer rather than an earlier launch. |
| 2 | Phase 1 is four milestones (1A to 1D), each ending in something playable with its own exit test. | Revision 1 had one exit test in March, so a wrong turn in January would surface in March. |
| 3 | Waves and currents move from Phase 1 to Phase 2 and head the cut list. | The design doc still lists them as open; tides and depth already prove flooding in lockstep. |
| 4 | "Fun in playtests" is now a recorded gate: five logged matches and written keep-or-drop decisions. | The old exit could not fail. |
| 5 | New design track: the systems spec, faction documents and paper balance are scheduled ahead of the code that needs them. | Phase 1 needs numbers the design doc defers to a "systems spec" that no phase produced. |
| 6 | New performance budgets, measured from milestone 1A. | Revision 1 first looked at performance in Phase 3, after flow fields, flooding and the 500-game harness were built. |
| 7 | New art and audio track **(proposed, [P-001](proposals/P-001-art-pipeline.md))**. | Revision 1 had one art line ("one faction at final quality", Phase 2) and no concept art, style guide or audio. |
| 8 | New community and store track **(proposed, [P-002](proposals/P-002-community-and-store.md))**. | The store page was scheduled for the launch phase; free-to-play needs players waiting on day one. |
| 9 | Orchestration rules replace "How to switch" and the weekly rhythm. | The work now runs through subagents and sessions; the rules say which, on what model, and how much per week. |
| 10 | Developer-only task list. | Some work cannot be done by an agent, and it was nobody's line item. |
| 11 | Scope cut order. | Revision 1 said "the dates slide"; with a fixed launch window, something has to give in a known order. |

## Milestones

| Phase | When | Ships |
| --- | --- | --- |
| 0. Foundations | Done (Nov 2026), Unity checks pending | Simulation skeleton, data loader, headless runner, CI |
| 1A. Move and fight | 16 Nov to 13 Dec 2026 | Two armies path around cliffs and fight, headless and in Unity |
| 1B. Economy and HQ | 14 Dec 2026 to 24 Jan 2027 | A whole match from opening to HQ kill against a build-order bot |
| 1C. Signature mechanics | 25 Jan to 28 Feb 2027 | Flooding with tides, Momentum, Grounded, air and water layers |
| 1D. Fog, AI and the fun gate | 1 to 31 Mar 2027 | Skai versus water against the skirmish AI; playtest decisions |
| 2. Vertical slice | Apr to Jun 2027 | Online 1v1 over Steam, Planetary Cycles, full harness, tutorial |
| 3. Early access | Late summer 2027 | Public free 1v1 and vs AI with Skai and water |
| 4. Earth update | First early-access update | Earth, Tailoring, three new matchups |
| 5. Full release | After the earth update | Full rosters, ranked, team and FFA queues, paid campaign |
| 6. Fire faction | 2+ months after a stable full release | Fourth faction, four new matchups |

Dates assume 10 to 15 developer hours a week. They are re-checked at the end of each Phase 1 milestone and at the vertical slice gate.

## Phase 1: Prototype, Skai versus water

"Designer" is the Opus `designer` subagent; "builder" is Sonnet; every `rts/src/Sim` change ends with the Opus `sim-reviewer`.

### 1A. Move and fight (16 Nov to 13 Dec 2026)

| Task | Model |
| --- | --- |
| Systems spec, part 1: terrain grid, ground pathfinding, combat model, order commands ([specs/](specs/)) | Opus (designer) |
| Move faction tuning out of the shared `Tuning` type into each mechanic plug-in (Phase 0 follow-up) | Sonnet |
| Terrain grid: height, three or more elevation layers, cliffs, ramps, and the surface-state field (dry, flooded, raised, scorched) even though only dry is used yet | Opus designs, Sonnet builds |
| Flow-field pathfinding for the ground layer on static terrain | Fable or Opus |
| Combat: ranged and melee attacks, damage and armour types, death, attack-move | Sonnet |
| Unity: camera, box selection, control groups, move and attack orders issued as commands, health bars | Sonnet |
| Harness prints simulation speed (ticks per second) for the performance budget | Sonnet |
| Unit stats for the 1A units into data | Haiku |

Exit: two scripted armies cross the greybox map by its ramps and fight to the last unit, with the same final hash headless and in Unity; the developer can select units and attack-move in the editor; the harness reports simulation speed.

### 1B. Economy and HQ (14 Dec 2026 to 24 Jan 2027)

| Task | Model |
| --- | --- |
| Systems spec, part 2: economy, production, buildings, the HQ; HQ movement speed per faction settled | Opus (designer) |
| Two resources, depleting fields, rare resource on the flanks, drop-offs, supply, defence caps | Sonnet |
| Buildings, placement and production queues | Sonnet |
| Rooted and Uprooted HQ, victory on HQ death, Last Stand behind a switch | Sonnet |
| Build-order bot: plays a fixed opening and attacks on a timer (throwaway; replaced in 1D) | Sonnet |
| HQ survivability check in the harness (20 seconds against 30 supply) | Sonnet |
| Flood rules specification, written and reviewed before any flood code | Opus (designer) |

Exit: the developer plays a full match against the build-order bot from opening to HQ kill; the harness plays bot against bot to the end and reports the HQ survivability number.

### 1C. Signature mechanics (25 Jan to 28 Feb 2027)

| Task | Model |
| --- | --- |
| Flooding: water sources, downhill flow in a fixed order, depth, flood limits, the shared tide clock | Opus designs, Sonnet builds, Fable reviews |
| Flow fields update when terrain changes; air and water movement layers | Fable or Opus |
| Shared statuses: Grounded (with the move to the ground layer) and slowed | Opus designs, Sonnet builds |
| Momentum; Spoutling Drench; Stormcaller Downdraft | Sonnet |
| Flood and tide drawing in Unity (greybox water plane per tile) | Sonnet |

Exit: a flood determinism test runs three full tide cycles twice with identical hashes; a Grounded flyer is stopped by a wall and slowed by deep water; the performance budgets below still hold.

### 1D. Fog, AI and the fun gate (1 to 31 Mar 2027)

| Task | Model |
| --- | --- |
| Fog of war in the simulation; low ground cannot see high ground; airborne sight limit | Opus |
| Skirmish AI that scouts, expands, uses its faction mechanic and replaces the build-order bot | Opus |
| Faction documents and the shared stats sheet for the early-access rosters; paper balance pass | Opus designs, Haiku enters data |
| Four units per faction playable (the rest arrive in Phase 2) | Sonnet; Haiku for data |
| Playtest log template and the gate report | Haiku |

Exit (the fun gate): Skai versus water is playable against the AI; the headless runner plays AI against AI to the end; the developer has logged five matches; and `docs/decisions` records keep-or-drop decisions for Last Stand, waves and currents, and any mechanic the log marks as not fun.

## Phases 2 to 6

Task lists are as in revision 1, with these changes.

| Phase | Change from revision 1 |
| --- | --- |
| 2. Vertical slice | Adds waves and currents (if kept at the fun gate) and the remaining one or two units per faction. "Art pipeline for one faction at final quality" is replaced by the art track. Lockstep is developed against a real Steam app ID (community track). Exit is unchanged and stays the date re-check gate. |
| 3. Early access | Adds a Unity build in CI. The store page and first playtest move earlier (community track). |
| 4 to 6 | Unchanged. Earth concept art is done before Phase 4 starts; fire concept art during early access. |

## Design track

The design doc's "Game design steps" produce documents that code depends on. Each is due before the milestone that reads it.

| Deliverable | Due before | Owner |
| --- | --- | --- |
| Systems spec part 1 (terrain, pathing, combat) | 1A build tasks | Opus designer |
| Systems spec part 2 (economy, HQ) and flood rules | 1B build tasks; 1C | Opus designer |
| Faction documents and stats sheet (Skai, water) | 1D | Opus designer |
| Paper balance (cost efficiency per unit) | Fun gate | Opus designer |
| Map design guide; two more 1v1 maps | Phase 2 maps | Opus designs, Sonnet builds |
| Modes spec; campaign outline; UI wireframes | Phase 2 | Opus designer |
| Names: game title, planet, water faction, both resources | Store page (community track) | Developer |

A spec proposes starting values and lists open questions; it never settles a design question the design doc leaves open. Those go to the developer.

## Performance budgets (proposed starting values)

Measured by the harness from 1A and checked at every milestone exit. A milestone that misses one gets a profiling task before new features.

| Budget | Value | Why |
| --- | --- | --- |
| Simulation step, 400 units, flooding active | 5 ms average, 15 ms worst | The tick is 50 ms (20 ticks per second, ADR-004); the rest is for rendering and slower machines. |
| Headless speed | 50 times real time per core | 500 games of about 20 minutes per matchup per map is 10,000 game minutes: about 200 core minutes at this speed. |
| Flow-field rebuild after a terrain change | Spread so no step exceeds the worst-case budget | Tides change many tiles at once. |

## Art and audio track (proposed)

Details and the decisions needed are in [P-001](proposals/P-001-art-pipeline.md). The art direction is in [art/CONCEPT_BRIEF.md](art/CONCEPT_BRIEF.md).

| Step | When | Model or tool |
| --- | --- | --- |
| Concept round 1: Skai and water line-ups, both HQ units, a flooded map, a battle mock, earth mood | Now | Sonnet `concept-artist`, image API |
| Concept round 2: revisions and buildings | After the developer's review | Same |
| Three-model trial of generated 3D (Windrunner, Shellback, Grand Shell) in the Unity greybox | Dec 2026 | Sonnet, 3D generation API |
| Style guide and readability test at game zoom | 1B | Opus reviews, Sonnet builds |
| Proxy models for the 1D roster | 1D | Sonnet |
| Final-quality Skai and water, effects, sound packs | Phase 2 to 3 | Sonnet; developer approves |

## Community and store track (proposed)

Details are in [P-002](proposals/P-002-community-and-store.md).

| Step | When |
| --- | --- |
| Steamworks account and app ID | By the end of 1C (Feb 2027) |
| Game title decided | By April 2027 |
| Sensitivity read of the Skai art and text | May 2027 |
| Store page live with vertical-slice footage | Vertical slice gate (end of June 2027) |
| Closed online playtest | July 2027 |

## Orchestration

One orchestrator session plans and hands out work; it does not build.

| Role | Model | Does | Never |
| --- | --- | --- | --- |
| Orchestrator | Fable | Reads status, picks the week's tasks, writes briefs, launches agents, final review of simulation code | Chores, feature code |
| `designer` | Opus | Specs with acceptance tests under `rts/docs/specs` | Code |
| `builder` | Sonnet | Implements one spec with tests | Edits `rts/src/Sim` without review |
| `sim-reviewer` | Opus | Determinism review of `rts/src/Sim` and `rts/src/Net` | Rewrites code |
| `concept-artist` | Sonnet | Images from a shot list, checked against the brief | Art direction |
| `docs-writer` | Haiku | Headers, changelog, tuning comments, data entry | Logic |

Subagent or session:

- **Subagent** for a task that fits in one sitting and shares the orchestrator's branch: a spec, a review, a documentation pass, an art round.
- **Session** for a milestone-sized build that runs on its own: one session per spec, on its own branch, merged after review.
- At most one session edits `rts/src/Sim` at a time. Sessions run in parallel only when their folders do not overlap (`Sim`, `Game`, `data`, `docs/art`).
- Every brief states the goal, the files to read, the acceptance tests, what is out of scope and the report format. An agent that lacks a value stops and asks.
- Two failures on the same task move it up one tier with the failing test attached.

Weekly usage budget, sized for a 10 to 15 hour week:

| Tier | Budget per week |
| --- | --- |
| Fable | One short orchestration pass; one extra review only in weeks that change pathfinding, flooding or netcode |
| Opus | One spec and one simulation review |
| Sonnet | The build work: one or two specs |
| Haiku | One tidy pass |

If the usage limit is hit mid-week, the build stops and the developer playtests; review and documentation are never skipped to catch up.

## Developer-only tasks

| Task | When |
| --- | --- |
| Open the Unity project and run the hash check in UNITY_SETUP.md (closes Phase 0) | This week |
| Pick Asset Store packages from the shortlist | 1A |
| Review concept round 1; approve or reject P-001 and P-002 | This week |
| Open Unity and play the current build once a week; log what felt wrong | Weekly from 1A |
| Answer open questions listed at the end of each spec | Within the week the spec lands |
| Steamworks registration; names; the sensitivity read | Community track dates |

## Scope cut order

If a milestone exit slips by more than two weeks, cut from the top; each cut is recorded in `docs/decisions`.

1. Waves and currents.
2. Last Stand.
3. The third early-access map.
4. Prologue missions at early access (ship after launch).
5. Custom lobbies for 1v1v1 and FFA at early access.
6. The sixth unit per faction.

Never cut: determinism tests, the simulation review, the documentation standard.

## Risks added in revision 2

| Risk | Mitigation |
| --- | --- |
| Unity code is written by sessions that cannot run Unity | Keep `rts/src/Game` scripts thin; the weekly developer play session is the test; hash parity with headless catches logic drift |
| Weekly usage runs out before the week's work | The usage budget above; Opus and Fable only where the table says |
| Generated art drifts in style or fails at game zoom | One brief, a checklist per image, a readability test in 1B before any final-quality work (P-001) |
| The harness is too slow for 500 games per matchup per map | Headless speed budget measured from 1A |

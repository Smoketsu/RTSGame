<!-- rts/docs/STATUS.md: the first file a new session reads. Says which branch is current, which phases are done, what is being built now and what comes next. Whoever finishes a plan step updates this file in the same commit. -->
# Project status: start here

Last verified: 16 Nov 2026, against `main` at commit `050c18f`.

## Which branch

**All work happens on `main`.** Decided by the developer on 16 Nov 2026.

Before doing anything, run `git ls-remote --heads origin` and `git log -1 origin/main`. If the branch you were started on is not `main`, or is behind it, stop and switch to `main`. Do not trust the phase a branch's own documents claim.

Why this warning exists: until 16 Nov 2026 the repository's default branch on GitHub was the old `phase-0/foundations`, so a new cloud session started on Phase 0 code. A session did exactly that and rebuilt part of Phase 1 before noticing that Phase 1 and most of Phase 2 already existed. The developer changed the default branch to `main` that day; the check above stays, because a session can still be started on an old branch by name.

| Branch | What it is |
| --- | --- |
| `main` | Current. Everything below is here. |
| `phase-0/foundations`, `phase-0/foundations-pr`, `phase-1/prototype`, `phase-2/vertical-slice` | Old phase branches, fully contained in `main`. Do not build on them. |
| `claude/game-dev-plan-concept-7107ge`, `claude/phase-1a-*` | The 16 Nov duplicate Phase 1A build, made from Phase 0 by mistake. Unrelated history. Do not merge. Its art and documents were copied to `main`; `docs/proposals/P-003-decisions-to-apply.md` lists what else is worth keeping from it. |

## Where the project stands

| Phase | State | Record |
| --- | --- | --- |
| 0. Foundations | Done, merged 26 Oct 2026 | `CHANGELOG.md` |
| 1. Prototype, Skai versus water | Done, merged 26 Oct 2026 (skirmish AI 27 Oct) | `PHASE1_PLAN.md`, `CHANGELOG.md` |
| 2. Vertical slice | In progress: see the table below | `PHASE2_PLAN.md` |
| 3. Early access (late summer 2027) | Not started | Game Design Proposal |
| 4 to 6. Earth, full release, fire | Not started | Game Design Proposal |

### Phase 2, step by step (`PHASE2_PLAN.md`)

| Step | What | State |
| --- | --- | --- |
| 1 | Lockstep protocol (`rts/src/Net`), loopback transport | Built and reviewed; 2, 3 and 4 peer tests |
| 2 | Versioned replays (`rts/src/Replay`), `tools/ReplayCheck` | Built and reviewed |
| 3 | Specs: Planetary Cycles, balance harness, campaign scripting | Written (`specs/`) |
| 4 | Planetary Cycles and Core capture | Built and reviewed |
| 5 | Balance harness at full scale | **Not built.** Spec: `specs/balance_harness.md`. The harness still plays one game per run. |
| 6 | Map checklist test | Built |
| 7 | Two more 1v1 maps (three in total) | Built; both pass the checklist |
| 8 | Campaign scripting layer and the first tutorial mission | **Not built.** Spec: `specs/campaign_scripting.md`. `rts/campaign` holds only a README. |
| 9 | Unity: timeline bar, HUD, menus, lobby, surrender, Steam transport | **Not built.** Unity has the camera, selection, orders and a terrain view. |
| 10 | Unity compile and tests in CI (GameCI) | Not done; needs a `UNITY_LICENSE` secret from the developer |
| 11 | Review of every simulation, net and replay change | Ongoing rule |

## What was checked on 16 Nov 2026

- `dotnet test rts/RtsGame.sln` on `main`: 429 simulation, 33 net and 27 replay tests pass.
- The headless runner's default script gives `STATE_HASH=24ffa53a8542d783` on Linux (cloud) and on the developer's Windows PC.
- Not yet confirmed that day: the Unity compile and the Play-mode hash on `main`. Unity is pinned to `6000.6.7f1` (`rts/src/Game/ProjectSettings/ProjectVersion.txt`).

## What comes next, in order

1. Compare the developer's 16 Nov decisions with the code (`proposals/P-003-decisions-to-apply.md`) and turn the differences into spec changes.
2. Build Phase 2 step 5 (balance harness) and step 8 (campaign scripting) from their specs.
3. Phase 2 step 9, the Unity HUD and menus: done by the session on the developer's PC, which has the Unity Editor.
4. Art: the art direction is settled (`art/CONCEPT_BRIEF.md`, table of approved designs). Approved units and buildings go to 3D through the pipeline trialled in `art/models/trial2`.

## Who does what

- The session on the developer's PC owns `rts/src/Game` and anything that needs the Unity Editor (through the Funplay connector; see the "Unity editor" section of `CLAUDE.md`).
- Cloud sessions own plans, specs, art and work in `rts/src/Sim`, `rts/src/Net` and `rts/src/Replay`.
- Everyone pulls `main` before starting and pushes small commits.

## Where things are

| You want | Read |
| --- | --- |
| The game design | The Game Design Proposal (26 Oct 2026, a Claude Doc outside the repository; updated 16 Nov 2026 to the science-fiction setting) |
| The rules every session follows | `CLAUDE.md` at the repository root |
| What has been built, change by change | `CHANGELOG.md` |
| Why the architecture is the way it is | `decisions/` (ADR-001 to ADR-009) |
| How each system is specified | `specs/` |
| Decisions waiting on the developer, or waiting to be applied | `proposals/` |
| The art direction and approved art | `art/CONCEPT_BRIEF.md` |

## Keeping this file true

A session that finishes or starts a plan step changes the tables above in the same commit, and updates the "Last verified" line when it has re-run the checks. If this file and the code disagree, the code is right and this file gets fixed.

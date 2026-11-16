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
| `phase-1/prototype` | Old Phase 1 branch whose content is all in `main`. Checked on 16 Nov 2026 and safe to delete. Do not build on it. |

The developer deleted every other old branch on 16 Nov 2026, including the duplicate Phase 1A build and its archive branch; that code is gone, and what was worth keeping is in `docs/archive/2026-11-16-duplicate-build/`. New work goes on `main` or on a short-lived branch merged back into it.

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
| 5 | Balance harness at full scale | Built 16 Nov 2026 (`--batch`). Spec: `specs/balance_harness.md`. The CI job is not added yet. |
| 6 | Map checklist test | Built |
| 7 | Two more 1v1 maps (three in total) | Built; both pass the checklist |
| 8 | Campaign scripting layer and the first tutorial mission | **Not built.** Spec: `specs/campaign_scripting.md`. `rts/campaign` holds only a README. |
| 9 | Unity: timeline bar, HUD, menus, lobby, surrender, Steam transport | **Not built.** Unity has the camera, selection, orders and a terrain view. |
| 10 | Unity compile and tests in CI (GameCI) | Not done; needs a `UNITY_LICENSE` secret from the developer |
| 11 | Review of every simulation, net and replay change | Ongoing rule |

## What was checked on 16 Nov 2026

- `dotnet test rts/RtsGame.sln` on `main`: 489 simulation, 33 net and 27 replay tests pass (after P-003 packages A and B and the balance harness).
- The headless runner's default script gives `STATE_HASH=06ab47ea375f0430` on Linux (cloud) after P-003 packages A and B. Before it the hash was `24ffa53a8542d783` on Linux and on the developer's Windows PC; Windows has not been re-run.
- Unity (`6000.6.7f1`, pinned in `rts/src/Game/ProjectSettings/ProjectVersion.txt`), checked on the developer's PC through Funplay at commit `e15f943` (later commits that day changed no simulation, data or Unity file):
  - Compile: no errors, and no warnings in `Rts.Game` or `Rts.Sim`, including no CS8632 nullable warnings. The 55 warnings are all inside the Funplay package.
  - Hash parity: Unity printed `script finished at tick 1500, final hash dac9a5db27e3fdb4`; the headless runner with `--script rts/data/scripts/economy_smoke.json` printed `STATE_HASH=dac9a5db27e3fdb4`, and all 75 per-interval hashes matched. **Stale since P-003 packages A and B:** the headless hash for that script is now `f4e4a16805e63190`; the PC session must repeat the Unity check against it.
  - Not confirmed: unit count and movement in Play mode. Funplay lost its connection when Play mode reloaded scripts (its server moved from port 29309 to 29310 while the session still pointed at 29309).
- Unity's `SimulationHost` plays `scripts/economy_smoke.json` by default, not the harness default `smoke.json`. Compare Unity with the harness run on the same script, as `UNITY_SETUP.md` describes. `CLAUDE.md` said otherwise until 16 Nov 2026 and was corrected.

## What comes next, in order

1. Build `specs/p003_rules.md` (the P-003 rule changes the developer approved on 16 Nov): both packages (A combat, B unit separation) are built, reviewed by the sim-reviewer and on `main` as of 16 Nov 2026. Left over: the PC session repeats the Unity check and draws the new events (shots, dome); the skirmish AI still counts workers as armed power; separation strength is a first guess to tune once seen in Unity. Both change every state hash, so the Unity-versus-headless check must be repeated afterwards.
2. Phase 2 step 5 (balance harness batch mode) is built and on `main` (16 Nov 2026). Its first reduced batch FAILS the HQ survivability floor (a Rooted Great Aerie dies in 6.6 s to 15 Brinecasters; the floor is 20 s) and shows strong spawn bias and many undecided games; these are balance and AI findings for the developer, not harness faults. Still to do: the CI `balance-batch` job, a test guarding that `harness_batch.json` stays out of the content digest, and step 8 (campaign scripting).
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
| Old material kept for reference only | `archive/` |

## Keeping this file true

A session that finishes or starts a plan step changes the tables above in the same commit, and updates the "Last verified" line when it has re-run the checks. If this file and the code disagree, the code is right and this file gets fixed.

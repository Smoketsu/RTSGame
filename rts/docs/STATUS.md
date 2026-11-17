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

- `dotnet test rts/RtsGame.sln` on `main`: 571 simulation, 33 net and 27 replay tests pass (after AI v2 packages A, B and C).
- The headless runner's default script gives `STATE_HASH=9ece0ce1b0338423` on Linux (cloud) after the Reef Runner unit landed (AI v3 package A, 17 Nov); it was `8eb22fe6a8e7c7ff` after AI v2. Before it the hash was `24ffa53a8542d783` on Linux and on the developer's Windows PC; Windows has not been re-run.
- Unity (`6000.6.7f1`, pinned in `rts/src/Game/ProjectSettings/ProjectVersion.txt`), checked on the developer's PC through Funplay at commit `e15f943` (later commits that day changed no simulation, data or Unity file):
  - Compile: no errors, and no warnings in `Rts.Game` or `Rts.Sim`, including no CS8632 nullable warnings. The 55 warnings are all inside the Funplay package.
  - Hash parity: Unity printed `script finished at tick 1500, final hash dac9a5db27e3fdb4`; the headless runner with `--script rts/data/scripts/economy_smoke.json` printed `STATE_HASH=dac9a5db27e3fdb4`, and all 75 per-interval hashes matched. **Stale since P-003 packages A and B:** the headless hash for that script is now `38db6dd44d0ec48b` (17 Nov, Reef Runner); the PC session must repeat the Unity check against it.
  - Not confirmed: unit count and movement in Play mode. Funplay lost its connection when Play mode reloaded scripts (its server moved from port 29309 to 29310 while the session still pointed at 29309).
- Unity's `SimulationHost` plays `scripts/economy_smoke.json` by default, not the harness default `smoke.json`. Compare Unity with the harness run on the same script, as `UNITY_SETUP.md` describes. `CLAUDE.md` said otherwise until 16 Nov 2026 and was corrected.

## What comes next, in order

1. Build `specs/p003_rules.md` (the P-003 rule changes the developer approved on 16 Nov): both packages (A combat, B unit separation) are built, reviewed by the sim-reviewer and on `main` as of 16 Nov 2026. Left over: the PC session repeats the Unity check and draws the new events (shots, dome); the skirmish AI still counts workers as armed power; separation strength is a first guess to tune once seen in Unity. Both change every state hash, so the Unity-versus-headless check must be repeated afterwards.
2. Phase 2 step 5 (balance harness batch mode) is built and on `main` (16 Nov 2026). Its first reduced batch FAILS the HQ survivability floor (a Rooted Great Aerie dies in 6.6 s to 15 Brinecasters; the floor is 20 s) and shows strong spawn bias and many undecided games; these are balance and AI findings for the developer, not harness faults. The AI trace review (`reports/ai_trace_2026-11-16/README.md`, 16 Nov) explains the undecided games: the AI never expands, so each side runs out of money within 4 to 6.5 minutes. The developer has put balance work on hold until the AI can play a full game; the plan is `specs/skirmish_ai_v2.md`. Packages A (depots, doubled fields, expansion fields), B (expansion by depot and HQ move) and C (army behaviour, production mix) are built, reviewed and on `main` (16 Nov); in seven traced games (`reports/ai_v2_2026-11-16/`) every game was decided and no AI froze. A 60-game batch on `prototype_1v1` (17 Nov, `reports/ai_v2_2026-11-16/batch_prototype_1v1.md`) had every game decided and mirror matches near 50 percent. Package D (`--ai-check` with the exit criteria) is not built; the batch covers its main question. The developer (17 Nov) keeps balance on hold until the AI reacts to its opponent: the plan is `proposals/P-004-smarter-skirmish-ai.md`; stages 1 to 3 were approved on 17 Nov and are specified in `specs/skirmish_ai_v3.md` (packages A to D, built in that order by the cloud orchestrator session). Package A (ladder and response modes, Reef Runner unit) is on `main` (17 Nov) with the baseline in `reports/ai_v3_2026-11-17/`: v2 passes 0 of 10 response scenarios. Package B (scouting and enemy model) is on `main` (17 Nov), switched off in shipped profiles; it missed its 45 percent ladder gate (41.3 percent, the scout costs an early unit and nothing uses the model yet). Package C (fight predictor, counter-mix, tech on demand, static defence) is on `main` (17 Nov) as `aggressive_s2`: it beats v2 in 285 of 288 games, but ablations show that is its 30-second expansion; with equal economies the reacting AI wins only 43.8 percent, so the counter-mix was diagnosed and revised (spec Package C2, on `main`); C2 is on `main` (18 Nov) as `aggressive_s2b`: 61.1 percent against `abl_s2_blind` (ladder gate met), 3 of 9 response scenarios (bar 5, missed). Package D (fight choices, `aggressive_s3`) is on `main` (18 Nov) after two reviews: 83.3 percent against `aggressive_s2b` on `prototype_1v1` (180 of 216, ladder gate met; the Skai-versus-water cell follows the faction, 36 of 36 as Skai and 0 of 36 as water), 5 of 9 counted response scenarios (gate 6, missed; not tuned further because worker construction changes build timing and moves the scenarios). Worker-built construction (`specs/worker_construction.md`, developer decision 18 Nov) is on `main` (18 Nov): WC-1 (Sim) and WC-2 (AI and harness) are built and reviewed; every state hash changed (smoke `03674dfb1efc68b7`, economy_smoke `93762ddc16910b23`) and older replays are refused. The PC session must repeat the Unity-versus-headless hash check and draw the new build-order events. WC-3 (re-baseline, `reports/worker_construction_wc3/README.md`) is done: `aggressive_s3` beats `aggressive_s2b` in 162 of 216 games (0.750, gate met; was 0.833) and passes 5 of the 9 counted response scenarios (gate 6, missed). No AI retuning until the developer has seen it. No balance batches until its ladder passes. The developer-approved map is `data/maps/prototype_1v1.json`. Still to do: the CI `balance-batch` job, a test guarding that `harness_batch.json` stays out of the content digest, and step 8 (campaign scripting).
3. Phase 2 step 9, the Unity HUD and menus: done by the session on the developer's PC, which has the Unity Editor.
4. Art: the art direction is settled (`art/CONCEPT_BRIEF.md`, table of approved designs). All thirteen Skai and water units have an approved 3D model as of 16 Nov 2026; `art/models/batch1/README.md` lists the file for each unit. Next for art: rig and import them into Unity (PC session), then model sheets and models for the HQs and buildings.

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

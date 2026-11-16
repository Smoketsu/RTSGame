<!-- rts/docs/archive/2026-11-16-duplicate-build/README.md: what this archive folder holds and why it was kept when the duplicate-build branches were deleted. -->
# Archive: the 16 Nov 2026 duplicate build

On 16 Nov 2026 a cloud session started on the old `phase-0/foundations` branch, believed the project was at Phase 0, and rebuilt part of Phase 1 ("Phase 1A": terrain grid, flow-field pathfinding, combat, orders) that already existed on `main`. The branches were deleted the same day at the developer's request. Nothing here is current; `rts/docs/STATUS.md` is.

## What was kept, and why

| File | Worth reading for |
| --- | --- |
| `phase-1a-systems-spec.md` | Section 4.4, the design of the short hit delay the developer asked for (a per-weapon delay in ticks, a hashed pending-hit queue, hits bound to their target at fire time). Section 3.5a, the sidestep rule that stops units meeting head-on from stalling. Both are rows in `docs/proposals/P-003-decisions-to-apply.md` and would need redesigning against the real combat and movement code on `main`. |
| `DEVELOPMENT_PLAN_revision_2.md` | The orchestration rules (which model does what, subagent or session, a weekly usage budget), the performance budgets, the scope cut order and the developer-only task list. Its status table, dates and Phase 1 breakdown are wrong. |

## What was not kept in the tree

- The duplicate simulation code and its tests. The last state of that work is preserved on the branch `archive/2026-11-16-phase-1a-duplicate` (the head of the deleted branch `claude/phase-1a-exit`, plus a notice marking it as an archive; tags could not be pushed from the session that did the clean-up: 231 passing tests on that codebase, reviewed by the Opus sim-reviewer up to the combat merge). It shares no history with `main` and cannot be merged; it is a reference only.
- The concept art, art brief, proposals, tools and agent definitions from that day: these were copied to `main` unchanged and are current.

## Branches deleted on 16 Nov 2026

`phase-0/foundations`, `phase-0/foundations-pr`, `phase-1/prototype`, `phase-2/vertical-slice` (all contained in `main`), and `claude/game-dev-plan-concept-7107ge`, `claude/phase-1a-systems-spec`, `claude/phase-1a-terrain`, `claude/phase-1a-pathing`, `claude/phase-1a-combat`, `claude/phase-1a-exit` (the duplicate build).

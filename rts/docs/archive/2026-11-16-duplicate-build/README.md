<!-- rts/docs/archive/2026-11-16-duplicate-build/README.md: what this archive folder holds and why it was kept when the duplicate-build branches were deleted. -->
# Archive: the 16 Nov 2026 duplicate build

On 16 Nov 2026 a cloud session started on the old `phase-0/foundations` branch, believed the project was at Phase 0, and rebuilt part of Phase 1 ("Phase 1A": terrain grid, flow-field pathfinding, combat, orders) that already existed on `main`. The developer asked for its branches to be deleted the same day. Nothing here is current; `rts/docs/STATUS.md` is.

## What was kept, and why

| File | Worth reading for |
| --- | --- |
| `phase-1a-systems-spec.md` | Section 4.4, the design of the short hit delay the developer asked for (a per-weapon delay in ticks, a hashed pending-hit queue, hits bound to their target at fire time). Section 3.5a, the sidestep rule that stops units meeting head-on from stalling. Both are rows in `docs/proposals/P-003-decisions-to-apply.md` and would need redesigning against the real combat and movement code on `main`. |
| `DEVELOPMENT_PLAN_revision_2.md` | The orchestration rules (which model does what, subagent or session, a weekly usage budget), the performance budgets, the scope cut order and the developer-only task list. Its status table, dates and Phase 1 breakdown are wrong. |

## What was not kept in the tree

- The duplicate simulation code and its tests. The developer deleted its branches on 16 Nov 2026, so that code no longer exists anywhere; the designs worth reusing are in the spec in this folder.
- The concept art, art brief, proposals, tools and agent definitions from that day: these were copied to `main` unchanged and are current.

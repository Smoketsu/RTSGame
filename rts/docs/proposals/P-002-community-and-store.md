<!-- rts/docs/proposals/P-002-community-and-store.md: proposal to move the Steam registration, store page and first playtest earlier than revision 1 of the plan; feeds the community and store track in DEVELOPMENT_PLAN.md. -->
# P-002: Community and store timing

**Decision needed:** approve registering on Steamworks by February 2027, a store page at the vertical slice gate, and a closed playtest in July 2027.

## Problem

Revision 1 creates the Steam store page in Phase 3, the launch phase. The design doc's own risk table says a free-to-play game needs a player base on day one and names public playtests as the mitigation, but no phase schedules them. Phase 2 also builds lockstep networking over Steam, which needs a Steam app ID.

## Proposal

| Step | When | Why then |
| --- | --- | --- |
| Steamworks registration and app ID | By 28 Feb 2027 | Phase 2 networking starts in April; registration has a one-time fee per app and a verification wait. Check the current fee and wait on Steam's partner site before paying. |
| Game title decided | By April 2027 | The store page, the logo and the sensitivity read all need it |
| Sensitivity read (Skai art and text) | May 2027 | The design doc requires it before the vertical slice; the store page shows Skai art |
| Store page live, wishlists open | End of June 2027 | The vertical slice gives real footage; the page then collects wishlists for two to three months before launch |
| Closed online playtest | July 2027 | First outside players on the netcode and the tutorial, while there is still time to act on it |
| Early access | Late summer 2027 | Unchanged |

## What it costs

- The Steam app fee (one-time).
- About one week of Phase 2 time for the page: capsule art, five screenshots, a short trailer cut from vertical-slice footage, and the generated-content disclosure (see P-001).
- Developer time to run the playtest and read feedback.

## Not proposed

- No paid marketing, no Discord server and no festival entry yet. Whether to enter a Steam Next Fest before launch is decided at the vertical slice gate, when the demo's quality is known.

## If rejected

The plan keeps revision 1's timing: page and playtests in Phase 3. Networking in Phase 2 would then be developed against Steam's shared test app ID, with the real ID swapped in before launch.

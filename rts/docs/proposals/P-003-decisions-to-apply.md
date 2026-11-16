<!-- rts/docs/proposals/P-003-decisions-to-apply.md: design decisions the developer made on 16 Nov 2026 that have not been checked against, or applied to, the Phase 1 and Phase 2 code on main. Written by the orchestrator session; each row needs a check of the existing code before any change. -->
# P-003: Decisions from 16 Nov 2026 still to apply

**Decision needed:** none yet. This is a work list. Each row was decided by the developer on 16 Nov 2026 in a session that, by mistake, worked from the old Phase 0 branch and did not know Phase 1 and Phase 2 already existed. Nothing here has been compared with the code on `main`. The first task is that comparison; rows that already match are struck off, the rest become spec changes.

## Gameplay rules

| # | Decision | Detail given by the developer or settled in review |
| --- | --- | --- |
| 1 | Ranged shots uphill do less damage | Multiplier 0.75; melee unaffected. A tuning value. |
| 2 | The Spoutling (anti-air) can hit ground units too | Its edge against air comes from Drench. |
| 3 | Workers are armed | A weak melee weapon; target acquisition puts armed enemies first. |
| 4 | Ranged hits land after a short delay but cannot be dodged | "A slight delay in milliseconds, but for all intents and purposes instant." Built that day as a per-weapon delay in ticks (start: 2 ticks, 100 ms; melee 0): the hit is bound to its target when fired, damage is computed at fire time, the hit is dropped if the target is already gone and still lands if the attacker died. A shot-fired event lets Unity draw a tracer or lobbed shell. |
| 5 | Three damage types against three armour types | Soft counters only. Names were placeholders. |
| 6 | The Shellback's dome shield is an ability | It blocks ranged attacks in a radius. Not yet settled: radius, duration and cost, whether units inside can fire out, whether melee and artillery get through. |
| 7 | An empty map is dry | Floods come only from the water faction's presence. Maps are grassy fields until then. |

## Names and look

| # | Decision | Detail |
| --- | --- | --- |
| 8 | Three unit names no longer fit their art | Options offered, none picked yet. Talon Archer (a heron with a long energy rifle): Skylancer, Longshot, Stiltshot, Talon Marksman. Ink Lobber (a sea snail whose shell is a cannon lobbing water-energy shells): Tide Mortar, Surge Lobber, Conch Cannon, Brinecaster. Grasper (a mantis shrimp with club claws): Breaker, Clubclaw, Reef Breaker, Pummeler. |
| 9 | Factions are recognised by their own colours | Red is reserved for the fire faction. Open: how two players of the same faction are told apart in a mirror match. |
| 10 | Building sets drawn for Skai, water and earth | Seven buildings each, with placeholder names, in `docs/art/CONCEPT_BRIEF.md`. The data on `main` already has its own buildings (for example `roost`, `windspire`, `perch_tower`, `coral_turret`, `shell_forge`, `tidepool`); the two lists need reconciling. |

## Findings from that day's reviews worth checking on `main`

These came from Opus determinism reviews of the duplicate Phase 1A code. They may or may not apply to the code on `main`.

| # | Finding | Why it matters |
| --- | --- | --- |
| 11 | A unit walking exactly head-on into another has no sideways push | It stalls and its order ends silently. Fixed that day with a "keep right" sidestep; check whether `main`'s movement has the same gap. |
| 12 | A unit can chase a target it can never reach | For example across a cliff but inside acquisition range. Suggested rule: drop a target after a stall and ignore it for a while. |
| 13 | Listeners must not be able to cast a read-only view back to the collection behind it | Found on a pending-hit queue and on a command's unit list. |
| 14 | Flow fields rebuilt per chased target tile dominate worst-case step time | Measured at 400 units: 1.2 ms average step, about 16 ms worst. |

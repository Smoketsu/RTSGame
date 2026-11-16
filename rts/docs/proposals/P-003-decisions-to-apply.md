<!-- rts/docs/proposals/P-003-decisions-to-apply.md: design decisions the developer made on 16 Nov 2026 that have not been checked against, or applied to, the Phase 1 and Phase 2 code on main. Written by the orchestrator session; each row needs a check of the existing code before any change. -->
# P-003: Decisions from 16 Nov 2026 still to apply

**Decision needed:** none yet. This is a work list. Each row was decided by the developer on 16 Nov 2026 in a session that, by mistake, worked from the old Phase 0 branch and did not know Phase 1 and Phase 2 already existed. The comparison with `main` was done on 16 Nov 2026 and is in the last section, "Compared with `main`": 4 rows already hold, 10 need a decision or a change. The developer picks which of those become spec changes.

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

## Compared with `main` (16 Nov 2026, at commit `06cf628`)

Read from the code and data, not run. "Holds" means nothing to do. Row 14 was not measured.

| # | Result | What `main` has | What a change would touch |
| --- | --- | --- | --- |
| 1 | Not present | Height only affects vision (`VisionSystem`); damage ignores it. | One tuning value in `data/tuning/combat.json` and one check in `CombatSystem`. |
| 2 | Holds | `spoutling.json`: `can_target` is air, ground and water, and it applies Drench. | Nothing. |
| 3 | Not present | `fledgling.json` and `tender.json` have no `weapon`. Acquisition has no "armed enemies first" rule to need yet. | A weapon block in both worker files, plus the acquisition priority. |
| 4 | Not present | `CombatSystem` applies damage on the tick the weapon fires. | A per-weapon delay and a pending-hit queue that is saved in the state hash. The design is in `docs/archive/2026-11-16-duplicate-build/phase-1a-systems-spec.md` section 4.4. The largest change on this list. |
| 5 | Holds, with other names | Damage `kinetic`, `blast`, `drench` against armour `light`, `armoured`, `structure`, multipliers in `data/tuning/combat.json`. | Nothing. Use these names from now on. |
| 6 | Not present | The Shellback has a kinetic weapon only. The simulation has no ability system at all (no activated abilities, cooldowns or ability commands). | Needs a spec first: the open questions in row 6 above, and whether it is a toggle or always on. An always-on aura through the existing `IDamageModifier` hook would be the cheapest form. |
| 7 | Holds | Maps have no water field (`greybox_1v1.json`: heights, ramps, resource fields, spawns, core). Water comes only from `tidepool` and a rooted `grand_shell` (`flood_source_output_per_second`). | Nothing. |
| 8 | Old names in use | Data IDs `talon_archer`, `ink_lobber`, `grasper`; descriptions say "blast ink shots" and "tentacle melee fighter". | Once names are picked: display names and descriptions only. Keep the data IDs, because replays, scripts and tests refer to them. |
| 9 | Not present | Unity draws terrain colours only; there is no faction or player colour yet. | Belongs to Phase 2 step 9 (Unity HUD and presentation). The mirror-match question is still open. |
| 10 | Differs | Data has three buildings per faction plus the HQ: Skai `roost`, `windspire`, `perch_tower`; water `tidepool`, `shell_forge`, `coral_turret`. The art has seven per faction. | No data change now. Map the three existing buildings onto drawings; the other four drawings wait until the design adds those buildings. |
| 11 | Does not apply | `MovementSystem` has no unit-to-unit collision: units follow the flow field and may overlap, so nothing stalls head-on. | Nothing in the simulation. Whether units should push apart is a design question (they will visibly stack in Unity). |
| 12 | Holds | `CombatSystem` abandons a chase when the flow field says the target is unreachable. | Nothing. |
| 13 | Present, low risk | Several read-only properties return the private list itself, for example `ResourceFields.All`, `Simulation.SpeedModifiers`, `Simulation.DamageModifiers`, `VisionSystem.Teams`, `EventLog.Current`, `UnitStatusSet.Slows`. A listener could cast them back and change state. | Only our own Unity code reads them, so nothing breaks today. Fix when one of those files is next edited. |
| 14 | Same pattern, not measured | A chasing unit sets its move target to the target's position each tick (`CombatSystem.cs` line 153), and `FlowFieldCache` is keyed by goal tile, so each new tile a target enters can build a field. | Measure with the balance harness (Phase 2 step 5) before changing anything. |

Suggested order if the developer wants these built: rows 1 and 3 together (small, data plus one rule each), row 8 once names are picked, row 4, then row 6 after its spec.

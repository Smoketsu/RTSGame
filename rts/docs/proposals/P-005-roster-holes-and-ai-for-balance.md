<!-- rts/docs/proposals/P-005-roster-holes-and-ai-for-balance.md: proposal written 18 Nov 2026 after the developer asked for fixes to the holes in the Skai and water rosters (asymmetric, but not a rock-paper-scissors meta) and for a CPU opponent good enough that its games are valid balance data. Part 1 is roster and counters, part 2 is the AI as a balance tool. It offers options and recommendations; it decides nothing. -->
# P-005: Roster holes, soft counters, and an AI whose games count as balance data

**Decision needed:** the numbered decisions at the end. Each has a recommended default.

The developer's words (18 Nov 2026): "We want things to be somewhat asymmetric between the different factions, but want to avoid a simple rock paper scissors meta ... if [the AI is] not good, or able to compete with a human, we won't get proper balancing data."

Sources: unit files in `rts/data/units/skai` and `rts/data/units/water`; `tuning/combat.json`, `hq.json`, `flood.json`, `statuses.json`, `momentum.json`; `specs/missing_roster.md`; `reports/ai_v3_2026-11-17/README.md` (with `traces/c2_equal_cost_fights.txt`); `reports/worker_construction_wc3/README.md`; `reports/ai_v2_2026-11-16/batch_prototype_1v1.md`; `specs/balance_harness.md`; `specs/flooding.md`, `momentum.md`, `statuses.md`, `planetary_cycles.md`, `skirmish_ai_v3.md`; `art/CONCEPT_BRIEF.md`. Nothing was re-measured. Numbers marked **computed** are arithmetic on the data files; numbers marked **inference** are my reading, not something the repository states.

---

# Part 1: Roster and counter holes

## 1.1 Role matrix (from the data)

DPS is raw damage per second (`damage / cooldown_seconds`) before the damage table. V is value: `cost_common + cost_rare`. Targets: G ground, W water, A air. Rows in *italics* are the three units now being built (`specs/missing_roster.md` stats, with the developer's 18 Nov changes: Tidecaller releases flood water and casts waves; Leviathan can hit air).

**Skai**

| Unit | Layer | Targets | Armour | Damage | Range | Speed | Cost (c/r), supply | HP/V | DPS/V | What it is for |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Fledgling (worker) | ground | G W | light | kinetic 3 | melee | 2.5 | 50/0, 1 | 0.80 | 0.060 | gather, build |
| Windrunner | ground | G W A | light | kinetic 7.5 | 4 | 3.5 | 75/0, 1 | 0.80 | 0.100 | scout, raid |
| Talon Marksman | ground | G W A | light | kinetic 8 | 6 | 2.5 | 100/0, 2 | 0.90 | 0.080 | core ranged |
| Stormcaller | ground | G W | light | blast 10 | 7 | 2.0 | 150/40, 3 | 0.47 | 0.053 | bombard (splash and Downdraft are design notes only) |
| Roc Rider | air | G W A | armoured | kinetic 11.7 | 5 | 4.0 | 150/50, 3 | 0.80 | 0.058 | heavy flyer |
| *Thunderhead* | air | G W | armoured | blast 17.6 | 5 | 4.0 | 200/65, 4 | 0.72 | 0.066 | bomber |
| Perch Tower | structure | G W A | structure | kinetic 7 | 7 | 0 | 100/0, 1 | 3.00 | 0.070 | static defence |
| Great Aerie (HQ) | ground | G W A | structure rooted, armoured uprooted | kinetic 8 | 6 | 1.5 | - | - | - | HQ |

**Water**

| Unit | Layer | Targets | Armour | Damage | Range | Speed | Cost (c/r), supply | HP/V | DPS/V | What it is for |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Tender (worker) | ground | G W | light | kinetic 3 | melee | 2.0 | 50/0, 1 | 1.00 | 0.060 | gather, build |
| Reef Runner | water | G W | light | kinetic 7.5 | 4 | 4.1 | 75/0, 1 | 0.80 | 0.100 | scout |
| Pummeler | water | G W | armoured | kinetic 12 | melee 1 | 3.0 | 100/0, 2 | 1.10 | 0.120 | melee fighter |
| Spoutling | water | G W A | light | drench 5, grounds a flyer after 3 hits | 5 | 3.0 | 100/25, 2 | 0.56 | 0.040 | anti-air |
| Shellback | water | G W | armoured | kinetic 10 | 2 | 1.8 | 175/0, 3 | 1.26 (2.11 with dome) | 0.057 | tank; dome absorbs ranged shots from outside |
| Brinecaster | water | G W | light | blast 12 | 7 | 2.0 | 150/40, 2 | 0.53 | 0.063 | artillery |
| *Tidecaller* | water | none | light | unarmed | - | 3.0 | 100/25, 2 | 0.56 | 0 | waves, flood around itself |
| *Leviathan* | water | G W A | armoured | blast 26.7 | 9 | 1.5 | 300/80, 4 | 0.84 | 0.070 | capstone siege |
| Coral Turret | structure | G W A | structure | drench 6 | 7 | 0 | 100/0, 1 | 3.20 | 0.060 | static defence |
| Grand Shell (HQ) | water | G W A | structure rooted, armoured uprooted | drench 7 | 6 | 1.0 | - | - | - | HQ, flood source |

Damage table (`tuning/combat.json`): kinetic 1.0 / 0.75 / 0.5, blast 0.75 / 1.0 / 1.25, drench 1.0 / 1.0 / 0.25 against light / armoured / structure. Its own comment says "soft counters only". Uphill ranged hits do 0.75.

## 1.2 Threat classes: how each faction answers today

"Non-unit" means an answer that is not buying a unit: terrain, a mechanic, timing, the HQ.

| Threat | Skai answers | Water answers | Gap |
| --- | --- | --- | --- |
| Air (Roc Rider, Thunderhead) | Windrunner, Marksman, Roc Rider, Perch Tower, HQ. Non-unit: none | Spoutling (one mobile unit), Coral Turret, HQ; Leviathan once built. Non-unit: Grounded, but only through Spoutling hits | Water: one cheap mobile answer. Skai: no way to ground flyers (matters in the mirror and against later factions) |
| Massed armour (Pummelers) | Roc Rider (Pummelers cannot hit it at all), Stormcaller, Thunderhead. Non-unit: kite with momentum, high ground | Brinecaster (loses at equal cost), Leviathan (rare-gated). Non-unit: waves (built, never used by the AI) | Skai's best answer is immunity. Water's unit answer does not work |
| Massed light (Windrunners) | All kinetic units | Pummeler, Reef Runner. Non-unit: flood slows ground units and wipes momentum | No area damage on either side |
| Worker raid | Windrunner, Perch Tower, HQ weapon near the main | Reef Runner, Coral Turret, own flood near own fields | Depots are unarmed; Skai has no non-unit answer at an expansion. AI fails `raid` both ways |
| Turtle (static defence) | Stormcaller, Thunderhead; kinetic does 0.5 to structures | Brinecaster, Leviathan | Skai siege is thin; `turtle` Skai fails |
| Siege on own HQ | HQ weapon only | HQ weapon only | Floor fails for both HQs (H8) |

## 1.3 The holes

| # | Hole | Evidence |
| --- | --- | --- |
| H1 | Water's anti-air is thin and binary. 5 of its 7 non-worker mobile unit types cannot hit air (Reef Runner, Pummeler, Shellback, Brinecaster, Tidecaller); the one mobile answer is the Spoutling at 5 DPS. Reef Runner cannot hit air though its mirror, the Windrunner, can. | Unit files. `roc_rush_water` p2 had 0 anti-air units at 270 s under v2. The AI's water mix (3 Pummeler, 1 Shellback, 1 Brinecaster, 1 Spoutling) is 5/6 unable to touch a flyer. |
| H2 | Skai cannot ground or slow enemy flyers. Downdraft is named as a Grounded source in the design but has no data or rule. | `statuses.md` section 1; Stormcaller `$doc`. |
| H3 | The Pummeler dominates. It has the best health and damage per value of any mobile unit in either faction, and beats every other water unit at equal cost. Its only drawback is a target gate. | 15 Pummelers beat 8 Brinecasters, 14 beat 8 Shellbacks, 15 beat 20 Reef Runners (`c2_equal_cost_fights.txt`). AI water armies are 67 to 118 Pummelers per game (C2 traces). |
| H4 | Water's answer to armour loses to armour. Blast does 1.33 times kinetic against armoured, but a Brinecaster costs 1.9 times a Pummeler. **Computed:** effective DPS per value against a Pummeler is 0.063 (Brinecaster) against 0.090 (Pummeler). | Same fights; `armoured_answer` water is a "known balance failure" in WC-3. |
| H5 | Skai answers massed armour mainly by immunity: Pummelers, Shellbacks and Brinecasters cannot shoot a Roc Rider at all. That is a hard counter, the core of rock-paper-scissors. **Inference:** this, with H1, likely explains why Skai won all 36 cross-faction games as `aggressive_s3`; no diagnosis exists in the repository. | Unit files; WC-3 and package D reports. |
| H6 | The Stormcaller is a worse Brinecaster: same cost (150/40) and range (7), less health (90 against 100), less DPS (10 against 12), more supply (3 against 2). Its designed jobs, splash and Downdraft, are not built. | Unit files. |
| H7 | Static defence is very cost-efficient against kinetic armies, and Skai's army is almost all kinetic. **Computed:** a Perch Tower has 3 times the health per value of any unit, is out-ranged only by the Leviathan (the 7-range artillery ties it), and takes half damage from kinetic. One tower kills a Talon Marksman in 13 s; the Marksman needs 75 s to kill it. | `turtle` Skai fails in every report since package A. The C-package Skai AI placed 11 towers in one game (`skirmish_ai_v3.md` C2.1). |
| H8 | Rooted HQs die far too fast. They wear `structure` armour, so blast gets 1.25. **Computed and matching the report:** 15 Brinecasters do 225 DPS, so a 1500 HP Great Aerie lasts 6.7 s; the floor is 20 s against 30 supply. A Grand Shell lasted 13.3 s against 30 Windrunners. | `batch_prototype_1v1.md`; `balance_harness.md` line 115. |
| H9 | Raids on expansions have few answers. Depots are unarmed and the HQ weapon covers only the main. The AI loses 7 workers per wave in both `raid` scenarios. Part design, part AI. | WC-3 response table. |
| H10 | Nothing punishes a clumped ball. There is no area damage, so the most cost-efficient unit can simply be massed. | `missing_roster.md` section 4 (S-1 splash not built). |
| H11 | There are no tiers and almost no cost to switch tech. Every unit is one 150-common building away, and the strongest Skai unit (Roc Rider) comes from the first building. No tech structure exists. Counters are instant, so whoever scouted last wins (**inference** about play). | Producer `trains` lists; `missing_roster.md` 3.4 ("no tech-prerequisite system"). |
| H12 | Walls and the tech building are missing. The concept brief draws seven building jobs per faction; the data has four structures. Quake already says "walls and defences take damage". | `CONCEPT_BRIEF.md` lines 134 to 144; `planetary_cycles.md`. |
| H13 | The ground armies are thin mirrors. Windrunner and Reef Runner are the same numbers except speed and air targeting; Perch Tower and Coral Turret are near-copies; Stormcaller and Brinecaster are near-copies. The real asymmetry is Pummeler/Shellback against flyers. | Unit files. |
| H14 | The Shellback has no clear job. Its dome blocks ranged shots from outside, almost an immunity window against an all-ranged Skai army, yet it loses to Pummelers at equal cost. | `c2_equal_cost_fights.txt`; Shellback file. |
| H15 | The faction mechanics are not yet counter tools. Waves and flood are built but the AI never uses them; Grounded comes from one water unit; momentum's counter-play (slow or ground it) rests on that same unit. | `missing_roster.md` section 5; P-004. |
| H16 | Windrunner and Talon Marksman overlap. The Windrunner gives more health and damage per supply; their equal-cost fight ended undecided. Unmeasured (**inference**). | Unit files; `c2_equal_cost_fights.txt` (Skai fights undecided). |

Not a hole today: detection. There is no stealth in the game, so nothing needs detecting. Scouting is covered (Windrunner, Reef Runner, flyers).

## 1.4 Why the design tends toward rock-paper-scissors

The damage table is not the main cause. Its numbers already sit between 0.75 and 1.25 for units. The causes are:

1. **Target gates make answers binary.** `can_target` without `air` is immunity, not a disadvantage. A unit that cannot be hit has an infinite advantage, so the only real answer is the one unit that can hit it.
2. **Soft counters that lose at equal cost.** If the counter unit costs more than its edge (H4), it fails, and the only counters that still work are the immune ones (H5).
3. **One dominant unit.** When one unit wins every fight it is allowed to take (H3), the game reduces to "that unit, or the thing it cannot hit".
4. **No positional punishment.** Without area damage, walls or terrain that matters to a fight, there is nothing to answer except composition.
5. **Free tech switches.** With every unit one cheap building away (H11), reacting to a scout costs almost nothing, so the scout decides the fight.
6. **Mechanics outside the counter system.** Flood, waves and momentum could be answers, but nothing uses them yet (H15).

### What "asymmetric but not rock-paper-scissors" can mean in practice

General RTS design principles, not any specific game:

- **Advantage bands, not immunity.** A counter wins a fair fight by a margin (say 1.3 to 2 times value), not always. A big enough mass of the "wrong" unit still wins.
- **Answers through the faction's own mechanic.** Water answers a ground push by flooding the route, or a melee ball with a wave; Skai answers slow armies by striking and pulling back on momentum; anyone answers flyers by grounding them. The answer differs per faction, which is where the asymmetry lives.
- **Two ways for every threat, one not a purchase.** Every faction can meet every threat class in at least two different ways, and one of them is not buying a unit: terrain, a mechanic, timing, static defence, the HQ.
- **Counters that depend on position.** Area damage punishes clumping; high ground, chokes, flood and tide change who wins. A player can turn a bad matchup with better position.
- **Switching costs something.** A counter that needs a new building and time gives the attacker a window, and rewards scouting early rather than reacting last.
- **Each unit wins some fights and loses some.** No unit is best at everything for its cost; no unit has no fight it wins.

## 1.5 Options

Smallest to largest. The options stack: B builds on A, and so on.

| | Option | What it changes | Holes closed | Simulation work | Risk | How measured |
| --- | --- | --- | --- | --- | --- | --- |
| A | **Retune numbers** | Costs, health, damage, ranges. Pummeler costs more or hits less. Brinecaster cheaper or stronger against armour so it wins its fight at equal value. Stormcaller differs from the Brinecaster (e.g. cheaper, faster, shorter cooldown). Perch Tower and Coral Turret lose some health. Give Rooted HQs a new armour type (e.g. `fortified`, blast 0.75) in `combat.json`; armour types are data. | H3, H4, H6, H7 (part), H8, H14 (part), H16 | **Data only** | Low. Changes every hash and pin; old replays refused (content digest). | Unit micro benchmark (part 2, option M) for each changed pair; HQ floor in the batch; `armoured_answer` and `turtle`. |
| B | **Soften the target gates** | Add an armour type for flyers (e.g. `aerial`) and let more units hit air at a reduced multiplier (e.g. kinetic 0.5, blast 0.25 against `aerial`). Give the Reef Runner and the Shellback air targeting at that reduced rate. Keep a few gates where they are the unit's identity (Pummeler melee, Thunderhead bomber), but no faction's core army is fully gated. | H1, H5, part of H13 | **Data only** (armour types, multipliers and `can_target` are all data) | Medium. It changes what "air" means; the Spoutling's job shifts toward grounding. The AI predictor and the response checks are generic and read the table. | Air-against-ground benchmark sweeps; `air_answer`; the Skai-versus-water cell. |
| C | **Answers through mechanics** | Stormcaller Downdraft (ground flyers in an area: Skai's own grounding). AI casts waves (push melee balls) and places flood on the routes it fights on (slows ground raiders, wipes momentum). The Tidecaller makes the flood mobile. | H2, H9 (water), H15, H5 (Skai then answers armour by momentum kiting too) | **New rule** for Downdraft (an ability: new command, cooldown, hashed state; sim-reviewer pass). Waves and flood: **none** in Sim, AI work only. | Medium. First active ability for Skai. Wave use by the AI is new code. | Per-ability benchmark (water with waves against water without, same army); `raid`; `armoured_answer`. |
| D | **New rules: area damage, structure bonus, walls** | S-1 splash (`weapon.splash_radius_tiles`, already sketched in `missing_roster.md`) on Stormcaller, Thunderhead, Brinecaster, Leviathan. S-2 per-weapon structure multiplier, so siege units siege and HQs do not melt to everything blast. Walls for both factions (concept brief jobs). | H10, H7 (Skai siege), H8 (finer control), H9 (walls), H12 (walls) | **New rules**, each its own spec and review. Walls are the largest: pathing, building placement, flood blocking (flooding.md already says "walls can stand in shallow water and block the flow"). | Medium to high. Splash interacts with unit separation; walls change pathing and the AI. | Clumped-versus-spread benchmarks; `turtle`; raid with walls. |
| E | **Fill structure jobs and tiers** | A tech structure per faction that gates the top units (Roc Rider, Thunderhead, Leviathan), so switching has a cost. Add a prerequisite rule. Only add a new unit where two answers per threat still cannot be met after A to D. A water flyer is **not** proposed: water can answer air by grounding and spread anti-air instead. | H11, H12 (tech), H13 | **New rule** (prerequisites), new data, art. | High: art, AI build orders, all reports re-baselined. | Ladder variety; time-to-first-counter in response tests. |

**Recommendation:** A and B together now, as one data change measured by the micro benchmark (part 2 option M must exist first). Then C (Downdraft, and AI wave and flood use, which part 2 needs anyway). Then splash and the structure multiplier from D. Walls and E only after those are measured, and only for holes still open.

Why this order: A and B cost no simulation code and close the binary counters, which are the root of the rock-paper-scissors feel. C gives each faction its own answers, which is where asymmetry should come from. D and E are large and their need is clearer once A to C are in.

## 1.6 Proposed design rules (acceptance checks for any unit)

Each threshold is a placeholder for the developer to set. Checks 1 to 6 can be automated in the harness (part 2 option M plus a data lint).

1. **No whole-army immunity.** Every enemy unit can be attacked by at least two of each faction's mobile combat units and one static defence, and by every armed unit while Grounded.
2. **Multiplier band.** Every damage-table entry between 0.5 and 1.5 (today 0.25 to 1.25); within a damage type, highest over lowest at most 2.
3. **Counters pay for themselves.** In an equal-value open-ground fight, a unit's stated counter beats it with at least 20 percent of its value left; and the counter does not win by more than 3 to 1 in value.
4. **No dominant unit.** At equal value, no unit beats every other unit of its faction, and every unit wins at least one equal-value fight against an enemy unit.
5. **Two answers per threat, one not a purchase.** For each threat class in 1.2, every faction has two answers, and one is a mechanic, terrain, static defence or the HQ.
6. **HQ floor holds.** A Rooted HQ survives 20 s against every 30-supply army of every faction (the existing floor, extended from "strongest found" to a sweep).
7. **Stated job.** Every unit's `$doc` names its job, the fight it wins and the fight it loses.
8. **Switching costs time.** The top unit of each faction needs at least one structure beyond the first producer (only once option E exists).

---

# Part 2: A CPU opponent whose games count as balance data

## 2.1 What balance data needs, and where the AI falls short

Balance data needs two things:

- **(a) Competent, varied play** that explores the strategy space, so a strong strategy is found if it exists.
- **(b) Validity:** a way to know a result reflects the game and not an AI blind spot.

| Need | Where today's AI (`aggressive_s3`) falls short |
| --- | --- |
| (a) Competent | Never casts waves, places flood or uses any ability. Fails `raid` both ways (7 workers lost per wave), `turtle` Skai and both `armoured_answer`. Re-issues gather orders to depleted fields. |
| (a) Varied | One build order and one base composition per faction per profile. Its only random draw is the attack-threshold jitter (`attack_supply_jitter` 2). The counter-mix varies only by what it sees. |
| (b) Valid sample | Seeds barely change games, so 72 games per cell are far fewer distinct games. The Wilson intervals in the reports count each game as independent, so they are too narrow (**inference**, consistent with whole cells at 72 of 72 and 0 of 36). Seat effect: slot 0 won 57 percent between identical profiles. |
| (b) Valid opponent | Only AI against AI. Its strength against a human is unmeasured. The batch config still names the v2 profile (`harness_batch.json` `ai_profile` is `aggressive`). |
| (b) Separating game from AI | The Skai-versus-water cell follows the faction (36 of 36 as Skai, 0 of 36 as water). Nothing can tell whether that is water being weak or water's AI not using waves and flood. |

## 2.2 Options

| | Option | What it is | Cost | Tier | Verified by |
| --- | --- | --- | --- | --- | --- |
| M | **Unit micro benchmarks** | A harness mode that plays equal-value army fights with no strategic AI, swept over compositions, terrain (flat, uphill, flooded, choke), distance and both seats. Winner by remaining value, not HQ kill (the Skai fights in `c2_equal_cost_fights.txt` ended undecided because survivors could not kill an HQ). Output: a value-exchange matrix. Generalises the scratch scripts already used in C2.1. | Small: harness only, no Sim change | Opus spec, Sonnet build | Deterministic re-run; known cases reproduce (15 Pummelers beat 8 Brinecasters). |
| F | **Fix known AI bugs** | Raid response (defend expansions, not only the depot nearest the HQ; workers flee or fight) and the stale-field bug. | Small | Sonnet, Opus review | `raid` both factions; a test that no gather order targets a depleted field. |
| R | **Real randomness** | Seeded random choice of opening, timing offsets, expansion order, target ties. Uses the AI's own generator, so still deterministic per seed. | Small | Opus spec, Sonnet build | Distinct games per cell: share of games with a unique (build, first-attack time, end tick) signature, target 80 percent. |
| L | **Build-order library** | Several openings per faction as data (rush, fast expand, air, defensive, tower), sampled per game and adjusted by scouting (P-004 stage 4). Variety without a smarter AI. | Medium (mostly data) | Opus spec, Sonnet build, Haiku data comments | No opening used in more than 40 percent of games; no opening wins more than 65 percent in the mirror. |
| A | **Ability use** | Waves, flood placement, Tidecaller, later Downdraft; momentum hit-and-run. A faction's numbers are not trusted until its AI uses its mechanic. | Medium | Opus spec, Sonnet build, sim-reviewer | Per ability: AI with the ability beats the same AI without it in at least 60 percent of mirrors; response test for a wave against a melee ball. |
| X | **Exploiters and a league** | Self-play search (P-004 stage 6) tunes candidate profiles to beat the current best. Balance is judged across the whole population: a faction is weak if it loses across many profiles, not one mirror. Strategies an exploiter finds become new response scenarios. | Large (compute and harness) | Opus spec, Sonnet build | An exploiter search of a set budget cannot find a profile that beats the shipped one in more than 70 percent. |
| H | **Human benchmark** | The developer or playtesters play N games against the AI in Unity, replays recorded. Replays become scripted openings for the response suite (a replay is a command stream, so it does not react; useful for openings and timing attacks, not whole games). Needs the Unity HUD (Phase 2 step 9, not built). | Medium; developer time | PC session; Sonnet for replay-to-scenario tooling | AI win rate against the developer; list of exploits found, each turned into a response scenario. |
| S | **Honest statistics** | Report distinct games and seat-balanced cells; compute intervals over distinct games; stop treating 72 near-identical games as 72 samples. | Small | Sonnet | Report format review. |

### A stated bar for "good enough to trust"

Proposed, every number a placeholder:

1. All 10 response scenarios pass, for both factions, with no knife-edge retune (passes at the neighbouring profile values too).
2. Uses each built faction mechanic, with a measured positive effect (option A).
3. At least 80 percent distinct games per cell (option R); intervals computed over distinct games.
4. An exploiter search of a set budget finds no profile above 70 percent against it (option X; can come later).
5. Against the developer: wins at least 3 of 10, and every exploit the developer finds is a response scenario that passes.
6. Where the micro benchmark and the AI ladder disagree on which side a matchup favours, nothing is tuned until the cause is found.

## 2.3 What an AI can and cannot tell us

It can:
- Find broken things: a dominant unit, a dead unit, a degenerate opening, an HQ that dies in seconds, a unit that is never worth building.
- Catch large imbalances (a 65/35 matchup) once the bar above is met.
- Check a change did not break something else, cheaply and often.

It cannot:
- Tell 52/48 from 48/52 for human play. AI micro differs from human micro: perfect focus fire inflates kiting and ranged units; poor control deflates units that need it (Shellback dome, waves). Its results lean whichever way its skill leans.
- Measure how a counter feels, how readable a fight is, or whether a strategy is fun.
- Find mind games, feints and scouting denial.
- Stand in for the top of the human skill curve.

So fine tuning and "does this feel like rock-paper-scissors" must wait for human playtests. AI data is for finding outliers and guarding against regressions.

## 2.4 Recommendation

Order of work:

1. **M, micro benchmarks.** Cheapest, needs no strategic AI, and it is what part 1 options A and B are measured with. Give the result to the developer next to each unit change.
2. **Part 1 A + B** (data), measured with M.
3. **F** (raid and stale-field fixes) and **S** (honest statistics).
4. **R + L** (randomness and the opening library) together.
5. **A**, ability use, alongside part 1 option C.
6. **H**, the human benchmark, once the Unity HUD exists.
7. **X**, exploiters, once the AI meets bar items 1 to 3.

Stop doing:
- Reading AI-against-AI win rates as balance. Until the bar is met, they measure the AI.
- Running balance batches with the v2 `aggressive` profile.
- Retuning AI profiles to pass a response scenario on a knife edge (package D had to, and the pass did not survive the next review).
- Reporting confidence intervals over games that are not distinct.

---

# Decisions for the developer

| # | Decision | Recommended default |
| --- | --- | --- |
| 1 | Adopt the design rules in 1.6 as acceptance checks for every new or changed unit? | Yes, with the placeholder thresholds, to be tuned once the micro benchmark exists. |
| 2 | Order for part 1: A + B now, then C, then splash and structure multiplier (D), walls and tiers (E) later? | Yes. |
| 3 | Soften "cannot hit air" with a flyer armour type and reduced multipliers (option B), keeping gates only where they are a unit's identity? | Yes. Keep Pummeler melee and Thunderhead ground-only; open air targeting at a reduced rate to Reef Runner and Shellback. |
| 4 | Give Rooted HQs their own armour type so blast does not shred them (option A)? | Yes, as the first fix for the HQ floor. |
| 5 | Give water a flyer? | No. Water answers air by grounding and spread anti-air. |
| 6 | Build Stormcaller Downdraft as Skai's grounding tool (option C, a new rule)? | Yes, after A + B are measured. |
| 7 | Splash (S-1) and per-weapon structure multiplier (S-2)? | Yes, after C; one spec each. |
| 8 | Walls and a tech structure with prerequisites? | Later; decide after D is measured. |
| 9 | Build the micro benchmark harness mode first? | Yes; it gates decisions 2 to 4. |
| 10 | The "good enough to trust" bar in 2.2? | Adopt it, numbers as placeholders. |
| 11 | No balance batches and no AI-against-AI balance reading until the bar's items 1 to 3 pass? | Yes. Micro benchmarks may be read at any time. |
| 12 | Human benchmark: who plays and how many games? | The developer, 10 games per faction once the Unity HUD exists; every exploit found becomes a response scenario. |
| 13 | Exploiter and league work? | Later, after bar items 1 to 3. |

---

# Developer decisions (18 Nov 2026)

All 13 decisions are answered below.

| # | Answer | What it changes in this proposal |
| --- | --- | --- |
| 1 | Yes for rules 1, 2, 3, 4, 5, 6 and 8. Rule 7 is replaced. | Rule 7 becomes: every new or changed unit updates a matchups file that records its job, the fight it wins and the fight it loses. Nothing is stored in the unit file for this. |
| 2 | Order is C, then D, then A. "Let's get all the relevant mechanics worked out before we start tuning numbers." | No number retuning (option A) until the mechanics of C and D are built. |
| 3 | Not the flyer armour type. Instead: some water units can hit flyers, but only while they stand on water. | Option B is replaced by a conditional target rule tied to the flood. Skai keeps its air niche; water gets help against early flying raids. New simulation rule. |
| 4 | Yes. Rooted HQs should last at least 15 seconds. | The HQ floor in rule 6 and in the harness is 15 s (was 20 s). |
| 5 | Maybe: a water flyer that can fly only over terrain covered by water. | To be specified as an option, not committed. New movement rule. |
| 6 | Yes. | Stormcaller Downdraft is built. |
| 7 | Yes, high priority. "Area damage is a huge balance changer, so useless to adjust numbers till it works." | Area damage and the per-weapon structure multiplier come first among the mechanics. |
| 8 | Definitely. | Walls and a tech structure with prerequisites are built, not deferred. |
| 9 | Yes. | The equal-cost fight mode of the harness is built first. |
| 10 | Yes. | The "good enough to trust" bar is adopted, numbers as placeholders. |
| 11 | Yes. | No balance batches and no reading of AI-against-AI win rates as balance until bar items 1 to 3 pass. Equal-cost fights may be read at any time. |
| 12 | "We'll do this at the end of these." | The human benchmark (the developer plays against the AI) comes after the other work in this proposal. |
| 13 | Yes, but later. | Exploiter profiles and a league are built after bar items 1 to 3. |

<!-- rts/docs/proposals/P-006-ai-built-for-balancing.md: proposal written 21 Nov 2026 after the developer said the AI work looked like trial and error, asked for research into how skirmish AI is built in other games, and decided that the AI is to be built for balancing first. It replaces hand-written threshold rules with methods taken from that research (budget queues, calculated economy, calibrated fight prediction, one threat map) and puts a measurement method in front of them. Part of rts/docs/proposals; continues P-004 and P-005 part 2. -->
# P-006: An AI built for balancing, by methods other games use

**Decision needed:** the numbered decisions at the end. Each has a recommended default.

The developer's words (21 Nov 2026): "Kinda sounds like we're just throwing shit at the wall to see what sticks with this. Can we do some research on how they've developed this type of ai for other games and see if we can apply it?" and, asked whether the AI is mainly a source of balance data or mainly an opponent players enjoy: "It seems vital to build for balancing at this juncture".

Sources inside the repository: `specs/field_saturation.md`; `reports/sat_m`, `reports/ai_e`, `reports/ai_p`, `reports/ai_h`, `reports/ai_hq` (the last on branch `review/ai-hq`); `reports/fights_2026-11-18`; `src/Ai/FightPredictor.cs`; `tools/BalanceHarness/README.md`. Outside sources are listed in section 7, with what was read and what could not be.

---

## 1. Why change the method

The record of the four AI packages built on top of field saturation, each measured over 216 games against a copy of the AI with only that package switched off:

| Package | What it added | Result | Reading |
| --- | --- | --- | --- |
| AI-E | worker count and expansion read from the gather slots | 167 of 216 (0.773) | a real gain |
| AI-P | producers at the emptiest base; towers when a fight is predicted lost | 80 of 216 (0.370) | fails; the tower rule spends 46 percent of army value on towers |
| AI-H | harass allowed by relative speed | 111 of 216 (0.514) | cannot be told from no change |
| AI-HQ | HQ moves to a field cluster with twice the slots | 111 of 216 (0.514) before review | cannot be told from no change; review found four defects in the rule |

Three things are wrong with how these were made, and each has a known answer in other games.

1. **The rules are thresholds somebody guessed.** "Expand at 80 percent saturation", "towers from 8 enemy supply", "1.2 times the speed", "35 percent and twice the slots". Only AI-E reads a quantity the game defines (the slots), and only AI-E worked.
2. **Nothing limits what a rule may spend.** The tower rule had a cap per base and a share of army value, yet it more than doubled tower orders, because no part of the AI decides how income is divided between economy, army and defence.
3. **The measurement cannot see small effects.** With 216 games a result must be about 7 points from 50 percent to be told from chance. To see a 5-point effect at the usual confidence needs about 780 independent games. Two of the four packages were built, measured for two hours each, and left us knowing nothing.

## 2. What other games and bots do

| Problem | Method | Where it is used | What we have |
| --- | --- | --- | --- |
| How to divide spending | Priority queues: each kind of spending (workers, army, defence, tech, expansion) has a queue with a priority; free income is split between queues in proportion to priority; priorities are data and change with events | 0 A.D. Petra (`queueManager.js`, `config.priorities`): defence buildings 70, soldiers 60, a new town centre 950; non-worker queues pause when workers fall below a floor | Nothing. Each rule checks its own cap. One reservation rule (R) for rare. |
| What to build next | A goal (a set of units and tech) set by the strategy; a search finds the fastest order that reaches it; replanned on events (queue empty, key unit lost, enemy tech seen), not on a clock | UAlbertaBot: strategy sets the goal, BOSS searches, the opening book runs until an event | Opening library (authored steps), then weighted composition. No search. |
| Attack or retreat | Simulate the fight with the units in range; attack on a simulated win, retreat on a loss | UAlbertaBot with SparCraft | `FightPredictor`: a formula from unit definitions that leaves out splash, slows, grounding, momentum, flooding, hit delay and focus fire. Never checked against measured fights. |
| Where to build, defend and raid | One map of own and enemy strength over the terrain (territory, resource and danger maps), read by every placement and target decision | Petra (territory and resource maps; towers spaced from own towers and outside enemy range); influence maps are standard in the literature | Separate rules: `IsUnsafe`, "most exposed base", "safe cluster", harass target. |
| Personality and difficulty | Tables of data per AI character: openings, tech preferences, favoured missions, aggressive and defensive scalars | Halo Wars 2 strategy tables; Petra `config.js` | Profiles under `rts/data/ai`. Already done this way. |
| Trusting bots for balance | Volume and repeatability are the bots' strength; they share one set of biases, so several play styles are needed and the agents themselves must be evaluated | Research on playtesting agents | Opening library with seeded variety; trust bar from P-005; a single lineage of AI as its own opponent. |

What the research does not offer: a published account of a studio balancing an RTS from bot games alone. Academic work optimises for winning; studios optimise for enjoyment. Our use, balance data, sits nearer the academic side, which is why the StarCraft bot methods fit.

## 3. What "built for balancing" requires of the AI

Balance data is a table of win rates between factions, openings and unit mixes. It is only as good as the players that made it. So the AI must:

1. **Use every unit and mechanic it is given, competently.** A unit the AI cannot use shows up as weak. Today it does not cast Downdraft, build walls or gates, or use the anti-air-on-water rule.
2. **Play several styles at similar strength.** One strongest line of play says nothing about the others. The opening library stays; openings that only lose are a fault in the AI, not data.
3. **Not waste resources in ways a competent player would not.** Idle money, over-built towers and pointless HQ moves distort every matchup they appear in.
4. **Be measurable.** A change to the AI must be shown to help, by a method that can see the effect.
5. **Be explainable.** When a matchup is lopsided we must be able to say whether the game or the AI caused it. Calculated decisions with logged inputs allow that; stacked thresholds do not.

Not required now, and dropped from scope until balance is done: difficulty levels, personalities for flavour, behaviour that is fun to play against. The profile system keeps the door open.

## 4. The proposal

Five packages, in this order. No change under `rts/src/Sim` is expected in any of them; all AI code stays integer and `Fix64`, deterministic, and limited to what a player could know.

### M. Measurement first (harness only)

1. **Paired games.** A comparison plays each game twice with the same seed, map, factions and opening draws, once with the candidate in each seat. The result is read from the pairs that disagree, with an exact binomial test. Games that both versions win or lose from the same seat carry no information and no longer add noise.
2. **A stated power.** Every report says the smallest effect its sample could have shown. A package whose effect is below that is recorded as "no measurable effect" and is switched off, so the AI does not collect rules that do nothing.
3. **An opponent pool.** A candidate is measured against a fixed pool (`aggressive_s3`, `aggressive_s4r`, `aggressive_s5`, and each opening forced), not only against itself. The gate is: not worse against any pool member, better against at least one.
4. **Behaviour before win rate.** Each package states what the AI should visibly do (for example "tower spending stays under its budget share", "no HQ move without new income") and is tested on that in traces first. A ladder is run only when the behaviour test passes.
5. **The balance report itself.** A standing command that prints the faction and opening matrix with intervals, the share of games in which each unit type was built and each mechanic used, and money left unspent. This is the product the AI exists to feed.

### C. Check the fight predictor against measured fights (measurement, then a fix)

The `--fights` mode holds 780 equal-value fights with known outcomes. Run `FightPredictor` on every one and report how often it names the right winner, and where it is wrong (by unit, terrain and mechanic). Then, by the size of the error:

- small: add the missing effects to the formula;
- large: replace it with a short deterministic fight simulation inside the AI (the SparCraft method), using the same damage rules as the game.

Every attack, retreat, counter-mix and defence decision already depends on this predictor, so its error is in everything measured so far.

### B. Budget queues (the Petra method)

Income is divided between named queues: workers, supply, army, tech, expansion, static defence. Each has a priority in the profile data. Free resources go to queues with unmet needs in proportion to priority; a queue that can already pay for its next item is weighted down; army and defence queues pause while workers are below a floor. Events move priorities inside a stated band (a raid raises defence; a scouted tech raises the answer to it).

This replaces the tower caps and value-share check, the rare reservation rule and the rare soft cap, and gives AI-P's two ideas a place to live: producer placement becomes a placement question (package S), and "how much defence" becomes a share that cannot exceed its band by construction.

### E. Economy by calculation

Worker and depot decisions come from the game's own numbers: slots per field, trip time, walk distance, cost and build time. A worker is worth building while the income it adds over a stated horizon exceeds its cost; a depot is worth building when the next worker at home would add nothing and the depot repays itself inside the horizon. The horizon is one value in the profile with a plain meaning (how far ahead the AI plans its economy), instead of several unrelated thresholds.

A harness check goes with it: for a map and an opening, compute the best worker and depot schedule by search offline, and report how far the AI's actual income curve is from it. That tests economy quality directly, without a ladder.

AI-E is the first step of this and stays on. Its 80 percent trigger and fixed walk cover are replaced by the calculation.

### S. One threat map

A coarse grid in the AI's memory holding own strength and remembered enemy strength, each fading with distance. Every spatial decision reads it: where to place producers and towers, which expansion is safe, which field cluster the HQ may move to, which target a raid picks and whether the way there is safe.

This replaces `IsUnsafe`, the exposed-base rule, the safe-cluster test and the harass target rule, and it is where the paused work goes: AI-P's producer placement, AI-HQ's moves (with the review's rule that a move must bring new income), and the shelved raid-response package F.

### Then: A. Mechanic use

Package A of `specs/skirmish_ai_v4.md` (Downdraft, walls and gates, anti-air on water, tech choices) follows, built on B and S. It is required by section 3 item 1 and was already next in P-005's order.

## 5. What happens to work already done

| Item | Now | Under this proposal |
| --- | --- | --- |
| Field saturation (Sim) | on `main` | unchanged |
| AI-E | on `main`, on | kept; its two thresholds replaced in package E |
| AI-H | on `main`, on | re-measured with paired games in M; switched off if still no measurable effect |
| AI-P | on `main`, off | stays off; superseded by B (defence share) and S (placement) |
| AI-HQ | branch `review/ai-hq`, review fixes pushed, not measured | not merged; superseded by S and E |
| Package F (raid response) | branch `review/ai-v4-f`, failed its gate | not merged; superseded by S |
| Opening library and variety | on `main` | kept; openings outside the 35 to 65 percent band are re-read after B and E, since both change every opening's economy |
| Unit numbers | frozen (P-005) | still frozen until section 6's bar is met |

## 6. When the AI's games count as balance data

The P-005 trust bar stays. This proposal adds four checks that the balance report (M item 5) prints:

1. Every unit type and every mechanic is used in at least a stated share of games.
2. The fight predictor names the right winner in at least a stated share of the measured fights.
3. Median unspent money at fixed times is below a stated level.
4. No opening wins or loses nearly every mirror game.

The human benchmark at the end (P-005) is unchanged: bots give volume, not a judgement of what a person would do.

## 7. Outside sources

Read (through page summaries, so close paraphrase, not quotation):

- UAlbertaBot wiki: Design and Architecture, Artificial Intelligence, Build Orders, Configuration File. https://github.com/davechurchill/ualbertabot/wiki . Not stated there: what SparCraft simulates in range and time, its attack margin, and what it does not model.
- 0 A.D. Petra source: `config.js`, `queueManager.js`, `defenseManager.js`, `attackManager.js`, and the first parts of `headquarters.js` and `attackPlan.js`. https://github.com/0ad/0ad/tree/master/binaries/data/mods/public/simulation/ai/petra . Not read: `baseManager.js`, `buildManager.js`, the code that enforces tower counts.
- Halo Wars 2 commander AI, summary only. https://gamedeveloper.com/design/atriox-s-commander-ai-in-halo-wars-2

Read in full on 21 Nov 2026 (the first, third and fourth below; notes and nine corrections to this proposal's sourcing are in `rts/docs/research/rts_ai_papers.md`, section 5). The Robertson and Watson review is still known by abstract only (its site needs a login):

- Ontañón and others, "A Survey of Real-Time Strategy Game AI Research and Competition in StarCraft". https://hal.archives-ouvertes.fr/hal-00871001
- Robertson and Watson, "A Review of Real-Time Strategy Game AI". https://ojs.aaai.org/index.php/aimagazine/article/view/2478
- Churchill and Buro, "Build Order Optimization in StarCraft". https://ojs.aaai.org/index.php/AIIDE/article/view/12435
- "Winning Isn't Everything: Enhancing Game Development with Intelligent Agents". https://arxiv.org/pdf/1903.10545

Not found: anything on Age of Empires, Supreme Commander or Company of Heroes AI.

## 8. Cost and risk

- **Order is cheapest first.** M and the measurement half of C change no AI behaviour and need no ladders. They also make every later ladder worth more.
- **B, E and S each change how every game plays**, so each needs a full re-baseline (two ladders, about 95 minutes of compute) plus a behaviour test. Paired games may let a ladder be smaller; that is measured in M, not assumed.
- **B is the largest rewrite**: it replaces how the AI spends, which touches the opening steps. The openings must keep their meaning under it.
- **A calculated economy can still be wrong** if the horizon is wrong. It is one value, and the offline optimum in E shows when it is off.
- **Search and simulation cost time per decision.** The AI runs inside the headless harness thousands of times; C's simulation needs a budget per think, measured in the harness.
- **The method does not make the factions balanced.** Water still wins 3 of 36 cross-faction games. A better AI should make that number believable; fixing it is the unit work that follows.

## Decisions

1. **Adopt this order (M, C, B, E, S, then A) in place of the remaining packages of `specs/field_saturation.md`.** Default: yes.
2. **Leave AI-HQ and package F unmerged and fold their purpose into S.** Default: yes. The alternative is to measure the fixed AI-HQ first (about two hours of compute) for a result S would replace.
3. **Switch off any package with no measurable effect.** Default: yes, starting with AI-H after its paired re-measurement.
4. **Drop difficulty levels and "fun to play against" from scope until the balance bar is met.** Default: yes, as decided on 21 Nov.
5. **Fight prediction: fix the formula, or simulate.** Default: decide after the calibration report in C; no choice needed now.
6. **Allow the build environment to reach arxiv.org** so the papers known only by abstract can be read before B and C are specified. Default: optional; the specs can be written from the sources already read.

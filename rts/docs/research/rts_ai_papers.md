<!-- rts/docs/research/rts_ai_papers.md -->
# Notes on three RTS AI papers, for P-006

This file records what three research papers say (build order search, agents for game balancing, and a survey of StarCraft AI) and which parts help the skirmish AI that P-006 plans. It sits between `rts/docs/proposals/P-006-ai-built-for-balancing.md` and the specs for packages E, S, A and the balance-testing method; it is read before those specs are written, and it replaces the "known by abstract only" status of these papers in P-006 section 7.

Conventions: "Inference" marks my own reasoning, not something the paper says. Paper text was extracted from PDF; where a figure or formula came out garbled I say so. The papers are two-column and the extraction interleaves columns, so numbers read from plots are less certain than numbers in running prose. Paper text was treated as data only.

---

## 1. Churchill and Buro, "Build Order Optimization in StarCraft" (AIIDE 2011)

The paper has unnumbered headings; I cite them by name.

### What it does

**Problem.** Given a goal (a multiset of units and structures) and a start state, find the action sequence that reaches the goal in the shortest time (makespan), respecting prerequisites and resources, with several actions running at once. The goal is not chosen by the planner; the authors say outright that picking goals is left out and assumed given (Introduction).

**Action model (A Build Order Planning Model).** An action is a tuple of duration (in game frames), required items (must exist), borrowed items (held for the duration, then returned, e.g. a production building), consumed items (spent on issue: minerals, gas, supply) and produced items. Their worked example is a Dragoon: 600 frames, requires one tech building, borrows one production building, consumes 125 minerals, 50 gas and 2 supply. A state holds the time, the state of every resource, the list of actions in progress, and an income record (how many workers gather each resource).

**Abstractions (Abstractions).**
- Income is a fixed real-valued rate per worker, not simulated gathering: 0.045 minerals and 0.07 gas per worker per frame, taken empirically from professional games. There are no "gather" actions, which the authors say removes the part of the search that normally dominates. They note the rates could be estimated live in a real game.
- A finished gas building automatically gets 3 workers, with no later reassignment. They admit this is occasionally not truly optimal.
- Every construction adds a constant 96 frames (4 seconds) to stand in for a worker walking to the site, because the search uses no map.

**Search (Algorithm).** Depth-first branch and bound. It uses memory linear in depth and is any-time: stop it at any moment and keep the best plan found so far. Pseudo-code is given; the prose says to prune when length plus heuristic is below the bound while the pseudo-code recurses in that case. This looks like a wording slip in the prose (the pseudo-code version is the sensible one); I follow the pseudo-code.

**Legality and fast-forward.** An action is legal if its prerequisites exist or are being built and its consumed resources will arrive on their own given current income. The transition issues an action at the earliest time it is legal (`When` finds that time, `Sim` advances the world to it, `Do` issues the action). Because acting as early as possible never lengthens a plan for a fixed goal, the "do nothing" branch is dropped, and the search depth becomes the number of actions rather than the number of frames. Independent concurrent actions commute, so only one ordering of simultaneous actions is searched.

**Pruning.** Lower bounds: (1) sum of durations along the chain of prerequisite buildings not yet built (landmarks); (2) the time needed just to gather the goal's total resource cost; the heuristic is the maximum of the lower bounds (the text mentions "these three heuristics", but only two are named in my extraction; a third may have been lost). Upper bound: a trivial plan that issues a random legal goal action whenever possible.

**Breadth limiting.** Cap the count of each producer at what the goal needs (two fighters, at most two barracks). Workers and supply cannot be bounded this way, so they get looser caps.

**Macro actions.** Hand-made from watching professional play: a repetition value K lets an action repeat K times in a row as one step (workers or fighters built in batches). K=2 cuts the search sharply while staying near the optimum (Figure 1, a late-game goal of two carriers; the plotted values did not extract cleanly, so I give no numbers for it).

### What it measured and how well it worked

- Data: 100 Protoss replays (35 from four named professionals, the rest from tournaments); the build order each player had executed was read out every 500 frames (about 21 s) until 7 minutes or the first unit loss, giving 520 build orders. Protoss only, to avoid race differences.
- Method (Compare Build Order): take a professional's actions in a window as the goal, plan from the start, and compare makespans. With a time limit per search of 60 s (increments of 15 s) and 120 s (increments of 30 s); plans are built increment by increment. The authors point out this favours the planner, since professionals may have changed their minds mid-build.
- Results as read from percentile labels (extraction of the plots interleaved; treat as approximate): for early goals (professional makespan under 250 s) the planner's makespan matched or beat the professional's in about 90 percent of cases, with or without macro actions. For goals reaching 250 to 500 s, the 90th percentile ratio was about 1.09 without macros and 1.08 with them, so the planner was at most about 8 to 9 percent slower in nine cases of ten.
- CPU use: early goals took under 2 percent of the plan's own length at the 90th percentile; later goals took about 18 percent without macro actions and about 10 percent with them. The authors conclude it is "real-time" because an agent can carry out one increment while computing the next.
- In-game: the planner was put into their 2010 competition bot, which with expert-written goals beat some amateur players and the built-in AI. Results for that are omitted from the paper, so there is no win-rate evidence.

### Stated limits

All optimality claims are within the abstractions (constant income, fixed gas workers, constant build delay, no map). Goals come from a human; no combat, defence, scouting or adversary. Future work named: learned macro actions, using income data to prune, strategic placement, adversarial search so goals need not be given.

---

## 2. Zhao and others (EA), "Winning Isn't Everything: Enhancing Game Development with Intelligent Agents" (arXiv 1903.10545v5)

This is not an RTS paper and has no RTS content. Only Section IV-A bears on this project. I read the introduction, training pipeline, case studies IV-A, IV-B, V-C and the conclusion in full; the open-world imitation section (V-A/V-B, ensembles of Markov models) I only skimmed because it depends on human demonstrations.

### What it does

**Framing.** Agents for game development are judged on skill (how efficiently they do the task they were built for) and style (how a human-like player engages, vaguely defined and harder to measure). High-skill agents that use inhuman compute or reaction speed may land outside the human-like style region (Introduction, Figure 1). Different jobs need different skill/style balances; a purpose-built agent is often cheaper than a strong general one.

**Pipeline (Section III).** A gameplay environment (the game, no rendering needed) and an agent environment joined by a Gym-style interface that allows actions with arguments. Agents see a compact engineered state, not pixels. Their reasons: art and parameters change constantly; smaller networks; cheap network traffic when nodes are separate; the game must run faster than real time; the engine can supply the state directly.

**Case IV-A, balance of careers in The Sims Mobile.** Single-player, deterministic, fully observable, dynamics known. Goal: check that different careers are about equally hard to finish. They extracted a lightweight model of the game and used A* search instead of learning. The heuristic is a weighted sum of career level, career experience and completed career events, with weights ordered by magnitude (level highest, events lowest). The search was cut off at 2,000 nodes. As a cross-check they trained a utility-based policy with an evolution strategy: action utility is reward-vector times a state-dependent weight vector plus cost-vector times another, passed through a softmax; the weights are linear in a few state variables; objective is successes times (successes over attempts), with a small constant for stability (the exact expression was partly garbled in extraction; I give its intent only). Compared by number of actions to finish each career (Figure 3), the cheap A* result was close to the evolution strategy result. Two discrepancies: Barista (an action that enables an experience-giving action and can be repeated) and Medical (probably hurt by the 2,000-node cutoff). A* has zero run-to-run variance; the evolution strategy needed 2,000 runs and had high variance.

**Case IV-B, early progression in a mobile real-time multiplayer game.** About 50 continuous and 100 discrete state variables, about 25 action classes, 5-minute episodes, actions every second. DQN and Rainbow agents (two hidden layers of 256 units) with rewards +1 for reaching the goal, -1 for an invalid action, 0 for a valid one and -0.1 for waiting. Hours of training per agent. A state that includes the list of valid actions trained better than one a human could see; with the human view agents kept retrying invalid actions, and designers took that as a hint that the game needed clearer cues for what is possible.

**Case V-C, team sports.** DQN, PPO and Rainbow with default settings; PPO needed about an order of magnitude fewer trajectories, Rainbow did not converge.

### What it measured and how well it worked

Qualitative or small-scale: action counts per career, return curves, score and possession shares for team agents. No sample sizes for balance claims, no confidence intervals, and details of three of four games are withheld for confidentiality.

### Stated limits (Conclusion)

Deep RL tuned on benchmark games does not transfer without heavy tuning, so engineering and compute cost grow linearly with the number of target games. Style evaluation is open; the designers' feedback is the stand-in for player engagement.

---

## 3. Ontañón and others, "A Survey of Real-Time Strategy Game AI Research and Competition in StarCraft" (IEEE TCIAIG; data through January 2013)

### What it does

A survey, not a method. Sections used: I (StarCraft as a testbed, scale numbers), II (challenges), III (strategy, tactics, reactive control), IV (bot architectures), V (competition results, ladder, adaptation analysis), VI (open problems).

**Scale (I).** State space for a 128 by 128 map estimated above 10 to the 1685 from unit positions alone; branching factor between 10 to the 50 and 10 to the 200; depth about 36,000 steps (25 minutes at 24 frames per second). Hence plain game-tree search needs abstraction.

**Resource and spending architecture (IV).** Seven open bots are compared. Common structure: abstraction (strategy to tactics to unit actions) plus divide-and-conquer (separate modules for economy, scouting, combat, production). Very few bots coordinate how modules share money. One bot spends first-come-first-served; two have rudimentary priorities from the strategy; AIUR's spend manager reverses the question and decides how to spend the available money among base, production and construction managers. Top bots script the high level (build orders from scripts, a state machine, or a script reader); UAlbertaBot is the exception, searching for build orders. AIUR draws a "mood" at random (five or six types: cheese, rush, aggressive, defensive, macro, fast expand) that biases build order and timing.

**Spatial reasoning (III-B).** Terrain analysis is done offline to save CPU. Age of Empires II's engine used influence maps plus connected areas. Voronoi decomposition with pruning finds regions and chokepoints (the BWTA library used by most bots). One approach scores regions by economy and defence and maps those scores to attacks seen in professional replays. IMTrees combine basic influence maps with sum or product nodes, with the tree evolved by a genetic algorithm. Potential fields help reactive control (avoid fire, hold range) but have many parameters and no lookahead, so units stick in local maxima. Fog of war: only one cited work tracks hidden enemy units (a particle model with linear movement update); all game-tree work assumes full information.

**Walls and tactics (III-B).** Walling a base entrance buys time against early aggression; one work solved wall placement as a constraint problem with answer-set programming. Tactical search: alpha-beta with durations (ABCD) and a later variant handle simultaneous moves and durative actions for up to 8 versus 8 units; UCT/Monte-Carlo tactics exist on abstract states in smaller games.

**Competitions (V).** AIIDE 2010 (108 games, bracket; winner Overmind, Zerg, potential-field mutalisk control), 2011 (1,170 games, round robin on 20 machines; Skynet 88.9 percent, UAlbertaBot 79.4, AIUR 70.3), 2012 (4,240 games; Skynet 84.4, AIUR 72.2, UAlbertaBot 68.6; persistent storage allowed across games), CIG 2011 and 2012, plus a continuous Elo ladder. UAlbertaBot's 2011 attack/retreat used a unit-counting heuristic (the survey does not mention a simulator for it).

### What it measured and how well it worked

- Top bots still lose to humans: best bots ranked about D to D+ on a human ladder, against amateurs C+ to B and professionals A- to A+. Skynet lost 0-2 to an ex-professional in 2011 and 0-2 to a strong Zerg player in 2012. The losses were traced to poor adaptation (no change of plan after an early push failed) and a target-priority error (shooting medics before tanks).
- Strategy structure dominates results: among the top three in 2011, Skynet beat UAlbertaBot in 26 of 30 rounds, UAlbertaBot beat AIUR in 29, AIUR beat Skynet in 19 (a rock-paper-scissors cycle). The 2010 winner's strategy was easily beaten by early rushes once others saw it, so its team entered a different race in 2011 (and finished 7th). In the Elo ladder, new bots with fixed strategies no other bot countered (named KillerBot, Ximp) took the top, with no adaptation.
- Adaptation analysis (V-D): the top three bots chose essentially the same opening regardless of opponent. Bots with persistent storage chose strategies using UCB (UAlbertaBot) or reweighted moods (AIUR), and win rates converged as rounds went on.
- Failure modes in tournaments: crashes (instant win for the other side), bots that cannot finish a beaten opponent and hit the time cap, dependence on map size, and no Zerg bots in CIG 2012 (the authors guess Zerg is harder to implement).

### Stated limits (VI, VII)

Hard-coded strategies still win; no bot adapts or synthesises a counter from scratch; no bot reasons its way to a tactic such as a drop; large-scale adversarial planning, planning under partial observability and multi-level integration are open; incorporating domain knowledge is unsolved.

---

## 4. What applies to this project

### Package E (calculated economy)

1. **State, `When` and fast-forward** (paper 1, Fast Forwarding and State Transition). Borrow the idea of computing, from current income and the cost of an item, the earliest tick at which it can be afforded, instead of polling a threshold. Treat "worker, depot, producer" as actions with duration, cost and a borrowed building. This is the cleanest way to make "when to expand, when to add production" a calculation. Does not transfer: it assumes one fixed goal and acting at the first legal moment; the budget queues in P-006 B deliberately hold money for competing uses (inference).
2. **Income as a rate per worker, estimated live** (paper 1, Abstractions and Conclusion). The authors fixed 0.045 and 0.07 from pro games and say the rate could be measured in-game. Borrow: measure our own income per worker from the harness and feed it to the calculation. Does not transfer: their income is linear in workers; ours saturates per field slot, so the offline optimum must use the sim's slot and trip-time model, not their constant (inference).
3. **Offline optimum check** (paper 1, Algorithm, Heuristics, Breadth Limiting, Macro Actions). P-006 E wants a best worker and depot schedule found by search offline. Borrow: depth-first branch and bound, the two admissible bounds (prerequisite-chain duration and time to gather the goal's cost), producer caps from the goal, and K-batch macro actions for workers. Gap in P-006 (inference): the search needs a goal. The paper minimises time to a given goal; it does not maximise income at a time. The check needs a stated goal such as "N workers, M depots, army value V" per opening.
4. **Determinism** (paper 1, Algorithm). The paper stops search on wall-clock time. That breaks the no-clock rule. Use a node count budget; any-time behaviour still works, and the result is reproducible (inference).
5. **Real numbers** (paper 1, Abstractions). The paper uses real-valued rates. Convert to `Fix64`; nothing in the method needs floats (inference).
6. **Spending shared between modules** (paper 3, IV). Supports the budget-queue premise; found that most bots hand out money first-come-first-served and a spend manager that decides "how to spend what we have" is the less common design. Does not transfer: no numbers on how to weight queues.
7. **Worker count heuristic** (paper 3, V-A, one bot). A "two workers per mineral patch, 18 for 100 percent saturation" rule is StarCraft-specific and is what our slot count already generalises.
8. **Learned priorities** (paper 3, III-A: evolutionary tuning of goal priorities beat static priorities). Not usable inside the AI (learning). Possibly usable offline: tune queue priorities with paired ladders (inference).

### Package S (threat and influence map)

1. **Region-level map, computed once at match start** (paper 3, III-B: terrain analysis offline; Voronoi regions and chokepoints; region scores for economy and defence). Borrow: score regions rather than only a fine grid, and compute static structure (regions, chokepoints) once. Does not transfer: BWTA's geometric method is not specified in the survey and is float-based (inference); we would build integer regions from our own map data.
2. **Influence maps as the shared spatial read** (paper 3, III-B, III-C: AoE II engine, kiting, flanking). Supports P-006's "every spatial decision reads one map". The survey gives no resolution, decay or weight values. Those must be designed and calibrated by us; do not assume the papers justify any number.
3. **Remembered enemy positions under fog** (paper 3, III-A: particle model with linear update). Borrow the idea that remembered enemies move by a simple rule and fade; the survey says almost nothing else exists. Float/probabilistic details do not transfer.
4. **Combining several maps** (paper 3, IMTrees). Borrow the structure (sum and product of basic maps, e.g. own strength minus enemy strength, times resource value). Does not transfer: the evolution of the tree is learning, and the survey warns that tuning many parameters is the weak point (potential fields).
5. **Potential fields for movement** (paper 3). Does not transfer to placement or target choice (no lookahead, local maxima); relevant only to unit micro, which P-006 does not plan.

### Package A (mechanic use)

1. **Walls** (paper 3, III-B). Supports the purpose (buy time vs early aggression). The one cited method is constraint solving over entrance geometry. Borrow: treat a wall as a placement constraint at a computed chokepoint; the survey gives no detail on gates. Does not transfer: StarCraft's wall rules (building sizes, unit stuck cases) are specific; the ASP solver is outside our deterministic-code rules (inference).
2. **Abilities and drops** (paper 3, VI). Every bot that used drops or special tactics had them hard-coded; none derive them. Supports writing each ability as an authored rule with a trigger, tested with the behaviour checks. Overmind's mutalisk control (potential fields) and Skynet's Dragoon kiting won tournaments, which says ability use alone can decide results.
3. **Valid-action feedback** (paper 2, IV-B). An agent repeatedly trying invalid actions pointed to a design issue. Borrow (inference): when the AI tries an unavailable ability or placement, log it; the log separates "AI cannot do it" from "game does not make it clear". Does not transfer: their agents learn from the penalty.
4. **Target priority errors** (paper 3, V-A: shooting medics before tanks). Fits a unit-use check: each unit type should be shown to be used and to be targeted sensibly.

### Balance testing method

1. **Pairing and repetition** (paper 3, V-A, V-B). The competitions moved from brackets (108 games, pairing luck) to round robins (1,170, then 4,240 games), and CIG pairings were played on every map twice with start positions swapped. Same idea as paired seeds; supports it, adds nothing new.
2. **Win rates are matchup structure, not a ranking** (paper 3, V-A). A rock-paper-scissors cycle of 26/30, 29/30 and 19/30 among top bots shows that a single average hides the matchup. Supports the per-opening, per-faction matrix in M item 5.
3. **Strategy coverage drives results** (paper 3, V-A, V-C, V-D). A fixed strategy nobody counters can win a whole ladder; a strategy that dominates one year is beaten the next. For us: a faction's win rate partly measures whether the AI's opening library for it contains a line the opponent's AI lacks an answer to, not only unit values (inference from these results). Supports P-006 section 3 item 2 and section 6 item 4.
4. **Faction coverage bias** (paper 3, V-B). No bot played Zerg in CIG 2012, probably because it is harder to script. Our equivalent: faction win rates mix game balance with how well the AI plays each faction; the unit-use share in the report should be read per faction (inference).
5. **No learning between games** (paper 3, V-A). Bot win rates drifted and converged as bots learned across games with persistent storage. The balance AI should stay stateless across games, or its state should be reset per pair, or the results are not independent (inference; our AI is scripted so this holds today).
6. **Stalemates and crashes** (paper 3, V-B). Crashes counted as wins and bots that failed to finish a beaten opponent were real tournament problems. The report should count timeouts and exceptions separately and never as wins (inference; may already be so in the harness, which I did not read).
7. **Planner-agent as a low-variance benchmark** (paper 2, IV-A). A deterministic search agent gives one repeatable number per scenario, and comparing it across content (careers there) checks that content is equally hard. Possible use here (inference): the E offline optimum gives a per-faction, per-opening time-to-benchmark without win-rate noise, alongside the ladders. Does not transfer directly: their game was single-player and fully known; ours has an opponent.
8. **Search budgets leak into the data** (paper 2, IV-A: Medical result likely hurt by the 2,000-node cutoff). Any node budget in our AI could bias one faction's results; the budget should be equal for all and logged (inference).
9. **Skill versus style** (paper 2, Introduction). Supports not treating the strongest line of play as the product, and P-006's several openings. Does not transfer: measuring human-likeness needs human data, which P-005's human benchmark supplies later.
10. **Bot strength ceiling** (paper 3, V-A, I). Best bots lost to amateurs and professionals. Supports P-005's human benchmark: bot-vs-bot data says what the bots do, not what people would.
11. **Not borrowed:** deep RL, imitation learning and Markov ensembles (paper 2) need training and tuning cost the project excludes, and the paper itself reports sensitivity to hyperparameters.

---

## 5. Checks against P-006

1. **Section 7 status.** All three papers are now read. Section 7 should say so. Paper 2 has no RTS content and little use for B and C; decision 6 (allow arxiv.org access) is then not needed for it.
2. **Section 2, build-order row.** P-006 says replanning happens on events (queue empty, key unit lost, enemy tech seen). That comes from the UAlbertaBot wiki, not paper 1. In paper 1 replanning is an experiment device (plan every 15 or 30 seconds, mimicking professionals), and the full-game agent details are omitted. Cite the wiki for it, not the paper.
3. **Section 4 E, offline search.** Needs a stated goal (paper 1 assumes goals are given) and a saturating income model (paper 1's income is linear in workers). It must also use a node budget, not wall-clock time. These are additions, not contradictions.
4. **Section 4 E, marginal worker rule.** "Income added over a horizon exceeds cost" is not in any of the papers; paper 1 searches rather than computing marginals. It stays a P-006 design choice, and the offline optimum is the check on it, as P-006 already says.
5. **Section 4 M item 3, pool gate ("not worse against any pool member").** Paper 3's rock-paper-scissors result implies a real improvement can lose to one pool member. Consider reporting per-member results and a stated tolerance, not a hard no-worse rule (inference).
6. **Section 4 C, fight simulation cost.** The survey says the published durative alpha-beta search handled up to 8 versus 8 units. P-006 section 8 already warns about per-think budget; add that full-army simulation will need grouping or abstraction. The survey also names a unit-counting heuristic, not a simulator, for UAlbertaBot's 2011 attacks; P-006's table credits SparCraft from the wiki, which may be a later version. No conflict, but the survey cannot confirm the SparCraft claim.
7. **Section 2, last row ("share one set of biases"; "agents themselves must be evaluated").** Neither phrase is in these three papers. Paper 2 supports a skill-and-style view and purpose-built agents; paper 3 supports the strategy-coverage and cycle effects above. Source the bias claim elsewhere or soften it.
8. **Section 2, closing paragraph ("academic work optimises for winning; studios for enjoyment").** Paper 2 is a studio paper whose aim is neither: it wants human-like skill and style for balancing, and notes strong agents may fall outside it. This is a nuance for P-006 section 3 (add a note that think rate and reaction speed of the AI should not be inhuman); it does not change the plan.
9. **Section 6 and section 3.** Consistent with all findings. Item 4 (no opening wins nearly every mirror) is supported by paper 3's fixed-strategy results.

Everything else in P-006 sections 1 to 6, 8 and the decisions: none found that the papers contradict.

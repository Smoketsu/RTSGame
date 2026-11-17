<!-- rts/docs/proposals/P-004-smarter-skirmish-ai.md: proposal for making the skirmish AI react to its opponent (scouting, counter-armies, fight choices, varied plans), with build stages and how each is measured. Written 17 Nov 2026 after the developer judged balance work pointless while the AI plays one fixed plan. -->
# P-004: A skirmish AI that reacts

**Decision needed:** approve stages 1 to 3 below as the next build, and answer the four questions at the end. Stages 4 to 6 can be decided later.

## Where the AI is today

After the 16 Nov work the AI plays whole games: it expands by depot and by moving its HQ, groups its army, and every one of 60 games on `prototype_1v1` ended in a kill. But it plays one script:

- One profile (`data/ai/aggressive.json`), one build order and one army mix per faction: Skai 3 Windrunner : 1 Talon Marksman : 1 Roc Rider; water 3 Pummeler : 1 Shellback : 1 Brinecaster.
- It never builds a Stormcaller, a Spoutling, a Perch Tower or a Coral Turret.
- It never looks at what the enemy has. Its only reactions are to retreat when outnumbered and to defend when its base is hit.
- It does not scout. It learns where the enemy is by walking its army into it.
- It ignores the faction mechanics (flooding, momentum), the uphill penalty, the dome shield and the Core.

So AI-versus-AI results measure that one script against itself. They say nothing about balance, and a human would beat it by building the one thing it never answers.

## The game already has counters to react to

Read from the unit data on `main`:

| If the enemy has | The answer in the data | Why |
| --- | --- | --- |
| Roc Riders (the only air unit) | Water: Spoutling, Coral Turret. Skai: Talon Marksman, Windrunner, Perch Tower | Pummeler, Shellback, Brinecaster and Stormcaller cannot hit air at all. A water army with no Spoutlings cannot touch a Roc Rider. |
| Armoured units (Pummeler, Shellback, Roc Rider, HQs) | Blast: Stormcaller, Brinecaster | Blast does 1.0 against armoured; kinetic does 0.75. |
| Light units (Windrunner, Marksman, Stormcaller, Brinecaster, Spoutling, workers) | Kinetic, and anything fast | Kinetic does 1.0 against light; blast only 0.75. |
| Shellbacks with domes | Melee, or get inside the dome | The dome absorbs ranged shots from outside only. |
| Ranged units on high ground | Do not attack uphill; go around or use air | Ranged shots uphill do 0.75. |
| Buildings and a rooted HQ | Blast | Blast does 1.25 against structures; drench 0.25. |

None of this is used. The first job is to make the AI see these and act on them.

## Principles (unchanged from the rest of the project)

- The AI issues only commands a player could issue, and knows only what its team has seen. No cheating at any difficulty.
- Deterministic: same inputs, same decisions. No floats, no clock, sorted iteration, its own seeded generator.
- Data-driven: every weight and threshold in the AI profile files. The counter table is computed from unit data, never hand-written per unit, so a new unit or faction needs no AI code.
- The core never names a faction. Faction-specific behaviour goes behind the same plug-in idea the simulation uses.

## Approach

Three ways to make an RTS AI smarter, and what I recommend:

| Approach | What it is | Verdict |
| --- | --- | --- |
| **Scored choices** | Keep the rule list, but replace fixed choices (what to build, where to attack) with a score over the options, computed from what the AI knows. | **Recommended as the base.** Small steps from today's code, easy to test rule by rule, deterministic. |
| **Fight prediction** | A cheap calculation that takes two armies and says who wins and by how much, using the real damage table, ranges and target rules. | **Recommended, built once and used everywhere:** which units to build, whether to attack, whether to retreat. This is what makes the scores honest. |
| **Tuning by self-play** | The harness plays thousands of games with slightly different profile numbers and keeps the ones that win. | **Later.** Worth doing once the AI has enough choices for the numbers to matter. It tunes data, not code. |
| Learned AI (neural network) | Train a model to play. | **Not recommended.** Hard to keep deterministic across machines, impossible to explain or tune by hand, and far more work than the rest of the game. |

## Stages

Each stage is a build of a few days with its own tests, and each is useful without the next.

### Stage 1: Know the enemy

- **Scouting.** Send one cheap fast unit (Windrunner; a worker for water until it has one) to the enemy's start early, then to each expansion spot in turn. Repeat on a timer. Flying units scout when available.
- **Enemy model.** Remember every enemy unit and building seen, by type, with the time last seen. Forget units seen die. Old sightings count for less. Seen production buildings imply what can come next (a Roost means Roc Riders are possible).
- **Measured by:** in a test, an enemy that builds only Roc Riders is known to the AI as "air" within a set time of the first one flying; nothing the AI knows comes from fogged tiles.

### Stage 2: Build the right army

- **Fight prediction** as above, as a pure function over two unit lists.
- **Counter-mix.** For each unit the AI can build, score it against the enemy model (how much it kills per cost, whether it can be hit back, whether it can hit air). The army mix becomes the base mix shifted toward the best scorers. With no information it plays the base mix.
- **Tech on demand.** If the best answer needs a building the AI lacks, it builds it. Static defence (towers, turrets) goes up at a base that keeps getting raided.
- **All units in play.** Stormcaller and Spoutling join the pool.
- **Measured by:** response tests: against a scripted Roc Rider rush, water has Spoutlings or a turret within a set time; against massed armoured units, the share of blast units rises. And the ladder (below): the new AI must beat the old one clearly.

### Stage 3: Choose fights

- **Attack when it would win.** Use fight prediction on its army against what it knows is there, including the uphill penalty and domes, in place of today's fixed supply threshold.
- **Pick the target.** Attack the weakest thing that matters: an undefended expansion or depot before the main army; workers with fast or flying units.
- **Defend what is attacked,** with as much as the prediction says is needed, not everything.
- **Simple unit control.** Ranged units keep their distance from melee; hurt units fall back; focus fire on what dies fastest.
- **Measured by:** fewer attacks that lose the whole army; games against the stage 2 AI.

### Stage 4: More than one plan

- Several openings per faction as data (rush, fast expand, air, defensive), chosen by the seed and then adjusted by scouting. Switch plan mid-game when losing a certain way.
- Personalities and difficulty as profiles: how often it scouts, how fast it reacts, how well it controls units. Easy AIs are slower and blinder, never weaker by cheating in reverse.
- **Measured by:** variety across games (how different the armies and timings are between seeds), and no single opening winning nearly every time.

### Stage 5: Play the faction

- Water: place flood sources to flood the routes it fights on; fight on flooded ground; use the tide.
- Skai: use momentum; raid with Roc Riders; relocate the HQ as a tactic, not only when the fields run out.
- Both: contest the Core when holding it is worth more than attacking, and pick cycles that suit the army it has.
- **Measured by:** per-mechanic tests, and the ladder.

### Stage 6: Tune by self-play

- The harness varies profile numbers, plays the versions against each other and keeps the winners. Output is a data file a person can read and overrule.

## How we will know it is smarter

Win rates between two copies of the same AI cannot show this. Three measures that can:

1. **The ladder.** Every stage plays the previous version, both ways round, on every map. A stage is accepted only if it wins clearly (the bar is a profile number; I suggest 65 percent over 200 games).
2. **Response tests.** Scripted opponents that do one thing (air rush, tower rush, mass of one unit, early raid on workers). The AI must show the right answer within a set time. These are the tests a human exploit would fail.
3. **Variety.** How much armies, timings and outcomes differ between seeds. Today the answer is almost zero by construction.

Only when the ladder stops moving and the response tests pass is AI-versus-AI data worth reading for balance.

## Cost and order

Stages 1 to 3 are the ones that answer your objection, and they depend on each other in that order. I would build them as three separate packages, each with a spec, tests, a determinism review and a ladder run. Stage 4 is the next most valuable for a player. Stages 5 and 6 can wait until the factions are closer to final.

What this does not need: any change to the simulation. Everything is in `rts/src/Ai`, the AI profile data and the harness.

One gap to know about: three units in the design have no data yet (Thunderhead, Tidecaller, Leviathan). The AI will use them the day their unit files exist, because the counter table is computed, but until then air play is Roc Riders only.

## Questions for you

1. **Scope now.** Stages 1 to 3 as the next build? Recommended: yes.
2. **Scouting unit for water.** Water has no fast cheap unit in the data. Use a worker, or wait for a unit? Recommended: a worker for now.
3. **Acceptance bar.** A new version must beat the old one in 65 percent of 200 games. Higher, lower, or leave it to me?
4. **Difficulty levels.** Do you want easy / normal / hard planned in from stage 4, or one strong AI first and levels later? Recommended: one strong AI first.

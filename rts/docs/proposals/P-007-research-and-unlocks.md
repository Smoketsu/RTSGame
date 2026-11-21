<!-- rts/docs/proposals/P-007-research-and-unlocks.md: proposal written 21 Nov 2026 on the developer's idea of a tech tree (research that unlocks units and buildings, possibly at a research building) to deal with the rare-resource pile-up the balance reports measure. Sizes the problem from the measured banks, splits it into a design gap and an AI gap, compares three options smallest first and lists decisions for the developer. Not a build spec; a spec under rts/docs/specs follows once the decisions are made. Part of rts/docs/proposals (index: README.md). -->
# P-007: Research and unlocks as a rare-resource sink

**Decision needed:** the numbered decisions at the end. Each has a recommended default.

The developer's idea (21 Nov 2026): "to deal with the rare pile-up: a tech tree that unlocks units and buildings, possibly a building that allows this research."

Sources: `reports/balance_first/README.md` (`aggressive_s5`, 216 games, `prototype_1v1`); `reports/ai_b/README.md` (branch `review/p006-b`, `aggressive_s5_b`, budget queues); `specs/walls_and_tech.md` package T; `specs/ai_budget_queues.md`; P-005 and P-006; the unit files; `data/tuning/economy.json`; the map files. Decisions in force: mechanics before numbers (P-005 decision 2), unit and combat numbers frozen (P-005, P-006 section 5), every number below a placeholder.

---

## 1. The problem in measured terms

Median unspent rare against the 150 bar of `CHECK balance_unspent` (n = slot-games still running):

| Faction | Time | `aggressive_s5` | `aggressive_s5_b` (budget queues) | Over the bar (s5 / s5_b) |
| --- | ---: | ---: | ---: | ---: |
| Skai | 300 s | 205 (216) | 165 (216) | 55 / 15 |
| Skai | 600 s | 285 (59) | 235 (100) | 135 / 85 |
| Water | 300 s | 220 (216) | 80 (216) | 70 / 0 |
| Water | 600 s | 475 (117) | 310 (114) | 325 / 160 |
| Water | 900 s | 1020 (16) | 950 (28) | 870 / 800 |

Rare spent per game: Skai 1130 to 1310, Water 730 to 855. Common is spent throughout (at most 385; Water 50 to 90 after 600 s).

### Design gap or AI gap

There is always something to buy with rare today. Every rare item is a repeatable unit: Skai Roc Rider 50, Stormcaller 40, Thunderhead 65; Water Spoutling 25, Tidecaller 25, Ink Lobber 40, Leviathan 80; plus one Storm Spire or Reef Workshop at 50. So "nothing to buy late" is not literally true. What is true:

- **A cost-ratio gap (design).** Every rare item also costs common, at about 3.75 to 4 common per rare (Leviathan 300/80, Ink Lobber 150/40, Spoutling 100/25). Water's common bank after 600 s is 50 to 90, so Water is common-bound: spending its 870 surplus rare on Leviathans would need about 3260 more common. Nothing in the game converts rare into value without common. Supply (cap 200) adds a second limit in long games. This part is a design gap, and it is the part research can fix: an item that costs mostly rare.
- **An AI gap.** The Tidecaller is never trained (use share 0.000). The budget queues cut the Water bank at 300 s from 220 to 80, so the AI can absorb early surplus when it is told to. The Water AI also has fewer workers under budget queues (21 against 24 at 300 s), which lowers common income and so deepens the ratio problem.

What the data can tell us: the bank grows with time, it grows much faster for Water (about +545 between 600 and 900 s, roughly 1.8 per second) than for Skai (+80 between 300 and 600 s), and better AI spending moved it only at 300 s.

What it cannot tell us:
- whether a human player piles rare up (no human games);
- how much of Water's common shortage is the AI's economy rather than the game (P-006 package E is not built);
- anything about Skai after 600 s (Skai wins every cross game, so few Skai games reach 900 s);
- the 900 s figures are survivors (16 and 28 slot-games, mostly Water mirrors), not typical games;
- other maps (only `prototype_1v1` was measured);
- per-queue spending (not in the game record).

## 2. Can one-off research absorb a pile that grows with time?

**Rare is finite.** Each rare field holds 600. `prototype_1v1` has 8 (4800, about 2400 per side if split evenly); `long_lowland` 4 (1200 per side); `greybox_1v1` and `twin_chokes` 2 (600 per side). Income stops when the fields are empty, so the pile cannot grow without bound. It is capped by the map.

Upper bound for a long Water game on `prototype_1v1`: by 900 s Water has banked 1020 and spent at most its whole-game 730 to 855, so it has gathered at least about 1750 of its 2400. At most about 650 more can arrive after 900 s. (Rough: the medians come from different game sets.)

How much a research tree would have to absorb to bring the median under the 150 bar, per faction:

| | Skai | Water |
| --- | --- | --- |
| by 300 s | 15 to 55 | 0 to 70 |
| by 600 s | 85 to 135 | 160 to 325 |
| by 900 s | unknown (no data) | 800 to 870 |
| until the fields run dry | unknown | up to about 1500 (870 + 650) |

Results:
1. **Yes, in principle**, on today's maps: because income ends, a fixed total can match it. A research total of about 300 rare per faction covers both factions to 600 s.
2. **The two factions need very different sizes.** At 600 s Water's surplus is about 2.4 times Skai's; by 900 s Water needs about 850 and Skai's need is unmeasured but small. One shared tree size either leaves Water piling or starves Skai of rare it now spends on Roc Riders and Thunderheads.
3. **Timing matters more than total.** Research bought early (to unlock units) is spent before 600 s; the 900 s Water pile arrives after any sensible tree is done. Covering it with one-offs means four to six expensive late items that a 600 s game never reaches.
4. **It depends on map rare.** A map with more rare reopens the pile. Map amounts are data, not unit numbers, and are a second lever this proposal does not pull.

So one-off unlock research fixes the 300 s and 600 s pile and the cost-ratio gap; it does not, alone, fix Water's late pile.

## 3. Options, smallest first

None of them changes a unit or combat number. Every cost and time below is a placeholder.

### (a) Research at the existing tech buildings (reuse package T)

- **Sim.** A research item is queued at a building that lists it, ticks down like a unit in production (step 7b) and, on completion, sets a per-player flag; nothing spawns. The existing `requires` list may name a research ID as well as a building ID; `FirstMissingRequirement` treats a research entry as met when the flag is set. One new command (start research), one new event (research completed), one new rejection set (already researched, already queued, not a researcher). Losing the building loses what was queued there, as for a queued unit today; finished research is never lost.
- **Data.** A `researches` list on `skai/storm_spire.json` and `water/reef_workshop.json`, like `trains`. One research file per faction under `rts/data`: ID, display name, common and rare cost, seconds, its own `requires`. The loader's reference and cycle checks extend to the mixed graph.
- **What moves behind research (placeholders):** Skai: Thunderhead (now behind the Storm Spire only) behind "Thunderhead plans", 25 common / 125 rare, 45 s. Water: Leviathan behind "Leviathan plans", 25 / 175, 45 s, and the Ink Lobber behind "Ink plans", 25 / 125, 30 s (about 300 in all). Optional: the Roc Rider behind a Skai item (this is Q-T1, still open).
- **AI.** Section 4.
- **Unity.** Research buttons on the two tech buildings, a progress bar, a disabled-button tooltip naming the missing research (T.5 already does this for buildings). No new model.
- **Covers:** the cost-ratio gap and the pile to about 600 s, if the AI buys it. Not Water at 900 s.

### (b) A dedicated research building per faction

Everything in (a), plus one new structure per faction that holds the research list; the tech buildings stay as gates. Adds two unit files, two models and placement work in the AI (package S). Gains: more research items without overloading the tech buildings, and a second building to scout and raid. The concept brief's seven building jobs have no research job, so it also adds an eighth job the design never names. Absorbs nothing (a) does not, except its own build cost (about 50 rare once).

### (c) Repeatable or levelled upgrades as a standing sink

A research item with levels (for example three levels at rising rare cost), or one that can be bought again. This is the only option that keeps absorbing until the fields are dry, and the only one that can carry Water's 900 s pile. But an upgrade must change something, and every obvious effect (damage, armour, speed, gather rate) is a unit, combat or economy number, which are frozen. The one upgrade the design names is "tide upgrades only affect the owner's water" (quoted in `specs/flooding.md`): a mechanic, not a unit number, and Water-only, which matches where the pile is. Its effect is not defined in the design.

## 4. Determinism, architecture and the AI

- **State and hash.** Per player: the set of completed research IDs (sorted, hashed as count then IDs in ordinal order) and, in (c), a level per ID. Queued research lives in the building's existing production queue, which is already hashed. Research definitions go into the content digest like `requires`.
- **Step order.** No new step. The command applies in step 2; progress and completion happen in step 7b with production, units in ascending ID order; two items finishing on one tick emit events in that order.
- **Command and events.** `start_research(building, researchId)`; `ResearchStartedEvent`, `ResearchCompletedEvent`; rejections through `CommandRejectedEvent`. No cancel command, as for units today.
- **Fog.** Is enemy research visible? The event log is global; Unity decides what to show. A default is in the decisions.
- **Factions.** Research is core (like `trains`), not a faction mechanic; the core never names a research ID. A Water tide upgrade in (c) would touch the flood mechanic and needs its own spec.
- **AI.** Walls, gates, the Tidecaller and Downdraft show what happens without a rule: use 0.000. The AI needs:
  1. rule T2 extended so a missing `requires` entry that is a research item is queued at an owned tech building (the tech queue of `ai_budget_queues.md`, rare weight 30, already exists);
  2. `RequirementsMet` reading the research flags, so the counter-mix never asks for a locked unit;
  3. research as a target of the rare-surplus rule: while `RareSurplusActive`, the tech queue may buy the next item even when no mix unit needs it.
  Budget queues (package B) are not switched on: two of their bars failed. Research for the AI therefore waits for B, or needs a small rule on the old spender.
- **Harness.** The balance report's use check must list each research item, so 0.000 shows up at once.

## 5. Risks

- **Slower games.** Each gated unit arrives later by the research time (45 s placeholder). Openings like `water_leviathan_tech` change meaning and must be re-read.
- **More for the AI to get wrong.** Every new decision has so far been ignored or overdone (AI-P towers). Measured gains have come only from rules that read game quantities.
- **Frozen numbers.** (a) and (b) change no unit number but do change when units exist, which changes fights as much as a number would. (c) needs an effect, and every effect except a tide upgrade is a frozen number.
- **Baselines and pins.** Gating Thunderhead and Leviathan behind research changes every state hash in games that train them, the `economy_smoke` and `smoke` pins, the `balance_first` and `ai_b` baselines and the Unity parity check. A full re-baseline (two ladders, about 95 minutes) follows.
- **Starving Skai.** A tree sized for Water takes rare Skai now spends on units.
- **Masking the AI gap.** A sink can hide that the Water AI is common-poor. Package E should be measured before research is sized.

## 6. How we would know it worked

Measured with the existing harness, paired against the same AI without research (P-006 method), every bar a placeholder:

1. `CHECK balance_unspent` passes at 300 and 600 s for both factions (median rare at most 150).
2. Each research item is used in at least the balance report's use share (0.100) of games for its faction.
3. Median time of the first Thunderhead and first Leviathan moves later by no more than the research time plus 30 s.
4. Rare spent per game for Skai does not fall below 1000 (it is 1130 to 1310 now): the tree must not starve Skai.
5. Water's 900 s bank is reported, not judged; it is the input to decision 5.
6. No new `missing_prerequisite` rejection from the AI in the trace of one game per matchup.

## 7. Rejected extras (one line each)

- Converting rare into common at a building: not in the design, and it hides the ratio gap instead of giving rare a use.
- Research that raises supply cap or gather rate: economy numbers, frozen.
- Sharing research with allies: like Q-T4, own only.
- A cancel command for research: none exists for units; added for both or neither, later.
- Research trees per opening or per AI profile: the AI chooses order, the data holds one tree.

## 8. Recommendation

Option (a), after package B is either fixed or replaced, and after P-006 package E so Water's common income is real. It is the smallest change, reuses package T's `requires` path, adds no building, model or job the design does not name, and fixes the part of the pile that is a design gap (nothing costs mostly rare). Size it per faction from the 600 s figures, Water larger than Skai. Do not build (c) until the design says what a tide upgrade does; it is the only answer to Water's 900 s pile, and that pile is from 16 to 28 survivor games. Do not build (b) unless (a) runs out of room.

## Decisions for the developer

1. **Is research part of the game?** The design names a tech building job and tide upgrades, but does not say whether tech buildings research, and package T rejected research as "one gate does the job". Default: yes, as option (a).
2. **Where research happens.** Default: at the existing Storm Spire and Reef Workshop; no new building.
3. **What research unlocks.** Default: units only (Thunderhead, Leviathan, and the Ink Lobber so Water's tree reaches its size); abilities such as Downdraft and the wave are not gated. Gating the Roc Rider stays Q-T1.
4. **Same tree size for both factions?** Default: no; Water's total rare cost larger, sized from the 600 s surplus (about 300 Water, 125 Skai, placeholders).
5. **Repeatable upgrades (option c).** Default: not now; revisit when the design defines tide upgrades.
6. **Is enemy research visible?** Default: not shown; a completed item is seen only when the unlocked unit is seen.
7. **Losing the building mid-research.** Default: the queued item is lost without refund, as for a queued unit; finished research stays.
8. **Order.** Default: after package E is measured and package B passes its bars, so research is sized against a fair economy.
9. **Map rare amounts** as the other lever (fields of 600). Default: unchanged; a map question, not part of this proposal.

## Developer decisions (21 Nov 2026)

Defaults accepted on decisions 1, 2, 3, 5, 6, 7 and 9. Decision 4 is changed: the research tree is the **same size for every faction** (same number of items, same total cost). Decision 8 is replaced by a new standing direction: "get every feature usable, then make the AI work", so research is built now and the AI packages of P-006 wait. Build spec: `rts/docs/specs/` (being written on `review/p007-spec`).

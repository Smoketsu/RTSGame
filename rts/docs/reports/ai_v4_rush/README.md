<!-- rts/docs/reports/ai_v4_rush/README.md: diagnosis of the two rush openings of aggressive_s4r (skai_windrunner_rush, water_grasper_rush), which won 0 of 20 and 1 of 18 self-ladder mirror games. Evidence from forced-opening traces on prototype_1v1 plus three one-change counterfactuals; feeds package L-R of rts/docs/specs/skirmish_ai_v4_rl.md. Measured 18 Nov 2026 on main at fe89236. A record of the AI, not a balance reading (P-005 decision 11). -->
# Why the rush openings of `aggressive_s4r` lose

**Verdict.** None of (a), (b) or (c) on its own. The cause is the openings' data together with the worker rule. Both rushes postpone their first depot to 240 s (`early_expansion_seconds` 240), and rule W caps workers at `workers_per_field` (3) times the served common fields. One base on `prototype_1v1` has 2 common fields, so a rusher is held at **6 workers until about 240 to 275 s**. Every other opening places its depot at 48 to 70 s and has 11 to 14 workers by 180 s. The rusher's income is about **half** of its opponent's from about 90 s to 300 s (median ratio 0.56 and 0.44 at 180 s, 0.49 at 300 s).

The attack does leave early, so (a) is false: 130 to 141 s for Skai, 116 to 153 s for Water. It is not stronger than the defence, because every opening's first building is the same Roost or Tidepool, so the rush gains only a third producer, not an earlier army. When the attacks fail, the rusher cannot recover: Skai is dead at 366 to 531 s, and Water reaches equal income only at about 480 s, with half the army.

Removing the fight predictor or the retreat rules changes nothing (0 of 8 each). Moving only the first depot to 0 s turns the openings around: Skai wins 15 of 24 against the other three Skai openings, Water 8 of 24.

Nothing in `rts/src` or `rts/data` was changed. Scratch copies are under `rts/build/rush/` (git-ignored).

## 1. Method

- **Forced profiles.** `rts/build/rush/data` is a copy of `rts/data`. In it, `ai/f_<opening>.json` is a copy of `aggressive_s4r.json` with that opening's weight set to 100 and the other openings of its faction set to 0. The other faction is forced to its `*_standard`. The `id` is renamed to match the file name. A Python script does the copy (scratch, not committed).
- **Traces.** One per game, with the ladder seat rule. Game g puts profile A in slot `(g / 2) % 2`, so games 0 to 3 cover both seats:
  ```
  dotnet rts/build/bin/Rts.Tools.BalanceHarness/Release/net8.0/Rts.Tools.BalanceHarness.dll --data rts/build/rush/data --trace --map prototype_1v1 --matchup <m> --profile-a <rush> --profile-b <other> --seed 20261117 --game <g> --out <file>
  ```
  The trace samples every 30 s, so economy figures are good to 30 s. Stance changes and structure sites are exact ticks (20 ticks per second).
- **Games.**
  - Each rush against each of the 3 other openings of its faction, games 0 to 3 (24 traces).
  - Rush against `*_standard`, games 4 to 7 (8 more).
  - Cross-faction: Skai rush against `water_standard`, games 0 and 3. Water rush against `skai_standard`, games 1 and 2.
  - Counterfactuals (section 5): 3 variants x 2 factions x 8 games against `*_standard`. Then the two "depot at once" variants against every other opening and `aggressive_s3`, 8 games each.
  - 192 games in all.
- "Opponent" below means the same-faction opening the rush faced, never the rusher.

## 2. When the first attack leaves, with what, against what (mirror games, 16 per rush)

| | Skai rush | Water rush |
| --- | --- | --- |
| first Massing to Attack | 130 to 141 s (median 131), threshold 6 to 8 supply | 116 to 153 s (median 152), threshold 6 to 8 |
| rush army at the sample before | 5 to 7 Windrunners (5 to 7 supply; about 10 by 150 s) | 4 Pummelers + 2 Reef Runners (10 supply) in 12 of 16; 1+1 or 3+2 in the rest |
| opponent army at the next sample | 4 Windrunners + 1 to 3 Talon Marksmen (6 to 10 supply) | 2 to 6 Pummelers + 1 to 2 Shellbacks + 0 to 1 Reef Runner (5 to 15 supply) |
| other defence | HQ (1500 hp, 8 damage per s, range 6), 7 to 14 armed workers. No tower at the first attack; rule SD builds a Perch Tower after the first raid (`static_defence_after_raids` 1) | HQ (1800 hp), 11 to 14 workers; Coral Turrets follow the raids |
| first attack ends | retreat 159 to 184 s (on arrival) | retreat 163 to 202 s |
| attacks started / retreats per game | 3 to 5 / all of them | 3 to 12 / all but 1 or 2 |
| losses by 300 s | rush lost 15 to 26 army supply; killed 2 to 13 workers and 8 to 19 army supply | rush lost 5 to 21 army supply; killed 0 to 5 workers and 9 to 28 army supply |
| damage to the enemy HQ, whole game | 0 to 10.8 | 0 to 6.0 |

So the Skai rush trades about even and the Water rush trades in its favour. Neither trade comes near what the lost income costs: about 300 to 350 common per 30 s for 150 s or more.

**Do the AI's own rules hold the rush back?**
- *Predictor and threshold.* Skai: no; it attacks within about 10 s of reaching its threshold. Water: yes, by about 35 to 45 s. It has 9 to 10 supply at 120 s but leaves at about 152 s; with `use_predictor_attack` off it leaves at 106 to 118 s (section 5). Removing the predictor does not change a result.
- *Massing and staging.* `rally_at_core` stages at the Core. Travel to the enemy base is about 30 to 40 s, so a wave that leaves at 150 s arrives at about 185 s, after the opponent's 14-worker economy has caught up.
- *Retreat rules.* Every attack ends in a retreat, usually as the HQ and defenders come into view. The predicted retreat counts the armed HQ among the engaged enemies. With both retreat rules off (`retreat_max_advantage` 0, `retreat_army_fraction` 0), the rush still loses 8 of 8 per faction, and sooner (347 to 378 s for Skai).
- *The defender's raid-tower response.* Present: the Skai defender builds a Perch Tower at about 177 s, and the Water defender builds 1 to 4 Coral Turrets. These cost the defender 100 common each; they do not decide the game.
- *Cross-faction.* Against `skai_standard`, the Water rush stays in Massing with 10 to 17 supply (the predictor refuses) and never attacks in game 2; it attacks once, at 213 s, in game 1. Cross-faction results follow the faction anyway: the Skai rush beat `water_standard` 2 of 2, and the Water rush lost 2 of 2.

## 3. Economy at 3, 5 and 8 minutes (rush / opponent; min, median, max over the mirror games)

| | Skai, 180 s | Skai, 300 s | Skai, 480 s | Water, 180 s | Water, 300 s | Water, 480 s |
| --- | --- | --- | --- | --- | --- | --- |
| games still running | 16 | 16 | 2 | 16 | 16 | 15 |
| workers | 6 / 7, 11, 14 | 6, 7, 10 / 11, 14, 15 | 7 / 17 | 6 / 11, 14, 14 | 8, 9.5, 10 / 14 | 6, 17, 18 / 17, 18, 19 |
| common income per 30 s | 300 to 305 / 435, 545, 675 | 305, 333, 475 / 535, 675, 720 | 305 / 885 | 270 to 285 / 540, 635, 665 | 270, 370, 450 / 635, 713, 785 | 330, 830, 990 / 795, 910, 975 |
| income ratio (median) | 0.56 | 0.49 | 0.35 | 0.44 | 0.49 | 0.98 |
| supply used | 17, 19, 22 / 18, 27, 30 | 12, 18, 29 / 35, 55, 58 | 10 to 15 / 82 | 23, 25, 26 / 26, 34, 37 | 23, 34, 37 / 45, 53, 64 | 12, 45, 65 / 80, 89, 97 |
| first depot | 236 to 265 s (opponent 48 to 53 s) | | | 241 to 275 s (opponent 53 to 70 s) | | |

Why 6 workers: `AiBrain.Turn.WantedWorkers` returns `min(max_workers, sum of the served fields' caps)`. The home base serves fields 0 and 1 (common), so 3 + 3 = 6. A depot at the natural serves fields 2, 3 (common) and 16 (rare), which brings the cap to 14. Nothing in the Sim limits gatherers per field: measured income per worker is the same at 6 and at 14 workers, about 1.6 common per second. The cap is the AI's rule, not the game's.

## 4. After the first attack

- **Skai.** No transition is possible on 6 workers. The depot goes down at 236 to 265 s. At 300 s the rusher has 6 to 10 workers against 14 to 15, and 12 to 29 supply against 35 to 58. In the first trace (seed 20261117, game 0) the opponent killed the late depot at about 340 s. Games end at 366 to 531 s (median 415), all losses.
- **Water.** It does transition: after the depot (241 to 275 s) it reaches 17 workers and equal income by 480 s. By then its army is 45 supply against 89 (median), and it loses at 452 to 785 s (median 559). The whole deficit is the economy lost between 90 and 270 s.

## 5. Counterfactuals (one change each, scratch copies, against `*_standard`, games 0 to 7)

These isolate causes. They were not tuned or iterated: each change was chosen before its run.

| variant (applied to the forced rush profile) | Skai wins | Water wins | what moved |
| --- | ---: | ---: | --- |
| as shipped (games 0 to 7) | 0 / 8 | 0 / 8 | |
| `use_predictor_attack` false (root) | 0 / 8 | 0 / 8 | first attack 115 to 132 s (Skai), 106 to 118 s (Water) |
| `retreat_max_advantage` 0 and `retreat_army_fraction` 0 (root) | 0 / 8 | 0 / 8 | no retreats; games end sooner |
| opening `early_expansion_seconds` 0 and `expand_at_seconds` 120 ("econ") | 7 / 8 | 6 / 8 | depot 52 to 54 s, 13 to 14 workers at 180 s, first attack 147 to 165 s |
| opening `early_expansion_seconds` 0 only ("min") | 6 / 8 | 8 / 8 | the same economy; second depot after the handover, as `*_standard` |

The two depot-at-once variants against every other opening and against `aggressive_s3` (8 games each; wins of the variant):

| variant | vs standard | vs fast_expand | vs air / leviathan_tech | sum vs the other 3 openings | vs `aggressive_s3` |
| --- | ---: | ---: | ---: | ---: | ---: |
| Skai "econ" | 7 | 5 | 6 | 18 / 24 (0.75) | 6 / 8 |
| Skai "min" | 6 | 4 | 5 | 15 / 24 (0.63) | 7 / 8 |
| Water "econ" | 6 | 0 | 0 | 6 / 24 (0.25) | 6 / 8 |
| Water "min" | 8 | 0 | 0 | 8 / 24 (0.33) | 5 / 8 |

Weighted by the self-ladder draw weights of the opponents (standard 30, the other two 25 each), "min" comes to about 0.63 for Skai and 0.38 for Water. Both are inside the gate band of 0.35 to 0.65, but near its edges. With 8 games per pairing these are rough estimates: the Wilson half-width at 8 games is about 0.3.

How distinct "min" stays (forced traces, the same 64 games):

| | first attack, median (range) | attacks started before 420 s, median |
| --- | --- | ---: |
| Skai pressure ("min") | 153.5 s (147 to 158) | 9 |
| `skai_standard` / `skai_fast_expand` / `skai_air` / `aggressive_s3` | 194.5 / 235 / 219 / 194.5 s | 10.5 / 8 / 10 / 11 |
| Water pressure ("min") | 158.5 s (150 to 165) | 9 |
| `water_standard` / `water_fast_expand` / `water_leviathan_tech` / `aggressive_s3` | 175.5 / 208.5 / 215.5 / 175.5 s | 10 / 11 / 7 / 9 |

The pressure line keeps its build (3 producers by about 90 s, no Windspire or Shell Forge before 16 supply, a cheap-unit mix) and attacks first. Every opening attacks often under the shared attack rules, so the number of attacks does not set it apart; its timing and composition do.

Water "min" plays rock-paper-scissors: it beats `water_standard` 8 of 8 and loses to `water_fast_expand` and `water_leviathan_tech` 0 of 8 each. In one traced loss (`water_fast_expand`, game 2) it banks 300 to 575 rare unspent, because its pre-handover mix has no rare-cost unit and its build order has no rare structure. This report does not diagnose that further.

## 6. Answers to the questions asked

- **(a) The rush never attacks early?** No. It leaves at 130 to 153 s, which is 20 to 80 s before every other opening.
- **(b) It attacks but is too weak?** Partly. Its army at the first attack is about the size of the defender's army, with the HQ and armed workers on the defender's side. The rush cannot build a bigger army sooner, because every opening starts army production at about the same time: first army unit at 55 to 61 s in the rush and standard openings, 73 to 79 s in the fast-expand and tech openings.
- **(c) It does damage, then has no transition?** It does modest damage (Water trades in its favour), and it is then stuck on 6 workers for another 90 to 120 s. The missing "transition" is the depot it chose not to build.
- **The cause** is the opening data (`early_expansion_seconds` 240) multiplied by rule W's served-field worker cap. On this map, with these numbers (depot 100 common, worker 50 common, about 1.6 common per second per worker), any delay of the first depot costs more than an early wave can take back. **A one-base all-in cannot work with the current AI rules.** It might work with a rule that lets a one-base opening put more than 3 workers on a home field, but that is a rule-W change and was not tested. An early-pressure opening that expands at once is not a dud (section 5).

## 7. Not checked

- Full ladders of either variant. Section 5 has 8 games per pairing only, and it does not show how the other openings' win fractions move when the pressure opening improves (for example `water_standard` lost 8 of 8 to Water "min").
- Seeds other than ladder base seed 20261117 (games 0 to 7). Maps other than `prototype_1v1`.
- Rush against rush.
- Response scenarios with the variants.
- Why Water "min" loses to fast_expand and leviathan_tech, beyond the banked-rare observation.
- Whether rule W's cap of 3 per field is a good rule for every opening.

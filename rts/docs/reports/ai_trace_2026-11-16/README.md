<!-- rts/docs/reports/ai_trace_2026-11-16/README.md: findings of the skirmish AI trace review of 16 Nov 2026. Answers seven questions about how the AI plays a full game, with trace line references. Facts and inferences are kept apart. The AI and the simulation were not changed. -->
# Skirmish AI trace review, 16 Nov 2026

Scope: `rts/src/Ai` (profile `rts/data/ai/aggressive.json`, the profile the batch harness uses) playing AI versus AI on the three 1v1 maps. Nothing in `rts/src/Ai` or `rts/src/Sim` was changed; the new tool lives in `rts/tools/BalanceHarness`.

## How the traces were made

```
dotnet run --project rts/tools/BalanceHarness -- --data rts/data --trace --map <id> --matchup <a_vs_b> --seed <base seed> [--game N] [--max-game-seconds S] [--out <file>]
```

The trace plays the game a batch would play (same seed derivation `SeedDerivation.GameSeed(base seed, map, matchup, game)`, same AI profile, same setup through `GameRunner.Play`), so its outcome equals the batch game. It only reads events, state and each controller's `LastCommands`; a test (`HarnessTraceTests.Trace_DoesNotChangeTheGame`) checks that the final state hash equals the untraced game. Rejected commands are counted from `CommandRejectedEvent`; a test (`Trace_CountsRejectedCommandsByReason`) feeds it a command that must be rejected and checks the count, so the zero rejections reported below is a measured zero.

Files in this folder, 18 games (3 maps, 3 matchups, base seeds 20261026 and 7, game index 0, cap 2400 s):

- `<map>__<matchup>__seed<S>.txt`: the timeline (one line per player every 30 game-seconds, line format in line 2 of each file) and the summary (line 168 or so in games that hit the cap, earlier in decided games).
- `<map>__<matchup>__seed<S>.record.json`: the batch-style result record of the same game.
- `side_bias_sweep.txt`: results of 54 extra games (games 0 to 5 of every cell) and 15 games with the two spawns swapped, used in section 5.

Timeline lines come in pairs, P0 then P1: t = 30 s is lines 3 and 4, t = 60 s lines 5 and 6, and in general t = 30 x k s is lines 2k + 1 and 2k + 2. Games that hit the cap end with a "final state" block and a summary; the last ten minutes (t = 1800 s to 2400 s) are lines 121 to 162 in every capped trace.

Outcomes of the 18 games (slot 0 is P0):

| Map | Matchup | Seed 20261026 | Seed 7 |
| --- | --- | --- | --- |
| greybox_1v1 | skai_vs_skai | cap | slot 0 wins, 286 s |
| greybox_1v1 | water_vs_water | slot 0 wins, 250 s | slot 0 wins, 250 s |
| greybox_1v1 | skai_vs_water | cap | cap |
| long_lowland | skai_vs_skai | cap | cap |
| long_lowland | water_vs_water | slot 1 wins, 267 s | slot 1 wins, 267 s |
| long_lowland | skai_vs_water | slot 1 (water) wins, 256 s | slot 1 (water) wins, 263 s |
| twin_chokes | skai_vs_skai | cap | slot 1 wins, 375 s |
| twin_chokes | water_vs_water | cap | cap |
| twin_chokes | skai_vs_water | cap | slot 1 (water) wins, 435 s |

## Summary in ten lines

1. The AI does act: it builds structures, trains workers and an army, attack-moves and focus-fires. Across the 18 traces there are 144 build, 323 gather, 1080 train, 2027 attack-move, 3099 attack-unit and 32 move commands, and 0 rejected commands.
2. Every player gathers exactly 3000 common resource (the two fields of 1500 within 10 tiles of its HQ) and 0 rare, in every game. Spending is 3150 to 3200, so the whole economy is the 200 starting plus 3000 mined.
3. The fields are empty at 240 to 390 s. After that nobody can pay for anything. In the 9 capped games the last training order is at 270 to 420 s and the last real command (anything but `pick_cycle`) at 270 to 450 s.
4. The remaining 1950 to 2130 s of a capped game is identical state sampled 40 times: all workers idle, bank under the cost of the cheapest unit, no commands except a `pick_cycle` every 240 s.
5. The game is decided in the first 130 to 450 s by whose army is left when the money ends. The armies are fed one unit at a time and fight at the map centre before any "attack" starts.
6. Seeds barely matter: the AI generator is only used for an attack-threshold jitter of 0 to 2 supply and the simulation generator only for a Planetary Cycle pick, so many games are identical (end ticks 5018 five times in six games, 6103 three times, and so on).
7. In the water mirror the winner follows the map position, not the slot (section 5); the cause was not isolated.
8. Skai Great Aerie HQs never heal; Water HQs heal 2 health per second on their own flooded ground, so a stalled water HQ recovers to full during the idle time.
9. The composition targets (3 to 1) are never reached: water trained 21 to 25 Graspers and 0 to 1 Shellbacks per game.
10. No plain execution bug (rejected command, unit stuck, worker not returning) was found; the problems are in the rules and the economy numbers.

## 1. Does each AI build an economy, and when?

Observed (every trace, lines 3 to 4 and the "structure sites" and "first:" lines of the summary):

- Start: 4 workers, 200 common, supply cap 10. Both AIs place the first supply structure on their first think (tick 10 for P0, tick 9 for P1) and train workers at the HQ: the first worker appears at tick 248 or 249 (12 s), and by t = 30 s both have trained exactly 2 (`trained=...fledgling:2` or `...tender:2`). That makes 6 workers, which never grows: `workers=6` in every line until units are lost.
- Supply cap 18 at 30 s, 26 at 60 s, 34 at 90 s to 120 s, 42 at 120 s to 150 s (for example `greybox_1v1__skai_vs_skai__seed20261026.txt` lines 3 to 12). The cap is never the limit: the highest supply used in any trace line is 30 of 42.
- Production buildings: 3 supply structures (Roost or Tidepool) and 1 tech building (Windspire or Shell Forge) in every game. Placement ticks of the four structures, greybox skai mirror seed 20261026: P0 10, 450, 1310, 2100; P1 9, 449, 1319, 2229 (summary lines 177 and 213). Water long_lowland: P0 10, 470, 1040, 1910; P1 9, 509, 1089, 1959 (lines 34 and 67 of `long_lowland__water_vs_water__seed20261026.txt`). The slowest is twin_chokes skai mirror P0: 10, 630, 2480, 4620 (line 177).
- Income at 6 workers, per 30 s, t = 60 s to 150 s: greybox 395 to 440, long_lowland 360 to 390, twin_chokes 235 to 280 (about 13, 12 and 8.6 per second). No worker was idle while a field within 12 tiles of its HQ still had resource, at any of the 30 s samples (`nearFields` against `idle`).
- Rare resource: `gathered=...c/0r` in all 36 player-games. The nearest rare field is 23 tiles (long_lowland) to 45 tiles (twin_chokes) from the HQ.

Inferred, with the code that causes it:

- 6 workers: `TrainWorkers` wants `min(max_workers, workers_per_field x fields in range)` = min(8, 3 x 2) = 6 (`rts/src/Ai/AiBrain.cs` line 678; `workers_per_field` 3 at `rts/data/ai/aggressive.json` line 12, `max_workers` 8 at line 14). Only fields within `field_max_distance_tiles` = 10 of the HQ count (`AiBrain.cs` lines 360 to 366; json line 16), and each map has exactly two common fields that near.
- No rare income: the same 10-tile rule excludes every rare field, and the compositions contain only units that cost no rare. The roc rider, stormcaller and similar units that do need rare are therefore never trained.
- The tick 9 versus tick 10 difference is the think stagger: `IsThinkTick` is `(tick + player) % ThinkTicks == 0` with `ThinkTicks` = 10 (`rts/src/Ai/SkirmishAiController.cs` lines 86 and 93), so P1 thinks on ticks 9, 19, 29 and P0 on 10, 20, 30; P1 acts one tick (50 ms) before P0.

## 2. Does it train an army, of what, and when does production stop and why?

Observed:

- First army unit at 46 to 64 s (Windrunner for Skai, Grasper for Water; summary line "first: ... army unit"). Trained over a game, per player: Skai 24 to 33 Windrunners, 0 to 6 Talon Archers, 2 to 3 Fledglings; Water 21 to 25 Graspers, 0 to 1 Shellbacks, 2 to 3 Tenders. Nearly all of them are lost: Skai loses 24 to 33 Windrunners and 0 to 6 Talon Archers, Water 14 to 25 Graspers.
- Production stops at the same moment as income. Last interval with a train command: 240 to 420 s (270 to 420 s in the capped games), for example 270 s in `greybox_1v1__skai_vs_skai__seed20261026.txt` (line 19, P0, and line 20, P1) and 390 to 420 s in `twin_chokes__water_vs_water__seed20261026.txt` (lines 29 and 30). From then on `bank` is 0 to 50 against costs of 50 (worker), 75 (Windrunner), 100 (Grasper or Talon Archer), 175 (Shellback). Examples: `bank=25/0` in lines 21 to 42 of the greybox skai trace; `bank=0/0` for both players in lines 37 to 42 of the twin_chokes water trace.
- Not the reason, with the evidence: supply (used 6 of 42 in the final lines), missing building (3 supply structures and 1 tech building complete in every final state), rejected commands (none, `rejected=-` on every line and `commands rejected, whole game=-` in every summary).
- Composition: target is 3 Windrunner to 1 Talon Archer and 3 Grasper to 1 Shellback (`rts/data/ai/aggressive.json` lines 76 and 99). Actual Water trained: `water.grasper:25,water.tender:2` in 10 of the 36 player summaries, and a Shellback in only 2 of the 36 (both in long_lowland skai_vs_water). Skai trained 0 to 6 Talon Archers against 24 to 33 Windrunners (a 3 to 1 mix would be 8 to 11 Archers), and 0 Talon Archers for the Skai player in the two twin_chokes skai_vs_water games (`trained=skai.fledgling:2,skai.windrunner:33`).

Inferred:

- Production stops because the economy ends (finite fields, section 1), not because of an AI decision. The 3000 common per side is the whole game: Skai spends it on 3 Roosts + 1 Windspire (600) and about 2400 of army and workers.
- The unmet composition comes from the production loop (`AiBrain.cs` lines 730 to 751): every idle producer, in ascending ID order, buys the unit it prefers as soon as the shared ledger can pay. The tech building (Shell Forge, Windspire) is placed after the first supply structures and so has a higher ID than two of them; its units cost more (Shellback 175, Talon Archer 100) than the cheap unit the earlier producers buy at 75 or 100 whenever the bank allows, so the bank rarely reaches the price. I did not trace a single think to prove this; the 0 Shellbacks in 18 water player-games is the evidence and the loop is the candidate cause.

## 3. Does it attack, with what, and what happens?

Observed:

- Real attacks (stance `Attack`, counted by `attacks=` and listed under "stance changes" in each summary) start at 118 to 363 s, with the drawn threshold of 8 to 10 army supply: for example 132 s (greybox skai P0, summary line 179), 123 s (greybox skai_vs_water P0), 133 s and 261 s (long_lowland skai mirror), 346 s and 363 s (twin_chokes water mirror). In 4 of the 9 decided games, the water mirror games, the losing side never reached stance `Attack` (greybox P1, long_lowland P0; `first stance Attack=never` in the summaries).
- The attacks that reach the enemy HQ:
  - `greybox_1v1__skai_vs_water__seed20261026.txt` lines 17 to 24: 8 Graspers reach the Skai base at t = 240 s (line 18, `dE 16.2`), 5 of them stand 1.2 tiles from the Skai HQ at 270 s with 39 attack-unit orders in the interval (line 20), the Skai HQ falls 1500 to 810 to 360 to 324 (lines 19, 21, 23), and the last Grasper dies between 300 s and 330 s (line 22 to 24). The HQ survives with 324 health; the Skai army is gone and both banks are 0.
  - `twin_chokes__water_vs_water__seed20261026.txt` lines 25 to 36: P0 Graspers reach P1's HQ at 390 to 480 s (lines 27, 29, 31, 33 show 7, 4, 3, 1 Graspers at 8.2, 2.6, 2.3, 2.9 tiles), P1's HQ goes 1630, 1006, 430, 190 health (lines 28 to 34) and the last Grasper dies between 480 s and 510 s (line 35 reports it lost). P1's HQ then heals 190 to 1800 (lines 36 onward, +60 per 30 s) and both sides sit at full health to 2400 s.
  - `greybox_1v1__skai_vs_skai__seed20261026.txt` line 20: P1's 2 Windrunners stand 1.8 tiles from P0's HQ at 270 s, total damage to that HQ over the game 385 of 1500 (summary line 214), then both armies are dead by 300 s (lines 21 and 22).
- Attack fate in the capped games: the attacker's army shrinks until it is gone, usually at or near the enemy base, never stalled in place with units alive. In a scan of every 30 s line of all 18 traces, no army with units alive stood within 12 tiles of the enemy HQ without an attack-move (`atk=`) order and an attack order in the interval (the only lines with `cmds=-` within 12 tiles of an HQ are line 22 of the greybox skai_vs_water trace, where one Grasper took the Skai HQ from 360 to 324, and lines 31 and 33 of the twin_chokes water trace, where the enemy HQ lost 240 health in those 30 s; in each the attackers were already ordered and hitting it).
- No retreat loops: `retreats=` is 0 or 1 in every game. The `Retreat` stance is transient (the same think moves to `Massing`), so a retreat shows as `Attack -> Massing` with `retreats 1` (for example greybox skai mirror seed 20261026, stance lines in the summary: 132 s attack, 203 s retreat, 207 s `Defend`).
- Stance flip-flop: between `Massing` and `Defend` several times a minute, for example 11 stance changes by P1 in `greybox_1v1__skai_vs_water__seed20261026.txt` (summary lines 217 to 227, 7 flips between 135 s and 204 s), 14 by P0 in `twin_chokes__skai_vs_water__seed20261026.txt` (lines 181 to 194). Every `Massing` entry draws a new threshold (the thresholds in those lines change 8, 9, 10, 8, ...).
- Command churn: P1 issued 82 attack-moves and 28 attack-units in the 30 s before t = 180 s in greybox skai mirror (line 14, `cmds=attack_move:82`); attack-unit orders outnumber every other command (3099 of 6807 over the 18 traces).

Inferred, with the code:

- The first fights happen during rallying, before any attack. At 60 s the single army unit of each side stands at (27.5, 32.1) and (32.2, 32.1) with stance `Massing` (greybox skai mirror seed 20261026 lines 5 and 6, `armyAt`); at 90 s units are being lost with stance `Massing` (lines 7 and 8: 2 and 3 Windrunners lost). The cause is the profile's `rally_at_core: true` (`rts/data/ai/aggressive.json` line 60): while the AI's team does not hold the Core, `RallyPoint` returns the Core centre (`AiBrain.cs` lines 1227 to 1231), and `Mass` orders every idle army unit there one by one as it is trained (lines 892 to 898). Units therefore cross the map alone, meet the other side's units alone at the centre and trade there. The attack threshold of 8 supply is only reached after about 120 s because those early units are being killed.
- The attacks end because the armies are not replaced: an `Attack` with no army left goes back to `Massing` without a retreat (`AiBrain.cs` lines 904 to 908), which is the `Attack -> Massing ... retreats 0` line (long_lowland skai mirror, summary line 182, 292 s) and cannot be re-formed because there is no income (section 2).
- Why the HQ survives a won fight: a Rooted HQ has a weapon (`CombatSystem.EffectiveWeapon`, `rts/src/Sim/Combat/CombatSystem.cs` lines 139 to 147), so the last attackers also take HQ fire. I did not measure the HQ's share of the deaths.
- Water HQs heal: `FloodingMechanic.Heal` heals water-layer units on their own team's flooded tiles (`rts/src/Sim/Factions/Water/FloodingMechanic.cs` lines 546 to 566) by `water_unit_heal_per_second` = 2 (`rts/data/tuning/flood.json` line 28), which matches the +60 per 30 s in the trace. The Great Aerie is an air unit and does not heal: 1115 of 1500 in greybox skai mirror stays unchanged from 300 s to 2400 s (lines 21 to 42 and 165), 81 of 1500 in twin_chokes skai mirror from 390 s on.

## 4. The 9 capped games: what both sides do in the last 10 minutes

Observed: in all 9 capped games, for both players, the 21 samples from 1800 s to 2400 s (lines 121 to 162) are identical in bank, supply, workers, structures, army and HQ health, with no unit trained or lost, `army=-`, and `cmds=-` except a single `pick_cycle` every 240 s (ticks 9609, 14409, ..., 43209) by one player. A script over the nine files confirmed 1 distinct state per player over those lines. In every case the state was already reached by 270 to 450 s, so the last 30 to 36 minutes are dead time.

| Game | State of the last 10 minutes, P0 / P1 | Final HQ health P0 / P1 |
| --- | --- | --- |
| greybox skai mirror, seed 20261026 | P0: 2 workers, bank 25; P1: 6 workers, bank 25; no army | 1115 / 1500 |
| greybox skai_vs_water, both seeds | both 6 idle workers, bank 0 | 324 / 1800 |
| long_lowland skai mirror, both seeds | both 6 idle workers, bank 25 | 1500 / 911 |
| twin_chokes skai mirror, seed 20261026 | P0 bank 50, P1 bank 0, 6 idle workers each | 81 / 1500 |
| twin_chokes skai_vs_water, seed 20261026 | P0 bank 25, P1 bank 0, 6 idle workers each | 1404 / 1800 |
| twin_chokes water mirror, both seeds | both bank 0, 6 idle workers | 1800 / 1800 (P1 had 190 at 480 s) |

Final state lines are the two lines after `== final state` (lines 165 and 166 in each capped trace); every field in range reads `C0` from 240 to 390 s on (`nearFields`).

Inferred: both AIs are alive and the match cannot end because nothing can still produce damage: no army, no income, no unit left to train. `all_in_when_mined_out` (`AiBrain.cs` line 344, json line 58) drops the attack threshold to 1 supply and stops retreats, but it needs an army and money to matter.

## 5. Why does the spawn side decide mirror matches?

Observed, from `side_bias_sweep.txt` part A (games 0 to 5 of each cell, base seed 20261026):

- Water mirror: greybox slot 0 wins 6 of 6; long_lowland slot 1 wins 6 of 6; twin_chokes 6 of 6 capped.
- Skai mirror: greybox 5 decided (slot 1 three times, slot 0 twice); long_lowland 5 of 6 capped (1 slot 0); twin_chokes 5 of 6 capped (1 slot 1).
- Skai versus water: long_lowland slot 1 wins 5 of 6 whichever faction holds slot 1; greybox slot 0 wins 2 of 6 (both with water in slot 0) and 4 are capped; twin_chokes 6 of 6 capped. 27 of the 54 games are capped.
- The games repeat: the 6 greybox water games end at tick 5018 five times and 5259 once; the 6 greybox skai mirror games end at 6103 (three times), 5730 (twice) or cap. Seeds enter the game in only two places: the AI's attack-threshold draw `rng.NextInt(profile.AttackSupplyJitter + 1)` with jitter 2 (`AiBrain.cs` line 159, jitter at json line 32) and the simulation's own generator, which only the Planetary Cycle uses when no cycle was picked (`rts/src/Sim/Cycles/CycleSystem.cs` line 486).
- Experiment: in a copy of `rts/data` I swapped the two spawn entries of each map (so slot 0 starts where slot 1 normally starts) and replayed (`side_bias_sweep.txt` part B). Water mirror game 0: greybox flips from slot 0 to slot 1 (the player at (8, 8) wins in both runs); long_lowland flips from slot 1 to slot 0 (the player at (56, 32) wins in both runs). In both, the winner follows the map position, not the player slot, which rules out causes that depend on the slot: the think stagger, `(issuer, sequence)` command order and the lower entity IDs of slot 0. Skai versus water and the skai mirror do not give a consistent picture: after the swap the greybox skai mirror games 1 and 2 are capped (decided in part A), and long_lowland skai_vs_water game 1 is won by Skai in both arrangements, game 0 by the east player in both. So for those matchups the result also depends on small differences (unit IDs, thresholds), not on position alone.
- Terrain symmetry, checked on `height_rows` and `ramp_rows` (the whole terrain description of a map file) with a throwaway script:
  - long_lowland and twin_chokes: terrain exactly symmetric under a 180 degree turn (0 mismatches); the resource fields too.
  - greybox_1v1: terrain is symmetric under swapping x and y (0 mismatches) but not under a 180 degree turn, the relation between the two bases: 66 tiles differ in height and 16 in ramp, all on the rim of the central basin (tiles x or y from 19 to 44): the basin's rim on the east and north side is one tile further out than the mirror of its west and south rim (for example ramps at x = 20 on the west and x = 44 on the east instead of 43; the Core is at (32, 32)).
  - HQ to field distance, computed from the map files: equal for both sides on all three maps (greybox 4.3 and 4.3 tiles, twin_chokes 6.4 and 5.5 tiles, long_lowland 4.9 and 4.3 tiles, from each HQ to its two common fields).
  - Path length for ground units was not measured. Skai units fly, so for skai it does not apply; on long_lowland and twin_chokes the terrain is exactly symmetric, so a difference would have to come from the flow field's direction order, not the map.

Confirmed asymmetries in the code (each is real; none was shown to decide a game):

- Build site search: `TryFindSite` scans rings around the HQ tile by ascending row then column, taking the first fit (`rts/src/Ai/AiBrain.cs` lines 604 to 637). P0 and P1 use the same order in map coordinates, not in their own frame, and the HQ tile of a spawn at integer coordinates is the tile whose corner it is (tile (8, 8) for a spawn at 8.0), whose 180 degree image is tile (55, 55), not (56, 56). Seen in the traces: greybox P0 builds at tiles (5,5), (7,5), (5,7), (11,7), behind its HQ in the map corner, while P1 builds at (53,53), (55,53), (53,55), (59,56), between its HQ and the centre (summary lines 177 and 213 of the greybox skai mirror trace).
- Think stagger: P1's decisions come one tick before P0's (section 1).
- Combat order: within a tick units act in ascending entity ID, and a hit with `hit_delay_ticks` 0 lands at once (Grasper, `rts/data/units/water/grasper.json` line 42), after which the victim can no longer be picked as a target by units later in the loop (`CombatSystem.cs` lines 11, 20 and 346). Slot 0's units mostly have the lower IDs. The swap experiment shows this does not decide the water mirror, but it applies to every Grasper fight.
- Tide, pathing and vision tie-breaks were not examined.

Inference: the water mirror is decided by something tied to the map position that I have not isolated. The terrain is exactly symmetric on long_lowland, so the greybox basin offset cannot be the reason there; the build-site order and tile-corner offsets of the HQ are the asymmetries that exist on every map and that I could point to in code.

## 6. Which commands are rejected most, and why?

Observed: none. `rejected=-` on every timeline line and `commands rejected, whole game=-` for both players in all 18 summaries; no `REJECTED` result in any "last 20 commands" list or in the 54 + 15 extra games of `side_bias_sweep.txt`. The AI submits at most the commands it computed from its own view, and the simulation accepted every one (a train order that cannot be afforded is not issued: `Pay` books cost and supply first, `AiBrain.cs` lines 394 to 399).

What the AI actually sends, by kind, over the 18 traces: attack_unit 3099, attack_move 2027, train_unit 1080, gather 323, build_structure 144, pick_cycle 102, move_unit 32.

## 7. Anything that looks like a plain bug

Checked and not found: workers idle while a field in range had resource (none at any sample); workers that do not return cargo (income per 30 s is steady until the fields empty); structures never placed (4 placed in every game by 2 to 6 minutes); an army standing next to an enemy HQ without attacking (none, section 3); production stuck on a rejected order (none).

Real problems seen, from most to least important:

1. Finite economy with no fallback (rules and data, not a code fault): 3000 common per side (`resource_fields` of each map file, lines 154 to 157 of `greybox_1v1.json`, `long_lowland.json` and `twin_chokes.json`, 1500 each, plus `starting_common` 200 at `rts/data/tuning/economy.json` line 6) is spent by 240 to 390 s, nothing re-opens, and the match cannot end. The AI does not know about other fields (rare fields lie beyond `field_max_distance_tiles` 10) and cannot expand.
2. Piecemeal army: units rally one by one at the Core (`rally_at_core`, `AiBrain.cs` lines 872 to 899 and 1225 to 1237) and die there before the first real attack. Observed in lines 5 to 10 of the greybox skai trace.
3. Composition never reached (3 to 1 targets, 25 to 0 actual): section 2.
4. Stance flip-flop `Massing` and `Defend` several times a minute, each flip redrawing the attack threshold from the AI generator (`AiBrain.cs` line 159): section 3. Harmless to the result as far as I can see, but it makes the seed matter in an unplanned way.
5. Only 6 workers for a 10-tile field radius: `max_workers` 8 is never used.
6. Seeds barely vary games: 54 games gave only a few distinct end ticks per cell (section 5), so a "20 games per cell" batch holds far fewer than 20 independent samples.
7. After mining out, a stalled water HQ heals to full while a Skai HQ does not: a balance asymmetry in what a stalemate leaves behind.
8. Spawn-side effect in the water mirror: the cause is not isolated (section 5).

## What was not checked

- Per-fight causes (who loses the first trade at the Core and why), the path length for ground units, and which of the asymmetries in section 5 decides the water mirror.
- Games longer than 2400 s, other seeds than 20261026 and 7 for the full traces, and profile `defensive`.
- The Unity side. Everything here is headless.

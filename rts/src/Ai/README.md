<!-- rts/src/Ai/README.md: what the skirmish AI assembly (Rts.Ai) is, how a host runs it, its decision rules, its determinism guarantees and where it differs from the Phase 1 spec. -->
# Ai (skirmish AI, assembly `Rts.Ai`)

The skirmish AI plays a player slot from the host, outside the simulation (spec: `rts/docs/specs/skirmish_ai.md`). It produces the
same command objects a human player does (architecture rule 2) and reads only what that player could know: its own units and
economy, public map data (spawns, resource fields and how much they hold, terrain) and enemy units through its team's vision.

- Engine-free C# 9, `netstandard2.1`, references only `Rts.Sim` (`Rts.Ai.csproj`, and `Rts.Ai.asmdef` with `noEngineReferences`
  for Unity; `package.json` makes it a local Unity package like `com.rtsgame.sim`).
- No floating point, no `System.Random`, no clock, no threads, no static state. Collections are sorted; ties go to the lower ID or index.

## Host loop

```
sim.Start();
var controllers = new List<IPlayerController> { new SkirmishAiController(new PlayerId(0), AiProfile.FromDataSet(data, "aggressive")) };
AiHost.StartAll(sim, controllers);
while (running) { AiHost.ThinkAll(sim, controllers); sim.Step(); }
```

Decision: a controller **submits** through an `ICommandSink` (the host decides the scheduling tick: the current tick locally, plus the
input delay online) rather than returning a list. `SimulationCommandSink` submits into a local simulation; `ListCommandSink` only
collects. `Think` must be called before every step (it reads the previous tick's `UnitDamagedEvent`s on own units); it decides only on
its think ticks, `(tick + player) % ThinkTicks == 0`, with `ThinkTicks = max(1, think_interval_seconds in ticks)`.

## Files

| File | Role |
| --- | --- |
| `IPlayerController.cs` | `IPlayerController` and `ICommandSink` |
| `CommandSinks.cs` | `SimulationCommandSink`, `ListCommandSink`, `AiHost` (ascending-slot host helper) |
| `AiProfile.cs` | Parses and validates `rts/data/ai/<id>.json` (errors name file and field) |
| `AiWorldView.cs` | What one player may know, copied out of the simulation once per think |
| `AiMemory.cs` | Per-controller memory: stance, thresholds, known enemy structures, damage reports, sequence counter |
| `AiBrain.cs` | The decision rules (pure: view + profile + memory + own generator to intents); ai_mechanic_use.md P2 (`use_support_units`) adds rule SU (train support units by army size, in `Produce`), the RQ-1 support extension and rule ST (support trail, in `Army`); P3 (`use_wall_breach`) adds rule WB (attack the enemy wall that seals a unit's route, in `Army`, from `AiMemory.UnreachableGoals`); P4 (`use_walls`) adds rule WL (`AiBrainWalls.cs`: walls and gates on the authored lines of the held areas, between static defence and expand) |
| `AiIntel.cs` | Enemy model and scouting route (skirmish_ai_v3.md package B) |
| `FightPredictor.cs` | Static fight predictor over unit definitions and the damage table (skirmish_ai_v3.md C.1) |
| `AiCounter.cs` | Unit pool, counter scores and counter-mix weights (skirmish_ai_v3.md C.3 and C.4) |
| `AiBrainFight.cs` | The package D fight choices (second half of the brain's working state): attack and retreat by prediction, target choice, rule HR harassment, measured defence, unit control (skirmish_ai_v3.md D) |
| `AiAbilities.cs` | Casts (ai_mechanic_use.md P1, `use_casts`): `ChooseGrounding` (Downdraft centre) and `ChooseWave` / `WaveTiles` (wave direction); rules CG and CW in `AiBrain` call them right after unit control |
| `AiWallLines.cs` | Authored wall lines (ai_mechanic_use.md P4): the parsed `ai/walls/<map id>.json` (`AiMapWalls`, `AiWallArea`, `AiWallLine`), `Parse` (validation against the map and tuning) and `HeldAreas` (which areas the AI holds, from its own units); attached to a profile by `AiProfile.FromDataSet` as `WallLines` |
| `AiBrainWalls.cs` | Rule WL (`use_walls`): a partial of `AiBrain`'s `Turn` that orders `build_wall_line` and gates on the held areas' lines from spare money within `wall_max_spend_common` |
| `AiBudget.cs` | Package B: the budget queue arithmetic on arrays (Open, Distribute, Close, the defence ceiling, the rare surplus helpers) |
| `AiBrainBudget.cs` | Package B: the Open and Close steps of a think and the buying helpers of the six queues (part of `AiBrain.Turn`) |
| `AiFight.cs` | Predictor-entry helpers for package D (merge, scale, structure entries) |
| `AiProximity.cs` | Grid index over a unit list so spatial tests look only at nearby cells (bounded by the army size), not at every pair |
| `AiIntent.cs` | Intents and `AiCommandWriter` (intents to commands with sequence numbers) |
| `SkirmishAiController.cs` | `IPlayerController` implementation tying it together |

## Rules (each think, in this order)

Intel, root an Uprooted HQ next to a field, the cycle pick (the Core holder picks the first allowed cycle of the faction's `cycle_preference`
once per cycle), idle workers to the nearest field under `workers_per_field`, supply structure and build
order (cost reserved when due but unaffordable), worker training (ignores reservations), production (one purchase at a time: the unit with the largest composition deficit, its cost held when it cannot be paid yet), an extra production building
when money piles up, then the army: Defend (visible enemy army or a recent hit inside the base; held for `defend_hold_seconds` after the last threat), Massing (home rally point, attack at the drawn threshold within
the tide window, or at once against a known enemy HQ at or below `kill_hq_health_fraction` and last seen within `kill_seen_within_seconds`), Attack (only the attack group; staging at the Core centre with `rally_at_core` while the team does not hold it, else short of the target; then attack-move; focus fire on visible enemy army units in reach; siege an
undefended visible HQ; retreat on losses), Retreat (move home, regroup timer). All in once the base is mined out. Details and every
number: the header of `AiBrain.cs` and the profile files.

Skirmish AI v3 (rts/docs/specs/skirmish_ai_v3.md) adds, each behind a profile switch (all off in `aggressive` and `defensive`, which stay v2):
scouting and the enemy model (`use_scouting`, `use_enemy_model`, package B), then the counter-mix (`use_counter_mix`: the plan composition is replaced
by weights computed from the enemy model and the unit data), tech on demand (rule T), static defence (rule SD) and rare priority (rule RS) in package C.
`aggressive_s1.json` has stage 1 on, `aggressive_s2.json` stages 1 and 2.

## Determinism

The AI's memory is **not** simulation state and is never hashed. Its only random source is its own `DeterministicRandom` (the
simulation's PCG32 class) seeded `matchSeed ^ (0x9E3779B97F4A7C15 * (player + 1))`; it never draws from `sim.Rng`. Package R (skirmish_ai_v4_rl.md) adds a second generator, `AiVariety` (seeded by `SplitMix64(seed ^ "VARIETY1")`),
created only when a profile has `composition_jitter_fraction`, `expand_jitter_seconds` or `attack_advantage_jitter` above 0, so the old stream's
draws never change and profiles with those keys at 0 play as before. Package L adds `openings` to each faction plan of a profile (weighted, one drawn at match start from the variety stream, a build order for the whole game and a mix and timing values until its handover); only `aggressive_s4r` has any. Its commands are
ordinary inputs, so two runs with the same data, script and seed submit the same commands and print the same hashes:
`BalanceHarness --script rts/data/scripts/ai_vs_ai.json` twice gives identical `OUTCOME` and `STATE_HASH` lines, and
`ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided` (rts/tests/Sim.Tests/Ai) checks it. Like simulation code, changes here get a
determinism review.

## Differences from the spec (written before HQ, economy, vision and flooding existed)

- Read API: the economy is `Player.Common/Rare/SupplyUsed/SupplyCap`, fields are `sim.Resources` (index = map order), orders are
  `Unit.HasMoveTarget/IsAttackMoving/GatherState/AttackTarget`, vision is `sim.Vision.CollectVisibleEnemies` and `IsTileVisible`.
  There is no `PlacementRules.CanPlace`: the AI picks sites itself (3x3 block free, same height as the HQ, no ramp, clear of fields).
- No expansion or HQ relocation (the Phase 1 roster has no drop-off structure; the HQ stays Rooted), so the spec's `expand_*`,
  `max_bases`, `base_site_taken_radius_tiles`, `drop_off_building` and `relocate_hq_when_main_depleted` keys are absent. The Phase 1
  supply structures are the production buildings (`provides_supply`), so `supply_building` names one of them.
- Added keys: `field_max_distance_tiles`, `build_ring_min_tiles`, `build_ring_max_tiles`, `field_clearance_tiles`,
  `rally_distance_tiles`, `damage_memory_seconds`, the staging keys, `focus_radius_tiles` and `all_in_when_mined_out`. Staging, focus
  fire, the siege rule and all-in were needed so AI-versus-AI on `greybox_1v1` (3000 common per side, then the mains are empty) ends
  inside the 6000-tick exit run instead of stalling against the defender's HQ.
- `AttackUnitCommand` is used (focus fire and siege), only ever on an enemy the team sees on that think.
- Phase 2 (planetary_cycles.md section 5): the root key `rally_at_core`, the per-faction `cycle_preference` list (every cycle ID, most wanted
  first; validated against `rts/data/cycles` when the profile comes from `AiProfile.FromDataSet`), the `PickCycle` intent and the cycle and
  Core fields of `AiWorldView`. Damage with no attacker (a Quake on an own defence) is not recorded as a hit, so it never triggers Defend.
- Profiles are loaded through the `ai/` route of `DataLoader` (`DataSet.AiProfiles`) and validated here; script player entries may
  name a profile with `"ai"`, and the harness has `--ai <slot>:<profile>`.
- Tests live in `rts/tests/Sim.Tests/Ai/` (the existing NUnit project references `Rts.Ai`) rather than a separate `Ai.Tests` project.
- Field saturation (rts/docs/specs/field_saturation.md package AI-E): the root keys `use_saturation_workers`,
  `saturation_extra_workers_per_field`, `rare_cap_yields_to_saturation`, `use_saturation_expansion`, `expand_saturation_fraction` and
  `expand_block_threat_supply` (every profile carries them; false, 0, false, false, 1, 0 play exactly as before). With them on, a field's
  worker cap is its gather slots plus the workers that cover the walk to the nearest drop-off, the rare soft cap closes rare fields only
  while a served common field is below its cap, a depot is also built at `expand_saturation_fraction` of the uncapped worker target, and a
  seen threat blocks a depot only from `expand_block_threat_supply` supply. `aggressive_s5` has them on (max_workers 48, max_live_depots 4);
  `abl_s5_no_e` is the same file with AI-E off. `AiWorldView` carries the gather slots, the trip length and the interact range.
- Placement and defence (rts/docs/specs/field_saturation.md package AI-P, `AiBrainPlacement.cs`): the root keys `use_forward_producers`,
  `use_threat_defence`, `threat_defence_min_enemy_supply`, `threat_defence_min_enemy_advantage`, `threat_defence_max_value_share` and
  `threat_defence_release_fraction` (every profile carries them; false, false, 0, 0, 0, 1 play exactly as before). With them on, the extra
  and tech producers go to the own base (Rooted HQ or a complete depot with a live field) with the fewest producers, and rule SD builds a
  defence at the base nearest the enemy when the enemy model and the fight predictor favour the enemy; the per-base cap is
  min(profile, Sim). `aggressive_s5` has them on (static_defence_max_per_base 6); `abl_s5_no_p` is the same file with AI-P off.
  `AiWorldView` carries `StaticDefencesPerBaseMax` and `BaseRadiusTiles`.
- Harass speed (rts/docs/specs/field_saturation.md package AI-H, `AiIntel.ChaserSpeed` and `MayHarass`): the root key `harass_speed_ratio`
  (every profile carries it; 0 plays exactly as before). Above 0 a unit may harass when its speed is at least that ratio times the chaser
  speed: the count-weighted mean speed of the enemy model's armed types that can hit its layer, else the mean over the enemy factions'
  armed mobile definitions, else it qualifies. The flyer exemption and `harass_min_speed_tiles_per_second` then do not apply.
  `aggressive_s5` has 1.2; `abl_s5_no_h` is the same file with AI-H off. `AiWorldView.EnemyFactionIds` lists the enemy factions.
- Budget queues (rts/docs/specs/ai_budget_queues.md package B, `AiBudget.cs` and `AiBrainBudget.cs`): the root keys `use_budget_queues`,
  `budget_common_weights`, `budget_rare_weights` (objects with the six queue IDs economy, expansion, production, army, tech, defence),
  `budget_opening_production_factor`, `budget_threat_defence_factor`, `budget_worker_floor`, `budget_defence_max_spend_share` and the three
  `budget_rare_surplus_*` keys (every profile carries them; the switch false plays exactly as before). With the switch on the AI divides its bank between
  the queues in proportion to the weights and each rule buys only from its own queue's account (see the header of `AiBrain.cs`); `aggressive_s5_b` has
  it on and `abl_s5b_no_b` is the same file with it off (it plays exactly as `aggressive_s5`). Queue state lives in `AiMemory` (never hashed).

<!-- rts/docs/specs/skirmish_ai.md -->
<!-- Implementation spec for the Phase 1 skirmish AI: a player controller that runs in the host before each tick and produces ordinary commands, its decision rules, its own seeded random generator, the data/ai profile format, and the economy, combat and HQ APIs it expects. Part of rts/docs/specs (index: README.md); built by a Sonnet builder in rts/src/Ai. -->
# Spec: Skirmish AI (`rts/src/Ai`, assembly `Rts.Ai`)

## 1. Purpose and design-doc source

- Technical planning / AI: "A skirmish AI is needed early: vs AI is how new free players practise, how the campaign is built, and what drives the balance simulations."
- Balance: "run headless AI-versus-AI simulations every patch, 500 games per matchup per map".
- Core gameplay loop: "Opening (0 to 3 min). Spawn with the mobile HQ unit and a few workers. Gather, build supply and the first production building." "Expansion (3 to 9 min). Take a second base ..."
- Matchups: "Opponents time attacks for low tide; the water player times theirs for high tide."
- Architecture rule 2: the AI produces the same command objects as a player. It never runs inside `Simulation.Step`.

## 2. Data

New folder `rts/data/ai/`, one profile per file `ai/<id>.json`. `DataLoader` gains the `ai/` prefix and stores each file's parsed `JsonValue` in `DataSet.AiProfiles` (`SortedDictionary<string, JsonValue>`, ordinal); `Rts.Ai.AiProfile.Parse(JsonValue, ContentCatalog)` validates it with the same `Require*` helpers (errors name file and field, for example `ai/standard.json: field 'attack_army_supply' is missing`). Every number is a **placeholder**. The excerpt below shortens some comments; the real file gives every key the full `// <what>. Unit: . Range: . Source: .` line. Building IDs are set from the Phase 1 building data (plan step 9); the parser checks each ID exists in content, belongs to that faction and has the right kind.

```jsonc
// rts/data/ai/standard.json: default skirmish AI profile ...
{
  "$schema_version": 1, "$doc": "...", "id": "standard",
  // Time between AI decisions. Unit: seconds. Range: 0.1 to 5. Source: placeholder.
  "think_interval_seconds": 0.5,
  // Most commands one decision may submit; later intents are dropped. Unit: commands. Range: 1 to 200. Source: placeholder.
  "max_commands_per_think": 40,
  // Most workers sent to one resource field. Unit: workers. Range: 1 to 8. Source: placeholder.
  "workers_per_field": 3,
  // Worker count the AI stops training at. Unit: workers. Range: 1 to 100. Source: placeholder.
  "max_workers": 45,
  // Build supply when free supply is at or below this. Unit: supply. Range: 0 to 40. Source: placeholder; Opening.
  "supply_buffer": 6,
  // Wait before re-trying an unfinished build order step. Unit: seconds. Range: 1 to 60. Source: placeholder.
  "build_retry_seconds": 10,
  // Earliest first expansion. Unit: seconds. Range: 60 to 900. Source: Core gameplay loop (Expansion 3 to 9 min); placeholder.
  "expand_at_seconds": 240,
  // Time between later expansions. Unit: seconds. Range: 60 to 900. Source: placeholder.
  "expand_interval_seconds": 180,
  // Expand early when the main base's common fields fall below this share of their start amount. Unit: fraction. Range: 0 to 1. Source: placeholder; Economy (fields run out).
  "expand_when_main_remaining_fraction": 0.4,
  // Most bases including the main. Unit: bases. Range: 1 to 6. Source: placeholder.
  "max_bases": 3,
  // A field this close to a known drop-off is taken. Unit: tiles. Range: 2 to 20. Source: placeholder.
  "base_site_taken_radius_tiles": 8,
  // Army supply at which the AI attacks. Unit: supply. Range: 2 to 200. Source: placeholder.
  "attack_army_supply": 24,
  // Extra supply, drawn 0..this from the AI's own generator for each attack wave. Unit: supply. Range: 0 to 40. Source: placeholder.
  "attack_supply_jitter": 6,
  // Retreat when the army falls below this share of its supply at the start of the attack. Unit: fraction. Range: 0 to 1. Source: placeholder.
  "retreat_army_fraction": 0.5,
  // Retreat when local own power is below this multiple of local enemy power. Unit: ratio. Range: 0 to 2. Source: placeholder.
  "retreat_power_ratio": 0.6,
  // Radius around the army centre used for local power. Unit: tiles. Range: 4 to 40. Source: placeholder.
  "engagement_radius_tiles": 12,
  // Enemies this close to an own structure or HQ are a threat. Unit: tiles. Range: 4 to 40. Source: placeholder.
  "defend_radius_tiles": 16,
  // Threat supply that recalls an attacking army home. Unit: supply. Range: 1 to 100. Source: placeholder.
  "defend_recall_supply": 6,
  // Time after a retreat before the next attack may start. Unit: seconds. Range: 0 to 120. Source: placeholder.
  "regroup_seconds": 20,
  // Move the HQ to an expansion when every main-base common field is empty. Unit: boolean. Range: true or false. Source: Late game (HQ relocations); placeholder.
  "relocate_hq_when_main_depleted": true,
  "factions": {
    "skai": {
      // Unit, supply, drop-off and production building IDs (placeholders until step 9 data).
      "worker_unit": "skai.fledgling", "supply_building": "skai.nest", "drop_off_building": "skai.roost",
      // Ordered steps: keep at least `count` of `structure` once supply used reaches `at_supply_used`.
      "build_order": [ { "structure": "skai.aerie_hall", "count": 1, "at_supply_used": 10 } ],
      // Army mix as integer weights per unit ID. Range: 0 to 100 each, at least one above 0.
      "composition": { "skai.windrunner": 3, "skai.roc_rider": 1 },
      // Attack only while the tide level (0 low, 1 high) is inside [min, max]. Unit: fraction. Source: Matchups (Skai strikes at low tide).
      "attack_tide_min": 0, "attack_tide_max": 0.5,
      // Ignore the tide window at or above this army supply. Unit: supply. Range: 2 to 200.
      "attack_tide_override_supply": 40
    },
    "water": { /* same keys with water IDs; attack_tide_min 0.6, attack_tide_max 1 (high tide) */ }
  }
}
```

## 3. State

All AI state lives outside the simulation and is **not** in the state hash; the AI's commands are inputs, so determinism is proven by harness hashes. `AiMemory` per controller: `Stance` (enum `Massing`, `Attack`, `Defend`, `Retreat`), `AttackThreshold` (int supply), `AttackStartSupply` (int), `RegroupUntilTick` (int ticks), `LastBuildAttemptTick` (`SortedDictionary<string, int>` by building ID), `KnownEnemyStructures` (`SortedDictionary<EntityId, FixVec2>`, last seen position, removed when seen dead or its tile visible and empty), `NextSequence` (uint). Plus `Rng`: a standalone `DeterministicRandom` seeded `matchSeed ^ unchecked(0x9E3779B97F4A7C15UL * (ulong)(player.Value + 1))`. The simulation's `sim.Rng` is off-limits (it is locked between steps anyway).

## 4. Per-think algorithm

Host loop (harness, Unity, campaign): for each controller in ascending `PlayerId`, `Think(sim, sink)`; then `sim.Step()`. A controller thinks when `(sim.Clock.Tick + player.Value) % ThinkTicks == 0`, `ThinkTicks = max(1, SecondsToTicks(think_interval_seconds))` (staggered per player). Commands are scheduled at `sink.ScheduleTick` (harness: `sim.Clock.Tick`; online: plus the lockstep input delay), issuer = the AI's player, sequence = `NextSequence++`.

**Allowed reads** (`AiWorldView`, built once per think, read-only): own units and structures; own economy; enemy units only through `sim.Vision.CollectVisibleEnemies(team)` and `IsTileExplored`; terrain heights and blockers; map spawns and resource fields (public map data, `sim.Terrain.Map`); `TideClock` level; the clock. Everything iterated in ascending ID or index order; distances compared as Fix64 squared distances; ties broken by lower ID or index unless a rule says RNG.

**Rules, in this fixed order** (each appends intents; the writer stops at `max_commands_per_think`):
1. *Intel.* Update `KnownEnemyStructures`. `Threat` = visible enemy non-worker units within `defend_radius_tiles` of any own structure or the HQ.
2. *Workers.* Each idle worker (no order), ascending ID: `GatherCommand` to the nearest common field (from the worker) that has resources and fewer than `workers_per_field` assigned. Train a worker at the Rooted HQ (or a drop-off that trains) when idle, workers `< min(max_workers, workers_per_field * own fields in use + 1)`, affordable and supply is free.
3. *Supply.* When `SupplyCap - SupplyUsed <= supply_buffer`, cap below `supply_cap` (tuning) and no supply building is under construction, and `build_retry_seconds` passed since the last attempt: the lowest-ID gathering worker builds `supply_building` at the first placeable tile on Chebyshev rings 3 to 10 around the HQ (ascending index per ring, `PlacementRules.CanPlace`).
4. *Build order.* The first entry with fewer own structures (built or under construction) than `count`: if `SupplyUsed >= at_supply_used`, affordable and retry time passed, build it as in rule 3. Later entries wait (strict order).
5. *Production.* Each idle production building, ascending ID, trains the unit with the largest deficit `d_u = w_u * (N + 1) - c_u * W` (integers: `w_u` weight, `c_u` own count of u, `N` sum of counts, `W` sum of weights; ties by ordinal ID), among units it can train, if affordable and supply is free.
6. *Expansion.* When there is no `Threat`, bases `< max_bases`, and either `seconds >= expand_at_seconds + (bases - 1) * expand_interval_seconds` or the main fields' remaining fraction `< expand_when_main_remaining_fraction`: candidate sites are common fields farther than `base_site_taken_radius_tiles` from any known drop-off; pick the nearest to the HQ; on an exact tie draw `Rng.NextInt(tiedCount)` over the tied sites in index order. The nearest worker builds `drop_off_building` on rings 3 to 8 around that field.
7. *Army* (army = own mobile units that are not workers or the HQ; `ArmySupply` = their supply). Power of a set = sum of `supply * health / maxHealth` (Fix64).
   - `Threat` exists and (stance is not `Attack` or threat supply `>= defend_recall_supply`): stance `Defend`, `AttackMoveCommand` for every army unit to the threat centre (mean of positions). Back to `Massing` when the threat is gone.
   - `Massing` (enter it with `AttackThreshold = attack_army_supply + Rng.NextInt(attack_supply_jitter + 1)`): when `tick >= RegroupUntilTick`, `ArmySupply >= AttackThreshold` and (tide level in `[attack_tide_min, attack_tide_max]` or `ArmySupply >= attack_tide_override_supply`): stance `Attack`, `AttackStartSupply = ArmySupply`, `AttackMoveCommand` for every army unit to the nearest known enemy structure, else the nearest enemy spawn.
   - `Attack`: idle army units get the same attack-move (reinforcements). Retreat when `FromInt(ArmySupply) < retreat_army_fraction * FromInt(AttackStartSupply)` or `ownPower < retreat_power_ratio * enemyPower` within `engagement_radius_tiles` of the army centre (enemy power above 0).
   - `Retreat`: `MoveUnitCommand` for every army unit to the HQ position; `RegroupUntilTick = tick + SecondsToTicks(regroup_seconds)`; stance `Massing` (new threshold drawn).
8. *HQ.* Uprooted and not moving: `RootHqCommand` if next to a field, else move to the nearest own drop-off. If `relocate_hq_when_main_depleted` and every main field is empty and an expansion drop-off exists: `UprootHqCommand`, then move there on the next think.

## 5. Commands and events

The AI adds no command or event types. It emits only commands other systems define (expected names, fields after `tick, issuer, sequence`):

| Command | Fields | Owner |
| --- | --- | --- |
| `GatherCommand` | `EntityId worker, int resourceFieldIndex` (index into the map's `resource_fields`, or the field's ID if economy makes fields entities) | economy |
| `TrainUnitCommand` | `EntityId producer, string unitDefinitionId` | economy |
| `BuildStructureCommand` | `EntityId worker, string buildingDefinitionId, int tileX, int tileY` | economy |
| `AttackMoveCommand` | `EntityId unit, FixVec2 goal` (exists, `Commands/CombatCommands.cs`; also `StopCommand`, `AttackUnitCommand`) | combat |
| `RootHqCommand`, `UprootHqCommand` | `EntityId hq` | HQ |
| `MoveUnitCommand` | exists | core |

Expected read APIs (names are proposals; adapt the `AiWorldView` builder, not the brain, if builders choose others): `sim.Economy.Of(PlayerId)` with `Common`, `Rare`, `SupplyUsed`, `SupplyCap`; resource fields with tile, kind, start and remaining amount; drop-offs; `UnitDefinition.Kind` (unit/building), worker flag, `Produces` list; `Unit.IsUnderConstruction`, `Unit.CurrentOrder` (none/move/attack-move/gather/build), production queue length; `Unit.HqState` (rooted, rooting, uprooted, uprooting); `PlacementRules.CanPlace(sim, player, definition, x, y)`. Rejected commands are fine: the AI re-reads state next think.

## 6. Interfaces

- `IPlayerController { PlayerId Player { get; } void OnMatchStart(Simulation sim); void Think(Simulation sim, ICommandSink sink); }` and `ICommandSink { int ScheduleTick { get; } void Submit(Command command); }` in `Rts.Ai`.
- Layers: `AiWorldView` (reads the simulation), `AiBrain` (pure: view + profile + memory + rng to an ordered intent list), `AiCommandWriter` (intents to commands, sequence numbers, the per-think cap). Tests drive `AiBrain` with hand-built views.
- `BalanceHarness` gains `--ai <profile>` per player in the script (`"controller": "ai:standard"`) and steps until a `MatchOutcome` or the tick limit.
- Must NOT: call `Step`, mutation methods or `sim.Rng`; read enemy units except through vision; use `float`, `double`, `System.Random`, the clock, threads, `Dictionary`/`HashSet` iteration; keep state in static fields (two AIs in one process must not share anything).

## 7. Acceptance tests (`rts/tests/Ai.Tests/`, new NUnit project referencing `Rts.Ai`)

1. **Seed.** Match seed 1234: player 0's AI seed is `0x9E3779B97F4A78C7` and its first four `NextInt(2)` are 0, 1, 1, 0; player 1's seed is `0x3C6EF372FE94FCF8` and its first `NextInt(7)` is 1.
2. **Hands off the simulation.** After `Think`, `sim.Rng.State`, `sim.Rng.DrawCount`, `sim.ComputeStateHash()` and the command queue size for other issuers are unchanged.
3. **Cadence.** `ThinkTicks` 10, player 1: thinks on ticks 9, 19, 29 and never on 10.
4. **Opening gather.** Idle workers 2, 3, 4, 5; field 0 at distance 5, field 1 at distance 9; `workers_per_field` 3: `GatherCommand` 2 to 0, 3 to 0, 4 to 0, 5 to 1, sequences 0 to 3.
5. **Supply.** Used 10 of cap 15, buffer 6: one `BuildStructureCommand(supply_building)` by the lowest-ID gathering worker; at 8 of 15: none; again 5 s later while the first is under construction: none.
6. **Composition.** Weights windrunner 3, roc 1 (W = 4). Counts (2, 0), N = 2: windrunner `3*3 - 2*4 = 1`, roc `1*3 - 0*4 = 3`, trains roc. Counts (2, 1), N = 3: windrunner `3*4 - 2*4 = 4`, roc `1*4 - 1*4 = 0`, trains windrunner.
7. **Attack threshold.** Jitter 0, threshold 24, Skai at tide 0.25: army supply 23 gives no attack-move; 24 gives one `AttackMoveCommand` per army unit, ascending ID, to the enemy spawn (56, 56) on `greybox_1v1`.
8. **Tide window.** Water profile (0.6 to 1, override 40), army 24: tide 0.5, no attack; tide 0.75, attack; tide 0.5 with army 40, attack.
9. **Retreat and regroup.** Attack started at 30 supply; army falls to 14: `MoveUnitCommand` to the HQ for every army unit; at 30 supply again 10 s later, no attack; after 20 s, attack (threshold re-drawn).
10. **Defend.** While massing, a visible enemy soldier 10 tiles from the HQ: attack-move to its position; at 20 tiles: nothing.
11. **Expansion tie.** Two mirror sites at equal distance: the chosen site index equals `[a, b][rng.NextInt(2)]` from an identically seeded generator, and two runs choose the same site.
12. **Harness determinism (exit test).** AI versus AI on `greybox_1v1`, seed 1234, until an outcome or 30 000 ticks: two runs print the same `STATE_HASH`; the match ends with one player Defeated.

## 8. Open questions and Phase 2+ follow-ups

- Difficulty levels (think interval, command cap, jitter) as more profiles; campaign mission scripts on the same controller.
- Faction tactics: Skai hit-and-run to keep Momentum, water flooding and wave use, Spoutlings versus flyers, HQ relocation under pressure.
- Scouting: Phase 1 relies on known spawns; add a scout rule with fog memory later.
- Lockstep online: the AI runs on the host only and its commands travel like a player's, or on every peer; both are deterministic. Network spec decides.

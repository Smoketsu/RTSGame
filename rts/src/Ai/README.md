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
| `AiBrain.cs` | The decision rules (pure: view + profile + memory + own generator to intents) |
| `AiIntent.cs` | Intents and `AiCommandWriter` (intents to commands with sequence numbers) |
| `SkirmishAiController.cs` | `IPlayerController` implementation tying it together |

## Rules (each think, in this order)

Intel, root an Uprooted HQ next to a field, idle workers to the nearest field under `workers_per_field`, supply structure and build
order (cost reserved when due but unaffordable), worker training (ignores reservations), production by largest composition deficit,
then the army: Defend (visible enemy army or a recent hit inside the base), Massing (rally point, attack at the drawn threshold within
the tide window), Attack (staging point short of the target, then attack-move; focus fire on visible enemy army units in reach; siege an
undefended visible HQ; retreat on losses), Retreat (move home, regroup timer). All in once the base is mined out. Details and every
number: the header of `AiBrain.cs` and the profile files.

## Determinism

The AI's memory is **not** simulation state and is never hashed. Its only random source is its own `DeterministicRandom` (the
simulation's PCG32 class) seeded `matchSeed ^ (0x9E3779B97F4A7C15 * (player + 1))`; it never draws from `sim.Rng`. Its commands are
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
- Profiles are loaded through the `ai/` route of `DataLoader` (`DataSet.AiProfiles`) and validated here; script player entries may
  name a profile with `"ai"`, and the harness has `--ai <slot>:<profile>`.
- Tests live in `rts/tests/Sim.Tests/Ai/` (the existing NUnit project references `Rts.Ai`) rather than a separate `Ai.Tests` project.

<!-- rts/src/Sim/README.md: package readme for com.rtsgame.sim, the engine-free deterministic RTS simulation (assembly Rts.Sim). -->
# RTS Simulation (`com.rtsgame.sim`, assembly `Rts.Sim`)

The whole game rules live here: units, commands, movement, factions, the match clock and the state hash. Unity (or a console tool, or a test) only **reads** the simulation, **submits commands** to it and **steps** it. Nothing in this folder knows what a Unity object, a frame or a renderer is.

The same folder is used three ways:

| Consumer | How it builds this folder |
| --- | --- |
| Unity 6 project `rts/src/Game` | Local package (`package.json`) with assembly definition `Rts.Sim.asmdef`; Unity compiles every `.cs` below this folder into `Rts.Sim.dll`. |
| Unit tests and headless tools | `Rts.Sim.csproj` (`netstandard2.1`, C# 9) compiled by the .NET SDK. Build output goes to `rts/build/`, never into this folder. |
| Documentation | This file, `rts/docs/decisions/*.md` and the header comment of every source file. |

## Why `noEngineReferences` is on

`Rts.Sim.asmdef` sets `"noEngineReferences": true`. Unity then compiles the assembly **without** `UnityEngine.dll` or `UnityEditor.dll`; only the .NET Standard 2.1 class library is visible. Consequences:

- A `using UnityEngine;`, a `Vector3`, a `Mathf` call or a `Debug.Log` anywhere in this folder is a **compile error**, so the rule "the simulation has no engine references" is enforced by the compiler, not by review.
- The identical source also compiles in the plain .NET projects (tests, balance harness, replay checker), which proves it does not secretly depend on Unity.
- Nothing here can read `Time.deltaTime`, Unity's `Random`, `float` physics or frame timing, which are the usual sources of desyncs in lockstep games.

`Rts.Sim.asmdef` also keeps `allowUnsafeCode` false and `autoReferenced` true (other assemblies in the project may reference `Rts.Sim` by name; the Unity side lists it explicitly in `Rts.Game.asmdef`). The asmdef is JSON, which cannot hold comments, which is why the explanation lives here.

`csc.rsp` (one line, `-nullable:enable`) tells the Unity compiler to treat the source as nullable-annotated, as `rts/Directory.Build.props` does for the .NET build. Unity reads a response file next to an asmdef only under the name `csc.rsp`. Without it Unity prints harmless CS8632 warnings for every `string?`.

Unity writes a `.meta` file next to every file and folder here when the project first opens. **Commit those `.meta` files**: they hold the GUIDs that references between assets use.

## Step order (copied from `Simulation.cs`; changing it changes every replay)

1. clear the event log
2. apply this tick's commands in (issuer, sequence) order
3. statuses (`StatusSystem.BeginTick`: Grounded and slow expiry, drench counter reset; units in ascending ID order)
4. HQ timers (`HqSystem`: Rooting and Uprooting countdowns, Last Stand countdown; HQs in ascending ID order)
5. movement
6. combat (`CombatSystem`: cooldowns, targeting, damage, deaths; units in ascending ID order)
7. economy (`EconomySystem`: worker gather loops, production queues, construction, supply; units in ascending ID order)
8. faction mechanics in ascending mechanic ID order
9. cycles and Core (`CycleSystem.Step`: a holder or capturer that left the match, who stands in the Core, the capture state machine, hold accrual, the cumulative Core win, the lock of the next Planetary Cycle, the shift and quake paths, quake damage). A cycle that shifts on tick S is read by movement and combat from tick S + 1.
10. vision recompute (`VisionSystem.Recompute`: per-team visible and explored grids; `Start` runs it once too, so tick 0 has vision)
11. state hash every StateHashIntervalTicks (reported as StateHashedEvent)
12. advance the clock

The host loop is: `Submit` commands for tick >= `Clock.Tick`, call `Step()`, read `Events.Current`, repeat. `Submit` is rejected inside a step and for ticks already played.

## Determinism rules (non-negotiable)

1. No engine references, no `float`/`double`/`decimal`, no `System.Numerics`, no `System.Random`, no `DateTime`/`Stopwatch`, no file, network or thread access. Numbers are `Fix64` (Q32.32, parse decimals with `Fix64.Parse`) or integers.
2. Everything that changes state is a `Command` applied inside `Simulation.Step`. Events go out, never in.
3. Data-driven: every tuning number lives in `rts/data`, never in code.
4. Faction mechanics are plug-ins behind `IFactionMechanic`, registered by ID; the core never names a faction (only `Factions/BuiltInMechanics.cs` does).
5. Any player count; nothing assumes 1v1.
6. Iterate collections in sorted-ID order (`SortedDictionary`, `SortedSet`, sorted lists), never `Dictionary`/`HashSet` order.
7. Everything that affects future ticks is written into the state hash (`StateHasher`) in a fixed order.
8. Fixed-point to float conversion (`Fix64.ToFloatForPresentation`) is for rendering only and must never flow back into the simulation.

## Folder map

| Folder | What lives there |
| --- | --- |
| `Core/` | `Fix64`, `FixVec2`, IDs (`EntityId`, `PlayerId`, `TeamId`), `MatchClock`, `DeterministicRandom`, `StateHasher`, `MatchSettings`, `SimulationConfig`, `ContentCatalog`. |
| `Commands/` | The `Command` base class, `CommandQueue` and the core commands (spawn, move, remove, no-op), the combat commands (attack-move, attack unit, stop), the HQ commands (`root_hq`, `uproot_hq`) and the economy commands (`gather`, `train_unit`, `build_structure`). The only way state changes. `Commands/Codec/` is the one binary encoding of commands (`CommandWriter`/`CommandReader`, `ICommandCodec`, `CommandCodecRegistry`, `CoreCommandCodecs`) shared by the lockstep protocol and the replay files; a faction's codec lives with the faction and is registered by `BuiltInMechanics.CreateCodecRegistry()`. |
| `Teams/` | `PlayerSetup` and `PlayerRegistry`: players, teams, alliances. |
| `Terrain/` | Placeholder (Phase 1): tile map, cliffs, walls, floods. |
| `Pathing/` | Placeholder (Phase 1): flow fields per movement layer. |
| `Vision/` | Fog of war (`sim.Vision`): per-team visible and explored grids with the high-ground rule, short airborne sight, query API and the combat hook. See `rts/docs/specs/fog_of_war.md`. |
| `Statuses/` | The shared status system: `UnitStatusSet` (Grounded, slows, drench counter), `StatusSystem` (step 3 and the apply methods), `StatusKind`, `SlowEntry`. See `rts/docs/specs/statuses.md`. |
| `Units/` | `Unit`, `UnitDefinition`, `WeaponDefinition`, `UnitRegistry`, `MovementLayer`, `MovementSystem`, and the mobile HQ: `HqDefinition`, `HqState`, `HqSystem` (root and uproot countdowns, Last Stand, defeat on HQ death), plus `UnitEconomyDefinition` (build time, worker, drop-off, trains, static defence, footprint, supply provided). A unit file may carry `armour_type`, `sight_range_tiles`, a `weapon` object, an `hq` object and the economy keys; see `rts/data/README.md`. |
| `Combat/` | `CombatSystem` (targeting, attack-move, cooldowns, damage, death, run after movement each tick), `CombatRules` (damage and armour table from `tuning/combat.json`), `IDamageModifier` (hook for mechanics such as Momentum). |
| `Economy/` | `EconomySystem` (worker gather loops, production queues, construction, footprints, supply), `EconomyRules` (supply cost, drop-off lookup, spawn tile), `ResourceFields` and `ResourceFieldState` (live field amounts from the map, hashed) and `GatherState`. Per-player stock and supply live on `Teams/Player`. See `Economy/README.md`. |
| `Factions/` | `IFactionMechanic`, `FactionMechanicRegistry`, `BuiltInMechanics`, and one subfolder per faction (`Skai/`, `Water/`, `Earth/`, `Fire/`). |
| `Cycles/` | Planetary Cycles and Core capture on the match clock (`sim.Cycles`): `CycleSystem` (schedule, lock and shift, quake paths and damage, the capture state machine, hold accrual, the Core win, state hash), `CycleRules` (pure schedule maths and `CaptureStep`) and the `cycle` speed and damage modifiers. See `Cycles/README.md` and `rts/docs/specs/planetary_cycles.md`. |
| `Events/` | `SimEvent`, `EventLog` and the core events hosts react to (spawned, removed, arrived, rejected, state hashed). |
| `Data/` | The data loader: `JsonReader`, `JsonValue`, `DataLoader`, `DataSet`, `CommandScript` (the shared command-script parser; scripts live in `rts/data/scripts`, which the loader skips), definitions for maps and cycles, and `Tuning/` (one typed class per `rts/data/tuning` file). Hosts do the file IO and pass `DataSourceFile(relativePath, text)` objects in. |

Related files in this folder: `Simulation.cs` (the one entry point), `Rts.Sim.csproj` (.NET build), `package.json` and `Rts.Sim.asmdef` (Unity). Setup for the Unity project is in `rts/docs/UNITY_SETUP.md`.

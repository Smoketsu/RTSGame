<!-- rts/docs/decisions/ADR-003-entity-ids.md: how entities, players, teams and data definitions are identified so replays, networking and data stay stable. -->
# ADR-003: Entity, player, team and definition IDs

Status: Accepted, 26 October 2026.

## Context

Every command names what it acts on, every event names what happened to whom, and every state hash must visit entities in one order on every machine. IDs therefore have to be small, ordered, stable within a match and never reused inside a match. Data definitions (unit types, factions, maps) are referenced from files and from commands, so they need stable text names that survive renames of C# classes.

## Decision

| Kind | Type | Rule |
| --- | --- | --- |
| Entity (unit, building, projectile, later anything simulated) | `EntityId`, a `readonly struct` over `uint` | Allocated by the simulation's `EntityIdAllocator`, starting at 1 and counting up by 1. `0` means "no entity". Never reused within a match, even after death. 4 billion IDs is enough for any match. |
| Player | `PlayerId`, a `readonly struct` over `byte` | Slot index in the match setup, 0 to 254, assigned by the lobby before tick 0 and fixed for the match. `255` means "no player" (neutral or the world). Any player count is allowed. |
| Team | `TeamId`, a `readonly struct` over `byte` | Same rules as `PlayerId`. A player belongs to exactly one team; a team can hold one player (1v1, FFA) or several (2v2). |
| Definition (unit type, building type, faction, map, cycle, mechanic) | `string` in `snake_case`, optionally namespaced with `.` | Comes from data files, for example `skai.windrunner`, `water.spoutling`, `faction.skai`, `cycle.windstorm`. Compared ordinally, never culture-aware. Renaming a definition ID is a data migration and bumps the file's `$schema_version`. |
| Command sequence | `uint` per player | Counts commands issued by one player from 0; combined with the tick and `PlayerId` it gives every command a total order (see `CommandQueue`). |

Ordering rules that follow from this:

- Collections of entities are `SortedDictionary<EntityId, ...>` or sorted lists, so iteration is by ascending ID. Hash-order iteration (`Dictionary`, `HashSet`) is banned in the simulation folder.
- Commands on the same tick are applied in `(PlayerId, sequence)` order. Different players' commands never interleave non-deterministically because the order is fixed by the IDs, not by arrival time.
- The state hash visits entities by ascending `EntityId` and players by ascending `PlayerId`.

## Consequences

- IDs are tiny and cheap to copy, hash and serialise, which matters for replays (Phase 2) and network messages.
- Because entity IDs are never reused, a stale command targeting a dead entity is simply ignored and logged as an event; there is no risk of hitting a newly spawned entity.
- Definition strings are looked up once at load time into definition objects; the hot path holds references, not strings.
- If a match ever needs more than 255 players or teams the structs widen to `ushort`; nothing else changes.

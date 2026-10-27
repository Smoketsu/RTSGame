<!-- rts/docs/decisions/README.md: index of architecture decision records (ADRs). Each big decision gets one short record saying what was chosen and why. -->
# Architecture decision records

One file per decision, numbered in the order they were made. A record is never edited after it is accepted; a later record supersedes it and says so.

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-001](ADR-001-fixed-point.md) | Fixed-point maths: our own Q32.32 `Fix64` | Accepted |
| [ADR-002](ADR-002-data-format.md) | Data files: JSON with comments, read by our own parser | Accepted |
| [ADR-003](ADR-003-entity-ids.md) | Entity, player and team IDs | Accepted |
| [ADR-004](ADR-004-tick-rate.md) | Simulation tick rate: 20 ticks per second | Accepted |
| [ADR-005](ADR-005-state-hash.md) | State hash: 64-bit FNV-1a over a canonical byte stream | Accepted |
| [ADR-006](ADR-006-terrain-model.md) | Terrain: tile grid of layered ground states | Accepted |
| [ADR-007](ADR-007-pathfinding.md) | Pathfinding: flow fields per movement layer | Accepted |

Template for new records: Context, Decision, Consequences, Alternatives considered.

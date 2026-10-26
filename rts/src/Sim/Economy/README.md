<!-- rts/src/Sim/Economy/README.md: what the economy folder holds (two resources, depleting fields, workers, drop-offs, production, construction, supply) and the rules it plays by. -->
# Economy (Phase 1)

Two resources (common and rare), gathered the same way by every faction from map fields that run out. Everything is deterministic: units are visited in ascending ID order, distances are compared squared in `Fix64`, and all numbers are data (`rts/data/tuning/economy.json`, `supply.json`, `hq.json` and the unit files).

- `EconomySystem.cs`: runs once per tick after combat. The header comment lists every rule: the worker loop (walk to the field, wait `gather_seconds_per_trip`, take `gather_amount_per_trip`, walk to the nearest drop-off, deposit, repeat until the field is empty), production queues (cost paid and supply reserved when queued, the unit appears next to the producer after `build_seconds`), structure construction (starts at `construction_start_health_fraction`, gains health evenly, no production, drop-off or supply until complete), single-tile footprints (a `Footprint` blocker on the structure's tile while it lives) and supply.
- `EconomyRules.cs`: supply cost of a unit type (a static defence costs `static_defence_supply_cost`), build time in ticks, drop-off lookup (a complete `is_drop_off` structure or a Rooted HQ, own units only) and spawn-tile search.
- `ResourceFields.cs`, `ResourceFieldState.cs`: the map's `resource_fields` as live state, remaining amount per field, hashed in ascending field index.
- `GatherState.cs`: Idle, ToField, Gathering, ToDropOff (stored on `Unit`, hashed).

Per-player stock (`Common`, `Rare`) and supply (`SupplyUsed`, `SupplyCap`) are on `Teams/Player` and are hashed. Supply used is recomputed by a scan of all units and queues (O(n)) at the end of every economy step and before each train or build command; the cap is `min(supply_cap, base_supply + provides_supply of complete structures)`.

Commands live in `Commands/EconomyCommands.cs` (`gather`, `train_unit`, `build_structure`; their rejection reasons are listed in each class), events in `Events/EconomyEvents.cs` (`resource_deposited`, `field_depleted`, `unit_trained`, `structure_completed`). The HQ rules (Rooted drop-off and worker training, Last Stand, defeat) are in `Units/HqSystem.cs`.

Placeholders and limits in Phase 1: the builder does not walk to the site; any number of workers may share a field; nothing is refunded when a producer dies; structures are one tile; an HQ that roots does not re-check that its field still has resources. Supply limits are data in `rts/data/tuning/supply.json`.

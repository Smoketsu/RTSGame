<!-- rts/src/Sim/Statuses/README.md: the shared status system, Grounded and Slowed, usable by every faction. -->
# Statuses

The shared status system (spec: `rts/docs/specs/statuses.md`). No status belongs to a faction.

- `UnitStatusSet`: one per unit (`Unit.Statuses`). Holds Grounded (until tick and source key), the slows (sorted by source key, the strongest wins) and the Spoutling drench counter. Hashed at the end of `Unit.WriteHash`.
- `StatusSystem`: `BeginTick` is step 3 of `Simulation.Step` (expiries, drench reset). `ApplyGrounded`, `ApplySlow` and `ApplyDrenchHit` start statuses; commands (`apply_status`), combat (`applies_drench` weapons) and future abilities call them. It is the only code that sets or clears `Unit.LayerOverride` and `Unit.StatusSpeedMultiplier`.
- Grounding ability (`grounding_ability` in a unit file, `GroundingAbilityDefinition`; `rts/docs/specs/combat_mechanics.md` package 4): `cast_grounding` (`CastGroundingCommand`, step 2) calls `StatusSystem.CastGrounding`, which grounds every enemy flyer within the radius of the aimed point for the shared `grounded_duration_seconds` through `ApplyGrounded` (source key `grounding`), sets `Unit.GroundingReadyTick` (hashed) and emits `GroundingCastEvent`. An instant pulse; the cooldown is its only cost. The AI casts it only with the profile switch `use_casts` on (`specs/ai_mechanic_use.md`, rule CG).
- `StatusKind`, `SlowEntry`, `StatusRates` (seconds converted to ticks once per use): small supporting types.
- Events (`Events/StatusEvents.cs`): `status_applied`, `status_expired`, `unit_layer_changed`, `drench_hit`.

Durations and values are data in `rts/data/tuning/statuses.json`. Momentum (`Factions/Skai`) reads `UnitStatusSet.IsGrounded` and `IsSlowed`.

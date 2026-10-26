<!-- rts/src/Sim/Combat/README.md: what the combat folder holds (system, rules, damage modifier hook) and the per-tick rules. -->
# Combat (Phase 1)

- `CombatSystem.cs`: runs once per tick after movement and before faction mechanics. Cooldowns, target acquisition, attack-move steering, damage and death. The header comment lists every rule.
- `CombatRules.cs`: the damage and armour table, acquire bonus and default armour type, built from `rts/data/tuning/combat.json` and stored on the `ContentCatalog`.
- `IDamageModifier.cs`: hook a faction mechanic registers (`Simulation.RegisterDamageModifier`, from `OnMatchStart`) to scale outgoing damage. Applied in ascending `Id` order. Momentum will use it.

Commands live in `Commands/CombatCommands.cs` (`attack_move`, `attack_unit`, `stop`), events in `Events/CombatEvents.cs` (`unit_damaged`, `unit_killed`). Unit weapons, armour types and sight range are data in `rts/data/units/`; the damage table is data in `rts/data/tuning/combat.json`. Not built yet: buildings as targets, projectiles, area damage, fog-of-war gating of targets.

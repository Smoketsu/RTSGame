<!-- rts/src/Sim/Combat/README.md: what the combat folder holds (system, rules, damage modifier hook) and the per-tick rules. -->
# Combat (Phase 1)

- `CombatSystem.cs`: runs once per tick after movement and before faction mechanics. Cooldowns, target acquisition, attack-move steering, damage and death. The header comment lists every rule.
- `CombatRules.cs`: the damage and armour table, acquire bonus and default armour type, built from `rts/data/tuning/combat.json` and stored on the `ContentCatalog`.
- `IDamageModifier.cs`: hook a faction mechanic registers (`Simulation.RegisterDamageModifier`, from `OnMatchStart`) to scale outgoing damage. Applied in ascending `Id` order. Momentum will use it.

Commands live in `Commands/CombatCommands.cs` (`attack_move`, `attack_unit`, `stop`), events in `Events/CombatEvents.cs` (`unit_damaged`, `unit_killed`). Unit weapons, armour types and sight range are data in `rts/data/units/`; the damage table is data in `rts/data/tuning/combat.json`. Structures are ordinary units, so they can be targeted and shot; an HQ fires only while Rooted, its armour type follows its state (`Unit.ArmourType`), and a unit with Last Stand active is invulnerable (`Unit.IsInvulnerable`, hits report damage 0). Not built yet: building-specific targeting priorities, area damage.

P-003 package A (`rts/docs/specs/p003_rules.md`): the step is 6a dome regen, 6b land due hits, 6c the unit loop, 6d deaths.
- `PendingHit.cs`, `PendingHitQueue.cs`: ranged hits fired with `hit_delay_ticks` above 0 wait here (hashed, ordered by landing tick then sequence) and land on their tick wherever the target is; a hit whose target is gone is dropped. Damage is fixed at fire time. `ShotFiredEvent` reports every shot.
- Uphill: a ranged hit on a target above the attacker's eye height (its tile height, plus 1 on a ramp) is multiplied by `uphill_ranged_damage_multiplier` (`tuning/combat.json`); melee, air attackers and air targets are exempt.
- Armed first: a new target pick prefers armed units (`CombatSystem.EffectiveWeapon` not null) over unarmed ones, then distance, then ID. A kept target is never swapped. Workers carry a weak melee weapon.
- Dome (`shield_dome` in a unit file, `ShieldDomeDefinition`): absorbs ranged shots fired from outside the dome at its owner's or allies' units inside it, at fire time, lowest dome unit ID first; `ShieldDomeHitEvent`, `ShieldDomeDownEvent`, `ShieldDomeUpEvent`.

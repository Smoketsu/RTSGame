<!-- rts/docs/specs/momentum.md -->
<!-- Implementation spec for the Skai Riders mechanic "momentum": integer stacks per unit gained while moving, drained after standing still, lost when Grounded or Slowed, feeding movement and combat through the existing ISpeedModifier and IDamageModifier hooks. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by sim-reviewer. -->
# Spec: Momentum (mechanic ID `momentum`)

## 1. Purpose and design-doc source

- Factions / Skai Riders / Signature mechanic: "Skai units gain one stack per second spent moving, up to 5 stacks. Each stack adds 4% speed and damage. Stacks start draining after 1.5 seconds standing still, and are lost entirely when the unit is Grounded or slowed."
- Intent: "Skai win by striking, pulling back and striking again; opponents answer by grounding or slowing them."

## 2. Data

Existing keys, all in `tuning/momentum.json`, none new:

| Key | Value | Use |
| --- | --- | --- |
| `stack_gain_per_second` | 1 | `GainTicksPerStack = max(1, SecondsToTicks(One / value))` = 20 at 20 tps |
| `max_stacks` | 5 | `MaxStacks` (int, `RoundToInt`; loader range 1 to 10) |
| `speed_bonus_per_stack` | 0.04 | speed multiplier `One + bonus * stacks` |
| `damage_bonus_per_stack` | 0.04 | damage multiplier `One + bonus * stacks` |
| `drain_delay_seconds` | 1.5 | `DrainDelayTicks = SecondsToTicks(value)` = 30 |
| `drain_stacks_per_second` | 1 (placeholder) | `DrainTicksPerStack = max(1, SecondsToTicks(One / value))` = 20 |
| `lost_on_grounded` | true | Grounded wipes stacks |
| `lost_on_slowed` | true | Slowed wipes stacks |

`One / value` is a Fix64 division (truncating) followed by `SecondsToTicks` (round to nearest), so 0.3 stacks/s gives 67 ticks. `max_stacks` must be an integer in data; the loader rejects a fraction. No unit-data fields: every unit owned by a player whose faction's `mechanic_id` is `momentum` and whose `SpeedTilesPerSecond > 0` is **eligible**. The mechanic compares against its own `Id`, never a faction name.

## 3. State

**Moving** is defined by the movement system, not by comparing positions: `Unit.LastSelfMoveTick` (new, int, ticks, -1 = never; hashed in `Unit.WriteHash` right after `StatusSpeedMultiplier` and before `CooldownTicksRemaining`) is set to the current tick by `MovementSystem` when it moves the unit toward its `MoveTarget` (set by a move order, by attack-move, or by `CombatSystem` chasing a target) and the new position differs from the old one. A unit **moved this tick** when `LastSelfMoveTick == tick`. Wave pushes, landing relocation, teleports and scripts never set it.

Per eligible unit, a `MomentumState` in `SortedDictionary<EntityId, MomentumState>` owned by `MomentumMechanic` (integer stacks plus integer tick accumulators, so no fractional stacks exist):

| Field | Type | Unit | Range |
| --- | --- | --- | --- |
| `Stacks` | int | stacks | 0 to `MaxStacks` |
| `GainTicks` | int | ticks moved toward the next stack | 0 to `GainTicksPerStack - 1` |
| `StillTicks` | int | consecutive still ticks, saturating | 0 to `DrainDelayTicks` |
| `DrainTicks` | int | ticks drained toward losing a stack | 0 to `DrainTicksPerStack - 1` |

`WriteHash` order: `Id`, entry count, then per entry ascending ID: ID, `Stacks`, `GainTicks`, `StillTicks`, `DrainTicks`. Tick constants are derived from tuning and not hashed (the content digest and tuning cover them).

## 4. Per-tick algorithm

`OnTick` runs in step 6 (faction mechanics, ascending ID; `flooding` runs before `momentum`), after statuses, movement and combat of the same tick.

1. **Sync entries.** Remove entries whose unit is no longer live (ascending key). Then for each live unit, ascending ID, that is eligible and has no entry: add one with all fields 0.
2. **Per entry**, ascending ID, `u` = the unit, `tick = sim.Clock.Tick`:
```
loss = (lost_on_grounded && u.Statuses.IsGrounded) || (lost_on_slowed && u.Statuses.IsSlowed)
if loss:
    if Stacks > 0: emit MomentumLostEvent(u, Stacks, reason)     // reason "grounded" wins over "slowed"
    Stacks = GainTicks = StillTicks = DrainTicks = 0
    continue                                                    // no gain while Grounded or Slowed
if u.LastSelfMoveTick == tick:                                  // moving
    StillTicks = 0; DrainTicks = 0
    if Stacks < MaxStacks:
        GainTicks += 1
        if GainTicks >= GainTicksPerStack: Stacks += 1; GainTicks = 0; emit MomentumChangedEvent
    else: GainTicks = 0
else:                                                            // still
    if StillTicks < DrainDelayTicks: StillTicks += 1; continue   // waiting; GainTicks is kept
    GainTicks = 0
    if Stacks == 0: DrainTicks = 0; continue
    DrainTicks += 1
    if DrainTicks >= DrainTicksPerStack: Stacks -= 1; DrainTicks = 0; emit MomentumChangedEvent
```
Timeline at the defaults: a unit that moves from tick m holds 1 stack at the end of tick m+19 and 5 at the end of m+99. A unit last moving on tick a waits ticks a+1 to a+30 (1.5 s), drains from a+31, and drops to 4 stacks at the end of tick a+50, then one stack every 20 ticks (0 at the end of a+130).

3. **Modifiers.** `MomentumMechanic : IFactionMechanic, ISpeedModifier, IDamageModifier` (both `Id` = `"momentum"`), registered in `OnMatchStart` with `sim.RegisterSpeedModifier(this)` and `sim.RegisterDamageModifier(this)`:
   - `SpeedMultiplier(sim, unit)` = `One + speed_bonus_per_stack * Stacks` (Fix64 times int), `One` without an entry. `MovementSystem.StepLength` applies it last (statuses.md 4).
   - `OutgoingDamageMultiplier(sim, attacker)` = `One + damage_bonus_per_stack * Stacks`, `One` without an entry.
   Stacks gained on tick t affect movement and combat from tick t+1 (mechanics run after both).

**Combat contract** (already built by the coordinator): `CombatSystem` computes `damage = weapon.Damage * rules.GetMultiplier(damageType, armourType)`, then multiplies by each registered `IDamageModifier` in ascending `Id`, each factor clamped at 0 or more. Stacks are read when the hit resolves.

## 5. Commands and events

No commands. Events (`rts/src/Sim/Factions/Skai/MomentumEvents.cs`):
- `MomentumChangedEvent(tick, EntityId unit, int previousStacks, int stacks)`, kind `"momentum_changed"`, emitted on every gain or drain step.
- `MomentumLostEvent(tick, EntityId unit, int stacksLost, string reason)`, kind `"momentum_lost"`, reason `"grounded"` or `"slowed"`; only when `stacksLost > 0`.

## 6. Interfaces

- Touches: `Factions/Skai/MomentumMechanic.cs` (exists as a shell); `Unit.LastSelfMoveTick` (targeted edit to `Unit.cs`; internal setter called only by `MovementSystem`, which the coordinator edits); the existing `ISpeedModifier` (`Units/`) and `IDamageModifier` (`Combat/`) hooks.
- Reads: `UnitStatusSet.IsGrounded`, `IsSlowed` (statuses.md); `sim.Players.TryGet(owner)` and `sim.Content.Factions[faction].MechanicId`.
- Must NOT: change unit speed or damage directly (only through the provider), compare positions to detect movement, read terrain speed (water slowing is not "slowed"), name a faction, use floats or `sim.Rng`, or keep entries for ineligible units.

## 7. Acceptance tests (`rts/tests/Sim.Tests/Momentum/`)

Test setup: flat open 64x64 map, one Skai player and one water player, a test ground definition with speed 2 tiles/s spawned at (2.5, 10.5) and ordered at tick 0 to (60.5, 10.5) unless stated.

1. **Conversion.** Defaults at 20 tps: `GainTicksPerStack == 20`, `DrainDelayTicks == 30`, `DrainTicksPerStack == 20`; with `stack_gain_per_second` 2 at 30 tps: 15; with `drain_delay_seconds` 1.5 at 30 tps: 45.
2. **Gain.** Stacks are 0 after 19 steps, 1 after 20, 4 after 80, 5 after 100 and still 5 after 200; exactly five `momentum_changed` events, all with increasing stacks.
3. **Speed bonus.** The X advance on tick 20 (first tick with 1 stack) equals `Clock.PerSecondToPerTick(FromInt(2) * (One + Fix64.Parse("0.04") * 1))` (about 0.104); at 5 stacks it equals the same with `* 5` (about 0.12).
4. **Drain.** Unit at 5 stacks arrives on tick a (`unit_arrived`): stacks are 5 at the end of tick a+49, 4 at a+50, 3 at a+70, 0 at a+130.
5. **Short stops keep progress.** Move 10 ticks, `stop` (StopCommand) for 5 ticks, move again: the first stack arrives on the 20th moving tick, not the 20th tick after the stop.
6. **Lost on Grounded.** An air test unit at 5 stacks is grounded by `apply_status` at tick t: after step t stacks are 0 with one `momentum_lost` (5, `"grounded"`); while still moving, stacks stay 0 through tick t+79; after the status expires at tick t+80, the unit holds 1 stack at the end of tick t+99.
7. **Lost on Slowed, switch respected.** Slowed 0.75 at 5 stacks: stacks 0, reason `"slowed"`. With `lost_on_slowed` false, stacks stay 5 and the unit keeps gaining logic as normal.
8. **Eligibility.** A water player's unit gets no entry; `SpeedMultiplier` and `OutgoingDamageMultiplier` for it are `One`. A Skai unit with speed 0 gets no entry.
9. **Pushes are not moving.** A Skai unit standing still and pushed by a wave (flooding.md) keeps `StillTicks` counting and gains nothing.
10. **Damage multiplier.** At 3 stacks `OutgoingDamageMultiplier == One + Fix64.Parse("0.04") * 3`; a hit from a weapon of damage 10 against a 1.0 armour multiplier removes exactly `FromInt(10) * (One + Fix64.Parse("0.04") * 3)` health (about 11.2).
11. **Determinism and hash.** Two identical runs give equal state hashes at every hash tick; a run where one unit stops 1 tick earlier differs at the next hash tick.

## 8. Open questions and Phase 2+ follow-ups

- Does water's terrain slow count as "slowed"? Phase 1: no, only the Slowed status. If playtests want it, add a switch `lost_on_terrain_slow`.
- Do workers and the HQ gain Momentum? Phase 1: every eligible mobile unit; an opt-out unit field can come with the Phase 1 unit docs.
- Talon Archer glide from cliffs and Stormcaller Downdraft are separate unit specs.
- UI: show stacks from `momentum_changed` events; nothing for the renderer to compute.

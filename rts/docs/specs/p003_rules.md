<!-- rts/docs/specs/p003_rules.md -->
<!-- Implementation spec for the five simulation rule changes the developer approved on 16 Nov 2026 (proposal P-003): uphill ranged penalty, armed workers with armed-first targeting, ranged hit delay, the Shellback's always-on dome shield (package A, combat), and unit separation (package B, movement). Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by sim-reviewer. -->
# Spec: P-003 rule changes (combat package A, movement package B)

## 0. Purpose, source and packages

Source: `rts/docs/proposals/P-003-decisions-to-apply.md` rows 1, 3, 4, 6 and 11, decided by the developer on 16 Nov 2026; dome details are defaults chosen by the coordinator (section 9). Built against `main` as of 16 Nov 2026.

| Package | Changes | Touches | Build |
| --- | --- | --- | --- |
| **A** combat | A1 uphill penalty, A2 armed workers, A3 hit delay, A4 dome shield | `Combat/`, `Units/WeaponDefinition.cs`, `Units/UnitDefinition.cs`, `Units/Unit.cs`, `Data/UnitSchema.cs`, `Data/DataLoader.cs`, `Data/Tuning/CombatTuning.cs`, `Statuses/StatusSystem.cs`, `Events/CombatEvents.cs`, `Simulation.cs`, data | first |
| **B** movement | B1 separation, sidestep, crowd arrival | `Units/MovementSystem.cs`, `Data/Tuning/TerrainTuning.cs`, `data/tuning/terrain.json` | after A is merged |

Each package ends with a `sim-reviewer` pass. No new commands in either package. Neither changes the step order of `Simulation.Step`; both change what happens inside one step (A: step 6, B: step 5).

Why the dome does not use `IDamageModifier`: the hook sees only the attacker (no target, no positions), must not change state, and returns a multiplier; the dome needs the target, drains a pool and absorbs instead of scaling. The dome is a generic unit-data block read by core combat (like `applies_drench`), so the core still never names a faction.

---

# Package A: combat

## A.1 Data

### `rts/data/tuning/combat.json`: one new key (after `acquire_range_bonus_tiles`)

```
  // Damage multiplier for a ranged hit on a target standing on higher ground than the attacker (target tile height above the attacker's eye height: its tile height, plus 1 on a ramp). Melee hits, air attackers and air targets are unaffected. Unit: fraction of damage. Range: 0.1 to 1. Source: P-003 row 1 (developer, 16 Nov 2026).
  "uphill_ranged_damage_multiplier": 0.75,
```

`CombatTuning`: add to `RejectUnknownKeys`, read with `RequireFix64("uphill_ranged_damage_multiplier", "0.1", "1")`, property `UphillRangedDamageMultiplier`, passed to `CombatRules`. Required key (missing = load error naming the file and field).

### Weapon object: new optional key `hit_delay_ticks`

Added to `UnitSchema.ReadWeapon` (`RejectUnknownKeys` list and parse). Integer, 0 to 10, default 0 when absent. A melee weapon (`range_tiles` 1.5 or less) with a value above 0 is a load error: `"must be 0 for a melee weapon (range_tiles 1.5 or less)"`. Comment line for unit files:

```
    // Ticks between firing and the hit landing; the hit is bound to its target when fired and cannot be dodged. 0 for melee. Unit: ticks (20 per second). Range: 0 to 10 (0 when absent; must be 0 when range_tiles is 1.5 or less). Source: P-003 row 4 (developer, 16 Nov 2026: 2 ticks = 100 ms); placeholder.
    "hit_delay_ticks": 2,
```

Set `"hit_delay_ticks": 2` in every shipped ranged weapon: `skai/great_aerie`, `skai/perch_tower`, `skai/roc_rider`, `skai/stormcaller`, `skai/talon_archer`, `skai/windrunner`, `water/coral_turret`, `water/grand_shell`, `water/ink_lobber`, `water/shellback`, `water/spoutling`. Set `"hit_delay_ticks": 0` explicitly in `water/grasper` (melee).

### Worker weapons: `skai/fledgling.json` and `water/tender.json`

Append to both files (same values; all **placeholder**):

```
  // Weapon: weak melee strike so workers can defend themselves. Every number is a placeholder for playtesting.
  "weapon": {
    // Damage per hit before the damage table. Unit: health points. Range: 0.01 to 100000. Source: P-003 row 3 (developer: workers get a weak melee weapon); placeholder.
    "damage": 3,
    // Damage type, a key of the damage table. Unit: damage type name. Range: "kinetic", "blast" or "drench" (names in tuning/combat.json). Source: P-003 row 3; placeholder.
    "damage_type": "kinetic",
    // Reach between unit edges (centre distance minus both radii); 1.5 or less counts as melee. Unit: tiles. Range: 0.1 to 64. Source: P-003 row 3 (melee); placeholder.
    "range_tiles": 0.3,
    // Time between two hits. Unit: seconds. Range: 0.05 to 60. Source: placeholder.
    "cooldown_seconds": 1.0,
    // Movement layers of targets the weapon may hit. Unit: list of layer names. Range: 1 to 3 distinct entries of "ground", "water", "air". Source: placeholder (melee reaches ground and water, as the Grasper).
    "can_target": ["ground", "water"],
    // Ticks between firing and the hit landing. Unit: ticks. Range: 0 for melee. Source: P-003 row 4.
    "hit_delay_ticks": 0
  }
```

### Unit file: new optional object `shield_dome` (only `water/shellback.json` has it)

`DataLoader.ReadUnit`: add `"shield_dome"` to `RejectUnknownKeys`. New `UnitSchema.ReadShieldDome(JsonValue root)` returns `ShieldDomeDefinition?` (null when absent); rejects unknown keys; all four keys required inside the object. Append to `shellback.json`:

```
  // Dome shield, always on: absorbs ranged hits fired from outside the dome at the Shellback or at its owner's and allies' units inside it. Melee hits and shots fired from inside the dome pass. Every number is a placeholder for playtesting.
  "shield_dome": {
    // Dome radius around the unit's centre; a unit is inside when its centre is within it. Unit: tiles. Range: 0.5 to 16. Source: P-003 row 6 (dome blocks ranged attacks in a radius); placeholder default chosen 16 Nov 2026.
    "radius_tiles": 3,
    // Damage the dome absorbs before it goes down. Unit: health points. Range: 1 to 100000. Source: P-003 row 6; placeholder default chosen 16 Nov 2026.
    "pool": 150,
    // Time without absorbing a hit before the pool starts to refill. Unit: seconds. Range: 0 to 60. Source: P-003 row 6; placeholder default chosen 16 Nov 2026.
    "regen_delay_seconds": 4,
    // Refill rate while refilling; a dome that went down comes back up only when its pool is full again. Unit: health points per second. Range: 0.01 to 10000. Source: P-003 row 6; placeholder.
    "regen_per_second": 25
  }
```

`Simulation.Start` (next to `StatusRates.Validate`): for every unit definition with a dome, in ordinal ID order, `Clock.PerSecondToPerTick(RegenPerSecond)` must be above 0, else throw `InvalidOperationException` naming the unit ID and `shield_dome.regen_per_second`.

## A.2 Types

| Type / member | Change | Unit, range |
| --- | --- | --- |
| `CombatRules.UphillRangedDamageMultiplier` | new `Fix64` property; new 4-argument constructor `(multipliers, acquireRangeBonusTiles, defaultArmourType, uphillRangedDamageMultiplier)`; the existing 3-argument constructor stays and passes `Fix64.One`; `Neutral` uses `One`. Throws when outside 0.1 to 1 (`One` allowed). `WriteHash` appends it after `DefaultArmourType`. | fraction, 0.1 to 1 |
| `WeaponDefinition.HitDelayTicks` | new `int`; constructor gains last parameter `int hitDelayTicks = 0`; throws `ArgumentOutOfRangeException` when below 0, above `MaxHitDelayTicks`, or above 0 while `IsMelee`. `WriteHash` appends it last. | ticks, 0 to 10 |
| `WeaponDefinition.MaxHitDelayTicks` | new `public const int` = 10 (schema bound, like `MeleeMaxRangeTiles`) | ticks |
| `ShieldDomeDefinition` (new, `Rts.Sim.Units`, immutable) | `RadiusTiles` (Fix64, 0.5 to 16), `Pool` (Fix64, 1 to 100000), `RegenDelaySeconds` (Fix64, 0 to 60), `RegenPerSecond` (Fix64, 0.01 to 10000); constructor validates; `WriteHash` in that order | as listed |
| `UnitDefinition.ShieldDome` | new `ShieldDomeDefinition?`, null when absent. The loader's longest constructor gains last parameter `ShieldDomeDefinition? shieldDome = null`. `WriteHash` (content digest) appends `bool present`, then the dome's `WriteHash` when present. | |
| `Unit.ShieldPool` | `Fix64`, starts at `Definition.ShieldDome.Pool` (0 without a dome) | health points, 0 to Pool |
| `Unit.ShieldDown` | `bool`, starts false; true while the pool is empty and not yet full again | |
| `Unit.ShieldLastAbsorbTick` | `int`, starts -1 | ticks, -1 = never absorbed |
| `Unit.SetShield(Fix64 pool, bool down, int lastAbsorbTick)` | internal setter, clamps pool to 0..Pool | |
| `PendingHit` (new, `Rts.Sim.Combat`, `readonly struct`) | `LandTick` int; `Sequence` long; `Attacker` EntityId; `AttackerOwner` PlayerId (at fire time); `Target` EntityId; `Damage` Fix64 (final, after table, uphill, modifiers and dome); `AppliesDrench` bool | LandTick: fire tick + 1 to fire tick + 10; Sequence 0 up; Damage 0 or more |
| `PendingHitQueue` (new, `Rts.Sim.Combat`, `public sealed class`, every member except `Count` is `internal`) | `SortedDictionary<HitKey, PendingHit>` where `HitKey` is a private `readonly struct (int LandTick, long Sequence) : IComparable<HitKey>` comparing LandTick then Sequence; `long NextSequence` (starts 0, +1 per enqueued hit, never reused); `Enqueue(...)`; `TakeDue(int tick, List<PendingHit> into)` removes and appends, in key order, every entry with `LandTick <= tick`, throwing `InvalidOperationException` if any has `LandTick < tick` (invariant breach); `int Count`; `WriteHash`. No public enumerator, list or view. | |
| `Simulation.PendingHits` | new `internal PendingHitQueue` field created in the constructor; public `int PendingHitCount => PendingHits.Count` for hosts and tests | |
| `CombatSystem.PendingKill` | gains `KillerOwner` (PlayerId, from the hit); `UnitKilledEvent` takes the killer owner from it instead of looking up the killer | |
| `StatusSystem.ApplyDrenchHit` | signature becomes `(Simulation sim, EntityId attacker, PlayerId attackerOwner, Unit target)` because the attacker may be gone when a delayed hit lands; body uses the two values where it used `attacker.Id` / `attacker.Owner`. Update `StatusTestWorld.cs` line 203. | |
| `CombatSystem.EffectiveWeapon(Unit u)` | new internal static helper: `u.Definition.Weapon`, or null while `u.IsUnderConstruction` or (`u.Definition.Hq != null && u.HqState != Rooted`). Replaces the inline check in `StepUnit`. A unit is **armed** when this is not null. | |

## A.3 Step 6 (combat), new internal order

`CombatSystem.Step` becomes (the `Simulation.cs` header line for step 6 is rewritten to this list):

- **6a Dome regen.** Units in ascending ID with a `ShieldDome`: let `delay = Clock.SecondsToTicks(RegenDelaySeconds)`, `rate = Clock.PerSecondToPerTick(RegenPerSecond)`. If `ShieldPool < Pool` and (`ShieldLastAbsorbTick < 0` or `tick - ShieldLastAbsorbTick >= delay`): `ShieldPool = Min(Pool, ShieldPool + rate)`. Then if `ShieldDown` and `ShieldPool == Pool`: `ShieldDown = false`, emit `ShieldDomeUpEvent`. While visiting, collect the IDs of dome units that are **active** (`Health > 0`, `!ShieldDown`, `!IsUnderConstruction`, `Owner.IsValid`) into this tick's dome list (ascending ID). A dome that goes down later this tick is re-checked at use (`ShieldDown`).
- **6b Land due hits.** `PendingHits.TakeDue(tick, list)`; land each in list order (A.4.5).
- **6c Unit loop** (existing `StepUnit` per unit in ascending ID: cooldown, target, fire, steering). Units hit to 0 health in 6b still act here (all units act within the tick, as today); they cannot be targeted (`Health > 0` check).
- **6d Deaths** (existing: sort by victim then killer, `UnitKilledEvent`, `RemoveUnit("killed")`).

After 6d no queued hit has `LandTick <= tick`.

## A.4 Rules

### A.4.1 Uphill (decision 1)

`IsUphill(sim, attacker, target)` is true when all hold: `!weapon.IsMelee`; `attacker.EffectiveLayer != Air`; `target.EffectiveLayer != Air`; `targetTile.Height > attackerTile.Height + (attackerTile.IsRamp ? 1 : 0)` (tiles from `Terrain.TileOf(position)`; same eye rule as `VisionSystem.Stamp`).

### A.4.2 Damage at fire time

`damage = weapon.Damage * rules.GetMultiplier(weapon.DamageType, target.ArmourType)`; then if uphill `damage = damage * rules.UphillRangedDamageMultiplier`; then each `IDamageModifier` in ascending Id as today. Left to right, Fix64 (truncation order is fixed by this sentence). Invulnerability is checked at land time (A.4.5).

### A.4.3 Acquisition (decision 2)

In `ResolveTarget`:
- **New pick**: among valid, visible candidates in acquire reach, take the minimum of the key (armed ? 0 : 1, squared centre distance, EntityId). With ascending-ID iteration: replace `best` when the candidate's rank is lower, or the rank is equal and its distance is strictly smaller.
- **Kept target**: unchanged from today. A target that is still valid, in reach and visible is kept whether it is armed or not; units never switch targets on their own. Armed-first applies only when a new target is picked. (Developer decision, 16 Nov 2026.)

### A.4.4 Fire (decision 3 and 4)

`Fire(sim, rules, attacker, weapon, target, kills)` in this order:
1. Compute `damage` (A.4.2).
2. `landTick = tick + weapon.HitDelayTicks`; emit `ShotFiredEvent(tick, attacker.Id, target.Id, landTick, weapon.IsMelee)`.
3. Set the cooldown (unchanged: `Max(1, SecondsToTicks(cooldown))`).
4. **Dome** (ranged only, `damage > 0`, `target.Owner.IsValid`): `remaining = damage`. For each ID in this tick's dome list, ascending, while `remaining > 0`: let `d` be that unit; skip unless it is still registered, `d.Health > 0`, `!d.ShieldDown`, `Players.AreAllied(d.Owner, target.Owner)` (allied includes the same player), `(target.Position - d.Position).LengthSquared() <= r*r` and `(attacker.Position - d.Position).LengthSquared() > r*r` (`r = d.Definition.ShieldDome.RadiusTiles`). Then `absorbed = Min(remaining, d.ShieldPool)`; `remaining -= absorbed`; `d.SetShield(d.ShieldPool - absorbed, false, tick)`; emit `ShieldDomeHitEvent(tick, d.Id, attacker.Id, target.Id, absorbed, d.ShieldPool)`; if `d.ShieldPool == 0`: set `ShieldDown = true`, emit `ShieldDomeDownEvent(tick, d.Id)`. Overflow of the breaking hit carries on to the next dome, then to the target. If any dome absorbed and `remaining == 0`, stop: no hit, no damage event, no drench.
5. Build the hit `(attacker.Id, attacker.Owner, target.Id, remaining, weapon.AppliesDrench)`. `HitDelayTicks == 0`: land it now (A.4.5). Otherwise `PendingHits.Enqueue` with `LandTick = landTick`, `Sequence = NextSequence++`.

### A.4.5 Land

`Land(sim, hit, kills)`: if the target is not registered or `target.Health <= 0`, drop the hit (no event, no retarget). Otherwise, as today: invulnerable target takes 0 (reported with damage 0), else `SetHealth(Health - Damage)` and `HqSystem.AfterDamage`; emit `UnitDamagedEvent(tick, hit.Attacker, target.Id, damage, target.Health)` (the attacker ID is reported even if that unit is gone); if `AppliesDrench`, `StatusSystem.ApplyDrenchHit(sim, hit.Attacker, hit.AttackerOwner, target)`; if `target.Health <= 0`, add `PendingKill(target.Id, hit.Attacker, hit.AttackerOwner)`.

Consequences to keep: the hit lands wherever the target is (out of range, out of sight, after the attacker died); a delayed hit's damage never changes after fire; melee (delay 0) behaves exactly as before this spec.

## A.5 State hash

- `Simulation.ComputeStateHash`: write `PendingHits.WriteHash` right after `Units.WriteHash`: `Count`, `NextSequence`, then per entry in key order `LandTick, Sequence, Attacker, AttackerOwner, Target, Damage, AppliesDrench`. Update the method's doc comment and the hash-order paragraph in `rts/docs/specs/README.md`.
- `Unit.WriteHash`: write `ShieldPool, ShieldDown, ShieldLastAbsorbTick` after `ConstructionTicksRemaining`, before `Statuses.WriteHash` (statuses stay last).
- Content digest: `CombatRules` (uphill key), `WeaponDefinition` (`HitDelayTicks`), `UnitDefinition` (dome) and the changed tuning and unit texts. The dome list of 6a is per-tick scratch, not state.

## A.6 Events (`Events/CombatEvents.cs`, each with `Describe()`)

| Event | Fields | When |
| --- | --- | --- |
| `ShotFiredEvent` | `Attacker`, `Target` (EntityId), `LandTick` (int), `IsMelee` (bool) | every shot, at fire, before dome events |
| `ShieldDomeHitEvent` | `Dome`, `Attacker`, `Target` (EntityId), `Absorbed` (Fix64, above 0), `PoolLeft` (Fix64, 0 up) | a dome absorbs part or all of a hit |
| `ShieldDomeDownEvent` | `Dome` | pool reached 0 |
| `ShieldDomeUpEvent` | `Dome` | a down dome's pool is full again (6a) |

Order within one shot: `ShotFired`, then per dome `ShieldDomeHit` (+ `ShieldDomeDown`), then for a zero-delay hit `UnitDamaged`, then `DrenchHit` if any. Unity reads `Unit.ShieldPool`/`ShieldDown` for the initial dome state.

## A.7 Acceptance tests (`rts/tests/Sim.Tests/Combat/P003CombatTests.cs`)

Build test content by adding units to a fresh `TestWorld.Catalog()` before the simulation is created; do not change `TestWorld.Catalog()` itself. Add `TestWorld.TerracedMap()`: 64 x 64, height 0 for x < 32, height 1 for x >= 32, tile (31, 20) a ramp. Test units (all placeholders in test code): `test.lobber` (ranged, kinetic 10, range 5, cooldown 1, delay 2, ground+water), `test.melee` (kinetic 10, range 1, delay 0), `test.dome` (ground, unarmed, `shield_dome` radius 3, pool 150, delay 4 s, regen 25/s), existing `test.shooter` (delay 0), `test.tank`, `test.runner` (unarmed).

1. Loader: shipped `combat.json` gives `UphillRangedDamageMultiplier == 0.75`; removing the key fails naming `uphill_ranged_damage_multiplier`; value 1.5 fails.
2. Uphill: shooter at height 0, tank at height 1, both in range: hit damage is exactly `(10 * table) * 0.75`, where `table` is kinetic against the tank's armour in `TestWorld.Rules()`. Shooter at height 1, tank at 0: `table` only. Same height: `table`.
3. Ramp: shooter on the ramp tile (31, 20) (height 0), tank at height 1: no penalty.
4. No penalty for: a melee attacker uphill; an air attacker; an air target.
5. Order: with the existing `FixedModifier` factor 2 registered, an uphill hit deals `((10 * table) * 0.75) * 2`.
6. Shipped workers: `skai.fledgling` and `water.tender` have a weapon with `IsMelee`, `HitDelayTicks 0`, damage 3, kinetic.
7. Armed first: shooter with an unarmed runner 2 tiles away and an armed tank 4 tiles away (both in reach) targets the tank. Two armed at equal distance: lowest ID (existing rule still holds).
8. No switching: shooter auto-targeting a runner; a tank spawns in reach; the next tick `AttackTarget` is still the runner. After the runner dies, the next pick is the tank.
9. Unarmed by state: an Uprooted HQ and a structure under construction rank as unarmed (a shooter prefers an armed tank further away).
10. A worker beside an enemy tank with no orders hits it for 3 x table every 20 ticks.
11. Loader `hit_delay_ticks`: absent gives 0; 11 fails; 2 on a melee weapon fails with the melee message; every shipped ranged weapon has 2 and every melee weapon 0.
12. Delay: a `test.lobber` firing on tick t emits `ShotFiredEvent(LandTick t+2)` on t; no `UnitDamagedEvent` for it on t or t+1; damage and the event on t+2. Repeat with delays 1 and 10.
13. Zero delay: `ShotFiredEvent` and `UnitDamagedEvent` of one shot are on the same tick in that order; `PendingHitCount == 0` after the step.
14. Fire-time damage: a test `IDamageModifier` whose factor field the test changes between steps: fire with factor 1 on t, set 2 before t+1, the hit landing on t+2 deals the factor-1 damage.
15. Undodgeable: after the lobber fires, the target is moved out of range and out of sight (move command); it still takes the hit on the land tick.
16. Target gone: the lobber's target is killed by a melee unit before the land tick: no `UnitDamagedEvent` from the lobber, no exception, `PendingHitCount == 0` after the land tick.
17. Attacker gone: the lobber is removed (remove command) after firing; the hit still lands; if lethal, `UnitKilledEvent.Killer` is the lobber's ID and `KillerOwner` its owner.
18. Land order: lobbers a < b by ID; b (delay 2) fires on t, a (delay 1) fires on t+1; both land on t+2, b's `UnitDamagedEvent` first (lower sequence).
19. A unit at 0 health after a 6b landing is not targeted by another unit in 6c on the same tick and is removed in 6d; it still fires its own ready shot that tick.
20. Hash (`HashCoverageTests`): a queued hit changes the hash; changing its `Damage`, `Target` or `LandTick` (internal test hook) changes it; `NextSequence` changes it; each of `ShieldPool`, `ShieldDown`, `ShieldLastAbsorbTick` changes it alone. Two identical runs give equal hashes on a hash tick that falls between a fire and its land tick.
21. Loader `shield_dome`: shipped Shellback has 3 / 150 / 4 / 25; an unknown key inside fails; `pool` 0 fails; `regen_per_second` so small that it converts to 0 per tick fails at `Start` naming the key.
22. Absorb: dome D (P0) at (20, 20), ally runner T at (22, 20), enemy lobber at (27, 20) given `attack_unit` on T: T takes no damage; `ShieldDomeHitEvent(absorbed 10, pool left 140)`; no `UnitDamagedEvent` for T; `PendingHitCount == 0` (absorbed hits are never queued).
23. Melee passes: an enemy melee unit next to T damages T; D's pool unchanged.
24. Inside shooter passes: an enemy lobber at (21, 21) (inside the dome) damages T; pool unchanged.
25. Fire out and no enemy cover: an ally lobber inside D hits an enemy outside normally; an enemy unit standing inside D is hit normally by P0's lobber outside D.
26. Down and overflow: set D's pool to 15 (`SetShield`); hit 1 absorbed fully (pool 5); hit 2 absorbs 5, T takes 5, `ShieldDomeDownEvent`; hit 3 passes in full.
27. Regen: after the last absorb on tick a, the pool is unchanged through tick a+79 and rises by 1.25 per tick from tick a+80; a down dome emits `ShieldDomeUpEvent` on the tick the pool reaches 150, not before, and absorbs again from the next shot.
28. Overlap: two allied domes covering T; the lower ID absorbs first; with its pool at 4, a 10-damage hit takes 4 from it and 6 from the other; T is untouched.
29. Determinism: `combat_smoke.json` with shipped data twice gives identical hash lists; existing `Battle_IsDeterministic` still passes.

## A.8 Existing code, tests and hashes that change

- Every state hash changes (tuning text, unit texts, new hashed state). Re-pin `DeterminismTests.GoldenFinalHash` with a doc-comment line naming this spec. The hashes recorded in `rts/docs/STATUS.md` (smoke `24ffa53a8542d783`, economy_smoke `dac9a5db27e3fdb4`) go stale: replace them with the new harness output and flag that Unity parity must be re-checked by the PC session.
- Existing `CombatTests`, `StructureAttackTests` and status tests should pass unchanged (code-built weapons default to delay 0, flat maps, no domes); only `StatusTestWorld.cs` needs the new `ApplyDrenchHit` signature. A failing existing assertion is a spec issue: report it, do not rewrite it.
- Doc updates in the same change: `CombatSystem.cs` header (hit delay, uphill, armed-first, dome, 6a to 6d), `Simulation.cs` header step 6, `Combat/README.md`, `specs/README.md` hash order.
- Outside `rts/src/Sim` (not in this package, tell the coordinator): `rts/src/Ai/AiBrain.cs` line 1128 counts every armed foe as power, so workers now count; Unity `SelectionController` lines 631 and 672 treat workers as armed (attack-move now applies to them). Unity can draw tracers from `ShotFiredEvent` and the dome from the dome events.

---

# Package B: movement (unit separation)

## B.1 Data: `rts/data/tuning/terrain.json`, new keys (after `arrival_radius_tiles`), all **placeholder**

```
  // Fraction of the overlap between two touching units removed per tick (split evenly when both can be pushed). 0 turns separation off. Unit: fraction per tick. Range: 0 to 1. Source: P-003 row 11 (developer, 16 Nov 2026: units push apart instead of stacking); placeholder.
  "separation_strength": 0.5,
  // Sideways push a moving unit gets when it walks into another unit, so head-on meetings pass instead of stalling ("keep right"). 0 turns it off. Unit: fraction of the overlap per tick. Range: 0 to 2. Source: P-003 row 11; placeholder.
  "sidestep_strength": 0.5,
  // Largest total push one unit receives in one tick. Unit: tiles per tick. Range: 0.01 to 0.5. Source: P-003 row 11; placeholder.
  "separation_max_push_tiles_per_tick": 0.1,
  // A moving unit within this distance of its goal that touches a stopped unit already nearer that goal ends its move (crowds settle instead of jostling). 0 turns it off. Unit: tiles. Range: 0 to 16. Source: P-003 row 11; placeholder.
  "crowd_arrival_radius_tiles": 3,
  // Side of the square cells used to find touching units; affects speed only, never results. Unit: tiles. Range: 1 to 16 (integer). Source: placeholder.
  "separation_cell_tiles": 2,
  // Whether air units push each other apart (they never push ground or water units). Unit: boolean. Range: true or false. Source: P-003 row 11; placeholder default chosen 16 Nov 2026.
  "air_separation_enabled": true,
```

`TerrainTuning`: one property per key (`Fix64` except `SeparationCellTiles` int and `AirSeparationEnabled` bool), ranges as above, required keys.

## B.2 Rules

- **Participant**: every registered unit except workers on a gather loop (`GatherState != Idle`) and, when `air_separation_enabled` is false, units whose `EffectiveLayer` is Air.
- **Layer group**: `EffectiveLayer`. Only participants in the same group interact (ground with ground, water with water, air with air; a Grounded flyer is ground). Owner does not matter: enemies and neutrals body-block too.
- **Pushable**: `Definition.SpeedTilesPerSecond > 0 && Definition.Hq == null && !IsUnderConstruction`. Non-pushable participants (buildings, HQs, speed-0 units) push others but never move.
- **Radius**: the existing `Definition.Radius` (`radius_tiles`).
- No new unit state; no new events (crowd arrival reuses `UnitArrivedEvent`). A push never calls `MarkSelfMoved` (Momentum unchanged).

## B.3 Algorithm (inside step 5, `MovementSystem.Step`)

- **5a** (start of `Step`, before the existing loop): record `start[i]` = position of every unit, ascending ID (scratch array).
- **5b** existing per-unit movement loop, unchanged.
- **5c Separation**, if `separation_strength > 0` or `sidestep_strength > 0` or `crowd_arrival_radius_tiles > 0`:
  1. Snapshot `P[i]` (post-5b positions) of participants, ascending ID. Per group, a uniform grid of `C = separation_cell_tiles`: cell `(floor(x / C), floor(y / C))`, each cell a list of participant indexes filled in ascending ID; `maxR[group]` = largest radius in the group.
  2. For each participant A ascending, scan cells covering `P_A ± (r_A + maxR)` in ascending (cellY, cellX); keep neighbours B with `B.Id > A.Id`, sorted ascending. Skip the pair when neither is pushable.
  3. Pair `(A, B)`: `d = P_A - P_B`, `m = r_A + r_B`. Skip if `|d.X| >= m` or `|d.Y| >= m`, or `s = d.LengthSquared() >= m*m` (strict overlap only). `len = Fix64.Sqrt(s)`. `n = len == 0 ? (-1, 0) : (d.X / len, d.Y / len)` (coincident: the lower ID A goes west, B east). `overlap = m - len`; `c = overlap * separation_strength`.
     - Both pushable: `push_A += n * (c / 2)`, `push_B -= n * (c / 2)`. Only A: `push_A += n * c`. Only B: `push_B -= n * c`.
     - Sidestep, for each pushable side X of the pair with `step_X = P_X - start_X` not zero, where `toward` is `-n` for A and `+n` for B: if `step_X.X * toward.X + step_X.Y * toward.Y > 0`, then `u = step_X * (1 / |step_X|)` (component-wise division by `step_X.Length()`), `push_X += (u.Y, -u.X) * (overlap * sidestep_strength / 2)`. That perpendicular is the unit's right-hand side, so two units meeting head-on both step right and pass.
     - Crowd flag: if A has a move target and B has none, and `crowd_arrival_radius_tiles > 0`, with `g = Terrain.ClampToMap(A.MoveTarget)`: when `(g - P_A).LengthSquared() <= R*R` and `(g - P_B).LengthSquared() < (g - P_A).LengthSquared()`, mark A settled. Symmetrically for B against A.
     - Every contribution depends only on the snapshot and sums are exact Fix64 additions, so results do not depend on visiting order or cell size.
  4. Apply, pushable participants ascending ID: clamp `push` to length `separation_max_push_tiles_per_tick` (if `L2 = push.LengthSquared()` exceeds `max*max`, `push = push * (max / Sqrt(L2))`). `p = Terrain.ClampToMap(P + push)`. Air: accept `p`. Ground or water: `a = TileOf(P)`, `b = TileOf(p)`; accept `p` if `a == b` or `Terrain.IsStepPassable(a, b, layer)`; else try `(p.X, P.Y)`, then `(P.X, p.Y)` with the same test; else stay. `SetPosition` only when the position changes. (The 0.5 cap keeps `b` within one tile of `a`.)
  5. Settled units, ascending ID: `ClearMoveTarget()` and emit `UnitArrivedEvent(tick, id, position)`. An attack-move whose move target is cleared this way ends on the next combat step by the existing `SteerAttackMove` rule.
- **Cost**: grid build O(n); pair work O(n x k), k = units within reach (a dense blob gives about 8 to 12). Scratch arrays and cell lists are reused between ticks (owned by a static cache keyed by map size, or rebuilt per tick if the builder prefers; neither is state).

## B.4 State hash

Nothing new: positions and move targets are already hashed. The new tuning keys enter the content digest through the tuning text.

## B.5 Acceptance tests (`rts/tests/Sim.Tests/Units/SeparationTests.cs`)

Test units added to a fresh `TestWorld.Catalog()`: `test.post` (ground, speed 0, no footprint, radius 0.5, unarmed). Use `test.runner` for pushable units.

1. Loader: all six keys load with the shipped values; each missing key fails naming it; `separation_max_push_tiles_per_tick` 0.6 fails.
2. Coincident: two runners spawned on the same point end, after 40 ticks, at least `0.9 * (rA + rB)` apart, the lower ID west, equal Y.
3. Cell size has no effect: scenario 2 plus a 12-unit blob, run with `separation_cell_tiles` 1 and 8: identical positions on every tick.
4. Head-on: two runners on one row at exact tile centres, 10 tiles apart, ordered to swap places: both emit `UnitArrivedEvent` within 2 x (10 / speed) seconds of ticks.
5. Through a post: a runner ordered through a `test.post` standing exactly on its line reaches a goal 4 tiles beyond it and arrives.
6. Pin: with `sidestep_strength` 0, scenario 5's runner has not arrived after the same tick budget.
7. Not pushed: a runner spawned overlapping a `test.post` and one overlapping a Rooted `test.hq`: the post and HQ positions never change; the runner ends at least `0.9 * (rA + rB)` from each.
8. Terrain: a runner pushed by another toward a cliff edge (`TerracedMap`, x = 31 / 32, away from the ramp) and toward a blocked footprint tile never ends a tick on a tile it could not step to (`IsStepPassable` from its previous tile, checked every tick for 40 ticks).
9. Layers: a ground and an air unit on the same point do not move; two air units separate; with `air_separation_enabled` false they do not; a ground and a water unit on the same point do not move; a Grounded flyer separates from a ground runner.
10. Workers gathering: two workers with gather orders on the same field may overlap (distance below `rA + rB` occurs); economy totals over 600 ticks equal a run with `separation_strength` 0.
11. Momentum: an idle runner pushed by a neighbour keeps `LastSelfMoveTick == -1`.
12. Crowd: 6 runners ordered to one point all have no move target within 200 ticks, each with a `UnitArrivedEvent`. With `crowd_arrival_radius_tiles` 0, at least one still has a move target at tick 200.
13. No early stop: a runner ordered 20 tiles past an idle runner standing on its path 10 tiles out ends within the arrival radius of its own goal.
14. Edge: units overlapping at the map edge stay inside the map (`ClampToMap`).
15. Scale: 400 runners on a 20 x 20 lattice at 0.5-tile spacing (overlapping) step 100 ticks without exception; all end in the map on standable tiles; two runs give identical hashes. Report the harness's average and worst step time for this setup in the build report (not asserted).

## B.6 Existing tests and hashes that change

- `GoldenFinalHash` re-pinned again (new tuning text, and its script's units may now separate); `STATUS.md` smoke and economy_smoke hashes replaced again; Unity parity re-check flagged.
- Existing tests that spawn overlapping non-gathering units and assert exact positions may move. Fix by spacing the fixture's spawn points apart (keep every assertion's intent); list each changed test in the report.
- Doc updates: `MovementSystem.cs` header, `Simulation.cs` header step 5 (", then separation"), `Pathing/README.md` if it describes movement, `specs/README.md` step-order paragraph.

---

## 8. Out of scope

- An ability system, toggles or commands for the dome (it is always on).
- Unit radius against cliffs: units collide with terrain as points (as today).
- Formation movement, flocking, or path re-planning around crowds.
- Spatial grid for combat acquisition (still O(n^2); P-003 row 14 is measured separately).
- Fixing the read-only list casts of P-003 row 13, except that `PendingHitQueue` must not repeat them.
- AI and Unity changes listed in A.8.

## 9. Questions for the developer

Each has a recommended answer the builder uses until the developer replies.

1. **Dome radius.** Recommended: 3 tiles (coordinator default).
2. **Dome pool and recharge.** Recommended: pool 150, refill starts 4 s after the last absorbed hit at 25 per second, and a dome that went down comes back up only when full (about 10 s after breaking).
3. **The breaking hit.** Recommended: overflow passes through (a 36-damage shot into a 5-point pool deals 31), rather than the last hit being absorbed whole.
4. **Overlapping domes.** Recommended: they chain, lowest unit ID first.
5. **Who the dome protects.** Recommended: its owner's and allies' units only; enemies standing inside are not covered.
6. **When the dome decides.** Recommended: at fire time, like damage, so absorbed shots never enter the hit queue.
7. **Uphill edge cases.** Recommended: flyers neither suffer nor cause the penalty; a ramp lifts the attacker's eye by one level (the vision rule), so a shooter on a ramp is not penalised against the level above.
8. **Uphill order.** Recommended: applied after the armour table and before faction damage modifiers.
9. **Switching to armed targets.** Decided 16 Nov 2026: no. A unit keeps its current target; armed-first only applies to a new pick.
10. **Worker weapon.** Recommended: 3 kinetic damage, 0.3 tiles, 1 hit per second, hits ground and water.
11. **Hit delay unit.** Recommended: ticks as decided (2 ticks = 100 ms at 20 per second), maximum 10.
12. **Air separation.** Recommended: on, among air units only.
13. **Gathering workers.** Recommended: ignore collision while on a gather loop, so fields and drop-offs do not jam.
14. **Crowd arrival.** Recommended: a moving unit within 3 tiles of its goal stops when it touches a stopped unit nearer the goal.
15. **Ground and water units.** Recommended: they do not push each other (different layers).
16. **Enemies.** Recommended: enemy units push each other too (body blocking).

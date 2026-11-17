<!-- rts/docs/specs/combat_mechanics.md: build spec for the four combat rules the developer decided on 18 Nov 2026 (P-005 decisions 3, 6 and 7): area damage, a per-weapon structure multiplier, anti-air that works only while the attacker stands on water, and the Stormcaller's Downdraft grounding ability. Four packages, each buildable and reviewable alone. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by the sim-reviewer after every package. -->
# Spec: combat mechanics (area damage, structure multiplier, anti-air on water, Downdraft)

Written 18 Nov 2026 by the designer. Sources: `proposals/P-005-roster-holes-and-ai-for-balance.md` (developer decisions 3, 6, 7, 18 Nov), `specs/missing_roster.md` section 4 (S-1, S-2 sketches), `specs/statuses.md`, `specs/flooding.md`, `specs/momentum.md`, `specs/p003_rules.md`, `rts/src/Sim/Combat/CombatSystem.cs`, `rts/src/Sim/Statuses/StatusSystem.cs`.

## 0. Ground rules for all four packages

- **Mechanics first, numbers last** (developer, 18 Nov: "Let's get all the relevant mechanics worked out before we start tuning numbers"). Every value in this spec is a **placeholder** worked out by the rule stated next to it. No existing number (damage, health, cost, range, the damage table) changes. In data comments write `Source: placeholder, developer to confirm (rts/docs/specs/combat_mechanics.md <section>).`
- **No new step.** Packages 1 to 3 run inside step 6 (combat). Package 4 runs inside step 2 (commands), the same way `cast_wave` does. The `Simulation.cs` header and `CombatSystem.cs` header gain one line each per package that describes the new rule; the step numbers do not change.
- **Coming units.** Thunderhead, Tidecaller and Leviathan are being added on `review/missing-roster-build` (R-2). Do not depend on their files. Whichever branch merges into `main` **second** adds this spec's keys to `skai/thunderhead.json` and `water/leviathan.json` and re-pins the hashes.
- **Test setup** (all packages unless a test says otherwise): a 24 x 24 flat open map, no cycles, two players on different teams, both factions with mechanic `none` (so no Momentum and no flood update changes water), hand-built definitions, and terrain tuning with separation off (`separation_strength` 0, `sidestep_strength` 0, `crowd_arrival_radius_tiles` 0) so units stay where they are spawned. Shots are started with a forced `attack_unit` at tick 0. Unit radius 0.5, max health 100, and no weapon (targets and victims do not shoot back) unless stated. Damage table: the shipped one (blast 0.75 / 1.0 / 1.25 against light / armoured / structure; kinetic 1.0 / 0.75 / 0.5). Every number in the tests below is an exact binary fraction, so Fix64 results are exact.
- **Structure target.** A unit counts as a structure when `Definition.Economy.FootprintTiles > 0` (the rule `AiWorldView.IsStructure` already uses) **or** it is an HQ in state Rooted. New helper: `internal static bool CombatSystem.IsStructureTarget(Unit u)`. It is used by packages 1 and 2.

---

## Package 1: area damage

### 1.1 Rule

A weapon with `splash_radius_tiles` above 0 deals a **blast** when its hit lands:

1. The primary target takes its hit exactly as today (damage fixed at fire time, dome absorption at fire time, drench, Last Stand, kill credit).
2. **Blast centre:** the primary target's position on the landing tick if it is still registered (even at 0 health), otherwise its position at fire time (`BlastFallback`). So a shell whose target died in flight still explodes where it was aimed.
3. **Victims:** every unit `v` other than the primary target, in ascending ID order, with `v.Health > 0`, `v.Owner` valid and `AreEnemies(attackerOwner, v.Owner)` (enemies only, no friendly fire: Q1), on a layer in the hit's `SplashLayers` (by `v.EffectiveLayer`), and with centre distance to the blast centre at most `splash_radius_tiles + v.Definition.Radius` (compared squared, after a per-axis check as in `CombatSystem.InReach`). Victims are collected first, then damaged.
4. **Layers:** a blast reaches exactly the layers its weapon can target, whatever the primary's layer. A ground blast from a weapon with `can_target` ground and water does not touch flyers overhead; a Leviathan blast (it targets air) does. A Thunderhead (air) hitting a ground unit damages ground and water units around it. Grounded flyers are on the ground layer and are hit by ground blasts.
5. **Flat falloff** (Q2): every victim takes the same base, `splashBase`, before its own armour.
6. **Armour:** each victim uses its own armour row: `d = splashBase * rules.GetMultiplier(damageType, v.ArmourType)`; then (package 2) `if IsStructureTarget(v): d = d * weapon.StructureDamageMultiplier`. Splash damage gets **no uphill multiplier** (the attacker may be gone or have moved; Q4).
7. **Domes:** for each victim, in ascending dome ID over this tick's active dome list: a dome allied to the victim, with the victim's centre inside its radius and the **blast centre outside** its radius, absorbs `min(remaining, pool)` exactly as at fire time (same pool update, `ShieldDomeHitEvent`, `ShieldDomeDownEvent`). A blast inside a dome is not absorbed for the units inside. Refactor the dome loop of `Fire` into one helper `AbsorbByDomes(sim, domes, attackerId, FixVec2 from, Unit victim, Fix64 damage) -> Fix64 remaining`, called with `from = attacker.Position` for the primary and `from = blast centre` for splash.
8. **A primary shot fully absorbed by a dome makes no blast** (the dome caught the shell; `Fire` returns before queuing, as today; Q5). A partly absorbed shot blasts with the full `splashBase`.
9. **Statuses:** drench applies to the primary only. Splash victims get no drench hit.
10. **Invulnerable** victims (Last Stand) take 0 and are reported with damage 0. `HqSystem.AfterDamage` runs after every splash damage, as in `Land`.
11. **Kill credit:** a splash victim at 0 health adds `PendingKill(victim, hit.Attacker, hit.AttackerOwner)`. Step 6d's sort (victim, then killer) is unchanged.

### 1.2 Arithmetic (fixed order, Fix64, left to right)

At fire time, only when `weapon.SplashRadiusTiles > 0`:
```
splashBase = weapon.Damage * rules.SplashDamageFraction
for each IDamageModifier m in ascending Id: splashBase = splashBase * Fix64.Max(0, m.OutgoingDamageMultiplier(sim, attacker))
```
The primary chain is unchanged: `weapon.Damage * table` (then package 2's structure factor), `* uphill`, `* modifiers`.

### 1.3 Where it runs

Inside `CombatSystem.Land`, which runs in 6b (queued hits, (land tick, sequence) order) and in 6c (hits with `hit_delay_ticks` 0). Order inside one landing: primary damage, `UnitDamagedEvent`, drench, primary pending kill (all as today); then, if the hit carries a blast: collect victims, emit `BlastEvent`, then per victim in ascending ID: domes, damage, `UnitDamagedEvent`, `AfterDamage`, pending kill. `Land` gains the `domes` list as a parameter. Splash never removes or moves a unit; removal stays in 6d.

### 1.4 Data

Unit file, `weapon` object (`UnitSchema.ReadWeapon`, add to `RejectUnknownKeys`; `WeaponDefinition.SplashRadiusTiles`, constructor argument with default 0):
```
// Radius of the blast around the point the hit lands; every enemy unit on a layer this weapon can target whose edge is within it takes splash damage (combat.json splash_damage_fraction of the damage, then its own armour). 0 means no blast. Unit: tiles. Range: 0 to 4 (0 when absent). Source: placeholder, developer to confirm (rts/docs/specs/combat_mechanics.md 1.4).
"splash_radius_tiles": 1.0,
```
`tuning/combat.json` (`CombatTuning`, `CombatRules.SplashDamageFraction`; `CombatRules` constructors take it as an optional last argument defaulting to 1; `CombatRules.Neutral` uses 1; `CombatRules.WriteHash` writes it after `UphillRangedDamageMultiplier`):
```
// Fraction of a weapon's damage that each splash victim takes before its own armour multiplier; the same at any distance inside the radius (flat). Unit: fraction of weapon damage. Range: 0.05 to 1. Source: placeholder, developer to confirm (rts/docs/specs/combat_mechanics.md 1.4).
"splash_damage_fraction": 0.5,
```
Placeholder rule for the fraction: a blast on a target with two touching enemy neighbours deals about twice the single-target damage (1 + 2 x 0.5).

**Placeholder radii.** Rule: **1 tile times the tier factor `t` of `missing_roster.md` D1, rounded down to a multiple of 0.25.** One tile is chosen because it reaches the ring of units touching the target (unit radii are 0.4 to 0.6, so neighbours' centres sit 0.8 to 1.2 tiles apart).

| Unit | File | `t` | `splash_radius_tiles` |
| --- | --- | --- | --- |
| Stormcaller | `skai/stormcaller.json` | 1 | 1.0 |
| Brinecaster | `water/ink_lobber.json` | 1 | 1.0 |
| Thunderhead | `skai/thunderhead.json` (when on `main`) | 4/3 | 1.25 |
| Leviathan | `water/leviathan.json` (when on `main`) | 2 | 2.0 |

Update the Stormcaller `$doc`: delete "The area splash ... are design notes only: Phase 1 has no splash radius or Downdraft field." and say it has a blast (package 4 adds Downdraft to the same sentence). Brinecaster `$doc`: add "blast splash".

### 1.5 State and hash

`PendingHit` gains four fields, set only for a splash weapon (defaults otherwise), written by `PendingHitQueue.WriteHash` after `AppliesDrench` in this order:

| Field | Type | Unit | Range |
| --- | --- | --- | --- |
| `SourceDefinitionId` | string | definition ID | `""` when the hit has no blast; else the attacker's definition ID, used at landing to read `DamageType`, `SplashRadiusTiles` (and package 2's multiplier) from `sim.Content.Units` |
| `SplashDamage` | Fix64 | health points | `splashBase` of 1.2; 0 without a blast |
| `SplashLayers` | byte | bit mask | bit 0 ground, bit 1 water, bit 2 air; 0 without a blast. Set at fire time from the weapon (package 3 adds the air bit conditionally) |
| `BlastFallback` | FixVec2 | tiles | primary target position at fire time; zero vector without a blast |

`Enqueue`, the in-place `PendingHit` built for 0-delay hits, and the `Rewrite` test hook carry the new fields. `WeaponDefinition.WriteHash` appends `SplashRadiusTiles` after `HitDelayTicks` (content digest). Nothing else is stored: victims, the centre and domes touched are per-landing scratch. `HashCoverageTests` gets the four fields (as built, the coverage lives in `SplashTests.Hash_CoversTheFourBlastFields`; `HashCoverageTests` does not repeat it).

### 1.6 Events

`BlastEvent(tick, EntityId attacker, PlayerId attackerOwner, FixVec2 centre, Fix64 radiusTiles, int victims)`, kind `"blast"`, in `Events/CombatEvents.cs`, once per blast (also with 0 victims), before the victims' `UnitDamagedEvent`s. For the renderer and the harness; nothing reads it inside the simulation.

### 1.7 Acceptance tests (`rts/tests/Sim.Tests/Combat/SplashTests.cs`)

Weapon `B`: damage 12, blast, range 7, cooldown 3, `hit_delay_ticks` 0, `can_target` ground and water, `splash_radius_tiles` 1. Attacker A (player 1) at (2.5, 10.5). Primary T (player 2, light) at (8.5, 10.5). `splash_damage_fraction` 0.5, so `splashBase` = 12 x 0.5 = 6.

- **A1 Basic blast.** Also spawn (player 2 unless stated): V1 light (9.5, 10.5) at distance 1.0; V2 light (10.0, 10.5) at 1.5, exactly on the edge (1 + 0.5), so it is hit; V3 light (10.25, 10.5) at 1.75, not hit; V4 armoured (8.5, 11.5); V5 light owned by player 1 at (8.5, 9.5); V6 air unit (8.5, 12.0); V7 structure (footprint 1, armour `structure`) at (7.5, 10.5). After step 0: T 100 - 12 x 0.75 = **91**; V1 and V2 100 - 6 x 0.75 = **95.5**; V3 **100**; V4 100 - 6 x 1.0 = **94**; V5 **100** (ally); V6 **100** (layer not targeted); V7 100 - 6 x 1.25 = **92.5**. Events in order: `ShotFiredEvent`, `UnitDamagedEvent(T)`, `BlastEvent(victims 4)`, then `UnitDamagedEvent` for V1, V2, V4, V7 in ascending ID.
- **A2 No splash weapon unchanged.** The same scene with `splash_radius_tiles` absent: only T is damaged, no `BlastEvent`, and the pending-hit hash fields are `""`, 0, 0, (0, 0).
- **A3 Delayed hit, target gone.** `hit_delay_ticks` 2, V1 as in A1. A `remove_unit` (issuer 255) removes T on tick 1. On tick 2 the blast lands at (8.5, 10.5): V1 is 95.5, and the event shows `centre` (8.5, 10.5).
- **A4 Delayed hit follows the target.** `hit_delay_ticks` 2; T is moved by a test hook to (8.5, 14.5) on tick 1 (or ordered to move with enough speed; the builder picks one and states it). The blast centres on T's tick-2 position; V1 at (9.5, 10.5) is not hit; a unit at (9.5, 14.5) is.
- **A5 Air blast reaches air.** Weapon `B` with `can_target` ground, water and air. V6 of A1 now takes 6 x 0.75: **95.5**.
- **A6 Air attacker, ground blast.** Attacker is an air unit with weapon `B` (ground and water): splash hits V1, not V6.
- **A7 Kill credit.** V1 has max health 4. After step 0: `UnitKilledEvent(V1, killer A)`, then `UnitRemovedEvent`. With a second attacker A2 (higher ID) firing the same shot on the same tick, the credit goes to A (lower killer ID).
- **A8 Dome.** Player 2 dome unit D (dome radius 3, pool 50, regen tuned so it does not refill during the test) at (8.5, 14.0). Victim V8 (light, player 2) at (8.5, 11.5): inside the splash radius (distance 1.0 from T) and inside the dome (2.5 from D). T is outside the dome (3.5 from D). V8 stays at 100; D's pool is 50 - 4.5 = **45.5**; one `ShieldDomeHitEvent(D, A, V8, 4.5, 45.5)`. Move T inside the dome to (7.0, 12.5) (2.12 from D; D itself is outside the blast) with V8 at (6.0, 12.5) (1.0 from T, 2.92 from D): the primary shot fired from outside is absorbed at fire time (pool 50 - 9 = 41), there is no blast and V8 stays at 100. Give D pool 4 instead: the primary is partly absorbed (T takes 9 - 4 = 5), the blast happens inside the dome, and V8 takes 4.5 (no absorption, the centre is inside).
- **A9 Drench is primary only.** Weapon `B` plus `applies_drench`, `can_target` air, primary an enemy flyer, a second enemy flyer 1 tile away: the primary's `DrenchHits` is 1, the second flyer's is 0 and it gets no `drench_hit` event.
- **A10 Last Stand.** An invulnerable enemy HQ inside the blast takes 0; its `UnitDamagedEvent` reports 0.
- **A11 Momentum multiplies the splash base.** A test damage modifier returning 1.5 for A: T takes 12 x 0.75 x 1.5 = 13.5; V1 takes (6 x 1.5) x 0.75 = **6.75**.
- **A12 Loader.** `splash_radius_tiles` -0.5 and 4.5 are rejected naming the file and `weapon.splash_radius_tiles`; `splash_damage_fraction` 0 and 1.5 are rejected naming `tuning/combat.json`. Shipped Stormcaller and Brinecaster load with 1.0.
- **A13 Determinism.** Spawn the A1 victims in reverse order (so IDs differ): the damage per position is the same and damage events follow ascending ID. Two identical runs give equal hashes at every hash tick; a run with V1 at (9.75, 10.5) differs from A1 at the first hash tick after the shot.

### 1.8 Determinism notes for the sim-reviewer

The victim scan walks `sim.Units.All` (ascending ID) once per blast: O(n) per blast, bounded by the unit count, no spatial structure, no `Dictionary` iteration (the existing acquisition is already O(n^2)). Distances are squared Fix64 with a per-axis pre-check; positions are bounded by `WorldBoundTiles`, so no overflow. No square root. Domes in ascending ID. Victims are collected before any is damaged. The fire-time and landing-time multiplication orders are fixed in 1.2 and 1.1 step 6. `SourceDefinitionId` reads a frozen catalogue. No RNG.

---

## Package 2: per-weapon structure multiplier

### 2.1 Rule

`weapon.structure_damage_multiplier` (default 1) multiplies damage against a structure target (section 0), **after** the damage-table multiplier and before uphill and the damage modifiers:
```
damage = weapon.Damage * table
if IsStructureTarget(target): damage = damage * weapon.StructureDamageMultiplier
damage = damage * uphill (if uphill); then modifiers
```
Splash victims: 1.1 step 6. The target's state at fire time decides for the primary (an HQ that Uproots while a shell flies still takes the fire-time damage); the victim's state at landing decides for splash.

### 2.2 Data

Weapon object (`WeaponDefinition.StructureDamageMultiplier`, appended to `WriteHash` after `SplashRadiusTiles`):
```
// Extra multiplier on this weapon's damage against structures (buildings with a footprint, and Rooted HQs), applied after the damage table. 1 means no bonus. Unit: multiplier of damage. Range: 0.1 to 4 (1 when absent). Source: placeholder, developer to confirm (rts/docs/specs/combat_mechanics.md 2.2).
"structure_damage_multiplier": 1.5,
```
**Placeholder rule:** a unit whose design text calls it a siege unit gets 1.5; every other weapon leaves the key out (1). Only the **Leviathan** qualifies ("capstone siege unit"); add it to `water/leviathan.json` when that file is on `main`. Nothing else changes, so this package changes no damage in today's shipped data. The tool exists for the HQ and siege tuning that comes later (P-005 decision 4: Rooted HQs last at least 15 s).

### 2.3 State, events, step

No new state, no new event. Step 6, inside `Fire` and the splash part of `Land`.

### 2.4 Acceptance tests (`rts/tests/Sim.Tests/Combat/StructureMultiplierTests.cs`)

Weapon: damage 12, blast, `hit_delay_ticks` 0, `structure_damage_multiplier` 1.5.
- **B1** Against a structure (footprint 1, armour `structure`): 12 x 1.25 x 1.5 = **22.5**.
- **B2** Against a Rooted HQ whose `rooted_armour_type` is `structure`: **22.5**. The same HQ Uprooted (armour `armoured`): 12 x 1.0 = **12** (no bonus).
- **B3** Against a light unit: 12 x 0.75 = **9**.
- **B4 Uphill.** Structure on height 1, attacker on height 0, not a ramp: 12 x 1.25 x 1.5 x 0.75 = **16.875**.
- **B5 Splash** (with package 1, radius 1): primary a light unit, a structure 1 tile away: the structure takes (12 x 0.5) x 1.25 x 1.5 = **11.25**.
- **B6 Default.** Key absent: B1 gives 15. Every shipped weapon loads with 1.
- **B7 Loader.** 0.05 and 4.5 are rejected naming `weapon.structure_damage_multiplier`.

---

## Package 3: anti-air on water

### 3.1 Rule

A weapon with `can_target_air_on_water: true` may also target units whose **effective layer is Air**, but only while the attacker is **on water**:

- **On water** means the tile under the attacker's centre (`sim.Terrain.TileOf(attacker.Position)`) has `TerrainTile.IsFlooded` (water depth above 0, that is `DepthClass >= 1`), read when the check runs. Any depth counts; any owner counts: own team, an enemy's flood, or natural water (no shipped map has natural water today, so in practice it is flood and wave water). The tide matters only through the depth it leaves on the tile. Q8.
- **The target's tile does not matter.** A flyer over dry land can be shot from a flooded tile.
- One helper, used everywhere the target layer is checked inside the simulation:
  `internal static bool CombatSystem.CanHitLayer(Simulation sim, Unit attacker, WeaponDefinition weapon, MovementLayer layer)` = `weapon.CanTarget(layer) || (layer == Air && weapon.CanTargetAirOnWater && IsOnWater(sim, attacker))`.
  Callers: `IsValidTarget` (acquisition and keeping a target, forced or not), `AttackUnitCommand.Apply` (`cannot_target_layer` unless the rule holds when the command applies), and package 1's `SplashLayers` at fire time (the air bit is set when the weapon targets air, or when this rule holds at fire time).
- **The tile dries mid-attack:** on the next 6c `IsValidTarget` fails, the target is dropped (a forced target too) and no new shot is fired. Hits already fired still land (P-003: a fired hit is bound to its target), and their blast keeps the air bit decided at fire time. There is **no wind-up** in this simulation (a shot fires the tick the cooldown is 0 and the target is valid), so there is no "mid-wind-up" case.
- **Public query for hosts and tools (added in the build, as micro_benchmark.md section 7 asks):** `public static bool CombatSystem.CanAttack(Simulation sim, Unit attacker, Unit target)` = the attacker has a weapon and `CanHitLayer(sim, attacker, weapon, target.EffectiveLayer)`. Layer only; the `--fights` harness uses it for its `cannot_target` stalemate test and idle re-targeting.
- **`WeaponDefinition.CanTarget(layer)` keeps its static meaning** (it does not see the flag). This keeps the 12 call sites in `rts/src/Ai` and `rts/tools/BalanceHarness` unchanged.
- A Grounded flyer is on the ground layer, so this rule does not apply to it. Ordinary ground targeting does.

### 3.2 Data

Weapon object (`WeaponDefinition.CanTargetAirOnWater`, appended to `WriteHash` after `StructureDamageMultiplier`; the loader rejects `true` when `can_target` already lists `"air"`: "is redundant: can_target already lists air"):
```
// Lets this weapon also hit flying units, but only while this unit stands on a flooded tile (any water depth above 0, any team's water). Unit: boolean. Range: true or false (false when absent; must be absent or false when can_target lists "air"). Source: P-005 developer decision 3 (18 Nov 2026: "Some water units can hit fliers, but only when they're on Water"); choice of units is a placeholder, developer to confirm (rts/docs/specs/combat_mechanics.md 3.2).
"can_target_air_on_water": true,
```
**Recommended units** (the developer said "some"; Q7): **Reef Runner** (`water/reef_runner.json`; it mirrors the Windrunner, which hits air, and is the cheap early unit that meets an early flying raid) and **Shellback** (`water/shellback.json`; the defender that sits on its own flood near the base). Not the Pummeler (melee identity, P-005), not the Brinecaster (artillery with splash), not the Tidecaller (unarmed). Spoutling, Coral Turret, Grand Shell and Leviathan already hit air everywhere.

### 3.3 State, events, step

No new state (water depth is already hashed per tile). No new event. Step 6c and step 2 (the command).

### 3.4 AI and harness (minimum change: none)

`FightPredictor`, `AiCounter` and `ResponseChecks` call `CanTarget(Air)`, which stays false for these weapons. The AI therefore sees Reef Runners and Shellbacks as unable to hit air: pessimistic but safe, and no AI code changes. The AI never issues an `attack_unit` these units cannot carry out, because it checks `CanTarget` first. A later AI package may model the rule (for example, credit air damage when the fight is on own flood).

### 3.5 Acceptance tests (`rts/tests/Sim.Tests/Combat/WaterAntiAirTests.cs`)

Water-layer attacker W (player 1) at (5.5, 5.5): kinetic damage 6, range 4, cooldown 1, `hit_delay_ticks` 0, `can_target` ground and water, `can_target_air_on_water` true. Enemy flyer F (light, `airborne_sight_range_tiles` set) at (8.5, 5.5). Water is set through `TerrainGrid.SetWater` from a test step hook, as `FloodingTests` does.
- **C1 Dry: no shot.** 20 steps: no `ShotFiredEvent` from W, F at 100.
- **C2 Wet: shoots.** Tile (5, 5) gets 64 drops of team 0 on tick 0 before combat: on tick 0 W fires and F is at **94**.
- **C3 Enemy and ownerless water count.** The same with team 1's water, and with `TeamId.None`: F at 94.
- **C4 Drying drops the target.** Cooldown 1 s = 20 ticks. The tile is wet on ticks 0 to 19 and dried on tick 20 before combat. W fires once, on tick 0. After step 20 `W.AttackTarget` is None, and F ends at 94.
- **C5 In-flight hit lands.** `hit_delay_ticks` 2, wet on tick 0, dried on tick 1: the tick-0 shot lands on tick 2 (F at 94), no further shot.
- **C6 Command.** `attack_unit(W, F)` while dry is rejected `cannot_target_layer`; while wet it is accepted. Dried later: the forced target is dropped.
- **C7 Grounded flyer.** W dry, F grounded by `apply_status`: W fires at it (ground target).
- **C8 Splash air bit is decided at fire time** (with package 1): W with `splash_radius_tiles` 1 and `hit_delay_ticks` 2, no F; a forced `attack_unit` on a ground enemy at (8.5, 5.5) and an enemy flyer F2 at (9.5, 5.5). Wet on tick 0, dried on tick 1: the blast on tick 2 hits F2 (6 x 0.5 x 1.0 = **3**, F2 at 97). Dry on tick 0: F2 is not hit.
- **C9 Loader.** `can_target` `["air", "ground"]` with the flag true is rejected. Shipped Reef Runner and Shellback load with the flag; every other weapon without it.
- **C10 AI unchanged.** `FightPredictor.Dps(reef_runner, roc_rider, ...)` is 0, as before.

---

## Package 4: Downdraft (grounding ability)

### 4.1 What exists and what is reused

`StatusSystem.ApplyGrounded(sim, target, durationTicks, sourceKey, sourcePlayer)` already grounds a flyer: it returns false for non-air definitions, refreshes (never extends) an active Grounded, moves a flyer off a blocked tile (landing), resets the drench counter and emits `StatusAppliedEvent` and `UnitLayerChangedEvent`. `statuses.md` section 8 already says Downdraft calls `ApplyGrounded` with its own key. Momentum already wipes stacks on Grounded. Everything a Grounded flyer can and cannot do is already built (statuses.md section 1 and 6). This package adds only the ability definition, a command, its cooldown and one event.

It lives in the **core**, not in a faction mechanic: grounding is a shared status, the ability names no faction, and the Earth Gravity Spire can reuse it. The core never says "Downdraft"; the player-facing name is the unit's business. Names: data object `grounding_ability`, command `cast_grounding`, status source key `"grounding"` (a constant `StatusTuning.GroundingSourceKey`, like `DrenchSourceKey`).

### 4.2 Rule

`CastGroundingCommand(tick, issuer, sequence, EntityId caster, FixVec2 target)`, script name `cast_grounding` (fields `unit`, `x`, `y`). Rejections in this order:
`unknown_unit`; `not_owner` (issuer valid and not the owner; script issuer may command any unit); `no_ability` (definition has no `grounding_ability`); `under_construction`; `out_of_bounds` (`Simulation.IsWithinWorldBounds`); `out_of_range` (centre distance from caster to target above `range_tiles`, squared compare); `on_cooldown` (`tick < caster.GroundingReadyTick`).

Apply (`StatusSystem.CastGrounding(sim, caster, target) -> int grounded`), in step 2:
1. For every unit `u` in ascending ID with `u.Health > 0`, `u.Owner` valid, `AreEnemies(caster.Owner, u.Owner)`, `u.Definition.Layer == Air`, and centre distance to `target` at most `radius_tiles + u.Definition.Radius`: `ApplyGrounded(sim, u, groundedTicks, "grounding", caster.Owner)`; count the ones that return true. `groundedTicks` is the existing `StatusRates.GroundedTicks` (`grounded_duration_seconds`, 4 s = 80 ticks; design: "A Grounded flyer lands for 4 seconds").
2. `caster.GroundingReadyTick = tick + cooldownTicks`.
3. Emit `GroundingCastEvent(tick, caster, owner, target, grounded)`.

It is an **instant pulse**, not a lingering zone (Q9); enemies only (Q10); no resource cost, the cooldown is the only cost (Q11). No vision is required for the target point (as `cast_wave`). The cast does not touch the caster's weapon cooldown, orders or `LastSelfMoveTick`.

Effects that follow from existing code, listed so the builder writes no new rule for them:
- Applied in step 2, so a grounded flyer moves on the ground layer in step 5 of the same tick, can be targeted by ground-only and melee weapons and by ground blasts in step 6, and loses airborne sight at step 10.
- It keeps its own weapon and `can_target` (a Grounded Roc Rider still shoots air; a Grounded Thunderhead still bombs). As a surface attacker it is now subject to the uphill penalty.
- Enemy floods slow it (ground speed row). Walls and footprints block it.
- Momentum: an enemy Skai flyer loses its stacks (reason `grounded`) in step 8 of the same tick and gains none until it lifts off.
- A flyer grounded on a plateau with no ramp stays there until lift-off (existing landing rule).

### 4.3 Data

Unit file, optional object (`UnitSchema.ReadGroundingAbility`, new `Units/GroundingAbilityDefinition.cs`, `UnitDefinition.GroundingAbility`, null when absent; add `"grounding_ability"` to the loader's `RejectUnknownKeys`; `ContentCatalog.ComputeDigest` writes, after the shield dome block, a presence flag and, when present, the three fields in this order):
```
// Grounding ability (Downdraft): an order to slam every enemy flyer near a point to the ground for grounded_duration_seconds (tuning/statuses.json). Source: P-005 developer decision 6 (18 Nov 2026); Factions / Grounded (Stormcaller Downdraft).
"grounding_ability": {
  // Farthest point the ability can be aimed at, from the caster's centre. Unit: tiles. Range: 0.5 to 32. Source: placeholder, developer to confirm (rts/docs/specs/combat_mechanics.md 4.3).
  "range_tiles": 7,
  // Radius around the aimed point; a flyer whose edge is inside it is grounded. Unit: tiles. Range: 0.5 to 8. Source: placeholder, developer to confirm (rts/docs/specs/combat_mechanics.md 4.3).
  "radius_tiles": 2,
  // Time before the same unit can use the ability again. Unit: seconds. Range: 1 to 300. Source: placeholder, developer to confirm (rts/docs/specs/combat_mechanics.md 4.3).
  "cooldown_seconds": 30
}
```
Placeholder rules: range = the Stormcaller's weapon range (7); radius = twice its splash radius (2 x 1.0); cooldown = `wave_cooldown_seconds` (30), the other active ability in the game. Duration: the shared `grounded_duration_seconds`, no new key. Only `skai/stormcaller.json` gets the object. `Simulation.Start` validates that every `cooldown_seconds` converts to at least one tick (same pattern as the dome check), so nothing throws inside a step.

### 4.4 State and hash

`Unit.GroundingReadyTick` (int, ticks; 0 = ready, never cast; else the first tick the ability may be used again; range 0 to int.MaxValue), internal setter used only by `StatusSystem.CastGrounding`. Written in `Unit.WriteHash` after `ShieldLastAbsorbTick` and before `Statuses.WriteHash`. Update the hash-order paragraph in `specs/README.md` and `HashCoverageTests`. The Grounded state it causes is already hashed (`UnitStatusSet`).

### 4.5 Command pipeline

- Command class in `Commands/StatusCommands.cs`. `WriteHash`: kind, caster, target.
- Codec `CastGroundingCodec` in `Commands/Codec/CoreCommandCodecs.cs` (caster as EntityId, target as FixVec2, the same layout as `CastWaveCodec`), registered in `CoreCommandCodecs.RegisterAll`. Net and Replay pick it up through the shared registry. No `ProtocolVersion` or replay format bump: no existing layout changes (the `cast_wave` precedent), and a build without the codec cannot share a match anyway because the Stormcaller file, and so the content digest, differs.
- Script: `"cast_grounding"` in `CommandScript`'s core kinds, `RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit", "x", "y")`, `ReadPoint`.
- Unity has no input for it yet (PC session work).

### 4.6 Events

`GroundingCastEvent(tick, EntityId caster, PlayerId owner, FixVec2 target, int unitsGrounded)`, kind `"grounding_cast"`, in `Events/StatusEvents.cs`. Per grounded unit the existing `StatusAppliedEvent` (key `"grounding"`) and, when newly grounded, `UnitLayerChangedEvent`, emitted by `ApplyGrounded` before this event.

### 4.7 Acceptance tests (`rts/tests/Sim.Tests/Statuses/GroundingAbilityTests.cs`)

Caster S (player 1, ground) at (5.5, 5.5) with range 7, radius 2, cooldown 30 s (600 ticks). Player 2 flyers radius 0.5.
- **D1 Pulse.** F1 at (10.5, 5.5), F2 at (12.0, 5.5) (1.5 from the point, inside 2 + 0.5), F3 at (13.5, 5.5) (3.0, outside), F4 owned by player 1 at (10.5, 6.5), ground enemy G at (9.5, 5.5). `cast_grounding(S, (10.5, 5.5))` on tick 10: F1 and F2 have `EffectiveLayer` Ground and `GroundedUntilTick` **90**; F3, F4 and G unchanged; `GroundingCastEvent.unitsGrounded` **2**; `S.GroundingReadyTick` **610**. On tick 90 F1 and F2 are Air again.
- **D2 Cooldown.** A second cast on tick 609 is rejected `on_cooldown`; one on tick 610 is accepted.
- **D3 Range.** Target (12.5, 5.5) (distance 7) is accepted; (12.75, 5.5) (7.25) is rejected `out_of_range`.
- **D4 Rejections and order.** Unknown unit; another player's caster (`not_owner`); a unit without the object (`no_ability`); a caster under construction; target beyond `WorldBoundTiles`. A command that fails two checks reports the first in 4.2's order.
- **D5 Refresh.** A second caster grounds F1 again on tick 50: `GroundedUntilTick` 130, `StatusAppliedEvent.Refreshed` true, `unitsGrounded` 1.
- **D6 Melee can hit it.** A melee ground enemy of F1 standing next to it, ordered `attack_unit` on tick 11: accepted and lands hits until tick 89; on tick 90 the target is invalid (Air) and dropped.
- **D7 Momentum.** F1 owned by a player whose faction uses `momentum`, at 5 stacks: after the cast's tick, stacks 0 and one `momentum_lost` with reason `grounded`.
- **D8 Codec and script.** Round-trip through `BuiltInMechanics.CreateCodecRegistry()`; a script line `{"type":"cast_grounding","tick":10,"issuer":1,"sequence":0,"unit":1,"x":10.5,"y":5.5}` parses to the same command; an unknown key fails.
- **D9 Hash.** Two identical runs have equal hashes; a run that casts on tick 11 instead of 10 differs at the next hash tick. Shipped Stormcaller loads with the object; no other shipped unit has it.

---

## 5. Replay, hashes and pins (every package)

Every package changes shipped data or the hash layout, so every shipped-data pin moves and older replays are refused by the content digest. Re-pin (values are whatever `main` holds when the package is built):

Each package appends a field to `WeaponDefinition.WriteHash`, `CombatRules.WriteHash` or `ContentCatalog.ComputeDigest`, which make up the content digest, and the digest is the first thing in every state hash. So **every pin moves in every package**, TestWorld's included. What must or may change beyond the hash value:

| Pin | Package 1 | 2 | 3 | 4 |
| --- | --- | --- | --- | --- |
| smoke and economy_smoke `STATE_HASH` (`Harness/HarnessUnitTests.cs`, `Economy/WorkerConstructionTests.cs`) | hash only | hash only | hash only | hash only |
| `V2ReferenceHash`, `BlindTraceHash` (`Ai/SkirmishAiTests.cs`) | **the outcome and end tick may move**: Stormcallers and Brinecasters now splash | hash only (unless R-2's Leviathan is on `main`: then the outcome may move) | the outcome may move (Reef Runners and Shellbacks on flood can now shoot flyers) | hash only (the AI never casts) |
| `DeterminismTests.GoldenFinalHash` (TestWorld, not shipped data) | hash only | hash only | hash only | hash only |

"Hash only" means every other assertion in that test holds unchanged, and for TestWorld the event log of the golden run is identical before and after the package (capture it before the change). Anything else moving under "hash only" means stop and report. When an AI outcome moves, write the old and new `OUTCOME` lines in the report and in the re-pin comment. Re-pin comments name this spec and the package.

## 6. What the skirmish AI does not do yet (separate AI package)

- It does not spread out against splash and does not aim at clumps. `FightPredictor` ignores splash (it already ignores drench and flood).
- It does not use the structure multiplier when choosing siege units (the predictor reads the table only).
- It counts Reef Runners and Shellbacks as unable to hit air (3.4).
- It never casts `cast_grounding`, and its flyers do not avoid Stormcallers.

## 7. What the equal-cost fight mode (`micro_benchmark.md`) needs to measure these

- **Area damage:** the same armies in a clumped formation (centre spacing close to the sum of radii) and a spread one (for example 2.5 tiles), with separation at its shipped tuning; an ablation run with the data override `splash_radius_tiles` 0; a splash share per side, from `BlastEvent` and the victims' `UnitDamagedEvent`s.
- **Structure multiplier:** time to kill a Rooted HQ and a static defence by each siege unit, with and without the multiplier, against the 15 s HQ floor (P-005 decision 4).
- **Anti-air on water:** an arena with the water side's half flooded before the fight. No map has water, and no command sets it, so the harness needs a pre-roll (a water flood source placed and run for N seconds before the armies spawn) or a harness-only setup step. That spec decides which. Fights: flyer raid against Reef Runners and Shellbacks on dry and on flooded ground.
- **Downdraft:** a scripted `cast_grounding` at first contact, or at a fixed tick, against a flyer army, against the same fight without the cast.

## 8. Build order and tiers

1. Package 1, area damage. 2. Package 2, structure multiplier (same `Fire` and `Land` code; after 1 to avoid conflicts). 3. Package 3, anti-air on water (uses package 1's `SplashLayers`). 4. Package 4, Downdraft (its code is independent, but it goes last so hash re-pins stay serial).

Each package: Sonnet `builder` (code, data, tests, re-pins, full suite green, `dotnet build` with no warnings), then an Opus `sim-reviewer` pass before merge. A package that fails twice moves up a tier with its failing test (CLAUDE.md).

## 9. Out of scope

Walls, tech prerequisites, a water flyer that flies only over water (a later spec). Retuning any number, including Rooted HQ armour. Damage falloff curves, friendly fire, and lingering grounding zones, unless the developer picks them in section 10. AI use of any of these. Unity input and presentation for blasts and Downdraft (PC session).

## 10. Questions for the developer

Each has a default the builder uses until you reply.

- **Q1. Friendly fire from blasts?** *Default: no, enemies only.* Friendly fire punishes the attacker's own melee pressed against the target, which hurts water (Pummelers in front of Brinecasters) more than Skai.
- **Q2. Falloff?** *Default: flat* (every victim takes the same fraction). A linear falloff needs a distance (a square root) and is harder to read in play.
- **Q3. Splash fraction: one global value or per weapon?** *Default: one global value in `combat.json`, 0.5.* Per weapon can come with tuning.
- **Q4. Does splash get the uphill penalty?** *Default: no, only the primary hit does.*
- **Q5. A shell fully stopped by a Shellback dome: blast or no blast?** *Default: no blast.*
- **Q6. Does the structure multiplier count Rooted HQs as structures?** *Default: yes (Rooted HQs wear structure armour); Uprooted HQs do not.*
- **Q7. Which water units get anti-air on water?** *Default: Reef Runner and Shellback.*
- **Q8. What counts as "on water"?** *Default: any depth above 0, any team's water.* Alternatives: own team's water only, or deep water only.
- **Q9. Downdraft: instant pulse or a zone that lingers?** *Default: instant pulse; each grounded flyer stays down for the shared 4 s.*
- **Q10. Does Downdraft ground allied flyers?** *Default: no.*
- **Q11. Does Downdraft cost anything besides its cooldown?** *Default: no cost.* There is no energy system.
- **Q12. Downdraft duration: the shared Grounded 4 s, or its own value?** *Default: the shared 4 s (design: "A Grounded flyer lands for 4 seconds").*

<!-- rts/docs/specs/planetary_cycles.md -->
<!-- Implementation spec for Planetary Cycles (a shift every 240 s with a 60 s warning, three cycles whose effects are data) and Core capture (20 s uncontested, the holder picks the next cycle, no repeat of a play style, the 8-minute Core win in 1v1v1 and FFA). Part of rts/docs/specs (index: README.md); PHASE2_PLAN.md step 4, built by a Sonnet builder in rts/src/Sim/Cycles, reviewed by sim-reviewer. -->
# Spec: Planetary Cycles and Core capture (`rts/src/Sim/Cycles`)

## 1. Purpose and design-doc source

- **Planetary Cycles** (Win condition and twists / Planetary Cycles): the planet shifts every 240 s (`planetary_cycle_interval_seconds`), and the next cycle is shown 60 s ahead (`planetary_cycle_warning_seconds`). "Cycles favour a play style, not a faction." Three cycles, from `rts/data/cycles/*.json`:
  - Windstorm (favours air): "Flyers gain speed; ground ranged units lose range."
  - Quake (favours attackers): "Walls and defences take damage; some paths open or close."
  - Core surge (favours position): "Units near the Core gain damage."
- **Holding the Core**: a unit must spend 20 s uncontested in the Core radius (`core_capture_seconds`) to make its side the holder. The holder picks the next cycle; the same play style never comes twice in a row. In 1v1v1 and FFA, holding the Core for 480 cumulative seconds (`ffa_core_win_cumulative_seconds`) wins.
- **Maps / Central element: the Core**: centre and capture radius are map data (`core` in `rts/data/maps/*.json`; `greybox_1v1`: (32, 32), radius 4 tiles).
- Architecture rule 6: one match clock drives tides, cycles, warnings and capture. Rule 4: cycles are core rules, not a faction mechanic, so `CycleSystem` lives in the core and may be named by core systems (combat), but never names a faction.
- Durations (ADR-004): interval 4800 ticks, warning 1200 ticks, capture 400 ticks, Core win 9600 ticks.

## 2. Data

**Existing keys** (`tuning/match.json`): `planetary_cycle_interval_seconds` (240), `planetary_cycle_warning_seconds` (60), `core_capture_seconds` (20), `ffa_core_win_cumulative_seconds` (480). Validation change in `MatchTuning`: the warning must be **shorter than** the interval (strict `<`, message `"must be shorter than planetary_cycle_interval_seconds"`), so every lock tick falls strictly after the previous shift.

**New keys in `tuning/match.json`** (add to `MatchTuning` with the same comment text):

| Key | Unit | Range | Value | Controls / source |
| --- | --- | --- | --- | --- |
| `core_win_min_teams` | teams (count) | 2 to 16 | 3 | The cumulative-hold win is on when the match starts with at least this many teams. Source: Holding the Core ("in 1v1v1 and FFA"); 3 is the design reading, not a placeholder. |
| `quake_path_relocate_radius_tiles` | tiles (Chebyshev rings) | 0 to 8 | 3 | How far a unit standing on a closing quake-path tile is moved to a standable tile. **PLACEHOLDER**. |

**New `effects` object in every `cycles/<id>.json`** (required; every key required, so no default hides a typo). Parsed into `CycleEffects` on `CycleDefinition`. Each key gets the standard comment line (`// <what>. Unit: . Range: . Source: Win condition and twists / Planetary Cycles; PLACEHOLDER.`).

| Key | Unit | Range | windstorm | quake | core_surge | What it does |
| --- | --- | --- | --- | --- | --- | --- |
| `air_speed_multiplier` | speed multiplier | 0.5 to 2 | **1.25** | 1 | 1 | Speed of units whose `EffectiveLayer` is Air (a Grounded flyer is not Air). |
| `surface_ranged_range_bonus_tiles` | tiles | -4 to 4 | **-1** | 0 | 0 | Added to the weapon range of *surface ranged units*: mobile (`SpeedTilesPerSecond > 0`), not an HQ (`Hq == null`), `EffectiveLayer` not Air, weapon not melee. Water-layer units count as surface units. |
| `defence_damage_per_second_fraction` | fraction of max health per second | 0 to 0.05 | 0 | **0.004** | 0 | Quake damage to static defences (`Economy.IsStaticDefence`), applied once per second. Wall structures join this set when they exist (Phase 4). |
| `defence_damage_min_health_fraction` | fraction of max health | 0 to 1 | 0.25 | **0.25** | 0.25 | Quake damage never takes a defence below this fraction, so a quake never kills. |
| `toggles_quake_paths` | boolean | true or false | false | **true** | false | While active, every quake path of the map (below) flips open or closed. |
| `core_damage_multiplier` | damage multiplier | 1 to 3 | 1 | 1 | **1.5** | Outgoing damage of any attacker whose centre is within `core_effect_radius_tiles` of the Core centre. |
| `core_effect_radius_tiles` | tiles | 0 to 32 | 0 | 0 | **8** | Radius of the Core surge damage area (twice the greybox capture radius). |

All bold values are **PLACEHOLDER**. The loader also checks (`cycles/` as a whole, error on the first cycle file in ordinal order): when any cycle file is present there must be at least two distinct `favours` values (`"cycles: at least two play styles are needed so the no-repeat rule can always pick"`). `CycleDefinition.WriteHash(hasher)` writes `Id`, `(byte)Favours`, then the seven effect values in table order (display name and description are left out).

Example (`cycles/windstorm.json`, keys added after `description`):
```jsonc
  // Effect numbers of this cycle; every key is required (planetary_cycles.md section 2). Source: Win condition and twists / Planetary Cycles.
  "effects": {
    // Speed multiplier for units flying (effective layer Air) while the cycle is active. Unit: speed multiplier. Range: 0.5 to 2. Source: Planetary Cycles (Windstorm: flyers gain speed); PLACEHOLDER.
    "air_speed_multiplier": 1.25,
    // Weapon range change for mobile non-HQ surface units with a ranged weapon; never below melee range 1.5. Unit: tiles. Range: -4 to 4. Source: Planetary Cycles (Windstorm: ground ranged units lose range); PLACEHOLDER.
    "surface_ranged_range_bonus_tiles": -1,
    // ... the other five keys with 0 / 0.25 / false / 1 / 0
  }
```

**New optional map key `quake_paths`** (`MapDefinition.QuakePaths`, read by `DataLoader.ReadMap`; absent means none; `greybox_1v1` has none, so Quake is damage-only there until map authors add paths):
```jsonc
  // Quake paths: tile groups that are open or closed outside a Quake and flip while a cycle with toggles_quake_paths is active. Unit: tile positions. Range: at most 16 paths of 1 to 64 tiles. Source: Planetary Cycles (Quake: some paths open or close); placement is a map-design choice.
  "quake_paths": [
    { "id": "west_gap", "closed_outside_quake": true, "tiles": [ { "x": 20, "y": 40 }, { "x": 21, "y": 40 } ] }
  ]
```
Validation: `id` unique snake_case; 1 to 64 tiles per path, 0 to 16 paths; tiles inside the map; no tile in two paths or twice in one; no tile holds a resource field. `MapDefinition.WriteHash` appends, after the Core: path count, then per path `id`, `closed_outside_quake`, tile count, each tile `x`, `y`.

**Getting cycles into the match**: `SimulationConfig` gains an optional last constructor parameter `IReadOnlyList<CycleDefinition>? cycles` (null means none), stored sorted by ordinal `Id`. Hosts pass `data.Cycles.Values`; the harness, Unity host and mission runner do; unit tests that pass nothing get a match **without Planetary Cycles** (Core capture still runs). `Simulation.Start` writes the cycles into the content digest after the map: count, then each `WriteHash`. This changes every state hash once (update `DeterminismTests.GoldenFinalHash` with a comment naming this spec).

## 3. State

`Simulation.Cycles` (new, `CycleSystem`, constructed in the `Simulation` constructor like `Vision`). Cached at `OnMatchStart` (pure functions of tuning, not hashed): `IntervalTicks` (4800), `WarningTicks` (1200), `CaptureTicks` (400), `WinTicks` (9600), `CoreWinEnabled = Players.TeamCount >= core_win_min_teams`, per cycle the per-application quake damage is **not** cached (computed per unit, below).

Hashed (`CycleSystem.WriteHash`, in this order):

| Field | Type, unit | Range | Meaning |
| --- | --- | --- | --- |
| `Enabled` | bool | | Cycles list non-empty. |
| `ActiveCycleId` | string | "" or a cycle ID | Cycle in force; "" before the first shift. |
| `ActiveSinceTick` | int, ticks | -1 or a shift tick | Tick the active cycle started. |
| `NextCycleId` | string | "" or a cycle ID | Locked next cycle; "" until the lock tick. |
| `NextLocked` | bool | | True from the lock tick to the shift tick. |
| `PendingPickId` | string | "" or a cycle ID | Accepted `pick_cycle` waiting for the lock. |
| `PendingPickTeam` | `TeamId` | team or None | Team that picked; `None` means a script pick. |
| `Holder` | `TeamId` | team or None | Core holder. |
| `CaptureTeam` | `TeamId` | team or None | Team currently capturing. |
| `CaptureProgressTicks` | int, ticks | 0 to `CaptureTicks - 1` | Progress of `CaptureTeam`. |
| `IsContested` | bool | | Two or more teams present in the Core this tick. |
| `HoldTicks` | count, then per team ascending: `TeamId`, int ticks | 0 to `WinTicks` | Cumulative uncontested holding per team (every team of the registry is written). |
| `CoreWinner` | `TeamId` | team or None | Set once by the Core win; freezes capture and accrual. |
| quake paths | count, then per path ascending index: `IsClosed` (bool), tile count, per tile `ClosedByCycle` (bool) | | Which path tiles this system has walled. |

Collections: `SortedDictionary<TeamId, int>` for `HoldTicks` (or an array in `Players.Teams` order); paths in file order. No `Dictionary`/`HashSet`, no float.

## 4. Per-tick algorithm

**Step order** (new step 9; the coordinator edits the `Simulation.cs` header and `Step`): 1 clear events, 2 commands, 3 statuses, 4 HQ timers, 5 movement, 6 combat, 7 economy, 8 faction mechanics, **9 cycles and Core (`CycleSystem.Step`)**, 10 vision, 11 state hash, 12 advance the clock. Capture therefore reads positions after movement, combat deaths, economy spawns and wave pushes. A cycle that shifts on tick S is read by movement and combat from tick S+1 (like statuses); quake damage and path toggles happen on tick S itself.

**Start** (`CycleSystem.RegisterModifiers(sim)` and `CycleSystem.OnMatchStart(sim)`, called by `Simulation.Start` next to the economy calls, inside the modifier registration window): register `ISpeedModifier` and `IDamageModifier`, both with `Id = "cycle"`, always (even when disabled, so the hashed modifier ID lists do not depend on data). Cache the tick values. Close every path with `closed_outside_quake = true` (procedure **SetPath** below; no unit can stand there yet except script-spawned ones, which are relocated the same way).

**Schedule maths** (pure, `CycleRules`): shift k (k >= 1) at `S_k = k * IntervalTicks`; lock tick `L_k = S_k - WarningTicks`. The first cycle starts at tick 4800; ticks 0 to 4799 have no cycle (the 0 to 4 min opening). `NextShiftTick(t) = (t / IntervalTicks + 1) * IntervalTicks`.

**Counting unit** (`CycleRules.CountsForCore`): live, `Health > 0`, owner a valid player who `IsInMatch` (Active or Disconnected), mobile (`Definition.SpeedTilesPerSecond > 0`; this admits workers, the HQ and flyers on any layer and excludes buildings), and `(Position - Core.Centre).LengthSquared() <= Radius * Radius` (centre distance, unit radius ignored; exactly on the edge counts). "Near the Core" means this test with the map's `radius_tiles`.

**CycleSystem.Step**, every tick, in this order (`t` = current tick):

1. *Out-of-match sides.* If `Holder != None` and no player of `Holder` is in match: emit `core_lost(Holder, "holder_out")`, set `Holder = None`; clear a pending pick whose `PendingPickTeam` was that team. If `CaptureTeam` has no player in match: `core_capture_reset(CaptureTeam, CaptureProgressTicks, "holder_out")`, clear it.
2. *Presence* (skipped entirely, with no events, when the map has no Core or `CoreWinner != None`): `present` = ascending set of teams with at least one counting unit (scan `sim.Units.All`, ascending ID). New `IsContested = present.Count >= 2`; when it changes emit `core_contested_changed(IsContested)`.
3. *Capture state machine.* `challengers = present` without `Holder`; `holderPresent = present contains Holder`.
   - `challengers.Count == 1` and not `holderPresent`, with `C` that challenger: if `CaptureTeam != C`: if `CaptureTeam != None` emit `core_capture_reset(CaptureTeam, progress, "other_team")`; set `CaptureTeam = C`, progress 0, emit `core_capture_started(C)`. Then `progress += 1`; if `progress == CaptureTicks`: previous = `Holder`; `Holder = C`; `CaptureTeam = None`; progress 0; if previous != None emit `core_lost(previous, "captured")`; emit `core_captured(C, previous)`; clear a pending pick whose team is not `C` and not `None`.
   - `challengers.Count == 0`: if `CaptureTeam != None` emit `core_capture_reset(CaptureTeam, progress, "left")` and clear it.
   - otherwise (contested): if `CaptureTeam` is in `present`, progress is frozen; if not, reset it with reason `"left"`.
   So capture needs `CaptureTicks` counted ticks during which the capturing team was alone; contest pauses, leaving resets, a different lone team restarts from 0.
4. *Hold accrual.* If `Holder != None`, `CoreWinner == None` and no team other than `Holder` is in `present`: `HoldTicks[Holder] += 1` (capped at `WinTicks`). The capture tick itself counts. A holder that has walked away keeps the hold and keeps accruing until someone else contests or captures; holding ends only by capture or leaving the match.
5. *Core win.* If `CoreWinEnabled`, `CoreWinner == None` and `HoldTicks[Holder] >= WinTicks`: `CoreWinner = Holder`; emit `core_win(Holder, HoldTicks)`; then for every player ascending ID not on `Holder` with `IsInMatch`: `PlayerTransitions.TryChange(sim, id, Defeated, "core_held", out _)`. `MatchOutcome` then names the holder as winner. Allies of the holder share the win.
6. *Lock* (only when `Enabled`): if `t == L_k` for the upcoming shift k (`k = t / IntervalTicks + 1`, or `t / IntervalTicks` when `WarningTicks == 0` and `t` is a shift tick): `allowed` = cycles in ascending ID whose `Favours` differs from the active cycle's (all of them before the first shift). If `PendingPickId != ""` and (`PendingPickTeam == None` or `PendingPickTeam == Holder`) and the pick is in `allowed`: next = the pick, method `"scripted"` (team None) or `"picked"`. Otherwise next = `allowed[sim.Rng.NextInt(allowed.Count)]`, method `"random"` (also when there is a holder that did not pick; the draw is the only RNG use here). Set `NextCycleId`, `NextLocked = true`, clear the pending pick, emit `cycle_warning(next, S_k, method, chosenBy)` where chosenBy is the holder for `"picked"`, else None.
7. *Shift* (only when `Enabled`): if `t == S_k`: previous = active; active = `NextCycleId`; `ActiveSinceTick = t`; `NextCycleId = ""`; `NextLocked = false`; emit `cycle_started(active, previous or "", S_{k+1})`. If `previous.toggles_quake_paths != active.toggles_quake_paths`, run **SetPath** for every path ascending to its new state: closed iff `closed_outside_quake XOR active.toggles_quake_paths`.
8. *Quake damage*: if the active cycle's `defence_damage_per_second_fraction > 0` and `(t - ActiveSinceTick) % TicksPerSecond == 0` (so on the shift tick and every 20 ticks after; 240 applications per cycle): for each unit ascending ID with `IsStaticDefence`, `Health > 0`, not under construction, not invulnerable: `amount = MaxHealth * rate`, `floor = MaxHealth * min_fraction` (both Fix64, left to right); if `Health > floor`: `newHealth = Max(floor, Health - amount)`; `SetHealth(newHealth)`; emit `UnitDamagedEvent(t, EntityId.None, unit, Health_before - newHealth, newHealth)` (attacker None means "the environment"; `HqSystem.AfterDamage` is not called because HQs are not defences). Any owner, neutral included.

**SetPath(path, closed)**: opening removes the `Wall` blocker from every tile with `ClosedByCycle` and clears the bit. Closing, per tile ascending index, skipping tiles with a `Footprint` or an existing `Wall`: every unit ascending ID with `EffectiveLayer != Air` whose `TileOf(Position)` is the tile is moved to the centre of the first tile on Chebyshev rings 1 to `quake_path_relocate_radius_tiles` (ascending index per ring) that is `IsStandable` for its effective layer and is not a tile of a path closing in this call; it keeps its orders (the movement system re-paths) and emits `unit_relocated(unit, from, to, "quake_path")`. If any such unit has no candidate, the tile stays open this time. Otherwise `TerrainGrid.AddBlockers(sim, i, Wall)` and set `ClosedByCycle`. Emit `quake_path_changed(pathIndex, closed, tilesChanged)`. Reusing `Wall` (no new ADR-006 flag): walls block ground and water movement and flood flow, which is what a rockfall should do, and the terrain version bump invalidates flow fields on its own.

**Modifiers** (pure reads, no state change):
- `ISpeedModifier "cycle"`: `air_speed_multiplier` of the active cycle when the unit's `EffectiveLayer` is Air, else `One`.
- `IDamageModifier "cycle"`: `core_damage_multiplier` when the map has a Core and `(attacker.Position - Centre).LengthSquared() <= r * r` with `r = core_effect_radius_tiles`, else `One`.
- Range: `CycleSystem.EffectiveRangeTiles(Unit unit, WeaponDefinition weapon)`: for a surface ranged unit under a non-zero bonus, `Max(weapon.RangeTiles + bonus, WeaponDefinition.MeleeMaxRangeTiles)` (a ranged weapon never shrinks into melee; Windstorm turns range 2 into 1.5), otherwise `weapon.RangeTiles`. `CombatSystem` uses it in `StepUnit` (fire reach) and `ResolveTarget` (acquire reach = effective range + bonus). Request to the coordinator; sim-reviewer pass.

## 5. Commands and events

**`PickCycleCommand(tick, issuer, sequence, string cycleId)`**, `Commands/CycleCommands.cs`, `Kind` and script type `pick_cycle` (added to `CommandScript.CoreKinds`; script field `"cycle"`). Rejections in this order: `cycles_disabled`; for a player issuer `player_out_of_match` (issuer not Active) then `not_holder` (issuer's team is not `Holder`, including when there is no holder or no Core); `unknown_cycle`; `pick_locked` (`NextLocked`); `repeats_play_style` (same `Favours` as the active cycle). Issuer `None` (script, campaign) skips the holder check. Apply: `PendingPickId = cycleId`, `PendingPickTeam` = issuer's team or `None`; emit `cycle_picked(issuer, team, cycleId)`. Later accepted picks replace earlier ones. A pick on the lock tick itself counts (commands run at step 2, the lock at step 9).

**Events** (`Events/CycleEvents.cs`, kinds as named; every event also carries `Tick`):

| Kind | Fields | When |
| --- | --- | --- |
| `cycle_picked` | `PlayerId Player, TeamId Team, string CycleId` | accepted pick |
| `cycle_warning` | `string CycleId, int ShiftTick, string Method ("picked", "scripted", "random"), TeamId ChosenBy` | lock tick |
| `cycle_started` | `string CycleId, string PreviousCycleId, int EndsAtTick` | shift tick |
| `core_contested_changed` | `bool IsContested` | contested flag flips |
| `core_capture_started` | `TeamId Team` | a lone team starts from 0 |
| `core_capture_reset` | `TeamId Team, int ProgressTicks, string Reason ("left", "other_team", "holder_out")` | progress lost |
| `core_captured` | `TeamId Team, TeamId PreviousHolder` | capture completes |
| `core_lost` | `TeamId Team, string Reason ("captured", "holder_out")` | holder changes |
| `core_win` | `TeamId Team, int HoldTicks` | cumulative win |
| `quake_path_changed` | `int PathIndex, string PathId, bool Closed, int TilesChanged` | SetPath |
| `unit_relocated` | `EntityId Unit, FixVec2 From, FixVec2 To, string Reason` | quake closure |

**Read API** for UI, AI and campaign (`sim.Cycles`, all read-only): `IsEnabled`, `ActiveCycle` (`CycleDefinition?`), `ActiveSinceTick`, `NextCycle` (null until locked), `IsNextLocked`, `NextShiftTick`, `NextLockTick`, `AllowedNextCycles()` (ascending ID), `PendingPick`, `Holder`, `CaptureTeam`, `CaptureProgressTicks`, `CaptureTicks`, `IsContested`, `HoldTicks(TeamId)`, `WinTicks`, `IsCoreWinEnabled`, `CoreWinner`, `EffectiveRangeTiles(unit, weapon)`. The Unity timeline bar (plan step 9) reads these plus `TideClock`.

**AI** (`rts/src/Ai`, same step 4 build): profile key per faction `cycle_preference` (list of every cycle ID, most wanted first; `AiProfile.Parse` checks each exists; PLACEHOLDER orders: skai `["windstorm", "core_surge", "quake"]`, water `["core_surge", "quake", "windstorm"]`) and root key `rally_at_core` (bool, PLACEHOLDER `true` in `aggressive.json`, `false` in `defensive.json`). New rule 9 *Cycle*, after the HQ rule: when own team is `Holder`, the next cycle is not locked and `PendingPick` is not already the AI's choice, emit `PickCycleCommand` for the first preference in `AllowedNextCycles()`. With `rally_at_core`, the Massing rally point becomes the Core centre whenever the Core exists and own team is not the holder, so a massing army takes and holds it. `AiWorldView` copies the read API above (all public information).

**Scripts and campaign**: `{ "tick": 100, "issuer": 255, "sequence": 3, "type": "pick_cycle", "cycle": "quake" }` forces the next cycle (tests, missions). A mission that wants no cycles passes no cycle list (campaign_scripting.md, `planetary_cycles: false`).

## 6. Interfaces

- `CycleSystem` (instance on `Simulation`, internal `Step`, `RegisterModifiers`, `OnMatchStart`, public read API, `WriteHash`); `CycleRules` (static, pure: schedule ticks, `CountsForCore`, `Allowed`, and `CaptureStep(ref CoreState, presentTeams, captureTicks)` returning the events to raise, so the state machine is tested without a simulation); `CycleEffects` (immutable data); the two modifier classes (private nested or `CycleModifiers.cs`).
- Writes only through `Unit.SetHealth`, `TerrainGrid.AddBlockers/RemoveBlockers`, unit position setter used by statuses landing (`Unit.SetPosition`), `PlayerTransitions.TryChange`, `sim.Rng` (one draw per lock without a valid pick).
- Coordinator edits: `Simulation` (field, constructor, `Start` calls, `Step` call at 9, hash after resource fields, digest), `SimulationConfig` (optional cycles), `CombatSystem` (effective range, two call sites), `DataLoader` (`effects`, `quake_paths`, two-favour check), `MatchTuning` (two keys, strict warning check), `CommandScript` (`pick_cycle`).
- Must NOT: name a faction or mechanic, read `Dictionary`/`HashSet` order, use float, draw from `sim.Rng` anywhere but the lock, change state from a modifier, or kill a unit with quake damage.

## 7. Acceptance tests (`rts/tests/Sim.Tests/Cycles/`)

World unless stated: flat 32x32 test map with a Core at (16, 16) radius 4, shipped `match.json` values, the three shipped cycle files, players 0 (team 0), 1 (team 1) and, where named, 2 (team 2); test unit "scout": ground, speed 2, no weapon. "Present" means a scout spawned by a script command at tick 0 at (16.5, 16.5) unless stated. Ticks are the tick the event is raised on.

1. **Schedule.** No units, 9601 ticks: `cycle_warning` on ticks 3600 and 8400 only; `cycle_started` on 4800 (previous "") and 9600; `EndsAtTick` 9600 and 14400. `planetary_cycle_warning_seconds` 0 with interval 60 s: warning and start both on tick 1200, warning first. Warning equal to the interval is a load error naming `planetary_cycle_warning_seconds`.
2. **Capture time and edge.** Team 0 present: `core_capture_started(0)` on tick 0, `core_captured(0, None)` on tick 399, `Holder` 0 after that step. A scout at (20, 16) (distance exactly 4) captures on 399; one at (20.01, 16) never starts.
3. **Contest freezes.** Team 0 present; team 1 scout spawned at (17, 16) on tick 100 and removed (`remove_unit`) on tick 200: `core_contested_changed(true)` on 100, `(false)` on 200, progress 100 throughout ticks 100 to 199, `core_captured(0)` on tick 499.
4. **Leaving resets.** Team 0 present, removed on tick 150: `core_capture_reset(0, 150, "left")` on 150; a new team 0 scout on tick 160: started on 160, captured on 559.
5. **Other lone team restarts.** Team 0 present, removed on tick 100; team 1 scout spawned on tick 100: on tick 100 `core_capture_reset(0, 100, "other_team")` then `core_capture_started(1)`; captured by 1 on tick 499.
6. **Holder blocks.** Team 0 captures on 399 and stays; team 1 scout from tick 500: contested, no progress; team 0 scout removed on 600: team 1 starts on 600, `core_lost(0, "captured")` then `core_captured(1, 0)` on 999.
7. **Who counts.** In the radius on their own: a speed-0 building of team 1, a neutral (`PlayerId.None`) scout, and a scout of a player who surrendered on tick 0: none starts a capture. An air test unit of team 1 starts one on its spawn tick.
8. **Core win (FFA).** Three teams, `ffa_core_win_cumulative_seconds` 60 (1200 ticks): team 0 present; `core_win(0, 1200)` on tick 1598; players 1 and 2 `Defeated` with cause `core_held` on 1598; `MatchOutcome.Winner` team 0. Variant: a team 1 scout present ticks 500 to 599: win on 1698. Two teams (1v1): no win, `HoldTicks[0]` keeps counting to the cap; `core_win_min_teams` 2 makes the 1v1 win on 1598.
9. **Holder picks.** Team 0 captures on 399; player 0 `pick_cycle quake` on tick 1000: `cycle_picked`; `cycle_warning(quake, 4800, "picked", team 0)` on 3600; `cycle_started(quake)` on 4800. Player 1 picking on 1000: `not_holder`. Player 0 on tick 3600: accepted; on tick 3601: `pick_locked`. `pick_cycle storm`: `unknown_cycle`.
10. **No repeat.** Continuing 9: player 0 picks `quake` on tick 5000: `repeats_play_style`; `windstorm` on 5001: accepted; warning on 8400 names windstorm.
11. **Random without a holder.** No units, 20 shifts (96 001 ticks): every `cycle_started` differs in `Favours` from the one before; two runs with seed 1 give the same sequence; seed 1 and seed 2 give different sequences; `sim.Rng.DrawCount` changes only on lock ticks and grows by at least 20 (rejection sampling may draw twice).
12. **Pick cleared by a new holder.** Test 9's pick, then test 6's takeover finishing before tick 3600 (team 1 captures on 999): warning on 3600 has method `"random"`. A script pick (issuer 255) on tick 10 with no holder: method `"scripted"`, and it survives a capture.
13. **Holder leaves the match.** Team 0 holds from 399; player 0 surrenders on tick 500: `core_lost(0, "holder_out")` on 500, `Holder` None, a pending team 0 pick is dropped.
14. **Windstorm.** Interval 60 s, warning 0, script pick `windstorm` on tick 0, shift on 1200. An air unit (speed 4) moving east covers 0.2 tiles on tick 1200 and 0.25 (within 2^-30) on tick 1201. A ground ranged attacker (range 5, radius 0.5, damage 1) and an idle enemy (radius 0.5, max health 100 000) 5.75 tiles east (edge distance 4.75): hit on tick 0; with windstorm active from 1201, no `unit_damaged` from it in ticks 1201 to 1300. A range-2 ground unit reads `EffectiveRangeTiles` 1.5; a melee (range 1) unit, an air unit and an HQ read their weapon range.
15. **Quake.** Same setup with `quake`, a static defence of 300 max health and an HQ: on ticks 1200, 1220, ... the defence loses `300 * Fix64.Parse("0.004")` each time, one `UnitDamagedEvent` with attacker `None` per application; after application 188 (tick 1200 + 187 x 20) health is exactly 75 and stays 75 with no further events; the HQ and a mobile unit are untouched; a defence under construction is untouched.
16. **Core surge.** Same setup with `core_surge`: from tick 1201 an attacker at 8 tiles from the Core centre deals 15 per 10-damage hit (multiplier table 1), one at 8.5 tiles deals 10.
17. **Quake paths.** Map with path `gap` (tiles (5, 5), (6, 5), `closed_outside_quake: true`) and path `bridge` (tile (9, 9), false): after `Start` tiles (5, 5), (6, 5) carry `Wall`, (9, 9) does not. Quake from 1200: on 1200 `gap` opens and `bridge` closes; a ground unit on (9, 9) is relocated to (8.5, 8.5) (ring 1, lowest index) with `unit_relocated`; a flyer there is not moved. At the next shift (no quake) both flip back. A path tile with a footprint is skipped.
18. **Hash.** Two identical runs of test 9 match at every `state_hashed`; the run without the pick differs from tick 3600 on; `HashCoverageTests` gains one case per hashed field of section 3.
19. **Data.** A cycle file missing `effects.core_effect_radius_tiles`: error `cycles/core_surge.json: field 'effects.core_effect_radius_tiles' is missing`; `air_speed_multiplier` 3: range error; three cycles all `favours: "air"`: the two-play-style error; a `quake_paths` tile on a resource field: error naming the map and field.
20. **Disabled.** A config without cycles: no cycle events in 9600 ticks, `pick_cycle` rejected `cycles_disabled`, capture still works (test 2). A map without a Core: no `core_*` events, picks by players `not_holder`, Core surge damage 1.
21. **AI pick.** `AiBrain` with a view where own team holds, active windstorm, preference skai `["windstorm", "core_surge", "quake"]`: one `PickCycleCommand(core_surge)`; on the next think with that pick pending: none; while locked: none.
22. **Harness determinism.** `ai_vs_ai.json` (cycles on, seed 1234) twice: identical output; with `--ticks 10000` the run shows at least one `cycle_started`.

## 8. Open questions and follow-ups

- Hold accrual while the holder has left the Core (chosen: yes, until contested) versus only while present; playtest call.
- Whether workers and flyers should count for capture (chosen: every mobile unit).
- Cycle effects on the AI (Skai attacking during Windstorm, retreating from Quake paths) are tactics for later profiles.
- Earth walls (Phase 4) join the Quake damage set; fire cycles or a fourth play style need only data.
- Quake paths on the shipped maps: map authors (plan step 7) choose them; the map checklist should require both bases stay connected whichever state the paths are in.
- 2v2v2 gets the Core win under `core_win_min_teams` 3; revisit if team modes ship.

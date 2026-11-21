// rts/src/Ai/FightSim.cs
// The lane simulation of the fight predictor (spec: rts/docs/specs/fight_predictor_v2.md sections 3 and 4): the units of both sides fight in a one-dimensional lane in fixed time steps with the
// game's damage table, cooldowns, layers, ranges, speeds, shield domes (with regeneration), splash, drench and grounding, Momentum, the uphill damage multiplier and the flood speed rules,
// and the result is mapped onto the same FightPrediction the formula returns (3.3). A pure function of its arguments: Fix64 and integers only, no random number, no clock, no file, no static
// mutable state, lists walked in index order, every tie broken by the lower index. Part of Rts.Ai. It names no faction or unit: the Momentum units are found through the faction mechanic ID.
// Left out on purpose (3.4): choke width and the number of melee units that can reach one target; the ramp and the high-ground vision rule (only the uphill damage multiplier); formation
// depth (all units of a side start at one point); hit delay; Windstorm range; the AI's own focus-fire and kiting micro; Downdraft casts; pathing and unit separation; the footprints of
// structures. Also not modelled, because the spec states no rule for it: the dry-ground speed factor of water-layer units (TerrainTuning water_unit_dry_speed_multiplier) and structure
// damage multipliers (structures fight as speed-0 units).
using System;
using System.Collections.Generic;
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Skai;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>Which model the fight predictor uses.</summary>
    public enum PredictorModel : byte
    {
        /// <summary>The closed-form estimate of skirmish_ai_v3.md C.1.</summary>
        Formula = 0,

        /// <summary>The lane simulation <see cref="FightSim"/>.</summary>
        Sim = 1,
    }

    /// <summary>The game data the lane simulation reads, built once per match. Immutable.</summary>
    public sealed class FightSimRules
    {
        /// <summary>The damage table (ContentCatalog.Combat).</summary>
        public CombatRules Combat { get; }

        /// <summary>Momentum stack rates and bonuses (tuning/momentum.json).</summary>
        public MomentumTuning Momentum { get; }

        /// <summary>Drench and grounding values (tuning/statuses.json).</summary>
        public StatusTuning Status { get; }

        /// <summary>Flood speed multipliers for enemy ground units and the water-unit healing rate (tuning/flood.json).</summary>
        public FloodTuning Flood { get; }

        /// <summary>Speed multiplier of a water-layer unit in shallow water (tuning/terrain.json). Unit: multiplier of speed. Range: above 0.</summary>
        public Fix64 WaterUnitShallowSpeed { get; }

        /// <summary>Speed multiplier of a water-layer unit in deep water (tuning/terrain.json). Unit: multiplier of speed. Range: above 0.</summary>
        public Fix64 WaterUnitDeepSpeed { get; }

        /// <summary>Definition IDs of the units that gain Momentum (their faction's mechanic is Momentum and they move), ordinal order.</summary>
        private readonly SortedSet<string> _momentumUnits;

        /// <summary>True when the unit type gains Momentum.</summary>
        /// <param name="definitionId">Unit definition ID.</param>
        public bool IsMomentumUnit(string definitionId)
        {
            return _momentumUnits.Contains(definitionId);
        }

        /// <summary>Bundles the values.</summary>
        /// <param name="combat">The damage table.</param>
        /// <param name="momentum">Momentum tuning.</param>
        /// <param name="status">Status tuning.</param>
        /// <param name="flood">Flood tuning.</param>
        /// <param name="terrain">Terrain tuning (the two water-unit speed multipliers).</param>
        /// <param name="momentumUnits">Definition IDs of the Momentum units.</param>
        public FightSimRules(CombatRules combat, MomentumTuning momentum, StatusTuning status, FloodTuning flood, TerrainTuning terrain, IEnumerable<string> momentumUnits)
        {
            Combat = combat;
            Momentum = momentum;
            Status = status;
            Flood = flood;
            WaterUnitShallowSpeed = terrain.WaterUnitShallowSpeedMultiplier;
            WaterUnitDeepSpeed = terrain.WaterUnitDeepSpeedMultiplier;
            _momentumUnits = new SortedSet<string>(momentumUnits, StringComparer.Ordinal);
        }

        /// <summary>Builds the rules from the content and tuning of a match (or of a loaded data set).</summary>
        /// <param name="content">The content catalogue.</param>
        /// <param name="tuning">The tuning values.</param>
        public static FightSimRules Build(ContentCatalog content, Tuning tuning)
        {
            // IDs of the Momentum units: a unit that moves and whose faction's mechanic is Momentum (the test the harness uses; no faction is named).
            List<string> momentum = new List<string>();
            foreach (UnitDefinition unit in content.Units.Values)
            {
                if (unit.SpeedTilesPerSecond > Fix64.Zero && content.Factions.TryGetValue(unit.FactionId, out FactionDefinition? faction) && faction.MechanicId == MomentumMechanic.MechanicId)
                {
                    momentum.Add(unit.Id);
                }
            }
            return new FightSimRules(content.Combat, tuning.Momentum, tuning.Status, tuning.Flood, tuning.Terrain, momentum);
        }
    }

    /// <summary>The lane simulation. Pure and stateless; every call builds its own lane.</summary>
    public static class FightSim
    {
        /// <summary>Pi, a mathematical constant (not tuning) for the number of units a blast reaches. Unit: ratio. Range: fixed.</summary>
        private static readonly Fix64 Pi = Fix64.Parse("3.141592654");

        /// <summary>One simulated unit.</summary>
        private sealed class SimUnit
        {
            /// <summary>The unit type.</summary>
            public UnitDefinition Def = null!;

            /// <summary>Its weapon, or null when unarmed.</summary>
            public WeaponDefinition? Weapon;

            /// <summary>Side: 0 is A, 1 is B. Unit: side index. Range: 0 or 1.</summary>
            public int Side;

            /// <summary>Position along the lane. Unit: tiles. Range: about -50 to gap + 50.</summary>
            public Fix64 X;

            /// <summary>Health. Unit: health points. Range: above 0 while alive; at most the definition's MaxHealth.</summary>
            public Fix64 Health;

            /// <summary>Damage taken in the current step, applied together at its end. Unit: health points. Range: 0 upward.</summary>
            public Fix64 Pending;

            /// <summary>True until the unit dies.</summary>
            public bool Alive;

            /// <summary>Seconds until the weapon may fire; at or below 0 it is ready. Unit: seconds. Range: -step to the weapon's cooldown.</summary>
            public Fix64 Timer;

            /// <summary>Index of the nearest enemy this unit can hit, or -1. Unit: index into the other side's list. Range: -1 upward.</summary>
            public int Target;

            /// <summary>True when <see cref="Target"/> is within weapon reach.</summary>
            public bool InReach;

            /// <summary>True when the unit moved in the last step.</summary>
            public bool Moved;

            /// <summary>Position after this step's movement. Unit: tiles.</summary>
            public Fix64 NextX;

            /// <summary>True for a Momentum unit.</summary>
            public bool Momentum;

            /// <summary>Momentum stacks, fractional (the bonus uses the whole number). Unit: stacks. Range: 0 to max_stacks.</summary>
            public Fix64 Stacks;

            /// <summary>Seconds since the unit last moved. Unit: seconds. Range: 0 upward.</summary>
            public Fix64 StillSeconds;

            /// <summary>Second until which the drench slow holds. Unit: seconds of simulated time. Range: 0 upward.</summary>
            public Fix64 SlowUntil;

            /// <summary>True while a flyer is Grounded (its layer is Ground).</summary>
            public bool Grounded;

            /// <summary>Second the Grounded status ends. Unit: seconds of simulated time. Range: 0 upward.</summary>
            public Fix64 GroundedUntil;

            /// <summary>Drench hits counted since the last reset or grounding. Unit: hits. Range: 0 to hits_to_ground.</summary>
            public int DrenchHits;

            /// <summary>Second of the last drench hit. Unit: seconds of simulated time. Range: 0 upward.</summary>
            public Fix64 LastDrench;

            /// <summary>Shield dome pool left. Unit: health points. Range: 0 to the dome's pool; 0 without a dome.</summary>
            public Fix64 Pool;

            /// <summary>True while the dome is down (it comes up when full again).</summary>
            public bool Down;

            /// <summary>True once the dome has absorbed something.</summary>
            public bool HasAbsorbed;

            /// <summary>Second the dome last absorbed. Unit: seconds of simulated time. Range: 0 upward.</summary>
            public Fix64 LastAbsorb;

            /// <summary>Most extra units one blast of this unit reaches besides its target. Unit: units. Range: 0 upward.</summary>
            public int SplashExtra;

            /// <summary>Value of the unit (cost common + cost rare). Unit: resource units. Range: 0 upward.</summary>
            public int Value;

            /// <summary>The layer the unit counts as now: Ground while Grounded, else its definition's layer.</summary>
            public MovementLayer Layer => Grounded ? MovementLayer.Ground : Def.Layer;
        }

        /// <summary>
        /// Predicts the fight of side A against side B by simulation (fight_predictor_v2.md 3). Entries must be in ordinal definition-ID order; entries with a count of 0 or less are ignored.
        /// </summary>
        /// <param name="a">Side A.</param>
        /// <param name="b">Side B.</param>
        /// <param name="c">Uphill, water and starting gap.</param>
        /// <param name="rules">The game data (damage table and tuning).</param>
        /// <param name="s">The predictor numbers; <see cref="PredictorSettings.Model"/> and the Sim values are read.</param>
        public static FightPrediction Predict(IReadOnlyList<FightEntry> a, IReadOnlyList<FightEntry> b, in FightContext c, FightSimRules rules, PredictorSettings s)
        {
            // Units per entry (rounded count, health each) for both sides, scaled down when the larger side is over the cap (3.1).
            List<int> countsA = new List<int>();
            List<int> countsB = new List<int>();
            List<Fix64> healthA = new List<Fix64>();
            List<Fix64> healthB = new List<Fix64>();
            List<FightEntry> entriesA = Expand(a, countsA, healthA);
            List<FightEntry> entriesB = Expand(b, countsB, healthB);
            // Units in each side before scaling, and the larger one. Unit: units.
            int totalA = Sum(countsA);
            int totalB = Sum(countsB);
            int larger = Math.Max(totalA, totalB);
            if (larger > s.SimMaxUnitsPerSide)
            {
                Scale(countsA, s.SimMaxUnitsPerSide, larger);
                Scale(countsB, s.SimMaxUnitsPerSide, larger);
            }
            if (Sum(countsA) == 0)
            {
                // Nothing on side A: it never wins; the times are as the formula gives them (an empty B as well makes both the shortest time).
                return new FightPrediction(Sum(countsB) == 0 ? s.MinSeconds : s.MaxSeconds, s.MinSeconds, false, Fix64.Zero);
            }
            if (Sum(countsB) == 0)
            {
                return new FightPrediction(s.MinSeconds, s.MaxSeconds, true, s.MaxAdvantage);
            }
            // The two sides' units in entry order, then unit number (the index that breaks every tie).
            List<SimUnit> sideA = Build(entriesA, countsA, healthA, 0, rules, s);
            List<SimUnit> sideB = Build(entriesB, countsB, healthB, 1, rules, s);
            // Starting gap between the sides. Unit: tiles. Range: 0 upward.
            Fix64 gap = c.GapTiles >= Fix64.Zero ? c.GapTiles : LongestRange(sideA, sideB) + s.SimApproachTiles;
            foreach (SimUnit unit in sideB)
            {
                unit.X = gap;
            }
            // Value at the start of each side (the denominator of the value left). Unit: resource units. Range: 1 upward.
            Fix64 startA = StartValue(sideA);
            Fix64 startB = StartValue(sideB);
            // Result of the run: winner side (-1 none) and the end time. Unit: side index; seconds.
            Lane lane = new Lane(sideA, sideB, c, rules, s);
            lane.Run();
            if (lane.Winner < 0)
            {
                return new FightPrediction(s.MaxSeconds, s.MaxSeconds, false, Fix64.One);
            }
            // Value left of the winner over its start value (3.3). Unit: fraction. Range: 0 to 1.
            Fix64 f = lane.Winner == 0 ? ValueLeft(sideA) / startA : ValueLeft(sideB) / startB;
            // One minus f squared: the share of strength the loser's force ratio leaves out. Unit: fraction. Range: 0 to 1.
            Fix64 rest = Fix64.One - f * f;
            // Seconds the winner needed, at least the shortest time. Unit: seconds. Range: predictor_min_seconds upward.
            Fix64 seconds = Fix64.Max(s.MinSeconds, lane.Time);
            if (lane.Winner == 0)
            {
                // Advantage 1 / (1 - f^2), capped (a rest at or below 1 / cap is the cap itself).
                Fix64 advantage = rest * s.MaxAdvantage <= Fix64.One ? s.MaxAdvantage : Fix64.Min(s.MaxAdvantage, Fix64.One / rest);
                return new FightPrediction(seconds, Fix64.Min(s.MaxSeconds, seconds * advantage), true, advantage);
            }
            // B wins: advantage 1 - f^2 and A's time to kill B scaled the other way (the longest time when the advantage is 0).
            return new FightPrediction(rest <= Fix64.Zero ? s.MaxSeconds : Fix64.Min(s.MaxSeconds, seconds / rest), seconds, false, rest);
        }

        /// <summary>The entries with units (count rounded half up to at least 1): the entries kept, with their unit counts and the health of each unit (3.1).</summary>
        private static List<FightEntry> Expand(IReadOnlyList<FightEntry> entries, List<int> counts, List<Fix64> health)
        {
            List<FightEntry> kept = new List<FightEntry>();
            foreach (FightEntry entry in entries)
            {
                if (entry.Count <= Fix64.Zero)
                {
                    continue;
                }
                // Units in the entry: the count rounded half up. Unit: units. Range: 0 upward.
                int units = (entry.Count + Fix64.Half).FloorToInt();
                if (units < 1)
                {
                    continue;
                }
                // Health of each unit: the entry's health split evenly, at most the full health. Unit: health points. Range: 0 to MaxHealth.
                Fix64 each = Fix64.Min(entry.Definition.MaxHealth, entry.Health / entry.Count);
                if (each <= Fix64.Zero)
                {
                    continue;
                }
                kept.Add(entry);
                counts.Add(units);
                health.Add(each);
            }
            return kept;
        }

        /// <summary>Sum of a count list. Unit: units. Range: 0 upward.</summary>
        private static int Sum(List<int> counts)
        {
            // Running sum. Unit: units.
            int sum = 0;
            foreach (int count in counts)
            {
                sum += count;
            }
            return sum;
        }

        /// <summary>Multiplies every count by cap / larger, rounded half up, at least 1 (3.1).</summary>
        private static void Scale(List<int> counts, int cap, int larger)
        {
            for (int i = 0; i < counts.Count; i++)
            {
                // Scaled count in integers. Unit: units. Range: 1 upward.
                counts[i] = Math.Max(1, (int)(((long)counts[i] * cap * 2 + larger) / (2L * larger)));
            }
        }

        /// <summary>The units of one side, in entry order then unit number.</summary>
        private static List<SimUnit> Build(List<FightEntry> entries, List<int> counts, List<Fix64> health, int side, FightSimRules rules, PredictorSettings s)
        {
            List<SimUnit> units = new List<SimUnit>();
            for (int e = 0; e < entries.Count; e++)
            {
                UnitDefinition def = entries[e].Definition;
                for (int n = 0; n < counts[e]; n++)
                {
                    SimUnit unit = new SimUnit
                    {
                        Def = def,
                        Weapon = def.Weapon,
                        Side = side,
                        X = Fix64.Zero,
                        Health = health[e],
                        Alive = true,
                        Target = -1,
                        Momentum = rules.IsMomentumUnit(def.Id),
                        Pool = def.ShieldDome == null ? Fix64.Zero : def.ShieldDome.Pool,
                        Value = def.CostCommon + def.CostRare,
                    };
                    if (def.Weapon != null && def.Weapon.SplashRadiusTiles > Fix64.Zero)
                    {
                        // Units one blast reaches: floor(density x pi x r^2), the target among them. Unit: units. Range: 0 upward.
                        unit.SplashExtra = Math.Max(0, (s.SimSplashDensity * Pi * def.Weapon.SplashRadiusTiles * def.Weapon.SplashRadiusTiles).FloorToInt() - 1);
                    }
                    units.Add(unit);
                }
            }
            return units;
        }

        /// <summary>The longest weapon range of any unit of either side. Unit: tiles. Range: 0 upward.</summary>
        private static Fix64 LongestRange(List<SimUnit> a, List<SimUnit> b)
        {
            // Longest range so far. Unit: tiles.
            Fix64 longest = Fix64.Zero;
            foreach (SimUnit unit in a)
            {
                longest = unit.Weapon == null ? longest : Fix64.Max(longest, unit.Weapon.RangeTiles);
            }
            foreach (SimUnit unit in b)
            {
                longest = unit.Weapon == null ? longest : Fix64.Max(longest, unit.Weapon.RangeTiles);
            }
            return longest;
        }

        /// <summary>Summed value of a side's units at the start; when every unit is free, one per unit so the value left stays a fraction. Unit: resource units. Range: 1 upward.</summary>
        private static Fix64 StartValue(List<SimUnit> side)
        {
            // Sum of values. Unit: resource units.
            int sum = 0;
            foreach (SimUnit unit in side)
            {
                sum += unit.Value;
            }
            return Fix64.FromInt(sum > 0 ? sum : side.Count);
        }

        /// <summary>Value left of a side: the sum over its living units of value x health / MaxHealth (value 1 each when every unit is free). Unit: resource units. Range: 0 upward.</summary>
        private static Fix64 ValueLeft(List<SimUnit> side)
        {
            // True when no unit has a value, so the start value counts units instead.
            bool free = true;
            foreach (SimUnit unit in side)
            {
                free &= unit.Value == 0;
            }
            // Running sum. Unit: resource units.
            Fix64 left = Fix64.Zero;
            foreach (SimUnit unit in side)
            {
                if (unit.Alive)
                {
                    left += Fix64.FromInt(free ? 1 : unit.Value) * unit.Health / unit.Def.MaxHealth;
                }
            }
            return left;
        }

        /// <summary>One fight in the lane.</summary>
        private sealed class Lane
        {
            /// <summary>Both sides' units: index 0 is A, index 1 is B.</summary>
            private readonly List<SimUnit>[] _sides;

            /// <summary>Uphill, water and gap.</summary>
            private readonly FightContext _context;

            /// <summary>The game data.</summary>
            private readonly FightSimRules _rules;

            /// <summary>The settings.</summary>
            private readonly PredictorSettings _s;

            /// <summary>The winning side (0 or 1), or -1 for no winner. Unit: side index. Range: -1 to 1.</summary>
            public int Winner { get; private set; } = -1;

            /// <summary>Simulated time at the end of the run. Unit: seconds. Range: 0 upward.</summary>
            public Fix64 Time { get; private set; }

            /// <summary>Second of the last damage to any unit's health. Unit: seconds. Range: 0 upward.</summary>
            private Fix64 _lastDamage;

            /// <summary>Starts a lane.</summary>
            public Lane(List<SimUnit> a, List<SimUnit> b, FightContext context, FightSimRules rules, PredictorSettings s)
            {
                _sides = new[] { a, b };
                _context = context;
                _rules = rules;
                _s = s;
            }

            /// <summary>Runs steps until one side is gone, nothing can happen, nothing was damaged for the idle window or the time cap is reached (3.2 step 7).</summary>
            public void Run()
            {
                // Step length. Unit: seconds. Range: 0.05 to 1.
                Fix64 dt = _s.SimStepSeconds;
                // The start of the current step. Unit: seconds.
                Fix64 now = Fix64.Zero;
                while (true)
                {
                    Step(now, dt);
                    now += dt;
                    Time = now;
                    // Living units per side. Unit: units.
                    int alive0 = Living(_sides[0]);
                    int alive1 = Living(_sides[1]);
                    if (alive0 == 0 || alive1 == 0)
                    {
                        Winner = alive0 == 0 && alive1 == 0 ? -1 : alive0 == 0 ? 1 : 0;
                        return;
                    }
                    if (!CanHurt(0) && !CanHurt(1))
                    {
                        return;
                    }
                    if (now - _lastDamage >= _s.SimIdleSeconds || now >= _s.SimMaxSeconds)
                    {
                        return;
                    }
                }
            }

            /// <summary>Living units in a side. Unit: units.</summary>
            private static int Living(List<SimUnit> side)
            {
                // Count so far. Unit: units.
                int alive = 0;
                foreach (SimUnit unit in side)
                {
                    alive += unit.Alive ? 1 : 0;
                }
                return alive;
            }

            /// <summary>True when the weapon can hit a unit on <paramref name="layer"/>: its layer list, or flyers for a weapon that hits air from water when the lane has water.</summary>
            private bool CanHit(WeaponDefinition weapon, MovementLayer layer)
            {
                return weapon.CanTarget(layer) || (layer == MovementLayer.Air && weapon.CanTargetAirOnWater && _context.Water != FightWater.None);
            }

            /// <summary>True when some living unit of <paramref name="side"/> can hit some living enemy (by layer only).</summary>
            private bool CanHurt(int side)
            {
                // Which layers the enemy side has living units on, as a bit mask (bit per MovementLayer value).
                int layers = 0;
                foreach (SimUnit enemy in _sides[1 - side])
                {
                    layers |= enemy.Alive ? 1 << (int)enemy.Layer : 0;
                }
                foreach (SimUnit unit in _sides[side])
                {
                    if (!unit.Alive || unit.Weapon == null)
                    {
                        continue;
                    }
                    for (int layer = 0; layer < 3; layer++)
                    {
                        if ((layers & (1 << layer)) != 0 && CanHit(unit.Weapon, (MovementLayer)layer))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>One step of 3.2: statuses and domes, targets, shots, damage and movement, Momentum and healing, timers.</summary>
            private void Step(Fix64 now, Fix64 dt)
            {
                // 1. Statuses and domes.
                for (int side = 0; side < 2; side++)
                {
                    foreach (SimUnit unit in _sides[side])
                    {
                        if (unit.Alive)
                        {
                            Upkeep(unit, now, dt);
                        }
                    }
                }
                // 2. Targets from the state at the start of the step.
                for (int side = 0; side < 2; side++)
                {
                    foreach (SimUnit unit in _sides[side])
                    {
                        unit.Target = -1;
                        unit.InReach = false;
                        if (unit.Alive && unit.Weapon != null)
                        {
                            Acquire(unit);
                        }
                    }
                }
                // 3. Shots: side A first, then side B; the two sides never touch each other's domes (a side's domes absorb only the other side's shots), so the order carries no bias.
                for (int side = 0; side < 2; side++)
                {
                    foreach (SimUnit unit in _sides[side])
                    {
                        if (!unit.Alive || unit.Weapon == null || !unit.InReach)
                        {
                            continue;
                        }
                        // Fix64 guard against a zero cooldown: at least one epsilon, so the loop ends.
                        Fix64 cooldown = Fix64.Max(unit.Weapon.CooldownSeconds, Fix64.Epsilon);
                        while (unit.Timer <= Fix64.Zero)
                        {
                            Fire(unit, _sides[1 - side][unit.Target], now);
                            unit.Timer += cooldown;
                        }
                    }
                }
                // 4. Apply damage, then move (every new position from the old ones, committed together).
                for (int side = 0; side < 2; side++)
                {
                    foreach (SimUnit unit in _sides[side])
                    {
                        if (unit.Alive && unit.Pending > Fix64.Zero)
                        {
                            unit.Health -= unit.Pending;
                            _lastDamage = now;
                            unit.Alive = unit.Health > Fix64.Zero;
                        }
                        unit.Pending = Fix64.Zero;
                    }
                }
                for (int side = 0; side < 2; side++)
                {
                    foreach (SimUnit unit in _sides[side])
                    {
                        unit.NextX = unit.X;
                        unit.Moved = false;
                        if (unit.Alive && unit.Weapon != null && unit.Def.SpeedTilesPerSecond > Fix64.Zero && unit.Target >= 0 && !unit.InReach)
                        {
                            March(unit, now, dt);
                        }
                    }
                }
                for (int side = 0; side < 2; side++)
                {
                    foreach (SimUnit unit in _sides[side])
                    {
                        unit.X = unit.NextX;
                    }
                }
                // 5. Momentum and healing, 6. timers.
                for (int side = 0; side < 2; side++)
                {
                    foreach (SimUnit unit in _sides[side])
                    {
                        if (!unit.Alive)
                        {
                            continue;
                        }
                        if (unit.Momentum)
                        {
                            Momentum(unit, now, dt);
                        }
                        if (unit.Def.Layer == MovementLayer.Water && _context.Water != FightWater.None && _context.WaterSide == side)
                        {
                            unit.Health = Fix64.Min(unit.Def.MaxHealth, unit.Health + _rules.Flood.WaterUnitHealPerSecond * dt);
                        }
                        unit.Timer -= dt;
                    }
                }
            }

            /// <summary>Expires the unit's slow, Grounded and drench count and regenerates its dome (step 1).</summary>
            private void Upkeep(SimUnit unit, Fix64 now, Fix64 dt)
            {
                if (unit.Grounded && unit.GroundedUntil <= now)
                {
                    unit.Grounded = false;
                }
                if (unit.DrenchHits > 0 && now - unit.LastDrench >= _rules.Status.SpoutlingDrenchResetSeconds)
                {
                    unit.DrenchHits = 0;
                }
                ShieldDomeDefinition? dome = unit.Def.ShieldDome;
                if (dome != null && unit.Pool < dome.Pool && (!unit.HasAbsorbed || now - unit.LastAbsorb >= dome.RegenDelaySeconds))
                {
                    unit.Pool = Fix64.Min(dome.Pool, unit.Pool + dome.RegenPerSecond * dt);
                }
                if (dome != null && unit.Down && unit.Pool == dome.Pool)
                {
                    unit.Down = false;
                }
            }

            /// <summary>Finds the nearest living enemy the unit can hit (ties to the lower index) and whether it is in reach (step 2).</summary>
            private void Acquire(SimUnit unit)
            {
                // Best enemy index and its distance so far. Unit: index; tiles.
                int best = -1;
                Fix64 bestDistance = Fix64.Zero;
                List<SimUnit> enemies = _sides[1 - unit.Side];
                for (int i = 0; i < enemies.Count; i++)
                {
                    SimUnit enemy = enemies[i];
                    if (!enemy.Alive || !CanHit(unit.Weapon!, enemy.Layer))
                    {
                        continue;
                    }
                    // Distance along the lane. Unit: tiles.
                    Fix64 distance = (enemy.X - unit.X).Abs();
                    if (best < 0 || distance < bestDistance)
                    {
                        best = i;
                        bestDistance = distance;
                    }
                }
                unit.Target = best;
                unit.InReach = best >= 0 && bestDistance <= unit.Weapon!.RangeTiles + unit.Def.Radius + enemies[best].Def.Radius;
            }

            /// <summary>Moves the unit towards its nearest enemy, stopping where it comes in reach (step 4); the new position goes to NextX.</summary>
            private void March(SimUnit unit, Fix64 now, Fix64 dt)
            {
                List<SimUnit> enemies = _sides[1 - unit.Side];
                // The enemy to walk to: the start-of-step target, or the nearest living one if it died this step.
                SimUnit? goal = enemies[unit.Target].Alive ? enemies[unit.Target] : null;
                if (goal == null)
                {
                    // Nearest living enemy it can hit now. Unit: tiles.
                    Fix64 bestDistance = Fix64.Zero;
                    foreach (SimUnit enemy in enemies)
                    {
                        if (!enemy.Alive || !CanHit(unit.Weapon!, enemy.Layer))
                        {
                            continue;
                        }
                        Fix64 distance = (enemy.X - unit.X).Abs();
                        if (goal == null || distance < bestDistance)
                        {
                            goal = enemy;
                            bestDistance = distance;
                        }
                    }
                    if (goal == null)
                    {
                        return;
                    }
                }
                // Signed offset to the goal, the distance still to close before it is in reach, and the distance this step allows. Unit: tiles.
                Fix64 offset = goal.X - unit.X;
                Fix64 toClose = offset.Abs() - (unit.Weapon!.RangeTiles + unit.Def.Radius + goal.Def.Radius);
                if (toClose <= Fix64.Zero)
                {
                    return;
                }
                Fix64 step = Fix64.Min(toClose, unit.Def.SpeedTilesPerSecond * SpeedFactor(unit, now) * dt);
                if (step <= Fix64.Zero)
                {
                    return;
                }
                unit.NextX = offset > Fix64.Zero ? unit.X + step : unit.X - step;
                unit.Moved = true;
            }

            /// <summary>Speed multiplier of the unit: Momentum stacks, drench slow and the water of the lane (step 4). Unit: multiplier of speed. Range: above 0.</summary>
            private Fix64 SpeedFactor(SimUnit unit, Fix64 now)
            {
                // Product of the factors so far.
                Fix64 factor = Fix64.One;
                if (unit.Momentum)
                {
                    factor *= Fix64.One + Fix64.FromInt(unit.Stacks.FloorToInt()) * _rules.Momentum.SpeedBonusPerStack;
                }
                if (unit.SlowUntil > now)
                {
                    factor *= _rules.Status.DrenchSlowMultiplier;
                }
                if (_context.Water != FightWater.None)
                {
                    // True for shallow water, false for deep.
                    bool shallow = _context.Water == FightWater.Shallow;
                    if (unit.Layer == MovementLayer.Ground && _context.WaterSide != unit.Side)
                    {
                        factor *= shallow ? _rules.Flood.ShallowWaterEnemySpeedMultiplier : _rules.Flood.DeepWaterEnemySpeedMultiplier;
                    }
                    else if (unit.Layer == MovementLayer.Water)
                    {
                        factor *= shallow ? _rules.WaterUnitShallowSpeed : _rules.WaterUnitDeepSpeed;
                    }
                }
                return factor;
            }

            /// <summary>Applies the Momentum rules (step 5): stacks are lost when Grounded or slowed (as configured), gained while moving, drained after the still delay.</summary>
            private void Momentum(SimUnit unit, Fix64 now, Fix64 dt)
            {
                MomentumTuning m = _rules.Momentum;
                // True while the unit is slowed by a drench.
                bool slowed = unit.SlowUntil > now + dt;
                if ((m.LostOnGrounded && unit.Grounded) || (m.LostOnSlowed && slowed))
                {
                    unit.Stacks = Fix64.Zero;
                    unit.StillSeconds = Fix64.Zero;
                    return;
                }
                if (unit.Moved)
                {
                    unit.Stacks = Fix64.Min(Fix64.FromInt(m.MaxStacks), unit.Stacks + m.StackGainPerSecond * dt);
                    unit.StillSeconds = Fix64.Zero;
                    return;
                }
                unit.StillSeconds += dt;
                if (unit.StillSeconds >= m.DrainDelaySeconds)
                {
                    unit.Stacks = Fix64.Max(Fix64.Zero, unit.Stacks - m.DrainStacksPerSecond * dt);
                }
            }

            /// <summary>One shot of <paramref name="shooter"/> at <paramref name="target"/> (step 3): damage with the table, uphill and Momentum factors, domes, drench and splash.</summary>
            private void Fire(SimUnit shooter, SimUnit target, Fix64 now)
            {
                WeaponDefinition weapon = shooter.Weapon!;
                CombatRules table = _rules.Combat;
                // Damage multiplier of the shooter's Momentum stacks. Unit: multiplier. Range: 1 upward.
                Fix64 momentum = shooter.Momentum ? Fix64.One + Fix64.FromInt(shooter.Stacks.FloorToInt()) * _rules.Momentum.DamageBonusPerStack : Fix64.One;
                // Uphill factor, as FightPredictor.Dps: only A's ranged ground-or-water shots lose damage, by the damage table's multiplier.
                Fix64 uphill = _context.AUphill && shooter.Side == 0 && !weapon.IsMelee && shooter.Def.Layer != MovementLayer.Air && target.Def.Layer != MovementLayer.Air
                    ? table.UphillRangedDamageMultiplier : Fix64.One;
                // Damage to the target before domes. Unit: health points. Range: 0 upward.
                Fix64 damage = weapon.Damage * table.GetMultiplier(weapon.DamageType, target.Def.ArmourType) * uphill * momentum;
                if (!weapon.IsMelee && damage > Fix64.Zero)
                {
                    damage = Absorb(target, shooter.X, damage, now);
                    if (damage <= Fix64.Zero)
                    {
                        // The dome caught the whole shell: no hit, no blast.
                        return;
                    }
                }
                target.Pending += damage;
                if (weapon.AppliesDrench)
                {
                    Drench(target, now);
                }
                if (shooter.SplashExtra > 0)
                {
                    Blast(shooter, target, momentum, now);
                }
            }

            /// <summary>The blast of one shot: up to SplashExtra other living enemies within the radius of the target, nearest first (ties by index), each takes the splash fraction of the damage through its own armour and the domes.</summary>
            private void Blast(SimUnit shooter, SimUnit target, Fix64 momentum, Fix64 now)
            {
                WeaponDefinition weapon = shooter.Weapon!;
                List<SimUnit> enemies = _sides[1 - shooter.Side];
                // Indices of the victims in reach of the blast, then ordered nearest first.
                List<int> victims = new List<int>();
                for (int i = 0; i < enemies.Count; i++)
                {
                    SimUnit enemy = enemies[i];
                    if (enemy != target && enemy.Alive && CanHit(weapon, enemy.Layer) && (enemy.X - target.X).Abs() <= weapon.SplashRadiusTiles)
                    {
                        victims.Add(i);
                    }
                }
                victims.Sort((x, y) =>
                {
                    int byDistance = (enemies[x].X - target.X).Abs().CompareTo((enemies[y].X - target.X).Abs());
                    return byDistance != 0 ? byDistance : x.CompareTo(y);
                });
                // Damage before armour each blast victim takes. Unit: health points. Range: 0 upward.
                Fix64 splashBase = weapon.Damage * _rules.Combat.SplashDamageFraction * momentum;
                for (int k = 0; k < victims.Count && k < shooter.SplashExtra; k++)
                {
                    SimUnit victim = enemies[victims[k]];
                    // Damage after the victim's armour row and the domes (which see the blast from the target's position). Unit: health points.
                    Fix64 damage = splashBase * _rules.Combat.GetMultiplier(weapon.DamageType, victim.Def.ArmourType);
                    if (damage > Fix64.Zero)
                    {
                        damage = Absorb(victim, target.X, damage, now);
                    }
                    victim.Pending += damage;
                }
            }

            /// <summary>Lets the victim's side's domes absorb <paramref name="damage"/> (lowest index first): a dome absorbs when it is up, the victim is inside its radius and the source is outside it. Returns the damage left.</summary>
            private Fix64 Absorb(SimUnit victim, Fix64 sourceX, Fix64 damage, Fix64 now)
            {
                // Damage still to place. Unit: health points.
                Fix64 remaining = damage;
                foreach (SimUnit dome in _sides[victim.Side])
                {
                    if (remaining <= Fix64.Zero)
                    {
                        break;
                    }
                    if (!dome.Alive || dome.Down || dome.Def.ShieldDome == null || dome.Pool <= Fix64.Zero)
                    {
                        continue;
                    }
                    // The dome's radius. Unit: tiles.
                    Fix64 radius = dome.Def.ShieldDome.RadiusTiles;
                    if ((victim.X - dome.X).Abs() > radius || (sourceX - dome.X).Abs() <= radius)
                    {
                        continue;
                    }
                    // Damage this dome takes. Unit: health points.
                    Fix64 absorbed = Fix64.Min(remaining, dome.Pool);
                    remaining -= absorbed;
                    dome.Pool -= absorbed;
                    dome.HasAbsorbed = true;
                    dome.LastAbsorb = now;
                    dome.Down = dome.Pool == Fix64.Zero;
                }
                return remaining;
            }

            /// <summary>A drench hit on a flyer (statuses.md): slows it, counts the hit and grounds it at the threshold; ground and water units ignore it.</summary>
            private void Drench(SimUnit target, Fix64 now)
            {
                if (target.Layer != MovementLayer.Air)
                {
                    return;
                }
                StatusTuning status = _rules.Status;
                target.DrenchHits++;
                target.LastDrench = now;
                target.SlowUntil = now + status.DrenchSlowDurationSeconds;
                if (target.DrenchHits >= status.SpoutlingDrenchHitsToGround)
                {
                    target.Grounded = true;
                    target.GroundedUntil = now + status.GroundedDurationSeconds;
                    target.DrenchHits = 0;
                }
            }
        }
    }
}

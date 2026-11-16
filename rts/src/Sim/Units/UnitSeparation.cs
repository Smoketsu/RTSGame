// rts/src/Sim/Units/UnitSeparation.cs
// Step 5c of the tick (P-003 package B, specs/p003_rules.md B.2 and B.3): touching units push each other apart, moving units sidestep to
// the right when they walk into someone, and a crowd near a goal settles instead of jostling. Part of Rts.Sim.Units; called only by
// MovementSystem.Step after the per-unit movement loop. Every push is computed from a snapshot of positions and summed with exact
// Fix64 additions, so the result does not depend on the order pairs are visited or on the grid cell size. No state of its own:
// positions and move targets are already in the state hash. No floating point, no randomness.
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Terrain;

namespace Rts.Sim.Units
{
    /// <summary>Unit separation, sidestep and crowd arrival for one tick.</summary>
    internal static class UnitSeparation
    {
        /// <summary>True when any of the three separation rules is switched on in the tuning (otherwise the whole step is skipped).</summary>
        internal static bool IsActive(TerrainTuning tuning)
        {
            return tuning.SeparationStrength > Fix64.Zero || tuning.SidestepStrength > Fix64.Zero || tuning.CrowdArrivalRadiusTiles > Fix64.Zero;
        }

        /// <summary>Whether a unit takes part in separation (B.2): not a gathering worker, and not an air unit when air separation is off.</summary>
        private static bool IsParticipant(Unit unit, TerrainTuning tuning)
        {
            if (unit.GatherState != GatherState.Idle)
            {
                return false;
            }
            return tuning.AirSeparationEnabled || unit.EffectiveLayer != MovementLayer.Air;
        }

        /// <summary>Whether a unit may be moved by a push (B.2): it moves, is not an HQ and is not a building under construction.</summary>
        private static bool IsPushable(Unit unit)
        {
            return unit.Definition.SpeedTilesPerSecond > Fix64.Zero && unit.Definition.Hq == null && !unit.IsUnderConstruction;
        }

        /// <summary>Runs step 5c.</summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="all">Every unit in ascending ID order (a snapshot taken before step 5b).</param>
        /// <param name="start">Position of each unit in <paramref name="all"/> before step 5b. Unit: tiles. Same length as <paramref name="all"/>.</param>
        internal static void Run(Simulation sim, Unit[] all, FixVec2[] start)
        {
            // Tuning values read many times below.
            TerrainTuning tuning = sim.Tuning.Terrain;
            // Terrain for clamping and passability checks.
            TerrainGrid terrain = sim.Terrain;
            // Participants: indexes into all, ascending (so ascending unit ID).
            List<int> parts = new List<int>();
            for (int i = 0; i < all.Length; i++)
            {
                if (IsParticipant(all[i], tuning))
                {
                    parts.Add(i);
                }
            }
            // Number of participants. Unit: units. Range: 0 upward.
            int n = parts.Count;
            if (n < 2)
            {
                return;
            }
            // Snapshot positions after step 5b, per participant (index into parts). Unit: tiles.
            FixVec2[] pos = new FixVec2[n];
            // Whether each participant can be pushed.
            bool[] pushable = new bool[n];
            // Summed push on each participant. Unit: tiles. Range: bounded by overlaps, clamped later.
            FixVec2[] push = new FixVec2[n];
            // Position of each participant before step 5b (index into parts). Unit: tiles.
            FixVec2[] begin = new FixVec2[n];
            // Participants whose move ends this tick by crowd arrival.
            bool[] settled = new bool[n];
            // Largest radius per layer group (index = MovementLayer value 0 to 2). Unit: tiles.
            Fix64[] maxRadius = new Fix64[3];
            for (int k = 0; k < n; k++)
            {
                // Unit being snapshotted.
                Unit u = all[parts[k]];
                pos[k] = u.Position;
                begin[k] = start[parts[k]];
                pushable[k] = IsPushable(u);
                // Layer group of the unit, 0 to 2.
                int g = (int)u.EffectiveLayer;
                if (u.Definition.Radius > maxRadius[g])
                {
                    maxRadius[g] = u.Definition.Radius;
                }
            }
            // Cell side. Unit: tiles. Range: 1 to 16.
            int cell = tuning.SeparationCellTiles;
            // One sparse grid per layer group: cell key to participant indexes ascending. Only looked up by key, never iterated.
            Dictionary<long, List<int>>[] grids =
            {
                new Dictionary<long, List<int>>(), new Dictionary<long, List<int>>(), new Dictionary<long, List<int>>(),
            };
            for (int k = 0; k < n; k++)
            {
                // Key of the cell holding participant k, in its group's grid.
                long key = CellKey(pos[k].X.FloorToInt() / cell, pos[k].Y.FloorToInt() / cell);
                Dictionary<long, List<int>> grid = grids[(int)all[parts[k]].EffectiveLayer];
                if (!grid.TryGetValue(key, out List<int>? members))
                {
                    members = new List<int>();
                    grid[key] = members;
                }
                members.Add(k);
            }
            // Rule strengths, squared crowd radius.
            // Share of the overlap removed per tick by separation, from tuning. Unit: fraction per tick. Range: 0 to 1.
            Fix64 strength = tuning.SeparationStrength;
            // Sideways push as a share of the overlap per tick, from tuning. Unit: fraction per tick. Range: 0 to 2.
            Fix64 sidestep = tuning.SidestepStrength;
            // Crowd arrival radius from tuning, 0 when off. Unit: tiles. Range: 0 to 16.
            Fix64 crowdRadius = tuning.CrowdArrivalRadiusTiles;
            Fix64 crowdRadiusSquared = crowdRadius * crowdRadius;
            for (int a = 0; a < n; a++)
            {
                // Unit A, its group and the cells that can hold a touching neighbour.
                Unit ua = all[parts[a]];
                // Layer group of unit A, indexes grids and maxRadius. Unit: index. Range: 0 to 2 (MovementLayer value).
                int group = (int)ua.EffectiveLayer;
                // Distance from A within which a touching neighbour can lie: A's radius plus the largest radius in its group. Unit: tiles. Range: 0 upward.
                Fix64 reach = ua.Definition.Radius + maxRadius[group];
                // Lowest grid column the reach box touches, clamped at 0. Unit: cell coordinate. Range: 0 up to the last column.
                int cx0 = System.Math.Max((pos[a].X - reach).FloorToInt(), 0) / cell;
                // Highest grid column the reach box touches, clamped at 0. Unit: cell coordinate. Range: cx0 up to the last column.
                int cx1 = System.Math.Max((pos[a].X + reach).FloorToInt(), 0) / cell;
                // Lowest grid row the reach box touches, clamped at 0. Unit: cell coordinate. Range: 0 up to the last row.
                int cy0 = System.Math.Max((pos[a].Y - reach).FloorToInt(), 0) / cell;
                // Highest grid row the reach box touches, clamped at 0. Unit: cell coordinate. Range: cy0 up to the last row.
                int cy1 = System.Math.Max((pos[a].Y + reach).FloorToInt(), 0) / cell;
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    for (int cx = cx0; cx <= cx1; cx++)
                    {
                        if (!grids[group].TryGetValue(CellKey(cx, cy), out List<int>? members))
                        {
                            continue;
                        }
                        foreach (int b in members)
                        {
                            // Each pair once: the neighbour must have the higher ID.
                            if (b > a)
                            {
                                Pair(sim, all, parts, begin, pos, pushable, push, settled, a, b, strength, sidestep, crowdRadiusSquared);
                            }
                        }
                    }
                }
            }
            // Max push length and its square. Unit: tiles per tick.
            Fix64 max = tuning.SeparationMaxPushTilesPerTick;
            Fix64 maxSquared = max * max;
            for (int k = 0; k < n; k++)
            {
                if (!pushable[k] || (push[k].X == Fix64.Zero && push[k].Y == Fix64.Zero))
                {
                    continue;
                }
                // Push clamped to the per-tick maximum.
                FixVec2 p = push[k];
                // Squared length of the summed push. Unit: squared tiles. Range: 0 upward.
                Fix64 l2 = p.LengthSquared();
                if (l2 > maxSquared)
                {
                    p = p * (max / Fix64.Sqrt(l2));
                }
                // Unit object for participant k. Unit: none (reference). Range: non-null.
                Unit u = all[parts[k]];
                // Movement layer of unit k, used for terrain checks. Unit: enum value. Range: Ground 0, Water 1, Air 2.
                MovementLayer layer = u.EffectiveLayer;
                // Wanted position, inside the map.
                FixVec2 from = pos[k];
                FixVec2 to = terrain.ClampToMap(from + p);
                if (layer != MovementLayer.Air)
                {
                    to = TerrainChecked(terrain, from, to, layer);
                }
                if (to != from)
                {
                    u.SetPosition(to);
                }
            }
            for (int k = 0; k < n; k++)
            {
                if (settled[k])
                {
                    Unit u = all[parts[k]];
                    u.ClearMoveTarget();
                    sim.Events.Add(new UnitArrivedEvent(sim.Clock.Tick, u.Id, u.Position));
                }
            }
        }

        /// <summary>The wanted position if the step is allowed, else the same move on one axis, else the old position (B.3 step 4).</summary>
        private static FixVec2 TerrainChecked(TerrainGrid terrain, FixVec2 from, FixVec2 to, MovementLayer layer)
        {
            // Tile before the push.
            // Tile index of the start position, from TileOf. Unit: index. Range: 0 to Width * Height - 1.
            int a = terrain.TileOf(from);
            if (Allowed(terrain, a, terrain.TileOf(to), layer))
            {
                return to;
            }
            // X-only and Y-only alternatives.
            FixVec2 xOnly = new FixVec2(to.X, from.Y);
            if (Allowed(terrain, a, terrain.TileOf(xOnly), layer))
            {
                return xOnly;
            }
            // Position with only the Y move applied (X stays at the start). Unit: tiles. Range: inside the map.
            FixVec2 yOnly = new FixVec2(from.X, to.Y);
            return Allowed(terrain, a, terrain.TileOf(yOnly), layer) ? yOnly : from;
        }

        /// <summary>True when a unit may be on tile <paramref name="b"/> after standing on <paramref name="a"/>.</summary>
        private static bool Allowed(TerrainGrid terrain, int a, int b, MovementLayer layer)
        {
            return a == b || terrain.IsStepPassable(a, b, layer);
        }

        // Keys are unique while cell coordinates stay within 0 to 2^20, which holds because positions are clamped to the map.
        /// <summary>Key of a grid cell. Unit: none. Range: cell coordinates 0 to 2^20 - 1 each.</summary>
        private static long CellKey(int cx, int cy)
        {
            return ((long)cy << 20) | (uint)cx;
        }

        /// <summary>Adds the push, sidestep and crowd contributions of the pair (a, b), a below b, to the accumulators.</summary>
        private static void Pair(Simulation sim, Unit[] all, List<int> parts, FixVec2[] start, FixVec2[] pos,
            bool[] pushable, FixVec2[] push, bool[] settled, int a, int b, Fix64 strength, Fix64 sidestep, Fix64 crowdRadiusSquared)
        {
            if (!pushable[a] && !pushable[b])
            {
                return;
            }
            Unit ua = all[parts[a]];
            Unit ub = all[parts[b]];
            // Offset from B to A and touching distance. Unit: tiles.
            FixVec2 d = pos[a] - pos[b];
            Fix64 m = ua.Definition.Radius + ub.Definition.Radius;
            if (d.X >= m || d.X <= -m || d.Y >= m || d.Y <= -m)
            {
                return;
            }
            // Squared distance between A and B. Unit: squared tiles. Range: 0 up to m * m (exclusive).
            Fix64 s = d.LengthSquared();
            if (s >= m * m)
            {
                return;
            }
            // Distance between A and B. Unit: tiles. Range: 0 up to m (exclusive).
            Fix64 len = Fix64.Sqrt(s);
            // Unit vector from B to A; coincident units: A goes west, B east.
            FixVec2 dir = len == Fix64.Zero ? new FixVec2(-Fix64.One, Fix64.Zero) : new FixVec2(d.X / len, d.Y / len);
            // Amount the two radii overlap, m - len. Unit: tiles. Range: above 0 up to m.
            Fix64 overlap = m - len;
            // Push magnitude for the pair: overlap times separation strength. Unit: tiles. Range: 0 up to overlap.
            Fix64 c = overlap * strength;
            if (pushable[a] && pushable[b])
            {
                // Share of c each pushable unit receives when both can move. Unit: tiles. Range: 0 up to c / 2.
                Fix64 half = c / 2;
                push[a] = push[a] + dir * half;
                push[b] = push[b] - dir * half;
            }
            else if (pushable[a])
            {
                push[a] = push[a] + dir * c;
            }
            else
            {
                push[b] = push[b] - dir * c;
            }
            if (sidestep > Fix64.Zero)
            {
                // Sidestep push length for each unit of the pair: overlap times sidestep strength, halved. Unit: tiles. Range: 0 up to overlap.
                Fix64 amount = overlap * sidestep / 2;
                Sidestep(start, pos, pushable, push, a, dir, -Fix64.One, amount);
                Sidestep(start, pos, pushable, push, b, dir, Fix64.One, amount);
            }
            if (crowdRadiusSquared > Fix64.Zero)
            {
                Crowd(sim, ua, ub, a, b, pos, settled, crowdRadiusSquared);
                Crowd(sim, ub, ua, b, a, pos, settled, crowdRadiusSquared);
            }
        }

        /// <summary>
        /// Keep-right push for participant x: if its step this tick points toward the other unit (the dot product with
        /// <paramref name="sign"/> times <paramref name="dir"/> is positive), it is pushed to its right-hand side.
        /// </summary>
        private static void Sidestep(FixVec2[] start, FixVec2[] pos, bool[] pushable, FixVec2[] push, int x, FixVec2 dir, Fix64 sign, Fix64 amount)
        {
            if (!pushable[x])
            {
                return;
            }
            // Distance walked this tick (start is indexed by participant). Unit: tiles.
            FixVec2 step = pos[x] - start[x];
            if (step.X == Fix64.Zero && step.Y == Fix64.Zero)
            {
                return;
            }
            // Direction toward the other unit: -dir for A (sign -1), +dir for B (sign +1).
            // Unit direction toward the other unit (components). Unit: fraction. Range: -1 to 1 per component.
            FixVec2 toward = dir * sign;
            if (step.X * toward.X + step.Y * toward.Y <= Fix64.Zero)
            {
                return;
            }
            // Length of this tick's step. Unit: tiles. Range: 0 upward; 0 for a step under 2^-16 tiles, whose square truncates to zero.
            Fix64 stepLength = step.Length();
            if (stepLength == Fix64.Zero)
            {
                return; // too short to have a direction; dividing by it would throw
            }
            // Unit vector of this tick's step (components). Unit: fraction. Range: -1 to 1 per component.
            FixVec2 u = new FixVec2(step.X / stepLength, step.Y / stepLength);
            push[x] = push[x] + new FixVec2(u.Y, -u.X) * amount;
        }

        /// <summary>Marks mover as settled when it has a goal within the crowd radius and touches a goal-less unit nearer that goal (B.3 crowd flag).</summary>
        private static void Crowd(Simulation sim, Unit mover, Unit other, int mi, int oi, FixVec2[] pos, bool[] settled, Fix64 radiusSquared)
        {
            // Only a stopped, pushable unit counts: a building, HQ or post in the way must not end a move short of its goal.
            if (!mover.HasMoveTarget || other.HasMoveTarget || !IsPushable(other))
            {
                return;
            }
            // Move target of the mover, clamped to the map. Unit: tiles. Range: inside the map.
            FixVec2 goal = sim.Terrain.ClampToMap(mover.MoveTarget);
            // Squared distance from the mover to its goal. Unit: squared tiles. Range: 0 upward.
            Fix64 mine = (goal - pos[mi]).LengthSquared();
            if (mine <= radiusSquared && (goal - pos[oi]).LengthSquared() < mine)
            {
                settled[mi] = true;
            }
        }
    }
}

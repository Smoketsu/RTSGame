// rts/src/Ai/AiIntel.cs
// The pure helpers of skirmish_ai_v3.md package B (know the enemy): the enemy model (B.3: what enemy army unit types the AI has
// seen, how much that sighting still counts, what known enemy structures imply, and the known and air supply derived from both)
// and the scouting route (B.2 step 5). Part of Rts.Ai. Every function reads only the AiWorldView (own units, the team's vision,
// public map data), the profile and the AiMemory, so the AI never uses a fogged unit; it uses Fix64 and integers only, walks
// collections in sorted order (SortedDictionary / SortedSet, ascending ID or index), draws no random number and keeps no static state.
// AiBrain calls UpdateModel once per think from its intel rule and the scouting rule from its Turn.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>Enemy model and scouting route helpers (skirmish_ai_v3.md B.2 and B.3). Stateless: all state is in <see cref="AiMemory"/>.</summary>
    internal static class AiIntel
    {
        /// <summary>True for a unit type that counts as army: mobile (footprint 0), not a worker, not an HQ.</summary>
        /// <param name="definition">The unit type.</param>
        internal static bool IsArmyType(UnitDefinition definition)
        {
            return definition.Economy.FootprintTiles <= 0 && !definition.Economy.CanGather && definition.Hq == null;
        }

        /// <summary>
        /// How much a sighting that is <paramref name="age"/> ticks old still counts: 1 up to enemy_model_hold_seconds, then a linear fade
        /// to enemy_model_floor_fraction over enemy_model_decay_seconds, and the floor afterwards.
        /// </summary>
        /// <param name="view">The view (for the tick rate).</param>
        /// <param name="profile">The profile (hold, decay and floor).</param>
        /// <param name="age">Ticks since the type was last confirmed. Unit: ticks. Range: 0 upward.</param>
        /// <returns>The factor. Unit: fraction. Range: enemy_model_floor_fraction to 1.</returns>
        internal static Fix64 DecayFactor(AiWorldView view, AiProfile profile, int age)
        {
            // Hold time in ticks. Unit: ticks. Range: 0 upward.
            int hold = view.SecondsToTicks(profile.EnemyModelHoldSeconds);
            // Decay time in ticks. Unit: ticks. Range: 1 upward.
            int decay = Math.Max(1, view.SecondsToTicks(profile.EnemyModelDecaySeconds));
            // Floor share. Unit: fraction. Range: 0 to 1.
            Fix64 floor = profile.EnemyModelFloorFraction;
            if (age <= hold)
            {
                return Fix64.One;
            }
            if (age >= hold + decay)
            {
                return floor;
            }
            return Fix64.One - (Fix64.One - floor) * Fix64.FromInt(age - hold) / Fix64.FromInt(decay);
        }

        /// <summary>Visible enemy army units by definition ID (ordinal). Unit: units.</summary>
        private static SortedDictionary<string, int> VisibleByType(AiWorldView view)
        {
            // Count per type. Unit: units. Range: 0 to the visible enemy count.
            SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (AiUnitView enemy in view.VisibleEnemies)
            {
                if (enemy.IsArmy)
                {
                    counts.TryGetValue(enemy.Definition.Id, out int before);
                    counts[enemy.Definition.Id] = before + 1;
                }
            }
            return counts;
        }

        /// <summary>
        /// Updates the enemy model for this think (B.3): subtracts the kills reported since the last think that were visible last think,
        /// confirms every type whose visible count reaches its decayed count, then replaces the last-visible list and clears the kills.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The memory; updated.</param>
        internal static void UpdateModel(AiWorldView view, AiProfile profile, AiMemory memory)
        {
            // Kills, ascending ID, each counted once.
            SortedSet<EntityId> kills = new SortedSet<EntityId>(memory.PendingEnemyKills);
            foreach (EntityId id in kills)
            {
                if (memory.LastVisibleEnemyTypes.TryGetValue(id, out string? type) && memory.EnemyTypes.TryGetValue(type, out EnemyTypeEstimate? estimate))
                {
                    estimate.Count = Fix64.Max(Fix64.Zero, estimate.Count - Fix64.One);
                }
            }
            // Visible enemy army units per type now.
            SortedDictionary<string, int> visible = VisibleByType(view);
            // Every type seen now or tracked, ordinal order.
            SortedSet<string> types = new SortedSet<string>(visible.Keys, StringComparer.Ordinal);
            types.UnionWith(memory.EnemyTypes.Keys);
            foreach (string type in types)
            {
                visible.TryGetValue(type, out int visibleNow);
                if (!memory.EnemyTypes.TryGetValue(type, out EnemyTypeEstimate? estimate))
                {
                    estimate = new EnemyTypeEstimate { LastSeenTick = view.Tick };
                    memory.EnemyTypes.Add(type, estimate);
                }
                // The decayed count the sighting is worth now. Unit: units.
                Fix64 decayed = estimate.Count * DecayFactor(view, profile, view.Tick - estimate.LastSeenTick);
                if (Fix64.FromInt(visibleNow) >= decayed)
                {
                    estimate.Count = Fix64.FromInt(visibleNow);
                    estimate.LastSeenTick = view.Tick;
                }
            }
            memory.LastVisibleEnemyTypes.Clear();
            foreach (AiUnitView enemy in view.VisibleEnemies)
            {
                if (enemy.IsArmy)
                {
                    memory.LastVisibleEnemyTypes[enemy.Id] = enemy.Definition.Id;
                }
            }
            memory.PendingEnemyKills.Clear();
            if (memory.ModelAirKnownTick < 0 && AirSupply(view, profile, memory) > Fix64.Zero)
            {
                memory.ModelAirKnownTick = view.Tick;
            }
        }

        /// <summary>
        /// The model count M(t) per enemy army type (B.3): the larger of the effective count (visible now, or the remembered count x its
        /// decay factor) and the count implied by known enemy structures that train the type. Types with a count of 0 are left out.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The memory (read only).</param>
        /// <returns>Count per definition ID in ordinal order. Unit: units. Range: above 0.</returns>
        internal static SortedDictionary<string, Fix64> ModelCounts(AiWorldView view, AiProfile profile, AiMemory memory)
        {
            // M(t) being built. Unit: units.
            SortedDictionary<string, Fix64> counts = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
            // Visible enemy army units per type now.
            SortedDictionary<string, int> visible = VisibleByType(view);
            foreach (KeyValuePair<string, EnemyTypeEstimate> pair in memory.EnemyTypes)
            {
                visible.TryGetValue(pair.Key, out int visibleNow);
                // Effective count: the larger of the visible count and the decayed remembered count. Unit: units.
                Fix64 effective = Fix64.Max(Fix64.FromInt(visibleNow), pair.Value.Count * DecayFactor(view, profile, view.Tick - pair.Value.LastSeenTick));
                counts[pair.Key] = effective;
            }
            foreach (KeyValuePair<string, int> pair in visible)
            {
                if (!counts.ContainsKey(pair.Key))
                {
                    counts[pair.Key] = Fix64.FromInt(pair.Value);
                }
            }
            // Count implied for each type a known structure trains. Unit: units.
            Fix64 implied = Fix64.FromInt(profile.EnemyModelImpliedCount);
            foreach (KnownStructure known in memory.KnownEnemyStructures.Values)
            {
                if (!view.Content.Units.TryGetValue(known.DefinitionId, out UnitDefinition? structure))
                {
                    continue;
                }
                foreach (string trained in structure.Economy.Trains)
                {
                    if (view.Content.Units.TryGetValue(trained, out UnitDefinition? unit) && IsArmyType(unit))
                    {
                        counts.TryGetValue(trained, out Fix64 before);
                        counts[trained] = Fix64.Max(before, implied);
                    }
                }
            }
            // Types with nothing believed present are dropped.
            List<string> empty = new List<string>();
            foreach (KeyValuePair<string, Fix64> pair in counts)
            {
                if (pair.Value <= Fix64.Zero)
                {
                    empty.Add(pair.Key);
                }
            }
            foreach (string type in empty)
            {
                counts.Remove(type);
            }
            return counts;
        }

        /// <summary>Known enemy army supply S_E: the sum of M(t) x supply(t) over every type (B.3). Unit: supply. Range: 0 upward.</summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The memory (read only).</param>
        internal static Fix64 KnownSupply(AiWorldView view, AiProfile profile, AiMemory memory)
        {
            return SupplyWhere(view, profile, memory, false);
        }

        /// <summary>Known enemy air supply: <see cref="KnownSupply"/> over the types whose layer is Air. Unit: supply. Range: 0 upward.</summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The memory (read only).</param>
        internal static Fix64 AirSupply(AiWorldView view, AiProfile profile, AiMemory memory)
        {
            return SupplyWhere(view, profile, memory, true);
        }

        /// <summary>Sums M(t) x supply(t), over air types only when <paramref name="airOnly"/>.</summary>
        private static Fix64 SupplyWhere(AiWorldView view, AiProfile profile, AiMemory memory, bool airOnly)
        {
            // Supply total so far. Unit: supply.
            Fix64 total = Fix64.Zero;
            foreach (KeyValuePair<string, Fix64> pair in ModelCounts(view, profile, memory))
            {
                if (!view.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition) || (airOnly && definition.Layer != MovementLayer.Air))
                {
                    continue;
                }
                total += pair.Value * Fix64.FromInt(view.SupplyOf(definition));
            }
            return total;
        }

        /// <summary>
        /// The scouting route (B.2 step 5), public data only: every spawn except the own, nearest the HQ first (ties to the lower
        /// index); then every field farther than field_max_distance_tiles from the own spawn and every own drop-off, nearest the first
        /// waypoint first (ties to the lower index), skipping a field within scout_merge_tiles of an earlier waypoint; at most
        /// scout_route_max_waypoints.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The memory (own spawn index; read only).</param>
        /// <param name="hq">The own HQ position. Unit: tiles.</param>
        internal static List<FixVec2> BuildRoute(AiWorldView view, AiProfile profile, AiMemory memory, FixVec2 hq)
        {
            // Waypoints so far. Unit: tiles.
            List<FixVec2> route = new List<FixVec2>();
            // Enemy spawn indices, nearest the HQ first.
            List<int> spawns = new List<int>();
            // Spawn index. Range: 0 to spawn count - 1.
            for (int i = 0; i < view.Spawns.Count; i++)
            {
                if (i != memory.OwnSpawnIndex)
                {
                    spawns.Add(i);
                }
            }
            spawns.Sort((a, b) =>
            {
                // Squared distances of the two spawns from the HQ. Unit: squared tiles.
                int byDistance = (view.Spawns[a] - hq).LengthSquared().CompareTo((view.Spawns[b] - hq).LengthSquared());
                return byDistance != 0 ? byDistance : a.CompareTo(b);
            });
            foreach (int index in spawns)
            {
                route.Add(view.Spawns[index]);
            }
            // Where the field distances are measured from: the first waypoint, else the HQ. Unit: tiles.
            FixVec2 origin = route.Count > 0 ? route[0] : hq;
            // Squared reach of a drop-off. Unit: squared tiles.
            Fix64 reachSquared = profile.FieldMaxDistanceTiles * profile.FieldMaxDistanceTiles;
            // Squared merge distance. Unit: squared tiles.
            Fix64 mergeSquared = profile.ScoutMergeTiles * profile.ScoutMergeTiles;
            // Fields far from every own base, nearest the origin first.
            List<AiFieldView> fields = new List<AiFieldView>();
            foreach (AiFieldView field in view.Fields)
            {
                if (!IsNearOwnBase(view, memory, field.Centre, reachSquared))
                {
                    fields.Add(field);
                }
            }
            fields.Sort((a, b) =>
            {
                // Squared distances of the two fields from the origin. Unit: squared tiles.
                int byDistance = (a.Centre - origin).LengthSquared().CompareTo((b.Centre - origin).LengthSquared());
                return byDistance != 0 ? byDistance : a.Index.CompareTo(b.Index);
            });
            foreach (AiFieldView field in fields)
            {
                // True when an earlier waypoint is within the merge distance.
                bool merged = false;
                foreach (FixVec2 waypoint in route)
                {
                    merged |= (waypoint - field.Centre).LengthSquared() <= mergeSquared;
                }
                if (!merged)
                {
                    route.Add(field.Centre);
                }
            }
            if (route.Count > profile.ScoutRouteMaxWaypoints)
            {
                route.RemoveRange(profile.ScoutRouteMaxWaypoints, route.Count - profile.ScoutRouteMaxWaypoints);
            }
            return route;
        }

        /// <summary>True when <paramref name="point"/> is within the squared reach of the own spawn or of an own HQ or drop-off structure.</summary>
        private static bool IsNearOwnBase(AiWorldView view, AiMemory memory, FixVec2 point, Fix64 reachSquared)
        {
            if (memory.OwnSpawnIndex >= 0 && (view.Spawns[memory.OwnSpawnIndex] - point).LengthSquared() <= reachSquared)
            {
                return true;
            }
            foreach (AiUnitView unit in view.OwnUnits)
            {
                if ((unit.IsHq || (unit.IsStructure && unit.Definition.Economy.IsDropOff)) && (unit.Position - point).LengthSquared() <= reachSquared)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

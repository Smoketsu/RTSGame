// rts/src/Sim/Economy/EconomyRules.cs
// Shared economy queries used by the commands and by EconomySystem: a unit's supply cost, drop-off lookup, build and train
// times in ticks, and where a unit may stand or appear. Part of Rts.Sim.Economy. Pure functions of the simulation state
// (no mutation, no randomness); every scan walks units in ascending ID order and breaks ties toward the lowest ID.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Economy
{
    /// <summary>Stateless helpers for the economy.</summary>
    public static class EconomyRules
    {
        /// <summary>
        /// Supply a unit of this type costs. A static defence costs static_defence_supply_cost (tuning/supply.json) whatever its
        /// own supply field says; everything else costs its definition's supply. Unit: supply. Range: 0 to 100.
        /// </summary>
        /// <param name="sim">The simulation (for the tuning).</param>
        /// <param name="definition">The unit type.</param>
        public static int SupplyOf(Simulation sim, UnitDefinition definition)
        {
            return definition.Economy.IsStaticDefence ? sim.Tuning.Supply.StaticDefenceSupplyCost : definition.Supply;
        }

        /// <summary>
        /// The first entry of <c>definition.Economy.Requires</c>, in file order, that <paramref name="owner"/> does not meet, or null when all are
        /// met or the list is empty (walls_and_tech.md T.1). A requirement is met when the owner has a registered, complete (not under
        /// construction), living unit of that definition; allies' and enemies' buildings never count. One scan of the units per entry.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="owner">The player who wants to train or build.</param>
        /// <param name="definition">The unit type to train or build.</param>
        public static string? FirstMissingRequirement(Simulation sim, PlayerId owner, UnitDefinition definition)
        {
            // The requirement list, in file order; an index loop avoids an enumerator allocation.
            IReadOnlyList<string> requires = definition.Economy.Requires;
            for (int r = 0; r < requires.Count; r++)
            {
                // Definition ID of the r-th requirement.
                string required = requires[r];
                // True once a complete, living building of the owner with this definition is found.
                bool met = false;
                foreach (Unit unit in sim.Units.All)
                {
                    if (unit.Owner == owner && !unit.IsUnderConstruction && unit.Health > Fix64.Zero && string.Equals(unit.Definition.Id, required, StringComparison.Ordinal))
                    {
                        met = true;
                        break;
                    }
                }
                if (!met)
                {
                    return required;
                }
            }
            return null;
        }

        /// <summary>
        /// Time to train or construct a unit type in whole ticks: build_seconds converted with the match clock, at least 1.
        /// Unit: ticks. Range: 1 upward.
        /// </summary>
        /// <param name="sim">The simulation (for the clock).</param>
        /// <param name="definition">The unit type.</param>
        public static int BuildTicks(Simulation sim, UnitDefinition definition)
        {
            return Math.Max(1, sim.Clock.SecondsToTicks(definition.Economy.BuildSeconds));
        }

        /// <summary>
        /// True when workers of the owner may deposit at this unit now: a complete structure with is_drop_off, or an HQ while
        /// Rooted. The owner must be a real player. Resources are never shared between allies, so only the owner's own
        /// drop-offs count.
        /// </summary>
        /// <param name="unit">Any unit.</param>
        public static bool IsDropOff(Unit unit)
        {
            if (!unit.Owner.IsValid || unit.IsUnderConstruction)
            {
                return false;
            }
            return unit.Definition.Economy.IsDropOff || (unit.Definition.Hq != null && unit.HqState == HqState.Rooted);
        }

        /// <summary>
        /// The owner's drop-off nearest to <paramref name="from"/> by centre distance, ties to the lowest unit ID, or null when
        /// the owner has none. Scans all units: O(n) per call, fine for Phase 1 sizes.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="owner">The player whose drop-offs are searched.</param>
        /// <param name="from">Where the worker or building site is. Unit: tiles.</param>
        public static Unit? NearestDropOff(Simulation sim, PlayerId owner, FixVec2 from)
        {
            // Best drop-off so far and its squared distance. Unit: squared tiles. Range: 0 or more.
            Unit? best = null;
            Fix64 bestSquared = Fix64.Zero;
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner != owner || !IsDropOff(unit))
                {
                    continue;
                }
                // Squared centre distance to this candidate. Unit: squared tiles.
                Fix64 squared = (unit.Position - from).LengthSquared();
                if (best == null || squared < bestSquared)
                {
                    best = unit;
                    bestSquared = squared;
                }
            }
            return best;
        }

        /// <summary>
        /// A point where a unit on <paramref name="layer"/> can stand close to <paramref name="point"/>: the point itself when its
        /// tile is standable, otherwise the centre of the first standable ring-1 neighbour tile in TerrainGrid direction order
        /// (N, E, S, W, NE, SE, SW, NW). Falls back to the point when every neighbour is blocked.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="point">Wanted point. Unit: tiles.</param>
        /// <param name="layer">The walking unit's movement layer.</param>
        public static FixVec2 StandablePointNear(Simulation sim, FixVec2 point, MovementLayer layer)
        {
            TerrainGrid terrain = sim.Terrain;
            // Tile containing the wanted point.
            int tile = terrain.TileOf(point);
            if (terrain.IsStandable(tile, layer))
            {
                return point;
            }
            // Direction number into the neighbour tables. Unit: direction index. Range: 0 to 7.
            for (int direction = 0; direction < TerrainGrid.DirectionCount; direction++)
            {
                // Neighbour tile index in that direction, or -1 off the map.
                int neighbour = terrain.Neighbour(tile, direction);
                if (neighbour >= 0 && terrain.IsStandable(neighbour, layer))
                {
                    return terrain.CentreOf(neighbour);
                }
            }
            return point;
        }

        /// <summary>
        /// Finds where a new unit appears next to its producer: the first tile in ascending (row, column) order, searching rings 1 to spawn_search_radius_tiles (tuning/economy.json)
        /// around the producer's tile, that is standable for the new unit's layer and, for ground and water units, on the producer's
        /// height layer (so a unit never appears across a cliff). Air units accept any tile. Returns the tile centre.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="producerPosition">Position of the producer. Unit: tiles.</param>
        /// <param name="layer">Movement layer of the unit being spawned.</param>
        /// <param name="position">The chosen tile centre when found. Unit: tiles.</param>
        public static bool TryFindSpawnPosition(Simulation sim, FixVec2 producerPosition, MovementLayer layer, out FixVec2 position)
        {
            TerrainGrid terrain = sim.Terrain;
            // The producer's tile and its coordinates.
            int origin = terrain.TileOf(producerPosition);
            int ox = terrain.XOf(origin);
            int oy = terrain.YOf(origin);
            // Outermost ring searched (spawn_search_radius_tiles). Unit: tiles. Range: 1 to 8.
            int maxRing = sim.Tuning.Economy.SpawnSearchRadiusTiles;
            // Ring distance from the producer's tile. Unit: tiles. Range: 1 to maxRing.
            for (int ring = 1; ring <= maxRing; ring++)
            {
                // Row offset from the producer's tile. Unit: tiles. Range: -ring to ring.
                for (int dy = -ring; dy <= ring; dy++)
                {
                    // Column offset from the producer's tile. Unit: tiles. Range: -ring to ring.
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring || !terrain.Contains(ox + dx, oy + dy))
                        {
                            continue;
                        }
                        // Candidate tile index.
                        int candidate = terrain.IndexOf(ox + dx, oy + dy);
                        if (!terrain.IsStandable(candidate, layer))
                        {
                            continue;
                        }
                        if (layer != MovementLayer.Air && terrain[candidate].Height != terrain[origin].Height)
                        {
                            continue;
                        }
                        position = terrain.CentreOf(candidate);
                        return true;
                    }
                }
            }
            position = producerPosition;
            return false;
        }
    }
}

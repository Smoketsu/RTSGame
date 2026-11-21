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
        /// Static check of one tile of a dragged wall line (walls_and_tech.md D.2 rule 2), returning why the tile is left out or null when it may be
        /// built: out_of_bounds, site_unexplored (the worker owner's team has never seen it), tile_blocked (any blocker on it), site_pending
        /// (another own unit, not <paramref name="worker"/>, has an active order or a queued tile there). A unit standing on the tile is not a problem here
        /// (units move; the arrival and placement checks handle it). Unity calls this for the ghost preview; duplicate_tile is added by the command.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="worker">The worker that would build the tile (its own orders are about to be replaced, so they do not count).</param>
        /// <param name="tileX">Tile X. Unit: tiles. Range: any int.</param>
        /// <param name="tileY">Tile Y. Unit: tiles. Range: any int.</param>
        public static string? WallLineTileProblem(Simulation sim, Unit worker, int tileX, int tileY)
        {
            if (!sim.Terrain.Contains(tileX, tileY))
            {
                return "out_of_bounds";
            }
            if (!sim.Vision.IsTileExplored(sim.Players.TeamOf(worker.Owner), tileX, tileY))
            {
                return "site_unexplored";
            }
            // Index of the tile. Unit: tile index. Range: 0 to tile count - 1.
            int tile = sim.Terrain.IndexOf(tileX, tileY);
            if (sim.Terrain[tile].IsBlocked)
            {
                return "tile_blocked";
            }
            foreach (Unit other in sim.Units.All)
            {
                if (other.Owner == worker.Owner && other.Id != worker.Id
                    && ((other.BuildState != BuildOrderState.None && other.BuildTileX == tileX && other.BuildTileY == tileY) || other.QueuesTile(tile)))
                {
                    return "site_pending";
                }
            }
            return null;
        }

        /// <summary>
        /// The first entry of <c>definition.Economy.Requires</c>, in file order, that <paramref name="owner"/> does not meet, or null when all are
        /// met or the list is empty (walls_and_tech.md T.1). A requirement is met when the owner has a registered, complete (not under
        /// construction), living unit of that definition, or, for a research ID, has finished that research; allies' and enemies' buildings never count. One scan of the units per entry.
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
                if (sim.Content.Researches.ContainsKey(required))
                {
                    // A research item is met when the owner has finished it (own research only).
                    met = sim.Players.TryGet(owner, out Player? player) && player.HasResearched(required);
                }
                foreach (Unit unit in sim.Units.All)
                {
                    if (met)
                    {
                        break;
                    }
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
        /// True when a unit owned by <paramref name="owner"/> has the research item anywhere in its production queue. An existence test over
        /// all units, so order cannot change the result (research_and_unlocks.md R1.5).
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="owner">The player whose producers are searched.</param>
        /// <param name="researchId">Research ID, such as "skai.thunderhead_plans".</param>
        public static bool IsResearchQueued(Simulation sim, PlayerId owner, string researchId)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == owner && System.Linq.Enumerable.Contains(unit.ProductionQueue, researchId))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Length in ticks of a production queue entry: <see cref="BuildTicks"/> for a unit ID, max(1, research_seconds in ticks) for a research ID,
        /// 0 for an unknown ID. Unit: ticks. Range: 0 upward.
        /// </summary>
        /// <param name="sim">The simulation (for the catalog and clock).</param>
        /// <param name="definitionId">Unit or research ID.</param>
        public static int QueueEntryTicks(Simulation sim, string definitionId)
        {
            if (sim.Content.Units.TryGetValue(definitionId, out UnitDefinition? unit))
            {
                return BuildTicks(sim, unit);
            }
            if (sim.Content.Researches.TryGetValue(definitionId, out ResearchDefinition? research))
            {
                return Math.Max(1, sim.Clock.SecondsToTicks(research.ResearchSeconds));
            }
            return 0;
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

        /// <summary>
        /// Why a structure cannot be placed on a tile for a player, or null when it can (specs/interface_support.md I1). Reasons, first match wins,
        /// in the order the command always used: unknown_definition, not_a_structure, wrong_faction, missing_prerequisite, out_of_bounds, site_unexplored, tile_blocked,
        /// tile_occupied, site_pending, no_base and static_defence_cap (static defences only), insufficient_resources, supply_cap (reads the stored supply figures; never
        /// recomputes them). Pure: it changes nothing. With <paramref name="fogSafe"/> and a tile the owner's team cannot see now, tile_blocked is reported only for map
        /// walls and tile_occupied counts only the team's own units, so the answer reveals nothing hidden (the command passes false and keeps its old checks).
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="owner">The player who would pay and own the structure; must have a record.</param>
        /// <param name="definitionId">Definition ID of the structure; any text.</param>
        /// <param name="tileX">Tile X of the site. Unit: tiles. Range: any int (out of range gives out_of_bounds).</param>
        /// <param name="tileY">Tile Y of the site. Unit: tiles. Range: any int (out of range gives out_of_bounds).</param>
        /// <param name="fogSafe">True for the ghost query, false for the command.</param>
        internal static string? BuildSiteProblem(Simulation sim, Player owner, string definitionId, int tileX, int tileY, bool fogSafe)
        {
            if (!sim.Content.Units.TryGetValue(definitionId, out UnitDefinition? definition))
            {
                return "unknown_definition";
            }
            if (definition.Economy.FootprintTiles <= 0)
            {
                return "not_a_structure";
            }
            if (!string.Equals(definition.FactionId, owner.FactionId, StringComparison.Ordinal))
            {
                return "wrong_faction";
            }
            if (FirstMissingRequirement(sim, owner.Id, definition) != null)
            {
                return "missing_prerequisite";
            }
            if (!sim.Terrain.Contains(tileX, tileY))
            {
                return "out_of_bounds";
            }
            // Site tile index. Range: 0 to tile count - 1.
            int tile = sim.Terrain.IndexOf(tileX, tileY);
            // Centre of the site tile. Unit: tiles (fixed-point).
            FixVec2 centre = sim.Terrain.CentreOf(tile);
            // The owner's team.
            TeamId team = sim.Players.TeamOf(owner.Id);
            if (!sim.Vision.IsTileExplored(team, tileX, tileY))
            {
                return "site_unexplored";
            }
            // True when the answer must not use what the team cannot see now.
            bool hidden = fogSafe && !sim.Vision.IsTileVisible(team, tileX, tileY);
            // Blockers that count: all of them, or only map walls on a hidden tile.
            TileBlockers blockers = hidden ? sim.Terrain[tile].Blockers & TileBlockers.Wall : sim.Terrain[tile].Blockers;
            if (blockers != TileBlockers.None)
            {
                return "tile_blocked";
            }
            foreach (Unit other in sim.Units.All)
            {
                if (other.EffectiveLayer != MovementLayer.Air && sim.Terrain.TileOf(other.Position) == tile && (!hidden || sim.Players.TeamOf(other.Owner) == team))
                {
                    return "tile_occupied";
                }
            }
            foreach (Unit other in sim.Units.All)
            {
                if (other.Owner == owner.Id && ((other.BuildState != BuildOrderState.None && other.BuildTileX == tileX && other.BuildTileY == tileY) || other.QueuesTile(tile)))
                {
                    return "site_pending";
                }
            }
            if (definition.Economy.IsStaticDefence)
            {
                // The static defence problem, if any.
                string? defenceProblem = StaticDefenceProblem(sim, owner.Id, centre);
                if (defenceProblem != null)
                {
                    return defenceProblem;
                }
            }
            if (!owner.CanAfford(definition.CostCommon, definition.CostRare))
            {
                return "insufficient_resources";
            }
            if (owner.SupplyUsed + SupplyOf(sim, definition) > owner.SupplyCap)
            {
                return "supply_cap";
            }
            return null;
        }

        /// <summary>
        /// Placement query for Unity's ghost (specs/interface_support.md I1): why <paramref name="player"/> cannot place the structure on the tile, or null when it can.
        /// cannot_build when the player has no record; otherwise <see cref="BuildSiteProblem"/> with fog-safe answers. The stored supply figures are those of the last step,
        /// so it can say supply_cap where the command (which recomputes them) accepts. Works between steps; mutates nothing.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="player">The player who would build. Must be a player of the match, else cannot_build.</param>
        /// <param name="definitionId">Definition ID of the structure; any text.</param>
        /// <param name="tileX">Tile X. Unit: tiles. Range: any int.</param>
        /// <param name="tileY">Tile Y. Unit: tiles. Range: any int.</param>
        public static string? PlacementProblem(Simulation sim, PlayerId player, string definitionId, int tileX, int tileY)
        {
            return sim.Players.TryGet(player, out Player? owner) ? BuildSiteProblem(sim, owner, definitionId ?? string.Empty, tileX, tileY, true) : "cannot_build";
        }

        /// <summary>
        /// Checks the per-base static defence limit for a site. Returns null when the site is fine, otherwise no_base when the owner has no drop-off within
        /// base_radius_tiles of the site, or static_defence_cap when that drop-off's base already holds static_defences_per_base_max of the owner's static defences
        /// (under construction ones and pending build orders for static defences sited in that base count).
        /// </summary>
        private static string? StaticDefenceProblem(Simulation sim, PlayerId owner, FixVec2 site)
        {
            // Squared base radius. Unit: squared tiles.
            Fix64 radius = sim.Tuning.Economy.BaseRadiusTiles;
            Fix64 radiusSquared = radius * radius;
            Unit? dropOff = NearestDropOff(sim, owner, site);
            if (dropOff == null || (dropOff.Position - site).LengthSquared() > radiusSquared)
            {
                return "no_base";
            }
            // Static defences the owner already has in that base. Unit: count.
            int count = 0;
            foreach (Unit other in sim.Units.All)
            {
                if (other.Owner == owner && other.Definition.Economy.IsStaticDefence && (other.Position - dropOff.Position).LengthSquared() <= radiusSquared)
                {
                    count++;
                }
                // Definition of another worker's pending structure (the out value is unused without an order).
                if (other.Owner == owner && other.BuildState != BuildOrderState.None
                    && sim.Content.Units.TryGetValue(other.BuildDefinitionId, out UnitDefinition? pending) && pending.Economy.IsStaticDefence
                    && (sim.Terrain.CentreOf(sim.Terrain.IndexOf(other.BuildTileX, other.BuildTileY)) - dropOff.Position).LengthSquared() <= radiusSquared)
                {
                    count++;
                }
            }
            return count >= sim.Tuning.Supply.StaticDefencesPerBaseMax ? "static_defence_cap" : null;
        }
    }
}

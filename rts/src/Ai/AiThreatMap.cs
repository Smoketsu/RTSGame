// rts/src/Ai/AiThreatMap.cs
// The threat map of rts/docs/specs/ai_threat_map.md section 3 (package S-MAP): a coarse grid in the skirmish AI's memory holding its own strength and its
// remembered enemy strength, each fading with distance. Part of Rts.Ai. Built once per think, right after the intel step, and only when the profile has
// use_threat_map true; no rule reads it yet (rules TS and TH are later packages), so it changes no decision. Fog: enemy strength comes only from
// AiWorldView.VisibleEnemies (sightings kept in AiMemory.ThreatSightings) and AiMemory.KnownEnemyStructures; own strength from AiWorldView.OwnUnits.
// Determinism: integers and Fix64 only (floor divisions with long products), sources walked in ascending entity ID and cells in ascending index, no random draw,
// nothing here is hashed.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>The layers, sightings and size of the threat grid after one think, copied for tests (see <see cref="AiBrain.ProbeThreat"/>).</summary>
    internal sealed class ThreatProbe
    {
        /// <summary>Grid columns. Unit: cells. Range: 1 to 1024.</summary>
        public int Columns { get; }

        /// <summary>Grid rows. Unit: cells. Range: 1 to 1024.</summary>
        public int Rows { get; }

        /// <summary>Copy of the enemy layer, row x columns + column. Unit: resource units. Range: 0 upward each.</summary>
        public int[] Enemy { get; }

        /// <summary>Copy of the own layer, same layout. Unit: resource units. Range: 0 upward each.</summary>
        public int[] Own { get; }

        /// <summary>Copy of the sightings kept, by entity ID: position (tiles), strength when seen (resource units) and tick seen (ticks).</summary>
        public SortedDictionary<EntityId, (FixVec2 Position, int Strength, int Tick)> Sightings { get; }

        /// <summary>Creates the probe result.</summary>
        /// <param name="columns">Grid columns. Unit: cells.</param>
        /// <param name="rows">Grid rows. Unit: cells.</param>
        /// <param name="enemy">Enemy layer copy. Unit: resource units.</param>
        /// <param name="own">Own layer copy. Unit: resource units.</param>
        /// <param name="sightings">Sightings copy.</param>
        public ThreatProbe(int columns, int rows, int[] enemy, int[] own, SortedDictionary<EntityId, (FixVec2 Position, int Strength, int Tick)> sightings)
        {
            Columns = columns;
            Rows = rows;
            Enemy = enemy;
            Own = own;
            Sightings = sightings;
        }
    }

    public static partial class AiBrain
    {
        /// <summary>
        /// Test hook (ai_threat_map.md 3.5): runs one think's intel step and the threat map update and returns the grid size, copies of both layers and of the sightings.
        /// Null when the player has no HQ. Changes only <paramref name="memory"/>.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The AI profile (use_threat_map should be true).</param>
        /// <param name="plan">The profile's plan for the player's faction (unused, kept for symmetry with the other probes).</param>
        /// <param name="memory">The controller's memory; updated.</param>
        internal static ThreatProbe? ProbeThreat(AiWorldView view, AiProfile profile, AiFactionPlan plan, AiMemory memory)
        {
            // The own HQ (lowest ID), or null when the player has none.
            AiUnitView? hq = FindHq(view);
            UpdateIntel(view, profile, memory, hq);
            AiThreatMap.Update(view, profile, memory);
            if (hq == null)
            {
                return null;
            }
            return new ThreatProbe(memory.ThreatColumns, memory.ThreatRows, (int[])memory.ThreatEnemy.Clone(), (int[])memory.ThreatOwn.Clone(),
                new SortedDictionary<EntityId, (FixVec2 Position, int Strength, int Tick)>(memory.ThreatSightings));
        }
    }

    /// <summary>Builds and reads the threat map. Stateless: all state is in <see cref="AiMemory"/>.</summary>
    internal static class AiThreatMap
    {
        /// <summary>Number of cells along one axis: ceil(tiles / cell side). Unit: cells. Range: 1 upward.</summary>
        /// <param name="tiles">Map width or height. Unit: tiles. Range: 1 upward.</param>
        /// <param name="cellTiles">Cell side. Unit: tiles. Range: 1 to 16.</param>
        internal static int CellCount(int tiles, int cellTiles)
        {
            return (tiles + cellTiles - 1) / cellTiles;
        }

        /// <summary>One axis of the cell of a point: floor(x / c) clamped to 0 to count - 1 (floor of the integer part equals the floor of the quotient).</summary>
        /// <param name="coordinate">A point coordinate. Unit: tiles (Fix64).</param>
        /// <param name="cellTiles">Cell side. Unit: tiles. Range: 1 to 16.</param>
        /// <param name="count">Cells along the axis. Unit: cells. Range: 1 upward.</param>
        internal static int Axis(Fix64 coordinate, int cellTiles, int count)
        {
            // Whole tile of the coordinate, negative below the map. Unit: tiles.
            int tile = coordinate.FloorToInt();
            if (tile < 0)
            {
                return 0;
            }
            // Cell along the axis before the upper clamp. Unit: cells.
            int cell = tile / cellTiles;
            return cell > count - 1 ? count - 1 : cell;
        }

        /// <summary>Cell index (row x columns + column) of a point on a grid of the given shape (ai_threat_map.md 3.1).</summary>
        /// <param name="point">A point. Unit: tiles.</param>
        /// <param name="cellTiles">Cell side. Unit: tiles. Range: 1 to 16.</param>
        /// <param name="columns">Grid columns. Unit: cells. Range: 1 upward.</param>
        /// <param name="rows">Grid rows. Unit: cells. Range: 1 upward.</param>
        internal static int CellOf(FixVec2 point, int cellTiles, int columns, int rows)
        {
            return Axis(point.Y, cellTiles, rows) * columns + Axis(point.X, cellTiles, columns);
        }

        /// <summary>Remembered enemy strength in the cell of <paramref name="point"/>; 0 before the first build. Unit: resource units. Range: 0 upward.</summary>
        /// <param name="memory">The memory holding the layers.</param>
        /// <param name="profile">The profile (cell side).</param>
        /// <param name="point">A point. Unit: tiles.</param>
        internal static int EnemyAt(AiMemory memory, AiProfile profile, FixVec2 point)
        {
            return memory.ThreatEnemy.Length == 0 ? 0 : memory.ThreatEnemy[CellOf(point, profile.ThreatCellTiles, memory.ThreatColumns, memory.ThreatRows)];
        }

        /// <summary>Own strength in the cell of <paramref name="point"/>; 0 before the first build. Unit: resource units. Range: 0 upward.</summary>
        /// <param name="memory">The memory holding the layers.</param>
        /// <param name="profile">The profile (cell side).</param>
        /// <param name="point">A point. Unit: tiles.</param>
        internal static int OwnAt(AiMemory memory, AiProfile profile, FixVec2 point)
        {
            return memory.ThreatOwn.Length == 0 ? 0 : memory.ThreatOwn[CellOf(point, profile.ThreatCellTiles, memory.ThreatColumns, memory.ThreatRows)];
        }

        /// <summary>
        /// Strength of one unit (3.2): floor(value x min(1, health / max health)), computed as value x health / max health. Unit: resource units. Range: 0 upward.
        /// </summary>
        /// <param name="definition">The unit's definition.</param>
        /// <param name="health">Current health. Unit: health points. Range: 0 upward.</param>
        /// <param name="profile">The profile (rare cost weight).</param>
        private static int Strength(UnitDefinition definition, Fix64 health, AiProfile profile)
        {
            // Health counted, capped at the maximum so a structure above its build health still counts as 1. Unit: health points.
            Fix64 counted = health > definition.MaxHealth ? definition.MaxHealth : health;
            if (counted <= Fix64.Zero || definition.MaxHealth <= Fix64.Zero)
            {
                return 0;
            }
            return (AiCounter.Value(definition, profile) * counted / definition.MaxHealth).FloorToInt();
        }

        /// <summary>
        /// Adds <paramref name="strength"/> at cell <paramref name="cell"/> and, fading by ring, to every cell within <paramref name="spread"/> rings (3.3): floor(s x (K + 1 - k) / (K + 1)).
        /// Cells off the grid are skipped. Rows and columns are walked in ascending order.
        /// </summary>
        /// <param name="layer">The layer to add to. Unit: resource units.</param>
        /// <param name="columns">Grid columns. Unit: cells.</param>
        /// <param name="rows">Grid rows. Unit: cells.</param>
        /// <param name="cell">Source cell index. Range: 0 to columns x rows - 1.</param>
        /// <param name="strength">Source strength. Unit: resource units. Range: 0 upward.</param>
        /// <param name="spread">K, the rings reached. Unit: cells. Range: 0 to 8.</param>
        private static void Splat(int[] layer, int columns, int rows, int cell, int strength, int spread)
        {
            if (strength <= 0)
            {
                return;
            }
            // Source column and row. Unit: cells.
            int qx = cell % columns;
            int qy = cell / columns;
            for (int y = Math.Max(0, qy - spread); y <= Math.Min(rows - 1, qy + spread); y++)
            {
                for (int x = Math.Max(0, qx - spread); x <= Math.Min(columns - 1, qx + spread); x++)
                {
                    // Ring distance of this cell from the source (Chebyshev). Unit: cells. Range: 0 to spread.
                    int ring = Math.Max(Math.Abs(x - qx), Math.Abs(y - qy));
                    layer[y * columns + x] += (int)((long)strength * (spread + 1 - ring) / (spread + 1));
                }
            }
        }

        /// <summary>
        /// The map update of one think (3.4): record sightings of visible armed army units, forget old or seen-gone ones, then rebuild both layers from zero.
        /// Called only with use_threat_map true.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The controller's memory; the threat members are updated.</param>
        internal static void Update(AiWorldView view, AiProfile profile, AiMemory memory)
        {
            // Now, and how long a sighting counts. Unit: ticks.
            int now = view.Tick;
            int memoryTicks = view.SecondsToTicks(profile.EnemyArmyMemorySeconds);
            // Step 1, seen now: IDs refreshed this think.
            SortedSet<EntityId> refreshed = new SortedSet<EntityId>();
            foreach (AiUnitView enemy in view.VisibleEnemies)
            {
                if (enemy.IsArmy && enemy.Definition.Weapon != null)
                {
                    memory.ThreatSightings[enemy.Id] = (enemy.Position, Strength(enemy.Definition, enemy.Health, profile), now);
                    refreshed.Add(enemy.Id);
                }
            }
            // Step 2, forget: sightings to remove, collected first.
            List<EntityId> removed = new List<EntityId>();
            foreach (KeyValuePair<EntityId, (FixVec2 Position, int Strength, int Tick)> pair in memory.ThreatSightings)
            {
                if (refreshed.Contains(pair.Key))
                {
                    continue;
                }
                if (now - pair.Value.Tick >= memoryTicks || view.IsVisible(pair.Value.Position.X.FloorToInt(), pair.Value.Position.Y.FloorToInt()))
                {
                    removed.Add(pair.Key);
                }
            }
            foreach (EntityId id in removed)
            {
                memory.ThreatSightings.Remove(id);
            }
            // Step 3, build: grid shape from the map size, then both layers from zero.
            // Side of one cell. Unit: tiles. Range: 1 to 16 (profile key threat_cell_tiles).
            int cellTiles = profile.ThreatCellTiles;
            // How far a source's strength reaches. Unit: cells (ring distance). Range: 0 to 8 (profile key threat_spread_cells).
            int spread = profile.ThreatSpreadCells;
            // Grid width. Unit: cells. Range: 1 to the map width (ceil of width over cellTiles).
            int columns = CellCount(view.Width, cellTiles);
            // Grid height. Unit: cells. Range: 1 to the map height (ceil of height over cellTiles).
            int rows = CellCount(view.Height, cellTiles);
            // Remembered enemy strength per cell, index row x columns + column. Unit: resource units. Range: 0 upward; columns x rows entries.
            int[] enemyLayer = new int[columns * rows];
            // Own strength per cell, same indexing as enemyLayer. Unit: resource units. Range: 0 upward; columns x rows entries.
            int[] ownLayer = new int[columns * rows];
            foreach (KeyValuePair<EntityId, (FixVec2 Position, int Strength, int Tick)> pair in memory.ThreatSightings)
            {
                // Age of the sighting. Unit: ticks. Range: 0 to memoryTicks - 1 (0 when memoryTicks is 0).
                int age = now - pair.Value.Tick;
                // Strength after ageing, s x (M - a) / M; unaged when M is 0 (only sightings seen this think survive then). Unit: resource units.
                int aged = memoryTicks <= 0 ? pair.Value.Strength : (int)((long)pair.Value.Strength * (memoryTicks - age) / memoryTicks);
                Splat(enemyLayer, columns, rows, CellOf(pair.Value.Position, cellTiles, columns, rows), aged, spread);
            }
            foreach (KeyValuePair<EntityId, KnownStructure> pair in memory.KnownEnemyStructures)
            {
                if (pair.Value.IsHq || !view.Content.Units.TryGetValue(pair.Value.DefinitionId, out UnitDefinition? definition) || definition.Weapon == null)
                {
                    continue;
                }
                // Strength of the structure as last seen: value x health share. Unit: resource units.
                int strength = (AiCounter.Value(definition, profile) * pair.Value.HealthFraction).FloorToInt();
                Splat(enemyLayer, columns, rows, CellOf(pair.Value.Position, cellTiles, columns, rows), strength, spread);
            }
            foreach (AiUnitView unit in view.OwnUnits)
            {
                if (unit.IsUnderConstruction || unit.Definition.Weapon == null || !(unit.IsArmy || (unit.IsStructure && unit.Definition.Economy.IsStaticDefence)))
                {
                    continue;
                }
                Splat(ownLayer, columns, rows, CellOf(unit.Position, cellTiles, columns, rows), Strength(unit.Definition, unit.Health, profile), spread);
            }
            memory.ThreatEnemy = enemyLayer;
            memory.ThreatOwn = ownLayer;
            memory.ThreatColumns = columns;
            memory.ThreatRows = rows;
            memory.ThreatTick = now;
        }
    }
}

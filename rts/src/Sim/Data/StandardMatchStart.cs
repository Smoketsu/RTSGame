// rts/src/Sim/Data/StandardMatchStart.cs
// The standard start of a skirmish match: each player's faction HQ on its map spawn point plus a few workers beside it, as
// script-issued spawn_unit commands at tick 0. Part of Rts.Sim.Data (spec: rts/docs/specs/balance_harness.md section 2). Pure: it
// reads the data set and the map and returns commands; the host submits them. The balance harness, the tests and the Unity skirmish
// lobby share it so every start looks the same. The number of workers is data (tuning/economy.json starting_worker_count).
using System;
using System.Collections.Generic;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Data
{
    /// <summary>Builds the tick-0 commands of a standard match start.</summary>
    public static class StandardMatchStart
    {
        /// <summary>Innermost Chebyshev ring around the spawn tile searched for worker tiles. Unit: tiles. Range: fixed schema bound, 1.</summary>
        private const int FirstRing = 1;

        /// <summary>
        /// For each player in ascending slot order: a <c>spawn_unit</c> of the faction's HQ unit at the map's spawn point for that slot
        /// (exactly as the map gives it), then <c>starting_worker_count</c> workers of the HQ's worker unit at the centres of the first
        /// tiles on Chebyshev rings 1 to starting_worker_search_radius_tiles (3 in the shipped data) around the spawn tile (ascending tile index per ring) that have the spawn tile's height, no
        /// ramp and no resource field. Commands are issued by <see cref="PlayerId.None"/> at tick 0 with sequences counting up from 0.
        /// </summary>
        /// <param name="data">The loaded data set (units, factions, tuning).</param>
        /// <param name="map">The map played on.</param>
        /// <param name="players">The match's players; any order, distinct slots.</param>
        /// <exception cref="InvalidOperationException">A player has no spawn point, an unknown faction or a faction without an HQ, or fewer than the wanted number of worker tiles exist; the message names the map and slot.</exception>
        public static List<Command> Commands(DataSet data, MapDefinition map, IReadOnlyList<PlayerSetup> players)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }
            // Players by slot so the commands come out in ascending slot order whatever order the host listed them in.
            SortedDictionary<PlayerId, PlayerSetup> bySlot = new SortedDictionary<PlayerId, PlayerSetup>();
            foreach (PlayerSetup setup in players)
            {
                if (!bySlot.TryAdd(setup.Id, setup))
                {
                    throw new InvalidOperationException("Map " + map.Id + ": player slot " + setup.Id.Value + " is listed twice.");
                }
            }
            // Workers each player starts with. Unit: workers. Range: 0 to 20.
            int workerCount = data.Tuning.Economy.StartingWorkerCount;
            // Outermost Chebyshev ring searched for worker tiles (tuning/economy.json starting_worker_search_radius_tiles). Unit: tiles. Range: 1 to 8.
            int lastRing = data.Tuning.Economy.StartingWorkerSearchRadiusTiles;
            // Commands built so far, in the order they are returned.
            List<Command> commands = new List<Command>();
            // Next script sequence number. Unit: counter. Range: 0 upward.
            uint sequence = 0;
            foreach (KeyValuePair<PlayerId, PlayerSetup> pair in bySlot)
            {
                // Slot of this player, which is also the index of its spawn point. Unit: slot number. Range: 0 to 254.
                int slot = pair.Key.Value;
                if (slot >= map.Spawns.Count)
                {
                    throw new InvalidOperationException("Map " + map.Id + " has no spawn point for slot " + slot + " (it has " + map.Spawns.Count + ").");
                }
                if (!data.Content.Factions.TryGetValue(pair.Value.FactionId, out Factions.FactionDefinition? faction) || faction.HqUnitId == null)
                {
                    throw new InvalidOperationException("Map " + map.Id + ", slot " + slot + ": faction '" + pair.Value.FactionId + "' has no HQ unit.");
                }
                // The HQ definition, whose hq object names the worker.
                UnitDefinition hq = data.Content.Units[faction.HqUnitId];
                if (hq.Hq == null)
                {
                    throw new InvalidOperationException("Map " + map.Id + ", slot " + slot + ": unit '" + hq.Id + "' is not an HQ.");
                }
                // Spawn point exactly as the map gives it. Unit: tiles.
                FixVec2 spawn = map.Spawns[slot];
                commands.Add(new SpawnUnitCommand(0, PlayerId.None, sequence++, hq.Id, pair.Key, spawn));
                // Worker tile centres, in ring then index order.
                List<FixVec2> sites = WorkerSites(map, spawn, workerCount, lastRing);
                if (sites.Count < workerCount)
                {
                    throw new InvalidOperationException("Map " + map.Id + ", slot " + slot + ": only " + sites.Count + " of " + workerCount
                        + " worker tiles exist within " + lastRing + " tiles of the spawn point.");
                }
                foreach (FixVec2 site in sites)
                {
                    commands.Add(new SpawnUnitCommand(0, PlayerId.None, sequence++, hq.Hq.WorkerUnitId, pair.Key, site));
                }
            }
            return commands;
        }

        /// <summary>
        /// The centres of up to <paramref name="count"/> tiles on rings 1 to <paramref name="lastRing"/> around the tile containing <paramref name="spawn"/>
        /// that are on the map, on the spawn tile's height layer, not a ramp and not a resource field. Fewer are returned when fewer exist.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="spawn">The spawn point. Unit: tiles.</param>
        /// <param name="count">Tiles wanted. Unit: tiles. Range: 0 to 20.</param>
        /// <param name="lastRing">Outermost ring searched. Unit: tiles. Range: 1 to 8.</param>
        private static List<FixVec2> WorkerSites(MapDefinition map, FixVec2 spawn, int count, int lastRing)
        {
            List<FixVec2> sites = new List<FixVec2>();
            // Spawn tile column and row, clamped onto the map like TerrainGrid.TileOf. Unit: tiles.
            int cx = Math.Min(Math.Max(spawn.X.FloorToInt(), 0), map.WidthTiles - 1);
            int cy = Math.Min(Math.Max(spawn.Y.FloorToInt(), 0), map.HeightTiles - 1);
            // Height layer of the spawn tile. Range: 0 to 7.
            byte height = map.HeightAt(cy * map.WidthTiles + cx);
            // Ring number (Chebyshev distance). Unit: tiles. Range: 1 to lastRing.
            for (int ring = FirstRing; ring <= lastRing && sites.Count < count; ring++)
            {
                // Row, then column: ascending tile index within the ring.
                for (int y = cy - ring; y <= cy + ring && sites.Count < count; y++)
                {
                    for (int x = cx - ring; x <= cx + ring && sites.Count < count; x++)
                    {
                        if (x < 0 || y < 0 || x >= map.WidthTiles || y >= map.HeightTiles || (Math.Abs(x - cx) != ring && Math.Abs(y - cy) != ring))
                        {
                            continue;
                        }
                        // Index of the candidate tile.
                        int index = y * map.WidthTiles + x;
                        if (map.HeightAt(index) != height || map.IsRampAt(index) || HasField(map, x, y))
                        {
                            continue;
                        }
                        sites.Add(new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half));
                    }
                }
            }
            return sites;
        }

        /// <summary>True when a resource field stands on tile (<paramref name="x"/>, <paramref name="y"/>).</summary>
        private static bool HasField(MapDefinition map, int x, int y)
        {
            foreach (ResourceField field in map.ResourceFields)
            {
                if (field.X == x && field.Y == y)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

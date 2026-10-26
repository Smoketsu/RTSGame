// rts/src/Sim/Data/DataSet.cs
// Everything DataLoader produces from rts/data: content catalog, typed tuning, maps and cycles.
// Part of Rts.Sim.Data. Hosts build a SimulationConfig from it (Content, CreateMatchSettings) and hand the
// Tuning to the systems that need it. Maps and cycles are exposed in ordinal ID order.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>The loaded, validated content of the data folder. Read-only after <see cref="DataLoader.Load"/>.</summary>
    public sealed class DataSet
    {
        /// <summary>Unit and faction definitions, ready for <see cref="SimulationConfig"/>.</summary>
        public ContentCatalog Content { get; }

        /// <summary>All tuning files, typed.</summary>
        public Tuning Tuning { get; }

        /// <summary>Maps by ID. Iteration is in ordinal ID order (backed by a SortedDictionary with StringComparer.Ordinal).</summary>
        public IReadOnlyDictionary<string, MapDefinition> Maps { get; }

        /// <summary>Planetary Cycle definitions by ID. Iteration is in ordinal ID order.</summary>
        public IReadOnlyDictionary<string, CycleDefinition> Cycles { get; }

        /// <summary>Bundles the loaded pieces. The loader is the normal caller; tests may build one by hand. The map and cycle dictionaries are copied into ordinal-ordered dictionaries so iteration order never depends on the caller's comparer.</summary>
        public DataSet(ContentCatalog content, Tuning tuning, IReadOnlyDictionary<string, MapDefinition> maps, IReadOnlyDictionary<string, CycleDefinition> cycles)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            if (maps == null)
            {
                throw new ArgumentNullException(nameof(maps));
            }
            if (cycles == null)
            {
                throw new ArgumentNullException(nameof(cycles));
            }
            // Ordinal copies: the only iteration order the simulation uses.
            SortedDictionary<string, MapDefinition> orderedMaps = new SortedDictionary<string, MapDefinition>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MapDefinition> pair in maps)
            {
                orderedMaps.Add(pair.Key, pair.Value);
            }
            // Cycle definitions copied in ordinal ID order. Range: one entry per cycle ID.
            SortedDictionary<string, CycleDefinition> orderedCycles = new SortedDictionary<string, CycleDefinition>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, CycleDefinition> pair in cycles)
            {
                orderedCycles.Add(pair.Key, pair.Value);
            }
            Maps = orderedMaps;
            Cycles = orderedCycles;
        }

        /// <summary>
        /// The map with <paramref name="id"/>, or the first map in ordinal ID order when <paramref name="id"/> is null.
        /// Throws <see cref="DataValidationException"/> naming the missing map, or when no map is loaded at all.
        /// </summary>
        /// <param name="id">Map ID or null.</param>
        public MapDefinition RequireMap(string? id)
        {
            if (id != null)
            {
                if (Maps.TryGetValue(id, out MapDefinition? named))
                {
                    return named;
                }
                throw new DataValidationException("maps/" + id + ".json", string.Empty, "is not a loaded map");
            }
            foreach (KeyValuePair<string, MapDefinition> pair in Maps)
            {
                return pair.Value;
            }
            throw new DataValidationException("maps/", string.Empty, "contains no map files");
        }

        /// <summary>Builds the core match settings from tuning/match.json (ticks_per_second and state_hash_interval_ticks).</summary>
        /// <param name="seed">Seed for the one simulation RNG; any ulong.</param>
        public MatchSettings CreateMatchSettings(ulong seed)
        {
            return new MatchSettings(seed, Tuning.Match.TicksPerSecond, Tuning.Match.StateHashIntervalTicks);
        }
    }
}

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

        /// <summary>Bundles the loaded pieces. The loader is the normal caller; tests may build one by hand.</summary>
        public DataSet(ContentCatalog content, Tuning tuning, SortedDictionary<string, MapDefinition> maps, SortedDictionary<string, CycleDefinition> cycles)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Maps = maps ?? throw new ArgumentNullException(nameof(maps));
            Cycles = cycles ?? throw new ArgumentNullException(nameof(cycles));
        }

        /// <summary>Builds the core match settings from tuning/match.json (ticks_per_second and state_hash_interval_ticks).</summary>
        /// <param name="seed">Seed for the one simulation RNG; any ulong.</param>
        public MatchSettings CreateMatchSettings(ulong seed)
        {
            return new MatchSettings(seed, Tuning.Match.TicksPerSecond, Tuning.Match.StateHashIntervalTicks);
        }
    }
}

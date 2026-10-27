// rts/src/Sim/Data/DataSet.cs
// Everything DataLoader produces from rts/data: content catalog, typed tuning, maps, cycles and the raw AI profile trees.
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

        /// <summary>
        /// Skirmish AI profile trees by profile ID (rts/data/ai/&lt;id&gt;.json), in ordinal ID order. The loader checked only the header
        /// and the id; Rts.Ai.AiProfile.Parse validates the rest. The simulation never reads these. Empty when the folder is absent.
        /// </summary>
        public IReadOnlyDictionary<string, JsonValue> AiProfiles { get; }

        /// <summary>Bundles the loaded pieces. The loader is the normal caller; tests may build one by hand. The map and cycle dictionaries are copied into ordinal-ordered dictionaries so iteration order never depends on the caller's comparer.</summary>
        public DataSet(ContentCatalog content, Tuning tuning, IReadOnlyDictionary<string, MapDefinition> maps, IReadOnlyDictionary<string, CycleDefinition> cycles)
            : this(content, tuning, maps, cycles, new SortedDictionary<string, JsonValue>(StringComparer.Ordinal))
        {
        }

        /// <summary>
        /// Bundles the loader's results including the skirmish AI profile trees (ai/&lt;id&gt;.json), copied into ordinal ID order.
        /// </summary>
        /// <param name="content">Unit and faction definitions.</param>
        /// <param name="tuning">Every tuning file.</param>
        /// <param name="maps">Maps by ID.</param>
        /// <param name="cycles">Planetary Cycles by ID.</param>
        /// <param name="aiProfiles">Parsed AI profile roots by profile ID; header and id already checked by the loader.</param>
        public DataSet(ContentCatalog content, Tuning tuning, IReadOnlyDictionary<string, MapDefinition> maps, IReadOnlyDictionary<string, CycleDefinition> cycles,
            IReadOnlyDictionary<string, JsonValue> aiProfiles)
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
            if (aiProfiles == null)
            {
                throw new ArgumentNullException(nameof(aiProfiles));
            }
            // AI profile roots copied in ordinal ID order. Range: one entry per ai/<id>.json file.
            SortedDictionary<string, JsonValue> orderedProfiles = new SortedDictionary<string, JsonValue>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in aiProfiles)
            {
                orderedProfiles.Add(pair.Key, pair.Value);
            }
            Maps = orderedMaps;
            Cycles = orderedCycles;
            AiProfiles = orderedProfiles;
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

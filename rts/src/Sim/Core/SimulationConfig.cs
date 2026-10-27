// rts/src/Sim/Core/SimulationConfig.cs
// Everything needed to construct a Simulation: settings, content, players, the mechanic registry, tuning, the map and the
// optional list of Planetary Cycles (null or empty switches Planetary Cycles off; Core capture still runs).
// Part of Rts.Sim.Core. Hosts build one of these (from data files and the lobby) and hand it over;
// the simulation keeps references and never modifies the inputs.
using System;
using System.Collections.Generic;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;

namespace Rts.Sim.Core
{
    /// <summary>Immutable bundle of match inputs.</summary>
    public sealed class SimulationConfig
    {
        /// <summary>Seed, tick rate and hash interval.</summary>
        public MatchSettings Settings { get; }

        /// <summary>Unit and faction definitions.</summary>
        public ContentCatalog Content { get; }

        /// <summary>One entry per player slot, any count.</summary>
        public IReadOnlyList<PlayerSetup> Players { get; }

        /// <summary>Registered faction mechanic plug-ins.</summary>
        public FactionMechanicRegistry Mechanics { get; }

        /// <summary>Every tuning file, loaded from data/tuning. Systems read the part they own; no tuning number lives in code.</summary>
        public Tuning Tuning { get; }

        /// <summary>The map to play on; the terrain grid is built from it at construction.</summary>
        public MapDefinition Map { get; }

        /// <summary>
        /// The Planetary Cycles in play, in ascending ordinal ID order, read-only. Empty means the match has no Planetary Cycles
        /// (hosts that want them pass <c>DataSet.Cycles.Values</c>). Range: zero or more cycles with distinct IDs; at least two
        /// distinct favoured play styles when not empty (checked by <see cref="Simulation.Start"/>).
        /// </summary>
        public IReadOnlyList<CycleDefinition> Cycles { get; }

        /// <summary>
        /// Bundles the inputs; null checks only, deeper validation happens in <see cref="Simulation.Start"/>.
        /// <paramref name="cycles"/> is optional: null means no Planetary Cycles. The list is copied and sorted by ordinal ID, so
        /// the order the host passes them in never matters. Duplicate IDs throw <see cref="ArgumentException"/>.
        /// </summary>
        public SimulationConfig(MatchSettings settings, ContentCatalog content, IReadOnlyList<PlayerSetup> players, FactionMechanicRegistry mechanics,
            Tuning tuning, MapDefinition map, IEnumerable<CycleDefinition>? cycles = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Players = players ?? throw new ArgumentNullException(nameof(players));
            Mechanics = mechanics ?? throw new ArgumentNullException(nameof(mechanics));
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            // Cycles sorted by ordinal ID. Range: zero or more entries with distinct IDs.
            SortedDictionary<string, CycleDefinition> sorted = new SortedDictionary<string, CycleDefinition>(StringComparer.Ordinal);
            if (cycles != null)
            {
                foreach (CycleDefinition cycle in cycles)
                {
                    if (cycle == null)
                    {
                        throw new ArgumentException("The cycle list contains a null entry.", nameof(cycles));
                    }
                    if (!sorted.TryAdd(cycle.Id, cycle))
                    {
                        throw new ArgumentException("The cycle list names '" + cycle.Id + "' twice.", nameof(cycles));
                    }
                }
            }
            // Snapshot of the sorted cycles, so later changes to the caller's collection cannot reach the match.
            List<CycleDefinition> ordered = new List<CycleDefinition>(sorted.Values);
            Cycles = ordered.AsReadOnly();
        }
    }
}

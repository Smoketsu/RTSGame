// rts/src/Sim/Core/SimulationConfig.cs
// Everything needed to construct a Simulation: settings, content, players and the mechanic registry.
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

        /// <summary>Bundles the inputs; null checks only, deeper validation happens in <see cref="Simulation.Start"/>.</summary>
        public SimulationConfig(MatchSettings settings, ContentCatalog content, IReadOnlyList<PlayerSetup> players, FactionMechanicRegistry mechanics,
            Tuning tuning, MapDefinition map)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Players = players ?? throw new ArgumentNullException(nameof(players));
            Mechanics = mechanics ?? throw new ArgumentNullException(nameof(mechanics));
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Map = map ?? throw new ArgumentNullException(nameof(map));
        }
    }
}

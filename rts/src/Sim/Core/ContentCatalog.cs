// rts/src/Sim/Core/ContentCatalog.cs
// Every data-defined definition a match can reference, keyed by stable ID in ordinal order.
// Part of Rts.Sim.Core. The data loader (Rts.Sim.Data) fills it from data/ files; tests may fill it in
// code. The simulation only reads it.
using System;
using System.Collections.Generic;
using Rts.Sim.Factions;
using Rts.Sim.Units;

namespace Rts.Sim.Core
{
    /// <summary>Unit and faction definitions for a match. Add-only; built before <see cref="Simulation.Start"/>.</summary>
    public sealed class ContentCatalog
    {
        /// <summary>Unit definitions by ID, ordinal order.</summary>
        private readonly SortedDictionary<string, UnitDefinition> _units = new SortedDictionary<string, UnitDefinition>(StringComparer.Ordinal);

        /// <summary>Faction definitions by ID, ordinal order.</summary>
        private readonly SortedDictionary<string, FactionDefinition> _factions = new SortedDictionary<string, FactionDefinition>(StringComparer.Ordinal);

        /// <summary>Unit definitions by ID. Iteration is in ordinal ID order.</summary>
        public IReadOnlyDictionary<string, UnitDefinition> Units => _units;

        /// <summary>Faction definitions by ID. Iteration is in ordinal ID order.</summary>
        public IReadOnlyDictionary<string, FactionDefinition> Factions => _factions;

        /// <summary>Adds a unit definition. A duplicate ID throws so two data files cannot silently collide.</summary>
        public void AddUnit(UnitDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (_units.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException("Duplicate unit definition id '" + definition.Id + "'.");
            }
            _units.Add(definition.Id, definition);
        }

        /// <summary>Adds a faction definition. A duplicate ID throws.</summary>
        public void AddFaction(FactionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (_factions.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException("Duplicate faction definition id '" + definition.Id + "'.");
            }
            _factions.Add(definition.Id, definition);
        }

        /// <summary>Checks every faction's unit list names a known unit. Returns the first problem or null.</summary>
        public string? Validate()
        {
            foreach (FactionDefinition faction in _factions.Values)
            {
                foreach (string unitId in faction.UnitIds)
                {
                    if (!_units.ContainsKey(unitId))
                    {
                        return "Faction '" + faction.Id + "' lists unknown unit '" + unitId + "'.";
                    }
                }
            }
            return null;
        }
    }
}

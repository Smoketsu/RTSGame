// rts/src/Sim/Factions/FactionDefinition.cs
// Immutable description of a faction from data/factions/*.json. Part of Rts.Sim.Factions.
// Names the mechanic plug-in by ID and lists the unit definitions the faction can field.
using System;
using System.Collections.Generic;

namespace Rts.Sim.Factions
{
    /// <summary>One faction as loaded from data. Never changes during a match.</summary>
    public sealed class FactionDefinition
    {
        /// <summary>Stable ID such as "skai" or "water" (ADR-003).</summary>
        public string Id { get; }

        /// <summary>Name shown to players. Presentation only.</summary>
        public string DisplayName { get; }

        /// <summary>ID of the signature mechanic plug-in, such as "momentum"; "none" means no mechanic (used by tests).</summary>
        public string MechanicId { get; }

        /// <summary>Unit definition IDs this faction can field, in the order listed in data.</summary>
        public IReadOnlyList<string> UnitIds { get; }

        /// <summary>Builds a definition. The loader validates that every unit ID exists.</summary>
        public FactionDefinition(string id, string displayName, string mechanicId, IReadOnlyList<string> unitIds)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Faction id must not be empty.", nameof(id));
            }
            if (string.IsNullOrEmpty(mechanicId))
            {
                throw new ArgumentException("Faction mechanic_id must not be empty.", nameof(mechanicId));
            }
            Id = id;
            DisplayName = displayName ?? id;
            MechanicId = mechanicId;
            UnitIds = unitIds ?? new List<string>();
        }

        /// <summary>The mechanic ID meaning "this faction has no signature mechanic".</summary>
        public const string NoMechanic = "none";
    }
}

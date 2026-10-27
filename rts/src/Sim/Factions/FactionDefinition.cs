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

        /// <summary>
        /// ID of the unit that is this faction's mobile HQ (data key "hq_unit"), or null for a faction without units
        /// (neutral) and for hand-built test factions. Range: a unit ID of this faction whose definition has HQ rules,
        /// checked by the loader. The simulation itself never reads it: hosts, scripts and the AI use it to know which
        /// unit to spawn for a player at match start.
        /// </summary>
        public string? HqUnitId { get; }

        /// <summary>Builds a definition without an HQ unit. The loader validates that every unit ID exists.</summary>
        public FactionDefinition(string id, string displayName, string mechanicId, IReadOnlyList<string> unitIds)
            : this(id, displayName, mechanicId, unitIds, null)
        {
        }

        /// <summary>Builds a definition. The loader validates that every unit ID exists.</summary>
        /// <param name="hqUnitId">ID of the faction's HQ unit, or null.</param>
        public FactionDefinition(string id, string displayName, string mechanicId, IReadOnlyList<string> unitIds, string? hqUnitId)
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
            HqUnitId = hqUnitId;
        }

        /// <summary>The mechanic ID meaning "this faction has no signature mechanic".</summary>
        public const string NoMechanic = "none";

        /// <summary>The faction ID for units owned by nobody (critters, map objects). Data file: data/factions/neutral.json.</summary>
        public const string NeutralId = "neutral";
    }
}

// rts/src/Sim/Units/UnitDefinition.cs
// Immutable description of a unit type, loaded from data/units/*.json by the data loader.
// Part of Rts.Sim.Units. Rule 3 of the architecture: every number here comes from a data file.
// Phase 0 holds the fields the skeleton needs; later phases add weapons, abilities and build info.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>One unit type. Shared by every unit of that type; never changes during a match.</summary>
    public sealed class UnitDefinition
    {
        /// <summary>Stable ID such as "skai.windrunner" (ADR-003). Lower-case, dots and underscores only.</summary>
        public string Id { get; }

        /// <summary>Name shown to players. Presentation only; not hashed.</summary>
        public string DisplayName { get; }

        /// <summary>ID of the faction that fields this unit, such as "skai", or <see cref="Rts.Sim.Factions.FactionDefinition.NeutralId"/>.</summary>
        public string FactionId { get; }

        /// <summary>Default movement layer.</summary>
        public MovementLayer Layer { get; }

        /// <summary>Health at full. Unit: health points. Range: greater than 0.</summary>
        public Fix64 MaxHealth { get; }

        /// <summary>Movement speed. Unit: tiles per second. Range: 0 (immobile) or more.</summary>
        public Fix64 SpeedTilesPerSecond { get; }

        /// <summary>Supply used while alive. Unit: supply. Range: 0 or more.</summary>
        public int Supply { get; }

        /// <summary>Collision radius. Unit: tiles. Range: greater than 0.</summary>
        public Fix64 Radius { get; }

        /// <summary>Cost in the common resource. Unit: resource units. Range: 0 or more.</summary>
        public int CostCommon { get; }

        /// <summary>Cost in the rare resource. Unit: resource units. Range: 0 or more.</summary>
        public int CostRare { get; }

        /// <summary>Builds a definition, validating every range. The loader turns a failure into an error naming the file and field.</summary>
        public UnitDefinition(string id, string displayName, string factionId, MovementLayer layer, Fix64 maxHealth,
            Fix64 speedTilesPerSecond, int supply, Fix64 radius, int costCommon, int costRare)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Unit id must not be empty.", nameof(id));
            }
            if (maxHealth <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "max_health must be greater than 0.");
            }
            if (speedTilesPerSecond < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(speedTilesPerSecond), "speed_tiles_per_second must be 0 or more.");
            }
            if (supply < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(supply), "supply must be 0 or more.");
            }
            if (radius <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "radius must be greater than 0.");
            }
            if (costCommon < 0 || costRare < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(costCommon), "costs must be 0 or more.");
            }
            Id = id;
            DisplayName = displayName ?? id;
            FactionId = factionId ?? Rts.Sim.Factions.FactionDefinition.NeutralId;
            Layer = layer;
            MaxHealth = maxHealth;
            SpeedTilesPerSecond = speedTilesPerSecond;
            Supply = supply;
            Radius = radius;
            CostCommon = costCommon;
            CostRare = costRare;
        }
    }
}

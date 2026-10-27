// rts/src/Sim/Data/CycleDefinition.cs
// Immutable description of a Planetary Cycle from rts/data/cycles/<id>.json. Part of Rts.Sim.Data.
// Phase 0 carries identity and which play style the cycle favours; the effect numbers are still to be settled in the
// systems spec (design doc, Planetary Cycles) and will be added as data fields when they are.
using System;

namespace Rts.Sim.Data
{
    /// <summary>Which play style a Planetary Cycle favours (design doc: cycles favour a play style, not a faction).</summary>
    public enum CycleFavour : byte
    {
        /// <summary>Favours flyers (Windstorm).</summary>
        Air = 0,

        /// <summary>Favours the player who attacks (Quake).</summary>
        Attackers = 1,

        /// <summary>Favours the player holding good ground near the Core (Core surge).</summary>
        Position = 2,
    }

    /// <summary>One Planetary Cycle type.</summary>
    public sealed class CycleDefinition
    {
        /// <summary>Stable ID, equal to the file name without ".json", such as "windstorm". Lower-case letters, digits and underscores.</summary>
        public string Id { get; }

        /// <summary>Name shown to players. Presentation only.</summary>
        public string DisplayName { get; }

        /// <summary>Play style this cycle favours.</summary>
        public CycleFavour Favours { get; }

        /// <summary>One or two sentences describing the effect, for tooltips. Presentation only.</summary>
        public string Description { get; }

        /// <summary>Builds a cycle definition.</summary>
        public CycleDefinition(string id, string displayName, CycleFavour favours, string description)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Cycle id must not be empty.", nameof(id));
            }
            Id = id;
            DisplayName = displayName ?? id;
            Favours = favours;
            Description = description ?? string.Empty;
        }
    }
}

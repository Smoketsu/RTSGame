// rts/src/Sim/Units/ResearchDefinition.cs
// Immutable definition of one research item (research_and_unlocks.md R1.3): an ID, a faction, a cost and a research time. Part of Rts.Sim.Units.
// Read from rts/data/research by the data loader; queued at a tech building through train_unit; finishing it adds the ID to the owner's research set.
// It is not a unit: it never spawns, uses no supply and has no requires.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>One research item. Never changes during a match.</summary>
    public sealed class ResearchDefinition
    {
        /// <summary>Stable ID, such as "skai.thunderhead_plans". Non-empty; shares one namespace with unit IDs.</summary>
        public string Id { get; }

        /// <summary>Name shown to players. Presentation only; not hashed. Range: 1 to 40 characters.</summary>
        public string DisplayName { get; }

        /// <summary>Faction that can research it. Range: an existing faction ID.</summary>
        public string FactionId { get; }

        /// <summary>Cost in the common resource, paid when queued. Unit: resource units. Range: 0 to 100000.</summary>
        public int CostCommon { get; }

        /// <summary>Cost in the rare resource, paid when queued. Unit: resource units. Range: 0 to 100000.</summary>
        public int CostRare { get; }

        /// <summary>Time at the head of the queue. Unit: seconds (fixed-point). Range: 0.05 to 3600.</summary>
        public Fix64 ResearchSeconds { get; }

        /// <summary>Builds the definition, validating every range.</summary>
        /// <param name="id">Research ID; non-empty.</param>
        /// <param name="displayName">Display name; not null.</param>
        /// <param name="factionId">Faction ID; non-empty.</param>
        /// <param name="costCommon">Common cost, 0 to 100000.</param>
        /// <param name="costRare">Rare cost, 0 to 100000.</param>
        /// <param name="researchSeconds">Seconds, 0.05 to 3600.</param>
        public ResearchDefinition(string id, string displayName, string factionId, int costCommon, int costRare, Fix64 researchSeconds)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("A research ID cannot be empty.", nameof(id));
            }
            if (string.IsNullOrEmpty(factionId))
            {
                throw new ArgumentException("A research faction ID cannot be empty.", nameof(factionId));
            }
            if (costCommon < 0 || costCommon > 100000)
            {
                throw new ArgumentOutOfRangeException(nameof(costCommon), "Research costs must be 0 to 100000.");
            }
            if (costRare < 0 || costRare > 100000)
            {
                throw new ArgumentOutOfRangeException(nameof(costRare), "Research costs must be 0 to 100000.");
            }
            if (researchSeconds < Fix64.Parse("0.05") || researchSeconds > Fix64.FromInt(3600))
            {
                throw new ArgumentOutOfRangeException(nameof(researchSeconds), "research_seconds must be 0.05 to 3600.");
            }
            Id = id;
            DisplayName = displayName ?? string.Empty;
            FactionId = factionId;
            CostCommon = costCommon;
            CostRare = costRare;
            ResearchSeconds = researchSeconds;
        }

        /// <summary>Feeds Id, FactionId, CostCommon, CostRare, ResearchSeconds into a hash in that order (content digest). The display name is left out.</summary>
        /// <param name="hasher">Hasher being filled.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
            hasher.Write(FactionId);
            hasher.Write(CostCommon);
            hasher.Write(CostRare);
            hasher.Write(ResearchSeconds);
        }
    }
}

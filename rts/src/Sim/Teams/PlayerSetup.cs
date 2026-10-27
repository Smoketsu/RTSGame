// rts/src/Sim/Teams/PlayerSetup.cs
// What the lobby decides about one player slot before tick 0. Part of Rts.Sim.Teams.
// Any number of slots is allowed; teams are explicit so 1v1, 2v2, 1v1v1 and FFA are all just lists.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Teams
{
    /// <summary>Immutable lobby data for one player slot.</summary>
    public sealed class PlayerSetup
    {
        /// <summary>Slot index, 0 to 254, unique within the match.</summary>
        public PlayerId Id { get; }

        /// <summary>Team the slot belongs to, 0 to 254. Players on the same team are allied.</summary>
        public TeamId Team { get; }

        /// <summary>Faction ID from data/factions, such as "skai".</summary>
        public string FactionId { get; }

        /// <summary>Name shown in the UI. Presentation only; not hashed.</summary>
        public string DisplayName { get; }

        /// <summary>Builds a slot description, validating the IDs.</summary>
        public PlayerSetup(PlayerId id, TeamId team, string factionId, string displayName)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A player slot needs a valid PlayerId.", nameof(id));
            }
            if (!team.IsValid)
            {
                throw new ArgumentException("A player slot needs a valid TeamId.", nameof(team));
            }
            if (string.IsNullOrEmpty(factionId))
            {
                throw new ArgumentException("A player slot needs a faction id.", nameof(factionId));
            }
            Id = id;
            Team = team;
            FactionId = factionId;
            DisplayName = displayName ?? ("Player " + id.Value);
        }
    }
}

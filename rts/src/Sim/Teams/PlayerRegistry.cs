// rts/src/Sim/Teams/PlayerRegistry.cs
// Minimal Phase 0 player and team registry: who is in the match and who is allied with whom.
// Part of Rts.Sim.Teams. Rule 5 of the architecture: any player count from day one. This file is the
// placeholder the team-model task extends with player states, alliances beyond fixed teams, and the
// surrender and disconnect hooks.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Rts.Sim.Core;

namespace Rts.Sim.Teams
{
    /// <summary>Players by ID in ascending order, plus team membership queries.</summary>
    public sealed class PlayerRegistry
    {
        /// <summary>Player setups by ID.</summary>
        private readonly SortedDictionary<PlayerId, PlayerSetup> _players = new SortedDictionary<PlayerId, PlayerSetup>();

        /// <summary>Distinct team IDs in ascending order.</summary>
        private readonly SortedSet<TeamId> _teams = new SortedSet<TeamId>();

        /// <summary>Number of player slots. Range: 1 to 255.</summary>
        public int Count => _players.Count;

        /// <summary>Number of distinct teams. Range: 1 to Count.</summary>
        public int TeamCount => _teams.Count;

        /// <summary>Player setups in ascending ID order.</summary>
        public IEnumerable<PlayerSetup> All => _players.Values;

        /// <summary>Builds the registry from lobby data. Duplicate IDs throw.</summary>
        /// <param name="setups">One entry per slot, at least one.</param>
        public PlayerRegistry(IReadOnlyList<PlayerSetup> setups)
        {
            if (setups == null || setups.Count == 0)
            {
                throw new ArgumentException("A match needs at least one player.", nameof(setups));
            }
            foreach (PlayerSetup setup in setups)
            {
                if (_players.ContainsKey(setup.Id))
                {
                    throw new ArgumentException("Duplicate player id " + setup.Id + ".", nameof(setups));
                }
                _players.Add(setup.Id, setup);
                _teams.Add(setup.Team);
            }
        }

        /// <summary>True when a slot with this ID exists.</summary>
        public bool Contains(PlayerId id)
        {
            return _players.ContainsKey(id);
        }

        /// <summary>Finds a player's setup.</summary>
        public bool TryGet(PlayerId id, [MaybeNullWhen(false)] out PlayerSetup setup)
        {
            return _players.TryGetValue(id, out setup);
        }

        /// <summary>True when both players are on the same team. A player is allied with itself. Unknown IDs are never allied.</summary>
        public bool AreAllied(PlayerId a, PlayerId b)
        {
            if (!_players.TryGetValue(a, out PlayerSetup? setupA) || !_players.TryGetValue(b, out PlayerSetup? setupB))
            {
                return false;
            }
            return setupA.Team == setupB.Team;
        }

        /// <summary>Feeds every player's simulation-relevant setup into the state hash, in ID order.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(_players.Count);
            foreach (PlayerSetup setup in _players.Values)
            {
                hasher.Write(setup.Id);
                hasher.Write(setup.Team);
                hasher.Write(setup.FactionId);
            }
        }
    }
}

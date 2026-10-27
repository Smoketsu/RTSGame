// rts/src/Sim/Teams/PlayerRegistry.cs
// Players and teams of one match: who is in it, which team each player is on, alliance queries, and the
// live player states that go into the state hash. Part of Rts.Sim.Teams. Rule 5 of the architecture: any
// player count. A team is just a list of 1..n players, so 1v1, 2v2, 1v1v1 and 4-player FFA need no special
// cases. Teams are fixed for the match (set by the lobby); allies share vision (Phase 1, fog of war) and
// never share resources. Player states change only through PlayerTransitions inside Simulation.Step.
// Every enumeration here is in ascending ID order (rule 6, ADR-003).
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Rts.Sim.Core;

namespace Rts.Sim.Teams
{
    /// <summary>Players by ID in ascending order, plus team membership and alliance queries.</summary>
    public sealed class PlayerRegistry
    {
        /// <summary>Shared empty result for queries about unknown players or teams.</summary>
        private static readonly IReadOnlyList<PlayerId> NoPlayers = new ReadOnlyCollection<PlayerId>(new PlayerId[0]);

        /// <summary>Live players by ID, ascending.</summary>
        private readonly SortedDictionary<PlayerId, Player> _players = new SortedDictionary<PlayerId, Player>();

        /// <summary>Members of each team, ascending by team ID; each member list is ascending by player ID and read-only.</summary>
        private readonly SortedDictionary<TeamId, IReadOnlyList<PlayerId>> _teamMembers = new SortedDictionary<TeamId, IReadOnlyList<PlayerId>>();

        /// <summary>Distinct team IDs in ascending order, read-only. Backs <see cref="Teams"/>.</summary>
        private readonly IReadOnlyList<TeamId> _teams;

        /// <summary>Number of player slots. Range: 1 to 255.</summary>
        public int Count => _players.Count;

        /// <summary>Number of distinct teams. Range: 1 to Count.</summary>
        public int TeamCount => _teams.Count;

        /// <summary>Live players in ascending ID order.</summary>
        public IEnumerable<Player> All => _players.Values;

        /// <summary>Distinct team IDs in ascending order. Fixed for the match.</summary>
        public IReadOnlyList<TeamId> Teams => _teams;

        /// <summary>Builds the registry from lobby data; every player starts Active. Duplicate IDs throw.</summary>
        /// <param name="setups">One entry per slot, at least one, in any order.</param>
        public PlayerRegistry(IReadOnlyList<PlayerSetup> setups)
        {
            if (setups == null || setups.Count == 0)
            {
                throw new ArgumentException("A match needs at least one player.", nameof(setups));
            }
            foreach (PlayerSetup setup in setups)
            {
                if (setup == null)
                {
                    throw new ArgumentException("Player setups cannot be null.", nameof(setups));
                }
                if (_players.ContainsKey(setup.Id))
                {
                    throw new ArgumentException("Duplicate player id " + setup.Id + ".", nameof(setups));
                }
                _players.Add(setup.Id, new Player(setup));
            }
            // Team member lists under construction, keyed by team in ascending order. Players are visited in ascending
            // ID order, so each list is already sorted.
            SortedDictionary<TeamId, List<PlayerId>> members = new SortedDictionary<TeamId, List<PlayerId>>();
            foreach (Player player in _players.Values)
            {
                if (!members.TryGetValue(player.Team, out List<PlayerId>? list))
                {
                    list = new List<PlayerId>();
                    members.Add(player.Team, list);
                }
                list.Add(player.Id);
            }
            // Team IDs in ascending order, for the read-only Teams view.
            List<TeamId> teams = new List<TeamId>(members.Count);
            foreach (KeyValuePair<TeamId, List<PlayerId>> pair in members)
            {
                teams.Add(pair.Key);
                _teamMembers.Add(pair.Key, pair.Value.AsReadOnly());
            }
            _teams = teams.AsReadOnly();
        }

        /// <summary>True when a slot with this ID exists.</summary>
        /// <param name="id">Player to look for.</param>
        public bool Contains(PlayerId id)
        {
            return _players.ContainsKey(id);
        }

        /// <summary>Finds a live player. False (and null) for unknown IDs and <see cref="PlayerId.None"/>.</summary>
        /// <param name="id">Player to look for.</param>
        /// <param name="player">The player when found.</param>
        public bool TryGet(PlayerId id, [MaybeNullWhen(false)] out Player player)
        {
            return _players.TryGetValue(id, out player);
        }

        /// <summary>Team of a player, or <see cref="TeamId.None"/> for an unknown ID.</summary>
        /// <param name="player">Player to look up.</param>
        public TeamId TeamOf(PlayerId player)
        {
            return _players.TryGetValue(player, out Player? found) ? found.Team : TeamId.None;
        }

        /// <summary>Members of a team in ascending ID order, including players who have left the match. Empty for an unknown team.</summary>
        /// <param name="team">Team to list.</param>
        public IReadOnlyList<PlayerId> PlayersOnTeam(TeamId team)
        {
            return _teamMembers.TryGetValue(team, out IReadOnlyList<PlayerId>? members) ? members : NoPlayers;
        }

        /// <summary>
        /// True when both players are on the same team. A player is allied with itself. Unknown IDs are never allied.
        /// Alliances are structural: they do not change when a player surrenders, disconnects or is defeated.
        /// </summary>
        /// <param name="a">First player.</param>
        /// <param name="b">Second player.</param>
        public bool AreAllied(PlayerId a, PlayerId b)
        {
            if (!_players.TryGetValue(a, out Player? playerA) || !_players.TryGetValue(b, out Player? playerB))
            {
                return false;
            }
            return playerA.Team == playerB.Team;
        }

        /// <summary>
        /// True when both players exist and are on different teams. A player is never its own enemy; unknown IDs
        /// (including <see cref="PlayerId.None"/>, the neutral world) are never enemies here; neutral hostility is a
        /// combat rule, not an alliance.
        /// </summary>
        /// <param name="a">First player.</param>
        /// <param name="b">Second player.</param>
        public bool AreEnemies(PlayerId a, PlayerId b)
        {
            if (!_players.TryGetValue(a, out Player? playerA) || !_players.TryGetValue(b, out Player? playerB))
            {
                return false;
            }
            return playerA.Team != playerB.Team;
        }

        /// <summary>
        /// The other players on <paramref name="player"/>'s team, in ascending ID order, excluding the player itself and
        /// regardless of their state. Empty for a solo team or an unknown ID. Allocates a new list per call.
        /// </summary>
        /// <param name="player">Player whose allies to list.</param>
        public IReadOnlyList<PlayerId> AlliesOf(PlayerId player)
        {
            if (!_players.TryGetValue(player, out Player? found))
            {
                return NoPlayers;
            }
            // Sorted member IDs of the player's team. Range: zero or more entries; never null.
            IReadOnlyList<PlayerId> members = _teamMembers[found.Team];
            // Team members other than the player, ascending because members is ascending.
            List<PlayerId> allies = new List<PlayerId>(members.Count);
            foreach (PlayerId member in members)
            {
                if (member != player)
                {
                    allies.Add(member);
                }
            }
            return allies.AsReadOnly();
        }

        /// <summary>
        /// The lowest-ID ally of <paramref name="player"/> who is still <see cref="PlayerState.Active"/>, or
        /// <see cref="PlayerId.None"/> when there is none. This is the hand-over target the Phase 3 surrender and
        /// disconnect rules will use (design: in 2v2 the units and resources pass to the ally); Phase 0 only answers the query.
        /// </summary>
        /// <param name="player">Player who is leaving.</param>
        public PlayerId FirstActiveAllyOf(PlayerId player)
        {
            if (!_players.TryGetValue(player, out Player? found))
            {
                return PlayerId.None;
            }
            foreach (PlayerId member in _teamMembers[found.Team])
            {
                if (member != player && _players[member].IsActive)
                {
                    return member;
                }
            }
            return PlayerId.None;
        }

        /// <summary>
        /// Feeds every player's simulation-relevant state into the state hash, in ascending ID order: the player count,
        /// then per player its ID, team, faction, state, state-since tick and disconnect and pause counters
        /// (see <see cref="Player"/>). Display names are not hashed.
        /// </summary>
        /// <param name="hasher">Hasher being filled by <see cref="Simulation.ComputeStateHash"/> or a test.</param>
        public void WriteHash(StateHasher hasher)
        {
            if (hasher == null)
            {
                throw new ArgumentNullException(nameof(hasher));
            }
            hasher.Write(_players.Count);
            foreach (Player player in _players.Values)
            {
                player.WriteHash(hasher);
            }
        }
    }
}

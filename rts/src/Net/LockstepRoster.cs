// rts/src/Net/LockstepRoster.cs
// Which peer controls which player slots, fixed by the lobby before tick 0 and identical on every peer. Part of
// Rts.Net (ADR-008). The protocol uses it to validate that a bundle's commands are issued by the sending peer's
// players, to find the peer a pause request belongs to, and to pick the referee (the lowest live connected peer).
// Any peer count and any players-per-peer count: a 1v1 is two peers with one player each, a local 2v2 could be one
// peer with two players.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Net
{
    /// <summary>Peer-to-players map of one session. Build it, add every peer, then hand it to the peers; it is not changed after.</summary>
    public sealed class LockstepRoster
    {
        /// <summary>Players of each peer, ascending by peer; each list ascending by player.</summary>
        private readonly SortedDictionary<PeerId, IReadOnlyList<PlayerId>> _players = new SortedDictionary<PeerId, IReadOnlyList<PlayerId>>();

        /// <summary>Owning peer of each player, ascending by player.</summary>
        private readonly SortedDictionary<PlayerId, PeerId> _peerOf = new SortedDictionary<PlayerId, PeerId>();

        /// <summary>Peer IDs in ascending order, rebuilt on every <see cref="Add"/>.</summary>
        private IReadOnlyList<PeerId> _peers = new PeerId[0];

        /// <summary>Peers in ascending order. Range: one or more once built.</summary>
        public IReadOnlyList<PeerId> Peers => _peers;

        /// <summary>Number of players across every peer. Range: 0 upward.</summary>
        public int PlayerCount => _peerOf.Count;

        /// <summary>Adds a peer with the players it controls. A repeated peer or a player already owned throws.</summary>
        /// <param name="peer">A valid peer ID not yet added.</param>
        /// <param name="players">One or more valid, distinct player slots not yet owned.</param>
        public void Add(PeerId peer, params PlayerId[] players)
        {
            if (!peer.IsValid)
            {
                throw new ArgumentException("A roster peer needs a valid id.", nameof(peer));
            }
            if (players == null || players.Length == 0)
            {
                throw new ArgumentException("A roster peer needs at least one player.", nameof(players));
            }
            if (_players.ContainsKey(peer))
            {
                throw new ArgumentException("Peer " + peer + " is already in the roster.", nameof(peer));
            }
            // The peer's players sorted ascending.
            SortedSet<PlayerId> sorted = new SortedSet<PlayerId>();
            foreach (PlayerId player in players)
            {
                if (!player.IsValid)
                {
                    throw new ArgumentException("A roster player needs a valid id.", nameof(players));
                }
                if (_peerOf.ContainsKey(player) || !sorted.Add(player))
                {
                    throw new ArgumentException("Player " + player + " is already owned by a peer.", nameof(players));
                }
            }
            foreach (PlayerId player in sorted)
            {
                _peerOf.Add(player, peer);
            }
            _players.Add(peer, new List<PlayerId>(sorted));
            _peers = new List<PeerId>(_players.Keys);
        }

        /// <summary>True when <paramref name="peer"/> is in the roster.</summary>
        /// <param name="peer">The peer to look for.</param>
        public bool Contains(PeerId peer)
        {
            return _players.ContainsKey(peer);
        }

        /// <summary>The players a peer controls, ascending. Throws for an unknown peer.</summary>
        /// <param name="peer">A roster peer.</param>
        public IReadOnlyList<PlayerId> PlayersOf(PeerId peer)
        {
            if (!_players.TryGetValue(peer, out IReadOnlyList<PlayerId>? players))
            {
                throw new ArgumentException("Peer " + peer + " is not in the roster.", nameof(peer));
            }
            return players;
        }

        /// <summary>The peer controlling a player, or <see cref="PeerId.None"/> for a player no peer owns (the script issuer None included).</summary>
        /// <param name="player">Any player ID.</param>
        public PeerId PeerOf(PlayerId player)
        {
            return _peerOf.TryGetValue(player, out PeerId peer) ? peer : PeerId.None;
        }

        /// <summary>Every player in ascending order.</summary>
        public IEnumerable<PlayerId> AllPlayers => _peerOf.Keys;
    }
}

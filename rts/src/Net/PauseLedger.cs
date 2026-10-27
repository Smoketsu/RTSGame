// rts/src/Net/PauseLedger.cs
// Per-player pause accounting of a lockstep session: how many disconnect pauses each player has used (design: one
// per player, capped at disconnect_pause_max_seconds) and how much of the voluntary pause budget they have spent
// (design: 120 s per player). Part of Rts.Net (ADR-008). It is protocol state, not simulation state: every peer
// holds a copy and every change comes from a referee message (PauseStarted for the disconnect count, PauseEnded's
// ElapsedMs for the budget), so the copies agree without being hashed. A reconnecting peer receives a snapshot in
// the SyncLog. The simulation's own Player.DisconnectPausesUsed and PauseTicksUsed stay at 0 until a simulation
// command carries these numbers in (proposed in the ADR); until then this ledger is the record.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Net
{
    /// <summary>Pause counters per player. Owned by <see cref="LockstepPeer"/>; hosts read it for the HUD.</summary>
    public sealed class PauseLedger
    {
        /// <summary>One player's counters.</summary>
        private sealed class Account
        {
            /// <summary>Disconnect pauses used. Unit: count. Range: 0 to <see cref="LockstepSettings.DisconnectPausesPerPlayer"/>.</summary>
            public int DisconnectPausesUsed;

            /// <summary>Budget spent. Unit: milliseconds. Range: 0 to <see cref="LockstepSettings.PauseBudgetMsPerPlayer"/>.</summary>
            public int PauseMsUsed;
        }

        /// <summary>Accounts by player, ascending.</summary>
        private readonly SortedDictionary<PlayerId, Account> _accounts = new SortedDictionary<PlayerId, Account>();

        /// <summary>The limits the counters are checked against.</summary>
        private readonly LockstepSettings _settings;

        /// <summary>Creates a ledger with every counter at 0.</summary>
        /// <param name="players">The session's players, any order.</param>
        /// <param name="settings">The limits.</param>
        public PauseLedger(IEnumerable<PlayerId> players, LockstepSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            foreach (PlayerId player in players ?? throw new ArgumentNullException(nameof(players)))
            {
                _accounts.Add(player, new Account());
            }
        }

        /// <summary>Players in ascending order.</summary>
        public IEnumerable<PlayerId> Players => _accounts.Keys;

        /// <summary>The account of a player; throws for a player not in the ledger.</summary>
        /// <param name="player">A session player.</param>
        private Account Require(PlayerId player)
        {
            if (!_accounts.TryGetValue(player, out Account? account))
            {
                throw new ArgumentException("Player " + player + " is not in the pause ledger.", nameof(player));
            }
            return account;
        }

        /// <summary>Disconnect pauses the player has used. Unit: count. Range: 0 to the per-player limit.</summary>
        /// <param name="player">A session player.</param>
        public int DisconnectPausesUsed(PlayerId player)
        {
            return Require(player).DisconnectPausesUsed;
        }

        /// <summary>Budget the player has spent. Unit: milliseconds. Range: 0 to the budget.</summary>
        /// <param name="player">A session player.</param>
        public int PauseMsUsed(PlayerId player)
        {
            return Require(player).PauseMsUsed;
        }

        /// <summary>True while the player has a disconnect pause left.</summary>
        /// <param name="player">A session player.</param>
        public bool CanStartDisconnectPause(PlayerId player)
        {
            return Require(player).DisconnectPausesUsed < _settings.DisconnectPausesPerPlayer;
        }

        /// <summary>Budget the player has left. Unit: milliseconds. Range: 0 to the budget.</summary>
        /// <param name="player">A session player.</param>
        public int RemainingBudgetMs(PlayerId player)
        {
            // Unspent budget, never negative.
            int remaining = _settings.PauseBudgetMsPerPlayer - Require(player).PauseMsUsed;
            return remaining < 0 ? 0 : remaining;
        }

        /// <summary>Counts one disconnect pause against the player. Internal: only the protocol, on a PauseStarted it issued or received.</summary>
        /// <param name="player">A session player.</param>
        internal void RecordDisconnectPause(PlayerId player)
        {
            Require(player).DisconnectPausesUsed++;
        }

        /// <summary>Charges pause time to the player's budget, saturating at the budget. Internal: only the protocol, on PauseEnded.</summary>
        /// <param name="player">A session player.</param>
        /// <param name="ms">Pause length. Unit: milliseconds. Range: 0 upward.</param>
        internal void ChargeBudget(PlayerId player, int ms)
        {
            if (ms < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ms));
            }
            // The account being charged.
            Account account = Require(player);
            account.PauseMsUsed = ms > _settings.PauseBudgetMsPerPlayer - account.PauseMsUsed ? _settings.PauseBudgetMsPerPlayer : account.PauseMsUsed + ms;
        }

        /// <summary>Every counter, ascending by player, for a <see cref="SyncLogMessage"/>.</summary>
        public List<PauseAccountEntry> Snapshot()
        {
            // The entries in player order.
            List<PauseAccountEntry> entries = new List<PauseAccountEntry>(_accounts.Count);
            foreach (KeyValuePair<PlayerId, Account> pair in _accounts)
            {
                entries.Add(new PauseAccountEntry(pair.Key, pair.Value.DisconnectPausesUsed, pair.Value.PauseMsUsed));
            }
            return entries;
        }

        /// <summary>Overwrites the counters from a snapshot. Entries for unknown players are a protocol error. Internal: only on a SyncLog.</summary>
        /// <param name="entries">The referee's snapshot.</param>
        internal void Restore(IReadOnlyList<PauseAccountEntry> entries)
        {
            foreach (PauseAccountEntry entry in entries)
            {
                if (!_accounts.TryGetValue(entry.Player, out Account? account))
                {
                    throw new NetProtocolException("Sync log names unknown player " + entry.Player + " in the pause ledger.");
                }
                if (entry.DisconnectPausesUsed < 0 || entry.PauseMsUsed < 0)
                {
                    throw new NetProtocolException("Sync log holds negative pause counters for " + entry.Player + ".");
                }
                account.DisconnectPausesUsed = entry.DisconnectPausesUsed;
                account.PauseMsUsed = entry.PauseMsUsed;
            }
        }
    }
}

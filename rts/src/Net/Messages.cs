// rts/src/Net/Messages.cs
// The protocol's message types and the pause vocabulary they share. Part of Rts.Net (ADR-008). One class per message
// kind, immutable, holding exactly the fields that go on the wire; MessageCodec (MessageCodec.cs) is the only place
// that turns them into bytes and back. Who sends what: every peer sends CommandBundle (one per tick, empty bundles
// too) and StateHash (one per hash interval); any peer may send PauseRequest, ResumeRequest, BundleRequest and
// SyncRequest; the referee alone sends PauseStarted, PauseEnded, PeerDropped, PeerRejoined, SyncLog and SyncRejected;
// a reconnecting peer sends SyncDone. The referee is the lowest live connected peer (LockstepPeer.Referee).
using System;
using System.Collections.Generic;
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Net
{
    /// <summary>Message kinds. Stored as one byte on the wire: never renumber, only append.</summary>
    public enum MessageKind : byte
    {
        /// <summary>One peer's commands for one tick (<see cref="CommandBundleMessage"/>).</summary>
        CommandBundle = 1,

        /// <summary>One peer's state hash at a hash tick (<see cref="StateHashMessage"/>).</summary>
        StateHash = 2,

        /// <summary>A player asks the referee for a voluntary pause (<see cref="PauseRequestMessage"/>).</summary>
        PauseRequest = 3,

        /// <summary>A player asks the referee to end their voluntary pause (<see cref="ResumeRequestMessage"/>).</summary>
        ResumeRequest = 4,

        /// <summary>The referee announces a pause (<see cref="PauseStartedMessage"/>).</summary>
        PauseStarted = 5,

        /// <summary>The referee ends a pause and states its length (<see cref="PauseEndedMessage"/>).</summary>
        PauseEnded = 6,

        /// <summary>The referee removes a peer from the live set (<see cref="PeerDroppedMessage"/>).</summary>
        PeerDropped = 7,

        /// <summary>The referee returns a peer to the live set (<see cref="PeerRejoinedMessage"/>).</summary>
        PeerRejoined = 8,

        /// <summary>A reconnecting peer asks the referee for the command log (<see cref="SyncRequestMessage"/>).</summary>
        SyncRequest = 9,

        /// <summary>The referee's command log and session state for a reconnecting peer (<see cref="SyncLogMessage"/>).</summary>
        SyncLog = 10,

        /// <summary>A reconnecting peer reports it replayed the log and the hash it reached (<see cref="SyncDoneMessage"/>).</summary>
        SyncDone = 11,

        /// <summary>The referee refuses a reconnect (<see cref="SyncRejectedMessage"/>).</summary>
        SyncRejected = 12,

        /// <summary>A peer asks the referee to resend a bundle it lacks (<see cref="BundleRequestMessage"/>).</summary>
        BundleRequest = 13,
    }

    /// <summary>Why a pause was started. One byte on the wire: never renumber.</summary>
    public enum PauseReason : byte
    {
        /// <summary>A peer disconnected and one of its players still had a disconnect pause; capped by disconnect_pause_max_seconds.</summary>
        Disconnect = 0,

        /// <summary>A player asked to pause; charged to pause_budget_seconds_per_player.</summary>
        Voluntary = 1,

        /// <summary>A peer is reconnecting outside a disconnect pause; charged to the reconnecting player's pause budget.</summary>
        Resync = 2,
    }

    /// <summary>How a pause ended. One byte on the wire: never renumber.</summary>
    public enum PauseEnd : byte
    {
        /// <summary>The player resumed, or the disconnected peer came back.</summary>
        Resumed = 0,

        /// <summary>The limit ran out: the disconnected player is out of the match, or the pause budget is spent.</summary>
        Expired = 1,
    }

    /// <summary>Base of every protocol message.</summary>
    public abstract class NetMessage
    {
        /// <summary>Which message this is.</summary>
        public abstract MessageKind Kind { get; }
    }

    /// <summary>One peer's commands for one tick. Sent once per tick by every live peer, empty when it issued nothing.</summary>
    public sealed class CommandBundleMessage : NetMessage
    {
        /// <summary>Tick the commands apply at. Unit: ticks. Range: 0 upward.</summary>
        public int Tick { get; }

        /// <summary>The sending peer, as stamped by the sender (the transport sender must match).</summary>
        public PeerId Peer { get; }

        /// <summary>The commands, each with <see cref="Command.Tick"/> equal to <see cref="Tick"/>. Range: zero or more.</summary>
        public IReadOnlyList<Command> Commands { get; }

        /// <summary>Builds a bundle. Every command's tick must equal <paramref name="tick"/>.</summary>
        /// <param name="tick">The bundle's tick. Unit: ticks. Range: 0 upward.</param>
        /// <param name="peer">The sending peer.</param>
        /// <param name="commands">The commands; not null, may be empty.</param>
        public CommandBundleMessage(int tick, PeerId peer, IReadOnlyList<Command> commands)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }
            Tick = tick;
            Peer = peer;
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            foreach (Command command in commands)
            {
                if (command.Tick != tick)
                {
                    throw new ArgumentException("Command " + command + " does not belong to the bundle for tick " + tick + ".", nameof(commands));
                }
            }
        }

        /// <summary><see cref="MessageKind.CommandBundle"/>.</summary>
        public override MessageKind Kind => MessageKind.CommandBundle;
    }

    /// <summary>One peer's state hash at a hash tick (ADR-005).</summary>
    public sealed class StateHashMessage : NetMessage
    {
        /// <summary>The hash tick. Unit: ticks. Range: a multiple of the hash interval.</summary>
        public int Tick { get; }

        /// <summary>The hash. Range: any ulong except 0 (the simulation never reports 0).</summary>
        public ulong Hash { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="tick">The hash tick.</param>
        /// <param name="hash">The hash reported by the simulation.</param>
        public StateHashMessage(int tick, ulong hash)
        {
            Tick = tick;
            Hash = hash;
        }

        /// <summary><see cref="MessageKind.StateHash"/>.</summary>
        public override MessageKind Kind => MessageKind.StateHash;
    }

    /// <summary>A player asks the referee for a voluntary pause.</summary>
    public sealed class PauseRequestMessage : NetMessage
    {
        /// <summary>The player who wants to pause; must be controlled by the sending peer.</summary>
        public PlayerId Player { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="player">The requesting player.</param>
        public PauseRequestMessage(PlayerId player)
        {
            Player = player;
        }

        /// <summary><see cref="MessageKind.PauseRequest"/>.</summary>
        public override MessageKind Kind => MessageKind.PauseRequest;
    }

    /// <summary>A player asks the referee to end their own voluntary pause.</summary>
    public sealed class ResumeRequestMessage : NetMessage
    {
        /// <summary>The player whose pause should end; must be controlled by the sending peer.</summary>
        public PlayerId Player { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="player">The requesting player.</param>
        public ResumeRequestMessage(PlayerId player)
        {
            Player = player;
        }

        /// <summary><see cref="MessageKind.ResumeRequest"/>.</summary>
        public override MessageKind Kind => MessageKind.ResumeRequest;
    }

    /// <summary>The referee announces a pause. Every peer stops stepping until the matching <see cref="PauseEndedMessage"/>.</summary>
    public sealed class PauseStartedMessage : NetMessage
    {
        /// <summary>Session-unique pause number, so an end can be matched to its start even when messages are reordered. Range: 1 upward.</summary>
        public uint PauseId { get; }

        /// <summary>The player the pause is held for (whose pause or budget it uses).</summary>
        public PlayerId Player { get; }

        /// <summary>The peer the pause waits for (the dropped or reconnecting peer), or None for a voluntary pause.</summary>
        public PeerId Peer { get; }

        /// <summary>Why the pause was started.</summary>
        public PauseReason Reason { get; }

        /// <summary>Longest the pause may last. Unit: milliseconds. Range: 1 upward.</summary>
        public int LimitMs { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="pauseId">The pause number.</param>
        /// <param name="player">The player the pause is held for.</param>
        /// <param name="peer">The peer waited for, or None.</param>
        /// <param name="reason">Why.</param>
        /// <param name="limitMs">The limit in milliseconds, 1 or more.</param>
        public PauseStartedMessage(uint pauseId, PlayerId player, PeerId peer, PauseReason reason, int limitMs)
        {
            if (limitMs < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(limitMs));
            }
            PauseId = pauseId;
            Player = player;
            Peer = peer;
            Reason = reason;
            LimitMs = limitMs;
        }

        /// <summary><see cref="MessageKind.PauseStarted"/>.</summary>
        public override MessageKind Kind => MessageKind.PauseStarted;
    }

    /// <summary>The referee ends a pause. Its <see cref="ElapsedMs"/> is the one number every ledger charges, so ledgers agree.</summary>
    public sealed class PauseEndedMessage : NetMessage
    {
        /// <summary>The pause being ended. Range: a <see cref="PauseStartedMessage.PauseId"/>.</summary>
        public uint PauseId { get; }

        /// <summary>How long it lasted, as timed by the referee. Unit: milliseconds. Range: 0 to the pause's limit.</summary>
        public int ElapsedMs { get; }

        /// <summary>How it ended.</summary>
        public PauseEnd End { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="pauseId">The pause number.</param>
        /// <param name="elapsedMs">The pause length in milliseconds, 0 or more.</param>
        /// <param name="end">How it ended.</param>
        public PauseEndedMessage(uint pauseId, int elapsedMs, PauseEnd end)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs));
            }
            PauseId = pauseId;
            ElapsedMs = elapsedMs;
            End = end;
        }

        /// <summary><see cref="MessageKind.PauseEnded"/>.</summary>
        public override MessageKind Kind => MessageKind.PauseEnded;
    }

    /// <summary>
    /// The referee removes a peer from the live set: its bundles count up to and including <see cref="LastTick"/> (the
    /// highest tick for which the referee holds every bundle from it since it last joined) and are empty after that.
    /// </summary>
    public sealed class PeerDroppedMessage : NetMessage
    {
        /// <summary>The dropped peer.</summary>
        public PeerId Peer { get; }

        /// <summary>Last tick whose bundle from the peer still counts. Unit: ticks. Range: -1 (none) upward.</summary>
        public int LastTick { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="peer">The dropped peer.</param>
        /// <param name="lastTick">Last counted tick, -1 or more.</param>
        public PeerDroppedMessage(PeerId peer, int lastTick)
        {
            if (lastTick < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(lastTick));
            }
            Peer = peer;
            LastTick = lastTick;
        }

        /// <summary><see cref="MessageKind.PeerDropped"/>.</summary>
        public override MessageKind Kind => MessageKind.PeerDropped;
    }

    /// <summary>The referee returns a synced peer to the live set: its bundles are required from <see cref="FromTick"/> on.</summary>
    public sealed class PeerRejoinedMessage : NetMessage
    {
        /// <summary>The returning peer.</summary>
        public PeerId Peer { get; }

        /// <summary>First tick the peer's bundle is required for again. Unit: ticks. Range: 0 upward.</summary>
        public int FromTick { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="peer">The returning peer.</param>
        /// <param name="fromTick">First required tick, 0 or more.</param>
        public PeerRejoinedMessage(PeerId peer, int fromTick)
        {
            if (fromTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromTick));
            }
            Peer = peer;
            FromTick = fromTick;
        }

        /// <summary><see cref="MessageKind.PeerRejoined"/>.</summary>
        public override MessageKind Kind => MessageKind.PeerRejoined;
    }

    /// <summary>A reconnecting peer asks the referee for the command log. Carries nothing: the sender is the transport's sender.</summary>
    public sealed class SyncRequestMessage : NetMessage
    {
        /// <summary><see cref="MessageKind.SyncRequest"/>.</summary>
        public override MessageKind Kind => MessageKind.SyncRequest;
    }

    /// <summary>One player's pause accounting as carried in a <see cref="SyncLogMessage"/>.</summary>
    public readonly struct PauseAccountEntry
    {
        /// <summary>The player.</summary>
        public readonly PlayerId Player;

        /// <summary>Disconnect pauses the player has used. Unit: count. Range: 0 to disconnect_pauses_per_player.</summary>
        public readonly int DisconnectPausesUsed;

        /// <summary>Voluntary and resync pause time charged to the player. Unit: milliseconds. Range: 0 to the budget.</summary>
        public readonly int PauseMsUsed;

        /// <summary>Builds the entry.</summary>
        /// <param name="player">The player.</param>
        /// <param name="disconnectPausesUsed">Disconnect pauses used.</param>
        /// <param name="pauseMsUsed">Budget milliseconds used.</param>
        public PauseAccountEntry(PlayerId player, int disconnectPausesUsed, int pauseMsUsed)
        {
            Player = player;
            DisconnectPausesUsed = disconnectPausesUsed;
            PauseMsUsed = pauseMsUsed;
        }
    }

    /// <summary>A closed or open range of ticks for which a peer's bundles are required (inclusive both ends; <see cref="To"/> is int.MaxValue while open).</summary>
    public readonly struct TickRange
    {
        /// <summary>First required tick. Unit: ticks. Range: 0 upward.</summary>
        public readonly int From;

        /// <summary>Last required tick, inclusive; int.MaxValue while the range is open. Unit: ticks. Range: From - 1 (empty) upward.</summary>
        public readonly int To;

        /// <summary>Builds a range.</summary>
        /// <param name="from">First tick.</param>
        /// <param name="to">Last tick inclusive, or int.MaxValue.</param>
        public TickRange(int from, int to)
        {
            From = from;
            To = to;
        }

        /// <summary>True when <paramref name="tick"/> lies inside the range.</summary>
        /// <param name="tick">The tick to test.</param>
        public bool Contains(int tick)
        {
            return tick >= From && tick <= To;
        }

        /// <summary>True while the range has no upper bound.</summary>
        public bool IsOpen => To == int.MaxValue;
    }

    /// <summary>The required ranges of one peer as carried in a <see cref="SyncLogMessage"/>.</summary>
    public sealed class PeerRangesEntry
    {
        /// <summary>The peer.</summary>
        public PeerId Peer { get; }

        /// <summary>Its required ranges in ascending order. Range: one or more.</summary>
        public IReadOnlyList<TickRange> Ranges { get; }

        /// <summary>Builds the entry.</summary>
        /// <param name="peer">The peer.</param>
        /// <param name="ranges">Its ranges; not null.</param>
        public PeerRangesEntry(PeerId peer, IReadOnlyList<TickRange> ranges)
        {
            Peer = peer;
            Ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
        }
    }

    /// <summary>
    /// Everything a reconnecting peer needs to rebuild the session: every bundle the referee holds, the referee's
    /// current tick, the hash to verify against, the pause ledger, the required ranges, the live set and the active pause.
    /// </summary>
    public sealed class SyncLogMessage : NetMessage
    {
        /// <summary>The referee's current tick (the next tick it will step). The joiner replays ticks 0 to this minus 1. Unit: ticks. Range: 0 upward.</summary>
        public int RefereeTick { get; }

        /// <summary>The last hash tick the referee has stepped, or -1 when none yet. Unit: ticks. Range: -1 upward.</summary>
        public int CheckTick { get; }

        /// <summary>The referee's hash at <see cref="CheckTick"/>; 0 when CheckTick is -1. Range: any ulong.</summary>
        public ulong CheckHash { get; }

        /// <summary>Every bundle the referee holds, in ascending (tick, peer) order.</summary>
        public IReadOnlyList<CommandBundleMessage> Bundles { get; }

        /// <summary>The pause ledger, one entry per player in ascending player order.</summary>
        public IReadOnlyList<PauseAccountEntry> Ledger { get; }

        /// <summary>The required ranges of every roster peer in ascending peer order.</summary>
        public IReadOnlyList<PeerRangesEntry> Ranges { get; }

        /// <summary>Peers currently dropped from the live set, ascending (the joiner itself is among them).</summary>
        public IReadOnlyList<PeerId> Dropped { get; }

        /// <summary>The active pause, or null when the session is not paused.</summary>
        public PauseStartedMessage? ActivePause { get; }

        /// <summary>The referee's next pause number, so a joiner that later becomes referee never reuses one. Range: 1 upward.</summary>
        public uint NextPauseId { get; }

        /// <summary>Builds the message.</summary>
        public SyncLogMessage(int refereeTick, int checkTick, ulong checkHash, IReadOnlyList<CommandBundleMessage> bundles,
            IReadOnlyList<PauseAccountEntry> ledger, IReadOnlyList<PeerRangesEntry> ranges, IReadOnlyList<PeerId> dropped,
            PauseStartedMessage? activePause, uint nextPauseId)
        {
            if (refereeTick < 0 || checkTick < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(refereeTick));
            }
            RefereeTick = refereeTick;
            CheckTick = checkTick;
            CheckHash = checkHash;
            Bundles = bundles ?? throw new ArgumentNullException(nameof(bundles));
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            Ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
            Dropped = dropped ?? throw new ArgumentNullException(nameof(dropped));
            ActivePause = activePause;
            NextPauseId = nextPauseId;
        }

        /// <summary><see cref="MessageKind.SyncLog"/>.</summary>
        public override MessageKind Kind => MessageKind.SyncLog;
    }

    /// <summary>A reconnecting peer has replayed the log up to the referee's tick and reports the hash it reached.</summary>
    public sealed class SyncDoneMessage : NetMessage
    {
        /// <summary>The hash tick compared, as given by <see cref="SyncLogMessage.CheckTick"/>. Unit: ticks. Range: -1 upward.</summary>
        public int CheckTick { get; }

        /// <summary>The joiner's hash at that tick; 0 when CheckTick is -1. Range: any ulong.</summary>
        public ulong Hash { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="checkTick">The compared tick.</param>
        /// <param name="hash">The joiner's hash.</param>
        public SyncDoneMessage(int checkTick, ulong hash)
        {
            CheckTick = checkTick;
            Hash = hash;
        }

        /// <summary><see cref="MessageKind.SyncDone"/>.</summary>
        public override MessageKind Kind => MessageKind.SyncDone;
    }

    /// <summary>The referee refuses a reconnect, with a snake_case reason such as "hash_mismatch" or "no_pause_budget".</summary>
    public sealed class SyncRejectedMessage : NetMessage
    {
        /// <summary>Why. Range: non-empty snake_case text.</summary>
        public string Reason { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="reason">Non-empty reason.</param>
        public SyncRejectedMessage(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("A rejection needs a reason.", nameof(reason));
            }
            Reason = reason;
        }

        /// <summary><see cref="MessageKind.SyncRejected"/>.</summary>
        public override MessageKind Kind => MessageKind.SyncRejected;
    }

    /// <summary>A peer asks the referee to resend one bundle it lacks (a dropped peer's bundle the referee kept, or one sent before the requester connected).</summary>
    public sealed class BundleRequestMessage : NetMessage
    {
        /// <summary>The peer whose bundle is missing.</summary>
        public PeerId Peer { get; }

        /// <summary>The bundle's tick. Unit: ticks. Range: 0 upward.</summary>
        public int Tick { get; }

        /// <summary>Builds the message.</summary>
        /// <param name="peer">The bundle's sender.</param>
        /// <param name="tick">The bundle's tick, 0 or more.</param>
        public BundleRequestMessage(PeerId peer, int tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }
            Peer = peer;
            Tick = tick;
        }

        /// <summary><see cref="MessageKind.BundleRequest"/>.</summary>
        public override MessageKind Kind => MessageKind.BundleRequest;
    }
}

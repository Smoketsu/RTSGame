// rts/src/Net/LockstepNotice.cs
// What a lockstep peer reports to its host: desyncs, pauses, drops, rejoins, sync progress and injected commands.
// Part of Rts.Net (ADR-008). Notices follow the simulation's events-out rule: the host reads them after each
// Update (LockstepPeer.Notices, cleared at the start of the next Update, or the NoticeRaised event) and never
// changes protocol state through them.
using Rts.Sim.Core;

namespace Rts.Net
{
    /// <summary>Kinds of notice. Appended to only.</summary>
    public enum NoticeKind : byte
    {
        /// <summary>A remote hash differs from the local one at <see cref="LockstepNotice.Tick"/>, the first mismatching hash tick. The peer stops stepping.</summary>
        Desynced = 0,

        /// <summary>A pause began (player, peer and reason in the notice).</summary>
        PauseStarted = 1,

        /// <summary>A pause ended; <see cref="LockstepNotice.Text"/> says how and for how long.</summary>
        PauseEnded = 2,

        /// <summary>The referee refused a pause request (already paused, no budget, not the requester's player).</summary>
        PauseRefused = 3,

        /// <summary>A peer left the live set; its bundles are empty after <see cref="LockstepNotice.Tick"/>.</summary>
        PeerDropped = 4,

        /// <summary>A peer rejoined the live set from <see cref="LockstepNotice.Tick"/>.</summary>
        PeerRejoined = 5,

        /// <summary>This peer received the command log and is replaying it up to <see cref="LockstepNotice.Tick"/>.</summary>
        SyncStarted = 6,

        /// <summary>This peer finished replaying and reported its hash; it waits to be rejoined.</summary>
        SyncDone = 7,

        /// <summary>The referee refused this peer's reconnect; <see cref="LockstepNotice.Text"/> holds the reason.</summary>
        SyncRejected = 8,

        /// <summary>The referee injected a protocol command (connection state, surrender) for <see cref="LockstepNotice.Tick"/>.</summary>
        CommandInjected = 9,

        /// <summary>This peer applied commands the referee later declared void (a drop raced ahead of it); it must rejoin.</summary>
        Inconsistent = 10,
    }

    /// <summary>One report from the protocol to the host. Immutable.</summary>
    public sealed class LockstepNotice
    {
        /// <summary>What happened.</summary>
        public NoticeKind Kind { get; }

        /// <summary>The tick the notice is about (desync tick, drop cutoff, rejoin tick, replay target, injected command tick), or -1 when none. Unit: ticks. Range: -1 upward.</summary>
        public int Tick { get; }

        /// <summary>The peer concerned, or None.</summary>
        public PeerId Peer { get; }

        /// <summary>The player concerned, or None.</summary>
        public PlayerId Player { get; }

        /// <summary>This peer's hash for a desync, else 0. Range: any ulong.</summary>
        public ulong LocalHash { get; }

        /// <summary>The remote peer's hash for a desync, else 0. Range: any ulong.</summary>
        public ulong RemoteHash { get; }

        /// <summary>Short snake_case detail such as "disconnect", "resumed 50000ms", "no_budget". Range: non-empty.</summary>
        public string Text { get; }

        /// <summary>Builds a notice.</summary>
        public LockstepNotice(NoticeKind kind, int tick, PeerId peer, PlayerId player, ulong localHash, ulong remoteHash, string text)
        {
            Kind = kind;
            Tick = tick;
            Peer = peer;
            Player = player;
            LocalHash = localHash;
            RemoteHash = remoteHash;
            Text = text ?? string.Empty;
        }

        /// <summary>One line for logs, such as "desynced @t40 N1 local=... remote=...".</summary>
        public override string ToString()
        {
            return Kind.ToString().ToLowerInvariant() + " @t" + Tick + " " + Peer + " " + Player + " " + Text
                + (Kind == NoticeKind.Desynced ? " local=" + LocalHash.ToString("x16") + " remote=" + RemoteHash.ToString("x16") : string.Empty);
        }
    }
}

// rts/src/Net/ITransport.cs
// The transport contract the lockstep protocol runs over, and the events a transport reports. Part of Rts.Net (ADR-008).
// The protocol never names Steam: the Unity project adapts Steam networking sockets to this interface, and the tests
// use LoopbackTransport. The contract is reliable, ordered delivery per (sender, receiver) pair, and a connection
// report for every peer that appears or vanishes; the protocol does no retransmission and no timing-based
// disconnect detection of its own. Delivery is pull-based (TryReceive) so the protocol stays single-threaded: the
// host drains the transport inside LockstepPeer.Update on its own thread.
using System.Collections.Generic;

namespace Rts.Net
{
    /// <summary>What one pulled transport event is.</summary>
    public enum TransportEventKind : byte
    {
        /// <summary>A payload from a peer; <see cref="TransportEvent.Payload"/> holds the bytes exactly as sent.</summary>
        Message = 0,

        /// <summary>A peer became reachable (joined the session, or came back after a disconnect).</summary>
        PeerConnected = 1,

        /// <summary>A peer became unreachable. Payloads it sent but that were not yet delivered are lost.</summary>
        PeerDisconnected = 2,
    }

    /// <summary>One event pulled from a transport: a message or a connection change, with the peer it concerns.</summary>
    public readonly struct TransportEvent
    {
        /// <summary>Which kind of event this is.</summary>
        public readonly TransportEventKind Kind;

        /// <summary>The sender of a message, or the peer that connected or disconnected.</summary>
        public readonly PeerId Peer;

        /// <summary>The message bytes for <see cref="TransportEventKind.Message"/>; null for connection events. Range: any length, owned by the receiver.</summary>
        public readonly byte[]? Payload;

        /// <summary>Builds an event.</summary>
        /// <param name="kind">The event kind.</param>
        /// <param name="peer">The peer concerned.</param>
        /// <param name="payload">Message bytes, or null for a connection event.</param>
        public TransportEvent(TransportEventKind kind, PeerId peer, byte[]? payload)
        {
            Kind = kind;
            Peer = peer;
            Payload = payload;
        }
    }

    /// <summary>
    /// Reliable, ordered, peer-to-peer message delivery. One instance per local peer. Implementations must deliver every
    /// payload sent between two connected peers exactly once and in the order sent, report <see cref="TransportEventKind.PeerConnected"/>
    /// and <see cref="TransportEventKind.PeerDisconnected"/> for every change, and never call back into the protocol:
    /// everything is pulled with <see cref="TryReceive"/>.
    /// </summary>
    public interface ITransport
    {
        /// <summary>The peer this transport belongs to.</summary>
        PeerId LocalPeer { get; }

        /// <summary>Peers currently reachable, in ascending ID order, never including <see cref="LocalPeer"/>.</summary>
        IReadOnlyList<PeerId> ConnectedPeers { get; }

        /// <summary>True when <paramref name="peer"/> is currently reachable.</summary>
        /// <param name="peer">The peer to check.</param>
        bool IsConnected(PeerId peer);

        /// <summary>Sends <paramref name="payload"/> to one peer. Returns false (and sends nothing) when the peer is not connected.</summary>
        /// <param name="to">The receiving peer.</param>
        /// <param name="payload">Message bytes; the transport must not keep a reference the caller could later change.</param>
        bool Send(PeerId to, byte[] payload);

        /// <summary>Sends <paramref name="payload"/> to every connected peer, in ascending peer order.</summary>
        /// <param name="payload">Message bytes.</param>
        void Broadcast(byte[] payload);

        /// <summary>Pulls the next event, oldest first. Returns false when nothing is waiting.</summary>
        /// <param name="evt">The event when one was waiting.</param>
        bool TryReceive(out TransportEvent evt);
    }
}

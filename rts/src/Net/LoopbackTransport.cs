// rts/src/Net/LoopbackTransport.cs
// In-process transport for tests and local multi-peer runs. Part of Rts.Net (ADR-008). A LoopbackNetwork is the hub:
// it owns one LoopbackTransport endpoint per peer, a host-tick clock the test advances by hand, and the knobs that
// shape delivery (latency in host ticks, a reorder window, a drop rate). Everything is deterministic: delivery order
// is (deliver-at tick, send sequence), and the reorder and drop decisions come from a DeterministicRandom seeded by
// the test. Reorder and drop are off by default; with both off the hub meets the ITransport contract (reliable,
// ordered). Reorder keeps every message but shuffles arrival within a window; drop makes the hub unreliable and
// exists only for tests of a future retransmission layer, since the lockstep protocol itself assumes reliability.
// A per-link latency (SetLinkLatency) models one slow direction between two peers, and a silent disconnect
// (Disconnect with reportLoss false) models a crash the transport has not noticed when the peer comes back.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Net
{
    /// <summary>The hub that connects loopback endpoints. One per test session.</summary>
    public sealed class LoopbackNetwork
    {
        /// <summary>One queued delivery. Internal so the endpoint's inbox can hold it.</summary>
        internal sealed class Pending
        {
            /// <summary>Host tick at or after which the receiver may pull it. Unit: host ticks. Range: 0 upward.</summary>
            public int DeliverAt;

            /// <summary>Send order, the tie-break between deliveries due on the same tick. Range: 0 upward, unique.</summary>
            public long Sequence;

            /// <summary>The event to hand over.</summary>
            public TransportEvent Event;
        }

        /// <summary>Endpoints by peer, ascending. An endpoint stays listed after a disconnect until replaced.</summary>
        private readonly SortedDictionary<PeerId, LoopbackTransport> _endpoints = new SortedDictionary<PeerId, LoopbackTransport>();

        /// <summary>Random source for the reorder and drop knobs. Seeded by the constructor.</summary>
        private readonly DeterministicRandom _rng;

        /// <summary>Latency overrides per directed link, keyed (sender id shifted by 8) | receiver id. Unit: host ticks. Range: 0 upward.</summary>
        private readonly SortedDictionary<int, int> _linkLatency = new SortedDictionary<int, int>();

        /// <summary>Next send sequence number. Range: 0 upward.</summary>
        private long _nextSequence;

        /// <summary>Messages sent so far, for the drop knob. Range: 0 upward.</summary>
        private long _sendCount;

        /// <summary>The hub's clock. Unit: host ticks. Range: 0 upward; advanced only by <see cref="Advance"/>.</summary>
        public int Now { get; private set; }

        /// <summary>Delay added to every delivery (connection events, and messages on links without a <see cref="SetLinkLatency"/> override). Unit: host ticks. Range: 0 upward; default 0 (delivered on the next TryReceive).</summary>
        public int LatencyTicks { get; set; }

        /// <summary>Extra random delay of 0 to this many host ticks per message, so messages can overtake each other. Unit: host ticks. Range: 0 (off, default) upward.</summary>
        public int ReorderWindowTicks { get; set; }

        /// <summary>When above 0, every Nth sent message is dropped. Unit: messages. Range: 0 (off, default) or 2 upward; 1 would drop everything.</summary>
        public int DropOneInEvery { get; set; }

        /// <summary>Messages sent and not yet pulled by their receivers. Range: 0 upward.</summary>
        public int InFlightCount { get; private set; }

        /// <summary>Messages the drop knob discarded. Range: 0 upward.</summary>
        public int DroppedCount { get; private set; }

        /// <summary>Creates an empty hub at host tick 0.</summary>
        /// <param name="seed">Seed for the reorder and drop decisions; any ulong.</param>
        public LoopbackNetwork(ulong seed = 1)
        {
            _rng = new DeterministicRandom(seed);
        }

        /// <summary>
        /// Creates (or, after a disconnect, re-creates) the endpoint for <paramref name="peer"/> and connects it to every
        /// online endpoint: both sides receive <see cref="TransportEventKind.PeerConnected"/> after the latency. Throws when
        /// the peer already has an online endpoint.
        /// </summary>
        /// <param name="peer">A valid peer ID.</param>
        public LoopbackTransport Connect(PeerId peer)
        {
            if (!peer.IsValid)
            {
                throw new ArgumentException("A loopback endpoint needs a valid peer id.", nameof(peer));
            }
            if (_endpoints.TryGetValue(peer, out LoopbackTransport? existing) && existing.IsOnline)
            {
                throw new InvalidOperationException("Peer " + peer + " already has an online endpoint; disconnect it first.");
            }
            // The new endpoint, replacing any dead one.
            LoopbackTransport endpoint = new LoopbackTransport(this, peer);
            _endpoints[peer] = endpoint;
            foreach (LoopbackTransport other in _endpoints.Values)
            {
                if (other.Peer == peer || !other.IsOnline)
                {
                    continue;
                }
                other.AddLink(peer);
                endpoint.AddLink(other.Peer);
                Enqueue(other, new TransportEvent(TransportEventKind.PeerConnected, peer, null), false, LatencyTicks);
                Enqueue(endpoint, new TransportEvent(TransportEventKind.PeerConnected, other.Peer, null), false, LatencyTicks);
            }
            return endpoint;
        }

        /// <summary>
        /// Takes <paramref name="peer"/> offline: its endpoint stops sending and receiving, its undelivered inbox is lost,
        /// and every other online endpoint receives <see cref="TransportEventKind.PeerDisconnected"/> after the latency.
        /// Messages it sent that are still in flight to others are lost too, like a crashed process's unsent reliable data.
        /// With <paramref name="reportLoss"/> false the others are not told and keep the peer listed as connected (sends to
        /// it are swallowed): the crash the transport has not detected yet; a later <see cref="Connect"/> of the same peer
        /// supersedes the dead connection and reports only <see cref="TransportEventKind.PeerConnected"/>, as the ITransport
        /// contract requires.
        /// </summary>
        /// <param name="peer">A connected peer.</param>
        /// <param name="reportLoss">True (default) to report the loss to every other peer; false for an undetected crash.</param>
        public void Disconnect(PeerId peer, bool reportLoss = true)
        {
            if (!_endpoints.TryGetValue(peer, out LoopbackTransport? endpoint) || !endpoint.IsOnline)
            {
                throw new InvalidOperationException("Peer " + peer + " has no online endpoint.");
            }
            endpoint.GoOffline();
            foreach (LoopbackTransport other in _endpoints.Values)
            {
                if (other.Peer == peer || !other.IsOnline)
                {
                    continue;
                }
                InFlightCount -= other.DropInboxFrom(peer);
                if (reportLoss)
                {
                    other.RemoveLink(peer);
                    Enqueue(other, new TransportEvent(TransportEventKind.PeerDisconnected, peer, null), false, LatencyTicks);
                }
            }
        }

        /// <summary>
        /// Sets the latency of messages from <paramref name="from"/> to <paramref name="to"/> (one direction), replacing
        /// <see cref="LatencyTicks"/> for that link. Set it before the peers play: changing it while messages are in flight
        /// lets later messages overtake earlier ones on that link, which the ITransport contract forbids.
        /// </summary>
        /// <param name="from">The sender.</param>
        /// <param name="to">The receiver.</param>
        /// <param name="latencyTicks">The link's latency. Unit: host ticks. Range: 0 upward.</param>
        public void SetLinkLatency(PeerId from, PeerId to, int latencyTicks)
        {
            if (latencyTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(latencyTicks));
            }
            _linkLatency[LinkKey(from, to)] = latencyTicks;
        }

        /// <summary>Key of a directed link for <see cref="_linkLatency"/>. Range: 0 to 65535.</summary>
        private static int LinkKey(PeerId from, PeerId to)
        {
            return (from.Value << 8) | to.Value;
        }

        /// <summary>Moves the hub clock forward so queued deliveries become due.</summary>
        /// <param name="ticks">Host ticks to advance. Unit: host ticks. Range: 0 upward.</param>
        public void Advance(int ticks = 1)
        {
            if (ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks));
            }
            Now += ticks;
        }

        /// <summary>Queues a message from <paramref name="from"/> to <paramref name="to"/>, applying the latency, reorder and drop knobs. Internal: endpoints call it.</summary>
        /// <param name="from">The sender.</param>
        /// <param name="to">The receiver; must be online.</param>
        /// <param name="payload">The bytes, copied.</param>
        internal void Deliver(PeerId from, PeerId to, byte[] payload)
        {
            if (!_endpoints.TryGetValue(to, out LoopbackTransport? receiver) || !receiver.IsOnline)
            {
                return;
            }
            _sendCount++;
            if (DropOneInEvery > 0 && _sendCount % DropOneInEvery == 0)
            {
                DroppedCount++;
                return;
            }
            // Private copy so the sender cannot change the bytes after sending.
            byte[] copy = new byte[payload.Length];
            Array.Copy(payload, copy, payload.Length);
            Enqueue(receiver, new TransportEvent(TransportEventKind.Message, from, copy), true, _linkLatency.TryGetValue(LinkKey(from, to), out int link) ? link : LatencyTicks);
        }

        /// <summary>Queues an event into an endpoint's inbox at Now + latency (+ reorder jitter for messages).</summary>
        /// <param name="receiver">The endpoint.</param>
        /// <param name="evt">The event.</param>
        /// <param name="jitter">True to apply the reorder window (messages only; connection events are never reordered).</param>
        /// <param name="latency">Latency of this delivery: the link's override for a message, else <see cref="LatencyTicks"/>. Unit: host ticks.</param>
        private void Enqueue(LoopbackTransport receiver, TransportEvent evt, bool jitter, int latency)
        {
            // Delivery tick. Unit: host ticks. Range: Now + latency upward.
            int deliverAt = Now + latency;
            if (jitter && ReorderWindowTicks > 0)
            {
                deliverAt += _rng.NextInt(ReorderWindowTicks + 1);
            }
            receiver.Inbox.Add(new Pending { DeliverAt = deliverAt, Sequence = _nextSequence++, Event = evt });
            if (evt.Kind == TransportEventKind.Message)
            {
                InFlightCount++;
            }
        }

        /// <summary>Pulls the oldest due event from an inbox. Internal: endpoints call it.</summary>
        /// <param name="endpoint">The endpoint pulling.</param>
        /// <param name="evt">The event when one is due.</param>
        internal bool TryDequeue(LoopbackTransport endpoint, out TransportEvent evt)
        {
            // Index of the best due entry: lowest DeliverAt, then lowest Sequence. -1 when none is due.
            int best = -1;
            // Index into the inbox. Range: 0 to Count - 1.
            for (int i = 0; i < endpoint.Inbox.Count; i++)
            {
                // The entry being compared.
                Pending candidate = endpoint.Inbox[i];
                if (candidate.DeliverAt > Now)
                {
                    continue;
                }
                if (best < 0 || candidate.DeliverAt < endpoint.Inbox[best].DeliverAt
                    || (candidate.DeliverAt == endpoint.Inbox[best].DeliverAt && candidate.Sequence < endpoint.Inbox[best].Sequence))
                {
                    best = i;
                }
            }
            if (best < 0)
            {
                evt = default;
                return false;
            }
            evt = endpoint.Inbox[best].Event;
            endpoint.Inbox.RemoveAt(best);
            if (evt.Kind == TransportEventKind.Message)
            {
                InFlightCount--;
            }
            return true;
        }

        /// <summary>The in-process endpoint of one peer. Create through <see cref="LoopbackNetwork.Connect"/>.</summary>
        public sealed class LoopbackTransport : ITransport
        {
            /// <summary>The hub this endpoint belongs to.</summary>
            private readonly LoopbackNetwork _network;

            /// <summary>Peers this endpoint is linked to, ascending.</summary>
            private readonly SortedSet<PeerId> _links = new SortedSet<PeerId>();

            /// <summary>Snapshot of <see cref="_links"/> handed out by <see cref="ConnectedPeers"/>; rebuilt when links change.</summary>
            private IReadOnlyList<PeerId> _linkList = new PeerId[0];

            /// <summary>Queued deliveries, unordered; <see cref="LoopbackNetwork.TryDequeue"/> picks the due one with the lowest key.</summary>
            internal List<Pending> Inbox { get; } = new List<Pending>();

            /// <summary>This endpoint's peer.</summary>
            public PeerId Peer { get; }

            /// <summary>False after <see cref="LoopbackNetwork.Disconnect"/>: sends are ignored and nothing is received.</summary>
            public bool IsOnline { get; private set; } = true;

            /// <summary>Messages this endpoint has sent (per destination counted once). Range: 0 upward.</summary>
            public long SentCount { get; private set; }

            /// <summary>Builds the endpoint; the hub links it.</summary>
            /// <param name="network">The hub.</param>
            /// <param name="peer">This endpoint's peer.</param>
            internal LoopbackTransport(LoopbackNetwork network, PeerId peer)
            {
                _network = network;
                Peer = peer;
            }

            /// <summary>The peer this transport belongs to.</summary>
            public PeerId LocalPeer => Peer;

            /// <summary>Linked peers in ascending order.</summary>
            public IReadOnlyList<PeerId> ConnectedPeers => _linkList;

            /// <summary>True when linked to <paramref name="peer"/>.</summary>
            public bool IsConnected(PeerId peer)
            {
                return IsOnline && _links.Contains(peer);
            }

            /// <summary>Queues the payload for one linked peer; false when not linked or offline. A payload over <see cref="TransportLimits.MaxPayloadBytes"/> throws, as the contract allows.</summary>
            public bool Send(PeerId to, byte[] payload)
            {
                if (payload == null)
                {
                    throw new ArgumentNullException(nameof(payload));
                }
                if (payload.Length > TransportLimits.MaxPayloadBytes)
                {
                    throw new ArgumentException("Payload of " + payload.Length + " bytes exceeds the transport limit of " + TransportLimits.MaxPayloadBytes + ".", nameof(payload));
                }
                if (!IsConnected(to))
                {
                    return false;
                }
                SentCount++;
                _network.Deliver(Peer, to, payload);
                return true;
            }

            /// <summary>Queues the payload for every linked peer in ascending order.</summary>
            public void Broadcast(byte[] payload)
            {
                foreach (PeerId to in _links)
                {
                    Send(to, payload);
                }
            }

            /// <summary>Pulls the oldest due event; false when offline or nothing is due.</summary>
            public bool TryReceive(out TransportEvent evt)
            {
                if (!IsOnline)
                {
                    evt = default;
                    return false;
                }
                return _network.TryDequeue(this, out evt);
            }

            /// <summary>Records a link to <paramref name="peer"/>. Internal: the hub maintains links.</summary>
            internal void AddLink(PeerId peer)
            {
                _links.Add(peer);
                _linkList = new List<PeerId>(_links);
            }

            /// <summary>Removes the link to <paramref name="peer"/>. Internal: the hub maintains links.</summary>
            internal void RemoveLink(PeerId peer)
            {
                _links.Remove(peer);
                _linkList = new List<PeerId>(_links);
            }

            /// <summary>Discards undelivered messages from <paramref name="peer"/> and returns how many were messages. Internal: the hub calls it on a disconnect.</summary>
            internal int DropInboxFrom(PeerId peer)
            {
                // Messages removed. Range: 0 upward.
                int removed = 0;
                // Index into the inbox, walked backwards so removals do not skip entries.
                for (int i = Inbox.Count - 1; i >= 0; i--)
                {
                    if (Inbox[i].Event.Kind == TransportEventKind.Message && Inbox[i].Event.Peer == peer)
                    {
                        Inbox.RemoveAt(i);
                        removed++;
                    }
                }
                return removed;
            }

            /// <summary>Takes the endpoint offline: links and inbox are dropped. Internal: the hub calls it.</summary>
            internal void GoOffline()
            {
                IsOnline = false;
                _links.Clear();
                _linkList = new PeerId[0];
                // In-flight messages to this endpoint are lost with it.
                for (int i = Inbox.Count - 1; i >= 0; i--)
                {
                    if (Inbox[i].Event.Kind == TransportEventKind.Message)
                    {
                        _network.InFlightCount--;
                    }
                }
                Inbox.Clear();
            }
        }
    }
}

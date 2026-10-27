// rts/tests/Net.Tests/LoopbackTransportTests.cs
// Tests for the in-process transport hub: latency in host ticks, ordered delivery, connection events on connect and
// disconnect, loss of in-flight messages on a disconnect, and the reorder and drop knobs. Part of Rts.Net.Tests.
using System.Collections.Generic;
using NUnit.Framework;

namespace Rts.Net.Tests
{
    /// <summary>LoopbackNetwork and its endpoints.</summary>
    [TestFixture]
    public class LoopbackTransportTests
    {
        /// <summary>Peer 0.</summary>
        private static readonly PeerId N0 = new PeerId(0);

        /// <summary>Peer 1.</summary>
        private static readonly PeerId N1 = new PeerId(1);

        /// <summary>Pulls every due event of a transport.</summary>
        private static List<TransportEvent> Drain(ITransport transport)
        {
            // Events pulled, in order.
            List<TransportEvent> events = new List<TransportEvent>();
            while (transport.TryReceive(out TransportEvent evt))
            {
                events.Add(evt);
            }
            return events;
        }

        /// <summary>Messages wait the latency, arrive in send order, and connecting reports both sides.</summary>
        [Test]
        public void Latency_And_Order()
        {
            // The hub under test.
            LoopbackNetwork network = new LoopbackNetwork { LatencyTicks = 2 };
            // The two endpoints.
            ITransport a = network.Connect(N0);
            ITransport b = network.Connect(N1);
            Assert.That(a.ConnectedPeers, Is.EqualTo(new[] { N1 }));
            Assert.That(b.IsConnected(N0), Is.True);
            Assert.That(a.Send(N1, new byte[] { 1 }), Is.True);
            a.Broadcast(new byte[] { 2 });
            Assert.That(a.Send(new PeerId(5), new byte[] { 3 }), Is.False, "unknown peer");
            Assert.That(Drain(b), Is.Empty, "nothing before the latency passes");
            network.Advance(1);
            Assert.That(Drain(b), Is.Empty);
            network.Advance(1);
            // Events after the latency: the connection report, then the two messages in order.
            List<TransportEvent> events = Drain(b);
            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events[0].Kind, Is.EqualTo(TransportEventKind.PeerConnected));
            Assert.That(events[0].Peer, Is.EqualTo(N0));
            Assert.That(events[1].Payload, Is.EqualTo(new byte[] { 1 }));
            Assert.That(events[2].Payload, Is.EqualTo(new byte[] { 2 }));
            Assert.That(network.InFlightCount, Is.EqualTo(0));
        }

        /// <summary>A disconnect reports to the others, loses in-flight messages, and a reconnect gives a fresh endpoint.</summary>
        [Test]
        public void Disconnect_ReportsAndLosesInFlight()
        {
            // The hub under test.
            LoopbackNetwork network = new LoopbackNetwork { LatencyTicks = 1 };
            // The two endpoints.
            ITransport a = network.Connect(N0);
            LoopbackNetwork.LoopbackTransport b = network.Connect(N1);
            network.Advance(1);
            Drain(a);
            Drain(b);
            b.Send(N0, new byte[] { 9 });
            network.Disconnect(N1);
            Assert.That(b.IsOnline, Is.False);
            Assert.That(b.Send(N0, new byte[] { 1 }), Is.False);
            Assert.That(a.ConnectedPeers, Is.Empty);
            network.Advance(1);
            // What a sees: only the disconnect, the in-flight message is gone.
            List<TransportEvent> events = Drain(a);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(TransportEventKind.PeerDisconnected));
            Assert.That(() => network.Disconnect(N1), Throws.InvalidOperationException);
            // The replacement endpoint.
            ITransport b2 = network.Connect(N1);
            Assert.That(b2.IsConnected(N0), Is.True);
            network.Advance(1);
            Assert.That(Drain(a)[0].Kind, Is.EqualTo(TransportEventKind.PeerConnected));
            Assert.That(Drain(b2)[0].Kind, Is.EqualTo(TransportEventKind.PeerConnected));
        }

        /// <summary>With the reorder knob every message still arrives, possibly out of order; with the drop knob some are lost.</summary>
        [Test]
        public void ReorderAndDropKnobs()
        {
            // A hub that shuffles arrivals within three ticks.
            LoopbackNetwork reorder = new LoopbackNetwork(seed: 7) { ReorderWindowTicks = 3 };
            // The two endpoints.
            ITransport a = reorder.Connect(N0);
            ITransport b = reorder.Connect(N1);
            // Message index. Range: 0 to 19.
            for (byte i = 0; i < 20; i++)
            {
                a.Send(N1, new[] { i });
            }
            reorder.Advance(3);
            // Payload bytes in arrival order.
            List<byte> arrived = new List<byte>();
            foreach (TransportEvent evt in Drain(b))
            {
                if (evt.Kind == TransportEventKind.Message)
                {
                    arrived.Add(evt.Payload![0]);
                }
            }
            Assert.That(arrived.Count, Is.EqualTo(20), "nothing lost");
            // True when some message overtook another.
            bool shuffled = false;
            for (int i = 1; i < arrived.Count; i++)
            {
                shuffled |= arrived[i] < arrived[i - 1];
            }
            Assert.That(shuffled, Is.True, "the window reorders");
            // A hub that drops every fourth message.
            LoopbackNetwork drop = new LoopbackNetwork { DropOneInEvery = 4 };
            ITransport c = drop.Connect(N0);
            ITransport d = drop.Connect(N1);
            for (byte i = 0; i < 8; i++)
            {
                c.Send(N1, new[] { i });
            }
            Assert.That(drop.DroppedCount, Is.EqualTo(2));
            // Messages that arrived at d.
            int received = 0;
            foreach (TransportEvent evt in Drain(d))
            {
                received += evt.Kind == TransportEventKind.Message ? 1 : 0;
            }
            Assert.That(received, Is.EqualTo(6));
        }
    }
}

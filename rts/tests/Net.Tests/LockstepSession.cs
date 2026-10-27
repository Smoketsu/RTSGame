// rts/tests/Net.Tests/LockstepSession.cs
// Test host for lockstep sessions: a loopback network, one LockstepPeer per peer with a skirmish AI controller for
// each player it controls (wired through BeforeStep and a sink that schedules for NextInputTick), a run loop that
// updates every peer by one host tick (50 ms) per iteration, and helpers to disconnect, reconnect and read notices.
// Part of Rts.Net.Tests. This is what the Unity host loop will look like minus rendering and the Steam transport.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Ai;
using Rts.Net.Codecs;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Net.Tests
{
    /// <summary>Schedules AI commands for the peer's next input tick and hands them to the peer.</summary>
    public sealed class LockstepCommandSink : ICommandSink
    {
        /// <summary>The peer commands go to.</summary>
        private readonly LockstepPeer _peer;

        /// <summary>Wraps a peer.</summary>
        /// <param name="peer">The local peer.</param>
        public LockstepCommandSink(LockstepPeer peer)
        {
            _peer = peer;
        }

        /// <summary>The peer's next input tick (current tick plus the input delay). Unit: ticks.</summary>
        public int ScheduleTick => _peer.NextInputTick;

        /// <summary>Queues the command in the peer's next bundle.</summary>
        public void Submit(Command command)
        {
            _peer.SubmitLocal(command);
        }
    }

    /// <summary>One peer of the test session with its AI controllers and the notices it raised.</summary>
    public sealed class SessionPeer
    {
        /// <summary>The peer under test.</summary>
        public LockstepPeer Peer { get; }

        /// <summary>Its loopback endpoint.</summary>
        public LoopbackNetwork.LoopbackTransport Transport { get; }

        /// <summary>AI controllers for the players this peer controls, in ascending player order.</summary>
        public List<SkirmishAiController> Controllers { get; } = new List<SkirmishAiController>();

        /// <summary>Every notice the peer raised since it was created.</summary>
        public List<LockstepNotice> Notices { get; } = new List<LockstepNotice>();

        /// <summary>The simulation whose controllers were last started, so a reconnect restarts them on the new one.</summary>
        private Simulation? _startedOn;

        /// <summary>Wires the peer: notices are collected, and BeforeStep runs the AI controllers.</summary>
        /// <param name="peer">The peer.</param>
        /// <param name="transport">Its endpoint.</param>
        /// <param name="ai">True to attach the script's AI profiles to this peer's players.</param>
        public SessionPeer(LockstepPeer peer, LoopbackNetwork.LoopbackTransport transport, bool ai)
        {
            Peer = peer;
            Transport = transport;
            peer.NoticeRaised += Notices.Add;
            if (ai)
            {
                foreach (PlayerId player in peer.Roster.PlayersOf(peer.LocalPeer))
                {
                    if (NetTestData.AiVsAi.AiProfiles.TryGetValue(player, out string? profile))
                    {
                        Controllers.Add(new SkirmishAiController(player, AiProfile.FromDataSet(NetTestData.Data, profile)));
                    }
                }
            }
            peer.BeforeStep += Think;
        }

        /// <summary>Runs every controller for the tick about to be stepped.</summary>
        private void Think(LockstepPeer peer)
        {
            // The simulation the controllers read; a new one after a reconnect.
            Simulation sim = peer.Simulation;
            if (!ReferenceEquals(sim, _startedOn))
            {
                _startedOn = sim;
                foreach (SkirmishAiController controller in Controllers)
                {
                    controller.OnMatchStart(sim);
                }
            }
            // The sink every controller submits through this tick.
            LockstepCommandSink sink = new LockstepCommandSink(peer);
            foreach (SkirmishAiController controller in Controllers)
            {
                controller.Think(sim, sink);
            }
        }

        /// <summary>The notices of one kind, in order.</summary>
        /// <param name="kind">The kind wanted.</param>
        public List<LockstepNotice> NoticesOf(NoticeKind kind)
        {
            // Matching notices.
            List<LockstepNotice> found = new List<LockstepNotice>();
            foreach (LockstepNotice notice in Notices)
            {
                if (notice.Kind == kind)
                {
                    found.Add(notice);
                }
            }
            return found;
        }

        /// <summary>The match outcome of this peer's simulation.</summary>
        public MatchOutcome Outcome => MatchOutcome.Evaluate(Peer.Simulation.Players);

        /// <summary>The current tick of this peer's simulation. Unit: ticks.</summary>
        public int Tick => Peer.Simulation.Clock.Tick;
    }

    /// <summary>A whole test session: network, roster, settings and peers.</summary>
    public sealed class LockstepSession
    {
        /// <summary>Peer 0, which controls player 0 (Skai).</summary>
        public static readonly PeerId N0 = new PeerId(0);

        /// <summary>Peer 1, which controls player 1 (Water).</summary>
        public static readonly PeerId N1 = new PeerId(1);

        /// <summary>Player 0.</summary>
        public static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1.</summary>
        public static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Host milliseconds per iteration: one simulation tick at 20 ticks per second. Unit: milliseconds.</summary>
        public const int StepMs = 50;

        /// <summary>The loopback hub.</summary>
        public LoopbackNetwork Network { get; }

        /// <summary>The two-peer roster.</summary>
        public LockstepRoster Roster { get; }

        /// <summary>The protocol limits from the shipped online.json.</summary>
        public LockstepSettings Settings { get; }

        /// <summary>The codec registry every peer uses.</summary>
        public CommandCodecRegistry Codecs { get; } = BuiltInCodecs.CreateRegistry();

        /// <summary>Peers by id; a reconnected peer replaces its entry.</summary>
        public SortedDictionary<PeerId, SessionPeer> Peers { get; } = new SortedDictionary<PeerId, SessionPeer>();

        /// <summary>Whether peers get AI controllers.</summary>
        private readonly bool _ai;

        /// <summary>Builds the roster and network; call <see cref="Start"/> to create the peers.</summary>
        /// <param name="latencyTicks">Loopback latency. Unit: host ticks.</param>
        /// <param name="ai">True to attach the script's AI profiles.</param>
        /// <param name="settings">Protocol limits; null for the shipped tuning.</param>
        public LockstepSession(int latencyTicks = 1, bool ai = true, LockstepSettings? settings = null)
        {
            Network = new LoopbackNetwork(seed: 42) { LatencyTicks = latencyTicks };
            Roster = new LockstepRoster();
            Roster.Add(N0, P0);
            Roster.Add(N1, P1);
            Settings = settings ?? LockstepSettings.FromTuning(NetTestData.Data.Tuning.Online);
            _ai = ai;
        }

        /// <summary>Creates and starts both peers.</summary>
        public void Start()
        {
            foreach (PeerId id in Roster.Peers)
            {
                // The endpoint and peer for this id.
                LoopbackNetwork.LoopbackTransport transport = Network.Connect(id);
                LockstepPeer peer = new LockstepPeer(Roster, id, transport, NetTestData.CreateAiVsAiSimulation, Codecs, Settings);
                Peers[id] = new SessionPeer(peer, transport, _ai);
            }
            foreach (SessionPeer peer in Peers.Values)
            {
                peer.Peer.Start();
            }
        }

        /// <summary>Peer by id.</summary>
        public SessionPeer this[PeerId id] => Peers[id];

        /// <summary>One host tick: every online peer updates by <see cref="StepMs"/> and the network advances one tick.</summary>
        public void Iterate()
        {
            foreach (SessionPeer peer in Peers.Values)
            {
                if (peer.Transport.IsOnline)
                {
                    peer.Peer.Update(StepMs);
                }
            }
            Network.Advance(1);
        }

        /// <summary>Iterates until <paramref name="done"/> holds or <paramref name="maxIterations"/> pass; returns the iterations used.</summary>
        /// <param name="done">The stop condition, checked after each iteration.</param>
        /// <param name="maxIterations">Safety cap. Unit: iterations.</param>
        public int RunUntil(Func<bool> done, int maxIterations)
        {
            // Iterations run so far.
            int iterations = 0;
            while (iterations < maxIterations && !done())
            {
                Iterate();
                iterations++;
            }
            return iterations;
        }

        /// <summary>Iterates until every online peer's simulation has reached <paramref name="tick"/>; fails the test if it takes too long.</summary>
        /// <param name="tick">The target tick. Unit: ticks.</param>
        public void RunToTick(int tick)
        {
            // Iterations used.
            int used = RunUntil(() =>
            {
                foreach (SessionPeer peer in Peers.Values)
                {
                    if (peer.Transport.IsOnline && (!peer.Peer.HasSimulation || peer.Tick < tick))
                    {
                        return false;
                    }
                }
                return true;
            }, tick * 4 + 400);
            foreach (SessionPeer peer in Peers.Values)
            {
                if (peer.Transport.IsOnline)
                {
                    Assert.That(peer.Tick, Is.GreaterThanOrEqualTo(tick), peer.Peer.LocalPeer + " reached tick " + tick + " in " + used + " iterations (phase " + peer.Peer.Phase + ")");
                }
            }
        }

        /// <summary>Takes a peer offline (its process died).</summary>
        /// <param name="id">The peer.</param>
        public void Disconnect(PeerId id)
        {
            Network.Disconnect(id);
        }

        /// <summary>Brings a peer back as a fresh instance that joins the running session.</summary>
        /// <param name="id">The peer.</param>
        public SessionPeer Reconnect(PeerId id)
        {
            // The new endpoint and peer instance.
            LoopbackNetwork.LoopbackTransport transport = Network.Connect(id);
            LockstepPeer peer = new LockstepPeer(Roster, id, transport, NetTestData.CreateAiVsAiSimulation, Codecs, Settings);
            // The replacement session peer.
            SessionPeer session = new SessionPeer(peer, transport, _ai);
            Peers[id] = session;
            peer.Join();
            return session;
        }

        /// <summary>Fails the test if any peer reported a desync.</summary>
        public void AssertNoDesync()
        {
            foreach (SessionPeer peer in Peers.Values)
            {
                Assert.That(peer.NoticesOf(NoticeKind.Desynced), Is.Empty, peer.Peer.LocalPeer + " desynced");
                Assert.That(peer.Peer.Phase, Is.Not.EqualTo(LockstepPhase.Desynced));
            }
        }

        /// <summary>Updates only whichever peer is behind until both simulations stand on the same tick (the leader has already sent the bundles the laggard needs), then fails the test unless they hash identically.</summary>
        public void AssertSameState()
        {
            // Iterations spent levelling the two peers. Range: 0 to the cap.
            int levelling = 0;
            while (this[N0].Tick != this[N1].Tick && levelling < 200)
            {
                // The peer that is behind.
                SessionPeer laggard = this[N0].Tick < this[N1].Tick ? this[N0] : this[N1];
                laggard.Peer.Update(StepMs);
                Network.Advance(1);
                levelling++;
            }
            Assert.That(this[N1].Tick, Is.EqualTo(this[N0].Tick), "same tick after levelling");
            Assert.That(this[N1].Peer.Simulation.ComputeStateHash(), Is.EqualTo(this[N0].Peer.Simulation.ComputeStateHash()), "same state");
        }
    }
}

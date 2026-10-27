// rts/tests/Net.Tests/LockstepSession.cs
// Test host for lockstep sessions: a loopback network, one LockstepPeer per peer (any peer count; peer i controls
// player i) with a skirmish AI controller for each player that has a profile (wired through BeforeStep and a sink
// that schedules for NextInputTick), a run loop that updates every peer by one host tick (50 ms) per iteration, and
// helpers to disconnect, reconnect, stall a peer's host, connect an endpoint without joining, and read notices.
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

        /// <summary>True while the test holds this peer's host loop still (no Update), as a frozen or slow machine; the endpoint stays online and buffers.</summary>
        public bool Stalled { get; set; }

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

        /// <summary>True when the session loop updates this peer: online, not stalled, and started or joined.</summary>
        public bool IsActive => Transport.IsOnline && !Stalled && Peer.Phase != LockstepPhase.Created;

        /// <summary>The match outcome of this peer's simulation.</summary>
        public MatchOutcome Outcome => MatchOutcome.Evaluate(Peer.Simulation.Players);

        /// <summary>The current tick of this peer's simulation. Unit: ticks.</summary>
        public int Tick => Peer.Simulation.Clock.Tick;

        /// <summary>Highest tick of a bundle from <paramref name="peer"/> in this peer's command log, or -1. Unit: ticks.</summary>
        /// <param name="peer">The bundles' sender.</param>
        public int HighestBundleTickFrom(PeerId peer)
        {
            // The highest tick seen. Range: -1 upward.
            int highest = -1;
            foreach (CommandBundleMessage bundle in Peer.CommandLog)
            {
                if (bundle.Peer == peer && bundle.Tick > highest)
                {
                    highest = bundle.Tick;
                }
            }
            return highest;
        }
    }

    /// <summary>A whole test session: network, roster, settings and peers.</summary>
    public sealed class LockstepSession
    {
        /// <summary>Peer 0, which controls player 0 (Skai, aggressive AI).</summary>
        public static readonly PeerId N0 = new PeerId(0);

        /// <summary>Peer 1, which controls player 1 (Water, defensive AI).</summary>
        public static readonly PeerId N1 = new PeerId(1);

        /// <summary>Peer 2, which controls player 2 (idle, team 0) in sessions of three or more peers.</summary>
        public static readonly PeerId N2 = new PeerId(2);

        /// <summary>Peer 3, which controls player 3 (idle, team 1) in sessions of four peers.</summary>
        public static readonly PeerId N3 = new PeerId(3);

        /// <summary>Player 0.</summary>
        public static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1.</summary>
        public static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Player 2.</summary>
        public static readonly PlayerId P2 = new PlayerId(2);

        /// <summary>Player 3.</summary>
        public static readonly PlayerId P3 = new PlayerId(3);

        /// <summary>Host milliseconds per iteration: one simulation tick at 20 ticks per second. Unit: milliseconds.</summary>
        public const int StepMs = 50;

        /// <summary>The loopback hub.</summary>
        public LoopbackNetwork Network { get; }

        /// <summary>The roster: peer i controls player i.</summary>
        public LockstepRoster Roster { get; }

        /// <summary>Peers in the roster. Range: 2 upward.</summary>
        public int PeerCount { get; }

        /// <summary>The protocol limits from the shipped online.json.</summary>
        public LockstepSettings Settings { get; }

        /// <summary>The codec registry every peer uses.</summary>
        public CommandCodecRegistry Codecs { get; } = BuiltInCodecs.CreateRegistry();

        /// <summary>Peers by id; a reconnected peer replaces its entry.</summary>
        public SortedDictionary<PeerId, SessionPeer> Peers { get; } = new SortedDictionary<PeerId, SessionPeer>();

        /// <summary>Whether peers get AI controllers.</summary>
        private readonly bool _ai;

        /// <summary>Builds the roster and network; call <see cref="Start"/> to create the peers.</summary>
        /// <param name="peerCount">Peers (and players) in the match. Range: 2 upward.</param>
        /// <param name="latencyTicks">Loopback latency. Unit: host ticks.</param>
        /// <param name="ai">True to attach the script's AI profiles.</param>
        /// <param name="settings">Protocol limits; null for the shipped tuning.</param>
        public LockstepSession(int peerCount = 2, int latencyTicks = 1, bool ai = true, LockstepSettings? settings = null)
        {
            Network = new LoopbackNetwork(seed: 42) { LatencyTicks = latencyTicks };
            Roster = new LockstepRoster();
            PeerCount = peerCount;
            // Index of the peer being added. Range: 0 to peerCount - 1.
            for (int i = 0; i < peerCount; i++)
            {
                Roster.Add(new PeerId((byte)i), new PlayerId((byte)i));
            }
            Settings = settings ?? LockstepSettings.FromTuning(NetTestData.Data.Tuning.Online);
            _ai = ai;
        }

        /// <summary>The match factory every peer uses: the N-player AI-versus-AI match.</summary>
        public LockstepMatch CreateMatch()
        {
            return NetTestData.CreateMatch(PeerCount);
        }

        /// <summary>Creates every endpoint, then every peer, then starts them all (every peer is connected before any Start).</summary>
        /// <param name="configure">Called with each created peer before any Start, to attach hooks such as a replay recorder; null for none.</param>
        public void Start(Action<SessionPeer>? configure = null)
        {
            foreach (PeerId id in Roster.Peers)
            {
                // The created peer, configured before any peer starts.
                SessionPeer created = Create(id);
                configure?.Invoke(created);
            }
            foreach (SessionPeer peer in Peers.Values)
            {
                peer.Peer.Start();
            }
        }

        /// <summary>Creates a fresh endpoint and peer instance for <paramref name="id"/> without starting or joining it.</summary>
        /// <param name="id">The peer.</param>
        private SessionPeer Create(PeerId id)
        {
            // The endpoint and peer for this id.
            LoopbackNetwork.LoopbackTransport transport = Network.Connect(id);
            LockstepPeer peer = new LockstepPeer(Roster, id, transport, CreateMatch, Codecs, Settings);
            // The session peer replacing any earlier instance.
            SessionPeer session = new SessionPeer(peer, transport, _ai);
            Peers[id] = session;
            return session;
        }

        /// <summary>Peer by id.</summary>
        public SessionPeer this[PeerId id] => Peers[id];

        /// <summary>One host tick: every active peer updates by <see cref="StepMs"/> and the network advances one tick.</summary>
        public void Iterate()
        {
            foreach (SessionPeer peer in Peers.Values)
            {
                if (peer.IsActive)
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

        /// <summary>Iterates until every active peer's simulation has reached <paramref name="tick"/>; fails the test if it takes too long.</summary>
        /// <param name="tick">The target tick. Unit: ticks.</param>
        public void RunToTick(int tick)
        {
            // Iterations used.
            int used = RunUntil(() =>
            {
                foreach (SessionPeer peer in Peers.Values)
                {
                    if (peer.IsActive && (!peer.Peer.HasSimulation || peer.Tick < tick))
                    {
                        return false;
                    }
                }
                return true;
            }, tick * 4 + 400);
            foreach (SessionPeer peer in Peers.Values)
            {
                if (peer.IsActive)
                {
                    Assert.That(peer.Tick, Is.GreaterThanOrEqualTo(tick), peer.Peer.LocalPeer + " reached tick " + tick + " in " + used + " iterations (phase " + peer.Peer.Phase + ": " + string.Join("; ", peer.Notices) + ")");
                }
            }
        }

        /// <summary>Takes a peer offline (its process died); the others are told unless <paramref name="reportLoss"/> is false.</summary>
        /// <param name="id">The peer.</param>
        /// <param name="reportLoss">False for a crash the transport never reports (the peer comes back before it is noticed).</param>
        public void Disconnect(PeerId id, bool reportLoss = true)
        {
            Network.Disconnect(id, reportLoss);
        }

        /// <summary>Brings a peer back as a fresh instance that joins the running session.</summary>
        /// <param name="id">The peer.</param>
        public SessionPeer Reconnect(PeerId id)
        {
            // The replacement session peer, joining at once.
            SessionPeer session = Create(id);
            session.Peer.Join();
            return session;
        }

        /// <summary>Brings a peer's endpoint back (so broadcasts reach it) with a fresh peer instance that has not called Join yet; call <see cref="Join"/> later.</summary>
        /// <param name="id">The peer.</param>
        public SessionPeer ConnectWithoutJoining(PeerId id)
        {
            return Create(id);
        }

        /// <summary>Calls Join on a peer created by <see cref="ConnectWithoutJoining"/>.</summary>
        /// <param name="id">The peer.</param>
        public void Join(PeerId id)
        {
            Peers[id].Peer.Join();
        }

        /// <summary>Fails the test if any peer reported a desync or an inconsistency, or failed.</summary>
        public void AssertNoDesync()
        {
            foreach (SessionPeer peer in Peers.Values)
            {
                Assert.That(peer.NoticesOf(NoticeKind.Desynced), Is.Empty, peer.Peer.LocalPeer + " desynced");
                Assert.That(peer.NoticesOf(NoticeKind.Inconsistent), Is.Empty, peer.Peer.LocalPeer + " inconsistent");
                Assert.That(peer.Peer.Phase, Is.Not.EqualTo(LockstepPhase.Desynced).And.Not.EqualTo(LockstepPhase.Failed), peer.Peer.LocalPeer + " phase: " + string.Join("; ", peer.Notices));
            }
        }

        /// <summary>
        /// Updates only the peers that are behind until every live simulation stands on the same tick (the leader has already sent
        /// the bundles the laggards need), then fails the test unless they all hash identically.
        /// </summary>
        public void AssertSameState()
        {
            // The live peers compared: online, started, with a simulation and not out of the session.
            List<SessionPeer> live = new List<SessionPeer>();
            foreach (SessionPeer peer in Peers.Values)
            {
                if (peer.Transport.IsOnline && peer.Peer.HasSimulation && (peer.Peer.Phase == LockstepPhase.Running || peer.Peer.Phase == LockstepPhase.Paused))
                {
                    live.Add(peer);
                }
            }
            Assert.That(live.Count, Is.GreaterThanOrEqualTo(2), "at least two live peers to compare");
            // Iterations spent levelling the peers. Range: 0 to the cap.
            int levelling = 0;
            while (levelling < 400 && !AllAtTick(live, HighestTick(live)))
            {
                // The target tick this round. Unit: ticks.
                int target = HighestTick(live);
                foreach (SessionPeer peer in live)
                {
                    if (peer.Tick < target)
                    {
                        peer.Peer.Update(StepMs);
                    }
                }
                Network.Advance(1);
                levelling++;
            }
            // The reference peer every other peer is compared with.
            SessionPeer first = live[0];
            foreach (SessionPeer peer in live)
            {
                Assert.That(peer.Tick, Is.EqualTo(first.Tick), peer.Peer.LocalPeer + " on the same tick as " + first.Peer.LocalPeer + " after levelling");
                Assert.That(peer.Peer.Simulation.ComputeStateHash(), Is.EqualTo(first.Peer.Simulation.ComputeStateHash()), peer.Peer.LocalPeer + " same state as " + first.Peer.LocalPeer);
            }
        }

        /// <summary>The highest current tick among <paramref name="peers"/>. Unit: ticks.</summary>
        private static int HighestTick(List<SessionPeer> peers)
        {
            // The highest tick seen. Range: 0 upward.
            int highest = 0;
            foreach (SessionPeer peer in peers)
            {
                highest = Math.Max(highest, peer.Tick);
            }
            return highest;
        }

        /// <summary>True when every peer of <paramref name="peers"/> stands on <paramref name="tick"/>.</summary>
        private static bool AllAtTick(List<SessionPeer> peers, int tick)
        {
            foreach (SessionPeer peer in peers)
            {
                if (peer.Tick != tick)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

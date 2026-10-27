// rts/tests/Net.Tests/MultiPeerLockstepTests.cs
// Lockstep sessions of three and four peers over the loopback transport: the cases the Opus determinism review of ADR-008
// found in the referee rules, which only show with a third peer that is neither the referee nor the peer that dropped.
// Each test names the review finding it covers in its comment and fails without that fix. Also here: a three-peer full
// AI-versus-AI match with every hash matching, a referee handover, and an online replay recorded through the peer's hooks.
// Part of Rts.Net.Tests. Peers 2 and 3 control idle players (no units, no AI), so the AI match of peers 0 and 1 plays as
// in the two-peer tests while the extra peers exchange empty bundles and hashes.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Replay;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Net.Tests
{
    /// <summary>Three- and four-peer lockstep sessions.</summary>
    [TestFixture]
    public class MultiPeerLockstepTests
    {
        /// <summary>Peer 0, the referee at start.</summary>
        private static readonly PeerId N0 = LockstepSession.N0;

        /// <summary>Peer 1.</summary>
        private static readonly PeerId N1 = LockstepSession.N1;

        /// <summary>Peer 2.</summary>
        private static readonly PeerId N2 = LockstepSession.N2;

        /// <summary>Peer 3.</summary>
        private static readonly PeerId N3 = LockstepSession.N3;

        /// <summary>Player 0.</summary>
        private static readonly PlayerId P0 = LockstepSession.P0;

        /// <summary>Player 1.</summary>
        private static readonly PlayerId P1 = LockstepSession.P1;

        /// <summary>Player 3.</summary>
        private static readonly PlayerId P3 = LockstepSession.P3;

        /// <summary>The player state of <paramref name="player"/> in a peer's simulation.</summary>
        private static PlayerState StateOf(SessionPeer peer, PlayerId player)
        {
            Assert.That(peer.Peer.Simulation.Players.TryGet(player, out Player? found), Is.True);
            return found!.State;
        }

        /// <summary>A codec over the session's registry, for messages a test injects through a transport endpoint.</summary>
        private static MessageCodec CodecOf(LockstepSession session)
        {
            return new MessageCodec(session.Codecs);
        }

        /// <summary>Disconnects <paramref name="id"/> and waits until the referee has paused for it.</summary>
        private static void DropAndPause(LockstepSession session, PeerId id, SessionPeer referee)
        {
            session.Disconnect(id);
            session.RunUntil(() => referee.Peer.Phase == LockstepPhase.Paused, 20);
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Paused), "paused for " + id);
        }

        /// <summary>Three peers play the AI-versus-AI script to a decided outcome (Skai's HQ falls to Water, as in the two-peer match; peer 2's idle player sits on Water's team) with every exchanged hash equal on every pair and the three final states identical.</summary>
        [Test]
        public void ThreePeers_FullMatch_AllHashesMatch()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 3);
            session.Start();
            Assert.That(session[N0].Peer.IsReferee, Is.True);
            Assert.That(session[N2].Peer.Referee, Is.EqualTo(N0));
            // Iterations used until the match is decided on every peer.
            int used = session.RunUntil(() => session[N0].Outcome.IsDecided && session[N1].Outcome.IsDecided && session[N2].Outcome.IsDecided, 6000 * 2);
            session.AssertNoDesync();
            Assert.That(session[N0].Outcome.IsDecided, Is.True, "decided after " + used + " iterations, tick " + session[N0].Tick);
            Assert.That(session[N0].Outcome.Winner, Is.EqualTo(new TeamId(1)));
            Assert.That(session[N0].Tick, Is.LessThan(6000));
            session.AssertSameState();
            // Hash interval of the match. Unit: ticks.
            int interval = session[N0].Peer.Simulation.Settings.StateHashIntervalTicks;
            foreach (PeerId a in session.Roster.Peers)
            {
                foreach (PeerId b in session.Roster.Peers)
                {
                    if (a != b)
                    {
                        Assert.That(session[a].Peer.ComparedThroughTick(b), Is.GreaterThanOrEqualTo(session[a].Tick - 2 * interval), a + " compared hashes with " + b + " all the way");
                    }
                }
            }
            // Every peer applied the same command log.
            Assert.That(new List<CommandBundleMessage>(session[N2].Peer.CommandLog).Count, Is.EqualTo(new List<CommandBundleMessage>(session[N0].Peer.CommandLog).Count));
        }

        /// <summary>
        /// Review finding 1 (blocker). The rejoin tick must be one the referee has not sent a bundle for, since a third peer can step
        /// every tick the referee's bundles cover: peers 1 and 2 freeze, so the referee runs ahead until it blocks at tick T having
        /// sent its bundle for T + delay; peer 2 catches up to T; peer 1 dies there (cutoff below T + delay) and rejoins. The rejoin
        /// must start at T + delay + 1, above every referee bundle peer 2 held when the announcement reached it; the old rule
        /// (T + delay) names a tick peer 2 already held the referee's bundle for. The loopback delivers every peer the same bundles,
        /// so the race itself cannot be staged here; the test pins the invariant that excludes it.
        /// </summary>
        [Test]
        public void Rejoin_StartsAfterTheRefereesLastSentBundle_ThreePeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 3);
            session.Start();
            session.RunToTick(100);
            // The referee and the input delay. Unit of the delay: ticks.
            SessionPeer referee = session[N0];
            int delay = referee.Peer.Settings.InputDelayTicks;
            // Peers 1 and 2 freeze; the referee steps on with the bundles it holds, then blocks.
            session[N1].Stalled = true;
            session[N2].Stalled = true;
            for (int i = 0; i < 8; i++)
            {
                session.Iterate();
            }
            // The tick the referee is blocked at, its bundle for blockedTick + delay already sent. Unit: ticks.
            int blockedTick = referee.Tick;
            Assert.That(blockedTick, Is.GreaterThan(100));
            // Peer 2's host resumes and catches up to the referee; peer 1's process dies while frozen.
            session[N2].Stalled = false;
            for (int i = 0; i < 6; i++)
            {
                session.Iterate();
            }
            Assert.That(session[N2].Tick, Is.EqualTo(blockedTick));
            Assert.That(session[N2].HighestBundleTickFrom(N0), Is.EqualTo(blockedTick + delay), "peer 2 holds the referee's bundle for its blocked tick plus the delay");
            // The highest referee bundle tick peer 2 held when the rejoin announcement reached it. Unit: ticks. Range: -1 until then.
            int highestAtRejoin = -1;
            session[N2].Peer.NoticeRaised += notice =>
            {
                if (notice.Kind == NoticeKind.PeerRejoined)
                {
                    highestAtRejoin = session[N2].HighestBundleTickFrom(N0);
                }
            };
            DropAndPause(session, N1, referee);
            Assert.That(referee.NoticesOf(NoticeKind.PeerDropped)[0].Tick, Is.LessThan(blockedTick + delay), "the cutoff is below the referee's last sent bundle");
            // The reconnecting peer.
            SessionPeer joiner = session.Reconnect(N1);
            session.RunUntil(() => joiner.Peer.Phase == LockstepPhase.Running && referee.Peer.Phase == LockstepPhase.Running, 400);
            Assert.That(joiner.Peer.Phase, Is.EqualTo(LockstepPhase.Running), string.Join("; ", joiner.Notices));
            // The announced rejoin tick. Unit: ticks.
            int rejoinTick = referee.NoticesOf(NoticeKind.PeerRejoined)[0].Tick;
            Assert.That(rejoinTick, Is.EqualTo(blockedTick + delay + 1), "one past the referee's last sent bundle");
            Assert.That(highestAtRejoin, Is.GreaterThanOrEqualTo(0), "peer 2 saw the rejoin");
            Assert.That(rejoinTick, Is.GreaterThan(highestAtRejoin), "no peer held a referee bundle for the rejoin tick, so none can have stepped it");
            session.RunToTick(rejoinTick + 60);
            session.AssertNoDesync();
            session.AssertSameState();
        }

        /// <summary>Review finding 1: a peer told to rejoin another from a tick it has already stepped past applied an empty bundle where the others apply the joiner's; it reports Inconsistent and stops.</summary>
        [Test]
        public void Rejoin_BehindAPeersTick_IsReportedInconsistent_ThreePeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 3);
            session.Start();
            session.RunToTick(60);
            DropAndPause(session, N1, session[N0]);
            // The announcement a faulty referee would send: peer 1 required again from a tick peer 2 has stepped.
            session[N0].Transport.Send(N2, CodecOf(session).Encode(new PeerRejoinedMessage(N1, session[N2].Tick - 1)));
            session.RunUntil(() => session[N2].NoticesOf(NoticeKind.Inconsistent).Count > 0, 10);
            Assert.That(session[N2].NoticesOf(NoticeKind.Inconsistent).Count, Is.EqualTo(1));
            Assert.That(session[N2].NoticesOf(NoticeKind.Inconsistent)[0].Text, Is.EqualTo("stepped_past_rejoin_tick"));
            Assert.That(session[N2].Peer.Phase, Is.EqualTo(LockstepPhase.Failed));
        }

        /// <summary>
        /// Review finding 3 (major). Hashes of a dropped peer beyond its cutoff are discarded, and hashes it sends while dropped are
        /// ignored: peer 1 reports a hash for a tick far ahead (as a peer that ran on with its own bundles would) and then dies; its
        /// fresh instance reports another while still joining. Neither may desync the referee or peer 2, who step those ticks with
        /// peer 1's bundles treated as empty; the hashes the rejoined peer 1 sends are compared and match.
        /// </summary>
        [Test]
        public void DroppedPeersHashes_AreDiscardedAndIgnored_ThreePeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 3);
            session.Start();
            session.RunToTick(100);
            // The codec for injected messages, the hash interval and a hash tick six intervals ahead. Unit: ticks.
            MessageCodec codec = CodecOf(session);
            int interval = session[N0].Peer.Simulation.Settings.StateHashIntervalTicks;
            int aheadTick = (session[N1].Tick / interval + 6) * interval;
            foreach (PeerId to in new[] { N0, N2 })
            {
                session[N1].Transport.Send(to, codec.Encode(new StateHashMessage(aheadTick, 0xdeadbeefUL)));
            }
            session.Iterate();
            session.Iterate();
            DropAndPause(session, N1, session[N0]);
            // The fresh instance; while it joins it is still a dropped peer and its hashes do not count.
            SessionPeer joiner = session.Reconnect(N1);
            foreach (PeerId to in new[] { N0, N2 })
            {
                joiner.Transport.Send(to, codec.Encode(new StateHashMessage(aheadTick + interval, 0xdeadbeefUL)));
            }
            session.RunUntil(() => joiner.Peer.Phase == LockstepPhase.Running && session[N0].Peer.Phase == LockstepPhase.Running, 400);
            Assert.That(joiner.Peer.Phase, Is.EqualTo(LockstepPhase.Running), string.Join("; ", joiner.Notices));
            session.RunToTick(aheadTick + 2 * interval + 5);
            session.AssertNoDesync();
            Assert.That(session[N0].Peer.ComparedThroughTick(N1), Is.GreaterThanOrEqualTo(aheadTick + interval), "the rejoined peer's real hashes were compared");
            Assert.That(session[N2].Peer.ComparedThroughTick(N1), Is.GreaterThanOrEqualTo(aheadTick + interval));
            session.AssertSameState();
        }

        /// <summary>
        /// Review finding 4 (major), with a referee handover. A disconnect pause is counted in every peer's ledger, not only the
        /// referee's: peer 3 drops (its one pause starts), the referee peer 0 dies during it, peer 1 takes over and peer 3 returns
        /// through peer 1 with the count intact; when peer 3 drops again, peer 1 grants no second pause and marks it disconnected.
        /// </summary>
        [Test]
        public void DisconnectPause_IsCountedOnEveryPeer_SoANewRefereeGrantsNoSecond_FourPeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 4);
            session.Start();
            session.RunToTick(80);
            DropAndPause(session, N3, session[N0]);
            session.RunUntil(() => session[N1].Peer.Phase == LockstepPhase.Paused && session[N2].Peer.Phase == LockstepPhase.Paused, 10);
            Assert.That(session[N0].Peer.Ledger.DisconnectPausesUsed(P3), Is.EqualTo(1), "referee");
            Assert.That(session[N1].Peer.Ledger.DisconnectPausesUsed(P3), Is.EqualTo(1), "peer 1 counted the pause it was told about");
            Assert.That(session[N2].Peer.Ledger.DisconnectPausesUsed(P3), Is.EqualTo(1), "peer 2 too");
            // The referee dies during the pause; peer 1 takes over and, with a pause already running, marks player 0 disconnected.
            session.Disconnect(N0);
            session.RunUntil(() => session[N1].Peer.IsReferee && session[N1].NoticesOf(NoticeKind.PeerDropped).Count == 2, 20);
            // The new referee.
            SessionPeer referee = session[N1];
            Assert.That(referee.Peer.IsReferee, Is.True);
            Assert.That(session[N2].Peer.Referee, Is.EqualTo(N1));
            Assert.That(referee.NoticesOf(NoticeKind.PeerDropped)[1].Peer, Is.EqualTo(N0));
            Assert.That(referee.NoticesOf(NoticeKind.CommandInjected)[0].Text, Is.EqualTo("disconnected_no_pause"));
            Assert.That(referee.NoticesOf(NoticeKind.CommandInjected)[0].Player, Is.EqualTo(P0));
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Paused), "peer 3's pause goes on under the new referee");
            // Peer 3 returns through peer 1 and inherits the count.
            SessionPeer joiner = session.Reconnect(N3);
            session.RunUntil(() => joiner.Peer.Phase == LockstepPhase.Running && referee.Peer.Phase == LockstepPhase.Running, 400);
            Assert.That(joiner.Peer.Phase, Is.EqualTo(LockstepPhase.Running), string.Join("; ", joiner.Notices));
            Assert.That(joiner.Peer.Ledger.DisconnectPausesUsed(P3), Is.EqualTo(1), "the log carried the count");
            Assert.That(referee.NoticesOf(NoticeKind.PauseEnded)[0].Text, Does.StartWith("resumed"));
            session.RunToTick(referee.Tick + 40);
            Assert.That(StateOf(referee, P0), Is.EqualTo(PlayerState.Disconnected), "player 0 marked disconnected once play resumed");
            // Peer 3 drops again: no pause left, so play goes on with player 3 marked disconnected.
            session.Disconnect(N3);
            session.RunUntil(() => referee.NoticesOf(NoticeKind.PeerDropped).Count == 3, 20);
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Running), "no second disconnect pause");
            Assert.That(referee.NoticesOf(NoticeKind.PauseStarted).Count, Is.EqualTo(1));
            Assert.That(referee.Peer.Ledger.DisconnectPausesUsed(P3), Is.EqualTo(1));
            Assert.That(referee.NoticesOf(NoticeKind.CommandInjected)[referee.NoticesOf(NoticeKind.CommandInjected).Count - 1].Player, Is.EqualTo(P3));
            session.RunToTick(referee.Tick + 40);
            Assert.That(StateOf(referee, P3), Is.EqualTo(PlayerState.Disconnected));
            session.AssertNoDesync();
            session.AssertSameState();
        }

        /// <summary>
        /// Review finding 6 (major). A peer missing a bundle asks the referee only once the referee's bundles prove it stepped the
        /// tick (its bundle for tick + delay + 1), and asks again as the referee advances. Peer 3's bundles reach the referee six
        /// host ticks late and peer 2 thirty host ticks late: the old rule asked when the referee had merely sent its bundle for
        /// tick + delay, before the referee held peer 3's bundle, got no answer and never asked again, leaving peer 2 to wait the
        /// thirty ticks; with the fix peer 2 gets the bundle from the referee within a few host ticks and keeps pace.
        /// </summary>
        [Test]
        public void MissingBundle_IsFetchedFromTheReferee_OnceItProvablySteppedTheTick_FourPeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 4);
            session.Start();
            session.RunToTick(100);
            // Peer 3's link to the referee slows a little and its link to peer 2 a lot; everything else stays at one host tick.
            session.Network.SetLinkLatency(N3, N0, 6);
            session.Network.SetLinkLatency(N3, N2, 30);
            // Tick every peer stands on when the links slow. Unit: ticks.
            int start = session[N2].Tick;
            // Iterations until peer 2 passed the first tick whose peer 3 bundle only the referee could give it in time.
            int used = session.RunUntil(() => session[N2].Tick >= start + 5, 20);
            Assert.That(session[N2].Tick, Is.GreaterThanOrEqualTo(start + 5), "peer 2 got the late bundles from the referee in " + used + " iterations (phase " + session[N2].Peer.Phase + ")");
            session.RunToTick(start + 60);
            session.AssertNoDesync();
            session.AssertSameState();
        }

        /// <summary>
        /// Review finding 9 (minor). A joiner that has sent its sync request but not yet received the log sees another peer rejoin:
        /// peer 2's endpoint is back (so it receives every announcement) before its player asks to rejoin; meanwhile peer 1 dies
        /// and returns. Peer 2 must take the rejoin announcement without a simulation, then sync and play on with everyone.
        /// </summary>
        [Test]
        public void JoinerWaitingForItsLog_SurvivesAnotherPeersRejoin_ThreePeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 3);
            session.Start();
            session.RunToTick(100);
            // The referee.
            SessionPeer referee = session[N0];
            DropAndPause(session, N2, referee);
            // Peer 2's endpoint returns; its peer instance waits for the player to ask to rejoin.
            SessionPeer waiting = session.ConnectWithoutJoining(N2);
            for (int i = 0; i < 3; i++)
            {
                session.Iterate();
            }
            // Peer 1 dies (no pause: one is running) and returns while peer 2 still waits.
            session.Disconnect(N1);
            session.RunUntil(() => referee.NoticesOf(NoticeKind.PeerDropped).Count == 2, 20);
            Assert.That(referee.NoticesOf(NoticeKind.CommandInjected)[0].Text, Is.EqualTo("disconnected_no_pause"));
            // Peer 1's fresh instance, rejoined under peer 2's pause (so it ends up paused, not running).
            SessionPeer joiner1 = session.Reconnect(N1);
            session.RunUntil(() => joiner1.Peer.Phase != LockstepPhase.Joining, 400);
            Assert.That(joiner1.Peer.Phase, Is.EqualTo(LockstepPhase.Paused), string.Join("; ", joiner1.Notices));
            Assert.That(referee.NoticesOf(NoticeKind.PeerRejoined).Count, Is.EqualTo(1));
            // Now peer 2 asks to rejoin, with the drop and rejoin of peer 1 still queued in its endpoint.
            session.Join(N2);
            Assert.DoesNotThrow(() => session.RunUntil(() => waiting.Peer.Phase == LockstepPhase.Running, 400), "a joiner without a simulation takes a rejoin announcement");
            Assert.That(waiting.Peer.Phase, Is.EqualTo(LockstepPhase.Running), string.Join("; ", waiting.Notices));
            Assert.That(waiting.NoticesOf(NoticeKind.PeerRejoined).Count, Is.EqualTo(2), "peer 1's rejoin, then its own");
            session.RunToTick(referee.Tick + 60);
            Assert.That(StateOf(referee, P1), Is.EqualTo(PlayerState.Active), "player 1 marked connected again");
            session.AssertNoDesync();
            session.AssertSameState();
        }

        /// <summary>
        /// Review finding 10 (minor). A peer that restarts before the transport reports its old connection gone sends a sync request
        /// while the referee still holds it live: the referee treats that as the restart it is, drops the peer (its disconnect pause
        /// starts) and answers, instead of refusing with not_dropped and locking the player out for good.
        /// </summary>
        [Test]
        public void RestartBeforeTheOldConnectionCloses_IsDroppedThenSynced_ThreePeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 3);
            session.Start();
            session.RunToTick(100);
            // The referee.
            SessionPeer referee = session[N0];
            // Peer 1 crashes without the transport noticing; the others stall on its bundles.
            session.Disconnect(N1, reportLoss: false);
            for (int i = 0; i < 5; i++)
            {
                session.Iterate();
            }
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Running));
            Assert.That(referee.NoticesOf(NoticeKind.PeerDropped), Is.Empty, "nothing reported the loss");
            // The restarted peer joins; the transport reports only its new connection.
            SessionPeer joiner = session.Reconnect(N1);
            session.RunUntil(() => joiner.Peer.Phase == LockstepPhase.Running && referee.Peer.Phase == LockstepPhase.Running, 400);
            Assert.That(joiner.Peer.Phase, Is.EqualTo(LockstepPhase.Running), string.Join("; ", joiner.Notices));
            Assert.That(joiner.NoticesOf(NoticeKind.SyncRejected), Is.Empty);
            Assert.That(referee.NoticesOf(NoticeKind.PeerDropped).Count, Is.EqualTo(1), "the sync request counted as the drop");
            Assert.That(referee.NoticesOf(NoticeKind.PauseStarted)[0].Text, Does.StartWith("disconnect"), "the restart used the peer's disconnect pause");
            Assert.That(referee.NoticesOf(NoticeKind.PeerRejoined).Count, Is.EqualTo(1));
            Assert.That(session[N2].NoticesOf(NoticeKind.PeerDropped).Count, Is.EqualTo(1));
            session.RunToTick(referee.Tick + 60);
            session.AssertNoDesync();
            session.AssertSameState();
        }

        /// <summary>Review finding 11 (minor). Start refuses to run while a roster peer is not connected (it would otherwise drop the peer and spend its only disconnect pause before tick 0); once every peer is connected it starts.</summary>
        [Test]
        public void Start_RequiresEveryRosterPeerConnected_ThreePeers()
        {
            // The session's network, roster and settings, used by hand.
            LockstepSession session = new LockstepSession(peerCount: 3, ai: false);
            // Endpoints of peers 0 and 1 only.
            LoopbackNetwork.LoopbackTransport t0 = session.Network.Connect(N0);
            session.Network.Connect(N1);
            // The peer under test.
            LockstepPeer peer = new LockstepPeer(session.Roster, N0, t0, session.CreateMatch, session.Codecs, session.Settings);
            Assert.That(() => peer.Start(), Throws.InvalidOperationException.With.Message.Contains("not connected"));
            Assert.That(peer.Phase, Is.EqualTo(LockstepPhase.Created));
            session.Network.Connect(N2);
            peer.Start();
            Assert.That(peer.Phase, Is.EqualTo(LockstepPhase.Running));
            Assert.That(peer.DroppedPeers, Is.Empty);
        }

        /// <summary>
        /// Review finding 12 (minor). A bundle forwarded by a peer that is not (or no longer) this peer's referee is stored, not a
        /// protocol error: peer 2 asks peer 1 for one of the referee's bundles (as it would have while peer 1 held the referee role)
        /// and peer 1 answers after the role is back with peer 0. The loopback's all-or-nothing links cannot stage the handover race
        /// itself, so the answer is provoked by a request sent through peer 2's endpoint.
        /// </summary>
        [Test]
        public void ForwardedBundle_FromAPeerThatIsNotTheReferee_IsAccepted_ThreePeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 3);
            session.Start();
            session.RunToTick(50);
            Assert.That(session[N2].Peer.Referee, Is.EqualTo(N0));
            session[N2].Transport.Send(N1, CodecOf(session).Encode(new BundleRequestMessage(N0, 10)));
            Assert.DoesNotThrow(() => session.RunToTick(80), "the forwarded bundle is taken from a non-referee");
            Assert.That(session[N2].Peer.Phase, Is.EqualTo(LockstepPhase.Running));
            session.AssertNoDesync();
            session.AssertSameState();
        }

        /// <summary>The referee dies: the next live peer takes over, pauses for it and answers its reconnect; once back, the lowest peer is the referee again and a voluntary pause through it works.</summary>
        [Test]
        public void RefereeHandover_OnDrop_AndBackOnRejoin_ThreePeers()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(peerCount: 3);
            session.Start();
            session.RunToTick(120);
            session.Disconnect(N0);
            session.RunUntil(() => session[N1].Peer.IsReferee && session[N1].Peer.Phase == LockstepPhase.Paused && session[N2].Peer.Phase == LockstepPhase.Paused, 20);
            Assert.That(session[N1].Peer.IsReferee, Is.True);
            Assert.That(session[N2].Peer.Referee, Is.EqualTo(N1));
            Assert.That(session[N1].NoticesOf(NoticeKind.PeerDropped)[0].Peer, Is.EqualTo(N0));
            Assert.That(session[N1].NoticesOf(NoticeKind.PauseStarted)[0].Text, Does.StartWith("disconnect"));
            Assert.That(session[N2].NoticesOf(NoticeKind.PauseStarted)[0].Peer, Is.EqualTo(N0));
            Assert.That(session[N2].Peer.Ledger.DisconnectPausesUsed(P0), Is.EqualTo(1));
            // Peer 0 returns through peer 1 and is the referee again once live.
            SessionPeer joiner = session.Reconnect(N0);
            session.RunUntil(() => joiner.Peer.Phase == LockstepPhase.Running && session[N1].Peer.Phase == LockstepPhase.Running, 400);
            Assert.That(joiner.Peer.Phase, Is.EqualTo(LockstepPhase.Running), string.Join("; ", joiner.Notices));
            Assert.That(joiner.Peer.IsReferee, Is.True);
            Assert.That(session[N1].Peer.IsReferee, Is.False);
            Assert.That(session[N2].Peer.Referee, Is.EqualTo(N0));
            session.RunToTick(joiner.Tick + 60);
            // A voluntary pause from peer 2 goes to the restored referee.
            session[N2].Peer.RequestPause(LockstepSession.P2);
            session.RunUntil(() => session[N2].Peer.Phase == LockstepPhase.Paused, 10);
            Assert.That(joiner.Peer.ActivePause!.Reason, Is.EqualTo(PauseReason.Voluntary));
            Assert.That(joiner.NoticesOf(NoticeKind.PauseStarted)[0].Player, Is.EqualTo(LockstepSession.P2));
            session[N2].Peer.RequestResume(LockstepSession.P2);
            session.RunUntil(() => session[N2].Peer.Phase == LockstepPhase.Running, 10);
            session.RunToTick(joiner.Tick + 40);
            session.AssertNoDesync();
            session.AssertSameState();
        }

        /// <summary>
        /// Review finding 7 (major). An online match recorded through the peer's hooks (SimulationCreated, CommandSubmitted, Stepped)
        /// holds the setup commands the peer submitted on the host's behalf and re-simulates to the same hashes; a factory that queued
        /// the setup itself is refused by LockstepMatch, and a recorder attached to a simulation with queued commands is refused too.
        /// </summary>
        [Test]
        public void OnlineMatch_RecordedThroughThePeerHooks_VerifiesAndHoldsTheSetup()
        {
            // The session under test, recording on peer 1.
            LockstepSession session = new LockstepSession(peerCount: 3);
            // The recorder, created when peer 1's simulation exists and before any command goes in.
            ReplayRecorder? recorder = null;
            session.Start(peer =>
            {
                if (peer.Peer.LocalPeer == N1)
                {
                    peer.Peer.SimulationCreated += sim => recorder = new ReplayRecorder(sim, NetTestData.PlayersFor(3));
                    peer.Peer.CommandSubmitted += command => recorder!.Record(command);
                    peer.Peer.Stepped += sim => recorder!.AfterStep();
                }
            });
            Assert.That(recorder, Is.Not.Null, "SimulationCreated fired at Start");
            session.RunToTick(200);
            // The recording and its re-simulation.
            ReplayData replay = recorder!.Finish();
            Assert.That(replay.TickCount, Is.EqualTo(session[N1].Tick));
            // Setup commands found in the replay: the script's ten spawns, issuer None with sequences 0 to 9 at tick 0.
            int setup = 0;
            foreach (Command command in replay.Commands)
            {
                if (command.Tick == 0 && !command.Issuer.IsValid && command.Sequence < 10)
                {
                    setup++;
                }
            }
            Assert.That(setup, Is.EqualTo(NetTestData.AiVsAi.Commands.Count), "the setup commands are in the replay");
            Assert.That(replay.Commands.Count, Is.GreaterThan(setup), "the AI's commands too");
            ReplayVerification result = ReplayVerifier.Verify(replay, ReplayVerifier.CreateSimulation(replay, NetTestData.Data));
            Assert.That(result.IsMatch, Is.True, "desync at tick " + result.DesyncTick);
            Assert.That(result.FinalHash, Is.EqualTo(session[N1].Peer.Simulation.ComputeStateHash()));
            // A simulation with queued commands is refused as a match and as a recorder target.
            Simulation queued = NetTestData.CreateMatch(2).Simulation;
            queued.Submit(NetTestData.AiVsAi.Commands[0]);
            Assert.That(() => new LockstepMatch(queued, Array.Empty<Command>()), Throws.ArgumentException);
            Assert.That(() => new ReplayRecorder(queued, NetTestData.PlayersFor(2)), Throws.InvalidOperationException);
        }
    }
}

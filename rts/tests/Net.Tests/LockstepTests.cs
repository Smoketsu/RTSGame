// rts/tests/Net.Tests/LockstepTests.cs
// The Phase 2 lockstep exit tests (rts/docs/PHASE2_PLAN.md step 1): two loopback peers play rts/data/scripts/ai_vs_ai.json
// through the protocol to a decided outcome with every exchanged hash matching; an injected divergence is reported as a
// desync at the first hash tick after it; a disconnect pauses and a reconnecting peer catches up from the command log;
// the once-only disconnect pause and the per-player pause budget are enforced; a reordering transport still plays.
// Part of Rts.Net.Tests. The full match test runs the whole AI-versus-AI game on two simulations (about 40 s); the
// others stop after a few hundred ticks.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Net.Tests
{
    /// <summary>Lockstep sessions over the loopback transport.</summary>
    [TestFixture]
    public class LockstepTests
    {
        /// <summary>Peer 0.</summary>
        private static readonly PeerId N0 = LockstepSession.N0;

        /// <summary>Peer 1.</summary>
        private static readonly PeerId N1 = LockstepSession.N1;

        /// <summary>Player 0.</summary>
        private static readonly PlayerId P0 = LockstepSession.P0;

        /// <summary>Player 1.</summary>
        private static readonly PlayerId P1 = LockstepSession.P1;

        /// <summary>Milliseconds in one second. Unit: milliseconds.</summary>
        private const int Second = 1000;

        /// <summary>The player state of <paramref name="player"/> in a peer's simulation.</summary>
        private static PlayerState StateOf(SessionPeer peer, PlayerId player)
        {
            Assert.That(peer.Peer.Simulation.Players.TryGet(player, out Player? found), Is.True);
            return found!.State;
        }

        /// <summary>
        /// Exit test: Skai (peer 0, aggressive AI) versus Water (peer 1, defensive AI) on greybox_1v1 through the lockstep
        /// protocol with the shipped input delay, one host tick of loopback latency, to a decided outcome within the script's
        /// 6000 ticks, with every exchanged hash equal and the two final states identical.
        /// </summary>
        [Test]
        public void AiVsAi_TwoPeers_PlayToADecidedOutcome_WithMatchingHashes()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(latencyTicks: 1);
            session.Start();
            Assert.That(session[N0].Peer.IsReferee, Is.True);
            Assert.That(session[N1].Peer.IsReferee, Is.False);
            Assert.That(session[N0].Peer.Settings.InputDelayTicks, Is.EqualTo(3), "shipped input_delay_ticks");
            // Iterations used until the match is decided on both peers.
            int used = session.RunUntil(() => session[N0].Outcome.IsDecided && session[N1].Outcome.IsDecided, 6000 * 2);
            session.AssertNoDesync();
            Assert.That(session[N0].Outcome.IsDecided, Is.True, "decided after " + used + " iterations, tick " + session[N0].Tick);
            Assert.That(session[N0].Outcome.IsDraw, Is.False);
            Assert.That(session[N0].Tick, Is.LessThan(6000));
            // Let the lagging peer finish the same tick, then compare states.
            session.RunToTick(session[N0].Tick);
            session.AssertSameState();
            Assert.That(session[N1].Outcome.Winner, Is.EqualTo(session[N0].Outcome.Winner));
            Assert.That(session[N0].Peer.ComparedThroughTick(N1), Is.GreaterThanOrEqualTo(session[N0].Tick - 2 * session[N0].Peer.Simulation.Settings.StateHashIntervalTicks), "hashes were compared all the way");
            // Both peers applied the same command log.
            List<CommandBundleMessage> log0 = new List<CommandBundleMessage>(session[N0].Peer.CommandLog);
            List<CommandBundleMessage> log1 = new List<CommandBundleMessage>(session[N1].Peer.CommandLog);
            Assert.That(log1.Count, Is.EqualTo(log0.Count));
            // Commands over the whole match, as a sanity check that both AIs played through the protocol.
            int commands = 0;
            foreach (CommandBundleMessage bundle in log0)
            {
                commands += bundle.Commands.Count;
            }
            Assert.That(commands, Is.GreaterThan(100));
        }

        /// <summary>A command applied on one peer only is reported by both as a desync at the first hash tick at or after it, and stepping stops.</summary>
        [Test]
        public void InjectedDivergence_IsReportedAtTheFirstHashTickAfterIt()
        {
            // The session under test.
            LockstepSession session = new LockstepSession();
            session.Start();
            session.RunToTick(110);
            // The tick the rogue command applies at: peer 1's current tick, bypassing the protocol.
            int rogueTick = session[N1].Tick;
            session[N1].Peer.Simulation.Submit(new SpawnUnitCommand(rogueTick, PlayerId.None, 777, "skai.windrunner", P0, FixVec2.FromInts(30, 30)));
            // The hash interval and the first hash tick at or after the rogue command. Unit: ticks.
            int interval = session[N0].Peer.Simulation.Settings.StateHashIntervalTicks;
            int expected = ((rogueTick + interval - 1) / interval) * interval;
            session.RunUntil(() => session[N0].Peer.Phase == LockstepPhase.Desynced && session[N1].Peer.Phase == LockstepPhase.Desynced, 400);
            foreach (PeerId id in new[] { N0, N1 })
            {
                // The desync notices of this peer.
                List<LockstepNotice> desyncs = session[id].NoticesOf(NoticeKind.Desynced);
                Assert.That(desyncs.Count, Is.EqualTo(1), id + " reports one desync");
                Assert.That(desyncs[0].Tick, Is.EqualTo(expected), id + " names the first mismatching hash tick");
                Assert.That(desyncs[0].LocalHash, Is.Not.EqualTo(desyncs[0].RemoteHash));
                Assert.That(session[id].Peer.Phase, Is.EqualTo(LockstepPhase.Desynced));
            }
            Assert.That(session[N0].NoticesOf(NoticeKind.Desynced)[0].RemoteHash, Is.EqualTo(session[N1].NoticesOf(NoticeKind.Desynced)[0].LocalHash));
            // The stopped tick on each peer; further updates must not move it.
            int stopped0 = session[N0].Tick;
            for (int i = 0; i < 20; i++)
            {
                session.Iterate();
            }
            Assert.That(session[N0].Tick, Is.EqualTo(stopped0), "a desynced peer stops stepping");
        }

        /// <summary>
        /// A disconnect pauses the match (one disconnect pause per player, from online.json); the referee's and the simulation's
        /// ticks stand still; a fresh peer instance rejoins by replaying the command log, the pause ends as resumed, the player's
        /// simulation state never changed, and play continues with matching hashes.
        /// </summary>
        [Test]
        public void Disconnect_PausesThenReconnectCatchesUpFromTheLog()
        {
            // The session under test.
            LockstepSession session = new LockstepSession();
            session.Start();
            session.RunToTick(300);
            session.Disconnect(N1);
            session.RunUntil(() => session[N0].Peer.Phase == LockstepPhase.Paused, 20);
            // The referee's view after the drop.
            SessionPeer referee = session[N0];
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Paused));
            Assert.That(referee.NoticesOf(NoticeKind.PeerDropped).Count, Is.EqualTo(1));
            Assert.That(referee.NoticesOf(NoticeKind.PauseStarted).Count, Is.EqualTo(1));
            Assert.That(referee.Peer.ActivePause!.Reason, Is.EqualTo(PauseReason.Disconnect));
            Assert.That(referee.Peer.ActivePause!.LimitMs, Is.EqualTo(300 * Second), "disconnect_pause_max_seconds");
            Assert.That(referee.Peer.Ledger.DisconnectPausesUsed(P1), Is.EqualTo(1));
            Assert.That(referee.Peer.Ledger.CanStartDisconnectPause(P1), Is.False, "disconnect_pauses_per_player is 1");
            // The frozen tick.
            int frozen = referee.Tick;
            for (int i = 0; i < 40; i++)
            {
                session.Iterate();
            }
            Assert.That(referee.Tick, Is.EqualTo(frozen), "no stepping while paused");
            Assert.That(referee.Peer.PauseElapsedMs, Is.GreaterThanOrEqualTo(40 * LockstepSession.StepMs));
            // The reconnecting peer.
            SessionPeer joiner = session.Reconnect(N1);
            Assert.That(joiner.Peer.Phase, Is.EqualTo(LockstepPhase.Joining));
            // Iterations the reconnect took.
            int used = session.RunUntil(() => joiner.Peer.Phase == LockstepPhase.Running && referee.Peer.Phase == LockstepPhase.Running, 200);
            Assert.That(joiner.Peer.Phase, Is.EqualTo(LockstepPhase.Running), "joiner live after " + used + " iterations: " + string.Join("; ", joiner.Notices));
            Assert.That(joiner.NoticesOf(NoticeKind.SyncStarted).Count, Is.EqualTo(1));
            Assert.That(joiner.NoticesOf(NoticeKind.SyncDone).Count, Is.EqualTo(1));
            Assert.That(joiner.NoticesOf(NoticeKind.PeerRejoined).Count, Is.EqualTo(1));
            Assert.That(referee.NoticesOf(NoticeKind.PeerRejoined)[0].Tick, Is.EqualTo(frozen + referee.Peer.Settings.InputDelayTicks), "rejoin from the referee's tick plus the delay");
            Assert.That(referee.NoticesOf(NoticeKind.PauseEnded).Count, Is.EqualTo(1));
            Assert.That(referee.NoticesOf(NoticeKind.PauseEnded)[0].Text, Does.StartWith("resumed"));
            Assert.That(joiner.Peer.Ledger.DisconnectPausesUsed(P1), Is.EqualTo(1), "the ledger came with the log");
            Assert.That(StateOf(referee, P1), Is.EqualTo(PlayerState.Active), "a pause hides the disconnect from the simulation");
            // Play on and check the two states still agree.
            session.RunToTick(frozen + 300);
            session.AssertNoDesync();
            session.RunToTick(session[N0].Tick);
            session.AssertSameState();
            Assert.That(referee.Peer.ComparedThroughTick(N1), Is.GreaterThan(frozen + 100), "hashes compared after the rejoin");
            Assert.That(joiner.Peer.ComparedThroughTick(N0), Is.GreaterThan(frozen + 100));
        }

        /// <summary>When the disconnected player does not return within disconnect_pause_max_seconds the referee surrenders them and the match is decided.</summary>
        [Test]
        public void DisconnectPause_Expires_AndTheNoShowLoses()
        {
            // The session under test.
            LockstepSession session = new LockstepSession();
            session.Start();
            session.RunToTick(200);
            session.Disconnect(N1);
            session.RunUntil(() => session[N0].Peer.Phase == LockstepPhase.Paused, 20);
            // The referee alone from here on.
            SessionPeer referee = session[N0];
            // Pause the full limit, in whole seconds per update.
            for (int i = 0; i < 300; i++)
            {
                referee.Peer.Update(Second);
            }
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Running));
            Assert.That(referee.NoticesOf(NoticeKind.PauseEnded)[0].Text, Is.EqualTo("expired " + 300 * Second + "ms"));
            Assert.That(referee.NoticesOf(NoticeKind.CommandInjected).Count, Is.EqualTo(1));
            Assert.That(referee.NoticesOf(NoticeKind.CommandInjected)[0].Text, Is.EqualTo("no_show_surrender"));
            Assert.That(referee.NoticesOf(NoticeKind.CommandInjected)[0].Player, Is.EqualTo(P1));
            // The referee keeps playing alone until the surrender applies.
            session.RunUntil(() => referee.Outcome.IsDecided, 100);
            Assert.That(referee.Outcome.IsDecided, Is.True);
            Assert.That(referee.Outcome.Winner, Is.EqualTo(new TeamId(0)));
            Assert.That(StateOf(referee, P1), Is.EqualTo(PlayerState.Surrendered));
        }

        /// <summary>
        /// A second disconnect of the same player gets no pause: the referee marks them Disconnected in the simulation and plays on;
        /// a later reconnect is granted under a resync pause charged to the player's pause budget and marks them Connected again.
        /// </summary>
        [Test]
        public void SecondDisconnect_GetsNoPause_AndReconnectsUnderAResyncPause()
        {
            // The session under test.
            LockstepSession session = new LockstepSession();
            session.Start();
            session.RunToTick(150);
            // First disconnect and reconnect, using up the one disconnect pause.
            session.Disconnect(N1);
            session.RunUntil(() => session[N0].Peer.Phase == LockstepPhase.Paused, 20);
            session.Reconnect(N1);
            session.RunUntil(() => session[N1].Peer.Phase == LockstepPhase.Running && session[N0].Peer.Phase == LockstepPhase.Running, 200);
            session.RunToTick(session[N0].Tick + 60);
            // The referee.
            SessionPeer referee = session[N0];
            // Second disconnect: no pause left.
            session.Disconnect(N1);
            // Tick at the second drop.
            int dropTick = referee.Tick;
            session.RunUntil(() => referee.NoticesOf(NoticeKind.PeerDropped).Count == 2, 20);
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Running), "no pause this time");
            Assert.That(referee.NoticesOf(NoticeKind.PauseStarted).Count, Is.EqualTo(1));
            Assert.That(referee.NoticesOf(NoticeKind.CommandInjected)[0].Text, Is.EqualTo("disconnected_no_pause"));
            session.RunToTick(dropTick + 40);
            Assert.That(referee.Tick, Is.GreaterThanOrEqualTo(dropTick + 40), "the referee plays on alone");
            Assert.That(StateOf(referee, P1), Is.EqualTo(PlayerState.Disconnected));
            Assert.That(referee.Peer.Ledger.PauseMsUsed(P1), Is.EqualTo(0));
            // Reconnect under a resync pause.
            SessionPeer joiner = session.Reconnect(N1);
            session.RunUntil(() => joiner.Peer.Phase == LockstepPhase.Running && referee.Peer.Phase == LockstepPhase.Running, 300);
            Assert.That(joiner.Peer.Phase, Is.EqualTo(LockstepPhase.Running), string.Join("; ", joiner.Notices));
            Assert.That(referee.NoticesOf(NoticeKind.PauseStarted).Count, Is.EqualTo(2));
            Assert.That(referee.NoticesOf(NoticeKind.PauseStarted)[1].Text, Does.StartWith("resync"));
            Assert.That(referee.Peer.Ledger.PauseMsUsed(P1), Is.GreaterThan(0), "the resync pause is charged to the budget");
            Assert.That(joiner.Peer.Ledger.PauseMsUsed(P1), Is.EqualTo(referee.Peer.Ledger.PauseMsUsed(P1)), "ledgers agree");
            session.RunToTick(referee.Tick + 60);
            Assert.That(StateOf(referee, P1), Is.EqualTo(PlayerState.Active), "marked connected again");
            session.AssertNoDesync();
            session.RunToTick(session[N0].Tick);
            session.AssertSameState();
        }

        /// <summary>
        /// Voluntary pauses draw on pause_budget_seconds_per_player (120 s): a resumed pause charges its length, the next pause is
        /// capped at what is left and expires when the budget runs out, and a further request is refused; the non-referee's
        /// request travels through the transport and both ledgers agree.
        /// </summary>
        [Test]
        public void PauseBudget_IsEnforcedPerPlayer()
        {
            // The session under test.
            LockstepSession session = new LockstepSession();
            session.Start();
            session.RunToTick(60);
            // The referee and the other peer.
            SessionPeer referee = session[N0];
            SessionPeer other = session[N1];
            Assert.That(referee.Peer.Settings.PauseBudgetMsPerPlayer, Is.EqualTo(120 * Second), "pause_budget_seconds_per_player");
            // Peer 1 asks for a pause through the transport.
            other.Peer.RequestPause(P1);
            session.RunUntil(() => other.Peer.Phase == LockstepPhase.Paused, 10);
            Assert.That(referee.Peer.ActivePause!.Reason, Is.EqualTo(PauseReason.Voluntary));
            Assert.That(referee.Peer.ActivePause!.LimitMs, Is.EqualTo(120 * Second));
            Assert.That(referee.Peer.ActivePause!.Player, Is.EqualTo(P1));
            // Fifty seconds of pause, then resume.
            for (int i = 0; i < 50; i++)
            {
                referee.Peer.Update(Second);
                other.Peer.Update(Second);
                session.Network.Advance(1);
            }
            other.Peer.RequestResume(P1);
            session.RunUntil(() => other.Peer.Phase == LockstepPhase.Running, 10);
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Running));
            // Budget charged for the first pause: the 50 s plus the few 50 ms host ticks the resume request spent in transit.
            int used = referee.Peer.Ledger.PauseMsUsed(P1);
            Assert.That(used, Is.InRange(50 * Second, 50 * Second + 10 * LockstepSession.StepMs));
            Assert.That(other.Peer.Ledger.PauseMsUsed(P1), Is.EqualTo(used), "the referee's length rules both ledgers");
            Assert.That(referee.Peer.Ledger.PauseMsUsed(P0), Is.EqualTo(0));
            // The second pause is capped at what is left (about 70 s) and expires.
            other.Peer.RequestPause(P1);
            session.RunUntil(() => other.Peer.Phase == LockstepPhase.Paused, 10);
            // The cap of the second pause. Unit: milliseconds.
            int remaining = 120 * Second - used;
            Assert.That(referee.Peer.ActivePause!.LimitMs, Is.EqualTo(remaining));
            for (int i = 0; i < 80 && referee.Peer.Phase == LockstepPhase.Paused; i++)
            {
                referee.Peer.Update(Second);
                other.Peer.Update(Second);
                session.Network.Advance(1);
            }
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Running));
            Assert.That(referee.NoticesOf(NoticeKind.PauseEnded)[1].Text, Is.EqualTo("expired " + remaining + "ms"));
            Assert.That(referee.Peer.Ledger.PauseMsUsed(P1), Is.EqualTo(120 * Second));
            Assert.That(referee.Peer.Ledger.RemainingBudgetMs(P1), Is.EqualTo(0));
            // A third request is refused; player 0 still has its full budget.
            other.Peer.RequestPause(P1);
            session.RunUntil(() => referee.NoticesOf(NoticeKind.PauseRefused).Count > 0, 10);
            Assert.That(referee.NoticesOf(NoticeKind.PauseRefused)[0].Text, Is.EqualTo("no_budget"));
            Assert.That(referee.Peer.Phase, Is.EqualTo(LockstepPhase.Running));
            referee.Peer.RequestPause(P0);
            Assert.That(referee.Peer.ActivePause!.LimitMs, Is.EqualTo(120 * Second));
            referee.Peer.RequestResume(P0);
            Assert.That(referee.Peer.ActivePause, Is.Null);
            // Play on; nothing diverged.
            session.RunToTick(referee.Tick + 100);
            session.AssertNoDesync();
        }

        /// <summary>A transport that reorders messages within a window still gives a matching match: every message is keyed, nothing depends on arrival order.</summary>
        [Test]
        public void ReorderingTransport_StillMatches()
        {
            // The session under test, with a three-tick reorder window.
            LockstepSession session = new LockstepSession(latencyTicks: 2);
            session.Network.ReorderWindowTicks = 3;
            session.Start();
            session.RunToTick(400);
            session.AssertNoDesync();
            session.RunToTick(session[N0].Tick);
            session.AssertSameState();
            Assert.That(session[N0].Peer.ComparedThroughTick(N1), Is.GreaterThan(300));
        }

        /// <summary>A peer that submits a command for another peer's player, or before the next input tick, is refused locally.</summary>
        [Test]
        public void LocalInput_IsValidated()
        {
            // The session under test.
            LockstepSession session = new LockstepSession(ai: false);
            session.Start();
            // The peer under test.
            LockstepPeer peer = session[N0].Peer;
            Assert.That(peer.NextInputTick, Is.EqualTo(peer.Settings.InputDelayTicks));
            Assert.That(() => peer.SubmitLocal(new NoOpCommand(peer.NextInputTick, P1, 1)), Throws.ArgumentException, "not my player");
            Assert.That(() => peer.SubmitLocal(new NoOpCommand(0, P0, 1)), Throws.InvalidOperationException, "already sent");
            peer.SubmitLocal(new NoOpCommand(peer.NextInputTick, P0, 1));
            session.RunToTick(10);
            // The bundle that carried the no-op, found in the other peer's log.
            bool carried = false;
            foreach (CommandBundleMessage bundle in session[N1].Peer.CommandLog)
            {
                carried |= bundle.Peer == N0 && bundle.Tick == peer.Settings.InputDelayTicks && bundle.Commands.Count == 1;
            }
            Assert.That(carried, Is.True, "the command travelled in the bundle for its tick");
            session.AssertNoDesync();
        }
    }
}

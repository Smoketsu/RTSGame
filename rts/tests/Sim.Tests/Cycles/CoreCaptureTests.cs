// rts/tests/Sim.Tests/Cycles/CoreCaptureTests.cs
// Acceptance tests 2 to 8, 12 (the script-pick half) and 13 of rts/docs/specs/planetary_cycles.md section 7, plus the pure
// capture state machine (CycleRules.CaptureStep): capture time and the radius edge, a contest freezing progress, leaving resetting,
// a different lone team restarting, the holder blocking, who counts, the cumulative Core win and a holder leaving the match.
// Part of Rts.Sim.Tests. Ticks are the tick an event is raised on.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Cycles;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Teams;

namespace Rts.Sim.Tests.Cycles
{
    /// <summary>Core capture, holding and the Core win.</summary>
    [TestFixture]
    public class CoreCaptureTests
    {
        /// <summary>Tuning with a 60-second Core win (1200 ticks) instead of 480 seconds.</summary>
        private static Tuning ShortWin(params string[] more)
        {
            // Edits: the win time, then the caller's extra triples.
            List<string> edits = new List<string> { "tuning/match.json", "\"ffa_core_win_cumulative_seconds\": 480", "\"ffa_core_win_cumulative_seconds\": 60" };
            edits.AddRange(more);
            return CycleWorld.EditedTuning(edits.ToArray());
        }

        // ---------------------------------------------------------------- 2. capture time and edge

        [Test]
        public void Capture_StartsOnTickZeroAndCompletesOnTick399()
        {
            CycleRig rig = new CycleRig();
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.RunThrough(398);
            Assert.That(rig.Sim.Cycles.Holder.IsValid, Is.False);
            Assert.That(rig.Sim.Cycles.CaptureProgressTicks, Is.EqualTo(399));
            rig.RunThrough(399);
            Assert.That(rig.TicksOf<CoreCaptureStartedEvent>(e => e.Team == CycleWorld.T0), Is.EqualTo(new List<int> { 0 }));
            CoreCapturedEvent? captured = rig.At<CoreCapturedEvent>(399);
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.Team, Is.EqualTo(CycleWorld.T0));
            Assert.That(captured.PreviousHolder.IsValid, Is.False);
            Assert.That(rig.Sim.Cycles.Holder, Is.EqualTo(CycleWorld.T0));
            Assert.That(rig.Sim.Cycles.CaptureTeam.IsValid, Is.False);
            Assert.That(rig.Sim.Cycles.CaptureProgressTicks, Is.EqualTo(0));
        }

        [Test]
        public void Capture_RadiusEdgeCountsAndJustOutsideDoesNot()
        {
            CycleRig onEdge = new CycleRig();
            onEdge.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "20", "16");
            onEdge.RunThrough(399);
            Assert.That(onEdge.TicksOf<CoreCapturedEvent>(), Is.EqualTo(new List<int> { 399 }), "a unit exactly 4 tiles from the centre captures");

            CycleRig outside = new CycleRig();
            outside.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "20.01", "16");
            outside.RunThrough(450);
            Assert.That(outside.Of<CoreCaptureStartedEvent>(), Is.Empty, "a unit 4.01 tiles from the centre never starts a capture");
        }

        // ---------------------------------------------------------------- 3. contest freezes

        [Test]
        public void Contest_FreezesProgressAndResumes()
        {
            CycleRig rig = new CycleRig();
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            EntityId rival = rig.Spawn(100, CycleWorld.ScoutId, CycleWorld.P1, "17", "16");
            rig.Remove(200, rival);
            // Progress after each of the ticks 100 to 199, which must all read 100.
            List<int> frozen = new List<int>();
            rig.RunThrough(99);
            for (int tick = 100; tick < 200; tick++)
            {
                rig.RunThrough(tick);
                frozen.Add(rig.Sim.Cycles.CaptureProgressTicks);
            }
            Assert.That(frozen, Is.All.EqualTo(100));
            rig.RunThrough(600);
            Assert.That(rig.TicksOf<CoreContestedChangedEvent>(e => e.IsContested), Is.EqualTo(new List<int> { 100 }));
            Assert.That(rig.TicksOf<CoreContestedChangedEvent>(e => !e.IsContested), Is.EqualTo(new List<int> { 200 }));
            Assert.That(rig.TicksOf<CoreCapturedEvent>(), Is.EqualTo(new List<int> { 499 }));
            Assert.That(rig.Of<CoreCaptureResetEvent>(), Is.Empty);
        }

        // ---------------------------------------------------------------- 4. leaving resets

        [Test]
        public void Leaving_ResetsProgressAndANewUnitStartsOver()
        {
            CycleRig rig = new CycleRig();
            EntityId first = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.Remove(150, first);
            rig.Spawn(160, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.RunThrough(600);
            CoreCaptureResetEvent? reset = rig.At<CoreCaptureResetEvent>(150);
            Assert.That(reset, Is.Not.Null);
            Assert.That(reset!.Team, Is.EqualTo(CycleWorld.T0));
            Assert.That(reset.ProgressTicks, Is.EqualTo(150));
            Assert.That(reset.Reason, Is.EqualTo("left"));
            Assert.That(rig.TicksOf<CoreCaptureStartedEvent>(), Is.EqualTo(new List<int> { 0, 160 }));
            Assert.That(rig.TicksOf<CoreCapturedEvent>(), Is.EqualTo(new List<int> { 559 }));
        }

        // ---------------------------------------------------------------- 5. another lone team restarts

        [Test]
        public void AnotherLoneTeam_RestartsFromZero()
        {
            CycleRig rig = new CycleRig();
            EntityId first = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.Remove(100, first);
            rig.Spawn(100, CycleWorld.ScoutId, CycleWorld.P1, "16.5", "16.5");
            rig.RunThrough(600);
            CoreCaptureResetEvent? reset = rig.At<CoreCaptureResetEvent>(100);
            Assert.That(reset, Is.Not.Null);
            Assert.That(reset!.Team, Is.EqualTo(CycleWorld.T0));
            Assert.That(reset.ProgressTicks, Is.EqualTo(100));
            Assert.That(reset.Reason, Is.EqualTo("other_team"));
            Assert.That(rig.IndexOf<CoreCaptureResetEvent>(100), Is.LessThan(rig.IndexOf<CoreCaptureStartedEvent>(100, e => e.Team == CycleWorld.T1)),
                "the reset is reported before the new start");
            CoreCapturedEvent? captured = rig.At<CoreCapturedEvent>(499);
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.Team, Is.EqualTo(CycleWorld.T1));
        }

        // ---------------------------------------------------------------- 6. the holder blocks

        [Test]
        public void Holder_BlocksUntilItLeaves()
        {
            CycleRig rig = new CycleRig();
            EntityId holderUnit = rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.Spawn(500, CycleWorld.ScoutId, CycleWorld.P1, "17", "16");
            rig.Remove(600, holderUnit);
            rig.RunThrough(599);
            Assert.That(rig.Sim.Cycles.Holder, Is.EqualTo(CycleWorld.T0));
            Assert.That(rig.Sim.Cycles.IsContested, Is.True);
            Assert.That(rig.Sim.Cycles.CaptureProgressTicks, Is.EqualTo(0), "a team present with the holder makes no progress");
            Assert.That(rig.Of<CoreCaptureStartedEvent>().Count, Is.EqualTo(1), "only the first capture started");
            rig.RunThrough(1000);
            Assert.That(rig.TicksOf<CoreCaptureStartedEvent>(e => e.Team == CycleWorld.T1), Is.EqualTo(new List<int> { 600 }));
            Assert.That(rig.TicksOf<CoreCapturedEvent>(), Is.EqualTo(new List<int> { 399, 999 }));
            CoreCapturedEvent? taken = rig.At<CoreCapturedEvent>(999);
            Assert.That(taken!.Team, Is.EqualTo(CycleWorld.T1));
            Assert.That(taken.PreviousHolder, Is.EqualTo(CycleWorld.T0));
            CoreLostEvent? lost = rig.At<CoreLostEvent>(999);
            Assert.That(lost, Is.Not.Null);
            Assert.That(lost!.Team, Is.EqualTo(CycleWorld.T0));
            Assert.That(lost.Reason, Is.EqualTo("captured"));
            Assert.That(rig.IndexOf<CoreLostEvent>(999), Is.LessThan(rig.IndexOf<CoreCapturedEvent>(999)), "core_lost comes before core_captured");
        }

        // ---------------------------------------------------------------- 7. who counts

        [Test]
        public void WhoCounts_BuildingsNeutralsAndSurrenderedPlayersDoNot()
        {
            CycleRig building = new CycleRig();
            building.Spawn(0, CycleWorld.WallId, CycleWorld.P1, "16.5", "16.5");
            building.RunThrough(60);
            Assert.That(building.Of<CoreCaptureStartedEvent>(), Is.Empty, "a speed-0 building does not count");

            CycleRig neutral = new CycleRig();
            neutral.Spawn(0, CycleWorld.ScoutId, PlayerId.None, "16.5", "16.5");
            neutral.RunThrough(60);
            Assert.That(neutral.Of<CoreCaptureStartedEvent>(), Is.Empty, "a neutral unit does not count");

            CycleRig surrendered = new CycleRig();
            surrendered.Surrender(0, CycleWorld.P1);
            surrendered.Spawn(0, CycleWorld.ScoutId, CycleWorld.P1, "16.5", "16.5");
            surrendered.RunThrough(60);
            Assert.That(surrendered.Of<CoreCaptureStartedEvent>(), Is.Empty, "a unit of a player who left the match does not count");

            CycleRig flyer = new CycleRig();
            flyer.Spawn(0, CycleWorld.SkyScoutId, CycleWorld.P1, "16.5", "16.5");
            flyer.RunThrough(0);
            Assert.That(flyer.TicksOf<CoreCaptureStartedEvent>(e => e.Team == CycleWorld.T1), Is.EqualTo(new List<int> { 0 }), "a flyer counts on its spawn tick");
        }

        // ---------------------------------------------------------------- 8. the Core win

        [Test]
        public void CoreWin_ThreeTeams_WinsAfterTheCumulativeTime()
        {
            CycleRig rig = new CycleRig(CycleWorld.Players(3), tuning: ShortWin());
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.RunThrough(1700);
            Assert.That(rig.TicksOf<CoreWinEvent>(), Is.EqualTo(new List<int> { 1598 }));
            Assert.That(rig.At<CoreWinEvent>(1598)!.HoldTicks, Is.EqualTo(1200));
            Assert.That(rig.Sim.Cycles.CoreWinner, Is.EqualTo(CycleWorld.T0));
            foreach (PlayerId loser in new[] { CycleWorld.P1, CycleWorld.P2 })
            {
                PlayerStateChangedEvent? change = rig.At<PlayerStateChangedEvent>(1598, e => e.Player == loser);
                Assert.That(change, Is.Not.Null, loser.ToString());
                Assert.That(change!.Current, Is.EqualTo(PlayerState.Defeated));
                Assert.That(change.Cause, Is.EqualTo("core_held"));
            }
            MatchOutcome outcome = MatchOutcome.Evaluate(rig.Sim.Players);
            Assert.That(outcome.IsDecided, Is.True);
            Assert.That(outcome.Winner, Is.EqualTo(CycleWorld.T0));
        }

        [Test]
        public void CoreWin_AContestPausesTheAccrual()
        {
            CycleRig rig = new CycleRig(CycleWorld.Players(3), tuning: ShortWin());
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            EntityId rival = rig.Spawn(500, CycleWorld.ScoutId, CycleWorld.P1, "17", "16");
            rig.Remove(600, rival);
            rig.RunThrough(1800);
            Assert.That(rig.TicksOf<CoreWinEvent>(), Is.EqualTo(new List<int> { 1698 }), "100 contested ticks do not count");
        }

        [Test]
        public void CoreWin_TwoTeamsDoNotWinUnlessTheMinimumIsLowered()
        {
            CycleRig oneVsOne = new CycleRig(CycleWorld.Players(2), tuning: ShortWin());
            oneVsOne.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            oneVsOne.RunThrough(2000);
            Assert.That(oneVsOne.Of<CoreWinEvent>(), Is.Empty);
            Assert.That(oneVsOne.Sim.Cycles.IsCoreWinEnabled, Is.False);
            Assert.That(oneVsOne.Sim.Cycles.HoldTicks(CycleWorld.T0), Is.EqualTo(1200), "the hold time keeps counting up to the cap");

            CycleRig lowered = new CycleRig(CycleWorld.Players(2), tuning: ShortWin("tuning/match.json", "\"core_win_min_teams\": 3", "\"core_win_min_teams\": 2"));
            lowered.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            lowered.RunThrough(1700);
            Assert.That(lowered.TicksOf<CoreWinEvent>(), Is.EqualTo(new List<int> { 1598 }));
        }

        [Test]
        public void CoreWin_FreezesCaptureAfterTheWin()
        {
            CycleRig rig = new CycleRig(CycleWorld.Players(3), tuning: ShortWin());
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.Spawn(1700, CycleWorld.ScoutId, CycleWorld.P1, "17", "16");
            rig.RunThrough(1800);
            Assert.That(rig.Sim.Cycles.IsContested, Is.False, "presence is not scanned after the win");
            Assert.That(rig.Sim.Cycles.HoldTicks(CycleWorld.T0), Is.EqualTo(1200));
        }

        // ---------------------------------------------------------------- 13. the holder leaves the match

        [Test]
        public void HolderLeavingTheMatch_LosesTheCoreAndItsPick()
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles());
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.Pick(450, CycleWorld.P0, "quake");
            rig.Surrender(500, CycleWorld.P0);
            rig.RunThrough(499);
            Assert.That(rig.Sim.Cycles.PendingPickId, Is.EqualTo("quake"));
            rig.RunThrough(501);
            CoreLostEvent? lost = rig.At<CoreLostEvent>(500);
            Assert.That(lost, Is.Not.Null);
            Assert.That(lost!.Team, Is.EqualTo(CycleWorld.T0));
            Assert.That(lost.Reason, Is.EqualTo("holder_out"));
            Assert.That(rig.Sim.Cycles.Holder.IsValid, Is.False);
            Assert.That(rig.Sim.Cycles.PendingPickId, Is.EqualTo(string.Empty), "the dropped team's pick is cleared");
        }

        [Test]
        public void CapturingTeamLeavingTheMatch_ResetsWithHolderOut()
        {
            CycleRig rig = new CycleRig();
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.Surrender(100, CycleWorld.P0);
            rig.RunThrough(110);
            CoreCaptureResetEvent? reset = rig.At<CoreCaptureResetEvent>(100);
            Assert.That(reset, Is.Not.Null);
            Assert.That(reset!.Reason, Is.EqualTo("holder_out"));
            Assert.That(reset.ProgressTicks, Is.EqualTo(100));
        }

        // ---------------------------------------------------------------- the pure state machine

        [Test]
        public void CaptureStep_LoneTeamCapturesAfterTheRequiredTicks()
        {
            CoreState state = CoreState.Empty();
            List<CoreChange> changes = new List<CoreChange>();
            List<TeamId> present = new List<TeamId> { CycleWorld.T1 };
            CycleRules.CaptureStep(ref state, present, 3, changes);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Kind, Is.EqualTo(CoreChangeKind.CaptureStarted));
            changes.Clear();
            CycleRules.CaptureStep(ref state, present, 3, changes);
            CycleRules.CaptureStep(ref state, present, 3, changes);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Kind, Is.EqualTo(CoreChangeKind.Captured));
            Assert.That(changes[0].Team, Is.EqualTo(CycleWorld.T1));
            Assert.That(state.Holder, Is.EqualTo(CycleWorld.T1));
            Assert.That(state.CaptureTeam.IsValid, Is.False);
        }

        [Test]
        public void CaptureStep_ContestFreezesAndAbsenceResets()
        {
            CoreState state = CoreState.Empty();
            List<CoreChange> changes = new List<CoreChange>();
            CycleRules.CaptureStep(ref state, new List<TeamId> { CycleWorld.T0 }, 10, changes);
            changes.Clear();
            // Two teams present: the capturer is among them, so progress stays at 1.
            CycleRules.CaptureStep(ref state, new List<TeamId> { CycleWorld.T0, CycleWorld.T1 }, 10, changes);
            Assert.That(changes, Is.Empty);
            Assert.That(state.CaptureProgressTicks, Is.EqualTo(1));
            // Two other teams present, the capturer gone: reset with "left".
            CycleRules.CaptureStep(ref state, new List<TeamId> { CycleWorld.T1, CycleWorld.T2 }, 10, changes);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Kind, Is.EqualTo(CoreChangeKind.CaptureReset));
            Assert.That(changes[0].Reason, Is.EqualTo("left"));
            Assert.That(changes[0].ProgressTicks, Is.EqualTo(1));
            Assert.That(state.CaptureTeam.IsValid, Is.False);
        }
    }
}

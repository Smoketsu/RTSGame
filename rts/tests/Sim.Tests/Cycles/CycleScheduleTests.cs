// rts/tests/Sim.Tests/Cycles/CycleScheduleTests.cs
// Acceptance tests 1, 9, 10, 11, 12 and 20 of rts/docs/specs/planetary_cycles.md section 7: the shift and warning schedule, the
// holder's pick (and every rejection), the no-repeat rule, the random choice without a holder, a pick cleared by a new holder, a script
// pick surviving a capture, and a match without cycles or without a Core. Part of Rts.Sim.Tests. Ticks are the tick an event is raised on.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Cycles;
using Rts.Sim.Data;
using Rts.Sim.Events;

namespace Rts.Sim.Tests.Cycles
{
    /// <summary>The cycle schedule, picks and the match without cycles.</summary>
    [TestFixture]
    public class CycleScheduleTests
    {
        /// <summary>A rig with the three shipped cycles. Team 0 captures the Core on tick 399 and stays.</summary>
        private static CycleRig HolderRig(ulong seed = 1234)
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles(), seed: seed);
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            return rig;
        }

        // ---------------------------------------------------------------- 1. schedule

        [Test]
        public void Schedule_WarningsAt3600And8400_ShiftsAt4800And9600()
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles());
            rig.RunThrough(9600);
            Assert.That(rig.TicksOf<CycleWarningEvent>(), Is.EqualTo(new List<int> { 3600, 8400 }));
            Assert.That(rig.TicksOf<CycleStartedEvent>(), Is.EqualTo(new List<int> { 4800, 9600 }));
            Assert.That(rig.At<CycleStartedEvent>(4800)!.PreviousCycleId, Is.EqualTo(string.Empty));
            Assert.That(rig.At<CycleStartedEvent>(4800)!.EndsAtTick, Is.EqualTo(9600));
            Assert.That(rig.At<CycleStartedEvent>(9600)!.EndsAtTick, Is.EqualTo(14400));
            Assert.That(rig.At<CycleStartedEvent>(9600)!.PreviousCycleId, Is.EqualTo(rig.At<CycleStartedEvent>(4800)!.CycleId));
            Assert.That(rig.At<CycleWarningEvent>(3600)!.ShiftTick, Is.EqualTo(4800));
            Assert.That(rig.At<CycleWarningEvent>(3600)!.CycleId, Is.EqualTo(rig.At<CycleStartedEvent>(4800)!.CycleId), "the warned cycle is the one that starts");
            Assert.That(rig.At<CycleWarningEvent>(3600)!.Method, Is.EqualTo("random"));
        }

        [Test]
        public void Schedule_ReadApi_TracksTheClock()
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles());
            Assert.That(rig.Sim.Cycles.NextShiftTick, Is.EqualTo(4800));
            Assert.That(rig.Sim.Cycles.NextLockTick, Is.EqualTo(3600));
            Assert.That(rig.Sim.Cycles.ActiveCycle, Is.Null);
            rig.RunThrough(3600);
            Assert.That(rig.Sim.Cycles.IsNextLocked, Is.True);
            Assert.That(rig.Sim.Cycles.NextCycle, Is.Not.Null);
            rig.RunThrough(4800);
            Assert.That(rig.Sim.Cycles.IsNextLocked, Is.False);
            Assert.That(rig.Sim.Cycles.ActiveCycle, Is.Not.Null);
            Assert.That(rig.Sim.Cycles.ActiveSinceTick, Is.EqualTo(4800));
            Assert.That(rig.Sim.Cycles.NextShiftTick, Is.EqualTo(9600));
        }

        [Test]
        public void Schedule_ZeroWarning_LocksAndShiftsOnTheSameTickWarningFirst()
        {
            CycleRig rig = new CycleRig(tuning: CycleWorld.EditedTuning(CycleWorld.ShortCycleEdits()), cycles: CycleWorld.ShippedCycles());
            rig.RunThrough(1201);
            Assert.That(rig.TicksOf<CycleWarningEvent>(), Is.EqualTo(new List<int> { 1200 }));
            Assert.That(rig.TicksOf<CycleStartedEvent>(), Is.EqualTo(new List<int> { 1200 }));
            Assert.That(rig.IndexOf<CycleWarningEvent>(1200), Is.LessThan(rig.IndexOf<CycleStartedEvent>(1200)));
        }

        [Test]
        public void Schedule_WarningEqualToTheInterval_IsALoadError()
        {
            // Interval 60 s with the shipped 60 s warning: equal, so the warning is not shorter than the interval.
            DataValidationException error = Expect.Throws<DataValidationException>(() => CycleWorld.EditedTuning(
                "tuning/match.json", "\"planetary_cycle_interval_seconds\": 240", "\"planetary_cycle_interval_seconds\": 60"));
            Assert.That(error.Message, Does.Contain("planetary_cycle_warning_seconds"));
            Assert.That(error.Message, Does.Contain("must be shorter than planetary_cycle_interval_seconds"));
        }

        // ---------------------------------------------------------------- 9. holder picks

        [Test]
        public void Pick_TheHolderPicksAndTheCycleStarts()
        {
            CycleRig rig = HolderRig();
            rig.Pick(1000, CycleWorld.P1, "quake");
            rig.Pick(1000, CycleWorld.P0, "storm");
            rig.Pick(1000, CycleWorld.P0, "quake");
            rig.Pick(3600, CycleWorld.P0, "quake");
            rig.Pick(3601, CycleWorld.P0, "quake");
            rig.RunThrough(4800);
            // Player 0's commands apply before player 1's: the unknown cycle is refused first, then the non-holder.
            Assert.That(rig.RejectionsAt(1000), Is.EqualTo(new List<string> { "unknown_cycle", "not_holder" }));
            CyclePickedEvent? picked = rig.At<CyclePickedEvent>(1000);
            Assert.That(picked, Is.Not.Null);
            Assert.That(picked!.Player, Is.EqualTo(CycleWorld.P0));
            Assert.That(picked.Team, Is.EqualTo(CycleWorld.T0));
            Assert.That(picked.CycleId, Is.EqualTo("quake"));
            Assert.That(rig.RejectionsAt(3600), Is.Empty, "a pick on the lock tick still counts");
            Assert.That(rig.RejectionsAt(3601), Is.EqualTo(new List<string> { "pick_locked" }));
            CycleWarningEvent? warning = rig.At<CycleWarningEvent>(3600);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning!.CycleId, Is.EqualTo("quake"));
            Assert.That(warning.ShiftTick, Is.EqualTo(4800));
            Assert.That(warning.Method, Is.EqualTo("picked"));
            Assert.That(warning.ChosenBy, Is.EqualTo(CycleWorld.T0));
            Assert.That(rig.At<CycleStartedEvent>(4800)!.CycleId, Is.EqualTo("quake"));
        }

        [Test]
        public void Pick_IsRejectedWithTheRightReason()
        {
            CycleRig rig = HolderRig();
            // Before the capture completes nobody holds the Core.
            rig.Pick(100, CycleWorld.P0, "quake");
            rig.Surrender(200, CycleWorld.P1);
            rig.Pick(300, CycleWorld.P1, "quake");
            rig.RunThrough(300);
            Assert.That(rig.RejectionsAt(100), Is.EqualTo(new List<string> { "not_holder" }));
            Assert.That(rig.RejectionsAt(300), Is.EqualTo(new List<string> { "player_out_of_match" }), "a player who left the match is told so before the holder check");
        }

        // ---------------------------------------------------------------- 10. no repeat

        [Test]
        public void NoRepeat_ARepeatedPlayStyleIsRefused()
        {
            CycleRig rig = HolderRig();
            rig.Pick(1000, CycleWorld.P0, "quake");
            rig.Pick(5000, CycleWorld.P0, "quake");
            rig.Pick(5001, CycleWorld.P0, "windstorm");
            rig.RunThrough(8400);
            Assert.That(rig.RejectionsAt(5000), Is.EqualTo(new List<string> { "repeats_play_style" }));
            Assert.That(rig.RejectionsAt(5001), Is.Empty);
            CycleWarningEvent? warning = rig.At<CycleWarningEvent>(8400);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning!.CycleId, Is.EqualTo("windstorm"));
            Assert.That(warning.Method, Is.EqualTo("picked"));
        }

        // ---------------------------------------------------------------- 11. random choice

        /// <summary>Runs 20 shifts without units or picks and returns the started cycle IDs in order, checking the RNG draw rule on the way.</summary>
        private static List<string> RandomRun(ulong seed)
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles(), seed: seed);
            // Rng draw count after the previous tick, to see on which ticks it changes.
            ulong before = rig.Sim.Rng.DrawCount;
            // Ticks on which the draw count changed.
            List<int> drawTicks = new List<int>();
            for (int tick = 0; tick <= 96000; tick++)
            {
                rig.RunThrough(tick);
                if (rig.Sim.Rng.DrawCount != before)
                {
                    drawTicks.Add(tick);
                    before = rig.Sim.Rng.DrawCount;
                }
            }
            Assert.That(drawTicks.Count, Is.EqualTo(20), "one lock per shift, so one draw per lock tick");
            foreach (int tick in drawTicks)
            {
                Assert.That((tick + 1200) % 4800, Is.EqualTo(0), "draws happen only on lock ticks");
            }
            Assert.That(rig.Sim.Rng.DrawCount, Is.GreaterThanOrEqualTo(20UL));
            List<string> started = new List<string>();
            foreach (CycleStartedEvent evt in rig.Of<CycleStartedEvent>())
            {
                started.Add(evt.CycleId);
            }
            return started;
        }

        [Test]
        public void RandomChoice_NeverRepeatsAPlayStyleAndIsDeterministic()
        {
            Dictionary<string, CycleFavour> favours = new Dictionary<string, CycleFavour>();
            foreach (CycleDefinition cycle in CycleWorld.ShippedCycles())
            {
                favours[cycle.Id] = cycle.Favours;
            }
            List<string> first = RandomRun(1);
            Assert.That(first.Count, Is.EqualTo(20));
            for (int i = 1; i < first.Count; i++)
            {
                Assert.That(favours[first[i]], Is.Not.EqualTo(favours[first[i - 1]]), "shift " + (i + 1));
            }
            Assert.That(RandomRun(1), Is.EqualTo(first), "the same seed gives the same sequence");
            Assert.That(RandomRun(2), Is.Not.EqualTo(first), "another seed gives another sequence");
        }

        // ---------------------------------------------------------------- 12. pick cleared, script pick survives

        [Test]
        public void PickIsClearedWhenAnotherTeamTakesTheCore()
        {
            CycleRig rig = HolderRig();
            EntityId holderUnit = new EntityId(1);
            rig.Spawn(500, CycleWorld.ScoutId, CycleWorld.P1, "17", "16");
            rig.Remove(600, holderUnit);
            rig.Pick(450, CycleWorld.P0, "quake");
            rig.RunThrough(998);
            Assert.That(rig.Sim.Cycles.PendingPickId, Is.EqualTo("quake"));
            rig.RunThrough(3600);
            Assert.That(rig.TicksOf<CoreCapturedEvent>(), Is.EqualTo(new List<int> { 399, 999 }));
            Assert.That(rig.At<CycleWarningEvent>(3600)!.Method, Is.EqualTo("random"), "the old holder's pick was dropped at the takeover");
        }

        [Test]
        public void ScriptPick_SurvivesACapture()
        {
            CycleRig rig = new CycleRig(cycles: CycleWorld.ShippedCycles());
            rig.ScriptPick(10, "quake");
            rig.Spawn(100, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.RunThrough(3600);
            Assert.That(rig.TicksOf<CoreCapturedEvent>(), Is.EqualTo(new List<int> { 499 }));
            CycleWarningEvent? warning = rig.At<CycleWarningEvent>(3600);
            Assert.That(warning!.CycleId, Is.EqualTo("quake"));
            Assert.That(warning.Method, Is.EqualTo("scripted"));
            Assert.That(warning.ChosenBy.IsValid, Is.False);
        }

        [Test]
        public void PickOfAHolderWhoLeavesTheMatchIsDropped()
        {
            // A holder who picks and then leaves the match takes the pick with it, so the lock falls back to the random choice.
            CycleRig rig = HolderRig();
            rig.Pick(450, CycleWorld.P0, "windstorm");
            rig.Surrender(460, CycleWorld.P0);
            rig.RunThrough(3600);
            Assert.That(rig.At<CycleWarningEvent>(3600)!.Method, Is.EqualTo("random"));
        }

        // ---------------------------------------------------------------- 20. disabled

        [Test]
        public void WithoutCycles_NoCycleEventsAndPicksAreRejected_CaptureStillWorks()
        {
            CycleRig rig = new CycleRig();
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.Pick(500, CycleWorld.P0, "quake");
            rig.RunThrough(9600);
            Assert.That(rig.Sim.Cycles.IsEnabled, Is.False);
            Assert.That(rig.Of<CycleWarningEvent>(), Is.Empty);
            Assert.That(rig.Of<CycleStartedEvent>(), Is.Empty);
            Assert.That(rig.RejectionsAt(500), Is.EqualTo(new List<string> { "cycles_disabled" }));
            Assert.That(rig.TicksOf<CoreCapturedEvent>(), Is.EqualTo(new List<int> { 399 }));
            Assert.That(rig.Sim.Cycles.AllowedNextCycles(), Is.Empty);
        }

        [Test]
        public void WithoutACore_NoCoreEventsAndPlayerPicksAreNotHolder()
        {
            CycleRig rig = new CycleRig(map: CycleWorld.Map(withCore: false), cycles: CycleWorld.ShippedCycles());
            rig.Spawn(0, CycleWorld.ScoutId, CycleWorld.P0, "16.5", "16.5");
            rig.Pick(500, CycleWorld.P0, "quake");
            rig.ScriptPick(600, "quake");
            rig.RunThrough(3600);
            Assert.That(rig.RejectionsAt(500), Is.EqualTo(new List<string> { "not_holder" }));
            Assert.That(rig.RejectionsAt(600), Is.Empty, "the script may still pick");
            Assert.That(rig.At<CycleWarningEvent>(3600)!.Method, Is.EqualTo("scripted"));
            foreach (SimEvent evt in rig.Events)
            {
                Assert.That(evt.Kind, Does.Not.StartWith("core_"), evt.ToString());
            }
        }

        // ---------------------------------------------------------------- config and start validation

        [Test]
        public void Config_SortsCyclesAndRejectsDuplicates()
        {
            List<CycleDefinition> cycles = CycleWorld.ShippedCycles();
            List<CycleDefinition> reversed = new List<CycleDefinition>(cycles);
            reversed.Reverse();
            CycleRig a = new CycleRig(cycles: cycles);
            CycleRig b = new CycleRig(cycles: reversed);
            Assert.That(b.Sim.ContentDigest, Is.EqualTo(a.Sim.ContentDigest), "the order the host lists the cycles in does not matter");
            Assert.That(a.Sim.Cycles.Definitions[0].Id, Is.EqualTo("core_surge"));
            cycles.Add(cycles[0]);
            Expect.Throws<System.ArgumentException>(() => new CycleRig(cycles: cycles));
        }

        [Test]
        public void Config_CyclesChangeTheContentDigest()
        {
            CycleRig none = new CycleRig();
            CycleRig some = new CycleRig(cycles: CycleWorld.ShippedCycles());
            Assert.That(some.Sim.ContentDigest, Is.Not.EqualTo(none.Sim.ContentDigest));
        }

        [Test]
        public void Start_RefusesCyclesOfASinglePlayStyle()
        {
            List<CycleDefinition> same = new List<CycleDefinition>
            {
                new CycleDefinition("a", "A", CycleFavour.Air, "x"),
                new CycleDefinition("b", "B", CycleFavour.Air, "y"),
            };
            Expect.Throws<System.InvalidOperationException>(() => new CycleRig(cycles: same));
        }
    }
}

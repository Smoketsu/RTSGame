// rts/tests/Replay.Tests/PlaybackTests.cs
// Tests of ReplayPlayback (spec player_interface_2.md gap G3): stepping to the end gives Verify's final hash, a tampered checkpoint is
// reported at the tick Verify reports, stepping past the end does nothing, and the refusals match Verify's. Part of Rts.Replay.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Replay;
using Rts.Sim.Data;

namespace Rts.Replay.Tests
{
    /// <summary>Stepwise playback against the one-call verifier.</summary>
    public sealed class PlaybackTests
    {
        /// <summary>Ticks recorded by the tests; covers several hash intervals. Unit: ticks. Range: 40 to 200.</summary>
        private const int Ticks = 100;

        /// <summary>Stepping to the end matches the recording, gives Verify's final hash and stops quietly past the end.</summary>
        [Test]
        public void SteppingToTheEndMatchesVerify()
        {
            DataSet data = ReplayTestSupport.LoadData();
            ReplayData replay = ReplayTestSupport.RecordScript(data, ReplayTestSupport.LoadScript("smoke.json"), Ticks);
            ReplayPlayback playback = new ReplayPlayback(replay, data);
            Assert.That(playback.IsFinished, Is.False);
            Assert.That(playback.DesyncTick, Is.EqualTo(-1));
            // Number of Step calls that ran a tick. Range: 0 to Ticks.
            int steps = 0;
            while (playback.Step())
            {
                steps++;
            }
            Assert.That(steps, Is.EqualTo(Ticks));
            Assert.That(playback.TickCount, Is.EqualTo(Ticks));
            Assert.That(playback.Sim.Clock.Tick, Is.EqualTo(Ticks));
            Assert.That(playback.IsFinished && playback.FinalMatches, Is.True);
            ReplayVerification verified = ReplayVerifier.Verify(replay, ReplayVerifier.CreateSimulation(replay, data));
            Assert.That(verified.IsMatch, Is.True);
            Assert.That(playback.FinalHash, Is.EqualTo(verified.FinalHash));
            Assert.That(playback.FinalHash, Is.EqualTo(replay.FinalHash));
            // Stepping past the end changes nothing.
            Assert.That(playback.Step(), Is.False);
            Assert.That(playback.Sim.Clock.Tick, Is.EqualTo(Ticks));
            Assert.That(playback.FinalHash, Is.EqualTo(replay.FinalHash));
        }

        /// <summary>A tampered checkpoint stops playback at that tick with the same tick and hashes as Verify.</summary>
        [Test]
        public void TamperedCheckpointDesyncsAtVerifysTick()
        {
            DataSet data = ReplayTestSupport.LoadData();
            ReplayData replay = ReplayTestSupport.RecordScript(data, ReplayTestSupport.LoadScript("smoke.json"), Ticks);
            Assert.That(replay.Checkpoints.Count, Is.GreaterThan(1));
            // Checkpoints with the second one's hash flipped.
            List<ReplayCheckpoint> tampered = new List<ReplayCheckpoint>(replay.Checkpoints);
            tampered[1] = new ReplayCheckpoint(tampered[1].Tick, tampered[1].Hash ^ 1UL);
            ReplayData bad = new ReplayData(replay.ContentDigest, replay.Seed, replay.MapId, replay.HashIntervalTicks, replay.TickCount,
                replay.Players, replay.Commands, tampered, replay.FinalHash, replay.CyclesEnabled);
            ReplayPlayback playback = new ReplayPlayback(bad, data);
            while (playback.Step())
            {
            }
            ReplayVerification verified = ReplayVerifier.Verify(bad, ReplayVerifier.CreateSimulation(bad, data));
            Assert.That(playback.IsFinished, Is.True);
            Assert.That(playback.FinalMatches, Is.False);
            Assert.That(playback.DesyncTick, Is.EqualTo(tampered[1].Tick));
            Assert.That(playback.DesyncTick, Is.EqualTo(verified.DesyncTick));
            Assert.That(playback.ExpectedHash, Is.EqualTo(verified.ExpectedHash));
            Assert.That(playback.ActualHash, Is.EqualTo(verified.ActualHash));
            Assert.That(playback.Sim.Clock.Tick, Is.EqualTo(tampered[1].Tick + 1), "stepping stopped on the tick that hashed (the clock advances after the hash)");
        }

        /// <summary>A digest, hash-interval or map mismatch is refused by the constructor like Verify refuses it.</summary>
        [Test]
        public void ConstructorRefusesLikeVerify()
        {
            DataSet data = ReplayTestSupport.LoadData();
            ReplayData replay = ReplayTestSupport.RecordScript(data, ReplayTestSupport.LoadScript("smoke.json"), 40);
            ReplayData otherDigest = new ReplayData(replay.ContentDigest + 1, replay.Seed, replay.MapId, replay.HashIntervalTicks, replay.TickCount,
                replay.Players, replay.Commands, replay.Checkpoints, replay.FinalHash);
            Expect.Throws<ReplayRefusedException>(() => new ReplayPlayback(otherDigest, data));
            ReplayData otherInterval = new ReplayData(replay.ContentDigest, replay.Seed, replay.MapId, replay.HashIntervalTicks + 1, replay.TickCount,
                replay.Players, replay.Commands, replay.Checkpoints, replay.FinalHash);
            Expect.Throws<ReplayRefusedException>(() => new ReplayPlayback(otherInterval, data));
            ReplayData otherMap = new ReplayData(replay.ContentDigest, replay.Seed, "no_such_map", replay.HashIntervalTicks, replay.TickCount,
                replay.Players, replay.Commands, replay.Checkpoints, replay.FinalHash);
            Expect.Throws<ReplayRefusedException>(() => new ReplayPlayback(otherMap, data));
        }
    }
}

// rts/tests/Sim.Tests/Ai/AiVarietyTests.cs
// Tests for package R of the skirmish AI (skirmish_ai_v4_rl.md R.5): the pin of a deterministic aggressive_s3 self-game taken before any
// package R code (R-0), then the seeded variety stream (jitter arithmetic, seeds, bounds, effective values, same seed twice).
// Part of Rts.Sim.Tests. Test code only (may use System.IO).
using System;
using System.IO;
using NUnit.Framework;
using Rts.Sim.Tests.Data;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package R: seeded variety.</summary>
    [TestFixture]
    public class AiVarietyTests
    {
        /// <summary>
        /// End tick of the traced game prototype_1v1, skai_vs_skai, seed 20261117, game 0, aggressive_s3 against aggressive_s3, recorded on
        /// main (e274808) before any package R code (skirmish_ai_v4_rl.md R-0). Unit: ticks. Range: fixed by the shipped data; changes only on purpose.
        /// </summary>
        private const int S3SelfTraceEndTick = 15462;

        /// <summary>The state hash that goes with <see cref="S3SelfTraceEndTick"/>, as 16 lower-case hex digits.</summary>
        private const string S3SelfTraceHash = "e5c323f688131dd0";

        /// <summary>The play signature (hash of every AI command) of the same game, as 16 lower-case hex digits.</summary>
        private const string S3SelfTraceSignature = "fde24604c8dc84e6";

        /// <summary>R-0: aggressive_s3 against itself plays exactly as before packages R and L: end tick, state hash and play signature are unchanged.</summary>
        [Test]
        public void S3SelfGame_IsUnchangedByPackagesRAndL()
        {
            // Temporary trace file the harness writes.
            string file = Path.Combine(Path.GetTempPath(), "r0_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                Assert.That(Rts.Sim.Tests.Harness.HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--trace", "--map", "prototype_1v1", "--matchup", "skai_vs_skai",
                    "--seed", "20261117", "--profile-a", "aggressive_s3", "--profile-b", "aggressive_s3", "--game", "0", "--out", file }, out string output, out string error), Is.EqualTo(0), error);
                Assert.That(output, Does.Contain("\"end_tick\":" + S3SelfTraceEndTick.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","));
                Assert.That(output, Does.Contain("\"state_hash\":\"" + S3SelfTraceHash + "\""));
                Assert.That(output, Does.Contain("\"play_signature\":\"" + S3SelfTraceSignature + "\""));
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}

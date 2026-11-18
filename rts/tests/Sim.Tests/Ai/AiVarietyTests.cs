// rts/tests/Sim.Tests/Ai/AiVarietyTests.cs
// Tests for package R of the skirmish AI (skirmish_ai_v4_rl.md R.5): the pin of a deterministic aggressive_s3 self-game taken before any
// package R code (R-0), then the seeded variety stream (jitter arithmetic, seeds, bounds, effective values, same seed twice).
// Part of Rts.Sim.Tests. Test code only (may use System.IO).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

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

        /// <summary>Player slot 0 (Skai in the greybox scenarios).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Water in the greybox scenarios).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

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

        /// <summary>The shipped aggressive_s3 text with the three package R keys switched on (the values of aggressive_s4r), parsed; it has no openings, so it does not change when openings are added to aggressive_s4r.</summary>
        private static AiProfile S3WithR()
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s3.json")).Replace("\r\n", "\n");
            text = text.Replace("\"composition_jitter_fraction\": 0,", "\"composition_jitter_fraction\": 0.25,")
                .Replace("\"expand_jitter_seconds\": 0,", "\"expand_jitter_seconds\": 20,")
                .Replace("\"attack_advantage_jitter\": 0,", "\"attack_advantage_jitter\": 0.15,");
            return AiProfile.Parse(text, "ai/aggressive_s3.json", Data.Content);
        }

        /// <summary>A started greybox_1v1 simulation, Skai in slot 0 and Water in slot 1.</summary>
        private static Simulation Greybox(string faction0 = "skai")
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction0, "A"),
                new PlayerSetup(P1, new TeamId(1), "water", "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            return sim;
        }

        /// <summary>Spawns the HQ of slot 0's faction at (7, 7) and steps until slot 0 may think (the AI does nothing without an HQ).</summary>
        private static void ReadyToThink(Simulation sim, SkirmishAiController controller, string hqDefinition)
        {
            sim.Submit(new Rts.Sim.Commands.SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, 0, hqDefinition, P0, FixVec2.FromInts(7, 7)));
            sim.Step();
            while (!controller.IsThinkTick(sim.Clock.Tick))
            {
                sim.Step();
            }
        }

        /// <summary>The variety values of one Create call as text: expansion offset, then the main composition weights in ordinal unit ID order.</summary>
        private static string Describe(AiVariety variety)
        {
            // The text being built.
            StringBuilder text = new StringBuilder();
            text.Append(variety.ExpandOffsetSeconds).Append(':');
            foreach (KeyValuePair<string, int> pair in variety.MainPlan.Composition)
            {
                text.Append(pair.Value).Append(',');
            }
            return text.ToString();
        }

        /// <summary>R-1: the jitter arithmetic of R.2.</summary>
        [Test]
        public void R1_JitterArithmetic()
        {
            Assert.That(AiVariety.Jitter(3, -25), Is.EqualTo(2));
            Assert.That(AiVariety.Jitter(3, 25), Is.EqualTo(4));
            Assert.That(AiVariety.Jitter(1, -25), Is.EqualTo(1));
            Assert.That(AiVariety.Jitter(100, 25), Is.EqualTo(100));
            Assert.That(AiVariety.Jitter(0, 25), Is.EqualTo(0));
            Assert.That(AiVariety.Jitter(4, 0), Is.EqualTo(4));
        }

        /// <summary>R-2: the variety seed differs between the two slots and from the controller's own seed, for seeds 1 to 32; adjacent game seeds and the two seats give mostly different first draws (a cheap decorrelation check).</summary>
        [Test]
        public void R2_Seeds()
        {
            // Match seed. Range: 1 to 32.
            for (ulong s = 1; s <= 32; s++)
            {
                Assert.That(AiVariety.SeedFor(s, P0), Is.Not.EqualTo(AiVariety.SeedFor(s, P1)), "slots, seed " + s);
                Assert.That(AiVariety.SeedFor(s, P0), Is.Not.EqualTo(SkirmishAiController.SeedFor(s, P0)), "controller stream, seed " + s);
                Assert.That(AiVariety.SeedFor(s, P1), Is.Not.EqualTo(SkirmishAiController.SeedFor(s, P1)), "controller stream, seed " + s);
            }
            // First draws (0 to 99) of both seats for the 32 adjacent seeds, as "a/b" pairs.
            SortedSet<string> firstDraws = new SortedSet<string>(StringComparer.Ordinal);
            // Match seed. Range: 1 to 32.
            for (ulong s = 1; s <= 32; s++)
            {
                DeterministicRandom a = new DeterministicRandom(AiVariety.SeedFor(s, P0));
                DeterministicRandom b = new DeterministicRandom(AiVariety.SeedFor(s, P1));
                firstDraws.Add(a.NextInt(100) + "/" + b.NextInt(100));
            }
            Assert.That(firstDraws.Count, Is.GreaterThanOrEqualTo(28), "32 seeds and two seats should give mostly different pairs");
            // The two seats of one seed must not draw the same values.
            DeterministicRandom p0 = new DeterministicRandom(AiVariety.SeedFor(7, P0));
            DeterministicRandom p1 = new DeterministicRandom(AiVariety.SeedFor(7, P1));
            // Draws that were equal between the seats. Unit: draws. Range: 0 to 32.
            int equal = 0;
            // Draw number. Range: 0 to 31.
            for (int i = 0; i < 32; i++)
            {
                equal += p0.NextInt(1000) == p1.NextInt(1000) ? 1 : 0;
            }
            Assert.That(equal, Is.LessThan(4));
        }

        /// <summary>The recorded <see cref="Describe"/> text for match seeds 1 to 8, slot 0, skai first and then water.</summary>
        private static readonly string[] CreatePin =
        {
            "-4:1,1,2,2,",
            "-20:1,1,2,3,",
            "-4:1,1,2,3,",
            "11:1,1,2,4,",
            "10:1,1,2,4,",
            "-8:1,1,2,3,",
            "5:1,1,2,4,",
            "20:1,1,2,4,",
            "-4:3,1,1,1,",
            "-20:4,1,1,1,",
            "-4:3,1,1,1,",
            "11:3,1,1,1,",
            "10:3,1,1,1,",
            "-8:3,1,1,1,",
            "5:3,1,1,1,",
            "20:3,1,1,1,",
        };

        /// <summary>R-3: Create is a pure function of its inputs; the values for seeds 1 to 8 (both factions, slot 0) are pinned.</summary>
        [Test]
        public void R3_CreateIsDeterministic()
        {
            AiProfile profile = S3WithR();
            AiFactionPlan skai = profile.RequireFaction("skai");
            Assert.That(Describe(AiVariety.Create(profile, skai, 7, 20)), Is.EqualTo(Describe(AiVariety.Create(profile, skai, 7, 20))));
            // The recorded values, in the order of CreatePin.
            List<string> seen = new List<string>();
            foreach (string faction in new[] { "skai", "water" })
            {
                // Match seed. Range: 1 to 8.
                for (ulong s = 1; s <= 8; s++)
                {
                    seen.Add(Describe(AiVariety.Create(profile, profile.RequireFaction(faction), AiVariety.SeedFor(s, P0), 20)));
                }
            }
            Assert.That(string.Join("|", seen), Is.EqualTo(string.Join("|", CreatePin)));
        }

        /// <summary>R-4: bounds of the expansion offset, the composition band and the advantage offset.</summary>
        [Test]
        public void R4_Bounds()
        {
            AiProfile profile = S3WithR();
            AiFactionPlan plan = profile.RequireFaction("skai");
            // Match seed. Range: 1 to 200.
            for (ulong s = 1; s <= 200; s++)
            {
                AiVariety variety = AiVariety.Create(profile, plan, AiVariety.SeedFor(s, P0), 20);
                Assert.That(variety.ExpandOffsetSeconds, Is.InRange(-20, 20));
                foreach (KeyValuePair<string, int> pair in plan.Composition)
                {
                    // Jittered weight. Unit: weight. Range: 0 to 100.
                    int moved = variety.MainPlan.Composition[pair.Key];
                    // Base weight. Unit: weight. Range: 0 to 100.
                    int w = pair.Value;
                    Assert.That(moved, Is.InRange(Math.Max(1, w * 75 / 100), Math.Min(100, (w * 125 + 99) / 100)), pair.Key + " seed " + s);
                }
            }
            AiVariety stream = AiVariety.Create(profile, plan, 99, 20);
            AiMemory memory = new AiMemory { Variety = stream };
            DeterministicRandom rng = new DeterministicRandom(5);
            // Distinct offsets seen over the calls (raw Fix64 values).
            SortedSet<long> offsets = new SortedSet<long>();
            // Call number. Range: 0 to 49.
            for (int i = 0; i < 50; i++)
            {
                AiBrain.EnterMassing(memory, profile, rng, 0);
                // True when the offset is k / 100 for a whole k in -15..15.
                bool onStep = false;
                // Step count. Range: -15 to 15.
                for (int k = -15; k <= 15; k++)
                {
                    onStep |= stream.AttackAdvantageOffset == Fix64.FromInt(k) / 100;
                }
                Assert.That(onStep, Is.True, stream.AttackAdvantageOffset.ToString());
                offsets.Add(stream.AttackAdvantageOffset.Raw);
            }
            Assert.That(offsets.Count, Is.GreaterThan(5), "50 draws over 31 steps should not repeat a handful of values");
        }

        /// <summary>R-5: the controller's own stream is untouched: aggressive_s3 and a profile with the R keys on end their first think with the same generator state and draw count.</summary>
        [Test]
        public void R5_OldStreamUntouched()
        {
            // Generator state and draw count after one think, per profile.
            List<string> results = new List<string>();
            foreach (AiProfile profile in new[] { AiProfile.FromDataSet(Data, "aggressive_s3"), S3WithR() })
            {
                Simulation sim = Greybox();
                SkirmishAiController controller = new SkirmishAiController(P0, profile);
                controller.OnMatchStart(sim);
                ReadyToThink(sim, controller, "skai.great_aerie");
                controller.Think(sim, new ListCommandSink(sim.Clock.Tick));
                results.Add(controller.Rng!.State + "/" + controller.Rng.DrawCount);
                Assert.That(controller.Rng.DrawCount, Is.EqualTo(1UL), "only the threshold jitter");
            }
            Assert.That(results[1], Is.EqualTo(results[0]));
        }

        /// <summary>R-6: every shipped profile but aggressive_s4r has no variety stream after OnMatchStart.</summary>
        [Test]
        public void R6_NoVarietyForOldProfiles()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                // First faction the profile plays (some test profiles have one plan only).
                string faction = new List<string>(profile.Factions.Keys)[0];
                Simulation sim = Greybox(faction);
                SkirmishAiController controller = new SkirmishAiController(P0, profile);
                controller.OnMatchStart(sim);
                if (id == "aggressive_s4r")
                {
                    Assert.That(controller.Memory.Variety, Is.Not.Null, id);
                }
                else
                {
                    Assert.That(controller.Memory.Variety, Is.Null, id);
                }
            }
        }

        /// <summary>R-7: the effective clock and bar values, clamped, and the profile values when there is no variety.</summary>
        [Test]
        public void R7_EffectiveValues()
        {
            AiProfile s3 = AiProfile.FromDataSet(Data, "aggressive_s3");
            AiVariety variety = AiVariety.ForTest(s3.RequireFaction("skai"), 1);
            variety.SetOffsetsForTest(-20, Fix64.Parse("0.15"));
            AiMemory memory = new AiMemory { Variety = variety };
            Assert.That(AiBrain.ExpandAtSeconds(s3, memory), Is.EqualTo(Fix64.FromInt(220)));
            Assert.That(AiBrain.AttackMinAdvantage(s3, memory), Is.EqualTo(Fix64.Parse("1.3") + Fix64.Parse("0.15")));
            // A profile whose clock is 10 s gives 0, not -10.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s3.json")).Replace("\r\n", "\n").Replace("\"expand_at_seconds\": 240,", "\"expand_at_seconds\": 10,");
            AiProfile early = AiProfile.Parse(text, "ai/aggressive_s3.json", Data.Content);
            Assert.That(AiBrain.ExpandAtSeconds(early, memory), Is.EqualTo(Fix64.Zero));
            AiMemory none = new AiMemory();
            Assert.That(AiBrain.ExpandAtSeconds(s3, none), Is.EqualTo(Fix64.FromInt(240)));
            Assert.That(AiBrain.AttackMinAdvantage(s3, none), Is.EqualTo(Fix64.Parse("1.3")));
        }

        /// <summary>R-8: the same seed twice gives the same play signature and state hash.</summary>
        [Test]
        public void R8_SameSeedTwice()
        {
            GameRecord a = GameRunner.Play(Data, "prototype_1v1", "water_vs_water", 0, 11, "aggressive_s4r", "aggressive_s4r", 300);
            GameRecord b = GameRunner.Play(Data, "prototype_1v1", "water_vs_water", 0, 11, "aggressive_s4r", "aggressive_s4r", 300);
            Assert.That(b.PlaySignature, Is.EqualTo(a.PlaySignature));
            Assert.That(b.StateHash, Is.EqualTo(a.StateHash));
        }

        /// <summary>Recorded number of distinct play signatures over seeds 1 to 8 of the R-9 game. Unit: signatures. Range: 1 to 8.</summary>
        private const int SeedsChangePlayDistinct = 6;

        /// <summary>R-9: different seeds change play: 6 of the 8 seeds give a different play signature in the first 300 s (measured; the spec guessed 7; the count is pinned).</summary>
        [Test]
        public void R9_SeedsChangePlay()
        {
            SortedSet<ulong> signatures = new SortedSet<ulong>();
            // Seed. Range: 1 to 8.
            for (ulong seed = 1; seed <= 8; seed++)
            {
                signatures.Add(GameRunner.Play(Data, "prototype_1v1", "water_vs_water", 0, seed, "aggressive_s4r", "aggressive_s4r", 300).PlaySignature);
            }
                        Assert.That(signatures.Count, Is.EqualTo(SeedsChangePlayDistinct));
        }
    }
}

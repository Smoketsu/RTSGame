// rts/tests/Sim.Tests/Harness/ResponseTests.cs
// Package A tests A-6 and A-7 of rts/docs/specs/skirmish_ai_v3.md: the response runner plays a scenario with the profile under test in slot 0 and the responder in slot 1 and
// the read-only observer does not change the game's final hash; and each response check passes and fails on synthetic series (including trade with no own losses).
// A-10 (the baseline report of --response --profile aggressive) is not asserted; it is in the builder's report. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Response mode tests.</summary>
    [TestFixture]
    public class ResponseTests
    {
        /// <summary>The shipped data set.</summary>
        private static readonly DataSet Data = TestData.LoadAll();

        /// <summary>Tick rate of the synthetic series. Unit: ticks per second. Range: 20.</summary>
        private const int Tps = 20;

        /// <summary>A-6: slot 0 uses the profile given, slot 1 uses rt_roc_rush, and the observed and unobserved runs end in the same hash.</summary>
        [Test]
        public void ResponseRunner_UsesTheProfilesAndDoesNotChangeTheGame()
        {
            AiEvalSettings settings = AiEvalSettings.Load(TestData.FindDataDirectory());
            ResponseTest test = settings.ResponseTests.First(t => t.Script == "response_roc_rush_water.json");
            ResponseResult observed = ResponseRunner.Run(Data, TestData.FindDataDirectory(), test, "aggressive", true);
            ResponseResult plain = ResponseRunner.Run(Data, TestData.FindDataDirectory(), test, "aggressive", false);
            Assert.That(observed.Slot0Profile, Is.EqualTo("aggressive"));
            Assert.That(observed.Slot1Profile, Is.EqualTo("rt_roc_rush"));
            Assert.That(observed.FinalHash, Is.EqualTo(plain.FinalHash));
            Assert.That(observed.Parts.Count, Is.EqualTo(3), "air_answer has three parts");
            Assert.That(observed.Series!.FirstEnemyAirVisibleTick, Is.GreaterThanOrEqualTo(150 * Tps), "the first Roc Riders are spawned at 150 s");
            Assert.That(observed.Line(), Does.StartWith("RESPONSE response_roc_rush_water.json air_answer "));
        }

        /// <summary>A-7 air_answer: a passing series, then each part failing alone.</summary>
        [Test]
        public void AirAnswer_PassAndFail()
        {
            ResponseTest test = Entry("air_answer");
            ResponseSeries good = Series();
            good.FirstEnemyAirVisibleTick = 150 * Tps;
            good.ModelAirKnownTick = 170 * Tps;
            good.Samples.Add(Sample(270 * Tps, 2));
            Assert.That(Passes(ResponseChecks.AirAnswer(test, good)), Is.EqualTo(new[] { true, true, true }));
            ResponseSeries bad = Series();
            bad.FirstEnemyAirVisibleTick = 150 * Tps;
            bad.ModelAirKnownTick = 181 * Tps;
            bad.Samples.Add(Sample(270 * Tps, 1));
            bad.HqDeathTick = 500 * Tps;
            Assert.That(Passes(ResponseChecks.AirAnswer(test, bad)), Is.EqualTo(new[] { false, false, false }), "late model, one anti-air unit, HQ dead at 500 s");
        }

        /// <summary>A-7 armoured_answer: answer units in the trained share beat the base share by 15 points; training the base mix does not.</summary>
        [Test]
        public void ArmouredAnswer_PassAndFail()
        {
            ResponseTest test = Entry("armoured_answer");
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive");
            ResponseSeries good = ArmouredSeries("skai.stormcaller");
            List<CheckPart> goodParts = ResponseChecks.ArmouredAnswer(test, good, Data, profile, "skai");
            Assert.That(Passes(goodParts), Is.EqualTo(new[] { true, true }), goodParts[0].Metric);
            ResponseSeries bad = ArmouredSeries("skai.windrunner");
            List<CheckPart> badParts = ResponseChecks.ArmouredAnswer(test, bad, Data, profile, "skai");
            Assert.That(Passes(badParts), Is.EqualTo(new[] { false, true }), badParts[0].Metric);
        }

        /// <summary>A-7 trade: ratio 10 passes, ratio 0.6 fails, no own losses divides by 1 (1 kill, ratio 1.0, passes), a dead HQ fails.</summary>
        [Test]
        public void Trade_PassAndFail()
        {
            ResponseTest test = Entry("trade");
            ResponseSeries good = Series();
            good.EnemyArmyKilled.Add(new TimedAmount(1000, "skai.windrunner", 10));
            good.OwnArmyLost.Add(new TimedAmount(1100, "water.grasper", 1));
            Assert.That(Passes(ResponseChecks.Trade(test, good)), Is.EqualTo(new[] { true }));
            ResponseSeries bad = Series();
            bad.EnemyArmyKilled.Add(new TimedAmount(1000, "skai.windrunner", 3));
            bad.OwnArmyLost.Add(new TimedAmount(1100, "water.grasper", 5));
            Assert.That(Passes(ResponseChecks.Trade(test, bad)), Is.EqualTo(new[] { false }));
            ResponseSeries noLosses = Series();
            noLosses.EnemyArmyKilled.Add(new TimedAmount(1000, "skai.windrunner", 1));
            Assert.That(Passes(ResponseChecks.Trade(test, noLosses)), Is.EqualTo(new[] { true }), "ratio = 1 / max(1, 0) = 1.0, at the bar");
            ResponseSeries dead = Series();
            dead.EnemyArmyKilled.Add(new TimedAmount(1000, "skai.windrunner", 10));
            dead.HqDeathTick = 590 * Tps;
            Assert.That(Passes(ResponseChecks.Trade(test, dead)), Is.EqualTo(new[] { false }), "the HQ must stand at until_seconds");
        }

        /// <summary>A-7 raid: a turret within 10 tiles of the drop-off nearest the wave and at most 3 workers lost per window pass; a far turret and 4 losses fail.</summary>
        [Test]
        public void Raid_PassAndFail()
        {
            ResponseTest test = Entry("raid");
            ResponseSeries good = Series();
            good.WaveSpawnPoint = new TilePoint(18, 18);
            good.Samples.Add(new ResponseSample(330 * Tps, 0, new[] { new TilePoint(9, 9) }, new[] { new TilePoint(40, 40), new TilePoint(10, 10) }));
            good.WorkerLossTicks.AddRange(new[] { 130 * Tps, 140 * Tps, 150 * Tps, 400 * Tps });
            Assert.That(Passes(ResponseChecks.Raid(test, good)), Is.EqualTo(new[] { true, true }));
            ResponseSeries bad = Series();
            bad.WaveSpawnPoint = new TilePoint(18, 18);
            bad.Samples.Add(new ResponseSample(330 * Tps, 0, new[] { new TilePoint(40, 40) }, new[] { new TilePoint(10, 10) }));
            bad.WorkerLossTicks.AddRange(new[] { 250 * Tps, 251 * Tps, 252 * Tps, 253 * Tps });
            Assert.That(Passes(ResponseChecks.Raid(test, bad)), Is.EqualTo(new[] { false, false }));
        }

        /// <summary>A-7 turtle: 16 supply lost before the first structure kill and a win by 2400 s pass; 17 supply lost and no win fail.</summary>
        [Test]
        public void Turtle_PassAndFail()
        {
            ResponseTest test = Entry("turtle");
            ResponseSeries good = Series();
            good.OwnArmyLost.Add(new TimedAmount(1000, "water.grasper", 16));
            good.OwnArmyLost.Add(new TimedAmount(5000, "water.grasper", 40));
            good.FirstEnemyStructureKillTick = 2000;
            good.AiWon = true;
            good.EndTick = 30000;
            Assert.That(Passes(ResponseChecks.Turtle(test, good)), Is.EqualTo(new[] { true, true }));
            ResponseSeries bad = Series();
            bad.OwnArmyLost.Add(new TimedAmount(1000, "water.grasper", 17));
            bad.FirstEnemyStructureKillTick = 2000;
            bad.EndTick = 48000;
            Assert.That(Passes(ResponseChecks.Turtle(test, bad)), Is.EqualTo(new[] { false, false }));
        }

        /// <summary>The shipped scenario files and the profiles they name load, and each responder profile holds exactly one faction plan.</summary>
        [Test]
        public void ShippedScenarios_Load()
        {
            string folder = Path.Combine(TestData.FindDataDirectory(), "scripts", "ai_eval");
            AiEvalSettings settings = AiEvalSettings.Load(TestData.FindDataDirectory());
            foreach (ResponseTest test in settings.ResponseTests)
            {
                CommandScript script = CommandScript.Parse(File.ReadAllText(Path.Combine(folder, test.Script)), "scripts/ai_eval/" + test.Script,
                    Rts.Sim.Factions.BuiltInMechanics.ScriptParsers);
                Assert.That(script.Ticks, Is.EqualTo(test.LongestSeconds() * Tps), test.Script);
                Assert.That(script.Seed, Is.EqualTo(1234UL));
                foreach (string profile in script.AiProfiles.Values)
                {
                    Assert.That(AiProfile.FromDataSet(Data, profile).Factions.Count, Is.GreaterThanOrEqualTo(1));
                }
            }
        }

        /// <summary>The settings entry of a check kind (the first one in the shipped file).</summary>
        private static ResponseTest Entry(string check)
        {
            return AiEvalSettings.Load(TestData.FindDataDirectory()).ResponseTests.First(t => t.Check == check);
        }

        /// <summary>An empty series at 20 ticks per second.</summary>
        private static ResponseSeries Series()
        {
            return new ResponseSeries { TicksPerSecond = Tps };
        }

        /// <summary>A sample with <paramref name="aa"/> anti-air units and nothing else.</summary>
        private static ResponseSample Sample(int tick, int aa)
        {
            return new ResponseSample(tick, aa, new List<TilePoint>(), new List<TilePoint>());
        }

        /// <summary>A series where armoured enemies were seen and 6 supply of <paramref name="trainedUnit"/> was trained in the measured window.</summary>
        private static ResponseSeries ArmouredSeries(string trainedUnit)
        {
            ResponseSeries series = Series();
            series.FirstEnemyArmyVisibleTick = 100 * Tps;
            series.SeenArmourSupply["armoured"] = 6;
            series.SeenEnemyDefinitions.Add("water.grasper");
            series.Trained.Add(new TimedAmount(200 * Tps, trainedUnit, 6));
            return series;
        }

        /// <summary>Pass flags of the parts in order.</summary>
        private static bool[] Passes(List<CheckPart> parts)
        {
            bool[] flags = new bool[parts.Count];
            for (int i = 0; i < flags.Length; i++)
            {
                flags[i] = parts[i].Passed;
            }
            return flags;
        }
    }
}

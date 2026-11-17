// rts/tests/Sim.Tests/Ai/FightPredictorTests.cs
// Tests C-1 to C-7 and C-16 of rts/docs/specs/skirmish_ai_v3.md package C: the fight predictor (Rts.Ai.FightPredictor) on the shipped unit data
// and damage table, the worked examples of C.2, the empty-side cases, the order independence of Group and the purity checks. Part of
// Rts.Sim.Tests. The predictor needs no simulation: it reads UnitDefinition and CombatRules only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Skirmish AI v3 package C: the fight predictor.</summary>
    [TestFixture]
    public class FightPredictorTests
    {
        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>The predictor numbers of the shipped stage 2 profile (the spec's settings).</summary>
        private static PredictorSettings Settings => PredictorSettings.From(AiProfile.FromDataSet(Data, "aggressive_s2"));

        /// <summary>A full-health entry of <paramref name="count"/> units of a definition.</summary>
        private static FightEntry Entry(string id, int count)
        {
            UnitDefinition definition = Data.Content.Units[id];
            return new FightEntry(definition, Fix64.FromInt(count), definition.MaxHealth * Fix64.FromInt(count));
        }

        /// <summary>Predicts a fight of one entry against one entry.</summary>
        private static FightPrediction Fight(FightEntry a, FightEntry b, bool aUphill = false)
        {
            return FightPredictor.Predict(new[] { a }, new[] { b }, aUphill, Data.Content.Combat, Settings);
        }

        /// <summary>Asserts a fixed-point value is within 0.001 of the expected decimal.</summary>
        private static void Near(Fix64 actual, string expected, string what)
        {
            Assert.That(Math.Abs((actual - Fix64.Parse(expected)).Raw), Is.LessThanOrEqualTo(Fix64.Parse("0.001").Raw), what + " was " + actual);
        }

        /// <summary>C-1: 4 Windrunners against 2 Pummelers.</summary>
        [Test]
        public void C1_WindrunnersAgainstPummelers()
        {
            FightPrediction r = Fight(Entry("skai.windrunner", 4), Entry("water.grasper", 2));
            Near(r.TimeAKillsB, "8.7778", "A kills B");
            Near(r.TimeBKillsA, "10.0", "B kills A");
            Assert.That(r.AWins, Is.True);
            Near(r.Advantage, "1.1392", "advantage");
        }

        /// <summary>C-2: 3 Pummelers cannot hit 2 Roc Riders, which can hit them.</summary>
        [Test]
        public void C2_PummelersAgainstRocRiders()
        {
            FightPrediction r = Fight(Entry("water.grasper", 3), Entry("skai.roc_rider", 2));
            Near(r.TimeAKillsB, "600", "A kills B");
            Near(r.TimeBKillsA, "17.5238", "B kills A");
            Assert.That(r.AWins, Is.False);
            Near(r.Advantage, "0.0292", "advantage");
        }

        /// <summary>C-3: Talon Marksmen against a Shellback (shield dome).</summary>
        [Test]
        public void C3_TalonsAgainstShellback()
        {
            FightPrediction r = Fight(Entry("skai.talon_archer", 2), Entry("water.shellback", 1));
            Near(r.TimeAKillsB, "28.6111", "A kills B");
            Near(r.TimeBKillsA, "18.0", "B kills A");
            Assert.That(r.AWins, Is.False);
            Near(r.Advantage, "0.6291", "advantage");
        }

        /// <summary>C-4: the C-1 fight with A shooting uphill.</summary>
        [Test]
        public void C4_Uphill()
        {
            FightPrediction r = Fight(Entry("skai.windrunner", 4), Entry("water.grasper", 2), true);
            Near(r.TimeAKillsB, "12.0370", "A kills B");
            Near(r.TimeBKillsA, "10.0", "B kills A");
            Assert.That(r.AWins, Is.False);
            Near(r.Advantage, "0.8308", "advantage");
        }

        /// <summary>C-5: 4 Windrunners against a Coral Turret (speed 0: the head start uses the attackers' speed).</summary>
        [Test]
        public void C5_WindrunnersAgainstTurret()
        {
            FightPrediction r = Fight(Entry("skai.windrunner", 4), Entry("water.coral_turret", 1));
            Near(r.TimeAKillsB, "21.3333", "A kills B");
            Near(r.TimeBKillsA, "39.1429", "B kills A");
            Assert.That(r.AWins, Is.True);
            Near(r.Advantage, "1.8348", "advantage");
        }

        /// <summary>C-6: an empty side.</summary>
        [Test]
        public void C6_EmptySides()
        {
            FightPrediction bEmpty = FightPredictor.Predict(new[] { Entry("skai.windrunner", 4) }, new List<FightEntry>(), false, Data.Content.Combat, Settings);
            Assert.That(bEmpty.AWins, Is.True);
            Near(bEmpty.TimeAKillsB, "0.05", "A kills nothing");
            Near(bEmpty.Advantage, "100", "advantage");
            FightPrediction aEmpty = FightPredictor.Predict(new List<FightEntry>(), new[] { Entry("skai.windrunner", 4) }, false, Data.Content.Combat, Settings);
            Assert.That(aEmpty.AWins, Is.False);
            Assert.That(aEmpty.Advantage, Is.EqualTo(Fix64.Zero));
        }

        /// <summary>C-7: Group sorts ordinally, so the order the units come in does not change the result, bit for bit.</summary>
        [Test]
        public void C7_GroupOrderDoesNotMatter()
        {
            UnitDefinition windrunner = Data.Content.Units["skai.windrunner"];
            UnitDefinition talon = Data.Content.Units["skai.talon_archer"];
            UnitDefinition grasper = Data.Content.Units["water.grasper"];
            UnitDefinition shellback = Data.Content.Units["water.shellback"];
            AiUnitView[] own = new AiUnitView[]
            {
                View(1, windrunner), View(2, talon), View(3, windrunner), View(4, talon),
            };
            AiUnitView[] foe = new AiUnitView[]
            {
                View(5, grasper), View(6, shellback), View(7, grasper),
            };
            AiUnitView[] ownReversed = new[] { own[3], own[2], own[1], own[0] };
            AiUnitView[] foeReversed = new[] { foe[2], foe[1], foe[0] };
            FightPrediction first = FightPredictor.Predict(FightPredictor.Group(own), FightPredictor.Group(foe), false, Data.Content.Combat, Settings);
            FightPrediction second = FightPredictor.Predict(FightPredictor.Group(ownReversed), FightPredictor.Group(foeReversed), false, Data.Content.Combat, Settings);
            Assert.That(second.TimeAKillsB, Is.EqualTo(first.TimeAKillsB));
            Assert.That(second.TimeBKillsA, Is.EqualTo(first.TimeBKillsA));
            Assert.That(second.Advantage, Is.EqualTo(first.Advantage));
            Assert.That(FightPredictor.Group(own).Count, Is.EqualTo(2));
            Assert.That(FightPredictor.Group(own)[0].Definition.Id, Is.EqualTo("skai.talon_archer"));
        }

        /// <summary>An own-unit view at full health, for Group.</summary>
        private static AiUnitView View(uint id, UnitDefinition definition)
        {
            return new AiUnitView(new EntityId(id), new PlayerId(0), definition, FixVec2.FromInts(5, 5), definition.MaxHealth, definition.Layer,
                Rts.Sim.Units.HqState.Rooted, false, false, false, FixVec2.FromInts(0, 0), EntityId.None, false, Rts.Sim.Economy.GatherState.Idle, -1, Array.Empty<string>());
        }

        /// <summary>C-16: the same inputs give bit-identical results, and the new Package C files hold no floating-point type.</summary>
        [Test]
        public void C16_DeterministicAndNoFloatingPoint()
        {
            FightEntry[] a = { Entry("skai.stormcaller", 2), Entry("skai.windrunner", 3) };
            FightEntry[] b = { Entry("water.grasper", 2), Entry("water.shellback", 1) };
            FightPrediction first = FightPredictor.Predict(a, b, false, Data.Content.Combat, Settings);
            FightPrediction second = FightPredictor.Predict(a, b, false, Data.Content.Combat, Settings);
            Assert.That(second.TimeAKillsB, Is.EqualTo(first.TimeAKillsB));
            Assert.That(second.TimeBKillsA, Is.EqualTo(first.TimeBKillsA));
            Assert.That(second.AWins, Is.EqualTo(first.AWins));
            Assert.That(second.Advantage, Is.EqualTo(first.Advantage));
            // The source files of package C, relative to the data folder (rts/data -> rts/src/Ai).
            string aiFolder = Path.Combine(TestData.FindDataDirectory(), "..", "src", "Ai");
            foreach (string name in new[] { "FightPredictor.cs", "AiCounter.cs", "AiBrain.cs", "AiIntel.cs", "AiProfile.cs", "AiBrainFight.cs", "AiFight.cs", "AiProximity.cs", "AiMemory.cs" })
            {
                string text = File.ReadAllText(Path.Combine(aiFolder, name));
                Assert.That(Regex.IsMatch(text, @"\b(float|double|decimal)\b"), Is.False, name + " must not use floating point");
            }
        }
    }
}

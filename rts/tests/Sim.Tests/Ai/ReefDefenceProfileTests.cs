// rts/tests/Sim.Tests/Ai/ReefDefenceProfileTests.cs
// Acceptance tests of package RR-1 (rts/docs/specs/ai_reef_runner_defence.md section 3): the candidate profile aggressive_s5_rr differs from aggressive_s5 only in the Reef Runner weight of Water's five
// compositions, its openings stay distinct, its Skai plays exactly like the baseline's Skai, and its Water trains Reef Runners beyond the scout. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Tests of the aggressive_s5_rr candidate profile.</summary>
    [TestFixture]
    public class ReefDefenceProfileTests
    {
        /// <summary>Baseline profile ID. Range: a shipped profile.</summary>
        private const string Baseline = "aggressive_s5";

        /// <summary>Candidate profile ID. Range: a shipped profile.</summary>
        private const string Candidate = "aggressive_s5_rr";

        /// <summary>Ladder base seed of the spec's tests. Range: any ulong.</summary>
        private const ulong BaseSeed = 20261026UL;

        /// <summary>The Reef Runner unit ID. Range: a unit of the data set.</summary>
        private const string Reef = "water.reef_runner";

        /// <summary>Loaded data, shared by the tests.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Adds every leaf of a parsed tree to <paramref name="into"/> as path = text, leaving out the root id and $doc and every Water composition.</summary>
        private static void Flatten(JsonValue node, string path, SortedDictionary<string, string> into)
        {
            if (path == "id" || path == "$doc" || (path.StartsWith("factions/water", StringComparison.Ordinal) && path.EndsWith("/composition", StringComparison.Ordinal)))
            {
                return;
            }
            switch (node.Kind)
            {
                case JsonKind.Object:
                    foreach (KeyValuePair<string, JsonValue> member in node.Members)
                    {
                        Flatten(member.Value, path.Length == 0 ? member.Key : path + "/" + member.Key, into);
                    }
                    break;
                case JsonKind.Array:
                    for (int i = 0; i < node.Items.Count; i++)
                    {
                        Flatten(node.Items[i], path + "[" + i + "]", into);
                    }
                    break;
                case JsonKind.String:
                    into[path] = node.AsString();
                    break;
                case JsonKind.Bool:
                    into[path] = node.AsBool().ToString();
                    break;
                case JsonKind.Number:
                    into[path] = node.NumberText;
                    break;
                default:
                    into[path] = "null";
                    break;
            }
        }

        /// <summary>The composition of <paramref name="baseline"/> plus water.reef_runner = 30.</summary>
        private static Dictionary<string, int> WithReef(IReadOnlyDictionary<string, int> baseline)
        {
            Dictionary<string, int> expected = new Dictionary<string, int>(baseline.Count + 1);
            foreach (KeyValuePair<string, int> pair in baseline)
            {
                expected[pair.Key] = pair.Value;
            }
            expected[Reef] = 30;
            return expected;
        }

        /// <summary>RR-1 1: the candidate parses; everything but id, $doc and Water's five compositions is equal to aggressive_s5; each Water composition is the baseline's plus water.reef_runner = 30.</summary>
        [Test]
        public void Rr1_ParsesAndDiffersOnlyInWaterReefWeights()
        {
            AiProfile baseline = AiProfile.FromDataSet(Data, Baseline);
            AiProfile candidate = AiProfile.FromDataSet(Data, Candidate);
            Assert.That(candidate.Id, Is.EqualTo(Candidate));
            SortedDictionary<string, string> a = new SortedDictionary<string, string>(StringComparer.Ordinal);
            SortedDictionary<string, string> b = new SortedDictionary<string, string>(StringComparer.Ordinal);
            Flatten(Data.AiProfiles[Baseline], string.Empty, a);
            Flatten(Data.AiProfiles[Candidate], string.Empty, b);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            foreach (KeyValuePair<string, string> pair in a)
            {
                Assert.That(b.TryGetValue(pair.Key, out string? other), Is.True, pair.Key);
                Assert.That(other, Is.EqualTo(pair.Value), pair.Key);
            }
            // The Skai plan field by field: composition and every opening composition.
            AiFactionPlan skaiA = baseline.Factions["skai"];
            AiFactionPlan skaiB = candidate.Factions["skai"];
            Assert.That(skaiB.Composition, Is.EquivalentTo(skaiA.Composition));
            Assert.That(skaiB.Openings.Count, Is.EqualTo(skaiA.Openings.Count));
            for (int i = 0; i < skaiA.Openings.Count; i++)
            {
                Assert.That(skaiB.Openings[i].Composition, Is.EquivalentTo(skaiA.Openings[i].Composition));
            }
            // Water: main composition and the four openings.
            AiFactionPlan waterA = baseline.Factions["water"];
            AiFactionPlan waterB = candidate.Factions["water"];
            Assert.That(waterB.Composition, Is.EquivalentTo(WithReef(waterA.Composition)));
            Assert.That(waterB.Openings.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(waterB.Openings[i].Composition, Is.EquivalentTo(WithReef(waterA.Openings[i].Composition)), waterA.Openings[i].Id);
            }
            Assert.That(waterB.Openings[1].Composition[Reef], Is.EqualTo(30), "water_grasper_pressure holds 30");
            // Re-pinned 25 Nov 2026: aggressive_s5 carries the weights itself now (was 10), so the baseline holds 30 too.
            Assert.That(waterA.Openings[1].Composition[Reef], Is.EqualTo(30));
        }

        /// <summary>RR-1 2: the four Water openings keep their IDs and weights and no two have the same composition.</summary>
        [Test]
        public void Rr1_OpeningsStayDistinct()
        {
            AiFactionPlan waterA = AiProfile.FromDataSet(Data, Baseline).Factions["water"];
            AiFactionPlan waterB = AiProfile.FromDataSet(Data, Candidate).Factions["water"];
            // Opening IDs and weights as "id=weight" in file order. Range: the four of the spec.
            string Text(AiFactionPlan plan)
            {
                List<string> parts = new List<string>();
                foreach (AiOpening opening in plan.Openings)
                {
                    parts.Add(opening.Id + "=" + opening.Weight);
                }
                return string.Join(",", parts);
            }
            Assert.That(Text(waterB), Is.EqualTo(Text(waterA)));
            Assert.That(Text(waterB), Is.EqualTo("water_standard=30,water_grasper_pressure=20,water_fast_expand=25,water_leviathan_tech=25"));
            for (int i = 0; i < waterB.Openings.Count; i++)
            {
                for (int j = i + 1; j < waterB.Openings.Count; j++)
                {
                    Assert.That(waterB.Openings[i].Composition, Is.Not.EquivalentTo(waterB.Openings[j].Composition), waterB.Openings[i].Id + " / " + waterB.Openings[j].Id);
                }
            }
        }

        /// <summary>RR-1 4: through the paired ladder path (skai_vs_skai, seed 20261026, 120 s) games 0 and 1 of pair 0 end with the same state hash and play signature: the candidate's Skai is the baseline's.</summary>
        [Test]
        public void Rr1_CandidateSkaiIsTheBaselineSkai()
        {
            GameRecord first = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_skai", 0, BaseSeed, Baseline, Candidate, 120, true);
            GameRecord second = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_skai", 1, BaseSeed, Baseline, Candidate, 120, true);
            Assert.That(second.StateHash, Is.EqualTo(first.StateHash));
            Assert.That(second.PlaySignature, Is.EqualTo(first.PlaySignature));
        }

        /// <summary>
        /// RR-1 5: skai_vs_water, pair 0 (slot 0 is Skai), 300 s, with the summary observer: the candidate's Water trains at least 4 Reef Runners and the baseline's Water fewer.
        /// Counts measured when written (300 s, seed 20261026, pair 0): candidate Water 19 Reef Runners, baseline Water 1.
        /// </summary>
        [Test]
        public void Rr1_WaterTrainsReefRunnersBeyondTheScout()
        {
            // Game 0 has the candidate in slot 0 (Skai); game 1 has the candidate in slot 1 (Water).
            GameRecord candidateWater = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_water", 1, BaseSeed, Baseline, Candidate, 300, true, 0UL, null, new SummaryObserver(30, new[] { 300 }));
            GameRecord baselineWater = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_water", 0, BaseSeed, Baseline, Candidate, 300, true, 0UL, null, new SummaryObserver(30, new[] { 300 }));
            Assert.That(candidateWater.Slot1Faction, Is.EqualTo("water"));
            Assert.That(baselineWater.Slot1Faction, Is.EqualTo("water"));
            Assert.That(candidateWater.Slot1Profile, Is.EqualTo(Candidate));
            Assert.That(baselineWater.Slot1Profile, Is.EqualTo(Baseline));
            // Reef Runners trained by Water in each game. Unit: units. Range: 0 upward.
            int candidateCount = candidateWater.Summary1!.Trained.TryGetValue(Reef, out int c) ? c : 0;
            int baselineCount = baselineWater.Summary1!.Trained.TryGetValue(Reef, out int b) ? b : 0;
            Assert.That(candidateCount, Is.AtLeast(4), "candidate Water Reef Runners trained (baseline " + baselineCount + ")");
            // Re-pinned 25 Nov 2026: aggressive_s5 carries the weights itself now (baseline was 1 Reef Runner), so both Waters train the same number.
            Assert.That(baselineCount, Is.EqualTo(candidateCount), "baseline " + baselineCount + " against candidate " + candidateCount);
        }
    }
}

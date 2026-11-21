// rts/tests/Sim.Tests/Harness/PoolTests.cs
// Acceptance tests O-1 to O-4 of rts/docs/specs/balance_measurement.md (package M, section M.3): the pool file and its errors, forced openings (AiProfile.WithForcedOpenings), the gate over synthetic member
// results and the skipped member and game count of a real (short) pool run. Part of Rts.Sim.Tests. The run test starts worker processes.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Opponent pool tests.</summary>
    [TestFixture]
    public class PoolTests
    {
        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>The shipped pool.json text with one edit applied (the old text must occur), parsed against the shipped data.</summary>
        private static PoolSettings Edited(string oldText, string newText)
        {
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), PoolSettings.RelativePath));
            Assert.That(text.Contains(oldText), Is.True, oldText);
            return PoolSettings.Parse(text.Replace(oldText, newText), TestData.LoadAll());
        }

        /// <summary>O-1: the shipped file parses (seven members, stage 1 has three); an unknown key, a duplicate member ID, odd pairs, an unknown profile and a forced opening that does not exist each fail naming the file and the field.</summary>
        [Test]
        public void PoolFile_ParsesAndRejectsBadInput()
        {
            PoolSettings shipped = PoolSettings.Load(TestData.FindDataDirectory(), TestData.LoadAll());
            Assert.That(shipped.Members.Count, Is.EqualTo(7));
            Assert.That(shipped.Stage1Pairs, Is.EqualTo(46));
            Assert.That(shipped.Require("s5_tech").ForceOpening["water"], Is.EqualTo("water_leviathan_tech"));
            // Pairs of (old text, new text, text the message must contain besides the file name).
            (string Old, string New, string Field)[] bad =
            {
                ("\"better_bonferroni\": true,", "\"better_bonferroni\": true, \"extra\": 1,", "extra"),
                ("\"id\": \"s4r\"", "\"id\": \"s3\"", "id"),
                ("\"stage1_pairs_per_cell\": 46", "\"stage1_pairs_per_cell\": 45", "stage1_pairs_per_cell"),
                ("\"profile\": \"aggressive_s3\"", "\"profile\": \"no_such_profile\"", "profile"),
                ("\"water\": \"water_standard\"", "\"water\": \"water_no_such_opening\"", "force_opening"),
                ("\"skai\": \"skai_standard\"", "\"nobody\": \"skai_standard\"", "force_opening"),
            };
            foreach ((string oldText, string newText, string field) in bad)
            {
                DataValidationException error = Expect.Throws<DataValidationException>(() => Edited(oldText, newText));
                Assert.That(error.Message, Does.StartWith(PoolSettings.SourceName).And.Contain(field), newText);
            }
        }

        /// <summary>O-2: WithForcedOpenings({skai: skai_air}) gives the opening skai_air for 20 seeds, leaves water's draws equal to the unforced profile's, keeps the later jitter draws aligned where the unforced draw was also skai_air, and rejects a missing faction or opening.</summary>
        [Test]
        public void ForcedOpening_ForcesAndKeepsTheStreamAligned()
        {
            DataSet data = TestData.LoadAll();
            AiProfile plain = AiProfile.FromDataSet(data, "aggressive_s5");
            AiProfile forced = plain.WithForcedOpenings(new Dictionary<string, string> { ["skai"] = "skai_air" });
            Assert.That(forced.Id, Is.EqualTo(plain.Id));
            Assert.That(forced.HarassSpeedRatio, Is.EqualTo(plain.HarassSpeedRatio));
            // Seeds where the unforced draw was also skai_air, so the aligned-stream claim was actually compared.
            int alignedSeeds = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                ulong streamSeed = AiVariety.SeedFor(seed * 7919UL, new PlayerId(0));
                AiVariety withForce = AiVariety.Create(forced, forced.RequireFaction("skai"), streamSeed, 20);
                AiVariety without = AiVariety.Create(plain, plain.RequireFaction("skai"), streamSeed, 20);
                Assert.That(withForce.OpeningId, Is.EqualTo("skai_air"), "seed " + seed);
                if (without.OpeningId == "skai_air")
                {
                    alignedSeeds++;
                    Assert.That(withForce.ExpandOffsetSeconds, Is.EqualTo(without.ExpandOffsetSeconds), "expansion offset, seed " + seed);
                    foreach (KeyValuePair<string, int> unit in without.MainPlan.Composition)
                    {
                        Assert.That(withForce.MainPlan.Composition[unit.Key], Is.EqualTo(unit.Value), "jitter step of " + unit.Key + ", seed " + seed);
                    }
                }
                AiVariety waterForced = AiVariety.Create(forced, forced.RequireFaction("water"), streamSeed, 20);
                AiVariety waterPlain = AiVariety.Create(plain, plain.RequireFaction("water"), streamSeed, 20);
                Assert.That(waterForced.OpeningId, Is.EqualTo(waterPlain.OpeningId), "water draw, seed " + seed);
                Assert.That(waterForced.ExpandOffsetSeconds, Is.EqualTo(waterPlain.ExpandOffsetSeconds));
            }
            Assert.That(alignedSeeds, Is.GreaterThan(0), "at least one seed must draw skai_air unforced");
            Expect.Throws<ArgumentException>(() => plain.WithForcedOpenings(new Dictionary<string, string> { ["skai"] = "skai_no_such" }));
            Expect.Throws<ArgumentException>(() => plain.WithForcedOpenings(new Dictionary<string, string> { ["nobody"] = "skai_air" }));
        }

        /// <summary>A "Pairs" all-row of w candidate-both, l baseline-both, s concordant and i incomplete synthetic pairs (every complete pair a distinct experiment).</summary>
        private static PairRow Row(int w, int l, int s, int i = 0)
        {
            List<PairOutcome> pairs = new List<PairOutcome>();
            ulong signature = 1;
            for (int k = 0; k < w + l + s; k++)
            {
                char cls = k < w ? PairOutcome.CandidateBoth : k < w + l ? PairOutcome.BaselineBoth : PairOutcome.Concordant;
                pairs.Add(new PairOutcome("prototype_1v1", "water_vs_water", k, true, true, cls, signature++, true));
            }
            for (int k = 0; k < i; k++)
            {
                pairs.Add(new PairOutcome("prototype_1v1", "water_vs_water", w + l + s + k, true, false, ' ', 0UL, false));
            }
            return PairedLadder.Row("all", pairs, AiEvalSettings.Load(TestData.FindDataDirectory()));
        }

        /// <summary>The statuses of the rows under the shipped settings, and the pool verdict.</summary>
        private static string Gate(bool bonferroni, params PairRow[] rows)
        {
            AiEvalSettings settings = AiEvalSettings.Load(TestData.FindDataDirectory());
            List<string> statuses = new List<string>();
            foreach (PairRow row in rows)
            {
                statuses.Add(PoolGate.Classify(row, rows.Length, bonferroni, settings));
            }
            return PoolGate.Verdict(statuses) + ":" + string.Join(",", statuses);
        }

        /// <summary>O-3: none worse and one better is PASS; one better and one worse is FAIL; nothing significant is NO_MEASURABLE_EFFECT; with 3 members and Bonferroni W 11, L 2 (p 0.0225, above 0.05 / 3) is not "better" (it is without the correction); an incomplete member with none worse gives INCOMPLETE.</summary>
        [Test]
        public void Gate_ClassifiesMembersAndThePool()
        {
            Assert.That(Gate(true, Row(10, 1, 20), Row(5, 5, 20)), Is.EqualTo("PASS:better,no_effect"));
            Assert.That(Gate(true, Row(10, 1, 20), Row(1, 10, 20)), Is.EqualTo("FAIL:better,worse"));
            Assert.That(Gate(true, Row(5, 5, 20), Row(6, 4, 20)), Is.EqualTo("NO_MEASURABLE_EFFECT:no_effect,no_effect"));
            Assert.That(Row(11, 2, 20).Test.PPt, Is.EqualTo(225));
            Assert.That(Gate(true, Row(11, 2, 20), Row(5, 5, 20), Row(5, 5, 20)), Is.EqualTo("NO_MEASURABLE_EFFECT:no_effect,no_effect,no_effect"));
            Assert.That(Gate(false, Row(11, 2, 20), Row(5, 5, 20), Row(5, 5, 20)), Is.EqualTo("PASS:better,no_effect,no_effect"));
            Assert.That(Gate(true, Row(5, 5, 10, 10), Row(5, 5, 20)), Is.EqualTo("INCOMPLETE:incomplete,no_effect"));
            // Worse wins over incomplete.
            Assert.That(Gate(true, Row(1, 10, 20, 20), Row(5, 5, 20)), Does.StartWith("FAIL:"));
        }

        /// <summary>
        /// O-4: a short real pool run (stage all, candidate aggressive_s5, one mirror cell, 2 pairs, 60 s games) skips member s5, runs the forced members, writes one record per game (games= equals the lines of pool.jsonl),
        /// each forced member's baseline side plays the forced opening, and the games are undecided so the verdict is INCOMPLETE (exit 2).
        /// </summary>
        [Test]
        public void PoolRun_SkipsTheCandidateAndCountsGames()
        {
            string folder = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            int exit = HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--pool", "--profile-b", "aggressive_s5", "--stage", "all", "--pairs", "2", "--matchups", "water_vs_water",
                "--max-game-seconds", "60", "--jobs", "4", "--out", folder }, out string stdout, out string stderr);
            string[] lines = stdout.TrimEnd('\n').Split('\n');
            Assert.That(exit, Is.EqualTo(2), stderr);
            Assert.That(lines, Does.Contain("MEMBER s5 skipped (candidate)"));
            Assert.That(stdout, Does.Contain("MEMBER s5_standard complete=").And.Contain("MEMBER s5_tech complete=").And.Contain("MEMBER s3 complete="));
            // 6 members play: s3, s4r and the four forced ones; 4 games each in the one cell.
            Assert.That(lines[lines.Length - 1], Is.EqualTo("POOL candidate=aggressive_s5 stage=all members=6 skipped=1 better=0 worse=0 no_effect=0 incomplete=6 games=24 verdict=INCOMPLETE"));
            string[] records = File.ReadAllLines(Path.Combine(folder, "pool.jsonl"));
            Assert.That(records.Length, Is.EqualTo(24));
            foreach (string line in records)
            {
                GameRecord record = GameRecord.Parse(line);
                Assert.That(record.Member, Is.Not.Empty);
                // The slot the member plays is the one that is not the candidate's; it carries the member's ID as its profile name.
                int memberSlot = 1 - record.CandidateSlot;
                string opening = memberSlot == 0 ? record.Opening0 : record.Opening1;
                if (record.Member == "s5_standard")
                {
                    Assert.That(opening, Is.EqualTo("water_standard"));
                }
                Assert.That(memberSlot == 0 ? record.Slot0Profile : record.Slot1Profile, Is.EqualTo(record.Member));
            }
            Assert.That(File.ReadAllText(Path.Combine(folder, "pool.md")), Does.Contain("# Gate"));
        }
    }
}

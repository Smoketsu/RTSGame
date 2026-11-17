// rts/tests/Sim.Tests/Harness/StatisticsTests.cs
// Package S tests S-1 to S-6 of rts/docs/specs/skirmish_ai_v4.md: the play signature ignores the seed and follows play, the distinct-game arithmetic, the distinct-share
// warning, the mirror and cross-faction rows, and the dome_absorbed stalemate reason of --fights. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Honest-statistics tests of the balance harness.</summary>
    [TestFixture]
    public class StatisticsTests
    {
        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>A data set whose aggressive_s3 has attack_supply_jitter 0, so the AI's only random draw always returns 0.</summary>
        private static DataSet NoJitterData()
        {
            return HarnessTestSupport.Load(HarnessTestSupport.CopyData(new[] { new DataEdit("ai/aggressive_s3.json", "\"attack_supply_jitter\": 2", "\"attack_supply_jitter\": 0") }));
        }

        /// <summary>S-1: seeds 1 and 2 give the same play signature and different state hashes; the same seed twice gives both equal.</summary>
        [Test]
        public void Signature_IgnoresTheSeed()
        {
            DataSet data = NoJitterData();
            GameRecord a = GameRunner.Play(data, "prototype_1v1", "water_vs_water", 0, 1, "aggressive_s3", "aggressive_s3", 120);
            GameRecord b = GameRunner.Play(data, "prototype_1v1", "water_vs_water", 0, 2, "aggressive_s3", "aggressive_s3", 120);
            GameRecord again = GameRunner.Play(data, "prototype_1v1", "water_vs_water", 0, 1, "aggressive_s3", "aggressive_s3", 120);
            Assert.That(b.PlaySignature, Is.EqualTo(a.PlaySignature));
            Assert.That(b.StateHash, Is.Not.EqualTo(a.StateHash));
            Assert.That(again.PlaySignature, Is.EqualTo(a.PlaySignature));
            Assert.That(again.StateHash, Is.EqualTo(a.StateHash));
            Assert.That(a.ToJsonLine(), Does.Contain(",\"play_signature\":\"" + a.PlaySignature.ToString("x16") + "\","));
            Assert.That(GameRecord.Parse(a.ToJsonLine()).PlaySignature, Is.EqualTo(a.PlaySignature));
        }

        /// <summary>S-2: the same game against another profile in slot 1 has another signature.</summary>
        [Test]
        public void Signature_ChangesWithPlay()
        {
            DataSet data = NoJitterData();
            GameRecord same = GameRunner.Play(data, "prototype_1v1", "water_vs_water", 0, 1, "aggressive_s3", "aggressive_s3", 120);
            GameRecord other = GameRunner.Play(data, "prototype_1v1", "water_vs_water", 0, 1, "aggressive_s3", "aggressive_s2b", 120);
            Assert.That(other.PlaySignature, Is.Not.EqualTo(same.PlaySignature));
        }

        /// <summary>S-3: signatures A A A B B C C C C D with candidate wins on A and C: 10 decided, 4 distinct, fraction 0.700, distinct fraction 0.500, Wilson over (2, 4).</summary>
        [Test]
        public void Distinct_Arithmetic()
        {
            LadderTally tally = new LadderTally();
            foreach (GameRecord record in Cell())
            {
                tally.Add(record);
            }
            Assert.That(tally.Decided, Is.EqualTo(10));
            Assert.That(tally.Distinct, Is.EqualTo(4));
            Assert.That(tally.DistinctWins, Is.EqualTo(2));
            Assert.That((double)tally.DistinctWins / tally.Distinct, Is.EqualTo(0.5));
            Assert.That(new LadderVerdict(tally.Decided, tally.CandidateWins, 1, 0.65).Fraction, Is.EqualTo(0.7).Within(1e-12));
            WinRate rate = HarnessStats.Wilson(tally.DistinctWins, 0, tally.Distinct - tally.DistinctWins, 1.96);
            Assert.That(rate, Is.EqualTo(HarnessStats.Wilson(2, 0, 2, 1.96)));
            Assert.That(rate.Games, Is.EqualTo(4));
            Assert.That(rate.P, Is.EqualTo(0.5));
        }

        /// <summary>S-4: the cell above warns at 0.8 (4 distinct of 10), not at 0.3, and the warning never touches the verdict.</summary>
        [Test]
        public void Distinct_WarningBelowMinimum()
        {
            List<string> warnings = LadderRunner.DistinctWarnings(Cell(), 0.8);
            Assert.That(warnings, Is.EqualTo(new[] { "WARNING m/skai_vs_water: 4 distinct of 10 decided games (0.400); seeds barely vary games, do not read these win rates as balance (P-005 decision 11)." }));
            Assert.That(LadderRunner.DistinctWarnings(Cell(), 0.3), Is.Empty);
            Assert.That(new LadderVerdict(10, 7, 1, 0.65).ExitCode, Is.EqualTo(0));
        }

        /// <summary>S-5: mirror 3 of 4, cross 2 of 4 (candidate as skai 2 of 2, as water 0 of 2).</summary>
        [Test]
        public void MirrorAndCross_Rows()
        {
            // Games 0 to 3 of each matchup: candidate slot 1, 1, 0, 0; every record has its own signature.
            List<GameRecord> records = new List<GameRecord>
            {
                Game("skai_vs_skai", "skai", "skai", 0, GameRecord.Slot1, 1), Game("skai_vs_skai", "skai", "skai", 1, GameRecord.Slot1, 2),
                Game("skai_vs_skai", "skai", "skai", 2, GameRecord.Slot0, 3), Game("skai_vs_skai", "skai", "skai", 3, GameRecord.Slot1, 4),
                Game("skai_vs_water", "skai", "water", 0, GameRecord.Slot0, 5), Game("skai_vs_water", "water", "skai", 1, GameRecord.Slot1, 6),
                Game("skai_vs_water", "skai", "water", 2, GameRecord.Slot0, 7), Game("skai_vs_water", "water", "skai", 3, GameRecord.Slot1, 8),
            };
            Assert.That(LadderRunner.StatsText(records), Is.EqualTo("distinct=8 mirror=3/4 cross=2/4"));
            SortedDictionary<string, LadderTally> rows = LadderRunner.MirrorCross(records);
            Assert.That(new List<string>(rows.Keys), Is.EqualTo(new[] { "cross", "cross, candidate as skai", "cross, candidate as water", "mirror" }));
            Assert.That(rows["cross, candidate as skai"].CandidateWins, Is.EqualTo(2));
            Assert.That(rows["cross, candidate as skai"].Decided, Is.EqualTo(2));
            Assert.That(rows["cross, candidate as water"].CandidateWins, Is.EqualTo(0));
            Assert.That(rows["cross, candidate as water"].Decided, Is.EqualTo(2));
            Assert.That(new LadderVerdict(8, 5, 1, 0.65).Line("c", "b", "distinct=8 mirror=3/4 cross=2/4"), Does.Contain(" fraction=0.625 distinct=8 mirror=3/4 cross=2/4 bar=0.650 "));
        }

        /// <summary>S-6: 4 Roc Riders against 4 Shellbacks is a dome_absorbed stalemate (their shots are all absorbed, none damage).</summary>
        [Test]
        public void Fights_DomeStalemateReason()
        {
            int exit = HarnessTestSupport.Run(new[] { "--data", TestDataFolder(), "--fights", "--only", "flat/750/skai.roc_rider/water.shellback" }, out string stdout, out string error);
            Assert.That(exit, Is.EqualTo(0), error + stdout);
            FightRecord record = FightRecord.Parse(stdout.TrimEnd('\n'));
            Assert.That(record.Result, Is.EqualTo(FightRecord.Stalemate));
            Assert.That(record.Reason, Is.EqualTo("dome_absorbed"));
            // The summary line counts it (the --only mode prints the record only, so build the summary directly).
            Assert.That(new FightSummary(new[] { record }, new List<CheckResult>()).Line(), Does.EndWith(" dome_absorbed=1"));
        }

        /// <summary>The shipped data folder.</summary>
        private static string TestDataFolder()
        {
            return Rts.Sim.Tests.Data.TestData.FindDataDirectory();
        }

        /// <summary>Ten decided games of one cell with signatures A A A B B C C C C D; the candidate wins on A and C (games 0 and 1 have the candidate in slot 1).</summary>
        private static List<GameRecord> Cell()
        {
            // Signature of each game; wins are games whose signature is 1 (A) or 3 (C).
            ulong[] signatures = { 1, 1, 1, 2, 2, 3, 3, 3, 3, 4 };
            List<GameRecord> records = new List<GameRecord>();
            for (int i = 0; i < signatures.Length; i++)
            {
                bool win = signatures[i] == 1 || signatures[i] == 3;
                // Candidate slot of game i * 4 is 1, so a slot1 result is a candidate win.
                records.Add(Game("skai_vs_water", "skai", "water", i * 4, win ? GameRecord.Slot1 : GameRecord.Slot0, signatures[i]));
            }
            return records;
        }

        /// <summary>A synthetic record on map m with the given factions, game index, result and play signature.</summary>
        private static GameRecord Game(string matchup, string slot0, string slot1, int game, string result, ulong signature)
        {
            return new GameRecord("m", matchup, game, 1, slot0, slot1, "A", "B", result, 10, 1, signature, 1, new List<HqKill>());
        }
    }
}

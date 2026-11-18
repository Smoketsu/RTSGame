// rts/tests/Sim.Tests/Harness/StatisticsTests.cs
// Package S tests S-1 to S-6 of rts/docs/specs/skirmish_ai_v4.md: the play signature ignores the seed and follows play, the distinct-game arithmetic, the distinct-share
// warning, the mirror and cross-faction rows, and the dome_absorbed stalemate reason of --fights; and tests H-1 to H-5 of rts/docs/specs/skirmish_ai_v4_rl.md
// (the opening fields of a game record, the opening rows and warnings of the ladder report and the mirror upper-bound warning). Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.IO;
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

        /// <summary>A synthetic skai_vs_skai record with openings, for H-2 to H-5.</summary>
        private static GameRecord OpeningGame(string opening0, string opening1, string result, ulong signature, int game = 0)
        {
            return new GameRecord("m", "skai_vs_skai", game, 1, "skai", "skai", "A", "B", result, 10, 1, signature, 1, new List<HqKill>(), opening0, opening1);
        }

        /// <summary>The eight decided games of H-2 (opening pairs a, b, c; slot 0 or slot 1 wins; signatures S1 to S7 as numbers 1 to 7).</summary>
        private static List<GameRecord> OpeningSet()
        {
            return new List<GameRecord>
            {
                OpeningGame("a", "b", GameRecord.Slot0, 1), OpeningGame("a", "b", GameRecord.Slot1, 2), OpeningGame("a", "a", GameRecord.Slot0, 3),
                OpeningGame("b", "c", GameRecord.Slot0, 4), OpeningGame("c", "a", GameRecord.Slot1, 5), OpeningGame("a", "b", GameRecord.Slot0, 1),
                OpeningGame("c", "c", GameRecord.Slot1, 6), OpeningGame("c", "b", GameRecord.Slot0, 7),
            };
        }

        /// <summary>The shipped ai_eval.json text with <paramref name="oldText"/> replaced, parsed.</summary>
        private static AiEvalSettings SettingsEdited(string oldText, string newText)
        {
            string text = File.ReadAllText(Path.Combine(TestDataFolder(), AiEvalSettings.RelativePath));
            Assert.That(text.Contains(oldText), Is.True, oldText);
            return AiEvalSettings.Parse(text.Replace(oldText, newText));
        }

        /// <summary>H-1: a record with opening0 "skai_air" and opening1 "" writes both after play_signature and parses back equal.</summary>
        [Test]
        public void H1_RecordRoundTrip()
        {
            GameRecord record = OpeningGame("skai_air", "", GameRecord.Slot0, 9);
            string line = record.ToJsonLine();
            Assert.That(line, Does.Contain(",\"play_signature\":\"0000000000000009\",\"opening0\":\"skai_air\",\"opening1\":\"\","));
            GameRecord back = GameRecord.Parse(line);
            Assert.That(back.Opening0, Is.EqualTo("skai_air"));
            Assert.That(back.Opening1, Is.EqualTo(string.Empty));
            Assert.That(back.ToJsonLine(), Is.EqualTo(line));
        }

        /// <summary>H-2: the opening rows of the eight synthetic games (16 draws).</summary>
        [Test]
        public void H2_OpeningArithmetic()
        {
            List<OpeningRow> rows = LadderRunner.OpeningRows(OpeningSet());
            Assert.That(rows.ConvertAll(r => r.Faction + "/" + r.Opening), Is.EqualTo(new[] { "skai/a", "skai/b", "skai/c" }));
            OpeningRow a = rows[0];
            Assert.That((a.Draws, a.UseShare, a.MirrorGames, a.MirrorWins, a.MirrorSignatures.Count, a.MirrorWinSignatures.Count), Is.EqualTo((6, 0.375, 4, 3, 3, 2)));
            OpeningRow b = rows[1];
            Assert.That((b.Draws, b.UseShare, b.MirrorGames, b.MirrorWins, b.MirrorSignatures.Count), Is.EqualTo((5, 0.3125, 5, 2, 4)));
            OpeningRow c = rows[2];
            Assert.That((c.Draws, c.UseShare, c.MirrorGames, c.MirrorWins), Is.EqualTo((5, 0.3125, 3, 1)));
            Assert.That(a.CrossGames + b.CrossGames + c.CrossGames, Is.EqualTo(0));
        }

        /// <summary>H-3: with opening_min_games 3, a (0.750) gets the "above 0.65" line, c (0.333) the "below 0.35" line, b (0.400) none; no use-share line.</summary>
        [Test]
        public void H3_Warnings()
        {
            AiEvalSettings settings = SettingsEdited("\"opening_min_games\": 10,", "\"opening_min_games\": 3,");
            List<string> warnings = LadderRunner.OpeningWarnings(LadderRunner.OpeningRows(OpeningSet()), settings);
            Assert.That(warnings, Is.EqualTo(new[]
            {
                "WARNING opening skai/a: wins 3 of 4 mirror games against other openings (0.750, above 0.65).",
                "WARNING opening skai/c: wins 1 of 3 mirror games against other openings (0.333, below 0.35).",
            }));
            // With the shipped opening_min_games 10 nothing has enough games.
            Assert.That(LadderRunner.OpeningWarnings(LadderRunner.OpeningRows(OpeningSet()), AiEvalSettings.Load(TestDataFolder())), Is.Empty);
            // A use share above 0.40 is reported whatever the game counts.
            List<GameRecord> skewed = new List<GameRecord> { OpeningGame("a", "a", GameRecord.Slot0, 1), OpeningGame("a", "b", GameRecord.Slot0, 2) };
            Assert.That(LadderRunner.OpeningWarnings(LadderRunner.OpeningRows(skewed), settings)[0], Is.EqualTo("WARNING opening skai/a: drawn in 0.750 of skai draws (above 0.40)."));
        }

        /// <summary>H-4: 40 distinct decided mirror games with 12 candidate wins give the mirror warning (Wilson upper bound about 0.454); with 16 wins none (about 0.554).</summary>
        [Test]
        public void H4_MirrorUpperBound()
        {
            AiEvalSettings settings = AiEvalSettings.Load(TestDataFolder());
            foreach ((int wins, bool warned) in new[] { (12, true), (16, false) })
            {
                // Games 0, 4, 8, ...: the candidate is in slot 1, so a slot1 result is a candidate win.
                List<GameRecord> records = new List<GameRecord>();
                for (int i = 0; i < 40; i++)
                {
                    records.Add(OpeningGame("", "", i < wins ? GameRecord.Slot1 : GameRecord.Slot0, (ulong)(i + 1), i * 4));
                }
                List<string> found = LadderRunner.MirrorWarnings(records, settings.LadderMirrorUpperMin, 1.96);
                Assert.That(found.Count, Is.EqualTo(warned ? 1 : 0), wins + " wins");
                if (warned)
                {
                    Assert.That(found[0], Does.StartWith("WARNING mirror: candidate wins 12 of 40 distinct mirror games, Wilson upper bound 0.45").And.Contain("below 0.50"));
                }
            }
        }

        /// <summary>H-5: records without openings give no opening row and no opening warning.</summary>
        [Test]
        public void H5_NoOpenings()
        {
            List<GameRecord> records = new List<GameRecord> { OpeningGame("", "", GameRecord.Slot0, 1), OpeningGame("", "", GameRecord.Slot1, 2) };
            Assert.That(LadderRunner.OpeningRows(records), Is.Empty);
            Assert.That(LadderRunner.OpeningWarnings(LadderRunner.OpeningRows(records), SettingsEdited("\"opening_min_games\": 10,", "\"opening_min_games\": 1,")), Is.Empty);
        }

        /// <summary>H-6 (not in the spec): a real small ladder of aggressive_s4r on greybox_1v1 writes the opening fields to ladder.jsonl and a "## Openings" section to ladder.md; one of aggressive_s3 does not.</summary>
        [Test]
        public void H6_LadderReportSection()
        {
            string data = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch);
            foreach (string profile in new[] { "aggressive_s4r", "aggressive_s3" })
            {
                string outDir = HarnessTestSupport.NewFolder();
                int exit = HarnessTestSupport.Run(new[] { "--data", data, "--ladder", "--profile-a", profile, "--profile-b", profile, "--games", "4", "--maps", "greybox_1v1",
                    "--matchups", "skai_vs_skai", "--max-game-seconds", "120", "--jobs", "1", "--out", outDir }, out string stdout, out string error);
                Assert.That(exit, Is.EqualTo(2).Or.EqualTo(0), error + stdout);
                string jsonl = File.ReadAllText(Path.Combine(outDir, "ladder.jsonl"));
                string markdown = File.ReadAllText(Path.Combine(outDir, "ladder.md"));
                if (profile == "aggressive_s4r")
                {
                    Assert.That(jsonl, Does.Contain("\"opening0\":\"skai_"));
                    Assert.That(markdown, Does.Contain("## Openings"));
                }
                else
                {
                    Assert.That(jsonl, Does.Contain("\"opening0\":\"\",\"opening1\":\"\""));
                    Assert.That(markdown, Does.Not.Contain("## Openings"));
                }
            }
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

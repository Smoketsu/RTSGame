// rts/tests/Sim.Tests/Harness/LadderTests.cs
// Package A tests A-1 to A-5 of rts/docs/specs/skirmish_ai_v3.md: the ladder seat rule, one game played twice with the same profile, the ladder pass arithmetic, the
// determinism of ladder.jsonl across --jobs and the validation of scripts/ai_eval/ai_eval.json. The ladder and determinism tests start worker processes. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Ladder mode tests.</summary>
    [TestFixture]
    public class LadderTests
    {
        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>A-1: games 0 to 3 put profile A in slot 0 for games 0 and 1 and profile B for games 2 and 3, and the factions of skai_vs_water swap on odd games.</summary>
        [Test]
        public void SeatRule_CoversEverySeatAndFactionCombination()
        {
            string[] slot0Profiles = new string[4];
            string[] slot0Factions = new string[4];
            for (int g = 0; g < 4; g++)
            {
                slot0Profiles[g] = GameRunner.LadderProfileInSlot(g, 0, "A", "B");
                slot0Factions[g] = GameRunner.FactionInSlot("skai_vs_water", g, 0);
                Assert.That(GameRunner.LadderProfileInSlot(g, 1, "A", "B"), Is.Not.EqualTo(slot0Profiles[g]), "the two seats never share a profile");
            }
            Assert.That(slot0Profiles, Is.EqualTo(new[] { "A", "A", "B", "B" }));
            Assert.That(slot0Factions, Is.EqualTo(new[] { "skai", "water", "skai", "water" }));
            Assert.That(new[] { LadderTally.CandidateSlot(0), LadderTally.CandidateSlot(1), LadderTally.CandidateSlot(2), LadderTally.CandidateSlot(3) }, Is.EqualTo(new[] { 1, 1, 0, 0 }));
        }

        /// <summary>A-2: the same game with (aggressive, aggressive) twice gives identical record lines, and the line names both profiles.</summary>
        [Test]
        public void SameProfileTwice_GivesIdenticalRecords()
        {
            DataSet data = TestData.LoadAll();
            ulong seed = SeedDerivation.GameSeed(20261117UL, "greybox_1v1", "skai_vs_water", 0);
            string first = GameRunner.Play(data, "greybox_1v1", "skai_vs_water", 0, seed, "aggressive", "aggressive", 120).ToJsonLine();
            string second = GameRunner.Play(data, "greybox_1v1", "skai_vs_water", 0, seed, "aggressive", "aggressive", 120).ToJsonLine();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("\"profile_slot0\":\"aggressive\",\"profile_slot1\":\"aggressive\""));
            // Different profiles per slot reach the record in slot order.
            string mixed = GameRunner.Play(data, "greybox_1v1", "skai_vs_water", 0, seed, "aggressive", "defensive", 60).ToJsonLine();
            Assert.That(mixed, Does.Contain("\"profile_slot0\":\"aggressive\",\"profile_slot1\":\"defensive\""));
            Assert.That(GameRecord.Parse(mixed).Slot1Profile, Is.EqualTo("defensive"));
        }

        /// <summary>A-3: the verdict arithmetic, with the numbers of the spec, and draws counting as decided games that are not wins.</summary>
        [Test]
        public void Verdict_Arithmetic()
        {
            LadderVerdict pass = new LadderVerdict(210, 140, 200, 0.65);
            Assert.That(pass.Fraction, Is.EqualTo(140.0 / 210.0).Within(1e-12));
            Assert.That(pass.Pass, Is.True);
            Assert.That(pass.ExitCode, Is.EqualTo(0));
            Assert.That(pass.Line("cand", "base"), Is.EqualTo("LADDER candidate=cand baseline=base decided=210 wins=140 fraction=0.667 bar=0.650 PASS"));
            LadderVerdict few = new LadderVerdict(150, 120, 200, 0.65);
            Assert.That(few.Pass, Is.False, "too few decided games");
            Assert.That(few.ExitCode, Is.EqualTo(2));
            Assert.That(new LadderVerdict(200, 129, 200, 0.65).Pass, Is.False);
            Assert.That(new LadderVerdict(200, 130, 200, 0.65).Pass, Is.True);
            Assert.That(new LadderVerdict(200, 90, 200, 0.45).Pass, Is.True, "non-regression bar");
            Assert.That(new LadderVerdict(0, 0, 1, 0.0).Pass, Is.False, "nothing decided never passes");

            // Tally: game 0 has the candidate in slot 1, game 2 in slot 0; a draw is decided and not a win; an undecided game is not decided.
            LadderTally tally = new LadderTally();
            tally.Add(Record(0, GameRecord.Slot1));
            tally.Add(Record(2, GameRecord.Slot0));
            tally.Add(Record(1, GameRecord.Slot0));
            tally.Add(Record(3, GameRecord.Draw));
            tally.Add(Record(0, GameRecord.Undecided));
            Assert.That(tally.Games, Is.EqualTo(5));
            Assert.That(tally.CandidateWins, Is.EqualTo(2));
            Assert.That(tally.BaselineWins, Is.EqualTo(1));
            Assert.That(tally.Draws, Is.EqualTo(1));
            Assert.That(tally.Undecided, Is.EqualTo(1));
            Assert.That(tally.Decided, Is.EqualTo(4));
        }

        /// <summary>A-4: --jobs 1 and --jobs 2 write byte-identical ladder.jsonl and ladder.md for 1 map, 1 matchup and 4 games at a 120 s cap.</summary>
        [Test]
        public void Ladder_OutputDoesNotDependOnJobs()
        {
            string data = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch);
            string[] jsonl = new string[2];
            string[] markdown = new string[2];
            string[] stdout = new string[2];
            for (int run = 0; run < 2; run++)
            {
                string outDir = HarnessTestSupport.NewFolder();
                int exit = HarnessTestSupport.Run(new[] { "--data", data, "--ladder", "--profile-a", "aggressive", "--profile-b", "aggressive", "--games", "4", "--maps", "greybox_1v1",
                    "--matchups", "skai_vs_water", "--max-game-seconds", "120", "--jobs", run == 0 ? "1" : "2", "--out", outDir }, out stdout[run], out string error);
                Assert.That(exit, Is.EqualTo(2).Or.EqualTo(0), error + stdout[run]);
                jsonl[run] = File.ReadAllText(Path.Combine(outDir, "ladder.jsonl"));
                markdown[run] = File.ReadAllText(Path.Combine(outDir, "ladder.md"));
            }
            Assert.That(jsonl[1], Is.EqualTo(jsonl[0]));
            Assert.That(markdown[1], Is.EqualTo(markdown[0]));
            Assert.That(stdout[1], Is.EqualTo(stdout[0]));
            Assert.That(jsonl[0].Split('\n').Length, Is.EqualTo(5), "four game lines and the final newline");
            Assert.That(stdout[0].TrimEnd('\n').Split('\n')[^1], Does.StartWith("LADDER candidate=aggressive baseline=aggressive decided="));
        }

        /// <summary>A-5: a games-per-cell value that is not a multiple of 4 and an unknown check kind are errors naming the file and the field; the shipped file parses.</summary>
        [Test]
        public void Settings_AreValidated()
        {
            string path = Path.Combine(TestData.FindDataDirectory(), "scripts", "ai_eval", "ai_eval.json");
            string text = File.ReadAllText(path);
            AiEvalSettings shipped = AiEvalSettings.Parse(text);
            Assert.That(shipped.ResponseTests.Count, Is.EqualTo(10));
            Assert.That(shipped.LadderGamesPerCell, Is.EqualTo(72));
            foreach (ResponseTest test in shipped.ResponseTests)
            {
                Assert.That(File.Exists(Path.Combine(TestData.FindDataDirectory(), "scripts", "ai_eval", test.Script)), Is.True, test.Script);
            }
            DataValidationException games = Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(text.Replace("\"ladder_games_per_cell\": 72", "\"ladder_games_per_cell\": 10")));
            Assert.That(games.Message, Does.Contain("scripts/ai_eval/ai_eval.json").And.Contain("ladder_games_per_cell"));
            DataValidationException kind = Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(text.Replace("\"check\": \"air_answer\"", "\"check\": \"nope\"")));
            Assert.That(kind.Message, Does.Contain("scripts/ai_eval/ai_eval.json").And.Contain("response_tests[0].check"));
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(text.Replace("\"ladder_verify_games\": 2,", "\"ladder_verify_games\": 2, \"bogus\": 1,")));
            Assert.That(unknown.Message, Does.Contain("bogus"));
        }

        /// <summary>A synthetic game record of the given game index and result.</summary>
        private static GameRecord Record(int game, string result)
        {
            return new GameRecord("m", "skai_vs_water", game, 1, "skai", "water", "A", "B", result, 10, 1, 1, 1, new List<HqKill>());
        }
    }
}

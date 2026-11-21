// rts/tests/Sim.Tests/Harness/MeasurementTests.cs
// Acceptance tests P-1 to P-10 of rts/docs/specs/balance_measurement.md (package M, sections M.1 and M.2): the paired layout, full pairing holding the random draws, the pairing kind, self-pairing,
// the exact sign test, the interval and advantage, incomplete and distinct pairs, the minimum detectable effect, the unchanged unpaired ladder with its EFFECT line, and determinism across --jobs.
// Real games use prototype_1v1 and short caps; the statistics tests use synthetic records. Part of Rts.Sim.Tests. The ladder tests start worker processes.
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
    /// <summary>Paired ladder and stated power tests.</summary>
    [TestFixture]
    public class MeasurementTests
    {
        /// <summary>Alpha in parts per 10 000 used by the statistics tests (0.05 two-sided).</summary>
        private const int Alpha = 500;

        /// <summary>The integer Z of 1.96 + 0.8416.</summary>
        private const int Z = 28016;

        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>P-1: in paired mode games 0 to 7 of skai_vs_water have pair g / 2, candidate slot g % 2, slot-0 faction skai for pairs 0 and 2 and water for 1 and 3, and both games of a pair share the seed of the pair.</summary>
        [Test]
        public void PairedLayout_PairSlotFactionAndSeed()
        {
            DataSet data = TestData.LoadAll();
            ulong baseSeed = 20261117UL;
            GameRecord[] records = new GameRecord[8];
            for (int g = 0; g < 8; g++)
            {
                records[g] = GameRunner.PlayLadder(data, "prototype_1v1", "skai_vs_water", g, baseSeed, "abl_s5_no_h", "aggressive_s5", 10, true);
            }
            for (int g = 0; g < 8; g++)
            {
                int pair = g / 2;
                Assert.That(records[g].Pair, Is.EqualTo(pair), "pair of game " + g);
                Assert.That(records[g].CandidateSlot, Is.EqualTo(g % 2), "candidate slot of game " + g);
                Assert.That(records[g].Slot0Faction, Is.EqualTo(pair % 2 == 0 ? "skai" : "water"), "slot-0 faction of game " + g);
                Assert.That(records[g].Seed, Is.EqualTo(SeedDerivation.GameSeed(baseSeed, "prototype_1v1", "skai_vs_water", pair)), "seed of game " + g);
                // The candidate's profile sits in its slot.
                Assert.That(g % 2 == 0 ? records[g].Slot0Profile : records[g].Slot1Profile, Is.EqualTo("aggressive_s5"));
                Assert.That(g % 2 == 0 ? records[g].Slot1Profile : records[g].Slot0Profile, Is.EqualTo("abl_s5_no_h"));
            }
            // The record line carries the keys after the last existing one, and parses back.
            string line = records[3].ToJsonLine();
            Assert.That(line, Does.EndWith("],\"pair\":1,\"candidate_slot\":1}"));
            Assert.That(GameRecord.Parse(line).ToJsonLine(), Is.EqualTo(line));
            Assert.That(GameRecord.Parse(line).Pair, Is.EqualTo(1));
        }

        /// <summary>P-2: (aggressive_s5, abl_s5_no_h) has pairing kind full for both factions; for 8 seeds and both slots the two profiles draw the same opening, expansion offset and jitter steps; in played pairs the openings are equal.</summary>
        [Test]
        public void FullPairing_HoldsTheDraws()
        {
            DataSet data = TestData.LoadAll();
            AiProfile s5 = AiProfile.FromDataSet(data, "aggressive_s5");
            AiProfile noH = AiProfile.FromDataSet(data, "abl_s5_no_h");
            Assert.That(PairedLadder.PairingText(noH, s5, new[] { "water", "skai" }), Is.EqualTo("skai:full,water:full"));
            // How many equal-weight units the jitter comparison saw, so the check cannot pass vacuously.
            int compared = 0;
            foreach (string faction in new[] { "skai", "water" })
            {
                AiFactionPlan planS5 = s5.RequireFaction(faction);
                AiFactionPlan planNoH = noH.RequireFaction(faction);
                for (ulong seed = 1; seed <= 8; seed++)
                {
                    for (byte slot = 0; slot < 2; slot++)
                    {
                        ulong streamSeed = AiVariety.SeedFor(seed * 7919UL, new PlayerId(slot));
                        AiVariety a = AiVariety.Create(s5, planS5, streamSeed, 20);
                        AiVariety b = AiVariety.Create(noH, planNoH, streamSeed, 20);
                        Assert.That(b.OpeningId, Is.EqualTo(a.OpeningId), faction + " seed " + seed + " slot " + slot);
                        Assert.That(b.ExpandOffsetSeconds, Is.EqualTo(a.ExpandOffsetSeconds));
                        foreach (KeyValuePair<string, int> unit in planS5.Composition)
                        {
                            if (planNoH.Composition.TryGetValue(unit.Key, out int other) && other == unit.Value)
                            {
                                compared++;
                                Assert.That(b.MainPlan.Composition[unit.Key], Is.EqualTo(a.MainPlan.Composition[unit.Key]), "jitter step of " + unit.Key);
                            }
                        }
                    }
                }
            }
            Assert.That(compared, Is.GreaterThan(0));
            // Played pairs: opening0 and opening1 are equal across the two games of each pair.
            for (int pair = 0; pair < 3; pair++)
            {
                GameRecord first = GameRunner.PlayLadder(data, "prototype_1v1", "water_vs_water", pair * 2, 20261117UL, "abl_s5_no_h", "aggressive_s5", 10, true);
                GameRecord second = GameRunner.PlayLadder(data, "prototype_1v1", "water_vs_water", pair * 2 + 1, 20261117UL, "abl_s5_no_h", "aggressive_s5", 10, true);
                Assert.That(second.Opening0, Is.EqualTo(first.Opening0));
                Assert.That(second.Opening1, Is.EqualTo(first.Opening1));
            }
            // Openings held counts complete pairs with equal openings (synthetic complete pairs).
            List<GameRecord> synthetic = new List<GameRecord> { Game(0, GameRecord.Slot0, 1, "a", "b"), Game(1, GameRecord.Slot0, 2, "a", "b"), Game(2, GameRecord.Slot0, 3, "a", "b"), Game(3, GameRecord.Slot0, 4, "a", "c") };
            PairRow row = PairedLadder.Row("x", PairedLadder.Outcomes(synthetic), Settings());
            Assert.That(row.Complete, Is.EqualTo(2));
            Assert.That(row.OpeningsHeld, Is.EqualTo(1));
        }

        /// <summary>P-3: a profile with a different opening library gives pairing kind seed (aggressive_s5 against aggressive_s3, which has no openings); equal profiles give full. (The forced-opening pool member of the spec belongs to M.3.)</summary>
        [Test]
        public void PairingKind_SeedForADifferentLibrary()
        {
            DataSet data = TestData.LoadAll();
            AiProfile s5 = AiProfile.FromDataSet(data, "aggressive_s5");
            AiProfile s3 = AiProfile.FromDataSet(data, "aggressive_s3");
            Assert.That(PairedLadder.PairingKind(s3, s5, "skai"), Is.EqualTo("seed"));
            Assert.That(PairedLadder.PairingKind(s3, s5, "water"), Is.EqualTo("seed"));
            Assert.That(PairedLadder.PairingKind(s5, s5, "skai"), Is.EqualTo("full"));
        }

        /// <summary>P-5: the exact sign test on synthetic counts (W 10, L 1: 3/256 and PASS; W 1, L 10: FAIL; W 9, L 2: 67/1024 not significant; no discordant pair: p 1).</summary>
        [Test]
        public void SignTest_ExactNumbersAndVerdicts()
        {
            SignTestResult pass = PairedStats.SignTest(10, 1, Alpha);
            Assert.That(PairedStats.P4(pass.PPt), Is.EqualTo("0.0117"));
            Assert.That(pass.Significant, Is.True);
            Assert.That(PairedStats.Verdict(pass), Is.EqualTo("PASS"));
            SignTestResult fail = PairedStats.SignTest(1, 10, Alpha);
            Assert.That(PairedStats.P4(fail.PPt), Is.EqualTo("0.0117"));
            Assert.That(PairedStats.Verdict(fail), Is.EqualTo("FAIL"));
            SignTestResult none = PairedStats.SignTest(9, 2, Alpha);
            Assert.That(PairedStats.P4(none.PPt), Is.EqualTo("0.0654"));
            Assert.That(none.Significant, Is.False);
            Assert.That(PairedStats.Verdict(none), Is.EqualTo("NO_MEASURABLE_EFFECT"));
            SignTestResult empty = PairedStats.SignTest(0, 0, Alpha);
            Assert.That(PairedStats.P4(empty.PPt), Is.EqualTo("1.0000"));
            Assert.That(PairedStats.Verdict(empty), Is.EqualTo("NO_MEASURABLE_EFFECT"));
            Assert.That(PairedStats.P4(PairedStats.SignTest(5, 5, Alpha).PPt), Is.EqualTo("1.0000"), "p is capped at 1");
            // The edge is inclusive: W 10 of D 11 is the smallest significant count at 0.05 (65 of 108 and 20 of 27 are stated in M.2).
            Assert.That(PairedStats.SignTest(9, 2, Alpha).Significant, Is.False);
            Assert.That(PairedStats.SignTest(20, 7, Alpha).Significant, Is.True);
            Assert.That(PairedStats.SignTest(19, 8, Alpha).Significant, Is.False);
            Assert.That(PairedStats.SignTest(65, 43, Alpha).Significant, Is.True);
            Assert.That(PairedStats.SignTest(64, 44, Alpha).Significant, Is.False);
            Assert.That(PairedStats.ExitCode("PASS"), Is.EqualTo(0));
            Assert.That(PairedStats.ExitCode("FAIL"), Is.EqualTo(2));
            Assert.That(PairedStats.ExitCode("INCOMPLETE"), Is.EqualTo(2));
            Assert.That(PairedStats.ExitCode("NO_MEASURABLE_EFFECT"), Is.EqualTo(4));
        }

        /// <summary>P-6: the Clopper-Pearson grid and the advantage (W 10, D 11, n 31: jL 587, jH 998, +14.5 [+3.0, +17.7]; W 20, D 27: jL 537, jH 889).</summary>
        [Test]
        public void Interval_AndAdvantage()
        {
            int[] grid = PairedStats.Interval(10, 11, Alpha);
            Assert.That(grid, Is.EqualTo(new[] { 587, 998 }));
            Assert.That(PairedStats.Signed(PairedStats.AdvantageTenths(10, 1, 31)), Is.EqualTo("+14.5"));
            int[] advantage = PairedStats.AdvantageInterval(grid[0], grid[1], 11, 31);
            Assert.That(PairedStats.Signed(advantage[0]) + " " + PairedStats.Signed(advantage[1]), Is.EqualTo("+3.0 +17.7"));
            Assert.That(PairedStats.Interval(20, 27, Alpha), Is.EqualTo(new[] { 537, 889 }));
            Assert.That(PairedStats.Interval(0, 0, Alpha), Is.EqualTo(new[] { 0, 1000 }));
            Assert.That(PairedStats.Interval(0, 5, Alpha)[0], Is.EqualTo(0));
            Assert.That(PairedStats.Interval(5, 5, Alpha)[1], Is.EqualTo(1000));
            Assert.That(PairedStats.Signed(PairedStats.AdvantageTenths(1, 4, 20)), Is.EqualTo("-7.5"));
            Assert.That(PairedStats.Signed(0), Is.EqualTo("+0.0"));
        }

        /// <summary>A synthetic paired game record of the cell water_vs_water (mirror): game g, result, play signature and the two openings; the candidate's slot is g % 2 and the pair g / 2.</summary>
        private static GameRecord Game(int game, string result, ulong signature, string opening0 = "", string opening1 = "", string matchup = "water_vs_water")
        {
            string[] factions = matchup.Split(new[] { "_vs_" }, StringSplitOptions.None);
            int pair = game / 2;
            return new GameRecord("prototype_1v1", matchup, game, (ulong)pair, factions[pair % 2], factions[(pair + 1) % 2], game % 2 == 0 ? "b" : "a", game % 2 == 0 ? "a" : "b", result, 100, 1UL, signature, 2UL,
                new List<HqKill>(), opening0, opening1, pair, game % 2);
        }

        /// <summary>A synthetic complete pair of the given class at pair index p: W = candidate won both (slot 0 in game 2p, slot 1 in game 2p + 1), L = baseline won both, S = the same slot won both.</summary>
        private static List<GameRecord> Pair(int p, char cls, ulong signature)
        {
            string even = cls == 'W' ? GameRecord.Slot0 : cls == 'L' ? GameRecord.Slot1 : GameRecord.Slot0;
            string odd = cls == 'W' ? GameRecord.Slot1 : cls == 'L' ? GameRecord.Slot0 : GameRecord.Slot0;
            return new List<GameRecord> { Game(p * 2, even, signature), Game(p * 2 + 1, odd, signature + 1000000) };
        }

        /// <summary>The shipped effect settings.</summary>
        private static AiEvalSettings Settings()
        {
            return AiEvalSettings.Load(TestData.FindDataDirectory());
        }

        /// <summary>P-5 and P-6 from records: W 10, L 1 and S 20 synthetic pairs read through the pair outcomes give the counts, p 0.0117 and a PASS on the all row.</summary>
        [Test]
        public void Rows_FromSyntheticPairs_GiveCountsAndVerdict()
        {
            List<GameRecord> records = new List<GameRecord>();
            int p = 0;
            for (int i = 0; i < 10; i++) { records.AddRange(Pair(p++, 'W', (ulong)(10 + i * 2))); }
            records.AddRange(Pair(p++, 'L', 100));
            for (int i = 0; i < 20; i++) { records.AddRange(Pair(p++, 'S', (ulong)(200 + i * 2))); }
            records.Sort(BatchRunner.CompareGames);
            AiEvalSettings settings = Settings();
            List<PairRow> rows = PairedLadder.Rows(PairedLadder.Outcomes(records), settings);
            PairRow all = rows[rows.Count - 1];
            Assert.That(all.Label, Is.EqualTo("all"));
            Assert.That(new[] { all.Pairs, all.Complete, all.N, all.S, all.W, all.L }, Is.EqualTo(new[] { 31, 31, 31, 20, 10, 1 }));
            Assert.That(PairedStats.P4(all.Test.PPt), Is.EqualTo("0.0117"));
            Assert.That(PairedLadder.Verdict(all, settings), Is.EqualTo("PASS"));
            Assert.That(PairedStats.Signed(all.AdvantageTenths), Is.EqualTo("+14.5"));
            Assert.That(rows.ConvertAll(r => r.Label), Is.EqualTo(new[] { "prototype_1v1 / water_vs_water", "mirror", "all" }));
            string line = PairedLadder.Line("cand", "base", all, "water:full", PairedLadder.Verdict(all, settings));
            Assert.That(line, Does.StartWith("PAIRED candidate=cand baseline=base pairs=31 complete=31 distinct=31 concordant=20 candidate_both=10 baseline_both=1 p=0.0117 advantage=+14.5 interval=[+3.0,+17.7] mde="));
            Assert.That(line, Does.EndWith(" pairing=water:full verdict=PASS"));
        }

        /// <summary>P-7: a pair with a draw and a pair with an undecided game are incomplete and out of the test; two complete pairs with equal pair signatures count once; too few complete pairs give INCOMPLETE and exit 2.</summary>
        [Test]
        public void IncompleteAndDistinctPairs()
        {
            AiEvalSettings settings = Settings();
            List<GameRecord> records = new List<GameRecord>();
            records.AddRange(Pair(0, 'W', 10));
            records.AddRange(Pair(1, 'W', 10));
            records.Add(Game(4, GameRecord.Draw, 50));
            records.Add(Game(5, GameRecord.Slot1, 51));
            records.Add(Game(6, GameRecord.Slot0, 60));
            records.Add(Game(7, GameRecord.Undecided, 61));
            records.Sort(BatchRunner.CompareGames);
            List<PairOutcome> outcomes = PairedLadder.Outcomes(records);
            Assert.That(outcomes.Count, Is.EqualTo(4));
            Assert.That(outcomes.ConvertAll(o => o.Complete), Is.EqualTo(new[] { true, true, false, false }));
            Assert.That(outcomes[0].Signature, Is.EqualTo(outcomes[1].Signature), "the same two play signatures in the same order are the same experiment");
            PairRow row = PairedLadder.Row("all", outcomes, settings);
            Assert.That(row.Pairs, Is.EqualTo(4));
            Assert.That(row.Complete, Is.EqualTo(2));
            Assert.That(row.N, Is.EqualTo(1), "two equal pair signatures count once");
            Assert.That(row.PlainW, Is.EqualTo(2));
            Assert.That(row.W, Is.EqualTo(1));
            Assert.That(PairedLadder.Verdict(row, settings), Is.EqualTo("INCOMPLETE"), "2 of 4 complete is below 0.9");
            Assert.That(PairedStats.ExitCode("INCOMPLETE"), Is.EqualTo(2));
            // The edge: 9 of 10 pairs complete is exactly paired_min_complete_fraction and is not incomplete.
            List<GameRecord> nine = new List<GameRecord>();
            for (int p = 0; p < 9; p++) { nine.AddRange(Pair(p, 'W', (ulong)(p * 10 + 1))); }
            nine.Add(Game(18, GameRecord.Draw, 900));
            nine.Add(Game(19, GameRecord.Slot0, 901));
            nine.Sort(BatchRunner.CompareGames);
            Assert.That(PairedLadder.Verdict(PairedLadder.Row("all", PairedLadder.Outcomes(nine), settings), settings), Is.EqualTo("PASS"));
            // Unpaired records are refused.
            GameRecord unpaired = new GameRecord("prototype_1v1", "water_vs_water", 0, 1UL, "water", "water", "a", "b", GameRecord.Slot0, 1, 1UL, 1UL, 1UL, new List<HqKill>());
            Assert.That(Expect.Throws<HarnessException>(() => PairedLadder.Outcomes(new List<GameRecord> { unpaired })).Message, Does.Contain("no pair"));
        }

        /// <summary>P-8: the minimum detectable effect (unpaired n 216: 9.6, n 144: 11.7; paired n 108 with D 54, 27, 11, 0: 9.6, 6.8, 4.4, 3.2; D_min 6) and the planning arithmetic of M.2.</summary>
        [Test]
        public void Mde_WorkedNumbers()
        {
            Assert.That(PairedStats.Plain(PairedStats.MdeUnpaired(216, Z)), Is.EqualTo("9.6"));
            Assert.That(PairedStats.Plain(PairedStats.MdeUnpaired(144, Z)), Is.EqualTo("11.7"));
            Assert.That(PairedStats.Plain(PairedStats.MdePaired(108, 108, Z, Alpha)), Is.EqualTo("13.5"));
            Assert.That(PairedStats.Plain(PairedStats.MdePaired(108, 54, Z, Alpha)), Is.EqualTo("9.6"));
            Assert.That(PairedStats.Plain(PairedStats.MdePaired(108, 27, Z, Alpha)), Is.EqualTo("6.8"));
            Assert.That(PairedStats.Plain(PairedStats.MdePaired(108, 11, Z, Alpha)), Is.EqualTo("4.4"));
            Assert.That(PairedStats.Plain(PairedStats.MdePaired(108, 0, Z, Alpha)), Is.EqualTo("3.2"));
            Assert.That(PairedStats.Plain(PairedStats.MdePaired(72, 36, Z, Alpha)), Is.EqualTo("11.7"));
            Assert.That(PairedStats.Plain(PairedStats.MdePaired(72, 18, Z, Alpha)), Is.EqualTo("8.3"));
            Assert.That(PairedStats.Plain(PairedStats.MdePaired(72, 7, Z, Alpha)), Is.EqualTo("5.2"));
            Assert.That(PairedStats.MinDiscordant(Alpha), Is.EqualTo(6));
            Assert.That(PairedStats.Z(Fix64.Parse("1.96"), Fix64.Parse("0.8416")), Is.EqualTo(Z));
            Assert.That(PairedStats.MdeUnpaired(0, Z), Is.EqualTo(-1));
            Assert.That(PairedStats.Plain(-1), Is.EqualTo("-"));
            // Unpaired 5.0 points needs n >= 785 games: 400 x 785 x 50^2 >= 28016^2 and 784 is not enough.
            Assert.That(PairedStats.MdeUnpaired(785, Z), Is.EqualTo(50));
            Assert.That(PairedStats.MdeUnpaired(784, Z), Is.EqualTo(51));
            // Planning (n >= Z^2 r / (400 m^2)): 5.2 points at r = 0.5 needs 363 pairs; 10.4 points needs 91 (M.3.5: 92 pairs give 10.4 points at r = 0.5).
            Assert.That(PairedStats.PairsNeeded(52, 5000, Z), Is.EqualTo(363));
            Assert.That(PairedStats.PairsNeeded(104, 5000, Z), Is.EqualTo(91));
        }

        /// <summary>The settings file: the shipped effect values are read as parts per 10 000, and a bad value, a missing key or an unknown key fails naming the file and the field.</summary>
        [Test]
        public void EffectSettings_AreValidated()
        {
            AiEvalSettings settings = Settings();
            Assert.That(settings.EffectAlphaPt, Is.EqualTo(500));
            Assert.That(settings.PairedMinCompletePt, Is.EqualTo(9000));
            Assert.That(PairedStats.Z(settings.EffectZAlpha, settings.EffectZPower), Is.EqualTo(Z));
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), AiEvalSettings.RelativePath));
            Assert.That(Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(FightTestSupport.SetKey(text, "effect_alpha", "0.6"))).Message, Does.Contain("effect_alpha"));
            Assert.That(Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(FightTestSupport.SetKey(text, "effect_alpha", "0.12345"))).Message, Does.Contain("at most 4 decimals"));
            Assert.That(Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(FightTestSupport.SetKey(text, "paired_min_complete_fraction", "1.5"))).Message, Does.Contain("paired_min_complete_fraction"));
            Assert.That(Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(text.Replace("\"effect_z_power\"", "\"effect_z_powr\""))).Message, Does.Contain("effect_z_powr"));
        }

        /// <summary>Runs a ladder through the harness entry point into a fresh folder; returns the exit code, stdout and the folder.</summary>
        private static int Ladder(out string stdout, out string folder, params string[] args)
        {
            folder = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            List<string> all = new List<string> { "--data", TestData.FindDataDirectory(), "--ladder", "--out", folder };
            all.AddRange(args);
            return HarnessTestSupport.Run(all.ToArray(), out stdout, out string error);
        }

        /// <summary>P-9: a small unpaired ladder writes lines without pair keys, the LADDER line and exit code as before, and an EFFECT line just before it (also repeated in ladder.md); --paired with --non-regression, or --paired without profiles, is an error.</summary>
        [Test]
        public void UnpairedLadder_IsUnchanged_AndStatesItsEffect()
        {
            int exit = Ladder(out string stdout, out string folder, "--profile-a", "abl_s5_no_h", "--profile-b", "aggressive_s5", "--games", "4", "--matchups", "water_vs_water", "--max-game-seconds", "60", "--jobs", "2");
            string[] lines = stdout.TrimEnd('\n').Split('\n');
            Assert.That(lines[lines.Length - 1], Does.StartWith("LADDER candidate=aggressive_s5 baseline=abl_s5_no_h decided=0 wins=0 fraction=0.000 distinct=0 mirror=0/0 cross=0/0 bar=0.650 FAIL"));
            Assert.That(lines[lines.Length - 2], Does.StartWith("EFFECT candidate=aggressive_s5 baseline=abl_s5_no_h games=0 wins=0 p=1.0000 advantage=+0.0 interval=[-50.0,+50.0] mde=- verdict=NO_MEASURABLE_EFFECT"));
            Assert.That(exit, Is.EqualTo(2), "the exit code stays bar-based");
            string jsonl = File.ReadAllText(Path.Combine(folder, "ladder.jsonl"));
            Assert.That(jsonl, Does.Not.Contain("\"pair\"").And.Not.Contain("candidate_slot"));
            Assert.That(File.ReadAllText(Path.Combine(folder, "ladder.md")), Does.Contain("**EFFECT candidate=").And.Not.Contain("## Pairs"));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--ladder", "--paired", "--non-regression", "--profile-a", "a", "--profile-b", "b" }, out string o1, out string e1), Is.EqualTo(1));
            Assert.That(e1, Does.Contain("--paired and --non-regression"));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--paired", "--batch" }, out string o2, out string e2), Is.EqualTo(1));
            Assert.That(e2, Does.Contain("--paired needs"));
        }

        /// <summary>
        /// P-4: self-pairing (aggressive_s5 against itself, water_vs_water, the full 2400 s cap so the games are decided) gives, within every pair, equal state hashes; every complete pair is concordant; W = L = 0, p 1.0000,
        /// verdict NO_MEASURABLE_EFFECT and exit 4. Four games (two pairs) in four worker processes instead of the spec's eight, because a decided game costs about two minutes of one core.
        /// </summary>
        [Test]
        public void SelfPairing_GamesInAPairAreIdentical()
        {
            // One game per worker chunk, so the four games run at once.
            string folder = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch);
            string outDirectory = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            int exit = HarnessTestSupport.Run(new[] { "--data", folder, "--ladder", "--out", outDirectory, "--paired", "--profile-a", "aggressive_s5", "--profile-b", "aggressive_s5", "--games", "4",
                "--matchups", "water_vs_water", "--jobs", "4" }, out string stdout, out string error);
            List<GameRecord> records = new List<GameRecord>();
            foreach (string line in File.ReadAllLines(Path.Combine(outDirectory, "ladder.jsonl")))
            {
                records.Add(GameRecord.Parse(line));
            }
            Assert.That(records.Count, Is.EqualTo(4), error);
            for (int p = 0; p < 2; p++)
            {
                Assert.That(records[p * 2 + 1].StateHash, Is.EqualTo(records[p * 2].StateHash), "pair " + p);
                Assert.That(records[p * 2 + 1].Result, Is.EqualTo(records[p * 2].Result));
            }
            List<PairOutcome> outcomes = PairedLadder.Outcomes(records);
            Assert.That(outcomes.TrueForAll(o => o.Complete), Is.True, "the games are decided within the cap");
            Assert.That(outcomes.TrueForAll(o => o.Class == PairOutcome.Concordant), Is.True);
            string last = stdout.TrimEnd('\n').Substring(stdout.TrimEnd('\n').LastIndexOf('\n') + 1);
            Assert.That(last, Does.StartWith("PAIRED candidate=aggressive_s5 baseline=aggressive_s5 pairs=2 complete=2 "));
            Assert.That(last, Does.Contain(" candidate_both=0 baseline_both=0 p=1.0000 advantage=+0.0 "));
            Assert.That(last, Does.EndWith(" pairing=water:full verdict=NO_MEASURABLE_EFFECT"));
            Assert.That(exit, Is.EqualTo(4));
        }

        /// <summary>P-10: a paired run of 8 games (abl_s5_no_h against aggressive_s5) with --jobs 1 and --jobs 4 gives identical ladder.jsonl and ladder.md, the spot check has 0 mismatches, and --trace --paired --game 3 reproduces record 3's line.</summary>
        [Test]
        public void PairedLadder_IsDeterministic_AndTraceReproduces()
        {
            string[] args = { "--paired", "--profile-a", "abl_s5_no_h", "--profile-b", "aggressive_s5", "--games", "8", "--matchups", "water_vs_water", "--max-game-seconds", "60" };
            List<string> one = new List<string>(args) { "--jobs", "1" };
            List<string> four = new List<string>(args) { "--jobs", "4" };
            int exit1 = Ladder(out string stdout1, out string folder1, one.ToArray());
            int exit4 = Ladder(out string stdout4, out string folder4, four.ToArray());
            Assert.That(exit4, Is.EqualTo(exit1));
            Assert.That(stdout4, Is.EqualTo(stdout1));
            Assert.That(File.ReadAllText(Path.Combine(folder4, "ladder.jsonl")), Is.EqualTo(File.ReadAllText(Path.Combine(folder1, "ladder.jsonl"))));
            string markdown = File.ReadAllText(Path.Combine(folder1, "ladder.md"));
            Assert.That(File.ReadAllText(Path.Combine(folder4, "ladder.md")), Is.EqualTo(markdown));
            Assert.That(markdown, Does.Contain("2 games re-run in-process, 0 mismatches"));
            Assert.That(markdown, Does.Contain("## Pairs"));
            string[] lines = File.ReadAllLines(Path.Combine(folder1, "ladder.jsonl"));
            Assert.That(lines.Length, Is.EqualTo(8));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--trace", "--paired", "--map", "prototype_1v1", "--matchup", "water_vs_water", "--seed", "20261117",
                "--game", "3", "--profile-a", "abl_s5_no_h", "--profile-b", "aggressive_s5", "--max-game-seconds", "60" }, out string traced, out string error), Is.EqualTo(0), error);
            string[] traceLines = traced.TrimEnd('\n').Split('\n');
            Assert.That(traceLines[traceLines.Length - 1], Is.EqualTo(lines[3]));
        }

        /// <summary>The check <paramref name="id"/> configured from an entry written as JSON text, with the bank times 300, 600 and 900 and 20 ticks per second.</summary>
        private static IBehaviourCheck HCheck(string id, string entry)
        {
            IBehaviourCheck check = BehaviourChecks.Create(id, new BehaviourContext(10000, 600, new[] { 300, 600, 900 }, 20));
            check.ReadSettings(JsonReader.Parse(entry, "test"));
            return check;
        }

        /// <summary>A synthetic summary that carries only the B0 fields.</summary>
        private static GameSummary HSummary(int?[] workers, int[] dropOffTicks)
        {
            return new GameSummary(0, 0, new SortedDictionary<string, int[]>(), new List<int[]?>(), new List<int[]>(), new List<int[]>(), new SortedDictionary<string, int>(), new SortedDictionary<string, int>(),
                new SortedDictionary<string, int>(), workers, dropOffTicks);
        }

        /// <summary>A throwaway game record for checks that read only the summary.</summary>
        private static GameRecord HGame()
        {
            return new GameRecord("prototype_1v1", "water_vs_water", 0, 1UL, "water", "water", "a", "b", GameRecord.Undecided, 1, 1UL, 1UL, 1UL, new List<HqKill>());
        }

        /// <summary>H-1: one 240 s game has one workers entry per bank time (null after the end), drop_off_ticks ascending and as many as the completed non-HQ drop-offs; the observer is still read-only; the line round-trips and a record without the fields parses with both empty.</summary>
        [Test]
        public void Summary_WorkersAndDropOffs()
        {
            DataSet data = TestData.LoadAll();
            ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", "skai_vs_water", 0);
            GameRecord plain = GameRunner.Play(data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 240);
            GameRecord watched = GameRunner.Play(data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 240, null, -1, -1, new SummaryObserver(30, new[] { 60, 200, 900 }));
            Assert.That(watched.StateHash, Is.EqualTo(plain.StateHash));
            Assert.That(watched.PlaySignature, Is.EqualTo(plain.PlaySignature));
            // Drop-offs seen in some slot, so the ascending check cannot pass on empty lists.
            int seen = 0;
            foreach (GameSummary summary in new[] { watched.Summary0!, watched.Summary1! })
            {
                Assert.That(summary.Workers.Count, Is.EqualTo(3));
                Assert.That(summary.Workers[0], Is.GreaterThan(0));
                Assert.That(summary.Workers[1], Is.GreaterThan(0));
                Assert.That(summary.Workers[2], Is.Null);
                // Completed non-HQ drop-offs by definition, from the same events.
                int completed = 0;
                foreach (KeyValuePair<string, int> pair in summary.Completed)
                {
                    if (data.Content.Units[pair.Key].Economy.IsDropOff && data.Content.Units[pair.Key].Hq == null)
                    {
                        completed += pair.Value;
                    }
                }
                Assert.That(summary.DropOffTicks.Count, Is.EqualTo(completed));
                for (int i = 0; i < summary.DropOffTicks.Count; i++)
                {
                    Assert.That(summary.DropOffTicks[i], Is.InRange(0, 240 * 20));
                    Assert.That(i == 0 || summary.DropOffTicks[i] >= summary.DropOffTicks[i - 1], Is.True);
                }
                seen += summary.DropOffTicks.Count;
            }
            Assert.That(seen, Is.GreaterThan(0), "some depot was completed");
            string line = watched.ToJsonLine();
            Assert.That(GameRecord.Parse(line).ToJsonLine(), Is.EqualTo(line));
            // An old record: strip the two fields.
            string old = System.Text.RegularExpressions.Regex.Replace(line, ",\"workers\":\\[[^\\]]*\\],\"drop_off_ticks\":\\[[^\\]]*\\]", "");
            Assert.That(old, Does.Not.Contain("drop_off_ticks"));
            GameRecord parsed = GameRecord.Parse(old);
            Assert.That(parsed.Summary0!.Workers, Is.Empty);
            Assert.That(parsed.Summary0.DropOffTicks, Is.Empty);
        }

        /// <summary>H-2: workers_at at 300 s with min 10: 10 holds, 9 violates, null (game ended early) and an old record are unjudged; an at_seconds outside the bank times fails naming the field.</summary>
        [Test]
        public void WorkersAt_EdgeAndUnjudged()
        {
            IBehaviourCheck check = HCheck("workers_at", "{\"enabled\": true, \"at_seconds\": 300, \"min_workers\": 10, \"max_violation_fraction\": 0}");
            Assert.That(check.Evaluate(HSummary(new int?[] { 10, 12, 12 }, new int[0]), HGame()).Outcome, Is.EqualTo(CheckOutcome.Held));
            BehaviourResult below = check.Evaluate(HSummary(new int?[] { 9, 12, 12 }, new int[0]), HGame());
            Assert.That(below.Outcome, Is.EqualTo(CheckOutcome.Violated));
            Assert.That(below.Num, Is.EqualTo(9));
            Assert.That(check.Evaluate(HSummary(new int?[] { null, null, null }, new int[0]), HGame()).Outcome, Is.EqualTo(CheckOutcome.Unjudged));
            Assert.That(check.Evaluate(HSummary(new int?[0], new int[0]), HGame()).Outcome, Is.EqualTo(CheckOutcome.Unjudged));
            // The second bank time is read for at_seconds 600.
            IBehaviourCheck second = HCheck("workers_at", "{\"enabled\": true, \"at_seconds\": 600, \"min_workers\": 0, \"max_violation_fraction\": 0}");
            Assert.That(second.Evaluate(HSummary(new int?[] { 1, 7, 9 }, new int[0]), HGame()).Num, Is.EqualTo(7));
            DataValidationException bad = Expect.Throws<DataValidationException>(() => HCheck("workers_at", "{\"enabled\": true, \"at_seconds\": 301, \"min_workers\": 0, \"max_violation_fraction\": 0}"));
            Assert.That(bad.Message, Does.Contain("at_seconds"));
        }

        /// <summary>H-3: first_drop_off with max 60 s at 20 ticks per second: tick 1219 is 60 s (integer division) and holds, 1220 is 61 s and violates, no drop-off is unjudged; the first entry is the one read.</summary>
        [Test]
        public void FirstDropOff_IntegerSecondsEdgeAndNone()
        {
            IBehaviourCheck check = HCheck("first_drop_off", "{\"enabled\": true, \"max_seconds\": 60, \"max_violation_fraction\": 0}");
            BehaviourResult held = check.Evaluate(HSummary(new int?[0], new[] { 1219, 5000 }), HGame());
            Assert.That(held.Outcome, Is.EqualTo(CheckOutcome.Held));
            Assert.That(held.Num, Is.EqualTo(60));
            Assert.That(check.Evaluate(HSummary(new int?[0], new[] { 1220 }), HGame()).Outcome, Is.EqualTo(CheckOutcome.Violated));
            Assert.That(check.Evaluate(HSummary(new int?[0], new int[0]), HGame()).Outcome, Is.EqualTo(CheckOutcome.Unjudged));
        }
    }
}

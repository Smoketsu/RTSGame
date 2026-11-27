// rts/tests/Sim.Tests/Harness/ResponseGuardTests.cs
// Tests RG-3 (1 to 10) of rts/docs/specs/response_suite_guard.md: the optional match seed of ResponseRunner.Run, the derived per-game seeds, --jobs independence of the seeded --out file,
// the guard verdict on synthetic records (holds, RG-a, RG-b, spread discordance), record mismatches (exit 1) and the four new ai_eval.json keys. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Response guard tests.</summary>
    [TestFixture]
    public class ResponseGuardTests
    {
        /// <summary>The shipped data set.</summary>
        private static readonly DataSet Data = TestData.LoadAll();

        /// <summary>Games per scenario of the synthetic runs. Unit: games. Range: 8.</summary>
        private const int N = 8;

        /// <summary>Removes the temporary folders.</summary>
        [OneTimeTearDown]
        public void Cleanup()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>The scenario entry of a script file name.</summary>
        private static ResponseTest Entry(string script)
        {
            return AiEvalSettings.Load(TestData.FindDataDirectory()).ResponseTests.First(t => t.Script == script);
        }

        /// <summary>Test 2: the script's own seed (1234) passed explicitly equals the null-seed run.</summary>
        [Test]
        public void Run_ExplicitScriptSeedEqualsNullSeed()
        {
            ResponseTest test = Entry("response_turtle_water.json");
            string folder = TestData.FindDataDirectory();
            ResponseResult none = ResponseRunner.Run(Data, folder, test, "aggressive_s5", true);
            ResponseResult explicitSeed = ResponseRunner.Run(Data, folder, test, "aggressive_s5", true, 1234UL);
            Assert.That(explicitSeed.FinalHash, Is.EqualTo(none.FinalHash));
            Assert.That(explicitSeed.Line(), Is.EqualTo(none.Line()));
            Assert.That(none.ContentDigest, Is.Not.EqualTo(0UL));
        }

        /// <summary>Test 1: a null seed passed explicitly is today's run of the call without the seed (same final hash and line).</summary>
        [Test]
        public void Run_NullSeedEqualsTodaysRun()
        {
            ResponseTest test = Entry("response_turtle_water.json");
            string folder = TestData.FindDataDirectory();
            ResponseResult none = ResponseRunner.Run(Data, folder, test, "aggressive_s5", true, null);
            ResponseResult today = ResponseRunner.Run(Data, folder, test, "aggressive_s5", true);
            Assert.That(none.FinalHash, Is.EqualTo(today.FinalHash));
            Assert.That(none.Line(), Is.EqualTo(today.Line()));
        }

        /// <summary>Test 3: the seeds of games 0 to 7 are distinct, differ between scripts and equal SeedDerivation.GameSeed.</summary>
        [Test]
        public void DerivedSeeds_AreDistinctAndMatchSeedDerivation()
        {
            string folder = TestData.FindDataDirectory();
            ResponseTest turtle = Entry("response_turtle_water.json");
            ResponseTest roc = Entry("response_roc_rush_skai.json");
            // Map ID of the turtle script's map as the runner resolves it.
            string map = Data.RequireMap(CommandScript.Parse(File.ReadAllText(Path.Combine(folder, "scripts/ai_eval", turtle.Script)), "x", Rts.Sim.Factions.BuiltInMechanics.ScriptParsers).MapId).Id;
            List<ulong> seeds = new List<ulong>();
            for (int g = 0; g < 8; g++)
            {
                ulong seed = ResponseSeeded.DerivedSeed(Data, folder, turtle, 20261128UL, g);
                seeds.Add(seed);
                Assert.That(seed, Is.EqualTo(SeedDerivation.GameSeed(20261128UL, map, "response_turtle_water", g)));
                Assert.That(seed, Is.Not.EqualTo(ResponseSeeded.DerivedSeed(Data, folder, roc, 20261128UL, g)), "another script gets another seed");
            }
            Assert.That(seeds.Distinct().Count(), Is.EqualTo(8));
        }

        /// <summary>Test 4: --out is byte-identical for --jobs 1 and --jobs 2.</summary>
        [Test]
        public void SeededRun_OutFileDoesNotDependOnJobs()
        {
            string[] files = new string[2];
            string[] outputs = new string[2];
            for (int run = 0; run < 2; run++)
            {
                string file = Path.Combine(HarnessTestSupport.NewFolder(), "r.jsonl");
                int exit = HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--response", "--profile", "aggressive_s5", "--only", "response_roc_rush_skai.json",
                    "--seeds", "2", "--jobs", run == 0 ? "1" : "2", "--out", file }, out outputs[run], out string error);
                Assert.That(exit, Is.EqualTo(0).Or.EqualTo(2), error);
                files[run] = File.ReadAllText(file);
            }
            Assert.That(files[1], Is.EqualTo(files[0]));
            Assert.That(outputs[1], Is.EqualTo(outputs[0]));
            Assert.That(files[0].Split('\n', System.StringSplitOptions.RemoveEmptyEntries).Length, Is.EqualTo(2));
            Assert.That(outputs[0], Does.Contain("RESPONSE_RATE response_roc_rush_skai.json passed="));
            Assert.That(outputs[0], Does.Contain(" seed=1\n"));
        }

        /// <summary>Writes a synthetic run file: per scenario, game g passes as the list says; the seed of game g is the prefix followed by g.</summary>
        private static string WriteRun(Dictionary<string, bool[]> passed, string seedPrefix = "s")
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, bool[]> scenario in passed)
            {
                for (int g = 0; g < scenario.Value.Length; g++)
                {
                    sb.Append("{\"script\":\"").Append(scenario.Key).Append("\",\"check\":\"trade\",\"game\":").Append(g).Append(",\"seed\":\"").Append(seedPrefix).Append(g)
                        .Append("\",\"profile\":\"p\",\"content_digest\":\"0000000000000000\",\"passed\":").Append(scenario.Value[g] ? "true" : "false")
                        .Append(",\"parts\":[],\"final_hash\":\"0000000000000000\"}\n");
                }
            }
            string file = Path.Combine(HarnessTestSupport.NewFolder(), "r.jsonl");
            File.WriteAllText(file, sb.ToString());
            return file;
        }

        /// <summary>Ten synthetic scenario names.</summary>
        private static string[] Scripts()
        {
            return Enumerable.Range(0, 10).Select(i => "response_s" + i + ".json").ToArray();
        }

        /// <summary>A baseline that passes everything and a candidate with <paramref name="b"/>[s] baseline-only passes and <paramref name="c"/>[s] candidate-only passes in scenario s.</summary>
        private static int RunGuard(int[] b, int[] c, out string output, out string error, string seedPrefixB = "s", int dropGame = -1)
        {
            Dictionary<string, bool[]> baseline = new Dictionary<string, bool[]>();
            Dictionary<string, bool[]> candidate = new Dictionary<string, bool[]>();
            string[] scripts = Scripts();
            for (int s = 0; s < scripts.Length; s++)
            {
                bool[] a = new bool[N];
                bool[] d = new bool[N];
                for (int g = 0; g < N; g++)
                {
                    // Games 0..b-1 discordant towards the baseline, then c towards the candidate, else both pass.
                    bool towardsBaseline = g < b[s];
                    bool towardsCandidate = !towardsBaseline && g < b[s] + c[s];
                    a[g] = !towardsCandidate;
                    d[g] = !towardsBaseline;
                }
                baseline[scripts[s]] = a;
                candidate[scripts[s]] = dropGame >= 0 && s == 0 ? a.Take(dropGame).ToArray() : d;
            }
            string fileA = WriteRun(baseline);
            string fileB = WriteRun(candidate, seedPrefixB);
            return HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--response-guard", "--from-a", fileA, "--from-b", fileB }, out output, out error);
        }

        /// <summary>Test 5: identical files hold with every B and C zero.</summary>
        [Test]
        public void Guard_IdenticalFilesHold()
        {
            int exit = RunGuard(new int[10], new int[10], out string output, out string error);
            Assert.That(exit, Is.EqualTo(0), error);
            Assert.That(output, Does.Contain("RESPONSE_GUARD HOLDS\n"));
            Assert.That(output.Split('\n').Count(l => l.StartsWith("RESPONSE_GUARD_SCENARIO") && l.Contains(" B=0 C=0 ")), Is.EqualTo(10));
        }

        /// <summary>Test 6: B = 7, C = 0 in one scenario fails with RG-a for that scenario.</summary>
        [Test]
        public void Guard_OneScenarioCollapsesFailsRgA()
        {
            int[] b = new int[10];
            b[3] = 7;
            int exit = RunGuard(b, new int[10], out string output, out string error);
            Assert.That(exit, Is.EqualTo(2), error);
            Assert.That(output, Does.Contain("RESPONSE_GUARD FAILS RG-a:response_s3.json"));
        }

        /// <summary>Test 7: B = 6, C = 0 in one scenario: RG-a holds (p = 1/64) and RG-b fails, so the reason is RG-b only.</summary>
        [Test]
        public void Guard_SixToZeroFailsRgBOnly()
        {
            int[] b = new int[10];
            b[0] = 6;
            int exit = RunGuard(b, new int[10], out string output, out string error);
            Assert.That(exit, Is.EqualTo(2), error);
            Assert.That(output, Does.Contain("RESPONSE_GUARD FAILS RG-b\n"));
            Assert.That(output, Does.Not.Contain("RG-a"));
        }

        /// <summary>Test 8: sum B = 10 against sum C = 6 spread over scenarios (none above 3) holds.</summary>
        [Test]
        public void Guard_SpreadDiscordanceHolds()
        {
            int[] b = { 2, 2, 2, 2, 2, 0, 0, 0, 0, 0 };
            int[] c = { 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 };
            int exit = RunGuard(b, c, out string output, out string error);
            Assert.That(exit, Is.EqualTo(0), error);
            Assert.That(output, Does.Contain("RESPONSE_GUARD_SUITE B=10 C=6 p_pt=2272"));
            Assert.That(output, Does.Contain("RESPONSE_GUARD HOLDS\n"));
        }

        /// <summary>Test 9: another seed, or a missing record, is an error (exit 1) naming the record.</summary>
        [Test]
        public void Guard_MismatchedRecordsExitWithOne()
        {
            int exit = RunGuard(new int[10], new int[10], out string output, out string error, "other");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error, Does.Contain("seed mismatch for record response_s0.json game 0"));
            exit = RunGuard(new int[10], new int[10], out output, out error, "s", 5);
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error, Does.Contain("missing record response_s0.json game 5"));
        }

        /// <summary>Test 10: the four keys load with the spec's starting values, and values out of range fail validation.</summary>
        [Test]
        public void AiEvalKeys_LoadAndValidate()
        {
            AiEvalSettings settings = AiEvalSettings.Load(TestData.FindDataDirectory());
            Assert.That(settings.ResponseGuardBaseSeed, Is.EqualTo(20261128UL));
            Assert.That(settings.ResponseGuardSeeds, Is.EqualTo(8));
            Assert.That(settings.ResponseGuardScenarioAlphaPt, Is.EqualTo(100));
            Assert.That(settings.ResponseGuardSuiteAlphaPt, Is.EqualTo(500));
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), AiEvalSettings.RelativePath));
            Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(text.Replace("\"response_guard_seeds\": 8", "\"response_guard_seeds\": 65")));
            Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(text.Replace("\"response_guard_scenario_alpha\": 0.01", "\"response_guard_scenario_alpha\": 0.9")));
            Expect.Throws<DataValidationException>(() => AiEvalSettings.Parse(text.Replace("\"response_guard_base_seed\": 20261128", "\"response_guard_base_seed\": 2147483648")));
        }
    }
}

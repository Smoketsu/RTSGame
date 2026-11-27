// rts/tests/Sim.Tests/Harness/PredictKiteTests.cs
// Acceptance tests of package K-H (rts/docs/specs/ai_kite_correction.md section 4.3): the harness flag --predict-kite-factor of --predict-fights: output unchanged without it,
// the kite_pair field, the errors, the PREDICT line suffix and the predict.md table. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Tests of --predict-kite-factor.</summary>
    [TestFixture]
    public class PredictKiteTests
    {
        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>A synthetic record of one unit type per seat, case "flat", value 750, seat 0 the winner.</summary>
        private static FightRecord Record(string id, string unit0, string unit1)
        {
            return new FightRecord(id, "flat", 750, "a", "b", new SortedDictionary<string, int>(new Dictionary<string, int> { [unit0] = 4 }, StringComparer.Ordinal),
                new SortedDictionary<string, int>(new Dictionary<string, int> { [unit1] = 4 }, StringComparer.Ordinal), 800, 800, false, FightRecord.Seat0Won, "eliminated", 100, "5.0", 0.5, 0.0, 1, 0, -1, 1UL, 2UL);
        }

        /// <summary>Writes the two hand-made records to a file and returns its path: Pummeler against Windrunner, and Windrunner against Reef Runner.</summary>
        private static string Records()
        {
            // Path of the records file in a new folder. Unit: none (text). Range: a path ending in kite.jsonl.
            string path = Path.Combine(HarnessTestSupport.NewFolder(), "kite.jsonl");
            File.WriteAllText(path, Record("flat/750/p/w", "water.grasper", "skai.windrunner").ToJsonLine() + "\n" + Record("flat/750/w/r", "skai.windrunner", "water.reef_runner").ToJsonLine() + "\n");
            return path;
        }

        /// <summary>Runs --predict-fights on <paramref name="records"/> with extra arguments; returns the exit code, stdout, stderr and the output folder.</summary>
        private static int Run(string records, string[] extra, out string stdout, out string stderr, out string folder)
        {
            folder = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            // Command-line arguments: the data folder, the command, the records and the output folder, then extra. Unit: none (text). Range: one text per argument.
            List<string> args = new List<string> { "--data", TestData.FindDataDirectory(), "--predict-fights", "--from", records, "--out", folder };
            args.AddRange(extra);
            return HarnessTestSupport.Run(args.ToArray(), out stdout, out stderr);
        }

        /// <summary>K-H 1: without the flag the output has no kite field and no suffix.</summary>
        [Test]
        public void KH1_WithoutFlagOutputIsUnchanged()
        {
            // Run without the flag: the standard output (text), the standard error (text) and the output folder (path). Unit: none. Range: any text or folder path.
            Assert.That(Run(Records(), new string[0], out string stdout, out string error, out string folder), Is.EqualTo(0), error);
            Assert.That(File.ReadAllText(Path.Combine(folder, "predict.jsonl")), Does.Not.Contain("kite_pair"));
            Assert.That(stdout, Does.Not.Contain("kite_factor"));
            Assert.That(File.ReadAllText(Path.Combine(folder, "predict.md")), Does.Not.Contain("kite pair"));
        }

        /// <summary>K-H 2 and 4: with 0.5 the Pummeler against Windrunner line has kite_pair true and other times; the Windrunner against Reef Runner line differs from the off line only by the field; the PREDICT line ends with the factor and the count.</summary>
        [Test]
        public void KH2_KitePairFieldAndSuffix()
        {
            // Path of the two-record fights file. Unit: none (text). Range: a path from Records().
            string records = Records();
            // Run without the flag: its standard error (text) and output folder (path). Unit: none. Range: any text or folder path.
            Assert.That(Run(records, new string[0], out string _, out string error0, out string offFolder), Is.EqualTo(0), error0);
            // Run with --predict-kite-factor 0.5: standard output (text), standard error (text) and output folder (path). Unit: none. Range: any text or folder path.
            Assert.That(Run(records, new[] { "--predict-kite-factor", "0.5" }, out string stdout, out string error, out string onFolder), Is.EqualTo(0), error);
            // Lines of predict.jsonl without the flag, one per fight. Unit: none (text lines). Range: 2 lines.
            string[] off = File.ReadAllLines(Path.Combine(offFolder, "predict.jsonl"));
            // Lines of predict.jsonl with the flag, one per fight. Unit: none (text lines). Range: 2 lines.
            string[] on = File.ReadAllLines(Path.Combine(onFolder, "predict.jsonl"));
            Assert.That(on.Length, Is.EqualTo(2));
            Assert.That(on[0], Does.EndWith(",\"kite_pair\":true}"));
            Assert.That(on[0].Replace(",\"kite_pair\":true}", "}"), Is.Not.EqualTo(off[0]), "times differ");
            Assert.That(on[1], Does.EndWith(",\"kite_pair\":false}"));
            Assert.That(on[1].Replace(",\"kite_pair\":false}", "}"), Is.EqualTo(off[1]));
            // The last stdout line, the PREDICT line. Unit: none (text). Range: a non-empty line ending the output.
            string last = stdout.TrimEnd('\n').Substring(stdout.TrimEnd('\n').LastIndexOf('\n') + 1);
            Assert.That(last, Does.EndWith(" kite_factor=0.5 kite_pair_fights=1"));
            Assert.That(File.ReadAllText(Path.Combine(onFolder, "predict.md")), Does.Contain("Accuracy by kite pair"));
        }

        /// <summary>K-H 3: the flag without --predict-fights, and the values 1.5 and -0.1, are errors naming the flag.</summary>
        [Test]
        public void KH3_Errors()
        {
            // New empty output folder for the refused run. Unit: none (path). Range: a folder that did not exist before.
            string folder = HarnessTestSupport.NewFolder();
            // Refused run (--ladder with the flag, no --predict-fights): standard error (text) is checked. Unit: none. Range: any text.
            Assert.That(HarnessTestSupport.Run(new[] { "--data", TestData.FindDataDirectory(), "--ladder", "--profile-a", "aggressive_s5", "--profile-b", "aggressive_s5_kite", "--predict-kite-factor", "0.5", "--out", folder }, out string _, out string error), Is.EqualTo(1));
            Assert.That(error, Does.Contain("--predict-kite-factor"));
            // A factor value that must be refused: 1.5 (above 1) or -0.1 (below 0). Unit: fraction. Range: 1.5 or -0.1 (both outside 0 to 1).
            foreach (string bad in new[] { "1.5", "-0.1" })
            {
                // Refused run with the bad value: standard output (text) and standard error (text). Unit: none. Range: any text.
                Assert.That(Run(Records(), new[] { "--predict-kite-factor", bad }, out string _, out string badError, out string _), Is.EqualTo(1), bad);
                Assert.That(badError, Does.Contain("--predict-kite-factor"), bad);
            }
        }
    }
}

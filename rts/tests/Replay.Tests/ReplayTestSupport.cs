// rts/tests/Replay.Tests/ReplayTestSupport.cs
// Shared helpers of the replay tests: finding and reading the real rts/data folder, temporary folders, building a started simulation
// from a script, and the Expect wrappers for exceptions. Part of Rts.Replay.Tests. This is test code, so it may use System.IO.
// Expect exists because Assert.Throws is ambiguous under NUnit 4.6 with C# 9 lambdas; these wrappers take TestDelegate only, so
// the same source builds with NUnit 4.2.2 (CI) and 4.6 (local). A copy of the helper in rts/tests/Sim.Tests/TestWorld.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Rts.Replay;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;

namespace Rts.Replay.Tests
{
    /// <summary>Exception assertions that compile on NUnit 4.2.2 and 4.6.</summary>
    public static class Expect
    {
#pragma warning disable 618 // TestDelegate is marked obsolete from NUnit 4.6; it is still the one unambiguous overload.
        /// <summary>Asserts that <paramref name="code"/> throws exactly <typeparamref name="T"/> and returns it.</summary>
        public static T Throws<T>(NUnit.Framework.TestDelegate code) where T : Exception
        {
            return NUnit.Framework.Assert.Throws<T>(code)!;
        }
#pragma warning restore 618
    }

    /// <summary>Helpers that locate the data folder and build simulations for the tests.</summary>
    public static class ReplayTestSupport
    {
        /// <summary>A file every valid data folder contains, used to recognise the folder while walking up directories.</summary>
        private const string MarkerFile = "tuning/match.json";

        /// <summary>
        /// Absolute path of rts/data. Walks up from the test assembly's folder and the current directory, preferring the source tree
        /// (a data folder next to rts/src/Sim) and falling back to the copy the test project places beside the binary.
        /// </summary>
        public static string FindDataDirectory()
        {
            // Directories to start walking from: assembly folder first, then the working directory.
            List<string> starts = new List<string>
            {
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
            };
            // Pass 0 wants the source tree; pass 1 takes any copy.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (string start in starts)
                {
                    // Directory being examined, moving up one level per iteration.
                    string? dir = string.IsNullOrEmpty(start) ? null : Path.GetFullPath(start);
                    while (dir != null)
                    {
                        foreach (string candidate in new[] { Path.Combine(dir, "rts", "data"), Path.Combine(dir, "data") })
                        {
                            if (!File.Exists(Path.Combine(candidate, MarkerFile)))
                            {
                                continue;
                            }
                            // Folder holding the candidate, to look for the simulation source beside it.
                            string? parent = Path.GetDirectoryName(candidate);
                            // True when rts/src/Sim sits beside the data folder.
                            bool isSourceTree = parent != null && File.Exists(Path.Combine(parent, "src", "Sim", "Rts.Sim.csproj"));
                            if (pass == 1 || isSourceTree)
                            {
                                return candidate;
                            }
                        }
                        dir = Path.GetDirectoryName(dir);
                    }
                }
            }
            throw new DirectoryNotFoundException("Could not find rts/data by walking up from the test assembly or the current directory.");
        }

        /// <summary>Creates a new empty folder under the system temp folder and returns its path. The caller deletes it.</summary>
        public static string NewTempDirectory()
        {
            // Unique folder path. Range: a new, empty directory.
            string path = Path.Combine(Path.GetTempPath(), "rts_replay_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>Copies every file under <paramref name="from"/> to <paramref name="to"/>, keeping the folder layout.</summary>
        /// <param name="from">Source folder.</param>
        /// <param name="to">Destination folder; created if missing.</param>
        public static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            }
            foreach (string sub in Directory.GetDirectories(from))
            {
                CopyDirectory(sub, Path.Combine(to, Path.GetFileName(sub)));
            }
        }

        /// <summary>Reads every *.json file under the data folder with data-root-relative forward-slash paths, in ordinal path order.</summary>
        public static List<DataSourceFile> ReadData()
        {
            // Absolute data folder.
            string root = FindDataDirectory();
            // Absolute file paths, sorted so the list is the same on every machine.
            string[] paths = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            List<DataSourceFile> files = new List<DataSourceFile>();
            foreach (string path in paths)
            {
                files.Add(new DataSourceFile(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)));
            }
            return files;
        }

        /// <summary>Loads the whole real data folder.</summary>
        public static DataSet LoadData()
        {
            return DataLoader.Load(ReadData());
        }

        /// <summary>Parses a script from rts/data/scripts.</summary>
        /// <param name="name">File name without folder, for example "smoke.json".</param>
        public static CommandScript LoadScript(string name)
        {
            // Absolute script path.
            string path = Path.Combine(FindDataDirectory(), "scripts", name);
            return CommandScript.Parse(File.ReadAllText(path), "scripts/" + name, BuiltInMechanics.ScriptParsers);
        }

        /// <summary>Builds and starts the simulation of a script, without submitting its commands.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="script">The script giving seed, map and players.</param>
        public static Simulation StartSimulation(DataSet data, CommandScript script)
        {
            // Match inputs from the data set and the script.
            SimulationConfig config = new SimulationConfig(data.CreateMatchSettings(script.Seed), data.Content, script.Players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap(script.MapId));
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>
        /// Plays a script through a recorder for <paramref name="ticks"/> ticks, with the script's commands submitted up front,
        /// and returns the finished replay.
        /// </summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="script">The script to play.</param>
        /// <param name="ticks">Ticks to step. Unit: ticks. Range: 0 upward.</param>
        public static ReplayData RecordScript(DataSet data, CommandScript script, int ticks)
        {
            Simulation sim = StartSimulation(data, script);
            ReplayRecorder recorder = new ReplayRecorder(sim, script.Players);
            foreach (Command command in script.Commands)
            {
                recorder.Submit(command);
            }
            // Step counter. Unit: ticks. Range: 0 to ticks - 1.
            for (int i = 0; i < ticks; i++)
            {
                recorder.Step();
            }
            return recorder.Finish();
        }
    }
}

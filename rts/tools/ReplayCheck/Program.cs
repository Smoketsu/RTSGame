// rts/tools/ReplayCheck/Program.cs
// ReplayCheck: loads the data folder, reads a replay file (ADR-009), re-simulates it with Rts.Replay.ReplayVerifier and compares every
// hash checkpoint and the final hash. Prints "REPLAY OK final=<16 hex>" and exits 0, or prints
// "REPLAY DESYNC tick=<n> expected=<16 hex> actual=<16 hex>" and exits 1, or writes "REPLAY REFUSED: <reason>" to stderr and exits 2.
// Exit code 2 covers every replay that must not be played with this build and data (different format version or data digest, unknown
// map), an unreadable or corrupt replay file, a data folder that fails to load, and a bad command line. Part of Rts.Tools.ReplayCheck.
// This tool does the file IO the simulation and replay assemblies may not. Phase 5 grows it into the anti-cheat re-simulator.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Replay;
using Rts.Sim;
using Rts.Sim.Data;

namespace Rts.Tools.ReplayCheck
{
    /// <summary>Entry point and check logic.</summary>
    public static class Program
    {
        /// <summary>Exit code when every hash matched. Range: fixed at 0.</summary>
        public const int ExitOk = 0;

        /// <summary>Exit code when a hash differed (a desync). Range: fixed at 1.</summary>
        public const int ExitDesync = 1;

        /// <summary>Exit code when the replay was refused or could not be checked. Range: fixed at 2.</summary>
        public const int ExitRefused = 2;

        /// <summary>Process entry point.</summary>
        public static int Main(string[] args)
        {
            return Run(args, Console.Out, Console.Error);
        }

        /// <summary>Runs the check with explicit output writers so tests can capture them.</summary>
        /// <param name="args">Command-line arguments after the program name.</param>
        /// <param name="output">Receives the REPLAY OK and REPLAY DESYNC lines.</param>
        /// <param name="error">Receives REPLAY REFUSED messages.</param>
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            try
            {
                ReplayCheckOptions options = ReplayCheckOptions.Parse(args);
                if (!File.Exists(options.ReplayPath))
                {
                    throw new ReplayCheckException("replay file not found: " + options.ReplayPath);
                }
                // The parsed replay; the version and format checks happen here, before any data is loaded.
                ReplayData replay = ReplayReader.Read(File.ReadAllBytes(options.ReplayPath), CommandCodecRegistry.CreateBuiltIn());
                DataSet data = DataLoader.Load(ReadDataFolder(options.DataDirectory));
                Simulation sim = ReplayVerifier.CreateSimulation(replay, data);
                // Result of re-simulating every tick.
                ReplayVerification result = ReplayVerifier.Verify(replay, sim);
                if (result.IsMatch)
                {
                    output.Write("REPLAY OK final=" + result.FinalHash.ToString("x16", CultureInfo.InvariantCulture) + "\n");
                    output.Flush();
                    return ExitOk;
                }
                output.Write("REPLAY DESYNC tick=" + result.DesyncTick.ToString(CultureInfo.InvariantCulture)
                    + " expected=" + result.ExpectedHash.ToString("x16", CultureInfo.InvariantCulture)
                    + " actual=" + result.ActualHash.ToString("x16", CultureInfo.InvariantCulture) + "\n");
                output.Flush();
                return ExitDesync;
            }
            catch (Exception ex) when (ex is ReplayException || ex is ReplayCheckException || ex is DataValidationException || ex is IOException
                || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException)
            {
                error.Write("REPLAY REFUSED: " + ex.Message + "\n");
                error.Flush();
                return ExitRefused;
            }
        }

        /// <summary>Reads every *.json file under the data folder recursively, with data-root-relative forward-slash paths, in ordinal path order (same as the BalanceHarness).</summary>
        /// <param name="directory">The data folder.</param>
        private static List<DataSourceFile> ReadDataFolder(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new ReplayCheckException("data folder not found: " + directory);
            }
            // Absolute file paths, sorted so the load order is the same on every machine.
            string[] paths = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            List<DataSourceFile> files = new List<DataSourceFile>();
            foreach (string path in paths)
            {
                // Path relative to the data root, as the loader expects.
                string relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
                files.Add(new DataSourceFile(relative, File.ReadAllText(path)));
            }
            return files;
        }
    }
}

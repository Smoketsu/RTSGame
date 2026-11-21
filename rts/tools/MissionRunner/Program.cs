// rts/tools/MissionRunner/Program.cs
// Headless mission runner (spec rts/docs/specs/campaign_scripting.md section 5): loads every *.json under --data, reads the mission and its scripted inputs
// from --campaign, plays the mission with MissionPlay and prints the runner log, one "tick=<n> hash=<16 hex>" line per state hash, the final
// "MISSION won|lost|timeout tick=<t>[ reason=<r>]" line (part of the log) and "STATE_HASH=<16 hex>". Exit codes: 0 won, 2 lost or timed out, 1 any error (message on
// stderr naming the file and field). Part of Rts.Tools.MissionRunner. This tool does the file IO the campaign library may not.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Campaign;
using Rts.Sim.Data;
using Rts.Sim.Factions;

namespace Rts.Tools.MissionTool
{
    /// <summary>An error reported to stderr with exit code 1.</summary>
    public sealed class MissionToolException : Exception
    {
        /// <summary>Creates the exception with a user-facing message.</summary>
        public MissionToolException(string message) : base(message)
        {
        }
    }

    /// <summary>Entry point.</summary>
    public static class Program
    {
        /// <summary>Usage text printed with command-line errors.</summary>
        public const string Usage = "usage: Rts.Tools.MissionRunner --data <data folder> --campaign <campaign folder> --mission <id> [--inputs <inputs.json>] [--ticks N]";

        /// <summary>Process entry point.</summary>
        public static int Main(string[] args)
        {
            return Run(args, Console.Out, Console.Error);
        }

        /// <summary>Runs the tool with explicit writers so tests can capture them. Returns the exit code.</summary>
        /// <param name="args">Arguments after the program name.</param>
        /// <param name="output">Receives the log, hash and result lines.</param>
        /// <param name="error">Receives error messages.</param>
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            try
            {
                // Option values; empty when not given.
                string dataDir = string.Empty;
                string campaignDir = string.Empty;
                string missionId = string.Empty;
                string inputsPath = string.Empty;
                // Step cap from --ticks, or null. Unit: ticks. Range: 0 to 1000000.
                int? ticks = null;
                for (int i = 0; i < args.Length; i++)
                {
                    // Flag being read; its value is the next argument.
                    string flag = args[i];
                    if (flag != "--data" && flag != "--campaign" && flag != "--mission" && flag != "--inputs" && flag != "--ticks")
                    {
                        throw new MissionToolException("unknown argument '" + flag + "'. " + Usage);
                    }
                    if (i + 1 >= args.Length)
                    {
                        throw new MissionToolException(flag + " needs a value. " + Usage);
                    }
                    // The flag's value.
                    string value = args[++i];
                    switch (flag)
                    {
                        case "--data": dataDir = value; break;
                        case "--campaign": campaignDir = value; break;
                        case "--mission": missionId = value; break;
                        case "--inputs": inputsPath = value; break;
                        default:
                            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed > 1000000)
                            {
                                throw new MissionToolException("--ticks must be a whole number from 0 to 1000000 (found '" + value + "')");
                            }
                            ticks = parsed;
                            break;
                    }
                }
                if (dataDir.Length == 0 || campaignDir.Length == 0 || missionId.Length == 0)
                {
                    throw new MissionToolException("--data, --campaign and --mission are required. " + Usage);
                }
                DataSet data = DataLoader.Load(ReadDataFolder(dataDir));
                // Mission file to read.
                string missionPath = Path.Combine(campaignDir, "missions", missionId + ".json");
                if (!File.Exists(missionPath))
                {
                    throw new MissionToolException("mission file not found: " + missionPath);
                }
                MissionDefinition mission = MissionLoader.Parse(File.ReadAllText(missionPath), missionPath, data, BuiltInMechanics.ScriptParsers);
                // Inputs file: --inputs, else the campaign folder's default for the mission when it exists, else nobody plays.
                if (inputsPath.Length == 0)
                {
                    // The default location of the mission's inputs.
                    string defaultPath = Path.Combine(campaignDir, "inputs", missionId + ".json");
                    inputsPath = File.Exists(defaultPath) ? defaultPath : string.Empty;
                }
                else if (!File.Exists(inputsPath))
                {
                    throw new MissionToolException("inputs file not found: " + inputsPath);
                }
                MissionInputs? inputs = inputsPath.Length == 0 ? null : MissionInputs.Parse(File.ReadAllText(inputsPath), inputsPath, mission, data);

                MissionResult result = MissionPlay.Play(data, mission, inputs, ticks, output);
                output.Write("STATE_HASH=" + result.Sim.ComputeStateHash().ToString("x16", CultureInfo.InvariantCulture) + "\n");
                output.Flush();
                return result.ExitCode;
            }
            catch (Exception ex) when (ex is MissionToolException || ex is DataValidationException || ex is IOException
                || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException)
            {
                error.Write("error: " + ex.Message + "\n");
                error.Flush();
                return 1;
            }
        }

        /// <summary>Reads every *.json file under the data folder recursively, with data-root-relative forward-slash paths, in ordinal path order (as the balance harness does).</summary>
        /// <param name="directory">The data folder.</param>
        private static List<DataSourceFile> ReadDataFolder(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new MissionToolException("data folder not found: " + directory);
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

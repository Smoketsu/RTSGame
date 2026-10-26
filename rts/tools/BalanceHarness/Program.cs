// rts/tools/BalanceHarness/Program.cs
// Headless runner: loads every *.json under --data, builds a Simulation with the built-in faction mechanics, plays
// a scripted command stream for N ticks without Unity, prints one "tick=<n> hash=<16 hex>" line per state hash and a
// final "STATE_HASH=<16 hex>". The script is parsed by Rts.Sim.Data.CommandScript and defaults to
// <data>/scripts/smoke.json, the same file the Unity SimulationHost plays. Exit code 0 on success, 1 on any error (message on stderr). Part of
// Rts.Tools.BalanceHarness. Phase 0 proves determinism and the data pipeline in CI; Phase 2 grows this into the
// AI-versus-AI balance harness (see README.md). This tool does the file IO the simulation assembly may not.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Entry point and run loop.</summary>
    public static class Program
    {
        /// <summary>Process entry point. Returns 0 on success and 1 on any error.</summary>
        public static int Main(string[] args)
        {
            return Run(args, Console.Out, Console.Error);
        }

        /// <summary>Runs the harness with explicit output writers so tests can capture them.</summary>
        /// <param name="args">Command-line arguments after the program name.</param>
        /// <param name="output">Receives the hash lines.</param>
        /// <param name="error">Receives error messages and warnings.</param>
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            try
            {
                HarnessOptions options = HarnessOptions.Parse(args);
                DataSet data = DataLoader.Load(ReadDataFolder(options.DataDirectory));
                // Script to run: --script, else <data dir>/scripts/smoke.json.
                string scriptPath = options.ResolveScriptPath();
                if (!File.Exists(scriptPath))
                {
                    throw new HarnessException("script file not found: " + scriptPath);
                }
                CommandScript script = CommandScript.Parse(File.ReadAllText(scriptPath), scriptPath.Replace('\\', '/'));
                // Seed and length: the command line wins over the script.
                ulong seed = options.SeedOverride ?? script.Seed;
                int ticks = options.TicksOverride ?? script.Ticks;

                // Map named by the script, or the first map in ordinal order.
                MapDefinition map = data.RequireMap(script.MapId);
                SimulationConfig config = new SimulationConfig(data.CreateMatchSettings(seed), data.Content, script.Players, BuiltInMechanics.CreateRegistry(), data.Tuning, map);
                Simulation sim = new Simulation(config);
                sim.Start();
                foreach (Command command in script.Commands)
                {
                    if (command.Tick >= ticks)
                    {
                        error.Write("warning: " + command + " is scheduled at or after the last tick (" + ticks + ") and will not run\n");
                    }
                    sim.Submit(command);
                }
                for (int i = 0; i < ticks; i++)
                {
                    sim.Step();
                    foreach (SimEvent simEvent in sim.Events.Current)
                    {
                        if (simEvent is StateHashedEvent hashed)
                        {
                            output.Write("tick=" + hashed.Tick.ToString(CultureInfo.InvariantCulture) + " hash=" + hashed.Hash.ToString("x16", CultureInfo.InvariantCulture) + "\n");
                        }
                        else if (simEvent is CommandRejectedEvent rejected)
                        {
                            error.Write("warning: tick " + rejected.Tick.ToString(CultureInfo.InvariantCulture) + ": " + rejected.Describe() + "\n");
                        }
                    }
                }
                output.Write("STATE_HASH=" + sim.ComputeStateHash().ToString("x16", CultureInfo.InvariantCulture) + "\n");
                output.Flush();
                return 0;
            }
            catch (Exception ex) when (ex is HarnessException || ex is DataValidationException || ex is IOException
                || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException)
            {
                error.Write("error: " + ex.Message + "\n");
                error.Flush();
                return 1;
            }
        }

        /// <summary>Reads every *.json file under the data folder recursively, with data-root-relative forward-slash paths, in ordinal path order.</summary>
        private static List<DataSourceFile> ReadDataFolder(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new HarnessException("data folder not found: " + directory);
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

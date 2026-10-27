// rts/tools/BalanceHarness/Program.cs
// Headless runner: loads every *.json under --data, builds a Simulation with the built-in faction mechanics, plays
// a scripted command stream for N ticks without Unity, prints one "tick=<n> hash=<16 hex>" line per state hash and a
// final "STATE_HASH=<16 hex>". The script is parsed by Rts.Sim.Data.CommandScript and defaults to
// <data>/scripts/smoke.json, the same file the Unity SimulationHost plays. Exit code 0 on success, 1 on any error (message on stderr). Part of
// Rts.Tools.BalanceHarness. Phase 0 proves determinism and the data pipeline in CI; Phase 2 grows this into the
// AI-versus-AI balance harness (see README.md). This tool does the file IO the simulation assembly may not.
// Skirmish AI (Phase 1, rts/src/Ai): a script player entry with "ai": "<profile>" or the option --ai <slot>:<profile> attaches a
// SkirmishAiController to that slot; before every step the controllers think in ascending slot order and submit their commands
// for the current tick (AiHost.ThinkAll). In a match with two or more teams the run stops early once MatchOutcome is decided,
// and every run prints "OUTCOME winner=<team number or draw> tick=<n>" (or "OUTCOME undecided tick=<n>") before STATE_HASH,
// where n is the last tick stepped.
// Replays (ADR-009): --record <file> routes every command (script and AI) and every step through a ReplayRecorder and, after the
// final STATE_HASH line, writes the binary replay and prints "REPLAY_RECORDED file=<path> ticks=<n> commands=<n> checkpoints=<n>";
// tools/ReplayCheck re-simulates that file. Without --record the run is exactly as before.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Ai;
using Rts.Replay;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;

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
                CommandScript script = CommandScript.Parse(File.ReadAllText(scriptPath), scriptPath.Replace('\\', '/'), BuiltInMechanics.ScriptParsers);
                // Seed and length: the command line wins over the script.
                ulong seed = options.SeedOverride ?? script.Seed;
                int ticks = options.TicksOverride ?? script.Ticks;

                // Map named by the script, or the first map in ordinal order.
                MapDefinition map = data.RequireMap(script.MapId);
                SimulationConfig config = new SimulationConfig(data.CreateMatchSettings(seed), data.Content, script.Players, BuiltInMechanics.CreateRegistry(), data.Tuning, map, data.Cycles.Values);
                Simulation sim = new Simulation(config);
                sim.Start();
                // Recorder that captures every command and hash checkpoint when --record is given; null otherwise.
                ReplayRecorder? recorder = options.RecordPath.Length > 0 ? new ReplayRecorder(sim, script.Players) : null;
                foreach (Command command in script.Commands)
                {
                    if (command.Tick >= ticks)
                    {
                        error.Write("warning: " + command + " is scheduled at or after the last tick (" + ticks + ") and will not run\n");
                    }
                    if (recorder != null)
                    {
                        recorder.Submit(command);
                    }
                    else
                    {
                        sim.Submit(command);
                    }
                }
                // AI controllers for the script's "ai" slots and the --ai options, in ascending slot order.
                List<IPlayerController> controllers = CreateControllers(data, script, options);
                if (recorder != null)
                {
                    // Wrap each controller so the commands it submits are recorded too. Index into the controller list.
                    for (int c = 0; c < controllers.Count; c++)
                    {
                        controllers[c] = new RecordingController(controllers[c], recorder);
                    }
                }
                AiHost.StartAll(sim, controllers);
                // True when the match has at least two teams, so a decided outcome can end the run early.
                bool canDecide = sim.Players.TeamCount >= 2;
                // Outcome after the last step run; undecided until a team is left standing.
                MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
                // Last tick stepped. Unit: ticks. Range: -1 before the first step.
                int lastTick = -1;
                for (int i = 0; i < ticks; i++)
                {
                    AiHost.ThinkAll(sim, controllers);
                    lastTick = sim.Clock.Tick;
                    sim.Step();
                    recorder?.AfterStep();
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
                    outcome = MatchOutcome.Evaluate(sim.Players);
                    if (canDecide && outcome.IsDecided)
                    {
                        break;
                    }
                }
                output.Write(DescribeOutcome(canDecide, outcome, lastTick) + "\n");
                output.Write("STATE_HASH=" + sim.ComputeStateHash().ToString("x16", CultureInfo.InvariantCulture) + "\n");
                if (recorder != null)
                {
                    // The finished replay: header, applied commands and checkpoints.
                    ReplayData replay = recorder.Finish();
                    File.WriteAllBytes(options.RecordPath, ReplayWriter.Write(replay, BuiltInMechanics.CreateCodecRegistry()));
                    output.Write("REPLAY_RECORDED file=" + options.RecordPath.Replace('\\', '/') + " ticks=" + replay.TickCount.ToString(CultureInfo.InvariantCulture)
                        + " commands=" + replay.Commands.Count.ToString(CultureInfo.InvariantCulture)
                        + " checkpoints=" + replay.Checkpoints.Count.ToString(CultureInfo.InvariantCulture) + "\n");
                }
                output.Flush();
                return 0;
            }
            catch (Exception ex) when (ex is HarnessException || ex is ReplayException || ex is DataValidationException || ex is IOException
                || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException)
            {
                error.Write("error: " + ex.Message + "\n");
                error.Flush();
                return 1;
            }
        }

        /// <summary>
        /// One <see cref="SkirmishAiController"/> per AI slot: the script's "ai" entries, overridden or extended by --ai options.
        /// Throws <see cref="HarnessException"/> for a slot that is not a script player, and DataValidationException for a missing
        /// or invalid profile file.
        /// </summary>
        private static List<IPlayerController> CreateControllers(DataSet data, CommandScript script, HarnessOptions options)
        {
            // Profile ID per player slot, in ascending slot order.
            SortedDictionary<int, string> slots = new SortedDictionary<int, string>();
            foreach (KeyValuePair<PlayerId, string> pair in script.AiProfiles)
            {
                slots[pair.Key.Value] = pair.Value;
            }
            foreach (KeyValuePair<int, string> pair in options.AiSlots)
            {
                slots[pair.Key] = pair.Value;
            }
            List<IPlayerController> controllers = new List<IPlayerController>();
            foreach (KeyValuePair<int, string> pair in slots)
            {
                // True when the script has a player in this slot.
                bool known = false;
                foreach (PlayerSetup player in script.Players)
                {
                    known |= player.Id.Value == pair.Key;
                }
                if (!known)
                {
                    throw new HarnessException("--ai names player slot " + pair.Key.ToString(CultureInfo.InvariantCulture) + ", which the script does not have");
                }
                controllers.Add(new SkirmishAiController(new PlayerId((byte)pair.Key), AiProfile.FromDataSet(data, pair.Value)));
            }
            return controllers;
        }

        /// <summary>The OUTCOME line: "OUTCOME winner=&lt;team&gt; tick=&lt;n&gt;", "OUTCOME winner=draw tick=&lt;n&gt;" or "OUTCOME undecided tick=&lt;n&gt;".</summary>
        /// <param name="canDecide">False for a match with fewer than two teams (always reported undecided).</param>
        /// <param name="outcome">The outcome after the last step.</param>
        /// <param name="lastTick">Last tick stepped. Unit: ticks. Range: -1 (no step) upward.</param>
        private static string DescribeOutcome(bool canDecide, MatchOutcome outcome, int lastTick)
        {
            // Tick text for the line.
            string tick = " tick=" + lastTick.ToString(CultureInfo.InvariantCulture);
            if (!canDecide || !outcome.IsDecided)
            {
                return "OUTCOME undecided" + tick;
            }
            return outcome.IsDraw ? "OUTCOME winner=draw" + tick : "OUTCOME winner=" + outcome.Winner.Value.ToString(CultureInfo.InvariantCulture) + tick;
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

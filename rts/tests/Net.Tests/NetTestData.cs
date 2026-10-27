// rts/tests/Net.Tests/NetTestData.cs
// Test helper that finds rts/data, loads it once, and builds the AI-versus-AI match setup the lockstep tests play.
// Part of Rts.Net.Tests. Test code only (may use System.IO); the libraries under test may not.
using System;
using System.Collections.Generic;
using System.IO;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;

namespace Rts.Net.Tests
{
    /// <summary>Locates and loads the real data folder, and builds simulations from the exit script.</summary>
    public static class NetTestData
    {
        /// <summary>The loaded data, shared by every test.</summary>
        private static DataSet? _data;

        /// <summary>The parsed exit script, shared by every test.</summary>
        private static CommandScript? _script;

        /// <summary>The shipped data set (every file under rts/data).</summary>
        public static DataSet Data => _data ??= DataLoader.Load(ReadAll());

        /// <summary>rts/data/scripts/ai_vs_ai.json, parsed with the built-in script parsers.</summary>
        public static CommandScript AiVsAi => _script ??= CommandScript.Parse(
            File.ReadAllText(Path.Combine(FindDataDirectory(), "scripts", "ai_vs_ai.json")), "scripts/ai_vs_ai.json", BuiltInMechanics.ScriptParsers);

        /// <summary>
        /// Absolute path of rts/data: walks up from the test assembly's folder and the working directory, preferring a
        /// data folder beside rts/src/Sim (the source tree) and otherwise taking the copy next to the test binary.
        /// </summary>
        public static string FindDataDirectory()
        {
            // Directories to walk up from.
            string[] starts = { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
            // Pass 0 wants the source tree; pass 1 takes any copy.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (string start in starts)
                {
                    // Directory being examined, moving up one level per iteration.
                    string? dir = Path.GetFullPath(start);
                    while (dir != null)
                    {
                        foreach (string candidate in new[] { Path.Combine(dir, "rts", "data"), Path.Combine(dir, "data") })
                        {
                            if (!File.Exists(Path.Combine(candidate, "tuning", "match.json")))
                            {
                                continue;
                            }
                            // True when a Sim source folder sits beside the data folder.
                            string? parent = Path.GetDirectoryName(candidate);
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

        /// <summary>Reads every *.json file under the data folder, with data-root-relative forward-slash paths, in ordinal path order.</summary>
        private static List<DataSourceFile> ReadAll()
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

        /// <summary>The exit script's match (greybox_1v1, Skai slot 0 versus Water slot 1) as the lockstep peers' factory builds it: an empty started simulation plus the script's spawns as setup commands.</summary>
        public static LockstepMatch CreateAiVsAiMatch()
        {
            return CreateMatch(2);
        }

        /// <summary>
        /// The player slots of an N-player match: the exit script's two AI slots, then idle extra Water slots (no units, no AI) on
        /// team 1 beside the Water AI. The script's match is won by Water (Skai's HQ falls around tick 4300), so with the idle
        /// players on the winning side the N-player match is still decided by that fall; an idle player on Skai's team would keep
        /// team 0 standing for ever.
        /// </summary>
        /// <param name="playerCount">Slots wanted. Range: 2 to 255.</param>
        public static List<PlayerSetup> PlayersFor(int playerCount)
        {
            if (playerCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }
            // The script's slots, then the extras.
            List<PlayerSetup> players = new List<PlayerSetup>(AiVsAi.Players);
            // Slot being added. Range: 2 to playerCount - 1.
            for (int slot = 2; slot < playerCount; slot++)
            {
                players.Add(new PlayerSetup(new PlayerId((byte)slot), new TeamId(1), "water", "Idle " + slot));
            }
            return players;
        }

        /// <summary>
        /// An N-player match on the exit script's map and seed: the script's two AI players plus <see cref="PlayersFor"/>'s idle
        /// extras, the script's spawns as setup commands, and nothing queued in the simulation (the peer submits the setup).
        /// </summary>
        /// <param name="playerCount">Slots in the match. Range: 2 upward.</param>
        public static LockstepMatch CreateMatch(int playerCount)
        {
            // The script to play.
            CommandScript script = AiVsAi;
            // The simulation under test, started and empty.
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(script.Seed), Data.Content, PlayersFor(playerCount),
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap(script.MapId), Data.Cycles.Values));
            sim.Start();
            return new LockstepMatch(sim, script.Commands);
        }
    }
}

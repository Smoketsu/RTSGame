// rts/tests/Sim.Tests/Campaign/CampaignTestSupport.cs
// Helpers for the campaign tests: loads the shipped data set and the shipped tutorial files, builds small test missions from text, drives a mission
// step by step with a per-tick callback, writes temporary campaign folders for the tool, and runs the MissionRunner tool with captured output.
// Test code, so it may use files. Part of Rts.Sim.Tests (spec: rts/docs/specs/campaign_scripting.md section 8).
using System;
using System.Collections.Generic;
using System.IO;
using Rts.Campaign;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using MissionTool = Rts.Tools.MissionTool.Program;

namespace Rts.Sim.Tests.Campaign
{
    /// <summary>Shared helpers of the campaign tests.</summary>
    public static class CampaignTestSupport
    {
        /// <summary>The shipped data folder copied next to the test binary. Range: an existing folder.</summary>
        public static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

        /// <summary>The shipped campaign folder (missions and inputs) copied next to the test binary. Range: an existing folder.</summary>
        public static string CampaignDir => Path.Combine(AppContext.BaseDirectory, "campaign");

        /// <summary>The loaded data set, read once; it is immutable.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        public static DataSet Data
        {
            get
            {
                if (_data == null)
                {
                    // Files of the data folder in ordinal order.
                    List<DataSourceFile> files = new List<DataSourceFile>();
                    string[] paths = Directory.GetFiles(DataDir, "*.json", SearchOption.AllDirectories);
                    Array.Sort(paths, StringComparer.Ordinal);
                    foreach (string path in paths)
                    {
                        files.Add(new DataSourceFile(Path.GetRelativePath(DataDir, path).Replace('\\', '/'), File.ReadAllText(path)));
                    }
                    _data = DataLoader.Load(files);
                }
                return _data;
            }
        }

        /// <summary>Text of the shipped tutorial mission file.</summary>
        public static string TutorialText => File.ReadAllText(Path.Combine(CampaignDir, "missions", "tutorial_01.json"));

        /// <summary>Text of the shipped tutorial inputs file.</summary>
        public static string TutorialInputsText => File.ReadAllText(Path.Combine(CampaignDir, "inputs", "tutorial_01.json"));

        /// <summary>Parses mission text as if it were the file <c>missions/&lt;id&gt;.json</c>.</summary>
        /// <param name="text">Mission text.</param>
        /// <param name="id">Mission ID (the file stem).</param>
        public static MissionDefinition Parse(string text, string id)
        {
            return MissionLoader.Parse(text, "campaign/missions/" + id + ".json", Data, BuiltInMechanics.ScriptParsers);
        }

        /// <summary>The tutorial's starting units as JSON, used by most test missions. Range: valid setup array text.</summary>
        public const string DefaultSetup =
            "[ { \"type\": \"spawn_unit\", \"definition\": \"skai.great_aerie\", \"owner\": 0, \"x\": \"7\", \"y\": \"7\" },"
            + " { \"type\": \"spawn_unit\", \"definition\": \"skai.fledgling\", \"owner\": 0, \"x\": \"6\", \"y\": \"9\" },"
            + " { \"type\": \"spawn_unit\", \"definition\": \"skai.fledgling\", \"owner\": 0, \"x\": \"9\", \"y\": \"6\" },"
            + " { \"type\": \"spawn_unit\", \"definition\": \"skai.windrunner\", \"owner\": 0, \"x\": \"10.5\", \"y\": \"10.5\" } ]";

        /// <summary>One human Skai player in slot 0. Range: valid players array text.</summary>
        public const string DefaultPlayers = "[ { \"id\": 0, \"team\": 0, \"faction\": \"skai\", \"name\": \"You\", \"controller\": \"human\" } ]";

        /// <summary>Builds mission text on greybox_1v1 from the parts a test varies.</summary>
        /// <param name="id">Mission ID.</param>
        /// <param name="objectives">Objectives array text.</param>
        /// <param name="triggers">Triggers array text.</param>
        /// <param name="victory">Victory objective ID.</param>
        /// <param name="extra">Extra root members text such as a defeat_when (starts with a comma), or empty.</param>
        /// <param name="maxSeconds">Time limit. Unit: seconds. Range: 30 to 7200.</param>
        /// <param name="cycles">The planetary_cycles value.</param>
        /// <param name="players">Players array text.</param>
        /// <param name="setup">Setup array text.</param>
        public static string Mission(string id, string objectives, string triggers, string victory, string extra = "", int maxSeconds = 900, bool cycles = false,
            string players = DefaultPlayers, string setup = DefaultSetup)
        {
            return "{ \"$schema_version\": 1, \"$doc\": \"Test mission.\", \"id\": \"" + id + "\", \"display_name\": \"Test\", \"map\": \"greybox_1v1\", \"seed\": 1,"
                + " \"max_seconds\": " + maxSeconds + ", \"planetary_cycles\": " + (cycles ? "true" : "false") + ", \"players\": " + players + ", \"setup\": " + setup
                + ", \"objectives\": " + objectives + ", \"triggers\": " + triggers + ", \"victory_objective\": \"" + victory + "\"" + extra + " }";
        }

        /// <summary>An objective that never completes inside a test run (3600 seconds of running).</summary>
        public const string NeverObjectives = "[ { \"id\": \"never\", \"text\": \"Wait.\", \"requires\": [], \"complete_when\": { \"type\": \"seconds_active\", \"seconds\": 3600 } } ]";

        /// <summary>Starts the mission's simulation and runner and steps it, calling <paramref name="afterStep"/> after each runner step.</summary>
        /// <param name="mission">The mission.</param>
        /// <param name="inputs">Scripted inputs, or null.</param>
        /// <param name="ticks">Steps to run at most; the loop also stops when the mission ends. Unit: ticks.</param>
        /// <param name="beforeStep">Called before each step with the tick about to run, the simulation and the runner; may report signals; may be null.</param>
        /// <param name="afterStep">Called after each runner step with the tick just stepped, the simulation and the runner; may be null.</param>
        /// <param name="runnerOut">Receives the runner (also usable inside the callbacks only after this returns).</param>
        public static Simulation Drive(MissionDefinition mission, MissionInputs? inputs, int ticks, Action<int, Simulation, MissionRunner>? beforeStep,
            Action<int, Simulation, MissionRunner>? afterStep, out MissionRunner runnerOut)
        {
            Simulation sim = mission.CreateSimulation(Data);
            sim.Start();
            MissionRunner runner = new MissionRunner(mission, null);
            runner.Start(sim);
            runnerOut = runner;
            for (int i = 0; i < ticks && runner.Status == MissionStatus.Running; i++)
            {
                inputs?.BeforeStep(sim, runner);
                beforeStep?.Invoke(sim.Clock.Tick, sim, runner);
                sim.Step();
                runner.AfterStep(sim);
                afterStep?.Invoke(sim.Clock.Tick - 1, sim, runner);
            }
            return sim;
        }

        /// <summary>Writes a campaign folder with the given mission and inputs files in a new temporary folder and returns its path.</summary>
        /// <param name="missions">Mission file stem to text.</param>
        /// <param name="inputs">Inputs file stem to text.</param>
        public static string TempCampaign(IDictionary<string, string> missions, IDictionary<string, string> inputs)
        {
            // Fresh folder under the system temp folder.
            string root = Path.Combine(Path.GetTempPath(), "rts_campaign_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "missions"));
            Directory.CreateDirectory(Path.Combine(root, "inputs"));
            foreach (KeyValuePair<string, string> pair in missions)
            {
                File.WriteAllText(Path.Combine(root, "missions", pair.Key + ".json"), pair.Value);
            }
            foreach (KeyValuePair<string, string> pair in inputs)
            {
                File.WriteAllText(Path.Combine(root, "inputs", pair.Key + ".json"), pair.Value);
            }
            return root;
        }

        /// <summary>Runs the MissionRunner tool and returns its exit code with the captured stdout and stderr.</summary>
        /// <param name="args">Arguments after the program name.</param>
        /// <param name="output">Captured stdout.</param>
        /// <param name="error">Captured stderr.</param>
        public static int RunTool(string[] args, out string output, out string error)
        {
            StringWriter outWriter = new StringWriter();
            StringWriter errWriter = new StringWriter();
            int code = MissionTool.Run(args, outWriter, errWriter);
            output = outWriter.ToString();
            error = errWriter.ToString();
            return code;
        }

        /// <summary>The tool arguments for a mission in a campaign folder against the shipped data.</summary>
        /// <param name="campaign">Campaign folder.</param>
        /// <param name="mission">Mission ID.</param>
        /// <param name="more">Extra arguments.</param>
        public static string[] ToolArgs(string campaign, string mission, params string[] more)
        {
            // Required arguments then the extras.
            List<string> args = new List<string> { "--data", DataDir, "--campaign", campaign, "--mission", mission };
            args.AddRange(more);
            return args.ToArray();
        }
    }
}

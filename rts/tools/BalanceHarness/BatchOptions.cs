// rts/tools/BalanceHarness/BatchOptions.cs
// Command-line options of the batch modes of the harness: --batch (coordinator) and --worker (one process of the pool), spec
// rts/docs/specs/balance_harness.md section 5. HarnessOptions routes to this class when the arguments contain --batch or --worker; every
// single-run option (--script, --ticks, --ai, --record) is then an error. Any problem is a HarnessException (exit code 1). Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Parsed batch or worker command line.</summary>
    public sealed class BatchOptions
    {
        /// <summary>Usage text printed with command-line errors.</summary>
        public const string Usage = "usage: Rts.Tools.BalanceHarness --data <data folder> --batch [--ci | --games N] [--jobs J] [--maps a,b] [--matchups x_vs_y,...] "
            + "[--seed S] [--max-game-seconds T] [--verify K] [--out <dir>] [--report-only]"
            + " | --data <data folder> --ladder --profile-a <baseline id> --profile-b <candidate id> [--games N] [--maps a,b] [--matchups x,y] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>] [--non-regression]"
            + " | --data <data folder> --response --profile <id> [--only <script file name>]"
            + " | --data <data folder> --fights [--jobs J] [--seed S] [--only <fight id>] [--out <dir>] [--strict]"
            + " | --data <data folder> --trace --map <id> --matchup <a_vs_b> [--seed S] [--game N] [--max-game-seconds T] [--out <file>]";

        /// <summary>Fault switch value that makes a re-run (verification) worker perturb the seed of its first game. Test use only.</summary>
        public const string FaultPerturb = "perturb";

        /// <summary>Fault switch value that makes every worker exit 1. Test use only.</summary>
        public const string FaultWorkerExit = "worker-exit";

        /// <summary>Data folder. Required.</summary>
        public string DataDirectory { get; private set; } = string.Empty;

        /// <summary>True for the worker mode, false for the coordinator (--batch).</summary>
        public bool IsWorker { get; private set; }

        /// <summary>True when --ci was given.</summary>
        public bool Ci { get; private set; }

        /// <summary>Games per cell from --games, or null. Unit: games. Range: 1 to 5000.</summary>
        public int? Games { get; private set; }

        /// <summary>Worker processes at once from --jobs, or null for the processor count. Unit: processes. Range: 1 to 64.</summary>
        public int? Jobs { get; private set; }

        /// <summary>Maps from --maps, or null to use the data. Unit: map IDs.</summary>
        public IReadOnlyList<string>? Maps { get; private set; }

        /// <summary>Matchups from --matchups, or null to use the data. Unit: matchup IDs.</summary>
        public IReadOnlyList<string>? Matchups { get; private set; }

        /// <summary>Base seed from --seed, or null to use the data. Range: any ulong.</summary>
        public ulong? Seed { get; private set; }

        /// <summary>Game length limit from --max-game-seconds, or null to use the data. Unit: seconds. Range: 1 to 7200.</summary>
        public int? MaxGameSeconds { get; private set; }

        /// <summary>Determinism re-runs per cell from --verify, or null to use the data. Unit: games. Range: 0 to 10.</summary>
        public int? Verify { get; private set; }

        /// <summary>Output folder from --out. Range: any writable folder path.</summary>
        public string OutDirectory { get; private set; } = DefaultOutDirectory;

        /// <summary>Output folder used when --out is omitted. Range: fixed path (git-ignored build output).</summary>
        public const string DefaultOutDirectory = "rts/build/harness";

        /// <summary>True when --report-only was given: a failed hard limit still exits 0.</summary>
        public bool ReportOnly { get; private set; }

        /// <summary>True when --ladder was given: play profile A against profile B (see <see cref="LadderRunner"/>).</summary>
        public bool Ladder { get; private set; }

        /// <summary>True when --response was given: run the scripted-opponent scenarios (see <see cref="ResponseRunner"/>).</summary>
        public bool Response { get; private set; }

        /// <summary>True when --fights was given: play the equal-value fight sweep (see <see cref="FightCoordinator"/>); with --worker, play a chunk of its fights.</summary>
        public bool Fights { get; private set; }

        /// <summary>True when --strict was given: a failed fight check exits 2.</summary>
        public bool Strict { get; private set; }

        /// <summary>True when --non-regression was given: the ladder bar is ladder_non_regression_fraction_min instead of ladder_candidate_win_fraction_min.</summary>
        public bool NonRegression { get; private set; }

        /// <summary>Baseline profile ID from --profile-a (ladder and ladder workers), or empty. Range: a file name under rts/data/ai without .json.</summary>
        public string ProfileA { get; private set; } = string.Empty;

        /// <summary>Candidate profile ID from --profile-b (ladder and ladder workers), or empty. Range: a file name under rts/data/ai without .json.</summary>
        public string ProfileB { get; private set; } = string.Empty;

        /// <summary>Profile of the AI under test from --profile (response mode), or empty. Range: a file name under rts/data/ai without .json.</summary>
        public string Profile { get; private set; } = string.Empty;

        /// <summary>Only scenario script file name from --only (response mode), or empty for every scenario.</summary>
        public string Only { get; private set; } = string.Empty;

        /// <summary>True when --trace was given: play one game and write its timeline (see <see cref="GameTrace"/>); needs --map and --matchup.</summary>
        public bool Trace { get; private set; }

        /// <summary>Trace: game index within the cell from --game; 0 is the game a batch plays first. Unit: games. Range: 0 upward.</summary>
        public int TraceGame { get; private set; }

        /// <summary>Worker: map ID of the cell.</summary>
        public string WorkerMap { get; private set; } = string.Empty;

        /// <summary>Worker: matchup ID of the cell.</summary>
        public string WorkerMatchup { get; private set; } = string.Empty;

        /// <summary>Worker: first game index. Unit: games. Range: 0 upward.</summary>
        public int WorkerFirst { get; private set; }

        /// <summary>Worker: number of games. Unit: games. Range: 1 upward.</summary>
        public int WorkerCount { get; private set; }

        /// <summary>Worker: the HQ floor trial "&lt;attacker faction&gt;:&lt;unit&gt;:&lt;defender faction&gt;:&lt;rooted|uprooted&gt;", or empty for a game worker.</summary>
        public string WorkerHqFloor { get; private set; } = string.Empty;

        /// <summary>Hidden test switch (--test-fault), or empty. See <see cref="FaultPerturb"/> and <see cref="FaultWorkerExit"/>.</summary>
        public string TestFault { get; private set; } = string.Empty;

        /// <summary>Worker: true when this call is a determinism re-run (--rerun), which the perturb fault acts on.</summary>
        public bool WorkerIsRerun { get; private set; }

        /// <summary>True when the argument list asks for a batch or worker run.</summary>
        /// <param name="args">Command-line arguments.</param>
        public static bool IsBatchMode(string[] args)
        {
            return Array.IndexOf(args, "--batch") >= 0 || Array.IndexOf(args, "--worker") >= 0 || Array.IndexOf(args, "--trace") >= 0
                || Array.IndexOf(args, "--ladder") >= 0 || Array.IndexOf(args, "--response") >= 0 || Array.IndexOf(args, "--fights") >= 0;
        }

        /// <summary>Parses the arguments; throws <see cref="HarnessException"/> on anything unexpected.</summary>
        /// <param name="args">The arguments after the program name.</param>
        public static BatchOptions Parse(string[] args)
        {
            BatchOptions options = new BatchOptions();
            options.IsWorker = Array.IndexOf(args, "--worker") >= 0;
            if (options.IsWorker && Array.IndexOf(args, "--batch") >= 0)
            {
                throw new HarnessException("--batch and --worker cannot be combined. " + Usage);
            }
            for (int i = 0; i < args.Length; i++)
            {
                // Flag being read.
                string flag = args[i];
                switch (flag)
                {
                    case "--batch":
                    case "--worker":
                        continue;
                    case "--trace":
                        options.Trace = true;
                        continue;
                    case "--ladder":
                        options.Ladder = true;
                        continue;
                    case "--response":
                        options.Response = true;
                        continue;
                    case "--fights":
                        options.Fights = true;
                        continue;
                    case "--strict":
                        options.Strict = true;
                        continue;
                    case "--non-regression":
                        options.NonRegression = true;
                        continue;
                    case "--ci":
                        options.Ci = true;
                        continue;
                    case "--report-only":
                        options.ReportOnly = true;
                        continue;
                    case "--rerun":
                        options.WorkerIsRerun = true;
                        continue;
                    case "--data":
                    case "--games":
                    case "--jobs":
                    case "--maps":
                    case "--matchups":
                    case "--seed":
                    case "--max-game-seconds":
                    case "--verify":
                    case "--out":
                    case "--map":
                    case "--matchup":
                    case "--first":
                    case "--game":
                    case "--count":
                    case "--hq-floor":
                    case "--test-fault":
                    case "--profile-a":
                    case "--profile-b":
                    case "--profile":
                    case "--only":
                        break;
                    default:
                        throw new HarnessException("unknown argument '" + flag + "' in batch mode. " + Usage);
                }
                if (i + 1 >= args.Length)
                {
                    throw new HarnessException(flag + " needs a value. " + Usage);
                }
                // The flag's value.
                string value = args[++i];
                switch (flag)
                {
                    case "--data":
                        options.DataDirectory = value;
                        break;
                    case "--games":
                        options.Games = ParseInt(flag, value, 1, 5000);
                        break;
                    case "--jobs":
                        options.Jobs = ParseInt(flag, value, 1, 64);
                        break;
                    case "--maps":
                        options.Maps = SplitList(value);
                        break;
                    case "--matchups":
                        options.Matchups = SplitList(value);
                        break;
                    case "--seed":
                        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
                        {
                            throw new HarnessException("--seed must be a whole number from 0 to 18446744073709551615 (found '" + value + "')");
                        }
                        options.Seed = seed;
                        break;
                    case "--max-game-seconds":
                        options.MaxGameSeconds = ParseInt(flag, value, 1, 7200);
                        break;
                    case "--verify":
                        options.Verify = ParseInt(flag, value, 0, 10);
                        break;
                    case "--out":
                        options.OutDirectory = value;
                        break;
                    case "--map":
                        options.WorkerMap = value;
                        break;
                    case "--matchup":
                        options.WorkerMatchup = value;
                        break;
                    case "--game":
                        options.TraceGame = ParseInt(flag, value, 0, int.MaxValue);
                        break;
                    case "--first":
                        options.WorkerFirst = ParseInt(flag, value, 0, int.MaxValue);
                        break;
                    case "--count":
                        options.WorkerCount = ParseInt(flag, value, 1, int.MaxValue);
                        break;
                    case "--hq-floor":
                        options.WorkerHqFloor = value;
                        break;
                    case "--profile-a":
                        options.ProfileA = value;
                        break;
                    case "--profile-b":
                        options.ProfileB = value;
                        break;
                    case "--profile":
                        options.Profile = value;
                        break;
                    case "--only":
                        options.Only = value;
                        break;
                    default:
                        options.TestFault = value;
                        break;
                }
            }
            if (options.DataDirectory.Length == 0)
            {
                throw new HarnessException("--data is required. " + Usage);
            }
            if (options.Ci && options.Games != null)
            {
                throw new HarnessException("--ci and --games cannot be combined. " + Usage);
            }
            if (options.Ladder && (options.ProfileA.Length == 0 || options.ProfileB.Length == 0))
            {
                throw new HarnessException("--ladder needs --profile-a and --profile-b. " + Usage);
            }
            if (options.Response && options.Profile.Length == 0)
            {
                throw new HarnessException("--response needs --profile. " + Usage);
            }
            if (options.ProfileA.Length > 0 != (options.ProfileB.Length > 0))
            {
                throw new HarnessException("--profile-a and --profile-b go together. " + Usage);
            }
            if (options.Trace && (options.WorkerMap.Length == 0 || options.WorkerMatchup.Length == 0))
            {
                throw new HarnessException("--trace needs --map and --matchup. " + Usage);
            }
            if (options.IsWorker && options.Fights && options.WorkerHqFloor.Length == 0 && options.WorkerCount == 0)
            {
                throw new HarnessException("a fight worker needs --first and --count, or --hq-floor. " + Usage);
            }
            if (options.IsWorker && !options.Fights && options.WorkerHqFloor.Length == 0 && (options.WorkerMap.Length == 0 || options.WorkerMatchup.Length == 0 || options.WorkerCount == 0))
            {
                throw new HarnessException("a game worker needs --map, --matchup and --count. " + Usage);
            }
            return options;
        }

        /// <summary>Parses a whole number in [min, max] or throws a HarnessException naming the flag.</summary>
        private static int ParseInt(string flag, string value, int min, int max)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < min || number > max)
            {
                throw new HarnessException(flag + " must be a whole number from " + min.ToString(CultureInfo.InvariantCulture) + " to "
                    + max.ToString(CultureInfo.InvariantCulture) + " (found '" + value + "')");
            }
            return number;
        }

        /// <summary>Splits a comma list into non-empty trimmed entries.</summary>
        private static List<string> SplitList(string value)
        {
            // Entries in the order given.
            List<string> list = new List<string>();
            foreach (string part in value.Split(','))
            {
                if (part.Trim().Length > 0)
                {
                    list.Add(part.Trim());
                }
            }
            return list;
        }
    }
}

// rts/tools/BalanceHarness/HarnessOptions.cs
// Command-line options of the headless harness: --data, --script, --ticks, --seed. Part of Rts.Tools.BalanceHarness.
// Plain parsing with clear error messages; any problem is a HarnessException that Program turns into exit code 1.
using System;
using System.Globalization;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>An error the harness reports to stderr with exit code 1.</summary>
    public sealed class HarnessException : Exception
    {
        /// <summary>Creates the exception with a user-facing message.</summary>
        public HarnessException(string message) : base(message)
        {
        }
    }

    /// <summary>Parsed command line.</summary>
    public sealed class HarnessOptions
    {
        /// <summary>Usage text printed with command-line errors.</summary>
        public const string Usage = "usage: Rts.Tools.BalanceHarness --data <data folder> --script <script.json> [--ticks N] [--seed S]";

        /// <summary>Folder holding the game data (tuning/, units/, factions/, maps/, cycles/). Required.</summary>
        public string DataDirectory { get; private set; } = string.Empty;

        /// <summary>Path of the script JSON file. Required.</summary>
        public string ScriptPath { get; private set; } = string.Empty;

        /// <summary>Tick count that overrides the script's "ticks", or null to use the script. Unit: ticks. Range: 0 to 1 000 000.</summary>
        public int? TicksOverride { get; private set; }

        /// <summary>Seed that overrides the script's "seed", or null to use the script. Range: any ulong.</summary>
        public ulong? SeedOverride { get; private set; }

        /// <summary>Parses the arguments; throws <see cref="HarnessException"/> on anything unexpected.</summary>
        /// <param name="args">The arguments after the program name.</param>
        public static HarnessOptions Parse(string[] args)
        {
            HarnessOptions options = new HarnessOptions();
            for (int i = 0; i < args.Length; i++)
            {
                // Flag being read; its value is the next argument.
                string flag = args[i];
                if (flag != "--data" && flag != "--script" && flag != "--ticks" && flag != "--seed")
                {
                    throw new HarnessException("unknown argument '" + flag + "'. " + Usage);
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
                    case "--script":
                        options.ScriptPath = value;
                        break;
                    case "--ticks":
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int ticks) || ticks > 1000000)
                        {
                            throw new HarnessException("--ticks must be a whole number from 0 to 1000000 (found '" + value + "')");
                        }
                        options.TicksOverride = ticks;
                        break;
                    default:
                        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
                        {
                            throw new HarnessException("--seed must be a whole number from 0 to 18446744073709551615 (found '" + value + "')");
                        }
                        options.SeedOverride = seed;
                        break;
                }
            }
            if (options.DataDirectory.Length == 0)
            {
                throw new HarnessException("--data is required. " + Usage);
            }
            if (options.ScriptPath.Length == 0)
            {
                throw new HarnessException("--script is required. " + Usage);
            }
            return options;
        }
    }
}

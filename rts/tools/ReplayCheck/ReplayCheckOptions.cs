// rts/tools/ReplayCheck/ReplayCheckOptions.cs
// Command-line options of ReplayCheck: --data <folder> and --replay <file>, both required. Part of Rts.Tools.ReplayCheck.
// Any problem is a ReplayCheckException that Program reports on stderr as a refusal (exit code 2).
using System;

namespace Rts.Tools.ReplayCheck
{
    /// <summary>A usage or input error ReplayCheck reports with exit code 2.</summary>
    public sealed class ReplayCheckException : Exception
    {
        /// <summary>Creates the exception with a user-facing message.</summary>
        /// <param name="message">What is wrong. Range: non-empty text.</param>
        public ReplayCheckException(string message) : base(message)
        {
        }
    }

    /// <summary>Parsed command line.</summary>
    public sealed class ReplayCheckOptions
    {
        /// <summary>Usage text printed with command-line errors.</summary>
        public const string Usage = "usage: Rts.Tools.ReplayCheck --data <data folder> --replay <replay file>";

        /// <summary>Folder holding the game data (tuning/, units/, factions/, maps/, cycles/). Required.</summary>
        public string DataDirectory { get; private set; } = string.Empty;

        /// <summary>Path of the replay file to verify. Required.</summary>
        public string ReplayPath { get; private set; } = string.Empty;

        /// <summary>Parses the arguments; throws <see cref="ReplayCheckException"/> on anything unexpected.</summary>
        /// <param name="args">The arguments after the program name.</param>
        public static ReplayCheckOptions Parse(string[] args)
        {
            ReplayCheckOptions options = new ReplayCheckOptions();
            for (int i = 0; i < args.Length; i++)
            {
                // Flag being read; its value is the next argument.
                string flag = args[i];
                if (flag != "--data" && flag != "--replay")
                {
                    throw new ReplayCheckException("unknown argument '" + flag + "'. " + Usage);
                }
                if (i + 1 >= args.Length)
                {
                    throw new ReplayCheckException(flag + " needs a value. " + Usage);
                }
                // The flag's value.
                string value = args[++i];
                if (flag == "--data")
                {
                    options.DataDirectory = value;
                }
                else
                {
                    options.ReplayPath = value;
                }
            }
            if (options.DataDirectory.Length == 0 || options.ReplayPath.Length == 0)
            {
                throw new ReplayCheckException("--data and --replay are both required. " + Usage);
            }
            return options;
        }
    }
}

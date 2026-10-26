// rts/src/Sim/Data/DataValidationException.cs
// The one exception type the data loader throws for bad content. Part of Rts.Sim.Data.
// Every error names the data file and the field inside it, so a designer can fix a typo without a debugger.
using System;

namespace Rts.Sim.Data
{
    /// <summary>
    /// A data file is missing, malformed or out of range. <see cref="Exception.Message"/> is
    /// <c>file + ": field '" + field + "' " + problem</c>; when <see cref="Field"/> is empty (a problem with the
    /// file as a whole, such as a JSON syntax error) the message is <c>file + ": " + problem</c>.
    /// </summary>
    public class DataValidationException : Exception
    {
        /// <summary>Data file the problem is in: a data-root-relative path such as "tuning/hq.json", or the source name given to the reader. Never empty.</summary>
        public string File { get; }

        /// <summary>Path of the offending field inside the file, such as "root_seconds", "spawns[1].x" or "" for a file-level problem.</summary>
        public string Field { get; }

        /// <summary>What is wrong, as a phrase that follows the field name, such as "is missing" or "must be a number from 1 to 60".</summary>
        public string Problem { get; }

        /// <summary>Builds the exception and its message.</summary>
        /// <param name="file">Data file path or source name. Not empty.</param>
        /// <param name="field">Field path inside the file; empty for a file-level problem.</param>
        /// <param name="problem">The problem phrase, shown after the field name.</param>
        public DataValidationException(string file, string field, string problem)
            : base(BuildMessage(file, field, problem))
        {
            File = file ?? string.Empty;
            Field = field ?? string.Empty;
            Problem = problem ?? string.Empty;
        }

        /// <summary>Formats the message in the shape the contract promises.</summary>
        private static string BuildMessage(string file, string field, string problem)
        {
            if (string.IsNullOrEmpty(field))
            {
                return file + ": " + problem;
            }
            return file + ": field '" + field + "' " + problem;
        }
    }
}

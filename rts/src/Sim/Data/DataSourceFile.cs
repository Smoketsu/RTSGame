// rts/src/Sim/Data/DataSourceFile.cs
// One data file handed to the loader: its path relative to the data root plus its text. Part of Rts.Sim.Data.
// Hosts (the headless tool, Unity, tests) do the file IO; the loader never touches the file system (ADR-002).
using System;

namespace Rts.Sim.Data
{
    /// <summary>Immutable pair of a data-root-relative path and the file's text.</summary>
    public sealed class DataSourceFile
    {
        /// <summary>Path relative to the data root with forward slashes, such as "tuning/match.json" or "units/skai/windrunner.json". Not empty.</summary>
        public string Path { get; }

        /// <summary>The whole file as text (UTF-8 already decoded). May be empty, which the loader reports as a syntax error.</summary>
        public string Text { get; }

        /// <summary>Creates the pair. Backslashes in <paramref name="path"/> are converted to forward slashes so Windows hosts work unchanged.</summary>
        /// <param name="path">Data-root-relative path. Not null or empty.</param>
        /// <param name="text">File contents. Not null.</param>
        public DataSourceFile(string path, string text)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A data file needs a path.", nameof(path));
            }
            Path = path.Replace('\\', '/');
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }
}

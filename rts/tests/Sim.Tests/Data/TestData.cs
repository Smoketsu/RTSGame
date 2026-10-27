// rts/tests/Sim.Tests/Data/TestData.cs
// Test helper that finds the repository's rts/data folder and reads every JSON file in it, so tests can load the
// real shipped content. Part of Rts.Sim.Tests. This is test code, so it may use System.IO; the simulation may not.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Sim.Tests.Data
{
    /// <summary>Locates and reads the real data folder.</summary>
    public static class TestData
    {
        /// <summary>A file every valid data folder contains, used to recognise the folder while walking up directories.</summary>
        private const string MarkerFile = "tuning/match.json";

        /// <summary>
        /// Absolute path of rts/data. Walks up from the test assembly's folder, then from the current directory. The first
        /// pass accepts only a folder that sits next to rts/src/Sim (the source tree); the second pass accepts any
        /// "data" folder holding tuning/match.json (for example the copy the test project places beside the binary).
        /// </summary>
        public static string FindDataDirectory()
        {
            // Directories to start walking from: assembly folder first, then the working directory.
            List<string> starts = new List<string>();
            starts.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty);
            starts.Add(AppContext.BaseDirectory);
            starts.Add(Directory.GetCurrentDirectory());
            // Pass 0 wants the source tree; pass 1 takes any copy.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (string start in starts)
                {
                    // Directory being examined, moving up one level per iteration.
                    string? dir = string.IsNullOrEmpty(start) ? null : Path.GetFullPath(start);
                    while (dir != null)
                    {
                        foreach (string candidate in new[] { Path.Combine(dir, "rts", "data"), Path.Combine(dir, "data") })
                        {
                            if (!File.Exists(Path.Combine(candidate, "tuning", "match.json")))
                            {
                                continue;
                            }
                            // True when a Sim source folder sits beside the data folder (rts/src/Sim).
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
        public static List<DataSourceFile> ReadAll()
        {
            // Absolute data folder.
            string root = FindDataDirectory();
            // Absolute file paths, sorted so the list is the same on every machine.
            string[] paths = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            List<DataSourceFile> files = new List<DataSourceFile>();
            foreach (string path in paths)
            {
                // Path relative to the data root with forward slashes.
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                files.Add(new DataSourceFile(relative, File.ReadAllText(path)));
            }
            return files;
        }

        /// <summary>Loads the whole real data folder.</summary>
        public static DataSet LoadAll()
        {
            return DataLoader.Load(ReadAll());
        }

        /// <summary>
        /// Returns a copy of the real file list in which the text of <paramref name="path"/> has <paramref name="oldText"/>
        /// replaced by <paramref name="newText"/> (first occurrence). Fails the test if the file or the text is absent,
        /// so a changed data file cannot make a failure test pass for the wrong reason.
        /// </summary>
        public static List<DataSourceFile> WithEdit(string path, string oldText, string newText)
        {
            // The real files, edited below.
            List<DataSourceFile> files = ReadAll();
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].Path != path)
                {
                    continue;
                }
                // Index of the text to replace.
                int at = files[i].Text.IndexOf(oldText, StringComparison.Ordinal);
                if (at < 0)
                {
                    throw new InvalidOperationException("Test setup: '" + oldText + "' not found in " + path + ".");
                }
                files[i] = new DataSourceFile(path, files[i].Text.Substring(0, at) + newText + files[i].Text.Substring(at + oldText.Length));
                return files;
            }
            throw new InvalidOperationException("Test setup: no data file " + path + ".");
        }

        /// <summary>Returns a copy of the real file list with <paramref name="path"/> removed.</summary>
        public static List<DataSourceFile> Without(string path)
        {
            // The real files, filtered below.
            List<DataSourceFile> files = ReadAll();
            files.RemoveAll(f => f.Path == path);
            return files;
        }
    }
}

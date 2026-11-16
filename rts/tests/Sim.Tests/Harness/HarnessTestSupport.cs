// rts/tests/Sim.Tests/Harness/HarnessTestSupport.cs
// Helpers for the balance harness tests: copies the shipped data folder to a temporary folder with text edits applied (so a test can change a
// unit or a tuning value without touching rts/data), loads a data set from it and runs the harness entry point with captured output.
// Test code, so it may use files and processes. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using HarnessProgram = Rts.Tools.BalanceHarness.Program;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>One text replacement in a data file.</summary>
    public sealed class DataEdit
    {
        /// <summary>Data-root-relative path of the file, forward slashes.</summary>
        public string File { get; }

        /// <summary>Text that must occur in the file; its first occurrence is replaced (or all of them when <see cref="All"/>).</summary>
        public string From { get; }

        /// <summary>Replacement text.</summary>
        public string To { get; }

        /// <summary>True to replace every occurrence.</summary>
        public bool All { get; }

        /// <summary>Bundles the values.</summary>
        public DataEdit(string file, string from, string to, bool all = false)
        {
            File = file;
            From = from;
            To = to;
            All = all;
        }
    }

    /// <summary>Temporary data folders and harness invocations.</summary>
    public static class HarnessTestSupport
    {
        /// <summary>Temporary folders created by the tests, deleted by <see cref="Cleanup"/>.</summary>
        private static readonly List<string> Folders = new List<string>();

        /// <summary>Hit tuning that keeps the batch tests quick: floor trials stop after the 30 s minimum and chunks hold one game each.</summary>
        public static readonly DataEdit[] QuickBatch =
        {
            new DataEdit("tuning/harness_batch.json", "\"hq_floor_max_test_seconds\": 300", "\"hq_floor_max_test_seconds\": 30"),
            new DataEdit("tuning/harness_batch.json", "\"chunk_games\": 25", "\"chunk_games\": 1"),
        };

        /// <summary>A new empty temporary folder, registered for cleanup.</summary>
        public static string NewFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "rts_harness_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Folders.Add(folder);
            return folder;
        }

        /// <summary>Deletes every folder made by <see cref="NewFolder"/>.</summary>
        public static void Cleanup()
        {
            foreach (string folder in Folders)
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (IOException)
                {
                    // Best effort: a leftover temp folder is harmless.
                }
            }
            Folders.Clear();
        }

        /// <summary>Copies rts/data to a new temporary folder and applies the edits; returns the folder. Fails the test when an edit's text is not found.</summary>
        /// <param name="edits">Replacements, applied in order.</param>
        public static string CopyData(params DataEdit[][] edits)
        {
            string source = TestData.FindDataDirectory();
            string target = Path.Combine(NewFolder(), "data");
            foreach (string path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(target, Path.GetRelativePath(source, path));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(path, destination);
            }
            foreach (DataEdit[] group in edits)
            {
                foreach (DataEdit edit in group)
                {
                    string file = Path.Combine(target, edit.File);
                    string text = File.ReadAllText(file);
                    Assert.That(text, Does.Contain(edit.From), edit.File + " must contain the text to replace");
                    if (edit.All)
                    {
                        text = text.Replace(edit.From, edit.To);
                    }
                    else
                    {
                        int at = text.IndexOf(edit.From, StringComparison.Ordinal);
                        text = text.Substring(0, at) + edit.To + text.Substring(at + edit.From.Length);
                    }
                    File.WriteAllText(file, text);
                }
            }
            return target;
        }

        /// <summary>Loads the data set of a data folder.</summary>
        public static DataSet Load(string folder)
        {
            List<DataSourceFile> files = new List<DataSourceFile>();
            string[] paths = Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            foreach (string path in paths)
            {
                files.Add(new DataSourceFile(Path.GetRelativePath(folder, path).Replace('\\', '/'), File.ReadAllText(path)));
            }
            return DataLoader.Load(files);
        }

        /// <summary>Runs the harness with the arguments and returns the exit code, stdout and stderr.</summary>
        public static int Run(string[] args, out string output, out string error)
        {
            StringWriter outWriter = new StringWriter();
            StringWriter errWriter = new StringWriter();
            int exit = HarnessProgram.Run(args, outWriter, errWriter);
            output = outWriter.ToString().Replace("\r\n", "\n");
            error = errWriter.ToString().Replace("\r\n", "\n");
            return exit;
        }
    }
}

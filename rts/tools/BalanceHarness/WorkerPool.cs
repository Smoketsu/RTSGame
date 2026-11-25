// rts/tools/BalanceHarness/WorkerPool.cs
// Starts worker processes of the batch harness (this same assembly run with --worker), captures their stdout and stderr and runs a list of
// them with at most a given number at once (spec: rts/docs/specs/balance_harness.md section 4). Results come back indexed by job, so the order of
// completion never matters. Processes, threads and the processor count are tool-side only; nothing here touches a simulation. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>What one worker process produced.</summary>
    public sealed class WorkerResult
    {
        /// <summary>Process exit code. Range: any int.</summary>
        public int ExitCode { get; }

        /// <summary>Non-empty stdout lines in order.</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>Everything the process wrote to stderr.</summary>
        public string Error { get; }

        /// <summary>Bundles the values.</summary>
        public WorkerResult(int exitCode, IReadOnlyList<string> lines, string error)
        {
            ExitCode = exitCode;
            Lines = lines;
            Error = error;
        }
    }

    /// <summary>Runs worker processes.</summary>
    public static class WorkerPool
    {
        /// <summary>Path of the .NET host that runs the harness assembly: the current process when it is dotnet, else DOTNET_HOST_PATH, else "dotnet" from PATH.</summary>
        private static string HostPath()
        {
            // Path of this process's executable, or empty.
            string current = Environment.ProcessPath ?? string.Empty;
            if (Path.GetFileNameWithoutExtension(current) == "dotnet")
            {
                return current;
            }
            // Host path the dotnet CLI exports to the programs it starts, or null.
            string? fromEnvironment = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            return string.IsNullOrEmpty(fromEnvironment) ? "dotnet" : fromEnvironment;
        }

        /// <summary>Runs one worker with <paramref name="arguments"/> (after "--worker") and waits for it.</summary>
        /// <param name="arguments">Worker arguments, each one argv entry.</param>
        public static WorkerResult RunOne(IReadOnlyList<string> arguments)
        {
            // Resume support: RTS_WORKER_CACHE names a folder where each finished worker's stdout lines are kept, one file per distinct argument list; a later run with the same arguments reads them
            // instead of playing again. Unset: no caching. Tool-side only, no effect on any record. Unit: folder path.
            string? cacheFolder = Environment.GetEnvironmentVariable("RTS_WORKER_CACHE");
            // Cache file of this worker call, or null without a cache folder. Unit: file path.
            string? cacheFile = null;
            if (!string.IsNullOrEmpty(cacheFolder))
            {
                // SHA-256 of the argument list, hex, as the file name. Range: 64 hex characters.
                string key = BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", arguments)))).Replace("-", string.Empty);
                Directory.CreateDirectory(cacheFolder);
                cacheFile = Path.Combine(cacheFolder, key + ".jsonl");
                if (File.Exists(cacheFile))
                {
                    return new WorkerResult(0, File.ReadAllLines(cacheFile), string.Empty);
                }
            }
            WorkerResult result = RunOneUncached(arguments);
            if (cacheFile != null && result.ExitCode == 0)
            {
                // Written to a temporary name first so a kill never leaves a half file under the real name.
                File.WriteAllLines(cacheFile + ".tmp", result.Lines);
                File.Move(cacheFile + ".tmp", cacheFile);
            }
            return result;
        }

        /// <summary>Plays one worker for real (the body of <see cref="RunOne"/> without the cache).</summary>
        /// <param name="arguments">Worker arguments, each one argv entry.</param>
        private static WorkerResult RunOneUncached(IReadOnlyList<string> arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo(HostPath())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            info.ArgumentList.Add(typeof(WorkerPool).Assembly.Location);
            info.ArgumentList.Add("--worker");
            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }
            // The non-empty stdout lines, filled by the reader thread's event.
            List<string> lines = new List<string>();
            // Stderr text, filled the same way.
            System.Text.StringBuilder error = new System.Text.StringBuilder();
            using (Process process = new Process { StartInfo = info })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (lines)
                        {
                            lines.Add(e.Data);
                        }
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        lock (error)
                        {
                            error.Append(e.Data).Append('\n');
                        }
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return new WorkerResult(process.ExitCode, lines, error.ToString());
            }
        }

        /// <summary>
        /// Runs every job (a worker argument list) with at most <paramref name="parallel"/> processes at once and returns the results in job order.
        /// <paramref name="onDone"/> is called, one call at a time, with the job index each time a job finishes.
        /// </summary>
        /// <param name="jobs">The worker argument lists.</param>
        /// <param name="parallel">Most processes at once. Unit: processes. Range: 1 to 64.</param>
        /// <param name="onDone">Callback per finished job; may be null.</param>
        public static WorkerResult[] RunAll(IReadOnlyList<IReadOnlyList<string>> jobs, int parallel, Action<int, WorkerResult[]>? onDone)
        {
            WorkerResult[] results = new WorkerResult[jobs.Count];
            // Index of the next job to start. Unit: job number. Range: 0 to jobs.Count.
            int next = -1;
            // Lock for the callback.
            object gate = new object();
            // Thread loop: take the next job until none is left.
            ThreadStart body = () =>
            {
                while (true)
                {
                    // Job this thread takes. Unit: job number.
                    int index = Interlocked.Increment(ref next);
                    if (index >= jobs.Count)
                    {
                        return;
                    }
                    WorkerResult result = RunOne(jobs[index]);
                    lock (gate)
                    {
                        results[index] = result;
                        onDone?.Invoke(index, results);
                    }
                }
            };
            List<Thread> threads = new List<Thread>();
            for (int t = 0; t < Math.Min(parallel, jobs.Count); t++)
            {
                Thread thread = new Thread(body);
                thread.Start();
                threads.Add(thread);
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }
            return results;
        }
    }
}

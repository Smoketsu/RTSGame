// rts/tools/BalanceHarness/FightCoordinator.cs
// Entry points of the equal-value fight mode (spec: rts/docs/specs/micro_benchmark.md sections 4.6 and 5): --fights --only plays one fight in-process and prints its JSON line;
// --worker --fights plays a chunk of the plan or one HQ floor trial. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Rts.Sim.Data;
using Rts.Sim.Factions;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Coordinator and worker of the fight mode.</summary>
    public static class FightCoordinator
    {
        /// <summary>Runs the fight mode for parsed options (--fights, with or without --worker or --only).</summary>
        /// <param name="options">Parsed options.</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="output">stdout.</param>
        /// <param name="error">stderr.</param>
        public static int Run(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            FightSettings settings = FightSettings.Load(options.DataDirectory, data);
            // Base seed after the command-line override. Range: any ulong.
            ulong baseSeed = options.Seed ?? settings.BaseSeed;
            if (options.IsWorker)
            {
                return RunWorker(options, data, settings, FightPlan.Build(data, settings), baseSeed, output);
            }
            // Parsed for its validation here; the checks read it in the full sweep.
            FightMatchups matchups = FightMatchups.Load(options.DataDirectory, settings, data);
            FightPlan plan = FightPlan.Build(data, settings);
            if (options.Only.Length > 0)
            {
                foreach (FightSpec spec in plan.Fights)
                {
                    if (spec.Id == options.Only)
                    {
                        output.Write(FightRunner.Run(data, settings, spec, baseSeed).Record.ToJsonLine() + "\n");
                        output.Flush();
                        return 0;
                    }
                }
                throw new HarnessException("--only: no fight '" + options.Only + "' in the plan");
            }
            return RunSweep(options, data, settings, matchups, plan, baseSeed, output, error);
        }

        /// <summary>
        /// The coordinator (spec 4.6): plans every fight, runs chunks of the plan and the HQ floor trials in worker processes, sorts the records into plan order, re-runs the first
        /// fights in-process, runs the checks and writes fights.jsonl, hq_floor.jsonl and fights.md. Exit codes: 0 done, 1 error, 2 a check failed with --strict, 3 spot check failed.
        /// </summary>
        private static int RunSweep(BatchOptions options, DataSet data, FightSettings settings, FightMatchups matchups, FightPlan plan, ulong baseSeed, TextWriter output, TextWriter error)
        {
            // Wall clock for the elapsed line on stderr only (tool code, never fed to a simulation).
            Stopwatch clock = Stopwatch.StartNew();
            // Worker processes at once. Unit: processes. Range: 1 to 64.
            int jobs = options.Jobs ?? Math.Min(64, Math.Max(1, Environment.ProcessorCount));
            // Common worker arguments.
            List<string> common = new List<string> { "--data", options.DataDirectory, "--seed", baseSeed.ToString(CultureInfo.InvariantCulture), "--fights" };
            // HQ floor trials: every ordered faction pair with an HQ, units then compositions.
            SortedSet<string> factions = new SortedSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, FactionDefinition> pair in data.Content.Factions)
            {
                if (pair.Value.HqUnitId != null)
                {
                    factions.Add(pair.Key);
                }
            }
            List<string> pairs = new List<string>();
            foreach (string a in factions)
            {
                foreach (string b in factions)
                {
                    if (string.CompareOrdinal(a, b) <= 0)
                    {
                        pairs.Add(a + "_vs_" + b);
                    }
                }
            }
            List<string> warnings = new List<string>();
            List<HqFloorSpec> specs = HqFloorTrial.Plan(data, pairs, plan.Sides, warnings);
            foreach (string warning in warnings)
            {
                error.Write("warning: " + warning + "\n");
            }
            // Chunks of the plan as (first fight, count), then the floor trials: the job list for the pool.
            List<int[]> chunks = new List<int[]>();
            List<IReadOnlyList<string>> jobList = new List<IReadOnlyList<string>>();
            for (int first = 0; first < plan.Fights.Count; first += settings.ChunkFights)
            {
                int count = Math.Min(settings.ChunkFights, plan.Fights.Count - first);
                chunks.Add(new[] { first, count });
                jobList.Add(new List<string>(common) { "--first", first.ToString(CultureInfo.InvariantCulture), "--count", count.ToString(CultureInfo.InvariantCulture) });
            }
            foreach (HqFloorSpec spec in specs)
            {
                jobList.Add(new List<string>(common) { "--hq-floor", spec.ToArgument() });
            }
            // Progress lines are printed in chunk order once the chunk and every chunk before it are done, so stdout never depends on scheduling.
            bool[] finished = new bool[jobList.Count];
            int printed = 0;
            WorkerResult[] results = WorkerPool.RunAll(jobList, jobs, (index, all) =>
            {
                finished[index] = true;
                while (printed < chunks.Count && finished[printed])
                {
                    output.Write("fights " + (chunks[printed][0] + chunks[printed][1]).ToString(CultureInfo.InvariantCulture) + "/" + plan.Fights.Count.ToString(CultureInfo.InvariantCulture) + "\n");
                    printed++;
                }
            });
            List<FightRecord> records = new List<FightRecord>();
            for (int c = 0; c < chunks.Count; c++)
            {
                records.AddRange(ReadFights(results[c], plan, chunks[c][0], chunks[c][1]));
            }
            List<HqFloorRecord> floors = new List<HqFloorRecord>();
            for (int s = 0; s < specs.Count; s++)
            {
                WorkerResult result = results[chunks.Count + s];
                BatchRunner.CheckWorker(result, "HQ floor trial " + specs[s].ToArgument());
                if (result.Lines.Count != 1)
                {
                    throw new HarnessException("HQ floor trial " + specs[s].ToArgument() + " returned " + result.Lines.Count.ToString(CultureInfo.InvariantCulture) + " lines, expected 1");
                }
                floors.Add(HqFloorRecord.Parse(result.Lines[0]));
            }
            floors.Sort(BatchRunner.CompareFloors);

            // Determinism spot check: the first fights of the plan run again in this process; the JSON lines must be identical.
            int verify = Math.Min(settings.VerifyFights, plan.Fights.Count);
            int mismatches = 0;
            for (int i = 0; i < verify; i++)
            {
                if (FightRunner.Run(data, settings, plan.Fights[i], baseSeed).Record.ToJsonLine() != records[i].ToJsonLine())
                {
                    mismatches++;
                }
            }

            List<CheckResult> checks = FightChecks.RunAll(records, floors, settings, matchups, data);
            FightSummary summary = new FightSummary(records, checks);
            // Output folder: --out, or the fights subfolder of the default harness folder.
            string outDirectory = options.OutDirectory == BatchOptions.DefaultOutDirectory ? Path.Combine(BatchOptions.DefaultOutDirectory, "fights") : options.OutDirectory;
            Directory.CreateDirectory(outDirectory);
            StringBuilder fightLines = new StringBuilder();
            foreach (FightRecord record in records)
            {
                fightLines.Append(record.ToJsonLine()).Append('\n');
            }
            StringBuilder floorLines = new StringBuilder();
            foreach (HqFloorRecord record in floors)
            {
                floorLines.Append(record.ToJsonLine()).Append('\n');
            }
            File.WriteAllText(Path.Combine(outDirectory, "fights.jsonl"), fightLines.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDirectory, "hq_floor.jsonl"), floorLines.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDirectory, "fights.md"),
                FightReport.Build(data, settings, plan, matchups, records, floors, checks, summary, baseSeed, verify, mismatches), new UTF8Encoding(false));
            foreach (CheckResult check in checks)
            {
                output.Write("CHECK " + check.Name + " " + (check.Pass ? "PASS" : "FAIL") + " " + (check.Detail.Count > 0 ? check.Detail[0] : string.Empty) + "\n");
            }
            output.Write(summary.Line() + "\n");
            output.Flush();
            error.Write("elapsed " + clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s\n");
            if (mismatches > 0)
            {
                return 3;
            }
            return options.Strict && summary.ChecksPassed < summary.ChecksTotal ? 2 : 0;
        }

        /// <summary>Reads and checks the fight records of one worker call: exit code 0, the expected count and the expected fight IDs in order.</summary>
        private static List<FightRecord> ReadFights(WorkerResult result, FightPlan plan, int first, int count)
        {
            // What the worker was asked for, for messages.
            string what = "fights " + first.ToString(CultureInfo.InvariantCulture) + "+" + count.ToString(CultureInfo.InvariantCulture);
            BatchRunner.CheckWorker(result, what);
            if (result.Lines.Count != count)
            {
                throw new HarnessException("worker for " + what + " returned " + result.Lines.Count.ToString(CultureInfo.InvariantCulture) + " lines, expected " + count.ToString(CultureInfo.InvariantCulture)
                    + (result.Error.Length > 0 ? ": " + result.Error.Trim() : string.Empty));
            }
            List<FightRecord> list = new List<FightRecord>();
            for (int i = 0; i < count; i++)
            {
                FightRecord record = FightRecord.Parse(result.Lines[i]);
                if (record.Fight != plan.Fights[first + i].Id)
                {
                    throw new HarnessException("worker for " + what + " returned a record for another fight: " + result.Lines[i]);
                }
                list.Add(record);
            }
            return list;
        }

        /// <summary>One worker call: a chunk of the plan, one JSON line per fight, or one HQ floor trial.</summary>
        private static int RunWorker(BatchOptions options, DataSet data, FightSettings settings, FightPlan plan, ulong baseSeed, TextWriter output)
        {
            if (options.WorkerHqFloor.Length > 0)
            {
                HqFloorSpec spec = HqFloorSpec.Parse(options.WorkerHqFloor);
                // Recipe of the composition named by the trial, or null for a unit trial.
                IReadOnlyDictionary<string, int>? recipe = null;
                foreach (FightComposition composition in settings.Compositions)
                {
                    if (composition.Id == spec.Unit)
                    {
                        recipe = composition.Units;
                    }
                }
                output.Write(HqFloorTrial.Run(data, spec, recipe).ToJsonLine() + "\n");
                output.Flush();
                return 0;
            }
            if (options.WorkerFirst + options.WorkerCount > plan.Fights.Count)
            {
                throw new HarnessException("fight worker asked for fights " + options.WorkerFirst.ToString(CultureInfo.InvariantCulture) + "+" + options.WorkerCount.ToString(CultureInfo.InvariantCulture)
                    + " but the plan has " + plan.Fights.Count.ToString(CultureInfo.InvariantCulture));
            }
            for (int i = options.WorkerFirst; i < options.WorkerFirst + options.WorkerCount; i++)
            {
                output.Write(FightRunner.Run(data, settings, plan.Fights[i], baseSeed).Record.ToJsonLine() + "\n");
                output.Flush();
            }
            return 0;
        }
    }
}

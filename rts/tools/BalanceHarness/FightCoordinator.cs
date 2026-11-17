// rts/tools/BalanceHarness/FightCoordinator.cs
// Entry points of the equal-value fight mode (spec: rts/docs/specs/micro_benchmark.md sections 4.6 and 5): --fights --only plays one fight in-process and prints its JSON line;
// --worker --fights plays a chunk of the plan or one HQ floor trial. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Sim.Data;

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
            throw new HarnessException("the full sweep is not built yet");
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

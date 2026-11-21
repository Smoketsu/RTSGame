// rts/tools/MissionRunner/MissionPlay.cs
// The headless host loop for one mission (rts/docs/specs/campaign_scripting.md section 4): build the simulation from the mission, start it and the runner,
// attach the AI controllers of "ai:" slots, then before every step let the scripted inputs and the AIs act, step, and let the runner read the result. Prints
// the runner's log lines and one "tick=<n> hash=<16 hex>" line per state hash as they happen. Used by Program and by the tests, so both play missions the same
// way. Part of Rts.Tools.MissionRunner. The loop never mutates the simulation except through Simulation.Step and Submit.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Ai;
using Rts.Campaign;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;

namespace Rts.Tools.MissionTool
{
    /// <summary>What a played mission ended with.</summary>
    public sealed class MissionResult
    {
        /// <summary>The runner after the last step (status, reason, log, objectives).</summary>
        public MissionRunner Runner { get; }

        /// <summary>The simulation after the last step.</summary>
        public Simulation Sim { get; }

        /// <summary>True when the run stopped at the tick cap with the mission still running (reported as a timeout).</summary>
        public bool CappedByTicks { get; }

        /// <summary>The last tick stepped. Unit: ticks. Range: -1 when no step ran, else 0 upward.</summary>
        public int LastTick { get; }

        /// <summary>Bundles the result.</summary>
        public MissionResult(MissionRunner runner, Simulation sim, bool cappedByTicks, int lastTick)
        {
            Runner = runner;
            Sim = sim;
            CappedByTicks = cappedByTicks;
            LastTick = lastTick;
        }

        /// <summary>Process exit code: 0 won, 2 lost or timed out.</summary>
        public int ExitCode => Runner.Status == MissionStatus.Won ? 0 : 2;
    }

    /// <summary>Plays missions headless.</summary>
    public static class MissionPlay
    {
        /// <summary>
        /// Plays a mission until it is won or lost, or <paramref name="tickCap"/> steps have run. Writes the log lines and hash lines to
        /// <paramref name="output"/>; the caller writes the final STATE_HASH line.
        /// </summary>
        /// <param name="data">The loaded game data.</param>
        /// <param name="mission">The validated mission.</param>
        /// <param name="inputs">The scripted player, or null when nobody plays the human slot.</param>
        /// <param name="tickCap">Most steps to run, or null for the mission's own time limit. Unit: ticks. Range: 0 upward.</param>
        /// <param name="output">Receives the lines.</param>
        public static MissionResult Play(DataSet data, MissionDefinition mission, MissionInputs? inputs, int? tickCap, TextWriter output)
        {
            Simulation sim = mission.CreateSimulation(data);
            sim.Start();
            MissionRunner runner = new MissionRunner(mission, null);
            runner.Start(sim);
            // AI controllers of the "ai:" slots, ascending slot order (the mission lists them in file order; AiHost sorts them again).
            List<IPlayerController> controllers = new List<IPlayerController>();
            foreach (MissionPlayer player in mission.Players)
            {
                if (player.AiProfile != null)
                {
                    controllers.Add(new SkirmishAiController(player.Setup.Id, AiProfile.FromDataSet(data, player.AiProfile)));
                }
            }
            AiHost.StartAll(sim, controllers);
            // Log lines already written to the output. Unit: lines.
            int written = Flush(runner, 0, output);
            // Last tick stepped. Unit: ticks. Range: -1 before the first step.
            int lastTick = -1;
            // Steps run so far. Unit: ticks.
            int steps = 0;
            while (runner.Status == MissionStatus.Running && (tickCap == null || steps < tickCap.Value))
            {
                inputs?.BeforeStep(sim, runner);
                AiHost.ThinkAll(sim, controllers);
                lastTick = sim.Clock.Tick;
                sim.Step();
                steps++;
                foreach (SimEvent simEvent in sim.Events.Current)
                {
                    if (simEvent is StateHashedEvent hashed)
                    {
                        output.Write("tick=" + hashed.Tick.ToString(CultureInfo.InvariantCulture) + " hash=" + hashed.Hash.ToString("x16", CultureInfo.InvariantCulture) + "\n");
                    }
                }
                runner.AfterStep(sim);
                written = Flush(runner, written, output);
            }
            // True when the cap, not the mission, ended the run.
            bool capped = runner.Status == MissionStatus.Running;
            if (capped)
            {
                output.Write("MISSION timeout tick=" + lastTick.ToString(CultureInfo.InvariantCulture) + "\n");
            }
            return new MissionResult(runner, sim, capped, lastTick);
        }

        /// <summary>Writes the runner's log lines from index <paramref name="from"/> and returns the new line count. Unit: lines.</summary>
        private static int Flush(MissionRunner runner, int from, TextWriter output)
        {
            for (int i = from; i < runner.Log.Count; i++)
            {
                output.Write(runner.Log[i] + "\n");
            }
            return runner.Log.Count;
        }
    }
}

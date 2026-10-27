// rts/src/Ai/CommandSinks.cs
// Two ICommandSink implementations: one that submits straight into a local simulation (the harness, tests and single-player
// Unity) and one that only collects (tests, and hosts that forward commands over the network themselves). Also AiHost, the
// helper that runs every controller in ascending player order before a step. Part of Rts.Ai.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Ai
{
    /// <summary>Submits each command to a local simulation with <see cref="Simulation.Submit"/>, scheduled for the current tick.</summary>
    public sealed class SimulationCommandSink : ICommandSink
    {
        /// <summary>The simulation commands go to.</summary>
        private readonly Simulation _sim;

        /// <summary>Extra ticks added to the current tick (lockstep input delay; 0 for local play). Unit: ticks. Range: 0 upward.</summary>
        private readonly int _delayTicks;

        /// <summary>Wraps <paramref name="sim"/>; commands are scheduled <paramref name="delayTicks"/> after the current tick.</summary>
        /// <param name="sim">The local simulation.</param>
        /// <param name="delayTicks">Input delay. Unit: ticks. Range: 0 upward.</param>
        public SimulationCommandSink(Simulation sim, int delayTicks = 0)
        {
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            if (delayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayTicks));
            }
            _delayTicks = delayTicks;
        }

        /// <summary>The current tick plus the delay. Unit: ticks.</summary>
        public int ScheduleTick => _sim.Clock.Tick + _delayTicks;

        /// <summary>Queues the command in the simulation.</summary>
        /// <param name="command">The command.</param>
        public void Submit(Command command)
        {
            _sim.Submit(command);
        }
    }

    /// <summary>Collects commands in submission order without touching any simulation.</summary>
    public sealed class ListCommandSink : ICommandSink
    {
        /// <summary>Commands received, in submission order.</summary>
        public List<Command> Commands { get; } = new List<Command>();

        /// <summary>Tick reported to controllers. Unit: ticks. Range: 0 upward.</summary>
        public int ScheduleTick { get; set; }

        /// <summary>Creates a sink that reports <paramref name="scheduleTick"/>.</summary>
        /// <param name="scheduleTick">Tick for the commands. Unit: ticks.</param>
        public ListCommandSink(int scheduleTick)
        {
            ScheduleTick = scheduleTick;
        }

        /// <summary>Stores the command.</summary>
        /// <param name="command">The command.</param>
        public void Submit(Command command)
        {
            Commands.Add(command ?? throw new ArgumentNullException(nameof(command)));
        }
    }

    /// <summary>Host helper: runs controllers before a step in ascending player order (spec section 4, host loop).</summary>
    public static class AiHost
    {
        /// <summary>
        /// Calls <see cref="IPlayerController.Think"/> on every controller in ascending <see cref="PlayerId"/> order, each submitting
        /// straight into <paramref name="sim"/> for the current tick. Call it before every <see cref="Simulation.Step"/>.
        /// </summary>
        /// <param name="sim">The simulation between steps.</param>
        /// <param name="controllers">The controllers; any order, at most one per player.</param>
        public static void ThinkAll(Simulation sim, IReadOnlyList<IPlayerController> controllers)
        {
            // Controllers keyed by player so they run in ascending slot order whatever order the host listed them in.
            SortedDictionary<PlayerId, IPlayerController> ordered = new SortedDictionary<PlayerId, IPlayerController>();
            foreach (IPlayerController controller in controllers)
            {
                if (ordered.ContainsKey(controller.Player))
                {
                    throw new ArgumentException("Two controllers play " + controller.Player + ".", nameof(controllers));
                }
                ordered.Add(controller.Player, controller);
            }
            // Sink shared by the controllers: each schedules for the current tick.
            SimulationCommandSink sink = new SimulationCommandSink(sim);
            foreach (IPlayerController controller in ordered.Values)
            {
                controller.Think(sim, sink);
            }
        }

        /// <summary>Calls <see cref="IPlayerController.OnMatchStart"/> on every controller in ascending player order.</summary>
        /// <param name="sim">The started simulation.</param>
        /// <param name="controllers">The controllers.</param>
        public static void StartAll(Simulation sim, IReadOnlyList<IPlayerController> controllers)
        {
            // Controllers in ascending slot order.
            SortedDictionary<PlayerId, IPlayerController> ordered = new SortedDictionary<PlayerId, IPlayerController>();
            foreach (IPlayerController controller in controllers)
            {
                ordered.Add(controller.Player, controller);
            }
            foreach (IPlayerController controller in ordered.Values)
            {
                controller.OnMatchStart(sim);
            }
        }
    }
}

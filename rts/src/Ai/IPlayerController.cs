// rts/src/Ai/IPlayerController.cs
// The host-side player controller contract and the command sink it writes to. Part of Rts.Ai (spec: rts/docs/specs/skirmish_ai.md
// section 6). A controller plays one player slot from outside the simulation: the host (harness, Unity, campaign) calls Think
// between steps, never from inside Simulation.Step, and the controller submits ordinary commands (architecture rule 2) through the
// sink. Decision: controllers SUBMIT through an ICommandSink rather than returning a list, so the host picks the scheduling tick
// (harness: the current tick; lockstep online: plus the input delay) without the controller knowing about the network.
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Ai
{
    /// <summary>Receives the commands a controller decides on. Implemented by hosts; see <see cref="SimulationCommandSink"/>.</summary>
    public interface ICommandSink
    {
        /// <summary>Tick the controller must schedule its commands for. Unit: ticks. Range: the simulation's current tick or later.</summary>
        int ScheduleTick { get; }

        /// <summary>Accepts one command, already stamped with <see cref="ScheduleTick"/>, the controller's player and its sequence number.</summary>
        /// <param name="command">The command; not null.</param>
        void Submit(Command command);
    }

    /// <summary>
    /// Plays one player slot from the host. Must not call <see cref="Simulation.Step"/>, mutation methods or <see cref="Simulation.Rng"/>,
    /// must read enemy units only through the team's vision, and keeps all its memory in the instance (no static state).
    /// </summary>
    public interface IPlayerController
    {
        /// <summary>The player slot this controller plays.</summary>
        PlayerId Player { get; }

        /// <summary>Called once after <see cref="Simulation.Start"/> and before the first <see cref="Think"/>.</summary>
        /// <param name="sim">The started simulation (read only).</param>
        void OnMatchStart(Simulation sim);

        /// <summary>
        /// Called by the host before every <see cref="Simulation.Step"/>. Reads the events of the last stepped tick and, on its
        /// think ticks, submits commands to <paramref name="sink"/>.
        /// </summary>
        /// <param name="sim">The simulation between steps (read only).</param>
        /// <param name="sink">Where the commands go.</param>
        void Think(Simulation sim, ICommandSink sink);
    }
}

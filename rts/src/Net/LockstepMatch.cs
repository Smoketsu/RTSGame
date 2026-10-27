// rts/src/Net/LockstepMatch.cs
// What the host's match factory hands a LockstepPeer at Start and on every reconnect: a started simulation at tick 0
// with nothing queued, and the setup commands (the match script's spawns) that turn it into the match. Part of Rts.Net
// (ADR-008). The peer submits the setup commands itself and reports each through LockstepPeer.CommandSubmitted, after
// raising LockstepPeer.SimulationCreated on the empty simulation; a host-side replay recorder (Rts.Replay, ADR-009)
// attached in SimulationCreated therefore sees every command of the match, setup included. The simulation API cannot
// enumerate queued commands, so a factory that queued them itself would leave them out of every online replay; the
// constructor refuses such a simulation.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;

namespace Rts.Net
{
    /// <summary>A fresh match for one peer: the empty started simulation and the commands that set it up.</summary>
    public sealed class LockstepMatch
    {
        /// <summary>The simulation: started, at tick 0, with no command queued.</summary>
        public Simulation Simulation { get; }

        /// <summary>The setup commands the peer submits before stepping, in submission order; each at a tick of 0 or more. Range: zero or more.</summary>
        public IReadOnlyList<Command> SetupCommands { get; }

        /// <summary>Builds the match description, refusing a simulation that is not started, not at tick 0, or already holds queued commands.</summary>
        /// <param name="simulation">The started, empty simulation; not null.</param>
        /// <param name="setupCommands">The setup commands; not null, may be empty.</param>
        public LockstepMatch(Simulation simulation, IReadOnlyList<Command> setupCommands)
        {
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            SetupCommands = setupCommands ?? throw new ArgumentNullException(nameof(setupCommands));
            if (!simulation.IsStarted)
            {
                throw new ArgumentException("The match factory must return a started simulation.", nameof(simulation));
            }
            if (simulation.Clock.Tick != 0)
            {
                throw new ArgumentException("The match factory must return a simulation at tick 0, not tick " + simulation.Clock.Tick + ".", nameof(simulation));
            }
            if (simulation.Commands.PendingCount != 0)
            {
                throw new ArgumentException("The match factory must not queue commands itself (" + simulation.Commands.PendingCount
                    + " pending); list them as setup commands so the peer submits and reports them.", nameof(simulation));
            }
        }
    }
}

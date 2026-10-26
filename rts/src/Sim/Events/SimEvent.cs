// rts/src/Sim/Events/SimEvent.cs
// Base type for everything the simulation reports, plus the per-tick event log.
// Part of Rts.Sim.Events. Rule 8 of the architecture: events go out, never in. Listeners (renderer,
// audio, telemetry, missions) read these and may never change simulation state through them.
using System;
using System.Collections.Generic;

namespace Rts.Sim.Events
{
    /// <summary>One thing that happened on one tick. Immutable; subclasses add the details.</summary>
    public abstract class SimEvent
    {
        /// <summary>Tick on which the event happened. Range: 0 upward.</summary>
        public int Tick { get; }

        /// <summary>Short machine-readable name such as "unit_spawned", for logs and tests.</summary>
        public abstract string Kind { get; }

        /// <summary>Stores the tick; subclasses pass it through.</summary>
        /// <param name="tick">Tick of the event, 0 or more.</param>
        protected SimEvent(int tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }
            Tick = tick;
        }

        /// <summary>One-line human-readable description for logs.</summary>
        public abstract string Describe();

        /// <summary>Same as <see cref="Describe"/>, prefixed with the tick.</summary>
        public override string ToString()
        {
            return "[t" + Tick + "] " + Describe();
        }
    }

    /// <summary>
    /// The events of the tick most recently stepped. The simulation clears it at the start of every step and
    /// appends as things happen; hosts read it after <see cref="Simulation.Step"/> returns.
    /// </summary>
    public sealed class EventLog
    {
        /// <summary>Backing list for the current tick's events, in the order they happened.</summary>
        private readonly List<SimEvent> _events = new List<SimEvent>();

        /// <summary>Events of the current tick, oldest first. Read-only view; valid until the next step.</summary>
        public IReadOnlyList<SimEvent> Current => _events;

        /// <summary>Number of events recorded over the whole match so far. Range: 0 upward. Not part of the state hash.</summary>
        public long TotalCount { get; private set; }

        /// <summary>Appends an event. Only the simulation and its systems call this.</summary>
        internal void Add(SimEvent evt)
        {
            _events.Add(evt);
            TotalCount++;
        }

        /// <summary>Forgets the current tick's events. Called by the simulation at the start of each step.</summary>
        internal void Clear()
        {
            _events.Clear();
        }
    }
}

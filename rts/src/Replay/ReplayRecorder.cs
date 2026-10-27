// rts/src/Replay/ReplayRecorder.cs
// Records a match as a ReplayData while a host plays it. Part of Rts.Replay. A host (the headless harness, the Unity SimulationHost,
// a lockstep client) attaches one recorder to a started simulation at tick 0 and then routes every command through it and every
// step through it. The simulation exposes no "command applied" hook (events out, never in), so the recorder sits on the host side:
// Submit records the command and forwards it to Simulation.Submit, and Step steps the simulation and reads the StateHashedEvent
// checkpoints. Commands from the AI, the network or a campaign script are recorded the same way because they all arrive through
// Submit. The simulation cannot enumerate commands already queued, so the recorder refuses a simulation that holds any: a host
// must attach it before the setup commands go in (a lockstep peer raises SimulationCreated for exactly that moment, then
// reports every command it submits through CommandSubmitted, which the host passes to Record). Host-side only: the recorder
// never runs inside Simulation.Step and never changes simulation state except through Submit and Step.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Events;
using Rts.Sim.Teams;

namespace Rts.Replay
{
    /// <summary>Collects the commands and hash checkpoints of one match and builds the <see cref="ReplayData"/> at the end.</summary>
    public sealed class ReplayRecorder
    {
        /// <summary>The simulation being recorded.</summary>
        private readonly Simulation _sim;

        /// <summary>The player setup the match was built with, copied at construction.</summary>
        private readonly List<PlayerSetup> _players;

        /// <summary>Every command submitted through the recorder, in submission order. Range: 0 upward.</summary>
        private readonly List<Command> _commands = new List<Command>();

        /// <summary>Hash checkpoints seen so far, ascending by tick.</summary>
        private readonly List<ReplayCheckpoint> _checkpoints = new List<ReplayCheckpoint>();

        /// <summary>Number of commands recorded so far (including any scheduled beyond the last tick that <see cref="Finish"/> will drop). Range: 0 upward.</summary>
        public int CommandCount => _commands.Count;

        /// <summary>Attaches to a started simulation that has not stepped yet and holds no queued command (a queued command could not be recorded and the replay would desync).</summary>
        /// <param name="sim">The simulation: started, at tick 0, nothing queued.</param>
        /// <param name="players">The player setup the simulation was built with. Range: at least one player.</param>
        public ReplayRecorder(Simulation sim, IReadOnlyList<PlayerSetup> players)
        {
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }
            if (!sim.IsStarted)
            {
                throw new InvalidOperationException("Start the simulation before attaching a replay recorder (the content digest exists only after Start).");
            }
            if (sim.Clock.Tick != 0)
            {
                throw new InvalidOperationException("A replay records a match from tick 0; the simulation is already at tick " + sim.Clock.Tick + ".");
            }
            if (sim.Commands.PendingCount != 0)
            {
                throw new InvalidOperationException("The simulation already holds " + sim.Commands.PendingCount
                    + " queued commands the recorder cannot see; attach the recorder before submitting the setup commands and submit them through it.");
            }
            _players = new List<PlayerSetup>(players);
        }

        /// <summary>Records the command, then queues it in the simulation with <see cref="Simulation.Submit"/> (which throws for a tick in the past; nothing is recorded then).</summary>
        /// <param name="command">The command; not null.</param>
        public void Submit(Command command)
        {
            _sim.Submit(command);
            _commands.Add(command);
        }

        /// <summary>
        /// Records a command the host has already queued itself (for example through a lockstep layer that calls Simulation.Submit directly).
        /// Throws when the command's tick is already behind the clock, because it can no longer have been applied.
        /// </summary>
        /// <param name="command">The command; not null.</param>
        public void Record(Command command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if (command.Tick < _sim.Clock.Tick)
            {
                throw new InvalidOperationException("Command " + command + " is behind the clock (tick " + _sim.Clock.Tick + ") and cannot be recorded.");
            }
            _commands.Add(command);
        }

        /// <summary>Steps the simulation one tick and collects the tick's hash checkpoint, if it has one.</summary>
        public void Step()
        {
            _sim.Step();
            AfterStep();
        }

        /// <summary>
        /// Collects the checkpoint of the tick just stepped. Call it after every <see cref="Simulation.Step"/> that the host runs itself
        /// instead of calling <see cref="Step"/>; it reads the simulation's events of the last tick.
        /// </summary>
        public void AfterStep()
        {
            foreach (SimEvent simEvent in _sim.Events.Current)
            {
                if (simEvent is StateHashedEvent hashed)
                {
                    _checkpoints.Add(new ReplayCheckpoint(hashed.Tick, hashed.Hash));
                }
            }
        }

        /// <summary>
        /// Builds the replay of the match so far: tick count is the simulation clock, the final hash is the current state hash, and
        /// commands scheduled at or after the clock (which never ran) are left out. Call after the last step; can be called more than once.
        /// </summary>
        public ReplayData Finish()
        {
            // Steps run so far. Unit: ticks. Range: 0 upward.
            int tickCount = _sim.Clock.Tick;
            // Hash interval of this match. Unit: ticks. Range: 1 to 600.
            int interval = _sim.Settings.StateHashIntervalTicks;
            // Checkpoints a complete recording holds: one per multiple of the interval below tickCount. Range: 0 upward.
            int expectedCheckpoints = (tickCount + interval - 1) / interval;
            if (_checkpoints.Count != expectedCheckpoints)
            {
                throw new InvalidOperationException("The recorder saw " + _checkpoints.Count + " hash checkpoints but " + tickCount + " ticks need "
                    + expectedCheckpoints + "; step through ReplayRecorder.Step or call AfterStep after every Simulation.Step.");
            }
            // Commands that were due on a stepped tick, in submission order (ReplayData sorts them).
            List<Command> applied = new List<Command>();
            foreach (Command command in _commands)
            {
                if (command.Tick < tickCount)
                {
                    applied.Add(command);
                }
            }
            return new ReplayData(_sim.ContentDigest, _sim.Settings.Seed, _sim.Map.Id, _sim.Settings.StateHashIntervalTicks, tickCount,
                _players, applied, _checkpoints, _sim.ComputeStateHash());
        }
    }
}

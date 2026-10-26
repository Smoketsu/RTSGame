// rts/src/Sim/Simulation.cs
// The match: owns the clock, RNG, IDs, command queue, event log, players, units and mechanics, and runs
// one deterministic step per tick. Part of Rts.Sim (root). This is the only entry point hosts use:
// build a SimulationConfig, Start, then Submit commands and Step. Everything inside is engine-free and
// follows the determinism rules in docs/decisions and CLAUDE.md.
//
// Step order (fixed, documented here and in rts/src/Sim/README.md):
//   1. clear the event log
//   2. apply this tick's commands in (issuer, sequence) order
//   3. movement
//   4. faction mechanics in ascending mechanic ID order
//   5. state hash every StateHashIntervalTicks (reported as StateHashedEvent)
//   6. advance the clock
using System;
using System.Collections.Generic;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim
{
    /// <summary>One match in progress. Not thread-safe; a host steps it from one thread.</summary>
    public sealed class Simulation
    {
        /// <summary>Seed, tick rate and hash interval for this match.</summary>
        public MatchSettings Settings { get; }

        /// <summary>Unit and faction definitions. Read-only during the match.</summary>
        public ContentCatalog Content { get; }

        /// <summary>The match clock. Its tick is the current tick during a step and the next tick between steps.</summary>
        public MatchClock Clock { get; }

        /// <summary>The one random number generator. Only commands, systems and mechanics may draw from it, and only inside a step.</summary>
        public DeterministicRandom Rng { get; }

        /// <summary>Entity ID allocator; IDs are never reused.</summary>
        public EntityIdAllocator EntityIds { get; }

        /// <summary>Commands waiting for their tick.</summary>
        public CommandQueue Commands { get; }

        /// <summary>Events of the most recently stepped tick.</summary>
        public EventLog Events { get; }

        /// <summary>Players and teams.</summary>
        public PlayerRegistry Players { get; }

        /// <summary>Live units.</summary>
        public UnitRegistry Units { get; }

        /// <summary>Mechanic instances active in this match, keyed by mechanic ID in ordinal order. Filled by <see cref="Start"/>.</summary>
        public IReadOnlyDictionary<string, IFactionMechanic> Mechanics => _mechanics;

        /// <summary>True after <see cref="Start"/> has run.</summary>
        public bool IsStarted { get; private set; }

        /// <summary>True while <see cref="Step"/> is running; mutation methods require it.</summary>
        public bool IsStepping { get; private set; }

        /// <summary>The most recent hash reported by a <see cref="StateHashedEvent"/>, or 0 before the first. For hosts that only want the latest value.</summary>
        public ulong LastReportedHash { get; private set; }

        /// <summary>
        /// What happens to a player's units and resources when they leave the match or disconnect. Called by
        /// <see cref="PlayerTransitions"/>. Defaults to <see cref="StateOnlyHandOverPolicy"/> (Phase 0: state change and
        /// event only); change it with <see cref="SetHandOverPolicy"/> before <see cref="Start"/>.
        /// </summary>
        public IHandOverPolicy HandOverPolicy { get; private set; } = StateOnlyHandOverPolicy.Instance;

        /// <summary>Backing store for <see cref="Mechanics"/>.</summary>
        private readonly SortedDictionary<string, IFactionMechanic> _mechanics = new SortedDictionary<string, IFactionMechanic>(StringComparer.Ordinal);

        /// <summary>Registry the mechanics are created from.</summary>
        private readonly FactionMechanicRegistry _mechanicRegistry;

        /// <summary>Reused hasher so hashing allocates nothing per tick.</summary>
        private readonly StateHasher _hasher = new StateHasher();

        /// <summary>Builds the match objects from <paramref name="config"/>. Nothing runs until <see cref="Start"/>.</summary>
        public Simulation(SimulationConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            Settings = config.Settings;
            Content = config.Content;
            Clock = new MatchClock(Settings.TicksPerSecond);
            Rng = new DeterministicRandom(Settings.Seed);
            EntityIds = new EntityIdAllocator();
            Commands = new CommandQueue();
            Events = new EventLog();
            Players = new PlayerRegistry(config.Players);
            Units = new UnitRegistry();
            _mechanicRegistry = config.Mechanics;
        }

        /// <summary>
        /// Validates content and players, instantiates one mechanic per faction present, lets mechanics initialise,
        /// and emits <see cref="MatchStartedEvent"/>. Call exactly once before the first <see cref="Step"/>.
        /// </summary>
        public void Start()
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("Simulation already started.");
            }
            string? contentProblem = Content.Validate();
            if (contentProblem != null)
            {
                throw new InvalidOperationException("Content invalid: " + contentProblem);
            }
            // Mechanic IDs needed, collected in ordinal order so creation order is deterministic.
            SortedSet<string> mechanicIds = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Player player in Players.All)
            {
                if (!Content.Factions.TryGetValue(player.FactionId, out FactionDefinition? faction))
                {
                    throw new InvalidOperationException("Player " + player.Id + " uses unknown faction '" + player.FactionId + "'.");
                }
                mechanicIds.Add(faction.MechanicId);
            }
            foreach (string mechanicId in mechanicIds)
            {
                _mechanics.Add(mechanicId, _mechanicRegistry.Create(mechanicId));
            }
            IsStarted = true;
            Events.Clear();
            Events.Add(new MatchStartedEvent(Clock.Tick, Players.Count, Settings.Seed));
            IsStepping = true;
            try
            {
                foreach (IFactionMechanic mechanic in _mechanics.Values)
                {
                    mechanic.OnMatchStart(this);
                }
            }
            finally
            {
                IsStepping = false;
            }
        }

        /// <summary>Replaces the <see cref="HandOverPolicy"/>. Only allowed before <see cref="Start"/>, so one policy rules the whole match.</summary>
        /// <param name="policy">The policy for this match; not null.</param>
        public void SetHandOverPolicy(IHandOverPolicy policy)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("The hand-over policy must be set before Start.");
            }
            HandOverPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>
        /// Queues a command for its tick. The tick must be the current tick or later; an earlier tick is a host bug
        /// and throws. Gameplay rejections (unknown unit, wrong owner) happen during the step as events.
        /// </summary>
        public void Submit(Command command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if (IsStepping)
            {
                throw new InvalidOperationException("Submit cannot be called from inside a step; schedule from the host loop.");
            }
            if (command.Tick < Clock.Tick)
            {
                throw new InvalidOperationException("Command " + command + " is scheduled before the current tick " + Clock.Tick + ".");
            }
            Commands.Enqueue(command);
        }

        /// <summary>Runs one tick: commands, movement, mechanics, hash, then advances the clock.</summary>
        public void Step()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("Call Start before Step.");
            }
            if (IsStepping)
            {
                throw new InvalidOperationException("Step is not re-entrant.");
            }
            IsStepping = true;
            try
            {
                int tick = Clock.Tick;
                Events.Clear();
                ApplyCommands(tick);
                MovementSystem.Step(this);
                foreach (IFactionMechanic mechanic in _mechanics.Values)
                {
                    mechanic.OnTick(this);
                }
                if (tick % Settings.StateHashIntervalTicks == 0)
                {
                    ulong hash = ComputeStateHash();
                    LastReportedHash = hash;
                    Events.Add(new StateHashedEvent(tick, hash));
                }
                Clock.Advance();
            }
            finally
            {
                IsStepping = false;
            }
        }

        /// <summary>Runs <paramref name="count"/> ticks. Events from only the last tick remain readable afterwards.</summary>
        public void Step(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            for (int i = 0; i < count; i++)
            {
                Step();
            }
        }

        /// <summary>Applies every command scheduled for <paramref name="tick"/>, rejecting duplicates and reporting results.</summary>
        private void ApplyCommands(int tick)
        {
            List<Command> commands = Commands.TakeForTick(tick);
            // The previous command, to detect a duplicate (same issuer and sequence) after sorting.
            Command? previous = null;
            foreach (Command command in commands)
            {
                if (previous != null && previous.Issuer == command.Issuer && previous.Sequence == command.Sequence)
                {
                    Events.Add(new CommandRejectedEvent(tick, command, "duplicate_sequence"));
                    continue;
                }
                previous = command;
                if (command.Issuer.IsValid && !Players.Contains(command.Issuer))
                {
                    Events.Add(new CommandRejectedEvent(tick, command, "unknown_issuer"));
                    continue;
                }
                CommandResult result = command.Apply(this);
                if (!result.Accepted)
                {
                    Events.Add(new CommandRejectedEvent(tick, command, result.Reason ?? "rejected"));
                }
            }
        }

        /// <summary>
        /// Hashes the whole simulation state in a fixed order: clock, RNG, ID allocator, players, units, mechanics.
        /// Safe to call between steps; the result is never 0.
        /// </summary>
        public ulong ComputeStateHash()
        {
            _hasher.Reset();
            Clock.WriteHash(_hasher);
            Rng.WriteHash(_hasher);
            EntityIds.WriteHash(_hasher);
            Players.WriteHash(_hasher);
            Units.WriteHash(_hasher);
            _hasher.Write(_mechanics.Count);
            foreach (KeyValuePair<string, IFactionMechanic> pair in _mechanics)
            {
                _hasher.Write(pair.Key);
                pair.Value.WriteHash(_hasher);
            }
            ulong hash = _hasher.Hash;
            // Reserve 0 for "no hash" in network messages and host code.
            return hash == 0 ? 1UL : hash;
        }

        /// <summary>Creates a unit. Only valid inside a step (from a command, system or mechanic).</summary>
        /// <param name="definition">Unit type.</param>
        /// <param name="owner">Owning player or None.</param>
        /// <param name="position">Spawn position in tiles.</param>
        public Unit SpawnUnit(UnitDefinition definition, PlayerId owner, FixVec2 position)
        {
            RequireStepping();
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            Unit unit = new Unit(EntityIds.Allocate(), owner, definition, position, Clock.Tick);
            Units.Add(unit);
            Events.Add(new UnitSpawnedEvent(Clock.Tick, unit.Id, owner, definition.Id, position));
            return unit;
        }

        /// <summary>Removes a unit and reports it. Only valid inside a step. Returns false when the ID is not live.</summary>
        /// <param name="id">Unit to remove.</param>
        /// <param name="reason">Short reason for the event, such as "killed".</param>
        public bool RemoveUnit(EntityId id, string reason)
        {
            RequireStepping();
            if (!Units.Remove(id))
            {
                return false;
            }
            Events.Add(new UnitRemovedEvent(Clock.Tick, id, reason));
            return true;
        }

        /// <summary>Throws unless called from inside a step, so listeners can never mutate state (rule 8).</summary>
        private void RequireStepping()
        {
            if (!IsStepping)
            {
                throw new InvalidOperationException("Simulation state can only change inside Step (through commands, systems or mechanics).");
            }
        }
    }
}

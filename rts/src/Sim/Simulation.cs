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
//   4. combat (CombatSystem: cooldowns, targeting, damage, deaths; units in ascending ID order)
//   5. faction mechanics in ascending mechanic ID order
//   6. state hash every StateHashIntervalTicks (reported as StateHashedEvent)
//   7. advance the clock
using System;
using System.Collections.Generic;
using Rts.Sim.Combat;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Pathing;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
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

        /// <summary>Every tuning file. Systems read the part they own.</summary>
        public Tuning Tuning { get; }

        /// <summary>The map being played (spawns, resource fields, Core). Static for the match.</summary>
        public MapDefinition Map { get; }

        /// <summary>Live terrain: heights, water, surfaces, blockers (ADR-006). Hashed every tile.</summary>
        public TerrainGrid Terrain { get; }

        /// <summary>Flow-field cache for ground and water movement (ADR-007). Not hashed: a pure function of terrain.</summary>
        public FlowFieldCache Pathing { get; }

        /// <summary>
        /// Speed modifiers registered by mechanics at <see cref="IFactionMechanic.OnMatchStart"/>, kept sorted by
        /// <see cref="ISpeedModifier.Id"/> (ordinal). <see cref="MovementSystem"/> multiplies a unit's speed by each in this order.
        /// </summary>
        public IReadOnlyList<ISpeedModifier> SpeedModifiers => _speedModifiers;

        /// <summary>Backing store for <see cref="SpeedModifiers"/>, kept sorted by Id.</summary>
        private readonly List<ISpeedModifier> _speedModifiers = new List<ISpeedModifier>();

        /// <summary>Mechanic instances active in this match, keyed by mechanic ID in ordinal order. Filled by <see cref="Start"/>.</summary>
        public IReadOnlyDictionary<string, IFactionMechanic> Mechanics => _mechanics;

        /// <summary>True after <see cref="Start"/> has run.</summary>
        public bool IsStarted { get; private set; }

        /// <summary>True while <see cref="Step"/> is running; mutation methods require it.</summary>
        public bool IsStepping { get; private set; }

        /// <summary>The most recent hash reported by a <see cref="StateHashedEvent"/>, or 0 before the first. For hosts that only want the latest value. Range: any ulong; 0 before the first report.</summary>
        public ulong LastReportedHash { get; private set; }

        /// <summary>
        /// What happens to a player's units and resources when they leave the match or disconnect. Called by
        /// <see cref="PlayerTransitions"/>. Defaults to <see cref="StateOnlyHandOverPolicy"/> (Phase 0: state change and
        /// event only); change it with <see cref="SetHandOverPolicy"/> before <see cref="Start"/>.
        /// </summary>
        public IHandOverPolicy HandOverPolicy { get; private set; } = StateOnlyHandOverPolicy.Instance;

        /// <summary>
        /// Largest absolute X or Y coordinate (in tiles) a command may name. Positions beyond this are rejected as
        /// out_of_bounds so squared distances never saturate Fix64 (2 × 16384² is far below the 2^31 limit). This is a
        /// validation bound, not a tuning value; Phase 1 maps are at most a few hundred tiles across.
        /// </summary>
        public static readonly Fix64 WorldBoundTiles = Fix64.FromInt(16384);

        /// <summary>
        /// FNV-1a digest of every unit and faction definition, computed once by <see cref="Start"/> and written into the
        /// state hash, so two peers with different data files diverge at tick 0 instead of mid-match. 0 before Start. Range: any ulong.
        /// </summary>
        public ulong ContentDigest { get; private set; }

        /// <summary>
        /// Damage modifiers registered by mechanics at <see cref="IFactionMechanic.OnMatchStart"/>, in ascending
        /// <see cref="IDamageModifier.Id"/> (ordinal) order. <see cref="CombatSystem"/> multiplies a hit's damage by each in this order.
        /// </summary>
        public IReadOnlyList<IDamageModifier> DamageModifiers => _damageModifiers;

        /// <summary>Backing store for <see cref="DamageModifiers"/>, kept sorted by Id.</summary>
        private readonly List<IDamageModifier> _damageModifiers = new List<IDamageModifier>();

        /// <summary>True only while mechanics run <see cref="IFactionMechanic.OnMatchStart"/>, the one time modifiers may be registered.</summary>
        private bool _modifierRegistrationOpen;

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
            // Draws are only legal inside a step; Start and Step unlock the generator while they run.
            Rng.SetLocked(true);
            EntityIds = new EntityIdAllocator();
            Commands = new CommandQueue();
            Events = new EventLog();
            Players = new PlayerRegistry(config.Players);
            Units = new UnitRegistry();
            Tuning = config.Tuning;
            Map = config.Map;
            Terrain = new TerrainGrid(Map, Tuning.Terrain, Tuning.Flood);
            Pathing = new FlowFieldCache(Terrain, Tuning.Terrain.MaxCachedFlowFields);
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
            // No definition may change once the match is running; the digest below would otherwise go stale.
            Content.Freeze();
            ContentDigest = Content.ComputeDigest();
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
            Rng.SetLocked(false);
            _modifierRegistrationOpen = true;
            try
            {
                foreach (IFactionMechanic mechanic in _mechanics.Values)
                {
                    mechanic.OnMatchStart(this);
                }
            }
            finally
            {
                _modifierRegistrationOpen = false;
                Rng.SetLocked(true);
                IsStepping = false;
            }
        }

        /// <summary>
        /// Adds a speed modifier. Same rules as <see cref="RegisterDamageModifier"/>: only from
        /// <see cref="IFactionMechanic.OnMatchStart"/>, sorted by Id, duplicate Ids throw.
        /// </summary>
        /// <param name="modifier">The modifier; its Id must be non-empty.</param>
        public void RegisterSpeedModifier(ISpeedModifier modifier)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }
            if (!_modifierRegistrationOpen)
            {
                throw new InvalidOperationException("Speed modifiers can only be registered from IFactionMechanic.OnMatchStart.");
            }
            if (string.IsNullOrEmpty(modifier.Id))
            {
                throw new ArgumentException("A speed modifier needs a non-empty Id.", nameof(modifier));
            }
            // Insertion index keeping the list sorted by Id. Unit: list position. Range: 0 to Count.
            int index = 0;
            while (index < _speedModifiers.Count)
            {
                // Comparison of the existing Id with the new one: negative when the existing one sorts first.
                int order = string.CompareOrdinal(_speedModifiers[index].Id, modifier.Id);
                if (order == 0)
                {
                    throw new InvalidOperationException("A speed modifier with id '" + modifier.Id + "' is already registered.");
                }
                if (order > 0)
                {
                    break;
                }
                index++;
            }
            _speedModifiers.Insert(index, modifier);
        }

        /// <summary>
        /// Adds a damage modifier. Only allowed from <see cref="IFactionMechanic.OnMatchStart"/>; the list is kept sorted by
        /// <see cref="IDamageModifier.Id"/> (ordinal) so the multiplication order never depends on mechanic creation order.
        /// A second modifier with the same Id throws.
        /// </summary>
        /// <param name="modifier">The modifier; its Id must be non-empty.</param>
        public void RegisterDamageModifier(IDamageModifier modifier)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }
            if (!_modifierRegistrationOpen)
            {
                throw new InvalidOperationException("Damage modifiers can only be registered from IFactionMechanic.OnMatchStart.");
            }
            if (string.IsNullOrEmpty(modifier.Id))
            {
                throw new ArgumentException("A damage modifier needs a non-empty Id.", nameof(modifier));
            }
            // Insertion index keeping the list sorted by Id. Unit: list position. Range: 0 to Count.
            int index = 0;
            while (index < _damageModifiers.Count)
            {
                // Comparison of the existing Id with the new one: negative when the existing one sorts first.
                int order = string.CompareOrdinal(_damageModifiers[index].Id, modifier.Id);
                if (order == 0)
                {
                    throw new InvalidOperationException("A damage modifier with id '" + modifier.Id + "' is already registered.");
                }
                if (order > 0)
                {
                    break;
                }
                index++;
            }
            _damageModifiers.Insert(index, modifier);
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

        /// <summary>Runs one tick: commands, movement, combat, mechanics, hash, then advances the clock.</summary>
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
            Rng.SetLocked(false);
            try
            {
                // Current tick being simulated. Unit: ticks. Range: 0 up to the match length.
                int tick = Clock.Tick;
                Events.Clear();
                ApplyCommands(tick);
                MovementSystem.Step(this);
                CombatSystem.Step(this);
                foreach (IFactionMechanic mechanic in _mechanics.Values)
                {
                    mechanic.OnTick(this);
                }
                if (tick % Settings.StateHashIntervalTicks == 0)
                {
                    // State hash of the whole simulation at this tick. Unit: 64-bit hash value. Range: any ulong except 0.
                    ulong hash = ComputeStateHash();
                    LastReportedHash = hash;
                    Events.Add(new StateHashedEvent(tick, hash));
                }
                Clock.Advance();
            }
            finally
            {
                Rng.SetLocked(true);
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
            // Loop counter over the ticks to run. Unit: ticks. Range: 0 to count - 1.
            for (int i = 0; i < count; i++)
            {
                Step();
            }
        }

        /// <summary>
        /// Applies every command scheduled for <paramref name="tick"/> in (issuer, sequence) order. When two or more
        /// commands share an (issuer, sequence) key, every one of them is rejected as duplicate_sequence, so the outcome
        /// never depends on which arrived first (a key is unique per issuer by contract; a collision is an issuer bug).
        /// </summary>
        private void ApplyCommands(int tick)
        {
            // Commands due at this tick, sorted by (issuer, sequence, kind). Range: zero or more commands.
            List<Command> commands = Commands.TakeForTick(tick);
            // Number of commands per (issuer, sequence) key; the key packs the issuer slot above the 32-bit sequence.
            SortedDictionary<ulong, int> keyCounts = new SortedDictionary<ulong, int>();
            foreach (Command command in commands)
            {
                // Packed (issuer, sequence) key: issuer slot in bits 32 to 39, sequence in bits 0 to 31.
                ulong key = ((ulong)command.Issuer.Value << 32) | command.Sequence;
                // Commands already counted under this key. Unit: commands. Range: 0 up; 0 when first seen.
                keyCounts.TryGetValue(key, out int count);
                keyCounts[key] = count + 1;
            }
            foreach (Command command in commands)
            {
                // Key of this command, as built above.
                ulong key = ((ulong)command.Issuer.Value << 32) | command.Sequence;
                if (keyCounts[key] > 1)
                {
                    Events.Add(new CommandRejectedEvent(tick, command, "duplicate_sequence"));
                    continue;
                }
                if (command.Issuer.IsValid && !Players.Contains(command.Issuer))
                {
                    Events.Add(new CommandRejectedEvent(tick, command, "unknown_issuer"));
                    continue;
                }
                // Outcome of applying this command: accepted flag and optional rejection reason.
                CommandResult result = command.Apply(this);
                if (!result.Accepted)
                {
                    Events.Add(new CommandRejectedEvent(tick, command, result.Reason ?? "rejected"));
                }
            }
        }

        /// <summary>
        /// Hashes the whole simulation state in a fixed order: content digest, clock, RNG, ID allocator, players, units,
        /// mechanics, damage modifier IDs, hand-over policy. Safe to call between steps; the result is never 0.
        /// </summary>
        public ulong ComputeStateHash()
        {
            _hasher.Reset();
            _hasher.Write(ContentDigest);
            Clock.WriteHash(_hasher);
            Rng.WriteHash(_hasher);
            EntityIds.WriteHash(_hasher);
            Players.WriteHash(_hasher);
            Units.WriteHash(_hasher);
            Terrain.WriteHash(_hasher);
            _hasher.Write(_mechanics.Count);
            foreach (KeyValuePair<string, IFactionMechanic> pair in _mechanics)
            {
                _hasher.Write(pair.Key);
                pair.Value.WriteHash(_hasher);
            }
            _hasher.Write(_damageModifiers.Count);
            foreach (IDamageModifier modifier in _damageModifiers)
            {
                _hasher.Write(modifier.Id);
            }
            _hasher.Write(HandOverPolicy.Id);
            HandOverPolicy.WriteHash(_hasher);
            // Raw hash of all state written so far. Unit: 64-bit hash value. Range: any ulong; 0 is remapped below.
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
            // The unit just created, with its ID allocated here. Range: ID from 1 up.
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

        /// <summary>True when both components of <paramref name="position"/> lie within ±<see cref="WorldBoundTiles"/>.</summary>
        /// <param name="position">Position in tiles.</param>
        public static bool IsWithinWorldBounds(FixVec2 position)
        {
            return position.X.Abs() <= WorldBoundTiles && position.Y.Abs() <= WorldBoundTiles;
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

// rts/src/Sim/Events/CoreEvents.cs
// The events the Phase 0 core emits: match start, unit spawn and arrival, command rejection, state hash.
// Part of Rts.Sim.Events. Later systems add their own event classes in their own folders.
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Sim.Events
{
    /// <summary>Emitted once by <see cref="Simulation.Start"/> before any tick runs.</summary>
    public sealed class MatchStartedEvent : SimEvent
    {
        /// <summary>Number of player slots in the match. Range: 1 to 255.</summary>
        public int PlayerCount { get; }

        /// <summary>Match seed, echoed so logs can reproduce the match.</summary>
        public ulong Seed { get; }

        public MatchStartedEvent(int tick, int playerCount, ulong seed) : base(tick)
        {
            PlayerCount = playerCount;
            Seed = seed;
        }

        public override string Kind => "match_started";

        public override string Describe() => "Match started with " + PlayerCount + " players, seed " + Seed + ".";
    }

    /// <summary>A unit entered the simulation.</summary>
    public sealed class UnitSpawnedEvent : SimEvent
    {
        /// <summary>The new unit's ID.</summary>
        public EntityId Unit { get; }

        /// <summary>Owning player, or <see cref="PlayerId.None"/> for neutral units.</summary>
        public PlayerId Owner { get; }

        /// <summary>Definition ID from data, for example "skai.windrunner".</summary>
        public string DefinitionId { get; }

        /// <summary>Spawn position in tiles.</summary>
        public FixVec2 Position { get; }

        public UnitSpawnedEvent(int tick, EntityId unit, PlayerId owner, string definitionId, FixVec2 position) : base(tick)
        {
            Unit = unit;
            Owner = owner;
            DefinitionId = definitionId;
            Position = position;
        }

        public override string Kind => "unit_spawned";

        public override string Describe() => Unit + " (" + DefinitionId + ") spawned for " + Owner + " at " + Position + ".";
    }

    /// <summary>A moving unit reached its move target this tick.</summary>
    public sealed class UnitArrivedEvent : SimEvent
    {
        /// <summary>The unit that arrived.</summary>
        public EntityId Unit { get; }

        /// <summary>Where it stopped, in tiles.</summary>
        public FixVec2 Position { get; }

        public UnitArrivedEvent(int tick, EntityId unit, FixVec2 position) : base(tick)
        {
            Unit = unit;
            Position = position;
        }

        public override string Kind => "unit_arrived";

        public override string Describe() => Unit + " arrived at " + Position + ".";
    }

    /// <summary>A unit left the simulation (killed, or removed by a command).</summary>
    public sealed class UnitRemovedEvent : SimEvent
    {
        /// <summary>The removed unit's ID; it is never reused.</summary>
        public EntityId Unit { get; }

        /// <summary>Why it was removed, for example "killed" or "despawn_command".</summary>
        public string Reason { get; }

        public UnitRemovedEvent(int tick, EntityId unit, string reason) : base(tick)
        {
            Unit = unit;
            Reason = reason;
        }

        public override string Kind => "unit_removed";

        public override string Describe() => Unit + " removed: " + Reason + ".";
    }

    /// <summary>A command was not applied. The command is kept so the host can show why.</summary>
    public sealed class CommandRejectedEvent : SimEvent
    {
        /// <summary>The rejected command.</summary>
        public Command Command { get; }

        /// <summary>Short reason such as "unknown_unit" or "not_owner".</summary>
        public string Reason { get; }

        public CommandRejectedEvent(int tick, Command command, string reason) : base(tick)
        {
            Command = command;
            Reason = reason;
        }

        public override string Kind => "command_rejected";

        public override string Describe() => "Rejected " + Command + ": " + Reason + ".";
    }

    /// <summary>The full state hash computed at the end of this tick (every state_hash_interval_ticks).</summary>
    public sealed class StateHashedEvent : SimEvent
    {
        /// <summary>64-bit FNV-1a hash of the whole simulation state (ADR-005). Never 0.</summary>
        public ulong Hash { get; }

        public StateHashedEvent(int tick, ulong hash) : base(tick)
        {
            Hash = hash;
        }

        public override string Kind => "state_hashed";

        public override string Describe() => "State hash " + Hash.ToString("x16") + ".";
    }
}

// rts/src/Sim/Units/Unit.cs
// The live state of one unit in a match. Part of Rts.Sim.Units.
// Phase 0 state is position, health and a straight-line move target. Mutation happens only through
// commands and systems inside Simulation.Step; everything here is hashed except the definition name.
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>One unit. Created by <see cref="Simulation.SpawnUnit"/>; never reused after removal.</summary>
    public sealed class Unit
    {
        /// <summary>Unique ID for the whole match.</summary>
        public EntityId Id { get; }

        /// <summary>Owning player, or <see cref="PlayerId.None"/> for neutral.</summary>
        public PlayerId Owner { get; }

        /// <summary>The data definition this unit was built from.</summary>
        public UnitDefinition Definition { get; }

        /// <summary>Current position in tiles.</summary>
        public FixVec2 Position { get; private set; }

        /// <summary>Current health. Unit: health points. Range: 0 to Definition.MaxHealth.</summary>
        public Fix64 Health { get; private set; }

        /// <summary>True while a move order is active. The unit stops and clears this on arrival.</summary>
        public bool HasMoveTarget { get; private set; }

        /// <summary>Destination of the active move order in tiles. Meaningless when <see cref="HasMoveTarget"/> is false (kept as zero).</summary>
        public FixVec2 MoveTarget { get; private set; }

        /// <summary>Tick the unit was spawned on. Range: 0 upward.</summary>
        public int SpawnTick { get; }

        /// <summary>Creates a unit at full health. Internal: use the simulation's spawn path so the ID and events are handled.</summary>
        internal Unit(EntityId id, PlayerId owner, UnitDefinition definition, FixVec2 position, int spawnTick)
        {
            Id = id;
            Owner = owner;
            Definition = definition;
            Position = position;
            Health = definition.MaxHealth;
            HasMoveTarget = false;
            MoveTarget = FixVec2.Zero;
            SpawnTick = spawnTick;
        }

        /// <summary>Starts or replaces a straight-line move order.</summary>
        /// <param name="target">Destination in tiles.</param>
        internal void SetMoveTarget(FixVec2 target)
        {
            HasMoveTarget = true;
            MoveTarget = target;
        }

        /// <summary>Cancels the move order and zeroes the stored target so the hash does not depend on stale data.</summary>
        internal void ClearMoveTarget()
        {
            HasMoveTarget = false;
            MoveTarget = FixVec2.Zero;
        }

        /// <summary>Teleports the unit; used by the movement system each tick.</summary>
        /// <param name="position">New position in tiles.</param>
        internal void SetPosition(FixVec2 position)
        {
            Position = position;
        }

        /// <summary>Sets health, clamped to [0, MaxHealth]. Combat (Phase 1) goes through here.</summary>
        /// <param name="health">New health in health points.</param>
        internal void SetHealth(Fix64 health)
        {
            Health = Fix64.Clamp(health, Fix64.Zero, Definition.MaxHealth);
        }

        /// <summary>Feeds every simulation-relevant field into the state hash, in a fixed order.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
            hasher.Write(Owner);
            hasher.Write(Definition.Id);
            hasher.Write((byte)Definition.Layer);
            hasher.Write(Position);
            hasher.Write(Health);
            hasher.Write(HasMoveTarget);
            hasher.Write(MoveTarget);
            hasher.Write(SpawnTick);
        }
    }
}

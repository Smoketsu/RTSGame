// rts/src/Sim/Units/Unit.cs
// The live state of one unit in a match. Part of Rts.Sim.Units.
// State is position, health, a move target and (Phase 1) the combat state: weapon cooldown, attack target, attack-move order. Mutation happens only through
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

        /// <summary>Current position in tiles. Range: each component within ±<see cref="Simulation.WorldBoundTiles"/>.</summary>
        public FixVec2 Position { get; private set; }

        /// <summary>Current health. Unit: health points. Range: 0 to Definition.MaxHealth.</summary>
        public Fix64 Health { get; private set; }

        /// <summary>True while a move order is active. The unit stops and clears this on arrival.</summary>
        public bool HasMoveTarget { get; private set; }

        /// <summary>Destination of the active move order in tiles. Range: each component within ±<see cref="Simulation.WorldBoundTiles"/>. Meaningless when <see cref="HasMoveTarget"/> is false (kept as zero).</summary>
        public FixVec2 MoveTarget { get; private set; }

        /// <summary>Tick the unit was spawned on. Range: 0 upward.</summary>
        public int SpawnTick { get; }

        /// <summary>True while a status (Grounded) overrides the definition's movement layer.</summary>
        public bool HasLayerOverride { get; private set; }

        /// <summary>The overriding layer when <see cref="HasLayerOverride"/>; Ground otherwise (ignored).</summary>
        public MovementLayer LayerOverride { get; private set; }

        /// <summary>Layer the unit moves on this tick: the override when set, else the definition's layer (ADR-007).</summary>
        public MovementLayer EffectiveLayer => HasLayerOverride ? LayerOverride : Definition.Layer;

        /// <summary>Speed multiplier from statuses (slowed). Unit: multiplier of base speed. Range: 0 to 2; 1 when no status applies.</summary>
        public Fix64 StatusSpeedMultiplier { get; private set; }

        /// <summary>Ticks until the weapon can fire again. Unit: ticks. Range: 0 up to the weapon's cooldown in ticks; 0 means ready. Written by <see cref="Rts.Sim.Combat.CombatSystem"/>.</summary>
        public int CooldownTicksRemaining { get; private set; }

        /// <summary>The enemy unit this unit is attacking, or <see cref="EntityId.None"/> when idle. Range: a live unit's ID or None. Written by the combat system and by attack commands.</summary>
        public EntityId AttackTarget { get; private set; }

        /// <summary>True when <see cref="AttackTarget"/> was named by an attack order and is chased at any distance; false when the combat system picked it automatically (dropped when out of reach). Meaningless when there is no target (kept false).</summary>
        public bool IsAttackTargetForced { get; private set; }

        /// <summary>True while an attack-move order is active: the unit walks to <see cref="AttackMoveGoal"/>, stopping to fight enemies within weapon range plus the acquire bonus.</summary>
        public bool IsAttackMoving { get; private set; }

        /// <summary>Destination of the active attack-move order in tiles. Range: each component within ±<see cref="Simulation.WorldBoundTiles"/>. Meaningless when <see cref="IsAttackMoving"/> is false (kept as zero).</summary>
        public FixVec2 AttackMoveGoal { get; private set; }

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
            HasLayerOverride = false;
            LayerOverride = MovementLayer.Ground;
            StatusSpeedMultiplier = Fix64.One;
            CooldownTicksRemaining = 0;
            AttackTarget = EntityId.None;
            IsAttackTargetForced = false;
            IsAttackMoving = false;
            AttackMoveGoal = FixVec2.Zero;
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

        /// <summary>Sets the weapon cooldown. Internal: combat system only.</summary>
        /// <param name="ticks">Ticks until the next shot. Unit: ticks. Range: 0 or more.</param>
        internal void SetCooldownTicks(int ticks)
        {
            CooldownTicksRemaining = ticks < 0 ? 0 : ticks;
        }

        /// <summary>Sets the attack target, or clears it with <see cref="EntityId.None"/> (which also clears the forced flag).</summary>
        /// <param name="target">Enemy unit ID or None.</param>
        /// <param name="forced">True when an attack order named the target; ignored when <paramref name="target"/> is None.</param>
        internal void SetAttackTarget(EntityId target, bool forced)
        {
            AttackTarget = target;
            IsAttackTargetForced = target.IsValid && forced;
        }

        /// <summary>Starts or replaces an attack-move order. Does not touch the move target.</summary>
        /// <param name="goal">Destination in tiles.</param>
        internal void SetAttackMove(FixVec2 goal)
        {
            IsAttackMoving = true;
            AttackMoveGoal = goal;
        }

        /// <summary>Ends the attack-move order and zeroes its goal so the hash does not depend on stale data.</summary>
        internal void ClearAttackMove()
        {
            IsAttackMoving = false;
            AttackMoveGoal = FixVec2.Zero;
        }

        /// <summary>Drops the attack-move order and the attack target (the weapon cooldown keeps running).</summary>
        internal void ClearCombatOrders()
        {
            ClearAttackMove();
            SetAttackTarget(EntityId.None, false);
        }

        /// <summary>Forces the unit onto <paramref name="layer"/> (a Grounded flyer). Statuses call this inside a step.</summary>
        internal void SetLayerOverride(MovementLayer layer)
        {
            HasLayerOverride = true;
            LayerOverride = layer;
        }

        /// <summary>Returns the unit to its definition's layer.</summary>
        internal void ClearLayerOverride()
        {
            HasLayerOverride = false;
            LayerOverride = MovementLayer.Ground;
        }

        /// <summary>Sets the status speed multiplier (1 = none). Statuses call this inside a step.</summary>
        /// <param name="multiplier">Multiplier of base speed, 0 to 2.</param>
        internal void SetStatusSpeedMultiplier(Fix64 multiplier)
        {
            StatusSpeedMultiplier = Fix64.Clamp(multiplier, Fix64.Zero, Fix64.FromInt(2));
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
            hasher.Write(HasLayerOverride);
            hasher.Write((byte)LayerOverride);
            hasher.Write(StatusSpeedMultiplier);
            hasher.Write(CooldownTicksRemaining);
            hasher.Write(AttackTarget);
            hasher.Write(IsAttackTargetForced);
            hasher.Write(IsAttackMoving);
            hasher.Write(AttackMoveGoal);
        }
    }
}

// rts/src/Sim/Units/Unit.cs
// The live state of one unit in a match. Part of Rts.Sim.Units.
// State is position, health, a move target and (Phase 1) the combat state (weapon cooldown, attack target, attack-move order),
// the HQ state (rooting, Last Stand), the worker gather loop, the production queue and construction progress. Mutation happens only through
// commands and systems inside Simulation.Step; everything here is hashed except the definition name.
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;

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

        /// <summary>Tick on which <see cref="MovementSystem"/> last moved this unit under its own order (move, attack-move, chase); -1 before any such move. Momentum reads it. Range: -1 upward.</summary>
        public int LastSelfMoveTick { get; private set; }

        /// <summary>Records a self-move on <paramref name="tick"/>. Only the movement system calls it.</summary>
        internal void MarkSelfMoved(int tick)
        {
            LastSelfMoveTick = tick;
        }

        /// <summary>True while a status (Grounded) overrides the definition's movement layer.</summary>
        public bool HasLayerOverride { get; private set; }

        /// <summary>The overriding layer when <see cref="HasLayerOverride"/>; Ground otherwise (ignored).</summary>
        public MovementLayer LayerOverride { get; private set; }

        /// <summary>Layer the unit moves on this tick: the override when set, else the definition's layer (ADR-007).</summary>
        public MovementLayer EffectiveLayer => HasLayerOverride ? LayerOverride : Definition.Layer;

        /// <summary>Speed multiplier from statuses (slowed). Unit: multiplier of base speed. Range: 0 to 2; 1 when no status applies.</summary>
        public Fix64 StatusSpeedMultiplier { get; private set; }

        /// <summary>Statuses on this unit (Grounded, slows, drench counter). Created with the unit; only <see cref="Rts.Sim.Statuses.StatusSystem"/> changes it, and it is the one that sets <see cref="LayerOverride"/> and <see cref="StatusSpeedMultiplier"/> from it. Hashed at the end of <see cref="WriteHash"/>.</summary>
        public Rts.Sim.Statuses.UnitStatusSet Statuses { get; }

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

        /// <summary>HQ state of an HQ unit (<see cref="UnitDefinition.Hq"/> not null); meaningless (kept Rooted) for other units. Written by <see cref="HqSystem"/> and the root and uproot commands. Hashed.</summary>
        public HqState HqState { get; private set; }

        /// <summary>Simulation steps left until a Rooting or Uprooting HQ finishes. Unit: ticks. Range: 0 (not switching) up to root_seconds or uproot_seconds in ticks (tuning/hq.json). Hashed.</summary>
        public int HqTransitionTicksRemaining { get; private set; }

        /// <summary>How many times this HQ's Last Stand has triggered. Unit: count. Range: 0 to last_stand_uses_per_match (tuning/hq.json, placeholder 1). Hashed.</summary>
        public int LastStandUses { get; private set; }

        /// <summary>True once Last Stand has triggered at least once for this unit.</summary>
        public bool LastStandUsed => LastStandUses > 0;

        /// <summary>Combat phases of invulnerability left from Last Stand. Unit: ticks. Range: 0 (vulnerable) up to last_stand_invulnerable_seconds in ticks. Counted down by <see cref="HqSystem"/> once per tick before combat. Hashed.</summary>
        public int LastStandTicksRemaining { get; private set; }

        /// <summary>True while Last Stand makes the unit immune to damage; <see cref="Rts.Sim.Combat.CombatSystem"/> ignores hits on it.</summary>
        public bool IsInvulnerable => LastStandTicksRemaining > 0;

        /// <summary>
        /// Armour type that applies to hits on this unit now: the definition's, except for an HQ, whose armour follows its state
        /// (Rooted uses the rooted armour type; Uprooted, Rooting and Uprooting use the uprooted one).
        /// </summary>
        public string ArmourType
        {
            get
            {
                if (Definition.Hq == null)
                {
                    return Definition.ArmourType;
                }
                return HqState == HqState.Rooted ? Definition.Hq.RootedArmourType : Definition.Hq.UprootedArmourType;
            }
        }

        /// <summary>Where a worker is in its gather loop. Idle for non-workers and workers with no gather order. Written by <see cref="EconomySystem"/> and the gather command. Hashed.</summary>
        public GatherState GatherState { get; private set; }

        /// <summary>Index of the resource field being worked, in map order. Range: 0 to field count - 1 while gathering; -1 when there is no gather order. Hashed.</summary>
        public int GatherFieldIndex { get; private set; }

        /// <summary>Resource units the worker carries to a drop-off. Unit: resource units. Range: 0 to gather_amount_per_trip (tuning/economy.json). Hashed.</summary>
        public int CarriedAmount { get; private set; }

        /// <summary>Kind of the carried resource; meaningless (kept Common) while <see cref="CarriedAmount"/> is 0. Hashed.</summary>
        public ResourceKind CarriedKind { get; private set; }

        /// <summary>Ticks left at the field before the next load is taken. Unit: ticks. Range: 0 (not gathering) up to gather_seconds_per_trip in ticks. Hashed.</summary>
        public int GatherTicksRemaining { get; private set; }

        /// <summary>Units waiting to be produced here, head first (the head is in production). Range: 0 to max_queue_length (tuning/economy.json) unit IDs. Hashed.</summary>
        public IReadOnlyList<string> ProductionQueue => _productionQueue;

        /// <summary>Backing list for <see cref="ProductionQueue"/>.</summary>
        private readonly List<string> _productionQueue = new List<string>();

        /// <summary>Ticks until the head of the queue is finished. Unit: ticks. Range: 0 when the queue is empty or the head just started; otherwise 1 up to the head's build_seconds in ticks. Hashed.</summary>
        public int ProductionTicksRemaining { get; private set; }

        /// <summary>Ticks of construction left for a structure placed by a worker. Unit: ticks. Range: 0 (complete, and always 0 for units that were never constructed) up to build_seconds in ticks. A unit under construction is not a drop-off or producer and provides no supply. Hashed.</summary>
        public int ConstructionTicksRemaining { get; private set; }

        /// <summary>True while the unit is still being constructed.</summary>
        public bool IsUnderConstruction => ConstructionTicksRemaining > 0;

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
            LastSelfMoveTick = -1;
            Statuses = new Rts.Sim.Statuses.UnitStatusSet();
            CooldownTicksRemaining = 0;
            AttackTarget = EntityId.None;
            IsAttackTargetForced = false;
            IsAttackMoving = false;
            AttackMoveGoal = FixVec2.Zero;
            HqState = HqState.Rooted;
            HqTransitionTicksRemaining = 0;
            LastStandUses = 0;
            LastStandTicksRemaining = 0;
            GatherState = GatherState.Idle;
            GatherFieldIndex = -1;
            CarriedAmount = 0;
            CarriedKind = ResourceKind.Common;
            GatherTicksRemaining = 0;
            ProductionTicksRemaining = 0;
            ConstructionTicksRemaining = 0;
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

        /// <summary>Sets the HQ state and the switching countdown. Internal: HQ system and HQ commands only.</summary>
        /// <param name="state">New state.</param>
        /// <param name="transitionTicks">Ticks left for Rooting or Uprooting; 0 for Rooted and Uprooted. Unit: ticks.</param>
        internal void SetHqState(HqState state, int transitionTicks)
        {
            HqState = state;
            HqTransitionTicksRemaining = transitionTicks < 0 ? 0 : transitionTicks;
        }

        /// <summary>Counts the HQ switching countdown down by one tick (not below 0). Internal: HQ system only.</summary>
        internal void TickHqTransition()
        {
            if (HqTransitionTicksRemaining > 0)
            {
                HqTransitionTicksRemaining--;
            }
        }

        /// <summary>Starts Last Stand: counts a use and sets the invulnerability countdown. Internal: HQ system only.</summary>
        /// <param name="ticks">Invulnerability length. Unit: ticks. Range: 0 or more.</param>
        internal void StartLastStand(int ticks)
        {
            LastStandUses++;
            LastStandTicksRemaining = ticks < 0 ? 0 : ticks;
        }

        /// <summary>Counts the Last Stand invulnerability down by one tick (not below 0). Internal: HQ system only.</summary>
        internal void TickLastStand()
        {
            if (LastStandTicksRemaining > 0)
            {
                LastStandTicksRemaining--;
            }
        }

        /// <summary>Starts or changes a gather order. Internal: gather command and economy system only.</summary>
        /// <param name="state">New gather state.</param>
        /// <param name="fieldIndex">Field being worked, or -1 for none.</param>
        /// <param name="ticksRemaining">Ticks left at the field (state Gathering), else 0. Unit: ticks.</param>
        internal void SetGather(GatherState state, int fieldIndex, int ticksRemaining)
        {
            GatherState = state;
            GatherFieldIndex = state == GatherState.Idle ? -1 : fieldIndex;
            GatherTicksRemaining = ticksRemaining < 0 ? 0 : ticksRemaining;
        }

        /// <summary>Ends the gather order. Whatever is carried stays carried. Internal.</summary>
        internal void ClearGather()
        {
            SetGather(GatherState.Idle, -1, 0);
        }

        /// <summary>Loads the worker with a resource. Internal: economy system only.</summary>
        /// <param name="kind">Resource kind.</param>
        /// <param name="amount">Resource units carried. Unit: resource units. Range: 1 or more.</param>
        internal void SetCarried(ResourceKind kind, int amount)
        {
            CarriedKind = kind;
            CarriedAmount = amount;
        }

        /// <summary>Empties the worker's load and resets the kind so the hash does not depend on stale data. Internal.</summary>
        internal void ClearCarried()
        {
            CarriedKind = ResourceKind.Common;
            CarriedAmount = 0;
        }

        /// <summary>Appends a unit ID to the production queue. Internal: train command only.</summary>
        /// <param name="unitId">Definition ID to produce.</param>
        /// <param name="ticksIfFirst">Build time in ticks, used when the queue was empty so this entry starts at once. Unit: ticks. Range: 1 or more.</param>
        internal void EnqueueProduction(string unitId, int ticksIfFirst)
        {
            if (_productionQueue.Count == 0)
            {
                ProductionTicksRemaining = ticksIfFirst;
            }
            _productionQueue.Add(unitId);
        }

        /// <summary>Counts the head of the queue down by one tick (not below 0). Internal: economy system only.</summary>
        internal void TickProduction()
        {
            if (ProductionTicksRemaining > 0)
            {
                ProductionTicksRemaining--;
            }
        }

        /// <summary>Removes the finished head of the queue and starts the next entry's countdown. Internal: economy system only.</summary>
        /// <param name="nextTicks">Build time of the new head in ticks, or 0 when the queue is now empty. Unit: ticks.</param>
        internal void FinishProductionHead(int nextTicks)
        {
            _productionQueue.RemoveAt(0);
            ProductionTicksRemaining = _productionQueue.Count == 0 ? 0 : nextTicks;
        }

        /// <summary>Marks a structure as under construction. Internal: build command only.</summary>
        /// <param name="ticks">Construction time. Unit: ticks. Range: 1 or more.</param>
        internal void SetConstructionTicks(int ticks)
        {
            ConstructionTicksRemaining = ticks < 0 ? 0 : ticks;
        }

        /// <summary>Counts construction down by one tick (not below 0). Internal: economy system only.</summary>
        internal void TickConstruction()
        {
            if (ConstructionTicksRemaining > 0)
            {
                ConstructionTicksRemaining--;
            }
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
            hasher.Write(LastSelfMoveTick);
            hasher.Write(CooldownTicksRemaining);
            hasher.Write(AttackTarget);
            hasher.Write(IsAttackTargetForced);
            hasher.Write(IsAttackMoving);
            hasher.Write(AttackMoveGoal);
            hasher.Write((byte)HqState);
            hasher.Write(HqTransitionTicksRemaining);
            hasher.Write(LastStandUses);
            hasher.Write(LastStandTicksRemaining);
            hasher.Write((byte)GatherState);
            hasher.Write(GatherFieldIndex);
            hasher.Write(CarriedAmount);
            hasher.Write((byte)CarriedKind);
            hasher.Write(GatherTicksRemaining);
            hasher.Write(_productionQueue.Count);
            foreach (string queued in _productionQueue)
            {
                hasher.Write(queued);
            }
            hasher.Write(ProductionTicksRemaining);
            hasher.Write(ConstructionTicksRemaining);
            Statuses.WriteHash(hasher);
        }
    }
}

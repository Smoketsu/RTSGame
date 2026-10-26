// rts/src/Sim/Commands/CombatCommands.cs
// The Phase 1 combat command set: attack-move to a point, attack a unit, stop. Part of Rts.Sim.Commands.
// All three use the same ownership and bounds checks as MoveUnitCommand: the unit must exist, must belong to the
// issuer (a script issuer, PlayerId.None, may order any unit), and positions must be within the world bound.
// What the orders do each tick is documented in rts/src/Sim/Combat/CombatSystem.cs.
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Sim.Commands
{
    /// <summary>Orders a unit to walk to a goal, stopping to fight enemies it meets on the way, then continuing.</summary>
    public sealed class AttackMoveCommand : Command
    {
        /// <summary>The unit to order. Must exist and belong to the issuer (or the issuer is None, the script).</summary>
        public EntityId Unit { get; }

        /// <summary>Destination in tiles. Range: each component within ±<see cref="Simulation.WorldBoundTiles"/>, else rejected.</summary>
        public FixVec2 Goal { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public AttackMoveCommand(int tick, PlayerId issuer, uint sequence, EntityId unit, FixVec2 goal)
            : base(tick, issuer, sequence)
        {
            Unit = unit;
            Goal = goal;
        }

        /// <summary>Machine-readable kind name: "attack_move".</summary>
        public override string Kind => "attack_move";

        /// <summary>Applies the command: replaces any move, attack-move and target with an attack-move to <see cref="Goal"/>.</summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (!sim.Units.TryGet(Unit, out Unit? unit))
            {
                return CommandResult.Reject("unknown_unit");
            }
            if (Issuer.IsValid && unit.Owner != Issuer)
            {
                return CommandResult.Reject("not_owner");
            }
            if (!Simulation.IsWithinWorldBounds(Goal))
            {
                return CommandResult.Reject("out_of_bounds");
            }
            unit.ClearCombatOrders();
            unit.SetAttackMove(Goal);
            unit.SetMoveTarget(Goal);
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Unit);
            hasher.Write(Goal);
        }
    }

    /// <summary>
    /// Orders a unit to attack one specific enemy unit: it chases the target at any distance until it is in weapon
    /// range, then fires. If the target dies the unit carries on to where the target stood when ordered (an attack-move).
    /// </summary>
    public sealed class AttackUnitCommand : Command
    {
        /// <summary>The unit to order. Must exist, belong to the issuer (or the issuer is None) and carry a weapon.</summary>
        public EntityId Unit { get; }

        /// <summary>The unit to attack. Must exist, be an enemy of the ordered unit's owner and be on a layer the weapon can hit.</summary>
        public EntityId Target { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public AttackUnitCommand(int tick, PlayerId issuer, uint sequence, EntityId unit, EntityId target)
            : base(tick, issuer, sequence)
        {
            Unit = unit;
            Target = target;
        }

        /// <summary>Machine-readable kind name: "attack_unit".</summary>
        public override string Kind => "attack_unit";

        /// <summary>
        /// Applies the command. Rejections: unknown_unit, not_owner, unknown_target, no_weapon, target_not_enemy,
        /// cannot_target_layer.
        /// </summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (!sim.Units.TryGet(Unit, out Unit? unit))
            {
                return CommandResult.Reject("unknown_unit");
            }
            if (Issuer.IsValid && unit.Owner != Issuer)
            {
                return CommandResult.Reject("not_owner");
            }
            if (!sim.Units.TryGet(Target, out Unit? target) || target.Id == unit.Id)
            {
                return CommandResult.Reject("unknown_target");
            }
            // The ordered unit's weapon; null for unarmed units.
            WeaponDefinition? weapon = unit.Definition.Weapon;
            if (weapon == null)
            {
                return CommandResult.Reject("no_weapon");
            }
            if (!unit.Owner.IsValid || !target.Owner.IsValid || !sim.Players.AreEnemies(unit.Owner, target.Owner))
            {
                return CommandResult.Reject("target_not_enemy");
            }
            if (!weapon.CanTarget(target.EffectiveLayer))
            {
                return CommandResult.Reject("cannot_target_layer");
            }
            unit.ClearCombatOrders();
            unit.SetAttackTarget(target.Id, true);
            unit.SetAttackMove(target.Position);
            unit.SetMoveTarget(target.Position);
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Unit);
            hasher.Write(Target);
        }
    }

    /// <summary>Orders a unit to stop: cancels its move order, attack-move order and attack target. The unit still shoots enemies already in weapon range.</summary>
    public sealed class StopCommand : Command
    {
        /// <summary>The unit to stop. Must exist and belong to the issuer (or the issuer is None, the script).</summary>
        public EntityId Unit { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public StopCommand(int tick, PlayerId issuer, uint sequence, EntityId unit)
            : base(tick, issuer, sequence)
        {
            Unit = unit;
        }

        /// <summary>Machine-readable kind name: "stop".</summary>
        public override string Kind => "stop";

        /// <summary>Applies the command; rejects with unknown_unit or not_owner.</summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (!sim.Units.TryGet(Unit, out Unit? unit))
            {
                return CommandResult.Reject("unknown_unit");
            }
            if (Issuer.IsValid && unit.Owner != Issuer)
            {
                return CommandResult.Reject("not_owner");
            }
            unit.ClearMoveTarget();
            unit.ClearCombatOrders();
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Unit);
        }
    }
}

// rts/src/Sim/Commands/CoreCommands.cs
// The Phase 0 command set: spawn a unit from a data definition, move a unit, remove a unit, do nothing.
// Part of Rts.Sim.Commands. Spawn and remove exist for tests, scripts and the campaign layer; gameplay
// production (Phase 1) will add build and train commands that go through the economy.
using System;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Sim.Commands
{
    /// <summary>Does nothing when applied. Used by tests and as a network keep-alive placeholder.</summary>
    public sealed class NoOpCommand : Command
    {
        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public NoOpCommand(int tick, PlayerId issuer, uint sequence) : base(tick, issuer, sequence)
        {
        }

        public override string Kind => "noop";

        /// <summary>Applies the command; see the class summary for the rules it checks.</summary>
        public override CommandResult Apply(Simulation sim)
        {
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
        }
    }

    /// <summary>
    /// Spawns one unit of a data-defined type for a player at a position. Only the match script or host
    /// (<see cref="PlayerId.None"/> as issuer) may spawn; a player issuer is rejected, because free spawning would
    /// bypass the economy. Phase 1 adds the player-facing train and build commands.
    /// </summary>
    public sealed class SpawnUnitCommand : Command
    {
        /// <summary>Definition ID from data/units, for example "skai.windrunner". Non-empty.</summary>
        public string DefinitionId { get; }

        /// <summary>Player who will own the unit, or <see cref="PlayerId.None"/> for a neutral unit.</summary>
        public PlayerId Owner { get; }

        /// <summary>Spawn position in tiles. Range: each component within ±<see cref="Simulation.WorldBoundTiles"/>, else rejected.</summary>
        public FixVec2 Position { get; }

        /// <summary>Builds the command from its scheduling fields and parameters. Throws on an empty definition ID so a bad command never reaches a step.</summary>
        public SpawnUnitCommand(int tick, PlayerId issuer, uint sequence, string definitionId, PlayerId owner, FixVec2 position)
            : base(tick, issuer, sequence)
        {
            if (string.IsNullOrEmpty(definitionId))
            {
                throw new ArgumentException("SpawnUnitCommand needs a definition id.", nameof(definitionId));
            }
            DefinitionId = definitionId;
            Owner = owner;
            Position = position;
        }

        public override string Kind => "spawn_unit";

        /// <summary>Applies the command; see the class summary for the rules it checks.</summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (Issuer.IsValid)
            {
                return CommandResult.Reject("spawn_requires_script_issuer");
            }
            if (!sim.Content.Units.TryGetValue(DefinitionId, out UnitDefinition? definition))
            {
                return CommandResult.Reject("unknown_definition");
            }
            if (Owner.IsValid && !sim.Players.Contains(Owner))
            {
                return CommandResult.Reject("unknown_owner");
            }
            if (!Simulation.IsWithinWorldBounds(Position))
            {
                return CommandResult.Reject("out_of_bounds");
            }
            sim.SpawnUnit(definition, Owner, Position);
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(DefinitionId);
            hasher.Write(Owner);
            hasher.Write(Position);
        }
    }

    /// <summary>Orders one unit to move in a straight line to a target. Phase 1 replaces the straight line with pathfinding.</summary>
    public sealed class MoveUnitCommand : Command
    {
        /// <summary>The unit to move. Must exist and belong to the issuer (or the issuer is None, the script).</summary>
        public EntityId Unit { get; }

        /// <summary>Destination in tiles. Range: each component within ±<see cref="Simulation.WorldBoundTiles"/>, else rejected.</summary>
        public FixVec2 Target { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public MoveUnitCommand(int tick, PlayerId issuer, uint sequence, EntityId unit, FixVec2 target)
            : base(tick, issuer, sequence)
        {
            Unit = unit;
            Target = target;
        }

        public override string Kind => "move_unit";

        /// <summary>Applies the command; see the class summary for the rules it checks.</summary>
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
            if (!Simulation.IsWithinWorldBounds(Target))
            {
                return CommandResult.Reject("out_of_bounds");
            }
            unit.SetMoveTarget(Target);
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

    /// <summary>Removes a unit from the match. For scripts and tests; combat deaths (Phase 1) use the same removal path.</summary>
    public sealed class RemoveUnitCommand : Command
    {
        /// <summary>The unit to remove.</summary>
        public EntityId Unit { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public RemoveUnitCommand(int tick, PlayerId issuer, uint sequence, EntityId unit) : base(tick, issuer, sequence)
        {
            Unit = unit;
        }

        public override string Kind => "remove_unit";

        /// <summary>Applies the command; see the class summary for the rules it checks.</summary>
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
            sim.RemoveUnit(Unit, "remove_command");
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

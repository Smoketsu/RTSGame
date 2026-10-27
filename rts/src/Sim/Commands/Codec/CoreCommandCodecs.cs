// rts/src/Sim/Commands/Codec/CoreCommandCodecs.cs
// One codec per faction-neutral command class of rts/src/Sim/Commands: noop, spawn_unit, move_unit, remove_unit, attack_move,
// attack_unit, stop, root_hq, uproot_hq, pick_cycle, gather, train_unit, build_structure, apply_status, surrender and
// set_connection_state. Part of Rts.Sim.Commands.Codec (ADR-008, ADR-009). Each codec writes exactly the command's parameters, in
// the order its constructor takes them, with CommandWriter's explicit primitives; nothing here names a faction. A new core command
// gets a codec here and a line in RegisterAll (the Sim.Tests codec fixture fails for any Command subclass without one).
using Rts.Sim.Core;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;

namespace Rts.Sim.Commands.Codec
{
    /// <summary>Registers the codecs of the faction-neutral command set.</summary>
    public static class CoreCommandCodecs
    {
        /// <summary>Adds every core codec to <paramref name="registry"/>.</summary>
        /// <param name="registry">The registry to fill; must not already hold any of these kinds.</param>
        public static void RegisterAll(CommandCodecRegistry registry)
        {
            registry.Register(new NoOpCodec());
            registry.Register(new SpawnUnitCodec());
            registry.Register(new MoveUnitCodec());
            registry.Register(new RemoveUnitCodec());
            registry.Register(new AttackMoveCodec());
            registry.Register(new AttackUnitCodec());
            registry.Register(new StopCodec());
            registry.Register(new RootHqCodec());
            registry.Register(new UprootHqCodec());
            registry.Register(new PickCycleCodec());
            registry.Register(new GatherCodec());
            registry.Register(new TrainUnitCodec());
            registry.Register(new BuildStructureCodec());
            registry.Register(new ApplyStatusCodec());
            registry.Register(new SurrenderCodec());
            registry.Register(new SetConnectionStateCodec());
        }
    }

    /// <summary>Codec for <see cref="NoOpCommand"/>: no parameters.</summary>
    public sealed class NoOpCodec : CommandCodec<NoOpCommand>
    {
        /// <summary>"noop".</summary>
        public override string Kind => "noop";

        /// <summary>Writes nothing.</summary>
        protected override void Write(NoOpCommand command, CommandWriter writer)
        {
        }

        /// <summary>Builds the command from the scheduling fields alone.</summary>
        protected override NoOpCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new NoOpCommand(tick, issuer, sequence);
        }
    }

    /// <summary>Codec for <see cref="SpawnUnitCommand"/>: definition id, owner, position.</summary>
    public sealed class SpawnUnitCodec : CommandCodec<SpawnUnitCommand>
    {
        /// <summary>"spawn_unit".</summary>
        public override string Kind => "spawn_unit";

        /// <summary>Writes definition id, owner and position.</summary>
        protected override void Write(SpawnUnitCommand command, CommandWriter writer)
        {
            writer.WriteString(command.DefinitionId);
            writer.WritePlayerId(command.Owner);
            writer.WriteFixVec2(command.Position);
        }

        /// <summary>Reads definition id, owner and position.</summary>
        protected override SpawnUnitCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            string definition = reader.ReadString();
            PlayerId owner = reader.ReadPlayerId();
            FixVec2 position = reader.ReadFixVec2();
            return new SpawnUnitCommand(tick, issuer, sequence, definition, owner, position);
        }
    }

    /// <summary>Codec for <see cref="MoveUnitCommand"/>: unit, target.</summary>
    public sealed class MoveUnitCodec : CommandCodec<MoveUnitCommand>
    {
        /// <summary>"move_unit".</summary>
        public override string Kind => "move_unit";

        /// <summary>Writes unit and target.</summary>
        protected override void Write(MoveUnitCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
            writer.WriteFixVec2(command.Target);
        }

        /// <summary>Reads unit and target.</summary>
        protected override MoveUnitCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            FixVec2 target = reader.ReadFixVec2();
            return new MoveUnitCommand(tick, issuer, sequence, unit, target);
        }
    }

    /// <summary>Codec for <see cref="RemoveUnitCommand"/>: unit.</summary>
    public sealed class RemoveUnitCodec : CommandCodec<RemoveUnitCommand>
    {
        /// <summary>"remove_unit".</summary>
        public override string Kind => "remove_unit";

        /// <summary>Writes the unit.</summary>
        protected override void Write(RemoveUnitCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
        }

        /// <summary>Reads the unit.</summary>
        protected override RemoveUnitCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new RemoveUnitCommand(tick, issuer, sequence, reader.ReadEntityId());
        }
    }

    /// <summary>Codec for <see cref="AttackMoveCommand"/>: unit, goal.</summary>
    public sealed class AttackMoveCodec : CommandCodec<AttackMoveCommand>
    {
        /// <summary>"attack_move".</summary>
        public override string Kind => "attack_move";

        /// <summary>Writes unit and goal.</summary>
        protected override void Write(AttackMoveCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
            writer.WriteFixVec2(command.Goal);
        }

        /// <summary>Reads unit and goal.</summary>
        protected override AttackMoveCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            FixVec2 goal = reader.ReadFixVec2();
            return new AttackMoveCommand(tick, issuer, sequence, unit, goal);
        }
    }

    /// <summary>Codec for <see cref="AttackUnitCommand"/>: unit, target.</summary>
    public sealed class AttackUnitCodec : CommandCodec<AttackUnitCommand>
    {
        /// <summary>"attack_unit".</summary>
        public override string Kind => "attack_unit";

        /// <summary>Writes unit and target.</summary>
        protected override void Write(AttackUnitCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
            writer.WriteEntityId(command.Target);
        }

        /// <summary>Reads unit and target.</summary>
        protected override AttackUnitCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            EntityId target = reader.ReadEntityId();
            return new AttackUnitCommand(tick, issuer, sequence, unit, target);
        }
    }

    /// <summary>Codec for <see cref="StopCommand"/>: unit.</summary>
    public sealed class StopCodec : CommandCodec<StopCommand>
    {
        /// <summary>"stop".</summary>
        public override string Kind => "stop";

        /// <summary>Writes the unit.</summary>
        protected override void Write(StopCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
        }

        /// <summary>Reads the unit.</summary>
        protected override StopCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new StopCommand(tick, issuer, sequence, reader.ReadEntityId());
        }
    }

    /// <summary>Codec for <see cref="RootHqCommand"/>: unit.</summary>
    public sealed class RootHqCodec : CommandCodec<RootHqCommand>
    {
        /// <summary>"root_hq".</summary>
        public override string Kind => "root_hq";

        /// <summary>Writes the HQ.</summary>
        protected override void Write(RootHqCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
        }

        /// <summary>Reads the HQ.</summary>
        protected override RootHqCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new RootHqCommand(tick, issuer, sequence, reader.ReadEntityId());
        }
    }

    /// <summary>Codec for <see cref="UprootHqCommand"/>: unit.</summary>
    public sealed class UprootHqCodec : CommandCodec<UprootHqCommand>
    {
        /// <summary>"uproot_hq".</summary>
        public override string Kind => "uproot_hq";

        /// <summary>Writes the HQ.</summary>
        protected override void Write(UprootHqCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
        }

        /// <summary>Reads the HQ.</summary>
        protected override UprootHqCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new UprootHqCommand(tick, issuer, sequence, reader.ReadEntityId());
        }
    }

    /// <summary>Codec for <see cref="PickCycleCommand"/>: cycle ID.</summary>
    public sealed class PickCycleCodec : CommandCodec<PickCycleCommand>
    {
        /// <summary>"pick_cycle".</summary>
        public override string Kind => "pick_cycle";

        /// <summary>Writes the cycle ID.</summary>
        protected override void Write(PickCycleCommand command, CommandWriter writer)
        {
            writer.WriteString(command.CycleId);
        }

        /// <summary>Reads the cycle ID.</summary>
        protected override PickCycleCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new PickCycleCommand(tick, issuer, sequence, reader.ReadString());
        }
    }

    /// <summary>Codec for <see cref="GatherCommand"/>: unit, field index.</summary>
    public sealed class GatherCodec : CommandCodec<GatherCommand>
    {
        /// <summary>"gather".</summary>
        public override string Kind => "gather";

        /// <summary>Writes unit and field index.</summary>
        protected override void Write(GatherCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
            writer.WriteInt32(command.FieldIndex);
        }

        /// <summary>Reads unit and field index.</summary>
        protected override GatherCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            int field = reader.ReadInt32();
            return new GatherCommand(tick, issuer, sequence, unit, field);
        }
    }

    /// <summary>Codec for <see cref="TrainUnitCommand"/>: producer, definition id.</summary>
    public sealed class TrainUnitCodec : CommandCodec<TrainUnitCommand>
    {
        /// <summary>"train_unit".</summary>
        public override string Kind => "train_unit";

        /// <summary>Writes producer and definition id.</summary>
        protected override void Write(TrainUnitCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Producer);
            writer.WriteString(command.DefinitionId);
        }

        /// <summary>Reads producer and definition id.</summary>
        protected override TrainUnitCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId producer = reader.ReadEntityId();
            string definition = reader.ReadString();
            return new TrainUnitCommand(tick, issuer, sequence, producer, definition);
        }
    }

    /// <summary>Codec for <see cref="BuildStructureCommand"/>: worker, definition id, tile x, tile y.</summary>
    public sealed class BuildStructureCodec : CommandCodec<BuildStructureCommand>
    {
        /// <summary>"build_structure".</summary>
        public override string Kind => "build_structure";

        /// <summary>Writes worker, definition id and the site tile.</summary>
        protected override void Write(BuildStructureCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Worker);
            writer.WriteString(command.DefinitionId);
            writer.WriteInt32(command.TileX);
            writer.WriteInt32(command.TileY);
        }

        /// <summary>Reads worker, definition id and the site tile.</summary>
        protected override BuildStructureCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId worker = reader.ReadEntityId();
            string definition = reader.ReadString();
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            return new BuildStructureCommand(tick, issuer, sequence, worker, definition, x, y);
        }
    }

    /// <summary>Codec for <see cref="ApplyStatusCommand"/>: unit, status kind, multiplier, duration, source key.</summary>
    public sealed class ApplyStatusCodec : CommandCodec<ApplyStatusCommand>
    {
        /// <summary>"apply_status".</summary>
        public override string Kind => "apply_status";

        /// <summary>Writes unit, status (one byte), multiplier, duration seconds and source key.</summary>
        protected override void Write(ApplyStatusCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Unit);
            writer.WriteByte((byte)command.Status);
            writer.WriteFix64(command.Multiplier);
            writer.WriteFix64(command.DurationSeconds);
            writer.WriteString(command.SourceKey);
        }

        /// <summary>Reads the fields; an unknown status byte is a <see cref="CommandCodecException"/>.</summary>
        protected override ApplyStatusCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            byte status = reader.ReadByte();
            Fix64 multiplier = reader.ReadFix64();
            Fix64 seconds = reader.ReadFix64();
            string source = reader.ReadString();
            if (status != (byte)StatusKind.Grounded && status != (byte)StatusKind.Slowed)
            {
                throw new CommandCodecException("Unknown status kind byte " + status + " in apply_status.");
            }
            return new ApplyStatusCommand(tick, issuer, sequence, unit, (StatusKind)status, multiplier, seconds, source);
        }
    }

    /// <summary>Codec for <see cref="SurrenderCommand"/>: player.</summary>
    public sealed class SurrenderCodec : CommandCodec<SurrenderCommand>
    {
        /// <summary>"surrender".</summary>
        public override string Kind => "surrender";

        /// <summary>Writes the surrendering player.</summary>
        protected override void Write(SurrenderCommand command, CommandWriter writer)
        {
            writer.WritePlayerId(command.Player);
        }

        /// <summary>Reads the surrendering player.</summary>
        protected override SurrenderCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new SurrenderCommand(tick, issuer, sequence, reader.ReadPlayerId());
        }
    }

    /// <summary>Codec for <see cref="SetConnectionStateCommand"/>: player, connection state (one byte).</summary>
    public sealed class SetConnectionStateCodec : CommandCodec<SetConnectionStateCommand>
    {
        /// <summary>"set_connection_state".</summary>
        public override string Kind => "set_connection_state";

        /// <summary>Writes player and connection state.</summary>
        protected override void Write(SetConnectionStateCommand command, CommandWriter writer)
        {
            writer.WritePlayerId(command.Player);
            writer.WriteByte((byte)command.Connection);
        }

        /// <summary>Reads player and connection state; an unknown state byte is a <see cref="CommandCodecException"/>.</summary>
        protected override SetConnectionStateCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            PlayerId player = reader.ReadPlayerId();
            byte state = reader.ReadByte();
            if (state != (byte)ConnectionState.Connected && state != (byte)ConnectionState.Disconnected)
            {
                throw new CommandCodecException("Unknown connection state byte " + state + " in set_connection_state.");
            }
            return new SetConnectionStateCommand(tick, issuer, sequence, player, (ConnectionState)state);
        }
    }
}

// rts/src/Net/Codecs/CoreCommandCodecs.cs
// One codec per faction-neutral command kind of rts/src/Sim/Commands: noop, spawn_unit, move_unit, remove_unit,
// attack_move, attack_unit, stop, root_hq, uproot_hq, gather, train_unit, build_structure, apply_status, surrender and
// set_connection_state. Part of Rts.Net.Codecs (ADR-008). Each codec writes exactly the command's parameters, in the
// order its constructor takes them, with the writer's explicit primitives; nothing here names a faction. A new core
// command gets a codec here and a line in RegisterAll (the codec round-trip test checks every kind is covered).
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;

namespace Rts.Net.Codecs
{
    /// <summary>Registers the codecs of the faction-neutral command set.</summary>
    public static class CoreCommandCodecs
    {
        /// <summary>Adds every core codec to <paramref name="registry"/>.</summary>
        /// <param name="registry">The registry to fill.</param>
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

        /// <summary>Casts a command to the codec's type or throws a protocol error naming the mismatch.</summary>
        /// <typeparam name="T">The expected command type.</typeparam>
        /// <param name="command">The command handed to the codec.</param>
        /// <param name="kind">The codec's kind, for the message.</param>
        internal static T Expect<T>(Command command, string kind) where T : Command
        {
            if (command is T typed)
            {
                return typed;
            }
            throw new NetProtocolException("Codec '" + kind + "' was given a " + command.Kind + " command.");
        }
    }

    /// <summary>Codec for <see cref="NoOpCommand"/>: no parameters.</summary>
    public sealed class NoOpCodec : ICommandCodec
    {
        /// <summary>"noop".</summary>
        public string Kind => "noop";

        /// <summary>Writes nothing.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            CoreCommandCodecs.Expect<NoOpCommand>(command, Kind);
        }

        /// <summary>Builds the command from the scheduling fields alone.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new NoOpCommand(tick, issuer, sequence);
        }
    }

    /// <summary>Codec for <see cref="SpawnUnitCommand"/>: definition id, owner, position.</summary>
    public sealed class SpawnUnitCodec : ICommandCodec
    {
        /// <summary>"spawn_unit".</summary>
        public string Kind => "spawn_unit";

        /// <summary>Writes definition id, owner and position.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            SpawnUnitCommand spawn = CoreCommandCodecs.Expect<SpawnUnitCommand>(command, Kind);
            writer.WriteString(spawn.DefinitionId);
            writer.WritePlayerId(spawn.Owner);
            writer.WriteFixVec2(spawn.Position);
        }

        /// <summary>Reads definition id, owner and position.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            string definition = reader.ReadRequiredString();
            PlayerId owner = reader.ReadPlayerId();
            FixVec2 position = reader.ReadFixVec2();
            return new SpawnUnitCommand(tick, issuer, sequence, definition, owner, position);
        }
    }

    /// <summary>Codec for <see cref="MoveUnitCommand"/>: unit, target.</summary>
    public sealed class MoveUnitCodec : ICommandCodec
    {
        /// <summary>"move_unit".</summary>
        public string Kind => "move_unit";

        /// <summary>Writes unit and target.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            MoveUnitCommand move = CoreCommandCodecs.Expect<MoveUnitCommand>(command, Kind);
            writer.WriteEntityId(move.Unit);
            writer.WriteFixVec2(move.Target);
        }

        /// <summary>Reads unit and target.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            FixVec2 target = reader.ReadFixVec2();
            return new MoveUnitCommand(tick, issuer, sequence, unit, target);
        }
    }

    /// <summary>Codec for <see cref="RemoveUnitCommand"/>: unit.</summary>
    public sealed class RemoveUnitCodec : ICommandCodec
    {
        /// <summary>"remove_unit".</summary>
        public string Kind => "remove_unit";

        /// <summary>Writes the unit.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            writer.WriteEntityId(CoreCommandCodecs.Expect<RemoveUnitCommand>(command, Kind).Unit);
        }

        /// <summary>Reads the unit.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new RemoveUnitCommand(tick, issuer, sequence, reader.ReadEntityId());
        }
    }

    /// <summary>Codec for <see cref="AttackMoveCommand"/>: unit, goal.</summary>
    public sealed class AttackMoveCodec : ICommandCodec
    {
        /// <summary>"attack_move".</summary>
        public string Kind => "attack_move";

        /// <summary>Writes unit and goal.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            AttackMoveCommand attack = CoreCommandCodecs.Expect<AttackMoveCommand>(command, Kind);
            writer.WriteEntityId(attack.Unit);
            writer.WriteFixVec2(attack.Goal);
        }

        /// <summary>Reads unit and goal.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            FixVec2 goal = reader.ReadFixVec2();
            return new AttackMoveCommand(tick, issuer, sequence, unit, goal);
        }
    }

    /// <summary>Codec for <see cref="AttackUnitCommand"/>: unit, target.</summary>
    public sealed class AttackUnitCodec : ICommandCodec
    {
        /// <summary>"attack_unit".</summary>
        public string Kind => "attack_unit";

        /// <summary>Writes unit and target.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            AttackUnitCommand attack = CoreCommandCodecs.Expect<AttackUnitCommand>(command, Kind);
            writer.WriteEntityId(attack.Unit);
            writer.WriteEntityId(attack.Target);
        }

        /// <summary>Reads unit and target.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            EntityId target = reader.ReadEntityId();
            return new AttackUnitCommand(tick, issuer, sequence, unit, target);
        }
    }

    /// <summary>Codec for <see cref="StopCommand"/>: unit.</summary>
    public sealed class StopCodec : ICommandCodec
    {
        /// <summary>"stop".</summary>
        public string Kind => "stop";

        /// <summary>Writes the unit.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            writer.WriteEntityId(CoreCommandCodecs.Expect<StopCommand>(command, Kind).Unit);
        }

        /// <summary>Reads the unit.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new StopCommand(tick, issuer, sequence, reader.ReadEntityId());
        }
    }

    /// <summary>Codec for <see cref="RootHqCommand"/>: unit.</summary>
    public sealed class RootHqCodec : ICommandCodec
    {
        /// <summary>"root_hq".</summary>
        public string Kind => "root_hq";

        /// <summary>Writes the HQ.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            writer.WriteEntityId(CoreCommandCodecs.Expect<RootHqCommand>(command, Kind).Unit);
        }

        /// <summary>Reads the HQ.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new RootHqCommand(tick, issuer, sequence, reader.ReadEntityId());
        }
    }

    /// <summary>Codec for <see cref="UprootHqCommand"/>: unit.</summary>
    public sealed class UprootHqCodec : ICommandCodec
    {
        /// <summary>"uproot_hq".</summary>
        public string Kind => "uproot_hq";

        /// <summary>Writes the HQ.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            writer.WriteEntityId(CoreCommandCodecs.Expect<UprootHqCommand>(command, Kind).Unit);
        }

        /// <summary>Reads the HQ.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new UprootHqCommand(tick, issuer, sequence, reader.ReadEntityId());
        }
    }

    /// <summary>Codec for <see cref="PickCycleCommand"/>: cycle ID.</summary>
    public sealed class PickCycleCodec : ICommandCodec
    {
        /// <summary>"pick_cycle".</summary>
        public string Kind => "pick_cycle";

        /// <summary>Writes the cycle ID.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            writer.WriteString(CoreCommandCodecs.Expect<PickCycleCommand>(command, Kind).CycleId);
        }

        /// <summary>Reads the cycle ID.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new PickCycleCommand(tick, issuer, sequence, reader.ReadRequiredString());
        }
    }

    /// <summary>Codec for <see cref="GatherCommand"/>: unit, field index.</summary>
    public sealed class GatherCodec : ICommandCodec
    {
        /// <summary>"gather".</summary>
        public string Kind => "gather";

        /// <summary>Writes unit and field index.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            GatherCommand gather = CoreCommandCodecs.Expect<GatherCommand>(command, Kind);
            writer.WriteEntityId(gather.Unit);
            writer.WriteInt32(gather.FieldIndex);
        }

        /// <summary>Reads unit and field index.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            int field = reader.ReadInt32();
            return new GatherCommand(tick, issuer, sequence, unit, field);
        }
    }

    /// <summary>Codec for <see cref="TrainUnitCommand"/>: producer, definition id.</summary>
    public sealed class TrainUnitCodec : ICommandCodec
    {
        /// <summary>"train_unit".</summary>
        public string Kind => "train_unit";

        /// <summary>Writes producer and definition id.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            TrainUnitCommand train = CoreCommandCodecs.Expect<TrainUnitCommand>(command, Kind);
            writer.WriteEntityId(train.Producer);
            writer.WriteString(train.DefinitionId);
        }

        /// <summary>Reads producer and definition id.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId producer = reader.ReadEntityId();
            string definition = reader.ReadRequiredString();
            return new TrainUnitCommand(tick, issuer, sequence, producer, definition);
        }
    }

    /// <summary>Codec for <see cref="BuildStructureCommand"/>: worker, definition id, tile x, tile y.</summary>
    public sealed class BuildStructureCodec : ICommandCodec
    {
        /// <summary>"build_structure".</summary>
        public string Kind => "build_structure";

        /// <summary>Writes worker, definition id and the site tile.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            BuildStructureCommand build = CoreCommandCodecs.Expect<BuildStructureCommand>(command, Kind);
            writer.WriteEntityId(build.Worker);
            writer.WriteString(build.DefinitionId);
            writer.WriteInt32(build.TileX);
            writer.WriteInt32(build.TileY);
        }

        /// <summary>Reads worker, definition id and the site tile.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId worker = reader.ReadEntityId();
            string definition = reader.ReadRequiredString();
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            return new BuildStructureCommand(tick, issuer, sequence, worker, definition, x, y);
        }
    }

    /// <summary>Codec for <see cref="ApplyStatusCommand"/>: unit, status kind, multiplier, duration, source key.</summary>
    public sealed class ApplyStatusCodec : ICommandCodec
    {
        /// <summary>"apply_status".</summary>
        public string Kind => "apply_status";

        /// <summary>Writes unit, status (one byte), multiplier, duration seconds and source key.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            ApplyStatusCommand apply = CoreCommandCodecs.Expect<ApplyStatusCommand>(command, Kind);
            writer.WriteEntityId(apply.Unit);
            writer.WriteByte((byte)apply.Status);
            writer.WriteFix64(apply.Multiplier);
            writer.WriteFix64(apply.DurationSeconds);
            writer.WriteString(apply.SourceKey);
        }

        /// <summary>Reads the fields; an unknown status byte or a malformed source key is a protocol error.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId unit = reader.ReadEntityId();
            byte status = reader.ReadByte();
            Fix64 multiplier = reader.ReadFix64();
            Fix64 seconds = reader.ReadFix64();
            string source = reader.ReadRequiredString();
            if (status != (byte)StatusKind.Grounded && status != (byte)StatusKind.Slowed)
            {
                throw new NetProtocolException("Unknown status kind byte " + status + " in apply_status.");
            }
            return new ApplyStatusCommand(tick, issuer, sequence, unit, (StatusKind)status, multiplier, seconds, source);
        }
    }

    /// <summary>Codec for <see cref="SurrenderCommand"/>: player.</summary>
    public sealed class SurrenderCodec : ICommandCodec
    {
        /// <summary>"surrender".</summary>
        public string Kind => "surrender";

        /// <summary>Writes the surrendering player.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            writer.WritePlayerId(CoreCommandCodecs.Expect<SurrenderCommand>(command, Kind).Player);
        }

        /// <summary>Reads the surrendering player.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return new SurrenderCommand(tick, issuer, sequence, reader.ReadPlayerId());
        }
    }

    /// <summary>Codec for <see cref="SetConnectionStateCommand"/>: player, connection state (one byte).</summary>
    public sealed class SetConnectionStateCodec : ICommandCodec
    {
        /// <summary>"set_connection_state".</summary>
        public string Kind => "set_connection_state";

        /// <summary>Writes player and connection state.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            SetConnectionStateCommand set = CoreCommandCodecs.Expect<SetConnectionStateCommand>(command, Kind);
            writer.WritePlayerId(set.Player);
            writer.WriteByte((byte)set.Connection);
        }

        /// <summary>Reads player and connection state; an unknown state byte is a protocol error.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            PlayerId player = reader.ReadPlayerId();
            byte state = reader.ReadByte();
            if (state != (byte)ConnectionState.Connected && state != (byte)ConnectionState.Disconnected)
            {
                throw new NetProtocolException("Unknown connection state byte " + state + " in set_connection_state.");
            }
            return new SetConnectionStateCommand(tick, issuer, sequence, player, (ConnectionState)state);
        }
    }
}

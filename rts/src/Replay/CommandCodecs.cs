// rts/src/Replay/CommandCodecs.cs
// The per-command-type binary codecs of the replay format and the registry that finds them by command kind. Part of Rts.Replay.
// One explicit codec per command in rts/src/Sim/Commands plus the Water faction's cast_wave. A codec writes only the command's own
// parameters (the replay writer adds tick, issuer, sequence, kind and a payload length around it), in a fixed field order, and
// reads them back into the same command object. Faction plug-ins that add a command kind register their own codec on the
// registry (CommandCodecRegistry.Register) before reading or writing replays. Self-contained on purpose: the lockstep network
// layer has its own command codec, and the two should be unified later (ADR-009, Consequences). Little-endian layout: ReplayBinary.cs.
using System;
using System.Collections.Generic;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Factions.Water;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;

namespace Rts.Replay
{
    /// <summary>Reads and writes the parameters of one command kind. Implementations are stateless and deterministic.</summary>
    public interface ICommandCodec
    {
        /// <summary>The command kind this codec handles, equal to <see cref="Command.Kind"/> (for example "move_unit"). Range: non-empty snake_case.</summary>
        string Kind { get; }

        /// <summary>Writes the command's own parameters (not tick, issuer, sequence or kind) to <paramref name="writer"/>.</summary>
        /// <param name="writer">Destination buffer.</param>
        /// <param name="command">The command; its runtime type must be the one this codec handles, else ArgumentException.</param>
        void Write(ReplayBinaryWriter writer, Command command);

        /// <summary>Reads the parameters written by <see cref="Write"/> and builds the command.</summary>
        /// <param name="reader">Reader over exactly this command's payload.</param>
        /// <param name="tick">Scheduled tick read by the replay reader. Unit: ticks. Range: 0 upward.</param>
        /// <param name="issuer">Issuer read by the replay reader.</param>
        /// <param name="sequence">Sequence read by the replay reader. Range: 0 upward.</param>
        Command Read(ReplayBinaryReader reader, int tick, PlayerId issuer, uint sequence);
    }

    /// <summary>A codec for command type <typeparamref name="T"/> built from two functions.</summary>
    /// <typeparam name="T">The command class this codec handles.</typeparam>
    public sealed class CommandCodec<T> : ICommandCodec where T : Command
    {
        /// <summary>Function that writes the parameters of one command.</summary>
        private readonly Action<ReplayBinaryWriter, T> _write;

        /// <summary>Function that reads the parameters and builds the command from the scheduling fields.</summary>
        private readonly Func<ReplayBinaryReader, int, PlayerId, uint, T> _read;

        /// <summary>The command kind handled. Range: non-empty snake_case.</summary>
        public string Kind { get; }

        /// <summary>Creates the codec.</summary>
        /// <param name="kind">Command kind. Range: non-empty.</param>
        /// <param name="write">Writes the parameters.</param>
        /// <param name="read">Reads the parameters (reader, tick, issuer, sequence) and builds the command.</param>
        public CommandCodec(string kind, Action<ReplayBinaryWriter, T> write, Func<ReplayBinaryReader, int, PlayerId, uint, T> read)
        {
            if (string.IsNullOrEmpty(kind))
            {
                throw new ArgumentException("A codec needs a command kind.", nameof(kind));
            }
            Kind = kind;
            _write = write ?? throw new ArgumentNullException(nameof(write));
            _read = read ?? throw new ArgumentNullException(nameof(read));
        }

        /// <summary>Writes the command's parameters; throws ArgumentException when the command is not a <typeparamref name="T"/>.</summary>
        public void Write(ReplayBinaryWriter writer, Command command)
        {
            if (!(command is T typed))
            {
                throw new ArgumentException("Codec '" + Kind + "' cannot write a " + command.GetType().Name + ".", nameof(command));
            }
            _write(writer, typed);
        }

        /// <summary>Reads the command's parameters and builds it.</summary>
        public Command Read(ReplayBinaryReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return _read(reader, tick, issuer, sequence);
        }
    }

    /// <summary>Codecs by command kind, in ordinal kind order. Faction plug-ins add their kinds with <see cref="Register"/>.</summary>
    public sealed class CommandCodecRegistry
    {
        /// <summary>Codecs keyed by kind, ordinal order.</summary>
        private readonly SortedDictionary<string, ICommandCodec> _codecs = new SortedDictionary<string, ICommandCodec>(StringComparer.Ordinal);

        /// <summary>The registered kinds in ordinal order.</summary>
        public IEnumerable<string> Kinds => _codecs.Keys;

        /// <summary>Adds a codec. Throws ArgumentException when its kind already has one, so two plug-ins cannot silently share a kind.</summary>
        /// <param name="codec">The codec to add. Range: not null.</param>
        public void Register(ICommandCodec codec)
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }
            if (!_codecs.TryAdd(codec.Kind, codec))
            {
                throw new ArgumentException("A replay codec for command kind '" + codec.Kind + "' is already registered.", nameof(codec));
            }
        }

        /// <summary>Finds the codec for a kind.</summary>
        /// <param name="kind">Command kind.</param>
        /// <param name="codec">The codec when found.</param>
        public bool TryGet(string kind, out ICommandCodec? codec)
        {
            return _codecs.TryGetValue(kind, out codec);
        }

        /// <summary>
        /// A registry with a codec for every command that ships with the game: the core and Phase 1 commands of rts/src/Sim/Commands
        /// and the Water faction's cast_wave. A faction plug-in with its own command adds a codec to the returned registry.
        /// </summary>
        public static CommandCodecRegistry CreateBuiltIn()
        {
            // The registry being filled.
            CommandCodecRegistry registry = new CommandCodecRegistry();

            registry.Register(new CommandCodec<NoOpCommand>("noop",
                (w, c) => { },
                (r, tick, issuer, seq) => new NoOpCommand(tick, issuer, seq)));

            // spawn_unit: definition ID string, owner byte, position vector.
            registry.Register(new CommandCodec<SpawnUnitCommand>("spawn_unit",
                (w, c) => { w.WriteString(c.DefinitionId); w.WritePlayer(c.Owner); w.WriteVec(c.Position); },
                (r, tick, issuer, seq) =>
                {
                    // Definition ID read first, then the owner and the position, matching the write order.
                    string definitionId = r.ReadString();
                    return new SpawnUnitCommand(tick, issuer, seq, definitionId, r.ReadPlayer(), r.ReadVec());
                }));

            // move_unit: unit ID, target vector.
            registry.Register(new CommandCodec<MoveUnitCommand>("move_unit",
                (w, c) => { w.WriteEntity(c.Unit); w.WriteVec(c.Target); },
                (r, tick, issuer, seq) =>
                {
                    // Unit read before the target, matching the write order.
                    EntityId unit = r.ReadEntity();
                    return new MoveUnitCommand(tick, issuer, seq, unit, r.ReadVec());
                }));

            // remove_unit: unit ID.
            registry.Register(new CommandCodec<RemoveUnitCommand>("remove_unit",
                (w, c) => w.WriteEntity(c.Unit),
                (r, tick, issuer, seq) => new RemoveUnitCommand(tick, issuer, seq, r.ReadEntity())));

            // attack_move: unit ID, goal vector.
            registry.Register(new CommandCodec<AttackMoveCommand>("attack_move",
                (w, c) => { w.WriteEntity(c.Unit); w.WriteVec(c.Goal); },
                (r, tick, issuer, seq) =>
                {
                    // Unit read before the goal, matching the write order.
                    EntityId unit = r.ReadEntity();
                    return new AttackMoveCommand(tick, issuer, seq, unit, r.ReadVec());
                }));

            // attack_unit: attacker ID, target ID.
            registry.Register(new CommandCodec<AttackUnitCommand>("attack_unit",
                (w, c) => { w.WriteEntity(c.Unit); w.WriteEntity(c.Target); },
                (r, tick, issuer, seq) =>
                {
                    // Attacker read before the target, matching the write order.
                    EntityId unit = r.ReadEntity();
                    return new AttackUnitCommand(tick, issuer, seq, unit, r.ReadEntity());
                }));

            // stop: unit ID.
            registry.Register(new CommandCodec<StopCommand>("stop",
                (w, c) => w.WriteEntity(c.Unit),
                (r, tick, issuer, seq) => new StopCommand(tick, issuer, seq, r.ReadEntity())));

            // gather: worker ID, resource field index (32-bit signed).
            registry.Register(new CommandCodec<GatherCommand>("gather",
                (w, c) => { w.WriteEntity(c.Unit); w.WriteI32(c.FieldIndex); },
                (r, tick, issuer, seq) =>
                {
                    // Worker read before the field index, matching the write order.
                    EntityId unit = r.ReadEntity();
                    return new GatherCommand(tick, issuer, seq, unit, r.ReadI32());
                }));

            // train_unit: producing structure ID, unit definition ID string.
            registry.Register(new CommandCodec<TrainUnitCommand>("train_unit",
                (w, c) => { w.WriteEntity(c.Producer); w.WriteString(c.DefinitionId); },
                (r, tick, issuer, seq) =>
                {
                    // Producer read before the definition, matching the write order.
                    EntityId producer = r.ReadEntity();
                    return new TrainUnitCommand(tick, issuer, seq, producer, r.ReadString());
                }));

            // build_structure: worker ID, structure definition ID string, tile X and tile Y (32-bit signed each).
            registry.Register(new CommandCodec<BuildStructureCommand>("build_structure",
                (w, c) => { w.WriteEntity(c.Worker); w.WriteString(c.DefinitionId); w.WriteI32(c.TileX); w.WriteI32(c.TileY); },
                (r, tick, issuer, seq) =>
                {
                    // Fields read in the write order: worker, definition, tile X, tile Y.
                    EntityId worker = r.ReadEntity();
                    string definitionId = r.ReadString();
                    int tileX = r.ReadI32();
                    int tileY = r.ReadI32();
                    return new BuildStructureCommand(tick, issuer, seq, worker, definitionId, tileX, tileY);
                }));

            // root_hq: HQ unit ID.
            registry.Register(new CommandCodec<RootHqCommand>("root_hq",
                (w, c) => w.WriteEntity(c.Unit),
                (r, tick, issuer, seq) => new RootHqCommand(tick, issuer, seq, r.ReadEntity())));

            // uproot_hq: HQ unit ID.
            registry.Register(new CommandCodec<UprootHqCommand>("uproot_hq",
                (w, c) => w.WriteEntity(c.Unit),
                (r, tick, issuer, seq) => new UprootHqCommand(tick, issuer, seq, r.ReadEntity())));

            // apply_status: unit ID, status kind byte, multiplier (raw Fix64), duration seconds (raw Fix64), source key string.
            registry.Register(new CommandCodec<ApplyStatusCommand>("apply_status",
                (w, c) => { w.WriteEntity(c.Unit); w.WriteU8((byte)c.Status); w.WriteFix(c.Multiplier); w.WriteFix(c.DurationSeconds); w.WriteString(c.SourceKey); },
                (r, tick, issuer, seq) =>
                {
                    // Fields read in the write order: unit, status kind, multiplier, duration, source key.
                    EntityId unit = r.ReadEntity();
                    byte statusByte = r.ReadU8();
                    if (statusByte > (byte)StatusKind.Slowed)
                    {
                        throw new ReplayFormatException("apply_status holds the unknown status kind " + statusByte);
                    }
                    Fix64 multiplier = r.ReadFix();
                    Fix64 duration = r.ReadFix();
                    return new ApplyStatusCommand(tick, issuer, seq, unit, (StatusKind)statusByte, multiplier, duration, r.ReadString());
                }));

            // pick_cycle: cycle ID string.
            registry.Register(new CommandCodec<PickCycleCommand>("pick_cycle",
                (w, c) => w.WriteString(c.CycleId),
                (r, tick, issuer, seq) => new PickCycleCommand(tick, issuer, seq, r.ReadString())));

            // surrender: surrendering player byte.
            registry.Register(new CommandCodec<SurrenderCommand>("surrender",
                (w, c) => w.WritePlayer(c.Player),
                (r, tick, issuer, seq) => new SurrenderCommand(tick, issuer, seq, r.ReadPlayer())));

            // set_connection_state: player byte, connection state byte (0 connected, 1 disconnected).
            registry.Register(new CommandCodec<SetConnectionStateCommand>("set_connection_state",
                (w, c) => { w.WritePlayer(c.Player); w.WriteU8((byte)c.Connection); },
                (r, tick, issuer, seq) =>
                {
                    // Player read before the connection state, matching the write order.
                    PlayerId player = r.ReadPlayer();
                    byte stateByte = r.ReadU8();
                    if (stateByte > (byte)ConnectionState.Disconnected)
                    {
                        throw new ReplayFormatException("set_connection_state holds the unknown connection state " + stateByte);
                    }
                    return new SetConnectionStateCommand(tick, issuer, seq, player, (ConnectionState)stateByte);
                }));

            // cast_wave (Water): caster ID, target vector.
            registry.Register(new CommandCodec<CastWaveCommand>(CastWaveCommand.KindName,
                (w, c) => { w.WriteEntity(c.Caster); w.WriteVec(c.Target); },
                (r, tick, issuer, seq) =>
                {
                    // Caster read before the target, matching the write order.
                    EntityId caster = r.ReadEntity();
                    return new CastWaveCommand(tick, issuer, seq, caster, r.ReadVec());
                }));

            return registry;
        }
    }
}

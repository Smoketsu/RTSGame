// rts/src/Sim/Commands/Codec/CommandCodecRegistry.cs
// Codecs by command kind and by command class. Part of Rts.Sim.Commands.Codec (ADR-008, ADR-009). The one registry the lockstep
// protocol and the replay format both use: hosts build it with BuiltInMechanics.CreateCodecRegistry (core codecs plus the
// built-in faction codecs) and may register more. Kinds iterate in ascending ordinal order. Duplicates (kind or class) throw at
// registration; every lookup failure names the kind or type. Encoding turns a command into its payload bytes and decoding turns
// (kind, scheduling fields, payload) back into the command; the framing around them belongs to the caller.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Commands.Codec
{
    /// <summary>Codecs indexed by kind and by command class.</summary>
    public sealed class CommandCodecRegistry
    {
        /// <summary>Codecs by kind, ascending ordinal. This is the only collection that is ever enumerated.</summary>
        private readonly SortedDictionary<string, ICommandCodec> _byKind = new SortedDictionary<string, ICommandCodec>(StringComparer.Ordinal);

        /// <summary>Codecs by exact command class. Lookup only: never enumerated, so its order cannot reach any output.</summary>
        private readonly Dictionary<Type, ICommandCodec> _byType = new Dictionary<Type, ICommandCodec>();

        /// <summary>Registered kinds in ascending ordinal order.</summary>
        public IEnumerable<string> Kinds => _byKind.Keys;

        /// <summary>Registered codecs in ascending ordinal kind order.</summary>
        public IEnumerable<ICommandCodec> Codecs => _byKind.Values;

        /// <summary>Number of registered codecs. Range: 0 upward.</summary>
        public int Count => _byKind.Count;

        /// <summary>
        /// Adds a codec. Throws <see cref="ArgumentException"/> for a codec with an empty kind or a command type that is not a concrete
        /// <see cref="Command"/> class, and <see cref="InvalidOperationException"/> when its kind or its command type is already registered.
        /// </summary>
        /// <param name="codec">The codec to add. Range: not null.</param>
        public void Register(ICommandCodec codec)
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }
            if (string.IsNullOrEmpty(codec.Kind))
            {
                throw new ArgumentException("A command codec needs a non-empty Kind.", nameof(codec));
            }
            if (codec.CommandType == null || codec.CommandType.IsAbstract || !typeof(Command).IsAssignableFrom(codec.CommandType))
            {
                throw new ArgumentException("Codec '" + codec.Kind + "' must name a concrete Command class as its CommandType.", nameof(codec));
            }
            if (_byKind.ContainsKey(codec.Kind))
            {
                throw new InvalidOperationException("A codec for command kind '" + codec.Kind + "' is already registered.");
            }
            if (_byType.ContainsKey(codec.CommandType))
            {
                throw new InvalidOperationException("A codec for command type '" + codec.CommandType.FullName + "' is already registered.");
            }
            _byKind.Add(codec.Kind, codec);
            _byType.Add(codec.CommandType, codec);
        }

        /// <summary>True when a codec for <paramref name="kind"/> is registered.</summary>
        /// <param name="kind">A command kind name.</param>
        public bool Contains(string kind)
        {
            return kind != null && _byKind.ContainsKey(kind);
        }

        /// <summary>Finds the codec for a kind.</summary>
        /// <param name="kind">A command kind name.</param>
        /// <param name="codec">The codec when found, else null.</param>
        public bool TryGetByKind(string kind, out ICommandCodec? codec)
        {
            if (kind == null)
            {
                codec = null;
                return false;
            }
            return _byKind.TryGetValue(kind, out codec);
        }

        /// <summary>Finds the codec for an exact command class.</summary>
        /// <param name="type">A command class.</param>
        /// <param name="codec">The codec when found, else null.</param>
        public bool TryGetByType(Type type, out ICommandCodec? codec)
        {
            if (type == null)
            {
                codec = null;
                return false;
            }
            return _byType.TryGetValue(type, out codec);
        }

        /// <summary>The codec for a kind; throws <see cref="CommandCodecException"/> naming the kind when there is none.</summary>
        /// <param name="kind">A command kind name.</param>
        public ICommandCodec GetByKind(string kind)
        {
            if (!TryGetByKind(kind, out ICommandCodec? codec) || codec == null)
            {
                throw new CommandCodecException("No codec registered for command kind '" + kind + "'.");
            }
            return codec;
        }

        /// <summary>The codec for an exact command class; throws <see cref="CommandCodecException"/> naming the type when there is none.</summary>
        /// <param name="type">A command class.</param>
        public ICommandCodec GetByType(Type type)
        {
            if (!TryGetByType(type, out ICommandCodec? codec) || codec == null)
            {
                throw new CommandCodecException("No codec registered for command type '" + (type == null ? "null" : type.FullName) + "'.");
            }
            return codec;
        }

        /// <summary>
        /// Encodes a command's parameters. Throws <see cref="CommandCodecException"/> for a command class with no codec, for a codec whose
        /// kind differs from the command's, and for any string that breaks the encoding rules (null, lone surrogate, over 65,535 UTF-8 bytes).
        /// </summary>
        /// <param name="command">The command; not null.</param>
        public byte[] EncodePayload(Command command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            // The codec for the command's class.
            ICommandCodec codec = GetByType(command.GetType());
            if (!string.Equals(codec.Kind, command.Kind, StringComparison.Ordinal))
            {
                throw new CommandCodecException("The codec for " + command.GetType().FullName + " is for kind '" + codec.Kind + "' but the command reports kind '" + command.Kind + "'.");
            }
            // Payload being built.
            CommandWriter writer = new CommandWriter();
            codec.Encode(command, writer);
            return writer.ToArray();
        }

        /// <summary>
        /// Checks that a command can be encoded without keeping the bytes. Hosts call it before sending or recording a local command, so
        /// a command that would later fail to encode (a string over the limit, a lone surrogate, no codec) is rejected up front.
        /// </summary>
        /// <param name="command">The command; not null.</param>
        public void Validate(Command command)
        {
            EncodePayload(command);
        }

        /// <summary>
        /// Builds a command from its framing fields and payload. Throws <see cref="CommandCodecException"/> for an unknown kind, a short or
        /// over-long payload, invalid text, a bad enum byte, or fields the command constructor rejects.
        /// </summary>
        /// <param name="kind">Command kind read from the framing.</param>
        /// <param name="tick">Tick read from the framing. Unit: ticks. Range: 0 upward, else the constructor rejects it.</param>
        /// <param name="issuer">Issuer read from the framing.</param>
        /// <param name="sequence">Sequence read from the framing. Range: any uint.</param>
        /// <param name="payload">Exactly this command's parameter bytes; not null.</param>
        public Command DecodePayload(string kind, int tick, PlayerId issuer, uint sequence, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
            // The codec for the kind.
            ICommandCodec codec = GetByKind(kind);
            // Reader over the payload.
            CommandReader reader = new CommandReader(payload);
            try
            {
                // The decoded command.
                Command command = codec.Decode(reader, tick, issuer, sequence);
                reader.RequireEnd();
                return command;
            }
            catch (ArgumentException ex)
            {
                throw new CommandCodecException("Command '" + kind + "' rejected its fields: " + ex.Message, ex);
            }
            catch (CommandCodecException ex)
            {
                throw new CommandCodecException("Command '" + kind + "': " + ex.Message, ex);
            }
        }
    }
}

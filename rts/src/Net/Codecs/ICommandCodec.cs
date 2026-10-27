// rts/src/Net/Codecs/ICommandCodec.cs
// The per-kind command codec contract and the registry that maps a command's Kind to its codec. Part of Rts.Net.Codecs
// (ADR-008). Mirrors IScriptCommandParser in the simulation: the core registry knows only the faction-neutral kinds,
// and a faction plug-in's command (the Water cast_wave) ships its own codec that hosts register by kind, so this
// assembly's core names no faction (BuiltInCodecs is the composition root). Wire layout of one command, written by
// the registry: kind (string), tick (int32), issuer (player id byte), sequence (uint32), then the codec's parameters.
using System;
using System.Collections.Generic;
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Net.Codecs
{
    /// <summary>Encodes and decodes the parameters of one command kind. Stateless.</summary>
    public interface ICommandCodec
    {
        /// <summary>The <see cref="Command.Kind"/> this codec handles, such as "move_unit". Range: non-empty, unique in a registry.</summary>
        string Kind { get; }

        /// <summary>Writes the command's parameters (not its tick, issuer or sequence) in a fixed order.</summary>
        /// <param name="command">A command whose Kind equals <see cref="Kind"/>.</param>
        /// <param name="writer">The message being built.</param>
        void Encode(Command command, NetWriter writer);

        /// <summary>Reads the parameters written by <see cref="Encode"/> and builds the command with the given scheduling fields.</summary>
        /// <param name="reader">Positioned at the parameters.</param>
        /// <param name="tick">Tick the command applies at. Unit: ticks. Range: 0 upward.</param>
        /// <param name="issuer">Issuing player slot or None.</param>
        /// <param name="sequence">Per-issuer sequence number. Range: any uint.</param>
        Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence);
    }

    /// <summary>Codecs by kind in ordinal order. Hosts build one with <see cref="BuiltInCodecs.CreateRegistry"/> and may register more.</summary>
    public sealed class CommandCodecRegistry
    {
        /// <summary>Codecs by kind, ascending ordinal.</summary>
        private readonly SortedDictionary<string, ICommandCodec> _byKind = new SortedDictionary<string, ICommandCodec>(StringComparer.Ordinal);

        /// <summary>Registered kinds in ascending ordinal order.</summary>
        public IEnumerable<string> Kinds => _byKind.Keys;

        /// <summary>Number of registered codecs. Range: 0 upward.</summary>
        public int Count => _byKind.Count;

        /// <summary>Adds a codec. A second codec for the same kind throws.</summary>
        /// <param name="codec">The codec; its Kind must be non-empty.</param>
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
            if (_byKind.ContainsKey(codec.Kind))
            {
                throw new InvalidOperationException("A codec for command kind '" + codec.Kind + "' is already registered.");
            }
            _byKind.Add(codec.Kind, codec);
        }

        /// <summary>True when a codec for <paramref name="kind"/> is registered.</summary>
        /// <param name="kind">A command kind name.</param>
        public bool Contains(string kind)
        {
            return _byKind.ContainsKey(kind);
        }

        /// <summary>Writes a whole command: kind, tick, issuer, sequence, parameters. Throws <see cref="NetProtocolException"/> for a kind with no codec.</summary>
        /// <param name="command">The command to send.</param>
        /// <param name="writer">The message being built.</param>
        public void Encode(Command command, NetWriter writer)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if (!_byKind.TryGetValue(command.Kind, out ICommandCodec? codec))
            {
                throw new NetProtocolException("No codec registered for command kind '" + command.Kind + "'.");
            }
            writer.WriteString(command.Kind);
            writer.WriteInt32(command.Tick);
            writer.WritePlayerId(command.Issuer);
            writer.WriteUInt32(command.Sequence);
            codec.Encode(command, writer);
        }

        /// <summary>Reads a whole command written by <see cref="Encode"/>. Unknown kinds and fields a command constructor rejects become <see cref="NetProtocolException"/>.</summary>
        /// <param name="reader">Positioned at the kind string.</param>
        public Command Decode(NetReader reader)
        {
            // The kind name as written. Range: a registered kind.
            string kind = reader.ReadRequiredString();
            if (!_byKind.TryGetValue(kind, out ICommandCodec? codec))
            {
                throw new NetProtocolException("No codec registered for command kind '" + kind + "'.");
            }
            // Scheduling fields in wire order.
            int tick = reader.ReadInt32();
            PlayerId issuer = reader.ReadPlayerId();
            uint sequence = reader.ReadUInt32();
            try
            {
                return codec.Decode(reader, tick, issuer, sequence);
            }
            catch (ArgumentException ex)
            {
                throw new NetProtocolException("Command '" + kind + "' rejected its fields: " + ex.Message, ex);
            }
        }
    }
}

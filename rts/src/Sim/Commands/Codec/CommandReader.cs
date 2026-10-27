// rts/src/Sim/Commands/Codec/CommandReader.cs
// Explicit little-endian byte reader for the parameters of one command, the mirror of CommandWriter. Part of
// Rts.Sim.Commands.Codec (ADR-008, ADR-009). Every read checks that enough bytes remain and throws CommandCodecException
// otherwise, so a truncated or hostile payload can never index past the buffer or allocate a giant string: a string length is a
// uint16 and is checked against the bytes that remain. Strings must be strict UTF-8; invalid bytes are an error, never replaced.
using System;
using System.Text;
using Rts.Sim.Core;

namespace Rts.Sim.Commands.Codec
{
    /// <summary>Reads command parameters from a byte array in the order <see cref="CommandWriter"/> wrote them.</summary>
    public sealed class CommandReader
    {
        /// <summary>Strict UTF-8 decoder: no byte order mark handling, throws on invalid sequences. Range: one shared stateless instance.</summary>
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>The payload bytes being read.</summary>
        private readonly byte[] _bytes;

        /// <summary>Index of the next byte to read. Unit: bytes. Range: 0 to the buffer length.</summary>
        private int _position;

        /// <summary>Bytes not yet read. Unit: bytes. Range: 0 to the buffer length.</summary>
        public int Remaining => _bytes.Length - _position;

        /// <summary>Wraps a payload for reading from its start.</summary>
        /// <param name="bytes">The payload; not null, not copied.</param>
        public CommandReader(byte[] bytes)
        {
            _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            _position = 0;
        }

        /// <summary>Throws unless at least <paramref name="count"/> bytes remain.</summary>
        /// <param name="count">Bytes about to be read. Unit: bytes. Range: 0 upward.</param>
        private void Require(int count)
        {
            if (count < 0 || Remaining < count)
            {
                throw new CommandCodecException("Command payload truncated: needed " + count + " bytes at offset " + _position + " of " + _bytes.Length + ".");
            }
        }

        /// <summary>Reads one byte.</summary>
        public byte ReadByte()
        {
            Require(1);
            return _bytes[_position++];
        }

        /// <summary>Reads a 16-bit unsigned value, least significant byte first.</summary>
        public ushort ReadUInt16()
        {
            Require(2);
            // The value assembled from two bytes.
            ushort value = (ushort)(_bytes[_position] | (_bytes[_position + 1] << 8));
            _position += 2;
            return value;
        }

        /// <summary>Reads a 32-bit unsigned value, least significant byte first.</summary>
        public uint ReadUInt32()
        {
            Require(4);
            // The value assembled from four bytes.
            uint value = _bytes[_position] | ((uint)_bytes[_position + 1] << 8) | ((uint)_bytes[_position + 2] << 16) | ((uint)_bytes[_position + 3] << 24);
            _position += 4;
            return value;
        }

        /// <summary>Reads a 32-bit signed value.</summary>
        public int ReadInt32()
        {
            return unchecked((int)ReadUInt32());
        }

        /// <summary>Reads a 64-bit unsigned value, least significant byte first.</summary>
        public ulong ReadUInt64()
        {
            // Low and high halves, in wire order.
            uint low = ReadUInt32();
            uint high = ReadUInt32();
            return low | ((ulong)high << 32);
        }

        /// <summary>Reads a 64-bit signed value.</summary>
        public long ReadInt64()
        {
            return unchecked((long)ReadUInt64());
        }

        /// <summary>Reads a fixed-point value from its raw bits.</summary>
        public Fix64 ReadFix64()
        {
            return Fix64.FromRaw(ReadInt64());
        }

        /// <summary>Reads a vector written as X then Y.</summary>
        public FixVec2 ReadFixVec2()
        {
            // X then Y, in wire order. Unit: tiles.
            Fix64 x = ReadFix64();
            Fix64 y = ReadFix64();
            return new FixVec2(x, y);
        }

        /// <summary>Reads an entity ID.</summary>
        public EntityId ReadEntityId()
        {
            return new EntityId(ReadUInt32());
        }

        /// <summary>Reads a player ID.</summary>
        public PlayerId ReadPlayerId()
        {
            return new PlayerId(ReadByte());
        }

        /// <summary>Reads a string written by <see cref="CommandWriter.WriteString"/>. A length beyond the remaining bytes or invalid UTF-8 throws <see cref="CommandCodecException"/>.</summary>
        public string ReadString()
        {
            // Byte length of the text. Unit: bytes. Range: 0 to 65535.
            int length = ReadUInt16();
            Require(length);
            try
            {
                // Decoded text. Range: any valid Unicode text.
                string text = StrictUtf8.GetString(_bytes, _position, length);
                _position += length;
                return text;
            }
            catch (DecoderFallbackException)
            {
                throw new CommandCodecException("A command string at offset " + _position + " is not valid UTF-8.");
            }
        }

        /// <summary>Throws unless every byte has been read, so a payload with trailing bytes is rejected rather than silently accepted.</summary>
        public void RequireEnd()
        {
            if (Remaining != 0)
            {
                throw new CommandCodecException("Command payload has " + Remaining + " unexpected trailing bytes.");
            }
        }
    }
}

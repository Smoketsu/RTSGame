// rts/src/Sim/Commands/Codec/CommandWriter.cs
// Explicit little-endian byte writer for the parameters of one command. Part of Rts.Sim.Commands.Codec (ADR-008, ADR-009).
// This is the single command encoding used by the lockstep protocol and by replay files: every field is written by a named
// call in a documented order, so the byte layout is the code and cannot differ between the two. Strings are strict UTF-8 with a
// uint16 byte length (at most 65,535 bytes); a string with a lone surrogate or too many bytes throws CommandCodecException.
// No floating point, no clock, no engine; the output depends only on the values written. CommandReader is the mirror.
using System;
using System.Collections.Generic;
using System.Text;
using Rts.Sim.Core;

namespace Rts.Sim.Commands.Codec
{
    /// <summary>Appends command parameters to a growing byte buffer. Create one per command; read the bytes with <see cref="ToArray"/>.</summary>
    public sealed class CommandWriter
    {
        /// <summary>Longest string a command may hold. Unit: UTF-8 bytes. Range: fixed at 65535, the largest uint16 length prefix.</summary>
        public const int MaxStringBytes = 65535;

        /// <summary>Strict UTF-8 encoder: no byte order mark, throws on lone surrogates. Range: one shared stateless instance.</summary>
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>The bytes written so far, in order.</summary>
        private readonly List<byte> _bytes = new List<byte>();

        /// <summary>Number of bytes written so far. Unit: bytes. Range: 0 upward.</summary>
        public int Length => _bytes.Count;

        /// <summary>Writes one byte.</summary>
        /// <param name="value">Any byte.</param>
        public void WriteByte(byte value)
        {
            _bytes.Add(value);
        }

        /// <summary>Writes a 16-bit unsigned value, least significant byte first.</summary>
        /// <param name="value">Any ushort.</param>
        public void WriteUInt16(ushort value)
        {
            _bytes.Add((byte)value);
            _bytes.Add((byte)(value >> 8));
        }

        /// <summary>Writes a 32-bit unsigned value, least significant byte first.</summary>
        /// <param name="value">Any uint.</param>
        public void WriteUInt32(uint value)
        {
            _bytes.Add((byte)value);
            _bytes.Add((byte)(value >> 8));
            _bytes.Add((byte)(value >> 16));
            _bytes.Add((byte)(value >> 24));
        }

        /// <summary>Writes a 32-bit signed value as its two's-complement bytes, least significant first.</summary>
        /// <param name="value">Any int.</param>
        public void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        /// <summary>Writes a 64-bit unsigned value, least significant byte first.</summary>
        /// <param name="value">Any ulong.</param>
        public void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)value);
            WriteUInt32((uint)(value >> 32));
        }

        /// <summary>Writes a 64-bit signed value as its two's-complement bytes, least significant first.</summary>
        /// <param name="value">Any long.</param>
        public void WriteInt64(long value)
        {
            WriteUInt64(unchecked((ulong)value));
        }

        /// <summary>Writes a fixed-point value through its raw bits (ADR-001).</summary>
        /// <param name="value">Any Fix64.</param>
        public void WriteFix64(Fix64 value)
        {
            WriteInt64(value.Raw);
        }

        /// <summary>Writes a vector as X then Y.</summary>
        /// <param name="value">Any vector. Unit: tiles.</param>
        public void WriteFixVec2(FixVec2 value)
        {
            WriteFix64(value.X);
            WriteFix64(value.Y);
        }

        /// <summary>Writes an entity ID as its 32-bit number.</summary>
        /// <param name="value">Any entity ID, None included.</param>
        public void WriteEntityId(EntityId value)
        {
            WriteUInt32(value.Value);
        }

        /// <summary>Writes a player ID as one byte.</summary>
        /// <param name="value">Any player ID, None included.</param>
        public void WritePlayerId(PlayerId value)
        {
            _bytes.Add(value.Value);
        }

        /// <summary>
        /// Writes a string as a uint16 byte length followed by its strict UTF-8 bytes. Throws <see cref="CommandCodecException"/>
        /// for null, for a lone surrogate (not valid Unicode text) and for more than <see cref="MaxStringBytes"/> UTF-8 bytes;
        /// nothing is written in those cases.
        /// </summary>
        /// <param name="value">The text. Range: not null, valid Unicode, at most 65535 UTF-8 bytes.</param>
        public void WriteString(string value)
        {
            // The UTF-8 bytes of the text. Unit: bytes. Range: 0 to MaxStringBytes.
            byte[] encoded = EncodeUtf8(value);
            WriteUInt16((ushort)encoded.Length);
            _bytes.AddRange(encoded);
        }

        /// <summary>A copy of everything written so far.</summary>
        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }

        /// <summary>Encodes text to strict UTF-8, enforcing the rules in <see cref="WriteString"/>.</summary>
        /// <param name="value">The text.</param>
        private static byte[] EncodeUtf8(string value)
        {
            if (value == null)
            {
                throw new CommandCodecException("A command string cannot be null.");
            }
            // The UTF-8 bytes of the text. Unit: bytes. Range: 0 to MaxStringBytes.
            byte[] encoded;
            try
            {
                encoded = StrictUtf8.GetBytes(value);
            }
            catch (EncoderFallbackException)
            {
                throw new CommandCodecException("A command string must be valid Unicode text (it holds an unpaired surrogate).");
            }
            if (encoded.Length > MaxStringBytes)
            {
                throw new CommandCodecException("A command string is limited to " + MaxStringBytes + " UTF-8 bytes; this one needs " + encoded.Length + ".");
            }
            return encoded;
        }
    }
}

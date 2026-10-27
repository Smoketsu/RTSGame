// rts/src/Net/NetWriter.cs
// Explicit little-endian binary writer for protocol messages. Part of Rts.Net (ADR-008). No BinaryFormatter, no
// reflection, no System.Text.Encoding: every field is written by a named call in a documented order, so the wire
// format is the code. Strings are written as their UTF-16 code units (length then two bytes per unit), the same
// representation StateHasher uses, so no encoder policy can differ between machines. NetReader is the mirror.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Net
{
    /// <summary>Appends values to a growing byte buffer. Create one per message; read the bytes with <see cref="ToArray"/>.</summary>
    public sealed class NetWriter
    {
        /// <summary>Longest string the protocol writes or reads. Unit: UTF-16 code units. Range: fixed protocol limit; longer strings throw.</summary>
        public const int MaxStringLength = 1 << 16;

        /// <summary>The bytes written so far, in order.</summary>
        private readonly List<byte> _bytes;

        /// <summary>Number of bytes written so far. Range: 0 upward.</summary>
        public int Length => _bytes.Count;

        /// <summary>Creates an empty writer.</summary>
        /// <param name="capacity">Initial capacity hint. Unit: bytes. Range: 0 upward.</param>
        public NetWriter(int capacity = 64)
        {
            _bytes = new List<byte>(capacity);
        }

        /// <summary>Writes one byte.</summary>
        /// <param name="value">Any byte.</param>
        public void WriteByte(byte value)
        {
            _bytes.Add(value);
        }

        /// <summary>Writes a bool as one byte: 1 for true, 0 for false.</summary>
        /// <param name="value">The flag.</param>
        public void WriteBool(bool value)
        {
            _bytes.Add(value ? (byte)1 : (byte)0);
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

        /// <summary>Writes a 64-bit signed value as its two's-complement bytes.</summary>
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
        /// <param name="value">Any vector.</param>
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

        /// <summary>Writes a peer ID as one byte.</summary>
        /// <param name="value">Any peer ID, None included.</param>
        public void WritePeerId(PeerId value)
        {
            _bytes.Add(value.Value);
        }

        /// <summary>
        /// Writes a string as its length (int32) followed by each UTF-16 code unit as two bytes, low byte first. Null is
        /// written as length -1 so it differs from the empty string. Throws when the string exceeds <see cref="MaxStringLength"/>.
        /// </summary>
        /// <param name="value">Any string or null.</param>
        public void WriteString(string? value)
        {
            if (value == null)
            {
                WriteInt32(-1);
                return;
            }
            if (value.Length > MaxStringLength)
            {
                throw new ArgumentException("String longer than the protocol limit of " + MaxStringLength + " code units.", nameof(value));
            }
            WriteInt32(value.Length);
            // Index into the string's UTF-16 code units. Unit: characters. Range: 0 to Length - 1.
            for (int i = 0; i < value.Length; i++)
            {
                // Each char as its 16-bit code unit, low byte first.
                ushort unit = value[i];
                _bytes.Add((byte)unit);
                _bytes.Add((byte)(unit >> 8));
            }
        }

        /// <summary>Writes a byte block as its length (int32) then the bytes.</summary>
        /// <param name="value">The bytes; not null.</param>
        public void WriteBytes(byte[] value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            WriteInt32(value.Length);
            _bytes.AddRange(value);
        }

        /// <summary>A copy of everything written so far.</summary>
        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }
    }
}

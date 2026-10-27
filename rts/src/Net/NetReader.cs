// rts/src/Net/NetReader.cs
// Explicit little-endian binary reader for protocol messages, the mirror of NetWriter. Part of Rts.Net (ADR-008).
// Every read checks that enough bytes remain and throws NetProtocolException otherwise, so a truncated or hostile
// message can never index past the buffer or allocate a giant string: lengths are bounded by NetWriter.MaxStringLength
// and by the bytes that actually remain.
using System;
using Rts.Sim.Core;

namespace Rts.Net
{
    /// <summary>Reads values from a byte array in the order <see cref="NetWriter"/> wrote them.</summary>
    public sealed class NetReader
    {
        /// <summary>The message bytes being read.</summary>
        private readonly byte[] _bytes;

        /// <summary>Index of the next byte to read. Unit: bytes. Range: 0 to the buffer length.</summary>
        public int Position { get; private set; }

        /// <summary>Bytes not yet read. Unit: bytes. Range: 0 to the buffer length.</summary>
        public int Remaining => _bytes.Length - Position;

        /// <summary>Wraps a buffer for reading from its start.</summary>
        /// <param name="bytes">The message bytes; not null, not copied.</param>
        public NetReader(byte[] bytes)
        {
            _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            Position = 0;
        }

        /// <summary>Throws unless at least <paramref name="count"/> bytes remain.</summary>
        /// <param name="count">Bytes about to be read. Unit: bytes. Range: 0 upward.</param>
        private void Require(int count)
        {
            if (count < 0 || Remaining < count)
            {
                throw new NetProtocolException("Message truncated: needed " + count + " bytes at offset " + Position + " of " + _bytes.Length + ".");
            }
        }

        /// <summary>Reads one byte.</summary>
        public byte ReadByte()
        {
            Require(1);
            return _bytes[Position++];
        }

        /// <summary>Reads a bool written by <see cref="NetWriter.WriteBool"/>; any value other than 0 or 1 is a protocol error.</summary>
        public bool ReadBool()
        {
            // The flag byte. Range: 0 or 1.
            byte value = ReadByte();
            if (value > 1)
            {
                throw new NetProtocolException("Bad bool byte " + value + " at offset " + (Position - 1) + ".");
            }
            return value == 1;
        }

        /// <summary>Reads a 16-bit unsigned value, least significant byte first.</summary>
        public ushort ReadUInt16()
        {
            Require(2);
            // The value assembled from two bytes.
            ushort value = (ushort)(_bytes[Position] | (_bytes[Position + 1] << 8));
            Position += 2;
            return value;
        }

        /// <summary>Reads a 32-bit unsigned value, least significant byte first.</summary>
        public uint ReadUInt32()
        {
            Require(4);
            // The value assembled from four bytes.
            uint value = _bytes[Position] | ((uint)_bytes[Position + 1] << 8) | ((uint)_bytes[Position + 2] << 16) | ((uint)_bytes[Position + 3] << 24);
            Position += 4;
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
            // X then Y, in wire order.
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

        /// <summary>Reads a peer ID.</summary>
        public PeerId ReadPeerId()
        {
            return new PeerId(ReadByte());
        }

        /// <summary>Reads a string written by <see cref="NetWriter.WriteString"/>; -1 gives null. A length above the protocol limit or beyond the remaining bytes is a protocol error.</summary>
        public string? ReadString()
        {
            // Length in code units, or -1 for null. Range: -1 to MaxStringLength.
            int length = ReadInt32();
            if (length == -1)
            {
                return null;
            }
            if (length < 0 || length > NetWriter.MaxStringLength)
            {
                throw new NetProtocolException("Bad string length " + length + " at offset " + (Position - 4) + ".");
            }
            Require(length * 2);
            // The code units, two bytes each.
            char[] units = new char[length];
            // Index into the units. Range: 0 to length - 1.
            for (int i = 0; i < length; i++)
            {
                units[i] = (char)(_bytes[Position] | (_bytes[Position + 1] << 8));
                Position += 2;
            }
            return new string(units);
        }

        /// <summary>Reads a string that must not be null.</summary>
        public string ReadRequiredString()
        {
            return ReadString() ?? throw new NetProtocolException("Null string where a value was required at offset " + (Position - 4) + ".");
        }

        /// <summary>Reads a byte block written by <see cref="NetWriter.WriteBytes"/>.</summary>
        public byte[] ReadBytes()
        {
            // Block length. Unit: bytes. Range: 0 to the remaining bytes.
            int length = ReadInt32();
            if (length < 0)
            {
                throw new NetProtocolException("Bad block length " + length + " at offset " + (Position - 4) + ".");
            }
            Require(length);
            // The block copied out of the buffer.
            byte[] block = new byte[length];
            Array.Copy(_bytes, Position, block, 0, length);
            Position += length;
            return block;
        }

        /// <summary>Throws unless every byte has been read, so a message with trailing bytes is rejected rather than silently accepted.</summary>
        public void RequireEnd()
        {
            if (Remaining != 0)
            {
                throw new NetProtocolException("Message has " + Remaining + " unexpected trailing bytes.");
            }
        }
    }
}

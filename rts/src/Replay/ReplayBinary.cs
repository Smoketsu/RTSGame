// rts/src/Replay/ReplayBinary.cs
// The little-endian byte writer and reader used by the replay file framing (header, players, checkpoints, command records). Part of Rts.Replay. Command parameters are NOT encoded here: they use Rts.Sim.Commands.Codec, shared with the lockstep protocol.
// Every multi-byte value is written least significant byte first, explicitly, so the layout does not depend on the machine
// (ADR-009). Strings are a 32-bit byte count followed by UTF-8 bytes. The reader never trusts a length: every read checks the
// bytes are really there and throws ReplayFormatException otherwise. In memory only; hosts and tools do the file IO.
using System;
using System.Collections.Generic;
using System.Text;
using Rts.Sim.Core;

namespace Rts.Replay
{
    /// <summary>Appends primitive values to a growing byte buffer in the replay layout (little-endian, length-prefixed strings).</summary>
    public sealed class ReplayBinaryWriter
    {
        /// <summary>Strict UTF-8 encoder (no byte order mark, throws on unpaired surrogates). Range: a single shared instance.</summary>
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>Bytes written so far, in order.</summary>
        private readonly List<byte> _bytes = new List<byte>();

        /// <summary>Number of bytes written so far. Unit: bytes. Range: 0 upward.</summary>
        public int Length => _bytes.Count;

        /// <summary>Appends one byte.</summary>
        /// <param name="value">The byte. Range: 0 to 255.</param>
        public void WriteU8(byte value)
        {
            _bytes.Add(value);
        }

        /// <summary>Appends a 32-bit unsigned integer, least significant byte first.</summary>
        /// <param name="value">The value. Range: 0 to uint.MaxValue.</param>
        public void WriteU32(uint value)
        {
            // Byte index within the value. Unit: bytes. Range: 0 (least significant) to 3.
            for (int i = 0; i < 4; i++)
            {
                _bytes.Add((byte)(value >> (8 * i)));
            }
        }

        /// <summary>Appends a 32-bit signed integer (two's complement), least significant byte first.</summary>
        /// <param name="value">The value. Range: any int.</param>
        public void WriteI32(int value)
        {
            WriteU32(unchecked((uint)value));
        }

        /// <summary>Appends a 64-bit unsigned integer, least significant byte first.</summary>
        /// <param name="value">The value. Range: 0 to ulong.MaxValue.</param>
        public void WriteU64(ulong value)
        {
            // Byte index within the value. Unit: bytes. Range: 0 (least significant) to 7.
            for (int i = 0; i < 8; i++)
            {
                _bytes.Add((byte)(value >> (8 * i)));
            }
        }

        /// <summary>Appends a 64-bit signed integer (two's complement), least significant byte first.</summary>
        /// <param name="value">The value. Range: any long.</param>
        public void WriteI64(long value)
        {
            WriteU64(unchecked((ulong)value));
        }

        /// <summary>Appends a string as a 32-bit byte count and its UTF-8 bytes.</summary>
        /// <param name="value">The text. Range: not null, at most <see cref="ReplayFormat.MaxStringBytes"/> UTF-8 bytes, no unpaired surrogates.</param>
        public void WriteString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            // UTF-8 bytes of the text. Unit: bytes. Range: 0 to MaxStringBytes.
            byte[] encoded;
            try
            {
                encoded = Utf8.GetBytes(value);
            }
            catch (EncoderFallbackException)
            {
                throw new ArgumentException("A replay string must be valid Unicode text.", nameof(value));
            }
            if (encoded.Length > ReplayFormat.MaxStringBytes)
            {
                throw new ArgumentException("A replay string is limited to " + ReplayFormat.MaxStringBytes + " UTF-8 bytes.", nameof(value));
            }
            WriteU32((uint)encoded.Length);
            _bytes.AddRange(encoded);
        }

        /// <summary>Appends a fixed-point number as its raw 64-bit value.</summary>
        /// <param name="value">The number. Range: any Fix64.</param>
        public void WriteFix(Fix64 value)
        {
            WriteI64(value.Raw);
        }

        /// <summary>Appends a fixed-point vector: X then Y, each as its raw 64-bit value.</summary>
        /// <param name="value">The vector. Unit: tiles. Range: any FixVec2.</param>
        public void WriteVec(FixVec2 value)
        {
            WriteFix(value.X);
            WriteFix(value.Y);
        }

        /// <summary>Appends an entity ID as 32 bits.</summary>
        /// <param name="value">The ID. Range: any EntityId.</param>
        public void WriteEntity(EntityId value)
        {
            WriteU32(value.Value);
        }

        /// <summary>Appends a player ID as one byte.</summary>
        /// <param name="value">The ID. Range: 0 to 254, or 255 for none.</param>
        public void WritePlayer(PlayerId value)
        {
            WriteU8(value.Value);
        }

        /// <summary>Appends a block of raw bytes without a length.</summary>
        /// <param name="bytes">The bytes. Range: not null.</param>
        public void WriteRaw(byte[] bytes)
        {
            _bytes.AddRange(bytes ?? throw new ArgumentNullException(nameof(bytes)));
        }

        /// <summary>Copies the bytes written so far.</summary>
        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }
    }

    /// <summary>Reads primitive values from a byte array in the replay layout, checking every length and throwing <see cref="ReplayFormatException"/> on bad data.</summary>
    public sealed class ReplayBinaryReader
    {
        /// <summary>Strict UTF-8 decoder (no byte order mark, throws on invalid bytes). Range: a single shared instance.</summary>
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>The buffer being read.</summary>
        private readonly byte[] _buffer;

        /// <summary>One past the last readable index. Unit: bytes. Range: 0 to the buffer length.</summary>
        private readonly int _end;

        /// <summary>Index of the next byte to read. Unit: bytes. Range: 0 to <see cref="_end"/>.</summary>
        private int _position;

        /// <summary>Reads all of <paramref name="buffer"/>.</summary>
        /// <param name="buffer">The bytes. Range: not null.</param>
        public ReplayBinaryReader(byte[] buffer) : this(buffer, 0, buffer == null ? 0 : buffer.Length)
        {
        }

        /// <summary>Reads a slice of <paramref name="buffer"/>.</summary>
        /// <param name="buffer">The bytes. Range: not null.</param>
        /// <param name="start">Index of the first byte to read. Unit: bytes. Range: 0 to the buffer length.</param>
        /// <param name="end">One past the last byte to read. Unit: bytes. Range: start to the buffer length.</param>
        public ReplayBinaryReader(byte[] buffer, int start, int end)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            if (start < 0 || end < start || end > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(end));
            }
            _position = start;
            _end = end;
        }

        /// <summary>Bytes still unread. Unit: bytes. Range: 0 upward.</summary>
        public int Remaining => _end - _position;

        /// <summary>Index of the next byte to read, from the start of the buffer. Unit: bytes. Range: 0 upward.</summary>
        public int Position => _position;

        /// <summary>Throws unless at least <paramref name="count"/> bytes remain.</summary>
        /// <param name="count">Bytes about to be read. Unit: bytes. Range: 0 upward.</param>
        private void Need(long count)
        {
            if (count > Remaining)
            {
                throw new ReplayFormatException("the replay is truncated: " + count + " more bytes were expected at offset " + _position + " but only " + Remaining + " remain");
            }
        }

        /// <summary>Reads one byte.</summary>
        public byte ReadU8()
        {
            Need(1);
            return _buffer[_position++];
        }

        /// <summary>Reads a 32-bit unsigned integer, least significant byte first.</summary>
        public uint ReadU32()
        {
            Need(4);
            // Accumulated value. Range: 0 to uint.MaxValue.
            uint value = 0;
            // Byte index within the value. Unit: bytes. Range: 0 (least significant) to 3.
            for (int i = 0; i < 4; i++)
            {
                value |= (uint)_buffer[_position++] << (8 * i);
            }
            return value;
        }

        /// <summary>Reads a 32-bit signed integer (two's complement).</summary>
        public int ReadI32()
        {
            return unchecked((int)ReadU32());
        }

        /// <summary>Reads a 64-bit unsigned integer, least significant byte first.</summary>
        public ulong ReadU64()
        {
            Need(8);
            // Accumulated value. Range: 0 to ulong.MaxValue.
            ulong value = 0;
            // Byte index within the value. Unit: bytes. Range: 0 (least significant) to 7.
            for (int i = 0; i < 8; i++)
            {
                value |= (ulong)_buffer[_position++] << (8 * i);
            }
            return value;
        }

        /// <summary>Reads a 64-bit signed integer (two's complement).</summary>
        public long ReadI64()
        {
            return unchecked((long)ReadU64());
        }

        /// <summary>Reads a string written by <see cref="ReplayBinaryWriter.WriteString"/>. Throws on a length above <see cref="ReplayFormat.MaxStringBytes"/> or invalid UTF-8.</summary>
        public string ReadString()
        {
            // Byte length of the text. Unit: bytes. Range: 0 to MaxStringBytes.
            uint length = ReadU32();
            if (length > ReplayFormat.MaxStringBytes)
            {
                throw new ReplayFormatException("a string of " + length + " bytes at offset " + (_position - 4) + " is longer than the " + ReplayFormat.MaxStringBytes + " byte limit");
            }
            Need(length);
            try
            {
                // Decoded text. Range: any valid Unicode text.
                string text = Utf8.GetString(_buffer, _position, (int)length);
                _position += (int)length;
                return text;
            }
            catch (DecoderFallbackException)
            {
                throw new ReplayFormatException("a string at offset " + _position + " is not valid UTF-8");
            }
        }

        /// <summary>Reads a fixed-point number from its raw 64-bit value.</summary>
        public Fix64 ReadFix()
        {
            return Fix64.FromRaw(ReadI64());
        }

        /// <summary>Reads a fixed-point vector: X then Y.</summary>
        public FixVec2 ReadVec()
        {
            // X component. Unit: tiles.
            Fix64 x = ReadFix();
            // Y component. Unit: tiles.
            Fix64 y = ReadFix();
            return new FixVec2(x, y);
        }

        /// <summary>Reads an entity ID.</summary>
        public EntityId ReadEntity()
        {
            return new EntityId(ReadU32());
        }

        /// <summary>Reads a player ID.</summary>
        public PlayerId ReadPlayer()
        {
            return new PlayerId(ReadU8());
        }

        /// <summary>Copies the next <paramref name="length"/> bytes out and moves this reader past them.</summary>
        /// <param name="length">Block size. Unit: bytes. Range: 0 up to <see cref="Remaining"/>, else ReplayFormatException.</param>
        public byte[] ReadBlock(uint length)
        {
            Need(length);
            // The copied bytes. Unit: bytes.
            byte[] block = new byte[length];
            Array.Copy(_buffer, _position, block, 0, (int)length);
            _position += (int)length;
            return block;
        }

        /// <summary>Returns a reader over the next <paramref name="length"/> bytes and moves this reader past them.</summary>
        /// <param name="length">Slice size. Unit: bytes. Range: 0 up to <see cref="Remaining"/>, else ReplayFormatException.</param>
        public ReplayBinaryReader ReadSlice(uint length)
        {
            Need(length);
            // Reader over the slice, sharing the buffer.
            ReplayBinaryReader slice = new ReplayBinaryReader(_buffer, _position, _position + (int)length);
            _position += (int)length;
            return slice;
        }

        /// <summary>Throws unless <paramref name="count"/> items of at least <paramref name="minBytesEach"/> bytes can still be present, so a corrupt count cannot trigger a huge allocation.</summary>
        /// <param name="count">Declared item count. Unit: items. Range: 0 to uint.MaxValue.</param>
        /// <param name="minBytesEach">Smallest encoded size of one item. Unit: bytes. Range: 1 upward.</param>
        /// <param name="what">Plural name of the items for the message.</param>
        public void RequireCount(uint count, int minBytesEach, string what)
        {
            if ((long)count * minBytesEach > Remaining)
            {
                throw new ReplayFormatException("the replay declares " + count + " " + what + " but is too short to hold them");
            }
        }
    }
}

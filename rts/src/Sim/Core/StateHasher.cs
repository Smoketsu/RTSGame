// rts/src/Sim/Core/StateHasher.cs
// Builds the deterministic state hash: 64-bit FNV-1a over a canonical little-endian byte stream.
// Part of Rts.Sim.Core. Chosen in docs/decisions/ADR-005-state-hash.md. The simulation decides what
// is written and in which order; this class only turns values into bytes and mixes them.
using System;

namespace Rts.Sim.Core
{
    /// <summary>
    /// Incremental 64-bit FNV-1a hasher. Create one, call the Write methods in a fixed order, read <see cref="Hash"/>.
    /// Never depends on object.GetHashCode, which is randomised per process on modern .NET.
    /// </summary>
    public sealed class StateHasher
    {
        /// <summary>FNV-1a 64-bit offset basis; the hash of an empty stream. Range: fixed constant.</summary>
        public const ulong OffsetBasis = 0xcbf29ce484222325UL;

        /// <summary>FNV-1a 64-bit prime. Range: fixed constant.</summary>
        private const ulong Prime = 0x100000001b3UL;

        /// <summary>Running hash value. Equals <see cref="OffsetBasis"/> before any write. Range: any ulong.</summary>
        public ulong Hash { get; private set; } = OffsetBasis;

        /// <summary>Number of bytes mixed so far. Range: 0 upward. Lets tests confirm two writers fed the same amount.</summary>
        public long BytesWritten { get; private set; }

        /// <summary>Resets to the empty-stream state so one instance can be reused each hash interval.</summary>
        public void Reset()
        {
            Hash = OffsetBasis;
            BytesWritten = 0;
        }

        /// <summary>Mixes one byte.</summary>
        public void Write(byte value)
        {
            Hash = unchecked((Hash ^ value) * Prime);
            BytesWritten++;
        }

        /// <summary>Mixes a bool as one byte: 1 for true, 0 for false.</summary>
        public void Write(bool value)
        {
            Write(value ? (byte)1 : (byte)0);
        }

        /// <summary>Mixes a 32-bit unsigned value, least significant byte first.</summary>
        public void Write(uint value)
        {
            Write((byte)value);
            Write((byte)(value >> 8));
            Write((byte)(value >> 16));
            Write((byte)(value >> 24));
        }

        /// <summary>Mixes a 32-bit signed value as its two's-complement bytes, least significant first.</summary>
        public void Write(int value)
        {
            Write(unchecked((uint)value));
        }

        /// <summary>Mixes a 64-bit unsigned value, least significant byte first.</summary>
        public void Write(ulong value)
        {
            Write((uint)value);
            Write((uint)(value >> 32));
        }

        /// <summary>Mixes a 64-bit signed value as its two's-complement bytes.</summary>
        public void Write(long value)
        {
            Write(unchecked((ulong)value));
        }

        /// <summary>Mixes a fixed-point value through its raw bits.</summary>
        public void Write(Fix64 value)
        {
            Write(value.Raw);
        }

        /// <summary>Mixes a vector as X then Y.</summary>
        public void Write(FixVec2 value)
        {
            Write(value.X);
            Write(value.Y);
        }

        /// <summary>Mixes an entity ID.</summary>
        public void Write(EntityId value)
        {
            Write(value.Value);
        }

        /// <summary>Mixes a player ID as one byte.</summary>
        public void Write(PlayerId value)
        {
            Write(value.Value);
        }

        /// <summary>Mixes a team ID as one byte.</summary>
        public void Write(TeamId value)
        {
            Write(value.Value);
        }

        /// <summary>
        /// Mixes a string as its length followed by each UTF-16 code unit as two bytes. Culture-independent.
        /// Null is written as length -1 so it differs from the empty string.
        /// </summary>
        public void Write(string? value)
        {
            if (value == null)
            {
                Write(-1);
                return;
            }
            Write(value.Length);
            // Index into the string's UTF-16 code units. Unit: characters. Range: 0 to Length - 1.
            for (int i = 0; i < value.Length; i++)
            {
                // Each char as its 16-bit code unit, low byte first.
                ushort unit = value[i];
                Write((byte)unit);
                Write((byte)(unit >> 8));
            }
        }
    }
}

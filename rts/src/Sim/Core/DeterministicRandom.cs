// rts/src/Sim/Core/DeterministicRandom.cs
// The one random number generator the simulation owns. Part of Rts.Sim.Core.
// PCG32 (O'Neill, 2014): 64-bit state, 32-bit output, pure integer arithmetic, so it gives the same
// sequence on every machine. The match seed comes from the match settings; nothing else may seed it,
// and nothing in the simulation may use System.Random or the system clock.
using System;

namespace Rts.Sim.Core
{
    /// <summary>Seeded PCG32 generator. One instance per match, owned by <see cref="Simulation"/>; its state is part of the state hash.</summary>
    public sealed class DeterministicRandom
    {
        /// <summary>PCG32 multiplier constant (from the reference implementation). Range: fixed constant.</summary>
        private const ulong Multiplier = 6364136223846793005UL;

        /// <summary>PCG32 increment constant. Must be odd; the reference default stream. Range: odd values only; fixed constant.</summary>
        private const ulong Increment = 1442695040888963407UL;

        /// <summary>Internal generator state. Any ulong; advances every call. Exposed read-only for hashing and tests.</summary>
        public ulong State { get; private set; }

        /// <summary>Number of values drawn since seeding. Range: 0 upward; part of the state hash so a missed draw is caught.</summary>
        public ulong DrawCount { get; private set; }

        /// <summary>
        /// True while draws are forbidden. The owning <see cref="Simulation"/> locks the generator between steps so a
        /// listener (renderer, audio, UI) can never consume a value and desync the match. A standalone generator
        /// (tests, tools) starts unlocked.
        /// </summary>
        public bool IsLocked { get; private set; }

        /// <summary>Seeds the generator. The same seed always gives the same sequence.</summary>
        /// <param name="seed">Match seed, any ulong.</param>
        public DeterministicRandom(ulong seed)
        {
            Reseed(seed);
        }

        /// <summary>Resets to the sequence for <paramref name="seed"/>, following the PCG32 seeding recipe. Internal: only the simulation and tests reseed.</summary>
        /// <param name="seed">Any ulong.</param>
        internal void Reseed(ulong seed)
        {
            // Seeding must draw even while locked, so remember and restore the lock around the two priming draws.
            bool wasLocked = IsLocked;
            IsLocked = false;
            State = 0;
            DrawCount = 0;
            NextUInt();
            State = unchecked(State + seed);
            NextUInt();
            DrawCount = 0;
            IsLocked = wasLocked;
        }

        /// <summary>Restores an exact state, for replays and tests that resume mid-sequence. Internal: only the simulation and tests restore.</summary>
        /// <param name="state">A state previously read from <see cref="State"/>.</param>
        /// <param name="drawCount">The matching <see cref="DrawCount"/>.</param>
        internal void Restore(ulong state, ulong drawCount)
        {
            State = state;
            DrawCount = drawCount;
        }

        /// <summary>Locks or unlocks draws; see <see cref="IsLocked"/>. Internal: only the owning simulation toggles it.</summary>
        /// <param name="locked">True to forbid draws.</param>
        internal void SetLocked(bool locked)
        {
            IsLocked = locked;
        }

        /// <summary>Next value uniformly distributed over all 32 bits. Throws while <see cref="IsLocked"/>.</summary>
        public uint NextUInt()
        {
            if (IsLocked)
            {
                throw new InvalidOperationException("The simulation RNG can only be drawn inside Simulation.Step (from a command, system or mechanic).");
            }
            // The state before this step is what the output function mixes.
            ulong old = State;
            State = unchecked(old * Multiplier + Increment);
            DrawCount++;
            // XSH-RR output: xorshift high bits, then rotate right by the top 5 bits.
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            // Right-rotation amount from the top 5 state bits. Unit: bit positions. Range: 0 to 31.
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        /// <summary>
        /// Uniform integer in [0, <paramref name="maxExclusive"/>). Uses rejection sampling so every value is equally likely.
        /// </summary>
        /// <param name="maxExclusive">Upper bound, exclusive. Must be at least 1.</param>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "NextInt: maxExclusive must be positive.");
            }
            return (int)NextBelow((uint)maxExclusive);
        }

        /// <summary>Uniform value in [0, <paramref name="bound"/>) by rejection sampling, so no value is favoured. Bound must be at least 1.</summary>
        /// <param name="bound">Exclusive upper bound, 1 to uint.MaxValue.</param>
        private uint NextBelow(uint bound)
        {
            // Smallest draw that would bias the modulo; draws at or above it are kept. Computed as 2^32 mod bound.
            uint threshold = unchecked((uint)(-bound)) % bound;
            while (true)
            {
                // Raw 32-bit draw from the generator. Unit: bits. Range: 0 to 2^32 - 1.
                uint draw = NextUInt();
                if (draw >= threshold)
                {
                    return draw % bound;
                }
            }
        }

        /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxInclusive"/>].</summary>
        public int NextIntInclusive(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive), "NextIntInclusive: max below min.");
            }
            // Span of the range as a long so int.MaxValue - int.MinValue does not overflow. Range: 1 to 2^32.
            long span = (long)maxInclusive - minInclusive + 1;
            // Offset into the range, 0 to span - 1, unbiased in every case: a full 32-bit span uses the raw draw.
            uint offset = span == (1L << 32) ? NextUInt() : NextBelow((uint)span);
            return unchecked((int)(minInclusive + offset));
        }

        /// <summary>Uniform fixed-point value in [0, 1): the 32 output bits become the fraction bits.</summary>
        public Fix64 NextFix64Unit()
        {
            return Fix64.FromRaw((long)NextUInt());
        }

        /// <summary>True with probability <paramref name="chance"/>: always false at or below 0, always true at or above 1.</summary>
        /// <param name="chance">Probability as a fraction; values outside 0 to 1 behave as those limits.</param>
        public bool NextChance(Fix64 chance)
        {
            return NextFix64Unit() < chance;
        }

        /// <summary>Feeds the generator state into a state hash.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(State);
            hasher.Write(DrawCount);
        }
    }
}

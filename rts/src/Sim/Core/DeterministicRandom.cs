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
        /// <summary>PCG32 multiplier constant (from the reference implementation).</summary>
        private const ulong Multiplier = 6364136223846793005UL;

        /// <summary>PCG32 increment constant. Must be odd; the reference default stream.</summary>
        private const ulong Increment = 1442695040888963407UL;

        /// <summary>Internal generator state. Any ulong; advances every call. Exposed read-only for hashing and tests.</summary>
        public ulong State { get; private set; }

        /// <summary>Number of values drawn since seeding. Range: 0 upward; part of the state hash so a missed draw is caught.</summary>
        public ulong DrawCount { get; private set; }

        /// <summary>Seeds the generator. The same seed always gives the same sequence.</summary>
        /// <param name="seed">Match seed, any ulong.</param>
        public DeterministicRandom(ulong seed)
        {
            Reseed(seed);
        }

        /// <summary>Resets to the sequence for <paramref name="seed"/>, following the PCG32 seeding recipe.</summary>
        /// <param name="seed">Any ulong.</param>
        public void Reseed(ulong seed)
        {
            State = 0;
            DrawCount = 0;
            NextUInt();
            State = unchecked(State + seed);
            NextUInt();
            DrawCount = 0;
        }

        /// <summary>Restores an exact state, for replays and tests that resume mid-sequence.</summary>
        /// <param name="state">A state previously read from <see cref="State"/>.</param>
        /// <param name="drawCount">The matching <see cref="DrawCount"/>.</param>
        public void Restore(ulong state, ulong drawCount)
        {
            State = state;
            DrawCount = drawCount;
        }

        /// <summary>Next value uniformly distributed over all 32 bits.</summary>
        public uint NextUInt()
        {
            // The state before this step is what the output function mixes.
            ulong old = State;
            State = unchecked(old * Multiplier + Increment);
            DrawCount++;
            // XSH-RR output: xorshift high bits, then rotate right by the top 5 bits.
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
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
            uint bound = (uint)maxExclusive;
            // Smallest value that would bias the modulo; draws at or above it are rejected. Computed as 2^32 mod bound.
            uint threshold = unchecked((uint)(-bound)) % bound;
            while (true)
            {
                uint draw = NextUInt();
                if (draw >= threshold)
                {
                    return (int)(draw % bound);
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
            // Span of the range as a long so int.MaxValue - int.MinValue does not overflow.
            long span = (long)maxInclusive - minInclusive + 1;
            if (span > int.MaxValue)
            {
                return unchecked((int)(minInclusive + (long)NextUInt() % span));
            }
            return minInclusive + NextInt((int)span);
        }

        /// <summary>Uniform fixed-point value in [0, 1): the 32 output bits become the fraction bits.</summary>
        public Fix64 NextFix64Unit()
        {
            return Fix64.FromRaw((long)NextUInt());
        }

        /// <summary>True with probability <paramref name="chance"/>, which is clamped to [0, 1].</summary>
        /// <param name="chance">Probability as a fraction, 0 to 1.</param>
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

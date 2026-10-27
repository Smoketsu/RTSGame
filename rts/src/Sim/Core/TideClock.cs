// rts/src/Sim/Core/TideClock.cs
// The shared tide as a pure function of the match tick: a triangle wave that is low at tick 0 and every whole cycle,
// and high half a cycle later. Part of Rts.Sim.Core (rule 6: one match clock drives tides). Used by the flooding
// mechanic, the UI (tide gauge) and the AI. No state: every value is computed from the tick and two tuning values, in
// integers and Fix64 only, evaluated in the bracket order given so every machine gets the same bits.
using System;

namespace Rts.Sim.Core
{
    /// <summary>Tide level queries on the match clock.</summary>
    public static class TideClock
    {
        /// <summary>
        /// Triangle wave over one cycle: 0 at low tide, <paramref name="tideTicks"/> at high tide, in whole ticks.
        /// Computed as T - |2t - T| with t = tick modulo the cycle length.
        /// </summary>
        /// <param name="tick">Match tick. Unit: ticks. Range: 0 upward.</param>
        /// <param name="tideTicks">Length of one full cycle. Unit: ticks. Range: 2 upward.</param>
        public static int Triangle(int tick, int tideTicks)
        {
            if (tideTicks < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(tideTicks), "A tide cycle needs at least 2 ticks.");
            }
            // Position inside the current cycle. Unit: ticks. Range: 0 to tideTicks - 1.
            int t = tick % tideTicks;
            // Distance of 2t from the cycle length, always 0 or more. Unit: ticks. Range: 0 to tideTicks.
            int away = Math.Abs(2 * t - tideTicks);
            return tideTicks - away;
        }

        /// <summary>
        /// Tide level as a fraction of high-tide volume: low + ((1 - low) * tri) / T, evaluated in exactly that order.
        /// Unit: fraction (1 = high tide). Range: <paramref name="lowFraction"/> at low tide up to 1 at high tide.
        /// </summary>
        /// <param name="tick">Match tick. Unit: ticks. Range: 0 upward.</param>
        /// <param name="tideTicks">Length of one full cycle. Unit: ticks. Range: 2 upward.</param>
        /// <param name="lowFraction">Fraction of high-tide volume held at low tide. Unit: fraction. Range: 0 to 1.</param>
        public static Fix64 Level(int tick, int tideTicks, Fix64 lowFraction)
        {
            // Position on the triangle wave. Unit: ticks. Range: 0 to tideTicks.
            int tri = Triangle(tick, tideTicks);
            return lowFraction + ((Fix64.One - lowFraction) * Fix64.FromInt(tri)) / Fix64.FromInt(tideTicks);
        }

        /// <summary>True when <paramref name="tick"/> is a low-tide turning point (the start of a cycle).</summary>
        public static bool IsLowTurn(int tick, int tideTicks)
        {
            return tick % tideTicks == 0;
        }

        /// <summary>True when <paramref name="tick"/> is the high-tide turning point of a cycle (half the cycle length, integer division).</summary>
        public static bool IsHighTurn(int tick, int tideTicks)
        {
            return tick % tideTicks == tideTicks / 2;
        }
    }
}

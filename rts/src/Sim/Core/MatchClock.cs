// rts/src/Sim/Core/MatchClock.cs
// The single match clock: counts simulation ticks and converts design durations in seconds to ticks.
// Part of Rts.Sim.Core. Rule 6 of the architecture: tides, Planetary Cycles, warnings and Core capture
// all read this clock and nothing else. Tick rate is data (ADR-004), default 20 ticks per second.
using System;

namespace Rts.Sim.Core
{
    /// <summary>Tick counter plus the seconds-to-ticks conversion. Owned by <see cref="Simulation"/>; the tick is hashed.</summary>
    public sealed class MatchClock
    {
        /// <summary>Simulation ticks per real second. Range: 10 to 60; default 20 (ADR-004). Fixed for the whole match.</summary>
        public int TicksPerSecond { get; }

        /// <summary>Index of the tick currently being simulated or about to be. Starts at 0, increases by 1 per <see cref="Advance"/>. Range: 0 to int.MaxValue.</summary>
        public int Tick { get; private set; }

        /// <summary>Length of one tick in seconds as a fixed-point value (1 / TicksPerSecond), truncated. For display; conversions divide by <see cref="TicksPerSecond"/> instead to avoid compounding the truncation.</summary>
        public Fix64 SecondsPerTick { get; }

        /// <summary>Creates a clock at tick 0.</summary>
        /// <param name="ticksPerSecond">Ticks per second, 10 to 60.</param>
        public MatchClock(int ticksPerSecond)
        {
            if (ticksPerSecond < MatchSettings.MinTicksPerSecond || ticksPerSecond > MatchSettings.MaxTicksPerSecond)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "ticks_per_second must be between " + MatchSettings.MinTicksPerSecond + " and " + MatchSettings.MaxTicksPerSecond + ".");
            }
            TicksPerSecond = ticksPerSecond;
            SecondsPerTick = Fix64.One / Fix64.FromInt(ticksPerSecond);
            Tick = 0;
        }

        /// <summary>Moves to the next tick. Internal: called once per <see cref="Simulation.Step"/>, after every system has run.</summary>
        internal void Advance()
        {
            if (Tick == int.MaxValue)
            {
                throw new InvalidOperationException("MatchClock overflow.");
            }
            Tick++;
        }

        /// <summary>Restores the tick counter, for replays and tests. Internal: only the simulation and tests restore.</summary>
        /// <param name="tick">Tick index, 0 or more.</param>
        internal void Restore(int tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }
            Tick = tick;
        }

        /// <summary>
        /// Converts a duration in seconds to whole ticks, rounded to nearest (halves up). Throws if the duration is
        /// negative or too long to fit an int tick count.
        /// </summary>
        /// <param name="seconds">Duration in seconds, 0 or more.</param>
        public int SecondsToTicks(Fix64 seconds)
        {
            if (seconds < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Durations cannot be negative.");
            }
            // Product in ticks; exact because TicksPerSecond is a whole number. Fix64 saturates on overflow, which RoundToInt then reports as int.MaxValue.
            Fix64 ticks = seconds * Fix64.FromInt(TicksPerSecond);
            int rounded = ticks.RoundToInt();
            if (rounded == int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Duration too long for a tick count.");
            }
            return rounded;
        }

        /// <summary>Converts a tick count to seconds as a fixed-point value, for UI text and logs. Divides rather than multiplying by <see cref="SecondsPerTick"/> so whole seconds come out exact.</summary>
        /// <param name="ticks">Tick count, 0 or more.</param>
        public Fix64 TicksToSeconds(int ticks)
        {
            return Fix64.FromInt(ticks) / Fix64.FromInt(TicksPerSecond);
        }

        /// <summary>Converts a per-second rate (for example tiles per second) to a per-tick amount, truncated toward zero. Divides for the same reason as <see cref="TicksToSeconds"/>.</summary>
        /// <param name="perSecond">Any rate per second.</param>
        public Fix64 PerSecondToPerTick(Fix64 perSecond)
        {
            return perSecond / Fix64.FromInt(TicksPerSecond);
        }

        /// <summary>Feeds the tick and tick rate into a state hash.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Tick);
            hasher.Write(TicksPerSecond);
        }
    }
}

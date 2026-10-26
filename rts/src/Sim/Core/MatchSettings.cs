// rts/src/Sim/Core/MatchSettings.cs
// The per-match numbers the simulation core itself needs: seed, tick rate and hash interval.
// Part of Rts.Sim.Core. Tick rate and hash interval come from data/tuning/match.json; the seed comes
// from the lobby (or the harness). Everything else in tuning belongs to the systems that read it.
using System;

namespace Rts.Sim.Core
{
    /// <summary>Immutable core settings for one match.</summary>
    public sealed class MatchSettings
    {
        /// <summary>Seed for the one simulation RNG. Any ulong; the same seed and commands replay the same match.</summary>
        public ulong Seed { get; }

        /// <summary>Simulation ticks per second. Range: 10 to 60; design default 20 (ADR-004).</summary>
        public int TicksPerSecond { get; }

        /// <summary>How often the state hash is computed and reported. Unit: ticks. Range: 1 to 600; design default 20.</summary>
        public int StateHashIntervalTicks { get; }

        /// <summary>Builds settings, validating ranges.</summary>
        public MatchSettings(ulong seed, int ticksPerSecond, int stateHashIntervalTicks)
        {
            if (ticksPerSecond < 10 || ticksPerSecond > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "ticks_per_second must be 10 to 60.");
            }
            if (stateHashIntervalTicks < 1 || stateHashIntervalTicks > 600)
            {
                throw new ArgumentOutOfRangeException(nameof(stateHashIntervalTicks), "state_hash_interval_ticks must be 1 to 600.");
            }
            Seed = seed;
            TicksPerSecond = ticksPerSecond;
            StateHashIntervalTicks = stateHashIntervalTicks;
        }
    }
}

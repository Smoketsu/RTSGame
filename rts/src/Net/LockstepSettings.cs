// rts/src/Net/LockstepSettings.cs
// The numbers the lockstep protocol runs on, taken from rts/data/tuning/online.json (OnlineTuning) and converted
// once to the units the protocol uses: ticks for the input delay and catch-up cap, integer milliseconds for the
// pause limits (the host reports elapsed time in milliseconds; the protocol never reads a clock). Part of Rts.Net
// (ADR-008). Immutable; one instance shared by every peer of a session, since all peers load the same tuning.
using System;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Net
{
    /// <summary>Protocol limits for one session. Build with <see cref="FromTuning"/>.</summary>
    public sealed class LockstepSettings
    {
        /// <summary>Milliseconds per second, the only unit conversion the protocol does. Range: fixed constant.</summary>
        private const int MsPerSecond = 1000;

        /// <summary>A command issued at tick t is scheduled for tick t + this. Unit: ticks. Range: 0 to 20 (online.json input_delay_ticks).</summary>
        public int InputDelayTicks { get; }

        /// <summary>Longest a single disconnect pause may last. Unit: milliseconds. Range: 30000 to 600000 (online.json disconnect_pause_max_seconds).</summary>
        public int DisconnectPauseMaxMs { get; }

        /// <summary>Disconnect pauses each player may trigger in a match. Unit: count. Range: 0 to 5 (online.json disconnect_pauses_per_player).</summary>
        public int DisconnectPausesPerPlayer { get; }

        /// <summary>Total voluntary (and resync) pause time each player may use. Unit: milliseconds. Range: 0 to 600000 (online.json pause_budget_seconds_per_player).</summary>
        public int PauseBudgetMsPerPlayer { get; }

        /// <summary>Most ticks a peer steps in one <see cref="LockstepPeer.Update"/>. Unit: ticks per update. Range: 1 to 10000 (online.json catch_up_max_ticks_per_update).</summary>
        public int CatchUpMaxTicksPerUpdate { get; }

        /// <summary>Builds settings from already converted values. Tests use this; hosts use <see cref="FromTuning"/>.</summary>
        /// <param name="inputDelayTicks">Input delay. Unit: ticks. Range: 0 upward.</param>
        /// <param name="disconnectPauseMaxMs">Disconnect pause cap. Unit: milliseconds. Range: 1 upward.</param>
        /// <param name="disconnectPausesPerPlayer">Disconnect pauses per player. Range: 0 upward.</param>
        /// <param name="pauseBudgetMsPerPlayer">Pause budget per player. Unit: milliseconds. Range: 0 upward.</param>
        /// <param name="catchUpMaxTicksPerUpdate">Catch-up cap. Unit: ticks per update. Range: 1 upward.</param>
        public LockstepSettings(int inputDelayTicks, int disconnectPauseMaxMs, int disconnectPausesPerPlayer, int pauseBudgetMsPerPlayer, int catchUpMaxTicksPerUpdate)
        {
            if (inputDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputDelayTicks));
            }
            if (disconnectPauseMaxMs < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(disconnectPauseMaxMs));
            }
            if (disconnectPausesPerPlayer < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(disconnectPausesPerPlayer));
            }
            if (pauseBudgetMsPerPlayer < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pauseBudgetMsPerPlayer));
            }
            if (catchUpMaxTicksPerUpdate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(catchUpMaxTicksPerUpdate));
            }
            InputDelayTicks = inputDelayTicks;
            DisconnectPauseMaxMs = disconnectPauseMaxMs;
            DisconnectPausesPerPlayer = disconnectPausesPerPlayer;
            PauseBudgetMsPerPlayer = pauseBudgetMsPerPlayer;
            CatchUpMaxTicksPerUpdate = catchUpMaxTicksPerUpdate;
        }

        /// <summary>Converts the online tuning file's values: seconds become milliseconds, rounded to nearest.</summary>
        /// <param name="online">The loaded tuning/online.json.</param>
        public static LockstepSettings FromTuning(OnlineTuning online)
        {
            if (online == null)
            {
                throw new ArgumentNullException(nameof(online));
            }
            return new LockstepSettings(online.InputDelayTicks, SecondsToMs(online.DisconnectPauseMaxSeconds), online.DisconnectPausesPerPlayer,
                SecondsToMs(online.PauseBudgetSecondsPerPlayer), online.CatchUpMaxTicksPerUpdate);
        }

        /// <summary>Whole milliseconds of a duration in seconds, rounded to nearest. Unit: milliseconds. Range: 0 upward.</summary>
        /// <param name="seconds">Duration in seconds, 0 or more.</param>
        public static int SecondsToMs(Fix64 seconds)
        {
            if (seconds < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            return (seconds * Fix64.FromInt(MsPerSecond)).RoundToInt();
        }
    }
}

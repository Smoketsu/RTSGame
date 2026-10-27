// rts/src/Sim/Statuses/SlowEntry.cs
// One slow on one unit: who applied it (a source key), how strong, until when. Part of Rts.Sim.Statuses
// (spec: rts/docs/specs/statuses.md section 3). A unit holds one entry per distinct source key.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Statuses
{
    /// <summary>An immutable slow entry. A slow is active on tick k when k is below <see cref="UntilTick"/>.</summary>
    public readonly struct SlowEntry
    {
        /// <summary>Longest allowed source key. Unit: characters. Range: fixed schema constant, 64.</summary>
        public const int MaxKeyLength = 64;

        /// <summary>Id of what applied the slow, such as "drench". Unit: text id. Range: 1 to 64 characters of a-z, 0-9, underscore and dot. Entries are ordered by this key (ordinal).</summary>
        public readonly string SourceKey;

        /// <summary>Speed multiplier while active. Unit: multiplier of speed. Range: above 0 and at most 1 (the strongest slow is the smallest).</summary>
        public readonly Fix64 Multiplier;

        /// <summary>First tick on which the slow is no longer active. Unit: ticks. Range: above the tick it was applied.</summary>
        public readonly int UntilTick;

        /// <summary>Player whose unit or script applied it. Range: any player, <see cref="PlayerId.None"/> for scripts.</summary>
        public readonly PlayerId SourcePlayer;

        /// <summary>Builds an entry; the status system validates the ranges before calling.</summary>
        public SlowEntry(string sourceKey, Fix64 multiplier, int untilTick, PlayerId sourcePlayer)
        {
            SourceKey = sourceKey;
            Multiplier = multiplier;
            UntilTick = untilTick;
            SourcePlayer = sourcePlayer;
        }

        /// <summary>True when <paramref name="key"/> is a valid source key: 1 to 64 characters of a-z, 0-9, underscore and dot.</summary>
        /// <param name="key">Candidate key; null counts as invalid.</param>
        public static bool IsValidKey(string? key)
        {
            if (string.IsNullOrEmpty(key) || key!.Length > MaxKeyLength)
            {
                return false;
            }
            // Index over the characters. Unit: character position. Range: 0 to key length - 1.
            for (int i = 0; i < key.Length; i++)
            {
                // The character under test.
                char c = key[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.'))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Throws <see cref="ArgumentException"/> unless <paramref name="key"/> is valid.</summary>
        /// <param name="key">Candidate key.</param>
        /// <param name="paramName">Parameter name for the exception.</param>
        public static void RequireValidKey(string? key, string paramName)
        {
            if (!IsValidKey(key))
            {
                throw new ArgumentException("A status source key is 1 to " + MaxKeyLength + " characters of a-z, 0-9, '_' and '.'.", paramName);
            }
        }
    }
}

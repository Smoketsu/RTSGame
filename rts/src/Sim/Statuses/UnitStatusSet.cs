// rts/src/Sim/Statuses/UnitStatusSet.cs
// The status container of one unit: Grounded, the list of slows and the Spoutling drench counter. Part of
// Rts.Sim.Statuses (spec: rts/docs/specs/statuses.md section 3). It is the source of truth; StatusSystem changes it
// through the internal mutators and pushes the two derived values (layer override, status speed multiplier) into the
// Unit. Everything here is hashed, in the order of WriteHash.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Statuses
{
    /// <summary>All statuses on one unit. Created with the unit, dropped with it. Read-only to everything except <see cref="StatusSystem"/>.</summary>
    public sealed class UnitStatusSet
    {
        /// <summary>Backing list for <see cref="Slows"/>, sorted by SourceKey (ordinal).</summary>
        private readonly List<SlowEntry> _slows = new List<SlowEntry>();

        /// <summary>First tick on which Grounded is no longer active. Unit: ticks. Range: 0 = not Grounded; otherwise 1 or more.</summary>
        public int GroundedUntilTick { get; private set; }

        /// <summary>Source key of the Grounded status that is active. Unit: text id. Range: "" when not Grounded, else a valid source key.</summary>
        public string GroundedSourceKey { get; private set; } = string.Empty;

        /// <summary>Active slows sorted by SourceKey (ordinal). Range: 0 to max_slow_sources_per_unit entries.</summary>
        public IReadOnlyList<SlowEntry> Slows => _slows;

        /// <summary>Spoutling drench hits counted toward Grounded. Unit: hits. Range: 0 to spoutling_drench_hits_to_ground - 1.</summary>
        public int DrenchHits { get; private set; }

        /// <summary>Tick of the latest drench hit. Unit: ticks. Range: -1 = none, else 0 or more.</summary>
        public int LastDrenchTick { get; private set; } = -1;

        /// <summary>True while the unit is Grounded.</summary>
        public bool IsGrounded => GroundedUntilTick != 0;

        /// <summary>True while at least one slow is active.</summary>
        public bool IsSlowed => _slows.Count > 0;

        /// <summary>
        /// The strongest active slow: the smallest multiplier over the entries (slows never multiply together).
        /// Unit: multiplier of speed. Range: above 0 and at most 1; <see cref="Fix64.One"/> when no slow is active.
        /// </summary>
        public Fix64 SpeedMultiplier
        {
            get
            {
                // Smallest multiplier seen so far.
                Fix64 strongest = Fix64.One;
                foreach (SlowEntry entry in _slows)
                {
                    if (entry.Multiplier < strongest)
                    {
                        strongest = entry.Multiplier;
                    }
                }
                return strongest;
            }
        }

        /// <summary>True when any status or the drench counter holds state, so the per-tick pass can skip idle units.</summary>
        internal bool HasAnyState => GroundedUntilTick != 0 || _slows.Count > 0 || LastDrenchTick >= 0;

        /// <summary>Starts or changes Grounded.</summary>
        /// <param name="untilTick">First tick it is no longer active. Unit: ticks. Range: 1 or more.</param>
        /// <param name="sourceKey">Source key to store.</param>
        internal void SetGrounded(int untilTick, string sourceKey)
        {
            GroundedUntilTick = untilTick;
            GroundedSourceKey = sourceKey;
        }

        /// <summary>Ends Grounded and clears its key so the hash holds no stale data.</summary>
        internal void ClearGrounded()
        {
            GroundedUntilTick = 0;
            GroundedSourceKey = string.Empty;
        }

        /// <summary>Index of the entry with <paramref name="key"/>, or -1.</summary>
        /// <param name="key">Source key to find.</param>
        internal int IndexOfSlow(string key)
        {
            // Position in the sorted list. Unit: list position. Range: 0 to Count - 1.
            for (int i = 0; i < _slows.Count; i++)
            {
                if (string.Equals(_slows[i].SourceKey, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Replaces the entry at <paramref name="index"/> (same key, so the order holds).</summary>
        /// <param name="index">List position of an existing entry.</param>
        /// <param name="entry">The new entry, with the same SourceKey.</param>
        internal void ReplaceSlowAt(int index, SlowEntry entry)
        {
            _slows[index] = entry;
        }

        /// <summary>Inserts an entry whose key is not present, keeping the list sorted by key (ordinal).</summary>
        /// <param name="entry">The new entry.</param>
        internal void InsertSlow(SlowEntry entry)
        {
            // Insertion position keeping ascending key order. Unit: list position. Range: 0 to Count.
            int index = 0;
            while (index < _slows.Count && string.CompareOrdinal(_slows[index].SourceKey, entry.SourceKey) < 0)
            {
                index++;
            }
            _slows.Insert(index, entry);
        }

        /// <summary>Removes the entry at <paramref name="index"/>.</summary>
        /// <param name="index">List position of an existing entry.</param>
        internal void RemoveSlowAt(int index)
        {
            _slows.RemoveAt(index);
        }

        /// <summary>Sets the drench counter.</summary>
        /// <param name="hits">Hits counted. Unit: hits. Range: 0 or more.</param>
        /// <param name="lastTick">Tick of the latest hit, or -1 for none. Unit: ticks.</param>
        internal void SetDrench(int hits, int lastTick)
        {
            DrenchHits = hits;
            LastDrenchTick = lastTick;
        }

        /// <summary>
        /// Feeds the set into the state hash: GroundedUntilTick, GroundedSourceKey, slow count, then per entry SourceKey,
        /// Multiplier, UntilTick, SourcePlayer; then DrenchHits and LastDrenchTick.
        /// </summary>
        /// <param name="hasher">The hasher to write to.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(GroundedUntilTick);
            hasher.Write(GroundedSourceKey);
            hasher.Write(_slows.Count);
            foreach (SlowEntry entry in _slows)
            {
                hasher.Write(entry.SourceKey);
                hasher.Write(entry.Multiplier);
                hasher.Write(entry.UntilTick);
                hasher.Write(entry.SourcePlayer);
            }
            hasher.Write(DrenchHits);
            hasher.Write(LastDrenchTick);
        }
    }
}

// rts/src/Sim/Combat/PendingHitQueue.cs
// The hashed queue of ranged hits in flight, ordered by (landing tick, sequence). Part of Rts.Sim.Combat. Owned by Simulation
// (Simulation.PendingHits); only CombatSystem enqueues and takes hits. No public enumerator: hosts see only Count.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Combat
{
    /// <summary>Hits fired but not yet landed, in deterministic (LandTick, Sequence) order. Part of the state hash.</summary>
    public sealed class PendingHitQueue
    {
        /// <summary>Sort key of an entry: landing tick, then firing sequence.</summary>
        private readonly struct HitKey : IComparable<HitKey>
        {
            /// <summary>Landing tick. Unit: ticks. Range: 1 upward.</summary>
            public readonly int LandTick;

            /// <summary>Firing sequence. Unit: count. Range: 0 upward.</summary>
            public readonly long Sequence;

            /// <summary>Stores the key.</summary>
            public HitKey(int landTick, long sequence)
            {
                LandTick = landTick;
                Sequence = sequence;
            }

            /// <summary>Orders by landing tick, then sequence.</summary>
            public int CompareTo(HitKey other)
            {
                // Comparison of the landing ticks; the sequences decide when equal. Range: negative, 0 or positive.
                int byTick = LandTick.CompareTo(other.LandTick);
                return byTick != 0 ? byTick : Sequence.CompareTo(other.Sequence);
            }
        }

        /// <summary>Entries in key order.</summary>
        private readonly SortedDictionary<HitKey, PendingHit> _hits = new SortedDictionary<HitKey, PendingHit>();

        /// <summary>Sequence given to the next enqueued hit; never reused. Unit: count. Range: 0 upward. Hashed.</summary>
        internal long NextSequence { get; private set; }

        /// <summary>Number of hits waiting. Unit: hits. Range: 0 upward.</summary>
        public int Count => _hits.Count;

        /// <summary>Queues a hit with the next sequence number.</summary>
        /// <param name="landTick">Tick the hit lands. Unit: ticks. Range: above the current tick.</param>
        /// <param name="attacker">Unit that fired; it may be gone when the hit lands.</param>
        /// <param name="attackerOwner">Owner of the attacker at fire time, used for kill credit and Drench.</param>
        /// <param name="target">Unit the hit is bound to.</param>
        /// <param name="damage">Final damage. Unit: health points. Range: 0 or more.</param>
        /// <param name="appliesDrench">True when the hit applies Drench on landing.</param>
        internal void Enqueue(int landTick, EntityId attacker, PlayerId attackerOwner, EntityId target, Fix64 damage, bool appliesDrench)
        {
            // Sequence of this hit. Unit: count. Range: 0 upward.
            long sequence = NextSequence++;
            _hits.Add(new HitKey(landTick, sequence), new PendingHit(landTick, sequence, attacker, attackerOwner, target, damage, appliesDrench));
        }

        /// <summary>
        /// Removes every hit with LandTick up to <paramref name="tick"/> and appends it to <paramref name="into"/> in key order.
        /// Throws when a hit is overdue (LandTick below <paramref name="tick"/>): that would be a broken invariant.
        /// </summary>
        /// <param name="tick">The current tick. Unit: ticks.</param>
        /// <param name="into">List the due hits are appended to.</param>
        internal void TakeDue(int tick, List<PendingHit> into)
        {
            // Keys of the due hits, collected first so the dictionary is not changed while enumerated.
            List<HitKey> due = new List<HitKey>();
            foreach (KeyValuePair<HitKey, PendingHit> pair in _hits)
            {
                if (pair.Key.LandTick > tick)
                {
                    break;
                }
                if (pair.Key.LandTick < tick)
                {
                    throw new InvalidOperationException("A pending hit is overdue: it should have landed on tick " + pair.Key.LandTick + ", now " + tick + ".");
                }
                due.Add(pair.Key);
                into.Add(pair.Value);
            }
            foreach (HitKey key in due)
            {
                _hits.Remove(key);
            }
        }

        /// <summary>Test hook: replaces a queued hit's fields in place (same key unless <paramref name="landTick"/> differs). Internal.</summary>
        /// <param name="sequence">Sequence of the hit to change.</param>
        /// <param name="landTick">New landing tick. Unit: ticks.</param>
        /// <param name="target">New target.</param>
        /// <param name="damage">New damage. Unit: health points.</param>
        internal void Rewrite(long sequence, int landTick, EntityId target, Fix64 damage)
        {
            foreach (KeyValuePair<HitKey, PendingHit> pair in _hits)
            {
                if (pair.Key.Sequence == sequence)
                {
                    // The entry being replaced; its attacker, owner and drench flag are kept.
                    PendingHit old = pair.Value;
                    _hits.Remove(pair.Key);
                    _hits.Add(new HitKey(landTick, sequence), new PendingHit(landTick, sequence, old.Attacker, old.AttackerOwner, target, damage, old.AppliesDrench));
                    return;
                }
            }
            throw new InvalidOperationException("No pending hit with sequence " + sequence + ".");
        }

        /// <summary>Feeds the count, the next sequence and every entry, in key order, into the state hash.</summary>
        internal void WriteHash(StateHasher hasher)
        {
            hasher.Write(_hits.Count);
            hasher.Write(NextSequence);
            foreach (PendingHit hit in _hits.Values)
            {
                hasher.Write(hit.LandTick);
                hasher.Write(hit.Sequence);
                hasher.Write(hit.Attacker);
                hasher.Write(hit.AttackerOwner);
                hasher.Write(hit.Target);
                hasher.Write(hit.Damage);
                hasher.Write(hit.AppliesDrench);
            }
        }
    }
}

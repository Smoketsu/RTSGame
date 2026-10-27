// rts/src/Sim/Commands/CommandQueue.cs
// Holds submitted commands until their tick and hands them back in deterministic order.
// Part of Rts.Sim.Commands. Order within a tick is (issuer, sequence, kind). Arrival order never affects the
// outcome because duplicated (issuer, sequence) keys are rejected wholesale by the simulation, which is what
// lets local input, network peers and replays produce identical matches.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Commands
{
    /// <summary>Tick-keyed command store. Owned by <see cref="Simulation"/>. Pending commands are input, not state, so they are not hashed.</summary>
    public sealed class CommandQueue
    {
        /// <summary>Commands grouped by scheduled tick. SortedDictionary so ticks are visited in order.</summary>
        private readonly SortedDictionary<int, List<Command>> _byTick = new SortedDictionary<int, List<Command>>();

        /// <summary>Number of commands waiting for a future tick. Range: 0 upward.</summary>
        public int PendingCount { get; private set; }

        /// <summary>Total commands ever enqueued, for logs. Range: 0 upward.</summary>
        public long TotalEnqueued { get; private set; }

        /// <summary>Comparer that fixes the order of commands scheduled for the same tick.</summary>
        private static readonly IComparer<Command> WithinTickOrder = new CommandOrderComparer();

        /// <summary>Adds a command for its scheduled tick. Internal: hosts go through <see cref="Simulation.Submit"/>, which checks the tick is not in the past.</summary>
        /// <param name="command">Command to store.</param>
        internal void Enqueue(Command command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if (!_byTick.TryGetValue(command.Tick, out List<Command>? list))
            {
                list = new List<Command>();
                _byTick.Add(command.Tick, list);
            }
            list.Add(command);
            PendingCount++;
            TotalEnqueued++;
        }

        /// <summary>
        /// Removes and returns every command scheduled for <paramref name="tick"/>, sorted by (issuer, sequence, kind).
        /// Returns an empty list when none are scheduled. Commands sharing an (issuer, sequence) key compare equal and
        /// may come back in any order; the simulation rejects every member of such a group, so that order never matters.
        /// Internal: only the simulation's step takes commands.
        /// </summary>
        /// <param name="tick">Tick being stepped.</param>
        internal List<Command> TakeForTick(int tick)
        {
            if (!_byTick.TryGetValue(tick, out List<Command>? list))
            {
                return new List<Command>();
            }
            _byTick.Remove(tick);
            PendingCount -= list.Count;
            // Sort by the deterministic key. List.Sort is unstable, but the key is unique except for duplicated
            // (issuer, sequence) groups, every member of which the simulation rejects, so the outcome is deterministic.
            list.Sort(WithinTickOrder);
            return list;
        }

        /// <summary>Lowest tick with a pending command, or -1 when the queue is empty.</summary>
        public int EarliestPendingTick()
        {
            foreach (KeyValuePair<int, List<Command>> pair in _byTick)
            {
                return pair.Key;
            }
            return -1;
        }

        /// <summary>Orders commands on one tick by issuer slot, then issuer sequence, then kind name (ordinal).</summary>
        private sealed class CommandOrderComparer : IComparer<Command>
        {
            /// <summary>Compares two commands by issuer, sequence, then kind; nulls sort first.</summary>
            public int Compare(Command? a, Command? b)
            {
                if (ReferenceEquals(a, b))
                {
                    return 0;
                }
                if (a == null)
                {
                    return -1;
                }
                if (b == null)
                {
                    return 1;
                }
                // Sign of the issuer-slot comparison. Range: -1, 0 or 1.
                int byIssuer = a.Issuer.CompareTo(b.Issuer);
                if (byIssuer != 0)
                {
                    return byIssuer;
                }
                // Sign of the sequence comparison. Range: -1, 0 or 1.
                int bySequence = a.Sequence.CompareTo(b.Sequence);
                if (bySequence != 0)
                {
                    return bySequence;
                }
                return string.CompareOrdinal(a.Kind, b.Kind);
            }
        }
    }
}

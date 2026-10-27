// rts/src/Sim/Pathing/MinHeap.cs
// Binary min-heap of 64-bit keys used by the flow-field Dijkstra. Part of Rts.Sim.Pathing (ADR-007).
// Keys pack (integration cost << 32) | tile index, so ties on cost break by tile index and the pop order is
// identical on every machine. Reused between field builds to avoid allocation.
using System;

namespace Rts.Sim.Pathing
{
    /// <summary>Minimal binary heap over <see cref="long"/> keys with Push and PopMin.</summary>
    internal sealed class MinHeap
    {
        /// <summary>Heap storage; element 0 is the smallest key. Only the first <see cref="Count"/> entries are live.</summary>
        private long[] _items;

        /// <summary>Number of live keys. Range: 0 to capacity.</summary>
        public int Count { get; private set; }

        /// <summary>Creates a heap with room for <paramref name="capacity"/> keys; it grows if needed.</summary>
        /// <param name="capacity">Initial capacity, 1 or more.</param>
        public MinHeap(int capacity)
        {
            _items = new long[Math.Max(1, capacity)];
            Count = 0;
        }

        /// <summary>Empties the heap without releasing storage.</summary>
        public void Clear()
        {
            Count = 0;
        }

        /// <summary>Adds a key, sifting it up to keep the heap property.</summary>
        public void Push(long key)
        {
            if (Count == _items.Length)
            {
                Array.Resize(ref _items, _items.Length * 2);
            }
            // Index of the new key as it moves up the tree.
            int index = Count;
            Count++;
            while (index > 0)
            {
                // Parent slot of index.
                int parent = (index - 1) >> 1;
                if (_items[parent] <= key)
                {
                    break;
                }
                _items[index] = _items[parent];
                index = parent;
            }
            _items[index] = key;
        }

        /// <summary>Removes and returns the smallest key. Throws when empty.</summary>
        public long PopMin()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("MinHeap is empty.");
            }
            // The smallest key, returned. Unit: packed (cost << 32 | tile index).
            long min = _items[0];
            Count--;
            if (Count > 0)
            {
                // The last key, sifted down from the root.
                long moving = _items[Count];
                // Hole position the last key is sifted down from. Unit: heap position. Range: 0 to Count - 1.
                int index = 0;
                while (true)
                {
                    // Left child of index; the right child is left + 1.
                    int child = (index << 1) + 1;
                    if (child >= Count)
                    {
                        break;
                    }
                    if (child + 1 < Count && _items[child + 1] < _items[child])
                    {
                        child++;
                    }
                    if (_items[child] >= moving)
                    {
                        break;
                    }
                    _items[index] = _items[child];
                    index = child;
                }
                _items[index] = moving;
            }
            return min;
        }
    }
}

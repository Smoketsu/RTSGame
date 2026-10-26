// rts/src/Sim/Pathing/FlowFieldCache.cs
// Owns the flow fields of a match: builds them on demand, reuses them while the terrain version matches,
// and evicts the oldest when over the tuning limit. Part of Rts.Sim.Pathing (ADR-007). Owned by Simulation.
using System;
using System.Collections.Generic;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Pathing
{
    /// <summary>Keyed by (layer, goal tile). Deterministic: eviction order follows creation order, never hash order.</summary>
    public sealed class FlowFieldCache
    {
        /// <summary>Fields by packed key (layer << 32 | goal index), ordinal order.</summary>
        private readonly SortedDictionary<long, FlowField> _fields = new SortedDictionary<long, FlowField>();

        /// <summary>The grid fields are built over.</summary>
        private readonly TerrainGrid _grid;

        /// <summary>Most fields kept. Unit: fields. Range: 1 to 1024 (tuning max_cached_flow_fields).</summary>
        private readonly int _capacity;

        /// <summary>Scratch heap reused by every build.</summary>
        private readonly MinHeap _heap;

        /// <summary>Creation counter handed to new fields. Range: 0 upward.</summary>
        private long _nextSequence;

        /// <summary>Fields built since the match started, for profiling. Range: 0 upward. Not hashed.</summary>
        public long BuildCount { get; private set; }

        /// <summary>Number of fields currently held. Range: 0 to capacity.</summary>
        public int Count => _fields.Count;

        /// <summary>Creates an empty cache over <paramref name="grid"/>.</summary>
        /// <param name="grid">Terrain grid.</param>
        /// <param name="capacity">Most fields kept, 1 or more.</param>
        public FlowFieldCache(TerrainGrid grid, int capacity)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            _capacity = capacity;
            _heap = new MinHeap(Math.Max(16, grid.TileCount / 8));
        }

        /// <summary>Returns the field for a layer and goal, building or rebuilding it when missing or stale.</summary>
        /// <param name="layer">Movement layer.</param>
        /// <param name="goalIndex">Goal tile index, 0 to tile count - 1.</param>
        public FlowField Get(MovementLayer layer, int goalIndex)
        {
            // Packed cache key.
            long key = ((long)(byte)layer << 32) | (uint)goalIndex;
            if (_fields.TryGetValue(key, out FlowField? field) && field.TerrainVersion == _grid.Version)
            {
                return field;
            }
            if (field != null)
            {
                _fields.Remove(key);
            }
            while (_fields.Count >= _capacity)
            {
                EvictOldest();
            }
            field = new FlowField(_grid, layer, goalIndex, _nextSequence++, _heap);
            BuildCount++;
            _fields.Add(key, field);
            return field;
        }

        /// <summary>Removes every cached field (for tests or a wholesale terrain reset).</summary>
        public void Clear()
        {
            _fields.Clear();
        }

        /// <summary>Removes the field with the smallest creation sequence.</summary>
        private void EvictOldest()
        {
            // Key and sequence of the oldest field found so far.
            long oldestKey = 0;
            long oldestSequence = long.MaxValue;
            foreach (KeyValuePair<long, FlowField> pair in _fields)
            {
                if (pair.Value.CreationSequence < oldestSequence)
                {
                    oldestSequence = pair.Value.CreationSequence;
                    oldestKey = pair.Key;
                }
            }
            _fields.Remove(oldestKey);
        }
    }
}

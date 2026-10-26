// rts/src/Sim/Units/UnitRegistry.cs
// All live units of a match, iterated in ascending entity ID order. Part of Rts.Sim.Units.
// Determinism rule: sorted-ID iteration only, never hash order, so a SortedDictionary backs it.
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>Lookup and ordered iteration over live units. Owned by <see cref="Simulation"/>.</summary>
    public sealed class UnitRegistry
    {
        /// <summary>Units by ID in ascending order.</summary>
        private readonly SortedDictionary<EntityId, Unit> _units = new SortedDictionary<EntityId, Unit>();

        /// <summary>Number of live units. Range: 0 upward.</summary>
        public int Count => _units.Count;

        /// <summary>Live units in ascending ID order. Do not add or remove while iterating.</summary>
        public IEnumerable<Unit> All => _units.Values;

        /// <summary>Finds a live unit by ID.</summary>
        /// <param name="id">Entity ID to look up.</param>
        /// <param name="unit">The unit when found.</param>
        public bool TryGet(EntityId id, [MaybeNullWhen(false)] out Unit unit)
        {
            return _units.TryGetValue(id, out unit);
        }

        /// <summary>True when a live unit has this ID.</summary>
        public bool Contains(EntityId id)
        {
            return _units.ContainsKey(id);
        }

        /// <summary>Adds a unit. Internal: only the simulation's spawn path calls this.</summary>
        internal void Add(Unit unit)
        {
            _units.Add(unit.Id, unit);
        }

        /// <summary>Removes a unit by ID. Returns false when it was not present.</summary>
        internal bool Remove(EntityId id)
        {
            return _units.Remove(id);
        }

        /// <summary>Snapshot of the IDs in ascending order, for systems that mutate the registry while visiting units.</summary>
        internal List<EntityId> IdsSnapshot()
        {
            return new List<EntityId>(_units.Keys);
        }

        /// <summary>Feeds the count and every unit, in ID order, into the state hash.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(_units.Count);
            foreach (Unit unit in _units.Values)
            {
                unit.WriteHash(hasher);
            }
        }
    }
}

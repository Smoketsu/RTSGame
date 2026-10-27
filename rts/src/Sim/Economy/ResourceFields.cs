// rts/src/Sim/Economy/ResourceFields.cs
// All resource fields of a match as live state, in map order. Part of Rts.Sim.Economy. Built by Simulation from the map at
// construction; workers take from it through EconomySystem. Iteration is in ascending field index, the order of the map file,
// and the whole list is written into the state hash.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Sim.Economy
{
    /// <summary>The fields of the map with how much each still holds.</summary>
    public sealed class ResourceFields
    {
        /// <summary>Field states in ascending index order.</summary>
        private readonly List<ResourceFieldState> _fields = new List<ResourceFieldState>();

        /// <summary>Number of fields. Range: 0 upward (the map's field count).</summary>
        public int Count => _fields.Count;

        /// <summary>The field at <paramref name="index"/> (0 to Count - 1).</summary>
        public ResourceFieldState this[int index] => _fields[index];

        /// <summary>All fields in ascending index order.</summary>
        public IReadOnlyList<ResourceFieldState> All => _fields;

        /// <summary>Builds one live field per field of the map, at its starting amount.</summary>
        /// <param name="map">The match's map; not null.</param>
        public ResourceFields(MapDefinition map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            // Index into the map's field list. Unit: list position. Range: 0 to field count - 1.
            for (int i = 0; i < map.ResourceFields.Count; i++)
            {
                _fields.Add(new ResourceFieldState(i, map.ResourceFields[i]));
            }
        }

        /// <summary>Feeds the field count and each field's remaining amount, in index order, into the state hash. The kind and position are static map data and are not repeated.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(_fields.Count);
            foreach (ResourceFieldState field in _fields)
            {
                hasher.Write(field.Remaining);
            }
        }
    }
}

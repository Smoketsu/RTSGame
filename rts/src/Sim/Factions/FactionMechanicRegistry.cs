// rts/src/Sim/Factions/FactionMechanicRegistry.cs
// Registry of mechanic factories keyed by mechanic ID. Part of Rts.Sim.Factions.
// Hosts (Unity bootstrap, headless runner, tests) register the mechanics they ship; the simulation
// looks them up by the ID named in faction data and never references a concrete faction type.
using System;
using System.Collections.Generic;

namespace Rts.Sim.Factions
{
    /// <summary>Maps mechanic IDs to factories that build a fresh mechanic instance per match.</summary>
    public sealed class FactionMechanicRegistry
    {
        /// <summary>Factories by ID, ordinal order so iteration is deterministic.</summary>
        private readonly SortedDictionary<string, Func<IFactionMechanic>> _factories =
            new SortedDictionary<string, Func<IFactionMechanic>>(StringComparer.Ordinal);

        /// <summary>Number of registered mechanics.</summary>
        public int Count => _factories.Count;

        /// <summary>Registered IDs in ordinal order.</summary>
        public IEnumerable<string> Ids => _factories.Keys;

        /// <summary>Registers a factory. Registering the same ID twice throws so a typo cannot silently replace a mechanic.</summary>
        /// <param name="id">Mechanic ID, non-empty.</param>
        /// <param name="factory">Builds a new instance; must not share state between instances.</param>
        public void Register(string id, Func<IFactionMechanic> factory)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Mechanic id must not be empty.", nameof(id));
            }
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            if (_factories.ContainsKey(id))
            {
                throw new InvalidOperationException("Mechanic '" + id + "' is already registered.");
            }
            _factories.Add(id, factory);
        }

        /// <summary>True when a factory exists for <paramref name="id"/>.</summary>
        public bool Contains(string id)
        {
            return _factories.ContainsKey(id);
        }

        /// <summary>Builds a new mechanic instance. Throws when the ID is unknown; the simulation reports that at match start.</summary>
        public IFactionMechanic Create(string id)
        {
            if (!_factories.TryGetValue(id, out Func<IFactionMechanic>? factory))
            {
                throw new KeyNotFoundException("No faction mechanic registered with id '" + id + "'.");
            }
            IFactionMechanic mechanic = factory();
            if (!string.Equals(mechanic.Id, id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Factory for '" + id + "' produced a mechanic with id '" + mechanic.Id + "'.");
            }
            return mechanic;
        }
    }
}

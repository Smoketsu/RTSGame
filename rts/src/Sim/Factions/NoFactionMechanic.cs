// rts/src/Sim/Factions/NoFactionMechanic.cs
// The empty mechanic used by factions whose data says mechanic_id "none" (neutral, tests, greybox).
// Part of Rts.Sim.Factions. It is registered by the core registry helper so every match can resolve it.
using Rts.Sim.Core;

namespace Rts.Sim.Factions
{
    /// <summary>A mechanic that does nothing and hashes nothing but its ID.</summary>
    public sealed class NoFactionMechanic : IFactionMechanic
    {
        /// <summary>Mechanic ID as named in faction data.</summary>
        public string Id => FactionDefinition.NoMechanic;

        /// <summary>Called once before tick 0 runs.</summary>
        public void OnMatchStart(Simulation sim)
        {
        }

        /// <summary>Called once per tick after commands and movement.</summary>
        public void OnTick(Simulation sim)
        {
        }

        /// <summary>Feeds the mechanic's state into the state hash; Phase 0 has only the ID.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
        }

        /// <summary>Registers this mechanic under "none" if it is not already present.</summary>
        public static void RegisterInto(FactionMechanicRegistry registry)
        {
            if (!registry.Contains(FactionDefinition.NoMechanic))
            {
                registry.Register(FactionDefinition.NoMechanic, () => new NoFactionMechanic());
            }
        }
    }
}

// rts/src/Sim/Factions/NoFactionMechanic.cs
// The empty mechanic used by factions whose data says mechanic_id "none" (neutral, tests, greybox).
// Part of Rts.Sim.Factions. It is registered by the core registry helper so every match can resolve it.
using Rts.Sim.Core;

namespace Rts.Sim.Factions
{
    /// <summary>A mechanic that does nothing and hashes nothing but its ID.</summary>
    public sealed class NoFactionMechanic : IFactionMechanic
    {
        public string Id => FactionDefinition.NoMechanic;

        public void OnMatchStart(Simulation sim)
        {
        }

        public void OnTick(Simulation sim)
        {
        }

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

// rts/src/Sim/Factions/BuiltInMechanics.cs
// Composition root for the mechanics shipped inside the simulation assembly. Part of Rts.Sim.Factions.
// This is the only file that names concrete factions, and it sits outside Core on purpose: hosts call
// RegisterAll so the core can stay faction-agnostic. New factions add one line here.
using Rts.Sim.Factions.Skai;
using Rts.Sim.Factions.Water;

namespace Rts.Sim.Factions
{
    /// <summary>Registers every built-in mechanic plug-in into a registry.</summary>
    public static class BuiltInMechanics
    {
        /// <summary>Adds "none", "momentum" and "flooding". Safe to call once per registry.</summary>
        public static void RegisterAll(FactionMechanicRegistry registry)
        {
            NoFactionMechanic.RegisterInto(registry);
            registry.Register(MomentumMechanic.MechanicId, () => new MomentumMechanic());
            registry.Register(FloodingMechanic.MechanicId, () => new FloodingMechanic());
        }

        /// <summary>Convenience: a new registry with everything built in registered.</summary>
        public static FactionMechanicRegistry CreateRegistry()
        {
            FactionMechanicRegistry registry = new FactionMechanicRegistry();
            RegisterAll(registry);
            return registry;
        }
    }
}

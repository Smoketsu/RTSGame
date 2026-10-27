// rts/src/Sim/Factions/BuiltInMechanics.cs
// Composition root for the mechanics shipped inside the simulation assembly. Part of Rts.Sim.Factions.
// This is the only file that names concrete factions, and it sits outside Core on purpose: hosts call
// RegisterAll so the core can stay faction-agnostic. New factions add one line here.
using System.Collections.Generic;
using Rts.Sim.Data;
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

        /// <summary>
        /// Script readers for the commands that built-in mechanics add (currently the Water "cast_wave"), sorted ascending by
        /// <see cref="IScriptCommandParser.Type"/> (ordinal). Hosts pass this to <c>CommandScript.Parse</c> so scripts can use
        /// those commands; the core reader itself names no faction.
        /// </summary>
        public static IReadOnlyList<IScriptCommandParser> ScriptParsers { get; } = new IScriptCommandParser[]
        {
            new CastWaveScriptParser(),
        };

        /// <summary>Convenience: a new registry with everything built in registered.</summary>
        public static FactionMechanicRegistry CreateRegistry()
        {
            // Registry that receives every built-in mechanic factory.
            FactionMechanicRegistry registry = new FactionMechanicRegistry();
            RegisterAll(registry);
            return registry;
        }
    }
}

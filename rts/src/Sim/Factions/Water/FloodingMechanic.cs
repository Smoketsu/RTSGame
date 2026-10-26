// rts/src/Sim/Factions/Water/FloodingMechanic.cs
// Water faction signature mechanic: Flooding with tides and waves. Phase 0 ships the plug-in shell only.
// Part of Rts.Sim.Factions.Water. Design: structures release water that fills low ground; one shared
// 210 s tide clock per match; floods stop 4 tiles from enemy resources and drop-offs. Phase 1 builds it
// on the terrain grid; numbers come from data/tuning/flood.json and match.json.
using Rts.Sim.Core;

namespace Rts.Sim.Factions.Water
{
    /// <summary>Flooding plug-in. Phase 0: registered and hashed, no behaviour yet.</summary>
    public sealed class FloodingMechanic : IFactionMechanic
    {
        /// <summary>Mechanic ID as named in data/factions/water.json.</summary>
        public const string MechanicId = "flooding";

        public string Id => MechanicId;

        /// <summary>Called once before tick 0 runs.</summary>
        public void OnMatchStart(Simulation sim)
        {
            // Phase 1: convert tide_cycle_seconds to ticks and register flood sources.
        }

        /// <summary>Called once per tick after commands and movement.</summary>
        public void OnTick(Simulation sim)
        {
            // Phase 1: advance the tide on the match clock and redistribute water in fixed tile order.
        }

        /// <summary>Feeds the mechanic's state into the state hash; Phase 0 has only the ID.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
        }
    }
}

// rts/src/Sim/Factions/Skai/MomentumMechanic.cs
// Skai Riders signature mechanic: Momentum. Phase 0 ships the plug-in shell only; Phase 1 adds stacks.
// Part of Rts.Sim.Factions.Skai. Design: +1 stack per second moving, max 5, +4% speed and damage per
// stack, drain after 1.5 s still, lost when Grounded or slowed. Every number comes from data/tuning/momentum.json.
using Rts.Sim.Core;

namespace Rts.Sim.Factions.Skai
{
    /// <summary>Momentum plug-in. Phase 0: registered and hashed, no behaviour yet.</summary>
    public sealed class MomentumMechanic : IFactionMechanic
    {
        /// <summary>Mechanic ID as named in data/factions/skai.json.</summary>
        public const string MechanicId = "momentum";

        /// <summary>Mechanic ID as named in faction data.</summary>
        public string Id => MechanicId;

        /// <summary>Called once before tick 0 runs.</summary>
        public void OnMatchStart(Simulation sim)
        {
            // Phase 1: read momentum tuning into tick counts here.
        }

        /// <summary>Called once per tick after commands and movement.</summary>
        public void OnTick(Simulation sim)
        {
            // Phase 1: gain stacks for moving Skai units, drain for still ones, clear on Grounded or slowed.
        }

        /// <summary>Feeds the mechanic's state into the state hash; Phase 0 has only the ID.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
        }
    }
}

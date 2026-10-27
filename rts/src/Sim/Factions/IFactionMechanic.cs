// rts/src/Sim/Factions/IFactionMechanic.cs
// The one shared interface every faction signature mechanic implements (Momentum, Flooding, Tailoring,
// Scorching). Part of Rts.Sim.Factions. Rule 4 of the architecture: mechanics are plug-ins registered by
// ID; the core never names a faction. A mechanic instance is created per faction present in a match.
using Rts.Sim.Core;

namespace Rts.Sim.Factions
{
    /// <summary>A faction's signature mechanic. Implementations live in Factions/&lt;Faction&gt;/ and keep all their state hashed.</summary>
    public interface IFactionMechanic
    {
        /// <summary>Stable mechanic ID such as "momentum" or "flooding"; matches faction data (`mechanic_id`).</summary>
        string Id { get; }

        /// <summary>Called once by <see cref="Simulation.Start"/> after players and content exist, before tick 0 runs.</summary>
        void OnMatchStart(Simulation sim);

        /// <summary>Called once per tick inside <see cref="Simulation.Step"/>, after commands and movement, in ascending mechanic ID order.</summary>
        void OnTick(Simulation sim);

        /// <summary>Feeds every piece of mechanic state that affects future ticks into the state hash.</summary>
        void WriteHash(StateHasher hasher);
    }
}

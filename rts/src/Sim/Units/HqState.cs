// rts/src/Sim/Units/HqState.cs
// The four states of the mobile HQ unit. Part of Rts.Sim.Units. Stored on Unit, hashed as a byte, and driven by
// HqSystem and the root and uproot commands (Game Design Proposal, "The mobile HQ unit").
namespace Rts.Sim.Units
{
    /// <summary>Where an HQ unit is in its Rooted / Uprooted cycle. Values are stable: they appear in hashes and events.</summary>
    public enum HqState : byte
    {
        /// <summary>Planted next to a resource field: weapon on, structure armour, drop-off, trains workers, cannot move.</summary>
        Rooted = 0,

        /// <summary>Mobile: weapon off, reduced (uprooted) armour, not a drop-off, cannot train.</summary>
        Uprooted = 1,

        /// <summary>Counting down to Rooted (root_seconds). Cannot move; weapon off; uprooted armour.</summary>
        Rooting = 2,

        /// <summary>Counting down to Uprooted (uproot_seconds). Cannot move; weapon off; uprooted armour; no longer a drop-off.</summary>
        Uprooting = 3,
    }
}

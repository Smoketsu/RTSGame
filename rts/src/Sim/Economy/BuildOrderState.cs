// rts/src/Sim/Economy/BuildOrderState.cs
// The stages of a worker's build order (specs/worker_construction.md section 2.1). Part of Rts.Sim.Economy. Stored on Unit,
// hashed as a byte, advanced by EconomySystem.
namespace Rts.Sim.Economy
{
    /// <summary>Where a worker is in its build order: walk to the site, count down the placement time, then the structure appears. Values are stable: they appear in hashes.</summary>
    public enum BuildOrderState : byte
    {
        /// <summary>No build order.</summary>
        None = 0,

        /// <summary>Walking to the site.</summary>
        ToSite = 1,

        /// <summary>At the site, counting down worker_placement_seconds.</summary>
        Placing = 2,
    }
}

// rts/src/Sim/Economy/GatherState.cs
// The stages of a worker's gather loop. Part of Rts.Sim.Economy. Stored on Unit, hashed as a byte, advanced by EconomySystem.
namespace Rts.Sim.Economy
{
    /// <summary>Where a worker is in its loop: walk to the field, work, walk to a drop-off, deposit, repeat. Values are stable: they appear in hashes.</summary>
    public enum GatherState : byte
    {
        /// <summary>No gather order (or the field ran dry and nothing is carried).</summary>
        Idle = 0,

        /// <summary>Walking to the field's tile.</summary>
        ToField = 1,

        /// <summary>At the field, waiting gather_seconds_per_trip for a load.</summary>
        Gathering = 2,

        /// <summary>Carrying a load to the nearest friendly drop-off.</summary>
        ToDropOff = 3,
    }
}

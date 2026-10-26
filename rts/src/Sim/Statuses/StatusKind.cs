// rts/src/Sim/Statuses/StatusKind.cs
// The kinds of shared status any faction can apply. Part of Rts.Sim.Statuses (spec: rts/docs/specs/statuses.md).
// Values are stable: they appear in events, commands and the evaluation order of the status system.
namespace Rts.Sim.Statuses
{
    /// <summary>Which shared status an event or command is about. No status belongs to a faction.</summary>
    public enum StatusKind : byte
    {
        /// <summary>A flyer is forced onto the ground layer for a few seconds (Spoutling Drench, Gravity Spire, Downdraft).</summary>
        Grounded = 0,

        /// <summary>The unit moves at a fraction of its speed for a while; the strongest active slow wins.</summary>
        Slowed = 1,
    }
}

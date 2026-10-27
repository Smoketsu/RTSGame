// rts/src/Sim/Factions/Water/FloodMath.cs
// The unit of water the flooding maths counts in: the drop, one 256th of a depth unit, and the exact conversions between
// drops and Fix64 depth. Part of Rts.Sim.Factions.Water (spec: rts/docs/specs/flooding.md section 3). The factor is a
// representation constant like ADR-007's CostScale, not a tuning number: it fixes how finely water is divided so all flow
// maths is integer and exact.
using Rts.Sim.Core;

namespace Rts.Sim.Factions.Water
{
    /// <summary>Drops (integer water volume) and depth (Fix64) conversions.</summary>
    public static class FloodMath
    {
        /// <summary>Drops in one depth unit. Unit: drops per depth unit. Range: fixed 256 (2^8, so a drop is 2^24 raw Fix64 units).</summary>
        public const int DropsPerDepthUnit = 256;

        /// <summary>Number of raw Fix64 fraction bits a drop occupies: 32 fraction bits minus the 8 bits of <see cref="DropsPerDepthUnit"/>. Range: fixed 24.</summary>
        private const int DropShift = 24;

        /// <summary>Whole drops in a depth, rounding down (so a depth another system wrote with a fraction of a drop loses that fraction). Unit: drops. Range: 0 upward for non-negative depths.</summary>
        /// <param name="depth">A depth. Unit: depth units. Range: 0 to max_water_depth.</param>
        public static int ToDrops(Fix64 depth)
        {
            return (int)(depth.Raw >> DropShift);
        }

        /// <summary>The exact depth of a whole number of drops. Unit: depth units. Range: 0 to max_water_depth.</summary>
        /// <param name="drops">Water volume. Unit: drops. Range: 0 to the per-tile cap (max_water_depth times 256).</param>
        public static Fix64 ToDepth(int drops)
        {
            return Fix64.FromRaw((long)drops << DropShift);
        }

        /// <summary>A depth that is a multiple of 1/256 as drops, with RoundToInt(value * 256) as the spec words it (exact for such depths). Unit: drops. Range: 0 upward.</summary>
        /// <param name="depth">A depth validated as a multiple of 1/256.</param>
        public static int RoundToDrops(Fix64 depth)
        {
            return (depth * Fix64.FromInt(DropsPerDepthUnit)).RoundToInt();
        }
    }
}

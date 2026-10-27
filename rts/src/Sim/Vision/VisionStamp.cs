// rts/src/Sim/Vision/VisionStamp.cs
// The integer circle used for sight: the list of tile offsets within a sight radius, computed with integer squares and
// fixed-point comparison only (no floats, no square roots). Part of Rts.Sim.Vision (spec: rts/docs/specs/fog_of_war.md
// section 4). A pure function of the radius, so it is cached by VisionSystem and never hashed.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Vision
{
    /// <summary>One tile offset from a viewer's own tile.</summary>
    public readonly struct VisionOffset
    {
        /// <summary>Offset along X. Unit: tiles. Range: -64 to 64 (a radius is at most 64).</summary>
        public readonly int Dx;

        /// <summary>Offset along Y. Unit: tiles. Range: -64 to 64.</summary>
        public readonly int Dy;

        /// <summary>Stores the offset.</summary>
        /// <param name="dx">Offset along X. Unit: tiles.</param>
        /// <param name="dy">Offset along Y. Unit: tiles.</param>
        public VisionOffset(int dx, int dy)
        {
            Dx = dx;
            Dy = dy;
        }
    }

    /// <summary>Builds the tile offsets of a sight circle.</summary>
    public static class VisionStamp
    {
        /// <summary>
        /// Offsets (dx, dy) whose tile centre lies within <paramref name="radius"/> of the viewer's tile centre, in the fixed
        /// order dy from -r to r, then dx from -r to r, where r is the radius rounded up. The offset (0, 0) is always
        /// included, so a radius of 0 gives exactly one tile; radius 1 gives 5, 2.5 gives 21 and 3 gives 29.
        /// </summary>
        /// <param name="radius">Sight radius. Unit: tiles (fixed-point). Range: 0 to 64; a negative radius counts as 0.</param>
        public static VisionOffset[] Offsets(Fix64 radius)
        {
            // Radius used for the test, never negative. Unit: tiles.
            Fix64 clamped = radius < Fix64.Zero ? Fix64.Zero : radius;
            // Whole-tile bound of the scan. Unit: tiles. Range: 0 to 64.
            int bound = clamped.CeilingToInt();
            // Squared radius, compared with the squared integer offset. Unit: squared tiles (fixed-point).
            Fix64 radiusSquared = clamped * clamped;
            // Offsets found, in scan order.
            List<VisionOffset> found = new List<VisionOffset>();
            for (int dy = -bound; dy <= bound; dy++)
            {
                for (int dx = -bound; dx <= bound; dx++)
                {
                    // Squared tile-centre distance of this offset. Unit: squared tiles. Range: 0 to 2 * bound^2.
                    int distanceSquared = dx * dx + dy * dy;
                    if (Fix64.FromInt(distanceSquared) <= radiusSquared)
                    {
                        found.Add(new VisionOffset(dx, dy));
                    }
                }
            }
            return found.ToArray();
        }
    }
}

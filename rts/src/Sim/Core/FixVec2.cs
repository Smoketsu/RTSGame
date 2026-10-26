// rts/src/Sim/Core/FixVec2.cs
// Two-component fixed-point vector used for positions and velocities on the map plane.
// Part of Rts.Sim.Core. X runs east, Y runs north; units are tiles unless a field says otherwise.
// Height lives on the terrain grid (Phase 1), not in this vector.
using System;

namespace Rts.Sim.Core
{
    /// <summary>A 2D vector of <see cref="Fix64"/> components. Immutable; every operation returns a new value.</summary>
    public readonly struct FixVec2 : IEquatable<FixVec2>
    {
        /// <summary>East-west component. Unit: tiles (for positions) or tiles per tick (for velocities). Range: any Fix64; command positions are limited to ±<see cref="Simulation.WorldBoundTiles"/>.</summary>
        public readonly Fix64 X;

        /// <summary>North-south component. Same unit and range as <see cref="X"/>.</summary>
        public readonly Fix64 Y;

        /// <summary>The origin (0, 0).</summary>
        public static readonly FixVec2 Zero = new FixVec2(Fix64.Zero, Fix64.Zero);

        /// <summary>
        /// Largest component magnitude whose square still fits Fix64 with room for the sum of two: 2^15 tiles (2 × 2^30
        /// is below the 2^31 saturation point). Offsets beyond it are pre-scaled in <see cref="MoveTowards"/>.
        /// </summary>
        public static readonly Fix64 SafeComponentLimit = Fix64.FromInt(1 << 15);

        /// <summary>Builds a vector from two components.</summary>
        public FixVec2(Fix64 x, Fix64 y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Builds a vector from whole-number components, exactly.</summary>
        public static FixVec2 FromInts(int x, int y)
        {
            return new FixVec2(Fix64.FromInt(x), Fix64.FromInt(y));
        }

        /// <summary>Component-wise saturating addition.</summary>
        public static FixVec2 operator +(FixVec2 a, FixVec2 b)
        {
            return new FixVec2(a.X + b.X, a.Y + b.Y);
        }

        /// <summary>Component-wise saturating subtraction.</summary>
        public static FixVec2 operator -(FixVec2 a, FixVec2 b)
        {
            return new FixVec2(a.X - b.X, a.Y - b.Y);
        }

        /// <summary>Scales both components by a scalar.</summary>
        public static FixVec2 operator *(FixVec2 a, Fix64 scale)
        {
            return new FixVec2(a.X * scale, a.Y * scale);
        }

        /// <summary>Squared length. Cheaper than <see cref="Length"/> and exact; prefer it for comparisons.</summary>
        public Fix64 LengthSquared()
        {
            return X * X + Y * Y;
        }

        /// <summary>Euclidean length, truncated to 2^-32. Saturates for components at or beyond <see cref="SafeComponentLimit"/> (32 768 tiles), far larger than any map; <see cref="MoveTowards"/> guards against that itself.</summary>
        public Fix64 Length()
        {
            return Fix64.Sqrt(LengthSquared());
        }

        /// <summary>
        /// Moves from this point toward <paramref name="target"/> by at most <paramref name="maxDistance"/>.
        /// Returns <paramref name="target"/> exactly when it is within reach, so arrival checks can use equality.
        /// </summary>
        /// <param name="target">Destination point, in tiles.</param>
        /// <param name="maxDistance">Largest step allowed this call, in tiles. Must be non-negative.</param>
        public FixVec2 MoveTowards(FixVec2 target, Fix64 maxDistance)
        {
            if (maxDistance < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), "MoveTowards: maxDistance must be non-negative.");
            }
            // Offset still to travel.
            FixVec2 delta = target - this;
            // Direction vector used for the ratio below. When a component is at or beyond SafeComponentLimit, squaring
            // would saturate Fix64, so the offset is scaled down by 2^16 first; the ratio step = delta × max / |delta|
            // is unchanged by uniform scaling, and such a far target is never "in reach" in one step.
            FixVec2 direction = delta;
            // True when the far-target path was taken and the in-reach shortcut must be skipped.
            bool scaled = false;
            if (delta.X.Abs() >= SafeComponentLimit || delta.Y.Abs() >= SafeComponentLimit)
            {
                direction = new FixVec2(Fix64.FromRaw(delta.X.Raw >> 16), Fix64.FromRaw(delta.Y.Raw >> 16));
                scaled = true;
            }
            Fix64 distanceSquared = direction.LengthSquared();
            // Comparing squares avoids a square root when the target is in reach.
            if (!scaled && distanceSquared <= maxDistance * maxDistance)
            {
                return target;
            }
            Fix64 distance = Fix64.Sqrt(distanceSquared);
            if (distance == Fix64.Zero)
            {
                // Only possible when the offset is non-zero but below 2^-16 of a tile after scaling; treat as arrived.
                return target;
            }
            // Scale each component by maxDistance / distance, multiplying first and dividing last so truncation
            // costs at most one raw unit per axis per call instead of compounding through a truncated fraction.
            // direction components are below 2^15 tiles and maxDistance is a per-tick step, so the product cannot saturate.
            FixVec2 step = new FixVec2(direction.X * maxDistance / distance, direction.Y * maxDistance / distance);
            return this + step;
        }

        /// <summary>True when both components are equal.</summary>
        public bool Equals(FixVec2 other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <summary>True when <paramref name="obj"/> is the same type with the same value.</summary>
        public override bool Equals(object? obj)
        {
            return obj is FixVec2 other && Equals(other);
        }

        /// <summary>Hash for dictionaries outside the simulation hot path; state hashing uses <see cref="StateHasher"/>.</summary>
        public override int GetHashCode()
        {
            return unchecked(X.GetHashCode() * 397) ^ Y.GetHashCode();
        }

        public static bool operator ==(FixVec2 a, FixVec2 b) => a.Equals(b);
        public static bool operator !=(FixVec2 a, FixVec2 b) => !a.Equals(b);

        /// <summary>Text such as "(1.5, -2)" for logs and tests.</summary>
        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }
}

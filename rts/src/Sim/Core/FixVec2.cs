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
        /// <summary>East-west component. Unit: tiles (for positions) or tiles per tick (for velocities).</summary>
        public readonly Fix64 X;

        /// <summary>North-south component. Same unit as <see cref="X"/>.</summary>
        public readonly Fix64 Y;

        /// <summary>The origin (0, 0).</summary>
        public static readonly FixVec2 Zero = new FixVec2(Fix64.Zero, Fix64.Zero);

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

        /// <summary>Euclidean length, truncated to 2^-32. Saturates for components beyond about 32 000 tiles, far larger than any map.</summary>
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
            Fix64 distanceSquared = delta.LengthSquared();
            // Comparing squares avoids a square root when the target is in reach.
            if (distanceSquared <= maxDistance * maxDistance)
            {
                return target;
            }
            Fix64 distance = Fix64.Sqrt(distanceSquared);
            // Scale each component by maxDistance / distance, multiplying first and dividing last so truncation
            // costs at most one raw unit per axis per call instead of compounding through a truncated fraction.
            // The product cannot saturate on any map: components stay below a few thousand tiles.
            FixVec2 step = new FixVec2(delta.X * maxDistance / distance, delta.Y * maxDistance / distance);
            return this + step;
        }

        /// <summary>True when both components are equal.</summary>
        public bool Equals(FixVec2 other)
        {
            return X == other.X && Y == other.Y;
        }

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

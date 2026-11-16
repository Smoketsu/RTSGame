// rts/src/Sim/Units/ShieldDomeDefinition.cs
// Immutable description of an always-on dome shield, parsed from the optional "shield_dome" object of a unit file (P-003 row 6).
// Part of Rts.Sim.Units. Read by CombatSystem (Rts.Sim.Combat): the dome absorbs ranged hits fired from outside it at its owner's
// and allies' units inside it. Generic unit data, so the core never names a faction. Every number comes from data (rule 3).
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>One dome shield: radius, damage pool and refill rules. Never changes during a match.</summary>
    public sealed class ShieldDomeDefinition
    {
        /// <summary>Dome radius around the unit's centre. Unit: tiles (fixed-point). Range: 0.5 to 16.</summary>
        public Fix64 RadiusTiles { get; }

        /// <summary>Damage the dome absorbs before it goes down. Unit: health points (fixed-point). Range: 1 to 100000.</summary>
        public Fix64 Pool { get; }

        /// <summary>Time without absorbing before the pool refills. Unit: seconds (fixed-point). Range: 0 to 60.</summary>
        public Fix64 RegenDelaySeconds { get; }

        /// <summary>Refill rate. Unit: health points per second (fixed-point). Range: 0.01 to 10000.</summary>
        public Fix64 RegenPerSecond { get; }

        /// <summary>Builds a dome, validating every range. The loader turns a failure into an error naming the file and field.</summary>
        /// <param name="radiusTiles">Radius in tiles, 0.5 to 16.</param>
        /// <param name="pool">Pool in health points, 1 to 100000.</param>
        /// <param name="regenDelaySeconds">Seconds before refill starts, 0 to 60.</param>
        /// <param name="regenPerSecond">Refill in health points per second, 0.01 to 10000.</param>
        public ShieldDomeDefinition(Fix64 radiusTiles, Fix64 pool, Fix64 regenDelaySeconds, Fix64 regenPerSecond)
        {
            if (radiusTiles < Fix64.Parse("0.5") || radiusTiles > Fix64.FromInt(16))
            {
                throw new ArgumentOutOfRangeException(nameof(radiusTiles), "shield_dome.radius_tiles must be 0.5 to 16.");
            }
            if (pool < Fix64.One || pool > Fix64.FromInt(100000))
            {
                throw new ArgumentOutOfRangeException(nameof(pool), "shield_dome.pool must be 1 to 100000.");
            }
            if (regenDelaySeconds < Fix64.Zero || regenDelaySeconds > Fix64.FromInt(60))
            {
                throw new ArgumentOutOfRangeException(nameof(regenDelaySeconds), "shield_dome.regen_delay_seconds must be 0 to 60.");
            }
            if (regenPerSecond < Fix64.Parse("0.01") || regenPerSecond > Fix64.FromInt(10000))
            {
                throw new ArgumentOutOfRangeException(nameof(regenPerSecond), "shield_dome.regen_per_second must be 0.01 to 10000.");
            }
            RadiusTiles = radiusTiles;
            Pool = pool;
            RegenDelaySeconds = regenDelaySeconds;
            RegenPerSecond = regenPerSecond;
        }

        /// <summary>Feeds every field into a hash in a fixed order (used by the content digest).</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(RadiusTiles);
            hasher.Write(Pool);
            hasher.Write(RegenDelaySeconds);
            hasher.Write(RegenPerSecond);
        }
    }
}

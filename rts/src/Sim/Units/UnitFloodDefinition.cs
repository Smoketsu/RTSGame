// rts/src/Sim/Units/UnitFloodDefinition.cs
// Immutable flooding part of a unit definition: how much water the unit releases per second (a flood source), its share of
// its team's high-tide volume, and whether it can cast the first-pass wave. Part of Rts.Sim.Units. Parsed from the optional
// unit-file keys flood_source_output_per_second, flood_source_capacity and can_cast_wave (rts/src/Sim/Data/UnitSchema.cs);
// read by the flooding mechanic (rts/src/Sim/Factions/Water, spec: rts/docs/specs/flooding.md section 2). Every value comes
// from data (rule 3); a unit without the keys gets <see cref="None"/>.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>The flooding rules attached to one unit type. Never changes during a match.</summary>
    public sealed class UnitFloodDefinition
    {
        /// <summary>Largest output a unit file may declare. Unit: depth-unit tiles per second. Range: fixed schema bound, 64.</summary>
        public static readonly Fix64 MaxOutputPerSecond = Fix64.FromInt(64);

        /// <summary>Largest capacity a unit file may declare. Unit: depth-unit tiles. Range: fixed schema bound, 4096.</summary>
        public static readonly Fix64 MaxCapacity = Fix64.FromInt(4096);

        /// <summary>The definition of a unit that is no flood source and casts no wave (output 0, capacity 0, cannot cast).</summary>
        public static readonly UnitFloodDefinition None = new UnitFloodDefinition(Fix64.Zero, Fix64.Zero, false);

        /// <summary>
        /// Water released per second while the unit is a live flood source. Unit: depth-unit tiles per second (fixed-point;
        /// 1 means one tile of depth 1 per second). Range: 0 to 64; 0 means the unit is not a flood source.
        /// </summary>
        public Fix64 OutputPerSecond { get; }

        /// <summary>
        /// The unit's share of its team's high-tide water volume. Unit: depth-unit tiles (fixed-point). Range: 0 to 4096;
        /// greater than 0 whenever <see cref="OutputPerSecond"/> is greater than 0.
        /// </summary>
        public Fix64 Capacity { get; }

        /// <summary>True when the unit may be given the cast_wave command (the Tidecaller). Range: true or false.</summary>
        public bool CanCastWave { get; }

        /// <summary>True when the unit releases water (output above 0).</summary>
        public bool IsSource => OutputPerSecond > Fix64.Zero;

        /// <summary>Builds the flooding rules, validating every range. The loader turns a failure into an error naming the file and field.</summary>
        /// <param name="outputPerSecond">Output in depth-unit tiles per second, 0 to 64.</param>
        /// <param name="capacity">Capacity in depth-unit tiles, 0 to 4096, above 0 when the output is above 0.</param>
        /// <param name="canCastWave">True for units that can cast the wave.</param>
        public UnitFloodDefinition(Fix64 outputPerSecond, Fix64 capacity, bool canCastWave)
        {
            if (outputPerSecond < Fix64.Zero || outputPerSecond > MaxOutputPerSecond)
            {
                throw new ArgumentOutOfRangeException(nameof(outputPerSecond), "flood_source_output_per_second must be 0 to 64.");
            }
            if (capacity < Fix64.Zero || capacity > MaxCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "flood_source_capacity must be 0 to 4096.");
            }
            if (outputPerSecond > Fix64.Zero && capacity <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "flood_source_capacity must be above 0 when flood_source_output_per_second is above 0.");
            }
            OutputPerSecond = outputPerSecond;
            Capacity = capacity;
            CanCastWave = canCastWave;
        }

        /// <summary>Feeds every field into a hash in a fixed order (used by the content digest).</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(OutputPerSecond);
            hasher.Write(Capacity);
            hasher.Write(CanCastWave);
        }
    }
}

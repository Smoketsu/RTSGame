// rts/src/Sim/Units/UnitEconomyDefinition.cs
// Immutable economy part of a unit definition: build time, worker and drop-off flags, what the unit can train, the
// static-defence flag, footprint size and supply provided. Part of Rts.Sim.Units. Parsed from the optional economy
// keys of a unit file (can_gather, is_drop_off, trains, build_seconds, is_static_defence, footprint, provides_supply);
// read by the economy system (Rts.Sim.Economy) and the train and build commands. Every value comes from data (rule 3).
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>The economy rules attached to one unit type. Never changes during a match.</summary>
    public sealed class UnitEconomyDefinition
    {
        /// <summary>Build time used when a unit file has no build_seconds and for hand-built definitions. Unit: seconds. Range: fixed schema default, 10 (inside the data range of 0.05 to 3600).</summary>
        public static readonly Fix64 DefaultBuildSeconds = Fix64.FromInt(10);

        /// <summary>The definition of an ordinary unit that gathers nothing, trains nothing and has no footprint, with the default build time.</summary>
        public static readonly UnitEconomyDefinition Default = new UnitEconomyDefinition(DefaultBuildSeconds, false, false, new List<string>(), false, 0, 0);

        /// <summary>
        /// Time to train this unit at a producer, or to construct it when it is a structure. Unit: seconds (fixed-point).
        /// Range: 0.05 to 3600 (converted to whole ticks, at least 1, with the match clock).
        /// </summary>
        public Fix64 BuildSeconds { get; }

        /// <summary>True when the unit is a worker that can gather from resource fields and place structures.</summary>
        public bool CanGather { get; }

        /// <summary>True when workers may deposit gathered resources at this unit (once it is complete). A Rooted HQ is a drop-off without this flag.</summary>
        public bool IsDropOff { get; }

        /// <summary>IDs of the units this unit can train, in file order. Range: 0 or more unit IDs, no duplicates, each checked to exist by the loader.</summary>
        public IReadOnlyList<string> Trains { get; }

        /// <summary>True for a static defence (perch tower, coral turret): costs the tuning supply and counts against the per-base cap.</summary>
        public bool IsStaticDefence { get; }

        /// <summary>Tiles blocked by the unit while it lives. Unit: tiles. Range: 0 (mobile unit) or 1 (single-tile structure, the only size in Phase 1).</summary>
        public int FootprintTiles { get; }

        /// <summary>Supply added to the owner's cap while the unit stands complete. Unit: supply. Range: 0 to 100.</summary>
        public int ProvidesSupply { get; }

        /// <summary>Builds the economy rules, validating every range. The loader turns a failure into an error naming the file and field.</summary>
        /// <param name="buildSeconds">Train or construction time in seconds, 0.05 to 3600.</param>
        /// <param name="canGather">True for workers.</param>
        /// <param name="isDropOff">True for drop-off structures.</param>
        /// <param name="trains">Unit IDs this unit can train; not null.</param>
        /// <param name="isStaticDefence">True for static defences.</param>
        /// <param name="footprintTiles">0 or 1.</param>
        /// <param name="providesSupply">Supply provided, 0 or more.</param>
        public UnitEconomyDefinition(Fix64 buildSeconds, bool canGather, bool isDropOff, IReadOnlyList<string> trains,
            bool isStaticDefence, int footprintTiles, int providesSupply)
        {
            if (buildSeconds < Fix64.Parse("0.05"))
            {
                throw new ArgumentOutOfRangeException(nameof(buildSeconds), "build_seconds must be at least 0.05.");
            }
            if (trains == null)
            {
                throw new ArgumentNullException(nameof(trains));
            }
            if (footprintTiles < 0 || footprintTiles > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(footprintTiles), "footprint must be 0 or 1 in Phase 1.");
            }
            if (providesSupply < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(providesSupply), "provides_supply must be 0 or more.");
            }
            BuildSeconds = buildSeconds;
            CanGather = canGather;
            IsDropOff = isDropOff;
            Trains = trains;
            IsStaticDefence = isStaticDefence;
            FootprintTiles = footprintTiles;
            ProvidesSupply = providesSupply;
        }

        /// <summary>Feeds every field into a hash in a fixed order (used by the content digest).</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(BuildSeconds);
            hasher.Write(CanGather);
            hasher.Write(IsDropOff);
            hasher.Write(Trains.Count);
            foreach (string id in Trains)
            {
                hasher.Write(id);
            }
            hasher.Write(IsStaticDefence);
            hasher.Write(FootprintTiles);
            hasher.Write(ProvidesSupply);
        }
    }
}

// rts/src/Sim/Units/HqDefinition.cs
// Immutable HQ-specific part of a unit definition, parsed from the optional "hq" object of a unit file. Part of
// Rts.Sim.Units. A unit with a non-null UnitDefinition.Hq is a mobile HQ: it roots and uproots, its weapon works only
// while Rooted, and its destruction defeats its owner (HqSystem). Every value comes from data (rule 3).
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>The HQ rules attached to one unit type. Never changes during a match.</summary>
    public sealed class HqDefinition
    {
        /// <summary>Armour type while Rooted, a key of the combat armour types (tuning/combat.json). Range: non-empty text; checked against the table by the loader.</summary>
        public string RootedArmourType { get; }

        /// <summary>Armour type while Uprooted, Rooting or Uprooting. Range: non-empty text; checked against the table by the loader.</summary>
        public string UprootedArmourType { get; }

        /// <summary>ID of the worker unit a Rooted HQ may train, such as "skai.fledgling". Range: non-empty unit ID; checked to exist by the loader.</summary>
        public string WorkerUnitId { get; }

        /// <summary>Builds the HQ rules; every argument must be non-empty.</summary>
        /// <param name="rootedArmourType">Armour type while Rooted.</param>
        /// <param name="uprootedArmourType">Armour type otherwise.</param>
        /// <param name="workerUnitId">Worker the Rooted HQ trains.</param>
        public HqDefinition(string rootedArmourType, string uprootedArmourType, string workerUnitId)
        {
            if (string.IsNullOrEmpty(rootedArmourType))
            {
                throw new ArgumentException("hq.rooted_armour_type must not be empty.", nameof(rootedArmourType));
            }
            if (string.IsNullOrEmpty(uprootedArmourType))
            {
                throw new ArgumentException("hq.uprooted_armour_type must not be empty.", nameof(uprootedArmourType));
            }
            if (string.IsNullOrEmpty(workerUnitId))
            {
                throw new ArgumentException("hq.worker_unit must not be empty.", nameof(workerUnitId));
            }
            RootedArmourType = rootedArmourType;
            UprootedArmourType = uprootedArmourType;
            WorkerUnitId = workerUnitId;
        }

        /// <summary>Feeds every field into a hash in a fixed order (used by the content digest).</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(RootedArmourType);
            hasher.Write(UprootedArmourType);
            hasher.Write(WorkerUnitId);
        }
    }
}

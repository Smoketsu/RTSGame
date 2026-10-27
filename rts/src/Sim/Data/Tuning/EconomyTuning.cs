// rts/src/Sim/Data/Tuning/EconomyTuning.cs
// Typed view of rts/data/tuning/economy.json: starting resources, base supply, worker gathering trips, the production
// queue length, the base radius for the static defence cap, the interaction range and the construction start health.
// Part of Rts.Sim.Data. Read by the economy system (Rts.Sim.Economy) and the train and build commands. When a key is added
// to the JSON file, add the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Immutable values of tuning/economy.json, validated against the ranges in the file's comments. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class EconomyTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/economy.json";

        /// <summary>Common resource each player starts with. Unit: resource units. Range: 0 to 100000. Source: Economy (two resources); placeholder.</summary>
        public int StartingCommon { get; }

        /// <summary>Rare resource each player starts with. Unit: resource units. Range: 0 to 100000. Source: Economy; placeholder.</summary>
        public int StartingRare { get; }

        /// <summary>Supply cap before any supply building stands; the cap grows with each building's provides_supply up to supply_cap in supply.json. Unit: supply. Range: 0 to 300. Source: Economy / Supply cap; placeholder.</summary>
        public int BaseSupply { get; }

        /// <summary>Resource a worker takes from a field in one trip (less when the field holds less). Unit: resource units per trip. Range: 1 to 100. Source: Economy; placeholder.</summary>
        public int GatherAmountPerTrip { get; }

        /// <summary>Time a worker spends at a field before it has taken one load. Unit: seconds. Range: 0.05 to 60. Source: Economy; placeholder.</summary>
        public Fix64 GatherSecondsPerTrip { get; }

        /// <summary>Speed multiplier for a worker that is carrying resources. Unit: multiplier of base speed. Range: 0.1 to 2. Source: Economy; placeholder (1.0 means carrying does not slow a worker).</summary>
        public Fix64 WorkerSpeedMultiplierWhileCarrying { get; }

        /// <summary>Longest production queue one producer holds (the unit being trained counts). Unit: queue entries (count). Range: 1 to 20. Source: Economy; placeholder.</summary>
        public int MaxQueueLength { get; }

        /// <summary>Radius around a drop-off that counts as one base for the static defence cap. Unit: tiles. Range: 2 to 64. Source: Economy / Supply cap; placeholder.</summary>
        public Fix64 BaseRadiusTiles { get; }

        /// <summary>How close a worker must get to a field or drop-off (edge distance for a drop-off) to gather or deposit; never less than the movement arrival radius. Unit: tiles. Range: 0.5 to 4. Source: Economy; placeholder.</summary>
        public Fix64 InteractRangeTiles { get; }

        /// <summary>Health a structure starts with when placed, as a fraction of its maximum. Unit: fraction of max health (0.1 = 10%). Range: 0.01 to 1. Source: Economy; placeholder.</summary>
        public Fix64 ConstructionStartHealthFraction { get; }

        /// <summary>How many rings of tiles around a producer are searched for a free tile when a trained unit appears. Unit: tiles (Chebyshev distance from the producer's tile). Range: 1 to 8. Source: Economy (production queue); placeholder.</summary>
        public int SpawnSearchRadiusTiles { get; }

        /// <summary>Workers each player starts with beside the HQ in a standard match start. Unit: workers (count). Range: 0 to 20. Source: Core gameplay loop / Opening ("spawn with the mobile HQ unit and a few workers"); PLACEHOLDER count.</summary>
        public int StartingWorkerCount { get; }

        /// <summary>Time a worker spends at a field per load, converted from <see cref="GatherSecondsPerTrip"/> to whole simulation ticks (rounded to nearest, ADR-004; at least 1). Unit: ticks. Range: 1 upward.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int GatherTicksPerTrip(MatchClock clock)
        {
            return System.Math.Max(1, clock.SecondsToTicks(GatherSecondsPerTrip));
        }

        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public EconomyTuning(JsonValue root)
        {
            root.RejectUnknownKeys("starting_common", "starting_rare", "base_supply", "gather_amount_per_trip", "gather_seconds_per_trip",
                "worker_speed_multiplier_while_carrying", "max_queue_length", "base_radius_tiles", "interact_range_tiles", "construction_start_health_fraction", "spawn_search_radius_tiles", "starting_worker_count");
            StartingCommon = root.RequireInt("starting_common", 0, 100000);
            StartingRare = root.RequireInt("starting_rare", 0, 100000);
            BaseSupply = root.RequireInt("base_supply", 0, 300);
            GatherAmountPerTrip = root.RequireInt("gather_amount_per_trip", 1, 100);
            GatherSecondsPerTrip = root.RequireFix64("gather_seconds_per_trip", "0.05", "60");
            WorkerSpeedMultiplierWhileCarrying = root.RequireFix64("worker_speed_multiplier_while_carrying", "0.1", "2");
            MaxQueueLength = root.RequireInt("max_queue_length", 1, 20);
            BaseRadiusTiles = root.RequireFix64("base_radius_tiles", "2", "64");
            InteractRangeTiles = root.RequireFix64("interact_range_tiles", "0.5", "4");
            ConstructionStartHealthFraction = root.RequireFix64("construction_start_health_fraction", "0.01", "1");
            SpawnSearchRadiusTiles = root.RequireInt("spawn_search_radius_tiles", 1, 8);
            StartingWorkerCount = root.RequireInt("starting_worker_count", 0, 20);
        }
    }
}

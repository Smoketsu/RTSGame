// rts/src/Sim/Data/Tuning/HarnessTuning.cs
// Typed view of rts/data/tuning/harness.json. Part of Rts.Sim.Data. Balance harness run sizes and hard limits. Read by the balance harness. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Balance harness run sizes and the hard limits it checks for match length and HQ survivability. Loaded from "tuning/harness.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class HarnessTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/harness.json";

        /// <summary>Number of simulated games run for each matchup on each map. Unit: games per matchup per map (count). Range: 50 to 5000. Source: Matchups and balance / Balance philosophy.</summary>
        public int GamesPerMatchupPerMap { get; }

        /// <summary>Maximum allowed median match length for water versus earth. Unit: seconds. Range: 600 to 1800. Source: Balance philosophy.</summary>
        public Fix64 WaterVsEarthMedianMaxSeconds { get; }

        /// <summary>Minimum HQ survival time the harness checks. Same check as hq.json. Unit: seconds. Range: 5 to 120. Source: Balance philosophy.</summary>
        public Fix64 HqFloorSeconds { get; }

        /// <summary>Supply value of the army the harness uses for the HQ floor check. Same check as hq.json. Unit: supply. Range: 1 to 200. Source: Balance philosophy.</summary>
        public int HqFloorSupply { get; }

        /// <summary>Maximum allowed median match length for water versus earth, converted from <see cref="WaterVsEarthMedianMaxSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 600 to 1800 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int WaterVsEarthMedianMaxTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(WaterVsEarthMedianMaxSeconds);
        }

        /// <summary>Minimum HQ survival time the harness checks. Same check as hq.json, converted from <see cref="HqFloorSeconds"/> to whole simulation ticks (rounded to nearest, ADR-004). Unit: ticks. Range: 5 to 120 seconds times the tick rate.</summary>
        /// <param name="clock">The match clock whose tick rate is used.</param>
        public int HqFloorTicks(MatchClock clock)
        {
            return clock.SecondsToTicks(HqFloorSeconds);
        }
        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public HarnessTuning(JsonValue root)
        {
            root.RejectUnknownKeys("games_per_matchup_per_map", "water_vs_earth_median_max_seconds", "hq_floor_seconds", "hq_floor_supply");
            GamesPerMatchupPerMap = root.RequireInt("games_per_matchup_per_map", 50, 5000);
            WaterVsEarthMedianMaxSeconds = root.RequireFix64("water_vs_earth_median_max_seconds", "600", "1800");
            HqFloorSeconds = root.RequireFix64("hq_floor_seconds", "5", "120");
            HqFloorSupply = root.RequireInt("hq_floor_supply", 1, 200);
        }
    }
}

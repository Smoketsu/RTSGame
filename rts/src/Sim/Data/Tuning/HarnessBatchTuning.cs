// rts/src/Sim/Data/Tuning/HarnessBatchTuning.cs
// Typed view of rts/data/tuning/harness_batch.json: the batch settings of the full-scale balance harness (rts/tools/BalanceHarness).
// Part of Rts.Sim.Data. No simulation system reads these values, so DataLoader leaves the file out of Tuning.SourceDigest: changing
// a batch size or a band never changes a state hash or invalidates a replay (spec: rts/docs/specs/balance_harness.md section 2).
// Cross-checks that need the content (matchup IDs name factions with an HQ, map IDs exist) are made by the harness, not here.
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Immutable values of tuning/harness_batch.json, validated against the ranges in the file's comments.</summary>
    public sealed class HarnessBatchTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/harness_batch.json";

        /// <summary>Reduced batch size for CI. Unit: games per matchup per map (count). Range: 1 to 500. Source: PHASE2_PLAN exit test ("for example 20").</summary>
        public int CiGamesPerMatchupPerMap { get; }

        /// <summary>Seed every game seed is derived from. Unit: integer. Range: 0 to 2147483647. Source: PLACEHOLDER.</summary>
        public long BaseSeed { get; }

        /// <summary>Faction pairs played, each "a_vs_b" with a before b in ordinal order. Unit: matchup IDs. Range: 1 to 64 entries. Source: Matchups and balance.</summary>
        public IReadOnlyList<string> Matchups { get; }

        /// <summary>Maps played; empty means every map with exactly 2 spawns. Unit: map IDs. Range: 0 to 32 entries. Source: Matchups and balance.</summary>
        public IReadOnlyList<string> Maps { get; }

        /// <summary>AI profile both slots play. Unit: profile ID (a file under rts/data/ai). Range: non-empty. Source: Matchups and balance.</summary>
        public string AiProfile { get; }

        /// <summary>Tick limit per game; a game still undecided there is undecided. Unit: seconds. Range: 300 to 7200. Source: PLACEHOLDER.</summary>
        public int MaxGameSeconds { get; }

        /// <summary>Lower edge of the acceptable win rate of the first-named faction. Unit: fraction. Range: 0 to 0.5. Source: Balance philosophy.</summary>
        public Fix64 WinRateBandMin { get; }

        /// <summary>Upper edge of the acceptable win rate of the first-named faction. Unit: fraction. Range: 0.5 to 1. Source: Balance philosophy.</summary>
        public Fix64 WinRateBandMax { get; }

        /// <summary>Width of the Wilson interval (1.96 is 95 percent). Unit: z-score. Range: 1 to 4. Source: standard statistics.</summary>
        public Fix64 ConfidenceZ { get; }

        /// <summary>Warn when more of a cell's games than this are undecided. Unit: fraction. Range: 0 to 1. Source: PLACEHOLDER.</summary>
        public Fix64 MaxUndecidedFraction { get; }

        /// <summary>Matchup the water-versus-earth median length limit applies to. Unit: matchup ID. Range: "a_vs_b". Source: Balance philosophy.</summary>
        public string MedianCapMatchup { get; }

        /// <summary>Longest HQ floor trial. Unit: seconds. Range: 30 to 1800. Source: PLACEHOLDER.</summary>
        public int HqFloorMaxTestSeconds { get; }

        /// <summary>Side of the flat HQ floor arena. Unit: tiles. Range: 32 to 128. Source: PLACEHOLDER.</summary>
        public int HqArenaSizeTiles { get; }

        /// <summary>Gap between the HQ's weapon reach and the nearest attacker at spawn. Unit: tiles. Range: 0 to 16. Source: PLACEHOLDER.</summary>
        public int HqFloorStandoffTiles { get; }

        /// <summary>Games per (map, matchup) re-run in fresh processes to check determinism. Unit: games (count). Range: 0 to 10. Source: PLACEHOLDER.</summary>
        public int VerifyGamesPerCell { get; }

        /// <summary>Games per worker process call. Unit: games (count). Range: 1 to 500. Source: PLACEHOLDER.</summary>
        public int ChunkGames { get; }

        /// <summary>Smallest share of distinct games among a cell's decided games before the harness warns. Unit: fraction. Range: 0 to 1. Source: skirmish_ai_v4.md S.2.</summary>
        public Fix64 MinDistinctFraction { get; }

        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public HarnessBatchTuning(JsonValue root)
        {
            root.RejectUnknownKeys("ci_games_per_matchup_per_map", "base_seed", "matchups", "maps", "ai_profile", "max_game_seconds",
                "win_rate_band_min", "win_rate_band_max", "confidence_z", "max_undecided_fraction", "median_cap_matchup",
                "hq_floor_max_test_seconds", "hq_arena_size_tiles", "hq_floor_standoff_tiles", "verify_games_per_cell", "chunk_games", "min_distinct_fraction");
            CiGamesPerMatchupPerMap = root.RequireInt("ci_games_per_matchup_per_map", 1, 500);
            BaseSeed = root.RequireInt("base_seed", 0, int.MaxValue);
            Matchups = ReadIds(root, "matchups", 1, 64);
            Maps = ReadIds(root, "maps", 0, 32);
            AiProfile = root.RequireNonEmptyString("ai_profile");
            MaxGameSeconds = root.RequireInt("max_game_seconds", 300, 7200);
            WinRateBandMin = root.RequireFix64("win_rate_band_min", "0", "0.5");
            WinRateBandMax = root.RequireFix64("win_rate_band_max", "0.5", "1");
            ConfidenceZ = root.RequireFix64("confidence_z", "1", "4");
            MaxUndecidedFraction = root.RequireFix64("max_undecided_fraction", "0", "1");
            MedianCapMatchup = root.RequireNonEmptyString("median_cap_matchup");
            HqFloorMaxTestSeconds = root.RequireInt("hq_floor_max_test_seconds", 30, 1800);
            HqArenaSizeTiles = root.RequireInt("hq_arena_size_tiles", 32, 128);
            HqFloorStandoffTiles = root.RequireInt("hq_floor_standoff_tiles", 0, 16);
            VerifyGamesPerCell = root.RequireInt("verify_games_per_cell", 0, 10);
            ChunkGames = root.RequireInt("chunk_games", 1, 500);
            MinDistinctFraction = root.RequireFix64("min_distinct_fraction", "0", "1");
        }

        /// <summary>Reads a required array of non-empty strings with a length in [min, max].</summary>
        /// <param name="root">The file's root object.</param>
        /// <param name="key">Member name of the array.</param>
        /// <param name="min">Fewest entries. Unit: entries. Range: 0 upward.</param>
        /// <param name="max">Most entries. Unit: entries. Range: min upward.</param>
        private static IReadOnlyList<string> ReadIds(JsonValue root, string key, int min, int max)
        {
            // The array node's items, in file order.
            IReadOnlyList<JsonValue> items = root.RequireArray(key);
            if (items.Count < min || items.Count > max)
            {
                throw root.Require(key).Fail("must have " + min + " to " + max + " entries (found " + items.Count + ")");
            }
            // The strings read from the items, in file order.
            List<string> ids = new List<string>();
            foreach (JsonValue item in items)
            {
                // The entry's text.
                string id = item.AsString();
                if (id.Length == 0)
                {
                    throw item.Fail("must not be empty");
                }
                ids.Add(id);
            }
            return ids.AsReadOnly();
        }
    }
}

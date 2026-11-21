// rts/tools/BalanceHarness/BalanceSettings.cs
// Reads rts/data/scripts/ai_eval/balance_report.json (spec: rts/docs/specs/balance_measurement.md M.5.4 and M.6): the profile that plays, the seed, the games and cells of the standing balance report and the bars of its
// four trust checks (kept as parts per 10 000 so every verdict is integer arithmetic). The file lives under scripts/ so DataLoader skips it (outside the content digest). Unknown keys are errors and every error names
// the file and the field. Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The parsed balance_report.json.</summary>
    public sealed class BalanceSettings
    {
        /// <summary>Source name used in every error message. Range: fixed path relative to rts/data.</summary>
        public const string SourceName = "scripts/ai_eval/balance_report.json";

        /// <summary>Path of the file relative to the data folder. Range: fixed.</summary>
        public const string RelativePath = "scripts/ai_eval/balance_report.json";

        /// <summary>Profile that plays both slots. Range: a file name under rts/data/ai without .json.</summary>
        public string Profile { get; private set; } = string.Empty;

        /// <summary>Base seed of the seed derivation. Unit: seed. Range: 0 to 2147483647.</summary>
        public ulong BaseSeed { get; private set; }

        /// <summary>Games per (map, matchup) cell. Unit: games. Range: 2 to 10000, a multiple of 2.</summary>
        public int GamesPerCell { get; private set; }

        /// <summary>Maps to play; empty means every 2-spawn map. Unit: map IDs. Range: 0 to 32 entries.</summary>
        public IReadOnlyList<string> Maps { get; private set; } = Array.Empty<string>();

        /// <summary>Matchups to play. Unit: matchup IDs. Range: 1 to 16 entries.</summary>
        public IReadOnlyList<string> Matchups { get; private set; } = Array.Empty<string>();

        /// <summary>Game length limit. Unit: seconds. Range: 60 to 7200.</summary>
        public int MaxGameSeconds { get; private set; }

        /// <summary>Check balance_use: least share of slot-games that must use a unit or mechanic. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int MinUseSharePt { get; private set; }

        /// <summary>Check balance_predictor: least share of decided fights the predictor must get right. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int PredictorMinAccuracyPt { get; private set; }

        /// <summary>Check balance_unspent: largest median unspent common. Unit: resource units. Range: 0 to 100000.</summary>
        public int MaxMedianUnspentCommon { get; private set; }

        /// <summary>Check balance_unspent: largest median unspent rare. Unit: resource units. Range: 0 to 100000.</summary>
        public int MaxMedianUnspentRare { get; private set; }

        /// <summary>Check balance_openings: lowest win fraction. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int OpeningMirrorMinPt { get; private set; }

        /// <summary>Check balance_openings: highest win fraction. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int OpeningMirrorMaxPt { get; private set; }

        /// <summary>Check balance_openings: distinct mirror games an opening needs before it is judged. Unit: distinct games. Range: 1 to 1000.</summary>
        public int OpeningMinGames { get; private set; }

        /// <summary>Reads and parses the file from <paramref name="dataDirectory"/>; throws <see cref="HarnessException"/> when it is missing.</summary>
        /// <param name="dataDirectory">The data folder (rts/data).</param>
        public static BalanceSettings Load(string dataDirectory)
        {
            // Full path of the settings file.
            string path = Path.Combine(dataDirectory, RelativePath);
            if (!File.Exists(path))
            {
                throw new HarnessException("settings file not found: " + path);
            }
            return Parse(File.ReadAllText(path));
        }

        /// <summary>Parses the text of balance_report.json.</summary>
        /// <param name="text">File text (comments allowed).</param>
        public static BalanceSettings Parse(string text)
        {
            JsonValue root = JsonReader.Parse(text, SourceName);
            root.RejectUnknownKeys("$schema_version", "$doc", "profile", "base_seed", "games_per_cell", "maps", "matchups", "max_game_seconds", "min_use_share", "predictor_min_accuracy",
                "max_median_unspent_common", "max_median_unspent_rare", "opening_mirror_min", "opening_mirror_max", "opening_min_games");
            BalanceSettings s = new BalanceSettings();
            s.Profile = root.RequireNonEmptyString("profile");
            s.BaseSeed = (ulong)root.RequireInt("base_seed", 0, int.MaxValue);
            JsonValue games = root.Require("games_per_cell");
            s.GamesPerCell = games.AsInt(2, 10000);
            if (s.GamesPerCell % 2 != 0)
            {
                throw games.Fail("must be a multiple of 2 (found " + s.GamesPerCell.ToString(CultureInfo.InvariantCulture) + ")");
            }
            s.Maps = ReadStrings(root.Require("maps"), 0, 32);
            s.Matchups = ReadStrings(root.Require("matchups"), 1, 16);
            s.MaxGameSeconds = root.RequireInt("max_game_seconds", 60, 7200);
            s.MinUseSharePt = PredictSettings.ReadPt(root, "min_use_share");
            s.PredictorMinAccuracyPt = PredictSettings.ReadPt(root, "predictor_min_accuracy");
            s.MaxMedianUnspentCommon = root.RequireInt("max_median_unspent_common", 0, 100000);
            s.MaxMedianUnspentRare = root.RequireInt("max_median_unspent_rare", 0, 100000);
            s.OpeningMirrorMinPt = PredictSettings.ReadPt(root, "opening_mirror_min");
            s.OpeningMirrorMaxPt = PredictSettings.ReadPt(root, "opening_mirror_max");
            s.OpeningMinGames = root.RequireInt("opening_min_games", 1, 1000);
            return s;
        }

        /// <summary>Reads an array of non-empty strings with a size range.</summary>
        private static List<string> ReadStrings(JsonValue node, int min, int max)
        {
            if (node.Items.Count < min || node.Items.Count > max)
            {
                throw node.Fail("must hold " + min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture) + " entries (found "
                    + node.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            List<string> list = new List<string>();
            foreach (JsonValue item in node.Items)
            {
                string value = item.AsString();
                if (value.Length == 0)
                {
                    throw item.Fail("must not be empty");
                }
                list.Add(value);
            }
            return list;
        }
    }
}

// rts/tools/BalanceHarness/AiEvalSettings.cs
// Reads rts/data/scripts/ai_eval/ai_eval.json (spec: rts/docs/specs/skirmish_ai_v3.md A.2 and A.4): the settings of the ladder mode (--ladder) and the list of
// scripted-opponent response tests (--response). The file lives under scripts/ so DataLoader skips it (it stays out of the content digest) and in a subfolder
// so CI does not run it as a command script. Parsed with JsonValue and its Require helpers; unknown keys are errors; every error names the file and the field.
// Harness-only: the simulation and the AI never read it, and the numbers here are tool-side statistics, so double is allowed (as in HarnessStats). Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>One scripted-opponent scenario: a script file, the check kind and the check's numbers.</summary>
    public sealed class ResponseTest
    {
        /// <summary>Script file name inside rts/data/scripts/ai_eval, for example "response_roc_rush_skai.json".</summary>
        public string Script { get; }

        /// <summary>Check kind: "air_answer", "armoured_answer", "trade", "raid" or "turtle".</summary>
        public string Check { get; }

        /// <summary>Numeric parameters of the check by key, as the spec's A.4 table names them (seconds, supply, tiles, fractions; ranges in <see cref="AiEvalSettings.CheckSchemas"/>).</summary>
        public IReadOnlyDictionary<string, double> Numbers { get; }

        /// <summary>Wave start times of a "raid" check (key wave_seconds). Unit: seconds. Range: 1 to 8 entries of 1 to 7200; empty for other checks.</summary>
        public IReadOnlyList<int> Waves { get; }

        /// <summary>Bundles the values.</summary>
        public ResponseTest(string script, string check, IReadOnlyDictionary<string, double> numbers, IReadOnlyList<int> waves)
        {
            Script = script;
            Check = check;
            Numbers = numbers;
            Waves = waves;
        }

        /// <summary>The numeric parameter <paramref name="key"/>; throws <see cref="HarnessException"/> when the check has no such key.</summary>
        /// <param name="key">Parameter name from the A.4 table.</param>
        public double Number(string key)
        {
            if (!Numbers.TryGetValue(key, out double value))
            {
                throw new HarnessException("response test " + Script + " (" + Check + ") has no parameter '" + key + "'");
            }
            return value;
        }

        /// <summary>The longest *_seconds parameter, and for a raid the end of the last wave window: the scenario's length (the script's ticks are this times 20). Unit: seconds. Range: 1 to 7200.</summary>
        public int LongestSeconds()
        {
            // Largest value of any key ending in _seconds, and of the last wave time plus the window.
            double longest = 0;
            foreach (int wave in Waves)
            {
                longest = Math.Max(longest, wave + (Numbers.TryGetValue("window_seconds", out double window) ? window : 0));
            }
            foreach (KeyValuePair<string, double> pair in Numbers)
            {
                if (pair.Key.EndsWith("_seconds", StringComparison.Ordinal))
                {
                    longest = Math.Max(longest, pair.Value);
                }
            }
            return (int)longest;
        }
    }

    /// <summary>The parsed ai_eval.json.</summary>
    public sealed class AiEvalSettings
    {
        /// <summary>Source name used in every error message. Range: fixed path relative to rts/data.</summary>
        public const string SourceName = "scripts/ai_eval/ai_eval.json";

        /// <summary>Path of the file relative to the data folder. Range: fixed.</summary>
        public const string RelativePath = "scripts/ai_eval/ai_eval.json";

        /// <summary>Allowed numeric keys of each check kind as (key, min, max) with the bounds as decimal text; "raid" also has the array key wave_seconds. Unit: per key (seconds, units, tiles, fraction, ratio).</summary>
        public static readonly IReadOnlyDictionary<string, string[][]> CheckSchemas = new SortedDictionary<string, string[][]>(StringComparer.Ordinal)
        {
            ["air_answer"] = new[] { new[] { "known_within_seconds", "1", "7200" }, new[] { "by_seconds", "1", "7200" }, new[] { "min_aa_units", "1", "100" }, new[] { "survive_seconds", "1", "7200" } },
            ["armoured_answer"] = new[] { new[] { "delay_seconds", "0", "7200" }, new[] { "until_seconds", "1", "7200" }, new[] { "min_share_gain", "0", "1" }, new[] { "survive_seconds", "1", "7200" } },
            ["trade"] = new[] { new[] { "until_seconds", "1", "7200" }, new[] { "min_trade_ratio", "0", "100" } },
            ["raid"] = new[] { new[] { "window_seconds", "1", "7200" }, new[] { "max_workers_lost", "0", "100" }, new[] { "defence_by_seconds", "1", "7200" }, new[] { "defence_radius_tiles", "1", "64" } },
            ["turtle"] = new[] { new[] { "until_seconds", "1", "7200" }, new[] { "max_supply_lost_before_first_kill", "0", "1000" } },
        };

        /// <summary>Base seed of the ladder's seed derivation. Unit: seed. Range: 0 to 2147483647.</summary>
        public ulong LadderBaseSeed { get; private set; }

        /// <summary>Games per (map, matchup) cell. Unit: games. Range: 4 to 1000, a multiple of 4.</summary>
        public int LadderGamesPerCell { get; private set; }

        /// <summary>Map IDs to play; empty means every map with exactly 2 spawns. Unit: map IDs. Range: 0 to 32 entries.</summary>
        public IReadOnlyList<string> LadderMaps { get; private set; } = Array.Empty<string>();

        /// <summary>Matchup IDs to play. Unit: matchup IDs. Range: 1 to 16 entries.</summary>
        public IReadOnlyList<string> LadderMatchups { get; private set; } = Array.Empty<string>();

        /// <summary>Game length limit. Unit: seconds. Range: 60 to 7200.</summary>
        public int LadderMaxGameSeconds { get; private set; }

        /// <summary>Decided games the ladder needs to pass. Unit: games. Range: 1 to 100000.</summary>
        public int LadderMinDecided { get; private set; }

        /// <summary>Candidate share of decided games needed to pass. Unit: fraction. Range: 0 to 1.</summary>
        public double LadderCandidateWinFractionMin { get; private set; }

        /// <summary>Candidate share needed with --non-regression. Unit: fraction. Range: 0 to 1.</summary>
        public double LadderNonRegressionFractionMin { get; private set; }

        /// <summary>Games re-run in-process for the determinism spot check. Unit: games. Range: 0 to 16.</summary>
        public int LadderVerifyGames { get; private set; }

        /// <summary>Smallest share of distinct games among a cell's decided games before the ladder warns (skirmish_ai_v4.md S.2, P-005 bar item 3). Unit: fraction. Range: 0 to 1.</summary>
        public double LadderMinDistinctFraction { get; private set; }

        /// <summary>The response scenarios in file order. Range: 1 to 64 entries.</summary>
        public IReadOnlyList<ResponseTest> ResponseTests { get; private set; } = Array.Empty<ResponseTest>();

        /// <summary>Reads and parses the file from <paramref name="dataDirectory"/>; throws <see cref="HarnessException"/> when it is missing and <see cref="DataValidationException"/> when it is invalid.</summary>
        /// <param name="dataDirectory">The data folder (rts/data).</param>
        public static AiEvalSettings Load(string dataDirectory)
        {
            // Full path of the settings file.
            string path = Path.Combine(dataDirectory, RelativePath);
            if (!File.Exists(path))
            {
                throw new HarnessException("settings file not found: " + path);
            }
            return Parse(File.ReadAllText(path));
        }

        /// <summary>Parses the text of ai_eval.json.</summary>
        /// <param name="text">File text (comments allowed).</param>
        public static AiEvalSettings Parse(string text)
        {
            JsonValue root = JsonReader.Parse(text, SourceName);
            root.RejectUnknownKeys("ladder_base_seed", "ladder_games_per_cell", "ladder_maps", "ladder_matchups", "ladder_max_game_seconds", "ladder_min_decided",
                "ladder_candidate_win_fraction_min", "ladder_non_regression_fraction_min", "ladder_verify_games", "ladder_min_distinct_fraction", "response_tests");
            AiEvalSettings settings = new AiEvalSettings();
            settings.LadderBaseSeed = (ulong)root.RequireInt("ladder_base_seed", 0, int.MaxValue);
            // Games per cell, checked for the multiple-of-4 rule (the seat rule needs whole blocks of four games).
            JsonValue games = root.Require("ladder_games_per_cell");
            settings.LadderGamesPerCell = games.AsInt(4, 1000);
            if (settings.LadderGamesPerCell % 4 != 0)
            {
                throw games.Fail("must be a multiple of 4 (found " + settings.LadderGamesPerCell.ToString(CultureInfo.InvariantCulture) + ")");
            }
            settings.LadderMaps = ReadStrings(root.Require("ladder_maps"), 0, 32);
            settings.LadderMatchups = ReadStrings(root.Require("ladder_matchups"), 1, 16);
            settings.LadderMaxGameSeconds = root.RequireInt("ladder_max_game_seconds", 60, 7200);
            settings.LadderMinDecided = root.RequireInt("ladder_min_decided", 1, 100000);
            settings.LadderCandidateWinFractionMin = HarnessStats.ToDouble(root.RequireFix64("ladder_candidate_win_fraction_min", "0", "1"));
            settings.LadderNonRegressionFractionMin = HarnessStats.ToDouble(root.RequireFix64("ladder_non_regression_fraction_min", "0", "1"));
            settings.LadderVerifyGames = root.RequireInt("ladder_verify_games", 0, 16);
            settings.LadderMinDistinctFraction = HarnessStats.ToDouble(root.RequireFix64("ladder_min_distinct_fraction", "0", "1"));
            // Response tests in file order.
            List<ResponseTest> tests = new List<ResponseTest>();
            IReadOnlyList<JsonValue> items = root.RequireArray("response_tests");
            if (items.Count < 1 || items.Count > 64)
            {
                throw root.Require("response_tests").Fail("must hold 1 to 64 entries (found " + items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            foreach (JsonValue item in items)
            {
                tests.Add(ReadResponseTest(item));
            }
            settings.ResponseTests = tests;
            return settings;
        }

        /// <summary>Reads one response_tests entry.</summary>
        private static ResponseTest ReadResponseTest(JsonValue item)
        {
            // Check kind; must be one of the schemas.
            JsonValue checkNode = item.Require("check");
            string check = checkNode.AsString();
            if (!CheckSchemas.TryGetValue(check, out string[][]? schema))
            {
                throw checkNode.Fail("is an unknown check kind '" + check + "' (known: " + string.Join(", ", CheckSchemas.Keys) + ")");
            }
            // Allowed keys: script, check, the schema's keys, and wave_seconds for raid.
            List<string> allowed = new List<string> { "script", "check" };
            foreach (string[] key in schema)
            {
                allowed.Add(key[0]);
            }
            if (check == "raid")
            {
                allowed.Add("wave_seconds");
            }
            item.RejectUnknownKeys(allowed.ToArray());
            string script = item.RequireNonEmptyString("script");
            SortedDictionary<string, double> numbers = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (string[] key in schema)
            {
                numbers[key[0]] = HarnessStats.ToDouble(item.RequireFix64(key[0], key[1], key[2]));
            }
            List<int> waves = new List<int>();
            if (check == "raid")
            {
                JsonValue waveNode = item.Require("wave_seconds");
                if (waveNode.Items.Count < 1 || waveNode.Items.Count > 8)
                {
                    throw waveNode.Fail("must hold 1 to 8 entries");
                }
                foreach (JsonValue wave in waveNode.Items)
                {
                    waves.Add(wave.AsInt(1, 7200));
                }
            }
            return new ResponseTest(script, check, numbers, waves);
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

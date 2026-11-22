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

        /// <summary>skirmish_ai_v4_rl.md H.3: an opening drawn in more than this share of its faction's draws gets a warning. Unit: fraction. Range: 0 to 1.</summary>
        public double OpeningMaxUseFraction { get; private set; }

        /// <summary>H.3: an opening whose mirror win fraction against other openings is above this gets a warning (needs OpeningMinGames games). Unit: fraction. Range: 0 to 1.</summary>
        public double OpeningMaxMirrorWinFraction { get; private set; }

        /// <summary>H.3: an opening whose mirror win fraction against other openings is below this gets a warning (needs OpeningMinGames games). Unit: fraction. Range: 0 to 1.</summary>
        public double OpeningMinMirrorWinFraction { get; private set; }

        /// <summary>H.3: decided mirror games against other openings an opening needs before its win fraction is judged. Unit: games. Range: 1 to 1000.</summary>
        public int OpeningMinGames { get; private set; }

        /// <summary>H.3: with --non-regression, the Wilson upper bound over distinct mirror games the candidate must reach, or a warning is printed. Unit: fraction. Range: 0 to 1.</summary>
        public double LadderMirrorUpperMin { get; private set; }

        /// <summary>Games re-run in-process for the determinism spot check. Unit: games. Range: 0 to 16.</summary>
        public int LadderVerifyGames { get; private set; }

        /// <summary>Two-sided level of the sign test and of the interval (balance_measurement.md M.6). Unit: parts per 10 000 (0.05 is 500). Range: 1 to 5000.</summary>
        public int EffectAlphaPt { get; private set; }

        /// <summary>z-score of <see cref="EffectAlphaPt"/>, used only in the MDE integer Z. Unit: z-score. Range: 0 to 6.</summary>
        public Rts.Sim.Core.Fix64 EffectZAlpha { get; private set; }

        /// <summary>z-score of the power of the MDE (0.8416 is 80 percent). Unit: z-score. Range: 0 to 6.</summary>
        public Rts.Sim.Core.Fix64 EffectZPower { get; private set; }

        /// <summary>Complete pairs a paired ladder needs, as a share of all its pairs, or its verdict is INCOMPLETE. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int PairedMinCompletePt { get; private set; }

        /// <summary>Seconds per income bucket of the game summary (M.4.2). Unit: seconds. Range: 5 to 600.</summary>
        public int SummaryBucketSeconds { get; private set; }

        /// <summary>Game times of the summary's bank snapshots (M.4.2). Unit: seconds. Range: 1 to 8 entries of 1 to 7200, ascending.</summary>
        public IReadOnlyList<int> SummaryBankTimesSeconds { get; private set; } = Array.Empty<int>();

        /// <summary>Game time up to which the production and defence metrics count (spec ai_production_defence.md section 7). Unit: seconds. Range: 60 to 2400.</summary>
        public int SummaryWindowSeconds { get; private set; }

        /// <summary>Game times of the metric samples (producers, capacity, income_60, army_supply). Unit: seconds. Range: 1 to 8 entries of 1 to 7200, ascending.</summary>
        public IReadOnlyList<int> SummarySampleSeconds { get; private set; } = Array.Empty<int>();

        /// <summary>A worker killed this soon after its training counts as fed to a raid. Unit: seconds. Range: 1 to 600.</summary>
        public int FedWindowSeconds { get; private set; }

        /// <summary>Radius around an own drop-off for raid hits and for the defenders that answer them. Unit: tiles. Range: 1 to 30.</summary>
        public int RaidRadiusTiles { get; private set; }

        /// <summary>First game second counted in the bank mean. Unit: seconds. Range: 0 to 600.</summary>
        public int BankMeanFromSeconds { get; private set; }

        /// <summary>A summary observer with this file's bucket, bank times and metric settings: the one every harness mode that records summaries must use, so records and spot-check re-runs agree.</summary>
        public SummaryObserver NewObserver()
        {
            return new SummaryObserver(SummaryBucketSeconds, SummaryBankTimesSeconds, this);
        }

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
                "ladder_candidate_win_fraction_min", "ladder_non_regression_fraction_min", "ladder_verify_games", "opening_max_use_fraction", "opening_max_mirror_win_fraction",
                "opening_min_mirror_win_fraction", "opening_min_games", "ladder_mirror_upper_min", "response_tests",
                "effect_alpha", "effect_z_alpha", "effect_z_power", "paired_min_complete_fraction", "summary_bucket_seconds", "summary_bank_times_seconds",
                "summary_window_seconds", "summary_sample_seconds", "fed_window_seconds", "raid_radius_tiles", "bank_mean_from_seconds");
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
            settings.OpeningMaxUseFraction = HarnessStats.ToDouble(root.RequireFix64("opening_max_use_fraction", "0", "1"));
            settings.OpeningMaxMirrorWinFraction = HarnessStats.ToDouble(root.RequireFix64("opening_max_mirror_win_fraction", "0", "1"));
            settings.OpeningMinMirrorWinFraction = HarnessStats.ToDouble(root.RequireFix64("opening_min_mirror_win_fraction", "0", "1"));
            settings.OpeningMinGames = root.RequireInt("opening_min_games", 1, 1000);
            settings.LadderMirrorUpperMin = HarnessStats.ToDouble(root.RequireFix64("ladder_mirror_upper_min", "0", "1"));
            settings.EffectAlphaPt = PredictSettings.ReadPt(root, "effect_alpha", "0.0001", "0.5");
            settings.EffectZAlpha = root.RequireFix64("effect_z_alpha", "0", "6");
            settings.EffectZPower = root.RequireFix64("effect_z_power", "0", "6");
            settings.PairedMinCompletePt = PredictSettings.ReadPt(root, "paired_min_complete_fraction");
            settings.SummaryBucketSeconds = root.RequireInt("summary_bucket_seconds", 5, 600);
            // The bank times, checked for size and ascending order.
            JsonValue bankNode = root.Require("summary_bank_times_seconds");
            if (bankNode.Items.Count < 1 || bankNode.Items.Count > 8)
            {
                throw bankNode.Fail("must hold 1 to 8 entries (found " + bankNode.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            List<int> bankTimes = new List<int>();
            foreach (JsonValue time in bankNode.Items)
            {
                int seconds = time.AsInt(1, 7200);
                if (bankTimes.Count > 0 && seconds <= bankTimes[bankTimes.Count - 1])
                {
                    throw time.Fail("must be above the entry before it (found " + seconds.ToString(CultureInfo.InvariantCulture) + ")");
                }
                bankTimes.Add(seconds);
            }
            settings.SummaryBankTimesSeconds = bankTimes;
            settings.SummaryWindowSeconds = root.RequireInt("summary_window_seconds", 60, 2400);
            JsonValue sampleNode = root.Require("summary_sample_seconds");
            if (sampleNode.Items.Count < 1 || sampleNode.Items.Count > 8)
            {
                throw sampleNode.Fail("must hold 1 to 8 entries (found " + sampleNode.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            List<int> sampleTimes = new List<int>();
            foreach (JsonValue time in sampleNode.Items)
            {
                int seconds = time.AsInt(1, 7200);
                if (sampleTimes.Count > 0 && seconds <= sampleTimes[sampleTimes.Count - 1])
                {
                    throw time.Fail("must be above the entry before it (found " + seconds.ToString(CultureInfo.InvariantCulture) + ")");
                }
                sampleTimes.Add(seconds);
            }
            settings.SummarySampleSeconds = sampleTimes;
            settings.FedWindowSeconds = root.RequireInt("fed_window_seconds", 1, 600);
            settings.RaidRadiusTiles = root.RequireInt("raid_radius_tiles", 1, 30);
            settings.BankMeanFromSeconds = root.RequireInt("bank_mean_from_seconds", 0, 600);
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

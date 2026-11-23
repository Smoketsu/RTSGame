// rts/tools/BalanceHarness/BehaviourSettings.cs
// Reads rts/data/scripts/ai_eval/behaviour_checks.json (spec: rts/docs/specs/balance_measurement.md M.4 and M.6): games per cell, the rare weight and one entry per declared check. Each entry is handed to its
// check's ReadSettings (BehaviourChecks). An entry whose ID is not in the registry is an error naming the entry; a registered check without an entry is "not declared" and is not run. The file lives under scripts/
// so DataLoader skips it (outside the content digest). Unknown keys are errors and every error names the file and the field. Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The parsed behaviour_checks.json.</summary>
    public sealed class BehaviourSettings
    {
        /// <summary>Source name used in every error message. Range: fixed path relative to rts/data.</summary>
        public const string SourceName = "scripts/ai_eval/behaviour_checks.json";

        /// <summary>Path of the file relative to the data folder. Range: fixed.</summary>
        public const string RelativePath = "scripts/ai_eval/behaviour_checks.json";

        /// <summary>Games per (map, matchup) cell of a --behaviour run. Unit: games. Range: 4 to 1000, a multiple of 4.</summary>
        public int GamesPerCell { get; private set; }

        /// <summary>Weight of one rare unit against one common unit, parts per 10 000. Unit: parts per 10 000. Range: 0 to 200000.</summary>
        public int RareWeightPt { get; private set; }

        /// <summary>The declared check entries by check ID, ordinal order (each already validated by its check).</summary>
        public IReadOnlyDictionary<string, JsonValue> Entries { get; private set; } = new SortedDictionary<string, JsonValue>(StringComparer.Ordinal);

        /// <summary>Reads and parses the file from <paramref name="dataDirectory"/>; throws <see cref="HarnessException"/> when it is missing.</summary>
        /// <param name="dataDirectory">The data folder (rts/data).</param>
        public static BehaviourSettings Load(string dataDirectory)
        {
            // Full path of the settings file.
            string path = Path.Combine(dataDirectory, RelativePath);
            if (!File.Exists(path))
            {
                throw new HarnessException("settings file not found: " + path);
            }
            return Parse(File.ReadAllText(path));
        }

        /// <summary>Parses the text of behaviour_checks.json; each declared entry is validated by a throwaway instance of its check.</summary>
        /// <param name="text">File text (comments allowed).</param>
        public static BehaviourSettings Parse(string text)
        {
            JsonValue root = JsonReader.Parse(text, SourceName);
            root.RejectUnknownKeys("$schema_version", "$doc", "games_per_cell", "rare_weight", "checks");
            BehaviourSettings s = new BehaviourSettings();
            JsonValue games = root.Require("games_per_cell");
            s.GamesPerCell = games.AsInt(4, 1000);
            if (s.GamesPerCell % 4 != 0)
            {
                throw games.Fail("must be a multiple of 4 (found " + s.GamesPerCell.ToString(CultureInfo.InvariantCulture) + ")");
            }
            s.RareWeightPt = PredictSettings.ReadPt(root, "rare_weight", "0", "20");
            SortedDictionary<string, JsonValue> entries = new SortedDictionary<string, JsonValue>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in root.Require("checks").Members)
            {
                if (!BehaviourChecks.IsRegistered(pair.Key))
                {
                    throw pair.Value.Fail("is not a registered behaviour check ('" + pair.Key + "')");
                }
                BehaviourChecks.Create(pair.Key, new BehaviourContext(s.RareWeightPt, 1)).ReadSettings(pair.Value);
                entries.Add(pair.Key, pair.Value);
            }
            s.Entries = entries;
            return s;
        }

        /// <summary>The checks to run, in registry order: the declared ones that are enabled (and named by <paramref name="only"/> when it is not null), each configured from its entry.</summary>
        /// <param name="only">Check IDs from --checks, or null for every enabled check; an ID that is not registered throws <see cref="HarnessException"/>.</param>
        /// <param name="bucketTicks">Length of one income bucket. Unit: ticks. Range: 1 upward.</param>
        /// <param name="notDeclared">Receives the registered checks that have no entry, in registry order.</param>
        /// <param name="bankSeconds">Seconds of the summary's bank snapshots, or null when unknown (a check that names a snapshot then skips its range check). Unit: seconds.</param>
        /// <param name="ticksPerSecond">Simulation ticks per second. Unit: ticks per second. Range: 1 upward.</param>
        /// <param name="data">The loaded data set for a check that models the game (econ_gap), or null when only validating.</param>
        public List<IBehaviourCheck> Checks(IReadOnlyList<string>? only, int bucketTicks, List<string> notDeclared, IReadOnlyList<int>? bankSeconds = null, int ticksPerSecond = 1, DataSet? data = null)
        {
            if (only != null)
            {
                foreach (string id in only)
                {
                    if (!BehaviourChecks.IsRegistered(id))
                    {
                        throw new HarnessException("--checks names '" + id + "', which is not a registered behaviour check");
                    }
                }
            }
            BehaviourContext context = new BehaviourContext(RareWeightPt, bucketTicks, bankSeconds, ticksPerSecond, data);
            List<IBehaviourCheck> checks = new List<IBehaviourCheck>();
            foreach (KeyValuePair<string, Func<BehaviourContext, IBehaviourCheck>> pair in BehaviourChecks.Registry)
            {
                if (!Entries.TryGetValue(pair.Key, out JsonValue? entry))
                {
                    notDeclared.Add(pair.Key);
                    continue;
                }
                IBehaviourCheck check = pair.Value(context);
                check.ReadSettings(entry);
                if (check.Enabled && (only == null || System.Linq.Enumerable.Contains(only, pair.Key)))
                {
                    checks.Add(check);
                }
            }
            return checks;
        }
    }
}

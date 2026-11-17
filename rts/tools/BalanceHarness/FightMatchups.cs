// rts/tools/BalanceHarness/FightMatchups.cs
// Reads rts/data/scripts/fights/matchups.json (spec: rts/docs/specs/micro_benchmark.md section 3.2): per combat unit its job and the fight it wins and the fight it loses. The file
// is documentation, not simulation input; it lives under scripts/ so DataLoader skips it (outside the content digest) and only this class parses it. A combat unit with no entry is
// not an error here: the matchups_agree check reports it as missing. Errors are DataValidationExceptions naming the file and the field. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>A claimed fight: against which unit, on which case.</summary>
    public sealed class MatchupClaim
    {
        /// <summary>Combat unit ID of the opponent.</summary>
        public string Against { get; }

        /// <summary>Case ID of fights.json whose army values contain rule_army_value.</summary>
        public string Case { get; }

        /// <summary>Bundles the values.</summary>
        public MatchupClaim(string against, string fightCase)
        {
            Against = against;
            Case = fightCase;
        }
    }

    /// <summary>One unit's entry.</summary>
    public sealed class MatchupEntry
    {
        /// <summary>Unit ID the entry belongs to.</summary>
        public string Unit { get; }

        /// <summary>The designed job. Range: 1 to 120 characters.</summary>
        public string Job { get; }

        /// <summary>The fight the unit claims to win, or null for none.</summary>
        public MatchupClaim? Wins { get; }

        /// <summary>The fight the unit claims to lose, or null for none.</summary>
        public MatchupClaim? Loses { get; }

        /// <summary>Bundles the values.</summary>
        public MatchupEntry(string unit, string job, MatchupClaim? wins, MatchupClaim? loses)
        {
            Unit = unit;
            Job = job;
            Wins = wins;
            Loses = loses;
        }
    }

    /// <summary>The parsed matchups.json.</summary>
    public sealed class FightMatchups
    {
        /// <summary>Source name used in every error message. Range: fixed path relative to rts/data.</summary>
        public const string SourceName = "scripts/fights/matchups.json";

        /// <summary>Path of the file relative to the data folder. Range: fixed.</summary>
        public const string RelativePath = "scripts/fights/matchups.json";

        /// <summary>Entries by unit ID in ordinal order. Range: 0 to one per swept unit.</summary>
        public IReadOnlyDictionary<string, MatchupEntry> Entries { get; }

        /// <summary>Bundles the values.</summary>
        public FightMatchups(IReadOnlyDictionary<string, MatchupEntry> entries)
        {
            Entries = entries;
        }

        /// <summary>An empty file: no entries.</summary>
        public static FightMatchups Empty => new FightMatchups(new SortedDictionary<string, MatchupEntry>(StringComparer.Ordinal));

        /// <summary>Reads and parses the file from <paramref name="dataDirectory"/>; throws <see cref="HarnessException"/> when it is missing.</summary>
        /// <param name="dataDirectory">The data folder (rts/data).</param>
        /// <param name="settings">The parsed fights.json.</param>
        /// <param name="data">The loaded data set.</param>
        public static FightMatchups Load(string dataDirectory, FightSettings settings, DataSet data)
        {
            // Full path of the matchups file.
            string path = Path.Combine(dataDirectory, RelativePath);
            if (!File.Exists(path))
            {
                throw new HarnessException("matchups file not found: " + path);
            }
            return Parse(File.ReadAllText(path), settings, data);
        }

        /// <summary>Parses the text of matchups.json and checks it against the settings and the data set.</summary>
        /// <param name="text">File text (comments allowed).</param>
        /// <param name="settings">The parsed fights.json.</param>
        /// <param name="data">The loaded data set.</param>
        public static FightMatchups Parse(string text, FightSettings settings, DataSet data)
        {
            JsonValue root = JsonReader.Parse(text, SourceName);
            SortedDictionary<string, MatchupEntry> entries = new SortedDictionary<string, MatchupEntry>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in root.Members)
            {
                if (pair.Key == DataLoader.SchemaVersionKey || pair.Key == DataLoader.DocKey)
                {
                    continue;
                }
                if (!data.Content.Units.ContainsKey(pair.Key))
                {
                    throw pair.Value.Fail("is an entry for the unknown unit '" + pair.Key + "'");
                }
                if (Array.IndexOf(ToArray(settings.Units), pair.Key) < 0)
                {
                    throw pair.Value.Fail("is an entry for '" + pair.Key + "', which is not in the sweep of fights.json");
                }
                pair.Value.RejectUnknownKeys("job", "wins", "loses");
                JsonValue jobNode = pair.Value.Require("job");
                string job = jobNode.AsString();
                if (job.Length < 1 || job.Length > 120)
                {
                    throw jobNode.Fail("must be 1 to 120 characters (found " + job.Length.ToString(CultureInfo.InvariantCulture) + ")");
                }
                entries.Add(pair.Key, new MatchupEntry(pair.Key, job, ReadClaim(pair.Value.Require("wins"), pair.Key, settings, data), ReadClaim(pair.Value.Require("loses"), pair.Key, settings, data)));
            }
            return new FightMatchups(entries);
        }

        /// <summary>Reads a claim object or null.</summary>
        private static MatchupClaim? ReadClaim(JsonValue node, string unit, FightSettings settings, DataSet data)
        {
            if (node.Kind == JsonKind.Null)
            {
                return null;
            }
            node.RejectUnknownKeys("against", "case");
            JsonValue againstNode = node.Require("against");
            string against = againstNode.AsString();
            if (!data.Content.Units.TryGetValue(against, out Rts.Sim.Units.UnitDefinition? definition) || !FightSettings.IsCombatUnit(definition))
            {
                throw againstNode.Fail("must name a combat unit (found '" + against + "')");
            }
            if (against == unit)
            {
                throw againstNode.Fail("must be a unit other than '" + unit + "' itself");
            }
            JsonValue caseNode = node.Require("case");
            string caseId = caseNode.AsString();
            FightCase? found = null;
            foreach (FightCase fightCase in settings.Cases)
            {
                if (fightCase.Id == caseId)
                {
                    found = fightCase;
                }
            }
            if (found == null || !new List<int>(found.ArmyValues).Contains(settings.RuleArmyValue))
            {
                throw caseNode.Fail("must name a case of fights.json whose army_values contain rule_army_value " + settings.RuleArmyValue.ToString(CultureInfo.InvariantCulture)
                    + " (found '" + caseId + "')");
            }
            return new MatchupClaim(against, caseId);
        }

        /// <summary>The list as an array, for Array.IndexOf.</summary>
        private static string[] ToArray(IReadOnlyList<string> list)
        {
            string[] array = new string[list.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = list[i];
            }
            return array;
        }
    }
}

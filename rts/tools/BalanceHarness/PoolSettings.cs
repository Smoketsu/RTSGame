// rts/tools/BalanceHarness/PoolSettings.cs
// Reads rts/data/scripts/ai_eval/pool.json (spec: rts/docs/specs/balance_measurement.md M.3.2): the maps and matchups of the opponent pool, the pairs per cell of the two stages, the Bonferroni switch and the
// members (a profile ID, optionally with one forced opening per faction, and the stage the member belongs to). The file lives under scripts/ so DataLoader skips it (outside the content digest). Unknown keys are
// errors and every error names the file and the field; with a data set the profile IDs and the forced openings are validated too (AiProfile.WithForcedOpenings). Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Ai;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>One opponent of the pool. Immutable.</summary>
    public sealed class PoolMember
    {
        /// <summary>Member ID, unique in the file and written to records. Range: 1 to 48 lower-case letters, digits or underscores.</summary>
        public string Id { get; }

        /// <summary>Profile ID the member plays by. Range: a file name under rts/data/ai without .json.</summary>
        public string Profile { get; }

        /// <summary>Opening ID to force by faction ID, in ordinal faction order; empty for none. Range: 0 or more entries, each an opening of that faction's plan in the profile.</summary>
        public IReadOnlyDictionary<string, string> ForceOpening { get; }

        /// <summary>Stage the member belongs to. Unit: stage number. Range: 1 or 2.</summary>
        public int Stage { get; }

        /// <summary>Bundles the values.</summary>
        public PoolMember(string id, string profile, IReadOnlyDictionary<string, string> forceOpening, int stage)
        {
            Id = id;
            Profile = profile;
            ForceOpening = forceOpening;
            Stage = stage;
        }

        /// <summary>The profile the member plays: the named profile, with the forced openings applied when there are any.</summary>
        /// <param name="data">The loaded data set.</param>
        public AiProfile Resolve(DataSet data)
        {
            AiProfile profile = AiProfile.FromDataSet(data, Profile);
            return ForceOpening.Count > 0 ? profile.WithForcedOpenings(ForceOpening) : profile;
        }
    }

    /// <summary>The parsed pool.json. Immutable after <see cref="Parse"/>.</summary>
    public sealed class PoolSettings
    {
        /// <summary>Source name used in every error message. Range: fixed path relative to rts/data.</summary>
        public const string SourceName = "scripts/ai_eval/pool.json";

        /// <summary>Path of the file relative to the data folder. Range: fixed.</summary>
        public const string RelativePath = "scripts/ai_eval/pool.json";

        /// <summary>Maps the pool plays; empty means the ladder's maps. Unit: map IDs. Range: 0 to 32 entries.</summary>
        public IReadOnlyList<string> PoolMaps { get; private set; } = Array.Empty<string>();

        /// <summary>Matchups the pool plays. Unit: matchup IDs. Range: 1 to 16 entries.</summary>
        public IReadOnlyList<string> PoolMatchups { get; private set; } = Array.Empty<string>();

        /// <summary>Pairs per cell in stage 1. Unit: pairs. Range: 2 to 1000, even.</summary>
        public int Stage1Pairs { get; private set; }

        /// <summary>Pairs per cell in stage 2. Unit: pairs. Range: 2 to 1000, even.</summary>
        public int Stage2Pairs { get; private set; }

        /// <summary>True when "better" must be significant at effect_alpha divided by the number of members run (Bonferroni). Unit: boolean.</summary>
        public bool BetterBonferroni { get; private set; }

        /// <summary>The members in file order. Range: 1 to 32 entries.</summary>
        public IReadOnlyList<PoolMember> Members { get; private set; } = Array.Empty<PoolMember>();

        /// <summary>Reads and parses the file from <paramref name="dataDirectory"/>; throws <see cref="HarnessException"/> when it is missing.</summary>
        /// <param name="dataDirectory">The data folder (rts/data).</param>
        /// <param name="data">The loaded data set to validate profiles and forced openings against, or null to skip that.</param>
        public static PoolSettings Load(string dataDirectory, DataSet? data)
        {
            // Full path of the settings file.
            string path = Path.Combine(dataDirectory, RelativePath);
            if (!File.Exists(path))
            {
                throw new HarnessException("settings file not found: " + path);
            }
            return Parse(File.ReadAllText(path), data);
        }

        /// <summary>The member with ID <paramref name="id"/>; throws <see cref="HarnessException"/> when there is none.</summary>
        /// <param name="id">A member ID.</param>
        public PoolMember Require(string id)
        {
            foreach (PoolMember member in Members)
            {
                if (member.Id == id)
                {
                    return member;
                }
            }
            throw new HarnessException("pool member '" + id + "' is not in " + SourceName);
        }

        /// <summary>Parses the text of pool.json.</summary>
        /// <param name="text">File text (comments allowed).</param>
        /// <param name="data">The loaded data set to validate profiles and forced openings against, or null to skip that.</param>
        public static PoolSettings Parse(string text, DataSet? data)
        {
            JsonValue root = JsonReader.Parse(text, SourceName);
            root.RejectUnknownKeys("$schema_version", "$doc", "pool_maps", "pool_matchups", "stage1_pairs_per_cell", "stage2_pairs_per_cell", "better_bonferroni", "members");
            PoolSettings s = new PoolSettings();
            s.PoolMaps = ReadStrings(root.Require("pool_maps"), 0, 32);
            s.PoolMatchups = ReadStrings(root.Require("pool_matchups"), 1, 16);
            s.Stage1Pairs = ReadPairs(root, "stage1_pairs_per_cell");
            s.Stage2Pairs = ReadPairs(root, "stage2_pairs_per_cell");
            s.BetterBonferroni = root.RequireBool("better_bonferroni");
            // Member nodes, checked for size.
            IReadOnlyList<JsonValue> items = root.RequireArray("members");
            if (items.Count < 1 || items.Count > 32)
            {
                throw root.Require("members").Fail("must hold 1 to 32 entries (found " + items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            // Members read so far, in file order, and their IDs.
            List<PoolMember> members = new List<PoolMember>();
            SortedSet<string> ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JsonValue item in items)
            {
                item.RejectUnknownKeys("id", "profile", "force_opening", "stage");
                // The member's ID, checked for shape and uniqueness.
                string id = item.RequireNonEmptyString("id");
                if (id.Length > 48 || !IsId(id))
                {
                    throw item.Require("id").Fail("must be 1 to 48 lower-case letters, digits or underscores (found '" + id + "')");
                }
                if (!ids.Add(id))
                {
                    throw item.Require("id").Fail("is used twice ('" + id + "')");
                }
                string profile = item.RequireNonEmptyString("profile");
                if (data != null && !data.AiProfiles.ContainsKey(profile))
                {
                    throw item.Require("profile").Fail("is not a loaded AI profile ('" + profile + "')");
                }
                // Forced openings by faction, in ordinal faction order.
                SortedDictionary<string, string> forced = new SortedDictionary<string, string>(StringComparer.Ordinal);
                if (item.TryGetMember("force_opening", out JsonValue? forceNode))
                {
                    foreach (KeyValuePair<string, JsonValue> pair in forceNode.Members)
                    {
                        forced[pair.Key] = pair.Value.AsString();
                    }
                }
                int stage = item.RequireInt("stage", 1, 2);
                PoolMember member = new PoolMember(id, profile, forced, stage);
                if (data != null && forced.Count > 0)
                {
                    try
                    {
                        member.Resolve(data);
                    }
                    catch (ArgumentException ex)
                    {
                        throw item.Require("force_opening").Fail(ex.Message);
                    }
                }
                members.Add(member);
            }
            s.Members = members;
            return s;
        }

        /// <summary>Reads a pairs-per-cell key: an even whole number from 2 to 1000.</summary>
        private static int ReadPairs(JsonValue root, string key)
        {
            JsonValue node = root.Require(key);
            int pairs = node.AsInt(2, 1000);
            if (pairs % 2 != 0)
            {
                throw node.Fail("must be even (found " + pairs.ToString(CultureInfo.InvariantCulture) + ")");
            }
            return pairs;
        }

        /// <summary>True for a non-empty text of lower-case letters, digits and underscores.</summary>
        private static bool IsId(string text)
        {
            foreach (char c in text)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }
            }
            return text.Length > 0;
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

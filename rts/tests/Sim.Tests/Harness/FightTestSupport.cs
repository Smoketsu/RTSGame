// rts/tests/Sim.Tests/Harness/FightTestSupport.cs
// Helpers for the equal-value fight tests: a copy of rts/data whose fights.json is cut down to a quick sweep (3 units, no compositions, one flat case), key-by-key edits of that file
// (every value is on one line, so a line replace edits it), and a plan lookup. Test code, so it may use files. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Fight-test helpers.</summary>
    public static class FightTestSupport
    {
        /// <summary>Data-root-relative path of the settings file.</summary>
        public const string SettingsPath = "scripts/fights/fights.json";

        /// <summary>Data-root-relative path of the matchups file.</summary>
        public const string MatchupsPath = "scripts/fights/matchups.json";

        /// <summary>Quick-sweep units: three units of two factions, 3 pairs and 6 fights per case value.</summary>
        public const string QuickUnits = "[\"skai.windrunner\", \"water.grasper\", \"water.ink_lobber\"]";

        /// <summary>A flat-only case list at 1500.</summary>
        public const string QuickCases = "[ { \"id\": \"flat\", \"kind\": \"flat\", \"army_values\": [1500] } ]";

        /// <summary>Threats of the quick sweep (the shipped ones name units outside it).</summary>
        public const string QuickThreats = "[ { \"id\": \"massed_armour\", \"side\": \"water.grasper\" } ]";

        /// <summary>Replaces the value of one single-line key of the JSON text; fails the test when the key is absent.</summary>
        /// <param name="text">File text.</param>
        /// <param name="key">Key name.</param>
        /// <param name="value">New value as JSON text.</param>
        public static string SetKey(string text, string key, string value)
        {
            Regex line = new Regex("^(\\s*\"" + Regex.Escape(key) + "\":\\s*)(.*?)(,?)[ \\t]*$", RegexOptions.Multiline);
            Assert.That(line.IsMatch(text), Is.True, "key " + key + " must be in the file");
            return line.Replace(text, m => m.Groups[1].Value + value + m.Groups[3].Value, 1);
        }

        /// <summary>Copies rts/data, applies key edits to fights.json (and any other edits) and returns the folder.</summary>
        /// <param name="keys">Pairs of key and JSON value text for fights.json.</param>
        public static string DataWith(params string[] keys)
        {
            string folder = HarnessTestSupport.CopyData();
            Edit(folder, keys);
            return folder;
        }

        /// <summary>Applies key edits to the fights.json of an existing data folder.</summary>
        public static void Edit(string folder, params string[] keys)
        {
            string path = Path.Combine(folder, SettingsPath);
            string text = File.ReadAllText(path);
            for (int i = 0; i < keys.Length; i += 2)
            {
                text = SetKey(text, keys[i], keys[i + 1]);
            }
            File.WriteAllText(path, text);
        }

        /// <summary>A data folder with the quick sweep; further key pairs override it.</summary>
        /// <param name="more">More pairs of key and JSON value text.</param>
        public static string Quick(params string[] more)
        {
            // The HQ floor trials stop after 30 s of game time so the sweep tests stay quick.
            string folder = HarnessTestSupport.CopyData(HarnessTestSupport.QuickBatch);
            Edit(folder, "units", QuickUnits, "compositions", "[]", "cases", QuickCases, "threats", QuickThreats);
            // The shipped matchups.json names units outside the quick sweep; the quick sweeps run with no entries.
            File.WriteAllText(Path.Combine(folder, MatchupsPath), "{ \"$schema_version\": 1 }");
            Edit(folder, more);
            return folder;
        }

        /// <summary>The planned fight with this ID of the data folder's settings.</summary>
        public static FightSpec Spec(string folder, string id, out DataSet data, out FightSettings settings)
        {
            data = HarnessTestSupport.Load(folder);
            settings = FightSettings.Load(folder, data);
            foreach (FightSpec spec in FightPlan.Build(data, settings).Fights)
            {
                if (spec.Id == id)
                {
                    return spec;
                }
            }
            Assert.Fail("no fight " + id);
            return null!;
        }

        /// <summary>Plays one fight in-process and returns the run.</summary>
        public static FightRun Play(string folder, string id)
        {
            FightSpec spec = Spec(folder, id, out DataSet data, out FightSettings settings);
            return FightRunner.Run(data, settings, spec, settings.BaseSeed);
        }

        /// <summary>Runs the harness for one fight (--only) and returns the exit code, stdout and stderr.</summary>
        public static int Only(string folder, string id, out string output, out string error)
        {
            return HarnessTestSupport.Run(new[] { "--data", folder, "--fights", "--only", id }, out output, out error);
        }

        /// <summary>The first fight ID of the quick sweep.</summary>
        public const string QuickFirst = "flat/1500/skai.windrunner/water.grasper";

        /// <summary>All quick-sweep fight IDs in plan order.</summary>
        public static readonly string[] QuickIds =
        {
            "flat/1500/skai.windrunner/water.grasper", "flat/1500/water.grasper/skai.windrunner", "flat/1500/skai.windrunner/water.ink_lobber",
            "flat/1500/water.ink_lobber/skai.windrunner", "flat/1500/water.grasper/water.ink_lobber", "flat/1500/water.ink_lobber/water.grasper",
        };

        /// <summary>Lines of a list, for messages.</summary>
        public static string Join(IEnumerable<string> lines)
        {
            return string.Join("\n", lines);
        }
    }
}

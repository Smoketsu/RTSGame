// rts/tools/BalanceHarness/FightChecks.cs
// The automated design-rule checks of the fight mode (spec: rts/docs/specs/micro_benchmark.md section 6). Each check is a pure function of the fight records, the settings, the
// matchups file and the data set; none changes the exit code unless --strict is given. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The verdict of one check.</summary>
    public sealed class CheckResult
    {
        /// <summary>Check name, for example "rule1_no_immunity".</summary>
        public string Name { get; }

        /// <summary>The rule it tests, for the report table.</summary>
        public string Rule { get; }

        /// <summary>The threshold in words, for the report table.</summary>
        public string Threshold { get; }

        /// <summary>True when the check passed.</summary>
        public bool Pass { get; }

        /// <summary>One line per failing item (or one note line when it passed).</summary>
        public IReadOnlyList<string> Detail { get; }

        /// <summary>Bundles the values.</summary>
        public CheckResult(string name, string rule, string threshold, bool pass, IReadOnlyList<string> detail)
        {
            Name = name;
            Rule = rule;
            Threshold = threshold;
            Pass = pass;
            Detail = detail;
        }
    }

    /// <summary>The checks.</summary>
    public static class FightChecks
    {
        /// <summary>Report rows of the matchups table, one per claim (filled in package F3).</summary>
        public static List<string> MatchupRows(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups)
        {
            return new List<string>();
        }

        /// <summary>Report rows of the suggested matchups entries, one per combat unit (filled in package F3).</summary>
        public static List<string> SuggestedRows(IReadOnlyList<FightRecord> records, FightSettings settings)
        {
            return new List<string>();
        }

        /// <summary>Runs every check in the order of spec section 6.</summary>
        /// <param name="records">Every fight record in plan order.</param>
        /// <param name="floors">Every HQ floor record.</param>
        /// <param name="settings">The parsed fights.json.</param>
        /// <param name="matchups">The parsed matchups.json.</param>
        /// <param name="data">The loaded data set.</param>
        public static List<CheckResult> RunAll(IReadOnlyList<FightRecord> records, IReadOnlyList<HqFloorRecord> floors, FightSettings settings, FightMatchups matchups,
            Rts.Sim.Data.DataSet data)
        {
            return new List<CheckResult>();
        }
    }
}

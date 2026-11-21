// rts/tools/BalanceHarness/FightReport.cs
// Writes fights.md of the fight mode (spec: rts/docs/specs/micro_benchmark.md section 5): the settings bullets and summary line, the design-rule check table, one value-exchange
// matrix per (case, army value), the HQ floor table, the matchups tables, the stalemates and seat splits, and every fight. Output depends only on the data and the records, never on
// --jobs, scheduling or the clock. Tool-side only (doubles for formatting). Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Counts shown in the summary line.</summary>
    public sealed class FightSummary
    {
        /// <summary>Fights played. Unit: fights.</summary>
        public int Fights { get; }

        /// <summary>Fights won by one seat. Unit: fights.</summary>
        public int Decided { get; }

        /// <summary>Fights where both armies died together. Unit: fights.</summary>
        public int Draws { get; }

        /// <summary>Stalemates. Unit: fights.</summary>
        public int Stalemates { get; }

        /// <summary>Stalemates whose reason is dome_absorbed (skirmish_ai_v4.md S.4); a subset of <see cref="Stalemates"/>. Unit: fights.</summary>
        public int DomeAbsorbed { get; }

        /// <summary>Fights at the time cap. Unit: fights.</summary>
        public int Unresolved { get; }

        /// <summary>Fights flagged mismatch. Unit: fights.</summary>
        public int Mismatched { get; }

        /// <summary>Checks that passed. Unit: checks.</summary>
        public int ChecksPassed { get; }

        /// <summary>Checks run. Unit: checks.</summary>
        public int ChecksTotal { get; }

        /// <summary>Counts the records and the checks.</summary>
        public FightSummary(IReadOnlyList<FightRecord> records, IReadOnlyList<CheckResult> checks)
        {
            Fights = records.Count;
            foreach (FightRecord record in records)
            {
                Decided += record.WinnerSeat >= 0 ? 1 : 0;
                Draws += record.Result == FightRecord.Draw ? 1 : 0;
                Stalemates += record.Result == FightRecord.Stalemate ? 1 : 0;
                DomeAbsorbed += record.Result == FightRecord.Stalemate && record.Reason == "dome_absorbed" ? 1 : 0;
                Unresolved += record.Result == FightRecord.Unresolved ? 1 : 0;
                Mismatched += record.Mismatch ? 1 : 0;
            }
            ChecksTotal = checks.Count;
            foreach (CheckResult check in checks)
            {
                ChecksPassed += check.Pass ? 1 : 0;
            }
        }

        /// <summary>"FIGHTS fights=&lt;n&gt; decided=&lt;n&gt; draws=&lt;n&gt; stalemates=&lt;n&gt; unresolved=&lt;n&gt; mismatched=&lt;n&gt; checks_passed=&lt;n&gt; of=&lt;m&gt; dome_absorbed=&lt;n&gt;".</summary>
        public string Line()
        {
            return "FIGHTS fights=" + Fights.ToString(CultureInfo.InvariantCulture) + " decided=" + Decided.ToString(CultureInfo.InvariantCulture)
                + " draws=" + Draws.ToString(CultureInfo.InvariantCulture) + " stalemates=" + Stalemates.ToString(CultureInfo.InvariantCulture)
                + " unresolved=" + Unresolved.ToString(CultureInfo.InvariantCulture) + " mismatched=" + Mismatched.ToString(CultureInfo.InvariantCulture)
                + " checks_passed=" + ChecksPassed.ToString(CultureInfo.InvariantCulture) + " of=" + ChecksTotal.ToString(CultureInfo.InvariantCulture)
                + " dome_absorbed=" + DomeAbsorbed.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Builds fights.md.</summary>
    public static class FightReport
    {
        /// <summary>Builds the report text.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="settings">The parsed fights.json.</param>
        /// <param name="plan">The plan (sides in order).</param>
        /// <param name="matchups">The parsed matchups.json.</param>
        /// <param name="records">Every fight record in plan order.</param>
        /// <param name="floors">Every HQ floor record, sorted.</param>
        /// <param name="checks">The check results.</param>
        /// <param name="summary">The summary counts.</param>
        /// <param name="baseSeed">Base seed in use. Range: any ulong.</param>
        /// <param name="verify">Fights re-run for the spot check. Unit: fights.</param>
        /// <param name="mismatches">Spot-check mismatches. Unit: fights.</param>
        public static string Build(DataSet data, FightSettings settings, FightPlan plan, FightMatchups matchups, IReadOnlyList<FightRecord> records, IReadOnlyList<HqFloorRecord> floors,
            IReadOnlyList<CheckResult> checks, FightSummary summary, ulong baseSeed, int verify, int mismatches)
        {
            SortedDictionary<string, FightRecord> byId = new SortedDictionary<string, FightRecord>(StringComparer.Ordinal);
            foreach (FightRecord record in records)
            {
                byId[record.Fight] = record;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<!-- fights.md: written by the balance harness --fights (rts/tools/BalanceHarness/FightCoordinator.cs); not a source file. -->\n");
            sb.Append("# Equal-value fights\n\n");
            sb.Append("- Data digest: tuning ").Append(data.Tuning.SourceDigest.ToString("x16", CultureInfo.InvariantCulture)).Append(", content ")
                .Append(data.Content.ComputeDigest().ToString("x16", CultureInfo.InvariantCulture)).Append(".\n");
            sb.Append("- Base seed ").Append(baseSeed.ToString(CultureInfo.InvariantCulture)).Append("; fight seed = SeedDerivation.GameSeed(base seed, \"fights\", fight ID, 0).\n");
            sb.Append("- Value: cost_common + cost_rare per unit; an army is the recipe repeated k = max(1, (2V + rv) / (2 rv)) times.\n");
            sb.Append("- Control: plain attack-move at the enemy front rank, idle units re-target every ")
                .Append(HarnessStats.ToDouble(settings.ReissueSeconds).ToString("0.##", CultureInfo.InvariantCulture)).Append(" s; no strategic AI, no HQ.\n");
            sb.Append("- Fights: ").Append(records.Count.ToString(CultureInfo.InvariantCulture)).Append(" (").Append(plan.Sides.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" sides); determinism spot check: ").Append(verify.ToString(CultureInfo.InvariantCulture)).Append(" fights re-run in-process, ")
                .Append(mismatches.ToString(CultureInfo.InvariantCulture)).Append(" mismatches.\n\n");
            sb.Append("**").Append(summary.Line()).Append("**\n\n");

            sb.Append("## Design-rule checks\n\n| Check | Rule | Threshold | Result | Detail |\n| --- | --- | --- | --- | --- |\n");
            foreach (CheckResult check in checks)
            {
                sb.Append("| ").Append(check.Name).Append(" | ").Append(check.Rule).Append(" | ").Append(check.Threshold).Append(" | ").Append(check.Pass ? "PASS" : "FAIL").Append(" | ")
                    .Append(string.Join("<br>", check.Detail).Replace("|", "\\|")).Append(" |\n");
            }
            sb.Append('\n');

            foreach (FightCase fightCase in settings.Cases)
            {
                // A case with no equal-value fights (every ratio differs from 1) has no matrix: its fight IDs carry a ratio suffix the matrix does not look up.
                if (!new List<int>(fightCase.RatiosPt).Contains(FightSpec.EqualRatioPt))
                {
                    continue;
                }
                foreach (int armyValue in fightCase.ArmyValues)
                {
                    AppendMatrix(sb, plan, fightCase, armyValue, byId);
                }
            }

            AppendFloors(sb, data, floors);
            AppendMatchups(sb, settings, matchups, records, data);
            AppendLists(sb, records, byId);
            return sb.ToString();
        }

        /// <summary>The value-exchange matrix of one (case, army value).</summary>
        private static void AppendMatrix(StringBuilder sb, FightPlan plan, FightCase fightCase, int armyValue, IReadOnlyDictionary<string, FightRecord> byId)
        {
            sb.Append("## Value exchange: ").Append(fightCase.Id).Append(", ").Append(armyValue.ToString(CultureInfo.InvariantCulture)).Append("\n\n| |");
            foreach (FightSide column in plan.Sides)
            {
                sb.Append(' ').Append(column.Id).Append(" |");
            }
            sb.Append("\n| --- |");
            foreach (FightSide column in plan.Sides)
            {
                sb.Append(" ---: |");
            }
            sb.Append('\n');
            foreach (FightSide row in plan.Sides)
            {
                sb.Append("| ").Append(row.Id).Append(" |");
                foreach (FightSide column in plan.Sides)
                {
                    sb.Append(' ').Append(Cell(fightCase, armyValue, row.Id, column.Id, byId)).Append(" |");
                }
                sb.Append('\n');
            }
            sb.Append("\nCell (row, column) is the row's value left minus the column's, from the fight with the row in seat 0 and the column in seat 1. S stalemate, U unresolved, - mismatch, ")
                .Append("`*` the two seats of the pair disagree on the winner, · the diagonal.");
            if (fightCase.Kind == FightKinds.HighGround)
            {
                sb.Append(" The row is the attacker; the column holds the plateau.");
            }
            else if (fightCase.Kind == FightKinds.Flooded)
            {
                sb.Append(" The row fights in the column's flood.");
            }
            sb.Append("\n\n");
        }

        /// <summary>One matrix cell.</summary>
        private static string Cell(FightCase fightCase, int armyValue, string row, string column, IReadOnlyDictionary<string, FightRecord> byId)
        {
            if (row == column)
            {
                return "·";
            }
            string prefix = fightCase.Id + "/" + armyValue.ToString(CultureInfo.InvariantCulture) + "/";
            if (!byId.TryGetValue(prefix + row + "/" + column, out FightRecord? record))
            {
                return "?";
            }
            // The mirrored fight, for the seat-split star.
            byId.TryGetValue(prefix + column + "/" + row, out FightRecord? mirror);
            string star = mirror != null && WinnerId(record) != WinnerId(mirror) ? "*" : string.Empty;
            if (record.Mismatch)
            {
                return "–" + star;
            }
            if (record.Result == FightRecord.Stalemate)
            {
                return "S" + star;
            }
            if (record.Result == FightRecord.Unresolved)
            {
                return "U" + star;
            }
            return (record.Left0 - record.Left1).ToString("+0.00;-0.00;+0.00", CultureInfo.InvariantCulture) + star;
        }

        /// <summary>The winning side's ID, or an empty string when nobody won.</summary>
        private static string WinnerId(FightRecord record)
        {
            return record.WinnerSeat < 0 ? string.Empty : record.Side(record.WinnerSeat);
        }

        /// <summary>The HQ floor table.</summary>
        private static void AppendFloors(StringBuilder sb, DataSet data, IReadOnlyList<HqFloorRecord> floors)
        {
            int tps = data.Tuning.Match.TicksPerSecond;
            int floorTicks = data.Tuning.Harness.HqFloorTicks(new MatchClock(tps));
            sb.Append("## HQ floor (Rooted >= ").Append(HarnessStats.Seconds(floorTicks, tps)).Append(" s against ")
                .Append(data.Tuning.Harness.HqFloorSupply.ToString(CultureInfo.InvariantCulture)).Append(" supply)\n\n");
            sb.Append("| Attacker | Army | Count | Defender HQ | State | Time to kill | Result |\n| --- | --- | ---: | --- | --- | ---: | --- |\n");
            foreach (HqFloorRecord floor in floors)
            {
                string kill = floor.TtkTicks >= 0 ? HarnessStats.Seconds(floor.TtkTicks, tps) + " s" : "> " + data.Tuning.HarnessBatch.HqFloorMaxTestSeconds.ToString(CultureInfo.InvariantCulture) + " s";
                string result = floor.HqState != "rooted" ? "reported" : floor.TtkTicks < 0 || floor.TtkTicks >= floorTicks ? "PASS" : "FAIL";
                sb.Append("| ").Append(floor.AttackerFaction).Append(" | ").Append(floor.Unit).Append(" | ").Append(floor.Count.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(floor.DefenderHq).Append(" | ").Append(floor.HqState).Append(" | ").Append(kill).Append(" | ").Append(result).Append(" |\n");
            }
            sb.Append('\n');
        }

        /// <summary>The matchups tables (filled by the matchups checks of package F3).</summary>
        private static void AppendMatchups(StringBuilder sb, FightSettings settings, FightMatchups matchups, IReadOnlyList<FightRecord> records, DataSet data)
        {
            sb.Append("## Matchups file\n\n| Unit | Claim | Measured | Agrees |\n| --- | --- | --- | --- |\n");
            foreach (string line in FightChecks.MatchupRows(records, settings, matchups))
            {
                sb.Append(line).Append('\n');
            }
            sb.Append("\n## Suggested matchups entries\n\n| Unit | Wins against | Loses to |\n| --- | --- | --- |\n");
            foreach (string line in FightChecks.SuggestedRows(records, settings, data))
            {
                sb.Append(line).Append('\n');
            }
            sb.Append('\n');
        }

        /// <summary>Stalemates, unresolved fights, seat splits and every fight.</summary>
        private static void AppendLists(StringBuilder sb, IReadOnlyList<FightRecord> records, IReadOnlyDictionary<string, FightRecord> byId)
        {
            sb.Append("## Stalemates, unresolved fights and seat splits\n\nA seat split means the two seat orders of a pair have different winners. On high_ground and flooded the seats are not symmetric by design, so splits there show terrain at work, not seat bias. A `no_damage` stalemate counts only damage events; when shots absorbed by a shield dome fell inside the idle window the stalemate is `dome_absorbed` instead.\n\n");
            sb.Append("| Fight | Finding | Reason |\n| --- | --- | --- |\n");
            foreach (FightRecord record in records)
            {
                if (record.Result == FightRecord.Stalemate || record.Result == FightRecord.Unresolved)
                {
                    sb.Append("| ").Append(record.Fight).Append(" | ").Append(record.Result).Append(" | ").Append(record.Reason).Append(" |\n");
                }
                else if (record.Seat0 != record.Seat1 && string.CompareOrdinal(record.Seat0, record.Seat1) < 0 && !record.Mismatch)
                {
                    // Seat split: the mirror (swapped seats) is won by the other side.
                    string mirrorId = record.Case + "/" + record.ArmyValue.ToString(CultureInfo.InvariantCulture) + "/" + record.Seat1 + "/" + record.Seat0;
                    if (byId.TryGetValue(mirrorId, out FightRecord? mirror) && !mirror.Mismatch && WinnerId(record).Length > 0 && WinnerId(mirror).Length > 0 && WinnerId(record) != WinnerId(mirror))
                    {
                        sb.Append("| ").Append(record.Fight).Append(" | seat split (").Append(WinnerId(record)).Append(" wins this order, ").Append(WinnerId(mirror))
                            .Append(" wins the swapped order) | ").Append(record.Reason).Append(" |\n");
                    }
                }
            }
            sb.Append("\n## All fights\n\n| Fight | Result | Reason | Seconds | Left 0 | Left 1 | Values |\n| --- | --- | --- | ---: | ---: | ---: | --- |\n");
            foreach (FightRecord record in records)
            {
                sb.Append("| ").Append(record.Fight).Append(" | ").Append(record.Result).Append(" | ").Append(record.Reason).Append(" | ").Append(record.Seconds).Append(" | ")
                    .Append(record.Left0.ToString("0.000", CultureInfo.InvariantCulture)).Append(" | ").Append(record.Left1.ToString("0.000", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(record.Value0.ToString(CultureInfo.InvariantCulture)).Append("/").Append(record.Value1.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
            }
        }
    }
}

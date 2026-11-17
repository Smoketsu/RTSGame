// rts/tools/BalanceHarness/FightChecks.cs
// The automated design-rule checks of the fight mode (spec: rts/docs/specs/micro_benchmark.md section 6, P-005 section 1.6 rules 1 to 6 and the matchups file). Each check is a pure
// function of the fight records, the settings, the matchups file and the data set (rule 6 reads the HQ floor records instead of fights); none changes the exit code unless --strict is
// given. All checks read fights on rule_case at rule_army_value and skip mismatch fights, except where a check names other cases. "A beats B" means A's side won in both seats.
// Tool-side only: ratios and fractions are doubles. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Units;

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

        /// <summary>One line per failing item (a note line when it passed).</summary>
        public IReadOnlyList<string> Detail { get; }

        /// <summary>Bundles the values.</summary>
        public CheckResult(string name, string rule, string threshold, IReadOnlyList<string> failures, string passNote)
        {
            Name = name;
            Rule = rule;
            Threshold = threshold;
            Pass = failures.Count == 0;
            Detail = Pass ? new[] { passNote } : failures;
        }
    }

    /// <summary>The fights of a sweep looked up by case, army value and the two sides.</summary>
    internal sealed class FightIndex
    {
        /// <summary>Records by fight ID.</summary>
        private readonly Dictionary<string, FightRecord> _byId = new Dictionary<string, FightRecord>(StringComparer.Ordinal);

        /// <summary>Indexes the records.</summary>
        public FightIndex(IReadOnlyList<FightRecord> records)
        {
            foreach (FightRecord record in records)
            {
                _byId[record.Fight] = record;
            }
        }

        /// <summary>The fight with <paramref name="seat0"/> in seat 0, or null when it was not played or is a mismatch.</summary>
        public FightRecord? Get(string fightCase, int armyValue, string seat0, string seat1)
        {
            return _byId.TryGetValue(fightCase + "/" + armyValue.ToString(CultureInfo.InvariantCulture) + "/" + seat0 + "/" + seat1, out FightRecord? record) && !record.Mismatch ? record : null;
        }

        /// <summary>True when <paramref name="a"/> won in both seats against <paramref name="b"/>; the two records come back for the details (null when missing).</summary>
        public bool Beats(string fightCase, int armyValue, string a, string b, out FightRecord? asSeat0, out FightRecord? asSeat1)
        {
            asSeat0 = Get(fightCase, armyValue, a, b);
            asSeat1 = Get(fightCase, armyValue, b, a);
            return asSeat0 != null && asSeat1 != null && asSeat0.WinnerSeat == 0 && asSeat1.WinnerSeat == 1;
        }

        /// <summary>Convenience: <see cref="Beats(string, int, string, string, out FightRecord?, out FightRecord?)"/> without the records.</summary>
        public bool Beats(string fightCase, int armyValue, string a, string b)
        {
            return Beats(fightCase, armyValue, a, b, out FightRecord? first, out FightRecord? second);
        }

        /// <summary>Mean value left of <paramref name="a"/> over its two fights against <paramref name="b"/>. Unit: fraction. Range: 0 to 1.</summary>
        public double MeanLeft(string fightCase, int armyValue, string a, string b)
        {
            Beats(fightCase, armyValue, a, b, out FightRecord? asSeat0, out FightRecord? asSeat1);
            return ((asSeat0?.Left0 ?? 0.0) + (asSeat1?.Left1 ?? 0.0)) / 2.0;
        }
    }

    /// <summary>The checks.</summary>
    public static class FightChecks
    {
        /// <summary>Formats a fraction with two decimals.</summary>
        private static string F2(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>Formats a threshold without trailing zeros.</summary>
        private static string Th(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>Runs every check in the order of spec section 6.</summary>
        /// <param name="records">Every fight record in plan order.</param>
        /// <param name="floors">Every HQ floor record.</param>
        /// <param name="settings">The parsed fights.json.</param>
        /// <param name="matchups">The parsed matchups.json.</param>
        /// <param name="data">The loaded data set.</param>
        public static List<CheckResult> RunAll(IReadOnlyList<FightRecord> records, IReadOnlyList<HqFloorRecord> floors, FightSettings settings, FightMatchups matchups, DataSet data)
        {
            return new List<CheckResult>
            {
                Rule1NoImmunity(records, settings, matchups, data),
                Rule2MultiplierBand(records, settings, matchups, data),
                Rule3CounterMargin(records, settings, matchups, data),
                Rule4NoDominantUnit(records, settings, matchups, data),
                Rule4EveryUnitWins(records, settings, matchups, data),
                Rule5TwoAnswers(records, settings, matchups, data),
                Rule6HqFloor(floors, settings, data),
                MatchupsAgree(records, settings, matchups, data),
            };
        }

        // ------------------------------------------------------------------ rule 1 and 2: data lints

        /// <summary>
        /// Rule 1 (data lint): every faction with combat units can answer every combat unit and HQ of another faction with at least rule1_min_mobile_answers of its combat units and
        /// rule1_min_static_answers of its static defences, and every armed combat unit can target Ground.
        /// </summary>
        public static CheckResult Rule1NoImmunity(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups, DataSet data)
        {
            List<string> failures = new List<string>();
            // Units of the data set in ordinal ID order: combat units, and combat units plus HQs (the things a faction must be able to hit).
            SortedDictionary<string, UnitDefinition> units = new SortedDictionary<string, UnitDefinition>(StringComparer.Ordinal);
            foreach (UnitDefinition definition in data.Content.Units.Values)
            {
                units[definition.Id] = definition;
            }
            SortedSet<string> factions = new SortedSet<string>(StringComparer.Ordinal);
            foreach (UnitDefinition definition in units.Values)
            {
                if (FightSettings.IsCombatUnit(definition))
                {
                    factions.Add(definition.FactionId);
                }
            }
            int items = 0;
            foreach (string faction in factions)
            {
                foreach (UnitDefinition target in units.Values)
                {
                    if (target.FactionId == faction || !(FightSettings.IsCombatUnit(target) || target.Hq != null))
                    {
                        continue;
                    }
                    items++;
                    // Own combat units and static defences that can hit the target's layer, in ID order.
                    List<string> mobile = new List<string>();
                    List<string> statics = new List<string>();
                    foreach (UnitDefinition own in units.Values)
                    {
                        if (own.FactionId != faction || own.Weapon == null || !own.Weapon.CanTarget(target.Layer))
                        {
                            continue;
                        }
                        if (FightSettings.IsCombatUnit(own))
                        {
                            mobile.Add(own.Id);
                        }
                        else if (own.Economy.IsStaticDefence)
                        {
                            statics.Add(own.Id);
                        }
                    }
                    if (mobile.Count < settings.Rule1MinMobileAnswers)
                    {
                        failures.Add(faction + ": " + target.Id + " attacked by " + mobile.Count.ToString(CultureInfo.InvariantCulture) + " mobile unit" + (mobile.Count == 1 ? string.Empty : "s")
                            + " (" + string.Join(", ", mobile) + "), needs " + settings.Rule1MinMobileAnswers.ToString(CultureInfo.InvariantCulture));
                    }
                    if (statics.Count < settings.Rule1MinStaticAnswers)
                    {
                        failures.Add(faction + ": " + target.Id + " attacked by " + statics.Count.ToString(CultureInfo.InvariantCulture) + " static defence" + (statics.Count == 1 ? string.Empty : "s")
                            + " (" + string.Join(", ", statics) + "), needs " + settings.Rule1MinStaticAnswers.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
            foreach (UnitDefinition unit in units.Values)
            {
                if (FightSettings.IsCombatUnit(unit) && !unit.Weapon!.CanTargetGround)
                {
                    failures.Add(unit.Id + " cannot target Ground");
                }
            }
            return new CheckResult("rule1_no_immunity", "P-005 1.6 rule 1",
                ">= " + settings.Rule1MinMobileAnswers.ToString(CultureInfo.InvariantCulture) + " mobile and >= " + settings.Rule1MinStaticAnswers.ToString(CultureInfo.InvariantCulture)
                + " static answers per enemy unit or HQ; every armed unit hits Ground", failures, items.ToString(CultureInfo.InvariantCulture) + " targets checked");
        }

        /// <summary>Rule 2 (data lint on combat.json): every multiplier within [rule2_min_multiplier, rule2_max_multiplier] and, within a damage type, highest over lowest at most rule2_max_ratio_within_type.</summary>
        public static CheckResult Rule2MultiplierBand(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups, DataSet data)
        {
            List<string> failures = new List<string>();
            // Tolerance for the double comparisons. Unit: fraction. Range: fixed 1e-9.
            const double tolerance = 1e-9;
            Rts.Sim.Data.CombatTuning combat = data.Tuning.Combat;
            foreach (string damage in combat.DamageTypes)
            {
                // Lowest and highest multiplier of this damage type. Unit: fraction.
                double low = double.PositiveInfinity;
                double high = double.NegativeInfinity;
                foreach (string armour in combat.ArmourTypes)
                {
                    double value = HarnessStats.ToDouble(combat.GetMultiplier(damage, armour));
                    low = Math.Min(low, value);
                    high = Math.Max(high, value);
                    if (value < settings.Rule2MinMultiplier - tolerance)
                    {
                        failures.Add(damage + "/" + armour + " " + Th(value) + " below " + Th(settings.Rule2MinMultiplier));
                    }
                    else if (value > settings.Rule2MaxMultiplier + tolerance)
                    {
                        failures.Add(damage + "/" + armour + " " + Th(value) + " above " + Th(settings.Rule2MaxMultiplier));
                    }
                }
                if (low > 0.0 && high / low > settings.Rule2MaxRatioWithinType + tolerance)
                {
                    failures.Add(damage + " ratio " + F2(high / low) + " above " + Th(settings.Rule2MaxRatioWithinType));
                }
            }
            return new CheckResult("rule2_multiplier_band", "P-005 1.6 rule 2",
                "multipliers in [" + Th(settings.Rule2MinMultiplier) + ", " + Th(settings.Rule2MaxMultiplier) + "]; ratio within a damage type <= " + Th(settings.Rule2MaxRatioWithinType), failures, "all multipliers in band");
        }

        // ------------------------------------------------------------------ rules 3 to 5: fights

        /// <summary>Rule 3: for each matchups entry whose loses names Y, Y beats the unit and in each seat keeps at least rule3_min_value_left with an exchange ratio of at most rule3_max_exchange_ratio.</summary>
        public static CheckResult Rule3CounterMargin(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups, DataSet data)
        {
            List<string> failures = new List<string>();
            FightIndex index = new FightIndex(records);
            int claims = 0;
            foreach (MatchupEntry entry in matchups.Entries.Values)
            {
                if (entry.Loses == null)
                {
                    continue;
                }
                claims++;
                string x = entry.Unit;
                string y = entry.Loses.Against;
                string prefix = x + " loses to " + y + " on " + entry.Loses.Case + ": ";
                if (!index.Beats(entry.Loses.Case, settings.RuleArmyValue, y, x, out FightRecord? ySeat0, out FightRecord? ySeat1))
                {
                    failures.Add(prefix + y + " does not beat it (" + SeatText(ySeat0, 0, y) + ", " + SeatText(ySeat1, 1, y) + ")");
                    continue;
                }
                foreach (FightRecord record in new[] { ySeat0!, ySeat1! })
                {
                    // The seat Y plays in this record, its value left and its exchange ratio.
                    int seat = record.Seat0 == y ? 0 : 1;
                    double left = record.Left(seat);
                    double ratio = record.WinnerExchangeRatio();
                    if (left < settings.Rule3MinValueLeft - 1e-9)
                    {
                        failures.Add(prefix + y + " in seat " + seat.ToString(CultureInfo.InvariantCulture) + " keeps " + F2(left) + ", below " + Th(settings.Rule3MinValueLeft));
                    }
                    if (ratio > settings.Rule3MaxExchangeRatio + 1e-9)
                    {
                        failures.Add(prefix + y + " in seat " + seat.ToString(CultureInfo.InvariantCulture) + " exchange ratio " + (double.IsInfinity(ratio) ? "inf" : ratio.ToString("0.0", CultureInfo.InvariantCulture))
                            + " above " + Th(settings.Rule3MaxExchangeRatio));
                    }
                }
            }
            return new CheckResult("rule3_counter_margin", "P-005 1.6 rule 3",
                "counter keeps >= " + Th(settings.Rule3MinValueLeft) + " value, exchange ratio <= " + Th(settings.Rule3MaxExchangeRatio), failures, claims.ToString(CultureInfo.InvariantCulture) + " counters checked");
        }

        /// <summary>Rule 4a: no combat unit beats every other combat unit of its own faction (a faction with one unit is not applicable).</summary>
        public static CheckResult Rule4NoDominantUnit(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups, DataSet data)
        {
            List<string> failures = new List<string>();
            FightIndex index = new FightIndex(records);
            foreach (string x in settings.Units)
            {
                // Faction mates of x in the sweep; dominant when x beats all of them (and there is at least one).
                List<string> mates = new List<string>();
                foreach (string other in settings.Units)
                {
                    if (other != x && data.Content.Units[other].FactionId == data.Content.Units[x].FactionId)
                    {
                        mates.Add(other);
                    }
                }
                bool dominant = mates.Count > 0;
                foreach (string mate in mates)
                {
                    dominant &= index.Beats(settings.RuleCase, settings.RuleArmyValue, x, mate);
                }
                if (dominant)
                {
                    failures.Add(data.Content.Units[x].FactionId + ": " + x + " beats every other " + data.Content.Units[x].FactionId + " unit");
                }
            }
            return new CheckResult("rule4_no_dominant_unit", "P-005 1.6 rule 4", "no unit beats all its faction mates on " + settings.RuleCase, failures, "no dominant unit");
        }

        /// <summary>Rule 4b: every combat unit beats at least one combat unit of another faction.</summary>
        public static CheckResult Rule4EveryUnitWins(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups, DataSet data)
        {
            List<string> failures = new List<string>();
            FightIndex index = new FightIndex(records);
            foreach (string x in settings.Units)
            {
                bool wins = false;
                foreach (string other in settings.Units)
                {
                    wins |= data.Content.Units[other].FactionId != data.Content.Units[x].FactionId && index.Beats(settings.RuleCase, settings.RuleArmyValue, x, other);
                }
                if (!wins)
                {
                    failures.Add(x + " beats no unit of another faction on " + settings.RuleCase);
                }
            }
            return new CheckResult("rule4_every_unit_wins", "P-005 1.6 rule 4", "every unit beats >= 1 unit of another faction on " + settings.RuleCase, failures, "every unit wins somewhere");
        }

        /// <summary>
        /// Rule 5: for each threat and faction, at least rule5_min_unit_answers combat units of the faction beat the threat on rule_case, and at least rule5_min_terrain_answers non-flat cases
        /// where such a unit that does not beat it on flat wins from the seat where the faction holds the terrain (non-purchase answers measured: terrain only).
        /// </summary>
        public static CheckResult Rule5TwoAnswers(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups, DataSet data)
        {
            List<string> failures = new List<string>();
            FightIndex index = new FightIndex(records);
            SortedSet<string> factions = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string unit in settings.Units)
            {
                factions.Add(data.Content.Units[unit].FactionId);
            }
            foreach (FightThreat threat in settings.Threats)
            {
                foreach (string faction in factions)
                {
                    // Own units of this faction that could answer, excluding the threat itself.
                    List<string> candidates = new List<string>();
                    foreach (string unit in settings.Units)
                    {
                        if (data.Content.Units[unit].FactionId == faction && unit != threat.Side)
                        {
                            candidates.Add(unit);
                        }
                    }
                    List<string> unitAnswers = new List<string>();
                    List<string> flatMisses = new List<string>();
                    foreach (string unit in candidates)
                    {
                        if (index.Beats(settings.RuleCase, settings.RuleArmyValue, unit, threat.Side))
                        {
                            unitAnswers.Add(unit);
                        }
                        else
                        {
                            flatMisses.Add(unit);
                        }
                    }
                    List<string> terrainAnswers = new List<string>();
                    foreach (FightCase fightCase in settings.Cases)
                    {
                        if (fightCase.Kind == FightKinds.Flat)
                        {
                            continue;
                        }
                        // Army value of the case nearest rule_army_value, the lower one on a tie. Unit: resource units.
                        int value = NearestValue(fightCase, settings.RuleArmyValue);
                        foreach (string unit in flatMisses)
                        {
                            bool wins;
                            if (fightCase.Kind == FightKinds.Choke)
                            {
                                wins = index.Beats(fightCase.Id, value, unit, threat.Side);
                            }
                            else
                            {
                                // The faction holds the terrain in seat 1: the threat attacks from seat 0.
                                FightRecord? held = index.Get(fightCase.Id, value, threat.Side, unit);
                                wins = held != null && held.WinnerSeat == 1;
                            }
                            if (wins)
                            {
                                terrainAnswers.Add(fightCase.Id + " (" + unit + ")");
                                break;
                            }
                        }
                    }
                    if (unitAnswers.Count < settings.Rule5MinUnitAnswers || terrainAnswers.Count < settings.Rule5MinTerrainAnswers)
                    {
                        failures.Add(threat.Id + " (" + threat.Side + ") against " + faction + ": " + unitAnswers.Count.ToString(CultureInfo.InvariantCulture) + " unit answers ("
                            + string.Join(", ", unitAnswers) + "), needs " + settings.Rule5MinUnitAnswers.ToString(CultureInfo.InvariantCulture) + "; "
                            + terrainAnswers.Count.ToString(CultureInfo.InvariantCulture) + " terrain answers (" + string.Join(", ", terrainAnswers) + "), needs "
                            + settings.Rule5MinTerrainAnswers.ToString(CultureInfo.InvariantCulture) + " (non-purchase answers measured: terrain only)");
                    }
                }
            }
            return new CheckResult("rule5_two_answers", "P-005 1.6 rule 5",
                ">= " + settings.Rule5MinUnitAnswers.ToString(CultureInfo.InvariantCulture) + " unit and >= " + settings.Rule5MinTerrainAnswers.ToString(CultureInfo.InvariantCulture)
                + " terrain answers per threat and faction", failures, settings.Threats.Count.ToString(CultureInfo.InvariantCulture) + " threats checked (non-purchase answers measured: terrain only)");
        }

        /// <summary>The army value of a case nearest to <paramref name="wanted"/>; the lower one on a tie.</summary>
        private static int NearestValue(FightCase fightCase, int wanted)
        {
            int best = fightCase.ArmyValues[0];
            foreach (int value in fightCase.ArmyValues)
            {
                int distance = Math.Abs(value - wanted);
                int bestDistance = Math.Abs(best - wanted);
                if (distance < bestDistance || (distance == bestDistance && value < best))
                {
                    best = value;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ rule 6: HQ floor

        /// <summary>Rule 6: every Rooted HQ floor trial lasts at least hq_floor_seconds (a trial the HQ survived passes); Uprooted trials are reported only.</summary>
        /// <param name="floors">Every HQ floor record.</param>
        /// <param name="settings">The parsed fights.json.</param>
        /// <param name="data">The loaded data set.</param>
        public static CheckResult Rule6HqFloor(IReadOnlyList<HqFloorRecord> floors, FightSettings settings, DataSet data)
        {
            List<string> failures = new List<string>();
            // Tick rate and the floor in ticks. Unit: ticks.
            int tps = data.Tuning.Match.TicksPerSecond;
            int floorTicks = data.Tuning.Harness.HqFloorTicks(new MatchClock(tps));
            int rooted = 0;
            foreach (HqFloorRecord floor in floors)
            {
                if (floor.HqState != "rooted")
                {
                    continue;
                }
                rooted++;
                if (floor.TtkTicks >= 0 && floor.TtkTicks < floorTicks)
                {
                    failures.Add(floor.AttackerFaction + " " + floor.Unit + " x" + floor.Count.ToString(CultureInfo.InvariantCulture) + " vs " + floor.DefenderHq + ": "
                        + HarnessStats.Seconds(floor.TtkTicks, tps) + " s, needs >= " + HarnessStats.Seconds(floorTicks, tps) + " s");
                }
            }
            return new CheckResult("rule6_hq_floor", "P-005 1.6 rule 6", "Rooted HQ lives >= " + HarnessStats.Seconds(floorTicks, tps) + " s against "
                + data.Tuning.Harness.HqFloorSupply.ToString(CultureInfo.InvariantCulture) + " supply", failures, rooted.ToString(CultureInfo.InvariantCulture) + " Rooted trials checked");
        }

        // ------------------------------------------------------------------ matchups file

        /// <summary>Text of the seat <paramref name="seat"/> fight from <paramref name="unit"/>'s side: "seat 0 win (left 0.41)".</summary>
        private static string SeatText(FightRecord? record, int seat, string unit)
        {
            if (record == null)
            {
                return "seat " + seat.ToString(CultureInfo.InvariantCulture) + " missing or mismatch";
            }
            // The seat the unit's side plays in the record, and how that went for it.
            int own = record.Side(seat) == unit ? seat : 1 - seat;
            string outcome = record.WinnerSeat < 0 ? record.Result : record.WinnerSeat == own ? "win" : "loss";
            return "seat " + seat.ToString(CultureInfo.InvariantCulture) + " " + outcome + " (left " + F2(record.Left(own)) + ")";
        }

        /// <summary>Measured text of <paramref name="unit"/> against <paramref name="other"/>: both seats from the unit's side, a mismatch or missing fight named as such.</summary>
        private static string Measured(FightIndex index, string fightCase, int armyValue, string unit, string other)
        {
            index.Beats(fightCase, armyValue, unit, other, out FightRecord? asSeat0, out FightRecord? asSeat1);
            return "measured " + SeatText(asSeat0, 0, unit) + ", " + SeatText(asSeat1, 1, unit);
        }

        /// <summary>
        /// matchups_agree: every combat unit has an entry; every wins is a measured beat on its case; every loses is a measured loss on its case; a null wins on an armed unit is a disagreement.
        /// </summary>
        public static CheckResult MatchupsAgree(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups, DataSet data)
        {
            List<string> failures = new List<string>();
            FightIndex index = new FightIndex(records);
            foreach (string unit in settings.Units)
            {
                if (!matchups.Entries.TryGetValue(unit, out MatchupEntry? entry))
                {
                    failures.Add(unit + ": no entry in matchups.json");
                    continue;
                }
                if (entry.Wins == null)
                {
                    failures.Add(unit + ": wins is null, but every armed unit must claim a fight it wins");
                }
                else if (!index.Beats(entry.Wins.Case, settings.RuleArmyValue, unit, entry.Wins.Against))
                {
                    failures.Add(unit + ": claims it beats " + entry.Wins.Against + " on " + entry.Wins.Case + "; " + Measured(index, entry.Wins.Case, settings.RuleArmyValue, unit, entry.Wins.Against));
                }
                if (entry.Loses != null && !index.Beats(entry.Loses.Case, settings.RuleArmyValue, entry.Loses.Against, unit))
                {
                    failures.Add(unit + ": claims it loses to " + entry.Loses.Against + " on " + entry.Loses.Case + "; " + Measured(index, entry.Loses.Case, settings.RuleArmyValue, unit, entry.Loses.Against));
                }
            }
            return new CheckResult("matchups_agree", "P-005 developer decision 1 (rule 7)", "every unit has an entry; wins and loses match the fights", failures,
                settings.Units.Count.ToString(CultureInfo.InvariantCulture) + " units checked");
        }

        /// <summary>Report rows of the matchups table: one row per claim of each swept unit (and one for a missing entry).</summary>
        public static List<string> MatchupRows(IReadOnlyList<FightRecord> records, FightSettings settings, FightMatchups matchups)
        {
            List<string> rows = new List<string>();
            FightIndex index = new FightIndex(records);
            foreach (string unit in settings.Units)
            {
                if (!matchups.Entries.TryGetValue(unit, out MatchupEntry? entry))
                {
                    rows.Add("| " + unit + " | (no entry) | - | missing |");
                    continue;
                }
                if (entry.Wins == null)
                {
                    rows.Add("| " + unit + " | wins: null | - | no |");
                }
                else
                {
                    bool agrees = index.Beats(entry.Wins.Case, settings.RuleArmyValue, unit, entry.Wins.Against);
                    rows.Add("| " + unit + " | beats " + entry.Wins.Against + " on " + entry.Wins.Case + " | " + Measured(index, entry.Wins.Case, settings.RuleArmyValue, unit, entry.Wins.Against) + " | " + (agrees ? "yes" : "no") + " |");
                }
                if (entry.Loses == null)
                {
                    rows.Add("| " + unit + " | loses: null | - | n/a |");
                }
                else
                {
                    bool agrees = index.Beats(entry.Loses.Case, settings.RuleArmyValue, entry.Loses.Against, unit);
                    rows.Add("| " + unit + " | loses to " + entry.Loses.Against + " on " + entry.Loses.Case + " | " + Measured(index, entry.Loses.Case, settings.RuleArmyValue, unit, entry.Loses.Against) + " | " + (agrees ? "yes" : "no") + " |");
                }
            }
            return rows;
        }

        /// <summary>
        /// Suggested entries (report only): per unit X, wins is the opponent X beats with the highest mean value left over both seats, loses the opponent that beats X with the highest
        /// mean value left; ties go to an other-faction opponent, then ordinal ID. Either is null when no such fight exists.
        /// </summary>
        public static List<string> SuggestedRows(IReadOnlyList<FightRecord> records, FightSettings settings, DataSet data)
        {
            List<string> rows = new List<string>();
            FightIndex index = new FightIndex(records);
            foreach (string x in settings.Units)
            {
                // Best win and worst loss found so far: opponent ID and mean value left of the winner. Unit: fraction.
                string? win = null;
                double winLeft = -1.0;
                string? loss = null;
                double lossLeft = -1.0;
                foreach (string other in settings.Units)
                {
                    if (other == x)
                    {
                        continue;
                    }
                    bool otherFaction = data.Content.Units[other].FactionId != data.Content.Units[x].FactionId;
                    if (index.Beats(settings.RuleCase, settings.RuleArmyValue, x, other))
                    {
                        double left = index.MeanLeft(settings.RuleCase, settings.RuleArmyValue, x, other);
                        if (win == null || left > winLeft + 1e-9 || (Math.Abs(left - winLeft) <= 1e-9 && otherFaction && data.Content.Units[win].FactionId == data.Content.Units[x].FactionId))
                        {
                            win = other;
                            winLeft = left;
                        }
                    }
                    if (index.Beats(settings.RuleCase, settings.RuleArmyValue, other, x))
                    {
                        double left = index.MeanLeft(settings.RuleCase, settings.RuleArmyValue, other, x);
                        if (loss == null || left > lossLeft + 1e-9 || (Math.Abs(left - lossLeft) <= 1e-9 && otherFaction && data.Content.Units[loss].FactionId == data.Content.Units[x].FactionId))
                        {
                            loss = other;
                            lossLeft = left;
                        }
                    }
                }
                rows.Add("| " + x + " | " + (win == null ? "null" : win + " (left " + F2(winLeft) + ")") + " | " + (loss == null ? "null" : loss + " (their left " + F2(lossLeft) + ")") + " |");
            }
            return rows;
        }
    }
}

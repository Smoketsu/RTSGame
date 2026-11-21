// rts/tools/BalanceHarness/FightPlan.cs
// The plan of the equal-value fight mode (spec: rts/docs/specs/micro_benchmark.md sections 4.1 to 4.3): the sides (swept units then compositions), the army scaling to an equal
// value, the list of fights in plan order (case, army value, unordered side pair, both seat orders), the in-memory arena of each terrain case and the formation positions.
// Pure of processes and files; coordinator, workers and tests all build the same plan from the same data. Plan-time problems are HarnessExceptions naming the fight ID.
// Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>One side of a fight: a swept unit or a composition.</summary>
    public sealed class FightSide
    {
        /// <summary>Unit ID or composition ID.</summary>
        public string Id { get; }

        /// <summary>True for a composition, false for a single unit.</summary>
        public bool IsComposition { get; }

        /// <summary>Faction ID of the side's units.</summary>
        public string Faction { get; }

        /// <summary>Recipe: unit ID to count in ordinal order (a single unit has count 1). Unit: units. Range: 1 to 8 entries.</summary>
        public IReadOnlyDictionary<string, int> Recipe { get; }

        /// <summary>Value of one recipe. Unit: resource units. Range: 1 upward.</summary>
        public int RecipeValue { get; }

        /// <summary>Supply of one recipe. Unit: supply. Range: 1 upward.</summary>
        public int RecipeSupply { get; }

        /// <summary>Bundles the values.</summary>
        public FightSide(string id, bool isComposition, string faction, IReadOnlyDictionary<string, int> recipe, int recipeValue, int recipeSupply)
        {
            Id = id;
            IsComposition = isComposition;
            Faction = faction;
            Recipe = recipe;
            RecipeValue = recipeValue;
            RecipeSupply = recipeSupply;
        }

        /// <summary>Recipes in an army of value <paramref name="armyValue"/>: max(1, (2V + rv) / (2 rv)), integer division (rounds half up).</summary>
        /// <param name="armyValue">Army value. Unit: resource units. Range: 50 to 20000.</param>
        public int Multiplier(int armyValue)
        {
            return Math.Max(1, (2 * armyValue + RecipeValue) / (2 * RecipeValue));
        }
    }

    /// <summary>One planned fight.</summary>
    public sealed class FightSpec
    {
        /// <summary>Position in plan order. Unit: fights. Range: 0 upward.</summary>
        public int Index { get; }

        /// <summary>Fight ID "&lt;case&gt;/&lt;value&gt;/&lt;seat 0 side&gt;/&lt;seat 1 side&gt;".</summary>
        public string Id { get; }

        /// <summary>The terrain case.</summary>
        public FightCase Case { get; }

        /// <summary>Target army value. Unit: resource units. Range: 50 to 20000.</summary>
        public int ArmyValue { get; }

        /// <summary>Side in seat 0.</summary>
        public FightSide Seat0 { get; }

        /// <summary>Side in seat 1.</summary>
        public FightSide Seat1 { get; }

        /// <summary>Recipes in seat 0's army. Unit: recipes. Range: 1 upward.</summary>
        public int Multiplier0 { get; }

        /// <summary>Recipes in seat 1's army. Unit: recipes. Range: 1 upward.</summary>
        public int Multiplier1 { get; }

        /// <summary>Start value of seat 0's army. Unit: resource units. Range: 1 upward.</summary>
        public int Value0 => Multiplier0 * Seat0.RecipeValue;

        /// <summary>Start value of seat 1's army. Unit: resource units. Range: 1 upward.</summary>
        public int Value1 => Multiplier1 * Seat1.RecipeValue;

        /// <summary>True when seat 1's start value differs from the ratio times seat 0's by more than max_value_mismatch_fraction of the larger of the two.</summary>
        public bool Mismatch { get; }

        /// <summary>The ratio of case value seat 1 aims at, relative to seat 0's (fight_predictor_v2.md 5.2). Unit: parts per 10 000. Range: 2000 to 50000; <see cref="EqualRatioPt"/> for an ordinary fight.</summary>
        public int RatioPt { get; }

        /// <summary>The ratio 1 in parts per 10 000: equal target values, fight IDs without a ratio suffix. Unit: parts per 10 000. Range: fixed 10000.</summary>
        public const int EqualRatioPt = 10000;

        /// <summary>Bundles the values.</summary>
        public FightSpec(int index, FightCase fightCase, int armyValue, FightSide seat0, FightSide seat1, double maxMismatchFraction, int ratioPt = EqualRatioPt)
        {
            Index = index;
            Case = fightCase;
            ArmyValue = armyValue;
            Seat0 = seat0;
            Seat1 = seat1;
            RatioPt = ratioPt;
            // Seat 1's target value, rounded half up in integers. Unit: resource units. Range: 10 to 100000.
            int target1 = ratioPt == EqualRatioPt ? armyValue : (int)(((long)armyValue * ratioPt + 5000) / 10000);
            Multiplier0 = seat0.Multiplier(armyValue);
            Multiplier1 = seat1.Multiplier(target1);
            Id = fightCase.Id + "/" + armyValue.ToString(CultureInfo.InvariantCulture) + "/" + seat0.Id + "/" + seat1.Id
                + (ratioPt == EqualRatioPt ? string.Empty : "/r" + ratioPt.ToString(CultureInfo.InvariantCulture));
            if (ratioPt == EqualRatioPt)
            {
                // Larger and smaller start value. Unit: resource units.
                int larger = Math.Max(Value0, Value1);
                int smaller = Math.Min(Value0, Value1);
                Mismatch = (larger - smaller) > maxMismatchFraction * larger;
            }
            else
            {
                // Seat 1's start value and the ratio times seat 0's, both times 10 000 so the comparison stays in integers. Unit: resource units x 10 000.
                long actual = (long)Value1 * 10000;
                long wanted = (long)ratioPt * Value0;
                Mismatch = Math.Abs(actual - wanted) > maxMismatchFraction * Math.Max(actual, wanted);
            }
        }

        /// <summary>The side in <paramref name="seat"/> (0 or 1).</summary>
        /// <param name="seat">Seat number. Unit: seat. Range: 0 or 1.</param>
        public FightSide Side(int seat)
        {
            return seat == 0 ? Seat0 : Seat1;
        }

        /// <summary>Recipes in the army of <paramref name="seat"/>. Unit: recipes. Range: 1 upward.</summary>
        /// <param name="seat">Seat number. Unit: seat. Range: 0 or 1.</param>
        public int Multiplier(int seat)
        {
            return seat == 0 ? Multiplier0 : Multiplier1;
        }
    }

    /// <summary>A spawn position of one army unit.</summary>
    public sealed class FightSpawn
    {
        /// <summary>Unit definition ID.</summary>
        public string Unit { get; }

        /// <summary>Position. Unit: tiles (fixed-point).</summary>
        public FixVec2 Position { get; }

        /// <summary>Bundles the values.</summary>
        public FightSpawn(string unit, FixVec2 position)
        {
            Unit = unit;
            Position = position;
        }
    }

    /// <summary>The plan: sides and fights.</summary>
    public sealed class FightPlan
    {
        /// <summary>The sides in order: swept units (ordinal), then compositions (file order).</summary>
        public IReadOnlyList<FightSide> Sides { get; }

        /// <summary>Every fight in plan order.</summary>
        public IReadOnlyList<FightSpec> Fights { get; }

        /// <summary>Bundles the values.</summary>
        public FightPlan(IReadOnlyList<FightSide> sides, IReadOnlyList<FightSpec> fights)
        {
            Sides = sides;
            Fights = fights;
        }

        /// <summary>Builds the plan and checks every spawn position; throws <see cref="HarnessException"/> naming the first fight that cannot be set up.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="settings">The parsed settings.</param>
        public static FightPlan Build(DataSet data, FightSettings settings)
        {
            List<FightSide> sides = new List<FightSide>();
            foreach (string unitId in settings.Units)
            {
                UnitDefinition unit = data.Content.Units[unitId];
                sides.Add(new FightSide(unitId, false, unit.FactionId, new SortedDictionary<string, int>(StringComparer.Ordinal) { [unitId] = 1 }, FightSettings.ValueOf(unit), unit.Supply));
            }
            foreach (FightComposition composition in settings.Compositions)
            {
                // Value and supply of one recipe. Unit: resource units and supply.
                int value = 0;
                int supply = 0;
                string faction = string.Empty;
                foreach (KeyValuePair<string, int> pair in composition.Units)
                {
                    UnitDefinition unit = data.Content.Units[pair.Key];
                    value += pair.Value * FightSettings.ValueOf(unit);
                    supply += pair.Value * unit.Supply;
                    faction = unit.FactionId;
                }
                sides.Add(new FightSide(composition.Id, true, faction, composition.Units, value, supply));
            }
            List<FightSpec> fights = new List<FightSpec>();
            foreach (FightCase fightCase in settings.Cases)
            {
                foreach (int armyValue in fightCase.ArmyValues)
                {
                    foreach (int ratioPt in fightCase.RatiosPt)
                    {
                        for (int i = 0; i < sides.Count; i++)
                        {
                            for (int j = i + 1; j < sides.Count; j++)
                            {
                                fights.Add(new FightSpec(fights.Count, fightCase, armyValue, sides[i], sides[j], settings.MaxValueMismatchFraction, ratioPt));
                                fights.Add(new FightSpec(fights.Count, fightCase, armyValue, sides[j], sides[i], settings.MaxValueMismatchFraction, ratioPt));
                            }
                        }
                    }
                }
            }
            foreach (FightSpec fight in fights)
            {
                Validate(data, settings, fight);
            }
            return new FightPlan(sides, fights);
        }

        /// <summary>Plan-time checks of one fight: the choke fits between the armies and every spawn position is on the arena.</summary>
        private static void Validate(DataSet data, FightSettings settings, FightSpec fight)
        {
            if (fight.Case.Kind == FightKinds.Choke && settings.StartDistanceTiles < settings.ChokeWallThicknessTiles + 2)
            {
                throw new HarnessException("fight " + fight.Id + ": start_distance_tiles " + settings.StartDistanceTiles.ToString(CultureInfo.InvariantCulture)
                    + " is too small for the choke wall (needs at least choke_wall_thickness_tiles + 2 = " + (settings.ChokeWallThicknessTiles + 2).ToString(CultureInfo.InvariantCulture) + ")");
            }
            for (int seat = 0; seat < 2; seat++)
            {
                foreach (FightSpawn spawn in Formation(data, settings, fight, seat))
                {
                    // Tile of the spawn position. Unit: tiles.
                    int x = spawn.Position.X.FloorToInt();
                    int y = spawn.Position.Y.FloorToInt();
                    if (x < 0 || y < 0 || x >= settings.ArenaWidthTiles || y >= settings.ArenaHeightTiles)
                    {
                        throw new HarnessException("fight " + fight.Id + ": a " + spawn.Unit + " of seat " + seat.ToString(CultureInfo.InvariantCulture) + " would spawn at tile ("
                            + x.ToString(CultureInfo.InvariantCulture) + ", " + y.ToString(CultureInfo.InvariantCulture) + "), outside the " + settings.ArenaWidthTiles.ToString(CultureInfo.InvariantCulture)
                            + " by " + settings.ArenaHeightTiles.ToString(CultureInfo.InvariantCulture) + " arena");
                    }
                }
            }
        }

        /// <summary>Arena centre column. Unit: tiles. Range: 8 to 64.</summary>
        public static int CentreX(FightSettings settings)
        {
            return settings.ArenaWidthTiles / 2;
        }

        /// <summary>Arena centre row. Unit: tiles. Range: 8 to 64.</summary>
        public static int CentreY(FightSettings settings)
        {
            return settings.ArenaHeightTiles / 2;
        }

        /// <summary>Front column of seat 0: cx - (d + 1) / 2. Unit: tiles. Range: may be negative for an invalid distance.</summary>
        public static int FrontColumn0(FightSettings settings)
        {
            return CentreX(settings) - (settings.StartDistanceTiles + 1) / 2;
        }

        /// <summary>Front column of seat 1: f0 + d. Unit: tiles.</summary>
        public static int FrontColumn1(FightSettings settings)
        {
            return FrontColumn0(settings) + settings.StartDistanceTiles;
        }

        /// <summary>The units of <paramref name="seat"/>'s army sorted for the formation: weapon range ascending (melee in front), ties by definition ID ordinal, each recipe unit repeated by its count times the multiplier.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="side">The side.</param>
        /// <param name="multiplier">Recipes in the army. Unit: recipes. Range: 1 upward.</param>
        public static List<UnitDefinition> ArmyUnits(DataSet data, FightSide side, int multiplier)
        {
            List<UnitDefinition> units = new List<UnitDefinition>();
            foreach (KeyValuePair<string, int> pair in side.Recipe)
            {
                for (int n = 0; n < pair.Value * multiplier; n++)
                {
                    units.Add(data.Content.Units[pair.Key]);
                }
            }
            units.Sort((a, b) =>
            {
                int byRange = a.Weapon!.RangeTiles.CompareTo(b.Weapon!.RangeTiles);
                return byRange != 0 ? byRange : string.CompareOrdinal(a.Id, b.Id);
            });
            return units;
        }

        /// <summary>The spawn positions of one seat in army order: ranks of formation_width_units, rank 0 nearest the enemy (spec 4.3).</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="settings">The parsed settings.</param>
        /// <param name="fight">The fight.</param>
        /// <param name="seat">Seat number. Unit: seat. Range: 0 or 1.</param>
        public static List<FightSpawn> Formation(DataSet data, FightSettings settings, FightSpec fight, int seat)
        {
            List<UnitDefinition> units = ArmyUnits(data, fight.Side(seat), fight.Multiplier(seat));
            // Spacing between spawn positions. Unit: tiles (fixed-point). Range: 0.5 to 4.
            Fix64 spacing = settings.FormationSpacingTiles;
            // Centre row and the front column of this seat. Unit: tiles.
            int cy = CentreY(settings);
            int front = seat == 0 ? FrontColumn0(settings) : FrontColumn1(settings);
            List<FightSpawn> spawns = new List<FightSpawn>();
            for (int index = 0; index < units.Count; index++)
            {
                // Rank number, position within the rank and units in this rank. Unit: units.
                int rank = index / settings.FormationWidthUnits;
                int j = index % settings.FormationWidthUnits;
                int n = Math.Min(settings.FormationWidthUnits, units.Count - rank * settings.FormationWidthUnits);
                // Distance behind the front column. Unit: tiles (fixed-point).
                Fix64 back = spacing * rank;
                Fix64 x = seat == 0 ? Fix64.FromInt(front) + Fix64.Half - back : Fix64.FromInt(front) + Fix64.Half + back;
                Fix64 y = Fix64.FromInt(cy) + Fix64.Half + spacing * (2 * j - (n - 1)) / 2;
                spawns.Add(new FightSpawn(units[index].Id, new FixVec2(x, y)));
            }
            return spawns;
        }

        /// <summary>True when the tile (x, y) is a lane tile: f0 &lt;= x &lt;= f1 and |y - cy| &lt;= formation_width_units * spacing / 2.</summary>
        /// <param name="settings">The parsed settings.</param>
        /// <param name="x">Tile column. Unit: tiles.</param>
        /// <param name="y">Tile row. Unit: tiles.</param>
        public static bool IsLaneTile(FightSettings settings, int x, int y)
        {
            // Half the formation width. Unit: tiles (fixed-point).
            Fix64 half = settings.FormationSpacingTiles * settings.FormationWidthUnits / 2;
            return x >= FrontColumn0(settings) && x <= FrontColumn1(settings) && Fix64.FromInt(Math.Abs(y - CentreY(settings))) <= half;
        }

        /// <summary>
        /// The in-memory arena of a terrain case (spec 4.3): id fight_arena, spawns at (2, 2) and (W-2, H-2), no fields, no Core. high_ground raises every column from f1 - 2
        /// and cuts one ramp at column f1 - 3; choke raises a wall at the centre with a gap.
        /// </summary>
        /// <param name="settings">The parsed settings.</param>
        /// <param name="fightCase">The case.</param>
        public static MapDefinition BuildArena(FightSettings settings, FightCase fightCase)
        {
            // Arena size, centre and front column of seat 1. Unit: tiles.
            int width = settings.ArenaWidthTiles;
            int height = settings.ArenaHeightTiles;
            int cx = CentreX(settings);
            int cy = CentreY(settings);
            int f1 = FrontColumn1(settings);
            byte[] heights = new byte[width * height];
            bool[] ramps = new bool[width * height];
            if (fightCase.Kind == FightKinds.HighGround)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = Math.Max(0, f1 - 2); x < width; x++)
                    {
                        heights[y * width + x] = 1;
                    }
                }
                // Ramp rows: cy - w/2 for w rows. Unit: tiles.
                int rampColumn = f1 - 3;
                for (int y = cy - settings.RampWidthTiles / 2; y < cy - settings.RampWidthTiles / 2 + settings.RampWidthTiles; y++)
                {
                    if (rampColumn >= 0 && rampColumn < width && y >= 0 && y < height)
                    {
                        ramps[y * width + rampColumn] = true;
                    }
                }
            }
            else if (fightCase.Kind == FightKinds.Choke)
            {
                // Gap rows: cy - g/2 for g rows. Unit: tiles.
                int gapStart = cy - settings.ChokeWidthTiles / 2;
                for (int x = cx - settings.ChokeWallThicknessTiles / 2; x < cx - settings.ChokeWallThicknessTiles / 2 + settings.ChokeWallThicknessTiles; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (x >= 0 && x < width && (y < gapStart || y >= gapStart + settings.ChokeWidthTiles))
                        {
                            heights[y * width + x] = 1;
                        }
                    }
                }
            }
            return new MapDefinition("fight_arena", "Fight arena", width, height,
                new List<FixVec2> { new FixVec2(Fix64.FromInt(2), Fix64.FromInt(2)), new FixVec2(Fix64.FromInt(width - 2), Fix64.FromInt(height - 2)) },
                heights, ramps, null, null);
        }
    }
}

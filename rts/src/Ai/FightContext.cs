// rts/src/Ai/FightContext.cs
// The terrain and set-up facts a fight prediction may use besides the two armies (spec: rts/docs/specs/fight_predictor_v2.md section 4): whether side A shoots uphill, whether the lane
// is flooded and whose water it is, and the gap between the armies when the fight starts. A plain value type with no logic; the formula predictor ignores every field but AUphill,
// the lane simulation (package FP2-2) reads all of them. Part of Rts.Ai. Fix64 and integers only.
using Rts.Sim.Core;

namespace Rts.Ai
{
    /// <summary>How deep the water on the fight's lane is.</summary>
    public enum FightWater : byte
    {
        /// <summary>No water: the lane is dry.</summary>
        None = 0,

        /// <summary>Shallow water (depth class 1).</summary>
        Shallow = 1,

        /// <summary>Deep water (depth class 2).</summary>
        Deep = 2,
    }

    /// <summary>The facts about a fight that are not in the armies. Immutable.</summary>
    public readonly struct FightContext
    {
        /// <summary>True when side A's ranged ground shots go uphill (the damage table's uphill multiplier applies to A's attacks). Unit: boolean.</summary>
        public bool AUphill { get; }

        /// <summary>Water on the lane. Range: <see cref="FightWater.None"/>, Shallow or Deep.</summary>
        public FightWater Water { get; }

        /// <summary>Which side's team owns the water: -1 neutral, 0 side A, 1 side B. Unit: side index. Range: -1 to 1.</summary>
        public int WaterSide { get; }

        /// <summary>Distance between the two armies at the start, or -1 for the predictor's default rule. Unit: tiles (fixed-point). Range: -1, or 0 upward.</summary>
        public Fix64 GapTiles { get; }

        /// <summary>Stores the context.</summary>
        /// <param name="aUphill">True when side A shoots uphill.</param>
        /// <param name="water">Water on the lane.</param>
        /// <param name="waterSide">Owner of the water: -1 neutral, 0 side A, 1 side B. Unit: side index.</param>
        /// <param name="gapTiles">Start gap, or -1 for the default. Unit: tiles.</param>
        public FightContext(bool aUphill, FightWater water, int waterSide, Fix64 gapTiles)
        {
            AUphill = aUphill;
            Water = water;
            WaterSide = waterSide;
            GapTiles = gapTiles;
        }

        /// <summary>A dry lane with no gap: the armies are already engaged (the retreat test of the AI, fight_predictor_v2.md 4).</summary>
        /// <param name="aUphill">True when side A shoots uphill.</param>
        public static FightContext Engaged(bool aUphill)
        {
            return new FightContext(aUphill, FightWater.None, -1, Fix64.Zero);
        }

        /// <summary>A dry lane with the default gap: what the callers of the original <c>Predict</c> signature mean.</summary>
        /// <param name="aUphill">True when side A shoots uphill.</param>
        public static FightContext Dry(bool aUphill)
        {
            return new FightContext(aUphill, FightWater.None, -1, -Fix64.One);
        }
    }
}

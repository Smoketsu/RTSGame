// rts/src/Sim/Data/Tuning/SupplyTuning.cs
// Typed view of rts/data/tuning/supply.json. Part of Rts.Sim.Data. Supply cap and static defence limits. Read by the economy supply simulation system. Part of rts/data/tuning; loaded by the data loader (rts/src/Sim/Data) at match start. Every value is a starting value for playtesting from the Game Design Proposal (26 Oct 2026).
// Generated once from the data file's comments and then maintained by hand: when a key is added to the JSON file, add
// the property here with the same Unit, Range and Source text, or the loader reports the key as unknown.
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Supply cap and static defence supply cost and count limits that the economy supply simulation reads at match start. Loaded from "tuning/supply.json". Immutable. Durations are Fix64 seconds with a matching <c>...Ticks(MatchClock)</c> method.</summary>
    public sealed class SupplyTuning
    {
        /// <summary>Data-root-relative path of the file this class is read from.</summary>
        public const string FilePath = "tuning/supply.json";

        /// <summary>Maximum supply a player can use. Unit: supply. Range: 50 to 300. Source: Economy / Supply cap.</summary>
        public int SupplyCap { get; }

        /// <summary>Supply consumed by each static defence. Unit: supply per static defence. Range: 0 to 10. Source: Economy / Supply cap.</summary>
        public int StaticDefenceSupplyCost { get; }

        /// <summary>Maximum number of static defences per base. Unit: static defences per base (count). Range: 0 to 20. Source: Economy / Supply cap.</summary>
        public int StaticDefencesPerBaseMax { get; }

        /// <summary>Reads and validates every key of the file. Unknown keys, missing keys, wrong types and out-of-range values throw <see cref="DataValidationException"/> naming the file and key.</summary>
        /// <param name="root">The parsed file (root must be an object).</param>
        public SupplyTuning(JsonValue root)
        {
            root.RejectUnknownKeys("supply_cap", "static_defence_supply_cost", "static_defences_per_base_max");
            SupplyCap = root.RequireInt("supply_cap", 50, 300);
            StaticDefenceSupplyCost = root.RequireInt("static_defence_supply_cost", 0, 10);
            StaticDefencesPerBaseMax = root.RequireInt("static_defences_per_base_max", 0, 20);
        }
    }
}

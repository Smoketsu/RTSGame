// rts/tools/BalanceHarness/PlayerLedger.cs
// The spending account of one player as the harness reads it (spec: rts/docs/specs/balance_measurement.md M.4.2): the cost of every accepted train_unit and build_structure command and the whole cost of every accepted build_wall_line (cost x tiles kept, class wall) less the refunds of cancelled
// build orders, in total and split by the class of the definition. One class used by both GameTrace (its "spent=" totals) and SummaryObserver, so the two cannot drift apart. Tool-side only; it only adds
// up numbers it is given and never touches a simulation. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Net spending of one player, in total and by definition class.</summary>
    public sealed class PlayerLedger
    {
        /// <summary>Class of an HQ definition.</summary>
        public const string ClassHq = "hq";

        /// <summary>Class of a worker (can gather, no footprint).</summary>
        public const string ClassWorker = "worker";

        /// <summary>Class of any other mobile unit.</summary>
        public const string ClassArmy = "army";

        /// <summary>Class of a static defence structure.</summary>
        public const string ClassStaticDefence = "static_defence";

        /// <summary>Class of a wall or gate segment.</summary>
        public const string ClassWall = "wall";

        /// <summary>Class of a drop-off structure that is not an HQ.</summary>
        public const string ClassDropOff = "drop_off";

        /// <summary>Class of any other structure.</summary>
        public const string ClassStructure = "structure";

        /// <summary>Class of a research item (research_and_unlocks.md R2.3).</summary>
        public const string ClassResearch = "research";

        /// <summary>Common resource spent, less refunds. Unit: resource units. Range: 0 upward.</summary>
        public int SpentCommon { get; private set; }

        /// <summary>Rare resource spent, less refunds. Unit: resource units. Range: 0 upward.</summary>
        public int SpentRare { get; private set; }

        /// <summary>Net spending by class as [common, rare], one entry for each of the eight classes, in ordinal class order. Unit: resource units. Range: 0 upward each.</summary>
        public SortedDictionary<string, int[]> ByClass { get; } = new SortedDictionary<string, int[]>(StringComparer.Ordinal)
        {
            [ClassArmy] = new int[2], [ClassDropOff] = new int[2], [ClassHq] = new int[2], [ClassStaticDefence] = new int[2],
            [ClassResearch] = new int[2], [ClassStructure] = new int[2], [ClassWall] = new int[2], [ClassWorker] = new int[2],
        };

        /// <summary>The class of a definition: hq, worker, army, static_defence, wall (wall or gate), drop_off, structure; the first that applies in that order of the spec.</summary>
        /// <param name="definition">A unit or structure definition.</param>
        public static string ClassOf(UnitDefinition definition)
        {
            if (definition.Hq != null)
            {
                return ClassHq;
            }
            if (definition.Economy.FootprintTiles <= 0)
            {
                return definition.Economy.CanGather ? ClassWorker : ClassArmy;
            }
            if (definition.Economy.IsStaticDefence)
            {
                return ClassStaticDefence;
            }
            if (definition.Economy.IsWall || definition.Economy.IsGate)
            {
                return ClassWall;
            }
            return definition.Economy.IsDropOff ? ClassDropOff : ClassStructure;
        }

        /// <summary>Adds the cost of an accepted train or build command.</summary>
        /// <param name="definition">The definition ordered.</param>
        public void Spend(UnitDefinition definition)
        {
            SpentCommon += definition.CostCommon;
            SpentRare += definition.CostRare;
            int[] split = ByClass[ClassOf(definition)];
            split[0] += definition.CostCommon;
            split[1] += definition.CostRare;
        }

        /// <summary>Adds an amount to the totals and to one class (an accepted build_wall_line, whose cost is the whole line: ai_mechanic_use.md M3.3).</summary>
        /// <param name="spendClass">One of the Class constants. Range: a key of <see cref="ByClass"/>.</param>
        /// <param name="common">Common spent. Unit: resource units. Range: 0 upward.</param>
        /// <param name="rare">Rare spent. Unit: resource units. Range: 0 upward.</param>
        public void SpendAmount(string spendClass, int common, int rare)
        {
            SpentCommon += common;
            SpentRare += rare;
            // Running totals of this class: [0] common, [1] rare. Unit: resource units. Range: 0 upward.
            int[] split = ByClass[spendClass];
            split[0] += common;
            split[1] += rare;
        }

        /// <summary>Adds the cost of an accepted research order to the totals and to the research class.</summary>
        /// <param name="research">The research item ordered.</param>
        public void SpendResearch(ResearchDefinition research)
        {
            SpentCommon += research.CostCommon;
            SpentRare += research.CostRare;
            int[] split = ByClass[ClassResearch];
            split[0] += research.CostCommon;
            split[1] += research.CostRare;
        }

        /// <summary>Takes a refund back out of the totals, and out of the class of <paramref name="definition"/> when it is known.</summary>
        /// <param name="definition">The definition of the cancelled order, or null when unknown (only the totals change).</param>
        /// <param name="common">Common refunded. Unit: resource units. Range: 0 upward.</param>
        /// <param name="rare">Rare refunded. Unit: resource units. Range: 0 upward.</param>
        public void Refund(UnitDefinition? definition, int common, int rare)
        {
            SpentCommon -= common;
            SpentRare -= rare;
            if (definition != null)
            {
                int[] split = ByClass[ClassOf(definition)];
                split[0] -= common;
                split[1] -= rare;
            }
        }
    }
}

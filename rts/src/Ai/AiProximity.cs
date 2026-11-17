// rts/src/Ai/AiProximity.cs
// A coarse grid over a list of units so "which of them stand within r tiles of this point" costs the units near the point, not the whole
// list (skirmish_ai_v3.md Package D keeps the per-think cost bounded: no pair loop over the army). Part of Rts.Ai. Built once per think for
// one list and one largest query radius; queries return list positions in ascending order, so callers that walk the result get the
// same order as a walk over the list. Integers and Fix64 only; cells live in a SortedDictionary, so nothing depends on hash order.
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Ai
{
    /// <summary>A grid index over a fixed list of unit views. Immutable once built; one instance per think and list.</summary>
    internal sealed class AiProximity
    {
        /// <summary>Cells per row of the key (a key is row x this + column). Fixed encoding, not tuning. Unit: cells. Range: above the widest map in cells.</summary>
        private const int RowStride = 4096;

        /// <summary>The indexed units, in the order given (ascending ID for every caller).</summary>
        private readonly IReadOnlyList<AiUnitView> _units;

        /// <summary>Side of one cell. Unit: tiles. Range: 1 upward (the largest query radius rounded up).</summary>
        private readonly int _cellTiles;

        /// <summary>List positions per cell key, each list ascending.</summary>
        private readonly SortedDictionary<int, List<int>> _cells = new SortedDictionary<int, List<int>>();

        /// <summary>Indexes <paramref name="units"/> for queries of at most <paramref name="maxRadiusTiles"/>.</summary>
        /// <param name="units">The units to index (positions are read once).</param>
        /// <param name="maxRadiusTiles">Largest radius that will be queried. Unit: tiles. Range: 0 upward.</param>
        public AiProximity(IReadOnlyList<AiUnitView> units, Fix64 maxRadiusTiles)
        {
            _units = units;
            _cellTiles = System.Math.Max(1, maxRadiusTiles.CeilingToInt());
            // List position. Range: 0 to units.Count - 1.
            for (int i = 0; i < units.Count; i++)
            {
                // Cell key of this unit.
                int key = KeyOf(units[i].Position.X.FloorToInt() / _cellTiles, units[i].Position.Y.FloorToInt() / _cellTiles);
                if (!_cells.TryGetValue(key, out List<int>? list))
                {
                    list = new List<int>();
                    _cells.Add(key, list);
                }
                list.Add(i);
            }
        }

        /// <summary>The cell key of a cell column and row. Unit: key. Range: 0 upward for on-map cells.</summary>
        private static int KeyOf(int column, int row)
        {
            return row * RowStride + column;
        }

        /// <summary>
        /// Appends to <paramref name="result"/> the units whose centre lies within <paramref name="radiusTiles"/> of <paramref name="point"/>,
        /// in ascending list position. The radius must not exceed the radius the index was built for.
        /// </summary>
        /// <param name="point">Query point. Unit: tiles.</param>
        /// <param name="radiusTiles">Query radius. Unit: tiles. Range: 0 to the build radius.</param>
        /// <param name="result">Receives the matches (not cleared first).</param>
        public void Near(FixVec2 point, Fix64 radiusTiles, List<AiUnitView> result)
        {
            // Cell of the point. Unit: cells.
            int column = point.X.FloorToInt() / _cellTiles;
            int row = point.Y.FloorToInt() / _cellTiles;
            // List positions found in the nine cells around the point, sorted before use. Unit: list positions.
            List<int> found = new List<int>();
            // Row offset and column offset of the neighbour cell. Range: -1 to 1.
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (column + dx >= 0 && column + dx < RowStride && row + dy >= 0 && _cells.TryGetValue(KeyOf(column + dx, row + dy), out List<int>? list))
                    {
                        found.AddRange(list);
                    }
                }
            }
            found.Sort();
            // Squared query radius. Unit: squared tiles.
            Fix64 radiusSquared = radiusTiles * radiusTiles;
            foreach (int index in found)
            {
                if ((_units[index].Position - point).LengthSquared() <= radiusSquared)
                {
                    result.Add(_units[index]);
                }
            }
        }

        /// <summary>True when some indexed unit lies within <paramref name="radiusTiles"/> of <paramref name="point"/>.</summary>
        /// <param name="point">Query point. Unit: tiles.</param>
        /// <param name="radiusTiles">Query radius. Unit: tiles. Range: 0 to the build radius.</param>
        public bool AnyNear(FixVec2 point, Fix64 radiusTiles)
        {
            // Matches of the query. Unit: units.
            List<AiUnitView> hits = new List<AiUnitView>();
            Near(point, radiusTiles, hits);
            return hits.Count > 0;
        }
    }
}

// rts/src/Sim/Pathing/FlowField.cs
// One flow field: for a movement layer and a goal tile, the integration cost of every tile and the direction
// each tile points in. Part of Rts.Sim.Pathing (ADR-007). Built by Dijkstra over the terrain grid with integer
// costs so the result is identical on every machine; not hashed because it is a pure function of terrain.
using System;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Pathing
{
    /// <summary>Integration and flow data for one (layer, goal) pair at one terrain version.</summary>
    public sealed class FlowField
    {
        /// <summary>Integration value meaning "unreachable from the goal".</summary>
        public const int Unreachable = int.MaxValue;

        /// <summary>Flow direction meaning "no next tile" (the goal itself, or unreachable).</summary>
        public const byte NoDirection = 255;

        /// <summary>Cost units per tile at speed multiplier 1.0. Unit: cost. A tile at multiplier m costs CostScale / m.</summary>
        public const int CostScale = 1024;

        /// <summary>Movement layer the field was built for.</summary>
        public MovementLayer Layer { get; }

        /// <summary>Goal tile index. Range: 0 to tile count - 1.</summary>
        public int GoalIndex { get; }

        /// <summary>Terrain version the field was built against.</summary>
        public int TerrainVersion { get; }

        /// <summary>Order in which the cache created this field; the smallest is evicted first. Range: 0 upward.</summary>
        public long CreationSequence { get; }

        /// <summary>Integration cost per tile index; <see cref="Unreachable"/> where the goal cannot be reached.</summary>
        private readonly int[] _integration;

        /// <summary>Direction (0 to 7, see TerrainGrid.NeighbourDx) each tile points in, or <see cref="NoDirection"/>.</summary>
        private readonly byte[] _flow;

        /// <summary>Builds the field now. Cost: one Dijkstra over the whole grid.</summary>
        /// <param name="grid">Terrain to path over.</param>
        /// <param name="layer">Movement layer.</param>
        /// <param name="goalIndex">Goal tile index.</param>
        /// <param name="creationSequence">Cache creation counter.</param>
        /// <param name="heap">Scratch heap shared by the cache.</param>
        internal FlowField(TerrainGrid grid, MovementLayer layer, int goalIndex, long creationSequence, MinHeap heap)
        {
            Layer = layer;
            GoalIndex = goalIndex;
            TerrainVersion = grid.Version;
            CreationSequence = creationSequence;
            _integration = new int[grid.TileCount];
            _flow = new byte[grid.TileCount];
            Build(grid, heap);
        }

        /// <summary>Integration cost at a tile; <see cref="Unreachable"/> when the goal cannot be reached from it.</summary>
        public int IntegrationAt(int index)
        {
            return _integration[index];
        }

        /// <summary>True when the goal is reachable from the tile.</summary>
        public bool IsReachable(int index)
        {
            return _integration[index] != Unreachable;
        }

        /// <summary>Direction a unit on the tile should step in (0 to 7), or <see cref="NoDirection"/> at the goal or when unreachable.</summary>
        public byte DirectionAt(int index)
        {
            return _flow[index];
        }

        /// <summary>Per-tile traversal cost on this layer: CostScale / speed multiplier, at least 1; int.MaxValue when not standable.</summary>
        private static int TileCost(TerrainGrid grid, int index, MovementLayer layer)
        {
            if (!grid.IsStandable(index, layer))
            {
                return int.MaxValue;
            }
            // Cost as fixed-point: CostScale divided by the multiplier (0.1 to 2), so 512 to 10240.
            int cost = (Core.Fix64.FromInt(CostScale) / grid.SpeedMultiplier(index, layer)).FloorToInt();
            return cost < 1 ? 1 : cost;
        }

        /// <summary>Dijkstra from the goal, then the flow pass.</summary>
        private void Build(TerrainGrid grid, MinHeap heap)
        {
            // Tile index being reset. Unit: tile index. Range: 0 to tile count - 1.
            for (int i = 0; i < _integration.Length; i++)
            {
                _integration[i] = Unreachable;
                _flow[i] = NoDirection;
            }
            heap.Clear();
            if (grid.IsStandable(GoalIndex, Layer))
            {
                _integration[GoalIndex] = 0;
                heap.Push(((long)0 << 32) | (uint)GoalIndex);
            }
            else
            {
                // A blocked goal (a structure footprint): the field leads to its standable neighbours at cost 0, in
                // neighbour order, so units approach the structure and stop beside it (ADR-007 follow-up from review).
                for (int direction = 0; direction < TerrainGrid.DirectionCount; direction++)
                {
                    // Tile index next to the blocked goal in this direction, or -1 off the map. Unit: tile index. Range: -1 or 0 to tile count - 1.
                    int neighbour = grid.Neighbour(GoalIndex, direction);
                    if (neighbour >= 0 && grid.IsStandable(neighbour, Layer))
                    {
                        _integration[neighbour] = 0;
                        heap.Push(((long)0 << 32) | (uint)neighbour);
                    }
                }
            }
            while (heap.Count > 0)
            {
                // Packed (cost, index) of the cheapest pending tile.
                long key = heap.PopMin();
                // Integration cost half of the packed key. Unit: cost (CostScale per straight step). Range: 0 to Unreachable - 1.
                int cost = (int)(key >> 32);
                // Tile index half of the packed key. Unit: tile index. Range: 0 to tile count - 1.
                int index = (int)(key & 0xFFFFFFFFL);
                if (cost != _integration[index])
                {
                    continue;
                }
                // Direction being relaxed. Unit: direction index. Range: 0 to 7 (TerrainGrid.DirectionCount - 1).
                for (int direction = 0; direction < TerrainGrid.DirectionCount; direction++)
                {
                    // Tile index next to the popped tile in this direction, or -1 off the map. Unit: tile index. Range: -1 or 0 to tile count - 1.
                    int neighbour = grid.Neighbour(index, direction);
                    if (neighbour < 0 || !grid.IsStepPassable(neighbour, index, Layer))
                    {
                        continue;
                    }
                    // Cost of entering the neighbour tile. Unit: cost. Range: 1 upward, int.MaxValue when it cannot be entered.
                    int stepCost = TileCost(grid, neighbour, Layer);
                    if (stepCost == int.MaxValue)
                    {
                        continue;
                    }
                    // Diagonal steps cost 1.5× (3/2 in integers); straight steps 1×. Integer math only.
                    long tentative = cost + (direction >= 4 ? (long)stepCost * 3 / 2 : stepCost);
                    if (tentative >= Unreachable)
                    {
                        continue;
                    }
                    if ((int)tentative < _integration[neighbour])
                    {
                        _integration[neighbour] = (int)tentative;
                        heap.Push((tentative << 32) | (uint)neighbour);
                    }
                }
            }
            // Tile whose flow direction is chosen. Unit: tile index. Range: 0 to tile count - 1.
            for (int index = 0; index < _integration.Length; index++)
            {
                if (_integration[index] == Unreachable || _integration[index] == 0)
                {
                    // The goal (or, for a blocked goal, each seeded neighbour) is an end point and points nowhere.
                    continue;
                }
                // Best neighbour integration seen so far and its direction; ties keep the earlier direction.
                int best = _integration[index];
                // Direction of the cheapest neighbour found so far. Unit: direction index. Range: 0 to 7, or NoDirection.
                byte bestDirection = NoDirection;
                // Direction being compared. Unit: direction index. Range: 0 to 7.
                for (int direction = 0; direction < TerrainGrid.DirectionCount; direction++)
                {
                    // Tile index next to this tile in this direction, or -1 off the map. Unit: tile index. Range: -1 or 0 to tile count - 1.
                    int neighbour = grid.Neighbour(index, direction);
                    if (neighbour < 0 || !grid.IsStepPassable(index, neighbour, Layer))
                    {
                        continue;
                    }
                    if (_integration[neighbour] < best)
                    {
                        best = _integration[neighbour];
                        bestDirection = (byte)direction;
                    }
                }
                _flow[index] = bestDirection;
            }
        }
    }
}

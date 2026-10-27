// rts/src/Sim/Events/TerrainEvents.cs
// Events raised by the terrain grid and the pathfinder. Part of Rts.Sim.Events.
// Renderers update visuals from TerrainChangedEvent; the pathfinding cache reads TerrainGrid.Version instead.
using Rts.Sim.Core;
using Rts.Sim.Terrain;

namespace Rts.Sim.Events
{
    /// <summary>One tile changed (depth, height, surface or blockers). Emitted once per changed tile.</summary>
    public sealed class TerrainChangedEvent : SimEvent
    {
        /// <summary>Index of the changed tile (y × width + x). Range: 0 to tile count - 1.</summary>
        public int TileIndex { get; }

        /// <summary>The tile's new value.</summary>
        public TerrainTile Tile { get; }

        /// <summary>Builds the event from its fields.</summary>
        public TerrainChangedEvent(int tick, int tileIndex, TerrainTile tile) : base(tick)
        {
            TileIndex = tileIndex;
            Tile = tile;
        }

        /// <summary>Machine-readable kind name: "terrain_changed".</summary>
        public override string Kind => "terrain_changed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => "Tile " + TileIndex + " changed: height " + Tile.Height + ", depth " + Tile.WaterDepth + ", surface " + Tile.Surface + ", blockers " + Tile.Blockers + ".";
    }

    /// <summary>A unit's move goal could not be reached from its tile; the order was cancelled.</summary>
    public sealed class MoveUnreachableEvent : SimEvent
    {
        /// <summary>The unit whose order was cancelled.</summary>
        public EntityId Unit { get; }

        /// <summary>The goal it could not reach, in tiles.</summary>
        public FixVec2 Goal { get; }

        /// <summary>Builds the event from its fields.</summary>
        public MoveUnreachableEvent(int tick, EntityId unit, FixVec2 goal) : base(tick)
        {
            Unit = unit;
            Goal = goal;
        }

        /// <summary>Machine-readable kind name: "move_unreachable".</summary>
        public override string Kind => "move_unreachable";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " cannot reach " + Goal + "; move cancelled.";
    }
}

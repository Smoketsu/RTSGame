// rts/src/Sim/Units/MovementLayer.cs
// The three movement layers from the design doc: ground, water and air.
// Part of Rts.Sim.Units. Pathfinding (Phase 1) keeps one flow field per layer; a Grounded flyer moves
// to the ground layer for the duration of the status.
namespace Rts.Sim.Units
{
    /// <summary>Which pathing layer a unit uses. Values are stable because they appear in data files and hashes.</summary>
    public enum MovementLayer : byte
    {
        /// <summary>Walks on terrain; blocked by walls and cliffs, slowed by floods.</summary>
        Ground = 0,

        /// <summary>Moves through flooded tiles; faster on deep water (water faction units).</summary>
        Water = 1,

        /// <summary>Flies over everything until Grounded.</summary>
        Air = 2,
    }
}

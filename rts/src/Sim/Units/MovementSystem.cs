// rts/src/Sim/Units/MovementSystem.cs
// Phase 0 movement: every unit with a move target steps straight toward it by speed × seconds-per-tick.
// Part of Rts.Sim.Units. Phase 1 replaces the straight line with flow-field pathfinding per movement
// layer and terrain speed modifiers; the per-tick step and arrival event stay the same.
using Rts.Sim.Core;
using Rts.Sim.Events;

namespace Rts.Sim.Units
{
    /// <summary>Moves units toward their targets once per tick, in ascending ID order.</summary>
    public static class MovementSystem
    {
        /// <summary>Runs one tick of movement for every unit in <paramref name="sim"/>. Internal: only <see cref="Simulation.Step"/> calls it.</summary>
        internal static void Step(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (!unit.HasMoveTarget)
                {
                    continue;
                }
                // Distance this unit covers in one tick, in tiles.
                Fix64 stepLength = sim.Clock.PerSecondToPerTick(unit.Definition.SpeedTilesPerSecond);
                // Where the unit is after this tick; equals the target exactly when it arrives.
                FixVec2 next = unit.Position.MoveTowards(unit.MoveTarget, stepLength);
                unit.SetPosition(next);
                if (next == unit.MoveTarget)
                {
                    unit.ClearMoveTarget();
                    sim.Events.Add(new UnitArrivedEvent(sim.Clock.Tick, unit.Id, next));
                }
            }
        }
    }
}

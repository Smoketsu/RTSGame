// rts/src/Sim/Units/MovementSystem.cs
// Phase 1 movement: ground and water units follow flow fields over the terrain grid; air units fly straight.
// Part of Rts.Sim.Units (ADR-007). Runs once per tick inside Simulation.Step in ascending entity ID order.
// Speed each tick = base speed × terrain multiplier (tile, effective layer) × status multiplier × every
// registered ISpeedModifier (ascending Id), converted to a per-tick step with the match clock.
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Pathing;
using Rts.Sim.Terrain;

namespace Rts.Sim.Units
{
    /// <summary>Moves units toward their targets once per tick.</summary>
    public static class MovementSystem
    {
        /// <summary>1 / sqrt(2) as fixed-point, the component of a diagonal unit vector. Computed once from Fix64.Sqrt, no floats.</summary>
        private static readonly Fix64 DiagonalComponent = Fix64.Sqrt(Fix64.Half);

        /// <summary>Unit vectors for the eight flow directions in TerrainGrid order N, E, S, W, NE, SE, SW, NW.</summary>
        private static readonly FixVec2[] DirectionVectors =
        {
            new FixVec2(Fix64.Zero, Fix64.One),
            new FixVec2(Fix64.One, Fix64.Zero),
            new FixVec2(Fix64.Zero, -Fix64.One),
            new FixVec2(-Fix64.One, Fix64.Zero),
            new FixVec2(DiagonalComponent, DiagonalComponent),
            new FixVec2(DiagonalComponent, -DiagonalComponent),
            new FixVec2(-DiagonalComponent, -DiagonalComponent),
            new FixVec2(-DiagonalComponent, DiagonalComponent),
        };

        /// <summary>Runs one tick of movement for every unit in <paramref name="sim"/>. Internal: only <see cref="Simulation.Step"/> calls it.</summary>
        internal static void Step(Simulation sim)
        {
            TerrainGrid terrain = sim.Terrain;
            // Squared arrival radius, compared against squared distances so no square root is needed.
            Fix64 arrivalRadius = sim.Tuning.Terrain.ArrivalRadiusTiles;
            Fix64 arrivalRadiusSquared = arrivalRadius * arrivalRadius;
            foreach (Unit unit in sim.Units.All)
            {
                if (!unit.HasMoveTarget)
                {
                    continue;
                }
                // Layer the unit moves on this tick (Grounded flyers use Ground).
                MovementLayer layer = unit.EffectiveLayer;
                // Tile the unit stands on now.
                int currentTile = terrain.TileOf(unit.Position);
                // Distance covered this tick. Unit: tiles. Range: 0 upward.
                Fix64 stepLength = StepLength(sim, unit, currentTile, layer);
                // Goal clamped into the map so an edge target is still reachable.
                FixVec2 goal = terrain.ClampToMap(unit.MoveTarget);
                // Position after this tick.
                FixVec2 next;
                if (layer == MovementLayer.Air)
                {
                    next = unit.Position.MoveTowards(goal, stepLength);
                }
                else
                {
                    // Tile containing the goal; the flow field is built for it.
                    int goalTile = terrain.TileOf(goal);
                    FlowField field = sim.Pathing.Get(layer, goalTile);
                    if (!field.IsReachable(currentTile))
                    {
                        unit.ClearMoveTarget();
                        sim.Events.Add(new MoveUnreachableEvent(sim.Clock.Tick, unit.Id, unit.MoveTarget));
                        continue;
                    }
                    if (currentTile == goalTile)
                    {
                        next = unit.Position.MoveTowards(goal, stepLength);
                    }
                    else
                    {
                        // Direction to the next tile along the field; NoDirection cannot happen on a reachable non-goal tile.
                        byte direction = field.DirectionAt(currentTile);
                        // Step along the direction's unit vector rather than toward the tile centre, so open-ground
                        // movement stays straight; the no-corner-cutting rule keeps the unit's row and column passable.
                        next = unit.Position + DirectionVectors[direction] * stepLength;
                    }
                }
                unit.SetPosition(terrain.ClampToMap(next));
                // Squared distance left to the goal, for the arrival test.
                Fix64 remainingSquared = (goal - unit.Position).LengthSquared();
                if (remainingSquared <= arrivalRadiusSquared)
                {
                    unit.ClearMoveTarget();
                    sim.Events.Add(new UnitArrivedEvent(sim.Clock.Tick, unit.Id, unit.Position));
                }
            }
        }

        /// <summary>Per-tick step for a unit: base speed × terrain × status × modifiers, converted with the clock. Unit: tiles per tick.</summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="unit">The moving unit.</param>
        /// <param name="tileIndex">Tile the unit stands on.</param>
        /// <param name="layer">Its effective layer.</param>
        public static Fix64 StepLength(Simulation sim, Unit unit, int tileIndex, MovementLayer layer)
        {
            // Speed in tiles per second after every multiplier.
            Fix64 speed = unit.Definition.SpeedTilesPerSecond * sim.Terrain.SpeedMultiplier(tileIndex, layer) * unit.StatusSpeedMultiplier;
            foreach (ISpeedModifier modifier in sim.SpeedModifiers)
            {
                speed = speed * modifier.SpeedMultiplier(sim, unit);
            }
            return sim.Clock.PerSecondToPerTick(speed);
        }
    }
}

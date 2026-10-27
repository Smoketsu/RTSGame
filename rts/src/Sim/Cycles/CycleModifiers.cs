// rts/src/Sim/Cycles/CycleModifiers.cs
// The two modifiers the cycle system registers at match start: a speed modifier (Windstorm: flyers move faster) and a damage
// modifier (Core surge: attackers near the Core hit harder). Both have the Id "cycle", are registered whether or not the match has
// cycles (so the hashed modifier ID lists never depend on data), and are pure reads of the active cycle. Part of Rts.Sim.Cycles
// (spec: rts/docs/specs/planetary_cycles.md section 4, Modifiers). They never change simulation state.
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Cycles
{
    /// <summary>Speed multiplier of the active cycle for units flying (effective layer Air).</summary>
    internal sealed class CycleSpeedModifier : ISpeedModifier
    {
        /// <summary>The cycle system whose active cycle is read.</summary>
        private readonly CycleSystem _cycles;

        /// <summary>Wraps the cycle system.</summary>
        /// <param name="cycles">The match's cycle system; not null.</param>
        public CycleSpeedModifier(CycleSystem cycles)
        {
            _cycles = cycles;
        }

        /// <summary>Stable modifier ID; fixes the multiplication order (before "economy.carrying" and "momentum"). Range: fixed text "cycle".</summary>
        public string Id => "cycle";

        /// <summary>The active cycle's air speed multiplier for an airborne unit, else 1. Unit: multiplier of base speed. Range: 0.5 to 2.</summary>
        public Fix64 SpeedMultiplier(Simulation sim, Unit unit)
        {
            // The cycle in force, or null before the first shift or when the match has no cycles.
            CycleDefinition? active = _cycles.ActiveCycle;
            if (active == null || unit.EffectiveLayer != MovementLayer.Air)
            {
                return Fix64.One;
            }
            return active.Effects.AirSpeedMultiplier;
        }
    }

    /// <summary>Damage multiplier of the active cycle for attackers whose centre is inside the Core effect radius.</summary>
    internal sealed class CycleDamageModifier : IDamageModifier
    {
        /// <summary>The cycle system whose active cycle is read.</summary>
        private readonly CycleSystem _cycles;

        /// <summary>Wraps the cycle system.</summary>
        /// <param name="cycles">The match's cycle system; not null.</param>
        public CycleDamageModifier(CycleSystem cycles)
        {
            _cycles = cycles;
        }

        /// <summary>Stable modifier ID; fixes the multiplication order (before "momentum"). Range: fixed text "cycle".</summary>
        public string Id => "cycle";

        /// <summary>The active cycle's Core damage multiplier when the map has a Core and the attacker stands within the effect radius, else 1. Unit: damage multiplier. Range: 1 to 3.</summary>
        public Fix64 OutgoingDamageMultiplier(Simulation sim, Unit attacker)
        {
            // The cycle in force, or null before the first shift or when the match has no cycles.
            CycleDefinition? active = _cycles.ActiveCycle;
            // The Core of the map, or null for a map without one.
            CoreArea? core = sim.Map.Core;
            if (active == null || core == null || active.Effects.CoreDamageMultiplier == Fix64.One)
            {
                return Fix64.One;
            }
            // Effect radius. Unit: tiles. Range: 0 to 32.
            Fix64 radius = active.Effects.CoreEffectRadiusTiles;
            // Squared distance from the attacker to the Core centre. Unit: squared tiles.
            Fix64 distanceSquared = (attacker.Position - core.Centre).LengthSquared();
            return distanceSquared <= radius * radius ? active.Effects.CoreDamageMultiplier : Fix64.One;
        }
    }
}

// rts/src/Sim/Units/ISpeedModifier.cs
// Hook for mechanics that scale a unit's movement speed (Skai Momentum). Part of Rts.Sim.Units.
// Registered with Simulation.RegisterSpeedModifier at OnMatchStart; MovementSystem multiplies a unit's speed by
// every registered modifier in ascending Id order, after the terrain and status multipliers.
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>A deterministic per-unit speed multiplier provider.</summary>
    public interface ISpeedModifier
    {
        /// <summary>Stable ID such as "momentum"; fixes the multiplication order.</summary>
        string Id { get; }

        /// <summary>Multiplier for <paramref name="unit"/> this tick. Unit: multiplier of base speed. Range: 0 to 2; return 1 for no effect.</summary>
        Fix64 SpeedMultiplier(Simulation sim, Unit unit);
    }
}

// rts/src/Sim/Combat/IDamageModifier.cs
// Hook that lets a faction mechanic scale the damage its units deal. Part of Rts.Sim.Combat. The Skai Momentum
// mechanic will register one at OnMatchStart; the core combat system never names a faction (architecture rule 4).
// Modifiers are applied in ascending Id order (ordinal) so the result never depends on registration order.
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Sim.Combat
{
    /// <summary>Scales the damage an attacker deals. Registered with <see cref="Simulation.RegisterDamageModifier"/> during <c>OnMatchStart</c>.</summary>
    public interface IDamageModifier
    {
        /// <summary>Stable ID, unique among modifiers; decides application order (ordinal). Range: non-empty text such as "momentum".</summary>
        string Id { get; }

        /// <summary>
        /// Multiplier on the damage of one hit by <paramref name="attacker"/>. Unit: fraction (1 means unchanged).
        /// Range: 0 or more. Must be deterministic and must not change simulation state.
        /// </summary>
        /// <param name="sim">The running simulation, for read-only queries.</param>
        /// <param name="attacker">The unit that is hitting.</param>
        Fix64 OutgoingDamageMultiplier(Simulation sim, Unit attacker);
    }
}

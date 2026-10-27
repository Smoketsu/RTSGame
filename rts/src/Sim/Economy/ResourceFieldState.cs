// rts/src/Sim/Economy/ResourceFieldState.cs
// The live state of one resource field: what is left in it. Part of Rts.Sim.Economy. The map (MapDefinition.ResourceFields)
// holds the starting amounts; this class holds the amount that remains as workers take loads. Hashed by ResourceFields.
using Rts.Sim.Data;

namespace Rts.Sim.Economy
{
    /// <summary>One resource field during a match.</summary>
    public sealed class ResourceFieldState
    {
        /// <summary>Position of the field in the map's field list. Range: 0 to field count - 1. Fields are always visited in this order.</summary>
        public int Index { get; }

        /// <summary>Tile X of the field. Unit: tiles. Range: 0 to map width - 1.</summary>
        public int X { get; }

        /// <summary>Tile Y of the field. Unit: tiles. Range: 0 to map height - 1.</summary>
        public int Y { get; }

        /// <summary>Common or rare.</summary>
        public ResourceKind Kind { get; }

        /// <summary>Resource units the field started with. Unit: resource units. Range: 1 to 1 000 000.</summary>
        public int Initial { get; }

        /// <summary>Resource units still in the field. Unit: resource units. Range: 0 (depleted) to <see cref="Initial"/>. Hashed.</summary>
        public int Remaining { get; private set; }

        /// <summary>True when nothing is left.</summary>
        public bool IsDepleted => Remaining <= 0;

        /// <summary>Creates the state of a field at its starting amount.</summary>
        /// <param name="index">Position in the map's field list.</param>
        /// <param name="field">The map's definition of the field.</param>
        internal ResourceFieldState(int index, ResourceField field)
        {
            Index = index;
            X = field.X;
            Y = field.Y;
            Kind = field.Kind;
            Initial = field.Amount;
            Remaining = field.Amount;
        }

        /// <summary>Takes up to <paramref name="amount"/> out of the field and returns what was actually taken (less when the field holds less).</summary>
        /// <param name="amount">Wanted resource units. Unit: resource units. Range: 0 upward.</param>
        internal int Take(int amount)
        {
            // What the field can give. Unit: resource units. Range: 0 to Remaining.
            int taken = amount < Remaining ? amount : Remaining;
            Remaining -= taken;
            return taken;
        }
    }
}

// rts/src/Sim/Data/CycleDefinition.cs
// Immutable description of a Planetary Cycle from rts/data/cycles/<id>.json: identity, the play style it favours and the
// seven effect numbers (CycleEffects) that Rts.Sim.Cycles applies while the cycle is active. Part of Rts.Sim.Data.
// Spec: rts/docs/specs/planetary_cycles.md section 2. Every effect number is data (rule 3); the cycle system never names a cycle.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Which play style a Planetary Cycle favours (design doc: cycles favour a play style, not a faction).</summary>
    public enum CycleFavour : byte
    {
        /// <summary>Favours flyers (Windstorm).</summary>
        Air = 0,

        /// <summary>Favours the player who attacks (Quake).</summary>
        Attackers = 1,

        /// <summary>Favours the player holding good ground near the Core (Core surge).</summary>
        Position = 2,
    }

    /// <summary>
    /// The numbers a cycle applies while it is active (the <c>effects</c> object of a cycle file). Immutable. A neutral set
    /// (<see cref="Neutral"/>) changes nothing. Every value is a placeholder until playtesting (spec section 2).
    /// </summary>
    public sealed class CycleEffects
    {
        /// <summary>Speed multiplier for units whose effective layer is Air. Unit: multiplier of speed. Range: 0.5 to 2; 1 means no change.</summary>
        public Fix64 AirSpeedMultiplier { get; }

        /// <summary>Added to the weapon range of mobile non-HQ surface units with a ranged weapon; never takes the range below melee range. Unit: tiles. Range: -4 to 4; 0 means no change.</summary>
        public Fix64 SurfaceRangedRangeBonusTiles { get; }

        /// <summary>Damage per second to static defences as a fraction of their maximum health. Unit: fraction of max health per second. Range: 0 to 0.05; 0 means no damage.</summary>
        public Fix64 DefenceDamagePerSecondFraction { get; }

        /// <summary>Cycle damage never takes a defence below this fraction of its maximum health, so a cycle never kills. Unit: fraction of max health. Range: 0 to 1.</summary>
        public Fix64 DefenceDamageMinHealthFraction { get; }

        /// <summary>True when quake paths of the map flip open or closed while this cycle is active. Unit: flag. Range: true or false.</summary>
        public bool TogglesQuakePaths { get; }

        /// <summary>Outgoing damage multiplier for attackers whose centre is within <see cref="CoreEffectRadiusTiles"/> of the Core centre. Unit: damage multiplier. Range: 1 to 3; 1 means no change.</summary>
        public Fix64 CoreDamageMultiplier { get; }

        /// <summary>Radius around the Core centre in which <see cref="CoreDamageMultiplier"/> applies. Unit: tiles. Range: 0 to 32.</summary>
        public Fix64 CoreEffectRadiusTiles { get; }

        /// <summary>The effects that change nothing: speed 1, range 0, no damage, floor 0.25, no path toggling, damage 1, radius 0.</summary>
        public static CycleEffects Neutral { get; } = new CycleEffects(Fix64.One, Fix64.Zero, Fix64.Zero, Fix64.Parse("0.25"), false, Fix64.One, Fix64.Zero);

        /// <summary>Builds the effect set; the loader validates the ranges, tests may pass anything.</summary>
        /// <param name="airSpeedMultiplier">Air speed multiplier. Unit: multiplier. Range: 0.5 to 2.</param>
        /// <param name="surfaceRangedRangeBonusTiles">Range bonus for surface ranged units. Unit: tiles. Range: -4 to 4.</param>
        /// <param name="defenceDamagePerSecondFraction">Defence damage rate. Unit: fraction of max health per second. Range: 0 to 0.05.</param>
        /// <param name="defenceDamageMinHealthFraction">Floor for cycle damage. Unit: fraction of max health. Range: 0 to 1.</param>
        /// <param name="togglesQuakePaths">True when quake paths flip while active.</param>
        /// <param name="coreDamageMultiplier">Damage multiplier near the Core. Unit: multiplier. Range: 1 to 3.</param>
        /// <param name="coreEffectRadiusTiles">Radius of the Core damage area. Unit: tiles. Range: 0 to 32.</param>
        public CycleEffects(Fix64 airSpeedMultiplier, Fix64 surfaceRangedRangeBonusTiles, Fix64 defenceDamagePerSecondFraction,
            Fix64 defenceDamageMinHealthFraction, bool togglesQuakePaths, Fix64 coreDamageMultiplier, Fix64 coreEffectRadiusTiles)
        {
            AirSpeedMultiplier = airSpeedMultiplier;
            SurfaceRangedRangeBonusTiles = surfaceRangedRangeBonusTiles;
            DefenceDamagePerSecondFraction = defenceDamagePerSecondFraction;
            DefenceDamageMinHealthFraction = defenceDamageMinHealthFraction;
            TogglesQuakePaths = togglesQuakePaths;
            CoreDamageMultiplier = coreDamageMultiplier;
            CoreEffectRadiusTiles = coreEffectRadiusTiles;
        }

        /// <summary>Feeds the seven values into a hash in table order (spec section 2).</summary>
        /// <param name="hasher">The hasher being filled.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(AirSpeedMultiplier);
            hasher.Write(SurfaceRangedRangeBonusTiles);
            hasher.Write(DefenceDamagePerSecondFraction);
            hasher.Write(DefenceDamageMinHealthFraction);
            hasher.Write(TogglesQuakePaths);
            hasher.Write(CoreDamageMultiplier);
            hasher.Write(CoreEffectRadiusTiles);
        }
    }

    /// <summary>One Planetary Cycle type.</summary>
    public sealed class CycleDefinition
    {
        /// <summary>Stable ID, equal to the file name without ".json", such as "windstorm". Lower-case letters, digits and underscores.</summary>
        public string Id { get; }

        /// <summary>Name shown to players. Presentation only.</summary>
        public string DisplayName { get; }

        /// <summary>Play style this cycle favours.</summary>
        public CycleFavour Favours { get; }

        /// <summary>One or two sentences describing the effect, for tooltips. Presentation only.</summary>
        public string Description { get; }

        /// <summary>The numbers applied while the cycle is active.</summary>
        public CycleEffects Effects { get; }

        /// <summary>Builds a cycle definition. <paramref name="effects"/> defaults to <see cref="CycleEffects.Neutral"/> for hand-built test cycles.</summary>
        public CycleDefinition(string id, string displayName, CycleFavour favours, string description, CycleEffects? effects = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Cycle id must not be empty.", nameof(id));
            }
            Id = id;
            DisplayName = displayName ?? id;
            Favours = favours;
            Description = description ?? string.Empty;
            Effects = effects ?? CycleEffects.Neutral;
        }

        /// <summary>Feeds the ID, the favoured play style and the effect values into a hash. Display name and description are presentation and are left out.</summary>
        /// <param name="hasher">The hasher being filled.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
            hasher.Write((byte)Favours);
            Effects.WriteHash(hasher);
        }
    }
}

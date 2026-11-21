// rts/src/Sim/Units/GroundingAbilityDefinition.cs
// Immutable description of a grounding ability (the Stormcaller's Downdraft), parsed from the optional "grounding_ability" object of a unit file
// (rts/docs/specs/combat_mechanics.md package 4). Part of Rts.Sim.Units. Read by CastGroundingCommand and StatusSystem.CastGrounding. Generic unit
// data, so the core never names a faction or the ability's player-facing name. The grounded duration is the shared grounded_duration_seconds of
// tuning/statuses.json, not a field here. Every number comes from data (rule 3).
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Units
{
    /// <summary>One grounding ability: how far it can be aimed, the pulse radius and the cooldown. Never changes during a match.</summary>
    public sealed class GroundingAbilityDefinition
    {
        /// <summary>Name of the ability shown to players. Presentation only; not hashed. Unit: text. Range: 1 to 40 characters when loaded from data; empty when the definition is built in code.</summary>
        public string DisplayName { get; }

        /// <summary>One-line description shown to players. Presentation only; not hashed. Unit: text. Range: 1 to 200 characters when loaded from data; empty when built in code.</summary>
        public string Description { get; }

        /// <summary>Farthest aimed point from the caster's centre. Unit: tiles (fixed-point). Range: 0.5 to 32.</summary>
        public Fix64 RangeTiles { get; }

        /// <summary>Radius around the aimed point; a flyer whose edge is inside it is grounded. Unit: tiles (fixed-point). Range: 0.5 to 8.</summary>
        public Fix64 RadiusTiles { get; }

        /// <summary>Time before the same unit can cast again. Unit: seconds (fixed-point). Range: 1 to 300.</summary>
        public Fix64 CooldownSeconds { get; }

        /// <summary>Smallest cast range the schema accepts. Unit: tiles. Range: fixed schema bound, 0.5.</summary>
        public static readonly Fix64 MinRangeTiles = Fix64.Half;

        /// <summary>Largest cast range the schema accepts. Unit: tiles. Range: fixed schema bound, 32.</summary>
        public static readonly Fix64 MaxRangeTiles = Fix64.FromInt(32);

        /// <summary>Smallest pulse radius the schema accepts. Unit: tiles. Range: fixed schema bound, 0.5.</summary>
        public static readonly Fix64 MinRadiusTiles = Fix64.Half;

        /// <summary>Largest pulse radius the schema accepts. Unit: tiles. Range: fixed schema bound, 8.</summary>
        public static readonly Fix64 MaxRadiusTiles = Fix64.FromInt(8);

        /// <summary>Smallest cooldown the schema accepts. Unit: seconds. Range: fixed schema bound, 1.</summary>
        public static readonly Fix64 MinCooldownSeconds = Fix64.One;

        /// <summary>Largest cooldown the schema accepts. Unit: seconds. Range: fixed schema bound, 300.</summary>
        public static readonly Fix64 MaxCooldownSeconds = Fix64.FromInt(300);

        /// <summary>Builds the ability, validating every range. The loader turns a failure into an error naming the file and field.</summary>
        /// <param name="rangeTiles">Cast range. Unit: tiles. Range: 0.5 to 32.</param>
        /// <param name="radiusTiles">Pulse radius. Unit: tiles. Range: 0.5 to 8.</param>
        /// <param name="cooldownSeconds">Cooldown. Unit: seconds. Range: 1 to 300.</param>
        /// <param name="displayName">Name shown to players. Unit: text. Range: any text; null becomes empty (the loader enforces 1 to 40 characters).</param>
        /// <param name="description">Description shown to players. Unit: text. Range: any text; null becomes empty (the loader enforces 1 to 200 characters).</param>
        public GroundingAbilityDefinition(Fix64 rangeTiles, Fix64 radiusTiles, Fix64 cooldownSeconds, string displayName = "", string description = "")
        {
            if (rangeTiles < MinRangeTiles || rangeTiles > MaxRangeTiles)
            {
                throw new ArgumentOutOfRangeException(nameof(rangeTiles), "grounding_ability.range_tiles must be 0.5 to 32.");
            }
            if (radiusTiles < MinRadiusTiles || radiusTiles > MaxRadiusTiles)
            {
                throw new ArgumentOutOfRangeException(nameof(radiusTiles), "grounding_ability.radius_tiles must be 0.5 to 8.");
            }
            if (cooldownSeconds < MinCooldownSeconds || cooldownSeconds > MaxCooldownSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), "grounding_ability.cooldown_seconds must be 1 to 300.");
            }
            RangeTiles = rangeTiles;
            RadiusTiles = radiusTiles;
            CooldownSeconds = cooldownSeconds;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
        }

        /// <summary>Feeds range, radius and cooldown into a hash in a fixed order (used by the content digest). The display name and description are left out.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(RangeTiles);
            hasher.Write(RadiusTiles);
            hasher.Write(CooldownSeconds);
        }
    }
}

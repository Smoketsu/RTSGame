// rts/src/Sim/Commands/StatusCommands.cs
// The status command: apply Grounded or Slowed to a unit from a script, a test or a campaign trigger. Part of
// Rts.Sim.Commands (spec: rts/docs/specs/statuses.md section 5). Players cannot issue it; factions apply statuses through
// their own abilities and weapons, which call StatusSystem directly.
using System;
using Rts.Sim.Core;
using Rts.Sim.Statuses;
using Rts.Sim.Units;

namespace Rts.Sim.Commands
{
    /// <summary>
    /// A unit casts its grounding ability (Downdraft) at a point: every enemy flyer whose edge is within the ability's radius is grounded at once for the
    /// shared Grounded duration (rts/docs/specs/combat_mechanics.md package 4). Script name <c>cast_grounding</c>. Rejections, in this order: unknown_unit,
    /// not_owner (the issuer is a player other than the owner; the script issuer None may command any unit), no_ability (the definition has no
    /// grounding_ability), under_construction, out_of_bounds (the point is beyond <see cref="Simulation.WorldBoundTiles"/>), out_of_range (the point is farther
    /// than range_tiles from the caster's centre) and on_cooldown (the tick is before <see cref="Unit.GroundingReadyTick"/>). Bad input is rejected, never thrown.
    /// </summary>
    public sealed class CastGroundingCommand : Command
    {
        /// <summary>Script and log name of the command kind.</summary>
        public const string KindName = "cast_grounding";

        /// <summary>The casting unit. Must exist, belong to the issuer and have a grounding ability.</summary>
        public EntityId Caster { get; }

        /// <summary>Centre of the pulse. Unit: tiles. Range: each component within the world bound and within range_tiles of the caster, else rejected.</summary>
        public FixVec2 Target { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public CastGroundingCommand(int tick, PlayerId issuer, uint sequence, EntityId caster, FixVec2 target)
            : base(tick, issuer, sequence)
        {
            Caster = caster;
            Target = target;
        }

        /// <summary>Machine-readable kind name: "cast_grounding".</summary>
        public override string Kind => KindName;

        /// <summary>Applies the command; see the class summary for the rejections.</summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (!sim.Units.TryGet(Caster, out Unit? caster))
            {
                return CommandResult.Reject("unknown_unit");
            }
            if (Issuer.IsValid && caster.Owner != Issuer)
            {
                return CommandResult.Reject("not_owner");
            }
            // The caster's ability; null when its definition has none.
            GroundingAbilityDefinition? ability = caster.Definition.GroundingAbility;
            if (ability == null)
            {
                return CommandResult.Reject("no_ability");
            }
            if (caster.IsUnderConstruction)
            {
                return CommandResult.Reject("under_construction");
            }
            if (!Simulation.IsWithinWorldBounds(Target))
            {
                return CommandResult.Reject("out_of_bounds");
            }
            if ((Target - caster.Position).LengthSquared() > ability.RangeTiles * ability.RangeTiles)
            {
                return CommandResult.Reject("out_of_range");
            }
            if (sim.Clock.Tick < caster.GroundingReadyTick)
            {
                return CommandResult.Reject("on_cooldown");
            }
            StatusSystem.CastGrounding(sim, caster, Target);
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Caster);
            hasher.Write(Target);
        }
    }

    /// <summary>
    /// Applies a status to a unit. Script name <c>apply_status</c>. Rejections, in order: status_requires_script_issuer
    /// (the issuer is not <see cref="PlayerId.None"/>), unknown_unit, invalid_duration (0 seconds or less, or 0 ticks; for
    /// Grounded a duration of 0 means "omitted" and uses grounded_duration_seconds), invalid_multiplier (Slowed outside
    /// 0 to 1, 0 excluded), not_a_flyer (Grounded on a unit whose definition is not Air).
    /// </summary>
    public sealed class ApplyStatusCommand : Command
    {
        /// <summary>Source key used when a script gives none.</summary>
        public const string DefaultSourceKey = "script";

        /// <summary>The unit to affect. Must exist.</summary>
        public EntityId Unit { get; }

        /// <summary>Which status to apply.</summary>
        public StatusKind Status { get; }

        /// <summary>Speed multiplier for Slowed. Unit: multiplier of speed. Range: above 0 and at most 1 (checked on apply); ignored for Grounded.</summary>
        public Fix64 Multiplier { get; }

        /// <summary>Length in seconds. Unit: seconds. Range: above 0 for Slowed; for Grounded 0 means "use grounded_duration_seconds"; negative values are rejected as invalid_duration.</summary>
        public Fix64 DurationSeconds { get; }

        /// <summary>Source label stored with the status. Range: 1 to 64 characters of a-z, 0-9, underscore and dot (checked at construction).</summary>
        public string SourceKey { get; }

        /// <summary>Builds the command; throws <see cref="ArgumentException"/> on a malformed source key so a bad command never reaches a step.</summary>
        public ApplyStatusCommand(int tick, PlayerId issuer, uint sequence, EntityId unit, StatusKind kind, Fix64 multiplier, Fix64 durationSeconds, string sourceKey)
            : base(tick, issuer, sequence)
        {
            SlowEntry.RequireValidKey(sourceKey, nameof(sourceKey));
            Unit = unit;
            Status = kind;
            Multiplier = multiplier;
            DurationSeconds = durationSeconds;
            SourceKey = sourceKey;
        }

        /// <summary>Machine-readable kind name: "apply_status".</summary>
        public override string Kind => "apply_status";

        /// <summary>Applies the command; see the class summary for the rejections.</summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (Issuer.IsValid)
            {
                return CommandResult.Reject("status_requires_script_issuer");
            }
            if (!sim.Units.TryGet(Unit, out Unit? unit))
            {
                return CommandResult.Reject("unknown_unit");
            }
            // Length in ticks. Unit: ticks. Range: 0 means invalid; otherwise 1 or more.
            int ticks;
            if (Status == StatusKind.Grounded && DurationSeconds == Fix64.Zero)
            {
                ticks = sim.Tuning.Status.GroundedDurationTicks(sim.Clock);
            }
            else if (DurationSeconds <= Fix64.Zero)
            {
                return CommandResult.Reject("invalid_duration");
            }
            else
            {
                ticks = sim.Clock.SecondsToTicks(DurationSeconds);
            }
            if (ticks < 1)
            {
                return CommandResult.Reject("invalid_duration");
            }
            if (Status == StatusKind.Slowed)
            {
                if (Multiplier <= Fix64.Zero || Multiplier > Fix64.One)
                {
                    return CommandResult.Reject("invalid_multiplier");
                }
                StatusSystem.ApplySlow(sim, unit, SourceKey, Multiplier, ticks, Issuer);
                return CommandResult.Ok;
            }
            if (unit.Definition.Layer != MovementLayer.Air)
            {
                return CommandResult.Reject("not_a_flyer");
            }
            return StatusSystem.ApplyGrounded(sim, unit, ticks, SourceKey, Issuer) ? CommandResult.Ok : CommandResult.Reject("not_a_flyer");
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Unit);
            hasher.Write((byte)Status);
            hasher.Write(Multiplier);
            hasher.Write(DurationSeconds);
            hasher.Write(SourceKey);
        }
    }
}

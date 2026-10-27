// rts/src/Sim/Events/StatusEvents.cs
// The events of the shared status system: a status was applied or refreshed, a status expired, a unit changed movement
// layer (Grounded landing and lift-off) and a Spoutling drench hit landed. Part of Rts.Sim.Events
// (spec: rts/docs/specs/statuses.md section 5). Removing a unit emits no status events.
using Rts.Sim.Core;
using Rts.Sim.Statuses;
using Rts.Sim.Units;

namespace Rts.Sim.Events
{
    /// <summary>A status was applied to a unit, or an active one was refreshed.</summary>
    public sealed class StatusAppliedEvent : SimEvent
    {
        /// <summary>The unit that got the status.</summary>
        public EntityId Unit { get; }

        /// <summary>Which status.</summary>
        public StatusKind Status { get; }

        /// <summary>Source key of the status, such as "drench". Range: 1 to 64 characters of a-z, 0-9, underscore and dot.</summary>
        public string SourceKey { get; }

        /// <summary>Speed multiplier of a slow. Unit: multiplier of speed. Range: above 0 and at most 1; 1 for Grounded (which does not slow).</summary>
        public Fix64 Multiplier { get; }

        /// <summary>First tick the status is no longer active. Unit: ticks. Range: above the event tick.</summary>
        public int UntilTick { get; }

        /// <summary>True when the status was already active and was refreshed instead of started.</summary>
        public bool Refreshed { get; }

        /// <summary>Builds the event from its fields.</summary>
        public StatusAppliedEvent(int tick, EntityId unit, StatusKind status, string sourceKey, Fix64 multiplier, int untilTick, bool refreshed) : base(tick)
        {
            Unit = unit;
            Status = status;
            SourceKey = sourceKey;
            Multiplier = multiplier;
            UntilTick = untilTick;
            Refreshed = refreshed;
        }

        /// <summary>Machine-readable kind name: "status_applied".</summary>
        public override string Kind => "status_applied";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " " + (Refreshed ? "refreshed " : "got ") + Status + " '" + SourceKey + "' until t" + UntilTick + ".";
    }

    /// <summary>A status ended on a unit because its time ran out.</summary>
    public sealed class StatusExpiredEvent : SimEvent
    {
        /// <summary>The unit the status ended on.</summary>
        public EntityId Unit { get; }

        /// <summary>Which status ended.</summary>
        public StatusKind Status { get; }

        /// <summary>Source key of the status that ended. Range: 1 to 64 characters.</summary>
        public string SourceKey { get; }

        /// <summary>Builds the event from its fields.</summary>
        public StatusExpiredEvent(int tick, EntityId unit, StatusKind status, string sourceKey) : base(tick)
        {
            Unit = unit;
            Status = status;
            SourceKey = sourceKey;
        }

        /// <summary>Machine-readable kind name: "status_expired".</summary>
        public override string Kind => "status_expired";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " lost " + Status + " '" + SourceKey + "'.";
    }

    /// <summary>A unit's effective movement layer changed (a flyer landed or lifted off).</summary>
    public sealed class UnitLayerChangedEvent : SimEvent
    {
        /// <summary>The unit that changed layer.</summary>
        public EntityId Unit { get; }

        /// <summary>Layer before the change.</summary>
        public MovementLayer From { get; }

        /// <summary>Layer after the change.</summary>
        public MovementLayer To { get; }

        /// <summary>Position of the unit after the change (after landing relocation). Unit: tiles.</summary>
        public FixVec2 Position { get; }

        /// <summary>Builds the event from its fields.</summary>
        public UnitLayerChangedEvent(int tick, EntityId unit, MovementLayer from, MovementLayer to, FixVec2 position) : base(tick)
        {
            Unit = unit;
            From = from;
            To = to;
            Position = position;
        }

        /// <summary>Machine-readable kind name: "unit_layer_changed".</summary>
        public override string Kind => "unit_layer_changed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " moved from layer " + From + " to " + To + " at " + Position + ".";
    }

    /// <summary>A weapon that applies drench hit an air unit.</summary>
    public sealed class DrenchHitEvent : SimEvent
    {
        /// <summary>The unit that was drenched.</summary>
        public EntityId Target { get; }

        /// <summary>The unit that fired.</summary>
        public EntityId Attacker { get; }

        /// <summary>Hits counted when this hit landed, before any reset by grounding. Unit: hits. Range: 1 to spoutling_drench_hits_to_ground.</summary>
        public int Hits { get; }

        /// <summary>True when this hit grounded the target.</summary>
        public bool Grounded { get; }

        /// <summary>Builds the event from its fields.</summary>
        public DrenchHitEvent(int tick, EntityId target, EntityId attacker, int hits, bool grounded) : base(tick)
        {
            Target = target;
            Attacker = attacker;
            Hits = hits;
            Grounded = grounded;
        }

        /// <summary>Machine-readable kind name: "drench_hit".</summary>
        public override string Kind => "drench_hit";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Attacker + " drenched " + Target + " (hit " + Hits + (Grounded ? ", grounded" : string.Empty) + ").";
    }
}

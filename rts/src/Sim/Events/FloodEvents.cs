// rts/src/Sim/Events/FloodEvents.cs
// The events of the flooding mechanic: the tide turned, a wave was cast, a wave shoved a unit, and flood water was removed by
// a flood limit. Part of Rts.Sim.Events (spec: rts/docs/specs/flooding.md section 5). The UI and the AI read the tide turning
// and the wave from these; tile changes are reported by TerrainChangedEvent as for every terrain change.
using Rts.Sim.Core;

namespace Rts.Sim.Events
{
    /// <summary>Which turning point of the tide an event reports.</summary>
    public enum TidePhase : byte
    {
        /// <summary>Low tide: the water is at its least extent (the match starts here, tick 0).</summary>
        Low = 0,

        /// <summary>High tide: the water is at its full extent (half a cycle after low tide).</summary>
        High = 1,
    }

    /// <summary>The shared tide reached low or high tide. One per turning point per match, never per team.</summary>
    public sealed class TideTurnedEvent : SimEvent
    {
        /// <summary>The turning point reached.</summary>
        public TidePhase Phase { get; }

        /// <summary>Index of the tide cycle, tick divided by the cycle length in ticks. Unit: cycles. Range: 0 upward.</summary>
        public int CycleIndex { get; }

        /// <summary>Builds the event from its fields.</summary>
        public TideTurnedEvent(int tick, TidePhase phase, int cycleIndex) : base(tick)
        {
            Phase = phase;
            CycleIndex = cycleIndex;
        }

        /// <summary>Machine-readable kind name: "tide_turned".</summary>
        public override string Kind => "tide_turned";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => (Phase == TidePhase.High ? "High" : "Low") + " tide in cycle " + CycleIndex + ".";
    }

    /// <summary>A unit cast the wave.</summary>
    public sealed class WaveCastEvent : SimEvent
    {
        /// <summary>The casting unit.</summary>
        public EntityId Caster { get; }

        /// <summary>Owner of the caster.</summary>
        public PlayerId Owner { get; }

        /// <summary>X part of the wave direction. Unit: tile step. Range: -1, 0 or 1 (not both 0).</summary>
        public sbyte DirX { get; }

        /// <summary>Y part of the wave direction. Unit: tile step. Range: -1, 0 or 1 (not both 0).</summary>
        public sbyte DirY { get; }

        /// <summary>Number of tiles that gained water. Unit: tiles. Range: 0 to wave length times wave width.</summary>
        public int TilesFlooded { get; }

        /// <summary>Builds the event from its fields.</summary>
        public WaveCastEvent(int tick, EntityId caster, PlayerId owner, sbyte dirX, sbyte dirY, int tilesFlooded) : base(tick)
        {
            Caster = caster;
            Owner = owner;
            DirX = dirX;
            DirY = dirY;
            TilesFlooded = tilesFlooded;
        }

        /// <summary>Machine-readable kind name: "wave_cast".</summary>
        public override string Kind => "wave_cast";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Caster + " cast a wave towards (" + DirX + ", " + DirY + "), " + TilesFlooded + " tiles flooded.";
    }

    /// <summary>A wave shoved a unit to another place.</summary>
    public sealed class UnitPushedEvent : SimEvent
    {
        /// <summary>The unit that was pushed.</summary>
        public EntityId Unit { get; }

        /// <summary>Where it stood. Unit: tiles.</summary>
        public FixVec2 From { get; }

        /// <summary>Where it stands now. Unit: tiles.</summary>
        public FixVec2 To { get; }

        /// <summary>Builds the event from its fields.</summary>
        public UnitPushedEvent(int tick, EntityId unit, FixVec2 from, FixVec2 to) : base(tick)
        {
            Unit = unit;
            From = from;
            To = to;
        }

        /// <summary>Machine-readable kind name: "unit_pushed".</summary>
        public override string Kind => "unit_pushed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " pushed from " + From + " to " + To + ".";
    }

    /// <summary>A team lost water to a flood limit during one flood update.</summary>
    public sealed class FloodWaterRemovedEvent : SimEvent
    {
        /// <summary>The team whose water was removed.</summary>
        public TeamId Team { get; }

        /// <summary>Water removed in this update. Unit: drops (1/256 depth unit). Range: 1 upward.</summary>
        public int Drops { get; }

        /// <summary>Why: "resource_gap" (water inside the dry gap around an enemy resource field or drop-off) or "displaced_off" (a building footprint pushed the water off the tile and no neighbour could take it).</summary>
        public string Reason { get; }

        /// <summary>Builds the event from its fields.</summary>
        public FloodWaterRemovedEvent(int tick, TeamId team, int drops, string reason) : base(tick)
        {
            Team = team;
            Drops = drops;
            Reason = reason;
        }

        /// <summary>Machine-readable kind name: "flood_water_removed".</summary>
        public override string Kind => "flood_water_removed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Team + " lost " + Drops + " drops of water (" + Reason + ").";
    }
}

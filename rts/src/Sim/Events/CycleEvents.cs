// rts/src/Sim/Events/CycleEvents.cs
// The events of Planetary Cycles and Core capture: a pick was accepted, the next cycle was locked (the warning), a cycle started,
// the Core became contested or free, capture started, was reset, completed, the holder lost the Core, the cumulative Core win,
// a quake path opened or closed and a unit was moved off a closing path tile. Part of Rts.Sim.Events
// (spec: rts/docs/specs/planetary_cycles.md section 5). Listeners read these; they never change simulation state (rule 8).
using Rts.Sim.Core;

namespace Rts.Sim.Events
{
    /// <summary>The Core holder (or the script) chose the next cycle. A later accepted pick replaces this one until the lock.</summary>
    public sealed class CyclePickedEvent : SimEvent
    {
        /// <summary>Player who picked; <see cref="PlayerId.None"/> for a script pick.</summary>
        public PlayerId Player { get; }

        /// <summary>Team of the picking player; <see cref="TeamId.None"/> for a script pick.</summary>
        public TeamId Team { get; }

        /// <summary>ID of the picked cycle, such as "quake".</summary>
        public string CycleId { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CyclePickedEvent(int tick, PlayerId player, TeamId team, string cycleId) : base(tick)
        {
            Player = player;
            Team = team;
            CycleId = cycleId;
        }

        /// <summary>Machine-readable kind name: "cycle_picked".</summary>
        public override string Kind => "cycle_picked";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Player + " (" + Team + ") picked '" + CycleId + "'.";
    }

    /// <summary>The next cycle is locked and shown to players (the 60 s warning).</summary>
    public sealed class CycleWarningEvent : SimEvent
    {
        /// <summary>ID of the cycle that will start next.</summary>
        public string CycleId { get; }

        /// <summary>Tick on which it starts. Unit: ticks. Range: above the event tick, or equal to it when the warning is 0 seconds.</summary>
        public int ShiftTick { get; }

        /// <summary>How it was chosen: "picked" (the holder), "scripted" (a script pick) or "random" (no valid pick).</summary>
        public string Method { get; }

        /// <summary>The holder who picked it; <see cref="TeamId.None"/> unless <see cref="Method"/> is "picked".</summary>
        public TeamId ChosenBy { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CycleWarningEvent(int tick, string cycleId, int shiftTick, string method, TeamId chosenBy) : base(tick)
        {
            CycleId = cycleId;
            ShiftTick = shiftTick;
            Method = method;
            ChosenBy = chosenBy;
        }

        /// <summary>Machine-readable kind name: "cycle_warning".</summary>
        public override string Kind => "cycle_warning";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => "Next cycle '" + CycleId + "' at t" + ShiftTick + " (" + Method + (ChosenBy.IsValid ? " by " + ChosenBy : string.Empty) + ").";
    }

    /// <summary>A cycle became active.</summary>
    public sealed class CycleStartedEvent : SimEvent
    {
        /// <summary>ID of the cycle that started.</summary>
        public string CycleId { get; }

        /// <summary>ID of the cycle that ended; empty for the first cycle of the match.</summary>
        public string PreviousCycleId { get; }

        /// <summary>Tick of the next shift. Unit: ticks. Range: above the event tick.</summary>
        public int EndsAtTick { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CycleStartedEvent(int tick, string cycleId, string previousCycleId, int endsAtTick) : base(tick)
        {
            CycleId = cycleId;
            PreviousCycleId = previousCycleId;
            EndsAtTick = endsAtTick;
        }

        /// <summary>Machine-readable kind name: "cycle_started".</summary>
        public override string Kind => "cycle_started";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => "Cycle '" + CycleId + "' started (after '" + PreviousCycleId + "'), ends at t" + EndsAtTick + ".";
    }

    /// <summary>The Core went from uncontested to contested or back (two or more teams present in the radius).</summary>
    public sealed class CoreContestedChangedEvent : SimEvent
    {
        /// <summary>True when two or more teams are present now; false when the contest ended.</summary>
        public bool IsContested { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CoreContestedChangedEvent(int tick, bool isContested) : base(tick)
        {
            IsContested = isContested;
        }

        /// <summary>Machine-readable kind name: "core_contested_changed".</summary>
        public override string Kind => "core_contested_changed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => "Core " + (IsContested ? "contested" : "no longer contested") + ".";
    }

    /// <summary>A lone team began capturing the Core from zero progress.</summary>
    public sealed class CoreCaptureStartedEvent : SimEvent
    {
        /// <summary>The capturing team.</summary>
        public TeamId Team { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CoreCaptureStartedEvent(int tick, TeamId team) : base(tick)
        {
            Team = team;
        }

        /// <summary>Machine-readable kind name: "core_capture_started".</summary>
        public override string Kind => "core_capture_started";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Team + " began capturing the Core.";
    }

    /// <summary>Capture progress was lost.</summary>
    public sealed class CoreCaptureResetEvent : SimEvent
    {
        /// <summary>The team whose progress was lost.</summary>
        public TeamId Team { get; }

        /// <summary>Progress that was lost. Unit: ticks. Range: 0 to capture ticks - 1.</summary>
        public int ProgressTicks { get; }

        /// <summary>Why: "left" (no unit of the team stayed), "other_team" (a different lone team took over) or "holder_out" (the team left the match).</summary>
        public string Reason { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CoreCaptureResetEvent(int tick, TeamId team, int progressTicks, string reason) : base(tick)
        {
            Team = team;
            ProgressTicks = progressTicks;
            Reason = reason;
        }

        /// <summary>Machine-readable kind name: "core_capture_reset".</summary>
        public override string Kind => "core_capture_reset";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Team + " lost " + ProgressTicks + " ticks of Core capture (" + Reason + ").";
    }

    /// <summary>A team finished capturing the Core and is now the holder.</summary>
    public sealed class CoreCapturedEvent : SimEvent
    {
        /// <summary>The new holder.</summary>
        public TeamId Team { get; }

        /// <summary>The holder before the capture; <see cref="TeamId.None"/> when there was none.</summary>
        public TeamId PreviousHolder { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CoreCapturedEvent(int tick, TeamId team, TeamId previousHolder) : base(tick)
        {
            Team = team;
            PreviousHolder = previousHolder;
        }

        /// <summary>Machine-readable kind name: "core_captured".</summary>
        public override string Kind => "core_captured";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Team + " captured the Core (was " + PreviousHolder + ").";
    }

    /// <summary>The holder stopped being the holder.</summary>
    public sealed class CoreLostEvent : SimEvent
    {
        /// <summary>The team that lost the Core.</summary>
        public TeamId Team { get; }

        /// <summary>Why: "captured" (another team took it) or "holder_out" (the holding team left the match).</summary>
        public string Reason { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CoreLostEvent(int tick, TeamId team, string reason) : base(tick)
        {
            Team = team;
            Reason = reason;
        }

        /// <summary>Machine-readable kind name: "core_lost".</summary>
        public override string Kind => "core_lost";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Team + " lost the Core (" + Reason + ").";
    }

    /// <summary>A team held the Core for the cumulative win time and wins the match.</summary>
    public sealed class CoreWinEvent : SimEvent
    {
        /// <summary>The winning team.</summary>
        public TeamId Team { get; }

        /// <summary>Cumulative hold time that earned the win. Unit: ticks. Range: equal to the win time in ticks.</summary>
        public int HoldTicks { get; }

        /// <summary>Builds the event from its fields.</summary>
        public CoreWinEvent(int tick, TeamId team, int holdTicks) : base(tick)
        {
            Team = team;
            HoldTicks = holdTicks;
        }

        /// <summary>Machine-readable kind name: "core_win".</summary>
        public override string Kind => "core_win";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Team + " won by holding the Core for " + HoldTicks + " ticks.";
    }

    /// <summary>A quake path opened or closed.</summary>
    public sealed class QuakePathChangedEvent : SimEvent
    {
        /// <summary>Index of the path in the map's quake path list. Range: 0 to path count - 1.</summary>
        public int PathIndex { get; }

        /// <summary>ID of the path.</summary>
        public string PathId { get; }

        /// <summary>True when the path is now walled (closed), false when open.</summary>
        public bool Closed { get; }

        /// <summary>Tiles whose blocker was added or removed. Unit: tiles. Range: 0 to 64; fewer than the path's tiles when some stayed as they were.</summary>
        public int TilesChanged { get; }

        /// <summary>Builds the event from its fields.</summary>
        public QuakePathChangedEvent(int tick, int pathIndex, string pathId, bool closed, int tilesChanged) : base(tick)
        {
            PathIndex = pathIndex;
            PathId = pathId;
            Closed = closed;
            TilesChanged = tilesChanged;
        }

        /// <summary>Machine-readable kind name: "quake_path_changed".</summary>
        public override string Kind => "quake_path_changed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => "Quake path '" + PathId + "' " + (Closed ? "closed" : "opened") + " (" + TilesChanged + " tiles).";
    }

    /// <summary>A unit was moved off a tile that is about to be walled.</summary>
    public sealed class UnitRelocatedEvent : SimEvent
    {
        /// <summary>The unit that was moved.</summary>
        public EntityId Unit { get; }

        /// <summary>Where it stood. Unit: tiles.</summary>
        public FixVec2 From { get; }

        /// <summary>Where it stands now. Unit: tiles.</summary>
        public FixVec2 To { get; }

        /// <summary>Why: "quake_path".</summary>
        public string Reason { get; }

        /// <summary>Builds the event from its fields.</summary>
        public UnitRelocatedEvent(int tick, EntityId unit, FixVec2 from, FixVec2 to, string reason) : base(tick)
        {
            Unit = unit;
            From = from;
            To = to;
            Reason = reason;
        }

        /// <summary>Machine-readable kind name: "unit_relocated".</summary>
        public override string Kind => "unit_relocated";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Unit + " moved from " + From + " to " + To + " (" + Reason + ").";
    }
}

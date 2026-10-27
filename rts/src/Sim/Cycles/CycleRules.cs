// rts/src/Sim/Cycles/CycleRules.cs
// The pure rules of Planetary Cycles and Core capture: the schedule maths (shift and lock ticks), which units count for the
// Core, which cycles may come next (no repeated play style) and the capture state machine. Part of Rts.Sim.Cycles
// (spec: rts/docs/specs/planetary_cycles.md sections 4 and 6). Nothing here changes simulation state or draws random numbers, so
// the capture state machine can be tested without a simulation: CaptureStep takes the teams present and returns the changes.
// Determinism: integers and Fix64 only, ascending team order, squared distances (no square root).
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Cycles
{
    /// <summary>What a step of the capture state machine changed; the cycle system turns each into an event.</summary>
    public enum CoreChangeKind : byte
    {
        /// <summary>A lone team started capturing from zero.</summary>
        CaptureStarted = 0,

        /// <summary>A team's capture progress was lost.</summary>
        CaptureReset = 1,

        /// <summary>A team finished capturing and became the holder.</summary>
        Captured = 2,

        /// <summary>The previous holder lost the Core to a capture.</summary>
        Lost = 3,
    }

    /// <summary>One change reported by <see cref="CycleRules.CaptureStep"/>. Immutable.</summary>
    public readonly struct CoreChange
    {
        /// <summary>What happened.</summary>
        public readonly CoreChangeKind Kind;

        /// <summary>The team the change is about: the capturer, the team that lost progress, the new holder or the team that lost the Core.</summary>
        public readonly TeamId Team;

        /// <summary>For <see cref="CoreChangeKind.Captured"/>: the holder before the capture (<see cref="TeamId.None"/> if none). Otherwise <see cref="TeamId.None"/>.</summary>
        public readonly TeamId PreviousHolder;

        /// <summary>For <see cref="CoreChangeKind.CaptureReset"/>: the progress that was lost. Unit: ticks. Range: 0 to capture ticks - 1. Otherwise 0.</summary>
        public readonly int ProgressTicks;

        /// <summary>For <see cref="CoreChangeKind.CaptureReset"/>: "left" or "other_team". For <see cref="CoreChangeKind.Lost"/>: "captured". Otherwise empty.</summary>
        public readonly string Reason;

        /// <summary>Stores the change.</summary>
        public CoreChange(CoreChangeKind kind, TeamId team, TeamId previousHolder, int progressTicks, string reason)
        {
            Kind = kind;
            Team = team;
            PreviousHolder = previousHolder;
            ProgressTicks = progressTicks;
            Reason = reason;
        }
    }

    /// <summary>The Core ownership and capture progress. Mutated only through <see cref="CycleRules.CaptureStep"/> and the cycle system.</summary>
    public struct CoreState
    {
        /// <summary>The team that holds the Core, or <see cref="TeamId.None"/>.</summary>
        public TeamId Holder;

        /// <summary>The team currently capturing, or <see cref="TeamId.None"/>.</summary>
        public TeamId CaptureTeam;

        /// <summary>Counted ticks of <see cref="CaptureTeam"/>'s capture. Unit: ticks. Range: 0 to capture ticks - 1; 0 when nobody is capturing.</summary>
        public int CaptureProgressTicks;

        /// <summary>A Core nobody holds or captures. (A default struct would name team 0, so always start from this.)</summary>
        public static CoreState Empty()
        {
            CoreState state = default;
            state.Holder = TeamId.None;
            state.CaptureTeam = TeamId.None;
            state.CaptureProgressTicks = 0;
            return state;
        }
    }

    /// <summary>Stateless helpers for the schedule, the counting rule, the allowed next cycles and the capture state machine.</summary>
    public static class CycleRules
    {
        /// <summary>Tick of shift number <paramref name="k"/>: k times the interval. Unit: ticks.</summary>
        /// <param name="k">Shift number. Range: 1 upward.</param>
        /// <param name="intervalTicks">Ticks between shifts. Unit: ticks. Range: 1 upward.</param>
        public static int ShiftTick(int k, int intervalTicks)
        {
            return k * intervalTicks;
        }

        /// <summary>Tick on which shift number <paramref name="k"/> is locked and announced: the shift tick minus the warning. Unit: ticks.</summary>
        /// <param name="k">Shift number. Range: 1 upward.</param>
        /// <param name="intervalTicks">Ticks between shifts. Unit: ticks. Range: 1 upward.</param>
        /// <param name="warningTicks">Warning length. Unit: ticks. Range: 0 to intervalTicks - 1.</param>
        public static int LockTick(int k, int intervalTicks, int warningTicks)
        {
            return k * intervalTicks - warningTicks;
        }

        /// <summary>The first shift strictly after tick <paramref name="tick"/>: (tick / interval + 1) * interval. Unit: ticks.</summary>
        /// <param name="tick">Any tick. Range: 0 upward.</param>
        /// <param name="intervalTicks">Ticks between shifts. Unit: ticks. Range: 1 upward.</param>
        public static int NextShiftTick(int tick, int intervalTicks)
        {
            return (tick / intervalTicks + 1) * intervalTicks;
        }

        /// <summary>The first shift at or after tick <paramref name="tick"/> (never tick 0, which is not a shift). Unit: ticks.</summary>
        /// <param name="tick">Any tick. Range: 0 upward.</param>
        /// <param name="intervalTicks">Ticks between shifts. Unit: ticks. Range: 1 upward.</param>
        public static int NextShiftTickAtOrAfter(int tick, int intervalTicks)
        {
            // Shift number of the first shift at or after the tick. Unit: count. Range: 1 upward.
            int k = (tick + intervalTicks - 1) / intervalTicks;
            return (k < 1 ? 1 : k) * intervalTicks;
        }

        /// <summary>
        /// The shift number locked on <paramref name="tick"/>, or 0 when no lock happens then. A lock happens on tick
        /// (k * interval - warning) for k from 1; with a warning of 0 it shares the tick of the shift.
        /// </summary>
        /// <param name="tick">The tick being simulated. Range: 0 upward.</param>
        /// <param name="intervalTicks">Ticks between shifts. Unit: ticks. Range: 1 upward.</param>
        /// <param name="warningTicks">Warning length. Unit: ticks. Range: 0 to intervalTicks - 1.</param>
        public static int LockedShiftNumber(int tick, int intervalTicks, int warningTicks)
        {
            // Tick at which the locked shift would happen. Unit: ticks. Range: 0 upward.
            int shiftTick = tick + warningTicks;
            return shiftTick > 0 && shiftTick % intervalTicks == 0 ? shiftTick / intervalTicks : 0;
        }

        /// <summary>The shift number that happens on <paramref name="tick"/>, or 0 when no shift happens then. Tick 0 is never a shift.</summary>
        /// <param name="tick">The tick being simulated. Range: 0 upward.</param>
        /// <param name="intervalTicks">Ticks between shifts. Unit: ticks. Range: 1 upward.</param>
        public static int ShiftNumber(int tick, int intervalTicks)
        {
            return tick > 0 && tick % intervalTicks == 0 ? tick / intervalTicks : 0;
        }

        /// <summary>
        /// True when <paramref name="unit"/> counts toward the Core: alive, owned by a player still in the match, mobile
        /// (speed above 0, so workers, the HQ and flyers count and buildings do not) and with its centre within the Core radius
        /// (exactly on the edge counts; the unit's own radius is ignored).
        /// </summary>
        /// <param name="sim">The simulation, for the player states.</param>
        /// <param name="unit">The unit to test.</param>
        /// <param name="core">The Core area of the map.</param>
        /// <param name="team">The owner's team when the unit counts; <see cref="TeamId.None"/> otherwise.</param>
        public static bool CountsForCore(Simulation sim, Unit unit, CoreArea core, out TeamId team)
        {
            team = TeamId.None;
            if (unit.Health <= Fix64.Zero || unit.Definition.SpeedTilesPerSecond <= Fix64.Zero)
            {
                return false;
            }
            // The unit's owner record (Active, Disconnected, Defeated or Surrendered).
            if (!sim.Players.TryGet(unit.Owner, out Player? owner) || !owner.IsInMatch)
            {
                return false;
            }
            // Offset from the Core centre. Unit: tiles.
            FixVec2 delta = unit.Position - core.Centre;
            if (delta.LengthSquared() > core.RadiusTiles * core.RadiusTiles)
            {
                return false;
            }
            team = owner.Team;
            return true;
        }

        /// <summary>
        /// The cycles that may start next, in the order of <paramref name="cycles"/> (ascending ID): every cycle whose play style
        /// differs from <paramref name="active"/>'s; all of them when there is no active cycle yet.
        /// </summary>
        /// <param name="cycles">Every cycle of the match, ascending ID.</param>
        /// <param name="active">The cycle in force, or null before the first shift.</param>
        public static List<CycleDefinition> Allowed(IReadOnlyList<CycleDefinition> cycles, CycleDefinition? active)
        {
            // Cycles that pass the no-repeat rule, ascending ID.
            List<CycleDefinition> allowed = new List<CycleDefinition>();
            foreach (CycleDefinition cycle in cycles)
            {
                if (active == null || cycle.Favours != active.Favours)
                {
                    allowed.Add(cycle);
                }
            }
            return allowed;
        }

        /// <summary>
        /// One tick of the capture state machine. <paramref name="present"/> lists the teams with a counting unit in the Core
        /// (ascending, distinct). A lone challenging team (not the holder, with the holder absent) gains one tick of progress
        /// and captures at <paramref name="captureTicks"/>; a contest freezes progress of a team that is present and resets one
        /// that is not; nobody present resets it. A different lone team restarts from 0. The state is updated in place and the
        /// changes are appended to <paramref name="changes"/> in the order they happened.
        /// </summary>
        /// <param name="state">Holder and capture progress; updated.</param>
        /// <param name="present">Teams present this tick, ascending.</param>
        /// <param name="captureTicks">Counted ticks needed to capture. Unit: ticks. Range: 1 upward.</param>
        /// <param name="changes">Receives the changes, in order.</param>
        public static void CaptureStep(ref CoreState state, IReadOnlyList<TeamId> present, int captureTicks, List<CoreChange> changes)
        {
            // Teams present other than the holder, ascending.
            List<TeamId> challengers = new List<TeamId>();
            // True when the holder is present.
            bool holderPresent = false;
            foreach (TeamId team in present)
            {
                if (state.Holder.IsValid && team == state.Holder)
                {
                    holderPresent = true;
                }
                else
                {
                    challengers.Add(team);
                }
            }
            if (challengers.Count == 1 && !holderPresent)
            {
                // The lone challenger.
                TeamId challenger = challengers[0];
                if (state.CaptureTeam != challenger)
                {
                    if (state.CaptureTeam.IsValid)
                    {
                        changes.Add(new CoreChange(CoreChangeKind.CaptureReset, state.CaptureTeam, TeamId.None, state.CaptureProgressTicks, "other_team"));
                    }
                    state.CaptureTeam = challenger;
                    state.CaptureProgressTicks = 0;
                    changes.Add(new CoreChange(CoreChangeKind.CaptureStarted, challenger, TeamId.None, 0, string.Empty));
                }
                state.CaptureProgressTicks++;
                if (state.CaptureProgressTicks >= captureTicks)
                {
                    // The holder before this capture, reported with the change.
                    TeamId previous = state.Holder;
                    state.Holder = challenger;
                    state.CaptureTeam = TeamId.None;
                    state.CaptureProgressTicks = 0;
                    if (previous.IsValid)
                    {
                        changes.Add(new CoreChange(CoreChangeKind.Lost, previous, TeamId.None, 0, "captured"));
                    }
                    changes.Add(new CoreChange(CoreChangeKind.Captured, challenger, previous, 0, string.Empty));
                }
                return;
            }
            if (!state.CaptureTeam.IsValid)
            {
                return;
            }
            // Whether the capturing team still has a unit in the Core (contested: progress freezes; absent: it resets).
            bool capturerPresent = false;
            foreach (TeamId team in present)
            {
                if (team == state.CaptureTeam)
                {
                    capturerPresent = true;
                }
            }
            if (challengers.Count == 0 || !capturerPresent)
            {
                changes.Add(new CoreChange(CoreChangeKind.CaptureReset, state.CaptureTeam, TeamId.None, state.CaptureProgressTicks, "left"));
                state.CaptureTeam = TeamId.None;
                state.CaptureProgressTicks = 0;
            }
        }
    }
}

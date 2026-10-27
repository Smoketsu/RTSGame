// rts/src/Sim/Commands/CycleCommands.cs
// The command that chooses the next Planetary Cycle: pick_cycle. Part of Rts.Sim.Commands (spec: rts/docs/specs/planetary_cycles.md
// section 5). Players on the team holding the Core issue it; the script issuer (PlayerId.None) may issue it for tests and missions.
// The pick only records a wish: the cycle system uses it when the next cycle is locked (the warning tick), so later picks replace
// earlier ones until then.
using System;
using Rts.Sim.Core;
using Rts.Sim.Cycles;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Teams;

namespace Rts.Sim.Commands
{
    /// <summary>
    /// Picks the cycle that follows the next lock. Script name <c>pick_cycle</c> (field <c>cycle</c>). Rejections, in order:
    /// cycles_disabled (the match has no cycles); for a player issuer player_out_of_match (the issuer is not Active) and not_holder (the
    /// issuer's team does not hold the Core, including when nobody does or the map has no Core); unknown_cycle; pick_locked (the next
    /// cycle is already locked); repeats_play_style (the cycle favours the same play style as the active one). The script issuer skips
    /// the holder checks.
    /// </summary>
    public sealed class PickCycleCommand : Command
    {
        /// <summary>ID of the cycle to pick, such as "quake". Non-empty.</summary>
        public string CycleId { get; }

        /// <summary>Builds the command from its scheduling fields and parameters. Throws on an empty cycle ID so a bad command never reaches a step.</summary>
        public PickCycleCommand(int tick, PlayerId issuer, uint sequence, string cycleId) : base(tick, issuer, sequence)
        {
            if (string.IsNullOrEmpty(cycleId))
            {
                throw new ArgumentException("PickCycleCommand needs a cycle id.", nameof(cycleId));
            }
            CycleId = cycleId;
        }

        /// <summary>Machine-readable kind name: "pick_cycle".</summary>
        public override string Kind => "pick_cycle";

        /// <summary>Applies the command; see the class summary for the rejections.</summary>
        public override CommandResult Apply(Simulation sim)
        {
            // The match's cycle system.
            CycleSystem cycles = sim.Cycles;
            if (!cycles.IsEnabled)
            {
                return CommandResult.Reject("cycles_disabled");
            }
            // Team of the issuer; None for the script issuer.
            TeamId team = TeamId.None;
            if (Issuer.IsValid)
            {
                if (!sim.Players.TryGet(Issuer, out Player? player) || !player.IsActive)
                {
                    return CommandResult.Reject("player_out_of_match");
                }
                team = player.Team;
                if (!cycles.Holder.IsValid || cycles.Holder != team)
                {
                    return CommandResult.Reject("not_holder");
                }
            }
            // The cycle named, or null for an unknown ID.
            CycleDefinition? picked = null;
            foreach (CycleDefinition cycle in cycles.Definitions)
            {
                if (string.Equals(cycle.Id, CycleId, StringComparison.Ordinal))
                {
                    picked = cycle;
                }
            }
            if (picked == null)
            {
                return CommandResult.Reject("unknown_cycle");
            }
            if (cycles.IsNextLocked)
            {
                return CommandResult.Reject("pick_locked");
            }
            // The cycle in force, or null before the first shift.
            CycleDefinition? active = cycles.ActiveCycle;
            if (active != null && active.Favours == picked.Favours)
            {
                return CommandResult.Reject("repeats_play_style");
            }
            cycles.SetPendingPick(picked.Id, team);
            sim.Events.Add(new CyclePickedEvent(sim.Clock.Tick, Issuer, team, picked.Id));
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(CycleId);
        }
    }
}

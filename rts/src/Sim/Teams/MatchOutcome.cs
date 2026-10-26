// rts/src/Sim/Teams/MatchOutcome.cs
// Team-level outcome query: which teams are still in the match and whether it is decided. Part of
// Rts.Sim.Teams. Works for any number of teams; nothing here assumes two. Phase 0 only answers the
// question; the match rules layer (Phase 3, with the Core win in 1v1v1/FFA) decides when to end the match
// and report results. Design source: Game Design Proposal, "Surrender and disconnects", "Victory".
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Sim.Teams
{
    /// <summary>
    /// A snapshot of the match result from the player states. A team is standing while at least one of its players is
    /// still in the match (Active, or Disconnected and able to reconnect). The match is decided when exactly one team
    /// or no team is standing. Examples: 1v1 surrender leaves the opponent standing alone (the opponent wins); a 2v2
    /// player surrendering leaves the ally standing (not decided); in a 4-player FFA the match is decided only after
    /// three players are out.
    /// </summary>
    public sealed class MatchOutcome
    {
        /// <summary>Teams with at least one <see cref="PlayerState.Active"/> player, ascending. Subset of <see cref="StandingTeams"/>.</summary>
        public IReadOnlyList<TeamId> TeamsWithActivePlayers { get; }

        /// <summary>
        /// Teams with at least one player still in the match (Active or Disconnected), ascending. A disconnect alone never
        /// decides a match: the Phase 2 rules turn a player who does not return into Defeated, which then counts.
        /// </summary>
        public IReadOnlyList<TeamId> StandingTeams { get; }

        /// <summary>
        /// True when at most one team is standing. Note: a sandbox or tutorial with a single team is decided from tick 0;
        /// the match rules layer checks <see cref="PlayerRegistry.TeamCount"/> before ending such a match.
        /// </summary>
        public bool IsDecided => StandingTeams.Count <= 1;

        /// <summary>The single standing team when decided with one team left; <see cref="TeamId.None"/> otherwise (undecided or a draw).</summary>
        public TeamId Winner => StandingTeams.Count == 1 ? StandingTeams[0] : TeamId.None;

        /// <summary>True when no team is standing (for example both players of a 1v1 surrendered on the same tick).</summary>
        public bool IsDraw => StandingTeams.Count == 0;

        /// <summary>Stores the two team lists, already sorted ascending.</summary>
        /// <param name="teamsWithActivePlayers">Teams with an Active player.</param>
        /// <param name="standingTeams">Teams with a player still in the match.</param>
        private MatchOutcome(IReadOnlyList<TeamId> teamsWithActivePlayers, IReadOnlyList<TeamId> standingTeams)
        {
            TeamsWithActivePlayers = teamsWithActivePlayers;
            StandingTeams = standingTeams;
        }

        /// <summary>Computes the outcome from the current player states. Pure; allocates two small lists.</summary>
        /// <param name="players">The match's players.</param>
        public static MatchOutcome Evaluate(PlayerRegistry players)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }
            // Teams with an Active player, appended in ascending team order.
            List<TeamId> active = new List<TeamId>(players.TeamCount);
            // Teams with a player still in the match, appended in ascending team order.
            List<TeamId> standing = new List<TeamId>(players.TeamCount);
            foreach (TeamId team in players.Teams)
            {
                // True once a member of this team is found Active.
                bool hasActive = false;
                // True once a member of this team is found still in the match.
                bool hasStanding = false;
                foreach (PlayerId id in players.PlayersOnTeam(team))
                {
                    if (players.TryGet(id, out Player? player))
                    {
                        hasActive |= player.IsActive;
                        hasStanding |= player.IsInMatch;
                    }
                }
                if (hasActive)
                {
                    active.Add(team);
                }
                if (hasStanding)
                {
                    standing.Add(team);
                }
            }
            return new MatchOutcome(active.AsReadOnly(), standing.AsReadOnly());
        }

        /// <summary>Text such as "decided, winner T2" or "undecided, standing T0,T1" for logs.</summary>
        public override string ToString()
        {
            if (IsDraw)
            {
                return "decided, draw";
            }
            if (IsDecided)
            {
                return "decided, winner " + Winner;
            }
            return "undecided, standing " + string.Join(",", StandingTeams);
        }
    }
}

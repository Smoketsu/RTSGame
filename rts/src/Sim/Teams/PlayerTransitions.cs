// rts/src/Sim/Teams/PlayerTransitions.cs
// The single place that moves a player through the state machine during a step: checks the transition
// table, updates the Player, emits PlayerStateChangedEvent and calls the match's IHandOverPolicy.
// Part of Rts.Sim.Teams. Used by the team commands now and by later systems (Phase 1 HQ destruction marks
// a player Defeated; Phase 3 FFA elimination marks a player Eliminated). Rule 2: only inside Simulation.Step.
using System;
using Rts.Sim.Core;
using Rts.Sim.Events;

namespace Rts.Sim.Teams
{
    /// <summary>Applies player state transitions inside a step.</summary>
    public static class PlayerTransitions
    {
        /// <summary>Rejection reason for a player ID that is not in the match.</summary>
        public const string ReasonUnknownPlayer = "unknown_player";

        /// <summary>
        /// Moves <paramref name="playerId"/> to <paramref name="to"/> if the transition table allows it. On success the
        /// player's state and state-since tick change, a <see cref="PlayerStateChangedEvent"/> is emitted with
        /// <paramref name="cause"/>, and the simulation's <see cref="IHandOverPolicy"/> is told (left match, disconnected or
        /// reconnected). On failure nothing changes and <paramref name="rejection"/> holds the snake_case reason.
        /// Throws when called outside <see cref="Simulation.Step"/>.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="playerId">Player to move.</param>
        /// <param name="to">Requested state.</param>
        /// <param name="cause">Short snake_case cause for the event, such as "surrender", "disconnect", "hq_destroyed".</param>
        /// <param name="rejection">Null on success; the reason otherwise.</param>
        public static bool TryChange(Simulation sim, PlayerId playerId, PlayerState to, string cause, out string? rejection)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }
            if (string.IsNullOrEmpty(cause))
            {
                throw new ArgumentException("A state change needs a cause.", nameof(cause));
            }
            if (!sim.IsStepping)
            {
                throw new InvalidOperationException("Player states can only change inside Step (through commands, systems or mechanics).");
            }
            if (!sim.Players.TryGet(playerId, out Player? player))
            {
                rejection = ReasonUnknownPlayer;
                return false;
            }
            // State before the change, for the event and the policy.
            PlayerState previous = player.State;
            rejection = PlayerStateRules.TransitionProblem(previous, to);
            if (rejection != null)
            {
                return false;
            }
            // Current tick, which becomes the player's state-since tick. Range: 0 upward.
            int tick = sim.Clock.Tick;
            player.SetState(to, tick);
            sim.Events.Add(new PlayerStateChangedEvent(tick, player.Id, player.Team, previous, to, cause));
            if (!PlayerStateRules.IsInMatch(to))
            {
                sim.HandOverPolicy.OnPlayerLeftMatch(sim, player, previous);
            }
            else if (to == PlayerState.Disconnected)
            {
                sim.HandOverPolicy.OnPlayerDisconnected(sim, player);
            }
            else if (previous == PlayerState.Disconnected)
            {
                sim.HandOverPolicy.OnPlayerReconnected(sim, player);
            }
            return true;
        }
    }
}

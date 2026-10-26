// rts/src/Sim/Teams/PlayerState.cs
// The per-player state machine of a match: the states a player can be in, the connection states the
// network layer reports, and the table of legal transitions with the snake_case reason given when a
// transition is refused. Part of Rts.Sim.Teams. Pure rules with no simulation access; the transitions
// themselves are applied by PlayerTransitions inside Simulation.Step (commands, later systems).
// Design source: Game Design Proposal, "Surrender and disconnects" and "Victory" (26 Oct 2026).

namespace Rts.Sim.Teams
{
    /// <summary>
    /// Where a player stands in the match. Stored as one byte and written into the state hash, so the numeric
    /// values are part of the replay format: never renumber them, only append.
    /// </summary>
    public enum PlayerState : byte
    {
        /// <summary>Playing normally. Every player starts here at tick 0.</summary>
        Active = 0,

        /// <summary>Gave up through a <see cref="Rts.Sim.Commands.SurrenderCommand"/>. Terminal.</summary>
        Surrendered = 1,

        /// <summary>
        /// Lost the network connection. Temporary: the player can reconnect (back to <see cref="Active"/>) or, if they do
        /// not return within disconnect_pause_max_seconds (tuning/online.json), the Phase 2 match rules mark them
        /// <see cref="Defeated"/>. A disconnected player still counts as standing for <see cref="MatchOutcome"/>.
        /// </summary>
        Disconnected = 2,

        /// <summary>Lost because their HQ unit was destroyed (design: "A player loses when their HQ unit is destroyed"), or did not return from a disconnect. Terminal.</summary>
        Defeated = 3,

        /// <summary>Lost because all their units were destroyed in 1v1v1 or FFA. Terminal.</summary>
        Eliminated = 4,
    }

    /// <summary>What the network layer (Phase 2) reports about a player's link. Carried by <see cref="Rts.Sim.Commands.SetConnectionStateCommand"/>.</summary>
    public enum ConnectionState : byte
    {
        /// <summary>The player's client is connected (or has just reconnected).</summary>
        Connected = 0,

        /// <summary>The player's client dropped.</summary>
        Disconnected = 1,
    }

    /// <summary>The legal-transition table of the player state machine, plus state classification helpers.</summary>
    public static class PlayerStateRules
    {
        /// <summary>Rejection reason when a terminal player (surrendered, defeated, eliminated) would change state.</summary>
        public const string ReasonOutOfMatch = "player_out_of_match";

        /// <summary>Rejection reason when a disconnected player would surrender (they cannot issue commands; a no-show becomes Defeated instead).</summary>
        public const string ReasonDisconnected = "player_disconnected";

        /// <summary>
        /// True while the player is still contesting the match: <see cref="PlayerState.Active"/> or
        /// <see cref="PlayerState.Disconnected"/> (which may still reconnect).
        /// </summary>
        /// <param name="state">State to classify.</param>
        public static bool IsInMatch(PlayerState state)
        {
            return state == PlayerState.Active || state == PlayerState.Disconnected;
        }

        /// <summary>True for states no transition leaves: surrendered, defeated, eliminated.</summary>
        /// <param name="state">State to classify.</param>
        public static bool IsTerminal(PlayerState state)
        {
            return state == PlayerState.Surrendered || state == PlayerState.Defeated || state == PlayerState.Eliminated;
        }

        /// <summary>
        /// Returns null when <paramref name="from"/> may move to <paramref name="to"/>, otherwise the snake_case reason
        /// used in <see cref="Rts.Sim.Events.CommandRejectedEvent"/>. Legal moves:
        /// Active to Surrendered, Disconnected, Defeated or Eliminated; Disconnected to Active (reconnect), Defeated or
        /// Eliminated. Everything else is refused: the same state ("already_&lt;state&gt;"), any move out of a terminal
        /// state, and Disconnected to Surrendered.
        /// </summary>
        /// <param name="from">Current state.</param>
        /// <param name="to">Requested state.</param>
        public static string? TransitionProblem(PlayerState from, PlayerState to)
        {
            if (from == to)
            {
                return "already_" + Name(to);
            }
            if (IsTerminal(from))
            {
                return ReasonOutOfMatch;
            }
            if (from == PlayerState.Disconnected && to == PlayerState.Surrendered)
            {
                return ReasonDisconnected;
            }
            // Remaining cases: Active to any other state, Disconnected to Active, Defeated or Eliminated. All legal.
            return null;
        }

        /// <summary>True when <see cref="TransitionProblem"/> returns null.</summary>
        /// <param name="from">Current state.</param>
        /// <param name="to">Requested state.</param>
        public static bool CanTransition(PlayerState from, PlayerState to)
        {
            return TransitionProblem(from, to) == null;
        }

        /// <summary>Lower-case snake_case name of a state for reasons and logs, such as "surrendered". Culture-independent.</summary>
        /// <param name="state">State to name.</param>
        public static string Name(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Active: return "active";
                case PlayerState.Surrendered: return "surrendered";
                case PlayerState.Disconnected: return "disconnected";
                case PlayerState.Defeated: return "defeated";
                case PlayerState.Eliminated: return "eliminated";
                default: return "state_" + ((byte)state).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}

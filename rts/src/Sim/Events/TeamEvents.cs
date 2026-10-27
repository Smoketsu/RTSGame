// rts/src/Sim/Events/TeamEvents.cs
// Events about players and teams: a player moved through the state machine (surrendered, disconnected,
// reconnected, defeated, eliminated). Part of Rts.Sim.Events. Emitted only by PlayerTransitions inside
// Simulation.Step; the UI, telemetry and the match rules layer read them.
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Sim.Events
{
    /// <summary>A player's <see cref="PlayerState"/> changed this tick.</summary>
    public sealed class PlayerStateChangedEvent : SimEvent
    {
        /// <summary>The player whose state changed.</summary>
        public PlayerId Player { get; }

        /// <summary>The player's team, so listeners can update team displays without a registry lookup.</summary>
        public TeamId Team { get; }

        /// <summary>State before the change.</summary>
        public PlayerState Previous { get; }

        /// <summary>State after the change.</summary>
        public PlayerState Current { get; }

        /// <summary>Short snake_case cause, such as "surrender", "disconnect", "reconnect", "hq_destroyed".</summary>
        public string Cause { get; }

        /// <summary>Builds the event.</summary>
        /// <param name="tick">Tick of the change, 0 upward.</param>
        /// <param name="player">Player whose state changed.</param>
        /// <param name="team">That player's team.</param>
        /// <param name="previous">State before.</param>
        /// <param name="current">State after.</param>
        /// <param name="cause">Snake_case cause.</param>
        public PlayerStateChangedEvent(int tick, PlayerId player, TeamId team, PlayerState previous, PlayerState current, string cause) : base(tick)
        {
            Player = player;
            Team = team;
            Previous = previous;
            Current = current;
            Cause = cause;
        }

        /// <summary>"player_state_changed".</summary>
        public override string Kind => "player_state_changed";

        /// <summary>Text such as "P1 (T0) active -> surrendered (surrender).".</summary>
        public override string Describe() => Player + " (" + Team + ") " + PlayerStateRules.Name(Previous) + " -> " + PlayerStateRules.Name(Current) + " (" + Cause + ").";
    }
}

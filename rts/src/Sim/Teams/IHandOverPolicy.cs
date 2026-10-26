// rts/src/Sim/Teams/IHandOverPolicy.cs
// Extension point for what happens to a player's units and resources when they leave the match or drop
// their connection. Part of Rts.Sim.Teams. Phase 0 ships only StateOnlyHandOverPolicy, which does nothing
// beyond the state change and PlayerStateChangedEvent that PlayerTransitions has already done. The real
// rules arrive later (see the TODOs). Design source: Game Design Proposal, "Surrender and disconnects".

using Rts.Sim.Core;

namespace Rts.Sim.Teams
{
    /// <summary>
    /// Called by <see cref="PlayerTransitions"/> inside <see cref="Simulation.Step"/> after a player's state has changed
    /// and the <see cref="Rts.Sim.Events.PlayerStateChangedEvent"/> has been emitted. Implementations must be
    /// deterministic, may change simulation state only through the simulation's mutation methods, and must keep any state
    /// of their own in the state hash (prefer keeping none). One instance serves the whole match; set it with
    /// <see cref="Simulation.SetHandOverPolicy"/> before <see cref="Simulation.Start"/>.
    /// </summary>
    public interface IHandOverPolicy
    {
        /// <summary>Stable policy ID such as "state_only"; written into the state hash so peers with different policies diverge at once.</summary>
        string Id { get; }

        /// <summary>Feeds any policy state that affects future ticks into the state hash. A stateless policy writes nothing.</summary>
        void WriteHash(StateHasher hasher);

        /// <summary>
        /// The player has just left the match: Surrendered, Defeated (HQ destroyed or did not return from a disconnect) or
        /// Eliminated. Design rules for later phases: in a team with another Active player (2v2) the leaver's units and
        /// resources pass to that ally (<see cref="PlayerRegistry.FirstActiveAllyOf"/>); when no ally is left (1v1v1, FFA)
        /// the leaver's units are destroyed and play continues; in 1v1 the opponent wins, which
        /// <see cref="MatchOutcome"/> already reports. Resources are only ever handed over on leaving; allies never share
        /// resources while both play.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="player">The player who left; its <see cref="Player.State"/> is already the new state.</param>
        /// <param name="previousState">State before the change: Active or Disconnected.</param>
        void OnPlayerLeftMatch(Simulation sim, Player player, PlayerState previousState);

        /// <summary>
        /// The player has just become Disconnected. Design rules for later phases: the game pauses for up to
        /// disconnect_pause_max_seconds (tuning/online.json), once per player (disconnect_pauses_per_player), charged to
        /// <see cref="Player.DisconnectPausesUsed"/>; if the player does not return they become Defeated, and in 2v2 the
        /// ally takes over their units.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="player">The player who dropped; its state is already Disconnected.</param>
        void OnPlayerDisconnected(Simulation sim, Player player);

        /// <summary>The player has just reconnected (Disconnected to Active). Later phases end any running disconnect pause here.</summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="player">The player who returned; its state is already Active.</param>
        void OnPlayerReconnected(Simulation sim, Player player);
    }

    /// <summary>
    /// Phase 0 default: the player state change and event are the whole effect. Units and resources stay with the
    /// player who left, and no pause happens. Stateless, so nothing to hash.
    /// </summary>
    public sealed class StateOnlyHandOverPolicy : IHandOverPolicy
    {
        /// <summary>The one shared instance; the policy has no state.</summary>
        public static readonly StateOnlyHandOverPolicy Instance = new StateOnlyHandOverPolicy();

        /// <summary>Policy ID as written into the state hash.</summary>
        public string Id => "state_only";

        /// <summary>Writes nothing: the policy holds no state.</summary>
        public void WriteHash(StateHasher hasher)
        {
        }

        /// <summary>Private so <see cref="Instance"/> is the only instance.</summary>
        private StateOnlyHandOverPolicy()
        {
        }

        /// <summary>Does nothing in Phase 0.</summary>
        /// <param name="sim">Unused.</param>
        /// <param name="player">Unused.</param>
        /// <param name="previousState">Unused.</param>
        public void OnPlayerLeftMatch(Simulation sim, Player player, PlayerState previousState)
        {
            // TODO(Phase 3, team and FFA modes): hand units and resources to PlayerRegistry.FirstActiveAllyOf(player.Id)
            // when it is valid (2v2); otherwise destroy the player's units with Simulation.RemoveUnit (1v1v1, FFA).
            // Needs Phase 1 ownership transfer and the economy's per-player resource stores.
        }

        /// <summary>Does nothing in Phase 0.</summary>
        /// <param name="sim">Unused.</param>
        /// <param name="player">Unused.</param>
        public void OnPlayerDisconnected(Simulation sim, Player player)
        {
            // TODO(Phase 2, lockstep netcode): start a disconnect pause if player.DisconnectPausesUsed is below
            // disconnect_pauses_per_player (tuning/online.json), capped at disconnect_pause_max_seconds, charging
            // Player.RecordDisconnectPause and Player.AddPauseTicks; on timeout move the player to Defeated.
            // TODO(Phase 3): in 2v2 the ally takes over the disconnected player's units after the timeout.
        }

        /// <summary>Does nothing in Phase 0.</summary>
        /// <param name="sim">Unused.</param>
        /// <param name="player">Unused.</param>
        public void OnPlayerReconnected(Simulation sim, Player player)
        {
            // TODO(Phase 2, lockstep netcode): end the running disconnect pause, if any.
        }
    }
}

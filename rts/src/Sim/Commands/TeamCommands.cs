// rts/src/Sim/Commands/TeamCommands.cs
// Commands that move players through the state machine: surrender, and the connection changes the
// network layer reports. Part of Rts.Sim.Commands. Both go through PlayerTransitions, so the transition
// table, PlayerStateChangedEvent and the IHandOverPolicy hooks apply the same way to every source
// (player input, network layer, replays, scripts). Design source: Game Design Proposal, "Surrender and disconnects".
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Sim.Commands
{
    /// <summary>
    /// A player gives up. Moves the player from Active to Surrendered. What happens to their units and resources is the
    /// <see cref="IHandOverPolicy"/>'s job (Phase 0: nothing). Rejections: "unknown_player", "not_self" (a player may only
    /// surrender for themselves; issuer None, the match script, may surrender anyone), "player_disconnected",
    /// "player_out_of_match".
    /// </summary>
    public sealed class SurrenderCommand : Command
    {
        /// <summary>Rejection reason when a player tries to change another player's state.</summary>
        public const string ReasonNotSelf = "not_self";

        /// <summary>The player who surrenders.</summary>
        public PlayerId Player { get; }

        /// <summary>Builds the command.</summary>
        /// <param name="tick">Tick to apply on, 0 upward.</param>
        /// <param name="issuer">The surrendering player, or None for the match script.</param>
        /// <param name="sequence">Issuer's command counter.</param>
        /// <param name="player">The player who surrenders.</param>
        public SurrenderCommand(int tick, PlayerId issuer, uint sequence, PlayerId player) : base(tick, issuer, sequence)
        {
            Player = player;
        }

        /// <summary>"surrender".</summary>
        public override string Kind => "surrender";

        /// <summary>Checks the issuer, then moves the player to Surrendered through <see cref="PlayerTransitions"/>.</summary>
        /// <param name="sim">The stepping simulation.</param>
        public override CommandResult Apply(Simulation sim)
        {
            if (Issuer.IsValid && Issuer != Player)
            {
                return CommandResult.Reject(ReasonNotSelf);
            }
            if (!PlayerTransitions.TryChange(sim, Player, PlayerState.Surrendered, "surrender", out string? rejection))
            {
                return CommandResult.Reject(rejection ?? "rejected");
            }
            return CommandResult.Ok;
        }

        /// <summary>Feeds kind and player into a hash.</summary>
        /// <param name="hasher">Hasher to fill.</param>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Player);
        }
    }

    /// <summary>
    /// The network layer (Phase 2) reports that a player's client dropped or came back. Disconnected moves an Active player
    /// to Disconnected (counting <see cref="Teams.Player.DisconnectCount"/>); Connected moves a Disconnected player back to
    /// Active. The disconnect pause itself is Phase 2 (see <see cref="IHandOverPolicy.OnPlayerDisconnected"/>). Normally
    /// issued with issuer None by the host; a player may only report their own connection. Rejections: "unknown_player",
    /// "not_self", "already_active", "already_disconnected", "player_out_of_match".
    /// </summary>
    public sealed class SetConnectionStateCommand : Command
    {
        /// <summary>The player whose connection changed.</summary>
        public PlayerId Player { get; }

        /// <summary>The new connection state.</summary>
        public ConnectionState Connection { get; }

        /// <summary>Builds the command.</summary>
        /// <param name="tick">Tick to apply on, 0 upward.</param>
        /// <param name="issuer">None for the host or network layer; otherwise must equal <paramref name="player"/>.</param>
        /// <param name="sequence">Issuer's command counter.</param>
        /// <param name="player">The player whose connection changed.</param>
        /// <param name="connection">The new connection state.</param>
        public SetConnectionStateCommand(int tick, PlayerId issuer, uint sequence, PlayerId player, ConnectionState connection)
            : base(tick, issuer, sequence)
        {
            Player = player;
            Connection = connection;
        }

        /// <summary>"set_connection_state".</summary>
        public override string Kind => "set_connection_state";

        /// <summary>Checks the issuer, then moves the player to Disconnected or back to Active through <see cref="PlayerTransitions"/>.</summary>
        /// <param name="sim">The stepping simulation.</param>
        public override CommandResult Apply(Simulation sim)
        {
            if (Issuer.IsValid && Issuer != Player)
            {
                return CommandResult.Reject(SurrenderCommand.ReasonNotSelf);
            }
            // True when the client dropped, false when it is (back) online.
            bool disconnected = Connection == ConnectionState.Disconnected;
            // State the player should move to.
            PlayerState target = disconnected ? PlayerState.Disconnected : PlayerState.Active;
            // Cause written into the PlayerStateChangedEvent.
            string cause = disconnected ? "disconnect" : "reconnect";
            if (!PlayerTransitions.TryChange(sim, Player, target, cause, out string? rejection))
            {
                return CommandResult.Reject(rejection ?? "rejected");
            }
            return CommandResult.Ok;
        }

        /// <summary>Feeds kind, player and connection state into a hash.</summary>
        /// <param name="hasher">Hasher to fill.</param>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Player);
            hasher.Write((byte)Connection);
        }
    }
}

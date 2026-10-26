// rts/src/Sim/Teams/Player.cs
// Live, mutable state of one player during a match: the immutable lobby setup plus the player state
// machine position and the disconnect and pause counters. Part of Rts.Sim.Teams. Owned by
// PlayerRegistry; only PlayerTransitions (inside Simulation.Step) and later-phase match rules change it.
// Everything here except the display name feeds the state hash (see WriteHash).
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Teams
{
    /// <summary>One player slot in a running match. Created by <see cref="PlayerRegistry"/>; hosts read it, never write it.</summary>
    public sealed class Player
    {
        /// <summary>The immutable lobby data this player was created from.</summary>
        public PlayerSetup Setup { get; }

        /// <summary>Slot index, 0 to 254, unique within the match. Same as <see cref="PlayerSetup.Id"/>.</summary>
        public PlayerId Id => Setup.Id;

        /// <summary>Team the player belongs to for the whole match, 0 to 254. Same as <see cref="PlayerSetup.Team"/>.</summary>
        public TeamId Team => Setup.Team;

        /// <summary>Faction ID from data/factions, such as "skai". Same as <see cref="PlayerSetup.FactionId"/>.</summary>
        public string FactionId => Setup.FactionId;

        /// <summary>Name shown in the UI. Presentation only; never hashed.</summary>
        public string DisplayName => Setup.DisplayName;

        /// <summary>Current position in the player state machine. Starts <see cref="PlayerState.Active"/>. Hashed.</summary>
        public PlayerState State { get; private set; }

        /// <summary>
        /// Tick on which <see cref="State"/> was entered. Unit: ticks. Range: 0 upward; 0 for the initial Active state.
        /// Hashed, because the Phase 2 disconnect timeout (disconnect_pause_max_seconds, tuning/online.json) counts from it.
        /// </summary>
        public int StateSinceTick { get; private set; }

        /// <summary>
        /// How many times this player has entered <see cref="PlayerState.Disconnected"/>. Unit: count. Range: 0 upward.
        /// Incremented by every Active to Disconnected transition, whether or not the disconnect earned a pause. Hashed.
        /// </summary>
        public int DisconnectCount { get; private set; }

        /// <summary>
        /// Disconnect pauses this player has triggered. Unit: count. Range: 0 to disconnect_pauses_per_player
        /// (tuning/online.json, design value 1). Hashed. Phase 0 never changes it: the Phase 2 pause logic calls
        /// <see cref="RecordDisconnectPause"/> when a disconnect actually pauses the game, and refuses a pause once the
        /// count reaches disconnect_pauses_per_player.
        /// </summary>
        public int DisconnectPausesUsed { get; private set; }

        /// <summary>
        /// Pause time this player has used. Unit: ticks. Range: 0 to pause_budget_seconds_per_player (tuning/online.json,
        /// design value 120 s) converted to ticks with the match tick rate. Hashed. Phase 0 never changes it: Phase 2 adds
        /// to it with <see cref="AddPauseTicks"/> for every tick the game is paused on this player's behalf. Each single
        /// disconnect pause is separately capped by disconnect_pause_max_seconds (tuning/online.json, design value 300 s).
        /// </summary>
        public int PauseTicksUsed { get; private set; }

        /// <summary>True while <see cref="State"/> is <see cref="PlayerState.Active"/>.</summary>
        public bool IsActive => State == PlayerState.Active;

        /// <summary>True while the player still contests the match (Active or Disconnected). See <see cref="PlayerStateRules.IsInMatch"/>.</summary>
        public bool IsInMatch => PlayerStateRules.IsInMatch(State);

        /// <summary>Creates the live player for a slot, Active from tick 0 with all counters at 0.</summary>
        /// <param name="setup">Lobby data; not null.</param>
        internal Player(PlayerSetup setup)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            State = PlayerState.Active;
            StateSinceTick = 0;
        }

        /// <summary>
        /// Moves to <paramref name="state"/>. The caller (<see cref="PlayerTransitions"/>) has already checked the
        /// transition table. Entering Disconnected from another state increments <see cref="DisconnectCount"/>.
        /// </summary>
        /// <param name="state">New state.</param>
        /// <param name="tick">Current tick, 0 upward.</param>
        internal void SetState(PlayerState state, int tick)
        {
            if (state == PlayerState.Disconnected && State != PlayerState.Disconnected)
            {
                DisconnectCount++;
            }
            State = state;
            StateSinceTick = tick;
        }

        /// <summary>
        /// Phase 2 hook: records that a disconnect of this player paused the game. Not called in Phase 0.
        /// TODO(Phase 2): call from the lockstep pause logic when a disconnect pause starts, after checking
        /// <see cref="DisconnectPausesUsed"/> against disconnect_pauses_per_player (tuning/online.json).
        /// </summary>
        internal void RecordDisconnectPause()
        {
            DisconnectPausesUsed++;
        }

        /// <summary>
        /// Phase 2 hook: charges paused time to this player's budget. Not called in Phase 0.
        /// TODO(Phase 2): call once per paused tick (or with the pause length when it ends) and stop the pause when
        /// <see cref="PauseTicksUsed"/> reaches pause_budget_seconds_per_player (tuning/online.json) in ticks.
        /// </summary>
        /// <param name="ticks">Paused ticks to add. Unit: ticks. Range: 0 upward.</param>
        internal void AddPauseTicks(int ticks)
        {
            if (ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks), "Pause time cannot be negative.");
            }
            PauseTicksUsed += ticks;
        }

        /// <summary>
        /// Feeds everything about this player that affects future ticks into the state hash, in a fixed order:
        /// ID, team, faction ID, state, state-since tick, disconnect count, disconnect pauses used, pause ticks used.
        /// The display name is deliberately left out.
        /// </summary>
        /// <param name="hasher">Hasher being filled by <see cref="PlayerRegistry.WriteHash"/>.</param>
        internal void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
            hasher.Write(Team);
            hasher.Write(FactionId);
            hasher.Write((byte)State);
            hasher.Write(StateSinceTick);
            hasher.Write(DisconnectCount);
            hasher.Write(DisconnectPausesUsed);
            hasher.Write(PauseTicksUsed);
        }

        /// <summary>Text such as "P2 (T1, skai, active)" for logs.</summary>
        public override string ToString()
        {
            return Id + " (" + Team + ", " + FactionId + ", " + PlayerStateRules.Name(State) + ")";
        }
    }
}

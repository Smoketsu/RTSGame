// rts/src/Game/Assets/Scripts/Bootstrap/LocalCommands.cs
// The one local command path (player_interface.md UI-0). Owns the local player slot and the single per-player sequence
// counter, and hands every command a panel or the selection controller builds to SimulationHost.Submit, which records it
// for the session replay and queues it in the simulation. Every input script calls Submit here; nothing else submits
// player commands, so two panels can never send the same sequence number. Lives on the SimulationHost object (added by
// AutoBootstrap.EnsureLocalCommands). Part of Rts.Game (Unity side, presentation only).
using System;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using UnityEngine;

namespace Rts.Game.Bootstrap
{
    /// <summary>Local player identity, sequence counter and the single Submit for player commands.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimulationHost))]
    public sealed class LocalCommands : MonoBehaviour
    {
        /// <summary>Player slot this machine controls. Unit: player slot. Range: 0 to 254; must exist in the match (default 0, the first player of the script).</summary>
        [SerializeField, Range(0, 254)] private int localPlayerSlot = 0;

        /// <summary>First sequence number of the commands this machine issues. Unit: sequence number. Range: 0 to 2147483647; default 1000000, above any scripted sequence.</summary>
        [SerializeField, Min(0)] private int firstSequence = 1000000;

        /// <summary>The host on the same object. Range: non-null after Awake.</summary>
        private SimulationHost _host;

        /// <summary>Next sequence number to use. Unit: sequence number. Range: firstSequence upward; wraps never in practice.</summary>
        private uint _nextSequence;

        /// <summary>True once the slot has been checked against the running match. Unit: on/off.</summary>
        private bool _checked;

        /// <summary>True when the checked slot exists in the match; commands are only issued then. Unit: on/off.</summary>
        private bool _valid;

        /// <summary>True once a dropped command for an AI-played local slot has been logged, so it is logged once. Unit: on/off.</summary>
        private bool _aiSlotWarned;

        /// <summary>
        /// The player this machine commands: the skirmish's human slot when the host started a skirmish, else the Inspector slot.
        /// Range: a player slot; only meaningful while <see cref="IsReady"/>.
        /// </summary>
        public PlayerId LocalPlayer => new PlayerId((byte)(_host != null && _host.LocalSlotOverride.HasValue ? _host.LocalSlotOverride.Value : localPlayerSlot));

        /// <summary>The host whose match the commands go to. Range: non-null after Awake.</summary>
        public SimulationHost Host => _host;

        /// <summary>True while the match runs and the local slot exists in it; the only state in which commands may be built. Unit: on/off.</summary>
        public bool IsReady
        {
            get
            {
                if (_host == null || _host.Match == null)
                {
                    return false;
                }
                if (!_checked)
                {
                    _checked = true;
                    _valid = _host.Match.Players.Contains(LocalPlayer);
                    if (!_valid)
                    {
                        Debug.LogWarning("[LocalCommands] Player slot " + localPlayerSlot + " does not exist in this match, so orders are disabled. Set 'Local Player Slot' to a slot of the script.");
                    }
                }
                return _valid;
            }
        }

        /// <summary>The tick new commands are scheduled for: the current clock tick, which the next step applies. Unit: ticks. Range: 0 upward; only valid while <see cref="IsReady"/>.</summary>
        public int Tick => _host.Match.Clock.Tick;

        /// <summary>Finds the host and sets the first sequence number.</summary>
        private void Awake()
        {
            _host = GetComponent<SimulationHost>();
            _nextSequence = (uint)firstSequence;
        }

        /// <summary>Returns the next sequence number of the local player and advances the counter. Unit: sequence number.</summary>
        public uint NextSequence()
        {
            return _nextSequence++;
        }

        /// <summary>
        /// Sends a command through the host (recorded, then queued). A refusal at submission (a past tick, a command the replay codec
        /// cannot encode: host bugs, not game rules) is logged, not thrown; game-rule refusals arrive later as CommandRejectedEvent.
        /// </summary>
        /// <param name="command">A command built for <see cref="Tick"/> with <see cref="NextSequence"/> and issued by <see cref="LocalPlayer"/>.</param>
        public void Submit(Command command)
        {
            if (_host.IsPlayback)
            {
                // A replay plays its recorded commands only; input must never reach its simulation (player_interface_2.md UI-15).
                return;
            }
            if (_host.IsAiPlayer(LocalPlayer))
            {
                // The local slot is played by an AI (a script with an "ai" key on it): input must not compete with it.
                if (!_aiSlotWarned)
                {
                    _aiSlotWarned = true;
                    Debug.LogWarning("[LocalCommands] Player " + LocalPlayer + " is played by an AI, so local orders are dropped.");
                }
                return;
            }
            try
            {
                _host.Submit(command);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                Debug.LogWarning("[LocalCommands] Submit refused " + command + ": " + exception.Message);
            }
        }
    }
}

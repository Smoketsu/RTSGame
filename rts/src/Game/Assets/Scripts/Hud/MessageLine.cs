// rts/src/Game/Assets/Scripts/Hud/MessageLine.cs
// The HUD message line, top centre (player_interface.md UI-0): the last few refusals of the local player's commands and the
// notices of each package, each shown for a few seconds. Reads SimulationHost.EventRaised: CommandRejectedEvent whose command the
// local player issued (worded by RejectionTexts), and the notice events of each package (UI-1: own HQ state changes and Last
// Stand, depletion of a field an own worker gathers from; UI-2: units trained by own producers, own research completed; UI-3:
// own build orders cancelled with their refund, own structures placed and completed; UI-4: own wall tiles skipped). Other HUD code may post with Show. Builds its own uGUI Text under the
// HUD canvas (AutoBootstrap.EnsureHud). Never writes to the simulation. Part of Rts.Game (presentation only).
using System.Collections.Generic;
using System.Text;
using Rts.Game.Bootstrap;
using Rts.Sim;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Shows recent local rejections and notices at the top centre of the screen.</summary>
    [DisallowMultipleComponent]
    public sealed class MessageLine : MonoBehaviour
    {
        /// <summary>One line on screen and when it goes away.</summary>
        private struct Entry
        {
            /// <summary>Text shown. Range: non-empty.</summary>
            public string Text;

            /// <summary>Real time at which the line is removed. Unit: seconds of Time.unscaledTime.</summary>
            public float ExpiresAt;
        }

        /// <summary>How long each line stays. Unit: seconds (real time). Range: 0.5 to 20; default 4.</summary>
        [SerializeField, Range(0.5f, 20f)] private float secondsPerMessage = 4f;

        /// <summary>Most lines shown at once; the oldest is dropped first. Unit: lines. Range: 1 to 10; default 4.</summary>
        [SerializeField, Range(1, 10)] private int maxLines = 4;

        /// <summary>Distance of the line block's top edge below the top of the screen, leaving room for the timeline bar and Core widget. Unit: reference pixels (1920 x 1080). Range: 0 to 540; default 150.</summary>
        [SerializeField, Range(0f, 540f)] private float topOffset = 150f;

        /// <summary>Font size of the lines. Unit: reference pixels. Range: 10 to 48; default 22.</summary>
        [SerializeField, Range(10, 48)] private int fontSize = 22;

        /// <summary>Lines currently shown, oldest first. Range: 0 to maxLines entries.</summary>
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>Reused builder for the joined text. Presentation only.</summary>
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>The uGUI text that draws the lines. Range: non-null after Awake.</summary>
        private Text _text;

        /// <summary>The local command path, for the local player and the host's events. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>True once subscribed to the host's events. Unit: on/off.</summary>
        private bool _subscribed;

        /// <summary>True when the lines changed since the text was last rebuilt. Unit: on/off.</summary>
        private bool _dirty;

        /// <summary>Adds a line, dropping the oldest when the block is full.</summary>
        /// <param name="message">Text to show; null or empty is ignored.</param>
        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            if (_entries.Count >= maxLines)
            {
                _entries.RemoveAt(0);
            }
            _entries.Add(new Entry { Text = message, ExpiresAt = Time.unscaledTime + secondsPerMessage });
            _dirty = true;
        }

        /// <summary>Builds the Text child, anchored top centre.</summary>
        private void Awake()
        {
            // Block size: wide enough for one long refusal per line. Unit: reference pixels.
            Vector2 size = new Vector2(1000f, fontSize * 1.4f * maxLines);
            _text = HudBuild.Text(transform, "Message Line Text", new Vector2(0.5f, 1f), new Vector2(0f, -topOffset), size, fontSize, TextAnchor.UpperCenter);
        }

        /// <summary>Subscribes once the host exists and removes expired lines.</summary>
        private void Update()
        {
            if (!_subscribed)
            {
                if (_commands == null)
                {
                    _commands = FindAnyObjectByType<LocalCommands>();
                }
                if (_commands != null && _commands.Host != null)
                {
                    _commands.Host.EventRaised += OnSimEvent;
                    _subscribed = true;
                }
            }
            // Real time now. Unit: seconds.
            float now = Time.unscaledTime;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].ExpiresAt <= now)
                {
                    _entries.RemoveAt(i);
                    _dirty = true;
                }
            }
            if (_dirty)
            {
                _dirty = false;
                _builder.Clear();
                foreach (Entry entry in _entries)
                {
                    _builder.Append(entry.Text).Append('\n');
                }
                _text.text = _builder.ToString();
            }
        }

        /// <summary>Unsubscribes from the host.</summary>
        private void OnDestroy()
        {
            if (_subscribed && _commands != null && _commands.Host != null)
            {
                _commands.Host.EventRaised -= OnSimEvent;
            }
        }

        /// <summary>Shows refusals of the local player's own commands and the local player's notices; everything else is ignored.</summary>
        /// <param name="simEvent">An event of the tick that just ran.</param>
        private void OnSimEvent(SimEvent simEvent)
        {
            if (!_commands.IsReady)
            {
                return;
            }
            Simulation sim = _commands.Host.Match;
            if (simEvent is CommandRejectedEvent rejected)
            {
                if (rejected.Command.Issuer == _commands.LocalPlayer)
                {
                    Show(RejectionTexts.For(sim, rejected));
                }
            }
            else if (simEvent is HqStateChangedEvent hqChanged)
            {
                if (TryGetOwn(sim, hqChanged.Unit, out Unit hq))
                {
                    Show(hq.Definition.DisplayName + ": " + hqChanged.To);
                }
            }
            else if (simEvent is HqLastStandEvent lastStand)
            {
                if (TryGetOwn(sim, lastStand.Unit, out Unit hq))
                {
                    Show(hq.Definition.DisplayName + ": Last Stand (" + HudBuild.Seconds(lastStand.InvulnerableTicks, sim.Clock.TicksPerSecond) + ")");
                }
            }
            else if (simEvent is UnitTrainedEvent trained)
            {
                if (TryGetOwn(sim, trained.Producer, out Unit _))
                {
                    Show(HudBuild.NameOf(sim, trained.DefinitionId) + " ready");
                }
            }
            else if (simEvent is ResearchCompletedEvent research)
            {
                // Only the local player's own research is ever shown (research_and_unlocks.md decision 6).
                if (research.Owner == _commands.LocalPlayer)
                {
                    Show("Research complete: " + HudBuild.NameOf(sim, research.ResearchId));
                }
            }
            else if (simEvent is BuildOrderCancelledEvent cancelled)
            {
                // A replaced order (new_order, also what Stop gives) names no reason, but its refund is still shown: the spec's table calls
                // new_order silent while its UI-3 check expects a refund notice on Stop; showing only the refund satisfies both.
                string reason = RejectionTexts.For(cancelled.Reason);
                string refund = RefundText(cancelled.RefundCommon, cancelled.RefundRare);
                if (cancelled.Owner == _commands.LocalPlayer && (reason != null || refund.Length > 0))
                {
                    Show(HudBuild.NameOf(sim, cancelled.DefinitionId) + " cancelled" + (reason != null ? ": " + reason : "") + refund);
                }
            }
            else if (simEvent is BuildLineTileSkippedEvent skipped)
            {
                // A tile dropped because the worker got a new order is the player's own doing and stays silent.
                string reason = RejectionTexts.For(skipped.Reason);
                if (skipped.Owner == _commands.LocalPlayer && reason != null)
                {
                    Show(HudBuild.NameOf(sim, skipped.DefinitionId) + " tile skipped: " + reason + RefundText(skipped.RefundCommon, skipped.RefundRare));
                }
            }
            else if (simEvent is StructurePlacedEvent placed)
            {
                if (TryGetOwn(sim, placed.Structure, out Unit _))
                {
                    Show(HudBuild.NameOf(sim, placed.DefinitionId) + " placed");
                }
            }
            else if (simEvent is StructureCompletedEvent completed)
            {
                if (TryGetOwn(sim, completed.Structure, out Unit _))
                {
                    Show(HudBuild.NameOf(sim, completed.DefinitionId) + " complete");
                }
            }
            else if (simEvent is FieldDepletedEvent depleted)
            {
                if (OwnWorkerGathersFrom(sim, depleted.FieldIndex))
                {
                    Show("A " + depleted.Resource + " field ran out");
                }
            }
        }

        /// <summary>" (refund 50c 0r)" for a refund, or empty when nothing was refunded.</summary>
        /// <param name="common">Common refunded. Unit: resource units. Range: 0 upward.</param>
        /// <param name="rare">Rare refunded. Unit: resource units. Range: 0 upward.</param>
        private static string RefundText(int common, int rare)
        {
            return common > 0 || rare > 0 ? " (refund " + common + "c " + rare + "r)" : "";
        }

        /// <summary>Finds a living unit of the local player.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="id">The unit.</param>
        /// <param name="unit">The unit when it lives and is the local player's; otherwise null.</param>
        private bool TryGetOwn(Simulation sim, Rts.Sim.Core.EntityId id, out Unit unit)
        {
            return sim.Units.TryGet(id, out unit) && unit.Owner == _commands.LocalPlayer;
        }

        /// <summary>
        /// True when a worker of the local player is assigned to the field. shortcut: reads the assignment after the depleting tick, so a
        /// worker the simulation already moved off the field that same tick is missed; upgrade when the event names the gatherers.
        /// </summary>
        /// <param name="sim">The running match.</param>
        /// <param name="fieldIndex">Index of the depleted field. Range: 0 upward.</param>
        private bool OwnWorkerGathersFrom(Simulation sim, int fieldIndex)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == _commands.LocalPlayer && unit.GatherState != GatherState.Idle && unit.GatherFieldIndex == fieldIndex)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

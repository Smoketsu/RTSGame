// rts/src/Game/Assets/Scripts/Hud/CoreWidget.cs
// The HUD Core widget, under the timeline bar (player_interface.md UI-9): one line naming the Core holder, the team capturing it
// with its progress (CaptureProgressTicks / CaptureTicks), and "contested" while IsContested; and, when Core wins are on
// (IsCoreWinEnabled), one bar per team filling HoldTicks(team) / WinTicks in the team's colour. Shown while cycles are on and the map
// has a Core. Every number is read from the simulation when drawn. Part of Rts.Game (presentation only).
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Game.Presentation;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Cycles;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Core holder, capture progress, contested state and hold bars.</summary>
    [DisallowMultipleComponent]
    public sealed class CoreWidget : MonoBehaviour
    {
        /// <summary>Widget width. Unit: reference pixels.</summary>
        private const float Width = 520f;

        /// <summary>Height of one hold bar. Unit: reference pixels.</summary>
        private const float BarHeight = 8f;

        /// <summary>Most teams with a hold bar. Unit: teams.</summary>
        private const int MaxBars = 8;

        /// <summary>The widget background. Range: non-null after Awake.</summary>
        private Image _panel;

        /// <summary>The status line. Range: non-null after Awake.</summary>
        private Text _text;

        /// <summary>Hold bar fills, one per team slot. Range: MaxBars after Awake.</summary>
        private readonly List<Image> _bars = new List<Image>();

        /// <summary>The local command path. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>Builds the panel, the line and the hold bars, hidden.</summary>
        private void Awake()
        {
            _panel = HudBuild.Image(transform, "Core Widget", new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(Width, 32f), HudBuild.PanelColor);
            _text = HudBuild.Text(_panel.transform, "Core Text", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(Width - 16f, 24f), 18, TextAnchor.UpperCenter);
            for (int i = 0; i < MaxBars; i++)
            {
                Image back = HudBuild.Image(_panel.transform, "Hold " + i, new Vector2(0.5f, 1f), new Vector2(0f, -32f - i * (BarHeight + 3f)), new Vector2(Width - 40f, BarHeight), new Color(0f, 0f, 0f, 0.6f));
                _bars.Add(HudBuild.Image(back.transform, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, BarHeight), Color.white));
                back.gameObject.SetActive(false);
            }
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Refreshes the line and the bars.</summary>
        private void Update()
        {
            if (_commands == null)
            {
                _commands = FindAnyObjectByType<LocalCommands>();
            }
            bool visible = _commands != null && _commands.IsReady && _commands.Host.Match.Cycles.IsEnabled && _commands.Host.Match.Map.Core != null;
            _panel.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            Simulation sim = _commands.Host.Match;
            CycleSystem cycles = sim.Cycles;
            TeamId local = sim.Players.TeamOf(_commands.LocalPlayer);
            string text = "Core: " + HudBuild.TeamLabel(sim, local, cycles.Holder);
            if (cycles.CaptureTeam.IsValid && cycles.CaptureTicks > 0)
            {
                text += "    capturing: " + HudBuild.TeamLabel(sim, local, cycles.CaptureTeam) + " " + (100 * cycles.CaptureProgressTicks / cycles.CaptureTicks) + "%";
            }
            if (cycles.IsContested)
            {
                text += "    contested";
            }
            _text.text = text;
            // Teams with a hold bar this frame. Range: 0 to MaxBars.
            int shown = cycles.IsCoreWinEnabled ? Mathf.Min(MaxBars, sim.Players.Teams.Count) : 0;
            for (int i = 0; i < _bars.Count; i++)
            {
                bool on = i < shown;
                _bars[i].transform.parent.gameObject.SetActive(on);
                if (on)
                {
                    TeamId team = sim.Players.Teams[i];
                    float fraction = cycles.WinTicks > 0 ? Mathf.Clamp01((float)cycles.HoldTicks(team) / cycles.WinTicks) : 0f;
                    _bars[i].color = CoreView.TeamColor(sim, team);
                    _bars[i].rectTransform.sizeDelta = new Vector2((Width - 40f) * fraction, BarHeight);
                }
            }
            _panel.rectTransform.sizeDelta = new Vector2(Width, 32f + shown * (BarHeight + 3f));
        }
    }
}

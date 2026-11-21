// rts/src/Game/Assets/Scripts/Hud/TimelineBar.cs
// The HUD timeline bar and cycle pick, top centre (player_interface.md UI-8). Shown while Planetary Cycles are on
// (sim.Cycles.IsEnabled). A text line names the active cycle, the next cycle once it is locked (or the local team's pending pick),
// the time to the lock and to the shift, and who holds the Core. Under it a strip covers the next IntervalTicks from now: a marker
// at NextLockTick and one at NextShiftTick, and, when the flooding mechanic runs, the tide level as a row of bars with "L" and "H"
// marks at the low and high turns (TideClock). While the local team holds the Core and the next cycle is not locked, one button per
// AllowedNextCycles entry (in that order) sends PickCycleCommand. Every number is read from the simulation when drawn.
// Part of Rts.Game (presentation only).
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Cycles;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Water;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Cycle and tide timeline with the cycle pick buttons.</summary>
    [DisallowMultipleComponent]
    public sealed class TimelineBar : MonoBehaviour
    {
        /// <summary>Panel width. Unit: reference pixels.</summary>
        private const float Width = 860f;

        /// <summary>Panel height. Unit: reference pixels.</summary>
        private const float Height = 112f;

        /// <summary>Strip width (the time window). Unit: reference pixels.</summary>
        private const float StripWidth = 820f;

        /// <summary>Strip height. Unit: reference pixels.</summary>
        private const float StripHeight = 26f;

        /// <summary>Tide bars across the strip. Unit: bars.</summary>
        private const int TideSamples = 90;

        /// <summary>Most pick buttons shown. Unit: buttons.</summary>
        private const int MaxButtons = 4;

        /// <summary>Colour of the lock marker. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color LockColor = new Color(1f, 0.8f, 0.3f, 1f);

        /// <summary>Colour of the shift marker. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color ShiftColor = new Color(1f, 0.35f, 0.3f, 1f);

        /// <summary>Colour of the tide bars. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color TideColor = new Color(0.3f, 0.6f, 1f, 0.7f);

        /// <summary>The panel; hidden while cycles are off. Range: non-null after Awake.</summary>
        private Image _panel;

        /// <summary>The text line. Range: non-null after Awake.</summary>
        private Text _text;

        /// <summary>The strip background. Range: non-null after Awake.</summary>
        private RectTransform _strip;

        /// <summary>Lock marker on the strip. Range: non-null after Awake.</summary>
        private RectTransform _lockMarker;

        /// <summary>Shift marker on the strip. Range: non-null after Awake.</summary>
        private RectTransform _shiftMarker;

        /// <summary>Tide bars, left to right. Range: TideSamples after Awake.</summary>
        private readonly List<RectTransform> _tideBars = new List<RectTransform>();

        /// <summary>"L" / "H" turn labels, reused. Range: 0 to the most turns one window held.</summary>
        private readonly List<Text> _turnLabels = new List<Text>();

        /// <summary>Pick buttons and their labels. Range: MaxButtons after Awake.</summary>
        private readonly List<(Button Button, Text Label)> _buttons = new List<(Button, Text)>();

        /// <summary>Cycle ID behind each shown pick button, by button index. Range: MaxButtons entries.</summary>
        private readonly string[] _buttonCycles = new string[MaxButtons];

        /// <summary>Tick the tide row was last drawn for. Unit: ticks. Range: -1 (never) upward.</summary>
        private int _tideDrawnTick = -1;

        /// <summary>The local command path. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>Builds the panel, text, strip, markers, tide bars and pick buttons, hidden.</summary>
        private void Awake()
        {
            _panel = HudBuild.Image(transform, "Timeline Bar", new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(Width, Height), HudBuild.PanelColor);
            _panel.raycastTarget = true;
            Transform panel = _panel.transform;
            _text = HudBuild.Text(panel, "Timeline Text", new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(Width - 24f, 26f), 19, TextAnchor.UpperCenter);
            _strip = HudBuild.Image(panel, "Timeline Strip", new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(StripWidth, StripHeight), new Color(0f, 0f, 0f, 0.5f)).rectTransform;
            // Width of one tide bar. Unit: reference pixels.
            float barWidth = StripWidth / TideSamples;
            for (int i = 0; i < TideSamples; i++)
            {
                _tideBars.Add(HudBuild.Image(_strip, "Tide " + i, new Vector2(0f, 0f), new Vector2(i * barWidth, 0f), new Vector2(barWidth, 0f), TideColor).rectTransform);
            }
            _lockMarker = HudBuild.Image(_strip, "Lock Marker", new Vector2(0f, 0f), Vector2.zero, new Vector2(4f, StripHeight), LockColor).rectTransform;
            _shiftMarker = HudBuild.Image(_strip, "Shift Marker", new Vector2(0f, 0f), Vector2.zero, new Vector2(4f, StripHeight), ShiftColor).rectTransform;
            for (int i = 0; i < MaxButtons; i++)
            {
                // Button position, left to right along the bottom of the panel. Unit: reference pixels.
                Vector2 position = new Vector2((i - (MaxButtons - 1) * 0.5f) * 205f, 6f);
                Image background = HudBuild.Image(panel, "Pick " + i, new Vector2(0.5f, 0f), position, new Vector2(198f, 36f), new Color(0.38f, 0.24f, 0.5f, 1f));
                background.raycastTarget = true;
                Button button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                // Captured copy of the button index for the click handler.
                int captured = i;
                button.onClick.AddListener(() => Pick(captured));
                Text label = HudBuild.Text(background.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 32f), 16, TextAnchor.MiddleCenter);
                background.gameObject.SetActive(false);
                _buttons.Add((button, label));
            }
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Refreshes the text, markers, tide row and pick buttons from the simulation.</summary>
        private void Update()
        {
            if (_commands == null)
            {
                _commands = FindAnyObjectByType<LocalCommands>();
            }
            bool visible = _commands != null && _commands.IsReady && _commands.Host.Match.Cycles.IsEnabled;
            _panel.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            Simulation sim = _commands.Host.Match;
            CycleSystem cycles = sim.Cycles;
            int now = sim.Clock.Tick;
            int rate = sim.Clock.TicksPerSecond;
            TeamId team = sim.Players.TeamOf(_commands.LocalPlayer);
            // What the bar says about the next cycle: locked, the local team's own pending pick, or unknown.
            string next = cycles.IsNextLocked && cycles.NextCycle != null ? cycles.NextCycle.DisplayName
                : cycles.PendingPick != null && cycles.PendingPickTeam == team ? cycles.PendingPick.DisplayName + " (your pick)" : "?";
            string holder = !cycles.Holder.IsValid ? "nobody" : cycles.Holder == team ? "your team" : "the enemy";
            _text.text = (cycles.ActiveCycle != null ? cycles.ActiveCycle.DisplayName : "No cycle") + "    Next: " + next
                + (cycles.IsNextLocked ? "    Locked" : "    Lock in " + Clock(cycles.NextLockTick - now, rate)) + ", shift in " + Clock(cycles.NextShiftTick - now, rate) + "    Core: " + holder;
            // Window shown by the strip. Unit: ticks. Range: 1 upward.
            int window = Mathf.Max(1, cycles.IntervalTicks);
            PlaceMarker(_lockMarker, cycles.NextLockTick - now, window);
            PlaceMarker(_shiftMarker, cycles.NextShiftTick - now, window);
            if (now != _tideDrawnTick && (now - _tideDrawnTick >= rate || _tideDrawnTick < 0))
            {
                DrawTide(sim, now, window);
            }
            DrawButtons(cycles, team);
        }

        /// <summary>Puts a marker at its offset from now along the strip; hidden when outside the window.</summary>
        /// <param name="marker">The marker.</param>
        /// <param name="offset">Ticks from now. Unit: ticks.</param>
        /// <param name="window">Ticks the strip covers. Unit: ticks.</param>
        private static void PlaceMarker(RectTransform marker, int offset, int window)
        {
            bool inside = offset >= 0 && offset <= window;
            marker.gameObject.SetActive(inside);
            marker.anchoredPosition = new Vector2(StripWidth * offset / window - 2f, 0f);
        }

        /// <summary>Draws the tide level as bars across the window and labels the low and high turns; hidden without the flooding mechanic.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="now">Current tick. Unit: ticks.</param>
        /// <param name="window">Ticks the strip covers. Unit: ticks.</param>
        private void DrawTide(Simulation sim, int now, int window)
        {
            _tideDrawnTick = now;
            FloodingMechanic flooding = sim.Mechanics.TryGetValue(FloodingMechanic.MechanicId, out IFactionMechanic mechanic) ? mechanic as FloodingMechanic : null;
            for (int i = 0; i < _tideBars.Count; i++)
            {
                // Tide level at this bar's tick. Unit: fraction. Range: 0 to 1.
                float level = flooding != null ? Mathf.Clamp01(flooding.TideLevel(sim, now + window * i / TideSamples).ToFloatForPresentation()) : 0f;
                _tideBars[i].sizeDelta = new Vector2(_tideBars[i].sizeDelta.x, StripHeight * level);
            }
            int used = 0;
            if (flooding != null && flooding.TideTicks > 0)
            {
                for (int offset = 0; offset <= window; offset++)
                {
                    int tick = now + offset;
                    bool low = TideClock.IsLowTurn(tick, flooding.TideTicks);
                    if (low || TideClock.IsHighTurn(tick, flooding.TideTicks))
                    {
                        Text label = TurnLabel(used++);
                        label.text = low ? "L" : "H";
                        label.rectTransform.anchoredPosition = new Vector2(StripWidth * offset / window - 6f, StripHeight - 2f);
                    }
                }
            }
            for (int i = used; i < _turnLabels.Count; i++)
            {
                _turnLabels[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Turn label number <paramref name="index"/>, created when the pool is too small, shown.</summary>
        /// <param name="index">Pool index. Range: 0 to the pool size.</param>
        private Text TurnLabel(int index)
        {
            if (index == _turnLabels.Count)
            {
                _turnLabels.Add(HudBuild.Text(_strip, "Turn " + index, new Vector2(0f, 0f), Vector2.zero, new Vector2(14f, 18f), 14, TextAnchor.MiddleCenter));
            }
            _turnLabels[index].gameObject.SetActive(true);
            return _turnLabels[index];
        }

        /// <summary>Shows one pick button per allowed next cycle while the local team holds the Core and the next cycle is open; hides them otherwise.</summary>
        /// <param name="cycles">The cycle system.</param>
        /// <param name="team">The local team.</param>
        private void DrawButtons(CycleSystem cycles, TeamId team)
        {
            IReadOnlyList<CycleDefinition> allowed = cycles.Holder == team && cycles.Holder.IsValid && !cycles.IsNextLocked ? cycles.AllowedNextCycles() : null;
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool shown = allowed != null && i < allowed.Count;
                _buttons[i].Button.gameObject.SetActive(shown);
                _buttonCycles[i] = shown ? allowed[i].Id : null;
                if (shown)
                {
                    bool picked = cycles.PendingPick != null && cycles.PendingPick.Id == allowed[i].Id && cycles.PendingPickTeam == team;
                    _buttons[i].Label.text = (picked ? "> " : "") + allowed[i].DisplayName;
                }
            }
        }

        /// <summary>Sends PickCycleCommand for the cycle behind a shown button.</summary>
        /// <param name="index">Button index. Range: 0 to MaxButtons - 1.</param>
        public void Pick(int index)
        {
            string cycleId = _buttonCycles[index];
            if (_commands == null || !_commands.IsReady || string.IsNullOrEmpty(cycleId))
            {
                return;
            }
            _commands.Submit(new PickCycleCommand(_commands.Tick, _commands.LocalPlayer, _commands.NextSequence(), cycleId));
        }

        /// <summary>A tick count as m:ss, 0:00 for negative counts. Unit: input ticks, output minutes and seconds.</summary>
        /// <param name="ticks">Tick count.</param>
        /// <param name="rate">Ticks per second.</param>
        private static string Clock(int ticks, int rate)
        {
            int seconds = Mathf.Max(0, ticks) / rate;
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }
    }
}

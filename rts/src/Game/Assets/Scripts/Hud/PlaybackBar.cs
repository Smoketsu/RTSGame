// rts/src/Game/Assets/Scripts/Hud/PlaybackBar.cs
// The replay playback bar (player_interface_2.md UI-15), shown top left only while the host plays a replay: tick and match time out
// of the replay's length, the speed, the viewed player, buttons for pause, slower, faster, next player and Main Menu, and the
// verdict line. Space pauses, minus and plus (main row or keypad) step the speed through 0.5, 1, 2, 4 and 8 times real time, Tab
// cycles the viewed player (the resource bar and selection follow LocalPlayer). At the end the verdict compares the final state hash
// with the recorded one; at the first checkpoint that differs it shows the desync tick and stepping stops. Speed only scales how much
// real time becomes ticks (SimulationHost.PlaybackSpeed), never the simulation. Part of Rts.Game (presentation only).
using Rts.Game.Bootstrap;
using Rts.Game.Controls;
using Rts.Replay;
using Rts.Sim.Teams;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Speed, pause, viewpoint and verdict of a replay being played.</summary>
    [DisallowMultipleComponent]
    public sealed class PlaybackBar : MonoBehaviour
    {
        /// <summary>The speed steps. Unit: times real time. Range: 0.5 to 8, ascending.</summary>
        private static readonly float[] Speeds = { 0.5f, 1f, 2f, 4f, 8f };

        /// <summary>Index of 1x in Speeds, the speed a replay starts at.</summary>
        private const int NormalSpeed = 1;

        /// <summary>The bar. Range: non-null after Awake.</summary>
        private GameObject _panel;

        /// <summary>Tick, time, speed and viewed player line. Range: non-null after Awake.</summary>
        private Text _status;

        /// <summary>Verdict line: playing, final hash match or difference, or desync. Range: non-null after Awake.</summary>
        private Text _verdict;

        /// <summary>Label of the pause button ("Pause" or "Play"). Range: non-null after Awake.</summary>
        private Text _pauseLabel;

        /// <summary>Current index into Speeds. Range: 0 to Speeds.Length - 1.</summary>
        private int _speedIndex = NormalSpeed;

        /// <summary>True while paused (speed 0). Unit: on/off.</summary>
        private bool _paused;

        /// <summary>The host. Range: null until found.</summary>
        private SimulationHost _host;

        /// <summary>Builds the bar, hidden.</summary>
        private void Awake()
        {
            Image panel = HudBuild.Image(transform, "Playback Bar", new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(470f, 116f), HudBuild.PanelColor);
            panel.raycastTarget = true;
            _panel = panel.gameObject;
            _status = HudBuild.Text(_panel.transform, "Status", new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(450f, 24f), 18, TextAnchor.UpperLeft);
            _verdict = HudBuild.Text(_panel.transform, "Verdict", new Vector2(0f, 1f), new Vector2(10f, -32f), new Vector2(450f, 24f), 16, TextAnchor.UpperLeft);
            Button pause = HudBuild.Button(_panel.transform, "Pause", new Vector2(0f, 0f), new Vector2(10f, 10f), new Vector2(80f, 40f), "Pause", TogglePause);
            _pauseLabel = pause.GetComponentInChildren<Text>();
            HudBuild.Button(_panel.transform, "Slower", new Vector2(0f, 0f), new Vector2(96f, 10f), new Vector2(44f, 40f), "-", () => ChangeSpeed(-1));
            HudBuild.Button(_panel.transform, "Faster", new Vector2(0f, 0f), new Vector2(146f, 10f), new Vector2(44f, 40f), "+", () => ChangeSpeed(1));
            HudBuild.Button(_panel.transform, "Next Player", new Vector2(0f, 0f), new Vector2(196f, 10f), new Vector2(120f, 40f), "Player (Tab)", NextPlayer);
            HudBuild.Button(_panel.transform, "Main Menu", new Vector2(0f, 0f), new Vector2(322f, 10f), new Vector2(138f, 40f), "Main Menu", () => MatchLauncher.Load(null));
            _panel.SetActive(false);
        }

        /// <summary>Shows the bar during playback, handles its keys and refreshes the lines.</summary>
        private void Update()
        {
            if (_host == null)
            {
                _host = FindAnyObjectByType<SimulationHost>();
            }
            bool show = _host != null && _host.IsPlayback;
            _panel.SetActive(show);
            if (!show)
            {
                return;
            }
            if (GameInput.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }
            if (GameInput.GetKeyDown(KeyCode.Minus) || GameInput.GetKeyDown(KeyCode.KeypadMinus))
            {
                ChangeSpeed(-1);
            }
            if (GameInput.GetKeyDown(KeyCode.Equals) || GameInput.GetKeyDown(KeyCode.KeypadPlus))
            {
                ChangeSpeed(1);
            }
            if (GameInput.GetKeyDown(KeyCode.Tab))
            {
                NextPlayer();
            }
            _host.PlaybackSpeed = _paused ? 0f : Speeds[_speedIndex];

            ReplayPlayback playback = _host.Playback;
            int ticksPerSecond = playback.Sim.Clock.TicksPerSecond;
            // Ticks run so far. Range: 0 to the replay's tick count.
            int tick = playback.Sim.Clock.Tick;
            string speed = _paused ? "paused" : Speeds[_speedIndex].ToString("0.#") + "x";
            _status.text = "Replay  tick " + tick + " / " + playback.TickCount + "  " + Clock(tick, ticksPerSecond) + " / " + Clock(playback.TickCount, ticksPerSecond)
                + "  " + speed + "  viewing " + ViewedName();
            _pauseLabel.text = _paused ? "Play" : "Pause";
            if (playback.DesyncTick >= 0)
            {
                _verdict.text = "Desync at tick " + playback.DesyncTick + ": hash " + playback.ActualHash.ToString("x16") + ", recorded " + playback.ExpectedHash.ToString("x16");
            }
            else if (playback.IsFinished)
            {
                _verdict.text = "Final hash " + playback.FinalHash.ToString("x16") + " / recorded " + _host.PlaybackData.FinalHash.ToString("x16")
                    + (playback.FinalMatches ? ": match" : ": differs");
            }
            else
            {
                _verdict.text = "Playing; every checkpoint so far matches.";
            }
        }

        /// <summary>Pauses or resumes stepping.</summary>
        private void TogglePause()
        {
            _paused = !_paused;
        }

        /// <summary>Steps the speed down or up one step (clamped) and resumes if paused.</summary>
        /// <param name="direction">-1 slower, +1 faster.</param>
        private void ChangeSpeed(int direction)
        {
            _speedIndex = Mathf.Clamp(_speedIndex + direction, 0, Speeds.Length - 1);
            _paused = false;
        }

        /// <summary>Views the next player of the replay (wrapping), so the resource bar shows theirs.</summary>
        private void NextPlayer()
        {
            if (_host == null || _host.PlaybackData == null || _host.PlaybackData.Players.Count == 0)
            {
                return;
            }
            var players = _host.PlaybackData.Players;
            // Index of the viewed player in the replay's list; -1 when none matches, so the next is the first.
            int current = -1;
            for (int i = 0; i < players.Count; i++)
            {
                if (_host.LocalSlotOverride == players[i].Id.Value)
                {
                    current = i;
                }
            }
            _host.ViewPlayer(players[(current + 1) % players.Count].Id.Value);
        }

        /// <summary>Display name of the viewed player, or its slot when the replay does not list it.</summary>
        private string ViewedName()
        {
            foreach (PlayerSetup player in _host.PlaybackData.Players)
            {
                if (_host.LocalSlotOverride == player.Id.Value)
                {
                    return player.DisplayName;
                }
            }
            return "slot " + _host.LocalSlotOverride;
        }

        /// <summary>A tick count as minutes and seconds, "m:ss". Unit: input ticks.</summary>
        /// <param name="ticks">Tick count. Range: 0 upward.</param>
        /// <param name="ticksPerSecond">The match's tick rate. Range: 10 to 60.</param>
        public static string Clock(int ticks, int ticksPerSecond)
        {
            // Whole seconds. Range: 0 upward.
            int seconds = ticks / ticksPerSecond;
            return seconds / 60 + ":" + (seconds % 60).ToString("00");
        }
    }
}

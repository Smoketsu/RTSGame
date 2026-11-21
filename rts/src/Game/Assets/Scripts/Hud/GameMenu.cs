// rts/src/Game/Assets/Scripts/Hud/GameMenu.cs
// The game menu and match end screen (player_interface.md UI-10, player_interface_2.md UI-13). F10 opens a centred menu with
// Surrender, Restart, Main Menu and Resume; Restart reloads the same request (a new seed when its Random switch was on) and Main Menu
// returns to the main menu, both through MatchLauncher (the replay of the match so far is written when the host is destroyed). Surrender asks
// to confirm, and Yes sends SurrenderCommand(local, local) through LocalCommands. When the match is decided (SimulationHost.Outcome,
// set after the deciding step, when stepping stops and the replay is written) the screen shows Victory if the local team won, Draw
// when nobody is left, else Defeat, with the cause of the last player who left the match, and Restart, Main Menu and Quit. If the
// local player is out but the match goes on, it shows "Defeated" with Keep Watching (hides the screen; orders are then refused by the
// simulation) and Main Menu. Quit stops Play Mode in the editor and quits a player. Part of Rts.Game (presentation only).
using Rts.Game.Bootstrap;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Game.Controls;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>F10 menu with surrender, and the victory, defeat or draw screen.</summary>
    [DisallowMultipleComponent]
    public sealed class GameMenu : MonoBehaviour
    {
        /// <summary>Key that opens and closes the menu (player_interface.md key table, placeholder).</summary>
        private const KeyCode MenuKey = KeyCode.F10;

        /// <summary>The menu panel (Surrender, Resume). Range: non-null after Awake.</summary>
        private GameObject _menu;

        /// <summary>The surrender confirmation panel (Yes, No). Range: non-null after Awake.</summary>
        private GameObject _confirm;

        /// <summary>The end screen panel. Range: non-null after Awake.</summary>
        private GameObject _end;

        /// <summary>End screen title: Victory, Defeat, Draw or Defeated. Range: non-null after Awake.</summary>
        private Text _endTitle;

        /// <summary>End screen cause line. Range: non-null after Awake.</summary>
        private Text _endCause;

        /// <summary>Keep Watching button, shown only while the match goes on. Range: non-null after Awake.</summary>
        private Button _keepWatching;

        /// <summary>End screen Restart button (decided matches only). Range: non-null after Awake.</summary>
        private Button _restart;

        /// <summary>End screen Main Menu button. Range: non-null after Awake.</summary>
        private Button _mainMenu;

        /// <summary>End screen Quit button (decided matches only). Range: non-null after Awake.</summary>
        private Button _quit;

        /// <summary>True after Keep Watching, so the "Defeated" screen stays hidden while the match goes on. Unit: on/off.</summary>
        private bool _watching;

        /// <summary>Cause of the last player who left the match ("surrender", "hq_destroyed", ...), or empty. Shown on the end screen.</summary>
        private string _lastCause = "";

        /// <summary>The local command path. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>True once subscribed to the host's events. Unit: on/off.</summary>
        private bool _subscribed;

        /// <summary>Builds the three panels, hidden.</summary>
        private void Awake()
        {
            _menu = HudBuild.Image(transform, "Game Menu", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 320f), HudBuild.PanelColor).gameObject;
            _menu.GetComponent<Image>().raycastTarget = true;
            HudBuild.Text(_menu.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(340f, 34f), 26, TextAnchor.UpperCenter).text = "Menu";
            HudBuild.Button(_menu.transform, "Surrender", new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(260f, 44f), "Surrender", () => Show(_confirm));
            HudBuild.Button(_menu.transform, "Restart", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(260f, 44f), "Restart", Restart);
            HudBuild.Button(_menu.transform, "Main Menu", new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(260f, 44f), "Main Menu", () => MatchLauncher.Load(null));
            HudBuild.Button(_menu.transform, "Resume", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(260f, 44f), "Resume", () => Show(null));

            _confirm = HudBuild.Image(transform, "Surrender Confirm", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 180f), HudBuild.PanelColor).gameObject;
            _confirm.GetComponent<Image>().raycastTarget = true;
            HudBuild.Text(_confirm.transform, "Question", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(400f, 70f), 22, TextAnchor.UpperCenter).text = "Surrender?\nYou lose the match.";
            HudBuild.Button(_confirm.transform, "Yes", new Vector2(0.5f, 0f), new Vector2(-90f, 20f), new Vector2(150f, 44f), "Yes", Surrender);
            HudBuild.Button(_confirm.transform, "No", new Vector2(0.5f, 0f), new Vector2(90f, 20f), new Vector2(150f, 44f), "No", () => Show(null));

            _end = HudBuild.Image(transform, "Match End", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 260f), HudBuild.PanelColor).gameObject;
            _end.GetComponent<Image>().raycastTarget = true;
            _endTitle = HudBuild.Text(_end.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(500f, 70f), 52, TextAnchor.UpperCenter);
            _endCause = HudBuild.Text(_end.transform, "Cause", new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(500f, 40f), 22, TextAnchor.UpperCenter);
            _keepWatching = HudBuild.Button(_end.transform, "Keep Watching", new Vector2(0.5f, 0f), new Vector2(-100f, 24f), new Vector2(180f, 44f), "Keep Watching", () => _watching = true);
            _restart = HudBuild.Button(_end.transform, "Restart", new Vector2(0.5f, 0f), new Vector2(-190f, 24f), new Vector2(170f, 44f), "Restart", Restart);
            _mainMenu = HudBuild.Button(_end.transform, "Main Menu", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(170f, 44f), "Main Menu", () => MatchLauncher.Load(null));
            _quit = HudBuild.Button(_end.transform, "Quit", new Vector2(0.5f, 0f), new Vector2(190f, 24f), new Vector2(170f, 44f), "Quit", MatchLauncher.Quit);

            Show(null);
            _end.SetActive(false);
        }

        /// <summary>Handles F10 and keeps the end screen in step with the match.</summary>
        private void Update()
        {
            if (_commands == null)
            {
                _commands = FindAnyObjectByType<LocalCommands>();
            }
            if (_commands == null || !_commands.IsReady)
            {
                return;
            }
            if (!_subscribed)
            {
                _commands.Host.EventRaised += OnSimEvent;
                _subscribed = true;
            }
            Simulation sim = _commands.Host.Match;
            MatchOutcome outcome = _commands.Host.Outcome;
            bool localOut = sim.Players.TryGet(_commands.LocalPlayer, out Player player) && !player.IsInMatch;
            bool showEnd = outcome != null || (localOut && !_watching);
            _end.SetActive(showEnd);
            if (showEnd)
            {
                Show(null);
                if (outcome != null)
                {
                    _endTitle.text = outcome.IsDraw ? "Draw" : outcome.Winner == sim.Players.TeamOf(_commands.LocalPlayer) ? "Victory" : "Defeat";
                }
                else
                {
                    _endTitle.text = "Defeated";
                }
                _endCause.text = _lastCause.Length > 0 ? "Cause: " + _lastCause : "";
                // Decided: Restart, Main Menu, Quit. Out but the match goes on: Keep Watching, Main Menu.
                bool decided = outcome != null;
                _keepWatching.gameObject.SetActive(!decided);
                _restart.gameObject.SetActive(decided);
                _quit.gameObject.SetActive(decided);
                ((RectTransform)_mainMenu.transform).anchoredPosition = new Vector2(decided ? 0f : 100f, 24f);
                return;
            }
            if (GameInput.GetKeyDown(MenuKey))
            {
                Show(_menu.activeSelf || _confirm.activeSelf ? null : _menu);
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

        /// <summary>Remembers the cause of each player who leaves the match, for the end screen.</summary>
        /// <param name="simEvent">An event of the tick that just ran.</param>
        private void OnSimEvent(SimEvent simEvent)
        {
            if (simEvent is PlayerStateChangedEvent changed && !PlayerStateRules.IsInMatch(changed.Current))
            {
                _lastCause = changed.Cause;
            }
        }

        /// <summary>Shows one of the menu panels (or none).</summary>
        /// <param name="panel">The menu or the confirmation panel, or null to close both.</param>
        private void Show(GameObject panel)
        {
            _menu.SetActive(panel == _menu);
            _confirm.SetActive(panel == _confirm);
        }

        /// <summary>Sends the local player's surrender and closes the menu.</summary>
        private void Surrender()
        {
            Show(null);
            if (_commands != null && _commands.IsReady)
            {
                _commands.Submit(new SurrenderCommand(_commands.Tick, _commands.LocalPlayer, _commands.NextSequence(), _commands.LocalPlayer));
            }
        }

        /// <summary>Restarts the match from the same request (a new seed when its Random switch was on); the scene reloads.</summary>
        private void Restart()
        {
            if (_commands != null && _commands.Host != null && _commands.Host.CurrentRequest != null)
            {
                MatchLauncher.Load(MatchLauncher.RestartOf(_commands.Host.CurrentRequest));
            }
        }
    }
}

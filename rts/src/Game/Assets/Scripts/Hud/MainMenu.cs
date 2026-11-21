// rts/src/Game/Assets/Scripts/Hud/MainMenu.cs
// The main menu (player_interface_2.md UI-13), shown while the host is idle and the setup screen is closed: Skirmish opens the setup
// screen, Quit stops Play Mode in the editor and quits a player. A Replays entry joins when replay playback (UI-15) is built.
// Built from code under the menu canvas. Part of Rts.Game (presentation only).
using Rts.Game.Bootstrap;
using UnityEngine;

namespace Rts.Game.Hud
{
    /// <summary>The first screen: Skirmish and Quit.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        /// <summary>The menu column. Range: non-null after Awake.</summary>
        private GameObject _panel;

        /// <summary>The setup screen on the same canvas. Range: null until found.</summary>
        private SkirmishSetup _setup;

        /// <summary>The host. Range: null until found.</summary>
        private SimulationHost _host;

        /// <summary>Builds the column with its title and buttons.</summary>
        private void Awake()
        {
            _panel = HudBuild.Image(transform, "Main Menu", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 320f), HudBuild.PanelColor).gameObject;
            HudBuild.Text(_panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(400f, 50f), 38, TextAnchor.UpperCenter).text = "RTS";
            HudBuild.Button(_panel.transform, "Skirmish", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(280f, 56f), "Skirmish", OpenSetup);
            HudBuild.Button(_panel.transform, "Quit", new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(280f, 56f), "Quit", MatchLauncher.Quit);
            _panel.SetActive(false);
        }

        /// <summary>Shows the column while the host is idle and the setup screen is closed.</summary>
        private void Update()
        {
            if (_host == null)
            {
                _host = FindAnyObjectByType<SimulationHost>();
            }
            if (_setup == null)
            {
                _setup = GetComponent<SkirmishSetup>();
            }
            _panel.SetActive(_host != null && _host.IsIdle && (_setup == null || !_setup.Open));
        }

        /// <summary>Opens the setup screen.</summary>
        private void OpenSetup()
        {
            if (_setup != null)
            {
                _setup.Open = true;
            }
        }
    }
}

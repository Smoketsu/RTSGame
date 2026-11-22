// rts/src/Game/Assets/Scripts/Hud/MainMenu.cs
// The main menu (player_interface_2.md UI-13, UI-15), shown while the host is idle and neither the setup screen nor the replay list is
// open: Skirmish opens the setup screen, Tutorial plays the first mission file in <campaign>/missions (ordinal file name order,
// player_interface_3.md UI-16), Replays opens the replay list, Quit stops Play Mode in the editor and quits a player. A start that
// failed (StartError, for example a mission that does not parse) shows under the menu.
// Built from code under the menu canvas. Part of Rts.Game (presentation only).
using System;
using System.IO;
using Rts.Game.Bootstrap;
using UnityEngine.UI;
using UnityEngine;

namespace Rts.Game.Hud
{
    /// <summary>The first screen: Skirmish, Tutorial, Replays and Quit.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        /// <summary>The menu column. Range: non-null after Awake.</summary>
        private GameObject _panel;

        /// <summary>The setup screen on the same canvas. Range: null until found.</summary>
        private SkirmishSetup _setup;

        /// <summary>The last start error, under the menu. Range: non-null after Awake.</summary>
        private Text _status;

        /// <summary>The replay list on the same canvas. Range: null until found.</summary>
        private ReplayList _replays;

        /// <summary>The host. Range: null until found.</summary>
        private SimulationHost _host;

        /// <summary>Builds the column with its title and buttons.</summary>
        private void Awake()
        {
            _panel = HudBuild.Image(transform, "Main Menu", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 400f), HudBuild.PanelColor).gameObject;
            HudBuild.Text(_panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(400f, 50f), 38, TextAnchor.UpperCenter).text = "RTS";
            HudBuild.Button(_panel.transform, "Skirmish", new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(280f, 56f), "Skirmish", OpenSetup);
            HudBuild.Button(_panel.transform, "Tutorial", new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(280f, 56f), "Tutorial", StartTutorial);
            HudBuild.Button(_panel.transform, "Replays", new Vector2(0.5f, 0.5f), new Vector2(0f, -58f), new Vector2(280f, 56f), "Replays", OpenReplays);
            _status = HudBuild.Text(_panel.transform, "Status", new Vector2(0.5f, 0f), new Vector2(0f, -50f), new Vector2(700f, 44f), 18, TextAnchor.UpperCenter);
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
            if (_replays == null)
            {
                _replays = GetComponent<ReplayList>();
            }
            _panel.SetActive(_host != null && _host.IsIdle && (_setup == null || !_setup.Open) && (_replays == null || !_replays.Open));
            if (_host != null && _host.StartError != null)
            {
                _status.text = _host.StartError;
            }
        }

        /// <summary>Starts the first mission in the campaign's missions folder (open question 1: the first file, no list yet).</summary>
        private void StartTutorial()
        {
            if (_host == null)
            {
                return;
            }
            string folder = Path.Combine(_host.CampaignDirectory, "missions");
            string[] files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json") : new string[0];
            if (files.Length == 0)
            {
                _status.text = "No mission files in " + folder;
                return;
            }
            Array.Sort(files, string.CompareOrdinal);
            MatchLauncher.Load(new MatchRequest { Kind = MatchKind.Mission, MissionId = Path.GetFileNameWithoutExtension(files[0]) });
        }

        /// <summary>Opens the replay list (it reads the folder again).</summary>
        private void OpenReplays()
        {
            if (_replays != null)
            {
                _replays.Open = true;
            }
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

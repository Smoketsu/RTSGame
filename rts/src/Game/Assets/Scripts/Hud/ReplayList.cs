// rts/src/Game/Assets/Scripts/Hud/ReplayList.cs
// The replay list (player_interface_2.md UI-15), opened from the main menu: every replay in ReplayFiles.Folder, newest first, read
// when the list opens, eight per page. Each row shows the file name, map, player names and length (m:ss at the data's tick rate); a
// file that cannot be read shows why and is disabled. Picking a row first builds a ReplayPlayback from it, so a replay recorded with
// other data (content digest), on a map this data lacks or with another hash interval is refused here with the reason, before the
// scene reloads; otherwise MatchLauncher loads a Replay request. Esc or Back closes the list. Part of Rts.Game (presentation only).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Rts.Game.Bootstrap;
using Rts.Game.Controls;
using Rts.Replay;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Lists the replay files and starts playing one.</summary>
    [DisallowMultipleComponent]
    public sealed class ReplayList : MonoBehaviour
    {
        /// <summary>Rows per page.</summary>
        private const int RowsPerPage = 8;

        /// <summary>Panel width. Unit: reference pixels (1920 x 1080 canvas).</summary>
        private const float Width = 900f;

        /// <summary>One readable or unreadable replay file.</summary>
        private sealed class Entry
        {
            /// <summary>Absolute file path.</summary>
            public string Path;

            /// <summary>Row text: name, map, players and length, or the read error.</summary>
            public string Label;

            /// <summary>True when the file could be read. Unit: on/off.</summary>
            public bool Readable;
        }

        /// <summary>The panel. Range: non-null after Awake.</summary>
        private GameObject _panel;

        /// <summary>The row buttons, RowsPerPage of them. Range: non-null after Awake.</summary>
        private readonly List<Button> _rows = new List<Button>();

        /// <summary>Page line ("Page 1 / 3") or the empty-folder note. Range: non-null after Awake.</summary>
        private Text _page;

        /// <summary>Status line: a refusal or read error of the picked file. Range: non-null after Awake.</summary>
        private Text _status;

        /// <summary>The files read when the list opened.</summary>
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>The page shown. Range: 0 to page count - 1.</summary>
        private int _pageIndex;

        /// <summary>The host (for its data). Range: null until found.</summary>
        private SimulationHost _host;

        /// <summary>True while the list shows; setting it to true reads the folder again. Unit: on/off.</summary>
        public bool Open
        {
            get => _panel != null && _panel.activeSelf;
            set
            {
                if (value)
                {
                    Refresh();
                }
                _panel.SetActive(value);
            }
        }

        /// <summary>Builds the panel, hidden.</summary>
        private void Awake()
        {
            _panel = HudBuild.Image(transform, "Replay List", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, 620f), HudBuild.PanelColor).gameObject;
            _panel.GetComponent<Image>().raycastTarget = true;
            HudBuild.Text(_panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(Width - 40f, 40f), 30, TextAnchor.UpperCenter).text = "Replays";
            for (int i = 0; i < RowsPerPage; i++)
            {
                // Row position in the page. Range: 0 to RowsPerPage - 1.
                int row = i;
                Button button = HudBuild.Button(_panel.transform, "Row " + i, new Vector2(0.5f, 1f), new Vector2(0f, -70f - i * 54f), new Vector2(Width - 40f, 48f), "", () => Pick(row));
                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 16;
                _rows.Add(button);
            }
            _page = HudBuild.Text(_panel.transform, "Page", new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(Width - 40f, 26f), 18, TextAnchor.MiddleCenter);
            _status = HudBuild.Text(_panel.transform, "Status", new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(Width - 460f, 44f), 16, TextAnchor.MiddleCenter);
            HudBuild.Button(_panel.transform, "Previous", new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(110f, 44f), "Previous", () => Turn(-1));
            HudBuild.Button(_panel.transform, "Next", new Vector2(0f, 0f), new Vector2(136f, 20f), new Vector2(110f, 44f), "Next", () => Turn(1));
            HudBuild.Button(_panel.transform, "Back", new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(150f, 44f), "Back", () => Open = false);
            _panel.SetActive(false);
        }

        /// <summary>Esc closes the list; it also closes when a match runs.</summary>
        private void Update()
        {
            if (_host == null)
            {
                _host = FindAnyObjectByType<SimulationHost>();
            }
            if (!Open)
            {
                return;
            }
            if (GameInput.GetKeyDown(KeyCode.Escape) || _host == null || !_host.IsIdle)
            {
                Open = false;
            }
        }

        /// <summary>Reads every replay file's header data for its row, and shows the first page.</summary>
        private void Refresh()
        {
            if (_host == null)
            {
                _host = FindAnyObjectByType<SimulationHost>();
            }
            _entries.Clear();
            _pageIndex = 0;
            _status.text = "";
            foreach (string path in ReplayFiles.NewestFirst())
            {
                Entry entry = new Entry { Path = path };
                try
                {
                    ReplayData replay = ReplayReader.Read(File.ReadAllBytes(path), BuiltInMechanics.CreateCodecRegistry());
                    entry.Label = Path.GetFileNameWithoutExtension(path) + "   " + replay.MapId + "   " + Names(replay) + "   " + Length(replay);
                    entry.Readable = true;
                }
                catch (Exception exception) when (exception is ReplayException || exception is IOException)
                {
                    entry.Label = Path.GetFileNameWithoutExtension(path) + "   Unreadable: " + exception.Message;
                }
                _entries.Add(entry);
            }
            ShowPage();
        }

        /// <summary>Fills the rows of the current page.</summary>
        private void ShowPage()
        {
            // Pages for the files; at least 1 so an empty folder shows "Page 1 / 1". Range: 1 upward.
            int pages = Math.Max(1, (_entries.Count + RowsPerPage - 1) / RowsPerPage);
            _pageIndex = Mathf.Clamp(_pageIndex, 0, pages - 1);
            for (int i = 0; i < RowsPerPage; i++)
            {
                // Index of this row's file. Range: 0 upward; past the end hides the row.
                int index = _pageIndex * RowsPerPage + i;
                bool used = index < _entries.Count;
                _rows[i].gameObject.SetActive(used);
                if (used)
                {
                    _rows[i].GetComponentInChildren<Text>().text = "  " + _entries[index].Label;
                    _rows[i].interactable = _entries[index].Readable;
                }
            }
            _page.text = _entries.Count == 0 ? "No replays yet. Every match writes one to " + ReplayFiles.Folder : "Page " + (_pageIndex + 1) + " / " + pages;
        }

        /// <summary>Turns the page.</summary>
        /// <param name="direction">-1 previous, +1 next.</param>
        private void Turn(int direction)
        {
            _pageIndex += direction;
            ShowPage();
        }

        /// <summary>Checks the picked replay against the loaded data and plays it, or shows why it cannot be played.</summary>
        /// <param name="row">Row position in the page. Range: 0 to RowsPerPage - 1.</param>
        private void Pick(int row)
        {
            // Index of the picked file. Range: 0 to file count - 1.
            int index = _pageIndex * RowsPerPage + row;
            if (index >= _entries.Count || _host == null || _host.Data == null)
            {
                return;
            }
            string path = _entries[index].Path;
            try
            {
                ReplayData replay = ReplayReader.Read(File.ReadAllBytes(path), BuiltInMechanics.CreateCodecRegistry());
                // Built only to check the replay against this data; the host builds its own after the reload.
                new ReplayPlayback(replay, _host.Data);
            }
            catch (Exception exception) when (exception is ReplayException || exception is IOException || exception is InvalidOperationException || exception is DataValidationException)
            {
                _status.text = "Cannot play: " + exception.Message;
                return;
            }
            MatchLauncher.Load(new MatchRequest { Kind = MatchKind.Replay, ReplayPath = path });
        }

        /// <summary>The replay's player names, comma separated.</summary>
        /// <param name="replay">A read replay.</param>
        private static string Names(ReplayData replay)
        {
            StringBuilder names = new StringBuilder();
            foreach (PlayerSetup player in replay.Players)
            {
                names.Append(names.Length > 0 ? ", " : "").Append(player.DisplayName);
            }
            return names.ToString();
        }

        /// <summary>The replay's length as m:ss at the loaded data's tick rate, or its tick count without data.</summary>
        /// <param name="replay">A read replay.</param>
        private string Length(ReplayData replay)
        {
            if (_host == null || _host.Data == null)
            {
                return replay.TickCount + " ticks";
            }
            return PlaybackBar.Clock(replay.TickCount, _host.Data.CreateMatchSettings(replay.Seed).TicksPerSecond);
        }
    }
}

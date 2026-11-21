// rts/src/Game/Assets/Scripts/Hud/SkirmishSetup.cs
// The skirmish setup screen (player_interface_2.md UI-12), shown while the host is idle. The player picks a map, then per slot (one
// per map spawn): Human, AI or Closed, a team, a faction and, for AI slots, an AI profile; and a seed with a "Random" switch. Start
// checks the slots (exactly one Human, at least two open slots on at least two teams), checks the start with
// StandardMatchStart.Commands so its refusal shows here, draws the seed when Random is on (UnityEngine.Random: an input to the match,
// never simulation code) and calls MatchLauncher.Load, which reloads the scene; the host then starts the request. Lists come from
// data in ascending ID order: maps (dataSet.Maps), factions with an HQ, AI profiles that parse and have a plan for the slot's
// faction (listed by ID: gap G2's menu names are not being built). Choices cycle on click. Opened from the main menu; Back or Esc
// returns to it (UI-13). Built from code under the menu canvas.
// Part of Rts.Game (presentation only).
using System;
using System.Collections.Generic;
using Rts.Ai;
using Rts.Game.Bootstrap;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Map, slots and seed for a skirmish against local AI.</summary>
    [DisallowMultipleComponent]
    public sealed class SkirmishSetup : MonoBehaviour
    {
        /// <summary>One slot row's controls.</summary>
        private sealed class Row
        {
            /// <summary>The row's root. Range: non-null.</summary>
            public GameObject Root;

            /// <summary>"Slot N" label. Range: non-null.</summary>
            public Text Label;

            /// <summary>Human / AI / Closed. Range: non-null.</summary>
            public Button Kind;

            /// <summary>Team 1 to the slot count. Range: non-null.</summary>
            public Button Team;

            /// <summary>Faction. Range: non-null.</summary>
            public Button Faction;

            /// <summary>AI profile (AI slots only). Range: non-null.</summary>
            public Button Profile;
        }

        /// <summary>Most slot rows. Unit: slots.</summary>
        private const int MaxSlots = 8;

        /// <summary>Panel width. Unit: reference pixels.</summary>
        private const float Width = 1040f;

        /// <summary>Height of one slot row. Unit: reference pixels.</summary>
        private const float RowHeight = 50f;

        /// <summary>Colour of refusal text. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.4f, 1f);

        /// <summary>The panel. Range: non-null after Awake.</summary>
        private RectTransform _panel;

        /// <summary>The map button. Range: non-null after Awake.</summary>
        private Button _mapButton;

        /// <summary>Slot rows, one per possible slot. Range: MaxSlots after Awake.</summary>
        private readonly List<Row> _rows = new List<Row>();

        /// <summary>The seed field. Range: non-null after Awake.</summary>
        private InputField _seedField;

        /// <summary>The Random switch button. Range: non-null after Awake.</summary>
        private Button _randomButton;

        /// <summary>Why Start is disabled or why the last Start failed. Range: non-null after Awake.</summary>
        private Text _status;

        /// <summary>The Start button. Range: non-null after Awake.</summary>
        private Button _start;

        /// <summary>The host whose data the lists come from. Range: null until found.</summary>
        private SimulationHost _host;

        /// <summary>Maps in ascending ID order. Range: filled on first use.</summary>
        private readonly List<MapDefinition> _maps = new List<MapDefinition>();

        /// <summary>Factions with an HQ, ascending ID. Range: filled on first use.</summary>
        private readonly List<FactionDefinition> _factions = new List<FactionDefinition>();

        /// <summary>AI profiles that parsed, by ID in ascending order. Range: filled on first use.</summary>
        private readonly SortedDictionary<string, AiProfile> _profiles = new SortedDictionary<string, AiProfile>(StringComparer.Ordinal);

        /// <summary>Index of the chosen map in _maps. Range: 0 to the map count - 1.</summary>
        private int _mapIndex;

        /// <summary>The slots as chosen, one per spawn of the chosen map.</summary>
        private readonly List<MatchSlot> _slots = new List<MatchSlot>();

        /// <summary>True when a new seed is drawn at Start. Unit: on/off. Default on.</summary>
        private bool _randomSeed = true;

        /// <summary>True once the lists have been read from the data. Unit: on/off.</summary>
        private bool _loaded;

        /// <summary>True while the setup screen is open (opened from the main menu, closed by Back or Esc). Unit: on/off.</summary>
        public bool Open { get; set; }

        /// <summary>Builds the panel and its controls.</summary>
        private void Awake()
        {
            _panel = HudBuild.Image(transform, "Skirmish Setup", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, 620f), HudBuild.PanelColor).rectTransform;
            _panel.GetComponent<Image>().raycastTarget = true;
            HudBuild.Text(_panel, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(Width - 40f, 44f), 34, TextAnchor.UpperCenter).text = "Skirmish";
            HudBuild.Text(_panel, "Map Label", new Vector2(0f, 1f), new Vector2(30f, -86f), new Vector2(120f, 36f), 22, TextAnchor.MiddleLeft).text = "Map";
            _mapButton = HudBuild.Button(_panel, "Map", new Vector2(0f, 1f), new Vector2(160f, -84f), new Vector2(420f, 40f), "", CycleMap);
            // Column headers. Unit: reference pixels from the left.
            string[] headers = { "Slot", "Player", "Team", "Faction", "AI profile" };
            float[] columns = { 30f, 160f, 340f, 480f, 700f };
            for (int i = 0; i < headers.Length; i++)
            {
                HudBuild.Text(_panel, "Header " + i, new Vector2(0f, 1f), new Vector2(columns[i], -140f), new Vector2(200f, 28f), 17, TextAnchor.MiddleLeft).text = headers[i];
            }
            for (int i = 0; i < MaxSlots; i++)
            {
                _rows.Add(CreateRow(i, columns));
            }
            HudBuild.Text(_panel, "Seed Label", new Vector2(0f, 0f), new Vector2(30f, 92f), new Vector2(120f, 36f), 22, TextAnchor.MiddleLeft).text = "Seed";
            _seedField = CreateSeedField(new Vector2(160f, 92f));
            _randomButton = HudBuild.Button(_panel, "Random Seed", new Vector2(0f, 0f), new Vector2(390f, 92f), new Vector2(180f, 40f), "", ToggleRandom);
            _status = HudBuild.Text(_panel, "Status", new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(Width - 470f, 48f), 18, TextAnchor.MiddleLeft);
            _status.color = ErrorColor;
            _start = HudBuild.Button(_panel, "Start", new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(220f, 52f), "Start", StartMatch);
            HudBuild.Button(_panel, "Back", new Vector2(1f, 0f), new Vector2(-270f, 24f), new Vector2(150f, 52f), "Back", () => Open = false);
        }

        /// <summary>Reads the lists once the host has data, and keeps every label and the Start state current.</summary>
        private void Update()
        {
            if (_host == null)
            {
                _host = FindAnyObjectByType<SimulationHost>();
            }
            bool visible = _host != null && _host.IsIdle && Open;
            _panel.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            if (Controls.GameInput.GetKeyDown(KeyCode.Escape))
            {
                Open = false;
                return;
            }
            if (!_loaded)
            {
                LoadLists();
            }
            Refresh();
        }

        /// <summary>Fills the map, faction and profile lists from the data and makes the default slots.</summary>
        private void LoadLists()
        {
            _loaded = true;
            DataSet data = _host.Data;
            _maps.AddRange(data.Maps.Values);
            foreach (FactionDefinition faction in data.Content.Factions.Values)
            {
                if (faction.HqUnitId != null)
                {
                    _factions.Add(faction);
                }
            }
            _factions.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (string id in data.AiProfiles.Keys)
            {
                try
                {
                    _profiles.Add(id, AiProfile.FromDataSet(data, id));
                }
                catch (Exception exception)
                {
                    // A profile that does not parse is left out of the lists.
                    Debug.LogWarning("[SkirmishSetup] AI profile " + id + " left out: " + exception.Message);
                }
            }
            ResetSlots();
        }

        /// <summary>One slot per spawn of the chosen map: slot 0 Human, the rest AI; team = slot; factions in turn; the first fitting profile.</summary>
        private void ResetSlots()
        {
            _slots.Clear();
            if (_maps.Count == 0 || _factions.Count == 0)
            {
                return;
            }
            int count = Mathf.Min(MaxSlots, _maps[_mapIndex].Spawns.Count);
            for (int i = 0; i < count; i++)
            {
                MatchSlot slot = new MatchSlot { Slot = i, Team = i, FactionId = _factions[i % _factions.Count].Id, Kind = i == 0 ? SlotKind.Human : SlotKind.Ai };
                slot.ProfileId = FirstProfile(slot.FactionId);
                _slots.Add(slot);
            }
        }

        /// <summary>The AI profile IDs with a plan for a faction, ascending ID.</summary>
        /// <param name="factionId">A faction ID.</param>
        private List<string> ProfilesFor(string factionId)
        {
            List<string> ids = new List<string>();
            foreach (KeyValuePair<string, AiProfile> pair in _profiles)
            {
                if (pair.Value.Factions.ContainsKey(factionId))
                {
                    ids.Add(pair.Key);
                }
            }
            return ids;
        }

        /// <summary>The first AI profile ID for a faction, or null when none has a plan for it.</summary>
        /// <param name="factionId">A faction ID.</param>
        private string FirstProfile(string factionId)
        {
            List<string> ids = ProfilesFor(factionId);
            return ids.Count > 0 ? ids[0] : null;
        }

        /// <summary>Writes every label and enables Start when the slots are valid (else names the reason).</summary>
        private void Refresh()
        {
            if (_maps.Count == 0 || _factions.Count == 0)
            {
                SetLabel(_mapButton, "-");
                _status.text = _host.StartError ?? "No maps or factions in the data.";
                _start.interactable = false;
                return;
            }
            SetLabel(_mapButton, _maps[_mapIndex].DisplayName);
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool used = i < _slots.Count;
                row.Root.SetActive(used);
                if (!used)
                {
                    continue;
                }
                MatchSlot slot = _slots[i];
                bool open = slot.Kind != SlotKind.Closed;
                row.Label.text = "Slot " + (slot.Slot + 1);
                SetLabel(row.Kind, slot.Kind == SlotKind.Ai ? "AI" : slot.Kind.ToString());
                row.Team.gameObject.SetActive(open);
                row.Faction.gameObject.SetActive(open);
                row.Profile.gameObject.SetActive(slot.Kind == SlotKind.Ai);
                SetLabel(row.Team, "Team " + (slot.Team + 1));
                SetLabel(row.Faction, FactionName(slot.FactionId));
                SetLabel(row.Profile, slot.ProfileId ?? "(no profile for this faction)");
            }
            SetLabel(_randomButton, _randomSeed ? "Random: on" : "Random: off");
            string problem = SlotProblem();
            _start.interactable = problem == null;
            _status.text = problem ?? _host.StartError ?? "";
        }

        /// <summary>Why the slots cannot start, or null when they can.</summary>
        private string SlotProblem()
        {
            int humans = 0;
            int open = 0;
            HashSet<int> teams = new HashSet<int>();
            foreach (MatchSlot slot in _slots)
            {
                if (slot.Kind == SlotKind.Closed)
                {
                    continue;
                }
                open++;
                teams.Add(slot.Team);
                if (slot.Kind == SlotKind.Human)
                {
                    humans++;
                }
                else if (slot.ProfileId == null)
                {
                    return "Slot " + (slot.Slot + 1) + " has no AI profile for its faction.";
                }
            }
            if (humans != 1)
            {
                return "Choose exactly one Human slot.";
            }
            if (open < 2)
            {
                return "Open at least two slots.";
            }
            return teams.Count < 2 ? "Put the open slots on at least two teams." : null;
        }

        /// <summary>Checks the start, draws or reads the seed and loads the match (the scene reloads).</summary>
        private void StartMatch()
        {
            if (SlotProblem() != null)
            {
                return;
            }
            int seed;
            if (_randomSeed)
            {
                seed = UnityEngine.Random.Range(0, int.MaxValue);
                _seedField.text = seed.ToString();
            }
            else if (!int.TryParse(_seedField.text, out seed) || seed < 0)
            {
                _status.text = "The seed must be a whole number from 0 to 2147483647.";
                return;
            }
            MatchRequest request = new MatchRequest { Kind = MatchKind.Skirmish, MapId = _maps[_mapIndex].Id, Seed = seed, RandomSeed = _randomSeed };
            foreach (MatchSlot slot in _slots)
            {
                request.Slots.Add(new MatchSlot { Slot = slot.Slot, Team = slot.Team, FactionId = slot.FactionId, Kind = slot.Kind, ProfileId = slot.Kind == SlotKind.Ai ? slot.ProfileId : null });
            }
            try
            {
                // The same start the host will build; its refusal (no spawn, no HQ, too few worker tiles) blocks Start here.
                StandardMatchStart.Commands(_host.Data, _maps[_mapIndex], SimulationHost.SkirmishPlayers(request));
            }
            catch (InvalidOperationException exception)
            {
                _status.text = exception.Message;
                Debug.LogWarning("[SkirmishSetup] Start refused: " + exception.Message);
                return;
            }
            MatchLauncher.Load(request);
        }

        /// <summary>Next map; the slots are made again for its spawn count.</summary>
        private void CycleMap()
        {
            if (_maps.Count == 0)
            {
                return;
            }
            _mapIndex = (_mapIndex + 1) % _maps.Count;
            ResetSlots();
        }

        /// <summary>Flips the Random seed switch.</summary>
        private void ToggleRandom()
        {
            _randomSeed = !_randomSeed;
        }

        /// <summary>Creates slot row <paramref name="index"/> with its cycling buttons.</summary>
        /// <param name="index">Row index. Range: 0 to MaxSlots - 1.</param>
        /// <param name="columns">Left edge of each column. Unit: reference pixels.</param>
        private Row CreateRow(int index, float[] columns)
        {
            RectTransform root = HudBuild.Rect(_panel, "Slot Row " + index, new Vector2(0f, 1f), new Vector2(0f, -176f - index * RowHeight), new Vector2(Width, RowHeight - 6f));
            Row row = new Row { Root = root.gameObject };
            row.Label = HudBuild.Text(root, "Label", new Vector2(0f, 0.5f), new Vector2(columns[0], 0f), new Vector2(120f, 36f), 20, TextAnchor.MiddleLeft);
            row.Kind = HudBuild.Button(root, "Kind", new Vector2(0f, 0.5f), new Vector2(columns[1], 0f), new Vector2(160f, 40f), "", () => CycleKind(index));
            row.Team = HudBuild.Button(root, "Team", new Vector2(0f, 0.5f), new Vector2(columns[2], 0f), new Vector2(120f, 40f), "", () => CycleTeam(index));
            row.Faction = HudBuild.Button(root, "Faction", new Vector2(0f, 0.5f), new Vector2(columns[3], 0f), new Vector2(200f, 40f), "", () => CycleFaction(index));
            row.Profile = HudBuild.Button(root, "Profile", new Vector2(0f, 0.5f), new Vector2(columns[4], 0f), new Vector2(310f, 40f), "", () => CycleProfile(index));
            return row;
        }

        /// <summary>Human, AI, Closed, Human, ...</summary>
        /// <param name="index">Slot index.</param>
        private void CycleKind(int index)
        {
            MatchSlot slot = _slots[index];
            slot.Kind = slot.Kind == SlotKind.Human ? SlotKind.Ai : slot.Kind == SlotKind.Ai ? SlotKind.Closed : SlotKind.Human;
        }

        /// <summary>Team 1, 2, ... up to the slot count, then 1 again.</summary>
        /// <param name="index">Slot index.</param>
        private void CycleTeam(int index)
        {
            _slots[index].Team = (_slots[index].Team + 1) % _slots.Count;
        }

        /// <summary>Next faction; the AI profile moves to the first one with a plan for it.</summary>
        /// <param name="index">Slot index.</param>
        private void CycleFaction(int index)
        {
            MatchSlot slot = _slots[index];
            int current = _factions.FindIndex(faction => faction.Id == slot.FactionId);
            slot.FactionId = _factions[(current + 1) % _factions.Count].Id;
            slot.ProfileId = FirstProfile(slot.FactionId);
        }

        /// <summary>Next AI profile with a plan for the slot's faction.</summary>
        /// <param name="index">Slot index.</param>
        private void CycleProfile(int index)
        {
            MatchSlot slot = _slots[index];
            List<string> ids = ProfilesFor(slot.FactionId);
            if (ids.Count == 0)
            {
                return;
            }
            slot.ProfileId = ids[(ids.IndexOf(slot.ProfileId) + 1) % ids.Count];
        }

        /// <summary>A faction's display name, or its ID when the catalog lacks it.</summary>
        /// <param name="factionId">A faction ID.</param>
        private string FactionName(string factionId)
        {
            FactionDefinition faction = _factions.Find(candidate => candidate.Id == factionId);
            return faction != null ? faction.DisplayName : factionId;
        }

        /// <summary>Creates the seed field: a whole-number InputField with a placeholder.</summary>
        /// <param name="position">Offset from the panel's bottom-left corner. Unit: reference pixels.</param>
        private InputField CreateSeedField(Vector2 position)
        {
            Image background = HudBuild.Image(_panel, "Seed", new Vector2(0f, 0f), position, new Vector2(210f, 40f), new Color(0f, 0f, 0f, 0.55f));
            background.raycastTarget = true;
            Text text = HudBuild.Text(background.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(194f, 36f), 20, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Text placeholder = HudBuild.Text(background.transform, "Placeholder", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(194f, 36f), 20, TextAnchor.MiddleLeft);
            placeholder.text = "0 to 2147483647";
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            InputField field = background.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.characterLimit = 10;
            field.text = "1234";
            return field;
        }

        /// <summary>Sets a button's label text.</summary>
        /// <param name="button">A button made by HudBuild.Button.</param>
        /// <param name="label">The text.</param>
        private static void SetLabel(Button button, string label)
        {
            button.GetComponentInChildren<Text>().text = label;
        }
    }
}

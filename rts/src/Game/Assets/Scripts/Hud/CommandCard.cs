// rts/src/Game/Assets/Scripts/Hud/CommandCard.cs
// The HUD command card, bottom right, and the production queue strip above it (player_interface.md UI-2, research panel of
// research_and_unlocks.md R1.10). With an own producer selected it shows one button per entry, in data order: the HQ worker
// (Definition.Hq.WorkerUnitId) first when the producer is an HQ, then Definition.Economy.Trains, then Definition.Economy.Researches.
// A click or the slot key (Q W E T Z X C V) sends TrainUnitCommand(producer, id) through LocalCommands; a research item is the
// same order with a research ID. Several producers of the same definition selected: the order goes to the complete one with the
// shortest queue, ties lowest EntityId. Buttons grey out only for a missing prerequisite, a producer under construction, an HQ that
// is not Rooted, or research already done; anything else is sent and the simulation answers (message line). Every number is read
// from the simulation when drawn. Shortcut: the cost, supply, time and "Requires" text is printed on the button instead of a
// hover tooltip, so a Play Mode check can read it. Part of Rts.Game (presentation only).
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Game.Controls;
using Rts.Game.UnitSelection;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Economy;
using Rts.Sim.Teams;
using Rts.Sim.Units;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Rts.Sim.Core.EntityId;

namespace Rts.Game.Hud
{
    /// <summary>Train and research buttons for the selected producer, and its production queue.</summary>
    [DisallowMultipleComponent]
    public sealed class CommandCard : MonoBehaviour
    {
        /// <summary>One button of the card.</summary>
        private sealed class Slot
        {
            /// <summary>The button. Range: non-null.</summary>
            public Button Button;

            /// <summary>Background image, tinted by entry kind. Range: non-null.</summary>
            public Image Background;

            /// <summary>First line: the key and the entry's display name. Range: non-null.</summary>
            public Text Name;

            /// <summary>Second line: cost, supply and time, or why the button is grey. Range: non-null.</summary>
            public Text Info;
        }

        /// <summary>One pooled icon of the queue strip.</summary>
        private sealed class QueueIcon
        {
            /// <summary>Icon background. Range: non-null.</summary>
            public Image Background;

            /// <summary>Short name. Range: non-null.</summary>
            public Text Label;

            /// <summary>Progress fill of the head entry (zero width for waiting entries). Range: non-null.</summary>
            public RectTransform Progress;
        }

        /// <summary>Keys of slots 1 to 8, in slot order (player_interface.md key table, placeholders). Range: 8 keys GameInput can map.</summary>
        private static readonly KeyCode[] SlotKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.T, KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V };

        /// <summary>Button columns of the card. Unit: buttons.</summary>
        private const int Columns = 4;

        /// <summary>Button width. Unit: reference pixels.</summary>
        private const float ButtonWidth = 132f;

        /// <summary>Button height. Unit: reference pixels.</summary>
        private const float ButtonHeight = 76f;

        /// <summary>Gap between buttons and the card's inner margin. Unit: reference pixels.</summary>
        private const float Gap = 8f;

        /// <summary>Edge length of a queue icon. Unit: reference pixels.</summary>
        private const float QueueIconSize = 48f;

        /// <summary>Tint of train buttons. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color TrainColor = new Color(0.20f, 0.30f, 0.50f, 1f);

        /// <summary>Tint of research buttons. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color ResearchColor = new Color(0.38f, 0.24f, 0.50f, 1f);

        /// <summary>Text colour of a cost the player cannot afford. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color UnaffordableColor = new Color(1f, 0.4f, 0.35f, 1f);

        /// <summary>The card background; hidden while no producer is selected. Range: non-null after Awake.</summary>
        private Image _card;

        /// <summary>The queue strip background; hidden while no producer is selected. Range: non-null after Awake.</summary>
        private Image _queue;

        /// <summary>"Queue n/max" label of the strip. Range: non-null after Awake.</summary>
        private Text _queueLabel;

        /// <summary>The eight slots, in slot order. Range: 8 entries after Awake.</summary>
        private readonly List<Slot> _slots = new List<Slot>();

        /// <summary>Queue icons created so far, reused frame to frame. Range: 0 to the longest queue seen.</summary>
        private readonly List<QueueIcon> _queueIcons = new List<QueueIcon>();

        /// <summary>Entry IDs shown this frame, in slot order: worker, trains, then researches. Range: 0 to 8 entries.</summary>
        private readonly List<string> _entries = new List<string>();

        /// <summary>Selected own producers of the shown definition, ascending EntityId. Presentation scratch list.</summary>
        private readonly List<Unit> _producers = new List<Unit>();

        /// <summary>The local command path. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>The selection source. Range: null until found.</summary>
        private SelectionController _selection;

        /// <summary>Builds the card, its eight buttons and the queue strip, all hidden.</summary>
        private void Awake()
        {
            // Card size from the button grid. Unit: reference pixels.
            Vector2 cardSize = new Vector2(Columns * ButtonWidth + (Columns + 1) * Gap, 2 * ButtonHeight + 3 * Gap);
            _card = HudBuild.Image(transform, "Command Card", new Vector2(1f, 0f), new Vector2(-12f, 10f), cardSize, HudBuild.PanelColor);
            _card.raycastTarget = true;
            for (int i = 0; i < SlotKeys.Length; i++)
            {
                _slots.Add(CreateSlot(i));
            }
            _queue = HudBuild.Image(transform, "Queue Strip", new Vector2(1f, 0f), new Vector2(-12f, 18f + cardSize.y), new Vector2(cardSize.x, QueueIconSize + 2 * Gap), HudBuild.PanelColor);
            _queue.raycastTarget = true;
            _queueLabel = HudBuild.Text(_queue.transform, "Queue Label", new Vector2(0f, 0.5f), new Vector2(Gap, 0f), new Vector2(110f, QueueIconSize), 18, TextAnchor.MiddleLeft);
            _card.gameObject.SetActive(false);
            _queue.gameObject.SetActive(false);
        }

        /// <summary>Refreshes the card and the queue from the selection and the simulation, and handles the slot keys.</summary>
        private void Update()
        {
            if (_commands == null)
            {
                _commands = FindAnyObjectByType<LocalCommands>();
            }
            if (_selection == null)
            {
                _selection = FindAnyObjectByType<SelectionController>();
            }
            bool visible = _commands != null && _selection != null && _commands.IsReady && FindProducers(_commands.Host.Match);
            _card.gameObject.SetActive(visible);
            _queue.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            Simulation sim = _commands.Host.Match;
            ListEntries(_producers[0].Definition);
            sim.Players.TryGet(_commands.LocalPlayer, out Player player);
            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < _entries.Count;
                _slots[i].Button.gameObject.SetActive(used);
                if (used)
                {
                    DrawSlot(sim, player, i);
                    if (_slots[i].Button.interactable && GameInput.GetKeyDown(SlotKeys[i]))
                    {
                        Click(i);
                    }
                }
            }
            DrawQueue(sim, _producers[0]);
        }

        /// <summary>
        /// Fills _producers with the selected own units of one producing definition: the definition of the lowest-ID selected unit that
        /// can produce anything. Returns false when no selected unit can produce.
        /// </summary>
        /// <param name="sim">The running match.</param>
        private bool FindProducers(Simulation sim)
        {
            _producers.Clear();
            foreach (EntityId id in _selection.Selected)
            {
                if (sim.Units.TryGet(id, out Unit unit) && unit.Owner == _commands.LocalPlayer && CanProduce(unit.Definition))
                {
                    _producers.Add(unit);
                }
            }
            if (_producers.Count == 0)
            {
                return false;
            }
            _producers.Sort((a, b) => a.Id.CompareTo(b.Id));
            // The definition shown: the lowest-ID producer's. Other producing definitions in the selection are dropped.
            UnitDefinition shown = _producers[0].Definition;
            _producers.RemoveAll(unit => unit.Definition != shown);
            return true;
        }

        /// <summary>True when units of this definition can train or research anything.</summary>
        /// <param name="definition">A unit definition.</param>
        private static bool CanProduce(UnitDefinition definition)
        {
            return definition.Hq != null || definition.Economy.Trains.Count > 0 || definition.Economy.Researches.Count > 0;
        }

        /// <summary>Fills _entries with the producer's buttons in data order (worker, trains, researches), at most one per slot.</summary>
        /// <param name="definition">The producer definition.</param>
        private void ListEntries(UnitDefinition definition)
        {
            _entries.Clear();
            if (definition.Hq != null)
            {
                _entries.Add(definition.Hq.WorkerUnitId);
            }
            _entries.AddRange(definition.Economy.Trains);
            _entries.AddRange(definition.Economy.Researches);
            if (_entries.Count > _slots.Count)
            {
                Debug.LogWarning("[CommandCard] " + definition.Id + " has " + _entries.Count + " entries; only " + _slots.Count + " slots are shown.");
                _entries.RemoveRange(_slots.Count, _entries.Count - _slots.Count);
            }
        }

        /// <summary>True when the producer can take an order now as far as the UI greys buttons: complete, and Rooted if it is an HQ.</summary>
        /// <param name="producer">A selected producer.</param>
        private static bool IsReadyProducer(Unit producer)
        {
            return !producer.IsUnderConstruction && (producer.Definition.Hq == null || producer.HqState == HqState.Rooted);
        }

        /// <summary>Sets one slot's labels, colours and greyed state from the simulation.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="player">The local player (stock, research).</param>
        /// <param name="index">Slot index. Range: 0 to the entry count - 1.</param>
        private void DrawSlot(Simulation sim, Player player, int index)
        {
            Slot slot = _slots[index];
            string id = _entries[index];
            // Tick rate for times. Unit: ticks per second. Range: 10 to 60.
            int rate = sim.Clock.TicksPerSecond;
            bool anyReady = _producers.Exists(IsReadyProducer);
            // Why no selected producer can take orders, or null when one can.
            string blocker = anyReady ? null : _producers[0].IsUnderConstruction ? "Under construction" : "Root the HQ first";
            bool interactable;
            string info;
            bool affordable;
            if (sim.Content.Researches.TryGetValue(id, out ResearchDefinition research))
            {
                slot.Background.color = ResearchColor;
                bool done = player.HasResearched(id);
                affordable = player.CanAfford(research.CostCommon, research.CostRare);
                interactable = !done && anyReady;
                info = done ? "Done"
                    : blocker ?? (EconomyRules.IsResearchQueued(sim, _commands.LocalPlayer, id) ? "In progress"
                    : CostText(research.CostCommon, research.CostRare, -1, EconomyRules.QueueEntryTicks(sim, id), rate));
            }
            else if (sim.Content.Units.TryGetValue(id, out UnitDefinition unit))
            {
                slot.Background.color = TrainColor;
                string missing = EconomyRules.FirstMissingRequirement(sim, _commands.LocalPlayer, unit);
                affordable = player.CanAfford(unit.CostCommon, unit.CostRare);
                interactable = missing == null && anyReady;
                info = missing != null ? "Requires " + HudBuild.NameOf(sim, missing)
                    : blocker ?? CostText(unit.CostCommon, unit.CostRare, EconomyRules.SupplyOf(sim, unit), EconomyRules.BuildTicks(sim, unit), rate);
            }
            else
            {
                slot.Background.color = TrainColor;
                affordable = true;
                interactable = false;
                info = "Unknown";
            }
            slot.Button.interactable = interactable;
            slot.Name.text = SlotKeys[index] + "  " + HudBuild.NameOf(sim, id);
            slot.Info.text = info;
            slot.Info.color = interactable && !affordable ? UnaffordableColor : HudBuild.TextColor;
        }

        /// <summary>The cost line of a button: "50c 0r 1s 12.0 s"; supply is left out when negative (research).</summary>
        /// <param name="common">Common cost. Unit: resource units. Range: 0 upward.</param>
        /// <param name="rare">Rare cost. Unit: resource units. Range: 0 upward.</param>
        /// <param name="supply">Supply used, or -1 for none. Unit: supply.</param>
        /// <param name="ticks">Time to produce. Unit: ticks. Range: 0 upward.</param>
        /// <param name="rate">Ticks per second.</param>
        private static string CostText(int common, int rare, int supply, int ticks, int rate)
        {
            return common + "c " + rare + "r " + (supply >= 0 ? supply + "s " : "") + HudBuild.Seconds(ticks, rate);
        }

        /// <summary>Sends the slot's order to the ready producer with the shortest queue (ties lowest EntityId), or the lowest-ID producer when none is ready.</summary>
        /// <param name="index">Slot index. Range: 0 to the entry count - 1.</param>
        public void Click(int index)
        {
            if (_commands == null || !_commands.IsReady || index >= _entries.Count || _producers.Count == 0)
            {
                return;
            }
            Unit best = null;
            foreach (Unit producer in _producers)
            {
                // _producers is in ascending ID order, so a strict comparison keeps the lowest ID on a tie.
                if (IsReadyProducer(producer) && (best == null || producer.ProductionQueue.Count < best.ProductionQueue.Count))
                {
                    best = producer;
                }
            }
            if (best == null)
            {
                best = _producers[0];
            }
            _commands.Submit(new TrainUnitCommand(_commands.Tick, _commands.LocalPlayer, _commands.NextSequence(), best.Id, _entries[index]));
        }

        /// <summary>Draws the producer's queue: one icon per entry, the head with its progress, and "Queue n/max".</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="producer">The producer whose queue is shown (the lowest-ID selected one).</param>
        private void DrawQueue(Simulation sim, Unit producer)
        {
            IReadOnlyList<string> queue = producer.ProductionQueue;
            _queueLabel.text = "Queue " + queue.Count + "/" + sim.Tuning.Economy.MaxQueueLength + (_producers.Count > 1 ? "\n(" + _producers.Count + " selected)" : "");
            for (int i = 0; i < queue.Count; i++)
            {
                QueueIcon icon = i < _queueIcons.Count ? _queueIcons[i] : CreateQueueIcon(i);
                icon.Background.gameObject.SetActive(true);
                string name = HudBuild.NameOf(sim, queue[i]);
                icon.Label.text = name.Length > 4 ? name.Substring(0, 4) : name;
                // Head progress: 1 - remaining / total ticks of the head entry (unit or research). Unit: fraction. Range: 0 to 1.
                float progress = 0f;
                if (i == 0)
                {
                    int total = Mathf.Max(1, EconomyRules.QueueEntryTicks(sim, queue[0]));
                    progress = Mathf.Clamp01(1f - producer.ProductionTicksRemaining / (float)total);
                }
                icon.Progress.sizeDelta = new Vector2((QueueIconSize - 6f) * progress, icon.Progress.sizeDelta.y);
            }
            for (int i = queue.Count; i < _queueIcons.Count; i++)
            {
                _queueIcons[i].Background.gameObject.SetActive(false);
            }
        }

        /// <summary>Creates button slot <paramref name="index"/> (row-major from the top left of the card) with its click handler.</summary>
        /// <param name="index">Slot index. Range: 0 to 7.</param>
        private Slot CreateSlot(int index)
        {
            // Button position from the card's top-left corner. Unit: reference pixels.
            Vector2 position = new Vector2(Gap + (index % Columns) * (ButtonWidth + Gap), -Gap - (index / Columns) * (ButtonHeight + Gap));
            Image background = HudBuild.Image(_card.transform, "Slot " + (index + 1), new Vector2(0f, 1f), position, new Vector2(ButtonWidth, ButtonHeight), TrainColor);
            background.raycastTarget = true;
            Button button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            button.colors = colors;
            button.targetGraphic = background;
            // Captured copy of the index for the click handler.
            int captured = index;
            button.onClick.AddListener(() => Click(captured));
            Text name = HudBuild.Text(background.transform, "Name", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(ButtonWidth - 8f, 40f), 17, TextAnchor.UpperCenter);
            // Long names ("Thunderhead Plans") shrink to fit two lines instead of being cut off. Unit: reference pixels.
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 10;
            name.resizeTextMaxSize = 17;
            Text info = HudBuild.Text(background.transform, "Info", new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(ButtonWidth - 8f, 32f), 15, TextAnchor.LowerCenter);
            background.gameObject.SetActive(false);
            return new Slot { Button = button, Background = background, Name = name, Info = info };
        }

        /// <summary>Creates queue icon <paramref name="index"/> to the right of the label and adds it to the pool.</summary>
        /// <param name="index">Queue position. Range: 0 upward; must equal the pool size.</param>
        private QueueIcon CreateQueueIcon(int index)
        {
            // Icon position from the strip's left middle. Unit: reference pixels.
            Vector2 position = new Vector2(120f + index * (QueueIconSize + 6f), 0f);
            Image background = HudBuild.Image(_queue.transform, "Queue " + index, new Vector2(0f, 0.5f), position, new Vector2(QueueIconSize, QueueIconSize), TrainColor);
            Text label = HudBuild.Text(background.transform, "Label", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(QueueIconSize, 22f), 14, TextAnchor.UpperCenter);
            Image back = HudBuild.Image(background.transform, "Progress Back", new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(QueueIconSize - 6f, 6f), new Color(0f, 0f, 0f, 0.6f));
            RectTransform progress = HudBuild.Image(back.transform, "Progress", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 6f), new Color(0.95f, 0.8f, 0.3f, 1f)).rectTransform;
            QueueIcon icon = new QueueIcon { Background = background, Label = label, Progress = progress };
            _queueIcons.Add(icon);
            return icon;
        }
    }
}

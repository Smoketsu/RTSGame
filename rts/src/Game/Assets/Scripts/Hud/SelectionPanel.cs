// rts/src/Game/Assets/Scripts/Hud/SelectionPanel.cs
// The HUD selected-unit panel, bottom centre (player_interface.md UI-1). One unit selected: its name, a health bar and the
// state lines that apply to it (shield dome, HQ state and transition countdown, Last Stand, construction progress, gather state
// and cargo, statuses, Momentum stacks). Several selected: one icon per unit with a health bar, grouped by definition, groups in
// the order of their lowest EntityId and units in EntityId order. Reads the selection from SelectionController and every value
// from the simulation each frame; never writes to either. Built from code under the HUD canvas. Part of Rts.Game (presentation only).
using System.Collections.Generic;
using System.Text;
using Rts.Game.Bootstrap;
using Rts.Game.UnitSelection;
using Rts.Sim;
using Rts.Sim.Economy;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Skai;
using Rts.Sim.Units;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Rts.Sim.Core.EntityId;

namespace Rts.Game.Hud
{
    /// <summary>Shows the selected unit's details, or icons with health bars for a multiple selection.</summary>
    [DisallowMultipleComponent]
    public sealed class SelectionPanel : MonoBehaviour
    {
        /// <summary>One pooled icon of the multiple-selection view.</summary>
        private sealed class Icon
        {
            /// <summary>The icon's root (background square). Range: non-null.</summary>
            public Image Background;

            /// <summary>Short name shown on the icon. Range: non-null.</summary>
            public Text Label;

            /// <summary>Health bar fill; its width is the health fraction of the bar width. Range: non-null.</summary>
            public RectTransform HealthFill;
        }

        /// <summary>Panel width. Unit: reference pixels. Range: 400 to 1200; default 780.</summary>
        [SerializeField, Range(400f, 1200f)] private float panelWidth = 780f;

        /// <summary>Panel height. Unit: reference pixels. Range: 100 to 300; default 180.</summary>
        [SerializeField, Range(100f, 300f)] private float panelHeight = 180f;

        /// <summary>Icons per row in the multiple-selection view. Unit: icons. Range: 4 to 20; default 12.</summary>
        [SerializeField, Range(4, 20)] private int iconsPerRow = 12;

        /// <summary>Rows of icons in the multiple-selection view; further units are counted in a "+N" label. Unit: rows. Range: 1 to 4; default 2.</summary>
        [SerializeField, Range(1, 4)] private int iconRows = 2;

        /// <summary>Health bar colour. Unit: RGBA, each channel 0 to 1; green.</summary>
        [SerializeField] private Color healthColor = new Color(0.3f, 0.9f, 0.35f, 1f);

        /// <summary>Edge length of one icon. Unit: reference pixels.</summary>
        private const float IconSize = 56f;

        /// <summary>Gap between icons. Unit: reference pixels.</summary>
        private const float IconGap = 6f;

        /// <summary>Height of a health bar. Unit: reference pixels.</summary>
        private const float BarHeight = 8f;

        /// <summary>Width of the single-unit health bar. Unit: reference pixels.</summary>
        private const float SingleBarWidth = 360f;

        /// <summary>The panel background; hidden while nothing is selected. Range: non-null after Awake.</summary>
        private Image _panel;

        /// <summary>Single-unit title (display name). Range: non-null after Awake.</summary>
        private Text _title;

        /// <summary>Single-unit health bar background. Range: non-null after Awake.</summary>
        private Image _healthBack;

        /// <summary>Single-unit health bar fill. Range: non-null after Awake.</summary>
        private RectTransform _healthFill;

        /// <summary>Single-unit lines: health numbers and every state that applies. Range: non-null after Awake.</summary>
        private Text _details;

        /// <summary>"+N" count of selected units beyond the icons shown. Range: non-null after Awake.</summary>
        private Text _overflow;

        /// <summary>Icons created so far, reused frame to frame. Range: 0 to iconsPerRow x iconRows.</summary>
        private readonly List<Icon> _icons = new List<Icon>();

        /// <summary>Selected living units, sorted for display each frame. Presentation scratch list.</summary>
        private readonly List<Unit> _units = new List<Unit>();

        /// <summary>Reused builder for the detail lines. Presentation only.</summary>
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>The local command path, for the match. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>The selection source. Range: null until found.</summary>
        private SelectionController _selection;

        /// <summary>Builds the panel background, the single-unit widgets and the overflow label.</summary>
        private void Awake()
        {
            _panel = HudBuild.Image(transform, "Selection Panel", new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(panelWidth, panelHeight), HudBuild.PanelColor);
            // The panel blocks clicks so they do not select or order through it.
            _panel.raycastTarget = true;
            Transform panel = _panel.transform;
            // Inner width left for content. Unit: reference pixels.
            float inner = panelWidth - 32f;
            _title = HudBuild.Text(panel, "Title", new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(inner, 32f), 26, TextAnchor.UpperLeft);
            _healthBack = HudBuild.Image(panel, "Health Back", new Vector2(0f, 1f), new Vector2(16f, -46f), new Vector2(SingleBarWidth, BarHeight * 1.5f), new Color(0f, 0f, 0f, 0.6f));
            _healthFill = HudBuild.Image(_healthBack.transform, "Health Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(SingleBarWidth, BarHeight * 1.5f), healthColor).rectTransform;
            _details = HudBuild.Text(panel, "Details", new Vector2(0f, 1f), new Vector2(16f, -64f), new Vector2(inner, panelHeight - 72f), 20, TextAnchor.UpperLeft);
            _overflow = HudBuild.Text(panel, "Overflow", new Vector2(1f, 0f), new Vector2(-12f, 8f), new Vector2(120f, 28f), 20, TextAnchor.LowerRight);
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Refreshes the panel from the selection and the simulation.</summary>
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
            _units.Clear();
            if (_commands != null && _selection != null && _commands.IsReady)
            {
                foreach (EntityId id in _selection.Selected)
                {
                    if (_commands.Host.Match.Units.TryGet(id, out Unit unit))
                    {
                        _units.Add(unit);
                    }
                }
            }
            _panel.gameObject.SetActive(_units.Count > 0);
            if (_units.Count == 0)
            {
                return;
            }
            bool single = _units.Count == 1;
            _title.gameObject.SetActive(single);
            _healthBack.gameObject.SetActive(single);
            _details.gameObject.SetActive(single);
            if (single)
            {
                ShowSingle(_commands.Host.Match, _units[0]);
                ShowIcons(0);
                _overflow.text = "";
            }
            else
            {
                SortForIcons(_units);
                ShowIcons(_units.Count);
            }
        }

        /// <summary>Fills the single-unit title, health bar and state lines.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="unit">The one selected unit.</param>
        private void ShowSingle(Simulation sim, Unit unit)
        {
            UnitDefinition definition = unit.Definition;
            // Tick rate for every countdown. Unit: ticks per second. Range: 10 to 60.
            int rate = sim.Clock.TicksPerSecond;
            _title.text = definition.DisplayName;
            SetFill(_healthFill, SingleBarWidth, HealthFraction(unit));
            _builder.Clear();
            _builder.Append("Health ").Append(HudBuild.Whole(unit.Health)).Append(" / ").Append(HudBuild.Whole(definition.MaxHealth));
            if (definition.ShieldDome != null)
            {
                _builder.Append("    Shield ").Append(HudBuild.Whole(unit.ShieldPool)).Append(" / ").Append(HudBuild.Whole(definition.ShieldDome.Pool));
                if (unit.ShieldDown)
                {
                    _builder.Append(" (down)");
                }
            }
            if (definition.Hq != null)
            {
                _builder.Append("\nHQ: ").Append(unit.HqState);
                if (unit.HqTransitionTicksRemaining > 0)
                {
                    _builder.Append(", ").Append(HudBuild.Seconds(unit.HqTransitionTicksRemaining, rate)).Append(" left");
                }
            }
            if (unit.IsInvulnerable)
            {
                _builder.Append("\nLast Stand: invulnerable for ").Append(HudBuild.Seconds(unit.LastStandTicksRemaining, rate));
            }
            if (unit.IsUnderConstruction)
            {
                // Total build time of this definition. Unit: ticks. Range: 1 upward.
                int total = Mathf.Max(1, EconomyRules.BuildTicks(sim, definition));
                _builder.Append("\nUnder construction: ").Append(Mathf.FloorToInt(100f * (1f - unit.ConstructionTicksRemaining / (float)total))).Append('%');
            }
            if (definition.Economy.CanGather)
            {
                _builder.Append("\nWorker: ").Append(unit.GatherState);
                if (unit.CarriedAmount > 0)
                {
                    _builder.Append(", carrying ").Append(unit.CarriedAmount).Append(' ').Append(unit.CarriedKind);
                }
            }
            AppendStatuses(sim, unit, rate);
            _details.text = _builder.ToString();
        }

        /// <summary>Adds a line for the statuses and Momentum stacks the unit has, if any.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="unit">The selected unit.</param>
        /// <param name="rate">Ticks per second.</param>
        private void AppendStatuses(Simulation sim, Unit unit, int rate)
        {
            // Length of the builder before the status line, to tell whether anything was added.
            int start = _builder.Length;
            if (unit.Statuses.IsGrounded)
            {
                _builder.Append("Grounded ").Append(HudBuild.Seconds(Mathf.Max(0, unit.Statuses.GroundedUntilTick - sim.Clock.Tick), rate)).Append("   ");
            }
            if (unit.Statuses.IsSlowed)
            {
                _builder.Append("Slowed   ");
            }
            if (unit.Statuses.DrenchHits > 0)
            {
                _builder.Append("Drenched x").Append(unit.Statuses.DrenchHits).Append("   ");
            }
            if (sim.Mechanics.TryGetValue(MomentumMechanic.MechanicId, out IFactionMechanic mechanic)
                && mechanic is MomentumMechanic momentum && momentum.TryGetState(unit.Id, out MomentumState state) && state.Stacks > 0)
            {
                _builder.Append("Momentum ").Append(state.Stacks);
            }
            if (_builder.Length > start)
            {
                _builder.Insert(start, "\n");
            }
        }

        /// <summary>Shows the first icons for the sorted selection and hides the rest; sets the "+N" label.</summary>
        /// <param name="count">Units to show. Range: 0 upward; more than fit are counted in the label.</param>
        private void ShowIcons(int count)
        {
            // Icons that fit in the panel. Unit: icons.
            int capacity = iconsPerRow * iconRows;
            int shown = Mathf.Min(count, capacity);
            for (int i = 0; i < shown; i++)
            {
                Icon icon = i < _icons.Count ? _icons[i] : CreateIcon(i);
                Unit unit = _units[i];
                icon.Background.gameObject.SetActive(true);
                string name = unit.Definition.DisplayName;
                icon.Label.text = name.Length > 4 ? name.Substring(0, 4) : name;
                SetFill(icon.HealthFill, IconSize - 8f, HealthFraction(unit));
            }
            for (int i = shown; i < _icons.Count; i++)
            {
                _icons[i].Background.gameObject.SetActive(false);
            }
            _overflow.text = count > capacity ? "+" + (count - capacity) : "";
        }

        /// <summary>Creates the icon for slot <paramref name="index"/> (row-major from the top left) and adds it to the pool.</summary>
        /// <param name="index">Slot index. Range: 0 to iconsPerRow x iconRows - 1; must equal the pool size.</param>
        private Icon CreateIcon(int index)
        {
            // Slot position from the panel's top-left corner. Unit: reference pixels.
            Vector2 position = new Vector2(16f + (index % iconsPerRow) * (IconSize + IconGap), -14f - (index / iconsPerRow) * (IconSize + IconGap));
            Image background = HudBuild.Image(_panel.transform, "Icon " + index, new Vector2(0f, 1f), position, new Vector2(IconSize, IconSize), new Color(0.2f, 0.3f, 0.5f, 0.9f));
            Text label = HudBuild.Text(background.transform, "Label", new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(IconSize, 24f), 16, TextAnchor.UpperCenter);
            Image back = HudBuild.Image(background.transform, "Health Back", new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(IconSize - 8f, BarHeight), new Color(0f, 0f, 0f, 0.6f));
            RectTransform fill = HudBuild.Image(back.transform, "Health Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(IconSize - 8f, BarHeight), healthColor).rectTransform;
            Icon icon = new Icon { Background = background, Label = label, HealthFill = fill };
            _icons.Add(icon);
            return icon;
        }

        /// <summary>Health as a fraction of maximum health. Unit: fraction. Range: 0 to 1.</summary>
        /// <param name="unit">A living unit.</param>
        private static float HealthFraction(Unit unit)
        {
            float max = unit.Definition.MaxHealth.ToFloatForPresentation();
            return max > 0f ? Mathf.Clamp01(unit.Health.ToFloatForPresentation() / max) : 0f;
        }

        /// <summary>Sets a left-anchored bar fill to a fraction of the full width.</summary>
        /// <param name="fill">The fill rect (anchored and pivoted at its left middle).</param>
        /// <param name="fullWidth">Width at 100 percent. Unit: reference pixels.</param>
        /// <param name="fraction">Filled part. Unit: fraction. Range: 0 to 1.</param>
        private static void SetFill(RectTransform fill, float fullWidth, float fraction)
        {
            fill.sizeDelta = new Vector2(fullWidth * fraction, fill.sizeDelta.y);
        }

        /// <summary>Orders units by EntityId, then groups them by definition with groups in the order of their lowest EntityId (stable).</summary>
        /// <param name="units">The selected living units; reordered in place.</param>
        private static void SortForIcons(List<Unit> units)
        {
            units.Sort((a, b) => a.Id.CompareTo(b.Id));
            // Group rank per definition ID: the position of its first (lowest-ID) unit. Presentation scratch.
            Dictionary<string, int> rank = new Dictionary<string, int>();
            for (int i = 0; i < units.Count; i++)
            {
                if (!rank.ContainsKey(units[i].Definition.Id))
                {
                    rank.Add(units[i].Definition.Id, i);
                }
            }
            units.Sort((a, b) =>
            {
                int byGroup = rank[a.Definition.Id].CompareTo(rank[b.Definition.Id]);
                return byGroup != 0 ? byGroup : a.Id.CompareTo(b.Id);
            });
        }
    }
}

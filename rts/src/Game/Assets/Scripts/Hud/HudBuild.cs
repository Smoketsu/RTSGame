// rts/src/Game/Assets/Scripts/Hud/HudBuild.cs
// Small helpers that build the uGUI pieces every HUD panel uses (a rect, a text, a flat image), so panels are made from code
// under the one HUD canvas (player_interface.md rule 7) without repeating the same setup. Sizes are reference pixels of the
// 1920 x 1080 canvas. Also the shared formatting of ticks as seconds and health numbers. Part of Rts.Game (presentation only).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>uGUI construction and formatting helpers for HUD panels.</summary>
    public static class HudBuild
    {
        /// <summary>Default text colour of the HUD. Unit: RGBA, each channel 0 to 1; warm white.</summary>
        public static readonly Color TextColor = new Color(1f, 0.95f, 0.85f, 1f);

        /// <summary>Background colour of HUD panels. Unit: RGBA, each channel 0 to 1; translucent near-black.</summary>
        public static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.08f, 0.78f);

        /// <summary>The built-in font (no font asset to import). Range: non-null after first use.</summary>
        private static Font _font;

        /// <summary>Creates a child object with a RectTransform anchored at one point of its parent.</summary>
        /// <param name="parent">Parent transform (the canvas or a panel).</param>
        /// <param name="name">Object name in the Hierarchy.</param>
        /// <param name="anchor">Anchor and pivot in the parent, each axis 0 to 1 (0,0 bottom left; 0.5,1 top centre).</param>
        /// <param name="position">Offset of the pivot from the anchor. Unit: reference pixels.</param>
        /// <param name="size">Width and height. Unit: reference pixels.</param>
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Creates a text with an outline, not a raycast target.</summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">Object name in the Hierarchy.</param>
        /// <param name="anchor">Anchor and pivot in the parent, each axis 0 to 1.</param>
        /// <param name="position">Offset of the pivot from the anchor. Unit: reference pixels.</param>
        /// <param name="size">Width and height. Unit: reference pixels.</param>
        /// <param name="fontSize">Font size. Unit: reference pixels. Range: 8 to 64.</param>
        /// <param name="alignment">Text alignment inside the rect.</param>
        public static Text Text(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            RectTransform rect = Rect(parent, name, anchor, position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            text.font = _font;
            text.fontSize = fontSize;
            text.color = TextColor;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            text.text = "";
            return text;
        }

        /// <summary>Creates a flat coloured rectangle, not a raycast target.</summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">Object name in the Hierarchy.</param>
        /// <param name="anchor">Anchor and pivot in the parent, each axis 0 to 1.</param>
        /// <param name="position">Offset of the pivot from the anchor. Unit: reference pixels.</param>
        /// <param name="size">Width and height. Unit: reference pixels.</param>
        /// <param name="color">Fill colour, RGBA 0 to 1.</param>
        public static Image Image(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(parent, name, anchor, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A tick count as seconds with one decimal, "4.8 s". Unit: input ticks, output seconds.</summary>
        /// <param name="ticks">Tick count. Range: 0 upward.</param>
        /// <param name="ticksPerSecond">The match's tick rate (sim.Clock.TicksPerSecond). Range: 10 to 60.</param>
        public static string Seconds(int ticks, int ticksPerSecond)
        {
            return (ticks / (float)ticksPerSecond).ToString("0.0") + " s";
        }

        /// <summary>The display name of a unit or research ID (production queues and requirements hold both), or the ID itself when neither catalog has it.</summary>
        /// <param name="sim">The running match (for its content catalog).</param>
        /// <param name="id">A unit or research ID.</param>
        public static string NameOf(Simulation sim, string id)
        {
            if (sim.Content.Units.TryGetValue(id, out UnitDefinition unit))
            {
                return unit.DisplayName;
            }
            return sim.Content.Researches.TryGetValue(id, out ResearchDefinition research) ? research.DisplayName : id;
        }

        /// <summary>A fixed-point amount as a whole number for display, rounded up so a unit with any health left never shows 0.</summary>
        /// <param name="value">Amount, for example health. Unit: fixed-point. Range: 0 upward.</param>
        public static int Whole(Fix64 value)
        {
            return Mathf.CeilToInt(value.ToFloatForPresentation());
        }
    }
}

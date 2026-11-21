// rts/src/Game/Assets/Scripts/Hud/MessageLine.cs
// The HUD message line, top centre (player_interface.md UI-0): the last few refusals of the local player's commands and other
// notices, each shown for a few seconds. Reads CommandRejectedEvent from SimulationHost.EventRaised, keeps only those whose
// command the local player issued, and words them with RejectionTexts. Other HUD panels post notices with Show. Builds its own
// uGUI Text under the HUD canvas (AutoBootstrap.EnsureHud). Never writes to the simulation. Part of Rts.Game (presentation only).
using System.Collections.Generic;
using System.Text;
using Rts.Game.Bootstrap;
using Rts.Sim.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Shows recent local rejections and notices at the top centre of the screen.</summary>
    [DisallowMultipleComponent]
    public sealed class MessageLine : MonoBehaviour
    {
        /// <summary>One line on screen and when it goes away.</summary>
        private struct Entry
        {
            /// <summary>Text shown. Range: non-empty.</summary>
            public string Text;

            /// <summary>Real time at which the line is removed. Unit: seconds of Time.unscaledTime.</summary>
            public float ExpiresAt;
        }

        /// <summary>How long each line stays. Unit: seconds (real time). Range: 0.5 to 20; default 4.</summary>
        [SerializeField, Range(0.5f, 20f)] private float secondsPerMessage = 4f;

        /// <summary>Most lines shown at once; the oldest is dropped first. Unit: lines. Range: 1 to 10; default 4.</summary>
        [SerializeField, Range(1, 10)] private int maxLines = 4;

        /// <summary>Distance of the line block's top edge below the top of the screen, leaving room for the timeline bar and Core widget. Unit: reference pixels (1920 x 1080). Range: 0 to 540; default 150.</summary>
        [SerializeField, Range(0f, 540f)] private float topOffset = 150f;

        /// <summary>Font size of the lines. Unit: reference pixels. Range: 10 to 48; default 22.</summary>
        [SerializeField, Range(10, 48)] private int fontSize = 22;

        /// <summary>Text colour. Unit: RGBA, each channel 0 to 1; default warm white.</summary>
        [SerializeField] private Color textColor = new Color(1f, 0.92f, 0.75f, 1f);

        /// <summary>Lines currently shown, oldest first. Range: 0 to maxLines entries.</summary>
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>Reused builder for the joined text. Presentation only.</summary>
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>The uGUI text that draws the lines. Range: non-null after Awake.</summary>
        private Text _text;

        /// <summary>The local command path, for the local player and the host's events. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>True once subscribed to the host's events. Unit: on/off.</summary>
        private bool _subscribed;

        /// <summary>True when the lines changed since the text was last rebuilt. Unit: on/off.</summary>
        private bool _dirty;

        /// <summary>Adds a line, dropping the oldest when the block is full.</summary>
        /// <param name="message">Text to show; null or empty is ignored.</param>
        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            if (_entries.Count >= maxLines)
            {
                _entries.RemoveAt(0);
            }
            _entries.Add(new Entry { Text = message, ExpiresAt = Time.unscaledTime + secondsPerMessage });
            _dirty = true;
        }

        /// <summary>Builds the Text child, anchored top centre.</summary>
        private void Awake()
        {
            GameObject textObject = new GameObject("Message Line Text", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
            // Block size. Unit: reference pixels. Wide enough for one long refusal per line.
            rect.sizeDelta = new Vector2(1000f, fontSize * 1.4f * maxLines);
            _text = textObject.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = fontSize;
            _text.color = textColor;
            _text.alignment = TextAnchor.UpperCenter;
            _text.raycastTarget = false;
            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            _text.text = "";
        }

        /// <summary>Subscribes once the host exists and removes expired lines.</summary>
        private void Update()
        {
            if (!_subscribed)
            {
                if (_commands == null)
                {
                    _commands = FindAnyObjectByType<LocalCommands>();
                }
                if (_commands != null && _commands.Host != null)
                {
                    _commands.Host.EventRaised += OnSimEvent;
                    _subscribed = true;
                }
            }
            // Real time now. Unit: seconds.
            float now = Time.unscaledTime;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].ExpiresAt <= now)
                {
                    _entries.RemoveAt(i);
                    _dirty = true;
                }
            }
            if (_dirty)
            {
                _dirty = false;
                _builder.Clear();
                foreach (Entry entry in _entries)
                {
                    _builder.Append(entry.Text).Append('\n');
                }
                _text.text = _builder.ToString();
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

        /// <summary>Shows refusals of the local player's own commands; everything else is ignored.</summary>
        /// <param name="simEvent">An event of the tick that just ran.</param>
        private void OnSimEvent(SimEvent simEvent)
        {
            if (simEvent is CommandRejectedEvent rejected && _commands.IsReady && rejected.Command.Issuer == _commands.LocalPlayer)
            {
                Show(RejectionTexts.For(rejected.Reason));
            }
        }
    }
}

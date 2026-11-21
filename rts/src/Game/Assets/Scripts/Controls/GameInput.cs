// rts/src/Game/Assets/Scripts/Controls/GameInput.cs
// Keyboard and mouse reads for every input script, through the Input System package (com.unity.inputsystem). It keeps the
// signatures of the legacy UnityEngine.Input calls the scripts used (GetKey, GetKeyDown, GetMouseButton*, mousePosition,
// mouseScrollDelta) so the call sites read the same. Why: the Funplay MCP input tools (simulate_key_press, simulate_mouse_click,
// simulate_mouse_drag) inject only Input System events, and the interface spec requires Play Mode checks to drive the match
// through them. Null-safe: with no keyboard or mouse every read is "not pressed" / zero. Part of Rts.Game (presentation only).
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rts.Game.Controls
{
    /// <summary>Legacy-shaped keyboard and mouse reads backed by the Input System.</summary>
    public static class GameInput
    {
        /// <summary>Mouse position in screen pixels, origin bottom left (z is 0). Unit: pixels. Range: the screen; zero without a mouse.</summary>
        public static Vector3 mousePosition => Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;

        /// <summary>Scroll this frame; y is wheel notches, about +1 per notch away from the user (Input System default: uniform across platforms). Unit: notches. Range: about -1 to 1 per frame.</summary>
        public static Vector2 mouseScrollDelta => Mouse.current != null ? Mouse.current.scroll.ReadValue() : Vector2.zero;

        /// <summary>
        /// True while the real cursor is over the game: in the editor, over the Game view window; in a player, while the window has focus.
        /// The Input System keeps the last pointer position (or 0,0 before the first event) when the cursor is elsewhere, unlike the legacy
        /// API, so edge scrolling must check this or it pans forever toward the edge the cursor left by.
        /// </summary>
        public static bool PointerOverGame
        {
            get
            {
#if UNITY_EDITOR
                UnityEditor.EditorWindow window = UnityEditor.EditorWindow.mouseOverWindow;
                return window != null && window.GetType().Name == "GameView";
#else
                return Application.isFocused;
#endif
            }
        }

        /// <summary>True while the key is held.</summary>
        /// <param name="key">A key from the set <see cref="ToKey"/> maps.</param>
        public static bool GetKey(KeyCode key)
        {
            return Keyboard.current != null && Keyboard.current[ToKey(key)].isPressed;
        }

        /// <summary>
        /// True on the frame the key went down: the Input System's own edge, or held now but not at the previous frame that asked about
        /// this key. The second test is needed because Funplay's simulate_key_press is processed outside the player-loop update, so
        /// wasPressedThisFrame is already false when Update runs; the held state is still seen. Every caller in the same frame gets the same answer.
        /// </summary>
        /// <param name="key">A key from the set <see cref="ToKey"/> maps.</param>
        public static bool GetKeyDown(KeyCode key)
        {
            if (Keyboard.current == null)
            {
                return false;
            }
            Key mapped = ToKey(key);
            // Frame now. Unit: frames (Time.frameCount).
            int frame = Time.frameCount;
            KeyPoll poll;
            KeyPolls.TryGetValue(mapped, out poll);
            if (poll.Frame != frame)
            {
                // Held now. Unit: on/off.
                bool held = Keyboard.current[mapped].isPressed;
                poll = new KeyPoll { Frame = frame, Held = held, Down = Keyboard.current[mapped].wasPressedThisFrame || (held && !poll.Held) };
                KeyPolls[mapped] = poll;
            }
            return poll.Down;
        }

        /// <summary>The last answer of GetKeyDown for one key.</summary>
        private struct KeyPoll
        {
            /// <summary>Frame of the answer. Unit: frames (Time.frameCount). Range: 0 (never asked) upward.</summary>
            public int Frame;

            /// <summary>Whether the key was held at that frame. Unit: on/off.</summary>
            public bool Held;

            /// <summary>The GetKeyDown answer for that frame. Unit: on/off.</summary>
            public bool Down;
        }

        /// <summary>Last GetKeyDown answer per key; presentation state only. Range: one entry per key ever asked about.</summary>
        private static readonly System.Collections.Generic.Dictionary<Key, KeyPoll> KeyPolls = new System.Collections.Generic.Dictionary<Key, KeyPoll>();

        /// <summary>True while the mouse button is held.</summary>
        /// <param name="button">0 left, 1 right, 2 middle.</param>
        public static bool GetMouseButton(int button)
        {
            return Button(button) != null && Button(button).isPressed;
        }

        /// <summary>True on the frame the mouse button went down.</summary>
        /// <param name="button">0 left, 1 right, 2 middle.</param>
        public static bool GetMouseButtonDown(int button)
        {
            return Button(button) != null && Button(button).wasPressedThisFrame;
        }

        /// <summary>True on the frame the mouse button was released.</summary>
        /// <param name="button">0 left, 1 right, 2 middle.</param>
        public static bool GetMouseButtonUp(int button)
        {
            return Button(button) != null && Button(button).wasReleasedThisFrame;
        }

        /// <summary>The mouse button control for a legacy button number, or null without a mouse.</summary>
        /// <param name="button">0 left, 1 right, 2 middle.</param>
        private static UnityEngine.InputSystem.Controls.ButtonControl Button(int button)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return null;
            }
            switch (button)
            {
                case 0: return mouse.leftButton;
                case 1: return mouse.rightButton;
                case 2: return mouse.middleButton;
                default: throw new System.ArgumentOutOfRangeException(nameof(button), button, "Only buttons 0 to 2 are mapped.");
            }
        }

        /// <summary>Maps a legacy key code to the Input System key: letters, top-row digits, arrows, Escape, F8, F10, Shift and Ctrl. Throws for any other key so a missing mapping shows at once.</summary>
        /// <param name="key">The legacy key code.</param>
        private static Key ToKey(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z)
            {
                return Key.A + (key - KeyCode.A);
            }
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9)
            {
                return Key.Digit1 + (key - KeyCode.Alpha1);
            }
            switch (key)
            {
                // Key.Digit0 comes after Digit9 in the Input System enum, unlike KeyCode.Alpha0.
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F10: return Key.F10;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                default: throw new System.ArgumentOutOfRangeException(nameof(key), key, "No Input System mapping for this key; add it to GameInput.ToKey.");
            }
        }
    }
}

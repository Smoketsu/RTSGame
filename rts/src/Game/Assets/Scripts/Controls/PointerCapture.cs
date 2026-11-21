// rts/src/Game/Assets/Scripts/Controls/PointerCapture.cs
// Which input mode owns the mouse. A targeting mode (build placement now; wall drag and ability targeting later) captures the
// pointer while it is active, and SelectionController ignores mouse buttons meanwhile, so a left click places a building instead
// of selecting and a right click cancels instead of ordering. Release keeps the capture through the current frame, because the
// click that ended a mode must not also reach the selection later in the same frame. Part of Rts.Game (presentation only).
using UnityEngine;

namespace Rts.Game.Controls
{
    /// <summary>The single owner of the mouse buttons, if any.</summary>
    public static class PointerCapture
    {
        /// <summary>The mode holding the pointer, or null. Unity null semantics: a destroyed owner reads as null.</summary>
        private static Object _owner;

        /// <summary>Last frame of a released capture that still counts as captured. Unit: frames (Time.frameCount). Range: -1 (none) upward.</summary>
        private static int _heldThroughFrame = -1;

        /// <summary>True while a mode holds the pointer, including the rest of the frame in which it was released. Unit: on/off.</summary>
        public static bool IsCaptured => _owner != null || Time.frameCount <= _heldThroughFrame;

        /// <summary>Gives the pointer to a mode (replacing any other owner).</summary>
        /// <param name="owner">The mode's component.</param>
        public static void Capture(Object owner)
        {
            _owner = owner;
        }

        /// <summary>Releases the pointer if this mode holds it; it stays captured until the end of this frame.</summary>
        /// <param name="owner">The mode's component.</param>
        public static void Release(Object owner)
        {
            if (_owner == owner)
            {
                _owner = null;
                _heldThroughFrame = Time.frameCount;
            }
        }
    }
}

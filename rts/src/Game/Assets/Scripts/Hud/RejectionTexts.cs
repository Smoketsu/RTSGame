// rts/src/Game/Assets/Scripts/Hud/RejectionTexts.cs
// The player-facing text for each CommandRejectedEvent reason (player_interface.md, the rejection tables of each package).
// One C# table for now (spec section 5, question 6: moves to a localisable strings file later). A reason mapped to null is
// not shown on the message line: either "logged only" (a UI bug, already in the Console through SimulationHost) or silent
// (the UI filters it before sending). A reason missing from the table is shown raw, so a new simulation reason is visible
// at once. Packages add their rows here. Part of Rts.Game (Unity side, presentation only).
using System.Collections.Generic;

namespace Rts.Game.Hud
{
    /// <summary>Maps simulation rejection reasons to message-line text.</summary>
    public static class RejectionTexts
    {
        /// <summary>Reason code to text; a null value means "do not show". Keys are the simulation's reason strings.</summary>
        private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
        {
            // UI-0: the orders that existed before the HUD.
            { "unknown_unit", "Unit lost" },
            { "not_owner", null },
            { "out_of_bounds", "Outside the map" },
            { "unknown_target", "Target lost" },
            { "no_weapon", "This unit cannot attack" },
            { "target_not_enemy", "Not an enemy" },
            { "cannot_target_layer", "Cannot hit that target" },
            { "cannot_gather", "Only workers gather" },
            { "unknown_field", null },
            { "field_depleted", "That field is empty" },
            { "no_drop_off", "No drop-off to return to" },
            { "not_hq", null },
            { "already_rooted", null },
            { "already_uprooted", null },
            { "transition_in_progress", "The HQ is still moving its roots" },
            { "not_near_resources", "Too far from resources to root" },
        };

        /// <summary>The text to show for a rejection reason: the table entry, the raw reason when it is not in the table, or null when it must not be shown.</summary>
        /// <param name="reason">The CommandRejectedEvent reason string.</param>
        public static string For(string reason)
        {
            return Texts.TryGetValue(reason, out string text) ? text : reason;
        }
    }
}

// rts/src/Game/Assets/Scripts/Hud/RejectionTexts.cs
// The player-facing text for each CommandRejectedEvent reason (player_interface.md, the rejection tables of each package).
// One C# table for now (spec section 5, question 6: moves to a localisable strings file later). A reason mapped to null is
// not shown on the message line: either "logged only" (a UI bug, already in the Console through SimulationHost) or silent
// (the UI filters it before sending). A reason missing from the table is shown raw, so a new simulation reason is visible
// at once. Packages add their rows here. Part of Rts.Game (Unity side, presentation only).
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Units;

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
            // UI-2: training (and research, which is a train order with a research ID, research_and_unlocks.md R1.10).
            { "under_construction", "Still under construction" },
            { "cannot_train", "This building cannot train that" },
            { "hq_not_rooted", "Root the HQ first" },
            { "missing_prerequisite", "Missing a requirement" },
            { "queue_full", "Queue is full" },
            { "insufficient_resources", "Not enough resources" },
            { "supply_cap", "Not enough supply" },
            { "unknown_definition", null },
            { "already_researched", "Already researched" },
            { "already_queued", "Already being researched" },
            // UI-3: structure orders, and the reasons a build order is cancelled (BuildOrderCancelledEvent, refunded).
            { "cannot_build", "Only workers can build" },
            { "not_a_structure", null },
            { "wrong_faction", null },
            { "site_unexplored", "You have not explored that spot" },
            { "tile_blocked", "Blocked" },
            { "tile_occupied", "A unit is in the way" },
            { "site_pending", "Already being built there" },
            { "no_base", "Defences must be built near a drop-off" },
            { "static_defence_cap", "This base has the most defences allowed" },
            { "worker_lost", "Builder lost" },
            { "site_blocked", "Site blocked" },
            { "site_occupied", "A unit stood on the site" },
            { "unreachable", "Builder could not reach the site" },
            { "new_order", null },
            // UI-4: wall and gate lines (the rasteriser and UI keep these from happening; logged only).
            { "not_a_wall", null },
            { "no_tiles", null },
            { "too_many_tiles", null },
            { "duplicate_tile", null },
            // UI-5: Downdraft and the wave.
            { "no_ability", null },
            { "not_wave_caster", null },
            { "out_of_range", "Out of range" },
            { "on_cooldown", "Not ready" },
            { "no_direction", "Aim away from the caster" },
            // Interface support: cancel a queued entry (I4) and rally points (I5). A stale queue click is refused quietly.
            { "no_entry", null },
            { "entry_mismatch", null },
            { "not_a_producer", null },
        };

        /// <summary>
        /// The text to show for a rejection event: like <see cref="For(string)"/>, but a missing prerequisite names the first missing
        /// building or research ("Requires Storm Spire") when the command says which definition it wanted.
        /// </summary>
        /// <param name="sim">The running match.</param>
        /// <param name="rejected">The rejection of a local command.</param>
        public static string For(Simulation sim, CommandRejectedEvent rejected)
        {
            // The definition the command wanted, for naming a missing prerequisite. Null for commands that name none.
            string wanted = rejected.Command is TrainUnitCommand train ? train.DefinitionId
                : rejected.Command is BuildStructureCommand build ? build.DefinitionId : null;
            if (rejected.Reason == "missing_prerequisite" && wanted != null && sim.Content.Units.TryGetValue(wanted, out UnitDefinition definition))
            {
                string missing = EconomyRules.FirstMissingRequirement(sim, rejected.Command.Issuer, definition);
                if (missing != null)
                {
                    return "Requires " + HudBuild.NameOf(sim, missing);
                }
            }
            return For(rejected.Reason);
        }

        /// <summary>The text to show for a rejection reason: the table entry, the raw reason when it is not in the table, or null when it must not be shown.</summary>
        /// <param name="reason">The CommandRejectedEvent reason string.</param>
        public static string For(string reason)
        {
            return Texts.TryGetValue(reason, out string text) ? text : reason;
        }
    }
}

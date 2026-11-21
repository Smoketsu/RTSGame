// rts/src/Game/Assets/Scripts/Hud/ResourceBar.cs
// The HUD resource, supply and clock bar, top right (player_interface.md UI-1): the local player's Common and Rare stock, supply
// used against the cap, and the match time as m:ss from the simulation clock. Read-only: every number is read from the simulation
// each frame, nothing is cached or computed here. Built from code under the HUD canvas (AutoBootstrap.EnsureHud).
// Part of Rts.Game (presentation only).
using Rts.Game.Bootstrap;
using Rts.Sim;
using Rts.Sim.Teams;
using UnityEngine;
using UnityEngine.UI;

namespace Rts.Game.Hud
{
    /// <summary>Shows the local player's resources, supply and the match time.</summary>
    [DisallowMultipleComponent]
    public sealed class ResourceBar : MonoBehaviour
    {
        /// <summary>Font size of the bar. Unit: reference pixels. Range: 12 to 40; default 22.</summary>
        [SerializeField, Range(12, 40)] private int fontSize = 22;

        /// <summary>The bar's text. Range: non-null after Awake.</summary>
        private Text _text;

        /// <summary>The local command path, for the local player and the match. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>Builds the background and the text, anchored top right.</summary>
        private void Awake()
        {
            // Bar size and inset from the top-right corner. Unit: reference pixels.
            Vector2 size = new Vector2(500f, 44f);
            Vector2 inset = new Vector2(-12f, -10f);
            Image background = HudBuild.Image(transform, "Resource Bar", new Vector2(1f, 1f), inset, size, HudBuild.PanelColor);
            background.raycastTarget = true;
            _text = HudBuild.Text(background.transform, "Resource Bar Text", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20f, 0f), fontSize, TextAnchor.MiddleRight);
        }

        /// <summary>Refreshes the text from the simulation.</summary>
        private void Update()
        {
            if (_commands == null)
            {
                _commands = FindAnyObjectByType<LocalCommands>();
            }
            if (_commands == null || !_commands.IsReady)
            {
                _text.text = "";
                return;
            }
            Simulation sim = _commands.Host.Match;
            if (!sim.Players.TryGet(_commands.LocalPlayer, out Player player))
            {
                _text.text = "";
                return;
            }
            // Whole seconds of match time. Unit: seconds. Range: 0 upward.
            int seconds = sim.Clock.Tick / sim.Clock.TicksPerSecond;
            _text.text = "Common " + player.Common + "   Rare " + player.Rare + "   Supply " + player.SupplyUsed + "/" + player.SupplyCap
                + "   " + (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }
    }
}
